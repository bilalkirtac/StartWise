# Kararlar

Projenin yönünü belirleyen kararlar ve gerekçeleri. Yeni bir karar alındığında buraya eklenir.

## Ürün

| Karar | Gerekçe |
|---|---|
| Amaç işi hızlandırmak: ekipman listesinden çalışan bir proje iskeleti | Mühendisin tekrarlayan işini azaltmak; ince ayarı mühendis yapar |
| Donanım yalnızca listedeki MLFB ile eklenir, varsayılan PLC yoktur | Proje boş açılır ve listeye göre şekillenir |
| Sorulmadan varsayılan uygulanır: en yeni firmware, ilk boş slot, IP'yi TIA atar | Arayüzdeki soru-cevap adımları işi yavaşlatıyor |
| PLC'ler şifresiz eklenir | Şifre sonradan eklenebilir; derleme hemen geçsin |
| PLC, HMI ve sürücüler aynı subnet'te | Mühendisin standart yapısı |
| Sürücülerde Standart Telegram 20 | Mühendisin standart seçimi |
| Motor verisinde sürücünün besleme gerilimine uyan gerilim/akım çifti yazılır | Plakada iki bağlantı varsa tekrar sormamak için |
| Sürücünün istemediği alanlar atlanır ve raporda belirtilir | Hata sayılması akışı gereksiz yere kesiyor |
| İşler senaryo olarak tarif edilir (`knowledge/scenarios`) | Mühendisin sahadaki sırası ve varsayılanları kalıcı olsun, her prompt'ta tekrar yazılmasın |
| Her şey offline; yükleme, online yazma, force yok | Sahadaki ekipmana dokunma riski olmasın |

## Teknik

| Karar | Gerekçe |
|---|---|
| Tek bir C# programı hem MCP sunucusu hem Openness istemcisi | İlk şartnamedeki Python + C# iki süreçli yapı gereksiz karmaşıktı |
| Windows ile gelen C# 5 derleyicisi, NuGet ve SDK yok | Kurulumda hiçbir ek araç gerekmesin; kod bu yüzden C# 5 sözdizimiyle yazılır |
| MCP protokolü elle uygulanır (stdio üzerinde JSON-RPC) | .NET Framework 4.8 ve bağımlılıksız derleme için |
| İlk şartnamedeki onay, hash ve denetim katmanları çıkarıldı | Claude uygulaması zaten araç bazında izin soruyor; proje offline ve kopya üzerinde |
| Sürüm başına ayrı adaptör, şimdilik yalnızca V18 | Openness API'si sürüme bağlı; V21 derleme modelini değiştiriyor |
| Kod, gerçek V18 `Siemens.Engineering.dll` dosyasına karşı derlenerek doğrulanır | İmza hataları mühendisin bilgisayarına gitmeden yakalanır |
| Var olan nesnelerin üzerine sessizce yazılmaz | Çakışma hata olarak döner; SCL'de `overwrite` açıkça verilmelidir |
| Araç parametresi `station`, `device` değil | Claude ile bilgisayar arasındaki bağlantı katmanı `device` adını kendine ayırıyor |

İlk şartname arşivde duruyor: [arsiv/ilk-sartname-GPT.md](arsiv/ilk-sartname-GPT.md). Geçerli değildir; karşılaştırma için saklanıyor.

## 17.11.2026 — Kod imzalama ertelendi
- Ürün satışa yakınlaşana kadar kod imzalama yok; geliştirmede Norton istisnası (bin) yeterli. Exe adı TiaAssistant.exe, Claude config anahtarı startwise.
- Sonra: Türkiye için bireysel IV sertifika (ör. SSL.com, bulut imzalama) ya da şirket kurulunca OV; EV gereksiz (2024 sonrası SmartScreen avantajı yok). Microsoft Artifact Signing bireylere yalnız ABD/Kanada.

## 18.11.2026 — Sürücü ve saha ekipmanı hazırlama
- Sürücü eklenince "en uç noktaya kadar" hazırla: servolar LAD FC "Servo Motors", asenkronlar "Asenkron Motors", sürücü başına bir ağ, OB1 çağırır (manisa_V18 yapısı). Servo port topolojisi varsayılanı hat (line).
- Arayüz = FB'nin kendi instance DB'si; ayrı <sürücü>_IO DB/UDT yok (Bilal: gereksiz program yükü). Ağda yalnız saha/HW bağlantıları (Axis, HWID, I/O tag) bağlı, diğer pinler boş (OpenCon); senaryo kodu instance DB'ye yazar/okur.
- Aynı desen saha ekipmanlarına genellendi (Bilal'in fikri): tür = knowledge/equipment/<tür>/equipment.json + FB SCL; yeni tür build gerektirmez. Instance DB adı = ekipman kodu (V3300, TT3300).
- Vana (Valve_SA): tek etkili, hata çıkışı kapatır (Reset'e kadar). Tag adları <kod>_Opened/_Closed/_CMD, tablo "Valves".
- Analog (Analog_Meas): ham 0..27648, HH/H/L/LL + tek histerezis (tolerans), kanal varsayılanı 2 telli 4..20 mA.

## 20.11.2026 — Ana sürüm V21
- StartWise'ın ana hedefi TIA V21 (son sürüm), tam kapasite. V18'den başlandı çünkü kuruluydu; V18 ve daha eski sürümlere geriye dönük destek ayrıca planlanacak.
- Ekipman hazırlama (equipment_prepare) yalnız kullanıcı isterse; drive_prepare'in parçası değil.
- Klasik HMI (V21): StartWise HMI–PLC bağlantısını kurar; tag/IO field bağlamayı mühendis TIA'da yapar (Bilal test etti: IO field'a instance DB tag'i bağlanıyor). Sembolik XML tag importu V21 klasik panelde yok.
- WinCC Unified: Siemens'in yönü; Bilal kuruyor, sonraki büyük HMI işi Unified API (nesne tabanlı) ile.


## 20.11.2026 - HMI: StartWise iş yükünü alır, tasarım mühendiste kalır
- StartWise'ın HMI'daki işi: HMI tag'leri ve PLC bağlantıları, ekranların ve objelerin (buton, gösterge, IO field, trend, alarm view, alarm) oluşturulması ve bağlanması.
- Konum, boyut, renk ve görsel düzen mühendisin işi. Objeler basit otomatik yerleşimle konur; yerleşimi prompt ile tarif ettirmek hedef değil.
- x/y/w/h parametreleri kalır ama öne çıkarılmaz.


## 21.11.2026 - Satış kanalı
- İlk satışlar arkadaşların şirketleri üzerinden: şirket satar ve fatura keser, Bilal lisans anahtarını üretir (repos\_lisans\keygen.py), pay alır.
- Online MoR (Polar.sh / Paddle) ve Claude eklenti dizini sonraya; araştırma notları 21.11 sohbetinde.
- Deneme: 3 proje, sonra lisans. Red mesajına satış iletişimi satırı eklenecek (şirket/iletişim belli olunca).


## 22.11.2026 - Açık kaynak ve Claude eklentisi
- StartWise açık kaynak: Apache-2.0, https://github.com/bilalkirtac/StartWise (public). Deneme kilidi ve lisans anahtarı kaldırıldı (0.34.0; License.cs yedeği repos\_lisans).
- İki parça: araçlar = Claude Desktop uzantısı (.mcpb, GitHub Releases); beceri = Claude eklentisi, repoda plugins/startwise (sadece md/json/png).
- Neden ayrı: masaüstü marketplace sync'i eklenti içinde uzak .mcpb adresini ve exe'yi reddetti; dizin, doğrulanmamış organizasyonlardan program/betik kabul etmiyor. Beceri, araçlar yoksa kullanıcıyı uzantı kurulumuna yönlendiriyor.
- Dizine başvuru 22.11.2026'da incelemeye gönderildi. Yayın kaynağı artık startwise reposu; tia-assistant geliştirme klasörü.
- Yeni sürüm: build.cmd + pack.cmd -> dist\StartWise-<ver>.mcpb -> GitHub Release v<ver>; kod değişiklikleri startwise reposuna da gider.


## 23.11.2026 - Açık / kapalı sınırı
- GitHub (açık kaynak) sadece Faz 0 ve genel araçlar için güncellenir.
- Senkron hareket işleri (uçan testere, LCamHdl/LFFS sihirbazı) tehlikeli olduğu için açık kaynağa girmez; sadece müşteri bazlı, kapalı paket olarak.
- customer_lib ve firma paketleri de kapalı. Yol haritası: Claude doc "StartWise Open Core Yol Haritası".
- Deneme projesi: Documents\Automation\sync_test (CPU 1515T-2 PN FW V4.1, TO Master + Saw).


## 26.11.2026 - Tek parça: uzantı + MCP üzerinden skill
- Claude eklentisi (plugins/startwise, marketplace.json) kaldırıldı (0.37.0). Kurulum tek adım: .mcpb uzantısı.
- Skill'ler repoda `skills/<ad>/SKILL.md`; .mcpb içine paketlenir ve sunucu sunar: Skills over MCP (`skills/list`, `skill://` resources) + istemci bunu henüz istemediği için `skill_list` / `skill_get` araçları. Claude Desktop local sunuculardan skill istemeye başlayınca ek iş gerekmez.
- Neden: eklentinin skill'i `skills/startwise-tia` kopyasıydı (iki kopya senkron tutulmak zorundaydı); dizin başvurusu zaten geri çekildi (24.11). Bilal, bağlantılı masaüstü + proje düzeninde çalışmanın gözle görülür hızlı olduğunu gördü (26.11).
- Exe üretilmez (26.11.2026): build.cmd kaldırıldı. Tek çalışma yolu .mcpb + start.ps1 (bellekte derleme, Microsoft imzalı powershell.exe). Geliştirme: uzantıda "Development source folder" = repo. Neden: imzasız exe'ler Norton/Symantec'e takılıyor; .mcpb yeterli.
