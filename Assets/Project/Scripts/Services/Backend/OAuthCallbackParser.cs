using System;
using System.Collections.Generic;

namespace DreamGate.Battlegrounds.Services.Backend
{
    public sealed class OAuthCallbackResult
    {
        public bool Success;
        public string AccessToken = string.Empty;
        public string RefreshToken = string.Empty;
        public string AuthCode = string.Empty;
        public string Error = string.Empty;

        public static OAuthCallbackResult Failed(string error)
        {
            return new OAuthCallbackResult
            {
                Success = false,
                Error = string.IsNullOrWhiteSpace(error) ? "Google sign in failed." : error
            };
        }
    }

    public static class OAuthCallbackParser
    {
        public static OAuthCallbackResult Parse(string callbackUrl)
        {
            if (string.IsNullOrWhiteSpace(callbackUrl))
            {
                return OAuthCallbackResult.Failed("Google sign in returned an empty response.");
            }

            if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri))
            {
                return OAuthCallbackResult.Failed("Google sign in returned an invalid response.");
            }

            var values = ParseParameters(uri);
            var error = GetValue(values, "error");
            if (!string.IsNullOrWhiteSpace(error))
            {
                var description = GetValue(values, "error_description");
                return OAuthCallbackResult.Failed(
                    string.IsNullOrWhiteSpace(description) ? error : description);
            }

            var accessToken = GetValue(values, "access_token");
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                return new OAuthCallbackResult
                {
                    Success = true,
                    AccessToken = accessToken,
                    RefreshToken = GetValue(values, "refresh_token") ?? string.Empty
                };
            }

            var authCode = GetValue(values, "code");
            if (!string.IsNullOrWhiteSpace(authCode))
            {
                return new OAuthCallbackResult
                {
                    Success = true,
                    AuthCode = authCode
                };
            }

            return OAuthCallbackResult.Failed("Google sign in did not return an authorization code.");
        }

        private static Dictionary<string, string> ParseParameters(Uri uri)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ParseQuery(uri.Query, values);

            if (!string.IsNullOrEmpty(uri.Fragment))
            {
                var fragment = uri.Fragment.StartsWith("#", StringComparison.Ordinal)
                    ? uri.Fragment.Substring(1)
                    : uri.Fragment;
                ParseQuery(fragment, values);
            }

            return values;
        }

        private static void ParseQuery(string query, Dictionary<string, string> values)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            var trimmed = query.StartsWith("?", StringComparison.Ordinal) ? query.Substring(1) : query;
            var pairs = trimmed.Split('&');
            foreach (var pair in pairs)
            {
                if (string.IsNullOrWhiteSpace(pair))
                {
                    continue;
                }

                var separator = pair.IndexOf('=');
                if (separator < 0)
                {
                    values[Uri.UnescapeDataString(pair)] = string.Empty;
                    continue;
                }

                var key = Uri.UnescapeDataString(pair.Substring(0, separator));
                var value = Uri.UnescapeDataString(pair.Substring(separator + 1));
                values[key] = value;
            }
        }

        private static string GetValue(Dictionary<string, string> values, string key)
        {
            return values.TryGetValue(key, out var value) ? value : null;
        }
    }
}