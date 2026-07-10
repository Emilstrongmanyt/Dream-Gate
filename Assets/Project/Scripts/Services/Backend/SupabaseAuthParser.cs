using System;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    internal static class SupabaseAuthParser
    {
        [Serializable]
        private sealed class TokenResponse
        {
            public string access_token;
            public string refresh_token;
            public string token_type;
            public int expires_in;
            public SupabaseUser user;
        }

        [Serializable]
        private sealed class SignupSessionResponse
        {
            public SupabaseUser user;
            public TokenResponse session;
        }

        [Serializable]
        private sealed class SupabaseUser
        {
            public string id;
            public string email;
        }

        internal sealed class ParsedAuthResponse
        {
            public string AccessToken;
            public string RefreshToken;
            public string UserId;
            public string UserEmail;

            public bool HasSession => !string.IsNullOrEmpty(AccessToken);

            public bool LooksLikePendingEmailConfirmation =>
                !HasSession &&
                !string.IsNullOrEmpty(UserId) &&
                !string.IsNullOrEmpty(UserEmail);
        }

        internal static ParsedAuthResponse Parse(string rawJson)
        {
            var result = new ParsedAuthResponse();
            var json = ApiJson.NormalizeResponseJson(rawJson);
            if (string.IsNullOrEmpty(json))
            {
                return result;
            }

            TryParseWithJsonUtility(json, result);
            ApplyManualFallback(json, result);
            ApplyJwtFallback(result);
            return result;
        }

        private static void TryParseWithJsonUtility(string json, ParsedAuthResponse result)
        {
            try
            {
                var signup = JsonUtility.FromJson<SignupSessionResponse>(json);
                if (signup?.session != null && !string.IsNullOrEmpty(signup.session.access_token))
                {
                    ApplyTokenResponse(result, signup.session);
                    if (signup.user != null)
                    {
                        result.UserId ??= signup.user.id;
                        result.UserEmail ??= signup.user.email;
                    }
                }
            }
            catch (Exception)
            {
            }

            if (result.HasSession)
            {
                return;
            }

            try
            {
                var token = JsonUtility.FromJson<TokenResponse>(json);
                if (token != null && !string.IsNullOrEmpty(token.access_token))
                {
                    ApplyTokenResponse(result, token);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void ApplyManualFallback(string json, ParsedAuthResponse result)
        {
            if (!result.HasSession)
            {
                var sessionJson = ApiJson.ExtractNestedObject(json, "session");
                var tokenSource = !string.IsNullOrEmpty(sessionJson) ? sessionJson : json;
                result.AccessToken = ApiJson.TryGetString(tokenSource, "access_token")
                                     ?? ApiJson.TryGetString(json, "access_token");
                result.RefreshToken = ApiJson.TryGetString(tokenSource, "refresh_token")
                                      ?? ApiJson.TryGetString(json, "refresh_token");
            }

            if (string.IsNullOrEmpty(result.UserId) || string.IsNullOrEmpty(result.UserEmail))
            {
                var userJson = ApiJson.ExtractNestedObject(json, "user");
                if (string.IsNullOrEmpty(userJson))
                {
                    var sessionJson = ApiJson.ExtractNestedObject(json, "session");
                    userJson = ApiJson.ExtractNestedObject(sessionJson, "user");
                }

                result.UserId ??= ApiJson.TryGetString(userJson, "id")
                                  ?? ApiJson.TryGetString(json, "id");
                result.UserEmail ??= ApiJson.TryGetString(userJson, "email")
                                     ?? ApiJson.TryGetString(json, "email");
            }
        }

        private static void ApplyJwtFallback(ParsedAuthResponse result)
        {
            if (string.IsNullOrEmpty(result.AccessToken))
            {
                return;
            }

            result.UserId ??= ApiJson.TryGetJwtClaim(result.AccessToken, "sub");
            result.UserEmail ??= ApiJson.TryGetJwtClaim(result.AccessToken, "email");
        }

        private static void ApplyTokenResponse(ParsedAuthResponse result, TokenResponse token)
        {
            result.AccessToken = token.access_token;
            result.RefreshToken = token.refresh_token;
            result.UserId ??= token.user?.id;
            result.UserEmail ??= token.user?.email;
        }
    }
}