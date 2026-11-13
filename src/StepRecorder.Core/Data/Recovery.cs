using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace StepRecorder.Data;

/// <summary>Wiederherstellung nach Absturz (Zustand <c>recording</c> ohne lebende Sperre), 04 §Integrität.</summary>
public static class Recovery
{
    public static void Recover(string folder)
    {
        string journal = Path.Combine(folder, "events.jsonl");
        if (!File.Exists(journal)) File.WriteAllText(journal, "");
        List<JsonObject> events; long validBytes; long fileLen;
        using (var fs = new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            fileLen = fs.Length;
            (events, _, validBytes) = Session.ReadJournal(fs);
        }
        long discarded = fileLen - validBytes;
        if (discarded > 0)
            using (var fs = new FileStream(journal, FileMode.Open, FileAccess.Write)) fs.SetLength(validBytes);

        long seq = events.Count;
        long lastT = events.Count == 0 ? 0 : events.Max(e => e.Long("t"));
        bool endsWithStop = events.Count > 0 && events[^1].Str("type") == "session.stop";
        int maxGap = events.Where(e => e.Str("type") == "gap").Select(e => int.TryParse(e.Str("gapId")?.TrimStart('g'), out var n) ? n : 0).DefaultIfEmpty(0).Max();

        var lines = new List<JsonObject>();
        if (!endsWithStop)
            lines.Add(new JsonObject { ["seq"] = ++seq, ["t"] = lastT, ["type"] = "gap", ["gapId"] = $"g{maxGap + 1}", ["phase"] = "begin", ["cause"] = "crash", ["tFrom"] = lastT, ["channels"] = new JsonArray("all"), ["inputLost"] = "unknown" });
        lines.Add(new JsonObject { ["seq"] = ++seq, ["t"] = lastT, ["type"] = "session.recovered", ["utc"] = Json.UtcNow(), ["lastValidSeq"] = events.Count, ["discardedBytes"] = discarded, ["tool"] = "StepRecorder " + AppInfo.Version });
        if (!endsWithStop)
            lines.Add(new JsonObject { ["seq"] = ++seq, ["t"] = lastT, ["type"] = "session.stop", ["reason"] = "recovered", ["utc"] = Json.UtcNow() });
        using (var w = new FileStream(journal, FileMode.Append, FileAccess.Write))
            foreach (var l in lines) { var b = Encoding.UTF8.GetBytes(Json.Line(l) + "\n"); w.Write(b); }

        var manifest = Json.ReadObject(Path.Combine(folder, "manifest.json")) ?? new JsonObject();
        manifest["state"] = "recovered";
        var created = Json.ParseUtc(manifest.Str("createdUtc"));
        manifest["endedUtc"] = Json.Utc(created.AddTicks(lastT * 10));
        manifest["durationUs"] = lastT;
        manifest["journal"] = new JsonObject
        {
            ["segments"] = new JsonArray(new JsonObject { ["file"] = "events.jsonl", ["firstSeq"] = 1, ["lastSeq"] = seq, ["bytes"] = new FileInfo(journal).Length, ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(journal))).ToLowerInvariant() }),
            ["events"] = seq, ["lastSeq"] = seq, ["lastT"] = lastT
        };
        if (manifest["parts"] is JsonArray parts && parts.Count > 0 && parts[^1] is JsonObject lp && lp["endUtc"] == null)
        { lp["endUtc"] = manifest["endedUtc"]!.DeepClone(); lp["tEnd"] = lastT; }
        var hist = manifest["history"] as JsonArray ?? new JsonArray();
        hist.Add(new JsonObject { ["atUtc"] = Json.UtcNow(), ["action"] = "recovered", ["tool"] = "StepRecorder " + AppInfo.Version, ["detail"] = new JsonObject { ["lastValidSeq"] = events.Count, ["discardedBytes"] = discarded } });
        manifest["history"] = hist;
        Json.WriteAtomic(Path.Combine(folder, "manifest.json"), Json.Pretty(manifest));
        foreach (var f in new[] { "session.lock", "session.reserve" })
            try { File.Delete(Path.Combine(folder, f)); } catch { }
    }
}

public static class AppInfo
{
    /// <summary>Technischer Name (Datenformat, Manifest, Prozess) – nicht ändern.</summary>
    public const string Name = "StepRecorder";
#if ANONYMOUS
    // Anonyme Ausgabe (dotnet publish -p:Anonymous=true): ohne Namen des Urhebers und ohne Verweis auf die Projektseite
    public const string ExeName = "StepRecorder.exe";
    public const string DisplayName = "Step Recorder";
    public const bool Anonymous = true;
#else
    /// <summary>Dateiname des Programms.</summary>
    public const string ExeName = "SebastiansStepRecorder.exe";
    /// <summary>Name in der Oberfläche.</summary>
    public const string DisplayName = "Sebastians Step Recorder";
    public const bool Anonymous = false;
#endif
    public static string Version => typeof(AppInfo).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.1.0";
    /// <summary>Prozessarchitektur für manifest.json (<c>x64</c> | <c>arm64</c>).</summary>
    public static string Arch => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
}
