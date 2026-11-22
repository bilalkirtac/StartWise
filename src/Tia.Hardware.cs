using System;
using System.Linq;
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
    // Hardware catalog lookup by MLFB, adding devices, plugging modules, PLC protection settings.
    public static partial class Tia
    {
        // ---------------- catalog ----------------

        static string Compact(string s) { return s == null ? "" : s.Replace(" ", "").ToUpperInvariant(); }

        // The catalog writes some article numbers with placeholders ("6SL3210-1PE22-7ULx", "6ES7 590-1***0-0AA0").
        static bool MlfbMatches(string catalogArticle, string mlfb)
        {
            string c = (catalogArticle ?? "").Replace(" ", "");
            string m = (mlfb ?? "").Replace(" ", "");
            if (c.Length != m.Length || c.Length == 0) return false;
            for (int i = 0; i < c.Length; i++)
            {
                char cc = c[i], mc = m[i];
                if (cc == 'x' || cc == '*') continue;            // placeholder (lower-case x only)
                if (char.ToUpperInvariant(cc) != char.ToUpperInvariant(mc)) return false;
            }
            return true;
        }

        static List<Siemens.Engineering.HW.HardwareCatalog.CatalogEntry> CatalogSearch(string query)
        {
            List<Siemens.Engineering.HW.HardwareCatalog.CatalogEntry> res = new List<Siemens.Engineering.HW.HardwareCatalog.CatalogEntry>();
            List<string> variants = new List<string>();
            variants.Add(query);
            string c = query.Replace(" ", "");
            // Siemens catalog spelling has a blank after the 4th character: "6ES7 516-3AN02-0AB0"
            if (Regex.IsMatch(c, @"^[0-9][A-Za-z]{2}[0-9]{1}[0-9A-Za-z]{3}-")) variants.Add(c.Substring(0, 4) + " " + c.Substring(4));
            if (c != query) variants.Add(c);
            // placeholders hide the last characters, so also search by shortened article numbers
            if (Regex.IsMatch(c, @"^[0-9][A-Za-z]{2}[0-9]") && c.Length > 8)
            {
                variants.Add(c.Substring(0, c.Length - 1));
                variants.Add(c.Substring(0, c.Length - 2));
            }
            foreach (string v in variants)
            {
                foreach (Siemens.Engineering.HW.HardwareCatalog.CatalogEntry e in Portal().HardwareCatalog.Find(v)) res.Add(e);
                if (res.Count > 0) break;
            }
            return res;
        }

        static Dictionary<string, object> EntryInfo(Siemens.Engineering.HW.HardwareCatalog.CatalogEntry e)
        {
            return U.Obj("article_number", e.ArticleNumber, "version", e.Version, "type_identifier", e.TypeIdentifier,
                "type_name", e.TypeName, "description", e.Description, "catalog_path", e.CatalogPath);
        }

        public static object CatalogFind(string query, int limit)
        {
            List<Siemens.Engineering.HW.HardwareCatalog.CatalogEntry> found = CatalogSearch(query);
            ArrayList list = new ArrayList();
            foreach (Siemens.Engineering.HW.HardwareCatalog.CatalogEntry e in found)
            {
                if (list.Count >= limit) break;
                list.Add(EntryInfo(e));
            }
            return U.Obj("query", query, "total", found.Count, "returned", list.Count, "entries", list);
        }

        static Version ParseVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return new Version(0, 0);
            string s = Regex.Replace(v, @"[^0-9.]", "").Trim('.');
            if (s.Length == 0) return new Version(0, 0);
            if (!s.Contains(".")) s += ".0";
            Version r;
            return Version.TryParse(s, out r) ? r : new Version(0, 0);
        }

        // Resolves MLFB (+ optional firmware) to the type identifier of the installed catalog.
        static string ResolveType(string mlfb, string version, string typeIdentifier, Dictionary<string, object> report)
        {
            if (!string.IsNullOrEmpty(typeIdentifier)) { report["type_identifier"] = typeIdentifier; return typeIdentifier; }
            if (string.IsNullOrEmpty(mlfb)) throw new ArgumentException("Give either mlfb or type_identifier.");

            List<Siemens.Engineering.HW.HardwareCatalog.CatalogEntry> found = CatalogSearch(mlfb);
            List<Siemens.Engineering.HW.HardwareCatalog.CatalogEntry> exact = new List<Siemens.Engineering.HW.HardwareCatalog.CatalogEntry>();
            foreach (Siemens.Engineering.HW.HardwareCatalog.CatalogEntry e in found)
                if (MlfbMatches(e.ArticleNumber, mlfb)) exact.Add(e);
            if (exact.Count == 0)
            {
                string msg = "MLFB '" + mlfb + "' was not found in the installed " + Program.TiaName + " hardware catalog.";
                if (found.Count > 0) msg += " " + found.Count + " similar entries exist; call catalog_find to see them.";
                else msg += " The device may need an HSP/GSD file that is not installed, or the article number may be wrong.";
                throw new InvalidOperationException(msg);
            }

            Siemens.Engineering.HW.HardwareCatalog.CatalogEntry pick = null;
            List<string> versions = new List<string>();
            foreach (Siemens.Engineering.HW.HardwareCatalog.CatalogEntry e in exact)
                if (!string.IsNullOrEmpty(e.Version) && !versions.Contains(e.Version)) versions.Add(e.Version);

            if (!string.IsNullOrEmpty(version))
            {
                foreach (Siemens.Engineering.HW.HardwareCatalog.CatalogEntry e in exact)
                    if (ParseVersion(e.Version) == ParseVersion(version)) { pick = e; break; }
                if (pick == null)
                    throw new InvalidOperationException("Firmware " + version + " is not in the catalog for " + mlfb +
                        ". Available: " + string.Join(", ", versions.ToArray()));
            }
            else
            {
                foreach (Siemens.Engineering.HW.HardwareCatalog.CatalogEntry e in exact)
                    if (pick == null || ParseVersion(e.Version) > ParseVersion(pick.Version)) pick = e;
                report["version_defaulted"] = true;
            }
            report["article_number"] = pick.ArticleNumber;
            report["version"] = pick.Version;
            report["available_versions"] = versions;
            report["type_identifier"] = pick.TypeIdentifier;
            report["type_name"] = pick.TypeName;
            return pick.TypeIdentifier;
        }

        // ---------------- hardware ----------------

        public static object DeviceAdd(string mlfb, string version, string typeIdentifier, string name, string stationName, string protection)
        {
            Project p = Proj();
            Dictionary<string, object> r = U.Obj();
            string type = ResolveType(mlfb, version, typeIdentifier, r);
            if (string.IsNullOrEmpty(stationName)) stationName = name + "_Station";
            foreach (Device d in AllDevices())
            {
                if (string.Equals(d.Name, stationName, StringComparison.OrdinalIgnoreCase) || FindItemByName(d.DeviceItems, name, 3) != null)
                    throw new InvalidOperationException("A device named '" + name + "' / station '" + stationName + "' already exists. Nothing was changed.");
            }
            Device dev = p.Devices.CreateWithItem(type, name, stationName);
            r["created"] = true;
            r["station"] = dev.Name;
            r["name"] = name;
            r["has_plc_software"] = FindPlcSoftware(dev.DeviceItems) != null;
            if (FindPlcSoftware(dev.DeviceItems) != null && protection != "keep")
            {
                // House default: PLCs are created without passwords; the engineer sets protection later.
                ArrayList changes = RemoveProtection(dev);
                if (changes.Count > 0) r["protection_removed"] = changes;
                string umac = AnonymousFullAccess(dev);
                if (umac != null) r["users_and_roles"] = umac;
            }
            Dictionary<string, object> sec = SecurityStatus(dev);
            if (sec != null) r["security"] = sec;
            return r;
        }

        // CPU firmware V4.x and newer use "Users and roles" (UMAC): the hardware compile fails with
        // "At least one user must have the following runtime right: Full access" until a user has it.
        // House default without passwords = the project's Anonymous user, active, with the system role "PLC administrator".
        // Reflection keeps this compiling against V18 (no UMAC API there).
        static string AnonymousFullAccess(Device dev)
        {
            try
            {
                int major = 0;
                foreach (DeviceItem i in AllItems(dev.DeviceItems))
                {
                    if (i.GetService<SoftwareContainer>() == null || !(i.GetService<SoftwareContainer>().Software is PlcSoftware)) continue;
                    string fw = null; try { fw = Convert.ToString(i.GetAttribute("FirmwareVersion")); } catch { }
                    Match m = Regex.Match(fw ?? "", @"(\d+)"); if (m.Success) major = int.Parse(m.Groups[1].Value);
                    break;
                }
                if (major < 4) return null;
                Type t = Program.SiemensAssemblies().Select(a => a.GetType("Siemens.Engineering.Umac.UmacConfigurator")).FirstOrDefault(x => x != null);
                if (t == null) return "CPU firmware V" + major + " needs a user with Full access, but this TIA version has no UMAC API. Set it in TIA: Security settings > Users and roles.";
                Project p = Proj();
                object cfg = p.GetType().GetMethods().First(x => x.Name == "GetService" && x.IsGenericMethodDefinition).MakeGenericMethod(t).Invoke(p, null);
                object anon = t.GetProperty("AnonymousUser").GetValue(cfg, null);
                bool active = (bool)anon.GetType().GetProperty("IsActive").GetValue(anon, null);
                if (!active) t.GetMethod("ActivateAnonymousUser").Invoke(cfg, null);
                object roles = t.GetProperty("SystemRoles").GetValue(cfg, null);
                object role = roles.GetType().GetMethod("Find", new[] { typeof(string) }).Invoke(roles, new object[] { "PLC administrator" });
                if (role == null) return "Anonymous user activated; system role 'PLC administrator' not found - assign Full access in TIA.";
                object assoc = anon.GetType().GetProperty("Roles").GetValue(anon, null);
                bool has = (bool)assoc.GetType().GetMethod("Contains").Invoke(assoc, new[] { role });
                if (!has) assoc.GetType().GetMethod("Add").Invoke(assoc, new[] { role });
                return "Anonymous user active with role 'PLC administrator' (runtime Full access, testing only - set real users before commissioning)";
            }
            catch (Exception ex) { return "Users and roles NOT set: " + (ex.InnerException ?? ex).Message; }
        }

        static IEnumerable<DeviceItem> AllItems(DeviceItemComposition items)
        {
            foreach (DeviceItem i in items)
            {
                yield return i;
                foreach (DeviceItem c in AllItems(i.DeviceItems)) yield return c;
            }
        }

        // Where can a module go? The rack(s) first, then head modules that carry sub-modules
        // (e.g. a G120 control unit takes its power module itself).
        static List<HardwareObject> PlugContainers(Device dev)
        {
            List<HardwareObject> list = new List<HardwareObject>();
            foreach (DeviceItem i in dev.DeviceItems) list.Add(i);
            if (list.Count == 0) list.Add(dev);
            return list;
        }

        static void CollectNested(DeviceItemComposition items, int depth, List<HardwareObject> list)
        {
            foreach (DeviceItem i in items)
            {
                list.Add(i);
                if (depth > 1) CollectNested(i.DeviceItems, depth - 1, list);
            }
        }

        public static object ModulePlug(string deviceName, string mlfb, string version, string typeIdentifier, string name, int? slot, int? container, string into)
        {
            Device dev = FindDevice(deviceName);
            Dictionary<string, object> r = U.Obj();
            string type = ResolveType(mlfb, version, typeIdentifier, r);
            if (FindItemByName(dev.DeviceItems, name, 3) != null)
                throw new InvalidOperationException("Station '" + dev.Name + "' already has an item named '" + name + "'. Nothing was changed.");

            List<HardwareObject> all = PlugContainers(dev);
            List<HardwareObject> targets = new List<HardwareObject>();
            if (!string.IsNullOrEmpty(into))
            {
                DeviceItem host = FindItemByName(dev.DeviceItems, into, 5);
                if (host == null) throw new InvalidOperationException("Station '" + dev.Name + "' has no item named '" + into + "'.");
                targets.Add(host);
            }
            else if (container.HasValue)
            {
                if (container.Value < 0 || container.Value >= all.Count)
                    throw new InvalidOperationException("Container index " + container.Value + " does not exist; station '" + dev.Name + "' has " + all.Count + " top-level item(s).");
                targets.Add(all[container.Value]);
            }
            else
            {
                // top-level items first, then nested ones (GSD devices take submodules inside a module, e.g. a telegram inside DRIVE_1)
                targets.AddRange(all);
                foreach (DeviceItem top in dev.DeviceItems) CollectNested(top.DeviceItems, 3, targets);
            }

            DeviceItem item = null;
            HardwareObject used = null;
            string lastError = null;
            foreach (HardwareObject t in targets)
            {
                int from = slot.HasValue ? slot.Value : 0;
                int to = slot.HasValue ? slot.Value : 80;
                for (int pos = from; pos <= to && item == null; pos++)
                {
                    if (!t.CanPlugNew(type, name, pos)) continue;
                    // CanPlugNew is optimistic on some devices, so a failed attempt just moves on to the next position
                    try { item = t.PlugNew(type, name, pos); used = t; }
                    catch (Exception ex) { lastError = U.Describe(ex); }
                }
                if (item != null) break;
            }
            if (item == null)
            {
                string where = slot.HasValue ? "slot " + slot.Value : "any free slot";
                throw new InvalidOperationException("Module " + (mlfb ?? type) + " could not be plugged into " + where + " of '" + dev.Name +
                    "'. The module may not fit this station, or the slot is occupied." + (lastError == null ? "" : " Last error: " + lastError));
            }
            if (!slot.HasValue) r["slot_defaulted"] = true;
            r["plugged"] = true;
            r["station"] = dev.Name;
            r["into"] = used.Name;
            r["name"] = item.Name;
            r["slot"] = item.PositionNumber;
            ArrayList addr = Addresses(item, 2);
            if (addr.Count > 0) r["io_addresses"] = addr;
            return r;
        }

        static string UsedSlots(HardwareObject rack)
        {
            StringBuilder sb = new StringBuilder();
            foreach (DeviceItem i in rack.DeviceItems)
                sb.Append(sb.Length > 0 ? ", " : "").Append(i.PositionNumber).Append("=").Append(i.Name);
            return sb.Length > 0 ? sb.ToString() : "(none)";
        }

        static ArrayList Addresses(DeviceItem item, int depth)
        {
            ArrayList list = new ArrayList();
            try
            {
                foreach (Address a in item.Addresses)
                    list.Add(U.Obj("io_type", a.IoType.ToString(), "start_byte", a.StartAddress, "length_bits", a.Length));
            }
            catch { }
            if (depth > 0)
                foreach (DeviceItem sub in item.DeviceItems)
                    foreach (object o in Addresses(sub, depth - 1)) list.Add(o);
            return list;
        }

        // ---------------- security ----------------

        static void FindSecurity(DeviceItemComposition items, ref PlcMasterSecretConfigurator ms, ref PlcAccessLevelProvider al)
        {
            foreach (DeviceItem i in items)
            {
                if (ms == null) ms = i.GetService<PlcMasterSecretConfigurator>();
                if (al == null) al = i.GetService<PlcAccessLevelProvider>();
                if (ms != null && al != null) return;
                FindSecurity(i.DeviceItems, ref ms, ref al);
            }
        }

        static Dictionary<string, object> SecurityStatus(Device dev)
        {
            PlcMasterSecretConfigurator ms = null; PlcAccessLevelProvider al = null;
            FindSecurity(dev.DeviceItems, ref ms, ref al);
            if (ms == null && al == null) return null;
            Dictionary<string, object> r = U.Obj();
            if (ms != null) { try { r["confidential_data_protection"] = ms.MasterSecretConfiguration.ToString(); } catch (Exception ex) { r["confidential_data_protection_error"] = ex.Message; } }
            if (al != null) { try { r["access_level"] = al.PlcProtectionAccessLevel.ToString(); } catch (Exception ex) { r["access_level_error"] = ex.Message; } }
            return r;
        }

        static ArrayList RemoveProtection(Device dev)
        {
            PlcMasterSecretConfigurator ms = null; PlcAccessLevelProvider al = null;
            FindSecurity(dev.DeviceItems, ref ms, ref al);
            ArrayList done = new ArrayList();
            if (ms != null)
            {
                try { ms.Unprotect(); done.Add("confidential data protection removed"); }
                catch (Exception ex) { done.Add("confidential data protection NOT changed: " + ex.Message); }
            }
            if (al != null)
            {
                try { al.PlcProtectionAccessLevel = PlcProtectionAccessLevel.FullAccess; done.Add("access level set to FullAccess"); }
                catch (Exception ex) { done.Add("access level NOT changed: " + ex.Message); }
            }
            return done;
        }

        public static object PlcSecurity(string deviceName, string action)
        {
            Device dev = FindDevice(deviceName);
            PlcMasterSecretConfigurator ms = null; PlcAccessLevelProvider al = null;
            FindSecurity(dev.DeviceItems, ref ms, ref al);
            if (ms == null && al == null)
                return U.Obj("station", dev.Name, "note", "This device exposes no protection settings through Openness (older firmware or not a PLC).");
            Dictionary<string, object> r = U.Obj("station", dev.Name, "before", SecurityStatus(dev));
            if (action == "no_protection")
            {
                r["changes"] = RemoveProtection(dev);
                r["after"] = SecurityStatus(dev);
            }
            else if (action != "status") throw new ArgumentException("Unknown action: " + action);
            return r;
        }
    }
}
