# Senaryo: SINAMICS G120 modüler sürücü ekleme

Durum: Donanım, ağ ve telegram adımları TIA Portal V18'de denendi (12.11.2026, `test_MCP` projesi; CU240E-2 PN, CU240E-2 PN-F, CU250S-2 PN + PM240-2). Motor verisi adımı bu sürücülerde henüz denenmedi.

## Ne zaman

Ekipman listesinde bir G120 kontrol ünitesi (CU, örneğin CU240E-2 / CU250S-2, MLFB `6SL3244-...` / `6SL3246-...`) ve ona ait bir güç modülü (PM, örneğin PM240-2, MLFB `6SL3210-1P...`) varsa. İki ayrı MLFB tek bir sürücüyü oluşturur.

## G120C'den farkı

Yalnızca donanımın eklenmesi farklıdır: önce kontrol ünitesi, sonra güç modülü. Ağ, telegram ve motor verisi adımları `scenarios/drive-g120c` ile aynıdır.

## Adımlar

1. `device_add` ile kontrol ünitesini kendi MLFB'siyle ekle (örnek isim `Drive_3`).
2. `module_plug` ile güç modülünü kendi MLFB'siyle aynı istasyona tak (örnek isim `Drive_3_PM`). Slot verme.
3. `scenarios/drive-g120c` senaryosunun 2–5. adımlarını uygula. Motor verisi istenmediyse 4. adımı atla.

## Bilinen davranışlar

- Güç modülü raya değil, kontrol ünitesinin içine, 3 numaralı konuma takılır. `module_plug` bunu kendisi bulur.
- Katalog, güç modüllerini son hanesi yer tutuculu yazar (`6SL3210-1PE22-7ULx`). Listedeki tam MLFB (`...7UL0`) bununla eşleşir.
- Güç modüllerinin firmware sürümü yoktur (katalogda `-`).
- PN-F kontrol ünitesinde bu senaryo yalnızca standart telegramı ayarlar; PROFIsafe telegramı ve safety ayarları kapsam dışıdır.

## Eşleştirme

- Listede hangi PM'in hangi CU'ya ait olduğu yazmıyorsa ve adetler eşitse sırayla eşleştir; eşit değilse mühendise sor.
