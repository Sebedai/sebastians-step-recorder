using System.Drawing;
using System.Drawing.Imaging;
using static StepRecorder.Native.Win32;

namespace StepRecorder.Native;

public sealed record MonitorInfo(int Index, string Device, Rectangle Bounds, Rectangle WorkArea, int Dpi, bool Primary)
{
    public double Scale => Dpi / 96.0;
}

/// <summary>
/// Bildschirmaufnahme per GDI (BitBlt). Fenster mit WDA_EXCLUDEFROMCAPTURE erscheinen dabei nicht
/// (Windows 10 2004+); damit ist der Recorder selbst nie im Bild.
/// </summary>
public static class ScreenGrabber
{
    public static List<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        EnumDisplayMonitors(0, 0, (nint h, nint hdc, ref RECT rc, nint d) =>
        {
            var mi = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
            GetMonitorInfo(h, ref mi);
            uint dpi = 96;
            try { GetDpiForMonitor(h, 0, out dpi, out _); } catch { }
            list.Add(new MonitorInfo(list.Count, mi.szDevice ?? "", ToRect(mi.rcMonitor), ToRect(mi.rcWork), (int)dpi, (mi.dwFlags & 1) != 0));
            return true;
        }, 0);
        // Primärmonitor zuerst, danach von links nach rechts
        list = list.OrderByDescending(m => m.Primary).ThenBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y)
            .Select((m, i) => m with { Index = i }).ToList();
        return list;
    }

    public static Rectangle VirtualScreen(IEnumerable<MonitorInfo> monitors)
    {
        var all = monitors.Select(m => m.Bounds).ToList();
        if (all.Count == 0) return new Rectangle(0, 0, 1, 1);
        var r = all[0];
        foreach (var b in all.Skip(1)) r = Rectangle.Union(r, b);
        return r;
    }

    static Rectangle ToRect(RECT r) => Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);

    /// <summary>Nimmt einen Bereich in physischen Bildschirmkoordinaten auf (32bpp).</summary>
    public static Bitmap Capture(Rectangle area)
    {
        var bmp = new Bitmap(Math.Max(1, area.Width), Math.Max(1, area.Height), PixelFormat.Format32bppRgb);
        nint screenDc = GetDC(0);
        try
        {
            using var g = Graphics.FromImage(bmp);
            nint dst = g.GetHdc();
            try { BitBlt(dst, 0, 0, area.Width, area.Height, screenDc, area.X, area.Y, SRCCOPY | CAPTUREBLT); }
            finally { g.ReleaseHdc(dst); }
        }
        finally { ReleaseDC(0, screenDc); }
        return bmp;
    }

    /// <summary>Schneller Vergleich zweier gleich großer Bilder; liefert das geänderte Rechteck oder null.</summary>
    public static unsafe Rectangle? DiffBounds(Bitmap a, Bitmap b, int tile = 16)
    {
        if (a.Width != b.Width || a.Height != b.Height) return new Rectangle(0, 0, b.Width, b.Height);
        var r = new Rectangle(0, 0, a.Width, a.Height);
        var da = a.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        var db = b.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < a.Height; y++)
            {
                uint* pa = (uint*)((byte*)da.Scan0 + y * da.Stride);
                uint* pb = (uint*)((byte*)db.Scan0 + y * db.Stride);
                for (int x = 0; x < a.Width; x++)
                {
                    if (((pa[x] ^ pb[x]) & 0x00FFFFFF) != 0)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                        // Rest der Kachel in dieser Zeile überspringen
                        x = Math.Min(a.Width - 1, (x / tile + 1) * tile - 1);
                        if (maxX < x) maxX = x;
                    }
                }
            }
            if (maxX < 0) return null;
            return Rectangle.FromLTRB(minX, minY, Math.Min(a.Width, maxX + 1), Math.Min(a.Height, maxY + 1));
        }
        finally { a.UnlockBits(da); b.UnlockBits(db); }
    }

    /// <summary>Anteil geänderter Pixel (Stichprobe jedes n-ten Pixels).</summary>
    public static unsafe double ChangedRatio(Bitmap a, Bitmap b, int step = 4)
    {
        if (a.Width != b.Width || a.Height != b.Height) return 1;
        var r = new Rectangle(0, 0, a.Width, a.Height);
        var da = a.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        var db = b.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            long changed = 0, total = 0;
            for (int y = 0; y < a.Height; y += step)
            {
                uint* pa = (uint*)((byte*)da.Scan0 + y * da.Stride);
                uint* pb = (uint*)((byte*)db.Scan0 + y * db.Stride);
                for (int x = 0; x < a.Width; x += step) { total++; if (((pa[x] ^ pb[x]) & 0x00FFFFFF) != 0) changed++; }
            }
            return total == 0 ? 0 : (double)changed / total;
        }
        finally { a.UnlockBits(da); b.UnlockBits(db); }
    }
}
