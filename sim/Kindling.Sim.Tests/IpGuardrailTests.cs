using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class IpGuardrailTests
    {
        static readonly string[] Banned = { "Battlegrounds", "Hearthstone", "Bob's Tavern", "Bobs Tavern" };
        static readonly Regex TavernWord = new Regex(@"\bTavern\b", RegexOptions.IgnoreCase);
        static readonly Regex BobWord = new Regex(@"\bBob\b", RegexOptions.IgnoreCase);

        [Fact]
        public void Client_and_content_do_not_use_competitor_names()
        {
            string root = FindRepoRoot();
            string[] scan = {
                Path.Combine(root, "client", "Assets"),
                Path.Combine(root, "content")
            };
            var hits = new List<string>();
            foreach (string dir in scan)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".cs" && ext != ".yaml" && ext != ".json" && ext != ".txt" && ext != ".md" && ext != ".unity")
                        continue;
                    string text = File.ReadAllText(file);
                    string rel = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    for (int i = 0; i < Banned.Length; i++)
                    {
                        if (text.IndexOf(Banned[i], StringComparison.OrdinalIgnoreCase) >= 0)
                            hits.Add(rel + ": " + Banned[i]);
                    }
                    if (TavernWord.IsMatch(text)) hits.Add(rel + ": Tavern");
                    if (BobWord.IsMatch(text)) hits.Add(rel + ": Bob");
                }
            }
            Assert.True(hits.Count == 0, string.Join("\n", hits));
        }

        static string FindRepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 12 && !string.IsNullOrEmpty(dir); i++)
            {
                if (Directory.Exists(Path.Combine(dir, "content")) && Directory.Exists(Path.Combine(dir, "client")))
                    return dir;
                DirectoryInfo parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            throw new DirectoryNotFoundException("kindling repo root not found");
        }
    }
}
