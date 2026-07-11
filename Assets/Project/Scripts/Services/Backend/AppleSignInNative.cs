using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    public sealed class AppleSignInNative : MonoBehaviour
    {
        private const string CallbackHostName = "DreamGateAppleSignIn";
        private const int ResultBufferSize = 65536;
        private static AppleSignInNative instance;
        private static Action<AppleSignInCredential> pendingCallback;

        public static void Warmup()
        {
            var host = Instance;
            host.gameObject.SetActive(true);
        }

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

                host.SetActive(true);
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
        private static extern void DreamGate_AppleSignIn_Reset();

        [DllImport("__Internal")]
        private static extern void DreamGate_AppleSignIn_Request(
            string hashedNonce,
            string callbackObject,
            string callbackMethod);

        [DllImport("__Internal")]
        private static extern int DreamGate_AppleSignIn_IsReady();

        [DllImport("__Internal")]
        private static extern void DreamGate_AppleSignIn_CopyResult(byte[] buffer, int bufferSize);
#endif

        public static void RequestAuthorization(string hashedNonce, Action<AppleSignInCredential> callback)
        {
            pendingCallback = callback;
#if UNITY_IOS && !UNITY_EDITOR
            Warmup();
            try
            {
                DreamGate_AppleSignIn_Reset();
            }
            catch
            {
                // Native bridge unavailable in editor/player without plugin.
            }

            DreamGate_AppleSignIn_Request(hashedNonce, CallbackHostName, nameof(OnNativeCallback));
#else
            InvokeFallback("Sign in with Apple is only available on iOS devices.");
#endif
        }

        public static bool TryConsumePendingResult(out AppleSignInCredential credential)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (DreamGate_AppleSignIn_IsReady() == 0)
            {
                credential = null;
                return false;
            }

            var json = ReadNativeResult(clearPending: true);
            credential = AppleSignInCredential.FromJson(json);
            return true;
#else
            credential = null;
            return false;
#endif
        }

        public void OnNativeCallback(string signal)
        {
            if (TryConsumePendingResult(out var credential))
            {
                DeliverCredential(credential);
                return;
            }

            if (!string.IsNullOrWhiteSpace(signal)
                && signal.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DeliverCredential(AppleSignInCredential.FromJson(signal));
            }
        }

        private static void DeliverCredential(AppleSignInCredential credential)
        {
            if (credential == null)
            {
                return;
            }

            var callback = pendingCallback;
            pendingCallback = null;
            callback?.Invoke(credential);
        }

        private static string ReadNativeResult(bool clearPending)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!clearPending && DreamGate_AppleSignIn_IsReady() == 0)
            {
                return string.Empty;
            }

            var buffer = new byte[ResultBufferSize];
            DreamGate_AppleSignIn_CopyResult(buffer, buffer.Length);
            var length = Array.IndexOf(buffer, (byte)0);
            if (length < 0)
            {
                length = buffer.Length;
            }

            return length > 0 ? Encoding.UTF8.GetString(buffer, 0, length) : string.Empty;
#else
            return string.Empty;
#endif
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
            if (payload == null || payload.success == 0)
            {
                return FromManualJson(json);
            }

            if (string.IsNullOrWhiteSpace(payload.identityToken))
            {
                return Failed(string.IsNullOrWhiteSpace(payload.error) ? "Apple did not return an identity token." : payload.error);
            }

            return new AppleSignInCredential
            {
                Success = true,
                IdentityToken = payload.identityToken,
                Email = payload.email ?? string.Empty,
                GivenName = payload.givenName ?? string.Empty,
                FamilyName = payload.familyName ?? string.Empty
            };
        }

        private static AppleSignInCredential FromManualJson(string json)
        {
            var success = ApiJson.TryGetInt(json, "success", 0) == 1;
            var identityToken = ApiJson.TryGetString(json, "identityToken") ?? string.Empty;
            if (!success || string.IsNullOrWhiteSpace(identityToken))
            {
                var error = ApiJson.TryGetString(json, "error");
                return Failed(string.IsNullOrWhiteSpace(error) ? "Apple sign in failed." : error);
            }

            return new AppleSignInCredential
            {
                Success = true,
                IdentityToken = identityToken,
                Email = ApiJson.TryGetString(json, "email") ?? string.Empty,
                GivenName = ApiJson.TryGetString(json, "givenName") ?? string.Empty,
                FamilyName = ApiJson.TryGetString(json, "familyName") ?? string.Empty
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

    internal sealed class SupabaseAuthNative : MonoBehaviour
    {
        private const string CallbackHostName = "DreamGateSupabaseAuth";
        private static SupabaseAuthNative instance;
        private static SupabaseHttpResult pendingResult;

        public static void Warmup()
        {
            var host = Instance;
            host.gameObject.SetActive(true);
        }

        public static SupabaseAuthNative Instance
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

                host.SetActive(true);
                instance = host.GetComponent<SupabaseAuthNative>();
                if (instance == null)
                {
                    instance = host.AddComponent<SupabaseAuthNative>();
                }

                return instance;
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DreamGate_AuthHttp_StartPost(
            string url,
            string body,
            string apikey,
            string authorization,
            string contentType,
            string callbackObject,
            string callbackMethod);
#endif

        public static IEnumerator Post(
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers,
            string contentType,
            float timeoutSeconds,
            Action<SupabaseHttpResult> callback)
        {
#if UNITY_IOS && !UNITY_EDITOR
            Warmup();
            pendingResult = null;

            var apikey = GetHeader(headers, "apikey");
            var authorization = GetHeader(headers, "Authorization");
            var resolvedContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/json"
                : contentType;
            DreamGate_AuthHttp_StartPost(
                url,
                body ?? string.Empty,
                apikey,
                authorization,
                resolvedContentType,
                CallbackHostName,
                nameof(OnAuthHttpResult));

            var deadline = AuthCoroutineTimeouts.CreateDeadline(timeoutSeconds);
            while (pendingResult == null && !AuthCoroutineTimeouts.HasTimedOut(deadline))
            {
                yield return null;
            }

            if (pendingResult == null)
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    Transport = "native-message",
                    Error = "Native auth HTTP timed out before Unity received the response."
                });
                yield break;
            }

            var result = pendingResult;
            pendingResult = null;
            result.Transport = "native-message";
            callback(result);
            yield break;
#else
            callback(new SupabaseHttpResult
            {
                Success = false,
                Transport = "native-message",
                Error = "Native auth HTTP is only available on iOS devices."
            });
            yield break;
#endif
        }

        public void OnAuthHttpResult(string json)
        {
            pendingResult = ParseNativePayload(json);
        }

        private static SupabaseHttpResult ParseNativePayload(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new SupabaseHttpResult
                {
                    Success = false,
                    Error = "Native auth HTTP returned an empty callback payload."
                };
            }

            var ok = ApiJson.TryGetInt(json, "ok", 0) == 1;
            var status = ApiJson.TryGetInt(json, "status", 0);
            var error = ApiJson.TryGetString(json, "error") ?? string.Empty;
            var body = DecodeBody(json);

            if (!ok)
            {
                return new SupabaseHttpResult
                {
                    Success = false,
                    StatusCode = status,
                    Body = body,
                    BodyBytes = string.IsNullOrEmpty(body) ? 0 : Encoding.UTF8.GetByteCount(body),
                    Error = string.IsNullOrWhiteSpace(error) ? "Native auth HTTP failed." : error
                };
            }

            var bodyBytes = string.IsNullOrEmpty(body) ? 0 : Encoding.UTF8.GetByteCount(body);
            var httpSucceeded = status >= 200 && status < 300;
            var resolvedError = httpSucceeded
                ? string.Empty
                : SupabaseHttpTransport.ExtractAuthError(body, error, status);
            return new SupabaseHttpResult
            {
                Success = httpSucceeded,
                StatusCode = status,
                Body = body,
                BodyBytes = bodyBytes,
                Error = resolvedError
            };
        }

        private static string DecodeBody(string json)
        {
            var bodyPath = ApiJson.TryGetString(json, "bodyPath");
            if (!string.IsNullOrWhiteSpace(bodyPath))
            {
                try
                {
                    if (File.Exists(bodyPath))
                    {
                        var bytes = File.ReadAllBytes(bodyPath);
                        return bytes.Length > 0 ? Encoding.UTF8.GetString(bytes) : string.Empty;
                    }
                }
                catch
                {
                    // Fall through to legacy inline body fields.
                }
                finally
                {
                    TryDeleteBodyFile(bodyPath);
                }
            }

            var bodyB64 = ApiJson.TryGetString(json, "bodyB64");
            if (!string.IsNullOrWhiteSpace(bodyB64))
            {
                try
                {
                    var bytes = Convert.FromBase64String(bodyB64);
                    return bytes.Length > 0 ? Encoding.UTF8.GetString(bytes) : string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }

            return ApiJson.TryGetString(json, "body") ?? string.Empty;
        }

        private static void TryDeleteBodyFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }

        private static string GetHeader(IReadOnlyDictionary<string, string> headers, string key)
        {
            if (headers == null)
            {
                return string.Empty;
            }

            return headers.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}