using System;
using System.IO;
using Kindling.Sim.Catalog;
using Kindling.Sim.Match;

namespace Kindling.Tools.HeadlessAlpha
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            string content = FindContent();
            if (content == null)
            {
                Console.Error.WriteLine("content/ not found");
                return 2;
            }
            Catalog cat = Catalog.LoadFromDirectory(content);
            Console.WriteLine("Kindling HeadlessAlpha  catalog=" + cat.ContentVersion
                + " units=" + cat.Units.Count + " tokens=" + cat.Tokens.Count
                + " captains=" + cat.Captains.Count);

            ulong seed = 1;
            if (args != null && args.Length > 0)
                ulong.TryParse(args[0], out seed);
            if (seed == 0) seed = 1;

            MatchLoop loop = MatchLoop.CreateHeadless(cat, seed);
            loop.RunToEnd();

            for (int i = 0; i < loop.RoundLog.Count; i++)
                Console.WriteLine(loop.RoundLog[i]);

            Console.WriteLine("Match over. Places:");
            bool[] seen = new bool[9];
            int assigned = 0;
            for (int s = 0; s < loop.State.Seats.Length; s++)
            {
                var p = loop.State.Seats[s];
                int place = p.Place ?? 0;
                Console.WriteLine("  seat " + s + " " + p.DisplayName + " captain=" + p.Captain.Value
                    + " wick=" + p.Wick + " place=" + place + " depth=" + p.Depth);
                if (place >= 1 && place <= 8)
                {
                    seen[place] = true;
                    assigned++;
                }
            }
            bool ok = assigned == 8;
            for (int i = 1; i <= 8; i++)
                if (!seen[i]) ok = false;
            Console.WriteLine(ok ? "places 1..8 assigned" : "place assignment incomplete");
            return ok ? 0 : 1;
        }

        static string FindContent()
        {
            string start = AppContext.BaseDirectory;
            string found = Catalog.FindContentRoot(start);
            if (found != null) return found;
            string cwd = Directory.GetCurrentDirectory();
            return Catalog.FindContentRoot(cwd);
        }
    }
}
