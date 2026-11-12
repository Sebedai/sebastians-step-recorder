# 11 – Roadmap, Tests und Risiken

`Status: Entwurf 0.1 · Stand: 2026-11-11`

Dieses Dokument legt fest, **in welcher Reihenfolge** StepRecorder gebaut wird, **wie** jede Stufe abgenommen wird, welche technischen Annahmen **vor** dem Bau durch Experimente geklärt werden, wie getestet wird und welche Risiken und offenen Entscheidungen bestehen.

## Inhalt

1. [Vorgehen](#1-vorgehen)
2. [Meilensteine](#2-meilensteine)
3. [Vorab-Experimente (M0)](#3-vorab-experimente-m0)
4. [Teststrategie](#4-teststrategie)
5. [Testwerkzeuge](#5-testwerkzeuge)
6. [Abnahmetests der Kernversprechen](#6-abnahmetests-der-kernversprechen)
7. [Testmatrix](#7-testmatrix)
8. [Risiken](#8-risiken)
9. [Offene Entscheidungen](#9-offene-entscheidungen)
10. [Veröffentlichung](#10-veröffentlichung)
11. [Spätere Ausbaustufen](#11-spätere-ausbaustufen)

---

## 1. Vorgehen

- **Erst messen, dann bauen.** Annahmen, die nicht durch Dokumentation belegt sind oder die Architektur tragen, werden in M0 als kleine Wegwerf-Prototypen geprüft. Ergebnis jedes Experiments ist ein kurzer Bericht unter `docs/experimente/` mit Messwerten und der Entscheidung.
- **Von innen nach außen.** Aufzeichnungskern und Datenformat zuerst – sie sind schwer nachträglich zu ändern. Oberfläche danach.
- **Jede Stufe ist benutzbar.** Ab M1 entsteht bei jedem Meilenstein etwas, das sich ausprobieren lässt.
- **Spezifikation lebt mit.** Erkenntnisse aus Experimenten und Umsetzung fließen als neue Version in die betroffenen Kapitel; Änderungen an Formaten erhöhen die Schema-Version ([04](04-datenformat.md)).

## 2. Meilensteine

| Meilenstein | Inhalt | Ergebnis | Abnahmekriterien |
|---|---|---|---|
| **M0 Fundament** | Git-Repo, Solution-Struktur ([02](02-architektur.md)), CI-Build der Single-File-EXE, Lizenzprüfung der Abhängigkeiten; Vorab-Experimente VE-01 bis VE-14 mit Erweiterungen (Abschnitt 3.1) | Risiken gemessen statt vermutet | Alle VE-Berichte liegen vor; jede Muss-Annahme ist bestätigt oder durch einen beschriebenen Ausweichweg ersetzt; CI erzeugt eine startfähige EXE |
| **M1 Aufzeichnungskern** | Capture-Host mit Hooks und Raw Input, Rohprotokoll, Bildspeicher, Aufnahmebereiche, Unsichtbarkeitsgarantie mit Selbsttest, UI-Element-Ermittlung, Fenster- und Systemereignisse, Lückenmarker, Passwort-Rückhaltung | Sitzungsordner per `SebastiansStepRecorder.exe record` | T-01, T-02, T-04 bestanden; T-05 für Rahmen und Selbsttest-Fenster; T-07 für den Sitzungsordner; `validate` meldet für eine 10-Minuten-Testsitzung keine unerklärten Lücken |
| **M2 Schritte, Leseschicht und KI-Paket** | Schritterkennung, Wirkung, Belegstufen, Text-Schnappschüsse, `steps.json`, Leseschicht mit Schwärzung und Durchsichtsregel, `trace.md`, `README_AI.md`, Bildvarianten, Vollständigkeitsbericht, `export <Sitzung> --format ki` | KI kann eine Sitzung nachvollziehen | T-03, T-09 bestanden; T-07 zusätzlich für das KI-Paket; Regressionstests der Schritterkennung grün |
| **M3 Aufnahmeleiste und Viewer** | Startfenster mit Aufnahmeprofilen und Ziel-Feldern, Aufnahmeleiste, Hotkeys, Marker mit Typ, Privattaste; Viewer zum Ansehen, Suchen, Abspielen und Kopieren | Aufnehmen und sofort ansehen | Kernablauf 1 (aufnehmen → ansehen → kopieren) in der festgelegten Höchstzahl an Klicks ([07](07-aufnahme-bedienung.md), [08](08-viewer.md)); T-05 vollständig (Leiste, Dialoge); Viewer-Zeitziele (T-11) |
| **M4 Bearbeiten und erste Exporte** | Bildbearbeitung, Kommentare, Schritte ordnen, Schwärzen, Werte verallgemeinern, Durchsicht, Qualitätsprüfung, Darstellungswahl, Volltextsuche über alle Sitzungen, HTML- und Markdown-Export | Erste fertige Anleitung | Kernablauf 2 bis HTML/Markdown; T-08 und T-15 für Viewer, Anleitungen und KI-Paket |
| **M5 MCP-Server** | Lesende Tools, Paging, Größenbudgets, Bildrückgabe, Suche über alle Sitzungen; optional `set_step_text` | Agent fragt Sitzungen ab | T-10 bestanden; T-07 und T-08 zusätzlich für MCP-Antworten; Beispielkonfiguration für Claude Code funktioniert |
| **M6 Härtung** | Verweilen, Ziehen, Wirkungen, Mehrmonitor/DPI, Topologiewechsel, Adminfenster, Absturzsicherheit, voller Datenträger, Langzeittest | Belastbar im Alltag | Testmatrix (Abschnitt 7) vollständig durchlaufen; T-06, T-12, T-13 bestanden |
| **M7 DOCX, PDF, Englisch, Komfort** | DOCX- und PDF-Export, Vorlagen, englische Oberfläche und Textvorlagen, endgültiges Schwärzen; Aufnahmen vergleichen (Viewer, `compare`, MCP `compare_sessions`), Abschnitt neu aufnehmen und ersetzen, Support-Paket, Einstellungen exportieren/importieren; übrige Komfortfunktionen | Funktionsumfang komplett | T-14, T-16, T-17 bestanden; alle Muss- und Soll-Anforderungen erfüllt oder begründet verschoben |
| **M8 Veröffentlichung** | Lizenz, englische README und Benutzerdoku, Beitragsregeln, Sicherheitsrichtlinie, signierte Releases | Version 1.0 | Signierte EXE startet auf frischem Windows 10 und 11 ohne Virenscanner-Blockade (Defender); Release-Checkliste (Abschnitt 10) erfüllt |

**Reihenfolge-Begründung:** MCP (M5) kommt nach dem Viewer, weil das KI-Paket aus M2 KI-Nutzung bereits ermöglicht und der Viewer für Menschen den größeren Nutzen hat. Die Leseschicht mit Schwärzung und Durchsichtsregel entsteht in M2, weil schon das KI-Paket nur über sie lesen darf; Viewer (M3/M4) und MCP (M5) nutzen sie.

### 2.1 Stand der Umsetzung

Über die Meilensteine hinaus wurde ein Bündel von acht Ausbaupaketen umgesetzt, das Funktionen aus M3, M4, M7 und M8 vorzieht oder ergänzt. Die Tabelle nennt den Stand je Paket; „erledigt“ heißt: gebaut und durch automatische Tests belegt. Die Prüfungen von Hand stehen in Abschnitt [5.2](#52-prüfungen-von-hand).

| Paket | Inhalt | Stand |
|---|---|---|
| 0 Fundament und Schutz-Korrekturen | Zweisprachigkeit (`Loc`) und Prüftest, Programmprotokoll mit Absturzbehandlung, Oberflächenzustand, robuste Einstellungen mit Migration, Tastenkürzel-Tabelle, vollständige und strenge Kommandozeile (`check`, `steps`, `settings export\|import\|reset`, `record --self-test`), Austausch von Einstellungen, Diagnose-Paket, Export-Unterbau, automatischer Build (GitHub); Korrekturen: Ordner-Exporte leeren nie fremde Ordner, `key.up` ungeprüfter Eingaben verrät keine Tasten, „Text überall schwärzen“ wirkt sofort, interne Kommentare, Terminal-Eingaben prüfpflichtig, Blob-Namen geprüft | erledigt |
| 1 Alltagskomfort | Papierkorb, Fensterlage und Spaltenbreiten merken, letzter Schritt, Befehlstabelle mit F1-Übersicht, Exportdialog mit Fortschritt und Ergebnisfenster, Ein-Klick-Wiederholung, einstellbare Tastenkürzel, Suche im Editor | erledigt |
| 2 Aufnahme-Bibliothek | Katalog, Fenster „Alle Aufnahmen“, `.steprec` packen und entpacken, Suche über alle Aufnahmen, bereinigte Kopie, endgültiges Entfernen der Bilder geschwärzter Zeiträume | erledigt |
| 3 Bessere Anleitungen | Zeichenwerkzeuge (Linie, Freihand, Spotlight, Lupe), Schritte zusammenfassen, Qualitätsprüfung Q01–Q20 (Q15 und Q18 teilweise), Bearbeitungsstände | erledigt |
| 4 Neue Ausgabeformate | Word mit Inhaltsverzeichnis, PDF mit Verweisen, Lesezeichen und Unicode, Arten der Ausgabe (Standard, Kurzfassung, Checkliste), PowerPoint, Klick-Tour, Fehlerbericht, GIF | erledigt |
| 5 Aufnahme und Datenschutz | UAC und sicherer Desktop als benannter Schritt, Bildschutz bei Sperre und Ruhezustand, Erinnerungen, letzter Aufnahmebereich | erledigt; **Regel T1** (Kennwortabfragen im Terminal) **offen** |
| 6 KI-Anbindung | Tokenschätzung im KI-Dialog, „Mit KI verbinden“, Agent bittet um Vorführung (`--allow-requests`) | erledigt |
| 7 Englische Oberfläche | alle Fenster und Meldungen zweisprachig, Prüftest ohne Ausnahmeliste | erledigt |

**Offen bleiben:**

- **.NET 10.** Gebaut wird mit .NET 8; das .NET-10-SDK fehlt auf dem Entwicklungsrechner (Abschnitt [9](#9-offene-entscheidungen), O-08).
- **Regel T1.** Die Erkennung von Kennwortabfragen im Terminal (DS-022, DS-023) ist nicht umgesetzt. Es gilt: Getipptes in Terminal, Remote-Desktop und VM ist prüfpflichtig, „Streng“ maskiert dort alles (O-09).
- **Code-Signierung.** Nicht umgesetzt; die Lizenzfrage ist offen (O-10).
- **Zurückgestellt:** Abschnitt neu aufnehmen und ersetzen (damit Aufnahmeabschnitte, Ersetzungen und Nahtstellen aus [05, Abschnitt 16](05-schritterkennung.md)), Lesezeichen und Textbausteine, Seitenformat (Letter, quer) und eigene Word-Vorlage, weitere Sprachen der automatischen Texte, Video-Export, ARM64, Bedienhilfen der Oberfläche (Namen für Sprachausgabe), Terminal-Regeln T2 und T3 (O-11).
- **Vorab-Experimente VE-01 bis VE-14** und die Berichte unter `docs/experimente/` liegen nicht vor; die Messkriterien aus Abschnitt 3 sind damit nicht belegt.

**Weiterer Ausbau für Software-Entwickler.** Der Fahrplan in [12](12-entwickler-fahrplan.md) plant die Pakete 8–19 (Vorgaben V-40 bis V-45): Agenten-Schleife, Reaktionszeiten, Programm-Kontext (Programmende, Fehlertext, Ereignisanzeige, Protokolldateien), Bereitschaft im Infobereich, Befehlspalette, UI-Tests aus Aufnahmen, Doku im Repository und visueller Vergleich. Er nennt außerdem Abweichungen zwischen Grundsätzen und Code (A-1 bis A-10), die vor den Paketen mit neuen Quellen zu klären sind; die Fragen dazu stehen als O-12 bis O-19 in Abschnitt [9](#9-offene-entscheidungen), die Experimente als VE-15 bis VE-23 in Abschnitt [3.2](#32-experimente-für-den-entwickler-fahrplan). Stand aller Pakete: geplant.

## 3. Vorab-Experimente (M0)

Jedes Experiment ist ein Wegwerf-Prototyp. Kriterium nicht erfüllt → Ausweichweg wird Teil der Spezifikation.

| ID | Frage | Aufbau | Erfolgskriterium | Ausweichweg |
|---|---|---|---|---|
| **VE-01** | Hält der Capture-Host das Hook-Zeitbudget? | Minimalprozess mit LL-Hooks, Ringpuffer im gemeinsamen Speicher; Lastgenerator (1000-Hz-Maus, 20 Tasten/s) bei paralleler 4K-PNG-Kodierung im Hauptprozess | p99,9 der Callback-Dauer < 1 ms, kein Hook-Verlust in 30 min | Hook-Thread mit erhöhter Priorität; Raw Input als Primärkanal für Maus |
| **VE-02** | Was liefert Raw Input, wenn ein Fenster mit Adminrechten im Vordergrund ist, und bei Tasten, die ein anderer Hook verschluckt (AutoHotkey, mstsc)? | Normaler Prozess mit LL-Hook + Raw Input (`RIDEV_INPUTSINK`); erhöhtes Testfenster; AutoHotkey-Skript, das Tasten schluckt | Verhalten dokumentiert; Abgleichslogik entsprechend festgelegt | Lückenmarker „Adminfenster“ bzw. „Eingabe abgefangen“ ohne Ergänzung |
| **VE-03** | Ist das Vorher-Bild garantiert vor der Eingabe? | Testanwendung, die bei jeder Eingabe sofort einen Zähler umschaltet; 1000 Klicks per Hardware-nahem Eingabegerät oder SendInput | In 1000 von 1000 Fällen zeigt das Vorher-Bild den alten Zähler | Größerer Ringpuffer, Bild im Callback festhalten (Frame-ID pinnen) |
| **VE-04** | Wie gut trifft die Element-Ermittlung? | Verweilen + Maustaste in: Win32-Dialog, WPF, WinForms, Chrome/Edge, Firefox, Electron (VS Code, Teams), Java Swing, Qt, Office | Trefferquote je Anwendungsart gemessen; mittlere und p95-Dauer; Hänger > Zeitlimit gezählt | Rückfallkette MSAA/Win32, Einstufung „undurchsichtig“ |
| **VE-05** | Fehlen die eigenen Fenster auf allen Aufnahmewegen? | `WDA_EXCLUDEFROMCAPTURE` auf Leiste, Rahmen, Dialog, Tooltip; Aufnahme per Desktop Duplication, Windows.Graphics.Capture, GDI; Testmuster dahinter | Pixelgleichheit mit Referenz auf allen drei Wegen; Verhalten auf Windows 10 22H2 und 11 | Ausweichmodus (Fenster für den Moment des Bildes verbergen) |
| **VE-06** | Stimmen Koordinaten bei gemischter Skalierung? | Zwei Monitore 100 % und 150 %, Hochformat-Monitor; Prozess Per-Monitor-V2; Klicks auf bekannte Punkte; DPI-unaware Testanwendung | Hook-, Raw-Input-, UIA- und Bildkoordinaten auf ±1 px konsistent | Umrechnungstabelle je Quelle; Plausibilitätsprüfung der Elementrechtecke |
| **VE-07** | Funktioniert eine EXE als Oberfläche **und** als MCP-Server über stdio? | Single-File-EXE (WinExe-Subsystem) mit `mcp`-Modus; Start durch Claude Code; Prüfung, dass stdout nur Protokollnachrichten enthält | Claude Code verbindet sich und ruft ein Test-Tool auf; keine Konsolenfenster beim GUI-Start | Zweites, kleines Konsolen-Startprogramm im ZIP-Release |
| **VE-08** | Funktioniert die Passwort-Rückhaltung ohne spürbare Verzögerung? | Tab in ein Passwortfeld und sofort tippen (Win32, WPF, Chrome, Firefox, Electron); Felder mit „Passwort anzeigen“ | 0 Klartext-Zeichen aus Passwortfeldern im Sitzungsordner; keine spürbare Eingabeverzögerung (Rückhaltung betrifft nur die Aufzeichnung, nie die Eingabe selbst) | Längeres Rückhaltefenster; im Zweifel maskieren |
| **VE-09** | Wie groß und wie schnell ist der Bildspeicher? | 30 min typische Büroarbeit auf 2× 4K; Schlüssel-/Differenzbilder; PNG-Kodierzeit | Speicherbedarf und Kodierzeit gemessen; Budgets in [03](03-aufzeichnungskern.md) bestätigt oder angepasst | Andere Kompressionsstufe, Kodierung zeitversetzt, gröbere Kacheln |
| **VE-10** | Laufen Desktop Duplication und Rückfallwege auf Problemsystemen? | Laptop mit Hybrid-Grafik, HDR-Monitor, RDP-Sitzung, Citrix/VM-Fenster | Für jedes System ist der funktionierende Aufnahmeweg bekannt | Automatische Rückfallkette mit Lückenmarker |
| **VE-11** | Wie groß ist die EXE, und wie schnell startet sie? | Release-Build Single-File, mit und ohne Kompression | Größe und Kaltstartzeit gemessen; Start < 2 s auf System A ([03 §12.1](03-aufzeichnungskern.md)) | ZIP-Variante als Hauptauslieferung |
| **VE-12** | Wie reagieren Virenscanner auf die unsignierte EXE? | Upload eines Test-Builds an Defender (lokal) und Mehrfachscanner; Prüfung durch Smart App Control; Vergleich Hooks im Kindprozess vs. im Hauptprozess | Erkennungen dokumentiert | Frühzeitige Signierung, Meldung als Fehlalarm, Architekturhinweise in README |
| **VE-13** | Liefern Hook und Raw Input bei Touch, Stift und Touchpad-Gesten verwertbare Ereignisse, und wird der Hook vor der Anwendung aufgerufen? | Touch-Laptop, Stift, Präzisions-Touchpad mit Wisch- und Zoomgesten gegen die Test-Zielanwendung | Verhalten je Gerät dokumentiert; Garantiestufe je Gerät festgelegt | Garantiestufe `atInput`, Lücke `unsupportedInput` |
| **VE-14** | Kleinere Kernfragen der Eingabe und Zwischenablage: Feststeller ohne Tastaturfokus lesen, Tottasten-Zusammensetzung gegen echte Layouts, Schlafzeit im Leistungszähler, Größe je Zwischenablage-Format ohne Abruf des Inhalts | Deutsch-, Französisch-, US-International-Layout; Ruhezustand während der Aufnahme; Kopieren aus Office, Browser, Explorer | Ergebnis je Frage dokumentiert | siehe [03 §15.2](03-aufzeichnungskern.md) Z-1, Z-5, Z-10, Z-14 |

### 3.1 Erweiterungen der Experimente

Fragen aus [02 §17](02-architektur.md) und [03 §15.2](03-aufzeichnungskern.md), die an bestehende Experimente angehängt werden:

| Experiment | Zusätzlich zu klären | Herkunft |
|---|---|---|
| VE-01 | Lesen von Fenster unter dem Zeiger und oberstem Fenster im Hook-Aufruf innerhalb des Budgets; tatsächliches Zeitlimit und Zeitpunkt der Hook-Entfernung; Verhalten im Akkubetrieb | Z-7, Z-13, O-02-1 |
| VE-02 | Kommt „Desktop gewechselt“ bei UAC und Sperre? Kommen WinEvents für Fenster mit Adminrechten an? Enthält Raw Input künstlich erzeugte Eingaben? | Z-4, Z-11 |
| VE-04 | Wann aktivieren Chromium, Electron und Firefox ihre Barrierefreiheit; reicht das Aufwärmen? | Z-8 |
| VE-05 | Wirkt die Ausnahme auch für durchsichtige Fenster, Tooltips und Popups? Windows.Graphics.Capture: Zeitstempel, gelber Rahmen, Nutzung ohne Paketidentität | Z-3, Z-6 |
| VE-06 | Melden Hook und Raw Input bei vertauschten Maustasten die physische oder die logische Taste? | Z-2, offener Punkt aus [04](04-datenformat.md) |
| VE-07 | Genügt das Kernpaket des MCP-SDK ohne HTTP-Transport? Fängt das Umbiegen des Standardausgabe-Handles Ausgaben nativer Bibliotheken ab? | O-02-5, O-02-6 |
| VE-10 | Abweichung der 8-Bit-Wandlung auf HDR-Monitoren vom Gesehenen | Z-12 |
| VE-11 | Größenzuwachs durch die WinRT-Projektion für Windows.Graphics.Capture | O-02-4 |

### 3.2 Experimente für den Entwickler-Fahrplan

Vor den Paketen aus [12](12-entwickler-fahrplan.md); jedes Experiment läuft vor dem Paket, das es nennt. Die Erkennung von Terminaltext bleibt O-09.

| ID | Frage | Aufbau | Erfolgskriterium | Ausweichweg | Paket |
|---|---|---|---|---|---|
| **VE-15** | Liefern `OpenProcess` mit eingeschränkten Rechten, `GetExitCodeProcess` und `IsHungAppWindow` verwertbare Angaben ohne Adminrechte? | Test-Zielanwendung normal, mit Adminrechten, als anderer Benutzer, als UWP-App; Ende durch Absturz (`0xC0000005`, .NET-Ausnahme), `exit(1)`, Task-Manager; künstlicher Hänger | Für jede Art ist bekannt, ob Ende, Code und Hänger ankommen; normale und eigene Adminprogramme liefern Code und Zeitpunkt | Lücke „Programmende nicht beobachtbar“ statt Vermutung; Ereignisanzeige als zweite Quelle | 10 |
| **VE-16** | Wann und wie erscheinen Ereignisse 1000, 1001, 1002 und 1026 im Anwendungsprotokoll, und lassen sie sich als Standardbenutzer lesen? | Abstürze wie in VE-15 auf Windows 10 22H2 und 11; Abfrage über `System.Diagnostics.Eventing.Reader` | Verzögerung nach dem Absturz gemessen (p95); Feldaufbau je Ereignis dokumentiert; Lesen ohne Adminrechte bestätigt | Abfrage zusätzlich beim Öffnen der Aufnahme, als nachträglich gekennzeichnet; Fakten nur aus dem Programmende | 10, 12 |
| **VE-17** | Wie lange dauert ein Text-Schnappschuss, was deckt er ab, und verzögert er die Elementermittlung bei Klicks? | MessageBox, TaskDialog, WPF, WinForms, WinUI 3, Electron, Qt; 30 Klicks je Sekunde bei gleichzeitigen Schnappschüssen | p95 unter 250 ms; Meldungstext in jeder Art enthalten; p95 der Elementermittlung um höchstens 20 % schlechter | eigener UIA-Arbeiter für Schnappschüsse; Abdeckung je Art dokumentiert | 11 |
| **VE-18** | Blockiert das Lesen des Zwischenablage-Inhalts, wenn die Quelle verzögert liefert? | Testprogramm mit verzögertem Bereitstellen; Office, Browser, Remote-Desktop | Lesen endet innerhalb der Frist ohne Wirkung auf die Quelle | nur Formate ohne verzögertes Bereitstellen lesen; sonst `withheld: "delayed"` | 12 |
| **VE-19** | Wie genau sind die Reaktionszeiten (`effect.latencyUs`, `sync.*`) gegenüber der wirklichen Reaktion? | Test-Zielanwendung mit einstellbarer Verzögerung 50 ms bis 10 s nach Klick | Abweichung höchstens ±1 Bildintervall bzw. ±50 ms; Werte jenseits des Beobachtungsfensters als „länger als“ | Anzeige nur in groben Stufen | 9 |
| **VE-20** | Was kostet die Bereitschaft im Infobereich? | Programm eine Stunde in Bereitschaft, danach Sofortaufnahme | Arbeitsspeicher und Prozessorzeit gemessen und dokumentiert; Start aus der Bereitschaft höchstens 1,5 s; kein Netzwerkzugriff | Bereitschaft nur als schlanke zweite Rolle der EXE | 14 |
| **VE-21** | Läuft ein erzeugter FlaUI-Test gegen die Test-Zielanwendung, und schlägt er nach einer Änderung an der richtigen Stelle fehl? | Vorführung in der Test-Zielanwendung, Export `flaui`, Übersetzen und Ausführen; danach geänderte AutomationId und geänderter Text | grün ohne Nacharbeit; nach der Änderung rot an der betroffenen Prüfung | mehr Notbehelfe als Kommentar; Abnahme über eine Prüfliste von Hand (O-18) | 16 |
| **VE-22** | Wie lassen sich UIA-Typ und Name in Chrome, Edge und Firefox auf Playwright-Rollen und zugängliche Namen abbilden? | Lokale Testseite mit gängigen Bedienelementen (`localhost`) | Zuordnungstabelle; Trefferquote je Rolle gemessen | Entwurf mit `getByText` und Kommentar statt Rolle | 17 |
| **VE-23** | Wie zuverlässig kennzeichnen Produktversion (Commit-Anhang), PE-Zeitstempel und PDB-Kennung einen Build? | Builds mit .NET 8 SDK, MSBuild klassisch, C++; reproduzierbare und nicht reproduzierbare Builds | Je Bauart ist bekannt, welche Angabe den Build eindeutig kennzeichnet | nur Version und Dateizeit, als „nicht eindeutig“ gekennzeichnet | 10 |

## 4. Teststrategie

| Ebene | Gegenstand | Werkzeug | Wann |
|---|---|---|---|
| Einheitstests | Schritterkennung, Textvorlagen, Belegstufen, Datenformat (Lesen/Schreiben/Migration), Leseschicht/Schwärzung, Token-Abschätzung | xUnit | jeder Build |
| **Golden-Journal-Regression** | Gespeicherte Rohprotokolle realer und künstlicher Sitzungen → `steps.json` und `trace.md` werden mit erwarteter Ausgabe verglichen | xUnit + Vergleichsdateien | jeder Build |
| Integrationstests | Aufzeichnungskern gegen die Test-Zielanwendung mit geskripteten Eingaben | eigener Test-Treiber | nächtlich, vor jedem Release |
| Systemtests | Abnahmetests T-01 bis T-17 auf echten Rechnern und VMs | Checkliste + Skripte | je Meilenstein |
| Datenschutzprüfungen | Alle DT-Prüfungen aus [10 §17](10-datenschutz-und-sicherheit.md) (Maskierung, Terminal-Abfragen, Ausschlüsse, Schwärzung, Durchsicht, Netzwerk, Speicherspuren) | Checkliste + Kanarien-Werte | M1 (Maskierung), M4 (Schwärzung, Durchsicht), M6 vollständig, vor jedem Release |
| Formatprüfungen | `validate` mit den Prüfungen F01–F18 ([04](04-datenformat.md)) und S01–S13 ([05](05-schritterkennung.md)) auf jeder Testsitzung | eingebaut | jeder Integrationslauf |
| Explorative Tests | Reale Anwendungen, ungewöhnliche Eingabegeräte, Layouts | Testprotokoll | M6, vor Release |
| Format-Verständnistest | Frischer KI-Agent liest nur `README_AI.md` und Beispiel-Traces | Prüffragen-Katalog | M2, bei Formatänderungen |

**Wichtig:** Geskriptete Eingaben (SendInput) werden vom System als „injiziert“ gekennzeichnet. Tests, die das Verhalten bei echten Eingaben prüfen, verwenden entweder einen Testmodus, der injizierte Eingaben wie echte behandelt (nur im Testbuild), oder ein Hardware-nahes Eingabegerät (z. B. Mikrocontroller als USB-Tastatur/Maus).

## 5. Testwerkzeuge

| Werkzeug | Zweck |
|---|---|
| **Test-Zielanwendung** | Eigenes WPF/Win32-Programm mit bekannten Steuerelementen (Buttons, Textfelder, Passwortfeld, Menüs, Listen, Drag-Ziel, Zeichenfläche). Zeichnet bei jeder Eingabe einen sichtbaren Zähler und protokolliert intern, was es empfangen hat – Grundlage für exakte Soll/Ist-Vergleiche. |
| **Testmuster-Bild** | Vollbild mit eindeutigem Muster (Koordinatengitter, Farbcodes) für Unsichtbarkeits- und Koordinatentests |
| **Eingabe-Skripter** | Erzeugt reproduzierbare Eingabefolgen inklusive Timing (Doppelklick-Grenzen, Tippgeschwindigkeit, gehaltene Tasten) |
| **Kanarien-Passwort** | Eindeutige Zeichenfolge, die nur in Passwortfelder getippt wird; danach wird der gesamte Sitzungsordner inklusive Bilder-Metadaten und Exporte danach durchsucht |
| **Netzwerk-Mitschnitt** | Windows-Firewall-Protokoll bzw. ETW-Netzwerkereignisse für den Prozess und alle Kindprozesse |
| **System-Diff** | Vergleich von Dateisystem (außer Sitzungs- und Einstellungsordner) und Registry vor und nach einem Lauf |

### 5.1 Automatische Tests im Projekt

Die Tests liegen in `tests/StepRecorder.Tests` (xUnit) und laufen bei jedem Build ohne Oberfläche. Der Integrationstest `RecorderIntegrationTests` braucht einen interaktiven Desktop mit echten Hooks; er ist als `Category=Integration` gekennzeichnet, läuft nicht im automatischen Build (`--filter "Category!=Integration"`) und wird von Hand gestartet. Das Hilfsmittel `SyntheticSession` baut künstliche Aufnahmen (mit Kommentar, Terminal-Eingabe, UAC-Lücke, Kanarienwerten), `DevDemoSession` die Demo-Aufnahme für Oberflächen-Schnappschüsse.

| Testdatei | Gegenstand | Bezug |
|---|---|---|
| `StepBuilderTests` | Buchführung (jedes Eingabeereignis genau einem Schritt), Schrittarten, Textvorlagen, Schwärzung und Bildzuschnitt | T-02, T-03, T-08 |
| `AhkContextTests`, `ExportAndMcpTests`, `GifWriterTests`, `ManualTests` | AutoHotkey-Auszüge, KI-Paket und MCP-Werkzeuge, GIF, Detailgrade und Anleitungsformate | T-09, T-10 |
| `EditorFeatureTests`, `GuideFeatureTests` | eigene Schritte, Zuschnitt, Texterkennung, Zeichenwerkzeuge und Geometrie, Zusammenfassen, Reihenfolge, Qualitätsprüfung, Arten der Ausgabe | – |
| `ProtectionFixTests` | Ordner-Exporte löschen nichts Fremdes, `key.up` verrät keine Tasten, Textschwärzung ohne endgültiges Schwärzen, interne Kommentare, Terminal-Eingabe prüfpflichtig, Blob-Pfade | T-07, T-08 |
| `FoundationTests`, `SettingsStateTests` | Austausch von Einstellungen (Einzelbestätigung, Rücknahme, Zurücksetzen), Export-Unterbau (Abbruch, Zielordner), Diagnose, Migration, tolerantes Lesen, Zustand, Tastenkürzel-Regeln, Protokoll | T-17 |
| `ComfortTests` | Fensterlage bei Monitor-Änderungen, Suche im Editor, Reihenfolge der Schrittliste | – |
| `LibraryTests` | Katalog, Packen und Entpacken mit Grenzen, Suchindex ohne geschützte Werte, wörtliche Verallgemeinerung | T-15 |
| `SanitizerTests` | bereinigte Kopie (keine Kanarienwerte, schwarze Bildstellen, Validator fehlerfrei, Original unverändert), endgültiges Entfernen der Bilder | T-08, T-16 |
| `EditHistoryTests` | Intervall, Deduplizierung, Obergrenzen, Schutz beim Wiederherstellen, Revision, Löschen | – |
| `ExportFormatsTests`, `PdfTests` | PPTX-Paket (bytegleich bei zwei Exporten), Word-Inhaltsverzeichnis, Klick-Tour, Fehlerbericht; PDF-Prüfer, Unicode-Einbettung, Teilmengen, Verweise und Lesezeichen | – |
| `SecureDesktopTests` | Zustandslogik des Desktop-Wechsels, UAC-Lücke als benannter Schritt ohne Bild | AUF-044 |
| `RegionMemoryTests`, `ReminderClockTests` | letzter Aufnahmebereich (genau, verschoben, beschnitten, verloren), Erinnerungen (Leerlauf, Privattaste, Ruhezustand) | – |
| `KiEstimateTests` | Bildgröße und Tokenschätzung entsprechen dem Export | – |
| `RecordingRequestsTests` | Bereinigung, Zustandsübergänge, Frist, Missbrauchsschutz, MCP-Werkzeuge nur mit Schalter | – |
| `LocalizationTests` | keine unübersetzten Texte, englischer Text und gleiche Platzhalter zu jedem Aufruf | – |

### 5.2 Prüfungen von Hand

Diese Prüfungen lassen sich nicht automatisieren und stehen vor einer Veröffentlichung aus:

- PowerPoint und Word öffnen die erzeugten Dateien ohne Rückfrage und ohne Reparaturhinweis; das Word-Inhaltsverzeichnis lässt sich mit „Felder aktualisieren“ vervollständigen.
- Zweiter Monitor mit anderer Skalierung: Fensterlage, Bereichsauswahl und „Letzter Bereich“ stimmen.
- Programm und Aufnahmen auf einem USB-Stick (portabler Betrieb, Papierkorb-Rückfrage).
- UAC-Abfrage („Ja“ und „Nein“), Win+L und Strg+Alt+Entf während der Aufnahme: Der Schritt erscheint, und unter `frames\` liegen keine schwarzen Bilder.
- Kennwortabfrage im Windows Terminal mit einem Kanarien-Kennwort: Die Eingabe ist prüfpflichtig (Regel T1 fehlt, siehe O-09); danach Aufnahmeordner, Exporte und MCP-Antworten nach dem Kanarienwert durchsuchen.
- Alle Fenster mit `STEPRECORDER_UILANG=en` und `qps` ansehen.

## 6. Abnahmetests der Kernversprechen

| ID | Versprechen | Test | Bestanden, wenn |
|---|---|---|---|
| T-01 | Ohne Installation | Frisches Windows 10 22H2 und 11 als Standardbenutzer; EXE von USB-Stick starten, aufnehmen, exportieren; System-Diff | Läuft ohne Adminrechte; keine Registry-Änderungen; Dateien nur im Sitzungs- und Einstellungsordner (und dokumentiertem Entpackordner) |
| T-02 | Alle Eingaben erfasst | Eingabe-Skripter: 10 000 gemischte Ereignisse (Bewegungen, Klicks, Doppelklicks, Ziehen, Rad, Tippen, Kombinationen, gehaltene Tasten) gegen Test-Zielanwendung | Rohprotokoll enthält jedes Ereignis in richtiger Reihenfolge; Abweichungen nur als benannte Lücke |
| T-03 | Schritte korrekt | Dieselbe Folge → Schritterkennung | Schrittliste entspricht der Erwartungsdatei; jedes Eingabeereignis genau einem Schritt zugeordnet |
| T-04 | Vorher-Bild vor der Eingabe | VE-03 als Dauertest, auch unter Last (Video im Hintergrund) | 1000/1000 |
| T-05 | Programm nie im Bild | Testmuster mit Leiste, Rahmen, Kommentar-Dialog, Tooltip, Kontextmenü des Recorders darüber; alle Aufnahmewege; Windows 10 und 11 | Alle Bilder pixelgleich zur Referenz; kein Taskleisten-/Alt+Tab-Eintrag während der Aufnahme |
| T-06 | Aufnahmebereich „eine Anwendung“ | Arbeit in Zielanwendung und zweiter Anwendung, überlappende Fenster, Dialoge der Zielanwendung | Bilder enthalten nur die Zielanwendung (verdeckte Stellen neutral gekennzeichnet); Arbeit außerhalb nur als Lückenmarker |
| T-07 | Passwörter nie auf der Platte | Kanarien-Passwort in Passwortfelder (alle Anwendungsarten aus VE-08), auch mit „Passwort anzeigen“ und Sofort-Tippen nach Tab | Kanarie nirgends im Sitzungsordner, in Exporten und MCP-Antworten; Passwortfelder in Bildern geschwärzt |
| T-08 | Schwärzung überall | Text in Schritt 5 schwärzen, der in Schritt 4–7 sichtbar ist und als Eingabe vorkommt | Text in keinem Bild, keinem Export, keiner MCP-Antwort (`get_events`, `search` eingeschlossen) |
| T-09 | KI versteht lückenlos | Format-Verständnistest mit Prüffragen; Sitzung mit eingebauten Lücken und einem absichtlichen Widerspruch | Agent gibt Ablauf richtig wieder, benennt alle Lücken als „nicht aufgezeichnet“ statt zu raten, findet den Widerspruch |
| T-10 | MCP-Grenzen | Alle Tools mit großen Sitzungen (2000 Schritte) | Jede Antwort unter dem Standardbudget oder sauber seitenweise; Bilder mit Maßstabsangabe; kein Zugriff außerhalb des Root-Ordners |
| T-11 | Viewer und Start schnell | Sitzungen S/M/L aus [08 §14](08-viewer.md) auf den Referenzsystemen aus [03 §12.1](03-aufzeichnungskern.md); Aufnahmestart per Sofortaufnahme | Alle Zeitziele aus 08 §14 eingehalten (u. a. erste Anzeige < 1 s); Aufnahmestart ≤ 1,5 s auf System A |
| T-12 | Kein Netzwerk | Netzwerk-Mitschnitt über eine komplette Aufnahme, Bearbeitung, alle Exporte, MCP-Betrieb | Keine Verbindung, keine DNS-Anfrage durch Prozess oder Kindprozesse |
| T-13 | Robust | Prozess während der Aufnahme hart beenden; Datenträger volllaufen lassen; 8-Stunden-Aufnahme | Sitzung bis zum letzten Schreibvorgang lesbar; voller Datenträger → Warnung, Pause, Lückenmarker; Speicherverbrauch stabil |
| T-14 | Vergleich zweier Aufnahmen | Zwei Sitzungen desselben Ablaufs mit bekannten Unterschieden (ein zusätzlicher Schritt, ein geändertes Ziel, ein fehlender Dialog, zusätzliche Spurschritte) | Alle bekannten Unterschiede richtig gekennzeichnet, keine falschen; gleiche Eingabe ergibt bytegleiches Ergebnis in Viewer, `compare` und `compare_sessions` |
| T-15 | Verallgemeinerung | Kanarien-Benutzername und -Servername in Fenstertiteln, Pfaden, getipptem Text und Elementnamen; Regeln aus den automatischen Vorschlägen | Kanarienwerte erscheinen in keinem Export, keiner MCP-Antwort, keinem Suchtreffer; Bildstellen mit bekannter Textposition tragen den Platzhalter; übrige Stellen meldet die Qualitätsprüfung |
| T-16 | Support-Paket | Frischer Rechner, Standardbenutzer, EXE aus einem Download; Aufnahme im Support-Modus mit Kanarien-Passwort im Terminal | Paket in der in [08](08-viewer.md) festgelegten Höchstzahl an Klicks; ZIP enthält nur die bereinigte Kopie (HTML + KI-Paket); Durchsicht war Pflicht; kein Netzwerkzugriff |
| T-17 | Profile und Einstellungs-Import | Import einer präparierten Einstellungsdatei, die Schutzstufe senkt, Zwischenablage-Inhalt einschaltet und die Ausschlussliste leert | Jede schwächende Änderung verlangt eine Einzelbestätigung; ohne Bestätigung bleibt der Schutz unverändert; Start mit schwächerem Profil wird in der Startmeldung angezeigt |

## 7. Testmatrix

Jede Zeile wird in M6 mindestens einmal mit T-02, T-03 (verkürzt) und T-05 durchlaufen.

| Dimension | Ausprägungen |
|---|---|
| Betriebssystem | Windows 10 22H2, Windows 11 24H2, Windows 11 aktuell |
| Anwendungsarten | Win32-Dialoge, WinForms, WPF, WinUI 3, Chrome, Edge, Firefox, Electron (VS Code, Teams), Java Swing, Qt, Office (Word, Excel, Outlook), Konsole/Windows-Terminal, Explorer |
| Undurchsichtige Oberflächen | RDP-Fenster, Citrix/VM-Fenster, Spiel im Fenster, Zeichenfläche (Paint) |
| Monitore | 1× FHD, 2× gemischt (FHD + 4K), 3 Monitore, Hochformat, Abdocken/Umstecken während der Aufnahme |
| Skalierung | 100 %, 125 %, 150 %, 200 %, gemischt |
| Grafik | Nur integriert, nur dediziert, Hybrid-Grafik, HDR an |
| Tastaturlayouts | Deutsch, Englisch (US), Französisch (Tottasten), Japanisch/Chinesisch (IME) |
| Eingabegeräte | Maus mit 1000 Hz, Touchpad mit Gesten, Touch/Stift (Basis), vertauschte Maustasten |
| Rechte | Standardbenutzer; Zielanwendung mit Adminrechten; Recorder mit Adminrechten |
| Sonderfälle | UAC-Abfrage, Sperren/Entsperren, Benutzerwechsel, Energiesparen, Bildschirmschoner |
| Umgebung | Sitzungsordner lokal, in OneDrive/Nextcloud (Warnung), auf USB-Stick; Defender aktiv; Smart App Control an |

## 8. Risiken

| Nr. | Risiko | Wahrscheinlichkeit | Auswirkung | Gegenmaßnahme | Prüfung |
|---|---|---|---|---|---|
| R-01 | Virenscanner, SmartScreen, Smart App Control stufen die EXE wegen Tastatur-Hooks als Keylogger ein | hoch | hoch | Code-Signierung (SignPath Foundation – *offen, siehe O-10*), offene Quellen, reproduzierbare Builds, Meldung als Fehlalarm, Erklärung in README | VE-12, M8 |
| R-02 | Unsichtbarkeit wirkt nicht auf jedem Aufnahmeweg oder jeder Windows-Version | mittel | hoch | Selbsttest bei jedem Start, Ausweichmodus | VE-05, T-05 |
| R-03 | Hook wird bei Last vom System entfernt | mittel | hoch | Capture-Host-Prozess, Raw Input als Gegenkanal, Lückenmarker | VE-01, T-02 |
| R-04 | UI Automation langsam, unvollständig oder hängt in einzelnen Anwendungen | hoch | mittel | Eigener Thread, Zeitlimits, doppelte Ermittlung, Rückfallkette, Einstufung „undurchsichtig“ | VE-04 |
| R-05 | Passworteingaben in Terminal, Remote-Desktop, VM nicht erkennbar | sicher | hoch | Entschieden: Schutznetz (Hinweis, Privattaste, Durchsichtspflicht für Getipptes in Terminal, Remote-Desktop und VM); Stufe „Streng“ maskiert dort alles; die Mustererkennung für Kennwortabfragen im Terminal (Regel T1) ist **nicht umgesetzt**; offen dokumentiert | VE-08, T-07 |
| R-06 | Fenster mit Adminrechten liefern keine Eingaben | sicher | mittel | Lückenmarker, „als Administrator neu starten“ | VE-02 |
| R-07 | Speicher- und Rechenbedarf bei 4K-Mehrmonitor | mittel | mittel | Differenzbilder, Pufferung im Grafikspeicher, zeitversetzte Kodierung, Lückenmarker statt stillem Verlust | VE-09, T-13 |
| R-08 | Hybrid-Grafik, HDR, Remote-Sitzungen brechen die Bildaufnahme | mittel | mittel | Rückfallkette Desktop Duplication → Windows.Graphics.Capture → GDI | VE-10 |
| R-09 | Koordinaten falsch bei gemischter Skalierung oder Topologiewechsel | mittel | hoch | Monitorrelative physische Pixel, Topologie-Epochen, Plausibilitätsprüfung | VE-06 |
| R-10 | Umfang des Viewers sprengt den Zeitrahmen | hoch | mittel | Muss/Soll/Kann je Funktion; Ansehen und Kopieren (M3) vor Bearbeiten (M4); Komfort in M7 | Meilenstein-Review |
| R-11 | Missbrauch als Überwachungswerkzeug | mittel | hoch (Ruf) | Kein verdeckter Modus, kein Autostart, sichtbare Aufnahme als Projektgrundsatz; Hinweise zu DSGVO und Betriebsrat ([10](10-datenschutz-und-sicherheit.md)) | Code-Review-Regel |
| R-12 | Prompt-Injection über aufgezeichnete Texte (Fenstertitel, Webseiteninhalte) | mittel | mittel | Aufgezeichnete Texte stets als Daten markiert, Hinweis in `README_AI.md`, Entschärfung in Exporten | T-09 |
| R-13 | Lizenzwechsel einer Abhängigkeit | niedrig | mittel | Nur freizügige Lizenzen, Lizenzprüfung im CI, Versionen festgeschrieben | CI |
| R-14 | Single-File-EXE groß (grob 70–100 MB) und entpackt Teile nach `%TEMP%` | sicher | niedrig | Bewusst akzeptiert; ZIP-Variante ohne Entpacken | VE-11 |
| R-15 | Schritterkennung segmentiert reale Eingaben falsch (IME, Gesten, Auto-Repeat) | mittel | mittel | Rohdaten bleiben, Regeln versioniert und neu berechenbar, Golden-Journal-Regression | T-03 |
| R-16 | Datenformat muss nach Veröffentlichung geändert werden | mittel | mittel | Schema- und Regelversion, Migrationsregeln, Leser toleriert unbekannte Felder ([04](04-datenformat.md)) | Einheitstests |
| R-17 | Zusätzliche Funktionen (Vergleich, Ersetzen, Verallgemeinern, Support-Paket, Profile, Suche) verzögern Version 1.0 | mittel | mittel | Alle als Soll eingestuft; Ziel-Felder, Marker und Profile früh (M3), aufwendige Teile gebündelt in M7 und bei Bedarf nach 1.0 verschiebbar | Meilenstein-Review |

## 9. Offene Entscheidungen

| Nr. | Frage | Empfehlung | Spätestens |
|---|---|---|---|
| O-01 | Projektlizenz | *Entschieden:* CC BY-ND 4.0 für Programm und Quellcode ([LICENSE](../../LICENSE)). Folge: keine OSI-Lizenz, siehe O-10 | erledigt |
| O-02 | Endgültiger Produktname | Namensrecherche (Marken, Domains, GitHub) | vor M8 |
| O-03 | Hauptauslieferung Einzel-EXE oder ZIP | Beides; Einzel-EXE als Standard, wenn VE-11 Start < 2 s bestätigt | M0 |
| O-04 | Konsolen-Startprogramm für `mcp` nötig? | Nur falls VE-07 scheitert | M0 |
| O-05 | Texterkennung (OCR) als Rückfall | Windows-OCR braucht Paketidentität; später mit mitgelieferter Engine prüfen | nach 1.0 |
| O-06 | Schwellenwerte (Beruhigungszeit, Verweilzeit, Rückhaltefenster) | Startwerte aus [03](03-aufzeichnungskern.md)/[05](05-schritterkennung.md), feinjustiert nach M6-Tests | M6 |
| O-07 | Quellcode-Hosting und Mitwirkung | GitHub (Releases, Issues, SignPath-Anbindung). *Stand: GitHub, Workflow `build.yml` baut, testet und veröffentlicht bei Tag `v*`; Anbindung der Signierung offen* | M0 |
| O-08 | .NET 10 statt .NET 8 | Das .NET-10-SDK installieren (`winget install Microsoft.DotNet.SDK.10`), `global.json` und die Zielplattform beider Projekte anheben, `PublishReadyToRun` neu bewerten, alle Tests wiederholen. Bis dahin läuft alles auf .NET 8 (SDK 8.0.4xx) | offen – wartet auf das SDK |
| O-09 | Regel T1: Kennwortabfragen im Terminal erkennen (DS-022, DS-023) | Vorversuch, ob Windows Terminal und die klassische Konsole die Cursorzeile über UI Automation liefern (`Read-Host`, `runas`, `sudo` in WSL, `ssh`; p95 unter 100 ms). Bei Erfolg Umsetzung mit maskierter Zeile bis zur Eingabetaste, sonst bleibt es bei Durchsichtspflicht und „Streng“ | offen |
| O-10 | Code-Signierung und Lizenz | Die SignPath Foundation verlangt nach bisherigem Kenntnisstand eine OSI-anerkannte Lizenz; CC BY-ND 4.0 ist keine. Zu klären: Lizenzwechsel, ein anderer Signierdienst oder ein eigenes Zertifikat. Bis dahin sind Releases unsigniert (Prüfsumme in `SHA256SUMS.txt`) | offen, vor M8 |
| O-11 | Zurückgestellte Funktionen | Abschnitt neu aufnehmen und ersetzen; Lesezeichen, Textbausteine; Seitenformat (Letter, quer) und eigene Word-Vorlage; weitere Sprachen der automatischen Texte; Video-Export; ARM64; Bedienhilfen (Namen für Sprachausgabe); Terminal-Regeln T2 und T3. Jede ist später einzeln planbar. Visueller Vergleich und Befehlspalette sind jetzt im Fahrplan ([12](12-entwickler-fahrplan.md), Pakete 19 und 15) | nach 1.0 |
| O-12 | Programmpfade und Befehlszeilen (12, A-1/A-2): Der Code speichert den vollständigen Programmpfad und die bereinigte Befehlszeile aufgenommener Programme; [10 §3](10-datenschutz-und-sicherheit.md#3-dateninventar) verspricht `%USERPROFILE%` und schließt Befehlszeilen fremder Prozesse aus | Code an 10 §3 anpassen: Pfade mit Platzhalter, Befehlszeile nur hinter „Erweiterter Programmkontext“ und als schwächer gemeldet – oder weglassen | vor Paket 10 |
| O-13 | Fremder Prozessspeicher (12, A-3): Listen- und Baumeinträge werden mit `VirtualAllocEx`/`ReadProcessMemory` gelesen; P9 schließt das aus | Auf UI Automation und MSAA umstellen; wo das nicht reicht, entfällt die Angabe | vor Paket 10 |
| O-14 | Update-Suche (12, A-4): abschaltbar, ab Werk aus; P2 sagt „kein Update-Check“ | P2 um die beschriebene Ausnahme ergänzen (ab Werk aus, nur die öffentliche Release-Seite, ohne Kennung, nichts wird heruntergeladen); T-12 gilt mit ausgeschalteter Suche | vor M8 |
| O-15 | Bereitschaft im Infobereich (12, Paket 14) gegen P1 („kein Autostart, kein Hintergrund“) | Erlauben als Einstellung, ab Werk aus: kein Autostart durch das Programm, in Bereitschaft keine Hooks und keine Bildaufnahme, eigenes Symbol | vor Paket 14 |
| O-16 | Ältere Leser (12, Paket 12): Programme bis 0.12 geben unbekannte Ereignisarten ungeprüft aus; nur eine neue Hauptversion des Formats hält sie auf | Leser-Mindestversion im Manifest und Sperre nach [04 §15.7](04-datenformat.md#157-erweiterungen-und-schutzrelevantes) für inhaltstragende Ereignisse; ausliefern mindestens eine Version vor der ersten solchen Quelle. Alternative: `schemaVersion` 2.0 mit der ersten Quelle | vor Paket 12 |
| O-17 | Erzeugter Testcode (12, Pakete 16, 17) gegen das Nicht-Ziel „keine Automatisierung oder Wiedergabe“ ([00 §4](00-ueberblick.md#4-nicht-ziele)) | Zulässig wie der AutoHotkey-Export: Das Programm erzeugt Text, spielt nichts ab. Wortlaut des Nicht-Ziels so schärfen | vor Paket 16 |
| O-18 | Abnahme erzeugter Tests (12, Paket 16): Darf das Testprojekt FlaUI oder einen C#-Übersetzer einbinden, um erzeugten Code zu übersetzen und auszuführen? | Ja, nur im Testprojekt und in einer eigenen Testkategorie mit Desktop (wie `Integration`); das Programm bleibt ohne Abhängigkeit (Lizenzprüfung wie NFA-019) | vor Paket 16 |
| O-19 | Notizen des Agenten (12, Paket 8): wo sichtbar? | Im Editor und in MCP sichtbar, gekennzeichnet als „vom Agenten“; in Anleitungen und KI-Paket erst, wenn der Autor sie übernimmt | vor Paket 8 |

## 10. Veröffentlichung

**Release-Checkliste (ab M8):**

- [x] Lizenzdatei (`LICENSE`, CC BY-ND 4.0), `THIRD-PARTY-NOTICES.md` mit allen Abhängigkeiten und Lizenztexten
- [x] Englische README (`README.en.md`) mit Zweck, Datenschutz-Grundsätzen, Erklärung der Hooks („warum Virenscanner warnen könnten“)
- [ ] Benutzerdoku (Deutsch, Englisch): Aufnehmen, Viewer, Export, KI-Paket, MCP-Einrichtung
- [ ] `CONTRIBUTING.md`, Verhaltenskodex, `SECURITY.md` (Meldeweg für Sicherheitslücken)
- [ ] Datenschutzerklärung: keine Datenerhebung, kein Netzwerk; Hinweise zu DSGVO/Betriebsrat
- [ ] Formatdokumentation (Sitzungsformat, KI-Paket) mit Schema-Version
- [x] SHA-256-Prüfsumme der EXE (`SHA256SUMS.txt`, Release-Auftrag des Workflows)
- [ ] Signierte EXE über SignPath Foundation oder einen anderen Dienst (offen, O-10); ZIP-Variante (nicht gebaut)
- [x] CI-Build aus getaggtem Stand (`build.yml`); die Bitgleichheit zweier Builds ist nicht geprüft
- [ ] T-01 bis T-17 und alle DT-Prüfungen aus [10 §17](10-datenschutz-und-sicherheit.md) auf dem Release-Build bestanden
- [ ] Rechtliche Hinweise ([10](10-datenschutz-und-sicherheit.md)) fachlich gegengelesen
- [ ] Optional: portable Manifeste für winget (`InstallerType: portable`) und Scoop

**Versionierung:** Semantic Versioning für das Programm; Sitzungsformat und KI-Paket haben eigene Schema-Versionen ([04](04-datenformat.md)).

## 11. Spätere Ausbaustufen

| Ausbau | Nutzen |
|---|---|
| Vollständige UI-Baum-Schnappschüsse zu jedem Schritt | KI sieht alle Bedienelemente, nicht nur das angeklickte |
| Texterkennung als Rückfall für undurchsichtige Oberflächen | Text aus Remote-Desktop- und VM-Fenstern |
| ARM64-Build | Windows on ARM |
| „Agent bittet um Vorführung“ über MCP – **umgesetzt** | Ein Agent bittet mit Aufgabenbeschreibung um eine Aufnahme (`request_recording`, nur mit `--allow-requests`) und wertet sie danach aus; der Nutzer bestätigt jede Anfrage ausdrücklich, die Aufnahme ist normal und sichtbar ([06](06-ki-export-und-mcp.md)) |
| Export in weitere Formate (Confluence, Agenten-Trajektorienformate, Video); **PowerPoint ist umgesetzt** | Breitere Weiterverwendung |
| Mehrsprachige Anleitungen aus einer Aufnahme | Textvorlagen in weiteren Sprachen (zurückgestellt; Deutsch und Englisch sind umgesetzt) |
