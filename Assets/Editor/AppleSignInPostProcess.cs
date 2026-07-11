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
        private const string OAuthUrlScheme = "com.solodreams.dreamgate";

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
            EnsureOAuthUrlScheme(pathToBuiltProject);
#endif
        }

#if UNITY_IOS
        private static void EnsureOAuthUrlScheme(string pathToBuiltProject)
        {
            var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            if (!File.Exists(plistPath))
            {
                UnityEngine.Debug.LogWarning($"Info.plist not found at {plistPath}");
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;

            PlistElementArray urlTypes;
            if (root.values.TryGetValue("CFBundleURLTypes", out var existingUrlTypes))
            {
                urlTypes = existingUrlTypes.AsArray();
            }
            else
            {
                urlTypes = root.CreateArray("CFBundleURLTypes");
            }

            foreach (var entry in urlTypes.values)
            {
                var dict = entry.AsDict();
                if (!dict.values.TryGetValue("CFBundleURLSchemes", out var schemesElement))
                {
                    continue;
                }

                foreach (var scheme in schemesElement.AsArray().values)
                {
                    if (scheme.AsString() == OAuthUrlScheme)
                    {
                        UnityEngine.Debug.Log($"OAuth URL scheme already present in Info.plist: {OAuthUrlScheme}");
                        return;
                    }
                }
            }

            var urlType = urlTypes.AddDict();
            urlType.SetString("CFBundleURLName", OAuthUrlScheme);
            var schemes = urlType.CreateArray("CFBundleURLSchemes");
            schemes.AddString(OAuthUrlScheme);
            plist.WriteToFile(plistPath);
            UnityEngine.Debug.Log($"Added OAuth URL scheme to Info.plist: {OAuthUrlScheme}");
        }
#endif
    }
}