using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Windows_10_MenuBar.Services;

public class DynamicThemeService : IDisposable
{
    private readonly Dictionary<string, string> _appColorCache = new();
    private readonly DispatcherTimer _colorTimer;
    private string _lastActiveWindow = "";
    private string _currentThemeColor = "#1A1A1A";
    private bool _useContentColor = true; // İçerik tabanlı renk
    private DateTime _lastColorChange = DateTime.MinValue;

    // Popüler uygulamalar için önceden tanımlı renkler
    private readonly Dictionary<string, string> _knownAppColors = new()
    {
        // Browsers
        ["chrome"] = "#4285F4",
        ["firefox"] = "#FF7139", 
        ["edge"] = "#0078D4",
        ["opera"] = "#FF1B2D",
        ["brave"] = "#FB542B",
        
        // Development
        ["code"] = "#007ACC",
        ["devenv"] = "#5C2D91",  // Visual Studio
        ["rider"] = "#000000",
        ["sublime_text"] = "#FF9800",
        ["notepad++"] = "#90C695",
        
        // Media
        ["spotify"] = "#1DB954",
        ["vlc"] = "#FF8800",
        ["potplayermini64"] = "#1976D2",
        ["wmplayer"] = "#00BCF2",
        
        // Social/Communication
        ["discord"] = "#5865F2",
        ["slack"] = "#4A154B",
        ["teams"] = "#6264A7",
        ["zoom"] = "#2D8CFF",
        ["whatsapp"] = "#25D366",
        
        // Gaming
        ["steam"] = "#1B2838",
        ["epicgameslauncher"] = "#313131",
        ["battle.net"] = "#00AEFF",
        
        // Productivity
        ["winword"] = "#2B579A",      // Word
        ["excel"] = "#217346",        // Excel
        ["powerpnt"] = "#D24726",     // PowerPoint
        ["outlook"] = "#0078D4",      // Outlook
        ["onenote"] = "#7719AA",      // OneNote
        
        // Default fallbacks
        ["explorer"] = "#0078D4",     // File Explorer
        ["cmd"] = "#0C0C0C",          // Command Prompt
        ["powershell"] = "#012456",   // PowerShell
        ["taskmgr"] = "#2D2D30"       // Task Manager
    };

    public event Action<string>? ThemeColorChanged;

    public DynamicThemeService()
    {
        // 2 saniyede bir aktif pencereyi kontrol et
        _colorTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _colorTimer.Tick += ColorTimer_Tick;
    }

    public void Start()
    {
        if (!_colorTimer.IsEnabled)
            _colorTimer.Start();
    }

    public void Stop()
    {
        _colorTimer.Stop();
    }

    public void SetUseContentColor(bool useContent)
    {
        _useContentColor = useContent;
        _appColorCache.Clear(); // Cache'i temizle, yeni metodu kullan
    }

    private void ColorTimer_Tick(object? sender, EventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                string activeWindow = GetActiveWindowProcessName();
                
                if (activeWindow != _lastActiveWindow && !string.IsNullOrEmpty(activeWindow))
                {
                    _lastActiveWindow = activeWindow;
                    string color = await GetColorForApp(activeWindow);
                    
                    if (color != _currentThemeColor)
                    {
                        // Throttling: Son değişimden 1 saniye geçmeden yeni değişim yapma
                        // (Animasyon varken flickering'i önler)
                        var timeSinceLastChange = (DateTime.Now - _lastColorChange).TotalMilliseconds;
                        if (timeSinceLastChange < 1000)
                        {
                            System.Diagnostics.Debug.WriteLine($"Throttled color change: {color} (last change {timeSinceLastChange:F0}ms ago)");
                            return;
                        }
                        
                        _currentThemeColor = color;
                        _lastColorChange = DateTime.Now;
                        
                        // UI thread'de event fire et
                        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                        {
                            ThemeColorChanged?.Invoke(color);
                        }, DispatcherPriority.Background);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Dynamic theme error: {ex.Message}");
            }
        });
    }

    private async Task<string> GetColorForApp(string processName)
    {
        string lowercaseName = processName.ToLowerInvariant();
        
        // Cache'de var mı kontrol et (process+mode kombinasyonu)
        string cacheKey = $"{lowercaseName}_{_useContentColor}";
        if (_appColorCache.TryGetValue(cacheKey, out string? cachedColor))
            return cachedColor;
        
        string extractedColor;
        
        if (_useContentColor)
        {
            // İçerik tabanlı renk - aktif pencerenin screenshot'ını al
            extractedColor = await ExtractWindowContentColor();
        }
        else
        {
            // Logo tabanlı renk - önceden tanımlı renkleri kontrol et
            if (_knownAppColors.TryGetValue(lowercaseName, out string? knownColor))
            {
                _appColorCache[cacheKey] = knownColor;
                return knownColor;
            }
            
            // Icon'dan renk çıkar
            extractedColor = await ExtractIconColor(processName);
        }
        
        _appColorCache[cacheKey] = extractedColor;
        return extractedColor;
    }

    private async Task<string> ExtractWindowContentColor()
    {
        return await Task.Run(() =>
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                {
                    Debug.WriteLine("No foreground window, using fallback");
                    return "#1A1A1A";
                }

                // Pencere boyutlarını al
                if (!GetWindowRect(hwnd, out RECT rect))
                {
                    Debug.WriteLine("GetWindowRect failed, using fallback");
                    return "#1A1A1A";
                }

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                // Çok küçük pencereler veya tam ekran için fallback
                if (width < 100 || height < 100 || width > 10000 || height > 10000)
                {
                    Debug.WriteLine($"Invalid window size ({width}x{height}), using fallback");
                    return "#1A1A1A";
                }

                // Screenshot boyutunu sınırla (performans için)
                int cropX = (int)(width * 0.25);
                int cropY = (int)(height * 0.25);
                int cropWidth = Math.Min((int)(width * 0.5), 800);  // Max 800px genişlik
                int cropHeight = Math.Min((int)(height * 0.5), 600); // Max 600px yükseklik

                // Çok küçük crop area için fallback
                if (cropWidth < 50 || cropHeight < 50)
                {
                    Debug.WriteLine($"Crop area too small ({cropWidth}x{cropHeight}), using fallback");
                    return "#1A1A1A";
                }

                using var bitmap = new Bitmap(cropWidth, cropHeight);
                using var graphics = Graphics.FromImage(bitmap);
                
                try
                {
                    // Aktif pencerenin merkezinden screenshot al
                    graphics.CopyFromScreen(
                        rect.Left + cropX, 
                        rect.Top + cropY, 
                        0, 0, 
                        new System.Drawing.Size(cropWidth, cropHeight)
                    );
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Bazı pencereler screenshot'a izin vermiyor (UAC, güvenlik)
                    Debug.WriteLine("CopyFromScreen failed (access denied), using icon fallback");
                    
                    // Icon'a fallback
                    try
                    {
                        GetWindowThreadProcessId(hwnd, out uint processId);
                        using var process = System.Diagnostics.Process.GetProcessById((int)processId);
                        string? exePath = process.MainModule?.FileName;
                        
                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            using var icon = Icon.ExtractAssociatedIcon(exePath);
                            if (icon != null)
                            {
                                using var iconBitmap = icon.ToBitmap();
                                return GetDominantColor(iconBitmap);
                            }
                        }
                    }
                    catch
                    {
                        Debug.WriteLine("Icon fallback also failed");
                    }
                    
                    return "#1A1A1A";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Screenshot error: {ex.Message}");
                    return "#1A1A1A";
                }

                var color = GetDominantColor(bitmap);
                Debug.WriteLine($"Extracted content color: {color}");
                return color;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Window content color extraction error: {ex.Message}");
                return "#1A1A1A";
            }
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    private async Task<string> ExtractIconColor(string processName)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Process'i bul
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0) return "#1A1A1A";
                
                var process = processes[0];
                string? exePath = process.MainModule?.FileName;
                
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return "#1A1A1A";
                
                // Icon'u çıkar ve dominant rengi bul
                using var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon == null) return "#1A1A1A";
                
                using var bitmap = icon.ToBitmap();
                return GetDominantColor(bitmap);
            }
            catch
            {
                return "#1A1A1A"; // Fallback
            }
        });
    }

    private static string GetDominantColor(Bitmap bitmap)
    {
        try
        {
            var colorCounts = new Dictionary<System.Drawing.Color, int>();
            
            // Bitmap'i küçült (performans için)
            int sampleSize = Math.Min(Math.Min(bitmap.Width, bitmap.Height), 32);
            int stepX = Math.Max(1, bitmap.Width / sampleSize);
            int stepY = Math.Max(1, bitmap.Height / sampleSize);
            
            int validPixels = 0;
            
            for (int x = 0; x < bitmap.Width; x += stepX)
            {
                for (int y = 0; y < bitmap.Height; y += stepY)
                {
                    try
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        
                        // Şeffaf ve çok açık/koyu renkleri atla
                        if (pixel.A < 128 || 
                            pixel.GetBrightness() > 0.95f || 
                            pixel.GetBrightness() < 0.05f)
                            continue;
                        
                        // Gri tonları atla (saturasyon çok düşük)
                        if (pixel.GetSaturation() < 0.1f)
                            continue;
                        
                        validPixels++;
                        
                        // Renkleri grupla (benzer renkleri birleştir)
                        var groupedColor = GroupColor(pixel);
                        colorCounts[groupedColor] = colorCounts.GetValueOrDefault(groupedColor) + 1;
                    }
                    catch
                    {
                        // GetPixel bazen hata verebilir, atla
                        continue;
                    }
                }
            }
            
            // Hiç geçerli pixel bulunamadıysa fallback
            if (colorCounts.Count == 0 || validPixels < 5)
            {
                Debug.WriteLine("No valid colors found in bitmap, using fallback");
                return "#1A1A1A";
            }
            
            // En çok tekrar eden rengi al
            var dominantColor = colorCounts.OrderByDescending(kv => kv.Value).First().Key;
            
            // Saturasyon ve parlaklığı optimize et (UI için uygun hale getir)
            var hsb = ColorToHsb(dominantColor);
            hsb.Saturation = Math.Max(0.3f, Math.Min(0.8f, hsb.Saturation)); // %30-80 arası
            hsb.Brightness = Math.Max(0.2f, Math.Min(0.7f, hsb.Brightness)); // %20-70 arası
            
            var optimizedColor = HsbToColor(hsb);
            return $"#{optimizedColor.R:X2}{optimizedColor.G:X2}{optimizedColor.B:X2}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetDominantColor error: {ex.Message}");
            return "#1A1A1A";
        }
    }

    private static System.Drawing.Color GroupColor(System.Drawing.Color color)
    {
        // Renkleri 32'şer seviyede grupla (256/8 = 32)
        int r = (color.R / 32) * 32;
        int g = (color.G / 32) * 32;
        int b = (color.B / 32) * 32;
        return System.Drawing.Color.FromArgb(r, g, b);
    }

    private static (float Hue, float Saturation, float Brightness) ColorToHsb(System.Drawing.Color color)
    {
        return (color.GetHue(), color.GetSaturation(), color.GetBrightness());
    }

    private static System.Drawing.Color HsbToColor((float Hue, float Saturation, float Brightness) hsb)
    {
        return ColorFromHsb(hsb.Hue, hsb.Saturation, hsb.Brightness);
    }

    private static System.Drawing.Color ColorFromHsb(float hue, float saturation, float brightness)
    {
        float c = brightness * saturation;
        float x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
        float m = brightness - c;
        
        float r, g, b;
        
        if (hue < 60)        { r = c; g = x; b = 0; }
        else if (hue < 120)  { r = x; g = c; b = 0; }
        else if (hue < 180)  { r = 0; g = c; b = x; }
        else if (hue < 240)  { r = 0; g = x; b = c; }
        else if (hue < 300)  { r = x; g = 0; b = c; }
        else                 { r = c; g = 0; b = x; }
        
        return System.Drawing.Color.FromArgb(
            (int)((r + m) * 255),
            (int)((g + m) * 255),
            (int)((b + m) * 255)
        );
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private static string GetActiveWindowProcessName()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            GetWindowThreadProcessId(hwnd, out uint processId);
            
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return "";
        }
    }

    public void Dispose()
    {
        _colorTimer?.Stop();
        GC.SuppressFinalize(this);
    }
}