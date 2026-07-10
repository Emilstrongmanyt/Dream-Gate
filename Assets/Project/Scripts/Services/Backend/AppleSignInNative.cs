using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    public sealed class AppleSignInNative : MonoBehaviour
    {
        private const string CallbackHostName = "DreamGateAppleSignIn";
        private static AppleSignInNative instance;
        private static Action<AppleSignInCredential> pendingCallback;

        public static AppleSignInNative Instance
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

                instance = host.GetComponent<AppleSignInNative>();
                if (instance == null)
                {
                    instance = host.AddComponent<AppleSignInNative>();
                }

                return instance;
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DreamGate_AppleSignIn_Request(
            string hashedNonce,
            string callbackObject,
            string callbackMethod);
#endif

        public static void RequestAuthorization(string hashedNonce, Action<AppleSignInCredential> callback)
        {
            pendingCallback = callback;
#if UNITY_IOS && !UNITY_EDITOR
            DreamGate_AppleSignIn_Request(hashedNonce, Instance.gameObject.name, nameof(OnNativeCallback));
#else
            InvokeFallback("Sign in with Apple is only available on iOS devices.");
#endif
        }

        public void OnNativeCallback(string json)
        {
            var credential = AppleSignInCredential.FromJson(json);
            var callback = pendingCallback;
            pendingCallback = null;
            callback?.Invoke(credential);
        }

        private static void InvokeFallback(string error)
        {
            var callback = pendingCallback;
            pendingCallback = null;
            callback?.Invoke(AppleSignInCredential.Failed(error));
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }

    public sealed class AppleSignInCredential
    {
        public bool Success;
        public string IdentityToken = string.Empty;
        public string Email = string.Empty;
        public string GivenName = string.Empty;
        public string FamilyName = string.Empty;
        public string Error = string.Empty;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(GivenName) && !string.IsNullOrWhiteSpace(FamilyName))
                {
                    return $"{GivenName.Trim()} {FamilyName.Trim()}";
                }

                if (!string.IsNullOrWhiteSpace(GivenName))
                {
                    return GivenName.Trim();
                }

                if (!string.IsNullOrWhiteSpace(FamilyName))
                {
                    return FamilyName.Trim();
                }

                if (!string.IsNullOrWhiteSpace(Email) && Email.Contains("@"))
                {
                    return Email.Split('@')[0];
                }

                return string.Empty;
            }
        }

        public static AppleSignInCredential Failed(string error)
        {
            return new AppleSignInCredential
            {
                Success = false,
                Error = string.IsNullOrWhiteSpace(error) ? "Apple sign in failed." : error
            };
        }

        public static AppleSignInCredential FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Failed("Apple sign in returned an empty response.");
            }

            var payload = JsonUtility.FromJson<AppleSignInPayload>(json);
            if (payload == null)
            {
                return Failed("Apple sign in returned an invalid response.");
            }

            if (payload.success == 0)
            {
                return Failed(payload.error);
            }

            return new AppleSignInCredential
            {
                Success = true,
                IdentityToken = payload.identityToken ?? string.Empty,
                Email = payload.email ?? string.Empty,
                GivenName = payload.givenName ?? string.Empty,
                FamilyName = payload.familyName ?? string.Empty
            };
        }

        [Serializable]
        private sealed class AppleSignInPayload
        {
            public int success;
            public string identityToken;
            public string email;
            public string givenName;
            public string familyName;
            public string error;
        }
    }
}