# 08 – Viewer

Status: Entwurf 0.1 · Stand: 2026-11-11

Der Viewer ist das Fenster, in dem der Autor eine Aufzeichnung ansieht, abspielt, bearbeitet, kommentiert und Teile daraus in andere Programme übernimmt. Er öffnet sich nach jeder Aufnahme von selbst. Dieses Dokument legt Aufbau, Funktionen, Kopierwege, Tastenkürzel und Geschwindigkeitsziele fest – dazu den Vergleich zweier Aufnahmen, das Ersetzen einzelner Abschnitte, das Verallgemeinern persönlicher Werte, die Qualitätshinweise, die Suche über alle Sitzungen und den geführten Weg zum Support-Paket. Wie Anleitungen als Datei ausgegeben werden, steht im [Anleitungs-Export](09-anleitungs-export.md).

## Inhalt

1. [Zweck und Abgrenzung](#1-zweck-und-abgrenzung)
2. [Leitlinien](#2-leitlinien)
3. [Öffnen und Schließen](#3-öffnen-und-schließen)
4. [Aufbau des Fensters](#4-aufbau-des-fensters)
5. [Ansehen](#5-ansehen)
6. [Abspielen](#6-abspielen)
7. [Bilder bearbeiten](#7-bilder-bearbeiten)
8. [Kommentieren](#8-kommentieren)
9. [Schritte ordnen und Texte bearbeiten](#9-schritte-ordnen-und-texte-bearbeiten)
10. [Kopieren und Herausziehen](#10-kopieren-und-herausziehen)
11. [Sitzungen verwalten](#11-sitzungen-verwalten)
12. [Komfortfunktionen](#12-komfortfunktionen)
13. [Durchsicht der Eingaben](#13-durchsicht-der-eingaben)
14. [Geschwindigkeitsziele](#14-geschwindigkeitsziele)
15. [Tastenkürzel](#15-tastenkürzel)
16. [Zustände und Fehlerfälle](#16-zustände-und-fehlerfälle)
17. [Anforderungen](#17-anforderungen)
18. [Offene Punkte](#18-offene-punkte)

## 1 Zweck und Abgrenzung

Der Viewer ist Betrachter und Bearbeitung in einem; ein getrenntes Editor-Programm gibt es nicht. Er ist Teil derselben EXE wie die Aufnahme.

| Thema | Steht in |
|---|---|
| Aufnahme, Aufnahmeleiste, Aufnahmebereiche | [07 Aufnahme und Bedienung](07-aufnahme-bedienung.md) |
| Dateiformate der Anleitung (HTML, Markdown, DOCX, PDF) | [09 Anleitungs-Export](09-anleitungs-export.md) |
| KI-Paket und MCP-Server | [06 KI-Export und MCP](06-ki-export-und-mcp.md) |
| Aufbau von `edits.json`, Bildspeicher, Verankerung an Ereignisnummern | [04 Datenformat](04-datenformat.md) |
| Schrittarten, Belegstufen, Wichtigkeitsstufen, Textvorlagen | [05 Schritterkennung](05-schritterkennung.md) |
| Regeln für Schwärzung, sensible Muster, Durchsicht der Eingaben | [10 Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md) |
| Gemeinsame Leseschicht, über die der Viewer alle Daten liest | [02 Architektur](02-architektur.md) |

Alle Bearbeitungen landen in `edits.json`; die Rohdaten der Sitzung bleiben unverändert. Das Schema legt [04](04-datenformat.md#edits-json) fest; dieses Dokument beschreibt nur, was der Viewer dort ablegt.

Begriffe in diesem Dokument:

| Begriff | Bedeutung |
|---|---|
| Schritt | Eine erkannte Handlung des Autors oder ein von ihm eingefügter Hinweis |
| Hauptschritt | Schritt der höchsten Wichtigkeitsstufe; nur diese zeigt die Anleitung standardmäßig |
| Bild | Ein gespeichertes Bild eines Schritts: Vorher-Bild, Nachher-Bild oder Zwischenbild |
| Darstellung | Welcher Teil eines Bildes gezeigt wird: ganzer Bildschirm, Anwendungsfenster, Ausschnitt um die Aktion oder freier Zuschnitt |
| Markierung | Vom Autor gezeichnetes Objekt im Bild (Pfeil, Rechteck, Text …) |
| Automatische Markierung | Klickmarke, Elementrahmen und Mauszeiger, die aus der Aufzeichnung stammen |
| Schwärzung | Unkenntlichmachung aus Datenschutzgründen; gilt in jeder Ausgabe |
| Kommentar | Text des Autors zu einem Schritt, an einer Bildstelle (Pin) oder als eigener Hinweisschritt |

## 2 Leitlinien

1. **Sofort da.** Nach dem Stopp steht das Ergebnis in unter einer Sekunde auf dem Bildschirm.
2. **Ansehen zuerst, Bearbeiten ohne Umweg.** Es gibt keinen Bearbeitungsmodus: Wer ein Werkzeug wählt oder einen Text anklickt, bearbeitet.
3. **Zerstörungsfrei.** Jede Bearbeitung lässt sich zurücknehmen; das Original bleibt erhalten. Einzige Ausnahme ist das ausdrücklich gewählte endgültige Schwärzen.
4. **Alles ist kopierbar.** Was der Viewer zeigt, lässt sich mit einem Griff in ein anderes Programm bringen.
5. **Was man sieht, bekommt man.** Anzeige, Kopie und Export zeigen dasselbe Bild.
6. **Ehrlich.** Lücken und unsichere Stellen der Aufzeichnung sind sichtbar und nicht geglättet.
7. **Schwärzungen gelten überall.** Kein Kopier- oder Exportweg führt an ihnen vorbei.
8. **Vollständig per Tastatur bedienbar.**

### 2.1 Bedienabläufe mit Höchstzahl

Gezählt werden Klicks, Tastenkürzel und Ziehbewegungen bei geöffneter Sitzung; das Tippen von Text zählt nicht.

| Nr. | Ablauf | Weg | Höchstzahl |
|---|---|---|---|
| V1 | Ergebnis einer Aufnahme ansehen | Der Viewer öffnet sich nach dem Stopp von selbst | 0 |
| V2 | Bild eines Schritts in ein anderes Programm bringen | `Strg+C` | 1 |
| V3 | Bild mit Schritttext kopieren | `Strg+Umschalt+C` | 1 |
| V4 | Beliebigen Bildausschnitt kopieren | `Alt+Ziehen` | 1 |
| V5 | Mehrere Schritte als Mini-Anleitung kopieren | Schritte wählen (`Umschalt+Klick`), `Strg+Umschalt+C` | 2 |
| V6 | Darstellung eines Schritts ändern | `1`, `2` oder `3` | 1 |
| V7 | Pfeil oder Rahmen einzeichnen | Werkzeugtaste, Ziehen | 2 |
| V8 | Bedienelement schwärzen | `S`, Klick auf das Element | 2 |
| V9 | Schritttext ändern | `F2`, Text, `Strg+Eingabe` | 2 |
| V10 | Kommentar zum Schritt | `K`, Text, `Strg+Eingabe` | 2 |
| V11 | Schritt ausblenden | `Entf` | 1 |
| V12 | Ablauf abspielen | `F5` | 1 |
| V13 | Anleitung exportieren | `Strg+E`, Eingabetaste – oder `Strg+Umschalt+E` | 2 bzw. 1 |
| V14 | Zwei Aufnahmen vergleichen | `F7`, zweite Sitzung wählen | 2 |
| V15 | Schritte neu aufnehmen und ersetzen | Schritte wählen, `Strg+R`, „Aufnahme starten“, nach der neuen Aufnahme Stopp | 4 |
| V16 | Eigene Werte überall verallgemeinern | `Strg+Umschalt+G`, „Anwenden“ | 2 |
| V17 | Qualitätshinweise durchgehen | `F8`, dann je Fundstelle `F4` | 1, zuzüglich 1 je Fundstelle |
| V18 | Ziel der Aufnahme ändern | „Ändern“ in der Zielzeile, Text, `Strg+Eingabe` | 2 |
| V19 | In allen Sitzungen suchen und die Fundstelle öffnen | `Strg+Umschalt+F`, Suchtext, Treffer anklicken | 2 |
| V20 | Support-Paket nach der Aufnahme erstellen | „Weiter“, Eingaben prüfen, „Weiter“, „Paket erstellen“ | 4, zuzüglich 1 je auffälliger Eingabe |

**Kernabläufe** dienen als Abnahme für die Meilensteine ([11](11-roadmap-tests-risiken.md)):

| Kernablauf | Schritte | Aktionen ohne die aufgenommene Arbeit |
|---|---|---|
| 1 – aufnehmen, ansehen, kopieren | Aufnahme starten ([07](07-aufnahme-bedienung.md#15-bedienabläufe-mit-höchstzahl)), Stopp, V1, V2 | höchstens 3 |
| 2 – aufnehmen, bearbeiten, exportieren | Aufnahme starten, Stopp, V1, V9, V8, V13 | höchstens 8 |
| 3 – Support-Paket | Kachel „Support-Paket“, „Aufnahme starten“, Stopp, V20 | höchstens 7, zuzüglich 1 je auffälliger Eingabe; mit dem Befehl `support` eine weniger |

V14 bis V20 und Kernablauf 3 gehören zu Funktionen der Priorität Soll. In V20 zählt das Prüfen der Eingaben mit einer Aktion – „Alle unauffälligen als geprüft markieren“ – und einer weiteren je auffälliger Eingabe.

## 3 Öffnen und Schließen

| Weg | Verhalten |
|---|---|
| Nach dem Stopp einer Aufnahme | Der Viewer öffnet sich von selbst mit der neuen Sitzung ([07](07-aufnahme-bedienung.md#12-stopp-und-übergang-zum-viewer)). |
| Startfenster, „Zuletzt verwendet“ | Ein Klick öffnet die Sitzung. |
| Sitzungsbibliothek | „Alle Aufnahmen“, siehe [Abschnitt 11](#11-sitzungen-verwalten). `Strg+L` öffnet sie im Editor und im Startfenster. |
| Drag & Drop | Sitzungsordner oder `.steprec`-Datei auf das Fenster oder auf die EXE ziehen. |
| Kommandozeile | EXE mit dem Pfad der Sitzung starten ([02](02-architektur.md)). |
| Dialog „Sitzung öffnen“ | `Strg+O`. |

Weitere Festlegungen:

- **Keine Dateizuordnung:** Ein Doppelklick auf eine `.steprec`-Datei öffnet den Viewer nicht von selbst. Dafür müsste das Programm die Registry ändern, und das tut es ohne Ausnahme nicht. Geöffnet wird per Drag & Drop, über „Zuletzt verwendet“ und über die Kommandozeile.
- **Gepackte Sitzungen** (`.steprec`) öffnen sich direkt, aber nur zum Lesen: Eine Leiste „Schreibgeschützt (.steprec) – zum Bearbeiten bitte entpacken.“ mit der Schaltfläche „Entpacken …“ steht unter der Kopfleiste. Ansehen, Abspielen, Kopieren und Export funktionieren; Bearbeitungsstände gibt es nicht ([7.10](#710-bearbeitungsstände)). „Entpacken …“ legt eine bearbeitbare Aufnahme im Aufnahmeordner an (bei gleicher Aufnahme fragt das Programm nach) und öffnet sie anstelle des Archivs ([11](#11-sitzungen-verwalten)). Wie das Archiv aufgebaut ist und geprüft wird, regelt [04](04-datenformat.md).
- **Nur lesen:** Liegt die Sitzung auf einem schreibgeschützten Datenträger oder stammt sie aus einer neueren Formatversion, öffnet sie sich mit einem Hinweisbalken; Ansehen, Abspielen und Kopieren funktionieren, Bearbeiten nicht. „Kopie zum Bearbeiten anlegen“ steht im Balken.
- **Mehrere Sitzungen** öffnen sich in eigenen Fenstern. Wird eine bereits geöffnete Sitzung erneut geöffnet – etwa aus der Bibliothek –, kommt ihr Fenster nach vorn, auf Wunsch mit Sprung zu einem Schritt.
- **Schließen** fragt nichts: Alles ist gespeichert. „Neue Aufnahme“ führt zurück zum Startfenster.
- Beginnt eine Aufnahme, verschwindet der Viewer und kehrt nach dem Stopp zurück ([07](07-aufnahme-bedienung.md#11-unsichtbarkeitsgarantie-aus-nutzersicht)).

## 4 Aufbau des Fensters

### 4.1 Überblick

```
┌─ Datei im Editor speichern – Sebastians Step Recorder ─────────────────────────────────────────── – □ × ┐
│ Datei Bearbeiten Schritt Bild Ansicht Prüfen Hilfe        ↶ ↷ │ ▷ Abspielen 👁 Vorschau ☑ Durchsicht │ ⧉ Kopieren v [Exportieren …] │
├───────────────────────────┬──────────────────────────────────────────────────┬───────────────────────────┤
│ [Schritte durchsuchen   ] │ [Vorher|Nachher] [Ganzer Bildschirm v] 🖱 Mausmarken v   ‹ 2/4 › │ [Schritt|Technik]         │
│ [Schritte|Mit Spur] [DE|EN]│ ┌──────────────────────────────────────────────┐ │ Text                      │
│ 🗎 Titelseite und Einleitung│ │                                              │ │ ┌ Überschrift ──────────┐ │
│ 1 [Bild] Menüpunkt „Datei“ │ │               Bild des Schritts              │ │ │ Anleitungstext        │ │
│ 2 [Bild] „notiz.txt“ …     │ │                                              │ │ └───────────────────────┘ │
│ 3  ⚠  Lücke: Pause         │ └──────────────────────────────────────────────┘ │ Hinweise · Einordnung ·   │
│ 4 [Bild] Soll-Zustand      │                           [− 100 % + Einpassen] │ Bild in der Anleitung     │
├───────────────────────────┴──────────────────────────────────────────────────┴───────────────────────────┤
│ Schritt 2 von 4 · Automatisch gespeichert · Eingaben geprüft · Deutsch          11.11.2026 · 4 Hauptschritte │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

| Bereich | Inhalt | Ein- und ausblenden |
|---|---|---|
| Kopfleiste | Menüleiste (Datei · Bearbeiten · Schritt · Bild · Ansicht · Prüfen · Hilfe) und rechts die häufigsten Befehle: Rückgängig, Wiederholen, Abspielen, Vorschau, Durchsicht, Kopieren, Exportieren | immer sichtbar |
| Zielzeile (Soll) | Ziel der Aufnahme, erwartetes Ergebnis und Bezug ([4.4](#44-ziel-der-aufnahme-und-marker-typen)) | nur vorhanden, wenn ein Ziel angegeben ist; einklappbar |
| Schrittliste (links) | Vorschaubild, Nummer, Schrittart, Kurztext, Kennzeichen; Lücken als eigene Zeilen | `Strg+B` |
| Bildbereich (Mitte) | Werkzeugleiste, Bild, Umschalter für Vorher/Nachher/Änderung, Darstellung, Zoom | immer sichtbar |
| Detailbereich (rechts) | Schritttext, Kommentare, Ziel, Eingabe, Wirkung, Belegstufe, technische Details | `Strg+I` |
| Zeitleiste (unten) | Schritte, Lücken, Marker, Pausen auf der Zeitachse | `Strg+T` |
| Statusleiste | Position, Speicherstatus, aktive Schwärzungen, Stand der Eingaben-Durchsicht, Sprache der Texte | immer sichtbar |

Die Trennlinien sind verschiebbar; was das Programm sich merkt, steht in [4.5](#45-fensterlage-und-gemerkter-zustand). Bei schmalem Fenster klappt zuerst der Detailbereich ein. `F11` zeigt nur das Bild.

### 4.1a Menüführung

- **Eine Heimat je Funktion:** Jede Funktion des Editors steht genau einmal in der Menüleiste. Befehlsknöpfe, Rechtsklick-Menüs und Tastenkürzel sind Abkürzungen dorthin und tragen denselben Namen, dasselbe Symbol und dasselbe Kürzel. Grundlage ist ein Befehlskatalog im Programm, aus dem auch die Kürzelübersicht (Hilfe › Tastenkürzel, `F1`) entsteht.
- **Kurze Menüs:** höchstens etwa zehn Einträge, Gruppen durch Trennlinien, Tastenkürzel rechtsbündig in einer Spalte, „…“ bei Einträgen mit Dialog, Unumkehrbares (Endgültig schwärzen, Aufnahme verwerfen) zuletzt. Das Rechtsklick-Menü am Schritt folgt dem Menü „Schritt“ und fasst Kopieren, Bild und Einfügen in Untermenüs.
- **Auswahl links, Eigenschaften rechts:** Ganz oben in der Schrittliste steht „Titelseite und Einleitung“. Ausgewählt zeigt die Mitte eine Vorschau der Titelseite mit der Gliederung; rechts stehen die Einstellungen der Anleitung (Titelseite, Einleitung und Texte, Aufbau, Bilder, Gestaltung, Seite (Word und PDF), Vorlage, weitere Aufnahmen). „Seite“ wählt das Seitenformat (A4 oder Letter, hoch oder quer) und eine eigene Word-Vorlage (`.dotx`/`.docx`; „Word-Vorlage wählen …“, „Entfernen“; Makrovorlagen werden mit einer Meldung abgelehnt) – Einzelheiten in [09 §4.4](09-anleitungs-export.md#44-dokumentrahmen) und [§7.3](09-anleitungs-export.md#73-eigene-formatvorlage). Bei einem Schritt zeigt der Reiter „Schritt“ die Karten Text · Hinweise · Einordnung · Bild in der Anleitung, der Reiter „Technik“ die technischen Angaben.
- **Ein Begriff je Sache:** Hinweis (Kasten in der Anleitung; Arten Hinweis, Tipp, Achtung, Wichtig, Ergebnis, Intern), Kommentar (Bemerkung während der Aufnahme), Marker, Mausmarken (automatische Klick- und Tastenmarken), Zeichnungen (selbst gezeichnet), Bildausschnitt, Zuschnitt, Titelseite, Einleitung.

### 4.2 Schrittliste

- Jede Zeile zeigt Vorschaubild (drei Größen wählbar), Nummer in der Anleitung, Symbol der Schrittart und den Kurztitel des Schritts.
- Kennzeichen an der Zeile: Kommentar vorhanden, Schwärzung vorhanden, Text geändert, Belegstufe „nur Eingabe“ oder „unbekannt“, ausgeblendet, verworfen, Marker.
- **Lücken**, die Eingaben betreffen können, stehen als eigene, schraffierte Zeilen an ihrer zeitlichen Stelle, mit Ursache und Dauer (Schrittart „Lücke“, [05](05-schritterkennung.md#schrittarten)). Die übrigen – etwa ein einzelnes ausgelassenes Bild oder die Bedienung des Recorders – stehen in Zeitleiste und Lückenliste.
- Der Umschalter **Haupt | Alle** zeigt nur Hauptschritte oder alle Schritte einschließlich Nebenschritten und Spurdaten ([05](05-schritterkennung.md#wichtigkeitsstufen)). Marker sind zusätzlich als Fähnchen sichtbar; das Fähnchen zeigt den Typ des Markers ([4.4](#44-ziel-der-aufnahme-und-marker-typen)). Ein Marker „Notiz“ ist ein Nebenschritt, die Typen „Fehler“, „Erwartet“ und „Wichtig“ sind Hauptschritte.
- Kapitel erscheinen als einklappbare Überschriften.
- **Reihenfolge:** Die Liste folgt der Reihenfolge der Anleitung, auch nach dem Umsortieren ([9.2](#92-aktionen)). Ausgeblendete Schritte behalten ihren Platz und erscheinen grau; „Nach oben“ und „Nach unten“ rechnen auf derselben Folge.
- **Zusammengefasste Schritte** ([9.2](#92-aktionen)): Der führende Schritt trägt das Kennzeichen „N zusammengefasst“, die übrigen Mitglieder „→ Schritt N“.
- **Qualität:** Ein Schritt mit einer Warnung der Qualitätsprüfung trägt ein Kennzeichen mit der Regelnummer ([12.1](#121-qualitätshinweise)).
- Nummeriert sind die sichtbaren Hauptschritte in der Reihenfolge der Anleitung. Die Kennung des Schritts (etwa `s1734`) ändert sich nie und steht in den technischen Details; über sie lassen sich Anleitung und KI-Protokoll einander zuordnen.

### 4.3 Sitzungsübersicht

Ein Klick auf das Info-Symbol neben dem Titel öffnet die Übersicht der Sitzung:

- Dauer, Zahl der Schritte (Haupt/alle), beteiligte Anwendungen, Aufnahmebereich, Monitore.
- **Vollständigkeitsbericht** mit beiden Aussagen: Buchführung und Belegqualität ([05](05-schritterkennung.md)).
- Liste aller Lücken mit Grund und Dauer; ein Klick springt zur Stelle.
- Datenschutz: Zahl maskierter Eingaben, Schwärzungen, Stand der Eingaben-Durchsicht; Vermerk, falls im Ausweichmodus aufgenommen wurde.
- Speichergröße der Sitzung.
- Schaltflächen: Export, KI-Paket erzeugen ([06](06-ki-export-und-mcp.md#31-erzeugen-viewer-und-kommandozeile)), Im Explorer zeigen.
- Aufnahmeprofil der Sitzung und – falls die Aufnahme schwächer schützte als die Einstellungen – worin ([07](07-aufnahme-bedienung.md#47-aufnahmeprofile)); Ziel der Aufnahme mit „Ziel hinzufügen“ bzw. „Ändern“ (Soll).
- Aufnahmeabschnitte der Sitzung: Fortsetzungen, Neuaufnahmen mit dem, was sie ersetzen, nicht verwendete Neuaufnahmen und verworfene Abschnitte – diese nur mit Zeitpunkt und der Aktion „Wieder aufnehmen“, nie mit Inhalt ([9.3](#93-abschnitt-neu-aufnehmen-und-ersetzen)) (Soll).

### 4.4 Ziel der Aufnahme und Marker-Typen

**Ziel im Sitzungskopf (Soll).** Hat der Autor ein Ziel angegeben ([07](07-aufnahme-bedienung.md#48-ziel-der-aufnahme)), steht es als Zielzeile unter der Kopfleiste:

```
┌─ 2026-11-11 14.30 – Editor – notiz.txt ──────────────────────────────────────────────┐
│ [Neue Aufnahme] [Abspielen]  Suche: [__________] [Filter v]  [Kopieren v] [Export v] │
├──────────────────────────────────────────────────────────────────────────────────────┤
│ Ziel: Rechnung als PDF speichern · Erwartet: Die PDF-Datei liegt im Ordner           │
│ „Rechnungen“ · Bezug: Ticket 4711                                           [Ändern] │
├──────────────────────────────────────────────────────────────────────────────────────┤
│ SCHRITTE  23  ...                                                                    │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

- Die Zielzeile zeigt, was vorhanden ist: „Ziel“, „Erwartet“, „Bezug“. Lange Texte sind gekürzt; ein Klick klappt sie auf.
- **Ändern** macht die drei Felder bearbeitbar; `Strg+Eingabe` schließt ab, `Esc` verwirft. Hat die Sitzung noch kein Ziel, legt „Ziel hinzufügen“ in der [Sitzungsübersicht](#43-sitzungsübersicht) eines an.
- Die Änderung steht im Overlay: `edits.goal` überlagert `manifest.goal` Feld für Feld ([04](04-datenformat.md#edits-goal)). Die Aufnahme bleibt unverändert. Ein geleertes Feld nimmt die Angabe aus allen Ausgaben; „Auf den Stand der Aufnahme zurücksetzen“ entfernt die Änderung.
- Ziel, erwartetes Ergebnis und Bezug sind **Texte des Autors**: Sie stehen in der [Durchsicht der Eingaben](#13-durchsicht-der-eingaben), lassen sich schwärzen und verallgemeinern und sind über die Suche auffindbar. Ein KI-Agent kann sie nicht schreiben.
- In den Ausgaben erscheinen sie stets als Angabe des Autors, nie als Beobachtung: im Kopf des KI-Pakets ([06](06-ki-export-und-mcp.md#76-angaben-des-autors-ziel-der-aufnahme-und-marker)) und – wenn die Option gesetzt ist – am Anfang der Anleitung ([09](09-anleitungs-export.md#46-ziel-marker-neuaufnahmen-und-platzhalter)).

**Marker-Typen (Soll).** Marker aus der Aufnahme tragen einen Typ ([07](07-aufnahme-bedienung.md#92-marker)). Der Viewer zeigt ihn mit Zeichen und Wort, nie nur mit Farbe:

| Typ | Zeichen am Fähnchen | In der Schrittliste |
|---|---|---|
| `note` | `F` | „Marker 3“ |
| `error` | `!` | „Marker 4 · Fehler: Hier tritt der Fehler auf“ |
| `expected` | `✓` | „Marker 5 · Erwartet: So sollte es aussehen“ |
| `important` | `★` | „Marker 6 · Wichtig“ |

- Schrittliste und Zeitleiste zeigen das Zeichen des Typs; Überfahren nennt Typ und Text.
- **Marker-Text:** Hat der Autor dem Marker bei der Aufnahme eine Bezeichnung mitgegeben, steht sie in Schrittliste und Detailbereich hinter dem Typ. Sie ist Text des Autors – Teil der [Durchsicht](#13-durchsicht-der-eingaben), schwärzbar und über die Suche auffindbar.
- Der Filter „Marker“ schränkt auf gewählte Typen ein ([5.6](#56-suche-und-filter)). `Strg+→` und `Strg+←` springen zum nächsten und vorigen Marker.
- Ein Klick auf das Fähnchen legt wie bisher einen Kommentar an dieser Stelle an ([Abschnitt 8](#8-kommentieren)).
- **Typ ändern:** Das Kontextmenü des Markers bietet die vier Typen an. Die Änderung steht im Overlay (`steps[].markerKind`, [04](04-datenformat.md#ev-marker)); das Rohprotokoll bleibt unverändert. Mit dem Typ wechselt die Wichtigkeit, sofern der Autor sie nicht eigens gesetzt hat ([05](05-schritterkennung.md#schrittarten)).
- **Wichtigkeit:** „Fehler“, „Erwartet“ und „Wichtig“ sind Hauptschritte und stehen damit in der Anleitung – als Hinweis mit dem Bild des Augenblicks ([09](09-anleitungs-export.md#46-ziel-marker-neuaufnahmen-und-platzhalter)). „Notiz“ ist ein Nebenschritt für den Autor.
- Der Typ ist eine Angabe des Autors, keine Beobachtung. Im Vergleich zweier Aufnahmen und im KI-Paket erscheint er mit.

### 4.5 Fensterlage und gemerkter Zustand

Das Programm merkt sich, wie der Autor arbeitet, damit er an der Stelle weitermachen kann, an der er aufgehört hat. Gespeichert wird das im flüchtigen Zustand des Programms (`StepRecorder.state.json`), nie in der Aufnahme; die Datei enthält keine Inhalte der Aufnahmen. Ist sie unlesbar, beginnt das Programm mit den Vorgaben.

| Was | Gilt für | Verhalten |
|---|---|---|
| Fensterlage und -größe, Maximierung | Startfenster, Editor, „Alle Aufnahmen“ (je Fenster) | Das Fenster öffnet dort, wo es geschlossen wurde. Die Lage wird an die heutigen Monitore und ihre Skalierung angepasst: Fehlt der Monitor oder ist die Fläche kleiner, rückt das Fenster in den sichtbaren Bereich. |
| Breite der Schrittliste und des Detailbereichs, gewählter Reiter im Detailbereich | alle Aufnahmen | Wie zuletzt eingestellt, soweit die Mindestbreiten es erlauben. |
| Zuletzt gewählter Schritt | je Aufnahme | Eine Aufnahme öffnet am zuletzt gewählten Schritt. |
| Filter „Alle“ (statt „Haupt“) und Bildansicht (Darstellung) | je Aufnahme | Wie zuletzt eingestellt. |

- Der Zustand der letzten 50 Aufnahmen bleibt erhalten, ältere fallen weg.
- Beim Öffnen aus der Bibliothek oder aus einem Suchtreffer hat der gewünschte Schritt Vorrang vor dem gemerkten.

### 4.6 Sprache der Oberfläche

Alle Fenster und Meldungen sind zweisprachig (Deutsch, Englisch). Die Sprache wählt die Einstellung „Oberflächensprache“: dem System folgen, Deutsch oder Englisch. Davon unabhängig ist die Sprache der Anleitungstexte ([9.1](#91-texte)), die in der Statusleiste steht.

- Texte, die in die Aufnahme oder in eine Ausgabe gelangen – ein neues Kapitel, ein eingefügter Schritt, die Kopie als Text oder Markdown –, folgen der Sprache der Anleitungstexte, nicht der Oberfläche.
- Tastennamen („Strg“ oder „Ctrl“), Datum und Zahlen folgen der Oberflächensprache. Der Name eines gespeicherten Detailgrads bleibt deutsch, angezeigt wird er übersetzt.
- Die Kürzelübersicht (`F1`) und die Menüs zeigen die Tastennamen der Oberflächensprache; die Beschreibungen in dieser Spezifikation nennen die deutschen.

## 5 Ansehen

### 5.1 Bild

- **Zoom:** Einpassen (Standard), 100 %, stufenlos mit `Strg+Mausrad` oder `Strg++` und `Strg+-`. Bei 100 % entspricht ein Bildpixel einem Bildschirmpixel.
- **Verschieben:** mittlere Maustaste oder `Leertaste+Ziehen`.
- **Standardbild je Schrittart** – welches Bild ein Schritt zuerst zeigt:

| Schrittart | Standardbild | Automatische Markierung |
|---|---|---|
| Klick, Doppelklick, Dreifachklick, Rechtsklick, Verweilen | Vorher-Bild | Klickmarke, Elementrahmen |
| Ziehen | Vorher-Bild | Start, Ziel und Weg |
| Texteingabe | Nachher-Bild (zeigt das ausgefüllte Feld) | Elementrahmen |
| Tastenkombination, Einzeltaste | Nachher-Bild, falls sich etwas geändert hat, sonst Vorher-Bild | – |
| Scrollen, Fensterwechsel, Systemereignis, Warten | Nachher-Bild | – |
| Kommentar, Schnappschuss | Das eingefrorene bzw. festgehaltene Bild | markierter Bereich |
| Marker | Vorher-Bild | – |
| Lücke | Vorher-Bild (letzter Stand davor); das Nachher-Bild zeigt den Stand danach | – |
| Mausbewegung ohne Aktion | Vorher-Bild | Mausweg |

Welche Bilder einem Schritt als Vorher-, Nachher- und Zwischenbilder zugeordnet sind, legt [05](05-schritterkennung.md#bildzuordnung) fest. Hier geht es nur darum, welches davon zuerst gezeigt wird.


- Bei Aufnahmen aller Monitore steht über dem Bild ein Umschalter für den Monitor (Soll).

### 5.2 Darstellung

Die Darstellung bestimmt, welcher Teil des gespeicherten Bildes gezeigt wird. Sie ändert nichts an den gespeicherten Daten.

| Darstellung | Taste | Zeigt | Verfügbar |
|---|---|---|---|
| Ganzer Bildschirm | `1` | Das gesamte gespeicherte Bild | Immer. Bei Aufnahmen einer Anwendung oder eines Bereichs heißt der Eintrag „Gesamte Aufnahme“ und zeigt alles Gespeicherte. |
| Anwendungsfenster | `2` | Das Fenster, in dem die Aktion stattfand, samt den in diesem Schritt offenen Menüs und Dialogen | Wenn Fensterdaten vorliegen |
| Ausschnitt um die Aktion | `3` | Den Standard-Ausschnitt um das Ziel (Regel unten) | Wenn der Schritt eine Position hat; sonst gilt „Anwendungsfenster“ |
| Frei | `C` | Den vom Autor gezogenen Zuschnitt | Immer |

Drei Ebenen, die spätere überschreibt die frühere:

1. **Sitzung:** Standard für alle Schritte, gewählt in der Kopfleiste. Neue Sitzungen beginnen mit „Ganzer Bildschirm“ (einstellbar).
2. **Schritt:** Abweichung für die gewählten Schritte über die Tasten `1` bis `3` oder den Zuschnitt.
3. **Export:** Ein Export kann eine Darstellung für alle Schritte vorgeben ([09](09-anleitungs-export.md)).

In `edits.json` heißen die Darstellungen `screen`, `window`, `focus` und `custom` ([04](04-datenformat.md#edits-json)). Zur Darstellung der Sitzung gehört auch, ob die Anleitung zusätzlich ein Nachher-Bild zeigt: nie (Standard), bei Änderung oder immer.

**Regel für den Standard-Ausschnitt** (gilt für Viewer und Anleitungs-Export; das KI-Paket darf abweichen):

1. Zielrechteck ist das Rechteck des Zielelements, sofern es als zuverlässig gilt; sonst der Punkt der Aktion. Bei Ziehen ist es das umschließende Rechteck von Start, Ziel und Weg, bei Texteingabe das Eingabefeld.
2. Um das Zielrechteck kommt ein Rand von 160 Pixeln je Seite.
3. Das Ergebnis wird auf mindestens 800 × 450 Pixel vergrößert, mittig um das Ziel.
4. Es wird auf das Anwendungsfenster des Schritts samt offener Menüs und Dialoge begrenzt. Ist das Fenster kleiner als die Mindestgröße, gilt das Fenster.
5. Ragt es über den Bildrand, wird es ins Bild geschoben.

Die Pixelwerte gelten bei 100 % Anzeigeskalierung und wachsen mit der Skalierung des aufgenommenen Monitors. Die Größe ist dreistufig wählbar (eng, normal, weit: Faktor 0,6, 1 und 1,6).

Für „Anwendungsfenster“ gelten die sichtbaren Fenstergrenzen plus 8 Pixel Rand. Bildteile außerhalb abgerundeter Fensterecken werden neutral gefüllt (Soll), damit dort kein fremder Inhalt durchscheint.

### 5.3 Vorher, Nachher, Änderung

- **Vorher | Nachher** schaltet zwischen den beiden Bildern des Schritts um (`W`).
- **Änderung** (`D`) zeigt, was sich zwischen Vorher- und Nachher-Bild geändert hat: Unveränderte Flächen sind abgedunkelt, geänderte umrandet. Darunter steht die erkannte Wirkung in Worten („Dialog geschlossen“, „Fenstertitel geändert“).
- **Nebeneinander** zeigt beide Bilder zugleich (Soll); ein Schieber zum Überblenden ist Kann.
- **Zwischenbilder:** Gibt es zu einem Schritt weitere Bilder, zeigt eine Zeile unter dem Bild ihre Anzahl; ein Klick klappt den Bildstreifen auf.

### 5.4 Galerie

Die Galerie zeigt die Bilder aller Schritte als Raster – für den schnellen Überblick und zum Auswählen mehrerer Schritte. Doppelklick wechselt zurück in die Schrittansicht. Mehrfachauswahl, Kopieren und Ausblenden funktionieren wie in der Schrittliste.

### 5.5 Zeitleiste

- Waagerechte Zeitachse der Sitzung. Jeder Schritt ist ein Strich (Hauptschritte höher), Lücken sind schraffiert, Marker tragen ein Fähnchen, Pausen sind grau.
- Ein Farbband zeigt, welche Anwendung jeweils im Vordergrund war.
- Klick oder Ziehen springt zum Schritt an dieser Stelle; Überfahren zeigt Vorschaubild und Zeit (Soll).
- Lange Zeiten ohne Eingabe sind gestaucht (abschaltbar); `Strg+Mausrad` dehnt und staucht die Achse.
- Überfahren einer Lücke nennt Grund und Dauer.

### 5.6 Suche und Filter

*Reaktionszeiten (Paket 9 aus [12](12-entwickler-fahrplan.md)):* Schritte, nach denen die Anwendung mindestens 0,5 s brauchte, tragen in der Schrittliste ein Abzeichen mit der Wartezeit (ab 2 s hervorgehoben, Tooltip mit erster Reaktion und „bereit nach“). Der Umschalter „Langsam“ über der Liste zeigt nur Schritte ab 2 s. Der Detailbereich nennt unter „Warten“ zusätzlich die erste Reaktion; die Sitzungsübersicht die Summe aus Wartezeit der Anwendung und Denkzeit sowie die Zahl langsamer Schritte. Der Vergleich (5.9) meldet deutlich verschiedene Reaktionszeiten als Unterschied „Reaktion a → b“.

- **Suche** (`Strg+F`) durchsucht Überschrift und Text der Schritte (Anleitungs- und Protokolltext, eigene Texte), die Titel und Texte der Kapitel, Elementnamen, getippten Text, Fenstertitel, Kommentare einschließlich interner Notizen und die Text-Schnappschüsse neuer Fenster. Treffer sind in der Schrittliste hervorgehoben; `F3` und `Umschalt+F3` springen weiter.
- **Suchregeln:** Ohne Beachtung der Groß- und Kleinschreibung. Mehrere durch Leerzeichen getrennte Wörter müssen **alle** vorkommen (UND); Wörter in Anführungszeichen („…“ oder "…") suchen die genaue **Wortfolge**. Mehrfache Leerzeichen und Zeilenumbrüche zählen als ein Leerzeichen. Dieselben Regeln gelten in der Kürzelübersicht (`F1`) und in der Bibliothek ([11.2](#112-volltextsuche-über-alle-sitzungen)).
- Maskierte und geschwärzte Inhalte sind nicht durchsuchbar.
- **Filter:** Anwendung, Schrittart, Wichtigkeitsstufe, mit Kommentar, bearbeitet, mit Schwärzung, Belegstufe, ausgeblendet, verworfen, „zu prüfen“ (Belegstufe nicht „belegt“ oder ungeprüfte Eingabe). Mit den zugehörigen Funktionen kommen hinzu (Soll): Marker nach Typ, ersetzt, neu aufgenommen, mit Qualitätshinweis.

### 5.7 Lücken und Unsicherheit

- Lücken stehen in Zeitleiste und Lückenliste, die meisten auch in der Schrittliste ([4.2](#42-schrittliste)) – jeweils mit dem Ursachentext aus [05](05-schritterkennung.md#textvorlagen), etwa „Arbeit außerhalb des Aufnahmebereichs; nicht aufgezeichnet“.
- Die Belegstufe (belegt, nur Eingabe, unbekannt) steht im Detailbereich und als Kennzeichen an der Zeile, wenn sie nicht „belegt“ ist. Überfahren zeigt die Begründung aus der Belegzeile ([05](05-schritterkennung.md#belegstufen)), etwa „Ziel nicht bestätigt“.
- Maskierte Eingaben erscheinen als „Eingabe maskiert (8 Tastenanschläge)“.
- Der Filter „zu prüfen“ führt den Autor zu allen Stellen, an denen er genauer hinsehen sollte.

### 5.8 Technische Details

Eingeklappt im Detailbereich; für Autoren, die es genau wissen wollen:

- Kennung des Schritts und seine Beschreibung im Protokollstil mit Belegzeile – so, wie er im KI-Protokoll steht –, dazu die neutrale Kurzform der Aktion ([05](05-schritterkennung.md#aktionsvokabular)).
- Rohereignisse des Schritts als Tabelle (Nummer, Zeit, Art, Kurzfassung).
- Alle Eigenschaften des Zielelements und des Fensters.
- Bilder des Schritts mit Kennung, Zeit und Rolle.
- Text-Schnappschuss des Fensters.
- Zeiten: Dauer des Schritts, Wartezeit davor, Zeit bis zur Reaktion.
- „Als JSON kopieren“.

Auch hier gelten Schwärzungen.

### 5.9 Zwei Aufnahmen vergleichen

Der Vergleich stellt zwei Sitzungen desselben Ablaufs nebeneinander und zeigt, wo sie sich unterscheiden (Soll) – etwa „so war es vor dem Update, so ist es danach“ oder „so geht es bei mir, so beim Kollegen“.

```
┌─ Vergleich ───────────────────────────────────────────────────────────────────────────────┐
│ A  2026-11-10 09.12 – Editor – notiz.txt        B  2026-11-11 14.30 – Editor – notiz.txt  │
│ [Nur Unterschiede | Alle]  [Haupt | Alle]       2 geändert · 1 nur in A · 1 nur in B      │
├────┬──────────────────────────────┬──────────────────────────────┬────────────────────────┤
│    │ A                            │ B                            │ Unterschied            │
├────┼──────────────────────────────┼──────────────────────────────┼────────────────────────┤
│ =  │ 1 Klick „Datei“              │ 1 Klick „Datei“              │ gleich                 │
│ ≠  │ 2 Klick „Speichern“          │ 2 Klick „Speichern unter…“   │ Ziel, Text             │
│ A  │ 3 Klick „OK“                 │ –                            │ nur in A               │
│ B  │ –                            │ 3 Text in „Dateiname“        │ nur in B               │
│ >≠ │ 4 Klick „Speichern“          │ 4 Klick „Speichern“          │ Wirkung, Dauer         │
├────┴──────────────────────────────┴──────────────────────────────┴────────────────────────┤
│ Paar 4 · geändert         [ Bild A ]                    [ Bild B ]                        │
│ Wirkung:  Dialog geschlossen  →  Meldung „Datei existiert bereits“                        │
│ Dauer:    0,2 s  →  3,1 s                                                                 │
└───────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Aufruf:** `F7` oder „Vergleichen mit …“ im Menü der Kopfleiste; die zweite Sitzung wird aus „Zuletzt verwendet“ oder der Sitzungsbibliothek gewählt. In der Bibliothek lassen sich auch zwei Sitzungen markieren und mit „Vergleichen“ öffnen. Die geöffnete Sitzung ist A, die gewählte B; „Seiten tauschen“ dreht das um.
- **Abgleich:** Welche Schritte zusammengehören und was als Unterschied gilt, bestimmt allein das Verfahren aus [05](05-schritterkennung.md#schrittabgleich) – reihenfolgeerhaltend, über Aktion und Zielidentität, mit Toleranz für zusätzliche Neben- und Spurschritte. Der Viewer zeigt das Ergebnis; er rechnet nicht selbst ([02](02-architektur.md#schrittabgleich)).
- **Kennzeichnung je Paar:**

| Zeichen | Kennzeichnung | Bedeutung |
|---|---|---|
| `=` | gleich | Beide Schritte stimmen überein |
| `≠` | geändert | Die Schritte gehören zusammen, unterscheiden sich aber. Benannt ist, worin: **Ziel**, **Text**, **Wirkung**, **Dauer**, **Bild** |
| `A` | nur in A | Der Schritt hat in B kein Gegenstück |
| `B` | nur in B | Der Schritt hat in A kein Gegenstück |

- **Ansicht:** oben die Paarliste, unten zum gewählten Paar beide Bilder in derselben Darstellung und daneben die Unterschiede in Worten, jeweils als „A → B“. Zoom und Bildausschnitt beider Bilder laufen gemeinsam.
- **Umfang:** „Nur Unterschiede“ (Standard) oder „Alle“; Hauptschritte oder alle Schritte. Mit „alle Schritte“ erscheinen auch Begleitschritte, die nur eine Seite hat – Neben- und Spurschritte, Systemereignisse, Kommentare, Marker.
- **Was verglichen wird:** die wirksame Schrittfolge jeder Sitzung. Ersetzte Schritte, nicht verwendete Neuaufnahmen und verworfene Abschnitte bleiben außen vor.
- **Lücken** verschweigt der Vergleich nie: Ein Lückenschritt hat kein Gegenstück und steht immer als „nur in A“ oder „nur in B“ da.
- **Bilder vergleichen** ist ein Schalter, der zunächst aus ist. Erst mit ihm kann „Bild“ als Unterschied erscheinen; ohne ihn steht das Merkmal bei den unverglichenen Angaben. Der Viewer zeigt die Paarliste sofort und trägt Bildunterschiede nach.
- **Navigation:** `↑` und `↓` wechseln das Paar; `F4` und `Umschalt+F4` springen zum nächsten und vorigen Unterschied. Ein Doppelklick auf eine Seite öffnet diese Sitzung an diesem Schritt.
- **Nur ansehen:** Im Vergleich wird nichts bearbeitet und nichts gespeichert. Beide Sitzungen bleiben unverändert. Ausnahme auf ausdrücklichen Wunsch: „Als Markdown mit Bildern …“ schreibt den Bericht und je Unterschied ein Vorher/Nachher-Bild (A links, B rechts mit hervorgehobenen Bereichen) in eine gewählte Datei samt Ordner `<name>-bilder/` – für Reviews und Pull Requests.
- **Bildabgleich (Umsetzung, 12 Paket 19):** Verglichen wird das Bild, das Anleitungen zeigen, ohne Einzeichnungen. Raster aus 16-Pixel-Zellen; ein Pixel gilt als geändert ab einer mittleren Farbabweichung von 32 (einstellbar, `--threshold`), eine Zelle ab 4 % geänderter Pixel, „Bild“ als Unterschied ab 0,5 % geänderter Zellen oder abweichender Bildgröße. Benachbarte Zellen bilden Bereiche, die in B hervorgehoben werden. Masken (Anteile des Bildes, etwa für die Uhr) blenden Bereiche aus. Geschwärzte Schritte und fehlende Bilder bleiben „unverglichen“. Zoom (Strg+Mausrad) und Bildausschnitt beider Bilder laufen gemeinsam.
- **Über die Leseschicht, ohne Orakel:** Für jede der beiden Sitzungen gelten ihre Schwärzungen, Verallgemeinerungen und Ersetzungen. Was zurückgehalten ist, geht nicht in den Vergleich ein und wird nie als gleich oder verschieden gemeldet; das Paar nennt stattdessen, welche Angaben unverglichen blieben. So lässt sich ein geschwärzter Inhalt nicht erraten, indem jemand eine zweite Sitzung mit Vermutungen danebenlegt.
- **Ausgabe:** „Vergleich speichern“ schreibt den Bericht als Markdown oder JSON, „Vergleich kopieren“ legt ihn in die Zwischenablage. Beides entspricht dem Befehl `compare` ([02](02-architektur.md#befehle-nachgelagert)) und dem MCP-Werkzeug `compare_sessions` ([06](06-ki-export-und-mcp.md#199-compare_sessions)) und gilt in deren Sicht: Ungeprüfte auffällige Eingaben stehen darin als `{unreviewed:N}`. Gleiche Sitzungen und gleiche Einstellungen ergeben eine bytegleiche Datei. Standardziel ist der Ordner `export/` der Sitzung A.
- **Bildunterschied hervorheben** (Kann): Zeigen beide Bilder denselben Ausschnitt in gleicher Größe, umrandet der Viewer die abweichenden Flächen.
- **Grenzen:** Verglichen wird, was aufgezeichnet ist. Unterscheiden sich Aufnahmebereich, Auflösung oder Sprache der Anwendung, nennt die Kopfzeile das; „Bild“ ist dann häufig als Unterschied gekennzeichnet. Verglichen werden nur abgeschlossene Sitzungen – auch zwei Fassungen derselben Aufnahme, etwa Original und bereinigte Kopie. Hat eine Sitzung mehr als 5 000 Kernschritte, wird nicht abgeglichen; der Viewer sagt das ([05](05-schritterkennung.md#schrittabgleich)). Der Vergleich urteilt nicht, welche Sitzung „richtig“ ist.
- **Geschwindigkeit:** Für zwei Sitzungen mit je 500 Schritten steht die Paarliste auf System A in höchstens 2 Sekunden; bis dahin zeigt der Viewer einen Fortschritt und bleibt bedienbar.

### 5.10 Seitenansicht und Drucken

Die Mitte zeigt wahlweise das Bild des Schritts oder die **Seiten der fertigen Anleitung** – so, wie das PDF sie enthält ([13](13-handbuch-fahrplan.md), Paket 20).

- **Umschalten:** „Bild | Seite“ über dem Bild, `Strg+F2`, Knopf „Seitenansicht“ in der Kopfleiste oder „Ansicht → Seitenansicht“. Der Eintrag „Titelseite und Einleitung“ zeigt immer die Seiten von vorn.
- **Aktualität:** Nach jedem Speichern wird das PDF nach einer kurzen Pause neu aufgebaut, im Hintergrund und abbrechbar. Gerenderte Schrittbilder bleiben im Arbeitsspeicher, solange sich weder das Bild noch Schwärzungen, Verallgemeinerungen oder die Durchsicht ändern; auf die Platte kommt nichts. Die Lage im Dokument bleibt beim Aktualisieren erhalten.
- **Auswahl in beide Richtungen:** Ein Klick auf einen Schritt in den Seiten wählt ihn in der Liste, ein Doppelklick wechselt zu seinem Bild. Wird ein Schritt gewählt, springt die Seitenansicht zu ihm und umrandet ihn kurz. Schritte angehängter Aufnahmen sind sichtbar, aber nur in ihrer eigenen Aufnahme bearbeitbar.
- **Ungeprüfte Eingaben** erscheinen in der Seitenansicht als Platzhalter; ein Hinweisband sagt das. Export und Druck warten weiter auf die Durchsicht.
- **Zoom:** Seitenbreite (Standard), `Strg+Mausrad`, `Strg+Plus`/`Strg+Minus`, `Strg+0` ganze Seite, `Strg+1` Seitenbreite. Seiten werden erst gerastert, wenn sie sichtbar werden.
- **Drucken** (`Strg+P`, „Datei → Drucken …“): nach der Durchsicht; Windows-Druckdialog mit Seitenbereich; die Seiten werden mit 200 dpi im Hintergrund vorbereitet, mit Fortschritt und Abbrechen.
- **Rückfall:** Fehlt der PDF-Renderer von Windows (`Windows.Data.Pdf`, etwa auf Windows Server ohne Desktopdarstellung), bleibt die nachgebaute Titelseite; „Vorschau im Browser“ und der PDF-Export funktionieren weiter.

## 6 Abspielen

Die Wiedergabe zeigt den Ablauf als Bildfolge mit bewegtem Mauszeiger. Sie macht sichtbar, was die Aufzeichnung über die Einzelbilder hinaus enthält: Mausweg, Tastenfolge, Wartezeiten.

- **Start:** `F5` oder „Abspielen“, ab dem gewählten Schritt. `Leertaste` hält an und setzt fort, `Esc` beendet.
- **Bildfolge:** Vorher-Bild, dann die Mausbewegung auf dem aufgezeichneten Weg, die Klickanimation, das Nachher-Bild zur aufgezeichneten Reaktionszeit. Zwischenbilder erscheinen an ihrer zeitlichen Stelle. Es wird hart umgeschaltet, nicht überblendet – eine Überblendung würde einen Verlauf vortäuschen, der nicht aufgezeichnet ist.
- **Mausweg:** Der Zeiger folgt dem vollständigen aufgezeichneten Pfad. Ziehen zeichnet den Weg als Linie nach. Ein Klick erscheint als kurze Ringanimation; Rechts- und Doppelklick sind unterscheidbar.
- **Tasten:** Eine Einblendung am unteren Bildrand zeigt Tastenkombinationen als Tastenkappen („Strg + S“) und baut getippten Text Zeichen für Zeichen auf. Maskierte Eingaben erscheinen als Punkte.
- **Untertitel:** Der Schritttext steht unter dem Bild (abschaltbar).
- **Geschwindigkeit:** 0,5-fach bis 4-fach. „Leerlauf überspringen“ (Standard an) kürzt Zeiten ohne Eingabe auf höchstens eine Sekunde; „Echtzeit“ spielt mit den aufgezeichneten Abständen.
- **Schrittweise:** `→` und `←` springen zum nächsten oder vorigen Schritt, auch während der Wiedergabe.
- **Umfang:** Hauptschritte oder alle Schritte, wie in der Schrittliste gewählt. Ausgeblendete Schritte werden übersprungen.
- **Zeitleiste:** Ein Zeiger läuft mit; Ziehen daran spult.
- **Vollbild:** `F11`, auch während der Wiedergabe.
- Lücken erscheinen als kurze Einblendung mit Grund und Dauer.
- Die Wiedergabe ändert nichts an der Sitzung. Sie steuert keine Programme; sie zeigt nur Aufgezeichnetes.

Die Ausgabe der Wiedergabe als GIF oder Video ist Kann.

## 7 Bilder bearbeiten

### 7.1 Grundsätze

- Bearbeitet wird direkt im Bildbereich. Werkzeug wählen, zeichnen – fertig.
- **Reihenfolge beim Zeichnen des Bildes**, gleich für Anzeige, Kopie und Export:
  1. gespeichertes Bild (oder ein vom Autor ersetztes Bild),
  2. Schwärzungen,
  3. Zuschnitt nach der Darstellung,
  4. automatische Markierungen,
  5. Markierungen des Autors,
  6. Skalierung auf die Zielgröße.
- Markierungen sind an **Bildkoordinaten des gespeicherten Bildes** gebunden. Beim Wechsel der Darstellung bleiben sie an derselben Stelle des Inhalts.
- Markierungen gehören zum **Schritt und Bild** (Vorher oder Nachher): Dasselbe gespeicherte Bild kann in zwei Schritten unterschiedliche Markierungen tragen. Eine Markierung erscheint **nur auf dem Bild, auf dem sie gezeichnet wurde** – im Editor, in Kopien und in jedem Export. Eine Markierung auf dem Vorher-Bild fehlt auf dem Nachher-Bild und umgekehrt.
- **Eine Geometrie für alle:** Wo ein Objekt getroffen wird, welche Griffe es hat und wie es sich verschiebt oder in der Größe ändert, rechnet ein gemeinsamer Baustein im Kern; Editor und Export zeichnen dieselben Formen und kommen deshalb zum selben Bild.
- Schwärzungen gehören dagegen zum **gespeicherten Bild** und gelten überall, wo es vorkommt ([7.6](#76-schwärzen-und-verpixeln)).

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ [V][A][R][O][T][N][M][L][C][S][X][P] │ Farbe [#] Stärke [3 v] Stil [Standard v] │ <- ->│
├────────────────────────────────────────────────────────────────────────────────────────┤
│   ┌────────────────────────────────────────────────────────────────┐                   │
│   │ Speichern unter                                              x │                   │
│   │                                                                │                   │
│   │  Dateiname: [notiz.txt            ]     ╔═══════════╗          │                   │
│   │  Dateityp:  [Textdateien        v ]     ║ Speichern ║ <─── (1) │                   │
│   │                                         ╚═══════════╝          │                   │
│   │  Ordner:    ████████████████████  (geschwärzt)   [Abbrechen]   │                   │
│   └────────────────────────────────────────────────────────────────┘                   │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ Pfeil · gewählt · Am Element einrasten: an │ Schwärzung gilt für 14 Bilder │ 100 %     │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### 7.2 Werkzeuge

| Werkzeug | Taste | Zweck | Einstellbar | Priorität |
|---|---|---|---|---|
| Auswahl | `V` | Objekte wählen, verschieben, in der Größe ändern, löschen | – | Muss |
| Pfeil | `A` | Auf etwas zeigen | Farbe, Stärke | Muss |
| Linie | `Umschalt+A` | Gerade Linie ohne Spitze | Farbe, Stärke | Soll |
| Rechteck | `R` | Etwas einrahmen | Farbe, Stärke, Füllung, Eckenrundung | Muss |
| Ellipse | `E` | Etwas einkreisen | wie Rechteck | Muss |
| Text | `T` | Beschriftung im Bild | Größe, Farbe, Hintergrund | Muss |
| Nummer | `N` | Nummerierte Marke; zählt je Bild automatisch und zählt nach Löschen neu | Farbe, Größe, Form (Kreis, Quadrat, abgerundet, Stecknadel, Kreis mit Rand), Zählweise (1 · a · A · i), „Ab hier neu zählen bei …“, feste Beschriftung (Doppelklick) | Muss |
| Markierstift | `M` | Halbtransparente Hervorhebung | Farbe (Standard gelb; das Werkzeug merkt sich seine Farbe) | Muss |
| Lupe | `L` | Vergrößerter Einsatz eines Bildteils: Bereich aufziehen, die runde Linse zeigt ihn vergrößert | Linse und Quelle lassen sich getrennt verschieben; ein runder Griff an der Linse ändert den Zoom | Soll |
| Zuschnitt | `C` | Freien Bildausschnitt festlegen | Seitenverhältnis, Vorgaben | Muss |
| Schwärzen | `X` | Bereich schwarz abdecken | Ausdehnung | Muss |
| Verpixeln | `P` | Bereich in Verpixel- oder Unschärfe-Optik abdecken | Optik, Ausdehnung | Muss |
| Kommentar-Pin | – | Nummerierter Pin mit Text | Nummer | Soll, noch nicht vorhanden |
| Spotlight | `U` | Alles außer einem Bereich abdunkeln | Die Strichstärke bestimmt die Abdunklung: dünn 0,4, mittel 0,55, dick 0,7 | Kann |
| Freihand | `F` | Freie Linie; beim Loslassen wird sie geglättet | Farbe, Stärke | Kann |

Die Werkzeugleiste ordnet die Werkzeuge in drei Spalten, darunter die Farbpalette. Umgesetzt sind alle Werkzeuge der Tabelle bis auf den Kommentar-Pin; die Tasten stehen auch in der Kürzelübersicht (`F1`, [15](#15-tastenkürzel)). Beim Anklicken wird ein Spotlight zuletzt geprüft, weil es große Flächen deckt; Markierungen darunter bleiben erreichbar.

Automatische Markierungen sind keine Werkzeuge, sondern Schalter:

| Automatische Markierung | Taste | Verhalten |
|---|---|---|
| Klickmarke | `I` | Zeigt die aufgezeichnete Klickstelle. Ein- und ausblendbar, im Stil änderbar, aber nicht verschiebbar – sie soll zeigen, wo wirklich geklickt wurde. |
| Elementrahmen | `E` | Rahmen um das erkannte Zielelement. Ein- und ausblendbar; fehlt, wenn kein zuverlässiges Rechteck vorliegt. |
| Mauszeiger | – (Menü) | Zeigt den aufgezeichneten Zeiger in seiner Form. Ein- und ausblendbar, Größe wählbar. |
| Schrittnummer | – (Menü) | Setzt die Nummer des Schritts, wie sie in der Anleitung steht (z. B. „2.3“), als Marke an die Stelle der Aktion. Ein- und ausblendbar (Ansicht → Mausmarken), Standard aus. Im KI-Paket nie. |

Bedienung der Werkzeuge:

- Zeichnen durch Ziehen. `Umschalt` erzwingt Quadrat, Kreis und Winkel in 45-Grad-Schritten.
- Ein Werkzeug bleibt gewählt, bis ein anderes gewählt oder `Esc` gedrückt wird. Über einem vorhandenen Objekt greift die Maus dieses Objekt; auf freier Fläche entsteht ein neues.
- Gewählte Objekte: verschieben, Größe ändern, `Entf` löscht, `Strg+D` verdoppelt, Pfeiltasten verschieben um 1 Pixel (mit `Umschalt` um 10).
- Text: Klick setzt die Schreibmarke; `Esc` oder ein Klick daneben beendet die Eingabe.

**Farben und Stil** ([13](13-handbuch-fahrplan.md), Paket 22): Die Palette hat zwölf Farben (Rot, Orange, Grün, Blau, Schwarz, Weiß, Gelb, Türkis, Violett, Pink, Braun, Grau), dazu „Weitere Farbe …“ (freie Farbwahl), die Pipette (nächster Klick ins Bild nimmt die Farbe auf) und ein Feld mit der zuletzt gewählten eigenen Farbe. Jedes Werkzeug merkt sich seine Farbe. Der Knopf **Stil** öffnet je nach Art: Linienart, Spitzen, Füllung, Hintergrund des Textes, Form und Zählweise von Nummern, Deckkraft – für die ausgewählte Zeichnung oder, ohne Auswahl, für neue Zeichnungen des Werkzeugs; dazu „Stil auf alle Zeichnungen dieser Art übertragen“. **Bild → Ziel markieren** legt aus dem erkannten Zielrahmen bearbeitbare Zeichnungen an (Rahmen und Nummer), auch für mehrere gewählte Schritte.

**Rückgängig über Neustart, Behalten oder Verwerfen** (0.14.0): Die Stapel für Rückgängig und Wiederholen liegen in `history/undo.json` der Aufnahme (höchstens 200 Schritte, 20 MB) und gelten nach dem Schließen und Neustarten weiter; Wiederholen nur, wenn das Overlay seither unverändert ist. Bearbeiten verändert nie die aufgenommenen Bilder (Zeichnungen, Zuschnitt, Schwärzung, Bild ersetzen sind Overlay bzw. eigene Dateien); nur „Endgültig schwärzen“ löscht bewusst Bilder geschwärzter Zeiträume und leert den Verlauf. Wird eine frisch aufgenommene Aufnahme geschlossen, fragt der Editor „Aufnahme behalten?“ (Behalten · Verwerfen in den Papierkorb · Abbrechen; „Nicht mehr fragen“, Einstellung `askKeepOnClose`). Beim allerersten Start (keine Einstellungsdatei) zeigt das Programm die Ersteinrichtung: Sprache von Programm und Anleitungen, Darstellung, Anrede, Aufnahmeordner, Schutzstufe, Countdown, Signaltöne, Aufräumen, Behalten-Frage, Infobereich, Update-Suche.

**Seite und Layout** (Paket 27): Im Abschnitt „Seite (Word und PDF)“ stehen zusätzlich *Layoutvorlage* (Standard, Kompakt, Schulung, Handbuch – setzt Rand, Bilder, Umbrüche, Kopf- und Fußzeile), *Rand*, *Kopfzeile* und *Fußzeile mit Feldern* (je links, Mitte, rechts; Felder `{{Seite}}`, `{{Seiten}}`, `{{Kapitel}}` und Variablen), „Jedes Kapitel auf einer neuen Seite“, „Ein Schritt je Seite“ und „„Erstellt mit …“ zeigen“. Am Schritt unter „Bild in der Anleitung“: „Neue Seite vor diesem Schritt“.

**Bausteine** (Paket 26): Im Dokumentbereich gibt es den Abschnitt „Bausteine“ mit *Impressum* (formatierbar; vor dem Inhaltsverzeichnis, am Ende oder nicht), *Änderungsverzeichnis* (je Zeile `Version | Datum | Autor | Änderung`, Knopf „Eintrag mit heutigem Datum“) und *Glossar* (je Zeile `Begriff = Erklärung`). Texte gelten für die Sprache der Ansicht. „Textabschnitt einfügen“ bietet zusätzlich „Störungsbehebung“ mit einer vorbereiteten Tabelle.

**Bildunterschriften und Warnhinweise** (Paket 25): Unter „Bild in der Anleitung“ stehen *Bildunterschrift* und *Alternativtext* (je Sprache der Ansicht). Die Art eines Hinweises kennt zusätzlich GEFAHR, WARNUNG, VORSICHT und HINWEIS (Signalwörter nach Schwere). In der englischen Ansicht bearbeitet das Hinweisfeld den englischen Text; leer bleibt der deutsche. Im Dokumentbereich unter „Aufbau“: *Bildunterschriften* (keine, „Abb. n: Überschrift“, eigene), *Abbildungsverzeichnis am Ende* und *Warnhinweise vor dem Schritttext*.

**Gliederung** (Paket 24): Schritt → Kapitel bietet „Kapitel nach oben/unten verschieben“ (`Alt+Umschalt+↑/↓`, der ganze Bereich samt Unterkapiteln tauscht mit dem Nachbarkapitel derselben Ebene), „Zum Unterkapitel machen“ (`Alt+Umschalt+→`, schaltet zwischen Kapitel und Unterkapitel) und „Kapitel ausblenden“. In der Liste steht ein Unterkapitel als „§§“, ein ausgeblendetes durchgestrichen. Schritt → „Textabschnitt einfügen“ legt einen Abschnitt ohne Nummer und Bild an: leer, „Sicherheitshinweise“, „Häufige Fragen“ oder „Kontakt und Hilfe“ (vorbefüllt, formatiert). Im Dokumentbereich gibt es die Zählweise „je Kapitel 1, 2 …“.

**Markenstil** (Ansicht → Mausmarken → Stil der Marken): Klassisch (bisher), Nummern (Schrittnummer statt Klickring), Dezent, Kräftig; dazu Farbe der Marken und Form der Schrittnummer. Der Stil gilt für alle Schritte der Aufnahme ([04 §13.19](04-datenformat.md#1319-markenstil-guideautostyle)).

### 7.3 Eigenschaften und Stilvorlagen

- Die Eigenschaftsleiste zeigt die Einstellungen des gewählten Werkzeugs oder Objekts.
- Eine **Stilvorlage** fasst Farben, Stärken und Schriftgrößen aller Werkzeuge sowie das Aussehen der automatischen Markierungen zusammen. Mitgeliefert: „Standard“ (Rot, mittlere Stärke), „Dezent“, „Kräftig“. Eigene Vorlagen lassen sich speichern.
- Die Stilvorlage gilt je Sitzung; „auf alle Schritte anwenden“ stellt bestehende Markierungen um.
- Neue Objekte übernehmen den Stil der zuletzt gezeichneten gleichen Art.

### 7.4 Am Element einrasten

Der Recorder kennt die Rechtecke der Bedienelemente im Bild. Bei den Werkzeugen Rechteck, Markierstift, Schwärzen, Verpixeln und Zuschnitt werden sie beim Überfahren hervorgehoben; **ein Klick** setzt das Objekt genau auf das Element. `Strg` schaltet das Einrasten vorübergehend ab. So entsteht ein passgenauer Rahmen oder eine passgenaue Schwärzung ohne Ziehen.

Priorität Soll. Voraussetzung: Elementrechtecke im Datenbestand ([04](04-datenformat.md)).

### 7.5 Zuschnitt

- Der Zuschnitt legt die Darstellung „Frei“ für den Schritt fest. Er ist jederzeit änderbar und rücknehmbar.
- Vorgaben: Anwendungsfenster, Ausschnitt um die Aktion, feste Seitenverhältnisse.
- „Auf folgende Schritte übertragen“ wendet denselben Zuschnitt auf die nächsten Schritte im selben Fenster an.

### 7.6 Schwärzen und Verpixeln

Eine Schwärzung ist keine Markierung, sondern eine Datenschutzmaßnahme. Sie wird von der gemeinsamen Leseschicht erzwungen und gilt für Anzeige, Wiedergabe, Kopie, Export, KI-Paket und MCP. Die Regeln stehen in [10](10-datenschutz-und-sicherheit.md#10-schwärzungsmodell).

Es gibt vier Arten: **Bereich im Bild**, **Textstelle**, **ganzer Schritt** und **Zeitraum**.

- **Im Bild:** Mit `S` oder `X` einen Bereich aufziehen oder per Einrasten ein Element wählen. Danach zeigt ein kleines Feld die **Ausdehnung** nach den Regeln aus [10](10-datenschutz-und-sicherheit.md#103-ausdehnung-auf-alle-vorkommen):

| Ausdehnung | Gilt für | Art |
|---|---|---|
| Solange unverändert | Alle aufeinanderfolgenden Bilder, in denen der Bereich denselben Inhalt zeigt | immer, von selbst |
| Dem Fenster folgen | Alle Bilder, in denen dieses Fenster zu sehen ist; die Stelle wandert mit dem Fenster mit | wählbar; vorbelegt, wenn die Stelle in einem Fenster liegt |
| Dem Element folgen | Alle Bilder, in denen das Bedienelement zu sehen ist; die Stelle folgt seinem Rechteck | wählbar, wenn per Einrasten gewählt |
| Diese Bildschirmstelle, ganze Sitzung | Alle Bilder des Monitors | wählbar; für feste Stellen wie die Uhr oder ein Benachrichtigungsfeld |
| Wiederauftreten | Andere Bilder der Sitzung, die denselben Bildausschnitt zeigen | Vorschlag zum Bestätigen |

- Die Statuszeile nennt die Zahl betroffener Bilder; „prüfen“ zeigt sie als Übersicht.
- **Im Text:** Jeder aufgezeichnete Text im Detailbereich (getippter Text, Feldinhalt, Zwischenablage, Fenstertitel, Elementname) hat die Aktion „Schwärzen“. Wahlweise nur an dieser Stelle oder überall in der Sitzung, wo derselbe Text vorkommt. Kennt der Datenbestand Bildstellen mit diesem Text, bietet der Viewer an, sie mitzuschwärzen. An die Stelle des Textes tritt `[geschwärzt]`, in englischen Ausgaben `[redacted]` – ohne Hinweis auf die Länge. Der gesuchte Text selbst wird nicht gespeichert. Text, der nur als Bildpunkte existiert (Remote-Sitzung, Zeichenfläche), wird dabei nicht gefunden – das sagt der Dialog.
- **Ganzer Schritt:** „Inhalt schwärzen“ schwärzt alle Texte eines Schritts und seine Bilder ([04](04-datenformat.md#edits-redactions)). In jeder Ausgabe steht an seiner Stelle der Platzhalter „Schritt geschwärzt“, englisch „Step redacted“. Der Befehl bietet an, den Schritt zugleich auszublenden; dann fehlt er in der Anleitung ganz. Verwendet ein Nachbarschritt eines der Bilder, weist der Viewer darauf hin.
- **Zeitraum:** In der Zeitleiste einen Abschnitt aufziehen oder mehrere aufeinanderfolgende Schritte wählen, dann „Zeitraum schwärzen“. Alles dazwischen – Eingaben, Texte, Bilder – wird nicht mehr ausgegeben. An der Stelle steht in **jeder** Ausgabe der neutrale Hinweis „vom Autor entfernt“, englisch „removed by the author“: in der Anleitung als Hinweiszeile, im KI-Paket und über den MCP-Server als Lücke. Er nennt Zeitpunkt und Dauer, nie Inhalt oder Grund ([10](10-datenschutz-und-sicherheit.md#105-darstellung)).
- **Anzeige im Viewer:** Schwärzungen sind schwarz mit feiner Schraffur, damit der Autor sie von schwarzem Bildinhalt unterscheidet. In Kopien und Exporten sind sie schwarz gefüllt. Zum Ändern lässt sich eine Schwärzung vorübergehend aufdecken („Schwärzung bearbeiten“); solange sie aufgedeckt ist, sind Kopieren und Export gesperrt.
- **Verpixeln und Unschärfe** sind reine Optik: Sie werden nicht aus den Originalpixeln berechnet, sondern aus der Durchschnittsfarbe des Bereichs und einem Zufallsmuster ([10](10-datenschutz-und-sicherheit.md#105-darstellung)). Der Inhalt lässt sich daraus nicht zurückrechnen.
- **Vorschläge:** Der Viewer listet Fundstellen sensibler Muster ([10](10-datenschutz-und-sicherheit.md#112-musterprüfung)) in getipptem Text, Feldinhalten, Fenstertiteln und Text-Schnappschüssen. Je Fundstelle: zur Stelle springen, schwärzen, ignorieren. Es wird nie von selbst geschwärzt.
- **Ältere Ausgaben:** Nach jeder Änderung an Schwärzungen werden Vorschaubilder und Suchindex neu erzeugt. Liegen im Ordner `export/` der Sitzung ältere Ausgaben, listet der Viewer sie auf und bietet an, sie zu löschen.
- **Endgültig schwärzen** (Menü der Sitzung) gibt es auf zwei Wegen, beide nach Rückfrage mit Zusammenfassung: als **bereinigte Kopie** – empfohlen, bevor eine Sitzung als Ordner oder `.steprec` weitergegeben wird – oder **an Ort und Stelle**. Danach lassen sich diese Schwärzungen nicht mehr zurücknehmen. Ablauf und Grenzen stehen in [10](10-datenschutz-und-sicherheit.md#108-endgültiges-schwärzen).

### 7.7 Anderes Bild wählen

„Bild wechseln“ öffnet den Bildstreifen des Schritts: Vorher-Bild, Nachher-Bild, Zwischenbilder und die Bilder der Nachbarschritte. Ein Klick macht das gewählte Bild zum Bild des Schritts – etwa das Zwischenbild, auf dem ein Tooltip zu sehen ist. Vorhandene Markierungen bleiben; liegt das Fenster im neuen Bild an anderer Stelle, weist der Viewer darauf hin.

### 7.8 Extern bearbeiten

1. „Extern bearbeiten“ schreibt das Bild als PNG in den Ordner `export/.tmp/` der Sitzung – wahlweise mit oder ohne Markierungen, **immer mit Schwärzungen** – und öffnet es im Standardprogramm für Bilder oder in einem in den Einstellungen gewählten Programm.
2. Der Viewer beobachtet die Datei. Wird sie gespeichert, fragt er: „Änderung übernehmen?“
3. Das bearbeitete Bild wird als **Bildersatz** des Schritts in der Sitzung abgelegt ([04](04-datenformat.md#edits-json)). Das gespeicherte Original bleibt unberührt; „Bildersatz entfernen“ stellt es wieder her.

Hat das bearbeitete Bild andere Maße, entfallen die automatischen Markierungen für diesen Schritt.

Ein Bildersatz umgeht keine Schwärzung: Kommt später eine hinzu, gilt sie auch für ihn. Hat er andere Maße als das Ausgangsbild, zeigt der Viewer stattdessen das geschwärzte Ausgangsbild, bis der Autor den Ersatz neu erzeugt oder bestätigt.

### 7.9 Rückgängig und Zurücksetzen

- `Strg+Z` und `Strg+Y` nehmen jede Bearbeitung zurück und stellen sie wieder her – Markierungen, Texte, Reihenfolge, Ausblenden, Schwärzungen. Unbegrenzt, solange die Sitzung geöffnet ist. Ein Verlauf über das Schließen hinaus ist Kann.
- „Schritt zurücksetzen“ entfernt alle Bearbeitungen eines Schritts; „Sitzung zurücksetzen“ nach Rückfrage alle.
- Nicht rücknehmbar ist allein das endgültige Schwärzen.
- Frühere Stände der gesamten Bearbeitung lassen sich über die [Bearbeitungsstände](#710-bearbeitungsstände) wiederherstellen.

### 7.10 Bearbeitungsstände

Das Programm sichert das Bearbeitungs-Overlay von selbst, damit sich auch eine große Bearbeitung zurückholen lässt, die längst aus dem Rückgängig-Verlauf ist. Die Stände liegen in der Sitzung unter `history/` (`edits-<UTC>-r<Revision>-<Anlass>.json`).

- **Wann gesichert wird:** beim ersten Speichern nach dem Öffnen, danach höchstens alle 10 Minuten und erzwungen vor „Alle ersetzen“, „Zuschnitt auf alle“, „Vorlage anwenden“ und vor dem Wiederherstellen. Ein Stand mit derselben Revision wird nicht doppelt gesichert.
- **Grenzen:** höchstens 40 Stände und 15 MB; die ältesten fallen weg. Ein Fehler beim Sichern hält die Bearbeitung nie auf.
- **Fenster „Bearbeitungsstände …“** (Menü „…“): Jeder Stand zeigt, wann er entstand (relativ), den Anlass, die Revision, eine Zusammenfassung und den Unterschied zum jetzigen Stand. Ein Stand, der dem jetzigen entspricht, lässt sich nicht wiederherstellen.
- **Wiederherstellen ist eine normale Bearbeitung:** `Strg+Z` nimmt sie zurück, die Revision steigt weiter. **Schwärzungen, Verallgemeinerungen und der Stand der Durchsicht bleiben standardmäßig, wie sie jetzt sind**; ein Kontrollkästchen im Fenster hebt das auf. So macht ein alter Stand weder eine Schwärzung noch eine Durchsicht ungeschehen.
- **Schutz:** „Endgültig schwärzen“ löscht alle Stände ([7.6](#76-schwärzen-und-verpixeln)). Eine `.steprec`-Datei und eine bereinigte Kopie enthalten keine Stände; ein geöffnetes Archiv hat keine.

## 8 Kommentieren

| Art des Kommentars | Wo | Erstellen | In der Anleitung |
|---|---|---|---|
| Kommentar zum Schritt | Unter dem Schritttext | `K` oder „+ Kommentar“ | Kasten unter dem Schritt |
| Kommentar-Pin | An einer Stelle im Bild | Werkzeug `P`, Stelle anklicken, Text tippen | Nummerierter Pin im Bild, Text als Legende darunter |
| Hinweis- oder Warnschritt | Als eigener Schritt zwischen zwei Schritten | `Einfg` | Eigener, nicht nummerierter Absatz |
| Kommentar aus der Aufnahme | Als Schritt „Kommentar“ mit eingefrorenem Bild | Entsteht bei der Aufnahme ([07](07-aufnahme-bedienung.md#9-kommentar-marker-schnappschuss)) | Wie Hinweisschritt, mit Bild |

Kommentare zum Schritt und Kommentare aus der Aufnahme haben eine **Kennzeichnung**; Hinweisschritte kennen Hinweis, Tipp und Warnung. Pins gelten als Hinweis.

| Kennzeichnung | Bedeutung | Ausgabe |
|---|---|---|
| Hinweis | Zusätzliche Information | In Anleitung und KI-Paket |
| Tipp | Empfehlung, Abkürzung | In Anleitung und KI-Paket |
| Warnung | Etwas kann schiefgehen | In Anleitung und KI-Paket, hervorgehoben |
| Intern | Notiz nur für den Autor | In keiner Ausgabe |

Ebenso erscheinen **ausgeblendete Kommentare** in keiner Ausgabe – weder in der Anleitung noch im KI-Paket noch über den MCP-Server. Das gilt auch für ausgeblendete Kommentare aus der Aufnahme.

- Kommentare sind reiner Text mit einfacher Auszeichnung: fett, kursiv, Code, Link. Andere Auszeichnung wird als Text ausgegeben.
- Kommentare lassen sich bearbeiten, verschieben, in der Kennzeichnung ändern und löschen. Das gilt auch für Kommentare aus der Aufnahme: Sie werden im Bearbeitungs-Overlay geändert oder ausgeblendet, das Rohprotokoll bleibt unberührt ([04](04-datenformat.md#edits-json)).
- **Interne Kommentare:** Auch ein bei der Aufnahme geschriebener Kommentar lässt sich als intern kennzeichnen („Interner Kommentar – erscheint in keiner Ausgabe“). Sein Text bleibt im Editor sichtbar. Interne Notizen eines Schritts stehen im Detailbereich unter „Interne Notizen (N) – in keiner Ausgabe“ und lassen sich dort löschen. Eine bereinigte Kopie ([11](#11-sitzungen-verwalten)) enthält sie nicht.
- Marker aus der Aufnahme erscheinen als Fähnchen; ein Klick darauf legt einen Kommentar an dieser Stelle an.
- Kommentare fließen als „Kommentar des Autors“ in die Anleitung ([09](09-anleitungs-export.md)) und ins KI-Paket ([06](06-ki-export-und-mcp.md#74-kommentare-des-autors)).

## 9 Schritte ordnen und Texte bearbeiten

### 9.1 Texte

- Jeder Schritt hat je Sprache einen **Kurztitel** (steht in der Schrittliste) und einen **Text** (die Anweisung; steht im Detailbereich und in der Anleitung). Beide entstehen beim Anzeigen aus den Textvorlagen im Anleitungsstil ([05](05-schritterkennung.md#textvorlagen)); wo dieses Dokument „Schritttext“ sagt, sind beide gemeint. `F2` oder ein Klick macht sie änderbar; `Strg+Eingabe` schließt ab, `Esc` verwirft.
- Geänderte Texte tragen ein Kennzeichen. „Automatischen Text wiederherstellen“ nimmt die Änderung zurück.
- Sprache (Deutsch, Englisch) und – im Deutschen – Anrede (Sie, du, neutral) der Texte stehen in der Statusleiste und sind dort umschaltbar. Automatische Texte wechseln sofort; eigene Texte gelten für die Sprache, in der sie geschrieben wurden, und werden nicht übersetzt.
- **Texte von KI-Agenten:** Schreibt ein Agent über den MCP-Server einen Schritttext ([06](06-ki-export-und-mcp.md)), trägt der Schritt das Kennzeichen „von KI-Agent geschrieben“ mit dem Namen des Programms und der Zeit. Solche Texte lassen sich einzeln oder gesammelt verwerfen; dann gilt wieder der automatische Text.
- Getippter Text, Tastenfolgen und Elementnamen stammen aus der Aufzeichnung und werden im Schritttext nicht verändert; wer sie nicht zeigen will, schwärzt sie.

**Formatieren** ([13](13-handbuch-fahrplan.md), Paket 21): Anleitungstext, Hinweise, Kapiteleinleitung, Einleitung und Abschluss sind formatierbare Felder. Eine Leiste über dem Feld (bei Hinweisen und Dokumenttexten nur, solange das Feld den Fokus hat) bietet fett (`Strg+B`), kursiv (`Strg+I`), Eingabe (`Strg+E`), Taste, Bedienelement, Aufzählung (`Strg+Umschalt+L`), Nummerierung (`Strg+Umschalt+N`), Link (`Strg+K`), „Formatierung entfernen“ und „</>“ für die Auszeichnung als Text. Eingefügt wird nur reiner Text; Unterstreichen, Schriftgröße und Ausrichtung gibt es bewusst nicht. Ohne Formatierung speichert das Feld reinen Text wie bisher. Die Überschrift bleibt ein einfaches Textfeld.

**Einfügen** (Knopf „＋“ in der Leiste, Paket 23): *Querverweis* (Liste aller Schritte und Kapitel mit ihren gedruckten Nummern; im Feld erscheint der Verweis unterstrichen mit der aktuellen Nummer), *Variable* (eigene und eingebaute, dazu „Variablen bearbeiten …“) und *Tabelle …* (Dialog mit Zeilen, Spalten und Kopfzeile; danach lassen sich die Zellen direkt im Feld bearbeiten und formatieren). Die eigenen Variablen stehen im Dokumentbereich unter „Einleitung und Texte“ als Zeilen `Name = Wert`, je Sprache der Ansicht.

### 9.2 Aktionen

| Aktion | Kürzel | Wirkung in der Anleitung |
|---|---|---|
| Ausblenden, Einblenden | `Entf` in der Schrittliste, `Strg+H` | Der Schritt fehlt; im Viewer bleibt er grau sichtbar |
| Verwerfen, Verwerfen zurücknehmen | Kontextmenü | Ein Schritt wird nachträglich als Fehlbedienung gekennzeichnet und fehlt dann; ein bei der Aufnahme verworfener Schritt erscheint wieder |
| Inhalt schwärzen | `Umschalt+Entf` | An der Stelle des Schritts steht der Platzhalter „Schritt geschwärzt“; er fehlt nur, wenn der Schritt zusätzlich ausgeblendet ist ([7.6](#76-schwärzen-und-verpixeln)) |
| Zusammenfassen | `Strg+J` | Aufeinanderfolgende Schritte werden ein Schritt mit dem ersten Vorher- und dem letzten Nachher-Bild (Festlegungen unten) |
| Zusammenfassung aufheben | `Strg+Umschalt+J` | Die Mitglieder erscheinen wieder einzeln |
| Teilen (Soll) | – | Eine Texteingabe wird an einer gewählten Stelle geteilt; noch nicht vorhanden |
| Verschieben | `Alt+↑`, `Alt+↓`, Ziehen | Neue Reihenfolge |
| Kapitel einfügen | `Strg+Umschalt+K` | Überschrift vor dem gewählten Schritt |
| Hinweisschritt einfügen | `Einfg` | Eigener Textschritt nach dem gewählten Schritt |
| Wichtigkeit ändern | `Strg+↑`, `Strg+↓` | Nebenschritt wird Hauptschritt und umgekehrt |
| Alle wählen, Mehrfachauswahl | `Strg+A`, `Umschalt+Klick`, `Strg+Klick` | Alle Aktionen wirken auf die Auswahl |

Festlegungen:

- **Ausblenden ist nicht Schwärzen.** Ausgeblendete Schritte fehlen nur in der Anleitung. Was niemand sehen darf, wird geschwärzt.
- **Bearbeitungen für die Anleitung nehmen der KI nichts weg.** Wie ausgeblendete, verworfene, zusammengefasste und umsortierte Schritte im KI-Paket erscheinen, regelt [06](06-ki-export-und-mcp.md); aus Sicht des Viewers bleibt das KI-Protokoll zeitlich geordnet und vollständig, und nur Schwärzungen entfernen dort Inhalt.
- **Umsortieren** ändert nur die Reihenfolge der Anleitung; die Schrittliste zeigt sie ([4.2](#42-schrittliste)). Die Statusleiste zeigt „Reihenfolge geändert“; „Zeitliche Reihenfolge wiederherstellen“ nimmt es zurück. Die Bilder zeigen weiterhin den Zustand zum Zeitpunkt der Aufnahme.
- **Teilen** zeigt die Eingaben des Schritts als Streifen; ein Klick zwischen zwei Eingaben setzt die Trennstelle.
- **Zusammenfassen** gilt nur für Anleitungen (HTML, Markdown, Word, PDF, PowerPoint, Klick-Tour, Fehlerbericht, GIF). KI-Paket, MCP-Server, Vergleich und AutoHotkey zeigen weiter die Einzelschritte.
  - **Voraussetzung:** Gewählt sind mindestens zwei **aufeinanderfolgende** aufgenommene Schritte. Eingefügte Schritte, Lücken der Aufzeichnung und Schritte, die schon zu einer Gruppe gehören, lassen sich nicht zusammenfassen; ebenso wenig eine Auswahl, in der ab dem zweiten Schritt ein Kapitel beginnt. Das Programm nennt den Grund in der Statuszeile.
  - **Ergebnis:** Der erste Schritt führt die Gruppe. Die Anleitung zeigt ihn mit dem ersten sichtbaren Vorher- und dem letzten Nachher-Bild; Zeitspanne, Kommentare und Wichtigkeit (die höchste der Mitglieder) fassen alle zusammen. Der Text ist die Aneinanderreihung der Texte der sichtbaren Mitglieder; ausgeblendete oder geschwärzte Mitglieder tragen nichts bei.
  - **Text der Gruppe:** Der Autor bearbeitet ihn am führenden Schritt. Er gilt für die Sprache, in der er geschrieben wurde, und steht in `edits.merges[].text` je Sprache ([04](04-datenformat.md#edits-json)); „Automatischen Text wiederherstellen“ nimmt ihn zurück. „Alle ersetzen“ erfasst auch diese Texte.
  - **Aufheben** (`Strg+Umschalt+J` auf einem Mitglied) löscht nur die Gruppe; die Einzelschritte und ihre Bearbeitungen bleiben erhalten. Ist der führende Schritt ausgeblendet, ist die Gruppe ausgeblendet.
- Alle Aktionen sind an Ereignisnummern verankert und überstehen eine Neuberechnung der Schritte ([04](04-datenformat.md)).

### 9.3 Abschnitt neu aufnehmen und ersetzen

Ist ein Teil der Aufnahme misslungen, muss nicht alles neu aufgenommen werden: Der Autor nimmt nur diesen Teil noch einmal auf, und die neue Fassung tritt an die Stelle der alten (Soll).

1. Der Autor wählt einen oder mehrere aufeinanderfolgende Schritte und ruft „Neu aufnehmen und ersetzen“ auf (`Strg+R`, Kontextmenü).
2. Ein kleines Fenster nennt die Schritte und zeigt das Vorher-Bild des ersten: In diesen Zustand bringt der Autor die Anwendung, bevor er startet.
3. „Aufnahme starten“ beginnt eine Aufnahme mit dem Aufnahmebereich, der beim ersten zu ersetzenden Schritt galt. Lässt er sich nicht herstellen – die Anwendung läuft nicht, der Monitor fehlt –, öffnet sich die passende Auswahl ([03](03-aufzeichnungskern.md#weitere-abschnitte)). Der Viewer verschwindet; die Leiste zeigt „Ersatz für Schritte 5–7“.
4. Nach einem sauberen Stopp trägt die Aufzeichnungssteuerung die Ersetzung ein; der Viewer kehrt zurück, und die neue Fassung steht an der Stelle der alten.

```
┌─ Neu aufnehmen und ersetzen ───────────────────────────────────────────┐
│ Ersetzt werden: Schritt 5 bis 7 (3 Schritte)                           │
│                                                                        │
│ Bringen Sie die Anwendung in diesen Zustand, bevor Sie starten:        │
│ ┌──────────────────────────────────────┐                               │
│ │ Vorher-Bild von Schritt 5            │                               │
│ └──────────────────────────────────────┘                               │
│ Aufgenommen wird: Eine Anwendung – Editor                              │
│ (i) An Schritt 6 hängen 1 Kommentar und 2 Markierungen.                │
│     Sie bleiben bei der bisherigen Fassung.                            │
│                                                                        │
│                                        [Aufnahme starten]  [Abbrechen] │
└────────────────────────────────────────────────────────────────────────┘
```

- **Voraussetzung:** eine abgeschlossene Sitzung, die als beschreibbarer Ordner vorliegt – nicht gepackt als `.steprec` und nicht auf einem schreibgeschützten Datenträger. Sonst ist der Befehl ausgegraut und nennt den Grund.
- **Daten:** Die Neuaufnahme ist ein weiterer Aufnahmeabschnitt derselben Sitzung. Dass sie an die Stelle der früheren Schritte tritt, steht im Overlay (`edits.replacements`, [04](04-datenformat.md#edits-replacements)). Den Eintrag legt die Aufzeichnungssteuerung unmittelbar nach dem sauberen Stopp an ([03](03-aufzeichnungskern.md#weitere-abschnitte)); der Viewer ändert ihn nur, wenn der Autor die Ersetzung aufhebt oder erneut einsetzt. Die Rohdaten bleiben vollständig; nichts wird gelöscht.
- **Ersetzte Schritte** fehlen in Anleitung und Wiedergabe. Im Viewer bleiben sie unter „Alle“ grau sichtbar, mit dem Kennzeichen „ersetzt“ und einem Verweis auf die neue Fassung; der Filter „ersetzt“ zeigt sie. Sie sind weder ausgeblendet noch geschwärzt: Schwärzungen, Durchsicht und Verallgemeinerungen gelten für sie unverändert.
- **Neue Schritte** tragen das Kennzeichen „neu aufgenommen“ und stehen dort, wo die ersetzten standen. Wie daraus Reihenfolge und Nummerierung entstehen, legt [05](05-schritterkennung.md#wirksame-schrittfolge) fest.
- **Auswahl:** Ersetzt wird ein zusammenhängender Bereich innerhalb eines Aufnahmeabschnitts, vom ersten bis zum letzten markierten Schritt. Bei einer Auswahl mit Unterbrechungen bietet der Viewer an, den ganzen Bereich dazwischen zu nehmen. Auch ein Teil einer Neuaufnahme lässt sich noch einmal ersetzen.
- **Bearbeitungen** an den ersetzten Schritten – Texte, Kommentare, Markierungen – bleiben bei diesen und kommen mit ihnen zurück, wenn die Ersetzung aufgehoben wird. Übertragen werden sie nicht; das Fenster nennt vor dem Start, was betroffen ist.
- **Anschluss:** Die Schritte nach dem ersetzten Bereich zeigen weiterhin den Stand der ursprünglichen Aufnahme. Weicht der Zustand am Ende der neuen Fassung davon ab, passen die Bilder nicht nahtlos zusammen; die Qualitätsprüfung weist auf die Nahtstelle hin ([12.1](#121-qualitätshinweise)).
- **Rückgängig:** „Ersetzung aufheben“ lässt die früheren Schritte wieder gelten. Die Neuaufnahme bleibt in der Sitzung als nicht verwendete Neuaufnahme: Ihre Schritte gehören zu keiner Anleitung und stehen im Viewer unter „Alle“ mit diesem Kennzeichen; „Erneut einsetzen“ legt die Ersetzung wieder an. Auch `Strg+Z` hebt eine eben angelegte Ersetzung auf.
- **Eingaben der neuen Fassung** sind in der Durchsicht ungeprüft ([Abschnitt 13](#13-durchsicht-der-eingaben)).
- **Einstellungen:** Die Neuaufnahme übernimmt die Einstellungen des Abschnitts, dessen Schritte sie ersetzt, damit Bilder und Schutz zum Rest der Sitzung passen; die Ausschlussliste kann dabei nur wachsen. Ein Aufnahmeprofil wird nicht gewählt, das Ziel nicht abgefragt. Schützt die Neuaufnahme damit schwächer als die aktuellen Einstellungen, sagt das die Startmeldung ([10](10-datenschutz-und-sicherheit.md#44-aufnahmeprofile-nie-unbemerkt-schwächer)).
- **Ausgaben:** In der Anleitung steht nur die neue Fassung ([09](09-anleitungs-export.md#46-ziel-marker-neuaufnahmen-und-platzhalter)). Das KI-Paket weist die Ersetzung an einer Nahtstelle aus; seine Detailstufe „Vollständig“ zeigt beide Fassungen ([06](06-ki-export-und-mcp.md#77-aufnahmeabschnitte-nahtstellen-und-ersetzte-schritte)).
- **Verwerfen:** „Aufnahme verwerfen“ in der Leiste verwirft während der Neuaufnahme **nur den neuen Abschnitt, nie die Sitzung** ([07](07-aufnahme-bedienung.md#12-stopp-und-übergang-zum-viewer)). Der Abschnitt wird als verworfen gekennzeichnet und aus allen Ausgaben herausgehalten – Anleitung, KI-Paket, MCP-Server, Suche und Vergleich; das Journal wird nie gekürzt. Es entsteht keine Ersetzung, die markierten Schritte gelten weiter, und der Viewer kehrt mit der Sitzung zurück, wie sie war. Dasselbe gilt für eine verworfene angehängte Aufnahme.
- **Verworfene Abschnitte im Viewer:** Ihre Schritte zeigt der Viewer nicht, und ihre Eingaben brauchen keine Entscheidung in der Durchsicht. Die [Sitzungsübersicht](#43-sitzungsübersicht) nennt, dass und wann Abschnitte verworfen wurden, und bietet „Wieder aufnehmen“ an (`edits.sections`, [04](04-datenformat.md#edits-sections)). Danach verhält sich eine angehängte Aufnahme wie jede andere; eine Neuaufnahme ist eine nicht verwendete Neuaufnahme und lässt sich einsetzen. Ihre Eingaben sind dann ungeprüft.
- **Verworfen heißt nicht gelöscht.** Die Rohdaten des Abschnitts liegen weiter im Sitzungsordner; wer den Ordner weitergibt, gibt sie mit. Dauerhaft entfernt werden sie erst durch die bereinigte Kopie oder das endgültige Schwärzen ([7.6](#76-schwärzen-und-verpixeln)).
- **Absturz:** Endet die Neuaufnahme durch einen Absturz, entsteht ebenfalls keine Ersetzung. Nach der Wiederherstellung ist sie eine nicht verwendete Neuaufnahme und lässt sich einsetzen.
- Bricht der Autor schon vor dem Start ab, bleibt alles, wie es war.
- *Stand der Umsetzung:* „Neu aufnehmen und ersetzen …“ (`Strg+R`, Menü „Schritt“ › „Neu aufnehmen“, Rechtsklick) prüft die Voraussetzungen, bildet den Bereich (`SessionView.ReplaceRangeFor`) und zeigt das Fenster wie oben – mit Vorher-Bild, Aufnahmebereich und der Zahl der Hinweise, Markierungen und eigenen Texte an den betroffenen Schritten. Danach läuft eine gewöhnliche Aufnahme ([03 §1.7](03-aufzeichnungskern.md#weitere-abschnitte)); nach dem Stopp wird sie angehängt, der Editor lädt neu, wählt den ersten neuen Schritt und nennt in der Statuszeile, was ersetzt wurde; `Strg+Z` hebt die eben angelegte Ersetzung auf. In der Schrittliste tragen neue Schritte das Kennzeichen „neu aufgenommen“, der erste Schritt hinter einer Nahtstelle das Kennzeichen „Nahtstelle“ (Tooltip: Wortlaut der Naht); mit „Alle“ stehen die ersetzten Schritte grau mit „ersetzt“ unmittelbar vor ihrem Ersatz und nicht verwendete Neuaufnahmen am Ende. „Ersetzung aufheben“ (an einem ersetzten oder neuen Schritt) und „Ersetzung erneut einsetzen“ (am gewählten nicht verwendeten Schritt, sonst an der jüngsten Neuaufnahme) stehen im selben Untermenü und sind rückgängig machbar. Abweichungen: Eine Auswahl mit Unterbrechungen nimmt ohne Rückfrage den ganzen Bereich dazwischen; die Leiste zeigt nicht „Ersatz für Schritte …“; es gibt keinen eigenen Filter „ersetzt“ und keinen Verweis vom ersetzten Schritt außer im Tooltip; verworfene Abschnitte entstehen nicht (die Sitzungsübersicht bietet darum kein „Wieder aufnehmen“).
- Abgrenzung: „Aufnahme anhängen“ ([Abschnitt 11](#11-sitzungen-verwalten)) stellt neue Schritte ans Ende. „Bild neu aufnehmen“ (Kann, [Abschnitt 12](#12-komfortfunktionen)) tauscht nur ein Bild.

### 9.4 Werte verallgemeinern

Eine Anleitung soll für andere gelten: nicht der eigene Benutzername, sondern `{Benutzername}`; nicht der eigene Server, sondern `{Server}`. Dafür legt der Autor Regeln „Wert → Platzhalter“ an (Soll). Sie wirken wie Schwärzungen über die gemeinsame Leseschicht in **allen** Ausgaben – nur steht an der Stelle statt `[geschwärzt]` der Platzhalter. Aufbau und Wirkung der Regeln legt [04](04-datenformat.md#edits-substitutions) fest, die Schutzregeln [10](10-datenschutz-und-sicherheit.md#109-verallgemeinerung-von-werten), die Darstellung im KI-Paket [06](06-ki-export-und-mcp.md#78-verallgemeinerte-werte); hier steht die Bedienung.

```
┌─ Werte verallgemeinern ────────────────────────────────────────────────────────────────────┐
│ Aus persönlichen Werten werden Platzhalter – in allen Ausgaben dieser Sitzung.             │
│                                                                                            │
│ Vorschläge (auf diesem Rechner erkannt)                                                    │
│ [x] Benutzerprofil  C:\Users\mmuster       →  {Benutzerprofil}     7 Fundstellen          │
│ [x] Benutzername    mmuster                →  {Benutzername}      12 Fundstellen          │
│ [x] Rechnername     PC-0815                 →  {Rechner}            3 Fundstellen          │
│                                                                                            │
│ Eigene Regeln                                                           [+ Regel]  [↑] [↓] │
│ [x] srv-fs-01.firma.example  →  {Server}    ganze Sitzung · Text und Bild   4 Fundstellen  │
│ ────────────────────────────────────────────────────────────────────────────────────────── │
│ 26 Fundstellen in Texten · 9 Bildstellen ersetzt · 4 ohne bekannte Bildposition            │
│ (i) Bilder können den Wert an weiteren Stellen zeigen, die das Programm nicht kennt.       │
│ [Fundstellen zeigen]                                               [Anwenden]  [Abbrechen] │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Aufruf:** `Strg+Umschalt+G` oder „Werte verallgemeinern …“ im Menü der Kopfleiste. Jeder aufgezeichnete Text im Detailbereich hat zusätzlich die Aktion „Verallgemeinern …“; sie öffnet den Dialog mit dem markierten Text als neuer Regel.
- **Vorschläge:** Benutzername, Rechnername, Domäne und Benutzerprofilpfad. Sie stammen aus der laufenden Windows-Sitzung des Rechners, auf dem der Viewer gerade läuft, werden nur im Arbeitsspeicher mit den Texten der Sitzung verglichen und stehen in keiner Datei, solange der Autor sie nicht als Regel annimmt. Ein Vorschlag erscheint nur, wenn sein Wert in der Sitzung vorkommt, und ist dann vorangekreuzt. Wurde die Sitzung auf einem anderen Rechner aufgenommen, passen die Werte nicht; dann erscheint kein Vorschlag, und der Dialog sagt das, statt Sicherheit vorzutäuschen.
- **Kein Ersatz für das Schwärzen von Geheimnissen:** Die Regel hält ihren Wert im Klartext in `edits.json`. Sieht der Wert einer neuen Regel nach einem Kennwort, Schlüssel oder einer Kontonummer aus, rät der Dialog zur Schwärzung.
- **Eigene Regel:**

| Angabe | Bedeutung | In `edits.substitutions` |
|---|---|---|
| Wert | Der wörtliche Text, mindestens drei Zeichen – oder, für Geübte, ein Muster (regulärer Ausdruck) | `match.literal` oder `match.pattern` |
| Platzhalter | Name, der stattdessen erscheint; aus „Server“ wird in den Ausgaben `{Server}` | `placeholder` |
| Groß- und Kleinschreibung beachten | an · aus (Standard aus) | `caseSensitive` |
| Geltung | ganze Sitzung · nur die gewählten Schritte | `scope`: `all` oder `steps` |
| Wirkt auf | Texte · Bilder · beides | `targets` |

- Der Dialog zeigt je Regel die Zahl der Fundstellen und aktualisiert sie beim Tippen; „Fundstellen zeigen“ listet sie mit Schritt und Umgebung. Ein ungültiges Muster oder ein unzulässiger Platzhaltername wird sofort gemeldet.
- **Reihenfolge:** Die Regeln wirken in der Reihenfolge der Liste; was eine Regel ersetzt hat, durchsucht keine spätere mehr. Der Viewer stellt den Benutzerprofilpfad deshalb vor den Benutzernamen – so wird aus `C:\Users\max` der Platzhalter `{Benutzerprofil}` und nicht `C:\Users\{Benutzername}`. Die Reihenfolge lässt sich ändern.
- **Wirkung in Texten:** in jedem Text, den die Leseschicht ausgibt – getippter Text, Feldinhalte, Fenstertitel, Elementnamen, Adressen und Pfade, Text-Schnappschüsse, Zwischenablage-Inhalte, Kommentare, Marker-Bezeichnungen, Ziel-Angaben und eigene Schritttexte. Geschwärztes, Maskiertes und Ungeprüftes bleibt, was es ist.
- **Wirkung in Bildern:** nur dort, wo die Position des Textes bekannt ist – am Rechteck eines Elements, dessen Name oder Wert den Text enthält, an der Titelzeile eines Fensters und am Feld, in das der Wert getippt wurde. Dort ist die ganze Fläche schwarz gefüllt, mit dem Platzhalter in heller Schrift. Alle anderen Stellen kennt das Programm nicht; es liest keine Bilder (keine Texterkennung in Version 1).
- **Nicht erfasste Bildstellen:** Der Dialog nennt, zu wie vielen Text-Fundstellen keine Bildposition bekannt ist, und führt zu den Schritten. Dort markiert der Autor die Stelle von Hand („Stelle von Hand markieren“); sie gehört dann zur Regel. Dieselben Stellen meldet die Qualitätsprüfung ([12.1](#121-qualitätshinweise)). Text, den es nur als Bildpunkte gibt – eine Remote-Sitzung, eine Zeichenfläche –, findet niemand außer dem Autor selbst; das sagt der Dialog.
- **Im Viewer** erscheinen überall die Platzhalter, damit Anzeige, Kopie und Export übereinstimmen. „Originalwerte zeigen“ (Statusleiste, Menü) blendet zur Kontrolle die aufgezeichneten Werte ein und hebt die Stellen hervor. Das ist eine reine Anzeige: Kopieren, Exportieren und Suchen liefern weiterhin die Platzhalter. Solange die Ansicht eingeschaltet ist, steht das in der Statusleiste, und jede Kopie bestätigt „mit Platzhaltern kopiert“. Beim Schließen der Sitzung schaltet sie sich aus.
- **Regeln ändern und entfernen** ist jederzeit möglich; die Rohdaten sind nicht verändert. Bei jeder Änderung werden Vorschaubilder und Suchindex verworfen und neu erzeugt, und ältere Ausgaben im Ordner `export/` werden wie nach einer Schwärzung aufgelistet ([7.6](#76-schwärzen-und-verpixeln)).
- **Die Regel nennt den Wert.** Solange sie nicht endgültig angewendet ist, steht der Wert in `edits.json` und in den Rohdaten; die Sitzung ist so vertraulich wie zuvor. Aus ihr verschwindet er erst mit der bereinigten Kopie oder dem endgültigen Anwenden – denselben zwei Wegen wie beim endgültigen Schwärzen ([7.6](#76-schwärzen-und-verpixeln), [10](10-datenschutz-und-sicherheit.md#108-endgültiges-schwärzen)).
- **Grenzen**, die der Dialog nennt: Erreicht wird nur die Schreibweise, die die Regel beschreibt – nicht der Kurzname eines Ordners, nicht der Anzeigename einer Person, nicht eine anders aufgebaute E-Mail-Adresse. Ein Wert, der über mehrere Tippläufe verteilt eingegeben wurde, wird im Getippten nicht erkannt, im Feldinhalt danach schon.
- **Verallgemeinern ist nicht Schwärzen.** Ein Platzhalter sagt, *dass* dort ein Benutzername stand. Was niemand erfahren soll, wird geschwärzt. Eine Verallgemeinerung hebt weder eine Schwärzung noch die Zurückhaltung einer ungeprüften Eingabe auf und zählt nicht als Durchsicht.

## 10 Kopieren und Herausziehen

### 10.1 Grundsätze

- **`Strg+C` kopiert das Bild so, wie es angezeigt wird:** mit der gewählten Darstellung, den Markierungen und den Schwärzungen, in voller Auflösung des gezeigten Ausschnitts – unabhängig vom Zoom.
- Jede Kopie enthält die Schwärzungen. Es gibt keinen Kopierweg ohne sie.
- Jeder Kopiervorgang wird bestätigt: „Bild kopiert · 1280 × 720“.
- Alle Befehle stehen auch im Menü **Kopieren** der Kopfleiste und im Kontextmenü von Bild, Schrittliste und Galerie.
- In einem Textfeld kopiert `Strg+C` wie gewohnt den markierten Text.

### 10.2 Kopierbefehle

| Befehl | Kürzel | Kopiert | Formate |
|---|---|---|---|
| Bild kopieren | `Strg+C` | Das Bild des Schritts, wie angezeigt | Bitmap, PNG |
| Bild mit Text kopieren | `Strg+Umschalt+C` | Nummer, Schritttext, Kommentare und Bild | HTML, RTF, Text |
| Nur Text kopieren | `Strg+Umschalt+T` | Schritttext und Kommentare | Text |
| Als Markdown kopieren | `Strg+M` | Schritt als Markdown-Listenpunkt, ohne Bildverweis ([09 §6](09-anleitungs-export.md#6-markdown)) | Text |
| Alle Schritte als Markdown kopieren | `Strg+Umschalt+M` | Ganze Anleitung als Markdown ohne Bilder, Dialekt wie zuletzt beim Markdown-Export (Standard GitHub) | Text |
| Originalbild kopieren | `Strg+Umschalt+O` | Das ganze gespeicherte Bild ohne Markierungen und Zuschnitt; Schwärzungen bleiben | Bitmap, PNG |
| Ausschnitt kopieren | `Alt+Ziehen` im Bild | Den aufgezogenen Bildteil, wie angezeigt | Bitmap, PNG |
| Als Datei kopieren | `Strg+Umschalt+D` | Das Bild als Datei | Datei |
| Mehrere Schritte kopieren | `Strg+C` bei Mehrfachauswahl | Die Bilder der gewählten Schritte nacheinander | HTML, RTF, Datei |
| Mini-Anleitung kopieren | `Strg+Umschalt+C` bei Mehrfachauswahl | Nummerierte Schritte mit Text und Bild | HTML, RTF, Text |
| Ganze Anleitung kopieren | Menü Kopieren | Wie die Mini-Anleitung, für alle sichtbaren Hauptschritte | HTML, RTF, Text |
| Als Fehlerbericht kopieren | Menü Kopieren | Der Fehlerbericht ([09](09-anleitungs-export.md#8c-fehlerbericht)) als Markdown, ohne Bildverweise; mit den Bildern gibt es ihn als Export. Voraussetzung ist die Durchsicht | Text |
| Einzelangabe kopieren | Klick auf das Kopier-Symbol | Getippter Text, Elementname, Fenstertitel, Pfad, Adresse oder Tastenfolge | Text |
| Technische Daten kopieren | Im Detailbereich | Der Schritt als JSON | Text |

### 10.3 Formate der Zwischenablage

Der Viewer legt je Befehl mehrere Formate zugleich ab; das Zielprogramm wählt das passende.

| Format | Name in Windows | Inhalt | Wer es nutzt |
|---|---|---|---|
| Bitmap | `CF_DIB` und `CF_DIBV5` | Unkomprimiertes Bild | Praktisch jedes Programm |
| PNG | Registriertes Format `PNG` | Dieselben Pixel, verlustfrei komprimiert | Office, Browser und Bildbearbeitungen bevorzugen es |
| HTML | Registriertes Format `HTML Format` | Text und Bild als HTML-Fragment; Bilder eingebettet als `data:`-Adresse | Browser-gestützte Ziele: Teams, Outlook im Web, Webmail, Wikis, Ticketsysteme |
| RTF | `Rich Text Format` | Text und eingebettetes PNG-Bild | Word, Outlook (Desktop), WordPad, LibreOffice |
| Text | `CF_UNICODETEXT` | Reiner Text oder Markdown | Jedes Programm |
| Datei | `CF_HDROP` mit `Preferred DropEffect` = Kopieren | Verweis auf eine Bilddatei in `export/.tmp/` der Sitzung | Explorer, E-Mail-Anhänge, Upload-Felder |
| Virtuelle Datei (Kann) | `FileGroupDescriptorW` und `FileContents` | Datei ohne Zwischendatei | Einzelne Ziele |

Festlegungen:

- HTML und RTF werden gemeinsam abgelegt, weil Desktop-Office RTF bevorzugt und Bilder aus `data:`-Adressen nicht zuverlässig übernimmt, während browser-gestützte Ziele HTML lesen.
- Das HTML-Fragment enthält keine Skripte und keine Verweise nach außen; aufgezeichnete Texte sind entschärft ([09](09-anleitungs-export.md#11-entschärfung-aufgezeichneter-texte)).
- In HTML und RTF sind Bilder auf eine Anzeigebreite von höchstens 16 cm begrenzt, behalten aber ihre volle Auflösung.
- **Zwischendateien** – für das Format „Datei“, das Herausziehen und das externe Bearbeiten – liegen im Sitzungsordner unter `export/.tmp/`, nie in `%TEMP%`. Sie werden beim Schließen der Sitzung entfernt; Reste eines Absturzes räumt das nächste Öffnen auf. Ihr Name ist sprechend: `Schritt-07-Speichern.png`.
- Ist die Sitzung nicht beschreibbar (schreibgeschützter Datenträger), stehen „Als Datei kopieren“, das Herausziehen und „Extern bearbeiten“ nicht zur Verfügung; das Kopieren in die Zwischenablage als Bild und Text funktioniert weiter. Bei gepackten Sitzungen liegt der Ordner neben der `.steprec`-Datei ([09](09-anleitungs-export.md#13-dateibenennung-und-ablage)).

**Zielprogramm-Matrix** – Abnahme durch Einfügen mit `Strg+V`:

| Ziel | Bild kopieren | Bild mit Text | Mini-Anleitung | Als Datei |
|---|---|---|---|---|
| Word (Desktop) | Bild | Text und Bild | Nummerierte Liste mit Bildern | – |
| Outlook (Desktop), neue Nachricht | Bild im Text | Text und Bild | Nummerierte Liste mit Bildern | Anhang |
| Teams (neu), Nachrichtenfeld | Bild | Text und Bild | Text und Bilder | Datei-Upload |
| Outlook im Web, Gmail (Edge, Chrome) | Bild | Text und Bild | Text und Bilder | – |
| OneNote | Bild | Text und Bild | Text und Bilder | – |
| LibreOffice Writer | Bild | Text und Bild | Text und Bilder | – |
| Paint, paint.net | Bild | – | – | – |
| Explorer | – | – | – | Datei im Ordner |
| Editor (Notepad) | – | Text | Text | – |

Word, Outlook (Desktop), Teams, Paint, Explorer und Editor sind Muss, die übrigen Soll.

### 10.4 Ausschnitt kopieren

- `Alt` gedrückt halten und im Bild ein Rechteck aufziehen: Beim Loslassen liegt der Ausschnitt in der Zwischenablage. Kein Werkzeugwechsel, keine Rückfrage.
- Der Ausschnitt enthält Markierungen und Schwärzungen, wie angezeigt, in voller Auflösung.
- Gilt bei gewähltem Auswahl-Werkzeug; `Esc` während des Ziehens bricht ab.
- Der Ausschnitt ändert nichts am Schritt.

### 10.5 Mini-Anleitung

Mehrere gewählte Schritte werden als nummerierte Liste kopiert:

```
1. Wählen Sie den Menüpunkt „Datei“.
   [Bild]
2. Wählen Sie den Menüpunkt „Speichern unter“.
   [Bild]
3. Geben Sie in das Feld „Dateiname“ „notiz.txt“ ein.
   [Bild]
```

- Die Nummerierung beginnt bei 1 (einstellbar: Nummern der Anleitung behalten).
- Kommentare stehen unter ihrem Schritt.
- Ab 0,5 Sekunden Dauer erscheint eine Fortschrittsanzeige; der Vorgang ist abbrechbar.
- Für mehr als 50 Schritte empfiehlt der Viewer den Export.

### 10.6 Herausziehen

- Ein **Vorschaubild** aus Schrittliste oder Galerie lässt sich aus dem Fenster heraus in ein anderes Programm ziehen. Innerhalb der Liste sortiert dieselbe Bewegung um; erst beim Verlassen der Liste wird daraus das Herausziehen, erkennbar am Mauszeiger.
- Am Bild selbst dient der Griff **„Bild ziehen“** oben rechts.
- Übergeben wird das Bild wie angezeigt, als Datei und als Bitmap. Mehrere gewählte Schritte ergeben mehrere Dateien.
- Ziel kann ein Explorer-Ordner, eine E-Mail, ein Chat, ein Dokument oder ein Upload-Feld im Browser sein.

### 10.7 Speichern

- **Bild speichern unter** (`Strg+Umschalt+S`): das Bild wie angezeigt, als PNG oder JPEG.
- **Alle Bilder exportieren:** nummerierte Bilddateien in einen Ordner. Wählbar: mit oder ohne Markierungen, Darstellung, Haupt- oder alle Schritte, Namensmuster (Standard `Schritt-{Nr}-{Kurztext}.png`).
- `Strg+S` zeigt nur „Automatisch gespeichert“ – zu speichern gibt es nichts.

### 10.8 Einstellungen zum Kopieren

| Einstellung | Werte | Standard |
|---|---|---|
| Bild beim Kopieren verkleinern | aus · höchstens 1280, 1600 oder 1920 Pixel Breite | aus |
| Dateiformat für „Als Datei“, Herausziehen, Speichern | PNG · JPEG (Qualität 85 bis 95) | PNG |
| Text beim Bild | Nummer und Text · nur Text | Nummer und Text |
| Kommentare mitkopieren | an · aus | an |
| Dünner Rahmen um kopierte Bilder | an · aus | aus |
| Nicht in die Cloud-Zwischenablage hochladen | an · aus | an |
| Nicht in den Zwischenablage-Verlauf aufnehmen | an · aus | aus |

Die beiden letzten Einstellungen setzen die dafür vorgesehenen Windows-Kennzeichen an der Zwischenablage.

### 10.9 Datenschutz beim Kopieren

- Schwärzungen sind in jeder Kopie enthalten, auch in „Originalbild“ und „Technische Daten“.
- Enthält eine Text-Kopie eine **ungeprüfte auffällige Eingabe**, fragt der Viewer einmal je Sitzung nach und bietet die [Durchsicht der Eingaben](#13-durchsicht-der-eingaben) an. Bild-Kopien fragen nicht – der Autor sieht, was er kopiert. Maßgeblich ist [10](10-datenschutz-und-sicherheit.md#11-prüfung-vor-dem-export).
- Ist die Zwischenablage von einem anderen Programm gesperrt, versucht es der Viewer eine halbe Sekunde lang erneut und meldet dann den Fehlschlag.

## 11 Sitzungen verwalten

```
┌─ Alle Aufnahmen ────────────────────────────────────────────────────────────────────┐
│ Suche: [____________]   Sortierung: [Datum v]   Ordner: C:\...\Aufnahmen  [Ändern]  │
├─────────────────────────────────────────────────────────────────────────────────────┤
│ ┌──────┐ 2026-11-11 14.30 – Editor – notiz.txt                                      │
│ │ Bild │ 23 Schritte · 2 min · 14 MB · [Editor] [Speichern]          [Öffnen] [...] │
│ └──────┘                                                                            │
│ ┌──────┐ 2026-11-10 09.12 – Explorer – Dokumente                                    │
│ │ Bild │ 11 Schritte · 1 min · 6 MB                                  [Öffnen] [...] │
│ └──────┘                                                                            │
│ ┌──────┐ 2026-11-09 16.05 – Aufnahme (unvollständig)        (!) nicht abgeschlossen │
│ │ Bild │ lesbar bis 00:04:12                                         [Öffnen] [...] │
│ └──────┘                                                                            │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

- Die **Sitzungsbibliothek** („Alle Aufnahmen“, `Strg+L` im Editor und im Startfenster) ist ein eigenes Fenster; ein zweiter Aufruf holt es nach vorn und aktualisiert es. Sie findet die Aufnahmen und `.steprec`-Archive des Aufnahmeordners, ohne Verknüpfungen zu folgen, und zeigt je Aufnahme Vorschaubild, Titel, Schrittzahl (Hauptschritte), Dauer und Größe. Das Vorschaubild kommt über die Leseschicht und enthält alle Schwärzungen – nie ein Rohbild. Titel, Stand der Durchsicht und Vorschaubild merkt sich das Programm unter `cache/`.
- **Kennzeichen** je Aufnahme: „Aufnahme läuft“, „nicht regulär beendet“, „nicht lesbar“, „schreibgeschützt (.steprec)“, „Durchsicht offen (N)“, „im Editor geöffnet“ und die Art des Aufnahmeprofils.
- **Filter und Sortierung:** Das Suchfeld filtert nach Titel („Titel“) oder sucht in den Inhalten aller Aufnahmen („Inhalte“, [11.2](#112-volltextsuche-über-alle-sitzungen)). Sortiert wird nach Datum (neueste oder älteste zuerst), Titel, Dauer oder Schrittzahl. Mehrfachauswahl ist möglich.
- **Aktionen:** Öffnen (`Eingabe`), Umbenennen (`F2`), Packen …, Im Explorer zeigen, Löschen (`Entf`); im Menü „Weitere Aktionen“ Exporte löschen …, Aus „Zuletzt“ entfernen und Pfad kopieren. `F5` aktualisiert, `Strg+F` und `Strg+Umschalt+F` setzen den Fokus ins Suchfeld im Modus „Titel“ bzw. „Inhalte“. Schlagwörter, Duplizieren, Verschieben, Vergleichen mit … und Support-Paket erstellen sind Soll.
- **Umbenennen** ändert den Titel in der Aufnahme, nicht den Ordnernamen. Bei `.steprec`-Archiven ist es gesperrt: erst entpacken.
- **Packen …** speichert die Aufnahme als `.steprec`-Datei und fragt, wie ([11.3](#113-packen-und-entpacken)). Im Editor steht dieselbe Funktion im Menü „…“ als „Als .steprec packen …“.
- **Entpacken:** Eine `.steprec`-Datei, die auf das Fenster gezogen oder über „Entpacken …“ gewählt wird, entpackt das Programm in den Aufnahmeordner und öffnet die Aufnahme ([11.3](#113-packen-und-entpacken)).
- **Automatischer Name:** Jede Sitzung erhält beim Stopp einen Titel nach dem Muster `{Datum} {Zeit} – {Anwendung} – {Titel}`, mit der Anwendung, in der die meisten Hauptschritte liegen, und dem ersten Fenstertitel. Der Ordner wird passend benannt; unzulässige Zeichen werden ersetzt. Gelingt das Umbenennen nicht, bleibt der Zeitstempel als Ordnername.
- **Lokal aufgenommen, später verschoben:** Wurde wegen der Sync-Warnung lokal aufgenommen ([07](07-aufnahme-bedienung.md#13-einstellungen)), bietet der Viewer nach der Durchsicht der Eingaben an, die Sitzung in den ursprünglich gewählten Ordner zu verschieben (Soll).
- **Löschen** verschiebt die Aufnahme nach Rückfrage in den Papierkorb. Geöffnete und laufende Aufnahmen lassen sich nicht löschen. Gibt es auf dem Laufwerk keinen Papierkorb, fragt das Programm ausdrücklich nach endgültigem Löschen und löscht nie stillschweigend. „Exporte löschen …“ entfernt nur die Ausgaben im Ordner `export/` auf demselben Weg.
- **Aufnahme anhängen:** „Aufnahme fortsetzen“ im Viewer startet eine Aufnahme mit dem Aufnahmebereich der Sitzung. Die neuen Schritte stehen am Ende; der Viewer öffnet sich danach beim ersten neuen Schritt. „Hier einfügen“ stellt sie in der Anleitung hinter den gewählten Schritt (Kann).
- Unvollständige und nicht lesbare Sitzungen sind gekennzeichnet.
- Die Bibliothek liest je Sitzung nur Kopfdaten und Vorschaubild, für die Suche in den Inhalten zusätzlich den Suchindex der Sitzung.

### 11.1 KI-Agenten anbinden

Ein Agent wie Claude Code liest Sitzungen über den MCP-Server ([06](06-ki-export-und-mcp.md#15-mcp-server-überblick)). Dazu braucht er einen Eintrag in seiner eigenen Konfiguration. Der Viewer liefert diesen Eintrag fertig zum Kopieren.

```
┌─ KI-Agenten anbinden (MCP) ──────────────────────────────────────────────────────┐
│ Freigegebener Ordner: [D:\Werkzeuge\StepRecorder\Sessions          ] [Ändern]    │
│ [ ] Agent darf Schritttexte schreiben          Sprache der Antworten: [Deutsch v]│
│                                                                                  │
│ Claude Code – Datei .mcp.json im Projektordner                      [Kopieren]   │
│   { "mcpServers": { "steprecorder": {                                            │
│       "command": "D:\\Werkzeuge\\StepRecorder\\SebastiansStepRecorder.exe",                │
│       "args": ["mcp", "--root", "D:\\Werkzeuge\\StepRecorder\\Sessions"] } } }   │
│                                                                                  │
│ Claude Code – als Befehl                                            [Kopieren]   │
│ Claude Desktop – Datei claude_desktop_config.json                   [Kopieren]   │
│                                                                                  │
│ Das Programm trägt sich nirgends selbst ein.                        [Schließen]  │
└──────────────────────────────────────────────────────────────────────────────────┘
```

- Aufruf über das Menü der Kopfleiste, die Sitzungsbibliothek und die Einstellungen ([07](07-aufnahme-bedienung.md#13-einstellungen)).
- Gezeigt wird die Konfiguration für Claude Code – als Datei und als Befehl – und für Claude Desktop, jeweils mit dem **tatsächlichen Pfad der EXE** und dem gewählten Ordner; Rückstriche sind für JSON bereits verdoppelt.
- Der freigegebene Ordner ist mit dem Aufnahmeordner vorbelegt. Nur was darin liegt, kann der Agent lesen.
- „Agent darf Schritttexte schreiben“ ergänzt die Option `--allow-edit`; die Sprache ergänzt `--lang`. Weitere Optionen stehen in [06](06-ki-export-und-mcp.md#161-kommandozeile).
- **Das Programm schreibt in keine fremde Datei** und startet hier keinen Server. Der Dialog zeigt nur Text; den Eintrag fügt der Autor selbst ein.
- Der Dialog weist darauf hin, dass ein Agent das Gelesene an seinen KI-Dienst senden kann, und dass ungeprüfte auffällige Eingaben bis zur [Durchsicht](#13-durchsicht-der-eingaben) zurückgehalten werden ([10](10-datenschutz-und-sicherheit.md#111-durchsicht-aller-eingaben)).
- Liegt die EXE an einem Ort, der sich leicht ändert – etwa im Download-Ordner oder auf einem Wechseldatenträger –, sagt der Dialog das dazu: Der Eintrag gilt nur, solange der Pfad stimmt.

### 11.2 Volltextsuche über alle Sitzungen

„Wo habe ich das schon einmal aufgenommen?“ – die Suche der Sitzungsbibliothek findet Text in den Inhalten aller Sitzungen des Aufnahmeordners.

```
┌─ Alle Aufnahmen ───────────────────────────────────────────────────────────────────────┐
│ Suche: [speichern unter______]  (•) Inhalte  ( ) Nur Titel       Sortierung: [Datum v] │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 2026-11-11 14.30 – Editor – notiz.txt                                        3 Treffer │
│   Schritt 7   Klick auf Schaltfläche „Speichern unter…“                         [Bild] │
│   Schritt 9   Fenster „Speichern unter“                                         [Bild] │
│   Ziel        „Speichern unter neuem Namen zeigen“                                     │
│ 2026-11-03 10.02 – Word – Bericht.docx                                       1 Treffer │
│   Schritt 14  Kommentar „Hier Speichern unter wählen“                           [Bild] │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 40 Sitzungen · 38 durchsucht · 2 noch nicht durchsucht (werden nachgeholt)             │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Aufruf:** das Suchfeld der Sitzungsbibliothek mit der Wahl „Inhalte“; in der Bibliothek setzt `Strg+Umschalt+F` den Fokus dorthin. Die Wahl „Titel“ oder „Inhalte“ wird gemerkt. „Nur Titel“ sucht wie bisher in Titel, Schlagwörtern und Anwendungen.
- **Durchsucht werden** die Texte, die der Suchindex führt ([06](06-ki-export-und-mcp.md#197-search)): getippte Texte, Elementnamen, Fenstertitel, Kommentare, das Ziel der Aufnahme – Ziel, erwartetes Ergebnis, Bezug –, Sitzungstitel, eigene Schritttexte und Marker-Texte. Feldinhalte, Text-Schnappschüsse und Zwischenablage-Inhalte stehen nicht im Index; sie findet nur die Suche innerhalb einer Sitzung ([5.6](#56-suche-und-filter)).
- **Treffer** stehen gruppiert je Sitzung, mit Schritt, einer Zeile Umgebung, hervorgehobener Fundstelle und Vorschaubild. Ein Klick öffnet die Sitzung im Viewer an diesem Schritt; der Suchbegriff ist dort als Suche übernommen. Treffer in Ziel und Titel gehören zu keinem Schritt und öffnen die Sitzung am Anfang.
- Treffer in ersetzten Schritten und in nicht verwendeten Neuaufnahmen ([9.3](#93-abschnitt-neu-aufnehmen-und-ersetzen)) sind als solche gekennzeichnet; verworfene Abschnitte durchsucht die Suche nicht.
- **Suchregeln:** ohne Beachtung der Groß- und Kleinschreibung; mehrere Wörter müssen alle vorkommen; Anführungszeichen suchen die genaue Wortfolge. Sortiert wird nach Datum oder Trefferzahl.
- **Index:** Je Sitzung liegt ein Suchindex unter `cache/search/` ([04](04-datenformat.md#cache)). Er ist abgeleitet, trägt eine Formatversion und lässt sich jederzeit löschen. Gebaut wird er nach dem Stopp im Hintergrund, nie während einer Aufnahme; einen zentralen Index gibt es nicht ([02](02-architektur.md#suchindex)).
- **Nie stillschweigend trefferlos:** Eine Sitzung ohne gültigen Index wird beim Suchen im Hintergrund nachgeholt und bis dahin als „noch nicht durchsucht“ gezählt. Treffer erscheinen fortlaufend. Laufende, abgestürzte und nicht lesbare Sitzungen überspringt die Suche und nennt ihre Zahl.
- **Nur Freigegebenes:** Der Index entsteht aus dem, was die Leseschicht an Abnehmer ohne Oberfläche herausgibt. Geschwärzte, maskierte und ungeprüfte auffällige Inhalte stehen nie darin, ebenso wenig interne und ausgeblendete Kommentare; verallgemeinerte Werte stehen nur als Platzhalter darin. Ändern sich Schwärzungen, Verallgemeinerungen oder der Stand der Durchsicht, wird der Index verworfen, bevor die Änderung gespeichert ist ([10](10-datenschutz-und-sicherheit.md#106-abgeleitete-daten-und-frühere-exporte)).
- Bei gepackten Sitzungen und auf schreibgeschützten Datenträgern lebt der Index nur im Arbeitsspeicher; die Suche funktioniert, dauert beim ersten Mal aber länger.
- **Geschwindigkeit:** Mit vorhandenen Indizes erscheinen die Treffer aus 200 Sitzungen auf System A in höchstens 2 Sekunden ([02](02-architektur.md#suchindex)). Kein Öffnen einer Sitzung und kein Aufnahmestart wartet auf den Index.
- Der MCP-Server bietet dieselbe Suche über die Sitzungen seines freigegebenen Ordners – das Werkzeug `search` mit `scope: "all"` ([06](06-ki-export-und-mcp.md#197-search)).

### 11.3 Packen und Entpacken

**Packen** (Bibliothek, Menü „…“ im Editor) schreibt eine `.steprec`-Datei und fragt zuerst, welche Fassung:

| Fassung | Inhalt | Voraussetzung |
|---|---|---|
| **Bereinigte Kopie** – zum Weitergeben | Eine neue Aufnahme aus der Sicht der Leseschicht: Schwärzungen und Verallgemeinerungen sind fest angewendet, interne Kommentare und Bearbeitungsstände entfernt, Bilder geschwärzter Zeiträume gelöscht; sie erhält eine neue Kennung. Vor dem Schreiben prüft das Programm alle Textdateien auf geschwärzte und verallgemeinerte Inhalte; schlägt das fehl, entsteht nichts | Die Durchsicht ist abgeschlossen; sonst ist die Fassung gesperrt |
| **Vollständig** – zum Weiterbearbeiten | Alle Rohdaten, auch was im Editor geschwärzt, verallgemeinert oder ausgeblendet ist | Eine zweite Bestätigung mit der Liste dessen, was der Empfänger sehen oder aufheben kann |

- Gepackt werden nur bekannte Teile; nie `cache`, `export`, `history` und die Sperrdatei. Absolute Pfade von Anhängen werden entfernt. Der Dateiname ergibt sich aus dem Titel (bei der bereinigten Kopie mit dem Zusatz „(bereinigt)“).
- **Entpacken** prüft das Archiv, bevor etwas im Aufnahmeordner entsteht: Eintrag für Eintrag nach einer Positivliste, mit Grenzen für Anzahl, Größe und Kompressionsverhältnis, über einen Zwischenordner. Ein präpariertes Archiv hinterlässt nichts. Liegt dieselbe Aufnahme schon im Ordner, fragt das Programm nach.
- Aufnahmen, die im Archiv angehängt sind, übernimmt das Programm nicht; ein Archiv trägt keine Bearbeitungsstände.
## 12 Komfortfunktionen

| Funktion | Beschreibung | Priorität |
|---|---|---|
| Automatisches Speichern | Jede Bearbeitung ist spätestens eine halbe Sekunde später gespeichert; es gibt keinen Speichern-Befehl | Muss |
| Automatische Sitzungsnamen | Siehe [Abschnitt 11](#11-sitzungen-verwalten) | Muss |
| Durchsicht der Eingaben vor dem Export | Siehe [Abschnitt 13](#13-durchsicht-der-eingaben) | Muss |
| Vollständige Tastaturbedienung | Jede Funktion ist ohne Maus erreichbar | Muss |
| Kürzelübersicht | `F1` zeigt alle Tastenkürzel, durchsuchbar ([15](#15-tastenkürzel)) | Soll |
| Sitzungsbibliothek mit Suche | Siehe [Abschnitt 11](#11-sitzungen-verwalten) | Soll |
| Ein-Klick-Export | `Strg+Umschalt+E` wiederholt den letzten Export dieser Aufnahme mit denselben Einstellungen und demselben Ziel ([09](09-anleitungs-export.md#32-ein-klick-export)) | Soll |
| Hell und Dunkel | Folgt der Windows-Einstellung; umschaltbar | Soll |
| Stilvorlagen für Markierungen | Siehe [7.3](#73-eigenschaften-und-stilvorlagen) | Soll |
| Vorschläge zum Schwärzen | Siehe [7.6](#76-schwärzen-und-verpixeln) | Soll |
| Aufnahme anhängen | Siehe [Abschnitt 11](#11-sitzungen-verwalten) | Soll |
| Am Element einrasten | Siehe [7.4](#74-am-element-einrasten) | Soll |
| Ganze Anleitung in die Zwischenablage | Siehe [10.2](#102-kopierbefehle) | Soll |
| Darstellung für alle Schritte auf einmal | Ein Klick stellt alle Bilder auf Fenster oder Ausschnitt um | Soll |
| Zuletzt gesehene Stelle | Eine Sitzung öffnet sich am zuletzt gewählten Schritt; Fensterlage und Spaltenbreiten bleiben erhalten, siehe [4.5](#45-fensterlage-und-gemerkter-zustand) | Soll |
| Vorschau beim Überfahren der Zeitleiste | Siehe [5.5](#55-zeitleiste) | Soll |
| Dauer und Wartezeit je Schritt | Im Detailbereich; hilft bei Hinweisen wie „kurz warten“ | Soll |
| Ziel der Aufnahme im Sitzungskopf | Ziel, erwartetes Ergebnis und Bezug ansehen und ändern, siehe [4.4](#44-ziel-der-aufnahme-und-marker-typen) | Soll |
| Marker nach Typ | Typ am Fähnchen, Filter und Sprung, siehe [4.4](#44-ziel-der-aufnahme-und-marker-typen) | Soll |
| Zwei Aufnahmen vergleichen | Siehe [5.9](#59-zwei-aufnahmen-vergleichen) | Soll |
| Abschnitt neu aufnehmen und ersetzen | Siehe [9.3](#93-abschnitt-neu-aufnehmen-und-ersetzen) | Soll |
| Werte verallgemeinern | Persönliche Werte durch Platzhalter ersetzen, siehe [9.4](#94-werte-verallgemeinern) | Soll |
| Qualitätshinweise | Fenster der Qualitätsprüfung (`F8`), siehe [12.1](#121-qualitätshinweise) | Soll |
| Support-Paket | Geführter Ablauf nach einer Support-Aufnahme, siehe [12.2](#122-support-paket-geführter-ablauf) | Soll |
| Volltextsuche über alle Sitzungen | Siehe [11.2](#112-volltextsuche-über-alle-sitzungen) | Soll |
| Schritt neu bebildern | „Bild neu aufnehmen“ ersetzt das Bild eines Schritts durch einen frischen Schnappschuss | Kann |
| Befehlspalette | `Strg+Umschalt+P` findet jeden Befehl über seinen Namen | Kann – *umgesetzt (Paket 15 aus [12](12-entwickler-fahrplan.md)): Suche über Namen und Gruppe, zuletzt benutzte zuerst, Kürzel daneben; auch im Menü „Hilfe“* |
| Lesezeichen | Schritte mit einem Stern merken und danach filtern | Kann – *umgesetzt: `Strg+Umschalt+L`, der Stern oben rechts im Detailbereich oder Schritt › Lesezeichen setzen (bei Mehrfachauswahl für alle); gemerkte Schritte tragen in der Liste einen Stern, der Stern neben der Suche (auch Ansicht › Nur Lesezeichen) zeigt nur sie. Gespeichert als `edits.steps[].bookmark` ([04](04-datenformat.md#edits-steps)); Arbeitsnotiz des Autors, erscheint in keiner Ausgabe und fällt in der bereinigten Kopie weg* |
| Textbausteine | Häufige Hinweise per Kürzel einfügen | Kann – *umgesetzt: Bausteine (Name und Text) stehen in den Einstellungen (Allgemein › Textbausteine verwalten …). `Strg+Umschalt+B` oder der Knopf „Textbaustein“ fügt an der Schreibmarke ein – in Überschrift und Anleitungstext des Detailbereichs, im Hinweis-Dialog (`K`) und in den übrigen Textdialogen; das Menü führt auch zur Verwaltung. Im Einstellungs-Austausch der Teil `textSnippets` ([07, 13.2](07-aufnahme-bedienung.md#132-einstellungen-exportieren-und-importieren))* |
| Rechtschreibprüfung | In Schritttexten und Kommentaren, soweit Windows die Sprache bereitstellt | Kann |
| Kapitelvorschläge | Vorschlag für Kapitel bei jedem Wechsel der Anwendung | Kann |
| Präsentationsansicht | Vollbild mit Bild und Schritttext zum Durchklicken in Schulungen | Kann |
| Bearbeitungsstände | Ältere Stände der Bearbeitung wiederherstellen, siehe [7.10](#710-bearbeitungsstände) | Kann |
| Mehrere Fenster | Zwei Sitzungen nebeneinander, siehe [3](#3-öffnen-und-schließen) | Kann |
| Wiedergabe als GIF oder Video | Siehe [Abschnitt 6](#6-abspielen) | Kann |

### 12.1 Qualitätshinweise

Die Qualitätsprüfung sucht Stellen, die eine Anleitung schlechter machen: ein Schritt ohne benanntes Ziel, eine Klickmarke außerhalb des Bildes, ein Text, der fehlt. Der Regelkatalog steht in [09](09-anleitungs-export.md#121-qualitätsprüfung-regelkatalog); der Viewer zeigt ihn jederzeit als Hinweisliste, der Exportdialog fasst ihn zusammen, und die Kommandozeile gibt ihn mit `check` und bei `export` aus.

```
┌─ Qualität ───────────────────────────────────────────────────────────────────────────┐
│ 3 Warnungen · 3 Hinweise                                             [Erneut prüfen] │
│ ──────────────────────────────────────────────────────────────────────────────────── │
│ (!) Q12 Auffällige Eingabe ungeprüft            Schritt 6   [Prüfen]   [Ignorieren]  │
│ (!) Q08 Ausschnitt schneidet das Ziel ab        Schritt 12  [Anpassen] [Ignorieren]  │
│ (!) Q09 Klickmarke außerhalb des Ausschnitts    Schritt 15  [Anpassen] [Ignorieren]  │
│ (i) Q02 Nur durch die Eingabe belegt            Schritt 9              [Ignorieren]  │
│ (i) Q11 Gleicher Text wie der Schritt davor     Schritt 18             [Ignorieren]  │
│ (i) Q14 Wert nicht verallgemeinert              3 Schritte  [Regel …]  [Ignorieren]  │
│ ──────────────────────────────────────────────────────────────────────────────────── │
│ [Ignorierte zeigen (1)]                                                              │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

- **Aufruf:** `F8`, der Link „Qualität: …“ in der Statusleiste, das Menü „…“ oder „Ansehen (F8)“ im Exportdialog. Die Liste ist ein **eigenes, nicht modales Fenster** (neben dem Editor); der Editor bleibt dabei bedienbar. Das Fenster zeigt oben „N Warnungen · M Hinweise“, „Ignorierte zeigen“ und „Erneut prüfen“; ohne Befunde steht dort „Keine Befunde – die Anleitung ist bereit.“
- **Regeln:** Es gilt der Regelkatalog Q01 ff. aus [09](09-anleitungs-export.md#121-qualitätsprüfung-regelkatalog). Jede Fundstelle nennt die Regel, ihre Schwere – Hinweis oder Warnung – und den Schritt. Die Liste steht nach Schwere geordnet – **Warnungen zuerst** – und innerhalb davon nach Schrittnummer. Was bei Q15 und Q18 bisher geprüft wird, steht in [09](09-anleitungs-export.md#121-qualitätsprüfung-regelkatalog).
- **Sprung:** Ein Klick auf die Fundstelle wählt den Schritt (bei Bedarf mit Filter „Alle“); die Statuszeile nennt die Regel. `F4` und `Umschalt+F4` gehen zur nächsten und vorigen Fundstelle mit Schritt, im Kreis. Die Fenster bleiben dabei offen.
- **Abhilfe:** Wo es eine naheliegende gibt, bietet die Fundstelle sie als Schaltfläche an: erst Sprung zum Schritt, dann das passende Werkzeug – „Durchsicht …“, „Text bearbeiten“, „Übersetzen“, „Zuschneiden“ (Zuschnitt-Werkzeug), „Ausblenden“, „Hinweis einfügen“, „Bilder prüfen …“, „Bild ersetzen …“ oder „Zusammenfassen“. Sonst heißt die Schaltfläche „Zum Schritt“.
- **Ignorieren:** „Ignorieren“ blendet genau diese Fundstelle aus; sie zählt dann auch in der Export-Vorprüfung nicht mehr. Gespeichert wird je Fundstelle in `edits.qualityIgnores` ([04](04-datenformat.md#edits-quality)). „Ignorierte zeigen“ holt sie zurück, dort heißt die Schaltfläche „Wieder berücksichtigen“. (Soll: „Alle Fundstellen dieser Regel ignorieren“, das sich jede Fundstelle einzeln merkt, sodass neue wieder erscheinen.)
- **Ignorieren hebt keinen Schutz auf.** Eine ungeprüfte auffällige Eingabe (Q12) führt in die [Durchsicht](#13-durchsicht-der-eingaben). Wer den Hinweis ignoriert, hat die Sperre vor der Weitergabe weiterhin vor sich; die Schaltfläche trägt den Hinweis „Ignorieren hebt die Durchsichtspflicht nicht auf.“ Auch Schwärzungen und Verallgemeinerungen bleiben unberührt.
- **Aktualität:** Der Editor behält die Befunde bis zur nächsten Änderung und rechnet erst dann neu; im Fenster gibt „Erneut prüfen“ den Anstoß. Die Statusleiste zeigt den Stand („Qualität: 2 Warnungen, 5 Hinweise“ bzw. „Qualität: keine Befunde“). Schritte mit einer Warnung tragen in der Schrittliste ein Kennzeichen mit der Regelnummer. Der Filter „mit Qualitätshinweis“ ([5.6](#56-suche-und-filter)) ist Soll.
- **Umfang:** Geprüft werden die Hauptschritte in der Sprache der Anleitungstexte und mit der Darstellung aus den Dokument-Einstellungen der Aufnahme. Die Kommandozeile prüft mit `--importance` und `--lang` ([09](09-anleitungs-export.md#14c-befehl-check)).
- Für Sitzung M steht die Liste auf den Systemen A und B höchstens eine Sekunde nach dem Aufruf.

### 12.2 Support-Paket: geführter Ablauf

Nach einer Support-Aufnahme ([07](07-aufnahme-bedienung.md#49-support-paket-aufnehmen)) führt der Viewer in drei Schritten zum fertigen Paket (Soll). Wer hier sitzt, hat das Programm vielleicht zum ersten Mal vor sich; deshalb zeigt der Ablauf nur, was er braucht.

```
┌─ Support-Paket ──────────────────────────────────────────────────────────────────────────┐
│ (1) Ansehen  ────────  (2) Eingaben prüfen  ────────  (3) Paket erstellen                │
│                                                                                          │
│ Schritt 1 von 3: Sehen Sie den Ablauf an. Ist etwas zu sehen, das nicht hineingehört?    │
│ Dann schwärzen Sie es – oder nehmen Sie die Stelle neu auf.                              │
│                                                                                          │
│ [Abbrechen]                                                         [Zurück]  [Weiter >] │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

| Schritt | Was der Viewer zeigt | Weiter geht es, wenn |
|---|---|---|
| 1 Ansehen | Schrittliste, Bild und Schritttext; Abspielen. Von den Werkzeugen sind Schwärzen und Verpixeln sichtbar, alles andere liegt hinter „Alle Funktionen“ | der Autor „Weiter“ wählt |
| 2 Eingaben prüfen | Die [Durchsicht der Eingaben](#13-durchsicht-der-eingaben), eingebettet. Oben stehen die Vorschläge zum Verallgemeinern ([9.4](#94-werte-verallgemeinern)); jeder Eintrag bietet Schwärzen und Verallgemeinern an | keine Eingabe mehr ungeprüft ist: Jede auffällige ist einzeln entschieden, die übrigen sind ausdrücklich bestätigt – auch wenn keine einzige auffällig ist |
| 3 Paket erstellen | Was das Paket enthält, wie die Datei heißt, wo sie liegen wird; dazu das Feld „Hinweis für den Empfänger“ | – |

```
┌─ Support-Paket · Schritt 3 von 3 ────────────────────────────────────────────────┐
│ Das Paket enthält:                                                               │
│   Anleitung (eine HTML-Datei)   der Ablauf zum Lesen, 12 Schritte                │
│   KI-Paket                      derselbe Ablauf für eine KI                      │
│ 3 Schwärzungen und 2 Platzhalter werden endgültig angewendet.                    │
│                                                                                  │
│ Hinweis für den Empfänger:                                                       │
│ [Der Fehler tritt nur beim ersten Speichern nach dem Start auf.                ] │
│                                                                                  │
│ Datei:   Support-2026-11-11-Editor – notiz.txt.zip                               │
│ Ordner:  D:\Werkzeuge\StepRecorder\Sessions\…\export                    [Ändern] │
│                                                                                  │
│ Das Programm versendet nichts. Sie geben die Datei selbst weiter.                │
│ [Abbrechen]                                          [Zurück]  [Paket erstellen] │
└──────────────────────────────────────────────────────────────────────────────────┘
```

- **Die Durchsicht ist hier Pflicht.** Schritt 2 lässt sich nicht überspringen; „Weiter“ ist gesperrt, solange eine Eingabe ungeprüft ist. Das gilt auch für unauffällige Eingaben und für Ziel, erwartetes Ergebnis und Bezug. Die Komponente, die das Paket baut, prüft die Vorbedingung selbst noch einmal ([02](02-architektur.md#support-paket)).
- **Hinweis für den Empfänger:** ein freier Text des Autors an den, der helfen soll – vorbelegt, wenn Profil oder Kommandozeile ihn liefern. Gespeichert wird er als Einleitung der Anleitung (`edits.guide.intro`, [04](04-datenformat.md#edits-json)); er steht am Anfang der HTML-Anleitung, nicht im KI-Paket ([06](06-ki-export-und-mcp.md#32-das-ki-paket-im-support-paket)). Er gehört zur Durchsicht wie die Ziel-Angaben: Wird er nach der Durchsicht geändert, gilt er wieder als nicht durchgesehen, und trifft die Musterprüfung, verlangt der Ablauf die Entscheidung, bevor das Paket entsteht.
- **Paket erstellen** erzeugt eine ZIP-Datei `Support-<Datum>-<Titel>.zip` mit der Anleitung als einer HTML-Datei und dem KI-Paket – beides aus einer bereinigten Kopie der Sitzung, in der Schwärzungen und Verallgemeinerungen endgültig angewendet sind. Inhalt, Benennung und Ablage stehen in [09](09-anleitungs-export.md#35-support-paket), der Ablauf im Einzelnen in [02](02-architektur.md#support-paket), die Schutzregeln in [10](10-datenschutz-und-sicherheit.md#113-support-paket). Der letzte Schritt zeigt den Dateinamen, bevor die Datei entsteht, und lässt den Titel darin ändern; auch der Name ist eine Ausgabe und trägt Platzhalter statt verallgemeinerter Werte.
- **Danach** öffnet sich der Explorer mit dem Ordner der Datei. Der Viewer zeigt den Pfad und die Aktionen „Pfad kopieren“ und „Datei kopieren“.
- **Das Programm versendet nichts.** Es öffnet kein Mailprogramm, keinen Teilen-Dialog und keine Internetseite; weiterzugeben ist Sache der Person am Rechner.
- **Abbrechen** verlässt den Ablauf jederzeit und führt in den gewöhnlichen Viewer; die Sitzung bleibt erhalten. „Support-Paket erstellen …“ im Menü **Export** setzt ihn fort und steht für jede Sitzung bereit, auch für eine ohne Support-Aufnahme.
- **Zurück** führt zum vorigen Schritt, ohne Entscheidungen zu verlieren.
- Schlägt das Erstellen fehl – der Datenträger ist voll, oder die Gegenprobe der bereinigten Kopie findet einen geschwärzten Inhalt –, nennt der Viewer den Grund. Es entsteht kein Paket, und am Ziel bleibt keine unvollständige Datei.
- Die Höchstzahl an Aktionen steht in [2.1](#21-bedienabläufe-mit-höchstzahl) (V20, Kernablauf 3). Sie ist das Abnahmekriterium des Tests T-16 in [11](11-roadmap-tests-risiken.md).

## 13 Durchsicht der Eingaben

Die Durchsicht ist Teil des Schutznetzes: Bevor etwas die Sitzung verlässt, sieht der Autor alle Texte, die über Tastatur oder Zwischenablage in die Sitzung gelangt sind. Wann sie Pflicht ist und welche Muster als auffällig gelten, regelt [10](10-datenschutz-und-sicherheit.md#111-durchsicht-aller-eingaben); hier steht die Bedienung.

```
┌─ Eingaben prüfen ────────────────────────────────────────────────────────────────────┐
│ 14 Eingaben · 2 aus Klartext-Fenstern · 1 Mustertreffer        Zeigen: [Alle v]      │
├──────────────────────────────────────────────────────────────────────────────────────┤
│ (!) Schritt 12 · Windows Terminal · Klartext-Fenster                                 │
│     Getippt: ssh admin@server01{Enter}                       [In Ordnung] [Schwärzen]│
│ (!) Schritt 13 · Windows Terminal · Klartext-Fenster · Muster: mögliches Passwort    │
│     Getippt: Sommer2026!{Enter}                              [In Ordnung] [Schwärzen]│
│     Schritt 6 · Editor · Feld „Dateiname“                                            │
│     Getippt: notiz.txt                                       [In Ordnung] [Schwärzen]│
│     ...                                                                              │
├──────────────────────────────────────────────────────────────────────────────────────┤
│ [Alle unauffälligen als geprüft markieren]                              [Fertig]     │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

- Aufgelistet werden Tippläufe, Feldinhalte, – falls aufgezeichnet – Inhalte der Zwischenablage und die **Texte des Autors**, gruppiert nach Fenster, jeweils mit Zeit, Fenster und Schritt.
- **Texte des Autors** sind Freitext und enthalten oft genau das, was nicht weitergegeben werden soll – Kundennamen, Ticketnummern, Fehlertexte. Durchzusehen sind ([10](10-datenschutz-und-sicherheit.md#111-durchsicht-aller-eingaben)):

| Text des Autors | Herkunft | Eintrag in der Liste |
|---|---|---|
| Kommentare | Aufnahme und Viewer ([Abschnitt 8](#8-kommentieren)) | je Kommentar |
| Ziel-Angaben: Ziel, erwartetes Ergebnis, Bezug | Startfenster oder Viewer ([4.4](#44-ziel-der-aufnahme-und-marker-typen)) | je Feld einzeln, in der wirksamen Fassung |
| Marker-Texte: die Bezeichnungen der Marker | Aufnahme ([07](07-aufnahme-bedienung.md#92-marker)) | je Marker, mit seinem Typ |
| Hinweis für den Empfänger eines Support-Pakets | Profil, Kommandozeile oder Viewer ([12.2](#122-support-paket-geführter-ablauf)) | ein Eintrag |

- Ändert der Autor einen dieser Texte nach der Durchsicht, gilt er wieder als nicht durchgesehen.
- Eingaben und Texte eines verworfenen Abschnitts stehen nicht in der Liste; er gehört zu keiner Ausgabe ([9.3](#93-abschnitt-neu-aufnehmen-und-ersetzen)).
- **Auffällige Eingaben** stehen oben und sind hervorgehoben: Eingaben aus Klartext-Fenstern und Eingaben mit einem Treffer der Musterprüfung. Ein Text des Autors ist auffällig, wenn die Musterprüfung darin trifft. In der Schutzstufe „Streng“ gibt es keine Klartext-Fenster; auffällig sind dort nur Mustertreffer.
- Jede auffällige Eingabe verlangt eine **Einzelentscheidung**: **In Ordnung**, **Schwärzen** oder **Überall schwärzen**. Ein Klick auf den Eintrag springt zum Schritt. Jeder Eintrag bietet daneben „Verallgemeinern …“ an ([9.4](#94-werte-verallgemeinern)); eine Verallgemeinerung zählt nicht als Durchsicht.
- „Alle unauffälligen als geprüft markieren“ erledigt den Rest mit einem Klick.
- Der Stand wird **je Eingabe** in `edits.json` gespeichert ([04](04-datenformat.md#edits-json)) und bleibt über das Schließen hinaus erhalten. Kommen Eingaben hinzu, etwa durch eine angehängte Aufnahme, sind nur die neuen ungeprüft.
- **Sperre vor der Weitergabe:** Solange eine auffällige Eingabe ungeprüft ist, die in der Ausgabe erscheinen würde, lassen sich weder Anleitung noch KI-Paket noch bereinigte Kopie erzeugen; der Exportdialog führt in die Durchsicht ([09](09-anleitungs-export.md#12-export-vorprüfung)).
- Wege ohne Oberfläche – MCP-Server und Export per Kommandozeile – erhalten ungeprüfte auffällige Eingaben nur als Zähler `{unreviewed:N}`, bis der Autor sie hier geprüft hat. N zählt bei getippten Läufen die Tastenanschläge, bei Feldinhalten, Zwischenablage-Inhalten, Kommentaren, Ziel-Angaben und Marker-Texten die Zeichen. Die Durchsicht ist damit auch Voraussetzung dafür, dass ein Agent diese Inhalte lesen kann.
- Die Statusleiste zeigt „Eingaben: 3 ungeprüft“; ein Klick öffnet die Durchsicht, ebenso das Menü der Sitzung und `Strg+Umschalt+R`.
- Maskierte Eingaben erscheinen als Zeile ohne Inhalt („maskiert, 8 Tastenanschläge“) und brauchen keine Entscheidung.
- **Terminal, Remote-Desktop und virtuelle Maschinen:** Was in einem Terminalfenster, einem Fernzugriffsprogramm oder einer virtuellen Maschine getippt wird, ist **prüfpflichtig** (Grund „Klartext-Fenster“). Die Aufnahme kann dort nicht erkennen, ob ein Kennwort eingegeben wird; Kennwortabfragen im Terminal werden **nicht** automatisch maskiert. Die Durchsicht ist deshalb der Schutz. In der Schutzstufe „Streng“ wird Getipptes in diesen Fenstern vollständig maskiert.
- **Lesbare Gründe:** Jeder auffällige Eintrag nennt den Grund in Worten („Grund: …“), etwa „Klartext-Fenster“ oder die Art des Mustertreffers („Schlüssel“, „IBAN“ …).
- **Sammelentscheidung für Terminalbefehle:** Sind mehrere Klartext-Eingaben offen, steht oben die Schaltfläche „Übrige Klartext-Eingaben (N): in Ordnung“. Sie setzt alle noch offenen Eingaben aus Terminal-, Remote-Desktop- und VM-Fenstern auf „In Ordnung“. **Mustertreffer** (Schlüssel, IBAN …) bleiben davon ausgenommen und brauchen weiter eine Einzelentscheidung.

## 14 Geschwindigkeitsziele

**Referenzsysteme** sind die Systeme A, B und C aus [03](03-aufzeichnungskern.md#leistungsbudgets) – dieselben wie für die Aufnahme:

| System | Kurzbeschreibung |
|---|---|
| A | Büro-PC: 4 Kerne, integrierte Grafik, 8 GB Arbeitsspeicher, SATA-SSD, ein Monitor 1920 × 1080 bei 100 % |
| B | Entwickler-PC: 8 Kerne, eigene Grafikkarte, 32 GB Arbeitsspeicher, NVMe-SSD, zwei Monitore 3840 × 2160 bei 150 % |
| C | Notebook mit Hybrid-Grafik, auch im Akkubetrieb |

**Sitzungsgrößen:**

| Kürzel | Bedeutung |
|---|---|
| Sitzung S | 50 Schritte, etwa 150 Bilder |
| Sitzung M | 1 000 Schritte, etwa 3 000 Bilder |
| Sitzung L | 5 000 Schritte, etwa 15 000 Bilder |

Die Ziele gelten für 95 % der Messungen mit **Sitzung M** und damit auch mit der kleineren Sitzung S; die Bilder der Sitzung haben jeweils die Auflösung des Systems. Für System C gelten die Werte der Spalte B, auch im Akkubetrieb.

| Vorgang | System A | System B |
|---|---|---|
| Nach dem Stopp: Fenster mit Schrittliste und erstem Bild sichtbar | ≤ 1,0 s | ≤ 1,0 s |
| Sitzung von der Platte öffnen | ≤ 1,0 s | ≤ 1,0 s |
| Sitzung zum ersten Mal öffnen, ohne Vorschau-Cache | ≤ 2,0 s | ≤ 2,0 s |
| Wechsel zum Nachbarschritt (Bild vorab geladen) | ≤ 50 ms | ≤ 100 ms |
| Sprung zu einem beliebigen Schritt (Bild nicht im Zwischenspeicher) | ≤ 150 ms | ≤ 300 ms |
| Vorschaubild in der Schrittliste nach dem Sichtbarwerden | ≤ 200 ms | ≤ 200 ms |
| Darstellung umschalten, Vorher/Nachher umschalten | ≤ 50 ms | ≤ 100 ms |
| Ansicht „Änderung“ | ≤ 200 ms | ≤ 500 ms |
| Suche | ≤ 100 ms | ≤ 100 ms |
| Bild kopieren (`Strg+C`), bis die Zwischenablage gefüllt ist | ≤ 150 ms | ≤ 300 ms |
| Bild mit Text kopieren | ≤ 300 ms | ≤ 600 ms |
| Mini-Anleitung mit 20 Schritten kopieren | ≤ 2,0 s | ≤ 4,0 s |
| Bearbeitung gespeichert, nach der Änderung | ≤ 0,5 s | ≤ 0,5 s |
| Wiedergabe: Abweichung eines Bildwechsels vom Soll | ≤ 50 ms | ≤ 50 ms |
| Sitzungsbibliothek mit 500 Sitzungen auflisten | ≤ 1,0 s | ≤ 1,0 s |
| Arbeitsspeicher des Viewers | ≤ 500 MB | ≤ 900 MB |

Unabhängig vom System gilt:

- Blättern in der Schrittliste, Zeichnen und Verschieben von Markierungen sowie die Mausbewegung in der Wiedergabe laufen mit mindestens 60 Bildern je Sekunde.
- Der Bild-Zwischenspeicher ist begrenzt (Standard 300 MB).
- Für **Sitzung L** gelten: erste Anzeige nach dem Stopp ≤ 2,0 s, Öffnen von der Platte ≤ 4,0 s, Suche ≤ 300 ms – jeweils auf allen drei Systemen. Alle übrigen Werte gelten unverändert.

Der Abnahmetest T-11 in [11](11-roadmap-tests-risiken.md) misst die Zeitziele dieses Abschnitts mit den Sitzungen S, M und L auf den Referenzsystemen, dazu den Aufnahmestart nach [07](07-aufnahme-bedienung.md#6-ablauf-und-zustände-einer-aufnahme).

Die Oberfläche reagiert auch während des Ladens: Kein Vorgang hält sie länger als 50 Millisekunden an; längere Vorgänge laufen im Hintergrund und sind abbrechbar.

Was diese Ziele voraussetzen:

- Die Schritterkennung läuft schon während der Aufnahme mit; nach dem Stopp liegen die Schritte einer Sitzung mit 1 000 Schritten binnen 300 ms vor ([05](05-schritterkennung.md#mitlaufend)).
- Vorschaubilder liegen im Ordner `cache/` der Sitzung und entstehen im Hintergrund ([04](04-datenformat.md)).
- Die Schrittliste lädt nur, was sichtbar ist; die Bilder der Nachbarschritte werden vorab geladen.
- Aus Differenzbildern zusammengesetzte Bilder werden zwischengespeichert.

## 15 Tastenkürzel

Die Kürzel gelten im Viewer-Fenster. Buchstaben ohne Zusatztaste wirken nicht, solange in einem Textfeld getippt wird. Die Kürzel des Editors sind nicht änderbar (Soll); die systemweiten Kürzel der Aufnahme lassen sich in den Einstellungen ändern, mit Prüfung auf Doppelbelegung.

**Befehlstabelle und Kürzelübersicht.** Der Editor führt seine Befehle in **einer Tabelle**: Befehl, Gruppe, Beschriftung, Kürzel, Bedingung. Aus ihr kommen die Tastenbehandlung, die Menüleiste, die Rechtsklick-Menüs, die Befehlsknöpfe und die Übersicht – ein Befehl heißt deshalb überall gleich, und ein Kürzel steht nie an zwei Stellen verschieden. Die Tabelle wird von oben nach unten abgearbeitet; die erste passende Zeile gilt, Zeilen mit Bedingung (etwa „Zeichnung gewählt“) stehen vor allgemeineren. Beim Tippen in einem Textfeld wirken nur die Befehle, die dafür vorgesehen sind (Funktionstasten wie `F1`, `F5`, `F8`, `F4`).

`F1` (auch Hilfe › Tastenkürzel) öffnet das Fenster **„Tastenkürzel“**. Es zeigt alle Befehle des Editors in den Gruppen „Schritte“, „Bild und Markierungen“, „Kopieren und Exportieren“ und „Allgemein“ und dazu **„Während der Aufnahme (systemweit)“** mit den aktuell belegten Kürzeln aus den Einstellungen („nicht belegt“, wo keines gesetzt ist). Das Suchfeld oben findet Befehl oder Kürzel; es gelten die Suchregeln aus [5.6](#56-suche-und-filter). Nicht aufgeführt sind Zweitbelegungen, etwa `←` und `→` für den nächsten und vorigen Schritt.

**Navigation**

| Kürzel | Funktion |
|---|---|
| `↑` `↓` oder `←` `→` | Voriger, nächster Schritt |
| `Bild↑` `Bild↓` | Eine Seite der Schrittliste zurück, vor |
| `Pos1` `Ende` | Erster, letzter Schritt |
| `Strg+G` | Zu Schrittnummer springen |
| `Strg+F`, `F3`, `Umschalt+F3` | Suchen, nächster, voriger Treffer |
| `Strg+→`, `Strg+←` | Nächster, voriger Marker |
| `F4`, `Umschalt+F4` | Nächste, vorige Fundstelle der Qualitätsprüfung (im Vergleich: nächster, voriger Unterschied, Soll) |
| `F6` | Zwischen Schrittliste, Bild, Detailbereich und Zeitleiste wechseln |

**Ansicht**

| Kürzel | Funktion |
|---|---|
| `Strg+0`, `Strg+1` | Einpassen, 100 % |
| `Strg++`, `Strg+-`, `Strg+Mausrad` | Vergrößern, verkleinern |
| `Leertaste+Ziehen`, mittlere Maustaste | Bild verschieben |
| `1` `2` `3` | Darstellung: ganzer Bildschirm, Anwendungsfenster, Ausschnitt |
| `W` | Zwischen Vorher- und Nachher-Bild wechseln |
| `D` | Ansicht „Änderung“ |
| `G` | Galerie ein und aus |
| `I` `E` | Klickmarke, Elementrahmen ein und aus |
| `Strg+B` `Strg+I` `Strg+T` | Schrittliste, Detailbereich, Zeitleiste ein und aus |
| `F7` | Vergleichen mit … |
| `Strg+F2` | Seitenansicht ein/aus (Mitte „Bild \| Seite“) |
| `Strg+P` | Drucken … |
| `F8` | Fenster der Qualitätsprüfung öffnen |
| `F11` | Vollbild |

**Abspielen**

| Kürzel | Funktion |
|---|---|
| `F5` | Wiedergabe ab dem gewählten Schritt |
| `Leertaste` | Anhalten, fortsetzen (während der Wiedergabe) |
| `←` `→` | Voriger, nächster Schritt |
| `+` `-` | Schneller, langsamer |
| `Esc` | Wiedergabe beenden |

**Werkzeuge**

| Kürzel | Funktion |
|---|---|
| `V` `A` `Umschalt+A` `F` | Auswahl, Pfeil, Linie, Freihand |
| `R` `E` `M` `U` `L` | Rechteck, Ellipse, Markierstift, Spotlight, Lupe |
| `T` `N` | Text, Nummer |
| `X` `P` `C` | Schwärzen, Verpixeln, Zuschnitt |
| Kommentar-Pin | noch ohne Kürzel (Soll) |
| `Umschalt` beim Zeichnen | Quadrat, Kreis, Winkel in 45-Grad-Schritten |
| `Strg` beim Zeichnen | Einrasten am Element aussetzen |
| `Entf` (Objekt gewählt) | Objekt löschen |
| `Strg+D` | Objekt verdoppeln |
| Pfeiltasten (Objekt gewählt) | Objekt um 1 Pixel verschieben, mit `Umschalt` um 10 |
| `Esc` | Werkzeug abwählen, Auswahl aufheben |

**Bearbeiten**

| Kürzel | Funktion |
|---|---|
| `Strg+Z`, `Strg+Y` | Rückgängig, wiederherstellen |
| `F2` | Schritttext ändern |
| `Strg+Eingabe`, `Esc` | Textänderung abschließen, verwerfen |
| `K` | Kommentar zum Schritt |
| `Einfg` | Hinweisschritt einfügen |
| `Entf` (Schrittliste), `Strg+H` | Schritt ausblenden, einblenden |
| `Umschalt+Entf` | Inhalt des Schritts schwärzen |
| `Strg+J`, `Strg+Umschalt+J` | Zusammenfassen, Zusammenfassung aufheben |
| `Alt+↑`, `Alt+↓` | Schritt nach oben, nach unten |
| `Strg+↑`, `Strg+↓` | Wichtigkeit erhöhen, verringern |
| `Strg+Umschalt+K` | Kapitel einfügen |
| `Strg+R` | Gewählte Schritte neu aufnehmen und ersetzen |
| `Strg+Umschalt+G` | Werte verallgemeinern |
| `Strg+Umschalt+L` | Lesezeichen setzen, entfernen |
| `Strg+Umschalt+B` | Textbaustein einfügen (wirkt auch beim Tippen in einem Textfeld) |
| `Strg+A` | Alle Schritte wählen |

**Kopieren und Ausgeben**

| Kürzel | Funktion |
|---|---|
| `Strg+C` | Bild kopieren, wie angezeigt |
| `Strg+Umschalt+C` | Bild mit Text kopieren; bei Mehrfachauswahl Mini-Anleitung |
| `Strg+Umschalt+T` | Nur Text kopieren |
| `Strg+M` | Als Markdown kopieren |
| `Strg+Umschalt+M` | Alle Schritte als Markdown kopieren |
| `Strg+Umschalt+O` | Originalbild kopieren |
| `Strg+Umschalt+D` | Als Datei kopieren |
| `Alt+Ziehen` im Bild | Ausschnitt kopieren |
| `Strg+Umschalt+S` | Bild speichern unter |
| `Strg+E` | Export-Dialog |
| `Strg+Umschalt+E` | Letzten Export wiederholen (Ein-Klick-Export) |

**Sitzung und Programm**

| Kürzel | Funktion |
|---|---|
| `Strg+O` | Sitzung öffnen |
| `Strg+N` | Neue Aufnahme (Startfenster) |
| `Strg+Umschalt+N` | Aufnahme an diese Sitzung anhängen |
| `Strg+Umschalt+R` | Eingaben prüfen |
| `Strg+L` | „Alle Aufnahmen“ (Bibliothek) öffnen |
| `Strg+Umschalt+F` | In der Bibliothek: in allen Sitzungen suchen |
| `Strg+W` | Fenster schließen |
| `Strg+,` | Einstellungen |
| `F1` | Hilfe und Kürzelübersicht |

Während einer Aufnahme gelten die systemweiten Kürzel aus [07](07-aufnahme-bedienung.md#8-tastenkürzel).

## 16 Zustände und Fehlerfälle

| Situation | Verhalten |
|---|---|
| Aufnahme wurde nicht ordnungsgemäß beendet | Die Sitzung öffnet sich mit dem Hinweis „lesbar bis 00:04:12“; alle Funktionen stehen zur Verfügung. |
| Ein Bild fehlt oder ist beschädigt | Platzhalter „Bild nicht verfügbar“ mit Bildkennung; der Schritt bleibt nutzbar, andere Schritte sind nicht betroffen. |
| Neuere Formatversion | Nur lesen, mit Hinweis auf die benötigte Programmversion. |
| Ältere Formatversion | Wird beim Öffnen angepasst; Regeln in [04](04-datenformat.md). |
| `edits.json` nicht lesbar | Der Viewer bietet den letzten lesbaren Stand an; die Rohdaten sind nicht betroffen. |
| Eine Bearbeitung lässt sich nach einer Neuberechnung keinem Schritt zuordnen | Sie erscheint unter „Nicht zugeordnet“ in der Sitzungsübersicht und lässt sich dort neu zuweisen oder löschen. Sie geht nie stillschweigend verloren ([04](04-datenformat.md#edits-json)). |
| Kein Schreibzugriff oder Datenträger voll | Hinweisbalken; Bearbeitungen bleiben im Arbeitsspeicher und werden gespeichert, sobald es wieder geht. Beim Schließen warnt der Viewer. |
| Bearbeitungen wurden von außen geändert, etwa über den MCP-Server | Der Viewer erkennt die Änderung und lädt sie nach; hat der Autor zugleich Ungespeichertes, fragt er nach. |
| Sitzung wird gerade aufgenommen | Lässt sich erst nach dem Stopp öffnen. |
| Sehr große Sitzung | Schrittliste und Bilder erscheinen fortlaufend; die Bedienung ist sofort möglich. |
| Zwischenablage gesperrt | Erneuter Versuch für eine halbe Sekunde, dann Meldung. |
| Suchindex fehlt, ist veraltet oder hat eine andere Formatversion | Er wird im Hintergrund neu gebaut; bis dahin zählt die Sitzung in der Suche über alle Sitzungen als „noch nicht durchsucht“. |
| Die Aufnahme für eine Ersetzung oder eine angehängte Aufnahme wird verworfen | Verworfen ist nur der neue Abschnitt: Er ist gekennzeichnet und erscheint in keiner Ausgabe; das Journal wird nicht gekürzt. Die Sitzung bleibt, wie sie war, und es entsteht keine Ersetzung. In der Sitzungsübersicht lässt sich der Abschnitt wieder aufnehmen. |
| Die Aufnahme für eine Ersetzung endet durch einen Absturz | Nach der Wiederherstellung ist sie eine nicht verwendete Neuaufnahme; die Ersetzung lässt sich im Viewer einsetzen. |
| Zweite Sitzung eines Vergleichs ist nicht abgeschlossen oder nicht lesbar | Der Vergleich beginnt nicht; der Viewer nennt den Grund. |
| Das Erstellen des Support-Pakets schlägt fehl | Es entsteht kein Paket und keine unvollständige Datei; der Ablauf bleibt in Schritt 3 und nennt den Grund. |

## 17 Anforderungen

| ID | Anforderung | Priorität | Abnahmekriterium |
|---|---|---|---|
| VIE-001 | Viewer öffnet sich nach dem Stopp | Muss | Nach jedem Stopp erscheint ohne weitere Aktion der Viewer mit der neuen Sitzung, erster Schritt gewählt. |
| VIE-002 | Erste Anzeige unter einer Sekunde | Muss | Nach dem Stopp sind Schrittliste und erstes Bild mit Sitzung M auf System A und auf System B in höchstens 1,0 s sichtbar (95 % der Messungen). |
| VIE-003 | Öffnen über Zuletzt, Drag & Drop, Kommandozeile, Dialog | Muss | Jeder der vier Wege öffnet einen Sitzungsordner und eine `.steprec`-Datei. |
| VIE-004 | Dateizuordnung für `.steprec` | entfallen | Gestrichen: Das Programm ändert die Registry ohne Ausnahme nicht (Entscheidung E19 in [01](01-anforderungen.md)). Geöffnet wird nach VIE-003. |
| VIE-005 | Gepackte Sitzungen | Soll | Eine `.steprec`-Datei öffnet schreibgeschützt mit der Leiste „Entpacken …“; nach dem Entpacken ist eine Bearbeitung nach erneutem Öffnen vorhanden. Bearbeiten direkt im Archiv ist nicht vorgesehen. |
| VIE-006 | Nur-Lesen-Betrieb | Muss | Sitzungen auf schreibgeschütztem Datenträger und aus neuerer Formatversion öffnen sich mit Hinweis; Ansehen, Abspielen, Kopieren funktionieren. |
| VIE-007 | Schließen ohne Rückfrage | Muss | Nach beliebiger Bearbeitung schließt das Fenster ohne Dialog; die Bearbeitung ist beim nächsten Öffnen vorhanden. |
| VIE-010 | Fensteraufbau | Muss | Kopfleiste, Schrittliste, Bildbereich, Detailbereich, Zeitleiste und Statusleiste sind vorhanden; die drei Seitenbereiche sind ein- und ausblendbar. |
| VIE-011 | Schrittliste mit Kennzeichen | Muss | Jede Zeile zeigt Vorschaubild, Nummer, Schrittart, Kurztext und die Kennzeichen aus 4.2. |
| VIE-012 | Haupt- und alle Schritte | Muss | Der Umschalter blendet Nebenschritte und Spurdaten ein und aus; die Nummerierung der Hauptschritte bleibt gleich. |
| VIE-013 | Sitzungsübersicht | Muss | Die Übersicht zeigt Vollständigkeitsbericht, Lückenliste und Datenschutzangaben; ein Klick auf eine Lücke springt zur Stelle. |
| VIE-014 | Aufteilung wird gemerkt | Soll | Fenstergröße, Trennlinien und Zoom sind nach Neustart erhalten. |
| VIE-020 | Zoom und Verschieben | Muss | Einpassen, 100 % und stufenloser Zoom; bei 100 % ist das Bild pixelgenau. |
| VIE-021 | Standardbild je Schrittart | Muss | Jeder Schritt zeigt zuerst das Bild nach der Tabelle in 5.1. |
| VIE-022 | Darstellung je Sitzung und je Schritt | Muss | Die vier Darstellungen sind wählbar; die Wahl je Schritt überschreibt die der Sitzung; an den gespeicherten Bildern ändert sich nichts (Prüfsummen gleich). |
| VIE-023 | Standard-Ausschnitt nach Regel | Muss | Für 20 Testschritte ergibt die Regel aus 5.2 die erwarteten Rechtecke; das Ziel liegt immer im Ausschnitt. |
| VIE-024 | Vorher und Nachher | Muss | `W` wechselt zwischen beiden Bildern; fehlt ein Nachher-Bild, ist der Umschalter ausgegraut. |
| VIE-025 | Ansicht „Änderung“ | Soll | Geänderte Bereiche zweier Testbilder sind umrandet; die erkannte Wirkung steht in Worten darunter. |
| VIE-026 | Galerie | Soll | Raster aller Schrittbilder mit Mehrfachauswahl; Doppelklick wechselt zum Schritt. |
| VIE-027 | Zeitleiste | Soll | Schritte, Lücken, Marker und Pausen sind an ihrer zeitlichen Stelle dargestellt; ein Klick springt dorthin. |
| VIE-028 | Suche | Muss | Die Suche findet Text in Schritttexten, Kapiteln, Elementnamen, getipptem Text, Fenstertiteln, Kommentaren und Text-Schnappschüssen; mehrere Wörter müssen alle vorkommen, Anführungszeichen suchen die Wortfolge; maskierte und geschwärzte Inhalte ergeben keine Treffer. |
| VIE-029 | Filter | Soll | Jeder Filter aus 5.6 schränkt die Schrittliste entsprechend ein; Filter sind kombinierbar. |
| VIE-030 | Lücken und Belegstufen sichtbar | Muss | Jede Lücke der Sitzung steht mit Ursachentext und Dauer in Zeitleiste und Lückenliste, Lücken mit eigenem Schritt auch in der Schrittliste; jeder Schritt mit Belegstufe ungleich „belegt“ trägt ein Kennzeichen mit Begründung. |
| VIE-031 | Technische Details | Soll | Rohereignisse, Elementeigenschaften, Bilder und Zeiten des Schritts sind einsehbar und als JSON kopierbar, mit Schwärzungen. |
| VIE-032 | Automatische Markierungen schaltbar | Muss | Klickmarke, Elementrahmen und Mauszeiger lassen sich je Sitzung und je Schritt ein- und ausblenden; die Klickmarke ist nicht verschiebbar. |
| VIE-040 | Wiedergabe als Bildfolge | Muss | `F5` spielt die Schritte in zeitlicher Folge mit Vorher- und Nachher-Bildern ab; Anhalten, Fortsetzen, Beenden funktionieren. |
| VIE-041 | Animierter Mausweg | Muss | Der Zeiger folgt dem aufgezeichneten Pfad; an einer Testsitzung mit bekanntem Pfad weicht die Anzeige höchstens 2 Pixel ab. |
| VIE-042 | Eingeblendete Tasten | Soll | Tastenkombinationen und getippter Text erscheinen in der Einblendung; maskierte Eingaben als Punkte. |
| VIE-043 | Geschwindigkeit und Leerlauf | Soll | 0,5- bis 4-fach wählbar; mit „Leerlauf überspringen“ dauert keine Pause länger als 1 s. |
| VIE-044 | Wiedergabe ändert nichts | Muss | Nach einer Wiedergabe sind alle Dateien der Sitzung unverändert. |
| VIE-045 | Wiedergabe als GIF oder Video | Kann | Eine Wiedergabe lässt sich als Datei ausgeben. |
| VIE-050 | Werkzeuge der Priorität Muss | Muss | Auswahl, Pfeil, Rechteck, Ellipse, Text, Nummer, Markierstift, Zuschnitt, Schwärzen und Verpixeln sind vorhanden und per Taste wählbar. |
| VIE-051 | Lupe und Kommentar-Pin | Soll | Die Lupe ist vorhanden und erscheint in Kopie und Export wie angezeigt; der Kommentar-Pin ist noch nicht umgesetzt. |
| VIE-052 | Objekte bearbeiten | Muss | Gezeichnete Objekte lassen sich wählen, verschieben, in der Größe ändern, verdoppeln und löschen. |
| VIE-053 | Stilvorlagen | Soll | Drei mitgelieferte Vorlagen; eigene Vorlagen speicherbar; „auf alle Schritte anwenden“ stellt bestehende Markierungen um. |
| VIE-054 | Am Element einrasten | Soll | Mit einem Klick liegt ein Rechteck oder eine Schwärzung deckungsgleich (±1 Pixel) auf dem Elementrechteck. |
| VIE-055 | Zerstörungsfreie Bearbeitung | Muss | Nach beliebiger Bearbeitung sind `events.jsonl` und alle Dateien in `frames/` unverändert (Prüfsummen); „Schritt zurücksetzen“ stellt den Ausgangszustand her. |
| VIE-056 | Rückgängig und Wiederherstellen | Muss | Jede Bearbeitungsart aus Abschnitt 7 bis 9 lässt sich mit `Strg+Z` zurücknehmen und mit `Strg+Y` wiederherstellen. |
| VIE-057 | Markierungen bleiben am Inhalt | Muss | Nach dem Wechsel der Darstellung liegt eine Markierung auf denselben Bildpixeln wie zuvor. |
| VIE-058 | Anderes Bild wählen | Soll | Der Bildstreifen bietet Vorher-, Nachher- und Zwischenbilder; die Wahl gilt in Anzeige, Kopie und Export. |
| VIE-059 | Extern bearbeiten | Soll | Das übergebene Bild enthält die Schwärzungen und liegt in `export/.tmp/` der Sitzung; nach dem Speichern im Fremdprogramm bietet der Viewer die Übernahme an; „Bildersatz entfernen“ stellt das Original her. |
| VIE-060 | Schwärzungen überall erzwungen | Muss | Ein geschwärzter Bereich ist in Anzeige, Wiedergabe, jeder Kopie nach 10.2, jedem Export, KI-Paket und MCP-Antwort unkenntlich; geprüft mit einem Testmuster unter der Schwärzung. |
| VIE-061 | Ausdehnung der Schwärzung | Muss | Eine Schwärzung gilt von selbst für alle aufeinanderfolgenden Bilder mit unverändertem Bereich; „dem Fenster folgen“ und „ganze Sitzung an dieser Stelle“ sind wählbar; die Zahl betroffener Bilder wird angezeigt und stimmt mit der Prüfung überein. |
| VIE-062 | Text schwärzen | Muss | Ein geschwärzter Text erscheint in keiner Ausgabe und in keiner Suche; an seiner Stelle steht `[geschwärzt]` bzw. `[redacted]`. „Überall in der Sitzung“ erfasst alle gleichlautenden Vorkommen. |
| VIE-063 | Ganzen Schritt schwärzen | Muss | Texte und Bilder des Schritts erscheinen in keiner Ausgabe; an seiner Stelle steht „Schritt geschwärzt“ bzw. „Step redacted“. Ist er zugleich ausgeblendet, fehlt er in der Anleitung. |
| VIE-064 | Vorschläge zum Schwärzen | Soll | Fundstellen der Muster aus [10](10-datenschutz-und-sicherheit.md#112-musterprüfung) werden gelistet; nichts wird ohne Zutun geschwärzt. |
| VIE-065 | Verpixeln und Unschärfe als reine Optik | Muss | Die Ausgabe im geschwärzten Bereich hängt nur von dessen Durchschnittsfarbe und einem Zufallsmuster ab: Zwei Testbilder mit unterschiedlichem Text gleicher Durchschnittsfarbe sind dort nicht unterscheidbar. |
| VIE-066 | Endgültig schwärzen | Soll | Beide Wege – bereinigte Kopie und an Ort und Stelle – sind nach Rückfrage ausführbar; danach ist der Testinhalt im Ergebnisordner nicht auffindbar. |
| VIE-067 | Aufgedeckte Schwärzung sperrt die Weitergabe | Muss | Solange eine Schwärzung zum Bearbeiten aufgedeckt ist, sind alle Kopier- und Exportbefehle gesperrt. |
| VIE-068 | Ältere Ausgaben nach einer Schwärzung | Soll | Nach einer neuen Schwärzung listet der Viewer die Ausgaben im Ordner `export/` der Sitzung und bietet das Löschen an. |
| VIE-069 | Zeitraum schwärzen | Muss | Ein in der Zeitleiste gewählter Zeitraum wird in keiner Ausgabe mehr ausgegeben – weder Eingaben noch Texte noch Bilder; an seiner Stelle steht in Anleitung, KI-Paket und MCP-Antwort der neutrale Hinweis „vom Autor entfernt“ mit Zeitpunkt und Dauer. |
| VIE-070 | Kommentar zum Schritt | Muss | `K` legt einen Kommentar an; er erscheint in Anleitung und KI-Paket. |
| VIE-071 | Hinweis- und Warnschritt | Muss | `Einfg` fügt einen Textschritt ein, der in der Anleitung an dieser Stelle steht. |
| VIE-072 | Kennzeichnungen einschließlich „Intern“ | Soll | Die vier Kennzeichnungen sind wählbar; ein interner und ein ausgeblendeter Kommentar erscheinen in keiner Ausgabe – weder in Anleitung noch KI-Paket noch MCP-Antwort. |
| VIE-073 | Kommentare aus der Aufnahme | Muss | Bei der Aufnahme geschriebene Kommentare erscheinen als Schritte mit Bild und markiertem Bereich und sind änderbar. |
| VIE-074 | Marker | Muss | Marker der Aufnahme erscheinen als Fähnchen in Schrittliste und Zeitleiste; ein Klick legt dort einen Kommentar an. |
| VIE-080 | Schritttext ändern und wiederherstellen | Muss | `F2` ändert den Text; „Automatischen Text wiederherstellen“ nimmt die Änderung zurück. |
| VIE-081 | Schritt ausblenden | Muss | Ein ausgeblendeter Schritt fehlt in Export und Mini-Anleitung und bleibt im Viewer grau sichtbar. |
| VIE-082 | Verwerfen zurücknehmen | Muss | Ein bei der Aufnahme verworfener Schritt ist gekennzeichnet und lässt sich wieder aufnehmen. |
| VIE-083 | Zusammenfassen und Teilen | Soll | Zusammenfassen funktioniert wie in 9.2 beschrieben und ist auflösbar; Teilen ist noch nicht umgesetzt. |
| VIE-084 | Umsortieren | Soll | Die neue Reihenfolge gilt in Export und Mini-Anleitung; „Zeitliche Reihenfolge wiederherstellen“ nimmt sie zurück. |
| VIE-085 | Kapitel | Soll | Kapitelüberschriften lassen sich einfügen, umbenennen, einklappen und erscheinen im Export. |
| VIE-086 | Mehrfachauswahl | Soll | Ausblenden, Darstellung, Stil, Zusammenfassen und Kopieren wirken auf alle gewählten Schritte. |
| VIE-087 | Texte je Sprache und Anrede | Soll | Nach dem Umschalten der Sprache wechseln automatische Texte; eigene Texte bleiben ihrer Sprache zugeordnet. Für deutsche Texte ist die Anrede Sie, du oder neutral wählbar. |
| VIE-088 | Bearbeitungen überstehen die Neuberechnung | Muss | Nach einer Neuberechnung der Schritte mit geänderten Regeln hängt jede Bearbeitung noch am selben Vorgang; nicht zuordenbare erscheinen unter „Nicht zugeordnet“ und gehen nicht verloren. |
| VIE-089 | Texte von KI-Agenten gekennzeichnet | Soll | Ein über den MCP-Server geschriebener Schritttext trägt das Kennzeichen „von KI-Agent geschrieben“ mit Programmname und Zeit; er lässt sich einzeln und gesammelt verwerfen, danach gilt der automatische Text. |
| VIE-090 | `Strg+C` kopiert das angezeigte Bild | Muss | Das eingefügte Bild entspricht pixelgenau dem angezeigten in voller Auflösung, mit Darstellung, Markierungen und Schwärzungen; Formate Bitmap und PNG liegen vor. |
| VIE-091 | Bild mit Text kopieren | Muss | Die Zwischenablage enthält HTML, RTF und Text; Einfügen in Word, Outlook und Teams ergibt Text und Bild. |
| VIE-092 | Nur Text kopieren | Muss | Die Zwischenablage enthält den Schritttext mit Kommentaren als reinen Text. |
| VIE-093 | Als Markdown kopieren | Soll | Der eingefügte Text ist gültiges Markdown; aufgezeichnete Texte sind entschärft. |
| VIE-094 | Originalbild kopieren | Soll | Das eingefügte Bild ist das ganze gespeicherte Bild ohne Markierungen, mit Schwärzungen. |
| VIE-095 | Ausschnitt mit `Alt+Ziehen` | Muss | Nach dem Loslassen liegt genau der aufgezogene Bereich in der Zwischenablage; am Schritt ändert sich nichts. |
| VIE-096 | Mini-Anleitung | Soll | Die Kopie mehrerer Schritte ergibt in Word eine nummerierte Liste mit Text und Bildern in der Reihenfolge der Anleitung. |
| VIE-097 | Als Datei kopieren | Soll | Einfügen im Explorer legt eine Bilddatei mit sprechendem Namen an; Einfügen in eine Outlook-Nachricht ergibt einen Anhang. Die Zwischendatei liegt in `export/.tmp/` der Sitzung; nach Kopieren, Herausziehen und Schließen findet sich in `%TEMP%` keine Datei des Programms mit Sitzungsinhalt. |
| VIE-098 | Herausziehen | Soll | Ein Vorschaubild lässt sich in Explorer, Word und ein Upload-Feld im Browser ziehen; innerhalb der Liste sortiert dieselbe Bewegung um. |
| VIE-099 | Speichern und alle Bilder exportieren | Muss | „Bild speichern unter“ schreibt das angezeigte Bild; „Alle Bilder exportieren“ schreibt nummerierte Dateien nach dem Namensmuster. |
| VIE-100 | Kopier-Symbole an Einzelangaben | Soll | Getippter Text, Elementname, Fenstertitel, Pfad und Tastenfolge sind mit einem Klick als Text kopierbar. |
| VIE-101 | Zielprogramm-Matrix | Muss | Die als Muss gekennzeichneten Zeilen der Matrix in 10.3 liefern das genannte Ergebnis. |
| VIE-102 | Bestätigung jedes Kopiervorgangs | Muss | Nach jedem Kopierbefehl erscheint für mindestens 1 s die Bestätigung mit Art und Größe; ein Fehlschlag wird gemeldet. |
| VIE-103 | Kennzeichen für Zwischenablage-Verlauf und Cloud | Soll | Mit den Einstellungen aus 10.8 erscheint die Kopie nicht im Verlauf bzw. nicht auf einem zweiten Gerät. |
| VIE-104 | Einstellungen zum Kopieren | Soll | Jede Einstellung der Tabelle in 10.8 wirkt wie beschrieben. |
| VIE-105 | Nachfrage bei ungeprüften auffälligen Eingaben | Muss | Die erste Text-Kopie einer Sitzung, die eine ungeprüfte auffällige Eingabe enthält, löst die Nachfrage aus; Bild-Kopien nicht. |
| VIE-110 | Sitzungsbibliothek | Soll | Alle Sitzungen des Aufnahmeordners sind gelistet, durchsuchbar und sortierbar. |
| VIE-111 | Automatischer Sitzungsname | Muss | Jede neue Sitzung trägt einen Titel nach dem Muster; der Autor wird nicht gefragt. |
| VIE-112 | Umbenennen, Schlagwörter, Löschen | Soll | Umbenennen ändert den Titel, nicht den Ordner; Löschen legt die Sitzung nach Rückfrage in den Papierkorb und fragt ohne Papierkorb ausdrücklich nach. Schlagwörter sind noch nicht umgesetzt. |
| VIE-113 | Aufnahme anhängen | Soll | Eine angehängte Aufnahme erscheint als weitere Schritte am Ende derselben Sitzung. |
| VIE-114 | Packen und Entpacken | Soll | Eine gepackte und wieder entpackte Sitzung ist inhaltsgleich. |
| VIE-115 | Verschieben und bereinigte Kopie | Soll | Eine verschobene Sitzung ist am Ziel vollständig und am Ursprung entfernt; eine bereinigte Kopie enthält keinen der geschwärzten Inhalte. |
| VIE-116 | Kopierbare MCP-Konfiguration | Soll | Der Dialog aus 11.1 zeigt die Konfiguration für Claude Code (Datei und Befehl) und Claude Desktop mit dem tatsächlichen Pfad der EXE und dem gewählten Ordner. In Claude Code eingefügt, startet der Server ohne Nacharbeit; das Programm selbst schreibt dabei in keine fremde Datei. |
| VIE-120 | Automatisches Speichern | Muss | Wird das Programm 1 s nach einer Bearbeitung hart beendet, ist die Bearbeitung beim nächsten Öffnen vorhanden. |
| VIE-121 | Ein-Klick-Export | Soll | (Umgesetzt als Wiederholung des letzten Exports der Aufnahme.) `Strg+Umschalt+E` erzeugt ohne Dialog die Ausgabe im zuletzt genutzten Format. |
| VIE-122 | Hell und Dunkel | Soll | Der Viewer folgt der Windows-Einstellung und ist umschaltbar. |
| VIE-123 | Kürzelübersicht | Soll | `F1` zeigt alle Befehle der Tabelle und die Aufnahme-Kürzel aus den Einstellungen mit aktueller Belegung; das Suchfeld findet nach Wortfolgen; Menüs zeigen dieselben Kürzel wie die Tabelle. |
| VIE-124 | Vollständige Tastaturbedienung | Muss | Jeder Bedienablauf dieses Dokuments ist ohne Maus ausführbar, mit Ausnahme des freien Zeichnens. |
| VIE-125 | Komfortfunktionen der Priorität Kann | Kann | Jede umgesetzte Kann-Funktion aus Abschnitt 12 wirkt wie dort beschrieben. |
| VIE-130 | Durchsicht der Eingaben | Muss | Die Durchsicht listet Tippläufe, Feldinhalte, aufgezeichnete Zwischenablage-Inhalte und die Texte des Autors – Kommentare sowie, wo vorhanden, Ziel-Angaben, Marker-Texte und den Hinweis für den Empfänger –, gruppiert nach Fenster. Ein nach der Durchsicht geänderter Text des Autors gilt wieder als nicht durchgesehen. Auffällige Eingaben stehen oben und verlangen je Eintrag eine Einzelentscheidung (In Ordnung, Schwärzen, Überall schwärzen); unauffällige lassen sich gesammelt bestätigen. |
| VIE-131 | Stand der Durchsicht und Sperre | Muss | Der Stand ist je Eingabe gespeichert und nach erneutem Öffnen vorhanden; nach einer angehängten Aufnahme sind nur die neuen Eingaben ungeprüft. Solange eine auffällige Eingabe ungeprüft ist, die in der Ausgabe erscheinen würde, lassen sich Anleitung, KI-Paket und bereinigte Kopie nicht erzeugen. |
| VIE-140 | Geschwindigkeitsziele | Muss | Jede Zeile der Tabelle in Abschnitt 14 ist mit Sitzung M auf System A mit dem Wert der Spalte A, auf System B und auf System C mit dem Wert der Spalte B erfüllt (95 % der Messungen); die Werte für Sitzung L sind auf allen drei Systemen erfüllt. |
| VIE-141 | Oberfläche bleibt bedienbar | Soll | Während des Ladens einer Sitzung L hält auf System A kein Vorgang die Oberfläche länger als 50 ms an. |
| VIE-142 | Bedienabläufe mit Höchstzahl | Muss | Jeder Ablauf V1 bis V13 und beide Kernabläufe aus 2.1 sind mit der genannten Höchstzahl an Aktionen ausführbar. |
| VIE-150 | Unvollständige und beschädigte Sitzungen | Muss | Eine durch hartes Beenden abgebrochene Aufnahme öffnet sich bis zum letzten Schreibvorgang; ein fehlendes Bild führt zu einem Platzhalter und nicht zum Abbruch. |
| VIE-151 | Änderungen von außen | Soll | Eine Änderung an `edits.json` durch ein anderes Programm erscheint im geöffneten Viewer, ohne ungespeicherte Bearbeitungen zu verlieren. |
| VIE-160 | Ziel im Sitzungskopf | Soll | Eine Sitzung mit Ziel zeigt die Zielzeile mit den vorhandenen Angaben; eine ohne Ziel zeigt keine und bietet in der Sitzungsübersicht „Ziel hinzufügen“. |
| VIE-161 | Ziel ändern, ohne die Aufnahme zu ändern | Soll | Nach einer Änderung steht der neue Text in `edits.goal`, `manifest.goal` ist unverändert; ein geleertes Feld erscheint in keiner Ausgabe; „zurücksetzen“ zeigt wieder den Stand der Aufnahme. Die Texte stehen in der Durchsicht der Eingaben und lassen sich schwärzen; Ablauf V18. |
| VIE-162 | Marker-Typen sichtbar | Soll | Schrittliste und Zeitleiste zeigen je Typ ein eigenes Zeichen und die Bezeichnung; die vier Typen sind in einer Graustufen-Darstellung unterscheidbar. |
| VIE-163 | Marker filtern, anspringen, Typ ändern | Soll | Der Filter „Marker“ schränkt auf die gewählten Typen ein; `Strg+→` und `Strg+←` springen zum nächsten und vorigen Marker. Ein im Viewer geänderter Typ steht in `edits.json`, das Rohprotokoll ist unverändert, und alle Ausgaben zeigen den neuen Typ. |
| VIE-170 | Vergleichsansicht | Soll | „Vergleichen mit …“ zeigt zwei abgeschlossene Sitzungen nebeneinander mit abgeglichenen Schritten; Ablauf V14. Beide Sitzungen sind danach unverändert, in keiner ist ein Ergebnis abgelegt. |
| VIE-171 | Kennzeichnung der Paare | Soll | Jedes Paar trägt genau eine der Kennzeichnungen gleich, geändert, nur in A, nur in B; bei „geändert“ sind die Unterschiede aus Ziel, Text, Wirkung, Dauer, Bild benannt. An den Testsitzungen aus T-14 sind alle bekannten Unterschiede richtig und keine falschen gekennzeichnet. |
| VIE-172 | Vergleich ohne Orakel | Soll | Ein in einer der Sitzungen geschwärzter oder verallgemeinerter Inhalt erscheint im Vergleich nur als Platzhalter; er wird weder als gleich noch als verschieden gemeldet, das Paar nennt die unverglichene Angabe. Gespeicherter und kopierter Vergleich zeigen ungeprüfte auffällige Eingaben als `{unreviewed:N}`. |
| VIE-173 | Gleiche Ausgabe wie Kommandozeile und MCP | Soll | Die gespeicherte Vergleichsdatei ist bytegleich mit der Ausgabe von `compare` für dieselben Sitzungen und Einstellungen (T-14). |
| VIE-174 | Vergleich schnell genug | Soll | Für zwei Sitzungen mit je 500 Schritten steht die Paarliste auf System A in höchstens 2 s; die Oberfläche bleibt währenddessen bedienbar. |
| VIE-180 | Neu aufnehmen und ersetzen | Soll | Für einen zusammenhängenden Bereich gewählter Schritte startet eine Aufnahme mit dem Aufnahmebereich, der beim ersten zu ersetzenden Schritt galt; nach dem sauberen Stopp steht die Ersetzung ohne weiteres Zutun in `edits.replacements`, und die neuen Schritte stehen in Viewer und Anleitung an der Stelle der alten; Ablauf V15. |
| VIE-181 | Ersetzte Schritte bleiben erhalten | Soll | Das Journal enthält alte und neue Fassung; `edits.replacements` nennt die Ersetzung; die ersetzten Schritte sind unter „Alle“ mit dem Kennzeichen „ersetzt“ sichtbar und fehlen in Anleitung und Wiedergabe; ihre Bearbeitungen sind unverändert vorhanden. |
| VIE-182 | Ersetzung aufheben | Soll | „Ersetzung aufheben“ lässt die früheren Schritte mit ihren Bearbeitungen wieder gelten; die Neuaufnahme ist danach als nicht verwendet gekennzeichnet und lässt sich erneut einsetzen. Wird die neue Aufnahme verworfen, entsteht keine Ersetzung: Der verworfene Abschnitt erscheint in keiner Ausgabe, das Journal ist nicht gekürzt, und die Sitzung ist sonst unverändert. |
| VIE-190 | Regeln zum Verallgemeinern | Soll | Regeln mit Wert oder Muster, Platzhalter, Groß- und Kleinschreibung, Geltung und Ziel lassen sich anlegen, ändern, umsortieren und entfernen und stehen in `edits.substitutions`; der Dialog nennt je Regel die Zahl der Fundstellen und weist ungültige Muster und Platzhalternamen zurück; Ablauf V16. |
| VIE-191 | Automatische Vorschläge | Soll | Benutzername, Rechnername, Domäne und Benutzerprofilpfad des laufenden Rechners werden mit ihrer Fundstellenzahl vorgeschlagen, sofern sie in der Sitzung vorkommen; ein Wert ohne Fundstelle erscheint nicht. Ohne Annahme durch den Autor steht keiner dieser Werte in einer Datei. Der Profilpfad steht in der Liste vor dem Benutzernamen. |
| VIE-192 | Wirkung in Text und Bild | Soll | Nach dem Anwenden zeigt der Viewer an jeder Textfundstelle den Platzhalter und im Bild an jeder Stelle mit bekannter Textposition eine gefüllte Fläche mit dem Platzhalter; Fundstellen ohne bekannte Bildposition sind im Dialog gezählt, lassen sich von Hand markieren und stehen in der Qualitätsprüfung (T-15). |
| VIE-193 | Originalwerte nur als Anzeige | Soll | Mit „Originalwerte zeigen“ erscheinen die aufgezeichneten Werte hervorgehoben; Kopie, Export und Suche liefern währenddessen weiterhin die Platzhalter, und die Kopierbestätigung sagt das. Nach dem Schließen der Sitzung ist die Ansicht aus. |
| VIE-194 | Verallgemeinerung endgültig anwenden | Soll | Nach bereinigter Kopie oder endgültigem Anwenden ist ein verallgemeinerter Wert im Ergebnisordner nicht mehr auffindbar – auch nicht in `edits.json`; vorher ist er in den Rohdaten unverändert vorhanden. |
| VIE-200 | Hinweisliste der Qualitätsprüfung | Soll | `F8` öffnet ein nicht modales Fenster, das alle Fundstellen des Regelkatalogs aus 09 mit Regelnummer, Schwere und Schritt; nach einer Bearbeitung, die eine Fundstelle behebt, ist sie ohne Zutun verschwunden; für Sitzung M steht die Liste in höchstens 1 s. |
| VIE-201 | Sprung und Abhilfe | Soll | Ein Klick auf eine Fundstelle wählt den Schritt und zeigt die betroffene Stelle; `F4` und `Umschalt+F4` wechseln die Fundstelle; die im Katalog genannte Abhilfe ist als Schaltfläche vorhanden; Ablauf V17. |
| VIE-202 | Ignorieren je Fundstelle | Soll | „Ignorieren“ entfernt genau diese Fundstelle aus Liste und Export-Vorprüfung und legt einen Eintrag in `edits.qualityIgnores` an; „Ignorierte zeigen“ macht es rückgängig. Die Sperre für ungeprüfte auffällige Eingaben bleibt auch nach dem Ignorieren bestehen. |
| VIE-210 | Geführter Support-Ablauf | Soll | Nach einer Support-Aufnahme zeigt der Viewer die drei Schritte Ansehen, Eingaben prüfen, Paket erstellen mit „Zurück“, „Weiter“ und „Abbrechen“; der Ablauf ist über das Menü „Export“ für jede Sitzung aufrufbar. |
| VIE-211 | Pflicht-Durchsicht | Soll | „Weiter“ in Schritt 2 ist gesperrt, solange eine Eingabe – auffällig oder nicht – oder ein Text des Autors ungeprüft ist; ein Paket lässt sich auf keinem Weg ohne vollständige Durchsicht erstellen (T-16). |
| VIE-212 | Paket erstellen und zeigen | Soll | „Paket erstellen“ erzeugt die ZIP-Datei nach 09 und öffnet den Explorer mit ihrem Ordner; der Dateiname ist vorher zu sehen. Ein Mitschnitt zeigt weder Netzwerkzugriff noch den Start eines Mail- oder Teilen-Programms. |
| VIE-213 | Höchstzahl im Support-Ablauf | Soll | Kernablauf 3 aus 2.1 ist mit höchstens 7 Aktionen zuzüglich 1 je auffälliger Eingabe ausführbar (T-16). |
| VIE-220 | Volltextsuche über alle Sitzungen | Soll | Die Suche „Inhalte“ findet einen Text in getippten Texten, Elementnamen, Fenstertiteln, Kommentaren, Ziel-Angaben, Sitzungstiteln, eigenen Schritttexten und Marker-Texten aller Sitzungen des Aufnahmeordners; Treffer in ersetzten Schritten und nicht verwendeten Neuaufnahmen sind als solche gekennzeichnet; Ablauf V19. |
| VIE-221 | Treffer mit Sprung | Soll | Jeder Treffer nennt Sitzung, Schritt und Umgebung mit Vorschaubild; ein Klick öffnet den Viewer an diesem Schritt. |
| VIE-222 | Index nur aus Freigegebenem | Soll | Ein geschwärzter Text, eine maskierte Eingabe, eine ungeprüfte auffällige Eingabe, ein interner Kommentar und der Originalwert einer Verallgemeinerung ergeben keinen Treffer und sind in keiner Datei unter `cache/search/` auffindbar; nach einer neuen Schwärzung ist der alte Index verworfen. |
| VIE-223 | Index abgeleitet, Suche ehrlich und schnell | Soll | Nach dem Löschen von `cache/search/` liefert die Suche nach dem Neuaufbau dieselben Treffer; Sitzungen ohne gültigen Index sind als „noch nicht durchsucht“ ausgewiesen. Mit vorhandenen Indizes erscheinen die Treffer aus 200 Sitzungen auf System A in höchstens 2 s. |
| VIE-230 | Befehlstabelle und Kürzelübersicht | Soll | Tasten, Menükürzel und `F1` kommen aus einer Tabelle; `F1` führt Editor- und Aufnahme-Kürzel, durchsuchbar nach Wortfolgen. |
| VIE-231 | Fensterlage und gemerkter Zustand | Soll | Start-, Editor- und Bibliotheksfenster öffnen dort, wo sie geschlossen wurden, auch nach Wechsel von Monitoren oder Skalierung; Spaltenbreiten, Reiter, letzter Schritt, Filter „Alle“ und Bildansicht bleiben erhalten; die Aufnahme enthält davon nichts. |
| VIE-232 | Linie, Freihand, Spotlight, Lupe | Soll | Die vier Werkzeuge sind mit `Umschalt+A`, `F`, `U` und `L` wählbar; Spotlight dunkelt je Strichstärke mit 0,4, 0,55 oder 0,7 ab; Linse und Quelle der Lupe sind getrennt verschiebbar; Kopie und Export zeigen dasselbe wie der Editor. |
| VIE-233 | Markierungen nur auf ihrem Bild | Muss | Eine auf dem Vorher-Bild gezeichnete Markierung erscheint nicht auf dem Nachher-Bild desselben Schritts – weder im Editor noch in Kopie oder Export. |
| VIE-234 | Schritte zusammenfassen | Soll | Aufeinanderfolgende aufgenommene Schritte ohne Lücke und ohne Kapitelbeginn lassen sich mit `Strg+J` zusammenfassen und mit `Strg+Umschalt+J` aufheben; Anleitungen zeigen einen Schritt, KI-Paket und AutoHotkey die Einzelschritte. |
| VIE-235 | Bearbeitungsstände | Kann | Nach Bearbeitungen liegen Stände unter `history/` (höchstens 40 und 15 MB); Wiederherstellen ist mit `Strg+Z` rückgängig zu machen und lässt Schwärzungen, Verallgemeinerungen und Durchsicht unverändert; „Endgültig schwärzen“ löscht alle Stände. |
| VIE-236 | Bibliothek, Packen, Entpacken | Soll | „Alle Aufnahmen“ listet Aufnahmen und Archive mit geschwärztem Vorschaubild; die bereinigte Kopie entsteht erst nach der Durchsicht; ein präpariertes Archiv hinterlässt beim Entpacken nichts. |
| VIE-237 | Oberflächensprache | Soll | Mit der Oberflächensprache Englisch erscheinen alle Fenster und Meldungen englisch; Texte, die in die Aufnahme oder einen Export gelangen, folgen der Sprache der Anleitungstexte. |
| VIE-239 | Seitenansicht | Soll | „Seite“ zeigt die Seiten des PDF-Exports; nach einer Textänderung ist die Seite bei einer mittleren Aufnahme in höchstens 1 s aktualisiert; Klick wählt den Schritt, Auswahl springt zur Stelle; ungeprüfte Eingaben erscheinen nur als Platzhalter. |
| VIE-240 | Drucken | Soll | `Strg+P` druckt die Anleitung wie das PDF, nach der Durchsicht, mit Seitenbereich; Abbrechen hinterlässt nichts. |
| VIE-242 | Schrittnummer im Bild | Soll | Mit „Schrittnummer“ zeigt das Bild von Schritt 1.2 eine Marke „1.2“ an der Klickstelle, in Editor und Anleitung gleich; nach dem Umsortieren stimmt die Nummer; das KI-Paket zeigt keine Nummer. |
| VIE-243 | Farben, Stil und Nummern | Soll | Zwölf Farben, freie Farbe und Pipette; „Stil“ ändert Linienart, Spitzen, Füllung, Hintergrund, Form, Zählweise und Deckkraft der gewählten Zeichnung; nach dem Löschen der Nummer 2 heißen die Nummern 1, 2 statt 1, 3; „Stil übertragen“ ändert alle Zeichnungen derselben Art. |
| VIE-244 | Querverweis, Variable, Tabelle einfügen | Soll | „＋ → Querverweis → Schritt 1“ zeigt im Feld „Schritt 1“ und speichert `[[ref:s…]]` mit `fmt: 1`; eine eingefügte Tabelle lässt sich im Feld bearbeiten und erscheint in der Seitenansicht; `Produkt = Kasse` im Dokumentbereich ergibt `guide.vars`. |
| VIE-245 | Gliederung bearbeiten | Soll | Kapitel 2 mit `Alt+Umschalt+↑` steht vor Kapitel 1 samt aller Schritte; „Zum Unterkapitel machen“ nummeriert „1.2“ und die Schritte darin „1.2.1“; ein ausgeblendetes Kapitel fehlt in der Seitenansicht; ein Textabschnitt hat keine Nummer und verschiebt die Nummern nicht. |
| VIE-246 | Bildunterschrift, Alternativtext, Signalwort | Soll | Eine eingegebene Bildunterschrift erscheint mit „Eigene“ in der Seitenansicht unter dem Bild; „GEFAHR“ als Art färbt den Hinweis rot und speichert `kind: warning, signal: danger`; der englische Hinweistext lässt den deutschen unverändert. |
| VIE-247 | Bausteine bearbeiten | Soll | Ein Glossareintrag „Mandant = …“ erscheint in der Seitenansicht am Ende im Glossar; „Am Ende“ beim Impressum verschiebt es hinter die Schritte; in der englischen Ansicht bleibt der deutsche Text unverändert. |
| VIE-248 | Seite und Layout | Soll | „Handbuch“ als Layoutvorlage zeigt in der Seitenansicht Kopfzeile mit Titel und Kapitel, Fußzeile „Seite 1 von N“ und Kapitel auf neuen Seiten; „Neue Seite vor diesem Schritt“ beginnt dort eine Seite. |
| VIE-241 | Formatierbare Textfelder | Soll | `Strg+B` im Anleitungstext zeigt sofort Fettdruck; nach dem Verlassen ist der Text mit `fmt: 1` gespeichert und erscheint fett in der Seitenansicht; ein Text ohne Formatierung wird ohne Kennzeichen gespeichert; Einfügen aus Word bringt nur den Text. |
| VIE-238 | Terminal, Remote-Desktop, virtuelle Maschinen | Muss | Getipptes in diesen Fenstern ist prüfpflichtig; in der Schutzstufe „Streng“ ist es vollständig maskiert. Kennwortabfragen im Terminal werden nicht automatisch erkannt. |

## 18 Offene Punkte

| Nr. | Punkt | Klärung |
|---|---|---|
| 1 | Welches Format Word, Outlook und Teams beim Einfügen tatsächlich bevorzugen, wenn HTML und RTF zugleich vorliegen | Messung beim Bau des Viewers (M3); die Matrix in 10.3 ist das Abnahmekriterium. |
| 2 | `Strg+C` bei Mehrfachauswahl legt Bilder als HTML/RTF und als Dateien ab – verhalten sich alle Ziele damit sinnvoll? | Mit der Matrix prüfen; sonst Dateien nur über „Als Datei kopieren“. |
| 3 | Ausgabe der Wiedergabe als GIF oder Video | Bibliothekswahl offen; erst nach Version 1. |
| 4 | Standard-Darstellung neuer Sitzungen: „Ganzer Bildschirm“ oder „Anwendungsfenster“ | Nach ersten Nutzertests entscheiden; derzeit „Ganzer Bildschirm“. |
| 5 | Bearbeitungen ersetzter Schritte auf die neue Fassung übertragen | Derzeit nicht vorgesehen; nach ersten Erfahrungen mit dem Ersetzen entscheiden. |
| 6 | Ob Bilder im Vergleich über „Bild“ als Unterschied hinaus hervorgehoben werden | Kann; setzt gleichen Ausschnitt und gleiche Größe voraus. |
| 7 | Bezeichnung eines Markers im Viewer nachträglich ändern | Das Overlay kennt bisher nur den Typ (`steps[].markerKind`); bis dahin schwärzt der Autor die Bezeichnung oder ergänzt einen Kommentar. |
