# StartWise for TIA Portal — Kurulum (kullanıcı için)

StartWise, Claude'un TIA Portal V21'i senin bilgisayarında, çevrimdışı (offline) olarak kullanmasını sağlayan bir eklentidir. Donanım, ağ, sürücü, PLC blokları ve HMI işlerini düz cümleyle yaptırırsın. PLC'ye asla yükleme (download) yapmaz.

## 1. Gereklilikler

| Ne | Neden |
|---|---|
| **Windows 10/11 (64 bit)** | TIA Portal ve Openness yalnızca Windows'ta çalışır. |
| **Claude Desktop (Windows uygulaması)** | StartWise bilgisayarında çalışan yerel bir eklentidir (local MCP). Tarayıcıdaki claude.ai veya telefon uygulaması bilgisayarındaki TIA'ya erişemez. |
| **Ücretli Claude planı (Pro, Max, Team veya Enterprise) önerilir** | Bir TIA işi (cihaz ekleme, sürücü hazırlama, HMI) çok sayıda araç çağrısı ve uzun cevap kullanır; ücretsiz planın kullanım limiti kısa sürede dolar. Team/Enterprise'da eklentilere yöneticinin izin vermesi gerekebilir. |
| **TIA Portal V21** (STEP 7) | StartWise, TIA'nın programlama arayüzü Openness ile çalışır. Openness V21 ile birlikte kurulur; ayrı kurulum gerekmez. |
| **Geçerli TIA lisansları** | Openness, TIA'da elle yapabildiğin işleri yapar; lisansın izin vermediğini StartWise de yapamaz. S7-1200 için STEP 7 Basic yeterlidir; S7-1500, ET 200 ve sürücü projeleri için **STEP 7 Professional**, sürücüler için **Startdrive**, klasik paneller için **WinCC Comfort/Advanced**, Unified paneller için **WinCC Unified** gerekir. |
| **Windows kullanıcın "Siemens TIA Openness" grubunda** | Siemens, Openness'e yalnızca bu gruptaki kullanıcıların erişmesine izin verir. TIA kurulumu grubu oluşturur; kullanıcıyı eklemek için: Bilgisayar Yönetimi → Yerel Kullanıcılar ve Gruplar → Gruplar → "Siemens TIA Openness" → Ekle. Sonra Windows oturumunu kapatıp aç. |

## 2. Kurulum

StartWise iki parçadan oluşur, ikisini de kur:

**1. Araçlar (Claude Desktop uzantısı)** — TIA Portal ile konuşan yerel sunucu:

1. [Releases](https://github.com/bilalkirtac/startwise/releases) sayfasından `StartWise-<sürüm>.mcpb` dosyasını indir.
2. Claude Desktop → **Settings → Extensions** (yeni görünümde **Settings → Desktop app → Extensions → Manage**) → **Advanced settings** → **Install Extension…** → dosyayı seç → **Install**.
   (Dosyaya çift tıklamak bazı bilgisayarlarda Windows'un "uygulama seç" penceresini açar; o durumda bu yolu kullan.)

**2. Beceri (Claude eklentisi)** — Claude'a TIA kurallarını ve iş akışlarını öğretir:

1. Claude → **Settings → Plugins → Add** → marketplace: `bilalkirtac/StartWise` → **Sync**.
2. Listede **startwise** eklentisini kur.

Sonra Claude'u yeniden başlat, TIA Portal V21'i aç ve Claude'a "TIA'ya bağlan" de. İlk bağlantıda TIA bir Openness erişim onayı sorar → **Yes to all**.

## 3. Açık kaynak

StartWise Apache 2.0 lisanslı, ücretsiz ve açık kaynaktır: https://github.com/bilalkirtac/startwise. Hata bildirimi ve öneriler için GitHub'da "Issues" bölümünü kullan ya da yaz: **bilal.kirtac@gmail.com**

## 4. Sorun giderme

- **"Openness group" / erişim reddedildi**: kullanıcı "Siemens TIA Openness" grubunda değil ya da gruba ekledikten sonra oturum kapatılıp açılmadı.
- **Cihaz eklerken lisans hatası**: ilgili TIA lisansı (STEP 7 Professional, WinCC ...) yok veya Automation License Manager'da etkin değil.
- **Antivirüs uyarısı**: StartWise henüz dijital imzalı değil. Dosyayı yalnızca GitHub Releases sayfasından indir; şüphen varsa kaynak koddan kendin derleyebilirsin (`build.cmd`).
