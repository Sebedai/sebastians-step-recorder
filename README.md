# StepRecorder

> Arbeitstitel · Status: **Version 0.9 (erste lauffähige Umsetzung)** · Spezifikation Entwurf 0.1 (2026-11-11) · siehe [Umsetzung](#umsetzung)

Ein portabler Schrittrekorder für Windows: ein besserer Nachfolger der abgekündigten Windows-Schrittaufzeichnung (PSR). Er läuft **ohne Installation** und zeichnet **alle** Klicks, Mausbewegungen, Tasteneingaben und Tastenkombinationen samt Screenshots und Bedienelement-Kontext auf. Daraus entstehen

- **Anleitungen für Menschen** – im eigenen Viewer ansehen, Bilder bearbeiten, kommentieren, mit einem Tastendruck kopieren, als HTML, Markdown, DOCX oder PDF exportieren;
- **ein lückenloses Protokoll für KI** – als KI-Paket zum Hochladen oder direkt abfragbar über einen lokalen MCP-Server.

Das Programm ist in seinen eigenen Screenshots nie zu sehen, arbeitet vollständig offline und maskiert Eingaben in Passwortfeldern.

## Kernmerkmale

| | |
|---|---|
| **Portabel** | Eine EXE, keine Adminrechte, keine Registry-Einträge, kein Netzwerk |
| **Vollständig** | Jede Eingabe im Rohprotokoll; was fehlt, steht als Lücke mit Ursache drin |
| **Bilder, die stimmen** | Vorher-Bild garantiert vor der Eingabe, Nachher-Bild nach der Reaktion, Recorder nie im Bild |
| **Flexibel** | Aufnahme von Bildschirm, Anwendung oder Bereich; Darstellung nachträglich wählbar |
| **KI-tauglich** | `trace.md` + `steps.json` + Bilder, Belegstufen je Schritt, MCP-Server für Agenten |
| **Automatisierbar** | Je Klick und Eingabe der volle Fensterkontext (ahk_class, ahk_exe, ClassNN, Steuerelement-ID und -Text, Fenster-/Client-/Bildschirmkoordinaten, Pixelfarbe, Fenstertexte, Programmversion und -rechte), Wartesignale, Erfolgsprüfungen, Wege zum Element nach Stabilität, Parameter und Wiederholungen – als AutoHotkey-v2-Skript, als AHK-KI-Paket und per MCP (`get_automation`) |
| **Datensparsam** | Passwortfelder maskiert, Privattaste, Ausschlussliste, Schwärzung und Verallgemeinerung persönlicher Werte in allen Ausgaben |
| **Praktisch** | Aufnahmeprofile, Ziel der Aufnahme und Fehler-Marker, Aufnahmen vergleichen, Abschnitte neu aufnehmen, Qualitätsprüfung vor dem Export, Volltextsuche, Support-Paket zum Zurückschicken |
| **Offen** | Open Source, dokumentierte Formate |

## Spezifikation

| Kapitel | Inhalt |
|---|---|
| [00 – Überblick](docs/spezifikation/00-ueberblick.md) | Vision, Ziele, Abgrenzung, Leitprinzipien, Glossar |
| [01 – Anforderungen](docs/spezifikation/01-anforderungen.md) | Gesamtindex, nicht-funktionale Anforderungen, Rückverfolgung |
| [02 – Architektur](docs/spezifikation/02-architektur.md) | Prozesse, Komponenten, Technik, Build |
| [03 – Aufzeichnungskern](docs/spezifikation/03-aufzeichnungskern.md) | Eingaben, Bildschirm, UI Automation, Lücken |
| [04 – Datenformat](docs/spezifikation/04-datenformat.md) | Sitzungsformat, Ereignistypen, Beispiele |
| [05 – Schritterkennung](docs/spezifikation/05-schritterkennung.md) | Schritte, Wirkung, Belegstufen, Textvorlagen |
| [06 – KI-Export und MCP](docs/spezifikation/06-ki-export-und-mcp.md) | KI-Paket, MCP-Server |
| [07 – Aufnahme-Bedienung](docs/spezifikation/07-aufnahme-bedienung.md) | Start, Aufnahmebereich, Leiste, Hotkeys |
| [08 – Viewer](docs/spezifikation/08-viewer.md) | Ansehen, Bearbeiten, Kommentieren, Kopieren |
| [09 – Anleitungs-Export](docs/spezifikation/09-anleitungs-export.md) | HTML, Markdown, DOCX, PDF |
| [10 – Datenschutz und Sicherheit](docs/spezifikation/10-datenschutz-und-sicherheit.md) | Maskierung, Schwärzung, Bedrohungsmodell |
| [11 – Roadmap, Tests, Risiken](docs/spezifikation/11-roadmap-tests-risiken.md) | Meilensteine, Experimente, Tests |

## Technik (geplant)

C# / .NET 10 (LTS) · WPF · self-contained Single-File-EXE für Windows 10 22H2 und Windows 11 (x64).

## Lizenz

Noch nicht festgelegt (Empfehlung: MIT) – siehe [Offene Entscheidungen](docs/spezifikation/11-roadmap-tests-risiken.md#9-offene-entscheidungen).

## Umsetzung

### Bauen und starten

Voraussetzung: .NET-SDK 8 mit Windows-10-SDK-Zielplattform (`net8.0-windows10.0.19041.0`, für die eingebaute Windows-Texterkennung); die Spezifikation nennt .NET 10, der Wechsel ist eine Zeile `TargetFramework`.

```bash
dotnet build StepRecorder.sln
dotnet test tests/StepRecorder.Tests
dotnet publish src/StepRecorder.App -c Release -r win-x64
```

Build-Ausgaben liegen außerhalb des (synchronisierten) Projektordners unter `%LOCALAPPDATA%\StepRecorder-build` (ARC-047). Die portable Einzel-EXE (≈ 67 MB, ohne Installation, ohne Registry) entsteht unter `…\StepRecorder-build\publish\StepRecorder\release_win-x64\StepRecorder.exe`.

| Aufruf | Wirkung |
|---|---|
| `StepRecorder.exe` | Startfenster (Profil, Ziel der Aufnahme, Ganzer Bildschirm / Eine Anwendung / Bereich, Support-Paket) |
| `StepRecorder.exe <Sitzung>` | Viewer |
| `StepRecorder.exe record [--scope screen\|app\|region] [--app x.exe] [--title …] [--goal …]` / `record --stop` | Aufnahme per Kommandozeile |
| `StepRecorder.exe export <Sitzung> --format html\|md\|docx\|pdf\|ki\|ahk\|ahk-ki [--with <Sitzung>]` | Anleitung, KI-Paket, AutoHotkey-v2-Skript bzw. AHK-KI-Paket (Kontext für KI-Assistenten, mit Bildausschnitten und Texterkennung) |
| `StepRecorder.exe validate <Sitzung>` · `compare <A> <B>` · `steps <Sitzung>` | Formatprüfung, Vergleich, Schrittliste |
| `StepRecorder.exe mcp --root <Sessions-Ordner> [--allow-edit]` | MCP-Server über stdio |

Tastenkürzel während der Aufnahme (alle `Strg+Umschalt+…`): F9 Sofortaufnahme/Pause · F10 Stopp · F12 Kommentar · F7 Marker · F1 Marker „Fehler“ · F2 Marker „Erwartet“ · F11 Schnappschuss · F8 Privattaste · F6 letzten Schritt verwerfen · F4 Leiste ein/aus.

Viewer: Das Menü „Mausmarkierung“ blendet Klickpunkt, Zug-/Haltepfad, Zielrahmen, Hinweise (Haltedauer, Tasten) und den Mausweg davor für alle Schritte ein oder aus; `H` schaltet sie beim aktuellen Schritt. Gedrückt halten und Ziehen werden mit Haltezeiten aufgezeichnet und im AutoHotkey-Skript nachgespielt.

Oberfläche: eigenes, neutrales Design (Stile in `src/StepRecorder.App/Ui/Theme.xaml`, Farben in `Theme.cs`), folgt dem hellen bzw. dunklen Windows-Farbschema; `STEPRECORDER_THEME=light|dark` erzwingt eines. Symbole aus Segoe Fluent Icons (Windows 11) bzw. Segoe MDL2 Assets (Windows 10). Für Entwickler rendert `STEPRECORDER_UISHOT=<Ordner>` alle Fenster als PNG – die Fenster selbst sind in Bildschirmfotos absichtlich unsichtbar.

MCP in Claude Code (`.mcp.json`):

```json
{ "mcpServers": { "steprecorder": { "command": "C:\\Tools\\StepRecorder\\StepRecorder.exe", "args": ["mcp", "--root", "C:\\Tools\\StepRecorder\\Sessions"] } } }
```

### Aufbau

| Projekt | Inhalt |
|---|---|
| `src/StepRecorder.Core` | Capture-Host (Hooks → gemeinsamer Speicher), Aufzeichnungssteuerung, GDI-Bildaufnahme mit Ringpuffer (Vorher/Nachher/Zwischenbilder, key/delta/same-PNG), UI Automation, Fenster-/System-/Zwischenablage-Ereignisse, Schutzfilter, Sitzungs-Writer/-Reader, Wiederherstellung, Schritterkennung, Textvorlagen (de/en), Leseschicht (Schwärzung, Verallgemeinerung, Durchsicht), Exporte (KI-Paket, HTML, Markdown, DOCX, PDF, Support-Paket), Vergleich, Validator, MCP-Server |
| `src/StepRecorder.App` | `StepRecorder.exe`: Rollenweiche (Capture-Host, MCP, CLI, Oberfläche), Startfenster, Aufnahmeleiste, Rahmen, Fenster-/Bereichsauswahl, Viewer/Editor, Einstellungen |
| `tests/StepRecorder.Tests` | Golden-Journal-Tests der Schritterkennung, Leseschicht/Schwärzung, Exporte, MCP, Mustererkennung, Integrationstest mit echter Aufnahme |

### Stand gegenüber der Spezifikation

Umgesetzt sind die Kernabläufe 1–4 (aufnehmen → ansehen → kopieren; bearbeiten → exportieren; KI-Paket/MCP; Support-Paket). Bewusste Vereinfachungen dieser Version, jeweils als nächste Ausbaustufe:

- **Bildaufnahme** nur über den GDI-Weg (Spec: Rückfallweg) mit Ringpuffer; DXGI Desktop Duplication und Windows.Graphics.Capture fehlen noch. Vorher-Bild-Garantie `beforeInput`, sonst ehrlich `atInput`.
- **Raw Input** als Gegenkanal, Hook-Neuinstallation, Herzschlag-Neustart des Hosts, Tottasten-Tabellen aus dem Layout, IME, Terminal-Passwortabfrage-Erkennung (Regeln T1–T3) und Ausweichmodus der Unsichtbarkeit sind noch nicht umgesetzt (Lücken werden aber benannt; Privattaste und Passwortfeld-Maskierung funktionieren).
- **UI Automation** über die verwaltete `System.Windows.Automation`-Schicht statt CsWin32-COM; Fokus über WinEvents. Kein `ui.snapshot`, keine `ui.toggle`/`ui.invoke`-Ereignisse.
- **Viewer**: Ansehen, Abspielen, Suche, Haupt/Alle, Vorher/Nachher, Darstellung 1/2/3, Markierungen (Pfeil, Rahmen, Hervorhebung, Text, Nummer), Bild-, Text- und Schrittschwärzung, endgültiges Schwärzen, Verallgemeinern, Kommentare, Text ändern, Ausblenden, Wichtigkeit, Reihenfolge, Rückgängig, Durchsicht, Kopierfunktionen, Export, Vergleich. Noch nicht: Galerie, Zeitleiste, Kapitel, Zusammenführen/Teilen, Abschnitt neu aufnehmen, Qualitätsprüfung Q01–Q20, Volltextsuche über alle Sitzungen im Viewer (per MCP `search` vorhanden).
- **Profile** als die drei eingebauten (Anleitung, Support-Fall, KI-Spur); kein Profileditor, kein Einstellungs-Export/-Import.
- `README_AI.md` liegt deutsch vor; die englische Fassung beginnt mit einer Kurzfassung der Regeln.
- Code-Signierung, Lizenzdatei und Release-Checkliste (M8) stehen aus.
