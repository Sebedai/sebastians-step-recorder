# Sebastians Step Recorder

**Arbeitsschritte einmal vorführen – daraus entsteht eine fertige Anleitung mit Bildern.**

Sebastians Step Recorder zeichnet auf, was Sie am Windows-PC tun: jeden Klick, jede Eingabe, jede Tastenkombination – samt Bildschirmfoto und dem Namen des angeklickten Bedienelements. Daraus entstehen Bedienungsanleitungen als PDF, Word, HTML, Markdown oder animiertes GIF, ein Paket für KI-Assistenten oder ein AutoHotkey-Skript, das den Ablauf wiederholt.

Eine einzelne Datei, keine Installation, keine Adminrechte, kein Internet. Das Programm ist in seinen eigenen Bildschirmfotos nie zu sehen. **Kostenlos nutzbar** – auch beruflich ([CC BY-ND 4.0](#lizenz)).

**[⬇ Download: SebastiansStepRecorder.exe](https://github.com/Sebedai/sebastians-step-recorder/releases/latest)**

---

## Inhalt

- [Schnellstart](#schnellstart)
- [Die vier Aufnahmemodi](#die-vier-aufnahmemodi)
- [Während der Aufnahme](#während-der-aufnahme)
- [Der Editor](#der-editor)
- [Bedienungsanleitungen gestalten](#bedienungsanleitungen-gestalten)
- [Exportformate](#exportformate)
- [Datenschutz](#datenschutz)
- [Für KI und Automatisierung](#für-ki-und-automatisierung)
- [Systemvoraussetzungen und Hinweise](#systemvoraussetzungen-und-hinweise)
- [Häufige Fragen](#häufige-fragen)
- [Lizenz](#lizenz)

---

## Schnellstart

1. **`SebastiansStepRecorder.exe` herunterladen** und an einen beliebigen Ort legen, z. B. `C:\Tools\` oder einen USB-Stick. Doppelklick startet das Programm.
2. **Modus wählen:** Wofür ist die Aufnahme? Im Zweifel „Anleitung“.
3. **Bereich anklicken:** ganzer Bildschirm (oder einfach die Eingabetaste), eine einzelne Anwendung oder ein selbst aufgezogener Ausschnitt. Die Aufnahme startet sofort.
4. **Vorführen:** ganz normal arbeiten. Zum Beenden **Strg+Umschalt+F10** drücken oder in der Aufnahmeleiste auf „Stopp“ klicken.
5. **Nachbearbeiten und exportieren:** Der Editor öffnet sich mit allen Schritten. Texte anpassen, Bilder markieren, dann **Exportieren** – fertig.

Beim ersten Start warnt Windows SmartScreen eventuell, weil die Datei nicht signiert ist: „Weitere Informationen“ → „Trotzdem ausführen“.

---

## Die vier Aufnahmemodi

Der Modus legt fest, wie genau aufgezeichnet wird. Mehr Detail heißt genauere KI- und Skript-Ausgaben, aber auch größere Aufnahmen.

| Modus | Gedacht für | Detailgrad | Nach dem Stopp |
|---|---|---|---|
| **Anleitung** | Schritt-für-Schritt-Anleitungen für Menschen | **Schlank:** Klicks, Eingaben und Tasten; keine Mauswege; Zusatzbilder nur bei großen Änderungen | Editor öffnet sich |
| **Support-Fall** | Fehler für den Helpdesk nachvollziehbar machen | **Ausführlich:** alle Eingaben inklusive Mauswegen; Zusatzbilder auch bei kleinen Änderungen; erweiterter Programmkontext | Eingaben prüfen, dann entsteht ein ZIP-Paket |
| **KI-Spur** | Abläufe für KI-Assistenten oder AutoHotkey | **Vollständig:** alles; Zusatzbilder bei jeder sichtbaren Änderung; erweiterter Programmkontext | KI-Export öffnet sich |
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

**Diktat:** Statt zu tippen, sprechen Sie Ihre Erklärungen einfach. Jeder Satz wird als Kommentar an der Stelle gespeichert, an der Sie ihn begonnen haben. Die Erkennung läuft offline über die Windows-Spracherkennung; das Mikrofon ist nur offen, solange das Diktat läuft (rotes Mikrofon in der Leiste), und gespeichert wird nur der Text.

**Countdown und Signaltöne:** Auf Wunsch zählt das Programm vor dem Start herunter und gibt kurze Töne bei Start, Stopp, Pause und Markern aus – praktisch, wenn keine Leiste zu sehen ist.

---

## Der Editor

Nach der Aufnahme öffnet sich der Editor: links die Schritte, in der Mitte das Bild, rechts Text und Einstellungen.

**Schritte**
- Überschrift und Anleitungstext direkt bearbeiten (mit Rechtschreibprüfung) – die automatisch erzeugten Texte sind ein guter Anfang.
- Schritte per Ziehen umsortieren, ausblenden oder als Haupt-/Nebenschritt einstufen.
- **Mehrere Schritte** mit Strg+Klick bzw. Umschalt+Klick auswählen und gemeinsam bearbeiten.
- **Eigene Schritte einfügen:** reine Textschritte oder Bildschritte aus Datei, Zwischenablage oder einem neuen Bildschirmfoto.
- **Bild ersetzen oder neu aufnehmen**, wenn sich die Oberfläche geändert hat – das Original lässt sich jederzeit wiederherstellen.
- **Suchen und Ersetzen** (Strg+H) über alle Überschriften, Texte, Hinweise und Kapitel.
- **Automatisch aufgeräumt:** Beim ersten Öffnen blendet der Editor überflüssige Schritte aus – den Klick ins Feld direkt vor der Eingabe, doppelte Klicks auf dasselbe Element, Fensterwechsel hin und sofort zurück und Eingaben, die wieder gelöscht wurden. Nichts wird gelöscht: Strg+Z macht es rückgängig, unter *⋯ → Aufräumen* sehen Sie jeden Vorschlag mit Begründung.
- **Bessere Texte:** Menüs erscheinen als Pfad („Datei › Speichern unter“), Schaltflächen im Menüband mit ihrer Gruppe. Eine **Begriffsliste** in den Einstellungen ersetzt technische Namen durch Ihre eigenen (z. B. „SAP Easy Access=SAP-Startseite“).

**Bilder**
- Vorher-/Nachher-Bild, ganzer Bildschirm, nur das Anwendungsfenster oder ein Ausschnitt um die Aktion (mit kleiner Übersicht des ganzen Bildschirms).
- Zeichenwerkzeuge: Pfeil, Rahmen, Ellipse, Hervorheben, Text, nummerierte Markierungen, Schwärzen, Verpixeln, Zuschneiden – in sechs Farben und drei Strichstärken.
- Automatische Markierungen: Klickpunkt, Zugweg, angeklicktes Element und gedrückte Tasten als kleine Tastenkappen im Bild – einzeln abschaltbar.
- Zoom (Strg+Mausrad), Großansicht (F11 oder Doppelklick), Bild kopieren (Strg+C).
- **Kurzer Clip je Schritt:** *Kopieren → Kurzen Clip speichern (GIF)* zeigt den Schritt als kleine Animation – vom Vorher-Bild über die Zwischenbilder bis zum Ergebnis.

**Weitere Funktionen**
- **Abspielen:** Schritte als Diashow.
- **Vorschau:** die fertige Anleitung im Browser ansehen.
- **Vergleichen:** zwei Aufnahmen desselben Ablaufs gegenüberstellen (F7).
- **Deutsch und Englisch:** Mit „DE | EN“ über dem Bild pflegen Sie Texte in beiden Sprachen – aus einer Aufnahme werden zwei Anleitungen.
- Alles wird automatisch gespeichert; Rückgängig mit Strg+Z.

---

## Bedienungsanleitungen gestalten

Im Reiter **„Dokument“** wird aus den Schritten eine richtige Bedienungsanleitung:

- **Titelseite:** Titel, Untertitel, Version, Zielgruppe, Autor oder Firma, **Logo**.
- **Texte:** Einleitung, Voraussetzungen (als Aufzählung) und ein Abschluss.
- **Aufbau:** Deckblatt, Inhaltsverzeichnis (im PDF mit Seitenzahlen), Nummerierung als 1.1, 1.2 … oder fortlaufend.
- **Kapitel:** An jedem Schritt lässt sich ein neues Kapitel mit eigener Einleitung beginnen.
- **Hinweiskästen:** Hinweis, Tipp, Achtung, Wichtig und Ergebnis – farbig abgesetzt.
- **Bilder:** Größe (klein bis volle Breite), Anordnung unter oder neben dem Text, Bildausschnitt; je Schritt änderbar oder ganz ausblendbar.
- **Gestaltung:** Akzentfarbe, Anrede „Sie“ oder „du“ in den automatischen Texten, Fußzeile mit Seitenzahl.
- **Vorlagen:** Logo, Farbe, Fußzeile und Aufbau einmal als Vorlage speichern – neue Aufnahmen erhalten den Firmenlook auf Wunsch automatisch.
- **Mehrere Aufnahmen zu einem Handbuch:** weitere Aufnahmen anhängen oder eine Aufnahme direkt fortsetzen; sie erscheinen als eigene Kapitel.

---

## Exportformate

| Format | Ergebnis | Geeignet für |
|---|---|---|
| **PDF** | fertiges Dokument mit Deckblatt, Inhaltsverzeichnis und Seitenzahlen | Drucken, Weitergeben, Archivieren |
| **Word (.docx)** | bearbeitbares Dokument mit echten Überschriften | Weiterbearbeiten in Word oder LibreOffice |
| **HTML** | eine einzige Datei, Bilder eingebettet, Bilder per Klick vergrößerbar | E-Mail, Intranet, Ansehen ohne weitere Programme |
| **Markdown** | Text plus Bilderordner | Wikis, Git-Repositories, Confluence, Obsidian |
| **GIF** | animierte Kurzfassung: Titelbild, dann jeder Schritt mit Nummer und Überschrift | Teams, Chat, Wiki, schnelle Vorschau |
| **KI-Paket** | Protokoll, Schritte, Bilder und Lesehilfe für KI-Assistenten | ChatGPT, Claude & Co. |
| **AutoHotkey** | AutoHotkey-v2-Skript, das den Ablauf wiederholt | Automatisierung wiederkehrender Abläufe |
| **Support-Paket** | ZIP mit HTML-Anleitung und KI-Paket | Fehler an den Helpdesk geben |

**Für Menschen lesbar:** Beim KI-Paket und bei AutoHotkey gibt es die Option *„Für Menschen lesbar – ausführlich kommentiert“*. Dann wird jede Angabe – auch Systeminfos, Bildschirme, Belegqualität, Koordinaten und Lücken – in Klartext erklärt, jeder Schritt bekommt einen Satz „In Worten“, dazu kommen eine `LIESMICH.md` und Erklärungen in den JSON-Dateien; das Skript erklärt seinen Aufbau und jeden Schritt. Ohne die Option bleibt der Export knapp und spart Tokens, wenn ihn nur eine KI liest.

Alle Exporte entstehen ohne Zusatzsoftware. Das Programm verschickt nichts – Sie geben die Dateien selbst weiter.

---

## Datenschutz

- **Passwortfelder** werden immer maskiert; im Bild sind die Punkte zu sehen, im Protokoll nur die Anzahl der Zeichen.
- **Privattaste** (Strg+Umschalt+F8): Eingaben werden nur gezählt, nicht gespeichert – z. B. für Kennwörter in Terminals.
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
  | `SebastiansStepRecorder.exe export <Aufnahme> --format pdf\|docx\|html\|md\|gif\|ki\|ahk` | exportieren |
  | `SebastiansStepRecorder.exe compare <A> <B>` | zwei Aufnahmen vergleichen |
  | `SebastiansStepRecorder.exe --help` | alle Optionen |

---

## Systemvoraussetzungen und Hinweise

- **Windows 10 (ab Version 2004) oder Windows 11, 64 Bit.** Keine Installation, kein .NET nötig – alles steckt in der EXE (ca. 73 MB).
- **Windows Server und ältere Windows-10-Versionen** (z. B. Windows Server 2019) können eigene Fenster nicht aus Bildschirmfotos ausschließen. Das Programm erkennt das und bietet an, **mit ausgeblendeter Anzeige** aufzunehmen: Leiste und Rahmen bleiben unsichtbar, die Bedienung läuft über Tastenkürzel und das Symbol im Infobereich.
- **Texterkennung** (für die Suche nach persönlichen Daten) nutzt die in Windows eingebaute OCR. Fehlt das Sprachpaket, lässt es sich unter *Einstellungen → Zeit und Sprache → Sprache* nachinstallieren.
- **Wo liegen die Daten?** Ist der Ordner der EXE beschreibbar, liegen Einstellungen und Aufnahmen daneben (Ordner `Sessions`) – ideal für einen USB-Stick. Sonst unter `%APPDATA%\StepRecorder` und `%LOCALAPPDATA%\StepRecorder`. Der Aufnahmeordner ist in den Einstellungen änderbar.
- **Bildschirmaufnahme:** Das Programm nutzt die schnelle Desktop-Duplizierung von Windows (DXGI) und fällt bei Problemen automatisch auf die klassische Aufnahme zurück. Hochformat-Monitore werden klassisch aufgenommen.
- **Englische Oberfläche:** unter *Einstellungen → Allgemein → Sprache der Oberfläche* (gilt ab dem nächsten Start). Unabhängig davon lassen sich die Texte der Anleitungen auf Deutsch oder Englisch erzeugen.
- **Bildschirmleser:** Alle Schaltflächen – auch die reinen Symbole – haben Namen für Sprachausgabe, NVDA und JAWS.
- **Updates:** Das Programm sucht nur auf Wunsch nach neuen Versionen (*Einstellungen → Allgemein*); dabei wird ausschließlich die öffentliche Release-Seite abgefragt, ohne Kennung oder persönliche Daten. Heruntergeladen wird nichts automatisch.
- **Eingaben:** Tottasten (^, ´, `), Bildschirmtastatur und Eingaben von Fernwartungs- und Passwort-Werkzeugen werden richtig erkannt. Entfernt Windows die Eingabeüberwachung (z. B. bei hoher Last), setzt das Programm sie sofort neu und vermerkt die mögliche Lücke.
- **Dunkel oder hell:** Das Programm startet im dunklen Design; unter *Einstellungen → Darstellung* lässt sich „Hell“ oder „Wie Windows“ wählen.

---

## Häufige Fragen

**Sieht man die Aufnahmeleiste auf den Bildern?**
Nein. Leiste, Rahmen, Menüs und Dialoge des Programms werden von Windows aus den Bildschirmfotos ausgeschlossen. Wo das nicht geht (z. B. Windows Server 2019), nimmt das Programm mit ausgeblendeter Anzeige auf.

**Kann ich eine Aufnahme später erweitern?**
Ja: im Editor unter „Dokument“ → „Aufnahme fortsetzen“ oder „Aufnahme anhängen“. Einzelne Bilder lassen sich neu aufnehmen, eigene Schritte einfügen.

**Was passiert mit Kennwörtern?**
Eingaben in Passwortfeldern werden nie gespeichert. Für Kennwörter außerhalb von Passwortfeldern (z. B. im Terminal) gibt es die Privattaste Strg+Umschalt+F8.

**Kann ich eine Aufnahme weitergeben, ohne den Editor?**
Ja – entweder als exportierte Datei (PDF, HTML …) oder den ganzen Aufnahmeordner. Wer das Programm hat, öffnet den Ordner über „Öffnen“ (dort `manifest.json` wählen) oder zieht ihn einfach auf das Startfenster.

---

## Lizenz

[![CC BY-ND 4.0](https://img.shields.io/badge/Lizenz-CC%20BY--ND%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nd/4.0/deed.de)

Programm und Quellcode stehen unter der Lizenz **[Creative Commons Namensnennung – Keine Bearbeitungen 4.0 International (CC BY-ND 4.0)](https://creativecommons.org/licenses/by-nd/4.0/deed.de)**.

| Erlaubt | Nicht erlaubt |
|---|---|
| das Programm kostenlos nutzen – privat und beruflich | veränderte Fassungen des Programms oder des Quellcodes weitergeben oder veröffentlichen |
| es unverändert weitergeben, auch in Firmen | Teile des Quellcodes in eigenen veröffentlichten Projekten verwenden |
| | den Eindruck erwecken, der Urheber unterstütze eine Weitergabe oder Nutzung |

Bei jeder Weitergabe sind der Urheber (Sebedai), ein Link zu diesem Repository und die Lizenz zu nennen. Die Lizenz gilt für Programm und Quellcode – nicht für die Anleitungen, Bilder und Pakete, die Sie mit dem Programm erstellen; die gehören Ihnen.

Verbindlich ist der Lizenztext in [LICENSE](LICENSE). Bestandteile Dritter (die .NET-Laufzeit von Microsoft, MIT-Lizenz) stehen in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Fehler und Wünsche gerne über [Issues](https://github.com/Sebedai/sebastians-step-recorder/issues).

© 2026 Sebedai
