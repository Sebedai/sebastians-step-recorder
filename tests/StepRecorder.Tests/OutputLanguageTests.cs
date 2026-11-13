using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;
using StepRecorder.Steps;

namespace StepRecorder.Tests;

/// <summary>Weitere Sprachen der automatischen Schritttexte und Rahmentexte (09 §10): Französisch, Spanisch, Italienisch, Niederländisch.</summary>
public class OutputLanguageTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create(withHotkey: true, withComment: true);
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-lang-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { foreach (var d in new[] { _folder, _out }) try { Directory.Delete(d, true); } catch { } }

    static string TestsDir([CallerFilePath] string p = "") =>
        typeof(OutputLanguageTests).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false).OfType<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "TestsDir")?.Value is { Length: > 0 } dir && Directory.Exists(dir) ? dir : Path.GetDirectoryName(p)!;
    static string RepoRoot => Path.GetFullPath(Path.Combine(TestsDir(), "..", ".."));

    /// <summary>Deutsche Wörter, die in einer Anleitung einer anderen Sprache nicht vorkommen dürfen (aufgezeichnete Namen sind deutsch, enthalten diese aber nicht).</summary>
    static readonly string[] GermanWords = { "Klicken", "Klick ", "Schritt", "Hinweis", "Voraussetzungen", "Über diese Anleitung", "Geben Sie", "Drücken", "Erstellt mit", "Umfang", "Stand", "Inhalt", "Abschluss", "Strg", "Ergebnis", "Wählen Sie", "Fenster ", "Aufnahme" };

    public static IEnumerable<object[]> MoreLanguages() => new[] { "fr", "es", "it", "nl" }.Select(l => new object[] { l });

    [Fact]
    public void Languages_AreSixAndUnknownFallsBackToEnglish()
    {
        Assert.Equal(new[] { "de", "en", "fr", "es", "it", "nl" }, OutText.Languages);
        Assert.Equal(OutText.Languages.Length, OutText.NativeNames.Length);
        Assert.True(OutText.IsSupported("nl"));
        Assert.False(OutText.IsSupported("pt"));
        Assert.Equal("en", OutText.Normalize("pt"));
        Assert.Equal("fr", OutText.Normalize(" FR "));
        Assert.Equal("Contents", OutText.T("pt", "Inhalt", "Contents"));
        Assert.Equal("Inhalt", OutText.T("de", "Inhalt", "Contents"));
        Assert.Equal("Sommaire", OutText.T("fr", "Inhalt", "Contents"));
        var step = Click("Button", "Save");
        Assert.Equal(TextTemplates.Body(step, "en"), TextTemplates.Body(step, "pt"));
        Assert.Equal(TextTemplates.Title(step, "en"), TextTemplates.Title(step, "pt"));
    }

    [Fact]
    public void FrameTable_EveryKeyHasAllSixLanguages()
    {
        foreach (var (en, t) in OutText.Frame)
        {
            Assert.False(string.IsNullOrWhiteSpace(en));
            foreach (var (lang, text) in new[] { ("de", t.de), ("fr", t.fr), ("es", t.es), ("it", t.it), ("nl", t.nl) })
                Assert.False(string.IsNullOrWhiteSpace(text), $"„{en}“ ohne {lang}");
            // Leerzeichen am Rand bleiben in allen Sprachen erhalten (Text wird angehängt)
            foreach (var text in new[] { t.de, t.fr, t.es, t.it, t.nl })
            {
                Assert.True(en.StartsWith(' ') == text.StartsWith(' '), $"Leerzeichen vorn: „{en}“ / „{text}“");
                Assert.True(en.EndsWith(' ') == text.EndsWith(' '), $"Leerzeichen hinten: „{en}“ / „{text}“");
            }
        }
        foreach (var (ct, w) in TextTemplates.TypeWordsMore)
            foreach (var text in new[] { w.fr, w.es, w.it, w.nl })
                Assert.False(string.IsNullOrWhiteSpace(text), ct);
    }

    [Fact]
    public void GuideFrameTexts_AllInTable()
    {
        // jeder Rahmentext d.L("deutsch", "english") der Anleitungs-Exporte hat einen Eintrag mit demselben deutschen Text
        var rx = new Regex(@"\bd\.L\(\s*""((?:[^""\\]|\\.)*)""\s*,\s*""((?:[^""\\]|\\.)*)""\s*\)");
        var missing = new List<string>();
        int found = 0;
        foreach (var f in Directory.EnumerateFiles(Path.Combine(RepoRoot, "src", "StepRecorder.Core", "Export"), "GuideExport*.cs"))
            foreach (Match m in rx.Matches(File.ReadAllText(f)))
            {
                found++;
                string de = Regex.Unescape(m.Groups[1].Value), en = Regex.Unescape(m.Groups[2].Value);
                if (!OutText.Frame.TryGetValue(en, out var t)) missing.Add($"{Path.GetFileName(f)}: „{en}“ fehlt");
                else if (t.de != de) missing.Add($"{Path.GetFileName(f)}: „{en}“ – deutsch „{t.de}“ statt „{de}“");
            }
        Assert.True(found > 30, "Scanner findet keine Rahmentexte");
        Assert.True(missing.Count == 0, string.Join("\n", missing));
    }

    static JsonObject Click(string ct, string name, string kind = "click") => new()
    {
        ["kind"] = kind, ["input"] = new JsonObject(),
        ["target"] = new JsonObject { ["status"] = "confirmed", ["el"] = new JsonObject { ["ct"] = ct, ["name"] = name } },
        ["ctx"] = new JsonObject { ["title"] = "Editor" }
    };

    [Fact]
    public void Templates_NaturalSentencesPerLanguage()
    {
        Assert.Equal("Cliquez sur le bouton « Enregistrer ».", TextTemplates.Body(Click("Button", "Enregistrer"), "fr"));
        Assert.Equal("Haga clic en el botón «Guardar».", TextTemplates.Body(Click("Button", "Guardar"), "es"));
        Assert.Equal("Fare clic sul pulsante «Salva».", TextTemplates.Body(Click("Button", "Salva"), "it"));
        Assert.Equal("Klik op de knop ‘Opslaan’.", TextTemplates.Body(Click("Button", "Opslaan"), "nl"));
        // Präposition + Artikel: Italienisch „nel“, „alla“; Französisch „à l'“; Spanisch „a la“
        Assert.Equal("Fare clic nel campo «Nome».", TextTemplates.Body(Click("Edit", "Nome"), "it"));
        Assert.Equal("Passare alla scheda «Generale».", TextTemplates.Body(Click("TabItem", "Generale"), "it"));
        Assert.Equal("Passez à l'onglet « Général ».", TextTemplates.Body(Click("TabItem", "Général"), "fr"));
        Assert.Equal("Cambie a la pestaña «General».", TextTemplates.Body(Click("TabItem", "General"), "es"));
        Assert.Equal("Fare clic nell'area di testo «Testo».", TextTemplates.Body(Click("Document", "Testo"), "it"));
        Assert.Equal("Fare doppio clic sulla voce «Documento».", TextTemplates.Body(Click("ListItem", "Documento", "doubleClick"), "it"));
        Assert.StartsWith("Glissement du bouton « A » vers l'emplacement (5, 6) (bouton gauche, maintenu ", TextTemplates.Body(new JsonObject
        {
            ["kind"] = "drag", ["input"] = new JsonObject(), ["endPosition"] = new JsonObject { ["x"] = 5, ["y"] = 6 },
            ["target"] = new JsonObject { ["status"] = "confirmed", ["el"] = new JsonObject { ["ct"] = "Button", ["name"] = "A" } },
        }, "fr", "protocol"));
        // Titel in der Grundform
        Assert.Equal("Cliquer sur « OK »", TextTemplates.Title(Click("Button", "OK"), "fr"));
        Assert.Equal("Op ‘OK’ klikken", TextTemplates.Title(Click("Button", "OK"), "nl"));
        // Tastenkombination: Strg heißt überall „Ctrl“, Umschalt je Sprache
        var hk = new JsonObject { ["kind"] = "hotkey", ["input"] = new JsonObject { ["combo"] = "Ctrl+Shift+S" } };
        Assert.Equal("Appuyez sur Ctrl+Maj+S.", TextTemplates.Body(hk, "fr"));
        Assert.Equal("Pulse Ctrl+Mayús+S.", TextTemplates.Body(hk, "es"));
        Assert.Equal("Premere Ctrl+Maiusc+S.", TextTemplates.Body(hk, "it"));
        Assert.Equal("Druk op Ctrl+Shift+S.", TextTemplates.Body(hk, "nl"));
        Assert.Equal("Drücken Sie Strg+Umschalt+S.", TextTemplates.Body(hk, "de"));
        Assert.Equal("Press Ctrl+Shift+S.", TextTemplates.Body(hk, "en"));
        // Zahlen mit Komma
        Assert.Equal("1,5 s", TextTemplates.Duration(1_500_000, "fr"));
        Assert.Equal("1.5 s", TextTemplates.Duration(1_500_000, "en"));
    }

    [Theory]
    [MemberData(nameof(MoreLanguages))]
    public void GuideExport_MarkdownAndHtml_Localized_NoGermanLeaks(string lang)
    {
        using var v = SessionView.Open(_folder, headless: true);
        var doc = new DocSettings { Prerequisites = "Notepad", Author = "Sebastian", Version = "1.0" };
        v.Edits["guide"] = new JsonObject { ["intro"] = "Notepad", ["outro"] = "OK" };
        var md = File.ReadAllText(GuideExport.Export(v, new GuideOptions { Format = "md", Language = lang, Doc = doc, Importance = "all", OutPath = Path.Combine(_out, lang, "g.md") }).Path!);
        var html = File.ReadAllText(GuideExport.Export(v, new GuideOptions { Format = "html", Language = lang, Doc = doc, Importance = "all", OutPath = Path.Combine(_out, lang, "g.html") }).Path!);
        html = System.Net.WebUtility.HtmlDecode(html);
        string prereq = OutText.T(lang, "Voraussetzungen", "Prerequisites"), about = OutText.T(lang, "Über diese Anleitung", "About this guide");
        Assert.NotEqual("Prerequisites", prereq);
        foreach (var text in new[] { md, html })
        {
            Assert.Contains(prereq, text);
            Assert.Contains(about, text);
            Assert.Contains(OutText.T(lang, "Abschluss", "Finally"), text);
        }
        Assert.Contains($"<html lang=\"{lang}\">", html);
        Assert.Contains(OutText.T(lang, "Erstellt mit", "Created with"), html);
        // automatische Schritttexte in der Exportsprache
        Assert.Contains(OutText.For(lang, "", "", "Choisissez la commande", "Elija la opción de menú", "Scegliere la voce di menu", "Kies het menu-item"), md);
        Assert.Contains(OutText.For(lang, "", "", "Saisissez", "Escriba", "Digitare", "Typ "), md);
        Assert.Contains("`Ctrl+1`", md);
        string press = OutText.For(lang, "", "", "Appuyez sur", "Pulse", "Premere", "Druk op");
        Assert.Contains(press, md);
        foreach (var w in GermanWords)
        {
            Assert.False(md.Contains(w, StringComparison.Ordinal), $"{lang}: „{w}“ im Markdown:\n{md}");
            Assert.False(Regex.Replace(html, "src=\"data:[^\"]*\"", "").Contains(w, StringComparison.Ordinal), $"{lang}: „{w}“ im HTML");
        }
        Assert.DoesNotContain("“", md);   // keine englischen Anführungszeichen
    }

    [Fact]
    public void GuideExport_GermanAndEnglish_Unchanged()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var de = File.ReadAllText(GuideExport.Export(v, new GuideOptions { Format = "md", Language = "de", OutPath = Path.Combine(_out, "de", "g.md") }).Path!);
        var en = File.ReadAllText(GuideExport.Export(v, new GuideOptions { Format = "md", Language = "en", OutPath = Path.Combine(_out, "en", "g.md") }).Path!);
        Assert.Contains("Wählen Sie den Menüpunkt **„Datei“**.", de);
        Assert.Contains("und drücken Sie die Eingabetaste.", de);
        Assert.Contains("Umfang: **6 Schritte**", de);
        Assert.Contains("Length: **6 steps**", en);
        Assert.Contains("Press `Ctrl+1`.", en);
        Assert.DoesNotContain("Wählen Sie", en);
    }

    [Theory]
    [MemberData(nameof(MoreLanguages))]
    public void OtherFormats_UseLanguageTag(string lang)
    {
        using var v = SessionView.Open(_folder, headless: true);
        var tour = System.Net.WebUtility.HtmlDecode(File.ReadAllText(GuideExport.Export(v, new GuideOptions { Format = "tour", Language = lang, OutPath = Path.Combine(_out, lang, "t.html") }).Path!));
        Assert.Contains(OutText.T(lang, "Tour starten →", "Start tour →"), tour);
        Assert.DoesNotContain(" von ", tour);
        string docx = GuideExport.Export(v, new GuideOptions { Format = "docx", Language = lang, OutPath = Path.Combine(_out, lang, "g.docx") }).Path!;
        using var zip = System.IO.Compression.ZipFile.OpenRead(docx);
        using var sr = new StreamReader(zip.GetEntry("word/styles.xml")!.Open());
        Assert.Contains(OutText.Tag(lang), sr.ReadToEnd());
    }

    [Fact]
    public void ExportRequest_AcceptsNewLanguages()
    {
        var r = ExportRequest.FromJson(new ExportRequest { Format = "html", Language = "it" }.ToJson());
        Assert.Equal("it", r.Language);
        using var v = SessionView.Open(_folder, headless: true);
        Assert.Contains(" – lista di controllo", Exporter.DefaultTarget(v, "html", null, "checklist", "it"));
    }
}
