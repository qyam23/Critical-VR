#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.OpenXR;

namespace CriticalVR.EditorTools
{
    /// <summary>Idempotent Quest camera setup, run by every validated build.</summary>
    public static class CriticalVRMetaSetup
    {
        public static void ConfigureSceneAndManifest(Material material)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != CriticalVRProjectPaths.ScenePath)
                throw new BuildFailedException("Meta setup requires the production QuestCamera scene.");
            var rig = Object.FindFirstObjectByType<OVRCameraRig>();
            if (rig == null)
            {
                const string prefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) throw new BuildFailedException("Meta XR Core 85 rig prefab not imported.");
                var rigObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                rig = rigObject.GetComponent<OVRCameraRig>();
            }
            var preview = GameObject.Find("XROriginPreview");
            if (preview != null) preview.SetActive(false);
            // Preserve legacy objects, but prevent a second eye camera rendering over the rig.
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (!camera.transform.IsChildOf(rig.transform)) camera.gameObject.SetActive(false);

            var ovrManager = rig.GetComponent<OVRManager>();
            if (ovrManager == null) ovrManager = rig.gameObject.AddComponent<OVRManager>();
            var managerSettings = new SerializedObject(ovrManager);
            managerSettings.FindProperty("isInsightPassthroughEnabled").boolValue = true;
            managerSettings.FindProperty("requestPassthroughCameraAccessPermissionOnStartup").boolValue = false;
            managerSettings.ApplyModifiedPropertiesWithoutUndo();
            var layer = rig.GetComponent<OVRPassthroughLayer>();
            if (layer == null) layer = rig.gameObject.AddComponent<OVRPassthroughLayer>();
            var layerSettings = new SerializedObject(layer);
            layerSettings.FindProperty("overlayType").intValue = (int)OVROverlay.OverlayType.Underlay;
            layerSettings.ApplyModifiedPropertiesWithoutUndo();

            var eye = rig.centerEyeAnchor.GetComponent<Camera>();
            eye.tag = "MainCamera";
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = new Color(0f, 0f, 0f, 0f);
            eye.nearClipPlane = 0.05f;
            eye.GetUniversalAdditionalCameraData().allowXRRendering = true;
            var experience = Object.FindFirstObjectByType<MotionToExperienceManager>();
            if (experience == null) throw new BuildFailedException("Motion runtime missing from production scene.");
            var motionSettings = new SerializedObject(experience);
            motionSettings.FindProperty("centerEyeAnchor").objectReferenceValue = rig.centerEyeAnchor;
            motionSettings.FindProperty("effectMaterial").objectReferenceValue = material;
            motionSettings.FindProperty("useDebugSlider").boolValue = false;
            motionSettings.ApplyModifiedPropertiesWithoutUndo();
            var bridge = experience.GetComponent<QuestPassthroughCameraBridge>();
            if (bridge == null) bridge = experience.gameObject.AddComponent<QuestPassthroughCameraBridge>();
            var bridgeSettings = new SerializedObject(bridge);
            bridgeSettings.FindProperty("effectMaterial").objectReferenceValue = material;
            bridgeSettings.FindProperty("experience").objectReferenceValue = experience;
            bridgeSettings.FindProperty("eyeCamera").objectReferenceValue = eye;
            var statusRoot = rig.centerEyeAnchor.Find("CriticalVRStatus");
            UnityEngine.UI.Text text;
            if (statusRoot == null)
            {
                var canvasObject = new GameObject("CriticalVRStatus", typeof(RectTransform), typeof(Canvas));
                canvasObject.transform.SetParent(rig.centerEyeAnchor, false);
                canvasObject.transform.localPosition = new Vector3(0f, -0.3f, 1.5f);
                canvasObject.transform.localScale = Vector3.one * 0.0015f;
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = eye;
                var rect = (RectTransform)canvasObject.transform;
                rect.sizeDelta = new Vector2(800f, 140f);
                var label = new GameObject("Status", typeof(RectTransform), typeof(UnityEngine.UI.Text));
                label.transform.SetParent(canvasObject.transform, false);
                var labelRect = (RectTransform)label.transform;
                labelRect.sizeDelta = rect.sizeDelta;
                text = label.GetComponent<UnityEngine.UI.Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 26;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
                text.text = "Critical-VR\nB / Y: stop effects    A / X: resume or retry";
            }
            else text = statusRoot.GetComponentInChildren<UnityEngine.UI.Text>(true);
            bridgeSettings.FindProperty("statusText").objectReferenceValue = text;
            bridgeSettings.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null) throw new BuildFailedException("Android OpenXR settings missing.");
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            var metaFeature = settings.GetFeatures<Meta.XR.MetaXRFeature>().FirstOrDefault();
            if (metaFeature == null) throw new BuildFailedException("Meta XR OpenXR feature missing; finish importing SDK and restart Unity.");
            metaFeature.enabled = true;
            foreach (var feature in settings.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())
            {
                string typeName = feature.GetType().Name;
                if (typeName == "OculusTouchControllerProfile") feature.enabled = true;
            }
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(metaFeature);

            var config = OVRProjectConfig.CachedProjectConfig;
            config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
            config.isPassthroughCameraAccessEnabled = true;
            config.sceneSupport = OVRProjectConfig.FeatureSupport.None;
            config.anchorSupport = OVRProjectConfig.AnchorSupport.Disabled;
            config.minHorizonOsSdkVersion = 74;
            config.targetDeviceTypes = new() { OVRProjectConfig.DeviceType.Quest3, OVRProjectConfig.DeviceType.Quest3S };
            EditorUtility.SetDirty(config);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene.path, true) };
            AssetDatabase.SaveAssets();
            OVRManifestPreprocessor.GenerateOrUpdateAndroidManifest(silentMode: true);
            const string manifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
            if (!File.Exists(manifestPath) || !File.ReadAllText(manifestPath).Contains("horizonos.permission.HEADSET_CAMERA"))
                throw new BuildFailedException("Meta manifest generator did not add HEADSET_CAMERA permission.");
            Debug.Log("CriticalVR: production rig, underlay, camera bridge, OpenXR feature and manifest configured. Hardware validation remains required.");
        }
    }
}
#endif
