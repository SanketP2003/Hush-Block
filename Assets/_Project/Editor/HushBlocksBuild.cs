using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HushBlocks.Editor
{
    /// <summary>
    /// Automation pipeline for Standalone Windows x64 builds of Hush Blocks.
    /// Exposes Unity Editor menu item and CLI batchmode entry point.
    /// </summary>
    public static class HushBlocksBuild
    {
        public const string OutputPath = "Builds/Windows/HushBlocks.exe";

        [MenuItem("Hush Blocks/Build/Windows x64", priority = 50)]
        public static void BuildWindows()
        {
            Debug.Log("[HushBlocksBuild] Starting Windows x64 build pipeline...");

            // 1. Collect all enabled scenes from EditorBuildSettings
            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(s.path))
                {
                    scenes.Add(s.path);
                    Debug.Log($"[HushBlocksBuild] Including scene: {s.path}");
                }
            }

            if (scenes.Count == 0)
            {
                throw new InvalidOperationException("[HushBlocksBuild] No enabled scenes found in EditorBuildSettings! Run 'Hush Blocks/Build Full Game Scene' first.");
            }

            // 2. Ensure output directory exists
            string outputDir = Path.GetDirectoryName(OutputPath);
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 3. Configure build options
            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            // 4. Execute build
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[HushBlocksBuild] Build finished with result: {summary.result}");
            Debug.Log($"[HushBlocksBuild] Total Time: {summary.totalTime.TotalSeconds:F1}s | Output Size: {summary.totalSize / (1024 * 1024)} MB | Errors: {summary.totalErrors} | Warnings: {summary.totalWarnings}");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"[HushBlocksBuild] Windows x64 build failed! Result: {summary.result}, Errors: {summary.totalErrors}");
            }
        }
    }
}

