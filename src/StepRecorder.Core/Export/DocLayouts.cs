using StepRecorder.Settings;

namespace StepRecorder.Export;

/// <summary>
/// Layoutvorlagen (13, Paket 27): fertige Zusammenstellungen von Seiten- und Aufbau-Einstellungen. Eine Vorlage setzt nur Layout,
/// nie Inhalte (Titel, Version, Texte). „Standard“ stellt die bisherigen Werte wieder her.
/// </summary>
public static class DocLayouts
{
    public static (string id, string name, string tip)[] All => new[]
    {
        ("standard", Loc.L("Standard", "Standard"), Loc.L("Wie bisher: Bilder mittel, Rand 2 cm, Fußzeile mit Seitenzahl", "As before: medium images, 2 cm margin, footer with page number")),
        ("compact", Loc.L("Kompakt", "Compact"), Loc.L("Kleine Bilder neben dem Text, schmaler Rand – wenig Seiten", "Small images beside the text, narrow margin – few pages")),
        ("training", Loc.L("Schulung", "Training"), Loc.L("Ein Schritt je Seite, große Bilder mit Unterschrift – zum Ausdrucken und Mitschreiben", "One step per page, large images with captions – for printing and taking notes")),
        ("manual", Loc.L("Handbuch", "Manual"), Loc.L("Titelseite, Verzeichnis, Kapitel auf neuer Seite, Bildunterschriften, Kopf- und Fußzeile mit „Seite n von N“", "Title page, contents, chapters on a new page, captions, header and footer with “Page n of N”")),
    };

    /// <summary>Vorlage anwenden; <paramref name="lang"/> bestimmt die Wörter in Kopf- und Fußzeile.</summary>
    public static void Apply(DocSettings d, string id, string lang)
    {
        // gemeinsame Grundlage: alles auf den bisherigen Stand
        d.MarginMm = 0; d.Layout = "below"; d.ImageSize = "m"; d.StepPerPage = false; d.ChapterNewPage = false; d.Captions = "none"; d.Figures = false;
        d.HeaderLeft = d.HeaderCenter = d.HeaderRight = d.FooterLeft = d.FooterCenter = d.FooterRight = "";
        switch (id)
        {
            case "compact":
                d.MarginMm = 12; d.Layout = "side"; d.ImageSize = "s"; d.Cover = false;
                break;
            case "training":
                d.StepPerPage = true; d.ImageSize = "l"; d.Captions = "auto";
                d.FooterLeft = "{{" + OutText.T(lang, "Titel", "Title") + "}}"; d.FooterRight = OutText.T(lang, "Seite {{Seite}}", "Page {{Page}}");
                break;
            case "manual":
                d.Cover = true; d.Toc = true; d.ChapterNewPage = true; d.Captions = "auto"; d.Figures = true; d.WarnFirst = true; d.PdfTagged = true;
                d.HeaderLeft = "{{" + OutText.T(lang, "Titel", "Title") + "}}"; d.HeaderRight = "{{Kapitel}}";
                d.FooterLeft = OutText.T(lang, "Version {{Version}}", "Version {{Version}}"); d.FooterRight = OutText.T(lang, "Seite {{Seite}} von {{Seiten}}", "Page {{Page}} of {{Pages}}");
                break;
        }
    }
}
