using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfColor = System.Windows.Media.Color;

namespace Windows_10_MenuBar.Services;

/// <summary>
/// Gradient animasyonlu tema Storyboard fabrikası.
///
/// Performans optimizasyonları:
/// - LinearColorKeyFrame kullanır (EasingColorKeyFrame yerine) — her frame matematik yok
/// - RepeatBehavior sadece Storyboard seviyesinde, animation'da tekrar yok
/// - Süre uzun tutulur (12-15s) — frame başına değişim miktarı az, render basit
/// - Sadece GradientStop.Color animate edilir — layout/measure tetiklenmez
/// - Storyboard önceden cache'lenir ve freeze edilir — immutable + thread-safe
/// - Timeline.DesiredFrameRate=30 — 60fps yerine 30fps, GPU yükü yarıya iner
/// </summary>
public static class GradientThemeService
{
    // Önceden oluşturulmuş storyboard'lar — her çağrıda yeni nesne yaratma
    private static readonly Dictionary<string, Storyboard> _cache = new();

    // Animasyon FPS hedefi — düşük FPS = daha az CPU/GPU kullanımı
    private const int TargetFps = 30;

    public static bool IsGradientTheme(string name) =>
        name is "Aurora" or "Sunset" or "Ocean" or "Wind";

    /// <summary>Cache'lenmiş Storyboard döndürür. İlk çağrıda oluşturur.</summary>
    public static Storyboard GetStoryboard(string themeName)
    {
        if (_cache.TryGetValue(themeName, out var cached)) return cached;
        var sb = themeName switch
        {
            "Aurora" => BuildAurora(),
            "Sunset" => BuildSunset(),
            "Ocean"  => BuildOcean(),
            "Wind"   => BuildWind(),
            _        => BuildAurora(),
        };
        _cache[themeName] = sb;
        return sb;
    }

    // ── Aurora — mavi / mor / yeşil ──────────────────────────────────────────

    private static Storyboard BuildAurora() => Build(
        stop0: new[]
        {
            WpfColor.FromRgb(0x05, 0x07, 0x20),
            WpfColor.FromRgb(0x18, 0x04, 0x28),
            WpfColor.FromRgb(0x04, 0x18, 0x10),
            WpfColor.FromRgb(0x05, 0x07, 0x20),
        },
        stop1: new[]
        {
            WpfColor.FromRgb(0x00, 0x38, 0x88),
            WpfColor.FromRgb(0x00, 0x88, 0x55),
            WpfColor.FromRgb(0x55, 0x00, 0x88),
            WpfColor.FromRgb(0x00, 0x38, 0x88),
        },
        durationSecs: 15.0   // yavaş = daha az frame değişimi = daha akıcı
    );

    // ── Sunset — turuncu / pembe / mor ───────────────────────────────────────

    private static Storyboard BuildSunset() => Build(
        stop0: new[]
        {
            WpfColor.FromRgb(0x18, 0x05, 0x00),
            WpfColor.FromRgb(0x18, 0x00, 0x0E),
            WpfColor.FromRgb(0x0E, 0x00, 0x18),
            WpfColor.FromRgb(0x18, 0x05, 0x00),
        },
        stop1: new[]
        {
            WpfColor.FromRgb(0xBB, 0x48, 0x00),
            WpfColor.FromRgb(0xBB, 0x00, 0x65),
            WpfColor.FromRgb(0x65, 0x00, 0xBB),
            WpfColor.FromRgb(0xBB, 0x48, 0x00),
        },
        durationSecs: 12.0
    );

    // ── Ocean — derin mavi / turkuaz ─────────────────────────────────────────

    private static Storyboard BuildOcean() => Build(
        stop0: new[]
        {
            WpfColor.FromRgb(0x00, 0x07, 0x18),
            WpfColor.FromRgb(0x00, 0x12, 0x28),
            WpfColor.FromRgb(0x00, 0x07, 0x18),
            WpfColor.FromRgb(0x00, 0x0E, 0x22),
        },
        stop1: new[]
        {
            WpfColor.FromRgb(0x00, 0x48, 0x90),
            WpfColor.FromRgb(0x00, 0x72, 0x82),
            WpfColor.FromRgb(0x00, 0x48, 0x90),
            WpfColor.FromRgb(0x00, 0x5A, 0x68),
        },
        durationSecs: 13.0
    );

    // ── Wind — soldan sağa akan soluk mavi/beyaz ─────────────────────────────

    /// <summary>
    /// Wind teması renk değil StartPoint/EndPoint'i anime eder.
    /// Gradient "soldan sağa akıyor" efekti yaratır — rüzgar gibi.
    ///
    /// Teknik: StartPoint X 0→-0.5→0, EndPoint X 1→1.5→1 döngüsü (daha yumuşak)
    /// Gradient slice pencere genişliğinin ötesine taşıp geri geliyor.
    /// Sonuç: ışık bantı soldan sağa süpürülüyor gibi görünür.
    /// </summary>
    private static Storyboard BuildWind()
    {
        // Renk: soğuk mavi-beyaz tonları
        var sb = new Storyboard 
        { 
            RepeatBehavior = RepeatBehavior.Forever,
        };
        Timeline.SetDesiredFrameRate(sb, TargetFps);

        // ── Önce renkleri ayarla: soluk mavi zemin, parlak bant ──
        sb.Children.Add(MakeAnim("BarStop0",
            new[]
            {
                WpfColor.FromRgb(0x08, 0x10, 0x1E),  // koyu mavi
                WpfColor.FromRgb(0x10, 0x1C, 0x30),  // biraz açık
                WpfColor.FromRgb(0x08, 0x10, 0x1E),  // geri
            },
            14.0));

        sb.Children.Add(MakeAnim("BarStop1",
            new[]
            {
                WpfColor.FromRgb(0x28, 0x4A, 0x72),  // orta mavi
                WpfColor.FromRgb(0x50, 0x80, 0xB0),  // parlak bant (yumuşatıldı)
                WpfColor.FromRgb(0x28, 0x4A, 0x72),  // geri
            },
            14.0));

        // ── StartPoint X: 0 → -0.5 → 0 (gradient sola kayıyor - yumuşatıldı) ──
        var startX = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            FillBehavior   = FillBehavior.HoldEnd,
        };
        Storyboard.SetTargetName(startX, "BarGradientBrush");
        Storyboard.SetTargetProperty(startX, new PropertyPath("StartPoint.X"));
        startX.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0)),   Value = 0.0  });
        startX.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(7.0)), Value = -0.5 });
        startX.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(14.0)), Value = 0.0  });
        sb.Children.Add(startX);

        // ── EndPoint X: 1 → 1.5 → 1 (gradient sağa taşıyor - yumuşatıldı) ──
        var endX = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            FillBehavior   = FillBehavior.HoldEnd,
        };
        Storyboard.SetTargetName(endX, "BarGradientBrush");
        Storyboard.SetTargetProperty(endX, new PropertyPath("EndPoint.X"));
        endX.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0)),   Value = 1.0 });
        endX.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(7.0)), Value = 1.5 });
        endX.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(14.0)), Value = 1.0 });
        sb.Children.Add(endX);

        sb.Freeze();
        return sb;
    }

    // ── Generic builder ───────────────────────────────────────────────────────

    private static Storyboard Build(WpfColor[] stop0, WpfColor[] stop1, double durationSecs)
    {
        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(sb, TargetFps);  // 30 FPS hedefi
        
        sb.Children.Add(MakeAnim("BarStop0", stop0, durationSecs));
        sb.Children.Add(MakeAnim("BarStop1", stop1, durationSecs));
        sb.Freeze(); // immutable → thread-safe, GC baskısı yok
        return sb;
    }

    private static ColorAnimationUsingKeyFrames MakeAnim(
        string targetName, WpfColor[] colors, double totalSecs)
    {
        var anim = new ColorAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
        Storyboard.SetTargetName(anim, targetName);
        Storyboard.SetTargetProperty(anim, new PropertyPath(GradientStop.ColorProperty));

        int n = colors.Length;
        for (int i = 0; i < n; i++)
        {
            double t = totalSecs * i / (n - 1);
            // LinearColorKeyFrame — matematik yok, sadece lineer interpolasyon
            // Sine easing kaldırıldı: her frame için trig hesabı yapılmıyordu
            anim.KeyFrames.Add(new LinearColorKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t)),
                Value   = colors[i],
            });
        }

        return anim;
    }
}
