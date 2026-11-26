# Durum ve yol haritası — StartWise

Son güncelleme: 17.11.2026, sürüm 0.18.0 (eski adı TIA Assistant).

Ortam: TIA Portal V18 Upd6 (STEP 7 Prof + Safety, WinCC Prof/Comfort/Basic), Startdrive Advanced V18 SP2 Upd1. Unified kurulu değil.
Test projeleri: `test_MCP`, `Et200_eplan`. Gerçek proje `manisa_V18` yalnızca okunur.

## Gerçek TIA V18'de doğrulananlar

| Alan | Yapılabilen | Doğrulandığı yer |
|---|---|---|
| Oturum ve proje | Bağlanma, oluşturma, açma, kaydetme, kapatma; TIA kapanırsa/çökerse oturumu bırakma; kullanıcı proje değiştirirse senkron | test_MCP, Et200_eplan |
| Donanım | MLFB ile CPU, ET 200 (PN ve PROFIBUS), sürücü, panel; varsayılan en yeni firmware, ilk boş slot, şifresiz PLC; modül takma (G120 güç modülü dahil) | test_MCP, Et200_eplan |
| Ağ | PROFINET subnet, IP'yi TIA verir, IO sistemi; PROFIBUS DP master sistemi; I/O adres ayarı | test_MCP, Et200_eplan |
| Sürücüler | G120C/G120 telegram (20 vb.), GSD sürücü telegramı, motor plaka verisi (sihirbaz arayüzü), parametre okuma/yazma | test_MCP |
| PLC tag/blok | Tag table + yorum, SCL kaynaktan FB/FC/DB/OB/UDT, SCL kaynağını okuma (`block_source`), SimaticML ile LAD export/import, sistem sabitleri (HW ID), derleme | test_MCP |
| Kütüphane | Global kütüphaneden blok kopyalama (DriveLib SINA_SPEED) | test_MCP |
| Senaryo | Start → 10 s → SINA_SPEED ile Drive_1 çalışır, geri bildirim; telegram 20'den akım/tork/güç ölçekleme | test_MCP FC_Senaryo |
| PLC alarmları | Program_Alarm FB'si (S7-1200 dahil), instance alarm metinlerini yazma/okuma | test_MCP FB_Drive_Alarm |
| HMI tag | PLC DB/tag'den HMI tag'i (TIA adlandırması DB_Uye), mevcut tag'i yeniden kullanma; Bool/Int/Real | KTP700 Basic, TP900 Comfort |
| HMI ekran | Değer alanları, Bool daire göstergeleri, ActivateScreen navigasyonu, şablon ataması | TP900 Comfort |
| HMI kütüphane | Ekranı master copy yapma ve ondan ekran üretme (Alarm view korunur) | TP900 Comfort |
| Faceplate | Proje kütüphanesindeki tipin instance'larını ekrana koyma ve arayüzlerini bağlama | TP900 Comfort, MotorFP V 0.0.5 |
| Openness keşfi | api_search/api_type (API + HW öznitelik kılavuzu), object_inspect/set/invoke/create — build gerektirmeden yeni işlem | test_MCP (clock/system memory, watch table, TO, AI kanal) |
| Motion | S210 → TO_PositioningAxis (sürüm CPU FW'den), telegram bağlantısı, IRT port topolojisi, S210_AbsCnt FB (MC blokları) | test_MCP Servo_1/Servo_2 |
| Sürücü hazırlama | `drive_prepare`: servo/asenkron tek çağrıda senaryoya hazır; arayüz = instance DB; LAD FC "Servo Motors"/"Asenkron Motors" + OB1 | test_MCP 2 servo + 6 asenkron |
| Saha ekipmanı | `equipment_prepare` + knowledge/equipment/<tür>: valve (Valve_SA, FC "Valve Blocks"), analog (Analog_Meas, FC "Analog Measurements"); params = DB başlangıç değerleri | test_MCP V3300–V3340, TT3300, PT3310 |
| Analog kart | 6ES7 531-7KF00-0AB0 ekleme, kanal 2 telli 4..20 mA (ParameterSettings → OperatingType) | test_MCP AI_1 |

## Openness API kullanımı (17.11.2026 ölçümü)

Derlenmiş exe'nin metadata'sından sayıldı (tahmin değil):

- **41 MCP aracı**
- **161 Openness tipi**, **290 üye** (109 metot + 181 özellik okuma/yazma) doğrudan çağrılıyor
- V18 `Siemens.Engineering.dll` herkese açık yüzeyi: ~1.445 tip, ~5.086 metot/özellik. Kaba oran: tiplerin ~%11'i, üyelerin ~%6'sı
- Bunlara ek olarak SimaticML XML üzerinden 25 HMI nesne türü (IOField, TextField, Circle, Button, FaceplateInstance, ScreenTemplate bağlantısı, animasyon ve olaylar vb.) ve PLC alarm metni xlsx biçimi işleniyor

Oran düşük görünüyor çünkü API'nin büyük kısmı bizim kapsamımız dışındaki alanlarda (Unified, Safety, DCC, sürüm kontrolü, online işlemler, MC detayları). Mühendislik akışının günlük adımları kapsanıyor.

## Openness'in izin vermedikleri (V18) ve çözümler

| Kısıt | Çözüm |
|---|---|
| Entegre HMI bağlantısı oluşturulamaz/listelenemez | Elle sürükle-bırak; adı Devices & networks → Connections'tan okunur |
| Klasik panelde Alarm view XML'e çıkmaz | Ekranı master copy yap, ondan üret |
| Ekran adı/numarası salt okunur | Kalıp ekran istenen adı taşır |
| Faceplate tipi oluşturulamaz; tip export'undan arayüz okunamıyor | Tip TIA'da tasarlanır; ilk instance elle konur, arayüz ondan doğrulanır |
| Klasik HMI discrete/analog alarmları API'de yok | PLC alarmları (Program_Alarm) kullanılır |
| Hatalı HMI import'u TIA'yı çökertebilir | `hmi_import` ön kontrolleri: PLC referansı, bağlantı türü, panel veri tipi, Real Coding |

## Yol haritası

1. Diğer saha ekipmanları (knowledge/equipment altına yeni tür: ör. çift etkili vana, motor (DOL/yıldız-üçgen), analog çıkış, PID, seviye/akış)
2. Kurulum paketi (kapalı, arkadaş PC'leri için), sonra LinkedIn videosu
3. V21 desteği (Bilal kuruyor): API klasör keşfi, sürüm başına build, API farkı, trace yeniden kontrol
4. HMI derlemesi aracı (compile şu an HMI yazılımını derlemiyor); ekipman başına HMI faceplate/ekran
5. DriveLib global kütüphanesinin otomatik açılması
6. Ekipman listesinden tam proje: donanım → ağ → sürücü/ekipman → tag → senaryo → HMI tek akış
4. WinCC Unified (kurulunca), Word/DWord HMI tipleri, ekran silme
5. Bilinen eksik: `drive_parameters` vb. bazı araçlar G120 dışı sürücülerde denenmedi
