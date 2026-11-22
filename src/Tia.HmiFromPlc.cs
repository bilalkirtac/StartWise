using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Screen;
using Siemens.Engineering.Hmi.Tag;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;

namespace TiaAssistant
{
    // "Show these PLC values on the HMI": does what the TIA editor does when an IO field is bound
    // directly to a PLC tag / DB member: creates the HMI tag (name DB_Member) on the integrated
    // connection, then optionally builds a screen of output IO fields.
    // XML templates are copied from TIA's own exports (V18, KTP700 Basic) - only verified types are allowed.
    public static partial class Tia
    {
        // PLC type -> HMI type, Coding, Length. Only combinations seen in TIA exports.
        static readonly Dictionary<string, string[]> VerifiedHmiTypes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Bool", new string[] { "Bool", "Binary", "1" } },
            { "Int",  new string[] { "Int", "Binary", "2" } },
            { "Real", new string[] { "Real", "IEEE754Float", "4" } },
        };

        static List<string> ArgList(object o)
        {
            List<string> r = new List<string>();
            if (o == null) return r;
            if (o is string) { foreach (string s in ((string)o).Split(',')) if (s.Trim().Length > 0) r.Add(s.Trim()); return r; }
            IEnumerable e = o as IEnumerable;
            if (e != null) foreach (object x in e) if (x != null && x.ToString().Trim().Length > 0) r.Add(x.ToString().Trim());
            return r;
        }

        static IEnumerable<XElement> Children(XElement e, string local)
        {
            foreach (XElement c in e.Elements()) if (c.Name.LocalName == local) yield return c;
        }

        static XElement DbStatic(string dbName, Dictionary<string, XElement> cache)
        {
            XElement sec;
            if (cache.TryGetValue(dbName, out sec)) return sec;
            sec = null;
            foreach (Device d in AllDevices())
            {
                PlcSoftware plc = FindPlcSoftware(d.DeviceItems);
                if (plc == null) continue;
                PlcBlock b = FindBlock(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, dbName);
                if (b == null) continue;
                if (!(b is DataBlock)) throw new InvalidOperationException("'" + dbName + "' is a " + b.GetType().Name + ", not a data block.");
                string xml = ExportToText(delegate(FileInfo f) { b.Export(f, ExportOptions.WithDefaults); });
                XDocument doc = XDocument.Parse(xml);
                // Global DB: members are in Static. Instance DB (the equipment interface, e.g. "SINA_SPEED_Drive_1", "V3300"):
                // FB inputs/outputs are in Input / Output / InOut too. All four are merged into one Static-like section.
                string[] wanted = { "Input", "Output", "InOut", "Static" };
                List<XElement> secs = doc.Descendants().Where(x => x.Name.LocalName == "Section" && wanted.Contains((string)x.Attribute("Name"))).ToList();
                if (secs.Count > 0)
                {
                    sec = new XElement(secs[0].Name, new XAttribute("Name", "Static"));
                    foreach (XElement x in secs) foreach (XElement m in x.Elements()) sec.Add(new XElement(m));
                }
                break;
            }
            cache[dbName] = sec;
            return sec;
        }

        // Returns PLC data type of "Tag" or "DB.Member[.Sub]"; null when not found.
        static string PlcTypeOf(string reference, Dictionary<string, XElement> cache)
        {
            string r = reference.Replace("\"", "");
            int dot = r.IndexOf('.');
            if (dot < 0)
            {
                foreach (Device d in AllDevices())
                {
                    PlcSoftware plc = FindPlcSoftware(d.DeviceItems);
                    if (plc == null) continue;
                    PlcTag t = FindTag(plc.TagTableGroup.TagTables, plc.TagTableGroup.Groups, r);
                    if (t != null) return t.DataTypeName;
                }
                return null;
            }
            XElement cur = DbStatic(r.Substring(0, dot), cache);
            if (cur == null) return null;
            string type = null;
            foreach (string seg in r.Substring(dot + 1).Split('.'))
            {
                XElement next = null;
                foreach (XElement m in Children(cur, "Member")) if ((string)m.Attribute("Name") == seg) { next = m; break; }
                if (next == null) return null;
                type = ((string)next.Attribute("Datatype") ?? "").Trim('"');
                cur = next;
            }
            return type;
        }

        static string HmiTagXml(int id, string name, string controllerTag, string plcType, string[] hmi, string connection, string cycle)
        {
            return "<Hmi.Tag.Tag ID=\"" + id.ToString("X") + "\" CompositionName=\"Tags\"><AttributeList>" +
                "<AcquisitionTriggerMode>Visible</AcquisitionTriggerMode><AddressAccessMode>Symbolic</AddressAccessMode>" +
                "<Coding>" + hmi[1] + "</Coding><ConfirmationType>None</ConfirmationType><GmpRelevant>false</GmpRelevant><JobNumber>0</JobNumber>" +
                "<Length>" + hmi[2] + "</Length><LinearScaling>false</LinearScaling><LogicalAddress /><MandatoryCommenting>false</MandatoryCommenting>" +
                "<Name>" + SecurityElement.Escape(name) + "</Name><Persistency>false</Persistency><QualityCode>false</QualityCode>" +
                "<ScalingHmiHigh>100</ScalingHmiHigh><ScalingHmiLow>0</ScalingHmiLow><ScalingPlcHigh>10</ScalingPlcHigh><ScalingPlcLow>0</ScalingPlcLow>" +
                "<StartValue /><SubstituteValue /><SubstituteValueUsage>None</SubstituteValueUsage><Synchronization>false</Synchronization>" +
                "<UpdateMode>ProjectWide</UpdateMode><UseMultiplexing>false</UseMultiplexing></AttributeList><LinkList>" +
                "<AcquisitionCycle TargetID=\"@OpenLink\"><Name>" + SecurityElement.Escape(cycle) + "</Name></AcquisitionCycle>" +
                "<Connection TargetID=\"@OpenLink\"><Name>" + SecurityElement.Escape(connection) + "</Name></Connection>" +
                "<ControllerTag TargetID=\"@OpenLink\"><Name>" + SecurityElement.Escape(controllerTag) + "</Name></ControllerTag>" +
                "<DataType TargetID=\"@OpenLink\"><Name>" + plcType + "</Name></DataType>" +
                "<HmiDataType TargetID=\"@OpenLink\"><Name>" + hmi[0] + "</Name></HmiDataType></LinkList></Hmi.Tag.Tag>";
        }

        static string Font(int id, int size, string style)
        {
            return "<Hmi.Globalization.MultiLingualFont ID=\"" + id.ToString("X") + "\" CompositionName=\"Font\"><ObjectList><Hmi.Globalization.FontItem ID=\"" + (id + 1).ToString("X") +
                "\" CompositionName=\"Items\"><AttributeList><Culture>en-US</Culture><FontFamily>Tahoma</FontFamily><FontSize>" + size + "</FontSize><FontStyle>" + style +
                "</FontStyle></AttributeList></Hmi.Globalization.FontItem></ObjectList></Hmi.Globalization.MultiLingualFont>";
        }

        static string TextFieldXml(ref int id, string obj, string text, int left, int top, int width, int size, string style)
        {
            int b = id; id += 5;
            return "<Hmi.Screen.TextField ID=\"" + b.ToString("X") + "\" CompositionName=\"ScreenItems\"><AttributeList><BackColor>255, 255, 255</BackColor><BackFillStyle>Transparent</BackFillStyle>" +
                "<BorderBackColor>101, 103, 115</BorderBackColor><BorderColor>71, 73, 87</BorderColor><BorderWidth>0</BorderWidth><BottomMargin>2</BottomMargin><CornerRadius>3</CornerRadius>" +
                "<EdgeStyle>Double</EdgeStyle><FitToLargest>true</FitToLargest><Flashing>None</Flashing><ForeColor>49, 52, 74</ForeColor><Height>34</Height><HorizontalAlignment>Left</HorizontalAlignment>" +
                "<Left>" + left + "</Left><LeftMargin>3</LeftMargin><ObjectName>" + SecurityElement.Escape(obj) + "</ObjectName><RightMargin>2</RightMargin><TabIndex>-1</TabIndex>" +
                "<TextOrientation>Horizontal</TextOrientation><Top>" + top + "</Top><TopMargin>2</TopMargin><UseDesignColorSchema>false</UseDesignColorSchema><VerticalAlignment>Middle</VerticalAlignment>" +
                "<Width>" + width + "</Width></AttributeList><ObjectList>" + Font(b + 1, size, style) +
                "<MultilingualText ID=\"" + (b + 3).ToString("X") + "\" CompositionName=\"Text\"><ObjectList><MultilingualTextItem ID=\"" + (b + 4).ToString("X") +
                "\" CompositionName=\"Items\"><AttributeList><Culture>en-US</Culture><Text><body><p>" + SecurityElement.Escape(text) +
                "</p></body></Text></AttributeList></MultilingualTextItem></ObjectList></MultilingualText></ObjectList></Hmi.Screen.TextField>";
        }

        static string IoFieldXml(ref int id, string obj, string hmiTag, string hmiType, int left, int top, int tabIndex)
        {
            int b = id; id += 7;
            string pattern = hmiType == "Real" ? "9999.9" : "99999";
            int len = pattern.Length;
            return "<Hmi.Screen.IOField ID=\"" + b.ToString("X") + "\" CompositionName=\"ScreenItems\"><AttributeList><AboveUpperLimitColor>237, 88, 97</AboveUpperLimitColor><BackColor>255, 255, 255</BackColor>" +
                "<BackFillStyle>Solid</BackFillStyle><BelowLowerLimitColor>241, 161, 44</BelowLowerLimitColor><BorderBackColor>101, 103, 115</BorderBackColor><BorderColor>71, 73, 87</BorderColor>" +
                "<BorderWidth>4</BorderWidth><BottomMargin>2</BottomMargin><CornerRadius>3</CornerRadius><DataFormat>Decimal</DataFormat><EdgeStyle>Double</EdgeStyle><Enabled>true</Enabled>" +
                "<FieldLength>" + len + "</FieldLength><FitToLargest>false</FitToLargest><Flashing>None</Flashing><ForeColor>49, 52, 74</ForeColor><FormatPattern>" + pattern + "</FormatPattern>" +
                "<Height>54</Height><HiddenInput>false</HiddenInput><HorizontalAlignment>Right</HorizontalAlignment><Left>" + left + "</Left><LeftMargin>3</LeftMargin><Mode>Output</Mode>" +
                "<ObjectName>" + SecurityElement.Escape(obj) + "</ObjectName><RightMargin>2</RightMargin><ShiftDecimalPoint>0</ShiftDecimalPoint><ShowLeadingZeros>false</ShowLeadingZeros>" +
                "<TabIndex>" + tabIndex + "</TabIndex><TextOrientation>Horizontal</TextOrientation><Top>" + top + "</Top><TopMargin>2</TopMargin><Unit /><UseDesignColorSchema>false</UseDesignColorSchema>" +
                "<UseTwoHandOperation>false</UseTwoHandOperation><VerticalAlignment>Middle</VerticalAlignment><Width>150</Width></AttributeList><ObjectList>" + Font(b + 1, 25, "Regular") +
                "<MultilingualText ID=\"" + (b + 3).ToString("X") + "\" CompositionName=\"HelpText\"><ObjectList><MultilingualTextItem ID=\"" + (b + 4).ToString("X") +
                "\" CompositionName=\"Items\"><AttributeList><Culture>en-US</Culture><Text /></AttributeList></MultilingualTextItem></ObjectList></MultilingualText>" +
                "<Hmi.Screen.Property ID=\"" + (b + 5).ToString("X") + "\" CompositionName=\"Properties\"><AttributeList><Name>ProcessValue</Name></AttributeList><ObjectList>" +
                "<Hmi.Dynamic.TagConnectionDynamic ID=\"" + (b + 6).ToString("X") + "\" CompositionName=\"Dynamic\"><AttributeList><Indirect>false</Indirect></AttributeList><LinkList>" +
                "<Tag TargetID=\"@OpenLink\"><Name>" + SecurityElement.Escape(hmiTag) + "</Name></Tag></LinkList></Hmi.Dynamic.TagConnectionDynamic></ObjectList></Hmi.Screen.Property>" +
                "</ObjectList></Hmi.Screen.IOField>";
        }

        static string CircleXml(ref int id, string obj, string hmiTag, int left, int top)
        {
            int b = id; id += 5;
            return "<Hmi.Screen.Circle ID=\"" + b.ToString("X") + "\" CompositionName=\"ScreenItems\"><AttributeList><BackColor>217, 217, 217</BackColor><BackFillStyle>Solid</BackFillStyle>" +
                "<BorderColor>24, 28, 49</BorderColor><BorderWidth>1</BorderWidth><EdgeStyle>Solid</EdgeStyle><Flashing>None</Flashing><Height>34</Height><Left>" + left + "</Left>" +
                "<ObjectName>" + SecurityElement.Escape(obj) + "</ObjectName><Radius>17</Radius><TabIndex>-1</TabIndex><Top>" + top + "</Top><UseDesignColorSchema>false</UseDesignColorSchema><Width>34</Width></AttributeList><ObjectList>" +
                "<Hmi.Dynamic.RangeAppearanceAnimation ID=\"" + (b + 1).ToString("X") + "\" CompositionName=\"Animations\"><AttributeList><Name>RangeAppearanceAnimation</Name></AttributeList><ObjectList>" +
                "<Hmi.Dynamic.TagElementTrigger ID=\"" + (b + 2).ToString("X") + "\" CompositionName=\"RangeTag\"><LinkList><Tag TargetID=\"@OpenLink\"><Name>" + SecurityElement.Escape(hmiTag) + "</Name></Tag></LinkList></Hmi.Dynamic.TagElementTrigger>" +
                "<Hmi.Dynamic.Range ID=\"" + (b + 3).ToString("X") + "\" CompositionName=\"RangeValues\"><AttributeList><BackColor>217, 217, 217</BackColor><FlashingType>No</FlashingType><ForeColor>24, 28, 49</ForeColor><LowerLimit>0</LowerLimit><UpperLimit>0</UpperLimit></AttributeList></Hmi.Dynamic.Range>" +
                "<Hmi.Dynamic.Range ID=\"" + (b + 4).ToString("X") + "\" CompositionName=\"RangeValues\"><AttributeList><BackColor>0, 255, 0</BackColor><FlashingType>No</FlashingType><ForeColor>24, 28, 49</ForeColor><LowerLimit>1</LowerLimit><UpperLimit>1</UpperLimit></AttributeList></Hmi.Dynamic.Range>" +
                "</ObjectList></Hmi.Dynamic.RangeAppearanceAnimation></ObjectList></Hmi.Screen.Circle>";
        }

        static string MlText(int id, string comp, string text)
        {
            string t = string.IsNullOrEmpty(text) ? "<Text />" : "<Text><body><p>" + SecurityElement.Escape(text) + "</p></body></Text>";
            return "<MultilingualText ID=\"" + id.ToString("X") + "\" CompositionName=\"" + comp + "\"><ObjectList><MultilingualTextItem ID=\"" + (id + 1).ToString("X") +
                "\" CompositionName=\"Items\"><AttributeList><Culture>en-US</Culture>" + t + "</AttributeList></MultilingualTextItem></ObjectList></MultilingualText>";
        }

        static string NavButtonXml(ref int id, string obj, string text, string targetScreen, int left, int top, int tabIndex)
        {
            int b = id; id += 14;
            return "<Hmi.Screen.Button ID=\"" + b.ToString("X") + "\" CompositionName=\"ScreenItems\"><AttributeList><BackColor>99, 101, 113</BackColor><BackFillStyle>GradientVertical</BackFillStyle>" +
                "<BitNumber>0</BitNumber><BorderBackColor>105, 105, 105</BorderBackColor><BorderColor>71, 73, 87</BorderColor><BorderWidth>2</BorderWidth><CornerRadius>3</CornerRadius><CornerStyle>Pointed</CornerStyle>" +
                "<EdgeStyle>Solid</EdgeStyle><Enabled>true</Enabled><FirstGradientColor>131, 132, 142</FirstGradientColor><FirstGradientOffset>15</FirstGradientOffset><FitToLargest>false</FitToLargest><Flashing>None</Flashing>" +
                "<FocusColor>148, 182, 231</FocusColor><FocusWidth>2</FocusWidth><ForeColor>255, 255, 255</ForeColor><Height>41</Height><HorizontalAlignment>Center</HorizontalAlignment><HorizontalPictureAlignment>Center</HorizontalPictureAlignment>" +
                "<HotKey>0</HotKey><Left>" + left + "</Left><MiddleGradientColor>99, 101, 113</MiddleGradientColor><Mode>Text</Mode><ObjectName>" + SecurityElement.Escape(obj) + "</ObjectName>" +
                "<PictureAreaBottomMargin>0</PictureAreaBottomMargin><PictureAreaLeftMargin>0</PictureAreaLeftMargin><PictureAreaRightMargin>0</PictureAreaRightMargin><PictureAreaTopMargin>0</PictureAreaTopMargin>" +
                "<PictureAutoSizing>StretchPicture</PictureAutoSizing><SecondGradientColor>88, 90, 103</SecondGradientColor><SecondGradientOffset>15</SecondGradientOffset><TabIndex>" + tabIndex + "</TabIndex>" +
                "<TextAreaBottomMargin>0</TextAreaBottomMargin><TextAreaLeftMargin>0</TextAreaLeftMargin><TextAreaRightMargin>0</TextAreaRightMargin><TextAreaTopMargin>0</TextAreaTopMargin>" +
                "<TextOrientation>Horizontal</TextOrientation><Top>" + top + "</Top><UseDesignColorSchema>false</UseDesignColorSchema><UseFirstGradient>true</UseFirstGradient><UseSecondGradient>true</UseSecondGradient>" +
                "<UseTwoHandOperation>false</UseTwoHandOperation><VerticalAlignment>Middle</VerticalAlignment><VerticalPictureAlignment>Middle</VerticalPictureAlignment><Width>103</Width></AttributeList><ObjectList>" +
                "<Hmi.Event.Event ID=\"" + (b + 1).ToString("X") + "\" CompositionName=\"Events\"><AttributeList><Name>Press</Name></AttributeList><ObjectList>" +
                "<Hmi.Event.FunctionListEventHandler ID=\"" + (b + 2).ToString("X") + "\" CompositionName=\"EventHandler\"><ObjectList>" +
                "<Hmi.Event.FunctionListEntry ID=\"" + (b + 3).ToString("X") + "\" CompositionName=\"FunctionListEntries\"><AttributeList><Name>ActivateScreen</Name><Type>SystemFunction</Type></AttributeList><ObjectList>" +
                "<Hmi.Event.FunctionListEntryParameter ID=\"" + (b + 4).ToString("X") + "\" CompositionName=\"Parameters\"><AttributeList><Name>Screen name</Name></AttributeList><LinkList><Value TargetID=\"@OpenLink\"><Name>" +
                SecurityElement.Escape(targetScreen) + "</Name></Value></LinkList></Hmi.Event.FunctionListEntryParameter>" +
                "<Hmi.Event.FunctionListEntryParameter ID=\"" + (b + 5).ToString("X") + "\" CompositionName=\"Parameters\"><AttributeList><Name>Object number</Name><Value Type=\"System.Int32\">0</Value></AttributeList></Hmi.Event.FunctionListEntryParameter>" +
                "</ObjectList></Hmi.Event.FunctionListEntry></ObjectList></Hmi.Event.FunctionListEventHandler></ObjectList></Hmi.Event.Event>" +
                Font(b + 6, 13, "Bold") + MlText(b + 8, "HelpText", null) + MlText(b + 10, "TextOff", text) + MlText(b + 12, "TextOn", text) +
                "</ObjectList></Hmi.Screen.Button>";
        }

        public static object HmiTagsFromPlc(string station, object refsArg, string table, string connection, string cycle, string screen, string title, object navArg, string template, bool dryRun)
        {
            HmiTarget h = HmiOf(station);
            Device dev = FindDevice(station);
            if (string.IsNullOrEmpty(connection)) connection = "HMI_Connection_1";
            if (string.IsNullOrEmpty(cycle)) cycle = "1 s";
            List<string> refs = ArgList(refsArg);
            if (refs.Count == 0) throw new ArgumentException("Give plc_refs, e.g. [\"DB_Drive_1_Durum.*\"] or [\"DB_Motor.Akim\", \"Motor1_Start\"].");

            Dictionary<string, XElement> cache = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
            // Expand DB.* to the top-level members of the DB.
            List<string> expanded = new List<string>();
            foreach (string r in refs)
            {
                string rr = r.Replace("\"", "");
                if (!rr.EndsWith(".*")) { expanded.Add(rr); continue; }
                string db = rr.Substring(0, rr.Length - 2);
                XElement sec = DbStatic(db, cache);
                if (sec == null) throw new InvalidOperationException("Data block '" + db + "' not found in any PLC. Nothing was changed.");
                foreach (XElement m in Children(sec, "Member")) expanded.Add(db + "." + (string)m.Attribute("Name"));
            }

            // Existing HMI tags of this panel: names, and which controller tag each one points to.
            Dictionary<string, string> byController = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<KeyValuePair<string, TagTable>> tables = new List<KeyValuePair<string, TagTable>>();
            CollectHmiTagTables(h.TagFolder, "", tables);
            foreach (KeyValuePair<string, TagTable> kv in tables)
            {
                tableNames.Add(kv.Value.Name);
                string ex = ExportToText(delegate(FileInfo f) { kv.Value.Export(f, ExportOptions.None); });
                foreach (Match t in Regex.Matches(ex, @"<Hmi\.Tag\.Tag\b.*?</Hmi\.Tag\.Tag>", RegexOptions.Singleline))
                {
                    Match n = Regex.Match(t.Value, @"<AttributeList>.*?<Name>([^<]+)</Name>", RegexOptions.Singleline);
                    Match c = Regex.Match(t.Value, @"<ControllerTag\b[^>]*>\s*<Name>([^<]+)</Name>");
                    if (!n.Success) continue;
                    string nm = System.Net.WebUtility.HtmlDecode(n.Groups[1].Value).Trim();
                    existingNames.Add(nm);
                    if (c.Success) byController[System.Net.WebUtility.HtmlDecode(c.Groups[1].Value).Trim().Replace("\"", "")] = nm;
                }
            }

            ArrayList created = new ArrayList(), reused = new ArrayList(), skipped = new ArrayList();
            List<string[]> screenRows = new List<string[]>(); // label, hmi tag, hmi type
            StringBuilder tagsXml = new StringBuilder();
            int id = 1;
            foreach (string r in expanded)
            {
                string plcType = PlcTypeOf(r, cache);
                if (plcType == null) { skipped.Add(U.Obj("plc", r, "reason", "not found in any PLC")); continue; }
                string[] hmi;
                if (!VerifiedHmiTypes.TryGetValue(plcType, out hmi))
                { skipped.Add(U.Obj("plc", r, "type", plcType, "reason", "HMI XML for this type is not verified yet (verified: Bool, Int, Real)")); continue; }
                string label = r.Substring(r.LastIndexOf('.') + 1).Replace('_', ' ');
                string existing;
                if (byController.TryGetValue(r, out existing))
                {
                    reused.Add(U.Obj("plc", r, "hmi_tag", existing));
                    screenRows.Add(new string[] { label, existing, hmi[0] });
                    continue;
                }
                string name = r.Replace('.', '_');
                if (existingNames.Contains(name)) { skipped.Add(U.Obj("plc", r, "reason", "an HMI tag named " + name + " already exists but points elsewhere")); continue; }
                tagsXml.Append(HmiTagXml(id++, name, r, plcType, hmi, connection, cycle)).Append('\n');
                existingNames.Add(name);
                created.Add(U.Obj("plc", r, "hmi_tag", name, "type", hmi[0]));
                screenRows.Add(new string[] { label, name, hmi[0] });
            }

            if (string.IsNullOrEmpty(table))
            {
                string first = expanded.Count > 0 ? expanded[0] : "PLC";
                table = (first.IndexOf('.') > 0 ? first.Substring(0, first.IndexOf('.')) : "PLC_Tags") + "_HMI";
            }
            if (created.Count > 0 && tableNames.Contains(table))
                throw new InvalidOperationException("HMI tag table '" + table + "' already exists. Give another 'table' name. Nothing was changed.");

            // Screen: values in two columns of five rows; numbers as output IO fields, Bools as circle indicators (grey/green).
            List<string[]> slots = new List<string[]>();
            foreach (string[] row in screenRows) if (row[2] != "Bool") slots.Add(row);
            foreach (string[] row in screenRows) if (row[2] == "Bool") slots.Add(row);
            List<string> nav = ArgList(navArg);
            string screenXml = null; int screenNumber = 0;
            if (!string.IsNullOrEmpty(screen))
            {
                List<KeyValuePair<string, Screen>> screens = new List<KeyValuePair<string, Screen>>();
                CollectScreens(h.ScreenFolder, "", screens);
                HashSet<string> screenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, Screen> kv in screens)
                {
                    screenNames.Add(kv.Value.Name);
                    if (string.Equals(kv.Value.Name, screen, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Screen '" + screen + "' already exists. Give another 'screen' name. Nothing was changed.");
                    Screen sc = kv.Value;
                    string sx = ExportToText(delegate(FileInfo f) { sc.Export(f, ExportOptions.WithDefaults); });
                    Match nm = Regex.Match(sx, @"<Number>(\d+)</Number>");
                    if (nm.Success) screenNumber = Math.Max(screenNumber, int.Parse(nm.Groups[1].Value));
                }
                screenNumber++;
                // A button that activates a missing screen could break the import: check first.
                foreach (string n in nav) if (!screenNames.Contains(n)) throw new InvalidOperationException("Navigation target screen '" + n + "' does not exist on '" + station + "'. Nothing was changed.");
                if (nav.Count > 7) throw new InvalidOperationException("At most 7 navigation buttons fit. Nothing was changed.");
                if (slots.Count > 10) throw new InvalidOperationException("At most 10 values fit on one screen; " + slots.Count + " were given. Split them. Nothing was changed.");
                string tplLink = TemplateLink(h, template);
                StringBuilder items = new StringBuilder();
                int sid = 4;
                items.Append(TextFieldXml(ref sid, "Baslik", string.IsNullOrEmpty(title) ? screen.Replace('_', ' ') : title, 40, 20, 400, 29, "Bold"));
                for (int i = 0; i < slots.Count; i++)
                {
                    int col = i / 5, row = i % 5;
                    int x = col == 0 ? 40 : 420, top = 90 + row * 66;
                    if (slots[i][2] == "Bool")
                    {
                        items.Append(CircleXml(ref sid, "Led_" + slots[i][1], slots[i][1], x, top + 10));
                        items.Append(TextFieldXml(ref sid, "Lbl_" + slots[i][1], slots[i][0], x + 44, top + 10, 300, 25, "Regular"));
                    }
                    else
                    {
                        items.Append(TextFieldXml(ref sid, "Lbl_" + slots[i][1], slots[i][0], x, top + 10, 200, 25, "Regular"));
                        items.Append(IoFieldXml(ref sid, "IO_" + slots[i][1], slots[i][1], slots[i][2], x + 210, top, i + 1));
                    }
                }
                for (int i = 0; i < nav.Count; i++)
                    items.Append(NavButtonXml(ref sid, "Nav_" + nav[i], nav[i].Replace('_', ' '), nav[i], 689 - i * 113, tplLink.Length > 0 ? 15 : 432, 50 + i));
                screenXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Document>\n<Engineering version=\"" + Program.TiaName + "\" />\n<Hmi.Screen.Screen ID=\"0\"><AttributeList><ActiveLayer>0</ActiveLayer>" +
                    "<BackColor>182, 182, 182</BackColor><GridColor>0, 0, 0</GridColor><Height>480</Height><Name>" + SecurityElement.Escape(screen) + "</Name><Number>" + screenNumber +
                    "</Number><Visible>true</Visible><Width>800</Width></AttributeList>" + tplLink + "<ObjectList><MultilingualText ID=\"1\" CompositionName=\"HelpText\"><ObjectList>" +
                    "<MultilingualTextItem ID=\"2\" CompositionName=\"Items\"><AttributeList><Culture>en-US</Culture><Text /></AttributeList></MultilingualTextItem></ObjectList></MultilingualText>" +
                    "<Hmi.Screen.ScreenLayer ID=\"3\" CompositionName=\"Layers\"><AttributeList><Index>0</Index><Name /><VisibleES>true</VisibleES></AttributeList><ObjectList>" +
                    items.ToString() + "</ObjectList></Hmi.Screen.ScreenLayer></ObjectList></Hmi.Screen.Screen>\n</Document>";
            }

            Dictionary<string, object> result = U.Obj("hmi", station, "table", created.Count > 0 ? table : null, "created", created, "reused", reused, "skipped", skipped,
                "dry_run", dryRun);
            if (screenXml != null) { result["screen"] = screen; result["screen_number"] = screenNumber; result["nav_buttons"] = nav; }
            if (dryRun) return result;

            if (created.Count > 0)
            {
                string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Document>\n<Engineering version=\"" + Program.TiaName + "\" />\n<Hmi.Tag.TagTable ID=\"0\"><AttributeList><Name>" +
                    SecurityElement.Escape(table) + "</Name></AttributeList><ObjectList>\n" + tagsXml.ToString() + "</ObjectList></Hmi.Tag.TagTable>\n</Document>";
                HmiImport(station, "tag_table", xml, false, true);
            }
            if (screenXml != null) HmiImport(station, "screen", screenXml, false, true);
            return result;
        }
    }
}
