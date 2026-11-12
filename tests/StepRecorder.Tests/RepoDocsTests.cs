using System.Text.RegularExpressions;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Paket 18 des Entwickler-Fahrplans: Markdown nach 09 §6 für Repositories, Kopieren als Markdown, GIF-Größenziel.</summary>
public class RepoDocsTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-md-" + Guid.NewGuid().ToString("N")[..8]);
    public RepoDocsTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    static SessionView Open(string folder) => SessionView.Open(folder, headless: true, lang: "de");

    /// <summary>Hinweis des Autors am ersten Schritt (wie im Editor über „Kommentar“).</summary>
    static string WithNote(string folder, string kind = "tip")
    {
        using var v = SessionView.Open(folder, lang: "de");
        var s = v.GuideSteps("primary").First();
        v.Edits["comments"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["id"] = "n1", ["anchor"] = s.Anchor, ["kind"] = kind, ["text"] = "Vorher speichern.", ["atUtc"] = "2026-11-11T14:00:00Z" });
        v.SaveEdits("test");
        return folder;
    }

    [Fact]
    public void Markdown_FollowsSpec_AsciiNames_ListImagesInOwnFolder()
    {
        var folder = WithNote(SyntheticSession.Create(Path.Combine(_root, "s"), withHotkey: true));
        using var v = Open(folder);
        var r = Exporter.Run(v, new ExportRequest { Format = "md", Target = Path.Combine(_root, "out") + "\\" });
        Assert.Equal("Datei-im-Editor-speichern.md", Path.GetFileName(r.Path));
        string md = File.ReadAllText(r.Path!);
        string dir = Path.GetDirectoryName(r.Path)!;
        // EXP-043: nur ASCII-Buchstaben, Ziffern, Bindestriche und Punkt
        foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(dir, f)))
            Assert.Matches(@"^[A-Za-z0-9\-]+(\\[A-Za-z0-9\-]+)*\.[a-z]+$", f);
        // EXP-040: jeder Bildverweis löst auf, Ordner <name>-bilder
        var refs = Regex.Matches(md, @"!\[[^\]]*\]\(([^)]+)\)").Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(refs);
        Assert.All(refs, x => { Assert.StartsWith("Datei-im-Editor-speichern-bilder/schritt-", x); Assert.True(File.Exists(Path.Combine(dir, x.Replace('/', '\\'))), x); });
        // EXP-041: kein rohes HTML, keine Verweise außer Bildern
        Assert.DoesNotMatch(@"(?<!\\)<[A-Za-z/!]", md);
        Assert.DoesNotMatch(@"(?<!!)\[[^\]]*\]\(", md);
        // 09 §6.2: nummerierte Liste, Bild eingerückt, Tastenkürzel als Code, Hinweis als Zitat
        Assert.Matches(@"(?m)^1\. \*\*", md);
        Assert.DoesNotMatch(@"(?m)^\d+\. \*\*[^\n]*\*\*„", md);                 // kein Fett im fetten Titel
        Assert.Matches(@"(?m)^   !\[", md);
        Assert.Matches(@"`(Strg|Ctrl)\+1`", md);
        Assert.Matches(@"(?m)^   > \*\*(Hinweis|Tipp|Achtung|Wichtig):\*\* ", md);
        // 09 §6.1: UTF-8 ohne BOM, LF, ein abschließender Zeilenumbruch
        var bytes = File.ReadAllBytes(r.Path!);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.DoesNotContain("\r", md);
        Assert.EndsWith("\n", md); Assert.False(md.EndsWith("\n\n"));
    }

    [Fact]
    public void Markdown_IsByteIdentical_OnReexport()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "d"));
        using var v = Open(folder);
        var a = Exporter.Run(v, new ExportRequest { Format = "md", Target = Path.Combine(_root, "a") + "\\" });
        var b = Exporter.Run(v, new ExportRequest { Format = "md", Target = Path.Combine(_root, "b") + "\\" });
        Assert.Equal(File.ReadAllBytes(a.Path!), File.ReadAllBytes(b.Path!));
        string ia = Path.Combine(Path.GetDirectoryName(a.Path)!, "Datei-im-Editor-speichern-bilder"), ib = Path.Combine(Path.GetDirectoryName(b.Path)!, "Datei-im-Editor-speichern-bilder");
        var fa = Directory.GetFiles(ia).Select(Path.GetFileName).OrderBy(x => x).ToList();
        Assert.Equal(fa, Directory.GetFiles(ib).Select(Path.GetFileName).OrderBy(x => x).ToList());
        foreach (var f in fa) Assert.Equal(File.ReadAllBytes(Path.Combine(ia, f!)), File.ReadAllBytes(Path.Combine(ib, f!)));
    }

    [Fact]
    public void Markdown_Dialects_AndFrontMatter()
    {
        var folder = WithNote(SyntheticSession.Create(Path.Combine(_root, "c")));
        using var v = Open(folder);
        string Md(string dialect, bool fm = false) => File.ReadAllText(Exporter.Run(v, new ExportRequest { Format = "md", MdDialect = dialect, MdFrontMatter = fm, Target = Path.Combine(_root, dialect + fm) + "\\" }).Path!);
        Assert.Matches(@"(?m)^   > \[!(NOTE|TIP|WARNING|IMPORTANT)\]$", Md("github"));
        Assert.Matches(@"(?m)^   > \[!(note|tip|warning|important|success)\] \S", Md("obsidian"));
        string fm = Md("plain", true);
        Assert.StartsWith("---\ntitle: \"Datei im Editor speichern\"\n", fm);
        Assert.Contains("\ndate: 2026-11-11\n", fm);
    }

    [Fact]
    public void Markdown_TerminalCommand_AsConsoleBlock_AfterReview()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "t"), withTerminal: true);
        using (var raw = Open(folder))
        {
            // kopflos ungeprüft: Platzhalter statt Befehl, kein Codeblock
            string md0 = File.ReadAllText(Exporter.Run(raw, new ExportRequest { Format = "md", Target = Path.Combine(_root, "t0") + "\\" }).Path!);
            Assert.DoesNotContain(SyntheticSession.TerminalCanary, md0);
        }
        // nach der Durchsicht („beibehalten“) erscheint der Befehl als Codeblock
        using (var ed = SessionView.Open(folder, lang: "de"))
        {
            var step = ed.All.First(s => s.FlagReason != null);
            var inputs = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["kind"] = "typed", ["anchor"] = step.Anchor, ["seqRange"] = new System.Text.Json.Nodes.JsonArray(step.Anchor, step.Anchor), ["status"] = "kept", ["flagged"] = new System.Text.Json.Nodes.JsonArray("plaintextWindow"), ["atUtc"] = "2026-11-11T14:00:00Z" });
            ed.Edits["review"] = new System.Text.Json.Nodes.JsonObject { ["inputs"] = inputs, ["confirmed"] = new System.Text.Json.Nodes.JsonObject { ["atUtc"] = "2026-11-11T14:00:00Z", ["throughSeq"] = ed.Session.Events.Count, ["patternsVersion"] = Patterns.Version } };
            ed.SaveEdits("test");
        }
        using var v = Open(folder);
        string md = File.ReadAllText(Exporter.Run(v, new ExportRequest { Format = "md", Target = Path.Combine(_root, "t1") + "\\" }).Path!);
        Assert.Matches(@"(?m)^ {3,4}```console\n {3,4}" + Regex.Escape(SyntheticSession.TerminalCanary) + @"\n {3,4}```$", md);
    }

    [Fact]
    public void MdText_EscapesOnlyWhatIsNeeded()
    {
        Assert.Equal("Datei.txt öffnen (ca. 5 min) - fertig!", GuideExport.MdText("Datei.txt öffnen (ca. 5 min) - fertig!"));
        Assert.Equal("1\\. Schritt", GuideExport.MdText("1. Schritt"));
        Assert.Equal("\\# kein Titel", GuideExport.MdText("# kein Titel"));
        Assert.Equal("a\\*b\\_c \\[x\\] \\<div> \\`code\\` \\&amp; & ok", GuideExport.MdText("a*b_c [x] <div> `code` &amp; & ok"));
        Assert.Equal("Umlaut-Aerger-Strasse-Cafe", Fmt.AsciiSlug("Umlaut Ärger: Straße / Café"));
        Assert.Equal("anleitung", Fmt.AsciiSlug("„“"));
    }

    [Fact]
    public void StepMarkdown_HasNoDanglingImageReference_AndClipboardCoversAllSteps()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "k"), withHotkey: true);
        using var v = Open(folder);
        var s = v.GuideSteps("primary").First();
        string one = GuideExport.StepMarkdown(v, s);
        Assert.StartsWith(s.Number + ". **", one);
        Assert.DoesNotContain("![", one);
        string all = GuideExport.MarkdownForClipboard(v, "de");
        Assert.DoesNotContain("![", all);
        Assert.Equal(v.GuideSteps("primary").Count(x => x.Kind != "move"), Regex.Matches(all, @"(?m)^\d+\. \*\*").Count);
    }

    [Fact]
    public void Gif_SizeTarget_ShrinksWidthThenSteps()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "g"));
        using var v = Open(folder);
        var free = Exporter.Run(v, new ExportRequest { Format = "gif", Target = Path.Combine(_root, "g0") + "\\" });
        long full = new FileInfo(free.Path!).Length;
        var small = Exporter.Run(v, new ExportRequest { Format = "gif", GifMaxMb = Math.Max(0.5, full / 1024.0 / 1024.0 / 2), Target = Path.Combine(_root, "g1") + "\\" });
        if (full > 1024 * 1024)   // nur sinnvoll, wenn das GIF das kleinste Ziel überhaupt überschreitet
        {
            Assert.True(new FileInfo(small.Path!).Length < full);
            Assert.Contains(small.Notices, n => n.Contains("GIF verkleinert") || n.Contains("GIF reduced"));
        }
        var req = ExportRequest.FromJson(new ExportRequest { Format = "gif", GifMaxMb = 10, MdDialect = "github", MdFrontMatter = true }.ToJson());
        Assert.Equal(10, req.GifMaxMb); Assert.Equal("github", req.MdDialect); Assert.True(req.MdFrontMatter);
    }
}
