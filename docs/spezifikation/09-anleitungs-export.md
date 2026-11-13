# 09 – Anleitungs-Export

Status: Entwurf 0.1 · Stand: 2026-11-11

Dieses Dokument legt fest, wie aus einer Sitzung eine Anleitung für Menschen wird: als HTML, Markdown, Word-Dokument oder PDF; dazu kommen PowerPoint, Klick-Tour, Fehlerbericht, GIF und Video ([8a](#8a-powerpoint) bis [8e](#8e-video-mp4)). Es beschreibt den Exportdialog, die Optionen, die Eigenschaften jedes Formats, Vorlagen, die Entschärfung aufgezeichneter Texte, die Prüfung vor dem Export mit dem Regelkatalog der Qualitätsprüfung, das Support-Paket und den Export über die Kommandozeile. Das KI-Paket ist kein Anleitungs-Export und steht in [06](06-ki-export-und-mcp.md).

## Inhalt

1. [Zweck und Abgrenzung](#1-zweck-und-abgrenzung)
2. [Grundsätze](#2-grundsätze)
3. [Exportdialog und Ein-Klick-Export](#3-exportdialog-und-ein-klick-export)
4. [Gemeinsame Optionen](#4-gemeinsame-optionen)
5. [HTML](#5-html)
6. [Markdown](#6-markdown)
7. [DOCX](#7-docx)
8. [PDF](#8-pdf)
9. [Vorlagen und Branding](#9-vorlagen-und-branding)
10. [Sprache der Schritttexte](#10-sprache-der-schritttexte)
11. [Entschärfung aufgezeichneter Texte](#11-entschärfung-aufgezeichneter-texte)
12. [Export-Vorprüfung](#12-export-vorprüfung)
13. [Dateibenennung und Ablage](#13-dateibenennung-und-ablage)
14. [Kommandozeilen-Export](#14-kommandozeilen-export)
15. [Qualitätsziele](#15-qualitätsziele)
16. [Beispiele](#16-beispiele)
17. [Anforderungen](#17-anforderungen)
18. [Offene Punkte](#18-offene-punkte)

## 1 Zweck und Abgrenzung

Der Anleitungs-Export erzeugt aus einer Sitzung ein Dokument, das ein Leser ohne das Programm öffnen kann. Er übernimmt, was der Autor im [Viewer](08-viewer.md) festgelegt hat: Texte, Reihenfolge, Kapitel, Kommentare, Markierungen, Darstellung und Schwärzungen.

| Thema | Steht in |
|---|---|
| Bearbeiten der Sitzung, schnelles Kopieren einzelner Bilder und Schritte | [08 Viewer](08-viewer.md) |
| KI-Paket und MCP-Server | [06 KI-Export und MCP](06-ki-export-und-mcp.md) |
| Schrittarten, Wichtigkeitsstufen, Textvorlagen und Tastennamen je Sprache | [05 Schritterkennung](05-schritterkennung.md) |
| Regeln für Schwärzung, sensible Muster und die Durchsicht der Eingaben | [10 Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md) |
| Datenbestand und `edits.json` | [04 Datenformat](04-datenformat.md) |
| Allgemeine Regeln der Kommandozeile, verwendete Bibliotheken | [02 Architektur](02-architektur.md) |

## 2 Grundsätze

1. **Eine Quelle.** Der Export liest dieselbe Sitzung über dieselbe Leseschicht wie der Viewer und zeichnet Bilder in derselben Reihenfolge ([08](08-viewer.md#71-grundsätze)). Ein exportiertes Bild gleicht dem im Viewer angezeigten.
2. **Schwärzungen sind erzwungen.** Es gibt keine Exportoption, die sie abschaltet.
3. **In sich geschlossen.** Die Ausgabe enthält keine Verweise nach außen und lädt nichts nach. Erzeugt wird sie ohne Netzwerk, ohne Browser und ohne installiertes Office.
4. **Aufgezeichnete Texte sind Daten.** Fenstertitel, Elementnamen und Eingaben werden so ausgegeben, dass sie nie als Auszeichnung, Verweis oder Skript wirken ([Abschnitt 11](#11-entschärfung-aufgezeichneter-texte)).
5. **Ohne das Programm lesbar.** Der Leser braucht nur einen Browser, eine Textverarbeitung oder einen PDF-Betrachter.
6. **Wiederholbar.** Dieselbe Sitzung mit denselben Einstellungen ergibt denselben Inhalt.
7. **Zugänglich.** Jedes Bild hat einen Alternativtext, die Dokumente haben eine Überschriftenstruktur und eine Sprachangabe.

## 3 Exportdialog und Ein-Klick-Export

### 3.1 Exportdialog

Aufruf mit `Strg+E` oder über das Exportmenü im Editor. Das Menü bietet nur Formate an, die das Programm erzeugen kann. Die Oberfläche ist zweisprachig; die Beschriftungen folgen der Programmsprache.

```
┌─ Exportieren ─────────────────────────────────────────────────────────────────────┐
│ Anleitung                │ HTML – eine einzige Datei für den Browser              │
│ ▌HTML        eine Datei  │ Zum Verschicken per E-Mail, für das Intranet …         │
│  Word        bearbeitbar │ Enthält: <Titel>.html – Titel, Einleitung, …           │
│  PDF         Drucken     │ Qualität: 2 Warnungen, 5 Vorschläge     [Ansehen (F8)] │
│  Markdown    Wiki, Git   │ Speichern unter: [C:\...\export\html\…] [Durchsuchen …]│
│  PowerPoint  Folien      │ Sprache: [Deutsch v]        Art: [Standard v]          │
│ Vorführen und melden     │ Umfang:  [Hauptschritte v]  Bildausschnitt: [Ganzer …] │
│  Klick-Tour · GIF ·      │ Nachher-Bild: Nie · Bei Änderung · Immer               │
│  Fehlerbericht           │ ˅ Weitere Optionen                                     │
│ KI und Automatisierung   │   (Fehlt eine Übersetzung, Mausmarken im Bild,         │
│  KI-Paket · AutoHotkey   │    beim KI-Paket Bildgröße und ZIP)                    │
│                                                     [Exportieren]  [Abbrechen]   │
└───────────────────────────────────────────────────────────────────────────────────┘
```

- **Aufbau:** Links stehen die Formate als Liste in drei Gruppen: *Anleitung* (HTML, Word, PDF, Markdown, PowerPoint), *Vorführen und melden* (Klick-Tour, GIF, Fehlerbericht) und *KI und Automatisierung* (KI-Paket, AutoHotkey). Rechts stehen die Beschreibung, das Ziel und nur die Optionen, die zum gewählten Format passen. Seltenes steht unter „Weitere Optionen“; ob der Bereich offen ist, merkt sich das Programm bis zum Beenden. Das Fenster hat eine feste Größe und springt beim Wechsel des Formats nicht; rechts wird bei Bedarf gescrollt. Die Tab-Taste geht von den Formaten über die Optionen zu „Exportieren“ und „Abbrechen“.

- **Vorbelegung:** Der Dialog öffnet mit den Optionen, die für das gewählte Format zuletzt galten ([4.5](#45-gespeicherte-export-einstellungen)). Die Sprache ist die des letzten Exports dieser Aufnahme, sonst die der Anleitungstexte. Beim ersten Mal stammen Darstellung und Ausschnittübersicht aus den Dokument-Einstellungen der Aufnahme.
- **Zielwahl:** „Speichern unter“ zeigt den vollständigen Pfad der Datei (beim KI-Paket und bei AutoHotkey als KI-Paket den des Ordners). „Durchsuchen …“ wählt Ordner und Namen; „Standardort“ kehrt in den Ordner `export/` der Aufnahme zurück. Ein unvollständiger Pfad wird zurückgewiesen. Das Feld „Enthält“ nennt die Dateien, die entstehen.
- **Optionen je Format:** „Art“ gibt es bei HTML, Markdown, Word und PDF ([9.4](#94-art-der-anleitung)). Umfang, Bildausschnitt (in diesem Dokument „Darstellung“) und der Umgang mit fehlenden Übersetzungen gelten für alle Anleitungsformate, „Mausmarken im Bild“ für alle Formate mit Bildern. Das KI-Paket zeigt Detailstufe und Bildgröße ([3.4](#34-ki-paket-im-selben-dialog)), GIF und Video die Dauer je Schritt (das GIF auch ein Größenziel), Markdown Dialekt, Kopfdaten und frühere Form, der UI-Test das Testgerüst, die Klick-Tour den Hinweisring, AutoHotkey die Denkpausen und die Wahl „Als KI-Paket“.
- **Qualität:** Bei Anleitungsformaten zeigt eine Zeile die Zusammenfassung der Qualitätsprüfung ([12.1](#121-qualitätsprüfung-regelkatalog)) – „Qualität: N Warnungen, M Vorschläge“ (Schweregrade der Befunde; „Vorschlag“, damit es nicht mit den Hinweisen der Anleitung verwechselt wird). „Ansehen (F8)“ schließt den Dialog und öffnet die Befundliste im Editor ([08](08-viewer.md#121-qualitätshinweise)). Gibt es keine Befunde, fehlt die Zeile.
- **Durchsicht:** Sind auffällige Eingaben ungeprüft, fragt das Programm schon vor dem Dialog, ob die Durchsicht jetzt geöffnet werden soll; die Ausgabe entsteht erst danach ([12](#12-export-vorprüfung)). Das KI-Paket ist davon ausgenommen ([06](06-ki-export-und-mcp.md), [10](10-datenschutz-und-sicherheit.md)).
- „Exportieren“ ist die Standardschaltfläche. Der häufigste Weg ist damit `Strg+E`, Eingabetaste.
- **Hintergrund, Fortschritt, Abbruch:** Der Export läuft in einer eigenen Leseansicht der Aufnahme im Hintergrund; der Editor ist währenddessen gesperrt. Dauert er länger als 0,7 s, erscheint ein Fortschrittsfenster („<Format> wird erstellt …“, „N von M“ und Phase) mit „Abbrechen“. Gearbeitet wird in einem Arbeitsordner; erst die fertige Ausgabe gelangt an ihr Ziel ([Abschnitt 13](#13-dateibenennung-und-ablage)). Ein Abbruch hinterlässt nichts am Ziel, die Statuszeile meldet ihn.
- **Ergebnisfenster:** Nach dem Export nennt das Fenster „Export erstellt“ den Pfad, die Warnungen und Hinweise des Exports sowie die Schaltflächen aus [3.3](#33-nach-dem-export).
- **Aufnahmeprofil (Soll):** Hat das Profil der Sitzung ein Format mit Vorlage festgelegt ([07](07-aufnahme-bedienung.md#47-aufnahmeprofile)), öffnet sich der Dialog nach dem Stopp von selbst über dem Viewer und ist damit ausgefüllt – solange die Sitzung noch keinen eigenen Export hat. Exportiert wird erst mit „Exportieren“; Vorprüfung und Durchsicht gelten wie immer.
- **Vorschau (Soll)** erzeugt die Ausgabe im Ordner `export/.tmp/` der Sitzung – nie in `%TEMP%` – und öffnet sie im Standardprogramm für das Format. Die Vorschaudatei wird beim Schließen der Sitzung entfernt. Im Dialog fehlt sie noch; die Schaltfläche „Vorschau“ der Kopfleiste im Editor öffnet die Anleitung als HTML im Browser und legt die Datei bei Aufnahmen im Ordner `export/.tmp/vorschau/` ab, bei schreibgeschützten Archiven im Temp-Ordner des Benutzers.
- **Soll, noch nicht im Dialog:** die Wahl benannter Vorlagen ([9](#9-vorlagen-und-branding)), der Anrede, der Seitenoptionen und des Umfangs „Auswahl“ sowie die Zeile „Prüfung“ mit allen Befunden der Vorprüfung.

### 3.2 Ein-Klick-Export

`Strg+Umschalt+E` wiederholt den letzten Export dieser Aufnahme ohne Dialog – im selben Format, mit denselben Optionen und demselben Ziel.

- Gibt es für die Aufnahme noch keinen Export, öffnet sich der Dialog mit dem zuletzt genutzten Format, sonst mit HTML.
- Sind auffällige Eingaben ungeprüft, fragt das Programm wie beim Dialog nach der Durchsicht.
- Fortschritt, Abbruch und Ergebnisfenster verhalten sich wie beim Dialog.
- **Soll:** Meldet die Vorprüfung eine Sperre oder Warnung, öffnet sich stattdessen der Dialog mit dem Befund; Hinweise ohne Handlungsbedarf erscheinen in der Erfolgsmeldung. Bisher wertet der Ein-Klick-Export die Qualitätsprüfung nicht aus.

### 3.3 Nach dem Export

Das Ergebnisfenster „Export erstellt“ zeigt den Pfad und bietet an: **Öffnen**, **Im Ordner zeigen**, **Pfad kopieren** und **Schließen**.

- „Öffnen“ fehlt bei AutoHotkey-Skripten und `.steprec`-Dateien: Ein Skript wird nie durch Öffnen gestartet. Bei Ordnerausgaben öffnet es den Ordner. Fehlt „Öffnen“, ist „Im Ordner zeigen“ die Hauptschaltfläche.
- Die Statuszeile des Editors nennt außerdem den Pfad („Exportiert: …“).
- **Datei kopieren** (zum Einfügen als E-Mail-Anhang) ist Soll.

### 3.4 KI-Paket im selben Dialog

Der Exportdialog bietet das **KI-Paket** als weiteres Ziel an. Statt der Optionen aus [Abschnitt 4](#4-gemeinsame-optionen) zeigt er dann die des KI-Pakets – Detailstufe, Bildprofil, Darstellung, Sprache, Umgang mit ausgeblendeten Schritten – und, bevor etwas geschrieben wird, die Token-Abschätzung: eine Zeile „Geschätzt: ≈ N Tokens (Text, Bilder) · M Bilder“ mit den Hinweisschwellen aus [06](06-ki-export-und-mcp.md#10-token-abschätzung-und-paketteilung). Sie wird im Hintergrund in einer eigenen Leseansicht berechnet, bei jeder Änderung der Optionen nach 150 ms neu, und liest keine Pixel. Daneben steht der Link „Mit KI verbinden …“: Statt eines Pakets liest die KI die Aufnahmen direkt über den MCP-Server ([06](06-ki-export-und-mcp.md)). Inhalt, Optionen und Abschätzung legt [06](06-ki-export-und-mcp.md#31-erzeugen-viewer-und-kommandozeile) fest. Vorprüfung, Fortschritt, Ablage und die Aktionen nach dem Export sind dieselben wie bei den Anleitungsformaten. Auf der Kommandozeile entsteht das KI-Paket mit `export <Sitzung> --format ki` ([Abschnitt 14](#14-kommandozeilen-export)).

### 3.5 Support-Paket

Das Support-Paket ist eine ZIP-Datei zum Weitergeben an jemanden, der helfen soll (Soll). Es entsteht nur aus dem geführten Ablauf im Viewer ([08](08-viewer.md#122-support-paket-geführter-ablauf)), weil dort die Durchsicht stattfindet. Wie es gebaut wird, legt [02](02-architektur.md#support-paket) fest, die Schutzregeln [10](10-datenschutz-und-sicherheit.md#113-support-paket), das KI-Paket darin [06](06-ki-export-und-mcp.md#32-das-ki-paket-im-support-paket); hier stehen Inhalt, Benennung und Ablage.

```
Support-2026-11-11-Editor – notiz.txt.zip
├── Editor – notiz.txt.html          Anleitung als eine Datei
└── Editor – notiz.txt.ki-paket/     KI-Paket
    ├── README_AI.md
    ├── trace.md
    ├── steps.json
    └── …                            Bilder
```

| Bestandteil | Inhalt | Festlegung |
|---|---|---|
| Anleitung | HTML als eine Datei nach [Abschnitt 5](#5-html) | Vorlage „Standard“, Hauptschritte, Darstellung wie im Viewer festgelegt, Sprache der Sitzung. Am Anfang stehen Ziel, erwartetes Ergebnis, Bezug und der Hinweis für den Empfänger ([4.6](#46-ziel-marker-neuaufnahmen-und-platzhalter)). |
| KI-Paket | Ordner `<Titel>.ki-paket/` neben der HTML-Datei, nicht noch einmal gepackt; Aufbau nach [06](06-ki-export-und-mcp.md#32-das-ki-paket-im-support-paket) | Detailstufe „Standard“, Bildprofil „kompatibel“, Sprache der Sitzung; ohne Rohereignisse, Text-Schnappschüsse und PDF-Variante. Es ist dasselbe Paket, das `export --format ki` mit Standardoptionen aus der bereinigten Kopie erzeugt. Der Hinweis für den Empfänger steht nicht darin. |

- **Nur diese beiden Ausgaben.** Die Sitzung selbst – Rohprotokoll, Bildspeicher, `edits.json` – ist nicht im Paket.
- **Aus einer bereinigten Kopie.** Beide Ausgaben entstehen aus einer Kopie der Sitzung, in der Schwärzungen und Verallgemeinerungen endgültig angewendet sind. Eine Gegenprobe sucht darin nach den geschwärzten und verallgemeinerten Inhalten, bevor etwas erzeugt wird; schlägt sie fehl, entsteht kein Paket. Die Kopie liegt während des Erstellens unter `export/.tmp/` und wird danach gelöscht.
- **Nur nach vollständiger Durchsicht.** Jede auffällige Eingabe ist entschieden, die übrigen sind bestätigt – einschließlich der Texte des Autors.
- **Benennung:** `Support-<Datum>-<Titel>.zip`. `<Datum>` ist das Datum der Aufnahme als `JJJJ-MM-TT`. `<Titel>` ist der Titel der Sitzung ohne das Datum und die Uhrzeit, die der automatische Name voranstellt, als gültiger Dateiname nach [Abschnitt 11](#11-entschärfung-aufgezeichneter-texte). Die Dateien im Paket heißen nach denselben Regeln wie einzeln exportiert.
- **Auch der Dateiname ist eine Ausgabe.** Der Titel ist Text des Autors; er steht im Namen so, wie die Leseschicht ihn ausgibt – mit Platzhaltern statt verallgemeinerter Werte und ohne Geschwärztes. Der letzte Schritt des Ablaufs zeigt den Namen, bevor die Datei entsteht, und lässt den Titel darin ändern.
- **Hinweis für den Empfänger:** Er ist als Einleitung der Anleitung gespeichert (`edits.guide.intro`, [04](04-datenformat.md#edits-json)) und steht am Anfang der HTML-Datei – im KI-Paket steht er nicht.
- **Nie überschreiben:** Gibt es eine Datei dieses Namens schon, erhält die neue eine laufende Nummer – anders als bei gewöhnlichen Ausgaben im Ordner `export/`.
- **Ablage:** Standardziel ist der Ordner `export/` der Sitzung; der Ablauf zeigt den Ort und lässt ihn ändern. Die fertige Datei wird in einem Schritt an ihr Ziel gesetzt; danach öffnet der Explorer den Ordner.
- **Gewöhnliches ZIP ohne Kennwort.** Wer das Paket vertraulich übermitteln muss, wählt den Weg dafür selbst.
- **Das Programm versendet nichts.** Es ruft kein Mailprogramm, keinen Teilen-Dialog und keinen Upload auf.
- **Nicht über die Kommandozeile:** `export` kennt kein Format „Support-Paket“, weil die Kommandozeile die Durchsicht nicht zeigen kann. Der Befehl `support` öffnet die Oberfläche für den Ablauf ([07](07-aufnahme-bedienung.md#32-support-aufnahme-über-die-kommandozeile)).
- Ältere Pakete im Ordner `export/` listet der Viewer nach einer neuen Schwärzung wie andere Ausgaben auf ([Abschnitt 13](#13-dateibenennung-und-ablage)).

## 4 Gemeinsame Optionen

Diese Optionen gelten für alle Formate, soweit nicht anders vermerkt. Jede Vorlage belegt sie vor ([Abschnitt 9](#9-vorlagen-und-branding)).

### 4.1 Umfang

| Option | Werte | Standard |
|---|---|---|
| Schritte | Hauptschritte · alle Schritte · Auswahl aus dem Viewer · Bereich von–bis | Hauptschritte |
| Kapitel | als Überschriften ausgeben · ignorieren | als Überschriften |
| Kommentare | einschließen · weglassen | einschließen |
| Hinweis auf nicht aufgezeichnete Sicherheitsdialoge | an · aus | an |
| Ziel der Aufnahme voranstellen (Soll) | an · aus | aus; im Support-Paket an |
| Technischer Anhang (Kann) | an · aus | aus |

Nie enthalten sind ausgeblendete Schritte, verworfene Schritte, geschwärzte Inhalte sowie interne und ausgeblendete Kommentare. Das ist keine Option. Ebenso fehlen Schritte, die der Autor durch eine Neuaufnahme ersetzt hat; an ihrer Stelle steht die neue Fassung ([4.6](#46-ziel-marker-neuaufnahmen-und-platzhalter)).

Geschwärztes erscheint so ([08](08-viewer.md#76-schwärzen-und-verpixeln), [10](10-datenschutz-und-sicherheit.md#10-schwärzungsmodell)):

| Schwärzung | In der Anleitung |
|---|---|
| Bereich im Bild | schwarz gefüllte Fläche |
| Textstelle | `[geschwärzt]`, in englischen Anleitungen `[redacted]` |
| Ganzer Schritt | Platzhalter „Schritt geschwärzt“ bzw. „Step redacted“ an seiner Stelle; ist der Schritt zugleich ausgeblendet, fehlt er |
| Zeitraum | neutraler Hinweis „vom Autor entfernt“, englisch „removed by the author“, an der Stelle des Zeitraums |

Lücken der Aufzeichnung erscheinen in der Anleitung nicht, mit zwei Ausnahmen:

- Ein **vom Autor geschwärzter Zeitraum** erscheint immer als neutraler Hinweis „vom Autor entfernt“ mit Zeitpunkt und Dauer, sofern seine Stelle im gewählten Umfang liegt ([10](10-datenschutz-und-sicherheit.md#105-darstellung)). Dass etwas fehlt, wird nie verschwiegen; was fehlt und warum, steht nirgends.
- Wo ein **Windows-Sicherheitsdialog** oder ein Fenster mit Adminrechten nicht aufgezeichnet werden konnte, steht ein neutraler Hinweis („An dieser Stelle erschien ein Windows-Sicherheitsdialog, der nicht aufgezeichnet werden konnte.“), damit dem Leser kein Schritt fehlt. Der Autor ersetzt ihn im Viewer durch einen eigenen Hinweisschritt oder schaltet die Option ab.

### 4.2 Bilder

| Option | Werte | Standard |
|---|---|---|
| Darstellung | wie im Viewer festgelegt · ganzer Bildschirm · Anwendungsfenster · Ausschnitt um die Aktion | wie im Viewer festgelegt |
| Einzelfestlegungen überschreiben | an · aus (nur wenn eine Darstellung vorgegeben ist) | aus |
| Nachher-Bild | wie im Viewer festgelegt · nie · bei Änderung · immer | wie im Viewer festgelegt (dort Standard: nie) |
| Automatische Markierungen | wie im Viewer · alle an · alle aus | wie im Viewer |
| Bildformat | PNG · JPEG (Qualität 85 bis 95) | PNG |
| Größte Bildbreite | Originalgröße · Wert in Pixeln | Originalgröße |

- **Darstellung:** Die Begriffe und die Regel für den Ausschnitt stehen in [08](08-viewer.md#52-darstellung). Eine vorgegebene Darstellung gilt für alle Schritte ohne eigene Festlegung; mit „Einzelfestlegungen überschreiben“ gilt sie für alle.
- Bei Sitzungen, die nur eine Anwendung oder einen Bereich aufgenommen haben, heißt „ganzer Bildschirm“ „gesamte Aufnahme“ und zeigt alles Gespeicherte.
- **Je Schritt erscheint ein Bild**, das Standardbild seiner Schrittart ([08](08-viewer.md#51-bild)) oder das vom Autor gewählte.
- **Nachher-Bild „bei Änderung“:** Zusätzlich erscheint das Nachher-Bild, wenn das erste Bild das Vorher-Bild ist und sich im gezeigten Bereich mindestens 2 % der Fläche geändert haben oder ein Fenster geöffnet oder geschlossen wurde.
- **Stand der Umsetzung:** Der Exportdialog bietet „Nachher-Bild: Nie · Bei Änderung · Immer“ (Standard „Nie“; HTML, Markdown, Word, PDF – nicht GIF). Das zusätzliche Bild steht unter dem Schrittbild mit der Beschriftung „Ergebnis“; in Markdown heißt es `schritt-001-nachher.png`. Die Änderung wird über den ganzen Bildschirm gemessen. Ist das Schrittbild schon das Nachher-Bild (Tastaturschritte), kommt keins hinzu.
- Schritte ohne Bild (etwa reine Hinweisschritte) erscheinen als Text.

### 4.3 Texte

| Option | Werte | Standard |
|---|---|---|
| Sprache der Schritttexte | Deutsch · Englisch | Sprache der Sitzung |
| Anrede (Soll, nur Deutsch) | Sie · du · neutral | Sie |
| Schrittnummern | fortlaufend · je Kapitel neu | fortlaufend |
| Getippter Text | zeigen · als Beispielwert kennzeichnen (Kann) | zeigen |

Einzelheiten zur Sprache in [Abschnitt 10](#10-sprache-der-schritttexte).

### 4.4 Dokumentrahmen

| Option | Werte | Standard | Formate |
|---|---|---|---|
| Titelblatt | an · aus; Felder Titel, Untertitel, Autor, Datum, Version, Logo | aus bei HTML und Markdown (dort Titelblock), an bei DOCX und PDF | alle |
| Einleitung und Voraussetzungen | Text aus der Sitzungsübersicht, falls vorhanden | an | alle |
| Inhaltsverzeichnis | automatisch (bei Kapiteln oder mehr als 15 Schritten) · immer · nie | automatisch | alle |
| Kopf- und Fußzeile | Titel, Seitenzahl „Seite X von Y“, Datum, freier Text | Titel links, Seitenzahl rechts | DOCX, PDF |
| Seitenformat | A4 · Letter; Hoch · Quer | A4 hoch | DOCX, PDF |
| Ein Schritt je Seite |

**Seitenformat (umgesetzt):** Steht in den Dokument-Einstellungen der Aufnahme (`edits.guide.doc.page` = `a4` oder `letter`, `edits.guide.doc.landscape`; Editor: Reiter „Dokument“, Abschnitt „Seite“) und gehört zu dem, was eine gespeicherte Dokumentvorlage überträgt. Die Kommandozeile überschreibt es für einen einzelnen Export mit `--page` und `--landscape`/`--portrait` ([14](#14-kommandozeilen-export)). A4 ist 595,28 × 841,89 pt bzw. 11906 × 16838 Twips, Letter 612 × 792 pt bzw. 12240 × 15840 Twips; quer werden Breite und Höhe getauscht (Word: `w:orient="landscape"`). Die Ränder bleiben gleich (Word 20 mm, PDF 50 pt). Bildbreiten wachsen mit der Satzspiegelbreite; die Bildhöhe ist auf den Satzspiegel abzüglich Platz für den Schritttext begrenzt, sodass auch quer jedes Bild auf eine Seite passt. Das PDF-Deckblatt verteilt seine Elemente nach der Seitenhöhe. A4 hoch ergibt dieselben Dateien wie vorher. an · aus | aus | DOCX, PDF |
| Zeile „Erstellt mit …“ | an · aus | aus | alle |
| Hinweis auf Platzhalter (Soll) | an · aus | an, sobald die Anleitung Platzhalter enthält ([4.6](#46-ziel-marker-neuaufnahmen-und-platzhalter)) | alle |

**Metadaten:** Titel, Sprache und Erstellungsdatum werden in das Dokument geschrieben. Das Feld **Autor** bleibt leer, solange der Autor es nicht selbst ausfüllt; der Windows-Benutzername, der Rechnername und Pfade des Aufnahmerechners werden nie von selbst eingetragen.

### 4.5 Gespeicherte Export-Einstellungen

Das Programm merkt sich die Optionen im eigenen Zustand ([02](02-architektur.md#konfiguration)), nicht in der Aufnahme. So bleibt eine weitergegebene Aufnahme frei von Ordnernamen des Rechners.

| Stelle | Inhalt |
|---|---|
| je Format | Die zuletzt gewählten Optionen (Art, Umfang, Darstellung, Markierungen, Umgang mit fehlender Übersetzung, Optionen des KI-Pakets, GIF-Dauer, Hinweisring der Klick-Tour, Vortragsnotizen bei PowerPoint) und der zuletzt gewählte Zielordner |
| zuletzt genutztes Format | Das Format, das der Dialog ohne Aufnahmebezug vorschlägt |
| je Aufnahme | Der letzte Export dieser Aufnahme mit allen Optionen, Sprache und Ziel – die Grundlage für `Strg+Umschalt+E` ([3.2](#32-ein-klick-export)) und für die Sprache der Vorbelegung |

- **Ohne Zielpfad und Sprache:** Die Optionen je Format enthalten weder Ziel noch Sprache. Gemerkt wird nur der Ordner.
- **Ordner innerhalb einer Aufnahme** (etwa `export/` einer Aufnahme) werden nie zur Vorgabe für andere Aufnahmen. Für sie gilt wieder der Standardort ([13](#13-dateibenennung-und-ablage)).
- Die Optionen eines Formats gelten für alle Aufnahmen; erst der letzte Export hängt an der Aufnahme.
- **Soll:** Das Objekt `exports` in `edits.json` ([04](04-datenformat.md#edits-json)) mit Vorlage und abweichenden Optionen je Format (`last`, `formats.<Format>.template`, `formats.<Format>.options`; Schlüssel wie die Optionen der Kommandozeile ohne `--`). Unbekannte Schlüssel blieben dann beim Speichern erhalten.
- **Vorgabe aus dem Aufnahmeprofil (Soll):** Ein Profil kann den Export nach dem Stopp vorbelegen. Es nennt dazu unter `afterStop.export` das Format und – wie ein Eintrag oben – Vorlage und Optionen: `{ "format": "pdf", "template": "Schulung", "options": { "after": "changed" } }`. `format` ist `html`, `md`, `docx`, `pdf` oder `ki`; für `ki` gelten die Optionen aus [06](06-ki-export-und-mcp.md#31-erzeugen-viewer-und-kommandozeile). Die Vorlage ist nach ihrem Namen genannt, ein Pfad steht dort nie. Die Sitzung hält die Vorgabe in `manifest.profile` fest ([04](04-datenformat.md#manifest-profile)).

### 4.6 Ziel, Marker, Neuaufnahmen und Platzhalter

Vier Angaben des Autors verändern, was die Anleitung zeigt (alle Soll). Sie stammen aus Aufnahme und Viewer; hier steht, wie sie ausgegeben werden.

**Ziel der Aufnahme.** Mit der Option „Ziel der Aufnahme voranstellen“ ([4.1](#41-umfang)) beginnt die Anleitung nach dem Titel mit einem Block aus bis zu drei Zeilen. Den Wortlaut liefern die Textvorlagen ([05](05-schritterkennung.md#ziel-vorlagen)):

| Angabe | Deutsch | Englisch |
|---|---|---|
| `description` | Ziel: … | Purpose: … |
| `expected` | Erwartetes Ergebnis: … | Expected result: … |
| `reference` | Bezug: … | Reference: … |

- Gezeigt werden die wirksamen Angaben – der Stand aus dem Viewer vor dem der Aufnahme ([04](04-datenformat.md#edits-goal)). Fehlt eine Angabe, entfällt ihre Zeile; fehlen alle, entfällt der Block.
- Der Block steht im Kopf der Anleitung, getrennt von den Schritten. Er gibt wieder, was der Autor zeigen wollte – eine Absicht, keine Beobachtung; ob das erwartete Ergebnis eintrat, zeigen allein die Schritte.
- Für die Texte gelten Durchsicht, Schwärzung, Verallgemeinerung und Entschärfung wie für Kommentare.
- Der **Hinweis für den Empfänger** eines Support-Pakets ([3.5](#35-support-paket)) steht als Einleitung unter dem Block; gespeichert ist er in `edits.guide.intro`.
- Im KI-Paket stehen dieselben Angaben im Kopf von `trace.md`, dort im Protokollstil mit dem Zusatz „Angabe des Autors“ ([06](06-ki-export-und-mcp.md#76-angaben-des-autors-ziel-der-aufnahme-und-marker)).

**Marker.** Marker sind Schritte der Aufzeichnung ([05](05-schritterkennung.md#schrittarten)) und erscheinen wie Kommentare aus der Aufnahme: als nicht nummerierter Absatz mit dem Bild des Augenblicks.

| Typ | In der Anleitung | Satz aus der Textvorlage ([05](05-schritterkennung.md#textvorlagen)) | Form |
|---|---|---|---|
| `error` | ja – Hauptschritt | „Hinweis: Hier tritt der Fehler auf.“ | wie eine Warnung |
| `expected` | ja – Hauptschritt | „Hinweis: So sollte es aussehen.“ | wie ein Hinweis |
| `important` | ja – Hauptschritt | „Wichtig: Beachten Sie diese Stelle.“ | wie ein Hinweis |
| `note` | nur mit „Alle Schritte“ – Nebenschritt | die Bezeichnung des Markers, falls er eine hat | wie ein Hinweis |

- Eine Bezeichnung des Markers hängt die Textvorlage an den Satz an. Ein Kommentar, den der Autor am Marker angelegt hat, steht darunter wie jeder Kommentar.
- Es gilt der wirksame Typ – also der im Viewer geänderte, wenn es einen gibt.
- Wie jeder Schritt lässt sich ein Marker ausblenden; dann fehlt er.

**Neuaufnahmen.** Hat der Autor Schritte neu aufgenommen und ersetzt ([08](08-viewer.md#93-abschnitt-neu-aufnehmen-und-ersetzen)), zeigt die Anleitung nur die neue Fassung.

*Stand der Umsetzung (EXP-160):* Alle Anleitungsformate lesen `GuideSteps`/`Effective` und damit die wirksame Schrittfolge ([05 §16](05-schritterkennung.md#wirksame-schrittfolge)): Die neuen Schritte stehen an der Stelle des ersten ersetzten, fortlaufend nummeriert; ersetzte Schritte und nicht verwendete Neuaufnahmen erscheinen bei keiner Wahl des Umfangs, nach „Ersetzung aufheben“ stehen wieder die früheren Schritte dort. Die Anleitung selbst zeigt keine Nahtstelle; die Qualitätsprüfung meldet sie als Q18 („Vor diesem Schritt passt der Zustand nicht nahtlos.“) am ersten Schritt dahinter, und Q06 bildet über eine Nahtstelle keinen Abstand.

- Maßgeblich ist die wirksame Schrittfolge aus [05](05-schritterkennung.md#wirksame-schrittfolge): Die neuen Schritte stehen an der Stelle des ersten ersetzten, und die Nummerierung läuft fortlaufend durch.
- Ersetzte Schritte und Schritte einer nicht verwendeten Neuaufnahme erscheinen in keiner der vier Ausgaben, auch nicht mit „Alle Schritte“. Dasselbe gilt für verworfene Abschnitte ([07](07-aufnahme-bedienung.md#12-stopp-und-übergang-zum-viewer)).
- Ein Vermerk „neu aufgenommen“ erscheint nicht: Die Anleitung zeigt den Ablauf, wie er sein soll. Wer beide Fassungen braucht, nimmt das KI-Paket in seiner ausführlichsten Detailstufe ([06](06-ki-export-und-mcp.md#77-aufnahmeabschnitte-nahtstellen-und-ersetzte-schritte)).
- Kommentare des Autors, die eine Schrittnummer nennen, passt das Programm nicht an; die Qualitätsprüfung weist darauf hin (Q20).

**Platzhalter für verallgemeinerte Werte.** Regeln aus `edits.substitutions` ([08](08-viewer.md#94-werte-verallgemeinern), [04](04-datenformat.md#edits-substitutions)) wirken in allen vier Formaten. Einen Schalter dafür gibt es nicht – wie bei Schwärzungen; die Schutzregeln stehen in [10](10-datenschutz-und-sicherheit.md#109-verallgemeinerung-von-werten), die Darstellung im KI-Paket in [06](06-ki-export-und-mcp.md#78-verallgemeinerte-werte).

| Stelle | Ausgabe |
|---|---|
| Schritttext, getippter Text, Fenstertitel, Elementnamen, Pfade, Kommentare, Ziel | `{Name}` an der Stelle des Werts; der übrige Text bleibt |
| Bild | gefüllte Fläche mit dem Platzhalter an den Stellen, die der Viewer zeigt – das exportierte Bild gleicht dem angezeigten |
| Alternativtext, Dateiname, Titel und übrige Metadaten | ebenfalls mit Platzhalter; der Wert steht an keiner Stelle der Datei |

| Format | Auszeichnung des Platzhalters |
|---|---|
| HTML | `<var class="placeholder">` |
| Markdown | keine eigene Auszeichnung; die Klammern werden wie jedes Satzzeichen maskiert und erscheinen als `{Name}` |
| DOCX | Zeichenvorlage `SR Platzhalter` |
| PDF | wie in DOCX: kursiv in der Schrift für getippten Text |

- **Platzhalter sind Text.** Der Name stammt aus einer Regel, die auch aus einer fremden Sitzung kommen kann; er wird entschärft wie jeder aufgezeichnete Text ([Abschnitt 11](#11-entschärfung-aufgezeichneter-texte)). Trifft die Leseschicht auf einen unzulässigen Namen, liefert sie an der Stelle `[geschwärzt]` – nie den Wert.
- **Hinweis auf Platzhalter:** Enthält die Anleitung Platzhalter, steht vor dem ersten Schritt ein Satz – „In dieser Anleitung stehen Platzhalter in geschweiften Klammern für Werte, die bei Ihnen anders lauten:“ – mit der Liste der verwendeten Namen. Die Werte selbst nennt er nie. Die Option im Dokumentrahmen schaltet ihn ab ([4.4](#44-dokumentrahmen)).
- Wo eine Regel den Wert im Bild nicht erreichen konnte, meldet das die Vorprüfung als Warnung (Q15); der Export hält deswegen nicht an.
- Geschwärztes, Maskiertes und Ungeprüftes bleibt, was es ist: Ein Platzhalter ersetzt nur, was sonst im Klartext stünde.

## 5 HTML

### 5.1 Eigenschaften

- **Eine Datei**, UTF-8. Alle Bilder sind als `data:`-Adressen eingebettet.
- **Keine Skripte:** kein `<script>`, keine Ereignis-Attribute, keine `javascript:`-Adressen.
- **Keine Verweise nach außen:** keine fremden Schriften, Stylesheets, Bilder oder Rahmen; kein `<iframe>`, `<object>`, `<embed>`, `<form>`.
- **Content Security Policy** als erstes Element im Kopf:

```html
<meta http-equiv="Content-Security-Policy"
      content="default-src 'none'; img-src data:; style-src 'sha256-…'; base-uri 'none'; form-action 'none'">
```

- Das einzige Stylesheet ist eingebettet und über seinen Hashwert zugelassen; `'unsafe-inline'` wird nicht verwendet. Es gibt keine `style`-Attribute; gestaltet wird über Klassen.
- Die Policy ist eine zweite Verteidigungslinie: Selbst wenn ein aufgezeichneter Text trotz [Entschärfung](#11-entschärfung-aufgezeichneter-texte) als Auszeichnung in die Datei geriete, würde der Browser weder ein Skript ausführen noch etwas nachladen.

### 5.2 Aufbau

| Teil | Auszeichnung |
|---|---|
| Titelblock | `<header>` mit `<h1>`, Untertitel, Angaben |
| Inhaltsverzeichnis | `<nav>` mit Sprungmarken |
| Kapitel | `<section>` mit `<h2>` |
| Schritte | `<ol class="steps">`; jeder Schritt ein `<li id="schritt-N">` |
| Schritttext | `<p>`; Elementnamen in `<b>`, Tasten und getippter Text in `<kbd>`; Platzhalter für verallgemeinerte Werte in `<var class="placeholder">` (Soll) |
| Bild | `<figure>` mit `<img alt width height>` |
| Kommentar | `<aside class="note">`, `tip`, `warning` |
| Nachher-Bild, Zusatzangaben | `<details>` mit `<summary>` |

- `width` und `height` stehen an jedem Bild, damit die Seite beim Laden nicht springt.
- Die Seite hat eine einspaltige Lesebreite von höchstens 900 Pixeln und passt sich schmalen Bildschirmen an.
- Bilder skalieren auf die Seitenbreite und sind in Exportauflösung eingebettet; der Leser vergrößert mit dem Browser. Eine Vergrößerung per Klick ohne Skript ist Kann.
- **Druck:** Ein Druck-Stylesheet verhindert Umbrüche innerhalb eines Schritts und blendet das Inhaltsverzeichnis aus.
- **Dunkel:** Die Seite folgt der Einstellung des Lesers (Soll); Bilder bleiben unverändert.
- **Zugänglichkeit:** Überschriften in richtiger Folge, `lang` am Wurzelelement, Alternativtext je Bild (der Schritttext), Kontrast von mindestens 4,5 : 1.

### 5.3 Größe

- Die geschätzte Dateigröße steht im Exportdialog.
- Ab 25 MB weist der Dialog auf Abhilfen hin: JPEG, kleinere Bildbreite, engere Darstellung.
- **Kann:** Variante „HTML mit Bilderordner“ – eine HTML-Datei und ein Ordner mit Bilddateien.

## 6 Markdown

### 6.1 Dateien

- Eine Datei `Titel.md` und ein Ordner `Titel-bilder/` mit den Bilddateien.
- UTF-8 ohne Byte-Order-Mark, Zeilenenden LF, abschließender Zeilenumbruch.
- Bilder heißen `schritt-001.png`, ein zusätzliches Nachher-Bild `schritt-001-nachher.png`. Die Nummer ist die der Anleitung, dreistellig mit führenden Nullen.
- Bildverweise sind relativ und verwenden `/`.
- Datei- und Ordnername enthalten für Markdown standardmäßig nur ASCII-Zeichen, Ziffern und Bindestriche (Umlaute umschrieben), damit sie in Versionsverwaltung und Wikis keine Schwierigkeiten machen.
- **Kann:** Ausgabe als ein ZIP-Archiv; Kopfdaten als YAML-Block am Dateianfang (`--front-matter`, Exportdialog „Kopfdaten (YAML)“): `title`, bei Untertitel `description`, `date` (Tag der Aufnahme, `JJJJ-MM-TT`), `lang`.
- **Stabile Ausgabe (12, Paket 18):** Dieselbe Aufnahme ergibt bytegleiches Markdown und bytegleiche Bilder mit festen Namen – ohne Zeitstempel und Programmversion –, damit ein erneuter Export im Repository nur echte Änderungen zeigt.
- **Frühere Form** (Übergangszeit, `--md-legacy`, Exportdialog „Frühere Form“): Ordner `bilder/`, Dateiname wie der Titel, Überschrift je Schritt, jedes Sonderzeichen maskiert.

### 6.2 Aufbau

| Teil | Markdown |
|---|---|
| Titel | `# Titel` |
| Kapitel | `## Kapitel` |
| Schritte | Nummerierte Liste; das Bild als eingerückter Absatz im Listenpunkt |
| Elementnamen | `**„Name“**` (im fett gesetzten Schritttitel nur die Anführungszeichen) |
| Tasten und getippter Text | Code-Spanne |
| Im Terminal getippter Befehl | Codeblock ```` ```console ```` im Listenpunkt – nur nach der Durchsicht; ungeprüft steht der Platzhalter im Text |
| Kommentar | Zitatblock mit fett gesetzter Kennzeichnung |
| Bild | `![Alternativtext](Titel-bilder/schritt-001.png)` |

- Grundlage ist **CommonMark**. Die Ausgabe enthält kein rohes HTML. **Maskiert wird nur, was Bedeutung hätte:** `` \ ` * _ [ ] < ~ `` überall, `&` vor einer Entität, `![`, am Zeilenanfang `#`, `>`, `+`, `-`, `=`, `|` und „1.“/„1)“. Punkte, Klammern und Bindestriche mitten im Text bleiben unverändert.
- Mehrzeilige Texte: Leerzeile trennt Absätze, sonst harter Zeilenumbruch mit `\` am Zeilenende.
- **Dialekte** (Soll) für Kommentare:

| Dialekt | Kommentar erscheint als |
|---|---|
| Allgemein (Standard, `--md-dialect plain`) | `> **Hinweis:** Text` |
| GitHub/GitLab (`--md-dialect github`) | `> [!NOTE]` und Text in der Folgezeile; entsprechend `[!TIP]`, `[!WARNING]`, `[!IMPORTANT]`; „Ergebnis“ als `[!NOTE]` mit Kennzeichnung im Text |
| Obsidian (`--md-dialect obsidian`) | `> [!note] Hinweis` und Text in der Folgezeile; „Ergebnis“ als `[!success]` |

- Die Liste beginnt mit der richtigen Nummer, auch wenn Kapitelüberschriften sie unterbrechen.
- Erzeugt wird Markdown durch einen eigenen Schreiber (`GuideExport.Markdown.cs`). Die Gegenprobe der Tests prüft heute ohne Markdig: kein rohes HTML, keine Verweise außer den eigenen Bildverweisen, jeder Bildverweis löst auf, bytegleiche Wiederholung. Eine Gegenprobe mit Markdig (Abhängigkeit nur im Testprojekt) bleibt Kann.
- **Kopieren im Editor** (08 §10): „Als Markdown kopieren“ legt einen Schritt als Listenpunkt ohne Bildverweis ab (das Bild käme nicht mit; „Bild kopieren“ legt es zusätzlich ab); „Alle Schritte als Markdown kopieren“ (`Strg+Umschalt+M`) die ganze Anleitung ohne Bilder im zuletzt gewählten Dialekt (Standard GitHub) – für Pull Requests und Issues.

## 7 DOCX

### 7.1 Eigenschaften

- Erzeugt mit einem eigenen Schreiber, ohne installiertes Word und ohne Fremdbibliothek.
- Dateityp `.docx`: keine Makros, keine eingebetteten Objekte, keine externen Bezüge. Verweise entstehen nur aus Links, die der Autor selbst in einen Kommentar geschrieben hat.
- Das Dokument öffnet in Word ab 2016, Word im Web und LibreOffice ab 7 ohne Reparaturhinweis.

### 7.2 Aufbau und Formatvorlagen

Der Inhalt verwendet durchgehend benannte Formatvorlagen. Wer das Dokument in Word weiterbearbeitet, ändert das Aussehen an einer Stelle.

| Inhalt | Formatvorlage | Anmerkung |
|---|---|---|
| Titel, Untertitel | `Title`, `Subtitle` | |
| Kapitel | `Heading 1` | Unterkapitel `Heading 2` |
| Schritttext | `SR Schritt` | Absatzvorlage mit echter Word-Nummerierung; bleibt mit dem folgenden Bild auf einer Seite |
| Bild | `SR Bild` | Bild in der Zeile, Alternativtext gesetzt |
| Kommentar | `SR Hinweis`, `SR Tipp`, `SR Warnung` | Absatz mit Rand und Schattierung |
| Taste | `SR Taste` | Zeichenvorlage |
| Getippter Text | `SR Eingabe` | Zeichenvorlage, dicktengleiche Schrift |
| Platzhalter (Soll) | `SR Platzhalter` | Zeichenvorlage für verallgemeinerte Werte ([4.6](#46-ziel-marker-neuaufnahmen-und-platzhalter)) |
| Einleitung, übriger Text | `Normal` | |

- **Nummerierung:** Schritte sind über eine Listendefinition nummeriert, nicht über getippte Zahlen. Löscht oder verschiebt der Leser in Word einen Schritt, stimmt die Nummerierung weiter.
- **Bilder:** auf die Satzspiegelbreite eingepasst, nie über ihre natürliche Größe hinaus vergrößert, und so begrenzt, dass Schritttext und Bild auf eine Seite passen.
- **Inhaltsverzeichnis:** ein echtes Word-Feld `TOC \o "1-2" \h \z \u` in einem Inhaltsverzeichnis-Block, bereits mit Einträgen gefüllt. Die Einträge sind Verweise auf Lesezeichen (`_Toc…`) an den Überschriften und lassen sich anklicken. Die Seitenzahlen ergänzt Word mit „Felder aktualisieren“ (`F9`) oder beim Drucken; das Dokument verlangt das beim Öffnen nicht (kein `updateFields`, kein Schmutz-Kennzeichen), damit Word keine Rückfrage stellt. Die Formatvorlagen `TOC Heading`, `toc 1`, `toc 2` und `Hyperlink` sind angelegt.
- **Sprache:** Die Dokumentsprache ist gesetzt, damit Rechtschreibprüfung und Silbentrennung stimmen.

### 7.3 Eigene Formatvorlage

Der Autor kann eine eigene Word-Vorlage (`.dotx` oder `.docx`) angeben, etwa mit Firmenlayout.

- Der Export übernimmt aus ihr Formatvorlagen, Design, Schriften, Seiteneinrichtung, Kopf- und Fußzeilen.
- Der Inhalt wird an der Stelle eingefügt, die mit dem Inhaltssteuerelement oder der Textmarke `StepRecorderContent` gekennzeichnet ist; fehlt beides, am Ende.
- Inhaltssteuerelemente mit den Kennungen `SR.Title`, `SR.Subtitle`, `SR.Author`, `SR.Date`, `SR.Version` werden gefüllt.
- Formatvorlagen werden über ihre Kennung gesucht, dann über ihren Namen. Fehlt eine `SR`-Vorlage, wird sie mit den Standardwerten angelegt.
- Die mitgelieferte Standardvorlage lässt sich als Datei speichern und als Ausgangspunkt anpassen.
- Enthält die Vorlage Makros (`.dotm`), wird sie abgelehnt.

**Umgesetzt** (eigener Schreiber, ohne Word; `SimpleDocx.Template.cs`):

- **Wahl und Ablage:** im Editor, Reiter „Dokument“, Abschnitt „Seite (Word und PDF)“ → „Word-Vorlage wählen …“ / „Entfernen“. Die Datei (höchstens 5 MB) liegt Base64-kodiert in den Dokument-Einstellungen (`edits.guide.doc.wordTemplate`, Anzeigename `wordTemplateName`) – wie das Logo – und reist mit gespeicherten Dokumentvorlagen und dem Einstellungsaustausch; beim Einlesen wird sie erneut geprüft.
- **Abgelehnt** werden beim Wählen, beim Einlesen und beim Export: Dateien, die keine Word-Datei sind, `.dotm`/`.docm` und jede Datei mit Makroteil (`vbaProject`, Inhaltstyp `macroEnabled`) sowie ActiveX-Steuerelemente. Der Export bricht dann mit einer Meldung ab.
- **Ergebnis:** Grundlage ist die Vorlage selbst; übernommen werden alle ihre Teile (Formatvorlagen, Design, Schriftentabelle, Einstellungen, Kopf- und Fußzeilen, letzter Abschnitt mit Seitengröße und Rändern). Der Hauptteil erhält den Inhaltstyp eines Dokuments. Entfernt werden der Verweis auf die Vorlagendatei (`w:attachedTemplate`) und Verweise nach außen in den Beziehungen der Einstellungen. Die Dokumentangaben (Titel, Programm) schreibt der Export neu.
- **Stelle:** Ein Inhaltssteuerelement `StepRecorderContent` (Kennung oder Titel) wird durch den Inhalt ersetzt; bei der Textmarke `StepRecorderContent` steht der Inhalt hinter ihrem Absatz; fehlt beides, vor dem letzten Abschnittsende.
- **Felder:** `SR.Title`, `SR.Subtitle`, `SR.Author`, `SR.Date`, `SR.Version` – im Hauptteil sowie in Kopf- und Fußzeilen – erhalten den Text; das Format des ersten Laufs bleibt, die Platzhalteranzeige entfällt. Hat die Vorlage ein Feld `SR.Title`, bringt sie Titelblock bzw. Deckblatt selbst mit, und der Export setzt keinen eigenen Titelblock (Logo, Titel, Angaben) davor; sonst steht der Titelblock am Anfang des Inhalts.
- **Formatvorlagen:** Jede Vorlage, die der Inhalt nutzt (`Title`, `Subtitle`, `Heading 1–3`, `TOC Heading`, `toc 1/2`, `Hyperlink`, `List Bullet`, `Note`), wird über ihre Kennung, dann über ihren Namen gesucht – so trifft „heading 1“ auch die deutsche Kennung `berschrift1`; `Normal` fällt notfalls auf die Standard-Absatzvorlage. Fehlende werden mit den Standardwerten angelegt, ihre Bezüge (`basedOn`, `next`) auf die Kennungen der Vorlage umgeschrieben. Die Dokumentsprache wird ergänzt, wenn die Vorlage keine festlegt.
- **Seite:** Mit Word-Vorlage gilt deren Seiteneinrichtung, nicht das Seitenformat der Dokument-Einstellungen; Bildbreiten und Tabulatoren richten sich nach ihrem Satzspiegel. Bilder, Textmarken und Zeichnungskennungen erhalten eigene, mit der Vorlage nicht kollidierende Namen und Nummern (`media/sr-image…`).
- **Noch nicht:** die eigenen `SR`-Formatvorlagen aus 7.2 (der Inhalt nutzt weiter die Vorlagen oben) und das Speichern der Standardvorlage als Datei.

## 8 PDF

### 8.1 Eigenschaften

- Erzeugt durch einen eigenen PDF-Schreiber mit eigenem Seitenlayout – ohne Browser, ohne Word, ohne Druckertreiber und ohne Fremdbibliothek.
- **Echter Text:** Der Text ist markierbar, durchsuchbar und kopierbar. Nur Screenshots sind Bilder.
- **Schriften und Unicode:** Text läuft in Helvetica (WinAnsi), solange alle Zeichen dort vorkommen; dann wird nichts eingebettet. Kommt ein anderes Zeichen vor – etwa Kyrillisch, Griechisch, Pfeile oder Häkchen –, setzt der Schreiber diesen Text in eine eingebettete **Teilmenge** von Segoe UI (fett: Segoe UI Bold, Symbole: Segoe UI Symbol; Rückfall Arial, Tahoma) als Type0-Schrift mit `Identity-H` und `ToUnicode`-Tabelle. Kopieren und Suchen funktionieren damit auch für diese Zeichen. Eingebettet sind nur die benutzten Glyphen. Die Schriften stammen vom Rechner, der exportiert; ihre Einbettungsrechte (`fsType`) werden beachtet.
- **Grenze:** Schriften mit Zeichenformung oder Schreibrichtung (CJK, Arabisch, Hebräisch, Thai) und Emoji sind nicht umgesetzt. Ihre Zeichen erscheinen als „?“, und der Export meldet genau eine Warnung dazu; er bricht nicht ab.
- **Verweise und Lesezeichen:** Das Inhaltsverzeichnis ist klickbar (Link-Anmerkungen auf die Seite des Eintrags). Die Lesezeichenleiste zeigt Kapitel und Schritte; Kapitel sind eingeklappt. Titel stehen als UTF-16.
- **Dokumentangaben:** `/Lang` ist gesetzt. Objektnummern werden beim Schreiben vergeben.
- **Metadaten:** Titel, Autor (falls angegeben), Sprache, erzeugendes Programm, Erstellungsdatum.

### 8.2 Seitenlayout

| Regel | Festlegung |
|---|---|
| Seite | A4 oder Letter, hoch oder quer ([4.4](#44-dokumentrahmen)); Ränder 50 pt (rund 18 mm) |
| Schritt | Nummer, Text, Bild, Kommentare bilden einen Block |
| Seitenumbruch | Ein Bild wird nie geteilt. Passt ein Schritt nicht mehr auf die Seite, beginnt er auf der nächsten. Schritttext und Bild bleiben zusammen. |
| Bildgröße | Auf die Satzspiegelbreite eingepasst, nie über die natürliche Größe vergrößert, höchstens so hoch wie der Satzspiegel abzüglich des Schritttexts |
| Kopf- und Fußzeile | Titel und „Seite X von Y“; das Layout wird dafür zweimal durchlaufen |
| Titelblatt | Eigene Seite mit Titel, Untertitel, Angaben, Logo |
| Inhaltsverzeichnis | Mit Seitenzahlen; Einträge sind Sprungziele |
| Kommentare | Kasten mit Kennzeichnung; wird bei Platzmangel zusammen mit seinem Schritt umbrochen |

- **Bilder** werden verlustfrei oder als JPEG eingebettet, in Originalauflösung. Eine Obergrenze für die Auflösung ist einstellbar; Standard ist keine, weil kleine Schrift in Screenshots sonst unleserlich wird.
- **Verweise:** Nur Links aus Kommentaren des Autors werden anklickbar, und nur mit den Schemata `http`, `https` und `mailto`.
- **Kann:** PDF/A und Strukturmarkierung für Bildschirmleser ([Abschnitt 18](#18-offene-punkte)). Die Lesezeichenleiste ist umgesetzt.

## 8a PowerPoint

Format `pptx`: ein Foliensatz im Format 16:9, den PowerPoint und LibreOffice öffnen. Er entsteht wie die anderen Formate aus den Texten und Bildern des Viewers ([Abschnitt 2](#2-grundsätze)); Schwärzungen, Verallgemeinerungen und Entschärfung gelten unverändert.

- **Erzeugung:** durch einen eigenen Schreiber, ohne installiertes PowerPoint. Das Paket enthält ein vollständiges Theme. Die Zeitstempel sind fest: Zwei Exporte mit gleichen Einstellungen sind bytegleich.
- **Aufbau:** Titelfolie (Titel, Untertitel, Angaben, Logo) · „Über diese Anleitung“ mit Einleitung und Voraussetzungen, falls vorhanden · eine Folie je Kapitel · je Schritt eine Folie · Abschlussfolie.
- **Schrittfolie:** Nummernmarke, Titel als echter Titelplatzhalter (damit Gliederungsansicht und Bildschirmleser ihn finden), Text links, bis zu zwei Hinweiskästen unter dem Text, das Bild eingepasst rechts. Weitere Hinweise hängen am Text. Die Fußzeile zeigt den Fußzeilentext oder den Titel; Akzentfarbe und Logo folgen den Dokument-Einstellungen. Die Schriftgröße wird so gewählt, dass Titel und Text in ihren Rahmen passen.
- **Vortragsnotizen:** Jede Folie trägt den vollen Text mit allen Hinweisen in ihren Notizen. Eine Option schaltet die Notizen ab.
- Die Art der Anleitung ([9.4](#94-art-der-anleitung)) gilt für PowerPoint nicht.

## 8b Klick-Tour

Format `tour`: eine HTML-Datei, in der der Leser die Schritte durchklickt wie in einer Vorführung. Sie ist in sich geschlossen wie die HTML-Anleitung ([5.1](#51-eigenschaften)) und enthält **kein Skript**.

- **Folien:** Jeder Schritt ist ein Abschnitt, der über das Sprungziel in der Adresse (`:target`) sichtbar wird. Eine Startseite zeigt Titel, Schrittzahl, Einleitung und Voraussetzungen, eine Endseite den Abschluss mit „Von vorn“ und „Übersicht“.
- **Klickfläche:** Über dem Ziel des Schritts liegt ein Verweis; ein Klick auf die markierte Stelle führt zum nächsten Schritt. Ihre Lage in Prozent des Bildes ergibt sich aus Bildausschnitt und Ziel des Schritts. Dazu gibt es „Zurück“ und „Weiter“ und die Zugriffstasten `p` und `n`. In den Bildern sind keine Mausmarken eingezeichnet, weil die Klickfläche die Stelle zeigt.
- **Hinweisring:** Der Ring um die Klickfläche pulsiert immer, nur beim Überfahren und bei Fokus (Standard) oder nie. Die Wahl steht im Exportdialog.
- **Ohne Skript:** Alle Maße, auch die der Klickflächen, stehen im Stilblock; es gibt kein `<script>`. Die Content Security Policy sperrt alles außer eingebetteten Bildern und dem Stilblock.
- **Druck:** Die Druckansicht zeigt alle Schritte untereinander, ohne Klickflächen und Schaltflächen.
- Ab 25 MB weist der Export darauf hin, dass sich die Darstellung „Ausschnitt um die Aktion“ oder weniger Schritte anbieten.

## 8c Fehlerbericht

Format `bugreport`: ein Markdown-Text für Ticket oder Issue und ein Ordner `bilder/` mit den Bildern der Schritte. Er richtet sich an jemanden, der den Fehler nachstellen soll.

| Abschnitt | Inhalt |
|---|---|
| Zusammenfassung | Das Ziel der Aufnahme ([4.6](#46-ziel-marker-neuaufnahmen-und-platzhalter)), sonst die Einleitung; fehlt beides, steht dort „(bitte ergänzen)“ |
| Umgebung | Windows-Ausgabe und -Version, Sprache, Monitore (Größe, Skalierung), beteiligte Programme mit Version, Aufnahmezeitpunkt und Programmversion |
| Schritte zum Nachstellen | Nummerierte Schritte mit Text, Hinweisen und Bild; Marker vom Typ „Fehler“ und „Erwartet“ stehen nicht hier |
| Erwartetes Ergebnis | Die Angabe „Erwartetes Ergebnis“ des Ziels und die Marker vom Typ `expected` mit ihrem Zeitpunkt |
| Tatsächliches Ergebnis | Die Marker vom Typ `error` mit ihrem Zeitpunkt |
| Anhänge | Die Liste der Bilddateien |

- **Nie enthalten:** Programmpfade, Kommandozeilen und Prozessnummern. Die Umgebung nennt Programme nur mit Prozessname, Produkt und Version.
- **Harte Durchsichtspflicht:** Solange eine auffällige Eingabe ungeprüft ist, entsteht kein Fehlerbericht – auch nicht über die Kommandozeile, wo bei den anderen Formaten `{unreviewed:N}` erscheint.
- **Ohne Angaben:** Fehlt das erwartete oder das tatsächliche Ergebnis, steht „(bitte ergänzen)“, damit der Autor es im Ticket nachträgt.
- **Zwischenablage:** Im Editor kopiert „Kopieren → Als Fehlerbericht“ den Text ohne Bildverweise in die Zwischenablage ([08](08-viewer.md#102-kopierbefehle)).
- Dateinamen und Ordner: [Abschnitt 13](#13-dateibenennung-und-ablage).

## 8d GIF

Format `gif`: ein animiertes Bild, das die Schritte nacheinander zeigt – für Wiki, Chat und E-Mail.

- Ein Titelbild, dann je Schritt ein Bild mit Nummer und Überschrift; ohne Anleitungstexte. Die Animation läuft in einer Schleife.
- Die Dauer je Schritt wählt der Dialog: 1,5, 2,5 (Standard) oder 4 Sekunden.
- **Größenziel** (12, Paket 18; Dialog: ohne, 5, 10 oder 25 MB; Kommandozeile `--gif-max-mb <n>`): Liegt das GIF darüber, wird erst die Breite schrittweise bis 480 Pixel verringert, dann nur jeder zweite, dritte … Schritt gezeigt. Das Ergebnis meldet die Verkleinerung als Hinweis, ein verfehltes Ziel als Warnung.
- Umfang, Darstellung und Markierungen gelten wie bei den Anleitungsformaten.

## 8e Video (MP4)

Format `mp4`: dieselbe Bildfolge wie das GIF ([8d](#8d-gif)) als H.264-Video im MP4-Container – schärfer und kleiner als das GIF, mit Anhalten und Spulen; für Intranet, Lernplattform, Teams oder Videoportale.

- **Kein Bildschirmvideo** ([00 §4](00-ueberblick.md#4-nicht-ziele)): Das Video entsteht nachträglich aus den Schrittbildern der Aufnahme – Titelbild, dann je Schritt Bild mit Nummer und Überschrift, ohne Anleitungstexte und ohne Ton. Es läuft einmal durch (keine Schleife).
- **Dauer je Schritt** wie beim GIF: 1,5, 2,5 (Standard) oder 4 Sekunden; das Titelbild steht mindestens 1,5 Sekunden. Kommandozeile: `--seconds <0.5–20>` (gilt auch für das GIF). Ein Größenziel gibt es nicht.
- **Bild:** bis 1600 Pixel breit, 15 Bilder je Sekunde (jedes Schrittbild wird wiederholt, bis seine Dauer erreicht ist), Breite und Höhe gerade (H.264), höchstens 1920 Pixel breit und 1920 × 1088 Bildpunkte. Qualitätsgesteuerte Kodierung, Schlüsselbild alle fünf Sekunden.
- **Technik:** Windows Media Foundation (Sink Writer, Software-Encoder für H.264) über eigene Aufrufe – keine fremde Bibliothek, kein ffmpeg. Fehlt Media Foundation oder der H.264-Encoder (Windows N ohne „Media Feature Pack“), bricht der Export mit einer verständlichen Meldung ab; die Kommandozeile endet mit Exit-Code 7 („Voraussetzung fehlt“).
- Umfang, Darstellung und Markierungen gelten wie bei den Anleitungsformaten; Datei `<Titel>.mp4` im Unterordner `video/` ([13](#13-dateibenennung-und-ablage)).

## 9 Vorlagen und Branding

Eine **Vorlage** ist ein benannter Satz aller Exportoptionen samt Gestaltung. Sie gilt für alle vier Formate. Umgesetzt sind bisher die **Dokument-Einstellungen** der Aufnahme (Deckblatt, Inhaltsverzeichnis, Nummerierung, Bildgröße und -anordnung, Akzentfarbe, Logo, Fußzeile, Seitenformat, Word-Vorlage; `edits.guide.doc`), die der Editor als Vorlage speichern und anwenden kann und die sich über den Einstellungsaustausch weitergeben lassen, und die **Art der Anleitung** ([9.4](#94-art-der-anleitung)). Die mitgelieferten Vorlagen aus 9.1 und die Wahl einer Vorlage im Exportdialog sind Soll.

### 9.1 Mitgelieferte Vorlagen

| Vorlage | Gedacht für | Kennzeichen |
|---|---|---|
| Standard | Allgemeine Anleitungen | Hauptschritte, Darstellung wie im Viewer, ein Bild je Schritt, Kommentare an |
| Kompakt | Kurzanleitungen, Wissensdatenbanken | Ausschnitt um die Aktion, kleinere Bilder, kein Titelblatt, kein Inhaltsverzeichnis |
| Schulung | Unterlagen zum Durcharbeiten | Anwendungsfenster, große Bilder, Nachher-Bild bei Änderung, ein Schritt je Seite in DOCX und PDF |
| Ticket | Fehlerberichte, Anfragen an den Support | Alle Schritte statt nur Hauptschritte, ganzer Bildschirm, technischer Anhang, kein Titelblatt |

### 9.2 Eigene Vorlagen

- „Als Vorlage speichern“ im Exportdialog sichert die aktuellen Einstellungen unter einem Namen.
- Vorlagen liegen als lesbare Dateien beim Programm, portabel wie die Einstellungen ([02](02-architektur.md)). Zugehörige Dateien (Logo, Word-Vorlage) werden in den Ordner der Vorlage kopiert.
- Vorlagen lassen sich als Datei weitergeben und einlesen (Kann). Beim Einlesen zeigt das Programm, was die Vorlage enthält.
- **Vorlagen enthalten nie ausführbaren Inhalt:** keine Skripte, keine Makros, keine Verweise nach außen.

### 9.3 Branding

| Element | Festlegung |
|---|---|
| Logo | PNG, erscheint auf Titelblatt bzw. im Titelblock und wahlweise in der Kopfzeile |
| Akzentfarbe | Für Überschriften, Schrittnummern und Kommentarkästen |
| Schrift | Für PDF aus den mitgelieferten Schriften; für DOCX aus der Word-Vorlage; HTML nutzt Systemschriften |
| Firmenname, Fußzeilentext | Freier Text, etwa „Vertraulich“ |
| Word-Vorlage | Siehe [7.3](#73-eigene-formatvorlage) |
| Eigenes Stylesheet für HTML (Kann) | In Version 1 nur über die vorgesehenen Stellgrößen Akzentfarbe, Schriftfamilie und Lesebreite |

Die Stilvorlage für Markierungen in den Bildern gehört zur Sitzung und wird im Viewer gewählt ([08](08-viewer.md#73-eigenschaften-und-stilvorlagen)); der Export übernimmt sie.

### 9.4 Art der Anleitung

Die Art wählt im Exportdialog, wie viel eine Anleitung enthält. Sie gilt für HTML, Markdown, Word und PDF und wirkt nur über Optionssätze auf die Dokument-Einstellungen; es gibt keine eigenen Ausgabewege.

| Art | Gedacht für | Kennzeichen |
|---|---|---|
| Standard | Die ausführliche Anleitung | Es gilt, was die Dokument-Einstellungen festlegen |
| Kurzfassung | Eine knappe Übersicht der Schritte | Ohne Bilder, ohne Deckblatt und Inhaltsverzeichnis, kompakte Schritte, fortlaufend nummeriert (nicht je Kapitel) |
| Checkliste | Zum Ausdrucken und Abhaken | Wie die Kurzfassung, mit einem Kästchen je Schritt |

- Auf der Kommandozeile heißt die Option `--template standard|short|checklist` ([14](#14-kommandozeilen-export)); `kurz`, `kurzfassung`, `kompakt` und `checkliste` werden ebenfalls erkannt.
- **Dateiname:** Kurzfassung und Checkliste hängen „ – Kurzfassung“ bzw. „ – Checkliste“ (englisch „ – short“, „ – checklist“) an den Titel, damit die Standardanleitung nicht überschrieben wird.
- Für PowerPoint, Klick-Tour, Fehlerbericht, GIF, Video, KI-Paket und AutoHotkey gibt es keine Art.

## 10 Sprache der Schritttexte

- Automatische Schritttexte entstehen beim Export aus den Textvorlagen im Anleitungsstil, in der gewählten Sprache ([05](05-schritterkennung.md#textvorlagen)). Eine Sitzung lässt sich so auf Deutsch und auf Englisch ausgeben, ohne neu aufzunehmen.
- **Sprachen der Ausgabe:** Deutsch (`de`), Englisch (`en`), Französisch (`fr`), Spanisch (`es`), Italienisch (`it`) und Niederländisch (`nl`). Wählbar im Exportdialog („Sprache“), über `export --lang de|en|fr|es|it|nl` und `ExportRequest.Language`; eine andere Angabe weist die Kommandozeile zurück. In der gewählten Sprache erscheinen die automatischen Schritttexte und alle **Rahmentexte** der Anleitung: Überschriften („Voraussetzungen“, „Über diese Anleitung“, „Inhalt“, „Abschluss“), Kopfdaten („Stand“, „Autor“, „Umfang: 12 Schritte“), Kennzeichen der Hinweiskästen („Hinweis:“, „Tipp:“ …), „Ergebnis“, Navigation der Klick-Tour, Abschnitte des Fehlerberichts, die Sprachkennung von HTML, DOCX, PDF und PPTX sowie der Zusatz im Dateinamen („ – Checkliste“). Die Oberfläche des Programms, der Editor (Umschalter DE/EN), Meldungen und Prüfbefunde bleiben Deutsch oder Englisch; KI-Paket, AutoHotkey-Skript, UI-Test-Entwürfe und MCP verwenden für weitere Sprachen ihre englischen Rahmentexte, enthalten aber die Schritttexte der gewählten Sprache. Eigene Texte des Autors für eine weitere Sprache liegen wie die englischen unter `text.<sprache>` bzw. `guide.i18n.<sprache>` ([04](04-datenformat.md#edits-json)); fehlen sie, gilt der automatische Text dieser Sprache, bei „Text der anderen Sprache“ der englische Autorentext, sonst der deutsche.
- **Eigene Texte des Autors** liegen in der Sprache vor, in der sie geschrieben wurden; sie werden nicht übersetzt. Fehlt ein eigener Text in der Exportsprache, gilt dort der automatische Text. Die Qualitätsprüfung meldet die betroffenen Schritte und Dokumenttexte (Q13). Der Exportdialog fragt dazu „Fehlt eine Übersetzung“ und bietet an:
  - den automatischen Text der Exportsprache verwenden (Standard),
  - stattdessen den eigenen Text der anderen Sprache unverändert übernehmen.

  Wer übersetzen will, bricht ab und übersetzt im Viewer. Die Wahl gilt für diesen Export; die Aufnahme bleibt unverändert. Die Kommandozeile verwendet den automatischen Text.
- **Namen aus der aufgenommenen Anwendung** (Schaltflächen, Menüs, Fenstertitel) bleiben, wie sie aufgezeichnet wurden. Eine englische Anleitung zu einer deutschen Oberfläche nennt die deutschen Beschriftungen in Anführungszeichen.
- **Tastennamen** folgen der Exportsprache („Strg“ oder „Ctrl“; in Französisch, Spanisch, Italienisch und Niederländisch „Ctrl“, dazu z. B. „Maj“, „Mayús“, „Maiusc“, „Entrée“, „Intro“, „Invio“); die Zuordnung steht in [05](05-schritterkennung.md#textvorlagen).
- **Anführungszeichen, Datums- und Zahlenformat** folgen der Exportsprache: „…“ / “…” / « … » / «…» / ‘…’; Stand als 11.11.2026 (de), 2026-11-11 (en), 11/11/2026 (fr, es, it), 11-11-2026 (nl); Dezimalkomma außer im Englischen.
- **Anrede** (Soll, nur Deutsch): „Klicken Sie auf …“, „Klicke auf …“ oder neutral „Auf … klicken“ – die Formen `formal`, `informal` und `neutral` der Textvorlagen. Englische Texte kennen nur die Befehlsform, die weiteren Sprachen je eine feste Form ([05](05-schritterkennung.md#textvorlagen)); die Wahl ist dann ausgegraut.

## 11 Entschärfung aufgezeichneter Texte

Aufgezeichnete Texte stammen aus fremden Programmen, Webseiten und Dateien. Ein Fenstertitel oder ein Elementname kann Zeichen enthalten, die in einem Ausgabeformat eine Bedeutung haben, oder absichtlich so gebaut sein. Deshalb gilt:

**Nicht vertrauenswürdig** sind Fenstertitel, Elementnamen und -werte, getippter Text, Feldinhalte, Inhalte der Zwischenablage, Adressen und Pfade, Text-Schnappschüsse, Prozess- und Dateinamen sowie der Sitzungstitel, soweit er aus solchen Angaben gebildet wurde. Texte, die der Autor im Viewer schreibt, sind seine eigenen; für sie gilt die eingeschränkte Auszeichnung aus [08](08-viewer.md#8-kommentieren).

| Format | Regel |
|---|---|
| HTML | Die Zeichen `&`, `<`, `>`, `"` und `'` werden als Zeichenverweise geschrieben. Aufgezeichnete Texte stehen nur als Textinhalt und in den Attributen `alt` und `title`, nie in Adressen, Kennungen oder Klassennamen. |
| Markdown | In Fließtext wird jedes Satzzeichen, das CommonMark als maskierbar kennt, mit einem Rückstrich versehen. Getippter Text, Adressen und Pfade stehen in Code-Spannen, deren Begrenzung länger ist als jede Folge von Gravis-Zeichen im Inhalt. Zeilenumbrüche im Inhalt werden durch ein sichtbares Zeichen ersetzt. |
| DOCX | Texte werden ausschließlich als Textknoten geschrieben; daraus entstehen keine Felder, Verweise oder Formeln. In XML unzulässige Steuerzeichen werden entfernt. |
| PDF | Texte werden als Text gezeichnet; daraus entstehen keine Verweise oder Aktionen. |
| Dateinamen | Unzulässige Zeichen (`< > : " / \ \| ? *`) und Steuerzeichen werden ersetzt, in Windows reservierte Namen (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`) umbenannt, abschließende Punkte und Leerzeichen entfernt, die Länge auf 100 Zeichen begrenzt. |

Für alle Formate:

- **Aufgezeichnete Adressen werden nie zu anklickbaren Verweisen.** Sie erscheinen als Text. Anklickbar sind nur Links, die der Autor selbst in einen Kommentar geschrieben hat, und nur mit den Schemata `http`, `https` und `mailto`.
- **Richtungs- und unsichtbare Steuerzeichen** (etwa U+202A bis U+202E und U+2066 bis U+2069) werden entfernt oder sichtbar gemacht (Soll), damit ein Text nicht anders aussieht, als er ist.
- **Überlange Texte:** Namen kürzt bereits die Textvorlage nach 60 Zeichen ([05](05-schritterkennung.md#textvorlagen)). Getippter Text und Feldinhalte werden im Schritttext nach 300 Zeichen gekürzt und mit „…“ gekennzeichnet.
- Die Entschärfung geschieht an einer einzigen Stelle je Format, unmittelbar beim Schreiben. Kein Baustein setzt aufgezeichnete Texte selbst in die Ausgabe.
- **Platzhalternamen, Marker-Bezeichnungen und Profilnamen** gelten ebenfalls als nicht vertrauenswürdig: Sie können aus einer fremden Sitzung oder einer importierten Einstellungsdatei stammen und werden entschärft wie aufgezeichnete Texte (Soll).

**Prüftexte für die Abnahme** – jeder dieser Texte wird als Fenstertitel, Elementname und getippter Text in eine Testsitzung gesetzt und muss in allen vier Formaten wörtlich lesbar erscheinen, ohne als Auszeichnung zu wirken:

| Prüftext | Darf nicht bewirken |
|---|---|
| `<script>alert(1)</script>` | Skript, HTML-Element |
| `<img src=x onerror=alert(1)>` | Bild, Ereignis-Attribut |
| `"><b>fett</b>` | Ausbruch aus einem Attribut |
| `[Klick](https://example.com)` und `![x](https://example.com/x.png)` | Verweis, nachgeladenes Bild |
| `https://example.com` und `www.example.com` | Anklickbarer Verweis |
| `**fett** _kursiv_ # Titel` | Auszeichnung in Markdown |
| `` a`b```c `` (Gravis-Zeichen einzeln und dreifach) | Abbruch der Code-Spanne |
| Zeilenumbruch, Tabulator, Zeichen U+0000 bis U+001F | Zerstörte Struktur, ungültiges XML |
| Text mit U+202E | Umgekehrte Darstellung ohne Kennzeichnung |
| `CON`, `a/b\c:d*?` (als Sitzungstitel) | Ungültiger oder gefährlicher Dateiname |

### 11.1 Formatierter Text des Autors

Eigene Texte können formatiert sein ([04 §13.18](04-datenformat.md#1318-formatierter-text-fmt)); aufgezeichnete Texte (Elementnamen, getippter Text, Fenstertitel) und automatische Texte sind es nie.

| Auszeichnung | Bedeutung | HTML | Markdown | Word | PDF |
|---|---|---|---|---|---|
| `**Text**`, `*Text*` | fett, kursiv (nie mitten im Wort) | `<strong>`, `<em>` | wie Eingabe | Fett, Kursiv | Helvetica-Bold, Helvetica-Oblique |
| `` `Text` `` | Eingabe, Code, Dateiname | `<code>` | Codespanne | Consolas, schattiert | Courier, schattiert |
| `[[Strg+S]]` | Tasten | `<kbd>` je Taste | `<kbd>` | Rahmen, schattiert | Tastenkästchen |
| `[[ui:Speichern]]` | Name eines Bedienelements | `<b class="ui">` | fett | fett | fett |
| `[Text](https://…)` | Link – nur http, https, mailto | `<a rel="noopener noreferrer">` | Link | Hyperlink (extern) | Link-Anmerkung |
| `[[ref:s1203]]`, `[[ref:k1203]]` | Querverweis auf den Schritt bzw. das Kapitel mit diesem Anker (Paket 23) | „Schritt 2.3“ als Sprung `#s5` | Link `#s5` | Text | Text |
| `{{Produkt}}` | Variable (Paket 23) | Wert | Wert | Wert | Wert |
| Zeilen mit `\|` am Anfang | Tabelle; Trennzeile `\|---\|` = Kopfzeile; höchstens 12 Spalten | `<table>` mit `th scope="col"` | Pipe-Tabelle | Tabelle mit wiederholter Kopfzeile | Gitter, Kopfzeile auf Folgeseiten wiederholt |
| `- ` / `1. ` am Zeilenanfang | Aufzählung, Nummerierung | `<ul>`, `<ol>` | Liste | Absätze mit Marke | Marke und Einzug |

- Leerzeile trennt Absätze, ein Zeilenumbruch bleibt ein Umbruch. `\` vor `* ` [ ]` (am Zeilenanfang auch vor `-` und Ziffern) macht ein Zeichen wörtlich; sonst ist ein Backslash wörtlich, damit Windows-Pfade unverändert bleiben.
- **PowerPoint, GIF, Fehlerbericht** zeigen den Text ohne Formatierung; Listen erscheinen dort als Zeilen mit „•“ bzw. „1.“.
- **KI-Paket, MCP, AutoHotkey, Suche und Qualitätsprüfung** lesen den sichtbaren Text ohne Auszeichnung.
- Zusätzliche Stile (HTML) und Schriften (PDF) kommen nur in Anleitungen, die formatierten Text enthalten; alle anderen Ausgaben bleiben bytegleich.
- **Querverweise** zeigen die Nummer, die der Schritt in genau dieser Anleitung hat (nach Umsortieren, Ausblenden, Kapiteln); ohne Nummerierung den Titel in Anführungszeichen. Ein Ziel, das nicht in der Anleitung steht, ergibt „??“ (nie dessen Titel) und den Befund Q32. Ein Verweis auf einen zusammengefassten Schritt zeigt auf den führenden Schritt. KI-Paket, MCP und Suche lesen „→“.
- **Variablen** stehen in `guide.vars` (je Sprache `guide.i18n.<lang>.vars`); eingebaut sind `{{Titel}}`/`{{Title}}`, `{{Untertitel}}`, `{{Version}}`, `{{Autor}}`, `{{Datum}}`, `{{Zielgruppe}}` (eingebaute Namen gewinnen). Sie wirken in allen eigenen Texten – Titel, Anleitungstext, Kapitel, Hinweise, Einleitung, Abschluss –, auch ohne Formatierung. Unbekannte Namen bleiben als `{{Name}}` stehen (Befund Q31). Einfache Klammern `{Benutzer}` bleiben Platzhalter der Verallgemeinerung.
- **Schutz:** siehe [10 §10.4](10-datenschutz-und-sicherheit.md#104-die-leseschicht-erzwingt), Regel L14.

## 12 Export-Vorprüfung

Vor jedem Export – im Dialog laufend, beim Ein-Klick-Export und auf der Kommandozeile einmalig – prüft das Programm die Sitzung. Die Regeln zu Eingaben und sensiblen Mustern legt [10](10-datenschutz-und-sicherheit.md#11-prüfung-vor-dem-export) fest; hier steht, wie der Export damit umgeht.

| Stufe | Bedeutung |
|---|---|
| Sperre | Der Export beginnt erst, wenn der Befund behoben ist. |
| Warnung | Wird vor dem Export gezeigt; der Klick auf „Exportieren“ gilt als Kenntnisnahme. Eine Warnung hält den Export nicht auf. |
| Hinweis | Wird angezeigt, braucht keine Bestätigung. |

| Prüfung | Stufe | Angebotene Aktion |
|---|---|---|
| Auffällige Eingabe ungeprüft – aus einem Klartext-Fenster oder mit treffsicherem Mustertreffer, auch in einem Kommentar des Autors –, die im Export erscheinen würde | Sperre | „Jetzt prüfen“ öffnet die Durchsicht der Eingaben ([08](08-viewer.md#13-durchsicht-der-eingaben)) |
| Unauffällige Eingaben ungeprüft | Hinweis | Prüfen · alle als geprüft markieren |
| Treffer der Musterprüfung in Bildschirmtexten (Elementnamen, Fenstertitel, Text-Schnappschüsse) | Hinweis | Zur Stelle springen · schwärzen · überall schwärzen |
| Treffer mit bekanntem Elementrechteck: zugehöriger Bildbereich | Hinweis | Bildbereich mitschwärzen |
| Eigene Texte fehlen in der Exportsprache | Warnung | Auswahl nach [Abschnitt 10](#10-sprache-der-schritttexte) |
| Schritte ohne Text | Warnung | Zur Stelle springen |
| Bilder fehlen oder sind beschädigt | Warnung | Zur Stelle springen · ohne Bild exportieren |
| Schritte mit Belegstufe „nur Eingabe“ oder „unbekannt“ im Umfang | Hinweis | Liste zeigen |
| Lücken der Aufzeichnung im Umfang | Hinweis | Liste zeigen |
| Reihenfolge wurde geändert | Hinweis | – |
| Schwärzungen werden angewendet (Anzahl) | Hinweis | – |
| Ausgabe größer als 25 MB | Hinweis | Abhilfen nach [5.3](#53-größe) |
| Zieldatei existiert außerhalb des Ordners `export/` der Sitzung | Sperre | Überschreiben bestätigen · anderen Namen wählen |
| Ziel nicht beschreibbar | Sperre | Anderes Ziel wählen |
| Qualitätsprüfung nach dem Regelkatalog ([12.1](#121-qualitätsprüfung-regelkatalog)) | Hinweis oder Warnung, je Regel | Sprung zum Schritt · Abhilfe der Regel · Ignorieren |

Mehrere Zeilen dieser Tabelle sind zugleich Regeln des Katalogs: fehlende Übersetzung (Q13), Schritte ohne Text (Q10), fehlende Bilder (Q16), Belegstufen (Q02, Q03) und Lücken (Q04). Sie werden auch ohne die Qualitätsprüfung geprüft; mit ihr tragen ihre Fundstellen die Regelnummer und lassen sich einzeln ignorieren.

Die Musterprüfung ist eine Hilfe, keine Garantie: Sie liest keine Bildinhalte und entscheidet nie selbst. Welche Muster eine Eingabe auffällig machen und welche nur gemeldet werden, steht in [10](10-datenschutz-und-sicherheit.md#11-prüfung-vor-dem-export). In der Schutzstufe „Streng“ gibt es keine Klartext-Fenster; auffällig sind dort nur Mustertreffer.

Die Vorprüfung ändert nichts an der Sitzung. Bestätigungen gelten für diesen Export, nicht dauerhaft; der Stand der Eingaben-Durchsicht dagegen bleibt gespeichert, ebenso ignorierte Fundstellen der Qualitätsprüfung. Für die Kommandozeile gilt [Abschnitt 14](#14-kommandozeilen-export).

### 12.1 Qualitätsprüfung: Regelkatalog

Die Qualitätsprüfung ist der Teil der Vorprüfung, der nicht nach Schutz fragt, sondern nach Verständlichkeit: Kommt ein Leser mit dieser Anleitung zurecht? Sie liest nur Daten der Leseschicht und dekodiert keine Bilder. Im Viewer steht sie jederzeit als Fenster mit Hinweisliste bereit (`F8`, [08](08-viewer.md#121-qualitätshinweise)); der Exportdialog fasst sie zusammen ([3.1](#31-exportdialog)), und die Kommandozeile gibt sie bei `export` und mit dem Befehl `check` aus ([14](#14-kommandozeilen-export)).

- Jede Regel hat eine feste **Nummer**, eine **Schwere** – Hinweis oder Warnung –, einen **Meldetext** und eine **Prüfregel**.
- Die Prüfregel stützt sich auf ein Kriterium aus [05](05-schritterkennung.md#qualitaetskriterien). Dort steht, aus welchen Daten es gelesen wird; hier stehen Schwere, Schwellen und Wortlaut.
- Geprüft werden die Schritte im Umfang des Exports – in der wirksamen Schrittfolge, in der Sprache und Darstellung dieses Exports und in der Sicht der Leseschicht.
- Jede Fundstelle bietet den **Sprung zum Schritt** und **Ignorieren**. Bestimmt ist sie durch Regel, Schritt und den Schlüssel der letzten Spalte; so steht sie in `edits.qualityIgnores` ([04](04-datenformat.md#edits-quality)). Der Schlüssel enthält nie aufgezeichneten Inhalt.
- Ignorierte Fundstellen erscheinen nicht und zählen nicht – weder im Dialog noch beim Ein-Klick-Export noch für `--strict`.

| Nr. | Schwere | Meldetext | Prüfregel (Kriterium aus 05) | Abhilfe | Schlüssel |
|---|---|---|---|---|---|
| Q01 | Hinweis | „Das Ziel dieses Schritts hat keinen Namen.“ | `targetUnnamed` – und der Autor hat den Schritttext nicht selbst geschrieben | Text schreiben · Stelle im Bild markieren | – |
| Q02 | Hinweis | „Dieser Schritt ist nur durch die Eingabe belegt.“ | `evidenceInputOnly` | Bild und Text prüfen | – |
| Q03 | Hinweis | „Dieser Schritt ist nicht belegt.“ | `evidenceUnknown` an einem Hauptschritt | Prüfen · ausblenden · neu aufnehmen | – |
| Q04 | Hinweis | „Hier fehlt ein Stück der Aufzeichnung.“ | `gapInFlow` zwischen dem ersten und dem letzten Schritt des Umfangs | Hinweisschritt einfügen · neu aufnehmen | Ursache der Lücke |
| Q05 | Hinweis | „Dieser Schritt dauert sehr lange.“ | `longStep`: Eingabe länger als 30 s | Teilen · Hinweis einfügen | – |
| Q06 | Hinweis | „Vor diesem Schritt vergeht viel Zeit.“ | `longIdle`: Leerlauf länger als 60 s, ohne dass davor ein Schritt „Warten“ oder ein Kommentar steht | Hinweisschritt einfügen | – |
| Q07 | Hinweis | „Das Bild ist sehr klein.“ bzw. „Das Ziel ist im Bild schwer zu finden.“ | `viewSize`: zu klein unter 400 × 225 Pixeln; zu groß, wenn der gezeigte Ausschnitt breiter ist als 2 560 Pixel oder das Ziel weniger als 0,05 % seiner Fläche einnimmt | Darstellung ändern · Größe des Ausschnitts ändern | `tooSmall` oder `tooLarge` |
| Q08 | Warnung | „Das Bild zeigt das Ziel nicht vollständig.“ | `targetCutOff` | Ausschnitt anpassen | Bild (`pre` oder `post`) |
| Q09 | Warnung | „Die Klickmarke liegt außerhalb des Bildes.“ | `clickOutsideView` | Ausschnitt anpassen | Bild |
| Q10 | Warnung | „Dieser Schritt hat keinen Text.“ | `textEmpty` | Text schreiben · automatischen Text wiederherstellen | Sprache |
| Q11 | Hinweis | „Dieser Schritt hat denselben Text wie der vorige.“ | `textDuplicate` | Zusammenfassen · Text ändern | Sprache |
| Q12 | Warnung | „Eine auffällige Eingabe ist ungeprüft.“ | `unreviewedInput` | Durchsicht öffnen | Art der Eingabe |
| Q13 | Warnung | „Ein eigener Text fehlt in der Exportsprache.“ | `translationMissing` | Auswahl nach [Abschnitt 10](#10-sprache-der-schritttexte) | Sprache |
| Q14 | Hinweis | „Ein persönlicher Wert ist nicht verallgemeinert.“ | `personalValue` | Regel anlegen | Art des Vorschlags (`userName`, `machineName`, `domain`, `profilePath`) |
| Q15 | Warnung | „Ein verallgemeinerter Wert kann im Bild noch zu sehen sein.“ | `substitutionImagesStale` | Stelle von Hand markieren · Bildflächen neu ermitteln | Kennung der Regel |
| Q16 | Warnung | „Zu diesem Schritt fehlt ein Bild.“ | `frameMissing` mit `preMissing` oder `postMissing`, wenn das fehlende Bild das gezeigte wäre | Anderes Bild wählen · ohne Bild exportieren | Eintrag aus `issues` |
| Q17 | Hinweis | „Das Bild dieses Schritts ist nicht verlässlich.“ | `frameMissing` mit `preNotGuaranteed`, `postTimeout` oder `masked` | Bild prüfen · anderes Bild wählen | Eintrag aus `issues` |
| Q18 | Hinweis | „Vor diesem Schritt passt der Zustand nicht nahtlos.“ | `seam` – etwa nach einer Neuaufnahme oder einer angehängten Aufnahme | Bilder vergleichen · Hinweisschritt einfügen | Art der Nahtstelle |
| Q19 | Hinweis | „Für diesen Schritt wurde keine Wirkung beobachtet.“ | `noEffect` | Prüfen, ob der Schritt nötig ist · ausblenden | – |
| Q20 | Hinweis | „Ein Kommentar nennt eine Schrittnummer, die sich verschoben haben kann.“ | `stepNumberInComment` | Kommentar prüfen | – |
| Q31 | Warnung | „Eine Variable ist nicht festgelegt …“ bzw. „Im Text steht noch ein Platzhalter (TODO, ???, XXX).“ – nur eigene Texte | `var`, `todo` | Text bearbeiten | – |
| Q34 | Hinweis | „Dieses Bild hat keine eigene Bildunterschrift …“ – nur bei `captions: custom` | `caption` | Text bearbeiten | – |
| Q32 | Warnung | „Ein Querverweis zeigt auf einen Schritt oder ein Kapitel, das nicht in der Anleitung steht.“ | `ref` | Text bearbeiten | – |
| Q21 | Hinweis | „Die Anwendung brauchte nach diesem Schritt … bis sie bereit war.“ – Wartezeit (bereit, sonst Bild ruhig, sonst erste Reaktion) eines Hauptschritts ab 2 s (`Timing.SlowMs`; Paket 9 aus [12](12-entwickler-fahrplan.md)) | Schritt | Zum Schritt | – |

Wirkung der Schwere – dieselbe wie in der Tabelle oben:

| Weg | Hinweis | Warnung |
|---|---|---|
| Exportdialog | wird in der Zeile „Prüfung“ gezählt | wird gezeigt; „Exportieren“ gilt als Kenntnisnahme |
| Ein-Klick-Export | erscheint in der Erfolgsmeldung | statt des Exports öffnet sich der Dialog |
| Kommandozeile | steht unter `notices` | steht unter `warnings`; mit `--strict` endet der Aufruf mit Exit-Code 9 |

Festlegungen:

- **Q12 ersetzt keine Sperre.** Im Viewer hält eine ungeprüfte auffällige Eingabe den Export ohnehin auf (erste Zeile der Tabelle oben). Die Regel sorgt dafür, dass der Befund in der Hinweisliste und in der Ausgabe der Kommandozeile steht. Wird sie ignoriert, verschwindet der Hinweis; die Sperre und der Zähler `{unreviewed:N}` bleiben.
- **Q14 und Q15 gehören zusammen:** Q14 nennt Werte, für die es noch keine Regel gibt; Q15 nennt Regeln, die das Bild nicht vollständig erreichen. Q14 vergleicht mit den Werten des Rechners, auf dem geprüft wird, und speichert sie nie ([10](10-datenschutz-und-sicherheit.md#109-verallgemeinerung-von-werten)).
- **Schwellen** stehen in dieser Tabelle; die Vorgaben aus 05 gelten, wo keine genannt ist.
- **Die Nummern sind fest.** Eine Regel behält ihre Nummer; entfällt sie, bleibt die Nummer frei; neue Regeln kommen ans Ende. Ein Eintrag in `qualityIgnores` mit einer unbekannten Regel bleibt erhalten und wirkt nicht.
- In der Ausgabe der Kommandozeile trägt jede Meldung der Qualitätsprüfung ihre Regelnummer und die Kennung des Schritts ([Abschnitt 14](#14-kommandozeilen-export)).
- **Stand der Regeln:** Q15 prüft bisher, ob eine Verallgemeinerungsregel überhaupt Bildflächen ermittelt hat (Schlüssel: Kennung der Regel), nicht ob der Wert in jedem Bild erreicht ist. Q18 meldet bisher angehängte Aufnahmen als Nahtstelle (Schlüssel `continuation:<Kennung>`), nicht Nahtstellen nach einer Neuaufnahme. Q20 meldet nur, wenn die Reihenfolge geändert wurde. Q13 gilt auch für die Dokumenttexte Einleitung und Abschluss (Schlüssel `<Sprache>:<Feld>`). Q15 und Q18 hängen an keinem Schritt. Die übrigen Regeln sind wie beschrieben umgesetzt.
- **Geprüfter Umfang:** Q12 prüft alle Schritte, nicht nur die des Umfangs, weil eine ungeprüfte Eingabe jede Weitergabe sperrt. Alle anderen Regeln prüfen die Schritte des gewählten Umfangs.
- **Zwischenspeicher:** Der Editor behält die Befunde bis zur nächsten Änderung der Aufnahme und rechnet erst dann neu.
- **Abhilfen im Editor:** Die Hinweisliste bietet je Befund die Abhilfe der Regel an – Durchsicht öffnen, Text bearbeiten, Zuschneiden, Ausblenden, Hinweisschritt einfügen, Zusammenfassen, Regel anlegen, anderes Bild wählen, Bilder prüfen – und „Ignorieren“. Einzelheiten stehen in [08](08-viewer.md#121-qualitätshinweise).
- **Wirkung im Dialog und beim Ein-Klick-Export:** Der Dialog zeigt die Zusammenfassung „Qualität: N Warnungen, M Hinweise“ mit dem Link „Ansehen (F8)“ und hält den Export nicht auf. Dass der Ein-Klick-Export bei einer Warnung stattdessen den Dialog öffnet, ist Soll ([3.2](#32-ein-klick-export)).

## 13 Dateibenennung und Ablage

- **Standardziel** ist der Ordner `export/` in der Sitzung ([04](04-datenformat.md)). Bei gepackten Sitzungen ist es ein Ordner `Name-export/` neben der `.steprec`-Datei.
- **Dateiname:** `{Titel}.{Endung}`. Bei der Kurzfassung und der Checkliste steht die Art dahinter ([9.4](#94-art-der-anleitung)). Die Vorlage kann `{Datum}`, `{Sprache}` und `{Vorlage}` hinzufügen (Soll), etwa `Datei im Editor speichern (en).pdf`.
- **Unterordner von `export/`:** `html/`, `markdown/`, `docx/`, `pdf/`, `pptx/`, `tour/`, `fehlerbericht/`, `gif/`, `video/`, `ahk/` und `ahk-ki/`; das KI-Paket liegt als `ki-paket-<Zeitstempel>/` unmittelbar in `export/`. Bei `.steprec`-Aufnahmen (schreibgeschützt) ist das Standardziel der Ordner neben der Archivdatei.
- **Fremde Ordner:** Wählt der Autor einen eigenen Ordner, legen Markdown, Fehlerbericht und AutoHotkey darin einen Unterordner mit dem Titel an, damit `bilder` nicht mit anderem vermischt wird. Ordnerausgaben (KI-Paket, AutoHotkey als KI-Paket) leeren nie einen fremden Ordner: Ist der Zielordner nicht leer und enthält er keinen früheren Export, endet der Export mit einer Meldung (Exit-Code 8 auf der Kommandozeile).
- Der Titel wird nach den Regeln aus [Abschnitt 11](#11-entschärfung-aufgezeichneter-texte) zu einem gültigen Dateinamen gemacht.
- **Markdown:** `{Titel}.md` und `{Titel}-bilder/`, beide in der ASCII-Schreibweise aus [6.1](#61-dateien).
- **Vorhandene Dateien:** Im Ordner `export/` der Sitzung wird ohne Rückfrage überschrieben, denn dort liegen nur erzeugte Ausgaben. An jedem anderen Ort fragt das Programm nach.
- Der zuletzt gewählte Ordner wird je Format gemerkt, ein Ordner innerhalb einer Aufnahme nicht ([4.5](#45-gespeicherte-export-einstellungen)).
- **Support-Paket** (Soll): `Support-<Datum>-<Titel>.zip` im Ordner `export/` der Sitzung. Es wird nie überschrieben; ein vergebener Name erhält eine laufende Nummer ([3.5](#35-support-paket)).
- **Vergleich zweier Sitzungen** (Soll): Der Viewer legt einen gespeicherten Vergleich im Ordner `export/` der Sitzung A ab ([08](08-viewer.md#59-zwei-aufnahmen-vergleichen)).
- **Zwischendateien** des Exports und der Vorschau liegen im Sitzungsordner unter `export/.tmp/job-<Kennung>/`, nie in `%TEMP%`, und werden entfernt, sobald sie nicht mehr gebraucht werden; Arbeitsordner, die älter als ein Tag sind, räumt das nächste Öffnen der Sitzung auf. Bei schreibgeschützten Aufnahmen liegt der Arbeitsordner neben dem Ziel.
- **Ältere Ausgaben nach einer Schwärzung:** Liegen im Ordner `export/` Ausgaben, die vor einer Schwärzung entstanden sind, listet der Viewer sie auf und bietet an, sie zu löschen ([10](10-datenschutz-und-sicherheit.md#106-abgeleitete-daten-und-frühere-exporte)). Ausgaben an anderen Orten kennt das Programm nicht.
- Ein abgebrochener oder fehlgeschlagener Export hinterlässt keine Datei: Geschrieben wird in eine Zwischendatei unter `export/.tmp/`, und erst die fertige Ausgabe gelangt an ihr Ziel. Liegt das Ziel auf einem anderen Datenträger, entsteht sie dort zunächst unter einem vorläufigen Namen und wird zuletzt umbenannt.

## 14 Kommandozeilen-Export

Der Befehl `export` erzeugt eine Anleitung ohne Oberfläche, etwa in Skripten. Rahmen, Standardströme und Exit-Codes der Kommandozeile legt [02](02-architektur.md#betriebsarten) fest; hier stehen die Optionen für die vier Anleitungsformate. Das KI-Paket entsteht mit demselben Befehl und `--format ki`; seine Optionen stehen in [06](06-ki-export-und-mcp.md#31-erzeugen-viewer-und-kommandozeile) – darunter die Detailstufe (`--detail`) und das Bildprofil (`--image-profile`).

**Ein Name, eine Bedeutung:** `--profile` gibt es bei `export` nicht. Der Name ist dem Aufnahmeprofil vorbehalten (`record --profile`, `support --profile`, [07](07-aufnahme-bedienung.md#31-aufnahme-über-die-kommandozeile)); das Bildprofil des KI-Pakets heißt auf der Kommandozeile `--image-profile`. Optionen, die nur für Anleitungen gelten – etwa `--template`, `--address`, `--after`, `--steps` –, sind zusammen mit `--format ki` ein Aufruffehler, ebenso die Optionen des KI-Pakets zusammen mit einem Anleitungsformat.

```
SebastiansStepRecorder.exe export <Sitzung> --format <html|md|docx|pdf|gif|mp4|pptx|tour|bugreport|ki|ahk|ahk-ki> [Optionen]
```

| Option | Bedeutung | Standard |
|---|---|---|
| `--format` | Ausgabeformat; `ki` erzeugt das KI-Paket, `ahk` das AutoHotkey-Skript, `ahk-ki` dessen KI-Paket | `html` |
| `--out <Pfad>` | Zieldatei oder Zielordner | Ordner `export/` der Sitzung ([13](#13-dateibenennung-und-ablage)) |
| `--template <standard\|short\|checklist>` | Art der Anleitung ([9.4](#94-art-der-anleitung)); nur HTML, Markdown, Word, PDF | `standard` |
| `--lang <de\|en>` | Sprache der Schritttexte | Sprache der Sitzung |
| `--importance <primary\|secondary\|all>` | Umfang: Hauptschritte · Haupt- und Nebenschritte · alle | `primary` |
| `--view <screen\|window\|crop>` | Darstellung der Bilder | aus den Dokument-Einstellungen |
| `--page <a4\|letter>`, `--landscape`, `--portrait` | Seitenformat für Word und PDF ([4.4](#44-dokumentrahmen)); gilt nur für diesen Export, `--landscape` und `--portrait` zusammen sind ein Aufruffehler | aus den Dokument-Einstellungen |
| `--seconds <n>` | GIF und Video: Anzeigedauer je Schritt in Sekunden (0,5–20) | `2.5` |
| `--strict` | Warnungen der Qualitätsprüfung und ungeprüfte Eingaben führen zum Abbruch | aus |
| `--json` | Ergebnis als JSON auf der Standardausgabe | aus |

Zusätzlich für das KI-Paket: `--detail`, `--image-profile`, `--zip`, `--no-events`; für AutoHotkey: `--real-timing`, `--no-images`, `--no-ocr`, `--with <Sitzung>` (wiederholbar). Sie stehen in [06](06-ki-export-und-mcp.md#31-erzeugen-viewer-und-kommandozeile) und [14a](#14a-autohotkey-skript). Die Hilfe (`--help`) gibt es auf Deutsch und Englisch.

Der Argumentleser ist streng: Eine unbekannte Option oder ein Wert außerhalb der Liste ist ein Aufruffehler (Exit-Code 2) und wird gemeldet, bevor etwas geschrieben wird.

**Soll, noch nicht vorhanden:** `--address`, `--override-step-views`, `--after`, `--steps` (an die Stelle tritt `--importance`), `--from`/`--to`, `--no-comments`, `--title`, `--docx-template` (die Word-Vorlage steht in den Dokument-Einstellungen, [7.3](#73-eigene-formatvorlage)), `--image-format`/`--jpeg-quality`, `--max-image-width`, `--overwrite` und `--quiet`. Die Ausgabe nimmt die Dokument-Einstellungen der Aufnahme.

Verhalten:

- Es erscheint nie ein Fenster und nie eine Rückfrage.
- Auf der **Standardausgabe** steht der Pfad der erzeugten Datei, oder mit `--json` ein Objekt mit den Feldern `files`, `warnings`, `notices`, `unreviewed`, `estimatedTokens` (Text und Bilder, für das KI-Paket) und `quality`. Hinweise und Meldungen der Qualitätsprüfung gehen auf die Fehlerausgabe.
- Schwärzungen werden immer angewendet; dafür gibt es keinen Schalter. Dasselbe gilt für Verallgemeinerungen und Neuaufnahmen ([4.6](#46-ziel-marker-neuaufnahmen-und-platzhalter)): Die Kommandozeile gibt Platzhalter aus und nur die wirksame Schrittfolge.
- **Qualitätsprüfung:** Bei Anleitungsformaten stehen ihre Fundstellen in der Ausgabe ([12.1](#121-qualitätsprüfung-regelkatalog)). Im JSON-Objekt trägt jede davon die Regelnummer (`rule`), die Nummer (`step`) und Kennung (`stepId`) des Schritts und, wo die Regel einen hat, den Schlüssel (`key`). Ignorierte Fundstellen fehlen. Auf der Fehlerausgabe steht je Fundstelle eine Zeile wie „Q08 Warnung Schritt 12: …“.
- **Ungeprüfte auffällige Eingaben:** Die Kommandozeile kann die Durchsicht nicht zeigen. Solche Eingaben erscheinen in der Ausgabe deshalb als `{unreviewed:N}`, bis der Autor sie im Viewer geprüft hat ([10](10-datenschutz-und-sicherheit.md#111-durchsicht-aller-eingaben)). Das JSON-Objekt nennt im Feld `unreviewed`, wie viele es sind. Einen Schalter, der sie im Klartext ausgibt, gibt es nicht. Eine Ausnahme ist der Fehlerbericht ([8c](#8c-fehlerbericht)): Er entsteht erst nach der Durchsicht.
- **Warnungen** halten den Export nicht auf. Mit `--strict` endet der Aufruf stattdessen mit Exit-Code 9, ebenso bei ungeprüften Eingaben; es wurde dann nichts geschrieben.
- Ein Abbruch mit `Strg+C` bricht den Export ab, ohne eine unvollständige Datei zu hinterlassen.
- Exportiert werden nur abgeschlossene Sitzungen.

Die Exit-Codes legt [02](02-architektur.md#betriebsarten) fest:

| Exit-Code | Bedeutung für `export` |
|---|---|
| 0 | Erfolg – auch mit Hinweisen und `{unreviewed:N}`; beides steht in der Ausgabe |
| 1 | Allgemeiner Fehler |
| 2 | Aufruffehler: unbekannte oder unzulässige Option, unbekanntes Format |
| 3 | Sitzung nicht gefunden, nicht lesbar oder noch nicht abgeschlossen |
| 7 | Voraussetzung fehlt: Video ohne Media Foundation oder H.264-Encoder ([8e](#8e-video-mp4)) |
| 8 | Ziel nicht beschreibbar oder ein Ordner, den der Export nicht ersetzen darf |
| 9 | Abbruch durch `--strict`: Die Vorprüfung meldete Warnungen oder zurückgehaltene Eingaben; es wurde nichts geschrieben |

### 14c Befehl check

`check <Sitzung>` führt nur die Qualitätsprüfung aus und schreibt nichts.

```
SebastiansStepRecorder.exe check <Sitzung> [--lang de|en] [--importance primary|secondary|all] [--json] [--strict] [--all]
```

- Ausgabe: je Fundstelle eine Zeile „Q08 Warnung Schritt 12: …“ und zuletzt „N Warnungen · M Hinweise“; mit `--json` ein Objekt mit `findings`, `warnings` und `hints`.
- `--all` nimmt ignorierte Fundstellen mit auf (sie sind als ignoriert gekennzeichnet); sonst fehlen sie.
- Exit-Code 0; mit `--strict` und mindestens einer nicht ignorierten Warnung 9.

Verwandte Befehle, die dieses Dokument nicht beschreibt:

| Befehl | Zweck | Beschrieben in |
|---|---|---|
| `compare <A> <B>` | Zwei Sitzungen abgleichen und den Bericht als Markdown oder JSON ausgeben | Aufruf in [02](02-architektur.md#befehle-nachgelagert), Verfahren in [05](05-schritterkennung.md#schrittabgleich), Ansicht in [08](08-viewer.md#59-zwei-aufnahmen-vergleichen), dasselbe Ergebnis für Agenten in [06](06-ki-export-und-mcp.md#199-compare_sessions) |
| `support` | Oberfläche für Support-Aufnahme und Support-Paket öffnen | [07](07-aufnahme-bedienung.md#32-support-aufnahme-über-die-kommandozeile) |

Ein Support-Paket erzeugt `export` nicht ([3.5](#35-support-paket)).

Beispiel:

```
SebastiansStepRecorder.exe export "C:\Aufnahmen\2026-11-11 14.30 – Editor – notiz.txt" ^
    --format pdf --template Schulung --lang de --out "D:\Doku\Datei-speichern.pdf"
```

## 14a AutoHotkey-Skript

Format `ahk`: Aus den Schritten entsteht ein lauffähiges **AutoHotkey-v2**-Skript (`export/ahk/<Titel>.ahk`, UTF-8 mit BOM). Grundlage ist der Win32-Kontext je Schritt ([04 §12.6](04-datenformat.md)).

| Schrittart | Erzeugter Code |
|---|---|
| Klick, Doppel-, Dreifach-, Rechtsklick | Fenster aktivieren (`Fenster(WinTitle)` = `WinWait` + `WinActivate` + `WinWaitActive`), dann `ControlClick ClassNN, hwnd,, Taste, Anzahl, "NA xN yN"` bei klassischen Steuerelementen, sonst `Click "x y"` in Fensterkoordinaten. Die jeweils andere Variante steht als Kommentar daneben, ebenso UIA-Name, AutomationId, Klasse und Pfad. |
| Ziehen | `MouseClickDrag` in Fensterkoordinaten. |
| Scrollen | `Click "x y WheelDown n"` bzw. mit Modifikatoren `Send`. |
| Eingabe | `ControlFocus ClassNN` (falls bekannt), `SendText "…"` für Text, `Send "{Taste}"` für Sondertasten und den Abschluss. Maskierte Eingaben werden beim Start mit `InputBox(…, "Password")` abgefragt und nie ins Skript geschrieben; Platzhalter werden zu Variablen. |
| Tastenkürzel, Taste | `Send "^s"`, `Send "{Tab 3}"` usw. |
| Fensterwechsel | `Fenster(WinTitle)`. |
| Wirkung „Fenster erschien“ | `WinWait Titel` nach dem Schritt. |
| Kommentar, Marker | Kommentarzeilen. |

WinTitle: `ahk_class … ahk_exe …`; für Dialoge (`#32770`) mit Titel. Klassennamen mit Laufzeitanteil (WinForms `WindowsForms10.…`, WPF `HwndWrapper[…]`, MFC `Afx:…`) werden durch Titel bzw. Steuerelement-Text ersetzt. Das Skript setzt Per-Monitor-DPI (physische Pixel wie die Aufnahme), übernimmt die echten Pausen (150–3000 ms, abschaltbar mit `--fixed-timing`), skaliert sie über `Tempo` und bricht mit Strg+Esc ab. Wie bei Anleitungen ist die Durchsicht auffälliger Eingaben Voraussetzung. Im Viewer kopiert „Code kopieren“ im Abschnitt *AutoHotkey* den Code eines einzelnen Schritts.

### 14b AHK-KI-Paket

Format `ahk-ki` (`export/ahk-ki/<Titel>/`; im Exportdialog: AutoHotkey → „Als KI-Paket“): Grundlage für einen KI-Assistenten, der das Skript schreibt oder verbessert.

| Datei | Inhalt |
|---|---|
| `README_AHK_KI.md` | Regeln für die KI: AutoHotkey v2, DPI-Kontext, WinTitle, Wege nach Stabilität, Tastatur vor Maus, Wartesignale statt fester Pausen, Erfolgsprüfungen, Parameter, Passwörter nie im Skript, Wiederholungen als Schleife, Gedrückthalten und Ziehen, Administratorrechte, Programmstart |
| `ablauf.json` | Format `steprec.ahk-ki`: Umgebung (Monitore, Tastaturlayouts, Programme mit Version, Startbefehl, Rechten und Fensterklassen), `parameters`, `repetitions`, je Schritt `window`, `selectors`, `point`, `control`, `element`, `elementPath`, `input`, `sync`, `verify`, `menuPath`, `choose`, `data`, `effect`, `images`, `ocr`, `windowTexts` |
| `entwurf.ahk` | Skript wie Format `ahk` |
| `bilder/schritt-NN.png`, `bilder/schritt-NN-kontext.png` | 48×48-Ausschnitt für `ImageSearch`; markierter Ausschnitt für Bildverständnis |
| `vergleich-N.md` | Mit `--with <Sitzung>`: Unterschiede zu weiteren Aufnahmen desselben Ablaufs (Parameter, Sonderfälle) |

Texterkennung: Windows-OCR (offline, Sprachen des Benutzerprofils) um den Klickpunkt – Wort unter dem Zeiger und nächstgelegene Zeilen mit Abstand (`ocr.underCursor`, `ocr.nearby`). Der MCP-Server liefert denselben Inhalt mit `get_automation`.

Ergänzungen im Format `ahk`: Eingaben werden zu Parametern oben im Skript; nach jedem Schritt wird gewartet, bis die Anwendung in der Aufnahme bereit war (`sync.readyMs`), bei beschäftigtem Zeiger mit `WarteBereit()`, bei neuen Fenstern mit `Erwarte(() => WinExist(…))`; beobachtete Änderungen (Texte, Häkchen, Titel) werden zu `Erwarte(…)`-Prüfungen (`Streng := true` bricht bei Abweichung ab). Menüpfade, Listenauswahl, Datenquellen, Tastenkürzel, Beschriftungen und Wiederholungen stehen als Kommentar, Bildsuche (`KlickBild`) als Alternative. Denkpausen nur mit `--real-timing`.

### 14d UI-Test-Entwürfe

Formate `flaui`, `pywinauto`, `playwright` und `test-ki` (12, Pakete 16 und 17; im Exportdialog: „UI-Test“ mit Auswahl des Testgerüsts). Grundlage ist dasselbe neutrale **Automationsmodell** wie beim AHK-KI-Paket (`ablauf.json`, §14b): Fenster, Selektoren nach Stabilität, Eingaben, Wartesignale, Zustand danach. AutoHotkey ist nur ein weiterer Abnehmer. Das Programm spielt nichts ab ([00 §4](00-ueberblick.md#4-nicht-ziele)); FlaUI, pywinauto und Playwright sind nur Ziel des erzeugten Textes, keine Abhängigkeit.

| Format | Datei (Standardort `export/tests/`) | Inhalt |
|---|---|---|
| `flaui` | `<Titel>Tests.cs` | xUnit-Testklasse mit FlaUI.UIA3: `Application.Launch` (Pfad aus `APP_UNDER_TEST`), Hilfsfunktionen `Find` (`Retry.WhileNull`) und `WaitWindow` (Titelteil) |
| `pywinauto` | `test_<titel>.py` | pytest-Funktion mit `Application(backend="uia")`, `child_window(auto_id/title, control_type)`, Hilfsfunktion `text_visible` (`wait_until`) |
| `playwright` | `<Titel>.spec.ts` | Playwright-Test für Schritte in Chrome, Edge, Firefox oder Brave: `goto` aus der aufgezeichneten Adresse, `getByRole` aus UIA-Typ und Name, `toHaveURL` nach Adresswechseln; Schritte außerhalb des Browsers als Kommentar |
| `test-ki` | Ordner `export/test-ki/<Titel>/` | `automation.json` (Format `steprec.automation`), die drei Entwürfe, `testbarkeit.md`/`.json`, `README_TEST_KI.md` mit Regeln für KI-Assistenten |

**Regeln der Entwürfe.**

| Thema | Regel |
|---|---|
| Selektoren | AutomationId (nur stabile, nicht rein numerische) → Name und Typ → Index. Bleibt nur der Klickpunkt, steht `NOTBEHELF / FALLBACK` mit Fensterkoordinaten im Code und eine Zeile im Testbarkeits-Bericht. |
| Warten | nie feste Pausen: `sync.waitWindow` → auf das Fenster warten; `sync.busyMs` > 300 ms → `Wait.UntilResponsive`; Fensterwechsel → auf das Fenster mit dem aufgezeichneten Titel warten. |
| Prüfungen | `verify.titleChanged` → Titelprüfung. Marker „Erwartet“ → Prüfung auf den ersten Text des Text-Schnappschusses ([03 §7.5](03-aufzeichnungskern.md)), sonst `TODO`. Marker „Fehler“ → Prüfung, die **fehlschlägt, solange der Fehler besteht** (der Fehlertext darf nicht erscheinen). |
| Eingaben | Text als Literal; maskierte und ungeprüfte Eingaben (`{unreviewed:N}`) nur aus Umgebungsvariablen `WERT_<n>`. Tastenkürzel über `VirtualKeyShort` bzw. pywinauto-Notation. |
| Nicht abgebildet | Kommentare, Lücken (als Kommentar), Spurdaten, Mausbewegungen ohne Aktion. Ziehen nur als gekennzeichneter Notbehelf. |

Wie bei Anleitungen ist die Durchsicht auffälliger Eingaben im Editor Voraussetzung; kopflos stehen Platzhalter. Der MCP-Server liefert Entwurf, Regeln und Testbarkeit mit `get_automation` und `target` ([06](06-ki-export-und-mcp.md)).

**Testbarkeits-Bericht** (Editor: Exportieren → „Testbarkeit prüfen …“; Kommandozeile: `testability <Sitzung> [--json] [--strict]`, [02](02-architektur.md)): je benutztem Bedienelement (Klick, Eingabe, Ziehen, Scrollen; je Programm, Typ, Name und AutomationId einmal) die Befunde `noUiaElement`, `noAutomationId`, `noName`, `nameNotUnique`, `coordinatesOnly`, `volatileWindowClass` mit Schwere (`warning`, wenn nur Koordinaten bleiben; `hint` bei fehlender AutomationId) und Empfehlung. `--strict` endet mit Exit-Code 9, sobald eine Warnung vorliegt – so kann die CI fehlende AutomationIds anmahnen.

## 15 Qualitätsziele

Die Referenzsysteme A und B stehen in [03](03-aufzeichnungskern.md#leistungsbudgets), die Sitzungsgrößen S, M und L in [08](08-viewer.md#14-geschwindigkeitsziele).

| Ziel | Festlegung |
|---|---|
| Dauer, Sitzung S (50 Schritte) auf System A | HTML und Markdown ≤ 3 s, DOCX und PDF ≤ 5 s |
| Dauer, Sitzung M (1 000 Schritte) auf System A | Jedes Format ≤ 60 s, mit Fortschrittsanzeige |
| Dauer auf System B (Bilder in 4K) | Höchstens das Doppelte der Werte für System A |
| Bedienbarkeit | Der Viewer bleibt während des Exports bedienbar; der Export ist abbrechbar |
| Arbeitsspeicher | Höchstens 600 MB zusätzlich; Bilder werden nacheinander verarbeitet, nicht alle zugleich gehalten |
| Bildtreue | Ein als PNG exportiertes Bild ist pixelgleich mit dem im Viewer angezeigten |
| Wiederholbarkeit | Zwei Exporte mit gleichen Einstellungen ergeben bei HTML und Markdown bytegleiche Dateien; bei DOCX und PDF mit der Option „festes Datum“ ebenfalls |
| Gültigkeit | HTML ohne Fehler im W3C-Prüfer; DOCX ohne Fehler im Open-XML-Prüfer; Markdown ohne Abweichung in der Gegenprobe |

## 16 Beispiele

Dieselbe kurze Anleitung in zwei Formaten. Die Bilddaten sind gekürzt.

### 16.1 HTML

```html
<!doctype html>
<html lang="de">
<head>
<meta http-equiv="Content-Security-Policy"
      content="default-src 'none'; img-src data:; style-src 'sha256-…'; base-uri 'none'; form-action 'none'">
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Datei im Editor speichern</title>
<style>/* eingebettetes Stylesheet, über seinen Hashwert zugelassen */</style>
</head>
<body>
<header>
  <h1>Datei im Editor speichern</h1>
  <p class="meta">3 Schritte · Stand 11.11.2026</p>
</header>
<main>
<ol class="steps">
  <li id="schritt-1">
    <p>Wählen Sie den Menüpunkt <b>„Datei“</b>.</p>
    <figure><img alt="Editor, Menüpunkt „Datei“ markiert" width="960" height="540"
                 src="data:image/png;base64,iVBORw0KGgo…"></figure>
  </li>
  <li id="schritt-2">
    <p>Wählen Sie den Menüpunkt <b>„Speichern unter“</b>.</p>
    <figure><img alt="Menü „Datei“, Menüpunkt „Speichern unter“ markiert" width="960" height="540"
                 src="data:image/png;base64,iVBORw0KGgo…"></figure>
    <aside class="note"><p><b>Hinweis:</b> Bei einer neuen Datei öffnet auch „Speichern“ diesen Dialog.</p></aside>
  </li>
  <li id="schritt-3">
    <p>Geben Sie in das Feld <b>„Dateiname“</b> „<kbd>notiz.txt</kbd>“ ein und drücken Sie die Eingabetaste.</p>
    <figure><img alt="Dialog „Speichern unter“, Feld „Dateiname“ ausgefüllt" width="960" height="540"
                 src="data:image/png;base64,iVBORw0KGgo…"></figure>
  </li>
</ol>
</main>
</body>
</html>
```

### 16.2 Markdown

```markdown
# Datei im Editor speichern

3 Schritte · Stand 11.11.2026

1. Wählen Sie den Menüpunkt **„Datei“**.

   ![Editor, Menüpunkt „Datei“ markiert](Datei-im-Editor-speichern-bilder/schritt-001.png)

2. Wählen Sie den Menüpunkt **„Speichern unter“**.

   ![Menü „Datei“, Menüpunkt „Speichern unter“ markiert](Datei-im-Editor-speichern-bilder/schritt-002.png)

   > **Hinweis:** Bei einer neuen Datei öffnet auch „Speichern“ diesen Dialog.

3. Geben Sie in das Feld **„Dateiname“** „`notiz.txt`“ ein und drücken Sie die Eingabetaste.

   ![Dialog „Speichern unter“, Feld „Dateiname“ ausgefüllt](Datei-im-Editor-speichern-bilder/schritt-003.png)
```

### 16.3 Entschärfung am Beispiel

Ein Fenster trägt den Titel `<b>Rechnung</b> [neu]_2026`. Im Schritttext erscheint er so:

| Format | Ausgabe im Quelltext | Der Leser sieht |
|---|---|---|
| HTML | `&lt;b&gt;Rechnung&lt;/b&gt; [neu]_2026` | `<b>Rechnung</b> [neu]_2026` |
| Markdown | `\<b\>Rechnung\</b\> \[neu\]\_2026` | `<b>Rechnung</b> [neu]_2026` |
| DOCX, PDF | Textknoten bzw. gezeichneter Text | `<b>Rechnung</b> [neu]_2026` |

## 17 Anforderungen

| ID | Anforderung | Priorität | Abnahmekriterium |
|---|---|---|---|
| EXP-001 | Export als HTML und Markdown | Muss | Beide Formate lassen sich aus Dialog und Kommandozeile erzeugen. |
| EXP-002 | Export als DOCX und PDF | Muss | Beide Formate lassen sich aus Dialog und Kommandozeile erzeugen. |
| EXP-003 | Schwärzungen im Export erzwungen | Muss | Ein Testmuster unter einer Schwärzung und ein geschwärzter Text sind in keiner der vier Ausgaben auffindbar; es gibt keine Option, die das ändert. |
| EXP-004 | Bildgleichheit mit dem Viewer | Muss | Ein als PNG exportiertes Bild ist pixelgleich mit der Kopie desselben Schritts aus dem Viewer. |
| EXP-005 | In sich geschlossene Ausgabe | Muss | Keine der vier Ausgaben enthält einen Verweis auf eine fremde Quelle; beim Öffnen der HTML-Datei entsteht kein Netzwerkzugriff. |
| EXP-006 | Erzeugung ohne Fremdprogramme | Muss | Der Export gelingt auf einem System ohne Office und mit gesperrtem Netzwerk. |
| EXP-007 | Wiederholbarkeit | Soll | Zwei Exporte mit gleichen Einstellungen sind nach den Regeln aus Abschnitt 15 bytegleich. |
| EXP-008 | Platzhalter für Geschwärztes | Muss | In allen vier Formaten erscheint eine geschwärzte Textstelle als `[geschwärzt]` bzw. `[redacted]`, ein geschwärzter Schritt als „Schritt geschwärzt“ bzw. „Step redacted“ (er fehlt nur, wenn er zugleich ausgeblendet ist), ein geschwärzter Zeitraum als Hinweis „vom Autor entfernt“ mit Zeitpunkt und Dauer und ein geschwärzter Bildbereich als schwarz gefüllte Fläche. |
| EXP-010 | Exportdialog | Muss | Der Dialog bietet Format, Art, Umfang, Darstellung, Sprache, Umgang mit fehlender Übersetzung und Ziel mit „Durchsuchen“ und „Standardort“; er zeigt die Zusammenfassung der Qualitätsprüfung. Vorlage, Nachher-Bild und die übrigen Zeilen der Vorprüfung sind Soll. |
| EXP-011 | Vorbelegung und kurzer Weg | Muss | Der Dialog öffnet mit den Einstellungen des letzten Exports; `Strg+E`, Eingabetaste erzeugt die Ausgabe. |
| EXP-012 | Ein-Klick-Export | Soll | `Strg+Umschalt+E` wiederholt den letzten Export dieser Aufnahme ohne Dialog; bei ungeprüften Eingaben fragt das Programm nach der Durchsicht. Dass bei Sperre oder Warnung der Dialog mit dem Befund öffnet, ist noch nicht umgesetzt. |
| EXP-013 | Aktionen nach dem Export | Soll | Das Ergebnisfenster bietet Öffnen (nicht bei Skripten), Im Ordner zeigen und Pfad kopieren. Datei kopieren ist noch nicht umgesetzt. |
| EXP-014 | Vorschau | Soll | „Vorschau“ öffnet die Ausgabe im Standardprogramm, ohne die Zieldatei anzulegen; die Vorschaudatei liegt in `export/.tmp/` der Sitzung, nicht in `%TEMP%`, und ist nach dem Schließen der Sitzung entfernt. |
| EXP-015 | Fortschritt und Abbruch | Muss | Ab 0,7 s Dauer erscheint ein Fortschrittsfenster mit „Abbrechen“; ein Abbruch hinterlässt keine Datei am Ziel. |
| EXP-016 | Gemerkte Export-Einstellungen | Muss | Nach einem Export öffnet der Dialog mit den Optionen des Formats, die Sprache stammt vom letzten Export der Aufnahme; die Aufnahme enthält dafür nichts und keinen Zielpfad (4.5). Das Objekt `exports` in `edits.json` ist Soll. |
| EXP-017 | KI-Paket im Exportdialog | Muss | Der Dialog bietet „KI-Paket“ als Ziel, zeigt dessen Optionen und die Token-Abschätzung aus [06](06-ki-export-und-mcp.md#10-token-abschätzung-und-paketteilung) und verwendet dieselbe Vorprüfung wie die Anleitungsformate. |
| EXP-020 | Umfang wählbar | Muss | Hauptschritte, alle Schritte, Auswahl und Bereich ergeben jeweils genau die erwarteten Schritte. |
| EXP-021 | Nie Enthaltenes | Muss | Ausgeblendete und verworfene Schritte, geschwärzte Inhalte sowie interne und ausgeblendete Kommentare erscheinen in keiner Ausgabe, bei keiner Kombination von Optionen. |
| EXP-022 | Darstellung je Export | Muss | Jede der vier Einstellungen ergibt die erwarteten Bildausschnitte; „Einzelfestlegungen überschreiben“ wirkt wie beschrieben. |
| EXP-023 | Nachher-Bild | Muss | „nie“, „bei Änderung“ und „immer“ ergeben die erwartete Bildzahl an einer Testsitzung mit bekannten Änderungen. |
| EXP-024 | Bildformat und Bildbreite | Soll | PNG und JPEG sind wählbar; kein Bild überschreitet die eingestellte Breite. |
| EXP-025 | Kommentare | Muss | Kommentare erscheinen mit ihrer Kennzeichnung am richtigen Schritt; mit der Option „weglassen“ fehlen sie. |
| EXP-026 | Kapitel, Titelblatt, Inhaltsverzeichnis | Soll | Kapitel erscheinen als Überschriften; Titelblatt und Inhaltsverzeichnis folgen den Optionen. |
| EXP-027 | Kopf- und Fußzeile, Seitenformat | Soll | DOCX und PDF zeigen Titel und „Seite X von Y“; A4 und Letter, Hoch und Quer sind wählbar. |
| EXP-028 | Hinweis auf Sicherheitsdialoge | Soll | An der Stelle einer solchen Lücke steht der neutrale Hinweis; mit abgeschalteter Option fehlt er. |
| EXP-029 | Keine unbeabsichtigten Personenangaben | Muss | Eine Ausgabe ohne ausgefülltes Autorfeld enthält weder Benutzernamen noch Rechnernamen noch Pfade des Aufnahmerechners in den Metadaten. |
| EXP-030 | HTML als eine Datei | Muss | Die Ausgabe besteht aus genau einer Datei; alle Bilder sind eingebettet. |
| EXP-031 | HTML ohne Skripte, mit Policy | Muss | Die Datei enthält kein `<script>`, kein Ereignis-Attribut, kein `style`-Attribut; die Policy aus 5.1 steht als erstes Element im Kopf; das Stylesheet ist über seinen Hashwert zugelassen. |
| EXP-032 | HTML gültig und zugänglich | Soll | Keine Fehler im W3C-Prüfer; jedes Bild hat einen Alternativtext; Kontrast mindestens 4,5 : 1. |
| EXP-033 | HTML für Druck, schmale Bildschirme, Dunkel | Soll | Der Druck teilt keinen Schritt; die Seite ist bei 360 Pixeln Breite ohne Querrollen lesbar; sie folgt der Dunkel-Einstellung. |
| EXP-034 | HTML mit Bilderordner | Kann | Die Variante erzeugt eine HTML-Datei und einen Bilderordner mit relativen Verweisen. |
| EXP-040 | Markdown mit Bilderordner | Muss | Die Ausgabe besteht aus einer `.md`-Datei und einem Ordner; alle Bildverweise lösen auf. |
| EXP-041 | Markdown nach CommonMark, ohne HTML | Muss | Die Gegenprobe mit Markdig findet weder HTML-Knoten noch Verweise außer den eigenen Bildverweisen und den Links des Autors. |
| EXP-042 | Dialekte | Soll | „GitHub“ und „Obsidian“ erzeugen die Kommentarformen aus 6.2. |
| EXP-043 | Sichere Dateinamen für Markdown | Muss | Datei-, Ordner- und Bildnamen enthalten nur ASCII-Buchstaben, Ziffern, Bindestriche und den Punkt vor der Endung. |
| EXP-044 | ZIP-Ausgabe, Kopfdaten | Kann | Beide Optionen wirken wie beschrieben. |
| EXP-050 | DOCX gültig | Muss | Keine Fehler im Open-XML-Prüfer; Word ab 2016 und LibreOffice ab 7 öffnen die Datei ohne Reparaturhinweis. |
| EXP-051 | DOCX mit benannten Formatvorlagen und echter Nummerierung | Muss | Jeder Inhalt trägt die Formatvorlage aus 7.2; nach dem Löschen eines Schritts in Word stimmt die Nummerierung. |
| EXP-052 | Eigene Word-Vorlage | Muss | Mit einer Vorlage erscheinen deren Kopfzeile, Schriften und Seitenränder; der Inhalt steht an der gekennzeichneten Stelle; eine `.dotm` wird abgelehnt. |
| EXP-053 | DOCX-Bilder | Muss | Kein Bild überschreitet den Satzspiegel; jedes hat einen Alternativtext; Schritttext und Bild stehen auf derselben Seite. |
| EXP-054 | DOCX-Inhaltsverzeichnis | Soll | Das Verzeichnis ist ein Word-Feld `TOC`, beim Öffnen mit anklickbaren Einträgen gefüllt, und lässt sich in Word aktualisieren; Word fragt beim Öffnen nicht nach. |
| EXP-055 | DOCX ohne Makros und externe Bezüge | Muss | Das Paket enthält keinen Makro-Teil und keine externe Beziehung außer Links des Autors. |
| EXP-060 | PDF mit echtem Text | Muss | Der Schritttext lässt sich im PDF-Betrachter markieren, kopieren und suchen. |
| EXP-061 | PDF-Seitenumbruch | Muss | In einer Testsitzung mit Bildern unterschiedlicher Höhe ist kein Bild geteilt und kein Schritttext von seinem Bild getrennt. |
| EXP-062 | PDF mit Unicode-Text | Muss | Deutsche und englische Texte zeigen keine Ersatzzeichen. Zeichen außerhalb von WinAnsi stehen in einer eingebetteten Teilmenge mit `ToUnicode` (Kopieren und Suchen funktionieren); nicht darstellbare Schriften (CJK, Arabisch, Hebräisch, Thai, Emoji) ergeben „?“ und genau eine Warnung. |
| EXP-063 | PDF-Kopf- und Fußzeile | Soll | Jede Seite zeigt „Seite X von Y“ mit richtiger Gesamtzahl. |
| EXP-064 | PDF-Inhaltsverzeichnis mit Sprungzielen | Soll | Ein Klick auf einen Eintrag führt zur Seite des Kapitels; die Lesezeichenleiste zeigt Kapitel und Schritte. |
| EXP-065 | PDF/A, Strukturmarkierung | Kann | Je nach Umsetzung nachweisbar mit einem Prüfprogramm. Lesezeichen sind umgesetzt (EXP-064). |
| EXP-070 | Mitgelieferte Vorlagen | Soll | Die vier Vorlagen aus 9.1 sind vorhanden und ergeben die genannten Kennzeichen. |
| EXP-071 | Eigene Vorlagen | Soll | Gespeicherte Vorlagen stehen nach Neustart zur Wahl und liegen portabel beim Programm. |
| EXP-072 | Branding | Soll | Logo, Akzentfarbe und Fußzeilentext erscheinen in allen Formaten, die sie vorsehen. |
| EXP-073 | Vorlagen ohne ausführbaren Inhalt | Muss | Eine Vorlage mit Skript, Makro oder Verweis nach außen wird beim Einlesen abgelehnt. |
| EXP-074 | Vorlagen weitergeben | Kann | Eine weitergegebene Vorlage ergibt auf einem zweiten Rechner dieselbe Ausgabe. |
| EXP-080 | Schritttexte auf Deutsch | Muss | Alle automatischen Texte erscheinen auf Deutsch. |
| EXP-081 | Schritttexte auf Englisch | Soll | Dieselbe Sitzung ergibt ohne Neuaufnahme eine englische Anleitung; Namen aus der Anwendung bleiben unverändert. |
| EXP-082 | Fehlende Übersetzung eigener Texte | Soll | Die Qualitätsprüfung meldet betroffene Schritte (Q13); der Dialog bietet den automatischen Text (Standard) und den Text der anderen Sprache; die Kommandozeile nimmt den automatischen Text. |
| EXP-083 | Anredeformen | Soll | Sie, du und neutral ergeben die jeweilige Form in allen automatischen deutschen Texten; für Englisch ist die Wahl ausgegraut. |
| EXP-090 | Entschärfung in HTML | Muss | Alle Prüftexte aus Abschnitt 11 erscheinen wörtlich und bewirken nichts; geprüft im Browser und im Quelltext. |
| EXP-091 | Entschärfung in Markdown | Muss | Alle Prüftexte erscheinen nach dem Rendern wörtlich; die Gegenprobe findet keine unerwarteten Knoten. |
| EXP-092 | Entschärfung in DOCX und PDF | Muss | Alle Prüftexte erscheinen wörtlich; die Dateien sind gültig; es entsteht kein Feld, kein Verweis, keine Aktion. |
| EXP-093 | Aufgezeichnete Adressen nie anklickbar | Muss | Eine aufgezeichnete Adresse ist in keiner der vier Ausgaben ein Verweis. |
| EXP-094 | Richtungs- und Steuerzeichen | Soll | Ein Text mit U+202E erscheint in keiner Ausgabe unbemerkt umgekehrt. |
| EXP-095 | Sichere Dateinamen | Muss | Die Titel-Prüftexte aus Abschnitt 11 ergeben gültige Dateinamen im Zielordner und keinen Pfad außerhalb. |
| EXP-096 | Entschärfung an einer Stelle | Soll | Je Format gibt es genau einen Baustein, der aufgezeichnete Texte schreibt (Nachweis durch Codeprüfung). |
| EXP-100 | Vorprüfung vor jedem Export | Muss | Dialog, Ein-Klick-Export und Kommandozeile führen die Prüfungen der Tabelle in Abschnitt 12 aus; die als Soll gekennzeichnete Zeile zur Qualitätsprüfung regelt EXP-140. |
| EXP-101 | Sperre bei ungeprüften auffälligen Eingaben | Muss | Solange eine auffällige Eingabe ungeprüft ist, die im Export erscheinen würde, beginnt kein Export aus der Oberfläche; nach der Entscheidung in der Durchsicht beginnt er. |
| EXP-102 | Hinweise zu Belegstufen und Lücken | Soll | Betroffene Schritte und Lücken im Umfang werden gezählt und sind als Liste abrufbar. |
| EXP-110 | Standardablage und Benennung | Muss | Ohne Angabe liegt die Ausgabe im Ordner `export/` der Sitzung und heißt nach dem Titel. |
| EXP-111 | Überschreibregel | Soll | Im Ordner `export/` wird ohne Rückfrage überschrieben, an anderen Orten nachgefragt. |
| EXP-112 | Keine unvollständigen Dateien | Muss | Wird der Export hart beendet, liegt am Ziel keine Datei mit dem Zielnamen. |
| EXP-113 | Ältere Ausgaben nach einer Schwärzung | Soll | Nach einer neuen Schwärzung sind die Ausgaben im Ordner `export/` der Sitzung aufgelistet und lassen sich mit einem Klick löschen. |
| EXP-120 | Kommandozeilen-Export | Muss | Der Befehl aus Abschnitt 14 erzeugt alle Anleitungsformate mit den Optionen der Tabelle; mit `--format ki` entsteht das KI-Paket nach [06](06-ki-export-und-mcp.md#31-erzeugen-viewer-und-kommandozeile). Unbekannte Optionen enden mit Exit-Code 2. Die Option `--overwrite` ist Soll. |
| EXP-121 | Exit-Codes und Ausgabe | Muss | Jeder Fall der Tabelle in Abschnitt 14 liefert seinen Exit-Code, auch „Ziel nicht beschreibbar“ und „Abbruch durch `--strict`“; die Standardausgabe enthält nur den Pfad bzw. das JSON-Objekt. |
| EXP-122 | Kein Fenster, keine Rückfrage | Muss | Der Befehl läuft in einer Sitzung ohne Desktop-Zugriff durch. |
| EXP-123 | Ungeprüfte Eingaben auf der Kommandozeile | Muss | Ungeprüfte auffällige Eingaben erscheinen in der Ausgabe als `{unreviewed:N}` und sind im JSON-Objekt gezählt; es gibt keine Option, die sie im Klartext ausgibt. Nach der Durchsicht im Viewer erscheinen sie im Klartext bzw. geschwärzt. |
| EXP-130 | Dauer des Exports | Soll | Die Zeiten aus Abschnitt 15 sind auf System A und – mit dem dort genannten Faktor – auf System B eingehalten. |
| EXP-131 | Bedienbarkeit während des Exports | Soll | Während eines Exports der Sitzung M reagiert der Viewer auf Schrittwechsel in höchstens 150 ms. |
| EXP-140 | Qualitätsprüfung nach Regelkatalog | Soll | (Q15 und Q18 teilweise, siehe 12.1.) An einer Testsitzung mit je einer vorbereiteten Fundstelle meldet jede Regel Q01 bis Q21 genau diese, mit Nummer, Schwere und Meldetext der Tabelle in 12.1; eine Sitzung ohne Fundstellen ergibt keine Meldung. |
| EXP-141 | Schwere wirkt einheitlich | Soll | Ein Hinweis hält keinen Export auf; eine Warnung öffnet beim Ein-Klick-Export den Dialog und führt mit `--strict` zu Exit-Code 9. Regeln, die schon bisher geprüft wurden (Q02, Q03, Q04, Q10, Q13, Q16), behalten ihre Schwere. |
| EXP-142 | Ignorierte Fundstellen | Soll | Eine in `edits.qualityIgnores` eingetragene Fundstelle erscheint weder im Dialog noch in der Ausgabe der Kommandozeile und zählt nicht für `--strict`; nach dem Entfernen des Eintrags erscheint sie wieder. Das Ignorieren von Q12 hebt weder die Sperre im Viewer noch `{unreviewed:N}` auf. |
| EXP-143 | Regelnummern in der Ausgabe, stabil | Soll | Jede Meldung der Qualitätsprüfung nennt im Dialog die Regelnummer und im JSON-Objekt `rule`, `step` und gegebenenfalls `key`; zwischen zwei Programmversionen ändert keine Regel ihre Nummer. |
| EXP-150 | Platzhalter in allen Formaten | Soll | Ein verallgemeinerter Kanarienwert ist in keiner der vier Ausgaben auffindbar – weder im Text noch in Alternativtexten, Metadaten oder Dateinamen; an jeder Textfundstelle steht `{Name}`, im Bild an jeder vom Viewer gezeigten Stelle die gefüllte Fläche (T-15). |
| EXP-151 | Platzhalter ausgezeichnet und entschärft | Soll | Platzhalter tragen die Auszeichnung aus 4.6; ein Platzhaltername aus den Prüftexten aus Abschnitt 11 erscheint wörtlich und bewirkt nichts. |
| EXP-152 | Hinweis auf Platzhalter | Soll | Eine Anleitung mit Platzhaltern nennt vor dem ersten Schritt die verwendeten Namen und keinen der Werte; mit abgeschalteter Option und ohne Platzhalter fehlt der Hinweis. |
| EXP-153 | Kein Schalter gegen Verallgemeinerungen | Soll | Weder Dialog noch Kommandozeile bieten eine Option, die Platzhalter durch die Werte ersetzt. |
| EXP-160 | Neuaufnahmen in der Anleitung | Soll | Nach einer Ersetzung stehen in allen vier Ausgaben die neuen Schritte an der Stelle des ersten ersetzten, fortlaufend nummeriert; ersetzte Schritte, nicht verwendete Neuaufnahmen und verworfene Abschnitte erscheinen bei keiner Wahl des Umfangs. Nach „Ersetzung aufheben“ stehen wieder die früheren Schritte dort. |
| EXP-161 | Ziel der Aufnahme in der Anleitung | Soll | Mit der Option steht nach dem Titel der Block mit den wirksamen Angaben im Wortlaut der Textvorlagen; eine geleerte Angabe fehlt; ohne Angaben fehlt der Block. Ohne die Option erscheint keine der Angaben. |
| EXP-162 | Marker in der Anleitung | Soll | Marker der Typen `error`, `expected` und `important` erscheinen als nicht nummerierter Absatz mit dem Satz der Textvorlage und ihrem Bild; `note` erscheint nur mit „Alle Schritte“; ein im Viewer geänderter Typ gilt. |
| EXP-170 | Inhalt des Support-Pakets | Soll | Die ZIP-Datei enthält genau die HTML-Anleitung als eine Datei und das KI-Paket; sie enthält weder `events.jsonl` noch `edits.json` noch den Bildspeicher der Sitzung (T-16). |
| EXP-171 | Benennung und Ablage des Support-Pakets | Soll | Die Datei heißt `Support-<Datum>-<Titel>.zip` nach den Regeln aus 3.5 und liegt ohne andere Wahl im Ordner `export/` der Sitzung; ein zweites Paket derselben Sitzung erhält eine laufende Nummer und überschreibt das erste nicht. |
| EXP-172 | Support-Paket aus bereinigter Kopie | Soll | Ein geschwärzter Kanarienwert und ein verallgemeinerter Wert sind in keinem Byte der ZIP-Datei und ihrer entpackten Inhalte auffindbar; nach dem Erstellen und nach einem Abbruch liegt unter `export/.tmp/` keine bereinigte Kopie mehr. |
| EXP-173 | Support-Paket nur nach Durchsicht, nur aus dem Viewer | Soll | Ohne vollständige Durchsicht entsteht kein Paket; `export` bietet kein Format dafür an. |
| EXP-174 | Kein Versand | Soll | Beim Erstellen entsteht kein Netzwerkzugriff, und es wird kein Mailprogramm, Teilen-Dialog oder Upload aufgerufen; geöffnet wird nur der Ordner im Explorer. |
| EXP-190 | Befehl `check` | Soll | `check` gibt die Fundstellen mit Regelnummer aus, mit `--json` als Objekt, und endet mit `--strict` bei einer nicht ignorierten Warnung mit Exit-Code 9; es schreibt nichts. |
| EXP-191 | Ziel wählen, Ergebnisfenster | Muss | „Durchsuchen“ ändert Ordner und Namen, „Standardort“ stellt `export/` wieder her; das Ergebnisfenster zeigt den Pfad; ein Ordner innerhalb einer Aufnahme wird nicht zur Vorgabe für andere Aufnahmen. |
| EXP-192 | Wiederholung | Soll | `Strg+Umschalt+E` erzeugt dieselbe Ausgabe wie der letzte Export dieser Aufnahme, ohne Dialog. |
| EXP-193 | Art der Anleitung | Soll | Kurzfassung und Checkliste enthalten keine Bilder, kein Deckblatt und kein Inhaltsverzeichnis; die Checkliste hat ein Kästchen je Schritt; die Standardanleitung wird nicht überschrieben. |
| EXP-194 | PowerPoint | Soll | Die Datei öffnet in PowerPoint und LibreOffice ohne Reparaturhinweis; sie enthält je Schritt eine Folie mit Titelplatzhalter und Notizen; zwei Exporte sind bytegleich. |
| EXP-195 | Klick-Tour | Soll | Die Datei enthält kein Skript und keine fremde Adresse; ein Klick auf die Klickfläche führt zum nächsten Schritt; die Druckansicht zeigt alle Schritte. |
| EXP-196 | Fehlerbericht | Soll | Der Bericht enthält Zusammenfassung, Umgebung, Schritte, erwartetes und tatsächliches Ergebnis und Anhänge, aber nie Pfade, Kommandozeilen oder Prozessnummern; mit ungeprüften Eingaben entsteht er nicht. |
| EXP-197 | GIF | Kann | Das GIF zeigt Titelbild und alle Schritte in der gewählten Dauer und läuft in Schleife. |
| EXP-200 | Querverweise, Variablen, Tabellen | Soll | `[[ref:s…]]` zeigt nach dem Umsortieren die aktuelle Nummer; ein ausgeblendetes Ziel ergibt „??“ ohne Titel; `{{Produkt}}` erscheint mit dem Wert der Exportsprache; eine Tabelle erscheint in HTML, Markdown, Word und PDF mit Kopfzeile. |
| EXP-201 | Unterkapitel und Textabschnitte | Soll | Unterkapitel erscheinen in HTML als kleinere Überschrift (eingerückt im Inhaltsverzeichnis), in Word als Überschrift 2, im PDF kleiner und als Lesezeichen eine Ebene tiefer, in Markdown als `###`; Textabschnitte ohne Nummer und Bild. |
| EXP-202 | Bildunterschriften und Warnhinweise | Soll | Mit `captions: custom` steht unter dem Bild „Abb. 1: …“ (HTML `figcaption`, Word kursiv, PDF klein, Markdown kursiv), das Abbildungsverzeichnis am Ende verweist auf die Schritte; der Alternativtext steht im `alt` bzw. in Word in der Bildbeschreibung; ein Hinweis mit `signal: danger` heißt „GEFAHR:“ und steht mit `warnFirst` vor dem Schritttext. |
| EXP-199 | Formatierter Text | Soll | Fett, kursiv, Eingabe, Tasten, Bedienelemente, Links und Listen erscheinen in HTML, Markdown, Word, PDF und Klick-Tour wie in 11.1; PowerPoint ohne Formatierung; KI-Paket und MCP ohne Auszeichnung. |
| EXP-198 | Video (MP4) | Kann | Die Datei beginnt mit einer `ftyp`-Box, enthält `moov` und einen H.264-Videostrom mit gerader Breite und Höhe; ihre Dauer ist Titelbild plus gewählte Dauer je Schritt. Ohne H.264-Encoder entsteht keine Datei, und die Meldung nennt die fehlende Voraussetzung. |
| EXP-180 | Exportvorgabe aus dem Aufnahmeprofil | Soll | Nach dem Stopp einer Aufnahme mit einem Profil, das Format und Vorlage festlegt, öffnet sich der Exportdialog mit diesen Werten; es entsteht keine Datei, bevor „Exportieren“ gewählt ist. |

## 18 Offene Punkte

| Nr. | Punkt | Klärung |
|---|---|---|
| 1 | PDF/A und Strukturmarkierung | Der PDF-Schreiber ist eigen; Lesezeichen sind umgesetzt, PDF/A und Struktur bleiben Kann. |
| 2 | Mitgelieferte Schriften | Das Programm liefert keine Schriften mit: Text außerhalb von WinAnsi nutzt Segoe UI (Rückfall Arial, Tahoma) vom Rechner des Exports, der übrige Text die PDF-Standardschriften. Ob freie Schriften mitgeliefert werden, damit das Ergebnis auf jedem Rechner gleich ist, ist offen. |
| 3 | Schriftsatz für Schriften mit komplexer Formung (etwa Arabisch) im PDF | Nicht umgesetzt: Die Zeichen erscheinen als „?“ mit einer Warnung; die Ausgabe bricht nicht ab. |
| 4 | Getippte Werte als Beispielwert kennzeichnen („zum Beispiel …“) | Braucht eine weitere Variante der Textvorlagen in [05](05-schritterkennung.md#textvorlagen); bis dahin Kann. |
| 5 | Zeile „Erstellt mit …“: Standard an oder aus? | Entscheidung des Projekts; derzeit aus. |
| 6 | Policy für die Variante „HTML mit Bilderordner“ | Verhalten der Browser bei lokalen Dateien prüfen, bevor die Variante umgesetzt wird. |
| 7 | Erkennung sensibler Muster in Bildern | Setzt Textstellen mit Rechtecken im Datenbestand voraus ([04](04-datenformat.md)); ohne sie entfällt die Prüfung. |
| 8 | Schwellen der Regel Q07 | Mit echten Sitzungen prüfen, ob 2 560 Pixel Breite und 0,05 % Flächenanteil zu viele oder zu wenige Hinweise ergeben. |
