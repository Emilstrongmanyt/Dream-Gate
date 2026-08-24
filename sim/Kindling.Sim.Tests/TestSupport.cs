using System;
using System.IO;
using Kindling.Sim.Catalog;
using Kindling.Sim.Model;
using Kindling.Sim.Recruit;
using Kindling.Sim.Rng;

namespace Kindling.Sim.Tests
{
    public static class TestSupport
    {
        static Catalog.Catalog _catalog;

        public static Catalog.Catalog Cat
        {
            get
            {
                if (_catalog == null)
                {
                    string root = FindContent();
                    _catalog = Catalog.Catalog.LoadFromDirectory(root);
                }
                return _catalog;
            }
        }

        public static string FindContent()
        {
            string found = Catalog.Catalog.FindContentRoot(AppContext.BaseDirectory);
            if (found != null) return found;
            found = Catalog.Catalog.FindContentRoot(Directory.GetCurrentDirectory());
            if (found != null) return found;
            throw new DirectoryNotFoundException("content/ not found from " + AppContext.BaseDirectory);
        }

        public static PlayerState Player(int seat = 0, int wick = 30)
        {
            return new PlayerState
            {
                Seat = seat,
                Wick = wick,
                Depth = 1,
                UpgradeCost = 5,
                DisplayName = "P" + seat
            };
        }

        public static UnitInstance Unit(MatchRng rng, string id, bool awakened = false)
        {
            return Units.Create(Cat, rng, new UnitId(id), awakened);
        }

        public static MatchRng Rng(ulong seed = 1)
        {
            return new MatchRng(seed);
        }
    }
}
