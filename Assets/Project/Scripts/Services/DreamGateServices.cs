using System;
using System.Collections;
using System.Collections.Generic;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Services.Backend;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services
{
    /// <summary>
    /// Player services with local fallback and optional Supabase cloud backend.
    /// </summary>
    public static class DreamGateServices
    {
        public static bool IsInitialized { get; private set; }
        public static bool IsLoggedIn => UseCloudBackend ? CloudClient?.IsAuthenticated == true : AuthService.IsLoggedIn;
        public static bool UseCloudBackend { get; private set; }
        public static SupabaseClient CloudClient { get; private set; }
        public static PlayerProfile Profile { get; private set; }

        public static void Initialize()
        {
            var settings = BackendSettings.Load();
            UseCloudBackend = settings != null && settings.IsConfigured;

            if (UseCloudBackend)
            {
                _ = CloudCoroutineHost.Instance;
                AppleSignInNative.Warmup();
                CloudClient ??= new SupabaseClient(settings, CloudCoroutineHost.Instance);
                if (CloudClient.IsAuthenticated)
                {
                    CloudCoroutineHost.Instance.Run(LoadCloudProfileRoutine());
                }
                else
                {
                    Profile = null;
                }
            }
            else
            {
                CloudClient = null;
                AuthService.RestoreSession();
                if (AuthService.IsLoggedIn)
                {
                    ProfileStore.MigrateLegacyProfileIfNeeded(AuthService.SessionPlayerId);
                    Profile = ProfileStore.Load(AuthService.SessionPlayerId);
                    if (AuthService.CurrentAccount != null)
                    {
                        Profile.displayName = AuthService.CurrentAccount.displayName;
                        Profile.email = AuthService.CurrentAccount.email;
                    }
                }
                else
                {
                    Profile = null;
                }
            }

            IsInitialized = true;
        }

        public static bool TryRegister(string displayName, string email, string password, string confirmPassword, out string message)
        {
            message = string.Empty;
            if (UseCloudBackend)
            {
                message = "Cloud registration is in progress. Please wait.";
                CloudCoroutineHost.Instance.Run(CoTryRegister(displayName, email, password, confirmPassword, (_, _, _) => { }));
                return false;
            }

            if (!AuthService.TryRegister(displayName, email, password, confirmPassword, out message))
            {
                return false;
            }

            Profile = ProfileStore.Load(AuthService.SessionPlayerId);
            return true;
        }

        public static bool TryLogin(string email, string password, out string message)
        {
            message = string.Empty;
            if (UseCloudBackend)
            {
                message = "Cloud login is in progress. Please wait.";
                CloudCoroutineHost.Instance.Run(CoTryLogin(email, password, (_, _) => { }));
                return false;
            }

            if (!AuthService.TryLogin(email, password, out message))
            {
                return false;
            }

            Profile = ProfileStore.Load(AuthService.SessionPlayerId);
            return true;
        }

        public static IEnumerator CoTryRegister(
            string displayName,
            string email,
            string password,
            string confirmPassword,
            System.Action<bool, string, bool> callback)
        {
            if (!UseCloudBackend || CloudClient == null)
            {
                var localSuccess = TryRegister(displayName, email, password, confirmPassword, out var localMessage);
                callback(localSuccess, localMessage, false);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length < 2)
            {
                callback(false, "Display name must be at least 2 characters.", false);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                callback(false, "Enter a valid email address.", false);
                yield break;
            }

            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                callback(false, "Password must be at least 6 characters.", false);
                yield break;
            }

            if (password != confirmPassword)
            {
                callback(false, "Passwords do not match.", false);
                yield break;
            }

            var success = false;
            var message = string.Empty;
            var requiresEmailConfirmation = false;
            yield return CloudClient.SignUp(email, password, displayName.Trim(), (ok, msg, pendingConfirmation) =>
            {
                success = ok;
                message = msg;
                requiresEmailConfirmation = pendingConfirmation;
            });

            if (!success)
            {
                if (message.IndexOf("could not start a session", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("already registered", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    yield return CloudClient.SignIn(email, password, (loginOk, loginMsg) =>
                    {
                        if (loginOk || CloudClient.IsAuthenticated)
                        {
                            success = true;
                            message = $"Welcome back, {displayName}!";
                        }
                        else if (!string.IsNullOrWhiteSpace(loginMsg))
                        {
                            message = loginMsg;
                        }
                    });
                }

                if (!success)
                {
                    if (message.IndexOf("Login failed", StringComparison.OrdinalIgnoreCase) >= 0
                        || message.IndexOf("could not start a session", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        message = "Account may have been created. Try logging in from the Log In screen.";
                    }

                    callback(false, message, false);
                    yield break;
                }
            }

            if (requiresEmailConfirmation)
            {
                callback(true, message, true);
                yield break;
            }

            yield return CloudClient.UpdateDisplayName(displayName.Trim(), (_, _) => { });
            yield return WaitForCloudProfileRoutine();
            callback(true, $"Account created. Welcome, {Profile?.displayName ?? displayName}!", false);
        }

        public static IEnumerator CoTryLogin(string email, string password, System.Action<bool, string> callback)
        {
            if (!UseCloudBackend || CloudClient == null)
            {
                var localSuccess = TryLogin(email, password, out var localMessage);
                callback(localSuccess, localMessage);
                yield break;
            }

            var success = false;
            var message = string.Empty;
            yield return CloudClient.SignIn(email, password, (ok, msg) =>
            {
                success = ok;
                message = msg;
            });

            if (!success && !CloudClient.IsAuthenticated)
            {
                callback(false, message);
                yield break;
            }

            yield return LoadCloudProfileRoutine();
            callback(true, $"Welcome back, {Profile?.displayName ?? CloudClient.DisplayNameFromMetadata ?? "Dreamer"}!");
        }

        public static IEnumerator CoTryAppleSignIn(System.Action<bool, string> callback)
        {
            if (!UseCloudBackend || CloudClient == null)
            {
                callback(false, "Cloud sign in is not available.");
                yield break;
            }

            if (!AppleSignInService.IsSupported)
            {
                callback(false, "Sign in with Apple is only available on iOS.");
                yield break;
            }

            AppleSignInRequestResult appleResult = null;
            yield return AppleSignInService.RequestSignIn(result => appleResult = result);
            if (appleResult == null || !appleResult.Success)
            {
                callback(false, appleResult?.Error ?? "Apple sign in failed.");
                yield break;
            }

            var success = false;
            var message = string.Empty;
            yield return CloudClient.SignInWithApple(appleResult.IdentityToken, appleResult.Nonce, (ok, msg) =>
            {
                success = ok;
                message = msg;
            });

            if (!success)
            {
                callback(false, message);
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(appleResult.DisplayName))
            {
                yield return CloudClient.UpdateDisplayName(appleResult.DisplayName.Trim(), (_, _) => { });
            }

            yield return WaitForCloudProfileRoutine();
            callback(true, $"Welcome, {Profile?.displayName ?? appleResult.DisplayName ?? "Dreamer"}!");
        }

        public static IEnumerator CoTryGoogleSignIn(System.Action<bool, string> callback)
        {
            if (!UseCloudBackend || CloudClient == null)
            {
                callback(false, "Cloud sign in is not available.");
                yield break;
            }

            if (!GoogleSignInService.IsSupported)
            {
                callback(false, "Sign in with Google is only available on iOS.");
                yield break;
            }

            var settings = BackendSettings.Load();
            GoogleSignInRequestResult googleResult = null;
            yield return GoogleSignInService.RequestSignIn(settings.supabaseUrl, result => googleResult = result);
            if (googleResult == null || !googleResult.Success)
            {
                callback(false, googleResult?.Error ?? "Google sign in failed.");
                yield break;
            }

            var success = false;
            var message = string.Empty;
            if (!string.IsNullOrWhiteSpace(googleResult.AccessToken))
            {
                yield return CloudClient.SignInWithOAuthTokens(
                    googleResult.AccessToken,
                    googleResult.RefreshToken,
                    (ok, msg) =>
                    {
                        success = ok;
                        message = msg;
                    });
            }
            else
            {
                yield return CloudClient.SignInWithOAuthCode(
                    googleResult.AuthCode,
                    googleResult.CodeVerifier,
                    (ok, msg) =>
                    {
                        success = ok;
                        message = msg;
                    });
            }

            if (!success)
            {
                callback(false, message);
                yield break;
            }

            var displayName = CloudClient.DisplayNameFromMetadata;
            if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(CloudClient.UserEmail))
            {
                displayName = CloudClient.UserEmail.Split('@')[0];
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                yield return CloudClient.UpdateDisplayName(displayName.Trim(), (_, _) => { });
            }

            yield return WaitForCloudProfileRoutine();
            callback(true, $"Welcome, {Profile?.displayName ?? displayName ?? "Dreamer"}!");
        }

        public static void Logout()
        {
            if (UseCloudBackend)
            {
                CloudClient?.SignOut();
            }
            else
            {
                AuthService.Logout();
            }

            Profile = null;
        }

        public static void ApplyRatedResult(MatchResult result)
        {
            if (!IsInitialized || !IsLoggedIn || Profile == null || result == null || result.matchMode != MatchMode.Rated)
            {
                return;
            }

            if (UseCloudBackend)
            {
                CloudCoroutineHost.Instance.Run(CoApplyRatedResult(result));
                return;
            }

            ApplyRatedResultLocal(result);
        }

        private static IEnumerator CoApplyRatedResult(MatchResult result)
        {
            var settings = BackendSettings.Load();
            if (settings == null || string.IsNullOrWhiteSpace(settings.ResolvedApplyMatchResultUrl))
            {
                ApplyRatedResultLocal(result);
                yield break;
            }

            var payload = new Dictionary<string, object>
            {
                { "lobbyId", MatchSessionContext.LobbyId ?? string.Empty },
                { "matchSeed", MatchSessionContext.MatchSeed },
                { "usedBotFill", MatchSessionContext.UsedBotFill },
                { "humanCount", MatchSessionContext.HumanCount },
                { "placement", result.placement },
                { "damageDealt", result.damageDealt },
                { "heroName", result.heroName ?? string.Empty },
                { "turnsPlayed", result.turnsPlayed }
            };

            var success = false;
            var response = string.Empty;
            yield return CloudClient.InvokeFunction(settings.ResolvedApplyMatchResultUrl, payload, (ok, _, raw) =>
            {
                success = ok;
                response = raw;
            });

            if (!success)
            {
                ApplyRatedResultLocal(result);
                yield break;
            }

            result.mmrBefore = ApiJson.TryGetInt(response, "mmrBefore");
            result.mmrAfter = ApiJson.TryGetInt(response, "mmrAfter");
            result.mmrDelta = ApiJson.TryGetInt(response, "mmrDelta");
            Profile = CloudProfileMapper.FromFunctionJson(response, CloudClient.UserId, CloudClient.UserEmail);
        }

        private static void ApplyRatedResultLocal(MatchResult result)
        {
            var mmrBefore = Profile.mmr;
            var delta = MmrCalculator.CalculateDelta(result.placement, mmrBefore);
            Profile.mmr = Mathf.Max(0, mmrBefore + delta);
            Profile.ratedGamesPlayed++;
            var won = result.placement == 1;
            Profile.wins += won ? 1 : 0;
            Profile.losses += result.placement >= 5 ? 1 : 0;
            Profile.top4Finishes += result.placement <= 4 ? 1 : 0;
            Profile.currentWinStreak = won ? Profile.currentWinStreak + 1 : 0;
            Profile.bestWinStreak = Mathf.Max(Profile.bestWinStreak, Profile.currentWinStreak);
            Profile.totalDamageDealt += result.damageDealt;
            ProfileStore.Save(Profile);

            result.mmrBefore = mmrBefore;
            result.mmrAfter = Profile.mmr;
            result.mmrDelta = delta;
        }

        private static IEnumerator LoadCloudProfileRoutine()
        {
            if (CloudClient == null || !CloudClient.IsAuthenticated)
            {
                Profile = null;
                yield break;
            }

            yield return CloudClient.GetProfile((success, _, loadedProfile) =>
            {
                if (success && loadedProfile != null)
                {
                    Profile = loadedProfile;
                    return;
                }

                if (!CloudClient.IsAuthenticated)
                {
                    Profile = null;
                    return;
                }

                Profile = BuildFallbackCloudProfile();
            });
        }

        private static PlayerProfile BuildFallbackCloudProfile()
        {
            var email = CloudClient.UserEmail ?? string.Empty;
            var displayName = CloudClient.DisplayNameFromMetadata;
            if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrEmpty(email))
            {
                displayName = email.Split('@')[0];
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = "Dreamer";
            }

            return new PlayerProfile
            {
                playerId = CloudClient.UserId,
                email = email,
                displayName = displayName,
                mmr = PlayerProfile.DefaultMmr,
                highestMmr = PlayerProfile.DefaultMmr
            };
        }

        private static IEnumerator WaitForCloudProfileRoutine()
        {
            const int maxAttempts = 4;
            for (var attempt = 0; attempt < maxAttempts && Profile == null; attempt++)
            {
                if (attempt > 0)
                {
                    yield return new WaitForSeconds(0.4f);
                }

                yield return LoadCloudProfileRoutine();
            }
        }

        public static string GetStatusLine()
        {
            if (!IsInitialized)
            {
                return "Services not ready";
            }

            if (!IsLoggedIn || Profile == null)
            {
                return "Not signed in";
            }

            return $"Signed in as {Profile.displayName} | MMR {Profile.mmr}";
        }

        public static string GetHomeStatusLine()
        {
            if (!IsInitialized)
            {
                Initialize();
            }

            if (!IsLoggedIn || Profile == null)
            {
                return UseCloudBackend
                    ? "Log in or create an account to play rated matches online."
                    : "Log in or create an account to track your rated MMR.";
            }

            return
                $"Welcome, {Profile.displayName}  •  MMR {Profile.mmr}  •  W {Profile.wins} / L {Profile.losses}  •  Streak {Profile.currentWinStreak}";
        }
    }
}