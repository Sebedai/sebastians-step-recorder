using System.Drawing;
using System.Drawing.Drawing2D;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Export;

/// <summary>
/// Kurzer Clip zu einem Schritt: animiertes GIF aus den gespeicherten Bildern des Schritts –
/// Vorher-Bild mit Markierung, Zwischenbilder während der Bildschirm sich ändert, Nachher-Bild.
/// Es wird nichts neu aufgenommen; Schwärzungen gelten wie in allen Bildern.
/// </summary>
public static class StepClip
{
    /// <summary>Bild-Kennungen in zeitlicher Reihenfolge (Vorher, Zwischenbilder, Nachher) auf dem Monitor des Schritts.</summary>
    public static List<string> FrameIds(SessionView view, ViewStep step, int max = 24)
    {
        var ids = new List<string>();
        if (step.Manual || step.Redacted) return ids;
        if (step.PreFrame != null) ids.Add(step.PreFrame);
        var pre = view.Session.Frame(step.PreFrame);
        var post = view.Session.Frame(step.PostFrame);
        string? m = pre?.Str("m") ?? post?.Str("m") ?? step.Monitor;
        long from = pre?.Long("t") ?? step.TFrom;
        long to = post?.Long("t") ?? step.TTo + 3_000_000;
        // nicht in den nächsten Schritt hineinlaufen
        var next = view.All.Where(x => x.TFrom > step.TTo && !x.Hidden).OrderBy(x => x.TFrom).FirstOrDefault();
        if (next != null && post == null) to = Math.Min(to, next.TFrom);
        foreach (var f in view.Session.Frames.Values.Where(f => f.Str("m") == m && f.Long("t") > from && f.Long("t") < to).OrderBy(f => f.Long("t")))
            if (f.Str("id") is { } id && id != step.PostFrame && view.FrameAvailable(id)) ids.Add(id);
        if (step.PostFrame != null && !ids.Contains(step.PostFrame)) ids.Add(step.PostFrame);
        if (ids.Count > max)
        {
            // gleichmäßig ausdünnen, erstes und letztes Bild bleiben
            var mid = ids.GetRange(1, ids.Count - 2);
            int k = max - 2;
            ids = new[] { ids[0] }.Concat(Enumerable.Range(0, k).Select(i => mid[(int)((long)i * mid.Count / k)])).Append(ids[^1]).ToList();
        }
        return ids;
    }

    /// <summary>Clip schreiben. Rückgabe: Anzahl der Bilder (0 = kein Clip möglich).</summary>
    public static int Write(SessionView view, ViewStep step, string path, int maxWidth = 1280, int frameMs = 700)
    {
        var ids = FrameIds(view, step);
        if (ids.Count == 0) return 0;
        var frames = new List<Bitmap>(); var delays = new List<int>();
        try
        {
            // erstes Bild mit Markierung der Aktion, wie in der Anleitung
            using (var first = StepImages.Render(view, step, "before", "original", annotate: true, applyCrop: false))
                if (first != null) { frames.Add(Scale(first.Bitmap, maxWidth)); delays.Add(frameMs * 2); }
            foreach (var id in ids.Skip(frames.Count > 0 ? 1 : 0))
            {
                using var b = view.Frame(id);
                if (b == null) continue;
                frames.Add(Scale(b, maxWidth)); delays.Add(frameMs);
            }
            if (frames.Count == 0) return 0;
            delays[^1] = Math.Max(delays[^1], 2000);   // am Ende kurz stehen bleiben
            // alle Bilder gleich groß (Monitorwechsel oder eigene Bilder)
            int w = frames.Max(f => f.Width), h = frames.Max(f => f.Height);
            for (int i = 0; i < frames.Count; i++)
                if (frames[i].Width != w || frames[i].Height != h)
                {
                    var c = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    using (var g = Graphics.FromImage(c)) { g.Clear(Color.Black); g.DrawImage(frames[i], (w - frames[i].Width) / 2, (h - frames[i].Height) / 2, frames[i].Width, frames[i].Height); }
                    frames[i].Dispose(); frames[i] = c;
                }
            using var fs = File.Create(path);
            GifWriter.Write(fs, frames, delays);
            return frames.Count;
        }
        finally { foreach (var f in frames) f.Dispose(); }
    }

    static Bitmap Scale(Bitmap src, int maxWidth)
    {
        double s = Math.Min(1, (double)maxWidth / src.Width);
        int w = Math.Max(1, (int)(src.Width * s)), h = Math.Max(1, (int)(src.Height * s));
        var b = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(b);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(src, 0, 0, w, h);
        return b;
    }
}
