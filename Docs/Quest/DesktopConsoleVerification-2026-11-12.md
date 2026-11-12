# Windows console and room reveal verification

Unity 6000.3.11f1 built the Quest development APK from the production scene. Build reported zero errors and eight warnings. Project validation reported zero errors and two warnings. Android APK signature verification passed. SHA-256 is `066b3bc434cbc04689dc1a850a8dfa3af401cd42ddf1b73641d3170910035612`. The ZIP passed an integrity test and contains the same APK.

The Windows executable served the Hebrew dashboard locally. An unauthenticated state request was rejected. On the authorized Quest 3S, installation and launch were exercised over USB, and the installed APK hash matched. During an earlier build in this change, live samples included rotational and translational motion, and CSV export produced 35 sample rows. The scrcpy client opened a Direct3D window and initialized a 1280×670 stream. The inline screenshot endpoint timed out on one attempt and returned a 3664×1920 PNG on the final test while Horizon OS was at the lock screen. The content and completeness of the room view in the live mirror still require visual confirmation.

The final app starts its seven-second reveal when it receives XR focus. If focus drops during the reveal, the app pauses the transition. Unity's splash is disabled. The Meta XR SDK retains contextual passthrough in the system loading screen, as described in [Meta's Unity splash screen documentation](https://developers.meta.com/vr/documentation/unity/unity-splash-screen/).

The final cold-start test reached Horizon OS's sensor lock screen. Visual timing and per-eye black coverage, acknowledged effect adjustments and resume with the headset worn, and physical sleep and power-off remain to be checked. The console's app-close command returned successfully.
