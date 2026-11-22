# Senaryo: izleme (watch) tablosu

Doğrulandı: 18.11.2026, TIA V18, test_MCP PLC_1 (Drive_1_Izleme, 12 satır, IsConsistent=True). Gösterim olmadan öğrenildi.

## Ne zaman
Devreye alma / sahada bir ekipmanın değerlerini canlı izlemek için (trace V18 Openness'te yok; V21'de api_search "trace" ile tekrar bakılacak).

## Adımlar
1. Satırlar nesne olarak eklenemez (PlcWatchTableEntry özellikleri salt okunur; Entries yalnız yorum satırı oluşturur). Tablo XML içe aktarmayla kurulur.
2. XML kalıbı: knowledge/templates/watch-table-ornek.xml. Satır: SW.WatchAndForceTables.PlcWatchTableEntry, CompositionName="Entries";
   AttributeList: DisplayFormat, ModifyTrigger, ModifyValue, MonitorTrigger, Name (ör. "DB".Uye veya "Tag"); ObjectList: Comment (MultilingualText).
   ModifyIntention YAZILMAZ (salt okunur → import hatası).
3. Dosyayı connected folder'a yaz (ör. repos\tia-assistant\work\) ve içe aktar:
   object_invoke path=station:<st>/DeviceItems[<CPU>]/service:SoftwareContainer/Software/WatchAndForceTableGroup/WatchTables
   action=Import args={path:"<xml>", importOptions:"Override"}  (Override = aynı adlı tabloyu değiştirir)
4. Kontrol: object_inspect .../WatchTables[<ad>] → IsConsistent=True (bütün adresler çözüldü).

## Değerler
DisplayFormat: Bool, DEC_signed, DEC_unsigned, Float, Hex, Bin, Time, String, ... (api_type PlcWatchAndForceTableDisplayFormat).
MonitorTrigger/ModifyTrigger: Permanent, PermanentAtStart, OnceOnlyAtStart, PermanentAtEnd, ... (PlcWatchAndForceTablePreDefinedTrigger).

## Varsayılan içerik (sürücü için)
Senaryo girişleri (start, hız referansı, geri bildirim), durum DB'si (çalışıyor, hata, kilit, Status Hex, hız, akım, tork, güç), ölçekleme kontrolü için ham telegram kelimesi.
