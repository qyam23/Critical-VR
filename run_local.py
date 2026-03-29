from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parent
ENV_EXAMPLE = ROOT / ".env.example"
ENV_LOCAL = ROOT / ".env.local"
NODE_MODULES = ROOT / "node_modules"


def find_command(candidates: list[str]) -> str | None:
    for candidate in candidates:
        path = shutil.which(candidate)
        if path:
            return path
    return None


def ensure_env_file() -> None:
    if ENV_LOCAL.exists():
        return

    if ENV_EXAMPLE.exists():
        ENV_LOCAL.write_text(ENV_EXAMPLE.read_text(encoding="utf-8"), encoding="utf-8")
        print("Created .env.local from .env.example")
    else:
        ENV_LOCAL.write_text(
            'GEMINI_API_KEY="MY_GEMINI_API_KEY"\nAPP_URL="http://localhost:3000"\n',
            encoding="utf-8",
        )
        print("Created default .env.local")


def run(command: list[str]) -> int:
    process = subprocess.run(command, cwd=ROOT)
    return process.returncode


def main() -> int:
    node = find_command(["node.exe", "node"])
    npm = find_command(["npm.cmd", "npm"])

    if not node:
        print("Node.js was not found. Install Node.js 18+ and try again.")
        return 1

    if not npm:
        print("npm was not found. Install Node.js with npm and try again.")
        return 1

    ensure_env_file()

    if not NODE_MODULES.exists():
        print("Installing dependencies...")
        install_code = run([npm, "install"])
        if install_code != 0:
            return install_code

    print("Starting local simulator at http://localhost:3000")
    print("Press Ctrl+C to stop the server.")
    return run([npm, "run", "dev"])


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        print("\nServer stopped.")
        raise SystemExit(0)
