using System;
using System.Security.Cryptography;
using System.Text;

namespace Kindling.Sim.Match
{
    public static class AccountAuth
    {
        public static string NormalizeLogin(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c >= 'A' && c <= 'Z') sb.Append((char)(c + 32));
                else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')
                    sb.Append(c);
            }
            return sb.ToString();
        }

        public static string ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "NAME_SHORT";
            string t = name.Trim();
            if (t.Length < 3) return "NAME_SHORT";
            if (t.Length > 16) return "NAME_LONG";
            for (int i = 0; i < t.Length; i++)
            {
                char c = t[i];
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
                          || c == ' ' || c == '_' || c == '-';
                if (!ok) return "NAME_CHARS";
            }
            if (NormalizeLogin(t).Length < 3) return "NAME_SHORT";
            return null;
        }

        public static string ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6) return "PASS_SHORT";
            if (password.Length > 64) return "PASS_LONG";
            return null;
        }

        public static string NewSalt()
        {
            var buf = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(buf);
            return ToHex(buf);
        }

        public static string HashPassword(string password, string pepper, string salt)
        {
            return HmacHex((pepper ?? "") + ":" + (salt ?? ""), password ?? "");
        }

        public static bool VerifyPassword(string password, string pepper, string salt, string hash)
        {
            if (string.IsNullOrEmpty(hash)) return false;
            string expect = HashPassword(password, pepper, salt);
            if (expect.Length != hash.Length) return false;
            int diff = 0;
            for (int i = 0; i < hash.Length; i++)
                diff |= hash[i] ^ expect[i];
            return diff == 0;
        }

        public static string CreateAccount(string accountId, string displayName, string login, string salt, string passHash, string deviceHash)
        {
            return "{\"id\":\"" + Esc(accountId)
                + "\",\"displayName\":\"" + Esc(displayName)
                + "\",\"login\":\"" + Esc(login)
                + "\",\"passSalt\":\"" + Esc(salt)
                + "\",\"passHash\":\"" + Esc(passHash)
                + "\",\"mmr\":1500,\"rd\":350,\"matches\":0"
                + ",\"cosmetics\":\"gold\",\"frame\":\"gold\""
                + ",\"deviceHash\":\"" + Esc(deviceHash) + "\"}";
        }

        public static string PublicJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return "{}";
            string id = Protocol.ReadString(json, "id");
            string name = Protocol.ReadString(json, "displayName");
            string login = Protocol.ReadString(json, "login");
            int mmr = Protocol.ReadInt(json, "mmr");
            int rd = Protocol.ReadInt(json, "rd");
            int matches = Protocol.ReadInt(json, "matches");
            int last = Protocol.ReadInt(json, "lastPlace");
            string cosmetics = Protocol.ReadString(json, "cosmetics");
            string frame = Protocol.ReadString(json, "frame");
            if (mmr < 1) mmr = 1500;
            if (rd < 1) rd = 350;
            if (string.IsNullOrEmpty(cosmetics)) cosmetics = "gold";
            if (string.IsNullOrEmpty(frame)) frame = "gold";
            return "{\"id\":\"" + Esc(id)
                + "\",\"displayName\":\"" + Esc(name)
                + "\",\"login\":\"" + Esc(login)
                + "\",\"mmr\":" + mmr + ",\"rd\":" + rd
                + ",\"matches\":" + matches + ",\"lastPlace\":" + last
                + ",\"cosmetics\":\"" + Esc(cosmetics) + "\",\"frame\":\"" + Esc(frame) + "\"}";
        }

        public static string WithCosmetic(string prev, string cosmetics, string frame)
        {
            if (prev == null) prev = "{}";
            return PatchRatings(prev,
                Protocol.ReadString(prev, "id"),
                Protocol.ReadString(prev, "displayName"),
                Protocol.ReadInt(prev, "mmr"),
                Protocol.ReadInt(prev, "rd"),
                Protocol.ReadInt(prev, "matches"),
                Protocol.ReadInt(prev, "lastPlace"),
                cosmetics, frame);
        }

        public static string PatchRatings(string prev, string accountId, string displayName, int mmr, int rd, int matches, int lastPlace)
        {
            if (prev == null) prev = "{}";
            return PatchRatings(prev, accountId, displayName, mmr, rd, matches, lastPlace,
                Protocol.ReadString(prev, "cosmetics"), Protocol.ReadString(prev, "frame"));
        }

        public static string PatchRatings(string prev, string accountId, string displayName, int mmr, int rd, int matches, int lastPlace, string cosmetics, string frame)
        {
            if (prev == null) prev = "{}";
            string login = Protocol.ReadString(prev, "login");
            string salt = Protocol.ReadString(prev, "passSalt");
            string hash = Protocol.ReadString(prev, "passHash");
            string device = Protocol.ReadString(prev, "deviceHash");
            string name = string.IsNullOrEmpty(displayName) ? Protocol.ReadString(prev, "displayName") : displayName;
            if (string.IsNullOrEmpty(cosmetics)) cosmetics = Protocol.ReadString(prev, "cosmetics");
            if (string.IsNullOrEmpty(cosmetics)) cosmetics = "gold";
            if (string.IsNullOrEmpty(frame)) frame = Protocol.ReadString(prev, "frame");
            if (string.IsNullOrEmpty(frame)) frame = "gold";
            return "{\"id\":\"" + Esc(accountId)
                + "\",\"displayName\":\"" + Esc(name)
                + "\",\"login\":\"" + Esc(login)
                + "\",\"passSalt\":\"" + Esc(salt)
                + "\",\"passHash\":\"" + Esc(hash)
                + "\",\"mmr\":" + mmr + ",\"rd\":" + rd
                + ",\"matches\":" + matches + ",\"lastPlace\":" + lastPlace
                + ",\"cosmetics\":\"" + Esc(cosmetics) + "\",\"frame\":\"" + Esc(frame)
                + "\",\"deviceHash\":\"" + Esc(device) + "\"}";
        }

        static string HmacHex(string key, string msg)
        {
            var k = Encoding.UTF8.GetBytes(key ?? "");
            var d = Encoding.UTF8.GetBytes(msg ?? "");
            using (var h = new HMACSHA256(k))
                return ToHex(h.ComputeHash(d));
        }

        static string ToHex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
