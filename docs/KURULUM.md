# Kurulum ve geliştirme (Bilal için)

Kullanıcı kurulumu: `docs/KURULUM-KULLANICI.md` (PDF: `packaging/StartWise-Kurulum.pdf`).

StartWise exe üretmez (0.37.0). Tek çalışma yolu Claude Desktop uzantısıdır: Claude, Microsoft imzalı `powershell.exe` ile `start.ps1`'i başlatır; bu betik `src\*.cs` dosyalarını .NET Framework 4.8 C# derleyicisiyle bellekte derler ve sunucuyu çalıştırır. Diske exe yazılmaz, antivirüs takılmaz.

## Ön koşullar

- TIA Portal V21 (STEP 7; sürücüler için Startdrive). Openness TIA ile birlikte gelir.
- Windows kullanıcısı `Siemens TIA Openness` grubunda (ekledikten sonra oturumu kapatıp aç).
- Claude Desktop. Ek derleyici, SDK veya NuGet gerekmez.

## Paket oluşturma

1. `pack.cmd` çalıştır. Kaynakları bellekte derler, öz-testi çalıştırır, `dist\StartWise-<sürüm>.mcpb` ve `.sha256` dosyasını yazar. Sürüm `src\Program.cs` içindeki `ServerVersion`'dan gelir.
2. Claude Desktop → Settings → Extensions → Advanced settings → Install Extension… → `.mcpb` → Install → Claude'u yeniden başlat.
3. Yayın: GitHub Release `v<sürüm>`, ekler `.mcpb`, `.sha256` ve `StartWise-Kurulum.pdf`.

## Geliştirme (paket kurmadan kod değiştirme)

1. Uzantı ayarlarında **Development source folder** = `C:\Users\bilal\source\repos\tia-assistant`.
2. Kod değişince Claude'u sistem tepsisinden **Quit** edip aç (pencereyi kapatmak yetmez). `start.ps1` o klasörün `src\`'sini derler, `knowledge\`'ını kullanır.
3. `knowledge\` ve `skills\` altındaki dosyalar için derleme gerekmez; skill ve senaryolar her çağrıda diskten okunur.

Elle eklenmiş eski `startwise` / `startwise-v21` girdileri `claude_desktop_config.json`'da kalmamalı; yoksa araçlar iki kez görünür.

## Sorun giderme

| Belirti | Neden ve çözüm |
|---|---|
| `tia_status` eski sürümü gösteriyor | Claude yeniden başlamamış: sistem tepsisinden Quit |
| Yeni araç görünmüyor | Aynı neden; ya da bellekte derleme hata verdi: `pack.cmd` çıktısındaki derleme mesajlarına bak |
| `EngineeringSecurityException` | Kullanıcı Openness grubunda değil ya da TIA erişim penceresi reddedildi |
| TIA başka klasöre kurulu | Uzantı ayarı **TIA Openness API folder** = `...\PublicAPI\V21\net48` |

Günlük: `startwise.log` (STARTWISE_HOME altında; geliştirmede `packaging\startwise.log`). Her araç çağrısı ve sonucu buraya yazılır.

`bin\` klasörü eski exe denemelerinden kalmadır (gitignore'da); kullanılmaz.
