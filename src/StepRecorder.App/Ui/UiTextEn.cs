using System.Text.RegularExpressions;

namespace StepRecorder.App.Ui;

/// <summary>
/// Englische Oberflächentexte: exakte Übersetzungen und Muster für zusammengesetzte Texte.
/// <para>
/// Anwendung: zuerst <see cref="Exact"/> für den ganzen Text; sonst alle <see cref="Patterns"/> der Reihe nach mit
/// <c>rx.Replace(text, replacement)</c> anwenden (nicht nach dem ersten Treffer aufhören). Die Muster sind so gebaut,
/// dass das gefahrlos ist: Teilstücke von Texten, die mit „ · “ zusammengesetzt sind (Statuszeile, Bildunterschrift,
/// „zuletzt …“), werden einzeln erkannt, Tastennamen in Kürzeln (Strg+…, Umschalt+…) zuletzt ersetzt, und kein
/// englisches Ergebnis passt wieder auf ein Muster.
/// </para>
/// </summary>
static class UiTextEn
{
    const string DN = "Sebastians Step Recorder";

    // Bausteine zusammengesetzter Texte (RecordingController.Start)
    const string InvOk = "Unsichtbarkeit geprüft · ", InvOkEn = "Invisibility verified · ";
    const string InvHidden = "Ausgeblendete Anzeige – Achtung: eingeblendet ist die Leiste auf den Bildern zu sehen · ", InvHiddenEn = "Hidden indicator – caution: when shown, the bar is visible in the images · ";
    const string InvNo = "Unsichtbarkeit nicht bestätigt · ", InvNoEn = "Invisibility not confirmed · ";
    const string Weak = "Schutznetz – Passwortfelder werden maskiert; in Terminals und Remote-Desktop Privattaste (Strg+Umschalt+F8) nutzen.", WeakEn = "Safety net – password fields are masked; use the privacy key (Ctrl+Shift+F8) in terminals and Remote Desktop.";
    const string Strict = "Streng – Eingaben in unbekannten Feldern werden maskiert.", StrictEn = "Strict – input in unknown fields is masked.";

    // Startfenster: Modi
    const string ModeGuide = "Schlank – nur was Menschen zum Nachmachen brauchen.", ModeGuideEn = "Lean – only what people need to follow along.";
    const string ModeSupport = "Ausführlich – alle Eingaben, damit der Helpdesk den Fehler nachvollziehen kann.", ModeSupportEn = "Detailed – all input, so the helpdesk can reproduce the problem.";
    const string ModeAi = "Vollständig – so genau wie möglich, für KI-Assistenten und AutoHotkey.", ModeAiEn = "Complete – as precise as possible, for AI assistants and AutoHotkey.";
    const string ModeCustom = "Selbst festgelegt – „Einstellen …“ öffnet die Auswahl.", ModeCustomEn = "User-defined – \"Configure …\" opens the options.";

    // Detailgrad (DetailPresets mit Standardwerten: ruhig 0,3 s, spätestens 3 s)
    const string BaseRule = "Immer: ein Bild direkt vor jeder Eingabe und eins, sobald der Bildschirm danach 0,3 Sekunden ruhig ist (spätestens nach 3 Sekunden).";
    const string BaseRuleEn = "Always: one image right before each input and one as soon as the screen has been still for 0.3 seconds afterwards (after 3 seconds at the latest).";
    const string RuleFew = "Zusatzbild, wenn sich der Bildschirm ohne Eingabe ändert – ab ca. 2 % des Bildschirms (etwa 200 × 200 Pixel bei Full HD), z. B. ein neues Fenster oder ein Seitenwechsel –, sobald er danach 0,3 Sekunden ruhig ist (bei Dauerbewegung spätestens nach 5 Sekunden); höchstens alle 2 Sekunden.";
    const string RuleFewEn = "Extra image when the screen changes without input – from about 2 % of the screen (roughly 200 × 200 pixels at Full HD), e.g. a new window or a page change – as soon as it has been still for 0.3 seconds (after 5 seconds at the latest with continuous motion); at most every 2 seconds.";
    const string RuleNormal = "Zusatzbild, wenn sich der Bildschirm ohne Eingabe ändert – ab ca. 0,1 % des Bildschirms (etwa 45 × 45 Pixel), z. B. eine Meldung, ein Tooltip oder eine Statusanzeige –, sobald er danach 0,3 Sekunden ruhig ist (bei Dauerbewegung spätestens nach 5 Sekunden); höchstens alle 0,5 Sekunden.";
    const string RuleNormalEn = "Extra image when the screen changes without input – from about 0.1 % of the screen (roughly 45 × 45 pixels), e.g. a message, a tooltip or a status display – as soon as it has been still for 0.3 seconds (after 5 seconds at the latest with continuous motion); at most every 0.5 seconds.";
    const string RuleMany = "Zusatzbild, wenn sich der Bildschirm ohne Eingabe ändert – ab ca. 0,01 % des Bildschirms (etwa 15 × 15 Pixel), z. B. Fortschrittsbalken, Zähler oder Ladeanimationen –, sobald er danach 0,3 Sekunden ruhig ist (bei Dauerbewegung spätestens nach 5 Sekunden); höchstens alle 0,25 Sekunden.";
    const string RuleManyEn = "Extra image when the screen changes without input – from about 0.01 % of the screen (roughly 15 × 15 pixels), e.g. progress bars, counters or loading animations – as soon as it has been still for 0.3 seconds (after 5 seconds at the latest with continuous motion); at most every 0.25 seconds.";

    // Exportdialog: Inhaltsbeschreibung der Anleitung
    const string GuideA = "Titel, Einleitung, Voraussetzungen, Kapitel und ", GuideAEn = "Title, introduction, prerequisites, chapters and ";
    const string GuideB = " – jeweils mit Überschrift, Anleitungstext, Bild und Hinweiskästen; Deckblatt, Inhaltsverzeichnis, Bildgröße und Farbe stellen Sie im Editor im Reiter „Dokument“ ein";
    const string GuideBEn = " – each with heading, guide text, image and callouts; set the cover page, table of contents, image size and color in the editor on the \"Document\" tab";
    const string ImpBoth = "Haupt- und Nebenschritte", ImpBothEn = "main and secondary steps";
    const string ImpAll = "alle Schritte", ImpAllEn = "all steps";
    const string ImpMain = "alle Hauptschritte", ImpMainEn = "all main steps";
    const string SufMd = " (als Markdown-Text).", SufMdEn = " (as Markdown text).";
    const string SufEmb = ". Die Bilder sind eingebettet.", SufEmbEn = ". The images are embedded.";
    const string SufHtml = ". Die Bilder stecken in der Datei – es gibt nur diese eine Datei.", SufHtmlEn = ". The images are inside the file – there is only this one file.";
    const string SavedIn = "Gespeichert im Aufnahmeordner unter ", SavedInEn = "Saved in the recording folder under ";

    public static readonly Dictionary<string, string> Exact = new(StringComparer.Ordinal)
    {
        // ---------------- Controls.cs (Msg) ----------------
        ["Ja"] = "Yes",
        ["Nein"] = "No",
        ["Abbrechen"] = "Cancel",

        // ---------------- Tastennamen (U.Kbd zerlegt Kürzel an „+“; Menü-Kürzel) ----------------
        ["Strg"] = "Ctrl",
        ["Umschalt"] = "Shift",
        ["Entf"] = "Del",
        ["Einfg"] = "Ins",
        ["Strg+Eingabe"] = "Ctrl+Enter",
        ["Umschalt+Eingabe"] = "Shift+Enter",
        ["Strg+Z"] = "Ctrl+Z",
        ["Strg+Y"] = "Ctrl+Y",
        ["Strg+C"] = "Ctrl+C",
        ["Strg+E"] = "Ctrl+E",
        ["Strg+F"] = "Ctrl+F",
        ["Strg+H"] = "Ctrl+H",
        ["Strg+M"] = "Ctrl+M",
        ["Strg+N"] = "Ctrl+N",
        ["Strg+O"] = "Ctrl+O",
        ["Strg+↑"] = "Ctrl+↑",
        ["Strg+↓"] = "Ctrl+↓",
        ["Strg+Umschalt+C"] = "Ctrl+Shift+C",
        ["Strg+Umschalt+G"] = "Ctrl+Shift+G",
        ["Strg+Umschalt+O"] = "Ctrl+Shift+O",
        ["Strg+Umschalt+R"] = "Ctrl+Shift+R",
        ["Strg+Umschalt+S"] = "Ctrl+Shift+S",
        ["Strg+Umschalt+T"] = "Ctrl+Shift+T",
        ["Strg+Umschalt+F1"] = "Ctrl+Shift+F1",
        ["Strg+Umschalt+F2"] = "Ctrl+Shift+F2",
        ["Strg+Umschalt+F4"] = "Ctrl+Shift+F4",
        ["Strg+Umschalt+F5"] = "Ctrl+Shift+F5",
        ["Strg+Umschalt+F6"] = "Ctrl+Shift+F6",
        ["Strg+Umschalt+F7"] = "Ctrl+Shift+F7",
        ["Strg+Umschalt+F8"] = "Ctrl+Shift+F8",
        ["Strg+Umschalt+F9"] = "Ctrl+Shift+F9",
        ["Strg+Umschalt+F10"] = "Ctrl+Shift+F10",
        ["Strg+Umschalt+F11"] = "Ctrl+Shift+F11",
        ["Strg+Umschalt+F12"] = "Ctrl+Shift+F12",
        ["Umschalt+Entf"] = "Shift+Del",
        ["Umschalt+Einfg"] = "Shift+Ins",
        ["Strg+Esc"] = "Ctrl+Esc",

        // ---------------- DevSnapshots.cs (nur Entwickler-Schnappschüsse) ----------------
        ["Klicken Sie auf „Speichern“."] = "Click \"Save\".",
        ["Datei speichern"] = "Save file",
        ["Unsichtbarkeit geprüft · Schutznetz – Passwortfelder werden maskiert."] = "Invisibility verified · Safety net – password fields are masked.",

        // ---------------- ImageViewing.cs ----------------
        ["Verkleinern (Strg+Minus, Strg+Mausrad)"] = "Zoom out (Ctrl+Minus, Ctrl+mouse wheel)",
        ["Vergrößern (Strg+Plus, Strg+Mausrad)"] = "Zoom in (Ctrl+Plus, Ctrl+mouse wheel)",
        ["Einpassen"] = "Fit",
        ["Ganzes Bild zeigen (Strg+0)"] = "Show whole image (Ctrl+0)",
        ["Originalgröße (Strg+1)"] = "Actual size (Ctrl+1)",
        ["Großansicht"] = "Large view",
        ["Schließen"] = "Close",
        ["Schließen (Esc)"] = "Close (Esc)",
        ["Vorheriges Bild (←)"] = "Previous image (←)",
        ["Nächstes Bild (→)"] = "Next image (→)",
        ["Strg+Mausrad zoomen · Leertaste oder mittlere Maustaste + Ziehen verschieben · Doppelklick: Einpassen / 100 % · ← → blättern · Esc schließt"] = "Ctrl+mouse wheel to zoom · Space or middle mouse button + drag to pan · Double-click: fit / 100 % · ← → browse · Esc closes",

        // ---------------- RecordingController.cs ----------------
        ["Aufnahme startet in"] = "Recording starts in",
        ["Aufnahme pausiert (Sperre/Benutzerwechsel) – mit Fortsetzen weiter."] = "Recording paused (lock/user switch) – press Resume to continue.",
        [Weak] = WeakEn,
        [Strict] = StrictEn,
        [InvOk + Weak] = InvOkEn + WeakEn,
        [InvOk + Strict] = InvOkEn + StrictEn,
        [InvHidden + Weak] = InvHiddenEn + WeakEn,
        [InvHidden + Strict] = InvHiddenEn + StrictEn,
        [InvNo + Weak] = InvNoEn + WeakEn,
        [InvNo + Strict] = InvNoEn + StrictEn,
        ["Achtung: Leiste und Rahmen sind jetzt auf den Bildern zu sehen – Strg+Umschalt+F4 blendet sie wieder aus."] = "Caution: the bar and frame are now visible in the images – Ctrl+Shift+F4 hides them again.",
        [DN + " – Aufnahme läuft"] = DN + " – Recording",
        [DN + " – Aufnahme"] = DN + " – Recording",
        ["Fortsetzen"] = "Resume",
        ["Beenden und speichern"] = "Stop and save",
        ["Fehlerstelle markieren"] = "Mark error",
        ["Marker setzen"] = "Set marker",
        ["Kommentar …"] = "Comment …",
        ["Schnappschuss"] = "Snapshot",
        ["Privattaste aus"] = "Privacy key off",
        ["Privattaste ein"] = "Privacy key on",
        ["Letzten Schritt verwerfen"] = "Discard last step",
        ["Leiste ausblenden"] = "Hide bar",
        ["Leiste anzeigen"] = "Show bar",
        ["Aufnahme verwerfen …"] = "Discard recording …",
        ["Privat – Eingaben werden nicht gespeichert."] = "Private – input is not saved.",
        ["Privatmodus aus."] = "Private mode off.",
        ["Schnappschuss gespeichert"] = "Snapshot saved",
        ["Kommentar gespeichert"] = "Comment saved",
        ["Es läuft bereits eine Aufnahme."] = "A recording is already running.",
        ["Aufnahme verwerfen?\n\nDer Sitzungsordner wird gelöscht. Das lässt sich nicht rückgängig machen."] = "Discard recording?\n\nThe session folder will be deleted. This cannot be undone.",
        ["Aufnahme verwerfen"] = "Discard recording",
        ["Wird verarbeitet …"] = "Processing …",
        ["Leiste verschieben"] = "Move bar",
        ["Pause / Fortsetzen (Strg+Umschalt+F9)"] = "Pause / Resume (Ctrl+Shift+F9)",
        ["Aufnahme beenden (Strg+Umschalt+F10)"] = "Stop recording (Ctrl+Shift+F10)",
        ["Stopp"] = "Stop",
        ["Kommentar (Strg+Umschalt+F12)"] = "Comment (Ctrl+Shift+F12)",
        ["Marker setzen (Strg+Umschalt+F7) – Rechtsklick: Art wählen"] = "Set marker (Ctrl+Shift+F7) – right-click: choose type",
        ["Notiz"] = "Note",
        ["Fehler"] = "Error",
        ["Erwartet"] = "Expected",
        ["Wichtig"] = "Important",
        ["Schnappschuss (Strg+Umschalt+F11)"] = "Snapshot (Ctrl+Shift+F11)",
        ["Privattaste: Eingaben nicht speichern (Strg+Umschalt+F8)"] = "Privacy key: don't save input (Ctrl+Shift+F8)",
        ["Letzten Schritt verwerfen (Strg+Umschalt+F6)"] = "Discard last step (Ctrl+Shift+F6)",
        ["Weitere Funktionen"] = "More functions",
        ["In den Infobereich minimieren (Strg+Umschalt+F4) – Symbol unten rechts in der Taskleiste"] = "Minimize to the notification area (Ctrl+Shift+F4) – icon at the bottom right of the taskbar",
        ["Letzte Eingabe schwärzen"] = "Redact last input",
        ["Marker mit Text …"] = "Marker with text …",
        ["Marker-Text"] = "Marker text",
        ["In den Infobereich minimieren"] = "Minimize to notification area",
        ["Tastenkürzel …"] = "Shortcuts …",
        ["Tastenkürzel während der Aufnahme\n\nStrg+Umschalt+F9  Pause / Fortsetzen\nStrg+Umschalt+F10  Stopp\nStrg+Umschalt+F12  Kommentar\nStrg+Umschalt+F5  Diktat ein/aus\nStrg+Umschalt+F7  Marker\nStrg+Umschalt+F1  Marker „Fehler“\nStrg+Umschalt+F2  Marker „Erwartet“\nStrg+Umschalt+F11  Schnappschuss\nStrg+Umschalt+F8  Privattaste\nStrg+Umschalt+F6  Letzten Schritt verwerfen\nStrg+Umschalt+F4  Leiste ein/aus\n\nAlle Befehle gibt es auch per Rechtsklick auf das Symbol unten rechts im Infobereich der Taskleiste."]
            = "Shortcuts during recording\n\nCtrl+Shift+F9  Pause / Resume\nCtrl+Shift+F10  Stop\nCtrl+Shift+F12  Comment\nCtrl+Shift+F5  Dictation on/off\nCtrl+Shift+F7  Marker\nCtrl+Shift+F1  Marker \"Error\"\nCtrl+Shift+F2  Marker \"Expected\"\nCtrl+Shift+F11  Snapshot\nCtrl+Shift+F8  Privacy key\nCtrl+Shift+F6  Discard last step\nCtrl+Shift+F4  Bar on/off\n\nAll commands are also available by right-clicking the icon at the bottom right in the taskbar's notification area.",
        ["Tastenkürzel"] = "Shortcuts",
        ["Diktat"] = "Dictation",
        ["Diktat beenden"] = "Stop dictation",
        ["Diktat ein/aus (Mikrofon, offline)"] = "Dictation on/off (microphone, offline)",
        ["Diktat (Mikrofon)"] = "Dictation (microphone)",
        ["Diktat starten (Mikrofon)"] = "Start dictation (microphone)",
        ["Diktat beendet – Mikrofon aus."] = "Dictation stopped – microphone off.",
        ["Privattaste aktiv – Diktat nicht gespeichert."] = "Privacy key active – dictation not saved.",
        ["Diktat läuft – klicken zum Beenden (Strg+Umschalt+F5)"] = "Dictation running – click to stop (Ctrl+Shift+F5)",
        ["Diktat: Gesprochenes wird offline zu Kommentaren (Strg+Umschalt+F5)"] = "Dictation: speech is turned into comments offline (Ctrl+Shift+F5)",
        ["1 Schritt"] = "1 step",
        ["PRIVAT"] = "PRIVATE",
        ["Was passiert hier? Was ist wichtig?"] = "What is happening here? What matters?",
        ["Kommentar"] = "Comment",
        ["neue Zeile"] = "new line",
        ["speichert"] = "saves",
        ["verwirft"] = "discards",
        ["Speichern"] = "Save",
        ["Verwerfen"] = "Discard",
        ["Künftig ohne Nachfrage mit ausgeblendeter Anzeige starten"] = "From now on, start with the hidden indicator without asking",
        ["Aufnahme ohne sichtbare Anzeige"] = "Recording without visible indicator",
        ["Dieses Windows kann die Leiste und den roten Rahmen nicht aus den Bildschirmfotos ausschließen – sie wären auf jedem Bild zu sehen. Das betrifft z. B. Windows Server 2019 und Windows 10 vor Version 2004."]
            = "This version of Windows cannot exclude the bar and the red frame from screenshots – they would be visible in every image. This affects e.g. Windows Server 2019 and Windows 10 before version 2004.",
        ["Empfohlen: mit ausgeblendeter Anzeige aufnehmen. Die Bilder bleiben sauber; gesteuert wird über Tastenkürzel:"] = "Recommended: record with the indicator hidden. The images stay clean; you control the recording with shortcuts:",
        ["Aufnahme beenden"] = "Stop recording",
        ["Pause / Fortsetzen"] = "Pause / Resume",
        ["Leiste und Rahmen ein- und ausblenden (eingeblendet auf den Bildern sichtbar)"] = "Show/hide bar and frame (visible in the images while shown)",
        ["Privattaste – Eingaben nicht speichern"] = "Privacy key – don't save input",
        ["Alternativ: Rechtsklick auf das Aufnahme-Symbol unten rechts im Infobereich der Taskleiste – dort gibt es alle Befehle als Menü."] = "Alternatively: right-click the recording icon at the bottom right in the taskbar's notification area – all commands are available there as a menu.",
        ["Ausgeblendet starten"] = "Start hidden",
        ["Mit Anzeige starten"] = "Start with indicator",
        ["Leiste und Rahmen erscheinen auf den Bildern"] = "Bar and frame will appear in the images",
        ["Unsichtbarkeit nicht bestätigt"] = "Invisibility not confirmed",

        // ---------------- StartWindow.cs ----------------
        ["Anleitung"] = "Guide",
        ["für Menschen"] = "for people",
        [ModeGuide] = ModeGuideEn,
        ["Support-Fall"] = "Support case",
        ["für den Helpdesk"] = "for the helpdesk",
        [ModeSupport] = ModeSupportEn,
        ["KI-Spur"] = "AI trace",
        ["für KI und Skripte"] = "for AI and scripts",
        [ModeAi] = ModeAiEn,
        ["Eigene"] = "Custom",
        ["selbst festgelegt"] = "user-defined",
        [ModeCustom] = ModeCustomEn,
        ["Anleitung – für Menschen\n" + ModeGuide] = "Guide – for people\n" + ModeGuideEn,
        ["Support-Fall – für den Helpdesk\n" + ModeSupport] = "Support case – for the helpdesk\n" + ModeSupportEn,
        ["KI-Spur – für KI und Skripte\n" + ModeAi] = "AI trace – for AI and scripts\n" + ModeAiEn,
        ["Eigene – selbst festgelegt\n" + ModeCustom] = "Custom – user-defined\n" + ModeCustomEn,
        ["Öffnen"] = "Open",
        ["Aufnahme öffnen (Strg+O) – oder einen Aufnahmeordner hierher ziehen"] = "Open recording (Ctrl+O) – or drag a recording folder here",
        ["Einstellungen"] = "Settings",
        ["Hilfe"] = "Help",
        ["Was möchten Sie festhalten?"] = "What would you like to capture?",
        ["Anpassen …"] = "Customize …",
        ["Einstellen …"] = "Configure …",
        ["Detailgrad: wie genau aufgezeichnet wird"] = "Level of detail: how precisely things are recorded",
        [BaseRule + " Passwortfelder werden in allen Modi maskiert."] = BaseRuleEn + " Password fields are masked in all modes.",
        ["Monitor mit Aktivität"] = "Monitor with activity",
        ["Alle Monitore"] = "All monitors",
        ["Welcher Bildschirm bei „Ganzer Bildschirm“ aufgenommen wird"] = "Which screen is recorded with \"Full screen\"",
        ["Ziel und Titel …"] = "Goal and title …",
        ["Ziel und Titel ✓"] = "Goal and title ✓",
        ["Ziel und Titel (empfohlen) …"] = "Goal and title (recommended) …",
        ["Optional: was die Aufnahme zeigen soll – hilft Menschen und KI beim Verstehen"] = "Optional: what the recording should show – helps people and AI understand it",
        ["Was soll aufgenommen werden?"] = "What should be recorded?",
        ["Eine Anwendung"] = "One application",
        ["danach Fenster anklicken"] = "then click a window",
        ["Nur die Fenster eines Programms aufnehmen – alles andere bleibt außen vor"] = "Record only the windows of one program – everything else stays out",
        ["Bereich"] = "Region",
        ["danach Rechteck aufziehen"] = "then drag a rectangle",
        ["Einen festen Ausschnitt des Bildschirms aufnehmen"] = "Record a fixed area of the screen",
        ["Wofür ist die Aufnahme?"] = "What is the recording for?",
        ["Neue Aufnahme"] = "New recording",
        ["Sofort starten · Pause"] = "Start instantly · Pause",
        ["Beenden"] = "Stop",
        ["Privattaste"] = "Privacy key",
        ["Alle Befehle auch per Rechtsklick auf das Symbol unten rechts im Infobereich."] = "All commands are also available by right-clicking the icon at the bottom right in the notification area.",
        ["Zuletzt"] = "Recent",
        ["Aufnahmeordner im Explorer öffnen"] = "Open recordings folder in Explorer",
        ["synchronisierter Ordner"] = "synced folder",
        ["Ganzer Bildschirm"] = "Full screen",
        ["Aufnahme starten"] = "Start recording",
        ["Alles auf dem Monitor, auf dem gerade gearbeitet wird (Eingabetaste)"] = "Everything on the monitor you are currently working on (Enter key)",
        ["Klicks"] = "Clicks",
        ["Mausklicks mit Bild und angeklicktem Element, Scrollen"] = "Mouse clicks with image and clicked element, scrolling",
        ["Tastatur"] = "Keyboard",
        ["ohne Tastatur"] = "no keyboard",
        ["Getippter Text, Tastenkombinationen und Einzeltasten (Passwortfelder immer maskiert)"] = "Typed text, key combinations and single keys (password fields always masked)",
        ["Mauswege"] = "Mouse paths",
        ["ohne Mauswege"] = "no mouse paths",
        ["Mausbewegungen und Verweilen über Elementen – nützlich für KI und Fehlersuche"] = "Mouse movements and hovering over elements – useful for AI and troubleshooting",
        ["Zusatzbilder: nur große Änderungen"] = "Extra images: large changes only",
        ["Zusatzbilder: auch kleine Änderungen"] = "Extra images: small changes too",
        ["Zusatzbilder: jede sichtbare Änderung"] = "Extra images: every visible change",
        ["Programmkontext erweitert"] = "Application context: extended",
        ["Programmkontext Basis"] = "Application context: basic",
        ["Zusätzlich Menüpfade, Listen, Explorer-Ordner, Browser-Adresse, Office-Dokument, Statusleiste – für AutoHotkey und KI"] = "Also menu paths, lists, Explorer folders, browser address, Office document, status bar – for AutoHotkey and AI",
        ["Fenster und angeklicktes Element – reicht für Anleitungen"] = "Window and clicked element – enough for guides",
        ["Editor öffnen"] = "Open editor",
        ["Anleitung im Editor – Export in jedes Format"] = "Guide in the editor – export to any format",
        ["KI-Paket exportieren"] = "Export AI package",
        ["KI-Paket: Protokoll, Bilder und Lesehilfe"] = "AI package: trace, images and reading guide",
        ["Support-Paket (ZIP) erstellen"] = "Create support package (ZIP)",
        ["ZIP-Paket: Anleitung (HTML) + KI-Paket"] = "ZIP package: guide (HTML) + AI package",
        ["Aufnahmeordner öffnen"] = "Open recording folder",
        ["Aufnahme im Ordner – später im Editor öffnen"] = "Recording in the folder – open in the editor later",
        ["Wird gespeichert und gilt für den Modus „Eigene“. Ausgeschaltetes wird gar nicht erst aufgezeichnet."] = "Saved and applies to the \"Custom\" mode. Anything switched off is not recorded at all.",
        ["Nur große"] = "Large only",
        ["Auch kleine"] = "Small too",
        ["Jede Änderung"] = "Every change",
        ["Zusatzbilder ohne Eingabe"] = "Extra images without input",
        ["Erweiterter Programmkontext"] = "Extended application context",
        ["Menüpfade, Listen, Explorer-Ordner, Browser-Adresse, Office-Dokument und Statusleiste – nötig für gute AutoHotkey-Skripte"] = "Menu paths, lists, Explorer folders, browser address, Office document and status bar – needed for good AutoHotkey scripts",
        ["Programmkontext"] = "Application context",
        ["Nach dem Stopp"] = "After stopping",
        ["Eigener Detailgrad"] = "Custom level of detail",
        ["Fertig"] = "Done",
        ["z. B. Neuen Kontakt im Adressbuch anlegen"] = "e.g. Create a new contact in the address book",
        ["z. B. Der Kontakt erscheint in der Liste"] = "e.g. The contact appears in the list",
        ["z. B. Ticketnummer"] = "e.g. ticket number",
        ["sonst automatisch aus der Aufnahme"] = "otherwise taken from the recording",
        ["Was wird gezeigt?"] = "What is shown?",
        ["Erwartetes Ergebnis"] = "Expected result",
        ["Bezug"] = "Reference",
        ["Titel der Aufnahme"] = "Recording title",
        ["Ziel und Titel"] = "Goal and title",
        ["Optional. Hilft beim Verstehen der Aufnahme – für Menschen und für KI."] = "Optional. Helps people and AI understand the recording.",
        ["Ziel der Aufnahme"] = "Recording goal",
        ["Übernehmen"] = "Apply",
        ["Leeren"] = "Clear",
        ["Eingabearten, Zusatzbilder, Programmkontext und „nach dem Stopp“ festlegen"] = "Set input types, extra images, application context and \"after stopping\"",
        ["Diesen Detailgrad als Ausgangspunkt für den Modus „Eigene“ übernehmen"] = "Use this level of detail as the starting point for the \"Custom\" mode",
        ["1 Aufnahme"] = "1 recording",
        ["Schutz: streng"] = "Protection: strict",
        ["Passwortfelder werden maskiert"] = "Password fields are masked",
        ["Modus wählen"] = "Choose a mode",
        ["Im Zweifel „Anleitung“."] = "If in doubt, \"Guide\".",
        ["Bereich anklicken"] = "Click an area",
        ["Die Aufnahme startet sofort."] = "Recording starts immediately.",
        ["Vorführen und beenden"] = "Demonstrate and stop",
        ["Nachbearbeiten"] = "Edit",
        ["Texte anpassen, als PDF oder Word speichern."] = "Adjust texts, save as PDF or Word.",
        ["Problem aufnehmen\n\n1. Aufnahme starten und das Problem vorführen.\n2. Fehlerstelle mit Strg+Umschalt+F1 markieren, mit Strg+Umschalt+F10 beenden.\n3. Eingaben durchsehen und das ZIP erstellen.\n\nDas Programm versendet nichts – Sie geben das ZIP selbst weiter. Aufnahme jetzt starten?"]
            = "Record a problem\n\n1. Start the recording and demonstrate the problem.\n2. Mark the error with Ctrl+Shift+F1, stop with Ctrl+Shift+F10.\n3. Review the input and create the ZIP.\n\nThe program does not send anything – you pass on the ZIP yourself. Start recording now?",
        ["Support-Paket"] = "Support package",
        ["Aufnahme (manifest.json;*.steprec)|manifest.json;*.steprec"] = "Recording (manifest.json;*.steprec)|manifest.json;*.steprec",
        ["So funktioniert " + DN + "\n\n" +
         "1. Modus wählen: Anleitung (schlank), Support-Fall (ausführlich), KI-Spur (vollständig) oder Eigene (selbst festgelegt). Der Modus bestimmt den Detailgrad – Eingabearten, Zusatzbilder bei Bildschirmänderungen, Programmkontext – und was nach dem Stopp passiert.\n\n" +
         "2. Bereich wählen: ganzer Bildschirm, eine Anwendung oder ein Ausschnitt. Ein Klick startet die Aufnahme; vorführen und mit Strg+Umschalt+F10 beenden.\n\n" +
         "Privattaste Strg+Umschalt+F8: Eingaben werden nur gezählt, nicht gespeichert.\n\n" +
         "Editor: Texte direkt rechts ändern, Kapitel und Hinweiskästen am Schritt anlegen, im Reiter „Dokument“ Deckblatt, Inhaltsverzeichnis, Bildgröße und Farbe festlegen, Werkzeuge links am Bild (V Auswahl, A Pfeil, R Rahmen, E Ellipse, M Hervorheben, T Text, N Nummer, X Schwärzen), Markierungen anklicken und mit Entf löschen, Schritte per Ziehen umsortieren. Strg+C kopiert das Bild, Strg+E exportiert.\n\n" +
         "KI-Zugriff: SebastiansStepRecorder.exe mcp --root <Aufnahmeordner> startet den MCP-Server (stdio).\n\n" +
         "Portabel, offline, ohne Installation. Das Programm ist in seinen eigenen Bildern nie zu sehen.\n\n" +
         "Kostenlos nutzbar, auch beruflich. Lizenz: CC BY-ND 4.0 (Namensnennung – keine Bearbeitungen). © 2026 Sebedai · github.com/Sebedai/sebastians-step-recorder"]
            = "How " + DN + " works\n\n" +
              "1. Choose a mode: Guide (lean), Support case (detailed), AI trace (complete) or Custom (user-defined). The mode determines the level of detail – input types, extra images on screen changes, application context – and what happens after stopping.\n\n" +
              "2. Choose an area: full screen, one application or a region. One click starts the recording; demonstrate, then stop with Ctrl+Shift+F10.\n\n" +
              "Privacy key Ctrl+Shift+F8: input is only counted, not saved.\n\n" +
              "Editor: change texts directly on the right, add chapters and callouts on the step, set cover page, table of contents, image size and color on the \"Document\" tab, use the tools to the left of the image (V select, A arrow, R rectangle, E ellipse, M highlight, T text, N number, X redact), click markings and delete them with Del, reorder steps by dragging. Ctrl+C copies the image, Ctrl+E exports.\n\n" +
              "AI access: SebastiansStepRecorder.exe mcp --root <recordings folder> starts the MCP server (stdio).\n\n" +
              "Portable, offline, no installation. The program never appears in its own images.\n\n" +
              "Free to use, including commercially. License: CC BY-ND 4.0 (Attribution – NoDerivatives). © 2026 Sebedai · github.com/Sebedai/sebastians-step-recorder",
        ["Alles ausgeschaltet – es entstehen nur noch Bilder bei Bildschirmänderungen."] = "Everything switched off – images are only taken on screen changes.",
        ["Maus"] = "Mouse",
        ["Aufgezeichnet: "] = "Recorded: ",
        ["keine Eingaben"] = "no input",
        ["   ·   Nicht: "] = "   ·   Not: ",
        ["abbrechen"] = "cancel",
        ["Anwendung anklicken, die aufgenommen werden soll"] = "Click the application to record",
        ["Rechteck aufziehen, das aufgenommen werden soll"] = "Drag a rectangle to record",
        ["mindestens 64 × 64 px"] = "at least 64 × 64 px",
        ["Deutsch"] = "German",
        ["Sie (förmlich)"] = "Sie (formal)",
        ["du (persönlich)"] = "du (informal)",
        ["Schutznetz – Passwortfelder maskieren"] = "Safety net – mask password fields",
        ["Streng – unbekannte Felder maskieren"] = "Strict – mask unknown fields",
        ["Der Ordner wird synchronisiert – Aufnahmen können dadurch Unbeteiligten zugänglich werden."] = "This folder is synced – recordings may become accessible to others.",
        ["Aufnahmeordner wählen"] = "Choose recordings folder",
        ["Ordner wählen …"] = "Choose folder …",
        ["Dunkel"] = "Dark",
        ["Hell"] = "Light",
        ["Wie Windows"] = "Same as Windows",
        ["Ohne Nachfrage mit ausgeblendeter Anzeige aufnehmen, wenn die Unsichtbarkeit nicht bestätigt werden kann"] = "Record with the hidden indicator without asking if invisibility cannot be confirmed",
        ["Z. B. unter Windows Server 2019: Leiste und Rahmen bleiben ausgeblendet, gesteuert wird über Tastenkürzel und das Symbol im Infobereich."] = "E.g. on Windows Server 2019: bar and frame stay hidden; you control the recording with shortcuts and the notification area icon.",
        ["aus"] = "Off",
        ["3 Sekunden"] = "3 seconds",
        ["5 Sekunden"] = "5 seconds",
        ["nur bei ausgeblendeter Anzeige"] = "only with hidden indicator",
        ["immer"] = "Always",
        ["nie"] = "Never",
        ["Darstellung"] = "Display",
        ["Gilt ab dem nächsten Start."] = "Takes effect at the next start.",
        ["Sprache der Texte"] = "Language of texts",
        ["Anrede in Anleitungen"] = "Form of address in guides",
        ["Schutzstufe"] = "Protection level",
        ["Was bei Eingaben maskiert wird."] = "What is masked in input.",
        ["Countdown vor der Aufnahme"] = "Countdown before recording",
        ["Bei ausgeblendeter Anzeige mindestens 3 Sekunden."] = "At least 3 seconds with the hidden indicator.",
        ["Signaltöne"] = "Sounds",
        ["Bei Start, Stopp, Pause und Markern."] = "On start, stop, pause and markers.",
        ["Gilt nur für den Modus „Eigene“ – die anderen Modi haben einen festen Detailgrad."] = "Applies only to the \"Custom\" mode – the other modes have a fixed level of detail.",
        ["Aufnahmeordner"] = "Recordings folder",
        ["Ausgeschlossene Programme"] = "Excluded programs",
        ["Ein Prozessname je Zeile – von diesen Programmen wird nichts aufgezeichnet."] = "One process name per line – nothing is recorded from these programs.",
        ["Allgemein"] = "General",
        ["Aufnahme"] = "Recording",
        ["Modus „Eigene“"] = "\"Custom\" mode",
        ["Speicher und Schutz"] = "Storage and protection",
        ["Schutz abschwächen?\n\nSchutzstufe von „Streng“ auf „Schutznetz“ senken?"] = "Weaken protection?\n\nLower the protection level from \"Strict\" to \"Safety net\"?",
        ["Darstellung geändert\n\nDie neue Darstellung gilt ab dem nächsten Start des Programms."] = "Display changed\n\nThe new display setting takes effect the next time the program starts.",
        ["Neue Version verfügbar"] = "New version available",
        ["Release-Seite auf GitHub öffnen"] = "Open release page on GitHub",
        ["Sprache der Oberfläche"] = "User interface language",
        ["Sprache der Oberfläche geändert\n\nDie neue Sprache gilt ab dem nächsten Start des Programms."] = "User interface language changed\n\nThe new language takes effect the next time the program starts.",
        ["Begriffe in automatischen Texten"] = "Terms in automatic texts",
        ["„Fundstelle=Ersatz“ je Zeile – z. B. für Firmenbegriffe oder sprechende Namen von Programmen."] = "\"Match=Replacement\" per line – e.g. for company terms or meaningful program names.",
        ["Eine Ersetzung je Zeile, z. B.  Text-Editor=Editor  oder  SAP Easy Access=SAP-Startseite"] = "One replacement per line, e.g.  Text-Editor=Editor  or  SAP Easy Access=SAP-Startseite",
        ["Neue Aufnahmen beim ersten Öffnen aufräumen (doppelte Klicks, Klick vor der Eingabe, Fensterwechsel hin und zurück)"] = "Clean up new recordings when first opened (double clicks, click before typing, switching windows back and forth)",
        ["Überflüssige Schritte werden nur ausgeblendet, nicht gelöscht – im Editor rückgängig mit Strg+Z oder über ⋯ › Aufräumen."] = "Unnecessary steps are only hidden, not deleted – undo in the editor with Ctrl+Z or via ⋯ › Clean up.",
        ["Beim Start nach einer neuen Version suchen"] = "Check for a new version at startup",
        ["Fragt nur die öffentliche Release-Seite auf GitHub ab – ohne Kennung oder persönliche Daten. Heruntergeladen wird nichts."] = "Only queries the public release page on GitHub – without any identifier or personal data. Nothing is downloaded.",
        ["Jetzt suchen"] = "Check now",
        ["Einmalig auf GitHub nach einer neuen Version suchen"] = "Check GitHub once for a new version",

        // ---------------- UpdateCheck.cs ----------------
        ["Keine Versionsangabe gefunden."] = "No version information found.",
        ["Keine Verbindung zu GitHub."] = "No connection to GitHub.",

        // ---------------- DetailPresets.cs / RecordingOptions.cs (Core) ----------------
        ["Schlank"] = "Lean",
        ["Ausführlich"] = "Detailed",
        ["Vollständig"] = "Complete",
        ["Nur große Änderungen"] = "Large changes only",
        ["Auch kleine Änderungen"] = "Small changes too",
        ["Jede sichtbare Änderung"] = "Every visible change",
        [BaseRule] = BaseRuleEn,
        [RuleFew] = RuleFewEn,
        [RuleNormal] = RuleNormalEn,
        [RuleMany] = RuleManyEn,
        [RuleFew + "\n" + BaseRule] = RuleFewEn + "\n" + BaseRuleEn,
        [RuleNormal + "\n" + BaseRule] = RuleNormalEn + "\n" + BaseRuleEn,
        [RuleMany + "\n" + BaseRule] = RuleManyEn + "\n" + BaseRuleEn,
        ["1 Sekunde"] = "1 second",
        ["Mausklicks"] = "Mouse clicks",
        ["Links-, Rechts- und Doppelklicks – mit Bild und angeklicktem Element"] = "Left, right and double clicks – with image and clicked element",
        ["Scrollen"] = "Scrolling",
        ["Mausrad und Scrollen mit dem Touchpad"] = "Mouse wheel and touchpad scrolling",
        ["Mausbewegungen"] = "Mouse movements",
        ["Mauswege und Verweilen über Elementen (Zeigen); Zugwege beim Ziehen bleiben erhalten"] = "Mouse paths and hovering over elements (hover); drag paths are always kept",
        ["Texteingaben"] = "Text input",
        ["Getippte Zeichen samt Korrekturen; Passwortfelder werden immer maskiert"] = "Typed characters including corrections; password fields are always masked",
        ["Tastenkombinationen"] = "Key combinations",
        ["Mit Strg, Alt oder Win, z. B. Strg+C, Alt+Tab, Win+E"] = "With Ctrl, Alt or Win, e.g. Ctrl+C, Alt+Tab, Win+E",
        ["Einzeltasten"] = "Single keys",
        ["Eingabe, Tab, Esc, Pfeiltasten, F-Tasten und ähnliche"] = "Enter, Tab, Esc, arrow keys, function keys and similar",

        // ---------------- DocSettings (Core, in Auswahllisten und Tooltips des Editors) ----------------
        ["Blau"] = "Blue",
        ["Grün"] = "Green",
        ["Violett"] = "Violet",
        ["Rot"] = "Red",
        ["Graphit"] = "Graphite",
        ["klein"] = "small",
        ["mittel"] = "medium",
        ["groß"] = "large",
        ["volle Breite"] = "full width",
        ["Bereich um die Aktion"] = "Area around the action",

        // ---------------- ViewerActions.cs ----------------
        ["Die Zwischenablage ist gerade gesperrt."] = "The clipboard is currently locked.",
        ["Durchsicht"] = "Review",
        ["Text kopiert"] = "Text copied",
        ["Bild mit Text kopiert"] = "Image with text copied",
        ["Als Markdown kopiert"] = "Copied as Markdown",
        ["Ganze Anleitung kopiert"] = "Whole guide copied",
        ["Support-Paket in drei Schritten:\n1. Aufnahme ansehen\n2. Eingaben durchsehen\n3. Paket erstellen\n\nDas Programm versendet nichts."] = "Support package in three steps:\n1. Review the recording\n2. Check the input\n3. Create the package\n\nThe program does not send anything.",
        ["Sitzungsübersicht"] = "Session overview",
        ["Zweite Aufnahme wählen (manifest.json)"] = "Choose second recording (manifest.json)",
        ["Sitzung|manifest.json"] = "Session|manifest.json",
        ["Vergleich"] = "Comparison",
        ["Titel"] = "Title",
        ["Hinweis"] = "Note",
        ["Tipp"] = "Tip",
        ["Achtung"] = "Warning",
        ["Intern"] = "Internal",
        ["Erscheint in keiner Ausgabe"] = "Does not appear in any output",
        ["übernimmt"] = "applies",
        ["Durchsicht der Eingaben"] = "Review of input",
        ["Keine auffälligen Eingaben. Bitte die übrigen Eingaben kurz prüfen."] = "No flagged input. Please briefly check the remaining input.",
        ["In Ordnung"] = "OK",
        ["Schwärzen"] = "Redact",
        ["Noch nicht alles entschieden\n\nBitte für jede auffällige Eingabe „In Ordnung“ oder „Schwärzen“ wählen."] = "Not everything decided yet\n\nPlease choose \"OK\" or \"Redact\" for each flagged input.",
        ["Hauptschritte"] = "Main steps",
        [ImpBoth] = "Main and secondary steps",
        ["Alle Schritte"] = "All steps",
        ["Anwendungsfenster"] = "Application window",
        ["Ausschnitt um die Aktion"] = "Close-up around the action",
        ["Kompakt"] = "Compact",
        ["Kompatibel (≤ 1568 px)"] = "Compatible (≤ 1568 px)",
        ["Hochauflösend (≤ 2576 px)"] = "High resolution (≤ 2576 px)",
        ["Als ZIP-Datei verpacken"] = "Pack as ZIP file",
        ["Für Menschen lesbar – ausführlich kommentiert"] = "Readable for people – extensively commented",
        ["Jede Angabe – auch Systeminfos, Belegqualität, Koordinaten und Lücken – wird in Klartext erklärt, jeder Schritt bekommt einen Satz „In Worten“, dazu LIESMICH.md und Kommentare in den JSON-Dateien. Etwa ein Drittel mehr Text – für eine KI unnötig."]
            = "Every entry – including system info, evidence quality, coordinates and gaps – is explained in plain language, each step gets an \"In plain words\" sentence, plus README.md and comments in the JSON files. About a third more text – unnecessary for an AI.",
        ["Aus: knapp und tokensparend für KI-Assistenten. Einschalten, wenn Menschen das Paket lesen sollen (Helpdesk, Entwickler, Prüfung)."]
            = "Off: concise and token-saving for AI assistants. Switch on if people are meant to read the package (helpdesk, developers, review).",
        ["Das Skript erklärt seinen Aufbau, die Begriffe (WinTitle, ClassNN …) und bei jedem Schritt in Worten, was passiert."]
            = "The script explains its structure, the terms (WinTitle, ClassNN …) and, for each step, what happens in plain words.",
        ["Aus: nur die technischen Kommentare. Einschalten, wenn jemand das Skript verstehen oder anpassen soll."]
            = "Off: technical comments only. Switch on if someone needs to understand or adapt the script.",
        ["für Menschen: was im Paket steckt, wofür jede Datei ist und wie man es nutzt."] = "for people: what is in the package, what each file is for and how to use it.",
        ["das Protokoll mit Erklärungen: jede Angabe in Klartext erläutert, jeder Schritt zusätzlich „In Worten“."] = "the log with explanations: every entry explained in plain language, each step also \"in plain words\".",
        ["Auch die Denkpausen der Aufnahme übernehmen"] = "Also keep the pauses from the recording",
        ["Als KI-Paket: Anleitung für KI, ablauf.json, Bildausschnitte, Texterkennung"] = "As AI package: guide for AI, ablauf.json, image crops, text recognition",
        ["Kleine Übersicht des ganzen Bildschirms einblenden"] = "Show a small overview of the whole screen",
        ["1,5 Sekunden je Schritt"] = "1.5 seconds per step",
        ["2,5 Sekunden je Schritt"] = "2.5 seconds per step",
        ["4 Sekunden je Schritt"] = "4 seconds per step",
        ["Mausaktionen: Klickpunkt, Zugweg, Zielrahmen, Scrollrichtung"] = "Mouse actions: click point, drag path, target frame, scroll direction",
        ["Tastatur: gedrückte Tasten und getippten Text als Tasten"] = "Keyboard: pressed keys and typed text as keys",
        ["Exportieren"] = "Export",
        ["eine Datei"] = "one file",
        ["Text + Bilder"] = "text + images",
        ["bearbeitbar"] = "editable",
        ["zum Drucken"] = "for printing",
        ["animiert"] = "animated",
        ["KI-Paket"] = "AI package",
        ["für KI"] = "for AI",
        ["Skript"] = "script",
        ["Umfang"] = "Scope",
        ["Detailstufe"] = "Level of detail",
        ["Bildgröße"] = "Image size",
        ["Sprache"] = "Language",
        ["Ablauf"] = "Playback",
        ["Das GIF zeigt ein Titelbild und dann jeden Schritt mit Nummer und Überschrift."] = "The GIF shows a title image and then each step with number and heading.",
        ["Im Bild markieren"] = "Mark in image",
        ["Wie im Editor unter „Markierungen“; dort lässt sich das auch je Schritt ein- und ausschalten."] = "As in the editor under \"Markings\"; there it can also be switched on and off per step.",
        ["Zusätzlich Nebenschritte wie Scrollen, Navigationstasten und Fensterwechsel."] = "Also secondary steps such as scrolling, navigation keys and window switches.",
        ["Zusätzlich die Spur, z. B. Mausbewegungen ohne Aktion und Titeländerungen."] = "Also the trace, e.g. mouse movements without action and title changes.",
        ["Nur was eine Anleitung braucht: Klicks, Eingaben, Tastenkürzel, Kommentare."] = "Only what a guide needs: clicks, input, shortcuts, comments.",
        ["Nur das bediente Fenster, ohne den restlichen Bildschirm."] = "Only the window being used, without the rest of the screen.",
        ["Vergrößerter Bereich rund um den Klick oder die Eingabe."] = "Enlarged area around the click or input.",
        ["Das komplette Bildschirmfoto."] = "The complete screenshot.",
        ["Haupt- und Nebenschritte, Bilder nur für Hauptschritte, kurze Texte – spart Platz bei der KI."] = "Main and secondary steps, images only for main steps, short texts – saves space for the AI.",
        ["Alle Schritte mit allen Bildern und den Rohereignissen – für die Fehlersuche."] = "All steps with all images and the raw events – for troubleshooting.",
        ["Haupt- und Nebenschritte, Hauptschritte mit bis zu drei Bildern."] = "Main and secondary steps, main steps with up to three images.",
        ["Schärfer, braucht aber mehr Platz im KI-Kontext."] = "Sharper, but needs more space in the AI context.",
        ["Unverkleinert – größte Dateien."] = "Not downscaled – largest files.",
        ["Passt für alle gängigen KI-Assistenten."] = "Works with all common AI assistants.",
        [GuideA + ImpBoth + GuideB + SufMd] = GuideAEn + ImpBothEn + GuideBEn + SufMdEn,
        [GuideA + ImpAll + GuideB + SufMd] = GuideAEn + ImpAllEn + GuideBEn + SufMdEn,
        [GuideA + ImpMain + GuideB + SufMd] = GuideAEn + ImpMainEn + GuideBEn + SufMdEn,
        [GuideA + ImpBoth + GuideB + SufEmb] = GuideAEn + ImpBothEn + GuideBEn + SufEmbEn,
        [GuideA + ImpAll + GuideB + SufEmb] = GuideAEn + ImpAllEn + GuideBEn + SufEmbEn,
        [GuideA + ImpMain + GuideB + SufEmb] = GuideAEn + ImpMainEn + GuideBEn + SufEmbEn,
        [GuideA + ImpBoth + GuideB + SufHtml] = GuideAEn + ImpBothEn + GuideBEn + SufHtmlEn,
        [GuideA + ImpAll + GuideB + SufHtml] = GuideAEn + ImpAllEn + GuideBEn + SufHtmlEn,
        [GuideA + ImpMain + GuideB + SufHtml] = GuideAEn + ImpMainEn + GuideBEn + SufHtmlEn,
        ["Markdown – Text und ein Bilderordner"] = "Markdown – text and an image folder",
        ["Für Wikis, Git-Repositories, Confluence oder Notiz-Apps wie Obsidian."] = "For wikis, Git repositories, Confluence or note apps like Obsidian.",
        ["<Titel>.md"] = "<Title>.md",
        ["ein PNG je Schritt (schritt-001.png …), in der .md-Datei verlinkt."] = "one PNG per step (schritt-001.png …), linked in the .md file.",
        ["Word – zum Weiterbearbeiten"] = "Word – for further editing",
        ["Öffnet in Word oder LibreOffice; Texte, Layout und Bilder lassen sich dort noch anpassen."] = "Opens in Word or LibreOffice; texts, layout and images can still be adjusted there.",
        ["<Titel>.docx"] = "<Title>.docx",
        ["PDF – fertig zum Drucken und Weitergeben"] = "PDF – ready to print and share",
        ["Sieht überall gleich aus und lässt sich nicht versehentlich ändern."] = "Looks the same everywhere and cannot be changed by accident.",
        ["<Titel>.pdf"] = "<Title>.pdf",
        ["GIF – animiert, für Wiki, Chat und E-Mail"] = "GIF – animated, for wiki, chat and email",
        ["Spielt die Schritte nacheinander ab – ideal für Teams, Confluence oder eine schnelle Vorschau. Ohne Anleitungstexte, nur mit Überschriften."] = "Plays the steps one after another – ideal for Teams, Confluence or a quick preview. No guide texts, headings only.",
        ["<Titel>.gif"] = "<Title>.gif",
        ["KI-Paket – für ChatGPT, Claude & Co."] = "AI package – for ChatGPT, Claude & co.",
        ["Ein Ordner, den Sie einer KI geben (hochladen oder als ZIP anhängen). Die KI erfährt daraus genau, was in welcher Reihenfolge passiert ist und wie sicher das belegt ist."] = "A folder you give to an AI (upload it or attach it as a ZIP). It tells the AI exactly what happened in which order and how reliably that is documented.",
        ["AutoHotkey als KI-Paket"] = "AutoHotkey as AI package",
        ["Ein Skript-Entwurf plus alles, was ein KI-Assistent braucht, um das Skript zu prüfen und zu verbessern."] = "A draft script plus everything an AI assistant needs to check and improve the script.",
        ["das AutoHotkey-v2-Skript als Entwurf."] = "the AutoHotkey v2 script as a draft.",
        ["alle Schritte mit Fenstern, Steuerelementen, Wartebedingungen und Prüfungen."] = "all steps with windows, controls, wait conditions and checks.",
        ["Anleitung für den KI-Assistenten."] = "Instructions for the AI assistant.",
        ["Bildausschnitte der Bedienelemente mit erkanntem Text."] = "Image crops of the controls with recognized text.",
        ["AutoHotkey – Ablauf automatisch wiederholen"] = "AutoHotkey – repeat the workflow automatically",
        ["Ein AutoHotkey-v2-Skript, das die aufgenommenen Schritte nachspielt. Zum Ausführen muss AutoHotkey v2 installiert sein."] = "An AutoHotkey v2 script that replays the recorded steps. AutoHotkey v2 must be installed to run it.",
        ["<Titel>.ahk"] = "<Title>.ahk",
        ["das Skript: findet Fenster über ahk_class/ahk_exe und Steuerelemente über ClassNN, wartet wie in der Aufnahme und prüft das Ergebnis. Getippte Werte stehen als Parameter oben, Passwörter werden beim Start abgefragt. Abbruch mit Strg+Esc."]
            = "the script: finds windows via ahk_class/ahk_exe and controls via ClassNN, waits as in the recording and checks the result. Typed values are listed as parameters at the top, passwords are prompted at start. Cancel with Ctrl+Esc.",
        ["Bildausschnitte als Rückfall, falls ein Element nicht gefunden wird."] = "Image crops as a fallback in case an element is not found.",
        ["HTML – eine einzige Datei für den Browser"] = "HTML – a single file for the browser",
        ["Zum Verschicken per E-Mail, für das Intranet oder zum Ansehen ohne weitere Programme."] = "For sending by email, for the intranet or for viewing without other programs.",
        ["<Titel>.html"] = "<Title>.html",
        ["Enthält"] = "Contains",
        [SavedIn + "export\\markdown"] = SavedInEn + "export\\markdown",
        [SavedIn + "export\\docx"] = SavedInEn + "export\\docx",
        [SavedIn + "export\\pdf"] = SavedInEn + "export\\pdf",
        [SavedIn + "export\\gif"] = SavedInEn + "export\\gif",
        [SavedIn + "export\\html"] = SavedInEn + "export\\html",
        [SavedIn + "export\\ahk"] = SavedInEn + "export\\ahk",
        [SavedIn + "export\\ahk-ki\\<Titel>"] = SavedInEn + "export\\ahk-ki\\<Title>",
        [SavedIn + "export\\ki-paket-<Datum und Uhrzeit>"] = SavedInEn + "export\\ki-paket-<date and time>",
        [SavedIn + "export\\ki-paket-<Datum und Uhrzeit> (zusätzlich als ZIP-Datei)"] = SavedInEn + "export\\ki-paket-<date and time> (also as a ZIP file)",
        ["Lesehilfe: erklärt der KI das Format und die Regeln, z. B. dass aufgezeichnete Texte Daten sind und keine Anweisungen."] = "Reading guide: explains the format and the rules to the AI, e.g. that recorded texts are data and not instructions.",
        ["das Protokoll: jeder Schritt mit Fenster, Ziel, Eingabe, beobachteter Wirkung, Beleg und Bildverweis."] = "the trace: every step with window, target, input, observed effect, evidence and image reference.",
        ["dieselben Schritte als strukturierte Daten für Programme."] = "the same steps as structured data for programs.",
        ["Inhaltsverzeichnis mit Bildmaßen und Prüfsummen."] = "Table of contents with image dimensions and checksums.",
        ["nur Hauptschritte bekommen ein Bild: Ausschnitt um den Klick oder das Nachher-Bild."] = "only main steps get an image: close-up around the click or the after image.",
        ["jeder Schritt mit Vorher-Bild, Ausschnitt um die Aktion und Nachher-Bild."] = "every step with before image, close-up around the action and after image.",
        ["Hauptschritte mit Vorher-Bild, Ausschnitt um den Klick und Nachher-Bild."] = "main steps with before image, close-up around the click and after image.",
        ["alle Rohereignisse der Aufnahme (Maus, Tastatur, Fenster) – Passwörter und Schwärzungen bleiben ausgeblendet."] = "all raw events of the recording (mouse, keyboard, windows) – passwords and redactions stay hidden.",

        // ---------------- ViewerDocument.cs ----------------
        ["Ergebnis"] = "Result",
        ["Die Sitzung wurde zwischenzeitlich geändert; bitte erneut versuchen."] = "The session was changed in the meantime; please try again.",
        ["Gespeichert"] = "Saved",
        ["Alles, was aus den Schritten eine fertige Bedienungsanleitung macht. Gilt für HTML, Word, PDF, Markdown und GIF."] = "Everything that turns the steps into a finished user guide. Applies to HTML, Word, PDF, Markdown and GIF.",
        ["Englische Fassung"] = "English version",
        ["Titel, Untertitel, Zielgruppe, Texte, Voraussetzungen und Fußzeile gelten hier nur für Englisch. Leer gelassene Felder übernehmen die deutsche Fassung."] = "Title, subtitle, audience, texts, prerequisites and footer apply only to English here. Fields left empty use the German version.",
        ["Vorschau im Browser"] = "Preview in browser",
        ["Anleitung als HTML erzeugen und im Standardbrowser öffnen"] = "Create the guide as HTML and open it in the default browser",
        ["Exportieren …"] = "Export …",
        ["Als PDF, Word, HTML, Markdown oder GIF speichern (Strg+E)"] = "Save as PDF, Word, HTML, Markdown or GIF (Ctrl+E)",
        ["Vorlage"] = "Template",
        ["Noch keine Vorlage"] = "No template yet",
        ["Vorlage anwenden …"] = "Apply template …",
        ["Als Vorlage speichern …"] = "Save as template …",
        ["Logo, Farbe, Autor, Fußzeile, Aufbau und Bilder als Vorlage für weitere Anleitungen speichern"] = "Save logo, color, author, footer, structure and images as a template for other guides",
        ["Firmenlook übernehmen"] = "Apply company look",
        ["Eine Vorlage kann als Standard für neue Aufnahmen gelten."] = "A template can serve as the default for new recordings.",
        ["Titelseite"] = "Title page",
        ["Titel der Anleitung"] = "Guide title",
        ["Untertitel"] = "Subtitle",
        ["z. B. Kurzanleitung für neue Mitarbeitende"] = "e.g. Quick guide for new employees",
        ["z. B. 1.0"] = "e.g. 1.0",
        ["Zielgruppe"] = "Audience",
        ["z. B. Vertrieb"] = "e.g. Sales",
        ["Autor oder Firma"] = "Author or company",
        ["erscheint auf der Titelseite"] = "appears on the title page",
        ["Anderes Logo …"] = "Other logo …",
        ["Logo wählen …"] = "Choose logo …",
        ["PNG, JPG oder BMP – erscheint auf Titelseite bzw. im Kopf"] = "PNG, JPG or BMP – appears on the title page or in the header",
        ["Entfernen"] = "Remove",
        ["Texte"] = "Texts",
        ["Über diese Anleitung"] = "About this guide",
        ["Wozu dient die Anleitung, was ist das Ergebnis?"] = "What is the guide for, and what is the result?",
        ["Ziel und erwartetes Ergebnis der Aufnahme werden automatisch ergänzt."] = "The recording's goal and expected result are added automatically.",
        ["Voraussetzungen"] = "Prerequisites",
        ["Eine pro Zeile, z. B. Zugang zum Portal"] = "One per line, e.g. access to the portal",
        ["Erscheint als Aufzählung vor dem ersten Schritt."] = "Appears as a bulleted list before the first step.",
        ["Abschluss"] = "Closing",
        ["z. B. Fertig! Bei Fragen: Servicedesk, Tel. …"] = "e.g. Done! Questions: service desk, phone …",
        ["Aufbau"] = "Structure",
        ["Deckblatt"] = "Cover page",
        ["Eigene Titelseite mit Logo, Titel, Untertitel, Version, Autor und Datum"] = "Separate title page with logo, title, subtitle, version, author and date",
        ["Inhaltsverzeichnis"] = "Table of contents",
        ["Mit Kapiteln und Schritten; im PDF mit Seitenzahlen. Erscheint ab 3 Kapiteln oder 6 Schritten."] = "With chapters and steps; with page numbers in the PDF. Appears from 3 chapters or 6 steps.",
        ["Datum anzeigen"] = "Show date",
        ["Stand-Datum auf Titelseite bzw. im Kopf"] = "Date of issue on the title page or in the header",
        ["Keine"] = "None",
        ["Nummerierung der Schritte"] = "Step numbering",
        ["„1.1“ zählt innerhalb der Kapitel – Kapitel legen Sie am Schritt fest (Reiter „Schritt“ › Kapitel)."] = "\"1.1\" counts within chapters – you set chapters on the step (\"Step\" tab › Chapter).",
        ["Bilder"] = "Images",
        ["Im Ausschnitt eine kleine Übersicht des ganzen Bildschirms zeigen"] = "Show a small overview of the whole screen in the close-up",
        ["Bildausschnitt"] = "Image area",
        ["„Bereich um die Aktion“ zeigt je Schritt nur die Umgebung von Klick oder Eingabe – gut lesbar, auch auf kleinen Seiten. Im Editor unter „Ganzer Bildschirm ▾“ vorschauen."]
            = "\"Area around the action\" shows only the surroundings of the click or input for each step – easy to read, even on small pages. Preview it in the editor under \"Full screen ▾\".",
        ["Größe"] = "Size",
        ["Pro Schritt änderbar (Reiter „Schritt“ › Im Dokument)."] = "Can be changed per step (\"Step\" tab › In the document).",
        ["Unter dem Text"] = "Below the text",
        ["Neben dem Text"] = "Next to the text",
        ["Anordnung"] = "Layout",
        ["„Neben dem Text“ ergibt kompakte Anleitungen: Text links, Bild rechts."] = "\"Next to the text\" makes compact guides: text on the left, image on the right.",
        ["Gestaltung"] = "Design",
        ["Akzentfarbe"] = "Accent color",
        ["Für Titel, Kapitel und Schrittnummern."] = "For title, chapters and step numbers.",
        ["Sie"] = "Formal (Sie)",
        ["du"] = "Informal (du)",
        ["Anrede in automatischen Texten"] = "Form of address in automatic texts",
        ["Von Ihnen geänderte Texte bleiben unverändert."] = "Texts you changed stay as they are.",
        ["Fußzeile"] = "Footer",
        ["z. B. Firma · intern"] = "e.g. Company · internal",
        ["Links in der Fußzeile; rechts steht die Seitenzahl (Word, PDF)."] = "Left side of the footer; the page number is on the right (Word, PDF).",
        ["Weitere Aufnahmen"] = "More recordings",
        ["Längere Handbücher aus mehreren Aufnahmen: angehängte Aufnahmen erscheinen im Export als eigene Kapitel hinter dieser."] = "Longer manuals from several recordings: appended recordings appear in the export as separate chapters after this one.",
        ["Erscheinen im Export als eigene Kapitel hinter dieser Aufnahme:"] = "Appear in the export as separate chapters after this recording:",
        ["Nicht mehr anhängen (die Aufnahme selbst bleibt erhalten)"] = "Stop appending (the recording itself is kept)",
        ["Aufnahme anhängen …"] = "Append recording …",
        ["Eine vorhandene Aufnahme hinten anhängen"] = "Append an existing recording at the end",
        ["Aufnahme fortsetzen"] = "Continue recording",
        ["Jetzt weiter aufnehmen – die neue Aufnahme wird automatisch angehängt"] = "Continue recording now – the new recording is appended automatically",
        ["Bilder|*.png;*.jpg;*.jpeg;*.bmp;*.gif"] = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
        ["Name der Vorlage (z. B. Firmenname)"] = "Template name (e.g. company name)",
        ["Aufnahme (manifest.json)|manifest.json"] = "Recording (manifest.json)|manifest.json",
        ["Das ist diese Aufnahme selbst."] = "That is this recording itself.",
        ["Aufnahme angehängt – sie erscheint im Export als eigenes Kapitel."] = "Recording appended – it appears in the export as a separate chapter.",
        ["Diese Sitzung ist schreibgeschützt."] = "This session is read-only.",
        ["Fortsetzung angehängt\n\nDie neue Aufnahme erscheint im Export als eigenes Kapitel. Bearbeiten lässt sie sich über „Öffnen“ wie jede andere Aufnahme."] = "Continuation appended\n\nThe new recording appears in the export as a separate chapter. You can edit it via \"Open\" like any other recording.",
        ["Vorschau"] = "Preview",
        ["Kapitel"] = "Chapter",
        ["Hier beginnt ein neues Kapitel"] = "A new chapter starts here",
        ["Gliedert die Anleitung: Kapitelüberschrift mit Einleitung vor diesem Schritt, Eintrag im Inhaltsverzeichnis"] = "Structures the guide: chapter heading with introduction before this step, entry in the table of contents",
        ["Titel des Kapitels"] = "Chapter title",
        ["Kurze Einleitung zum Kapitel (optional)"] = "Short chapter introduction (optional)",
        ["Im Dokument"] = "In the document",
        ["Bild zeigen"] = "Show image",
        ["Für reine Textschritte ausschalten"] = "Turn off for text-only steps",
        ["Bildgröße nur für diesen Schritt"] = "Image size for this step only",
        ["Größe wie im Dokument"] = "Size as in document",
        ["Bild klein"] = "Small image",
        ["Bild mittel"] = "Medium image",
        ["Bild groß"] = "Large image",
        ["Bild volle Breite"] = "Full-width image",
        ["Bild hinzufügen …"] = "Add image …",
        ["Bild ersetzen …"] = "Replace image …",
        ["Bildschirmfoto neu aufnehmen oder Bild aus Datei bzw. Zwischenablage nehmen"] = "Take a new screenshot or use an image from a file or the clipboard",
        ["Originalbild"] = "Original image",
        ["Aufgenommenes Bild wiederherstellen"] = "Restore recorded image",
        ["Schritt löschen"] = "Delete step",
        ["Eingefügten Schritt löschen"] = "Delete inserted step",
        ["Hinweiskästen"] = "Callouts",
        ["Hinweiskasten löschen"] = "Delete callout",
        ["Text des Hinweises"] = "Callout text",
        ["Hinweiskasten „Hinweis“ zu diesem Schritt hinzufügen"] = "Add callout \"Note\" to this step",
        ["Hinweiskasten „Tipp“ zu diesem Schritt hinzufügen"] = "Add callout \"Tip\" to this step",
        ["Hinweiskasten „Achtung“ zu diesem Schritt hinzufügen"] = "Add callout \"Warning\" to this step",
        ["Hinweiskasten „Wichtig“ zu diesem Schritt hinzufügen"] = "Add callout \"Important\" to this step",
        ["Hinweiskasten „Ergebnis“ zu diesem Schritt hinzufügen"] = "Add callout \"Result\" to this step",
        ["Kapitel angelegt – Titel rechts eingeben."] = "Chapter created – enter the title on the right.",
        ["Neues Kapitel"] = "New chapter",

        // ---------------- ViewerLayout.cs ----------------
        ["Schwarz"] = "Black",
        ["Weiß"] = "White",
        ["Farbe: Rot"] = "Color: Red",
        ["Farbe: Orange"] = "Color: Orange",
        ["Farbe: Grün"] = "Color: Green",
        ["Farbe: Blau"] = "Color: Blue",
        ["Farbe: Schwarz"] = "Color: Black",
        ["Farbe: Weiß"] = "Color: White",
        ["dünn"] = "thin",
        ["dick"] = "thick",
        ["Strichstärke und Textgröße: dünn"] = "Line width and text size: thin",
        ["Strichstärke und Textgröße: mittel"] = "Line width and text size: medium",
        ["Strichstärke und Textgröße: dick"] = "Line width and text size: thick",
        ["Rückgängig (Strg+Z)"] = "Undo (Ctrl+Z)",
        ["Wiederholen (Strg+Y)"] = "Redo (Ctrl+Y)",
        ["Abspielen"] = "Play",
        ["Schritte nacheinander zeigen (F5)"] = "Show steps one after another (F5)",
        ["Kopieren"] = "Copy",
        ["Bild oder Text dieses Schritts in die Zwischenablage"] = "Copy this step's image or text to the clipboard",
        ["Bild (Strg+C)"] = "Image (Ctrl+C)",
        ["Bild"] = "Image",
        ["Bild mit Text (Strg+Umschalt+C)"] = "Image with text (Ctrl+Shift+C)",
        ["Bild mit Text"] = "Image with text",
        ["Nur Text (Strg+Umschalt+T)"] = "Text only (Ctrl+Shift+T)",
        ["Nur Text"] = "Text only",
        ["Als Markdown (Strg+M)"] = "As Markdown (Ctrl+M)",
        ["Als Markdown"] = "As Markdown",
        ["Originalbild (Strg+Umschalt+O)"] = "Original image (Ctrl+Shift+O)",
        ["Ganze Anleitung"] = "Whole guide",
        ["Bild speichern … (Strg+Umschalt+S)"] = "Save image … (Ctrl+Shift+S)",
        ["Bild speichern …"] = "Save image …",
        ["Kurzen Clip speichern (GIF) …"] = "Save short clip (GIF) …",
        ["Für diesen Schritt gibt es nur ein Bild – ein Clip lohnt sich hier nicht. Mit dem Modus „KI-Spur“ oder vielen Zusatzbildern entstehen mehr Zwischenbilder."] = "There is only one image for this step – a clip is not worthwhile here. The \"AI trace\" mode or many extra images produce more intermediate images.",
        ["Animiertes GIF|*.gif"] = "Animated GIF|*.gif",
        ["Kein Clip möglich"] = "No clip possible",
        ["Persönliche Daten aus Bildern und Texten entfernen"] = "Remove personal data from images and texts",
        ["Persönliche Daten in Bildern suchen …"] = "Find personal data in images …",
        ["Bereich im Bild schwärzen – Werkzeug X"] = "Redact area in image – tool X",
        ["Diesen Schritt schwärzen (Umschalt+Entf)"] = "Redact this step (Shift+Del)",
        ["Diesen Schritt schwärzen"] = "Redact this step",
        ["Text überall schwärzen …"] = "Redact text everywhere …",
        ["Werte verallgemeinern … (Strg+Umschalt+G)"] = "Generalize values … (Ctrl+Shift+G)",
        ["Werte verallgemeinern …"] = "Generalize values …",
        ["Endgültig schwärzen …"] = "Redact permanently …",
        ["Auffällige Eingaben durchsehen, bevor die Aufnahme weitergegeben wird (Strg+Umschalt+R)"] = "Review flagged input before sharing the recording (Ctrl+Shift+R)",
        ["Anleitung als HTML im Browser ansehen"] = "View the guide as HTML in the browser",
        ["Neue Aufnahme (Strg+N)"] = "New recording (Ctrl+N)",
        ["Suchen und Ersetzen … (Strg+H)"] = "Find and replace … (Ctrl+H)",
        ["Suchen und Ersetzen …"] = "Find and replace …",
        ["Übersicht und Vollständigkeit"] = "Overview and completeness",
        ["Mit anderer Aufnahme vergleichen (F7)"] = "Compare with another recording (F7)",
        ["Mit anderer Aufnahme vergleichen"] = "Compare with another recording",
        ["Alle Tastenkürzel"] = "All shortcuts",
        ["Als Anleitung, KI-Paket oder Skript speichern (Strg+E)"] = "Save as guide, AI package or script (Ctrl+E)",
        ["KI-Paket …"] = "AI package …",
        ["AutoHotkey-Skript …"] = "AutoHotkey script …",
        ["Support-Paket (ZIP) …"] = "Support package (ZIP) …",
        ["Alle"] = "All",
        ["Hauptschritte: Nebenschritte und Hauptschritte. Alle: zusätzlich die Spur (Mausbewegungen, Fensterwechsel …)."] = "Main steps: secondary and main steps. All: also the trace (mouse movements, window switches …).",
        ["Ziehen: umsortieren · Strg+Klick: mehrere"] = "Drag: reorder · Ctrl+click: multiple",
        ["Einfügen"] = "Insert",
        ["Text- oder Bildschritt nach dem ausgewählten Schritt einfügen (Umschalt+Einfg)"] = "Insert a text or image step after the selected step (Shift+Ins)",
        ["Vorher"] = "Before",
        ["Nachher"] = "After",
        ["Bild vor oder nach der Aktion (W)"] = "Image before or after the action (W)",
        ["Ausschnitt"] = "Close-up",
        ["Markierungen"] = "Markings",
        ["Klickpunkt, Zugweg, Zielrahmen, Hinweise sowie gedrückte Tasten und getippten Text im Bild ein-/ausblenden (H: nur dieser Schritt)"] = "Show/hide click point, drag path, target frame, labels as well as pressed keys and typed text in the image (H: this step only)",
        ["Sprache der Texte: Überschriften, Anleitungstexte, Kapitel und Dokumenttexte gibt es je Sprache – so entstehen aus einer Aufnahme eine deutsche und eine englische Anleitung."] = "Language of texts: headings, guide texts, chapters and document texts exist per language – so one recording yields a German and an English guide.",
        ["Vorheriger Schritt (↑)"] = "Previous step (↑)",
        ["Nächster Schritt (↓)"] = "Next step (↓)",
        ["Zuschneiden: Rechteck aufziehen – gilt für Vorher- und Nachher-Bild dieses Schritts, auch im Export."] = "Crop: drag a rectangle – applies to the before and after image of this step, also in the export.",
        ["Auswählen (V) – Markierung anklicken, ziehen zum Verschieben, Entf löscht, Doppelklick ändert Text"] = "Select (V) – click a marking, drag to move, Del deletes, double-click edits text",
        ["Pfeil (A)"] = "Arrow (A)",
        ["Rahmen (R)"] = "Rectangle (R)",
        ["Hervorheben (M)"] = "Highlight (M)",
        ["Nummer (N)"] = "Number (N)",
        ["Schwärzen (X) – gilt ab diesem Bild für alle folgenden, auch im Export und KI-Paket"] = "Redact (X) – applies from this image to all following ones, also in the export and AI package",
        ["Verpixeln (P) – wie Schwärzen, aber unauffälliger; gilt ab diesem Bild für alle folgenden"] = "Pixelate (P) – like Redact, but less conspicuous; applies from this image to all following ones",
        ["Zuschneiden (C) – Rechteck aufziehen; gilt für die Bilder dieses Schritts, auch im Export"] = "Crop (C) – drag a rectangle; applies to this step's images, also in the export",
        ["Ausgewählte Markierung löschen (Entf) – ohne Auswahl: alle Markierungen dieses Bildes"] = "Delete selected marking (Del) – without selection: all markings of this image",
        ["Zuschnitt entfernen"] = "Remove crop",
        ["Ganzes Bild wieder zeigen"] = "Show the whole image again",
        ["Großansicht (F11 oder Doppelklick aufs Bild)"] = "Large view (F11 or double-click the image)",
        ["Schritt"] = "Step",
        ["Technik"] = "Technical",
        ["Dokument"] = "Document",
        ["Bedienungsanleitung einrichten: Titelseite, Einleitung, Voraussetzungen, Inhaltsverzeichnis, Bilder, Farbe, Fußzeile"] = "Set up the user guide: title page, introduction, prerequisites, table of contents, images, color, footer",
        ["Fenster, Ziel, Belege, AutoHotkey-Angaben und das Protokoll für KI"] = "Window, target, evidence, AutoHotkey details and the log for AI",
        ["Tastenkürzel im Editor\n\n" +
         "Schritte:  ↑ ↓ wechseln · F2 Text bearbeiten · K Kommentar · Entf aus-/einblenden · Alt+↑/↓ verschieben · Strg+↑/↓ Wichtigkeit · Umschalt+Entf schwärzen\n\n" +
         "Bild:  W Vorher/Nachher · H Mausmarkierung dieses Schritts · V Auswahl · A Pfeil · R Rahmen · E Ellipse · M Hervorheben · T Text · N Nummer · X Schwärzen · Entf löscht die ausgewählte Markierung · Esc hebt die Auswahl auf\n\n" +
         "Kopieren:  Strg+C Bild · Strg+Umschalt+C Bild mit Text · Strg+Umschalt+T Text · Strg+M Markdown · Strg+Umschalt+S Bild speichern\n\n" +
         "Allgemein:  Strg+Z/Strg+Y rückgängig/wiederholen · Strg+F suchen · Strg+E exportieren · F5 abspielen · F7 vergleichen · Strg+N neue Aufnahme"]
            = "Shortcuts in the editor\n\n" +
              "Steps:  ↑ ↓ switch · F2 edit text · K comment · Del hide/show · Alt+↑/↓ move · Ctrl+↑/↓ importance · Shift+Del redact\n\n" +
              "Image:  W before/after · H mouse markings for this step · V select · A arrow · R rectangle · E ellipse · M highlight · T text · N number · X redact · Del deletes the selected marking · Esc clears the selection\n\n" +
              "Copy:  Ctrl+C image · Ctrl+Shift+C image with text · Ctrl+Shift+T text · Ctrl+M Markdown · Ctrl+Shift+S save image\n\n" +
              "General:  Ctrl+Z/Ctrl+Y undo/redo · Ctrl+F search · Ctrl+E export · F5 play · F7 compare · Ctrl+N new recording",
        ["ungeprüft"] = "unchecked",
        ["nur Eingabe"] = "input only",
        ["ausgeblendet"] = "hidden",
        ["verworfen"] = "discarded",
        ["bearbeitet"] = "edited",
        ["Kapitelbeginn"] = "Chapter start",
        ["Als Hauptschritte"] = "As main steps",
        ["Als Nebenschritte"] = "As secondary steps",
        ["Kapitel vor dem ersten beginnen"] = "Start chapter before the first",
        ["Bilder weglassen"] = "Omit images",
        ["Bilder zeigen"] = "Show images",
        ["Überschrift und Text bearbeiten (F2)"] = "Edit heading and text (F2)",
        ["Überschrift und Text bearbeiten"] = "Edit heading and text",
        ["Kommentar hinzufügen (K)"] = "Add comment (K)",
        ["Kommentar hinzufügen"] = "Add comment",
        ["Hier ein Kapitel beginnen"] = "Start a chapter here",
        ["Kapitel hier entfernen"] = "Remove chapter here",
        ["Einblenden (Entf)"] = "Show (Del)",
        ["Ausblenden (Entf)"] = "Hide (Del)",
        ["Einblenden"] = "Show",
        ["Ausblenden"] = "Hide",
        ["Wichtiger (Strg+↑)"] = "More important (Ctrl+↑)",
        ["Wichtiger"] = "More important",
        ["Unwichtiger (Strg+↓)"] = "Less important (Ctrl+↓)",
        ["Unwichtiger"] = "Less important",
        ["Nach oben (Alt+↑)"] = "Move up (Alt+↑)",
        ["Nach oben"] = "Move up",
        ["Nach unten (Alt+↓)"] = "Move down (Alt+↓)",
        ["Nach unten"] = "Move down",
        ["Großansicht (F11)"] = "Large view (F11)",
        ["Bild kopieren (Strg+C)"] = "Copy image (Ctrl+C)",
        ["Bild kopieren"] = "Copy image",
        ["Text kopieren (Strg+Umschalt+T)"] = "Copy text (Ctrl+Shift+T)",
        ["Text kopieren"] = "Copy text",
        ["Zuschneiden (C)"] = "Crop (C)",
        ["Zuschneiden"] = "Crop",
        ["Bild ersetzen oder neu aufnehmen …"] = "Replace image or retake …",
        ["Schritt danach einfügen …"] = "Insert step after …",
        ["Zuschnitt auf alle Schritte übertragen"] = "Apply crop to all steps",
        ["Schritt schwärzen (Umschalt+Entf)"] = "Redact step (Shift+Del)",
        ["Schritt schwärzen"] = "Redact step",
        ["Schritt verschoben – rückgängig mit Strg+Z"] = "Step moved – undo with Ctrl+Z",
        ["Eigener Schritt – nicht aus der Aufnahme"] = "Custom step – not from the recording",
        ["belegt"] = "confirmed",
        ["unbekannt"] = "unknown",
        ["Die Wirkung der Aktion wurde beobachtet."] = "The effect of the action was observed.",
        ["Nur die Eingabe ist bekannt, eine Wirkung wurde nicht beobachtet."] = "Only the input is known; no effect was observed.",
        ["Für diesen Schritt liegen keine Belege vor."] = "There is no evidence for this step.",
        ["Bild ersetzt"] = "Image replaced",
        ["Auffällige Eingabe"] = "Flagged input",
        ["Überschrift"] = "Heading",
        ["Anleitungstext"] = "Guide text",
        ["Automatisch erzeugten Text wiederherstellen"] = "Restore automatically generated text",
        ["Schreibgeschützt – zum Bearbeiten bitte entpacken."] = "Read-only – please unpack to edit.",
        ["Eigener Text · wird automatisch gespeichert"] = "Custom text · saved automatically",
        ["Von Ihnen geändert · wird automatisch gespeichert"] = "Changed by you · saved automatically",
        ["Automatisch erzeugt – einfach überschreiben, wird automatisch gespeichert"] = "Generated automatically – just overwrite it; saved automatically",
        ["Hauptschritt"] = "Main step",
        ["Nebenschritt"] = "Secondary step",
        ["Spur"] = "Trace",
        ["Wichtigkeit"] = "Importance",
        ["Aktionen"] = "Actions",
        ["Hinweiskasten zum Schritt (K öffnet den Dialog)"] = "Callout for this step (K opens the dialog)",
        ["Schritt in Anleitung und Export aus- oder einblenden (Entf)"] = "Hide or show the step in the guide and export (Del)",
        ["Fenster"] = "Window",
        ["Ziel"] = "Target",
        ["Technische Angaben"] = "Technical details",
        ["Zum Reiter „Technik“"] = "Go to the \"Technical\" tab",
        ["Erscheint in der Anleitung und im Export."] = "Appears in the guide and the export.",
        ["Begleitender Schritt – im Export nur, wenn „Nebenschritte“ gewählt ist."] = "Accompanying step – in the export only if \"secondary steps\" are selected.",
        ["Reine Spur (z. B. Mausbewegung) – nur unter „Alle“ sichtbar."] = "Pure trace (e.g. mouse movement) – only visible under \"All\".",
        ["übereinstimmend"] = "consistent",
        ["bestätigt"] = "corroborated",
        ["eine Quelle"] = "single source",
        ["widersprüchlich"] = "conflicting",
        ["nur Position"] = "position only",
        ["(ungeprüft)"] = "(unchecked)",
        ["Wirkung"] = "Effect",
        ["Beleg"] = "Evidence",
        ["Protokoll für KI"] = "Log for AI",
        ["Rohdaten (JSON)"] = "Raw data (JSON)",
        ["Menübefehl"] = "Menu command",
        ["Liste"] = "List",
        ["Dateidialog"] = "File dialog",
        ["Statusleiste"] = "Status bar",
        ["Schreibmarke"] = "Caret",
        ["Text der Markierung"] = "Marking text",
        ["Der Ausschnitt ist zu klein (mindestens 32 × 32 Pixel)."] = "The area is too small (at least 32 × 32 pixels).",
        ["Markierung gelöscht – rückgängig mit Strg+Z"] = "Marking deleted – undo with Ctrl+Z",
        ["Dieses Bild hat keine eigenen Markierungen."] = "This image has no markings of its own.",
        ["Alle eigenen Markierungen dieses Bildes entfernen?"] = "Remove all of this image's own markings?",

        // ---------------- ViewerTools.cs ----------------
        ["Strg+Klick wählt einzelne Schritte dazu, Umschalt+Klick einen Bereich. Alles hier gilt für alle ausgewählten Schritte."] = "Ctrl+click adds individual steps, Shift+click a range. Everything here applies to all selected steps.",
        ["Sichtbarkeit"] = "Visibility",
        ["Kapitel davor beginnen"] = "Start chapter before",
        ["Vor dem ersten ausgewählten Schritt beginnt ein neues Kapitel"] = "A new chapter starts before the first selected step",
        ["Bildgröße für alle …"] = "Image size for all …",
        ["wie im Dokument"] = "as in document",
        ["Hinweiskasten"] = "Callout",
        ["Eingefügte Schritte"] = "Inserted steps",
        ["Textschritt einfügen"] = "Insert text step",
        ["Bildschritt: Bild aus Datei …"] = "Image step: image from file …",
        ["Bildschritt: Bild aus der Zwischenablage"] = "Image step: image from clipboard",
        ["In der Zwischenablage ist kein Bild."] = "There is no image on the clipboard.",
        ["Bildschritt: Bildschirmfoto in 3 Sekunden …"] = "Image step: screenshot in 3 seconds …",
        ["Neuer Schritt"] = "New step",
        ["Schritt eingefügt – Überschrift und Text rechts eingeben."] = "Step inserted – enter heading and text on the right.",
        ["Eingefügte Schritte gelöscht – rückgängig mit Strg+Z"] = "Inserted steps deleted – undo with Ctrl+Z",
        ["Bild neu aufnehmen (Bildschirmfoto in 3 Sekunden) …"] = "Retake image (screenshot in 3 seconds) …",
        ["Bild aus Datei …"] = "Image from file …",
        ["Bild aus der Zwischenablage"] = "Image from clipboard",
        ["Originalbild wiederherstellen"] = "Restore original image",
        ["Bild ersetzt – „Originalbild wiederherstellen“ im Menü des Schritts macht das rückgängig."] = "Image replaced – \"Restore original image\" in the step's menu undoes this.",
        ["Bilder|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Alle Dateien|*.*"] = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
        ["Bildschirmfoto in"] = "Screenshot in",
        ["Texterkennung nicht verfügbar\n\nFür diese Funktion braucht Windows ein OCR-Sprachpaket (Einstellungen › Zeit und Sprache › Sprache › Optionen › „Optische Zeichenerkennung“)."]
            = "Text recognition not available\n\nThis feature requires a Windows OCR language pack (Settings › Time & language › Language › Options › \"Optical character recognition\").",
        ["Persönliche Daten suchen"] = "Find personal data",
        ["Texte auf Deutsch – die englischen Texte bleiben getrennt erhalten."] = "Texts in German – the English texts are kept separately.",
        ["Suchen nach …"] = "Find …",
        ["Ersetzen durch …"] = "Replace with …",
        ["Groß-/Kleinschreibung beachten"] = "Match case",
        ["Nur ganze Wörter"] = "Whole words only",
        ["Überschriften und Texte"] = "Headings and texts",
        ["Titel und Dokumenttexte"] = "Title and document texts",
        ["Suchen und Ersetzen"] = "Find and replace",
        ["Suchen"] = "Find",
        ["Ersetzen durch"] = "Replace with",
        ["Wo"] = "Where",
        ["Ersetzt wird in der aktuellen Textsprache. Automatisch erzeugte Texte werden dabei zu Ihren eigenen Texten."] = "Replacement happens in the current text language. Automatically generated texts become your own texts.",
        ["Alle ersetzen"] = "Replace all",
        ["Suchbegriff eingeben."] = "Enter a search term.",
        ["Keine Treffer."] = "No matches.",
        ["Verpixeln"] = "Pixelate",
        ["Persönliche Daten in Bildern"] = "Personal data in images",
        ["Die Texterkennung von Windows sucht in allen Bildern nach E-Mail-Adressen, IBANs, Kartennummern, Telefonnummern und Schlüsseln. Gefundene Stellen werden nur im jeweiligen Bild geschwärzt."]
            = "Windows text recognition searches all images for email addresses, IBANs, card numbers, phone numbers and keys. Found areas are redacted only in the respective image.",
        ["Ausgewählte schwärzen"] = "Redact selected",
        ["Art"] = "Type",
        ["Persönliche Daten in Bildern suchen"] = "Find personal data in images",
        ["Schritt eingefügt  "] = "Inserted step  ",

        // ---------------- ViewerWindow.cs ----------------
        ["Schritte durchsuchen"] = "Search steps",
        ["Suche (Strg+F)"] = "Search (Ctrl+F)",
        ["Klicken zum Umbenennen"] = "Click to rename",
        ["Für diesen Schritt gibt es kein Bild."] = "There is no image for this step.",
        ["Klick"] = "Click",
        ["Doppelklick"] = "Double-click",
        ["Dreifachklick"] = "Triple-click",
        ["Rechtsklick"] = "Right-click",
        ["Ziehen"] = "Drag",
        ["Eingabe"] = "Input",
        ["Taste"] = "Key",
        ["Zeigen"] = "Hover",
        ["Fensterwechsel"] = "Window switch",
        ["Warten"] = "Wait",
        ["Lücke"] = "Gap",
        ["Mausbewegung"] = "Mouse move",
        ["Eingefügt"] = "Inserted",
        ["Automatisch gespeichert"] = "Saved automatically",
        ["Eingaben geprüft"] = "Input checked",
        ["schreibgeschützt"] = "read-only",
        ["nach Absturz wiederhergestellt"] = "restored after crash",
        ["Vorher-Bild"] = "Before image",
        ["Nachher-Bild"] = "After image",
        ["zugeschnitten"] = "cropped",
        ["Code kopieren"] = "Copy code",
        ["AutoHotkey-Code kopiert"] = "AutoHotkey code copied",
        ["AutoHotkey-v2-Code für diesen Schritt kopieren"] = "Copy AutoHotkey v2 code for this step",
        ["Passwortfeld – Inhalt nicht gelesen"] = "Password field – content not read",
        ["Eingabefeld – Inhalt nur über UIA"] = "Input field – content only via UIA",
        ["Text im Privatmodus nicht gelesen"] = "Text not read in private mode",
        ["Fokus"] = "Focus",
        ["Zeiger"] = "Pointer",
        ["Wege"] = "Locators",
        ["Prüfung"] = "Check",
        ["Menü"] = "Menu",
        ["Auswahl"] = "Selection",
        ["Programm"] = "Program",
        ["läuft als Administrator"] = "runs as administrator",
        ["rechte Taste"] = "right button",
        ["mittlere Taste"] = "middle button",
        ["Zusatztaste"] = "extra button",
        ["linke Taste"] = "left button",
        ["Klickpunkt"] = "Click point",
        ["Zug- und Haltepfad"] = "Drag and hold path",
        ["Zielrahmen"] = "Target frame",
        ["Hinweise (Haltedauer, Scrollrichtung)"] = "Labels (hold time, scroll direction)",
        ["Mausweg davor"] = "Mouse path before",
        ["Tasten und Texteingaben"] = "Keys and text input",
        ["Alle Markierungen ausblenden"] = "Hide all markings",
        ["Markierungen einblenden"] = "Show markings",
        ["Wie alle Schritte"] = "Same as all steps",
        ["Diese Sitzung ist schreibgeschützt (.steprec). Bitte entpacken, um sie zu bearbeiten."] = "This session is read-only (.steprec). Please unpack it to edit it.",
        ["Rückgängig gemacht"] = "Undone",
        ["Wiederholt"] = "Redone",
        ["Hinweis einfügen (als Kommentar am Schritt)"] = "Insert note (as comment on the step)",
        ["Text des Schritts"] = "Step text",
        ["Welcher Text soll überall geschwärzt werden?"] = "Which text should be redacted everywhere?",
        ["Keine Fundstelle."] = "Nothing found.",
        ["Persönlichen Wert verallgemeinern – Wert (z. B. Benutzername):"] = "Generalize personal value – value (e.g. user name):",
        ["Benutzername"] = "User name",
        ["Platzhalter"] = "Placeholder",
        ["Alle Text- und Zeitraum-Schwärzungen jetzt endgültig in die Rohdaten schreiben?\n\nDas lässt sich nicht rückgängig machen."] = "Write all text and time-range redactions permanently into the raw data now?\n\nThis cannot be undone.",
        ["Endgültig schwärzen"] = "Redact permanently",

        // ---------------- ViewerCleanup.cs ----------------
        ["1 überflüssiger Schritt wurde ausgeblendet – rückgängig mit Strg+Z, Details unter ⋯ › Aufräumen"] = "1 unnecessary step was hidden – undo with Ctrl+Z, details under ⋯ › Clean up",
        ["Aufräumen"] = "Clean up",
        ["Keine weiteren Vorschläge"] = "No further suggestions",
        ["Alle sichtbaren Schritte wirken nötig."] = "All visible steps seem necessary.",
        ["Neue Aufnahmen beim ersten Öffnen automatisch aufräumen"] = "Clean up new recordings automatically when first opened",
        ["Aufräumen …"] = "Clean up …",
        ["aufgeräumt"] = "cleaned up",

        // ---------------- Überschriften in Großbuchstaben (U.Overline, Kopf des Markierungsmenüs) ----------------
        ["WICHTIGKEIT"] = "IMPORTANCE",
        ["AKTIONEN"] = "ACTIONS",
        ["PROTOKOLL FÜR KI"] = "LOG FOR AI",
        ["PROGRAMMKONTEXT"] = "APPLICATION CONTEXT",
        ["SICHTBARKEIT"] = "VISIBILITY",
        ["IM DOKUMENT"] = "IN THE DOCUMENT",
        ["HINWEISKASTEN"] = "CALLOUT",
        ["HINWEISKÄSTEN"] = "CALLOUTS",
        ["EINGEFÜGTE SCHRITTE"] = "INSERTED STEPS",
        ["WO"] = "WHERE",
        ["VORLAGE"] = "TEMPLATE",
        ["TITELSEITE"] = "TITLE PAGE",
        ["TEXTE"] = "TEXTS",
        ["AUFBAU"] = "STRUCTURE",
        ["BILDER"] = "IMAGES",
        ["GESTALTUNG"] = "DESIGN",
        ["WEITERE AUFNAHMEN"] = "MORE RECORDINGS",
        ["KAPITEL"] = "CHAPTER",
        ["ENTHÄLT"] = "CONTAINS",
        ["ALLE SCHRITTE"] = "ALL STEPS",
        ["KEINE WEITEREN VORSCHLÄGE"] = "NO FURTHER SUGGESTIONS",

        // ---------------- Exportdialog: Bildauswahl ----------------
        ["Automatisch"] = "Automatic",
        ["Immer"] = "Always",
        ["Nie"] = "Never",
        ["Bei Änderung"] = "When changed",
        ["Weitere Bilder"] = "More images",
        ["Noch keine Aufnahmen"] = "No recordings yet",
        ["Jeder Mausschritt bekommt eine eigene Übersicht vor der Aktion – auch wenn sie dem Nachher-Bild des vorigen Schritts gleicht."] = "Every mouse step gets its own overview before the action – even if it matches the after image of the previous step.",
        ["Keine Übersichten vor der Aktion. Klickpunkt und Zielrahmen zeigt weiterhin der Ausschnitt."] = "No overviews before the action. The close-up still shows the click point and the target frame.",
        ["Nur wenn der Bildschirm davor nicht schon im Paket zu sehen ist – sonst verweist das Protokoll auf das frühere Bild."] = "Only if the screen before is not already shown in the package – otherwise the trace refers to the earlier image.",
        ["Zusätzlich das Bild nach dem Schritt („Ergebnis“), wenn sich mindestens 2 % des Bildschirms geändert haben oder ein Fenster auf- oder zuging."] = "Also the image after the step (\"Result\") if at least 2 % of the screen changed or a window opened or closed.",
        ["Zusätzlich das Bild nach jedem Schritt („Ergebnis“), sofern eines vorliegt."] = "Also the image after every step (\"Result\"), if there is one.",
        ["Ein Bild je Schritt – wie im Editor."] = "One image per step – as in the editor.",
        ["ein PNG je Schritt (schritt-001.png …), dazu das Ergebnis (schritt-001-nachher.png), in der .md-Datei verlinkt."] = "one PNG per step (schritt-001.png …) plus the result (schritt-001-nachher.png), linked in the .md file.",
    };

    /// <summary>Texte, die auch als Teilstück einer mit „ · “ zusammengesetzten Zeile vorkommen (Statuszeile, Bildunterschrift, Technik-Zeile).</summary>
    static readonly string[] SegmentKeys =
    {
        "Automatisch gespeichert", "Eingaben geprüft", "Deutsch", "schreibgeschützt", "nach Absturz wiederhergestellt",
        "1 Schritt", "1 Aufnahme", "Schutz: streng", "Passwortfelder werden maskiert",
        "Vorher-Bild", "Nachher-Bild", "Ausschnitt", "zugeschnitten",
        "Klick", "Doppelklick", "Dreifachklick", "Rechtsklick", "Ziehen", "Scrollen", "Eingabe", "Tastenkürzel", "Taste", "Zeigen",
        "Fensterwechsel", "Warten", "Kommentar", "Schnappschuss", "Lücke", "Mausbewegung", "Eingefügt",
        "Hauptschritt", "Nebenschritt", "Spur",
    };

    public static readonly (Regex rx, string replacement)[] Patterns = Build();

    static (Regex, string)[] Build()
    {
        const string S = @"(?<=^|·\s{1,3})", E = @"(?=$|\s{1,3}·)";   // Grenzen eines Teilstücks „… · Teil · …“
        const string L = @"(?m)^", LE = @"(?=\r?$)";                     // Grenzen einer Zeile in mehrzeiligen Texten
        var p = new List<(Regex, string)>();
        void P(string rx, string repl) => p.Add((new Regex(rx, RegexOptions.CultureInvariant), repl));

        // Reihenfolge: zuerst Muster für ganze Texte, dann Teilstücke („ · “), zuletzt Tastennamen.

        // ---------------- Begrüßung (StartWindow.Greeting, Run mit Leerzeichen am Ende) ----------------
        P(@"^Guten Morgen(, [\p{L}-]+)?\.( ?)$", "Good morning$1.$2");
        P(@"^Guten Tag(, [\p{L}-]+)?\.( ?)$", "Good afternoon$1.$2");
        P(@"^Guten Abend(, [\p{L}-]+)?\.( ?)$", "Good evening$1.$2");
        P(@"^Version (\S+) · offline · kostenlos$", "Version $1 · offline · free");
        P(@"^Version (\S+) verfügbar$", "Version $1 available");

        // ---------------- Updates (UpdateCheck.cs) ----------------
        P(@"(?s)^Suche nach Updates fehlgeschlagen\n\n(.*)$", "Update check failed\n\n$1");
        P(@"^Sie verwenden die aktuelle Version \((.+)\)\.$", "You are using the latest version ($1).");
        P(@"^Version (.+) ist verfügbar \(installiert: (.+)\)\.\n\nRelease-Seite auf GitHub öffnen\?$", "Version $1 is available (installed: $2).\n\nOpen the release page on GitHub?");

        // ---------------- Bedienungshilfen (Accessibility.cs, AutomationProperties.Name) ----------------
        P(@"^Schritt (\d+): (.*) \(ausgeblendet\)$", "Step $1: $2 (hidden)");
        P(@"^Eingefügter Schritt: (.*) \(ausgeblendet\)$", "Inserted step: $1 (hidden)");
        P(@"^Eingefügter Schritt: (.*)$", "Inserted step: $1");
        P(@"^Schritt (\d+): (.*)$", "Step $1: $2");

        // ---------------- Aufnahme: Leiste, Infobereich, Countdown ----------------
        P(@"^Ohne sichtbare Anzeige\. Beenden: (\S+) · Anzeige: (\S+)$", "No visible indicator. Stop: $1 · Indicator: $2");
        P(@"^Tastenkürzel belegt: (.+)$", "Shortcuts already in use: $1");
        P(@"^Marker (\d+) \(Fehler\) gesetzt$", "Marker $1 (error) set");
        P(@"^Marker (\d+) \(Erwartet\) gesetzt$", "Marker $1 (expected) set");
        P(@"^Marker (\d+) \(Wichtig\) gesetzt$", "Marker $1 (important) set");
        P(@"^Marker (\d+) \(Notiz\) gesetzt$", "Marker $1 (note) set");
        // Kopfzeile des Infobereich-Menüs und (mehrzeilig) Tooltip des Infobereich-Symbols
        P(@"(?m)^Aufnahme läuft \(privat\) · (\d+:\d{2}) · (\d+) Schritte$", "Recording (private) · $1 · $2 steps");
        P(@"(?m)^Aufnahme läuft · (\d+:\d{2}) · (\d+) Schritte$", "Recording · $1 · $2 steps");
        P(@"(?m)^Aufnahme pausiert · (\d+:\d{2}) · (\d+) Schritte$", "Recording paused · $1 · $2 steps");
        P(@"(?m)^Klick: Leiste · Rechtsklick: Menü$", "Click: bar · Right-click: menu");
        P(@"^(\S+) stoppt\.$", "$1 stops.");

        // Diktat (RecordingController.ToggleDictation, Dictation.cs)
        P(@"^Diktiert: (.*)$", "Dictated: $1");
        P(@"^Diktat läuft \((.+)\) – sprechen Sie; jeder Satz wird ein Kommentar\. Strg\+Umschalt\+F5 beendet\.$", "Dictation running ($1) – speak; each sentence becomes a comment. Ctrl+Shift+F5 stops.");
        P(@"(?s)^Diktat nicht möglich\n\n", "Dictation not possible\n\n");
        P(@"(?m)^Auf diesem Rechner ist keine Windows-Spracherkennung installiert \(gefunden: keine\)\. Sie lässt sich unter Einstellungen › Zeit und Sprache › Sprache › Sprachpaket › „Spracherkennung“ hinzufügen\.$",
          "No Windows speech recognition is installed on this computer (found: none). You can add it under Settings › Time & language › Language › Language pack › \"Speech recognition\".");
        P(@"(?m)^Auf diesem Rechner ist keine Windows-Spracherkennung installiert \(gefunden: (.*)\)\. Sie lässt sich unter Einstellungen › Zeit und Sprache › Sprache › Sprachpaket › „Spracherkennung“ hinzufügen\.$",
          "No Windows speech recognition is installed on this computer (found: $1). You can add it under Settings › Time & language › Language › Language pack › \"Speech recognition\".");
        P(@"(?m)^Das Mikrofon oder die Spracherkennung konnte nicht gestartet werden: ", "The microphone or speech recognition could not be started: ");

        // ---------------- Startfenster: Bereichswahl, Einstellungen ----------------
        P(@"(?m)^(.*) · alle Fenster dieser Anwendung$", "$1 · all windows of this application");
        P(@"(?m)^ausgeschlossen – wird nicht aufgezeichnet$", "excluded – will not be recorded");
        P(@"^Portabler Modus – Einstellungen liegen neben der EXE \((.+)\)\.$", "Portable mode – settings are stored next to the EXE ($1).");
        P(@"^Einstellungen liegen in (.+)\.$", "Settings are stored in $1.");
        P(@"(?s)^Schutz abschwächen\?\n\nFolgende Ausschlüsse werden entfernt und deren Eingaben künftig aufgezeichnet:\n(.*)$", "Weaken protection?\n\nThe following exclusions will be removed and their input will be recorded from now on:\n$1");
        P(@"^alles – (.*)$", "everything – $1");

        // ---------------- Editor: Kopieren, Export, Durchsicht ----------------
        P(@"^Bild kopiert · (\d+ × \d+)$", "Image copied · $1");
        P(@"^Originalbild kopiert · (\d+ × \d+)$", "Original image copied · $1");
        P(@"^(\d+) auffällige Eingaben sind noch nicht durchgesehen\. Trotzdem Text kopieren\?$", "$1 flagged inputs have not been reviewed yet. Copy text anyway?");
        P(@"^(\d+) auffällige Eingaben sind noch nicht durchgesehen\. Die Anleitung kann erst nach der Durchsicht erstellt werden\.\n\nJetzt prüfen\?$", "$1 flagged inputs have not been reviewed yet. The guide can only be created after the review.\n\nReview now?");
        P(@"^Vorschau erst nach der Durchsicht\n\n(\d+) auffällige Eingaben sind noch nicht durchgesehen\. Jetzt durchsehen\?$", "Preview only after the review\n\n$1 flagged inputs have not been reviewed yet. Review now?");
        P(@"^(\d+) auffällige Eingaben – bitte für jede entscheiden, ob sie bleiben darf\.$", "$1 flagged inputs – please decide for each one whether it may stay.");
        P(@"^Exportiert: (.+)$", "Exported: $1");
        P(@"(?s)^Export erstellt:\n(.*)\n\nIm Explorer zeigen\?$", "Export created:\n$1\n\nShow in Explorer?");
        P(@"(?s)^Support-Paket erstellt:\n(.*)\n\nGeben Sie das ZIP selbst an den Helpdesk weiter\. Im Explorer zeigen\?$", "Support package created:\n$1\n\nPass the ZIP on to the helpdesk yourself. Show in Explorer?");
        P(@"^Grund: (.*)$", "Reason: $1");
        P(@"^UNAUFFÄLLIGE EINGABEN \((\d+)\)$", "UNFLAGGED INPUTS ($1)");
        P(@"^Unauffällige Eingaben \((\d+)\)$", "Unflagged inputs ($1)");
        P(@"^Gespeichert im Aufnahmeordner unter (.+)$", "Saved in the recording folder under $1");
        foreach (var (de, en) in new[] { (ImpBoth, ImpBothEn), (ImpAll, ImpAllEn), (ImpMain, ImpMainEn) })
        {
            P(@"^Titelbild, dann " + Regex.Escape(de) + @" mit Bild, Nummer und Überschrift; (\d+),(\d+) Sekunden je Schritt, läuft in Schleife\.$", "Title image, then " + en + " with image, number and heading; $1.$2 seconds per step, loops.");
            P(@"^Titelbild, dann " + Regex.Escape(de) + @" mit Bild, Nummer und Überschrift; (\d+) Sekunden je Schritt, läuft in Schleife\.$", "Title image, then " + en + " with image, number and heading; $1 seconds per step, loops.");
        }

        // Sitzungsübersicht (mehrzeilig, Zeilenenden \r\n)
        P(L + @"Titel: (.*?)" + LE, "Title: $1");
        P(L + @"Aufgenommen: (.*?) · Dauer (.*?)" + LE, "Recorded: $1 · Duration $2");
        P(L + @"Zustand: (.*?) · Profil: (.*?) · Schutzstufe: (.*?)" + LE, "State: $1 · Profile: $2 · Protection level: $3");
        P(L + @"Ziel: (.*?) · Erwartet: (.*?) · Bezug: (.*?)" + LE, "Goal: $1 · Expected: $2 · Reference: $3");
        P(L + @"Vollständigkeitsbericht" + LE, "Completeness report");
        P(L + @"  Buchführung: (\d+) Eingabeereignisse, (\d+) zugeordnet, (\d+) ohne Schritt" + LE, "  Bookkeeping: $1 input events, $2 assigned, $3 without step");
        P(L + @"  Belegqualität: (\d+) Aktionsschritte · (\d+) belegt · (\d+) nur Eingabe · (\d+) unbekannt" + LE, "  Evidence quality: $1 action steps · $2 confirmed · $3 input only · $4 unknown");
        P(L + @"  Lücken: (\d+) \((.*?)\)" + LE, "  Gaps: $1 ($2)");
        P(L + @"  Bilder: (\d+) angefordert · (\d+) gespeichert · (\d+) unverändert · Vorher-Bild garantiert (\d+/\d+)" + LE, "  Images: $1 requested · $2 stored · $3 unchanged · before image guaranteed $4");
        P(L + @"Speicher: (.*?) MB · (.*?)" + LE, "Storage: $1 MB · $2");
        P(L + @"Ordner im Explorer öffnen\?" + LE, "Open folder in Explorer?");

        // ---------------- Editor: Dokument, Vorlagen ----------------
        P(@"^Vorlage „(.*)“ angewendet – rückgängig mit Strg\+Z$", "Template \"$1\" applied – undo with Ctrl+Z");
        P(@"^(.*)  \(Standard\)$", "$1  (default)");
        P(@"^Neue Aufnahmen erhalten automatisch die Vorlage „(.*)“\.$", "New recordings automatically get the template \"$1\".");
        P(@"(?s)^Vorlage „(.*)“ gespeichert\.\n\nSollen neue Aufnahmen diese Vorlage automatisch erhalten\?$", "Template \"$1\" saved.\n\nShould new recordings get this template automatically?");
        P(@"^nicht gefunden: (.*)$", "not found: $1");
        P(@"(?s)^Das Logo lässt sich nicht öffnen:\n(.*)$", "The logo cannot be opened:\n$1");
        P(@"(?s)^Das Bild lässt sich nicht öffnen:\n(.*)$", "The image cannot be opened:\n$1");
        P(@"^Vorschau geöffnet: (.*)$", "Preview opened: $1");
        P(@"^Hinweiskästen \((\d+)\)$", "Callouts ($1)");
        P(@"^HINWEISKÄSTEN \((\d+)\)$", "CALLOUTS ($1)");

        // ---------------- Editor: Liste, Details, Status ----------------
        P(@"^(\d+) Schritte ausblenden$", "Hide $1 steps");
        P(@"^(\d+) Schritte einblenden$", "Show $1 steps");
        P(@"^(.*) – bitte in der Durchsicht prüfen, bevor Sie die Aufnahme weitergeben\.$", "$1 – please check it in the review before you share the recording.");
        P(@"^„(.*)“ · (.*)$", "\"$1\" · $2");
        P(@"^„(.*)“\n(.*)$", "\"$1\"\n$2");
        P(@"^Feldinhalt danach: (.*)$", "Field content afterwards: $1");
        P(@"^Schritt (\d+) zugeschnitten \((\d+) × (\d+)\) – rückgängig mit Strg\+Z oder „Zuschnitt entfernen“\.$", "Step $1 cropped ($2 × $3) – undo with Ctrl+Z or \"Remove crop\".");
        P(@"^Zuschnitt auf (\d+) Schritte übertragen – rückgängig mit Strg\+Z\.$", "Crop applied to $1 steps – undo with Ctrl+Z.");
        P(@"^Mausmarkierungen bei Schritt (\d+) eingeblendet$", "Mouse markings shown for step $1");
        P(@"^Mausmarkierungen bei Schritt (\d+) ausgeblendet$", "Mouse markings hidden for step $1");
        P(@"^NUR SCHRITT (\d+)$", "STEP $1 ONLY");
        P(@"^Nur Schritt (\d+)$", "Step $1 only");
        P(@"^Fenstertexte \((\d+)\)$", "Window texts ($1)");
        P(@"^(\d+) Fundstellen geschwärzt \(Texte\)\. Im Bild sichtbare Stellen bitte zusätzlich mit dem Werkzeug „Schwärzen“ markieren\.$", "$1 occurrences redacted (texts). Please also mark places visible in the image with the \"Redact\" tool.");
        P(@"^(\d+) Stellen endgültig geschwärzt\.$", "$1 occurrences permanently redacted.");

        // Technik-Reiter (schreibgeschützte Textfelder; mehrzeilig)
        P(@"„([^“]*)“ erschien", "\"$1\" appeared");
        P(@"„([^“]*)“ geschlossen", "\"$1\" closed");
        P(@"Titel → „([^“]*)“", "Title → \"$1\"");
        P(@"Bildschirm ([\d.,]+) % geändert", "Screen $1 % changed");
        P(@"^Text: „(.*)“$", "Text: \"$1\"");
        P(L + @"Befehls-ID (\d+) \(WM_COMMAND\)" + LE, "Command ID $1 (WM_COMMAND)");
        P(L + @"deaktiviert" + LE, "disabled");
        P(L + @"Eintrag „(.*)“" , "Item \"$1\"");
        P(@" · Nr\. (\d+) von (\d+)", " · no. $1 of $2");
        P(@" · Nr\. (\d+)(?=\r?$|\n)", " · no. $1");
        P(L + @"Pfad (.*?)" + LE, "Path $1");
        P(L + @"(\d+) ausgewählt" + LE, "$1 selected");
        P(L + @"Dateiname-Feld (.*?)" + LE, "File name field $1");
        P(L + @"Blatt (.*?) · Zelle (.*?)" + LE, "Sheet $1 · cell $2");
        P(L + @"Blatt (.*?)" + LE, "Sheet $1");
        P(L + @"\(nach der Aktion geändert\)" + LE, "(changed after the action)");
        P(@"^Zeile (\d+), Spalte (\d+)", "Line $1, column $2");
        P(@" · (\d+) Zeichen markiert", " · $1 characters selected");
        P(@" · schreibgeschützt$", " · read-only");
        P(@" · Standardaktion „(.*)“$", " · default action \"$1\"");
        P(@"„(.*)“ \(links daneben\)", "\"$1\" (to the left)");
        P(@"„(.*)“ \(darüber\)", "\"$1\" (above)");
        P(@"\bNr\. (\d+)/(\d+)", "no. $1/$2");
        P(L + @"beschäftigt (\d+) ms \((.*?)\)" + LE, "busy $1 ms ($2)");
        P(L + @"Fenster „(.*)“ nach (\d+) ms" + LE, "Window \"$1\" after $2 ms");
        P(L + @"Bild ruhig nach (\d+) ms" + LE, "Image still after $1 ms");
        P(L + @"bereit nach (\d+) ms" + LE, "ready after $1 ms");
        P(L + @"Denkpause (\d+) ms" + LE, "Think time $1 ms");
        P(L + @"(\S+) neu: „(.*)“" + LE, "$1 new: \"$2\"");
        P(L + @"(\S+) weg" + LE, "$1 gone");
        P(L + @"(\S+) geändert" + LE, "$1 changed");
        P(L + @"Fokus danach (.*?)" + LE, "Focus afterwards $1");
        P(@"^„(.*)“ \(Nr\. (\d+) von (\d+)\)$", "\"$1\" (no. $2 of $3)");
        P(L + @"Fenster  ", "Window  ");
        P(L + @"gedrückt (\d+) ms · Weg (\d+) px" + LE, "pressed $1 ms · distance $2 px");
        P(L + @"vor der Bewegung gehalten (\d+) ms" + LE, "held before moving $1 ms");
        P(L + @"über dem Ziel verweilt (\d+) ms" + LE, "hovered over target $1 ms");
        P(L + @"gedrückt (\d+) ms \(gedrückt gehalten\)" + LE, "pressed $1 ms (held down)");
        P(L + @"gedrückt (\d+) ms" + LE, "pressed $1 ms");
        P(@"\+ rechte Taste$", "+ right button");
        P(@"\+ mittlere Taste$", "+ middle button");
        P(@"\+ Zusatztaste$", "+ extra button");
        P(@"\+ linke Taste$", "+ left button");
        P(L + @"rechte Taste" + LE, "right button");
        P(L + @"mittlere Taste" + LE, "middle button");
        P(L + @"Zusatztaste" + LE, "extra button");
        P(L + @"linke Taste" + LE, "left button");

        // ---------------- Editor: Mehrfachauswahl, Einfügen, Suchen, Texterkennung ----------------
        foreach (var (de, en) in new[] { ("Hinweis", "Note"), ("Tipp", "Tip"), ("Achtung", "Warning"), ("Wichtig", "Important"), ("Ergebnis", "Result") })
            P(@"^" + de + @" zu (\d+) Schritten hinzufügen$", "Add " + en + " to $1 steps");
        P(@"^(\d+) eingefügte löschen$", "Delete $1 inserted");
        P(@"^(\d+) Schritte geändert – rückgängig mit Strg\+Z$", "$1 steps changed – undo with Ctrl+Z");
        P(@"^(\d+) Stellen in Bildern geschwärzt – rückgängig mit Strg\+Z$", "$1 areas in images redacted – undo with Ctrl+Z");
        P(@"^(\d+) Stellen ersetzt – rückgängig mit Strg\+Z im Editor\.$", "$1 occurrences replaced – undo with Ctrl+Z in the editor.");
        P(@"^Bild (\d+) von (\d+) wird geprüft …  \((\d+) Fundstellen\)$", "Checking image $1 of $2 …  ($3 found)");
        P(@"^(\d+) Bilder geprüft – keine Fundstellen\. Namen und frei geschriebene Angaben erkennt die Suche nicht; bitte trotzdem selbst durchsehen\.$", "$1 images checked – nothing found. The search does not detect names and free-form details; please review them yourself anyway.");
        P(@"^(\d+) Bilder geprüft – (\d+) Fundstellen\. Bitte prüfen und auswählen\.$", "$1 images checked – $2 found. Please check and select.");

        // ---------------- Aufräumen (ViewerCleanup.cs) ----------------
        P(@"^(\d+) überflüssige Schritte wurden ausgeblendet – rückgängig mit Strg\+Z, Details unter ⋯ › Aufräumen$", "$1 unnecessary steps were hidden – undo with Ctrl+Z, details under ⋯ › Clean up");
        P(@"^Aufgeräumt: (\d+) ausgeblendet, (\d+) wieder eingeblendet – rückgängig mit Strg\+Z$", "Cleaned up: $1 hidden, $2 shown again – undo with Ctrl+Z");
        P(@"^Aufgeräumt: (\d+) ausgeblendet – rückgängig mit Strg\+Z$", "Cleaned up: $1 hidden – undo with Ctrl+Z");
        P(@"^Aufgeräumt: (\d+) wieder eingeblendet – rückgängig mit Strg\+Z$", "Cleaned up: $1 shown again – undo with Ctrl+Z");
        P(@"^Vorschläge \((\d+)\)$", "Suggestions ($1)");
        P(@"^VORSCHLÄGE \((\d+)\)$", "SUGGESTIONS ($1)");
        P(@"^Bereits aufgeräumt \((\d+)\) – Haken setzen, um wieder einzublenden$", "Already cleaned up ($1) – check to show again");
        P(@"^BEREITS AUFGERÄUMT \((\d+)\) – HAKEN SETZEN, UM WIEDER EINZUBLENDEN$", "ALREADY CLEANED UP ($1) – CHECK TO SHOW AGAIN");
        P(@"^Regeln: (.*)\. Ausgeblendete Schritte bleiben in der Aufnahme erhalten und fehlen nur in Anleitung und Export\.$", "Rules: $1. Hidden steps remain in the recording and are only missing from the guide and export.");

        P(@"^Clip mit (\d+) Bildern gespeichert$", "Clip with $1 images saved");

        // ---------------- Zeitangaben „zuletzt …“, „heute, 14:05“, Wochentage (auch als Teilstück) ----------------
        var days = new[] { ("Montag", "Monday"), ("Dienstag", "Tuesday"), ("Mittwoch", "Wednesday"), ("Donnerstag", "Thursday"), ("Freitag", "Friday"), ("Samstag", "Saturday"), ("Sonntag", "Sunday"), ("heute", "today"), ("gestern", "yesterday") };
        foreach (var (de, en) in days)
        {
            P(S + "zuletzt " + de + @", (\d{1,2}:\d{2})" + E, "latest: " + en + ", $1");
            P(S + de + @", (\d{1,2}:\d{2})" + E, en + ", $1");
        }
        P(S + @"zuletzt (\d{2}\.\d{2}\.\d{4})" + E, "latest: $1");

        // ---------------- Zähler und Schrittangaben (auch als Teilstück) ----------------
        P(S + @"Schritt (\d+) von (\d+)" + E, "Step $1 of $2");
        P(S + @"Schritt (\d+)" + E, "Step $1");
        P(@"^Schritt (\d+)  $", "Step $1  ");
        P(S + @"(\d+) Schritte ausgewählt" + E, "$1 steps selected");
        P(S + @"(\d+) Schritte" + E, "$1 steps");
        P(S + @"(\d+) Hauptschritte" + E, "$1 main steps");
        P(S + @"(\d+) Aufnahmen" + E, "$1 recordings");
        P(S + @"(\d+) Treffer" + E, "$1 matches");
        P(S + @"(\d+) Schwärzungen" + E, "$1 redactions");
        P(S + @"(\d+) Eingaben ungeprüft" + E, "$1 inputs unchecked");
        P(S + @"(\d{2}\.\d{2}\.\d{4}), (\d{1,2}:\d{2}) Uhr" + E, "$1, $2");
        P(S + "Hauptmonitor" + E, "Primary monitor");

        // ---------------- Teilstücke zusammengesetzter Zeilen ----------------
        foreach (var k in SegmentKeys) P(S + Regex.Escape(k) + E, Exact[k].Replace("$", "$$"));

        // ---------------- Tastennamen in Kürzeln (zuletzt) ----------------
        P(@"\bStrg\+", "Ctrl+");
        P(@"\bUmschalt\+", "Shift+");
        P(@"\+Entf\b", "+Del");
        P(@"\+Einfg\b", "+Ins");
        P(@"\+Eingabe\b", "+Enter");
        P(@"\+Mausrad\b", "+mouse wheel");
        P(@"\+Klick\b", "+click");

        return p.ToArray();
    }
}
