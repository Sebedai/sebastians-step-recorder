using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Data;
using StepRecorder.Text;

namespace StepRecorder.ReadLayer;

/// <summary>
/// Formatierter Text in der Leseschicht (13, Paket 21; 10 §10.4): Schwärzungen und Verallgemeinerungen wirken vor dem Auswerten der
/// Auszeichnung (eingesetzte Platzhalter sind unteilbar) und noch einmal auf dem sichtbaren Ergebnis. Ergibt erst das Auswerten einen
/// geschützten Wert – etwa weil Sterne ihn geteilt haben –, wird er bereinigt; lässt sich das nicht mit der Formatierung vereinbaren,
/// entfällt die Formatierung, nie der Schutz.
/// </summary>
public sealed partial class SessionView
{
    /// <summary>Gilt dieser Text als formatiert? Nur mit <c>fmt: 1</c> und nicht von einem Agenten geschrieben.</summary>
    public static bool IsRich(JsonObject? holder, string fmtKey = "fmt") => holder != null && holder.Int(fmtKey) == 1 && holder.Str("by") != "mcp";

    /// <summary>Wie <see cref="Clean"/>, aber jede Ersetzung zwischen U+FDD0 und U+FDD1 gefasst.</summary>
    string CleanMarked(string text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        string Mark(string s) => RichDoc.MarkOpen + s + RichDoc.MarkClose;
        foreach (var r in _redactedStrings.OrderByDescending(s => s.Length)) text = text.Replace(r, Mark(RedactedText), StringComparison.Ordinal);
        foreach (var (rx, ph) in _subst) { try { string m = Mark(ph); text = rx.Replace(text, _ => m); } catch { } }
        return MaskUnreviewed(text);
    }

    static string Unmark(string s) => s.Replace(RichDoc.MarkOpen.ToString(), "").Replace(RichDoc.MarkClose.ToString(), "");

    static readonly Regex Controls = new(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F﷐﷑]", RegexOptions.Compiled);

    /// <summary>
    /// Formatierten Autorentext freigeben. Reihenfolge: normalisieren → schwärzen (markiert) → auswerten → Links prüfen →
    /// sichtbaren Text erneut bereinigen → notfalls je Lauf bereinigen → sonst ohne Formatierung.
    /// </summary>
    public RichText SealRich(string? raw)
    {
        // Variablen zuerst (maskiert eingesetzt): ihre Werte durchlaufen Schwärzung und Verallgemeinerung wie jeder andere Text
        string s0 = Controls.Replace(ExpandVars((raw ?? "").Replace("\r\n", "\n").Replace('\r', '\n'), escape: true), "");
        string clean = Clean(s0);
        string s1 = CleanMarked(s0);
        if (Unmark(s1) != clean) s1 = clean;
        var doc = RichDoc.Parse(s1);
        // Linkziele: nur, wenn kein geschützter Wert darin steckt
        doc = doc.Map(x => x.Kind == RichKind.Link && (x.Target == null || Clean(x.Target) != x.Target) ? x with { Kind = RichKind.Text, Target = null } : x);
        string p1 = doc.Projection(), p2 = Clean(p1);
        if (p2 == p1) return new RichText(doc, false);
        var d2 = doc.Map(x => x.Kind == RichKind.Ref ? x : x with { Text = Clean(x.Text) });
        if (d2.Projection() == p2) return new RichText(d2, false);
        return RichText.FromPlain(p2, degraded: true);
    }

    /// <summary>Bereinigter Hinweis; ein formatierter Hinweis trägt danach seinen sichtbaren Text, die Formatierung steht in <see cref="ViewStep.NoteRich"/>.</summary>
    JsonObject CleanComment(JsonObject c, ViewStep vs)
    {
        var o = (JsonObject)CleanNode(c)!;
        if (IsRich(c) && c.Str("text") is { } raw && c.Str("id") is { } id)
        {
            var r = SealRich(raw);
            o["text"] = r.Plain;
            (vs.NoteRich ??= new())[id] = r;
        }
        else if (c.Str("text") is { } plain && plain.Contains("{{")) o["text"] = CleanVars(plain);
        return o;
    }

    /// <summary>Text eines Feldes: formatiert, wenn gekennzeichnet, sonst <c>null</c> (dann gilt der bereinigte reine Text).</summary>
    public RichText? RichOf(string? raw, bool rich) => rich && raw != null ? SealRich(raw) : null;


    /// <summary>Formatierte Fassung eines Dokumenttexts (Einleitung, Abschluss) in der Sprache der Ansicht, oder <c>null</c>.</summary>
    public RichText? GuideRich(string key, string lang)
    {
        var guide = Edits.Obj("guide");
        if (guide == null) return null;
        if (lang != "de" && guide.Obj("i18n")?.Obj(lang) is { } loc && loc.Str(key) is { Length: > 0 } lt)
            return loc.Obj("fmt")?.Int(key) == 1 ? SealRich(lt) : null;
        return guide.Obj("fmt")?.Int(key) == 1 && guide.Str(key) is { Length: > 0 } t ? SealRich(t) : null;
    }
}
