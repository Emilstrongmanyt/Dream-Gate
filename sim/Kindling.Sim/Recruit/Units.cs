using Kindling.Sim.Catalog;
using Kindling.Sim.Model;
using Kindling.Sim.Rng;

namespace Kindling.Sim.Recruit
{
    public static class Units
    {
        public static UnitInstance Create(Catalog.Catalog cat, MatchRng rng, UnitId id, bool awakened = false)
        {
            UnitDef def = cat.GetUnit(id);
            if (def == null)
                throw new System.InvalidOperationException("unknown unit " + id.Value);
            return Create(def, rng, awakened);
        }

        public static UnitInstance Create(UnitDef def, MatchRng rng, bool awakened = false)
        {
            int atk = def.Atk;
            int hp = def.Hp;
            if (awakened)
            {
                atk = def.Atk * 2;
                hp = def.Hp * 2;
            }
            var u = new UnitInstance
            {
                InstanceId = rng != null ? rng.NextId() : 1,
                CatalogId = def.Id,
                Atk = atk,
                Hp = hp,
                MaxHp = hp,
                Keywords = def.Keywords,
                Awakened = awakened,
                AttackCharges = 1
            };
            return u;
        }

        public static UnitInstance CreateRaw(MatchRng rng, string catalogId, int atk, int hp, Keyword keywords, bool awakened = false)
        {
            return new UnitInstance
            {
                InstanceId = rng != null ? rng.NextId() : 1,
                CatalogId = new UnitId(catalogId),
                Atk = atk,
                Hp = hp,
                MaxHp = hp,
                Keywords = keywords,
                Awakened = awakened,
                AttackCharges = 1
            };
        }

        public static void GiveCinder(UnitInstance u, int n)
        {
            if (u == null || n == 0) return;
            u.Cinders += n;
            u.ExtraAtk += n;
            u.ExtraHp += n;
            u.Atk += n;
            u.Hp += n;
            u.MaxHp += n;
        }

        public static void BuffPermanent(UnitInstance u, int atk, int hp)
        {
            if (u == null) return;
            u.ExtraAtk += atk;
            u.ExtraHp += hp;
            u.Atk += atk;
            u.Hp += hp;
            u.MaxHp += hp;
            if (u.Atk < 0) u.Atk = 0;
            if (u.Hp < 1) u.Hp = 1;
            if (u.MaxHp < 1) u.MaxHp = 1;
        }

        public static void BuffCombat(UnitInstance u, int atk, int hp, Keyword kw = Keyword.None)
        {
            if (u == null) return;
            u.Atk += atk;
            u.Hp += hp;
            u.MaxHp += hp;
            if (kw != Keyword.None) u.AddKeyword(kw);
            u.Mods.Add(new Modifier
            {
                Tag = ModTag.ThisCombat,
                Atk = atk,
                Hp = hp,
                Keywords = kw
            });
        }

        public static void ApplyAura(UnitInstance u, int atk, int hp)
        {
            if (u == null) return;
            u.AuraAtk += atk;
            u.AuraHp += hp;
            u.Atk += atk;
            u.Hp += hp;
            u.MaxHp += hp;
        }

        public static void StripAura(UnitInstance u)
        {
            if (u == null) return;
            u.Atk -= u.AuraAtk;
            u.Hp -= u.AuraHp;
            u.MaxHp -= u.AuraHp;
            u.AuraAtk = 0;
            u.AuraHp = 0;
            u.EchoTimesBonus = 0;
            if (u.Atk < 0) u.Atk = 0;
            if (u.Hp > 0 && u.Hp < 1) u.Hp = 1;
            if (u.Hp <= 0)
            {
            }
            else if (u.Hp < 1)
            {
                u.Hp = 1;
            }
        }

        public static int RingDepth(UnitDef def)
        {
            if (def == null) return 1;
            if (def.Token)
                return def.TokenDamageDepth > 0 ? def.TokenDamageDepth : 1;
            return def.Depth;
        }
    }
}
