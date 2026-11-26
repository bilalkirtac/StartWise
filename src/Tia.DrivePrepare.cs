using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Siemens.Engineering;
using Siemens.Engineering.Library;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.TechnologicalObjects;
using Siemens.Engineering.SW.TechnologicalObjects.Motion;

namespace TiaAssistant
{
    // "Drive added -> ready for scenario writing" in one call (knowledge/scenarios/surucu-ekle-uctan-uca.md).
    // Servo (TO telegram, e.g. 105): TO_PositioningAxis (version from CPU FW) + actor connection + line topology +
    //   S210_AbsCnt FB + instance (the instance DB is the drive's interface) + network in LAD FC "Servo Motors" + OB1 call.
    // Async (telegram 1/20/352): SINA_SPEED (from DriveLib) + instance (= interface) +
    //   network in LAD FC "Asenkron Motors" (HWIDSTW/HWIDZSW = HW identifier constant) + OB1 call.
    // Every step is idempotent: existing objects are kept, only missing ones are created.
    public static partial class Tia
    {
        const string ServoFc = "Servo Motors", AsyncFc = "Asenkron Motors", MainOb = "Main";
        static readonly int[] ServoTelegrams = { 3, 4, 5, 6, 102, 103, 105, 106, 111, 116, 118, 125, 136, 138, 139 };
        static readonly int[] AsyncTelegrams = { 1, 2, 20, 352 };

        static readonly string[][] S210Pins = {
            new[]{"Enable","Input","Bool","Cmd.Enable"}, new[]{"Reset","Input","Bool","Cmd.Reset"}, new[]{"Home","Input","Bool","Cmd.Home"},
            new[]{"HomeMode","Input","Int","Cmd.HomeMode"}, new[]{"HomePosition","Input","LReal","Cmd.HomePosition"},
            new[]{"StartPos","Input","Bool","Cmd.StartPos"}, new[]{"Position","Input","LReal","Cmd.Position"},
            new[]{"Velocity","Input","LReal","Cmd.Velocity"}, new[]{"Direction","Input","Int","Cmd.Direction"},
            new[]{"JogFwd","Input","Bool","Cmd.JogFwd"}, new[]{"JogBwd","Input","Bool","Cmd.JogBwd"},
            new[]{"JogVelocity","Input","LReal","Cmd.JogVelocity"}, new[]{"Stop","Input","Bool","Cmd.Stop"},
            new[]{"StandStillVel","Input","LReal","Cmd.StandStillVel"},
            new[]{"Powered","Output","Bool","Sts.Powered"}, new[]{"Homed","Output","Bool","Sts.Homed"}, new[]{"Busy","Output","Bool","Sts.Busy"},
            new[]{"InPosition","Output","Bool","Sts.InPosition"}, new[]{"StandStill","Output","Bool","Sts.StandStill"},
            new[]{"ActPos","Output","LReal","Sts.ActPos"}, new[]{"ActVel","Output","LReal","Sts.ActVel"},
            new[]{"Error","Output","Bool","Sts.Error"}, new[]{"ErrorID","Output","Word","Sts.ErrorID"} };

        static readonly string[][] SinaPins = {
            new[]{"EnableAxis","Input","Bool","Cmd.EnableAxis"}, new[]{"AckError","Input","Bool","Cmd.AckError"},
            new[]{"SpeedSp","Input","Real","Cmd.SpeedSp"}, new[]{"RefSpeed","Input","Real","Cmd.RefSpeed"},
            new[]{"ConfigAxis","Input","Word","Cmd.ConfigAxis"},
            new[]{"AxisEnabled","Output","Bool","Sts.AxisEnabled"}, new[]{"Lockout","Output","Bool","Sts.Lockout"},
            new[]{"ActVelocity","Output","Real","Sts.ActVelocity"}, new[]{"Error","Output","Bool","Sts.Error"},
            new[]{"Status","Output","Word","Sts.Status"}, new[]{"DiagId","Output","Word","Sts.DiagId"} };

        class Pin { public string Name, Section, Type, Source; }   // Source: "DB.Member.Sub", "const:<name>" or null (open)

        // ---------- LAD XML ----------
        static string LadAccess(int uid, string src)
        {
            if (src.StartsWith("const:"))
                return "      <Access Scope=\"GlobalConstant\" UId=\"" + uid + "\">\n        <Constant Name=\"" + SecurityElement.Escape(src.Substring(6)) + "\" />\n      </Access>";
            string comps = string.Concat(src.Split('.').Select(p => "<Component Name=\"" + SecurityElement.Escape(p) + "\" />").ToArray());
            return "      <Access Scope=\"GlobalVariable\" UId=\"" + uid + "\">\n        <Symbol>" + comps + "</Symbol>\n      </Access>";
        }

        static string FbCallFlgNet(string fb, string instance, List<Pin> pins)
        {
            int uid = 21; List<string> parts = new List<string>(), wires = new List<string>();
            int call = uid++, inst = uid++;
            parts.Add("      <Call UId=\"" + call + "\">\n      <CallInfo Name=\"" + SecurityElement.Escape(fb) + "\" BlockType=\"FB\">\n        <Instance Scope=\"GlobalVariable\" UId=\"" + inst +
                "\">\n          <Component Name=\"" + SecurityElement.Escape(instance) + "\" />\n        </Instance>\n" +
                string.Join("\n", pins.Select(p => "        <Parameter Name=\"" + p.Name + "\" Section=\"" + p.Section + "\" Type=\"" + p.Type + "\" />").ToArray()) +
                "\n      </CallInfo>\n    </Call>");
            Action<string> wire = inner => { wires.Add("    <Wire UId=\"" + uid + "\">\n" + inner + "\n    </Wire>"); uid++; };
            wire("      <Powerrail />\n      <NameCon UId=\"" + call + "\" Name=\"en\" />");
            foreach (Pin p in pins)
            {
                if (p.Source == null)
                {
                    // Open pin: scenario code reads/writes it in the instance DB. LAD import needs an OpenCon wire;
                    // leaving the pin out of <Wires> fails ("The connection with the name '1' is not connected ...").
                    int oc = uid++;
                    if (p.Section == "Input") wire("      <OpenCon UId=\"" + oc + "\" />\n      <NameCon UId=\"" + call + "\" Name=\"" + p.Name + "\" />");
                    else wire("      <NameCon UId=\"" + call + "\" Name=\"" + p.Name + "\" />\n      <OpenCon UId=\"" + oc + "\" />");
                    continue;
                }
                int a = uid++;
                parts.Add(LadAccess(a, p.Source));
                if (p.Section == "Output") wire("      <NameCon UId=\"" + call + "\" Name=\"" + p.Name + "\" />\n      <IdentCon UId=\"" + a + "\" />");
                else wire("      <IdentCon UId=\"" + a + "\" />\n      <NameCon UId=\"" + call + "\" Name=\"" + p.Name + "\" />");
            }
            return "<FlgNet xmlns=\"http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4\">\n  <Parts>\n" + string.Join("\n", parts.ToArray()) +
                "\n  </Parts>\n  <Wires>\n" + string.Join("\n", wires.ToArray()) + "\n  </Wires>\n</FlgNet>";
        }

        static string FcCallFlgNet(string fc)
        {
            return "<FlgNet xmlns=\"http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4\">\n  <Parts>\n    <Call UId=\"21\">\n      <CallInfo Name=\"" +
                SecurityElement.Escape(fc) + "\" BlockType=\"FC\" />\n    </Call>\n  </Parts>\n  <Wires>\n    <Wire UId=\"22\">\n      <Powerrail />\n      <NameCon UId=\"21\" Name=\"en\" />\n    </Wire>\n  </Wires>\n</FlgNet>";
        }

        static string DpMlText(int id, string comp, string text)
        {
            return "<MultilingualText ID=\"" + id.ToString("X") + "\" CompositionName=\"" + comp + "\"><ObjectList><MultilingualTextItem ID=\"" + (id + 1).ToString("X") +
                "\" CompositionName=\"Items\"><AttributeList><Culture>en-US</Culture>" + (string.IsNullOrEmpty(text) ? "<Text />" : "<Text>" + SecurityElement.Escape(text) + "</Text>") +
                "</AttributeList></MultilingualTextItem></ObjectList></MultilingualText>";
        }

        static string LadUnit(ref int id, string flgNet, string title)
        {
            int b = id; id += 5;
            return "<SW.Blocks.CompileUnit ID=\"" + b.ToString("X") + "\" CompositionName=\"CompileUnits\"><AttributeList><NetworkSource>" + flgNet +
                "</NetworkSource><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList><ObjectList>" + DpMlText(b + 1, "Comment", null) + DpMlText(b + 3, "Title", title) +
                "</ObjectList></SW.Blocks.CompileUnit>";
        }

        static string NewLadFcXml(string name, string comment, string firstFlgNet, string firstTitle)
        {
            int id = 3;
            string unit = LadUnit(ref id, firstFlgNet, firstTitle);
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Document>\n<Engineering version=\"" + Program.TiaName + "\" />\n<SW.Blocks.FC ID=\"0\"><AttributeList><AutoNumber>true</AutoNumber>" +
                "<HeaderAuthor>StartWise</HeaderAuthor><HeaderFamily /><HeaderName /><HeaderVersion>0.1</HeaderVersion><Interface><Sections xmlns=\"http://www.siemens.com/automation/Openness/SW/Interface/v5\">" +
                "<Section Name=\"Input\" /><Section Name=\"Output\" /><Section Name=\"InOut\" /><Section Name=\"Temp\" /><Section Name=\"Constant\" /><Section Name=\"Return\">" +
                "<Member Name=\"Ret_Val\" Datatype=\"Void\" Accessibility=\"Public\" /></Section></Sections></Interface><IsIECCheckEnabled>false</IsIECCheckEnabled>" +
                "<MemoryLayout>Optimized</MemoryLayout><Name>" + SecurityElement.Escape(name) + "</Name><Namespace /><ProgrammingLanguage>LAD</ProgrammingLanguage>" +
                "<SetENOAutomatically>false</SetENOAutomatically><UDABlockProperties /><UDAEnableTagReadback>false</UDAEnableTagReadback></AttributeList><ObjectList>" +
                DpMlText(1, "Comment", comment) + unit + DpMlText(id, "Title", name) + "</ObjectList></SW.Blocks.FC>\n</Document>\n";
        }

        static string ExportBlockXml(PlcBlock b)
        {
            return ExportToText(delegate(FileInfo f) { b.Export(f, ExportOptions.WithDefaults); });
        }

        static void ImportBlockXml(PlcSoftware plc, string xml)
        {
            string file = Path.Combine(Path.GetTempPath(), "startwise_" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(file, xml, new UTF8Encoding(true));
            try { plc.BlockGroup.Blocks.Import(new FileInfo(file), ImportOptions.Override); }
            finally { try { File.Delete(file); } catch { } }
        }

        // Adds a LAD network to an existing LAD block (export -> insert -> import).
        // Returns "exists" when 'marker' is already present, "replaced" when 'rewrite' says the network holding 'marker'
        // is an old layout (rewritten in place), else "added".
        static string AppendLadNetwork(PlcSoftware plc, PlcBlock block, string flgNet, string title, string marker, Func<string, bool> rewrite)
        {
            string xml;
            try { xml = ExportBlockXml(block); }
            catch (Exception)
            {
                // Blocks just imported leave the program inconsistent; inconsistent blocks cannot be exported. Compile once and retry.
                ICompilable c = plc.GetService<ICompilable>();
                if (c == null) throw;
                c.Compile();
                xml = ExportBlockXml(block);
            }
            if (!Regex.IsMatch(xml, @"<ProgrammingLanguage>(LAD|F_LAD)</ProgrammingLanguage>"))
                throw new InvalidOperationException("Block '" + block.Name + "' is not LAD; StartWise only appends networks to LAD blocks.");
            int id = Regex.Matches(xml, @"\bID=""([0-9A-F]+)""").Cast<Match>().Max(m => Convert.ToInt32(m.Groups[1].Value, 16)) + 1;
            if (xml.Contains(marker))
            {
                if (rewrite == null) return "exists";
                foreach (Match u in Regex.Matches(xml, @"<SW\.Blocks\.CompileUnit\b.*?</SW\.Blocks\.CompileUnit>", RegexOptions.Singleline))
                {
                    if (!u.Value.Contains(marker)) continue;
                    if (!rewrite(u.Value)) return "exists";
                    string unitNew = LadUnit(ref id, flgNet, title);
                    ImportBlockXml(plc, xml.Substring(0, u.Index) + unitNew + xml.Substring(u.Index + u.Length));
                    return "replaced";
                }
                return "exists";
            }
            string unit = LadUnit(ref id, flgNet, title);
            int at = xml.LastIndexOf("</SW.Blocks.CompileUnit>", StringComparison.Ordinal);
            if (at < 0) throw new InvalidOperationException("Block '" + block.Name + "' has no network to append after.");
            at += "</SW.Blocks.CompileUnit>".Length;
            ImportBlockXml(plc, xml.Substring(0, at) + "\n" + unit + xml.Substring(at));
            return "added";
        }

        // ---------- helpers ----------
        static string Template(string file)
        {
            string p = Path.Combine(U.KnowledgeDir(), "templates", file);
            if (!File.Exists(p)) throw new InvalidOperationException("Template missing: " + p);
            return File.ReadAllText(p, Encoding.UTF8);
        }

        static bool BlockExists(PlcSoftware plc, string name) { return FindBlock(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, name) != null; }
        static bool TypeExists(PlcSoftware plc, string name) { return FindType(plc.TypeGroup.Types, plc.TypeGroup.Groups, name) != null; }

        static DeviceItem CpuItem(DeviceItemComposition items)
        {
            foreach (DeviceItem i in items)
            {
                SoftwareContainer sc = i.GetService<SoftwareContainer>();
                if (sc != null && sc.Software is PlcSoftware) return i;
                DeviceItem r = CpuItem(i.DeviceItems);
                if (r != null) return r;
            }
            return null;
        }

        static NetworkInterface FirstInterface(DeviceItemComposition items, Func<NetworkInterface, bool> ok)
        {
            foreach (DeviceItem i in items)
            {
                NetworkInterface ni = i.GetService<NetworkInterface>();
                if (ni != null && ni.InterfaceType == NetType.Ethernet && ok(ni)) return ni;
                NetworkInterface r = FirstInterface(i.DeviceItems, ok);
                if (r != null) return r;
            }
            return null;
        }

        // Line topology: from the PLC's last port follow already cabled devices (their other port) to the first free port, then cable it to the drive's first port.
        static string LineTopology(Device plcDev, Device driveDev)
        {
            NetworkInterface plcIf = FirstInterface(plcDev.DeviceItems, n => n.Ports.Count > 0 && (n.InterfaceOperatingMode & InterfaceOperatingModes.IoController) != 0);
            NetworkInterface drvIf = FirstInterface(driveDev.DeviceItems, n => n.Ports.Count > 0);
            if (plcIf == null || drvIf == null) return "no PROFINET ports found";
            NetworkPort target = drvIf.Ports[0];
            foreach (NetworkPort p in drvIf.Ports) if (p.ConnectedPorts.Count > 0) return "drive already cabled (" + PortName(p) + ")";
            NetworkPort cur = plcIf.Ports[plcIf.Ports.Count - 1];
            for (int hop = 0; hop < 64; hop++)
            {
                if (cur.ConnectedPorts.Count == 0) { cur.ConnectToPort(target); return "cabled " + PortName(cur) + " -> " + PortName(target); }
                NetworkPort partner = cur.ConnectedPorts[0];
                NetworkInterface pif = partner.Interface;
                NetworkPort next = null;
                foreach (NetworkPort q in pif.Ports) if (q != partner && !q.Equals(partner)) { next = q; break; }
                if (next == null) return "line ends at " + PortName(partner) + " (single-port device); cable the drive by hand";
                cur = next;
            }
            return "line too long; cable the drive by hand";
        }

        static string PortName(NetworkPort p)
        {
            string port = "?", dev = "";
            try { DeviceItem pi = (DeviceItem)p.Parent; port = pi.Name; dev = ((DeviceItem)pi.Parent).Name; DeviceItem up = pi.Parent.Parent as DeviceItem; if (up != null) dev = up.Name; } catch { }
            return dev + "/" + port;
        }

        static readonly Dictionary<string, string> FwToTo = new Dictionary<string, string> { { "V4.1", "8.0" }, { "V4.0", "8.0" }, { "V3.1", "8.0" }, { "V3.0", "7.0" }, { "V2.9", "6.0" }, { "V2.8", "5.0" }, { "V2.6", "4.0" }, { "V2.5", "3.0" } };

        // ---------- main ----------
        public static object DrivePrepare(string plcName, string driveName, string kind, string topology, bool dryRun)
        {
            ArrayList steps = new ArrayList();
            Action<string, string, string> step = (s, st, d) => steps.Add(U.Obj("step", s, "status", st, "detail", d));
            PlcSoftware plc = PlcOf(plcName);
            Device plcDev = FindDevice(plcName);
            Device dev;
            Siemens.Engineering.MC.Drives.DriveObject dobj = null;
            try { dobj = DriveObjectOf(driveName, 0, out dev); } catch { dev = FindDevice(driveName); }
            string drive = dev.Name.EndsWith("_Station") ? dev.Name.Substring(0, dev.Name.Length - 8) : dev.Name;

            int telegram = 0, inByte = -1, outByte = -1;
            if (dobj != null)
                foreach (Siemens.Engineering.MC.Drives.Telegram t in dobj.Telegrams)
                    if (t.Type.ToString() == "MainTelegram")
                    {
                        telegram = t.TelegramNumber;
                        foreach (Address a in t.Addresses) { if (a.IoType == AddressIoType.Input) inByte = a.StartAddress; else if (a.IoType == AddressIoType.Output) outByte = a.StartAddress; }
                    }
            if (string.IsNullOrEmpty(kind) || kind == "auto")
                kind = ServoTelegrams.Contains(telegram) ? "servo" : AsyncTelegrams.Contains(telegram) || dobj == null ? "async" : null;
            if (kind == null) throw new InvalidOperationException("Main telegram " + telegram + " of '" + drive + "' is neither a TO (servo) nor a speed (async) telegram. Give kind='servo' or 'async'.");
            step("detect", "ok", drive + ": " + kind + ", main telegram " + telegram + (inByte >= 0 ? ", I/Q bytes " + inByte + "/" + outByte : ""));
            string io = drive + "_IO";

            if (dryRun)
            {
                string fc = kind == "servo" ? ServoFc : AsyncFc;
                step("plan", "dry_run", kind == "servo"
                    ? "TO '" + drive + "' + actor connect, line topology, S210_AbsCnt, instance S210_AbsCnt_" + drive + " (= interface), network in '" + fc + "', OB1 call"
                    : "SINA_SPEED, instance SINA_SPEED_" + drive + " (= interface), network in '" + fc + "' (HW ID constant), OB1 call");
                return U.Obj("drive", drive, "kind", kind, "steps", steps, "dry_run", true);
            }

            string fcName, flg, title, marker, iface, wired;
            if (kind == "servo")
            {
                if (inByte < 0) throw new InvalidOperationException("Telegram addresses of '" + drive + "' not found; servo path needs a Startdrive drive.");
                // TO version from CPU firmware
                DeviceItem cpu = CpuItem(plcDev.DeviceItems);
                string fw = cpu == null ? null : Convert.ToString(cpu.GetAttribute("FirmwareVersion"));
                TechnologicalInstanceDB to = plc.TechnologicalObjectGroup.TechnologicalObjects.Find(drive);
                string toVer;
                if (to == null)
                {
                    List<string> tries = new List<string>();
                    if (fw != null && FwToTo.ContainsKey(fw)) tries.Add(FwToTo[fw]);
                    foreach (string v in new[] { "8.0", "7.0", "6.0", "5.0", "4.0", "3.0" }) if (!tries.Contains(v)) tries.Add(v);
                    Exception last = null;
                    foreach (string v in tries)
                    {
                        try { to = plc.TechnologicalObjectGroup.TechnologicalObjects.Create(drive, "TO_PositioningAxis", new Version(v)); break; }
                        catch (Exception e) { last = e; }
                    }
                    if (to == null) throw new InvalidOperationException("Could not create TO_PositioningAxis for CPU FW " + fw + ": " + (last != null ? last.Message : ""));
                    step("technology object", "created", "TO_PositioningAxis " + to.GetAttribute("OfSystemLibVersion") + " (CPU FW " + fw + ")");
                }
                else step("technology object", "exists", Convert.ToString(to.GetAttribute("OfSystemLibElement")) + " " + to.GetAttribute("OfSystemLibVersion"));
                toVer = Convert.ToString(to.GetAttribute("OfSystemLibVersion")).TrimStart('V', 'v');

                AxisHardwareConnectionProvider hw = to.GetService<AxisHardwareConnectionProvider>();
                if (!hw.ActorInterface.IsConnected)
                {
                    hw.ActorInterface.Connect(inByte * 8, outByte * 8, ConnectOption.Default);
                    step("actor connection", "connected", "bit addresses " + inByte * 8 + "/" + outByte * 8 + (hw.SensorInterface.Count > 0 && hw.SensorInterface[0].IsConnected ? ", encoder in telegram" : ""));
                }
                else step("actor connection", "exists", "bit addresses " + hw.ActorInterface.InputAddress + "/" + hw.ActorInterface.OutputAddress);

                if (topology != "none") step("topology", "ok", LineTopology(plcDev, dev));

                if (!BlockExists(plc, "S210_AbsCnt"))
                {
                    SclImport(plcName, Template("S210_AbsCnt.scl").Replace("{MC_VERSION}", toVer), "StartWise_S210_AbsCnt", false);
                    step("FB S210_AbsCnt", "created", "MC blocks " + toVer);
                }
                string inst = "S210_AbsCnt_" + drive;
                if (!BlockExists(plc, inst)) { SclImport(plcName, "DATA_BLOCK \"" + inst + "\"\n{ S7_Optimized_Access := 'TRUE' }\nVERSION : 0.1\n\"S210_AbsCnt\"\nBEGIN\nEND_DATA_BLOCK\n", "StartWise_" + inst, false); step(inst, "created", null); }

                List<Pin> pins = S210Pins.Select(p => new Pin { Name = p[0], Section = p[1], Type = p[2], Source = inst + "." + p[0] }).ToList();
                pins.Add(new Pin { Name = "Axis", Section = "InOut", Type = "TO_PositioningAxis", Source = drive });
                fcName = ServoFc; flg = FbCallFlgNet("S210_AbsCnt", inst, pins);
                title = drive + " - S210_AbsCnt (TO: " + drive + ", arayuz: " + inst + ")"; iface = inst; marker = "<Component Name=\"" + inst + "\" />"; wired = "<Component Name=\"ErrorID\" />";
            }
            else
            {
                if (!BlockExists(plc, "SINA_SPEED"))
                {
                    string lib = null;
                    foreach (GlobalLibrary g in Portal().GlobalLibraries) if (g.Name.IndexOf("Drive_Lib", StringComparison.OrdinalIgnoreCase) >= 0) { lib = g.Name; break; }
                    if (lib == null) foreach (var info in Portal().GlobalLibraries.GetGlobalLibraryInfos()) if (info.Name.IndexOf("Drive_Lib", StringComparison.OrdinalIgnoreCase) >= 0) { lib = info.Name; break; }
                    if (lib == null)
                    {
                        List<string> files = FindLibraryFiles("Drive_Lib");
                        if (files.Count > 0) { lib = Portal().GlobalLibraries.Open(new FileInfo(files[0]), OpenMode.ReadOnly).Name; step("DriveLib", "opened", files[0] + " (read-only)"); }
                    }
                    if (lib == null) throw new InvalidOperationException("SINA_SPEED is not in the PLC and no DriveLib was found (open ones, Documents\\Automation). Open it with lib_open (path of the DriveLib folder or .al21) and call again. Nothing else was changed.");
                    LibCopyBlock(plcName, lib, "SINA_SPEED", null);
                    step("SINA_SPEED", "copied", "from " + lib);
                }
                ArrayList consts = new ArrayList();
                CollectSysConsts(plc.TagTableGroup.TagTables, plc.TagTableGroup.Groups, drive + "~", consts, 200);
                string hwid = null;
                foreach (IDictionary c in consts)
                {
                    string n = Convert.ToString(c["name"]);
                    if (n.IndexOf("telegram", StringComparison.OrdinalIgnoreCase) >= 0 && (telegram == 0 || n.EndsWith("_" + telegram))) { hwid = n; break; }
                    if (hwid == null && n.IndexOf("telegram", StringComparison.OrdinalIgnoreCase) >= 0) hwid = n;
                }
                if (hwid == null) throw new InvalidOperationException("No telegram HW identifier constant '" + drive + "~...telegram...' found. Is the drive assigned to this PLC's IO system?");
                step("HW identifier", "ok", hwid);

                string inst = "SINA_SPEED_" + drive;
                if (!BlockExists(plc, inst)) { SclImport(plcName, "DATA_BLOCK \"" + inst + "\"\n{ S7_Optimized_Access := 'TRUE' }\nVERSION : 0.1\n\"SINA_SPEED\"\nBEGIN\nEND_DATA_BLOCK\n", "StartWise_" + inst, false); step(inst, "created", null); }

                // Pins show their own instance member; ConfigAxis stays open so the library default (16#003F) applies.
                List<Pin> pins = SinaPins.Select(p => new Pin { Name = p[0], Section = p[1], Type = p[2], Source = p[0] != "ConfigAxis" ? inst + "." + p[0] : null }).ToList();
                pins.Insert(5, new Pin { Name = "HWIDSTW", Section = "Input", Type = "HW_IO", Source = "const:" + hwid });
                pins.Insert(6, new Pin { Name = "HWIDZSW", Section = "Input", Type = "HW_IO", Source = "const:" + hwid });
                fcName = AsyncFc; flg = FbCallFlgNet("SINA_SPEED", inst, pins);
                title = drive + " - SINA_SPEED (HW: " + hwid + ", arayuz: " + inst + ")"; iface = inst; marker = "<Component Name=\"" + inst + "\" />"; wired = "<Component Name=\"DiagId\" />";
            }

            // FC with one network per drive
            PlcBlock fcBlock = FindBlock(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, fcName);
            if (fcBlock == null)
            {
                ImportBlockXml(plc, NewLadFcXml(fcName, "StartWise: surucu basina bir ag. Komut/durum surucunun instance DB'sinde (pinler kendi instance uyelerine bagli, senaryo kodu instance DB'ye yazar/okur).", flg, title));
                step("FC " + fcName, "created", "network for " + drive);
            }
            else
            {
                string legacyIo = "<Component Name=\"" + io + "\" />";
                string r = AppendLadNetwork(plc, fcBlock, flg, title, marker, net => net.Contains(legacyIo) || !net.Contains(wired));
                step("FC " + fcName, r == "added" ? "network added" : r == "replaced" ? "network rewritten (pins wired to " + iface + ")" : "network exists", drive);
                if (r == "replaced" && BlockExists(plc, io))
                    step(io, "unused", "Not referenced by the network any more. Delete it after checking that no scenario code uses it (compile shows the users).");
            }

            // OB1 calls the FC
            PlcBlock ob = FindBlock(plc.BlockGroup.Blocks, plc.BlockGroup.Groups, MainOb);
            if (ob == null) step("OB1", "skipped", "no block named Main");
            else
            {
                try { step("OB1", AppendLadNetwork(plc, ob, FcCallFlgNet(fcName), fcName + " (StartWise)", "<CallInfo Name=\"" + fcName + "\" BlockType=\"FC\"", null) == "added" ? "call added" : "call exists", fcName); }
                catch (Exception e) { step("OB1", "skipped", e.Message + " Call '" + fcName + "' from your cyclic OB by hand."); }
            }

            Dictionary<string, object> comp = (Dictionary<string, object>)Compile(plcName, "software");
            step("compile software", Convert.ToInt32(comp["errors"]) == 0 ? "ok" : "errors", "errors " + comp["errors"] + ", warnings " + comp["warnings"]);
            return U.Obj("drive", drive, "kind", kind, "interface_db", iface, "steps", steps, "compile", comp,
                "note", "Scenario code writes the FB inputs and reads the outputs directly in \"" + iface + "\" (e.g. \"" + iface + "\"." + (kind == "servo" ? "Enable" : "EnableAxis") + "). The network wires each FB pin to its own instance member so the values are visible on the call" + (kind == "servo" ? "" : "; ConfigAxis stays open (library default 16#003F)") + ". Project not saved; call project_save.");
        }
    }
}
