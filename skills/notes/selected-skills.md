# Selected Skills For Critical-VR

Downloaded into this project:

- `skills/openai/develop-web-game`
- `skills/downloaded/game-developer-skill`
- `skills/downloaded/csharp-developer-skill`
- `skills/meta-quest-xr`

Most relevant skill files reviewed:

- `skills/openai/develop-web-game/SKILL.md`
- `skills/downloaded/game-developer-skill/SKILL.md`
- `skills/downloaded/csharp-developer-skill/SKILL.md`
- `skills/meta-quest-xr/SKILL.md`

Why these matter for this project:

- `develop-web-game` gives a concrete loop for iterating on the local React/Vite simulator and validating behavior with Playwright.
- `game-developer-skill` contains Unity- and VR-relevant guidance, including Unity + URP fit, VR comfort constraints, and graphics optimization reminders.
- `csharp-developer-skill` is a lightweight fallback for keeping the Unity-side C# code organized and maintainable.
- `meta-quest-xr` fills the main project gap with Quest-specific Passthrough, OVRCameraRig, URP, and comfort guidance.

Current recommendation:

- Use `develop-web-game` for the local browser simulator and test loop.
- Use `game-developer-skill` as the closest external guidance for Unity/VR decisions.
- Treat `csharp-developer-skill` as secondary support, not a primary XR skill.
- Use `meta-quest-xr` as the project-specific primary skill for future Quest implementation work.

Gaps still not covered by downloaded skills:

- Meta Quest Passthrough specifics
- OVRManager / OVRCameraRig setup
- URP Full Screen Shader Graph implementation details
- Quest APK build + deploy workflow
