using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    internal sealed class SupabaseHttpResult
    {
        public bool Success;
        public long StatusCode;
        public string Body = string.Empty;
        public string Error = string.Empty;
    }

    internal static class SupabaseHttpTransport
    {
        private static readonly HttpClient SharedClient = CreateClient();

        public static IEnumerator Post(
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers,
            Action<SupabaseHttpResult> callback)
        {
            Task<SupabaseHttpResult> task = null;
            try
            {
                task = PostAsync(url, body, headers);
            }
            catch (Exception ex)
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    Error = ex.Message
                });
                yield break;
            }

            while (task != null && !task.IsCompleted)
            {
                yield return null;
            }

            if (task == null)
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    Error = "Request failed to start."
                });
                yield break;
            }

            if (task.IsFaulted)
            {
                callback(new SupabaseHttpResult
                {
                    Success = false,
                    Error = task.Exception?.GetBaseException().Message ?? "Request failed."
                });
                yield break;
            }

            callback(task.Result);
        }

        private static async Task<SupabaseHttpResult> PostAsync(
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json")
            };

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

            using var response = await SharedClient.SendAsync(request).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return new SupabaseHttpResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (long)response.StatusCode,
                Body = responseBody ?? string.Empty,
                Error = response.IsSuccessStatusCode
                    ? string.Empty
                    : ExtractHttpError(responseBody, response.ReasonPhrase, (long)response.StatusCode)
            };
        }

        private static HttpClient CreateClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
                    | System.Net.DecompressionMethods.Deflate
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
            return client;
        }

        private static string ExtractHttpError(string body, string reasonPhrase, long statusCode)
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

            return string.IsNullOrWhiteSpace(reasonPhrase)
                ? $"Request failed (HTTP {statusCode})."
                : $"{reasonPhrase} (HTTP {statusCode}).";
        }
    }
}