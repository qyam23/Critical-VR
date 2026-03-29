# Critical-VR

Local simulator and Unity implementation notes for a Meta Quest XR "motion trip" experience.

## What is in this repo

- A local `React + Vite` simulator for testing motion-driven intensity and comfort tuning on desktop
- A Unity-ready runtime script in [`MotionToExperienceManager.cs`](./MotionToExperienceManager.cs)
- Setup and shader notes in [`SetupGuide.md`](./SetupGuide.md) and [`ShaderGraphGuide.md`](./ShaderGraphGuide.md)
- Downloaded and project-local skills under [`skills/`](./skills/)

## Local run

Prerequisites:
- Node.js

Start locally with one of these:

```bash
python run_local.py
```

or:

```bat
run_local.bat
```

The app runs at [http://localhost:3000](http://localhost:3000).

## Desktop simulator controls

- Move the mouse to simulate headset motion when `Debug slider` is off
- Use the `Debug slider` to test intensity without live motion
- Press `Space`, `B`, or `Y` for emergency reset
- Toggle the camera button to use webcam passthrough-like background input

## Installed and added skills

Installed into `~/.codex/skills`:
- `develop-web-game`
- `game-developer-skill`
- `csharp-developer-skill`

Added to this project:
- [`skills/meta-quest-xr`](./skills/meta-quest-xr)

The new `meta-quest-xr` skill captures:
- Meta Quest setup expectations
- Passthrough composition constraints
- Motion analysis patterns
- Comfort and safety rules for XR visuals

## Notes

This repository is not a full Unity project export. It is a working simulator plus Unity-side implementation assets and guidance for the Quest build.
