using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kindling.EditorTools
{
    public static class KindlingBuild
    {
        const string ScenePath = "Assets/Kindling/Scenes/Match.unity";

        [MenuItem("Kindling/Ensure Match Scene")]
        public static void EnsureMatchScene()
        {
            Directory.CreateDirectory("Assets/Kindling/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Saved " + ScenePath + " and added it to Editor Build Settings.");
        }

        public static void BuildIos()
        {
            EnsureMatchScene();
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.solodreams.dreamgate");
            PlayerSettings.productName = "Kindling";
            PlayerSettings.companyName = "Kindling";
            PlayerSettings.bundleVersion = "1.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "build/iOS",
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception("iOS build failed: " + report.summary.result);
        }
    }
}
