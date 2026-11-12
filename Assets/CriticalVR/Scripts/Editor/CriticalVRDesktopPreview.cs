#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CriticalVR.EditorTools
{
    /// <summary>Separate, synthetic room for visual tuning. No headset or camera permission needed.</summary>
    public static class CriticalVRDesktopPreview
    {
        private const string Folder = "Assets/CriticalVR/DesktopPreview";

        [MenuItem("CriticalVR/Preview/Create And Play Synthetic Room")]
        public static void CreateAndPlay()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateScene();
            EditorApplication.isPlaying = true;
        }

        public static void CreateScene()
        {
            CriticalVRPreviewRestore.RememberQuestState();
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            Scene scene;
            bool existing = File.Exists(Folder + "/SyntheticRoom.unity");
            scene = existing
                ? EditorSceneManager.OpenScene(Folder + "/SyntheticRoom.unity", OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Folder + "/Renderer.asset");
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, Folder + "/Renderer.asset");
            }
            var shader = Shader.Find("CriticalVR/PassthroughTrip");
            if (shader == null) throw new System.InvalidOperationException("CriticalVR shader not imported.");
            var effect = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Effect.mat");
            if (effect == null)
            {
                effect = new Material(shader);
                AssetDatabase.CreateAsset(effect, Folder + "/Effect.mat");
            }
            foreach (var old in renderer.rendererFeatures) if (old != null) Object.DestroyImmediate(old, true);
            renderer.rendererFeatures.Clear();
            var feature = ScriptableObject.CreateInstance<Rendering.CriticalVRFullscreenRendererFeature>();
            feature.name = "CriticalVR Desktop Effects";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            var featureSettings = new SerializedObject(feature);
            featureSettings.FindProperty("settings").FindPropertyRelative("material").objectReferenceValue = effect;
            featureSettings.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Folder + "/Pipeline.asset");
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, Folder + "/Pipeline.asset");
            }
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 1;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            if (existing) { AssetDatabase.SaveAssets(); return; }
            var cameraObject = new GameObject("DesktopPreviewCamera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.6f, -3f);
            camera.backgroundColor = new Color(0.04f, 0.06f, 0.12f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            var manager = new GameObject("CriticalVRExperienceRoot").AddComponent<MotionToExperienceManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("centerEyeAnchor").objectReferenceValue = camera.transform;
            serialized.FindProperty("effectMaterial").objectReferenceValue = effect;
            serialized.FindProperty("useDebugSlider").boolValue = true;
            serialized.FindProperty("debugIntensity").floatValue = 0.45f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            RoomBox("Floor", new Vector3(0f, -0.1f, 2f), new Vector3(8f, 0.2f, 12f), new Color(0.2f, 0.3f, 0.4f));
            RoomBox("Back wall", new Vector3(0f, 2f, 6f), new Vector3(8f, 4f, 0.2f), new Color(0.35f, 0.4f, 0.5f));
            for (int i = -3; i <= 3; i++)
                RoomBox("Reference pillar " + i, new Vector3(i, 1f, 3f), new Vector3(0.15f, 2f, 0.15f), Color.HSVToRGB((i + 3) / 7f, 0.65f, 0.9f));
            RoomBox("Table", new Vector3(0f, 0.8f, 1f), new Vector3(2f, 0.15f, 1f), new Color(0.7f, 0.4f, 0.2f));
            var light = new GameObject("PreviewLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            EditorSceneManager.SaveScene(scene, Folder + "/SyntheticRoom.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("Synthetic preview: wait 10 seconds, select CriticalVRExperienceRoot and adjust Debug Intensity. Space/B/Y resets. This is NOT passthrough validation. Restore Quest bootstrap before building Android.");
        }

        private static void RoomBox(string name, Vector3 position, Vector3 scale, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            string path = Folder + "/" + name.Replace(" ", "_") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            box.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
#endif
