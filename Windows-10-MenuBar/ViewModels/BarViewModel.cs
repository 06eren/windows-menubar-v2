using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Windows_10_MenuBar.Services;
using Windows_10_MenuBar.Helpers;
using Windows_10_MenuBar.Interop;
using Windows_10_MenuBar.Models;
using Wpf.Ui.Common;

namespace Windows_10_MenuBar.ViewModels;

public partial class BarViewModel : ObservableObject, IDisposable
{
    // ── Services ─────────────────────────────────────────────────────────────
    private readonly WeatherService      _weather      = new();
    private readonly NetworkService      _network      = new();
    private readonly MediaService        _media        = new();
    private readonly HardwareService     _hardware     = new();
    private readonly NotificationService _notifications = new();

    // ── Clock ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _currentTime = "";
    [ObservableProperty] private string _currentDate = "";
    private readonly DispatcherTimer _clockTimer;

    // ── Window title ──────────────────────────────────────────────────────────
    [ObservableProperty] private string _activeWindowTitle = "Windows";

    // ── Media ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private MediaInfo _currentMedia = new();

    // ── Network ───────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isWifiConnected;
    [ObservableProperty] private SymbolRegular _currentNetworkIcon = SymbolRegular.WifiOff24;
    [ObservableProperty] private bool _hasWifi;
    [ObservableProperty] private bool _isEthernet;
    [ObservableProperty] private string _networkName = "Ağ";
    public ObservableCollection<WifiInfo> AvailableNetworks { get; } = new();

    // ── Bluetooth ─────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _hasBluetooth;
    [ObservableProperty] private bool _isBluetoothOn;
    public ObservableCollection<BluetoothDeviceInfo> BluetoothDevices { get; } = new();

    // ── Hardware ──────────────────────────────────────────────────────────────
    [ObservableProperty] private BatteryInfo _battery = new();
    [ObservableProperty] private bool _isMicrophoneActive;
    [ObservableProperty] private bool _isCameraActive;
    [ObservableProperty] private bool _isVpnConnected;
    [ObservableProperty] private string _vpnName = string.Empty;
    [ObservableProperty] private int _volumeLevel = 100;
    [ObservableProperty] private SymbolRegular _volumeIcon = SymbolRegular.Speaker224;
    [ObservableProperty] private int _brightnessLevel = 100;
    [ObservableProperty] private bool _hasBrightnessControl;

    // ── Weather ───────────────────────────────────────────────────────────────
    [ObservableProperty] private string _weatherCondition = "Yükleniyor...";
    [ObservableProperty] private SymbolRegular _weatherIcon = SymbolRegular.WeatherSunny24;
    [ObservableProperty] private string _weatherTooltip = "Hava durumu bilgisi alınıyor...";
    [ObservableProperty] private ObservableCollection<string> _provinces = new();
    [ObservableProperty] private ObservableCollection<string> _districts = new();

    // ── Settings ──────────────────────────────────────────────────────────────
    [ObservableProperty] private BarSettings _settings = SettingsService.Current;
    [ObservableProperty] private string _barBackground = SettingsService.Current.BarColor;
    [ObservableProperty] private double _barOpacity    = SettingsService.Current.BarOpacity;
    [ObservableProperty] private string _foregroundColor = "#FFFFFF";

    // ── Notifications ─────────────────────────────────────────────────────────
    // Sadece flash — sayaç gösterilmiyor

    // ── Calendar ──────────────────────────────────────────────────────────────
    [ObservableProperty] private int _calendarYear;
    [ObservableProperty] private int _calendarMonth;
    [ObservableProperty] private string _calendarMonthName = "";
    public ObservableCollection<CalendarDay> CalendarDays { get; } = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public BarViewModel()
    {
        SyncStartWithWindows();

        // Clock timer'ı 1 saniye yerine sadece dakika değişimlerinde güncelle
        // Performans: saniyede 1 PropertyChanged yerine dakikada 1
        _clockTimer = new DispatcherTimer(DispatcherPriority.Background) 
        { 
            Interval = TimeSpan.FromSeconds(1)  // Her saniye kontrol
        };
        _clockTimer.Tick += (_, _) => UpdateTime();
        _clockTimer.Start();
        UpdateTime();

        ResetCalendarToToday();
        LoadCities();

        Settings.PropertyChanged += OnSettingsPropertyChanged;

        // Start background loops — App.AppCts iptal edilince hepsi durur
        var ct = App.AppCts.Token;

        _ = _hardware.InitBluetoothAsync(BluetoothDevices, ct).ContinueWith(_ =>
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                HasBluetooth = _hardware.HasBluetooth, 
                DispatcherPriority.Background));

        _ = _hardware.RunSystemLoopAsync(async snap =>
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Battery            = snap.Battery;
                IsMicrophoneActive = snap.IsMicrophoneActive;
                IsCameraActive     = snap.IsCameraActive;
                IsVpnConnected     = snap.IsVpnConnected;
                VpnName            = snap.VpnName;
            }, DispatcherPriority.Background), ct);

        // Audio: event-driven, polling yok
        _hardware.InitAudio(async snap =>
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                VolumeLevel = snap.VolumeLevel;
                VolumeIcon  = snap.VolumeIcon;
            }, DispatcherPriority.Background));

        _ = _network.RunLoopAsync(AvailableNetworks, async state =>
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsWifiConnected    = state.IsWifiConnected;
                HasWifi            = state.HasWifi;
                IsEthernet         = state.IsEthernet;
                NetworkName        = state.NetworkName;
                CurrentNetworkIcon = state.Icon;
            }, DispatcherPriority.Background), ct);

        _media.MediaChanged += OnMediaChanged;
        _ = _media.InitAsync();

        _ = _weather.RunLoopAsync(async result =>
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                WeatherCondition = result.Condition;
                WeatherTooltip   = result.Tooltip;
                WeatherIcon      = result.Icon;
            }, DispatcherPriority.Background), ct);

        var (brightness, hasControl) = _hardware.GetBrightness();
        BrightnessLevel      = brightness;
        HasBrightnessControl = hasControl;

        _ = DetectWallpaperColorAsync();

        // Bildirim servisi — sadece yeni bildirim flash'ı
        _notifications.NewNotificationArrived += OnNewNotificationArrived;
        _ = _notifications.InitAsync(ct);

        // Zamanlı tema — clock ile aynı frekansta kontrol (1s), gecikme yok
        _ = RunScheduledThemeLoopAsync(ct);
    }

    /// <summary>
    /// MainWindow tarafından set edilir — yeni bildirim gelince UI flash'ı tetikler.
    /// ViewModel → View bağımlılığını tersine çevirmeden, callback ile köprü kurulur.
    /// </summary>
    public Action? OnNewNotification { get; set; }

    // ── Tray tooltip sync ─────────────────────────────────────────────────────

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(WeatherCondition) or nameof(CurrentTime) or nameof(CurrentDate))
            App.TrayIcon?.UpdateTooltip(WeatherCondition, CurrentTime, CurrentDate);
    }

    // ── Clock ─────────────────────────────────────────────────────────────────

    private int _lastMinute = -1;  // Son güncellenen dakika

    private void UpdateTime()
    {
        var now = DateTime.Now;
        
        // Sadece dakika değiştiğinde UI'ı güncelle (performans optimizasyonu)
        // Saniye değişimlerinde PropertyChanged tetiklenmez
        if (now.Minute == _lastMinute) return;
        
        _lastMinute = now.Minute;
        
        CurrentTime = Settings.Use24HourClock ? now.ToString("HH:mm") : now.ToString("hh:mm tt");
        CurrentDate = now.ToString("dd MMMM ddd", new CultureInfo("tr-TR"));

        // Saat başında zamanlı temayı kontrol et
        if (now.Minute == 0) ApplyScheduledThemeIfNeeded();
    }

    // ── Calendar ─────────────────────────────────────────────────────────────

    public void ResetCalendarToToday()
    {
        CalendarYear  = DateTime.Today.Year;
        CalendarMonth = DateTime.Today.Month;
        BuildCalendarDays();
    }

    [RelayCommand]
    private void NavigateCalendar(string delta)
    {
        if (!int.TryParse(delta, out int d)) return;
        var dt = new DateTime(CalendarYear, CalendarMonth, 1).AddMonths(d);
        CalendarYear  = dt.Year;
        CalendarMonth = dt.Month;
        BuildCalendarDays();
    }

    private void BuildCalendarDays()
    {
        CalendarDays.Clear();
        var today    = DateTime.Today;
        var culture  = new CultureInfo("tr-TR");
        var firstDay = new DateTime(CalendarYear, CalendarMonth, 1);
        CalendarMonthName = firstDay.ToString("MMMM yyyy", culture);

        int startOffset = ((int)firstDay.DayOfWeek + 6) % 7;
        int daysInMonth = DateTime.DaysInMonth(CalendarYear, CalendarMonth);

        for (int i = 0; i < startOffset; i++)
        {
            var d = firstDay.AddDays(-(startOffset - i));
            CalendarDays.Add(new CalendarDay { Day = d.Day, Date = d, IsCurrentMonth = false,
                IsWeekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday });
        }
        for (int n = 1; n <= daysInMonth; n++)
        {
            var d = new DateTime(CalendarYear, CalendarMonth, n);
            CalendarHelper.TurkishHolidays.TryGetValue((CalendarMonth, n), out var holiday);
            CalendarDays.Add(new CalendarDay
            {
                Day = n, Date = d, IsToday = d == today, IsCurrentMonth = true,
                IsWeekend    = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                IsSpecialDay = holiday != null, SpecialDayName = holiday,
            });
        }
        for (int i = 1; i <= 42 - CalendarDays.Count; i++)
        {
            var d = new DateTime(CalendarYear, CalendarMonth, daysInMonth).AddDays(i);
            CalendarDays.Add(new CalendarDay { Day = d.Day, Date = d, IsCurrentMonth = false,
                IsWeekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday });
        }
    }

    // ── Weather ───────────────────────────────────────────────────────────────

    private void LoadCities()
    {
        _weather.LoadCities();
        var provinces = _weather.Cities
            .Select(c => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(c.name));
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Provinces.Clear();
            foreach (var p in provinces) Provinces.Add(p);
        });
    }

    private void UpdateDistricts()
    {
        if (string.IsNullOrWhiteSpace(Settings.WeatherProvince)) return;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Districts.Clear();
            foreach (var d in _weather.GetDistrictsFor(Settings.WeatherProvince))
                Districts.Add(d);
            if (!Districts.Contains(Settings.WeatherDistrict))
                Settings.WeatherDistrict = string.Empty;
        });
        _weather.SetLocation(Settings.WeatherProvince, Settings.WeatherDistrict);
    }

    [RelayCommand]
    private async Task RefreshWeatherAsync()
    {
        WeatherCondition = "...";
        _weather.SetLocation(Settings.WeatherProvince, Settings.WeatherDistrict);
        var result = await _weather.FetchAsync();
        WeatherCondition = result.Condition;
        WeatherTooltip   = result.Tooltip;
        WeatherIcon      = result.Icon;
    }

    // ── Media commands ────────────────────────────────────────────────────────

    [RelayCommand] private async Task PreviousMediaAsync()  => await _media.PreviousAsync();
    [RelayCommand] private async Task PlayPauseMediaAsync() => await _media.PlayPauseAsync();
    [RelayCommand] private async Task NextMediaAsync()      => await _media.NextAsync();

    // ── Network commands ──────────────────────────────────────────────────────

    [RelayCommand] private async Task SelectWifiAsync(WifiInfo info)
        => await _network.SelectAsync(info, AvailableNetworks);

    [RelayCommand] private async Task ConnectWifiAsync(System.Windows.Controls.PasswordBox pb)
    {
        var info = AvailableNetworks.FirstOrDefault(n => n.IsPasswordPromptVisible);
        await _network.ConnectWithPasswordAsync(info, pb.Password);
        if (info?.StatusText == "Bağlı") pb.Password = string.Empty;
    }

    [RelayCommand] private async Task ForgetWifiAsync(WifiInfo info)   => await _network.ForgetAsync(info);
    [RelayCommand] private async Task DisconnectWifiAsync(WifiInfo info) => await _network.DisconnectAsync(info);

    // ── Bluetooth commands ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ToggleBluetoothPairingAsync(BluetoothDeviceInfo info)
    {
        if (info == null) return;
        HardwareService.OpenBluetoothSettings(info);
        await Task.Delay(5000);
        info.StatusText = string.Empty;
    }

    // ── Hardware commands ─────────────────────────────────────────────────────

    [RelayCommand]
    private void SetBrightness(int level)
    {
        _hardware.SetBrightness(level);
        BrightnessLevel = level;
    }

    // ── Shell commands ────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenTaskView()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            { FileName = "explorer.exe", Arguments = "shell:::{3080F90E-D7AD-11D9-BD98-0000947B0257}", UseShellExecute = true }); }
        catch { }
    }

    [RelayCommand] private void OpenVirtualDesktops() => NativeMethods.SimulateWinTab();

    [RelayCommand]
    private void TakeScreenshot()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            { FileName = "snippingtool", Arguments = "/clip", UseShellExecute = true }); }
        catch { }
    }

    // ── Settings commands ─────────────────────────────────────────────────────

    // Gradient animasyon tetikleyici — MainWindow tarafından dinlenir
    public event Action<string>? GradientThemeRequested;

    [RelayCommand]
    private void ApplyTheme(string name)
    {
        if (name == "Auto") { Settings.Theme = "Auto"; _ = DetectWallpaperColorAsync(); return; }

        // Gradient animasyonlu temalar
        if (GradientThemeService.IsGradientTheme(name))
        {
            var t = SettingsService.Themes.FirstOrDefault(x => x.Name == name);
            if (t != default)
            {
                Settings.Theme  = name;
                BarBackground   = t.Color;
                BarOpacity      = t.Opacity;
                ForegroundColor = "#FFFFFF";
                GradientThemeRequested?.Invoke(name);
            }
            SaveSettings();
            return;
        }

        // Düz renk temalar
        if (name == "Custom" && !string.IsNullOrWhiteSpace(Settings.CustomHexColor))
        {
            Settings.Theme  = "Custom";
            BarBackground   = Settings.CustomHexColor;
            ForegroundColor = "#FFFFFF";
        }
        else
        {
            var t = SettingsService.Themes.FirstOrDefault(x => x.Name == name);
            if (t != default) { Settings.Theme = name; BarBackground = t.Color; BarOpacity = t.Opacity; ForegroundColor = "#FFFFFF"; }
        }

        // Gradient animasyonu durdur (başka temaya geçildi)
        GradientThemeRequested?.Invoke("Stop");
        SaveSettings();
    }

    [RelayCommand]
    private void SaveSettings()
    {
        Settings.BarColor   = BarBackground;
        Settings.BarOpacity = BarOpacity;
        SettingsService.Save();
    }

    [RelayCommand]
    private void ApplyStartWithWindows(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (enable) key?.SetValue("Windows10MenuBar", $"\"{System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName}\"");
            else        key?.DeleteValue("Windows10MenuBar", throwOnMissingValue: false);
            Settings.StartWithWindows = enable;
            SaveSettings();
        }
        catch { }
    }

    // ── Scheduled theme ───────────────────────────────────────────────────────

    // ── Scheduled theme ───────────────────────────────────────────────────────

    private async Task RunScheduledThemeLoopAsync(System.Threading.CancellationToken ct)
    {
        // 1 dakika aralıklarla değil, her saat değişimini clock timer zaten yakalıyor.
        // Bu loop sadece başlangıçta bir kez çalışır, sonra dakikalık kontrol.
        ApplyScheduledThemeIfNeeded();
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(1), ct); }
            catch (OperationCanceledException) { break; }
            ApplyScheduledThemeIfNeeded();
        }
    }

    private void ApplyScheduledThemeIfNeeded()
    {
        if (!Settings.EnableScheduledTheme) return;

        int hour = DateTime.Now.Hour;
        bool shouldBeLight = hour >= Settings.LightThemeStartHour && hour < Settings.DarkThemeStartHour;

        // Şu anki tema ile hedef tema aynıysa değiştirme
        bool isCurrentlyLight = BarBackground is "#E8E8E8" or "#F0F0F0";
        if (shouldBeLight == isCurrentlyLight) return;

        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (shouldBeLight)
            {
                BarBackground   = "#E8E8E8";
                ForegroundColor = "#1A1A1A";
                BarOpacity      = 0.92;
            }
            else
            {
                BarBackground   = "#1A1A1A";
                ForegroundColor = "#FFFFFF";
                BarOpacity      = 0.92;
            }
            SaveSettings();
        });
    }

    // ── Wallpaper color detection ─────────────────────────────────────────────

    private async Task DetectWallpaperColorAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                string? path  = key?.GetValue("Wallpaper")?.ToString();
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;

                double brightness = WallpaperHelper.SampleBrightness(path);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Settings.Theme != "Auto") return;
                    if (brightness > 0.55) { BarBackground = "#E8E8E8"; ForegroundColor = "#1A1A1A"; BarOpacity = 0.88; }
                    else                   { BarBackground = "#1A1A1A"; ForegroundColor = "#FFFFFF";  BarOpacity = 0.92; }
                });
            }
            catch { }
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SyncStartWithWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: false);
            Settings.StartWithWindows = key?.GetValue("Windows10MenuBar") != null;
        }
        catch { }
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        // Timer'ları durdur
        _clockTimer?.Stop();
        
        // Event subscription'ları temizle
        if (_media != null)
            _media.MediaChanged -= OnMediaChanged;
        
        if (_notifications != null)
            _notifications.NewNotificationArrived -= OnNewNotificationArrived;
        
        // Settings event'ini temizle
        if (Settings != null)
            Settings.PropertyChanged -= OnSettingsPropertyChanged;
    }

    private void OnMediaChanged(MediaInfo info)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() => CurrentMedia = info,
            DispatcherPriority.Background);
    }

    private void OnNewNotificationArrived()
    {
        OnNewNotification?.Invoke();
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BarSettings.Use24HourClock)) UpdateTime();
        else if (e.PropertyName == nameof(BarSettings.WeatherProvince)) UpdateDistricts();
    }
}
