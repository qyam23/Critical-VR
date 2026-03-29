# Shader Graph Logic: Edge Glow & Distortion

To implement the visual effects in Unity URP, follow this logic to create a **Full Screen Shader Graph**.

## 1. Edge Detection (Sobel Filter)
This creates the "Glow" effect on the real-world Passthrough edges.

- **Input**: Use the `Scene Color` node (or `_CameraOpaqueTexture` if using a custom pass).
- **Logic**: 
    - Sample the texture at the current UV.
    - Sample 4 or 8 neighboring pixels (offset by a small `TexelSize`).
    - Calculate the horizontal (Gx) and vertical (Gy) gradients.
    - `Strength = sqrt(Gx² + Gy²)`.
- **Nodes**: `Custom Function` node is best for Sobel, or a series of `Sample Texture 2D` nodes with `Add/Subtract`.
- **Output**: Multiply the result by `_EdgeColor` and `_IntensityScore`.

## 2. Chromatic Aberration (KinoFringe Style)
- **Logic**: Apply color fringing that increases radially from the center.
- **Implementation**:
    - `Screen Position` -> `Distance` from `(0.5, 0.5)`.
    - `Power` (e.g., 3.0) to create a steep edge-weighted curve.
    - `Multiply` by `_ChromaticAmount`.
    - **Axial Fringing**: Subtly shift UVs for R, G, and B channels based on this radial mask.
    - **Highlight Sensitivity**: Multiply the effect by a `Luminance` mask of the `Scene Color` to make it pop more on bright edges.

## 3. Screen Space Distortion (Radial & Local)
- **Logic**: Perceptual "breathing" and peripheral instability.
- **Implementation**:
    - **Radial Falloff (Peripheral-First)**: Use `Distance` from center `(0.5, 0.5)` to mask distortion intensity. The foveal center should remain stable (0 distortion) while the periphery "melts" (max distortion). This is critical for VR comfort and immersion.
    - **Local Wobble**: Sample `Simple Noise` with `Time` and `Screen Position`.
    - **Perspective Instability**: Subtly `Lerp` the `Screen Position` UVs with a low-frequency noise map to simulate "micro-instability" in spatial anchoring.
    - **360 Continuity**: For VR, ensure distortion doesn't create seams. For Passthrough, this is naturally handled by the camera rig, but avoid screen-space effects that don't rotate with the head.

## 4. Intelligent Edge & Structure Salience (Sobel)
- **Logic**: Anchor effects to real scene structure (walls, hands, objects).
- **Implementation**:
    - **Sobel Filter**: Use a custom function to detect edges in `Scene Color` or `Depth`.
    - **Peripheral Bias**: Apply the radial mask to the `_EdgeColor` intensity. This ensures the "world shifting" feeling begins in the user's peripheral vision.
    - **Contrast Masking**: Use the Sobel output to mask `_EdgeGlow` and `_PatternOpacity`.
    - **Luminance Weighting**: Multiply the edge mask by `Luminance(SceneColor)` to ensure glow attaches to highlights.
    - **Mode Difference**: 
        - `Psychedelic`: Sharp, fragmented edges with high contrast.
        - `Empathic`: Soft, glowing "halos" with smoother transitions.

## 5. Pattern Grounding & Spatial Continuity
- **Logic**: Geometric patterns that feel "embedded" in the environment.
- **Implementation**:
    - **World-Space Anchoring**: If possible, project patterns using world-space coordinates rather than screen-space UVs. This ensures that as the user turns their head in 360, the patterns stay "stuck" to the environment.
    - **Texture Masking**: Sample a geometric fractal texture.
    - **Structure Anchoring**: Multiply the pattern by the `Sobel` edge mask. This makes patterns "cling" to the boundaries of real objects.
    - **Peripheral Seeding**: Use the `Radial Mask` to increase pattern density in the periphery, leaving the center clear for task focus.

## 6. Motion Persistence (Afterimages)
- **Logic**: Short-lived ghosting during rapid head movement.
- **Implementation**:
    - **Frame Accumulation**: If using a custom Renderer Feature, blend the current frame with the previous frame using `_IntensityScore` as the blend weight.
    - **Simulated Trails**: In Shader Graph, use a high-strength `Directional Blur` or `Chromatic Stretch` that is masked by `_IntensityTrend` (from C#) to simulate motion-induced persistence.

## 7. Mode-Specific Logic
- **Psychedelic**: 
    - High `Sobel` threshold for sharp edges.
    - High-frequency noise for distortion.
    - Geometric pattern overlays.
- **Empathic**:
    - Low `Sobel` threshold for soft glow.
    - Low-frequency "breathing" only.
    - Warm color grading (Amber/Gold) via `Color Filter`.

## 5. Final Blend
- **Combine**: `FinalColor = SceneColor + (EdgeGlow * Intensity)`.
- **Post-Process**: Apply the Chromatic Aberration and Distortion to the `FinalColor`.
- **Master Stack**: Use the **Full Screen** target in Shader Graph.

---

### Required Properties in Shader:
- `_IntensityScore` (Float)
- `_EdgeColor` (Color)
- `_DistortionAmount` (Float)
- `_WobbleSpeed` (Float)
- `_ChromaticAmount` (Float)
- `_NoiseMap` (Texture2D)
