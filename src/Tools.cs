using System;
using System.Collections;
using System.Collections.Generic;

namespace TiaAssistant
{
    public delegate object ToolHandler(Dictionary<string, object> args);

    public class Tool
    {
        public string Name;
        public string Description;
        public Dictionary<string, object> Schema;
        public ToolHandler Handler;
    }

    public static class Tools
    {
        public const string Instructions =
            "StartWise (formerly TIA Assistant) drives Siemens TIA Portal " + Program.TiaName + " offline through the Openness API. " +
            "SKILLS: call skill_list once per conversation and read (skill_get) the skill that matches the job before you start it - skill 'startwise-tia' holds the ground rules; follow the skills. " +
            "FIRST: call scenario_get with name 'defaults', then scenario_list; before handling a kind of equipment read its scenario and follow it instead of asking the engineer things it already answers. " +
            "Typical flow: tia_connect -> project_create or project_open -> for each hardware row of the equipment list: " +
            "device_add (CPUs, interface modules, drives, HMIs) and module_plug (rack modules), using the MLFB from the list -> " +
            "network_connect (subnet, IP, IO system) -> drive_telegram for drives -> tags_add -> scl_import -> compile -> project_save. " +
            "Hardware is always driven by MLFB (article number); never invent one. If an MLFB is not found, use catalog_find and ask the engineer. " +
            "If firmware version is not given, the newest catalog version is used and reported back. " +
            "Nothing is ever downloaded to a PLC; all work is offline. project_save is the only tool that writes the project to disk.";

        static List<Tool> _tools;

        public static Tool Find(string name)
        {
            Ensure();
            foreach (Tool t in _tools) if (t.Name == name) return t;
            return null;
        }

        public static ArrayList List()
        {
            Ensure();
            ArrayList l = new ArrayList();
            foreach (Tool t in _tools)
                l.Add(U.Obj("name", t.Name, "description", t.Description, "inputSchema", t.Schema));
            return l;
        }

        static Dictionary<string, object> P(string type, string desc)
        {
            return U.Obj("type", type, "description", desc);
        }

        static Dictionary<string, object> Schema(string[] required, params object[] props)
        {
            return U.Obj("type", "object", "properties", U.Obj(props), "required", required);
        }

        static void Add(string name, string desc, Dictionary<string, object> schema, ToolHandler h)
        {
            Tool t = new Tool();
            t.Name = name; t.Description = desc; t.Schema = schema; t.Handler = h;
            _tools.Add(t);
        }

        // Separate, non-inlined method so the Siemens assembly is only needed when this actually runs.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static object StatusViaTia()
        {
            return Tia.Status();
        }

        // "station" is the documented name; "device" is still accepted (some MCP bridges reserve that word).
        static string Station(Dictionary<string, object> a)
        {
            string s = U.Str(a, "station", false);
            if (s == null) s = U.Str(a, "device", false);
            if (s == null) throw new ArgumentException("Missing required argument: station");
            return s;
        }

        static void Ensure()
        {
            if (_tools != null) return;
            _tools = new List<Tool>();
            string[] none = new string[0];

            Add("tia_status",
                "Read-only. Reports Openness prerequisites (API location, Windows group membership), running TIA Portal instances, and the currently connected instance/project.",
                Schema(none),
                delegate(Dictionary<string, object> a)
                {
                    // Do not touch the Tia class when the Siemens assembly cannot be loaded.
                    if (Program.ApiDir == null)
                        return U.Obj("server_version", Program.ServerVersion, "api_found", false,
                            "problem", "Openness API for " + Program.TiaName + " was not found. Set the TIA_API_DIR environment variable to the PublicAPI\\" + Program.TiaName + " folder.");
                    return StatusViaTia();
                });

            Add("scenario_list",
                "Read-only. Lists the engineer's own working rules: 'defaults' (house defaults that apply to everything) and the scenarios (step-by-step procedures per kind of equipment).",
                Schema(none),
                delegate(Dictionary<string, object> a) { return U.KnowledgeList(); });

            Add("skill_list",
                "Read-only. Lists the skills this server ships (name, description, files). A skill is a workflow with rules and steps; " +
                "read the one that fits the job with skill_get before starting it. The same skills are also served over MCP (skill:// resources) to clients that support it.",
                Schema(none),
                delegate(Dictionary<string, object> a) { return Skills.ToolList(); });

            Add("skill_get",
                "Read-only. Returns a skill's SKILL.md, or one of its supporting files ('file', a path relative to the skill, e.g. references/drives.md). Follow the instructions in it.",
                Schema(new string[] { "name" },
                    "name", P("string", "Skill name from skill_list, e.g. startwise-tia"),
                    "file", P("string", "Supporting file relative to the skill folder. Optional; default SKILL.md")),
                delegate(Dictionary<string, object> a) { return Skills.ToolGet(U.Str(a, "name", true), U.Str(a, "file", false)); });

            Add("scenario_get",
                "Read-only. Returns one document from scenario_list, e.g. 'defaults' or 'scenarios/drive-g120c'. " +
                "Read 'defaults' once per conversation and the matching scenario BEFORE adding or configuring that kind of equipment, and follow it.",
                Schema(new string[] { "name" }, "name", P("string", "Document name as returned by scenario_list")),
                delegate(Dictionary<string, object> a) { return U.KnowledgeGet(U.Str(a, "name", true)); });

            Add("api_search",
                "Read-only, works without TIA Portal. Search the Openness API of the installed TIA version (types, methods, properties with signatures " +
                "and Siemens' doc comments) and the hardware attribute manual (attribute names for GetAttribute/SetAttribute on device items, with " +
                "types, labels and enum values). Use it before writing code or a call for an unknown operation. All words must match.",
                Schema(new string[] { "query" },
                    "query", P("string", "Words to search, e.g. 'telegram', 'screen template', 'IpAddress', 'archive project'"),
                    "scope", P("string", "'all' (default), 'api' or 'hw'"),
                    "limit", P("integer", "Max results per scope (default 25)")),
                delegate(Dictionary<string, object> a) { return ApiIndex.Search(U.Str(a, "query", true), U.Str(a, "scope", false), U.Int(a, "limit") ?? 25); });

            Add("api_type",
                "Read-only, works without TIA Portal. Full description of one Openness type: kind, base type, and every member with signature and doc summary. " +
                "Name may be short (e.g. 'ScreenTemplate', 'HW.DeviceItem') or full.",
                Schema(new string[] { "name" }, "name", P("string", "Type name")),
                delegate(Dictionary<string, object> a) { return ApiIndex.TypeInfo(U.Str(a, "name", true)); });

            Add("object_inspect",
                "Read-only live explorer of the open TIA project. Shows any Openness object's attributes (with access mode and current value), child " +
                "compositions (with item counts), invocable actions and, optionally, available services. Use it to learn where a setting lives before " +
                "writing a tool or call. Path segments separated by '/': roots 'project' (default), 'station:<device>', 'item:<device item>'; " +
                "segments 'Devices', 'DeviceItems[PLC_1]', 'DeviceItems[0]', 'service:NetworkInterface'. Nothing is changed.",
                Schema(none,
                    "path", P("string", "e.g. 'station:PLC_1_Station/DeviceItems[PLC_1]' or 'item:PROFINET interface_1/service:NetworkInterface'. Default: project"),
                    "filter", P("string", "Only attributes/compositions/actions whose name contains this text. Optional"),
                    "values", P("boolean", "Read attribute values (default true)"),
                    "services", P("boolean", "Probe which services the object provides (default false; slower)")),
                delegate(Dictionary<string, object> a) { return Tia.Inspect(U.Str(a, "path", false), U.Str(a, "filter", false), U.Bool(a, "values", true), U.Bool(a, "services", false)); });

            Add("object_set",
                "Write attributes of one Openness object found with object_inspect (same path syntax). Attributes are set in passes until nothing more can be set, " +
                "so dependent attributes may be given together in any order (e.g. ClockMemoryByte + ClockMemoryByteAddress, or a channel's ParameterSettings=0 + OperatingType); " +
                "one that already has the wanted value counts as done (TIA often adjusts dependent values itself), the rest are reported with the reason. " +
                "Values are converted to the attribute's type and read back. The project is not saved.",
                Schema(new string[] { "path", "attributes" },
                    "path", P("string", "Object path as in object_inspect"),
                    "attributes", U.Obj("type", "object", "description", "Ordered {name: value}, e.g. {\"ClockMemoryByte\": true, \"ClockMemoryByteAddress\": 100}")),
                delegate(Dictionary<string, object> a)
                {
                    object v; a.TryGetValue("attributes", out v);
                    return Tia.SetAttributes(U.Str(a, "path", true), v as System.Collections.IDictionary);
                });

            Add("object_invoke",
                "Call an action of an Openness object or list found with object_inspect (its 'actions'), e.g. ConnectToSubnet, PlugNew, CreateAndConnectToSubnet. " +
                "args: {parameterName: value}; an engineering-object parameter is given as its object_inspect path, files/folders as paths. " +
                "Plain .NET methods outside the generic API also work (e.g. a user's Roles.Add); a generic method takes its type as \"_T\" " +
                "(e.g. CommunicationManagement Connections.Create with _T=HmiConnection). " +
                "Online actions are refused. Actions that delete or change existing content (Delete, Remove, Disconnect, ...) return a preview " +
                "and need confirm=true after the engineer agreed. The project is not saved.",
                Schema(new string[] { "path", "action" },
                    "path", P("string", "Object or list path as in object_inspect"),
                    "action", P("string", "Action name"),
                    "args", U.Obj("type", "object", "description", "Arguments by parameter name, e.g. {\"subnet\": \"project/Subnets[PN/IE_1]\"}"),
                    "confirm", P("boolean", "Required for destructive actions, only after the engineer explicitly agreed (default false)")),
                delegate(Dictionary<string, object> a)
                {
                    object v; a.TryGetValue("args", out v);
                    return Tia.Invoke(U.Str(a, "path", true), U.Str(a, "action", true), v as System.Collections.IDictionary, U.Bool(a, "confirm", false));
                });

            Add("object_create",
                "Create a new object in a list (composition) found with object_inspect (see its 'creatable'), then optionally set attributes in order. " +
                "E.g. a tag table in .../TagTableGroup/TagTables with parameters {\"Name\": \"Motors\"}. The project is not saved.",
                Schema(new string[] { "path" },
                    "path", P("string", "List path as in object_inspect"),
                    "type", P("string", "Type to create when the list offers several (see 'creatable'). Optional"),
                    "parameters", U.Obj("type", "object", "description", "Creation parameters by name (see 'creatable'; '?' = optional). A value can be typed: {\"type\": \"Version\", \"value\": \"7.0\"} or {\"type\": \"path\", \"value\": \"<object path>\"}. Version-like values of *Version parameters are converted automatically"),
                    "attributes", U.Obj("type", "object", "description", "Attributes to set on the new object afterwards, in order. Optional")),
                delegate(Dictionary<string, object> a)
                {
                    object p, at; a.TryGetValue("parameters", out p); a.TryGetValue("attributes", out at);
                    return Tia.Create(U.Str(a, "path", true), U.Str(a, "type", false), p as System.Collections.IDictionary, at as System.Collections.IDictionary);
                });

            Add("tia_connect",
                "Connect to TIA Portal " + Program.TiaName + ". mode 'attach' (default) attaches to an already running instance (fast; pass process_id if more than one is running). " +
                "mode 'new' starts a new instance, which can take a minute or more. On first use TIA Portal shows an Openness access prompt that the engineer must accept.",
                Schema(none,
                    "mode", U.Obj("type", "string", "enum", new string[] { "attach", "new" }, "description", "attach (default) or new"),
                    "process_id", P("integer", "PID of the TIA Portal instance to attach to (only needed when several are running)"),
                    "with_ui", P("boolean", "For mode 'new': show the TIA Portal window (default true)")),
                delegate(Dictionary<string, object> a) { return Tia.Connect(U.Str(a, "mode", false), U.Int(a, "process_id"), U.Bool(a, "with_ui", true)); });

            Add("project_create",
                "Create a new, empty TIA project. 'directory' is the parent folder; a subfolder named after the project is created inside it.",
                Schema(new string[] { "directory", "name" },
                    "directory", P("string", "Existing parent folder, e.g. C:\\TIA_Projects"),
                    "name", P("string", "Project name")),
                delegate(Dictionary<string, object> a) { return Tia.ProjectCreate(U.Str(a, "directory", true), U.Str(a, "name", true)); });

            Add("project_open",
                "Open an existing " + Program.TiaName + " project file (.ap" + Program.TiaMajor + "). Never upgrades a project from an older version.",
                Schema(new string[] { "path" }, "path", P("string", "Full path of the .ap" + Program.TiaMajor + " file")),
                delegate(Dictionary<string, object> a) { return Tia.ProjectOpen(U.Str(a, "path", true)); });

            Add("project_save",
                "Save the open project to disk.",
                Schema(none),
                delegate(Dictionary<string, object> a) { return Tia.ProjectSave(); });

            Add("project_archive",
                "Back up the open TIA project as a compressed archive (.zap" + Program.TiaMajor + ", TIA 'Archive'). The project is saved first. " +
                "Default target: a 'Yedekler' folder next to the project folder; default name <project>_<yyyyMMdd_HHmm>.",
                Schema(none,
                    "target_dir", P("string", "Folder for the archive. Optional"),
                    "name", P("string", "Archive file name. Optional")),
                delegate(Dictionary<string, object> a) { return Tia.ProjectArchive(U.Str(a, "target_dir", false), U.Str(a, "name", false)); });

            Add("project_close",
                "Close the open project. With save=true the project is saved first (one step: save and close). " +
                "With save=false (default) unsaved changes are LOST, so only do that when the engineer wants to discard them.",
                Schema(none, "save", P("boolean", "Save before closing (default false)")),
                delegate(Dictionary<string, object> a) { return Tia.ProjectClose(U.Bool(a, "save", false)); });

            Add("project_tree",
                "Read-only inventory of the open project: devices, racks/modules with slot numbers and type identifiers, the input/output address ranges of every module (io_addresses), network settings, and per PLC the tag tables, blocks and data types.",
                Schema(none, "depth", P("integer", "How deep to list nested device items (default 3)")),
                delegate(Dictionary<string, object> a) { return Tia.ProjectTree(U.Int(a, "depth") ?? 3); });

            Add("project_summary",
                "Read-only. Compact inventory for large projects: one line per station (head module, type, subnet/address, IO system) plus per PLC the block counts, tag tables and number of technology objects, and per HMI its name. " +
                "Use this first on existing projects; use project_tree only for a small project or after narrowing down.",
                Schema(none, "filter", P("string", "Only stations whose name, head module or type contains this text. Optional")),
                delegate(Dictionary<string, object> a) { return Tia.ProjectSummary(U.Str(a, "filter", false)); });

            Add("to_list",
                "Read-only. Lists the technology objects (motion axes, PID, counters, ...) of a PLC with type and version, and for axes the hardware connection: which drive telegram / module the actor and encoder are connected to.",
                Schema(new string[] { "station" }, "station", P("string", "Station name or CPU name of the PLC")),
                delegate(Dictionary<string, object> a) { return Tia.ToList(Station(a)); });

            Add("to_get",
                "Read-only. Parameters of one technology object, e.g. filter 'Actor', 'Sensor', 'Mechanics', 'DynamicLimits', 'Homing'.",
                Schema(new string[] { "station", "name" },
                    "station", P("string", "Station name or CPU name of the PLC"),
                    "name", P("string", "Technology object name"),
                    "filter", P("string", "Only parameters whose name contains this text. Optional"),
                    "limit", P("integer", "Maximum parameters to return (default 80)")),
                delegate(Dictionary<string, object> a) { return Tia.ToGet(Station(a), U.Str(a, "name", true), U.Str(a, "filter", false), U.Int(a, "limit") ?? 80); });

            Add("hmi_tree",
                "Read-only. Inventory of an HMI device (Comfort/Basic panel or WinCC Professional station): connections, HMI tag tables with tag counts, screens, screen templates, text and graphic lists, cycles.",
                Schema(new string[] { "station" }, "station", P("string", "Station name of the HMI device")),
                delegate(Dictionary<string, object> a) { return Tia.HmiTree(Station(a)); });

            Add("hmi_export",
                "Read-only. Returns one HMI object as SimaticML XML. kind: tag_table, connection, screen, screen_template, text_list or cycle. " +
                "An HMI tag table export shows how each HMI tag is bound to its connection and PLC tag.",
                Schema(new string[] { "station", "kind", "name" },
                    "station", P("string", "Station name of the HMI device"),
                    "kind", U.Obj("type", "string", "enum", new string[] { "tag_table", "connection", "screen", "screen_template", "text_list", "cycle" }, "description", "Object kind"),
                    "name", P("string", "Object name as listed by hmi_tree")),
                delegate(Dictionary<string, object> a) { return Tia.HmiExport(Station(a), U.Str(a, "kind", true), U.Str(a, "name", true)); });

#if !TIA_V18
            Add("hmi_connect",
                "TIA V21+: create the integrated HMI connection between an HMI panel (classic Basic/Comfort or WinCC Unified) and a PLC, " +
                "through the hardware connection API (in V18 this had to be dragged by hand). Both must already be on a common Ethernet subnet (network_connect). " +
                "Idempotent: an existing connection to the same PLC is reported. Default name from TIA (HMI_Connection_1, ...). " +
                "Classic panels: binding IO fields / HMI tags to PLC tags is then done in the TIA editor (V21 Openness refuses the symbolic PLC-tag link on classic HMI tags).",
                Schema(new string[] { "hmi", "plc" },
                    "hmi", P("string", "HMI station or device name, e.g. HMI_1"),
                    "plc", P("string", "PLC station or CPU name, e.g. PLC_1"),
                    "name", P("string", "Connection name. Optional")),
                delegate(Dictionary<string, object> a) { return Tia.HmiConnect(U.Str(a, "hmi", true), U.Str(a, "plc", true), U.Str(a, "name", false)); });
#endif

#if !TIA_V18 && !NO_UNIFIED
            Add("hmi_unified_screen",
                "WinCC Unified panels (TIA V21+): show PLC values on a screen. For each item an HMI tag is created (or reused) and bound symbolically to the PLC tag / DB member " +
                "(e.g. SINA_SPEED_Drive_1.ActVelocity, V3300.Opened, a raw telegram word), optionally with linear scaling; then a row 'label | output IO field | unit' is added " +
                "to the screen (created if missing, rows appended, existing IO fields skipped). Needs an HMI connection (hmi_connect). Max 12 items per call. " +
                "For decimals on a scaled integer give decimals=n (HMI range x10^n, IO field shifts n places).",
                Schema(new string[] { "station", "screen", "items" },
                    "station", P("string", "Unified HMI station/device name, e.g. HMI_2"),
                    "screen", P("string", "Screen name, e.g. Drive_1"),
                    "title", P("string", "Title text at the top. Optional"),
                    "items", U.Obj("type", "array", "description", "Rows: {label, plc, unit?, hmi_tag?, decimals?, scale?: {plc_min, plc_max, hmi_min, hmi_max}}",
                        "items", U.Obj("type", "object")),
                    "connection", P("string", "HMI connection name. Optional (first connection)"),
                    "table", P("string", "HMI tag table. Optional (Default tag table)"),
                    "dry_run", P("boolean", "Only show the plan")),
                delegate(Dictionary<string, object> a) {
                    object it; a.TryGetValue("items", out it);
                    return Tia.HmiUnifiedScreen(U.Str(a, "station", true), U.Str(a, "screen", true), U.Str(a, "title", false), it as System.Collections.IEnumerable,
                        U.Str(a, "connection", false), U.Str(a, "table", false), U.Bool(a, "dry_run", false)); });

            Add("hmi_unified_add",
                "WinCC Unified panels (TIA V21+): add operator elements to a screen in one call (screen created if missing). Each element binds to a PLC value " +
                "('plc': PLC tag / DB member like SINA_SPEED_Drive_1.EnableAxis or V3300.ManOpen; HMI tag created or reused) or to an existing HMI 'tag'. Types: " +
                "button {text, action: momentary (default: true while pressed) | set | reset | toggle | value, value?} - writes via a JavaScript event; " +
                "indicator {label, bit? (for Word/Int status bits), on_color?, off_color?} - square coloured by the value (default green/grey; names or #RRGGBB); " +
                "input / output {label, unit?, decimals?, scale?} - IO field; trend {series: [{plc|tag, color?, label?}], w?, h?} - live trend control; " +
                "alarm_view {w?, h?} - alarm control; alarm {text, bit?, class? (Alarm, Warning ...)} - HMI discrete alarm (no screen item). " +
                "Elements without x/y are laid out in rows below the existing items. Max 30 per call; a failing element is reported, the rest continue.",
                Schema(new string[] { "station", "elements" },
                    "station", P("string", "Unified HMI device name, e.g. HMI_2"),
                    "screen", P("string", "Screen name (not needed when only alarms are added)"),
                    "elements", U.Obj("type", "array", "description", "[{type, plc|tag, ...}] see description; optional name, x, y, w, h", "items", U.Obj("type", "object")),
                    "connection", P("string", "HMI connection name. Optional (first connection)"),
                    "table", P("string", "HMI tag table. Optional (Default tag table)")),
                delegate(Dictionary<string, object> a) {
                    object el; a.TryGetValue("elements", out el);
                    return Tia.HmiUnifiedAdd(U.Str(a, "station", true), U.Str(a, "screen", false), el as System.Collections.IEnumerable,
                        U.Str(a, "connection", false), U.Str(a, "table", false)); });
#endif

            Add("hmi_tags_from_plc",
                "Show PLC values on an HMI panel, like binding an IO field directly to a PLC tag in the TIA editor: creates the HMI tags " +
                "(name DB_Member, integrated connection HMI_Connection_1) in a new HMI tag table and, with 'screen', a new screen of output IO fields " +
                "(label + value, max 10). plc_refs: PLC tag names or DB members, 'DB_Name.*' = all top-level members. HMI tags that already point to the same " +
                "PLC value are reused. Only Bool, Int and Real are supported (verified XML); other types are skipped and reported. On the screen numbers are output IO fields and Bools are circle indicators (grey 0 / green 1); " +
                "'nav' adds buttons that open other existing screens. " +
                "Use dry_run=true to see the plan first. The project is saved before each import.",
                Schema(new string[] { "station", "plc_refs" },
                    "station", P("string", "Station name of the HMI device"),
                    "plc_refs", U.Obj("type", "array", "description", "e.g. [\"DB_Drive_1_Durum.*\"] or [\"DB_Motor.Akim\", \"Motor1_Start\"]", "items", U.Obj("type", "string")),
                    "table", P("string", "New HMI tag table name (default <DB>_HMI)"),
                    "screen", P("string", "Also create a new screen with this name. Optional"),
                    "title", P("string", "Screen title text (default: screen name)"),
                    "connection", P("string", "HMI connection (default HMI_Connection_1)"),
                    "cycle", P("string", "Acquisition cycle (default 1 s)"),
                    "template", P("string", "Screen template to assign (e.g. Template_1). Optional"),
                    "nav", U.Obj("type", "array", "description", "Existing screen names to add navigation buttons for (bottom right). Optional", "items", U.Obj("type", "string")),
                    "dry_run", P("boolean", "Only report what would be created (default false)")),
                delegate(Dictionary<string, object> a)
                {
                    object r; a.TryGetValue("plc_refs", out r);
                    object nv; a.TryGetValue("nav", out nv);
                    return Tia.HmiTagsFromPlc(Station(a), r, U.Str(a, "table", false), U.Str(a, "connection", false), U.Str(a, "cycle", false),
                        U.Str(a, "screen", false), U.Str(a, "title", false), nv, U.Str(a, "template", false), U.Bool(a, "dry_run", false));
                });

            Add("plc_alarm_texts",
                "PLC alarm texts (Program_Alarm instances etc.). Without 'texts' it is read-only and returns the instance alarm text table " +
                "(the same as TIA's 'export instance texts to xlsx'). With 'texts' it sets the alarm text of each listed alarm and imports the table back. " +
                "'alarm' is matched against the cells of a row (e.g. the instance name like DB_Drive_1_Alarm.Alarm_Hata). Compile the PLC first.",
                Schema(new string[] { "station" },
                    "station", P("string", "Station name or CPU name of the PLC"),
                    "texts", U.Obj("type", "array", "description", "e.g. [{\"alarm\":\"DB_Drive_1_Alarm.Alarm_Hata\",\"text\":\"Drive_1 surucu hatasi\"}]",
                        "items", U.Obj("type", "object", "properties", U.Obj("alarm", P("string", "Alarm / instance name"), "text", P("string", "Alarm text")), "required", new string[] { "alarm", "text" })),
                    "column", P("string", "Exact header of the text column to write (default: first header containing 'Alarm text')")),
                delegate(Dictionary<string, object> a)
                {
                    object t; a.TryGetValue("texts", out t);
                    return Tia.PlcAlarmTexts(Station(a), t, U.Str(a, "column", false));
                });

            Add("block_source",
                "Read-only. Returns an existing PLC block as SCL source text (TIA 'Generate source from blocks'), ready to edit and send back with scl_import overwrite=true. " +
                "Works for SCL blocks and data blocks; LAD/FBD blocks cannot be turned into source.",
                Schema(new string[] { "station", "block" },
                    "station", P("string", "Station name or CPU name of the PLC"),
                    "block", P("string", "Block name")),
                delegate(Dictionary<string, object> a) { return Tia.BlockSource(Station(a), U.Str(a, "block", true)); });

            Add("hmi_screen_to_library",
                "Save an existing HMI screen as a master copy in the project library (folder TIA_Assistant). " +
                "Use it for screens with objects that XML export drops on classic panels, e.g. the Alarm view; then create new screens from it with hmi_screen_from_library.",
                Schema(new string[] { "station", "screen" },
                    "station", P("string", "Station name of the HMI device"),
                    "screen", P("string", "Screen name"),
                    "item", P("string", "Master copy name (default: screen name)")),
                delegate(Dictionary<string, object> a) { return Tia.HmiScreenToLibrary(Station(a), U.Str(a, "screen", true), U.Str(a, "item", false)); });

            Add("hmi_screen_from_library",
                "Create a new HMI screen from a library master copy (TIA copies it internally, so Alarm views and other objects are kept). " +
                "The new screen gets the next free screen number and, if possible, the given name. Use a master copy made from the same panel family.",
                Schema(new string[] { "station", "item" },
                    "station", P("string", "Station name of the HMI device"),
                    "item", P("string", "Master copy name (see lib_list)"),
                    "library", P("string", "'project' (default) or a global library name"),
                    "name", P("string", "Name for the new screen. Optional")),
                delegate(Dictionary<string, object> a) { return Tia.HmiScreenFromLibrary(Station(a), U.Str(a, "library", false), U.Str(a, "item", true), U.Str(a, "name", false)); });

            Add("hmi_faceplate_screen",
                "Create a new HMI screen with instances of a faceplate type from the project library (e.g. one MotorFP per motor) and bind each " +
                "instance's interface properties. Interface values are PLC refs ('DB.Member' or PLC tag); the HMI tags are created/reused like hmi_tags_from_plc. " +
                "The newest released version of the type is used and interface names are checked against the type before import. " +
                "The faceplate type itself must be designed in TIA. Use dry_run=true first.",
                Schema(new string[] { "station", "screen", "faceplate", "instances" },
                    "station", P("string", "Station name of the HMI device"),
                    "screen", P("string", "New screen name"),
                    "title", P("string", "Screen title (default: screen name)"),
                    "faceplate", P("string", "Faceplate type name in the project library, e.g. MotorFP"),
                    "instances", U.Obj("type", "array", "description", "e.g. [{\"name\":\"Drive_1\",\"interface\":{\"Process_value\":\"DB_Drive_1_Durum.Akim_A\"}}]. Optional left/top/width/height (default grid 2 columns, 380x180)",
                        "items", U.Obj("type", "object")),
                    "connection", P("string", "HMI connection (default HMI_Connection_1)"),
                    "template", P("string", "Screen template to assign (e.g. Template_1). Optional"),
                    "dry_run", P("boolean", "Only report what would be created (default false)")),
                delegate(Dictionary<string, object> a)
                {
                    object i; a.TryGetValue("instances", out i);
                    return Tia.HmiFaceplateScreen(Station(a), U.Str(a, "screen", true), U.Str(a, "title", false), U.Str(a, "faceplate", true), i,
                        U.Str(a, "connection", false), U.Str(a, "template", false), U.Bool(a, "dry_run", false));
                });

            Add("hmi_import",
                "Create or replace an HMI object from SimaticML XML (the only way to create classic HMI tag tables, connections and screens). " +
                "Base the XML on an hmi_export from the same TIA version and device family. overwrite=true replaces an existing object of the same name. " +
                "For tag tables every ControllerTag is checked against the PLCs first and the import is refused if one is missing. " +
                "HMI tags must use the integrated HMI connection (TIA name HMI_Connection_1); it does not appear in hmi_tree connections.",
                Schema(new string[] { "station", "kind", "xml" },
                    "station", P("string", "Station name of the HMI device"),
                    "kind", U.Obj("type", "string", "enum", new string[] { "tag_table", "connection", "screen", "screen_template", "text_list", "cycle" }, "description", "Object kind"),
                    "xml", P("string", "Complete SimaticML document"),
                    "overwrite", P("boolean", "Replace an existing object with the same name (default false)"),
                    "save_first", P("boolean", "Save the project before importing (default true). A failed HMI import can crash TIA Portal and lose unsaved work")),
                delegate(Dictionary<string, object> a) { return Tia.HmiImport(Station(a), U.Str(a, "kind", true), U.Str(a, "xml", true), U.Bool(a, "overwrite", false), U.Bool(a, "save_first", true)); });

            Add("catalog_find",
                "Read-only search of the installed hardware catalog. Query by MLFB/article number (e.g. '6ES7 516-3AN02-0AB0') or by text. Returns article number, firmware version and the type identifier needed to create the hardware.",
                Schema(new string[] { "query" },
                    "query", P("string", "MLFB or search text"),
                    "limit", P("integer", "Maximum entries to return (default 40)")),
                delegate(Dictionary<string, object> a) { return Tia.CatalogFind(U.Str(a, "query", true), U.Int(a, "limit") ?? 40); });

            Add("device_add",
                "Add a station-level device to the project by MLFB: a PLC CPU, a distributed IO interface module (ET 200), a drive or an HMI panel. " +
                "No dialogs are shown; TIA defaults are applied. If 'version' is omitted the newest firmware in the catalog is used. PLCs are added without password protection by default.",
                Schema(new string[] { "name" },
                    "mlfb", P("string", "Article number from the equipment list, e.g. 6ES7516-3AN02-0AB0"),
                    "version", P("string", "Firmware version, e.g. V2.9. Optional; newest if omitted"),
                    "type_identifier", P("string", "Exact type identifier from catalog_find. Use instead of mlfb/version when given"),
                    "name", P("string", "Name of the device item, e.g. PLC_1"),
                    "station_name", P("string", "Name of the station. Optional; defaults to <name>_Station"),
                    "protection", U.Obj("type", "string", "enum", new string[] { "none", "keep" },
                        "description", "PLCs only. 'none' (default): add the PLC without passwords (no confidential-data protection, full access) so it compiles immediately; the engineer sets protection later. 'keep': leave TIA's initial protection state untouched")),
                delegate(Dictionary<string, object> a)
                {
                    return Tia.DeviceAdd(U.Str(a, "mlfb", false), U.Str(a, "version", false), U.Str(a, "type_identifier", false),
                        U.Str(a, "name", true), U.Str(a, "station_name", false), U.Str(a, "protection", false) ?? "none");
                });

            Add("module_plug",
                "Plug a module (DI/DO/AI/AO, communication, power supply, ...) into the rack of an existing device by MLFB. " +
                "Also used for sub-modules of a head module, e.g. the power module (PM) of a SINAMICS G120 control unit. " +
                "If 'slot' is omitted the first position that accepts the module is used. If 'version' is omitted the newest firmware is used.",
                Schema(new string[] { "station", "name" },
                    "station", P("string", "Station name or device item name of the target (e.g. PLC_1)"),
                    "mlfb", P("string", "Article number of the module"),
                    "version", P("string", "Firmware version. Optional"),
                    "type_identifier", P("string", "Exact type identifier from catalog_find. Use instead of mlfb/version when given"),
                    "name", P("string", "Name for the module, e.g. DI_1"),
                    "slot", P("integer", "Slot/position number. Optional"),
                    "rack", P("integer", "Index of the top-level item to plug into (rack or head module; see project_tree). Optional: all are tried in order"),
                    "into", P("string", "Name of the item to plug into, e.g. 'DRIVE_1' for the telegram submodule of a GSD drive. Optional: without it the rack, the head modules and then nested items are tried in order")),
                delegate(Dictionary<string, object> a)
                {
                    return Tia.ModulePlug(Station(a), U.Str(a, "mlfb", false), U.Str(a, "version", false),
                        U.Str(a, "type_identifier", false), U.Str(a, "name", true), U.Int(a, "slot"), U.Int(a, "rack"), U.Str(a, "into", false));
                });

            Add("network_connect",
                "Connect a station to a subnet and optionally assign it to a controller. bus 'profinet' (default): Ethernet interface, IP address, PROFINET IO system. " +
                "bus 'profibus': PROFIBUS interface (a combined MPI/DP interface is switched to PROFIBUS), PROFIBUS address, DP master system. " +
                "Defaults: subnet 'PN/IE_1' or 'PROFIBUS_1' (created if missing, or the controller's subnet), first matching interface, address chosen by TIA. " +
                "When 'io_controller' is given, the IO system / DP master system is created on that PLC if it does not exist yet.",
                Schema(new string[] { "station" },
                    "station", P("string", "Station name or device item name to connect"),
                    "bus", U.Obj("type", "string", "enum", new string[] { "profinet", "profibus" }, "description", "profinet (default) or profibus"),
                    "subnet", P("string", "Subnet name. Optional"),
                    "ip", P("string", "IP address (profinet) or PROFIBUS address (profibus). Optional; TIA assigns a free one if omitted"),
                    "subnet_mask", P("string", "Subnet mask (profinet only). Optional"),
                    "interface", P("integer", "Index of the interface of that bus on the station (default 0; see project_tree 'network')"),
                    "io_controller", P("string", "Station/CPU name of the PLC that controls this station (PROFINET IO controller or DP master). Optional"),
                    "io_controller_interface", P("integer", "Index of the interface of that bus on the controller (default 0)"),
                    "pn_device_name", P("string", "PROFINET device name. Optional; TIA derives it from the station name if omitted")),
                delegate(Dictionary<string, object> a)
                {
                    return Tia.NetworkConnect(Station(a), U.Str(a, "subnet", false), U.Str(a, "ip", false), U.Str(a, "subnet_mask", false),
                        U.Int(a, "interface") ?? 0, U.Str(a, "io_controller", false), U.Int(a, "io_controller_interface") ?? 0,
                        U.Str(a, "pn_device_name", false), U.Str(a, "bus", false));
                });

            Add("io_address_set",
                "Set the start byte of a module's input or output address range, so the TIA addresses match the electrical drawing. " +
                "Read the current ranges with project_tree 'io_addresses' first. Fails if the new range overlaps another module.",
                Schema(new string[] { "station", "item", "start_byte" },
                    "station", P("string", "Station name"),
                    "item", P("string", "Module name, e.g. DI1 or AI1"),
                    "io_type", U.Obj("type", "string", "enum", new string[] { "input", "output" }, "description", "Needed only when the module has both inputs and outputs"),
                    "start_byte", P("integer", "New start byte, e.g. 256 for %IW256")),
                delegate(Dictionary<string, object> a)
                {
                    int? sb = U.Int(a, "start_byte");
                    if (!sb.HasValue) throw new ArgumentException("Missing required argument: start_byte");
                    return Tia.IoAddressSet(Station(a), U.Str(a, "item", true), U.Str(a, "io_type", false), sb.Value);
                });

            Add("drive_telegram",
                "SINAMICS drives (Startdrive): list the PROFIdrive telegrams of a drive and optionally change the main telegram (e.g. 1, 20, 352, 105, 111). " +
                "The telegram's IO addresses can be moved with input_start_byte / output_start_byte. Without any of these it is read-only.",
                Schema(new string[] { "station" },
                    "station", P("string", "Station name of the drive"),
                    "telegram", P("integer", "New main telegram number. Optional"),
                    "input_start_byte", P("integer", "Move the telegram's input (status) address range to this start byte. Optional"),
                    "output_start_byte", P("integer", "Move the telegram's output (control) address range to this start byte. Optional"),
                    "drive", P("string", "Multi-axis drives (S120): drive object by name - axis rack or module name, e.g. 'Drive axis_2', 'Infeed_1'. Wins over drive_object. Optional"),
                    "drive_object", P("integer", "Index of the drive object for multi-axis devices (default 0)")),
                delegate(Dictionary<string, object> a)
                {
                    return Tia.DriveTelegram(Station(a), U.Int(a, "telegram"), DriveObjectArg(a),
                        U.Int(a, "input_start_byte"), U.Int(a, "output_start_byte"));
                });

            Add("drive_prepare",
                "Make a drive that is already in the hardware ready for scenario writing, in one call (idempotent, safe to repeat). " +
                "Servo (TO telegram e.g. 105): TO_PositioningAxis (version from CPU firmware), actor connection to the telegram, line port topology " +
                "(PLC last port -> ... -> drive port 1), FB S210_AbsCnt + instance S210_AbsCnt_<drive>, one network in LAD FC 'Servo Motors', OB1 'Main' calls the FC. " +
                "Async (telegram 1/20/352): SINA_SPEED (from the opened DriveLib global library) + instance SINA_SPEED_<drive>, HW identifier constant, " +
                "one network in LAD FC 'Asenkron Motors', OB1 call. Ends with a software compile; does NOT save. " +
                "The instance DB is the drive's interface: scenario code writes inputs / reads outputs there; in the network each FB input and output is wired to its own instance member so the values are visible " +
                "(SINA_SPEED ConfigAxis stays open = library default 16#003F; HWIDSTW/HWIDZSW = HW identifier constant; servo Axis = TO) " +
                "(e.g. \"S210_AbsCnt_Servo_2\".Enable, \"SINA_SPEED_Drive_6\".ActVelocity). A network from an old layout (open pins or wired to a <drive>_IO DB) is rewritten in place. " +
                "Run dry_run=true first to show the plan.",
                Schema(new string[] { "plc", "drive" },
                    "plc", P("string", "PLC station name, e.g. PLC_1"),
                    "drive", P("string", "Drive station/device name, e.g. Servo_2 or Drive_6"),
                    "kind", P("string", "auto (default, from main telegram) | servo | async"),
                    "topology", P("string", "line (default) | none - servo only"),
                    "dry_run", P("boolean", "Only detect and show the plan")),
                delegate(Dictionary<string, object> a) { return Tia.DrivePrepare(U.Str(a, "plc", true), U.Str(a, "drive", true),
                    U.Str(a, "kind", false), U.Str(a, "topology", false) ?? "line", U.Bool(a, "dry_run", false)); });

            Add("equipment_types",
                "Read-only. Lists the field equipment types StartWise can prepare (knowledge/equipment/<type>/equipment.json): FB, FC, field signals (io keys) and usage.",
                Schema(none),
                delegate(Dictionary<string, object> a) { return Tia.EquipmentTypesList(); });

            Add("equipment_prepare",
                "Add one field equipment item that has no hardware of its own (e.g. a valve V3300) ready for scenario writing, in one call, like drive_prepare (idempotent). " +
                "Creates the type's FB from its template (once), an instance DB named after the equipment (= its interface: scenario code writes inputs / reads outputs there), " +
                "PLC tags for the field signals given in 'io' (table from the type, e.g. 'Valves'), one network in the type's LAD FC (e.g. 'Valve Blocks') with only the field signals wired, " +
                "and the OB1 call of that FC; ends with a software compile, does NOT save. Field signals without an address stay open (wire them later). " +
                "See equipment_types for the types and io keys. Run dry_run=true first to show the plan.",
                Schema(new string[] { "plc", "type", "name" },
                    "plc", P("string", "PLC station name, e.g. PLC_1"),
                    "type", P("string", "Equipment type, e.g. valve"),
                    "name", P("string", "Equipment code, e.g. V3300 (becomes the instance DB name)"),
                    "io", U.Obj("type", "object", "description", "Field signals by io key, value = address (e.g. {\"out\": \"%Q4.0\", \"fb_open\": \"%I8.0\", \"fb_closed\": \"%I8.1\"}) or an existing PLC tag name. Optional"),
                    "params", U.Obj("type", "object", "description", "Start values of the instance DB by FB input name, set when the DB is created (e.g. {\"ScaleHigh\": 150, \"LimitH\": 120, \"Hyst\": 2, \"MonTime\": \"T#8S\"}). Optional"),
                    "dry_run", P("boolean", "Only show the plan")),
                delegate(Dictionary<string, object> a) {
                    object io, prms; a.TryGetValue("io", out io); a.TryGetValue("params", out prms);
                    return Tia.EquipmentPrepare(U.Str(a, "plc", true), U.Str(a, "type", true), U.Str(a, "name", true), io as System.Collections.IDictionary,
                        prms as System.Collections.IDictionary, U.Bool(a, "dry_run", false)); });

            Add("gsd_drive_telegram",
                "Drives that exist in the project only as GSD devices (e.g. SINAMICS G120X when Startdrive does not know the article number): " +
                "list the telegram submodule of several drives at once and optionally plug a standard telegram (e.g. 1, 20, 350, 352) into each. " +
                "Without 'telegram' it is read-only. A different existing telegram is replaced.",
                Schema(new string[] { "stations" },
                    "stations", U.Obj("type", "array", "description", "Station names of the GSD drives", "items", U.Obj("type", "string")),
                    "telegram", P("integer", "Standard telegram number to plug. Optional")),
                delegate(Dictionary<string, object> a)
                {
                    object st; a.TryGetValue("stations", out st);
                    return Tia.GsdDriveTelegram(st, U.Int(a, "telegram"));
                });

            Add("drive_parameters",
                "SINAMICS drives (Startdrive, offline): read and/or write drive parameters by number, e.g. motor nameplate data " +
                "p0304 (rated voltage), p0305 (rated current), p0307 (rated power), p0308 (cos phi), p0310 (rated frequency), p0311 (rated speed). " +
                "Values are written to the offline project only; nothing is sent to a drive. Each written parameter is read back and reported.",
                Schema(new string[] { "station" },
                    "station", P("string", "Station name of the drive"),
                    "set", U.Obj("type", "array", "description", "Parameters to write",
                        "items", U.Obj("type", "object",
                            "properties", U.Obj(
                                "parameter", P("string", "Parameter, e.g. p0304 or p0304[0]"),
                                "value", U.Obj("type", "number", "description", "New value in the parameter's unit")),
                            "required", new string[] { "parameter", "value" })),
                    "read", U.Obj("type", "array", "description", "Parameters to read, e.g. [\"p0304\", \"p0305\"]", "items", U.Obj("type", "string")),
                    "drive", P("string", "Multi-axis drives (S120): drive object by name - axis rack or module name, e.g. 'Drive axis_2', 'Infeed_1'. Wins over drive_object. Optional"),
                    "drive_object", P("integer", "Index of the drive object for multi-axis devices (default 0)")),
                delegate(Dictionary<string, object> a)
                {
                    object rd; a.TryGetValue("read", out rd);
                    object st; a.TryGetValue("set", out st);
                    if (rd == null && st == null) throw new ArgumentException("Give 'read' and/or 'set'.");
                    return Tia.DriveParameters(Station(a), rd, st, DriveObjectArg(a));
                });

            Add("drive_motor",
                "SINAMICS drives (Startdrive, offline): enter the motor nameplate data the way the commissioning wizard does. " +
                "Without 'data' it is read-only and lists the entries this drive expects (required and optional). " +
                "With 'data' it sets the motor type (default InductionMotor), writes the values and applies the motor configuration. " +
                "Typical nameplate entries: p0304 voltage [V], p0305 current [A], p0307 power [kW], p0308 cos phi, p0310 frequency [Hz], p0311 speed [rpm]. " +
                "Use the voltage/current pair that matches the actual connection (star or delta).",
                Schema(new string[] { "station" },
                    "station", P("string", "Station name of the drive"),
                    "data", U.Obj("type", "array", "description", "Nameplate values to write",
                        "items", U.Obj("type", "object",
                            "properties", U.Obj(
                                "parameter", P("string", "Parameter, e.g. p0304"),
                                "value", U.Obj("type", "number", "description", "Value in the parameter's unit")),
                            "required", new string[] { "parameter", "value" })),
                    "motor_type", P("string", "Motor type, e.g. InductionMotor (default when writing) or SynchronousMotor"),
                    "drive_data_set", P("integer", "Drive/motor data set number (default 0)"),
                    "drive", P("string", "Multi-axis drives (S120): drive object by name - axis rack or module name, e.g. 'Drive axis_2', 'Infeed_1'. Wins over drive_object. Optional"),
                    "drive_object", P("integer", "Index of the drive object for multi-axis devices (default 0)")),
                delegate(Dictionary<string, object> a)
                {
                    object d; a.TryGetValue("data", out d);
                    return Tia.DriveMotor(Station(a), U.Str(a, "motor_type", false), d, U.Int(a, "drive_data_set") ?? 0, DriveObjectArg(a));
                });

            Add("drive_parameter_list",
                "Read-only. Lists the drive parameters that are accessible through Openness in a number range (e.g. 300-399), with current values.",
                Schema(new string[] { "station", "from", "to" },
                    "station", P("string", "Station name of the drive"),
                    "from", P("integer", "First parameter number"),
                    "to", P("integer", "Last parameter number"),
                    "drive", P("string", "Multi-axis drives (S120): drive object by name - axis rack or module name, e.g. 'Drive axis_2', 'Infeed_1'. Wins over drive_object. Optional"),
                    "drive_object", P("integer", "Index of the drive object (default 0)")),
                delegate(Dictionary<string, object> a)
                {
                    return Tia.DriveParameterList(Station(a), U.Int(a, "from") ?? 0, U.Int(a, "to") ?? 65535, DriveObjectArg(a));
                });

            Add("plc_security",
                "Read or change the PLC protection settings that the TIA security wizard normally asks for. action 'status' (default) is read-only. " +
                "action 'no_protection' removes protection of confidential PLC configuration data and sets full access without password - " +
                "only use it when the engineer has explicitly agreed, because it lowers the security of the PLC configuration.",
                Schema(new string[] { "station" },
                    "station", P("string", "Station name or CPU name"),
                    "action", U.Obj("type", "string", "enum", new string[] { "status", "no_protection" }, "description", "status (default) or no_protection")),
                delegate(Dictionary<string, object> a) { return Tia.PlcSecurity(Station(a), U.Str(a, "action", false) ?? "status"); });

            Add("tags_add",
                "Create PLC tags in a tag table (the table is created if it does not exist), each with an optional comment. " +
                "Existing tag names are never overwritten: if the tag already exists at the same address only a given comment is applied; otherwise it is reported and skipped. " +
                "Take real addresses from project_tree 'io_addresses', do not assume them.",
                Schema(new string[] { "station", "table", "tags" },
                    "station", P("string", "Station name or CPU name of the PLC"),
                    "table", P("string", "Tag table name"),
                    "tags", U.Obj("type", "array", "description", "Tags to create",
                        "items", U.Obj("type", "object",
                            "properties", U.Obj(
                                "name", P("string", "Tag name"),
                                "data_type", P("string", "PLC data type, e.g. Bool, Int, Real, Word"),
                                "address", P("string", "Logical address, e.g. %I0.0, %Q4.1, %IW64, %M10.0"),
                                "comment", P("string", "Tag comment, e.g. device tag, terminal and drawing page: '-B1 NC | XDI:3 | s.4'. Optional")),
                            "required", new string[] { "name", "data_type", "address" }))),
                delegate(Dictionary<string, object> a) { return Tia.TagsAdd(Station(a), U.Str(a, "table", true), a["tags"]); });

            Add("scl_import",
                "Generate PLC blocks/data types from SCL source text (FUNCTION_BLOCK, FUNCTION, DATA_BLOCK, ORGANIZATION_BLOCK, TYPE). " +
                "Refuses to run if a block or type with the same name already exists unless overwrite=true. If generation fails nothing is kept and the compiler messages are returned.",
                Schema(new string[] { "station", "source" },
                    "station", P("string", "Station name or CPU name of the PLC"),
                    "source", P("string", "Complete SCL source text"),
                    "source_name", P("string", "Name for the external source (default AssistantSource)"),
                    "overwrite", P("boolean", "Allow replacing existing blocks/types with the same names (default false)")),
                delegate(Dictionary<string, object> a)
                {
                    return Tia.SclImport(Station(a), U.Str(a, "source", true),
                        U.Str(a, "source_name", false) ?? "AssistantSource", U.Bool(a, "overwrite", false));
                });

            Add("block_export",
                "Read-only. Returns a PLC block (OB/FB/FC/DB, any language incl. LAD) as SimaticML XML. Use it to study an existing block or as a template before block_import. The block must be compiled.",
                Schema(new string[] { "station", "block" },
                    "station", P("string", "Station name or CPU name of the PLC"),
                    "block", P("string", "Block name, e.g. Main")),
                delegate(Dictionary<string, object> a) { return Tia.BlockExport(Station(a), U.Str(a, "block", true)); });

            Add("block_import",
                "Create or replace a PLC block from SimaticML XML (the way to write LAD/FBD). Refuses to replace an existing block unless overwrite=true. " +
                "Base the XML on a block_export of the same TIA version and CPU family; run compile afterwards.",
                Schema(new string[] { "station", "xml" },
                    "station", P("string", "Station name or CPU name of the PLC"),
                    "xml", P("string", "Complete SimaticML document"),
                    "overwrite", P("boolean", "Allow replacing an existing block with the same name (default false)")),
                delegate(Dictionary<string, object> a) { return Tia.BlockImport(Station(a), U.Str(a, "xml", true), U.Bool(a, "overwrite", false)); });

            Add("lib_list",
                "Read-only. Without 'library': lists the global libraries TIA knows (system, corporate, user) and whether they are open. " +
                "With 'library' (a name from that list, or 'project'): lists its types and master copies, optionally filtered by name. A closed global library is opened read-only.",
                Schema(none,
                    "library", P("string", "Library name, or 'project' for the project library. Optional"),
                    "filter", P("string", "Only items whose name contains this text, e.g. SINA. Optional"),
                    "limit", P("integer", "Maximum items (default 60)")),
                delegate(Dictionary<string, object> a) { return Tia.LibList(U.Str(a, "library", false), U.Str(a, "filter", false), U.Int(a, "limit") ?? 60); });

            Add("lib_open",
                "Open a global library from a file (.al21) or a folder that contains one, read-only, e.g. Siemens DriveLib or a company library. " +
                "Afterwards lib_list shows it and lib_copy_block / drive_prepare can use its blocks. The library itself is never changed. " +
                "Without 'path' it searches Documents\\Automation (TIA's default folder) for *.al21, filtered by 'name'; without both it lists what it finds.",
                Schema(none,
                    "path", P("string", "Path of the .al21 file or of its folder. Optional"),
                    "name", P("string", "Part of the library name to search for, e.g. Drive_Lib. Optional")),
                delegate(Dictionary<string, object> a) { return Tia.LibOpen(U.Str(a, "path", false), U.Str(a, "name", false)); });

            Add("lib_copy_block",
                "Copy a PLC block (e.g. SINA_SPEED) from a library type or master copy into a PLC. For types the default (or newest) version is used unless 'version' is given. " +
                "Refuses if a block with that name already exists.",
                Schema(new string[] { "station", "library", "item" },
                    "station", P("string", "Station name or CPU name of the PLC"),
                    "library", P("string", "Library name from lib_list, or 'project'"),
                    "item", P("string", "Exact type or master copy name"),
                    "version", P("string", "Type version, e.g. 1.0.2. Optional")),
                delegate(Dictionary<string, object> a) { return Tia.LibCopyBlock(Station(a), U.Str(a, "library", true), U.Str(a, "item", true), U.Str(a, "version", false)); });

            Add("plc_system_constants",
                "Read-only. Lists PLC system constants (hardware identifiers) with name, type and value, e.g. filter 'Drive_1' to find the HW_IO / telegram identifiers that SINA_SPEED (HWIDSTW/HWIDZSW) or MC technology objects need.",
                Schema(new string[] { "station" },
                    "station", P("string", "Station name or CPU name of the PLC"),
                    "filter", P("string", "Only constants whose name contains this text. Optional"),
                    "limit", P("integer", "Maximum entries (default 60)")),
                delegate(Dictionary<string, object> a) { return Tia.SystemConstants(Station(a), U.Str(a, "filter", false), U.Int(a, "limit") ?? 60); });

            Add("compile",
                "Compile a device offline and return all compiler messages. scope: 'software' (PLC program), 'hardware' (device configuration) or 'all' (default). Large projects can take minutes.",
                Schema(new string[] { "station" },
                    "station", P("string", "Station name or CPU name"),
                    "scope", U.Obj("type", "string", "enum", new string[] { "all", "hardware", "software" }, "description", "all (default), hardware or software")),
                delegate(Dictionary<string, object> a) { return Tia.Compile(Station(a), U.Str(a, "scope", false) ?? "all"); });
        }

        // drive_object index, or the drive object chosen by its name ('drive', wins over the index).
        static int DriveObjectArg(Dictionary<string, object> a)
        {
            string name = U.Str(a, "drive", false);
            if (!string.IsNullOrEmpty(name)) return Tia.DriveObjectIndex(Station(a), name);
            return U.Int(a, "drive_object") ?? 0;
        }
    }
}
