#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace CriticalVR.EditorTools
{
    internal enum CriticalVRValidationSeverity
    {
        Info,
        Warning,
        Error,
    }

    internal sealed class CriticalVRValidationIssue
    {
        public CriticalVRValidationSeverity Severity;
        public string Code;
        public string Message;
        public string Fix;
    }

    internal sealed class CriticalVRValidationReport
    {
        public DateTime GeneratedAtUtc;
        public string CurrentBranch;
        public List<CriticalVRValidationIssue> Issues = new();

        public bool HasErrors => Issues.Any(issue => issue.Severity == CriticalVRValidationSeverity.Error);
        public bool HasWarnings => Issues.Any(issue => issue.Severity == CriticalVRValidationSeverity.Warning);

        public void Add(CriticalVRValidationSeverity severity, string code, string message, string fix)
        {
            Issues.Add(new CriticalVRValidationIssue
            {
                Severity = severity,
                Code = code,
                Message = message,
                Fix = fix,
            });
        }

        public string ToMarkdown()
        {
            StringBuilder builder = new();
            builder.AppendLine("# Quest Validation Report");
            builder.AppendLine();
            builder.AppendLine($"- Generated: `{GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC`");
            builder.AppendLine($"- Branch: `{CurrentBranch}`");
            builder.AppendLine($"- Errors: `{Issues.Count(issue => issue.Severity == CriticalVRValidationSeverity.Error)}`");
            builder.AppendLine($"- Warnings: `{Issues.Count(issue => issue.Severity == CriticalVRValidationSeverity.Warning)}`");
            builder.AppendLine($"- Info: `{Issues.Count(issue => issue.Severity == CriticalVRValidationSeverity.Info)}`");
            builder.AppendLine();

            if (Issues.Count == 0)
            {
                builder.AppendLine("No issues detected.");
                return builder.ToString();
            }

            foreach (CriticalVRValidationIssue issue in Issues)
            {
                builder.AppendLine($"## [{issue.Severity}] {issue.Code}");
                builder.AppendLine(issue.Message);
                if (!string.IsNullOrWhiteSpace(issue.Fix))
                {
                    builder.AppendLine();
                    builder.AppendLine($"Fix: {issue.Fix}");
                }
                builder.AppendLine();
            }

            return builder.ToString();
        }
    }

    public static class CriticalVRProjectValidator
    {
        private const string OpenXRLoaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";

        [MenuItem("CriticalVR/Validation/Run Quest Validation")]
        public static void RunQuestValidationMenu()
        {
            CriticalVRValidationReport report = ValidateProject(attemptAutoFix: true, openScene: true);
            WriteReport(report, CriticalVRProjectPaths.ValidationReportPath);
            PrintSummary(report, CriticalVRProjectPaths.ValidationReportPath);

            if (report.HasErrors)
            {
                Debug.LogError($"Critical-VR validation found blocking issues. Report: {CriticalVRProjectPaths.ValidationReportPath}");
            }
            else if (report.HasWarnings)
            {
                Debug.LogWarning($"Critical-VR validation passed with warnings. Report: {CriticalVRProjectPaths.ValidationReportPath}");
            }
            else
            {
                Debug.Log($"Critical-VR validation passed cleanly. Report: {CriticalVRProjectPaths.ValidationReportPath}");
            }
        }

        public static void RunQuestValidationBatch()
        {
            CriticalVRValidationReport report = ValidateProject(attemptAutoFix: true, openScene: true);
            WriteReport(report, CriticalVRProjectPaths.ValidationReportPath);
            PrintSummary(report, CriticalVRProjectPaths.ValidationReportPath);

            if (report.HasErrors)
            {
                throw new BuildFailedException($"Critical-VR validation failed. See {CriticalVRProjectPaths.ValidationReportPath}");
            }

            EditorApplication.Exit(0);
        }

        internal static CriticalVRValidationReport ValidateProject(bool attemptAutoFix, bool openScene)
        {
            if (attemptAutoFix)
            {
                CriticalVRProjectBootstrap.EnsureQuestProjectScaffold();
            }

            CriticalVRValidationReport report = new()
            {
                GeneratedAtUtc = DateTime.UtcNow,
                CurrentBranch = CriticalVRProjectPaths.ReadCurrentGitBranch(),
            };

            ValidateBranch(report);
            ValidateCoreAssets(report);
            ValidateRenderPipeline(report);
            ValidateScene(report, openScene);
            ValidateAndroidSettings(report);
            ValidateXRSettings(report);
            ValidateMetaQuestReadiness(report);
            ValidateCameraIntegration(report);
            ValidateMetaProjectSetup(report, attemptAutoFix);
            return report;
        }

        private static void ValidateMetaProjectSetup(CriticalVRValidationReport report, bool autoFix)
        {
            // The SDK registry is internal. Inspect its real required tasks instead of reproducing them.
            var getTasks = typeof(OVRProjectSetup).GetMethod("GetTasks", BindingFlags.Static | BindingFlags.NonPublic);
            if (getTasks == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "meta.setupRegistry", "Meta Project Setup task registry was not found.", "Check the installed SDK version.");
                return;
            }
            var tasks = ((System.Collections.IEnumerable)getTasks.Invoke(null, new object[] { BuildTargetGroup.Android })).Cast<object>().ToArray();
            for (int pass = 0; pass < (autoFix ? 4 : 1); pass++)
            {
                foreach (object task in tasks)
                {
                    Type type = task.GetType();
                    type.GetMethod("InvalidateCache").Invoke(task, new object[] { BuildTargetGroup.Android });
                    var isDone = (Func<BuildTargetGroup, bool>)type.GetProperty("IsDone").GetValue(task);
                    object level = type.GetProperty("Level").GetValue(task);
                    string severity = level.GetType().GetMethod("GetValue").Invoke(level, new object[] { BuildTargetGroup.Android }).ToString();
                    if (severity != "Required" || isDone(BuildTargetGroup.Android)) continue;
                    if (IsSceneRecommendationForCameraOnlyScene(task)) continue;
                    var fix = type.GetProperty("FixAction").GetValue(task) as Action<BuildTargetGroup>;
                    if (autoFix && fix != null) fix(BuildTargetGroup.Android);
                }
            }
            OVRManifestPreprocessor.GenerateOrUpdateAndroidManifest(silentMode: true);
            foreach (object task in tasks)
            {
                Type type = task.GetType();
                type.GetMethod("InvalidateCache").Invoke(task, new object[] { BuildTargetGroup.Android });
                var isDone = (Func<BuildTargetGroup, bool>)type.GetProperty("IsDone").GetValue(task);
                if (isDone(BuildTargetGroup.Android)) continue;
                object level = type.GetProperty("Level").GetValue(task);
                string severity = level.GetType().GetMethod("GetValue").Invoke(level, new object[] { BuildTargetGroup.Android }).ToString();
                if (severity != "Required") continue;
                object message = type.GetProperty("Message").GetValue(task);
                string detail = message.GetType().GetMethod("GetValue").Invoke(message, new object[] { BuildTargetGroup.Android }).ToString();
                if (IsSceneRecommendationForCameraOnlyScene(task))
                {
                    report.Add(CriticalVRValidationSeverity.Warning, "meta.mrukSceneRecommendation",
                        "MRUK 85's generic rule remains unsatisfied: " + detail + ". This camera-only scene contains no MRUK scene manager, OVRSceneManager or spatial anchors. PCA itself checks HEADSET_CAMERA only. Scene/anchor support and permissions are deliberately absent. The SDK task was evaluated and was not ignored or marked fixed.",
                        "If room reconstruction or spatial anchors are introduced, enable their support/permissions and satisfy the rule. See CameraIntegration-2026-11-07.md for this scoped applicability decision.");
                    continue;
                }
                report.Add(CriticalVRValidationSeverity.Error, "meta.requiredTask", detail, "Resolve this Meta Project Setup requirement before building.");
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        private static bool IsSceneRecommendationForCameraOnlyScene(object task)
        {
            object message = task.GetType().GetProperty("Message").GetValue(task);
            string text = message.GetType().GetMethod("GetValue").Invoke(message, new object[] { BuildTargetGroup.Android }).ToString();
            return text == "MR Utility Kit recommends Scene Support to be set to \"Required\"" &&
                UnityEngine.Object.FindFirstObjectByType<QuestPassthroughCameraBridge>() != null &&
                UnityEngine.Object.FindFirstObjectByType<Meta.XR.MRUtilityKit.MRUK>(FindObjectsInactive.Include) == null &&
                UnityEngine.Object.FindFirstObjectByType<OVRSceneManager>(FindObjectsInactive.Include) == null &&
                UnityEngine.Object.FindFirstObjectByType<OVRSpatialAnchor>(FindObjectsInactive.Include) == null;
        }

        private static void ValidateCameraIntegration(CriticalVRValidationReport report)
        {
            var bridge = UnityEngine.Object.FindFirstObjectByType<QuestPassthroughCameraBridge>();
            if (bridge == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "camera.bridgeMissing", "Production stereo camera bridge is missing.", "Run Quest bootstrap.");
                return;
            }
            var settings = new SerializedObject(bridge);
            foreach (string field in new[] { "effectMaterial", "experience", "eyeCamera" })
                if (settings.FindProperty(field).objectReferenceValue == null)
                    report.Add(CriticalVRValidationSeverity.Error, "camera.reference." + field, "Camera bridge reference missing: " + field, "Run Quest bootstrap.");
            var eye = settings.FindProperty("eyeCamera").objectReferenceValue as Camera;
            if (eye != null && (eye.clearFlags != CameraClearFlags.SolidColor || eye.backgroundColor.a > 0f))
                report.Add(CriticalVRValidationSeverity.Error, "camera.opaqueBackground", "Eye camera must clear transparent for native underlay fallback.", "Run Quest bootstrap.");
            string manifestPath = CriticalVRProjectPaths.ToAbsolutePath("Assets/Plugins/Android/AndroidManifest.xml");
            if (!File.Exists(manifestPath) || !File.ReadAllText(manifestPath).Contains("horizonos.permission.HEADSET_CAMERA"))
                report.Add(CriticalVRValidationSeverity.Error, "camera.permissionMissing", "Generated Android manifest lacks HEADSET_CAMERA.", "Run Meta setup and regenerate the manifest.");
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var feature = xr == null ? null : xr.GetFeature<Meta.XR.MetaXRFeature>();
            if (feature == null || !feature.enabled)
                report.Add(CriticalVRValidationSeverity.Error, "camera.metaOpenXRDisabled", "Meta XR OpenXR feature is missing or disabled.", "Run Quest bootstrap after package import.");
            var layer = UnityEngine.Object.FindFirstObjectByType<OVRPassthroughLayer>();
            if (layer != null && layer.overlayType != OVROverlay.OverlayType.Underlay)
                report.Add(CriticalVRValidationSeverity.Error, "camera.underlay", "Passthrough must use Underlay composition.", "Run Quest bootstrap.");
            if (EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path != CriticalVRProjectPaths.ScenePath))
                report.Add(CriticalVRValidationSeverity.Error, "build.previewScene", "An enabled build scene is outside the Quest scene allowlist.", "Run Quest bootstrap.");
            Shader shader = Shader.Find(CriticalVRProjectPaths.ShaderPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                report.Add(CriticalVRValidationSeverity.Error, "camera.shader", "Effect shader is missing or has compiler errors.", "Inspect Unity shader compiler messages.");
            report.Add(CriticalVRValidationSeverity.Warning, "camera.hardwareUnverified",
                "Static validation cannot verify device permission, frame pairing, per-eye alignment, near-object parallax or sustained GPU time. Reference-distance reprojection is approximate.",
                "Run the device checklist on a Quest 3/3S. Never treat a green static report as headset verification.");
        }

        internal static void WriteReport(CriticalVRValidationReport report, string relativePath)
        {
            CriticalVRProjectPaths.EnsureAbsoluteDirectoryForFile(relativePath);
            File.WriteAllText(CriticalVRProjectPaths.ToAbsolutePath(relativePath), report.ToMarkdown());
            AssetDatabase.Refresh();
        }

        private static void ValidateBranch(CriticalVRValidationReport report)
        {
            if (report.CurrentBranch == CriticalVRProjectPaths.TargetBranch)
            {
                report.Add(CriticalVRValidationSeverity.Info, "branch.ok", "Repository is opened on the expected working branch.", "No action required.");
                return;
            }

            report.Add(
                CriticalVRValidationSeverity.Warning,
                "branch.mismatch",
                $"Current git branch is `{report.CurrentBranch}`, expected `{CriticalVRProjectPaths.TargetBranch}`.",
                $"Checkout `{CriticalVRProjectPaths.TargetBranch}` before making deployment changes.");
        }

        private static void ValidateCoreAssets(CriticalVRValidationReport report)
        {
            if (!File.Exists(CriticalVRProjectPaths.ToAbsolutePath(CriticalVRProjectPaths.ScenePath)))
            {
                report.Add(CriticalVRValidationSeverity.Error, "scene.missing", "Bootstrap Quest scene is missing.", "Run `CriticalVR > Bootstrap > Run Full Quest Bootstrap`.");
            }

            if (!File.Exists(CriticalVRProjectPaths.ToAbsolutePath(CriticalVRProjectPaths.MaterialPath)))
            {
                report.Add(CriticalVRValidationSeverity.Error, "material.missing", "Effect material is missing.", "Run `CriticalVR > Bootstrap > Run Full Quest Bootstrap`.");
            }
        }

        private static void ValidateRenderPipeline(CriticalVRValidationReport report)
        {
            UniversalRenderPipelineAsset pipelineAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipelineAsset == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "urp.unassigned", "GraphicsSettings does not point to a Universal Render Pipeline asset.", "Run bootstrap to create and assign the URP asset.");
                return;
            }

            report.Add(CriticalVRValidationSeverity.Info, "urp.assigned", $"GraphicsSettings uses `{pipelineAsset.name}`.", "No action required.");

            if (!pipelineAsset.supportsCameraOpaqueTexture)
            {
                report.Add(CriticalVRValidationSeverity.Error, "urp.opaqueTexture", "URP pipeline does not enable Opaque Texture.", "Enable Opaque Texture or rerun bootstrap.");
            }

            if (!pipelineAsset.supportsCameraDepthTexture)
            {
                report.Add(CriticalVRValidationSeverity.Error, "urp.depthTexture", "URP pipeline does not enable Depth Texture.", "Enable Depth Texture or rerun bootstrap.");
            }

            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(CriticalVRProjectPaths.RendererDataPath);
            if (rendererData == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "rendererData.missing", "Renderer Data asset is missing.", "Run bootstrap to create the renderer data asset.");
                return;
            }

            CriticalVR.Rendering.CriticalVRFullscreenRendererFeature feature = rendererData.rendererFeatures.OfType<CriticalVR.Rendering.CriticalVRFullscreenRendererFeature>().FirstOrDefault();
            if (feature == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "rendererFeature.missing", "CriticalVR fullscreen renderer feature is not attached to the active renderer data.", "Rerun bootstrap to attach the renderer feature.");
                return;
            }

            SerializedObject featureSerializedObject = new SerializedObject(feature);
            Material material = featureSerializedObject.FindProperty("settings").FindPropertyRelative("material").objectReferenceValue as Material;
            if (material == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "rendererFeature.materialMissing", "CriticalVR fullscreen renderer feature does not have an effect material assigned.", $"Assign `{CriticalVRProjectPaths.MaterialPath}`.");
            }
            else
            {
                if (material.GetTag("RenderPipeline", false) != "UniversalPipeline")
                    report.Add(CriticalVRValidationSeverity.Error, "shader.pipelineTag",
                        "Effect shader does not declare URP's UniversalPipeline tag; SRP build stripping can remove every variant.",
                        "Set the shader SubShader RenderPipeline tag to UniversalPipeline and rebuild.");
                report.Add(CriticalVRValidationSeverity.Info, "rendererFeature.ok", $"Renderer feature is attached with material `{material.name}`.", "No action required.");
            }
        }

        private static void ValidateScene(CriticalVRValidationReport report, bool openScene)
        {
            if (!openScene || !File.Exists(CriticalVRProjectPaths.ToAbsolutePath(CriticalVRProjectPaths.ScenePath)))
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(CriticalVRProjectPaths.ScenePath, OpenSceneMode.Single);
            MotionToExperienceManager manager = UnityEngine.Object.FindFirstObjectByType<MotionToExperienceManager>();
            if (manager == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "scene.managerMissing", "MotionToExperienceManager is missing from the bootstrap scene.", "Open the scene and rerun bootstrap.");
                return;
            }

            SerializedObject serializedManager = new SerializedObject(manager);
            Transform centerEyeAnchor = serializedManager.FindProperty("centerEyeAnchor").objectReferenceValue as Transform;
            Material effectMaterial = serializedManager.FindProperty("effectMaterial").objectReferenceValue as Material;

            if (effectMaterial == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "scene.materialReferenceMissing", "MotionToExperienceManager is missing its effect material reference.", $"Assign `{CriticalVRProjectPaths.MaterialPath}`.");
            }

            if (centerEyeAnchor == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "scene.centerEyeMissing", "MotionToExperienceManager is missing a CenterEyeAnchor reference.", "Assign OVRCameraRig/TrackingSpace/CenterEyeAnchor after Meta XR import.");
            }
            else if (centerEyeAnchor.name == "CenterEyeAnchorPreview")
            {
                report.Add(CriticalVRValidationSeverity.Warning, "scene.previewAnchor", "Scene is still wired to the preview center eye anchor.", "Replace the preview rig with OVRCameraRig and assign the real CenterEyeAnchor.");
            }

            report.Add(CriticalVRValidationSeverity.Info, "scene.loaded", $"Validated scene `{scene.path}`.", "No action required.");
        }

        private static void ValidateAndroidSettings(CriticalVRValidationReport report)
        {
            var playerSettings = Resources.FindObjectsOfTypeAll<PlayerSettings>().FirstOrDefault();
            var inputHandler = playerSettings == null ? null : new SerializedObject(playerSettings).FindProperty("activeInputHandler");
            if (inputHandler == null || inputHandler.intValue != 1)
                report.Add(CriticalVRValidationSeverity.Error, "android.inputSystem", "Android requires Input System only; Both is unsupported.", "Set Active Input Handling to Input System Package (New), save and restart Unity before building.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                report.Add(CriticalVRValidationSeverity.Error, "android.supportMissing", "This Unity installation does not report Android build support for the current editor.", "Install Android Build Support in Unity Hub or use a Unity editor that includes Android support.");
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                report.Add(CriticalVRValidationSeverity.Error, "android.target", "Active build target is not Android.", "Switch the project to Android or rerun bootstrap.");
            }

            GraphicsDeviceType[] graphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (graphicsApis.Length == 0 || graphicsApis[0] != GraphicsDeviceType.Vulkan)
            {
                report.Add(CriticalVRValidationSeverity.Error, "android.vulkanFirst", "Android graphics APIs are not configured with Vulkan first.", "Rerun bootstrap or set Vulkan to the first graphics API.");
            }

            if (!graphicsApis.Contains(GraphicsDeviceType.OpenGLES3))
            {
                report.Add(CriticalVRValidationSeverity.Warning, "android.glesFallback", "OpenGLES3 fallback is not present.", "Add OpenGLES3 after Vulkan if your device/plugin combination needs a fallback.");
            }

            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                report.Add(CriticalVRValidationSeverity.Error, "android.linearColor", "Project color space is not Linear.", "Set PlayerSettings color space to Linear.");
            }
        }

        private static void ValidateXRSettings(CriticalVRValidationReport report)
        {
            string[] assetGuids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            XRGeneralSettingsPerBuildTarget generalSettings = null;
            if (assetGuids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[0]);
                generalSettings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(assetPath);
            }

            if (generalSettings == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "xr.generalSettingsMissing", "XRGeneralSettingsPerBuildTarget asset was not found.", "Rerun bootstrap to create XR management assets.");
                return;
            }

            XRGeneralSettings androidSettings = generalSettings.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (androidSettings == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "xr.settingsMissing", "XR General Settings are missing for Android.", "Rerun bootstrap to create Android XR settings.");
                return;
            }

            XRManagerSettings managerSettings = androidSettings.AssignedSettings;
            if (managerSettings == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "xr.managerMissing", "XR Manager Settings are missing for Android.", "Rerun bootstrap to create Android XR manager settings.");
            }
            else if (!managerSettings.activeLoaders.Any(loader => loader != null && loader.GetType().FullName == OpenXRLoaderTypeName))
            {
                report.Add(CriticalVRValidationSeverity.Error, "xr.openxrLoaderMissing", "OpenXR loader is not assigned to Android XR management.", "Rerun bootstrap or assign the OpenXR loader in XR Plug-in Management.");
            }
            else
            {
                report.Add(CriticalVRValidationSeverity.Info, "xr.openxrLoaderPresent", "OpenXR loader is assigned for Android.", "No action required.");
            }

            if (!HasAndroidOpenXRSettings())
            {
                report.Add(CriticalVRValidationSeverity.Error, "xr.openxrSettingsMissing", "OpenXR package settings for Android are missing.", "Rerun bootstrap to create the OpenXR settings asset.");
            }
            else
            {
                report.Add(CriticalVRValidationSeverity.Info, "xr.openxrSettingsPresent", "OpenXR package settings for Android exist.", "No action required.");
            }
        }

        private static void ValidateMetaQuestReadiness(CriticalVRValidationReport report)
        {
            Type ovrCameraRigType = CriticalVRProjectPaths.FindType("OVRCameraRig");
            Type passthroughLayerType = CriticalVRProjectPaths.FindType("OVRPassthroughLayer");
            Type ovrManagerType = CriticalVRProjectPaths.FindType("OVRManager");

            if (ovrCameraRigType == null || passthroughLayerType == null || ovrManagerType == null)
            {
                report.Add(
                    CriticalVRValidationSeverity.Error,
                    "meta.dependenciesMissing",
                    "Meta XR / Oculus Integration types were not found in the project.",
                    "Import the Meta XR package set so OVRCameraRig, OVRManager, and OVRPassthroughLayer are available.");
                return;
            }

            if (UnityEngine.Object.FindFirstObjectByType(ovrCameraRigType) == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "meta.cameraRigMissing", "OVRCameraRig is not present in the bootstrap scene.", "Replace XROriginPreview with OVRCameraRig.");
            }

            if (UnityEngine.Object.FindFirstObjectByType(ovrManagerType) == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "meta.ovrManagerMissing", "OVRManager is not present in the scene.", "Add OVRManager to the rig root.");
            }

            if (UnityEngine.Object.FindFirstObjectByType(passthroughLayerType) == null)
            {
                report.Add(CriticalVRValidationSeverity.Error, "meta.passthroughMissing", "OVRPassthroughLayer is not present in the scene.", "Add OVRPassthroughLayer and set it to Underlay.");
            }
        }

        private static void PrintSummary(CriticalVRValidationReport report, string relativePath)
        {
            Debug.Log($"Critical-VR validation wrote `{relativePath}` with {report.Issues.Count} entries.");
            foreach (CriticalVRValidationIssue issue in report.Issues)
            {
                string logLine = $"[{issue.Severity}] {issue.Code}: {issue.Message} Fix: {issue.Fix}";
                switch (issue.Severity)
                {
                    case CriticalVRValidationSeverity.Error:
                        Debug.LogError(logLine);
                        break;
                    case CriticalVRValidationSeverity.Warning:
                        Debug.LogWarning(logLine);
                        break;
                    default:
                        Debug.Log(logLine);
                        break;
                }
            }
        }

        private static bool HasAndroidOpenXRSettings()
        {
            return File.Exists(CriticalVRProjectPaths.ToAbsolutePath("Assets/XR/Settings/OpenXRPackageSettings.asset"))
                && File.Exists(CriticalVRProjectPaths.ToAbsolutePath("Assets/XR/Settings/OpenXR Editor Settings.asset"));
        }
    }
}
#endif
