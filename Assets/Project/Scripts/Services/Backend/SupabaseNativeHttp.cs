using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    internal static class SupabaseNativeHttp
    {
        private const int BodyBufferSize = 262144;
        private const int ErrorBufferSize = 1024;
        private const float TimeoutSeconds = 45f;

        public static bool IsSupported =>
#if UNITY_IOS && !UNITY_EDITOR
            true;
#else
            false;
#endif

        public static IEnumerator Post(
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers,
            Action<SupabaseHttpResult> callback)
        {
#if UNITY_IOS && !UNITY_EDITOR
            yield return RunRequest(
                () => DreamGate_Http_StartPost(
                    url,
                    body ?? string.Empty,
                    GetHeader(headers, "apikey"),
                    GetHeader(headers, "Authorization")),
                callback);
#else
            callback(new SupabaseHttpResult
            {
                Success = false,
                Error = "Native HTTP is only available on iOS devices."
            });
            yield break;
#endif
        }

        public static IEnumerator Get(
            string url,
            IReadOnlyDictionary<string, string> headers,
            Action<SupabaseHttpResult> callback)
        {
#if UNITY_IOS && !UNITY_EDITOR
            yield return RunRequest(
                () => DreamGate_Http_StartGet(
                    url,
                    GetHeader(headers, "apikey"),
                    GetHeader(headers, "Authorization")),
                callback);
#else
            callback(new SupabaseHttpResult
            {
                Success = false,
                Error = "Native HTTP is only available on iOS devices."
            });
            yield break;
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DreamGate_Http_Reset();

        [DllImport("__Internal")]
        private static extern void DreamGate_Http_StartPost(
            string url,
            string body,
            string apikey,
            string authorization);

        [DllImport("__Internal")]
        private static extern void DreamGate_Http_StartGet(
            string url,
            string apikey,
            string authorization);

        [DllImport("__Internal")]
        private static extern int DreamGate_Http_IsDone();

        [DllImport("__Internal")]
        private static extern int DreamGate_Http_GetStatusCode();

        [DllImport("__Internal")]
        private static extern int DreamGate_Http_GetBodySize();

        [DllImport("__Internal")]
        private static extern void DreamGate_Http_CopyBody(byte[] buffer, int bufferSize);

        [DllImport("__Internal")]
        private static extern void DreamGate_Http_CopyError(byte[] buffer, int bufferSize);

        private static IEnumerator RunRequest(Action startRequest, Action<SupabaseHttpResult> callback)
        {
            DreamGate_Http_Reset();
            startRequest();

            var deadline = AuthCoroutineTimeouts.CreateDeadline(TimeoutSeconds);
            while (DreamGate_Http_IsDone() == 0 && !AuthCoroutineTimeouts.HasTimedOut(deadline))
            {
                yield return null;
            }

            if (DreamGate_Http_IsDone() == 0)
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    Error = "Native HTTP request timed out."
                });
                yield break;
            }

            callback(ReadResult());
        }

        private static SupabaseHttpResult ReadResult()
        {
            var statusCode = DreamGate_Http_GetStatusCode();
            var bodyBytes = DreamGate_Http_GetBodySize();
            var bodyBuffer = new byte[Math.Max(BodyBufferSize, bodyBytes + 1)];
            DreamGate_Http_CopyBody(bodyBuffer, bodyBuffer.Length);

            var responseBody = bodyBytes > 0
                ? Encoding.UTF8.GetString(bodyBuffer, 0, bodyBytes)
                : string.Empty;

            var errorBuffer = new byte[ErrorBufferSize];
            DreamGate_Http_CopyError(errorBuffer, errorBuffer.Length);
            var transportError = ReadNullTerminatedUtf8(errorBuffer);
            var httpSucceeded = statusCode >= 200 && statusCode < 300;

            if (!string.IsNullOrWhiteSpace(transportError))
            {
                return new SupabaseHttpResult
                {
                    Success = false,
                    StatusCode = statusCode,
                    Body = responseBody,
                    BodyBytes = bodyBytes,
                    Error = transportError
                };
            }

            return new SupabaseHttpResult
            {
                Success = httpSucceeded,
                StatusCode = statusCode,
                Body = responseBody,
                BodyBytes = bodyBytes,
                Error = httpSucceeded
                    ? string.Empty
                    : ExtractHttpError(responseBody, statusCode)
            };
        }

        private static string ReadNullTerminatedUtf8(byte[] buffer)
        {
            var length = Array.IndexOf(buffer, (byte)0);
            if (length < 0)
            {
                length = buffer.Length;
            }

            return length > 0 ? Encoding.UTF8.GetString(buffer, 0, length) : string.Empty;
        }
#endif

        private static string GetHeader(IReadOnlyDictionary<string, string> headers, string key)
        {
            if (headers == null)
            {
                return string.Empty;
            }

            foreach (var header in headers)
            {
                if (header.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return header.Value ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static string ExtractHttpError(string body, long statusCode)
        {
            var parsed = ApiJson.TryGetString(body, "msg")
                         ?? ApiJson.TryGetString(body, "error_description")
                         ?? ApiJson.TryGetString(body, "error")
                         ?? ApiJson.TryGetString(body, "message");
            if (!string.IsNullOrWhiteSpace(parsed))
            {
                return parsed;
            }

            if (!string.IsNullOrWhiteSpace(body))
            {
                return body;
            }

            return $"Request failed (HTTP {statusCode}).";
        }
    }
}