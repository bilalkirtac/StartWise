// StartWise (formerly TIA Assistant) - MCP server for Siemens TIA Portal (one build per TIA version: default V21, /define:TIA_V18 for V18)
// Written for the in-box .NET Framework C# 5 compiler: no NuGet, no SDK needed.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace TiaAssistant
{
    public static class Program
    {
        public const string ServerVersion = "0.34.1";
#if TIA_V18
        public const string TiaMajor = "18";
#else
        public const string TiaMajor = "21";      // V21 is the main line (Bilal, 20.11.2026); V18 builds with /define:TIA_V18
#endif
        public const string TiaVersion = TiaMajor + ".0";
        public const string TiaName = "V" + TiaMajor;      // "V18", "V21": folder names, XML <Engineering version>, messages
        public static string ApiDir;
        static StreamWriter _out;
        static StreamWriter _log;
        static JavaScriptSerializer _json;

        [STAThread]
        public static int Main(string[] argv)
        {
            // Must run before any Siemens type is touched.
            ApiDir = FindApiDir();
            AppDomain.CurrentDomain.AssemblyResolve += ResolveSiemens;

            _json = new JavaScriptSerializer();
            _json.MaxJsonLength = int.MaxValue;
            _json.RecursionLimit = 200;
            OpenLog();

            if (argv.Length > 0 && argv[0] == "--selftest")
            {
                Console.OutputEncoding = new UTF8Encoding(false);
                Console.WriteLine(Pretty(SafeCall("tia_status", new Dictionary<string, object>())));
                return 0;
            }

            _out = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
            _out.NewLine = "\n";
            _out.AutoFlush = true;
            StreamReader stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
            Log("server started, api dir: " + (ApiDir ?? "(not found)"));

            string line;
            while ((line = stdin.ReadLine()) != null)
            {
                if (line.Trim().Length == 0) continue;
                try { HandleLine(line); }
                catch (Exception ex) { Log("fatal while handling message: " + ex); }
            }
            Log("stdin closed, exiting");
            return 0;
        }

        // ---------- MCP (JSON-RPC 2.0 over newline-delimited stdio) ----------

        static void HandleLine(string line)
        {
            Dictionary<string, object> msg;
            try { msg = _json.DeserializeObject(line) as Dictionary<string, object>; }
            catch (Exception ex) { SendError(null, -32700, "Parse error: " + ex.Message); return; }
            if (msg == null) { SendError(null, -32600, "Invalid request"); return; }

            object id; msg.TryGetValue("id", out id);
            object m; msg.TryGetValue("method", out m);
            string method = m as string;
            if (method == null) return; // a response to us; nothing to do
            Dictionary<string, object> prm = null;
            object p; if (msg.TryGetValue("params", out p)) prm = p as Dictionary<string, object>;
            if (prm == null) prm = new Dictionary<string, object>();
            bool isNotification = !msg.ContainsKey("id");

            if (method == "initialize")
            {
                object pv; prm.TryGetValue("protocolVersion", out pv);
                SendResult(id, U.Obj(
                    "protocolVersion", (pv as string) ?? "2024-11-05",
                    "capabilities", U.Obj("tools", U.Obj()),
                    "serverInfo", U.Obj("name", "startwise", "version", ServerVersion),
                    "instructions", Tools.Instructions));
            }
            else if (method == "ping") { SendResult(id, U.Obj()); }
            else if (method == "tools/list") { SendResult(id, U.Obj("tools", Tools.List())); }
            else if (method == "tools/call")
            {
                object n; prm.TryGetValue("name", out n);
                object a; prm.TryGetValue("arguments", out a);
                Dictionary<string, object> args = a as Dictionary<string, object>;
                if (args == null) args = new Dictionary<string, object>();
                string name = n as string;
                Log("call " + name + " " + _json.Serialize(args));
                bool isError;
                string text = Invoke(name, args, out isError);
                Log((isError ? "  -> ERROR " : "  -> ok ") + (text.Length > 400 ? text.Substring(0, 400) + "..." : text));
                ArrayList content = new ArrayList();
                content.Add(U.Obj("type", "text", "text", text));
                SendResult(id, U.Obj("content", content, "isError", isError));
            }
            else if (isNotification) { /* notifications/initialized, cancelled, ... */ }
            else { SendError(id, -32601, "Method not found: " + method); }
        }

        static string Invoke(string name, Dictionary<string, object> args, out bool isError)
        {
            isError = false;
            try
            {
                Tool t = Tools.Find(name);
                if (t == null) { isError = true; return "Unknown tool: " + name; }
                return Pretty(t.Handler(args));
            }
            catch (Exception ex)
            {
                isError = true;
                return U.Describe(ex);
            }
        }

        static object SafeCall(string name, Dictionary<string, object> args)
        {
            try { return Tools.Find(name).Handler(args); }
            catch (Exception ex) { return U.Obj("error", U.Describe(ex)); }
        }

        static void SendResult(object id, object result)
        {
            _out.WriteLine(_json.Serialize(U.Obj("jsonrpc", "2.0", "id", id, "result", result)));
        }

        static void SendError(object id, int code, string message)
        {
            _out.WriteLine(_json.Serialize(U.Obj("jsonrpc", "2.0", "id", id, "error", U.Obj("code", code, "message", message))));
        }

        public static string Pretty(object o)
        {
            if (o is string) return (string)o;
            return _json.Serialize(o);
        }

        // ---------- Siemens assembly location ----------

        // V18 ships one Siemens.Engineering.dll in PublicAPI\V18; V21 splits it (Siemens.Engineering.Base/Step7/WinCC/Startdrive/...)
        // and puts it in PublicAPI\V21\net48. A folder counts when either main assembly is in it.
        static bool IsApiDir(string d)
        {
            return !string.IsNullOrEmpty(d) && (File.Exists(Path.Combine(d, "Siemens.Engineering.dll")) || File.Exists(Path.Combine(d, "Siemens.Engineering.Base.dll")));
        }

        static string FindApiDir()
        {
            string env = Environment.GetEnvironmentVariable("TIA_API_DIR");
            if (IsApiDir(env)) return env;
            if (!string.IsNullOrEmpty(env) && IsApiDir(Path.Combine(env, "net48"))) return Path.Combine(env, "net48");
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey k = hklm.OpenSubKey(@"SOFTWARE\Siemens\Automation\Openness\" + TiaVersion + @"\PublicAPI"))
                {
                    if (k != null)
                    {
                        // The key lists every API generation shipped with this TIA (e.g. 15.1 ... 18 under V18). Only our major is used.
                        foreach (string sub in k.GetSubKeyNames())
                        {
                            if (!sub.StartsWith(TiaMajor + ".")) continue;
                            using (RegistryKey v = k.OpenSubKey(sub))
                            {
                                foreach (string key in new[] { "Siemens.Engineering", "Siemens.Engineering.Base" })
                                {
                                    string dll = v == null ? null : v.GetValue(key) as string;
                                    if (!string.IsNullOrEmpty(dll) && File.Exists(dll)) return Path.GetDirectoryName(dll);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            string def = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"Siemens\Automation\Portal " + TiaName + @"\PublicAPI\" + TiaName);
            if (IsApiDir(def)) return def;
            if (IsApiDir(Path.Combine(def, "net48"))) return Path.Combine(def, "net48");
            return null;
        }

        // All Openness assemblies of this TIA version (one in V18, several from V21 on). AddIn assemblies are left out.
        public static List<Assembly> SiemensAssemblies()
        {
            List<Assembly> r = new List<Assembly>();
            if (ApiDir == null) return r;
            foreach (string f in Directory.GetFiles(ApiDir, "Siemens.Engineering*.dll"))
            {
                string n = Path.GetFileNameWithoutExtension(f);
                if (n.StartsWith("Siemens.Engineering.AddIn", StringComparison.OrdinalIgnoreCase)) continue;
                try { r.Add(Assembly.Load(n)); } catch { }
            }
            return r;
        }

        static Assembly ResolveSiemens(object sender, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name;
            if (ApiDir == null || !name.StartsWith("Siemens.Engineering")) return null;
            string path = Path.Combine(ApiDir, name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        }

        // ---------- log (never stdout: stdout is protocol only) ----------

        static void OpenLog()
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                _log = new StreamWriter(Path.Combine(dir, "startwise.log"), true, new UTF8Encoding(false));
                _log.AutoFlush = true;
            }
            catch { _log = null; }
        }

        public static void Log(string s)
        {
            string l = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + s;
            try { Console.Error.WriteLine(l); } catch { }
            try { if (_log != null) _log.WriteLine(l); } catch { }
        }
    }

    public static class U
    {
        public static Dictionary<string, object> Obj(params object[] kv)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        public static string Str(Dictionary<string, object> a, string key, bool required)
        {
            object v;
            if (a.TryGetValue(key, out v) && v != null && v.ToString().Trim().Length > 0) return v.ToString().Trim();
            if (required) throw new ArgumentException("Missing required argument: " + key);
            return null;
        }

        public static int? Int(Dictionary<string, object> a, string key)
        {
            object v;
            if (!a.TryGetValue(key, out v) || v == null) return null;
            return Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool Bool(Dictionary<string, object> a, string key, bool def)
        {
            object v;
            if (!a.TryGetValue(key, out v) || v == null) return def;
            if (v is bool) return (bool)v;
            return string.Equals(v.ToString(), "true", StringComparison.OrdinalIgnoreCase);
        }

        // knowledge\ folder next to bin\ : plain Markdown the engineer can edit without rebuilding.
        // Searched upwards, so bin\TiaAssistant.exe (V18) and bin\V21\TiaAssistant.exe share one knowledge folder.
        public static string KnowledgeDir()
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            for (string d = Path.GetDirectoryName(dir); d != null; d = Path.GetDirectoryName(d))
                if (Directory.Exists(Path.Combine(d, "knowledge"))) return Path.Combine(d, "knowledge");
            return Path.Combine(Path.GetDirectoryName(dir) ?? dir, "knowledge");
        }

        public static object KnowledgeList()
        {
            string dir = KnowledgeDir();
            ArrayList list = new ArrayList();
            if (Directory.Exists(dir))
            {
                string[] files = Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string f in files)
                {
                    string name = f.Substring(dir.Length).TrimStart('\\', '/').Replace('\\', '/');
                    name = name.Substring(0, name.Length - 3);
                    string title = "";
                    foreach (string line in File.ReadAllLines(f, Encoding.UTF8))
                        if (line.Trim().Length > 0) { title = line.Trim().TrimStart('#', ' '); break; }
                    list.Add(Obj("name", name, "title", title));
                }
            }
            return Obj("folder", dir, "documents", list);
        }

        public static object KnowledgeGet(string name)
        {
            string dir = KnowledgeDir();
            string rel = name.Replace('/', Path.DirectorySeparatorChar);
            if (rel.Contains("..") || Path.IsPathRooted(rel)) throw new ArgumentException("Invalid document name.");
            string path = Path.Combine(dir, rel + ".md");
            if (!File.Exists(path)) throw new FileNotFoundException("No such document: " + name + ". Call scenario_list to see what exists.");
            return File.ReadAllText(path, Encoding.UTF8);
        }

        public static string Describe(Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            int depth = 0;
            for (Exception e = ex; e != null && depth < 6; e = e.InnerException, depth++)
            {
                if (e is TargetInvocationException && e.InnerException != null) continue;
                if (sb.Length > 0) sb.Append("\n  caused by: ");
                sb.Append(e.GetType().Name).Append(": ").Append(e.Message);
            }
            string s = sb.ToString();
            if (s.Contains("EngineeringSecurityException"))
                s += "\nHint: the Windows user must be a member of the local group 'Siemens TIA Openness' (sign out/in after adding), and the Openness access prompt in TIA Portal must be accepted.";
            return s;
        }
    }
}
