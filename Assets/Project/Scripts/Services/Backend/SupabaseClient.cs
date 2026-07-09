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
                    callback(false, NormalizeAuthError(error), false);
                    return;
                }

                ApplyAuthResponse(response);
                if (IsAuthenticated)
                {
                    callback(true, $"Account created. Welcome, {displayName}!", false);
                    return;
                }

                if (HasSignupUserPendingConfirmation(response))
                {
                    callback(
                        true,
                        "Account created. Check your email to confirm your account, then log in.",
                        true);
                    return;
                }

                callback(false, "Sign up failed.", false);
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
                    callback(false, NormalizeAuthError(error));
                    return;
                }

                ApplyAuthResponse(response);
                callback(IsAuthenticated, IsAuthenticated ? "Welcome back!" : "Login failed.");
            });
        }

        public void SignOut()
        {
            AccessToken = null;
            RefreshToken = null;
            UserId = null;
            UserEmail = null;
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

            if (string.IsNullOrEmpty(AccessToken) || string.IsNullOrEmpty(UserId))
            {
                SignOut();
            }
        }

        private void ApplyAuthResponse(string response)
        {
            AccessToken = ApiJson.TryGetString(response, "access_token");
            RefreshToken = ApiJson.TryGetString(response, "refresh_token");

            var userJson = ExtractNestedObject(response, "user");
            UserId = ApiJson.TryGetString(userJson, "id");
            UserEmail = ApiJson.TryGetString(userJson, "email");

            if (!IsAuthenticated)
            {
                return;
            }

            PlayerPrefs.SetString(SessionAccessTokenKey, AccessToken);
            PlayerPrefs.SetString(SessionRefreshTokenKey, RefreshToken ?? string.Empty);
            PlayerPrefs.SetString(SessionUserIdKey, UserId);
            PlayerPrefs.SetString(SessionUserEmailKey, UserEmail ?? string.Empty);
            PlayerPrefs.Save();
        }

        private IEnumerator PostJson(string url, string body, bool useAuth, Action<bool, string, string> callback)
        {
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            var bytes = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(settings?.supabaseAnonKey))
            {
                request.SetRequestHeader("apikey", settings.supabaseAnonKey);
            }

            if (useAuth)
            {
                request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
            }

            yield return request.SendWebRequest();

            var response = request.downloadHandler?.text ?? string.Empty;
            if (request.result != UnityWebRequest.Result.Success)
            {
                var message = ApiJson.TryGetString(response, "msg")
                              ?? ApiJson.TryGetString(response, "error_description")
                              ?? ApiJson.TryGetString(response, "error")
                              ?? request.error
                              ?? "Request failed.";
                callback(false, message, response);
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

            var response = request.downloadHandler?.text ?? string.Empty;
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
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", settings.supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
            request.SetRequestHeader("Prefer", "return=minimal");
            yield return request.SendWebRequest();

            var response = request.downloadHandler?.text ?? string.Empty;
            if (request.result != UnityWebRequest.Result.Success)
            {
                callback(false, request.error ?? "Request failed.", response);
                yield break;
            }

            callback(true, string.Empty, response);
        }

        private static bool HasSignupUserPendingConfirmation(string response)
        {
            if (!string.IsNullOrEmpty(ApiJson.TryGetString(response, "access_token")))
            {
                return false;
            }

            var userJson = ExtractNestedObject(response, "user");
            return !string.IsNullOrEmpty(ApiJson.TryGetString(userJson, "id"));
        }

        private static string NormalizeAuthError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return "Request failed.";
            }

            if (error.IndexOf("confirm", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Please confirm your email using the link we sent, then log in again.";
            }

            return error;
        }

        private static string ExtractNestedObject(string json, string key)
        {
            var pattern = $"\"{key}\":";
            var index = json.IndexOf(pattern, StringComparison.Ordinal);
            if (index < 0)
            {
                return string.Empty;
            }

            index = json.IndexOf('{', index);
            if (index < 0)
            {
                return string.Empty;
            }

            var depth = 0;
            for (var i = index; i < json.Length; i++)
            {
                if (json[i] == '{')
                {
                    depth++;
                }
                else if (json[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return json.Substring(index, i - index + 1);
                    }
                }
            }

            return string.Empty;
        }

        private const string SessionAccessTokenKey = "dreamgate.cloud.access_token";
        private const string SessionRefreshTokenKey = "dreamgate.cloud.refresh_token";
        private const string SessionUserIdKey = "dreamgate.cloud.user_id";
        private const string SessionUserEmailKey = "dreamgate.cloud.user_email";
    }
}