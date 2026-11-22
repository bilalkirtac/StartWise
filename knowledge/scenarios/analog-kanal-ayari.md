# Senaryo: S7-1500 analog giriş kartı ekle ve kanal tipini ayarla (4..20 mA, 0..10 V ...)

Doğrulandı 18.11.2026 (test_MCP, PLC_1 CPU 1511-1): AI 8xU/I/RTD/TC ST **6ES7 531-7KF00-0AB0** V2.2
(Bilal "7KF02" dedi; katalogda yok, doğrusu 7KF00). Kanal 0/1 → 2 telli 4..20 mA, derleme 0 hata.

## Adımlar
1. module_plug station=PLC_1 mlfb="6ES7 531-7KF00-0AB0" name=AI_1 → ilk boş slot; dönen io_addresses giriş başlangıç byte'ını verir
   (test_MCP: slot 5, IB4..IB19 → kanal n = %IW(4 + 2n): kanal0 %IW4, kanal1 %IW6 ...).
2. Kanal yolu: station:PLC_1_Station/DeviceItems[AI_1]/DeviceItems[0]/Channels[n]  (modül adı iki kez geçer; item:AI_1 belirsiz).
3. Kanallar varsayılan olarak "modül şablonundan" gelir: ParameterSettings = 1 (FromTemplate) iken OperatingType/OperatingRange SALT OKUNUR.
   Önce ayrı bir object_set ile ParameterSettings = 0 (Manual).
4. Sonra ayrı bir object_set ile OperatingType:
   - 4 = Current2WireTransducer → TIA aralığı kendisi 16 (4..20 mA) yapar (tek geçerli aralık). Doğrulandı.
   - 3 = Current4WireTransducer → ardından OperatingRange 16 (4..20 mA) veya 15 (0..20 mA). Denenmedi.
   - 1 = Voltage → ardından OperatingRange 52 (0..10 V), 11 (±10 V, varsayılan), 10 (1..5 V). Denenmedi.
   - 0 = Deactivated (kullanılmayan kanal).
   Değer listesi: api_search scope=hw "OperatingType" / "OperatingRange".
5. Ölçüm: equipment_prepare type=analog io={"raw":"%IW4"} params={ScaleLow, ScaleHigh, LimitHH/H/L/LL, Hyst}.
   Ham değer 4..20 mA ve 0..10 V için 0..27648 (Analog_Meas varsayılanı); 4 mA altı / kablo kopması → Raw < -4864 veya 32767 → Error.

## Tuzaklar
- Geçersiz kombinasyon (ör. tip 4 iken aralık 16'yı ayrıca yazmak, ya da sırası ters) → "Value is not valid in the current configuration."
- object_set'e birden fazla alan verilince sıra korunmuyor (JSON sözlüğü alfabetik sıralanıyor: OperatingRange, OperatingType'tan önce gidiyor).
  0.26.1 ve sonrası: araç turlar halinde yazar, bağımlı alanlar tek çağrıda verilebilir. Daha eski sürümde AYRI çağrılarla yaz.
- Kanal ayarı donanım derlemesiyle doğrulanır: compile scope=all.
