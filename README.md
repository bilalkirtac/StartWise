# StartWise for TIA Portal

StartWise lets Claude work in **Siemens TIA Portal V21** on your own computer. You describe the job in plain language, for example *"add a CPU 1515-2 PN with a DI 32 module"*, *"add a 2.2 kW G120 and prepare it"* or *"add a Unified panel, connect it to the PLC and show the drive's speed and current"*, and StartWise builds it in the open TIA project through Siemens' **Openness** interface: hardware from article numbers, PROFINET networks, SINAMICS drives (telegram, technology object, control block and LAD network), field equipment such as valves and analog measurements, PLC tags and blocks, PLC alarms, classic and WinCC Unified HMI screens, and an offline compile.

The engineer stays in control: every change is visible in TIA Portal, destructive actions need explicit confirmation, and screen design is left to you. StartWise does the wiring and the repetitive setup.

## Privacy and safety

- **Offline only.** StartWise talks only to TIA Portal on the same computer. It never downloads anything to a PLC, drive or panel and never goes online to devices.
- **No data leaves your computer** through StartWise. It has no network code. What Claude sees is the tool results it reads in your conversation.
- It works on the project that is open in TIA Portal. Keep a backup or an archive of important projects, as with any engineering tool.

## Requirements

| What | Why |
|---|---|
| Windows 10/11, 64-bit | TIA Portal and Openness run only on Windows |
| TIA Portal **V21** with STEP 7 | Openness is installed together with TIA Portal |
| Licenses for what you build | Openness can do what your TIA licenses allow: STEP 7 Professional for S7-1500/ET 200, Startdrive for drives, WinCC Comfort/Advanced or WinCC Unified for panels |
| Your Windows user in the group **"Siemens TIA Openness"** | Siemens allows Openness only for members of this group (log off and on after adding yourself) |
| Claude Desktop or Claude Code on the same PC | StartWise is a local MCP server; it does not run in the browser |

## Install

StartWise has two parts. Install both:

1. **The tools (Claude Desktop extension).** Download `StartWise-<version>.mcpb` from the [Releases](https://github.com/bilalkirtac/startwise/releases) page, then Claude Desktop → Settings → Extensions → Advanced settings → **Install Extension…**. This is the local server that talks to TIA Portal.
2. **The skill (Claude plugin).** Settings → Plugins → Add → marketplace `bilalkirtac/StartWise`, then install **startwise**. It teaches Claude the rules and workflows for TIA Portal (defaults, confirmations, which tool for which job).

**Check the download (optional).** Each release lists the SHA-256 checksum of the `.mcpb` (also in `StartWise-<version>.sha256`). In PowerShell: `Get-FileHash .\StartWise-<version>.mcpb` and compare. The package contains no exe: Claude starts Windows PowerShell, which compiles the C# sources from the package in memory and runs the server.

Then open TIA Portal V21 and tell Claude *"connect to TIA"*. On the first connection TIA Portal asks to allow Openness access: choose **Yes to all**. A Turkish installation guide is in [docs/KURULUM.md](docs/KURULUM.md).

## Build from source

`build.cmd` compiles `src\*.cs` with the C# compiler that ships with Windows (.NET Framework 4.8), against the Openness assemblies of your TIA installation. No SDK or NuGet packages are needed. `pack.cmd` creates the `.mcpb` package in `dist\`.

- `src/` – the MCP server (C#)
- `knowledge/` – defaults, scenarios, equipment blocks (SCL) and the hardware-attribute index Claude reads while working; editable without rebuilding
- `plugins/startwise/` – the Claude plugin (skill, manifest, icon); this folder is what the plugin directory reviews
- `packaging/` – extension manifest, icon, installation guide (PDF)

Siemens Openness assemblies are **not** part of this repository; they are loaded from your own TIA Portal installation.

## Türkçe

StartWise, Claude'un TIA Portal V21'i kendi bilgisayarınızda, çevrimdışı olarak kullanmasını sağlar: donanım, ağ, sürücüler, saha ekipmanı, tag'ler, bloklar, alarmlar ve HMI ekranları düz cümlelerle kurulur. Kurulum rehberi: [docs/KURULUM.md](docs/KURULUM.md). İletişim ve destek: bilal.kirtac@gmail.com

## For companies

StartWise is free and open source. For teams that want it fitted to their own way of working, these are available on request:

- **Setup and customisation:** your naming rules, tag table structure and standard function blocks built into StartWise's knowledge
- **Equipment packages:** your machine and equipment types (conveyors, pumps, dosing, ...) prepared in one call, like drives and valves today
- **Training:** hands-on sessions for your engineering team
- **Support:** help with issues and updates for new TIA Portal versions

Contact: **bilal.kirtac@gmail.com**

### Şirketler için

StartWise ücretsiz ve açık kaynaktır. Kendi çalışma şeklinize uyarlanmasını isteyen ekipler için şunlar sunulur:

- **Kurulum ve uyarlama:** İsimlendirme kurallarınız, tag tablosu yapınız ve standart FB'leriniz StartWise'ın bilgi tabanına eklenir.
- **Ekipman paketleri:** Konveyör, pompa, dozaj gibi kendi makine ve ekipman tipleriniz; bugün sürücü ve vanada olduğu gibi tek komutla hazırlanır.
- **Eğitim:** Mühendis ekibinize uygulamalı kullanım eğitimi.
- **Destek:** Sorun giderme ve yeni TIA Portal sürümlerine uyum.

İletişim: **bilal.kirtac@gmail.com**

## Contact

Questions, support or feedback: **bilal.kirtac@gmail.com**, or open an issue on GitHub.

## License

Apache License 2.0, see [LICENSE](LICENSE) and [NOTICE](NOTICE). "StartWise" and its logo are not licensed for use in other products.

SIMATIC, STEP 7, TIA Portal, SINAMICS and WinCC are trademarks of Siemens AG. StartWise is an independent project and is not affiliated with or endorsed by Siemens. Claude is a trademark of Anthropic.
