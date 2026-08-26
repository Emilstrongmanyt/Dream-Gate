using System;
using System.Security.Cryptography;
using System.Text;

namespace Kindling.Sim.Match
{
    public static class DeviceAuth
    {
        public static string HashDevice(string deviceId, string pepper)
        {
            return HmacHex(pepper ?? "kindling", deviceId ?? "");
        }

        public static string NewAccountId()
        {
            return Guid.NewGuid().ToString("D");
        }

        public static string IssueToken(string accountId, string pepper)
        {
            string sig = HmacHex(pepper ?? "kindling", accountId ?? "");
            return (accountId ?? "") + "." + sig;
        }

        public static bool Verify(string token, string pepper)
        {
            if (string.IsNullOrEmpty(token)) return false;
            int dot = token.IndexOf('.');
            if (dot <= 0) return false;
            string id = token.Substring(0, dot);
            string sig = token.Substring(dot + 1);
            string expect = HmacHex(pepper ?? "kindling", id);
            if (sig.Length != expect.Length) return false;
            int diff = 0;
            for (int i = 0; i < sig.Length; i++)
                diff |= sig[i] ^ expect[i];
            return diff == 0;
        }

        public static string AccountId(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            int dot = token.IndexOf('.');
            return dot <= 0 ? "" : token.Substring(0, dot);
        }

        static string HmacHex(string pepper, string msg)
        {
            var key = Encoding.UTF8.GetBytes(pepper);
            var data = Encoding.UTF8.GetBytes(msg);
            using (var h = new HMACSHA256(key))
            {
                byte[] hash = h.ComputeHash(data);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
