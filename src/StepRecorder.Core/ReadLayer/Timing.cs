using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.ReadLayer;

/// <summary>
/// Reaktionszeiten eines Schritts (12, Paket 9) – aus Messwerten, die die Schritterkennung schon ablegt:
/// erste beobachtete Reaktion (<c>effect.latencyUs</c>), Bild ruhig (<c>sync.settleMs</c>), Mauszeiger beschäftigt
/// (<c>sync.busyMs</c>), bereit (<c>sync.readyMs</c>), Denkzeit bis zur nächsten Eingabe (<c>sync.thinkMs</c>).
/// Gemessen wird sichtbare Reaktion, nicht Rechenzeit. Endete die Beobachtung, bevor das Bild ruhig war, gilt der Wert
/// als Untergrenze (<c>atLeast</c>).
/// </summary>
public static class Timing
{
    /// <summary>Standardschwelle „langsam“ in Millisekunden (Editor-Filter, Qualitätsprüfung Q21).</summary>
    public const int SlowMs = 2000;

    public static JsonObject? Of(JsonObject step)
    {
        var eff = step.Obj("effect");
        var sync = step.Obj("sync");
        if (eff == null && sync == null) return null;
        var o = new JsonObject();
        if (eff?.LongN("latencyUs") is long lat) o["reactionMs"] = lat / 1000;
        if (sync?.LongN("settleMs") is long st) o["settleMs"] = st;
        if (sync?.LongN("busyMs") is long busy) o["busyMs"] = busy;
        if (sync?.Obj("waitWindow")?.LongN("afterMs") is long ww) o["windowAfterMs"] = ww;
        if (sync?.LongN("readyMs") is long ready) o["readyMs"] = ready;
        if (sync?.LongN("thinkMs") is long think) o["thinkMs"] = think;
        string? why = sync?.Str("settleReason") ?? eff?.Obj("screen")?.Str("settleReason");
        if (why is "maxWait" or "timeout" || eff?.Obj("screen") is { } scr && scr["settled"] is JsonValue sv && !sv.GetValue<bool>()) o["atLeast"] = true;
        if (o.Count == 0) return null;
        o["waitMs"] = WaitMs(o);
        return o;
    }

    /// <summary>Wartezeit des Schritts: bereit, sonst Bild ruhig, sonst erste Reaktion.</summary>
    public static long WaitMs(JsonObject timing) => timing.LongN("readyMs") ?? timing.LongN("settleMs") ?? timing.LongN("reactionMs") ?? 0;

    public static long? WaitMsOf(JsonObject step) => Of(step) is { } t ? WaitMs(t) : null;

    /// <summary>Lesbare Kurzform, z. B. „Reaktion 120 ms · bereit nach 2,4 s“ (Untergrenze: „länger als“).</summary>
    public static string Text(JsonObject timing, string lang)
    {
        bool de = lang == "de";
        var parts = new List<string>();
        if (timing.LongN("reactionMs") is long r) parts.Add((de ? "Reaktion " : "reaction ") + Ms(r, de));
        if (timing.LongN("busyMs") is long b && b > 0) parts.Add((de ? "beschäftigt " : "busy ") + Ms(b, de));
        long wait = WaitMs(timing);
        if (wait > 0) parts.Add((de ? "bereit nach " : "ready after ") + (timing.Bool("atLeast") ? (de ? "mehr als " : "more than ") : "") + Ms(wait, de));
        return string.Join(" · ", parts);
    }

    public static string Ms(long ms, bool de) => ms < 1000 ? $"{ms} ms" : (ms / 1000.0).ToString(ms < 10_000 ? "0.0" : "0", de ? System.Globalization.CultureInfo.GetCultureInfo("de-DE") : System.Globalization.CultureInfo.InvariantCulture) + " s";

    /// <summary>Summen einer Aufnahme: Wartezeit der Anwendung gegen Denkzeit der Person (Hauptschritte mit Messwerten).</summary>
    public static (long waitMs, long thinkMs, int slow) Totals(IEnumerable<JsonObject> steps, int slowMs = SlowMs)
    {
        long wait = 0, think = 0; int slow = 0;
        foreach (var s in steps)
        {
            if (Of(s) is not { } t) continue;
            long w = WaitMs(t);
            wait += w; think += t.LongN("thinkMs") ?? 0;
            if (w >= slowMs) slow++;
        }
        return (wait, think, slow);
    }
}
