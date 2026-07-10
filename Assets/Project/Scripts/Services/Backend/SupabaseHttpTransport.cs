using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
        public string Transport = string.Empty;
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

            if (url.Contains("/auth/v1/", StringComparison.Ordinal))
            {
                SupabaseHttpResult authResult = null;

#if UNITY_IOS && !UNITY_EDITOR
                yield return PostViaNative(url, body, headers, value => authResult = value);
                if (IsUsableAuthResult(authResult))
                {
                    callback(authResult);
                    yield break;
                }
#endif

                yield return PostViaHttpClient(url, body, headers, value => authResult = value);
                if (IsUsableAuthResult(authResult))
                {
                    callback(authResult);
                    yield break;
                }

                yield return PostViaUnityWebRequest(url, body, headers, anonKey, value => authResult = value);
                callback(authResult ?? new SupabaseHttpResult
                {
                    Success = false,
                    Error = "All auth HTTP transports failed."
                });
                yield break;
            }

            SupabaseHttpResult result = null;
            yield return PostViaUnityWebRequest(url, body, headers, anonKey, value => result = value);
            callback(result ?? new SupabaseHttpResult
            {
                Success = false,
                Error = "Request failed to complete."
            });
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

            if (url.Contains("/auth/v1/", StringComparison.Ordinal))
            {
                SupabaseHttpResult authResult = null;

#if UNITY_IOS && !UNITY_EDITOR
                yield return GetViaNative(url, headers, value => authResult = value);
                if (IsUsableAuthResult(authResult))
                {
                    callback(authResult);
                    yield break;
                }
#endif

                yield return GetViaHttpClient(url, headers, value => authResult = value);
                if (IsUsableAuthResult(authResult))
                {
                    callback(authResult);
                    yield break;
                }
            }

            using var request = UnityWebRequest.Get(url);
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
                        request.responseCode),
                "unity-webrequest"));
        }

        private static bool IsUsableAuthResult(SupabaseHttpResult result)
        {
            return result != null
                   && (result.Success
                       ? result.BodyBytes > 0
                       : !string.IsNullOrWhiteSpace(result.Error));
        }

        private static IEnumerator PostViaUnityWebRequest(
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers,
            string anonKey,
            Action<SupabaseHttpResult> callback)
        {
            SupabaseHttpResult result = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
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
                            request.responseCode),
                    "unity-webrequest");

                if (!ShouldRetryEmptyAuthBody(url, result) || attempt == 1)
                {
                    break;
                }

                yield return WebRequestHelper.WaitForResponseReady();
            }

            callback(result);
        }

        private static IEnumerator PostViaHttpClient(
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers,
            Action<SupabaseHttpResult> callback)
        {
            var task = SendAuthRequestAsync(HttpMethod.Post, url, body, headers);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    Transport = "httpclient",
                    Error = task.Exception?.GetBaseException()?.Message ?? "HTTP client request failed."
                });
                yield break;
            }

            callback(task.Result);
        }

        private static IEnumerator GetViaHttpClient(
            string url,
            IReadOnlyDictionary<string, string> headers,
            Action<SupabaseHttpResult> callback)
        {
            var task = SendAuthRequestAsync(HttpMethod.Get, url, null, headers);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    Transport = "httpclient",
                    Error = task.Exception?.GetBaseException()?.Message ?? "HTTP client request failed."
                });
                yield break;
            }

            callback(task.Result);
        }

        private static async Task<SupabaseHttpResult> SendAuthRequestAsync(
            HttpMethod method,
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
            using var request = new HttpRequestMessage(method, url);
            if (method == HttpMethod.Post)
            {
                request.Content = new StringContent(body ?? "{}", Encoding.UTF8, "application/json");
            }

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    if (string.IsNullOrWhiteSpace(header.Key) || header.Value == null)
                    {
                        continue;
                    }

                    if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            using var response = await client.SendAsync(request).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false) ?? string.Empty;
            var bodyBytes = Encoding.UTF8.GetByteCount(responseBody);
            var statusCode = (long)response.StatusCode;
            var httpSucceeded = statusCode >= 200 && statusCode < 300;

            return new SupabaseHttpResult
            {
                Success = httpSucceeded,
                StatusCode = statusCode,
                Body = responseBody,
                BodyBytes = bodyBytes,
                Transport = "httpclient",
                Error = httpSucceeded
                    ? string.Empty
                    : ExtractHttpError(responseBody, null, statusCode)
            };
        }

#if UNITY_IOS && !UNITY_EDITOR
        private static IEnumerator PostViaNative(
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers,
            Action<SupabaseHttpResult> callback)
        {
            SupabaseHttpResult result = null;
            yield return RunNativeRequest(
                () => DreamGate_Http_StartPost(
                    url,
                    body ?? string.Empty,
                    GetHeader(headers, "apikey"),
                    GetHeader(headers, "Authorization")),
                value => result = value);

            if (result != null)
            {
                result.Transport = "native-ios";
            }

            callback(result);
        }

        private static IEnumerator GetViaNative(
            string url,
            IReadOnlyDictionary<string, string> headers,
            Action<SupabaseHttpResult> callback)
        {
            SupabaseHttpResult result = null;
            yield return RunNativeRequest(
                () => DreamGate_Http_StartGet(
                    url,
                    GetHeader(headers, "apikey"),
                    GetHeader(headers, "Authorization")),
                value => result = value);

            if (result != null)
            {
                result.Transport = "native-ios";
            }

            callback(result);
        }

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

        private static IEnumerator RunNativeRequest(Action startRequest, Action<SupabaseHttpResult> callback)
        {
            const int BodyBufferSize = 262144;
            const int ErrorBufferSize = 1024;
            const float TimeoutSeconds = 45f;

            try
            {
                DreamGate_Http_Reset();
                startRequest();
            }
            catch (Exception ex)
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    Error = $"Native HTTP unavailable: {ex.Message}"
                });
                yield break;
            }

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
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    StatusCode = statusCode,
                    Body = responseBody,
                    BodyBytes = bodyBytes,
                    Error = transportError
                });
                yield break;
            }

            callback(new SupabaseHttpResult
            {
                Success = httpSucceeded,
                StatusCode = statusCode,
                Body = responseBody,
                BodyBytes = bodyBytes,
                Error = httpSucceeded
                    ? string.Empty
                    : ExtractHttpError(responseBody, null, statusCode)
            });
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

            return headers.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
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
    }
}