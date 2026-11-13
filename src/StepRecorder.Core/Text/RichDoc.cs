using System.Text;

namespace StepRecorder.Text;

/// <summary>Art eines Inline-Elements des formatierten Texts.</summary>
public enum RichKind { Text, Code, Key, Ui, Link, Break, Placeholder, Ref }

/// <summary>Fett/kursiv eines Textlaufs.</summary>
[Flags] public enum RichStyle { None = 0, Bold = 1, Italic = 2 }

/// <summary>
/// Ein Inline-Element: Textlauf (mit Stil), Eingabe/Code, Tastenkombination (<see cref="Text"/> = Tasten mit „+“), Name eines
/// Bedienelements, Link (Beschriftung in <see cref="Text"/>, Ziel in <see cref="Target"/>), Zeilenumbruch oder ein von der Leseschicht
/// eingesetzter Platzhalter (Schwärzung, Verallgemeinerung – nie Auszeichnung) oder ein Querverweis (<see cref="RichKind.Ref"/>:
/// Ziel in <see cref="Target"/>, z. B. <c>s1203</c> für den Schritt mit Anker 1203, <c>k1203</c> für das dort beginnende Kapitel;
/// <see cref="Text"/> ist die Beschriftung, die erst die Anleitung mit der gedruckten Nummer füllt).
/// </summary>
public sealed record RichInline(RichKind Kind, string Text, RichStyle Style = RichStyle.None, string? Target = null);

public enum RichBlockKind { Paragraph, Bullet, Numbered, Table }

/// <summary>
/// Absatz, Aufzählung, nummerierte Liste oder Tabelle. Listen tragen je Punkt eine Inline-Folge; Tabellen Zeilen aus Zellen
/// (<see cref="Rows"/>), mit <see cref="Header"/> ist die erste Zeile die Kopfzeile.
/// </summary>
public sealed record RichBlock(RichBlockKind Kind, IReadOnlyList<RichInline>? Inlines = null, IReadOnlyList<IReadOnlyList<RichInline>>? Items = null, int Start = 1,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<RichInline>>>? Rows = null, bool Header = false)
{
    /// <summary>Spaltenzahl einer Tabelle (größte Zeile).</summary>
    public int Columns => Rows?.Max(r => r.Count) ?? 0;
}

/// <summary>
/// Formatierter Text (13, Paket 21; 09 §11.1): eine kleine Auszeichnung in den vorhandenen Textfeldern, nur für Texte mit dem Kennzeichen
/// <c>fmt: 1</c>. Eigener, linearer Parser ohne Ausnahmen – was nicht passt, bleibt Text.
/// <list type="bullet">
/// <item>Blöcke: Leerzeile trennt Absätze, ein Zeilenumbruch bleibt ein Umbruch; <c>- </c> Aufzählung, <c>1. </c>/<c>1) </c> Nummerierung
/// (Folgezeilen eines Punkts mit mindestens zwei Leerzeichen eingerückt).</item>
/// <item>Inline: <c>**fett**</c>, <c>*kursiv*</c>, <c>***beides***</c> (nie mitten im Wort), <c>`Eingabe`</c>, <c>[[Strg+S]]</c> Tasten,
/// <c>[[ui:Speichern]]</c> Bedienelement, <c>[Text](https://…)</c> Link (nur http, https, mailto).</item>
/// <item>Maskierung: <c>\</c> vor <c>* ` [ ]</c> (am Zeilenanfang auch <c>-</c> und Ziffern); sonst ist ein Backslash wörtlich, damit
/// Windows-Pfade unverändert bleiben.</item>
/// </list>
/// U+FDD0 … U+FDD1 fassen Stellen, die die Leseschicht eingesetzt hat (Schwärzung, Platzhalter); sie sind nie Auszeichnung.
/// </summary>
public sealed class RichDoc
{
    public const char MarkOpen = '﷐', MarkClose = '﷑';
    public IReadOnlyList<RichBlock> Blocks { get; }
    public RichDoc(IReadOnlyList<RichBlock> blocks) => Blocks = blocks;

    // ---------------------------------------------------------------------------------
    // Blöcke
    // ---------------------------------------------------------------------------------

    public static RichDoc Parse(string? markup)
    {
        var blocks = new List<RichBlock>();
        if (string.IsNullOrEmpty(markup)) return new RichDoc(blocks);
        var lines = markup.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int i = 0;
        var para = new List<string>();
        void FlushPara()
        {
            if (para.Count == 0) return;
            var inl = new List<RichInline>();
            for (int k = 0; k < para.Count; k++)
            {
                if (k > 0) inl.Add(new RichInline(RichKind.Break, "\n"));
                inl.AddRange(ParseInline(para[k]));
            }
            blocks.Add(new RichBlock(RichBlockKind.Paragraph, inl));
            para.Clear();
        }
        while (i < lines.Length)
        {
            string line = lines[i];
            if (line.Trim().Length == 0) { FlushPara(); i++; continue; }
            // Tabelle: zusammenhängende Zeilen, die mit „|“ beginnen (13, Paket 23)
            if (line.StartsWith('|'))
            {
                FlushPara();
                var rows = new List<IReadOnlyList<IReadOnlyList<RichInline>>>();
                bool header = false;
                while (i < lines.Length && lines[i].StartsWith('|'))
                {
                    var cells = SplitCells(lines[i]);
                    if (rows.Count == 1 && !header && cells.All(c => c.Trim().Length > 0 && c.Trim().All(ch => ch is '-' or ':'))) { header = true; i++; continue; }
                    if (cells.Count > MaxColumns) cells = cells.Take(MaxColumns - 1).Append(string.Join(" | ", cells.Skip(MaxColumns - 1))).ToList();
                    rows.Add(cells.Select(c => (IReadOnlyList<RichInline>)ParseInline(c.Trim())).ToList());
                    i++;
                }
                blocks.Add(new RichBlock(RichBlockKind.Table, Rows: rows, Header: header && rows.Count > 1));
                continue;
            }
            if (ListMarker(line, out var kind, out int start, out string first))
            {
                FlushPara();
                var items = new List<IReadOnlyList<RichInline>>();
                int listStart = start;
                while (i < lines.Length && ListMarker(lines[i], out var k2, out _, out string text) && k2 == kind)
                {
                    var parts = new List<string> { text };
                    i++;
                    while (i < lines.Length && lines[i].StartsWith("  ") && lines[i].Trim().Length > 0 && !ListMarker(lines[i].TrimStart(), out _, out _, out _)) { parts.Add(lines[i].Trim()); i++; }
                    var inl = new List<RichInline>();
                    for (int k = 0; k < parts.Count; k++) { if (k > 0) inl.Add(new RichInline(RichKind.Break, "\n")); inl.AddRange(ParseInline(parts[k])); }
                    items.Add(inl);
                }
                blocks.Add(new RichBlock(kind, Items: items, Start: listStart));
                continue;
            }
            para.Add(line);
            i++;
        }
        FlushPara();
        return new RichDoc(blocks);
    }

    public const int MaxColumns = 12;

    /// <summary>Zellen einer Tabellenzeile: an „|“ geteilt, außer maskiert (<c>\|</c>) oder in einem Platzhalter; äußere Striche entfallen.</summary>
    static List<string> SplitCells(string line)
    {
        var cells = new List<string>();
        var cur = new StringBuilder();
        string s = line.TrimEnd();
        int start = s.StartsWith('|') ? 1 : 0, end = s.Length;
        if (end > start && s[end - 1] == '|' && (end < 2 || s[end - 2] != '\\')) end--;
        for (int i = start; i < end; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < end && s[i + 1] == '|') { cur.Append("\\|"); i++; continue; }
            if (c == MarkOpen) { int e = s.IndexOf(MarkClose, i + 1); if (e > i) { cur.Append(s, i, e - i + 1); i = e; continue; } }
            if (c == '|') { cells.Add(cur.ToString()); cur.Clear(); continue; }
            cur.Append(c);
        }
        cells.Add(cur.ToString());
        return cells;
    }

    static bool ListMarker(string line, out RichBlockKind kind, out int start, out string text)
    {
        kind = RichBlockKind.Bullet; start = 1; text = "";
        if (line.StartsWith("- ")) { text = line[2..]; return true; }
        int n = 0;
        while (n < line.Length && n < 4 && char.IsAsciiDigit(line[n])) n++;
        if (n > 0 && n + 1 < line.Length && (line[n] == '.' || line[n] == ')') && line[n + 1] == ' ')
        {
            kind = RichBlockKind.Numbered; start = int.Parse(line[..n]); text = line[(n + 2)..];
            return true;
        }
        return false;
    }

    // ---------------------------------------------------------------------------------
    // Inline
    // ---------------------------------------------------------------------------------

    static bool IsEscapable(char c) => c is '*' or '`' or '[' or ']' or '|';
    static bool IsPunctOrSpace(char c) => char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c);

    public static List<RichInline> ParseInline(string s) => ParseInline(s, RichStyle.None, allowLinks: true);

    static List<RichInline> ParseInline(string s, RichStyle style, bool allowLinks)
    {
        var res = new List<RichInline>();
        var text = new StringBuilder();
        void Flush() { if (text.Length > 0) { res.Add(new RichInline(RichKind.Text, text.ToString(), style)); text.Clear(); } }
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            // Platzhalter der Leseschicht: unteilbar, nie Auszeichnung
            if (c == MarkOpen)
            {
                int e = s.IndexOf(MarkClose, i + 1);
                if (e > i) { Flush(); res.Add(new RichInline(RichKind.Placeholder, s[(i + 1)..e], style)); i = e + 1; continue; }
                i++; continue;
            }
            if (c == MarkClose) { i++; continue; }
            if (c == '\\' && i + 1 < s.Length && (IsEscapable(s[i + 1]) || (i == 0 && ListMarker(s[1..], out _, out _, out _))))
            { text.Append(s[i + 1]); i += 2; continue; }
            if (c == '`')
            {
                int n = 0; while (i + n < s.Length && s[i + n] == '`') n++;
                string delim = new('`', n);
                int e = FindRun(s, delim, i + n);
                if (e > i + n)
                {
                    Flush();
                    string code = s[(i + n)..e];
                    if (code.Length > 1 && code[0] == ' ' && code[^1] == ' ' && code.Trim().Length > 0) code = code[1..^1];
                    res.Add(new RichInline(RichKind.Code, StripMarks(code), style));
                    i = e + n; continue;
                }
                text.Append(delim); i += n; continue;
            }
            if (c == '[' && i + 1 < s.Length && s[i + 1] == '[')
            {
                int e = s.IndexOf("]]", i + 2, StringComparison.Ordinal);
                if (e > i + 2 && s.IndexOf('\n', i, e - i) < 0)
                {
                    string inner = s[(i + 2)..e];
                    if (inner.StartsWith("ui:", StringComparison.Ordinal) && inner.Length > 3) { Flush(); res.Add(new RichInline(RichKind.Ui, StripMarks(inner[3..]), style)); i = e + 2; continue; }
                    if (inner.StartsWith("ref:", StringComparison.Ordinal) && IsRefTarget(inner[4..])) { Flush(); res.Add(new RichInline(RichKind.Ref, RefPending, style, inner[4..])); i = e + 2; continue; }
                    if (!inner.Contains(':') && inner.Trim().Length > 0 && inner.Length <= 60) { Flush(); res.Add(new RichInline(RichKind.Key, StripMarks(inner.Trim()), style)); i = e + 2; continue; }
                }
            }
            if (c == '[' && allowLinks && TryLink(s, i, out int end, out string label, out string url))
            {
                Flush();
                res.Add(new RichInline(RichKind.Link, label, style, url));
                i = end; continue;
            }
            if (c == '*')
            {
                int n = 0; while (i + n < s.Length && s[i + n] == '*') n++;
                int take = Math.Min(n, 3);
                bool canOpen = (i == 0 || IsPunctOrSpace(s[i - 1])) && i + n < s.Length && !char.IsWhiteSpace(s[i + n]);
                if (canOpen && n <= 3)
                {
                    int close = FindCloser(s, i + n, take);
                    if (close > i + n)
                    {
                        Flush();
                        var add = take switch { 1 => RichStyle.Italic, 2 => RichStyle.Bold, _ => RichStyle.Bold | RichStyle.Italic };
                        res.AddRange(ParseInline(s[(i + n)..close], style | add, allowLinks));
                        i = close + take; continue;
                    }
                }
                text.Append('*', n); i += n; continue;
            }
            text.Append(c); i++;
        }
        Flush();
        return Merge(res);
    }

    /// <summary>Beschriftung eines noch nicht aufgelösten Querverweises (KI-Paket, Suche, Editor ohne Nummern).</summary>
    public const string RefPending = "→";

    /// <summary>Ziel eines Querverweises: <c>s</c> (Schritt) oder <c>k</c> (Kapitel) und ein Anker (bei eingefügten Schritten negativ).</summary>
    public static bool IsRefTarget(string t) => t.Length is >= 2 and <= 21 && t[0] is 's' or 'k' && long.TryParse(t.AsSpan(1), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out _);

    /// <summary>Alle Querverweisziele des Dokuments.</summary>
    public IEnumerable<string> References() => AllInlines().Where(x => x.Kind == RichKind.Ref && x.Target != null).Select(x => x.Target!);

    /// <summary>Alle Inline-Elemente (Absätze, Listenpunkte, Tabellenzellen).</summary>
    public IEnumerable<RichInline> AllInlines()
    {
        foreach (var b in Blocks)
        {
            if (b.Inlines != null) foreach (var x in b.Inlines) yield return x;
            if (b.Items != null) foreach (var it in b.Items) foreach (var x in it) yield return x;
            if (b.Rows != null) foreach (var r in b.Rows) foreach (var c in r) foreach (var x in c) yield return x;
        }
    }

    /// <summary>Jedes Inline-Element abbilden (Absätze, Listen, Tabellen).</summary>
    public RichDoc Map(Func<RichInline, RichInline> f)
        => new(Blocks.Select(b => b with
        {
            Inlines = b.Inlines?.Select(f).ToList(),
            Items = b.Items?.Select(i => (IReadOnlyList<RichInline>)i.Select(f).ToList()).ToList(),
            Rows = b.Rows?.Select(r => (IReadOnlyList<IReadOnlyList<RichInline>>)r.Select(c => (IReadOnlyList<RichInline>)c.Select(f).ToList()).ToList()).ToList()
        }).ToList());

    static string StripMarks(string s) => s.Replace(MarkOpen.ToString(), "").Replace(MarkClose.ToString(), "");

    static int FindRun(string s, string delim, int from)
    {
        int i = from;
        while (i < s.Length)
        {
            int e = s.IndexOf(delim, i, StringComparison.Ordinal);
            if (e < 0) return -1;
            // genau diese Länge (keine längere Backtick-Folge)
            int len = 0; while (e + len < s.Length && s[e + len] == '`') len++;
            if (len == delim.Length && (e == 0 || s[e - 1] != '`')) return e;
            i = e + len;
        }
        return -1;
    }

    /// <summary>Schließer aus genau <paramref name="n"/> Sternen: davor kein Leerraum, danach Ende, Leerraum oder Satzzeichen; nicht in Code, Platzhalter oder Link.</summary>
    static int FindCloser(string s, int from, int n)
    {
        int i = from;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length && IsEscapable(s[i + 1])) { i += 2; continue; }
            if (c == MarkOpen) { int e = s.IndexOf(MarkClose, i + 1); i = e > i ? e + 1 : i + 1; continue; }
            if (c == '`') { int k = 0; while (i + k < s.Length && s[i + k] == '`') k++; int e = FindRun(s, new string('`', k), i + k); i = e > 0 ? e + k : i + k; continue; }
            if (c == '*')
            {
                int k = 0; while (i + k < s.Length && s[i + k] == '*') k++;
                bool closes = !char.IsWhiteSpace(s[i - 1]) && (i + k >= s.Length || IsPunctOrSpace(s[i + k]));
                if (closes && k == n) return i;
                if (closes && k > n && n < 3) return i + (k - n);   // „***a* b**“ – innerer Schließer zuerst
                i += k; continue;
            }
            i++;
        }
        return -1;
    }

    static bool TryLink(string s, int i, out int end, out string label, out string url)
    {
        end = 0; label = url = "";
        int close = -1, depth = 0;
        for (int k = i + 1; k < s.Length; k++)
        {
            if (s[k] == '\\' && k + 1 < s.Length) { k++; continue; }
            if (s[k] == MarkOpen) { int e = s.IndexOf(MarkClose, k + 1); if (e < 0) return false; k = e; continue; }
            if (s[k] == '[') depth++;
            else if (s[k] == ']') { if (depth == 0) { close = k; break; } depth--; }
        }
        if (close < 0 || close + 1 >= s.Length || s[close + 1] != '(') return false;
        int pe = s.IndexOf(')', close + 2);
        if (pe < 0) return false;
        string raw = s[(close + 2)..pe];
        if (!IsAllowedLink(raw, out url)) return false;
        string lab = s[(i + 1)..close];
        if (lab.Trim().Length == 0) return false;
        // Beschriftung: Text ohne Auszeichnung (Maskierungen aufgelöst); Platzhalter bleiben als Text
        label = Unescape(StripMarks(lab));
        end = pe + 1;
        return true;
    }

    static string Unescape(string s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length && IsEscapable(s[i + 1])) { sb.Append(s[i + 1]); i++; continue; }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    /// <summary>Erlaubte Linkziele: http, https, mailto – ohne Leerraum, Anführungszeichen, spitze Klammern oder Platzhalter.</summary>
    public static bool IsAllowedLink(string url, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2000) return false;
        if (url.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '"' or '\'' or '<' or '>' or '{' or '}' or '`' or MarkOpen or MarkClose)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        if (u.Scheme is not ("http" or "https" or "mailto")) return false;
        if (!u.IsWellFormedOriginalString()) return false;
        normalized = url;
        return true;
    }

    /// <summary>Aufeinanderfolgende Textläufe gleichen Stils zusammenfassen.</summary>
    static List<RichInline> Merge(List<RichInline> list)
    {
        var r = new List<RichInline>();
        foreach (var x in list)
        {
            if (r.Count > 0 && x.Kind == RichKind.Text && r[^1].Kind == RichKind.Text && r[^1].Style == x.Style)
                r[^1] = r[^1] with { Text = r[^1].Text + x.Text };
            else if (x.Kind != RichKind.Text || x.Text.Length > 0) r.Add(x);
        }
        return r;
    }

    // ---------------------------------------------------------------------------------
    // Projektion, Maskieren, Serialisieren
    // ---------------------------------------------------------------------------------

    /// <summary>Sichtbarer Text ohne Auszeichnung (Schutzprüfung, KI-Paket, Suche): Umbrüche und Blockgrenzen als <c>\n</c>.</summary>
    public string Projection() => string.Join("\n", Blocks.Select(BlockText));

    static string BlockText(RichBlock b) => b.Kind switch
    {
        RichBlockKind.Paragraph => InlineText(b.Inlines!),
        RichBlockKind.Table => string.Join("\n", b.Rows!.Select(r => string.Join("\t", r.Select(InlineText)))),
        _ => string.Join("\n", b.Items!.Select(InlineText))
    };

    public static string InlineText(IEnumerable<RichInline> inl) => string.Concat(inl.Select(x => x.Text));

    /// <summary>Reiner Text als Auszeichnung, die genau diesen Text darstellt: <c>Parse(Escape(s)).Projection() == s</c> (bis auf Leerzeilen).</summary>
    public static string Escape(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        var lines = plain.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sb = new StringBuilder();
        for (int l = 0; l < lines.Length; l++)
        {
            if (l > 0) sb.Append('\n');
            string line = lines[l];
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c is MarkOpen or MarkClose) continue;
                // „|“ nur am Zeilenanfang (sonst würde die Zeile eine Tabelle) – Texte mit Strichen bleiben gut lesbar
                if (IsEscapable(c) && (c != '|' || i == 0 || line[i - 1] == '\\')) sb.Append('\\');
                else if (i == 0 && (c == '-' || char.IsAsciiDigit(c)) && NeedsLineEscape(line)) sb.Append('\\');
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    static bool NeedsLineEscape(string line) => ListMarker(line, out _, out _, out _);

    /// <summary>Kanonische Auszeichnung des Dokuments (für den Editor); <c>Parse(ToMarkup())</c> ergibt dasselbe Dokument.</summary>
    public string ToMarkup()
    {
        var sb = new StringBuilder();
        for (int b = 0; b < Blocks.Count; b++)
        {
            if (b > 0) sb.Append("\n\n");
            var bl = Blocks[b];
            if (bl.Kind == RichBlockKind.Paragraph) sb.Append(InlineMarkup(bl.Inlines!, true));
            else if (bl.Kind == RichBlockKind.Table)
            {
                int cols = Math.Max(1, bl.Columns);
                for (int r = 0; r < bl.Rows!.Count; r++)
                {
                    if (r > 0) sb.Append('\n');
                    sb.Append('|');
                    for (int c = 0; c < cols; c++)
                        sb.Append(' ').Append(c < bl.Rows[r].Count ? CellMarkup(bl.Rows[r][c]) : "").Append(" |");
                    if (r == 0 && bl.Header) { sb.Append('\n').Append('|'); for (int c = 0; c < cols; c++) sb.Append("---|"); }
                }
            }
            else
                for (int k = 0; k < bl.Items!.Count; k++)
                {
                    if (k > 0) sb.Append('\n');
                    sb.Append(bl.Kind == RichBlockKind.Bullet ? "- " : $"{bl.Start + k}. ");
                    sb.Append(InlineMarkup(bl.Items[k], false).Replace("\n", "\n  "));
                }
        }
        return sb.ToString();
    }

    /// <summary>Zelleninhalt: ohne Umbrüche, „|“ maskiert, nie leer an einer Stelle, die die Zeile beenden würde.</summary>
    static string CellMarkup(IReadOnlyList<RichInline> inl)
        => InlineMarkup(inl.Select(x => x.Kind == RichKind.Break ? new RichInline(RichKind.Text, " ") : x).ToList(), false).Replace("\n", " ");

    /// <summary>Tabelle aus reinen Zelltexten als Auszeichnung (Tabellen-Dialog des Editors).</summary>
    public static string TableMarkup(IReadOnlyList<IReadOnlyList<string>> cells, bool header)
    {
        var rows = cells.Select(r => (IReadOnlyList<IReadOnlyList<RichInline>>)r.Select(c => (IReadOnlyList<RichInline>)new List<RichInline> { new(RichKind.Text, (c ?? "").Replace("\r", "").Replace("\n", " ")) }).ToList()).ToList();
        return new RichDoc(new[] { new RichBlock(RichBlockKind.Table, Rows: rows, Header: header && rows.Count > 1) }).ToMarkup();
    }

    public static string InlineMarkup(IReadOnlyList<RichInline> inl, bool lineStartEscape = true)
    {
        var sb = new StringBuilder();
        bool atLineStart = true;
        for (int idx = 0; idx < inl.Count; idx++)
        {
            var x = inl[idx];
            // Hervorhebung nur, wo die Begrenzer gültig sind (nicht mitten im Wort) – sonst ohne Stil, damit nie Sterne sichtbar werden
            if (x.Kind is RichKind.Text or RichKind.Placeholder && x.Style != RichStyle.None && x.Text.Trim().Length > 0)
            {
                char? prev = sb.Length > 0 ? sb[^1] : null;
                char? next = idx + 1 < inl.Count && inl[idx + 1].Text.Length > 0 ? inl[idx + 1].Text[0] : null;
                if (inl.ElementAtOrDefault(idx + 1) is { Kind: RichKind.Break }) next = null;
                bool ok = (prev == null || IsPunctOrSpace(prev.Value) || prev == '\n') && (next == null || IsPunctOrSpace(next.Value))
                          && !(x.Text.TrimStart().StartsWith('*') || x.Text.TrimEnd().EndsWith('*'));
                if (!ok) x = x with { Style = RichStyle.None };
            }
            string body = x.Kind switch
            {
                RichKind.Text or RichKind.Placeholder => EscapeInline(x.Text, atLineStart && lineStartEscape),
                RichKind.Code => CodeSpan(x.Text),
                RichKind.Key => "[[" + x.Text + "]]",
                RichKind.Ui => "[[ui:" + x.Text + "]]",
                RichKind.Link => "[" + EscapeInline(x.Text, false) + "](" + x.Target + ")",
                RichKind.Ref => "[[ref:" + x.Target + "]]",
                RichKind.Break => "\n",
                _ => x.Text
            };
            if (x.Kind is RichKind.Text or RichKind.Placeholder && x.Style != RichStyle.None && body.Trim().Length > 0)
            {
                // Leerraum außen halten, damit die Begrenzer gültig bleiben
                string lead = body[..(body.Length - body.TrimStart().Length)], trail = body[body.TrimEnd().Length..];
                string core = body.Trim();
                string d = x.Style switch { RichStyle.Bold => "**", RichStyle.Italic => "*", _ => "***" };
                body = lead + d + core + d + trail;
            }
            sb.Append(body);
            atLineStart = x.Kind == RichKind.Break;
        }
        return sb.ToString();
    }

    static string EscapeInline(string s, bool lineStart)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c is MarkOpen or MarkClose) continue;
            if (IsEscapable(c)) sb.Append('\\');
            else if (lineStart && i == 0 && (c == '-' || char.IsAsciiDigit(c)) && ListMarker(s, out _, out _, out _)) sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    static string CodeSpan(string code)
    {
        int longest = 0, run = 0;
        foreach (char c in code) { run = c == '`' ? run + 1 : 0; longest = Math.Max(longest, run); }
        string d = new('`', longest + 1);
        bool pad = code.StartsWith('`') || code.EndsWith('`');
        return d + (pad ? " " + code + " " : code) + d;
    }

    /// <summary>Hat das Dokument Formatierung über reine Absätze hinaus?</summary>
    public bool IsSimple => Blocks.All(b => b.Kind == RichBlockKind.Paragraph && b.Inlines!.All(i => i.Kind is RichKind.Text or RichKind.Break or RichKind.Placeholder && i.Style == RichStyle.None));
}
