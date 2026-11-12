using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace StepRecorder.Compare;

/// <summary>Einstellungen des Bildabgleichs (12, Paket 19): Schwelle und Masken gegen Fehlalarme durch Kantenglättung, Mauszeiger und Uhrzeiten.</summary>
public sealed class ImageDiffOptions
{
    /// <summary>Mittlere Farbabweichung (0–255), ab der ein Pixel als geändert gilt.</summary>
    public int PixelThreshold { get; set; } = 32;
    /// <summary>Rastergröße in Pixeln (des verglichenen Bildes).</summary>
    public int Cell { get; set; } = 16;
    /// <summary>Anteil geänderter Pixel in einer Zelle, ab dem die Zelle als geändert gilt.</summary>
    public double CellRatio { get; set; } = 0.04;
    /// <summary>Anteil geänderter Zellen am Bild, ab dem „Bild“ als Unterschied gemeldet wird.</summary>
    public double MinChangedRatio { get; set; } = 0.005;
    /// <summary>Ignorierte Bereiche als Anteile des Bildes (x, y, Breite, Höhe; 0–1), z. B. die Uhr in der Taskleiste.</summary>
    public List<RectangleF> Masks { get; set; } = new();
}

public sealed record ImageDiffResult(double ChangedRatio, List<Rectangle> Regions, Size Size, bool SizeDiffers)
{
    public JsonObject ToJson() => new()
    {
        ["changedPct"] = Math.Round(ChangedRatio * 100, 2), ["width"] = Size.Width, ["height"] = Size.Height, ["sizeDiffers"] = SizeDiffers ? true : null,
        ["regions"] = new JsonArray(Regions.Select(r => (JsonNode)new JsonObject { ["x"] = r.X, ["y"] = r.Y, ["w"] = r.Width, ["h"] = r.Height }).ToArray())
    };
}

/// <summary>
/// Bildunterschiede zweier Schrittbilder: Raster aus Zellen, je Zelle der Anteil deutlich geänderter Pixel; benachbarte
/// geänderte Zellen werden zu Bereichen zusammengefasst. Unterschiedlich große Bilder werden auf die Größe von A gebracht.
/// Rein deterministisch – gleiche Bilder und Einstellungen ergeben dasselbe Ergebnis in Editor, Kommandozeile und MCP.
/// </summary>
public static class ImageDiff
{
    public static ImageDiffResult Diff(Bitmap a, Bitmap b, ImageDiffOptions? o = null)
    {
        o ??= new ImageDiffOptions();
        int w = a.Width, h = a.Height;
        bool sizeDiffers = a.Width != b.Width || a.Height != b.Height;
        using var a32 = To32(a, w, h);
        using var b32 = To32(b, w, h);
        var pa = Pixels(a32); var pb = Pixels(b32);
        int cell = Math.Max(4, o.Cell);
        int cols = (w + cell - 1) / cell, rows = (h + cell - 1) / cell;
        var changed = new bool[cols, rows];
        var masks = o.Masks.Select(m => Rectangle.Round(new RectangleF(m.X * w, m.Y * h, m.Width * w, m.Height * h))).ToList();
        int changedCells = 0, totalCells = 0;
        for (int cy = 0; cy < rows; cy++)
            for (int cx = 0; cx < cols; cx++)
            {
                var rc = new Rectangle(cx * cell, cy * cell, Math.Min(cell, w - cx * cell), Math.Min(cell, h - cy * cell));
                if (masks.Any(m => m.Contains(rc.X + rc.Width / 2, rc.Y + rc.Height / 2))) continue;
                totalCells++;
                int diff = 0, n = rc.Width * rc.Height;
                for (int y = rc.Top; y < rc.Bottom; y++)
                {
                    int row = y * w;
                    for (int x = rc.Left; x < rc.Right; x++)
                    {
                        int p = pa[row + x], q = pb[row + x];
                        int d = Math.Abs((p & 0xFF) - (q & 0xFF)) + Math.Abs(((p >> 8) & 0xFF) - ((q >> 8) & 0xFF)) + Math.Abs(((p >> 16) & 0xFF) - ((q >> 16) & 0xFF));
                        if (d > o.PixelThreshold * 3) diff++;
                    }
                }
                if (diff >= Math.Max(2, n * o.CellRatio)) { changed[cx, cy] = true; changedCells++; }
            }
        var regions = Regions(changed, cols, rows, cell, w, h);
        double ratio = totalCells == 0 ? 0 : (double)changedCells / totalCells;
        return new ImageDiffResult(ratio, regions, new Size(w, h), sizeDiffers);
    }

    /// <summary>Zusammenhängende geänderte Zellen (8er-Nachbarschaft) als umschließende Rechtecke, eine Zelle Rand, nach Lage sortiert.</summary>
    static List<Rectangle> Regions(bool[,] changed, int cols, int rows, int cell, int w, int h)
    {
        var seen = new bool[cols, rows];
        var list = new List<Rectangle>();
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                if (!changed[x, y] || seen[x, y]) continue;
                int minX = x, maxX = x, minY = y, maxY = y;
                var stack = new Stack<(int, int)>(); stack.Push((x, y)); seen[x, y] = true;
                while (stack.Count > 0)
                {
                    var (cx, cy) = stack.Pop();
                    minX = Math.Min(minX, cx); maxX = Math.Max(maxX, cx); minY = Math.Min(minY, cy); maxY = Math.Max(maxY, cy);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (nx < 0 || ny < 0 || nx >= cols || ny >= rows || seen[nx, ny] || !changed[nx, ny]) continue;
                            seen[nx, ny] = true; stack.Push((nx, ny));
                        }
                }
                var r = Rectangle.FromLTRB(Math.Max(0, minX * cell - cell / 2), Math.Max(0, minY * cell - cell / 2), Math.Min(w, (maxX + 1) * cell + cell / 2), Math.Min(h, (maxY + 1) * cell + cell / 2));
                list.Add(r);
            }
        return list.OrderBy(r => r.Y).ThenBy(r => r.X).ToList();
    }

    static Bitmap To32(Bitmap src, int w, int h)
    {
        var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode = src.Width == w && src.Height == h ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBilinear;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(src, new Rectangle(0, 0, w, h));
        return dst;
    }

    static int[] Pixels(Bitmap b)
    {
        var data = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var px = new int[b.Width * b.Height];
            if (data.Stride == b.Width * 4) Marshal.Copy(data.Scan0, px, 0, px.Length);
            else for (int y = 0; y < b.Height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, px, y * b.Width, b.Width);
            return px;
        }
        finally { b.UnlockBits(data); }
    }

    /// <summary>Kopie von <paramref name="b"/> mit hervorgehobenen Bereichen (Rahmen, leicht getönt).</summary>
    public static Bitmap Highlight(Bitmap b, IEnumerable<Rectangle> regions, Size? diffSize = null)
    {
        var dst = new Bitmap(b.Width, b.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dst);
        g.DrawImage(b, 0, 0, b.Width, b.Height);
        double sx = diffSize is { } ds ? (double)b.Width / ds.Width : 1, sy = diffSize is { } ds2 ? (double)b.Height / ds2.Height : 1;
        using var fill = new SolidBrush(Color.FromArgb(48, 230, 80, 30));
        using var pen = new Pen(Color.FromArgb(230, 220, 60, 20), Math.Max(2, b.Width / 500f));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var r0 in regions)
        {
            var r = new Rectangle((int)(r0.X * sx), (int)(r0.Y * sy), (int)(r0.Width * sx), (int)(r0.Height * sy));
            g.FillRectangle(fill, r); g.DrawRectangle(pen, r);
        }
        return dst;
    }

    /// <summary>Vorher/Nachher-Bild: A links, B rechts (Bereiche in B hervorgehoben), darüber je eine Beschriftung.</summary>
    public static Bitmap SideBySide(Bitmap? a, Bitmap? b, string labelA, string labelB, IEnumerable<Rectangle>? regions = null, Size? diffSize = null, int maxHalfWidth = 960)
    {
        const int bar = 34, gap = 12;
        Size Fit(Bitmap? x) => x == null ? new Size(maxHalfWidth / 2, maxHalfWidth / 3) : x.Width <= maxHalfWidth ? x.Size : new Size(maxHalfWidth, (int)Math.Round(x.Height * (double)maxHalfWidth / x.Width));
        Size sa = Fit(a), sb = Fit(b);
        int W = sa.Width + gap + sb.Width, H = bar + Math.Max(sa.Height, sb.Height);
        var dst = new Bitmap(W, H, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(dst);
        g.Clear(Color.FromArgb(0x24, 0x23, 0x20));
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Segoe UI Semibold", 15, GraphicsUnit.Pixel);
        var fmt = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(labelA, font, Brushes.White, new RectangleF(8, 0, sa.Width - 8, bar), fmt);
        g.DrawString(labelB, font, Brushes.White, new RectangleF(sa.Width + gap + 8, 0, sb.Width - 8, bar), fmt);
        using var empty = new SolidBrush(Color.FromArgb(0x3A, 0x38, 0x33));
        if (a != null) g.DrawImage(a, new Rectangle(0, bar, sa.Width, sa.Height)); else g.FillRectangle(empty, 0, bar, sa.Width, sa.Height);
        if (b != null)
        {
            using var hb = regions != null ? Highlight(b, regions, diffSize) : null;
            g.DrawImage(hb ?? b, new Rectangle(sa.Width + gap, bar, sb.Width, sb.Height));
        }
        else g.FillRectangle(empty, sa.Width + gap, bar, sb.Width, sb.Height);
        return dst;
    }
}
