using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows_10_MenuBar.Interop;
using Windows_10_MenuBar.Services;
using Windows_10_MenuBar.ViewModels;

namespace Windows_10_MenuBar;

public partial class MainWindow : Window
{
    internal BarViewModel _viewModel = null!;
    private DispatcherTimer? _windowTitleTimer;
    private DispatcherTimer? _fullscreenTimer;
    private DispatcherTimer? _autoHideTimer;
    internal IntPtr _ownHwnd;
    internal bool _suppressPositionLock;
    internal bool _isHiddenByFullscreen;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new BarViewModel();
        DataContext = _viewModel;

        this.Width = SystemParameters.PrimaryScreenWidth;
        this.Top   = 0;
        this.Left  = 0;

        ApplyBarVisuals();

        Loaded  += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed  += MainWindow_Closed;

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(_viewModel.BarBackground) or nameof(_viewModel.BarOpacity))
                ApplyBarVisuals();
        };
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _ownHwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.HideFromAltTab(_ownHwnd);

        var src = HwndSource.FromHwnd(_ownHwnd);
        src?.AddHook(WndProc);

        AppBarInterop.RegisterBar(this, _viewModel.Settings.BarHeight);

        // Timer'ları düşük öncelikli DispatcherPriority ile başlat
        // UI render'ı bloke etmemek için Background priority kullan
        _windowTitleTimer = new DispatcherTimer(DispatcherPriority.Background) 
        { 
            Interval = TimeSpan.FromMilliseconds(1000)  // 500ms → 1000ms (daha az update)
        };
        _windowTitleTimer.Tick += (_, _) =>
        {
            var t = WindowHelper.GetActiveWindowTitle(_ownHwnd);
            if (!string.IsNullOrWhiteSpace(t)) _viewModel.ActiveWindowTitle = t;
        };
        _windowTitleTimer.Start();

        _fullscreenTimer = new DispatcherTimer(DispatcherPriority.Background) 
        { 
            Interval = TimeSpan.FromMilliseconds(1000)  // 750ms → 1000ms
        };
        _fullscreenTimer.Tick += FullscreenTimer_Tick;
        _fullscreenTimer.Start();

        if (_viewModel.Settings.AutoHide) SetupAutoHide();

        // Gradient tema event'ini dinle
        _viewModel.GradientThemeRequested += OnGradientThemeRequested;

        // Başlangıçta kaydedilmiş tema gradient'sa yeniden başlat
        if (GradientThemeService.IsGradientTheme(_viewModel.Settings.Theme))
            OnGradientThemeRequested(_viewModel.Settings.Theme);

        // Bildirim gelince flash - Background priority ile
        _viewModel.OnNewNotification = () => Dispatcher.InvokeAsync(
            TriggerFlashAnimation, 
            DispatcherPriority.Background);
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _fullscreenTimer?.Stop();
        _windowTitleTimer?.Stop();
        _autoHideTimer?.Stop();
        AppBarInterop.UnregisterBar(this);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _fullscreenTimer?.Stop();
        _windowTitleTimer?.Stop();
        _autoHideTimer?.Stop();
        
        // ViewModel'i dispose et - memory leak önleme
        _viewModel?.Dispose();
    }

    // ── Tray API ──────────────────────────────────────────────────────────────

    public void OpenSettingsFromTray()
    {
        if (!this.IsVisible) ShowBar();
        OpenSettings_Click(this, new RoutedEventArgs());
    }

    public void ShowBar()
    {
        this.Show();
        this.Topmost = true;
        AppBarInterop.RegisterBar(this, _viewModel.Settings.BarHeight);
    }

    public void HideBar()
    {
        _suppressPositionLock = true;
        this.Top = -(this.ActualHeight + 10);
        AppBarInterop.UnregisterBar(this);
        this.Hide();
        _suppressPositionLock = false;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Alt+F4 veya X butonu ile kapatma girişimlerini engelle
        // Sadece tray menu'den çıkış yapılabilsin
        if (App.TrayIcon != null)
        {
            e.Cancel = true;
            HideBar();
            App.TrayIcon.SyncVisibility(false);
        }
        else
        {
            base.OnClosing(e);
        }
    }
}
