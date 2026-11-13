using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.Capture;

/// <summary>
/// Einstellungen eines weiteren Aufnahmeabschnitts (03 §1.7, AUF-104/106): Er übernimmt die wirksamen Einstellungen seines
/// Bezugsabschnitts und den Aufnahmebereich, der beim ersten zu ersetzenden Schritt galt. Die Ausschlussliste wird mit der
/// aktuellen vereinigt (sie kann nur wachsen). Ziel-Angaben und Profil werden nicht übernommen.
/// </summary>
public static class SectionOptions
{
    /// <param name="current">Optionen aus den aktuellen Einstellungen (Kürzel, Erinnerungen, Ausschlussliste, Schutzstufe der Einstellungen).</param>
    public static RecordingOptions For(Session s, int section, long atSeq, RecordingOptions current)
    {
        var parts = SessionParts.Read(s);
        var part = parts.FirstOrDefault(p => p.Part == section) ?? parts.FirstOrDefault();
        var start = part != null ? s.BySeq(part.FirstSeq) : null;
        var settings = start?.Obj("settings") ?? s.Manifest.Obj("settings") ?? new JsonObject();
        var o = new RecordingOptions
        {
            Language = settings.Str("language") ?? current.Language,
            PrivacyLevel = settings.Obj("privacy")?.Str("level") ?? current.PrivacyLevel,
            SettingsPrivacyLevel = current.SettingsPrivacyLevel ?? current.PrivacyLevel,
            IdleReminderMinutes = current.IdleReminderMinutes, PrivacyReminderSeconds = current.PrivacyReminderSeconds,
            SelfHotkeys = new List<(uint, uint)>(current.SelfHotkeys),
            ProfileId = current.ProfileId, ProfileName = current.ProfileName, DetailLevel = current.DetailLevel,
            AfterStop = "viewer"
        };
        var priv = settings.Obj("privacy");
        o.ClipboardContent = priv?.Bool("clipboardContent") ?? false;
        o.EventLogText = priv?.Bool("eventLogText") ?? false;
        o.LogFiles = priv?.Long("logFiles") > 0 ? new List<string>(current.LogFiles) : new List<string>();
        // Ausschlussliste: vereinigt – nie kürzer als die aktuelle
        var excluded = new List<string>(current.ExcludedProcesses);
        foreach (var x in priv?.Arr("excluded")?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            if (x.Str("process") is { Length: > 0 } p && !excluded.Contains(p, StringComparer.OrdinalIgnoreCase)) excluded.Add(p);
        o.ExcludedProcesses = excluded;
        if (settings.Obj("capture") is { } cap)
        {
            o.ChangeFrames = cap.Str("changeFrames") ?? current.ChangeFrames;
            if (cap["appContext"] != null) o.AppContext = cap.Bool("appContext");
            if (cap["settleQuietMs"] != null) o.SettleQuietMs = cap.Int("settleQuietMs");
            if (cap["settleMaxMs"] != null) o.SettleMaxMs = cap.Int("settleMaxMs");
            if (cap["typingFrameMinGapMs"] != null) o.TypingFrameMinGapMs = cap.Int("typingFrameMinGapMs");
            if (cap["valueMaxChars"] != null) o.ValueMaxChars = cap.Int("valueMaxChars");
            if (cap["snapshotMaxNodes"] != null) o.SnapshotMaxNodes = cap.Int("snapshotMaxNodes");
        }
        else { o.ChangeFrames = current.ChangeFrames; o.AppContext = current.AppContext; }
        if (settings.Obj("input") is { } input)
            o.Off = new HashSet<string>(RecordingOptions.Channels.Where(c => input[c.key] is JsonValue v && v.TryGetValue<bool>(out var on) && !on).Select(c => c.key));
        else o.Off = new HashSet<string>(current.Off);

        // Aufnahmebereich beim ersten zu ersetzenden Schritt: letzter scope.change davor im selben Abschnitt
        var scope = s.Events.LastOrDefault(e => e.Str("type") == "scope.change" && e.Long("seq") <= atSeq && (part == null || part.Contains(e.Long("seq"))))
                    ?? settings.Obj("scope");
        ApplyScope(o, scope);
        return o;
    }

    static void ApplyScope(RecordingOptions o, JsonObject? scope)
    {
        switch (scope.Str("mode"))
        {
            case "app":
                o.Scope = ScopeMode.App;
                var app = scope!.Arr("apps")?.OfType<JsonObject>().FirstOrDefault();
                o.AppProcess = app?.Str("process");
                o.AppPid = null; o.AppWindow = 0;   // Prozess- und Fensterkennungen des alten Abschnitts gelten nicht mehr
                o.IncludeRelated = scope["includeRelated"] is JsonValue ir && ir.TryGetValue<bool>(out var b) ? b : true;
                break;
            case "region":
                o.Scope = ScopeMode.Region;
                o.Region = null; o.RegionNeedsConfirm = true;   // das Rechteck steht nicht im Kopf: Auswahl öffnen, nie stillschweigend anders
                break;
            default:
                o.Scope = ScopeMode.Screen;
                o.Monitors = scope?["monitors"] switch
                {
                    JsonArray a => string.Join(",", a.Select(x => x?.GetValue<string>()).Where(x => !string.IsNullOrEmpty(x))),
                    JsonValue v when v.TryGetValue<string>(out var m) => m,
                    _ => "all"
                };
                if (string.IsNullOrEmpty(o.Monitors)) o.Monitors = "all";
                break;
        }
    }

    /// <summary>Beschreibung des Aufnahmebereichs für das Startfenster („Eine Anwendung – notepad.exe“).</summary>
    public static string ScopeLabel(RecordingOptions o) => o.Scope switch
    {
        ScopeMode.App => Loc.F("Eine Anwendung – {0}", "One application – {0}", o.AppProcess ?? "?"),
        ScopeMode.Region => Loc.L("Ein Bereich – wird vor dem Start neu gewählt", "A region – chosen again before starting"),
        _ => o.Monitors switch
        {
            "all" => Loc.L("Ganzer Bildschirm – alle Monitore", "Whole screen – all monitors"),
            "active" => Loc.L("Ganzer Bildschirm – Monitor mit Aktivität", "Whole screen – monitor with activity"),
            var m => Loc.F("Ganzer Bildschirm – {0}", "Whole screen – {0}", m)
        }
    };
}
