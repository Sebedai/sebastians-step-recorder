using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using StepRecorder.Settings;

namespace StepRecorder.Tests;

/// <summary>
/// Sicherheitsnetz der Zweisprachigkeit: Deutsch aussehende Texte außerhalb von <c>Loc.L/F/N(…)</c> dürfen je Datei
/// nicht mehr werden (Baseline <c>loc-baseline.json</c>; neu schreiben mit STEPRECORDER_LOC_BASELINE=update).
/// </summary>
[Collection("Loc")]
public partial class LocalizationTests
{
    static string TestsDir([CallerFilePath] string p = "") => Path.GetDirectoryName(p)!;
    static string RepoRoot => Path.GetFullPath(Path.Combine(TestsDir(), "..", ".."));
    static string BaselineFile => Path.Combine(TestsDir(), "loc-baseline.json");

    public sealed record Literal(string Text, bool Wrapped, int Line, string? Call);

    [GeneratedRegex(@"[äöüÄÖÜß„“]|\b(und|oder|nicht|der|die|das|den|dem|ein|eine|einen|ist|sind|wird|werden|mit|für|auf|bitte|Aufnahme|Aufnahmen|Schritt|Schritte|Bild|Bilder|Datei|Ordner|Fehler|Einstellungen|Abbrechen|Öffnen|Schließen|Speichern|Kopieren|Hinweis|Anleitung|Fenster|Taste|Tasten|Eingabe|Eingaben|Ausblenden|Einblenden|Zurück|Weiter|Neu|Alle|Keine|Kein|Ziel|Bereich|Übernehmen|Löschen|Exportieren|Durchsicht|Vorlage|Kapitel|Text|Titel|Sprache|Leiste|Marker|Kommentar)\b")]
    private static partial Regex GermanLooking();

    /// <summary>Zerlegt C#-Quelltext in String-Literale und merkt, ob sie in einem Loc-Aufruf (oder Protokoll-Aufruf) stehen.</summary>
    public static List<Literal> Scan(string src)
    {
        var result = new List<Literal>();
        var calls = new Stack<(int depth, string name)>();
        int depth = 0, line = 1, i = 0;
        string? pendingCall = null;
        while (i < src.Length)
        {
            char c = src[i];
            if (c == '\n') { line++; i++; continue; }
            if (c == '/' && i + 1 < src.Length && src[i + 1] == '/') { while (i < src.Length && src[i] != '\n') i++; continue; }
            if (c == '/' && i + 1 < src.Length && src[i + 1] == '*') { int e = src.IndexOf("*/", i + 2, StringComparison.Ordinal); for (int k = i; k < (e < 0 ? src.Length : e + 2); k++) if (src[k] == '\n') line++; i = e < 0 ? src.Length : e + 2; continue; }
            if (c == '\'') { i++; if (i < src.Length && src[i] == '\\') i += 2; else i++; while (i < src.Length && src[i] != '\'') i++; i++; continue; }
            if (char.IsLetter(c) || c == '_')
            {
                int s = i; while (i < src.Length && (char.IsLetterOrDigit(src[i]) || src[i] is '_' or '.')) i++;
                string ident = src[s..i];
                int j = i; while (j < src.Length && src[j] == ' ') j++;
                if (j < src.Length && src[j] == '(' && (ident is "Loc.L" or "Loc.F" or "Loc.N" or "Loc.Keys" or "Settings.Loc.L" or "Settings.Loc.F" or "Settings.Loc.N"
                    || ident.EndsWith("Log") || ident.EndsWith(".Write") && ident.Contains("AppLog") || ident is "Debug.Assert" or "nameof" or "Theme.Style" or "Style" or "FindResource" or "Regex" or "GeneratedRegex"))
                    pendingCall = ident;
                continue;
            }
            if (c == '(') { depth++; if (pendingCall != null) { calls.Push((depth, pendingCall)); pendingCall = null; } i++; continue; }
            if (c == ')') { if (calls.Count > 0 && calls.Peek().depth == depth) calls.Pop(); depth--; i++; continue; }
            if (c is ';' or '{' or '}') pendingCall = null;
            if (c == '$' || c == '@' || c == '"')
            {
                int p = i; bool interp = false, verbatim = false;
                while (p < src.Length && src[p] is '$' or '@') { if (src[p] == '$') interp = true; else verbatim = true; p++; }
                if (p >= src.Length || src[p] != '"') { i++; continue; }
                int startLine = line;
                var sb = new StringBuilder();
                int q = 0; while (p + q < src.Length && src[p + q] == '"') q++;
                if (q >= 3)
                {
                    // Rohstring """ … """
                    string fence = new('"', q);
                    int e = src.IndexOf(fence, p + q, StringComparison.Ordinal);
                    if (e < 0) e = src.Length;
                    sb.Append(src, p + q, e - (p + q));
                    for (int k = p; k < e; k++) if (src[k] == '\n') line++;
                    i = Math.Min(src.Length, e + q);
                }
                else
                {
                    int k = p + 1, brace = 0;
                    while (k < src.Length)
                    {
                        char ch = src[k];
                        if (ch == '\n') line++;
                        if (verbatim && ch == '"' && k + 1 < src.Length && src[k + 1] == '"') { sb.Append('"'); k += 2; continue; }
                        if (!verbatim && ch == '\\') { sb.Append(ch); if (k + 1 < src.Length) sb.Append(src[k + 1]); k += 2; continue; }
                        if (interp && ch == '{' && k + 1 < src.Length && src[k + 1] == '{') { sb.Append("{{"); k += 2; continue; }
                        if (interp && ch == '{') brace++;
                        if (interp && ch == '}' && brace > 0) brace--;
                        if (ch == '"' && brace == 0) break;
                        if (interp && brace > 0 && ch == '"')
                        {
                            // verschachteltes Literal in einem Ausdruck: überspringen
                            int e2 = k + 1; while (e2 < src.Length && src[e2] != '"') { if (src[e2] == '\\') e2++; e2++; }
                            sb.Append(src, k, Math.Min(src.Length, e2 + 1) - k); k = e2 + 1; continue;
                        }
                        sb.Append(ch); k++;
                    }
                    i = k + 1;
                }
                var call = calls.Count > 0 ? calls.Peek().name : null;
                bool wrapped = calls.Any(x => x.name.StartsWith("Loc.") || x.name.StartsWith("Settings.Loc.") || x.name.EndsWith("Log") || x.name.Contains("AppLog") || x.name is "Debug.Assert" or "nameof" or "Theme.Style" or "Style" or "FindResource" or "Regex" or "GeneratedRegex");
                result.Add(new Literal(sb.ToString(), wrapped, startLine, call));
                continue;
            }
            i++;
        }
        return result;
    }

    static bool LooksGerman(string text) => text.Any(char.IsLetter) && GermanLooking().IsMatch(text);

    static IEnumerable<string> AppSources() => Directory.EnumerateFiles(Path.Combine(RepoRoot, "src", "StepRecorder.App"), "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    static Dictionary<string, int> Count()
    {
        var d = new Dictionary<string, int>();
        foreach (var f in AppSources().OrderBy(x => x, StringComparer.Ordinal))
        {
            int n = Scan(File.ReadAllText(f)).Count(l => !l.Wrapped && LooksGerman(l.Text));
            if (n > 0) d[Path.GetRelativePath(RepoRoot, f).Replace('\\', '/')] = n;
        }
        return d;
    }

    [Fact]
    public void Scanner_FindsWrappedAndBareLiterals()
    {
        var lits = Scan("var a = Loc.L(\"Öffnen\", \"Open\"); var b = \"Schließen\"; var c = $\"{n} Schritte\"; UiApp.Log(\"Aufnahme gestartet\"); var r = \"\"\"\nLänger\n\"\"\";");
        Assert.Contains(lits, l => l.Text == "Öffnen" && l.Wrapped);
        Assert.Contains(lits, l => l.Text == "Schließen" && !l.Wrapped);
        Assert.Contains(lits, l => l.Text.Contains("Schritte") && !l.Wrapped);
        Assert.Contains(lits, l => l.Text == "Aufnahme gestartet" && l.Wrapped);
        Assert.Contains(lits, l => l.Text.Contains("Länger") && !l.Wrapped);
    }

    [Fact]
    public void BareGermanLiterals_DoNotIncrease()
    {
        var now = Count();
        if (Environment.GetEnvironmentVariable("STEPRECORDER_LOC_BASELINE") == "update")
        {
            File.WriteAllText(BaselineFile, JsonSerializer.Serialize(new SortedDictionary<string, int>(now), new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        var baseline = File.Exists(BaselineFile) ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(BaselineFile))! : new();
        var worse = now.Where(kv => kv.Value > baseline.GetValueOrDefault(kv.Key)).Select(kv => $"{kv.Key}: {kv.Value} (erlaubt {baseline.GetValueOrDefault(kv.Key)})").ToList();
        Assert.True(worse.Count == 0, "Neue unübersetzte Texte – bitte Loc.L(\"…\", \"…\") verwenden:\n" + string.Join("\n", worse));
    }

    [Fact]
    public void LocCalls_HaveEnglishAndMatchingPlaceholders()
    {
        var problems = new List<string>();
        var rx = new Regex(@"Loc\.(L|F)\(\s*(\$?)""((?:[^""\\]|\\.)*)""\s*,\s*(\$?)""((?:[^""\\]|\\.)*)""");
        foreach (var f in AppSources().Concat(Directory.EnumerateFiles(Path.Combine(RepoRoot, "src", "StepRecorder.Core"), "*.cs", SearchOption.AllDirectories)))
        {
            foreach (Match m in rx.Matches(File.ReadAllText(f)))
            {
                string de = m.Groups[3].Value, en = m.Groups[5].Value;
                if (en.Trim().Length == 0 && de.Trim().Length > 0) problems.Add($"{Path.GetFileName(f)}: leerer englischer Text zu „{de}“");
                if (m.Groups[1].Value == "F")
                {
                    var pd = Regex.Matches(de, @"\{\d+[^}]*\}").Select(x => x.Value).OrderBy(x => x).ToList();
                    var pe = Regex.Matches(en, @"\{\d+[^}]*\}").Select(x => x.Value).OrderBy(x => x).ToList();
                    if (!pd.SequenceEqual(pe)) problems.Add($"{Path.GetFileName(f)}: Platzhalter verschieden in „{de}“ / „{en}“");
                }
                if (m.Groups[2].Value == "$" && m.Groups[4].Value == "$" && (de.Contains("++") || de.Contains("--"))) problems.Add($"{Path.GetFileName(f)}: Seiteneffekt im Loc-Argument „{de}“");
            }
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Loc_ResolvesAndFormats()
    {
        Assert.Equal("de", Loc.Resolve("system", System.Globalization.CultureInfo.GetCultureInfo("de-AT")));
        Assert.Equal("en", Loc.Resolve("system", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")));
        Assert.Equal("en", Loc.Resolve("en", System.Globalization.CultureInfo.GetCultureInfo("de-DE")));
        Assert.Equal("de", Loc.Resolve("quatsch", System.Globalization.CultureInfo.GetCultureInfo("de-DE")));
        try
        {
            Loc.Set("en");
            Assert.Equal("Open", Loc.L("Öffnen", "Open"));
            Assert.Equal("3 steps", Loc.N(3, "1 Schritt", "{0} Schritte", "1 step", "{0} steps"));
            Assert.Equal("Ctrl+Shift+F10", Loc.Keys("Ctrl+Shift+F10"));
            Assert.Equal("2.0 MB", Loc.Bytes(2 * 1024 * 1024));
            Loc.Set("de");
            Assert.Equal("Strg+Umschalt+F10", Loc.Keys("Ctrl+Shift+F10"));
            Assert.Equal("1 Schritt", Loc.N(1, "1 Schritt", "{0} Schritte", "1 step", "{0} steps"));
            Assert.Equal("2,0 MB", Loc.Bytes(2 * 1024 * 1024));
            Loc.Set("qps");
            Assert.Equal("⟦Open⟧", Loc.L("Öffnen", "Open"));
        }
        finally { Loc.Set("de"); }
    }
}
