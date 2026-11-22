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
    // Subnets, IP addresses and PROFINET IO system assignment.
    public static partial class Tia
    {
        // ---------------- network ----------------

        static void CollectInterfaces(DeviceItemComposition items, List<KeyValuePair<DeviceItem, NetworkInterface>> list)
        {
            foreach (DeviceItem i in items)
            {
                NetworkInterface ni = i.GetService<NetworkInterface>();
                if (ni != null) list.Add(new KeyValuePair<DeviceItem, NetworkInterface>(i, ni));
                CollectInterfaces(i.DeviceItems, list);
            }
        }

        static bool IsDp(NetType t) { return t == NetType.Profibus || t == NetType.Mpi; }

        // profibus=false: Ethernet/PROFINET interfaces. profibus=true: PROFIBUS interfaces, including combined MPI/DP ones.
        static List<KeyValuePair<DeviceItem, NetworkInterface>> Interfaces(Device dev, bool profibus)
        {
            List<KeyValuePair<DeviceItem, NetworkInterface>> all = new List<KeyValuePair<DeviceItem, NetworkInterface>>();
            CollectInterfaces(dev.DeviceItems, all);
            List<KeyValuePair<DeviceItem, NetworkInterface>> res = new List<KeyValuePair<DeviceItem, NetworkInterface>>();
            foreach (KeyValuePair<DeviceItem, NetworkInterface> kv in all)
            {
                if (kv.Value.Nodes.Count == 0) continue;
                bool match = profibus ? IsDp(kv.Value.InterfaceType) : kv.Value.InterfaceType == NetType.Ethernet;
                if (match) res.Add(kv);
            }
            return res;
        }

        static List<KeyValuePair<DeviceItem, NetworkInterface>> EthernetInterfaces(Device dev) { return Interfaces(dev, false); }

        static object Attr(IEngineeringObject o, string name)
        {
            try { return o.GetAttribute(name); } catch { return null; }
        }

        static Dictionary<string, object> InterfaceInfo(KeyValuePair<DeviceItem, NetworkInterface> kv, int index)
        {
            Node n = kv.Value.Nodes[0];
            bool dp = IsDp(kv.Value.InterfaceType);
            Dictionary<string, object> d = U.Obj("index", index, "bus", dp ? "profibus" : "profinet", "interface", kv.Key.Name,
                "subnet", n.ConnectedSubnet == null ? null : n.ConnectedSubnet.Name);
            if (dp)
            {
                d["interface_type"] = kv.Value.InterfaceType.ToString();
                d["address"] = Plain(Attr(n, "Address"));
            }
            else
            {
                d["ip"] = Attr(n, "Address"); d["subnet_mask"] = Attr(n, "SubnetMask"); d["pn_device_name"] = Attr(n, "PnDeviceName");
            }
            if (kv.Value.IoControllers.Count > 0)
            {
                IoSystem sys = kv.Value.IoControllers[0].IoSystem;
                d["io_controller_of"] = sys == null ? null : sys.Name;
            }
            if (kv.Value.IoConnectors.Count > 0)
            {
                IoSystem sys = kv.Value.IoConnectors[0].ConnectedToIoSystem;
                d["io_device_in"] = sys == null ? null : sys.Name;
            }
            return d;
        }

        static ArrayList NetworkInfo(Device dev)
        {
            ArrayList list = new ArrayList();
            List<KeyValuePair<DeviceItem, NetworkInterface>> eth = Interfaces(dev, false);
            for (int i = 0; i < eth.Count; i++) list.Add(InterfaceInfo(eth[i], i));
            List<KeyValuePair<DeviceItem, NetworkInterface>> dp = Interfaces(dev, true);
            for (int i = 0; i < dp.Count; i++) list.Add(InterfaceInfo(dp[i], i));
            return list;
        }

        static KeyValuePair<DeviceItem, NetworkInterface> PickInterface(Device dev, int index, bool profibus)
        {
            string kind = profibus ? "PROFIBUS" : "Ethernet/PROFINET";
            List<KeyValuePair<DeviceItem, NetworkInterface>> list = Interfaces(dev, profibus);
            if (list.Count == 0) throw new InvalidOperationException("Station '" + dev.Name + "' has no " + kind + " interface.");
            if (index < 0 || index >= list.Count)
                throw new InvalidOperationException("Interface index " + index + " does not exist; station '" + dev.Name + "' has " + list.Count + " " + kind + " interface(s).");
            return list[index];
        }

        // A combined MPI/DP interface starts as MPI; it has to be PROFIBUS before it can join a PROFIBUS subnet.
        static void EnsureProfibus(Node node, string owner, ArrayList done)
        {
            if (node.NodeType == NetType.Profibus) return;
            node.SetAttribute("NodeType", NetType.Profibus);
            done.Add("interface of '" + owner + "' switched from " + "MPI to PROFIBUS");
        }

        public static object NetworkConnect(string stationName, string subnetName, string address, string mask, int ifIndex,
            string controllerName, int controllerIfIndex, string pnName, string bus)
        {
            Project p = Proj();
            bool pb = string.Equals(bus, "profibus", StringComparison.OrdinalIgnoreCase);
            if (!pb && !string.IsNullOrEmpty(bus) && !string.Equals(bus, "profinet", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Unknown bus '" + bus + "'. Use profinet or profibus.");
            string sysKind = pb ? "DP master system" : "IO system";
            Device dev = FindDevice(stationName);
            KeyValuePair<DeviceItem, NetworkInterface> itf = PickInterface(dev, ifIndex, pb);
            Node node = itf.Value.Nodes[0];
            ArrayList done = new ArrayList();

            Device ctrl = null;
            KeyValuePair<DeviceItem, NetworkInterface> citf = new KeyValuePair<DeviceItem, NetworkInterface>();
            if (!string.IsNullOrEmpty(controllerName))
            {
                ctrl = FindDevice(controllerName);
                citf = PickInterface(ctrl, controllerIfIndex, pb);
                if (pb) EnsureProfibus(citf.Value.Nodes[0], ctrl.Name, done);
                // default subnet: the one the controller is already on
                Subnet cs = citf.Value.Nodes[0].ConnectedSubnet;
                if (string.IsNullOrEmpty(subnetName) && cs != null) subnetName = cs.Name;
            }
            if (pb) EnsureProfibus(node, dev.Name, done);
            if (string.IsNullOrEmpty(subnetName)) subnetName = pb ? "PROFIBUS_1" : "PN/IE_1";

            Subnet subnet = p.Subnets.Find(subnetName);
            if (node.ConnectedSubnet == null || subnet == null || node.ConnectedSubnet.Name != subnet.Name)
            {
                if (subnet == null) { subnet = node.CreateAndConnectToSubnet(subnetName); done.Add("subnet '" + subnetName + "' created"); }
                else node.ConnectToSubnet(subnet);
                done.Add("'" + dev.Name + "' connected to '" + subnet.Name + "'");
            }

            if (!string.IsNullOrEmpty(address))
            {
                object v = address;
                if (pb) { object cur = Attr(node, "Address"); if (cur != null && !(cur is string)) v = Convert.ChangeType(address, cur.GetType(), System.Globalization.CultureInfo.InvariantCulture); }
                node.SetAttribute("Address", v);
                done.Add((pb ? "PROFIBUS address" : "IP") + " set to " + address);
            }
            if (!pb && !string.IsNullOrEmpty(mask)) { node.SetAttribute("SubnetMask", mask); done.Add("subnet mask set to " + mask); }

            if (ctrl != null)
            {
                Node cnode = citf.Value.Nodes[0];
                if (cnode.ConnectedSubnet == null || cnode.ConnectedSubnet.Name != subnet.Name)
                {
                    cnode.ConnectToSubnet(subnet);
                    done.Add("controller '" + ctrl.Name + "' connected to '" + subnet.Name + "'");
                }
                // read the services again: they can change once the interface is on a subnet
                if (citf.Value.IoControllers.Count == 0)
                    throw new InvalidOperationException("Interface " + controllerIfIndex + " of '" + ctrl.Name + "' cannot act as " + (pb ? "DP master" : "PROFINET IO controller") + ".");
                if (itf.Value.IoConnectors.Count == 0)
                    throw new InvalidOperationException("Station '" + dev.Name + "' cannot be a " + (pb ? "DP slave" : "PROFINET IO device") + " on interface " + ifIndex + ".");
                IoController ioc = citf.Value.IoControllers[0];
                IoSystem sys = ioc.IoSystem;
                if (sys == null) { sys = ioc.CreateIoSystem(""); done.Add(sysKind + " '" + sys.Name + "' created on '" + ctrl.Name + "'"); }
                IoConnector con = itf.Value.IoConnectors[0];
                if (con.ConnectedToIoSystem == null || con.ConnectedToIoSystem.Name != sys.Name)
                {
                    con.ConnectToIoSystem(sys);
                    done.Add("'" + dev.Name + "' assigned to " + sysKind + " '" + sys.Name + "'");
                }
            }

            if (!pb && !string.IsNullOrEmpty(pnName))
            {
                try { node.SetAttribute("PnDeviceNameAutoGeneration", false); } catch { }
                node.SetAttribute("PnDeviceName", pnName);
                done.Add("PROFINET device name set to " + pnName);
            }

            Dictionary<string, object> r = U.Obj("station", dev.Name, "changes", done, "interface", InterfaceInfo(itf, ifIndex));
            if (ctrl != null) r["controller_interface"] = InterfaceInfo(citf, controllerIfIndex);
            return r;
        }

        // ---------------- IO addresses ----------------

        static void CollectAddresses(DeviceItem item, List<Address> list)
        {
            try { foreach (Address a in item.Addresses) list.Add(a); } catch { }
            foreach (DeviceItem sub in item.DeviceItems) CollectAddresses(sub, list);
        }

        public static object IoAddressSet(string stationName, string itemName, string ioType, int startByte)
        {
            Device dev = FindDevice(stationName);
            DeviceItem item = FindItemByName(dev.DeviceItems, itemName, 5);
            if (item == null) throw new InvalidOperationException("Station '" + dev.Name + "' has no item named '" + itemName + "'.");
            bool wantInput = ioType == null || ioType.StartsWith("i", StringComparison.OrdinalIgnoreCase);
            if (ioType != null && !wantInput && !ioType.StartsWith("o", StringComparison.OrdinalIgnoreCase) && !ioType.StartsWith("q", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("io_type must be input or output.");
            List<Address> all = new List<Address>();
            CollectAddresses(item, all);
            List<Address> match = new List<Address>();
            foreach (Address a in all)
            {
                if (a.IoType == AddressIoType.Input && (ioType == null || wantInput)) match.Add(a);
                else if (a.IoType == AddressIoType.Output && (ioType == null || !wantInput)) match.Add(a);
            }
            if (match.Count == 0) throw new InvalidOperationException("Item '" + item.Name + "' has no " + (ioType ?? "input/output") + " address.");
            if (match.Count > 1) throw new InvalidOperationException("Item '" + item.Name + "' has " + match.Count + " matching address ranges; give io_type (input or output) or a more specific item.");
            Address adr = match[0];
            int old = adr.StartAddress;
            if (old != startByte) adr.StartAddress = startByte;
            string pre = adr.IoType == AddressIoType.Input ? "%I" : "%Q";
            int bytes = (adr.Length + 7) / 8;
            return U.Obj("station", dev.Name, "item", item.Name, "io_type", adr.IoType.ToString(), "previous_start_byte", old,
                "start_byte", adr.StartAddress, "length_bits", adr.Length,
                "range", pre + adr.StartAddress + ".0 - " + pre + (adr.StartAddress + bytes - 1) + ".7", "changed", old != adr.StartAddress);
        }
    }
}
