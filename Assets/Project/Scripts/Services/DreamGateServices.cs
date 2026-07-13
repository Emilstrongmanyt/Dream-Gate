using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Heroes;
using DreamGate.Battlegrounds.Services.Backend;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services
{
    /// <summary>
    /// Player services with local fallback and optional Supabase cloud backend.
    /// </summary>
    public static class DreamGateServices
    {
        public static bool IsInitialized { get; private set; }
        public static bool PendingRatedLobbyAfterLogin { get; set; }
        public static event Action ProfileChanged;
        public static bool IsLoggedIn => UseCloudBackend ? CloudClient?.IsAuthenticated == true : AuthService.IsLoggedIn;

        private const string CachedDisplayNameKey = "dreamgate_cached_display_name";
        public static bool UseCloudBackend { get; private set; }
        public static SupabaseClient CloudClient { get; private set; }
        public static PlayerProfile Profile { get; private set; }

        public static void Initialize()
        {
            var settings = BackendSettings.Load();
            UseCloudBackend = settings != null && settings.IsConfigured;

#if UNITY_IOS && !UNITY_EDITOR
            if (UseCloudBackend)
            {
                SupabaseAuthNative.Warmup();
            }
#endif

            if (UseCloudBackend)
            {
                _ = CloudCoroutineHost.Instance;
                CloudClient ??= new SupabaseClient(settings, CloudCoroutineHost.Instance);
                if (CloudClient.IsAuthenticated)
                {
                    CloudCoroutineHost.Instance.Run(CoLoadAndRepairCloudProfile());
                }
                else
                {
                    Profile = null;
                    CloudCoroutineHost.Instance.Run(CoRestoreBridgedCloudSession());
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

            HeroCollectionService.EnsureStarterCollection();
            IsInitialized = true;
        }

        public static void SetGuestProfile(PlayerProfile profile)
        {
            Profile = profile;
            HeroCollectionService.EnsureStarterCollection();
            NotifyProfileChanged();
        }

        public static void NotifyProfileChangedPublic() => NotifyProfileChanged();

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
            string username,
            string password,
            string confirmPassword,
            System.Action<bool, string, bool> callback)
        {
            if (!UseCloudBackend || CloudClient == null)
            {
                var localSuccess = TryRegister(displayName, username, password, confirmPassword, out var localMessage);
                callback(localSuccess, localMessage, false);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length < 2)
            {
                callback(false, "Display name must be at least 2 characters.", false);
                yield break;
            }

            if (!UgsAuthService.TryValidateUsername(username, out var usernameError))
            {
                callback(false, usernameError, false);
                yield break;
            }

            if (!UgsAuthService.TryValidatePassword(password, out var passwordError))
            {
                callback(false, passwordError, false);
                yield break;
            }

            if (password != confirmPassword)
            {
                callback(false, "Passwords do not match.", false);
                yield break;
            }

            var success = false;
            var message = string.Empty;
            yield return UgsAuthService.CoSignUpWithUsernamePassword(username, password, (ok, msg) =>
            {
                success = ok;
                message = msg;
            });

            if (!success)
            {
                if (message.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("exists", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    yield return UgsAuthService.CoSignInWithUsernamePassword(username, password, (loginOk, loginMsg) =>
                    {
                        if (loginOk)
                        {
                            success = true;
                            message = string.Empty;
                        }
                        else if (!string.IsNullOrWhiteSpace(loginMsg))
                        {
                            message = loginMsg;
                        }
                    });
                }

                if (!success)
                {
                    callback(false, string.IsNullOrWhiteSpace(message) ? "Could not create account." : message, false);
                    yield break;
                }
            }

            var bridgedDisplayName = displayName.Trim();
            yield return CoBridgeUgsToSupabase(bridgedDisplayName, (bridgeOk, bridgeMessage) =>
            {
                success = bridgeOk;
                message = bridgeMessage;
            });

            if (!success)
            {
                callback(false, string.IsNullOrWhiteSpace(message) ? "Account created, but cloud sync failed." : message, false);
                yield break;
            }

            CacheDisplayName(bridgedDisplayName);
            yield return CloudClient.UpdateDisplayName(bridgedDisplayName, (_, _) => { });
            yield return WaitForCloudProfileRoutine();
            callback(true, $"Account created. Welcome, {Profile?.displayName ?? displayName}!", false);
        }

        public static IEnumerator CoTryLogin(string username, string password, System.Action<bool, string> callback)
        {
            if (!UseCloudBackend || CloudClient == null)
            {
                var localSuccess = TryLogin(username, password, out var localMessage);
                callback(localSuccess, localMessage);
                yield break;
            }

            if (!UgsAuthService.TryValidateUsername(username, out var usernameError))
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
            yield return UgsAuthService.CoSignInWithUsernamePassword(username, password, (ok, msg) =>
            {
                success = ok;
                message = msg;
            });

            if (!success)
            {
                callback(false, string.IsNullOrWhiteSpace(message) ? "Login failed." : message);
                yield break;
            }

            var bridgedDisplayName = string.Empty;
            yield return CoResolveUgsDisplayName(username.Trim(), resolved => bridgedDisplayName = resolved);
            yield return CoBridgeUgsToSupabase(bridgedDisplayName, (bridgeOk, bridgeMessage) =>
            {
                success = bridgeOk;
                message = bridgeMessage;
            });

            if (!success)
            {
                callback(false, string.IsNullOrWhiteSpace(message) ? "Signed in, but cloud sync failed." : message);
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(bridgedDisplayName))
            {
                CacheDisplayName(bridgedDisplayName);
            }

            yield return CoLoadAndRepairCloudProfile();
            callback(true, $"Welcome back, {Profile?.displayName ?? bridgedDisplayName ?? CloudClient.DisplayNameFromMetadata ?? "Dreamer"}!");
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
            yield return UgsAuthService.CoSignInWithApple(appleResult.IdentityToken, (ok, msg) =>
            {
                success = ok;
                message = msg;
            });

            if (!success)
            {
                callback(false, message);
                yield break;
            }

            var displayName = appleResult.DisplayName;
            yield return CoBridgeUgsToSupabase(displayName, (bridgeOk, bridgeMessage) =>
            {
                success = bridgeOk;
                message = bridgeMessage;
            });

            if (!success)
            {
                callback(false, string.IsNullOrWhiteSpace(message) ? "Apple sign in succeeded, but cloud sync failed." : message);
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                yield return CloudClient.UpdateDisplayName(displayName.Trim(), (_, _) => { });
            }

            yield return WaitForCloudProfileRoutine();
            callback(true, $"Welcome, {Profile?.displayName ?? displayName ?? "Dreamer"}!");
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

        public static IEnumerator CoTryPhoneSendOtp(string rawPhone, string displayName, Action<bool, string> callback)
        {
            if (!UseCloudBackend || CloudClient == null)
            {
                callback(false, "Cloud sign in is not available.");
                yield break;
            }

            if (!PhoneAuthService.TryNormalize(rawPhone, out var phone, out var normalizeError))
            {
                callback(false, normalizeError);
                yield break;
            }

            var success = false;
            var message = string.Empty;
            yield return CloudClient.SendPhoneOtp(phone, displayName, (ok, msg) =>
            {
                success = ok;
                message = msg;
            });

            callback(success, success ? message : (string.IsNullOrWhiteSpace(message) ? "Could not send verification code." : message));
        }

        public static IEnumerator CoTryPhoneVerifyOtp(
            string rawPhone,
            string rawOtp,
            string displayName,
            Action<bool, string> callback)
        {
            if (!UseCloudBackend || CloudClient == null)
            {
                callback(false, "Cloud sign in is not available.");
                yield break;
            }

            if (!PhoneAuthService.TryNormalize(rawPhone, out var phone, out var normalizeError))
            {
                callback(false, normalizeError);
                yield break;
            }

            if (!PhoneAuthService.IsValidOtp(rawOtp, out var otp, out var otpError))
            {
                callback(false, otpError);
                yield break;
            }

            var success = false;
            var message = string.Empty;
            yield return CloudClient.VerifyPhoneOtp(phone, otp, (ok, msg) =>
            {
                success = ok;
                message = msg;
            });

            if (!success)
            {
                callback(false, string.IsNullOrWhiteSpace(message) ? "Verification failed." : message);
                yield break;
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
                UgsAuthService.SignOut();
                CloudClient?.SignOut();
            }
            else
            {
                AuthService.Logout();
            }

            Profile = null;
            PlayerPrefs.DeleteKey(CachedDisplayNameKey);
            PlayerPrefs.Save();
        }

        private static IEnumerator CoRestoreBridgedCloudSession()
        {
            yield return UgsAuthService.CoEnsureInitialized();
            if (!UgsAuthService.IsSignedIn || CloudClient == null)
            {
                yield break;
            }

            if (CloudClient.IsAuthenticated)
            {
                yield return CoLoadAndRepairCloudProfile();
                yield break;
            }

            var bridgedDisplayName = string.Empty;
            yield return CoResolveUgsDisplayName(string.Empty, resolved => bridgedDisplayName = resolved);

            var bridged = false;
            yield return CoBridgeUgsToSupabase(bridgedDisplayName, (ok, _) => bridged = ok);
            if (bridged)
            {
                if (!string.IsNullOrWhiteSpace(bridgedDisplayName))
                {
                    CacheDisplayName(bridgedDisplayName);
                }

                yield return CoLoadAndRepairCloudProfile();
            }
        }

        private static IEnumerator CoBridgeUgsToSupabase(string displayName, System.Action<bool, string> callback)
        {
            if (CloudClient == null)
            {
                callback(false, "Cloud backend is not available.");
                yield break;
            }

            if (!UgsAuthService.IsSignedIn)
            {
                callback(false, "Unity sign in is not active.");
                yield break;
            }

            var success = false;
            var message = string.Empty;
            yield return CloudClient.SignInWithUgsBridge(
                UgsAuthService.PlayerId,
                UgsAuthService.AccessToken,
                displayName,
                (ok, msg) =>
                {
                    success = ok;
                    message = msg;
                });

            callback(success, message);
        }

        public static void ApplyCampaignResult(MatchResult result)
        {
            if (!IsInitialized || result == null || result.matchMode != MatchMode.Campaign || !result.playerWon)
            {
                return;
            }

            HeroCollectionService.EnsureStarterCollection();
            HeroCollectionService.CompleteCampaignMission(result.campaignMissionLevel, result.campaignUnlockHeroId);
        }

        public static void SaveHeroCollection(PlayerProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            Profile = profile;
            if (UseCloudBackend && CloudClient != null && CloudClient.IsAuthenticated)
            {
                CloudCoroutineHost.Instance.Run(CoSaveHeroCollection(profile));
                return;
            }

            ProfileStore.Save(profile);
            NotifyProfileChanged();
        }

        private static IEnumerator CoSaveHeroCollection(PlayerProfile profile)
        {
            var success = false;
            yield return CloudClient.UpdateHeroCollection(
                profile.selectedHeroId,
                profile.unlockedHeroIdsCsv,
                profile.campaignHighestLevel,
                (ok, _) => success = ok);

            if (!success)
            {
                ProfileStore.Save(profile);
            }

            NotifyProfileChanged();
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

        private static IEnumerator CoLoadAndRepairCloudProfile()
        {
            yield return LoadCloudProfileRoutine();
            yield return CoRepairProfileDisplayName();
            if (Profile != null)
            {
                NotifyProfileChanged();
            }
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

        private static IEnumerator CoRepairProfileDisplayName()
        {
            if (Profile == null || CloudClient == null || !CloudClient.IsAuthenticated)
            {
                yield break;
            }

            if (!IsSyntheticDisplayName(Profile.displayName))
            {
                CacheDisplayName(Profile.displayName);
                yield break;
            }

            var repairedName = string.Empty;
            yield return CoResolveUgsDisplayName(string.Empty, resolved => repairedName = resolved);
            if (string.IsNullOrWhiteSpace(repairedName))
            {
                yield break;
            }

            Profile.displayName = repairedName;
            CacheDisplayName(repairedName);
            yield return CloudClient.UpdateDisplayName(repairedName, (_, _) => { });
        }

        private static IEnumerator CoResolveUgsDisplayName(string preferredName, Action<string> callback)
        {
            var resolved = ResolveBridgedDisplayName(preferredName);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                callback(resolved);
                yield break;
            }

            yield return UgsAuthService.CoGetUsername(username =>
            {
                callback(!string.IsNullOrWhiteSpace(username) ? username.Trim() : string.Empty);
            });
        }

        private static PlayerProfile BuildFallbackCloudProfile()
        {
            var email = CloudClient.UserEmail ?? string.Empty;
            var displayName = ResolveBridgedDisplayName(CloudClient.DisplayNameFromMetadata);
            if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrEmpty(email))
            {
                var emailPrefix = email.Split('@')[0];
                if (!IsSyntheticDisplayName(emailPrefix))
                {
                    displayName = emailPrefix;
                }
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

        private static bool IsSyntheticDisplayName(string displayName)
        {
            return !string.IsNullOrWhiteSpace(displayName)
                && displayName.StartsWith("ugs+", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveBridgedDisplayName(string preferredName)
        {
            if (!string.IsNullOrWhiteSpace(preferredName) && !IsSyntheticDisplayName(preferredName))
            {
                return preferredName.Trim();
            }

            var cached = PlayerPrefs.GetString(CachedDisplayNameKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(cached) && !IsSyntheticDisplayName(cached))
            {
                return cached.Trim();
            }

            var metadata = CloudClient?.DisplayNameFromMetadata;
            if (!string.IsNullOrWhiteSpace(metadata) && !IsSyntheticDisplayName(metadata))
            {
                return metadata.Trim();
            }

            return string.Empty;
        }

        private static void CacheDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName) || IsSyntheticDisplayName(displayName))
            {
                return;
            }

            PlayerPrefs.SetString(CachedDisplayNameKey, displayName.Trim());
            PlayerPrefs.Save();
        }

        private static void NotifyProfileChanged()
        {
            ProfileChanged?.Invoke();
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

                yield return CoLoadAndRepairCloudProfile();
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
                return "Play Practice anytime. Sign in only when you want to queue for Rated matches.";
            }

            return
                $"Welcome, {Profile.displayName}  •  MMR {Profile.mmr}  •  W {Profile.wins} / L {Profile.losses}  •  Streak {Profile.currentWinStreak}";
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        public static IEnumerator CoRunAuthSmokeTest(Action<string> callback)
        {
            if (!IsInitialized)
            {
                Initialize();
            }

            if (!UseCloudBackend || CloudClient == null)
            {
                callback("Cloud backend is not configured.");
                yield break;
            }

            var lines = new List<string>
            {
                $"Transport: {SupabaseHttpTransport.AuthTransportRevision}",
                $"UGS signed in: {UgsAuthService.IsSignedIn}",
                $"Supabase signed in: {CloudClient.IsAuthenticated}"
            };

            if (!CloudClient.IsAuthenticated)
            {
                lines.Add("Log in first, then rerun the smoke test.");
                callback(string.Join("\n", lines));
                yield break;
            }

            var token = CloudClient.AccessToken ?? string.Empty;
            var userId = CloudClient.UserId ?? string.Empty;
            lines.Add($"Token length: {token.Length}");
            lines.Add($"User id: {(userId.Length > 12 ? userId.Substring(0, 12) + "..." : userId)}");

            var refreshed = false;
            yield return CloudClient.RefreshSession(ok => refreshed = ok);
            lines.Add($"Refresh: {(refreshed ? "OK" : "failed")}");

            if (refreshed)
            {
                lines.Add($"New token length: {CloudClient.AccessToken?.Length ?? 0}");
            }

            var profileLoaded = false;
            var profileError = string.Empty;
            yield return CloudClient.GetProfile((success, error, loadedProfile) =>
            {
                profileLoaded = success && loadedProfile != null;
                profileError = error;
            });

            if (profileLoaded)
            {
                lines.Add($"Profile: {Profile?.displayName ?? "loaded"} | MMR {Profile?.mmr ?? 0}");
            }
            else
            {
                lines.Add($"Profile fetch failed: {profileError}");
            }

            callback(string.Join("\n", lines));
        }
#endif
    }
}

namespace DreamGate.Battlegrounds.Services.Backend
{
    public static class UgsAuthService
    {
        private static bool initializeStarted;
        private static bool initializeFinished;
        private static string initializeError = string.Empty;

        public static bool IsSupported => Application.isEditor || Application.platform == RuntimePlatform.IPhonePlayer;

        public static bool IsSignedIn
        {
            get
            {
                if (!initializeFinished || !string.IsNullOrEmpty(initializeError) || !IsUnityServicesReady())
                {
                    return false;
                }

                return AuthenticationService.Instance.IsSignedIn;
            }
        }

        public static string PlayerId
        {
            get
            {
                if (!IsSignedIn)
                {
                    return string.Empty;
                }

                return AuthenticationService.Instance.PlayerId ?? string.Empty;
            }
        }

        public static string AccessToken
        {
            get
            {
                if (!IsSignedIn)
                {
                    return string.Empty;
                }

                return AuthenticationService.Instance.AccessToken ?? string.Empty;
            }
        }

        public static IEnumerator CoEnsureInitialized(Action<bool, string> callback = null)
        {
            if (initializeFinished
                && string.IsNullOrEmpty(initializeError)
                && IsUnityServicesReady())
            {
                callback?.Invoke(true, string.Empty);
                yield break;
            }

            if (initializeFinished && !IsUnityServicesReady())
            {
                initializeFinished = false;
                initializeStarted = false;
                initializeError = string.Empty;
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
            if (!IsUnityServicesReady())
            {
                return;
            }

            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
            }
        }

        public static IEnumerator CoGetUsername(Action<string> callback)
        {
            var success = false;
            var message = string.Empty;
            yield return CoEnsureInitialized((ok, error) =>
            {
                success = ok;
                message = error;
            });

            if (!success || !IsSignedIn)
            {
                callback(string.Empty);
                yield break;
            }

            PlayerInfo playerInfo = null;
            Exception failure = null;
            yield return RunAsync(
                async () => playerInfo = await AuthenticationService.Instance.GetPlayerInfoAsync(),
                ex => failure = ex);

            if (failure != null || playerInfo == null || string.IsNullOrWhiteSpace(playerInfo.Username))
            {
                callback(string.Empty);
                yield break;
            }

            callback(playerInfo.Username.Trim());
        }

        private static bool IsUnityServicesReady()
        {
            return UnityServices.State == ServicesInitializationState.Initialized;
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