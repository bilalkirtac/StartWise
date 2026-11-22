#if !TIA_V18 && !NO_UNIFIED
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.HmiAlarm;
using Siemens.Engineering.HmiUnified.HmiTags;
using Siemens.Engineering.HmiUnified.UI.Base;
using Siemens.Engineering.HmiUnified.UI.Controls;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.Dynamization.Tag;
using Siemens.Engineering.HmiUnified.UI.Enum;
using Siemens.Engineering.HmiUnified.UI.Events;
using Siemens.Engineering.HmiUnified.UI.Parts;
using Siemens.Engineering.HmiUnified.UI.Screens;
using Siemens.Engineering.HmiUnified.UI.Shapes;
using Siemens.Engineering.HmiUnified.UI.Widgets;

namespace TiaAssistant
{
    // WinCC Unified building blocks for the usual operator screen: buttons that write PLC values (JavaScript events),
    // colour indicators (BackColor tag dynamization with a bit mapping table), input/output fields, a live trend,
    // an alarm view and HMI discrete alarms. One call adds any mix; elements without x/y are laid out in rows below
    // what is already on the screen. Learned 20.11.2026 on test_v21 / HMI_2.
    public static partial class Tia
    {
        static Color ColorOf(object v, Color dflt)
        {
            string s = v == null ? "" : Convert.ToString(v).Trim();
            if (s.Length == 0) return dflt;
            switch (s.ToLowerInvariant())
            {
                case "yesil": case "yeşil": return Color.FromArgb(0, 176, 80);
                case "green": return Color.FromArgb(0, 176, 80);
                case "kirmizi": case "kırmızı": case "red": return Color.FromArgb(220, 30, 30);
                case "gri": case "grey": case "gray": return Color.FromArgb(128, 128, 128);
                case "sari": case "sarı": case "yellow": return Color.FromArgb(255, 200, 0);
                case "mavi": case "blue": return Color.FromArgb(0, 112, 192);
                case "turuncu": case "orange": return Color.FromArgb(255, 140, 0);
                case "siyah": case "black": return Color.Black;
                case "beyaz": case "white": return Color.White;
            }
            return ColorTranslator.FromHtml(s.StartsWith("#") || char.IsLetter(s[0]) ? s : "#" + s);
        }

        // TIA compiles Unified with the fallback font "Siemens Sans"; on a PC without that font (e.g. after removing an
        // older TIA/WinCC) the HMI compile fails. If it is missing, switch on language-compatible font families (only then
        // other fonts are accepted) and use Microsoft Sans Serif. Returns what was done, or null.
        static string EnsureUnifiedFont(HmiSoftware sw)
        {
            try
            {
                bool hasSiemensSans;
                using (System.Drawing.Text.InstalledFontCollection fc = new System.Drawing.Text.InstalledFontCollection())
                    hasSiemensSans = fc.Families.Any(f => f.Name.Equals("Siemens Sans", StringComparison.OrdinalIgnoreCase));
                if (hasSiemensSans) return null;
                var rs = sw.RuntimeSettings;
                List<string> changed = new List<string>();
                foreach (var lf in rs.LanguageAndFonts)
                {
                    if (!string.Equals(lf.DefaultFont, "Siemens Sans", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!rs.EnableLanguageCompatibleFontFamilies) rs.EnableLanguageCompatibleFontFamilies = true;
                    lf.DefaultFont = "Microsoft Sans Serif";
                    changed.Add(lf.Language);
                }
                return changed.Count == 0 ? null : "Siemens Sans is not installed on this PC: fallback font set to Microsoft Sans Serif (" + string.Join(", ", changed.ToArray()) + ").";
            }
            catch (Exception ex) { return "Font check failed: " + (ex.InnerException ?? ex).Message; }
        }

        static string S(IDictionary d, string k) { return d != null && d.Contains(k) && d[k] != null ? Convert.ToString(d[k]) : null; }
        static int I(IDictionary d, string k, int dflt) { return d != null && d.Contains(k) && d[k] != null ? Convert.ToInt32(d[k]) : dflt; }

        // HMI tag for an element: 'tag' = existing HMI tag name; 'plc' = PLC tag / DB member (HMI tag created or reused).
        static string EnsureUnifiedTag(HmiSoftware sw, IDictionary d, string connection, string table, ArrayList created)
        {
            string tag = S(d, "tag"), plc = S(d, "plc");
            if (plc != null) plc = plc.Replace("\"", "");
            if (tag != null && sw.Tags.Find(tag) != null) return tag;
            if (plc == null) throw new ArgumentException("Element needs 'plc' (PLC tag / DB member) or an existing HMI 'tag'" + (tag != null ? " ('" + tag + "' not found)" : "") + ".");
            string tn = tag ?? S(d, "hmi_tag") ?? TagNameFor(plc);
            HmiTag t = sw.Tags.Find(tn);
            if (t != null) return tn;
            t = sw.Tags.Create(tn, table);
            t.Connection = connection;
            t.PlcTag = plc;
            IDictionary sc = d.Contains("scale") ? d["scale"] as IDictionary : null;
            int dec = I(d, "decimals", 0);
            if (sc != null)
            {
                t.LinearScaling = true;
                double? a;
                if ((a = Num(sc, "plc_min")) != null) t.PlcStartValue = a.Value;
                if ((a = Num(sc, "plc_max")) != null) t.PlcEndValue = a.Value;
                if ((a = Num(sc, "hmi_min")) != null) t.HmiStartValue = a.Value * Math.Pow(10, dec);
                if ((a = Num(sc, "hmi_max")) != null) t.HmiEndValue = a.Value * Math.Pow(10, dec);
            }
            created.Add(tn + " -> " + plc);
            return tn;
        }

        static string JsTag(string tag) { return "Tags(\"" + tag.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\")"; }

        static void Script(HmiButton b, HmiButtonEventType ev, string code)
        {
            HmiButtonEventHandler h = b.EventHandlers.Find(ev) ?? b.EventHandlers.Create(ev);
            h.Script.ScriptCode = code;
        }

        // Colour by tag value: bit (Bool = bit 0) -> on/off colour.
        static void ColorByBit(HmiScreenItemBase item, string property, string tag, int bit, Color on, Color off)
        {
            TagDynamization dyn = item.Dynamizations.Create<TagDynamization>(property);
            dyn.Tag = tag;
            MappingTableEntryBaseComposition entries = dyn.ValueConverter.MappingTable.Entries;
            try
            {
                IList<MappingTableEntryBitmask> bits = entries.Create(BitDynamizationType.SingleBit);
                // state 0 entry has Condition 0, state 1 entry 2^bit
                if (bits.Count >= 2)
                {
                    bits[0].Condition = 0; bits[0].Value = off;
                    bits[1].Condition = 1UL << bit; bits[1].Value = on;
                    return;
                }
                foreach (MappingTableEntryBitmask m in bits) m.Delete();
            }
            catch (Exception) { }
            MappingTableEntryRange r0 = entries.Create<MappingTableEntryRange>(); r0.From = 0; r0.To = 0; r0.Value = off;
            MappingTableEntryRange r1 = entries.Create<MappingTableEntryRange>(); r1.From = 1; r1.To = 1; r1.Value = on;
        }

        class Flow
        {
            public int X0 = 40, X, Y, RowH, MaxW;
            public void Place(IDictionary d, int w, int h, out int x, out int y)
            {
                if (d.Contains("x") && d.Contains("y")) { x = I(d, "x", 0); y = I(d, "y", 0); return; }
                if (X > X0 && X + w > MaxW - 20) { X = X0; Y += RowH + 20; RowH = 0; }
                x = X; y = Y; X += w + 30; RowH = Math.Max(RowH, h);
            }
        }

        static T NewItem<T>(HmiScreen scr, string name, int x, int y, int w, int h) where T : HmiScreenItemBase
        {
            T it = scr.ScreenItems.Create<T>(name);
            SetGeo(it, "Left", x); SetGeo(it, "Top", y); SetGeo(it, "Width", w); SetGeo(it, "Height", h);
            return it;
        }

        // Left/Top/Width/Height live on different base classes (simple items vs. controls): set them by name.
        static void SetGeo(object o, string prop, int v)
        {
            System.Reflection.PropertyInfo pi = o.GetType().GetProperty(prop);
            if (pi != null && pi.CanWrite) pi.SetValue(o, Convert.ChangeType(v, pi.PropertyType), null);
        }

        static int GetGeo(object o, string prop)
        {
            System.Reflection.PropertyInfo pi = o.GetType().GetProperty(prop);
            try { return pi == null ? 0 : Convert.ToInt32(pi.GetValue(o, null)); } catch { return 0; }
        }

        static string Unique(HmiScreen scr, string baseName)
        {
            string n = baseName; int i = 2;
            while (scr.ScreenItems.Find(n) != null) n = baseName + "_" + (i++);
            return n;
        }

        static void Label(HmiScreen scr, string name, string text, int x, int y, int w, int h)
        {
            HmiText t = NewItem<HmiText>(scr, Unique(scr, name), x, y, w, h);
            SetRichText(t.Text, text);
        }

        // elements: [{type: button|indicator|input|output|trend|alarm_view|alarm, ...}]
        public static object HmiUnifiedAdd(string station, string screenName, IEnumerable elements, string connection, string table)
        {
            HmiSoftware sw = UnifiedSoftware(station);
            List<string> conns = new List<string>();
            foreach (object c in (IEnumerable)sw.Connections) conns.Add(NameOfObj(c));
            if (string.IsNullOrEmpty(connection))
            {
                if (conns.Count == 0) throw new InvalidOperationException("The panel has no HMI connection. Call hmi_connect first.");
                connection = conns[0];
            }
            else if (!conns.Contains(connection))
                throw new InvalidOperationException("No HMI connection '" + connection + "'. Existing: " + string.Join(", ", conns.ToArray()));
            if (string.IsNullOrEmpty(table)) table = "Default tag table";
            string font = EnsureUnifiedFont(sw);

            List<IDictionary> els = new List<IDictionary>();
            if (elements != null) foreach (object o in elements) { IDictionary d = o as IDictionary; if (d != null) els.Add(d); }
            if (els.Count == 0) throw new ArgumentException("elements is empty.");
            if (els.Count > 30) throw new ArgumentException("At most 30 elements per call.");

            HmiScreen scr = null; bool newScreen = false;
            bool needsScreen = els.Any(d => (S(d, "type") ?? "") != "alarm");
            if (needsScreen)
            {
                if (string.IsNullOrEmpty(screenName)) throw new ArgumentException("screen is required for screen elements.");
                scr = sw.Screens.Find(screenName);
                if (scr == null) { scr = sw.Screens.Create(screenName); newScreen = true; }
            }

            Flow flow = new Flow();
            if (scr != null)
            {
                flow.MaxW = GetGeo(scr, "Width") > 0 ? GetGeo(scr, "Width") : 1280;
                int bottom = 10;
                foreach (HmiScreenItemBase si in scr.ScreenItems) bottom = Math.Max(bottom, GetGeo(si, "Top") + GetGeo(si, "Height"));
                flow.X = flow.X0; flow.Y = bottom + 20;
            }

            ArrayList report = new ArrayList(), tagsCreated = new ArrayList();
            foreach (IDictionary d in els)
            {
                string type = (S(d, "type") ?? "").ToLowerInvariant();
                try
                {
                    int x, y;
                    switch (type)
                    {
                        case "button":
                        {
                            string tn = EnsureUnifiedTag(sw, d, connection, table, tagsCreated);
                            string text = S(d, "text") ?? tn;
                            string action = (S(d, "action") ?? "momentary").ToLowerInvariant();
                            int w = I(d, "w", 160), h = I(d, "h", 50);
                            flow.Place(d, w, h, out x, out y);
                            string name = Unique(scr, S(d, "name") ?? "BTN_" + tn);
                            HmiButton b = NewItem<HmiButton>(scr, name, x, y, w, h);
                            SetRichText(b.Text, text);
                            string t = JsTag(tn);
                            switch (action)
                            {
                                case "momentary": Script(b, HmiButtonEventType.Down, t + ".Write(true);"); Script(b, HmiButtonEventType.Up, t + ".Write(false);"); break;
                                case "set": Script(b, HmiButtonEventType.Tapped, t + ".Write(true);"); break;
                                case "reset": Script(b, HmiButtonEventType.Tapped, t + ".Write(false);"); break;
                                case "toggle": Script(b, HmiButtonEventType.Tapped, t + ".Write(!" + t + ".Read());"); break;
                                case "value":
                                    string val = S(d, "value") ?? "0";
                                    double dv; bool num = double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dv);
                                    Script(b, HmiButtonEventType.Tapped, t + ".Write(" + (num || val == "true" || val == "false" ? val : "\"" + val.Replace("\"", "\\\"") + "\"") + ");");
                                    break;
                                default: throw new ArgumentException("action must be momentary, set, reset, toggle or value.");
                            }
                            report.Add(U.Obj("type", type, "name", name, "tag", tn, "action", action, "x", x, "y", y));
                            break;
                        }
                        case "indicator":
                        {
                            string tn = EnsureUnifiedTag(sw, d, connection, table, tagsCreated);
                            string label = S(d, "label") ?? tn;
                            int bit = I(d, "bit", 0);
                            Color on = ColorOf(S(d, "on_color"), Color.FromArgb(0, 176, 80)), off = ColorOf(S(d, "off_color"), Color.FromArgb(128, 128, 128));
                            int w = 300, h = 40;
                            flow.Place(d, w, h, out x, out y);
                            string name = Unique(scr, S(d, "name") ?? "LED_" + tn + (bit > 0 ? "_" + bit : ""));
                            HmiRectangle r = NewItem<HmiRectangle>(scr, name, x, y, 40, 40);
                            r.BackColor = off;
                            ColorByBit(r, "BackColor", tn, bit, on, off);
                            Label(scr, "L_" + name, label, x + 50, y, w - 50, h);
                            report.Add(U.Obj("type", type, "name", name, "tag", tn, "bit", bit, "on", ColorTranslator.ToHtml(on), "off", ColorTranslator.ToHtml(off), "x", x, "y", y));
                            break;
                        }
                        case "input":
                        case "output":
                        {
                            string tn = EnsureUnifiedTag(sw, d, connection, table, tagsCreated);
                            string label = S(d, "label") ?? tn, unit = S(d, "unit");
                            int dec = I(d, "decimals", 0);
                            int w = 480, h = 40;
                            flow.Place(d, w, h, out x, out y);
                            string name = Unique(scr, S(d, "name") ?? (type == "input" ? "IN_" : "IO_") + tn);
                            Label(scr, "L_" + name, label, x, y, 220, h);
                            HmiIOField io = NewItem<HmiIOField>(scr, name, x + 230, y, 150, h);
                            io.IOFieldType = type == "input" ? HmiIOFieldType.InputOutput : HmiIOFieldType.Output;
                            if (dec > 0 && d.Contains("scale")) io.ShiftDecimalPlaces = (byte)dec;
                            TagDynamization dyn = io.Dynamizations.Create<TagDynamization>("ProcessValue");
                            dyn.Tag = tn;
                            if (!string.IsNullOrEmpty(unit)) Label(scr, "U_" + name, unit, x + 390, y, 90, h);
                            report.Add(U.Obj("type", type, "name", name, "tag", tn, "x", x, "y", y));
                            break;
                        }
                        case "trend":
                        {
                            IEnumerable series = d.Contains("series") ? d["series"] as IEnumerable : null;
                            List<IDictionary> ser = new List<IDictionary>();
                            if (series != null) foreach (object o in series) { IDictionary sd = o as IDictionary; if (sd != null) ser.Add(sd); else if (o is string) { Hashtable h0 = new Hashtable(); h0["tag"] = o; h0["plc"] = o; ser.Add(h0); } }
                            if (ser.Count == 0) throw new ArgumentException("trend needs series: [{plc|tag, color?}]");
                            int w = I(d, "w", Math.Max(400, flow.MaxW - 80)), h = I(d, "h", 360);
                            flow.Place(d, w, h, out x, out y);
                            string name = Unique(scr, S(d, "name") ?? "Trend_1");
                            HmiTrendControl tc = NewItem<HmiTrendControl>(scr, name, x, y, w, h);
                            HmiTrendAreaPart area = tc.TrendAreas[0];
                            string[] palette = { "#0070C0", "#DC1E1E", "#00B050", "#FF8C00", "#7030A0", "#808080" };
                            ArrayList sr = new ArrayList();
                            for (int i = 0; i < ser.Count; i++)
                            {
                                string tn = EnsureUnifiedTag(sw, ser[i], connection, table, tagsCreated);
                                HmiTrendPart tp = i < area.Trends.Count ? area.Trends[i] : area.Trends.Create();
                                tp.DataSourceY.Source = tn;
                                tp.LineColor = ColorOf(S(ser[i], "color"), ColorTranslator.FromHtml(palette[i % palette.Length]));
                                tp.LineWidth = 2;
                                string lbl = S(ser[i], "label");
                                if (lbl != null) foreach (Siemens.Engineering.MultilingualTextItem mi in tp.DisplayName.Items) mi.Text = lbl;
                                sr.Add(tn);
                            }
                            report.Add(U.Obj("type", type, "name", name, "series", sr, "x", x, "y", y, "w", w, "h", h));
                            break;
                        }
                        case "alarm_view":
                        {
                            int w = I(d, "w", Math.Max(400, flow.MaxW - 80)), h = I(d, "h", 300);
                            flow.Place(d, w, h, out x, out y);
                            string name = Unique(scr, S(d, "name") ?? "Alarms_1");
                            NewItem<HmiAlarmControl>(scr, name, x, y, w, h);
                            report.Add(U.Obj("type", type, "name", name, "x", x, "y", y, "w", w, "h", h));
                            break;
                        }
                        case "alarm":
                        {
                            string tn = EnsureUnifiedTag(sw, d, connection, table, tagsCreated);
                            string text = S(d, "text") ?? tn;
                            string name = S(d, "name") ?? ("ALM_" + tn + (d.Contains("bit") ? "_" + I(d, "bit", 0) : ""));
                            HmiDiscreteAlarm al = sw.DiscreteAlarms.Find(name);
                            string st = "exists";
                            if (al == null)
                            {
                                al = sw.DiscreteAlarms.Create(name);
                                al.RaisedStateTag = tn;
                                al.RaisedStateTagBitNumber = (uint)I(d, "bit", 0);
                                al.AlarmClass = S(d, "class") ?? "Alarm";
                                SetRichText(al.EventText, text);
                                st = "created";
                            }
                            report.Add(U.Obj("type", type, "name", name, "tag", tn, "bit", I(d, "bit", 0), "class", al.AlarmClass, "status", st));
                            break;
                        }
                        default:
                            throw new ArgumentException("unknown type '" + type + "' (button, indicator, input, output, trend, alarm_view, alarm)");
                    }
                }
                catch (Exception ex)
                {
                    report.Add(U.Obj("type", type, "error", (ex.InnerException ?? ex).Message));
                }
            }
            return U.Obj("station", station, "screen", screenName, "screen_created", newScreen, "connection", connection,
                "font", font, "hmi_tags_created", tagsCreated, "elements", report,
                "note", "Project not saved; call project_save. Compile HMI_x in TIA to check.");
        }
    }
}
#endif
