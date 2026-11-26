# Kurulum

## Ön koşullar

- TIA Portal V18 ve TIA Openness kurulu.
- Windows kullanıcısı `Siemens TIA Openness` yerel grubuna üye. Gruba eklendikten sonra oturumu kapatıp açmak gerekir.
- Claude masaüstü uygulaması.

Ek bir derleyici, SDK veya paket gerekmez.

## İlk kurulum

1. `build.cmd` dosyasını çalıştır. Sonunda `BUILD OK` ve bir öz-test çıktısı görünmeli; `api_dir` satırı `...\PublicAPI\V18` ile bitmeli.
2. Claude'da Settings → Developer → Edit Config ile `claude_desktop_config.json` dosyasını aç. `"mcpServers": {` satırının hemen altına şunu ekle (sondaki virgül, arkasından başka girdi geliyorsa gerekir):

```json
    "startwise": {
      "command": "C:\\Users\\bilal\\source\\repos\\tia-assistant\\bin\\TiaAssistant.exe"
    },
```

3. Claude'u sistem tepsisinden Quit ile kapatıp yeniden aç. Developer sayfasında `startwise` "running" görünmeli.
4. TIA Portal V18'i aç ve Claude'a "TIA'ya bağlan" de. TIA bir Openness erişim penceresi gösterir; "Yes to all" seç.

## TIA Assistant → StartWise geçişi (17.11.2026)

Eski ayardaki `"tia-assistant"` girdisini yukarıdaki `"startwise"` girdisiyle **değiştir** (ikisini birlikte bırakma; aynı araçlar iki kez görünür). Exe dosyasının adı `TiaAssistant.exe` olarak kalır (yeni adlı exe Norton tarafından engellendi; `bin` klasörü Norton'da istisna olarak eklendi). Claude'da görünen ad config anahtarından gelir. Yeni exe için TIA erişim penceresi bir kez daha çıkabilir. Senaryolar, belgeler ve TIA projeleri bu değişiklikten etkilenmez.

## TIA V21 (19.11.2026)

V21'de Openness TIA ile birlikte kurulur (kurulumda ayrı seçenek yok). Startdrive V21 API'si aynı klasöre gelir.

1. `build.cmd V21` → `bin\V21\TiaAssistant.exe`. (`build.cmd` tek başına V18 build'ini yapar; ikisi yan yana durur.)
2. Claude ayarına ikinci bir girdi ekle (V18 girdisi kalır):

```json
    "startwise-v21": {
      "command": "C:\\Users\\bilal\\source\\repos\\tia-assistant\\bin\\V21\\TiaAssistant.exe"
    },
```

3. Claude'u Quit ile kapatıp aç. V21 araçları `startwise-v21` altında görünür; her exe yalnız kendi TIA sürümüne bağlanır.

## Kod değiştikten sonra

1. `build.cmd` dosyasını çalıştır. Claude açık kalabilir; betik çalışan exe'yi `.old.exe` olarak kenara alır.
2. Claude'u sistem tepsisinden Quit ile kapatıp yeniden aç. Pencereyi kapatmak yetmez.
3. Yeniden derlenen exe için TIA erişim penceresi bir kez daha çıkabilir.

`knowledge/` altındaki dosyaları değiştirmek için derleme veya yeniden başlatma gerekmez.

## Sorun giderme

| Belirti | Neden ve çözüm |
|---|---|
| Claude açılışta "Couldn't load app settings" diyor | `claude_desktop_config.json` geçersiz. Girdi `mcpServers` bloğunun içinde olmalı; virgülleri kontrol et |
| `tia_status` eski sürümü gösteriyor | Claude yeniden başlamamış. Sistem tepsisinden Quit et |
| Yeni araç görünmüyor | Aynı neden; ayrıca derlemenin `BUILD OK` verdiğini doğrula |
| `EngineeringSecurityException` | Kullanıcı Openness grubunda değil ya da TIA erişim penceresi reddedilmiş |
| `api_dir` V18 dışında bir klasör gösteriyor | `TIA_API_DIR` ortam değişkenini `...\Portal V18\PublicAPI\V18` olarak ayarla |

Günlük dosyası: `bin\startwise.log` (0.18.0 öncesi: `bin\tia-assistant.log`). Her araç çağrısı ve sonucu buraya yazılır.

## Kurulum paketi (.mcpb) — 20.11.2026

1. `build.cmd` (V21), sonra `pack.cmd` → `dist\StartWise-<sürüm>.mcpb`.
2. Hedef bilgisayarda: dosyaya çift tıkla ya da Claude Desktop penceresine sürükle (Ayarlar > Eklentiler). Claude kurulum ekranını açar; lisans anahtarı boş bırakılabilir.
3. Ön koşullar: TIA Portal V21 + STEP 7 (sürücüler için Startdrive), Windows kullanıcısı "Siemens TIA Openness" grubunda.
4. Geliştirme bilgisayarında paketi denemeden önce claude_desktop_config.json'daki elle eklenmiş "startwise-v21" girdisini kaldır; yoksa araçlar iki kez görünür.
5. Paket imzasız; antivirüs exe'yi işaretlerse istisna gerekir. Herkese açık dağıtımda kod imzalama eklenecek.


## Deneme sürümü ve lisans (0.33.0)
- Kurulum: `StartWise-<sürüm>.mcpb` dosyasına çift tıkla (veya Claude Desktop > Settings > Extensions'a sürükle) > Install. Lisans alanı boş = deneme.
- Deneme: StartWise en fazla 3 farklı TIA projesinde değişiklik yapar. Okuma araçları (inceleme, arama, derleme, dışa aktarma) her zaman çalışır. Sayılan projelerde çalışmaya devam eder.
- Lisans: anahtarı uzantının "License key" alanına gir ya da Claude'a "bu lisans anahtarını etkinleştir: SW1...." de (license_activate). Durum: tia_status > license.
- Ön koşullar: Windows, TIA Portal V21 (STEP 7; sürücüler için Startdrive; Unified için WinCC Unified), Windows kullanıcısı "Siemens TIA Openness" grubunda.
