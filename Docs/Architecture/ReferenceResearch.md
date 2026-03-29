# Reference Research And Capability Gaps

## Repositories Reviewed

### `oculus-samples/Unity-PassthroughCameraApiSamples`

Why it mattered:

- it shows real Meta Quest sample-project structure
- it reinforces that passthrough features are headset-first and Editor simulation is limited
- it highlights the importance of setup tooling, scene organization, and device-only validation

What was adopted:

- Quest-first scene/setup mindset
- explicit device validation notes
- clear separation between runtime assets and setup guidance

What was not copied:

- camera API sample code
- MRUK-specific flows unrelated to this altered-perception passthrough experience

### `yahiaetman/URPCustomPostProcessingStack`

Why it mattered:

- it demonstrates proven URP custom full-screen pass architecture
- it validates renderer-feature and full-screen material patterns for post effects

What was adopted:

- custom renderer feature structure
- full-screen pass injection approach
- separation between runtime-controlled parameters and render-pass execution

What was not copied:

- the full post stack package
- unrelated effect implementations

### `needle-mirror/com.unity.xr.meta-openxr`

Why it mattered:

- it confirms the Meta/OpenXR packaging path for Unity projects
- it helps anchor package expectations around Quest-focused OpenXR support

What was adopted:

- OpenXR-forward package strategy in `Packages/manifest.json`
- documentation language around Meta/OpenXR plugin configuration

What was not copied:

- package source code
- package internals

### `Unity-Technologies/Graphics`

Why it mattered:

- it is the authoritative URP/SRP reference point
- it helps align shader and renderer-feature expectations with real URP architecture

What was adopted:

- URP-centered project organization
- reliance on `_CameraOpaqueTexture`, depth, normals, and renderer feature flow

What was not copied:

- large package internals
- URP source implementations

## Capability Gap Analysis

Originally missing or incomplete in the repository:

- Unity project root structure
- Quest-oriented Android package/build setup
- URP renderer-feature path
- production-centered effect shader source
- deployment checklist and ADB install path
- bootstrap tooling for scene/material creation
- explicit separation between shipping path and browser simulator
- documented 360/peripheral-first comfort logic

## How Those Gaps Were Addressed

- created a real Unity project root with `Assets`, `Packages`, and `ProjectSettings`
- upgraded motion runtime into a headset-oriented controller with motion history, hysteresis, layered timing, recovery, overdrive hold, and intro fade
- added a URP full-screen renderer feature script
- added a production shader that implements peripheral weighting, edge-aware glow, chromatic response, patterning, and intro fade
- added Unity Editor bootstrap tooling for Android settings plus scene/material creation
- rewrote Quest deployment and shader guides
- added build/install helper docs and an ADB helper script
- moved the React/Vite app into `Tools/WebSimulator` and documented it as support-only

## Remaining Manual Capabilities

Still outside repository-only editing:

- importing Meta XR / Oculus Integration package content
- final `OVRCameraRig` and `OVRPassthroughLayer` scene wiring
- attaching the renderer feature to the exact URP renderer asset created in the Unity Editor
- final headset comfort validation on hardware
