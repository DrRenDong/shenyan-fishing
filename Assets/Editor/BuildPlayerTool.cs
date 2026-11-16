using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 命令行打包入口（Windows + Android）：
/// Unity.exe -batchmode -quit -projectPath "E:\UNITY\ShenyanFishing" -executeMethod BuildPlayerTool.BuildAll -logFile &lt;log&gt;
/// 注意：Android 必须在「纯 ASCII 路径」下构建（用 E:\UNITY\ShenyanFishing 这个 junction），
/// 并且要显式设置 ANDROID_NDK_ROOT，否则 Burst 的 AOT 会失败。
/// </summary>
public static class BuildPlayerTool
{
    const string WindowsDir = "Builds/Windows";
    const string AndroidDir = "Builds/Android";

    [MenuItem("Build/打包 Windows + Android")]
    public static void BuildAll()
    {
        BuildWindows();
        BuildAndroid();
    }

    public static void BuildWindows()
    {
        Directory.CreateDirectory(WindowsDir);
        var opts = new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = WindowsDir + "/ShenyanFishing.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        Run(opts, "Windows");
    }

    public static void BuildAndroid()
    {
        string ndk = Path.Combine(EditorApplication.applicationContentsPath,
                                  "PlaybackEngines/AndroidPlayer/NDK");
        Environment.SetEnvironmentVariable("ANDROID_NDK_ROOT", ndk, EnvironmentVariableTarget.Process);

        // 手机与商店都要求 64 位：用 IL2CPP，同时出 ARMv7 + ARM64（老机、新机都能装）
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

        Directory.CreateDirectory(AndroidDir);
        EditorUserBuildSettings.buildAppBundle = false;   // 出 APK，方便直接装手机
        EditorUserBuildSettings.development = false;

        var opts = new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = AndroidDir + "/ShenyanFishing.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None,
        };
        Run(opts, "Android");
    }

    static string[] EnabledScenes()
    {
        var list = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled) list.Add(s.path);
        return list.ToArray();
    }

    static void Run(BuildPlayerOptions opts, string tag)
    {
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary s = report.summary;
        float mb = s.totalSize / (1024f * 1024f);
        Debug.Log($"[BUILD][{tag}] result={s.result} out={s.outputPath} size={mb:0.0}MB " +
                  $"errors={s.totalErrors} warnings={s.totalWarnings} time={s.totalTime}");

        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[BUILD][{tag}] 打包失败");
            EditorApplication.Exit(1);
        }
    }
}
