# Openness V18 notları

Geliştirme sırasında gerçek TIA Portal V18'de gözlenen davranışlar. Dokümantasyondan değil, denemeden gelir; yeni bir şey öğrenildiğinde buraya eklenir.

## Kurulum ve bağlantı

- V18 kurulumu eski API sürümlerini de (15.1, 16, 17) kayıt defterine yazar: `HKLM\SOFTWARE\Siemens\Automation\Openness\18.0\PublicAPI`. Yalnızca `18.` ile başlayan alt anahtar kullanılmalı. Yanlış sürüm yüklenirse temel çağrılar çalışır ama katalog ve koruma servisleri eksik kalır.
- `PublicAPI\V18\Siemens.Engineering.xml` tüm açık üyelerin listesidir; imza doğrulamak için en hızlı kaynak.
- Bu kurulumda WinCC Unified tipi yok (`HmiUnified` ad alanı DLL'de bulunmuyor). Klasik HMI ekranları için `Create` yok, yalnızca XML `Import` var.
- Çalışan TIA'ya `Attach` hızlıdır. İlk bağlantıda TIA erişim penceresi çıkar; exe değişince tekrar çıkabilir.

## Donanım

- `HardwareCatalog.Find` MLFB ile önek araması yapar. Boşluksuz yazım (`6ES7516-3AN02-0AB0`) için 4. karakterden sonra boşluk eklenmiş hali de denenmeli.
- Katalog bazı MLFB'leri yer tutuculu yazar: `6SL3210-1PE22-7ULx`, `6ES7 590-1***0-0AA0`. Bu girdilerin sürümü `-` olur.
- `CreateWithItem(typeId, itemAdı, istasyonAdı)` diyalog açmaz.
- Yeni firmware'li S7-1500 (V2.9 denendi) API ile eklenince gizli veri koruması `WithoutPassword`, erişim seviyesi `NoAccess` gelir ve donanım derlemesi üç hata verir. `PlcMasterSecretConfigurator.Unprotect()` ve `PlcAccessLevelProvider.PlcProtectionAccessLevel = FullAccess` sonrası 0 hata.
- `CanPlugNew` bazı cihazlarda iyimserdir: `true` döner ama `PlugNew` başarısız olur. Sıradaki konum denenmeli.
- İstasyonların üst düzey öğeleri farklıdır: S7-1500'de `Rail_0`; G120'de adı `\t_0` olan bir raf ve baş modül (kontrol ünitesi). Raf adına güvenilmemeli.
- G120 güç modülü raya değil, kontrol ünitesinin içine, 3 numaralı konuma takılır. G120C'de bu alt öğe hazır gelir (`PM G120C`).
- Panellerde baş öğenin adı istasyon adıdır (`HMI_1_Station`), verilen öğe adı değil.

## Ağ

- `Node.CreateAndConnectToSubnet(ad)` subnet'i oluşturup bağlar; TIA benzersiz IP atar (192.168.0.1'den başlayarak).
- IP, maske ve cihaz adı `Node` üzerinde öznitelik olarak okunur: `Address`, `SubnetMask`, `PnDeviceName`.
- `IoController.CreateIoSystem("")` varsayılan adla (`PROFINET IO-System`) oluşturur. `IoConnector.ConnectToIoSystem` cihazı atar.

## Sürücüler (Startdrive)

- Sürücü nesnesi: `DeviceItem.GetService<DriveObjectContainer>().DriveObjects`.
- Ana telegram: `Telegrams` içinde `Type == MainTelegram`; `CanChangeTelegram(n)` sonra `TelegramNumber = n`. Telegram 20: 12 bayt giriş, 4 bayt çıkış.
- `DriveObject.Parameters` yaklaşık 4600 parametre verir ama motor bloğu (p0300–p0311) içinde yoktur.
- Motor verisi yolu: `DriveObject.GetService<DriveFunctionInterface>().HardwareProjection` → `SetMotorType(InductionMotor, 0)` → `GetCurrentMotorConfiguration(0)` → `RequiredConfigurationEntries` içindeki değerleri yaz → `ProjectMotorConfiguration(cfg, 0)`. Motor tipi ayarlanmadan `GetCurrentMotorConfiguration` hata verir.
- G120C'nin zorunlu alanları: p304, p305, p307, p310, p311, p335. p308 ve p309 yoktur.
- Uygulamadan sonra türetilen değerler hesaplanır: r313 kutup çifti, r333 nominal moment.
- G120C ve G120 istasyonlarında `ICompilable` servisi bulunamadı.

## PLC yazılımı

- `PlcTagComposition.Create(ad, tip, adres)` doğrudan çalışır; adres `%I0.0` biçiminde.
- SCL: `ExternalSources.CreateFromFile(ad, yol)` sonra `GenerateBlocksFromSource()`. Parametresiz çağrı hata durumunda blokları bırakmaz. Dosya UTF-8 BOM ile yazıldı.
- Derleme mesajlarının bir kısmı boş metinlidir (yalnızca yol taşır); bunlar süzülür.

## MCP tarafı

- Claude ile bilgisayar arasındaki bağlantı katmanı `device` adlı araç parametresini araca iletmiyor. Parametre adı olarak kullanılmamalı.
- Claude Store paketi olarak kuruluysa `claude_desktop_config.json` standart `%APPDATA%\Claude` klasöründe değildir. En güvenilir yol uygulamadaki Edit Config düğmesidir.
- Uzaktaki dosya gönderimi bir kez eski içerikle yazıldı; gönderimden sonra dosya boyutu doğrulanmalı.

## object_inspect ile öğrenilenler (18.11.2026, V18, test_MCP PLC_1)
- Yol: station:PLC_1_Station/DeviceItems[PLC_1]/DeviceItems[PROFINET interface_1]/service:NetworkInterface/Nodes[0] → HW.Node
  - Address (IP), SubnetMask, UseRouter, IpProtocolSelection: ReadWrite. ConnectedSubnet: Read.
  - PnDeviceName: Read while PnDeviceNameAutoGeneration=True (RW). Elle ad vermek için önce otomatik üretimi kapatmak gerekiyor gibi (denenecek).
  - Eylemler: ConnectToSubnet(Subnet), CreateAndConnectToSubnet(String), DisconnectFromSubnet().
- NetworkInterface (arayüz): InterfaceOperatingMode, MediaRedundancyRole (MRP), PnSendClock, TimeSynchronizationNtp, UseWebServerForInterface: ReadWrite. Compositions: Nodes, IoControllers, IoConnectors, TransferAreas.
- Device (istasyon): eylemler PlugNew/PlugCopy/PlugMove, CanPlug*, GetPlugLocations, CompareTo(IHardwareCompareTarget), ShowInEditor(View), Delete.
- item:<ad> projede birden fazla aynı adlı öğe varsa reddedilir (ör. 4 × "PROFINET interface_1") → station: ile tam yol.
- Clock memory (18.11, gösterimsiz öğrenildi): CPU DeviceItem'da ClockMemoryByte=true yazılınca ClockMemoryByteAddress özelliği ortaya çıkıyor (dinamik özellik) → object_set ile sırayla yazılır. TIA, Openness ile açıldığında da Default tag table'a Clock_Byte (%MB<n>) ve 8 bit tag'i kendisi ekliyor: Clock_10Hz .0, Clock_5Hz .1, Clock_2.5Hz .2, Clock_2Hz .3, Clock_1.25Hz .4, Clock_1Hz .5, Clock_0.625Hz .6, Clock_0.5Hz .7. Ayrıca tag eklemeye gerek yok. SystemMemoryByte büyük olasılıkla aynı desen (denenmedi).
- Tag gezinme yolu: station:<st>/DeviceItems[<CPU>]/service:SoftwareContainer/Software/TagTableGroup/TagTables[<tablo>]/Tags[<ad>] → LogicalAddress, DataTypeName (RW).
- System memory (18.11): SystemMemoryByte=true → SystemMemoryByteAddress görünür (varsayılan 1, clock MB0'da olduğu için TIA çakışmayı kendisi önledi). TIA Default tag table'a System_Byte (%MB<n>) ve 4 bit ekliyor: FirstScan .0, DiagStatusUpdate .1, AlwaysTRUE .2, AlwaysFALSE .3 (doğrulandı: AlwaysFALSE = %M1.3).
- PlcTagComposition: creatable PlcTag(DataTypeName?, LogicalAddress?, Name?); actions CreateFrom(MasterCopy), Find(String), Import(FileInfo, ImportOptions).
