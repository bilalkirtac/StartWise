# Örnek girdiler

`test_MCP` projesini kurarken kullanılan gerçek girdiler. Yeni bir özelliği denerken aynı girdilerle tekrar çalıştırmak için burada duruyor.

| Dosya | İçerik |
|---|---|
| `ekipman-listesi.csv` | 7 kalemlik ekipman listesi: CPU 1511-1 PN, 2 DI, 1 DQ, ön konnektör (atlanır), KTP700 Basic, SCALANCE XB005, 2 adet G120C |
| `motor-plaka-degerleri.xlsx` | İki motorun plaka değerleri (4 kW ve 3 kW). Motor 2'nin üçgen akımı bilerek tutarsız yazılmıştır; tutarsızlık yakalama testi |
| `g120-moduler-suruculer.xlsx` | Üç modüler G120: CU240E-2 PN, CU240E-2 PN-F, CU250S-2 PN, her biri PM240-2 11 kW ile |
| `eplan-DENS_G140.pdf` | Eplan'dan alınmış 6 sayfalık elektrik projesi: CPU 1214C, 14 DI / 10 DQ / 2 AI. `fromEplan` projesindeki `Eplan_IO` tablosunun kaynağı |
