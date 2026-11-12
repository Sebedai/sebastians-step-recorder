using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace StepRecorder.Data;

/// <summary>
/// Einziger Schreiber eines Sitzungsordners während der Aufnahme: vergibt <c>seq</c>, hängt an
/// <c>events.jsonl</c> an (nur anhängen), schreibt Bild-Blobs vor ihrem Ereignis, führt die
/// Prüfsummenkette der Checkpoints und hält <c>manifest.json</c> aktuell.
/// </summary>
public sealed class SessionWriter : IDisposable
{
    public const string FormatName = "steprec";
    public const string SchemaVersion = "1.0";
    const long ReserveBytes = 8L * 1024 * 1024;

    readonly object _lock = new();
    readonly FileStream _journal;
    readonly IncrementalHash _chainHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    byte[] _chain = new byte[32];
    long _linesSinceCheckpoint;
    long _seq;
    long _lastT;
    long _bytes;
    DateTime _lastCheckpoint = DateTime.UtcNow;
    DateTime _lastFlush = DateTime.UtcNow;
    readonly HashSet<string> _blobs = new();
    public int FramesWritten, KeyFrames, BlobsWritten; public long BlobBytes;

    public string Folder { get; }
    public JsonObject Manifest { get; }
    public long LastSeq { get { lock (_lock) return _seq; } }
    public bool Closed { get; private set; }

    SessionWriter(string folder, JsonObject manifest, FileStream journal)
    {
        Folder = folder; Manifest = manifest; _journal = journal;
        _chainHash.AppendData(_chain); // chain(0) = 32 Nullbytes
    }

    /// <summary>Legt einen neuen Sitzungsordner an (Manifest und Sperrdatei vor dem ersten Ereignis).</summary>
    public static SessionWriter Create(string folder, JsonObject manifest)
    {
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder, "frames"));
        File.WriteAllText(Path.Combine(folder, "session.lock"),
            $"{{\"pid\":{Environment.ProcessId},\"startedUtc\":\"{Json.UtcNow()}\",\"mode\":\"record\"}}\n");
        try
        {
            using var r = new FileStream(Path.Combine(folder, "session.reserve"), FileMode.Create);
            r.SetLength(ReserveBytes);
        }
        catch { /* Reserve ist optional */ }
        Json.WriteAtomic(Path.Combine(folder, "manifest.json"), Json.Pretty(manifest));
        var fs = new FileStream(Path.Combine(folder, "events.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1 << 16);
        return new SessionWriter(folder, manifest, fs);
    }

    /// <summary>Hängt ein Ereignis an; vergibt <c>seq</c> und gibt es zurück.</summary>
    public long Append(JsonObject ev)
    {
        lock (_lock)
        {
            if (Closed) return 0;
            long seq = ++_seq;
            // seq, t, type zuerst
            var ordered = new JsonObject { ["seq"] = seq };
            if (ev.TryGetPropertyValue("t", out var t)) { ev.Remove("t"); ordered["t"] = t; }
            if (ev.TryGetPropertyValue("type", out var ty)) { ev.Remove("type"); ordered["type"] = ty; }
            foreach (var kv in ev.ToList()) { ev.Remove(kv.Key); if (kv.Value != null) ordered[kv.Key] = kv.Value; }
            WriteLine(ordered);
            long tv = ordered.Long("t");
            if (tv > _lastT) _lastT = tv;
            MaybeFlush();
            return seq;
        }
    }

    void WriteLine(JsonObject o)
    {
        var bytes = Encoding.UTF8.GetBytes(Json.Line(o) + "\n");
        _journal.Write(bytes);
        _chainHash.AppendData(bytes);
        _linesSinceCheckpoint++;
        _bytes += bytes.Length;
    }

    void MaybeFlush()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastFlush).TotalMilliseconds >= 250) { _journal.Flush(); _lastFlush = now; }
        if ((now - _lastCheckpoint).TotalSeconds >= 30) CheckpointLocked(_lastT);
    }

    /// <summary>Regelmäßig aufrufen (z. B. vom Wächter), damit Zeilen binnen 250 ms beim System sind.</summary>
    public void Tick(long tNow)
    {
        lock (_lock)
        {
            if (Closed) return;
            _journal.Flush();
            _lastFlush = DateTime.UtcNow;
            if ((DateTime.UtcNow - _lastCheckpoint).TotalSeconds >= 30) CheckpointLocked(tNow);
        }
    }

    public void Checkpoint(long t) { lock (_lock) { if (!Closed) CheckpointLocked(t); } }

    void CheckpointLocked(long t)
    {
        // chain(n) = SHA-256(chain(n-1) ‖ Zeilen seit dem letzten Checkpoint)
        // Der inkrementelle Hash wurde mit der vorherigen Kette begonnen und enthält alle Zeilen seitdem.
        _chain = _chainHash.GetHashAndReset();
        long seq = ++_seq;
        var cp = new JsonObject
        {
            ["seq"] = seq, ["t"] = Math.Max(t, _lastT), ["type"] = "session.checkpoint", ["utc"] = Json.UtcNow(),
            ["lines"] = _linesSinceCheckpoint, ["chain"] = "sha256:" + Convert.ToHexString(_chain).ToLowerInvariant()
        };
        var bytes = Encoding.UTF8.GetBytes(Json.Line(cp) + "\n");
        _journal.Write(bytes);
        _bytes += bytes.Length;
        _linesSinceCheckpoint = 0;
        _chainHash.AppendData(_chain); // nächstes Intervall beginnt mit der aktuellen Kette (die Checkpoint-Zeile selbst zählt nicht)
        _journal.Flush(true);
        _lastCheckpoint = DateTime.UtcNow;
    }

    /// <summary>Schreibt einen PNG-Blob inhaltsadressiert (vor dem zugehörigen Ereignis).</summary>
    public string WriteBlob(byte[] png)
    {
        string hex = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
        string rel = $"frames/{hex[..2]}/{hex}.png";
        lock (_blobs)
        {
            if (_blobs.Add(hex))
            {
                string path = Path.Combine(Folder, "frames", hex[..2], hex + ".png");
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    string tmp = path + ".tmp";
                    File.WriteAllBytes(tmp, png);
                    File.Move(tmp, path, true);
                    BlobsWritten++; BlobBytes += png.Length;
                }
            }
        }
        _ = rel;
        return "sha256:" + hex;
    }

    public void SaveManifest()
    {
        lock (_lock) Json.WriteAtomic(Path.Combine(Folder, "manifest.json"), Json.Pretty(Manifest));
    }

    /// <summary>Schließt die Aufnahme: Checkpoint, Manifest abschließen, Sperr- und Reservedatei entfernen.</summary>
    public void Close(long tEnd, Action<JsonObject>? finalizeManifest = null)
    {
        lock (_lock)
        {
            if (Closed) return;
            CheckpointLocked(tEnd);
            _journal.Flush(true);
            _journal.Dispose();
            Closed = true;
        }
        var journal = new JsonObject
        {
            ["segments"] = new JsonArray(new JsonObject
            {
                ["file"] = "events.jsonl", ["firstSeq"] = 1, ["lastSeq"] = _seq, ["bytes"] = _bytes,
                ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Folder, "events.jsonl")))).ToLowerInvariant()
            }),
            ["events"] = _seq, ["lastSeq"] = _seq, ["lastT"] = _lastT
        };
        Manifest["journal"] = journal;
        Manifest["frames"] = new JsonObject { ["records"] = FramesWritten, ["blobs"] = BlobsWritten, ["keyframes"] = KeyFrames, ["bytes"] = BlobBytes };
        finalizeManifest?.Invoke(Manifest);
        Json.WriteAtomic(Path.Combine(Folder, "manifest.json"), Json.Pretty(Manifest));
        TryDelete(Path.Combine(Folder, "session.reserve"));
        TryDelete(Path.Combine(Folder, "session.lock"));
    }

    /// <summary>Gibt die Notreserve frei (bei vollem Datenträger).</summary>
    public void ReleaseReserve() => TryDelete(Path.Combine(Folder, "session.reserve"));

    static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }

    public void Dispose()
    {
        lock (_lock) { if (!Closed) { try { _journal.Flush(true); _journal.Dispose(); } catch { } Closed = true; } }
        _chainHash.Dispose();
    }
}
