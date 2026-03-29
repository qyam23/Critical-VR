# Unity Meta Quest Passthrough Setup Guide

Follow these steps to set up the environment for the Passthrough experience.

## 1. Project Configuration
- **Unity Version**: 2022.3 LTS or newer.
- **Platform**: Switch to **Android**.
- **XR Plug-in Management**: Enable **Oculus**.
- **URP Setup**: Ensure you have a `Universal Render Pipeline Asset` assigned in Graphics settings.

## 2. Meta XR SDK
- Import the **Meta XR Core SDK** from the Unity Asset Store or Package Manager.
- Run the **Oculus Project Setup Tool** (Fix all recommended items).

## 3. OVRCameraRig Setup
1. Delete the default `Main Camera`.
2. Drag the `OVRCameraRig` prefab into the scene.
3. On the `OVRManager` component:
    - **Quest Features**: Set `Passthrough Support` to **Required**.
    - **Insight Passthrough**: Enable `Enable Passthrough`.
4. Add the `OVRPassthroughLayer` component to the `OVRCameraRig`:
    - **Placement**: Set to **Underlay**.
    - **Composition Layer**: Ensure it's active.

## 4. Visual Effects Integration (URP)
To make the Shader Graph work over the Passthrough:
1. Create a new **Material** using your Full Screen Shader Graph.
2. In your URP Renderer Data (e.g., `UniversalRendererData`):
    - Add a **Full Screen Pass Renderer Feature**.
    - **Pass Material**: Assign the material created above.
    - **Injection Point**: Set to `After Rendering Post Processing` or `After Rendering Transparents`.
    - **Requirements**: Ensure `Opaque Texture` is enabled on the URP Asset.

## 5. Script Assignment
1. Create an empty GameObject named `ExperienceManager`.
2. Attach the `MotionToExperienceManager.cs` script.
3. **Center Eye Anchor**: Drag `OVRCameraRig > TrackingSpace > CenterEyeAnchor` here.
4. **Effect Material**: Drag the material used in the Renderer Feature here.

## 6. Building for Quest
- In **Player Settings > Android**:
    - **Graphics APIs**: Use **Vulkan** (preferred for Passthrough).
    - **Minimum API Level**: Android 10 (API 29).
- Build and Run!
