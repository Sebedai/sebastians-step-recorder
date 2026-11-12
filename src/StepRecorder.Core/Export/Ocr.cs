using System.Drawing;
using System.Text.Json.Nodes;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace StepRecorder.Export;

/// <summary>
/// Texterkennung mit der in Windows eingebauten OCR (offline, Sprachen des Benutzerprofils).
/// Liefert für einen Bildausschnitt das Wort unter dem Klickpunkt und die nächstgelegenen Zeilen.
/// </summary>
public static class Ocr
{
    static OcrEngine? _engine;
    static bool _tried;

    public static bool Available
    {
        get
        {
            if (_tried) return _engine != null;
            _tried = true;
            try { _engine = OcrEngine.TryCreateFromUserProfileLanguages(); } catch { _engine = null; }
            return _engine != null;
        }
    }

    public static string? Language => Available ? _engine!.RecognizerLanguage.LanguageTag : null;

    static Windows.Media.Ocr.OcrResult? Recognize(Bitmap bmp)
    {
        if (!Available) return null;
        byte[] png;
        using (var ms = new MemoryStream()) { bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png); png = ms.ToArray(); }
        var task = Task.Run(async () =>
        {
            using var ras = new InMemoryRandomAccessStream();
            using (var w = new DataWriter(ras)) { w.WriteBytes(png); await w.StoreAsync(); w.DetachStream(); }
            ras.Seek(0);
            var dec = await BitmapDecoder.CreateAsync(ras);
            using var sb = await dec.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            return await _engine!.RecognizeAsync(sb);
        });
        return task.Wait(15000) ? task.Result : null;
    }

    /// <summary>
    /// Sucht personenbezogene und geheime Daten (E-Mail, IBAN, Kartennummer, Telefon, Schlüssel) im Bild.
    /// Liefert je Fundstelle das umschließende Rechteck der betroffenen Wörter (in Bildpixeln, mit kleinem Rand).
    /// </summary>
    public static List<(Rectangle rect, string kind, string text)> FindSensitive(Bitmap bmp)
    {
        var found = new List<(Rectangle, string, string)>();
        try
        {
            // kleine Bilder vergrößern: die Windows-OCR erkennt Bildschirmtext ab ca. 10 px Zeilenhöhe zuverlässiger
            double scale = Math.Max(bmp.Width, bmp.Height) < 1400 ? 2.0 : 1.0;
            if (Math.Max(bmp.Width, bmp.Height) * scale > OcrEngine.MaxImageDimension) scale = (double)OcrEngine.MaxImageDimension / Math.Max(bmp.Width, bmp.Height);
            using var src = scale == 1.0 ? (Bitmap)bmp.Clone() : new Bitmap(bmp, (int)(bmp.Width * scale), (int)(bmp.Height * scale));
            var result = Recognize(src);
            if (result == null) return found;
            foreach (var line in result.Lines)
            {
                var words = line.Words.ToList();
                var sbText = new System.Text.StringBuilder();
                var offsets = new List<(int start, int end, Windows.Foundation.Rect r)>();
                foreach (var w in words)
                {
                    if (sbText.Length > 0) sbText.Append(' ');
                    offsets.Add((sbText.Length, sbText.Length + w.Text.Length, w.BoundingRect));
                    sbText.Append(w.Text);
                }
                string text = sbText.ToString();
                // Telefonnummern und Kartennummern werden oft in Wortgruppen erkannt: auch ohne Leerzeichen prüfen
                foreach (var (start, length, kind) in StepRecorder.ReadLayer.Patterns.SensitiveSpans(text))
                {
                    var hit = offsets.Where(o => o.start < start + length && start < o.end).ToList();
                    if (hit.Count == 0) continue;
                    double l = hit.Min(h => h.r.X), t = hit.Min(h => h.r.Y), r = hit.Max(h => h.r.X + h.r.Width), b = hit.Max(h => h.r.Y + h.r.Height);
                    var rect = Rectangle.FromLTRB((int)(l / scale) - 3, (int)(t / scale) - 3, (int)Math.Ceiling(r / scale) + 3, (int)Math.Ceiling(b / scale) + 3);
                    rect.Intersect(new Rectangle(0, 0, bmp.Width, bmp.Height));
                    if (rect.Width > 2 && rect.Height > 2) found.Add((rect, kind, text.Substring(start, length)));
                }
            }
        }
        catch { }
        return found;
    }

    /// <summary>Erkennt Text in <paramref name="bmp"/>; <paramref name="point"/> ist der Klickpunkt im Bild.</summary>
    public static JsonObject? Around(Bitmap bmp, Point point, int maxLines = 8)
    {
        if (!Available) return null;
        try
        {
            byte[] png;
            using (var ms = new MemoryStream()) { bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png); png = ms.ToArray(); }
            var task = Task.Run(async () =>
            {
                using var ras = new InMemoryRandomAccessStream();
                using (var w = new DataWriter(ras)) { w.WriteBytes(png); await w.StoreAsync(); w.DetachStream(); }
                ras.Seek(0);
                var dec = await BitmapDecoder.CreateAsync(ras);
                using var sb = await dec.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                return await _engine!.RecognizeAsync(sb);
            });
            if (!task.Wait(8000)) return null;
            var result = task.Result;
            var o = new JsonObject { ["language"] = Language };
            string? under = null; double underD = double.MaxValue;
            var lines = new List<(double d, JsonObject o)>();
            foreach (var line in result.Lines)
            {
                double l = double.MaxValue, t = double.MaxValue, r = 0, b = 0;
                foreach (var word in line.Words)
                {
                    var wr = word.BoundingRect;
                    l = Math.Min(l, wr.X); t = Math.Min(t, wr.Y); r = Math.Max(r, wr.X + wr.Width); b = Math.Max(b, wr.Y + wr.Height);
                    double dx = point.X < wr.X ? wr.X - point.X : point.X > wr.X + wr.Width ? point.X - (wr.X + wr.Width) : 0;
                    double dy = point.Y < wr.Y ? wr.Y - point.Y : point.Y > wr.Y + wr.Height ? point.Y - (wr.Y + wr.Height) : 0;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d < underD && d <= 12) { underD = d; under = word.Text; }
                }
                if (l == double.MaxValue) continue;
                double cx = (l + r) / 2, cy = (t + b) / 2;
                double dist = Math.Sqrt(Math.Pow(cx - point.X, 2) + Math.Pow(cy - point.Y, 2));
                lines.Add((dist, new JsonObject { ["text"] = line.Text, ["dx"] = (int)Math.Round(cx - point.X), ["dy"] = (int)Math.Round(cy - point.Y) }));
            }
            if (under != null) o["underCursor"] = under;
            if (lines.Count > 0) o["nearby"] = new JsonArray(lines.OrderBy(x => x.d).Take(maxLines).Select(x => (JsonNode)x.o).ToArray());
            return o.Count > 1 ? o : null;
        }
        catch { return null; }
    }
}
