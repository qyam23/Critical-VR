# Shader Guide

This repository now includes a production-targeted shader at:

- `Assets/CriticalVR/Shaders/CriticalVRPassthroughTrip.shader`

Use this guide if you want to rebuild the same logic in `URP Full Screen Shader Graph` or extend the HLSL version.

## Design Goals

- peripheral-first onset
- center stability
- no obvious “filter switched on” moment
- edge-aware glow and structure-sensitive response
- gradual layer entry and staggered fade timing
- 360 continuity with no direction-dependent popping

## Production Composition Model

Quest passthrough is not sampled like a normal unrestricted texture. The practical composition model here is:

1. passthrough stays in `Underlay`
2. the URP effect is rendered over the camera output
3. the runtime script drives subtle world-response layers rather than a hard full-screen replacement

## Runtime-Driven Shader Properties

Driven from `MotionToExperienceManager.cs`:

- `_IntensityScore`
- `_MotionSignal`
- `_EdgeGlow`
- `_DistortionAmount`
- `_PatternAmount`
- `_ChromaticAmount`
- `_ColorAtmosphere`
- `_PeripheralBlur`
- `_BreathingAmount`
- `_BreathingPhase`
- `_AfterImageAmount`
- `_OverdriveAmount`
- `_PeripheralWeight`
- `_CenterStability`
- `_IntroFade`
- `_PeakHold`
- `_StateIndex`
- `_ModeIndex`
- `_EdgeColor`
- `_AtmosphereColor`

## Layer Logic

The intended perceptual order is:

1. `Distortion`
   subtle, peripheral, first to arrive, first to leave
2. `Pattern intrusion`
   arrives later, more structure-aware, lingers longer than distortion
3. `Color atmosphere`
   broad environmental cast, lingers longer than patterning
4. `Breathing`
   slow, organic expansion/compression, last to read as an effect

This timing is already encoded in the runtime controller. The shader should stay obedient to those material values rather than reintroducing sharp thresholds.

## Full Screen Shader Graph Node Plan

### 1. Source

- `Scene Color`
- optional `Scene Depth`
- optional `Normal From Depth` if you want stronger structure masking

Keep `Opaque Texture` and `Depth Texture` enabled in URP.

### 2. Peripheral Mask

Nodes:

- `Screen Position`
- `Tiling And Offset` or simple `Multiply/Add` to remap UV into `-1..1`
- `Length`
- `Smoothstep`
- `One Minus` only if you need inverse center weighting
- `Lerp` to preserve a stable center

Formula intent:

- center remains stable
- effect weight ramps up toward the edges
- mask is radial and continuous so it feels 360-safe

### 3. Distortion

Nodes:

- `Simple Noise` or sampled `Noise Map`
- `Time`
- `Multiply`
- `Normalize`
- `Screen Position`
- `Add`
- `Scene Color`

Rules:

- drive with `_DistortionAmount`
- multiply by the peripheral mask
- clamp total UV offset to `0.05`
- avoid strobing or fast noise speed

### 4. Edge Glow

Preferred implementation:

- `Custom Function` Sobel on `Scene Color` luminance

Alternative:

- approximate edges with depth and normal contrast if you already have those buffers

Nodes:

- `Scene Color`
- luminance conversion
- neighbor sampling in screen UV
- Sobel X and Y
- `Length`
- `Saturate`
- `Multiply` by `_EdgeGlow`
- tint with `_EdgeColor`

Goal:

- highlights structural edges rather than flat areas
- makes the world feel subtly alive before the center changes strongly

### 5. Contrast-Sensitive Masking

Mask stronger responses to places that already have structure:

- edge mask from Sobel
- luminance contrast mask
- optional depth discontinuity mask

Multiply those masks with:

- `_PatternAmount`
- `_ChromaticAmount`
- `_ColorAtmosphere`

This prevents a pasted-on overlay feel.

### 6. Highlight-Sensitive Chromatic Aberration

Nodes:

- `Scene Color`
- luminance / highlight mask
- radial direction from center
- two opposite UV offsets
- recombine R/G/B

Rules:

- stronger on highlights and edges
- stronger in the periphery
- weaker in the center

### 7. Pattern Anchoring To Scene Structure

Patterning should not float independently of the scene.

Anchor it by multiplying procedural pattern noise with:

- edge mask
- highlight mask
- optional normals/depth mask

This makes the pattern feel attached to walls, silhouettes, and contrast boundaries rather than floating as a flat UI layer.

## Optional World-Space Grounding

If later you move from a pure full-screen pass to a more advanced setup:

- derive world position from depth
- sample triplanar or world-space noise
- project low-frequency patterning into the room

Do not start there. The current repository uses a simpler screen-space structure-anchored approach because it is more robust and easier to ship.

## Motion Persistence / Afterimage

The current shader exposes `_AfterImageAmount`.

Two implementation levels:

1. current repository path
   approximate persistence by layering delayed color bloom and structure-tinted residue
2. advanced path
   add a previous-frame accumulation buffer or custom render texture and blend with decay

If you implement the advanced path, keep decay slow and avoid flicker.

## 360 Continuity Rules

- use radial masks, not hard directional masks
- avoid left/right-only or top/bottom-only behavior
- keep effect continuity around the user
- prioritize peripheral response so the world changes “around” the user before it changes “at” the focal center
- do not let any single compass direction have a special pop-in threshold

## Mode Differences

### Psychedelic

- sharper edge salience
- stronger pattern intrusion
- bolder chromatic spread
- geometric fragmentation can be higher
- overdrive palette can move into non-natural magenta/cyan/yellow territory

### Empathic

- softer glow
- warmer grading
- gentler breathing
- less geometric disruption
- chromatic spread stays more restrained

## Recommended Property Defaults

- `_DistortionAmount`: `0.0 - 0.05`
- `_ChromaticAmount`: `0.0 - 0.022`
- `_PatternAmount`: `0.0 - 0.85`
- `_ColorAtmosphere`: `0.0 - 0.95`
- `_PeripheralBlur`: `0.0 - 0.68`
- `_BreathingAmount`: `0.0 - 0.28`
- `_AfterImageAmount`: `0.0 - 0.38`

## What To Drive From C#

Drive from runtime:

- all thresholds, attack/release timing, holds, recovery, intro fade, and safety reset

Keep inside the shader:

- the compositing math
- edge extraction
- peripheral weighting
- color grading response
- pattern generation and structure masking

That split keeps the experience state machine deterministic and comfort-reviewed on the C# side.
