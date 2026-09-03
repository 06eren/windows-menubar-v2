using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Windows_10_MenuBar.Interop;
using Windows_10_MenuBar.Services;

namespace Windows_10_MenuBar;

public partial class MainWindow
{
    // ── Gradient theme animation ──────────────────────────────────────────────

    private Storyboard? _activeGradientSb;

    internal void OnGradientThemeRequested(string themeName)
    {
        // Önceki animasyonu temiz durdur
        _activeGradientSb?.Stop(this);
        _activeGradientSb = null;

        if (themeName == "Stop") return;

        try
        {
            // Cache'den al — her değişimde yeni Storyboard oluşturma
            var sb = GradientThemeService.GetStoryboard(themeName);

            // Freeze edilmiş sb doğrudan Begin ile kullanılamaz;
            // Clone alıp bu Window'un NameScope'una register et
            var sbClone = sb.Clone();
            sbClone.Begin(this, HandoffBehavior.SnapshotAndReplace, isControllable: true);
            _activeGradientSb = sbClone;
        }
        catch { }
    }

    // ── Notification flash ────────────────────────────────────────────────────

    /// <summary>
    /// Bar'ın tamamını kısa süre beyazca parlatır — yeni bildirim sinyali.
    /// FlashOverlay adlı Border'ın Opacity'sini Storyboard ile anime eder.
    /// </summary>
    internal void TriggerFlashAnimation()
    {
        try
        {
            var sb = (System.Windows.Media.Animation.Storyboard)
                FindResource("SbFlash");
            sb.Begin(this, true);
        }
        catch { }
    }

    // ── WndProc ───────────────────────────────────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_WINDOWPOSCHANGING = 0x0046;
        if (msg == WM_WINDOWPOSCHANGING && !_suppressPositionLock)
        {
            var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            if ((pos.flags & 0x0002) == 0)   // SWP_NOMOVE not set → clamp to 0,0
            {
                pos.x = 0;
                pos.y = 0;
                Marshal.StructureToPtr(pos, lParam, false);
            }
        }
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd, hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    // ── Fullscreen detection ──────────────────────────────────────────────────

    private void FullscreenTimer_Tick(object? sender, EventArgs e)
    {
        bool shouldHide = NativeMethods.IsFullscreenAppRunning(_ownHwnd)
                       && _viewModel.Settings.HideOnFullscreen;

        if (shouldHide && !_isHiddenByFullscreen)
        {
            _isHiddenByFullscreen = true;
            AppBarInterop.UnregisterBar(this);
            this.Topmost    = false;
            this.Visibility = Visibility.Hidden;
        }
        else if (!shouldHide && _isHiddenByFullscreen)
        {
            _isHiddenByFullscreen = false;
            this.Visibility = Visibility.Visible;
            this.Topmost    = true;
            AppBarInterop.RegisterBar(this, _viewModel.Settings.BarHeight);
            if (_viewModel.Settings.AutoHide) this.Opacity = 0;
        }
    }

    // ── Auto-hide ─────────────────────────────────────────────────────────────

    internal void SetupAutoHide()
    {
        _autoHideTimer?.Stop();
        _autoHideTimer = null;
        this.Opacity   = 0;

        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _autoHideTimer.Tick += (_, _) =>
        {
            if (_isHiddenByFullscreen) return;
            GetCursorPos(out var pt);
            this.Opacity = pt.Y <= _viewModel.Settings.BarHeight + 2 ? 1 : 0;
        };
        _autoHideTimer.Start();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);
}
