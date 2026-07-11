using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        internal const string AuthTransportRevision = "v10-emailonly";

        internal static string LastAuthAttemptDetails = string.Empty;

        public static IEnumerator Post(
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers,
            Action<SupabaseHttpResult> callback)
        {
            yield return Post(url, body, headers, "application/json", callback);
        }

        public static IEnumerator Post(
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers,
            string contentType,
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
                var authUrl = PrepareAuthUrl(url, anonKey);
                var attempts = new List<string>();
                SupabaseHttpResult authResult = null;

#if UNITY_IOS && !UNITY_EDITOR
                yield return PostViaNative(url, body, headers, value => authResult = value);
                attempts.Add(DescribeAttempt(authResult, "native-ios"));
                if (IsUsableAuthResult(authResult, url))
                {
                    callback(authResult);
                    yield break;
                }

                LastAuthAttemptDetails = BuildAuthFailureMessage(attempts, AuthTransportRevision);
                callback(BuildFailedAuthChainResult(authResult, attempts));
                yield break;
#else
                yield return PostViaHttpClient(authUrl, body, headers, value => authResult = value);
                attempts.Add(DescribeAttempt(authResult, "httpclient"));
                if (IsUsableAuthResult(authResult, url))
                {
                    callback(authResult);
                    yield break;
                }

                yield return PostViaUnityWebRequest(authUrl, body, headers, anonKey, value => authResult = value);
                attempts.Add(DescribeAttempt(authResult, "unity-webrequest"));
                if (IsUsableAuthResult(authResult, url))
                {
                    callback(authResult);
                    yield break;
                }

                LastAuthAttemptDetails = BuildAuthFailureMessage(attempts, AuthTransportRevision);
                callback(BuildFailedAuthChainResult(authResult, attempts));
                yield break;
#endif
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
                var authUrl = PrepareAuthUrl(url, anonKey);
                SupabaseHttpResult authResult = null;

#if UNITY_IOS && !UNITY_EDITOR
                yield return GetViaNative(authUrl, headers, value => authResult = value);
                if (IsUsableAuthResult(authResult, url))
                {
                    callback(authResult);
                    yield break;
                }
#endif

                yield return GetViaHttpClient(authUrl, headers, value => authResult = value);
                if (IsUsableAuthResult(authResult, url))
                {
                    callback(authResult);
                    yield break;
                }

                url = authUrl;
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

        private static bool IsUsableAuthResult(SupabaseHttpResult result, string url = null)
        {
            if (result == null)
            {
                return false;
            }

            if (IsEmptyBodySuccess(result))
            {
                return false;
            }

            var requiresSession = !string.IsNullOrWhiteSpace(url)
                                  && url.Contains("/auth/v1/token", StringComparison.Ordinal);
            if (requiresSession)
            {
                if (!result.Success)
                {
                    return result.StatusCode > 0
                           && (!string.IsNullOrWhiteSpace(result.Error) || result.BodyBytes > 0);
                }

                return ContainsAuthSession(result.Body) && result.BodyBytes >= 32;
            }

            if (result.Success && result.BodyBytes > 0)
            {
                return true;
            }

            if (result.StatusCode > 0 && (!string.IsNullOrWhiteSpace(result.Error) || result.BodyBytes > 0))
            {
                return true;
            }

            return false;
        }

        private static bool ContainsAuthSession(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            return body.Contains("access_token", StringComparison.Ordinal)
                   || SupabaseAuthParser.Parse(body).HasSession;
        }

        private static bool IsEmptyBodySuccess(SupabaseHttpResult result)
        {
            return result != null
                   && result.Success
                   && result.BodyBytes == 0
                   && result.StatusCode >= 200
                   && result.StatusCode < 300;
        }

        private static IEnumerator PostViaUnityWebRequest(
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers,
            string anonKey,
            Action<SupabaseHttpResult> callback)
        {
            yield return PostViaUnityWebRequest(url, body, headers, anonKey, "application/json", 45, callback);
        }

        private static IEnumerator PostViaUnityWebRequest(
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers,
            string anonKey,
            string contentType,
            int timeoutSeconds,
            Action<SupabaseHttpResult> callback)
        {
            SupabaseHttpResult result = null;
            var payload = body ?? "{}";
            var resolvedContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/json"
                : contentType;
            var retryEmptyBody = timeoutSeconds >= 30
                                 && resolvedContentType.Equals("application/json", StringComparison.OrdinalIgnoreCase);

            for (var attempt = 0; attempt < (retryEmptyBody ? 2 : 1); attempt++)
            {
                using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                var bytes = Encoding.UTF8.GetBytes(payload);
                WebRequestHelper.ConfigurePost(request, bytes, resolvedContentType);
                request.timeout = timeoutSeconds;
                WebRequestHelper.ApplySupabaseHeaders(request, headers, anonKey);

                yield return request.SendWebRequest();
                yield return WebRequestHelper.WaitForResponseReady(request);

                result = WebRequestHelper.BuildResult(
                    request,
                    httpSucceeded => httpSucceeded
                        ? string.Empty
                        : ExtractHttpError(
                            WebRequestHelper.ReadResponseText(request),
                            request.error,
                            request.responseCode),
                    "unity-webrequest");

                if (!retryEmptyBody || !ShouldRetryEmptyAuthBody(url, result) || attempt == 1)
                {
                    break;
                }

                yield return WebRequestHelper.WaitForResponseReady(request);
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
            var deadline = AuthCoroutineTimeouts.CreateDeadline(45f);
            while (!task.IsCompleted && !AuthCoroutineTimeouts.HasTimedOut(deadline))
            {
                yield return null;
            }

            if (!task.IsCompleted)
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    Transport = "httpclient",
                    Error = "HTTP client request timed out."
                });
                yield break;
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
            var deadline = AuthCoroutineTimeouts.CreateDeadline(45f);
            while (!task.IsCompleted && !AuthCoroutineTimeouts.HasTimedOut(deadline))
            {
                yield return null;
            }

            if (!task.IsCompleted)
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    Transport = "httpclient",
                    Error = "HTTP client request timed out."
                });
                yield break;
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
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.None
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };
            using var request = new HttpRequestMessage(method, url);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("Accept-Encoding", "identity");

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

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
            var responseBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false) ?? Array.Empty<byte>();
            var responseBody = responseBytes.Length > 0
                ? Encoding.UTF8.GetString(responseBytes)
                : string.Empty;
            var statusCode = (long)response.StatusCode;
            var httpSucceeded = statusCode >= 200 && statusCode < 300;

            return new SupabaseHttpResult
            {
                Success = httpSucceeded,
                StatusCode = statusCode,
                Body = responseBody,
                BodyBytes = responseBytes.Length,
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
        private static extern int DreamGate_Http_GetBodyByteCount();

        [DllImport("__Internal")]
        private static extern int DreamGate_Http_GetRevision();

        [DllImport("__Internal")]
        private static extern int DreamGate_Http_HasBodyFile();

        [DllImport("__Internal")]
        private static extern int DreamGate_Http_CopyBodyFilePath(byte[] buffer, int bufferSize);

        [DllImport("__Internal")]
        private static extern int DreamGate_Http_CopyBody(byte[] buffer, int bufferSize);

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
            var nativeByteCount = DreamGate_Http_GetBodyByteCount();
            var responseBody = string.Empty;
            var bodyBytes = 0;
            var readDiagnostic = "none";

            if (!TryReadNativeBodyFromFile(out responseBody, out bodyBytes, out readDiagnostic)
                && nativeByteCount > 0)
            {
                if (TryReadNativeBodyPinned(nativeByteCount, BodyBufferSize, out responseBody, out bodyBytes, out var pinnedDiagnostic))
                {
                    readDiagnostic = pinnedDiagnostic;
                }
                else
                {
                    readDiagnostic = $"native={nativeByteCount}, file={readDiagnostic}, pin={pinnedDiagnostic}";
                }
            }

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

            var result = new SupabaseHttpResult
            {
                Success = httpSucceeded,
                StatusCode = statusCode,
                Body = responseBody,
                BodyBytes = bodyBytes,
                Transport = DescribeNativeTransport(),
                Error = httpSucceeded
                    ? string.Empty
                    : ExtractHttpError(responseBody, null, statusCode)
            };

            if (httpSucceeded && (bodyBytes == 0 || bodyBytes < 32))
            {
                result.Success = false;
                result.Error = $"native-ios short body (HTTP {statusCode}, bytes={bodyBytes}, nativeBytes={nativeByteCount}, read={readDiagnostic ?? "none"})";
            }

            callback(result);
        }

        private static bool TryReadNativeBodyFromFile(out string responseBody, out int bodyBytes, out string diagnostic)
        {
            responseBody = string.Empty;
            bodyBytes = 0;
            diagnostic = "no-file";

            if (DreamGate_Http_HasBodyFile() == 0)
            {
                return false;
            }

            var pathBuffer = new byte[1024];
            var pathLength = DreamGate_Http_CopyBodyFilePath(pathBuffer, pathBuffer.Length);
            if (pathLength <= 0)
            {
                diagnostic = "path-copy-failed";
                return false;
            }

            var path = ReadNullTerminatedUtf8(pathBuffer);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                diagnostic = $"missing:{path}";
                return false;
            }

            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length == 0)
                {
                    diagnostic = "file-empty";
                    return false;
                }

                responseBody = Encoding.UTF8.GetString(bytes);
                bodyBytes = bytes.Length;
                diagnostic = $"file:{bodyBytes}";
                return true;
            }
            catch (Exception ex)
            {
                diagnostic = $"file-read:{ex.GetType().Name}";
                return false;
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }

        private static bool TryReadNativeBodyPinned(
            int nativeByteCount,
            int maxBufferSize,
            out string responseBody,
            out int bodyBytes,
            out string diagnostic)
        {
            responseBody = string.Empty;
            bodyBytes = 0;
            diagnostic = "pin-skipped";

            if (nativeByteCount <= 0)
            {
                return false;
            }

            var buffer = new byte[Math.Min(nativeByteCount, maxBufferSize)];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var copiedBytes = DreamGate_Http_CopyBody(buffer, buffer.Length);
                if (copiedBytes <= 0)
                {
                    diagnostic = $"pin-copy-0/native={nativeByteCount}";
                    return false;
                }

                responseBody = Encoding.UTF8.GetString(buffer, 0, copiedBytes);
                bodyBytes = copiedBytes;
                diagnostic = $"pin:{copiedBytes}";
                return true;
            }
            finally
            {
                handle.Free();
            }
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

        private static string DescribeNativeTransport()
        {
            try
            {
                var revision = DreamGate_Http_GetRevision();
                return revision > 0
                    ? $"native-ios-r{revision}"
                    : "native-ios-missing";
            }
            catch (Exception ex)
            {
                return $"native-ios-unavailable ({ex.GetType().Name})";
            }
        }
#endif

        private static SupabaseHttpResult BuildFailedAuthChainResult(
            SupabaseHttpResult lastResult,
            IReadOnlyList<string> attempts)
        {
            return new SupabaseHttpResult
            {
                Success = false,
                StatusCode = lastResult?.StatusCode ?? 0,
                Body = lastResult?.Body ?? string.Empty,
                BodyBytes = lastResult?.BodyBytes ?? 0,
                Transport = "all",
                Error = BuildAuthFailureMessage(attempts, AuthTransportRevision)
            };
        }

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

        private static string PrepareAuthUrl(string url, string anonKey)
        {
            return WebRequestHelper.WithApiKeyQuery(url, anonKey);
        }

        private static string DescribeAttempt(SupabaseHttpResult result, string transport)
        {
            if (result == null)
            {
                return $"{transport}: no result";
            }

            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                return $"{transport}: {result.Error}";
            }

            return $"{transport}: HTTP {result.StatusCode}, {result.BodyBytes} bytes";
        }

        private static string BuildAuthFailureMessage(IReadOnlyList<string> attempts, string transportRevision)
        {
            if (attempts == null || attempts.Count == 0)
            {
                return $"Auth HTTP failed ({transportRevision}).";
            }

            return $"Auth HTTP failed ({transportRevision}). " + string.Join("; ", attempts);
        }

        private static bool ShouldRetryEmptyAuthBody(string url, SupabaseHttpResult result)
        {
            return url.Contains("/auth/v1/", StringComparison.Ordinal)
                   && result != null
                   && result.StatusCode >= 200
                   && result.StatusCode < 300
                   && result.BodyBytes == 0;
        }

        internal static string ExtractAuthError(string body, string transportError, long statusCode)
        {
            return ExtractHttpError(body, transportError, statusCode);
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