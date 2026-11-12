import csv
import hmac
import io
import json
import math
import os
import secrets
import subprocess
import sys
import threading
import time
import uuid
import webbrowser
from collections import deque
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

PACKAGE = "com.qyam23.criticalvr"
ACTIVITY = PACKAGE + "/com.unity3d.player.UnityPlayerGameActivity"
ROOT = Path(sys.executable).resolve().parent if getattr(sys, "frozen", False) else Path(__file__).resolve().parent
ASSETS = Path(getattr(sys, "_MEIPASS", ROOT))
ADB = Path(os.environ.get("CRITICALVR_ADB", ROOT / "platform-tools" / "adb.exe"))
APK = ROOT / "CriticalVR-Quest-Development.apk"
NO_WINDOW = getattr(subprocess, "CREATE_NO_WINDOW", 0)
TOKEN = secrets.token_urlsafe(32)
COMMAND_FILE = "files/criticalvr-command.json"
SAMPLE_FILE = "files/criticalvr-sample.json"


def adb(*args, serial=None, data=None, timeout=6):
    target = ["-s", serial] if serial else []
    process = subprocess.run([str(ADB), *target, *args], input=data, capture_output=True,
                             timeout=timeout, creationflags=NO_WINDOW)
    if process.returncode:
        raise RuntimeError(process.stderr.decode("utf-8", "replace").strip() or "ADB command failed")
    return process.stdout


class Quest:
    def __init__(self):
        self.lock = threading.RLock()
        self.serial = None
        self.model = None
        self.message = "מחפש קסדה"
        self.sample = None
        self.received = 0.0
        self.next_heartbeat = 0.0
        self.observation_id = None
        self.samples = deque(maxlen=14400)
        self.mirror = None
        self.recording = False

    def scan(self):
        rows = [line.split() for line in adb("devices").decode().splitlines()[1:] if line.strip()]
        devices = [row[0] for row in rows if len(row) > 1 and row[1] == "device"]
        if len(devices) != 1:
            self.serial = self.model = None
            self.sample = None
            self.message = ("אשר USB debugging בקסדה" if any("unauthorized" in row for row in rows)
                            else "חבר Quest יחיד בכבל USB")
            return
        serial = devices[0]
        if serial != self.serial:
            self.sample = None
            self.model = adb("shell", "getprop", "ro.product.model", serial=serial).decode().strip()
            if self.model not in {"Quest 3", "Quest 3S", "Meta Quest 3", "Meta Quest 3S"}:
                self.serial = None
                self.message = "חבר Quest 3 או Quest 3S"
                return
            self.serial = serial
            self.next_heartbeat = 0.0
        self.message = "מחובר ב־USB"

    def send(self, action, **values):
        command_id = uuid.uuid4().hex
        payload = json.dumps(dict(id=command_id, action=action, **values), allow_nan=False).encode()
        write = "'umask 077; cat > files/.criticalvr-command.tmp && mv -f files/.criticalvr-command.tmp " + COMMAND_FILE + "'"
        adb("shell", "-T", "run-as", PACKAGE, "sh", "-c", write, serial=self.serial, data=payload)
        self.next_heartbeat = time.monotonic() + 2.5
        return command_id

    def read_sample(self):
        raw = adb("shell", "run-as", PACKAGE, "cat", SAMPLE_FILE, serial=self.serial)
        sample = json.loads(raw)
        if self.sample is None and sample["command"] != self.observation_id:
            return sample
        if not self.sample or (sample["session"], sample["seconds"]) != (self.sample["session"], self.sample["seconds"]):
            self.sample = sample
            self.received = time.monotonic()
            if self.recording:
                self.samples.append(dict(sample, pc_time=time.strftime("%Y-%m-%d %H:%M:%S")))
        return sample

    def poll(self):
        while True:
            with self.lock:
                try:
                    self.scan()
                    if self.serial and adb("shell", "pidof", PACKAGE, serial=self.serial).strip():
                        if time.monotonic() >= self.next_heartbeat:
                            self.observation_id = self.send("observe")
                        self.read_sample()
                except (RuntimeError, subprocess.TimeoutExpired, OSError, ValueError, KeyError):
                    pass
            time.sleep(0.4)

    def snapshot(self):
        with self.lock:
            fresh = self.sample if self.serial and time.monotonic() - self.received < 2.5 else None
            return dict(connected=bool(self.serial), model=self.model, message=self.message,
                        sample=fresh, recording=self.recording, samples=len(self.samples))

    def action(self, command):
        action = command.get("action")
        with self.lock:
            if action in {"record", "end-record"}:
                if action == "record":
                    self.samples.clear()
                self.recording = action == "record"
                return
            self.scan()
            if not self.serial:
                raise RuntimeError(self.message)
            serial = self.serial
            if action == "install":
                if not APK.is_file():
                    raise RuntimeError("קובץ ההתקנה חסר בתיקיית הקונסולה")
                adb("install", "-r", str(APK), serial=serial, timeout=120)
            if action in {"install", "launch"}:
                launched = adb("shell", "am", "start", "-n", ACTIVITY, serial=serial, timeout=25)
                if b"Starting:" not in launched or b"Error:" in launched:
                    raise RuntimeError(launched.decode("utf-8", "replace").strip() or "פתיחת האפליקציה נכשלה")
                self.sample = None
                self.next_heartbeat = 0.0
                self.observation_id = None
            elif action in {"set", "stop", "resume"}:
                values = {}
                if action == "set":
                    for name in ("strength", "depth"):
                        value = float(command[name])
                        if not math.isfinite(value) or not 0 <= value <= 1.3:
                            raise ValueError("עוצמה ועיוות חייבים להיות בטווח 0–130%")
                        values[name] = value
                command_id = self.send(action, **values)
                for _ in range(8):
                    time.sleep(0.12)
                    try:
                        sample = self.read_sample()
                        if sample["command"] == command_id:
                            if action == "resume" and sample["suspended"]:
                                raise RuntimeError("הרכב את הקסדה והחזר פוקוס לאפליקציה, ואז נסה שוב")
                            return
                    except (ValueError, KeyError):
                        continue
                raise RuntimeError("האפליקציה לא אישרה את הפעולה. ודא שהגרסה המצורפת פועלת בקסדה")
            elif action in {"quit", "sleep", "poweroff"}:
                adb("shell", "am", "force-stop", PACKAGE, serial=serial)
                self.sample = None
                if action == "sleep":
                    adb("shell", "input", "keyevent", "KEYCODE_SLEEP", serial=serial)
                elif action == "poweroff":
                    adb("shell", "reboot", "-p", serial=serial)
            elif action == "mirror":
                player = ROOT / "mirror" / "scrcpy.exe"
                if not player.is_file():
                    raise RuntimeError("קובץ השיקוף חסר בתיקיית הקונסולה")
                if self.mirror and self.mirror.poll() is None:
                    return
                env = dict(os.environ, ADB=str(ADB))
                self.mirror = subprocess.Popen([str(player), "-s", serial, "--no-audio", "--no-control",
                                                "--max-size=1280", "--max-fps=30", "--window-title=Critical-VR | Quest"],
                                               cwd=player.parent, env=env, creationflags=NO_WINDOW)
            else:
                raise ValueError("פעולה לא מוכרת")


quest = Quest()


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def valid_host(self):
        return self.headers.get("Host") == f"127.0.0.1:{self.server.server_port}"

    def respond(self, code, content, kind, download=None):
        self.send_response(code)
        self.send_header("Content-Type", kind)
        self.send_header("Content-Length", str(len(content)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("X-Frame-Options", "DENY")
        self.send_header("Referrer-Policy", "no-referrer")
        if download:
            self.send_header("Content-Disposition", f'attachment; filename="{download}"')
        self.end_headers()
        try:
            self.wfile.write(content)
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            pass

    def json_response(self, code, data):
        self.respond(code, json.dumps(data, ensure_ascii=False).encode(), "application/json; charset=utf-8")

    def authorized(self):
        return self.valid_host() and hmac.compare_digest(self.headers.get("X-CriticalVR-Token", ""), TOKEN)

    def do_GET(self):
        if self.path == "/" and self.valid_host():
            page = (ASSETS / "index.html").read_text(encoding="utf-8").replace("__SESSION_TOKEN__", TOKEN)
            self.respond(200, page.encode(), "text/html; charset=utf-8")
        elif not self.authorized():
            self.json_response(403, {"error": "Forbidden"})
        elif self.path == "/state":
            self.json_response(200, quest.snapshot())
        elif self.path == "/frame" and quest.serial:
            try:
                data = adb("exec-out", "screencap", "-p", serial=quest.serial, timeout=3)
                if not data.startswith(b"\x89PNG\r\n\x1a\n"):
                    raise RuntimeError("תמונת הקסדה לא זמינה")
                self.respond(200, data, "image/png")
            except (RuntimeError, OSError, subprocess.TimeoutExpired):
                self.json_response(503, {"error": "תמונת הקסדה אינה זמינה דרך ADB"})
        elif self.path == "/csv":
            with quest.lock:
                rows = list(quest.samples)
            stream = io.StringIO(newline="")
            fields = ["pc_time", "session", "seconds", "angularDegPerSec", "linearMetersPerSec",
                      "intensity", "strength", "depth", "suspended", "camerasReady", "reveal"]
            writer = csv.DictWriter(stream, fieldnames=fields, extrasaction="ignore")
            writer.writeheader()
            writer.writerows(rows)
            self.respond(200, stream.getvalue().encode("utf-8-sig"), "text/csv", "criticalvr-motion.csv")
        else:
            self.json_response(404, {"error": "Unavailable"})

    def do_POST(self):
        expected_origin = f"http://127.0.0.1:{self.server.server_port}"
        if not self.authorized() or self.headers.get("Origin") != expected_origin:
            self.json_response(403, {"error": "Forbidden"})
            return
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if self.path != "/action" or not 0 < length < 2048:
                raise ValueError("בקשה לא תקינה")
            command = json.loads(self.rfile.read(length))
            if not isinstance(command, dict):
                raise ValueError("בקשה לא תקינה")
            quest.action(command)
            self.json_response(200, {"ok": True})
        except (ValueError, KeyError, TypeError, RuntimeError, OSError, subprocess.TimeoutExpired) as error:
            self.json_response(400, {"error": str(error)})


def main():
    if not ADB.is_file():
        raise SystemExit("ADB missing. Extract the entire Critical-VR console package.")
    server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    threading.Thread(target=quest.poll, daemon=True).start()
    url = f"http://127.0.0.1:{server.server_port}/"
    print(f"Critical-VR Console: {url}\nClose this window to stop the desktop console.", flush=True)
    if "--no-browser" not in sys.argv:
        webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        if quest.mirror and quest.mirror.poll() is None:
            quest.mirror.terminate()
        server.server_close()


if __name__ == "__main__":
    main()
