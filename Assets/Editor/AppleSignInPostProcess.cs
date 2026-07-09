using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace DreamGate.Editor
{
    public static class AppleSignInPostProcess
    {
        private const string EntitlementsFileName = "DreamGate.entitlements";

        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
        {
#if !UNITY_IOS
            return;
#else
            if (buildTarget != BuildTarget.iOS)
            {
                return;
            }

            var sourceEntitlements = Path.Combine(UnityEngine.Application.dataPath, "Plugins", "iOS", EntitlementsFileName);
            if (!File.Exists(sourceEntitlements))
            {
                UnityEngine.Debug.LogWarning($"Apple Sign In entitlements file not found at {sourceEntitlements}");
                return;
            }

            var destinationEntitlements = Path.Combine(pathToBuiltProject, EntitlementsFileName);
            File.Copy(sourceEntitlements, destinationEntitlements, true);

            var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var mainTarget = project.GetUnityMainTargetGuid();
            project.AddCapability(mainTarget, PBXCapabilityType.SignInWithApple, EntitlementsFileName);
            project.SetBuildProperty(mainTarget, "CODE_SIGN_ENTITLEMENTS", EntitlementsFileName);

            project.WriteToFile(projectPath);
#endif
        }
    }
}