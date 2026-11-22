using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Communication;
using Siemens.Engineering.Hmi.Cycle;
using Siemens.Engineering.Hmi.Screen;
using Siemens.Engineering.Hmi.Tag;
using Siemens.Engineering.Hmi.TextGraphicList;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.TechnologicalObjects;
using Siemens.Engineering.SW.TechnologicalObjects.Motion;

namespace TiaAssistant
{
    // Read-mostly tools for learning from existing projects: compact inventory, technology objects, HMI.
    public static partial class Tia
    {
        // ---------------- compact project summary ----------------

        static string ShortType(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            if (t.StartsWith("OrderNumber:")) return t.Substring(12);
            if (t.StartsWith("System:")) return t.Substring(7);
            if (t.StartsWith("GSD:")) { int i = t.IndexOf(".XML"); return "GSD " + (i > 0 ? t.Substring(4, i - 4) : t.Substring(4)); }
            return t;
        }

        public static object ProjectSummary(string filter)
        {
            Proj();
            ArrayList devs = new ArrayList();
            int total = 0;
            foreach (Device d in AllDevices())
            {
                total++;
                DeviceItem head = null;
                foreach (DeviceItem i in d.DeviceItems)
                    if (i.Classification != DeviceItemClassifications.None || (head == null && (i.TypeIdentifier ?? "").StartsWith("OrderNumber"))) { head = i; if (i.Classification != DeviceItemClassifications.None) break; }
                string headName = head == null ? "" : head.Name;
                string type = ShortType(head == null ? d.TypeIdentifier : head.TypeIdentifier);
                if (!string.IsNullOrEmpty(filter) && d.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && headName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 && type.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                Dictionary<string, object> dd = U.Obj("station", d.Name, "head", headName, "type", type, "modules", d.DeviceItems.Count);
                ArrayList nets = new ArrayList();
                foreach (Dictionary<string, object> n in NetworkInfo(d))
                {
                    if (n["subnet"] == null) continue;
                    object adr = n.ContainsKey("ip") ? n["ip"] : n["address"];
                    object sys = n.ContainsKey("io_device_in") ? n["io_device_in"] : (n.ContainsKey("io_controller_of") ? n["io_controller_of"] : null);
                    nets.Add(n["subnet"] + " " + adr + (sys == null ? "" : " [" + sys + "]"));
                }
                if (nets.Count > 0) dd["net"] = nets;
                PlcSoftware plc = FindPlcSoftware(d.DeviceItems);
                if (plc != null)
                {
                    ArrayList blocks = new ArrayList();
                    CollectBlocks(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, "", blocks);
                    Dictionary<string, int> kinds = new Dictionary<string, int>();
                    foreach (Dictionary<string, object> b in blocks)
                    {
                        string k = b["kind"] + "/" + b["language"];
                        kinds[k] = kinds.ContainsKey(k) ? kinds[k] + 1 : 1;
                    }
                    ArrayList tables = new ArrayList();
                    CollectTagTables(plc.TagTableGroup.TagTables, plc.TagTableGroup.Groups, tables);
                    List<TechnologicalInstanceDB> tos = new List<TechnologicalInstanceDB>();
                    CollectTos(plc.TechnologicalObjectGroup, tos);
                    dd["plc"] = U.Obj("name", plc.Name, "blocks", kinds, "tag_tables", tables, "technology_objects", tos.Count);
                }
                HmiTarget hmi = FindHmiTarget(d.DeviceItems);
                if (hmi != null) dd["hmi"] = hmi.Name;
                devs.Add(dd);
            }
            return U.Obj("project", ProjectInfo(), "stations_total", total, "stations", devs);
        }

        // ---------------- technology objects ----------------

        static void CollectTos(IEngineeringObject group, List<TechnologicalInstanceDB> list)
        {
            try
            {
                IEnumerable tos = group.GetComposition("TechnologicalObjects") as IEnumerable;
                if (tos != null) foreach (object o in tos) { TechnologicalInstanceDB t = o as TechnologicalInstanceDB; if (t != null) list.Add(t); }
            }
            catch { }
            try
            {
                IEnumerable groups = group.GetComposition("Groups") as IEnumerable;
                if (groups != null) foreach (object g in groups) { IEngineeringObject eg = g as IEngineeringObject; if (eg != null) CollectTos(eg, list); }
            }
            catch { }
        }

        static string ItemPath(object o)
        {
            List<string> parts = new List<string>();
            IEngineeringObject cur = o as IEngineeringObject;
            int guard = 0;
            while (cur != null && guard++ < 12)
            {
                DeviceItem di = cur as DeviceItem; Device dv = cur as Device;
                if (di != null) parts.Insert(0, di.Name);
                else if (dv != null) { parts.Insert(0, dv.Name); break; }
                cur = cur.Parent;
            }
            return string.Join(" / ", parts.ToArray());
        }

        static object Prop(object o, string name)
        {
            if (o == null) return null;
            try
            {
                PropertyInfo p = o.GetType().GetProperty(name);
                return p == null ? null : p.GetValue(o, null);
            }
            catch { return null; }
        }

        static Dictionary<string, object> ConnInfo(object itf)
        {
            if (itf == null) return null;
            Dictionary<string, object> d = U.Obj("connected", Plain(Prop(itf, "IsConnected")));
            object m = Prop(itf, "InputOutputModule"); if (m != null) d["module"] = ItemPath(m);
            m = Prop(itf, "InputModule"); if (m != null) d["input_module"] = ItemPath(m);
            m = Prop(itf, "OutputModule"); if (m != null) d["output_module"] = ItemPath(m);
            object v = Prop(itf, "InputAddress"); if (v != null) d["input_bit_address"] = Plain(v);
            v = Prop(itf, "OutputAddress"); if (v != null) d["output_bit_address"] = Plain(v);
            v = Prop(itf, "PathToDBMember"); if (v != null && v.ToString().Length > 0) d["db_member"] = v.ToString();
            v = Prop(itf, "SensorIndexInActorTelegram"); if (v != null) d["sensor_in_actor_telegram"] = Plain(v);
            return d;
        }

        static Dictionary<string, object> ToInfo(TechnologicalInstanceDB t, bool connection)
        {
            Dictionary<string, object> d = U.Obj("name", t.Name, "type", t.OfSystemLibElement,
                "version", t.OfSystemLibVersion == null ? null : t.OfSystemLibVersion.ToString());
            if (!connection) return d;
            try
            {
                AxisHardwareConnectionProvider p = t.GetService<AxisHardwareConnectionProvider>();
                if (p != null)
                {
                    d["actor"] = ConnInfo(p.ActorInterface);
                    ArrayList sensors = new ArrayList();
                    IEnumerable si = p.SensorInterface as IEnumerable;
                    if (si != null) foreach (object s in si) { Dictionary<string, object> ci = ConnInfo(s); if (ci != null && !false.Equals(ci["connected"])) sensors.Add(ci); }
                    if (sensors.Count > 0) d["sensors"] = sensors;
                }
            }
            catch (Exception ex) { d["connection_error"] = ex.Message; }
            return d;
        }

        public static object ToList(string deviceName)
        {
            PlcSoftware plc = PlcOf(deviceName);
            List<TechnologicalInstanceDB> tos = new List<TechnologicalInstanceDB>();
            CollectTos(plc.TechnologicalObjectGroup, tos);
            ArrayList list = new ArrayList();
            foreach (TechnologicalInstanceDB t in tos) list.Add(ToInfo(t, true));
            return U.Obj("plc", plc.Name, "count", list.Count, "technology_objects", list);
        }

        public static object ToGet(string deviceName, string name, string filter, int limit)
        {
            PlcSoftware plc = PlcOf(deviceName);
            List<TechnologicalInstanceDB> tos = new List<TechnologicalInstanceDB>();
            CollectTos(plc.TechnologicalObjectGroup, tos);
            TechnologicalInstanceDB to = null;
            foreach (TechnologicalInstanceDB t in tos) if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) to = t;
            if (to == null) throw new InvalidOperationException("Technology object '" + name + "' not found in '" + plc.Name + "'.");
            Dictionary<string, object> r = ToInfo(to, true);
            ArrayList ps = new ArrayList();
            int total = 0, matched = 0;
            foreach (TechnologicalParameter p in to.Parameters)
            {
                total++;
                if (!string.IsNullOrEmpty(filter) && p.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                matched++;
                if (ps.Count >= limit) continue;
                object v; try { v = Plain(p.Value); } catch (Exception ex) { v = "error: " + ex.Message; }
                ps.Add(U.Obj("name", p.Name, "value", v));
            }
            r["parameters_total"] = total; r["parameters_matched"] = matched; r["parameters"] = ps;
            return r;
        }

        // ---------------- HMI ----------------

        static HmiTarget FindHmiTarget(DeviceItemComposition items)
        {
            foreach (DeviceItem i in items)
            {
                SoftwareContainer sc = i.GetService<SoftwareContainer>();
                if (sc != null && sc.Software is HmiTarget) return (HmiTarget)sc.Software;
                HmiTarget r = FindHmiTarget(i.DeviceItems);
                if (r != null) return r;
            }
            return null;
        }

        static HmiTarget HmiOf(string deviceName)
        {
            Device d = FindDevice(deviceName);
            HmiTarget h = FindHmiTarget(d.DeviceItems);
            if (h == null) throw new InvalidOperationException("Device '" + d.Name + "' has no HMI software (it is not an HMI panel or WinCC station).");
            return h;
        }

        static void CollectHmiTagTables(TagFolder f, string path, List<KeyValuePair<string, TagTable>> list)
        {
            foreach (TagTable t in f.TagTables) list.Add(new KeyValuePair<string, TagTable>(path, t));
            foreach (TagUserFolder sub in f.Folders) CollectHmiTagTables(sub, path + "/" + sub.Name, list);
        }

        static void CollectScreens(ScreenFolder f, string path, List<KeyValuePair<string, Screen>> list)
        {
            foreach (Screen s in f.Screens) list.Add(new KeyValuePair<string, Screen>(path, s));
            foreach (ScreenUserFolder sub in f.Folders) CollectScreens(sub, path + "/" + sub.Name, list);
        }

        static void CollectTemplates(ScreenTemplateFolder f, List<ScreenTemplate> list)
        {
            foreach (ScreenTemplate s in f.ScreenTemplates) list.Add(s);
            foreach (ScreenTemplateUserFolder sub in f.Folders) CollectTemplates(sub, list);
        }

        public static object HmiTree(string deviceName)
        {
            HmiTarget h = HmiOf(deviceName);
            ArrayList conns = new ArrayList(); foreach (Connection c in h.Connections) conns.Add(c.Name);
            List<KeyValuePair<string, TagTable>> tts = new List<KeyValuePair<string, TagTable>>();
            CollectHmiTagTables(h.TagFolder, "", tts);
            ArrayList tables = new ArrayList();
            foreach (KeyValuePair<string, TagTable> kv in tts) tables.Add(U.Obj("name", kv.Value.Name, "folder", kv.Key, "tags", kv.Value.Tags.Count));
            List<KeyValuePair<string, Screen>> scr = new List<KeyValuePair<string, Screen>>();
            CollectScreens(h.ScreenFolder, "", scr);
            ArrayList screens = new ArrayList();
            foreach (KeyValuePair<string, Screen> kv in scr) screens.Add(kv.Key.Length > 0 ? kv.Key + "/" + kv.Value.Name : kv.Value.Name);
            List<ScreenTemplate> tpl = new List<ScreenTemplate>(); CollectTemplates(h.ScreenTemplateFolder, tpl);
            ArrayList templates = new ArrayList(); foreach (ScreenTemplate t in tpl) templates.Add(t.Name);
            ArrayList texts = new ArrayList(); foreach (TextList t in h.TextLists) texts.Add(t.Name);
            ArrayList graphics = new ArrayList(); foreach (GraphicList g in h.GraphicLists) graphics.Add(g.Name);
            ArrayList cycles = new ArrayList(); foreach (Cycle c in h.Cycles) cycles.Add(c.Name);
            return U.Obj("hmi", h.Name, "connections", conns, "tag_tables", tables, "screens", screens, "screen_templates", templates,
                "text_lists", texts, "graphic_lists", graphics, "cycles", cycles);
        }

        static string ExportToText(Action<FileInfo> export)
        {
            string file = Path.Combine(Path.GetTempPath(), "tia_assistant_" + Guid.NewGuid().ToString("N") + ".xml");
            try { export(new FileInfo(file)); return File.ReadAllText(file, Encoding.UTF8); }
            finally { try { File.Delete(file); } catch { } }
        }

        public static object HmiExport(string deviceName, string kind, string name)
        {
            HmiTarget h = HmiOf(deviceName);
            ExportOptions opt = ExportOptions.WithDefaults;
            switch ((kind ?? "").ToLowerInvariant())
            {
                case "tag_table":
                    {
                        List<KeyValuePair<string, TagTable>> tts = new List<KeyValuePair<string, TagTable>>();
                        CollectHmiTagTables(h.TagFolder, "", tts);
                        foreach (KeyValuePair<string, TagTable> kv in tts)
                            if (string.Equals(kv.Value.Name, name, StringComparison.OrdinalIgnoreCase)) { TagTable t = kv.Value; return ExportToText(delegate(FileInfo f) { t.Export(f, opt); }); }
                        break;
                    }
                case "connection":
                    { Connection c = h.Connections.Find(name); if (c != null) return ExportToText(delegate(FileInfo f) { c.Export(f, opt); }); break; }
                case "screen":
                    {
                        List<KeyValuePair<string, Screen>> scr = new List<KeyValuePair<string, Screen>>();
                        CollectScreens(h.ScreenFolder, "", scr);
                        foreach (KeyValuePair<string, Screen> kv in scr)
                            if (string.Equals(kv.Value.Name, name, StringComparison.OrdinalIgnoreCase)) { Screen s = kv.Value; return ExportToText(delegate(FileInfo f) { s.Export(f, opt); }); }
                        break;
                    }
                case "screen_template":
                    {
                        List<ScreenTemplate> tpl = new List<ScreenTemplate>(); CollectTemplates(h.ScreenTemplateFolder, tpl);
                        foreach (ScreenTemplate t0 in tpl)
                            if (string.Equals(t0.Name, name, StringComparison.OrdinalIgnoreCase)) { ScreenTemplate t = t0; return ExportToText(delegate(FileInfo f) { t.Export(f, opt); }); }
                        break;
                    }
                case "text_list":
                    { TextList t = h.TextLists.Find(name); if (t != null) return ExportToText(delegate(FileInfo f) { t.Export(f, opt); }); break; }
                case "cycle":
                    { Cycle c = h.Cycles.Find(name); if (c != null) return ExportToText(delegate(FileInfo f) { c.Export(f, opt); }); break; }
                default:
                    throw new ArgumentException("Unknown kind '" + kind + "'. Use tag_table, connection, screen, screen_template, text_list or cycle.");
            }
            throw new InvalidOperationException("No " + kind + " named '" + name + "' in '" + h.Name + "'. Call hmi_tree to see what exists.");
        }

        // A failed HMI tag import can crash TIA Portal, so every PLC reference is checked first.
        static List<string> MissingControllerTags(string xml)
        {
            List<PlcSoftware> plcs = new List<PlcSoftware>();
            foreach (Device d in AllDevices()) { PlcSoftware p = FindPlcSoftware(d.DeviceItems); if (p != null) plcs.Add(p); }
            List<string> missing = new List<string>();
            System.Text.RegularExpressions.MatchCollection mc = System.Text.RegularExpressions.Regex.Matches(xml,
                @"<ControllerTag\b[^>]*>\s*<Name>([^<]+)</Name>");
            foreach (System.Text.RegularExpressions.Match m in mc)
            {
                string full = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
                string first = full;
                if (first.StartsWith("\"")) { int e = first.IndexOf('"', 1); first = e > 0 ? first.Substring(1, e - 1) : first.Trim('"'); }
                else { int dot = first.IndexOf('.'); if (dot > 0) first = first.Substring(0, dot); }
                bool member = full.Length > first.Length + (full.StartsWith("\"") ? 2 : 0);
                bool found = false;
                foreach (PlcSoftware plc in plcs)
                {
                    if (!member && FindTag(plc.TagTableGroup.TagTables, plc.TagTableGroup.Groups, first) != null) { found = true; break; }
                    if (FindBlock(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, first) != null) { found = true; break; }
                }
                if (!found && !missing.Contains(full)) missing.Add(full);
            }
            return missing;
        }

        // Basic panels reject some HMI data types (e.g. Word) and TIA Portal crashes on the failed import.
        static readonly string[] HmiCoreTypes = { "Bool", "SInt", "USInt", "Int", "UInt", "DInt", "UDInt", "Real", "String" };
        static readonly string[] HmiWideTypes = { "Byte", "Word", "DWord", "LReal", "Char", "WChar", "WString", "Time", "Date", "Time_Of_Day", "DateTime", "DTL", "LInt", "ULInt", "LWord" };

        static void CollectTypeIds(DeviceItemComposition items, StringBuilder sb)
        { foreach (DeviceItem i in items) { sb.Append(i.TypeIdentifier).Append(' '); CollectTypeIds(i.DeviceItems, sb); } }

        static bool IsBasicPanel(Device d)
        {
            StringBuilder sb = new StringBuilder(d.TypeIdentifier ?? ""); CollectTypeIds(d.DeviceItems, sb);
            string t = sb.ToString().Replace(" ", "").ToUpperInvariant();
            return t.Contains("6AV2123") || t.Contains("6AV6647") || t.Contains("6AV6648");
        }

        static List<string> HmiTypesIn(string xml)
        {
            List<string> r = new List<string>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(xml, @"<HmiDataType\b[^>]*>\s*<Name>([^<]+)</Name>"))
            { string n = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim(); if (!r.Contains(n)) r.Add(n); }
            return r;
        }

        static void CheckHmiDataTypes(Device d, HmiTarget h, string xml)
        {
            bool basic = IsBasicPanel(d);
            List<string> unknown = new List<string>();
            foreach (string t in HmiTypesIn(xml))
                if (Array.IndexOf(HmiCoreTypes, t) < 0 && (basic || Array.IndexOf(HmiWideTypes, t) < 0)) unknown.Add(t);
            if (unknown.Count == 0) return;
            // Types already used by existing tags of this panel are known to be accepted.
            List<KeyValuePair<string, TagTable>> tables = new List<KeyValuePair<string, TagTable>>();
            CollectHmiTagTables(h.TagFolder, "", tables);
            foreach (KeyValuePair<string, TagTable> kv in tables)
            {
                if (unknown.Count == 0) break;
                string ex = ExportToText(delegate(FileInfo f) { kv.Value.Export(f, ExportOptions.None); });
                foreach (string t in HmiTypesIn(ex)) unknown.Remove(t);
            }
            if (unknown.Count == 0) return;
            throw new InvalidOperationException("Not imported: panel '" + d.Name + "'" + (basic ? " (Basic panel)" : "") +
                " is not known to accept these HMI data types: " + string.Join(", ", unknown.ToArray()) +
                ". Use Word->UInt, DWord->UDInt, Byte->USInt, LReal->Real (keep the PLC DataType as it is). Nothing was changed.");
        }

        public static object HmiImport(string deviceName, string kind, string xml, bool overwrite, bool saveFirst)
        {
            HmiTarget h = HmiOf(deviceName);
            if (string.Equals(kind, "tag_table", StringComparison.OrdinalIgnoreCase)) CheckHmiDataTypes(FindDevice(deviceName), h, xml);
            if (string.Equals(kind, "tag_table", StringComparison.OrdinalIgnoreCase))
            {
                List<string> missing = MissingControllerTags(xml);
                if (missing.Count > 0)
                    throw new InvalidOperationException("Not imported: these PLC tags / DBs do not exist in any PLC of the project: " +
                        string.Join(", ", missing.ToArray()) + ". Create them first (tags_add / scl_import). Nothing was changed.");
            }
            if (string.Equals(kind, "tag_table", StringComparison.OrdinalIgnoreCase))
            {
                // Symbolic tags only work over the integrated HMI connection (created in the network view).
                // Integrated connections do not appear in h.Connections; the ones that do appear are non-integrated,
                // and a symbolic tag on them makes TIA Portal crash during import.
                List<string> bad = new List<string>();
                System.Text.RegularExpressions.MatchCollection tags = System.Text.RegularExpressions.Regex.Matches(xml,
                    @"<Hmi\.Tag\.Tag\b.*?</Hmi\.Tag\.Tag>", System.Text.RegularExpressions.RegexOptions.Singleline);
                foreach (System.Text.RegularExpressions.Match t in tags)
                {
                    if (t.Value.IndexOf("<AddressAccessMode>Absolute", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    System.Text.RegularExpressions.Match c = System.Text.RegularExpressions.Regex.Match(t.Value, @"<Connection\b[^>]*>\s*<Name>([^<]+)</Name>");
                    if (!c.Success) continue;
                    string cn = System.Net.WebUtility.HtmlDecode(c.Groups[1].Value).Trim();
                    if (h.Connections.Find(cn) != null && !bad.Contains(cn)) bad.Add(cn);
                }
                if (bad.Count > 0)
                    throw new InvalidOperationException("Not imported: symbolic HMI tags need the integrated HMI connection (drag the HMI to the PLC in the network view; TIA names it HMI_Connection_1). " +
                        "These connections are non-integrated and only allow absolute addressing: " + string.Join(", ", bad.ToArray()) + ". Nothing was changed.");
            }
            if (string.Equals(kind, "tag_table", StringComparison.OrdinalIgnoreCase))
            {
                // A Real tag without Coding gets the default Binary, which is read-only for Real: TIA Portal crashes (17.11).
                foreach (System.Text.RegularExpressions.Match t in System.Text.RegularExpressions.Regex.Matches(xml, @"<Hmi\.Tag\.Tag\b.*?</Hmi\.Tag\.Tag>", System.Text.RegularExpressions.RegexOptions.Singleline))
                {
                    bool real = System.Text.RegularExpressions.Regex.IsMatch(t.Value, @"<HmiDataType\b[^>]*>\s*<Name>L?Real</Name>");
                    if (real && t.Value.IndexOf("<Coding>", StringComparison.Ordinal) < 0)
                        throw new InvalidOperationException("Not imported: a Real HMI tag has no <Coding> (TIA would apply Binary and crash). Use <Coding>IEEE754Float</Coding><Length>4</Length>, or use hmi_tags_from_plc. Nothing was changed.");
                }
            }
            bool saved = false;
            if (saveFirst && Proj().IsModified) { Proj().Save(); saved = true; }
            ImportOptions opt = overwrite ? ImportOptions.Override : ImportOptions.None;
            string file = Path.Combine(Path.GetTempPath(), "tia_assistant_" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(file, xml, new UTF8Encoding(true));
            try
            {
                FileInfo f = new FileInfo(file);
                ArrayList names = new ArrayList();
                switch ((kind ?? "").ToLowerInvariant())
                {
                    case "tag_table": foreach (TagTable t in h.TagFolder.TagTables.Import(f, opt)) names.Add(t.Name); break;
                    case "connection": foreach (Connection c in h.Connections.Import(f, opt)) names.Add(c.Name); break;
                    case "screen": foreach (Screen s in h.ScreenFolder.Screens.Import(f, opt)) names.Add(s.Name); break;
                    case "screen_template": foreach (ScreenTemplate s in h.ScreenTemplateFolder.ScreenTemplates.Import(f, opt)) names.Add(s.Name); break;
                    case "text_list": foreach (TextList t in h.TextLists.Import(f, opt)) names.Add(t.Name); break;
                    case "cycle": foreach (Cycle c in h.Cycles.Import(f, opt)) names.Add(c.Name); break;
                    default: throw new ArgumentException("Unknown kind '" + kind + "'. Use tag_table, connection, screen, screen_template, text_list or cycle.");
                }
                return U.Obj("imported", names, "kind", kind, "project_saved_before_import", saved);
            }
            finally { try { File.Delete(file); } catch { } }
        }
    }
}
