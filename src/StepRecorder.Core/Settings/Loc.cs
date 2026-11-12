using System.Globalization;

namespace StepRecorder.Settings;

/// <summary>
/// Sprache der Oberfläche (nicht der Anleitungstexte): <c>Loc.L("Deutsch", "English")</c> direkt am Steuerelement.
/// Die Sprache der erzeugten Inhalte bleibt <see cref="AppSettings.Language"/> bzw. die Sprache der Aufnahme.
/// </summary>
public static class Loc
{
    /// <summary>Windows-Anzeigesprache und Regionalformat beim Start – für Kopfdaten der Aufnahme, nie durch die App-Sprache verändert.</summary>
    public static CultureInfo SystemUiCulture { get; } = CultureInfo.CurrentUICulture;
    public static CultureInfo SystemCulture { get; } = CultureInfo.CurrentCulture;

    /// <summary>"de" | "en" | "qps" (Pseudo-Sprache zum Finden unübersetzter Texte).</summary>
    public static string Lang { get; private set; } = "de";
    public static bool IsGerman => Lang == "de";
    /// <summary>Kultur für Datum und Zahlen in der Oberfläche.</summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Einstellung → Sprache: "system" folgt der Windows-Anzeigesprache (Deutsch bei de-*, sonst Englisch).</summary>
    public static string Resolve(string? setting, CultureInfo systemUi)
    {
        var s = (setting ?? "system").Trim().ToLowerInvariant();
        if (s is "de" or "en" or "qps") return s;
        return systemUi.TwoLetterISOLanguageName == "de" ? "de" : "en";
    }

    /// <summary>Formatkultur: Stimmt die Sprache mit dem Windows-Regionalformat überein, gilt dieses; sonst feste Muster.</summary>
    public static CultureInfo CultureFor(string lang, CultureInfo regional)
    {
        string l = lang == "qps" ? "en" : lang;
        if (regional.TwoLetterISOLanguageName == l && !regional.IsNeutralCulture) return regional;
        return CultureInfo.GetCultureInfo(l == "de" ? "de-DE" : "en-GB");
    }

    /// <summary>Einmal beim Start aufrufen (Oberfläche, Kommandozeile, MCP). Umgebungsvariable STEPRECORDER_UILANG gewinnt.</summary>
    public static void Init(string? setting, bool applyToThreads = false)
    {
        var env = Environment.GetEnvironmentVariable("STEPRECORDER_UILANG");
        Lang = Resolve(string.IsNullOrWhiteSpace(env) ? setting : env, SystemUiCulture);
        Culture = CultureFor(Lang, SystemCulture);
        if (applyToThreads)
        {
            // Nur die Anzeigesprache (WPF-eigene Texte wie das Kontextmenü von Textfeldern); CurrentCulture bleibt unberührt.
            var ui = CultureInfo.GetCultureInfo(Lang == "de" ? "de-DE" : "en-US");
            CultureInfo.CurrentUICulture = ui;
            CultureInfo.DefaultThreadCurrentUICulture = ui;
        }
    }

    /// <summary>Nur für Tests.</summary>
    public static void Set(string lang) { Lang = lang; Culture = CultureFor(lang, SystemCulture); }

    public static string L(string de, string en) => Lang switch { "de" => de, "qps" => "⟦" + en + "⟧", _ => en };

    /// <summary>
    /// Inhalt statt Oberfläche: Text in der Sprache der Anleitungstexte (<paramref name="lang"/> = „de“/„en“),
    /// z. B. Vorgaben, die in die Aufnahme oder einen Export geschrieben werden – unabhängig von der Oberflächensprache.
    /// </summary>
    public static string For(string? lang, string de, string en) => lang == "de" ? de : en;

    /// <summary>Mit nummerierten Platzhaltern ({0}, {1} …) – die Wortstellung darf je Sprache abweichen.</summary>
    public static string F(string de, string en, params object?[] args) => string.Format(Culture, L(de, en), args);

    /// <summary>Einzahl/Mehrzahl: <c>Loc.N(n, "1 Schritt", "{0} Schritte", "1 step", "{0} steps")</c>.</summary>
    public static string N(long n, string deOne, string deMany, string enOne, string enMany)
        => string.Format(Culture, n == 1 ? L(deOne, enOne) : L(deMany, enMany), n);

    /// <summary>Tastenkombination zur Anzeige: „Strg+Umschalt+F10“ bzw. „Ctrl+Shift+F10“.</summary>
    public static string Keys(string combo)
    {
        if (string.IsNullOrEmpty(combo)) return combo;
        var parts = combo.Split('+', StringSplitOptions.TrimEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = parts[i].ToLowerInvariant() switch
            {
                "ctrl" or "strg" or "control" => L("Strg", "Ctrl"),
                "shift" or "umschalt" or "umsch" => L("Umschalt", "Shift"),
                "alt" => "Alt",
                "win" or "windows" => "Win",
                "del" or "entf" or "delete" => L("Entf", "Del"),
                "ins" or "einfg" or "insert" => L("Einfg", "Ins"),
                "esc" or "escape" => "Esc",
                "enter" or "eingabe" or "return" => L("Eingabe", "Enter"),
                "space" or "leertaste" => L("Leertaste", "Space"),
                "pause" => "Pause",
                _ => parts[i]
            };
        }
        return string.Join("+", parts);
    }

    public static string Date(DateTime local) => local.ToString(Lang == "de" ? "dd.MM.yyyy" : Culture.Name == "en-US" ? "MM/dd/yyyy" : Culture.DateTimeFormat.ShortDatePattern, Culture);
    public static string Time(DateTime local) => local.ToString("HH:mm", Culture);
    public static string DateAndTime(DateTime local) => Date(local) + " " + Time(local);

    /// <summary>„heute, 14:05“, „gestern, 09:12“, Wochentag oder Datum.</summary>
    public static string Relative(DateTime local)
    {
        var d = DateTime.Today - local.Date;
        return d.TotalDays switch
        {
            0 => F("heute, {0}", "today, {0}", Time(local)),
            1 => F("gestern, {0}", "yesterday, {0}", Time(local)),
            > 1 and < 7 => local.ToString("dddd", CultureInfo.GetCultureInfo(Lang == "de" ? "de-DE" : "en-GB")) + ", " + Time(local),
            _ => Date(local)
        };
    }

    public static string Number(double v, string format = "0.#") => v.ToString(format, Culture);

    /// <summary>Bytes lesbar: „812 KB“, „3,4 MB“, „1,2 GB“.</summary>
    public static string Bytes(long n) => n switch
    {
        < 1024 => n + " B",
        < 1024 * 1024 => Number(n / 1024.0, "0") + " KB",
        < 1024L * 1024 * 1024 => Number(n / 1024.0 / 1024.0, "0.0") + " MB",
        _ => Number(n / 1024.0 / 1024.0 / 1024.0, "0.0") + " GB"
    };
}
