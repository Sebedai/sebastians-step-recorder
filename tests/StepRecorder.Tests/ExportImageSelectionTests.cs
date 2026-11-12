using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Bildauswahl beim Export: Vorher-Bilder nur, wo nötig; Ausschnitt und Nachher abwählbar; Nachher-Bild in Anleitungen.</summary>
public class ExportImageSelectionTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-img-" + Guid.NewGuid().ToString("N")[..8]);
    readonly string _folder;
    public ExportImageSelectionTests() { Directory.CreateDirectory(_root); _folder = SyntheticSession.Create(Path.Combine(_root, "s1")); }
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    ExportResult Ki(string name, KiOptions o)
    {
        using var v = SessionView.Open(_folder, headless: true);
        o.OutDir = Path.Combine(_root, name);
        return KiPackage.Export(v, o);
    }

    static string[] Images(ExportResult r, string suffix) => Directory.GetFiles(Path.Combine(r.Path, "images"), "*" + suffix + ".png").Select(Path.GetFileName).ToArray()!;

    [Fact]
    public void Standard_FirstStepKeepsItsBeforeImage()
    {
        var r = Ki("std", new KiOptions());
        // der erste Mausschritt hat kein früheres Bild im Paket – seine Übersicht vorher bleibt
        Assert.Contains("schritt001-vorher.png", Images(r, "-vorher"));
        var pkg = Json.ReadObject(Path.Combine(r.Path, "ai-package.json"))!;
        Assert.Equal("auto", pkg.Obj("imageSelection")!.Str("before"));
    }

    [Fact]
    public void Auto_ReplacesRedundantBeforeImagesByReference()
    {
        var always = Ki("always", new KiOptions { Detail = "full", Before = "always" });
        var auto = Ki("auto", new KiOptions { Detail = "full" });
        int all = Images(always, "-vorher").Length, kept = Images(auto, "-vorher").Length;
        Assert.True(all > 0);
        Assert.True(kept < all, $"auto sollte Vorher-Bilder einsparen ({kept} von {all})");

        // jedes eingesparte Vorher-Bild ist im Protokoll durch einen Verweis auf ein vorhandenes Bild ersetzt
        var trace = File.ReadAllText(Path.Combine(auto.Path, "trace.md"));
        var refs = trace.Split('\n').Where(l => l.StartsWith("- Vorher: ")).Select(l => l["- Vorher: ".Length..].Split(' ')[0]).ToList();
        Assert.Equal(all - kept, refs.Count);
        Assert.All(refs, f => Assert.True(File.Exists(Path.Combine(auto.Path, "images", f)), f));
        Assert.Contains("durch Verweis ersetzt", trace);
        var pkg = Json.ReadObject(Path.Combine(auto.Path, "ai-package.json"))!;
        Assert.Equal(all - kept, (int)pkg.Obj("imageSelection")!.Long("beforeByReference"));
    }

    [Fact]
    public void Never_And_DeselectedKinds_WriteNoSuchImages()
    {
        var never = Ki("never", new KiOptions { Detail = "full", Before = "never" });
        Assert.Empty(Images(never, "-vorher"));
        Assert.NotEmpty(Images(never, "-ausschnitt"));          // Klickpunkt bleibt im Ausschnitt sichtbar

        var bare = Ki("bare", new KiOptions { Detail = "full", Crop = false, After = false });
        Assert.Empty(Images(bare, "-ausschnitt"));
        Assert.Empty(Images(bare, "-nachher"));
        Assert.Contains("ohne Ausschnitte", File.ReadAllText(Path.Combine(bare.Path, "trace.md")));
    }

    [Fact]
    public void Guide_AfterImage_OnlyWhenRequested()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var plain = GuideExport.Export(v, new GuideOptions { Format = "md", OutPath = Path.Combine(_root, "md1", "a.md") });
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "md1", "a-bilder"), "*-nachher.png"));

        var withAfter = GuideExport.Export(v, new GuideOptions { Format = "md", OutPath = Path.Combine(_root, "md2", "a.md"), After = "always" });
        var after = Directory.GetFiles(Path.Combine(_root, "md2", "a-bilder"), "*-nachher.png");
        Assert.NotEmpty(after);
        var md = File.ReadAllText(withAfter.Path);
        Assert.Contains("*Ergebnis:*", md);
        Assert.Contains("](a-bilder/" + Path.GetFileName(after[0]) + ")", md);

        // HTML und PDF vertragen das zweite Bild ebenfalls
        var html = File.ReadAllText(GuideExport.Export(v, new GuideOptions { Format = "html", OutPath = Path.Combine(_root, "h.html"), After = "always" }).Path);
        Assert.Contains("<figcaption>Ergebnis</figcaption>", html);
        Assert.True(new FileInfo(GuideExport.Export(v, new GuideOptions { Format = "pdf", OutPath = Path.Combine(_root, "p.pdf"), After = "always" }).Path).Length > 500);
        Assert.True(new FileInfo(GuideExport.Export(v, new GuideOptions { Format = "docx", OutPath = Path.Combine(_root, "d.docx"), After = "always" }).Path).Length > 500);
    }
}
