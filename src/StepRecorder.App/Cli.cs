using System.Text.Json.Nodes;
using StepRecorder.Compare;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.App;

/// <summary>Kopflose Befehle: export, validate, compare, settings, record --stop. Exit-Codes nach 02 §8.6.</summary>
static class Cli
{
    const string Help = """
Sebastians Step Recorder – portabler Schrittrekorder

Aufruf:
  SebastiansStepRecorder.exe                         Startfenster
  SebastiansStepRecorder.exe <Sitzung>               Sitzung im Viewer öffnen
  SebastiansStepRecorder.exe record [Optionen]       sofort aufnehmen (--scope screen|app|region, --monitor active|all|M1,
                                           --app <prozess.exe>, --region x,y,b,h, --title, --goal, --expected,
                                           --reference, --protection balanced|strict, --out <Ordner>, --profile <Name>)
  SebastiansStepRecorder.exe record --stop           laufende Aufnahme beenden (gibt den Sitzungspfad aus)
  SebastiansStepRecorder.exe export <Sitzung> --format html|md|docx|pdf|gif|ki|ahk|ahk-ki [--out <Pfad>] [--with <Sitzung>] [--lang de|en]
                                           [--detail kompakt|standard|vollstaendig] [--image-profile kompatibel|hochaufloesend|original]
                                           [--zip] [--strict] [--json]
  SebastiansStepRecorder.exe validate <Sitzung> [--json]
  SebastiansStepRecorder.exe compare <A> <B> [--format md|json] [--out <Pfad>]
  SebastiansStepRecorder.exe mcp --root <Ordner> [--allow-edit] [--lang de|en]
  SebastiansStepRecorder.exe --version

Exit-Codes: 0 ok · 1 Fehler · 2 Aufruf · 3 Sitzung nicht gefunden · 4 Prüfung fehlgeschlagen · 5 abgebrochen
            6 Aufnahme läuft bereits · 7 Voraussetzung fehlt · 8 Ziel nicht beschreibbar · 9 --strict-Abbruch
""";

    public static int Run(string[] args)
    {
        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "--version": Console.WriteLine($"{AppInfo.DisplayName} {AppInfo.Version}"); return 0;
                case "--help" or "-h" or "/?": Console.WriteLine(Help); return 0;
                case "export": return Export(args);
                case "validate": return ValidateCmd(args);
                case "compare": return CompareCmd(args);
                case "steps": return StepsCmd(args);
                case "record" when args.Contains("--stop"): return Ui.SingleInstance.SendStop();
                case "record" when args.Contains("--self-test"): Console.WriteLine("Selbsttest: Unsichtbarkeit wird beim Start der Oberfläche geprüft."); return 0;
                case "settings": Console.Error.WriteLine("Einstellungen exportieren/importieren: im Startfenster unter Einstellungen."); return 2;
            }
            Console.Error.WriteLine(Help);
            return 2;
        }
        catch (DirectoryNotFoundException ex) { Console.Error.WriteLine("Sitzung nicht gefunden: " + ex.Message); return 3; }
        catch (FileNotFoundException ex) { Console.Error.WriteLine("Sitzung nicht gefunden: " + ex.Message); return 3; }
        catch (InvalidDataException ex) { Console.Error.WriteLine("Sitzung nicht lesbar: " + ex.Message); return 3; }
        catch (UnauthorizedAccessException ex) { Console.Error.WriteLine("Ziel nicht beschreibbar: " + ex.Message); return 8; }
        catch (ExportTargetException ex) { Console.Error.WriteLine(ex.Message); return 8; }
        catch (Exception ex) { Console.Error.WriteLine("Fehler: " + ex.Message); if (Environment.GetEnvironmentVariable("STEPRECORDER_TRACE") == "1") Console.Error.WriteLine(ex); return 1; }
    }

    static string? Opt(string[] a, string name) { int i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }
    static bool Flag(string[] a, string name) => a.Contains(name);

    static int Export(string[] a)
    {
        if (a.Length < 2) { Console.Error.WriteLine("Sitzung fehlt."); return 2; }
        string fmt = (Opt(a, "--format") ?? "html").ToLowerInvariant();
        bool json = Flag(a, "--json");
        using var view = SessionView.Open(a[1], headless: true, lang: Opt(a, "--lang"));
        if (view.Session.State == "recording") { Console.Error.WriteLine("Die Sitzung ist nicht abgeschlossen."); return 3; }
        if (Flag(a, "--strict") && view.UnreviewedCount > 0) { Console.Error.WriteLine($"{view.UnreviewedCount} auffällige Eingaben ungeprüft – Abbruch (--strict)."); return 9; }
        ExportResult r;
        if (fmt == "ki")
        {
            string detail = (Opt(a, "--detail") ?? "standard") switch { "kompakt" or "compact" => "compact", "vollstaendig" or "vollständig" or "full" => "full", _ => "standard" };
            string prof = (Opt(a, "--image-profile") ?? "kompatibel") switch { "hochaufloesend" or "hochauflösend" or "highres" => "highres", "original" => "original", _ => "compatible" };
            r = KiPackage.Export(view, new KiOptions { Detail = detail, ImageProfile = prof, OutDir = Opt(a, "--out"), Zip = Flag(a, "--zip"), IncludeEvents = !Flag(a, "--no-events"), Language = Opt(a, "--lang"), View = Opt(a, "--view") ?? "screen" });
        }
        else if (fmt is "html" or "md" or "markdown" or "docx" or "pdf" or "gif")
            r = GuideExport.Export(view, new GuideOptions { Format = fmt == "markdown" ? "md" : fmt, OutPath = Opt(a, "--out"), Language = Opt(a, "--lang"), Importance = Opt(a, "--importance") ?? "primary", Overwrite = true });
        else if (fmt is "ahk" or "autohotkey")
            r = AhkExport.Export(view, new AhkOptions { OutPath = Opt(a, "--out"), RealTiming = Flag(a, "--real-timing"), Images = !Flag(a, "--no-images") });
        else if (fmt is "ahk-ki" or "ahkki")
        {
            var with = new List<string>();
            for (int i = 0; i + 1 < a.Length; i++) if (a[i] == "--with") with.Add(a[i + 1]);
            r = AhkKiPackage.Export(view, new AhkKiOptions { OutDir = Opt(a, "--out"), With = with, Ocr = !Flag(a, "--no-ocr") });
        }
        else { Console.Error.WriteLine("Unbekanntes Format: " + fmt); return 2; }
        if (json)
            Console.WriteLine(Json.Line(new JsonObject
            {
                ["files"] = new JsonArray(r.Files.Select(f => (JsonNode)f).ToArray()), ["warnings"] = new JsonArray(r.Warnings.Select(f => (JsonNode)f).ToArray()),
                ["notices"] = new JsonArray(r.Notices.Select(f => (JsonNode)f).ToArray()), ["unreviewed"] = r.Unreviewed,
                ["estimatedTokens"] = new JsonObject { ["text"] = r.TextTokens, ["images"] = r.ImageTokens }
            }));
        else
        {
            Console.WriteLine(r.Path);
            foreach (var w in r.Warnings.Concat(r.Notices)) Console.Error.WriteLine("Hinweis: " + w);
        }
        return 0;
    }

    static int ValidateCmd(string[] a)
    {
        if (a.Length < 2) { Console.Error.WriteLine("Sitzung fehlt."); return 2; }
        var (issues, report) = Validator.Validate(a[1], thorough: Flag(a, "--thorough"));
        if (Flag(a, "--json")) Console.WriteLine(Json.Pretty(report));
        else
        {
            var bk = report.Obj("bookkeeping")!; var ev = report.Obj("evidence")!;
            Console.WriteLine($"Eingaben: {bk.Long("inputEvents")} · zugeordnet {bk.Long("assigned")} · ohne Schritt {bk.Long("orphans")}");
            Console.WriteLine($"Aktionsschritte: {ev.Long("actionSteps")} · belegt {ev.Long("confirmed")} · nur Eingabe {ev.Long("inputOnly")} · unbekannt {ev.Long("unknown")}");
            Console.WriteLine($"Lücken: {report.Obj("gaps").Long("count")}");
            foreach (var i in issues) Console.WriteLine($"{i.Severity.ToUpperInvariant(),-7} {i.Code} {i.Message}{(i.Seq != null ? $" (seq {i.Seq})" : "")}");
            Console.WriteLine(issues.Any(i => i.Severity == "error") ? "Ergebnis: FEHLER" : "Ergebnis: OK");
        }
        return issues.Any(i => i.Severity == "error") ? 4 : 0;
    }

    static int CompareCmd(string[] a)
    {
        if (a.Length < 3) { Console.Error.WriteLine("Zwei Sitzungen angeben."); return 2; }
        using var va = SessionView.Open(a[1], headless: true);
        using var vb = SessionView.Open(a[2], headless: true);
        var r = StepCompare.Compare(va, vb);
        string text = (Opt(a, "--format") ?? "md") == "json" ? Json.Pretty(r) : StepCompare.Markdown(r, va.Language);
        if (Opt(a, "--out") is { } o) { File.WriteAllText(o, text); Console.WriteLine(o); } else Console.WriteLine(text);
        return 0;
    }

    static int StepsCmd(string[] a)
    {
        using var view = SessionView.Open(a[1], headless: true);
        foreach (var s in view.All.Where(x => x.Importance != "trace"))
            Console.WriteLine($"{s.Number,4}. {Fmt.Time(s.TFrom, "de")}  {s.Kind,-12} {s.Protocol}  [{s.EvidenceLevel}]");
        return 0;
    }
}
