#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace CriticalVR.EditorTools
{
    internal static class CriticalVRProjectPaths
    {
        internal const string TargetBranch = "codex/vr-preview-readiness-2026-11-06";

        internal const string ScenePath = "Assets/CriticalVR/Scenes/CriticalVR_QuestCamera.unity";
        internal const string OriginalScenePath = "Assets/CriticalVR/Scenes/CriticalVR_QuestBootstrap.unity";
        internal const string MaterialPath = "Assets/CriticalVR/Resources/Materials/CriticalVRPassthroughTrip.mat";
        internal const string ShaderPath = "CriticalVR/PassthroughTrip";
        internal const string RenderingSettingsFolder = "Assets/CriticalVR/Settings/Rendering";
        internal const string RendererDataPath = "Assets/CriticalVR/Settings/Rendering/CriticalVR_Renderer.asset";
        internal const string PipelineAssetPath = "Assets/CriticalVR/Settings/Rendering/CriticalVR_URP.asset";
        internal const string ValidationReportPath = "Docs/Quest/ValidationReport.latest.md";
        internal const string ProgressReportPath = "Docs/Quest/IMPLEMENTATION_PROGRESS_REPORT.md";

        internal const string BuildRoot = "Builds/Quest";
        internal const string BuildLogFolder = "Builds/Quest/Logs";
        internal const string DevelopmentApkRelativePath = "Builds/Quest/Development/CriticalVR-Quest-Development.apk";
        internal const string ReleaseApkRelativePath = "Builds/Quest/Release/CriticalVR-Quest-Release.apk";
        internal const string BuildReportRelativePath = "Builds/Quest/Logs/last-build-report.md";

        internal static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        internal static string ToAbsolutePath(string relativePath)
        {
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(ProjectRoot, normalized));
        }

        internal static void EnsureAbsoluteDirectoryForFile(string relativePath)
        {
            string absolute = ToAbsolutePath(relativePath);
            string directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        internal static string ReadCurrentGitBranch()
        {
            try
            {
                string headPath = ToAbsolutePath(".git/HEAD");
                if (!File.Exists(headPath))
                {
                    return "unknown";
                }

                string headContents = File.ReadAllText(headPath).Trim();
                if (headContents.StartsWith("ref: ", StringComparison.OrdinalIgnoreCase))
                {
                    string branchRef = headContents.Substring(5).Trim();
                    return branchRef.Replace("refs/heads/", string.Empty);
                }

                return "detached";
            }
            catch
            {
                return "unknown";
            }
        }

        internal static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
#endif
