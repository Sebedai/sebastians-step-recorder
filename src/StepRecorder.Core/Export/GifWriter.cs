using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace StepRecorder.Export;

/// <summary>
/// Schreibt animierte GIF89a-Dateien ohne Fremdbibliothek: je Bild eine lokale Farbtabelle (≤ 256 Farben, Octree,
/// ohne Dithering – UI-Text bleibt scharf), LZW-Kompression, Wiederholung per NETSCAPE2.0-Erweiterung.
/// Ab dem zweiten Bild wird nur das geänderte Rechteck geschrieben (Entsorgung „nicht entsorgen“);
/// ein unverändertes Bild wird als transparentes 1×1-Bild ausgegeben, damit die Zeitsteuerung stimmt.
/// </summary>
public static class GifWriter
{
    const int MaxColors = 256;

    /// <summary>Schreibt die Bilder als animiertes GIF in <paramref name="output"/> (der Stream bleibt offen).</summary>
    /// <param name="output">Zielstream.</param>
    /// <param name="frames">Bilder, alle gleich groß.</param>
    /// <param name="delaysMs">Anzeigedauer je Bild in Millisekunden (wird auf Hundertstelsekunden gerundet, mindestens 2).</param>
    /// <param name="loopCount">Anzahl der Wiederholungen; 0 = endlos.</param>
    /// <exception cref="ArgumentException">Keine Bilder, Anzahl der Verzögerungen passt nicht oder Bildgrößen weichen ab.</exception>
    public static void Write(Stream output, IReadOnlyList<Bitmap> frames, IReadOnlyList<int> delaysMs, int loopCount = 0)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(delaysMs);
        if (frames.Count == 0) throw new ArgumentException("Mindestens ein Bild erforderlich.", nameof(frames));
        if (delaysMs.Count != frames.Count)
            throw new ArgumentException("Je Bild wird genau eine Verzögerung erwartet.", nameof(delaysMs));
        if (loopCount is < 0 or > 0xFFFF) throw new ArgumentOutOfRangeException(nameof(loopCount));

        int width = frames[0]?.Width ?? throw new ArgumentException("Bild 0 ist null.", nameof(frames));
        int height = frames[0].Height;
        if (width is < 1 or > 0xFFFF || height is < 1 or > 0xFFFF)
            throw new ArgumentException("Bildgröße außerhalb des GIF-Bereichs.", nameof(frames));
        for (int i = 1; i < frames.Count; i++)
        {
            var f = frames[i] ?? throw new ArgumentException($"Bild {i} ist null.", nameof(frames));
            if (f.Width != width || f.Height != height)
                throw new ArgumentException($"Bild {i} hat {f.Width}×{f.Height}, erwartet {width}×{height}.", nameof(frames));
        }

        // Kopf, logischer Bildschirm (ohne globale Farbtabelle, Farbauflösung 8 Bit), NETSCAPE2.0-Schleife
        output.Write("GIF89a"u8);
        WriteU16(output, width);
        WriteU16(output, height);
        output.Write([0x70, 0, 0]);
        output.Write([0x21, 0xFF, 0x0B]);
        output.Write("NETSCAPE2.0"u8);
        output.Write([0x03, 0x01]);
        WriteU16(output, loopCount);
        output.WriteByte(0);

        int[]? prev = null;
        for (int i = 0; i < frames.Count; i++)
        {
            int[] px = ReadPixels(frames[i]);
            int delay = Math.Clamp((int)Math.Round(delaysMs[i] / 10.0, MidpointRounding.AwayFromZero), 2, 0xFFFF);
            if (prev is null)
                WriteImage(output, px, width, new Rectangle(0, 0, width, height), delay);
            else if (ChangedBounds(prev, px, width, height) is { } r)
                WriteImage(output, px, width, r, delay);
            else
                WriteUnchanged(output, delay);
            prev = px;
        }

        output.WriteByte(0x3B);
    }

    // ------------------------------------------------------------------ Pixel

    /// <summary>Liest das Bild als 0xRRGGBB-Werte (Alpha verworfen); GDI+ wandelt andere Formate selbst um.</summary>
    static int[] ReadPixels(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        var px = new int[w * h];
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < h; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, px, y * w, w);
        }
        finally { bmp.UnlockBits(data); }
        for (int i = 0; i < px.Length; i++) px[i] &= 0xFFFFFF;
        return px;
    }

    /// <summary>Kleinstes Rechteck, in dem sich <paramref name="cur"/> von <paramref name="prev"/> unterscheidet; null = identisch.</summary>
    static Rectangle? ChangedBounds(int[] prev, int[] cur, int w, int h)
    {
        int top = 0;
        while (top < h && Row(prev, top, w).SequenceEqual(Row(cur, top, w))) top++;
        if (top == h) return null;
        int bottom = h - 1;
        while (Row(prev, bottom, w).SequenceEqual(Row(cur, bottom, w))) bottom--;

        int left = w, right = -1;
        for (int y = top; y <= bottom; y++)
        {
            var a = Row(prev, y, w);
            var b = Row(cur, y, w);
            int l = a.CommonPrefixLength(b);
            if (l == w) continue;
            if (l < left) left = l;
            int r = w - 1;
            while (r > right && a[r] == b[r]) r--;
            if (r > right) right = r;
        }
        return new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    static ReadOnlySpan<int> Row(int[] px, int y, int w) => px.AsSpan(y * w, w);

    // ------------------------------------------------------------------ Bilder

    static void WriteImage(Stream s, int[] px, int stride, Rectangle r, int delay)
    {
        var indices = new byte[r.Width * r.Height];
        int[] palette = Quantize(px, stride, r, indices);
        int bits = 1;
        while ((1 << bits) < palette.Length) bits++;

        WriteGce(s, delay, transparent: false);
        WriteDescriptor(s, r, bits);
        var table = new byte[3 << bits];
        for (int i = 0; i < palette.Length; i++)
        {
            table[3 * i] = (byte)(palette[i] >> 16);
            table[3 * i + 1] = (byte)(palette[i] >> 8);
            table[3 * i + 2] = (byte)palette[i];
        }
        s.Write(table);
        WriteLzw(s, indices, Math.Max(2, bits));
    }

    /// <summary>Unverändertes Bild: transparentes 1×1-Pixel, damit nur die Anzeigedauer zählt.</summary>
    static void WriteUnchanged(Stream s, int delay)
    {
        WriteGce(s, delay, transparent: true);
        WriteDescriptor(s, new Rectangle(0, 0, 1, 1), 1);
        s.Write(new byte[6]);
        WriteLzw(s, [0], 2);
    }

    static void WriteGce(Stream s, int delay, bool transparent)
    {
        // Entsorgung 1 = nicht entsorgen; Transparenzindex 0
        s.Write([0x21, 0xF9, 0x04, (byte)(0x04 | (transparent ? 1 : 0))]);
        WriteU16(s, delay);
        s.Write([0, 0]);
    }

    static void WriteDescriptor(Stream s, Rectangle r, int tableBits)
    {
        s.WriteByte(0x2C);
        WriteU16(s, r.X);
        WriteU16(s, r.Y);
        WriteU16(s, r.Width);
        WriteU16(s, r.Height);
        s.WriteByte((byte)(0x80 | (tableBits - 1)));
    }

    static void WriteU16(Stream s, int v)
    {
        s.WriteByte((byte)v);
        s.WriteByte((byte)(v >> 8));
    }

    // ------------------------------------------------------------------ Farbreduktion

    /// <summary>
    /// Ermittelt die Farbtabelle für den Ausschnitt und füllt <paramref name="indices"/>. Bis 256 Farben exakt,
    /// sonst Octree mit anschließender Nächste-Farbe-Zuordnung (kein Dithering).
    /// </summary>
    static int[] Quantize(int[] px, int stride, Rectangle r, byte[] indices)
    {
        var hist = new Dictionary<int, int>();
        int last = -1, run = 0;
        for (int y = r.Top; y < r.Bottom; y++)
        {
            int row = y * stride;
            for (int x = r.Left; x < r.Right; x++)
            {
                int c = px[row + x];
                if (c == last) { run++; continue; }
                if (run > 0) CollectionsMarshal.GetValueRefOrAddDefault(hist, last, out _) += run;
                last = c;
                run = 1;
            }
        }
        CollectionsMarshal.GetValueRefOrAddDefault(hist, last, out _) += run;

        int[] palette;
        var map = new Dictionary<int, byte>(hist.Count);
        if (hist.Count <= MaxColors)
        {
            palette = new int[hist.Count];
            int i = 0;
            foreach (int c in hist.Keys) { palette[i] = c; map[c] = (byte)i++; }
        }
        else
        {
            // Häufige Farben (Fensterflächen, Text, Weiß) exakt übernehmen – sonst verfärben große Flächen.
            // Den Rest teilt sich der Octree; eine Farbe zählt als häufig ab 0,1 % der Fläche.
            int total = r.Width * r.Height, minCount = Math.Max(16, total / 1000);
            var exact = hist.Where(kv => kv.Value >= minCount).OrderByDescending(kv => kv.Value).Take(MaxColors / 2).Select(kv => kv.Key).ToList();
            var exactSet = exact.ToHashSet();
            var rest = new Dictionary<int, int>(hist.Count);
            foreach (var kv in hist) if (!exactSet.Contains(kv.Key)) rest[kv.Key] = kv.Value;
            var tree = rest.Count == 0 ? Array.Empty<int>() : Octree.Build(rest, MaxColors - exact.Count);
            palette = exact.Concat(tree.Where(c => !exactSet.Contains(c))).Take(MaxColors).ToArray();
            for (int i = 0; i < exact.Count; i++) map[exact[i]] = (byte)i;
            foreach (int c in rest.Keys) map[c] = Nearest(palette, c);
        }

        int k = 0;
        last = -1;
        byte idx = 0;
        for (int y = r.Top; y < r.Bottom; y++)
        {
            int row = y * stride;
            for (int x = r.Left; x < r.Right; x++)
            {
                int c = px[row + x];
                if (c != last) { idx = map[c]; last = c; }
                indices[k++] = idx;
            }
        }
        return palette;
    }

    static byte Nearest(int[] palette, int c)
    {
        int r = (c >> 16) & 0xFF, g = (c >> 8) & 0xFF, b = c & 0xFF;
        int best = 0, bestD = int.MaxValue;
        for (int i = 0; i < palette.Length; i++)
        {
            int p = palette[i];
            int dr = ((p >> 16) & 0xFF) - r, dg = ((p >> 8) & 0xFF) - g, db = (p & 0xFF) - b;
            int d = dr * dr + dg * dg + db * db;
            if (d < bestD) { bestD = d; best = i; if (d == 0) break; }
        }
        return (byte)best;
    }

    /// <summary>Octree-Farbreduktion; Blätter auf Tiefe 6, zusammengelegt werden zuerst die seltensten tiefsten Knoten.</summary>
    static class Octree
    {
        const int Depth = 6;

        sealed class Node
        {
            public long R, G, B, N;
            public Node?[]? Kids;
        }

        public static int[] Build(Dictionary<int, int> hist, int maxColors)
        {
            var root = new Node();
            var levels = new List<Node>[Depth];
            for (int l = 0; l < Depth; l++) levels[l] = [];
            int leaves = 0;

            foreach (var (c, n) in hist)
            {
                int r = (c >> 16) & 0xFF, g = (c >> 8) & 0xFF, b = c & 0xFF;
                var node = root;
                for (int l = 0; l < Depth; l++)
                {
                    int s = 7 - l;
                    int i = (((r >> s) & 1) << 2) | (((g >> s) & 1) << 1) | ((b >> s) & 1);
                    if (node.Kids is null) { node.Kids = new Node[8]; levels[l].Add(node); }
                    var kid = node.Kids[i];
                    if (kid is null)
                    {
                        kid = node.Kids[i] = new Node();
                        if (l == Depth - 1) leaves++;
                    }
                    node = kid;
                }
                node.R += (long)r * n;
                node.G += (long)g * n;
                node.B += (long)b * n;
                node.N += n;
            }

            // Von unten nach oben: auf jeder Ebene die Knoten mit den wenigsten Pixeln zuerst zusammenlegen
            for (int l = Depth - 1; l >= 0 && leaves > maxColors; l--)
            {
                var nodes = levels[l];
                var totals = new long[nodes.Count];
                for (int i = 0; i < nodes.Count; i++)
                    foreach (var k in nodes[i].Kids!) if (k is not null) totals[i] += k.N;
                var order = nodes.ToArray();
                Array.Sort(totals, order);
                foreach (var node in order)
                {
                    int kids = 0;
                    foreach (var k in node.Kids!)
                    {
                        if (k is null) continue;
                        node.R += k.R; node.G += k.G; node.B += k.B; node.N += k.N;
                        kids++;
                    }
                    node.Kids = null;
                    leaves -= kids - 1;
                    if (leaves <= maxColors) break;
                }
            }

            var palette = new List<int>(leaves);
            Collect(root, palette);
            return palette.ToArray();
        }

        static void Collect(Node node, List<int> palette)
        {
            if (node.Kids is null)
            {
                if (node.N == 0) return;
                long h = node.N / 2;
                int r = (int)((node.R + h) / node.N), g = (int)((node.G + h) / node.N), b = (int)((node.B + h) / node.N);
                palette.Add((r << 16) | (g << 8) | b);
                return;
            }
            foreach (var k in node.Kids) if (k is not null) Collect(k, palette);
        }
    }

    // ------------------------------------------------------------------ LZW

    /// <summary>LZW mit variabler Codelänge bis 12 Bit, Clear-Code bei voller Tabelle, Unterblöcke ≤ 255 Byte.</summary>
    static void WriteLzw(Stream s, byte[] data, int minCodeSize)
    {
        const int HashBits = 13, HashSize = 1 << HashBits, HashMask = HashSize - 1, MaxCode = 4096;
        s.WriteByte((byte)minCodeSize);
        var w = new BlockWriter(s);
        int clear = 1 << minCodeSize, eoi = clear + 1;
        int codeSize = minCodeSize + 1, next = clear + 2;
        var keys = new int[HashSize];   // Schlüssel + 1, 0 = frei
        var codes = new short[HashSize];

        w.Put(clear, codeSize);
        int prefix = data[0];
        for (int i = 1; i < data.Length; i++)
        {
            int p = data[i];
            int key = (prefix << 8) | p;
            int h = (int)(((uint)key * 0x9E3779B1u) >> (32 - HashBits));
            while (keys[h] != 0 && keys[h] != key + 1) h = (h + 1) & HashMask;
            if (keys[h] != 0) { prefix = codes[h]; continue; }

            w.Put(prefix, codeSize);
            if (next < MaxCode)
            {
                keys[h] = key + 1;
                codes[h] = (short)next;
                if (next == 1 << codeSize && codeSize < 12) codeSize++;
                next++;
            }
            else
            {
                w.Put(clear, codeSize);
                Array.Clear(keys);
                codeSize = minCodeSize + 1;
                next = clear + 2;
            }
            prefix = p;
        }
        w.Put(prefix, codeSize);
        if (next == 1 << codeSize && codeSize < 12) codeSize++;
        w.Put(eoi, codeSize);
        w.Flush();
        s.WriteByte(0);
    }

    /// <summary>Packt Codes LSB-zuerst in Datenunterblöcke zu höchstens 255 Byte.</summary>
    sealed class BlockWriter(Stream s)
    {
        readonly byte[] _buf = new byte[256];
        int _len;
        uint _acc;
        int _bits;

        public void Put(int code, int size)
        {
            _acc |= (uint)code << _bits;
            _bits += size;
            while (_bits >= 8)
            {
                Byte((byte)_acc);
                _acc >>= 8;
                _bits -= 8;
            }
        }

        public void Flush()
        {
            if (_bits > 0) { Byte((byte)_acc); _acc = 0; _bits = 0; }
            if (_len > 0) Emit();
        }

        void Byte(byte b)
        {
            _buf[++_len] = b;
            if (_len == 255) Emit();
        }

        void Emit()
        {
            _buf[0] = (byte)_len;
            s.Write(_buf, 0, _len + 1);
            _len = 0;
        }
    }
}
