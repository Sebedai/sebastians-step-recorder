using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>
/// Sicherheitsnetz für den Handbuch-Ausbau (13, Leitplanke 1): Eine Aufnahme ohne neue Schlüssel ergibt dieselbe Ausgabe wie vorher.
/// Verglichen wird eine normalisierte Fassung je Format (Bilddaten und eingebettete Schriften durch ihre Länge ersetzt, Programmversion
/// entfernt) mit den Referenzdateien unter <c>Golden/</c>. Neu schreiben: Umgebungsvariable <c>STEPRECORDER_UPDATE_GOLDEN=1</c> –
/// nur bei einer gewollten, im Commit begründeten Änderung.
/// </summary>
[Collection("Loc")]
public class LegacyOutputTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create(withComment: true);
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-golden-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } try { Directory.Delete(_out, true); } catch { } }

    SessionView Prepared(string lang)
    {
        var v = SessionView.Open(_folder, headless: true, lang: lang);
        var steps = v.Effective(guide: true, importance: "primary").Where(s => s.Kind != "move").ToList();
        v.Edits["steps"] = new JsonArray(
            new JsonObject { ["anchor"] = steps[0].Anchor, ["chapter"] = new JsonObject { ["title"] = "Vorbereitung", ["text"] = "Zuerst das Menü öffnen.", ["en"] = new JsonObject { ["title"] = "Preparation" } } },
            new JsonObject { ["anchor"] = steps[1].Anchor, ["text"] = new JsonObject { ["de"] = new JsonObject { ["title"] = "Speichern unter wählen", ["body"] = "Wählen Sie *Speichern unter* – siehe [Hilfe](https://example.org).\nZweite Zeile mit `Code`." } } });
        v.Edits["comments"] = new JsonArray(
            new JsonObject { ["id"] = "uc1", ["anchor"] = steps[0].Anchor, ["kind"] = "important", ["text"] = "Vorher speichern!" },
            new JsonObject { ["id"] = "uc2", ["anchor"] = steps[1].Anchor, ["kind"] = "warning", ["text"] = "Nicht **schließen**." });
        v.Edits["guide"] = new JsonObject
        {
            ["title"] = "Handbuch Test", ["intro"] = "Diese Anleitung zeigt den Ablauf.", ["outro"] = "Fertig.",
            ["doc"] = new DocSettings { Subtitle = "Kurzanleitung", Version = "1.2", Author = "IT", Prerequisites = "Zugang\nRechte", Cover = true, Toc = true, Footer = "Intern" }.ToJson()
        };
        v.Rebuild();
        return v;
    }

    public static IEnumerable<object[]> Cases() => new[]
    {
        new object[] { "html", "de" }, new object[] { "md", "de" }, new object[] { "docx", "de" }, new object[] { "pdf", "de" },
        new object[] { "pptx", "de" }, new object[] { "tour", "de" }, new object[] { "bugreport", "de" }, new object[] { "pdf", "en" }, new object[] { "html", "en" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Export_MatchesGolden(string format, string lang)
    {
        using var v = Prepared(lang);
        string dir = Path.Combine(_out, format + "-" + lang);
        Directory.CreateDirectory(dir);
        var r = GuideExport.Export(v, new GuideOptions { Format = format, Language = lang, OutPath = Path.Combine(dir, "out." + (format is "md" or "bugreport" ? "md" : format == "tour" ? "html" : format)) });
        string actual = Normalize(format, r.Path!);
        string golden = Path.Combine(GoldenDir(), $"{format}-{lang}.txt");
        if (Environment.GetEnvironmentVariable("STEPRECORDER_UPDATE_GOLDEN") == "1" || !File.Exists(golden))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(golden)!);
            File.WriteAllText(golden, actual, new UTF8Encoding(false));
            if (Environment.GetEnvironmentVariable("STEPRECORDER_UPDATE_GOLDEN") != "1") Assert.Fail("Referenz fehlte und wurde angelegt: " + golden);
            return;
        }
        string expected = File.ReadAllText(golden).Replace("\r\n", "\n");
        if (expected != actual)
        {
            string dump = Path.Combine(Path.GetTempPath(), $"steprec-golden-actual-{format}-{lang}.txt");
            File.WriteAllText(dump, actual, new UTF8Encoding(false));
            Assert.Fail($"Ausgabe {format}/{lang} weicht von der Referenz ab. Ist: {dump}  Soll: {golden}");
        }
    }

    static string GoldenDir([System.Runtime.CompilerServices.CallerFilePath] string p = "")
    {
        string dir = typeof(LegacyOutputTests).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "TestsDir")?.Value is { Length: > 0 } d && Directory.Exists(d) ? d : Path.GetDirectoryName(p)!;
        return Path.Combine(dir, "Golden");
    }

    static readonly Regex DataUri = new(@"data:image/[a-z]+;base64,[A-Za-z0-9+/=]+", RegexOptions.Compiled);

    /// <summary>Format-unabhängige, maschinenneutrale Fassung: Text bleibt, Bild- und Schriftbytes werden zur Länge.</summary>
    public static string Normalize(string format, string path)
    {
        string Version(string s) => s.Replace(StepRecorder.Data.AppInfo.Version, "{VERSION}");
        switch (format)
        {
            case "html" or "tour":
                return Version(DataUri.Replace(File.ReadAllText(path), _ => "data:image")).Replace("\r\n", "\n");
            case "md" or "bugreport":
            {
                var sb = new StringBuilder(File.ReadAllText(path).Replace("\r\n", "\n"));
                foreach (var f in Directory.GetFiles(Path.GetDirectoryName(path)!, "*", SearchOption.AllDirectories).Where(f => f != path).OrderBy(f => f, StringComparer.Ordinal))
                    sb.Append("\n#file ").Append(Path.GetRelativePath(Path.GetDirectoryName(path)!, f).Replace('\\', '/'));
                return Version(sb.ToString());
            }
            case "docx" or "pptx":
            {
                using var z = ZipFile.OpenRead(path);
                var sb = new StringBuilder();
                foreach (var e in z.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
                {
                    sb.Append("#entry ").Append(e.FullName).Append('\n');
                    if (e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels"))
                    {
                        using var r = new StreamReader(e.Open());
                        sb.Append(r.ReadToEnd().Replace("\r\n", "\n")).Append('\n');
                    }
                    else sb.Append("binary\n");
                }
                return Version(sb.ToString());
            }
            case "pdf":
            {
                var raw = Encoding.Latin1.GetString(File.ReadAllBytes(path));
                // Bild- und Schriftströme durch ihre Länge ersetzen; Querverweistabelle und Versätze entfallen
                var sb = new StringBuilder();
                var obj = new Regex(@"(\d+) 0 obj\n(.*?)\nendobj\n", RegexOptions.Singleline);
                foreach (Match m in obj.Matches(raw))
                {
                    string body = m.Groups[2].Value;
                    int s = body.IndexOf("stream\n", StringComparison.Ordinal);
                    if (s >= 0)
                    {
                        string dict = body[..s];
                        bool binary = dict.Contains("/DCTDecode") || dict.Contains("/Length1") || dict.Contains("/FlateDecode");
                        string data = body[(s + 7)..];
                        int e = data.LastIndexOf("\nendstream", StringComparison.Ordinal);
                        if (e >= 0) data = data[..e];
                        body = binary ? Regex.Replace(dict, @"/Length1? \d+", "/Length ?") + "stream binary" : dict + "stream\n" + data;
                    }
                    sb.Append(m.Groups[1].Value).Append(" obj ").Append(body).Append('\n');
                }
                return Version(sb.ToString());
            }
        }
        throw new ArgumentException(format);
    }
}
