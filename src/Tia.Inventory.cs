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
    // Read-only project inventory (project_tree).
    public static partial class Tia
    {
        // ---------------- inventory ----------------

        public static object ProjectTree(int depth)
        {
            Proj();
            ArrayList devs = new ArrayList();
            foreach (Device d in AllDevices())
            {
                Dictionary<string, object> dd = U.Obj("station", d.Name, "type_identifier", d.TypeIdentifier, "items", ItemTree(d.DeviceItems, depth));
                ArrayList io = new ArrayList();
                CollectIo(d.DeviceItems, null, io);
                if (io.Count > 0) dd["io_addresses"] = io;
                ArrayList net = NetworkInfo(d);
                if (net.Count > 0) dd["network"] = net;
                PlcSoftware plc = FindPlcSoftware(d.DeviceItems);
                if (plc != null) dd["plc_software"] = PlcInfo(plc);
                devs.Add(dd);
            }
            return U.Obj("project", ProjectInfo(), "devices", devs);
        }

        // Every input/output address range of the station, including on-board IO of compact CPUs.
        // Bit n of a range is %I<start + n/8>.<n%8> (or %Q...); analog channels are words (%IW<start>, %IW<start+2>, ...).
        static void CollectIo(DeviceItemComposition items, string module, ArrayList list)
        {
            foreach (DeviceItem i in items)
            {
                string owner = module == null || !string.IsNullOrEmpty(i.TypeIdentifier) ? i.Name : module + " / " + i.Name;
                try
                {
                    foreach (Address a in i.Addresses)
                    {
                        if (a.IoType != AddressIoType.Input && a.IoType != AddressIoType.Output) continue;
                        if (a.StartAddress < 0 || a.Length <= 0) continue;
                        int bytes = (a.Length + 7) / 8;
                        string pre = a.IoType == AddressIoType.Input ? "%I" : "%Q";
                        list.Add(U.Obj("item", owner, "io_type", a.IoType.ToString(), "start_byte", a.StartAddress,
                            "length_bits", a.Length, "range", pre + a.StartAddress + ".0 - " + pre + (a.StartAddress + bytes - 1) + ".7",
                            "channels", i.Channels.Count));
                    }
                }
                catch { }
                CollectIo(i.DeviceItems, owner, list);
            }
        }

        static ArrayList ItemTree(DeviceItemComposition items, int depth)
        {
            ArrayList list = new ArrayList();
            foreach (DeviceItem i in items)
            {
                Dictionary<string, object> d = U.Obj("name", i.Name, "slot", i.PositionNumber);
                if (!string.IsNullOrEmpty(i.TypeIdentifier)) d["type_identifier"] = i.TypeIdentifier;
                if (i.Classification != DeviceItemClassifications.None) d["class"] = i.Classification.ToString();
                if (depth > 1)
                {
                    ArrayList sub = ItemTree(i.DeviceItems, depth - 1);
                    // built-in sub items without an own article number are noise; keep only real modules
                    ArrayList keep = new ArrayList();
                    foreach (Dictionary<string, object> s in sub) if (s.ContainsKey("type_identifier") || s.ContainsKey("items")) keep.Add(s);
                    if (keep.Count > 0) d["items"] = keep;
                }
                list.Add(d);
            }
            return list;
        }

        static Dictionary<string, object> PlcInfo(PlcSoftware plc)
        {
            ArrayList tables = new ArrayList();
            CollectTagTables(plc.TagTableGroup.TagTables, plc.TagTableGroup.Groups, tables);
            ArrayList blocks = new ArrayList();
            CollectBlocks(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, "", blocks);
            ArrayList types = new ArrayList();
            CollectTypes(plc.TypeGroup.Types, plc.TypeGroup.Groups, types);
            return U.Obj("name", plc.Name, "tag_tables", tables, "blocks", blocks, "data_types", types);
        }

        static void CollectTagTables(PlcTagTableComposition tables, PlcTagTableUserGroupComposition groups, ArrayList list)
        {
            foreach (PlcTagTable t in tables) list.Add(U.Obj("name", t.Name, "tags", t.Tags.Count));
            foreach (PlcTagTableUserGroup g in groups) CollectTagTables(g.TagTables, g.Groups, list);
        }

        static void CollectBlocks(PlcBlockComposition blocks, PlcBlockUserGroupComposition groups, string path, ArrayList list)
        {
            foreach (PlcBlock b in blocks)
                list.Add(U.Obj("name", b.Name, "kind", b.GetType().Name, "number", b.Number,
                    "language", b.ProgrammingLanguage.ToString(), "folder", path));
            foreach (PlcBlockUserGroup g in groups) CollectBlocks(g.Blocks, g.Groups, path + "/" + g.Name, list);
        }

        static void CollectTypes(PlcTypeComposition types, PlcTypeUserGroupComposition groups, ArrayList list)
        {
            foreach (PlcType t in types) list.Add(t.Name);
            foreach (PlcTypeUserGroup g in groups) CollectTypes(g.Types, g.Groups, list);
        }
    }
}
