using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Siemens.Engineering;
using Siemens.Engineering.HW;

namespace TiaAssistant
{
    // Self-learning layer 2: read-only live object explorer. Any Openness object describes itself:
    // attributes (GetAttributeInfos), child compositions (GetCompositionInfos), invocable actions (GetInvocationInfos),
    // creatable types (GetCreationInfos) and services (GetService<T>). Nothing is changed.
    //
    // Path: segments separated by '/'. Roots: "project" (default), "station:<device name>", "item:<device item name>" (searched in all devices).
    // Segment forms: "Devices" (composition or navigation attribute), "Devices[PLC_1_Station]" or "DeviceItems[2]" (select by Name or index),
    //                "service:NetworkInterface" (GetService of that service type).
    public static partial class Tia
    {
        static string NameOf(object o)
        {
            if (o == null) return null;
            IEngineeringObject eo = o as IEngineeringObject;
            if (eo != null) { try { object n = eo.GetAttribute("Name"); if (n != null) return n.ToString(); } catch { } }
            try { PropertyInfo p = o.GetType().GetProperty("Name"); if (p != null) { object n = p.GetValue(o, null); if (n != null) return n.ToString(); } } catch { }
            return null;
        }

        static string Describe(object v)
        {
            if (v == null) return "null";
            if (v is string) { string s = (string)v; return s.Length > 200 ? s.Substring(0, 200) + "..." : s; }
            if (v.GetType().IsPrimitive || v is Enum || v is decimal || v is DateTime || v is Version) return v.ToString();
            if (v is IEngineeringObject) { string n = NameOf(v); return "<" + v.GetType().Name + (n != null ? " '" + n + "'" : "") + ">"; }
            IEnumerable e = v as IEnumerable;
            if (e != null)
            {
                int c = 0; List<string> first = new List<string>();
                try { foreach (object x in e) { if (c < 5) first.Add(Describe(x)); c++; if (c > 1000) break; } } catch { }
                return "[" + c + (c > 1000 ? "+" : "") + "] " + string.Join(", ", first.ToArray());
            }
            string t = v.ToString();
            return t.Length > 200 ? t.Substring(0, 200) + "..." : t;
        }

        static IEnumerable<Type> _serviceTypes;
        static IEnumerable<Type> ServiceTypes()
        {
            if (_serviceTypes != null) return _serviceTypes;
            List<Type> r = new List<Type>();
            Type svc = typeof(IEngineeringService);
            // V21 splits the API over several assemblies (Base, Step7, WinCC, ...): services live in all of them,
            // e.g. motion and library services in Siemens.Engineering.Step7.
            foreach (Assembly a in Program.SiemensAssemblies())
            {
                Type[] all;
                try { all = a.GetTypes(); } catch (ReflectionTypeLoadException ex) { all = ex.Types.Where(t => t != null).ToArray(); }
                foreach (Type t in all) { try { if (t.IsPublic && !t.IsAbstract && svc.IsAssignableFrom(t) && t.IsClass) r.Add(t); } catch { } }
            }
            _serviceTypes = r;
            return r;
        }

        static object GetServiceByName(object o, string typeName)
        {
            Type st = ServiceTypes().FirstOrDefault(t => t.Name == typeName || t.FullName == typeName || t.FullName.EndsWith("." + typeName));
            if (st == null) throw new InvalidOperationException("Unknown service type '" + typeName + "'. Use api_search to find it.");
            return CallGetService(o, st);
        }

        static object CallGetService(object o, Type st)
        {
            IEngineeringServiceProvider sp = o as IEngineeringServiceProvider;
            if (sp == null) return null;
            MethodInfo gm = typeof(IEngineeringServiceProvider).GetMethod("GetService");
            try { return gm.MakeGenericMethod(st).Invoke(sp, null); } catch { return null; }
        }

        static void FindItem(DeviceItemComposition items, string name, List<DeviceItem> found)
        {
            foreach (DeviceItem i in items)
            {
                if (string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)) found.Add(i);
                FindItem(i.DeviceItems, name, found);
            }
        }

        static object Step(object cur, string seg)
        {
            string name = seg, sel = null;
            int b = seg.IndexOf('[');
            if (b > 0 && seg.EndsWith("]")) { name = seg.Substring(0, b); sel = seg.Substring(b + 1, seg.Length - b - 2); }

            object next = null;
            if (name.StartsWith("service:", StringComparison.OrdinalIgnoreCase))
            {
                next = GetServiceByName(cur, name.Substring(8));
                if (next == null) throw new InvalidOperationException("'" + Describe(cur) + "' does not provide service " + name.Substring(8) + ".");
            }
            else
            {
                IEngineeringObject eo = cur as IEngineeringObject;
                if (eo != null) { try { next = eo.GetComposition(name); } catch { } }
                if (next == null && eo != null) { try { next = eo.GetAttribute(name); } catch { } }
                if (next == null) { try { PropertyInfo p = cur.GetType().GetProperty(name); if (p != null) next = p.GetValue(cur, null); } catch { } }
                if (next == null) throw new InvalidOperationException("'" + name + "' not found on " + Describe(cur) + ". Inspect it to see its compositions and attributes.");
            }
            if (sel == null) return next;

            IEnumerable list = next as IEnumerable;
            if (list == null || next is string) throw new InvalidOperationException("'" + name + "' is not a list; cannot select [" + sel + "].");
            int idx; bool byIndex = int.TryParse(sel, out idx);
            int k = 0; List<string> names = new List<string>();
            foreach (object x in list)
            {
                string n = NameOf(x);
                if (byIndex ? k == idx : string.Equals(n, sel, StringComparison.OrdinalIgnoreCase)) return x;
                if (names.Count < 30) names.Add(n ?? ("#" + k));
                k++;
            }
            throw new InvalidOperationException("[" + sel + "] not found in '" + name + "'. Items: " + string.Join(", ", names.ToArray()));
        }


        // A typed value given as {"type": "Version", "value": "7.0"}; type may be a .NET or Openness type name, or "path" for an object path.
        static object TypedValue(object raw)
        {
            IDictionary d = raw as IDictionary;
            if (d == null || !d.Contains("type") || !d.Contains("value")) return raw;
            string tn = Convert.ToString(d["type"]); object v = d["value"];
            if (tn.Equals("path", StringComparison.OrdinalIgnoreCase)) return ResolvePath(Convert.ToString(v));
            Type t = Type.GetType(tn) ?? Type.GetType("System." + tn)
                ?? Program.SiemensAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null).ToArray(); } })
                       .FirstOrDefault(x => x.FullName == tn || x.FullName == "Siemens.Engineering." + tn || x.Name == tn);
            if (t == null) throw new ArgumentException("Unknown type '" + tn + "'.");
            return ConvertTo(v, t);
        }

        static Assembly ServiceTypesAssembly() { return typeof(IEngineeringObject).Assembly; }

        static readonly Regex VersionLike = new Regex(@"^[Vv]?\d+\.\d+(\.\d+){0,2}$");

        static object ConvertTo(object value, Type target)
        {
            if (value == null) return null;
            value = TypedValue(value);
            if (target.IsInstanceOfType(value)) return value;
            if (target == typeof(Version)) return new Version(Convert.ToString(value).TrimStart('V', 'v'));
            string sv = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            if (target == typeof(System.Drawing.Color)) return System.Drawing.ColorTranslator.FromHtml(sv);
            if (target.IsEnum) return Enum.Parse(target, sv, true);
            if (target == typeof(bool)) return sv == "1" || sv.Equals("true", StringComparison.OrdinalIgnoreCase);
            return Convert.ChangeType(sv, target, System.Globalization.CultureInfo.InvariantCulture);
        }

        // Generic guarded write: attributes are set one by one in the given order (enabling one can reveal the next,
        // e.g. ClockMemoryByte -> ClockMemoryByteAddress). Each must be ReadWrite at that moment. Values are converted to the
        // attribute's own type and read back.
        public static object SetAttributes(string path, IDictionary values)
        {
            if (values == null || values.Count == 0) throw new ArgumentException("attributes: give at least one {name: value}");
            object cur = ResolvePath(path);
            // WinCC Unified objects do not support the attribute interface (even the 'as' cast throws InvalidCastException
            // on their proxies): set .NET properties instead.
            bool unified = cur != null && cur.GetType().Namespace != null && cur.GetType().Namespace.StartsWith("Siemens.Engineering.HmiUnified");
            IEngineeringObject eo = null;
            if (!unified) { try { eo = cur as IEngineeringObject; } catch (InvalidCastException) { } }
            if (eo == null) return U.Obj("path", path, "changes", ApplyProperties(cur, values), "project_modified", Proj().IsModified, "note", "Not saved; call project_save when satisfied.");
            ArrayList done = ApplyAttributes(eo, values);
            return U.Obj("path", path, "changes", done, "project_modified", Proj().IsModified, "note", "Not saved; call project_save when satisfied.");
        }

        // Sets attributes in passes: the caller's key order is not reliable (JSON objects arrive sorted), and one attribute
        // often unlocks or changes another (ParameterSettings -> OperatingType -> OperatingRange). Each pass sets what is
        // writable now; an attribute that already has the wanted value counts as done. Stops when a pass makes no progress.
        // IDictionary.GetEnumerator yields DictionaryEntry; LINQ Cast<DictionaryEntry> would use the non-generic
        // IEnumerable of the JSON Dictionary<string, object> (KeyValuePair) and throw InvalidCastException.
        static List<DictionaryEntry> Entries(IDictionary values)
        {
            List<DictionaryEntry> l = new List<DictionaryEntry>();
            IDictionaryEnumerator en = values.GetEnumerator();
            while (en.MoveNext()) l.Add(en.Entry);
            return l;
        }

        static ArrayList ApplyAttributes(IEngineeringObject eo, IDictionary values)
        {
            ArrayList done = new ArrayList();
            if (values == null) return done;
            List<DictionaryEntry> pending = Entries(values);
            Dictionary<string, string> lastError = new Dictionary<string, string>();
            bool progress = true;
            while (pending.Count > 0 && progress)
            {
                progress = false;
                foreach (DictionaryEntry e in pending.ToList())
                {
                    string name = Convert.ToString(e.Key);
                    EngineeringAttributeInfo info = eo.GetAttributeInfos().FirstOrDefault(i => i.Name == name);
                    object old = null; try { old = eo.GetAttribute(name); } catch { }
                    Type t = old != null ? old.GetType() : (info != null && info.SupportedTypes != null ? info.SupportedTypes.FirstOrDefault() : null) ?? typeof(string);
                    object v;
                    try { v = ConvertTo(e.Value, t); } catch (Exception ex) { lastError[name] = "value: " + ex.Message; continue; }
                    if (old != null && Equals(old, v))
                    {
                        done.Add(U.Obj("attribute", name, "old", Describe(old), "new", Describe(old), "status", "already set"));
                        pending.Remove(e); progress = true; continue;
                    }
                    if (info == null) { lastError[name] = "not present on this object now"; continue; }
                    if ((info.AccessMode & EngineeringAttributeAccessMode.Write) == 0) { lastError[name] = "read-only now"; continue; }
                    try { eo.SetAttribute(name, v); }
                    catch (Exception ex) { lastError[name] = (ex.InnerException ?? ex).Message; continue; }
                    object now = null; try { now = eo.GetAttribute(name); } catch { }
                    done.Add(U.Obj("attribute", name, "old", Describe(old), "new", Describe(now), "status", "set"));
                    pending.Remove(e); progress = true;
                }
            }
            foreach (DictionaryEntry e in pending)
            {
                string name = Convert.ToString(e.Key);
                string[] writable = eo.GetAttributeInfos().Where(i => (i.AccessMode & EngineeringAttributeAccessMode.Write) != 0).Select(i => i.Name).ToArray();
                done.Add(U.Obj("attribute", name, "status", "not set", "reason", lastError.ContainsKey(name) ? lastError[name] : "unknown",
                    "writable_now", writable.Where(w => w.IndexOf(name.Length > 6 ? name.Substring(0, 6) : name, StringComparison.OrdinalIgnoreCase) >= 0).ToArray()));
            }
            return done;
        }

        // Same idea for objects without the attribute interface (WinCC Unified): public .NET properties, in passes.
        static ArrayList ApplyProperties(object o, IDictionary values)
        {
            if (o == null) throw new InvalidOperationException("No object at this path.");
            ArrayList done = new ArrayList();
            List<DictionaryEntry> pending = Entries(values);
            Dictionary<string, string> lastError = new Dictionary<string, string>();
            bool progress = true;
            while (pending.Count > 0 && progress)
            {
                progress = false;
                foreach (DictionaryEntry e in pending.ToList())
                {
                    string name = Convert.ToString(e.Key);
                    System.Reflection.PropertyInfo pi = o.GetType().GetProperty(name);
                    if (pi == null || !pi.CanWrite || pi.GetSetMethod() == null) { lastError[name] = pi == null ? "no such property" : "read-only"; continue; }
                    object old = null; try { old = pi.GetValue(o, null); } catch { }
                    try
                    {
                        object v = ConvertTo(e.Value, pi.PropertyType == typeof(object) && old != null ? old.GetType() : pi.PropertyType);
                        pi.SetValue(o, v, null);
                    }
                    catch (Exception ex) { lastError[name] = (ex.InnerException ?? ex).Message; continue; }
                    object now = null; try { now = pi.GetValue(o, null); } catch { }
                    done.Add(U.Obj("property", name, "old", Describe(old), "new", Describe(now), "status", "set"));
                    pending.Remove(e); progress = true;
                }
            }
            foreach (DictionaryEntry e in pending)
            {
                string name = Convert.ToString(e.Key);
                done.Add(U.Obj("property", name, "status", "not set", "reason", lastError.ContainsKey(name) ? lastError[name] : "unknown",
                    "writable", o.GetType().GetProperties().Where(p => p.CanWrite && p.GetSetMethod() != null).Select(p => p.Name).ToArray()));
            }
            return done;
        }

        static string InvocationText(EngineeringInvocationInfo ii)
        {
            return ii.Name + "(" + string.Join(", ", ii.ParameterInfos.Select(p => p.Type.Name + " " + p.Name).ToArray()) + ")";
        }

        static string CreationText(EngineeringCreationInfo ci)
        {
            return ci.Type.Name + "(" + string.Join(", ", ci.ParameterInfos.Select(p => p.Name + (p.IsMandatory ? "" : "?")).ToArray()) + ")";
        }

        // Online / irreversible actions. Online ones are refused (StartWise is offline-only); destructive ones need confirm=true.
        static readonly string[] OnlineWords = { "Online", "Download", "Upload", "Flash", "GoOnline" };
        static readonly string[] DestructiveWords = { "Delete", "Remove", "Disconnect", "Clear", "Reset", "Unplug", "Detach", "Overwrite", "Restore", "Retrieve" };

        static object ArgValue(object raw, Type t)
        {
            if (raw == null) return null;
            if (raw is string && !t.IsPrimitive && !t.IsEnum && t != typeof(string) && !t.IsValueType && t != typeof(object))
            {
                if (t == typeof(System.IO.FileInfo)) return new System.IO.FileInfo((string)raw);
                if (t == typeof(System.IO.DirectoryInfo)) return new System.IO.DirectoryInfo((string)raw);
                return ResolvePath((string)raw);     // engineering object given by its object_inspect path
            }
            return ConvertTo(raw, t);
        }

        public static object Invoke(string path, string action, IDictionary args, bool confirm)
        {
            if (string.IsNullOrEmpty(action)) throw new ArgumentException("action is required");
            foreach (string w in OnlineWords)
                if (action.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new InvalidOperationException("'" + action + "' is an online action; StartWise works offline only. Nothing was done.");
            bool destructive = DestructiveWords.Any(w => action.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
            object cur = ResolvePath(path);
            IEngineeringObject eo = cur as IEngineeringObject; IEngineeringComposition comp = cur as IEngineeringComposition;
            IList<EngineeringInvocationInfo> infos = eo != null ? eo.GetInvocationInfos() : comp != null ? comp.GetInvocationInfos() : null;
            List<string> keys = new List<string>();
            if (args != null) foreach (DictionaryEntry e in args) keys.Add(Convert.ToString(e.Key));
            List<EngineeringInvocationInfo> cands = infos == null ? new List<EngineeringInvocationInfo>()
                : infos.Where(i => string.Equals(i.Name, action, StringComparison.OrdinalIgnoreCase)).ToList();
            if (cands.Count == 0)
            {
                // Objects outside the generic invocation API (e.g. association lists like a user's Roles) : public .NET method.
                if (cur == null) throw new InvalidOperationException("Nothing at this path.");
                // Generic methods (e.g. Connections.Create<HmiConnection>(...)) take the type argument as "_T": an Openness type name.
                Type genArg = null;
                if (keys.Contains("_T"))
                {
                    string tn = Convert.ToString(args["_T"]);
                    genArg = Program.SiemensAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null).ToArray(); } })
                        .FirstOrDefault(x => x.FullName == tn || x.FullName == "Siemens.Engineering." + tn || x.Name == tn);
                    if (genArg == null) throw new ArgumentException("Unknown type '" + tn + "' for _T.");
                    keys.Remove("_T");
                }
                MethodInfo[] ms = cur.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => string.Equals(m.Name, action, StringComparison.OrdinalIgnoreCase)
                        && (genArg == null ? !m.IsGenericMethod : m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1))
                    .Select(m => genArg == null ? m : m.MakeGenericMethod(genArg)).ToArray();
                MethodInfo mi = ms.FirstOrDefault(m => m.GetParameters().Length == keys.Count && m.GetParameters().All(pp => keys.Contains(pp.Name)));
                if (mi == null)
                {
                    string avail = infos != null && infos.Count > 0 ? string.Join("; ", infos.Select(InvocationText).ToArray())
                        : string.Join("; ", cur.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
                            .Select(m => m.Name + (m.IsGenericMethodDefinition ? "<_T>" : "") + "(" + string.Join(", ", m.GetParameters().Select(pp => pp.ParameterType.Name + " " + pp.Name).ToArray()) + ")").Distinct().ToArray());
                    throw new InvalidOperationException("No action '" + action + "' with these arguments here. Available: " + avail);
                }
                if (mi.GetParameters().Any(pp => pp.ParameterType == typeof(System.Security.SecureString)))
                    throw new InvalidOperationException("Passwords are never set by StartWise; set them in TIA Portal.");
                string pv = mi.Name + "(" + string.Join(", ", mi.GetParameters().Select(pp => pp.ParameterType.Name + " " + pp.Name).ToArray()) + ") on " + Describe(cur);
                if (destructive && !confirm)
                    return U.Obj("needs_confirmation", true, "would_call", pv,
                        "note", "This action changes or removes existing content. Ask the engineer, then call again with confirm=true. Nothing was done.");
                object[] av = mi.GetParameters().Select(pp => ArgValue(args[pp.Name], pp.ParameterType)).ToArray();
                object res;
                try { res = mi.Invoke(cur, av); }
                catch (TargetInvocationException tie) { throw tie.InnerException ?? tie; }
                return U.Obj("called", pv, "result", Describe(res), "result_type", res == null ? null : res.GetType().Name,
                    "project_modified", Proj().IsModified, "note", "Not saved; call project_save when satisfied.");
            }
            EngineeringInvocationInfo info = cands.FirstOrDefault(i => i.ParameterInfos.Count == keys.Count && i.ParameterInfos.All(p => keys.Contains(p.Name)));
            if (info == null)
                throw new InvalidOperationException("Arguments do not match. Overloads: " + string.Join("; ", cands.Select(InvocationText).ToArray()));
            string preview = InvocationText(info) + " on " + Describe(cur);
            if (destructive && !confirm)
                return U.Obj("needs_confirmation", true, "would_call", preview,
                    "note", "This action changes or removes existing content. Ask the engineer, then call again with confirm=true. Nothing was done.");
            List<KeyValuePair<Type, object>> ps = new List<KeyValuePair<Type, object>>();
            foreach (EngineeringInvocationParameterInfo p in info.ParameterInfos)
                ps.Add(new KeyValuePair<Type, object>(p.Type, ArgValue(args[p.Name], p.Type)));
            object result = eo != null ? eo.Invoke(info.Name, ps) : comp.Invoke(info.Name, ps);
            return U.Obj("called", preview, "result", Describe(result), "result_type", result == null ? null : result.GetType().Name,
                "project_modified", Proj().IsModified, "note", "Not saved; call project_save when satisfied.");
        }

        public static object Create(string path, string type, IDictionary parameters, IDictionary attributes)
        {
            object cur = ResolvePath(path);
            IEngineeringComposition comp = cur as IEngineeringComposition;
            if (comp == null) throw new InvalidOperationException("Path must point to a list (composition), e.g. .../TagTables. Inspect it to see 'creatable'.");
            IList<EngineeringCreationInfo> infos = comp.GetCreationInfos();
            if (infos.Count == 0) throw new InvalidOperationException("Nothing can be created in this list through the generic API.");
            List<EngineeringCreationInfo> cands = infos.Where(c => string.IsNullOrEmpty(type) || c.Type.Name == type || c.Type.FullName == type || c.Type.FullName.EndsWith("." + type)).ToList();
            List<string> keys = new List<string>();
            if (parameters != null) foreach (DictionaryEntry e in parameters) keys.Add(Convert.ToString(e.Key));
            EngineeringCreationInfo info = cands.FirstOrDefault(c => c.ParameterInfos.Where(p => p.IsMandatory).All(p => keys.Contains(p.Name)) && keys.All(k => c.ParameterInfos.Any(p => p.Name == k)));
            if (info == null)
                throw new InvalidOperationException("No matching way to create. Options: " + string.Join("; ", infos.Select(CreationText).ToArray()) + " ('?' = optional).");
            List<KeyValuePair<string, object>> ps = new List<KeyValuePair<string, object>>();
            if (parameters != null)
                foreach (DictionaryEntry e in parameters)
                {
                    object v = TypedValue(e.Value);
                    if (v is string && VersionLike.IsMatch((string)v) && Convert.ToString(e.Key).IndexOf("Version", StringComparison.OrdinalIgnoreCase) >= 0)
                        v = new Version(((string)v).TrimStart('V', 'v'));    // creation params carry no type info; versions are System.Version
                    ps.Add(new KeyValuePair<string, object>(Convert.ToString(e.Key), v));
                }
            IEngineeringObject created = comp.Create(info.Type, ps);
            ArrayList attrs = ApplyAttributes(created, attributes);
            return U.Obj("created", Describe(created), "type", created.GetType().Name, "name", NameOf(created), "attributes", attrs,
                "project_modified", Proj().IsModified, "note", "Not saved; call project_save when satisfied.");
        }

        // '/' separates segments, except inside [...] (names like "PN/IE_1").
        static List<string> SplitPath(string path)
        {
            List<string> r = new List<string>(); StringBuilder cur = new StringBuilder(); int depth = 0;
            foreach (char c in path ?? "")
            {
                if (c == '[') depth++; else if (c == ']' && depth > 0) depth--;
                if (c == '/' && depth == 0) { if (cur.ToString().Trim().Length > 0) r.Add(cur.ToString().Trim()); cur.Length = 0; }
                else cur.Append(c);
            }
            if (cur.ToString().Trim().Length > 0) r.Add(cur.ToString().Trim());
            return r;
        }

        static object ResolvePath(string path)
        {
            object cur = Proj();
            List<string> segs = SplitPath(path);
            if (segs.Count > 0 && segs[0].Equals("project", StringComparison.OrdinalIgnoreCase)) segs.RemoveAt(0);
            else if (segs.Count > 0 && segs[0].StartsWith("station:", StringComparison.OrdinalIgnoreCase))
            {
                // Station names may contain '/' (TIA default "S7-1500/ET200MP station_1"): take the longest prefix that names a station.
                int take = 1;
                for (int k = segs.Count; k > 1; k--)
                {
                    string cand = string.Join("/", segs.Take(k).ToArray()).Substring(8);
                    if (AllDevices().Any(d => string.Equals(d.Name, cand, StringComparison.OrdinalIgnoreCase))) { take = k; break; }
                }
                cur = FindDevice(string.Join("/", segs.Take(take).ToArray()).Substring(8)); segs.RemoveRange(0, take);
            }
            else if (segs.Count > 0 && segs[0].StartsWith("item:", StringComparison.OrdinalIgnoreCase))
            {
                string n = segs[0].Substring(5); List<DeviceItem> found = new List<DeviceItem>();
                foreach (Device d in AllDevices()) FindItem(d.DeviceItems, n, found);
                if (found.Count == 0) throw new InvalidOperationException("No device item named '" + n + "'.");
                if (found.Count > 1)
                {
                    List<DeviceItem> exact = found.Where(x => x.Name == n).ToList();   // names differ only in case: "PROFINET interface_1" vs "PROFINET Interface_1"
                    if (exact.Count == 1) found = exact;
                    else throw new InvalidOperationException("'" + n + "' is ambiguous (" + found.Count + " items). Use station:<name>/DeviceItems[...] instead.");
                }
                cur = found[0]; segs.RemoveAt(0);
            }
            foreach (string s in segs) cur = Step(cur, s);
            return cur;
        }

        public static object Inspect(string path, string filter, bool values, bool services)
        {
            object cur = ResolvePath(path);

            Dictionary<string, object> r = U.Obj("path", path ?? "project", "type", cur.GetType().FullName.Replace("Siemens.Engineering.", ""), "name", NameOf(cur));
            Func<string, bool> pass = n => string.IsNullOrEmpty(filter) || n.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

            // A list: show its items.
            if (!(cur is IEngineeringObject) && cur is IEnumerable && !(cur is string))
            {
                ArrayList items = new ArrayList(); int k = 0;
                foreach (object x in (IEnumerable)cur)
                {
                    if (k >= 100) { r["truncated"] = true; break; }
                    string n = NameOf(x);
                    if (n == null || pass(n)) items.Add(U.Obj("index", k, "name", n, "type", x == null ? null : x.GetType().Name));
                    k++;
                }
                r["items"] = items;
                IEngineeringComposition comp = cur as IEngineeringComposition;
                if (comp != null)
                {
                    try { r["creatable"] = comp.GetCreationInfos().Select(CreationText).ToArray(); } catch { }
                    try { r["actions"] = comp.GetInvocationInfos().Select(InvocationText).ToArray(); } catch { }
                }
                return r;
            }

            IEngineeringObject eo = cur as IEngineeringObject;
            if (eo != null)
            {
                ArrayList attrs = new ArrayList();
                IList<EngineeringAttributeInfo> infos = null;
                try { infos = eo.GetAttributeInfos(); } catch (Exception e) { r["attributes_error"] = e.Message; }
                if (infos != null)
                    foreach (EngineeringAttributeInfo ai in infos.Where(i => pass(i.Name)).OrderBy(i => i.Name))
                    {
                        Dictionary<string, object> a = U.Obj("name", ai.Name, "access", ai.AccessMode.ToString());
                        if (values && (ai.AccessMode & EngineeringAttributeAccessMode.Read) != 0)
                        {
                            try { object v = eo.GetAttribute(ai.Name); a["value"] = Describe(v); if (v != null) a["type"] = v.GetType().Name; }
                            catch (Exception e) { a["error"] = e.Message.Length > 120 ? e.Message.Substring(0, 120) : e.Message; }
                        }
                        attrs.Add(a);
                    }
                r["attributes"] = attrs;

                ArrayList comps = new ArrayList();
                try
                {
                    foreach (EngineeringCompositionInfo ci in eo.GetCompositionInfos().Where(c => pass(c.Name)))
                    {
                        string count = null;
                        try { count = Describe(eo.GetComposition(ci.Name)); } catch { }
                        comps.Add(U.Obj("name", ci.Name, "content", count));
                    }
                }
                catch { }
                r["compositions"] = comps;

                ArrayList acts = new ArrayList();
                try
                {
                    foreach (EngineeringInvocationInfo ii in eo.GetInvocationInfos().Where(i => pass(i.Name)))
                        acts.Add(InvocationText(ii));
                }
                catch { }
                r["actions"] = acts;
            }

            if (services && cur is IEngineeringServiceProvider)
            {
                ArrayList svcs = new ArrayList();
                foreach (Type st in ServiceTypes())
                    if (CallGetService(cur, st) != null) svcs.Add(st.FullName.Replace("Siemens.Engineering.", ""));
                r["services"] = svcs;
            }
            r["hint"] = "Go deeper with path + '/<Composition>[<Name or index>]' or '/service:<Type>'. Read-only; nothing was changed.";
            return r;
        }
    }
}
