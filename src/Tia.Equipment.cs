using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;

namespace TiaAssistant
{
    // Field equipment without own hardware (valves, analog measurements, ...): same pattern as drive_prepare.
    // Each type lives in knowledge/equipment/<type>/equipment.json (+ FB SCL source). One call:
    //   FB (from template, once) -> instance DB named after the equipment -> PLC tags for the field signals (when addresses are given)
    //   -> one network in the type's LAD FC (pins open except field signals) -> OB1 calls the FC -> compile.
    // New equipment types need only a new folder, no build.
    public static partial class Tia
    {
        static string EquipmentDir() { return Path.Combine(U.KnowledgeDir(), "equipment"); }

        static Dictionary<string, object> EquipmentSpec(string type)
        {
            string f = Path.Combine(EquipmentDir(), type ?? "", "equipment.json");
            if (string.IsNullOrEmpty(type) || !File.Exists(f))
                throw new InvalidOperationException("Unknown equipment type '" + type + "'. Known: " + string.Join(", ", EquipmentTypes().ToArray()));
            return (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(File.ReadAllText(f, Encoding.UTF8));
        }

        static List<string> EquipmentTypes()
        {
            List<string> r = new List<string>();
            if (Directory.Exists(EquipmentDir()))
                foreach (string d in Directory.GetDirectories(EquipmentDir()))
                    if (File.Exists(Path.Combine(d, "equipment.json"))) r.Add(Path.GetFileName(d));
            return r;
        }

        static string Sv(Dictionary<string, object> d, string k) { object o; return d.TryGetValue(k, out o) && o != null ? Convert.ToString(o) : null; }

        public static object EquipmentTypesList()
        {
            ArrayList list = new ArrayList();
            foreach (string t in EquipmentTypes())
            {
                Dictionary<string, object> s = EquipmentSpec(t);
                ArrayList io = new ArrayList();
                foreach (object po in (IEnumerable)s["pins"])
                {
                    Dictionary<string, object> p = (Dictionary<string, object>)po;
                    if (Sv(p, "io") != null) io.Add(U.Obj("io", Sv(p, "io"), "pin", Sv(p, "name"), "section", Sv(p, "section"), "type", Sv(p, "type"), "tag", Sv(p, "tag")));
                }
                list.Add(U.Obj("type", t, "description", Sv(s, "description"), "fb", Sv(s, "fb"), "fc", Sv(s, "fc"), "field_signals", io, "usage", Sv(s, "usage")));
            }
            return U.Obj("types", list, "folder", EquipmentDir());
        }

        static string StartValue(string type, object v)
        {
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            switch ((type ?? "").ToLowerInvariant())
            {
                case "bool": return Convert.ToBoolean(v is string ? ((string)v).Trim().ToLowerInvariant() == "true" || (string)v == "1" : v) ? "TRUE" : "FALSE";
                case "real": case "lreal":
                    { string r = Convert.ToDouble(v, ci).ToString("R", ci); return r.Contains(".") || r.Contains("E") ? r : r + ".0"; }
                case "int": case "dint": case "sint": case "uint": case "udint": case "usint":
                    return Convert.ToInt64(v, ci).ToString(ci);
                default: return Convert.ToString(v, ci);   // Time (T#5S), Word (16#00FF) ... as given
            }
        }

        // io: {"<io key>": "%I10.0" | "<existing PLC tag name>"}; missing keys leave the pin open.
        public static object EquipmentPrepare(string plcName, string type, string name, IDictionary io, IDictionary prms, bool dryRun)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("name is required (equipment code, e.g. V3300).");
            Dictionary<string, object> spec = EquipmentSpec(type);
            Func<string, string> fill = s => s == null ? null : s.Replace("{name}", name);
            string fb = Sv(spec, "fb"), fcName = Sv(spec, "fc"), inst = fill(Sv(spec, "instance") ?? "{name}");
            string table = Sv(spec, "tag_table") ?? fcName;
            ArrayList steps = new ArrayList();
            Action<string, string, string> step = (s, st, d) => steps.Add(U.Obj("step", s, "status", st, "detail", d));
            PlcSoftware plc = PlcOf(plcName);

            // Field signals: decide per pin (tag to wire, or open)
            List<Pin> pins = new List<Pin>();
            List<string[]> newTags = new List<string[]>();   // name, type, address, comment
            HashSet<string> usedKeys = new HashSet<string>();
            foreach (object po in (IEnumerable)spec["pins"])
            {
                Dictionary<string, object> p = (Dictionary<string, object>)po;
                Pin pin = new Pin { Name = Sv(p, "name"), Section = Sv(p, "section"), Type = Sv(p, "type") };
                string key = Sv(p, "io");
                if (key != null && io != null && io.Contains(key) && !string.IsNullOrEmpty(Convert.ToString(io[key])))
                {
                    usedKeys.Add(key);
                    string v = Convert.ToString(io[key]).Trim();
                    string tagName;
                    if (v.StartsWith("%"))
                    {
                        tagName = fill(Sv(p, "tag")) ?? (name + "_" + pin.Name);
                        PlcTag existing = FindTag(plc.TagTableGroup.TagTables, plc.TagTableGroup.Groups, tagName);
                        if (existing == null) newTags.Add(new[] { tagName, pin.Type, v, fill(Sv(p, "comment")) ?? "" });
                        else if (!string.Equals(existing.LogicalAddress, v, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Tag '" + tagName + "' already exists at " + existing.LogicalAddress + ", not " + v + ". Give the existing address or the tag name.");
                    }
                    else
                    {
                        tagName = v.Trim('"');
                        if (FindTag(plc.TagTableGroup.TagTables, plc.TagTableGroup.Groups, tagName) == null)
                            throw new InvalidOperationException("io '" + key + "': PLC tag '" + tagName + "' not found. Give an address like %I10.0 or an existing tag name.");
                    }
                    pin.Source = tagName;
                }
                pins.Add(pin);
            }
            if (io != null)
                foreach (object k in io.Keys)
                    if (!usedKeys.Contains(Convert.ToString(k)))
                        throw new ArgumentException("Unknown io key '" + k + "' for type " + type + ". Use: " +
                            string.Join(", ", ((IEnumerable)spec["pins"]).Cast<Dictionary<string, object>>().Select(p => Sv(p, "io")).Where(x => x != null).ToArray()));

            // Start values of the instance DB (params): only input pins, formatted by pin type.
            List<string> startVals = new List<string>();
            if (prms != null)
                foreach (object ko in prms.Keys)
                {
                    string k = Convert.ToString(ko);
                    Pin pin = pins.FirstOrDefault(x => string.Equals(x.Name, k, StringComparison.OrdinalIgnoreCase));
                    if (pin == null || pin.Section != "Input")
                        throw new ArgumentException("params: '" + k + "' is not an input of " + Sv(spec, "fb") + ". Inputs: " +
                            string.Join(", ", pins.Where(x => x.Section == "Input").Select(x => x.Name).ToArray()));
                    if (pin.Source != null) throw new ArgumentException("params: '" + pin.Name + "' is wired to a field signal; it has no start value.");
                    startVals.Add(pin.Name + " := " + StartValue(pin.Type, prms[ko]) + ";");
                }

            ArrayList wiring = new ArrayList();
            foreach (Pin p in pins) if (p.Source != null) wiring.Add(p.Name + " = \"" + p.Source + "\"");
            if (dryRun)
            {
                step("plan", "dry_run", "FB " + fb + " (once), instance DB \"" + inst + "\"" + (startVals.Count > 0 ? " (start values: " + string.Join(" ", startVals.ToArray()) + ")" : "") + ", " + (newTags.Count > 0 ? newTags.Count + " new tag(s) in '" + table + "', " : "") +
                    "network in '" + fcName + "', OB1 call. Wired: " + (wiring.Count > 0 ? string.Join(", ", (string[])wiring.ToArray(typeof(string))) : "none (all pins open)"));
                return U.Obj("equipment", name, "type", type, "steps", steps, "dry_run", true);
            }

            if (!BlockExists(plc, fb))
            {
                string src = File.ReadAllText(Path.Combine(EquipmentDir(), type, Sv(spec, "fb_source")), Encoding.UTF8);
                SclImport(plcName, src, "StartWise_" + fb, false);
                step("FB " + fb, "created", "from knowledge/equipment/" + type);
            }
            if (!BlockExists(plc, inst))
            {
                SclImport(plcName, "DATA_BLOCK \"" + inst + "\"\n{ S7_Optimized_Access := 'TRUE' }\nVERSION : 0.1\n\"" + fb + "\"\nBEGIN\n" +
                    string.Concat(startVals.Select(v => "   " + v + "\n").ToArray()) + "END_DATA_BLOCK\n", "StartWise_" + inst, false);
                step("instance \"" + inst + "\"", "created", startVals.Count > 0 ? string.Join(" ", startVals.ToArray()) : null);
            }
            else step("instance \"" + inst + "\"", "exists", startVals.Count > 0 ? "params NOT applied (DB already exists); change the start values in the DB or delete it first" : null);

            if (newTags.Count > 0)
            {
                ArrayList tags = new ArrayList();
                foreach (string[] t in newTags) tags.Add(U.Obj("name", t[0], "data_type", t[1], "address", t[2], "comment", t[3]));
                TagsAdd(plcName, table, tags);
                step("PLC tags", "created", string.Join(", ", newTags.Select(t => t[0] + " " + t[2]).ToArray()) + " (table '" + table + "')");
            }

            string flg = FbCallFlgNet(fb, inst, pins);
            string title = fill(Sv(spec, "title") ?? "{name}");
            string marker = "<Component Name=\"" + inst + "\" />";
            PlcBlock fcBlock = FindBlock(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, fcName);
            if (fcBlock == null)
            {
                ImportBlockXml(plc, NewLadFcXml(fcName, Sv(spec, "fc_comment") ?? "StartWise", flg, title));
                step("FC " + fcName, "created", "network for " + name);
            }
            else
            {
                string r = AppendLadNetwork(plc, fcBlock, flg, title, marker, null);
                step("FC " + fcName, r == "added" ? "network added" : "network exists", r == "exists" ? "the existing network is left as it is (wiring not changed)" : name);
            }

            PlcBlock ob = FindBlock(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, MainOb);
            if (ob == null) step("OB1", "skipped", "no block named Main");
            else
            {
                try { step("OB1", AppendLadNetwork(plc, ob, FcCallFlgNet(fcName), fcName + " (StartWise)", "<CallInfo Name=\"" + fcName + "\" BlockType=\"FC\"", null) == "added" ? "call added" : "call exists", fcName); }
                catch (Exception e) { step("OB1", "skipped", e.Message + " Call '" + fcName + "' from your cyclic OB by hand."); }
            }

            Dictionary<string, object> comp = (Dictionary<string, object>)Compile(plcName, "software");
            step("compile software", Convert.ToInt32(comp["errors"]) == 0 ? "ok" : "errors", "errors " + comp["errors"] + ", warnings " + comp["warnings"]);
            return U.Obj("equipment", name, "type", type, "interface_db", inst, "wired", wiring, "steps", steps, "compile", comp,
                "usage", fill(Sv(spec, "usage")), "note", "Project not saved; call project_save.");
        }
    }
}
