using System.Text.Json.Nodes;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Text;

namespace StepRecorder.Tests;

/// <summary>Formatierter Text (13, Paket 21): Grammatik, Maskierung, Hin- und Rückweg.</summary>
public class RichTextTests
{
    static List<RichInline> P(string s) => RichDoc.Parse(s).Blocks.Single().Inlines!.ToList();

    [Fact]
    public void Inline_BoldItalicCodeKeyUiLink()
    {
        var x = P("Klicken Sie **Speichern** und *dann* `notiz.txt`, [[Strg+S]], [[ui:Datei]] – siehe [Hilfe](https://example.org/a?b=1).");
        Assert.Contains(x, i => i.Kind == RichKind.Text && i.Text == "Speichern" && i.Style == RichStyle.Bold);
        Assert.Contains(x, i => i.Kind == RichKind.Text && i.Text == "dann" && i.Style == RichStyle.Italic);
        Assert.Contains(x, i => i.Kind == RichKind.Code && i.Text == "notiz.txt");
        Assert.Contains(x, i => i.Kind == RichKind.Key && i.Text == "Strg+S");
        Assert.Contains(x, i => i.Kind == RichKind.Ui && i.Text == "Datei");
        Assert.Contains(x, i => i.Kind == RichKind.Link && i.Text == "Hilfe" && i.Target == "https://example.org/a?b=1");
    }

    [Theory]
    [InlineData("2*3*4")]                  // nicht im Wort
    [InlineData("a * b * c")]              // Leerraum nach dem Öffner
    [InlineData("**offen")]                // ohne Schließer
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](file:///C:/a)")]
    [InlineData("[x](https://a b)")]
    [InlineData(@"C:\Users\max\*.txt")]
    [InlineData(@"\\server\share")]
    [InlineData("[[a:b]]")]
    public void Literal_WhenNotMarkup(string s)
    {
        var d = RichDoc.Parse(s);
        Assert.DoesNotContain(d.Blocks.SelectMany(b => b.Inlines!), i => i.Kind is RichKind.Link || i.Style != RichStyle.None && s.Contains(' ') == false && s.StartsWith("2"));
        Assert.Equal(s.Replace(@"\*", "*"), d.Projection().Replace(@"\*", "*"));
    }

    [Fact]
    public void Lists_And_Paragraphs()
    {
        var d = RichDoc.Parse("Vorher:\n- erstens\n- zweitens\n  weiter\n\n3. drei\n4. vier\n\nEnde\nZeile 2");
        Assert.Equal(RichBlockKind.Paragraph, d.Blocks[0].Kind);
        Assert.Equal(RichBlockKind.Bullet, d.Blocks[1].Kind);
        Assert.Equal(2, d.Blocks[1].Items!.Count);
        Assert.Contains(d.Blocks[1].Items![1], i => i.Kind == RichKind.Break);
        Assert.Equal(RichBlockKind.Numbered, d.Blocks[2].Kind);
        Assert.Equal(3, d.Blocks[2].Start);
        Assert.Contains(d.Blocks[3].Inlines!, i => i.Kind == RichKind.Break);
    }

    [Theory]
    [InlineData("a *b* c")]
    [InlineData("**fett** und `code` und [[Strg+Umschalt+S]]")]
    [InlineData("- eins\n- zwei")]
    [InlineData("1. a\n2. b")]
    [InlineData("Text mit [Link](https://example.org) und [[ui:OK]]")]
    [InlineData("***beides*** normal")]
    public void Markup_RoundTrips(string s)
    {
        var d = RichDoc.Parse(s);
        var again = RichDoc.Parse(d.ToMarkup());
        Assert.Equal(d.Projection(), again.Projection());
        Assert.Equal(d.ToMarkup(), again.ToMarkup());
    }

    [Fact]
    public void Escape_KeepsAnyTextLiteral()
    {
        var rnd = new Random(7);
        const string alphabet = "ab *`[]\\-1.)(:{}|_xyzÄ\n";
        for (int n = 0; n < 400; n++)
        {
            var chars = Enumerable.Range(0, rnd.Next(1, 40)).Select(_ => alphabet[rnd.Next(alphabet.Length)]).ToArray();
            string s = new string(chars).Replace("\n\n", "\n");
            // bekannte Grenze: eine Zeile, die mit „\“ und einer Listenmarke beginnt („\- x“), verliert den Backslash
            if (s.Split('\n').Any(l => l.StartsWith('\\') && (l.Length > 2 && (l[1] == '-' && l[2] == ' ' || char.IsAsciiDigit(l[1]))))) continue;
            string norm = string.Join("\n", s.Split('\n').Where(l => l.Trim().Length > 0));
            string proj = RichDoc.Parse(RichDoc.Escape(s)).Projection();
            Assert.Equal(string.Join("\n", norm.Split('\n').Select(l => l)), string.Join("\n", proj.Split('\n')));
        }
    }

    [Fact]
    public void PathologicalInput_IsFast()
    {
        string s = string.Concat(Enumerable.Repeat("*[`\\", 25_000));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        RichDoc.Parse(s);
        Assert.True(sw.ElapsedMilliseconds < 3000, sw.ElapsedMilliseconds + " ms");
    }
}

/// <summary>Schutz vor Formatierung (13, Leitplanke H-2): kein geschützter Wert wird durch Auszeichnung sichtbar.</summary>
[Collection("Loc")]
public class RichProtectionTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-richprot-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } try { Directory.Delete(_out, true); } catch { } }
    const string Secret = "mmuster";

    SessionView WithBody(string body, string placeholder = "Benutzer", string by = "user")
    {
        var v = SessionView.Open(_folder, headless: true);
        var st = v.GuideSteps("primary").First(s => s.Kind != "move");
        v.Edits["steps"] = new JsonArray(new JsonObject { ["anchor"] = st.Anchor, ["text"] = new JsonObject { ["de"] = new JsonObject { ["title"] = "T", ["body"] = body, ["fmt"] = 1, ["by"] = by } } });
        v.Edits["substitutions"] = new JsonArray(new JsonObject { ["id"] = "s1", ["match"] = new JsonObject { ["literal"] = Secret }, ["placeholder"] = placeholder });
        v.Rebuild();
        return v;
    }

    string AllExports(SessionView v)
    {
        Directory.CreateDirectory(_out);
        var sb = new System.Text.StringBuilder();
        foreach (var f in new[] { "html", "md", "docx", "pdf", "pptx", "tour", "bugreport" })
        {
            string path = Path.Combine(_out, "x-" + f + "." + (f is "md" or "bugreport" ? "md" : f == "tour" ? "html" : f));
            GuideExport.Export(v, new GuideOptions { Format = f, OutPath = path });
            sb.Append(LegacyOutputTests.Normalize(f, path));
            if (f == "pdf") sb.Append(PdfText(path));
        }
        return sb.ToString();
    }

    static string PdfText(string path) => System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(path));

    [Theory]
    [InlineData("Profil: mm**uster**")]
    [InlineData("Profil: mm`uster`")]
    [InlineData("Profil: [mm](https://a.example)uster")]
    [InlineData("- mm\n  uster")]
    [InlineData("Wert *mmuster* hier")]
    [InlineData("Wert **mmuster** hier")]
    public void SplitSecret_NeverVisible(string body)
    {
        using var v = WithBody(body);
        var st = v.GuideSteps("primary").First(s => s.BodyRich != null);
        Assert.DoesNotContain(Secret, st.AuthorBody!);
        Assert.DoesNotContain(Secret, st.BodyRich!.Plain);
        string all = AllExports(v);
        Assert.DoesNotContain(Secret, all);
        // „uster“ allein ist erlaubt – aber nie direkt an „mm“
        Assert.DoesNotContain("mm</strong>", all + "");
    }

    [Fact]
    public void FormattingKept_WhenValueIsWhole()
    {
        using var v = WithBody("Wert **mmuster** hier");
        var r = v.GuideSteps("primary").First(s => s.BodyRich != null).BodyRich!;
        Assert.False(r.Degraded);
        Assert.Contains(r.Doc.Blocks[0].Inlines!, i => i.Style == RichStyle.Bold && i.Text.Contains("{Benutzer}"));
    }

    [Theory]
    [InlineData("$0")]
    [InlineData("x}[y](https://evil.example){x")]
    public void HostilePlaceholderName_IsRedacted(string name)
    {
        using var v = WithBody("Hallo mmuster!", name);
        var st = v.GuideSteps("primary").First(s => s.BodyRich != null);
        Assert.DoesNotContain(Secret, st.AuthorBody!);
        Assert.DoesNotContain(st.BodyRich!.Doc.Blocks.SelectMany(b => b.Inlines!), i => i.Kind == RichKind.Link);
        Assert.Contains(v.RedactedText, st.AuthorBody!);
    }

    [Fact]
    public void LinkTargetWithSecret_IsDropped()
    {
        using var v = WithBody("[Profil](https://mmuster.example.org/)");
        var st = v.GuideSteps("primary").First(s => s.BodyRich != null);
        Assert.DoesNotContain(st.BodyRich!.Doc.Blocks.SelectMany(b => b.Inlines!), i => i.Kind == RichKind.Link);
        Assert.DoesNotContain(Secret, AllExports(v));
    }

    [Fact]
    public void AgentText_StaysLiteral()
    {
        using var v = WithBody("**fett** [x](https://a.example)", by: "mcp");
        var st = v.GuideSteps("primary").First(s => s.AuthorBody != null);
        Assert.Null(st.BodyRich);
        Assert.Contains("**fett**", st.AuthorBody!);
    }

    [Fact]
    public void KiAndMcp_SeePlainText()
    {
        using var v = WithBody("Bitte **Speichern** drücken");
        var st = v.GuideSteps("primary").First(s => s.BodyRich != null);
        Assert.Equal("Bitte Speichern drücken", st.AuthorBody);
    }
}

/// <summary>Eingabefeld des Editors: Auszeichnung ⇄ FlowDocument verlustfrei (WPF braucht einen STA-Thread).</summary>
public class RichFlowTests
{
    static void Sta(Action a)
    {
        Exception? err = null;
        var t = new Thread(() => { try { a(); } catch (Exception e) { err = e; } });
        t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
        if (err != null) throw new Xunit.Sdk.XunitException(err.ToString());
    }

    [Theory]
    [InlineData("Wählen Sie **Speichern** und *dann* `notiz.txt`.")]
    [InlineData("Mit [[Strg+S]] oder [[ui:Datei]] – siehe [Hilfe](https://example.org/x).")]
    [InlineData("Zeile 1\nZeile 2\n\nNeuer Absatz")]
    [InlineData("- eins\n- **zwei**\n\n3. drei\n4. vier")]
    [InlineData("***beides*** und normal")]
    public void Flow_RoundTrips(string markup)
    {
        Sta(() =>
        {
            var d = RichDoc.Parse(markup);
            var fd = RichFlow.ToFlow(d, RichFlowStyle.Default);
            var back = RichFlow.FromFlow(fd, RichFlowStyle.Default);
            Assert.Equal(d.ToMarkup(), back.ToMarkup());
        });
    }

    [Fact]
    public void BoldMidWord_LosesStyle_NeverShowsStars()
    {
        var d = new RichDoc(new[] { new RichBlock(RichBlockKind.Paragraph, new[] { new RichInline(RichKind.Text, "Spei"), new RichInline(RichKind.Text, "chern", RichStyle.Bold) }) });
        string m = d.ToMarkup();
        Assert.DoesNotContain("*", m);
        Assert.Equal("Speichern", RichDoc.Parse(m).Projection());
    }
}
