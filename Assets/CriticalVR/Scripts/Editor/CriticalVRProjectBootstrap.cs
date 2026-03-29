#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CriticalVR.EditorTools
{
    public static class CriticalVRProjectBootstrap
    {
        private const string MaterialPath = "Assets/CriticalVR/Resources/Materials/CriticalVRPassthroughTrip.mat";
        private const string ScenePath = "Assets/CriticalVR/Scenes/CriticalVR_QuestBootstrap.unity";
        private const string ShaderPath = "CriticalVR/PassthroughTrip";

        [MenuItem("CriticalVR/Bootstrap/Run Full Quest Bootstrap")]
        public static void RunFullBootstrap()
        {
            ConfigureProjectSettings();
            CreateBootstrapAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Critical-VR bootstrap completed. Import Meta XR packages next, then swap the preview rig for OVRCameraRig.");
        }

        public static void RunFromBatchMode()
        {
            RunFullBootstrap();
            EditorApplication.Exit(0);
        }

        [MenuItem("CriticalVR/Bootstrap/Configure Android Quest Settings")]
        public static void ConfigureProjectSettings()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.companyName = "qyam23";
            PlayerSettings.productName = "Critical-VR";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.qyam23.criticalvr");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.runInBackground = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
        }

        [MenuItem("CriticalVR/Bootstrap/Create Scene And Material")]
        public static void CreateBootstrapAssets()
        {
            EnsureFolders();

            Material material = EnsureMaterial();

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

            GameObject previewQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            previewQuad.name = "CriticalVRPreviewOverlay";
            previewQuad.transform.SetParent(mainCamera.transform, false);
            previewQuad.transform.localPosition = new Vector3(0f, 0f, 0.75f);
            previewQuad.transform.localScale = new Vector3(1.6f, 0.9f, 1f);

            MeshRenderer meshRenderer = previewQuad.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }

            SerializedObject serializedManager = new(manager);
            serializedManager.FindProperty("centerEyeAnchor").objectReferenceValue = mainCamera.transform;
            serializedManager.FindProperty("effectMaterial").objectReferenceValue = material;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            Light sceneLight = Object.FindFirstObjectByType<Light>();
            if (sceneLight != null)
            {
                sceneLight.type = LightType.Directional;
                sceneLight.intensity = 0.2f;
                sceneLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/CriticalVR",
                "Assets/CriticalVR/Scenes",
                "Assets/CriticalVR/Resources",
                "Assets/CriticalVR/Resources/Materials",
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
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find(ShaderPath);
            if (shader == null)
            {
                throw new FileNotFoundException($"Shader '{ShaderPath}' not found. Import scripts and shader assets before bootstrapping.");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
#endif
