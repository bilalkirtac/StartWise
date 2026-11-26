# Değişiklik günlüğü — StartWise (eski adı TIA Assistant)

## 0.37.0 - 2026-11-26
- One install step: the Claude plugin (`plugins/startwise`, `.claude-plugin/marketplace.json`) is removed. The desktop extension (.mcpb) is the whole product; its skills in `skills/` are served by the server (Skills over MCP `skill://` resources, and the `skill_list` / `skill_get` tools until Claude requests skills from local servers). The plugin's skill was a copy of `skills/startwise-tia` plus an "install the extension" note that is no longer needed, so no skill content is lost.
- No exe anymore: build.cmd is removed. StartWise runs only from the .mcpb (start.ps1 compiles the sources in memory); development uses the extension option "Development source folder". docs/KURULUM.md rewritten for this.
- pack.ps1 no longer updates a plugin manifest. README and installation guide (docs/KURULUM-KULLANICI.md, StartWise-Kurulum.pdf) describe the single extension install.
- Multi-axis drives (SINAMICS S120): drive objects were counted twice (once on the axis rack, once on its Motor Module), so S120 showed 7 indexes for 4 drive objects. Each drive object is now listed once.
- drive_telegram, drive_parameters, drive_motor, drive_parameter_list: new optional `drive` argument - choose the drive object by name (rack or module name, e.g. "Drive axis_2", "Infeed_1", case-insensitive); wins over the `drive_object` index. Single-axis drives (G120, G120C, S210) are unchanged.
- drive_telegram lists every drive object of a multi-axis device (index, name, drive object number, main telegram).

## 0.36.0 - 2026-11-26
- Skills over MCP (io.modelcontextprotocol/skills, SEP-2640): the server declares the extension and the resources capability and serves `skills/list`, `skills/get`, `resources/list` and `resources/read` for `skill://` URIs. Skills live in `skills/<name>/SKILL.md` (packed into the .mcpb). First skill: startwise-tia, the same rules as the plugin skill.
- Claude Desktop does not request skills from local servers yet (it connects with protocol 2025-11-25 and only the ui extension), so the same skills are also offered through the tools `skill_list` and `skill_get`; the server instructions tell Claude to read the matching skill first.
- New tool `lib_open`: opens a global library from its .al21 file or folder (read-only), e.g. Siemens DriveLib or a company library; then `lib_list`, `lib_copy_block` and `drive_prepare` can use it.
- lib_open without a path searches Documents\Automation (TIA's default folder, also when Documents is redirected, e.g. to OneDrive) for *.al21, filtered by name.
- Extension setting "Development source folder" (STARTWISE_SRC): start.ps1 compiles that repository's src\ and uses its knowledge\, so a Claude restart picks up changes without a new package.
- drive_prepare finds an opened DriveLib, or opens Drive_Lib*.al21 from Documents\Automation by itself (read-only).

## 0.35.0 - 2026-11-25
- The extension no longer ships an exe: Claude starts `powershell.exe` (signed by Microsoft) with `server/start.ps1`, which compiles `server/src/*.cs` in memory with the .NET Framework C# compiler and runs the server. An unsigned, newly built exe was flagged heuristically by antivirus (Symantec Heur.AdvML.C / Trojan.Gen.9) and removed from the extension folder. Start-up time is unchanged in practice.
- `STARTWISE_HOME` (set by start.ps1) tells the server where its log and knowledge folder are; the exe build still works as before.
- build.cmd prints the exe SHA-256; pack.ps1 writes `StartWise-<version>.sha256` for the release page.

## 0.34.1 - 2026-11-23
- object_inspect / object_invoke `service:` paths find services in every Openness assembly (V21 split API), not only in Siemens.Engineering.Base.
- device_add: in a new project the UMAC anonymous user is null until activated; now activated first, so FW V4.x CPUs (incl. S7-1500T) get Full access again.

## 0.34.0 - 2026-11-22
- Open source (Apache-2.0) and Claude plugin: `.claude-plugin/plugin.json` + `marketplace.json`, skill `startwise-tia`, README, LICENSE, NOTICE.
- Trial limit and license keys removed (`license_activate` tool, License.cs, STARTWISE_LICENSE). All tools work without a key.
- Two parts: the tools come as the Claude Desktop extension (`.mcpb` on GitHub Releases), the Claude plugin carries the skill. Claude Desktop's marketplace sync rejected a plugin whose MCP server is a remote `.mcpb` URL and one that ships the exe; local plugin MCP servers would only run in Claude Code / Cowork anyway, not in chat.

## 0.33.0 - 2026-11-21
- Trial and license. Without a key: changes in up to 3 different TIA projects (a project counts at the first changing tool; it stays usable). Read-only tools and dry runs always work. State kept DPAPI-protected in HKCU\Software\Demory\StartWise and %LOCALAPPDATA%\Demory\StartWise\state.dat (merged).
- License key "SW1.<payload>.<sig>" (RSA-SHA256, offline check, expiry date). Sources: STARTWISE_LICENSE (the .mcpb License key field) or license.key written by the new tool `license_activate`. tia_status shows "license".
- Keys are issued with repos\_lisans\keygen.py (private key outside the repo/package).
- build.cmd references System.Security. Manifest lists hmi_unified_add and license_activate.
- compile (Unified): removed the duplicate start-screen check (RuntimeSettings.Validate reports it).

## 0.32.0 - 2026-11-21
- compile on a WinCC Unified panel: Openness V21 has no ICompilable for HmiSoftware, so the software part runs Validate() on runtime settings, connections, tags, screens + every screen item and alarms, and checks that the start screen exists. Errors/warnings per object and property. (If a later TIA offers ICompilable, it is used instead.) Hardware compile unchanged.

## 0.31.2 - 2026-11-20
- object_set / object_create attributes: real cause of the InvalidCastException found - LINQ Cast<DictionaryEntry> on the JSON Dictionary<string, object> (yields KeyValuePair). Now read through IDictionaryEnumerator. Affected every object, not only Unified.

## 0.31.1 - 2026-11-20
- Unified tools: if the font "Siemens Sans" is not installed on the PC, the fallback font is switched to Microsoft Sans Serif (language-compatible font families on) so the HMI compiles; reported as "font".
- object_set on WinCC Unified objects fixed (the 'as IEngineeringObject' cast itself threw InvalidCastException on Unified proxies).
- Known limit: "Activate encrypted transfer" (Runtime settings > General) is not in Openness V21 - untick it in TIA per panel.

## 0.31.0 - 2026-11-20
- New `hmi_unified_add` (WinCC Unified): buttons (momentary/set/reset/toggle/value, JavaScript events), colour indicators (BackColor bit mapping, Bool or status-word bit), input/output IO fields, live trend control (several series, own colours), alarm view and HMI discrete alarms. Auto layout below existing items.
- `object_set` works on WinCC Unified objects (sets .NET properties in passes); colours accept names / #RRGGBB.
- build.cmd references System.Drawing.

## 0.30.0 - 2026-11-20
- Yeni araç `hmi_unified_screen` (WinCC Unified): her satır için HMI tag'i (Connection + PlcTag, isteğe bağlı doğrusal ölçekleme) ve ekranda "etiket | çıkış IO field | birim". Ekran yoksa oluşturur, varsa satır ekler, var olan IO field'ı atlar.
- Unified bilgisi (test_v21, HMI_2): Tags.Create(ad, tablo); Screens.Create; ScreenItems.Create<HmiIOField/HmiText>; IOField.Dynamizations.Create<TagDynamization>("ProcessValue").Tag; metin "<body><p>..</p></body>"; HmiDataType PLC tipine kilitli → ölçekli Int'te ondalık için HMI aralığı x10^n + ShiftDecimalPlaces n.
- build.cmd: Siemens.Engineering.WinCCUnified.dll referansı; yoksa NO_UNIFIED ile Unified araçları dışarıda kalır.

## 0.29.0 - 2026-11-20
- Kurulum paketi: `pack.cmd` → dist\StartWise-<sürüm>.mcpb (Claude Desktop eklentisi: çift tık / sürükle-bırak kurulum, config JSON elle düzenlenmez). İçerik: manifest, ikon (geçici "SW"), V21 exe, knowledge (generators hariç); kaynak kod yok. Ayarlar: lisans anahtarı (henüz kontrol edilmiyor, STARTWISE_LICENSE) ve isteğe bağlı TIA API klasörü. Manifest `mcpb validate` ile doğrulandı.
- Yeni araç `hmi_connect(hmi, plc)` (V21+): entegre HMI bağlantısı tek çağrıda (HW CommunicationManagement → Connections.Create<HmiConnection>); ortak Ethernet subnet'i kendisi bulur, aynı PLC'ye bağlantı varsa "exists". Klasik ve Unified paneller.
- knowledge/api/hw-attributes-V21.json: V21 "Hardware parameters" PDF'inden 1806 öznitelik (622'si enum'lu) + V18 dizininden tamamlanan (ET200SP, S7-1200 V21 PDF'inde yok) → 2474. Üretici: knowledge/generators/hw_attributes_from_pdf.py.

## 0.28.0 - 2026-11-20
- **V21 ana sürüm** (Bilal): `build.cmd` varsayılanı V21 (bin\V21\TiaAssistant.exe); V18 için `build.cmd V18` (/define:TIA_V18). Geriye dönük destek sonra planlanacak.
- device_add (protection none): CPU FW V4.x+ için "Users and roles" — proje Anonymous kullanıcısı etkin + sistem rolü "PLC administrator" (runtime Full access; yalnız test, devreye almadan önce gerçek kullanıcılar). Reflection ile; V18'de UMAC API yok.
- compile: HMI panelleri de derlenir (PLC yazılımı yoksa cihazdaki derlenebilir yazılım).
- object_* yolları: istasyon adında "/" (ör. "S7-1500/ET200MP station_1") station: kökünde çalışır; item: adında yalnız büyük/küçük harf farkı varsa birebir eşleşme seçilir.
- drive_prepare: CPU FW V4.0/V4.1 → TO 8.0.
- (0.27.3) hmi_tags_from_plc instance DB Input/Output üyeleri.

## 0.27.3 - 2026-11-20
- hmi_tags_from_plc: DB üyeleri Input/Output/InOut/Static bölümlerinden okunur (önce yalnız Static). Instance DB arayüzleri ("SINA_SPEED_Drive_1".ActVelocity, "V3300".Opened) artık HMI'ya bağlanabilir.

## 0.27.2 - 2026-11-20
- object_invoke: generic metotlar "_T" argümanıyla (ör. HW CommunicationManagement Connections.Create<HmiConnection>(localInterface, partnerTarget, partnerInterface)). V21'de HMI bağlantısı API ile kurulabiliyor (V18'de elle sürükleniyordu).

## 0.27.1 - 2026-11-20
- object_invoke: Openness'in genel çağrı API'si dışındaki nesnelerde (ör. kullanıcının `Roles` ilişki listesi) public .NET metodu çağrılır. Parola (SecureString) alan metotlar reddedilir.
- V21 testi: drive_prepare servo (S210 V6.4, CPU 1515-2 FW V4.1 → TO 8.0) uçtan uca 0 hata; LAD XML importu V21 başlığıyla çalışıyor.

## 0.27.0 - 2026-11-19
- TIA V21 desteği (ilk build): sürüm başına ayrı exe. `build.cmd` → V18 (bin\TiaAssistant.exe), `build.cmd V21` → bin\V21\TiaAssistant.exe (/define:TIA_V21).
- V21 Openness TIA ile birlikte kurulu; API PublicAPI\V21\net48 içinde ve bölünmüş (Siemens.Engineering.Base/Step7/WinCC/Safety/Startdrive ...). API klasörü bulma, kitaplık yükleme, api_search dokümanları (tüm Siemens.Engineering*.xml) ve tip arama artık çok kitaplıklı.
- Sürüme bağlı: tia_adapter, mesajlar, .ap/.zap uzantısı, XML <Engineering version>. knowledge\ klasörü yukarı doğru aranır (iki exe aynı knowledge'ı kullanır).
- Kod değiştirilmeden V21 kitaplıklarına karşı derlendi (Startdrive dahil); V18 build'i aynen devam.

## 0.26.1 - 2026-11-18
- object_set / object_create: alanlar artık turlar halinde yazılıyor (sıra önemli değil). Neden: istemci JSON anahtarlarını alfabetik sıralıyor; AI kanalında OperatingRange, OperatingType'tan önce gidip "Value is not valid in the current configuration" veriyordu ve ilk salt-okunur alanda (ParameterSettings=FromTemplate) tüm çağrı duruyordu. İstenen değeri zaten taşıyan alan "already set" sayılır.
- knowledge/scenarios/analog-kanal-ayari.md: 6ES7 531-7KF00-0AB0 ekleme + kanal tipi (ParameterSettings 0 → OperatingType 4 = 2 telli 4..20 mA).

## 0.26.0 - 2026-11-18
- equipment_prepare: `params` = instance DB başlangıç değerleri (FB giriş adıyla; tipine göre biçimlenir, ör. ScaleHigh 150 → 150.0). Yalnız DB oluşturulurken uygulanır.
- Yeni tür `analog`: FB Analog_Meas (0..27648 → mühendislik birimi, HH/H/L/LL + tek histerezis, kablo kopması/taşma hatası, simülasyon, Status). FC "Analog Measurements", tablo "Analogs", tag <kod>_RAW.
- valve: Valve_SA 0.3 — hata çıkışı kapatır, hata bitleri statik latch'te; tag adları <kod>_Opened / _Closed / _CMD.

## 0.25.0 - 2026-11-18
- Yeni: saha ekipmanı hazırlama (drive_prepare yapısının genellemesi, Bilal'in fikri). `equipment_types`, `equipment_prepare(plc, type, name, io)`.
  Tür = knowledge/equipment/<tür>/equipment.json + FB SCL şablonu; yeni tür build gerektirmez.
- İlk tür `valve`: FB Valve_SA (tek etkili, yay geri dönüşlü; oto/manuel, kilitleme, açık/kapalı geri bildirim, hareket izleme, simülasyon, HMI Status kelimesi).
  Instance DB = vana kodu ("V3300"), LAD FC "Valve Blocks", saha sinyalleri `Valves` tag tablosuna (V3300_Y / _ZSO / _ZSC).

## 0.24.1 - 2026-11-18
- drive_prepare: boş FB pinleri LAD içe aktarmada `OpenCon` teli ister. Pin tellerden tamamen çıkarılınca TIA "The connection with the name '1' is not connected to the object with the UID ..." hatası veriyor (SW_Test_A/B denemesi). Boş pin = `<Wire><OpenCon/><NameCon/></Wire>`.

## 0.24.0 - 2026-11-18
- drive_prepare: ayrı `<drive>_IO` DB'si ve S210_IO / SINA_SPEED_IO UDT'leri kaldırıldı (Bilal'in önerisi: gereksiz program yükü). Arayüz artık FB'nin kendi instance DB'si (`S210_AbsCnt_<drive>`, `SINA_SPEED_<drive>`); FC ağında FB pinleri boş bırakılır, senaryo kodu girişleri instance DB'ye yazar, çıkışları oradan okur. Yalnızca Axis (TO) ve HWIDSTW/HWIDZSW bağlı.
- Eski düzende (IO DB'ye bağlı) bir ağ bulunursa yerinde yeniden yazılır; artık kullanılmayan `<drive>_IO` DB'si raporlanır, silinmez.

## 0.23.1 - 2026-11-18
- drive_prepare: yeni bloklar içe aktarıldıktan sonra OB1 tutarsız kaldığı için dışa aktarılamıyordu ("Inconsistent blocks ... cannot be exported") ve OB1 adımı atlanıyordu. Artık önce bir kez derleyip yeniden deniyor.
- İlk saha testi (test_MCP): Servo_2 (S210 5 kW, telegram 105) ve Drive_6 (G120 CU240E-2 PN + PM240-2 2.2 kW, telegram 20) tek çağrıyla hazırlandı; ikinci çalıştırma hiçbir şeyi tekrar eklemedi; tam derleme 0 hata.

## 0.23.0 - 2026-11-18
- Yeni araç `drive_prepare(plc, drive)`: donanıma eklenmiş sürücüyü tek çağrıda senaryo yazımına hazır hale getirir (idempotent).
  - Servo (TO telegramı, ör. 105): CPU FW'ye göre TO_PositioningAxis, aktör bağlantısı, hat (line) port topolojisi, S210_AbsCnt FB + UDT S210_IO + <drive>_IO + instance, LAD FC "Servo Motors" içine ağ, OB1 çağrısı.
  - Asenkron (1/20/352): DriveLib'den SINA_SPEED, UDT SINA_SPEED_IO + <drive>_IO + instance, HW ID sabiti, LAD FC "Asenkron Motors" içine ağ, OB1 çağrısı.
  - Sonunda yazılım derlemesi; kaydetmez. `dry_run=true` ile plan.
- Şablonlar: knowledge/templates/S210_IO.scl, SINA_SPEED_IO.scl, S210_AbsCnt.scl ({MC_VERSION}).

## 0.22.1 — 2026-11-18
- object_create/set/invoke: tipli değer ({"type":"Version","value":"7.0"}, {"type":"path",...}); *Version parametrelerinde 'V7.0' gibi değerler otomatik System.Version. Neden: TechnologicalInstanceDB oluşturma OfSystemLibVersion için Version istiyor (S210 TO denemesi).

## 0.22.0 — 2026-11-18
- Yeni araçlar object_invoke ve object_create: Openness'in genel eylem (GetInvocationInfos/Invoke) ve oluşturma (GetCreationInfos/Create) mekanizması. Nesne parametreleri object_inspect yoluyla verilir. Online eylemler reddedilir; Delete/Remove/Disconnect vb. önce önizleme döner, confirm=true ister.
- object_inspect: listelerde 'creatable' artık tip ve parametre adlarını, 'actions' liste eylemlerini gösteriyor; yol bölme köşeli parantez içindeki '/' karakterini korur (PN/IE_1).
- Hedef: yeni öğrenilen işlemlerin çoğu build gerektirmeden api_search → object_inspect → object_set/invoke/create ile yapılır.

## 0.21.0 — 2026-11-18
- Yeni araç object_set: object_inspect yoluyla bulunan nesnenin özelliklerini sırayla yazar (dinamik özellikler için), tipine çevirir, geri okur. İlk kullanım: PLC_1 clock memory MB0 — gösterim olmadan, yalnız api_search + object_inspect ile öğrenildi.

## 0.20.0 — 2026-11-18
- Kendi kendine öğrenme, 2. katman: object_inspect. Açık projedeki herhangi bir nesnenin özellikleri (erişim + değer), alt listeleri, çağrılabilir eylemleri (GetInvocationInfos) ve isteğe bağlı servisleri. Yol: project / station:<cihaz> / item:<öğe> + 'Devices', 'DeviceItems[PLC_1]', 'service:NetworkInterface'. Salt okunur.

## 0.19.0 — 2026-11-18
- Kendi kendine öğrenme, 1. katman: api_search ve api_type. Kurulu TIA sürümünün Openness API'si (Siemens.Engineering.xml açıklamaları + yansıma ile imzalar) ve donanım parametre kılavuzu (knowledge/api/hw-attributes-V18.json, 1.887 özellik; 'TIA Portal Openness Hardware parameters.pdf'ten üretildi) aranabilir. TIA gerektirmez; her sürüm kendi API dosyalarını okur.

## 0.18.0 — 2026-11-17
- İsim: TIA Assistant → **StartWise**. Claude config anahtarı ve MCP sunucu adı `startwise`, günlük `bin\startwise.log`. Exe dosya adı bilerek `bin\TiaAssistant.exe` kaldı: yeni adlı exe (StartWise.exe) Norton tarafından Heur.AdvML.B olarak engellendi (yanlış alarm, yeni/imzasız exe). C# namespace ve depo klasörü aynı.
- Yeni araç project_archive: TIA projesini .zap18 arşivi olarak yedekler (varsayılan: proje klasörünün yanında Yedekler).

## 0.17.1 — 2026-11-17
- hmi_tags_from_plc: şablon atanmışsa nav butonları sağ üste konur (şablonun alt çubuğuyla çakışmasın). 0.15–0.17 TIA'da doğrulandı (HMI_3 Drive_1_Ozet, Motorlar_2).

## 0.17.0 — 2026-11-17
- hmi_tags_from_plc ve hmi_faceplate_screen: 'template' parametresi (Screen LinkList/Template, 17.11'de doğrulandı); şablon yoksa içe aktarma yapılmaz.

## 0.16.1 — 2026-11-17
- hmi_faceplate_screen: tip export'undan arayüz okunamazsa, HMI'daki mevcut instance'ların arayüz adları kullanılır; hiçbiri yoksa tip XML'inin eleman listesiyle birlikte reddedilir.

## 0.16.0 — 2026-11-17
- Yeni araç hmi_faceplate_screen: proje kütüphanesindeki faceplate tipinin (ör. MotorFP) instance'larıyla yeni ekran; arayüz bağlantıları PLC değerlerinden, HMI tag'leri otomatik. Arayüz adları tipin export'undan doğrulanır. (FaceplateInstance XML'i 17.11'de TP900'de doğrulandı.)

## 0.15.0 — 2026-11-17
- hmi_tags_from_plc: Bool'lar ekranda daire gösterge (0 gri / 1 yeşil, RangeAppearanceAnimation) olarak; 'nav' ile mevcut ekranlara ActivateScreen butonları. Kalıplar Bilal'in HMI_3 Drive_1_Durum ekranından.

## 0.14.1 — 2026-11-17
- hmi_screen_from_library: V18'de ekran adı/numarası salt okunur; kopya kaynak ekranın adını alır. Gerçek numara artık okunup raporlanıyor. hmi_tags_from_plc ekran numarası okuması düzeltildi (WithDefaults).

## 0.14.0 — 2026-11-17
- Yeni araçlar hmi_screen_to_library / hmi_screen_from_library: ekranları proje kütüphanesi master copy'si üzerinden kopyalar. V18 Openness klasik panellerde Alarm view'ı XML'e çıkarmıyor (KTP700 Basic ve TP900 Comfort'ta doğrulandı); master copy yolu onu koruyor.

## 0.13.0 — 2026-11-17
- Yeni araç plc_alarm_texts: PLC alarm instance metinlerini okur / yazar (PlcAlarmTextProvider xlsx dışa-içe aktarma üzerinden).
- Yeni araç block_source: mevcut SCL bloğunu/DB'yi SCL kaynak metni olarak döndürür (düzenleyip scl_import ile geri yazmak için).
- hmi_tags_from_plc: yeni ekran numarası artık mevcut ekranların gerçek numaralarından hesaplanıyor (0.12.0'da 1 öneriyordu).
- build.cmd: System.IO.Compression ve System.IO.Compression.FileSystem referansları.

## 0.12.0 — 2026-11-17
- Yeni araç hmi_tags_from_plc: PLC tag'leri / DB elemanlarından (DB.* destekli) HMI tag'lerini TIA'nın adlandırmasıyla (DB_Uye) oluşturur, aynı PLC değerine bağlı mevcut HMI tag'lerini yeniden kullanır, istenirse çıkış IO alanlı ekran üretir. Yalnız doğrulanmış tipler: Bool, Int, Real. dry_run destekli.
- hmi_import: Coding'i olmayan Real HMI tag'i reddedilir (17.11 çökmesi).

## 0.11.1 — 2026-11-17
- hmi_import (tag_table): HMI veri tipi ön kontrolü. Basic panelde (6AV2123/6AV6647/6AV6648) yalnız Bool, SInt, USInt, Int, UInt, DInt, UDInt, Real, String güvenli; diğer tipler panelin mevcut tag'lerinde kullanılmışsa kabul edilir, yoksa içe aktarma TIA'ya gönderilmeden reddedilir (16.11 Word çökmesi).

## 0.11.0 — 16.11.2026

- `lib_list`, `lib_copy_block`: global/proje kütüphanelerinde tip ve master copy arama, PLC'ye blok kopyalama (SINA_SPEED gibi).
- `plc_system_constants`: donanım kimliklerini (HW ID) listeler; SINA_SPEED'in `HWIDSTW`/`HWIDZSW` girişleri için.

## 0.10.3 — 16.11.2026

- `hmi_import`: sembolik HMI tag'i entegre olmayan bir bağlantıya bağlanmak istenirse içe aktarma TIA'ya gönderilmez (TIA'yı çökertiyordu). Entegre bağlantı Openness V18 ile oluşturulamıyor; ağ görünümünde elle kurulur.

## 0.10.2 — 16.11.2026

- TIA Portal kapanır ya da çökerse sunucu eski bağlantıyı bırakır; `tia_connect` yeni örneğe bağlanır.
- `hmi_import`: HMI tag'lerinin bağlandığı PLC tag'leri/DB'leri önceden kontrol edilir, eksik varsa TIA'ya gönderilmez (başarısız içe aktarma TIA'yı çökertiyordu). Varsayılan olarak içe aktarmadan önce proje kaydedilir.

## 0.10.0 — 16.11.2026

- `project_summary`: büyük projeler için istasyon başına tek satırlık özet.
- `to_list`, `to_get`: teknoloji nesneleri (eksenler) ve sürücüye donanım bağlantıları.
- `hmi_tree`, `hmi_export`, `hmi_import`: HMI bağlantıları, tag table'ları, ekranlar, metin listeleri (SimaticML).
- Gerçek TIA'da henüz denenmedi.

## 0.9.0 — 13.11.2026

- `block_export` / `block_import`: blokları SimaticML XML olarak okur ve yazar (LAD için).
- Gerçek TIA'da henüz denenmedi.

## 0.8.0 — 13.11.2026

- `network_connect`: `bus: profibus` ile PROFIBUS subnet'i, PROFIBUS adresi ve DP master sistemi. Birleşik MPI/DP arayüzü PROFIBUS'a çevrilir.
- `io_address_set`: bir kartın giriş/çıkış başlangıç adresini şemaya göre ayarlar.
- `module_plug`: `into` parametresi ve iç içe öğelerde arama (GSD sürücünün telegram alt modülü için).
- `drive_telegram`: telegramın giriş/çıkış adres aralığını taşıma.
- `gsd_drive_telegram`: yalnızca GSD olarak eklenebilen sürücülere (G120X) toplu halde telegram alt modülü takar.
- `project_tree`: PROFIBUS arayüzleri de listelenir.
- Gerçek TIA'da henüz denenmedi.

## 0.7.0 — 13.11.2026

- `tags_add`: tag başına isteğe bağlı yorum. Aynı adreste var olan tag'e yalnızca yorum uygulanır.
- `project_tree`: her kartın giriş/çıkış adres aralıkları (`io_addresses`), kompakt CPU'ların dahili kanalları dahil.
- Yeni senaryo: `scenarios/eplan-tag-table` (elektrik projesinden donanım ve tag table).
- Gerçek TIA'da henüz denenmedi.

## 0.6.0 — 12.11.2026

- Katalogdaki yer tutuculu MLFB'ler (`6SL3210-1PE22-7ULx`) tam MLFB ile eşleşir.
- `module_plug` hem rayı hem baş modülleri dener; reddedilen konumdan sonrakine geçer (G120 güç modülü için).
- Kaynak kod alanlara göre dosyalara bölündü (`Tia.*.cs`).
- Proje belgeleri eklendi (`docs/`, `examples/`).
- Gerçek TIA'da henüz denenmedi.

## 0.5.0 — 12.11.2026

- `scenario_list` ve `scenario_get`: `knowledge/` klasöründeki varsayılanlar ve senaryolar Claude'a sunulur.
- `drive_motor`, sürücünün istemediği alanları atlar ve ayrıca raporlar.

## 0.4.0 — 12.11.2026

- `drive_motor`: motor plaka verisi, Startdrive devreye alma sihirbazının arayüzü üzerinden yazılır.
- `drive_parameter_list`: erişilebilir sürücü parametrelerini aralıkla listeler.

## 0.3.0 — 12.11.2026

- `drive_parameters`: sürücü parametrelerini okur ve yazar.
- `project_close`: kaydet ve kapat seçeneği.
- Panellerin `HMI_1` gibi adlarla bulunamaması düzeltildi.

## 0.2.0 — 12.11.2026

- PLC'ler varsayılan olarak şifresiz eklenir.
- `network_connect`: subnet, IP, PROFINET IO sistemi.
- `drive_telegram`: SINAMICS ana telegramı.
- Araç parametresi `device` yerine `station` (bağlantı katmanı `device` adını kendine ayırıyor).

## 0.1.0 — 12.11.2026

- İlk sürüm: bağlantı, proje, katalog, cihaz ve modül ekleme, tag, SCL, derleme, PLC koruma ayarları.
- V18 API klasörünün yanlış seçilmesi (V15.1) düzeltildi.
