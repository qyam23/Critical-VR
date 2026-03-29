# Passthrough Composition Notes

Quest Passthrough is privacy-sensitive and should not be treated like an arbitrary readable background texture.

Preferred composition order:
- Real world through Passthrough `Underlay`
- Virtual scene content above it
- Full-screen or camera-locked overlay for stylization

Common implementation options:

## Option A: URP Full Screen Effect

Use when the project already has a URP renderer feature path.

- Enable `_CameraOpaqueTexture`
- Use a full-screen shader for Sobel edges, chromatic offsets, vignette blur, and subtle wobble
- Expose properties such as:
  - `_IntensityScore`
  - `_EdgeColor`
  - `_ChromaticAmount`
  - `_DistortionAmount`
  - `_PeripheralBlur`

## Option B: Camera-Locked Overlay Quad

Use when a renderer feature is too heavy or the pipeline is still being prototyped.

- Attach a quad slightly in front of the camera
- Use a transparent material
- Keep the quad large enough to cover the field of view
- Drive the material from a runtime controller script

Safety limits:
- Clamp UV distortion to `0.05`
- Prefer low-frequency noise for wobble
- Avoid strobing or rapidly oscillating hue shifts
- Fade all major effects over at least `1.5s`
