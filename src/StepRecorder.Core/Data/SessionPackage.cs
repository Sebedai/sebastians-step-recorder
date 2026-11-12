using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Settings;

namespace StepRecorder.Data;

/// <summary>Grenzen beim Entpacken fremder Archive (Schutz vor Zip-Bomben und überlangen Archiven).</summary>
public sealed record UnpackLimits(int MaxEntries = 200_000, long MaxTotalBytes = 20L << 30, long MaxEntryBytes = 2L << 30, double MaxRatio = 250);

/// <summary>Fehler beim Entpacken: Archiv ungültig oder verdächtig; am Ziel bleibt nichts zurück.</summary>
public sealed class PackageException : Exception { public PackageException(string message) : base(message) { } }

/// <summary>
/// <c>.steprec</c>: eine Aufnahme als ZIP. Gepackt wird nur, was zur Aufnahme gehört (Manifest, Journal, Schritte, Overlay,
/// Bilder, Mauszeiger, eigene Bilder) – nie Zwischenspeicher, Exporte, Bearbeitungsstände oder Sperrdateien.
/// Entpackt wird nie mit <c>ExtractToDirectory</c>, sondern Eintrag für Eintrag nach einer Positivliste.
/// </summary>
public static partial class SessionPackage
{
    static readonly string[] TopFiles = { "manifest.json", "events.jsonl", "steps.json", "edits.json" };
    static readonly string[] TopDirs = { "frames", "cursors", "assets" };

    [GeneratedRegex(@"^(CON|PRN|AUX|NUL|COM[0-9¹²³]|LPT[0-9¹²³])(\..*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex DeviceName();

    /// <summary>Darf dieser Archivname (mit „/“) in eine Aufnahme? Keine Pfadsprünge, Laufwerke, Gerätenamen, nur bekannte Teile.</summary>
    public static bool IsAllowed(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 260 || name.Contains('\\') || name.Contains(':') || name.StartsWith('/') || name.Contains('\0')) return false;
        var parts = name.Split('/');
        foreach (var p in parts)
        {
            if (p.Length == 0 || p == "." || p == ".." || p.EndsWith('.') || p.EndsWith(' ') || DeviceName().IsMatch(p)) return false;
            if (p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        }
        if (parts.Length == 1) return TopFiles.Contains(parts[0], StringComparer.Ordinal);
        return TopDirs.Contains(parts[0], StringComparer.Ordinal);
    }

    /// <summary>Dateien, die gepackt würden (relativ, mit „/“), Manifest zuerst.</summary>
    public static List<string> PackList(string folder)
    {
        var list = new List<string>();
        foreach (var f in TopFiles) if (File.Exists(Path.Combine(folder, f))) list.Add(f);
        foreach (var d in TopDirs)
        {
            var dir = Path.Combine(folder, d);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                var rel = Path.GetRelativePath(folder, file).Replace('\\', '/');
                if (IsAllowed(rel)) list.Add(rel);
            }
        }
        return list;
    }

    /// <summary>
    /// Packt eine abgeschlossene Aufnahme. Aus dem Overlay werden absolute Pfade angehängter Aufnahmen entfernt
    /// (sie verraten Ordnerstrukturen dieses Rechners und wären beim Empfänger ohnehin ungültig).
    /// </summary>
    public static void Pack(string folder, string targetFile, IProgress<(int done, int total)>? progress = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(folder) || !File.Exists(Path.Combine(folder, "manifest.json"))) throw new DirectoryNotFoundException(folder);
        var man = Json.ReadObject(Path.Combine(folder, "manifest.json"));
        if (man.Str("state") == "recording" && Session.IsLockAlive(folder)) throw new PackageException(Loc.L("Die Aufnahme läuft noch.", "The recording is still running."));
        var files = PackList(folder);
        string tmp = targetFile + ".tmp-" + Guid.NewGuid().ToString("N")[..6];
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(targetFile))!);
        try
        {
            using (var fs = new FileStream(tmp, FileMode.CreateNew))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                int i = 0;
                foreach (var rel in files)
                {
                    ct.ThrowIfCancellationRequested();
                    var src = Path.Combine(folder, rel.Replace('/', '\\'));
                    // PNG ist schon komprimiert; Text-Dateien gut komprimieren
                    var level = rel.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? CompressionLevel.NoCompression : CompressionLevel.Optimal;
                    var entry = zip.CreateEntry(rel, level);
                    entry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    using var es = entry.Open();
                    if (rel == "edits.json") { var b = Encoding.UTF8.GetBytes(SanitizeEdits(File.ReadAllText(src))); es.Write(b); }
                    else { using var inp = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); inp.CopyTo(es); }
                    progress?.Report((++i, files.Count));
                }
            }
            File.Move(tmp, targetFile, true);
        }
        finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
    }

    static string SanitizeEdits(string json)
    {
        try
        {
            if (JsonNode.Parse(json) is not JsonObject e) return json;
            foreach (var a in (e.Obj("guide")?.Arr("append") ?? new JsonArray()).OfType<JsonObject>()) a.Remove("path");
            return Json.Pretty(e);
        }
        catch { return json; }
    }

    /// <summary>Kopfdaten eines Archivs ohne Entpacken (Titel, Kennung) – null, wenn kein gültiges Archiv.</summary>
    public static JsonObject? PeekManifest(string file)
    {
        try
        {
            using var zip = ZipFile.OpenRead(file);
            var e = zip.GetEntry("manifest.json");
            if (e == null || e.Length > 2_000_000) return null;
            using var r = new StreamReader(e.Open(), Encoding.UTF8);
            return JsonNode.Parse(r.ReadToEnd()) as JsonObject;
        }
        catch { return null; }
    }

    /// <summary>Ordner unter <paramref name="parent"/> mit derselben Aufnahme-Kennung (für die Rückfrage vor dem Entpacken).</summary>
    public static string? FindExisting(string parent, string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || !Directory.Exists(parent)) return null;
        foreach (var d in Directory.EnumerateDirectories(parent))
            if (Json.ReadObject(Path.Combine(d, "manifest.json"))?.Str("sessionId") == sessionId) return d;
        return null;
    }

    /// <summary>
    /// Entpackt in einen neuen Ordner unter <paramref name="targetParent"/> und gibt ihn zurück. Erst wird alles geprüft und
    /// in einen Zwischenordner geschrieben, dann verschoben – bei jedem Fehler bleibt nichts zurück.
    /// </summary>
    public static string Unpack(string file, string targetParent, UnpackLimits? limits = null, CancellationToken ct = default)
    {
        limits ??= new UnpackLimits();
        using var zip = ZipFile.OpenRead(file);
        if (zip.Entries.Count > limits.MaxEntries) throw new PackageException(Loc.F("Zu viele Einträge im Archiv ({0}).", "Too many entries in the archive ({0}).", zip.Entries.Count));
        long total = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in zip.Entries)
        {
            if (e.FullName.EndsWith('/')) { if (e.Length != 0) throw new PackageException(Loc.L("Ungültiger Ordnereintrag.", "Invalid folder entry.")); continue; }
            if (!IsAllowed(e.FullName)) throw new PackageException(Loc.F("Unerlaubter Eintrag im Archiv: {0}", "Entry not allowed in archive: {0}", Shorten(e.FullName)));
            if (!names.Add(e.FullName)) throw new PackageException(Loc.F("Doppelter Eintrag im Archiv: {0}", "Duplicate entry in archive: {0}", Shorten(e.FullName)));
            if (e.Length < 0 || e.Length > limits.MaxEntryBytes) throw new PackageException(Loc.F("Eintrag zu groß: {0}", "Entry too large: {0}", Shorten(e.FullName)));
            if (e.Length > 1_000_000 && e.CompressedLength > 0 && e.Length / (double)e.CompressedLength > limits.MaxRatio) throw new PackageException(Loc.F("Verdächtig stark komprimierter Eintrag: {0}", "Suspiciously highly compressed entry: {0}", Shorten(e.FullName)));
            total += e.Length;
            if (total > limits.MaxTotalBytes) throw new PackageException(Loc.L("Das Archiv ist entpackt zu groß.", "The archive is too large when unpacked."));
        }
        if (!names.Contains("manifest.json") || !names.Contains("events.jsonl")) throw new PackageException(Loc.L("Das Archiv enthält keine Aufnahme (manifest.json oder events.jsonl fehlt).", "The archive contains no recording (manifest.json or events.jsonl missing)."));
        var man = ReadEntryJson(zip, "manifest.json") ?? throw new PackageException(Loc.L("manifest.json ist ungültig.", "manifest.json is invalid."));
        if (man.Str("format") is { } fmt && fmt != "steprec") throw new PackageException(Loc.L("Unbekanntes Format.", "Unknown format."));

        Directory.CreateDirectory(targetParent);
        string tmp = Path.Combine(targetParent, ".steprec-unpack-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tmp);
        try
        {
            string tmpFull = Path.GetFullPath(tmp) + Path.DirectorySeparatorChar;
            long written = 0;
            foreach (var e in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                if (e.FullName.EndsWith('/')) continue;
                string dest = Path.GetFullPath(Path.Combine(tmp, e.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!dest.StartsWith(tmpFull, StringComparison.OrdinalIgnoreCase)) throw new PackageException(Loc.L("Unerlaubter Pfad im Archiv.", "Path not allowed in archive."));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                using var src = e.Open();
                using var dst = new FileStream(dest, FileMode.CreateNew);
                // Angaben im Archivkopf können lügen: tatsächlich geschriebene Bytes mitzählen
                var buf = new byte[81920];
                long entryBytes = 0;
                int n;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    entryBytes += n; written += n;
                    if (entryBytes > e.Length || entryBytes > limits.MaxEntryBytes || written > limits.MaxTotalBytes) throw new PackageException(Loc.F("Eintrag größer als angegeben: {0}", "Entry larger than declared: {0}", Shorten(e.FullName)));
                    dst.Write(buf, 0, n);
                }
            }
            string name = Export.Fmt.FileSafe(man.Str("title") is { Length: > 0 } t ? t : Path.GetFileNameWithoutExtension(file), 60);
            if (name.Length == 0) name = Loc.L("Aufnahme", "Recording");
            string final = Path.Combine(targetParent, name);
            for (int i = 2; Directory.Exists(final) || File.Exists(final); i++) final = Path.Combine(targetParent, $"{name} ({i})");
            Directory.Move(tmp, final);
            return final;
        }
        finally { try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } }
    }

    static JsonObject? ReadEntryJson(ZipArchive zip, string name)
    {
        var e = zip.GetEntry(name);
        if (e == null || e.Length > 50_000_000) return null;
        try { using var r = new StreamReader(e.Open(), Encoding.UTF8); return JsonNode.Parse(r.ReadToEnd()) as JsonObject; }
        catch { return null; }
    }

    static string Shorten(string s) => s.Length <= 80 ? s : s[..77] + "…";
}
