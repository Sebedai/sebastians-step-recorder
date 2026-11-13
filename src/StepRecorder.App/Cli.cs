using System.Text.Json.Nodes;
using StepRecorder.Compare;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App;

/// <summary>Kopflose Befehle: export, check, validate, compare, steps, settings, record --stop/--self-test. Exit-Codes nach 02 §8.6.</summary>
static class Cli
{
    public static string Help => Loc.L(
"""
Sebastians Step Recorder – portabler Schrittrekorder

Aufruf:
  SebastiansStepRecorder.exe                         Startfenster
  SebastiansStepRecorder.exe <Aufnahme>              Aufnahme im Editor öffnen (Ordner, manifest.json oder .steprec)
  SebastiansStepRecorder.exe record [Optionen]       sofort aufnehmen
      --scope screen|app|region   --monitor active|all|M1[,M2]   --app <prozess.exe>   --region x,y,b,h|last
      --title <Text>   --goal <Text>   --expected <Text>   --reference <Text>
      --protection balanced|strict   --profile anleitung|support-fall|ki-spur|fehler-nachstellen|eigene   --out <Ordner>
      --after-stop viewer|export|supportPackage|folder|none   --skip clicks,wheel,moves,typing,hotkeys,keys   --json
      --log <Datei>…   Protokolldatei mitlesen (Zeilen während der Aufnahme; prüfpflichtig; nicht in „Streng“)
      gibt am Ende den Aufnahmeordner aus (mit --json als JSON); Exit 5, wenn die Aufnahme verworfen wurde
  SebastiansStepRecorder.exe record --stop           laufende Aufnahme beenden (gibt den Aufnahmeordner aus)
  SebastiansStepRecorder.exe record --self-test [--json]   Bildaufnahme, Unsichtbarkeit, UI Automation, Ordner, Tastenkürzel prüfen
  SebastiansStepRecorder.exe support                 Support-Aufnahme im Startfenster vorbereiten
  SebastiansStepRecorder.exe export <Aufnahme> --format html|md|docx|pdf|gif|mp4|pptx|tour|bugreport|ki|ahk|ahk-ki
                                                 |flaui|pywinauto|playwright|test-ki   (UI-Test-Entwürfe)
      [--out <Pfad>] [--lang de|en] [--json] [--strict] [--from n] [--to n] [--steps a-b]
      Anleitungen: [--importance primary|secondary|all] [--view screen|window|crop] [--template standard|kurz|checkliste]
                   [--after nie|aenderung|immer]   Nachher-Bild zusätzlich als „Ergebnis“
      Word, PDF:   [--page a4|letter] [--landscape|--portrait]   Seitenformat (sonst aus der Aufnahme)
      Markdown:    [--md-dialect plain|github|obsidian] [--front-matter] [--md-legacy]      GIF: [--gif-max-mb n]
      GIF/Video:   [--seconds n]   Sekunden je Schritt (0.5–20, Standard 2.5); Video = MP4 (H.264)
      KI-Paket:    [--detail kompakt|standard|vollstaendig] [--image-profile kompatibel|hochaufloesend|original] [--zip] [--no-events]
                   Bilder: [--before auto|immer|nie] [--no-crop] [--no-after]
      AutoHotkey:  [--real-timing] [--no-images]      AHK für KI: [--with <Aufnahme>]… [--no-ocr]
      KI-Paket und AutoHotkey: [--readable]   für Menschen lesbar, ausführlich kommentiert
  SebastiansStepRecorder.exe check <Aufnahme> [--lang de|en] [--importance …] [--json] [--strict]   Qualitätsprüfung
  SebastiansStepRecorder.exe validate <Aufnahme> [--json] [--thorough]
  SebastiansStepRecorder.exe testability <Aufnahme> [--json] [--strict] [--lang de|en]   Bedienelemente ohne AutomationId (--strict: Exit 9)
  SebastiansStepRecorder.exe compare <A> <B> [--format md|json] [--out <Pfad>] [--images [--threshold 1-255]]
      --images: Bilder vergleichen; mit md und --out zusätzlich Vorher/Nachher-Bilder im Ordner <name>-bilder
  SebastiansStepRecorder.exe steps <Aufnahme> [--json] [--all]   Schrittliste mit Reaktionszeiten
  SebastiansStepRecorder.exe list [--json] [--limit n] [--root <Ordner>] [--all]   Aufnahmen auflisten (neueste zuerst)
      Statt <Aufnahme> überall auch: latest (neueste Aufnahme im Aufnahmeordner)
  SebastiansStepRecorder.exe settings export <Datei> [--parts hotkeys,excluded,exportTemplates,customProfile,preferences]
  SebastiansStepRecorder.exe settings import <Datei> [--parts …] [--confirm] [--allow-weaker <Kennung>]… [--json]
  SebastiansStepRecorder.exe settings reset [--templates] [--confirm] [--allow-weaker <Kennung>]…
  SebastiansStepRecorder.exe mcp [--root <Ordner>] [--allow-edit] [--allow-requests] [--lang de|en]
      [--max-result-tokens 2000-24000] [--max-image-pixels 250000-3750000] [--max-images 0-6]
  SebastiansStepRecorder.exe --version

Exit-Codes: 0 ok · 1 Fehler · 2 Aufruf · 3 Aufnahme nicht gefunden · 4 Prüfung fehlgeschlagen · 5 abgebrochen
            6 Aufnahme läuft · 7 Voraussetzung fehlt · 8 Ziel nicht beschreibbar · 9 --strict-Abbruch
""",
"""
Sebastians Step Recorder – portable step recorder

Usage:
  SebastiansStepRecorder.exe                         start window
  SebastiansStepRecorder.exe <recording>             open a recording in the editor (folder, manifest.json or .steprec)
  SebastiansStepRecorder.exe record [options]        record now
      --scope screen|app|region   --monitor active|all|M1[,M2]   --app <process.exe>   --region x,y,w,h|last
      --title <text>   --goal <text>   --expected <text>   --reference <text>
      --protection balanced|strict   --profile guide|support|aitrace|bugrepro|custom   --out <folder>
      --after-stop viewer|export|supportPackage|folder|none   --skip clicks,wheel,moves,typing,hotkeys,keys   --json
      --log <file>…   read a log file along (lines during the recording; requires review; not in "strict")
      prints the recording folder at the end (JSON with --json); exit 5 if the recording was discarded
  SebastiansStepRecorder.exe record --stop           stop the running recording (prints its folder)
  SebastiansStepRecorder.exe record --self-test [--json]   check capture, invisibility, UI Automation, folder, shortcuts
  SebastiansStepRecorder.exe support                 prepare a support recording in the start window
  SebastiansStepRecorder.exe export <recording> --format html|md|docx|pdf|gif|mp4|pptx|tour|bugreport|ki|ahk|ahk-ki
                                                 |flaui|pywinauto|playwright|test-ki   (UI test drafts)
      [--out <path>] [--lang de|en] [--json] [--strict] [--from n] [--to n] [--steps a-b]
      Guides:     [--importance primary|secondary|all] [--view screen|window|crop] [--template standard|short|checklist]
                  [--after never|changed|always]   also the after image as "Result"
      Word, PDF:  [--page a4|letter] [--landscape|--portrait]   page format (otherwise from the recording)
      Markdown:   [--md-dialect plain|github|obsidian] [--front-matter] [--md-legacy]      GIF: [--gif-max-mb n]
      GIF/video:  [--seconds n]   seconds per step (0.5–20, default 2.5); video = MP4 (H.264)
      AI package: [--detail compact|standard|full] [--image-profile compatible|highres|original] [--zip] [--no-events]
                  images: [--before auto|always|never] [--no-crop] [--no-after]
      AutoHotkey: [--real-timing] [--no-images]      AHK for AI: [--with <recording>]… [--no-ocr]
      AI package and AutoHotkey: [--readable]   readable for people, extensively commented
  SebastiansStepRecorder.exe check <recording> [--lang de|en] [--importance …] [--json] [--strict]   quality check
  SebastiansStepRecorder.exe validate <recording> [--json] [--thorough]
  SebastiansStepRecorder.exe testability <recording> [--json] [--strict] [--lang de|en]   controls without AutomationId (--strict: exit 9)
  SebastiansStepRecorder.exe compare <A> <B> [--format md|json] [--out <path>] [--images [--threshold 1-255]]
      --images: compare images; with md and --out also before/after images in the folder <name>-bilder
  SebastiansStepRecorder.exe steps <recording> [--json] [--all]   step list with response times
  SebastiansStepRecorder.exe list [--json] [--limit n] [--root <folder>] [--all]   list recordings (newest first)
      Instead of <recording> you can always write: latest (newest recording in the recordings folder)
  SebastiansStepRecorder.exe settings export <file> [--parts hotkeys,excluded,exportTemplates,customProfile,preferences]
  SebastiansStepRecorder.exe settings import <file> [--parts …] [--confirm] [--allow-weaker <id>]… [--json]
  SebastiansStepRecorder.exe settings reset [--templates] [--confirm] [--allow-weaker <id>]…
  SebastiansStepRecorder.exe mcp [--root <folder>] [--allow-edit] [--allow-requests] [--lang de|en]
      [--max-result-tokens 2000-24000] [--max-image-pixels 250000-3750000] [--max-images 0-6]
  SebastiansStepRecorder.exe --version

Exit codes: 0 ok · 1 error · 2 usage · 3 recording not found · 4 check failed · 5 cancelled
            6 recording in progress · 7 prerequisite missing · 8 target not writable · 9 --strict abort
""");

    public static int Run(string[] args)
    {
        Loc.Init(AppSettings.Load().UiLanguage);
        AppLog.InstallCrashHandlers();
        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "--version": Console.WriteLine($"{AppInfo.DisplayName} {AppInfo.Version}"); return 0;
                case "--help" or "-h" or "/?": Console.WriteLine(Help); return 0;
                case "export": return Export(args);
                case "check": return CheckCmd(args);
                case "validate": return ValidateCmd(args);
                case "compare": return CompareCmd(args);
                case "steps": return StepsCmd(args);
                case "list": return ListCmd(args);
                case "testability": return TestabilityCmd(args);
                case "record" when args.Contains("--stop"): return Ui.SingleInstance.SendStop();
                case "record" when args.Contains("--self-test"): return SelfTest(args);
                case "settings": return SettingsCmd(args);
            }
            Console.Error.WriteLine(Help);
            return 2;
        }
        catch (DirectoryNotFoundException ex) { Console.Error.WriteLine(Loc.L("Aufnahme nicht gefunden: ", "Recording not found: ") + ex.Message); return 3; }
        catch (FileNotFoundException ex) { Console.Error.WriteLine(Loc.L("Aufnahme nicht gefunden: ", "Recording not found: ") + ex.Message); return 3; }
        catch (InvalidDataException ex) { Console.Error.WriteLine(Loc.L("Aufnahme nicht lesbar: ", "Recording not readable: ") + ex.Message); return 3; }
        catch (UnauthorizedAccessException ex) { Console.Error.WriteLine(Loc.L("Ziel nicht beschreibbar: ", "Target not writable: ") + ex.Message); return 8; }
        catch (ExportTargetException ex) { Console.Error.WriteLine(ex.Message); return 8; }
        catch (ExportPrerequisiteException ex) { Console.Error.WriteLine(ex.Message); return 7; }
        catch (OperationCanceledException) { Console.Error.WriteLine(Loc.L("Abgebrochen.", "Cancelled.")); return 5; }
        catch (Exception ex) { Console.Error.WriteLine(Loc.L("Fehler: ", "Error: ") + ex.Message); AppLog.Error("CLI", ex); if (Environment.GetEnvironmentVariable("STEPRECORDER_TRACE") == "1") Console.Error.WriteLine(ex); return 1; }
    }

    static int Usage(string? error) { Console.Error.WriteLine(error ?? Loc.L("Aufruf unvollständig.", "Incomplete command.")); Console.Error.WriteLine(Loc.L("Hilfe: SebastiansStepRecorder.exe --help", "Help: SebastiansStepRecorder.exe --help")); return 2; }

    /// <summary>Sitzungsangabe: Pfad oder <c>latest</c> (neueste Aufnahme im Aufnahmeordner).</summary>
    static string S(string arg) => SessionCatalog.Resolve(arg, AppPaths.DefaultSessionsFolder());

    static string? NeedSession(CliArgs c, int count = 1)
        => c.Positional.Count < count ? Loc.L("Aufnahme fehlt.", "Recording missing.") : c.Positional.Count > count ? Loc.F("Zu viele Angaben: {0}", "Too many arguments: {0}", string.Join(" ", c.Positional.Skip(count))) : null;

    // ---------------- record (Prüfung vor dem Start der Oberfläche) ----------------

    static readonly string[] RecordValues = { "--scope", "--monitor", "--app", "--region", "--title", "--goal", "--expected", "--reference", "--protection", "--out", "--profile", "--after-stop", "--skip" };

    /// <summary>Prüft die Optionen von <c>record</c>; null = in Ordnung.</summary>
    public static string? ValidateRecordArgs(string[] args)
    {
        var c = CliArgs.Parse(args, 1, RecordValues, new[] { "--json" }, "--log");
        if (c.Error != null) return c.Error;
        if (c.Positional.Count > 0) return Loc.F("Unerwartete Angabe: {0}", "Unexpected argument: {0}", c.Positional[0]);
        if (c["--scope"] is { } sc && sc is not ("screen" or "app" or "region")) return Loc.L("--scope muss screen, app oder region sein.", "--scope must be screen, app or region.");
        if (c["--monitor"] is { } mo && mo is not ("active" or "all") && !System.Text.RegularExpressions.Regex.IsMatch(mo, @"^M\d+(,M\d+)*$")) return Loc.L("--monitor: active, all oder M1,M2 …", "--monitor: active, all or M1,M2 …");
        if (c["--region"] is { } rg && rg != "last")
        {
            var p = rg.Split(',');
            if (p.Length != 4 || !p.All(x => int.TryParse(x.Trim(), out _))) return Loc.L("--region: x,y,Breite,Höhe (ganze Zahlen) oder last", "--region: x,y,width,height (integers) or last");
            if (int.Parse(p[2]) < 64 || int.Parse(p[3]) < 64) return Loc.L("--region: mindestens 64 × 64 Pixel", "--region: at least 64 × 64 pixels");
        }
        if (c["--region"] != null && c["--scope"] is { } s2 && s2 != "region") return Loc.L("--region nur zusammen mit --scope region", "--region only together with --scope region");
        if (c["--protection"] is { } pr && pr is not ("balanced" or "strict")) return Loc.L("--protection: balanced oder strict", "--protection: balanced or strict");
        if (c["--profile"] is { } pf && Capture.RecordingModes.Find(pf) == null) return Loc.F("--profile: {0}", "--profile: {0}", Capture.RecordingModes.CliNames(Loc.Lang));
        if (c["--after-stop"] is { } af && af is not ("viewer" or "export" or "supportPackage" or "folder" or "none")) return Loc.L("--after-stop: viewer, export, supportPackage, folder oder none", "--after-stop: viewer, export, supportPackage, folder or none");
        if (c["--skip"] is { } sk)
            foreach (var k in sk.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (!Capture.RecordingOptions.Channels.Any(ch => ch.key == k)) return Loc.F("--skip: unbekannte Eingabeart „{0}“", "--skip: unknown input type \"{0}\"", k);
        foreach (var lf in c.All("--log")) if (string.IsNullOrWhiteSpace(lf) || lf.Length > 500) return Loc.L("--log: Pfad einer Protokolldatei (Platzhalter * und ? im Dateinamen)", "--log: path of a log file (wildcards * and ? in the file name)");
        foreach (var t in new[] { "--title", "--goal", "--expected", "--reference" })
            if (c[t] is { Length: > 2000 }) return Loc.F("{0} ist zu lang (höchstens 2000 Zeichen).", "{0} is too long (at most 2000 characters).", t);
        return null;
    }

    // ---------------- export ----------------

    static int Export(string[] a)
    {
        var c = CliArgs.Parse(a, 1, new[] { "--format", "--out", "--lang", "--importance", "--view", "--template", "--detail", "--image-profile", "--before", "--after", "--from", "--to", "--steps", "--md-dialect", "--gif-max-mb", "--page", "--seconds" },
            new[] { "--json", "--strict", "--zip", "--no-events", "--real-timing", "--no-images", "--no-ocr", "--readable", "--no-crop", "--no-after", "--front-matter", "--md-legacy", "--landscape", "--portrait" }, "--with");
        if (c.Error != null) return Usage(c.Error);
        if (NeedSession(c) is { } e) return Usage(e);
        // Bilder im KI-Paket (Vorher-Bild) und in Anleitungen (Nachher-Bild als „Ergebnis“) – deutsche und englische Werte
        string? before = (c["--before"] ?? "auto").ToLowerInvariant() switch { "immer" or "always" => "always", "nie" or "never" => "never", "auto" or "automatisch" => "auto", _ => null };
        if (before == null) return Usage(Loc.F("--before: auto, immer oder nie (nicht „{0}“)", "--before: auto, always or never (not \"{0}\")", c["--before"]));
        string? guideAfter = (c["--after"] ?? "never").ToLowerInvariant() switch { "nie" or "never" or "as-edited" => "never", "aenderung" or "changed" => "changed", "immer" or "always" => "always", _ => null };
        if (guideAfter == null) return Usage(Loc.F("--after: nie, aenderung oder immer (nicht „{0}“)", "--after: never, changed or always (not \"{0}\")", c["--after"]));
        var req = new ExportRequest
        {
            Format = (c["--format"] ?? "html").ToLowerInvariant(), Target = c["--out"], Language = c["--lang"],
            Importance = c["--importance"] ?? "primary", View = c["--view"], Template = c["--template"], GuideAfter = guideAfter,
            Detail = c["--detail"], ImageProfile = c["--image-profile"], Zip = c.Has("--zip"), IncludeEvents = !c.Has("--no-events"),
            Before = before, Crop = !c.Has("--no-crop"), After = !c.Has("--no-after"), Readable = c.Has("--readable"),
            RealTiming = c.Has("--real-timing"), Images = !c.Has("--no-images"), Ocr = !c.Has("--no-ocr"), With = c.All("--with"),
            MdDialect = c["--md-dialect"] ?? "plain", MdFrontMatter = c.Has("--front-matter"), MdLegacy = c.Has("--md-legacy")
        };
        if (req.MdDialect is not ("plain" or "github" or "obsidian")) return Usage("--md-dialect: plain | github | obsidian");
        // Seitenformat für Word und PDF (09 §4.4); ohne Angabe gilt die Einstellung der Aufnahme
        if (c["--page"] is { } pg) { req.Page = pg.ToLowerInvariant(); if (req.Page is not ("a4" or "letter")) return Usage("--page: a4 | letter"); }
        if (c.Has("--landscape") && c.Has("--portrait")) return Usage("--landscape | --portrait");
        if (c.Has("--landscape")) req.Landscape = true; else if (c.Has("--portrait")) req.Landscape = false;
        if (c["--gif-max-mb"] is { } gm) { if (!double.TryParse(gm, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var mb) || mb < 0.5 || mb > 1000) return Usage("--gif-max-mb: 0.5–1000"); req.GifMaxMb = mb; }
        if (c["--seconds"] is { } gs) { if (!double.TryParse(gs, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sec) || sec < 0.5 || sec > 20) return Usage("--seconds: 0.5–20"); req.GifSeconds = sec; }
        if (!Exporter.IsKnownFormat(req.Format)) return Usage(Loc.F("Unbekanntes Format: {0}", "Unknown format: {0}", req.Format));
        // Schrittbereich: --from/--to oder --steps a-b (Nummern der Schrittliste)
        if (c["--steps"] is { } range)
        {
            var m = System.Text.RegularExpressions.Regex.Match(range, @"^\s*(\d+)?\s*-\s*(\d+)?\s*$|^\s*(\d+)\s*$");
            if (!m.Success) return Usage(Loc.L("--steps: a-b, a- oder -b (Schrittnummern)", "--steps: a-b, a- or -b (step numbers)"));
            if (m.Groups[3].Success) req.FromStep = req.ToStep = int.Parse(m.Groups[3].Value);
            else { if (m.Groups[1].Success) req.FromStep = int.Parse(m.Groups[1].Value); if (m.Groups[2].Success) req.ToStep = int.Parse(m.Groups[2].Value); }
        }
        if (c["--from"] is { } fs) { if (!int.TryParse(fs, out var fv) || fv < 1) return Usage("--from: >= 1"); req.FromStep = fv; }
        if (c["--to"] is { } ts) { if (!int.TryParse(ts, out var tv) || tv < 1) return Usage("--to: >= 1"); req.ToStep = tv; }
        if (req.FromStep > req.ToStep) return Usage(Loc.L("--from liegt hinter --to.", "--from is after --to."));
        if (req.Language is { } l && l is not ("de" or "en")) return Usage("--lang: de | en");
        if (req.Importance is not ("primary" or "secondary" or "all")) return Usage("--importance: primary | secondary | all");
        bool json = c.Has("--json");
        using var view = SessionView.Open(S(c.Positional[0]), headless: true, lang: req.Language);
        if (view.Session.State == "recording") { Console.Error.WriteLine(Loc.L("Die Aufnahme ist nicht abgeschlossen.", "The recording is not finished.")); return 3; }
        if (c.Has("--strict") && view.UnreviewedCount > 0) { Console.Error.WriteLine(Loc.F("{0} auffällige Eingaben ungeprüft – Abbruch (--strict).", "{0} flagged inputs not reviewed – aborted (--strict).", view.UnreviewedCount)); return 9; }
        var quality = Exporter.IsGuideFormat(req.Format) ? QualityCheck.Run(view, new QualityOptions { Importance = req.Importance }) : new List<QualityFinding>();
        if (c.Has("--strict") && quality.Any(q => q.Severity == "warning" && !q.Ignored))
        {
            foreach (var q in quality.Where(q => q.Severity == "warning" && !q.Ignored)) Console.Error.WriteLine(q.Line(view.Language));
            Console.Error.WriteLine(Loc.L("Qualitätswarnungen – Abbruch (--strict).", "Quality warnings – aborted (--strict)."));
            return 9;
        }
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, ev) => { ev.Cancel = true; cts.Cancel(); };
        ExportResult r;
        using (ExportJob.Begin(null, cts.Token)) r = Exporter.Run(view, req);
        if (json)
            Console.WriteLine(Json.Line(new JsonObject
            {
                ["files"] = new JsonArray(r.Files.Select(f => (JsonNode)f).ToArray()), ["warnings"] = new JsonArray(r.Warnings.Select(f => (JsonNode)f).ToArray()),
                ["notices"] = new JsonArray(r.Notices.Select(f => (JsonNode)f).ToArray()), ["unreviewed"] = r.Unreviewed,
                ["estimatedTokens"] = new JsonObject { ["text"] = r.TextTokens, ["images"] = r.ImageTokens },
                ["quality"] = QualityCheck.ToJson(quality.Where(q => !q.Ignored), view.Language)
            }));
        else
        {
            Console.WriteLine(r.Path);
            foreach (var w in r.Warnings.Concat(r.Notices)) Console.Error.WriteLine(Loc.L("Hinweis: ", "Note: ") + w);
            foreach (var q in quality.Where(q => !q.Ignored)) Console.Error.WriteLine(q.Line(view.Language));
        }
        return 0;
    }

    // ---------------- check ----------------

    static int CheckCmd(string[] a)
    {
        var c = CliArgs.Parse(a, 1, new[] { "--lang", "--importance" }, new[] { "--json", "--strict", "--all" });
        if (c.Error != null) return Usage(c.Error);
        if (NeedSession(c) is { } e) return Usage(e);
        using var view = SessionView.Open(S(c.Positional[0]), headless: true, lang: c["--lang"]);
        var list = QualityCheck.Run(view, new QualityOptions { Importance = c["--importance"] ?? "primary", IncludeIgnored = c.Has("--all") });
        if (c.Has("--json")) Console.WriteLine(Json.Pretty(new JsonObject { ["findings"] = QualityCheck.ToJson(list, view.Language), ["warnings"] = list.Count(q => q.Severity == "warning" && !q.Ignored), ["hints"] = list.Count(q => q.Severity == "hint" && !q.Ignored) }));
        else
        {
            foreach (var q in list) Console.WriteLine(q.Line(view.Language));
            int warn = list.Count(q => q.Severity == "warning" && !q.Ignored), hint = list.Count(q => q.Severity == "hint" && !q.Ignored);
            // Befunde stehen in der Sprache von --lang (bzw. der Aufnahme) – die Summenzeile auch
            string lang = view.Language;
            Console.WriteLine((warn == 1 ? Loc.For(lang, "1 Warnung", "1 warning") : Loc.For(lang, $"{warn} Warnungen", $"{warn} warnings"))
                + " · " + (hint == 1 ? Loc.For(lang, "1 Hinweis", "1 hint") : Loc.For(lang, $"{hint} Hinweise", $"{hint} hints")));
        }
        return c.Has("--strict") && list.Any(q => q.Severity == "warning" && !q.Ignored) ? 9 : 0;
    }

    // ---------------- validate, compare, steps ----------------

    static int ValidateCmd(string[] a)
    {
        var c = CliArgs.Parse(a, 1, Array.Empty<string>(), new[] { "--json", "--thorough" });
        if (c.Error != null) return Usage(c.Error);
        if (NeedSession(c) is { } e) return Usage(e);
        var (issues, report) = Validator.Validate(S(c.Positional[0]), thorough: c.Has("--thorough"));
        if (c.Has("--json")) Console.WriteLine(Json.Pretty(report));
        else
        {
            var bk = report.Obj("bookkeeping")!; var ev = report.Obj("evidence")!;
            Console.WriteLine(Loc.F("Eingaben: {0} · zugeordnet {1} · ohne Schritt {2}", "Input events: {0} · assigned {1} · without step {2}", bk.Long("inputEvents"), bk.Long("assigned"), bk.Long("orphans")));
            Console.WriteLine(Loc.F("Aktionsschritte: {0} · belegt {1} · nur Eingabe {2} · unbekannt {3}", "Action steps: {0} · confirmed {1} · input only {2} · unknown {3}", ev.Long("actionSteps"), ev.Long("confirmed"), ev.Long("inputOnly"), ev.Long("unknown")));
            Console.WriteLine(Loc.F("Lücken: {0}", "Gaps: {0}", report.Obj("gaps").Long("count")));
            foreach (var i in issues) Console.WriteLine($"{i.Severity.ToUpperInvariant(),-7} {i.Code} {i.Message}{(i.Seq != null ? $" (seq {i.Seq})" : "")}");
            Console.WriteLine(issues.Any(i => i.Severity == "error") ? Loc.L("Ergebnis: FEHLER", "Result: ERROR") : Loc.L("Ergebnis: OK", "Result: OK"));
        }
        return issues.Any(i => i.Severity == "error") ? 4 : 0;
    }

    static int CompareCmd(string[] a)
    {
        var c = CliArgs.Parse(a, 1, new[] { "--format", "--out", "--threshold" }, new[] { "--images" });
        if (c.Error != null) return Usage(c.Error);
        if (c.Positional.Count != 2) return Usage(Loc.L("Zwei Aufnahmen angeben.", "Specify two recordings."));
        string fmt = c["--format"] ?? "md";
        if (fmt is not ("md" or "json")) return Usage("--format: md | json");
        var io = new ImageDiffOptions();
        if (c["--threshold"] is { } th) { if (!int.TryParse(th, out int tv) || tv < 1 || tv > 255) return Usage("--threshold: 1-255"); io.PixelThreshold = tv; }
        bool images = c.Has("--images");
        using var va = SessionView.Open(S(c.Positional[0]), headless: true);
        using var vb = SessionView.Open(S(c.Positional[1]), headless: true);
        var r = StepCompare.Compare(va, vb, images: images, imageOptions: io);
        if (images && fmt == "md" && c["--out"] is { } mdOut)
        {
            foreach (var f in StepCompare.WriteMarkdownWithImages(r, va, vb, mdOut, va.Language)) Console.WriteLine(f);
            return 0;
        }
        string text = fmt == "json" ? Json.Pretty(r) : StepCompare.Markdown(r, va.Language);
        if (c["--out"] is { } o) { File.WriteAllText(o, text); Console.WriteLine(o); } else Console.WriteLine(text);
        return 0;
    }

    static int StepsCmd(string[] a)
    {
        var c = CliArgs.Parse(a, 1, new[] { "--lang" }, new[] { "--json", "--all" });
        if (c.Error != null) return Usage(c.Error);
        if (NeedSession(c) is { } e) return Usage(e);
        using var view = SessionView.Open(S(c.Positional[0]), headless: true, lang: c["--lang"]);
        var steps = view.All.Where(x => c.Has("--all") || x.Importance != "trace").ToList();
        if (c.Has("--json"))
        {
            Console.WriteLine(Json.Line(new JsonObject
            {
                ["sessionId"] = view.Session.SessionId, ["title"] = view.Title,
                ["steps"] = new JsonArray(steps.Select(s => (JsonNode)new JsonObject
                {
                    ["number"] = s.Number, ["id"] = s.Id, ["t"] = Fmt.Time(s.TFrom, "en"), ["kind"] = s.Kind, ["importance"] = s.Importance,
                    ["title"] = s.Title, ["text"] = s.Protocol, ["evidence"] = s.EvidenceLevel, ["markerKind"] = s.Kind == "marker" ? s.Json.Obj("input")?.Str("markerKind") : null,
                    ["timing"] = s.Redacted ? null : Timing.Of(s.Json)
                }.Clean()).ToArray())
            }));
            return 0;
        }
        foreach (var s in steps)
        {
            string timing = !s.Redacted && Timing.Of(s.Json) is { } t ? "  (" + Timing.Text(t, view.Language) + ")" : "";
            Console.WriteLine($"{s.Number,4}. {Fmt.Time(s.TFrom, view.Language)}  {s.Kind,-12} {s.Protocol}  [{s.EvidenceLevel}]{timing}");
        }
        return 0;
    }

    // ---------------- testability ----------------

    /// <summary>Testbarkeits-Bericht (Paket 16): welche benutzten Bedienelemente keine AutomationId oder keinen eindeutigen Namen haben.</summary>
    static int TestabilityCmd(string[] a)
    {
        var c = CliArgs.Parse(a, 1, new[] { "--lang" }, new[] { "--json", "--strict" });
        if (c.Error != null) return Usage(c.Error);
        if (NeedSession(c) is { } e) return Usage(e);
        if (c["--lang"] is { } l && l is not ("de" or "en")) return Usage("--lang: de | en");
        using var view = SessionView.Open(S(c.Positional[0]), headless: true, lang: c["--lang"]);
        var list = TestCodeGen.Testability(view, view.Language);
        int warnings = list.Count(f => f.Str("severity") == "warning");
        if (c.Has("--json")) Console.WriteLine(Json.Pretty(new JsonObject { ["findings"] = new JsonArray(list.Select(f => (JsonNode)f).ToArray()), ["warnings"] = warnings, ["hints"] = list.Count - warnings }));
        else Console.WriteLine(TestCodeGen.TestabilityMarkdown(view, list, view.Language));
        return c.Has("--strict") && warnings > 0 ? 9 : 0;
    }

    // ---------------- list ----------------

    static int ListCmd(string[] a)
    {
        var c = CliArgs.Parse(a, 1, new[] { "--limit", "--root" }, new[] { "--json", "--all" });
        if (c.Error != null) return Usage(c.Error);
        if (c.Positional.Count > 0) return Usage(Loc.F("Unerwartete Angabe: {0}", "Unexpected argument: {0}", c.Positional[0]));
        int limit = 20;
        if (c["--limit"] is { } ls && (!int.TryParse(ls, out limit) || limit < 1)) return Usage("--limit: >= 1");
        string root = c["--root"] ?? AppPaths.DefaultSessionsFolder();
        if (!Directory.Exists(root)) { Console.Error.WriteLine(Loc.F("Aufnahmeordner nicht gefunden: {0}", "Recordings folder not found: {0}", root)); return 3; }
        var list = SessionCatalog.Scan(root, includeArchives: c.Has("--all")).Where(s => s.Error == null).OrderByDescending(s => s.StartUtc).Take(limit).ToList();
        if (c.Has("--json"))
        {
            Console.WriteLine(Json.Line(new JsonObject
            {
                ["root"] = root,
                ["sessions"] = new JsonArray(list.Select(s => (JsonNode)new JsonObject
                {
                    ["id"] = s.Id, ["title"] = s.Title, ["startedAt"] = s.StartUtc.ToString("o"), ["durationMs"] = s.DurationUs / 1000, ["state"] = s.State,
                    ["steps"] = s.Steps, ["primarySteps"] = s.PrimarySteps, ["profile"] = s.ProfileId, ["path"] = s.Path, ["archive"] = s.Archive
                }).ToArray())
            }));
            return 0;
        }
        string stepsWord = Loc.L("Schritte", "steps");
        foreach (var s in list)
            Console.WriteLine($"{s.StartUtc.ToLocalTime():yyyy-MM-dd HH:mm}  {s.Steps,4} {stepsWord}  {s.Title}  {s.Path}");
        if (list.Count == 0) Console.WriteLine(Loc.L("Keine Aufnahmen.", "No recordings."));
        return 0;
    }

    // ---------------- settings ----------------

    static int SettingsCmd(string[] a)
    {
        if (a.Length < 2) return Usage(Loc.L("settings export | import | reset", "settings export | import | reset"));
        string sub = a[1].ToLowerInvariant();
        var c = CliArgs.Parse(a, 2, new[] { "--parts" }, new[] { "--confirm", "--json", "--overwrite", "--templates" }, "--allow-weaker");
        if (c.Error != null) return Usage(c.Error);
        var parts = c["--parts"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (parts != null && parts.FirstOrDefault(p => !SettingsExchange.Parts.Contains(p)) is { } badPart) return Usage(Loc.F("Unbekannter Teil: {0}", "Unknown part: {0}", badPart));
        var current = AppSettings.Load();
        switch (sub)
        {
            case "export":
                {
                    if (c.Positional.Count != 1) return Usage(Loc.L("Zieldatei fehlt.", "Target file missing."));
                    string path = Path.GetFullPath(c.Positional[0]);
                    if (File.Exists(path) && !c.Has("--overwrite")) { Console.Error.WriteLine(Loc.F("Datei existiert bereits (--overwrite): {0}", "File already exists (--overwrite): {0}", path)); return 8; }
                    try { File.WriteAllText(path, Json.Pretty(SettingsExchange.Export(current, parts))); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Console.Error.WriteLine(ex.Message); return 8; }
                    Console.WriteLine(path);
                    return 0;
                }
            case "import" or "reset":
                {
                    ImportPlan plan;
                    if (sub == "import")
                    {
                        if (c.Positional.Count != 1) return Usage(Loc.L("Datei fehlt.", "File missing."));
                        if (!File.Exists(c.Positional[0])) { Console.Error.WriteLine(Loc.F("Datei nicht gefunden: {0}", "File not found: {0}", c.Positional[0])); return 3; }
                        plan = SettingsExchange.Plan(File.ReadAllText(c.Positional[0]), current, parts);
                    }
                    else plan = SettingsExchange.PlanReset(current, c.Has("--templates"));
                    var allowed = c.All("--allow-weaker").ToHashSet();
                    if (c.Has("--json"))
                        Console.WriteLine(Json.Pretty(new JsonObject
                        {
                            ["ok"] = plan.Ok, ["errors"] = new JsonArray(plan.Errors.Select(x => (JsonNode)x).ToArray()), ["skipped"] = new JsonArray(plan.SkippedParts.Select(x => (JsonNode)x).ToArray()),
                            ["changes"] = new JsonArray(plan.Changes.Select(x => (JsonNode)new JsonObject { ["part"] = x.Part, ["key"] = x.Key, ["text"] = x.Text, ["weakening"] = x.WeakeningId, ["allowed"] = x.WeakeningId != null ? allowed.Contains(x.WeakeningId) : null }.Clean()).ToArray()),
                            ["applied"] = c.Has("--confirm") && plan.Ok
                        }));
                    else
                    {
                        foreach (var er in plan.Errors) Console.Error.WriteLine(Loc.L("Fehler: ", "Error: ") + er);
                        foreach (var sk in plan.SkippedParts) Console.WriteLine(Loc.L("Übersprungen: ", "Skipped: ") + sk);
                        foreach (var ch in plan.Changes) Console.WriteLine((ch.Weakens ? (allowed.Contains(ch.WeakeningId!) ? "  ! " : "  ✗ ") : "  · ") + ch.Text + (ch.Weakens ? $"  [{ch.WeakeningId}]" : ""));
                        if (plan.Changes.Count == 0 && plan.Ok) Console.WriteLine(Loc.L("Keine Änderungen.", "No changes."));
                        if (plan.Weakening.Any(w => !allowed.Contains(w.WeakeningId!)))
                            Console.WriteLine(Loc.L("Schwächende Änderungen (✗) bleiben unverändert, solange sie nicht einzeln mit --allow-weaker <Kennung> erlaubt werden.", "Weakening changes (✗) are not applied unless each is allowed with --allow-weaker <id>."));
                    }
                    if (!plan.Ok) return sub == "import" ? 4 : 1;
                    if (!c.Has("--confirm")) { if (!c.Has("--json")) Console.WriteLine(Loc.L("Vorschau – zum Anwenden --confirm angeben.", "Preview – add --confirm to apply.")); return 0; }
                    if (Ui.SingleInstance.IsUiRunning()) { Console.Error.WriteLine(Loc.L("Das Programm läuft – bitte erst schließen oder in den Einstellungen importieren.", "The program is running – close it first or import in the settings.")); return 7; }
                    var next = SettingsExchange.Apply(plan, current, allowed);
                    if (!SettingsExchange.Commit(next, AppPaths.SettingsFile)) { Console.Error.WriteLine(next.LastSaveError); return 8; }
                    if (!c.Has("--json")) Console.WriteLine(Loc.L("Übernommen. Rückgängig: Einstellungen → „Letzten Import zurücknehmen“.", "Applied. Undo: Settings → \"Undo last import\"."));
                    return 0;
                }
        }
        return Usage(Loc.L("settings export | import | reset", "settings export | import | reset"));
    }

    // ---------------- record --self-test ----------------

    static int SelfTest(string[] a)
    {
        bool json = a.Contains("--json");
        var results = new List<(string check, string level, string text)>();
        void R(string check, string level, string text) => results.Add((check, level, text));
        if (Ui.SingleInstance.IsUiRunning() && Ui.SingleInstance.IsRecording()) { Console.Error.WriteLine(Loc.L("Es läuft eine Aufnahme – Selbsttest später wiederholen.", "A recording is in progress – run the self-test later.")); return 6; }

        // 1 Bildaufnahme je Monitor
        bool captureOk = true;
        foreach (var m in Native.ScreenGrabber.GetMonitors())
        {
            using var bmp = Native.ScreenGrabber.TryCapture(new System.Drawing.Rectangle(m.Bounds.X, m.Bounds.Y, 64, 64), out int err);
            if (bmp == null) { captureOk = false; R("capture", "error", Loc.F("Monitor M{0}: Bildaufnahme fehlgeschlagen (Fehler {1})", "Monitor M{0}: capture failed (error {1})", m.Index + 1, err)); }
            else R("capture", "ok", Loc.F("Monitor M{0}: {1} × {2}, {3} %", "Monitor M{0}: {1} × {2}, {3} %", m.Index + 1, m.Bounds.Width, m.Bounds.Height, (int)Math.Round(m.Dpi / 96.0 * 100)));
        }
        // 2 Unsichtbarkeit
        bool invis = Ui.Invisibility.SelfTest();
        bool fallbackHidden = AppSettings.Load().InvisibleFallback == "hidden";
        R("invisibility", invis ? "ok" : fallbackHidden ? "warning" : "error",
            invis ? Loc.L("Eigene Fenster erscheinen nicht in Bildschirmfotos", "Own windows are excluded from screenshots")
                  : Loc.F("Unsichtbarkeit nicht bestätigt ({0}) – Aufnahme nur mit ausgeblendeter Anzeige", "Invisibility not confirmed ({0}) – recording only with hidden display", Ui.Invisibility.LastFailure ?? "?"));
        // 3 UI Automation
        var uia = Task.Run(() => { try { return System.Windows.Automation.AutomationElement.RootElement.Current.ClassName ?? ""; } catch { return null; } });
        bool uiaOk = uia.Wait(2000) && uia.Result != null;
        R("uia", uiaOk ? "ok" : "warning", uiaOk ? Loc.L("UI Automation antwortet", "UI Automation responds") : Loc.L("UI Automation antwortet nicht – Bedienelemente werden nicht benannt", "UI Automation does not respond – controls will not be named"));
        // 4 Aufnahmeordner
        string folder = AppPaths.DefaultSessionsFolder();
        string probe = folder; while (!Directory.Exists(probe) && Path.GetDirectoryName(probe) is { } parent && parent != probe) probe = parent;
        bool writable = Directory.Exists(probe) && AppPaths.IsWritable(probe);
        R("folder", writable ? "ok" : "error", Loc.F("Aufnahmeordner {0}: {1}", "Recordings folder {0}: {1}", folder, writable ? Loc.L("beschreibbar", "writable") : Loc.L("nicht beschreibbar", "not writable")));
        try
        {
            var free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(probe))!).AvailableFreeSpace;
            R("space", free < 2L * 1024 * 1024 * 1024 ? "warning" : "ok", Loc.F("Freier Speicher: {0}", "Free space: {0}", Loc.Bytes(free)));
        }
        catch { }
        if (AppPaths.LooksSynced(probe)) R("sync", "warning", Loc.L("Aufnahmeordner liegt in einem synchronisierten Ordner (OneDrive, Nextcloud …)", "Recordings folder is inside a synced folder (OneDrive, Nextcloud …)"));
        // 5 Tastenkürzel
        if (Ui.SingleInstance.IsUiRunning()) R("hotkeys", "info", Loc.L("Programm läuft – Prüfung der Tastenkürzel übersprungen", "Program is running – shortcut check skipped"));
        else
            foreach (var (id, combo) in AppSettings.Load().EffectiveHotkeys().Where(kv => kv.Value.Length > 0))
                if (!Ui.Hotkeys.Probe(combo)) R("hotkeys", "warning", Loc.F("{0} ({1}) ist von einem anderen Programm belegt", "{0} ({1}) is taken by another program", Loc.Keys(combo), HotkeyActions.Label(HotkeyActions.ById(id)!)));
        if (!results.Any(r => r.check == "hotkeys")) R("hotkeys", "ok", Loc.L("Tastenkürzel frei", "Shortcuts available"));

        if (json)
            Console.WriteLine(Json.Pretty(new JsonObject { ["results"] = new JsonArray(results.Select(r => (JsonNode)new JsonObject { ["check"] = r.check, ["level"] = r.level, ["text"] = r.text }).ToArray()) }));
        else
            foreach (var (_, level, text) in results)
                Console.WriteLine($"{level switch { "ok" => "OK     ", "warning" => Loc.L("WARNUNG", "WARNING"), "error" => Loc.L("FEHLER ", "ERROR  "), _ => "INFO   " }} {text}");
        return !captureOk || (!invis && !fallbackHidden) || !writable ? 7 : 0;
    }
}
