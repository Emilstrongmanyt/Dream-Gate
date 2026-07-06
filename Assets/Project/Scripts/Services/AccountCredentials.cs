using System;

namespace DreamGate.Battlegrounds.Services
{
    [Serializable]
    public class AccountCredentials
    {
        public string playerId;
        public string email;
        public string displayName;
        public string passwordHash;
        public string salt;
        public long createdAtUnix;
    }

    [Serializable]
    public class AccountRegistryData
    {
        public AccountCredentials[] accounts = Array.Empty<AccountCredentials>();
    }
}