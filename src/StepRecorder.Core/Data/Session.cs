using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Settings;

namespace StepRecorder.Data;

/// <summary>
/// Geöffnete Sitzung (Ordner oder <c>.steprec</c>-ZIP, letzteres nur lesend). Rohzugriff –
/// Ausgaben lesen ausschließlich über <see cref="StepRecorder.ReadLayer.SessionView"/>.
/// </summary>
public sealed class Session : IDisposable
{
    readonly ZipArchive? _zip;
    public string Path { get; }
    public bool IsArchive => _zip != null;
    public bool ReadOnly => IsArchive;
    public JsonObject Manifest { get; private set; }
    public List<JsonObject> Events { get; }
    readonly Dictionary<long, JsonObject> _bySeq = new();
    readonly Dictionary<string, JsonObject> _frames = new();
    readonly Dictionary<string, JsonObject> _windows = new();
    public List<string> LoadWarnings { get; } = new();

    public string SessionId => Manifest.Str("sessionId") ?? "";
    public string Title => Manifest.Str("title") ?? System.IO.Path.GetFileNameWithoutExtension(Path.TrimEnd('\\', '/'));
    public string State => Manifest.Str("state") ?? "complete";
    public string Language => Manifest.Obj("settings").Str("language") ?? "de";
    public long DurationUs => Manifest.LongN("durationUs") ?? (Events.Count > 0 ? Events.Max(e => e.Long("t")) : 0);
    public DateTime StartUtc => Json.ParseUtc(Manifest.Str("createdUtc"));
    public IReadOnlyDictionary<string, JsonObject> Frames => _frames;
    public IReadOnlyDictionary<string, JsonObject> Windows => _windows;

    Session(string path, JsonObject manifest, List<JsonObject> events, ZipArchive? zip)
    {
        Path = path; Manifest = manifest; Events = events; _zip = zip;
        foreach (var e in events)
        {
            _bySeq[e.Long("seq")] = e;
            switch (e.Str("type"))
            {
                case "frame": if (e.Str("id") is { } id) _frames[id] = e; break;
                case "window.info": if (e.Str("win") is { } w) _windows[w] = e; break;
            }
        }
    }

    public static bool LooksLikeSession(string path)
        => (Directory.Exists(path) && File.Exists(System.IO.Path.Combine(path, "manifest.json")))
           || (File.Exists(path) && path.EndsWith(".steprec", StringComparison.OrdinalIgnoreCase));

    /// <summary>Öffnet eine Sitzung. Bei abgestürzter Aufnahme (Zustand recording ohne lebende Sperre) wird wiederhergestellt.</summary>
    public static Session Open(string path, bool recover = true)
    {
        if (File.Exists(path) && path.EndsWith(".steprec", StringComparison.OrdinalIgnoreCase))
        {
            var zip = ZipFile.OpenRead(path);
            var man = ReadZipJson(zip, "manifest.json") ?? throw new InvalidDataException(Loc.L("manifest.json fehlt", "manifest.json is missing"));
            CheckVersion(man);
            var evEntry = zip.GetEntry("events.jsonl") ?? throw new InvalidDataException(Loc.L("events.jsonl fehlt", "events.jsonl is missing"));
            using var s = evEntry.Open();
            var (events, _, _) = ReadJournal(s);
            return new Session(path, man, events, zip);
        }
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
        var manifest = Json.ReadObject(System.IO.Path.Combine(path, "manifest.json")) ?? throw new InvalidDataException(Loc.L("manifest.json fehlt oder ist ungültig", "manifest.json is missing or invalid"));
        CheckVersion(manifest);
        if (recover && manifest.Str("state") == "recording" && !IsLockAlive(path))
            Recovery.Recover(path);
        manifest = Json.ReadObject(System.IO.Path.Combine(path, "manifest.json"))!;
        using var fs = new FileStream(System.IO.Path.Combine(path, "events.jsonl"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var (evs, invalidAt, _) = ReadJournal(fs);
        var sess = new Session(path, manifest, evs, null);
        if (invalidAt >= 0) sess.LoadWarnings.Add($"Journal ab Byte {invalidAt} unlesbar; ignoriert.");
        return sess;
    }

    static void CheckVersion(JsonObject man)
    {
        var v = man.Str("schemaVersion") ?? "1.0";
        if (!int.TryParse(v.Split('.')[0], out int major) || major > 1)
            throw new NotSupportedException(Loc.F("Sitzungsformat {0} wird nicht unterstützt (benötigt neuere Programmversion).", "Session format {0} is not supported (needs a newer program version).", v));
        // Mindestversion des Lesers (inhaltstragende Ereignisse, 12 Paket 12): ältere Programme dürfen sie nicht ungeprüft ausgeben
        if (man.Obj("requires")?.Str("reader") is { } need && Version.TryParse(need, out var nv) && Version.TryParse(AppInfo.Version, out var have) && nv > have)
            throw new NotSupportedException(Loc.F("Diese Aufnahme braucht Programmversion {0} oder neuer.", "This recording needs program version {0} or later.", need));
    }

    /// <summary>Leser-Mindestversion für Aufnahmen mit inhaltstragenden Quellen (Freitext der Ereignisanzeige, Zwischenablage, Protokolldateien).</summary>
    public const string ContentReaderVersion = "0.13.0";

    public static bool IsLockAlive(string folder)
    {
        var lockFile = System.IO.Path.Combine(folder, "session.lock");
        if (!File.Exists(lockFile)) return false;
        try
        {
            var o = JsonNode.Parse(File.ReadAllText(lockFile));
            int pid = o.Int("pid");
            if (pid == 0) return false;
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch { return false; }
    }

    /// <summary>Liest das Journal tolerant bis zur ersten ungültigen Zeile oder Lücke in <c>seq</c>.</summary>
    public static (List<JsonObject> events, long invalidAtByte, long validBytes) ReadJournal(Stream s)
    {
        var list = new List<JsonObject>();
        var buf = new MemoryStream();
        s.CopyTo(buf);
        var data = buf.GetBuffer();
        int len = (int)buf.Length;
        long pos = 0; long expected = 1;
        while (pos < len)
        {
            int nl = Array.IndexOf(data, (byte)'\n', (int)pos, len - (int)pos);
            if (nl < 0) return (list, pos, pos); // unvollständige letzte Zeile
            var line = Encoding.UTF8.GetString(data, (int)pos, nl - (int)pos);
            JsonObject? o = null;
            try { o = JsonNode.Parse(line) as JsonObject; } catch { }
            if (o == null || o.Long("seq", -1) != expected) return (list, pos, pos);
            list.Add(o);
            expected++;
            pos = nl + 1;
        }
        return (list, -1, pos);
    }

    static JsonObject? ReadZipJson(ZipArchive zip, string name)
    {
        var e = zip.GetEntry(name);
        if (e == null) return null;
        using var r = new StreamReader(e.Open(), Encoding.UTF8);
        return JsonNode.Parse(r.ReadToEnd()) as JsonObject;
    }

    public JsonObject? BySeq(long seq) => _bySeq.TryGetValue(seq, out var e) ? e : null;
    public JsonObject? Window(string? win) => win != null && _windows.TryGetValue(win, out var w) ? w : null;
    public JsonObject? Frame(string? id) => id != null && _frames.TryGetValue(id, out var f) ? f : null;

    // ---- Dateien ----
    public bool FileExists(string rel)
        => _zip != null ? _zip.GetEntry(rel.Replace('\\', '/')) != null : File.Exists(System.IO.Path.Combine(Path, rel));

    public byte[]? ReadFile(string rel)
    {
        if (_zip != null)
        {
            var e = _zip.GetEntry(rel.Replace('\\', '/'));
            if (e == null) return null;
            using var s = e.Open(); var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray();
        }
        var p = System.IO.Path.Combine(Path, rel);
        return File.Exists(p) ? File.ReadAllBytes(p) : null;
    }

    public JsonObject? ReadJson(string rel)
    {
        var b = ReadFile(rel);
        if (b == null) return null;
        try { return JsonNode.Parse(Encoding.UTF8.GetString(b)) as JsonObject; } catch { return null; }
    }

    public JsonObject? LoadSteps() => ReadJson("steps.json");
    public JsonObject? LoadEdits() => ReadJson("edits.json");

    /// <summary>Speichert <c>edits.json</c> mit Revisionsprüfung (optimistische Sperre).</summary>
    public bool SaveEdits(JsonObject edits, long expectedRevision, string savedBy)
    {
        if (ReadOnly) throw new InvalidOperationException(Loc.L("Sitzung ist schreibgeschützt (.steprec)", "The recording is read-only (.steprec)"));
        var current = LoadEdits();
        long curRev = current?.Long("revision") ?? 0;
        if (curRev != expectedRevision) return false;
        edits["format"] = "steprec.edits";
        edits["schemaVersion"] = SessionWriter.SchemaVersion;
        edits["sessionId"] = SessionId;
        edits["revision"] = curRev + 1;
        edits["savedAtUtc"] = Json.UtcNow();
        edits["savedBy"] = savedBy;
        Json.WriteAtomic(System.IO.Path.Combine(Path, "edits.json"), Json.Pretty(edits));
        return true;
    }

    public void SaveSteps(JsonObject steps)
    {
        if (ReadOnly) return;
        Json.WriteAtomic(System.IO.Path.Combine(Path, "steps.json"), Json.Pretty(steps));
    }

    public void SaveManifest(JsonObject manifest)
    {
        if (ReadOnly) return;
        Json.WriteAtomic(System.IO.Path.Combine(Path, "manifest.json"), Json.Pretty(manifest));
        Manifest = manifest;
    }

    // ---- Bilder ----
    readonly Dictionary<string, Bitmap> _frameCache = new();
    readonly LinkedList<string> _frameLru = new();
    readonly object _imgLock = new();

    /// <summary>Pfad eines Bild-Blobs; null bei einem ungültigen Namen (schützt vor Pfaden außerhalb der Sitzung).</summary>
    public static string? BlobPath(string blob)
    {
        var hex = blob.StartsWith("sha256:") ? blob[7..] : blob;
        if (hex.Length != 64 || !hex.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')) return null;
        return $"frames/{hex[..2]}/{hex}.png";
    }

    /// <summary>Stellt ein Vollbild (Monitorgröße) aus key/delta/same wieder her. Aufrufer darf das Bild nicht verändern.</summary>
    public Bitmap? RestoreFrame(string frameId)
    {
        lock (_imgLock) return RestoreLocked(frameId, 0);
    }

    /// <summary>Kopie eines wiederhergestellten Bildes (darf verändert werden).</summary>
    public Bitmap? RestoreFrameCopy(string frameId)
    {
        lock (_imgLock)
        {
            var b = RestoreLocked(frameId, 0);
            return b == null ? null : new Bitmap(b);
        }
    }

    Bitmap? RestoreLocked(string id, int depth)
    {
        if (depth > 64) return null;
        if (_frameCache.TryGetValue(id, out var cached)) { Touch(id); return cached; }
        var f = Frame(id);
        if (f == null) return null;
        var store = f.Obj("store");
        Bitmap? result = null;
        switch (store.Str("kind"))
        {
            case "key":
                {
                    var bytes = BlobPath(store.Str("blob") ?? "") is { } bp ? ReadFile(bp) : null;
                    if (bytes == null) return null;
                    using var ms = new MemoryStream(bytes);
                    using var img = new Bitmap(ms);
                    result = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppRgb);
                    using (var g = Graphics.FromImage(result)) g.DrawImageUnscaled(img, 0, 0);
                    break;
                }
            case "same":
                {
                    var b = RestoreLocked(store.Str("as") ?? "", depth + 1);
                    if (b == null) return null;
                    result = new Bitmap(b);
                    break;
                }
            case "delta":
                {
                    var b = RestoreLocked(store.Str("base") ?? "", depth + 1);
                    var bytes = BlobPath(store.Str("blob") ?? "") is { } bp ? ReadFile(bp) : null;
                    if (b == null || bytes == null) return null;
                    result = new Bitmap(b);
                    var rect = store.Obj("rect");
                    using var ms = new MemoryStream(bytes);
                    using var patch = new Bitmap(ms);
                    using (var g = Graphics.FromImage(result))
                    {
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                        g.DrawImage(patch, new Rectangle(rect.Int("x"), rect.Int("y"), patch.Width, patch.Height));
                    }
                    break;
                }
            default: return null;
        }
        _frameCache[id] = result;
        _frameLru.AddFirst(id);
        while (_frameLru.Count > 24)
        {
            var last = _frameLru.Last!.Value; _frameLru.RemoveLast();
            if (_frameCache.Remove(last, out var old)) old.Dispose();
        }
        return result;
    }

    void Touch(string id) { _frameLru.Remove(id); _frameLru.AddFirst(id); }

    /// <summary>Monitor-Infos aus der (letzten) Topologie.</summary>
    public JsonObject? Monitor(string? m, int epoch = -1)
    {
        JsonObject? found = null;
        foreach (var e in Events)
        {
            if (e.Str("type") != "display.topology") continue;
            if (epoch >= 0 && e.Int("epoch") > epoch) break;
            foreach (var mon in e.Arr("monitors") ?? new JsonArray())
                if (mon.Str("id") == m) found = mon as JsonObject;
        }
        return found;
    }

    public void Dispose()
    {
        lock (_imgLock) { foreach (var b in _frameCache.Values) b.Dispose(); _frameCache.Clear(); }
        _zip?.Dispose();
    }
}
