using System;
using System.Runtime.InteropServices;

namespace Windows_10_MenuBar.Interop;

public static class NativeMethods
{
    private const byte VK_LWIN = 0x5B;
    private const byte VK_TAB = 0x09;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    /// <summary>Simulates Win+Tab to open Task View / virtual desktops</summary>
    public static void SimulateWinTab()
    {
        keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
        keybd_event(VK_TAB,  0, 0, UIntPtr.Zero);
        keybd_event(VK_TAB,  0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    // ── Minimize All ─────────────────────────────────────────────────────────

    private const byte VK_D   = 0x44;
    private const byte VK_M   = 0x4D;

    /// <summary>
    /// Tüm pencereleri minimize eder — Win+D simüle eder.
    /// Tekrar çağrılırsa pencereleri geri getirir (toggle davranışı).
    /// </summary>
    public static void MinimizeAll()
    {
        keybd_event(VK_LWIN, 0, 0,              UIntPtr.Zero);
        keybd_event(VK_D,    0, 0,              UIntPtr.Zero);
        keybd_event(VK_D,    0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    // ── Fullscreen detection ─────────────────────────────────────────────────

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public const int GWL_EXSTYLE      = -20;
    public const int WS_EX_TOPMOST    = 0x00000008;
    public const int WS_EX_TOOLWINDOW = 0x00000080; // Alt+Tab ve görev çubuğundan gizler
    public const int WS_EX_APPWINDOW  = 0x00040000; // Görev çubuğunda zorla gösterir (kaldırmak için)
    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    /// <summary>
    /// Pencereyi Alt+Tab listesinden ve görev çubuğundan tamamen gizler.
    /// WS_EX_TOOLWINDOW ekler, WS_EX_APPWINDOW kaldırır.
    /// </summary>
    public static void HideFromAltTab(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle = (exStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    // ── Fullscreen detection — ek P/Invoke ─────────────────────────────────

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    private const uint GW_OWNER = 4;

    // GWL_STYLE için kullanılan pencere stil sabitleri
    public const int GWL_STYLE    = -16;
    public const int WS_CAPTION   = 0x00C00000; // Başlık çubuğu (titlebar)
    public const int WS_BORDER    = 0x00800000; // Kenarlık
    public const int WS_MAXIMIZE  = 0x01000000; // Maximize edilmiş
    public const int WS_THICKFRAME= 0x00040000; // Yeniden boyutlandırılabilir kenarlık

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    /// <summary>
    /// Foreground penceresi gerçekten tam ekran mı?
    ///
    /// Strateji — hem rect hem stil kontrolü:
    ///   1. Monitörü ±1px toleransla kapatıyor mu?
    ///   2. Titlebar (WS_CAPTION) yok mu?  ← Chrome F11, VLC, oyunlar burada yakalanır
    ///   3. Shell/desktop penceresi değil mi?
    ///
    /// Sadece rect karşılaştırması yeterli değil çünkü maximize pencereler de
    /// monitörü kapatıyor. Sadece WS_MAXIMIZE kontrolü de yeterli değil çünkü
    /// Chrome F11 WS_MAXIMIZE ekliyor ama aynı zamanda WS_CAPTION kaldırıyor.
    /// İkisini birden kontrol etmek en güvenilir yöntem.
    /// </summary>
    public static bool IsFullscreenAppRunning(IntPtr barHwnd)
    {
        try
        {
            IntPtr fgWnd = GetForegroundWindow();
            if (fgWnd == IntPtr.Zero || fgWnd == barHwnd)
                return false;

            if (!IsWindowVisible(fgWnd))
                return false;

            // Shell/sistem pencerelerini erken elemek
            var sb = new System.Text.StringBuilder(256);
            GetClassName(fgWnd, sb, sb.Capacity);
            string cls = sb.ToString();
            if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd"
                     or "DV2ControlHost" or "MsgrIMEWindowClass"
                     or "SysShadow" or "Button" or "Windows.UI.Core.CoreWindow")
                return false;

            // Pencere stil kontrolü:
            // Gerçek fullscreen → titlebar yok (WS_CAPTION = 0)
            // Normal maximize   → titlebar var (WS_CAPTION != 0)
            // Chrome F11        → WS_MAXIMIZE var ama WS_CAPTION yok  ✓
            // VS Code maximize  → WS_MAXIMIZE var, WS_CAPTION var      ✗
            int style = GetWindowLong(fgWnd, GWL_STYLE);
            bool hasTitleBar = (style & WS_CAPTION) != 0;
            if (hasTitleBar)
                return false; // Başlık çubuğu olan pencere fullscreen olamaz

            // Rect kontrolü — titlebar'ı olmayan pencere monitörü kapatıyor mu?
            if (!GetWindowRect(fgWnd, out RECT wndRect))
                return false;

            IntPtr monitor = MonitorFromWindow(fgWnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
                return false;

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (!GetMonitorInfo(monitor, ref mi))
                return false;

            RECT mon = mi.rcMonitor;

            // ±1 px tolerans — gerçek fullscreen neredeyse birebir örtüşür
            // (DWM'in invisible border'ı fullscreen modda devreye girmiyor)
            bool coversMonitor = wndRect.Left   <= mon.Left   + 1
                              && wndRect.Top    <= mon.Top    + 1
                              && wndRect.Right  >= mon.Right  - 1
                              && wndRect.Bottom >= mon.Bottom - 1;

            return coversMonitor;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr hIcon);
}
