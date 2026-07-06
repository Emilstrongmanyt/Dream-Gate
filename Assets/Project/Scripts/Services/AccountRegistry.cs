using System;
using System.Linq;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services
{
    public static class AccountRegistry
    {
        private const string RegistryKey = "dreamgate.accounts.registry";

        public static AccountCredentials[] LoadAll()
        {
            if (!PlayerPrefs.HasKey(RegistryKey))
            {
                return Array.Empty<AccountCredentials>();
            }

            var json = PlayerPrefs.GetString(RegistryKey);
            if (string.IsNullOrEmpty(json))
            {
                return Array.Empty<AccountCredentials>();
            }

            var data = JsonUtility.FromJson<AccountRegistryData>(json);
            return data?.accounts ?? Array.Empty<AccountCredentials>();
        }

        public static AccountCredentials FindByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var normalized = NormalizeEmail(email);
            return LoadAll().FirstOrDefault(account => account.email == normalized);
        }

        public static AccountCredentials FindByPlayerId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return null;
            }

            return LoadAll().FirstOrDefault(account => account.playerId == playerId);
        }

        public static bool TryAdd(AccountCredentials account, out string error)
        {
            error = string.Empty;
            if (account == null)
            {
                error = "Account data is missing.";
                return false;
            }

            var accounts = LoadAll().ToList();
            account.email = NormalizeEmail(account.email);
            if (accounts.Any(existing => existing.email == account.email))
            {
                error = "An account with that email already exists.";
                return false;
            }

            accounts.Add(account);
            SaveAll(accounts);
            return true;
        }

        public static void SaveAll(System.Collections.Generic.IEnumerable<AccountCredentials> accounts)
        {
            var payload = new AccountRegistryData
            {
                accounts = accounts?.ToArray() ?? Array.Empty<AccountCredentials>()
            };
            PlayerPrefs.SetString(RegistryKey, JsonUtility.ToJson(payload));
            PlayerPrefs.Save();
        }

        public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    }
}