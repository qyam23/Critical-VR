---
name: meta-quest-xr
description: Build and refine Unity XR applications for Meta Quest with a focus on OVRCameraRig, Passthrough underlay composition, URP full-screen effects, motion-driven interaction, comfort limits, controller safety actions, and deploy-ready Quest workflows. Use when Codex needs to implement or review Meta Quest XR features, Unity C# runtime logic, Shader Graph/URP screen effects, headset comfort safeguards, or Quest-specific setup and debugging.
---

# Meta Quest XR

Build Quest-first Unity XR experiences with comfort and iteration speed as first-class constraints.

## Quick Start

1. Confirm the project target before editing:
   - Unity `2022.3+ LTS`
   - `URP`
   - `OVRCameraRig` or equivalent Meta XR rig
   - Passthrough composed as `Underlay`
2. Decide which layer is being changed:
   - Runtime motion analysis in C#
   - Visual composition in URP / Shader Graph / Volume
   - Rig, project, or build setup
   - Comfort and safety behavior
3. Keep the change small and testable.
4. Clamp comfort-sensitive parameters before exposing them.
5. Leave the project with a clear local test path and device test notes.

## Workflow

### 1. Runtime Motion Logic

Use this skill for scripts that sample head movement and convert it into smooth effect signals.

Prefer this pattern:
- Sample `CenterEyeAnchor` position and rotation once per frame.
- Derive `LinearVelocity`, `LinearAcceleration`, and `AngularVelocity`.
- Smooth noisy input with a moving average or exponential smoothing.
- Convert motion into a normalized `IntensityScore` in `[0, 1]`.
- Apply exponential decay when motion settles.
- Drive a small state machine such as `Calm`, `Shift`, `Intense`, `Recovery`.

Guardrails:
- Do not map raw per-frame spikes directly to strong visuals.
- Avoid hard jumps in score or state transitions.
- Prefer thresholds with hysteresis so the experience does not chatter.
- Expose debug controls in Editor so behavior can be tested without wearing the headset.

For implementation patterns, read:
- [references/motion-analysis.md](references/motion-analysis.md)
- [references/comfort-and-safety.md](references/comfort-and-safety.md)

### 2. Passthrough + Visual Composition

Treat Quest Passthrough as a compositing surface, not as a normal texture you can freely sample.

Prefer these approaches:
- Passthrough as `Underlay`, with graphics rendered on top.
- Full-screen URP effect using `_CameraOpaqueTexture` for scene content and transparent overlays for stylized treatment.
- Camera-locked quad or plane for overlay materials when a renderer feature is too heavy or unavailable.

Keep these constraints:
- Distortion must stay subtle.
- Avoid rapid flashing, hard contrast pulses, and abrupt color inversion.
- Fade visual layers in and out over at least `1.5s`.
- Clamp UV distortion to a comfort-safe cap such as `0.05`.

For composition details, read:
- [references/passthrough-composition.md](references/passthrough-composition.md)

### 3. Rig And Project Setup

When editing setup instructions or reviewing a Unity scene:
- Verify `OVRCameraRig` placement and tracking origin assumptions.
- Confirm URP settings, opaque texture availability, and renderer features.
- Keep effect materials and runtime scripts easy to attach from the Inspector.
- Prefer one clearly documented setup path over multiple competing options.

For setup guidance, read:
- [references/quest-setup-checklist.md](references/quest-setup-checklist.md)

### 4. Comfort Review

Before finalizing any Quest-facing change, check:
- Can the user instantly reset or disable the effect?
- Are strong visuals clamped?
- Is there fade-in and fade-out smoothing?
- Would this stay comfortable during repeated head turns?
- Is the app still intelligible when intensity is near zero?

If the answer is unclear, simplify the effect.

## Deliverables

When this skill is used for implementation, aim to leave:
- A Unity-ready C# script with explicit serialized fields
- Clear material/shader property names
- Setup notes for `OVRCameraRig` and URP
- At least one editor-side debug path
- Explicit safety limits in code or docs

## Anti-Patterns

- Reading Quest Passthrough like a normal camera texture without validating the pipeline
- Using raw headset jitter as a direct distortion driver
- Triggering visual jumps with no fade window
- Allowing unbounded chromatic aberration or wobble values
- Hiding comfort-sensitive magic numbers without comments or serialized fields
