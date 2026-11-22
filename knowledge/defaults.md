# Varsayılanlar

Bu dosya her işte geçerli kuralları tutar. Mühendis prompt'ta farklı bir şey söylerse prompt geçerlidir. Burada veya ilgili senaryoda cevabı olan bir şeyi mühendise sorma.

## Genel

- Tüm işler offline yapılır. PLC'ye, sürücüye veya panele hiçbir şey yüklenmez.
- Donanım yalnızca ekipman listesindeki MLFB koduyla eklenir. MLFB uydurulmaz; katalogda bulunamazsa mühendise sorulur.
- Listede "eklemeye gerek yok" denen veya TIA'da karşılığı olmayan kalemler (ön konnektör, kablo, montaj rayı gibi) atlanır ve raporda belirtilir.
- Adet sütunu birden büyükse o kadar ayrı cihaz/modül eklenir.

## Donanım

- Firmware sürümü listede yoksa katalogdaki en yeni sürüm seçilir.
- Slot listede yoksa ilk uygun boş slot kullanılır; modüller listedeki sırayla takılır.
- PLC'ler şifresiz eklenir (gizli veri koruması yok, tam erişim). Korumayı mühendis sonradan ayarlar.

## Ağ

- PLC, HMI ve sürücüler aynı subnet'e bağlanır (`PN/IE_1`).
- IP adreslerini ve PROFINET cihaz adlarını TIA atar; listede adres verilmişse o kullanılır.
- Sürücüler ve dağıtık IO istasyonları PLC'nin PROFINET IO sistemine IO cihazı olarak atanır.
- Yönetilemeyen switch'ler projeye eklenir ama IO sistemine atanmaz.

## İsimlendirme

- Listede etiket yoksa: `PLC_1`, `DI_1`, `DQ_1`, `HMI_1`, `Switch_1`, `Drive_1` biçiminde, türüne göre sırayla.

## İş bitince

- PLC donanım ve yazılım olarak derlenir; hata ve uyarılar raporlanır.
- Proje kaydedilir.
- Rapor kısa bir tablodur: eklenenler, seçilen varsayılanlar (sürüm, slot, IP), atlananlar ve mühendisin kontrol etmesi gerekenler.
