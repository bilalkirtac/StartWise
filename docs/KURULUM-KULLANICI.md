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

1. `StartWise-<sürüm>.mcpb` dosyasını indir.
2. Claude Desktop → **Settings → Extensions** (yeni görünümde **Settings → Desktop app → Extensions → Manage**) → **Advanced settings** → **Install Extension…** → dosyayı seç.
   (Dosyaya çift tıklamak bazı bilgisayarlarda Windows'un "uygulama seç" penceresini açar; o durumda bu yolu kullan.)
3. **License key** alanı: lisansın varsa yapıştır, yoksa boş bırak (deneme sürümü).
4. **Install** → Claude'u yeniden başlat.
5. TIA Portal V21'i aç, Claude'a "TIA'ya bağlan" de. İlk bağlantıda TIA bir Openness erişim onayı sorar → **Yes to all**.

## 3. Deneme sürümü ve lisans

- Lisanssız StartWise **3 farklı TIA projesinde** değişiklik yapar. Bu projelerde çalışmaya devam eder.
- İnceleme, arama, derleme ve dışa aktarma gibi okuma işleri her projede her zaman çalışır.
- Lisans anahtarını sonradan girmek için Claude'a: "Bu StartWise lisans anahtarını etkinleştir: SW1...." de.
- Durumu görmek için: "StartWise durumu nedir?"

## 4. Sorun giderme

- **"Openness group" / erişim reddedildi**: kullanıcı "Siemens TIA Openness" grubunda değil ya da gruba ekledikten sonra oturum kapatılıp açılmadı.
- **Cihaz eklerken lisans hatası**: ilgili TIA lisansı (STEP 7 Professional, WinCC ...) yok veya Automation License Manager'da etkin değil.
- **Antivirüs uyarısı**: StartWise henüz dijital imzalı değil; dosyayı güvenilir kaynaktan aldıysan izin ver.
