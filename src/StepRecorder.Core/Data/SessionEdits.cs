using System.Text.Json.Nodes;

namespace StepRecorder.Data;

/// <summary>Kleine Overlay-Änderungen ohne Leseansicht (Bibliothek): nur mit Revisionsprüfung, Zwischenspeicher wird verworfen.</summary>
public static class SessionEdits
{
    /// <summary>Setzt den Titel der Anleitung (<c>edits.guide.title</c>); der Ordnername bleibt. false = gleichzeitig geändert oder schreibgeschützt.</summary>
    public static bool SetTitle(string folder, string title)
    {
        title = title.Trim();
        if (title.Length == 0 || title.Length > 300) throw new ArgumentException(nameof(title));
        using var s = Session.Open(folder, recover: false);
        if (s.ReadOnly || s.State == "recording") return false;
        var e = s.LoadEdits() ?? new JsonObject { ["revision"] = 0 };
        long rev = e.Long("revision");
        var g = e.Obj("guide") ?? new JsonObject();
        g["title"] = title;
        e["guide"] = g;
        ClearCache(folder);
        bool ok = s.SaveEdits(e, rev, "library");
        if (ok) ClearCache(folder);
        return ok;
    }

    static void ClearCache(string folder)
    {
        try { var c = Path.Combine(folder, "cache"); if (Directory.Exists(c)) Directory.Delete(c, true); } catch { }
    }
}
