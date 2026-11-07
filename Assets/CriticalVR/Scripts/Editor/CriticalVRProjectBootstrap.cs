#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace CriticalVR.EditorTools
{
    [InitializeOnLoad]
    public static class CriticalVRProjectBootstrap
    {
        private const string OpenXRLoaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";

        static CriticalVRProjectBootstrap() => ConfigureLocalAndroidTools();

        [MenuItem("CriticalVR/Bootstrap/Run Full Quest Bootstrap")]
        public static void RunFullBootstrap()
        {
            EnsureQuestProjectScaffold();
            Debug.Log("Critical-VR production Quest scene configured. Run validation, then build and test on the headset.");
        }

        public static void RunFromBatchMode()
        {
            EnsureQuestProjectScaffold();
            EditorApplication.Exit(0);
        }

        [MenuItem("CriticalVR/Bootstrap/Configure Android Quest Settings")]
        public static void ConfigureProjectSettings()
        {
            ConfigureLocalAndroidTools();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.companyName = "qyam23";
            PlayerSettings.productName = "Critical-VR";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.qyam23.criticalvr");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.runInBackground = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
        }

        private static void ConfigureLocalAndroidTools()
        {
            string toolchain = Environment.GetEnvironmentVariable("CRITICAL_VR_ANDROID_TOOLS");
            if (string.IsNullOrEmpty(toolchain))
            {
                string localConfig = Path.Combine("UserSettings", "CriticalVRAndroidToolchain.txt");
                if (File.Exists(localConfig)) toolchain = File.ReadAllText(localConfig).Trim();
            }
            if (!string.IsNullOrEmpty(toolchain))
            {
                UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = Path.Combine(toolchain, "SDK");
                UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath = Path.Combine(toolchain, "NDK");
                UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath = Path.Combine(toolchain, "OpenJDK");
            }
        }

        [MenuItem("CriticalVR/Bootstrap/Create Or Repair Quest Assets")]
        public static void CreateOrRepairBootstrapAssets()
        {
            EnsureQuestProjectScaffold();
        }

        [MenuItem("CriticalVR/Bootstrap/Recreate Preview Scene")]
        public static void RecreatePreviewScene()
        {
            EnsureFolders();
            Material material = EnsureMaterial();
            CreateOrRepairRenderPipelineAssets(material);
            CreateSceneAsset(material, recreateScene: true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        internal static void EnsureQuestProjectScaffold()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new BuildFailedException("Quest setup canceled; current scene was not changed.");
            ConfigureProjectSettings();
            EnsureFolders();

            Material material = EnsureMaterial();
            CreateOrRepairRenderPipelineAssets(material);
            EnsureXRSettings();
            CreateSceneAsset(material, recreateScene: false);
            CriticalVRMetaSetup.ConfigureSceneAndManifest(material);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/CriticalVR",
                "Assets/CriticalVR/Scenes",
                "Assets/CriticalVR/Resources",
                "Assets/CriticalVR/Resources/Materials",
                "Assets/CriticalVR/Settings",
                CriticalVRProjectPaths.RenderingSettingsFolder,
            };

            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }
        }

        private static Material EnsureMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CriticalVRProjectPaths.MaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find(CriticalVRProjectPaths.ShaderPath);
            if (shader == null)
            {
                throw new FileNotFoundException($"Shader '{CriticalVRProjectPaths.ShaderPath}' not found. Import scripts and shader assets before bootstrapping.");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, CriticalVRProjectPaths.MaterialPath);
            return material;
        }

        private static void CreateOrRepairRenderPipelineAssets(Material material)
        {
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(CriticalVRProjectPaths.RendererDataPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, CriticalVRProjectPaths.RendererDataPath);
            }

            EnsureRendererFeature(rendererData, material);

            UniversalRenderPipelineAsset pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(CriticalVRProjectPaths.PipelineAssetPath);
            if (pipelineAsset == null)
            {
                pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipelineAsset, CriticalVRProjectPaths.PipelineAssetPath);
            }
            else
            {
                SerializedObject serializedPipeline = new SerializedObject(pipelineAsset);
                SerializedProperty rendererDataList = serializedPipeline.FindProperty("m_RendererDataList");
                if (rendererDataList != null && rendererDataList.arraySize > 0)
                {
                    rendererDataList.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
                    serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            pipelineAsset.supportsCameraDepthTexture = true;
            pipelineAsset.supportsCameraOpaqueTexture = true;
            pipelineAsset.supportsHDR = false;
            pipelineAsset.msaaSampleCount = 1;
            pipelineAsset.renderScale = 1f;

            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(pipelineAsset);
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;
        }

        private static void EnsureRendererFeature(UniversalRendererData rendererData, Material material)
        {
            CriticalVR.Rendering.CriticalVRFullscreenRendererFeature feature = null;
            foreach (var candidate in rendererData.rendererFeatures)
            {
                if (candidate is CriticalVR.Rendering.CriticalVRFullscreenRendererFeature matched)
                {
                    feature = matched;
                    break;
                }
            }

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<CriticalVR.Rendering.CriticalVRFullscreenRendererFeature>();
                feature.name = "CriticalVR Fullscreen Renderer Feature";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
            }

            SerializedObject featureSerializedObject = new SerializedObject(feature);
            SerializedProperty settingsProperty = featureSerializedObject.FindProperty("settings");
            settingsProperty.FindPropertyRelative("material").objectReferenceValue = material;
            settingsProperty.FindPropertyRelative("injectionPoint").intValue = (int)RenderPassEvent.AfterRenderingTransparents;
            featureSerializedObject.ApplyModifiedPropertiesWithoutUndo();

            MethodInfo validateMethod = typeof(ScriptableRendererData).GetMethod("ValidateRendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
            validateMethod?.Invoke(rendererData, null);
            rendererData.SetDirty();
        }

        private static void EnsureXRSettings()
        {
            MethodInfo getOrCreateMethod = typeof(XRGeneralSettingsPerBuildTarget).GetMethod("GetOrCreate", BindingFlags.Static | BindingFlags.NonPublic);
            XRGeneralSettingsPerBuildTarget generalSettings = getOrCreateMethod?.Invoke(null, null) as XRGeneralSettingsPerBuildTarget;
            if (generalSettings == null)
            {
                throw new BuildFailedException("Unable to create XRGeneralSettingsPerBuildTarget asset.");
            }
            if (!generalSettings.HasSettingsForBuildTarget(BuildTargetGroup.Android))
            {
                generalSettings.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            }

            XRGeneralSettings androidSettings = generalSettings.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (androidSettings != null)
            {
                androidSettings.InitManagerOnStart = true;
            }

            if (!generalSettings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            {
                generalSettings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            }

            XRManagerSettings managerSettings = generalSettings.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (managerSettings != null)
            {
                XRPackageMetadataStore.AssignLoader(managerSettings, OpenXRLoaderTypeName, BuildTargetGroup.Android);
            }

            EnsureOpenXRPackageSettings();
        }

        private static void EnsureOpenXRPackageSettings()
        {
            Type packageSettingsType = CriticalVRProjectPaths.FindType("UnityEditor.XR.OpenXR.OpenXRPackageSettings");
            MethodInfo getOrCreateInstance = packageSettingsType?.GetMethod("GetOrCreateInstance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            object packageSettings = getOrCreateInstance?.Invoke(null, null);
            MethodInfo getSettingsMethod = packageSettingsType?.GetMethod("GetSettingsForBuildTargetGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            getSettingsMethod?.Invoke(packageSettings, new object[] { BuildTargetGroup.Android });

            if (packageSettings is UnityEngine.Object packageSettingsObject)
            {
                EditorUtility.SetDirty(packageSettingsObject);
                AssetDatabase.SaveAssets();
            }
        }

        private static void CreateSceneAsset(Material material, bool recreateScene)
        {
            // Upgrade a copy; keep the user's original bootstrap scene intact.
            if (!File.Exists(CriticalVRProjectPaths.ToAbsolutePath(CriticalVRProjectPaths.ScenePath)) &&
                File.Exists(CriticalVRProjectPaths.ToAbsolutePath(CriticalVRProjectPaths.OriginalScenePath)))
                AssetDatabase.CopyAsset(CriticalVRProjectPaths.OriginalScenePath, CriticalVRProjectPaths.ScenePath);
            if (recreateScene || !File.Exists(CriticalVRProjectPaths.ToAbsolutePath(CriticalVRProjectPaths.ScenePath)))
            {
                CreateNewScene(material);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(CriticalVRProjectPaths.ScenePath, OpenSceneMode.Single);
            RepairScene(scene, material);
            EditorSceneManager.SaveScene(scene, CriticalVRProjectPaths.ScenePath);
        }

        private static void CreateNewScene(Material material)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "CriticalVR_QuestBootstrap";

            GameObject root = new("CriticalVRExperienceRoot");
            MotionToExperienceManager manager = root.AddComponent<MotionToExperienceManager>();

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObject = new("Main Camera");
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            GameObject rigRoot = new("XROriginPreview");
            mainCamera.transform.SetParent(rigRoot.transform, false);
            mainCamera.transform.localPosition = Vector3.zero;
            mainCamera.transform.localRotation = Quaternion.identity;
            mainCamera.name = "CenterEyeAnchorPreview";
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.black;
            mainCamera.nearClipPlane = 0.01f;
            mainCamera.farClipPlane = 1000f;

            EnsurePreviewOverlay(mainCamera, material);
            AssignManagerReferences(manager, mainCamera.transform, material);

            Light sceneLight = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (sceneLight != null)
            {
                sceneLight.type = LightType.Directional;
                sceneLight.intensity = 0.2f;
                sceneLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            EditorSceneManager.SaveScene(scene, CriticalVRProjectPaths.ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(CriticalVRProjectPaths.ScenePath, true) };
        }

        private static void RepairScene(Scene scene, Material material)
        {
            MotionToExperienceManager manager = UnityEngine.Object.FindFirstObjectByType<MotionToExperienceManager>();
            if (manager == null)
            {
                GameObject root = GameObject.Find("CriticalVRExperienceRoot") ?? new GameObject("CriticalVRExperienceRoot");
                manager = root.GetComponent<MotionToExperienceManager>() ?? root.AddComponent<MotionToExperienceManager>();
            }

            Transform centerEye = FindCenterEyeAnchor();
            if (centerEye == null)
            {
                GameObject previewRig = GameObject.Find("XROriginPreview") ?? new GameObject("XROriginPreview");
                Camera mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    GameObject cameraObject = new("Main Camera");
                    mainCamera = cameraObject.AddComponent<Camera>();
                    cameraObject.tag = "MainCamera";
                }

                mainCamera.transform.SetParent(previewRig.transform, false);
                mainCamera.name = "CenterEyeAnchorPreview";
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = Color.black;
                mainCamera.nearClipPlane = 0.01f;
                mainCamera.farClipPlane = 1000f;
                centerEye = mainCamera.transform;
                EnsurePreviewOverlay(mainCamera, material);
            }

            AssignManagerReferences(manager, centerEye, material);

            if (EditorBuildSettings.scenes.Length == 0 || EditorBuildSettings.scenes[0].path != CriticalVRProjectPaths.ScenePath)
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(CriticalVRProjectPaths.ScenePath, true) };
            }
        }

        private static void EnsurePreviewOverlay(Camera mainCamera, Material material)
        {
            // A fullscreen-triangle blit shader cannot also render on a mesh quad.
            GameObject previewQuad = GameObject.Find("CriticalVRPreviewOverlay");
            if (previewQuad != null) UnityEngine.Object.DestroyImmediate(previewQuad);
        }

        private static void AssignManagerReferences(MotionToExperienceManager manager, Transform centerEyeAnchor, Material material)
        {
            SerializedObject serializedManager = new(manager);
            serializedManager.FindProperty("centerEyeAnchor").objectReferenceValue = centerEyeAnchor;
            serializedManager.FindProperty("effectMaterial").objectReferenceValue = material;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        private static Transform FindCenterEyeAnchor()
        {
            string[] candidateNames =
            {
                "CenterEyeAnchor",
                "CenterEyeAnchorPreview",
            };

            foreach (string candidate in candidateNames)
            {
                GameObject match = GameObject.Find(candidate);
                if (match != null)
                {
                    return match.transform;
                }
            }

            return Camera.main != null ? Camera.main.transform : null;
        }
    }
}
#endif
