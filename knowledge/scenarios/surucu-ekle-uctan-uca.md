# Senaryo: sürücü eklendi → senaryo yazmaya hazır (uçtan uca)

Araç: `drive_prepare(plc, drive)` — tek çağrı, idempotent (tekrar çalıştırmak güvenli). Önce `dry_run=true` ile planı göster.
Doğrulandı 18.11.2026 (test_MCP, StartWise 0.24.1): Servo_1, Servo_2 (S210 105), Drive_1–6 (G120C / G120 CU240E-2 PN, telegram 20); derleme 0 hata.

Kararlar (Bilal, 18.11.2026):
- Yapı manisa_V18 gibi: servolar LAD FC "Servo Motors", asenkronlar LAD FC "Asenkron Motors", sürücü başına bir ağ; OB1 "Main" FC'leri çağırır.
- Arayüz = FB'nin KENDİ instance DB'si. Ayrı <sürücü>_IO DB / UDT YOK (gereksiz program yükü). Senaryo kodu girişleri instance DB'ye yazar,
  çıkışları oradan okur.
- (26.11.2026) Ağda her FB girişi ve çıkışı kendi instance üyesine bağlanır (ör. EnableAxis ← "SINA_SPEED_Drive_6".EnableAxis,
  ActVelocity → "SINA_SPEED_Drive_6".ActVelocity): değerler çağrıda görünür.
  İstisnalar: servo `Axis` = TO, asenkron `HWIDSTW`/`HWIDZSW` = HW identifier sabiti, `ConfigAxis` boş kalır (kütüphane varsayılanı 16#003F).
  Pinleri boş eski ağlar aynı çağrıda yerinde yeniden yazılır.
  Doğrulandı 26.11.2026 (sync_test, V21, Drive_1 G120C telegram 20): eski ağ yeniden yazıldı, derleme 0 hata 0 uyarı; ikinci çağrı "network exists".
- Servo port topolojisi: hat (PLC son portu → mevcut servolar → yeni servo P1). Gerçek kablolama farklıysa mühendis düzeltir.

## Akış (aracın yaptığı)
1. Tür: ana telegram 105/102/3/5/… → servo; 1/20/352 → asenkron (kind ile zorlanabilir).
2. Servo: TO_PositioningAxis (sürüm CPU FW'den: V2.9 → 6.0), ActorInterface.Connect(telegram byte ×8), hat topolojisi,
   FB S210_AbsCnt (knowledge/templates/S210_AbsCnt.scl, {MC_VERSION}=TO sürümü), instance "S210_AbsCnt_<sürücü>".
3. Asenkron: SINA_SPEED yoksa açık DriveLib global kütüphanesinden kopyala; HW ID sabiti "<sürücü>~PROFINET_interface~Standard_telegram_20";
   instance "SINA_SPEED_<sürücü>".
4. FC'ye ağ ekle (FC yoksa oluştur). Eski düzende (pinleri boş ya da <sürücü>_IO'ya bağlı) ağ varsa yerinde yeniden yazar; artık kullanılmayan _IO DB'yi raporlar, silmez.
5. OB1'de FC çağrısı yoksa ağ ekler. Yazılım derlemesi. Kaydetmez → project_save.

## Senaryo kodu nasıl sürer
- Servo: "S210_AbsCnt_<sürücü>".Enable / Reset / Home / HomeMode(7) / HomePosition / StartPos / Position / Velocity(10.0) / Direction(1) /
  JogFwd / JogBwd / JogVelocity(5.0) / Stop / StandStillVel(0.01) yazılır; Powered / Homed / Busy / InPosition / StandStill / ActPos / ActVel /
  Error / ErrorID okunur. (Parantez içi = FB varsayılanı.)
- Asenkron: "SINA_SPEED_<sürücü>".EnableAxis / AckError / SpeedSp / RefSpeed (sürücü p2000 ile aynı olmalı) / ConfigAxis yazılır;
  AxisEnabled / Lockout / ActVelocity / Error / Status / DiagId okunur.
- Senaryo FC'si OB1'de motor FC'lerinden ÖNCE çağrılmalı (komut aynı çevrimde işlenir).
- Bir sürücü yalnız bir yerden sürülür (FB tek yerde çağrılır).

## Teknik notlar
- LAD içe aktarmada boş FB pini `<Wire><OpenCon UId=".."/><NameCon UId="call" Name="pin"/></Wire>` ister (çıkışta sıra ters).
  Pin tellerden tamamen çıkarılırsa: "The connection with the name '1' is not connected to the object with the UID ...".
- Yeni içe aktarılan bloklar programı tutarsız bırakır; tutarsız blok dışa aktarılamaz → araç bir kez derleyip yeniden dener.
- Sistem sabiti erişimi: <Access Scope="GlobalConstant"><Constant Name="Drive_1~PROFINET_interface~Standard_telegram_20"/></Access>.
- Ayrıntı: servo-s210-to.md (TO), drive-g120c.md / drive-g120-modular.md (donanım + telegram).
