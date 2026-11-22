#if !TIA_V18 && !NO_UNIFIED
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.HmiTags;
using Siemens.Engineering.HmiUnified.UI.Base;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.Enum;
using Siemens.Engineering.HmiUnified.UI.Screens;
using Siemens.Engineering.HmiUnified.UI.Shapes;
using Siemens.Engineering.HmiUnified.UI.Widgets;

namespace TiaAssistant
{
    // WinCC Unified: HMI tags bound symbolically to PLC tags / DB members, and a screen of labelled output IO fields.
    // Unlike classic panels, Unified binds PLC tags through the object API (HmiTag.Connection + HmiTag.PlcTag), no XML.
    // Learned 20.11.2026 on test_v21 (HMI_2, 6AV2 128-3KB06): Tags.Create(name, table); Screens.Create(name);
    // ScreenItems.Create<HmiIOField>(name); IOField.Dynamizations.Create<TagDynamization>("ProcessValue").Tag = hmiTag;
    // texts are rich text "<body><p>..</p></body>"; HmiDataType is locked to the PLC type (scaled Int stays Int ->
    // use HmiEndValue x10^n with ShiftDecimalPlaces n on the IO field for decimals).
    public static partial class Tia
    {
        static HmiSoftware UnifiedSoftware(string station)
        {
            Device dev = FindDevice(station);
            foreach (DeviceItem i in ItemsDeep(dev.DeviceItems))
            {
                SoftwareContainer sc = i.GetService<SoftwareContainer>();
                if (sc != null && sc.Software is HmiSoftware) return (HmiSoftware)sc.Software;
            }
            throw new InvalidOperationException("'" + station + "' is not a WinCC Unified device (no HmiSoftware). Classic panels: use hmi_tags_from_plc.");
        }

        static string NameOfObj(object o)
        {
            if (o == null) return null;
            var p = o.GetType().GetProperty("Name");
            return p == null ? null : Convert.ToString(p.GetValue(o, null));
        }

        static void SetRichText(Siemens.Engineering.MultilingualText mt, string text)
        {
            if (mt == null) return;
            string v = "<body><p>" + SecurityElement.Escape(text ?? "") + "</p></body>";
            foreach (Siemens.Engineering.MultilingualTextItem it in mt.Items) it.Text = v;
        }

        static string TagNameFor(string plcRef)
        {
            string s = (plcRef ?? "").Replace("\"", "").Replace(".", "_").Replace("[", "_").Replace("]", "").Replace(" ", "_");
            return s.Length > 0 ? s : "Tag";
        }

        static double? Num(IDictionary d, string k)
        {
            if (d == null || !d.Contains(k) || d[k] == null) return null;
            return Convert.ToDouble(d[k], System.Globalization.CultureInfo.InvariantCulture);
        }

        // items: [{label, plc, unit?, hmi_tag?, decimals?, scale?: {plc_min, plc_max, hmi_min, hmi_max}}]
        public static object HmiUnifiedScreen(string station, string screenName, string title, IEnumerable items, string connection, string table, bool dryRun)
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

            ArrayList report = new ArrayList();
            List<IDictionary> rows = new List<IDictionary>();
            if (items != null) foreach (object o in items) { IDictionary d = o as IDictionary; if (d != null) rows.Add(d); }
            if (rows.Count == 0) throw new ArgumentException("items is empty.");
            if (rows.Count > 12) throw new ArgumentException("At most 12 items per screen.");

            if (dryRun)
            {
                foreach (IDictionary d in rows)
                {
                    string plc = Convert.ToString(d["plc"]);
                    string tn = d.Contains("hmi_tag") && d["hmi_tag"] != null ? Convert.ToString(d["hmi_tag"]) : TagNameFor(plc);
                    report.Add(U.Obj("label", d["label"], "plc", plc, "hmi_tag", tn, "exists", sw.Tags.Find(tn) != null, "scaled", d.Contains("scale")));
                }
                return U.Obj("station", station, "screen", screenName, "screen_exists", sw.Screens.Find(screenName) != null, "connection", connection, "items", report, "dry_run", true);
            }

            string font = EnsureUnifiedFont(sw);
            HmiScreen scr = sw.Screens.Find(screenName);
            bool newScreen = scr == null;
            if (newScreen) scr = sw.Screens.Create(screenName);

            int x0 = 40, top = 30, rowH = 60;
            if (!string.IsNullOrEmpty(title) && scr.ScreenItems.Find("T_Title") == null)
            {
                HmiText t = scr.ScreenItems.Create<HmiText>("T_Title");
                t.Left = x0; t.Top = top; t.Width = 900; t.Height = 50;
                SetRichText(t.Text, title);
            }
            // continue below existing rows (idempotent: an item whose IO field exists is skipped)
            int row = scr.ScreenItems.Cast<HmiScreenItemBase>().Count(si => NameOfObj(si).StartsWith("IO_"));

            foreach (IDictionary d in rows)
            {
                string plc = Convert.ToString(d["plc"]).Replace("\"", "");
                string label = d.Contains("label") && d["label"] != null ? Convert.ToString(d["label"]) : plc;
                string tn = d.Contains("hmi_tag") && d["hmi_tag"] != null ? Convert.ToString(d["hmi_tag"]) : TagNameFor(plc);
                string unit = d.Contains("unit") ? Convert.ToString(d["unit"]) : null;
                IDictionary sc = d.Contains("scale") ? d["scale"] as IDictionary : null;
                int dec = d.Contains("decimals") && d["decimals"] != null ? Convert.ToInt32(d["decimals"]) : 0;

                // HMI tag (reused when it exists)
                HmiTag tag = sw.Tags.Find(tn);
                string tagStatus = "exists";
                if (tag == null)
                {
                    tag = sw.Tags.Create(tn, table);
                    tag.Connection = connection;
                    tag.PlcTag = plc;
                    tagStatus = "created";
                    if (sc != null)
                    {
                        tag.LinearScaling = true;
                        double? a;
                        if ((a = Num(sc, "plc_min")) != null) tag.PlcStartValue = a.Value;
                        if ((a = Num(sc, "plc_max")) != null) tag.PlcEndValue = a.Value;
                        if ((a = Num(sc, "hmi_min")) != null) tag.HmiStartValue = a.Value * Math.Pow(10, dec);
                        if ((a = Num(sc, "hmi_max")) != null) tag.HmiEndValue = a.Value * Math.Pow(10, dec);
                    }
                }

                string ioName = "IO_" + tn;
                if (scr.ScreenItems.Find(ioName) != null)
                {
                    report.Add(U.Obj("label", label, "hmi_tag", tn, "tag", tagStatus, "io_field", "exists"));
                    continue;
                }
                int y = top + 80 + row * rowH;
                HmiText lbl = scr.ScreenItems.Create<HmiText>("L_" + tn);
                lbl.Left = x0; lbl.Top = y; lbl.Width = 320; lbl.Height = 40;
                SetRichText(lbl.Text, label);

                HmiIOField io = scr.ScreenItems.Create<HmiIOField>(ioName);
                io.Left = x0 + 340; io.Top = y; io.Width = 200; io.Height = 40;
                io.IOFieldType = HmiIOFieldType.Output;
                if (sc != null && dec > 0) io.ShiftDecimalPlaces = (byte)dec;
                TagDynamization dyn = io.Dynamizations.Create<TagDynamization>("ProcessValue");
                dyn.Tag = tn;

                if (!string.IsNullOrEmpty(unit))
                {
                    HmiText u = scr.ScreenItems.Create<HmiText>("U_" + tn);
                    u.Left = x0 + 560; u.Top = y; u.Width = 120; u.Height = 40;
                    SetRichText(u.Text, unit);
                }
                report.Add(U.Obj("label", label, "plc", plc, "hmi_tag", tn, "tag", tagStatus, "io_field", "created", "plc_type", dyn.DataType));
                row++;
            }
            return U.Obj("station", station, "screen", screenName, "screen_created", newScreen, "connection", connection, "items", report, "font", font,
                "note", "Output IO fields bound to HMI tags bound to PLC values. Project not saved; call project_save. Compile the HMI to check.");
        }

        // WinCC Unified software: Openness V21 offers no ICompilable on HmiSoftware (tried 20.11). What it offers is
        // Validate() on every Unified object (HmiBase / UIBase / tags / connections / runtime settings). This walks the
        // whole HMI and reports errors and warnings per object and property - the closest offline check to a compile.
        public static Dictionary<string, object> UnifiedValidate(Siemens.Engineering.HW.Device dev, ref int errors, ref int warnings)
        {
            HmiSoftware sw = null;
            foreach (DeviceItem i in ItemsDeep(dev.DeviceItems))
            {
                SoftwareContainer sc = i.GetService<SoftwareContainer>();
                if (sc != null && sc.Software is HmiSoftware) { sw = (HmiSoftware)sc.Software; break; }
            }
            if (sw == null) return null;

            // a real compiler first, in case a later TIA version provides one
            try
            {
                Siemens.Engineering.Compiler.ICompilable c = ((Siemens.Engineering.IEngineeringServiceProvider)(object)sw).GetService<Siemens.Engineering.Compiler.ICompilable>();
                if (c != null) return RunCompile("software (HmiSoftware)", c, ref errors, ref warnings);
            }
            catch (Exception) { }

            ArrayList msgs = new ArrayList();
            int e = 0, w = 0, objects = 0;
            Action<string, Func<IList<Siemens.Engineering.HmiUnified.Common.HmiValidationResult>>> check = (where, f) =>
            {
                objects++;
                IList<Siemens.Engineering.HmiUnified.Common.HmiValidationResult> res;
                try { res = f(); }
                catch (Exception ex) { msgs.Add(U.Obj("object", where, "state", "Error", "text", "Validate failed: " + (ex.InnerException ?? ex).Message)); e++; return; }
                if (res == null) return;
                foreach (var r in res)
                {
                    if (r.Errors != null) foreach (string t in r.Errors) { e++; if (msgs.Count < 60) msgs.Add(U.Obj("object", where, "property", r.PropertyName, "state", "Error", "text", t)); }
                    if (r.Warnings != null) foreach (string t in r.Warnings) { w++; if (msgs.Count < 60) msgs.Add(U.Obj("object", where, "property", r.PropertyName, "state", "Warning", "text", t)); }
                }
            };
            check("RuntimeSettings", () => sw.RuntimeSettings.Validate());
            // (a missing start screen is reported by RuntimeSettings.Validate itself - tested 21.11)
            foreach (var c in sw.Connections) { var cc = c; check("Connection " + cc.Name, () => cc.Validate()); }
            foreach (HmiTag t in sw.Tags) { var tt = t; check("Tag " + tt.Name, () => tt.Validate()); }
            foreach (HmiScreen scr in sw.Screens)
            {
                var ss = scr;
                check("Screen " + ss.Name, () => ss.Validate());
                foreach (HmiScreenItemBase si in ss.ScreenItems) { var it = si; check("Screen " + ss.Name + "/" + NameOfObj(it), () => it.Validate()); }
            }
            foreach (var a in sw.DiscreteAlarms) { var aa = a; check("DiscreteAlarm " + aa.Name, () => aa.Validate()); }
            foreach (var a in sw.AnalogAlarms) { var aa = a; check("AnalogAlarm " + NameOfObj(aa), () => aa.Validate()); }
            errors += e; warnings += w;
            return U.Obj("scope", "software (WinCC Unified, validation)", "state", e > 0 ? "Error" : (w > 0 ? "Warning" : "Success"),
                "errors", e, "warnings", w, "objects_checked", objects, "messages", msgs,
                "note", "Openness has no Unified compiler; this is Validate() on every HMI object. Machine-dependent checks (fonts installed on the PC, encrypted-transfer password) only show in the TIA compile.");
        }
    }
}
#endif
