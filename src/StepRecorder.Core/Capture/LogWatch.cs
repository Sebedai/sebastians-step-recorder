using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Data;

namespace StepRecorder.Capture;

/// <summary>
/// Mitgelesene Protokolldateien (12, Paket 13): ausdrücklich genannte Dateien oder Muster im Dateinamen
/// (<c>C:\App\logs\*.log</c>). Gelesen werden nur Zeilen, die während der Aufnahme dazukommen – ab der Länge beim Start,
/// mit geteiltem Zugriff, im 500-ms-Takt auf einem eigenen Thread. Nur lokale Festplatten; Grenzen je Datei und Zeile;
/// während Pause und Privattaste werden Zeilen übersprungen und nur gezählt. Inhalte sind prüfpflichtig (Paket 12).
/// </summary>
public sealed class LogWatch : IDisposable
{
    public const int MaxFiles = 5, MaxLineChars = 2000, MaxBytesPerFile = 256 * 1024, MaxLinesPerEvent = 200;

    sealed class Source
    {
        public string Id = "", Path = "", Name = "";
        public long Pos, Total;
        public int Dropped;
        public Encoding Enc = Encoding.UTF8;
        public readonly StringBuilder Partial = new();
    }

    readonly List<Source> _sources = new();
    readonly Func<long> _now;
    readonly Action<JsonObject> _emit;
    readonly Func<bool> _active;
    readonly Thread _thread;
    volatile bool _stop;

    /// <param name="active">Aufnahme läuft und Privattaste ist aus (sonst werden Zeilen nur gezählt).</param>
    public LogWatch(IEnumerable<string> patterns, Func<long> now, Action<JsonObject> emit, Func<bool> active)
    {
        _now = now; _emit = emit; _active = active;
        int n = 0;
        foreach (var path in Resolve(patterns))
        {
            var s = new Source { Id = "l" + ++n, Path = path, Name = System.IO.Path.GetFileName(path) };
            try { s.Pos = new FileInfo(path).Length; } catch { }
            _sources.Add(s);
        }
        _thread = new Thread(Loop) { IsBackground = true, Name = "Log.Watch", Priority = ThreadPriority.BelowNormal };
    }

    /// <summary>Gefundene Dateien (für die Startmeldung).</summary>
    public IReadOnlyList<string> Files => _sources.Select(s => s.Name).ToList();

    public void Start()
    {
        foreach (var s in _sources) _emit(new JsonObject { ["t"] = _now(), ["type"] = "log.source", ["log"] = s.Id, ["file"] = s.Name, ["startLength"] = s.Pos });
        if (_sources.Count > 0) _thread.Start();
    }

    /// <summary>Pfade auflösen: nur lokale Festplatten, Platzhalter nur im Dateinamen, höchstens <see cref="MaxFiles"/>.</summary>
    public static List<string> Resolve(IEnumerable<string> patterns)
    {
        var res = new List<string>();
        foreach (var raw in patterns)
        {
            var p = Environment.ExpandEnvironmentVariables((raw ?? "").Trim().Trim('"'));
            if (p.Length == 0) continue;
            try
            {
                p = System.IO.Path.GetFullPath(p);
                if (!LocalFixed(p)) continue;
                string dir = System.IO.Path.GetDirectoryName(p) ?? "", name = System.IO.Path.GetFileName(p);
                if (name.IndexOfAny(new[] { '*', '?' }) >= 0)
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.EnumerateFiles(dir, name).OrderByDescending(File.GetLastWriteTimeUtc).Take(MaxFiles)) if (!res.Contains(f, StringComparer.OrdinalIgnoreCase)) res.Add(f);
                }
                else if (File.Exists(p) && !res.Contains(p, StringComparer.OrdinalIgnoreCase)) res.Add(p);
            }
            catch { }
            if (res.Count >= MaxFiles) break;
        }
        return res.Take(MaxFiles).ToList();
    }

    static bool LocalFixed(string path)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return false;
        try { return new DriveInfo(System.IO.Path.GetPathRoot(path)!).DriveType == DriveType.Fixed; } catch { return false; }
    }

    void Loop()
    {
        while (!_stop)
        {
            Thread.Sleep(500);
            foreach (var s in _sources) { try { Poll(s); } catch { } }
        }
    }

    /// <summary>Neue Bytes einer Datei lesen und als Zeilen melden (auch für Tests).</summary>
    void Poll(Source s)
    {
        if (!File.Exists(s.Path)) return;
        using var fs = new FileStream(s.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long len = fs.Length;
        bool rotated = false;
        if (len < s.Pos) { s.Pos = 0; s.Partial.Clear(); rotated = true; }   // Datei gedreht oder geleert
        if (len == s.Pos) return;
        long take = Math.Min(len - s.Pos, 64 * 1024);
        fs.Position = s.Pos;
        var buf = new byte[take];
        int read = fs.Read(buf, 0, buf.Length);
        s.Pos += read;
        if (s.Pos == read && read >= 2)
        {
            if (buf[0] == 0xFF && buf[1] == 0xFE) s.Enc = Encoding.Unicode;
            else if (buf[0] == 0xFE && buf[1] == 0xFF) s.Enc = Encoding.BigEndianUnicode;
        }
        string text = s.Enc.GetString(buf, 0, read).TrimStart('\uFEFF');
        s.Partial.Append(text);
        var all = s.Partial.ToString();
        int lastNl = all.LastIndexOf('\n');
        if (lastNl < 0) { if (s.Partial.Length > MaxLineChars * 4) s.Partial.Clear(); return; }
        s.Partial.Clear(); s.Partial.Append(all[(lastNl + 1)..]);
        var lines = all[..lastNl].Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0) return;
        if (!_active()) { s.Dropped += lines.Count; return; }                       // Pause, Privattaste: nur zählen
        bool truncated = false;
        var outLines = new List<string>();
        foreach (var l in lines)
        {
            if (s.Total >= MaxBytesPerFile || outLines.Count >= MaxLinesPerEvent) { s.Dropped++; truncated = true; continue; }
            string line = l.Length > MaxLineChars ? l[..MaxLineChars] + "…" : l;
            if (line.Length != l.Length) truncated = true;
            outLines.Add(line);
            s.Total += line.Length;
        }
        var ev = new JsonObject { ["t"] = _now(), ["tSrc"] = "obs", ["type"] = "log.lines", ["log"] = s.Id, ["file"] = s.Name, ["count"] = outLines.Count };
        if (outLines.Count > 0) ev["lines"] = string.Join("\n", outLines);
        if (truncated) ev["truncated"] = true;
        if (s.Dropped > 0) { ev["dropped"] = s.Dropped; s.Dropped = 0; }
        if (rotated) ev["rotated"] = true;
        if (outLines.Count > 0 || ev["dropped"] != null) _emit(ev);
    }

    /// <summary>Für Tests: einmal alle Quellen abfragen.</summary>
    internal void PollAll() { foreach (var s in _sources) Poll(s); }

    public void Dispose()
    {
        _stop = true;
        if (_thread.IsAlive) _thread.Join(1000);
    }
}
