using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一鍵出 APK（不改任何 PlayerSettings / package id，照現有 Build Settings 場景）。
///  - Editor 內：選單 SPPB → Build APK（不必關 Editor）
///  - Batchmode：Unity.exe -quit -batchmode -buildTarget Android -executeMethod SppbBuild.BuildAPK -logFile build.log
/// 輸出：Builds/SPPBV2_yyyyMMdd_HHmm.apk
/// </summary>
public static class SppbBuild
{
    [MenuItem("SPPB/Build APK")]
    public static void BuildAPKFromMenu()
    {
        bool ok = Build(out string path);
        if (ok)
        {
            EditorUtility.RevealInFinder(path);
            EditorUtility.DisplayDialog("SPPB Build", $"APK 打包完成：\n{path}", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("SPPB Build", "打包失敗，請看 Console 錯誤訊息。", "OK");
        }
    }

    /// <summary>Batchmode 入口：打包完依結果結束 Unity。</summary>
    public static void BuildAPK()
    {
        bool ok = Build(out _);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool Build(out string outputPath)
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        EditorUserBuildSettings.buildAppBundle = false; // 要 APK 不要 AAB
        Directory.CreateDirectory("Builds");
        outputPath = $"Builds/SPPBV2_{DateTime.Now:yyyyMMdd_HHmm}.apk";

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log($"[SppbBuild] result={s.result} errors={s.totalErrors} scenes={scenes.Length} output={s.outputPath}");
        return s.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
    }
}
