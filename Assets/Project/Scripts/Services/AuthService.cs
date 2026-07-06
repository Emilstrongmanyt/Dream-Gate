using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services
{
    public static class AuthService
    {
        private const string SessionKey = "dreamgate.session.player_id";
        private const int MinPasswordLength = 6;

        public static bool IsLoggedIn => !string.IsNullOrEmpty(SessionPlayerId);
        public static string SessionPlayerId { get; private set; }
        public static AccountCredentials CurrentAccount { get; private set; }

        public static void RestoreSession()
        {
            SessionPlayerId = PlayerPrefs.GetString(SessionKey, string.Empty);
            CurrentAccount = AccountRegistry.FindByPlayerId(SessionPlayerId);
            if (CurrentAccount == null)
            {
                SessionPlayerId = string.Empty;
            }
        }

        public static bool TryRegister(string displayName, string email, string password, string confirmPassword, out string message)
        {
            message = string.Empty;
            if (!ValidateRegistrationInput(displayName, email, password, confirmPassword, out message))
            {
                return false;
            }

            var salt = GenerateSalt();
            var account = new AccountCredentials
            {
                playerId = Guid.NewGuid().ToString("N"),
                email = AccountRegistry.NormalizeEmail(email),
                displayName = displayName.Trim(),
                salt = salt,
                passwordHash = HashPassword(password, salt),
                createdAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            if (!AccountRegistry.TryAdd(account, out message))
            {
                return false;
            }

            var profile = ProfileStore.CreateNewProfile(account.playerId, account.displayName, account.email);
            ProfileStore.Save(profile);
            SetSession(account);
            message = $"Account created. Welcome, {account.displayName}!";
            return true;
        }

        public static bool TryLogin(string email, string password, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                message = "Enter your email and password.";
                return false;
            }

            var account = AccountRegistry.FindByEmail(email);
            if (account == null)
            {
                message = "No account found for that email.";
                return false;
            }

            if (account.passwordHash != HashPassword(password, account.salt))
            {
                message = "Incorrect password.";
                return false;
            }

            SetSession(account);
            var profile = ProfileStore.Load(account.playerId);
            profile.displayName = account.displayName;
            profile.email = account.email;
            ProfileStore.Save(profile);
            message = $"Welcome back, {account.displayName}!";
            return true;
        }

        public static void Logout()
        {
            SessionPlayerId = string.Empty;
            CurrentAccount = null;
            PlayerPrefs.DeleteKey(SessionKey);
            PlayerPrefs.Save();
        }

        private static void SetSession(AccountCredentials account)
        {
            CurrentAccount = account;
            SessionPlayerId = account.playerId;
            PlayerPrefs.SetString(SessionKey, SessionPlayerId);
            PlayerPrefs.Save();
        }

        private static bool ValidateRegistrationInput(
            string displayName,
            string email,
            string password,
            string confirmPassword,
            out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length < 2)
            {
                message = "Display name must be at least 2 characters.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                message = "Enter a valid email address.";
                return false;
            }

            if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            {
                message = $"Password must be at least {MinPasswordLength} characters.";
                return false;
            }

            if (password != confirmPassword)
            {
                message = "Passwords do not match.";
                return false;
            }

            return true;
        }

        private static string GenerateSalt()
        {
            var bytes = new byte[16];
            RandomNumberGenerator.Create().GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static string HashPassword(string password, string salt)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(salt + password);
            return Convert.ToBase64String(sha.ComputeHash(bytes));
        }
    }
}