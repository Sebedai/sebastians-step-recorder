using StepRecorder.Settings;

namespace StepRecorder.Capture;

/// <summary>Ein Aufnahmemodus: Kennung, Namen, Namen für <c>--profile</c>, Ziel-Felder, Ausgabe nach dem Stopp.</summary>
public sealed record RecordingMode(string Id, string De, string En, string[] Aliases, bool BuiltIn, bool ShowGoalFields, string AfterStop)
{
    public string Name => Loc.L(De, En);
}

/// <summary>
/// Die einzige Liste der Aufnahmemodi (12, Paket 14). Startfenster, Kommandozeile (<c>--profile</c>), Profilangaben im
/// Manifest, Bibliothek und Katalog lesen sie von hier; den Detailgrad legt <see cref="DetailPresets"/> fest.
/// </summary>
public static class RecordingModes
{
    public static readonly RecordingMode[] All =
    {
        new("guide", "Anleitung", "Guide", new[] { "guide", "anleitung" }, true, false, "viewer"),
        new("support", "Support-Fall", "Support case", new[] { "support", "support-fall" }, true, true, "supportPackage"),
        new("aiTrace", "KI-Spur", "AI trace", new[] { "aitrace", "ki-spur" }, true, true, "export"),
        new("bugRepro", "Fehler nachstellen", "Reproduce bug", new[] { "bugrepro", "bug", "fehler", "fehler-nachstellen" }, true, true, "viewerError"),
        new("custom", "Eigene", "Custom", new[] { "custom", "eigene" }, false, true, "viewer"),
    };

    /// <summary>Modus nach Kennung oder Namen (ohne Rücksicht auf Groß- und Kleinschreibung); null = unbekannt.</summary>
    public static RecordingMode? Find(string? nameOrId)
    {
        if (string.IsNullOrWhiteSpace(nameOrId)) return null;
        string k = nameOrId.Trim().ToLowerInvariant();
        return All.FirstOrDefault(m => m.Id.Equals(k, StringComparison.OrdinalIgnoreCase) || m.Aliases.Contains(k) || m.De.Equals(nameOrId.Trim(), StringComparison.OrdinalIgnoreCase) || m.En.Equals(nameOrId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Anzeigename in der gewünschten Sprache (null = unbekannte Kennung).</summary>
    public static string? Name(string? id, string? lang = null) => All.FirstOrDefault(m => m.Id == id) is { } m ? (lang ?? Loc.Lang) == "de" ? m.De : m.En : null;

    /// <summary>Namen für die Hilfe der Kommandozeile.</summary>
    public static string CliNames(string lang) => string.Join(", ", All.Select(m => lang == "de" ? m.Aliases.Last() : m.Aliases.First()));
}
