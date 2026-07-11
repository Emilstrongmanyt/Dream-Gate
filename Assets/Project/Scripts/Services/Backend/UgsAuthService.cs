using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    public static class UgsAuthService
    {
        private static bool initializeStarted;
        private static bool initializeFinished;
        private static string initializeError = string.Empty;

        public static bool IsSupported => Application.isEditor || Application.platform == RuntimePlatform.IPhonePlayer;

        public static bool IsSignedIn =>
            initializeFinished
            && string.IsNullOrEmpty(initializeError)
            && AuthenticationService.Instance != null
            && AuthenticationService.Instance.IsSignedIn;

        public static string PlayerId =>
            IsSignedIn ? AuthenticationService.Instance.PlayerId ?? string.Empty : string.Empty;

        public static string AccessToken =>
            IsSignedIn ? AuthenticationService.Instance.AccessToken ?? string.Empty : string.Empty;

        public static IEnumerator CoEnsureInitialized(Action<bool, string> callback = null)
        {
            if (initializeFinished && string.IsNullOrEmpty(initializeError))
            {
                callback?.Invoke(true, string.Empty);
                yield break;
            }

            if (initializeStarted && !initializeFinished)
            {
                var waitDeadline = AuthCoroutineTimeouts.CreateDeadline(30f);
                while (!initializeFinished && !AuthCoroutineTimeouts.HasTimedOut(waitDeadline))
                {
                    yield return null;
                }

                callback?.Invoke(string.IsNullOrEmpty(initializeError), initializeError);
                yield break;
            }

            initializeStarted = true;
            initializeError = string.Empty;

            if (!IsSupported)
            {
                initializeFinished = true;
                initializeError = "Unity Authentication is only available on iOS builds.";
                callback?.Invoke(false, initializeError);
                yield break;
            }

            Exception failure = null;
            yield return RunAsync(
                async () =>
                {
                    if (UnityServices.State != ServicesInitializationState.Initialized)
                    {
                        await UnityServices.InitializeAsync();
                    }

                    if (!AuthenticationService.Instance.IsSignedIn)
                    {
                        AuthenticationService.Instance.SignedIn -= OnSignedIn;
                        AuthenticationService.Instance.SignedOut -= OnSignedOut;
                        AuthenticationService.Instance.SignedIn += OnSignedIn;
                        AuthenticationService.Instance.SignedOut += OnSignedOut;
                    }
                },
                ex => failure = ex);

            initializeFinished = true;
            if (failure != null)
            {
                initializeError = FormatException(failure);
            }

            callback?.Invoke(string.IsNullOrEmpty(initializeError), initializeError);
        }

        public static IEnumerator CoSignUpWithUsernamePassword(
            string username,
            string password,
            Action<bool, string> callback)
        {
            if (!TryValidateUsername(username, out var usernameError))
            {
                callback(false, usernameError);
                yield break;
            }

            if (!TryValidatePassword(password, out var passwordError))
            {
                callback(false, passwordError);
                yield break;
            }

            var success = false;
            var message = string.Empty;
            yield return CoEnsureInitialized((ok, error) =>
            {
                success = ok;
                message = error;
            });

            if (!success)
            {
                callback(false, string.IsNullOrWhiteSpace(message) ? "Unity Authentication is not ready." : message);
                yield break;
            }

            Exception failure = null;
            yield return RunAsync(
                () => AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username.Trim(), password),
                ex => failure = ex);

            if (failure != null)
            {
                callback(false, FormatAuthFailure(failure));
                yield break;
            }

            callback(true, string.Empty);
        }

        public static IEnumerator CoSignInWithUsernamePassword(
            string username,
            string password,
            Action<bool, string> callback)
        {
            if (!TryValidateUsername(username, out var usernameError))
            {
                callback(false, usernameError);
                yield break;
            }

            if (string.IsNullOrEmpty(password))
            {
                callback(false, "Enter your password.");
                yield break;
            }

            var success = false;
            var message = string.Empty;
            yield return CoEnsureInitialized((ok, error) =>
            {
                success = ok;
                message = error;
            });

            if (!success)
            {
                callback(false, string.IsNullOrWhiteSpace(message) ? "Unity Authentication is not ready." : message);
                yield break;
            }

            Exception failure = null;
            yield return RunAsync(
                () => AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username.Trim(), password),
                ex => failure = ex);

            if (failure != null)
            {
                callback(false, FormatAuthFailure(failure));
                yield break;
            }

            callback(true, string.Empty);
        }

        public static IEnumerator CoSignInWithApple(string idToken, Action<bool, string> callback)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                callback(false, "Apple sign in did not return an identity token.");
                yield break;
            }

            var success = false;
            var message = string.Empty;
            yield return CoEnsureInitialized((ok, error) =>
            {
                success = ok;
                message = error;
            });

            if (!success)
            {
                callback(false, string.IsNullOrWhiteSpace(message) ? "Unity Authentication is not ready." : message);
                yield break;
            }

            Exception failure = null;
            yield return RunAsync(
                () => AuthenticationService.Instance.SignInWithAppleAsync(idToken),
                ex => failure = ex);

            if (failure != null)
            {
                callback(false, FormatAuthFailure(failure));
                yield break;
            }

            callback(true, string.Empty);
        }

        public static void SignOut()
        {
            if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
            }
        }

        public static bool TryValidateUsername(string rawUsername, out string error)
        {
            error = string.Empty;
            var username = rawUsername?.Trim() ?? string.Empty;
            if (username.Length < 3 || username.Length > 20)
            {
                error = "Username must be 3-20 characters.";
                return false;
            }

            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9.\-_@]+$"))
            {
                error = "Username can only use letters, numbers, and . - _ @";
                return false;
            }

            return true;
        }

        public static bool TryValidatePassword(string password, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Length > 30)
            {
                error = "Password must be 8-30 characters.";
                return false;
            }

            if (!Regex.IsMatch(password, @"[a-z]"))
            {
                error = "Password needs a lowercase letter.";
                return false;
            }

            if (!Regex.IsMatch(password, @"[A-Z]"))
            {
                error = "Password needs an uppercase letter.";
                return false;
            }

            if (!Regex.IsMatch(password, @"[0-9]"))
            {
                error = "Password needs a number.";
                return false;
            }

            if (!Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
            {
                error = "Password needs a symbol.";
                return false;
            }

            return true;
        }

        private static void OnSignedIn()
        {
        }

        private static void OnSignedOut()
        {
        }

        private static IEnumerator RunAsync(Func<Task> taskFactory, Action<Exception> onFailure)
        {
            var task = taskFactory();
            var deadline = AuthCoroutineTimeouts.CreateDeadline(60f);
            while (!task.IsCompleted && !AuthCoroutineTimeouts.HasTimedOut(deadline))
            {
                yield return null;
            }

            if (!task.IsCompleted)
            {
                onFailure(new TimeoutException("Unity Authentication request timed out."));
                yield break;
            }

            if (task.IsFaulted)
            {
                onFailure(task.Exception?.GetBaseException() ?? new Exception("Unity Authentication request failed."));
            }
        }

        private static string FormatAuthFailure(Exception exception)
        {
            if (exception is AuthenticationException authException)
            {
                if (!string.IsNullOrWhiteSpace(authException.Message))
                {
                    return authException.Message;
                }

                return $"Sign in failed ({authException.ErrorCode}).";
            }

            return FormatException(exception);
        }

        private static string FormatException(Exception exception)
        {
            return string.IsNullOrWhiteSpace(exception?.Message)
                ? "Unity Authentication request failed."
                : exception.Message;
        }
    }
}