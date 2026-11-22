#if !TIA_V18
using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.CommunicationConnections;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;

namespace TiaAssistant
{
    // V21+: HMI <-> PLC integrated connection through the hardware connection API (in V18 it had to be dragged by hand).
    // Works for classic panels (Basic/Comfort) and WinCC Unified. Classic panels: binding HMI tags to PLC tags stays
    // a TIA editor task (V21 Openness refuses the symbolic ControllerTag link on classic HMI tags).
    public static partial class Tia
    {
        static IEnumerable<DeviceItem> ItemsDeep(DeviceItemComposition items)
        {
            foreach (DeviceItem i in items)
            {
                yield return i;
                foreach (DeviceItem c in ItemsDeep(i.DeviceItems)) yield return c;
            }
        }

        static IEnumerable<Node> NodesOf(Device dev)
        {
            foreach (DeviceItem i in ItemsDeep(dev.DeviceItems))
            {
                NetworkInterface ni = i.GetService<NetworkInterface>();
                if (ni == null || ni.InterfaceType != NetType.Ethernet) continue;
                foreach (Node n in ni.Nodes) yield return n;
            }
        }

        public static object HmiConnect(string hmiName, string plcName, string connectionName)
        {
            Device hmi = FindDevice(hmiName), plcDev = FindDevice(plcName);
            DeviceItem cpu = CpuItem(plcDev.DeviceItems);
            if (cpu == null) throw new InvalidOperationException("'" + plcName + "' has no PLC CPU.");

            // The panel item that owns the HMI software also carries the connection list (e.g. HMI_RT_1).
            CommunicationManagement cm = null; DeviceItem rt = null;
            foreach (DeviceItem i in ItemsDeep(hmi.DeviceItems))
            {
                SoftwareContainer sc = i.GetService<SoftwareContainer>();
                CommunicationManagement c = i.GetService<CommunicationManagement>();
                if (c != null && sc != null && sc.Software != null && !(sc.Software is PlcSoftware)) { cm = c; rt = i; break; }
                if (c != null && cm == null) { cm = c; rt = i; }
            }
            if (cm == null) throw new InvalidOperationException("'" + hmiName + "' offers no connection management (not an HMI device?).");

            foreach (Connection ex in cm.Connections)
            {
                HmiConnection h = ex as HmiConnection;
                if (h == null) continue;
                DeviceItem partner = null; try { partner = h.GetAttribute("PartnerTarget") as DeviceItem; } catch { }
                if (partner != null && partner.Name == cpu.Name)
                    return U.Obj("status", "exists", "connection", h.GetAttribute("LocalConnectionName"), "hmi", hmi.Name, "plc", cpu.Name,
                        "note", "The panel is already connected to this PLC.");
            }

            // Both ends on the same subnet.
            Node local = null, partnerNode = null;
            foreach (Node ln in NodesOf(hmi))
            {
                Subnet s = ln.ConnectedSubnet;
                if (s == null) continue;
                partnerNode = NodesOf(plcDev).FirstOrDefault(pn => pn.ConnectedSubnet != null && pn.ConnectedSubnet.Name == s.Name);
                if (partnerNode != null) { local = ln; break; }
            }
            if (local == null)
                throw new InvalidOperationException("'" + hmiName + "' and '" + plcName + "' are not on a common Ethernet subnet. Call network_connect for both first.");

            HmiConnection conn = cm.Connections.Create<HmiConnection>(local, cpu, partnerNode);
            if (!string.IsNullOrEmpty(connectionName)) conn.SetAttribute("LocalConnectionName", connectionName);
            return U.Obj("status", "created", "connection", conn.GetAttribute("LocalConnectionName"), "hmi", hmi.Name, "runtime_item", rt.Name,
                "plc", cpu.Name, "subnet", local.ConnectedSubnet.Name, "local_address", conn.GetAttribute("LocalAddress"),
                "partner_address", conn.GetAttribute("PartnerAddress"), "valid", conn.GetAttribute("IsValid"),
                "note", "Integrated HMI connection. Classic panels: bind IO fields / HMI tags to PLC tags and DB members in the TIA editor. Project not saved; call project_save.");
        }
    }
}
#endif
