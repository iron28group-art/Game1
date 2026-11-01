using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MagicTree.EditorTools
{
    /// <summary>
    /// Android APK 빌드.
    /// - 에디터 메뉴: 요술나무 ▸ Android APK 빌드  → build/Android/MagicTree.apk
    /// - CI(GameCI unity-builder): buildMethod = MagicTree.EditorTools.BuildScript.BuildAndroid
    ///
    /// 게임은 GameBootstrap 이 코드로 UI 를 만들기 때문에 씬은 빈 씬 하나면 된다.
    /// 씬 파일이 없으면 여기서 만들어 저장한다.
    /// </summary>
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string DefaultOutput = "build/Android/MagicTree.apk";

        [MenuItem("요술나무/Android APK 빌드")]
        public static void BuildAndroidMenu() => Build(DefaultOutput, exitOnFinish: false);

        /// <summary>CI 진입점. GameCI 가 넘겨 주는 -customBuildPath 를 따른다.</summary>
        public static void BuildAndroid()
        {
            string output = GetArg("-customBuildPath") ?? DefaultOutput;
            if (!output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                output = Path.Combine(output, "MagicTree.apk");
            Build(output, exitOnFinish: true);
        }

        static void Build(string output, bool exitOnFinish)
        {
            EnsureScene();
            ApplyPlayerSettings();

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            EditorUserBuildSettings.buildAppBundle = false; // .aab 가 아니라 바로 설치하는 .apk

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });

            var s = report.summary;
            Debug.Log($"[BuildScript] {s.result}  {output}  ({s.totalSize / (1024 * 1024)} MB, 오류 {s.totalErrors}개)");
            if (exitOnFinish) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
            else if (s.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(output);
        }

        static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "요술나무";
            PlayerSettings.companyName = "Iron28";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.iron28.magictree");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;

            // 세로 고정 (UI 기준 해상도 1080×1920)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // 최신 64비트 전용 폰에서도 돌도록 IL2CPP + ARM64
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
