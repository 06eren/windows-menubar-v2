using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using Windows_10_MenuBar.Models;

namespace Windows_10_MenuBar.Services;

/// <summary>
/// Çoklu monitör yönetimi - her monitörde ayrı MainWindow instance'ı oluşturur ve yönetir
/// </summary>
public class MonitorManager : IDisposable
{
    private readonly List<MonitorBarInstance> _instances = new();
    private readonly BarSettings _settings;
    private bool _isInitialized = false;

    public MonitorManager(BarSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Monitörleri tespit et ve bar instance'larını oluştur
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        var monitors = DetectMonitors();
        
        // Settings'ten profilleri yükle veya oluştur
        var profiles = LoadOrCreateProfiles(monitors);
        
        // Her monitör için bar instance'ı oluştur
        foreach (var profile in profiles.Where(p => p.IsEnabled))
        {
            CreateBarForMonitor(profile);
        }

        // Monitör değişikliklerini dinle
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>
    /// Tüm monitörlerdeki bar'ları ayarlarla senkronize et
    /// </summary>
    public void SyncAllBars()
    {
        if (!_settings.EnableMultiMonitor || _settings.SyncMonitors)
        {
            // Sync mode: tüm bar'lar aynı ayarları kullanır
            foreach (var instance in _instances)
            {
                instance.Window._viewModel.Settings.Theme = _settings.Theme;
                instance.Window._viewModel.BarOpacity = _settings.BarOpacity;
                instance.Window._viewModel.Settings.BarHeight = _settings.BarHeight;
                instance.Window._viewModel.ApplyThemeCommand.Execute(_settings.Theme);
            }
        }
    }

    /// <summary>
    /// Belirli bir monitördeki bar'ı güncelle
    /// </summary>
    public void UpdateMonitor(int monitorIndex, MonitorProfile profile)
    {
        var instance = _instances.FirstOrDefault(i => i.Profile.MonitorIndex == monitorIndex);
        if (instance == null) return;

        instance.Profile = profile;

        if (!_settings.SyncMonitors)
        {
            // Her monitörün kendi ayarları var
            if (profile.CustomTheme != null)
                instance.Window._viewModel.ApplyThemeCommand.Execute(profile.CustomTheme);
            
            if (profile.CustomOpacity.HasValue)
                instance.Window._viewModel.BarOpacity = profile.CustomOpacity.Value;
            
            if (profile.CustomBarHeight.HasValue)
                instance.Window._viewModel.Settings.BarHeight = profile.CustomBarHeight.Value;
        }
    }

    /// <summary>
    /// Tüm monitör profillerini profillere eşitle (Profilleri Eşitle butonu)
    /// </summary>
    public void EqualizeProfiles()
    {
        if (_instances.Count == 0) return;

        // Primary monitor'ün ayarlarını al
        var primaryInstance = _instances.FirstOrDefault(i => i.Profile.IsPrimary);
        if (primaryInstance == null) 
            primaryInstance = _instances.First();

        var sourceProfile = primaryInstance.Profile;

        // Tüm monitörlere aynı özel ayarları uygula
        foreach (var instance in _instances)
        {
            instance.Profile.CustomTheme = sourceProfile.CustomTheme;
            instance.Profile.CustomOpacity = sourceProfile.CustomOpacity;
            instance.Profile.CustomBarHeight = sourceProfile.CustomBarHeight;
            
            UpdateMonitor(instance.Profile.MonitorIndex, instance.Profile);
        }

        SaveProfiles();
    }

    /// <summary>
    /// Profilleri kaydet
    /// </summary>
    public void SaveProfiles()
    {
        var profiles = _instances.Select(i => i.Profile).ToList();
        _settings.MonitorProfilesJson = JsonSerializer.Serialize(profiles);
        SettingsService.Save();
    }

    /// <summary>
    /// Tüm bar'ları getir
    /// </summary>
    public IReadOnlyList<MonitorBarInstance> GetInstances() => _instances.AsReadOnly();

    // ── Private Methods ───────────────────────────────────────────────────────

    private List<MonitorProfile> LoadOrCreateProfiles(List<MonitorInfo> monitors)
    {
        var profiles = new List<MonitorProfile>();

        try
        {
            // Kaydedilmiş profilleri yükle
            if (!string.IsNullOrEmpty(_settings.MonitorProfilesJson))
            {
                var saved = JsonSerializer.Deserialize<List<MonitorProfile>>(_settings.MonitorProfilesJson);
                if (saved != null) profiles = saved;
            }
        }
        catch { }

        // Yeni monitörler için profil oluştur
        foreach (var monitor in monitors)
        {
            var existing = profiles.FirstOrDefault(p => 
                p.DeviceName == monitor.DeviceName || 
                p.MonitorIndex == monitor.Index);

            if (existing == null)
            {
                profiles.Add(new MonitorProfile
                {
                    DeviceName = monitor.DeviceName,
                    MonitorIndex = monitor.Index,
                    IsEnabled = true,
                    ScreenLeft = monitor.Bounds.Left,
                    ScreenTop = monitor.Bounds.Top,
                    ScreenWidth = monitor.Bounds.Width,
                    ScreenHeight = monitor.Bounds.Height,
                    IsPrimary = monitor.IsPrimary
                });
            }
            else
            {
                // Pozisyon bilgilerini güncelle (monitör değişmiş olabilir)
                existing.ScreenLeft = monitor.Bounds.Left;
                existing.ScreenTop = monitor.Bounds.Top;
                existing.ScreenWidth = monitor.Bounds.Width;
                existing.ScreenHeight = monitor.Bounds.Height;
                existing.IsPrimary = monitor.IsPrimary;
            }
        }

        // Artık olmayan monitörleri kaldır
        profiles.RemoveAll(p => !monitors.Any(m => m.DeviceName == p.DeviceName));

        SaveProfiles();
        return profiles;
    }

    private void CreateBarForMonitor(MonitorProfile profile)
    {
        var window = new MainWindow();
        
        // Pencereyi monitöre konumlandır
        window.Left = profile.ScreenLeft;
        window.Top = profile.ScreenTop;
        window.Width = profile.ScreenWidth;

        // Özel ayarları uygula (sync kapalıysa)
        if (!_settings.SyncMonitors)
        {
            if (profile.CustomTheme != null)
                window._viewModel.Settings.Theme = profile.CustomTheme;
            
            if (profile.CustomOpacity.HasValue)
                window._viewModel.BarOpacity = profile.CustomOpacity.Value;
            
            if (profile.CustomBarHeight.HasValue)
                window._viewModel.Settings.BarHeight = profile.CustomBarHeight.Value;
        }

        window.Show();

        _instances.Add(new MonitorBarInstance
        {
            Window = window,
            Profile = profile
        });
    }

    private List<MonitorInfo> DetectMonitors()
    {
        var monitors = new List<MonitorInfo>();
        int index = 0;

        foreach (var screen in Screen.AllScreens)
        {
            monitors.Add(new MonitorInfo
            {
                Index = index++,
                DeviceName = screen.DeviceName,
                IsPrimary = screen.Primary,
                Bounds = new Rect(
                    screen.Bounds.X,
                    screen.Bounds.Y,
                    screen.Bounds.Width,
                    screen.Bounds.Height
                )
            });
        }

        return monitors;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Monitör eklendi/çıkarıldı - yeniden başlat
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Reinitialize();
        });
    }

    private void Reinitialize()
    {
        // Mevcut instance'ları kapat
        foreach (var instance in _instances)
        {
            instance.Window.Close();
        }
        _instances.Clear();

        // Yeniden başlat
        _isInitialized = false;
        Initialize();
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        
        foreach (var instance in _instances)
        {
            try { instance.Window.Close(); }
            catch { }
        }
        _instances.Clear();
    }
}

/// <summary>
/// Monitör bilgisi (tespit için)
/// </summary>
internal class MonitorInfo
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public Rect Bounds { get; set; }
}

/// <summary>
/// Her monitör için bar instance'ı
/// </summary>
public class MonitorBarInstance
{
    public MainWindow Window { get; set; } = null!;
    public MonitorProfile Profile { get; set; } = null!;
}
