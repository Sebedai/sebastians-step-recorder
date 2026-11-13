using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.Tests;

/// <summary>Lesezeichen (Editor-Metadaten), Textbausteine (Einstellungen und Austausch), Namen für Bildschirmleser (Quelltext-Prüfung).</summary>
[Collection("Loc")]
public partial class BookmarkSnippetTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-bm-" + Guid.NewGuid().ToString("N")[..8]);
    public BookmarkSnippetTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    // ---------------- Lesezeichen ----------------

    [Fact]
    public void Bookmarks_AreRead_ButAppearInNoOutput()
    {
        var a = SyntheticSession.Create(Path.Combine(_root, "a"));
        long anchor;
        using (var v = SessionView.Open(a, headless: true))
        {
            var click = v.All.First(s => s.Kind == "click");
            anchor = click.Anchor;
            Assert.False(click.Bookmarked);
            v.Edits["inserted"] = new JsonArray(new JsonObject { ["anchor"] = -1001, ["after"] = anchor, ["title"] = "Eigener Schritt" });
            v.Edits["steps"] = new JsonArray(new JsonObject { ["anchor"] = anchor, ["bookmark"] = true }, new JsonObject { ["anchor"] = -1001, ["bookmark"] = true });
            Assert.True(v.SaveEdits());
            Assert.True(v.All.First(s => s.Anchor == anchor).Bookmarked);
            Assert.True(v.Manual.Single().Bookmarked);
            Assert.Equal(1, v.All.Count(s => s.Bookmarked));
        }
        using (var v = SessionView.Open(a, headless: true))
        {
            // die Anleitung ist mit und ohne Lesezeichen gleich
            string with = GuideExport.MarkdownForClipboard(v, "de");
            foreach (var se in v.Edits["steps"]!.AsArray().OfType<JsonObject>()) se.Remove("bookmark");
            v.Rebuild();
            Assert.DoesNotContain(v.Manual, s => s.Bookmarked);
            Assert.Equal(with, GuideExport.MarkdownForClipboard(v, "de"));
        }
        // bereinigte Kopie: Lesezeichen sind Arbeitsnotizen und fallen weg
        var copy = SessionSanitizer.CreateCopy(a, Path.Combine(_root, "kopie"));
        Assert.DoesNotContain("bookmark", File.ReadAllText(Path.Combine(copy, "edits.json")));
    }

    // ---------------- Textbausteine ----------------

    [Fact]
    public void Snippets_CleanAndInsert()
    {
        var list = TextSnippet.Clean(new[]
        {
            new TextSnippet { Name = " Neustart ", Text = "Bitte das Programm neu starten." },
            new TextSnippet { Name = "", Text = "ohne Namen" },
            new TextSnippet { Name = "Leer", Text = "  " },
            new TextSnippet { Name = "neustart", Text = "Zweite Fassung" },
            null,
        });
        Assert.Single(list);
        Assert.Equal("neustart", list[0].Name);
        Assert.Equal("Zweite Fassung", list[0].Text);
        Assert.False(TextSnippet.Valid("zwei\nZeilen", "Text"));
        Assert.False(TextSnippet.Valid(new string('x', TextSnippet.MaxName + 1), "Text"));

        Assert.Equal(("Hallo Welt!", 10), TextSnippet.Insert("Hallo !", 6, 0, "Welt"));
        Assert.Equal(("A-B", 2), TextSnippet.Insert("AxB", 1, 1, "-"));
        Assert.Equal(("abcX", 4), TextSnippet.Insert("abc", 99, 5, "X"));
    }

    [Fact]
    public void Snippets_RoundTripThroughSettingsFile()
    {
        var path = Path.Combine(_root, "settings.json");
        var s = new AppSettings { TextSnippets = { new TextSnippet { Name = "Tipp", Text = "Strg+S speichert." } } };
        Assert.True(s.Write(path));
        Assert.Contains("textSnippets", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
        var r = AppSettings.Read(path);
        Assert.Equal("Tipp", Assert.Single(r.TextSnippets).Name);
    }

    [Fact]
    public void Snippets_ExchangePart_MergesAndNeverWeakens()
    {
        Assert.Contains("textSnippets", SettingsExchange.Parts);
        var source = new AppSettings { TextSnippets = { new TextSnippet { Name = "Neustart", Text = "Neu" }, new TextSnippet { Name = "Achtung", Text = "Vorsicht" } } };
        var file = SettingsExchange.Export(source, new[] { "textSnippets" });
        Assert.Equal(2, file["parts"]!["textSnippets"]!.AsArray().Count);
        Assert.Null(file["parts"]!["hotkeys"]);

        var current = new AppSettings { TextSnippets = { new TextSnippet { Name = "neustart", Text = "Alt" }, new TextSnippet { Name = "Eigen", Text = "bleibt" } } };
        var plan = SettingsExchange.Plan(file.ToJsonString(), current);
        Assert.True(plan.Ok);
        Assert.Empty(plan.Weakening);
        Assert.Equal(2, plan.Changes.Count(c => c.Part == "textSnippets"));
        var r = SettingsExchange.Apply(plan, current, new HashSet<string>());
        Assert.Equal(3, r.TextSnippets.Count);
        Assert.Equal("Neu", r.TextSnippets.Single(x => x.Name.Equals("Neustart", StringComparison.OrdinalIgnoreCase)).Text);
        Assert.Contains(r.TextSnippets, x => x.Name == "Eigen");

        // nur gewählte Teile: ohne textSnippets ändert sich nichts
        var none = SettingsExchange.Plan(file.ToJsonString(), current, new[] { "hotkeys" });
        Assert.DoesNotContain(none.Changes, c => c.Part == "textSnippets");

        // ungültige Bausteine: Teil übersprungen, Rest der Datei gilt
        var bad = SettingsExchange.Export(new AppSettings { Theme = "light" });
        bad["parts"]!["textSnippets"] = new JsonArray(new JsonObject { ["name"] = "", ["text"] = "x" });
        var badPlan = SettingsExchange.Plan(bad.ToJsonString(), new AppSettings());
        Assert.True(badPlan.Ok);
        Assert.Contains(badPlan.SkippedParts, p => p.StartsWith(SettingsExchange.PartName("textSnippets")));
        Assert.Equal("light", SettingsExchange.Apply(badPlan, new AppSettings(), new HashSet<string>()).Theme);

        // Zurücksetzen lässt die eigenen Bausteine stehen
        var reset = SettingsExchange.Apply(SettingsExchange.PlanReset(current, includeTemplates: true), current, new HashSet<string>());
        Assert.Equal(2, reset.TextSnippets.Count);
    }

    // ---------------- Bildschirmleser: Symbolknöpfe brauchen einen Namen ----------------

    static string RepoRoot => Path.GetFullPath(Path.Combine(TestsDir(), "..", ".."));
    static string TestsDir([System.Runtime.CompilerServices.CallerFilePath] string p = "") =>
        typeof(BookmarkSnippetTests).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false).OfType<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "TestsDir")?.Value is { Length: > 0 } dir && Directory.Exists(dir) ? dir : Path.GetDirectoryName(p)!;

    /// <summary>Argumente eines Aufrufs ab der öffnenden Klammer (oberste Ebene, Zeichenketten und Klammern beachtet).</summary>
    public static List<string> Args(string src, int open)
    {
        var args = new List<string>();
        int depth = 0, start = open + 1;
        for (int i = open; i < src.Length; i++)
        {
            char c = src[i];
            if (c == '"')
            {
                bool verbatim = i > 0 && src[i - 1] == '@';
                i++;
                while (i < src.Length && src[i] != '"') { if (!verbatim && src[i] == '\\') i++; i++; }
                continue;
            }
            if (c == '\'') { i++; if (i < src.Length && src[i] == '\\') i++; i++; continue; }
            if (c is '(' or '{' or '[') depth++;
            else if (c is ')' or '}' or ']')
            {
                depth--;
                if (depth == 0) { args.Add(src[start..i].Trim()); return args; }
            }
            else if (c == ',' && depth == 1) { args.Add(src[start..i].Trim()); start = i + 1; }
        }
        return args;
    }

    [GeneratedRegex(@"\bU\.(Btn|Ghost|IconToggle)\(")]
    private static partial Regex ButtonCall();

    [GeneratedRegex(@"new\s+(?:[\w.]+\.)?(Button|ToggleButton|RadioButton)\s*\{")]
    private static partial Regex ButtonInit();

    /// <summary>Findet Symbolknöpfe ohne Namen: U.Btn/U.Ghost ohne Text und ohne Tooltip, Knöpfe mit reinem Symbol als Inhalt ohne Tooltip/Namen.</summary>
    public static List<string> UnnamedIconButtons(string src)
    {
        var found = new List<string>();
        int Line(int pos) => src.Take(pos).Count(ch => ch == '\n') + 1;
        static bool Empty(string? a) => a == null || a is "null" or "\"\"" or "default";
        foreach (Match m in ButtonCall().Matches(src))
        {
            var args = Args(src, m.Index + m.Length - 1);
            string kind = m.Groups[1].Value;
            if (kind == "IconToggle") { if (args.Count < 3 || Empty(args[2])) found.Add($"{Line(m.Index)}: U.IconToggle ohne Tooltip"); continue; }
            // U.Btn(text, click, …) hat den Text vorn; Symbolform: (glyph, text, click, tip, …)
            if (kind == "Btn" && args.Count >= 1 && (args[0].StartsWith("Loc.") || args[0].StartsWith('"') || args[0].StartsWith('$'))) continue;
            if (args.Count < 2 || !Empty(args[1])) continue;
            if (args.Count < 4 || Empty(args[3])) found.Add($"{Line(m.Index)}: U.{kind} ohne Text und ohne Tooltip");
        }
        foreach (Match m in ButtonInit().Matches(src))
        {
            var body = Args(src, m.Index + m.Length - 1).FirstOrDefault() ?? "";
            var all = string.Join(",", Args(src, m.Index + m.Length - 1));
            if (!Regex.IsMatch(all, @"Content\s*=\s*(U\.Icon|U\.IconBadge)\(")) continue;
            if (all.Contains("ToolTip") || all.Contains("AutomationProperties")) continue;
            // gleich danach benannt (… }.Named(…) oder SetName im selben Satz)
            int end = src.IndexOf(';', m.Index);
            var stmt = end > 0 ? src[m.Index..end] : "";
            if (stmt.Contains(".Named(") || stmt.Contains("SetName(")) continue;
            found.Add($"{Line(m.Index)}: {m.Groups[1].Value} mit reinem Symbol ohne Tooltip oder Namen");
        }
        return found;
    }

    [Fact]
    public void Scanner_FlagsUnnamedIconButtons()
    {
        Assert.Single(UnnamedIconButtons("var a = U.Ghost(Theme.IDelete, null, () => Del());"));
        Assert.Single(UnnamedIconButtons("var a = U.Btn(Theme.IAdd, null, Add, null, \"Ghost\");"));
        Assert.Empty(UnnamedIconButtons("var a = U.Ghost(Theme.IDelete, null, () => Del(), Loc.L(\"Löschen\", \"Delete\"));"));
        Assert.Empty(UnnamedIconButtons("var a = U.Btn(Theme.IAdd, Loc.L(\"Neu\", \"New\"), Add);"));
        Assert.Empty(UnnamedIconButtons("var a = U.Btn(Loc.L(\"Neu\", \"New\"), Add);"));
        Assert.Single(UnnamedIconButtons("var b = new Button { Content = U.Icon(Theme.IAdd), Margin = new Thickness(2) };"));
        Assert.Empty(UnnamedIconButtons("var b = new Button { Content = U.Icon(Theme.IAdd), ToolTip = Loc.L(\"Neu\", \"New\") };"));
        Assert.Empty(UnnamedIconButtons("var b = new ToggleButton { Content = U.Icon(Theme.IStar) }.Named(Loc.L(\"Stern\", \"Star\"));"));
    }

    /// <summary>NFA-015, BED-112: jeder Symbolknopf der App trägt einen Namen (aus Text oder Tooltip, siehe U.Named).</summary>
    [Fact]
    public void AppSources_HaveNoUnnamedIconButtons()
    {
        var files = Directory.EnumerateFiles(Path.Combine(RepoRoot, "src", "StepRecorder.App"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        var found = files.OrderBy(f => f, StringComparer.Ordinal)
            .SelectMany(f => UnnamedIconButtons(File.ReadAllText(f)).Select(x => Path.GetFileName(f) + ":" + x)).ToList();
        Assert.True(found.Count == 0, "Symbolknöpfe ohne Namen für Bildschirmleser – Tooltip oder .Named(…) setzen:\n" + string.Join("\n", found));
        // die Bausteine setzen den Namen selbst
        var controls = File.ReadAllText(Path.Combine(RepoRoot, "src", "StepRecorder.App", "Ui", "Controls.cs"));
        Assert.Contains("SetLabeledBy", controls);
        Assert.Contains("b.Named(", controls);
        Assert.Contains("rb.Named(", controls);
    }
}
