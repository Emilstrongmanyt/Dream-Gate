using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kindling.EditorTools
{
    public static class KindlingImport
    {
        const int ProductId = 165698;
        const string ResourceDir = "Assets/Kindling/Resources/CardShirts";

        [MenuItem("Kindling/Import Card Shirts Lite")]
        public static void ImportCardShirtsLite()
        {
            string pkg = FindDownloadedPackage();
            if (string.IsNullOrEmpty(pkg))
            {
                Debug.Log("Card shirts Lite not on disk yet. Opening Asset Store page (id " + ProductId + ").");
                Application.OpenURL("com.unity3d.kharma:content/" + ProductId);
                TryDownloadWithToken();
                pkg = FindDownloadedPackage();
            }
            if (string.IsNullOrEmpty(pkg))
            {
                Debug.LogError("Download Card shirts Lite from Package Manager > My Assets, then run Kindling/Import Card Shirts Lite.");
                return;
            }
            Debug.Log("Importing " + pkg);
            AssetDatabase.ImportPackage(pkg, false);
            EditorApplication.delayCall += CopyShirtsToResources;
        }

        public static void ImportCardShirtsLiteBatch()
        {
            ImportCardShirtsLite();
            CopyShirtsToResources();
        }

        static string FindDownloadedPackage()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Unity", "Asset Store-5.x");
            if (!Directory.Exists(root)) return null;
            string[] files = Directory.GetFiles(root, "*.unitypackage", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string n = files[i];
                if (n.IndexOf("shirt", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Shirt", StringComparison.Ordinal) >= 0)
                    return n;
            }
            return null;
        }

        static void TryDownloadWithToken()
        {
            try
            {
                var connect = Type.GetType("UnityEditor.Connect.UnityConnect,UnityEditor.CoreModule");
                if (connect == null)
                    connect = Type.GetType("UnityEditor.Connect.UnityConnect,UnityEditor");
                if (connect == null) return;
                var inst = connect.GetProperty("instance")?.GetValue(null);
                if (inst == null) return;
                var tokenObj = connect.GetMethod("GetAccessToken")?.Invoke(inst, null);
                string token = tokenObj as string;
                if (string.IsNullOrEmpty(token)) return;
                string url = "https://packages-v2.unity.com/api/v1/asset-store/download/" + ProductId;
                var req = UnityEngine.Networking.UnityWebRequest.Get(url);
                req.SetRequestHeader("Authorization", "Bearer " + token);
                var op = req.SendWebRequest();
                while (!op.isDone) { }
                Debug.Log("Asset download response " + req.responseCode + " " + req.error);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Token download skipped: " + ex.Message);
            }
        }

        static void CopyShirtsToResources()
        {
            Directory.CreateDirectory(ResourceDir);
            string[] roots =
            {
                "Assets/Card shirts Lite",
                "Assets/Card Shirts Lite",
                "Assets/CardshirtsLite",
                "Assets/Saji"
            };
            int copied = 0;
            for (int r = 0; r < roots.Length; r++)
            {
                if (!Directory.Exists(roots[r])) continue;
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { roots[r] });
                for (int i = 0; i < (guids != null ? guids.Length : 0); i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path)) continue;
                    if (path.IndexOf(".png", StringComparison.OrdinalIgnoreCase) < 0
                        && path.IndexOf(".jpg", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    string dest = ResourceDir + "/" + Path.GetFileName(path);
                    if (AssetDatabase.CopyAsset(path, dest)) copied++;
                    TextureImporter imp = AssetImporter.GetAtPath(dest) as TextureImporter;
                    if (imp != null)
                    {
                        imp.textureType = TextureImporterType.Sprite;
                        imp.spriteImportMode = SpriteImportMode.Single;
                        imp.alphaIsTransparency = true;
                        imp.SaveAndReimport();
                    }
                }
            }
            AssetDatabase.Refresh();
            Debug.Log("Copied " + copied + " card shirts into " + ResourceDir);
        }
    }
}
