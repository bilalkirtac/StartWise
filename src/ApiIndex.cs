using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace TiaAssistant
{
    // Self-learning layer 1: searchable map of the Openness API of the TIA version this exe runs against
    // (Siemens.Engineering.xml doc comments + reflection), and of the hardware attribute manual
    // (knowledge/api/hw-attributes-V18.json, generated from "TIA Portal Openness Hardware parameters.pdf").
    // Needs no TIA Portal; works for any version because it reads the API files of that version.
    public static class ApiIndex
    {
        class Entry { public string Kind, Id, Name, Owner, Summary, Signature; }

        static List<Entry> _api;
        static Dictionary<string, object> _hw;
        static readonly object _lock = new object();

        static string Clean(string s)
        {
            if (s == null) return "";
            s = Regex.Replace(s, @"<see\s+cref=""\w:([^""]+)""\s*/>", m => Short(m.Groups[1].Value));
            s = Regex.Replace(s, @"<paramref\s+name=""([^""]+)""\s*/>", "$1");
            s = Regex.Replace(s, @"<[^>]+>", "");
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        static string Short(string id)
        {
            string s = Regex.Replace(id, @"\(.*\)$", "");
            return s.StartsWith("Siemens.Engineering.") ? s.Substring(20) : s;
        }

        static string TypeName(Type t)
        {
            if (t == null) return "void";
            if (t.IsGenericType)
            {
                string n = t.Name; int i = n.IndexOf('`'); if (i > 0) n = n.Substring(0, i);
                return n + "<" + string.Join(", ", t.GetGenericArguments().Select(TypeName).ToArray()) + ">";
            }
            return t.Name;
        }

        static Type[] LoadTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null).ToArray(); }
        }

        static void AddSignatures(Type t, Dictionary<string, string> sig)
        {
            if (!t.IsPublic && !t.IsNestedPublic) return;
            string tn = t.FullName;
            if (t.IsEnum) { sig["T:" + tn] = "enum " + t.Name + " { " + string.Join(", ", Enum.GetNames(t)) + " }"; return; }
            string kind = t.IsInterface ? "interface" : t.IsValueType ? "struct" : "class";
            string bases = t.BaseType != null && t.BaseType != typeof(object) ? " : " + TypeName(t.BaseType) : "";
            sig["T:" + tn] = kind + " " + TypeName(t) + bases;
            foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                string rw = (p.CanRead ? "get; " : "") + (p.CanWrite && p.GetSetMethod() != null ? "set; " : "");
                sig["P:" + tn + "." + p.Name] = TypeName(p.PropertyType) + " " + p.Name + " { " + rw + "}";
            }
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.IsSpecialName) continue;
                string key = "M:" + tn + "." + m.Name;
                string s = TypeName(m.ReturnType) + " " + m.Name + "(" +
                    string.Join(", ", m.GetParameters().Select(x => TypeName(x.ParameterType) + " " + x.Name).ToArray()) + ")";
                sig[key] = sig.ContainsKey(key) ? sig[key] + " | " + s : s;
            }
        }

        static readonly HashSet<string> NoiseNames = new HashSet<string> { "Equals", "GetHashCode", "ToString", "GetType", "#ctor" };
        static bool Noise(string name) { return NoiseNames.Contains(name) || name.StartsWith("global::") || name.Contains("#"); }

        static string ParamsFromId(string id)
        {
            Match m = Regex.Match(id, @"\((.*)\)$");
            if (!m.Success) return "";
            List<string> parts = new List<string>(); int depth = 0; StringBuilder cur = new StringBuilder();
            foreach (char c in m.Groups[1].Value)
            {
                if (c == '{') depth++; else if (c == '}') depth--;
                if (c == ',' && depth == 0) { parts.Add(cur.ToString()); cur.Clear(); } else cur.Append(c);
            }
            parts.Add(cur.ToString());
            return string.Join(", ", parts.Select(p => Regex.Replace(p.Replace('{', '<').Replace('}', '>'), @"[A-Za-z0-9_]+\.", "")).ToArray());
        }

        static void EnsureApi()
        {
            lock (_lock)
            {
                if (_api != null) return;
                if (Program.ApiDir == null) throw new InvalidOperationException("Openness API folder not found; see tia_status.");
                Dictionary<string, string> docs = new Dictionary<string, string>();
                // V18: one Siemens.Engineering.xml; V21+: one xml per assembly (Base, Step7, WinCC, Startdrive ...).
                foreach (string xmlPath in Directory.GetFiles(Program.ApiDir, "Siemens.Engineering*.xml"))
                {
                    if (Path.GetFileName(xmlPath).StartsWith("Siemens.Engineering.AddIn", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        foreach (XElement m in XDocument.Load(xmlPath).Descendants("member"))
                        {
                            string id = (string)m.Attribute("name");
                            XElement sum = m.Element("summary");
                            if (id != null && !docs.ContainsKey(id)) docs[id] = Clean(sum != null ? string.Concat(sum.Nodes()) : "");
                        }
                    }
                    catch { }
                }

                // Signatures from the loaded assembly (same version as the docs).
                Dictionary<string, string> sig = new Dictionary<string, string>();
                foreach (Assembly asm in Program.SiemensAssemblies())
                    foreach (Type t in LoadTypes(asm))
                    {
                        try { AddSignatures(t, sig); } catch { }   // one unloadable type must not break the index
                    }

                List<Entry> list = new List<Entry>();
                HashSet<string> seen = new HashSet<string>();
                foreach (KeyValuePair<string, string> d in docs)
                {
                    string id = d.Key; char k = id[0];
                    string full = id.Substring(2);
                    string noArgs = Regex.Replace(full, @"\(.*\)$", "");
                    string owner = k == 'T' ? noArgs : noArgs.Substring(0, Math.Max(0, noArgs.LastIndexOf('.')));
                    string name = noArgs.Substring(noArgs.LastIndexOf('.') + 1);
                    if (Noise(name)) continue;
                    string skey = k + ":" + noArgs, s;
                    sig.TryGetValue(skey, out s);
                    if (s == null && k == 'M') s = name + "(" + ParamsFromId(full) + ")";   // reflection unavailable: params from the doc id
                    list.Add(new Entry { Kind = KindName(k), Id = id, Name = name, Owner = Short(owner), Summary = d.Value, Signature = s });
                    seen.Add(skey);
                }
                foreach (KeyValuePair<string, string> s in sig)   // members without doc comments
                {
                    if (seen.Contains(s.Key)) continue;
                    string noArgs = s.Key.Substring(2);
                    if (Noise(noArgs.Substring(noArgs.LastIndexOf('.') + 1))) continue;
                    string owner = s.Key[0] == 'T' ? noArgs : noArgs.Substring(0, Math.Max(0, noArgs.LastIndexOf('.')));
                    list.Add(new Entry { Kind = KindName(s.Key[0]), Id = s.Key, Name = noArgs.Substring(noArgs.LastIndexOf('.') + 1), Owner = Short(owner), Summary = "", Signature = s.Value });
                }
                _api = list;
            }
        }

        static string KindName(char k)
        {
            switch (k) { case 'T': return "type"; case 'M': return "method"; case 'P': return "property"; case 'E': return "event"; case 'F': return "field"; default: return k.ToString(); }
        }

        static void EnsureHw()
        {
            lock (_lock)
            {
                if (_hw != null) return;
                string file = Path.Combine(U.KnowledgeDir(), "api", "hw-attributes-" + Program.TiaName + ".json");
                if (!File.Exists(file)) file = Path.Combine(U.KnowledgeDir(), "api", "hw-attributes-V18.json");   // until the version's own PDF is indexed
                if (!File.Exists(file)) { _hw = new Dictionary<string, object>(); return; }
                JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                _hw = js.Deserialize<Dictionary<string, object>>(File.ReadAllText(file, Encoding.UTF8));
            }
        }

        static int Score(string query, params string[] fields)
        {
            int score = 0;
            foreach (string tok in query.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int best = 0;
                for (int i = 0; i < fields.Length; i++)
                {
                    string f = (fields[i] ?? "").ToLowerInvariant();
                    if (f.Length == 0) continue;
                    if (i == 0 && f == tok) best = Math.Max(best, 100);
                    else if (f.Contains(tok)) best = Math.Max(best, i == 0 ? 40 : i == 1 ? 20 : 8);
                }
                if (best == 0) return 0;      // every word must match somewhere
                score += best;
            }
            return score;
        }

        public static object Search(string query, string scope, int limit)
        {
            if (string.IsNullOrEmpty(query)) throw new ArgumentException("query is required");
            if (limit <= 0) limit = 25;
            scope = string.IsNullOrEmpty(scope) ? "all" : scope.ToLowerInvariant();
            ArrayList api = new ArrayList(), hw = new ArrayList();
            int apiTotal = 0, hwTotal = 0;
            if (scope == "all" || scope == "api")
            {
                EnsureApi();
                var hits = _api.Select(e => new { e, s = Score(query, e.Name, e.Owner, e.Summary + " " + e.Signature) })
                               .Where(x => x.s > 0).OrderByDescending(x => x.s).ThenBy(x => x.e.Kind == "type" ? 0 : 1).ToList();
                apiTotal = hits.Count;
                foreach (var h in hits.Take(limit))
                    api.Add(U.Obj("kind", h.e.Kind, "owner", h.e.Kind == "type" ? null : h.e.Owner, "name", h.e.Kind == "type" ? h.e.Owner : h.e.Name,
                        "signature", h.e.Signature, "summary", h.e.Summary.Length > 300 ? h.e.Summary.Substring(0, 300) + "..." : h.e.Summary));
            }
            if (scope == "all" || scope == "hw")
            {
                EnsureHw();
                var hits = _hw.Select(kv => new { kv, d = (IDictionary)kv.Value })
                              .Select(x => new { x.kv, x.d, s = Score(query, x.kv.Key, Convert.ToString(x.d["label"]), string.Join(" ", ((IEnumerable)x.d["families"]).Cast<object>().Select(Convert.ToString).ToArray())) })
                              .Where(x => x.s > 0).OrderByDescending(x => x.s).ToList();
                hwTotal = hits.Count;
                foreach (var h in hits.Take(limit))
                {
                    Dictionary<string, object> o = U.Obj("attribute", h.kv.Key, "type", h.d["type"], "label", h.d["label"], "families", h.d["families"],
                        "module_examples", h.d["module_examples"]);
                    if (h.d.Contains("enums")) o["values"] = h.d["enums"];
                    hw.Add(o);
                }
            }
            return U.Obj("query", query, "tia_api", Program.ApiDir, "api_matches", apiTotal, "api", api,
                "hw_attribute_matches", hwTotal, "hw_attributes", hw,
                "note", "hw_attributes are read/written with GetAttribute/SetAttribute on DeviceItems (see device_inspect later); names are case-sensitive.");
        }

        // Everything about one type: kind, base, members with signatures and doc summaries.
        public static object TypeInfo(string name)
        {
            EnsureApi();
            string n = name.StartsWith("Siemens.Engineering.") ? name.Substring(20) : name;
            List<Entry> types = _api.Where(e => e.Kind == "type" && (e.Owner == n || e.Owner.EndsWith("." + n))).ToList();
            if (types.Count == 0)
            {
                string[] near = _api.Where(e => e.Kind == "type" && e.Owner.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0).Select(e => e.Owner).Take(20).ToArray();
                throw new InvalidOperationException("Type '" + name + "' not found. Similar: " + string.Join(", ", near));
            }
            if (types.Count > 1 && !types.Any(t => t.Owner == n))
                return U.Obj("ambiguous", types.Select(t => t.Owner).ToArray());
            Entry ty = types.FirstOrDefault(t => t.Owner == n) ?? types[0];
            ArrayList members = new ArrayList();
            foreach (Entry m in _api.Where(e => e.Kind != "type" && e.Owner == ty.Owner).OrderBy(e => e.Kind).ThenBy(e => e.Name))
                members.Add(U.Obj("kind", m.Kind, "name", m.Name, "signature", m.Signature, "summary", m.Summary));
            return U.Obj("type", ty.Owner, "signature", ty.Signature, "summary", ty.Summary, "members", members);
        }
    }
}
