# Senaryo: SINAMICS G120C sürücü ekleme

Durum: TIA Portal V18'de uçtan uca denendi (12.11.2026, `test_MCP` projesi, 6SL3210-1KE18-8UF1).

## Ne zaman

Ekipman listesinde SINAMICS G120C satırı varsa (MLFB `6SL3210-1KE...`). G120C kompakt bir sürücüdür: tek MLFB, ayrı güç modülü yoktur.

## Listeden gelen

- MLFB, adet, varsa etiket.
- Motor plaka değerleri: gerilim, akım, güç, frekans, devir. Listede veya ayrı bir motor tablosunda olabilir.

## Varsayılanlar

- Telegram: Standart Telegram 20.
- Firmware: katalogdaki en yeni sürüm.
- IP ve cihaz adı: TIA atar.
- Motor tipi: asenkron (InductionMotor).
- Gerilim/akım çifti: plakada iki bağlantı varsa (örneğin 230V Δ / 400V Y), sürücünün besleme gerilimine uyan çift yazılır. 3AC 380–480V sürücüde bu 400V ve ona karşılık gelen akımdır.

## Adımlar

1. `device_add` ile sürücüyü MLFB'den ekle.
2. `network_connect` ile subnet'e bağla ve `io_controller` olarak PLC'yi ver.
3. `drive_telegram` ile ana telegramı ayarla.
4. `drive_motor` ile plaka değerlerini yaz: p0304 gerilim, p0305 akım, p0307 güç, p0310 frekans, p0311 devir.
5. Tüm sürücüler bitince PLC'yi derle ve projeyi kaydet.

## Bilinen davranışlar

- Motor parametreleri (p0300–p0311) `drive_parameters` ile okunamaz ve yazılamaz; yalnızca `drive_motor` çalışır.
- G120C, p0308 (güç faktörü) ve p0309 (verim) alanlarını istemez. Listede varsa atlanır ve raporda belirtilir.
- p0335 (soğutma tipi) varsayılanda kalır (0, kendinden soğutmalı). Cebri fanlı motor belirtilmişse mühendise sor.
- Sürücü istasyonunun kendi derleme servisi yoktur; doğrulama PLC derlemesiyle yapılır.

## Sorulacaklar

- Motor plaka değerleri hiç verilmemişse: sürücüyü ekle, motor adımını atla, raporda eksik olarak belirt.
- Plakadaki iki akım değeri birbirini tutmuyorsa (üçgen akımı ≈ yıldız akımı × 1.73 olmalı): kullanılan değeri yaz ve tutarsızlığı raporda belirt.

## Raporda

Sürücü adı, firmware, IP, telegram numarası, giriş/çıkış adresleri, yazılan motor değerleri, sürücünün hesapladığı kutup çifti ve nominal moment (`drive_parameter_list` 300–360).

## Kapsam dışı

Online işlemler, motor tanıma ölçümü (p1900), safety fonksiyonları, sürücüye yükleme.
