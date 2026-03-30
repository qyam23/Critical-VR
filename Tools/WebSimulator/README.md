# Web Simulator

This folder contains the desktop support simulator.

It is useful for:

- tuning motion-to-intensity behavior without wearing the headset
- checking layered entry and exit timing
- demonstrating the intended “feel” of state transitions

It is not the shipping product path.

## Run

```bash
python run_local.py
```

or

```bat
run_local.bat
```

Open:

`http://localhost:3000`

## Controls

- move mouse when `Debug slider` is off
- use the debug slider for editor-style intensity tests
- `Space`, `B`, or `Y` resets the experience

## Relationship To Unity

The simulator mirrors the timing philosophy of `MotionToExperienceManager`, but the final Quest deployment path is documented in:

- `Docs/Quest/SetupGuide.md`
- `Docs/Quest/ShaderGraphGuide.md`
- `Docs/Quest/BUILD_AND_INSTALL.md`
