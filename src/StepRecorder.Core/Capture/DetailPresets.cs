namespace StepRecorder.Capture;

/// <summary>
/// Detailgrad eines Aufnahmemodus: welche Eingabearten gespeichert werden, wann Zusatzbilder ohne Eingabe entstehen
/// und ob der erweiterte Programmkontext (Menüpfade, Listen, Browser-Adresse, Office, Statusleiste …) gelesen wird.
/// </summary>
/// <param name="Bars">Stufe 1–4 für die Anzeige.</param>
public sealed record DetailPreset(string Id, string Level, int Bars, string[] Off, string ChangeFrames, bool AppContext);

public static class DetailPresets
{
    /// <summary>Anleitung: nur was Menschen zum Nachmachen brauchen – keine Mauswege, Zusatzbilder nur bei großen Änderungen, Basis-Kontext.</summary>
    public static readonly DetailPreset Guide = new("guide", "Schlank", 2, new[] { "moves" }, "few", false);
    /// <summary>Support-Fall: alle Eingaben, Zusatzbilder auch bei kleinen Änderungen, erweiterter Kontext.</summary>
    public static readonly DetailPreset Support = new("support", "Ausführlich", 3, Array.Empty<string>(), "normal", true);
    /// <summary>KI-Spur: alles, Zusatzbilder bei jeder sichtbaren Änderung, erweiterter Kontext.</summary>
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

    /// <summary>
    /// Zusatzbilder ohne Eingabe: ab welchem Anteil geänderter Bildschirmfläche und mit welchem Mindestabstand.
    /// Einzige Quelle der Zahlen – die Bildaufnahme (FrameEngine) liest sie von hier.
    /// </summary>
    public static (double ratio, int gapMs, string name, string size, string example) ChangeRule(string v) => v switch
    {
        "few" => (0.02, 2000, "Nur große Änderungen", "ab ca. 2 % des Bildschirms (etwa 200 × 200 Pixel bei Full HD)", "z. B. ein neues Fenster oder ein Seitenwechsel"),
        "many" => (0.0001, 250, "Jede sichtbare Änderung", "ab ca. 0,01 % des Bildschirms (etwa 15 × 15 Pixel)", "z. B. Fortschrittsbalken, Zähler oder Ladeanimationen"),
        _ => (0.001, 500, "Auch kleine Änderungen", "ab ca. 0,1 % des Bildschirms (etwa 45 × 45 Pixel)", "z. B. eine Meldung, ein Tooltip oder eine Statusanzeige"),
    };

    /// <summary>Kurzname für Anzeigen, z. B. „Nur große Änderungen“.</summary>
    public static string ChangeFramesName(string v) => ChangeRule(v).name;

    /// <summary>Vollständige Regel für Zusatzbilder in einem Satz.</summary>
    public static string ChangeFramesRule(string v, RecordingOptions? o = null)
    {
        var r = ChangeRule(v);
        double quiet = (o?.SettleQuietMs ?? 300) / 1000.0;
        return $"Zusatzbild, wenn sich der Bildschirm ohne Eingabe ändert – {r.size}, {r.example} –, sobald er danach {Sec(quiet)} ruhig ist " +
               $"(bei Dauerbewegung spätestens nach 5 Sekunden); höchstens alle {Sec(r.gapMs / 1000.0)}.";
    }

    /// <summary>Was in jedem Modus gilt: Vorher- und Nachher-Bild zu jeder Eingabe.</summary>
    public static string BaseRule(RecordingOptions? o = null)
    {
        double quiet = (o?.SettleQuietMs ?? 300) / 1000.0, max = (o?.SettleMaxMs ?? 3000) / 1000.0;
        return $"Immer: ein Bild direkt vor jeder Eingabe und eins, sobald der Bildschirm danach {Sec(quiet)} ruhig ist (spätestens nach {Sec(max)}).";
    }

    static string Sec(double s) => (s == 1 ? "1 Sekunde" : s.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + " Sekunden");
}
