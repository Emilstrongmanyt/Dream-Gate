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

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

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

            var signupSuccess = false;
            var signupResponse = string.Empty;
            var signupError = string.Empty;
            yield return PostJson($"{settings.EffectiveSupabaseUrl}/auth/v1/signup", body, false, (success, response, error) =>
            {
                signupSuccess = success;
                signupResponse = response;
                signupError = error;
            });

            if (!signupSuccess)
            {
                callback(false, NormalizeAuthError(signupError, signupResponse), false);
                yield break;
            }

            var parsed = ApplyAuthResponse(signupResponse);
            if (IsAuthenticated)
            {
                yield return EnsureUserIdentity();
                callback(true, $"Account created. Welcome, {displayName}!", false);
                yield break;
            }

            if (parsed.LooksLikePendingEmailConfirmation)
            {
                callback(
                    true,
                    "Account created. Check your email to confirm your account, then log in.",
                    true);
                yield break;
            }

            callback(
                false,
                DescribeMissingSession(signupResponse, "Sign up could not start a session. If you already registered, try logging in instead."),
                false);
        }

        public IEnumerator SignIn(string email, string password, Action<bool, string> callback)
        {
            var body = BuildPasswordGrantBody(email, password);
            if (string.IsNullOrWhiteSpace(body) || body.Length < 12 || !body.Contains("email", StringComparison.Ordinal))
            {
                callback(false, "Login request could not be built. Check your email and password.");
                yield break;
            }

            var contentType = PasswordGrantContentType;

            var loginSuccess = false;
            var loginResponse = string.Empty;
            var loginError = string.Empty;
            yield return PostJson(
                $"{settings.EffectiveSupabaseUrl}/auth/v1/token?grant_type=password",
                body,
                contentType,
                false,
                (success, response, error) =>
            {
                loginSuccess = success;
                loginResponse = response;
                loginError = error;
            });

            if (!loginSuccess)
            {
                callback(false, NormalizeAuthError(loginError, loginResponse));
                yield break;
            }

            ApplyAuthResponse(loginResponse);
            if (!IsAuthenticated)
            {
                callback(false, DescribeMissingSession(loginResponse, "Login failed."));
                yield break;
            }

            yield return EnsureUserIdentity();
            callback(true, "Welcome back!");
        }

        public IEnumerator SignInWithApple(string idToken, string nonce, Action<bool, string> callback)
        {
            var body = ApiJson.BuildObject(new Dictionary<string, object>
            {
                { "provider", "apple" },
                { "id_token", idToken },
                { "nonce", nonce }
            });

            var appleSuccess = false;
            var appleResponse = string.Empty;
            var appleError = string.Empty;
            yield return PostJson($"{settings.EffectiveSupabaseUrl}/auth/v1/token?grant_type=id_token", body, false, (success, response, error) =>
            {
                appleSuccess = success;
                appleResponse = response;
                appleError = error;
            });

            if (!appleSuccess)
            {
                callback(false, NormalizeAuthError(appleError, appleResponse));
                yield break;
            }

            ApplyAuthResponse(appleResponse);
            if (!IsAuthenticated)
            {
                callback(false, DescribeMissingSession(appleResponse, "Apple sign in failed."));
                yield break;
            }

            yield return EnsureUserIdentity();
            callback(true, "Welcome!");
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

        public IEnumerator SendPhoneOtp(string phone, string displayName, Action<bool, string> callback)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                callback(false, "Enter your phone number.");
                yield break;
            }

            var body = string.IsNullOrWhiteSpace(displayName)
                ? ApiJson.BuildObject(new Dictionary<string, object>
                {
                    { "phone", phone },
                    { "create_user", true }
                })
                : "{" +
                  $"\"phone\":\"{ApiJson.Escape(phone)}\"," +
                  "\"create_user\":true," +
                  $"\"data\":{{\"display_name\":\"{ApiJson.Escape(displayName.Trim())}\"}}" +
                  "}";
            var otpSuccess = false;
            var otpResponse = string.Empty;
            var otpError = string.Empty;
            yield return PostJson($"{settings.EffectiveSupabaseUrl}/auth/v1/otp", body, false, (success, response, error) =>
            {
                otpSuccess = success;
                otpResponse = response;
                otpError = error;
            });

            if (!otpSuccess)
            {
                callback(false, NormalizeAuthError(otpError, otpResponse));
                yield break;
            }

            callback(true, "Verification code sent.");
        }

        public IEnumerator VerifyPhoneOtp(string phone, string otp, Action<bool, string> callback)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                callback(false, "Enter your phone number.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(otp))
            {
                callback(false, "Enter the verification code.");
                yield break;
            }

            var body = ApiJson.BuildObject(new Dictionary<string, object>
            {
                { "phone", phone },
                { "token", otp },
                { "type", "sms" }
            });

            var verifySuccess = false;
            var verifyResponse = string.Empty;
            var verifyError = string.Empty;
            yield return PostJson($"{settings.EffectiveSupabaseUrl}/auth/v1/verify", body, false, (success, response, error) =>
            {
                verifySuccess = success;
                verifyResponse = response;
                verifyError = error;
            });

            if (!verifySuccess)
            {
                callback(false, NormalizeAuthError(verifyError, verifyResponse));
                yield break;
            }

            ApplyAuthResponse(verifyResponse);
            if (!IsAuthenticated)
            {
                callback(false, DescribeMissingSession(verifyResponse, "Phone verification failed."));
                yield break;
            }

            yield return EnsureUserIdentity();
            callback(true, "Welcome!");
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

            yield return PostJson($"{settings.EffectiveSupabaseUrl}/auth/v1/token?grant_type=pkce", body, false, (success, response, error) =>
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

            if (string.IsNullOrEmpty(UserId))
            {
                yield return EnsureUserIdentity();
            }

            if (string.IsNullOrEmpty(UserId))
            {
                callback(false, "Signed in, but the user id was missing from the auth response.", null);
                yield break;
            }

            var url = $"{settings.EffectiveSupabaseUrl}/rest/v1/player_profiles?id=eq.{UserId}&select=*";
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
            var url = $"{settings.EffectiveSupabaseUrl}/rest/v1/player_profiles?id=eq.{UserId}";
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

            if (string.IsNullOrEmpty(AccessToken))
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

            PlayerPrefs.SetString(SessionAccessTokenKey, AccessToken);
            PlayerPrefs.SetString(SessionRefreshTokenKey, RefreshToken ?? string.Empty);
            PlayerPrefs.SetString(SessionUserIdKey, UserId);
            PlayerPrefs.SetString(SessionUserEmailKey, UserEmail ?? string.Empty);
            PlayerPrefs.Save();
            return parsed;
        }

        private static string DescribeEmptyAuthBody(SupabaseHttpResult result)
        {
            var detail = string.IsNullOrWhiteSpace(result?.Error)
                ? $"via {result?.Transport ?? "unknown"} (HTTP {result?.StatusCode ?? 0}, {result?.BodyBytes ?? 0} bytes)"
                : result.Error;
            if (!string.IsNullOrWhiteSpace(SupabaseHttpTransport.LastAuthAttemptDetails))
            {
                detail = $"{detail}. {SupabaseHttpTransport.LastAuthAttemptDetails}";
            }

            return $"Authentication server returned an empty response {detail}, {SupabaseHttpTransport.AuthTransportRevision}. This is not a firewall issue.";
        }

        private static string DescribeInvalidAuthBody(SupabaseHttpResult result, string response)
        {
            var preview = string.IsNullOrWhiteSpace(response)
                ? $"HTTP {result?.StatusCode ?? 0}, {result?.BodyBytes ?? 0} bytes"
                : response.Length > 120
                    ? response.Substring(0, 120) + "..."
                    : response;
            var detail = string.IsNullOrWhiteSpace(result?.Error) ? preview : $"{result.Error} ({preview})";
            if (!string.IsNullOrWhiteSpace(SupabaseHttpTransport.LastAuthAttemptDetails))
            {
                detail = $"{detail}. {SupabaseHttpTransport.LastAuthAttemptDetails}";
            }

            return $"Authentication response did not include a session token ({SupabaseHttpTransport.AuthTransportRevision}). {detail}";
        }

        private static string DescribeMissingSession(string response, string fallback)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return $"Authentication server returned an empty response (0 bytes, {SupabaseHttpTransport.AuthTransportRevision}). This is not a firewall issue.";
            }

            if (!SupabaseAuthParser.Parse(response).HasSession)
            {
                var preview = response.Length > 120 ? response.Substring(0, 120) + "..." : response;
                return $"Authentication response did not include a session token. Server said: {preview}";
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
            yield return PostJson(url, body, "application/json", useAuth, callback);
        }

        private IEnumerator PostJson(
            string url,
            string body,
            string contentType,
            bool useAuth,
            Action<bool, string, string> callback)
        {
            SupabaseHttpResult result = null;
            yield return SupabaseHttpTransport.Post(
                url,
                body,
                BuildRequestHeaders(useAuth, url),
                contentType,
                value => result = value);

            if (result == null)
            {
                callback(false, "Request failed to start.", string.Empty);
                yield break;
            }

            var response = result.Body ?? string.Empty;
            if (!result.Success)
            {
                callback(false, NormalizeAuthError(result.Error, response), response);
                yield break;
            }

            if (url.Contains("/auth/v1/token", StringComparison.Ordinal)
                && result.Success
                && !SupabaseAuthParser.Parse(response).HasSession)
            {
                callback(false, DescribeInvalidAuthBody(result, response), response);
                yield break;
            }

            if (url.Contains("/auth/v1/", StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(response)
                && !AllowsEmptyAuthBody(url))
            {
                callback(false, DescribeEmptyAuthBody(result), response);
                yield break;
            }

            callback(true, string.Empty, response);
        }

        private IEnumerator EnsureUserIdentity()
        {
            if (string.IsNullOrEmpty(AccessToken))
            {
                yield break;
            }

            UserId ??= ApiJson.TryGetJwtClaim(AccessToken, "sub");
            UserEmail ??= ApiJson.TryGetJwtClaim(AccessToken, "email");
            if (!string.IsNullOrEmpty(UserId))
            {
                yield break;
            }

            var url = $"{settings.EffectiveSupabaseUrl}/auth/v1/user";
            SupabaseHttpResult result = null;
            yield return SupabaseHttpTransport.Get(url, BuildRequestHeaders(true, url), value => result = value);
            if (result == null || !result.Success || string.IsNullOrWhiteSpace(result.Body))
            {
                yield break;
            }

            UserId = ApiJson.TryGetTopLevelString(result.Body, "id");
            UserEmail ??= ApiJson.TryGetTopLevelString(result.Body, "email");
            if (!string.IsNullOrEmpty(UserId))
            {
                PlayerPrefs.SetString(SessionUserIdKey, UserId);
                PlayerPrefs.SetString(SessionUserEmailKey, UserEmail ?? string.Empty);
                PlayerPrefs.Save();
            }
        }

        private Dictionary<string, string> BuildRequestHeaders(bool useAuth, string url)
        {
            var headers = new Dictionary<string, string>();
            var anonKey = settings?.EffectiveAnonKey;
            if (string.IsNullOrEmpty(anonKey))
            {
                return headers;
            }

            headers["apikey"] = anonKey;
            if (useAuth)
            {
                headers["Authorization"] = $"Bearer {AccessToken}";
            }
            else if (url.Contains("/auth/v1/", StringComparison.Ordinal))
            {
                headers["Authorization"] = $"Bearer {anonKey}";
            }

            return headers;
        }

        private IEnumerator Get(string url, Action<bool, string, string> callback)
        {
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("apikey", settings.EffectiveAnonKey);
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
            request.SetRequestHeader("apikey", settings.EffectiveAnonKey);
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

        private static bool AllowsEmptyAuthBody(string url)
        {
            return url.Contains("/auth/v1/otp", StringComparison.Ordinal);
        }

        private static string BuildPasswordGrantBody(string email, string password)
        {
            return ApiJson.BuildObject(new Dictionary<string, object>
            {
                { "email", email },
                { "password", password }
            });
        }

        private static string PasswordGrantContentType => "application/json";

        private static string NormalizeAuthError(string error, string responseJson = null)
        {
            if (string.IsNullOrWhiteSpace(error) && !string.IsNullOrWhiteSpace(responseJson))
            {
                error = ApiJson.TryGetString(responseJson, "msg")
                        ?? ApiJson.TryGetString(responseJson, "error_description")
                        ?? ApiJson.TryGetString(responseJson, "error")
                        ?? ApiJson.TryGetString(responseJson, "message");
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                return "Request failed.";
            }

            if (error.Equals("invalid_request", StringComparison.OrdinalIgnoreCase)
                || error.IndexOf("invalid request", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Login request was not accepted by the server. Install the latest TestFlight build and try again.";
            }

            if (error.IndexOf("bad json", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("406", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Login request format was rejected by the server. Install the latest TestFlight build and try again.";
            }

            if (error.IndexOf("Invalid login credentials", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("invalid_credentials", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Incorrect email or password.";
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

            if (error.IndexOf("No API key found", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Supabase API key was not sent with the request. Reinstall the latest TestFlight build.";
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