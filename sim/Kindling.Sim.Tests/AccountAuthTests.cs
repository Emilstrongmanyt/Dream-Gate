using Kindling.Sim.Match;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class AccountAuthTests
    {
        [Fact]
        public void Normalize_login_strips_spaces_and_case()
        {
            Assert.Equal("adafox", AccountAuth.NormalizeLogin("Ada Fox"));
            Assert.Equal("cap-1", AccountAuth.NormalizeLogin("Cap-1"));
        }

        [Fact]
        public void Name_and_password_validate()
        {
            Assert.Equal("NAME_SHORT", AccountAuth.ValidateName("ab"));
            Assert.Equal("NAME_CHARS", AccountAuth.ValidateName("Ada!"));
            Assert.Null(AccountAuth.ValidateName("Ada Fox"));
            Assert.Equal("PASS_SHORT", AccountAuth.ValidatePassword("123"));
            Assert.Null(AccountAuth.ValidatePassword("secret1"));
        }

        [Fact]
        public void Password_hash_roundtrip()
        {
            string salt = AccountAuth.NewSalt();
            string hash = AccountAuth.HashPassword("secret1", "pepper", salt);
            Assert.True(AccountAuth.VerifyPassword("secret1", "pepper", salt, hash));
            Assert.False(AccountAuth.VerifyPassword("wrong", "pepper", salt, hash));
            Assert.False(AccountAuth.VerifyPassword("secret1", "other", salt, hash));
        }

        [Fact]
        public void Public_json_strips_password_fields()
        {
            string acc = AccountAuth.CreateAccount("id-1", "Ada", "ada", "salt", "hashsecret", "dev");
            Assert.Contains("passHash", acc);
            string pub = AccountAuth.PublicJson(acc);
            Assert.DoesNotContain("passHash", pub);
            Assert.DoesNotContain("passSalt", pub);
            Assert.Contains("Ada", pub);
            Assert.Contains("1500", pub);
        }

        [Fact]
        public void Patch_ratings_keeps_password_hash()
        {
            string acc = AccountAuth.CreateAccount("id-1", "Ada", "ada", "s", "h", "d");
            string next = AccountAuth.PatchRatings(acc, "id-1", "Ada", 1601, 200, 3, 2);
            Assert.Contains("\"passHash\":\"h\"", next);
            Assert.Contains("\"login\":\"ada\"", next);
            Assert.Contains("1601", next);
            Assert.Contains("\"lastPlace\":2", next);
        }

        [Fact]
        public void Login_index_roundtrip_on_memory_store()
        {
            var store = new MemoryMatchStore();
            store.PutLogin("ada", "acc-1");
            Assert.Equal("acc-1", store.GetLogin("ada"));
            Assert.Null(store.GetLogin("missing"));
        }
    }
}
