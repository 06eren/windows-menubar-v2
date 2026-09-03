using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NAudio.CoreAudioApi;
using Windows_10_MenuBar.Interop;
using Windows_10_MenuBar.Services;

namespace Windows_10_MenuBar;

public partial class MainWindow
{
    // ── Bar visuals ───────────────────────────────────────────────────────────

    internal void ApplyBarVisuals()
    {
        try
        {
            // Gradient brush — her iki stop'u aynı renge ayarla (düz renk gibi)
            if (BarStop0 != null && BarStop1 != null)
            {
                var color = (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter.ConvertFromString(_viewModel.BarBackground);
                BarStop0.Color = color;
                BarStop1.Color = color;
            }
            BarGradientBrush.Opacity = _viewModel.BarOpacity;
            this.Height = _viewModel.Settings.BarHeight;
            AppBarInterop.RegisterBar(this, _viewModel.Settings.BarHeight);
        }
        catch { }
    }

    // ── Scroll → ses seviyesi ─────────────────────────────────────────────────

    /// <summary>
    /// Bar'ın herhangi bir boş alanı üzerinde scroll yapılınca ses seviyesini
    /// ±2 birim değiştirir. Buton/popup gibi interaktif elementlerde event
    /// zaten o elemente gideceği için buraya ulaşmaz.
    /// </summary>
    private void Grid_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Sadece bir popup açıksa ses değiştirme
        if (SettingsPopup.IsOpen) return;

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var vol = device.AudioEndpointVolume;

            // Her 120 delta birimi = 1 scroll adımı → ±2%
            int steps  = e.Delta / 120;
            float delta = steps * 0.02f;

            float newVol = Math.Clamp(vol.MasterVolumeLevelScalar + delta, 0f, 1f);
            vol.MasterVolumeLevelScalar = newVol;

            // ViewModel'i anında güncelle — NAudio event zaten tetiklenecek ama
            // gecikmeyi önlemek için burada da set ediyoruz
            _viewModel.VolumeLevel = (int)(newVol * 100);
        }
        catch { }

        // Scroll olayının üstteki pencereye (masaüstü vb.) iletilmesini engelle
        e.Handled = true;
    }

    // ── Çift tık → Masaüstü göster (Win+D) ──────────────────────────────────

    /// <summary>
    /// Bar'ın boş alanına çift tıklanınca tüm pencereler minimize edilir.
    /// MouseLeftButtonDown + ClickCount=2 ile tespit edilir (Grid MouseDoubleClick desteklemez).
    /// </summary>
    private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (SettingsPopup.IsOpen) return;

        NativeMethods.MinimizeAll();
        e.Handled = true;
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e) { }

    internal void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.PlacementTarget  = this;
        SettingsPopup.HorizontalOffset = SystemParameters.PrimaryScreenWidth - 380;
        SettingsPopup.VerticalOffset   = this.Height;
        SettingsPopup.IsOpen           = true;
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e)
        => SettingsPopup.IsOpen = false;

    // ── Sliders ───────────────────────────────────────────────────────────────

    private void OpacitySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel == null) return;
        _viewModel.BarOpacity = e.NewValue;
        try { BarGradientBrush.Opacity = e.NewValue; } catch { }
        SettingsService.Save();
    }

    private void HeightSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel == null) return;
        double h = Math.Round(e.NewValue / 2) * 2;
        this.Height = h;
        _viewModel.Settings.BarHeight = h;
        AppBarInterop.RegisterBar(this, h);
        SettingsService.Save();
    }

    private void BrightnessSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        => _viewModel?.SetBrightnessCommand.Execute((int)e.NewValue);

    // ── Toggles ───────────────────────────────────────────────────────────────

    private void Settings_Changed(object sender, RoutedEventArgs e)
        => SettingsService.Save();

    private void AutoHide_Changed(object sender, RoutedEventArgs e)
    {
        SettingsService.Save();
        if (_viewModel.Settings.AutoHide)
            SetupAutoHide();
        else
        {
            _autoHideTimer?.Stop();
            _autoHideTimer = null;
            this.Opacity = 1;
        }
    }

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
        => _viewModel.ApplyStartWithWindowsCommand.Execute(_viewModel.Settings.StartWithWindows);

    private void BtnClose_Click(object sender, RoutedEventArgs e)
        => System.Windows.Application.Current.Shutdown();

    // ── Popups ────────────────────────────────────────────────────────────────

    private void PopupWifi_Closed(object sender, EventArgs e)
    {
        if (_viewModel == null) return;
        foreach (var n in _viewModel.AvailableNetworks)
        {
            n.IsPasswordPromptVisible = false;
            if (n.StatusText == "Şifre Gerekli") n.StatusText = string.Empty;
        }
    }

    private void CalendarPopup_Opened(object sender, EventArgs e)
        => _viewModel?.ResetCalendarToToday();

    private void CalendarTodayBtn_Click(object sender, RoutedEventArgs e)
        => _viewModel?.ResetCalendarToToday();
}
