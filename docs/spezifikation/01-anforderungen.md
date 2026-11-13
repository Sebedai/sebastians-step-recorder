# 01 – Anforderungen

`Status: Entwurf 0.1 · Stand: 2026-11-11`

Dieses Dokument ist der **Gesamtindex** aller Anforderungen. Die einzelnen Anforderungen stehen mit Abnahmekriterium in den Fachkapiteln; hier stehen die Konventionen, eine Übersicht je Kapitel, die kapitelübergreifenden nicht-funktionalen Anforderungen, die **Rückverfolgung** von jeder Vorgabe des Auftraggebers auf Anforderungen und Tests sowie das Protokoll der Entscheidungen, die beim Abgleich der Kapitel getroffen wurden.

## Inhalt

1. [Konventionen](#1-konventionen)
2. [Übersicht je Kapitel](#2-übersicht-je-kapitel)
3. [Nicht-funktionale Anforderungen](#3-nicht-funktionale-anforderungen)
4. [Rückverfolgung der Vorgaben](#4-rückverfolgung-der-vorgaben)
5. [Rückverfolgung der Review-Befunde](#5-rückverfolgung-der-review-befunde)
6. [Entscheidungsprotokoll](#6-entscheidungsprotokoll)
7. [Pflege](#7-pflege)

---

## 1. Konventionen

| Thema | Regel |
|---|---|
| Kennung | `PRÄFIX-NNN`, dreistellig, je Kapitel eindeutig; Lücken in der Nummerierung sind erlaubt (Blöcke je Thema) |
| Ort | Jede Anforderung steht genau einmal, im Abschnitt „Anforderungen“ ihres Fachkapitels |
| Spalten | ID · Anforderung · Priorität · Abnahmekriterium |
| Priorität | **Muss** – Voraussetzung für Version 1.0 · **Soll** – für 1.0 vorgesehen, begründet verschiebbar · **Kann** – Ausbau. Gemischte Angaben („Muss (de), Soll (en)“) gelten je Teil. „Soll (Schutzregel: Muss bei Umsetzung)“ heißt: Die Funktion darf verschoben werden, ihre Schutzregel aber nie, sobald es die Funktion gibt |
| Abnahme | Jede Anforderung nennt ein prüfbares Kriterium; Systemtests `T-nn` und Experimente `VE-nn` stehen in [11](11-roadmap-tests-risiken.md), Datenschutzprüfungen `DT-nn` in [10 §17](10-datenschutz-und-sicherheit.md), Formatprüfungen `F..` und `S..` in [04](04-datenformat.md) und [05](05-schritterkennung.md) |
| Änderungen | Eine Kennung wird nie neu vergeben; entfallene Anforderungen bleiben mit „entfallen“ stehen |

## 2. Übersicht je Kapitel

| Kapitel | Präfix | Anzahl | Muss | Soll | Kann | Themenblöcke |
|---|---|---:|---:|---:|---:|---|
| [02 Architektur](02-architektur.md#16-anforderungen) | `ARC` | 76 | 43 | 33 | 0 | Prozesse · Schichten und Schreiben · Betriebsarten · Technologie · Build · Konfiguration und Betrieb · Oberflächensprache, Protokoll, Export- und Pack-Ablauf |
| [03 Aufzeichnungskern](03-aufzeichnungskern.md) | `AUF` | 111 | 73 | 37 | 1 | Lebenszyklus und Zeit · Eingabe · Bild · Aufnahmebereich · Unsichtbarkeit · UI Automation · Fenster/System/Zwischenablage/Lücken · Leistung |
| [04 Datenformat](04-datenformat.md) | `DAT` | 83 | 60 | 21 | 2 | Formate und Ordner · Ereigniskopf, Zeit, Koordinaten · Eingaben und Lücken · Bilder · `steps.json` · `edits.json` · Versionierung und Robustheit |
| [05 Schritterkennung](05-schritterkennung.md) | `SCH` | 64 | 47 | 17 | 0 | Grundsätze · Schrittarten · Ziel, Wirkung, Bilder · Belegstufen, Wichtigkeit · Textvorlagen · Bericht · Neuberechnung |
| [06 KI-Export](06-ki-export-und-mcp.md) | `KI` | 43 | 29 | 14 | 0 | Paketaufbau · `trace.md` · Bilder · Detailstufen · Entschärfung · Schwärzung |
| [06 MCP-Server](06-ki-export-und-mcp.md) | `MCP` | 43 | 26 | 16 | 1 | Start und Sandbox · Tools · Paging und Budgets · Bilder · Schwärzung · Robustheit |
| [07 Aufnahme-Bedienung](07-aufnahme-bedienung.md) | `BED` | 102 | 57 | 43 | 2 | Start · Aufnahmebereiche · Leiste · Tastenkürzel · Kommentar/Marker · Pause/Privattaste · Unsichtbarkeit · Einstellungen · `record` |
| [08 Viewer](08-viewer.md) | `VIE` | 138 | 58 | 77 | 3 | Öffnen · Ansehen · Abspielen · Bildbearbeitung · Schwärzen · Kommentare · Schritte · Kopieren · Sitzungen · Komfort · Durchsicht · Geschwindigkeit |
| [09 Anleitungs-Export](09-anleitungs-export.md) | `EXP` | 103 | 47 | 50 | 6 | Dialog · Optionen · HTML · Markdown · DOCX · PDF · Vorlagen · Sprache · Entschärfung · Vorprüfung · Kommandozeile |
| [10 Datenschutz](10-datenschutz-und-sicherheit.md) | `DS` | 75 | 49 | 25 | 1 | Grundsätze · Schutzstufen · Passwort-Rückhaltung · Terminal · Privattaste · Ausschlüsse · Zwischenablage · Schwärzung · Durchsicht · Netzwerk · Signierung |
| 01 (dieses Dokument) | `NFA` | 20 | 18 | 2 | 0 | kapitelübergreifend |
| **Summe** | | **858** | **507** | **335** | **16** | |

Gezählt sind gültige Anforderungen; entfallen sind BED-066 und VIE-004 (Entscheidungen E24, E19) sowie eine Anforderung in 03. Neu ermittelt am 13.11.2026 aus den Anforderungstabellen der Kapitel (die frühere Fortschreibung lag um 42 zu niedrig); enthalten sind die Ergänzungen des Entwickler-Fahrplans (MCP-039–043, EXP-198). Gemischte Prioritäten sind nach ihrem höchsten Teil gezählt. Die Zahlen werden bei jeder Spezifikationsversion neu ermittelt (Abschnitt 7).

## 3. Nicht-funktionale Anforderungen

Diese Anforderungen gelten für das ganze Produkt. Sie fassen zusammen und verweisen auf die Stellen, an denen sie im Einzelnen geregelt und geprüft werden.

| ID | Anforderung | Priorität | Abnahmekriterium | Einzelregelungen |
|---|---|---|---|---|
| NFA-001 | **Portabilität:** Das Programm läuft als einzelne EXE (oder entpacktes ZIP) ohne Installation, ohne Adminrechte, ohne Registry-Einträge, ohne Dienst. | Muss | T-01 | ARC-031–033, ARC-038, ARC-039, ARC-050 |
| NFA-002 | **Plattform:** Windows 10 22H2 und Windows 11, x64; .NET 10 (LTS) als eingebettete Laufzeit. | Muss | Testmatrix [11 §7](11-roadmap-tests-risiken.md#7-testmatrix) | ARC-025 |
| NFA-003 | **Vollständigkeit:** Jedes Eingabeereignis steht im Rohprotokoll oder ist durch einen Lückenmarker mit Ursache erklärt. | Muss | T-02; `validate` | AUF-087, AUF-088, DAT-018, SCH-005, SCH-035 |
| NFA-004 | **Ehrlichkeit:** Jeder Schritt trägt eine Belegstufe; nichts wird erfunden, Unsicheres wird als solches ausgewiesen. | Muss | T-09 | SCH-027, SCH-028, SCH-033, KI-008, KI-016, MCP-031 |
| NFA-005 | **Reaktionszeit des Systems:** Die Aufnahme verzögert keine Eingabe spürbar; 99,9 % der Hook-Aufrufe unter 1 ms. | Muss | VE-01 | ARC-003, ARC-004, AUF-012, DS-021 |
| NFA-006 | **Ressourcen:** Prozessor-, Speicher- und Datenträgerbedarf bleiben innerhalb der Budgets aus [03 §12](03-aufzeichnungskern.md); bei Überlast entstehen Lückenmarker statt stiller Verluste. | Muss | VE-09, T-13 | AUF-049, AUF-090–095, ARC-014 |
| NFA-007 | **Geschwindigkeit der Oberfläche:** Viewer, Kopieren und Aufnahmestart halten die Zeitziele aus [08 §14](08-viewer.md) und [07](07-aufnahme-bedienung.md) auf den Referenzsystemen aus [03 §12.1](03-aufzeichnungskern.md) ein (u. a. erste Anzeige unter 1 s, Aufnahmestart höchstens 1,5 s). | Muss | T-11 | VIE-140, VIE-141, BED-005 |
| NFA-008 | **Robustheit:** Nach Absturz, Stromausfall oder vollem Datenträger ist die Sitzung bis zum letzten Schreibvorgang lesbar. | Muss | T-13 | ARC-012, ARC-013, ARC-015, DAT-041–043 |
| NFA-009 | **Unsichtbarkeit:** Kein Teil des Recorders erscheint in einem Bild; der Inhalt dahinter bleibt vollständig. | Muss | T-05 | AUF-061–066, BED-080–086 |
| NFA-010 | **Privatsphäre ab Werk:** Eingaben in erkannte Passwortfelder erreichen nie den Datenträger; Schwärzungen gelten in jeder Ausgabe. | Muss | T-07, T-08, DT-01 ff. | DS-010–017, DS-032–036, DAT-033, KI-023, MCP-016 |
| NFA-011 | **Offline:** Kein Netzwerkzugriff durch Programm und Kindprozesse. | Muss | T-12 | ARC-005, ARC-029, DS-042, DS-043 |
| NFA-012 | **Transparenz der Aufnahme:** Aufnahme nur nach ausdrücklichem Start, für den Nutzer immer erkennbar; kein verdeckter Modus, kein Autostart. | Muss | Code-Review-Regel; DT-Prüfungen | DS-001–004, DS-051 |
| NFA-013 | **Einfachheit:** Kernablauf 1 (aufnehmen → ansehen → kopieren) und Kernablauf 2 (aufnehmen → bearbeiten → exportieren) gelingen mit der in [08 §2.1](08-viewer.md) festgelegten Höchstzahl an Klicks und Tasten. | Muss | Bedienabläufe in 07/08 | BED-001–009, VIE-142 |
| NFA-014 | **Bedienbarkeit per Tastatur:** Alle Funktionen von Leiste und Viewer sind ohne Maus erreichbar; Kürzelübersicht im Programm. | Muss | Tastaturdurchgang | BED-050–057, BED-111, VIE-123, VIE-124 |
| NFA-015 | **Barrierefreiheit der eigenen Oberfläche:** Eigene Fenster sind über UI Automation bedienbar, kontrastreich in Hell und Dunkel, skalieren mit der Systemeinstellung. | Soll | Prüfung mit Sprachausgabe und 200 % Skalierung | BED-111, BED-112, VIE-122, VIE-124 |
| NFA-016 | **Sprachen:** Oberfläche, Textvorlagen und Exporte in Deutsch und Englisch; Bezeichner im Datenformat englisch. *Stand: Oberfläche und Meldungen sind vollständig zweisprachig (ARC-066).* | Muss (de), Soll (en) | Sprachumschaltung ohne fehlende Texte | BED-104, BED-105, SCH-031, SCH-032, EXP-080–083, KI-024 |
| NFA-017 | **Offene, stabile Formate:** Sitzungsformat und KI-Paket sind dokumentiert, versioniert, mit JSON-Schemata; neue Programmversionen lesen ältere Sitzungen. | Muss | Migrationstests | DAT-001, DAT-038–040, DAT-045 |
| NFA-018 | **Reproduzierbarkeit:** Gleiche Eingabe ergibt gleiche Schritte und bytegleiche Exporte; Builds sind deterministisch. | Soll | Golden-Journal-Regression; Build-Vergleich | SCH-001, SCH-002, KI-022, ARC-034 |
| NFA-019 | **Lizenzen:** Nur Abhängigkeiten mit freizügigen Lizenzen (MIT, Apache-2.0, BSD); Lizenzhinweise Dritter werden mitgeliefert. | Muss | Lizenzprüfung im CI | ARC-026–028 |
| NFA-020 | **Vertrauenswürdige Auslieferung:** Releases sind signiert, mit Prüfsummen, ohne Verschleierung oder Laufzeitpacker. *Stand: Prüfsummen und Release über GitHub-Workflow (ARC-076); Signierung offen – die Lizenz CC BY-ND 4.0 ist keine OSI-Lizenz, die SignPath Foundation verlangt (Klärung in [11 §9](11-roadmap-tests-risiken.md#9-offene-entscheidungen)).* | Muss (für 1.0) | Release-Checkliste [11 §10](11-roadmap-tests-risiken.md#10-veröffentlichung) | ARC-035, DS-050–052 |

## 4. Rückverfolgung der Vorgaben

Jede Vorgabe des Auftraggebers ist mindestens einer Anforderung mit Abnahmekriterium zugeordnet.

| Nr. | Vorgabe | Anforderungen (Auswahl, maßgeblich) | Abnahme |
|---|---|---|---|
| V-01 | Besser als Microsoft PSR | [00 §6](00-ueberblick.md#6-abgrenzung-zu-psr-und-alternativen) – Gesamtheit der folgenden Vorgaben | Vergleichstabelle |
| V-02 | **Ohne Installation** lauffähig | NFA-001, ARC-031, ARC-032, ARC-033, ARC-038, ARC-039 | T-01 |
| V-03 | **Alle Klicks** aufzeichnen | AUF-009, AUF-014, SCH-005, SCH-006, SCH-007 | T-02, T-03 |
| V-04 | **Alle Mausbewegungen** aufzeichnen | AUF-018, DAT-014, SCH-022; Abspielen mit Mausweg VIE-040–045 | T-02 |
| V-05 | **Alle Tasteneingaben** aufzeichnen | AUF-019–023, DAT-015, SCH-009–013 | T-02, T-03 |
| V-06 | **Tastenkombinationen** usw. aufzeichnen | AUF-019, AUF-023, SCH-013, SCH-014, SCH-048 | T-02, T-03 |
| V-07 | **Screenshots** zu den Schritten | AUF-032–043, DAT-019–025, SCH-026 | T-04 |
| V-08 | **KI kann lückenlos nachvollziehen**, was passiert ist | NFA-003, NFA-004, AUF-087, AUF-088, AUF-075, SCH-027, SCH-035, KI-001–016, KI-028 | T-09 |
| V-09 | KI-Zugriff für Agenten (Entscheidung: MCP-Server) | MCP-001–031 | T-10 |
| V-10 | **Bedienungsanleitungen und Arbeitsschritte erstellen** | SCH-029, SCH-031–034, EXP-001–131 | Kernablauf 2 |
| V-11 | **Eigener Viewer** zum schnellen Ansehen nach der Aufnahme | VIE-001–032 | T-11, Kernablauf 1 |
| V-12 | **Bilder bearbeiten** | VIE-050–059 (Werkzeuge), VIE-060–069 (Schwärzen) | Bedienablauf „Bild bearbeiten“ |
| V-13 | **Kommentare hinzufügen** | BED-060–066 (während der Aufnahme), VIE-070–074 (im Viewer), KI-010 | Bedienablauf „kommentieren“ |
| V-14 | **Bilder schnell und einfach kopieren** | VIE-090–105 | T-11 (Kopierzeit), Bedienablauf „Bild kopieren“ |
| V-15 | **Weitere Quality-of-Life-Funktionen** | VIE-110–125, BED-100–112 | Bedienabläufe in 07/08 |
| V-16 | **Programm nie im Screenshot**, kompletter eigentlicher Inhalt sichtbar | NFA-009, AUF-061–066, BED-080–086 | T-05 |
| V-17 | **Aufnahmebereich wählbar:** nur eine Anwendung und/oder ganzer Bildschirm | AUF-053–060, BED-010–023; Darstellung nachträglich wählbar VIE-022, VIE-023, DAT-035, EXP-022 | T-06 |
| V-18 | **Flexibel und trotzdem einfach** | NFA-013, BED-001–009 (Start in zwei Klicks), Leitprinzip 6 in [00](00-ueberblick.md#7-leitprinzipien) | Kernabläufe |
| V-19 | Erkannte Passwortfelder immer maskieren; nicht erkennbare mit Schutznetz (Entscheidung) | NFA-010, DS-007–026 | T-07, DT-Prüfungen |
| V-20 | Technologie C# / .NET + WPF | ARC-025, [02 §9](02-architektur.md) | Build |
| V-21 | Öffentliches Projekt unter CC BY-ND 4.0 (siehe LICENSE) | NFA-017, NFA-019, NFA-020, [11 §10](11-roadmap-tests-risiken.md#10-veröffentlichung) | Release-Checkliste |
| V-22 | Kein KI-Aufruf im Tool, vollständig offline | NFA-011, ARC-029, DS-042 | T-12 |

**Freigegebene Zusatzfunktionen** (vom Auftraggeber aus den Verbesserungsvorschlägen übernommen; alle Soll):

| Nr. | Vorgabe | Anforderungen (Auswahl, maßgeblich) | Abnahme |
|---|---|---|---|
| V-23 | **Ziel der Aufnahme** und **Marker mit Typ** (Fehler, Erwartet, Wichtig, Notiz) | AUF-099–101, BED-130–134, VIE-160–163, EXP-161, EXP-162 | T-09 (Ziel und Marker im KI-Paket) |
| V-24 | **Zwei Aufnahmen vergleichen** (Viewer, Kommandozeile, MCP) | ARC-056–058, VIE-170–174; Schrittabgleich in [05](05-schritterkennung.md); MCP-Tool `compare_sessions` in [06](06-ki-export-und-mcp.md) | T-14 |
| V-25 | **Abschnitt neu aufnehmen und ersetzen** | AUF-102–109, VIE-180–182, EXP-160 | Bedienablauf in 08 |
| V-26 | **Persönliche Werte verallgemeinern** | ARC-052, VIE-190–194, EXP-150–153; Regeln in [10](10-datenschutz-und-sicherheit.md) | T-15 |
| V-27 | **Qualitätsprüfung vor dem Export** | VIE-200–202, EXP-140–143 (Regelkatalog Q01–Q20) | Golden-Sitzungen mit bekannten Mängeln |
| V-28 | **Aufnahmeprofile** | ARC-062, ARC-063, AUF-097, AUF-098, BED-140–145, EXP-180 | T-17 |
| V-29 | **Support-Paket** | ARC-059–061, BED-150–152, VIE-210–213, EXP-170–174 | T-16 |
| V-30 | **Volltextsuche über alle Sitzungen** | ARC-053–055, VIE-220–223; MCP `search` mit `scope: all` | T-15 (keine geschützten Inhalte im Index) |
| V-31 | **Einstellungen, Vorlagen und Profile austauschen** | ARC-064, ARC-065, BED-160–163 | T-17 |

**Umgesetzte Ausbaupakete** (Auswahl; Auftraggeber hat alle acht Pakete freigegeben, die Funktionen sind gebaut – Stand siehe [11 §2](11-roadmap-tests-risiken.md#2-meilensteine)):

| Nr. | Vorgabe | Anforderungen (Auswahl, maßgeblich) | Abnahme |
|---|---|---|---|
| V-32 | **Alltagskomfort:** Exportdialog mit Zielwahl, Fortschritt und Abbruch; Papierkorb; Fenster und letzten Schritt merken; Tastenkürzel einstellen; Befehlstabelle und F1-Übersicht; Suche im Editor | ARC-069, ARC-071, ARC-073; [08](08-viewer.md), [09](09-anleitungs-export.md) | ComfortTests |
| V-33 | **Aufnahme-Bibliothek:** alle Aufnahmen finden und durchsuchen, `.steprec` packen und entpacken, bereinigte Kopie | ARC-072; [08](08-viewer.md) | LibraryTests, SanitizerTests |
| V-34 | **Bessere Anleitungen:** Zeichenwerkzeuge, Schritte zusammenfassen, Qualitätsprüfung Q01–Q20 (`check`), Bearbeitungsstände, Arten der Ausgabe | SCH-062, SCH-064, ARC-075; [08](08-viewer.md), [09](09-anleitungs-export.md) | EditorFeatureTests, GuideFeatureTests, EditHistoryTests |
| V-35 | **Neue Ausgabeformate:** Word mit Inhaltsverzeichnis, PDF mit Verweisen und Unicode, PowerPoint, Klick-Tour, Fehlerbericht | [09](09-anleitungs-export.md) | ExportFormatsTests, PdfTests |
| V-36 | **Aufnahme und Datenschutz:** UAC und sicherer Desktop als Schritt, Erinnerungen, letzter Aufnahmebereich; Terminal-Eingaben prüfpflichtig, „Streng“ maskiert Terminal, Remote-Desktop und VM | SCH-049, SCH-063; [03](03-aufzeichnungskern.md), [07](07-aufnahme-bedienung.md), [10](10-datenschutz-und-sicherheit.md) | SecureDesktopTests, ReminderClockTests, RegionMemoryTests, ProtectionFixTests |
| V-37 | **KI-Anbindung:** Tokenschätzung im KI-Dialog, „Mit KI verbinden“, Agent bittet um Vorführung (nur mit `--allow-requests`) | [06](06-ki-export-und-mcp.md) | KiEstimateTests, RecordingRequestsTests |
| V-38 | **Englische Oberfläche** mit Prüftest gegen unübersetzte Texte | NFA-016, ARC-066 | LocalizationTests |
| V-39 | **Fundament:** Programmprotokoll, Diagnose-Paket, robuste Einstellungen, vollständige Kommandozeile, automatischer Build | ARC-067, ARC-068, ARC-070, ARC-074, ARC-076 | FoundationTests, SettingsStateTests |

Datenformat, Schrittlogik, KI-Paket und Datenschutzregeln dieser Funktionen stehen in DAT-064–073, SCH-052–060, KI-034–041, MCP-032–036 und DS-056–067.

**Entwickler-Fahrplan** (vom Auftraggeber am 2026-11-12 gewählt; geplant, nicht umgesetzt – Pakete, Reihenfolge und Schutzregeln in [12](12-entwickler-fahrplan.md)). Anforderungen mit Kennung entstehen bei der Feinplanung des jeweiligen Pakets; bis dahin verweist die Spalte auf die Pakete.

| Nr. | Vorgabe | Anforderungen (Auswahl, maßgeblich) | Abnahme |
|---|---|---|---|
| V-40 | **KI-Agent-Schleife:** der ganze Fehlerkontext für Agenten in einem Aufruf, MCP-Prompts, Notizen des Agenten, Prüfaufnahme; Kommandozeile mit `list`, `latest` und JSON | noch keine – Paket 8 | wie in [12, Paket 8](12-entwickler-fahrplan.md#paket-8--agenten-schleife-m) |
| V-41 | **Entwickler-Kontext:** festhalten, was die Anwendung tat – Programmende und Absturz, Hänger, Build-Stand, Fehlertext, Einträge der Ereignisanzeige, Protokolldateien | noch keine – Pakete 10–13 | Kanarien-Abnahme über alle Ausgabewege (Paket 12) |
| V-42 | **UI-Tests aus Aufnahmen:** Testentwürfe für FlaUI, pywinauto und Playwright; Testbarkeits-Bericht | noch keine – Pakete 16, 17 | erzeugter Test läuft gegen die Test-Zielanwendung (VE-21) |
| V-43 | **Tempo im Alltag:** Bereitschaft im Infobereich, Modus „Fehler nachstellen“, Befehlspalette, Sofort-Bildschirmfoto | noch keine – Pakete 14, 15 | Tastaturdurchgang, Startzeit aus der Bereitschaft |
| V-44 | **Doku und Reviews im Repository:** Markdown nach 09 §6, GIF für Pull Requests, visueller Vorher/Nachher-Vergleich | noch keine – Pakete 18, 19 | bytegleiche Exporte, T-14 mit Bildunterschieden |
| V-45 | **Reaktionszeiten sichtbar machen** | noch keine – Paket 9 | Zahlen in Editor, `trace.md`, MCP und Vergleich stimmen überein |

## 5. Rückverfolgung der Review-Befunde

Das unabhängige Architektur-Review zum Planentwurf ergab 14 Befunde und 5 fehlende Themen. Die Fachkapitel führen eigene Zuordnungstabellen ([02 §15](02-architektur.md), [03 §13](03-aufzeichnungskern.md)); hier die Gesamtsicht.

| Nr. | Befund | Gelöst in |
|---|---|---|
| R1 | Passwort-Rückhaltung gegen veralteten Fokus | DS-010–013 (Fenster-Stempel, Freigaberegel), AUF-089 (Andockpunkte) |
| R2 | Passwörter ohne Passwortkennzeichen (Terminal, RDP, Auto-Type, „anzeigen“) | DS-016, DS-019, DS-022–024, SCH-049; Entscheidung „Schutznetz“ |
| R3 | Schwärzung nur im Overlay, Rohdaten lesbar | ARC-011, DS-032–036, DAT-031–034, DAT-054 |
| R4 | Hook-Wächter erkennt echte Verluste nicht | AUF-013–017, AUF-019 (Neuabgleich), `keystate.sync` |
| R5 | Vollständigkeitsprüfung belegt nur Buchführung | SCH-027, SCH-028, SCH-035, KI-009 |
| R6 | Elementidentität in Grenzfällen | AUF-068, SCH-024, SCH-047 |
| R7 | KI weiß nicht, was auf dem Bildschirm stand | AUF-070, AUF-071, AUF-075, KI-011 |
| R8 | Netzwerkfreiheit vs. Browser-Komponente, Injektion | ARC-027 (WebView2 ausgeschlossen), KI-020, EXP-090–096, DS-042 |
| R9 | Speicherbereinigung im Hook-Prozess | ARC-002–004, AUF-012 |
| R10 | Bildregeln unter Last, Neuberechnung ohne Bilder | AUF-033–035, AUF-039, AUF-049, SCH-003 |
| R11 | Zeitsemantik des Protokolls | AUF-005, AUF-007, DAT-008 |
| R12 | Koordinaten bei Topologiewechsel | AUF-050, AUF-051, DAT-009, DAT-010 |
| R13 | Undurchsichtige Oberflächen | AUF-074, SCH-023 |
| R14 | Verifikation nur am einfachen Fall | [11 §4–7](11-roadmap-tests-risiken.md#4-teststrategie), DT-Prüfungen, Beispiele in [04](04-datenformat.md) und [06](06-ki-export-und-mcp.md) |
| F1 | Absturzsicherheit und Formatentwicklung | NFA-008, NFA-017 |
| F2 | Eine EXE als GUI- und Konsolenprogramm | ARC-019–022, VE-07 |
| F3 | Eigene Bedienung des Recorders | AUF-027, AUF-028 |
| F4 | Erfassungsumfang (Toasts, Umschalter, zweiter Monitor, Sync-Ordner) | AUF-054, AUF-060, DS-030, DS-044 |
| F5 | Anzeige- und Eingabeeinstellungen (HDR, Maustasten) | AUF-029, AUF-046, VE-06 |

## 6. Entscheidungsprotokoll

Beim Abgleich der parallel geschriebenen Kapitel getroffene Festlegungen. Sie gelten kapitelübergreifend.

| Nr. | Festlegung | Begründung |
|---|---|---|
| E1 | Standard-Aufnahmeordner: `Sessions` neben der EXE, falls beschreibbar und nicht in einem Sync-Ordner; sonst `%LOCALAPPDATA%\StepRecorder\Sessions`. Nie „Dokumente“. | Portabel; „Dokumente“ wird oft von OneDrive umgeleitet |
| E2 | `export` hat zusätzlich die Exit-Codes 8 (Ziel nicht beschreibbar) und 9 (Abbruch durch `--strict`). | Skripte können Fehlerarten unterscheiden |
| E3 | Ungeprüfte auffällige Eingaben erscheinen in MCP und Kommandozeilen-Export nur als `{unreviewed:N}`, bis sie im Viewer durchgesehen sind; keine Option zum Umgehen. | Privatsphäre vor Bequemlichkeit (Leitprinzip 3) |
| E4 | `inSeq` ist Pflicht für alle Eingabeereignisse aus dem Capture-Host; aus Raw Input ergänzte Ereignisse tragen keins. | Lückenerkennung über die Nummernfolge |
| E5 | „Belegt“ ist auch über eine beobachtete Reaktion des Elements (z. B. `ui.invoke`) als zweiten Beleg erreichbar. | Robuster bei schnellen Klicks |
| E6 | Tastenkombinationen und Einzeltasten sind belegt, wenn das Zielfenster zum Hook-Zeitpunkt bekannt ist. | Tasten haben kein Element-Ziel |
| E7 | Lückenmarker `outOfScope` dürfen Ereigniszahlen je Art enthalten, `excludedApp` keine. | Ausgeschlossene Anwendungen sollen keine Spuren hinterlassen |
| E8 | Einheitliche Platzhalter: `[geschwärzt]` / `[redacted]`, „Schritt geschwärzt“ / „Step redacted“; Bilder schwarz gefüllt. | Eindeutig für Mensch und KI |
| E9 | Schwärzungsarten: Bereich im Bild, Textstelle, Schritt, Zeitraum; „Schritt“ wird beim Anlegen auf seinen `seq`-Bereich aufgelöst. | Übersteht Neuberechnung der Schritte |
| E10 | Privattaste ist ein Umschalter ohne automatisches Ende; zusätzlich soll die Terminal-Abfrageerkennung jede erkannte Abfragezeile automatisch bis zur Eingabetaste (bzw. Abbruch der Abfrage) maskieren – *umgesetzt (Regeln T1–T3, 10 §6), im echten Terminal noch nicht bestätigt, siehe E29*. | Auch doppelte Passwortabfragen geschützt |
| E11 | Interne und ausgeblendete Kommentare erscheinen in keiner Ausgabe (Anleitung, KI-Paket, MCP). | Kommentare können Vertrauliches enthalten |
| E12 | Sperren, Benutzerwechsel und Trennen pausieren die Aufnahme; sie läuft erst nach Bestätigung weiter (Lücken `sessionLocked`, dann `paused`). | Nach dem Entsperren kann jemand anderes am Rechner sitzen |
| E13 | Zwischendateien (extern bearbeiten, Herausziehen, Exportvorschau) liegen im Sitzungsordner unter `export/.tmp/`, nie in `%TEMP%`. | Keine Spuren außerhalb des Sitzungsordners |
| E14 | Ein vom Autor geschwärzter Zeitraum erscheint in jeder Ausgabe, auch in Anleitungen, als neutraler Hinweis „vom Autor entfernt“. | Lücken werden nie verschwiegen |
| E15 | In `{unreviewed:N}` zählt N bei getippten Läufen Tastenanschläge, bei Feldinhalten, Zwischenablage und Kommentaren Zeichen. | Einheitliche Bedeutung in allen Kapiteln |
| E16 | Die Durchsicht vor der Weitergabe umfasst auch Kommentare des Autors. | Kommentare sind Freitext und können Sensibles enthalten |
| E17 | Eingaben außerhalb des Aufnahmebereichs werden nur gezählt, nie aufgezeichnet; die Option „mitprotokollieren“ entfällt. | Der Aufnahmebereich ist auch eine Datenschutzgrenze |
| E18 | „Letzte Eingabe schwärzen“ während der Aufnahme legt sofort eine Zeitraum-Schwärzung an und wendet sie beim Stopp endgültig an – ausdrückliche Ausnahme vom Nur-Anhängen-Prinzip. | Ein versehentlich getipptes Passwort soll die Platte nicht im Klartext überdauern |
| E19 | Keine Dateizuordnung für `.steprec` (würde die Registry ändern). Öffnen per Drag & Drop, „Zuletzt verwendet“ und Kommandozeile. | Portabilitätsversprechen ohne Ausnahme |
| E20 | Schutzstufen heißen in Daten und Kommandozeile `balanced` (Anzeige „Schutznetz“) und `strict` (Anzeige „Streng“). | Ein Name je Wert |
| E21 | Das KI-Paket wird per Kommandozeile mit `export <Sitzung> --format ki` erzeugt; die Optionen definiert [06](06-ki-export-und-mcp.md). | Einheitlicher Exportbefehl |
| E22 | Ein Satz Referenzsysteme (A/B/C aus [03 §12.1](03-aufzeichnungskern.md)) für alle Zeitziele; Aufnahmestart vom Auslöser bis zum ersten Ereignis höchstens 1,5 s auf System A. | Vergleichbare, widerspruchsfreie Zielwerte |
| E23 | Exit-Code 7 bedeutet „Voraussetzung nicht erfüllt“ (u. a. nicht unterstütztes System, `record` ohne laufende Instanz mit `--fallback deny`). | Weniger Codes, klare Bedeutung |
| E24 | Registrierte Hotkeys des Recorders erreichen die Zielanwendung nicht (Ausnahme vom Grundsatz „kein Eingriff“); eine Kommentareingabe ohne Fokuswechsel gibt es nicht. | Bedienbarkeit ohne versteckte Eingabeumleitung |
| E25 | Gemeinsame Namen der Zusatzfunktionen: `manifest.goal`/`edits.goal` (`description`, `expected`, `reference`), `marker.kind` (`note`, `error`, `expected`, `important`), `manifest.profile`, `edits.replacements[]`, `edits.substitutions[]`, `edits.qualityIgnores[]`, `edits.guide.intro`, Suchindex `cache/search/`, Befehle `compare`, `support`, `settings export|import`, MCP-Tool `compare_sessions`. | Ein Name je Begriff in allen Kapiteln |
| E26 | Ein verworfener weiterer Aufnahmeabschnitt (z. B. abgebrochene Neuaufnahme) wird markiert und von der Leseschicht aus allen Ausgaben herausgehalten; das Journal wird nie gekürzt. „Aufnahme verwerfen“ betrifft dann nur diesen Abschnitt. Die Ersetzung trägt die Aufzeichnungssteuerung nach sauberem Stopp ein. | Nur-Anhängen-Prinzip bleibt unangetastet |
| E27 | `--profile` meint überall das Aufnahmeprofil; das Bildprofil des KI-Exports heißt `--image-profile`. | Keine Verwechslung in der Kommandozeile |
| E28 | Schutzregeln der Zusatzfunktionen (Profile, Import, Suche, Vergleich, Support-Paket, Verallgemeinerung) gelten als Muss, sobald die Funktion gebaut wird. | Neue Funktionen dürfen den Schutz nie aufweichen |
| E29 | Getipptes in Terminals, Remote-Desktop- und VM-Fenstern ist **prüfpflichtig** (`flags.plaintext`); die Stufe „Streng“ hält dort alles zurück. Die automatische Erkennung von Kennwortabfragen im Terminal (Regeln T1–T3, DS-022/DS-023) ist **gebaut**: Eine erkannte Abfrage maskiert die Eingabe ohne Durchsicht; alles andere Getippte dort bleibt prüfpflichtig. Ob Terminals ihren Text über UI Automation liefern, ist nicht gemessen (O-09). | Ehrlicher Stand: Erkennung als zusätzlicher Schutz, Durchsicht und Stufenwahl bleiben das Netz darunter |
| E30 | Schritte lassen sich nur für **Anleitungen** zusammenfassen (`edits.merges`); `steps.json`, KI-Paket, MCP, Vergleich und AutoHotkey-Export behalten die Einzelschritte. | Die Spur bleibt vollständig, die Anleitung darf kürzer sein |
| E31 | Die Sprache der Oberfläche (`uiLanguage`) ist unabhängig von der Sprache der Anleitungstexte. Texte, die in eine Aufnahme oder einen Export gehen, folgen der Inhaltssprache; das Manifest nennt weiter die Windows-Sprache. | Ein englisch bedienter Rechner erzeugt keine gemischten Anleitungen |
| E32 | Löschen aus der Oberfläche geht nur in den Papierkorb; wo es keinen gibt, fragt das Programm ausdrücklich. Ordner-Exporte leeren nie einen fremden Ordner (Exit-Code 8). | Kein stilles, endgültiges Löschen |

## 7. Pflege

- Die Zahlen in Abschnitt 2 werden per Skript aus den Anforderungstabellen ermittelt (Zeilen, die mit `| PRÄFIX-NNN |` beginnen). Abweichungen zwischen Index und Kapiteln sind ein Fehler im Index.
- Neue Vorgaben des Auftraggebers erhalten eine `V-nn`-Zeile in Abschnitt 4, bevor Anforderungen dazu geschrieben werden.
- Neue kapitelübergreifende Festlegungen werden als `E-nn` in Abschnitt 6 eingetragen und in allen betroffenen Kapiteln umgesetzt.
