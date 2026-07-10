using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DreamGate.Battlegrounds.Services.Backend
{
    public class SupabaseClient
    {
        private readonly BackendSettings settings;
        private readonly MonoBehaviour coroutineHost;

        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }
        public string UserId { get; private set; }
        public string UserEmail { get; private set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(UserId);

        public string DisplayNameFromMetadata { get; private set; }

        public SupabaseClient(BackendSettings settings, MonoBehaviour coroutineHost)
        {
            this.settings = settings;
            this.coroutineHost = coroutineHost;
            RestoreSession();
        }

        public IEnumerator SignUp(string email, string password, string displayName, Action<bool, string, bool> callback)
        {
            var body =
                "{" +
                $"\"email\":\"{ApiJson.Escape(email)}\"," +
                $"\"password\":\"{ApiJson.Escape(password)}\"," +
                $"\"data\":{{\"display_name\":\"{ApiJson.Escape(displayName)}\"}}" +
                "}";

            yield return PostJson($"{settings.supabaseUrl}/auth/v1/signup", body, false, (success, response, error) =>
            {
                if (!success)
                {
                    callback(false, NormalizeAuthError(error, response), false);
                    return;
                }

                var parsed = ApplyAuthResponse(response);
                if (IsAuthenticated)
                {
                    callback(true, $"Account created. Welcome, {displayName}!", false);
                    return;
                }

                if (parsed.LooksLikePendingEmailConfirmation)
                {
                    callback(
                        true,
                        "Account created. Check your email to confirm your account, then log in.",
                        true);
                    return;
                }

                callback(
                    false,
                    DescribeMissingSession(response, "Sign up could not start a session. If you already registered, try logging in instead."),
                    false);
            });
        }

        public IEnumerator SignIn(string email, string password, Action<bool, string> callback)
        {
            var body = ApiJson.BuildObject(new Dictionary<string, object>
            {
                { "email", email },
                { "password", password }
            });

            yield return PostJson($"{settings.supabaseUrl}/auth/v1/token?grant_type=password", body, false, (success, response, error) =>
            {
                if (!success)
                {
                    callback(false, NormalizeAuthError(error, response));
                    return;
                }

                ApplyAuthResponse(response);
                var authenticated = IsAuthenticated;
                callback(authenticated, authenticated ? "Welcome back!" : DescribeMissingSession(response, "Login failed."));
            });
        }

        public IEnumerator SignInWithApple(string idToken, string nonce, Action<bool, string> callback)
        {
            var body = ApiJson.BuildObject(new Dictionary<string, object>
            {
                { "provider", "apple" },
                { "id_token", idToken },
                { "nonce", nonce }
            });

            yield return PostJson($"{settings.supabaseUrl}/auth/v1/token?grant_type=id_token", body, false, (success, response, error) =>
            {
                if (!success)
                {
                    callback(false, NormalizeAuthError(error, response));
                    return;
                }

                ApplyAuthResponse(response);
                var authenticated = IsAuthenticated;
                callback(authenticated, authenticated ? "Welcome!" : DescribeMissingSession(response, "Apple sign in failed."));
            });
        }

        public IEnumerator SignInWithOAuthTokens(string accessToken, string refreshToken, Action<bool, string> callback)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                callback(false, "Google sign in did not return an access token.");
                yield break;
            }

            var response = ApiJson.BuildObject(new Dictionary<string, object>
            {
                { "access_token", accessToken },
                { "refresh_token", refreshToken ?? string.Empty }
            });

            ApplyAuthResponse(response);
            var authenticated = IsAuthenticated;
            callback(authenticated, authenticated ? "Welcome!" : "Google sign in failed.");
            yield break;
        }

        public IEnumerator SignInWithOAuthCode(string authCode, string codeVerifier, Action<bool, string> callback)
        {
            if (string.IsNullOrWhiteSpace(authCode))
            {
                callback(false, "Google sign in did not return an authorization code.");
                yield break;
            }

            var body = ApiJson.BuildObject(new Dictionary<string, object>
            {
                { "auth_code", authCode },
                { "code_verifier", codeVerifier ?? string.Empty }
            });

            yield return PostJson($"{settings.supabaseUrl}/auth/v1/token?grant_type=pkce", body, false, (success, response, error) =>
            {
                if (!success)
                {
                    callback(false, NormalizeAuthError(error, response));
                    return;
                }

                ApplyAuthResponse(response);
                var authenticated = IsAuthenticated;
                callback(authenticated, authenticated ? "Welcome!" : DescribeMissingSession(response, "Google sign in failed."));
            });
        }

        public void SignOut()
        {
            AccessToken = null;
            RefreshToken = null;
            UserId = null;
            UserEmail = null;
            DisplayNameFromMetadata = null;
            PlayerPrefs.DeleteKey(SessionAccessTokenKey);
            PlayerPrefs.DeleteKey(SessionRefreshTokenKey);
            PlayerPrefs.DeleteKey(SessionUserIdKey);
            PlayerPrefs.DeleteKey(SessionUserEmailKey);
            PlayerPrefs.Save();
        }

        public IEnumerator GetProfile(Action<bool, string, PlayerProfile> callback)
        {
            if (!IsAuthenticated)
            {
                callback(false, "Not signed in.", null);
                yield break;
            }

            var url = $"{settings.supabaseUrl}/rest/v1/player_profiles?id=eq.{UserId}&select=*";
            yield return Get(url, (success, response, error) =>
            {
                if (!success)
                {
                    callback(false, error, null);
                    return;
                }

                var chunks = ApiJson.ExtractObjectChunks(response);
                if (chunks.Count == 0)
                {
                    callback(false, "Profile not found.", null);
                    return;
                }

                callback(true, string.Empty, CloudProfileMapper.FromRestJson(chunks[0], UserId, UserEmail));
            });
        }

        public IEnumerator UpdateDisplayName(string displayName, Action<bool, string> callback)
        {
            if (!IsAuthenticated)
            {
                callback(false, "Not signed in.");
                yield break;
            }

            var body = ApiJson.BuildObject(new Dictionary<string, object> { { "display_name", displayName } });
            var url = $"{settings.supabaseUrl}/rest/v1/player_profiles?id=eq.{UserId}";
            yield return Patch(url, body, (success, _, error) => callback(success, success ? string.Empty : error));
        }

        public IEnumerator InvokeFunction(string functionUrl, Dictionary<string, object> payload, Action<bool, string, string> callback)
        {
            if (!IsAuthenticated)
            {
                callback(false, "Not signed in.", null);
                yield break;
            }

            var body = ApiJson.BuildObject(payload);
            yield return PostJson(functionUrl, body, true, callback);
        }

        private void RestoreSession()
        {
            AccessToken = PlayerPrefs.GetString(SessionAccessTokenKey, string.Empty);
            RefreshToken = PlayerPrefs.GetString(SessionRefreshTokenKey, string.Empty);
            UserId = PlayerPrefs.GetString(SessionUserIdKey, string.Empty);
            UserEmail = PlayerPrefs.GetString(SessionUserEmailKey, string.Empty);

            if (!string.IsNullOrEmpty(AccessToken))
            {
                UserId = string.IsNullOrEmpty(UserId) ? ApiJson.TryGetJwtClaim(AccessToken, "sub") : UserId;
                UserEmail = string.IsNullOrEmpty(UserEmail) ? ApiJson.TryGetJwtClaim(AccessToken, "email") : UserEmail;
            }

            if (string.IsNullOrEmpty(AccessToken) || string.IsNullOrEmpty(UserId))
            {
                SignOut();
            }
        }

        private SupabaseAuthParser.ParsedAuthResponse ApplyAuthResponse(string response)
        {
            var parsed = SupabaseAuthParser.Parse(response);
            AccessToken = parsed.AccessToken;
            RefreshToken = parsed.RefreshToken;
            UserId = parsed.UserId;
            UserEmail = parsed.UserEmail;
            DisplayNameFromMetadata = ReadDisplayNameFromResponse(response);

            if (string.IsNullOrEmpty(AccessToken))
            {
                RefreshToken = null;
                UserId = null;
                UserEmail = null;
                DisplayNameFromMetadata = null;
                return parsed;
            }

            UserId ??= ApiJson.TryGetJwtClaim(AccessToken, "sub");
            UserEmail ??= ApiJson.TryGetJwtClaim(AccessToken, "email");

            if (string.IsNullOrEmpty(UserId))
            {
                Debug.LogWarning("Supabase auth response contained an access token but no user id.");
                return parsed;
            }

            PlayerPrefs.SetString(SessionAccessTokenKey, AccessToken);
            PlayerPrefs.SetString(SessionRefreshTokenKey, RefreshToken ?? string.Empty);
            PlayerPrefs.SetString(SessionUserIdKey, UserId);
            PlayerPrefs.SetString(SessionUserEmailKey, UserEmail ?? string.Empty);
            PlayerPrefs.Save();
            return parsed;
        }

        private static string DescribeMissingSession(string response, string fallback)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return "Authentication server returned an empty response. Check your connection and try again.";
            }

            var errorCode = ApiJson.TryGetString(response, "error_code");
            if (errorCode == "email_not_confirmed")
            {
                return "Please confirm your email using the link we sent, then log in again.";
            }

            return fallback;
        }

        private static string ReadDisplayNameFromResponse(string response)
        {
            var userJson = ApiJson.ExtractNestedObject(response, "user");
            if (string.IsNullOrEmpty(userJson))
            {
                userJson = response;
            }

            var metadataJson = ApiJson.ExtractNestedObject(userJson, "user_metadata");
            return ApiJson.TryGetString(metadataJson, "display_name");
        }

        private IEnumerator PostJson(string url, string body, bool useAuth, Action<bool, string, string> callback)
        {
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            var bytes = Encoding.UTF8.GetBytes(body);
            WebRequestHelper.ConfigureJsonPost(request, bytes);
            if (!string.IsNullOrEmpty(settings?.supabaseAnonKey))
            {
                request.SetRequestHeader("apikey", settings.supabaseAnonKey);
                if (useAuth)
                {
                    request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
                }
                else if (url.Contains("/auth/v1/", StringComparison.Ordinal))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {settings.supabaseAnonKey}");
                }
            }

            yield return request.SendWebRequest();

            var response = WebRequestHelper.ReadResponseText(request);
            if (request.result != UnityWebRequest.Result.Success)
            {
                var message = NormalizeAuthError(
                    ApiJson.TryGetString(response, "msg")
                    ?? ApiJson.TryGetString(response, "error_description")
                    ?? ApiJson.TryGetString(response, "error")
                    ?? request.error
                    ?? $"Request failed (HTTP {request.responseCode}).",
                    response);
                callback(false, message, response);
                yield break;
            }

            if (url.Contains("/auth/v1/", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(response))
            {
                callback(
                    false,
                    $"Authentication server returned an empty response (HTTP {request.responseCode}). Check your connection and try again.",
                    response);
                yield break;
            }

            callback(true, string.Empty, response);
        }

        private IEnumerator Get(string url, Action<bool, string, string> callback)
        {
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("apikey", settings.supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
            yield return request.SendWebRequest();

            var response = WebRequestHelper.ReadResponseText(request);
            if (request.result != UnityWebRequest.Result.Success)
            {
                callback(false, request.error ?? "Request failed.", response);
                yield break;
            }

            callback(true, string.Empty, response);
        }

        private IEnumerator Patch(string url, string body, Action<bool, string, string> callback)
        {
            using var request = new UnityWebRequest(url, "PATCH");
            var bytes = Encoding.UTF8.GetBytes(body);
            WebRequestHelper.ConfigureJsonPost(request, bytes);
            request.SetRequestHeader("apikey", settings.supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
            request.SetRequestHeader("Prefer", "return=minimal");
            yield return request.SendWebRequest();

            var response = WebRequestHelper.ReadResponseText(request);
            if (request.result != UnityWebRequest.Result.Success)
            {
                callback(false, request.error ?? "Request failed.", response);
                yield break;
            }

            callback(true, string.Empty, response);
        }

        private static string NormalizeAuthError(string error, string responseJson = null)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return "Request failed.";
            }

            var errorCode = ApiJson.TryGetString(responseJson, "error_code");
            if (errorCode == "user_already_exists"
                || error.IndexOf("already registered", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "An account with this email already exists. Try logging in instead.";
            }

            if (errorCode == "weak_password")
            {
                return "Password is too weak. Use at least 6 characters.";
            }

            if (error.IndexOf("confirm", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Please confirm your email using the link we sent, then log in again.";
            }

            return error;
        }

        private const string SessionAccessTokenKey = "dreamgate.cloud.access_token";
        private const string SessionRefreshTokenKey = "dreamgate.cloud.refresh_token";
        private const string SessionUserIdKey = "dreamgate.cloud.user_id";
        private const string SessionUserEmailKey = "dreamgate.cloud.user_email";
    }
}