using System.Globalization;

namespace StepRecorder.Settings;

/// <summary>
/// Sprache der <b>Ausgabe</b> (automatische Schritttexte und Rahmentexte der Anleitungen, 09 §10): Deutsch, Englisch,
/// Französisch, Spanisch, Italienisch, Niederländisch. Die Oberfläche bleibt Deutsch/Englisch (<see cref="Loc"/>).
/// Unbekannte Sprache → Englisch. Deutsch und Englisch liefern genau die bisherigen Texte.
/// </summary>
public static class OutText
{
    /// <summary>Unterstützte Sprachen der Ausgabe, in der Reihenfolge der Auswahllisten.</summary>
    public static readonly string[] Languages = { "de", "en", "fr", "es", "it", "nl" };

    /// <summary>Namen der Sprachen in der jeweiligen Sprache (für Auswahllisten).</summary>
    public static readonly string[] NativeNames = { "Deutsch", "English", "Français", "Español", "Italiano", "Nederlands" };

    public static bool IsSupported(string? lang) => lang != null && Array.IndexOf(Languages, lang) >= 0;

    /// <summary>„FR“ → „fr“; Unbekanntes → „en“.</summary>
    public static string Normalize(string? lang)
    {
        var l = (lang ?? "").Trim().ToLowerInvariant();
        return IsSupported(l) ? l : "en";
    }

    /// <summary>Text je Ausgabesprache; unbekannte Sprache → Englisch.</summary>
    public static string For(string? lang, string de, string en, string fr, string es, string it, string nl) => lang switch
    {
        "de" => de, "fr" => fr, "es" => es, "it" => it, "nl" => nl, _ => en
    };

    /// <summary>Sprachkennung für Dokumente (DOCX, PDF, PPTX).</summary>
    public static string Tag(string? lang) => For(lang, "de-DE", "en-US", "fr-FR", "es-ES", "it-IT", "nl-NL");

    /// <summary>Kultur für Zahlen in Texten (Englisch: invariant wie bisher).</summary>
    public static CultureInfo Culture(string? lang) => lang is "de" or "fr" or "es" or "it" or "nl" ? CultureInfo.GetCultureInfo(Tag(lang)) : CultureInfo.InvariantCulture;

    /// <summary>Datumsformat der Kopfdaten (Stand).</summary>
    public static string DateFormat(string? lang) => For(lang, "dd.MM.yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "dd/MM/yyyy", "dd/MM/yyyy", "dd-MM-yyyy");

    /// <summary>Übliche Anführungszeichen: „…“, “…”, « … », «…», «…», ‘…’.</summary>
    public static string Quote(string s, string? lang) => lang switch
    {
        "de" => $"„{s}“",
        "fr" => $"« {s} »",
        "es" or "it" => $"«{s}»",
        "nl" => $"‘{s}’",
        _ => $"“{s}”"
    };

    /// <summary>
    /// Rahmentexte der Anleitungen (Überschriften, Kopfdaten, Navigation), Schlüssel ist der englische Text, wie er in
    /// <c>Doc.L("deutsch", "english")</c> steht. Fehlt ein Eintrag, gilt für weitere Sprachen der englische Text.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string de, string fr, string es, string it, string nl)> Frame =
        new Dictionary<string, (string, string, string, string, string)>(StringComparer.Ordinal)
        {
            ["Result"] = ("Ergebnis", "Résultat", "Resultado", "Risultato", "Resultaat"),
            ["Prerequisites"] = ("Voraussetzungen", "Prérequis", "Requisitos previos", "Prerequisiti", "Vereisten"),
            ["About this guide"] = ("Über diese Anleitung", "À propos de ce guide", "Acerca de esta guía", "Informazioni su questa guida", "Over deze handleiding"),
            ["Finally"] = ("Abschluss", "Pour terminer", "Para terminar", "Per concludere", "Tot slot"),
            ["Contents"] = ("Inhalt", "Sommaire", "Contenido", "Sommario", "Inhoud"),
            ["steps"] = ("Schritte", "étapes", "pasos", "passaggi", "stappen"),
            ["1 step"] = ("1 Schritt", "1 étape", "1 paso", "1 passaggio", "1 stap"),
            ["step"] = ("Schritt", "étape", "paso", "passaggio", "stap"),
            ["Step "] = ("Schritt ", "Étape ", "Paso ", "Passaggio ", "Stap "),
            ["chapters"] = ("Kapitel", "chapitres", "capítulos", "capitoli", "hoofdstukken"),
            ["Created with "] = ("Erstellt mit ", "Créé avec ", "Creado con ", "Creato con ", "Gemaakt met "),
            ["Created with"] = ("Erstellt mit", "Créé avec", "Creado con", "Creato con", "Gemaakt met"),
            ["Version"] = ("Version", "Version", "Versión", "Versione", "Versie"),
            ["Date"] = ("Stand", "Date", "Fecha", "Data", "Datum"),
            ["Author"] = ("Autor", "Auteur", "Autor", "Autore", "Auteur"),
            ["Audience"] = ("Zielgruppe", "Public cible", "Destinatarios", "Destinatari", "Doelgroep"),
            ["Length"] = ("Umfang", "Étendue", "Extensión", "Lunghezza", "Omvang"),
            ["done"] = ("erledigt", "terminé", "hecho", "fatto", "klaar"),
            ["Evidence"] = ("Beleg", "Preuve", "Evidencia", "Evidenza", "Bewijs"),
            ["Click to enlarge"] = ("Klicken zum Vergrößern", "Cliquer pour agrandir", "Haga clic para ampliar", "Fare clic per ingrandire", "Klik om te vergroten"),
            ["Click to close"] = ("Klicken zum Schließen", "Cliquer pour fermer", "Haga clic para cerrar", "Fare clic per chiudere", "Klik om te sluiten"),
            ["The GIF stays above the size target."] = ("Das GIF bleibt über dem Größenziel.", "Le GIF reste au-dessus de la taille cible.", "El GIF sigue superando el tamaño objetivo.", "La GIF resta sopra la dimensione obiettivo.", "De GIF blijft boven de doelgrootte."),
            // Klick-Tour
            ["Click the marked spot in the image to continue."] = ("Klicken Sie im Bild auf die markierte Stelle, um weiterzugehen.", "Cliquez sur l'emplacement marqué dans l'image pour continuer.", "Haga clic en el punto marcado de la imagen para continuar.", "Fare clic sul punto evidenziato nell'immagine per continuare.", "Klik op de gemarkeerde plek in de afbeelding om verder te gaan."),
            ["Start tour →"] = ("Tour starten →", "Démarrer la visite →", "Iniciar el recorrido →", "Avvia il tour →", "Rondleiding starten →"),
            ["Click here – next"] = ("Hier klicken – weiter", "Cliquez ici – suivant", "Haga clic aquí – siguiente", "Fare clic qui – avanti", "Klik hier – volgende"),
            ["Click here"] = ("Hier klicken", "Cliquez ici", "Haga clic aquí", "Fare clic qui", "Klik hier"),
            ["← Back"] = ("← Zurück", "← Précédent", "← Atrás", "← Indietro", "← Terug"),
            ["Next →"] = ("Weiter →", "Suivant →", "Siguiente →", "Avanti →", "Volgende →"),
            ["or click the spot in the image"] = ("oder im Bild auf die Stelle klicken", "ou cliquez sur l'emplacement dans l'image", "o haga clic en el punto de la imagen", "oppure fare clic sul punto nell'immagine", "of klik op de plek in de afbeelding"),
            ["Done"] = ("Geschafft", "Terminé", "Listo", "Fatto", "Klaar"),
            ["Start again"] = ("Von vorn", "Recommencer", "Empezar de nuevo", "Ricomincia", "Opnieuw beginnen"),
            ["Overview"] = ("Übersicht", "Vue d'ensemble", "Resumen", "Panoramica", "Overzicht"),
            // PowerPoint
            ["Done – all steps are described."] = ("Geschafft – alle Schritte sind beschrieben.", "Terminé – toutes les étapes sont décrites.", "Listo: todos los pasos están descritos.", "Fatto – tutti i passaggi sono descritti.", "Klaar – alle stappen zijn beschreven."),
            // Fehlerbericht
            ["(please add)"] = ("(bitte ergänzen)", "(à compléter)", "(por completar)", "(da completare)", "(aanvullen)"),
            ["Summary"] = ("Zusammenfassung", "Résumé", "Resumen", "Riepilogo", "Samenvatting"),
            ["Environment"] = ("Umgebung", "Environnement", "Entorno", "Ambiente", "Omgeving"),
            ["Language"] = ("Sprache", "Langue", "Idioma", "Lingua", "Taal"),
            ["Monitor"] = ("Monitor", "Écran", "Monitor", "Monitor", "Beeldscherm"),
            [" (primary)"] = (" (Hauptmonitor)", " (principal)", " (principal)", " (principale)", " (primair)"),
            ["Applications"] = ("Programme", "Applications", "Aplicaciones", "Applicazioni", "Toepassingen"),
            ["linked "] = ("erstellt ", "créé ", "creado ", "creato ", "gemaakt "),
            ["Git working tree (unchecked): "] = ("Git-Arbeitsstand (ungeprüft): ", "Arbre de travail Git (non vérifié) : ", "Árbol de trabajo de Git (sin comprobar): ", "Albero di lavoro Git (non verificato): ", "Git-werkmap (niet gecontroleerd): "),
            ["detached"] = ("losgelöst", "détaché", "desasociado", "scollegato", "losgekoppeld"),
            ["Recorded"] = ("Aufgenommen", "Enregistré", "Grabado", "Registrato", "Opgenomen"),
            [" with "] = (" mit ", " avec ", " con ", " con ", " met "),
            ["Steps to reproduce"] = ("Schritte zum Nachstellen", "Étapes pour reproduire", "Pasos para reproducir", "Passaggi per riprodurre", "Stappen om te reproduceren"),
            ["at"] = ("bei", "à", "en", "a", "op"),
            ["Expected result"] = ("Erwartetes Ergebnis", "Résultat attendu", "Resultado esperado", "Risultato previsto", "Verwacht resultaat"),
            ["Actual result"] = ("Tatsächliches Ergebnis", "Résultat obtenu", "Resultado real", "Risultato effettivo", "Werkelijk resultaat"),
            ["observed"] = ("beobachtet", "observé", "observado", "osservato", "waargenomen"),
            ["Attachments"] = ("Anhänge", "Pièces jointes", "Adjuntos", "Allegati", "Bijlagen"),
            // Texte außerhalb von Doc.L (Leseschicht, Einleitung, Dateinamen)
            ["Guide"] = ("Anleitung", "Guide", "Guía", "Guida", "Handleiding"),
            ["Goal: "] = ("Ziel: ", "Objectif : ", "Objetivo: ", "Obiettivo: ", "Doel: "),
            ["Expected result: "] = ("Erwartetes Ergebnis: ", "Résultat attendu : ", "Resultado esperado: ", "Risultato previsto: ", "Verwacht resultaat: "),
            ["Note"] = ("Hinweis", "Remarque", "Nota", "Nota", "Opmerking"),
            ["Step redacted"] = ("Schritt geschwärzt", "Étape masquée", "Paso oculto", "Passaggio oscurato", "Stap afgeschermd"),
            ["[redacted]"] = ("[geschwärzt]", "[masqué]", "[oculto]", "[oscurato]", "[afgeschermd]"),
        };

    /// <summary>
    /// Rahmentext in der Ausgabesprache: Deutsch und Englisch wie übergeben, weitere Sprachen aus <see cref="Frame"/>
    /// (Schlüssel = englischer Text), sonst Englisch.
    /// </summary>
    public static string T(string? lang, string de, string en)
    {
        if (lang == "de") return de;
        if (lang is "fr" or "es" or "it" or "nl" && Frame.TryGetValue(en, out var t)) return For(lang, t.de, en, t.fr, t.es, t.it, t.nl);
        return en;
    }

    /// <summary>Rahmentext nur über den englischen Schlüssel.</summary>
    public static string T(string? lang, string en) => Frame.TryGetValue(en, out var t) ? T(lang, t.de, en) : en;
}
