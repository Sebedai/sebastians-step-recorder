using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.Steps;

namespace StepRecorder.ReadLayer;

/// <summary>Kurzbeschreibung einer Aufnahme für Listen (aus Manifest und Overlay, ohne Schritte zu bauen).</summary>
public sealed class SessionInfo
{
    public string Path = "";
    public string Id = "";
    public string Title = "";
    public DateTime StartUtc;
    public long DurationUs;
    public int Steps, PrimarySteps;
    public string ProfileId = "";
    /// <summary>complete | recording | recovered | unreadable</summary>
    public string State = "complete";
    /// <summary><c>.steprec</c>-Archiv (nur lesend).</summary>
    public bool Archive;
    public bool Edited;
    public DateTime LastWriteUtc;
    public string? Error;
    public bool Recording => State == "recording";
}

/// <summary>Teure Angaben (Durchsicht), zwischengespeichert in <c>cache\catalog.json</c>.</summary>
public sealed record SessionDetails(int Unreviewed, int GuideSteps);

/// <summary>
/// Findet und beschreibt Aufnahmen unter einem Ordner (Bibliothek, Startfenster, MCP). Vorschaubilder entstehen über
/// die Leseschicht – mit Schwärzungen – und werden unter <c>cache\thumbs\</c> zwischengespeichert.
/// </summary>
public static class SessionCatalog
{
    /// <summary>
    /// Sitzungsangabe der Kommandozeile auflösen: <c>latest</c> (auch <c>neueste</c>) = die zuletzt begonnene abgeschlossene
    /// Aufnahme im Aufnahmeordner <paramref name="root"/>; sonst der Pfad unverändert.
    /// </summary>
    public static string Resolve(string arg, string root)
    {
        if (!arg.Equals("latest", StringComparison.OrdinalIgnoreCase) && !arg.Equals("neueste", StringComparison.OrdinalIgnoreCase)) return arg;
        return Latest(root)?.Path ?? throw new DirectoryNotFoundException(root);
    }

    /// <summary>Zuletzt begonnene, abgeschlossene Aufnahme (ohne Archive); null, wenn es keine gibt.</summary>
    public static SessionInfo? Latest(string root)
        => Scan(root, includeArchives: false).Where(s => s.Error == null && !s.Recording).OrderByDescending(s => s.StartUtc).FirstOrDefault();

    /// <summary>Aufnahmeordner (und optional <c>.steprec</c>-Dateien) bis <paramref name="maxDepth"/> Ebenen tief; folgt keinen Verknüpfungen.</summary>
    public static List<SessionInfo> Scan(string root, int maxDepth = 3, bool includeArchives = true, CancellationToken ct = default)
    {
        var list = new List<SessionInfo>();
        if (!Directory.Exists(root)) return list;
        void Walk(string dir, int depth)
        {
            if (depth > maxDepth) return;
            ct.ThrowIfCancellationRequested();
            IEnumerable<string> dirs;
            try { dirs = Directory.EnumerateDirectories(dir).ToList(); } catch { return; }
            foreach (var d in dirs)
            {
                try
                {
                    var di = new DirectoryInfo(d);
                    if ((di.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if (File.Exists(System.IO.Path.Combine(d, "manifest.json"))) { list.Add(Read(d)); continue; }
                    if (di.Name is "export" or "cache" or "frames" or "history" || di.Name.StartsWith('.')) continue;
                    Walk(d, depth + 1);
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }
            if (includeArchives)
                try { foreach (var f in Directory.EnumerateFiles(dir, "*.steprec")) list.Add(Read(f)); } catch { }
        }
        Walk(root, 1);
        return list;
    }

    /// <summary>Liest Manifest und Overlay-Titel; Fehler landen in <see cref="SessionInfo.Error"/>, nie als Ausnahme.</summary>
    public static SessionInfo Read(string path)
    {
        var info = new SessionInfo { Path = path, Title = System.IO.Path.GetFileNameWithoutExtension(path.TrimEnd('\\', '/')) };
        try
        {
            info.Archive = File.Exists(path);
            JsonObject? man, edits;
            if (info.Archive)
            {
                using var s = Session.Open(path, recover: false);
                man = s.Manifest; edits = s.LoadEdits();
                info.LastWriteUtc = File.GetLastWriteTimeUtc(path);
            }
            else
            {
                man = Json.ReadObject(System.IO.Path.Combine(path, "manifest.json"));
                var ep = System.IO.Path.Combine(path, "edits.json");
                edits = File.Exists(ep) ? Json.ReadObject(ep) : null;
                info.LastWriteUtc = Directory.GetLastWriteTimeUtc(path);
                if (edits != null) info.LastWriteUtc = Max(info.LastWriteUtc, File.GetLastWriteTimeUtc(ep));
            }
            if (man == null) { info.State = "unreadable"; info.Error = "manifest.json"; return info; }
            info.Id = man.Str("sessionId") ?? "";
            info.Title = edits?.Obj("guide")?.Str("title") is { Length: > 0 } et ? et : man.Str("title") is { Length: > 0 } mt ? mt : info.Title;
            try { info.StartUtc = Json.ParseUtc(man.Str("createdUtc")); } catch { }
            info.DurationUs = man.Long("durationUs");
            info.Steps = (int)man.Obj("summary").Long("steps");
            info.PrimarySteps = (int)man.Obj("summary").Long("primarySteps");
            info.ProfileId = man.Obj("profile")?.Str("id") ?? "";
            info.State = man.Str("state") ?? "complete";
            if (info.State == "recording" && !info.Archive && !Session.IsLockAlive(path)) info.State = "recovered";
            info.Edited = edits != null && edits.Long("revision") > 0;
        }
        catch (Exception ex) { info.State = "unreadable"; info.Error = ex.Message; }
        return info;
    }

    static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    /// <summary>Belegter Platz; <paramref name="exportsOnly"/>: nur der Ordner <c>export</c>.</summary>
    public static long SizeOnDisk(string path, bool exportsOnly = false)
    {
        try
        {
            if (File.Exists(path)) return exportsOnly ? 0 : new FileInfo(path).Length;
            var dir = exportsOnly ? System.IO.Path.Combine(path, "export") : path;
            if (!Directory.Exists(dir)) return 0;
            return new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => { try { return f.Length; } catch { return 0L; } });
        }
        catch { return 0; }
    }

    /// <summary>Laufende oder abgestürzte Aufnahmen nicht öffnen – das Öffnen würde sie wiederherstellen (schreiben).</summary>
    static bool Untouchable(string path) => !File.Exists(path) && Json.ReadObject(System.IO.Path.Combine(path, "manifest.json"))?.Str("state") == "recording";

    static string Stamp(SessionView v) => $"{v.Edits.Long("revision")}|{v.Steps.Obj("source").Long("lastSeq")}|{StepParams.BuilderVersion}";

    /// <summary>Anzahl ungeprüfter Eingaben und Anleitungsschritte (baut die Leseansicht; zwischengespeichert).</summary>
    public static SessionDetails? Details(string path)
    {
        try
        {
            if (Untouchable(path)) return null;
            bool archive = File.Exists(path);
            string cacheFile = System.IO.Path.Combine(path, "cache", "catalog.json");
            if (!archive && Json.ReadObject(cacheFile) is { } c && c.Str("stamp") is { } cs)
            {
                // Stempel ohne Leseansicht prüfen: Overlay-Revision und Journal-Länge
                var edits = Json.ReadObject(System.IO.Path.Combine(path, "edits.json"));
                if (cs.StartsWith((edits?.Long("revision") ?? 0) + "|") && c.Long("journalBytes") == new FileInfo(System.IO.Path.Combine(path, "events.jsonl")).Length)
                    return new SessionDetails(c.Int("unreviewed"), c.Int("guideSteps"));
            }
            using var v = SessionView.Open(path, headless: true);
            var d = new SessionDetails(v.UnreviewedCount, v.GuideSteps().Count);
            if (!archive && v.Session.State != "recording")
                try
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cacheFile)!);
                    Json.WriteAtomic(cacheFile, Json.Line(new JsonObject { ["stamp"] = Stamp(v), ["journalBytes"] = new FileInfo(System.IO.Path.Combine(path, "events.jsonl")).Length, ["unreviewed"] = d.Unreviewed, ["guideSteps"] = d.GuideSteps }));
                }
                catch { }
            return d;
        }
        catch { return null; }
    }

    /// <summary>
    /// Vorschaubild (PNG) über die Leseschicht: Vorher-Bild des ersten Anleitungsschritts mit allen Schwärzungen;
    /// zwischengespeichert unter <c>cache\thumbs\list.png</c> (das Overlay löscht den Zwischenspeicher bei jeder Änderung).
    /// </summary>
    public static byte[]? Thumbnail(string path, int width = 192)
    {
        try
        {
            if (Untouchable(path)) return null;
            bool archive = File.Exists(path);
            string file = System.IO.Path.Combine(path, "cache", "thumbs", $"list-{width}.png");
            string stampFile = file + ".json";
            long journal = archive ? 0 : new FileInfo(System.IO.Path.Combine(path, "events.jsonl")).Length;
            if (!archive && File.Exists(file) && Json.ReadObject(stampFile) is { } st && st.Long("journalBytes") == journal)
                return File.ReadAllBytes(file);
            using var v = SessionView.Open(path, headless: true);
            var step = v.GuideSteps().FirstOrDefault(s => !s.Redacted && (s.PreFrame != null || s.PostFrame != null));
            if (step == null) return null;
            using var img = StepImages.Render(v, step, step.PreFrame != null ? "before" : "after", "compatible", annotate: false, authorAnnotations: true);
            if (img == null) return null;
            int h = Math.Max(1, width * img.Bitmap.Height / Math.Max(1, img.Bitmap.Width));
            using var small = new Bitmap(width, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(small))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(img.Bitmap, 0, 0, width, h);
            }
            using var ms = new MemoryStream();
            small.Save(ms, ImageFormat.Png);
            var bytes = ms.ToArray();
            if (!archive && v.Session.State != "recording")
                try
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
                    File.WriteAllBytes(file, bytes);
                    Json.WriteAtomic(stampFile, Json.Line(new JsonObject { ["journalBytes"] = journal, ["revision"] = v.Edits.Long("revision") }));
                }
                catch { }
            return bytes;
        }
        catch { return null; }
    }
}
