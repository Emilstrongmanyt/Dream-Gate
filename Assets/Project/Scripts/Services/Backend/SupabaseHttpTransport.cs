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

            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            WebRequestHelper.ConfigureJsonPost(request, bytes);

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    if (string.IsNullOrWhiteSpace(header.Key) || header.Value == null)
                    {
                        continue;
                    }

                    request.SetRequestHeader(header.Key, header.Value);
                }
            }

            yield return request.SendWebRequest();

            var responseBody = WebRequestHelper.ReadResponseText(request);
            var bodyByteCount = request.downloadHandler?.data?.Length ?? 0;
            var statusCode = request.responseCode;
            var transportSucceeded = request.result == UnityWebRequest.Result.Success;
            var httpSucceeded = statusCode >= 200 && statusCode < 300;

            if (!transportSucceeded && !httpSucceeded)
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    StatusCode = statusCode,
                    Body = responseBody ?? string.Empty,
                    BodyBytes = bodyByteCount,
                    Error = WebRequestHelper.DescribeTransportError(request, responseBody)
                });
                yield break;
            }

            callback(new SupabaseHttpResult
            {
                Success = httpSucceeded,
                StatusCode = statusCode,
                Body = responseBody ?? string.Empty,
                BodyBytes = bodyByteCount,
                Error = httpSucceeded
                    ? string.Empty
                    : ExtractHttpError(responseBody, request.error, statusCode)
            });
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

            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Accept-Encoding", "identity");

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    if (string.IsNullOrWhiteSpace(header.Key) || header.Value == null)
                    {
                        continue;
                    }

                    request.SetRequestHeader(header.Key, header.Value);
                }
            }

            yield return request.SendWebRequest();

            var responseBody = WebRequestHelper.ReadResponseText(request);
            var bodyByteCount = request.downloadHandler?.data?.Length ?? 0;
            var statusCode = request.responseCode;
            var transportSucceeded = request.result == UnityWebRequest.Result.Success;
            var httpSucceeded = statusCode >= 200 && statusCode < 300;

            if (!transportSucceeded && !httpSucceeded)
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    StatusCode = statusCode,
                    Body = responseBody ?? string.Empty,
                    BodyBytes = bodyByteCount,
                    Error = WebRequestHelper.DescribeTransportError(request, responseBody)
                });
                yield break;
            }

            callback(new SupabaseHttpResult
            {
                Success = httpSucceeded,
                StatusCode = statusCode,
                Body = responseBody ?? string.Empty,
                BodyBytes = bodyByteCount,
                Error = httpSucceeded
                    ? string.Empty
                    : ExtractHttpError(responseBody, request.error, statusCode)
            });
        }
    }
}