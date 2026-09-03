using System.Threading;
using Windows_10_MenuBar.Services;

namespace Windows_10_MenuBar;

public partial class App : System.Windows.Application
{
    public static TrayIconService? TrayIcon { get; private set; }
    public static MonitorManager? MonitorManager { get; private set; }

    /// <summary>
    /// Tüm arka plan servis loop'larını iptal etmek için kullanılır.
    /// OnExit'te Cancel() çağrılır.
    /// </summary>
    public static CancellationTokenSource AppCts { get; } = new();

    private MainWindow? _primaryWindow;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        TrayIcon = new TrayIconService();

        var mainWindow = new MainWindow();
        _primaryWindow = mainWindow;
        MainWindow = mainWindow;

        // Çoklu monitör desteğini ayarlardan kontrol et
        if (mainWindow._viewModel.Settings.EnableMultiMonitor)
        {
            // MonitorManager tüm monitörleri yönetir
            MonitorManager = new MonitorManager(mainWindow._viewModel.Settings);
            MonitorManager.Initialize();
            
            // Primary window'u gizle (MonitorManager her monitörde bar açtı)
            mainWindow.Hide();
        }
        else
        {
            // Tek monitör modu - sadece primary window'u göster
            mainWindow.Show();
        }

        TrayIcon.VisibilityToggled += (_, isVisible) =>
        {
            if (mainWindow._viewModel.Settings.EnableMultiMonitor && MonitorManager != null)
            {
                // Çoklu monitör: tüm bar'ları göster/gizle
                var instances = MonitorManager.GetInstances();
                foreach (var instance in instances)
                {
                    if (isVisible) instance.Window.ShowBar();
                    else           instance.Window.HideBar();
                }
            }
            else
            {
                // Tek monitör
                if (isVisible) mainWindow.ShowBar();
                else           mainWindow.HideBar();
            }
            TrayIcon.SyncVisibility(isVisible);
        };
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        AppCts.Cancel();      // tüm loop'lara dur sinyali
        AppCts.Dispose();
        MonitorManager?.Dispose();
        TrayIcon?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Çoklu monitör modunu aç/kapat
    /// </summary>
    public static void ToggleMultiMonitor(bool enable, MainWindow primaryWindow)
    {
        if (enable)
        {
            // Çoklu monitör modunu aktifleştir
            if (MonitorManager == null)
            {
                MonitorManager = new MonitorManager(primaryWindow._viewModel.Settings);
                MonitorManager.Initialize();
            }
            
            // Primary window'u gizle
            primaryWindow.Hide();
        }
        else
        {
            // Çoklu monitör modunu kapat
            if (MonitorManager != null)
            {
                MonitorManager.Dispose();
                MonitorManager = null;
            }
            
            // Primary window'u göster
            primaryWindow.Show();
        }
    }

    /// <summary>
    /// Tüm monitörleri senkronize et
    /// </summary>
    public static void SyncAllMonitors()
    {
        MonitorManager?.SyncAllBars();
    }

    /// <summary>
    /// Monitör profillerini eşitle
    /// </summary>
    public static void EqualizeMonitorProfiles()
    {
        MonitorManager?.EqualizeProfiles();
    }
}
