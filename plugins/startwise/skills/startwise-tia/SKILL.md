---
name: startwise-tia
description: Use when the user wants something done in Siemens TIA Portal with the StartWise tools - adding PLCs, I/O modules, drives, panels or field equipment, PROFINET networks, PLC tags and blocks, alarms, classic or WinCC Unified HMI screens, or compiling a TIA project.
---

# Working in TIA Portal with StartWise

StartWise drives the TIA Portal V21 that is open on the user's computer through Siemens Openness. Reply in the user's language. The user is usually an automation engineer: use TIA terms (station, device item, instance DB, telegram, IO system) without explaining them.

## Ground rules

- Offline only. Never download to a PLC, drive or panel and never go online; StartWise refuses online actions anyway.
- The open project is the user's work. Actions that delete or overwrite existing content (Delete, Remove, Disconnect, overwriting blocks) need the user's explicit yes first; tools that need it return `needs_confirmation` - ask, then call again with `confirm=true`.
- Do only what was asked. Field equipment (valves, analog measurements) is prepared only when the user asks for it, never as a side effect of drive preparation.
- Before asking the user a question, check `knowledge/defaults.md` and the scenarios (`scenario_list`, `scenario_get`): what they answer is not asked.
- Defaults unless the user says otherwise: hardware from real article numbers (`catalog_find`, never invent an MLFB), newest firmware, PLCs without password protection, all devices on one subnet (`PN/IE_1`), drives as IO devices of the PLC's IO system, telegram 20 for asynchronous drives.
- Screen layout and visual design belong to the engineer. Create and bind HMI tags, screens and objects; place them with the automatic layout and do not ask for positions.

## If the StartWise tools are missing

This plugin carries the skill only. The tools (`tia_status`, `device_add`, `drive_prepare` ...) come from the StartWise desktop extension, a local server that must be installed once on the Windows PC where TIA Portal V21 runs. If none of these tools is available, do not try to do the work another way. Tell the user, in their language:

1. Download `StartWise-<version>.mcpb` from https://github.com/bilalkirtac/StartWise/releases (latest release).
2. Claude Desktop → Settings → Extensions (or Settings → Desktop app → Extensions → Manage) → Advanced settings → **Install Extension…** → choose the file → Install.
3. Restart Claude, open TIA Portal V21, and ask again.

Requirements to mention: Windows, TIA Portal V21 with the licenses for what they build, and their Windows user in the "Siemens TIA Openness" group. Installation guide (Turkish): https://github.com/bilalkirtac/StartWise/blob/main/docs/KURULUM.md. Support: bilal.kirtac@gmail.com.

## Start of every session

1. `tia_status` - Openness found? user in the "Siemens TIA Openness" group? TIA running? If not, say exactly what is missing.
2. `tia_connect` (attach). On first use TIA shows an Openness access prompt: the user must click "Yes to all".
3. `project_tree` or `project_summary` to see what is there before changing it.

## Typical jobs

| Job | Tools |
|---|---|
| Hardware | `catalog_find` → `device_add` → `module_plug`; addresses: `project_tree` io_addresses, `io_address_set` |
| Network | `network_connect` (subnet, IO system, IP) |
| SINAMICS drive ready for programming | `drive_prepare` (TO or telegram, control block, LAD network) - one call per drive |
| Valve / analog measurement | `equipment_types`, `equipment_prepare` (only on request) |
| Tags, blocks | `tags_add`, `scl_import`, `block_import`/`block_export`, `block_source`, `lib_list`, `lib_copy_block` |
| PLC alarms | `plc_alarm_texts` |
| Classic panel (Comfort) | `hmi_connect` creates the HMI connection; the engineer binds IO fields in TIA (Openness V21 cannot set the controller tag of classic HMI tags) |
| WinCC Unified panel | `hmi_connect`, then `hmi_unified_screen` (labelled output values) and `hmi_unified_add` (buttons, indicators, inputs, trends, alarm view, discrete alarms) bound to PLC tags or DB members such as `SINA_SPEED_Drive_1.ActVelocity` |
| Check the result | `compile` (PLC: real compile; Unified panel: Validate on every HMI object), then `project_save` |

Finish with `compile` and `project_save`, and report in a short table: what was added, which defaults were chosen (firmware, slot, IP), what was skipped and what the engineer should check.

## When no tool fits

Openness can do more than the dedicated tools. Find the operation with `api_search` / `api_type`, look at the live object with `object_inspect` (attributes, compositions, actions, services), then change it with `object_set`, `object_create` or `object_invoke`. Paths look like `station:PLC_1/DeviceItems[PLC_1]/service:NetworkInterface`. Prefer a dedicated tool when one exists.

## Known limits (TIA Portal V21 Openness)

- WinCC Unified "Activate encrypted transfer" (Runtime settings > General) is not exposed: the engineer unticks it in TIA.
- A Unified panel needs the font "Siemens Sans" on the PC; if it is missing the Unified tools switch the fallback font to Microsoft Sans Serif.
- CPUs with firmware V4.x get an anonymous user with the "PLC administrator" role so the hardware compiles; the engineer sets real access control later.
