using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TiaAssistant
{
    // MCP Skills extension (io.modelcontextprotocol/skills, SEP-2640): the server publishes skills (folders with SKILL.md)
    // next to its tools. skills/list and skills/get return the entries, resources/read returns the files.
    // Skills live in a 'skills' folder found like knowledge\ (package root, or the repository in development mode).
    static class Skills
    {
        public const string ExtensionId = "io.modelcontextprotocol/skills";

        public static string Root()
        {
            string dir = Program.HomeDir();
            for (string d = Path.GetDirectoryName(dir); d != null; d = Path.GetDirectoryName(d))
                if (Directory.Exists(Path.Combine(d, "skills"))) return Path.Combine(d, "skills");
            return null;
        }

        // Every folder under the root that contains SKILL.md is a skill; its skill-path is the folder path relative to the root.
        static List<string> SkillDirs()
        {
            List<string> r = new List<string>();
            string root = Root();
            if (root == null) return r;
            foreach (string f in Directory.GetFiles(root, "SKILL.md", SearchOption.AllDirectories)) r.Add(Path.GetDirectoryName(f));
            r.Sort(StringComparer.OrdinalIgnoreCase);
            return r;
        }

        static string Rel(string root, string full) { return full.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/'); }

        static string UriOf(string file) { return "skill://" + Rel(Root(), file); }

        // Minimal YAML frontmatter reader: top-level "key: value" lines (quotes removed). Enough for name/description/license.
        static Dictionary<string, object> Frontmatter(string skillMd)
        {
            Dictionary<string, object> fm = new Dictionary<string, object>();
            string[] lines = File.ReadAllText(skillMd, Encoding.UTF8).Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != "---") return fm;
            for (int i = 1; i < lines.Length; i++)
            {
                string l = lines[i];
                if (l.Trim() == "---") break;
                if (l.Length == 0 || char.IsWhiteSpace(l[0])) continue;
                int c = l.IndexOf(':');
                if (c <= 0) continue;
                string v = l.Substring(c + 1).Trim();
                if (v.Length >= 2 && ((v[0] == '"' && v[v.Length - 1] == '"') || (v[0] == '\'' && v[v.Length - 1] == '\''))) v = v.Substring(1, v.Length - 2);
                fm[l.Substring(0, c).Trim()] = v;
            }
            return fm;
        }

        static Dictionary<string, object> Entry(string dir)
        {
            string md = Path.Combine(dir, "SKILL.md");
            ArrayList res = new ArrayList();
            List<string> files = new List<string>(Directory.GetFiles(dir, "*", SearchOption.AllDirectories));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            files.Remove(md); files.Insert(0, md);
            using (SHA256 sha = SHA256.Create())
                foreach (string f in files)
                {
                    byte[] b = File.ReadAllBytes(f);
                    StringBuilder hex = new StringBuilder("sha256:");
                    foreach (byte x in sha.ComputeHash(b)) hex.Append(x.ToString("x2"));
                    res.Add(U.Obj("uri", UriOf(f), "digest", hex.ToString(), "size", b.Length));
                }
            return U.Obj("uri", UriOf(md), "frontmatter", Frontmatter(md), "resources", res);
        }

        public static object List()
        {
            ArrayList skills = new ArrayList();
            foreach (string d in SkillDirs()) skills.Add(Entry(d));
            return U.Obj("resultType", "complete", "skills", skills);
        }

        // Maps skill://<path> to a file under the skills root; null if it is not one (also blocks '..').
        static string FileOf(string uri)
        {
            string root = Root();
            if (root == null || uri == null || !uri.StartsWith("skill://", StringComparison.Ordinal)) return null;
            string rel = uri.Substring("skill://".Length);
            if (rel.Contains("..") || rel.Contains("\\")) return null;
            string full = Path.GetFullPath(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return null;
            return full;
        }

        public static object Get(string uri)
        {
            string f = FileOf(uri);
            if (f == null || !string.Equals(Path.GetFileName(f), "SKILL.md", StringComparison.Ordinal)) return null;
            return U.Obj("resultType", "complete", "skill", Entry(Path.GetDirectoryName(f)));
        }

        public static object Read(string uri)
        {
            string f = FileOf(uri);
            if (f == null) return null;
            string ext = Path.GetExtension(f).ToLowerInvariant();
            ArrayList contents = new ArrayList();
            if (ext == ".md" || ext == ".txt" || ext == ".scl" || ext == ".json" || ext == ".xml" || ext == ".ps1")
                contents.Add(U.Obj("uri", uri, "mimeType", ext == ".md" ? "text/markdown" : "text/plain", "text", File.ReadAllText(f, Encoding.UTF8)));
            else
                contents.Add(U.Obj("uri", uri, "mimeType", "application/octet-stream", "blob", Convert.ToBase64String(File.ReadAllBytes(f))));
            return U.Obj("contents", contents);
        }

        // resources/list: the SKILL.md of each skill (supporting files are reached from the skill entry).
        public static object ResourceList()
        {
            ArrayList list = new ArrayList();
            foreach (string d in SkillDirs())
            {
                string md = Path.Combine(d, "SKILL.md");
                Dictionary<string, object> fm = Frontmatter(md);
                object n, ds; fm.TryGetValue("name", out n); fm.TryGetValue("description", out ds);
                list.Add(U.Obj("uri", UriOf(md), "name", n ?? Path.GetFileName(d), "description", ds, "mimeType", "text/markdown"));
            }
            return U.Obj("resources", list);
        }
    }
}
