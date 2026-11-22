using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Library.Types;

namespace TiaAssistant
{
    // Faceplate instances on a new screen. The faceplate TYPE is designed by the engineer in TIA (no Openness API);
    // instances and their interface bindings are XML (Hmi.Screen.FaceplateInstance, verified 17.11 on TP900 Comfort).
    public static partial class Tia
    {
        static void FindLibTypes(LibraryTypeFolder f, string name, List<LibraryType> found)
        {
            foreach (LibraryType t in f.Types) if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) found.Add(t);
            foreach (LibraryTypeFolder sub in f.Folders) FindLibTypes(sub, name, found);
        }

        // Interface property names declared by the faceplate type version (from its XML export).
        static List<string> FaceplateInterface(LibraryTypeVersion v, out string error)
        {
            error = null;
            List<string> names = new List<string>();
            try
            {
                string xml = ExportToText(delegate(FileInfo f) { v.Export(f, ExportOptions.WithDefaults); });
                foreach (Match m in Regex.Matches(xml, @"<([A-Za-z.]*Interface[A-Za-z.]*)\b[^>]*>\s*<AttributeList>.*?<Name>([^<]+)</Name>", RegexOptions.Singleline))
                {
                    string n = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value).Trim();
                    if (!names.Contains(n)) names.Add(n);
                }
            }
            catch (Exception e) { error = e.Message; }
            return names;
        }

        static string TypeExportHint(LibraryTypeVersion v)
        {
            try
            {
                string xml = ExportToText(delegate(FileInfo f) { v.Export(f, ExportOptions.WithDefaults); });
                List<string> tags = new List<string>();
                foreach (Match m in Regex.Matches(xml, @"<([A-Za-z][A-Za-z0-9.]*)")) if (!tags.Contains(m.Groups[1].Value)) tags.Add(m.Groups[1].Value);
                return "[" + xml.Length + " chars] " + string.Join(" ", tags.ToArray());
            }
            catch (Exception e) { return "export failed: " + e.Message; }
        }

        public static object HmiFaceplateScreen(string station, string screen, string title, string faceplate, object instancesArg, string connection, string template, bool dryRun)
        {
            HmiTarget h = HmiOf(station);
            List<LibraryType> types = new List<LibraryType>();
            FindLibTypes(Proj().ProjectLibrary.TypeFolder, faceplate, types);
            if (types.Count == 0) throw new InvalidOperationException("Faceplate type '" + faceplate + "' not found in the project library. Nothing was changed.");
            LibraryTypeVersion ver = null;
            foreach (LibraryTypeVersion v in types[0].Versions)
                if (v.State == LibraryTypeVersionState.Committed && (ver == null || v.VersionNumber > ver.VersionNumber)) ver = v;
            if (ver == null) throw new InvalidOperationException("Faceplate type '" + faceplate + "' has no released version. Release it in TIA first. Nothing was changed.");
            string typeName = types[0].Name + " V " + ver.VersionNumber.ToString();
            string ifErr;
            List<string> iface = FaceplateInterface(ver, out ifErr);
            string ifSource = iface.Count > 0 ? "type export" : null;
            if (iface.Count == 0)
            {
                // Fallback: interface names used by existing instances of this type on any screen of this HMI (proven to import).
                List<KeyValuePair<string, Siemens.Engineering.Hmi.Screen.Screen>> screens = new List<KeyValuePair<string, Siemens.Engineering.Hmi.Screen.Screen>>();
                CollectScreens(h.ScreenFolder, "", screens);
                string prefix = types[0].Name + " V ";
                foreach (KeyValuePair<string, Siemens.Engineering.Hmi.Screen.Screen> kv in screens)
                {
                    Siemens.Engineering.Hmi.Screen.Screen sc = kv.Value;
                    string sx = ExportToText(delegate(FileInfo f) { sc.Export(f, ExportOptions.None); });
                    foreach (Match fm in Regex.Matches(sx, @"<Hmi\.Screen\.FaceplateInstance\b.*?</Hmi\.Screen\.FaceplateInstance>", RegexOptions.Singleline))
                    {
                        if (fm.Value.IndexOf("<FaceplateTypeName>" + prefix, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        foreach (Match pm in Regex.Matches(fm.Value, @"<Hmi\.Screen\.InterfaceProperty\w*\b[^>]*>\s*<AttributeList>\s*<Name>([^<]+)</Name>"))
                        { string n = System.Net.WebUtility.HtmlDecode(pm.Groups[1].Value).Trim(); if (!iface.Contains(n)) iface.Add(n); }
                    }
                }
                if (iface.Count > 0) ifSource = "existing instances on " + station;
            }

            IEnumerable list = instancesArg as IEnumerable;
            if (list == null || instancesArg is string) throw new ArgumentException("instances: [{name, interface:{Prop: 'DB.Member' or HMI tag}, left?, top?, width?, height?}]");
            List<IDictionary> inst = new List<IDictionary>();
            List<string> refs = new List<string>();
            foreach (object o in list)
            {
                IDictionary d = o as IDictionary;
                if (d == null) continue;
                inst.Add(d);
                IDictionary map = d["interface"] as IDictionary;
                if (map == null) throw new ArgumentException("Each instance needs an 'interface' object.");
                foreach (DictionaryEntry e in map)
                {
                    string prop = Convert.ToString(e.Key);
                    if (iface.Count > 0 && !iface.Contains(prop))
                        throw new InvalidOperationException("'" + prop + "' is not an interface property of " + typeName + ". Known: " + string.Join(", ", iface.ToArray()) + ". Nothing was changed.");
                    string r = Convert.ToString(e.Value);
                    if (!refs.Contains(r)) refs.Add(r);
                }
            }
            if (iface.Count == 0)
                throw new InvalidOperationException("Could not read the interface of " + typeName + (ifErr != null ? " (" + ifErr + ")" : "") +
                    " and no instance of it exists on this HMI. Place one instance by hand once (any binding), then call again. " +
                    "Refusing to import unchecked interface names because a wrong one can crash TIA. Nothing was changed. Type export elements: " + TypeExportHint(ver));

            // HMI tags for the PLC values (created like hmi_tags_from_plc, reused when they exist).
            Dictionary<string, object> tags = (Dictionary<string, object>)HmiTagsFromPlc(station, refs, screen + "_Tags", connection, null, null, null, null, null, dryRun);
            Dictionary<string, string> hmiOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string k in new string[] { "created", "reused" })
                foreach (object o in (ArrayList)tags[k]) { IDictionary d = (IDictionary)o; hmiOf[Convert.ToString(d["plc"])] = Convert.ToString(d["hmi_tag"]); }
            List<string> missing = new List<string>();
            foreach (string r in refs) if (!hmiOf.ContainsKey(r.Replace("\"", ""))) missing.Add(r);
            if (missing.Count > 0)
                throw new InvalidOperationException("No PLC value / supported type for: " + string.Join(", ", missing.ToArray()) + ". " +
                    (dryRun ? "" : "HMI tags for the others may already have been created. ") + "Screen not created.");

            if (FindScreen(h, screen) != null) throw new InvalidOperationException("Screen '" + screen + "' already exists. Nothing was changed.");
            string tplLink = TemplateLink(h, template);
            int number = MaxScreenNumber(h) + 1;
            StringBuilder items = new StringBuilder();
            int sid = 4;
            items.Append(TextFieldXml(ref sid, "Baslik", string.IsNullOrEmpty(title) ? screen.Replace('_', ' ') : title, 20, 10, 400, 29, "Bold"));
            for (int i = 0; i < inst.Count; i++)
            {
                IDictionary d = inst[i];
                Func<string, int, int> num = delegate(string k, int def) { return d.Contains(k) && d[k] != null ? Convert.ToInt32(d[k]) : def; };
                int w = num("width", 380), hgt = num("height", 180);
                int left = num("left", 20 + (i % 2) * 400), top = num("top", 60 + (i / 2) * (hgt + 10));
                string obj = d.Contains("name") ? Convert.ToString(d["name"]) : faceplate + "_" + (i + 1);
                int b = sid; sid++;
                items.Append("<Hmi.Screen.FaceplateInstance ID=\"" + b.ToString("X") + "\" CompositionName=\"ScreenItems\"><AttributeList><FaceplateTypeName>" + SecurityElement.Escape(typeName) +
                    "</FaceplateTypeName><Height>" + hgt + "</Height><Left>" + left + "</Left><ObjectName>" + SecurityElement.Escape(obj) + "</ObjectName><Resizing>None</Resizing><TabIndex>" + (i + 1) +
                    "</TabIndex><Top>" + top + "</Top><Width>" + w + "</Width></AttributeList><ObjectList>");
                foreach (DictionaryEntry e in (IDictionary)d["interface"])
                {
                    int p = sid; sid += 2;
                    items.Append("<Hmi.Screen.InterfacePropertySimple ID=\"" + p.ToString("X") + "\" CompositionName=\"Interface\"><AttributeList><Name>" + SecurityElement.Escape(Convert.ToString(e.Key)) +
                        "</Name></AttributeList><ObjectList><Hmi.Dynamic.TagConnectionDynamic ID=\"" + (p + 1).ToString("X") + "\" CompositionName=\"Dynamic\"><AttributeList><Indirect>false</Indirect></AttributeList>" +
                        "<LinkList><Tag TargetID=\"@OpenLink\"><Name>" + SecurityElement.Escape(hmiOf[Convert.ToString(e.Value).Replace("\"", "")]) + "</Name></Tag></LinkList></Hmi.Dynamic.TagConnectionDynamic></ObjectList></Hmi.Screen.InterfacePropertySimple>");
                }
                items.Append("</ObjectList></Hmi.Screen.FaceplateInstance>");
            }
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Document>\n<Engineering version=\"" + Program.TiaName + "\" />\n<Hmi.Screen.Screen ID=\"0\"><AttributeList><ActiveLayer>0</ActiveLayer>" +
                "<BackColor>182, 182, 182</BackColor><GridColor>0, 0, 0</GridColor><Height>480</Height><Name>" + SecurityElement.Escape(screen) + "</Name><Number>" + number +
                "</Number><Visible>true</Visible><Width>800</Width></AttributeList>" + tplLink + "<ObjectList><MultilingualText ID=\"1\" CompositionName=\"HelpText\"><ObjectList>" +
                "<MultilingualTextItem ID=\"2\" CompositionName=\"Items\"><AttributeList><Culture>en-US</Culture><Text /></AttributeList></MultilingualTextItem></ObjectList></MultilingualText>" +
                "<Hmi.Screen.ScreenLayer ID=\"3\" CompositionName=\"Layers\"><AttributeList><Index>0</Index><Name /><VisibleES>true</VisibleES></AttributeList><ObjectList>" +
                items.ToString() + "</ObjectList></Hmi.Screen.ScreenLayer></ObjectList></Hmi.Screen.Screen>\n</Document>";
            Dictionary<string, object> result = U.Obj("station", station, "screen", screen, "number", number, "faceplate", typeName, "interface", iface, "interface_source", ifSource,
                "instances", inst.Count, "hmi_tags", tags, "dry_run", dryRun);
            if (!dryRun) HmiImport(station, "screen", xml, false, true);
            return result;
        }
    }
}
