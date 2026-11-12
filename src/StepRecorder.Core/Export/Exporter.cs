using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Export;

/// <summary>Ein Exportauftrag – dieselbe Beschreibung für Exportdialog, Ein-Klick-Export und Kommandozeile.</summary>
public sealed class ExportRequest
{
    public string Format { get; set; } = "html";
    /// <summary>Datei- oder Ordnerziel; null = Standardort in der Aufnahme (export\…).</summary>
    public string? Target { get; set; }
    public string? Language { get; set; }
    public string Importance { get; set; } = "primary";
    public string? View { get; set; }
    public bool? Overview { get; set; }
    public double GifSeconds { get; set; } = 2.5;
    public bool MouseMarks { get; set; } = true;
    public bool KeyMarks { get; set; } = true;
    /// <summary>Art der Anleitung: standard | short | checklist.</summary>
    public string? Template { get; set; }
    /// <summary>Fehlt die Übersetzung eines Autorentexts: auto (automatischer Text) | other (Text der anderen Sprache).</summary>
    public string? MissingTranslation { get; set; }
    public string? TourHints { get; set; }
    public bool PptxNotes { get; set; } = true;
    // KI-Paket
    public string? Detail { get; set; }
    public string? ImageProfile { get; set; }
    public bool Zip { get; set; }
    public bool IncludeEvents { get; set; } = true;
    // AutoHotkey
    public bool RealTiming { get; set; }
    public bool Images { get; set; } = true;
    public bool AhkKi { get; set; }
    public bool Ocr { get; set; } = true;
    public List<string> With { get; set; } = new();

    public JsonObject ToJson() => new JsonObject
    {
        ["format"] = Format, ["target"] = Target, ["language"] = Language, ["importance"] = Importance, ["view"] = View, ["overview"] = Overview,
        ["gifSeconds"] = GifSeconds, ["mouseMarks"] = MouseMarks, ["keyMarks"] = KeyMarks, ["template"] = Template, ["missingTranslation"] = MissingTranslation,
        ["tourHints"] = TourHints, ["pptxNotes"] = PptxNotes, ["detail"] = Detail, ["imageProfile"] = ImageProfile, ["zip"] = Zip, ["includeEvents"] = IncludeEvents,
        ["realTiming"] = RealTiming, ["images"] = Images, ["ahkKi"] = AhkKi, ["ocr"] = Ocr,
    }.Clean();

    public static ExportRequest FromJson(JsonObject? o)
    {
        var r = new ExportRequest();
        if (o == null) return r;
        r.Format = o.Str("format") ?? r.Format; r.Target = o.Str("target"); r.Language = o.Str("language"); r.Importance = o.Str("importance") ?? r.Importance;
        r.View = o.Str("view"); if (o["overview"] != null) r.Overview = o.Bool("overview");
        r.GifSeconds = o.Dbl("gifSeconds", r.GifSeconds); if (o["mouseMarks"] != null) r.MouseMarks = o.Bool("mouseMarks"); if (o["keyMarks"] != null) r.KeyMarks = o.Bool("keyMarks");
        r.Template = o.Str("template"); r.MissingTranslation = o.Str("missingTranslation"); r.TourHints = o.Str("tourHints"); if (o["pptxNotes"] != null) r.PptxNotes = o.Bool("pptxNotes");
        r.Detail = o.Str("detail"); r.ImageProfile = o.Str("imageProfile"); r.Zip = o.Bool("zip"); if (o["includeEvents"] != null) r.IncludeEvents = o.Bool("includeEvents");
        r.RealTiming = o.Bool("realTiming"); if (o["images"] != null) r.Images = o.Bool("images"); r.AhkKi = o.Bool("ahkKi"); if (o["ocr"] != null) r.Ocr = o.Bool("ocr");
        return r;
    }

    public ExportRequest Clone() => FromJson(ToJson());
}

/// <summary>
/// Führt Exporte aus: Ziel bestimmen, in einem Arbeitsordner erzeugen, erst am Ende an das Ziel verschieben –
/// ein Abbruch oder Fehler hinterlässt nichts Halbes, und nie wird ein fremder Ordner geleert.
/// </summary>
public static class Exporter
{
    static readonly HashSet<string> Guide = new() { "html", "md", "markdown", "docx", "pdf", "gif", "pptx", "tour", "bugreport" };
    static readonly HashSet<string> Known = new(Guide) { "ki", "ahk", "autohotkey", "ahk-ki", "ahkki" };

    /// <summary>Formate, die <see cref="GuideExport"/> wirklich erzeugen kann (wird mit neuen Formaten erweitert).</summary>
    public static Func<string, bool> GuideSupports { get; set; } = f => f is "html" or "md" or "docx" or "pdf" or "gif";

    public static string Normalize(string format) => format.ToLowerInvariant() switch { "markdown" => "md", "autohotkey" => "ahk", "ahkki" => "ahk-ki", var f => f };
    public static bool IsKnownFormat(string format) { var f = Normalize(format); return Known.Contains(f) && (!Guide.Contains(f) || GuideSupports(f)); }
    public static bool IsGuideFormat(string format) => Guide.Contains(Normalize(format));
    /// <summary>Formate, die einen eigenen Ordner erzeugen (statt einer Datei).</summary>
    public static bool IsFolderOutput(string format) => Normalize(format) is "ki" or "ahk-ki";

    public static string Extension(string format) => Normalize(format) switch
    {
        "md" => "md", "docx" => "docx", "pdf" => "pdf", "gif" => "gif", "pptx" => "pptx", "bugreport" => "md", "ahk" => "ahk", _ => "html"
    };

    /// <summary>Unterordner unter <c>export\</c> der Aufnahme.</summary>
    public static string SubFolder(string format) => Normalize(format) switch
    {
        "md" => "markdown", "bugreport" => "fehlerbericht", "tour" => "tour", "ahk" => "ahk", "ahk-ki" => "ahk-ki", "ki" => "", var f => f
    };

    /// <summary>Ordner, in den Exporte dieser Aufnahme standardmäßig gehen (bei .steprec-Archiven: daneben).</summary>
    public static string SessionExportDir(SessionView view)
        => view.Session.ReadOnly ? Path.GetDirectoryName(view.Session.Path) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Path.Combine(view.Session.Path, "export");

    static string Title(SessionView view, string fallback = "Anleitung") => Fmt.FileSafe(view.Title.Length > 0 ? view.Title : fallback);

    /// <summary>
    /// Vollständiges Ziel. <paramref name="lastFolder"/>: zuletzt gewählter Ordner für dieses Format (null = Standardort in der Aufnahme).
    /// Markdown und AutoHotkey bekommen in fremden Ordnern einen eigenen Unterordner, damit „bilder“ nicht mit anderem vermischt wird.
    /// </summary>
    public static string DefaultTarget(SessionView view, string format, string? lastFolder, string? template = null, string? lang = null)
    {
        string f = Normalize(format), title = Title(view, f is "ahk" or "ahk-ki" ? "Ablauf" : "Anleitung");
        string suffix = f is "html" or "md" or "docx" or "pdf" ? GuidePresets.FileSuffix(template, lang ?? view.Language) : "";
        if (lastFolder != null)
        {
            return f switch
            {
                "ki" => Path.Combine(lastFolder, "ki-paket-" + title),
                "ahk-ki" => Path.Combine(lastFolder, title + "-ahk-ki"),
                "md" or "ahk" or "bugreport" => Path.Combine(lastFolder, title + (f == "bugreport" ? "-fehlerbericht" : ""), title + "." + Extension(f)),
                _ => Path.Combine(lastFolder, title + suffix + "." + Extension(f))
            };
        }
        string root = SessionExportDir(view);
        return f switch
        {
            "ki" => Path.Combine(root, $"ki-paket-{DateTime.Now:yyyyMMdd-HHmmss}"),
            "ahk-ki" => Path.Combine(root, "ahk-ki", title),
            _ => Path.Combine(root, SubFolder(f), title + suffix + "." + Extension(f))
        };
    }

    /// <summary>Ziel eines Auftrags auflösen (Ordnerangabe bei Dateiformaten → Titel als Dateiname).</summary>
    public static string ResolveTarget(SessionView view, ExportRequest r)
    {
        string f = Normalize(r.Format);
        if (string.IsNullOrWhiteSpace(r.Target)) return DefaultTarget(view, f, null, r.Template, r.Language);
        string t = Path.GetFullPath(r.Target);
        if (!IsFolderOutput(f) && (Directory.Exists(t) || r.Target.EndsWith('\\') || r.Target.EndsWith('/')))
            t = Path.Combine(t, Title(view) + "." + Extension(f));
        return t;
    }

    /// <summary>Export ausführen. Fortschritt und Abbruch über einen umgebenden <see cref="ExportJob"/>.</summary>
    public static ExportResult Run(SessionView view, ExportRequest r)
    {
        string f = Normalize(r.Format);
        if (!IsKnownFormat(f)) throw new ArgumentException("Unbekanntes Format: " + r.Format);
        string final = ResolveTarget(view, r);
        bool folder = IsFolderOutput(f);
        if (folder) ExportFolders.CheckOwnable(final, f == "ki" ? "ai-package.json" : "ablauf.json");
        string tmpRoot = view.Session.ReadOnly
            ? Path.Combine(Path.GetDirectoryName(final)!, ".steprec-tmp-" + Guid.NewGuid().ToString("N")[..8])
            : Path.Combine(view.Session.Path, "export", ".tmp", "job-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tmpRoot);
        try
        {
            string work = Path.Combine(tmpRoot, Path.GetFileName(final));
            var res = RunTo(view, r, f, work);
            ExportJob.ThrowIfCancelled();
            Commit(tmpRoot, work, final, folder, f == "ki" && r.Zip);
            return Rebase(res, tmpRoot, Path.GetDirectoryName(final)!);
        }
        finally { try { Directory.Delete(tmpRoot, true); } catch { } }
    }

    static ExportResult RunTo(SessionView view, ExportRequest r, string f, string work)
    {
        switch (f)
        {
            case "ki":
                return KiPackage.Export(view, new KiOptions
                {
                    Detail = r.Detail switch { "kompakt" or "compact" => "compact", "vollstaendig" or "vollständig" or "full" => "full", _ => "standard" },
                    ImageProfile = r.ImageProfile switch { "hochaufloesend" or "hochauflösend" or "highres" => "highres", "original" => "original", _ => "compatible" },
                    OutDir = work, Zip = r.Zip, IncludeEvents = r.IncludeEvents, Language = r.Language, View = r.View ?? "screen"
                });
            case "ahk" when r.AhkKi:
            case "ahk-ki":
                return AhkKiPackage.Export(view, new AhkKiOptions { OutDir = work, With = r.With, Ocr = r.Ocr });
            case "ahk":
                return AhkExport.Export(view, new AhkOptions { OutPath = work, RealTiming = r.RealTiming, Images = r.Images });
            default:
                return GuideExport.Export(view, new GuideOptions
                {
                    Format = f, OutPath = work, Language = r.Language, Importance = r.Importance, View = r.View, Overview = r.Overview,
                    GifSeconds = r.GifSeconds, MouseMarks = r.MouseMarks, KeyMarks = r.KeyMarks, Preset = r.Template, MissingTranslation = r.MissingTranslation,
                    TourHints = r.TourHints, PptxNotes = r.PptxNotes
                });
        }
    }

    /// <summary>Ergebnis vom Arbeitsordner an das Ziel bringen.</summary>
    static void Commit(string tmpRoot, string work, string final, bool folder, bool zip)
    {
        string finalDir = Path.GetDirectoryName(final)!;
        Directory.CreateDirectory(finalDir);
        if (folder)
        {
            if (Directory.Exists(final)) Directory.Delete(final, true); // CheckOwnable hat geprüft: leer oder früherer Export
            MoveDir(work, final);
            if (zip && File.Exists(work + ".zip")) File.Move(work + ".zip", final + ".zip", true);
            return;
        }
        File.Move(work, final, true);
        // Begleitordner (Markdown- und AutoHotkey-Bilder): Dateien zusammenführen, fremde Dateien bleiben
        foreach (var dir in Directory.GetDirectories(tmpRoot))
        {
            string target = Path.Combine(finalDir, Path.GetFileName(dir));
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                string dest = Path.Combine(target, Path.GetRelativePath(dir, file));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Move(file, dest, true);
            }
        }
    }

    static void MoveDir(string src, string dst)
    {
        try { Directory.Move(src, dst); return; }
        catch (IOException) { }
        // anderes Laufwerk: kopieren
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            string dest = Path.Combine(dst, Path.GetRelativePath(src, file));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, true);
        }
        Directory.CreateDirectory(dst);
    }

    static ExportResult Rebase(ExportResult r, string tmpRoot, string finalDir)
    {
        string Map(string p) => p.StartsWith(tmpRoot, StringComparison.OrdinalIgnoreCase) ? Path.Combine(finalDir, Path.GetRelativePath(tmpRoot, p)) : p;
        var res = new ExportResult { Unreviewed = r.Unreviewed, TextTokens = r.TextTokens, ImageTokens = r.ImageTokens, Path = Map(r.Path), Text = r.Text };
        res.Files.AddRange(r.Files.Select(Map));
        res.Warnings.AddRange(r.Warnings); res.Notices.AddRange(r.Notices);
        return res;
    }

    /// <summary>Alte Arbeitsordner (abgebrochene oder abgestürzte Exporte) aufräumen.</summary>
    public static void CleanStaleJobs(string sessionFolder)
    {
        try
        {
            var dir = Path.Combine(sessionFolder, "export", ".tmp");
            if (!Directory.Exists(dir)) return;
            foreach (var d in Directory.GetDirectories(dir, "job-*"))
                if (Directory.GetLastWriteTimeUtc(d) < DateTime.UtcNow.AddDays(-1)) try { Directory.Delete(d, true); } catch { }
        }
        catch { }
    }
}
