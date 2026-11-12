namespace StepRecorder.Capture;

/// <summary>
/// Detailgrad eines Aufnahmemodus: welche Eingabearten gespeichert werden, wie viele Zwischenbilder entstehen
/// und ob der erweiterte Programmkontext (Menüpfade, Listen, Browser-Adresse, Office, Statusleiste …) gelesen wird.
/// </summary>
/// <param name="Bars">Stufe 1–4 für die Anzeige.</param>
public sealed record DetailPreset(string Id, string Level, int Bars, string[] Off, string ChangeFrames, bool AppContext);

public static class DetailPresets
{
    /// <summary>Anleitung: nur was Menschen zum Nachmachen brauchen – keine Mauswege, wenige Zwischenbilder, Basis-Kontext.</summary>
    public static readonly DetailPreset Guide = new("guide", "Schlank", 2, new[] { "moves" }, "few", false);
    /// <summary>Support-Fall: alle Eingaben, normale Zwischenbilder, erweiterter Kontext.</summary>
    public static readonly DetailPreset Support = new("support", "Ausführlich", 3, Array.Empty<string>(), "normal", true);
    /// <summary>KI-Spur: alles, viele Zwischenbilder, erweiterter Kontext.</summary>
    public static readonly DetailPreset AiTrace = new("aiTrace", "Vollständig", 4, Array.Empty<string>(), "many", true);

    public static DetailPreset? For(string profileId) => profileId switch { "guide" => Guide, "support" => Support, "aiTrace" => AiTrace, _ => null };

    public static void Apply(RecordingOptions o, DetailPreset p)
    {
        o.Off = new HashSet<string>(p.Off);
        o.ChangeFrames = p.ChangeFrames;
        o.AppContext = p.AppContext;
        o.DetailLevel = p.Level;
    }

    /// <summary>Stufe 1–4 aus einer freien Auswahl (für die Anzeige im Modus „Eigene“).</summary>
    public static int BarsFor(IEnumerable<string> off, string changeFrames, bool appContext)
    {
        int on = RecordingOptions.Channels.Count(c => !off.Contains(c.key));
        double score = on / (double)RecordingOptions.Channels.Length * 2 + (changeFrames switch { "few" => 0, "many" => 1, _ => 0.5 }) + (appContext ? 1 : 0);
        return Math.Clamp((int)Math.Round(score), 1, 4);
    }

    public static string ChangeFramesName(string v) => v switch { "few" => "wenige", "many" => "viele", _ => "normal" };
}
