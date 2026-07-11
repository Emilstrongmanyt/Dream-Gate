using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    public sealed class SupabaseAuthNative : MonoBehaviour
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
            string callbackObject,
            string callbackMethod);
#endif

        public static IEnumerator Post(
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers,
            Action<SupabaseHttpResult> callback)
        {
#if UNITY_IOS && !UNITY_EDITOR
            Warmup();
            pendingResult = null;

            var apikey = GetHeader(headers, "apikey");
            var authorization = GetHeader(headers, "Authorization");
            DreamGate_AuthHttp_StartPost(
                url,
                body ?? string.Empty,
                apikey,
                authorization,
                CallbackHostName,
                nameof(OnAuthHttpResult));

            const float timeoutSeconds = 45f;
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
            return new SupabaseHttpResult
            {
                Success = httpSucceeded,
                StatusCode = status,
                Body = body,
                BodyBytes = bodyBytes,
                Error = httpSucceeded ? string.Empty : error
            };
        }

        private static string DecodeBody(string json)
        {
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