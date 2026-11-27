# Senaryo: Elektrik projesinden (Eplan / AutoCAD Electrical) donanım ve tag table

Durum: TIA Portal V18'de denendi (13.11.2026, `fromEplan` projesi; DENS_G140, 6 sayfa PDF, CPU 1214C, 14 DI / 10 DQ / 2 AI, 26 tag, 0 hata). Adresler `io_addresses` ile TIA'dan okunarak doğrulandı ve 26 tag'e yorum yazıldı (0.7.0).

## Ne zaman

Mühendis bir elektrik projesi (Eplan P8 veya AutoCAD Electrical'dan alınmış PDF) verip donanımı ve/veya tag table'ı ondan oluşturmayı istediğinde. Elektrik projesi otomasyon projesinin ana veri kaynağıdır; tag table şemayla birebir uyumlu olmalıdır.

## Şema nasıl okunur

- PLC giriş ve çıkışları, bayt bayt, "PLC bağlantı şeması" sayfalarında gösterilir. Bir kart birkaç sayfaya bölünebilir ("PART 1 OF 3").
- Sayfanın altında veya üstünde kartın kutusu durur: adı, MLFB'si ve kanal adları (`DIa.0`, `DIb.3`, `DQa.5`, `AI.0`).
- Her kanalın hizasında, kutunun dışında, o kanalın işlev metni yazar (`EM STOP`, `UST DOME OPENED`). Tag adı bu metinden gelir.
- Kanal ile metin arasındaki hat takip edilir: eleman adı (`-B1`, `K1_F`, `R3`), kontak tipi (NO/NC), klemens (`XDI:3`) ve varsa sinyal referansı (`GOND_OK`).
- Hem sayfanın metnini hem görüntüsünü kullan. Metin kanal ve etiketleri verir, görüntü hangi etiketin hangi kanalın hizasında olduğunu doğrular. İkisi çelişirse görüntü geçerlidir.

## Adımlar

1. PDF'in tüm sayfalarını tara; PLC bağlantı sayfalarını ve üzerlerindeki kartları (MLFB) bul.
2. Donanım: projede yoksa kartları şemadaki MLFB ile ekle (`device_add`, `module_plug`). Sıra, şemadaki kart sırasıdır. Projede zaten varsa ekleme; MLFB'nin şemayla aynı olduğunu kontrol et.
3. `project_tree` çağır ve `io_addresses` listesinden her kartın gerçek adres aralığını al. Adres varsayma.
4. Kanal → adres eşlemesi: kartın giriş aralığı `start_byte`'tan başlar; `DIa.n` → `%I<start>.n`, `DIb.n` → `%I<start+1>.n`; çıkışlar aynı mantıkla `%Q`. Analog kanallar kelimedir: `AI.0` → `%IW<start>`, `AI.1` → `%IW<start+2>`.
5. Her kanal için bir tag üret (aşağıdaki kurallarla) ve `tags_add` ile tek tabloya yaz.
6. PLC'yi derle, projeyi kaydet, raporla.

## Tag kuralları

- **Ad:** şemadaki işlev metni; boşluk yerine alt çizgi, büyük harf, Türkçe karakterler sadeleştirilir (`SEVİYE SENSÖRÜ` → `SEVIYE_SENSORU`). İki satırlık metinler birleştirilir. Ad uydurulmaz ve çevrilmez.
- **Aynı ad iki kanalda geçerse** sonuna eleman adı eklenir.
- **Tip:** dijital kanal `Bool`, analog kanal `Int`.
- **Yorum:** `eleman | kontak tipi | klemens | sayfa`, bilinenler yazılır. Örnek: `-B1 | NC | s.4` veya `R3 | GOND. VALF 1 -Y1 | s.6`. Analoglarda sinyal aralığı eklenir (`0-10V`).
- **İşlevi yazmayan ama kablolanmış kanal** (metin yalnızca `DIGITAL INPUT` gibi): `YEDEK_DI_I1_2` biçiminde eklenir, yorumuna klemens yazılır.
- **Hiç kablolanmamış kanal:** tag üretilmez.
- **Tablo adı:** `Eplan_IO`. Mühendis başka ad verirse o.

## Raporda

- Giriş ve çıkış tabloları: adres, tag, şemadaki eleman.
- NC kontaklı girişler ayrıca belirtilir (program ters mantık ister).
- Bir girişin önünde seri bağlı başka kontak varsa (örneğin motor koruma şalteri) belirtilir.
- Okunamayan, çelişen veya emin olunamayan her kanal açıkça listelenir; tahminle tag üretilmez.
- Şemadaki kart ile projedeki kart farklıysa hiçbir şey değiştirilmez, mühendise sorulur.

## Bilinen davranışlar

- Kompakt CPU'larda `io_addresses` listesinde hızlı sayıcı (`HSC_n`, %I1000 ve sonrası) ve darbe çıkışı (`Pulse_n`, %Q1000 ve sonrası) aralıkları da görünür; bunlar şemadaki kanallar değildir, atlanır. Dahili kanallar `DI 14/DQ 10_1` ve `AI 2_1` öğelerindedir.
- Şemadaki sayfa numarası olarak çizimin kendi numarası kullanılır (antetteki `4/7`), PDF sayfa sırası değil; çapraz referanslar (`/7.1`) bu numarayı kullanır.

- (27.11.2026, `eplan_skillTest`, DALSAN 26000_PLC + 26000_MCC, 132 sayfa) Uzak IO panosu (ET200M IM153-2 + 7 DI32 + 2 DQ32) ve MCC'deki 21 G120 CU240E-2 DP + 1 S120 CU320-2 DP PROFIBUS'a bağlandı; 288 tag, derleme 0 hata.
  - Çizimde DP master CPU olmayabilir (DP hattı başka panoya gider): CPU mühendise sorulur.
  - Tag metni koordinatla eşlenir: `pdftotext -bbox`, kanal etiketlerinin (`I0.0`) x merkezine en yakın, kart kutusunun altındaki ve antetin üstündeki kelimeler. Antet/başlık bloğu metinleri dışarıda bırakılır.
  - Çizimdeki kanal adresleri yanlış olabilir (ör. DI7 sayfalarında I20.0, DQ2 sayfalarında Q0.0 tekrar). Kart sırası (sayfa başlığı "Module 7", kart adı) esastır; TIA'daki gerçek adres kullanılır ve fark raporlanır.
  - Kod + işlev çok satırlıysa ad = son satır + ekipman kodu (`MOTOR_KORUMA_26_110_10`); açıklama yoruma.
  - PROFIBUS: G120 DP varsayılan adresi 126 gelir (veri alışverişinde kullanılamaz) → `network_connect ip=<adres>` ile sıralı adres (master 2, slave'ler 3..). Master adresi de aynı araçla değiştirilir.
  - S120 CU320-2 DP + CUA31 + PM240-2: istasyonda `PlugNew OrderNumber:6SL3210-...` (CUA'yı Startdrive kendisi ekler). Eksende telegram 20 yok → 1.

## Kapsam dışı

Güç devresi, klemens planı ve kablo listesi üretimi; Eplan'ın kendi dışa aktarım dosyalarının (AML, EDZ) okunması; safety kanalları.
