using System.Collections.Generic;
using Kindling.Sim.Catalog;

namespace Kindling.Sim.Model
{
    public sealed class UnitInstance
    {
        public ulong InstanceId;
        public UnitId CatalogId;
        public int Atk;
        public int Hp;
        public int MaxHp;
        public int Cinders;
        public int ExtraAtk;
        public int ExtraHp;
        public int AuraAtk;
        public int AuraHp;
        public Keyword Keywords;
        public bool Awakened;
        public int AttacksThisCombat;
        public int AttackCharges = 1;
        public bool AfterglowConsumed;
        public bool DeathProcessed;
        public int EchoTimesBonus;
        public List<LatchAttachment> Latches = new List<LatchAttachment>();
        public List<Modifier> Mods = new List<Modifier>();
        public List<EffectDef> ExtraEffects = new List<EffectDef>();
        public List<string> ExhaustedOnce = new List<string>();
        public int CombatSeat = -1;

        public int EffectiveAtk => Atk + AuraAtk;

        public bool Has(Keyword k) => (Keywords & k) != 0;

        public void AddKeyword(Keyword k) => Keywords |= k;

        public void RemoveKeyword(Keyword k) => Keywords &= (Keyword)~(ushort)k;

        public bool HasEcho(UnitDef def)
        {
            if (def != null)
            {
                List<EffectDef> src = Awakened && def.AwakenedEffects != null ? def.AwakenedEffects : def.Effects;
                if (src != null)
                {
                    for (int i = 0; i < src.Count; i++)
                    {
                        if (src[i].Trigger == Trigger.Echo) return true;
                    }
                }
            }
            if (ExtraEffects != null)
            {
                for (int i = 0; i < ExtraEffects.Count; i++)
                {
                    if (ExtraEffects[i].Trigger == Trigger.Echo) return true;
                }
            }
            return false;
        }

        public bool HasKindle(UnitDef def)
        {
            if (def != null)
            {
                List<EffectDef> src = Awakened && def.AwakenedEffects != null ? def.AwakenedEffects : def.Effects;
                if (src != null)
                {
                    for (int i = 0; i < src.Count; i++)
                    {
                        if (src[i].Trigger == Trigger.Kindle) return true;
                    }
                }
            }
            if (ExtraEffects != null)
            {
                for (int i = 0; i < ExtraEffects.Count; i++)
                {
                    if (ExtraEffects[i].Trigger == Trigger.Kindle) return true;
                }
            }
            return false;
        }

        public List<EffectDef> AllEffects(UnitDef def)
        {
            var list = new List<EffectDef>();
            if (def != null)
            {
                List<EffectDef> src = Awakened && def.AwakenedEffects != null ? def.AwakenedEffects : def.Effects;
                if (src != null)
                {
                    for (int i = 0; i < src.Count; i++)
                        list.Add(src[i]);
                }
            }
            if (ExtraEffects != null)
            {
                for (int i = 0; i < ExtraEffects.Count; i++)
                    list.Add(ExtraEffects[i]);
            }
            return list;
        }

        public UnitInstance Clone()
        {
            var c = new UnitInstance();
            CopyInto(c);
            return c;
        }

        public UnitInstance CloneForCombat(int seat)
        {
            var c = Clone();
            c.CombatSeat = seat;
            c.AttacksThisCombat = 0;
            c.AttackCharges = 1;
            c.DeathProcessed = false;
            c.EchoTimesBonus = 0;
            StripCombatMods(c);
            return c;
        }

        static void StripCombatMods(UnitInstance c)
        {
            c.Atk -= c.AuraAtk;
            c.Hp -= c.AuraHp;
            c.MaxHp -= c.AuraHp;
            c.AuraAtk = 0;
            c.AuraHp = 0;
            if (c.Hp < 1) c.Hp = 1;
            if (c.Mods == null) return;
            var keep = new List<Modifier>();
            for (int i = 0; i < c.Mods.Count; i++)
            {
                Modifier m = c.Mods[i];
                if (m.Tag == ModTag.ThisCombat || m.FromAura)
                {
                    c.Atk -= m.Atk;
                    c.Hp -= m.Hp;
                    c.MaxHp -= m.Hp;
                    c.Keywords &= (Keyword)~(ushort)m.Keywords;
                    continue;
                }
                keep.Add(m);
            }
            c.Mods = keep;
            if (c.Hp < 1) c.Hp = 1;
            if (c.Atk < 0) c.Atk = 0;
        }

        void CopyInto(UnitInstance c)
        {
            c.InstanceId = InstanceId;
            c.CatalogId = CatalogId;
            c.Atk = Atk;
            c.Hp = Hp;
            c.MaxHp = MaxHp;
            c.Cinders = Cinders;
            c.ExtraAtk = ExtraAtk;
            c.ExtraHp = ExtraHp;
            c.AuraAtk = AuraAtk;
            c.AuraHp = AuraHp;
            c.Keywords = Keywords;
            c.Awakened = Awakened;
            c.AttacksThisCombat = AttacksThisCombat;
            c.AttackCharges = AttackCharges;
            c.AfterglowConsumed = AfterglowConsumed;
            c.DeathProcessed = DeathProcessed;
            c.EchoTimesBonus = EchoTimesBonus;
            c.CombatSeat = CombatSeat;
            c.Latches = new List<LatchAttachment>(Latches.Count);
            for (int i = 0; i < Latches.Count; i++)
            {
                LatchAttachment a = Latches[i];
                c.Latches.Add(new LatchAttachment
                {
                    CatalogId = a.CatalogId,
                    Atk = a.Atk,
                    Hp = a.Hp,
                    Keywords = a.Keywords
                });
            }
            c.Mods = new List<Modifier>(Mods.Count);
            for (int i = 0; i < Mods.Count; i++)
            {
                Modifier m = Mods[i];
                c.Mods.Add(new Modifier
                {
                    Tag = m.Tag,
                    Atk = m.Atk,
                    Hp = m.Hp,
                    Keywords = m.Keywords,
                    FromAura = m.FromAura
                });
            }
            c.ExtraEffects = new List<EffectDef>(ExtraEffects.Count);
            for (int i = 0; i < ExtraEffects.Count; i++)
                c.ExtraEffects.Add(ExtraEffects[i]);
            c.ExhaustedOnce = new List<string>(ExhaustedOnce.Count);
            for (int i = 0; i < ExhaustedOnce.Count; i++)
                c.ExhaustedOnce.Add(ExhaustedOnce[i]);
        }

        public bool OnceExhausted(string key)
        {
            if (ExhaustedOnce == null) return false;
            for (int i = 0; i < ExhaustedOnce.Count; i++)
            {
                if (ExhaustedOnce[i] == key) return true;
            }
            return false;
        }

        public void ExhaustOnce(string key)
        {
            if (ExhaustedOnce == null) ExhaustedOnce = new List<string>();
            ExhaustedOnce.Add(key);
        }

        public void ClearOnceThisRecruit()
        {
            if (ExhaustedOnce == null || ExhaustedOnce.Count == 0) return;
            var keep = new List<string>();
            for (int i = 0; i < ExhaustedOnce.Count; i++)
            {
                string k = ExhaustedOnce[i];
                if (k != null && k.IndexOf("|ThisRecruit|") >= 0) continue;
                keep.Add(k);
            }
            ExhaustedOnce = keep;
        }
    }
}
