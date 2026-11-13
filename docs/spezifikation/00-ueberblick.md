# 00 – Überblick

`Status: Entwurf 0.1 · Stand: 2026-11-11`

Dieses Dokument beschreibt, **was** StepRecorder ist, für wen es gedacht ist, was es bewusst nicht ist und nach welchen Grundsätzen alle weiteren Kapitel der Spezifikation entschieden wurden. Es ist der Einstieg für alle, die die Spezifikation lesen, umsetzen oder beurteilen.

## Inhalt

1. [Vision](#1-vision)
2. [Ausgangslage](#2-ausgangslage)
3. [Ziele](#3-ziele)
4. [Nicht-Ziele](#4-nicht-ziele)
5. [Zielgruppen und Anwendungsfälle](#5-zielgruppen-und-anwendungsfälle)
6. [Abgrenzung zu PSR und Alternativen](#6-abgrenzung-zu-psr-und-alternativen)
7. [Leitprinzipien](#7-leitprinzipien)
8. [Systemüberblick](#8-systemüberblick)
9. [Rahmenbedingungen](#9-rahmenbedingungen)
10. [Aufbau der Spezifikation](#10-aufbau-der-spezifikation)
11. [Glossar](#11-glossar)

---

## 1. Vision

> Einmal vormachen – und daraus entsteht eine saubere Anleitung für Menschen **und** ein lückenloses Protokoll, mit dem eine KI exakt nachvollziehen kann, was passiert ist.

StepRecorder ist ein portables Windows-Werkzeug zur Dokumentation von Arbeitsabläufen. Es zeichnet jede Maus- und Tastatureingabe mit Bildschirmbildern und dem Kontext der Bedienelemente auf, leitet daraus nachvollziehbare Schritte ab und stellt sie in einem eigenen Viewer zum Ansehen, Bearbeiten, Kopieren und Exportieren bereit. Es läuft ohne Installation und ohne Netzwerk.

## 2. Ausgangslage

Die **Windows-Schrittaufzeichnung (PSR, `psr.exe`)** war lange das Standardwerkzeug für „Zeig mir, was du gemacht hast“. Sie ist seit November 2023 abgekündigt. Als Ersatz nennt Microsoft nur Videorekorder (Snipping Tool, Xbox Game Bar, Clipchamp). Ihre Schwächen:

| Schwäche von PSR | Folge |
|---|---|
| Getippter Text wird nicht aufgezeichnet | Formulareingaben, Befehle, Suchbegriffe fehlen |
| Standardmäßig nur die letzten 25 Bilder; ältere werden ohne Warnung verworfen | Längere Abläufe sind unvollständig |
| Bild entsteht nach dem Klick | Oft ist schon die Reaktion zu sehen, nicht der Zustand davor |
| Keine Mausbewegungen, kein Verweilen, kein Ziehen-Weg | Hover-Menüs und Drag & Drop sind nicht nachvollziehbar |
| Ausgabe als MHT im ZIP, nicht bearbeitbar | Für Anleitungen muss alles neu gebaut werden |
| Keine maschinenlesbare Ausgabe | KI muss raten |

**Kommerzielle Werkzeuge** (Scribe, Tango, Guidde, Folge) erzeugen schöne Anleitungen, brauchen aber Installation, meist Konto und Cloud, und liefern keine vollständige maschinenlesbare Spur.

**Open-Source-Ansätze** decken Teile ab: BetterStepsRecorder (Klicks und Drag & Drop), der Workflow-Recorder von terminator (Ereignisse und UI Automation, aber keine Bilder und Passwörter im Klartext), OpenAdapt (Python, Forschungsfokus, beendet die Aufnahme bei Monitorwechsel). Für Agenten-Trajektorien gibt es kein einheitliches Format; am verbreitetsten ist ein PyAutoGUI-ähnliches Aktionsvokabular (z. B. AgentNet/OpenCUA).

Es fehlt die Kombination **portabel + vollständig + datensparsam + KI-tauglich + angenehm zu bedienen**.

## 3. Ziele

| Nr. | Ziel | Messbar an |
|---|---|---|
| Z1 | **Ohne Installation** lauffähig | Eine EXE, keine Adminrechte, keine Registry-Einträge, kein Dienst |
| Z2 | **Vollständige Aufzeichnung** aller Klicks, Mausbewegungen, Tasteneingaben und Tastenkombinationen | Jedes Eingabeereignis steht im Rohprotokoll oder in einem benannten Lückenmarker |
| Z3 | **Aussagekräftige Bilder** zu jedem Schritt | Vorher-Bild garantiert vor der Eingabe, Nachher-Bild nach Beruhigung, Programm selbst nie im Bild |
| Z4 | **Lückenlos nachvollziehbar für KI** | KI-Paket und MCP-Server; ein Agent kann Ablauf, Ziele, Wirkungen und Lücken korrekt wiedergeben |
| Z5 | **Anleitungen schnell erstellen** | Viewer mit Bearbeitung, Zeichenwerkzeugen, Kommentaren, Qualitätsprüfung, Kopierfunktionen; Export HTML, Markdown, DOCX, PDF, PowerPoint, GIF, Video (MP4), Klick-Tour, Fehlerbericht |
| Z6 | **Privatsphäre ab Werk** | Eingaben in erkannte Passwortfelder nie auf der Platte, kein Netzwerkzugriff, sichtbare Aufnahme |
| Z7 | **Einfach im Normalfall, flexibel bei Bedarf** | Aufnahme mit einer Aktion, höchstens zwei Klicks; Aufnahmebereich und Darstellung frei wählbar |
| Z8 | **Offen** | Kostenloses Programm, Quellcode frei unter der MIT-Lizenz (siehe [LICENSE](../../LICENSE)), dokumentierte und versionierte Formate |

## 4. Nicht-Ziele

- **Keine Automatisierung oder Wiedergabe.** Das Tool steuert keine Programme und spielt Abläufe nicht ab. Es erzeugt höchstens **Text**, den andere Werkzeuge ausführen: ein AutoHotkey-Skript oder einen UI-Test-Entwurf für FlaUI, pywinauto oder Playwright ([09 §14a](09-anleitungs-export.md#14a-autohotkey-skript), [§14d](09-anleitungs-export.md#14d-ui-test-entwürfe)). Ausführen ist Sache dieser Werkzeuge und des Nutzers.
- **Keine Überwachung.** Kein verdeckter Modus, kein Autostart, keine Daueraufzeichnung im Hintergrund, keine zentrale Sammlung. Das ist ein Projektgrundsatz, kein fehlendes Feature. Auch ein Agent startet nie von sich aus eine Aufnahme: Er kann über MCP nur um eine Vorführung bitten (nur mit `--allow-requests`), und der Nutzer bestätigt jede Anfrage ausdrücklich.
- **Keine eingebaute KI und keine Cloud.** Das Tool ruft keine KI-Dienste auf und sendet nichts über das Netzwerk. KI-Nutzung geschieht außerhalb – über den Export oder den lokalen MCP-Server.
- **Kein Videorekorder.** Bilder entstehen ereignisgesteuert; eine Abspielfunktion im Viewer zeigt den Ablauf als animierte Bildfolge. Auch der Video-Export (MP4) zeichnet nichts auf: Er setzt nur die Schrittbilder zu einer Bildfolge zusammen ([09 §8e](09-anleitungs-export.md#8e-video-mp4)).
- **Version 1 nur Windows.** Kein macOS oder Linux. Hauptplattform ist x64; ein ARM64-Build (`win-arm64`) wird mitgebaut und veröffentlicht, ist auf ARM-Hardware aber nicht getestet ([02 §11](02-architektur.md#build)).

## 5. Zielgruppen und Anwendungsfälle

| Zielgruppe | Typischer Anwendungsfall |
|---|---|
| IT-Support, Helpdesk | Nutzer zeichnet ein Problem auf; Support sieht exakt, was geklickt und getippt wurde, inklusive Fehlermeldungstext |
| Technische Redaktion, Schulung | Bedienungsanleitungen und Arbeitsanweisungen aus einer Vorführung erzeugen, nachbearbeiten, als DOCX/PDF ausliefern |
| Software-Test, QA | Reproduktionsschritte für Fehlerberichte mit Bildern und exakten Eingaben |
| Prozessdokumentation | Abläufe in Fachanwendungen (ERP, Verwaltung) festhalten, Kapitel bilden, aktuell halten |
| KI-gestützte Arbeit | Einer KI zeigen, wie etwas gemacht wurde: zur Erklärung, Fehlersuche, Anleitungserstellung oder als Vorlage für Automatisierung |
| Entwickler | Agent (z. B. Claude Code) fragt über MCP eine Aufzeichnung ab, um einen gemeldeten Fehler nachzuvollziehen |

**Kernabläufe:**

1. *Aufnehmen → ansehen → kopieren:* Aufnahme starten, Ablauf durchführen, stoppen; der Viewer öffnet sich; Bild eines Schritts mit Strg+C in eine Mail oder ein Ticket einfügen.
2. *Aufnehmen → bearbeiten → exportieren:* Schritte ordnen, Texte anpassen, Markierungen und Kommentare setzen, Sensibles schwärzen, persönliche Werte verallgemeinern, Qualitätshinweise abarbeiten, als DOCX oder PDF exportieren.
3. *Aufnehmen → an KI übergeben:* Ziel und erwartetes Ergebnis angeben, Fehlerstelle markieren, KI-Paket exportieren und hochladen – oder ein Agent liest die Sitzung direkt über MCP.
4. *Support-Paket:* Der Helpdesk gibt jemandem die EXE; dieser zeichnet das Problem auf, sieht seine Eingaben durch und erhält ein fertiges ZIP mit Anleitung und KI-Paket zum Zurückschicken.
5. *Anleitung pflegen:* Nach einem Software-Update den betroffenen Abschnitt neu aufnehmen und ersetzen (noch nicht umgesetzt); mit der alten Aufnahme vergleichen, um Änderungen zu sehen.
6. *Aufnahmen verwalten:* Im Fenster „Alle Aufnahmen“ finden, über alle Inhalte durchsuchen, umbenennen, als `.steprec` packen (bereinigt oder vollständig) und in den Papierkorb legen; frühere Bearbeitungsstände wiederherstellen.
7. *Agent bittet um Vorführung:* Ein Agent bittet über MCP um eine Aufnahme zu einer Aufgabe; der Nutzer sieht die Anfrage, bestätigt oder lehnt ab und führt vor; danach liest der Agent das Ergebnis.

## 6. Abgrenzung zu PSR und Alternativen

| Merkmal | PSR | Kommerzielle Guide-Tools | StepRecorder |
|---|---|---|---|
| Installation | integriert (abgekündigt) | Installation, Konto | **keine**, eine EXE |
| Netzwerk/Cloud | nein | ja | **nein** |
| Getippter Text | nein | teilweise | **ja**, Passwortfelder maskiert |
| Mausbewegung, Verweilen, Ziehen | nein | nein | **ja** |
| Bild vor der Eingabe garantiert | nein | unbekannt | **ja** |
| Programm im Bild | teilweise | teilweise | **nie** |
| Aufnahmebereich wählbar | nein | teilweise | **Bildschirm, Anwendung, Bereich** |
| Bearbeiten, Kommentieren | nein | ja | **ja** |
| Export | MHT | HTML, PDF, teils DOCX | **HTML, Markdown, DOCX, PDF, PowerPoint, GIF, Klick-Tour, Fehlerbericht** |
| Maschinenlesbare, lückenlose Spur | nein | nein | **ja** (JSONL, KI-Paket, MCP) |
| Lücken ausgewiesen | nein | nein | **ja**, mit Ursache |
| Ziel der Aufnahme, typisierte Marker | nein | teilweise | **ja** |
| Aufnahmen vergleichen, Abschnitte ersetzen | nein | teilweise | **Vergleich ja**; Abschnitte ersetzen noch nicht umgesetzt |
| Persönliche Werte verallgemeinern | nein | nein | **ja** |
| Support-Paket zum Zurückschicken | nein | nein | **ja**, ohne Versand durch das Programm |
| Offen | nein | nein | **kostenlos, MIT-Lizenz** |

## 7. Leitprinzipien

Bei Zielkonflikten entscheiden diese Prinzipien. Kollidieren Vollständigkeit (2) und Privatsphäre (3), hat die Privatsphäre Vorrang – die Lücke wird dann als solche ausgewiesen.

1. **Rohdaten zuerst, Deutung danach.** Alles wird unverändert protokolliert; Schritte werden daraus abgeleitet und lassen sich mit besseren Regeln neu berechnen.
2. **Lücken und Unsicherheit werden nie verschwiegen.** Was nicht aufgezeichnet werden konnte oder durfte, steht als Lückenmarker im Protokoll; jede Aussage trägt ihre Belegstufe.
3. **Privatsphäre ab Werk.** Sichtbare Aufnahme, Eingaben in erkannte Passwortfelder erreichen nie die Festplatte, kein Netzwerk.
4. **Portabel.** Eine EXE, keine Adminrechte, keine Registry-Einträge, kein Dienst.
5. **Ein Datenbestand, viele Ausgaben.** Viewer, Anleitung, KI-Protokoll und MCP lesen dieselbe Sitzung – über eine gemeinsame Leseschicht, die Schwärzungen erzwingt.
6. **Einfach im Normalfall, flexibel bei Bedarf.** Der häufigste Weg braucht eine Aktion, höchstens zwei Klicks; Optionen sind da, stehen aber nicht im Weg.

## 8. Systemüberblick

```
 Capture-Host (eigener Minimalprozess)
   Maus-/Tastatur-Hooks + Raw Input ──┐  gemeinsamer Speicher
                                      ▼
 Bildschirm ─────┐
 UI Automation ──┼─► Rohprotokoll ─► Schritt-  ─► Lese-    ─┬─ Viewer ─► Kopieren · HTML · Markdown · DOCX · PDF
 Fenster/System ─┤   events.jsonl    erkennung    schicht   ├─ KI-Paket (trace.md, steps.json, Bilder)
 Zwischenablage ─┘   + frames/                              ├─ MCP-Server (stdio)
                                                            └─ Validator (Vollständigkeitsbericht)
```

| Baustein | Aufgabe | Kapitel |
|---|---|---|
| Aufzeichnungskern | Eingaben, Bilder, UI-Elemente, Fenster- und Systemereignisse erfassen | [03](03-aufzeichnungskern.md) |
| Datenformat | Sitzungsordner, Rohprotokoll, Schritte, Bearbeitungen | [04](04-datenformat.md) |
| Schritterkennung | Rohereignisse zu verständlichen Schritten mit Wirkung und Belegstufe | [05](05-schritterkennung.md) |
| KI-Export und MCP | Lückenlose Ausgabe für KI; Abfrage durch Agenten | [06](06-ki-export-und-mcp.md) |
| Aufnahme-Bedienung | Startfenster, Aufnahmebereich, Leiste, Hotkeys, Unsichtbarkeit | [07](07-aufnahme-bedienung.md) |
| Viewer | Ansehen, Abspielen, Bearbeiten, Kommentieren, Kopieren | [08](08-viewer.md) |
| Anleitungs-Export | HTML, Markdown, DOCX, PDF, PowerPoint, GIF, Klick-Tour, Fehlerbericht | [09](09-anleitungs-export.md) |
| Bibliothek | Alle Aufnahmen finden und durchsuchen, als `.steprec` packen und entpacken, bereinigte Kopie | [08](08-viewer.md) |
| Datenschutz | Maskierung, Ausschlüsse, Schwärzung, Bedrohungsmodell | [10](10-datenschutz-und-sicherheit.md) |

## 9. Rahmenbedingungen

| Bereich | Festlegung |
|---|---|
| Betriebssystem | Windows 10 22H2 und Windows 11, x64 (ARM64: Build vorhanden, auf ARM-Hardware nicht getestet) |
| Rechte | Läuft als normaler Benutzer; Neustart mit Adminrechten optional, um Fenster mit Adminrechten aufzunehmen |
| Technologie | C# / .NET 10 (LTS, Support bis November 2028), WPF, self-contained Single-File-EXE ([02](02-architektur.md)). *Stand: umgesetzt* |
| Abhängigkeiten | Nur freizügige Lizenzen (MIT, Apache-2.0, BSD) |
| Netzwerk | Kein Netzwerkzugriff |
| Sprache | Oberfläche Deutsch und Englisch (Einstellung „System“ folgt Windows; die Sprache der Anleitungstexte ist davon unabhängig); Datenformat-Bezeichner englisch; Spezifikation deutsch |
| Lizenz des Projekts | MIT-Lizenz für Programm und Quellcode (seit 0.14.0; vorher CC BY-ND 4.0); anonyme Ausgabe mit `-p:Anonymous=true` – siehe [LICENSE](../../LICENSE) |
| Arbeitstitel | StepRecorder, `SebastiansStepRecorder.exe` |

## 10. Aufbau der Spezifikation

| Datei | Inhalt | Anforderungs-Präfix |
|---|---|---|
| [00-ueberblick.md](00-ueberblick.md) | Dieses Dokument | – |
| [01-anforderungen.md](01-anforderungen.md) | Gesamtindex aller Anforderungen, nicht-funktionale Anforderungen, Rückverfolgung | `NFA-` |
| [02-architektur.md](02-architektur.md) | Prozesse, Komponenten, Threads, Technik, Build | `ARC-` |
| [03-aufzeichnungskern.md](03-aufzeichnungskern.md) | Eingaben, Bildschirm, UI Automation, Lücken, Budgets | `AUF-` |
| [04-datenformat.md](04-datenformat.md) | Sitzungsformat, Ereignistypen, Beispiele | `DAT-` |
| [05-schritterkennung.md](05-schritterkennung.md) | Schritte, Wirkung, Belegstufen, Textvorlagen | `SCH-` |
| [06-ki-export-und-mcp.md](06-ki-export-und-mcp.md) | KI-Paket, MCP-Server | `KI-`, `MCP-` |
| [07-aufnahme-bedienung.md](07-aufnahme-bedienung.md) | Start, Aufnahmebereich, Leiste, Hotkeys | `BED-` |
| [08-viewer.md](08-viewer.md) | Viewer, Bildbearbeitung, Kopieren, Komfort | `VIE-` |
| [09-anleitungs-export.md](09-anleitungs-export.md) | Anleitungs-Exporte | `EXP-` |
| [10-datenschutz-und-sicherheit.md](10-datenschutz-und-sicherheit.md) | Datenschutz, Sicherheit, Signierung | `DS-` |
| [11-roadmap-tests-risiken.md](11-roadmap-tests-risiken.md) | Meilensteine, Experimente, Tests, Risiken | – (Kennungen: Meilensteine `M0`–`M8`, Experimente `VE-`, Tests `T-`) |
| [12-entwickler-fahrplan.md](12-entwickler-fahrplan.md) | Fahrplan für Software-Entwickler: Pakete 8–19, Reihenfolge, Schutzregeln, Abgleich Grundsätze ↔ Code | – (Pakete; Abweichungen `A-`; Vorgaben V-40–V-45 in 01) |
| [13-handbuch-fahrplan.md](13-handbuch-fahrplan.md) | Fahrplan Handbuch-Editor und Bild-Editor: Pakete 20–38, Leitplanken, Entscheidungen | – (Pakete; Vorgaben V-46–V-54 in 01) |

**Prioritäten:** *Muss* = Voraussetzung für Version 1.0; *Soll* = für 1.0 vorgesehen, kann begründet verschoben werden; *Kann* = Ausbau.

## 11. Glossar

| Begriff | Bedeutung |
|---|---|
| **Sitzung** | Eine Aufzeichnung: Ordner (oder `.steprec`-ZIP) mit Rohprotokoll, Bildern, Schritten und Bearbeitungen |
| **Rohprotokoll** | `events.jsonl` – unveränderliche, zeitlich geordnete Liste aller erfassten Ereignisse |
| **Ereignis** | Ein Eintrag im Rohprotokoll, z. B. Tastendruck, Mausbewegung, Fensterwechsel, Bild |
| **Schritt** | Aus Ereignissen abgeleitete, für Menschen verständliche Handlung („Klick auf ‚Speichern‘“) |
| **Wirkung** | Was ein Schritt sichtbar ausgelöst hat: neue/geschlossene Fenster, geänderte Werte, Titel |
| **Belegstufe** | Wie sicher ein Schritt belegt ist: *belegt*, *nur Eingabe*, *unbekannt* |
| **Wichtigkeitsstufe** | Hauptschritt, Nebenschritt oder Spurdaten – steuert, was die Anleitung zeigt |
| **Ziel der Aufnahme** | Optionale Angaben des Autors: was gezeigt wird, erwartetes Ergebnis, Bezug (z. B. Ticketnummer) – Absicht, keine Beobachtung |
| **Marker** | Zeitpunkt, den der Autor während der Aufnahme setzt; Typen: Notiz, Fehler, Erwartet, Wichtig |
| **Aufnahmeprofil** | Benannte Voreinstellung für Aufnahme und Export (z. B. „Anleitung“, „Support-Fall“, „KI-Spur“) |
| **Verallgemeinerung** | Regel, die einen persönlichen Wert in allen Ausgaben durch einen Platzhalter wie `{Benutzername}` ersetzt |
| **Ersetzung** | Neu aufgenommener Abschnitt, der markierte Schritte in der Anleitung ersetzt; die alten bleiben in den Rohdaten (noch nicht umgesetzt) |
| **Support-Paket** | ZIP aus einer bereinigten Kopie mit HTML-Anleitung und KI-Paket, zum Weitergeben an einen Helpdesk |
| **Lückenmarker** | Ereignis `gap`: benennt Zeitraum und Ursache, warum nichts oder nur Teile erfasst wurden |
| **Vorher-/Nachher-Bild** | Bildschirmzustand unmittelbar vor der Eingabe bzw. nach Beruhigung des Bildschirms |
| **Zwischenbild** | Bild einer wesentlichen Änderung ohne Eingabe (z. B. aufpoppender Dialog) |
| **Schlüssel-/Differenzbild** | Vollbild bzw. nur der geänderte Bereich gegenüber dem Vorgänger |
| **Aufnahmebereich** | Was aufgenommen wird: Bildschirm(e), Anwendung(en) oder Rechteck |
| **Darstellung** | Was gezeigt wird: Vollbild, Anwendungsfenster oder Ausschnitt – unabhängig vom Aufnahmebereich |
| **Unsichtbarkeitsgarantie** | Kein Teil des Recorders erscheint in einem Bild |
| **Capture-Host** | Minimalprozess, der die Eingabe-Hooks ausführt |
| **Leseschicht** | Zentrale Komponente, über die alle Ausgaben lesen; erzwingt Schwärzungen |
| **Overlay / `edits.json`** | Alle Bearbeitungen getrennt von den Rohdaten |
| **Bearbeitungsstand** | Automatische Sicherung des Overlays (`history\`), aus der sich eine frühere Fassung wiederherstellen lässt; Schwärzungen und Durchsicht bleiben auf heutigem Stand |
| **Zusammenfassung** | Mehrere aufeinanderfolgende Schritte, die eine Anleitung als einen Schritt zeigt; KI-Paket und Rohdaten behalten die Einzelschritte |
| **Qualitätsprüfung** | Regeln Q01–Q21, die Anleitung und Aufnahme auf Mängel prüfen (Editor F8, Kommandozeile `check`); Befunde sind Warnungen oder Hinweise |
| **Bibliothek** | Fenster „Alle Aufnahmen“: finden, durchsuchen, umbenennen, packen, löschen |
| **`.steprec`** | Eine Aufnahme als ZIP-Datei, bereinigt oder vollständig; nur ansehen, zum Bearbeiten entpacken |
| **Bereinigte Kopie** | Neue Aufnahme aus der Sicht der Leseschicht, ohne Geschwärztes, Regelwerte und interne Kommentare |
| **Agent-Anfrage** | Bitte eines Agenten über MCP um eine Vorführung; der Nutzer bestätigt oder lehnt ab |
| **Sicherer Desktop** | Gesonderte Arbeitsfläche von Windows, etwa für die Abfrage der Benutzerkontensteuerung (UAC); dort werden weder Eingaben noch Bilder aufgezeichnet, die Aufnahme zeigt einen benannten Schritt |
| **Privattaste** | Umschalter, der Tasteninhalte maskiert, bis er erneut gedrückt wird (Pausieren ist eine eigene Funktion) |
| **Schutzstufe** | *Schutznetz* (Standard) oder *Streng* – wie Eingaben in nicht klassifizierbaren Fenstern behandelt werden |
| **Undurchsichtige Oberfläche** | Fenster ohne verwertbare Elementinformationen (Remote-Desktop, VM, Spiel, Zeichenfläche) |
| **KI-Paket** | Export für KI: `README_AI.md`, `trace.md`, `steps.json`, Bilder |
| **MCP** | Model Context Protocol – Standard, über den KI-Agenten Werkzeuge lokal abfragen |
| **UIA** | UI Automation – Windows-Schnittstelle für Informationen über Bedienelemente |
| **DXGI Desktop Duplication** | Windows-Schnittstelle für schnelle Bildschirmaufnahme |
| **Low-Level-Hook / Raw Input** | Zwei Windows-Mechanismen, globale Maus- und Tastatureingaben zu empfangen |
