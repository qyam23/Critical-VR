#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CriticalVR.EditorTools
{
    public static class CriticalVRBuildAutomation
    {
        [MenuItem("CriticalVR/Build/Build Quest Development APK")]
        public static void BuildQuestDevelopmentMenu()
        {
            BuildQuestApk(isDevelopmentBuild: true, validationReportPath: CriticalVRProjectPaths.ValidationReportPath);
        }

        [MenuItem("CriticalVR/Build/Build Quest Release APK")]
        public static void BuildQuestReleaseMenu()
        {
            BuildQuestApk(isDevelopmentBuild: false, validationReportPath: CriticalVRProjectPaths.ValidationReportPath);
        }

        public static void BuildQuestDevelopmentBatch()
        {
            BuildQuestApk(isDevelopmentBuild: true, validationReportPath: CriticalVRProjectPaths.ValidationReportPath);
            EditorApplication.Exit(0);
        }

        public static void BuildQuestReleaseBatch()
        {
            BuildQuestApk(isDevelopmentBuild: false, validationReportPath: CriticalVRProjectPaths.ValidationReportPath);
            EditorApplication.Exit(0);
        }

        public static void BuildQuestDevelopmentCI()
        {
            BuildQuestApk(isDevelopmentBuild: true, validationReportPath: CriticalVRProjectPaths.ValidationReportPath);
        }

        public static void BuildQuestReleaseCI()
        {
            BuildQuestApk(isDevelopmentBuild: false, validationReportPath: CriticalVRProjectPaths.ValidationReportPath);
        }

        private static void BuildQuestApk(bool isDevelopmentBuild, string validationReportPath)
        {
            CriticalVRProjectBootstrap.EnsureQuestProjectScaffold();

            bool skipValidation = string.Equals(Environment.GetEnvironmentVariable("CRITICAL_VR_SKIP_VALIDATION"), "1", StringComparison.OrdinalIgnoreCase);
            CriticalVRValidationReport validationReport = null;
            if (!skipValidation)
            {
                validationReport = CriticalVRProjectValidator.ValidateProject(attemptAutoFix: true, openScene: true);
                CriticalVRProjectValidator.WriteReport(validationReport, validationReportPath);
                if (validationReport.HasErrors)
                {
                    throw new BuildFailedException($"Quest build aborted because validation found blocking issues. See {validationReportPath}");
                }
            }

            string relativeOutputPath = isDevelopmentBuild
                ? CriticalVRProjectPaths.DevelopmentApkRelativePath
                : CriticalVRProjectPaths.ReleaseApkRelativePath;

            CriticalVRProjectPaths.EnsureAbsoluteDirectoryForFile(relativeOutputPath);
            CriticalVRProjectPaths.EnsureAbsoluteDirectoryForFile(CriticalVRProjectPaths.BuildReportRelativePath);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
                locationPathName = CriticalVRProjectPaths.ToAbsolutePath(relativeOutputPath),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = isDevelopmentBuild
                    ? BuildOptions.Development | BuildOptions.AllowDebugging
                    : BuildOptions.None,
            };

            BuildReport buildReport = BuildPipeline.BuildPlayer(options);
            string reportMarkdown = BuildReportToMarkdown(buildReport, relativeOutputPath, skipValidation ? "skipped by environment override" : validationReportPath);
            File.WriteAllText(CriticalVRProjectPaths.ToAbsolutePath(CriticalVRProjectPaths.BuildReportRelativePath), reportMarkdown);

            if (buildReport.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Quest build failed. See {CriticalVRProjectPaths.BuildReportRelativePath}");
            }

            Debug.Log($"Quest build succeeded: {relativeOutputPath}");
        }

        private static string BuildReportToMarkdown(BuildReport buildReport, string apkRelativePath, string validationReportPath)
        {
            BuildSummary summary = buildReport.summary;
            StringBuilder builder = new();
            builder.AppendLine("# Quest Build Report");
            builder.AppendLine();
            builder.AppendLine($"- Generated: `{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC`");
            builder.AppendLine($"- Result: `{summary.result}`");
            builder.AppendLine($"- Platform: `{summary.platform}`");
            builder.AppendLine($"- Output: `{apkRelativePath}`");
            builder.AppendLine($"- Total size: `{summary.totalSize}` bytes");
            builder.AppendLine($"- Total time: `{summary.totalTime}`");
            builder.AppendLine($"- Warnings: `{summary.totalWarnings}`");
            builder.AppendLine($"- Errors: `{summary.totalErrors}`");
            builder.AppendLine($"- Validation report: `{validationReportPath}`");
            builder.AppendLine();

            if (buildReport.steps != null)
            {
                builder.AppendLine("## Build Steps");
                builder.AppendLine();
                foreach (BuildStep step in buildReport.steps)
                {
                    builder.AppendLine($"- `{step.name}`: `{step.duration}`");
                }
            }

            return builder.ToString();
        }
    }
}
#endif
