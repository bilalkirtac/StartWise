using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Siemens.Engineering;
using Siemens.Engineering.Library;
using Siemens.Engineering.Library.MasterCopies;
using Siemens.Engineering.Library.Types;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;

namespace TiaAssistant
{
    // Global and project libraries: find master copies / types and copy blocks into a PLC.
    public static partial class Tia
    {
        static void WalkMasterCopies(MasterCopyFolder f, string path, string filter, string lib, ArrayList list, int limit)
        {
            foreach (MasterCopy m in f.MasterCopies)
            {
                if (list.Count >= limit) return;
                if (string.IsNullOrEmpty(filter) || m.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    list.Add(U.Obj("library", lib, "kind", "master_copy", "name", m.Name, "path", path + "/" + m.Name));
            }
            foreach (MasterCopyFolder sub in f.Folders) WalkMasterCopies(sub, path + "/" + sub.Name, filter, lib, list, limit);
        }

        static void WalkTypes(LibraryTypeFolder f, string path, string filter, string lib, ArrayList list, int limit)
        {
            foreach (LibraryType t in f.Types)
            {
                if (list.Count >= limit) return;
                if (!string.IsNullOrEmpty(filter) && t.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                ArrayList vers = new ArrayList();
                foreach (LibraryTypeVersion v in t.Versions) vers.Add(v.VersionNumber.ToString() + (v.IsDefault ? " (default)" : ""));
                list.Add(U.Obj("library", lib, "kind", "type", "name", t.Name, "path", path + "/" + t.Name, "versions", vers));
            }
            foreach (LibraryTypeFolder sub in f.Folders) WalkTypes(sub, path + "/" + sub.Name, filter, lib, list, limit);
        }

        static GlobalLibrary OpenGlobal(string name, ArrayList infoOut)
        {
            TiaPortal tia = Portal();
            foreach (GlobalLibrary g in tia.GlobalLibraries)
                if (string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)) return g;
            foreach (GlobalLibraryInfo i in tia.GlobalLibraries.GetGlobalLibraryInfos())
                if (string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (infoOut != null) infoOut.Add("opened global library '" + i.Name + "' (" + i.LibraryType + ")");
                    return tia.GlobalLibraries.Open(i);
                }
            return null;
        }

        // Open a global library file (.al21 or a folder that holds one) so lib_list / lib_copy_block / drive_prepare can use it.
        // Read-only: StartWise never changes the library itself.
        // Global library files (*.al21) under Documents\Automation, TIA's default folder for projects and libraries.
        // Searched a few levels deep; folders that cannot be read are skipped.
        public static List<string> FindLibraryFiles(string nameFilter)
        {
            List<string> found = new List<string>();
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Automation");
            if (!Directory.Exists(root)) return found;
            Queue<KeyValuePair<string, int>> q = new Queue<KeyValuePair<string, int>>();
            q.Enqueue(new KeyValuePair<string, int>(root, 0));
            while (q.Count > 0 && found.Count < 50)
            {
                KeyValuePair<string, int> cur = q.Dequeue();
                try
                {
                    foreach (string f in Directory.GetFiles(cur.Key, "*.al21"))
                        if (string.IsNullOrEmpty(nameFilter) || Path.GetFileNameWithoutExtension(f).IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0) found.Add(f);
                    if (cur.Value < 3)
                        foreach (string d in Directory.GetDirectories(cur.Key)) q.Enqueue(new KeyValuePair<string, int>(d, cur.Value + 1));
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
            return found;
        }

        public static object LibOpen(string path, string name)
        {
            TiaPortal tia = Portal();
            if (string.IsNullOrEmpty(path))
            {
                List<string> hits = FindLibraryFiles(name);
                if (hits.Count == 0)
                    throw new InvalidOperationException("No global library (*.al21)" + (string.IsNullOrEmpty(name) ? "" : " matching '" + name + "'") +
                        " found under Documents\\Automation. Give its path.");
                if (hits.Count > 1 && string.IsNullOrEmpty(name))
                    return U.Obj("found", hits, "note", "Several libraries found. Call lib_open again with 'name' or 'path' to open one.");
                path = hits[0];
            }
            string file = path;
            if (Directory.Exists(path))
            {
                string[] hits = Directory.GetFiles(path, "*.al21", SearchOption.AllDirectories);
                if (hits.Length == 0)
                {
                    string[] older = Directory.GetFiles(path, "*.al*", SearchOption.AllDirectories);
                    throw new InvalidOperationException("No V21 library (*.al21) under '" + path + "'." +
                        (older.Length > 0 ? " Found " + Path.GetFileName(older[0]) + ": an older library must be upgraded in TIA V21 first (open it there once and confirm the upgrade)." : ""));
                }
                file = hits[0];
            }
            if (!File.Exists(file)) throw new InvalidOperationException("Library file not found: " + file);
            foreach (GlobalLibrary g in tia.GlobalLibraries)
                if (g.Path != null && string.Equals(g.Path.FullName, file, StringComparison.OrdinalIgnoreCase))
                    return U.Obj("library", g.Name, "path", file, "already_open", true);
            GlobalLibrary lib = tia.GlobalLibraries.Open(new FileInfo(file), OpenMode.ReadOnly);
            return U.Obj("library", lib.Name, "path", file, "opened", "read-only", "next", "lib_list library='" + lib.Name + "' to see its types and master copies");
        }

        public static object LibList(string library, string filter, int limit)
        {
            TiaPortal tia = Portal();
            ArrayList libs = new ArrayList();
            foreach (GlobalLibraryInfo i in tia.GlobalLibraries.GetGlobalLibraryInfos())
                libs.Add(U.Obj("name", i.Name, "type", i.LibraryType.ToString(), "open", i.IsOpen, "path", i.Path == null ? null : i.Path.FullName));
            if (string.IsNullOrEmpty(library)) return U.Obj("libraries", libs, "note", "Pass 'library' (a name from this list, or 'project') to list its master copies and types.");

            ArrayList notes = new ArrayList();
            ArrayList items = new ArrayList();
            if (string.Equals(library, "project", StringComparison.OrdinalIgnoreCase))
            {
                ProjectLibrary pl = Proj().ProjectLibrary;
                WalkTypes(pl.TypeFolder, "", filter, "project", items, limit);
                WalkMasterCopies(pl.MasterCopyFolder, "", filter, "project", items, limit);
            }
            else
            {
                GlobalLibrary g = OpenGlobal(library, notes);
                if (g == null) throw new InvalidOperationException("Global library '" + library + "' not found. Call lib_list without arguments to see the available libraries.");
                WalkTypes(g.TypeFolder, "", filter, g.Name, items, limit);
                WalkMasterCopies(g.MasterCopyFolder, "", filter, g.Name, items, limit);
            }
            return U.Obj("library", library, "filter", filter, "items", items, "truncated", items.Count >= limit, "notes", notes);
        }

        static MasterCopy FindMasterCopy(MasterCopyFolder f, string name)
        {
            foreach (MasterCopy m in f.MasterCopies) if (string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)) return m;
            foreach (MasterCopyFolder sub in f.Folders) { MasterCopy r = FindMasterCopy(sub, name); if (r != null) return r; }
            return null;
        }

        static LibraryType FindLibType(LibraryTypeFolder f, string name)
        {
            foreach (LibraryType t in f.Types) if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) return t;
            foreach (LibraryTypeFolder sub in f.Folders) { LibraryType r = FindLibType(sub, name); if (r != null) return r; }
            return null;
        }

        // System constants: hardware identifiers (HW_IO etc.) that blocks like SINA_SPEED use to address a drive telegram.
        static void CollectSysConsts(PlcTagTableComposition tables, PlcTagTableUserGroupComposition groups, string filter, ArrayList list, int limit)
        {
            foreach (PlcTagTable t in tables)
            {
                foreach (PlcSystemConstant c in t.SystemConstants)
                {
                    if (list.Count >= limit) return;
                    IEngineeringObject o = c as IEngineeringObject;
                    Dictionary<string, object> d = U.Obj();
                    foreach (string a in new string[] { "Name", "DataTypeName", "Value" })
                    {
                        try { d[a.ToLowerInvariant()] = Plain(o.GetAttribute(a)); } catch { }
                    }
                    string n = d.ContainsKey("name") && d["name"] != null ? d["name"].ToString() : "";
                    if (!string.IsNullOrEmpty(filter) && n.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    list.Add(d);
                }
            }
            foreach (PlcTagTableUserGroup g in groups) CollectSysConsts(g.TagTables, g.Groups, filter, list, limit);
        }

        public static object SystemConstants(string deviceName, string filter, int limit)
        {
            PlcSoftware plc = PlcOf(deviceName);
            ArrayList list = new ArrayList();
            CollectSysConsts(plc.TagTableGroup.TagTables, plc.TagTableGroup.Groups, filter, list, limit);
            return U.Obj("plc", plc.Name, "filter", filter, "constants", list, "truncated", list.Count >= limit);
        }

        public static object LibCopyBlock(string deviceName, string library, string item, string version)
        {
            PlcSoftware plc = PlcOf(deviceName);
            ArrayList notes = new ArrayList();
            MasterCopyFolder mcf; LibraryTypeFolder tf;
            if (string.Equals(library, "project", StringComparison.OrdinalIgnoreCase))
            { mcf = Proj().ProjectLibrary.MasterCopyFolder; tf = Proj().ProjectLibrary.TypeFolder; }
            else
            {
                GlobalLibrary g = OpenGlobal(library, notes);
                if (g == null) throw new InvalidOperationException("Global library '" + library + "' not found. Call lib_list to see the available libraries.");
                mcf = g.MasterCopyFolder; tf = g.TypeFolder;
            }
            if (FindBlock(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, item) != null)
                return U.Obj("copied", false, "note", "A block named '" + item + "' already exists in '" + plc.Name + "'. Nothing was changed.", "notes", notes);

            LibraryType t = FindLibType(tf, item);
            if (t != null)
            {
                LibraryTypeVersion pick = null;
                foreach (LibraryTypeVersion v in t.Versions)
                {
                    if (!string.IsNullOrEmpty(version)) { if (v.VersionNumber.ToString() == version.TrimStart('V', 'v')) pick = v; }
                    else if (pick == null || v.IsDefault || (!pick.IsDefault && v.VersionNumber > pick.VersionNumber)) pick = v;
                }
                if (pick == null) throw new InvalidOperationException("Version " + version + " of type '" + t.Name + "' not found.");
                CodeBlockLibraryTypeVersion cv = pick as CodeBlockLibraryTypeVersion;
                if (cv == null) throw new InvalidOperationException("Type '" + t.Name + "' is not a PLC code block type.");
                PlcBlock b = plc.BlockGroup.Blocks.CreateFrom(cv);
                return U.Obj("copied", true, "from", "type", "name", b.Name, "kind", b.GetType().Name, "number", b.Number, "version", pick.VersionNumber.ToString(), "notes", notes);
            }
            MasterCopy m = FindMasterCopy(mcf, item);
            if (m != null)
            {
                PlcBlock b = plc.BlockGroup.Blocks.CreateFrom(m);
                return U.Obj("copied", true, "from", "master_copy", "name", b.Name, "kind", b.GetType().Name, "number", b.Number, "notes", notes);
            }
            throw new InvalidOperationException("No type or master copy named '" + item + "' in library '" + library + "'. Use lib_list with a filter to find the exact name.");
        }
    }
}
