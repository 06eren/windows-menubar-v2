using CommunityToolkit.Mvvm.ComponentModel;

namespace Windows_10_MenuBar.Models;

/// <summary>
/// Her monitör için ayrı ayar profili
/// </summary>
public partial class MonitorProfile : ObservableObject
{
    [ObservableProperty] private string _deviceName = string.Empty;    // Monitör cihaz adı
    [ObservableProperty] private int _monitorIndex = 0;                // Monitör index'i
    [ObservableProperty] private bool _isEnabled = true;               // Bu monitörde bar göster
    
    // Monitör pozisyon bilgileri
    [ObservableProperty] private double _screenLeft = 0;
    [ObservableProperty] private double _screenTop = 0;
    [ObservableProperty] private double _screenWidth = 1920;
    [ObservableProperty] private double _screenHeight = 1080;
    [ObservableProperty] private bool _isPrimary = false;

    // Özel ayarlar (sync kapalıysa her monitör farklı olabilir)
    [ObservableProperty] private string? _customTheme = null;          // null = sync edilmiş tema kullan
    [ObservableProperty] private double? _customOpacity = null;        // null = sync edilmiş opacity kullan
    [ObservableProperty] private double? _customBarHeight = null;      // null = sync edilmiş height kullan
}
