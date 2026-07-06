using DreamGate.Battlegrounds.Core;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services
{
    /// <summary>
    /// Player services with local account auth. Cloud sync can be layered on later.
    /// </summary>
    public static class DreamGateServices
    {
        public static bool IsInitialized { get; private set; }
        public static bool IsLoggedIn => AuthService.IsLoggedIn;
        public static PlayerProfile Profile { get; private set; }

        public static void Initialize()
        {
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

            IsInitialized = true;
        }

        public static bool TryRegister(string displayName, string email, string password, string confirmPassword, out string message)
        {
            if (!AuthService.TryRegister(displayName, email, password, confirmPassword, out message))
            {
                return false;
            }

            Profile = ProfileStore.Load(AuthService.SessionPlayerId);
            return true;
        }

        public static bool TryLogin(string email, string password, out string message)
        {
            if (!AuthService.TryLogin(email, password, out message))
            {
                return false;
            }

            Profile = ProfileStore.Load(AuthService.SessionPlayerId);
            return true;
        }

        public static void Logout()
        {
            AuthService.Logout();
            Profile = null;
        }

        public static void ApplyRatedResult(MatchResult result)
        {
            if (!IsInitialized || !IsLoggedIn || Profile == null || result == null || result.matchMode != MatchMode.Rated)
            {
                return;
            }

            var mmrBefore = Profile.mmr;
            var delta = MmrCalculator.CalculateDelta(result.placement, mmrBefore);
            Profile.mmr = Mathf.Max(0, mmrBefore + delta);
            Profile.ratedGamesPlayed++;
            Profile.wins += result.placement == 1 ? 1 : 0;
            Profile.top4Finishes += result.placement <= 4 ? 1 : 0;
            ProfileStore.Save(Profile);

            result.mmrBefore = mmrBefore;
            result.mmrAfter = Profile.mmr;
            result.mmrDelta = delta;
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
                DreamGateServices.Initialize();
            }

            if (!IsLoggedIn || Profile == null)
            {
                return "Log in or create an account to track your rated MMR.";
            }

            return $"Welcome, {Profile.displayName}  •  MMR {Profile.mmr}  •  Games {Profile.ratedGamesPlayed}";
        }
    }
}