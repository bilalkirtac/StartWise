# Devir notu — StartWise (eski adı TIA Assistant), 17.11.2026, sürüm 0.18.0

Bu not, işi devralan (insan ya da model) için yazıldı. Önce bunu, sonra `DURUM.md` (17.11 itibarıyla güncel), `KARARLAR.md`, `OPENNESS-NOTLARI.md` ve `knowledge/` klasörünü oku.

## Çalışma düzeni

- Claude bu bilgisayarda komut çalıştıramaz; dosya okur/yazar. Derlemeyi Bilal `build.cmd` ile yapar, sonra Claude'u sistem tepsisinden Quit edip açar.
- Kod C# 5 sözdizimiyle yazılır (Windows'un kendi derleyicisi). Göndermeden önce V18 `Siemens.Engineering.dll` dosyasına karşı derlenerek doğrulanır.
- Bir API çağrısı tahmin edilmez: imzası `PublicAPI\V18\Siemens.Engineering.xml` içinden okunur, sonra gerçek TIA'da denenir.
- Araç parametresi `station` kullanılır; köprü `device` adını yutuyor (çağrılarda ikisini birden vermek zararsız).
- Büyük projelerde önce `project_summary`. `project_tree`, `block_export`, `hmi_export` çıktıları yüz binlerce karakter olabilir; dosyaya düşer, yalnızca dar bir özet okunmalı. Bilal'in kullanım limiti sınırlı: az çağrı, kısa çıktı.
- Bilal'in gerçek projeleri (`manisa_V18`) yalnızca okunur. Denemeler `test_MCP` ve `Et200_eplan` içinde yapılır.

## 0.10.x ile gelenler (16.11.2026)

Gerçek TIA'da çalıştı: `project_tree` (büyük projede), `block_export`, `to_get`, `hmi_tree`, `hmi_export`, proje değişimini takip (0.10.1).
Denenmedi: `project_summary`, `to_list`. `hmi_import` denendi, aşağıdaki nedenle başarısız oldu.

## manisa_V18'den öğrenilenler (referans proje, salt okunur)

- 2 × CPU 1517T, 37 S210, 8 S120, 8 G120 CU250S-2, 68 G120C (GSD, Telegram 20), 2 × TP1200 Comfort.
- Motion kalıbı 1 (S210): Telegram 105 → sürücüyle aynı adlı `TO_PositioningAxis` V5.0 → Bilal'in FB'si (`S210_ABSOLUTE_KONTROL`, eksen InOut `SRC_GRS`) içinde MC_RESET / MC_POWER / MC_HOME (Mode 7) / MC_MOVEABSOLUTE / MC_MOVEJOG. Eksen sürücüye telegram adresiyle bağlı (aktör bit adresi = telegram bayt adresi × 8; enkoder aynı telegramda).
- Motion kalıbı 2 (S120): `SINA_POS`, `HWIDSTW`/`HWIDZSW` girişlerine telegram alt modülünün sistem sabiti (`SRPD11~PROFINET_interface~Drive_axis_1_...`, Access Scope `GlobalConstant`); değerler `SINA_PARA_S` ile parametre 2617/2618'e.
- HMI: tek tag table, 1016 tag, hepsi sembolik (`AddressAccessMode` Symbolic, adres yok). Bir tag dört bağdan oluşur: `AcquisitionCycle` (1 s), `Connection` (`HMI_Connection_1`), `ControllerTag` (PLC yolu, örn. `RULO5.SURUCU_CALISIYOR` veya `MANUELLER."cprz1 rulo2 man"`), `DataType`/`HmiDataType`. HMI tag adı = PLC yolu, nokta yerine alt çizgi.

## Açık sorun: HMI bağlantısı

- `manisa_V18`'de panelin `Connections` listesi boş döndü ama tag'ler `HMI_Connection_1` kullanıyor: ağ görünümünde kurulan entegre bağlantı bu API listesinde görünmüyor.
- `test_MCP`'de Bilal'in elle açtığı `Connection_1` entegre değil: sürücüsü `SMART_S7_1200_OMS`, adresler elle (panel 192.168.0.2, PLC 192.168.0.1). PLC ise CPU 1511.
- Bu bağlantıyla sembolik tag içe aktarma denemesi "The controller tag Motor1_Start was not found" ile reddedildi (PLC tag'leri `HMI_Test` tablosunda var). En olası neden bağlantının entegre olmaması; doğrulanmadı.
- Sıradaki iş: entegre HMI bağlantısının Openness ile nasıl kurulduğunu bulmak (ağ görünümü / `Connection` ad alanı), ya da ağ görünümünde elle kurulmuş bir bağlantıyla içe aktarmayı tekrar denemek. Sonra: boş başlangıç ekranı şablonu (`Screen_1` dışa aktarılıp okunmadı), panel eklenince otomatik ekran + bağlantı.
- İçe aktarma hatası `NonRecoverableException` verdi; sonraki çağrıdan önce `tia_connect` gerekebilir.

## Güncelleme 16.11 öğleden sonra (0.10.2) — HMI tag'leri çalışıyor

- Çözüldü: HMI tag table `test_MCP` / `HMI_1` içinde `TIA_Assistant_Test` adıyla oluşturuldu (3 sembolik tag), proje kaydedildi.
- Önceki hatanın nedeni bağlantı değil, PLC tag'lerinin yokluğuydu: kaydedilmemiş `HMI_Test` tablosu bir TIA çökmesinde kaybolmuştu.
- Başarısız HMI tag içe aktarması (`NonRecoverableException`) TIA Portal'ı çökertiyor. 0.10.2: `hmi_import` önce her `ControllerTag`'i PLC'lerde arar, eksikse TIA'ya göndermez; varsayılan olarak önce projeyi kaydeder. TIA çökünce sunucu eski bağlantıyı bırakır.
- Bağlantı: Bilal ağ görünümünde entegre HMI bağlantısı kurdu; TIA adı `HMI_Connection_1`. `hmi_tree` bağlantı listesinde görünmüyor ama tag'ler bu adla bağlanıyor. Openness ile entegre bağlantının nasıl kurulacağı hâlâ bilinmiyor.
- İçe aktarma için gereken en küçük tag XML'i: `AttributeList` içinde `AddressAccessMode`=Symbolic ve `Name`; `LinkList` içinde `AcquisitionCycle`, `Connection`, `ControllerTag`, `DataType`, `HmiDataType`. Diğer öznitelikler varsayılana düşüyor.
- Denenmedi: DB üyesine bağlı HMI tag'i (`DB.Uye`), ekran içe aktarma, entegre bağlantıyı API ile kurma.

## test_MCP'nin şu anki hali (sabah; güncel hali yukarıda)

Kaydedilmedi. Bu oturumda eklenen: PLC'de `HMI_Test` tablosu (3 tag). Bilal'in eklediği: `Connection_1`, `Screen_1`. HMI tag table oluşturulamadı.

## Et200_eplan

13.11'de OB1 düzeltmeleri ve `FC_Senaryo_Suruculer` eklendi; o sırada kaydedilmemişti, son durumu Bilal'e sor.

## Yol haritası (Bilal'in hedefi: Openness'ta ne varsa araçta da olsun)

1. HMI: bağlantı, tag table, başlangıç ekranı, şablondan ekran.
2. Motion: eksen nesnesi oluşturma (`TechnologicalInstanceDBComposition.Create(ad, tip, sürüm)`) ve sürücüye bağlama (`AxisHardwareConnectionProvider.ActorInterface.Connect(...)`), sonra FB çağrısı.
3. Ekipman katmanı üretimi (her sürücü için blok çağrısı + instance DB) ve SCL senaryoları; LAD için `block_export` → düzenle → `block_import` kalıbı çalışıyor.
4. Kütüphaneler (master copy), `docs/` için yetenek haritası, senaryo dosyalarının güncellenmesi (PROFIBUS, GSD sürücü, LAD/SCL henüz `knowledge/` altında yazılı değil).

## Güncelleme 16.11 akşam (0.11.0) — SINA_SPEED senaryosu çalışıyor

- `test_MCP` / `PLC_1` (CPU 1511): DriveLib'den SINA_SPEED (master copy `/03_SINAMICS/SINA_SPEED`, FB 285) `lib_copy_block` ile kopyalandı.
- `FC_Senaryo` (SCL): `Motor1_Start` 10 s kesintisiz gelince (IEC_TIMER `DB_Drive_1_Gecikme`) `Drive_1` (`DB_Drive_1`) çalışır, hız `INT_TO_REAL(Motor1_Hiz)`, `Motor1_Calisiyor := DB_Drive_1.AxisEnabled`. HWIDSTW/HWIDZSW = `"Drive_1~PROFINET_interface~Standard_telegram_20"` (`plc_system_constants` ile okundu).
- OB1 (boştu) LAD network ile `FC_Senaryo`'yu çağırıyor. Donanım + yazılım 0 hata, proje kaydedildi.
- DriveLib: `C:\Users\bilal\Documents\Automation\Drive_Lib_S7_1200_1500_V18\Drive_Lib_S7_1200_1500_V18.al18`. TIA çökünce kapanıyor; Bilal'in isteği: proje açılınca/oluşturulunca otomatik açılsın (yapılmadı; `GlobalLibraries.Open(FileInfo, OpenMode.ReadOnly)` ve yolu `knowledge/defaults.md`'ye yazmak).
- Entegre olmayan bağlantı (XML ile oluşturulan `PLC_1_Baglanti`, `HMI_2`) ağ görünümünde HMI connection olarak görünüyor ama sembolik tag taşımıyor; 0.10.3 bunu içe aktarmadan önce engelliyor.
- 16.11 son: `DB_Drive_1_Durum` (durum + akım/tork/güç, telegram 20 PZD3-6, tag tablosu `Drive_1_Telegram` IW268-IW274) ve güncel `FC_Senaryo` eklendi, derlendi ve `hmi_import` öncesi kaydedildi. Offline referanslar varsayılan (p2002=100 A, p2003=1 Nm, r2004=0); DB'de `Ref_*` parametresi olarak duruyor.
- HMI tag içe aktarma TIA'yı 3. kez çökertti: KTP700 Basic'te HMI veri tipi `Word` yok ("The HMI data type Word was not found"). Comfort panelde (manisa) var. Basic panelde Word yerine UInt kullan. `hmi_import` ön kontrolüne HMI veri tipi kontrolü eklenmeli (panelin kabul ettiği tipler öğrenilmeli).
- HMI ekranı: IO alanı / metin alanı içeren bir ekranın XML örneği yok; Bilal'den `Screen_1`'e bir IO alanı koymasını isteyip dışa aktararak öğrenilecek.

## 17.11 sabah — çökme analizi (v0.11.1)
- 16.11'deki 3 TIA çökmesinin üçü de HMI tag tablosu Import'unda oldu: (1) olmayan PLC tag'i, (2) entegre olmayan bağlantıda sembolik tag, (3) panelde olmayan HMI veri tipi (KTP700 Basic'te Word). 0.10.2, 0.10.3 ve 0.11.1 ön kontrolleri bunları TIA'ya göndermeden reddediyor.
- Kural: HMI tag'inde PLC DataType aynen kalır; Basic panelde HmiDataType Word->UInt, DWord->UDInt, Byte->USInt.
- 4. çökme (17.11): Real HMI tag'i Coding alanı olmadan içe aktarıldı → TIA varsayılan Binary'yi uyguladı, Real için salt okunur → çöktü. Kural: HMI tag XML'i daima TIA'nın kendi export'undan (tam AttributeList) üretilir. Real: Coding=IEEE754Float, Length=4; Bool: Binary/1; Int: Binary/2.
- TIA UI'da IO alanı doğrudan PLC DB elemanına bağlanınca HMI tag'ini kendisi üretir: Default tag table, ad = DB_Uye (nokta→alt çizgi). Openness'te ekran yalnız HMI tag adına bağlanır; bu yüzden aynı adlandırmayla HMI tag'leri önce biz oluşturuyoruz.
- IO alanı bağlantısı (ekran XML): IOField/ObjectList içinde Hmi.Screen.Property Name=ProcessValue → Hmi.Dynamic.TagConnectionDynamic (Indirect=false) → LinkList/Tag Name=<HMI tag>.
- Başarılı: HMI_1'de tag tablosu Drive_1_Durum (Calisiyor/Hata/Kilit Bool, Hiz_rpm/Tork_Nm/Guc_kW Real) + ekran Drive_1_Durum (No 2; hız/akım/tork/güç Output IO alanları).
- TODO: Bool durum göstergeleri (kalıp gerekli), Screen_1'den Drive_1_Durum'a geçiş butonu, Status/Namur (Word→UInt) tag'leri, hmi_import ön kontrolüne "Real/LReal tag'de Coding eksikse reddet" kuralı, HMI tag üretimini tag_table XML yerine tool'a almak (hmi_tags_from_plc).

## 17.11 öğle — alarm ve kütüphane (v0.12.0–0.14.0)
- PLC alarmları: FB_Drive_Alarm (2× Program_Alarm, S7-1200'de derleniyor) + DB_Drive_1_Alarm; FC_Senaryo 0.3'te çağrılıyor. Metinler plc_alarm_texts ile yazıldı (PlcAlarmTextProvider xlsx export/import).
- V18 Openness klasik panellerde Alarm view'ı ekran XML'ine çıkarmıyor (KTP700 Basic ve TP900 Comfort'ta doğrulandı). Çözüm: ekranı master copy yap (hmi_screen_to_library), yeni ekranları ondan üret (hmi_screen_from_library).
- HMI_3 (TP900 Comfort, 192.168.0.9) eklendi. Bilal: network_connect sonrası HMI connection Devices&networks'te otomatik görünüyor — doğrulanacak (HMI_3'e tag importu ile).
- Screen.Name API'de salt okunur; yeniden adlandırma SetAttribute("Name") ile deneniyor.
- 17.11 11:30: HMI bağlantısı OTOMATİK DEĞİL — sürükle-bırak gerekli (Bilal doğruladı; HMI_3 → HMI_Connection_3). hmi_tags_from_plc HMI_3'te 7 tag + Drive_1_Durum ekranı (No 3) oluşturdu, sorunsuz. Kalıptan oluşan Screen_2'de Alarm view var. Senaryo: knowledge/scenarios/hmi-ekle-ve-alarm.md
- HMI_1'deki fazla bağlantılar Bilal'in yanlışlıkla oluşturduğu; HMI_Connection_2 = HMI_2_Station, _3 = HMI_3_Station.
- 17.11 14:00: Faceplate instance XML'e çıkıyor: Hmi.Screen.FaceplateInstance (FaceplateTypeName "MotorFP V 0.0.4") + InterfacePropertySimple Name=Process_value → TagConnectionDynamic/Tag. hmi_faceplate_screen (v0.16.0) bunu üretiyor; arayüz adlarını tip versiyonunun export'undan doğruluyor (doğrulama TIA'da henüz denenmedi).
- Template_1 (Back=ActivatePreviousScreen, Main, Next) XML'e çıkıyor; ekrana şablon atamasının XML karşılığı henüz görülmedi.
- v0.15.0: hmi_tags_from_plc Bool → daire gösterge, nav butonları (TIA'da denenmedi).
- 17.11 14:05: Ekrana şablon ataması XML: Screen AttributeList'ten sonra <LinkList><Template TargetID="@OpenLink"><Name>Template_1</Name></Template></LinkList>. v0.17.0 'template' parametresi. Faceplate tip export'undan arayüz okunamadı (format bilinmiyor) → 0.16.1 mevcut instance'lardan okuyor.
- 17.11 14:03: hmi_faceplate_screen gerçek testte çalıştı: HMI_3 'Motorlar' (No 4), Template_1 atanmış, Drive_1 MotorFP instance'ı Process_value→DB_Drive_1_Durum_Akim_A. Çökme yok.
- 17.11 15:20: MotorFP V 0.0.5 (arayüz: Process_value, bool — Bilal Bool'u IO alanıyla çözdü). HMI_3'te gerçek testler başarılı: Motorlar_2 (No 6, faceplate akım+çalışıyor, Template_1) ve Drive_1_Ozet (No 7, hmi_tags_from_plc: 4 değer + 3 Bool daire + nav butonları + Template_1). Çökme yok. 0.17.1: şablon varken nav butonları sağ üste.
- Test ekranları (Motorlar, Motorlar_2, Drive_1_Ozet, Screen_3) Bilal'in kararıyla silinebilir.


## 17.11.2026 özeti (0.11 → 0.18)

- Çökme analizi: 16–17.11'deki 4 TIA çökmesinin hepsi HMI tag import'unda; hepsi artık `hmi_import` ön kontrolüyle engelleniyor (PLC referansı, bağlantı türü, panel veri tipi, Real Coding).
- Yeni araçlar: hmi_tags_from_plc, plc_alarm_texts, block_source, hmi_screen_to_library, hmi_screen_from_library, hmi_faceplate_screen, project_archive.
- HMI akışı (knowledge/scenarios/hmi-ekle-ve-alarm.md): bağlantı elle sürüklenir; tag + durum ekranı + Bool gösterge + navigasyon + şablon + PLC alarmları + alarm sayfası (master copy) + faceplate instance'ları otomatik.
- İsim değişikliği 0.18.0: config anahtarı/sunucu `startwise`; exe adı `bin\TiaAssistant.exe` kaldı (StartWise.exe Norton'a takıldı: Heur.AdvML.B yanlış alarmı; bin Norton'da istisna). Bin'de 0 baytlık kilitli StartWise.exe kalıntısı zararsız. Claude ayarında "tia-assistant" girdisi "startwise" ile değiştirilecek (docs/KURULUM.md). C# namespace ve depo klasörü bilerek değiştirilmedi.
- Yedekler: kod `C:\Users\bilal\source\repos\_yedek\tia-assistant_v0.17.1_20261117_1525.zip`; TIA projesi project_archive ile `Documents\Automation\Yedekler`.
- Openness ölçümü: 41 araç, 161 tip, 290 üye (DURUM.md).
- TIA proje yedeği: C:\Users\bilal\Documents\Automation\Yedekler\test_MCP_20261117_1550.zap18 (24 MB, project_archive ile). StartWise 0.18.0 Windows'ta çalışıyor (exe bu sefer bulutta mcs ile derlenip kondu; sonraki derlemeler build.cmd ile).
- 17.11 16:30 — Sürüm desteği planı: hedef V18–V21 (V21 en son). Bilal V19–V21'i aynı PC'ye yan yana kuracak (önce V21). Plan: sürüm başına derleme (aynı kaynak, her sürümün PublicAPI\Vxx DLL'i), sürüm farkları için küçük koşullu bölümler, açık TIA sürümüne göre exe seçen başlatıcı, her sürümde ana akış testi. Program.cs API dizini şu an yalnız registry "18." alt anahtarını okuyor → genelleştirilecek.

## 18.11.2026 — kendi kendine öğrenme planı (0.19.0)
- 1. katman (yapıldı): api_search / api_type — API haritası + donanım özellik dizini. Bulutta mono ile çevrimdışı test edildi; Windows'ta TIA iç kütüphaneleri olduğu için metot imzaları yansımadan tam gelecek (bulutta belge kimliğinden parametre listesi).
- 2. katman (sırada): canlı nesne gezgini, salt okunur (GetAttributeInfos / GetCompositionInfos / GetAttributes).
- 3. katman (sırada): gösterimle öğrenme — önce/sonra anlık görüntü + fark (gerektiğinde Siemens Compare API).
- PublicAPI\V18\Schemas: yalnız PLC tarafı XSD'leri (blok arayüzü, LAD/FBD/SCL/STL, TO). HMI şeması yok. block_import öncesi XSD doğrulaması için kullanılabilir.
- Dağıtım dersi: device_commit_files bazen eski kopyayı yazıyor → gönderimden sonra md5 karşılaştırması yapılır (boyut/kelime kontrolü yetmez).
- 18.11 10:30: Kendi kendine öğrenme döngüsünün ilk gerçek işi: Drive_1_Izleme watch tablosu (12 satır, yorumlu) — api_search → object_inspect → object_create/Export/Import, gösterimsiz. Trace V18 Openness'te yok. Senaryo: knowledge/scenarios/watch-table.md. Test tablosu SW_Deneme projede duruyor (silme onaya bağlı).
- 18.11 10:45: S210 (Servo_1) + TO_PositioningAxis V6.0 tamamen genel araçlarla kuruldu ve derlendi (0 hata). Takılmalar ve çözümleri: TO sürümü Version tipi (0.22.1), TO sürümü CPU FW'ye bağlı, telegram bit adresiyle bağlantı, IRT için port topolojisi. Senaryo: knowledge/scenarios/servo-s210-to.md.
- 18.11 11:15: Uçtan uca sürücü hazırlama — servo yolu Servo_1'de tamam: Servo_1_IO arayüz DB, "Servo Motors" LAD FC (üreteç: knowledge/generators/lad_drive_fc.py), OB1 ağ 4 → "Servo Motors" çağrısı. Derleme 0 hata. Asenkron yolu tasarlandı, uygulanmadı. Senaryo: knowledge/scenarios/surucu-ekle-uctan-uca.md. Kararlar: arayüz DB, hat topolojisi.
- 18.11 11:25: Asenkron yolu tamam: UDT SINA_SPEED_IO, Drive_1..5_IO, SINA_SPEED_Drive_1..5, "Asenkron Motors" LAD FC (5 ağ, HWID GlobalConstant), OB1 ağ 5. FC_Senaryo 0.4 Drive_1_IO üzerinden. compile all 0/0. Sürücü senaryosu: knowledge/scenarios/surucu-ekle-uctan-uca.md (servo + asenkron). DB_Drive_1 kullanılmıyor.


## 18.11.2026 öğlen — drive_prepare
- Yeni araç drive_prepare (0.23.0 → 0.24.1). Servo_2 (S210 5 kW, katalogda 4 kW yok) ve Drive_6 (G120 CU240E-2 PN + PM240-2 2.2 kW U) eklendi, tek çağrıyla hazırlandı.
- Bilal'in önerisiyle <sürücü>_IO DB'leri kaldırıldı: arayüz = instance DB, ağda pinler boş (OpenCon). 8 sürücünün ağları yerinde yeniden yazıldı, derleme 0 hata, proje kaydedildi.
- Silme onayı bekleyen (test_MCP): Servo_1_IO, Servo_2_IO, Drive_2..6_IO, UDT S210_IO, UDT SINA_SPEED_IO, SW_Test_B, SW_Deneme (watch table), DB_Drive_1.
  Drive_1_IO'yu FC_Senaryo hâlâ kullanıyor → önce FC_Senaryo "SINA_SPEED_Drive_1"e çevrilmeli.
- Sıradaki: kapalı kurulum paketi; video cuma.
- 12:30 Bilal onayıyla silindi: Servo_1_IO, Servo_2_IO, Drive_2..6_IO, DB_Drive_1, UDT S210_IO (object_invoke Delete). Derleme 0/0, kaydedildi.
  Kalan: Drive_1_IO + UDT SINA_SPEED_IO (FC_Senaryo kullanıyor), SW_Test_B (FC), SW_Deneme (watch table).
- 12:35 FC_Senaryo 0.5: Drive_1_IO yerine "SINA_SPEED_Drive_1" (girişler yazılır, çıkışlar okunur). Bilal onayıyla silindi: Drive_1_IO, UDT SINA_SPEED_IO, SW_Test_B, SW_Deneme.
  PLC hardware+software derleme 0/0; HMI_1/HMI_3 etiketleri silinenlere bağlı değil (inspect ile bakıldı). Kaydedildi. test_MCP'de bekleyen silme kalmadı.
- Eksik bulundu: compile aracı HMI yazılımını derlemiyor ("no compilable PLC software"); object_invoke service: ile arayüz tipi (ICompilable) çözülmüyor. İleride eklenecek.
- 12:50 equipment_prepare (0.25.0) ilk test: valve türü. V3300 (I0.0 Opened, I0.1 Closed, Q0.0 CMD), V3310 (I0.2/I0.3/Q0.1), V3320 (I0.4/I0.5/Q0.2),
  V3330 (I0.6/I0.7/Q0.3), V3340 (I1.0/I1.1/Q0.4). Tag tablosu Valves, LAD FC "Valve Blocks" (OB1 çağırıyor). Valve_SA 0.3: hata çıkışı kapatır (Bilal);
  hata bitleri statik latch'te (çıkış üzerinde latch "might not be initialized" uyarısı veriyordu). Tam derleme 0/0, kaydedildi.
- Not: commit_files ara sıra eski kopyayı yazıyor — her committen sonra md5 kontrolü ŞART, gerekirse ikinci commit.
- Sıradaki: Analog Scale + Measurement türü (knowledge/equipment/analog/), sonra kurulum paketi.
- 13:05 AI_1 (6ES7 531-7KF00-0AB0 V2.2, slot 5, IB4..19) eklendi; kanal 0/1 2 telli 4..20 mA (knowledge/scenarios/analog-kanal-ayari.md).
  equipment_prepare analog: TT3300 (%IW4, 0..150, HH140/H120/L20/LL10, Hyst 2), PT3310 (%IW6, 0..10 bar, 9.5/8.5/1.5/0.5, Hyst 0.2); params ile başlangıç değerleri. Tam derleme 0 hata, kaydedildi.
- object_set sıra sorunu 0.26.1 ile düzeltildi (turlar halinde yazma); build bekliyor.

### 18.11.2026 gün sonu
- Sürüm 0.26.1 (repo); Bilal'in build'i 0.26.0 — 0.26.1 (object_set turlar halinde) build bekliyor.
- Yedekler: Documents\Automation\Yedekler\test_MCP_20261118_1258.zap18; _yedek\tia-assistant_v0.26.1_*.zip.
- test_MCP durumu: PLC_1 (1511-1 V2.9) + DI_1/DI_2/DQ_1/AI_1; Servo_1/2 (S210), Drive_1–6; FC Servo Motors / Asenkron Motors / Valve Blocks / Analog Measurements + FC_Senaryo 0.5; derleme 0 hata.
- Bilal V21 kuruyor. Sonraki oturum: diğer saha ekipmanları → kurulum paketi → video (cuma) → V21.

### 19.11.2026 — V21 Openness kontrolü
- V21'de Openness TIA ile birlikte kuruluyor (ayrı seçenek yok). API: C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48.
- Tek Siemens.Engineering.dll yerine bölünmüş: Siemens.Engineering.Base / Step7 / WinCC / Safety / WinCC.Extension / AddIn.* (+ .xml dokümanları), HW parametre PDF'i ve Schemas aynı yerde.
- Mevcut kod (0.26.1) değiştirilmeden Base+Step7+WinCC+Safety'ye karşı derlendi: tek eksik Siemens.Engineering.MC (Startdrive, 7 hata). Bilal Startdrive V21'i kuruyor; sürücü kitaplığının yeri sonra bulunacak.
- Yapılacak: build.cmd sürüm parametresi (V18/V21 ayrı exe), AssemblyResolve → <ver>\net48, registry "21." anahtarı, Startdrive yoksa sürücü kodu ayrı, ApiIndex için V21 xml'leri.
- 15:50 Startdrive V21 kuruldu: Siemens.Engineering.Startdrive.dll de PublicAPI\V21\net48'de. 0.27.0: build.cmd V21 + sürüme bağlı çalışma anı; iki sürüm de kapta derlendi. Bilal ilk V21 build'ini alacak, config'e "startwise-v21" girdisi eklenecek.
  V21'de denenecek: bağlantı, api_search (xml'ler), HMI/LAD XML importu (<Engineering version="V21">, şemalar), trace, drive_prepare, equipment_prepare. hw-attributes-V21.json henüz yok (V18'inki kullanılıyor).
- 16:05 İLK V21 TESTİ (test_v21.ap21, StartWise 0.27.0 "startwise-v21"): tia_status/tia_connect OK, catalog_find OK, api_search OK (V21 xml'leri).
  device_add S210 (V6.4) ve G120C PN (4.7.14) OK, drive_telegram (G120C 1→20) OK, project_save OK.
  device_add CPU 1516-3 → LicenseNotFoundException "STEP 7 Professional" eksik; TP900 Comfort → "WinCC Comfort" eksik. V21 lisansları ALM'de etkin değil (V18 lisansı V21'i kapsamıyor). Lisans/trial sonrası PLC, HMI, drive_prepare, equipment_prepare, LAD/HMI XML import denenecek.
  S210 V21'de drive_objects=2 gösteriyor (V18'de 1) — drive_prepare index 0 varsayımı kontrol edilmeli.
  Trace: V21 Openness'te de PLC trace API'si yok (yalnız SafetyValidation.TraceConfiguration, HW.TraceTriggerEdge).

### 20.11.2026 — V21 testi devam (lisans sonrası)
- Bilal CPU 1515-2 PN FW V4.1'i elle eklemiş (istasyon "S7-1500/ET200MP station_1"). Ağ + IO sistemi OK. drive_prepare Servo_1: TO_PositioningAxis 8.0, Connect 2048/2048, topoloji, S210_AbsCnt (MC 8.0), FC "Servo Motors", OB1 → derleme 0 hata. V21'de LAD import çalışıyor.
- Drive_1 (G120C): DriveLib V21'de açık değil → SINA_SPEED yok. Bilal V18 DriveLib'i V21'de açmalı (yükseltme) ya da V21 DriveLib.
- HMI: "WinCC Comfort" lisansı hâlâ eksik.
- FW V4.x CPU'lar: UMAC. Donanım derleme hatası "At least one user must have the following runtime right: Full access".
  Openness: project/service:UmacConfigurator → ActivateAnonymousUser() (yapıldı), AnonymousUser.Roles.Add(SystemRoles["PLC administrator"]) — RoleAssociation genel API dışında; 0.27.1 reflection ile çağıracak (build bekliyor). device_add'in 'none' korumasına V4.x için bu adım eklenmeli.
- FwToTo haritası V4.x içermiyor; V4.1 için 8.0 seçildi. Daha yeni TO sürümü var mı kontrol et.
- 09:10 (0.27.1 build) UMAC çözüldü: object_invoke project/service:UmacConfigurator/AnonymousUser/Roles Add(item=SystemRoles[PLC administrator]) → donanım derlemesi 0 hata (uyarı: Anonymous Full access yalnız test içindir). device_add 'none' koruması V4.x için bunu otomatik yapmalı (ActivateAnonymousUser + PLC administrator).
- Drive_Lib_S7_1200_1500_V21 açık → drive_prepare Drive_1 (G120C, telegram 20): SINA_SPEED kopyalandı, HW ID sabiti, FC "Asenkron Motors", OB1 → 0 hata.
- equipment_prepare valve V3300 (I0.0/I0.1/Q0.0, params MonTime T#8S) V21'de 0 hata (uyarı: I/O donanımda yok — DI/DQ kartı eklenmedi).
- SONUÇ: V21'de bağlantı, katalog, ağ, sürücü (servo+asenkron), UMAC, LAD import, SCL import, equipment çalışıyor. Kalan: HMI (WinCC Comfort lisansı), HMI XML import V21'de denenmedi, hw-attributes-V21.json, FwToTo V4.x.
- 09:20 HMI_1 (TP900 Comfort, Bilal ekledi; istasyon adı "HMI_1") PN/IE_1'e bağlandı (192.168.0.4). V21 API: HW.Features.CommunicationManagement servisi HMI_RT_1 ve HMI_1 öğelerinde, Connections.Create<HmiConnection>(Node localInterface, DeviceItem partnerTarget, Node partnerInterface). 0.27.2 build bekliyor → bağlantıyı kur, sonra hmi_tags_from_plc (HMI XML import V21) dene.
- Bilal: vana testi istenmemişti — ekipman (equipment_prepare) yalnız kullanıcı isterse; drive_prepare'in parçası değil. V3300 deneme nesnelerinin silinmesi onay bekliyor.
- 09:25 Bilal: vana kalsın. DI_1 (6ES7 521-1BL00-0AB0 V2.2, slot 2, IB0..3) + DQ_1 (6ES7 522-1BL01-0AB0 V1.1, slot 3, QB0..3) eklendi; V3300 tag'leri (I0.0/I0.1/Q0.0) gerçek kanallara oturdu. Tam derleme 0 hata. Kaydedildi.
- HMI bağlantısı yolları (V21): localInterface = station:HMI_1/DeviceItems[HMI_1.IE_CP_1]/DeviceItems[0]/service:NetworkInterface/Nodes[0];
  partnerTarget = project/Devices[S7-1500/ET200MP station_1]/DeviceItems[PLC_1]; partnerInterface = .../DeviceItems[PLC_1]/DeviceItems[PROFINET interface_1]/service:NetworkInterface/Nodes[0];
  servis: station:HMI_1/DeviceItems[HMI_RT_1]/service:CommunicationManagement/Connections → Create (generic, _T=HW.CommunicationConnections.HmiConnection). Çalışan exe 0.27.1 idi → 0.27.2 build bekliyor.
- Yol tuzağı: istasyon adında "/" varsa "station:" kökü bölünür → project/Devices[<ad>] kullan. item:<ad> büyük/küçük harf duyarsız eşleştiği için "PROFINET interface_1" belirsiz.
- 09:30 (0.27.2) HMI bağlantısı Openness ile KURULDU: CommunicationManagement/Connections.Create<HmiConnection> → HMI_Connection_1, 192.168.0.4↔192.168.0.1, IsValid True.
  AMA HMI yazılımı (HmiTarget.Connections) boş kalıyor; hmi_tags_from_plc importu "Unable to resolve open link ... 'set_ControllerTag' is not supported" ile düşüyor. Kayıt sonrası da boş.
  Denenecek: TIA'da HMI derlemesi / Connections editörünü açmak senkronu tetikliyor mu? Bilal'den kontrol istendi. HMI derleme aracı hâlâ yok (service:ICompilable çözülmüyor).
- 0.27.3: instance DB üyeleri (Input/Output/InOut) HMI'ya bağlanabilir (önce yalnız Static okunuyordu → "not found in any PLC").
- 09:40 V21 HMI bulgusu: Bilal'in elle eklediği HMI tag (SINA_SPEED_Drive_1_ActVelocity) TIA'da PLC'ye bağlı, ama V21 Openness export'unda (WithDefaults ve WithReadOnly) <ControllerTag> YOK; Hmi.Tag.Tag'in Openness'te yalnız Name özniteliği var; HmiTarget.Connections boş (UI'da HMI_Connection_1 görünüyor).
  hmi_tags_from_plc importu "'set_ControllerTag' is not supported by type Hmi.Tag.Tag". Sonraki: 0.28.0 ile HMI derle → Connections/ControllerTag görünüyor mu? Görünmüyorsa V21'de klasik panel tag bağlama Openness ile yapılamıyor olabilir → Siemens V21 Openness "WinCC Comfort" notlarını araştır / Unified panel seçeneği.
- 0.28.0 (V21 ana sürüm, UMAC device_add, HMI compile, yol düzeltmeleri) repoda, Bilal build alacak (build.cmd tek başına = V21).
- 09:35 (0.28.0) compile HMI_1 → 0 hata (HMI derleme aracı çalışıyor). hmi_tags_from_plc dry-run instance DB üyelerini buluyor ("V3300".Opened, "SINA_SPEED_Drive_1".AxisEnabled).
  HMI derlemesi senkronu çözmedi: HmiTarget.Connections boş, import yine "'set_ControllerTag' is not supported".
  DOĞRULAMA (dış kaynak): github.com/bulaofen0036-coder/TIA_Portal_Openness_MCP (MIT, V20+V21, 7.11.2026) aynı bulgu: V21'de bağlantı HW connection API ile kurulur (Classic + Unified), ama
  Classic/Comfort/Basic panellerde XML ile HMI tag için sembolik ControllerTag REDDEDİLİR, yalnız mutlak adres (AddressAccessMode=Absolute + LogicalAddress + Connection) çalışır; uçtan uca HMI için WinCC Unified panel önerilir.
  → Karar Bilal'de: (A) Classic'te mutlak adres (yalnız I/Q/M ve optimize OLMAYAN DB'ler), (B) V21 ana hat için WinCC Unified (Siemens.Engineering.HmiUnified API, nesne tabanlı, XML değil).
- Not (ürün): Aynı alanda açık kaynak (MIT) bir rakip var: "TIA Portal MCP Server" + masaüstü "TIA 助手 (TIA Assist)" aeenhance.com; V20+V21, CLI, S7DCL, online izleme (salt okunur).
- 09:50 Bilal: klasik HMI'da bağlantı kurmak yeterli, bağlamayı kendisi yapıyor. Unified bugün kuruluyor → sıradaki HMI işi Unified. Video ertelenebilir.
  Yapılacak küçük iş: HMI bağlantısını object_invoke yerine tek araç (hmi_connect: panel + PLC adı) haline getirmek.

### 20.11.2026 öğleden sonra — V18 kaldırılıyor
- WinCC Professional/Unified çakışması: WINCC SQL örneği (SQL 2022, MSSQL16) kaldırıldı; "SIMATIC WinCC Common Archiving" (V18 WinCC paketine bağlı paylaşılan bileşen) tek başına kaldırılamadı. Bilal V18'i kaldırıyor; geliştirme yalnız V21'de.
- Sonuçlar: Claude config'teki "startwise" (V18, bin\TiaAssistant.exe) girdisi kaldırılmalı; yalnız "startwise-v21". build.cmd varsayılanı zaten V21. V18 derleme kontrolü kapta (uploads/PublicAPI/V18) yapılabilir ama PC'de V18 build olmaz.
- test_MCP (V18) ve manisa_V18 (salt okunur referans) V21'de açılırken TIA yükseltme ister; yükseltme kopyası ayrı klasöre yazılmalı, manisa_V18 asla yerinde değiştirilmez. Yedekler\*.zap18 V21'de retrieve + upgrade ile açılır.
- Sıradaki: Unified kurulumu → Unified araçları (Siemens.Engineering.HmiUnified), hmi_connect aracı, video (V21 akışı: PLC + drive_prepare + ekipman + HMI bağlantısı).
- 13:40 V18 kaldırılırken hazırlık: 0.29.0 (hmi_connect + hw-attributes-V21.json) repoda, build bekliyor. Unified API (Siemens.Engineering.HmiUnified) Unified kurulunca incelenecek.
- 13:45 .mcpb taslağı hazır (packaging/, pack.cmd). Lisans tasarımı konuşuldu (3 projelik deneme → imzalı lisans dosyası + Supabase'de çevrimiçi etkinleştirme önerisi); henüz kodlanmadı, Bilal tasarım dokümanını onaylayınca.

### 20.11.2026 14:05 — Unified ilk test (0.29.0)
- WinCC Unified kuruldu (V18 kaldırıldı). PublicAPI\V21\net48\Siemens.Engineering.WinCCUnified.dll (+xml) geldi; SiemensAssemblies otomatik yüklüyor → api_search/object_* Unified'ı build'siz görüyor.
- HMI_2 = 6AV2 128-3KB06 (Unified Comfort, FW 21.0), varsayılan IP 192.168.0.2 Servo_1 ile çakışıyordu → network_connect ip 192.168.0.5.
- hmi_connect HMI_2→PLC_1: HMI_Connection_2 OLUŞTU (valid). HMI_1 için "exists". Unified HmiSoftware.Connections bağlantıyı GÖRÜYOR (klasikte boş).
- Unified HMI tag SEMBOLİK BAĞLAMA ÇALIŞIYOR: Software/Tags Create HmiTag(Name, TagTableName) → Connection="HMI_Connection_2", PlcTag="V3300.Opened" → PlcName=PLC_1, DataType/HmiDataType otomatik Bool.
  Not: PlcTag biçimi tırnaksız "DB.Member". object_set Unified nesnelerinde InvalidCastException (Unified tipleri IEngineeringObject değil); geçici: object_invoke set_<Özellik>(value). Kalıcı düzeltme: object_set/create'te reflection ile property set.
- Sıradaki: Unified araçları — hmi_tags_from_plc'nin Unified yolu (HmiTag API, XML değil), ekran + IO field (HmiScreen/HmiScreenItems), alarm view; object_set düzeltmesi.
- 14:30 Drive_1 (G120C) referansları varsayılan (motor verisi yok): p2000 1500 rpm, p2002 100 A, p2003 1 Nm, r2004 0 kW. PLC tag'leri Drive_1_Telegram: Drive_1_Akim_Ham %IW280, Drive_1_Tork_Ham %IW282 (telegram 20 PZD3/4).
  HMI_2 tag'leri (elle API ile): Drive_1_Hiz, Drive_1_Akim (0..16384→0..1000, 0.1 A), Drive_1_Tork (0..16384→0..1000, %0.1), Drive_1_Calisiyor, Drive_1_Hata; ekran Drive_1 (IO_Hiz bağlı, T_Baslik). 0.30.0 hmi_unified_screen build bekliyor → ekranı tamamla, Bilal görsel kontrol.
- 14:58 (0.30.0) hmi_unified_screen HMI_2 / Drive_1_Durum: başlık + 5 satır (hız rpm Real, akım A Int ölçekli 0.1, tork % Int ölçekli 0.1, çalışıyor Bool, hata Bool), mevcut HMI tag'leri yeniden kullanıldı. Kaydedildi.
  compile HMI_2 → "no compilable software": Unified HmiSoftware GetService<ICompilable> vermiyor (Unified tipleri IEngineeringServiceProvider değil?) → Unified derleme yolu araştırılacak. Eski elle test ekranı "Drive_1" (IO_Hiz, T_Baslik konumsuz) duruyor; silme Bilal onayına bağlı.


### 20.11 öğleden sonra - Unified ilk sürüm (0.31.0)
- `hmi_unified_add` test_v21/HMI_2'de çalıştı: Drive_1_Durum'a Start/Stop/Reset butonları, Çalışıyor/Hata göstergeleri, SpeedSp girişi; yeni V3300 ekranı (Oto/Manuel, Aç, Kapat, Reset, 4 gösterge, alarm view) + 3 discrete alarm (Status bit5/bit6 Word, Drive_1_Hata Bool). HMI derlemesinde bu öğelerden hata yok.
- Ders: başlangıç ekranı silinirse derleme hatası verir (RuntimeSettings.StartScreen). Ekran silerken kontrol et.
- Openness'te YOK: Runtime settings > General > "Activate encrypted transfer" (şifre). Panel başına TIA'da elle kapatılır.
- DefaultFont (Language & font) Arial/Tahoma'yı "inconsistency" ile reddediyor. "Siemens Sans not installed" hatası PC'de yazı tipi eksikliği (V18 kaldırılınca gitti olabilir) -> yazı tipini kur / V21 Repair.
- object_set Unified düzeltmesi: proxy üzerinde `as IEngineeringObject` bile InvalidCastException atıyor -> Unified ise cast etmeden .NET property set (bir sonraki build).

- object_set gerçek hatası bulundu ve düzeldi (0.31.2): JSON Dictionary üzerinde Cast<DictionaryEntry>. Trend eğrileri object_set ile renklendi (akım mavi, tork kırmızı).
- Font: "Enable language-compatible font families" açıkken DefaultFont değişebiliyor; Unified araçları Siemens Sans yoksa otomatik Microsoft Sans Serif yapıyor (0.31.1).
- Karar: HMI'da StartWise tag/bağlantı/obje oluşturur, görsel tasarım mühendiste (KARARLAR.md).

### Gün sonu 20.11 - sıradaki işler
1. Video senaryosu ve çekim (ilk sürüm HMI dahil hazır).
2. .mcpb paketini kendi PC'de test (önce config'teki elle yazılmış "startwise-v21" girdisini kaldır; eski V18 "startwise" girdisini de sil).
3. Lisans/deneme tasarım dokümanı (istenirse), kod imzalama sonra.
4. Unified HmiSoftware derlemesi compile aracından yapılamıyor - araştırılacak.
