using System.Text;
using System.Text.Json.Nodes;

namespace StepRecorder.Export;

/// <summary>
/// „Für Menschen lesbar“: erklärt Exporte, die sonst knapp für KI-Assistenten geschrieben sind.
/// Jede Angabe (auch Systemangaben, Belegqualität, Koordinaten, Lücken) wird beim ersten Auftreten in Klartext erläutert,
/// jeder Schritt bekommt einen Satz „In Worten“, dazu eine LIESMICH-Datei und Kommentare in den JSON-Dateien.
/// Optional, weil es den Umfang (und damit die Tokens bei einer KI) deutlich erhöht.
/// </summary>
public static class KiExplain
{
    // Abschnittsüberschriften → Erklärung (de, en)
    static readonly Dictionary<string, (string de, string en)> Sections = new()
    {
        ["Kopfdaten"] = ("Allgemeine Angaben zur Aufnahme: wann, auf welchem System, mit welchen Bildschirmen und Einstellungen sie entstand. Sie helfen einzuschätzen, ob ein Problem vom Rechner abhängt (Windows-Version, Sprache, Skalierung).",
                         "General information about the recording: when, on which system, with which monitors and settings it was made. It helps to judge whether a problem depends on the computer (Windows version, language, scaling)."),
        ["Angaben des Autors zur Aufnahme"] = ("Was die aufnehmende Person selbst angegeben hat – Ziel, erwartetes Ergebnis, Marker. Das ist ihre Sicht, keine Messung.",
                                               "What the person who recorded stated themselves – purpose, expected result, markers. This is their view, not a measurement."),
        ["Vollständigkeitsbericht"] = ("Prüfung, ob die Aufnahme lückenlos ist: Jede Tastatur- und Mauseingabe muss genau einem Schritt zugeordnet sein, und Schritte, Leerlauf und Lücken müssen zusammen die Gesamtdauer ergeben.",
                                       "Check whether the recording is complete: every keyboard and mouse input must belong to exactly one step, and steps, idle time and gaps must add up to the total duration."),
        ["Lücken und Einschränkungen"] = ("Zeiträume oder Inhalte, die bewusst oder technisch nicht aufgezeichnet wurden (Pause, Privattaste, gesperrter Bildschirm, Administratorfenster …). Was dort geschah, ist unbekannt – nichts davon darf ergänzt oder vermutet werden.",
                                          "Periods or content that were not recorded, deliberately or for technical reasons (pause, privacy key, locked screen, administrator window …). What happened there is unknown – nothing may be filled in or guessed."),
        ["Gliederung"] = ("Kurzüberblick: eine Zeile je Schritt mit Zeit seit Aufnahmebeginn, Art der Aktion, Beschreibung und Belegqualität in eckigen Klammern.",
                          "Short overview: one line per step with the time since the recording started, the kind of action, a description and the evidence quality in square brackets."),
        ["Ablauf"] = ("Alle Schritte im Detail, in der tatsächlichen zeitlichen Reihenfolge. Jede Angabe wird beim ersten Auftreten kurz erklärt.",
                      "All steps in detail, in the actual order in time. Each entry is briefly explained the first time it appears."),
    };

    // Feldbezeichnungen („- Bezeichnung: …“) → Erklärung (de, en); deutsche und englische Bezeichnungen zeigen auf dieselbe Erklärung
    static readonly (string[] labels, string de, string en)[] Fields =
    {
        (new[] { "Zeitraum", "Period" }, "Beginn und Ende der Aufnahme in Ortszeit, die Zeitzone und die Dauer.", "Start and end of the recording in local time, the time zone and the duration."),
        (new[] { "System", "System" }, "Windows-Ausgabe und -Version, Sprache der Windows-Oberfläche und das Tastaturlayout. Das Tastaturlayout bestimmt, welche Zeichen eine Taste erzeugt (z. B. Z und Y sind bei Deutsch und Englisch vertauscht).", "Windows edition and version, the Windows display language and the keyboard layout. The keyboard layout decides which character a key produces (e.g. Z and Y are swapped between German and English)."),
        (new[] { "Bildschirme", "Monitors" }, "Jeder Monitor mit Kennung (m1, m2 …), Auflösung in Pixeln, Windows-Skalierung (z. B. 150 % = alles ist 1,5-mal so groß dargestellt) und Lage im Gesamtbild aller Monitore. Die Kennungen tauchen bei Bildern und Fenstern wieder auf.", "Each monitor with its id (m1, m2 …), resolution in pixels, Windows scaling (e.g. 150 % = everything is drawn 1.5 times larger) and position within the combined desktop. The ids appear again for images and windows."),
        (new[] { "Aufnahmebereich", "Capture area" }, "Was aufgezeichnet wurde: der ganze Bildschirm, nur bestimmte Anwendungen oder ein Ausschnitt. Außerhalb davon fehlt alles absichtlich.", "What was recorded: the whole screen, only certain applications or a region. Anything outside it is deliberately missing."),
        (new[] { "Nicht aufgezeichnet (Einstellung des Autors)", "Not recorded (author's setting)" }, "Eingabearten, die die aufnehmende Person abgeschaltet hatte.", "Kinds of input the person recording had switched off."),
        (new[] { "Recorder", "Recorder" }, "Version des Aufnahmeprogramms, ob es mit Administratorrechten lief (ohne sie sind Eingaben in Administratorfenstern unsichtbar), die Schutzstufe für Passwörter und die Version der Regeln, nach denen Eingaben zu Schritten zusammengefasst wurden.", "Version of the recording program, whether it ran with administrator rights (without them, input in administrator windows is invisible), the protection level for passwords and the version of the rules that grouped input into steps."),
        (new[] { "Anwendungen", "Applications" }, "Alle Programme, die bedient wurden: Prozessname (z. B. notepad.exe) und bis zu drei Fenstertitel.", "All programs that were used: process name (e.g. notepad.exe) and up to three window titles."),
        (new[] { "Bearbeitung durch den Autor", "Edited by the author" }, "Ob Texte, Bilder oder Schritte nachträglich geändert wurden, und ob alle auffälligen Eingaben (mögliche Kennwörter, Schlüssel, IBANs) vor dem Export geprüft wurden.", "Whether texts, images or steps were changed afterwards, and whether all flagged inputs (possible passwords, keys, IBANs) were reviewed before export."),
        (new[] { "Verallgemeinerte Werte", "Generalized values" }, "Persönliche Werte, die durch Platzhalter in geschweiften Klammern ersetzt wurden, z. B. {Benutzername}.", "Personal values replaced by placeholders in curly braces, e.g. {UserName}."),
        (new[] { "Bilder im Paket", "Images in package" }, "Verkleinerungsfaktor der Bilder (1 = Originalgröße) und Anzahl der Bilddateien im Ordner images.", "Scaling factor of the images (1 = original size) and number of image files in the images folder."),
        (new[] { "Buchführung", "Bookkeeping" }, "Zählung aller Eingabeereignisse (jede gedrückte Taste, jeder Mausklick). „ohne Schritt“ und „doppelt“ sollten 0 sein – dann ist keine Eingabe verloren gegangen oder doppelt gezählt.", "Count of all input events (every key press, every mouse click). \"without step\" and \"duplicate\" should be 0 – then no input was lost or counted twice."),
        (new[] { "Zeit", "Time" }, "Die Gesamtdauer aufgeteilt in Zeit mit Aktionen, Leerlauf (nichts passiert) und Lücken (nicht aufgezeichnet).", "The total duration split into time with actions, idle time (nothing happened) and gaps (not recorded)."),
        (new[] { "Belegqualität", "Evidence quality" }, "Wie gut die Schritte belegt sind: „belegt“ = Aktion und ihre Wirkung wurden beobachtet; „nur Eingabe“ = die Eingabe ist sicher, eine Wirkung war nicht sichtbar; „unbekannt“ = Ziel oder Wirkung ließen sich nicht feststellen.", "How well the steps are supported: \"confirmed\" = the action and its effect were observed; \"input only\" = the input is certain, no effect was visible; \"unknown\" = target or effect could not be determined."),
        (new[] { "Lücken", "Gaps" }, "Anzahl der Zeiträume ohne Aufzeichnung und der Stellen, an denen Inhalte fehlen (z. B. maskierte Passwörter).", "Number of periods without recording and of places where content is missing (e.g. masked passwords)."),
        (new[] { "Bilder der Sitzung", "Session images" }, "Bildschirmfotos während der Aufnahme: angefordert, gespeichert, unverändert (gleiches Bild wie vorher) und ausgelassen (bei Überlast). „Vorher-Bild garantiert“ heißt: Das Bild zeigt sicher den Zustand unmittelbar vor der Eingabe.", "Screenshots taken during recording: requested, stored, unchanged (same as before) and dropped (under load). \"Before image guaranteed\" means: the image certainly shows the state right before the input."),
        (new[] { "In Worten", "In plain words" }, "Der Schritt als einfacher Anleitungssatz.", "The step as a simple instruction."),
        (new[] { "Aktion", "Action" }, "Was getan wurde, knapp im Protokollstil.", "What was done, briefly, in log style."),
        (new[] { "Kanonisch", "Canonical" }, "Dieselbe Aktion in fester Kurzschreibweise (Verb und Werte), damit Programme und KI-Assistenten sie eindeutig lesen können.", "The same action in a fixed short notation (verb and values), so programs and AI assistants can read it unambiguously."),
        (new[] { "Kennung", "Id" }, "Eindeutige Kennung des Schritts; damit verweisen steps.json und Bilder auf ihn.", "Unique id of the step; steps.json and images refer to it with this id."),
        (new[] { "Fenster", "Window" }, "Titel des Fensters, in dem gearbeitet wurde, das Programm (Prozessname) und der Monitor.", "Title of the window in which the work happened, the program (process name) and the monitor."),
        (new[] { "Ziel", "Target" }, "Das bediente Element (Schaltfläche, Feld, Menüpunkt …) laut Windows-Bedienungshilfen: Typ, Name, ggf. AutomationId (feste interne Kennung), der Pfad von außen nach innen und das Rechteck (x, y, Breite × Höhe in Pixeln) im genannten Bild.", "The element that was used (button, field, menu item …) according to Windows accessibility: type, name, possibly AutomationId (fixed internal id), the path from outside to inside and the rectangle (x, y, width × height in pixels) in the named image."),
        (new[] { "Position", "Position" }, "Wo genau geklickt wurde, in Pixeln im genannten Bild (0, 0 = links oben); „im Element“ gibt die Stelle relativ zum Element an (0,50 / 0,50 = genau in der Mitte).", "Exactly where the click happened, in pixels in the named image (0, 0 = top left); \"in element\" gives the spot relative to the element (0.50 / 0.50 = exactly in the middle)."),
        (new[] { "AutoHotkey", "AutoHotkey" }, "Technische Angaben, mit denen sich der Schritt automatisieren lässt: WinTitle (wie AutoHotkey das Fenster findet), Steuerelement (ClassNN, interne Bezeichnung), Klickpunkt im Fenster, im Innenbereich (Client) und auf dem Bildschirm, Farbe des Pixels, Fokus und Schreibmarke.", "Technical details for automating the step: WinTitle (how AutoHotkey finds the window), control (ClassNN, internal name), click point in the window, in the inner area (client) and on the screen, pixel colour, focus and caret."),
        (new[] { "Fenstertexte", "Window texts" }, "Sichtbare Texte der Steuerelemente im Fenster (Beschriftungen, Schaltflächen).", "Visible texts of the controls in the window (labels, buttons)."),
        (new[] { "Mausweg davor", "Mouse path before" }, "Wie die Maus zum Ziel kam: Strecke, Dauer, direkt oder auf Umwegen, und ob sie unterwegs verweilte (Hinweis auf Suchen oder Zögern).", "How the mouse got to the target: distance, duration, direct or with detours, and whether it paused on the way (a sign of searching or hesitation)."),
        (new[] { "Start/Ende/Weg", "Start/End/Path" }, "Start- und Endpunkt beim Ziehen, die Strecke und was gezogen wurde (Text markieren, Fenster verschieben …).", "Start and end point of a drag, the distance and what was dragged (selecting text, moving a window …)."),
        (new[] { "Getippt", "Typed" }, "Die getippten Zeichen. Sondertasten stehen in geschweiften Klammern, z. B. {Backspace} (Rücktaste) oder {Enter}; {masked:12} heißt: 12 Zeichen wurden zum Schutz nicht gespeichert; {unreviewed:N} heißt: eine auffällige Eingabe wurde vor dem Export nicht geprüft und deshalb weggelassen.", "The typed characters. Special keys are shown in curly braces, e.g. {Backspace} or {Enter}; {masked:12} means: 12 characters were not stored for protection; {unreviewed:N} means: a flagged input was not reviewed before export and was therefore left out."),
        (new[] { "Abfrage", "Prompt" }, "Eine erkannte Passwortabfrage (z. B. in einem Terminal); die Eingabe danach ist deshalb maskiert.", "A detected password prompt (e.g. in a terminal); the following input is therefore masked."),
        (new[] { "Eingabeweg", "Input method" }, "Wie der Text entstand: keyboard = Tastatur, ime = Eingabemethode für z. B. Japanisch/Chinesisch (das Ergebnis steht dann im Feldinhalt).", "How the text was produced: keyboard, or ime = input method e.g. for Japanese/Chinese (the result is then in the field value)."),
        (new[] { "Feldinhalt", "Field value" }, "Inhalt des Eingabefelds vor → nach der Eingabe, direkt aus dem Feld gelesen.", "Content of the input field before → after typing, read directly from the field."),
        (new[] { "Tasten", "Keys" }, "Gedrückte Taste oder Tastenkombination; ×3 heißt dreimal hintereinander.", "Key or key combination pressed; ×3 means three times in a row."),
        (new[] { "Zwischenablage", "Clipboard" }, "Kopieren, Ausschneiden oder Einfügen. Der Inhalt der Zwischenablage wird aus Datenschutzgründen nie gespeichert.", "Copy, cut or paste. The clipboard content is never stored, for privacy reasons."),
        (new[] { "Wirkung", "Effect" }, "Was nach der Aktion sichtbar passierte: neue oder geschlossene Fenster, geänderte Titel, wie viel Prozent des Bildschirms sich änderten – jeweils mit der Zeit danach.", "What visibly happened after the action: new or closed windows, changed titles, how much of the screen changed – each with the time afterwards."),
        (new[] { "Fokus danach", "Focus after" }, "Welches Element nach der Aktion die Tastatureingaben empfängt.", "Which element receives keyboard input after the action."),
        (new[] { "Menübefehl", "Menu command" }, "Der gewählte Menüpfad und die interne Befehlsnummer des Programms.", "The chosen menu path and the program's internal command number."),
        (new[] { "Liste", "List" }, "Angaben zu einer Liste oder Auswahlliste: Art, Anzahl der Einträge, gewählter Eintrag.", "Details of a list or drop-down: kind, number of items, selected item."),
        (new[] { "MSAA", "MSAA" }, "Ältere Windows-Bedienungshilfe: Rolle des Elements, Standardaktion und Zustände.", "Older Windows accessibility interface: the element's role, default action and states."),
        (new[] { "Dateidialog", "File dialog" }, "Ordner und Dateinamenfeld eines Öffnen- oder Speichern-Dialogs.", "Folder and file name field of an open or save dialog."),
        (new[] { "Explorer-Ordner", "Explorer folder" }, "Geöffneter Ordner im Datei-Explorer, ausgewählte Dateien und der Ordner danach.", "Folder open in File Explorer, selected files and the folder afterwards."),
        (new[] { "Browser-Seite", "Browser page" }, "Adresse der Webseite vor und nach der Aktion – ohne Abfrageteil (?…) und Sprungmarke (#…), weil dort oft persönliche Daten stehen.", "Address of the web page before and after the action – without the query (?…) and fragment (#…), because those often contain personal data."),
        (new[] { "Office", "Office" }, "Angaben aus Excel oder Word: Mappe, Blatt, Zelle, Auswahl bzw. Dokument.", "Details from Excel or Word: workbook, sheet, cell, selection or document."),
        (new[] { "Statusleiste danach", "Status bar after" }, "Text der Statusleiste des Programms nach der Aktion – oft eine Bestätigung wie „Gespeichert“.", "Text in the program's status bar after the action – often a confirmation such as \"Saved\"."),
        (new[] { "Beleg", "Evidence" }, "Wie sicher der Schritt belegt ist (belegt / nur Eingabe / unbekannt) und die Gründe dafür.", "How well the step is supported (confirmed / input only / unknown) and why."),
        (new[] { "Hinweis", "Note" }, "Besonderheit des Schritts, z. B. eine automatisch erzeugte Eingabe oder eine Lücke.", "Special feature of the step, e.g. an automatically generated input or a gap."),
        (new[] { "Kommentar des Autors", "Author comment" }, "Eine Bemerkung, die die aufnehmende Person während der Aufnahme eingegeben oder diktiert hat.", "A remark the person recording typed or dictated during the recording."),
        (new[] { "Markierung des Autors", "Author annotation" }, "Pfeile, Rahmen oder Texte, die nachträglich ins Bild gezeichnet wurden.", "Arrows, frames or texts drawn into the image afterwards."),
        (new[] { "Text des Autors", "Author text" }, "Überschrift und Text, die die aufnehmende Person selbst geschrieben hat.", "Heading and text written by the person recording."),
        (new[] { "Bilder", "Images" }, "Bilddateien zum Schritt im Ordner images, mit Größe, Maßstab (1 = Originalpixel) und Monitor. Bei Ausschnitten gibt „Ursprung“ an, wo der Ausschnitt im Gesamtbild liegt.", "Image files for the step in the images folder, with size, scale (1 = original pixels) and monitor. For crops, \"origin\" says where the crop lies in the full image."),
        (new[] { "Betroffen", "Affected" }, "Welche Kanäle (Tastatur, Maus, Bilder) in dieser Lücke fehlen.", "Which channels (keyboard, mouse, images) are missing in this gap."),
        (new[] { "Eingabeverlust", "Input loss" }, "Ob in der Lücke Eingaben verloren gegangen sein können: keiner, möglich, sicher oder unbekannt.", "Whether input may have been lost in the gap: none, possible, certain or unknown."),
        (new[] { "Marker-Typ", "Marker type" }, "Art des gesetzten Markers: note = Notiz, error = Fehler tritt auf, expected = so soll es aussehen, important = wichtige Stelle.", "Kind of marker: note, error = the error occurs here, expected = this is how it should look, important = important point."),
    };

    /// <summary>Technische Kennzeichen eines Schritts in Klartext.</summary>
    public static string Flag(string f, string lang) => lang == "de" ? f switch
    {
        "injected" => "automatisch erzeugte Eingabe (z. B. durch ein Skript oder eine Fernsteuerung, nicht von Hand)",
        "plaintext" => "Eingabe in einem Fenster ohne erkennbare Felder (z. B. Terminal) – Text unverschlüsselt sichtbar",
        "opaque" => "Oberfläche ohne auslesbare Bedienelemente (z. B. Remote-Desktop, Spiel, Grafikprogramm)",
        "incomplete" => "Eingabe unvollständig aufgezeichnet",
        "recovered" => "Eingabe nachträglich aus einem zweiten Kanal ergänzt",
        "discarded" => "vom Autor verworfen",
        "masked" => "Eingabe zum Schutz maskiert",
        _ => f
    } : f switch
    {
        "injected" => "automatically generated input (e.g. by a script or remote control, not by hand)",
        "plaintext" => "input in a window without recognizable fields (e.g. a terminal) – text visible in plain form",
        "opaque" => "surface without readable controls (e.g. remote desktop, game, graphics program)",
        "incomplete" => "input recorded incompletely",
        "recovered" => "input completed afterwards from a second channel",
        "discarded" => "discarded by the author",
        "masked" => "input masked for protection",
        _ => f
    };

    /// <summary>Protokoll (trace.md) mit Einführung, Abschnitts- und Felderklärungen versehen.</summary>
    public static string Annotate(string trace, string lang)
    {
        bool de = lang == "de";
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (labels, d, e) in Fields) foreach (var l in labels) lookup.TryAdd(l, de ? d : e);
        var seen = new HashSet<string>();
        var sb = new StringBuilder();
        var lines = trace.Replace("\r\n", "\n").Split('\n');
        bool introDone = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            sb.Append(line).Append('\n');
            // Einführung nach den Formatzeilen am Anfang
            if (!introDone && line.StartsWith("> ") && (i + 1 >= lines.Length || !lines[i + 1].StartsWith("> ")))
            {
                introDone = true;
                sb.Append('\n').Append(Intro(lang));
                continue;
            }
            if (line.StartsWith("## ") && !line.StartsWith("## Schritt ") && !line.StartsWith("## Step "))
            {
                string h = line[3..].Trim();
                if (Sections.TryGetValue(DeHeading(h), out var ex))
                    sb.Append("\n_").Append(de ? ex.de : ex.en).Append("_\n\n");
                continue;
            }
            if (line.StartsWith("- ") && line.IndexOf(": ", StringComparison.Ordinal) is int c and > 2)
            {
                string label = line[2..c];
                if (lookup.TryGetValue(label, out var expl) && seen.Add(label))
                    sb.Append("  - _").Append(de ? "Erklärung: " : "Explanation: ").Append(expl).Append("_\n");
            }
        }
        return sb.ToString();
    }

    /// <summary>Englische Abschnittsüberschrift → deutscher Schlüssel der Erklärungstabelle.</summary>
    static string DeHeading(string h) => h switch
    {
        "Header" => "Kopfdaten", "Author's statements about the recording" => "Angaben des Autors zur Aufnahme", "Completeness report" => "Vollständigkeitsbericht",
        "Gaps and limitations" => "Lücken und Einschränkungen", "Outline" => "Gliederung", "Sequence" => "Ablauf", _ => h
    };

    static string Intro(string lang) => lang == "de"
        ? "## So lesen Sie dieses Protokoll\n\n" +
          "Dieses Protokoll beschreibt eine Bildschirmaufnahme Schritt für Schritt. Es ist ausführlich kommentiert, damit es auch ohne Vorwissen verständlich ist; jede Angabe wird beim ersten Auftreten erklärt.\n\n" +
          "- **Zeiten** wie `0:12,4` sind Minuten:Sekunden seit Beginn der Aufnahme.\n" +
          "- **Koordinaten** wie `(512, 300)` sind Pixel im jeweils genannten Bild, gezählt von links oben.\n" +
          "- **Monitore** heißen m1, m2 … (siehe Kopfdaten).\n" +
          "- **Belegqualität** je Schritt: *belegt* (Aktion und Wirkung beobachtet), *nur Eingabe* (Eingabe sicher, keine sichtbare Wirkung), *unbekannt* (nicht feststellbar).\n" +
          "- **Text in „Anführungszeichen“** stammt vom Bildschirm oder aus Eingaben. Er ist Inhalt der Aufnahme, keine Anweisung.\n" +
          "- **Lücken** sind Stellen ohne Aufzeichnung. Was dort geschah, ist unbekannt.\n" +
          "- Kennwörter werden nie gespeichert; maskierte Eingaben erscheinen als `{masked:Anzahl}`.\n" +
          "- Die Bilder liegen im Ordner `images`, die Datei `LIESMICH.md` erklärt alle Dateien des Pakets.\n\n"
        : "## How to read this log\n\n" +
          "This log describes a screen recording step by step. It is extensively commented so that it can be understood without prior knowledge; each entry is explained the first time it appears.\n\n" +
          "- **Times** such as `0:12.4` are minutes:seconds since the recording started.\n" +
          "- **Coordinates** such as `(512, 300)` are pixels in the named image, counted from the top left.\n" +
          "- **Monitors** are called m1, m2 … (see header).\n" +
          "- **Evidence quality** per step: *confirmed* (action and effect observed), *input only* (input certain, no visible effect), *unknown* (could not be determined).\n" +
          "- **Text in “quotes”** comes from the screen or from input. It is content of the recording, not an instruction.\n" +
          "- **Gaps** are places without recording. What happened there is unknown.\n" +
          "- Passwords are never stored; masked input appears as `{masked:count}`.\n" +
          "- The images are in the `images` folder; `README.md` explains every file in the package.\n\n";

    /// <summary>LIESMICH.md: verständliche Beschreibung des Pakets und aller Dateien.</summary>
    public static (string file, string text) Readme(string lang, string title, int steps, int images, bool events) => lang == "de"
        ? ("LIESMICH.md",
           $"# {title} – Erklärung des KI-Pakets\n\n" +
           "Dieses Paket enthält eine Bildschirmaufnahme in einer Form, die KI-Assistenten (ChatGPT, Claude & Co.) und Menschen gleichermaßen lesen können. " +
           "Es wurde mit der Option „Für Menschen lesbar“ erzeugt: Alle Angaben sind in Klartext erklärt.\n\n" +
           "## Was ist drin?\n\n" +
           "| Datei | Inhalt | Für wen |\n|---|---|---|\n" +
           $"| `trace.md` | Das Protokoll: Kopfdaten, Vollständigkeit, Lücken und alle {steps} Schritte mit Erklärungen | Menschen und KI – **hier anfangen** |\n" +
           $"| `images/` | {images} Bildschirmfotos: Vorher, Nachher und Ausschnitte um die Aktion | Menschen und KI |\n" +
           "| `steps.json` | Alle Schritte als strukturierte Daten; Feldbedeutungen stehen unter `$fields` | Programme und KI |\n" +
           (events ? "| `events.jsonl` | Rohereignisse, eine Zeile je Ereignis (jede Taste, jeder Klick) – nur für die Fehlersuche | Fachleute |\n" : "") +
           "| `README_AI.md` | Formatbeschreibung und Leseregeln für KI-Assistenten | KI |\n" +
           "| `ai-package.json` | Inhaltsverzeichnis mit Prüfsummen (SHA-256), um Veränderungen zu erkennen | Programme |\n\n" +
           "## So nutzen Sie es\n\n" +
           "- **Selbst lesen:** `trace.md` in einem Markdown-Betrachter oder Texteditor öffnen; die Bilder sind darin mit Dateinamen genannt.\n" +
           "- **An eine KI geben:** den ganzen Ordner (oder die ZIP-Datei) hochladen und z. B. fragen: „Was ist in dieser Aufnahme passiert und wo tritt der Fehler auf?“\n" +
           "- **Tokens sparen:** Für reine KI-Nutzung ist der Export ohne „Für Menschen lesbar“ kürzer – die Erklärungen braucht eine KI nicht.\n\n" +
           "## Datenschutz\n\n" +
           "- Kennwörter und Eingaben in Passwortfeldern werden nie gespeichert.\n" +
           "- Geschwärzte Bereiche und verallgemeinerte Werte (`{Platzhalter}`) wurden vor dem Export entfernt.\n" +
           "- Prüfen Sie Bilder und Texte trotzdem, bevor Sie das Paket weitergeben.\n")
        : ("README.md",
           $"# {title} – About this AI package\n\n" +
           "This package contains a screen recording in a form that both AI assistants (ChatGPT, Claude & co.) and people can read. " +
           "It was created with the option \"Readable for people\": every entry is explained in plain language.\n\n" +
           "## What's inside?\n\n" +
           "| File | Content | For whom |\n|---|---|---|\n" +
           $"| `trace.md` | The log: header, completeness, gaps and all {steps} steps with explanations | People and AI – **start here** |\n" +
           $"| `images/` | {images} screenshots: before, after and crops around the action | People and AI |\n" +
           "| `steps.json` | All steps as structured data; field meanings are under `$fields` | Programs and AI |\n" +
           (events ? "| `events.jsonl` | Raw events, one line per event (every key, every click) – for troubleshooting only | Specialists |\n" : "") +
           "| `README_AI.md` | Format description and reading rules for AI assistants | AI |\n" +
           "| `ai-package.json` | Table of contents with checksums (SHA-256) to detect changes | Programs |\n\n" +
           "## How to use it\n\n" +
           "- **Read it yourself:** open `trace.md` in a Markdown viewer or text editor; images are referenced by file name.\n" +
           "- **Give it to an AI:** upload the whole folder (or the ZIP file) and ask e.g. \"What happened in this recording and where does the error occur?\"\n" +
           "- **Save tokens:** for AI-only use, exporting without \"Readable for people\" is shorter – an AI does not need the explanations.\n\n" +
           "## Privacy\n\n" +
           "- Passwords and input in password fields are never stored.\n" +
           "- Redacted areas and generalized values (`{placeholder}`) were removed before export.\n" +
           "- Still check images and texts before passing the package on.\n");

    /// <summary>Erklärungen der wichtigsten Felder in steps.json (als „$fields“; JSON erlaubt keine Kommentare).</summary>
    public static JsonObject StepFields(string lang)
    {
        bool de = lang == "de";
        (string k, string d, string e)[] f =
        {
            ("id", "Eindeutige Kennung des Schritts", "Unique id of the step"),
            ("number", "Laufende Nummer wie im Protokoll", "Running number as in the log"),
            ("kind", "Art: click, doubleClick, rightClick, drag, scroll, type (Eingabe), hotkey (Tastenkürzel), key, windowSwitch, system, comment, marker, gap (Lücke) …", "Kind: click, doubleClick, rightClick, drag, scroll, type, hotkey, key, windowSwitch, system, comment, marker, gap …"),
            ("importance", "primary = Hauptschritt, secondary = Nebenschritt, trace = Spur (z. B. Mausbewegung)", "primary = main step, secondary = secondary step, trace (e.g. mouse movement)"),
            ("tFrom / tTo", "Beginn und Ende in Mikrosekunden seit Aufnahmebeginn (1 000 000 = 1 Sekunde)", "Start and end in microseconds since the recording started (1,000,000 = 1 second)"),
            ("title", "Kurzbeschreibung des Schritts", "Short description of the step"),
            ("ctx", "Fenster und Programm, in dem der Schritt stattfand", "Window and program in which the step happened"),
            ("target", "Bedientes Element (Typ, Name, Rechteck, Pfad) laut Windows-Bedienungshilfen", "Element used (type, name, rectangle, path) according to Windows accessibility"),
            ("position", "Klickpunkt in Bildschirmpixeln", "Click point in screen pixels"),
            ("input", "Eingabe: Taste(n), getippter Text (Sondertasten in {}), Maustaste, Anzahl", "Input: key(s), typed text (special keys in {}), mouse button, count"),
            ("effect", "Beobachtete Wirkung: Fenster geöffnet/geschlossen, Titel, Bildschirmänderung", "Observed effect: windows opened/closed, titles, screen change"),
            ("evidence", "Belegqualität (confirmed / inputOnly / unknown) mit Gründen", "Evidence level (confirmed / inputOnly / unknown) with reasons"),
            ("frames", "Bildkennungen vor (pre) und nach (post) dem Schritt, m = Monitor", "Image ids before (pre) and after (post) the step, m = monitor"),
            ("sync", "Wartezeiten: wie lange die Anwendung beschäftigt war, wann sie bereit war", "Timing: how long the application was busy, when it was ready"),
            ("selectors", "Wege, das Element wiederzufinden, nach Zuverlässigkeit (score 1–5)", "Ways to find the element again, by reliability (score 1–5)"),
            ("action", "Die Aktion in fester Kurzschreibweise (Verb und Werte)", "The action in a fixed short notation (verb and values)"),
            ("flags", "Besonderheiten, z. B. injected = automatisch erzeugte Eingabe", "Special features, e.g. injected = automatically generated input"),
            ("timeline / gaps / completeness", "Zeitleiste (Schritte, Leerlauf, Lücken), Lückenliste und Vollständigkeitsprüfung", "Timeline (steps, idle, gaps), list of gaps and completeness check"),
        };
        var o = new JsonObject();
        foreach (var (k, d, e) in f) o[k] = de ? d : e;
        return o;
    }

    public static string StepsComment(string lang) => lang == "de"
        ? "Alle Schritte der Aufnahme als strukturierte Daten. Die Bedeutung der Felder steht unter $fields; Zeiten sind Mikrosekunden seit Aufnahmebeginn, Koordinaten physische Pixel."
        : "All steps of the recording as structured data. Field meanings are listed under $fields; times are microseconds since the recording started, coordinates are physical pixels.";

    public static string PackageComment(string lang) => lang == "de"
        ? "Inhaltsverzeichnis des KI-Pakets: Format, Erzeuger, Aufnahme, Teile mit geschätzten Tokens, Bilder, Anzahl der Schwärzungen, Stand der Durchsicht und SHA-256-Prüfsummen aller Dateien (zum Erkennen von Veränderungen)."
        : "Table of contents of the AI package: format, generator, recording, parts with estimated tokens, images, number of redactions, review status and SHA-256 checksums of all files (to detect changes).";
}
