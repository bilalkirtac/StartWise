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
    // SINAMICS drives through Startdrive Openness: telegrams, parameters, motor nameplate data.
    public static partial class Tia
    {
        // ---------------- drives (Startdrive) ----------------

        static void CollectDriveObjects(DeviceItemComposition items, List<Siemens.Engineering.MC.Drives.DriveObject> list)
        {
            foreach (DeviceItem i in items)
            {
                Siemens.Engineering.MC.Drives.DriveObjectContainer c = i.GetService<Siemens.Engineering.MC.Drives.DriveObjectContainer>();
                if (c != null) foreach (Siemens.Engineering.MC.Drives.DriveObject o in c.DriveObjects) list.Add(o);
                CollectDriveObjects(i.DeviceItems, list);
            }
        }

        static ArrayList TelegramList(Siemens.Engineering.MC.Drives.DriveObject o)
        {
            ArrayList list = new ArrayList();
            foreach (Siemens.Engineering.MC.Drives.Telegram t in o.Telegrams)
            {
                ArrayList addr = new ArrayList();
                try
                {
                    foreach (Address a in t.Addresses)
                        addr.Add(U.Obj("io_type", a.IoType.ToString(), "start_byte", a.StartAddress, "length", a.Length));
                }
                catch { }
                list.Add(U.Obj("type", t.Type.ToString(), "number", t.TelegramNumber, "addresses", addr));
            }
            return list;
        }

        // ---- drives that exist only as GSD (GSDML) devices, e.g. SINAMICS G120X without Startdrive support ----

        static DeviceItem FindGsdDriveModule(DeviceItemComposition items, int depth)
        {
            foreach (DeviceItem i in items)
            {
                string t = i.TypeIdentifier ?? "";
                if (t.StartsWith("GSD:") && t.Contains("/M/")) return i;
                if (depth > 0)
                {
                    DeviceItem r = FindGsdDriveModule(i.DeviceItems, depth - 1);
                    if (r != null) return r;
                }
            }
            return null;
        }

        static Dictionary<string, object> GsdTelegramOne(string stationName, int? telegram)
        {
            Device dev = FindDevice(stationName);
            DeviceItem drive = FindGsdDriveModule(dev.DeviceItems, 4);
            if (drive == null)
                throw new InvalidOperationException("Station '" + dev.Name + "' has no GSD drive module. For Startdrive drives use drive_telegram.");
            string type = drive.TypeIdentifier;
            string prefix = type.Substring(0, type.IndexOf("/M/"));
            DeviceItem current = null;
            foreach (DeviceItem sub in drive.DeviceItems)
                if ((sub.TypeIdentifier ?? "").Contains("/SM/IDS_TEL")) current = sub;
            Dictionary<string, object> r = U.Obj("station", dev.Name, "drive_module", drive.Name);
            if (telegram.HasValue)
            {
                string want = prefix + "/SM/IDS_TEL" + telegram.Value;
                if (current != null && string.Equals(current.TypeIdentifier, want, StringComparison.OrdinalIgnoreCase)) r["changed"] = false;
                else
                {
                    int? oldPos = null;
                    if (current != null) { r["previous"] = current.Name; oldPos = current.PositionNumber; current.Delete(); current = null; }
                    string name = "Standard telegram " + telegram.Value;
                    string lastError = null;
                    for (int pos = oldPos ?? 0; pos <= 8 && current == null; pos++)
                    {
                        if (!drive.CanPlugNew(want, name, pos)) continue;
                        try { current = drive.PlugNew(want, name, pos); }
                        catch (Exception ex) { lastError = U.Describe(ex); }
                    }
                    if (current == null)
                        throw new InvalidOperationException("Telegram " + telegram.Value + " could not be plugged into '" + drive.Name + "' of '" + dev.Name +
                            "' (type " + want + ")." + (lastError == null ? "" : " Last error: " + lastError));
                    r["changed"] = true;
                }
            }
            if (current != null)
            {
                r["telegram"] = current.Name;
                r["slot"] = current.PositionNumber;
                r["addresses"] = Addresses(current, 1);
            }
            else r["telegram"] = null;
            return r;
        }

        public static object GsdDriveTelegram(object stationsArg, int? telegram)
        {
            Proj();
            IEnumerable stations = (stationsArg is string) ? null : stationsArg as IEnumerable;
            if (stations == null) throw new ArgumentException("'stations' must be an array of station names.");
            ArrayList done = new ArrayList();
            ArrayList failed = new ArrayList();
            foreach (object o in stations)
            {
                string name = o == null ? null : o.ToString();
                try { done.Add(GsdTelegramOne(name, telegram)); }
                catch (Exception ex) { failed.Add(U.Obj("station", name, "error", U.Describe(ex))); }
            }
            return U.Obj("results", done, "failed", failed);
        }

        public static object DriveTelegram(string stationName, int? telegram, int driveObjectIndex, int? inputStart, int? outputStart)
        {
            Device dev = FindDevice(stationName);
            List<Siemens.Engineering.MC.Drives.DriveObject> objs = new List<Siemens.Engineering.MC.Drives.DriveObject>();
            CollectDriveObjects(dev.DeviceItems, objs);
            if (objs.Count == 0)
                throw new InvalidOperationException("Station '" + dev.Name + "' exposes no Startdrive drive object. It is not a SINAMICS drive, or the drive is incomplete (e.g. power module missing).");
            if (driveObjectIndex < 0 || driveObjectIndex >= objs.Count)
                throw new InvalidOperationException("Drive object index " + driveObjectIndex + " does not exist; the station has " + objs.Count + ".");
            Siemens.Engineering.MC.Drives.DriveObject o = objs[driveObjectIndex];
            Dictionary<string, object> r = U.Obj("station", dev.Name, "drive_objects", objs.Count);
            if (telegram.HasValue)
            {
                Siemens.Engineering.MC.Drives.Telegram main = null;
                foreach (Siemens.Engineering.MC.Drives.Telegram t in o.Telegrams)
                    if (t.Type.ToString() == "MainTelegram") main = t;
                if (main == null) throw new InvalidOperationException("The drive object has no main telegram to change.");
                if (main.TelegramNumber != telegram.Value)
                {
                    if (!main.CanChangeTelegram(telegram.Value))
                        throw new InvalidOperationException("Telegram " + telegram.Value + " is not possible for this drive (current: " + main.TelegramNumber + "). Nothing was changed.");
                    r["previous_telegram"] = main.TelegramNumber;
                    main.TelegramNumber = telegram.Value;
                    r["changed"] = true;
                }
                else r["changed"] = false;
            }
            if (inputStart.HasValue || outputStart.HasValue)
            {
                Siemens.Engineering.MC.Drives.Telegram main = null;
                foreach (Siemens.Engineering.MC.Drives.Telegram t in o.Telegrams)
                    if (t.Type.ToString() == "MainTelegram") main = t;
                if (main == null) throw new InvalidOperationException("The drive object has no main telegram.");
                ArrayList moved = new ArrayList();
                foreach (Address a in main.Addresses)
                {
                    int? want = a.IoType == AddressIoType.Input ? inputStart : a.IoType == AddressIoType.Output ? outputStart : null;
                    if (!want.HasValue || a.StartAddress == want.Value) continue;
                    int old = a.StartAddress;
                    a.StartAddress = want.Value;
                    moved.Add(U.Obj("io_type", a.IoType.ToString(), "previous_start_byte", old, "start_byte", a.StartAddress));
                }
                r["addresses_moved"] = moved;
            }
            r["telegrams"] = TelegramList(o);
            return r;
        }

        static Siemens.Engineering.MC.Drives.DriveObject DriveObjectOf(string stationName, int index, out Device dev)
        {
            dev = FindDevice(stationName);
            List<Siemens.Engineering.MC.Drives.DriveObject> objs = new List<Siemens.Engineering.MC.Drives.DriveObject>();
            CollectDriveObjects(dev.DeviceItems, objs);
            if (objs.Count == 0)
                throw new InvalidOperationException("Station '" + dev.Name + "' exposes no Startdrive drive object. It is not a SINAMICS drive, or the drive is incomplete (e.g. power module missing).");
            if (index < 0 || index >= objs.Count)
                throw new InvalidOperationException("Drive object index " + index + " does not exist; the station has " + objs.Count + ".");
            return objs[index];
        }

        // Accepts "p0304", "P304", "304", "p0304[0]", "r0021".
        static bool ParseParam(string token, out int number, out int? index)
        {
            number = 0; index = null;
            Match m = Regex.Match(token == null ? "" : token.Trim(), @"^[pPrR]?0*(\d+)(?:\[(\d+)\])?$");
            if (!m.Success) return false;
            number = int.Parse(m.Groups[1].Value);
            if (m.Groups[2].Success) index = int.Parse(m.Groups[2].Value);
            return true;
        }

        static Siemens.Engineering.MC.Drives.DriveParameter FindParam(Siemens.Engineering.MC.Drives.DriveObject o, int number, int? index)
        {
            Siemens.Engineering.MC.Drives.DriveParameter p = null;
            int[] tryIdx = index.HasValue ? new int[] { index.Value } : new int[] { 0, -1 };
            foreach (int i in tryIdx)
            {
                try { p = o.Parameters.Find(number, i); } catch { p = null; }
                if (p != null) return p;
            }
            string[] names = index.HasValue
                ? new string[] { "p" + number + "[" + index.Value + "]", "r" + number + "[" + index.Value + "]" }
                : new string[] { "p" + number, "p" + number + "[0]", "r" + number, "r" + number + "[0]" };
            foreach (string n in names)
            {
                try { p = o.Parameters.Find(n); } catch { p = null; }
                if (p != null) return p;
            }
            return null;
        }

        static object Plain(object v)
        {
            if (v == null) return null;
            if (v is string || v is bool || v is int || v is long || v is double || v is float || v is decimal
                || v is short || v is byte || v is uint || v is ushort || v is ulong || v is sbyte) return v;
            return v.ToString();
        }

        static Dictionary<string, object> ParamInfo(Siemens.Engineering.MC.Drives.DriveParameter p)
        {
            Dictionary<string, object> d = U.Obj("name", p.Name, "number", p.Number, "index", p.ArrayIndex);
            try { d["value"] = Plain(p.Value); } catch (Exception ex) { d["value_error"] = ex.Message; }
            try { if (!string.IsNullOrEmpty(p.Unit)) d["unit"] = p.Unit; } catch { }
            try { d["text"] = p.ParameterText; } catch { }
            try { d["min"] = Plain(p.MinValue); d["max"] = Plain(p.MaxValue); } catch { }
            return d;
        }

        public static object DriveParameters(string stationName, object readArg, object setArg, int driveObjectIndex)
        {
            Device dev;
            Siemens.Engineering.MC.Drives.DriveObject o = DriveObjectOf(stationName, driveObjectIndex, out dev);
            Dictionary<string, object> r = U.Obj("station", dev.Name);
            ArrayList written = new ArrayList();
            ArrayList failed = new ArrayList();
            ArrayList read = new ArrayList();

            IEnumerable sets = (setArg is string) ? null : setArg as IEnumerable;
            if (sets != null)
            {
                foreach (object item in sets)
                {
                    Dictionary<string, object> t = item as Dictionary<string, object>;
                    string token = t == null ? null : U.Str(t, "parameter", false);
                    int number; int? index;
                    if (token == null || !t.ContainsKey("value") || !ParseParam(token, out number, out index))
                    { failed.Add(U.Obj("parameter", token, "error", "each entry needs 'parameter' (e.g. p0304) and 'value'")); continue; }
                    Siemens.Engineering.MC.Drives.DriveParameter p = FindParam(o, number, index);
                    if (p == null) { failed.Add(U.Obj("parameter", token, "error", "parameter not found on this drive object")); continue; }
                    try
                    {
                        object old = p.Value;
                        object v = t["value"];
                        if (old != null && v != null && !(old is string))
                            v = Convert.ChangeType(v, old.GetType(), System.Globalization.CultureInfo.InvariantCulture);
                        p.Value = v;
                        Dictionary<string, object> info = ParamInfo(p);
                        info["previous"] = Plain(old);
                        written.Add(info);
                    }
                    catch (Exception ex) { failed.Add(U.Obj("parameter", token, "name", p.Name, "error", U.Describe(ex))); }
                }
            }

            IEnumerable reads = (readArg is string) ? null : readArg as IEnumerable;
            if (reads != null)
            {
                foreach (object item in reads)
                {
                    string token = item == null ? null : item.ToString();
                    int number; int? index;
                    if (!ParseParam(token, out number, out index)) { failed.Add(U.Obj("parameter", token, "error", "cannot parse parameter name")); continue; }
                    Siemens.Engineering.MC.Drives.DriveParameter p = FindParam(o, number, index);
                    if (p == null) { failed.Add(U.Obj("parameter", token, "error", "parameter not found on this drive object")); continue; }
                    read.Add(ParamInfo(p));
                }
            }
            if (sets != null) r["written"] = written;
            if (reads != null) r["read"] = read;
            r["failed"] = failed;
            return r;
        }

        // ---- motor configuration through the Startdrive "drive function interface" (same path as the commissioning wizard) ----

        static Dictionary<string, object> EntryInfo(Siemens.Engineering.MC.Drives.DFI.ConfigurationEntry e)
        {
            Dictionary<string, object> d = U.Obj("name", e.Name, "number", e.Number);
            try { d["value"] = Plain(e.Value); } catch (Exception ex) { d["value_error"] = ex.Message; }
            try { if (!string.IsNullOrEmpty(e.Unit)) d["unit"] = e.Unit; } catch { }
            try { d["text"] = e.Description; } catch { }
            try { d["min"] = Plain(e.MinValue); d["max"] = Plain(e.MaxValue); } catch { }
            return d;
        }

        static ArrayList EntryList(Siemens.Engineering.MC.Drives.DFI.ConfigurationEntryComposition entries)
        {
            ArrayList list = new ArrayList();
            if (entries != null) foreach (Siemens.Engineering.MC.Drives.DFI.ConfigurationEntry e in entries) list.Add(EntryInfo(e));
            return list;
        }

        public static object DriveMotor(string stationName, string motorType, object dataArg, int dds, int driveObjectIndex)
        {
            Device dev;
            Siemens.Engineering.MC.Drives.DriveObject o = DriveObjectOf(stationName, driveObjectIndex, out dev);
            Siemens.Engineering.MC.Drives.DFI.DriveFunctionInterface dfi = o.GetService<Siemens.Engineering.MC.Drives.DFI.DriveFunctionInterface>();
            if (dfi == null || dfi.HardwareProjection == null)
                throw new InvalidOperationException("Drive '" + dev.Name + "' does not offer motor configuration through Openness (no drive function interface).");
            Siemens.Engineering.MC.Drives.DFI.HardwareProjection hp = dfi.HardwareProjection;
            ushort ds = (ushort)dds;
            Dictionary<string, object> r = U.Obj("station", dev.Name, "drive_data_set", dds);

            IEnumerable data = (dataArg is string) ? null : dataArg as IEnumerable;
            bool write = data != null;

            if (!string.IsNullOrEmpty(motorType) || write)
            {
                string mt = string.IsNullOrEmpty(motorType) ? "InductionMotor" : motorType;
                Siemens.Engineering.MC.Drives.Enums.MotorType t;
                if (!Enum.TryParse<Siemens.Engineering.MC.Drives.Enums.MotorType>(mt, true, out t))
                    throw new ArgumentException("Unknown motor_type '" + mt + "'. Possible: " +
                        string.Join(", ", Enum.GetNames(typeof(Siemens.Engineering.MC.Drives.Enums.MotorType))));
                r["motor_type"] = t.ToString();
                r["motor_type_set"] = hp.SetMotorType(t, ds);
            }

            Siemens.Engineering.MC.Drives.DFI.MotorConfiguration cfg = hp.GetCurrentMotorConfiguration(ds);
            if (cfg == null) throw new InvalidOperationException("The drive returned no motor configuration for data set " + dds + ".");

            if (write)
            {
                ArrayList written = new ArrayList();
                ArrayList failed = new ArrayList();
                ArrayList skipped = new ArrayList();
                foreach (object item in data)
                {
                    Dictionary<string, object> t = item as Dictionary<string, object>;
                    string token = t == null ? null : U.Str(t, "parameter", false);
                    int number; int? index;
                    if (token == null || !t.ContainsKey("value") || !ParseParam(token, out number, out index))
                    { failed.Add(U.Obj("parameter", token, "error", "each entry needs 'parameter' (e.g. p0304) and 'value'")); continue; }
                    Siemens.Engineering.MC.Drives.DFI.ConfigurationEntry e = null;
                    try { e = cfg.RequiredConfigurationEntries.Find((uint)number); } catch { }
                    if (e == null) { try { e = cfg.OptionalConfigurationEntries.Find((uint)number); } catch { } }
                    if (e == null) { skipped.Add(token); continue; } // this drive does not ask for it
                    try
                    {
                        object old = e.Value;
                        object v = t["value"];
                        if (old != null && v != null && !(old is string))
                            v = Convert.ChangeType(v, old.GetType(), System.Globalization.CultureInfo.InvariantCulture);
                        e.Value = v;
                        written.Add(U.Obj("parameter", e.Name, "previous", Plain(old), "value", Plain(v)));
                    }
                    catch (Exception ex) { failed.Add(U.Obj("parameter", token, "error", U.Describe(ex))); }
                }
                r["entries_set"] = written;
                r["skipped_not_used_by_this_drive"] = skipped;
                r["failed"] = failed;
                if (failed.Count == 0)
                {
                    r["applied"] = hp.ProjectMotorConfiguration(cfg, ds);
                    cfg = hp.GetCurrentMotorConfiguration(ds);
                }
                else r["applied"] = false;
            }

            r["required"] = EntryList(cfg.RequiredConfigurationEntries);
            r["optional"] = EntryList(cfg.OptionalConfigurationEntries);
            return r;
        }

        public static object DriveParameterList(string stationName, int from, int to, int driveObjectIndex)
        {
            Device dev;
            Siemens.Engineering.MC.Drives.DriveObject o = DriveObjectOf(stationName, driveObjectIndex, out dev);
            ArrayList list = new ArrayList();
            int total = 0;
            foreach (Siemens.Engineering.MC.Drives.DriveParameter p in o.Parameters)
            {
                total++;
                if (p.Number < from || p.Number > to) continue;
                if (list.Count >= 300) break;
                list.Add(ParamInfo(p));
            }
            return U.Obj("station", dev.Name, "accessible_parameters_total", total, "range", from + "-" + to, "parameters", list);
        }
    }
}
