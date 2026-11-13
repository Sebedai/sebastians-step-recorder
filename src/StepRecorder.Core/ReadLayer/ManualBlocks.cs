namespace StepRecorder.ReadLayer;

/// <summary>Vorbefüllte Textabschnitte eines Handbuchs (13, Paket 24): Überschrift und Text je Sprache, Text als Auszeichnung.</summary>
public static class ManualBlocks
{
    public static readonly (string id, string de, string en, string bodyDe, string bodyEn)[] All =
    [
        ("text", "Textabschnitt", "Text section", "", ""),
        ("safety", "Sicherheitshinweise", "Safety information", "- Lesen Sie diese Anleitung vollständig, bevor Sie beginnen.\n- Arbeiten Sie nur mit den Berechtigungen, die Sie für diese Aufgabe brauchen.\n- Geben Sie Kennwörter nie weiter.", "- Read this guide completely before you start.\n- Only work with the permissions you need for this task.\n- Never share passwords."),
        ("faq", "Häufige Fragen", "Frequently asked questions", "**Was tun, wenn …?**\nAntwort.\n\n**Wo finde ich …?**\nAntwort.", "**What if …?**\nAnswer.\n\n**Where do I find …?**\nAnswer."),
        ("contact", "Kontakt und Hilfe", "Contact and help", "Bei Fragen hilft Ihnen der Servicedesk weiter:\n- Telefon: \n- E-Mail: ", "If you have questions, the service desk will help:\n- Phone: \n- Email: "),
    ];
}
