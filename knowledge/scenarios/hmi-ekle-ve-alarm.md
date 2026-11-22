# Senaryo: HMI ekle, PLC değerlerini göster, alarm sayfası

Doğrulandı: 17.11.2026, TIA V18 Upd6, KTP700 Basic (HMI_1) ve TP900 Comfort (HMI_3), PLC_1.

## 1. HMI ekle
- device_add (MLFB) → network_connect (aynı subnet, IP'yi TIA verir).
- ELLE ADIM: Network view'da HMI'ı PLC'ye sürükle → entegre bağlantı (HMI_Connection_n).
  V18 Openness bunu oluşturamıyor ve listeleyemiyor. Adı Devices & networks → Connections sekmesinden okunur
  (HMI'ı seçerek). Yanlış bağlantı adıyla tag import'u denenmez.

## 2. PLC değerlerini HMI'da göster
- hmi_tags_from_plc(station, plc_refs=["DB.*" veya "DB.Uye", "PlcTag"], connection="HMI_Connection_n", screen=..., title=...)
- HMI tag adı TIA ile aynı: DB_Uye. Aynı PLC değerine bağlı tag varsa yeniden kullanılır.
- Doğrulanmış tipler: Bool, Int, Real (Real: Coding=IEEE754Float, Length=4). Diğerleri atlanır.
- Bool'lar ekrana henüz konmuyor (gösterge kalıbı yok).

## 3. PLC alarmları
- FB içinde Program_Alarm (SIG := durum biti), instance DB; çağrıyı senaryo FC'sine ekle (block_source → düzenle → scl_import overwrite).
- Metinler: plc_alarm_texts(texts=[{alarm:"Alarm_Hata", text:"..."}]) → instance metni.
- Entegre bağlantı üzerinden HMI alarm penceresinde görünür.

## 4. Alarm sayfası
- V18 Openness klasik panellerde Alarm view'ı XML'e çıkarmaz → XML ile oluşturulamaz.
- Panel ailesi başına bir kez: Alarm view'lı ekranı hmi_screen_to_library ile master copy yap
  (kalıp ekranın adı yeni ekranın adı olur; V18'de ekran adı/numarası değiştirilemez).
- Sonra: hmi_screen_from_library(station, item).
- Mevcut kalıp: proje kütüphanesi TIA_Assistant/Comfort_Alarm_Sayfasi (HMI_3 Screen_1'den).

## Çökme dersleri (HMI import'u TIA'yı kapatabiliyor)
1. Olmayan PLC tag'i, 2. entegre olmayan bağlantıda sembolik tag, 3. panelin tanımadığı tip (Basic'te Word),
4. Real tag'de Coding eksik. Hepsi hmi_import ön kontrolünde reddediliyor. Kural: HMI XML'i daima TIA export'undan kalıp alınır.

## 5. Şablon ve faceplate (17.11, TP900 Comfort'ta doğrulandı)
- Ekran şablonu: TIA'da bir kez tasarlanır (ör. Template_1: Back/Main/Next). Yeni ekranlara `template` parametresiyle atanır
  (hmi_tags_from_plc, hmi_faceplate_screen). XML: Screen/LinkList/Template.
- Faceplate tipi (ör. MotorFP) TIA'da bir kez tasarlanır ve sürümü yayınlanır; Openness tip oluşturamaz.
- Instance'lar: hmi_faceplate_screen(station, screen, faceplate, instances=[{name, interface:{Prop:"DB.Uye"}}], template).
  Arayüz adları tip export'undan okunamıyor; HMI'daki mevcut bir instance'tan doğrulanıyor → her tip için ilk instance elle konur.
- Faceplate içinde Bool göstergesi: animasyon tag'i özellik gibi arayüze sürüklenemez. Ya faceplate tag'i tanımlanıp arayüze açılır
  (V18 menüsü henüz doğrulanmadı) ya da Bool'a bağlı IO/sembolik IO alanının process value'su arayüze açılır.
