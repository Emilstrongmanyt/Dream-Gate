using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace DreamGate.Battlegrounds.Services.Backend
{
    public static class AppleSignInService
    {
        public static bool IsSupported =>
#if UNITY_IOS && !UNITY_EDITOR
            true;
#else
            UnityEngine.Application.platform == UnityEngine.RuntimePlatform.IPhonePlayer;
#endif

        public static IEnumerator RequestSignIn(Action<AppleSignInRequestResult> callback)
        {
            if (!IsSupported)
            {
                callback(AppleSignInRequestResult.Failed("Sign in with Apple is only available on iOS."));
                yield break;
            }

            AppleSignInNative.Warmup();

            var nonce = GenerateNonce();
            var hashedNonce = HashNonce(nonce);
            AppleSignInCredential credential = null;

            AppleSignInNative.RequestAuthorization(hashedNonce, result => credential = result);

            const float timeoutSeconds = 120f;
            var deadline = AuthCoroutineTimeouts.CreateDeadline(timeoutSeconds);
            while (credential == null && !AuthCoroutineTimeouts.HasTimedOut(deadline))
            {
                if (AppleSignInNative.TryConsumePendingResult(out var polled))
                {
                    credential = polled;
                    break;
                }

                yield return null;
            }

            if (credential == null)
            {
                callback(AppleSignInRequestResult.Failed(
                    "Apple sign in timed out before the app received Apple's response. Check Sign in with Apple is enabled for this build."));
                yield break;
            }

            if (!credential.Success || string.IsNullOrWhiteSpace(credential.IdentityToken))
            {
                callback(AppleSignInRequestResult.Failed(credential.Error));
                yield break;
            }

            callback(new AppleSignInRequestResult
            {
                Success = true,
                Nonce = nonce,
                IdentityToken = credential.IdentityToken,
                DisplayName = credential.DisplayName
            });
        }

        private static string GenerateNonce()
        {
            return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        }

        private static string HashNonce(string nonce)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(nonce));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }
    }

    public sealed class AppleSignInRequestResult
    {
        public bool Success;
        public string Nonce = string.Empty;
        public string IdentityToken = string.Empty;
        public string DisplayName = string.Empty;
        public string Error = string.Empty;

        public static AppleSignInRequestResult Failed(string error)
        {
            return new AppleSignInRequestResult
            {
                Success = false,
                Error = string.IsNullOrWhiteSpace(error) ? "Apple sign in failed." : error
            };
        }
    }
}