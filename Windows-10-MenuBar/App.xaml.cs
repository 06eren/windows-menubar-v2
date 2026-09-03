using System.Threading;
using Windows_10_MenuBar.Services;

namespace Windows_10_MenuBar;

public partial class App : System.Windows.Application
{
    public static TrayIconService? TrayIcon { get; private set; }

    /// <summary>
    /// Tüm arka plan servis loop'larını iptal etmek için kullanılır.
    /// OnExit'te Cancel() çağrılır.
    /// </summary>
    public static CancellationTokenSource AppCts { get; } = new();

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        TrayIcon = new TrayIconService();

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();

        TrayIcon.VisibilityToggled += (_, isVisible) =>
        {
            if (isVisible) mainWindow.ShowBar();
            else           mainWindow.HideBar();
            TrayIcon.SyncVisibility(isVisible);
        };
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        AppCts.Cancel();      // tüm loop'lara dur sinyali
        AppCts.Dispose();
        TrayIcon?.Dispose();
        base.OnExit(e);
    }
}
