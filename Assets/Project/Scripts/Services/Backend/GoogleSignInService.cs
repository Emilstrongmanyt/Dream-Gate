using System;
using System.Collections;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    public static class GoogleSignInService
    {
        public static bool IsSupported =>
#if UNITY_IOS && !UNITY_EDITOR
            true;
#else
            Application.platform == RuntimePlatform.IPhonePlayer;
#endif

        public static string BuildAuthorizeUrl(string supabaseUrl, string codeChallenge)
        {
            var baseUrl = supabaseUrl?.TrimEnd('/') ?? string.Empty;
            var redirect = Uri.EscapeDataString(OAuthConstants.RedirectUrl);
            var challenge = Uri.EscapeDataString(codeChallenge);
            return
                $"{baseUrl}/auth/v1/authorize?provider=google&redirect_to={redirect}&code_challenge={challenge}&code_challenge_method=s256";
        }

        public static IEnumerator RequestSignIn(
            string supabaseUrl,
            Action<GoogleSignInRequestResult> callback)
        {
            if (!IsSupported)
            {
                callback(GoogleSignInRequestResult.Failed("Sign in with Google is only available on iOS."));
                yield break;
            }

            if (string.IsNullOrWhiteSpace(supabaseUrl))
            {
                callback(GoogleSignInRequestResult.Failed("Cloud backend is not configured."));
                yield break;
            }

            var codeVerifier = OAuthPkce.GenerateVerifier();
            var codeChallenge = OAuthPkce.CreateChallenge(codeVerifier);
            var authorizeUrl = BuildAuthorizeUrl(supabaseUrl, codeChallenge);
            GoogleSignInCredential credential = null;

            GoogleSignInNative.RequestAuthorization(authorizeUrl, result => credential = result);

            const float timeoutSeconds = 120f;
            var elapsed = 0f;
            while (credential == null && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (credential == null)
            {
                callback(GoogleSignInRequestResult.Failed("Google sign in timed out. Try again."));
                yield break;
            }

            if (!credential.Success || string.IsNullOrWhiteSpace(credential.CallbackUrl))
            {
                callback(GoogleSignInRequestResult.Failed(credential.Error));
                yield break;
            }

            var parsed = OAuthCallbackParser.Parse(credential.CallbackUrl);
            if (!parsed.Success)
            {
                callback(GoogleSignInRequestResult.Failed(parsed.Error));
                yield break;
            }

            callback(new GoogleSignInRequestResult
            {
                Success = true,
                AccessToken = parsed.AccessToken,
                RefreshToken = parsed.RefreshToken,
                AuthCode = parsed.AuthCode,
                CodeVerifier = codeVerifier
            });
        }
    }

    public sealed class GoogleSignInRequestResult
    {
        public bool Success;
        public string AccessToken = string.Empty;
        public string RefreshToken = string.Empty;
        public string AuthCode = string.Empty;
        public string CodeVerifier = string.Empty;
        public string Error = string.Empty;

        public static GoogleSignInRequestResult Failed(string error)
        {
            return new GoogleSignInRequestResult
            {
                Success = false,
                Error = string.IsNullOrWhiteSpace(error) ? "Google sign in failed." : error
            };
        }
    }
}