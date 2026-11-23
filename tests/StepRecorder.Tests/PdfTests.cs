using System.Text;
using System.Text.RegularExpressions;
using StepRecorder.Export;
using StepRecorder.Export.Formats;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>PDF: gültige Querverweistabelle, Verweise und Lesezeichen, WinAnsi ohne Einbettung, Unicode mit Teilmengen-Schrift.</summary>
[Collection("Loc")]
public class PdfTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-pdf-" + Guid.NewGuid().ToString("N")[..8]);
    public PdfTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    /// <summary>Jeder xref-Eintrag zeigt auf „n 0 obj“; startxref zeigt auf „xref“.</summary>
    static void AssertXref(byte[] pdf)
    {
        string s = Encoding.Latin1.GetString(pdf);
        var m = Regex.Match(s, @"startxref\n(\d+)\n%%EOF\n$");
        Assert.True(m.Success, "startxref fehlt");
        int xref = int.Parse(m.Groups[1].Value);
        Assert.StartsWith("xref\n0 ", s[xref..]);
        var head = Regex.Match(s[xref..], @"^xref\n0 (\d+)\n");
        int count = int.Parse(head.Groups[1].Value);
        int pos = xref + head.Length + 20;                       // Eintrag 0 überspringen
        for (int n = 1; n < count; n++, pos += 20)
        {
            long off = long.Parse(s.Substring(pos, 10));
            Assert.True(off > 0, $"Objekt {n} ohne Lage");
            Assert.StartsWith($"{n} 0 obj", s[(int)off..]);
        }
    }

    [Fact]
    public void WinAnsiText_NeedsNoEmbeddedFont()
    {
        var p = new SimplePdf { Title = "Größe" };
        p.Paragraph("„Größe“ – 5 € · Übersicht");
        var b = p.ToBytes();
        AssertXref(b);
        string s = Encoding.Latin1.GetString(b);
        Assert.DoesNotContain("/FontFile2", s);
        Assert.Contains("/Title <FEFF", s);                      // Metadaten als UTF-16
        Assert.Empty(p.Warnings);
    }

    [Fact]
    public void Unicode_EmbedsSubset_WithToUnicode()
    {
        if (TrueTypeFont.TryLoadSystem("segoeui.ttf", "SegoeUI") == null && TrueTypeFont.TryLoadSystem("arial.ttf", "ArialMT") == null) return;   // ohne Systemschrift nicht prüfbar
        var p = new SimplePdf();
        p.Paragraph("Привет мир → ✓");
        var b = p.ToBytes();
        AssertXref(b);
        string s = Encoding.Latin1.GetString(b);
        Assert.Contains("/FontFile2", s);
        Assert.Contains("/Subtype /Type0", s);
        Assert.Contains("/Encoding /Identity-H", s);
        // ToUnicode ist unkomprimiert: Kyrillisch lässt sich zurücklesen
        Assert.Contains("<041F>", s);  // П
        Assert.Contains("<0440>", s);  // р
        Assert.Empty(p.Warnings);
    }

    [Fact]
    public void UnsupportedScripts_BecomeQuestionMarks_WithOneWarning()
    {
        var p = new SimplePdf { Language = "de-DE" };
        p.Paragraph("你好 世界");
        p.Paragraph("שלום");
        var b = p.ToBytes();
        AssertXref(b);
        Assert.Single(p.Warnings);
        Assert.Contains("(\\?", Encoding.Latin1.GetString(b).Replace("(?", "(\\?"));
    }

    [Fact]
    public void Subset_KeepsGlyphNumbersAndWidths()
    {
        var f = TrueTypeFont.TryLoadSystem("segoeui.ttf", "SegoeUI") ?? TrueTypeFont.TryLoadSystem("arial.ttf", "ArialMT");
        if (f == null) return;
        int g = f.GlyphFor('П');
        Assert.True(g > 0);
        var sub = new TrueTypeFont(f.Subset(new[] { g }), "Sub");
        Assert.Equal(f.NumGlyphs, sub.NumGlyphs);
        Assert.Equal(f.Advance(g), sub.Advance(g));
        Assert.True(f.Subset(new[] { g }).Length < 200_000);
    }

    [Fact]
    public void LinksAndBookmarks_AreWritten()
    {
        var p = new SimplePdf();
        p.Paragraph("Inhalt");
        p.LinkTo(50, 700, 200, 14, "k1");
        p.LinkTo(50, 680, 200, 14, "fehlt");                    // Ziel ohne Sprungmarke wird übergangen
        p.NewPage();
        p.Dest("k1"); p.Bookmark("1  Kapitel Ü", 0, "k1");
        p.Paragraph("Kapitel");
        p.Dest("s1"); p.Bookmark("1.1  Schritt", 1, "s1");
        var b = p.ToBytes();
        AssertXref(b);
        string s = Encoding.Latin1.GetString(b);
        Assert.Contains("/Type /Outlines", s);
        Assert.Contains("/PageMode /UseOutlines", s);
        Assert.Equal(1, Regex.Matches(s, "/Subtype /Link").Count);
        Assert.Contains("/Count -1", s);                         // Kapitel mit einem (eingeklappten) Schritt
        Assert.Contains("/Lang (de-DE)", s);
    }

    /// <summary>Fett misst mit der Breitentabelle von Helvetica-Bold (kein Pauschalfaktor).</summary>
    [Fact]
    public void BoldWidth_IsSumOfHelveticaBoldTable()
    {
        // n o t i z . t x t = 611+611+333+278+500+278+333+556+333 = 3833/1000 em
        Assert.Equal(38.33, SimplePdf.TextWidth("notiz.txt", 10, true), 6);
        Assert.Equal(34.46, SimplePdf.TextWidth("notiz.txt", 10), 6);                          // normal: 3446/1000 em
        // akzentuierte Buchstaben wie der Grundbuchstabe, Sonderzeichen mit Fett-Breite
        Assert.Equal(SimplePdf.TextWidth("aouAOUeEn", 10, true), SimplePdf.TextWidth("äöüÄÖÜéÉñ", 10, true), 6);
        Assert.Equal((611 + 500 + 500 + 278 + 1000) / 100.0, SimplePdf.TextWidth("ß„“’—", 10, true), 6);
    }

    /// <summary>Gegenprobe: Arial bzw. Arial Bold sind metrisch gleich zu Helvetica bzw. Helvetica-Bold (höchstens 1 Einheit je Zeichen).</summary>
    [Theory]
    [InlineData("arial.ttf", false)]
    [InlineData("arialbd.ttf", true)]
    public void HelveticaTables_MatchArial(string file, bool bold)
    {
        var f = TrueTypeFont.TryLoadSystem(file, "Arial");
        if (f == null) return;   // ohne Systemschrift nicht prüfbar
        var off = new List<string>();
        string special = "ßÄÖÜäöüéÉ€–—…„“”‘’‚•°§©®×";   // ohne „·“: Arial 333, Helvetica laut AFM 278
        foreach (var c in Enumerable.Range(32, 95).Select(i => (char)i).Concat(special))
        {
            double table = SimplePdf.TextWidth(c.ToString(), 1000, bold), ttf = f.Width1000(f.GlyphFor(c));
            if (Math.Abs(table - ttf) > 1) off.Add($"'{c}' {table} statt {ttf:0.#}");
        }
        Assert.True(off.Count == 0, string.Join(", ", off));
    }

    /// <summary>Rückfall: „Geben Sie **notiz.txt** ein“ – das Wort nach dem fetten Wort beginnt erst hinter dessen echter Breite plus Leerzeichen.</summary>
    [Fact]
    public void RichParagraph_WordAfterBold_StartsAfterBoldWidthAndSpace()
    {
        var p = new SimplePdf();
        p.RichParagraph(StepRecorder.Text.RichDoc.ParseInline("Geben Sie **notiz.txt** ein"), 10);
        string s = Encoding.Latin1.GetString(p.ToBytes());
        double X(string font, string text)
        {
            var m = Regex.Match(s, @"BT [\d. ]+ rg ([\d.]+) [\d.]+ Td /" + font + @" 10 Tf \(" + Regex.Escape(text) + @"\) Tj");
            Assert.True(m.Success, text + " nicht gefunden");
            return double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
        double bold = X("F2", "notiz.txt"), next = X("F1", "ein");
        // echte Breite in Helvetica-Bold: 3833/1000 em, Leerzeichen 278/1000 em
        Assert.True(Math.Abs(next - (bold + 38.33 + 2.78)) < 1e-6, $"„ein“ bei {next}, erwartet {bold + 38.33 + 2.78}");
    }

    [Fact]
    public void GuidePdf_IsValid_AndHasBookmarks()
    {
        var a = SyntheticSession.Create(Path.Combine(_root, "a"));
        using var v = SessionView.Open(a);
        var r = GuideExport.Export(v, new GuideOptions { Format = "pdf", OutPath = Path.Combine(_root, "a.pdf") });
        var b = File.ReadAllBytes(r.Path);
        AssertXref(b);
        Assert.Contains("/Type /Outlines", Encoding.Latin1.GetString(b));
    }
}
