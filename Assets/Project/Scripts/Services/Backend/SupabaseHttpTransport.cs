using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DreamGate.Battlegrounds.Services.Backend
{
    internal sealed class SupabaseHttpResult
    {
        public bool Success;
        public long StatusCode;
        public string Body = string.Empty;
        public string Error = string.Empty;
        public int BodyBytes;
    }

    internal static class SupabaseHttpTransport
    {
        public static IEnumerator Post(
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers,
            Action<SupabaseHttpResult> callback)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    Error = "Request URL is missing."
                });
                yield break;
            }

            var anonKey = TryGetAnonKey(headers);
            if (string.IsNullOrWhiteSpace(anonKey) && url.Contains("/auth/v1/", StringComparison.Ordinal))
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    Error = "Supabase anon key is missing from this build."
                });
                yield break;
            }

            var requestUrl = WebRequestHelper.WithApiKeyQuery(url, anonKey);

#if UNITY_IOS && !UNITY_EDITOR
            if (url.Contains("/auth/v1/", StringComparison.Ordinal))
            {
                yield return SupabaseNativeHttp.Post(requestUrl, body, headers, callback);
                yield break;
            }
#endif

            SupabaseHttpResult result = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var request = new UnityWebRequest(requestUrl, UnityWebRequest.kHttpVerbPOST);
                var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
                WebRequestHelper.ConfigureJsonPost(request, bytes);
                request.timeout = 45;
                WebRequestHelper.ApplySupabaseHeaders(request, headers, anonKey);

                yield return request.SendWebRequest();
                yield return WebRequestHelper.WaitForResponseReady();

                result = WebRequestHelper.BuildResult(
                    request,
                    httpSucceeded => httpSucceeded
                        ? string.Empty
                        : ExtractHttpError(
                            WebRequestHelper.ReadResponseText(request),
                            request.error,
                            request.responseCode));

                if (!ShouldRetryEmptyAuthBody(url, result) || attempt == 1)
                {
                    break;
                }

                yield return WebRequestHelper.WaitForResponseReady();
            }

            callback(result ?? new SupabaseHttpResult
            {
                Success = false,
                Error = "Request failed to complete."
            });
        }

        private static string TryGetAnonKey(IReadOnlyDictionary<string, string> headers)
        {
            if (headers == null)
            {
                return string.Empty;
            }

            return headers.TryGetValue("apikey", out var anonKey) ? anonKey : string.Empty;
        }

        private static bool ShouldRetryEmptyAuthBody(string url, SupabaseHttpResult result)
        {
            return url.Contains("/auth/v1/", StringComparison.Ordinal)
                   && result != null
                   && result.StatusCode >= 200
                   && result.StatusCode < 300
                   && result.BodyBytes == 0;
        }

        private static string ExtractHttpError(string body, string transportError, long statusCode)
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

            if (!string.IsNullOrWhiteSpace(transportError))
            {
                return transportError;
            }

            return $"Request failed (HTTP {statusCode}).";
        }

        public static IEnumerator Get(
            string url,
            IReadOnlyDictionary<string, string> headers,
            Action<SupabaseHttpResult> callback)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    Error = "Request URL is missing."
                });
                yield break;
            }

            var anonKey = TryGetAnonKey(headers);
            var requestUrl = WebRequestHelper.WithApiKeyQuery(url, anonKey);

#if UNITY_IOS && !UNITY_EDITOR
            if (url.Contains("/auth/v1/", StringComparison.Ordinal))
            {
                yield return SupabaseNativeHttp.Get(requestUrl, headers, callback);
                yield break;
            }
#endif

            using var request = UnityWebRequest.Get(requestUrl);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 45;
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Accept-Encoding", "identity");
            WebRequestHelper.ApplySupabaseHeaders(request, headers, anonKey);

            yield return request.SendWebRequest();
            yield return WebRequestHelper.WaitForResponseReady();

            callback(WebRequestHelper.BuildResult(
                request,
                httpSucceeded => httpSucceeded
                    ? string.Empty
                    : ExtractHttpError(
                        WebRequestHelper.ReadResponseText(request),
                        request.error,
                        request.responseCode)));
        }
    }
}