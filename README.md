English version: [README.en.md](README.en.md)

# Sebastians Step Recorder

**Arbeitsschritte einmal vorführen – daraus entsteht eine fertige Anleitung mit Bildern.**

Sebastians Step Recorder zeichnet auf, was Sie am Windows-PC tun: jeden Klick, jede Eingabe, jede Tastenkombination – samt Bildschirmfoto und dem Namen des angeklickten Bedienelements. Daraus entstehen Bedienungsanleitungen als PDF, Word, HTML, Markdown oder animiertes GIF, ein Paket für KI-Assistenten oder ein AutoHotkey-Skript, das den Ablauf wiederholt.

Eine einzelne Datei, keine Installation, keine Adminrechte, kein Internet. Das Programm ist in seinen eigenen Bildschirmfotos nie zu sehen. **Kostenlos und frei** – auch beruflich ([MIT-Lizenz](#lizenz)).

**[⬇ Download: SebastiansStepRecorder.exe](https://github.com/Sebedai/sebastians-step-recorder/releases/latest)**

---

## Inhalt

- [Schnellstart](#schnellstart)
- [Die Aufnahmemodi](#die-aufnahmemodi)
- [Während der Aufnahme](#während-der-aufnahme)
- [Der Editor](#der-editor)
- [Bedienungsanleitungen gestalten](#bedienungsanleitungen-gestalten)
- [Exportformate](#exportformate)
- [Datenschutz](#datenschutz)
- [Für KI und Automatisierung](#für-ki-und-automatisierung)
- [Für Software-Entwickler](#für-software-entwickler)
- [Systemvoraussetzungen und Hinweise](#systemvoraussetzungen-und-hinweise)
- [Häufige Fragen](#häufige-fragen)
- [Lizenz](#lizenz)

---

## Schnellstart

1. **`SebastiansStepRecorder.exe` herunterladen** und an einen beliebigen Ort legen, z. B. `C:\Tools\` oder einen USB-Stick. Doppelklick startet das Programm.
2. **Zweck wählen:** Wofür ist die Aufnahme? Im Zweifel „Anleitung“. (Links im Startfenster liegen außerdem **Aufnahmen** – alle bisherigen Aufnahmen mit Suche –, **Einstellungen** und **Hilfe** mit allen Tastenkürzeln.)
3. **Bereich anklicken:** ganzer Bildschirm (oder einfach die Eingabetaste), eine einzelne Anwendung oder ein selbst aufgezogener Ausschnitt. Die Aufnahme startet sofort.
4. **Vorführen:** ganz normal arbeiten. Zum Beenden **Strg+Umschalt+F10** drücken oder in der Aufnahmeleiste auf „Stopp“ klicken.
5. **Nachbearbeiten und exportieren:** Der Editor öffnet sich mit allen Schritten. Texte anpassen, Bilder markieren, dann **Exportieren** – fertig.

Beim ersten Start warnt Windows SmartScreen eventuell, weil die Datei nicht signiert ist: „Weitere Informationen“ → „Trotzdem ausführen“.

---

## Die Aufnahmemodi

Der Modus legt fest, wie genau aufgezeichnet wird. Mehr Detail heißt genauere KI- und Skript-Ausgaben, aber auch größere Aufnahmen.

| Modus | Gedacht für | Detailgrad | Nach dem Stopp |
|---|---|---|---|
| **Anleitung** | Schritt-für-Schritt-Anleitungen für Menschen | **Schlank:** Klicks, Eingaben und Tasten; keine Mauswege; Zusatzbilder nur bei großen Änderungen | Editor öffnet sich |
| **Support-Fall** | Fehler für den Helpdesk nachvollziehbar machen | **Ausführlich:** alle Eingaben inklusive Mauswegen; Zusatzbilder auch bei kleinen Änderungen; erweiterter Programmkontext | Eingaben prüfen, dann entsteht ein ZIP-Paket |
| **KI-Spur** | Abläufe für KI-Assistenten oder AutoHotkey | **Vollständig:** alles; Zusatzbilder bei jeder sichtbaren Änderung; erweiterter Programmkontext | KI-Export öffnet sich |
| **Fehler nachstellen** | Einen Fehler für Entwickler belegen | **Ausführlich** wie Support-Fall, Ziel und erwartetes Ergebnis gleich mit erfasst, Programm-Kontext (Programmende, Hänger, Build-Stand) | Editor springt zur Fehlerstelle |
| **Eigene** | Wenn Sie es genau selbst bestimmen wollen | **Frei wählbar:** Eingabearten, Zusatzbilder, Programmkontext, Aktion nach dem Stopp | wählbar |

„Anpassen …“ übernimmt den Detailgrad eines Modus als Ausgangspunkt für „Eigene“.

### Wann entstehen Bilder?

**In jedem Modus** entstehen zu jeder Eingabe zwei Bilder: eines direkt **vor** der Eingabe und eines, sobald der Bildschirm **danach** 0,3 Sekunden ruhig ist (spätestens nach 3 Sekunden).

**Zusatzbilder** entstehen, wenn sich der Bildschirm ändert, ohne dass Sie etwas eingeben – z. B. weil ein Programm lädt. Sie werden aufgenommen, sobald der Bildschirm danach 0,3 Sekunden ruhig ist, bei Dauerbewegung spätestens nach 5 Sekunden. Wie groß die Änderung dafür sein muss, legt die Einstellung fest:

| Einstellung | Zusatzbild ab einer Änderung von … | Beispiele | Höchstens alle | Modus |
|---|---|---|---|---|
| **Nur große Änderungen** | ca. 2 % des Bildschirms (etwa 200 × 200 Pixel bei Full HD) | neues Fenster, Seitenwechsel | 2 Sekunden | Anleitung |
| **Auch kleine Änderungen** | ca. 0,1 % (etwa 45 × 45 Pixel) | Meldung, Tooltip, Statusanzeige | 0,5 Sekunden | Support-Fall |
| **Jede sichtbare Änderung** | ca. 0,01 % (etwa 15 × 15 Pixel) | Fortschrittsbalken, Zähler, Ladeanimation | 0,25 Sekunden | KI-Spur |

**Erweiterter Programmkontext** bedeutet: Zusätzlich werden Menüpfade, Listeneinträge, Explorer-Ordner, Browser-Adressen, Office-Dokumente und Statusleisten gelesen. Für Anleitungen reicht der Basis-Kontext, für gute AutoHotkey-Skripte und KI-Auswertungen lohnt sich der erweiterte.

---

## Während der Aufnahme

Oben auf dem Bildschirm schwebt eine kleine **Aufnahmeleiste** mit Zeit, Schrittzahl und den wichtigsten Knöpfen. Zusätzlich erscheint ein **Symbol im Infobereich der Taskleiste** (unten rechts). Ein Klick darauf blendet die Leiste ein oder aus, ein Rechtsklick öffnet ein Menü mit allen Befehlen. Unter Windows 11 liegt das Symbol zunächst im Überlauf „^“; über *Taskleisteneinstellungen → Andere Taskleistensymbole* lässt es sich dauerhaft anzeigen.

| Tastenkürzel | Wirkung |
|---|---|
| **Strg+Umschalt+F9** | Aufnahme sofort starten · Pause / Fortsetzen |
| **Strg+Umschalt+F10** | Aufnahme beenden und speichern |
| **Strg+Umschalt+F1** | Fehlerstelle markieren |
| **Strg+Umschalt+F2** | Marker „Erwartet“ |
| **Strg+Umschalt+F7** | Marker setzen |
| **Strg+Umschalt+F12** | Kommentar eingeben |
| **Strg+Umschalt+F5** | Diktat ein/aus: Gesprochenes wird zu Kommentaren |
| **Strg+Umschalt+F11** | Schnappschuss |
| **Strg+Umschalt+F8** | Privattaste: Eingaben werden nur gezählt, nicht gespeichert |
| **Strg+Umschalt+F6** | Letzten Schritt verwerfen |
| **Strg+Umschalt+F4** | Leiste ein-/ausblenden (in den Infobereich minimieren) |
| **Strg+Umschalt+F3** | Sofort-Bildschirmfoto: Bereich wählen, im Editor bemalen, kopieren (während einer Aufnahme: Schnappschuss) |

**Diktat:** Statt zu tippen, sprechen Sie Ihre Erklärungen einfach. Jeder Satz wird als Kommentar an der Stelle gespeichert, an der Sie ihn begonnen haben. Die Erkennung läuft offline über die Windows-Spracherkennung; das Mikrofon ist nur offen, solange das Diktat läuft (rotes Mikrofon in der Leiste), und gespeichert wird nur der Text.

**Countdown und Signaltöne:** Auf Wunsch zählt das Programm vor dem Start herunter und gibt kurze Töne bei Start, Stopp, Pause und Markern aus – praktisch, wenn keine Leiste zu sehen ist.

---

## Der Editor

Nach der Aufnahme öffnet sich der Editor: links die Schritte, in der Mitte das Bild, rechts Text und Einstellungen. Oben steht eine Menüleiste wie in Editor oder Paint (Datei · Bearbeiten · Schritt · Bild · Ansicht · Prüfen · Hilfe) – dort findet sich jede Funktion an genau einer Stelle; rechts daneben die häufigsten Befehle als Knöpfe. **F1** zeigt alle Tastenkürzel.

**Schritte**
- Überschrift und Anleitungstext direkt bearbeiten (mit Rechtschreibprüfung) – die automatisch erzeugten Texte sind ein guter Anfang.
- Schritte per Ziehen umsortieren, ausblenden oder als Haupt-/Nebenschritt einstufen.
- **Mehrere Schritte** mit Strg+Klick bzw. Umschalt+Klick auswählen und gemeinsam bearbeiten.
- **Eigene Schritte einfügen:** reine Textschritte oder Bildschritte aus Datei, Zwischenablage oder einem neuen Bildschirmfoto.
- **Bild ersetzen oder neu aufnehmen**, wenn sich die Oberfläche geändert hat – das Original lässt sich jederzeit wiederherstellen.
- **Abschnitt neu aufnehmen und ersetzen** (Strg+R): Schritte markieren, den Abschnitt neu vorführen – die neuen Schritte treten an die Stelle der alten. Die alten bleiben erhalten, die Nahtstelle ist gekennzeichnet, Strg+Z nimmt die Ersetzung zurück.
- **Lesezeichen** (Strg+Umschalt+L): Schritte mit einem Stern merken und danach filtern.
- **Textbausteine** (Strg+Umschalt+B): oft gebrauchte Texte für Hinweise und Schritttexte einfügen; verwaltet unter *Einstellungen → Allgemein*.
- **Schrittnummern und Markierungen im Bild:** Auf Wunsch steht die Nummer des Schritts – genau wie in der Anleitung, z. B. „2.3“ – direkt an der Klickstelle (Ansicht → Mausmarken → Schrittnummer). Stile für die Marken: Klassisch, Nummern, Dezent, Kräftig. Zwölf Farben, freie Farbwahl und Pipette; Nummern zählen nach dem Löschen automatisch neu (auch als a, b, c oder i, ii, iii), Pfeile mit zwei Spitzen, gestrichelte Rahmen, gefüllte Flächen, farbiger Markierstift; „Stil übertragen“ und „Ziel markieren“ mit einem Klick.
- **Seite und Layout:** Layoutvorlagen (Standard, Kompakt, Schulung, Handbuch), Rand, Kopf- und Fußzeile mit Feldern wie „Seite 2 von 9“, Kapitelname oder Version, Kapitel oder jeder Schritt auf neuer Seite und Seitenumbruch an einzelnen Schritten.
- **Handbuch-Bausteine:** Impressum, Änderungsverzeichnis (Version, Datum, Autor, Änderung) und Glossar – je Sprache, vorn oder am Ende – sowie eine vorbereitete Störungsbehebung (Problem, Ursache, Abhilfe).
- **Bildunterschriften und Warnhinweise:** „Abb. 1: …“ unter jedem Bild (automatisch oder eigener Text) samt Abbildungsverzeichnis, Alternativtext für Bildschirmleser, Warnhinweise mit Signalwort (GEFAHR, WARNUNG, VORSICHT, HINWEIS) – auf Wunsch vor dem Schritt, auf den sie sich beziehen – und Hinweise in Deutsch und Englisch getrennt.
- **Gliederung:** Kapitel und Unterkapitel (Nummern wie 2.3.1), Kapitel mit Alt+Umschalt+↑/↓ als Ganzes verschieben oder ausblenden, Zählweise „je Kapitel“ und Textabschnitte ohne Nummer – leer oder vorbefüllt als Sicherheitshinweise, Häufige Fragen, Kontakt. Eine Kapitelüberschrift bleibt erhalten, auch wenn ihr erster Schritt ausgeblendet ist.
- **Querverweise, Variablen und Tabellen:** Über „＋“ im Textfeld verweist ein Text auf einen anderen Schritt („siehe Schritt 2.3“ – die Nummer stimmt auch nach dem Umsortieren), setzt Variablen wie {{Produkt}} oder {{Version}} ein (festgelegt im Dokumentbereich, je Sprache) und fügt Tabellen ein, die in PDF, Word, HTML und Markdown als echte Tabellen erscheinen.
- **Formatierter Text:** Im Anleitungstext, in Hinweisen und in Einleitung und Abschluss gibt es **fett** (Strg+B), *kursiv* (Strg+I), Eingaben wie `notiz.txt` (Strg+E), Tasten wie Strg+S, Namen von Schaltflächen, Links (Strg+K) sowie Aufzählungen und Nummerierungen. Die Formatierung erscheint in PDF, Word, HTML und Markdown; Schwärzungen und Platzhalter gelten trotzdem überall.
- **Suchen und Ersetzen** (Strg+H) über alle Überschriften, Texte, Hinweise und Kapitel.
- **Automatisch aufgeräumt:** Beim ersten Öffnen blendet der Editor überflüssige Schritte aus – den Klick ins Feld direkt vor der Eingabe, doppelte Klicks auf dasselbe Element, Fensterwechsel hin und sofort zurück und Eingaben, die wieder gelöscht wurden. Nichts wird gelöscht: Strg+Z macht es rückgängig, unter *Prüfen → Aufräumen* sehen Sie jeden Vorschlag mit Begründung.
- **Bessere Texte:** Menüs erscheinen als Pfad („Datei › Speichern unter“), Schaltflächen im Menüband mit ihrer Gruppe. Eine **Begriffsliste** in den Einstellungen ersetzt technische Namen durch Ihre eigenen (z. B. „SAP Easy Access=SAP-Startseite“).

**Bilder**
- Vorher-/Nachher-Bild, ganzer Bildschirm, nur das Anwendungsfenster oder ein Ausschnitt um die Aktion (mit kleiner Übersicht des ganzen Bildschirms).
- Zeichenwerkzeuge: Pfeil, Rahmen, Ellipse, Hervorheben, Text, Nummern, Schwärzen, Verpixeln, Zuschneiden – in sechs Farben und drei Strichstärken.
- Mausmarken: Klickpunkt, Zugweg, angeklicktes Element und gedrückte Tasten als kleine Tastenkappen im Bild – einzeln abschaltbar (*Ansicht → Mausmarken*).
- Zoom (Strg+Mausrad), Großansicht (F11 oder Doppelklick), Bild kopieren (Strg+C).
- **Kurzer Clip je Schritt:** *Bild → Kurzen Clip speichern (GIF)* zeigt den Schritt als kleine Animation – vom Vorher-Bild über die Zwischenbilder bis zum Ergebnis.

**Weitere Funktionen**
- **Abspielen:** Schritte als Diashow.
- **Seitenansicht (Strg+F2):** die Seiten der fertigen Anleitung direkt im Editor – so, wie das PDF sie enthält. Jede Änderung erscheint nach einem Augenblick; ein Klick auf einen Schritt in den Seiten wählt ihn aus.
- **Drucken (Strg+P):** die Anleitung direkt auf dem Drucker ausgeben.
- **Vorschau im Browser:** die fertige Anleitung als HTML ansehen (*Datei → Vorschau im Browser*).
- **Vergleichen:** zwei Aufnahmen desselben Ablaufs gegenüberstellen (F7) – Paarliste, beide Bilder nebeneinander mit hervorgehobenen Bildunterschieden, Zeitunterschiede; als Markdown mit Vorher/Nachher-Bildern speicherbar.
- **Befehlspalette** (Strg+Umschalt+P): jeden Befehl über seinen Namen finden.
- **Reaktionszeiten:** Jeder Schritt zeigt, wie lange die Anwendung gebraucht hat; der Filter „Langsam“ zeigt nur langsame Schritte.
- **Als Markdown kopieren:** einen Schritt (Strg+M) oder alle Schritte (Strg+Umschalt+M) – für Pull Requests, Issues und Wikis.
- **Deutsch und Englisch:** Mit „DE | EN“ über der Schrittliste pflegen Sie Texte in beiden Sprachen – aus einer Aufnahme werden zwei Anleitungen.
- Alles wird automatisch gespeichert; Rückgängig mit Strg+Z.

---

## Bedienungsanleitungen gestalten

Ganz oben in der Schrittliste steht **„Titelseite und Einleitung“**: Dort wird aus den Schritten eine richtige Bedienungsanleitung – mit Vorschau der Titelseite und Gliederung in der Mitte:

- **Titelseite:** Titel, Untertitel, Version, Zielgruppe, Autor oder Firma, **Logo**.
- **Texte:** Einleitung, Voraussetzungen (als Aufzählung) und ein Abschluss.
- **Aufbau:** eigene Titelseite, Inhaltsverzeichnis (im PDF mit Seitenzahlen), Nummerierung als 1.1, 1.2 … oder fortlaufend.
- **Kapitel:** An jedem Schritt lässt sich ein neues Kapitel mit eigener Einleitung beginnen.
- **Hinweiskästen:** Hinweis, Tipp, Achtung, Wichtig und Ergebnis – farbig abgesetzt.
- **Bilder:** Größe (klein bis volle Breite), Anordnung unter oder neben dem Text, Bildausschnitt; je Schritt änderbar oder ganz ausblendbar.
- **Gestaltung:** Akzentfarbe, Anrede „Sie“ oder „du“ in den automatischen Texten, Fußzeile mit Seitenzahl.
- **Vorlagen:** Logo, Farbe, Fußzeile und Aufbau einmal als Vorlage speichern – neue Aufnahmen erhalten den Firmenlook auf Wunsch automatisch.
- **Mehrere Aufnahmen zu einem Handbuch:** weitere Aufnahmen anhängen oder eine Aufnahme direkt fortsetzen; sie erscheinen als eigene Kapitel.
- **Seite:** A4 oder Letter, Hoch- oder Querformat (Word und PDF).
- **Eigene Word-Vorlage:** eine `.dotx` oder `.docx` mit Kopf- und Fußzeile, Schriften, Formatvorlagen und Rändern Ihrer Firma; der Inhalt erscheint an der markierten Stelle (`StepRecorderContent`). Vorlagen mit Makros werden abgelehnt.
- **Sprachen der Anleitung:** Deutsch, Englisch, Französisch, Spanisch, Italienisch oder Niederländisch – die automatischen Schritttexte und alle Überschriften der Anleitung.

---

## Exportformate

| Format | Ergebnis | Geeignet für |
|---|---|---|
| **PDF** | fertiges Dokument mit Deckblatt, Inhaltsverzeichnis und Seitenzahlen | Drucken, Weitergeben, Archivieren |
| **Word (.docx)** | bearbeitbares Dokument mit echten Überschriften | Weiterbearbeiten in Word oder LibreOffice |
| **HTML** | eine einzige Datei, Bilder eingebettet, Bilder per Klick vergrößerbar | E-Mail, Intranet, Ansehen ohne weitere Programme |
| **Markdown** | Text plus Bilderordner `<name>-bilder/`, ASCII-Dateinamen, Hinweiskästen für GitHub/GitLab oder Obsidian, auf Wunsch Kopfdaten (YAML); gleiche Aufnahme ergibt dieselbe Datei | Wikis, Git-Repositories, Docusaurus, MkDocs, Obsidian |
| **GIF** | animierte Kurzfassung: Titelbild, dann jeder Schritt mit Nummer und Überschrift; mit Größenziel (z. B. 10 MB für Pull Requests) | Teams, Chat, Wiki, schnelle Vorschau |
| **Video (MP4)** | dieselbe Bildfolge als H.264-Video, erzeugt mit Windows Media Foundation | Schulungsplattformen, Präsentationen, Chat |
| **KI-Paket** | Protokoll, Schritte, Bilder und Lesehilfe für KI-Assistenten | ChatGPT, Claude & Co. |
| **AutoHotkey** | AutoHotkey-v2-Skript, das den Ablauf wiederholt | Automatisierung wiederkehrender Abläufe |
| **UI-Test** | Testentwurf für FlaUI (C#), pywinauto (Python) oder Playwright (Browser-Schritte), auf Wunsch als Test-KI-Paket | automatisierte Oberflächentests |
| **Support-Paket** | ZIP mit HTML-Anleitung und KI-Paket | Fehler an den Helpdesk geben |

**Für Menschen lesbar:** Beim KI-Paket und bei AutoHotkey gibt es die Option *„Für Menschen lesbar – ausführlich kommentiert“*. Dann wird jede Angabe – auch Systeminfos, Bildschirme, Belegqualität, Koordinaten und Lücken – in Klartext erklärt, jeder Schritt bekommt einen Satz „In Worten“, dazu kommen eine `LIESMICH.md` und Erklärungen in den JSON-Dateien; das Skript erklärt seinen Aufbau und jeden Schritt. Ohne die Option bleibt der Export knapp und spart Tokens, wenn ihn nur eine KI liest.

**Welche Bilder mitkommen:** Im Exportdialog wählen Sie beim KI-Paket das *Vorher-Bild* (*Automatisch*, *Immer*, *Nie*) sowie *Ausschnitt um die Aktion* und *Nachher-Bild*. *Automatisch* (Standard) legt ein Vorher-Bild nur bei, wenn der Bildschirm davor nicht schon im Paket zu sehen ist – sonst verweist das Protokoll auf das Nachher-Bild des vorigen Schritts. Bei Anleitungen (HTML, Markdown, Word, PDF) kann zusätzlich das Bild nach dem Schritt als *Ergebnis* erscheinen (*Nie*, *Bei Änderung*, *Immer*; Standard *Nie*).

Alle Exporte entstehen ohne Zusatzsoftware. Das Programm verschickt nichts – Sie geben die Dateien selbst weiter.

---

## Datenschutz

- **Passwortfelder** werden immer maskiert; im Bild sind die Punkte zu sehen, im Protokoll nur die Anzahl der Zeichen.
- **Privattaste** (Strg+Umschalt+F8): Eingaben werden nur gezählt, nicht gespeichert.
- **Kennwortabfragen im Terminal** (`ssh`, `sudo`, `Read-Host -AsSecureString` …) werden am Text des Terminals erkannt und bis zur Eingabetaste maskiert; die Leiste zeigt „Passwortabfrage erkannt“. Alles andere, was im Terminal getippt wird, muss vor der Weitergabe durchgesehen werden.
- **Ausschlussliste:** Passwortmanager wie KeePass, 1Password oder Bitwarden werden nie aufgezeichnet; die Liste ist erweiterbar.
- **Durchsicht vor dem Export:** Auffällige Eingaben (Schlüssel, IBANs, Kartennummern, Kennwörter in Befehlen) müssen vor der Weitergabe geprüft werden.
- **Schwärzen:** Bereiche im Bild schwärzen oder verpixeln, Texte überall schwärzen, persönliche Werte (z. B. Benutzername) durch Platzhalter ersetzen.
- **Persönliche Daten in Bildern finden:** Die Windows-Texterkennung sucht in allen Bildern nach E-Mail-Adressen, IBANs, Kartennummern, Telefonnummern und Schlüsseln und bietet sie zum Schwärzen an.
- Alles bleibt auf Ihrem Rechner. Das Programm braucht kein Internet und sendet keine Daten (Ausnahme: die abschaltbare, standardmäßig ausgeschaltete Suche nach Updates).

---

## Für KI und Automatisierung

- **KI-Paket:** enthält eine Lesehilfe für die KI, das vollständige Protokoll (`trace.md`), alle Schritte als JSON und die Bilder. Jeder Schritt sagt, wie sicher seine Wirkung belegt ist.
- **MCP-Server:** KI-Agenten können Aufnahmen direkt abfragen:
  ```bash
  SebastiansStepRecorder.exe mcp --root "C:\Tools\Sessions"
  ```
  Beispiel für Claude Code (`.mcp.json`):
  ```json
  { "mcpServers": { "steprecorder": { "command": "C:\\Tools\\SebastiansStepRecorder.exe", "args": ["mcp", "--root", "C:\\Tools\\Sessions"] } } }
  ```
- **AutoHotkey:** Je Klick und Eingabe werden Fensterklasse, Programm, Steuerelement (ClassNN), Koordinaten und Wartesignale festgehalten. Daraus entsteht ein AutoHotkey-v2-Skript oder ein Paket, mit dem ein KI-Assistent das Skript verbessern kann.
- **Kommandozeile:**

  | Aufruf | Wirkung |
  |---|---|
  | `SebastiansStepRecorder.exe` | Startfenster |
  | `SebastiansStepRecorder.exe <Aufnahmeordner>` | Aufnahme im Editor öffnen |
  | `SebastiansStepRecorder.exe record [--scope screen\|app\|region] [--profile …] [--title …]` | Aufnahme sofort starten |
  | `SebastiansStepRecorder.exe record --stop` | laufende Aufnahme beenden |
  | `SebastiansStepRecorder.exe export <Aufnahme> --format pdf\|docx\|html\|md\|gif\|mp4\|ki\|ahk\|flaui\|pywinauto\|playwright [--lang de\|en\|fr\|es\|it\|nl]` | exportieren |
  | `SebastiansStepRecorder.exe compare <A> <B> [--images]` | zwei Aufnahmen vergleichen (mit Bildunterschieden) |
  | `SebastiansStepRecorder.exe list` · `steps <Aufnahme>` | Aufnahmen bzw. Schritte auflisten (`--json`) |
  | `SebastiansStepRecorder.exe testability <Aufnahme> --strict` | Bedienelemente ohne AutomationId melden (Exit 9 für die CI) |
  | `SebastiansStepRecorder.exe --help` | alle Optionen |

  Statt eines Aufnahmepfads geht überall `latest` – die neueste Aufnahme.

---

## Für Software-Entwickler

**Fehler mit allem Kontext.** Der Modus *Fehler nachstellen* hält zusätzlich fest, was das Programm tat: Programmende mit Exit-Code (Absturz nur bei eindeutigen Fehlercodes), „reagiert nicht“, Absturzdaten der Ereignisanzeige, Produktversion, Build-Kennung und – als Zusatzangabe – Git-Zweig und Commit. Bei den Markern „Fehler“ und „Erwartet“ wird der sichtbare Text des Fensters gespeichert (z. B. die Fehlermeldung). Auf Wunsch (*Einstellungen → Entwickler*, ab Werk aus, immer prüfpflichtig): Fehlertexte der Ereignisanzeige (.NET-Stacktraces), Inhalt der Zwischenablage und neue Zeilen ausdrücklich genannter **Protokolldateien** (`record --log app.log`).

**Agenten-Schleife (MCP).** Ein Agent wie Claude Code holt mit `get_failure_context` die Fehlerstelle samt vorigen Schritten, Selektoren, Wartesignalen, Programm-Kontext und Bild in einem Aufruf. Die MCP-Prompts *Fehler analysieren*, *UI-Test schreiben*, *Anleitung schreiben* und *Behebung prüfen* erscheinen als Slash-Befehle. Mit `--allow-requests` kann der Agent um eine Prüfaufnahme bitten und sie mit `compare_sessions` gegen die Fehleraufnahme abgleichen. „Mit KI verbinden“ erzeugt die Einrichtung für Claude Code, VS Code, Cursor und Codex.

**UI-Tests aus Aufnahmen.** *Exportieren → UI-Test* erzeugt einen Testentwurf: FlaUI (C#, xUnit), pywinauto (pytest) oder Playwright (nur Browser-Schritte). Selektoren nach Stabilität (AutomationId vor Name und Typ, Koordinaten nur als gekennzeichneter Notbehelf), Warten auf Fenster statt fester Pausen, Marker „Erwartet“ und „Fehler“ als Prüfungen, Kennwörter nur aus Umgebungsvariablen. Der **Testbarkeits-Bericht** zeigt, welchen Bedienelementen eine AutomationId fehlt – im Editor oder als CI-Schritt (`testability --strict`). Das Programm selbst spielt nichts ab.

**Schneller im Alltag.** *Einstellungen → Entwickler → Im Infobereich bereit bleiben* hält das Programm erreichbar, damit **Strg+Umschalt+F9** jederzeit eine Aufnahme startet (kein Autostart, im Leerlauf keine Erfassung). `steprec.cmd` neben der EXE lässt Konsolen auf das Ende einer Aufnahme warten. Sofort-Bildschirmfoto mit **Strg+Umschalt+F3**, Befehlspalette mit **Strg+Umschalt+P**.

**Doku und Reviews im Repository.** Markdown mit stabilen Namen für saubere Diffs, Terminal-Befehle als Codeblock, „Alle Schritte als Markdown kopieren“, GIF mit Größenziel und der visuelle Vergleich mit Vorher/Nachher-Bildern für Pull Requests.

---

## Systemvoraussetzungen und Hinweise

- **Windows 10 (ab Version 2004) oder Windows 11, 64 Bit** (x64; für Windows on ARM gibt es zusätzlich `SebastiansStepRecorder-arm64.exe`, noch ohne Test auf ARM-Geräten). Keine Installation, kein .NET nötig – alles steckt in der EXE (ca. 76 MB, mit .NET 10).
- **Windows Server und ältere Windows-10-Versionen** (z. B. Windows Server 2019) können eigene Fenster nicht aus Bildschirmfotos ausschließen. Das Programm erkennt das und bietet an, **mit ausgeblendeter Anzeige** aufzunehmen: Leiste und Rahmen bleiben unsichtbar, die Bedienung läuft über Tastenkürzel und das Symbol im Infobereich.
- **Texterkennung** (für die Suche nach persönlichen Daten) nutzt die in Windows eingebaute OCR. Fehlt das Sprachpaket, lässt es sich unter *Einstellungen → Zeit und Sprache → Sprache* nachinstallieren.
- **Wo liegen die Daten?** Ist der Ordner der EXE beschreibbar, liegen Einstellungen und Aufnahmen daneben (Ordner `Sessions`) – ideal für einen USB-Stick. Sonst unter `%APPDATA%\StepRecorder` und `%LOCALAPPDATA%\StepRecorder`. Der Aufnahmeordner ist in den Einstellungen änderbar.
- **Bildschirmaufnahme:** Das Programm nutzt die schnelle Desktop-Duplizierung von Windows (DXGI) und fällt bei Problemen automatisch auf die klassische Aufnahme zurück. Hochformat-Monitore werden klassisch aufgenommen.
- **Bildschirmleser:** Alle Schaltflächen – auch die reinen Symbole – haben Namen für Sprachausgabe, NVDA und JAWS.
- **Updates:** Das Programm sucht nur auf Wunsch nach neuen Versionen (*Einstellungen → Allgemein*); dabei wird ausschließlich die öffentliche Release-Seite abgefragt, ohne Kennung oder persönliche Daten. Heruntergeladen wird nichts automatisch.
- **Eingaben:** Tottasten (^, ´, `), Bildschirmtastatur und Eingaben von Fernwartungs- und Passwort-Werkzeugen werden richtig erkannt. Entfernt Windows die Eingabeüberwachung (z. B. bei hoher Last), setzt das Programm sie sofort neu und vermerkt die mögliche Lücke.
- **Dunkel oder hell:** Das Programm startet im dunklen Design; unter *Einstellungen → Darstellung* lässt sich „Hell“ oder „Wie Windows“ wählen.
- **Sprache der Oberfläche (auch Englisch):** Wie Windows / Deutsch / English (*Einstellungen → Allgemein → Sprache der Oberfläche*); gilt ab dem nächsten Start. Die Texte der Anleitungen lassen sich unabhängig davon auf Deutsch oder Englisch erzeugen.

---

## Häufige Fragen

**Sieht man die Aufnahmeleiste auf den Bildern?**
Nein. Leiste, Rahmen, Menüs und Dialoge des Programms werden von Windows aus den Bildschirmfotos ausgeschlossen. Wo das nicht geht (z. B. Windows Server 2019), nimmt das Programm mit ausgeblendeter Anzeige auf.

**Kann ich eine Aufnahme später erweitern?**
Ja: im Editor unter *Datei → Aufnahme fortsetzen* oder *Aufnahme anhängen* (auch unter „Titelseite und Einleitung“ → „Weitere Aufnahmen“). Einzelne Bilder lassen sich neu aufnehmen, eigene Schritte einfügen.

**Was passiert mit Kennwörtern?**
Eingaben in Passwortfeldern werden nie gespeichert. Für Kennwörter außerhalb von Passwortfeldern (z. B. im Terminal) gibt es die Privattaste Strg+Umschalt+F8.

**Kann ich eine Aufnahme weitergeben, ohne den Editor?**
Ja – entweder als exportierte Datei (PDF, HTML …) oder den ganzen Aufnahmeordner. Wer das Programm hat, öffnet den Ordner unter „Aufnahmen“ → „Öffnen …“ (dort `manifest.json` wählen) oder zieht ihn einfach auf das Startfenster.

---

## Lizenz

[![MIT](https://img.shields.io/badge/Lizenz-MIT-blue.svg)](LICENSE)

Programm und Quellcode stehen unter der **MIT-Lizenz**: Sie dürfen das Programm nutzen, verändern, weitergeben und in eigene Projekte übernehmen – privat und beruflich, auch verkaufen. Bedingung: Der Lizenzhinweis aus [LICENSE](LICENSE) bleibt in Kopien und wesentlichen Teilen des Quellcodes erhalten. Es gibt keine Gewährleistung.

Die Lizenz gilt für Programm und Quellcode – nicht für die Anleitungen, Bilder und Pakete, die Sie mit dem Programm erstellen; die gehören Ihnen. Bestandteile Dritter (die .NET-Laufzeit von Microsoft, WPF UI, MIT-Lizenz) stehen in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

**Anonyme Ausgabe:** `build-anonym.ps1` baut `StepRecorder.exe` ohne Namen des Urhebers, ohne Projektseite und ohne Update-Suche (`dotnet publish src/StepRecorder.App -c Release -p:Anonymous=true`).

© 2026 Sebedai
