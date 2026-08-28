using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Kindling.Sim.Tests
{
    public class UnityMetaTests
    {
        static readonly Regex GuidLine = new Regex(@"^guid:\s*([0-9a-fA-F]+)\s*$", RegexOptions.Multiline);

        [Fact]
        public void Unity_script_metas_have_unique_32_char_guids()
        {
            string root = FindRepoRoot();
            string[] scan = {
                Path.Combine(root, "client", "Assets"),
                Path.Combine(root, "sim", "Kindling.Sim")
            };
            var guids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var missing = new List<string>();
            var bad = new List<string>();
            foreach (string dir in scan)
            {
                if (!Directory.Exists(dir))
                {
                    bad.Add("missing scan dir " + dir);
                    continue;
                }
                foreach (string cs in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    if (!File.Exists(cs + ".meta"))
                        missing.Add(Rel(root, cs));
                }
                foreach (string meta in Directory.GetFiles(dir, "*.meta", SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(meta);
                    var m = GuidLine.Match(text);
                    if (!m.Success)
                    {
                        bad.Add(Rel(root, meta) + ": no guid");
                        continue;
                    }
                    string g = m.Groups[1].Value;
                    if (g.Length != 32)
                        bad.Add(Rel(root, meta) + ": guid length " + g.Length);
                    if (guids.TryGetValue(g, out string other))
                        bad.Add(Rel(root, meta) + ": duplicate guid with " + other);
                    else
                        guids[g] = Rel(root, meta);
                }
            }
            Assert.True(missing.Count == 0, "missing .meta:\n" + string.Join("\n", missing));
            Assert.True(bad.Count == 0, string.Join("\n", bad));
            Assert.True(guids.Count > 20, "expected Unity metas under client/sim");
        }

        static string Rel(string root, string path)
        {
            return path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        static string FindRepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 12 && !string.IsNullOrEmpty(dir); i++)
            {
                if (Directory.Exists(Path.Combine(dir, "client", "Assets"))
                    && Directory.Exists(Path.Combine(dir, "sim", "Kindling.Sim")))
                    return dir;
                DirectoryInfo parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            dir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                if (Directory.Exists(Path.Combine(dir, "client", "Assets"))
                    && Directory.Exists(Path.Combine(dir, "sim", "Kindling.Sim")))
                    return dir;
                DirectoryInfo parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            throw new DirectoryNotFoundException("kindling repo root not found from " + AppContext.BaseDirectory);
        }
    }
}
