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
    // All Openness access lives in this class (split over the Tia.*.cs files). Everything runs on the single STA main thread.
    // This file: connection to TIA Portal, project life cycle, and helpers to find devices and PLC software.
    public static partial class Tia
    {
        static TiaPortal _tia;
        static bool _owned;
        static Project _project;

        // ---------------- session ----------------

        public static object Status()
        {
            Dictionary<string, object> r = U.Obj(
                "server_version", Program.ServerVersion,
                "tia_adapter", Program.TiaName,
                "api_dir", Program.ApiDir,
                "api_found", Program.ApiDir != null,
                "openness_group_member", InOpennessGroup());
            if (Program.ApiDir == null)
            {
                r["problem"] = "Openness API for " + Program.TiaName + " was not found. Set the TIA_API_DIR environment variable to the PublicAPI\\" + Program.TiaName + " folder (V21+: its net48 subfolder).";
                return r;
            }
            StatusTia(r);
            return r;
        }

        static void StatusTia(Dictionary<string, object> r)
        {
            ArrayList procs = new ArrayList();
            foreach (TiaPortalProcess p in TiaPortal.GetProcesses())
            {
                procs.Add(U.Obj("process_id", p.Id, "mode", p.Mode.ToString(),
                    "project", p.ProjectPath == null ? null : p.ProjectPath.FullName));
            }
            r["running_instances"] = procs;
            r["connected"] = _tia != null;
            if (_tia != null)
            {
                r["started_by_assistant"] = _owned;
                SyncProject();
                r["project"] = _project == null ? null : ProjectInfo();
            }
        }

        static object InOpennessGroup()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                if (id.Groups == null) return "unknown";
                foreach (IdentityReference g in id.Groups)
                {
                    string n;
                    try { n = g.Translate(typeof(NTAccount)).Value; } catch { continue; }
                    if (n.EndsWith("\\Siemens TIA Openness", StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }
            catch { return "unknown"; }
        }

        public static object Connect(string mode, int? processId, bool withUi)
        {
            if (Program.ApiDir == null) throw new InvalidOperationException(Program.TiaName + " Openness API not found (see tia_status).");
            if (_tia != null && !TiaAlive()) DropTia();
            if (_tia != null && processId.HasValue)
            {
                int cur = -1;
                try { cur = _tia.GetCurrentProcess().Id; } catch { }
                if (cur != processId.Value) DropTia();
            }
            SyncProject();
            if (_tia != null) return U.Obj("connected", true, "note", "Already connected.", "project", _project == null ? null : ProjectInfo());
            return ConnectCore(mode ?? "attach", processId, withUi);
        }

        static object ConnectCore(string mode, int? processId, bool withUi)
        {
            if (mode == "new")
            {
                _tia = new TiaPortal(withUi ? TiaPortalMode.WithUserInterface : TiaPortalMode.WithoutUserInterface);
                _owned = true;
            }
            else
            {
                IList<TiaPortalProcess> procs = TiaPortal.GetProcesses();
                TiaPortalProcess target = null;
                if (processId.HasValue)
                {
                    foreach (TiaPortalProcess p in procs) if (p.Id == processId.Value) target = p;
                    if (target == null) throw new InvalidOperationException("No running TIA Portal " + Program.TiaName + " instance with process id " + processId.Value + ".");
                }
                else if (procs.Count == 1) target = procs[0];
                else if (procs.Count == 0)
                    throw new InvalidOperationException("No TIA Portal " + Program.TiaName + " instance is running. Open TIA Portal " + Program.TiaName + " first, or call tia_connect with mode 'new'. (This StartWise build is for " + Program.TiaName + " only.)");
                else
                    throw new InvalidOperationException("Several TIA Portal instances are running. Call tia_status and pass process_id.");
                _tia = target.Attach();
                _owned = false;
            }
            _project = null;
            foreach (Project p in _tia.Projects) { _project = p; break; }
            return U.Obj("connected", true, "started_by_assistant", _owned, "project", _project == null ? null : ProjectInfo());
        }

        // True while the attached TIA Portal process is still alive.
        static bool TiaAlive()
        {
            if (_tia == null) return false;
            try { return _tia.GetCurrentProcess() != null; }
            catch { return false; }
        }

        static void DropTia()
        {
            if (_tia != null && _owned) { try { _tia.Dispose(); } catch { } }
            _tia = null; _project = null; _owned = false;
        }

        static TiaPortal Portal()
        {
            if (_tia != null && !TiaAlive())
            {
                DropTia();
                throw new InvalidOperationException("TIA Portal was closed or crashed. Call tia_connect to attach to the running instance.");
            }
            if (_tia == null) throw new InvalidOperationException("Not connected. Call tia_connect first.");
            return _tia;
        }

        // The engineer may close or switch the project in the TIA window; follow whatever is open now.
        static void SyncProject()
        {
            if (_tia == null) return;
            if (_project != null)
            {
                try { string n = _project.Name; if (n != null) return; }
                catch { }
                _project = null;
            }
            try { foreach (Project p in _tia.Projects) { _project = p; break; } }
            catch { _project = null; }
        }

        public static string CurrentProjectPath()
        {
            SyncProject();
            try { return _project == null ? null : _project.Path.FullName; } catch { return null; }
        }

        static Project Proj()
        {
            Portal();
            SyncProject();
            if (_project == null) throw new InvalidOperationException("No project is open. Call project_create or project_open first.");
            return _project;
        }

        static Dictionary<string, object> ProjectInfo()
        {
            return U.Obj("name", _project.Name, "path", _project.Path == null ? null : _project.Path.FullName,
                "modified", _project.IsModified);
        }

        // ---------------- project ----------------

        public static object ProjectCreate(string directory, string name)
        {
            TiaPortal tia = Portal();
            SyncProject();
            if (_project != null) throw new InvalidOperationException("A project is already open (" + _project.Name + "). Save/close it first.");
            DirectoryInfo dir = new DirectoryInfo(directory);
            if (!dir.Exists) throw new DirectoryNotFoundException("Folder does not exist: " + directory);
            if (Directory.Exists(Path.Combine(dir.FullName, name)))
                throw new InvalidOperationException("A folder named '" + name + "' already exists in " + dir.FullName + ". Choose another project name.");
            _project = tia.Projects.Create(dir, name);
            return U.Obj("created", true, "project", ProjectInfo());
        }

        public static object ProjectOpen(string path)
        {
            TiaPortal tia = Portal();
            SyncProject();
            if (_project != null) throw new InvalidOperationException("A project is already open (" + _project.Name + "). Save/close it first.");
            FileInfo f = new FileInfo(path);
            if (!f.Exists) throw new FileNotFoundException("Project file not found: " + path);
            _project = tia.Projects.Open(f);
            return U.Obj("opened", true, "project", ProjectInfo());
        }

        public static object ProjectSave()
        {
            Proj().Save();
            return U.Obj("saved", true, "project", ProjectInfo());
        }

        // TIA project archive (.zap18 / .zap21 ...). Default target: a "Yedekler" folder next to the project folder.
        public static object ProjectArchive(string targetDir, string name)
        {
            Project p = Proj();
            if (p.IsModified) p.Save();
            FileInfo pf = p.Path;
            DirectoryInfo dir = string.IsNullOrEmpty(targetDir) ? new DirectoryInfo(Path.Combine(pf.Directory.Parent.FullName, "Yedekler")) : new DirectoryInfo(targetDir);
            if (!dir.Exists) dir.Create();
            if (string.IsNullOrEmpty(name)) name = p.Name + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm");
            string ext = ".zap" + Program.TiaMajor;
            if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) name += ext;
            string full = Path.Combine(dir.FullName, name);
            if (File.Exists(full)) throw new InvalidOperationException("Archive already exists: " + full + ". Give another name. Nothing was changed.");
            p.Archive(dir, name, ProjectArchivationMode.Compressed);
            long size = File.Exists(full) ? new FileInfo(full).Length : -1;
            return U.Obj("archived", File.Exists(full), "file", full, "bytes", size, "project", ProjectInfo());
        }

        public static object ProjectClose(bool save)
        {
            Project p = Proj();
            string name = p.Name;
            if (save) p.Save();
            bool modified = p.IsModified;
            p.Close();
            _project = null;
            return U.Obj("closed", name, "saved", save, "unsaved_changes_discarded", modified);
        }

        // ---------------- lookup helpers ----------------

        static List<Device> AllDevices()
        {
            Project p = Proj();
            List<Device> list = new List<Device>();
            foreach (Device d in p.Devices) list.Add(d);
            foreach (Device d in p.UngroupedDevicesGroup.Devices) if (!list.Contains(d)) list.Add(d);
            foreach (DeviceUserGroup g in p.DeviceGroups) CollectGroup(g, list);
            return list;
        }

        static void CollectGroup(DeviceUserGroup g, List<Device> list)
        {
            foreach (Device d in g.Devices) if (!list.Contains(d)) list.Add(d);
            foreach (DeviceUserGroup sub in g.Groups) CollectGroup(sub, list);
        }

        static Device FindDevice(string name)
        {
            List<Device> all = AllDevices();
            foreach (Device d in all)
                if (string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) return d;
            foreach (Device d in all)
                if (FindItemByName(d.DeviceItems, name, 3) != null) return d;
            // panels and some drives rename their head item, so also accept the default station naming
            foreach (Device d in all)
                if (string.Equals(d.Name, name + "_Station", StringComparison.OrdinalIgnoreCase)) return d;
            StringBuilder sb = new StringBuilder();
            foreach (Device d in all) sb.Append(sb.Length > 0 ? ", " : "").Append(d.Name);
            throw new InvalidOperationException("Device '" + name + "' not found. Stations in project: " + (sb.Length > 0 ? sb.ToString() : "(none)"));
        }

        static DeviceItem FindItemByName(DeviceItemComposition items, string name, int depth)
        {
            foreach (DeviceItem i in items)
            {
                if (string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)) return i;
                if (depth > 0)
                {
                    DeviceItem r = FindItemByName(i.DeviceItems, name, depth - 1);
                    if (r != null) return r;
                }
            }
            return null;
        }

        static PlcSoftware FindPlcSoftware(DeviceItemComposition items)
        {
            foreach (DeviceItem i in items)
            {
                SoftwareContainer sc = i.GetService<SoftwareContainer>();
                if (sc != null && sc.Software is PlcSoftware) return (PlcSoftware)sc.Software;
                PlcSoftware r = FindPlcSoftware(i.DeviceItems);
                if (r != null) return r;
            }
            return null;
        }

        static PlcSoftware PlcOf(string deviceName)
        {
            Device d = FindDevice(deviceName);
            PlcSoftware plc = FindPlcSoftware(d.DeviceItems);
            if (plc == null) throw new InvalidOperationException("Device '" + d.Name + "' has no PLC software (it is not a PLC).");
            return plc;
        }
    }
}
