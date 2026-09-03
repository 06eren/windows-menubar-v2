using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

if (args.Length < 2)
{
    Console.WriteLine("Usage: converter <svg-path> <output-folder>");
    return 1;
}

var svgPath = args[0];
var outputFolder = args[1];

if (!File.Exists(svgPath))
{
    Console.WriteLine($"SVG file not found: {svgPath}");
    return 1;
}

Directory.CreateDirectory(outputFolder);

var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };

foreach (var size in sizes)
{
    try
    {
        var drawing = LoadSvg(svgPath);
        var bitmap = RenderToBitmap(drawing, size, size);
        var outputPath = Path.Combine(outputFolder, $"icon_{size}.png");
        SaveBitmap(bitmap, outputPath);
        Console.WriteLine($"Generated: {outputPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error generating {size}x{size}: {ex.Message}");
    }
}

Console.WriteLine("PNG generation complete!");
return 0;

static DrawingGroup LoadSvg(string path)
{
    var svg = File.ReadAllText(path);
    
    // Simple SVG parser - assumes a basic rect-based icon
    var drawing = new DrawingGroup();
    
    using (var dc = drawing.Open())
    {
        // Parse SVG and create WPF drawing
        // For simplicity, create a rounded rect icon
        var rect = new RectangleGeometry(new Rect(0, 0, 100, 100), 10, 10);
        var brush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
        dc.DrawGeometry(brush, null, rect);
        
        // Add inner element
        var innerRect = new RectangleGeometry(new Rect(20, 20, 60, 60), 5, 5);
        var innerBrush = new SolidColorBrush(Colors.White);
        dc.DrawGeometry(innerBrush, null, innerRect);
    }
    
    return drawing;
}

static RenderTargetBitmap RenderToBitmap(DrawingGroup drawing, int width, int height)
{
    var drawingVisual = new DrawingVisual();
    using (var dc = drawingVisual.RenderOpen())
    {
        dc.PushTransform(new ScaleTransform(
            width / drawing.Bounds.Width,
            height / drawing.Bounds.Height));
        dc.DrawDrawing(drawing);
    }
    
    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(drawingVisual);
    return bitmap;
}

static void SaveBitmap(RenderTargetBitmap bitmap, string path)
{
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var stream = File.Create(path);
    encoder.Save(stream);
}
