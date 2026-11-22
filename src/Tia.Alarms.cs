using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Alarm;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;

namespace TiaAssistant
{
    // PLC alarm texts (Program_Alarm, ProDiag ...) through the xlsx export/import of PlcAlarmTextProvider,
    // and SCL source text of existing blocks.
    public static partial class Tia
    {
        static readonly XNamespace Xl = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        static int ColIndex(string cellRef)
        {
            int n = 0;
            foreach (char ch in cellRef) { if (ch >= 'A' && ch <= 'Z') n = n * 26 + (ch - 'A' + 1); else break; }
            return n - 1;
        }

        static string ColName(int idx)
        {
            string s = ""; idx++;
            while (idx > 0) { int m = (idx - 1) % 26; s = (char)('A' + m) + s; idx = (idx - 1) / 26; }
            return s;
        }

        static string FirstSheetPath(ZipArchive z)
        {
            foreach (ZipArchiveEntry e in z.Entries)
                if (e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml")) return e.FullName;
            throw new InvalidOperationException("The alarm text export has no worksheet.");
        }

        static XDocument ReadEntry(ZipArchive z, string path)
        {
            ZipArchiveEntry e = z.GetEntry(path);
            if (e == null) return null;
            using (Stream s = e.Open()) return XDocument.Load(s);
        }

        static string CellText(XElement c, List<string> shared)
        {
            string t = (string)c.Attribute("t");
            if (t == "inlineStr") { StringBuilder sb = new StringBuilder(); foreach (XElement x in c.Descendants(Xl + "t")) sb.Append(x.Value); return sb.ToString(); }
            XElement v = c.Element(Xl + "v");
            if (v == null) return "";
            if (t == "s") { int i; return int.TryParse(v.Value, out i) && i < shared.Count ? shared[i] : ""; }
            return v.Value;
        }

        static List<string> SharedStrings(ZipArchive z)
        {
            List<string> r = new List<string>();
            XDocument d = ReadEntry(z, "xl/sharedStrings.xml");
            if (d == null) return r;
            foreach (XElement si in d.Root.Elements(Xl + "si"))
            { StringBuilder sb = new StringBuilder(); foreach (XElement t in si.Descendants(Xl + "t")) sb.Append(t.Value); r.Add(sb.ToString()); }
            return r;
        }

        // Rows as dictionaries column-index -> text, keyed by row number.
        static SortedDictionary<int, Dictionary<int, string>> SheetRows(XDocument sheet, List<string> shared)
        {
            SortedDictionary<int, Dictionary<int, string>> rows = new SortedDictionary<int, Dictionary<int, string>>();
            foreach (XElement row in sheet.Descendants(Xl + "row"))
            {
                int rn = (int)row.Attribute("r");
                Dictionary<int, string> cells = new Dictionary<int, string>();
                foreach (XElement c in row.Elements(Xl + "c")) cells[ColIndex((string)c.Attribute("r"))] = CellText(c, shared);
                rows[rn] = cells;
            }
            return rows;
        }

        static void SetCell(XDocument sheet, int rowNum, int col, string text)
        {
            XElement row = null;
            foreach (XElement r in sheet.Descendants(Xl + "row")) if ((int)r.Attribute("r") == rowNum) { row = r; break; }
            if (row == null) throw new InvalidOperationException("Row " + rowNum + " not found in alarm text sheet.");
            string cref = ColName(col) + rowNum;
            XElement cell = null, after = null;
            foreach (XElement c in row.Elements(Xl + "c"))
            {
                int ci = ColIndex((string)c.Attribute("r"));
                if (ci == col) { cell = c; break; }
                if (ci < col) after = c;
            }
            if (cell == null)
            {
                cell = new XElement(Xl + "c", new XAttribute("r", cref));
                if (after != null) after.AddAfterSelf(cell); else row.AddFirst(cell);
            }
            cell.RemoveNodes();
            if (cell.Attribute("t") != null) cell.Attribute("t").Remove();
            cell.Add(new XAttribute("t", "inlineStr"), new XElement(Xl + "is", new XElement(Xl + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
        }

        static string LogTail(PlcAlarmTextXlsxResult r)
        {
            try { if (r != null && r.LogFilePath != null && r.LogFilePath.Exists) { string s = File.ReadAllText(r.LogFilePath.FullName); return s.Length > 3000 ? s.Substring(s.Length - 3000) : s; } }
            catch { }
            return null;
        }

        // texts: null -> read only. Otherwise list of {alarm, text[, column]}.
        public static object PlcAlarmTexts(string station, object textsArg, string column)
        {
            PlcSoftware plc = PlcOf(station);
            PlcAlarmTextProvider prov = plc.GetService<PlcAlarmTextProvider>();
            if (prov == null) throw new InvalidOperationException("This PLC offers no alarm text service.");
            IEnumerable<Language> langs = Proj().LanguageSettings.ActiveLanguages;
            string file = Path.Combine(Path.GetTempPath(), "tia_assistant_alarms_" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                PlcAlarmTextXlsxResult ex = prov.ExportInstanceTextsToXlsx(new FileInfo(file), langs, PlcAlarmTextXlsxExportOption.None);
                if (ex.State == PlcAlarmTextXlsxResultState.Error || !File.Exists(file))
                    throw new InvalidOperationException("Alarm text export failed. Compile the PLC first. Log: " + LogTail(ex));

                List<object> changes = new List<object>();
                IEnumerable list = (textsArg is string) ? null : textsArg as IEnumerable;
                using (ZipArchive z = ZipFile.Open(file, list == null ? ZipArchiveMode.Read : ZipArchiveMode.Update))
                {
                    List<string> shared = SharedStrings(z);
                    string sheetPath = FirstSheetPath(z);
                    XDocument sheet = ReadEntry(z, sheetPath);
                    SortedDictionary<int, Dictionary<int, string>> rows = SheetRows(sheet, shared);
                    if (rows.Count == 0) return U.Obj("station", station, "alarms", new ArrayList(), "note", "No PLC alarms with instance texts.");
                    int headerRow = 0; foreach (int k in rows.Keys) { headerRow = k; break; }
                    Dictionary<int, string> header = rows[headerRow];
                    int maxCol = 0; foreach (int k in header.Keys) maxCol = Math.Max(maxCol, k);

                    if (list == null)
                    {
                        ArrayList hdr = new ArrayList(); for (int i = 0; i <= maxCol; i++) hdr.Add(header.ContainsKey(i) ? header[i] : "");
                        ArrayList data = new ArrayList();
                        foreach (KeyValuePair<int, Dictionary<int, string>> kv in rows)
                        {
                            if (kv.Key == headerRow) continue;
                            if (data.Count >= 200) break;
                            ArrayList cells = new ArrayList(); for (int i = 0; i <= maxCol; i++) cells.Add(kv.Value.ContainsKey(i) ? kv.Value[i] : "");
                            data.Add(cells);
                        }
                        return U.Obj("station", station, "columns", hdr, "rows", data, "row_count", rows.Count - 1);
                    }

                    // Which column holds the alarm text.
                    int textCol = -1;
                    foreach (KeyValuePair<int, string> h in header)
                    {
                        if (!string.IsNullOrEmpty(column)) { if (string.Equals(h.Value.Trim(), column.Trim(), StringComparison.OrdinalIgnoreCase)) { textCol = h.Key; break; } }
                        else if (h.Value.IndexOf("alarm text", StringComparison.OrdinalIgnoreCase) >= 0 || h.Value.IndexOf("alarm_text", StringComparison.OrdinalIgnoreCase) >= 0) { textCol = h.Key; break; }
                    }
                    if (textCol < 0)
                    {
                        ArrayList hdr = new ArrayList(); foreach (string v in header.Values) hdr.Add(v);
                        throw new InvalidOperationException("Could not find the alarm text column. Pass 'column' with one of these headers: " + string.Join(" | ", (string[])hdr.ToArray(typeof(string))));
                    }

                    int changed = 0;
                    foreach (object o in list)
                    {
                        IDictionary d = o as IDictionary;
                        if (d == null) continue;
                        string key = Convert.ToString(d["alarm"]), text = Convert.ToString(d["text"]);
                        List<int> exact = new List<int>(), loose = new List<int>();
                        foreach (KeyValuePair<int, Dictionary<int, string>> kv in rows)
                        {
                            if (kv.Key == headerRow) continue;
                            foreach (string cell in kv.Value.Values)
                            {
                                string c = (cell ?? "").Replace("\"", "");
                                if (string.Equals(c, key, StringComparison.OrdinalIgnoreCase)) { exact.Add(kv.Key); break; }
                                if (c.EndsWith(key, StringComparison.OrdinalIgnoreCase) || c.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) { loose.Add(kv.Key); break; }
                            }
                        }
                        List<int> hit = exact.Count > 0 ? exact : loose;
                        if (hit.Count != 1) { changes.Add(U.Obj("alarm", key, "status", hit.Count == 0 ? "not found" : "ambiguous (" + hit.Count + " rows) - use the full name")); continue; }
                        SetCell(sheet, hit[0], textCol, text);
                        changes.Add(U.Obj("alarm", key, "status", "set", "row", hit[0]));
                        changed++;
                    }
                    if (changed == 0) return U.Obj("station", station, "changes", changes, "imported", false);
                    ZipArchiveEntry old = z.GetEntry(sheetPath); old.Delete();
                    ZipArchiveEntry ne = z.CreateEntry(sheetPath);
                    using (Stream s = ne.Open()) sheet.Save(s);
                }
                PlcAlarmTextXlsxResult im = prov.ImportInstanceTextsFromXlsx(new FileInfo(file), langs);
                return U.Obj("station", station, "changes", changes, "imported", im.State != PlcAlarmTextXlsxResultState.Error,
                    "state", im.State.ToString(), "log", im.State == PlcAlarmTextXlsxResultState.OK ? null : LogTail(im));
            }
            finally { try { File.Delete(file); } catch { } }
        }

        public static object BlockSource(string station, string blockName)
        {
            PlcSoftware plc = PlcOf(station);
            PlcBlock b = FindBlock(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, blockName);
            if (b == null) throw new InvalidOperationException("Block '" + blockName + "' not found in '" + plc.Name + "'.");
            string file = Path.Combine(Path.GetTempPath(), "tia_assistant_" + Guid.NewGuid().ToString("N") + ".scl");
            try
            {
                plc.ExternalSourceGroup.GenerateSource(new PlcBlock[] { b }, new FileInfo(file), GenerateOptions.None);
                return U.Obj("block", b.Name, "language", b.ProgrammingLanguage.ToString(), "source", File.ReadAllText(file, Encoding.UTF8));
            }
            finally { try { File.Delete(file); } catch { } }
        }
    }
}
