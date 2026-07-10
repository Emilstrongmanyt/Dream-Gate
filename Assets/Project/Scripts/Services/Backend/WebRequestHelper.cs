using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DreamGate.Battlegrounds.Services.Backend
{
    internal static class WebRequestHelper
    {
        public static string WithApiKeyQuery(string url, string anonKey)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(anonKey))
            {
                return url;
            }

            if (url.IndexOf("apikey=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return url;
            }

            var separator = url.Contains("?") ? "&" : "?";
            return $"{url}{separator}apikey={Uri.EscapeDataString(anonKey.Trim())}";
        }

        public static void ApplySupabaseHeaders(
            UnityWebRequest request,
            IReadOnlyDictionary<string, string> headers,
            string anonKey)
        {
            if (!string.IsNullOrWhiteSpace(anonKey))
            {
                request.SetRequestHeader("apikey", anonKey.Trim());
            }

            if (headers == null)
            {
                return;
            }

            foreach (var header in headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key) || header.Value == null)
                {
                    continue;
                }

                if (header.Key.Equals("apikey", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                request.SetRequestHeader(header.Key, header.Value);
            }
        }

        public static string ReadResponseText(UnityWebRequest request)
        {
            var handler = request?.downloadHandler;
            if (handler == null)
            {
                return string.Empty;
            }

            var data = handler.data;
            if (data != null && data.Length > 0)
            {
                return Encoding.UTF8.GetString(data);
            }

            return handler.text ?? string.Empty;
        }

        public static void ConfigureJsonPost(UnityWebRequest request, byte[] body, bool disposeHandlers = true)
        {
            request.uploadHandler = new UploadHandlerRaw(body)
            {
                contentType = "application/json"
            };
            request.downloadHandler = new DownloadHandlerBuffer();
            request.disposeUploadHandlerOnDispose = disposeHandlers;
            request.disposeDownloadHandlerOnDispose = disposeHandlers;
            request.useHttpContinue = false;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Accept-Encoding", "identity");
            request.SetRequestHeader("Content-Length", (body?.Length ?? 0).ToString());
        }

        public static IEnumerator WaitForResponseReady()
        {
#if UNITY_IOS && !UNITY_EDITOR
            yield return null;
            yield return null;
#else
            yield return null;
#endif
        }

        public static SupabaseHttpResult BuildResult(UnityWebRequest request, System.Func<bool, string> buildError)
        {
            var responseBody = ReadResponseText(request);
            var bodyByteCount = request.downloadHandler?.data?.Length ?? 0;
            var statusCode = request.responseCode;
            var transportSucceeded = request.result == UnityWebRequest.Result.Success;
            var httpSucceeded = statusCode >= 200 && statusCode < 300;

            if (!transportSucceeded && !httpSucceeded)
            {
                return new SupabaseHttpResult
                {
                    Success = false,
                    StatusCode = statusCode,
                    Body = responseBody ?? string.Empty,
                    BodyBytes = bodyByteCount,
                    Error = DescribeTransportError(request, responseBody)
                };
            }

            return new SupabaseHttpResult
            {
                Success = httpSucceeded,
                StatusCode = statusCode,
                Body = responseBody ?? string.Empty,
                BodyBytes = bodyByteCount,
                Error = buildError(httpSucceeded)
            };
        }

        public static string DescribeTransportError(UnityWebRequest request, string responseBody)
        {
            var parsed = ApiJson.TryGetString(responseBody, "msg")
                         ?? ApiJson.TryGetString(responseBody, "error_description")
                         ?? ApiJson.TryGetString(responseBody, "error")
                         ?? ApiJson.TryGetString(responseBody, "message");
            if (!string.IsNullOrWhiteSpace(parsed))
            {
                return parsed;
            }

            if (!string.IsNullOrWhiteSpace(request?.error))
            {
                return request.error;
            }

            return $"Request failed (HTTP {request?.responseCode ?? 0}).";
        }
    }

    internal static class AuthCoroutineTimeouts
    {
        public static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        public static bool HasTimedOut(float deadlineRealtime)
        {
            return Time.realtimeSinceStartup >= deadlineRealtime;
        }

        public static float CreateDeadline(float timeoutSeconds)
        {
            return Time.realtimeSinceStartup + timeoutSeconds;
        }
    }
}