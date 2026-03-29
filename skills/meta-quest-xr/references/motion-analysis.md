# Motion Analysis Patterns

Use a small, explicit pipeline:

1. Sample current head pose.
2. Compute:
   - `linearVelocity = (position - lastPosition) / dt`
   - `linearAcceleration = (linearVelocity - lastVelocity).magnitude / dt`
   - `angularVelocity = Quaternion.Angle(rotation, lastRotation) / dt`
3. Normalize each component into comfort-safe bands.
4. Blend them into a single motion signal.
5. Smooth the signal with:
   - moving average over 8-24 samples, or
   - exponential smoothing with a serialized smoothing coefficient
6. Convert to `IntensityScore` and apply exponential decay when idle.

Recommended logic:
- Weight angular motion more than translation for perceptual instability.
- Use hysteresis for activity gates.
- Track trend so `Recovery` can be inferred from a falling score instead of a separate trigger.

Suggested state thresholds:
- `Calm`: score < `0.12`
- `Shift`: `0.12` to `0.45`
- `Intense`: score >= `0.45`
- `Recovery`: falling trend while score remains elevated

Editor debugging:
- Provide `useDebugSlider`
- Provide `debugIntensity`
- Allow keyboard reset in Editor
