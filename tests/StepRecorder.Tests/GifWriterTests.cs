using System.Drawing;
using System.Drawing.Imaging;
using StepRecorder.Export;

namespace StepRecorder.Tests;

/// <summary>GIF-Export: Die geschriebene Datei lässt sich mit GDI+ laden und enthält alle Bilder.</summary>
public class GifWriterTests
{
    [Fact]
    public void Write_ZweiBilder_LaedtMitZweiBildern()
    {
        using var a = new Bitmap(64, 48, PixelFormat.Format32bppArgb);
        using var b = new Bitmap(64, 48, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(a)) { g.Clear(Color.White); g.FillRectangle(Brushes.Red, 8, 8, 20, 12); }
        using (var g = Graphics.FromImage(b)) { g.Clear(Color.White); g.FillRectangle(Brushes.Blue, 30, 20, 20, 12); }

        using var ms = new MemoryStream();
        GifWriter.Write(ms, [a, b], [500, 800]);
        ms.Position = 0;

        using var img = Image.FromStream(ms);
        Assert.Equal(2, img.GetFrameCount(FrameDimension.Time));
        img.SelectActiveFrame(FrameDimension.Time, 1);
        using var copy = new Bitmap(img);
        Assert.Equal(Color.Blue.ToArgb(), copy.GetPixel(40, 26).ToArgb());
        Assert.Equal(Color.White.ToArgb(), copy.GetPixel(2, 2).ToArgb());
    }
}
