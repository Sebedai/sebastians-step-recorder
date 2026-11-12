# 12 – Fahrplan: StepRecorder für Software-Entwickler

`Status: Entwurf 0.1 · Stand: 2026-11-12`

Dieses Dokument plant, wie StepRecorder zum täglichen Werkzeug für **Software-Entwickler** wird: Fehler in der eigenen Anwendung nachstellen und festhalten, einem KI-Agenten den Fehler vollständig übergeben, aus einer Vorführung einen UI-Test erzeugen, Abläufe im Repository dokumentieren. Es beschreibt **Pakete, Reihenfolge, Aufwand, Schutzregeln und Abnahme** – noch keine Anforderungen mit Kennung. Diese entstehen bei der Feinplanung eines Pakets in den Fachkapiteln.

## Inhalt

1. [Zweck und Abgrenzung](#1-zweck-und-abgrenzung)
2. [Ausgangslage](#2-ausgangslage)
3. [Leitlinien](#3-leitlinien)
4. [Übersicht der Pakete](#4-übersicht-der-pakete)
5. [Pakete im Einzelnen](#5-pakete-im-einzelnen)
6. [Vorarbeiten und Abgleich](#6-vorarbeiten-und-abgleich)
7. [Offene Entscheidungen](#7-offene-entscheidungen)
8. [Vorab-Experimente](#8-vorab-experimente)
9. [Bewusst nicht im Fahrplan](#9-bewusst-nicht-im-fahrplan)
10. [Pflege](#10-pflege)

---

## 1. Zweck und Abgrenzung

Der Auftraggeber hat am 2026-11-12 sechs Bereiche für den Ausbau gewählt (Vorgaben V-40 bis V-45 in [01 §4](01-anforderungen.md#4-rückverfolgung-der-vorgaben)):

| Vorgabe | Bereich | Pakete |
|---|---|---|
| V-40 | KI-Agent-Schleife: der ganze Fehlerkontext für Agenten, Rückmeldung, Kommandozeile für Skripte | 8 |
| V-41 | Entwickler-Kontext: festhalten, was das *Programm* tat – Programmende, Absturz, Fehlertext, Build-Stand, Protokolle | 10–13 |
| V-42 | UI-Tests aus Aufnahmen: Testgerüste und Testbarkeits-Bericht | 16, 17 |
| V-43 | Tempo im Alltag: Bereitschaft im Infobereich, eigener Modus, Befehlspalette, Sofort-Bildschirmfoto | 14, 15 |
| V-44 | Doku und Reviews im Repository: Markdown, GIF, Vorher/Nachher-Vergleich | 18, 19 |
| V-45 | Reaktionszeiten sichtbar machen | 9 |

Nicht gewählt wurde ein eigener „ticket-fertiger Fehlerbericht“ für GitHub, GitLab, Jira und Azure DevOps; der vorhandene Fehlerbericht ([09 §8c](09-anleitungs-export.md#8c-fehlerbericht)) bleibt, wie er ist.

**Was dieses Kapitel nicht ändert:** die Grundsätze aus [00 §7](00-ueberblick.md#7-leitprinzipien) und [10 §2](10-datenschutz-und-sicherheit.md#2-grundsätze), die Nicht-Ziele aus [00 §4](00-ueberblick.md#4-nicht-ziele) – mit einer Klarstellung zu erzeugtem Testcode (O-17) – und die Formate von Sitzung und KI-Paket. Wo ein Paket einen Grundsatz berührt, steht das als offene Entscheidung in [Abschnitt 7](#7-offene-entscheidungen), nicht als stillschweigende Ausnahme.

## 2. Ausgangslage

Grundlage ist eine Durchsicht des Codes am 2026-11-12 (Programmversion 0.12, Regelversion der Schritte 1.6.0).

**Was Entwicklern heute schon hilft:**

- Lückenlose Spur mit Belegstufe je Schritt; Vorher-Bild garantiert vor der Eingabe.
- Je Klick das Element mit UIA-Typ, Name, AutomationId, Pfad, Win32-Klasse und ClassNN; Programmname und **Produktversion** des Fensters (`target.win32.window.app`, `Win32Detail.AppInfo`).
- Je Schritt Wartesignale und Zustand danach (`sync`, `verify`), nach Stabilität geordnete Selektoren (`selectors`), Menüpfade, Listenauswahl (`StepBuilder.Automation.cs`).
- Browser-Adresse ohne Abfrageteil für Chrome, Edge, Firefox und Brave (`AppProbe`).
- KI-Paket, MCP-Server mit zehn lesenden Werkzeugen, `request_recording` (nur mit `--allow-requests`), `set_step_text` (nur mit `--allow-edit`).
- Kommandozeile mit strengen Argumenten, Exit-Codes und JSON bei `export`, `check`, `validate`, `compare`, `settings`, `record --self-test`.
- Fehlerbericht als Markdown, Vergleich zweier Aufnahmen, AutoHotkey-Skript und AHK-KI-Paket.

**Was fehlt:**

| Bereich | Befund | Fundstelle |
|---|---|---|
| Programm-Kontext | Kein Programmende, kein Exit-Code, kein Hänger, keine Einträge der Ereignisanzeige, keine Protokollzeilen | `Recorder.System.cs` (Quellen), `StepBuilder.Finish.BuildSystemSteps` |
| Fehlertext | Nur klassische Win32-Fenster liefern ihre Texte (`target.win32.texts`); `ui.snapshot` ist beschrieben ([03 §7.5](03-aufzeichnungskern.md#text-schnappschuss)), aber nicht gebaut; `window.dialog` kommt nur für klassische Dialoge | `Win32Detail.cs`, `Recorder.System.cs` (`EVENT_SYSTEM_DIALOGSTART`) |
| Build-Stand | Produktversion wird aufgezeichnet, aber nur im Fehlerbericht gezeigt – nicht in `trace.md`, nicht in MCP | `GuideExport.BugReport.cs`, `KiPackage.cs` |
| MCP | Nur Werkzeuge, keine Prompts; Schrittobjekte ohne `selectors`, `sync`, `verify`, `browser`, `menuPath`; zurückschreiben nur Titel und Text | `McpStdioServer.cs` (`prompts/list` leer), `StepRecorderMcp.StepObject` |
| Kommandozeile | `record` gibt am Ende keinen Sitzungspfad aus; kein `list`, kein „neueste Aufnahme“; `steps` ohne JSON; Export ohne Schrittbereich | `Cli.cs`, `UiApp.ParseRecordArgs` |
| UI-Tests | Aus den neutralen Selektoren entsteht nur AutoHotkey; UI Automation steht dort nur als Kommentar, geklickt wird über ClassNN oder Koordinaten | `AhkExport.ClickCore` |
| Reaktionszeiten | Gemessen (`effect.latencyUs`, `sync.settleMs`, `sync.busyMs`, `sync.readyMs`), aber nur im Detailbereich des Editors sichtbar | `StepBuilder.Bind.BindEffects`, `ViewerWindow.cs` |
| Bereitschaft | Das Programm beendet sich, sobald kein Fenster offen ist; das globale Start-Kürzel wirkt nur, solange ein Fenster offen ist | `UiApp.MaybeShutdown`, `RecordingController.cs` (Symbol nur während der Aufnahme) |
| Editor | Keine Befehlspalette; Tastenkürzel des Editors fest | `ViewerCommands.BuildCommands` |
| Markdown | Weicht von [09 §6](09-anleitungs-export.md#6-markdown) ab: Überschriften statt nummerierter Liste, gemeinsamer Ordner `bilder\`, Umlaute in Dateinamen, starke Maskierung; „Als Markdown kopieren“ verweist auf ein Bild, das nicht mitkommt | `GuideExport.Markdown`, `Fmt.MdEscape`, `ViewerActions.CopyMarkdown` |
| Vergleich | Schrittabgleich ohne Bilder; im Editor nur ein Textfeld mit Markdown | `StepCompare.cs`, `ViewerActions.Compare` |

## 3. Leitlinien

Jedes Paket hält diese Regeln ein. Sie fassen zusammen, was die übrigen Kapitel festlegen.

| Nr. | Leitlinie | Folge für die Pakete |
|---|---|---|
| L-1 | **Offline, portabel.** Kein Netzwerk, keine Adminrechte, keine Registry-Schreibzugriffe, kein Dienst, keine Injektion. | Neue Quellen nutzen nur dokumentierte Schnittstellen ohne erhöhte Rechte: `OpenProcess` mit eingeschränkten Rechten, `System.Diagnostics.Eventing.Reader` (Teil der Laufzeit, kein Paket), UI Automation, Dateilesen mit geteiltem Zugriff. Kein `git.exe`, kein ETW, keine Absturzabbilder. |
| L-2 | **Sichtbar, nie im Hintergrund.** Aufnahme nur nach ausdrücklichem Start, immer mit Leiste und Rahmen. | Auch die Bereitschaft im Infobereich (Paket 14) erfasst nichts, solange keine Aufnahme läuft. Ein Agent erbittet, startet nie. |
| L-3 | **Rohdaten zuerst.** Neue Quellen schreiben neue Ereignisarten ins Journal; Schritte werden daraus abgeleitet. | Jede Änderung an der Schrittbildung erhöht `StepParams.BuilderVersion` und bekommt Regressionstests mit `SyntheticSession`. |
| L-4 | **Ehrlich.** Was nicht beobachtet wurde, wird nicht behauptet. | „Absturz“ nur bei eindeutigem Fehlercode; Git-Stand als „Arbeitsstand, ungeprüft“; Reaktionszeiten jenseits des Beobachtungsfensters als „länger als …“; erzeugte Tests sind Entwürfe. |
| L-5 | **Ein Tor.** Alle Ausgaben lesen über die Leseschicht (`SessionView`). | Neue inhaltstragende Ereignisse stehen nicht in der Menge `Redactor.Structural`, werden also in geschwärzten Zeiträumen geleert; sie laufen durch Schwärzung, Verallgemeinerung und Durchsicht. Inhalt gehört nie in `system`-Ereignisse. |
| L-6 | **Nie unbemerkt schwächer.** | Jede Quelle, die mehr Inhalt erfasst, ist ab Werk aus, wird als schwächer gemeldet ([10 §4.4](10-datenschutz-und-sicherheit.md#44-aufnahmeprofile-nie-unbemerkt-schwächer)) und ist in „Streng“ aus oder auf Fakten beschränkt. |
| L-7 | **Nie erhoben:** Rechner- und Benutzername, absolute Pfade unter dem Profil, Befehlszeilen, Umgebungsvariablen, Modul- und Prozesslisten, Git-Fernadressen, Autoren und Commit-Texte. | Gilt für alle Pakete; Pfade nur als Dateiname oder mit Platzhalter. |
| L-8 | **Zweisprachig.** | Jeder neue Text über `Loc`; `LocalizationTests` bleibt grün. |
| L-9 | **Ohne neue Abhängigkeiten im Programm.** | Erzeugter Testcode darf FlaUI, pywinauto oder Playwright verwenden – das Programm selbst bindet sie nicht ein. |
| L-10 | **Je Paket vollständig.** | Code, Tests, Fachkapitel mit Anforderungen, README (Deutsch und Englisch) und Stand in [11 §2.1](11-roadmap-tests-risiken.md#21-stand-der-umsetzung) gehören in dasselbe Paket; ein Commit je Paket. |

## 4. Übersicht der Pakete

Die Pakete zählen die Ausbaupakete 0–7 aus [11 §2.1](11-roadmap-tests-risiken.md#21-stand-der-umsetzung) fort. Aufwand: **S** ≈ ein Arbeitsabschnitt, **M** ≈ zwei bis drei, **L** ≈ vier und mehr – jeweils mit Tests und Spezifikation. Die Schätzung ist grob; die Feinplanung schärft sie.

| Nr. | Paket | Vorgabe | Aufwand | Greift ein in | Hängt ab von | Regelversion | Stand |
|---|---|---|---|---|---|---|---|
| V | Vorarbeiten und Abgleich | – | S–M | Kern, Kommandozeile, Spezifikation | – | nein | erledigt |
| 8 | Agenten-Schleife | V-40 | M | MCP, Kommandozeile | – | nein | erledigt |
| 9 | Reaktionszeiten | V-45 | S | Editor, Vergleich, Ausgaben | – | nein | erledigt |
| 10 | Kontext I: Programmende, Hänger, Build-Stand | V-41 | M | Aufzeichnungskern, Schritte | V | ja (1.7) | geplant |
| 11 | Kontext II: Fehlertext | V-41 | M (L bei zweitem UIA-Arbeiter) | Aufzeichnungskern, Schritte | 10 | ja | geplant |
| 12 | Kontext III: prüfbare Programmausgaben | V-41 | L | Leseschicht, Durchsicht, Datenschutz | 10 | ja | geplant |
| 13 | Kontext IV: Protokolldateien | V-41 | M | Aufzeichnungskern, Startfenster | 12 | nur bei Zuordnung zu Schritten | geplant |
| 14 | Bereitschaft und Modus „Fehler nachstellen“ | V-43 | M | Oberfläche, Lebenszyklus | (10 für den vollen Modus) | nein | geplant |
| 15 | Befehlspalette und Sofort-Bildschirmfoto | V-43 | M | Editor | – | nein | geplant |
| 16 | UI-Tests I: Automationsmodell, FlaUI, Testbarkeits-Bericht | V-42 | L | Export | – | nein | geplant |
| 17 | UI-Tests II: pywinauto, Playwright, Test-KI-Paket | V-42 | M–L | Export, MCP | 16 | nein | geplant |
| 18 | Doku im Repository | V-44 | M | Export, Editor | – | nein | geplant |
| 19 | Visueller Vergleich | V-44 | M–L | Vergleich, Editor | 9 | nein | geplant |

**Empfohlene Reihenfolge:** V → 8 → 9 → 10 → 11 → 14 → 16 → 12 → 13 → 15 → 18 → 17 → 19.

- **Zuerst ohne Eingriff in die Aufzeichnung** (8, 9): Die Daten liegen vor, es fehlen Zugänge. Schnellster Nutzen, kleinstes Risiko.
- **Dann der größte Unterschied für Entwickler** (10, 11): Was das Programm tat, mit geringem Schutzrisiko, weil nur Fakten und sichtbarer Text erfasst werden.
- **Dann Tempo und Tests** (14, 16).
- **Inhaltstragende Quellen zuletzt** (12, 13): erst, wenn die Durchsicht dafür gebaut ist.
- Bis auf die Abhängigkeiten in der Tabelle lässt sich die Reihenfolge umstellen; 15 und 18 passen jederzeit dazwischen.

## 5. Pakete im Einzelnen

### Paket 8 – Agenten-Schleife (M)

**Ziel:** Ein Agent wie Claude Code bekommt einen aufgezeichneten Fehler mit einem Aufruf vollständig, kann Ergebnisse an Schritte hängen und eine Prüfaufnahme erbitten; Skripte finden Aufnahmen ohne Oberfläche.

**Umfang:**

- **MCP-Werkzeug `get_failure_context`:** Ziel und erwartetes Ergebnis, alle Marker, die *n* Schritte vor dem ersten Marker „Fehler“ (sonst vor dem Ende) mit Selektoren, Wartesignalen und Zustand danach, Browser-Adresse und Programmversion, Lücken im Zeitraum, ein Bildausschnitt um die Fehlerstelle – im Tokenbudget, seitenweise. Spätere Pakete hängen ihren Kontext an (Programmende, Fehlertext, Einträge der Ereignisanzeige).
- **`get_steps`/`get_step` mit `include`:** auf Wunsch `selectors`, `sync`, `verify`, `menuPath`, `choose`, `browser`, `explorer`, `office`, `data` (heute fest weggelassen in `StepRecorderMcp.StepObject`).
- **Programmversion** der beteiligten Programme in `get_session` und im Kopf von `trace.md`.
- **MCP-Prompts** (`prompts/list`, `prompts/get`; in Claude Code als Slash-Befehle): `analyze_bug`, `write_ui_test`, `write_guide`, `verify_fix`. Sie enthalten nur Arbeitsanweisungen und Kennungen, **nie aufgezeichnete Inhalte**.
- **Notiz des Agenten** (`add_step_note`, nur mit `--allow-edit`, Ratenbegrenzung wie `set_step_text`): eigene Kommentarart `agentNote` mit `by: mcp`; Sichtbarkeit nach O-19.
- **Prüfaufnahme:** `request_recording` nimmt optional die Kennung der Ausgangsaufnahme (`retestOf`); nach `finished` vergleicht `compare_sessions` beide. Die Person bestätigt wie bisher ([10 §2.2](10-datenschutz-und-sicherheit.md#22-aufnahme-auf-bitte-eines-ki-agenten)).
- **Kommandozeile:** `list [--json] [--limit n]`; `latest` als Aufnahmeangabe für alle Befehle (`export latest --format ki`); `steps --json`; `export --from/--to` und `--steps` (in [06 §3.1](06-ki-export-und-mcp.md#31-erzeugen-viewer-und-kommandozeile) beschrieben, nicht gebaut); `record --json`.
- **„Mit KI verbinden“** für weitere Programme neben Claude Code und Claude Desktop; die Konfigurationsformate werden bei der Umsetzung gegen deren aktuelle Dokumentation geprüft.

**Nicht-Umfang:** Aufnahme starten, stoppen oder markieren durch den Agenten (A6 bleibt).

**Bausteine:** `StepRecorderMcp.cs`, `McpStdioServer.cs` (Fähigkeit `prompts`), `KiPackage.Trace`, `Cli.cs`, `CliArgs.cs`, `AiConnect.cs`, `McpConfig.cs`, `RecordingRequests.cs`. **Kapitel:** 02 §8, 06, 10 §2.2.

**Schutzregeln:** Alle neuen Werkzeuge lesen über die Leseschicht im Modus ohne Oberfläche (`{unreviewed:N}`). `latest` löst nur innerhalb des Aufnahmeordners auf. Prompts enthalten keine Daten.

**Abnahme:** Byteweise reine Standardausgabe im `mcp`-Modus (02 §8.5) über alle neuen Werkzeuge; Kanarienwert aus einer ungeprüften Eingabe erscheint in keiner Antwort; `get_failure_context` bleibt bei 2 000 Schritten im Budget; `export latest` und `list --json` mit Exit-Codes nach 02 §8.6.

**Risiko:** gering. Die Antwortgröße von `get_failure_context` muss gegen das Budget gemessen werden.

### Paket 9 – Reaktionszeiten (S)

**Ziel:** Sichtbar machen, wie lange die Anwendung nach jedem Schritt brauchte – für Performance-Fehler und für Vorher/Nachher-Vergleiche.

**Umfang:**

- Die Messwerte gibt es schon: erste beobachtete Reaktion (`effect.latencyUs`), Bild ruhig (`sync.settleMs`), Mauszeiger beschäftigt (`sync.busyMs`), Fenster erschienen (`sync.waitWindow`), bereit (`sync.readyMs`), Denkzeit bis zur nächsten Eingabe (`sync.thinkMs`).
- Editor: Spalte oder Abzeichen „Reaktion“ in der Schrittliste, Filter „langsame Schritte“ mit einstellbarer Schwelle, Summe Wartezeit gegen Denkzeit in der Sitzungsübersicht.
- Vergleich: Zeitunterschied je Schrittpaar in `compare` und `compare_sessions` (heute nur Dauer).
- Ausgaben: Zeile „Reaktion“ in `trace.md` und in MCP-Schrittobjekten; neuer Hinweis der Qualitätsprüfung „Reaktion langsamer als Schwelle“.

**Nicht-Umfang:** Profiler, Messung im Prozess der Anwendung, neue Ereignisse.

**Bausteine:** `ViewerWindow.cs`, `ViewerLayout.cs`, `StepCompare.cs`, `KiPackage.cs`, `StepRecorderMcp.cs`, `QualityCheck.cs`. **Kapitel:** 05 (Wirkung), 06, 08 §5.9, 09 §12.1.

**Schutzregeln:** keine neuen Daten.

**Abnahme:** Zahlen in Editor, `trace.md`, MCP und Vergleich stimmen für eine synthetische Sitzung mit bekannten Zeiten überein; Werte jenseits des Beobachtungsfensters erscheinen als „länger als …“.

**Risiko:** Fehldeutung. Die Werte messen sichtbare Reaktion, nicht Rechenzeit; die Oberfläche sagt das (VE-19).

### Paket 10 – Kontext I: Programmende, Hänger, Build-Stand (M)

**Ziel:** Die Aufnahme zeigt, wenn die getestete Anwendung abstürzt, hängt oder sich beendet – und welcher Build lief.

**Umfang:**

- **Programmende:** Für jedes Programm, mit dessen Fenster gearbeitet wurde, hält der Recorder beim Registrieren des Fensters einen Prozess-Handle (`OpenProcess` mit `SYNCHRONIZE` und `PROCESS_QUERY_LIMITED_INFORMATION`, höchstens 64) und fragt im 250-ms-Takt ohne Warten ab. Neue Ereignisse `process.info` (Programmname, Startzeit, „vor der Aufnahme gestartet“) und `process.exit` (Exit-Code, Einstufung, Zeitpunkt aus `GetProcessTimes`).
- **Einstufung:** „Absturz“ nur bei Fehlercodes mit eindeutiger Bedeutung (Bereich `0xC…`, `0xE0434352` für .NET, `0xE06D7363` für C++); sonst „beendet mit Code N“. Eigener Hauptschritt mit Bild; im vorangehenden Schritt als Wirkung `processExited`.
- **Hänger:** Ereignis `window.hang` (Beginn, Ende) über `IsHungAppWindow` im Takt; im Schritt als Wartesignal, in `trace.md` als Zeile.
- **Absturzfakten aus der Ereignisanzeige:** einmal kurz nach einem Absturz und einmal beim Stopp eine Abfrage des Anwendungsprotokolls im Zeitraum der Aufnahme, nur für aufgenommene Programme: Ereignisse 1000 (Anwendungsfehler) und 1002 (Hänger) mit **Fakten ohne Freitext** – Ausnahmecode, Modulname, Offset. Den Freitext (etwa den .NET-Stacktrace aus Ereignis 1026) bringt erst Paket 12.
- **Build-Stand:** Produktversion (vorhanden; trägt bei SDK-Projekten ab .NET 8 oft den Commit als Anhang), dazu Änderungszeit und Größe der Programmdatei, Zeitstempel und PDB-Kennung aus dem PE-Kopf – an die Binärdatei gebunden und damit belastbarer als jeder Git-Stand.
- **Git-Arbeitsstand (Zusatz):** Liegt die Programmdatei in einem Git-Arbeitsverzeichnis, liest der Recorder Zweig und Commit aus `.git` (`HEAD`, Refs, `packed-refs`, Verweis `gitdir:` bei Worktrees) – ohne `git.exe`, mit Zeit- und Größengrenzen. Gekennzeichnet als `basis: workingTree`, `dirty: notChecked`: Es ist der Stand des Ordners, kein Beleg für den Build.
- Ausgaben: Schritt und Zeilen in Editor, `trace.md`, MCP (`get_failure_context`, `get_session`) und Fehlerbericht („Tatsächliches Ergebnis“ nennt ein Programmende).

**Nicht-Umfang:** Absturzabbilder, Debugger, Ereignisse anderer Programme, Kindprozesse ohne Fenster, Abstürze vor dem ersten Fenster (nur über die Ereignisanzeige erkennbar, VE-15).

**Bausteine:** `WindowRegistry.cs`, `Recorder.System.cs`, `Recorder.Input.cs` (Takt), `Win32Detail.cs` (Zwischenspeicher je Prozess mit Startzeit statt nur Prozessnummer), `StepBuilder.Finish.cs`, `StepBuilder.Bind.cs`, `TextTemplates.cs`, `KiPackage.cs`, `GuideExport.BugReport.cs`, `StepModel.cs` (Regelversion 1.7.0). **Kapitel:** 03, 04 (neue Ereignisarten als MINOR-Erweiterung), 05, 06, 10 §3.

**Schutzregeln:**

- Nur Programme im Aufnahmebereich und nicht auf der Ausschlussliste; nichts während Pause und Privattaste.
- Nie: absolute Pfade, weitere Befehlszeilen, Umgebungsvariablen, Modul- oder Prozesslisten, Git-Fernadressen, Autoren, Commit-Texte, Pfad des Repositorys. Der Zweigname ist Text und lässt sich schwärzen.
- Dateien und `.git` werden nur gelesen, wenn „Erweiterter Programmkontext“ an ist; Programmende und Hänger sind strukturelle Angaben und immer an.
- Exporte nennen keine Prozessnummer.

**Abnahme:** Synthetische Sitzungen mit Absturz (`0xC0000005`), normalem Ende, Ende mit Code 1 und Hänger ergeben die richtigen Schritte und Texte in beiden Sprachen; kein absoluter Pfad und kein Benutzername in Sitzung und Ausgaben (Kanarienpfad); Test mit einem Git-Ordner, einem Worktree und einem Ordner ohne Git.

**Risiko:** falsche Einstufung als Absturz (deshalb nur eindeutige Codes); Programme mit erhöhten Rechten anderer Benutzer liefern keinen Handle – dann Lücke statt Vermutung (VE-15).

### Paket 11 – Kontext II: Fehlertext (M; L bei zweitem UIA-Arbeiter)

**Ziel:** Der Wortlaut von Fehlermeldungen steht als Text in der Aufnahme – durchsuchbar, für Agenten lesbar, unabhängig von der Bauart der Anwendung.

**Umfang:**

- `ui.snapshot` nach [03 §7.5](03-aufzeichnungskern.md#text-schnappschuss) und dem Format in 04, zunächst für drei Auslöser: modales Fenster erscheint (Besitzerfenster deaktiviert – erfasst auch WPF-, WinForms- und WinUI-Dialoge, für die `window.dialog` nicht kommt), Marker „Fehler“ und „Erwartet“ (Vordergrundfenster), Schnappschuss-Taste.
- Grenzen wie spezifiziert: 250 ms, `capture.snapshotMaxNodes` (300, heute ungenutzt), 12 Ebenen, Werte höchstens 200 Zeichen; Kennzeichen „unvollständig“.
- Schritte verweisen auf ihre Schnappschüsse (`snapshots`); `trace.md` erhält den Block aus [06 §7.5](06-ki-export-und-mcp.md#75-text-schnappschüsse); MCP und Suche lesen ihn; der Editor zeigt „Text im Fenster“ mit Kopieren.

**Nicht-Umfang:** Menüs, Aufklapplisten und Tooltips (spätere Stufe), vollständiger Elementbaum.

**Bausteine:** `UiaService.cs`, `Recorder.System.cs`, `Recorder.cs` (Marker), `StepBuilder`, `SessionView.cs`, `SessionSearch.cs`, `KiPackage.cs`, `StepRecorderMcp.cs`, Editor. **Kapitel:** 03 §7.5, 04, 05, 06, 08.

**Schutzregeln:** Nur sichtbare Elemente innerhalb des Fensterrechtecks – der Text zeigt nie mehr, als das Bild zeigt. Kennwortfelder nie; Feldwerte nur bei eindeutig normalen Feldern und nie in „Streng“; nichts im Privatmodus und für ausgeschlossene Programme. Die Musterprüfung meldet Treffer als Hinweis ([10 §11.2](10-datenschutz-und-sicherheit.md#112-musterprüfung)).

**Abnahme:** Schnappschüsse von MessageBox, TaskDialog, WPF-, WinForms- und Win32-Dialog in einer Test-Zielanwendung; Kanarienwert in einem Kennwortfeld erscheint nie; „Schritt schwärzen“ und „Text überall schwärzen“ entfernen den Schnappschuss aus allen Ausgaben.

**Risiko:** Der Schnappschuss teilt sich den einen UIA-Arbeiter mit der Elementermittlung bei Klicks. VE-17 misst Dauer und Abdeckung; reicht es nicht, bekommt der Schnappschuss einen eigenen Arbeiter (Aufwand L).

### Paket 12 – Kontext III: prüfbare Programmausgaben (L)

**Ziel:** Freitext, den die Anwendung erzeugt, aber nicht auf dem Bildschirm zeigt – Stacktrace aus der Ereignisanzeige, Inhalt der Zwischenablage –, kommt in die Aufnahme, ohne den Schutz zu schwächen.

**Umfang:**

1. **Grundlage, vor jeder Quelle:**
   - **Allgemeine Prüfposten** in der Durchsicht. Heute prüft sie nur getippte Eingaben (`SessionView`, Schrittart `type`); [10 §11.1](10-datenschutz-und-sicherheit.md#111-durchsicht-aller-eingaben) verlangt auch Kommentare, Ziel-Angaben und Marker-Bezeichnungen. Ein Prüfposten kann künftig jedes inhaltstragende Ereignis sein.
   - **„Nie unbemerkt schwächer“** nach [10 §4.4](10-datenschutz-und-sicherheit.md#44-aufnahmeprofile-nie-unbemerkt-schwächer): `manifest.profile.weakens`, Statuszeile im Startfenster, Startmeldung der Leiste, Zeile auf stderr bei `record`.
   - **Schutz gegen ältere Leser:** Programme bis 0.12 reichen unbekannte Ereignisarten ungeprüft durch. Lösung nach O-16.
2. **Quellen**, jede ab Werk aus und im Startfenster mit Namen genannt:
   - **Freitext der Ereignisanzeige:** Ereignis 1026 (.NET-Laufzeit mit Stacktrace) und der Text von 1000/1002 für aufgenommene Programme, je Eintrag höchstens 8 KB, höchstens 20 Einträge.
   - **Zwischenablage-Inhalt** nach [03 §9](03-aufzeichnungskern.md#9-zwischenablage) – die Einstellung `ClipboardContent` gibt es, sie wirkt aber nicht.

**Nicht-Umfang:** Ereignisse anderer Programme, Sicherheits- und Systemprotokoll.

**Bausteine:** `SessionView.cs`, `Redactor.cs`, `ViewerActions.cs` (Durchsicht), `QualityCheck.cs`, `SessionSearch.cs`, `SessionSanitizer.cs`, `RecordingOptions.cs`, `StartWindow.cs`, `RecordingController.cs`, `Recorder.System.cs`. **Kapitel:** 03 §9, 04 §15.7, 07, 08 §13, 10 §3, §4.4, §9, §11.

**Schutzregeln:** Jede Quelle meldet sich als schwächer; die Leiste zeigt dauerhaft, welche Quelle läuft. Freitext ist immer prüfpflichtig: Ausgaben ohne Oberfläche zeigen `{unreviewed:N}`, Exporte aus dem Editor warten auf die Durchsicht. In „Streng“ nur Fakten aus Paket 10. Nie: Rechnername, Kontokennung, Einträge anderer Programme.

**Abnahme:** Ein Kanarienwert je Quelle erscheint vor der Durchsicht in **keinem** Ausgabeweg – Schritte, Rohereignisse (`get_events`), Suche und Suchindex, bereinigte Kopie, `.steprec`, KI-Paket samt `events.jsonl`, MCP, Fehlerbericht, Kopieren im Editor; nach der Durchsicht genau dort, wo er hingehört.

**Risiko:** ein übersehener Ausgabeweg. Deshalb die Kanarien-Abnahme über alle Wege, nicht je Quelle.

### Paket 13 – Kontext IV: Protokolldateien (M)

**Ziel:** Zeilen, die die Anwendung während der Vorführung in ihre Protokolldatei schreibt, stehen zeitlich neben den Schritten.

**Umfang:** Die Person nennt vor der Aufnahme Dateien oder Muster (Startfenster, `record --log <Datei>`, Profil „Fehler nachstellen“). Gelesen wird ab der Länge beim Start mit geteiltem Zugriff, im 500-ms-Takt, nur angehängte Zeilen; Drehen der Datei und gängige Kodierungen werden erkannt. Neue Ereignisse `log.source` und `log.lines` mit Zeitpunkt der Beobachtung; Grenzen höchstens 5 Dateien, 2 000 Zeichen je Zeile, 256 KB je Datei – darüber `truncated`/`dropped`. Ausgaben: Abschnitt „Protokoll“ im Editor, in `trace.md` und in `get_failure_context`; Zuordnung zum vorangehenden Schritt nur, wenn die Regelversion mitsteigt.

**Nicht-Umfang:** Netzlaufwerke, ganze Verzeichnisse, rückwirkendes Lesen.

**Schutzregeln:** nur lokale Festplatten; je Datei prüfpflichtig; in „Streng“ nicht verfügbar, und das Startfenster sagt warum; meldet sich als schwächer. Braucht eine Änderung von [10 §3](10-datenschutz-und-sicherheit.md#3-dateninventar) („Dateiinhalte“ stehen dort unter „bewusst nicht erhoben“).

**Abnahme:** wie Paket 12 mit einem Kanarienwert in einer Protokollzeile; Drehen der Datei während der Aufnahme; 10 MB Protokoll in einer Minute bleiben in den Grenzen.

**Risiko:** Menge und Geheimnisse in Protokollen; weiche Zeitstempel durch Pufferung der Anwendung (in den Ausgaben als „beobachtet um“ benannt).

### Paket 14 – Bereitschaft und Modus „Fehler nachstellen“ (M)

**Ziel:** Ein Fehler lässt sich in dem Moment festhalten, in dem er auftritt – ohne erst das Programm zu öffnen.

**Umfang:**

- **Bereitschaft im Infobereich** (Einstellung, ab Werk aus): Nach dem Schließen des letzten Fensters bleibt das Programm mit einem eigenen Symbol „bereit“ im Infobereich; das Kürzel für die Sofortaufnahme wirkt jederzeit; das Menü des Symbols bietet Aufnahme, Alle Aufnahmen, Einstellungen, Beenden. Heute beendet sich das Programm (`UiApp.MaybeShutdown`), und das Symbol gibt es nur während einer Aufnahme.
- **Modus „Fehler nachstellen“** (`bugRepro`): Detailgrad wie „Support-Fall“, Ziel-Felder offen und auf Fehler zugeschnitten („Was passiert?“, „Was hätte passieren sollen?“, „Bezug“), Programm-Kontext aus Paket 10 und 11 an, nach dem Stopp der Editor mit dem Fehlerkontext. Vorher wird die Liste der Modi, die heute an rund zwölf Stellen fest steht (`DetailPresets`, `RecordingOptions.ProfileJson`, `UiApp.ApplyProfile`, `StartWindow`, `Cli.ValidateRecordArgs`, `LibraryPage`, `SessionCatalog`, `AppSettings.LastProfile`, Tests), an einer Stelle gebündelt.
- **Warten in Konsolen:** eine kleine `steprec.cmd`, die die EXE startet und auf ihr Ende wartet ([02 §8.3](02-architektur.md#83-gui--oder-konsolenprogramm), Punkt 4) – auf Befehl in den Einstellungen neben der EXE angelegt.

**Nicht-Umfang:** Autostart mit Windows, Start ohne Fenster beim Anmelden.

**Schutzregeln:** In Bereitschaft laufen keine Hooks und keine Bildaufnahme – nur das registrierte Kürzel. Kein Autostart durch das Programm (P1); wer es beim Anmelden starten will, legt selbst eine Verknüpfung an. Das Symbol unterscheidet sich deutlich vom Aufnahmesymbol. Jede Aufnahme zeigt Leiste und Rahmen.

**Abnahme:** Speicher und Prozessorzeit in Bereitschaft über eine Stunde (VE-20); Start per Kürzel aus der Bereitschaft in höchstens 1,5 s (NFA-007); Netzwerk-Mitschnitt in Bereitschaft leer (T-12).

**Risiko:** Wahrnehmung als Hintergrundprogramm. Deshalb ab Werk aus und die Entscheidung O-15.

### Paket 15 – Befehlspalette und Sofort-Bildschirmfoto (M)

**Ziel:** Jeder Befehl des Editors in einer Sekunde; ein beschriftetes Bildschirmfoto in drei Tasten.

**Umfang:**

- **Befehlspalette** `Strg+Umschalt+P` ([08 §12](08-viewer.md#12-komfortfunktionen), Kann) über der vorhandenen Befehlstabelle (`ViewerCommands.BuildCommands`; die F1-Übersicht ist schon durchsuchbar): Suche über Beschriftung und Gruppe, zuletzt benutzte Befehle zuerst, Kürzel daneben.
- **Sofort-Bildschirmfoto:** eigenes globales Kürzel → Bereich wählen → Editor mit allen Zeichenwerkzeugen → `Strg+C`. Es entsteht eine Aufnahme mit einem Bildschritt, damit Schwärzen, Verpixeln, Kopieren und Export ohne Sonderweg gelten; sie erscheint in „Alle Aufnahmen“ und lässt sich wie jede andere löschen.

**Nicht-Umfang:** Lesezeichen und Textbausteine (bleiben zurückgestellt, O-11), frei belegbare Editor-Kürzel.

**Schutzregeln:** Das Bildschirmfoto nimmt nur den gewählten Bereich auf; die Fenster des Programms fehlen im Bild (Unsichtbarkeitsgarantie).

**Abnahme:** Tastaturdurchgang ohne Maus (NFA-014); Bildschirmfoto bis in die Zwischenablage mit höchstens drei Tasten und einem Ziehen.

**Risiko:** gering.

### Paket 16 – UI-Tests I: Automationsmodell, FlaUI, Testbarkeits-Bericht (L)

**Ziel:** Aus einer Vorführung entsteht ein lauffähiger Entwurf eines UI-Tests für die eigene Windows-Anwendung – und ein Bericht, was die Anwendung schwer testbar macht.

**Umfang:**

- **Gemeinsames Automationsmodell:** Das neutrale Dokument aus `AhkKiPackage.Document()` (Fenster, Selektoren, Eingaben, Wartesignale, erwarteter Zustand, Parameter, Wiederholungen) wird zu einer eigenen Komponente; AutoHotkey bleibt ein Abnehmer.
- **Ausgabeformat `flaui`** (C#, xUnit): eine Testklasse je Aufnahme.
  - Element über AutomationId, sonst Name und Typ, sonst Pfad der Vorfahren; Koordinaten nur als gekennzeichneter Notbehelf.
  - Aktion über das UIA-Muster (Invoke, Toggle, Value, SelectionItem, ExpandCollapse) statt Mausklick, wo das Element es anbietet.
  - Warten aus `sync` (Fenster erscheint, Mauszeiger beschäftigt, bereit) statt fester Pausen.
  - Prüfungen aus `verify` und `effect` (Titel, geänderte Texte, Häkchen) und an jedem Marker „Erwartet“; der Marker „Fehler“ wird zur Prüfung, die den Fehler belegt.
  - Getippte Werte als Parameter; maskierte und geheime Werte nie als Literal, sondern aus Umgebungsvariablen.
  - Ordner-Ausgabe mit Markerdatei, damit nie ein fremder Ordner geleert wird (`ExportFolders`).
- **Testbarkeits-Bericht** (`testability <Aufnahme> [--json] [--strict]`, im Editor unter Qualität): je benutztem Bedienelement, ob AutomationId oder eindeutiger Name fehlen, ob nur Koordinaten bleiben, ob die Klasse Laufzeitanteile enthält; mit `--strict` Exit-Code 9 bei Befunden – für CI.

**Nicht-Umfang:** Abspielen durch das Programm selbst (Nicht-Ziel [00 §4](00-ueberblick.md#4-nicht-ziele), siehe O-17); Abhängigkeit des Programms von FlaUI (L-9).

**Bausteine:** `AhkKiPackage.cs` (Modell herauslösen), neues Exportformat nach dem Muster von `bugreport` und `ahk` (`Exporter`, `ExportDialogs`, `ViewerExport`, `Cli`), `QualityCheck`. **Kapitel:** 00 §4, 06 (`get_automation`), 09 (neues Format), 02 §8.

**Schutzregeln:** wie beim AutoHotkey-Export: keine Kennwörter, ungeprüfte Eingaben nur nach der Durchsicht, Verallgemeinerungen als Parameter.

**Abnahme:** Der erzeugte Test für eine Vorführung in einer Test-Zielanwendung übersetzt und läuft grün; nach einer absichtlichen Änderung an der Anwendung schlägt er an der richtigen Stelle fehl (VE-21, Abnahmeweg nach O-18). Der Bericht nennt bekannte Mängel einer präparierten Anwendung vollständig.

**Risiko:** Erwartungen an „fertige Tests“. Die Ausgabe heißt Entwurf, nennt jede unsichere Stelle als Kommentar und begründet Notbehelfe.

### Paket 17 – UI-Tests II: pywinauto, Playwright, Test-KI-Paket (M–L)

**Umfang:**

- **pywinauto** (Python, Rückgriff `uia`) aus demselben Modell wie Paket 16.
- **Playwright-Entwurf** (TypeScript, `@playwright/test`) für Browser-Schritte: `page.goto` aus der aufgezeichneten Adresse, `getByRole` aus UIA-Typ und Name, Texteingaben als `fill`, „Erwartet“ als `expect`. Zuordnung der Typen zu Rollen nach VE-22.
- **Test-KI-Paket** wie das AHK-KI-Paket (Modell, Entwurf, Bilder der Zielelemente, Regeln für das Verbessern) und `get_automation` mit Zielangabe (`target: ahk|flaui|pywinauto|playwright`).

**Grenzen, offen benannt:** Es gibt keine DOM-Daten (keine CSS-Selektoren, keine HTML-Kennungen), und die Adresse ist ohne Abfrageteil gespeichert. Der Playwright-Entwurf ist ein Gerüst, das ein Mensch oder Agent mit dem Seitenquelltext vervollständigt.

**Abnahme:** erzeugter pywinauto-Test läuft gegen die Test-Zielanwendung; Playwright-Entwurf für eine lokale Testseite (`localhost`) läuft nach höchstens einer Korrektur je Selektor.

**Risiko:** UIA-Namen in Browsern weichen von zugänglichen Namen im DOM ab (VE-22).

### Paket 18 – Doku im Repository (M)

**Ziel:** Anleitungen und Abläufe leben als Markdown im Repository – mit sauberen Diffs und Bildern, die GitHub und GitLab anzeigen.

**Umfang:**

- **Markdown nach [09 §6](09-anleitungs-export.md#6-markdown):** eigener Bilderordner `<Titel>-bilder/`, ASCII-Dateinamen, nummerierte Liste, Maskierung nur wo Markdown sie braucht, Hinweiskästen im GitHub-Dialekt (`> [!NOTE]`, EXP-042), Kopfdaten für Docusaurus und MkDocs (EXP-044).
- **Stabile Ausgabe:** gleiche Aufnahme → bytegleiches Markdown und gleiche Bildnamen (NFA-018), damit ein erneuter Export nur echte Änderungen zeigt.
- **Terminal-Eingaben** als Codeblock (```` ```console ````), nach der Durchsicht.
- **Kopieren:** „Alle Schritte als Markdown kopieren“; „Als Markdown kopieren“ ohne Bildverweis ins Leere (heute `Schritt-NN.png`, das nicht mitkommt).
- **GIF für Pull Requests:** einstellbares Größenziel (etwa 10 MB) mit automatischer Verkleinerung von Breite und Bildzahl.

**Nicht-Umfang:** Hochladen, Commit oder Pull Request durch das Programm.

**Bausteine:** `GuideExport.Markdown`, `Fmt.cs`, `GifWriter.cs`, `ViewerActions.cs`, `ExportDialogs.cs`. **Kapitel:** 08 §10, 09 §6, §8d.

**Abnahme:** Zwei Exporte derselben Aufnahme sind bytegleich; GitHub-Vorschau zeigt Bilder und Hinweiskästen (Prüfung von Hand); Markdown besteht eine Markdown-Prüfung ohne Befund.

**Risiko:** Änderung bestehender Ausgaben – wer heute exportierte Markdown-Dateien pflegt, bekommt neue Namen. Die alte Form bleibt eine Exportoption für eine Übergangszeit.

### Paket 19 – Visueller Vergleich (M–L)

**Ziel:** Vorher/Nachher zweier Aufnahmen desselben Ablaufs auf einen Blick – für Reviews von Oberflächenänderungen und für Regressionen.

**Umfang:** Fenster nach [08 §5.9](08-viewer.md#59-zwei-aufnahmen-vergleichen) statt des heutigen Textfelds: Paarliste aus `StepCompare`, Bilder nebeneinander, hervorgehobene Bildunterschiede (Kann laut 08), Zeitunterschiede aus Paket 9, Filter „nur geänderte“. Ausgabe als Bildpaar je Schritt und als Markdown mit beiden Bildern für einen Pull Request; `compare --images`.

**Bausteine:** `StepCompare.cs`, `ViewerActions.Compare`, `StepImages.cs`, `Cli.cs`. **Kapitel:** 05 §17, 06 (`compare_sessions`), 08 §5.9.

**Abnahme:** T-14 zusätzlich mit Bildunterschieden: bekannte geänderte Bereiche werden hervorgehoben, unveränderte nicht; gleiche Eingabe ergibt gleiches Ergebnis in Editor, `compare` und `compare_sessions`.

**Risiko:** Fehlalarme durch Kantenglättung, Cursor und Uhrzeiten – Schwelle und Masken müssen einstellbar sein.

## 6. Vorarbeiten und Abgleich

Beim Durchgang durch den Code sind Abweichungen zwischen Grundsätzen und Umsetzung aufgefallen. Sie betreffen den Schutz und gehören vor die Pakete, die neue Quellen einführen. Je Punkt ist zu entscheiden, ob der Code oder die Spezifikation angepasst wird.

| Nr. | Abweichung | Fundstelle | Spezifikation | Empfehlung | Stand |
|---|---|---|---|---|---|
| A-1 | Vollständiger Programmpfad steht im Klartext in jedem Win32-Kontext | `Win32Detail.WindowInfo` (`o["path"]`) | [10 §3](10-datenschutz-und-sicherheit.md#3-dateninventar): Pfade unter dem Profil mit `%USERPROFILE%` | Code: Platzhalter, wie versprochen | erledigt (Paket V): `Win32Detail.ProfilePath` |
| A-2 | Befehlszeile aufgenommener Programme wird gespeichert (bereinigt) | `Win32Detail.AppInfo` (`commandLine`) | 10 §3: Befehlszeilen fremder Prozesse „bewusst nicht erhoben“ | Code: weglassen oder nur hinter „Erweiterter Programmkontext“ und als schwächer gemeldet | erledigt (Paket V): nur mit „Erweitertem Programmkontext“ (`Win32Detail.KeepCommandLine`) |
| A-3 | Listen- und Baumeinträge werden über Speicher des fremden Prozesses gelesen | `Win32Detail.Extra.cs` (`VirtualAllocEx`, `ReadProcessMemory`) | [10 §2](10-datenschutz-und-sicherheit.md#2-grundsätze) P9: kein Lesen fremden Prozessspeichers | Code: auf UI Automation und MSAA umstellen | erledigt (Paket V): Texte über UI Automation (`Win32Detail.Uia.cs`), Test `Core_DoesNotReadForeignProcessMemory` |
| A-4 | Abschaltbare Suche nach Updates (ab Werk aus) | `UpdateCheck.cs` | P2: kein Update-Check | Spezifikation: Ausnahme ausdrücklich beschreiben (ab Werk aus, nur Release-Seite, ohne Kennung) | erledigt (Paket V): Ausnahme in P2 beschrieben |
| A-5 | Durchsicht erfasst nur getippte Eingaben | `SessionView` (Schrittart `type`) | [10 §11.1](10-datenschutz-und-sicherheit.md#111-durchsicht-aller-eingaben): auch Kommentare, Ziel-Angaben, Marker | Code, in Paket 12 | – |
| A-6 | „Nie unbemerkt schwächer“ nicht gebaut; Einstellung `ClipboardContent` wirkungslos | `RecordingOptions.ProfileJson` (ohne `weakens`) | 10 §4.4 | Code, in Paket 12 | – |
| A-7 | `ui.value` ist für Terminals nicht eigens gesperrt | `Recorder.System.ApplyFocus`, `Recorder.Input.PollValue` | [03 §7.4](03-aufzeichnungskern.md#74-rückfall-und-undurchsichtige-oberflächen): Terminaltext nie als Feldinhalt | Test belegt, dass kein Konsoleninhalt entsteht; sonst Sperre | erledigt (Paket V): kein `ui.value` in Terminal, Remote-Desktop und VM |
| A-8 | Regelversion: Code 1.6.0, Spezifikation 1.5.0 | `StepModel.cs`, 05 §15.1 | – | Spezifikation nachziehen | erledigt (Paket V) |
| A-9 | Terminals gelten im Code als `partial`, in 03 als undurchsichtig | `WindowRegistry.SurfaceOf` | 03 §7.4 | Spezifikation nachziehen, Wirkung auf die Durchsicht beschreiben | erledigt (Paket V): 03 §7.4 nennt den Stand |
| A-10 | `record` gibt am Ende keinen Sitzungspfad aus; `record --stop` wartet ohne laufende Aufnahme 30 s und endet mit 1 | `UiApp`, `SingleInstance.SendStop` | 02 §8.4, §8.6 (Exit-Code 7) | Code, in Paket 8 | erledigt (Paket V): Abschlusszeile, `--json`, Exit 5 und 7 |

Außerdem bleibt offen: der Wechsel auf .NET 10 (O-08); auf dem Entwicklungsrechner ist nur das SDK 8.0.414 installiert.

## 7. Offene Entscheidungen

Maßgeblich mit Empfehlung und Frist stehen sie in [11 §9](11-roadmap-tests-risiken.md#9-offene-entscheidungen).

| Nr. | Frage | Betrifft |
|---|---|---|
| O-12 | Programmpfade und Befehlszeilen: Code an 10 §3 anpassen oder 10 §3 ändern? | A-1, A-2; Paket 10 |
| O-13 | Lesen fremden Prozessspeichers für Listen und Bäume ersetzen oder P9 einschränken? | A-3 |
| O-14 | Update-Suche als beschriebene Ausnahme von P2 oder entfernen? | A-4 |
| O-15 | Bereitschaft im Infobereich mit P1 vereinbar? | Paket 14 |
| O-16 | Wie werden ältere Programme daran gehindert, neue inhaltstragende Ereignisse ungeprüft auszugeben? | Pakete 12, 13 |
| O-17 | Erzeugter Testcode gegen das Nicht-Ziel „keine Automatisierung“ | Pakete 16, 17 |
| O-18 | Darf das Testprojekt FlaUI (oder einen Übersetzer) verwenden, um erzeugte Tests zu prüfen? | Paket 16 |
| O-19 | Sind Notizen des Agenten nur im Editor und in MCP sichtbar oder auch in Anleitungen und KI-Paket? | Paket 8 |

## 8. Vorab-Experimente

Beschreibung mit Aufbau, Erfolgskriterium und Ausweichweg in [11 §3.2](11-roadmap-tests-risiken.md#32-experimente-für-den-entwickler-fahrplan). Die Terminal-Ausgabe hängt an O-09 und ist kein eigenes Experiment.

| ID | Frage | Vor Paket |
|---|---|---|
| VE-15 | Programmende, Exit-Codes und Hänger bei erhöhten, fremden und UWP-Programmen | 10 |
| VE-16 | Einträge der Ereignisanzeige: Verzögerung, Aufbau, Lesbarkeit als Standardbenutzer | 10, 12 |
| VE-17 | Text-Schnappschuss: Dauer, Abdeckung, Wirkung auf die Elementermittlung | 11 |
| VE-18 | Zwischenablage-Inhalt lesen bei träger Quelle | 12 |
| VE-19 | Genauigkeit der Reaktionszeiten | 9 |
| VE-20 | Ressourcen in Bereitschaft | 14 |
| VE-21 | Erzeugter FlaUI-Test gegen die Test-Zielanwendung | 16 |
| VE-22 | Zuordnung UIA-Typ und Name zu Playwright-Rolle und Name | 17 |
| VE-23 | Build-Kennung: Commit in der Produktversion, PE-Zeitstempel, PDB-Kennung | 10 |

## 9. Bewusst nicht im Fahrplan

| Idee | Grund |
|---|---|
| Ticket-fertiger Fehlerbericht für GitHub, GitLab, Jira, Azure DevOps | Nicht gewählt. Vermerkt: Der Fehlerbericht nennt den Bezug (Ticketnummer) nicht. |
| Terminal-Ausgabe mitlesen | Erst nach O-09 und Regel T1: Der Leser soll schützen, bevor er aufzeichnet; er würde zudem getippte Befehle wiederholen, die prüfpflichtig oder maskiert sind. |
| Debug-Ausgaben (`OutputDebugString`) | Nur ein Zuhörer je Sitzung; jeder Aufrufer wartet auf den Leser – das bremst die getestete Anwendung. |
| Browser-Konsole und Netzwerkverkehr | Bräuchte eine Verbindung zum Browser (Debug-Schnittstelle) oder eine Erweiterung – unvereinbar mit L-1. |
| Rückwirkende Aufnahme („die letzten 30 Sekunden“) | Setzt Daueraufzeichnung voraus (P1). |
| Absturzabbilder, Debugger, ETW, `git.exe` | Adminrechte, Registry, Eingriff in die Anwendung oder Ausführen fremder Konfiguration (L-1). |
| Eingebaute KI, Versand, Hochladen | Nicht-Ziele ([00 §4](00-ueberblick.md#4-nicht-ziele)). |

## 10. Pflege

- Beginnt ein Paket, entstehen seine Anforderungen mit Kennung in den Fachkapiteln; die Spalte „Stand“ in Abschnitt 4 wechselt auf „in Arbeit“, danach auf „erledigt“, und [11 §2.1](11-roadmap-tests-risiken.md#21-stand-der-umsetzung) erhält eine Zeile.
- Ergebnisse der Vorab-Experimente kommen als Bericht unter `docs/experimente/` und ändern dieses Kapitel, wenn ein Ausweichweg greift.
- Entschiedene Fragen aus Abschnitt 7 werden in [11 §9](11-roadmap-tests-risiken.md#9-offene-entscheidungen) als entschieden vermerkt und hier gestrichen.
- Neue Vorschläge für Entwickler-Funktionen kommen als neues Paket in Abschnitt 4, nachdem der Auftraggeber sie gewählt hat.
