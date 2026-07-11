using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    public sealed class GoogleSignInNative : MonoBehaviour
    {
        private const string CallbackHostName = "DreamGateGoogleSignIn";
        private static GoogleSignInNative instance;
        private static Action<GoogleSignInCredential> pendingCallback;

        public static void Warmup()
        {
            var host = Instance;
            host.gameObject.SetActive(true);
        }

        public static GoogleSignInNative Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                var host = GameObject.Find(CallbackHostName);
                if (host == null)
                {
                    host = new GameObject(CallbackHostName);
                    DontDestroyOnLoad(host);
                }

                instance = host.GetComponent<GoogleSignInNative>();
                if (instance == null)
                {
                    instance = host.AddComponent<GoogleSignInNative>();
                }

                return instance;
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DreamGate_GoogleSignIn_Request(
            string authUrl,
            string callbackScheme,
            string callbackObject,
            string callbackMethod);
#endif

        public static void RequestAuthorization(string authUrl, Action<GoogleSignInCredential> callback)
        {
            pendingCallback = callback;
#if UNITY_IOS && !UNITY_EDITOR
            DreamGate_GoogleSignIn_Request(
                authUrl,
                OAuthConstants.RedirectScheme,
                Instance.gameObject.name,
                nameof(OnNativeCallback));
#else
            InvokeFallback("Sign in with Google is only available on iOS devices.");
#endif
        }

        public void OnNativeCallback(string json)
        {
            var credential = GoogleSignInCredential.FromJson(json);
            var callback = pendingCallback;
            pendingCallback = null;
            callback?.Invoke(credential);
        }

        private static void InvokeFallback(string error)
        {
            var callback = pendingCallback;
            pendingCallback = null;
            callback?.Invoke(GoogleSignInCredential.Failed(error));
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }

    public sealed class GoogleSignInCredential
    {
        public bool Success;
        public string CallbackUrl = string.Empty;
        public string Error = string.Empty;

        public static GoogleSignInCredential Failed(string error)
        {
            return new GoogleSignInCredential
            {
                Success = false,
                Error = string.IsNullOrWhiteSpace(error) ? "Google sign in failed." : error
            };
        }

        public static GoogleSignInCredential FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Failed("Google sign in returned an empty response.");
            }

            var payload = JsonUtility.FromJson<GoogleSignInPayload>(json);
            if (payload == null)
            {
                return Failed("Google sign in returned an invalid response.");
            }

            if (payload.success == 0)
            {
                return Failed(payload.error);
            }

            return new GoogleSignInCredential
            {
                Success = true,
                CallbackUrl = payload.callbackUrl ?? string.Empty
            };
        }

        [Serializable]
        private sealed class GoogleSignInPayload
        {
            public int success;
            public string callbackUrl;
            public string error;
        }
    }
}