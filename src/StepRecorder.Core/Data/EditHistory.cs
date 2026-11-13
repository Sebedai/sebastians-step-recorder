using System.Globalization;
using System.Text.Json.Nodes;
using StepRecorder.Settings;

namespace StepRecorder.Data;

/// <summary>Ein gespeicherter Bearbeitungsstand.</summary>
public sealed record EditSnapshot(string Path, DateTime Utc, long Revision, string Reason, long Bytes);

/// <summary>
/// Bearbeitungsstände (3.4): Kopien von <c>edits.json</c> unter <c>history\edits-&lt;UTC&gt;-r&lt;Revision&gt;-&lt;Anlass&gt;.json</c>.
/// Beim ersten Speichern nach dem Öffnen, danach höchstens alle 10 Minuten, erzwungen vor großen Änderungen.
/// Höchstens 40 Stände und 15 MB. Ein Fehler beim Sichern blockiert das Speichern nie.
/// </summary>
public static class EditHistory
{
    public const int MaxCount = 40;
    public const long MaxBytes = 15L * 1024 * 1024;
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    /// <summary>Bleiben beim Wiederherstellen standardmäßig auf heutigem Stand.</summary>
    public static readonly string[] ProtectionKeys = { "redactions", "substitutions", "review" };
    static readonly string[] MetaKeys = { "format", "schemaVersion", "sessionId", "revision", "savedAtUtc", "savedBy" };

    static string Dir(string folder) => Path.Combine(folder, "history");

    /// <summary>Sichert <paramref name="onDisk"/> (den Stand vor dem Speichern), wenn fällig. true = gesichert.</summary>
    public static bool MaybeSnapshot(string folder, JsonObject? onDisk, string reason, bool force, DateTime? nowUtc = null)
    {
        if (onDisk == null || onDisk.Long("revision") <= 0) return false;
        var now = nowUtc ?? DateTime.UtcNow;
        var newest = List(folder).FirstOrDefault();
        if (newest != null && newest.Revision == onDisk.Long("revision")) return false;          // gleicher Stand schon gesichert
        if (!force && newest != null && now - newest.Utc < Interval) return false;
        reason = new string((reason.Length == 0 ? "auto" : reason).Where(char.IsLetterOrDigit).Take(20).ToArray());
        Directory.CreateDirectory(Dir(folder));
        string file = Path.Combine(Dir(folder), $"edits-{now:yyyyMMddTHHmmssfff}Z-r{onDisk.Long("revision")}-{reason}.json");
        Json.WriteAtomic(file, Json.Pretty(onDisk));
        Trim(folder);
        return true;
    }

    /// <summary>Älteste Stände löschen, bis höchstens <see cref="MaxCount"/> und <see cref="MaxBytes"/> übrig sind.</summary>
    static void Trim(string folder)
    {
        var all = List(folder);
        long total = all.Sum(s => s.Bytes);
        foreach (var s in all.AsEnumerable().Reverse())
        {
            if (all.Count <= MaxCount && total <= MaxBytes) break;
            try { File.Delete(s.Path); } catch { }
            all.Remove(s); total -= s.Bytes;
        }
    }

    /// <summary>Alle Stände, neueste zuerst.</summary>
    public static List<EditSnapshot> List(string folder)
    {
        var list = new List<EditSnapshot>();
        var dir = Dir(folder);
        if (!Directory.Exists(dir)) return list;
        foreach (var f in Directory.EnumerateFiles(dir, "edits-*.json"))
        {
            var parts = Path.GetFileNameWithoutExtension(f).Split('-');
            if (parts.Length < 4 || !DateTime.TryParseExact(parts[1], "yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc)) continue;
            long rev = long.TryParse(parts[2].TrimStart('r'), out var r) ? r : 0;
            list.Add(new EditSnapshot(f, utc, rev, parts[3], new FileInfo(f).Length));
        }
        return list.OrderByDescending(s => s.Utc).ThenByDescending(s => s.Revision).ToList();
    }

    public static JsonObject? Load(EditSnapshot s) => Json.ReadObject(s.Path);

    /// <summary>
    /// Inhalt des Stands mit den Kopfdaten von heute. <paramref name="keepProtection"/>: Schwärzungen, Verallgemeinerungen
    /// und Durchsicht bleiben wie jetzt – ein alter Stand macht keinen Schutz rückgängig.
    /// </summary>
    public static JsonObject Restore(JsonObject current, JsonObject snapshot, bool keepProtection = true)
    {
        var r = new JsonObject();
        foreach (var kv in snapshot) if (!MetaKeys.Contains(kv.Key)) r[kv.Key] = kv.Value?.DeepClone();
        foreach (var k in MetaKeys) if (current[k] != null) r[k] = current[k]!.DeepClone();
        if (keepProtection)
            foreach (var k in ProtectionKeys)
            {
                r.Remove(k);
                if (current[k] != null) r[k] = current[k]!.DeepClone();
            }
        return r;
    }

    static int TextCount(JsonObject e) => (e.Arr("steps") ?? new JsonArray()).OfType<JsonObject>().Count(s => s.Obj("text") != null) + (e.Arr("merges") ?? new JsonArray()).OfType<JsonObject>().Count(m => m.Obj("text") != null);
    static int Count(JsonObject e, string key) => e.Arr(key)?.Count ?? 0;

    /// <summary>Kurzbeschreibung eines Stands: eigene Texte, Markierungen, Kommentare, ausgeblendete Schritte …</summary>
    public static string Summarize(JsonObject e)
    {
        var parts = new List<string>();
        void P(int n, string deOne, string deMany, string enOne, string enMany) { if (n > 0) parts.Add(Loc.N(n, deOne, deMany, enOne, enMany)); }
        P(TextCount(e), "1 eigener Text", "{0} eigene Texte", "1 own text", "{0} own texts");
        P(Count(e, "annotations"), "1 Zeichnung", "{0} Zeichnungen", "1 drawing", "{0} drawings");
        P(Count(e, "comments"), "1 Kommentar", "{0} Kommentare", "1 comment", "{0} comments");
        P((e.Arr("steps") ?? new JsonArray()).OfType<JsonObject>().Count(s => s.Bool("hidden")), "1 ausgeblendet", "{0} ausgeblendet", "1 hidden", "{0} hidden");
        P(Count(e, "merges"), "1 Zusammenfassung", "{0} Zusammenfassungen", "1 merge", "{0} merges");
        P(Count(e, "redactions"), "1 Schwärzung", "{0} Schwärzungen", "1 redaction", "{0} redactions");
        if (e.Obj("guide")?.Str("title") is { Length: > 0 } t) parts.Insert(0, Loc.F("Titel „{0}“", "title \"{0}\"", t.Length > 40 ? t[..40] + "…" : t));
        return parts.Count == 0 ? Loc.L("keine Bearbeitungen", "no edits") : string.Join(" · ", parts);
    }

    /// <summary>Was sich von <paramref name="from"/> nach <paramref name="to"/> ändert (für die Vorschau beim Wiederherstellen).</summary>
    public static List<string> Diff(JsonObject from, JsonObject to)
    {
        var l = new List<string>();
        void D(int a, int b, string de, string en) { if (a != b) l.Add(Loc.F(de, en, b - a > 0 ? "+" + (b - a) : (b - a).ToString(CultureInfo.InvariantCulture))); }
        D(TextCount(from), TextCount(to), "eigene Texte {0}", "own texts {0}");
        D(Count(from, "annotations"), Count(to, "annotations"), "Zeichnungen {0}", "drawings {0}");
        D(Count(from, "comments"), Count(to, "comments"), "Kommentare {0}", "comments {0}");
        D(Count(from, "inserted"), Count(to, "inserted"), "eingefügte Schritte {0}", "inserted steps {0}");
        D(Count(from, "merges"), Count(to, "merges"), "Zusammenfassungen {0}", "merges {0}");
        if ((from.Arr("order")?.ToJsonString() ?? "") != (to.Arr("order")?.ToJsonString() ?? "")) l.Add(Loc.L("andere Reihenfolge", "different order"));
        if ((from.Obj("guide")?.ToJsonString() ?? "") != (to.Obj("guide")?.ToJsonString() ?? "")) l.Add(Loc.L("Titelseite und Einleitung geändert", "title page and introduction changed"));
        if ((from.Arr("steps")?.ToJsonString() ?? "") != (to.Arr("steps")?.ToJsonString() ?? "") && l.Count == 0) l.Add(Loc.L("Schritt-Einstellungen geändert", "step settings changed"));
        return l;
    }

    /// <summary>Alle Stände löschen (nach endgültigem Schwärzen: alte Stände könnten Geschwärztes noch beschreiben).</summary>
    public static void Purge(string folder)
    {
        try { var d = Dir(folder); if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
    }
}
