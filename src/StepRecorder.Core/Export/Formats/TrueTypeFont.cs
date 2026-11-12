using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace StepRecorder.Export.Formats;

/// <summary>
/// Liest eine TrueType-Schrift (glyf-Umrisse) so weit, wie das PDF sie braucht: Zeichen → Glyph (cmap 4/12), Breiten (hmtx),
/// Kennzahlen (head, hhea, OS/2) und Einbettungsrechte (fsType). <see cref="Subset"/> erzeugt eine Teilmenge mit
/// erhaltenen Glyphennummern (CID = GID), nicht benutzte Glyphen sind leer.
/// </summary>
public sealed class TrueTypeFont
{
    readonly byte[] _d;
    readonly Dictionary<string, (int off, int len)> _tables = new();
    readonly ConcurrentDictionary<int, int> _glyphCache = new();
    public string PostScriptName { get; }
    public int UnitsPerEm { get; }
    public int NumGlyphs { get; }
    public short XMin { get; }
    public short YMin { get; }
    public short XMax { get; }
    public short YMax { get; }
    public short Ascent { get; }
    public short Descent { get; }
    public short CapHeight { get; }
    public ushort FsType { get; }
    readonly int _numHMetrics, _locFormat;
    readonly (int platform, int encoding, int offset)[] _cmaps;

    /// <summary>Einbetten erlaubt (nicht „eingeschränkte Lizenz“, nicht „nur Bitmaps“).</summary>
    public bool EmbeddingAllowed => (FsType & 0x0002) == 0 && (FsType & 0x0200) == 0;
    /// <summary>Teilmengen verboten: dann wird die ganze Schrift eingebettet.</summary>
    public bool NoSubsetting => (FsType & 0x0100) != 0;

    ushort U16(int o) => BinaryPrimitives.ReadUInt16BigEndian(_d.AsSpan(o));
    short I16(int o) => BinaryPrimitives.ReadInt16BigEndian(_d.AsSpan(o));
    uint U32(int o) => BinaryPrimitives.ReadUInt32BigEndian(_d.AsSpan(o));
    int T(string tag) => _tables.TryGetValue(tag, out var t) ? t.off : throw new InvalidDataException("Tabelle fehlt: " + tag);

    public TrueTypeFont(byte[] data, string postScriptName)
    {
        _d = data;
        PostScriptName = postScriptName;
        uint version = U32(0);
        if (version != 0x00010000 && version != 0x74727565) throw new InvalidDataException("keine TrueType-Schrift");
        int n = U16(4);
        for (int i = 0; i < n; i++)
        {
            int rec = 12 + i * 16;
            string tag = System.Text.Encoding.ASCII.GetString(_d, rec, 4);
            int off = (int)U32(rec + 8), len = (int)U32(rec + 12);
            if (off < 0 || len < 0 || off + len > _d.Length) throw new InvalidDataException("Tabelle außerhalb der Datei: " + tag);
            _tables[tag] = (off, len);
        }
        foreach (var req in new[] { "head", "hhea", "maxp", "hmtx", "loca", "glyf" }) T(req);
        int head = T("head");
        UnitsPerEm = U16(head + 18);
        XMin = I16(head + 36); YMin = I16(head + 38); XMax = I16(head + 40); YMax = I16(head + 42);
        _locFormat = I16(head + 50);
        int hhea = T("hhea");
        Ascent = I16(hhea + 4); Descent = I16(hhea + 6);
        _numHMetrics = U16(hhea + 34);
        NumGlyphs = U16(T("maxp") + 4);
        if (_tables.TryGetValue("OS/2", out var os2))
        {
            FsType = U16(os2.off + 8);
            CapHeight = os2.len >= 90 && U16(os2.off) >= 2 ? I16(os2.off + 88) : (short)(Ascent * 0.7);
        }
        else CapHeight = (short)(Ascent * 0.7);
        if (_tables.TryGetValue("cmap", out var cm))
        {
            int nt = U16(cm.off + 2);
            _cmaps = Enumerable.Range(0, nt).Select(i => ((int)U16(cm.off + 4 + i * 8), (int)U16(cm.off + 6 + i * 8), cm.off + (int)U32(cm.off + 8 + i * 8))).ToArray();
        }
        else _cmaps = Array.Empty<(int, int, int)>();   // z. B. eine Teilmenge (CID = GID)
    }

    /// <summary>Lädt eine Schrift aus dem Windows-Schriftordner (null, wenn nicht vorhanden oder nicht lesbar).</summary>
    public static TrueTypeFont? TryLoadSystem(string file, string postScriptName)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), file);
            if (!File.Exists(path)) return null;
            var f = new TrueTypeFont(File.ReadAllBytes(path), postScriptName);
            return f.EmbeddingAllowed ? f : null;
        }
        catch { return null; }
    }

    /// <summary>Glyphennummer eines Unicode-Zeichens (0 = nicht vorhanden).</summary>
    public int GlyphFor(int cp) => _glyphCache.GetOrAdd(cp, LookupGlyph);

    int LookupGlyph(int cp)
    {
        // bevorzugt Windows Unicode voll (3,10), dann Windows BMP (3,1), dann Unicode-Plattform
        foreach (var (p, e, off) in _cmaps.OrderBy(c => c switch { (3, 10, _) => 0, (0, 4, _) or (0, 6, _) => 1, (3, 1, _) => 2, (0, _, _) => 3, _ => 9 }))
        {
            if (p is not (0 or 3)) continue;
            int fmt = U16(off);
            int g = fmt switch { 4 => Format4(off, cp), 12 => Format12(off, cp), _ => -1 };
            if (g > 0 && g < NumGlyphs) return g;
            if (g == 0 && fmt == 12) return 0;
        }
        return 0;
    }

    int Format4(int off, int cp)
    {
        if (cp > 0xFFFF) return 0;
        int segX2 = U16(off + 6), seg = segX2 / 2;
        int ends = off + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
        int lo = 0, hi = seg - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            int end = U16(ends + mid * 2);
            if (cp > end) { lo = mid + 1; continue; }
            int start = U16(starts + mid * 2);
            if (cp < start) { hi = mid - 1; continue; }
            int delta = I16(deltas + mid * 2), ro = U16(ranges + mid * 2);
            if (ro == 0) return (cp + delta) & 0xFFFF;
            int addr = ranges + mid * 2 + ro + 2 * (cp - start);
            if (addr + 2 > _d.Length) return 0;
            int g = U16(addr);
            return g == 0 ? 0 : (g + delta) & 0xFFFF;
        }
        return 0;
    }

    int Format12(int off, int cp)
    {
        uint groups = U32(off + 12);
        int lo = 0, hi = (int)groups - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2, rec = off + 16 + mid * 12;
            uint s = U32(rec), e = U32(rec + 4);
            if (cp < s) hi = mid - 1;
            else if (cp > e) lo = mid + 1;
            else return (int)(U32(rec + 8) + (uint)cp - s);
        }
        return 0;
    }

    /// <summary>Vorschubbreite eines Glyphs in Schrifteinheiten.</summary>
    public int Advance(int gid)
    {
        int hmtx = T("hmtx");
        int i = Math.Min(gid, _numHMetrics - 1);
        return U16(hmtx + i * 4);
    }

    /// <summary>Breite in tausendstel Geviert (PDF-Einheiten).</summary>
    public double Width1000(int gid) => Advance(gid) * 1000.0 / UnitsPerEm;

    (int start, int end) GlyphRange(int gid)
    {
        int loca = T("loca");
        return _locFormat == 0 ? (U16(loca + gid * 2) * 2, U16(loca + gid * 2 + 2) * 2) : ((int)U32(loca + gid * 4), (int)U32(loca + gid * 4 + 4));
    }

    /// <summary>Bestandteile eines zusammengesetzten Glyphs.</summary>
    IEnumerable<int> Components(int gid)
    {
        var (s, e) = GlyphRange(gid);
        if (e - s < 10) yield break;
        int glyf = T("glyf"), p = glyf + s;
        if (I16(p) >= 0) yield break;
        p += 10;
        while (true)
        {
            int flags = U16(p), comp = U16(p + 2);
            yield return comp;
            p += 4 + ((flags & 0x0001) != 0 ? 4 : 2);
            if ((flags & 0x0008) != 0) p += 2; else if ((flags & 0x0040) != 0) p += 4; else if ((flags & 0x0080) != 0) p += 8;
            if ((flags & 0x0020) == 0) break;
        }
    }

    /// <summary>
    /// Teilmenge als eigenständige TrueType-Datei: alle Glyphennummern bleiben, nur die benutzten (samt Bestandteilen und
    /// Glyph 0) haben Umrisse. Tabellen: head, hhea, maxp, hmtx, loca (lang), glyf, dazu cvt/fpgm/prep, falls vorhanden.
    /// </summary>
    public byte[] Subset(IEnumerable<int> gids)
    {
        if (NoSubsetting) return _d;
        var keep = new HashSet<int> { 0 };
        var stack = new Stack<int>(gids.Where(g => g > 0 && g < NumGlyphs));
        while (stack.Count > 0)
        {
            int g = stack.Pop();
            if (!keep.Add(g)) continue;
            foreach (var c in Components(g)) if (c < NumGlyphs && !keep.Contains(c)) stack.Push(c);
        }
        int glyfOff = T("glyf");
        using var glyf = new MemoryStream();
        var loca = new uint[NumGlyphs + 1];
        for (int g = 0; g < NumGlyphs; g++)
        {
            loca[g] = (uint)glyf.Length;
            if (!keep.Contains(g)) continue;
            var (s, e) = GlyphRange(g);
            if (e > s) glyf.Write(_d, glyfOff + s, e - s);
            while (glyf.Length % 4 != 0) glyf.WriteByte(0);
        }
        loca[NumGlyphs] = (uint)glyf.Length;
        var locaBytes = new byte[loca.Length * 4];
        for (int i = 0; i < loca.Length; i++) BinaryPrimitives.WriteUInt32BigEndian(locaBytes.AsSpan(i * 4), loca[i]);

        var head = Table("head");
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(8), 0);         // checkSumAdjustment später
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(50), 1);         // lange loca
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["head"] = head, ["hhea"] = Table("hhea"), ["maxp"] = Table("maxp"), ["hmtx"] = Table("hmtx"), ["loca"] = locaBytes, ["glyf"] = glyf.ToArray()
        };
        foreach (var opt in new[] { "cvt ", "fpgm", "prep" }) if (_tables.ContainsKey(opt)) tables[opt] = Table(opt);
        return Assemble(tables);
    }

    byte[] Table(string tag) { var (o, l) = _tables[tag]; return _d.AsSpan(o, l).ToArray(); }

    static uint CheckSum(ReadOnlySpan<byte> b)
    {
        uint sum = 0;
        for (int i = 0; i < b.Length; i += 4)
        {
            uint v = 0;
            for (int k = 0; k < 4; k++) v = (v << 8) | (i + k < b.Length ? b[i + k] : 0u);
            sum += v;
        }
        return sum;
    }

    static byte[] Assemble(SortedDictionary<string, byte[]> tables)
    {
        int n = tables.Count;
        int pow = 1, log = 0;
        while (pow * 2 <= n) { pow *= 2; log++; }
        using var ms = new MemoryStream();
        var hdr = new byte[12 + 16 * n];
        BinaryPrimitives.WriteUInt32BigEndian(hdr.AsSpan(0), 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(hdr.AsSpan(4), (ushort)n);
        BinaryPrimitives.WriteUInt16BigEndian(hdr.AsSpan(6), (ushort)(pow * 16));
        BinaryPrimitives.WriteUInt16BigEndian(hdr.AsSpan(8), (ushort)log);
        BinaryPrimitives.WriteUInt16BigEndian(hdr.AsSpan(10), (ushort)(n * 16 - pow * 16));
        int offset = hdr.Length, i = 0, headOffset = 0;
        var body = new MemoryStream();
        foreach (var (tag, data) in tables)
        {
            int rec = 12 + i++ * 16;
            System.Text.Encoding.ASCII.GetBytes(tag).CopyTo(hdr, rec);
            BinaryPrimitives.WriteUInt32BigEndian(hdr.AsSpan(rec + 4), CheckSum(data));
            BinaryPrimitives.WriteUInt32BigEndian(hdr.AsSpan(rec + 8), (uint)(offset + body.Length));
            BinaryPrimitives.WriteUInt32BigEndian(hdr.AsSpan(rec + 12), (uint)data.Length);
            if (tag == "head") headOffset = offset + (int)body.Length;
            body.Write(data);
            while (body.Length % 4 != 0) body.WriteByte(0);
        }
        ms.Write(hdr); body.Position = 0; body.CopyTo(ms);
        var font = ms.ToArray();
        uint adjust = 0xB1B0AFBA - CheckSum(font);
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(headOffset + 8), adjust);
        return font;
    }
}
