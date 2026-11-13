using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.ReadLayer;

/// <summary>
/// Fertigstellung und Freigabe (13, Paket 30). <c>guide.status = { state: draft|released, releasedUtc, fingerprint, version }</c>.
/// Der Fingerabdruck fasst das Overlay ohne flüchtige Schlüssel zusammen; jede inhaltliche Änderung nach der Freigabe macht aus
/// „Freigegeben“ wieder „Entwurf“. Ohne <c>guide.status</c> gibt es keinen Status und kein Wasserzeichen.
/// </summary>
public static class GuideStatus
{
    public sealed record Item(string Id, string De, string En, bool Done, bool Required, int Count);

    static readonly string[] Volatile = { "revision", "savedAtUtc", "savedBy", "qualityIgnores", "ui", "format", "schemaVersion", "sessionId" };

    public static string Fingerprint(JsonObject edits)
    {
        var c = (JsonObject)edits.DeepClone();
        foreach (var k in Volatile) c.Remove(k);
        if (c["guide"] is JsonObject g) g.Remove("status");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(c.ToJsonString())))[..16].ToLowerInvariant();
    }

    /// <summary>none (kein Status) | draft | released | changed (freigegeben, seither geändert = wieder Entwurf).</summary>
    public static string State(SessionView view)
    {
        var st = view.Edits.Obj("guide")?.Obj("status");
        if (st == null) return "none";
        if (st.Str("state") != "released") return "draft";
        return st.Str("fingerprint") == Fingerprint(view.Edits) ? "released" : "changed";
    }

    /// <summary>Wasserzeichen „Entwurf“: nur mit Status, nicht freigegeben, nicht abgeschaltet.</summary>
    public static bool Watermark(SessionView view) => State(view) is "draft" or "changed" && view.Edits.Obj("guide")?.Obj("doc")?.Str("watermark") != "off";

    /// <summary>Prüfliste der Fertigstellung; Pflichtpunkte müssen für die Freigabe erledigt sein.</summary>
    public static List<Item> Readiness(SessionView view, List<QualityFinding>? findings = null)
    {
        findings ??= QualityCheck.Run(view);
        int N(params string[] rules) => findings.Count(f => !f.Ignored && rules.Contains(f.Rule));
        var guide = view.Edits.Obj("guide");
        bool title = guide?.Str("title") is { Length: > 0 };
        var list = new List<Item>
        {
            new("review", "Auffällige Eingaben durchgesehen", "Flagged inputs reviewed", N("Q12") == 0, true, N("Q12")),
            new("title", "Eigener Titel vergeben", "Own title set", title, true, title ? 0 : 1),
            new("texts", "Jeder Schritt hat einen Text", "Every step has a text", N("Q10") == 0, true, N("Q10")),
            new("images", "Kein Bild fehlt", "No image is missing", N("Q16") == 0, true, N("Q16")),
            new("refs", "Variablen und Querverweise aufgelöst, keine TODO", "Variables and cross-references resolved, no TODO", N("Q31", "Q32") == 0, true, N("Q31", "Q32")),
            new("intro", "Einleitung geschrieben", "Introduction written", guide?.Str("intro") is { Length: > 0 }, false, 0),
            new("version", "Version und Autor eingetragen", "Version and author entered", guide?.Obj("doc")?.Str("version") is { Length: > 0 } && guide?.Obj("doc")?.Str("author") is { Length: > 0 }, false, 0),
            new("style", "Keine Stilhinweise", "No style hints", N("Q22", "Q23", "Q24", "Q25", "Q43", "Q39") == 0, false, N("Q22", "Q23", "Q24", "Q25", "Q43", "Q39")),
            new("translate", "Übersetzung vollständig", "Translation complete", N("Q13") == 0, false, N("Q13")),
            new("privacy", "Persönliche Werte verallgemeinert", "Personal values generalised", N("Q14") == 0, false, N("Q14")),
        };
        return list;
    }

    public static bool CanRelease(IEnumerable<Item> items) => items.All(i => !i.Required || i.Done);

    /// <summary>Freigeben: Status „released“ mit Fingerabdruck und Version. Liefert false, wenn ein Pflichtpunkt offen ist.</summary>
    public static bool Release(SessionView view, List<Item> items)
    {
        if (!CanRelease(items)) return false;
        var g = view.Edits["guide"] as JsonObject ?? new JsonObject(); view.Edits["guide"] = g;
        g["status"] = new JsonObject { ["state"] = "released", ["releasedUtc"] = Json.UtcNow(), ["version"] = g.Obj("doc")?.Str("version") ?? "" };
        ((JsonObject)g["status"]!)["fingerprint"] = Fingerprint(view.Edits);
        return true;
    }

    public static void SetDraft(JsonObject edits)
    {
        var g = edits["guide"] as JsonObject ?? new JsonObject(); edits["guide"] = g;
        g["status"] = new JsonObject { ["state"] = "draft" };
    }
}
