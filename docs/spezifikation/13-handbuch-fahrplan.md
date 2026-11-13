# 13 – Fahrplan: Handbuch-Editor und Bild-Editor

`Status: Entwurf 0.1 · Stand: 2026-11-13`

Dieses Kapitel plant, wie der Editor zum Werkzeug für **fertige, gute Bedienhandbücher** wird – mit Seitenansicht, formatiertem Text, Handbuch-Bausteinen, Seitenlayout, redaktioneller Prüfung, Pflegefunktionen und einem ausgebauten Bild-Editor. Es nennt **Pakete, Reihenfolge, Leitplanken und Abnahme**. Die Anforderungen mit Kennung entstehen je Paket in den Fachkapiteln ([08](08-viewer.md), [09](09-anleitungs-export.md), [04](04-datenformat.md)).

## Inhalt

1. [Zweck und Vorgaben](#1-zweck-und-vorgaben)
2. [Ausgangslage](#2-ausgangslage)
3. [Leitplanken](#3-leitplanken)
4. [Übersicht der Pakete](#4-übersicht-der-pakete)
5. [Pakete im Einzelnen](#5-pakete-im-einzelnen)
6. [Entscheidungen](#6-entscheidungen)
7. [Vorab-Experimente](#7-vorab-experimente)
8. [Bewusst nicht im Fahrplan](#8-bewusst-nicht-im-fahrplan)
9. [Pflege](#9-pflege)

---

## 1. Zweck und Vorgaben

Der Auftraggeber hat am 2026-11-12 acht Bereiche gewählt und am 2026-11-13 den Ausbau des Bild-Editors ergänzt (Vorgaben V-46 bis V-54 in [01 §4](01-anforderungen.md#4-rückverfolgung-der-vorgaben)). Vorgehen: direkt bauen, Paket für Paket.

| Vorgabe | Bereich | Pakete |
|---|---|---|
| V-46 | Handbuch-Bausteine: freie Textabschnitte, Unterkapitel, Änderungsverzeichnis, Glossar, Abbildungsverzeichnis mit Bildunterschriften, Impressum, Warnhinweise mit Signalwort, Störungsbehebung, Tabellen | 24, 25, 26 |
| V-47 | Formatierter Text: fett, kursiv, Listen, Tastenkappen, Namen von Bedienelementen, Links, Querverweise, Variablen, Textbausteine | 21, 23 |
| V-48 | Seitenansicht und Layout: Seitenansicht im Editor, Seitenformat, Kopf- und Fußzeile mit Feldern, Seitenumbrüche, Layoutvorlagen, eigene Word-Vorlage | 20, 27, 31 |
| V-49 | Redaktionsprüfung: Fertigstellung, Stilregeln, Lesbarkeit, Status Entwurf/Freigegeben mit Wasserzeichen | 29, 30 |
| V-50 | Pflege des Handbuchs: Abschnitt ersetzen, Schritt teilen, Kapitel verschieben, Übersetzungsübersicht | 24, 33, 34 |
| V-51 | Tempo im Editor: Befehlspalette, Lesezeichen, Kapitelvorschläge, Markierungsstil übertragen | 22, 37 |
| V-52 | Schulung und Weitergabe: Präsentationsansicht, Drucken, Stapel-Export | 20, 38 |
| V-53 | Barrierearmes PDF: Strukturmarkierung, Alternativtext je Bild, Metadaten | 32 |
| V-54 | Bild-Editor: Schrittmarken 1, 2, 3 …, weitere Markierungen, mehr Farben, weitere Werkzeuge | 22, 28 |

## 2. Ausgangslage

Stand 0.13.0. Vorhanden: Titelseite, Kapitel (eine Ebene), Hinweiskästen (Hinweis, Tipp, Achtung, Wichtig, Ergebnis), Dokumentvorlagen, Seitenformat A4/Letter hoch/quer, eigene Word-Vorlage, Befehlspalette, Lesezeichen, Textbausteine, Abschnitt neu aufnehmen und ersetzen, PDF/Word/HTML/Markdown/PowerPoint/GIF/Video.

Es fehlen für ein fertiges Handbuch: formatierter Text (jeder Text ist Fließtext), freie Textabschnitte, Unterkapitel, Verzeichnisse, Kopf- und Fußzeile mit Feldern, eine Seitenansicht im Editor, redaktionelle Prüfung; im Bild-Editor Schrittnummern im Bild, mehr Farben, Mehrfachauswahl und weitere Formen.

## 3. Leitplanken

| Nr. | Leitplanke | Folge |
|---|---|---|
| H-1 | **Bestehende Aufnahmen bleiben, wie sie sind.** Fehlt ein neuer Schlüssel, entsteht die heutige Ausgabe. | Referenztest `LegacyOutputTests` mit normalisierten Ausgaben aller Formate (`tests/…/Golden/`); geändert nur mit Begründung im Commit. Ausnahmen in [6](#6-entscheidungen). |
| H-2 | **Schutz geht vor Formatierung.** | Schwärzung, Verallgemeinerung und Durchsicht wirken vor und nach dem Auswerten der Auszeichnung; im Zweifel entfällt die Formatierung, nie der Schutz. Kanarien-Test je neuem Textweg. |
| H-3 | **Keine neuen Abhängigkeiten.** | Eigene Schreiber und Parser; in Windows eingebaute Schnittstellen (`Windows.Data.Pdf`) sind zulässig ([02 §9](02-architektur.md)). |
| H-4 | **KI-Paket, MCP, AutoHotkey ändern sich inhaltlich nicht.** | Auszeichnungsfreier Text, feste Bildmarken, Einzelschritte. |
| H-5 | **Je Paket vollständig.** | Code, Tests, Fachkapitel, README Deutsch und Englisch, Stand hier und in [11 §2.1](11-roadmap-tests-risiken.md#21-stand-der-umsetzung); ein Commit je Paket. |
| H-6 | **Testbare Logik im Kern.** | Die Tests verweisen nur auf `StepRecorder.Core`. |

## 4. Übersicht der Pakete

Aufwand wie in [12](12-entwickler-fahrplan.md): S ≈ ein Arbeitsabschnitt, M ≈ zwei bis drei, L ≈ vier und mehr.

| Nr. | Paket | Vorgabe | Aufwand | Hängt ab von | Stand |
|---|---|---|---|---|---|
| 20 | Fundament und Seitenansicht (mit Drucken) | V-48, V-52 | L | – | erledigt |
| 21 | Formatierter Text | V-47 | L | 20 | erledigt |
| 22 | Bild-Editor I: Schrittmarken, Nummern, Farben, Stilvorlagen | V-54, V-51 | L | 20 | erledigt |
| 23 | Querverweise, Variablen, Tabellen | V-47 | M | 21 | erledigt |
| 24 | Gliederung: Unterkapitel, Textabschnitte, Kapitel ordnen | V-46, V-50 | M–L | 21 | erledigt |
| 25 | Bildunterschriften, Abbildungsverzeichnis, Warnhinweise | V-46 | M | 24 | erledigt |
| 26 | Glossar, Änderungsverzeichnis, Impressum, Störungsbehebung | V-46 | M | 25 | erledigt |
| 27 | Seite und Layout: Ränder, Kopf-/Fußzeile, Umbrüche, Layoutvorlagen | V-48 | L | 24 | geplant |
| 28 | Bild-Editor II: Objekte, neue Werkzeuge, Einrasten | V-54 | L | 22 | geplant |
| 29 | Redaktionsprüfung I: Stil und Lesbarkeit | V-49 | M | 21 | geplant |
| 30 | Redaktionsprüfung II: Fertigstellung und Freigabe | V-49 | M | 27, 29 | geplant |
| 31 | Word-Profil mit Formatvorlagen und Listen | V-48 | M | 27 | geplant |
| 32 | Barrierearmes PDF | V-53 | L | 25, 27 | geplant |
| 33 | Übersetzungsübersicht und CSV | V-50 | M | 21 | geplant |
| 34 | Schritt teilen und Ergebnisschritt | V-50 | M | – | geplant |
| 37 | Kapitelvorschläge | V-51 | S | 24 | geplant |
| 38 | Präsentationsansicht und Stapel-Export | V-52 | M | 27 | geplant |

Die Nummern 35 und 36 (Übernahme als Abschnitt, Abschnitt ersetzen) entfallen: „Abschnitt neu aufnehmen und ersetzen“ ist mit 0.13.0 umgesetzt ([08 §9.3](08-viewer.md#93-abschnitt-neu-aufnehmen-und-ersetzen)). Befehlspalette, Lesezeichen und Textbausteine sind ebenfalls vorhanden; Paket 37 behält nur die Kapitelvorschläge.

## 5. Pakete im Einzelnen

### Paket 20 – Fundament und Seitenansicht (L)

**Ziel:** Die Anleitung lässt sich im Editor so ansehen, wie sie gedruckt wird – und wird bei jeder Änderung neu aufgebaut.

**Umfang:**

- Referenztest `LegacyOutputTests` (Leitplanke H-1).
- `PdfRaster` im Kern über `Windows.Data.Pdf` (Verfügbarkeit einmal geprüft, `STEPRECORDER_FORCE_NOPDF=1` erzwingt den Rückfall).
- `GuideExport.RenderPdf`: dasselbe PDF wie der Export, im Speicher, mit der Lage jedes Schritts und Kapitels; `GuideImageCache` hält gerenderte Schrittbilder im Arbeitsspeicher (Schlüssel enthält einen Fingerabdruck aller Schwärzungen, Verallgemeinerungen und der Durchsicht).
- Editor: Mitte „Bild | Seite“ (`Strg+F2`, Knopf „Seitenansicht“ in der Kopfleiste); der Eintrag „Titelseite und Einleitung“ zeigt die echten Seiten statt der nachgebauten Titelseite; Klick auf einen Schritt in den Seiten wählt ihn, Doppelklick öffnet sein Bild; der gewählte Schritt wird angesprungen und kurz umrandet.
- Drucken (`Strg+P`, Datei-Menü): Seiten mit 200 dpi im Hintergrund, dann Windows-Druckdialog mit Seitenbereich.

**Schutzregeln:** Die Seitenansicht liest über eine eigene Leseansicht ohne Oberfläche – ungeprüfte Eingaben erscheinen als Platzhalter, ein Hinweisband nennt das. Drucken verlangt wie der Export die Durchsicht. Der Bildspeicher liegt nur im Arbeitsspeicher.

**Abnahme:** Referenztest grün; `PdfRasterTests` öffnet erzeugte PDFs mit dem Windows-Renderer und prüft Seitengröße und Pixel; `RenderPdf` liefert für jeden Schritt eine Lage, der zweite Aufbau kommt ganz aus dem Speicher und ist bytegleich, nach einer Schwärzung wird kein altes Bild wiederverwendet.

### Paket 21 – Formatierter Text (L)

Auszeichnung in den vorhandenen Textfeldern, nur für Texte mit Kennzeichen `fmt: 1` (alte Texte bleiben wörtlich): `**fett**`, `*kursiv*`, `` `Eingabe` ``, `[[Strg+S]]` Tastenkappen, `[[ui:Name]]` Bedienelement, `[Text](https://…)` Links (nur http, https, mailto), Aufzählung `- `, Nummerierung `1. `. Eigener Parser `RichDoc` im Kern; sichere Reihenfolge Schwärzen → Auswerten → erneut prüfen (im Zweifel Text ohne Formatierung); Darstellung in HTML, Markdown, Word, PDF, PowerPoint; Editor mit Formatleiste und `Strg+B`/`Strg+I`. Agenten-Texte (`by: mcp`) bleiben wörtlich.

### Paket 22 – Bild-Editor I (L)

Automatische Schrittnummer im Bild und Markenstile (Klassisch · Nummern · Dezent · Kräftig; Standard bleibt Klassisch), Nummern-Werkzeug mit Neuzählen, Formen und Zählweisen, Legende; 12 Farben plus freie Farbwahl und zuletzt benutzte, farbiger Markierstift, Linienarten und Spitzen, Deckkraft und Füllung, Text direkt im Bild, Stilvorlagen mit „Auf alle Schritte anwenden“. Das KI-Paket behält die festen Marken.

### Pakete 23–38

Querverweise und Variablen (23), Gliederung mit Unterkapiteln und freien Textabschnitten und dem Verschieben ganzer Kapitel (24), Bildunterschriften, Abbildungsverzeichnis und Warnhinweise mit Signalwort (25), Glossar, Änderungsverzeichnis, Impressum und Störungsbehebung (26), Ränder, Kopf- und Fußzeile mit Feldern, Seitenumbrüche und Layoutvorlagen (27), Mehrfachauswahl und neue Bildwerkzeuge (28), Stilregeln und Lesbarkeit (29), Fertigstellung und Freigabe mit Wasserzeichen (30), Word-Profil (31), strukturiertes PDF (32), Übersetzungsübersicht mit CSV (33), Schritt teilen (34), Kapitelvorschläge (37), Präsentationsansicht und Stapel-Export (38). Einzelheiten entstehen bei der Feinplanung des jeweiligen Pakets in diesem Abschnitt.

## 6. Entscheidungen

| Nr. | Entscheidung (Auftraggeber, 2026-11-13) |
|---|---|
| E-H1 | „Abschnitt neu aufnehmen und ersetzen“ ohne Weiterschreiben in die fertige Aufnahme – inzwischen mit 0.13.0 umgesetzt. |
| E-H2 | Die Präsentationsansicht darf bei einer Bildschirmfreigabe sichtbar sein, solange keine Aufnahme läuft; beim Start einer Aufnahme schließt sie sich. |
| E-H3 | Zwei Korrekturen dürfen bestehende Ausgaben ändern: Eine Kapitelüberschrift erscheint vor dem nächsten sichtbaren Schritt, wenn ihr erster Schritt ausgeblendet ist; die du-Form der automatischen Texte wird vollständig. |

## 7. Vorab-Experimente

| ID | Frage | Ergebnis |
|---|---|---|
| VE-24 | Rastert `Windows.Data.Pdf` die eigenen PDFs ohne Paketidentität? | Ja (Windows 11, `PdfRasterTests`); auf Windows Server offen – dort gilt der Rückfall „Vorschau im Browser“. |
| VE-26 | Trefferquote der Stilregeln an echten Anleitungen | offen (vor Paket 29) |
| VE-27 | Strukturiertes PDF mit Bildschirmleser | offen (Paket 32) |

## 8. Bewusst nicht im Fahrplan

PDF/A- und PDF/UA-Kennzeichen, Silbentrennung und Blocksatz, komplexe Schriften (CJK, Arabisch) im PDF, Stichwortverzeichnis, Verweise über Aufnahmen hinweg.

## 9. Pflege

Beginnt ein Paket, entstehen seine Anforderungen mit Kennung in den Fachkapiteln; „Stand“ in Abschnitt 4 wechselt auf „in Arbeit“, dann „erledigt“, und [11 §2.1](11-roadmap-tests-risiken.md#21-stand-der-umsetzung) erhält eine Zeile.
