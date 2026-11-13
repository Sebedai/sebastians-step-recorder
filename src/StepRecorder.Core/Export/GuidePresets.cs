namespace StepRecorder.Export;

/// <summary>
/// Art der Anleitung (09 §9.1): Standard, Kurzfassung (ohne Bilder, kompakt), Checkliste (Kästchen je Schritt).
/// Nur Optionssätze auf <see cref="GuideOptions"/> und <see cref="DocSettings"/> – keine eigenen Ausgabewege.
/// </summary>
public static class GuidePresets
{
    public static readonly (string id, string de, string en, string hintDe, string hintEn)[] All =
    {
        ("standard", "Standard", "Standard", "Wie im Editor unter „Titelseite und Einleitung“ festgelegt", "As set in the editor under “Title page and introduction”"),
        ("short", "Kurzfassung", "Short version", "Ohne Bilder und Titelseite, Schritte fortlaufend nummeriert", "No images or title page, steps numbered consecutively"),
        ("checklist", "Checkliste", "Checklist", "Kurzfassung mit einem Kästchen je Schritt – zum Ausdrucken und Abhaken", "Short version with a tick box per step – for printing"),
    };

    /// <summary>„kurz“, „kurzfassung“, „kompakt“ → short; „checkliste“ → checklist; sonst standard.</summary>
    public static string Normalize(string? id) => (id ?? "").Trim().ToLowerInvariant() switch
    {
        "short" or "kurz" or "kurzfassung" or "kompakt" or "compact" => "short",
        "checklist" or "checkliste" => "checklist",
        _ => "standard"
    };

    /// <summary>Wendet die Art auf die Exportoptionen an.</summary>
    public static void Apply(string? id, GuideOptions o, DocSettings? s)
    {
        string p = Normalize(id);
        o.Preset = p;
        if (p is "short" or "checklist") { o.Images = false; o.Compact = true; }
        if (p == "checklist") o.Checkboxes = true;
        if (s != null) ApplyDoc(p, s);
    }

    /// <summary>Wendet die Art auf die Dokument-Einstellungen an (nach dem Laden aus der Aufnahme).</summary>
    public static void ApplyDoc(string? id, DocSettings s)
    {
        string p = Normalize(id);
        if (p is "short" or "checklist") { s.Cover = false; s.Toc = false; s.Numbering = "flat"; s.Layout = "below"; }
    }

    /// <summary>Zusatz im Dateinamen, damit die Standardanleitung nicht überschrieben wird.</summary>
    public static string FileSuffix(string? id, string lang) => Normalize(id) switch
    {
        "short" => Settings.OutText.For(lang, " – Kurzfassung", " – short", " – version courte", " – versión corta", " – versione breve", " – korte versie"),
        "checklist" => Settings.OutText.For(lang, " – Checkliste", " – checklist", " – liste de contrôle", " – lista de comprobación", " – lista di controllo", " – checklist"),
        _ => ""
    };
}
