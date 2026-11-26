# Automation Engineering Assistant

**Codex-ready technical specification — revision 1.0 — 10 November 2026**  
**Initial engineering and acceptance environment: Siemens TIA Portal V18 on Windows.**  
**Product direction: V18 and later through explicitly qualified adapters.**

## 1. Implementation mandate

Build a professional AI assistant that performs supervised, offline automation engineering through Siemens TIA Portal. It supplements the engineer and Siemens engineering tools. It must use the installed compiler, catalog and supported public APIs rather than emulate TIA Portal or claim capabilities it has not demonstrated.

Start with a small runnable V18 vertical slice: Python MCP server → local C# Openness worker → existing disposable V18 project → inventory → approved creation of one PLC object → compile → save → reopen and verify. Do not attempt the whole product in the first sprint.

This document specifies the product and implementation backlog; it does not claim that an adapter has been built, that this computer has been inspected, or that any TIA project has been compiled. No live project changes are authorized by delivery of this document.

Normative terms: **MUST** is required; **SHOULD** is the default unless a documented reason exists; **MAY** is optional. Examples are design fixtures, not installed device identifiers or production control logic.

### 1.1 Decision provenance

The referenced conversation is “TIA V18 MCP Araştırması,” ID `6af2ef04-2de0-83ed-83ad-4f65b1cd048d`. The available retrieved history contains five decision turns and an earlier-decision summary; the retrieval reports no older page. This specification preserves all decisions visible in that material and the current request. Details not settled there are explicitly implementation proposals rather than retroactively attributed user decisions.

| ID | Confirmed decision |
|---|---|
| D01 | Siemens TIA Portal is the engineering platform; V18 is the first available testing environment, not the product ceiling. |
| D02 | LLM + custom Python MCP server + C# Siemens Openness adapter. |
| D03 | Support both new-project engineering and existing-project inspection, revision and extension. |
| D04 | Cover hardware, I/O, networks, PLC software and HMI/SCADA. |
| D05 | Accept equipment lists, I/O lists, P&IDs and FDS documents. |
| D06 | Target OB, FB, FC, DB, UDT, SCL and LAD with honest feasibility gates. |
| D07 | WinCC Unified, Comfort and Professional require separate integrations. |
| D08 | Offline-first supervised engineering: preview changes and obtain engineer approval before application. |
| D09 | Support customer, company and project standards, approved PLC blocks, faceplates and control algorithms. |
| D10 | Requirement traceability and detailed change impact analysis are optional modules. |
| D11 | Motor interlocks, permissives, sequences, start/stop and alarm behavior are configurable. |
| D12 | Basic engineering validation is mandatory; safety-related changes require authorized engineering review. |
| D13 | Version differences, unavailable functions and project upgrades must be explicit; never assume downgrade support. |

Later D10 supersedes any earlier suggestion that a complete traceability database or comprehensive impact report is mandatory. A bounded change preview and required reference checks remain mandatory.

## 2. Scope and operating boundaries

### 2.1 New projects

Import engineering inputs, resolve ambiguities, select standards and hardware, propose architecture, create an approved offline project, configure qualified hardware and networks, allocate I/O, generate PLC software and supported HMI artifacts, compile and deliver a verification package. Missing engineering decisions must remain unresolved issues until the engineer supplies them; the assistant must not invent CPU order numbers, wiring, safety design or process intent.

### 2.2 Existing projects

Inspect a selected project or explicitly selected TIA process. Inventory device groups, nested device items, software containers, blocks/types/tags and accessible HMI objects. Preserve hierarchy, comments, languages, protection state, library bindings and unknown attributes where supported. Infer conventions as suggestions; never silently declare them authoritative standards.

Create a baseline, inspect relevant dependencies, propose a bounded diff, apply to a working copy after approval, compile affected scope and deliver verified results. Unreadable or protected content is an explicit coverage gap, not evidence that dependencies are absent. Do not regenerate whole projects to implement small changes.

### 2.3 Not in the initial product boundary

No PLC/HMI download, online writes, force tables, RUN/STOP, firmware update, commissioning, network discovery of production equipment, credential extraction or automatic migration. Do not expose these commands as hidden worker options. Runtime integrations and simulation may be later separately authorized modules. Offline compilation does not prove functional correctness, SIL/PL compliance or safe commissioning.

Safety programs, F-signatures, safety hardware parameters and safety logic are outside initial write support. The system may report accessible metadata with appropriate permissions. Future safety engineering requires a dedicated qualified workflow, competent reviewer and applicable lifecycle evidence; a generic approval button cannot qualify safety software.

## 3. Official API evidence and compatibility policy

Evidence was checked against official Siemens web material on 10 November 2026. `documented` means a cited source describes an API; `tested` requires local evidence. Neither a later-version manual nor a search snippet establishes V18 compatibility. The direct V18 manual PDF returned HTTP 403 during retrieval; indexed excerpts and later official documentation were accessible. Exact V18 signatures must be validated with installed V18 public assemblies and local help in Sprint 0.

### 3.1 Verified claims and limits

| Claim | Evidence and engineering consequence |
|---|---|
| Openness installation and Windows group membership are prerequisites. | [S1], [S2]. Preflight must detect access problems and explain manual administrator setup; do not change membership silently. |
| V18 introduced explicit product license checking through Openness. | [S3]. Missing engineering licenses must produce a real prerequisite error, not a fake success or workaround. |
| API version, installed product version and exported file engineering version are distinct. | [S4]. Record all three; qualify import/export pairs instead of inferring compatibility from assembly identity. |
| V20 includes older V17/V18/V19 API libraries, but exports use the V20 engineering version. | [S4]. This is a bounded documented compatibility fact, not a perpetual guarantee. |
| V21 changed the assembly model and does not ship V17–V20 Siemens.Engineering libraries. | [S5]. V21 needs a separately built and tested adapter; do not load a V18 worker against V21 by path substitution. |
| Openness self-description exposes attributes, compositions, invocations, creation metadata and services. | [S6]. Use metadata to discover available operations; do not turn arbitrary dynamic invocation into an LLM tool. |
| Compilation uses the `ICompilable` service; targets differ and devices must be offline. | [S7]. Resolve service availability and recursively collect compiler diagnostics. |
| External source generation can overwrite existing blocks and is an offline operation. | [S8]. Detect every possible output collision before generation; approve replacement explicitly. |
| Device creation is based on catalog type identifiers. | [S9]. Resolve identifiers and firmware on the installed catalog; a generic product name is insufficient. |
| Unified engineering access is distinct from runtime access such as GraphQL. | [S10], [S11]. Runtime tag APIs do not establish the ability to engineer screens. |
| Unified behavior depends on the installed update level. | [S12]. Include updates in the support key and retain regression fixtures. |

### 3.2 Initial support matrix

These are delivery targets, not claims of current implementation.

| Adapter | First milestone | Promotion gate |
|---|---|---|
| TIA V18 | Mandatory first worker and local acceptance | Read, approved PLC-object creation, compile, save/reopen, recovery evidence |
| TIA V19 | Next qualification | Build with selected supported API, run full contract and golden-project suite |
| TIA V20 | Later qualification | Verify assembly/file compatibility and repeat suite |
| TIA V21+ | Independent port and future qualification | Re-evaluate assemblies, runtime, installation and API changes; no automatic inheritance |

A supported release is an explicit tuple: `{TIA version, update/build, API assembly identity/hash, OS, installed products, device firmware/catalog, HMI family, tested operation}`. Future versions default to `unverified`. No automatic upgrade on opening an older project. Offer a separately approved copy-based migration plan; retain the original. Never promise conversion back to an older project format.

### 3.3 HMI capability boundaries

“Comfort” means classic WinCC Comfort engineering, not Unified Comfort Panels. “Professional” means WinCC Professional in TIA Portal, not standalone WinCC V7/V8. Runtime Advanced and Basic are outside the initially committed family set unless explicitly added.

| Operation | Unified | Classic Comfort | TIA Professional |
|---|---|---|---|
| Detect target and inventory supported metadata | Probe and qualify | Probe and qualify | Probe and qualify |
| Tags and PLC connections | Separate API mapping and tests | Separate API mapping and tests | Separate API mapping and tests |
| Screens, objects, properties, dynamics | Qualify each object/property and version | Qualify supported import/object operations | Qualify separately from Comfort |
| Faceplate types/instances and library versions | Family-native artifacts only | Family-native artifacts only | Verify actual type support; no inherited assumption |
| Alarms, text lists, scripts, recipes, archives, users | Per-feature feasibility gates | Per-feature feasibility gates | Per-feature feasibility gates |
| Compile and diagnostic extraction | Probe actual target/service | Probe actual target/service | Probe actual target/service |
| Cross-family migration | No automatic equivalence | No automatic equivalence | No automatic equivalence |

Initial status for HMI write operations is `unverified`, not `supported`. A family may provide inventory, qualified templates or manual handoff while another provides automated creation. Report partial support precisely, including unsupported object types and properties. Preserve unknown objects by leaving them untouched; refuse a destructive whole-screen round trip if preservation is unproven.

Fallback order: supported public API → qualified Siemens-supported import/library workflow → exportable engineering package with explicit manual steps → clear unsupported result. UI automation must not be a silent substitute for missing APIs. Any later UI fallback requires a separate observed workflow and approval.

Do not assume Unified and Professional engineering products can coexist in one installation; Siemens' V18 changeover guide identifies a parallel-installation restriction [S10]. Confirm exact release installation rules and use separate qualified engineering environments where needed.

## 4. Architecture

```text
Engineer / Codex MCP client
        | MCP stdio: typed tools, resources, structured results
Python MCP server
        +-- Engineering core: input normalization, models, standards, planners
        +-- Policy: approvals, workspace restrictions, allowed operations
        +-- Validation: model checks, compile orchestration, result coverage
        +-- Audit store and artifact store
        +-- Optional: requirement graph and detailed impact analysis
        |
        | versioned local JSON-RPC over private stdio pipes
Version manager / worker launcher
        +-- C# V18 worker --> installed V18 Openness --> TIA Portal
        +-- C# V19 worker --> qualified V19 API --> TIA Portal
        +-- C# V20 worker --> qualified V20 API --> TIA Portal
        +-- C# V21 worker --> qualified modular API --> TIA Portal
                    +-- PLC adapter
                    +-- Hardware/network adapter
                    +-- Unified / Comfort / Professional adapters
```

The LLM proposes intent and artifacts. Deterministic code validates schemas, resolves object references, checks permissions, prepares diffs and executes allowlisted actions. The LLM never receives a raw reflection, shell, unrestricted file-write or Siemens method invocation tool.

### 4.1 Proposed technology choices

- Python 3.11+ for the MCP and engineering core, subject to pinned SDK support; Pydantic models, JSON Schema and pytest. Pin tested dependencies in a lockfile.
- C# x64 process for V18, initially targeting .NET Framework 4.8, to be confirmed against installed V18 documentation and assemblies. Later workers choose their own validated target frameworks. Do not reference all Siemens versions in one process.
- Official Python MCP SDK, version pinned at implementation time; use supported SDK registration and transport rather than invent a custom MCP wire protocol. Verify current SDK methods before coding.
- Private child-process stdio for the first worker bridge. JSON lines carry JSON-RPC 2.0; stdout is protocol-only, stderr is diagnostics. No listening TCP port in the POC.
- SQLite for local jobs, plans, approval metadata and audit indexes; immutable artifact files with SHA-256 manifests. Store credentials in an OS-backed secret store if later needed, not SQLite plaintext.
- Human-readable JSON/YAML standards and engineering models. SQLite storage of trace links is optional; core engineering must work with the module disabled.

### 4.2 Worker execution rules

One serialized command queue per owned TIA session. Execute Siemens calls on a dedicated thread configured as required by the release documentation; choose STA for the initial worker and verify event handling. Do not parallelize writes or share Siemens object references across workers. A worker returns opaque session-scoped handles, never serialized CLR objects.

Enumerate nested device groups/items recursively and use supported software-container services to discover PLC/HMI software; do not assume CPU index `DeviceItems[1]`. Record inaccessible branches. Separate lifecycle ownership: an attached user-owned TIA process is never disposed or closed as though the worker created it. Closing a session must not save by default.

Version discovery reads installed Openness registration and verified installation locations, then checks file version and assembly identity. Do not scan drives indiscriminately or select a DLL based only on a filename. Resolve Siemens dependencies from the selected installation; do not distribute Siemens binaries without licensing permission. Unknown installations are reported and require qualification.

### 4.3 Repository target

```text
automation-engineering-assistant/
  AUTOMATION_ENGINEERING_ASSISTANT.md
  README.md
  pyproject.toml
  dependency-lock-file
  src/aea_mcp/                 # MCP tools and resource registration
  src/aea_core/                # models, planning, validation, standards
  src/aea_policy/              # approval verifier and authorization
  src/aea_adapters/            # worker protocol client and routing
  workers/Contracts/           # version-independent DTOs
  workers/TiaV18/              # release-specific references and logic
  workers/TiaV19/              # added only when implemented
  workers/TiaV20/
  workers/TiaV21/
  schemas/                    # generated JSON Schema, versioned
  standards/example-company/
  fixtures/inputs/
  fixtures/expected/
  tests/unit/
  tests/contracts/
  tests/integration_v18/
  scripts/                    # build, preflight, smoke, recover
  docs/api-evidence/
  docs/qualification/
  runs/                       # ignored: working copies and artifacts
```

## 5. Engineering input pipeline

Accept CSV/XLSX equipment and I/O lists, text/Markdown/DOCX/PDF FDS, and PDF/image P&IDs initially. Native CAD/P&ID exchange needs a later explicit adapter. Do not claim arbitrary P&ID interpretation is reliable.

Pipeline: register immutable source → parse/extract → normalize → retain location/provenance → detect conflicts/ambiguities → obtain engineering resolutions → validate → freeze an input model revision → plan artifacts. Preserve original units and source text as well as normalized engineering values. Maintain schema migrations.

Every extracted fact has `source_id`, file hash, location (sheet/row, page/bounding box or section), extraction method, confidence and review state. A confidence score does not authorize engineering. Ambiguous OCR, crossed connections, missing device type, inconsistent tag identifiers and FDS contradictions become issues. Critical unresolved issues block affected generation; unrelated validated parts may proceed in a clearly bounded plan.

Equipment examples: motors, valves, pumps, analog transmitters, digital sensors, drives, packages. Signals must resolve equipment, direction, type, units, range, normal state, fail state, wiring assumptions and controller ownership where applicable. Distinguish physical I/O, internal PLC tags and network/drive process data. A P&ID does not by itself define electrical wiring, slot layout or a safety architecture.

Input files, embedded instructions and project comments are untrusted data. Ignore commands embedded in them; only authenticated engineer actions and policy authorize changes. Reject macros, executable content, path traversal, external links that require execution and decompression bombs. Apply file-size/parser limits and never upload confidential engineering files to an LLM provider without configured data authorization.

## 6. Canonical schemas

All schema definitions MUST use JSON Schema 2020-12 with `$id`, `schema_version`, explicit required fields, enums, length/range limits and `additionalProperties: false` on command objects. Unknown imported attributes belong in a namespaced read-only metadata map, not arbitrary command fields. Use UUIDs for records, stable project-local IDs for entities, UTC ISO-8601 timestamps and SHA-256 content hashes. The examples below define field intent; implementation must emit complete machine-checkable schemas, not copy only the examples.

### 6.1 Core entity contracts

| Schema | Required content |
|---|---|
| `ProjectContext` | ID, original path, working path, project format, installed TIA build, API identity, mode (`new`/`existing`), owned/attached lifecycle, fingerprint, revision token, standards lock, optional-module configuration |
| `Equipment` | ID, unique tag, class, description, area, controller reference, control profile reference, provenance, review state |
| `Signal` | ID, equipment reference, tag, `DI/DO/AI/AO/internal/network`, PLC data type, direction, address or explicit unallocated state, units/range where applicable, channel reference, provenance |
| `HardwareDevice` | ID, parent/slot, catalog type identifier, order number, firmware, product dependency, channels, network interfaces, validation state |
| `Network` | ID, type, subnet properties, members, address/name uniqueness domain, proposed topology and verified constraints |
| `ControlProfile` | Equipment scope, mode logic, command arbitration, permissives, run interlocks, trip/reset/feedback/restart policy, alarm policy, library binding |
| `PlcArtifact` | ID, target PLC/group, kind, language, interface, dependencies, source/import artifact hash, naming/number policy, ownership, protection/library constraints |
| `HmiArtifact` | ID, HMI family and target, kind, native template version, PLC binding, language/text bindings, feature requirements, preservation strategy |
| `ValidationResult` | Check ID, scope, `pass/fail/warning/not_run/unsupported`, severity, diagnostics, coverage, evidence references, timestamps |
| `Issue` | ID, kind, affected entities, source references, severity, unresolved/resolved state, engineer resolution and provenance |

References must resolve within a pinned model revision. Duplicate equipment tags, signal IDs or addresses are errors within the relevant namespace. Optional fields must be absent or explicitly null according to schema; do not use empty strings as unknown values.

```json
{
  "schema_version": "1.0",
  "equipment": {
    "id": "eq-m101",
    "tag": "M-101",
    "class": "motor",
    "description": "Conveyor motor",
    "area": "AREA-01",
    "controller_ref": "plc-main",
    "control_profile_ref": "motor-profile-01",
    "review_state": "engineer_confirmed",
    "provenance": [{"source_id": "equipment-list-01", "location": "Equipment!row:2"}]
  },
  "signal": {
    "id": "sig-b102",
    "equipment_ref": "eq-m101",
    "tag": "B102_Ready",
    "kind": "DI",
    "data_type": "Bool",
    "direction": "input",
    "address": "%I0.2",
    "channel_ref": "di-module-01/ch2",
    "units": null,
    "review_state": "engineer_confirmed",
    "provenance": [{"source_id": "io-list-01", "location": "IO!row:4"}]
  }
}
```

This example address is a test fixture only; generation must verify the real hardware address assignment.

### 6.2 Capability contract

```json
{
  "schema_version": "1.0",
  "capability": "plc.tag_table.create",
  "status": "unverified",
  "context": {"tia_version": "V18", "tia_update": "unknown", "hmi_family": null},
  "documentation": [{"source_id": "local-v18-help", "section": "pending"}],
  "probe": {"state": "not_run", "evidence": []},
  "constraints": ["offline", "writable_working_copy", "approved_plan"],
  "unsupported_reason": null
}
```

Status enum: `unverified`, `documented`, `tested`, `unsupported`, `blocked_by_environment`. A `tested` capability must include exact environment tuple, test ID, timestamp and evidence hash. A blocked license or missing installed component is not the same as unsupported API. A method discovered in metadata is not automatically a tested business capability.

### 6.3 Change plan contract

```json
{
  "schema_version": "1.0",
  "plan_id": "0a6bce1a-a447-4f41-9c3b-21b98b9f3c46",
  "project_id": "test-project",
  "base_fingerprint": "sha256:<baseline>",
  "model_hash": "sha256:<model>",
  "standards_lock_hash": "sha256:<standards>",
  "capability_snapshot_hash": "sha256:<capabilities>",
  "operations": [{
    "operation_id": "op-01",
    "kind": "plc.tag_table.create",
    "target_ref": "plc-main/tag-tables",
    "arguments": {"name": "AEA_POC_Approved"},
    "preconditions": [{"kind": "name_absent", "value": "AEA_POC_Approved"}],
    "expected_postconditions": [{"kind": "object_exists", "value": "AEA_POC_Approved"}],
    "risk": "standard_offline",
    "replaces_existing": false
  }],
  "validation_policy": "v18-poc-01",
  "rollback_strategy": "discard_working_copy_and_restore_baseline",
  "publish_policy": "retain_original_and_deliver_candidate",
  "plan_hash": "sha256:<canonical-plan-excluding-this-field>"
}
```

Hash canonical JSON using a documented stable canonicalization procedure; arrays retain semantic order. Approval binds all operation arguments, artifact bytes, preconditions, dependencies and publication destination. A narrative summary alone is insufficient. Plans are immutable once presented; any edit creates a new revision and invalidates prior approval.

## 7. Configurable control logic

No motor template may hardcode universal interlocks. Standards provide defaults; the engineer may change them per project/equipment within mandatory technical and safety constraints. Distinguish start permissive (conditions for starting), run interlock (conditions maintained while running), trip/latch behavior and normal stop. Their exact semantics belong to a versioned profile.

Use a typed expression AST with `and`, `or`, `not`, comparisons and approved timer/state elements; avoid `eval`, arbitrary Python/C# expressions or opaque natural-language predicates. Resolve symbols and types before code generation. Explicitly configure signal polarity, missing/invalid feedback handling, local/remote and manual/auto arbitration, start edge/level behavior, stop priority, feedback timeout, overload response, reset policy, power restoration and restart prevention.

```yaml
schema_version: '1.0'
profile_id: motor-profile-01
library_binding: company/FB_Motor@2.1.0
equipment_ref: eq-m101
start_permissive:
  op: and
  args:
    - {op: symbol, ref: ProcessReady, data_type: Bool}
    - {op: symbol, ref: B102_Ready, data_type: Bool}
run_interlocks:
  - id: overload
    healthy_expression: {op: symbol, ref: OverloadHealthy, data_type: Bool}
    on_false: trip_latched
    reset: manual_when_healthy
restart_policy: require_new_start_command
safety_classification: standard_control_only
```

This is an illustrative standard-control profile, not a safety function or a complete motor design. Safety signals must not be demoted to ordinary configurable permissives.

Required revision scenario: “Add B-102 to M-101 start permissives and retain the overload interlock.” Read the current interface and logic, resolve B-102, preserve overload behavior and library binding, show the exact logical delta, flag unresolved semantic issues, obtain approval, apply offline, compile and verify. If the existing implementation cannot be inspected or changed without breaking a library contract, stop with a concrete limitation rather than replace it wholesale.

Behavioral acceptance must cover: permissive false blocks start; new permissive true allows an otherwise valid start; overload still trips; resetting while overload unhealthy cannot restart; power restoration follows the agreed restart policy. Whether a start permissive dropping during operation stops the motor must come from the profile, not an implicit generator assumption.

## 8. Engineering Standards Manager

Support versioned organization, customer and project packages containing PLC FB/FC/UDT/DB templates, HMI faceplates/screen templates, alarm/text conventions, tag/block naming, state machines, interlock patterns, I/O allocation rules and validation policies.

Precedence: mandatory safety/technical constraints → customer/project requirements → company standards → platform defaults. Report conflicts; do not silently override mandatory constraints. Conflicting customer and project requirements need engineer resolution.

Package manifest requires ID, semantic version, publisher, hashes, dependency lock, supported TIA/API/HMI/CPU/language combinations, artifact inventory, parameter/interface schema, migration policy, license provenance and test evidence. Pin exact versions per project; never use a floating `latest` library. Validate package integrity and reject executable hooks unless a separately governed trusted extension mechanism exists.

Prefer approved blocks over LLM rewrites. Preserve library type/instance relationships; importing a copied block must not be described as instantiating a library type unless that relationship actually exists. Upgrades require an interface diff and approved plan. The assistant must not modify a shared global library while changing one project unless the plan explicitly includes that scope.

Support both TIA external-source/import artifacts and editor handoff. Label them correctly: complete source files are imported/generated through the appropriate TIA workflow; an implementation body is pasted into an existing block after configuring its interface. Do not present legacy Simatic Manager wrappers as directly pasteable TIA V18 editor content. Verify exported/imported types, string lengths, optimized access and block interfaces in the target project.

## 9. Hardware, I/O and PLC engineering

### 9.1 Hardware and network planning

Model CPU family, performance/capacity requirements, modules/racks/slots, remote I/O, PROFINET network, device names, IP/subnet, firmware, HSP/GSDML dependencies and drive/package integration. Resolve actual catalog entries and check slot compatibility before creation. Never silently substitute a different CPU, module, firmware or channel type.

Validate channel capacity/type, reserved channels, electrical-range assumptions, byte/bit address overlaps (including byte/word/dword spans), analog ranges/units, controller ownership, duplicate station names/IPs within each network, slot constraints and unresolved catalog dependencies. Address aliases are rejected unless explicitly modeled and approved. Network and device-specific rules may exceed API visibility; identify manual verification obligations.

Use hardware catalog and supported public services for create/plug/configure operations. Treat network connections, address attributes, channel settings, topology and Startdrive parameters as separate capabilities. A successful device creation does not prove supported parameterization or drive commissioning.

### 9.2 PLC software

Support block/type/tag inventory first, then qualified creation and revision. Generate deterministic interfaces and dependency order (types → dependent types → DBs → FC/FBs → instances/call sites); report cycles and unavailable dependencies. Use controlled SCL external source generation or qualified SimaticML/library import, not undocumented binary project edits.

SCL is the first generation path. LAD is a later capability using validated Siemens exchange schemas/templates and a real round-trip fixture. Never represent SCL text as a LAD network or promise automatic SCL-to-LAD conversion. OB scheduling, scan cycle, initialization, retentivity, addressing, data types and memory limits require explicit design choices.

Before replacing generated outputs, inventory all names and potential source-generated artifacts; external-source generation can overwrite existing blocks [S8]. Reject collisions unless the exact replacements were approved. Preserve protected blocks and do not attempt protection bypass. Semantic diffs must include interfaces, instance DB implications and call-site changes where discoverable. Record unresolved coverage.

## 10. Mandatory validation and optional modules

Mandatory checks: schema and references, naming/uniqueness, I/O address overlap and capacity, unresolved symbols, PLC/HMI binding consistency where accessible, standards compatibility, source/import syntax, offline status, required product/license availability, compiler errors and post-apply object verification. Warnings must be visible and classified; strict acceptance may block selected warning classes. Never relabel `not_run` as passed.

Collect compiler results recursively with severity, message/code where available, object path, timestamp and original diagnostic text. Record exact compiled scope. A successful PLC software compile is not a successful hardware/HMI compile. Capture baseline diagnostics for existing projects and distinguish pre-existing from introduced issues; introduced errors always block candidate acceptance. The POC fixture requires zero compiler errors.

Optional module A, requirement traceability: versioned requirements, provenance, links to equipment, PLC/HMI artifacts and tests, coverage views and exported trace matrix. Disabled by default unless configured; no requirement database needed for basic generation.

Optional module B, detailed impact analysis: dependency graph, affected hardware/software/HMI/tests, before/after requirement coverage and transitive impact report. Report confidence and inaccessible edges. Never claim complete analysis of protected code, dynamic accesses or external systems.

Even with both disabled, keep an immutable change audit, exact operation preview, relevant dependency checks, safety classification, backup and compiler evidence. Audit is accountability for the executed job, not an optional requirements management system.

## 11. Supervised change lifecycle and approvals

```text
DISCOVERED → BASELINED → PLANNED → PREVIEWED → AWAITING_APPROVAL
 → APPROVED → APPLYING → APPLIED → VALIDATING
 → VALIDATED → SAVED_CANDIDATE → REOPEN_VERIFIED → DELIVERED

Any stage may → FAILED / CANCELED / STALE / OUTCOME_UNKNOWN
Failure after mutation → QUARANTINED_CANDIDATE → RECOVERY_REVIEW
```

1. Read/analyze without project mutation; obtain explicit target selection, never choose a process ambiguously.
2. Establish a clean baseline. Prefer a closed-project copy or supported archive/retrieve workflow. Do not copy a live project's changing database files and call it a consistent backup.
3. Produce a concrete plan with proposed artifacts, semantic diff, mandatory checks, limitations, intended working copy and save/publication behavior.
4. Obtain approval from an authenticated engineer through a trusted local approval surface. An MCP client confirmation alone is not proof unless it creates a verifiable record bound to this plan. An LLM cannot approve its own plan.
5. Bind approval to identity, role, project ID, baseline fingerprint, plan/artifact/standards hashes, scope, expiry and nonce. Enforce authorization in both Python and the C# worker, or a shared trusted verifier the worker independently calls. Worker access must not bypass approval.
6. Recheck state under the available exclusive-access/concurrency mechanism. Re-read affected objects and preconditions; timestamp-only file checks are insufficient. Any relevant external edit makes the plan stale and requires a new preview/approval.
7. Apply only allowed operations to the working copy. Log intent before each mutation and evidence after it. Use release-supported transactions where applicable, but do not assume all Openness operations share one atomic transaction.
8. Validate and compile; save the candidate only according to approved scope. Reopen in an owned instance and verify persisted objects and checks. Deliver the candidate and evidence while retaining the original.

Creation of a project, hardware changes, PLC/HMI imports, source generation, destructive actions, saving and upgrading are mutations governed by the plan. Compilation is a validation action that may update generated project state: run it only in the approved sandboxed working project or clearly authorized validation scope. Read-only session access does not imply permission to compile/save.

An approval may cover a finite batch including apply, compile, save and reopen; do not ask repeatedly for already approved steps. Publishing over an original or replacing a delivered project is a distinct scope and requires a concrete approved publication plan. Initial policy delivers a separate candidate and never overwrites the original.

Detailed safety review is required for proposed safety-related changes even if those writes remain unsupported. Standard motor interlocks remain configurable; changing them requires engineering review and behavior verification, not a blanket immutable rule.

## 12. MCP tool contracts

Tool names below are proposed application contracts, not Siemens API method names. All tools must publish input/output JSON Schema and truthful MCP annotations (`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`) where supported. Annotations are descriptive; policy enforcement is server-side.

### 12.1 Common envelope

Every project call requires an opaque `session_id` and `project_id` where applicable. Never accept arbitrary filesystem paths or object expressions from a model when a registered artifact/object handle can be used. Common response:

```json
{
  "schema_version": "1.0",
  "request_id": "req-123",
  "status": "succeeded",
  "data": {},
  "diagnostics": [],
  "coverage": {"complete": true, "gaps": []},
  "evidence": [],
  "state": {"project_revision": "rev-7", "mutation_outcome": "none"}
}
```

Status enum: `succeeded/partial/failed/blocked/canceled/outcome_unknown`. Mutation outcome: `none/applied/not_applied/unknown`. Do not return succeeded when only a job was queued; return a job ID with its current state. All diagnostics carry stable error code, human explanation, recoverability and action required. Never expose credentials or arbitrary local file contents in diagnostics.

### 12.2 Initial and staged tools

| Tool | Inputs | Result and authorization |
|---|---|---|
| `system.preflight` | Optional requested release | Install/build/API/product/access checks; read-only; unknown license state stays unknown |
| `system.capabilities` | Environment/session, optional target | Capability records and evidence; read-only |
| `tia.list_instances` | Release filter | PID, version, accessible project summary, ownership state; read-only |
| `tia.connect` | Explicit instance ID or owned-instance release, access mode | Session handle; no save/upgrade; security prompt may require user action |
| `project.open` | Registered project artifact/path ID, read mode | Project handle; reject unexpected upgrade; no automatic migration |
| `project.inspect` | Project handle, bounded selectors, pagination | Inventory, fingerprints and coverage gaps |
| `inputs.register` | Approved workspace file IDs, source kind | Source IDs/hashes and parse jobs; no TIA mutation |
| `engineering.normalize` | Source IDs, mapping profile | Model revision and issues; no TIA mutation |
| `standards.resolve` | Project requirements, package IDs/versions | Standards lock and conflicts; no TIA mutation |
| `engineering.plan` | Project/model revision, typed intent, standards lock | Immutable plan, artifacts, checks and approval requirements |
| `engineering.preview` | Plan ID | Exact diff, risks, replacements, unknowns, compile/save scope |
| `engineering.approval_status` | Plan ID | Trusted approval state; cannot grant approval |
| `engineering.apply` | Plan ID, approval record ID, idempotency key | Durable job ID; verify approval and baseline in worker |
| `engineering.validate` | Plan/job ID, approved validation policy | Mandatory checks and compiler job/results; working copy only |
| `project.save_candidate` | Plan/job ID, approved destination ID | Save manifest; only after required validation passes |
| `project.verify_candidate` | Candidate ID | Owned reopen and persisted-state verification |
| `jobs.get` | Job ID | Durable phase, diagnostics, evidence and mutation outcome |
| `jobs.cancel` | Job ID | Best-effort request; cancellation is not rollback |
| `recovery.inspect` | Job ID | Read-only reconciliation after crash/unknown outcome |
| `project.close` | Project/session handle | Close without save; refuse ambiguous unsaved-state handling |
| `traceability.report` | Project/model ID | Optional module; explicit disabled status if unavailable |
| `impact.analyze` | Plan ID | Optional detailed report with coverage |

Worker action allowlist initially contains discovery, inventory, create tag table, compile, save, close and verification. Later actions include create project/device, configure qualified attributes, create/import tags, generate/import PLC artifacts, instantiate approved library types and HMI family-specific operations. They must execute from approved plans; do not expose convenience write tools that bypass the same lifecycle.

Expose source/plan/diagnostic artifacts through bounded MCP resources. Large exports return artifact IDs and summaries, not entire projects in tool output. Paginate inventories; default to bounded depth and size with continuation tokens. Protected data must remain redacted according to policy.

### 12.3 Worker bridge protocol

JSON-RPC requests have `id`, `method`, `params`, `protocol_version` and context containing session, project, plan, approval and idempotency identifiers. Version handshake occurs before any Siemens call. Methods are an enum, not CLR names. Reject unsupported protocol/schema versions before mutation.

Structured errors include: `INSTALLATION_NOT_FOUND`, `VERSION_UNQUALIFIED`, `API_MISMATCH`, `ACCESS_DENIED`, `LICENSE_MISSING`, `PRODUCT_MISSING`, `PROJECT_LOCKED`, `UPGRADE_REQUIRED`, `CAPABILITY_UNVERIFIED`, `UNSUPPORTED`, `APPROVAL_REQUIRED`, `APPROVAL_EXPIRED`, `PLAN_STALE`, `OBJECT_CONFLICT`, `UNRESOLVED_REFERENCE`, `VALIDATION_FAILED`, `COMPILE_FAILED`, `TIMEOUT`, `OUTCOME_UNKNOWN` and `RECOVERY_REQUIRED`.

Timeouts are configurable per method; UI dialogs may need a human. Never auto-accept unknown dialogs or security prompts. Log the blocked state and resume only after the required action. Killing a timed-out process is not proof of rollback.

### 12.4 Idempotency and recovery

Persist idempotency key → plan hash → operation state before mutation. The same key with a different payload is rejected. Repeated completed requests return the previous result. “Ensure exists” checks semantic equivalence, not just a matching name; conflicts require a new plan.

After crash/disconnect, treat in-flight writes as `outcome_unknown`. Reconnect and inspect expected postconditions before retry. Never blindly repeat source imports, device creation or save. If state cannot be reconciled, quarantine the copy and restore/recreate from a verified baseline. Preserve failed candidates for diagnosis; recovery must not overwrite the original.

## 13. Capability discovery implementation

Build an environment manifest and explicit operation registry. For each operation, combine release-specific documentation, compiled adapter support, available self-description/service metadata, installed products/licenses, target state and a non-destructive probe. Create no hardware or software merely to probe a production project; use a disposable fixture for write probes.

Where available, inspect `GetAttributeInfos`, `GetCompositionInfos`, `GetInvocationInfos`, `GetCreationInfos` and `GetServiceInfos` [S6]. Respect attribute access modes. Compile against actual interfaces for the selected release. Do not infer support by finding an internal type, reflecting private assemblies, or seeing a UI command.

Cache by full environment and target type, invalidate on update/install/catalog changes and retain evidence. Dynamic discovery informs planning but never bypasses allowlists or approval. Return the exact missing capability and a manual/export alternative when applicable.

Deliver a capability catalog with columns: stable capability ID, TIA build, API assembly, target type/HMI family, documentation reference, status, constraints, probe ID, last test, unsupported reason and fallback. Support claims in README and UI must be generated from this catalog.

## 14. First runnable V18 proof-of-concept

This is the implementation contract for the first executable milestone, not a claim that the following commands already exist. Codex must implement these commands and scripts before declaring the POC runnable.

### 14.1 Prerequisites and fixture

- Windows engineering PC with licensed TIA Portal V18 and matching installed Openness; record exact update/build and installed STEP 7/WinCC products.
- Engineer's Windows account has required Openness group membership; administrator performs any setup and the user signs out/in where required. Respect Siemens application access prompts [S1], [S2].
- Build tools capable of compiling the chosen x64 V18 C# target and Python environment. Confirm installed assembly paths (commonly under `Portal V18/PublicAPI/V18`) rather than assuming them.
- A closed, disposable `.ap18` project containing one standard, non-safety S7-1200 or S7-1500 PLC with a baseline that compiles without errors. Record the selected CPU, firmware and catalog dependencies; do not prescribe an unverified order number.
- No connection to production devices. Work on a verified project copy. If no PLC fixture exists, supply manual fixture-creation instructions first; do not block the entire POC on automatic hardware creation.

### 14.2 Implemented minimum behavior

1. `system.preflight` records version, API identity, user access checks and known/unknown product/license state.
2. Start an owned V18 TIA process in a visible mode for first acceptance; opening/connecting must respect Siemens access prompts.
3. Open the disposable working copy without upgrade and inventory nested hardware and PLC software.
4. Read block/type/tag-table metadata and produce a baseline report with coverage.
5. Plan creation of one empty PLC tag table named `AEA_POC_Approved` in the selected PLC. This is a small PLC engineering object with no change to executed control logic. Resolve the exact creation API from V18 assemblies/local help and record it in the evidence catalog.
6. Present project identity, target PLC, object name, absence precondition, validation and save/reopen scope. Obtain trusted engineer approval for this finite batch.
7. Apply the approved creation; read back its name, type and parent. Reject an existing conflicting table rather than silently reuse/replace it.
8. Compile selected PLC software through the available `ICompilable` service, capture all nested diagnostics and require zero errors.
9. Save the separate candidate, close the owned project, reopen and confirm persistence. Verify the original baseline project remains unchanged.
10. Repeat the same idempotency request and show no duplicate table. Export run manifest, operation journal, compiler report and post-reopen inventory.

The empty tag-table object satisfies the first “hardware or PLC object” milestone. It is not evidence of SCL/LAD generation, control behavior or hardware parameterization. Follow immediately with a second slice that imports/generates a minimal standard SCL FC on the same fixture, after qualifying V18 source generation and collision behavior. Do not insert that FC into OB1 or alter runtime call structure unless the plan explicitly includes it.

### 14.3 Required operator runbook

Implement a configuration file using only registered workspace paths and values discovered on the PC:

```yaml
schema_version: '1.0'
tia:
  release: V18
  installation: '<verified installation root>'
  api_assembly: '<verified Siemens.Engineering.dll path>'
  mode: WithUserInterface
workspace:
  root: '<dedicated test workspace>'
  baseline_project: '<closed disposable .ap18 fixture>'
  candidate_directory: '<new per-run destination>'
policy:
  offline_only: true
  require_engineer_approval: true
  overwrite_original: false
modules:
  traceability: false
  detailed_impact_analysis: false
```

Required commands after implementation:

```powershell
# Build script validates local prerequisites and selected Siemens references.
.\scripts\build-v18.ps1 -Config .\config\local-v18.yaml

# Read-only prerequisites and compatibility report.
.\scripts\preflight-v18.ps1 -Config .\config\local-v18.yaml

# Start the Python MCP server; stdout must remain MCP protocol-only.
python -m aea_mcp --config .\config\local-v18.yaml

# Human-operated smoke flow: pauses at trusted approval, then completes the batch.
.\scripts\smoke-v18.ps1 -Config .\config\local-v18.yaml
```

The smoke flow must use the same core/worker contracts as MCP rather than a permissive test-only mutation path. It must not accept an `--approve-all` shortcut. Document MCP client configuration using the actual tested Python executable/module invocation and verify tools can be listed/called from Codex; do not invent client configuration keys.

Expected per-run deliverables: `environment.json`, `capabilities.json`, `baseline-inventory.json`, `plan.json`, `preview.md`, `approval-reference.json` (no bearer secrets), `operations.jsonl`, `compiler-results.json`, `post-reopen-inventory.json`, `manifest.json` and `acceptance-report.md`. The manifest includes hashes and evidence paths.

### 14.4 POC pass/fail gates

Pass only when a real local V18 run proves: MCP connectivity; correct project/PLC selection; readable inventory; blocked mutation without approval; successful approved creation; successful compile; saved persistence after reopen; no duplicate on retry; original retained; and no online operation attempted. A mocked worker is useful for development but cannot satisfy V18 acceptance.

Also demonstrate rejection of a stale plan, an approval for another project, an expired approval and a pre-existing object collision. Inject one worker disconnect during mutation and show reconciliation or explicit `outcome_unknown` without blind retry. Record operator, environment, exact commands, results and evidence hashes. Any unexecuted criterion is marked `not_run`.

## 15. Staged implementation sprints

Sprints are ordered scope increments, not promised calendar durations. Each ends with reviewable code, tests, evidence and updated capability status. Work only on the next incomplete milestone; keep the runnable V18 path working.

| Sprint | Work and deliverables | Acceptance |
|---|---|---|
| 0 — Feasibility and environment | Build skeleton, exact V18 assembly references, offline preflight, API evidence register, fixture and access runbook | C# worker builds against installed V18; Python exchanges protocol messages; missing access/license/product errors are real; no project writes |
| 1 — Read-only vertical slice | MCP server, process selection, owned/attached lifecycle, open/inspect, recursive inventory, capability records | Codex lists/calls tools; reads actual fixture; explicit coverage; user-owned process is not closed; no unexpected save/upgrade |
| 2 — Supervised first write | Immutable plans, trusted approval, working-copy baseline, empty tag-table creation, compiler report, save/reopen, idempotency/recovery | All Section 14 POC gates pass on real V18; evidence bundle delivered |
| 3 — Inputs and standards | Equipment/I/O parsing, FDS extraction, typed model, provenance/issues, standards lock and validation | Valid fixtures normalize deterministically; duplicates/conflicts/ambiguous critical data block affected plans; modules work disabled |
| 4 — PLC generation and configurable control | Qualified SCL import/generation, UDT/DB/FB/FC dependencies, standard motor library mapping, controlled existing-block revision | Generate and compile fixture; source collisions rejected; M-101/B-102 change retains overload; behavior tests exercise agreed profile |
| 5 — Hardware and networks | Catalog resolution, CPU/module insertion, channel/address allocation, qualified PROFINET configuration | Create approved offline hardware fixture; reject invalid slots, overlapping addresses and unknown firmware; compile relevant hardware scope |
| 6 — HMI feasibility and first family slice | Test Unified, Comfort and Professional independently; choose first automatable family based on installed products/evidence | Publish honest three-family operation matrix; one qualified tag/connection/screen or template slice compiles on its target; unsupported features have explicit handoff |
| 7 — Remaining HMI and LAD qualification | Add per-family tested operations, approved faceplates, alarms/texts, LAD exchange fixture | Each advertised operation round-trips and compiles; unknown objects preserved or writes refused; no cross-family parity claims |
| 8 — Multi-version | V19 qualification first, then V20, independently port V21 if available | Same stable tool contracts; per-version workers and regression results; untested releases stay disabled; copy-based upgrades separately approved |
| 9 — Optional modules and hardening | Traceability/impact plugins, larger-project performance, packaging, audit/recovery robustness | Enabled modules produce coverage-aware reports; disabling them leaves core fully functional; threat/recovery tests pass |

HMI inventory feasibility may be explored earlier without delaying the V18 PLC POC. Full HMI generation must not be a dependency for the first runnable release. An unavailable Professional or Unified installation yields a documented pending qualification, not a fabricated pass.

## 16. Test and acceptance strategy

### 16.1 Automated tests without Siemens

Test schema validation and migrations; canonical hashing; expression typing; naming/address-span collisions; dependency ordering; standards precedence; parser provenance; plan immutability; approval signature/scope/expiry; worker allowlist; path containment; idempotency; crash-state machine and MCP contracts. Mock responses must be labeled simulated. Avoid tests that merely restate implementation; exercise failure boundaries and observable behavior.

### 16.2 Real integration fixtures

Maintain separate disposable fixtures per TIA build and HMI family. Keep minimal clean and deliberately broken PLC projects; nested hardware/groups; existing/protected blocks; approved library type/instance; address collisions; missing product/catalog dependencies; mixed language/tag data and HMI binding gaps. Use licensed Siemens environments for real compilation; ordinary CI runs protocol/model tests and reports integration tests skipped when unavailable.

For imported artifacts, compare semantic inventories before/after and preserve original export bytes. Normalize only known volatile metadata for comparison; never remove unknown fields to make a test pass. Check original project integrity, target objects and unrelated representative objects after changes. Re-run appropriate tests after changes or failures; do not claim whole-product validation from one successful smoke run.

### 16.3 Global release criteria

- Every advertised write has exact-release evidence, negative authorization tests and recovery behavior.
- No write path bypasses approved plan verification, including direct worker requests.
- Basic validation functions with traceability and detailed impact disabled.
- Configurable interlocks preserve approved existing conditions and follow explicit semantics.
- Every compile report states scope and errors/warnings/coverage honestly.
- Unsupported operations return stable errors with no project mutation.
- Existing-project modifications retain original files and preserve unrelated artifacts to the demonstrated coverage.
- Secrets and proprietary source are not leaked through logs, errors or default telemetry.
- A fresh user can follow the documented setup and execute the V18 POC; prerequisites and human approval steps are explicit.

Performance acceptance starts with measured inventory and compile times on a recorded fixture. Do not set a universal compile deadline without measurements. Ensure long operations return durable job handles, bounded progress events and cancellation status so the client remains responsive.

## 17. Security, audit and delivery

Run with normal user privileges after administrator-managed installation. Restrict workers to authenticated parent processes and per-user private pipes, approved workspace paths and allowlisted operations. An attacker able to invoke a worker directly must still be unable to bypass approval. Keep signing/approval secrets out of model context, command lines and audit exports. Validate all file paths after resolution, including symlinks/reparse points.

Maintain append-only operation records with actor, timestamp, project/session, plan/hash, approval reference, before/after evidence and result. Hash-chain local records and export manifests for tamper detection; do not describe a local log as tamper-proof against an administrator. Apply retention/redaction policy. Disable outbound telemetry by default; provider/model/data-routing settings must be explicit.

Deliver source, build instructions, pinned dependencies, generated schemas, API/capability evidence, qualification reports, sample standards/input fixtures, recovery runbook and known limitations. Exclude proprietary Siemens assemblies, production projects and credentials from the repository. Include licensing notices for third-party dependencies and imported library content.

## 18. Open decisions to resolve during implementation

These do not change the agreed product direction and must not be silently guessed:

1. Exact installed V18 update, Windows build, STEP 7/WinCC options and licensing.
2. First disposable CPU fixture and catalog/firmware availability.
3. First installed HMI family suitable for a real acceptance run.
4. Initial company library artifacts, naming rules and motor behavior semantics.
5. Trusted local approval surface and identity model; CLI prompt may serve a single-user POC only if bound records are independently verified and the LLM cannot grant approval.
6. LLM provider/model and confidentiality/data-routing policy.
7. Which V19+ engineering environments are available for genuine qualification.

Codex should resolve discoverable environment facts using read-only preflight, implement independent work and ask only for engineering choices or access that cannot be inferred. The first implementation report must distinguish implemented, simulated, locally tested, blocked and planned items.

## 19. Codex execution checklist

1. Read this specification and repository instructions; produce a short milestone plan.
2. Implement Sprint 0 and the read-only Sprint 1 slice first. Verify exact API signatures locally and record them with source references.
3. Implement trusted approval and working-copy recovery before enabling Sprint 2 writes.
4. Make the V18 smoke flow executable end to end. Do not stop at a tool catalog, mock server or architecture diagram.
5. Present the concrete first-write plan to the engineer; after approval, complete the approved finite batch and verify the outcome.
6. Deliver real evidence and explicit limitations. Never label generated-but-uncompiled logic as working PLC software.
7. Continue in staged increments; retain version adapters and HMI-family boundaries rather than growing one unqualified generic adapter.

## 20. Official reference register

References support the narrowly stated claims above. Product requirements, security design, schemas, sprint names and tool contracts are proposed architecture, not Siemens guarantees. Later-release sources require local V18 confirmation before using a signature. Links and update-specific details may change after the verification date.

- **[S1]** Siemens, [Installing TIA Portal Openness — V20](https://docs.tia.siemens.cloud/r/en-us/v20/tia-portal-openness-api-for-automation-of-engineering-workflows/basics/installation/installing-tia-portal-openness). Installation options and matching product versions; used as prerequisite guidance, with V18 local confirmation required.
- **[S2]** Siemens, [Adding users to the Siemens TIA Openness user group — V20](https://docs.tia.siemens.cloud/r/en-us/v20/tia-portal-openness-api-for-automation-of-engineering-workflows/basics/installation/adding-users-to-the-siemens-tia-openness-user-group). Windows access prerequisites.
- **[S3]** Siemens, [Major changes for long-term stability in TIA Portal Openness V18](https://docs.tia.siemens.cloud/r/en-us/v20/tia-portal-openness-api-for-automation-of-engineering-workflows/major-changes/major-changes-for-long-term-stability-in-tia-portal-openness-v18). V18 license checks and API rebuild considerations.
- **[S4]** Siemens, [What's new in TIA Portal Openness — V20](https://docs.tia.siemens.cloud/r/en-us/v20/tia-portal-openness-api-for-automation-of-engineering-workflows/what-s-new-in-tia-portal-openness?contentId=qNjpjHkUruq33qNd9m3Wjg). Bundled older APIs and engineering file compatibility.
- **[S5]** Siemens, [What's new in TIA Portal Openness — V21](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/what-s-new-in-tia-portal-openness?contentId=rajAJWIJSS9m84qRksoNrg). Modular assembly changes and migration from older APIs.
- **[S6]** Siemens, [Self-description support for attributes, navigators, actions, and services — V20](https://docs.tia.siemens.cloud/r/en-us/v20/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/general-functions/self-description-support-for-attributes-navigators-actions-and-services). Public capability metadata methods.
- **[S7]** Siemens, [TIA Portal Openness V18 system manual, December 2022](https://support.industry.siemens.com/cs/attachments/109815199/TIAPortalOpeness_enUS_en-US.pdf), compilation section, indexed page 137. Indexed compilation excerpt was accessible; direct PDF retrieval was blocked. Corroborating later documentation: [Compiling a project — V21](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/functions-for-projects-and-project-data/compiling-a-project?contentId=Lh19wmmzQOEBZSyi3sS~RQ). Confirm target-specific V18 service behavior locally.
- **[S8]** Siemens, [Generating blocks from source — V20](https://docs.tia.siemens.cloud/r/en-us/v20/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/functions-for-accessing-the-data-of-a-plc-device/blocks/generating-blocks-from-source). Offline requirement, overwrite behavior, ASCII-source note and explicit V18 diagnostic change. Qualify encodings and exact overloads on V18; do not assume Unicode/localized source support from a generic file extension.
- **[S9]** Siemens, [Creating a device — V20](https://docs.tia.siemens.cloud/r/en-us/v20/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/functions-on-devices/creating-a-device). Catalog identifier-based creation; does not validate any proposed device on the user's installation.
- **[S10]** Siemens, [Guide for switching from WinCC Comfort to WinCC Unified, V4.0, June 2023](https://cache.industry.siemens.com/dl/files/002/109768002/att_1146559/v4/109768002_Guide_Comfort_to_UnifiedComfort_V40_DOC_en.pdf), section 2.4.3 and installation notes. Distinguishes engineering/runtime openness and identifies the Unified/Professional ES parallel-installation limitation in that scope.
- **[S11]** Siemens, [SIMATIC WinCC — changes in TIA Portal V18](https://docs.tia.siemens.cloud/r/en-us/v20/what-s-new-in-tia-portal/changes-in-previous-versions-of-tia-portal/changes-in-tia-portal-v18/simatic-wincc). Unified GraphQL is runtime access; it is not a general engineering screen-creation API.
- **[S12]** Siemens, [Readme WinCC Unified V18 Update 4, May 2024](https://cache.industry.siemens.com/dl/files/123/109807123/att_1289846/v1/ReadmeRTUniUpdate_V18.0Upd4_enUS.pdf), Unified engineering improvements. Evidence of update-specific Openness behavior, not full feature parity.

**Final implementation rule:** prefer a small real V18 success with clear evidence over broad unsupported capability claims. Keep engineering configurable, changes supervised, validation mandatory and support claims testable.
