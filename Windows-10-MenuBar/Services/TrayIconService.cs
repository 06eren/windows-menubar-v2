using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Windows_10_MenuBar.Interop;
using Windows_10_MenuBar.UI;
using WinForms = System.Windows.Forms;

namespace Windows_10_MenuBar.Services;

/// <summary>
/// Windows sistem tepsisi (NotifyIcon) yönetimi.
/// Bar'ı göster/gizle ve uygulamayı kapatma menüsü sağlar.
/// Tooltip'te hava durumu + saat bilgisi gösterilir.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly WinForms.NotifyIcon        _notifyIcon;
    private readonly WinForms.ContextMenuStrip  _menu;
    private WinForms.ToolStripMenuItem          _toggleItem   = null!;
    private WinForms.ToolStripLabel             _weatherLabel = null!;
    private Icon? _currentIcon;  // Dispose için referans tut

    public event EventHandler<bool>? VisibilityToggled;

    private bool _barVisible = true;

    public TrayIconService()
    {
        _menu = BuildContextMenu();
        _currentIcon = CreateDefaultIcon();

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon             = _currentIcon,
            Text             = "Windows MenuBar",
            Visible          = true,
            ContextMenuStrip = _menu,
        };

        // Sadece sağ tık menüsü — sol tık/çift tık bar'a dokunmaz
        // ContextMenuStrip zaten sağ tıkta otomatik açılır
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void UpdateTooltip(string weather, string time, string date)
    {
        string full = $"MenuBar  {weather}\n{date}  {time}";
        _notifyIcon.Text = full.Length > 63 ? full[..63] : full;

        if (_weatherLabel != null)
            _weatherLabel.Text = $"  {weather}  ·  {time}";
    }

    public void SyncVisibility(bool isVisible)
    {
        _barVisible = isVisible;
        UpdateToggleItem();
    }

    // ── Özel Metodlar ────────────────────────────────────────────────────────

    private void ToggleBar()
    {
        _barVisible = !_barVisible;
        UpdateToggleItem();
        VisibilityToggled?.Invoke(this, _barVisible);
    }

    private void UpdateToggleItem()
    {
        if (_toggleItem == null) return;
        _toggleItem.Text = _barVisible ? "✕  Bar'ı Gizle" : "✓  Bar'ı Göster";
        _toggleItem.Font = new Font(
            _toggleItem.Font ?? System.Drawing.SystemFonts.MenuFont!,
            _barVisible ? FontStyle.Regular : FontStyle.Bold);
    }

    private WinForms.ContextMenuStrip BuildContextMenu()
    {
        var menu = new WinForms.ContextMenuStrip
        {
            BackColor       = Color.FromArgb(30, 30, 30),
            ForeColor       = Color.White,
            ShowImageMargin = false,
            RenderMode      = WinForms.ToolStripRenderMode.Professional,
            Renderer        = new DarkMenuRenderer(),
        };

        // Hava durumu / saat bilgisi satırı (salt bilgi)
        _weatherLabel = new WinForms.ToolStripLabel("  Yükleniyor...")
        {
            ForeColor = Color.FromArgb(140, 180, 255),
            Font      = new Font("Segoe UI Variable Text", 9f, FontStyle.Regular),
            Enabled   = false,
        };
        menu.Items.Add(_weatherLabel);
        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Bar'ı Göster / Gizle
        _toggleItem = new WinForms.ToolStripMenuItem("✕  Bar'ı Gizle")
        {
            ForeColor = Color.White,
            Font      = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Regular),
        };
        _toggleItem.Click += (_, _) => ToggleBar();
        menu.Items.Add(_toggleItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Ayarlar
        var settingsItem = new WinForms.ToolStripMenuItem("⚙  Ayarlar")
        {
            ForeColor = Color.White,
            Font      = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Regular),
        };
        settingsItem.Click += (_, _) =>
        {
            if (System.Windows.Application.Current.MainWindow is MainWindow mw)
            {
                if (!_barVisible) { _barVisible = true; VisibilityToggled?.Invoke(this, true); }
                mw.OpenSettingsFromTray();
            }
        };
        menu.Items.Add(settingsItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Çıkış
        var exitItem = new WinForms.ToolStripMenuItem("⏻  Çıkış")
        {
            ForeColor = Color.FromArgb(255, 80, 80),
            Font      = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Regular),
        };
        exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();
        menu.Items.Add(exitItem);

        return menu;
    }

    private static Icon CreateDefaultIcon()
    {
        try
        {
            // Önce Assets/icon.ico dosyasını dene
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string iconPath = Path.Combine(basePath, "Assets", "icon.ico");
            
            if (File.Exists(iconPath))
            {
                // 16x16 boyutu seç - system tray için ideal boyut
                return new Icon(iconPath, 16, 16);
            }

            // Yoksa EXE'den çıkar
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var extracted = Icon.ExtractAssociatedIcon(exePath);
                if (extracted != null) return extracted;
            }
        }
        catch { }

        return CreateFallbackIcon();
    }

    private static Icon CreateFallbackIcon()
    {
        var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            using var barBrush = new SolidBrush(Color.White);
            g.FillRectangle(barBrush, 1, 3, 14, 3);  // üst bar şeridi
            g.FillRectangle(barBrush, 3, 9,  4, 4);  // sol nokta
            g.FillRectangle(barBrush, 9, 9,  4, 4);  // sağ nokta
        }

        // GetHicon() handle'ını DestroyIcon ile temizlemek gerekir
        // ama Icon.FromHandle zaten handle'ı yönetir
        IntPtr hIcon = bmp.GetHicon();
        Icon icon = Icon.FromHandle(hIcon);
        
        // Bitmap'i dispose et ama icon'u döndür
        // Icon clone et ki bitmap dispose olduktan sonra da çalışsın
        Icon result = (Icon)icon.Clone();
        
        // Handle'ı serbest bırak
        NativeMethods.DestroyIcon(hIcon);
        bmp.Dispose();
        
        return result;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Icon = null;  // Icon referansını kaldır
        _notifyIcon.Dispose();
        
        // Icon'u dispose et (GDI handle leak önleme)
        _currentIcon?.Dispose();
        _currentIcon = null;
        
        _menu.Dispose();
    }
}
