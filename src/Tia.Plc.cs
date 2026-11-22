using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;

namespace TiaAssistant
{
    // PLC software: tag tables, SCL block generation, compiling.
    public static partial class Tia
    {
        // ---------------- tags ----------------

        static PlcTagTable FindTagTable(PlcTagTableComposition tables, PlcTagTableUserGroupComposition groups, string name)
        {
            PlcTagTable t = tables.Find(name);
            if (t != null) return t;
            foreach (PlcTagTableUserGroup g in groups)
            {
                t = FindTagTable(g.TagTables, g.Groups, name);
                if (t != null) return t;
            }
            return null;
        }

        static void AllTagNames(PlcTagTableComposition tables, PlcTagTableUserGroupComposition groups, Dictionary<string, string> names)
        {
            foreach (PlcTagTable t in tables)
                foreach (PlcTag tag in t.Tags) names[tag.Name.ToUpperInvariant()] = t.Name;
            foreach (PlcTagTableUserGroup g in groups) AllTagNames(g.TagTables, g.Groups, names);
        }

        static PlcTag FindTag(PlcTagTableComposition tables, PlcTagTableUserGroupComposition groups, string name)
        {
            foreach (PlcTagTable t in tables)
            {
                PlcTag tag = t.Tags.Find(name);
                if (tag != null) return tag;
            }
            foreach (PlcTagTableUserGroup g in groups)
            {
                PlcTag tag = FindTag(g.TagTables, g.Groups, name);
                if (tag != null) return tag;
            }
            return null;
        }

        // Writes the comment in every active project language (usually just one).
        static void SetComment(PlcTag tag, string text)
        {
            foreach (MultilingualTextItem item in tag.Comment.Items) item.Text = text;
        }

        static string NormAddr(string a) { return a == null ? "" : a.Replace("%", "").Replace(" ", "").ToUpperInvariant(); }

        public static object TagsAdd(string deviceName, string tableName, object tagsArg)
        {
            PlcSoftware plc = PlcOf(deviceName);
            IEnumerable tags = tagsArg as IEnumerable;
            if (tags == null || tagsArg is string) throw new ArgumentException("'tags' must be an array.");

            bool tableCreated = false;
            PlcTagTable table = FindTagTable(plc.TagTableGroup.TagTables, plc.TagTableGroup.Groups, tableName);
            if (table == null) { table = plc.TagTableGroup.TagTables.Create(tableName); tableCreated = true; }

            Dictionary<string, string> existing = new Dictionary<string, string>();
            AllTagNames(plc.TagTableGroup.TagTables, plc.TagTableGroup.Groups, existing);

            int created = 0, commented = 0;
            ArrayList skipped = new ArrayList();
            ArrayList failed = new ArrayList();
            foreach (object o in tags)
            {
                Dictionary<string, object> t = o as Dictionary<string, object>;
                if (t == null) { failed.Add(U.Obj("error", "tag entry is not an object")); continue; }
                string name = U.Str(t, "name", false), type = U.Str(t, "data_type", false), addr = U.Str(t, "address", false);
                string comment = U.Str(t, "comment", false);
                if (name == null || type == null || addr == null) { failed.Add(U.Obj("name", name, "error", "name, data_type and address are required")); continue; }
                if (!addr.StartsWith("%")) addr = "%" + addr;
                string inTable;
                if (existing.TryGetValue(name.ToUpperInvariant(), out inTable))
                {
                    // Same tag at the same address: nothing is recreated, but a given comment is applied.
                    PlcTag old = FindTag(plc.TagTableGroup.TagTables, plc.TagTableGroup.Groups, name);
                    bool same = old != null && NormAddr(old.LogicalAddress) == NormAddr(addr);
                    if (same && comment != null)
                    {
                        try { SetComment(old, comment); commented++; }
                        catch (Exception ex) { failed.Add(U.Obj("name", name, "error", "comment not written: " + U.Describe(ex))); }
                    }
                    else skipped.Add(U.Obj("name", name, "exists_in_table", inTable,
                        "existing_address", old == null ? null : old.LogicalAddress, "same_address", same));
                    continue;
                }
                try
                {
                    PlcTag tag = table.Tags.Create(name, type, addr);
                    existing[name.ToUpperInvariant()] = table.Name;
                    created++;
                    if (comment != null)
                    {
                        try { SetComment(tag, comment); commented++; }
                        catch (Exception ex) { failed.Add(U.Obj("name", name, "error", "tag created, comment not written: " + U.Describe(ex))); }
                    }
                }
                catch (Exception ex) { failed.Add(U.Obj("name", name, "address", addr, "error", U.Describe(ex))); }
            }
            return U.Obj("table", table.Name, "table_created", tableCreated, "created", created, "comments_written", commented,
                "skipped_existing", skipped, "failed", failed, "tags_in_table", table.Tags.Count);
        }

        // ---------------- SCL ----------------

        static PlcBlock FindBlock(PlcBlockComposition blocks, PlcBlockUserGroupComposition groups, string name)
        {
            PlcBlock b = blocks.Find(name);
            if (b != null) return b;
            foreach (PlcBlockUserGroup g in groups)
            {
                b = FindBlock(g.Blocks, g.Groups, name);
                if (b != null) return b;
            }
            return null;
        }

        static PlcType FindType(PlcTypeComposition types, PlcTypeUserGroupComposition groups, string name)
        {
            PlcType t = types.Find(name);
            if (t != null) return t;
            foreach (PlcTypeUserGroup g in groups)
            {
                t = FindType(g.Types, g.Groups, name);
                if (t != null) return t;
            }
            return null;
        }

        public static object SclImport(string deviceName, string source, string sourceName, bool overwrite)
        {
            PlcSoftware plc = PlcOf(deviceName);

            // What does this source declare?
            List<string> declared = new List<string>();
            List<string> collisions = new List<string>();
            MatchCollection mc = Regex.Matches(source,
                @"^\s*(FUNCTION_BLOCK|FUNCTION|DATA_BLOCK|ORGANIZATION_BLOCK|TYPE)\s+(""[^""]+""|[A-Za-z_][A-Za-z0-9_]*)",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            foreach (Match m in mc)
            {
                string kind = m.Groups[1].Value.ToUpperInvariant();
                string n = m.Groups[2].Value.Trim('"');
                declared.Add(kind + " " + n);
                bool exists = kind == "TYPE"
                    ? FindType(plc.TypeGroup.Types, plc.TypeGroup.Groups, n) != null
                    : FindBlock(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, n) != null;
                if (exists) collisions.Add(kind + " " + n);
            }
            if (declared.Count == 0)
                throw new ArgumentException("The source declares no FUNCTION_BLOCK / FUNCTION / DATA_BLOCK / ORGANIZATION_BLOCK / TYPE.");
            if (collisions.Count > 0 && !overwrite)
                throw new InvalidOperationException("These objects already exist and would be replaced: " + string.Join(", ", collisions.ToArray()) +
                    ". Nothing was changed. Ask the engineer, then call again with overwrite=true.");

            // TYPE-only sources are .udt, everything else .scl
            bool onlyTypes = true;
            foreach (string d in declared) if (!d.StartsWith("TYPE ")) onlyTypes = false;
            string file = Path.Combine(Path.GetTempPath(), "tia_assistant_" + Guid.NewGuid().ToString("N") + (onlyTypes ? ".udt" : ".scl"));
            File.WriteAllText(file, source, new UTF8Encoding(true));
            try
            {
                PlcExternalSource old = plc.ExternalSourceGroup.ExternalSources.Find(sourceName);
                if (old != null) old.Delete();
                PlcExternalSource src = plc.ExternalSourceGroup.ExternalSources.CreateFromFile(sourceName, file);
                try { src.GenerateBlocksFromSource(); }
                finally { try { src.Delete(); } catch { } }
            }
            finally { try { File.Delete(file); } catch { } }

            return U.Obj("generated", declared, "replaced", collisions,
                "note", "Blocks were generated but not compiled yet. Run compile to verify them.");
        }

        // ---------------- block exchange (SimaticML) ----------------
        // LAD/FBD blocks cannot be created from text sources; they travel as SimaticML XML.

        public static object BlockExport(string deviceName, string blockName)
        {
            PlcSoftware plc = PlcOf(deviceName);
            PlcBlock b = FindBlock(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, blockName);
            if (b == null) throw new InvalidOperationException("Block '" + blockName + "' not found in '" + plc.Name + "'.");
            string file = Path.Combine(Path.GetTempPath(), "tia_assistant_" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                b.Export(new FileInfo(file), ExportOptions.WithDefaults);
                return File.ReadAllText(file, Encoding.UTF8);
            }
            finally { try { File.Delete(file); } catch { } }
        }

        public static object BlockImport(string deviceName, string xml, bool overwrite)
        {
            PlcSoftware plc = PlcOf(deviceName);
            Match m = Regex.Match(xml, @"<SW\.Blocks\.[A-Za-z]+[^>]*>.*?<Name>([^<]+)</Name>", RegexOptions.Singleline);
            string name = m.Success ? m.Groups[1].Value : null;
            if (name != null && !overwrite && FindBlock(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, name) != null)
                throw new InvalidOperationException("Block '" + name + "' already exists and would be replaced. Nothing was changed. Ask the engineer, then call again with overwrite=true.");
            string file = Path.Combine(Path.GetTempPath(), "tia_assistant_" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(file, xml, new UTF8Encoding(true));
            try
            {
                IList<PlcBlock> blocks = plc.BlockGroup.Blocks.Import(new FileInfo(file), overwrite ? ImportOptions.Override : ImportOptions.None);
                ArrayList names = new ArrayList();
                foreach (PlcBlock b in blocks) names.Add(b.Name);
                return U.Obj("imported", names, "note", "Imported but not compiled yet. Run compile to verify.");
            }
            finally { try { File.Delete(file); } catch { } }
        }

        // ---------------- compile ----------------

        public static object Compile(string deviceName, string scope)
        {
            Device dev = FindDevice(deviceName);
            ArrayList runs = new ArrayList();
            int errors = 0, warnings = 0;

            if (scope == "hardware" || scope == "all")
            {
                ICompilable c = dev.GetService<ICompilable>();
                if (c == null)
                {
                    // some device types offer compile on the head item instead of the station
                    foreach (DeviceItem i in dev.DeviceItems) { c = i.GetService<ICompilable>(); if (c != null) break; }
                }
                if (c == null) runs.Add(U.Obj("scope", "hardware", "state", "not_run", "reason", "This device offers no hardware compile service."));
                else runs.Add(RunCompile("hardware", c, ref errors, ref warnings));
            }
            if (scope == "software" || scope == "all")
            {
                PlcSoftware plc = FindPlcSoftware(dev.DeviceItems);
                ICompilable c = plc == null ? null : plc.GetService<ICompilable>();
                string what = "software";
                if (c == null)
                {
                    // HMI panels (and other non-PLC software): the software object of any device item that offers a compiler.
                    foreach (DeviceItem i in AllItemsOf(dev.DeviceItems))
                    {
                        SoftwareContainer sc = i.GetService<SoftwareContainer>();
                        IEngineeringServiceProvider sp = sc == null ? null : sc.Software as IEngineeringServiceProvider;
                        if (sp == null) continue;
                        c = sp.GetService<ICompilable>();
                        if (c != null) { what = "software (" + sc.Software.GetType().Name + ")"; break; }
                    }
                }
#if !TIA_V18 && !NO_UNIFIED
                if (c == null)
                {
                    Dictionary<string, object> u = UnifiedValidate(dev, ref errors, ref warnings);
                    if (u != null) { runs.Add(u); return U.Obj("station", dev.Name, "errors", errors, "warnings", warnings, "runs", runs); }
                }
#endif
                if (c == null) runs.Add(U.Obj("scope", "software", "state", "not_run", "reason", "This device has no compilable software."));
                else runs.Add(RunCompile(what, c, ref errors, ref warnings));
            }
            if (runs.Count == 0) throw new ArgumentException("Unknown scope: " + scope);
            return U.Obj("station", dev.Name, "errors", errors, "warnings", warnings, "runs", runs);
        }

        static IEnumerable<DeviceItem> AllItemsOf(DeviceItemComposition items)
        {
            foreach (DeviceItem i in items)
            {
                yield return i;
                foreach (DeviceItem c in AllItemsOf(i.DeviceItems)) yield return c;
            }
        }

        static Dictionary<string, object> RunCompile(string scope, ICompilable c, ref int errors, ref int warnings)
        {
            CompilerResult res = c.Compile();
            errors += res.ErrorCount;
            warnings += res.WarningCount;
            ArrayList msgs = new ArrayList();
            bool truncated = false;
            CollectMessages(res.Messages, msgs, ref truncated);
            Dictionary<string, object> r = U.Obj("scope", scope, "state", res.State.ToString(),
                "errors", res.ErrorCount, "warnings", res.WarningCount, "messages", msgs);
            if (truncated) r["messages_truncated"] = true;
            return r;
        }

        static void CollectMessages(CompilerResultMessageComposition messages, ArrayList list, ref bool truncated)
        {
            foreach (CompilerResultMessage m in messages)
            {
                // success/info lines without text only add noise; keep everything that is a warning or error
                bool important = m.State == CompilerResultState.Error || m.State == CompilerResultState.Warning;
                if (important && m.Description != null && m.Description.Trim().Length > 0)
                {
                    if (list.Count >= 200) { truncated = true; return; }
                    list.Add(U.Obj("state", m.State.ToString(), "path", m.Path, "text", m.Description));
                }
                CollectMessages(m.Messages, list, ref truncated);
                if (truncated) return;
            }
        }
    }
}
