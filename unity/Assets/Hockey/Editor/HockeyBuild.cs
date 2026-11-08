// Сборка и автотест из командной строки (для Claude Code на компьютере разработчика):
//   Unity -batchmode -quit -projectPath unity -executeMethod Hockey.EditorTools.HockeyBuild.Build -logFile Logs/build.log
// Собранная игра кладётся в unity/Build/. Затем её можно запустить с -autotest (см. CLAUDE.md).
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hockey.EditorTools
{
    public static class HockeyBuild
    {
        [MenuItem("Hockey/Автотест (Play)")]
        public static void AutotestInEditor()
        {
            HockeySetup.Run();
            SessionState.SetBool("hockey.autotest", true);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Hockey/Собрать игру")]
        public static void Build()
        {
            HockeySetup.Run();
            BuildTarget target;
            string exe;
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor: target = BuildTarget.StandaloneOSX; exe = "Hockey.app"; break;
                case RuntimePlatform.LinuxEditor: target = BuildTarget.StandaloneLinux64; exe = "Hockey.x86_64"; break;
                default: target = BuildTarget.StandaloneWindows64; exe = "Hockey.exe"; break;
            }
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build"));
            Directory.CreateDirectory(outDir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Hockey/Hockey.unity" },
                locationPathName = Path.Combine(outDir, exe),
                target = target,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[HockeyBuild] {s.result}: {opts.locationPathName}, ошибок {s.totalErrors}, предупреждений {s.totalWarnings}, {s.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
