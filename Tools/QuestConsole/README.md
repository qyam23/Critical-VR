# Critical-VR Windows console

Extract the entire Windows package and run `CriticalVRConsole.exe`. The console opens a local dashboard in your browser. Connect one Quest 3 or Quest 3S by USB and approve USB debugging inside the headset. Use **התקן או עדכן** for the first run and **פתח אפליקציה** on subsequent runs. If Horizon OS requests headset camera access, allow it in the headset. The app starts from black and reveals the room over seven seconds once it receives XR focus. Meta XR requires contextual passthrough on the preceding Horizon OS loading screen.

- **עוצמת האפקט** controls the color and distortion mix. At 0% the native room view remains visible. **עיוות ודפוסים** controls distortion and pattern amplitude. The app clamps both adjustments.
- **עצור אפקט** restores the native room view. **חדש אפקט** resumes when the app has focus. The headset hand gestures remain available.
- **הפעל תצוגה** requests a local screenshot about once per second. **פתח שיקוף חי** opens a read-only scrcpy window. Both are limited to content Horizon OS exposes to Android capture. Meta Developer Hub also offers [headset casting](https://developers.meta.com/vr/documentation/spatial-sdk/ts-mqdh-media/).
- **התחל הקלטה** collects head rotation speed, translation speed, and estimated effect intensity. **ייצא CSV** downloads the most recent recording. The graph scales speed and intensity independently. The recording is kept in memory until you export it or close the console.
- **סגור אפליקציה** exits Critical-VR. **העבר לשינה** also requests headset sleep. **כבה קסדה** asks for confirmation before requesting full shutdown over ADB, subject to Horizon OS support.

For development, run `python Tools/QuestConsole/console.py` with `CRITICALVR_ADB` set to the absolute ADB executable path. A packaged copy needs the development APK beside the console executable and scrcpy in `mirror/`. The Windows package bundles the Python runtime using PyInstaller. The dashboard listens on loopback, and the development app exchanges commands and samples through its private storage using ADB. Camera video is displayed locally on request. CSV export contains motion and effect measurements.
