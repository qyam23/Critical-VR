Original prompt: Turn this repository into an end-to-end, Unity-first, Meta Quest passthrough project that is as close as possible to APK-ready deployment, not just a concept demo.

- Restructured the repository around a Unity-first root with `Assets/`, `Packages/`, `ProjectSettings/`, `Docs/`, and `Tools/WebSimulator/`.
- Researched Quest passthrough, URP post-processing, and Meta/OpenXR repository patterns before editing the implementation.
- Rewriting runtime logic, Quest deployment docs, shader guidance, and bootstrap tooling so the repository can be opened as a Unity project and pushed toward Quest deployment.
- Unity batchmode bootstrap completed and generated `Assets/CriticalVR/Scenes/CriticalVR_QuestBootstrap.unity` plus `Assets/CriticalVR/Resources/Materials/CriticalVRPassthroughTrip.mat`.
- `npm.cmd run build` passes inside `Tools/WebSimulator`.
- Added Quest validation, Quest build automation, batch scripts for validation/build/install, and CI workflow scaffolding.
- Final TODO while closing: run Unity validation/build batch commands against the new editor automation, update generated reports, then commit and push.
