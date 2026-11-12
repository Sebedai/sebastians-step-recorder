using System.Drawing;

namespace StepRecorder.Settings;

/// <summary>
/// Gemerkte Fensterlage auf die heutigen Monitore bringen (rein, testbar; physische Pixel):
/// der Monitor mit der größten Überlappung gewinnt; bei anderer Skalierung bleibt die Größe in geräteunabhängigen
/// Einheiten gleich; fehlt der Monitor, öffnet das Fenster mittig auf dem Hauptmonitor; immer ganz im Arbeitsbereich.
/// </summary>
public static class WindowPlacement
{
    public readonly record struct Monitor(Rectangle Work, int Dpi, bool Primary);

    public static WindowBounds? Fit(WindowBounds? saved, IReadOnlyList<Monitor> monitors, double minWidthDip = 0, double minHeightDip = 0)
    {
        if (saved == null || saved.W <= 0 || saved.H <= 0 || monitors.Count == 0) return null;
        var r = new Rectangle(saved.X, saved.Y, saved.W, saved.H);
        Monitor? best = null;
        long bestArea = 0;
        foreach (var m in monitors)
        {
            var i = Rectangle.Intersect(r, m.Work);
            long a = (long)Math.Max(0, i.Width) * Math.Max(0, i.Height);
            if (a > bestArea) { bestArea = a; best = m; }
        }
        bool lost = best == null;
        var mon = best ?? monitors.FirstOrDefault(m => m.Primary);
        if (mon.Work.Width <= 0 || mon.Work.Height <= 0) mon = monitors[0];
        var work = mon.Work;
        int savedDpi = saved.Dpi > 0 ? saved.Dpi : 96, dpi = mon.Dpi > 0 ? mon.Dpi : 96;
        double k = dpi / (double)savedDpi;
        int minW = (int)Math.Ceiling(Math.Max(0, minWidthDip) * dpi / 96.0), minH = (int)Math.Ceiling(Math.Max(0, minHeightDip) * dpi / 96.0);
        int w = Math.Clamp((int)Math.Round(saved.W * k), Math.Min(minW, work.Width), work.Width);
        int h = Math.Clamp((int)Math.Round(saved.H * k), Math.Min(minH, work.Height), work.Height);
        int x, y;
        if (lost) { x = work.X + (work.Width - w) / 2; y = work.Y + (work.Height - h) / 2; }
        else { x = Math.Clamp(saved.X, work.X, work.Right - w); y = Math.Clamp(saved.Y, work.Y, work.Bottom - h); }
        return new WindowBounds { X = x, Y = y, W = w, H = h, Max = saved.Max, Dpi = dpi };
    }
}
