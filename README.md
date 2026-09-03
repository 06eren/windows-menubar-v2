# Windows MenuBar v2

<div align="center">

![Windows MenuBar](https://img.shields.io/badge/Windows-MenuBar-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-12.0-239120?style=for-the-badge&logo=csharp&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

**macOS tarzı şık ve güçlü bir üst menü çubuğu deneyimi Windows'ta!**

[İndir](#-kurulum) · [Özellikler](#-özellikler) · [Kullanım](#-kullan%C4%B1m) · [Katkıda Bulun](#-katk%C4%B1da-bulun)

</div>

---

## Genel Bakış

Windows MenuBar, Windows işletim sisteminize macOS benzeri minimalist ve işlevsel bir üst menü çubuğu kazandırır. Hafif, performanslı ve özelleştirilebilir tasarımıyla masaüstü deneyiminizi bir üst seviyeye taşır.

## Özellikler

### Temel Özellikler

- **Sistem Saati & Takvim** - 12/24 saat formatı, Türkçe tarih, resmi tatiller
- **Hava Durumu** - İl/ilçe bazlı, gerçek zamanlı meteoroloji verileri
- **Ağ Yönetimi** - Wi-Fi ağları, sinyal gücü, Ethernet, VPN durumu
- **Donanım İzleme** 
  - Batarya seviyesi ve şarj durumu
  - Ses seviyesi kontrolü (scroll ile ayarlama)
  - Mikrofon ve kamera aktiflik göstergesi
  - VPN bağlantı durumu
- **Medya Kontrolü** - Spotify, YouTube ve diğer medya oynatıcılarını kontrol edin
- **Bluetooth Yönetimi** - Cihaz eşleştirme ve bağlantı kontrolü

### Gelişmiş Özellikler

- **Animasyonlu Gradient Temalar** - Aurora, Sunset, Ocean, Wind
- **Zamanlı Tema** - Gündüz/gece otomatik tema değişimi
- **Duvar Kağıdı Adaptasyonu** - Duvar kağıdı rengine göre otomatik tema
- **Tam Ekran Algılama** - Oyun ve videolarda otomatik gizlenme
- **AppBar Docking** - Ekrana profesyonelce sabitlenme
- **Bildirim Flash** - Yeni bildirimde görsel geri bildirim
- **Sistem Tepsisi** - Minimize edildiğinde sistem tepsisinde çalışmaya devam eder

### Kısayollar & Kontroller

- **Scroll ile Ses** - Bar üzerinde scroll yaparak ses ayarlayın
- **Çift Tık Minimize** - Boş alana çift tıklayarak tüm pencereleri minimize edin
- **Sağ Tık Menüsü** - Hızlı erişim için bağlam menüsü

## Kurulum

### Hazır Kurulum (Önerilen)

1. [Releases](https://github.com/06eren/windows-menubar-v2/releases) sayfasından en son sürümü indirin
2. `Windows10MenuBar_Setup.exe` dosyasını çalıştırın
3. Kurulum tamamlandığında uygulama otomatik başlayacaktır

### Gereksinimler

- Windows 10 (19041+) veya Windows 11
- .NET 10.0 Runtime
- 50 MB boş disk alanı
- 100 MB RAM

## Kullanım

### İlk Başlatma

Uygulama başladığında ekranınızın üst kısmında bir menü çubuğu görünecektir. İlk kullanımda:

1. Hava durumu konumunuzu seçin (Ayarlar > Hava Durumu Konumu)
2. Tercih ettiğiniz temayı seçin (Ayarlar > Görünüm)
3. İsteğe bağlı olarak "Windows ile Başlat" seçeneğini aktifleştirin

### Ayarlar

Ayarlar menüsüne üç şekilde erişebilirsiniz:

- Bar'a sağ tıklayıp "Ayarlar" seçin
- Sistem tepsisindeki ikona sağ tıklayıp "Ayarlar" seçin
- Herhangi bir öğeye tıklayın (saat, hava durumu vb.)

### Özelleştirme

**Temalar:**
- Dark, Midnight, Blue, Purple, Forest, Glass
- Animasyonlu: Aurora, Sunset, Ocean, Wind
- Duvar Kağıdı Adaptasyonu (Otomatik)
- Özel HEX renk kodu

**Davranış:**
- Zamanlı tema (gündüz açık, gece koyu)
- Tam ekranda otomatik gizlenme
- Windows ile otomatik başlatma
- Bar yüksekliği ve şeffaflık ayarı

## Performans Optimizasyonları

Uygulama, sistem kaynaklarını en verimli şekilde kullanacak şekilde optimize edilmiştir:

- **30 FPS Animasyon** - Düşük GPU kullanımı
- **Akıllı Güncelleme** - Clock sadece dakika değiştiğinde güncellenir
- **Background Priority** - UI thread asla bloke edilmez
- **Memory Safe** - Proper dispose pattern, memory leak yok
- **Optimized Polling** - Ağ/donanım kontrolleri 5-10 saniyede bir

**Ortalama Sistem Kullanımı:**
- CPU: %0.1-0.3 (idle)
- RAM: ~60-80 MB
- GPU: Minimal (30 FPS sınırlı)

## Teknolojiler

- **Dil:** C# 12.0
- **Framework:** .NET 10.0
- **UI:** WPF (Windows Presentation Foundation)
- **Kütüphaneler:**
  - CommunityToolkit.Mvvm - MVVM pattern
  - WPF-UI - Modern UI controls
  - NAudio - Audio control
  - System.Management - Hardware monitoring

## Katkıda Bulun

Katkılarınızı bekliyoruz! Projeye katkıda bulunmak için:

1. Fork edin
2. Feature branch oluşturun (`git checkout -b feature/AmazingFeature`)
3. Değişikliklerinizi commit edin (`git commit -m 'feat: Add AmazingFeature'`)
4. Branch'inizi push edin (`git push origin feature/AmazingFeature`)
5. Pull Request açın

### Geliştirme

```bash
# Repository'yi klonlayın
git clone https://github.com/06eren/windows-menubar-v2.git

# Proje dizinine gidin
cd windows-menubar-v2

# Build edin
cd Windows-10-MenuBar
dotnet build

# Çalıştırın
dotnet run
```

## Planlanan Özellikler

- [ ] CPU/RAM/Disk kullanım göstergesi
- [ ] Ağ hızı monitörü (upload/download)
- [ ] Pomodoro timer
- [ ] Pano geçmişi
- [ ] Spotify şarkı sözleri
- [ ] Widget sistemi
- [ ] Tema marketplace
- [ ] Çoklu dil desteği

## Bilinen Sorunlar

- Bazı durumlarda tam ekran algılama gecikebilir (fix: tam ekran tespiti optimize edildi)
- İlk başlatmada hava durumu yüklenmesi 5-10 saniye sürebilir

## Lisans

Bu proje MIT Lisansı altında lisanslanmıştır. Detaylar için [LICENSE](LICENSE) dosyasına bakın.

## İletişim

**Eren Arif Kargalıoğlu**

- GitHub: [@06eren](https://github.com/06eren)
- Project: [windows-menubar-v2](https://github.com/06eren/windows-menubar-v2)

## Teşekkürler

- [WPF-UI](https://github.com/lepoco/wpfui) - Modern WPF kontrolleri
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM framework
- [NAudio](https://github.com/naudio/NAudio) - Audio library

---

<div align="center">

**Windows MenuBar'ı beğendiniz mi? ⭐ bırakın!**

Made with ❤️ by [Eren Arif Kargalıoğlu](https://github.com/06eren)

</div>
