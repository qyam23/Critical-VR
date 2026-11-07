#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

namespace CriticalVR.EditorTools
{
    [InitializeOnLoad]
    internal static class CriticalVRPreviewRestore
    {
        private const string Key = "CriticalVR.PreviewRestore.";
        static CriticalVRPreviewRestore()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) Restore();
            };
        }

        internal static void RememberQuestState()
        {
            if (SessionState.GetBool(Key + "Pending", false)) return;
            SessionState.SetString(Key + "Pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + "Quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetString(Key + "Scene", EditorSceneManager.GetActiveScene().path);
            SessionState.SetBool(Key + "Background", PlayerSettings.runInBackground);
            PlayerSettings.runInBackground = true;
            if (XRGeneralSettings.Instance != null)
            {
                SessionState.SetString(Key + "XR", AssetDatabase.GetAssetPath(XRGeneralSettings.Instance));
                SessionState.SetBool(Key + "InitXR", XRGeneralSettings.Instance.InitManagerOnStart);
                XRGeneralSettings.Instance.InitManagerOnStart = false;
            }
            SessionState.SetBool(Key + "Pending", true);
        }

        [MenuItem("CriticalVR/Preview/Restore Quest Scene And Pipeline")]
        public static void Restore()
        {
            if (!SessionState.GetBool(Key + "Pending", false)) return;
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + "Pipeline", ""));
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + "Quality", ""));
            PlayerSettings.runInBackground = SessionState.GetBool(Key + "Background", false);
            var xr = AssetDatabase.LoadAssetAtPath<XRGeneralSettings>(SessionState.GetString(Key + "XR", ""));
            if (xr != null) xr.InitManagerOnStart = SessionState.GetBool(Key + "InitXR", true);
            string scene = SessionState.GetString(Key + "Scene", "");
            if (!string.IsNullOrEmpty(scene) && System.IO.File.Exists(scene))
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            SessionState.SetBool(Key + "Pending", false);
            Debug.Log("CriticalVR: restored pre-preview scene, render pipeline, and XR initialization.");
        }
    }
}
#endif
