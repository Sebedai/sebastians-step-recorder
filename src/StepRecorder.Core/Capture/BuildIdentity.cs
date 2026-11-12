using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Data;

namespace StepRecorder.Capture;

/// <summary>
/// Programmende einstufen (12, Paket 10): „Absturz“ nur bei Fehlercodes mit eindeutiger Bedeutung – NTSTATUS mit
/// Schwere „Fehler“ (0xC…), .NET- und C++-Ausnahmen, Haltepunkt –, sonst „beendet mit Code N“.
/// </summary>
public static class ExitCodes
{
    static readonly Dictionary<uint, (string de, string en)> Names = new()
    {
        [0xC0000005] = ("Zugriffsverletzung", "access violation"),
        [0xC0000409] = ("Sofortabbruch (Stapelpuffer-Überlauf oder FailFast)", "fail-fast (stack buffer overrun or FailFast)"),
        [0xC00000FD] = ("Stapelüberlauf", "stack overflow"),
        [0xC0000374] = ("beschädigter Heap", "heap corruption"),
        [0xC0000094] = ("Division durch null", "integer division by zero"),
        [0xC000001D] = ("ungültiger Befehl", "illegal instruction"),
        [0xC0000135] = ("DLL nicht gefunden", "DLL not found"),
        [0xC0000142] = ("DLL-Initialisierung fehlgeschlagen", "DLL initialization failed"),
        [0xC0000417] = ("ungültiger Parameter (C-Laufzeit)", "invalid parameter (C runtime)"),
        [0xE0434352] = ("unbehandelte .NET-Ausnahme", "unhandled .NET exception"),
        [0xE06D7363] = ("unbehandelte C++-Ausnahme", "unhandled C++ exception"),
        [0x80000003] = ("Haltepunkt", "breakpoint"),
    };

    /// <summary>Einstufung: <c>normal</c> (0), <c>crash</c> (eindeutiger Fehlercode) oder <c>error</c> (anderer Code).</summary>
    public static string Classify(uint code) => code switch
    {
        0 => "normal",
        0xC000013A => "error",            // STATUS_CONTROL_C_EXIT: Abbruch mit Strg+C, kein Absturz
        0x40010004 => "error",            // DBG_TERMINATE_PROCESS
        _ when (code & 0xC0000000) == 0xC0000000 || code is 0xE0434352 or 0xE06D7363 or 0x80000003 => "crash",
        _ => "error"
    };

    public static string? Name(uint code, string lang) => Names.TryGetValue(code, out var n) ? (lang == "de" ? n.de : n.en) : null;

    public static string Hex(uint code) => "0x" + code.ToString("X8");

    /// <summary>Exit-Code lesbar: kleine Codes dezimal, sonst hexadezimal.</summary>
    public static string Display(uint code) => code < 0x10000 ? code.ToString() : Hex(code);
}

/// <summary>
/// Kennung eines Builds aus der Programmdatei (12, Paket 10): Zeitstempel des PE-Kopfs und die PDB-Kennung
/// (CodeView „RSDS“: GUID und Alter). Liest nur die Datei, nie Speicher des Prozesses; höchstens 1 MB.
/// </summary>
public static class PeInfo
{
    public static JsonObject? Read(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var br = new BinaryReader(fs);
            if (fs.Length < 0x40 || br.ReadUInt16() != 0x5A4D) return null;          // "MZ"
            fs.Position = 0x3C;
            uint peOff = br.ReadUInt32();
            if (peOff > fs.Length - 24) return null;
            fs.Position = peOff;
            if (br.ReadUInt32() != 0x00004550) return null;                            // "PE\0\0"
            br.ReadUInt16();                                                           // Machine
            int sections = br.ReadUInt16();
            uint timeStamp = br.ReadUInt32();
            fs.Position += 8;
            int optSize = br.ReadUInt16();
            fs.Position += 2;
            long optStart = fs.Position;
            ushort magic = br.ReadUInt16();
            bool pe32Plus = magic == 0x20B;
            long dirStart = optStart + (pe32Plus ? 112 : 96);
            var o = new JsonObject();
            // Deterministische Builds tragen statt einer Zeit einen Hash – nur plausible Zeiten als Zeit ausgeben
            var ts = DateTimeOffset.FromUnixTimeSeconds(timeStamp).UtcDateTime;
            if (ts.Year is >= 2000 and <= 2100) o["peTimestampUtc"] = ts.ToString("yyyy-MM-ddTHH:mm:ssZ"); else o["peTimestamp"] = "0x" + timeStamp.ToString("X8");
            // Datenverzeichnis 6 = Debug
            fs.Position = dirStart + 6 * 8;
            uint debugRva = br.ReadUInt32(), debugSize = br.ReadUInt32();
            if (debugRva != 0 && debugSize >= 28)
            {
                fs.Position = optStart + optSize;
                var secs = new List<(uint va, uint size, uint raw)>();
                for (int i = 0; i < Math.Min(sections, 96); i++)
                {
                    fs.Position += 8;
                    uint vsize = br.ReadUInt32(), va = br.ReadUInt32(), rawSize = br.ReadUInt32(), raw = br.ReadUInt32();
                    fs.Position += 16;
                    secs.Add((va, Math.Max(vsize, rawSize), raw));
                }
                long Off(uint rva) { foreach (var s in secs) if (rva >= s.va && rva < s.va + s.size) return s.raw + (rva - s.va); return -1; }
                long dbg = Off(debugRva);
                for (int i = 0; dbg >= 0 && i < Math.Min(debugSize / 28, 16); i++)
                {
                    fs.Position = dbg + i * 28 + 12;
                    uint type = br.ReadUInt32(); uint size = br.ReadUInt32(); br.ReadUInt32(); uint ptr = br.ReadUInt32();
                    if (type != 2 || size < 24 || ptr > fs.Length - 24 || ptr > 1_048_576 * 64) continue;   // CodeView
                    fs.Position = ptr;
                    if (br.ReadUInt32() != 0x53445352) continue;                       // "RSDS"
                    var guid = new Guid(br.ReadBytes(16));
                    uint age = br.ReadUInt32();
                    var name = new List<byte>();
                    for (int k = 0; k < 260; k++) { byte b = br.ReadByte(); if (b == 0) break; name.Add(b); }
                    o["pdbId"] = guid.ToString("N").ToUpperInvariant() + age.ToString("X");
                    string pdb = Encoding.UTF8.GetString(name.ToArray());
                    if (pdb.Length > 0) o["pdbFile"] = Path.GetFileName(pdb);   // nur der Dateiname, nie der Pfad des Build-Rechners
                    break;
                }
            }
            return o;
        }
        catch { return null; }
    }
}

/// <summary>
/// Git-Arbeitsstand des Ordners, in dem die Programmdatei liegt (12, Paket 10) – nur als gekennzeichnete Zusatzangabe:
/// Es ist der Stand des Ordners, kein Beleg für den Build. Liest <c>.git</c> direkt (HEAD, Refs, packed-refs, Verweis
/// <c>gitdir:</c>), führt nie <c>git.exe</c> aus und gibt nie Pfade, Fernadressen, Autoren oder Commit-Texte aus.
/// </summary>
public static class GitInfo
{
    static readonly Regex Sha = new("^[0-9a-f]{40}([0-9a-f]{24})?$", RegexOptions.CultureInvariant);

    public static JsonObject? Read(string exePath, int maxLevels = 12)
    {
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(exePath));
            for (int i = 0; i < maxLevels && dir != null; i++, dir = Path.GetDirectoryName(dir))
            {
                string dotGit = Path.Combine(dir, ".git");
                string? gitDir = null;
                if (Directory.Exists(dotGit)) gitDir = dotGit;
                else if (File.Exists(dotGit) && new FileInfo(dotGit).Length < 4096)
                {
                    var line = File.ReadAllText(dotGit).Trim();
                    if (line.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase))
                    {
                        var p = line[7..].Trim();
                        gitDir = Path.GetFullPath(Path.IsPathRooted(p) ? p : Path.Combine(dir, p));
                    }
                }
                if (gitDir == null) continue;
                return FromGitDir(gitDir);
            }
        }
        catch { }
        return null;
    }

    public static JsonObject? FromGitDir(string gitDir)
    {
        string headFile = Path.Combine(gitDir, "HEAD");
        if (!File.Exists(headFile) || new FileInfo(headFile).Length > 4096) return null;
        string head = File.ReadAllText(headFile).Trim();
        // Worktree: gemeinsame Refs liegen im Hauptverzeichnis (commondir)
        string common = gitDir;
        string cd = Path.Combine(gitDir, "commondir");
        if (File.Exists(cd) && new FileInfo(cd).Length < 4096) { var c = File.ReadAllText(cd).Trim(); common = Path.GetFullPath(Path.IsPathRooted(c) ? c : Path.Combine(gitDir, c)); }
        var o = new JsonObject { ["basis"] = "workingTree", ["dirty"] = "notChecked" };
        if (head.StartsWith("ref:", StringComparison.Ordinal))
        {
            string reference = head[4..].Trim();
            if (reference.StartsWith("refs/heads/", StringComparison.Ordinal)) o["branch"] = reference["refs/heads/".Length..];
            string? sha = RefSha(gitDir, reference) ?? RefSha(common, reference);
            if (sha != null) o["commit"] = sha;
        }
        else if (Sha.IsMatch(head)) { o["commit"] = head; o["detached"] = true; }
        else return null;
        return o;
    }

    static string? RefSha(string dir, string reference)
    {
        if (reference.Contains("..")) return null;
        string loose = Path.Combine(dir, reference.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(loose) && new FileInfo(loose).Length < 200) { var s = File.ReadAllText(loose).Trim(); if (Sha.IsMatch(s)) return s; }
        string packed = Path.Combine(dir, "packed-refs");
        if (File.Exists(packed) && new FileInfo(packed).Length <= 4 * 1024 * 1024)
            foreach (var line in File.ReadLines(packed))
            {
                if (line.Length < 42 || line[0] is '#' or '^') continue;
                int sp = line.IndexOf(' ');
                if (sp > 0 && line[(sp + 1)..] == reference && Sha.IsMatch(line[..sp])) return line[..sp];
            }
        return null;
    }
}

/// <summary>
/// Fakten aus Einträgen der Windows-Ereignisanzeige (Anwendungsprotokoll) zu aufgenommenen Programmen (12, Paket 10):
/// Ereignis 1000 (Anwendungsfehler), 1002 (Hänger), 1026 (.NET-Laufzeit). Nur Fakten ohne Freitext –
/// Programm, Version, Modul, Ausnahmecode, Offset, Ausnahmetyp; nie Pfade. Den Freitext bringt erst Paket 12.
/// </summary>
public static class EventLogFacts
{
    public static readonly int[] Ids = { 1000, 1002, 1026 };

    /// <summary>Fakten aus den Eigenschaften eines Eintrags (Reihenfolge wie im Anwendungsprotokoll von Windows).</summary>
    public static JsonObject? FromProperties(int id, IReadOnlyList<object?> p)
    {
        string? S(int i) => i < p.Count ? p[i]?.ToString()?.Trim() : null;
        switch (id)
        {
            case 1000 when p.Count >= 8:
                return new JsonObject
                {
                    ["app"] = FileOnly(S(0)), ["appVersion"] = S(1), ["module"] = FileOnly(S(3)), ["moduleVersion"] = S(4),
                    ["exceptionCode"] = Code(S(6)), ["offset"] = S(7), ["pid"] = Pid(S(8))
                }.Clean();
            case 1002 when p.Count >= 2:
                return new JsonObject { ["app"] = FileOnly(S(0)), ["appVersion"] = S(1), ["pid"] = Pid(S(2)) }.Clean();
            case 1026:
                {
                    string text = string.Join("\n", p.Select(x => x?.ToString() ?? ""));
                    var o = new JsonObject();
                    var app = Regex.Match(text, @"(?m)^(?:Application|Anwendung):\s*(\S+)");
                    if (app.Success) o["app"] = FileOnly(app.Groups[1].Value);
                    var ex = Regex.Match(text, @"(?m)^(?:Exception Info|Ausnahmeinformationen):\s*([A-Za-z_][\w.`+]*)");
                    if (ex.Success) o["exceptionType"] = ex.Groups[1].Value.Length <= 200 ? ex.Groups[1].Value : ex.Groups[1].Value[..200];
                    return o.Count == 0 ? null : o;
                }
        }
        return null;
    }

    static string? FileOnly(string? s) => string.IsNullOrEmpty(s) ? s : Path.GetFileName(s.Replace('/', '\\'));

    static string? Code(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s;
        return uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v) ? ExitCodes.Hex(v) : null;
    }

    static long? Pid(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && long.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out var h)) return h;
        return long.TryParse(s, out var d) ? d : null;
    }
}
