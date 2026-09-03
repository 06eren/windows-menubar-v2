using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Windows_10_MenuBar.Helpers;

public static class WallpaperHelper
{
    /// <summary>
    /// Duvar kağıdının ortalama parlaklığını 0–1 aralığında döndürür.
    /// Hata durumunda 0.5 döner (nötr).
    /// </summary>
    public static double SampleBrightness(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream,
                BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];

            int w = Math.Min(frame.PixelWidth,  100);
            int h = Math.Min(frame.PixelHeight, 100);

            var scaled = new TransformedBitmap(frame, new ScaleTransform(
                (double)w / frame.PixelWidth,
                (double)h / frame.PixelHeight));

            int    stride = w * 4;
            byte[] pixels = new byte[stride * h];
            scaled.CopyPixels(pixels, stride, 0);

            double total = 0;
            int    count = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                double r = pixels[i + 2] / 255.0;
                double g = pixels[i + 1] / 255.0;
                double b = pixels[i + 0] / 255.0;
                total += 0.299 * r + 0.587 * g + 0.114 * b;
                count++;
            }
            return count > 0 ? total / count : 0.5;
        }
        catch { return 0.5; }
    }
}
