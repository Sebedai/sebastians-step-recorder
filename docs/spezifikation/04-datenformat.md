# 04 · Datenformat

Status: Entwurf 0.1 · Stand: 2026-11-11

Dieses Dokument ist die kanonische Definition des Sitzungsformats von StepRecorder. Es legt fest, welche Dateien eine Aufzeichnung bilden, welche Felder jedes Ereignis trägt, wie Zeit und Koordinaten gemeint sind, wie Bilder gespeichert werden und wie abgeleitete Schritte (`steps.json`) und Bearbeitungen (`edits.json`) aussehen. Alle anderen Dokumente verwenden die hier definierten Namen. Wie die Ereignisse entstehen, steht im [Aufzeichnungskern](03-aufzeichnungskern.md); wie aus ihnen Schritte werden, in der [Schritterkennung](05-schritterkennung.md); wann maskiert und geschwärzt wird, in [Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md).

## Inhalt

1. [Grundsätze](#grundsaetze)
2. [Sitzungsordner](#sitzungsordner)
3. [Gepackte Form `.steprec`](#steprec-zip)
4. [`manifest.json`](#manifest)
5. [Rohprotokoll `events.jsonl`](#journal)
6. [Ereigniskopf](#ereigniskopf)
7. [Zeitmodell](#zeitmodell)
8. [Koordinatenmodell](#koordinatenmodell)
9. [Ereignistypen](#ereignistypen)
10. [Mausbewegungs-Bündel](#mausbewegungen)
11. [Bildspeicher](#bildspeicher)
12. [`steps.json`](#steps-json)
13. [`edits.json`](#edits-json)
14. [`cache/`, `history/` und `export/`](#cache) · [Zustandsdateien außerhalb der Sitzung](#zustandsdateien)
15. [Versionierung und Migration](#versionierung)
16. [Absturzsicherheit und Integrität](#absturzsicherheit)
17. [JSON-Schema-Auszüge](#json-schema)
18. [Anhang: durchgespielte Beispiele](#beispiele) · [Abdeckung der Review-Befunde](#review-abdeckung)
19. [Anforderungen](#anforderungen)

---

<a id="grundsaetze"></a>
## 1. Grundsätze

| Nr. | Grundsatz | Folge für das Format |
|---|---|---|
| G1 | **Offene Formate.** | JSON (RFC 8259, UTF-8 ohne BOM), JSON Lines, PNG. Keine proprietären Binärformate. |
| G2 | **Drei Schichten.** Rohdaten sind unveränderlich, Ableitungen neu berechenbar, Bearbeitungen ein Overlay. | `events.jsonl`, `frames/`, `cursors/` werden nach dem Schreiben nie geändert. `steps.json` darf jederzeit neu erzeugt werden. `edits.json` verweist auf Rohdaten, verändert sie aber nicht. |
| G3 | **Selbsttragendes Journal.** | Aus `events.jsonl` und `frames/` allein lassen sich `manifest.json` und `steps.json` wiederherstellen. Alles, was dafür nötig ist, steht als Ereignis im Journal. |
| G4 | **Sensibles wird gar nicht erst gespeichert.** | Maskieren und Zurückhalten geschieht vor dem Schreiben. Das Format kennt dafür ausdrückliche Platzhalter (`text.masked`, `withheld`, `masks`). |
| G5 | **Nichts geschieht stillschweigend.** | Lücken, Unsicherheiten, ausgelassene Bilder und verworfene Messwerte sind selbst Daten (`gap`, `tErr`, `rectIssue`, `store.kind: "none"`). |
| G6 | **Toleranter Leser.** | Unbekannte Felder und Ereignistypen werden ignoriert, unbekannte Aufzählungswerte wie `unknown` behandelt. Kein Leser darf daran scheitern. |

**Schreibweisen in diesem Dokument**

- Feld- und Typnamen sind englisch in camelCase. Ereignistypen sind kleingeschrieben mit Punkt (`mouse.down`).
- Spalte „Pfl.“: ● Pflichtfeld, ○ optional, ◐ bedingt (Bedingung steht in der Beschreibung).
- `µs` bedeutet eine ganze Zahl in Mikrosekunden. Alle Zahlen sind ganze Zahlen innerhalb ±2^53, sofern nicht „Zahl“ (Gleitkomma) angegeben ist.
- Fehlt ein optionales boolesches Feld, gilt `false`. Fehlt eine optionale Liste, gilt die leere Liste.
- Zeitangaben mit Datum sind ISO 8601 in UTC mit Mikrosekunden (`2026-11-11T13:30:05.123456Z`).

---

<a id="sitzungsordner"></a>
## 2. Sitzungsordner

Eine Aufzeichnung („Sitzung“) ist ein Ordner. Der Ordnername ist frei wählbar; die Identität der Sitzung ist `sessionId` im Manifest, nicht der Ordnername. Empfohlen: `<JJJJ-MM-TT_HHmm>_<Kurztitel>`.

```
<Sitzung>/
  manifest.json            Metadaten, Umgebung, Einstellungen, Kurzbericht
  events.jsonl             Rohprotokoll, ein Ereignis je Zeile, nur anhängend geschrieben
  steps.json               abgeleitete Schritte (strukturiert, ohne gerenderte Texte)
  edits.json               Bearbeitungs-Overlay
  frames/                  Bildspeicher, inhaltsadressiert
    3f/3fa9…c1.png           Blob, benannt nach dem SHA-256 seiner Bytes
  cursors/                 Mauszeigerformen
    cur3.png
  assets/                  vom Nutzer eingebrachte Bilder (extern bearbeitet)
  cache/                   Vorschaubilder, Indizes – jederzeit löschbar
  history/                 automatische Sicherungen von edits.json (Bearbeitungsstände)
  export/                  erzeugte Ausgaben (KI-Paket, HTML, …)
    .tmp/                    Zwischendateien (extern bearbeiten, Herausziehen, Vorschau) – flüchtig
  session.lock             nur solange ein Prozess schreibend zugreift
```

| Pfad | Schicht | Pfl. | Schreibt | Bemerkung |
|---|---|---|---|---|
| `manifest.json` | Metadaten | ● | Recorder (Start/Stopp), Schritterkennung (Kurzbericht), Viewer (Titel) | Wird stets atomar ersetzt, siehe [Absturzsicherheit](#absturzsicherheit). |
| `events.jsonl` | Rohdaten | ● | ausschließlich der Recorder | Nur anhängen. Einzige Ausnahme: endgültiges Schwärzen – auf Befehl im Viewer oder beim Stopp für „Letzte Eingabe schwärzen“ ([13.6](#edits-redactions)). Zeilen werden nie entfernt – auch nicht, wenn ein Aufnahmeabschnitt verworfen wird ([13.16](#edits-sections)). |
| `frames/` | Rohdaten | ◐ | Recorder | Pflicht, sobald ein `frame`-Ereignis einen Blob nennt. |
| `cursors/` | Rohdaten | ○ | Recorder | Je Zeigerform eine PNG-Datei. |
| `steps.json` | Ableitung | ○ | Schritterkennung | Fehlt vor der ersten Ableitung und unmittelbar nach einem Absturz. |
| `steps.<Regelversion>.json` | Ableitung | ○ | Schritterkennung | Sicherung der vorherigen Ableitung nach einer Neuberechnung. |
| `edits.json` | Bearbeitung | ○ | Viewer, MCP-Server (`set_step_text`), Aufzeichnungssteuerung des Recorders (nur die vier Fälle aus [13.7](#edits-writers)) | Fehlt, solange nichts bearbeitet wurde. |
| `assets/` | Bearbeitung | ○ | Viewer | Bilddateien, auf die `edits.json` unter `assets` verweist. |
| `session.lock` | Laufzeit | ○ | Recorder, Viewer | Enthält Prozess-ID, Startzeit und Zugriffsart. Kein Teil der Sitzung. |
| `cache/` | abgeleitet | ○ | beliebig | Darf jederzeit vollständig gelöscht werden. |
| `history/` | Bearbeitung | ○ | Viewer (beim Speichern von `edits.json`) | Bearbeitungsstände: Kopien von `edits.json`, siehe [14](#history). Kein Teil der Sitzung im engeren Sinn; wird nie gepackt, nie exportiert und beim endgültigen Schwärzen gelöscht. |
| `export/` | Ausgabe | ○ | Exporte | Kein Teil des Datenmodells. |
| `export/.tmp/` | Laufzeit | ○ | Viewer, Exporte | Zwischendateien für „extern bearbeiten“, „als Datei kopieren“, Herausziehen und Exportvorschau. Sie liegen **immer hier, nie in `%TEMP%`**, enthalten nur die geschwärzte Sicht der Leseschicht und sind flüchtig, siehe [14](#cache). |

Dateien, die hier nicht genannt sind, werden von Lesern ignoriert und von Werkzeugen beim Packen nicht übernommen.

---

<a id="steprec-zip"></a>
## 3. Gepackte Form `.steprec`

Eine `.steprec`-Datei ist ein ZIP-Archiv des Sitzungsordners. Sie dient dem Weitergeben und Archivieren; gearbeitet wird im Ordner.

- ZIP nach APPNOTE 6.3, Dateinamen in UTF-8, Trennzeichen `/`. ZIP64 ist erlaubt. Keine Verschlüsselung in Formatversion 1.
- `manifest.json` ist der **erste** Eintrag, damit sich eine Sitzung ohne vollständiges Entpacken erkennen lässt (Titel und Kennung sind aus dem Archiv allein lesbar).
- PNG-Dateien werden unkomprimiert gespeichert (Methode *stored*), alle anderen Dateien mit *deflate*. Die Zeitstempel der Einträge sind fest (2026-01-01), damit gleiche Inhalte gleiche Archive ergeben.
- **Positivliste beim Packen:** Gepackt wird nur, was zur Aufnahme gehört – `manifest.json`, `events.jsonl`, `steps.json`, `edits.json` und der Inhalt von `frames/`, `cursors/` und `assets/`. **Nie** gepackt werden `cache/`, `export/` (auch nicht `export/.tmp/`), `history/` (Bearbeitungsstände), `session.lock`, `steps.<Regelversion>.json` und alles Unbekannte. Eine Option, die davon etwas hinzunimmt, gibt es nicht.
- Beim Packen entfernt das Programm aus `edits.json` die absoluten Pfade angehängter Aufnahmen (`guide.append[].path`): Sie verraten Ordnerstrukturen des Rechners und wären beim Empfänger ungültig. Eine laufende Aufnahme lässt sich nicht packen.
- **Grenzen beim Entpacken.** Ein Archiv gilt als fremde Eingabe. Es wird nie mit einem Sammelbefehl entpackt, sondern Eintrag für Eintrag; jeder Eintrag muss auf der Positivliste stehen (dieselben Namen wie oben, höchstens 260 Zeichen, mit `/` getrennt). Abgelehnt wird ein Archiv bei: Pfadsprüngen (`..`), Laufwerksangaben oder Doppelpunkt im Namen, führendem `/`, Gerätenamen (`CON`, `NUL`, `COM1` …), Namen mit unzulässigen Zeichen oder mit Punkt oder Leerzeichen am Ende, unbekannten Teilen, doppelten Einträgen, mehr als 200 000 Einträgen, einem Eintrag über 2 GiB, mehr als 20 GiB entpackt, einem Eintrag über 1 MB mit einem Kompressionsverhältnis über 250, fehlendem `manifest.json` oder `events.jsonl`, ungültigem Manifest oder einem `format` ungleich `steprec`. Die tatsächlich geschriebenen Bytes werden mitgezählt, denn die Größenangaben im Archivkopf können lügen.
- **Nichts bleibt zurück.** Entpackt wird zuerst in einen Zwischenordner (`.steprec-unpack-<8 Zeichen>`) im Zielordner und erst nach vollständigem Erfolg an den endgültigen Ort verschoben; der Ordnername folgt dem Titel (`Titel`, bei Kollision `Titel (2)` …). Bei jedem Fehler wird der Zwischenordner gelöscht.
- Die Pfade im Archiv entsprechen genau den Pfaden im Sitzungsordner, ohne umschließenden Ordner.
- Zum Bearbeiten wird das Archiv in einen Ordner entpackt. Ein Leser darf ein Archiv direkt lesend öffnen.
- Vor dem Packen einer Sitzung zur Weitergabe sind Overlay-Schwärzungen zu beachten: Sie schützen nur innerhalb des Tools. Was weitergegeben wird, muss vorher endgültig geschwärzt sein (siehe [Schwärzungen](#edits-redactions) und [Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md)). Dasselbe gilt für [Verallgemeinerungen](#edits-substitutions): Solange sie nicht endgültig angewendet sind, stehen die Originalwerte in den Rohdaten, und die Regel selbst nennt den Wert, den sie ersetzt.

---

<a id="manifest"></a>
## 4. `manifest.json`

Das Manifest wird **beim Start der Aufnahme** mit `state: "recording"` angelegt und beim Stopp abgeschlossen. Es ist die schnelle Übersicht für Sitzungsbibliothek, Viewer und MCP-Server. Es enthält nichts, was sich nicht aus dem Journal wiederherstellen ließe (G3).

### 4.1 Felder der obersten Ebene

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `format` | string | ● | Konstant `"steprec"`. |
| `schemaVersion` | string | ● | Version des Sitzungsformats, `"MAJOR.MINOR"`. Hier `"1.0"`. |
| `sessionId` | string | ● | UUID (empfohlen Version 7), klein geschrieben. |
| `title` | string | ○ | Vom Nutzer änderbarer Titel. |
| `state` | enum | ● | `recording` (Aufnahme läuft oder wurde nicht sauber beendet), `complete` (sauber beendet), `recovered` (nach Absturz wiederhergestellt). |
| `createdUtc` | Zeit | ● | Beginn der Aufnahme. |
| `endedUtc` | Zeit | ◐ | Ende der Aufnahme; Pflicht außer bei `recording`. |
| `durationUs` | µs | ◐ | Länge der Sitzungsuhr; Pflicht außer bei `recording`. |
| `timeZone` | Objekt | ● | `id` (Windows-Zeitzonenkennung), `offsetMin` (Abstand zu UTC beim Start in Minuten). |
| `parts` | Objekt[] | ● | Aufnahmeabschnitte mit Uhrenbasis, Ereignisbereich und Zweck, siehe [7.4](#aufnahmeabschnitte). Mindestens einer. |
| `recorder` | Objekt | ● | `name`, `version`, `commit` ○, `arch` (`x64` \| `arm64`, Prozessarchitektur), `elevated` (bool), `captureHost` (`process` \| `inProcess`). |
| `environment` | Objekt | ● | Systemumgebung, siehe 4.2. |
| `topology` | Objekt | ● | Monitor-Anordnung beim Start; gleicher Aufbau wie die Nutzdaten von [`display.topology`](#ev-display-topology). |
| `settings` | Objekt | ● | Wirksame Aufnahme-Einstellungen, siehe 4.3. |
| `goal` | Objekt | ○ | Ziel der Aufnahme in den Worten des Autors, siehe [4.7](#manifest-goal). Fehlt, wenn nichts angegeben wurde. |
| `profile` | Objekt | ○ | Aufnahmeprofil, mit dem gestartet wurde: Name und wirksame Einstellungen, siehe [4.8](#manifest-profile). |
| `rules` | Objekt | ○ | Regelstand der vorhandenen `steps.json`: `stepBuilderVersion`, `derivedAtUtc`, `derivedThroughSeq`. |
| `journal` | Objekt | ● | `segments` (Liste aus `file`, `firstSeq`, `lastSeq`, `bytes`, `sha256`), `events`, `lastSeq`, `lastT`. Bei `recording` dürfen Zählwerte fehlen. |
| `frames` | Objekt | ○ | `records` (Anzahl `frame`-Ereignisse), `blobs`, `keyframes`, `bytes`. |
| `summary` | Objekt | ○ | Für Listenansichten: `steps`, `primarySteps`, `apps` (Liste aus `process` und `steps`), `monitors`, `gaps`. |
| `completeness` | Objekt | ○ | Kurzfassung des Vollständigkeitsberichts, siehe 4.4. |
| `privacy` | Objekt | ○ | Zähler: `maskedRuns`, `maskedKeys`, `privacyModeSpans`, `excludedAppSpans`, `withheldValues`, `overlayRedactions`, `destructiveRedactions`. |
| `selfTest` | Objekt | ○ | Ergebnis des Unsichtbarkeits-Selbsttests: `invisibility` mit `passed` (bool), `mode` (`excluded` \| `fallbackHidden` = Ausweichmodus), `backend`. Spiegelt das `system`-Ereignis mit `kind: "selfTest"`. |
| `performance` | Objekt[] | ○ | Leistungszähler der Aufnahme, **je Aufnahmeabschnitt ein Eintrag**: `part` und die Zähler dieses Abschnitts (z. B. längster Hook-Aufruf, höchste Warteschlangentiefe, ausgelassene Bilder). Die Zähler beginnen mit jedem Abschnitt bei null und werden nie über Abschnitte summiert. Der Eintrag spiegelt `session.stop.performance`; für einen Abschnitt, der durch Absturz endete, fehlt er. Die Zählernamen legt der [Aufzeichnungskern](03-aufzeichnungskern.md) fest; Leser dürfen sie ignorieren. |
| `deviceSalt` | string | ○ | Salz der Sitzung für die Wiedererkennung von Monitoren (`deviceKey`, [9.4](#ev-display-topology)); spiegelt `session.start.deviceSalt`. |
| `files` | Objekt[] | ○ | Prüfsummen beim Abschluss: `path`, `bytes`, `sha256` für `steps.json` und `cursors/*`. Blobs in `frames/` prüfen sich über ihren Namen selbst. |
| `history` | Objekt[] | ● | Lebenslauf der Sitzung, siehe 4.5. |

### 4.2 `environment`

| Feld | Typ | Bedeutung |
|---|---|---|
| `os` | Objekt | `product` („Windows 11 Pro“), `version` (`10.0.26100.2314`), `displayVersion` (`24H2`). |
| `locale` | Objekt | `ui` und `formats` als BCP-47-Kennung (`de-DE`). |
| `keyboardLayouts` | Objekt[] | Installierte Layouts: `hkl` (8 Hex-Ziffern), `klid`, `name`. Das jeweils aktive Layout steht an jedem Tastenereignis. |
| `inputSettings` | Objekt | Momentaufnahme der Eingabe-Einstellungen, siehe unten. |
| `accessibility` | Objekt | `highContrast`, `screenReader` (bool), `textScale` (Zahl). |
| `sessionInfo` | Objekt | `remote` (bool: Aufnahme läuft in einer Remote-Sitzung), `virtualMachine` (bool oder `null`). |
| `theme` | Objekt | `apps`, `system`: `light` \| `dark`. |
| `identity` | Objekt | Nur wenn in den Einstellungen ausdrücklich eingeschaltet: `machine`, `user`. Standard: fehlt. |

`inputSettings` hält fest, was die Deutung der Rohereignisse beeinflusst. Die Schritterkennung liest diese Werte, statt eigene Annahmen zu treffen:

| Feld | Typ | Windows-Quelle | Wozu |
|---|---|---|---|
| `swapMouseButtons` | bool | `SM_SWAPBUTTON` | Zuordnung physische ↔ logische Maustaste. |
| `doubleClickTimeMs` | int | `GetDoubleClickTime` | Doppelklick-Erkennung. |
| `doubleClickWidth`, `doubleClickHeight` | int | `SM_CXDOUBLECLK`, `SM_CYDOUBLECLK` | Doppelklick-Rechteck in Pixeln. |
| `dragWidth`, `dragHeight` | int | `SM_CXDRAG`, `SM_CYDRAG` | Schwelle Klick ↔ Ziehen. |
| `wheelScrollLines`, `wheelScrollChars` | int | `SPI_GETWHEELSCROLLLINES`, `SPI_GETWHEELSCROLLCHARS` | Umrechnung Raster ↔ Zeilen. |
| `keyboardDelay`, `keyboardSpeed` | int | `SPI_GETKEYBOARDDELAY`, `SPI_GETKEYBOARDSPEED` | Auto-Repeat einordnen. |
| `hoverTimeMs` | int | `SPI_GETMOUSEHOVERTIME` | Untergrenze für Verweilen. |
| `stickyKeys`, `filterKeys`, `toggleKeys`, `mouseKeys` | bool | `SPI_GETSTICKYKEYS` usw. | Einrastfunktion und Verwandte verändern, was eine Tastenfolge bewirkt. |
| `caretBlinkTimeMs` | int | `GetCaretBlinkTime` | Blinkender Cursor als Bildänderung erkennen. |
| `lowLevelHooksTimeoutMs` | int | Registrierungswert `LowLevelHooksTimeout` | Zeitlimit, nach dem Windows einen Hook-Aufruf übergeht; hilft, verlorene Hooks einzuordnen. |

Ändert sich eine dieser Einstellungen während der Aufnahme, steht die Änderung als `system`-Ereignis mit `kind: "inputSettings"` im Journal.

### 4.3 `settings`

| Feld | Typ | Bedeutung |
|---|---|---|
| `scope` | Objekt | Aufnahmebereich beim Start; gleicher Aufbau wie die Nutzdaten von [`scope.change`](#ev-scope-change). |
| `privacy.level` | enum | `balanced` (Standard: Klartext mit Schutznetz) \| `strict` (Klartext nur in sicher unkritischen Feldern). |
| `privacy.clipboardContent` | bool | Zwischenablage-Inhalt aufzeichnen. Standard `false`. |
| `privacy.excluded` | Objekt[] | Ausschlussliste: Einträge mit `process` und/oder `titlePattern`. |
| `privacy.recordIdentity` | bool | Rechner- und Benutzername speichern. Standard `false`. |
| `privacy.maskPasswordFieldsInFrames` | bool | Erkannte Passwortfelder im Bild schwärzen. Standard `true`. |
| `privacy.storeMaskedCount` | bool | Anzahl maskierter Tastenanschläge speichern. Standard `true`; bei `false` fehlen `count`, `inSeqEnd` und `events` an `text.masked` ([9.9](#ev-text-masked)). |
| `privacy.privacyModeSuspendsFrames` | bool | „Im Privatmodus auch Bilder aussetzen“. Standard `false`: Bilder laufen im Privatmodus weiter. Bei `true` wird im Privatmodus kein Bildinhalt gespeichert – jedes `frame`-Ereignis dieser Zeit trägt `store.kind: "none"` mit `reason: "privacyMode"` –, und die Lücke `privacyMode` nennt zusätzlich den Kanal `frames` ([9.19](#ev-gap)). |
| `capture.backend` | enum | Bevorzugter Aufnahmeweg: `dda` \| `wgc` \| `gdi`. Der tatsächlich je Monitor genutzte Weg steht in der Topologie. |
| `capture.maxDeltaChain` | int | Höchstlänge einer Differenzbild-Kette. Standard 30. |
| `capture.keyframeChangeRatio` | Zahl | Ab diesem Änderungsanteil wird ein Schlüsselbild geschrieben. Standard 0,4. |
| `capture.settleQuietMs`, `capture.settleMaxMs` | int | Ruhezeit und Obergrenze für das Nachher-Bild. Standard 300 und 3000. |
| `capture.typingFrameMinGapMs` | int | Mindestabstand der Vorher-Bilder beim Tippen. Standard 250. |
| `capture.valueMaxChars` | int | Höchstlänge gespeicherter Feldinhalte. Standard 2000. |
| `capture.snapshotMaxNodes` | int | Höchstzahl Knoten je Text-Schnappschuss. Standard 300. |
| `language` | string | Oberflächensprache während der Aufnahme (`de` \| `en`). Ohne Einfluss auf die Rohdaten. |

### 4.4 `completeness` (Kurzfassung)

Die ausführliche Fassung mit Einzelnachweisen steht in [`steps.json`](#steps-completeness). Das Manifest spiegelt daraus:

| Feld | Typ | Bedeutung |
|---|---|---|
| `stepBuilderVersion` | string | Regelversion, mit der der Bericht berechnet wurde. |
| `bookkeepingOk` | bool | Jedes Eingabeereignis gehört zu genau einem Schritt, und Schritte + Leerlauf + Lücken ergeben die Sitzungsdauer. |
| `inputEvents`, `orphanInputEvents` | int | Gezählte und nicht zugeordnete Eingabeereignisse. |
| `actionSteps`, `confirmed`, `inputOnly`, `unknown` | int | Schritte mit Nutzereingabe und ihre Verteilung auf die Belegstufen. |
| `confirmedRatio` | Zahl | `confirmed / actionSteps`. |
| `gaps` | int | Anzahl Lücken. |
| `gapsUs` | µs | Gesamtdauer der Lücken. |
| `gapsByCause` | Objekt | Anzahl je Ursache, einschließlich `maskedInput`. |
| `framesDropped` | int | Bilder, die nicht gespeichert werden konnten. |

### 4.5 `history`

Jeder Eintrag: `atUtc`, `action`, `tool` (Name und Version), `detail` ○ (Objekt).

| `action` | Wann |
|---|---|
| `created` | Manifest beim Aufnahmestart angelegt. |
| `finalized` | Aufnahme sauber beendet. |
| `recovered` | Sitzung nach Absturz wiederhergestellt; `detail` nennt `lastValidSeq` und `discardedBytes`. |
| `derived` | `steps.json` erzeugt oder neu berechnet; `detail` nennt alte und neue Regelversion. |
| `appended` | Neuer Aufnahmeabschnitt angehängt. |
| `redactedDestructive` | Endgültig geschwärzt (an Ort und Stelle); `detail` nennt den Anlass (`trigger`: `viewer` \| `lastInput` = beim Stopp aus „Letzte Eingabe schwärzen“) sowie die Anzahl betroffener Ereignisse und Blobs, aber nie den geschwärzten Inhalt. |
| `sanitizedCopy` | Diese Sitzung ist eine bereinigte Kopie: Sie wurde aus der geschwärzten Sicht einer anderen Sitzung geschrieben und hat eine eigene `sessionId`. `detail` nennt nur Zählwerte. |
| `migrated` | Aus einer älteren Formatversion überführt; `detail` nennt Quell- und Zielversion. |

### 4.6 Beispiel

```json
{
  "format": "steprec",
  "schemaVersion": "1.0",
  "sessionId": "019328a4-6b1e-7c3a-9d52-0f4e8a1b77c0",
  "title": "Datei im Editor speichern",
  "state": "complete",
  "createdUtc": "2026-11-11T13:30:05.123456Z",
  "endedUtc": "2026-11-11T13:31:31.123456Z",
  "durationUs": 86000000,
  "timeZone": { "id": "W. Europe Standard Time", "offsetMin": 60 },
  "parts": [
    { "part": 0, "startUtc": "2026-11-11T13:30:05.123456Z", "endUtc": "2026-11-11T13:31:31.123456Z",
      "tStart": 0, "tEnd": 86000000, "qpcFrequency": 10000000, "qpcAtStart": 8123456789012, "tickCountAtStart": 812345678 }
  ],
  "recorder": { "name": "StepRecorder", "version": "1.0.0", "arch": "x64", "elevated": false, "captureHost": "process" },
  "environment": {
    "os": { "product": "Windows 11 Pro", "version": "10.0.26100.2314", "displayVersion": "24H2" },
    "locale": { "ui": "de-DE", "formats": "de-DE" },
    "keyboardLayouts": [ { "hkl": "04070407", "klid": "00000407", "name": "Deutsch" } ],
    "inputSettings": { "swapMouseButtons": false, "doubleClickTimeMs": 500, "doubleClickWidth": 4, "doubleClickHeight": 4,
      "dragWidth": 4, "dragHeight": 4, "wheelScrollLines": 3, "wheelScrollChars": 3, "keyboardDelay": 1, "keyboardSpeed": 31,
      "hoverTimeMs": 400, "stickyKeys": false, "filterKeys": false, "toggleKeys": false, "mouseKeys": false, "caretBlinkTimeMs": 530 },
    "accessibility": { "highContrast": false, "screenReader": false, "textScale": 1.0 },
    "sessionInfo": { "remote": false, "virtualMachine": false },
    "theme": { "apps": "light", "system": "dark" }
  },
  "topology": {
    "epoch": 0, "reason": "initial", "virtual": { "x": 0, "y": 0, "w": 2560, "h": 1440 },
    "monitors": [ { "id": "M1", "name": "\\\\.\\DISPLAY1", "label": "DELL U2722D", "primary": true, "vx": 0, "vy": 0, "w": 2560, "h": 1440,
      "work": { "x": 0, "y": 0, "w": 2560, "h": 1392 }, "dpi": 96, "scale": 1.0, "rotation": 0, "refreshHz": 60, "hdr": false, "backend": "dda" } ]
  },
  "settings": {
    "scope": { "mode": "screen", "monitors": "active" },
    "privacy": { "level": "balanced", "clipboardContent": false, "excluded": [ { "process": "KeePass.exe" } ],
      "recordIdentity": false, "maskPasswordFieldsInFrames": true, "storeMaskedCount": true, "privacyModeSuspendsFrames": false },
    "capture": { "backend": "dda", "maxDeltaChain": 30, "keyframeChangeRatio": 0.4, "settleQuietMs": 300, "settleMaxMs": 3000,
      "typingFrameMinGapMs": 250, "valueMaxChars": 2000, "snapshotMaxNodes": 300 },
    "language": "de"
  },
  "profile": { "name": "Anleitung", "id": "guide", "builtIn": true,
    "effective": { "scopeMode": "screen", "privacyLevel": "balanced", "clipboardContent": false,
      "afterStop": { "action": "viewer" }, "showGoalFields": false } },
  "rules": { "stepBuilderVersion": "1.0.0", "derivedAtUtc": "2026-11-11T13:31:31.540000Z", "derivedThroughSeq": 1756 },
  "journal": { "segments": [ { "file": "events.jsonl", "firstSeq": 1, "lastSeq": 1756, "bytes": 412877,
      "sha256": "9b1f…e07a" } ], "events": 1756, "lastSeq": 1756, "lastT": 86000000 },
  "frames": { "records": 31, "blobs": 24, "keyframes": 2, "bytes": 2914332 },
  "summary": { "steps": 7, "primarySteps": 7, "apps": [ { "process": "explorer.exe", "steps": 2 }, { "process": "notepad.exe", "steps": 5 } ],
    "monitors": [ "M1" ], "gaps": 0 },
  "completeness": { "stepBuilderVersion": "1.0.0", "bookkeepingOk": true, "inputEvents": 81, "orphanInputEvents": 0,
    "actionSteps": 7, "confirmed": 7, "inputOnly": 0, "unknown": 0, "confirmedRatio": 1.0,
    "gaps": 0, "gapsUs": 0, "gapsByCause": {}, "framesDropped": 0 },
  "privacy": { "maskedRuns": 0, "maskedKeys": 0, "privacyModeSpans": 0, "excludedAppSpans": 0, "withheldValues": 0,
    "overlayRedactions": 0, "destructiveRedactions": 0 },
  "selfTest": { "invisibility": { "passed": true, "mode": "excluded", "backend": "dda" } },
  "history": [
    { "atUtc": "2026-11-11T13:30:05.123456Z", "action": "created", "tool": "StepRecorder 1.0.0" },
    { "atUtc": "2026-11-11T13:31:31.123456Z", "action": "finalized", "tool": "StepRecorder 1.0.0" },
    { "atUtc": "2026-11-11T13:31:31.540000Z", "action": "derived", "tool": "StepRecorder 1.0.0", "detail": { "to": "1.0.0" } }
  ]
}
```

Das Beispiel beschreibt dieselbe Sitzung wie [Anhang A](#beispiel-a) und [Beispiel A des KI-Exports](06-ki-export-und-mcp.md#13-beispiel-a-datei-im-editor-speichern): 86 Sekunden, sieben Schritte in zwei Anwendungen, 81 Eingabeereignisse, 31 Bildereignisse, von denen 24 eigene Bilddaten tragen und 7 auf ein vorhandenes Bild verweisen. Aufgenommen wurde mit dem mitgelieferten Profil „Anleitung“ und ohne Ziel-Angaben; `goal` fehlt deshalb.

<a id="manifest-goal"></a>
### 4.7 Ziel der Aufnahme (`goal`)

Beim Start kann der Autor festhalten, **was die Aufnahme zeigen soll**. Die Angaben sind freiwillig – das Startfenster verlangt sie nie – und sie sind **Aussagen des Autors über seine Absicht, keine Beobachtungen des Recorders**. Die Schritterkennung liest sie nicht; sie ändern weder Schritte noch Belegstufen.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `description` | string | ○ | „Was wird gezeigt?“ – Zweck der Aufnahme. Höchstens 2000 Zeichen. |
| `expected` | string | ○ | „Erwartetes Ergebnis“ – was am Ende zu sehen sein soll. Höchstens 2000 Zeichen. |
| `reference` | string | ○ | „Bezug“ – freier Verweis, etwa eine Ticketnummer. Höchstens 200 Zeichen. |
| `statedBy` | enum | ○ | Wer die Angaben formuliert hat: `author` (Standard; das Feld fehlt dann) oder `agent` – ein KI-Agent hat die Aufnahme über `request_recording` angefragt, der Mensch hat zugestimmt ([06](06-ki-export-und-mcp.md#1910-request_recording-und-get_recording_request-nur-mit---allow-requests)). Dann ist `description` der Text des Agenten (bereinigt, bis 500 Zeichen) und `expected` sein erwartetes Ergebnis. Der Wert wird nie vom Viewer gesetzt oder geändert. |

Regeln:

- Bei `statedBy: "agent"` ist das Ziel weiterhin eine Angabe über die Absicht, keine Beobachtung; es stammt aber nicht vom Aufnehmenden. Alle Ausgaben kennzeichnen das (KI-Paket, `get_session`, Kopf von `trace.md`). Ändert der Autor `description` im Viewer, bleibt `statedBy` im Manifest stehen; die Ausgaben nennen den Agenten weiter als Urheber der ursprünglichen Angabe.
- Alle drei Felder sind wahlfrei. Ein nicht ausgefülltes Feld fehlt; leere Zeichenketten stehen weder im Manifest noch im Journal. Ist nichts ausgefüllt, fehlt `goal` ganz.
- Im Journal steht `goal` im `session.start` des **ersten** Abschnitts ([9.2](#ev-session)); das Manifest spiegelt es (G3).
- Der Autor kann die Angaben im Viewer ändern. Das geschieht im Overlay (`edits.goal`, [13.12](#edits-goal)); Manifest und Journal bleiben unverändert.
- Die Angaben sind **Autorentext** wie Kommentare: Sie gehören zur Durchsicht ([13.10](#edits-review)), lassen sich als Textstelle schwärzen (Fundstelle: das `session.start`-Ereignis mit Feldpfad `goal.description`, `goal.expected` oder `goal.reference`) und unterliegen den Verallgemeinerungen ([13.14](#edits-substitutions)).
- Zeichen zählen als Unicode-Codepunkte. Zeilenumbrüche sind in `description` und `expected` erlaubt, in `reference` nicht.

```json
{ "goal": { "description": "Rechnung als PDF exportieren und per E-Mail versenden.",
    "expected": "Die E-Mail mit der PDF-Datei liegt im Postausgang.", "reference": "TICKET-4711" } }
```

<a id="manifest-profile"></a>
### 4.8 Aufnahmeprofil (`profile`)

Ein Profil ist eine benannte Voreinstellung für die Aufnahme ([Bedienung](07-aufnahme-bedienung.md)). Wo Profile gespeichert sind, gehört nicht zum Sitzungsformat. Die Sitzung hält nur fest, **mit welchem Profil** sie entstand und **was dabei tatsächlich galt** – damit sich später nachvollziehen lässt, wie aufgenommen wurde, auch wenn es das Profil nicht mehr gibt oder es geändert wurde.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `name` | string | ● | Name des Profils, wie er beim Start angezeigt wurde. |
| `id` | string | ○ | Sprachunabhängige Kennung eines mitgelieferten Profils: `guide` („Anleitung“), `support` („Support-Fall“), `aiTrace` („KI-Spur“). Fehlt bei eigenen Profilen. |
| `builtIn` | bool | ○ | `true` bei einem mitgelieferten Profil. |
| `effective` | Objekt | ● | Die beim Start **wirksamen** Werte der Profil-Einstellungen – nach dem Profil und nach allem, was der Nutzer im Startfenster noch geändert hat. Felder siehe unten. |
| `weakens` | string[] | ○ | Einstellungen, in denen diese Aufnahme **schwächer schützt** als die Standardeinstellungen des Nutzers zum Startzeitpunkt: `privacyLevel`, `clipboardContent`. Fehlt oder leer = nicht schwächer. Die Startmeldung nennt genau diese Punkte ([Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md)). |

Felder von `effective`:

| Feld | Typ | Bedeutung |
|---|---|---|
| `scopeMode` | enum | Art des Aufnahmebereichs: `screen` \| `app` \| `region`. |
| `privacyLevel` | enum | Schutzstufe: `balanced` \| `strict`. |
| `clipboardContent` | bool | Inhalt der Zwischenablage aufzeichnen. |
| `capture` | Objekt | Bildeinstellungen, soweit das Profil sie setzt – gleiche Namen wie unter `settings.capture`. |
| `excludedAdded` | Objekt[] | Ergänzungen der Ausschlussliste durch das Profil – gleiche Form wie `settings.privacy.excluded`. |
| `afterStop` | Objekt | Was nach dem Stopp geschieht: `action` (`viewer`, Standard \| `export` \| `supportPackage`), dazu bei `export` das Feld `export` mit Format, Vorlage und Optionen nach dem [Anleitungs-Export](09-anleitungs-export.md). |
| `showGoalFields` | bool | Ziel-Felder im Startfenster anzeigen. |

Regeln:

- Maßgeblich für jeden, der die Aufnahme **deutet**, bleibt `settings`. `profile.effective` wiederholt daraus, was das Profil betrifft, und ergänzt, was nur das Profil kennt (`afterStop`, `showGoalFields`). Beide dürfen sich nicht widersprechen: `scopeMode` = `settings.scope.mode`, `privacyLevel` = `settings.privacy.level`, `clipboardContent` = `settings.privacy.clipboardContent`, jeder Wert unter `capture` = der gleichnamige unter `settings.capture`, und jeder Eintrag aus `excludedAdded` steht in `settings.privacy.excluded` (Prüfung F21).
- Im Journal steht `profile` im `session.start` jedes Abschnitts, der mit einem Profil gestartet wurde; das Manifest zeigt das des ersten Abschnitts.
- Ein Profil kann die Schutzstufe nie unbemerkt senken: Schützt die Aufnahme schwächer als der Standard, steht das in `weakens` – und damit dauerhaft in der Sitzung.
- `profile` fehlt bei Sitzungen, die ohne Profil entstanden. Solche Sitzungen sind gültig.

```json
{ "profile": { "name": "Support-Fall", "id": "support", "builtIn": true,
    "effective": { "scopeMode": "app", "privacyLevel": "strict", "clipboardContent": false,
      "capture": { "typingFrameMinGapMs": 250 }, "excludedAdded": [ { "process": "KeePassXC.exe" } ],
      "afterStop": { "action": "supportPackage" }, "showGoalFields": true },
    "weakens": [] } }
```

(Die Werte sind beispielhaft; was die mitgelieferten Profile enthalten, legt die [Bedienung](07-aufnahme-bedienung.md) fest.)

---

<a id="journal"></a>
## 5. Rohprotokoll `events.jsonl`

### 5.1 Dateiregeln

- UTF-8 ohne BOM. Jede Zeile ist genau ein JSON-Objekt, abgeschlossen mit einem Zeilenvorschub (LF, `0x0A`). Keine Leerzeilen, keine Kommentare, keine Einrückung.
- Eine Zeile ist höchstens 1 MiB lang. Größere Inhalte werden vorher begrenzt (`valueMaxChars`, `snapshotMaxNodes`).
- Die Reihenfolge der Felder ist frei; empfohlen wird `seq`, `t`, `type` vorn.
- Die Datei wird **nur angehängt**. Zeilen werden nie umsortiert und nie entfernt – auch nicht beim endgültigen Schwärzen (dort werden Feldinhalte geleert, siehe [Schwärzungen](#edits-redactions)) und auch nicht, wenn ein Aufnahmeabschnitt verworfen wird: Das Journal wird **nie gekürzt**; ein verworfener Abschnitt wird markiert ([13.16](#edits-sections)).
- Das Journal kann aus mehreren Dateien bestehen (`manifest.journal.segments`, in dieser Reihenfolge zu lesen). Schreiber der Formatversion 1 erzeugen genau ein Segment `events.jsonl`; Leser müssen mehrere Segmente verarbeiten können.

### 5.2 Sitzungskopf im Journal

Damit das Journal selbsttragend ist (G3), beginnt jeder Aufnahmeabschnitt mit dieser Folge, bevor das erste Eingabeereignis geschrieben wird:

1. `session.start` – Formatversion, Sitzungskennung, Uhrenbasis, Einstellungen
2. `system` mit `kind: "environment"` – Systemumgebung
3. `display.topology` – Monitor-Anordnung (Epoche)
4. `keystate.sync` – Tastenzustand
5. `scope.change` – Aufnahmebereich
6. `window.info` und `window.foreground` – das Fenster im Vordergrund
7. `frame` mit `trigger: "sessionStart"` – ein Schlüsselbild je aufgenommenem Monitor

Ein sauber beendetes Journal endet mit `session.stop`.

---

<a id="ereigniskopf"></a>
## 6. Ereigniskopf

Jedes Ereignis trägt diese gemeinsamen Felder; die typspezifischen Felder stehen daneben auf derselben Ebene.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `seq` | int ≥ 1 | ● | Laufende Nummer in Schreibreihenfolge. Lückenlos: jede Zeile hat `seq` der vorigen + 1, auch über Abschnitte hinweg. |
| `t` | µs | ● | **Zeitpunkt des Geschehens** auf der Sitzungsuhr (bestmögliche Schätzung). |
| `type` | string | ● | Ereignistyp, siehe [Ereignistypen](#ereignistypen). |
| `tObs` | µs | ○ | **Zeitpunkt der Beobachtung**: wann der Recorder die Information erhalten hat. Fehlt, wenn gleich `t`. |
| `tSrc` | enum | ○ | Herkunft von `t`, siehe [Zeitmodell](#zeitmodell). Standard: `hook` für `mouse.*`, `key.*`, `text.masked`; sonst `obs`. |
| `tErr` | µs | ○ | Geschätzte Unsicherheit von `t` (±). |
| `ref` | int | ○ | `seq` des Ereignisses, auf das sich dieses bezieht (Anreicherung, Auslöser). |
| `src` | enum | ○ | Kanal, über den die Information kam: `hook`, `raw`, `uia`, `msaa`, `win32`, `winevent`, `dda`, `wgc`, `gdi`, `user`, `recorder`. |
| `ep` | int | ◐ | Topologie-Epoche. Pflicht bei jedem Ereignis, das Koordinaten enthält. |
| `inSeq` | int | ◐ | **Eingabenummer.** Laufende Nummer der Hook-Ereignisse über beide Kanäle; sie gibt die physische Reihenfolge wieder – unabhängig davon, wann ein Ereignis geschrieben wurde. **Pflicht** bei `mouse.move`, `mouse.down`, `mouse.up`, `mouse.wheel`, `key.down`, `key.up` und `text.masked`. Bei `mouse.move` ist es die Nummer des ersten Messwerts (das Bündel belegt `n` Nummern, siehe [Mausbewegungs-Bündel](#mausbewegungen)), bei `text.masked` die der ersten zurückgehaltenen Taste. Kein `inSeq` haben `mouse.cursor` und aus dem zweiten Kanal ergänzte Ereignisse (`recovered`). Wer die Nummer vergibt und was die Nummernfolge überspringt, steht unter der Tabelle. |
| `self` | bool | ○ | Das Ereignis betrifft die Bedienung des Recorders selbst: Hotkeys des Recorders (`key.*`) und Fenster des Recorders (`window.*`). Solche Ereignisse werden nie zu Nutzerschritten. Eingaben **in** Fenster des Recorders werden gar nicht als Eingabeereignisse geschrieben; ihre Zeitspanne steht als `gap` mit `cause: "recorderInteraction"`. |
| `redacted` | string[] | ○ | Pfade der Felder dieser Zeile, deren Inhalt endgültig geschwärzt wurde (z. B. `"el.value"`). |
| `substituted` | string[] | ○ | Pfade der Felder dieser Zeile, in denen ein Wert durch einen Platzhalter ersetzt wurde ([Verallgemeinerungen](#edits-substitutions)). Im Journal steht das Feld nach endgültigem Anwenden; in der Sicht der Leseschicht auch für Regeln, die nur im Overlay stehen. |

Regeln:

- `ref` zeigt immer auf ein Ereignis mit **kleinerem** `seq`.
- Jede Fensterkennung `win` muss vor ihrer ersten Verwendung durch ein `window.info` eingeführt worden sein.
- Namen, die mit `x_` beginnen, sind für Erweiterungen Dritter reserviert, ebenso Typen, die mit `x.` beginnen. Der Recorder selbst verwendet sie nie.

<a id="eingabenummer"></a>
**Eingabenummer (`inSeq`) – Vergabe und Lücken**

| Punkt | Festlegung |
|---|---|
| Wer vergibt | Die **Eingabeverarbeitung im Hauptprozess**, wenn sie die Sätze aus dem Ring des Capture-Hosts entnimmt: in dessen Reihenfolge und erst **nach** der Ausschlussprüfung über den Fenster-Stempel ([Aufzeichnungskern, 2.4](03-aufzeichnungskern.md#zeitmodell)). Der Capture-Host bestimmt die Reihenfolge, vergibt aber selbst keine Nummern. |
| Nummer verbraucht, Ereignis nicht geschrieben | Maskierte Tasten, Eingaben außerhalb des Aufnahmebereichs und die Bedienung des Recorders. Nach einem Ring-Überlauf rückt die Nummer um die Zahl der verlorenen Sätze vor. Jede so fehlende Nummer ist durch ein `text.masked` oder eine Lücke erklärt (Prüfung F18). |
| Keine Nummer | Eingaben, deren Fenster-Stempel – bei Maustasten und Rad auch das Fenster unter dem Zeiger – zu einer **ausgeschlossenen Anwendung** gehört. Die Nummernfolge läuft über eine Lücke `excludedApp` nahtlos weiter und verrät so nicht, wie viel dort geschah. Sätze aus dem zweiten Kanal (Raw Input) werden getrennt gezählt und verbrauchen ebenfalls keine Nummer. |
| Zufälliger Zuschlag | Nur wenn das Speichern der Anzahl maskierter Tasten abgeschaltet ist: Dann rückt die Nummer an **jeder Grenze** einer zurückgehaltenen Folge um einen zufälligen Betrag vor ([9.9](#ev-text-masked)). |
| Über Aufnahmeabschnitte | Die Nummer **läuft fort**: Ein weiterer Abschnitt beginnt oberhalb der höchsten Nummer, die das Journal bis dahin nennt. Keine Nummer kommt in einer Sitzung zweimal vor – auch nicht in einem verworfenen Abschnitt. |

Die Nummern sind in jedem Fall streng aufsteigend in der Reihenfolge der Hook-Aufrufe. Lückenlos sind sie im Journal nur dort, wo jede vergebene Nummer auch geschrieben wurde.

---

<a id="zeitmodell"></a>
## 7. Zeitmodell

### 7.1 Sitzungsuhr

`t` zählt Mikrosekunden seit dem Start der Sitzung. Grundlage ist der hochauflösende Zähler von Windows (`QueryPerformanceCounter`):

```
t = tStart(Abschnitt) + (qpc − qpcAtStart) · 1 000 000 / qpcFrequency      (abgerundet)
```

`qpcAtStart` und `qpcFrequency` stehen in `session.start` und in `manifest.parts`. Die Sitzungsuhr läuft während einer Pause weiter; die Pause ist eine Lücke (`gap` mit `cause: "paused"`), kein Zeitsprung.

### 7.2 Geschehen und Beobachtung

Viele Informationen treffen später ein, als sie gelten: Fensterereignisse 10–500 ms, Element-Ermittlung bis zum Zeitlimit, Bilder nach dem Kodieren. Deshalb trägt jedes Ereignis zwei Zeiten:

- `t` – wann es **geschah**. Danach wird sortiert und zugeordnet.
- `tObs` – wann der Recorder es **erfuhr**. Dient nur der Diagnose und der Bewertung der Verlässlichkeit.

Die **Zeitachse** einer Sitzung ist die stabile Sortierung aller Ereignisse nach (`t`, `seq`). Keine Regel der Schritterkennung darf sich auf `tObs` oder auf die bloße Reihenfolge der Zeilen stützen.

| `tSrc` | `t` stammt aus | Genauigkeit |
|---|---|---|
| `hook` | Zählerstand im Hook-Aufruf | Mikrosekunden; die Eingabe selbst liegt weniger als 1 ms zurück |
| `raw` | Zählerstand bei Verarbeitung der Raw-Input-Nachricht | wenige Millisekunden |
| `present` | Darstellungszeit des Bildes (`LastPresentTime` der Desktop Duplication, ebenfalls ein Zählerstand) | Mikrosekunden |
| `sys` | Ereigniszeit des Systems in Millisekunden (`GetTickCount`-Basis, z. B. bei WinEvents), auf die Sitzungsuhr umgerechnet | ±16 ms; `tErr` ist zu setzen |
| `obs` | Zeitpunkt der Beobachtung – es gibt keine bessere Quelle | Verzögerung unbekannt |
| `ref` | übernommen vom Ereignis in `ref` | wie dort |
| `est` | geschätzt (z. B. aus dem zweiten Eingabekanal rekonstruiert); `tErr` ist Pflicht | laut `tErr` |

Für `element` und `ui.snapshot` ist `t` der Beginn der Abfrage (der Zustand, den das Ergebnis beschreibt, liegt zwischen `t` und `tObs`).

### 7.3 Garantien

- **Je Kanal geordnet:** Mausereignisse unter sich und Tastenereignisse unter sich haben – bei `tSrc: "hook"` ohne `recovered: true` – in `seq`-Reihenfolge nicht fallende `t`.
- **Zwischen den Kanälen nicht:** Tasten werden vor dem Schreiben kurz zurückgehalten, bis ihr Eingabekontext feststeht ([Datenschutz](10-datenschutz-und-sicherheit.md)). Sie können deshalb später im Journal stehen als Mausereignisse, die nach ihnen geschahen. Die wahre Reihenfolge ergibt sich aus `t`, bei gleichem `t` aus `inSeq`.
- **Bezug vor Anreicherung:** Ein Ereignis mit `ref` wird nie vor seinem Bezugsereignis geschrieben. Bilder und Elementdaten zu einer zurückgehaltenen Taste warten also mit.
- Nachträglich ergänzte Ereignisse (aus dem zweiten Eingabekanal) tragen `recovered: true`, `tSrc: "raw"` oder `"est"`, haben kein `inSeq` und dürfen ein kleineres `t` haben als ihre Vorgänger im Journal.

<a id="aufnahmeabschnitte"></a>
### 7.4 Aufnahmeabschnitte

Eine Sitzung besteht aus einem oder mehreren Abschnitten (`parts`). Ein neuer Abschnitt entsteht, wenn an eine bestehende Sitzung eine weitere Aufnahme angehängt wird – als Fortsetzung oder als **Neuaufnahme**, die einen früheren Teil ersetzen soll. Jeder Abschnitt hat eine eigene Uhrenbasis und beginnt mit dem vollständigen [Sitzungskopf](#journal). Die Sitzungsuhr läuft über Abschnitte hinweg stetig weiter: `tStart` eines Abschnitts ist `tEnd` des vorigen. Der tatsächliche zeitliche Abstand ergibt sich aus `startUtc` und `endUtc` und wird in keiner Dauer mitgezählt.

Die Wanduhrzeit eines Ereignisses ist `startUtc(Abschnitt) + (t − tStart)`. `session.checkpoint` liefert Stützstellen, an denen sich eine Abweichung zwischen Zähler und Systemuhr ablesen lässt.

Je Abschnitt steht im Manifest (`parts[]`):

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `part` | int | ● | Abschnittsnummer ab 0, fortlaufend. In `edits.replacements[]` heißt dieselbe Nummer `section`. |
| `startUtc`, `endUtc` | Zeit | ● / ◐ | Wanduhrzeit von Beginn und Ende; `endUtc` fehlt, solange der Abschnitt läuft. |
| `tStart`, `tEnd` | µs | ● / ◐ | Beginn und Ende auf der Sitzungsuhr. |
| `qpcFrequency`, `qpcAtStart`, `tickCountAtStart` | int | ● | Uhrenbasis des Abschnitts. |
| `firstSeq`, `lastSeq` | int | ○ | Erstes und letztes Ereignis des Abschnitts im Journal. `firstSeq` ist das `seq` seines `session.start`. |
| `purpose` | enum | ○ | Zweck: `initial` (Abschnitt 0) \| `append` (Fortsetzung; Standard für spätere Abschnitte) \| `replacement` (Neuaufnahme, die einen früheren Bereich ersetzen soll). |
| `replaces` | Objekt | ◐ | Nur bei `purpose: "replacement"` (dort Pflicht): der Bereich, für den die Neuaufnahme gedacht ist – `section`, `fromSeq`, `toSeq`. |
| `discarded` | bool | ○ | `true`, wenn der Abschnitt mit `session.stop` und `reason: "discarded"` endete – der Autor hat ihn beim Aufnehmen verworfen ([13.16](#edits-sections)). |

`purpose` und `replaces` stammen aus dem `session.start` des Abschnitts ([9.2](#ev-session)). Ein Abschnitt mit `purpose: "replacement"` **ersetzt für sich noch nichts**: Wirksam wird die Ersetzung erst durch einen Eintrag in `edits.replacements[]` ([13.13](#edits-replacements)). Ohne einen solchen Eintrag gilt der Abschnitt als nicht verwendete Neuaufnahme.

**Was über die Abschnittsgrenze fortläuft** – nie neu vergeben, nie wiederverwendet:

| Größe | Regel |
|---|---|
| `seq` | lückenlos weiter |
| Sitzungsuhr `t` | beginnt beim Ende des vorigen Abschnitts |
| Eingabenummer `inSeq` | beginnt oberhalb der höchsten im Journal genannten Nummer ([6](#eingabenummer)) |
| Topologie-Epoche | nächste Nummer, auch bei unveränderter Anordnung (`reason: "partStart"`) |
| Monitor-Kennungen | Derselbe Monitor behält seine Kennung, wenn er über `deviceKey` wiedererkannt wird; das Salz dafür steht im Sitzungskopf (`deviceSalt`) |
| Kennungen von Fenstern, Bildern, Lücken, Kommentaren, Markern, Zwischenablage-Inhalten und Zeigerformen | laufen weiter; Fenster werden im neuen Abschnitt neu eingeführt (`window.info`) |

**Neu beginnen** mit jedem Abschnitt: die Uhrenbasis, die Bildketten (das erste Bild je Monitor ist ein Schlüsselbild; kein Differenzbild verweist in einen früheren Abschnitt), alle mitgeführten Zustände und die Leistungszähler. Keine Lücke bleibt über die Grenze offen. Dauern und Abstände werden nie über eine Abschnittsgrenze hinweg gerechnet.

---

<a id="koordinatenmodell"></a>
## 8. Koordinatenmodell

### 8.1 Grundregel

Alle Koordinaten sind **physische Pixel** und **monitorrelativ**: Ursprung ist die linke obere Ecke des Monitors, so wie er dargestellt wird (also nach Drehung), x nach rechts, y nach unten.

| Form | Darstellung | Verwendung |
|---|---|---|
| Punkt | Felder `m`, `x`, `y` direkt am Ereignis | Mausereignisse, Treffpunkt einer Element-Ermittlung |
| Rechteck | Objekt `{ "m": "M1", "x": 1012, "y": 688, "w": 88, "h": 26 }` | Fenster, Elemente, Bereiche |
| Epoche | Feld `ep` am Ereignis | macht die Koordinaten eindeutig |

Ein Rechteck darf über den Monitor hinausragen (negative Werte, `x + w` größer als die Monitorbreite) – etwa ein Fenster, das zwei Monitore überspannt. Es wird dann relativ zu dem Monitor angegeben, auf dem sein größter sichtbarer Teil liegt.

### 8.2 Monitor-Kennung und Epochen

- Jeder physische Monitor erhält beim ersten Auftreten eine sitzungsweit **stabile Kennung** `M1`, `M2`, … Sie hängt am Gerät, nicht an der Windows-Nummer (`\\.\DISPLAY2`) und nicht am Monitor-Handle – beide ändern sich beim Umstecken.
- Jede Anordnung der Monitore ist eine **Epoche**, beschrieben durch ein [`display.topology`](#ev-display-topology)-Ereignis. Epoche 0 gilt ab Sitzungsstart; bei jeder Änderung (Monitor an/ab, Auflösung, Drehung, Skalierung, Hauptmonitor) beginnt die nächste.
- Koordinaten gelten immer innerhalb der Epoche in `ep`. Eine Aufnahme endet nicht, wenn sich die Anordnung ändert.

Umrechnung in Koordinaten des virtuellen Desktops der Epoche (nur wenn benötigt):

```
vx = monitor.vx + x        vy = monitor.vy + y
```

### 8.3 Bezug zu Bildern

Jedes gespeicherte Vollbild hat genau die Pixelmaße seines Monitors in der jeweiligen Epoche und ist bereits in Darstellungsrichtung gedreht. Daraus folgt die wichtigste Invariante des Formats:

> **Bildpixel (x, y) eines Bildes von Monitor M = monitorrelative Koordinate (x, y) auf M.**

Das gilt auch bei eingeschränktem Aufnahmebereich: Das Bild behält Monitorgröße, Flächen außerhalb des Bereichs sind schwarz gefüllt und als solche ausgewiesen (siehe [`frame`](#ev-frame)). Verkleinerte oder zugeschnittene Bilder entstehen erst beim Anzeigen und Exportieren und bringen ihre eigene Umrechnung mit ([KI-Export](06-ki-export-und-mcp.md), [Anleitungs-Export](09-anleitungs-export.md)).

### 8.4 Plausibilität

- Mauskoordinaten werden auf die Fläche des virtuellen Desktops der Epoche begrenzt.
- Ein Elementrechteck, das den auslösenden Klickpunkt nicht enthält, leer ist oder offensichtlich skaliert gemeldet wurde, wird **nicht** als `rect` gespeichert. Stattdessen stehen `rectIssue` (`outsideHit` \| `empty` \| `scaled` \| `offscreen`) und der gemeldete Wert in `rectReported`. So entstehen keine falschen Markierungen im Bild.
- Abgeleitete Koordinaten (Position im Element, Position im Fenster) stehen nicht im Journal, sondern in `steps.json`.

---

<a id="ereignistypen"></a>
## 9. Ereignistypen

Formatversion 1 kennt 44 Ereignistypen in 18 Familien. Die Familiennamen sind verbindlich.

Zu den Beispielzeilen: Zeilen mit `seq` zwischen 1650 und 1750 stammen aus der Beispielsitzung in [Anhang A](#beispiel-a) und passen dort zusammen. Alle übrigen Beispielzeilen sind unabhängige Veranschaulichungen.

| Familie | Typen | Inhalt | Abschnitt |
|---|---|---|---|
| `session.*` | `session.start`, `session.pause`, `session.resume`, `session.stop`, `session.checkpoint`, `session.recovered` | Lebenszyklus der Aufnahme | [9.2](#ev-session) |
| `system` | `system` (Feld `kind`) | Umgebung und Systemzustände | [9.3](#ev-system) |
| `display.topology` | `display.topology` | Monitor-Anordnung, Epochen | [9.4](#ev-display-topology) |
| `scope.change` | `scope.change` | Aufnahmebereich | [9.5](#ev-scope-change) |
| `keystate.sync` | `keystate.sync` | Abgleich des Tastenzustands | [9.6](#ev-keystate-sync) |
| `mouse.*` | `mouse.move`, `mouse.down`, `mouse.up`, `mouse.wheel`, `mouse.cursor` | Mauseingaben und Zeigerform | [9.7](#ev-mouse) |
| `key.*` | `key.down`, `key.up` | Tasteneingaben | [9.8](#ev-key) |
| `text.masked` | `text.masked` | Platzhalter für zurückgehaltene Tasten | [9.9](#ev-text-masked) |
| `window.*` | `window.info`, `window.open`, `window.close`, `window.foreground`, `window.title`, `window.rect`, `window.menu`, `window.dialog`, `window.switch`, `window.dragdrop` | Fenster | [9.10](#ev-window) |
| `focus` | `focus` | Tastaturfokus und Eingabekontext | [9.11](#ev-focus) |
| `element` | `element` | Ergebnis einer Element-Ermittlung | [9.12](#ev-element) |
| `ui.*` | `ui.value`, `ui.text`, `ui.toggle`, `ui.selection`, `ui.expand`, `ui.invoke`, `ui.scroll`, `ui.notification` | Zustandsänderungen von Elementen | [9.13](#ev-ui) |
| `ui.snapshot` | `ui.snapshot` | Text-Schnappschuss eines Fensters | [9.14](#ev-ui-snapshot) |
| `frame` | `frame` | Bild (Beobachtung des Bildschirms) | [9.15](#ev-frame) |
| `clipboard` | `clipboard` | Änderung der Zwischenablage | [9.16](#ev-clipboard) |
| `comment` | `comment` | Kommentar des Nutzers | [9.17](#ev-comment) |
| `marker` | `marker` | Marker, Schnappschuss, „letzten Schritt verwerfen“ | [9.18](#ev-marker) |
| `gap` | `gap` | Lücke | [9.19](#ev-gap) |
| `process.*` | `process.info`, `process.exit` | Programme, mit deren Fenstern gearbeitet wurde: Start mit Build-Kennung, Ende mit Exit-Code | [9.20](#ev-process) |
| `window.hang` | `window.hang` | Fenster reagiert nicht (Beginn, Ende) | [9.20](#ev-process) |
| `eventlog.entry` | `eventlog.entry` | Eintrag der Windows-Ereignisanzeige zu einem aufgenommenen Programm | [9.20](#ev-process) |
| `log.*` | `log.source`, `log.lines` | Mitgelesene Protokolldatei | [9.21](#ev-log) |

### 9.1 Gemeinsame Bausteine

**Rechteck (`Rect`)** – `{ "m": "M1", "x": 0, "y": 0, "w": 100, "h": 20 }`, siehe [Koordinatenmodell](#koordinatenmodell).

**Umschalter (`mods`)** – Liste aus `ctrl`, `alt`, `shift`, `win`, `altGr`, in dieser Reihenfolge. `altGr` steht allein (ohne `ctrl` und `alt`), wenn die rechte Alt-Taste auf einem Layout mit AltGr gedrückt ist.

**Maustaste (`button`)** – logische Rolle: `primary`, `secondary`, `middle`, `x1`, `x2`. Die physische Taste steht in `physButton` (`left`, `right`, `middle`, `x1`, `x2`). Bei vertauschten Maustasten ist `primary` die physisch rechte Taste. `held` ist eine Liste gleichzeitig gehaltener Tasten in derselben Benennung.

**Künstliche Eingabe (`inj`)** – fehlt bei echter Eingabe; `true`, wenn die Eingabe von einem Programm erzeugt wurde; `"lowerIl"`, wenn sie von einem Prozess niedrigerer Integritätsstufe stammt.

**Eingabekontext (`ctx`)** – wohin eine Tasteneingabe ging:

| Wert | Bedeutung |
|---|---|
| `textField` | Sicher erkanntes, unkritisches Textfeld. |
| `passwordField` | Erkanntes Passwortfeld (auch „einmal Passwort, immer Passwort“). |
| `nonText` | Fokus liegt nicht in einem Textfeld (Schaltfläche, Liste, Menü). |
| `opaque` | Undurchsichtige Oberfläche: Terminal, Remote-Desktop, VM, Spiel, Zeichenfläche. |
| `unknown` | Einstufung nicht rechtzeitig möglich. |

**Zurückgehaltener Inhalt (`withheld`)** – Grund, warum ein Inhalt fehlt: `password`, `privacyMode`, `strict`, `excludedApp`, `outOfScope`, `optOut`, `sensitiveFormat`, `tooLarge`. Wann welcher Grund greift, regelt [Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md).

**Elementverweis (`ElementRef`)** – Kurzform für Stellen, an denen das Element nur benannt wird: `{ "rid": "42.1312.4.87", "ct": "Edit", "name": "Dateiname", "aid": "1001" }`. Alle Felder optional außer `ct`.

**Elementbeschreibung (`ElementDescriptor`)** – vollständige Beschreibung, wie sie [`element`](#ev-element) liefert. Alle Felder außer `ct` sind optional; ein fehlendes Feld bedeutet „nicht gemeldet“.

| Feld | Typ | Bedeutung |
|---|---|---|
| `rid` | string | UIA-RuntimeId als Punktfolge. Nur innerhalb der Sitzung und der Lebensdauer des Elements gültig. |
| `ct` | string | Steuerelementtyp, englischer UIA-Name: `Button`, `Edit`, `Document`, `Text`, `CheckBox`, `RadioButton`, `ComboBox`, `List`, `ListItem`, `Menu`, `MenuBar`, `MenuItem`, `Tab`, `TabItem`, `Tree`, `TreeItem`, `DataGrid`, `DataItem`, `Table`, `Header`, `HeaderItem`, `Hyperlink`, `Image`, `Slider`, `Spinner`, `ProgressBar`, `ScrollBar`, `ToolBar`, `ToolTip`, `StatusBar`, `Group`, `Pane`, `Window`, `TitleBar`, `SplitButton`, `Thumb`, `Calendar`, `Separator`, `Custom`. Bei Rückfall ohne UIA der nächstliegende Typ oder `Unknown`. |
| `lct` | string | Typbezeichnung in der Sprache der Anwendung („Schaltfläche“). |
| `name` | string | Name (Beschriftung). |
| `aid` | string | AutomationId. |
| `cls` | string | Klassenname. |
| `fw` | string | Framework (`Win32`, `WPF`, `XAML`, `WinForm`, `Chrome`, `Gecko`, `Qt`, `DirectUI`, …). |
| `hwnd` | string | Natives Fensterhandle des Elements (hexadezimal), falls vorhanden. |
| `pid` | int | Prozess, der das Element bereitstellt. |
| `rect` | Rect | Begrenzungsrechteck. Fehlt bei unplausiblem Wert, dann stehen `rectIssue` und `rectReported`. |
| `enabled`, `offscreen`, `focusable`, `focused` | bool | Zustände. |
| `password` | bool | UIA-Eigenschaft `IsPassword`. |
| `value` | string | Wert (Textinhalt, Auswahl, Zahlenwert als Text), höchstens `valueMaxChars` Zeichen. |
| `valueLen` | int | Volle Länge des Werts, wenn gekürzt. |
| `valueWithheld` | enum | Grund aus `withheld`, wenn der Wert nicht gespeichert wurde. |
| `toggle` | enum | `on`, `off`, `mixed`. |
| `selected` | bool | Auswahlzustand. |
| `expanded` | enum | `expanded`, `collapsed`, `partial`, `leaf`. |
| `help`, `accel`, `access`, `itemStatus`, `itemType`, `desc` | string | Hilfetext, Tastenkürzel, Zugriffstaste, Status, Elementart, Beschreibung. |
| `patterns` | string[] | Unterstützte Muster: `Invoke`, `Value`, `RangeValue`, `Toggle`, `SelectionItem`, `Selection`, `ExpandCollapse`, `Scroll`, `ScrollItem`, `Text`, `Grid`, `GridItem`, `Table`, `TableItem`, `Window`, `Transform`, `LegacyIAccessible`. |
| `labeledBy` | ElementRef | Beschriftendes Element. |
| `posInSet`, `sizeOfSet`, `level` | int | Position in einer Gruppe, Gruppengröße, Ebene im Baum. |
| `dupIndex`, `dupCount` | int | Position (ab 1) unter den sichtbaren Elementen desselben Fensters mit gleichem `ct` und `name` in Lesereihenfolge, und deren Anzahl. `dupCount: 1` heißt eindeutig. Fehlen beide, wurde nicht gezählt. |
| `row` | Objekt | Zeilenkontext für Einträge in Listen, Tabellen, Bäumen: `container` (ElementRef), `rowIndex`, `colIndex`, `colHeader`, `rowHeader`, `cells` (höchstens 8 Texte zu je höchstens 80 Zeichen). |
| `url` | string | Adresse des Dokuments, in dem das Element liegt (Browser). |
| `doc` | string | Name oder Pfad des Dokuments (Editoren, Office). |
| `scroll` | Objekt | `hPct`, `vPct` des nächsten scrollbaren Vorfahren (0–100, `-1` = nicht scrollbar). |
| `role`, `childId` | string, int | Nur bei MSAA-Rückfall. |
| `ctlId` | int | Nur bei Win32-Rückfall: Steuerelement-Kennung. |
| `opaqueHint` | bool | Der Recorder hält das Element für die bloße Hülle einer undurchsichtigen Oberfläche. |

---

<a id="ev-session"></a>
### 9.2 `session.*`

**`session.start`** – erster Eintrag jedes Aufnahmeabschnitts.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `part` | int | ● | Abschnittsnummer ab 0. |
| `schemaVersion` | string | ● | Formatversion des Journals. |
| `sessionId` | string | ● | Wie im Manifest. |
| `utc` | Zeit | ● | Wanduhrzeit des Starts. |
| `tz` | Objekt | ● | `id`, `offsetMin`. |
| `qpc`, `qpcFrequency` | int | ● | Zählerstand und Zählerfrequenz beim Start. |
| `tickCount` | int | ● | Millisekundenzähler des Systems beim Start (Basis für `tSrc: "sys"`). |
| `recorder` | Objekt | ● | Wie `manifest.recorder`. |
| `settings` | Objekt | ● | Wie `manifest.settings`. |
| `goal` | Objekt | ○ | Ziel der Aufnahme, wie [`manifest.goal`](#manifest-goal). Nur im ersten Abschnitt (`part: 0`). |
| `profile` | Objekt | ○ | Aufnahmeprofil dieses Abschnitts, wie [`manifest.profile`](#manifest-profile). |
| `purpose` | enum | ○ | Zweck des Abschnitts: `initial` \| `append` \| `replacement`, siehe [7.4](#aufnahmeabschnitte). Fehlt = `initial` bei Abschnitt 0, sonst `append`. |
| `replaces` | Objekt | ◐ | Pflicht bei `purpose: "replacement"`: `section`, `fromSeq`, `toSeq` – der Bereich, für den neu aufgenommen wird. |
| `deviceSalt` | string | ○ | Salz der Sitzung für `deviceKey` ([9.4](#ev-display-topology)): 32 Hex-Ziffern (16 zufällige Bytes), beim ersten Abschnitt erzeugt und im Kopf **jedes** weiteren Abschnitts unverändert wiederholt. So kann ein späterer Abschnitt – auch nach einem Neustart – dieselben Monitore wiedererkennen. Das Salz ist kein Geheimnis: Es verhindert nur, dass sich Monitore über verschiedene Sitzungen hinweg wiedererkennen lassen. Pflicht, sobald ein Monitor einen `deviceKey` trägt. |

**`session.pause`** – `reason` ● (siehe Tabelle), `gapId` ● (die zugehörige Lücke).

| `reason` | Wann |
|---|---|
| `user` | Der Nutzer hat die Aufnahme angehalten. |
| `lock` | Nach **Sperren** der Arbeitsstation, nach **Benutzerwechsel** und nach **Trennen** der Sitzung (Remote-Desktop, Konsole): Die Aufnahme wartet auf die Bestätigung zum Fortsetzen. Der Wert gilt für alle drei Fälle; welcher vorlag, sagen das vorausgehende `system`-Ereignis und die Lücke `sessionLocked` bzw. `sessionDisconnected`. |
| `diskFull` | Kein Speicherplatz mehr ([16.3](#absturzsicherheit)). |
| `error` | Fehler des Recorders, etwa ein dauerhaft verlorener Eingabe-Hook. |
| `scopeLost` | Der Aufnahmebereich ist weggefallen (Monitor entfernt, Anwendung beendet). |

**`session.resume`** – `gapId` ●. Unmittelbar danach folgen `keystate.sync` und, falls nötig, `display.topology`.

**`session.stop`** – `reason` ● (`user` \| `discarded` \| `error` \| `diskFull` \| `systemShutdown` \| `sessionEnd` \| `recovered`), `utc` ●, `counts` ○ (`events`, `frames`), `performance` ○ (die Leistungszähler dieses Abschnitts; das Manifest spiegelt sie unter `performance`). `discarded` steht, wenn der Autor einen **weiteren** Abschnitt mit „Aufnahme verwerfen“ beendet hat: Der Abschnitt bleibt im Journal und gilt als verworfen ([13.16](#edits-sections)).

**Sperren, Benutzerwechsel, Trennen (`reason: "lock"`).** Wird die Arbeitsstation gesperrt, der Benutzer gewechselt oder die Sitzung getrennt, beginnt eine Lücke `sessionLocked` bzw. `sessionDisconnected`. Sobald entsperrt bzw. wieder verbunden ist, endet diese Lücke – und unmittelbar danach beginnt eine Lücke `paused` mit `session.pause` und `reason: "lock"`. Die Aufnahme läuft erst weiter, wenn die Person am Rechner das Fortsetzen bestätigt; von selbst nie, denn nach dem Entsperren kann jemand anderes dort sitzen.

```json
{"seq":5100,"t":300000000,"type":"system","kind":"sessionLock"}
{"seq":5101,"t":300000000,"type":"gap","gapId":"g4","phase":"begin","cause":"sessionLocked","tFrom":300000000,"channels":["all"],"inputLost":"possible"}
{"seq":5102,"t":420000000,"type":"system","kind":"sessionUnlock"}
{"seq":5103,"t":420000000,"type":"gap","gapId":"g4","phase":"end","cause":"sessionLocked","tFrom":300000000,"tTo":420000000,"channels":["all"],"inputLost":"possible"}
{"seq":5104,"t":420000000,"type":"gap","gapId":"g5","phase":"begin","cause":"paused","tFrom":420000000,"channels":["all"],"inputLost":"possible"}
{"seq":5105,"t":420000000,"type":"session.pause","reason":"lock","gapId":"g5"}
{"seq":5106,"t":431500000,"type":"session.resume","gapId":"g5"}
{"seq":5107,"t":431500000,"type":"gap","gapId":"g5","phase":"end","cause":"paused","tFrom":420000000,"tTo":431500000,"channels":["all"],"inputLost":"possible"}
```

(Zwei Minuten gesperrt, danach 11,5 s bis zur Bestätigung; Kontrollpunkte und `keystate.sync` sind hier ausgelassen.)

Bei Benutzerwechsel und beim Trennen einer Sitzung ist die Folge dieselbe – mit `consoleDisconnect`/`consoleConnect` bzw. `remoteDisconnect`/`remoteConnect` als `system`-Ereignissen und der Lücke `sessionDisconnected` anstelle von `sessionLocked`. `session.pause` trägt auch dort `reason: "lock"`.

**`session.checkpoint`** – Stützstelle für Integrität und Uhrenabgleich; mindestens alle 30 s sowie vor jeder Pause und vor dem Stopp.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `utc` | Zeit | ● | Wanduhrzeit im Moment des Schreibens. |
| `lines` | int | ● | Anzahl der abgedeckten Zeilen. |
| `chain` | string | ● | `"sha256:<hex>"`, siehe [Integrität](#absturzsicherheit). |

**`session.recovered`** – wird von der Wiederherstellung angehängt: `utc` ●, `lastValidSeq` ●, `discardedBytes` ●, `chainOk` ○ (bool), `tool` ●.

```json
{"seq":1,"t":0,"type":"session.start","part":0,"schemaVersion":"1.0","sessionId":"019328a4-6b1e-7c3a-9d52-0f4e8a1b77c0","utc":"2026-11-11T13:30:05.123456Z","tz":{"id":"W. Europe Standard Time","offsetMin":60},"qpc":8123456789012,"qpcFrequency":10000000,"tickCount":812345678,"recorder":{"name":"StepRecorder","version":"1.0.0","arch":"x64","elevated":false,"captureHost":"process"},"settings":{"scope":{"mode":"screen","monitors":"active"},"privacy":{"level":"balanced","clipboardContent":false},"language":"de"},"profile":{"name":"Anleitung","id":"guide","builtIn":true,"effective":{"scopeMode":"screen","privacyLevel":"balanced","clipboardContent":false,"afterStop":{"action":"viewer"},"showGoalFields":false}}}
```

Der Kopf einer Sitzung mit Ziel-Angaben (hier und im folgenden Beispiel gekürzt um Uhrenbasis, Recorder und Einstellungen):

```json
{"seq":1,"t":0,"type":"session.start","part":0,"schemaVersion":"1.0","sessionId":"0193301e-4b7a-7c05-a2d8-91e6f0c3b5d4","utc":"2026-11-12T10:02:00.000000Z","goal":{"description":"Rechnung als PDF exportieren und per E-Mail versenden.","expected":"Die E-Mail mit der PDF-Datei liegt im Postausgang.","reference":"TICKET-4711"}}
```

Der Kopf einer Neuaufnahme, die die Ereignisse 2140 bis 2305 des ersten Abschnitts ersetzen soll:

```json
{"seq":4410,"t":212000000,"type":"session.start","part":1,"schemaVersion":"1.0","sessionId":"01932c57-9d02-7e48-b1a6-3c5f8e2d4a90","utc":"2026-11-12T08:40:12.000000Z","purpose":"replacement","replaces":{"section":0,"fromSeq":2140,"toSeq":2305}}
```

---

<a id="ev-system"></a>
### 9.3 `system`

Ein Typ, unterschieden durch `kind` ●. Weitere Felder je `kind`:

| `kind` | Felder | Bedeutung |
|---|---|---|
| `environment` | `os`, `locale`, `keyboardLayouts`, `inputSettings`, `accessibility`, `sessionInfo`, `theme`, `identity` ○ | Vollständige Umgebung, Aufbau wie `manifest.environment`. Teil des Sitzungskopfs. |
| `inputSettings` | `inputSettings` (nur geänderte Felder) | Eingabe-Einstellung während der Aufnahme geändert. |
| `keyboardLayout` | `hkl`, `klid`, `name`, `win` ○ | Aktives Tastaturlayout gewechselt. |
| `sessionLock`, `sessionUnlock` | – | Arbeitsstation gesperrt/entsperrt. |
| `remoteConnect`, `remoteDisconnect`, `consoleConnect`, `consoleDisconnect` | – | Wechsel der Sitzungsverbindung. |
| `powerSuspend`, `powerResume` | – | Ruhezustand. |
| `desktopSwitch` | `to` (`secure` \| `default`), `desktop` ○ (beim Wechsel auf `secure`: Name des Eingabe-Desktops laut System, z. B. `Winlogon` oder `Screen-saver`; `inaccessible`, wenn er sich nicht öffnen lässt; `unknown`, wenn der Name nicht lesbar ist) | Eingabe-Desktop gewechselt (z. B. sicherer Desktop bei UAC). Der Recorder fragt den Desktop alle 250 ms ab. Ein anderer Desktop als `Default` beginnt sofort die Lücke `secureDesktop`; die Rückkehr gilt erst nach zwei aufeinanderfolgenden Lesungen von `Default` (Flackern beim Umschalten). Solange der Rechner gesperrt, getrennt oder im Ruhezustand ist, entsteht keine eigene Lücke `secureDesktop`. |
| `captureBackend` | `m`, `from`, `to`, `reason` | Aufnahmeweg eines Monitors gewechselt. |
| `hook` | `channel` (`keyboard` \| `mouse`), `action` (`installed` \| `lost` \| `reinstalled`), `reason` ○ | Zustand eines Eingabe-Hooks. |
| `selfTest` | `test` (`invisibility`), `passed` (bool), `mode` (`excluded` \| `fallbackHidden`), `backend` | Ergebnis des Selbsttests beim Aufnahmestart. |
| `privacyMode` | `active` (bool), `trigger` (`hotkey` \| `toolbar` \| `terminalPrompt` \| `rule`), `rule` ○ (bei `terminalPrompt`: `T1`, `T2`, `T3`), `end` ◐ (Endgrund, Pflicht bei `active: false`), `gapId` | Privatmodus ein/aus. Die Privattaste ist ein Umschalter ohne automatisches Ende; ihr Endgrund ist `toggle` (Taste oder Schaltfläche erneut betätigt), `paused` oder `stopped` (Pause bzw. Ende der Aufnahme). Die Erkennung von Passwortabfragen im Terminal schaltet für jede erkannte Abfragezeile selbst ein und wieder aus; ihr Endgrund ist `enter` (Eingabetaste), `cancelCtrlC`, `cancelCtrlD`, `cancelEscape` (Abfrage abgebrochen), `focusChange` (Fokus verließ das Terminal), `timeout` (60 s ohne Taste) oder `stopped`; eine Pause beendet sie nicht ([10, 6.2](10-datenschutz-und-sicherheit.md#62-regeln)). Bei `terminalPrompt` steht `rule` an beiden Ereignissen; die Abfragezeile steht nur am `text.masked` (`promptLine`). Jede Abfragezeile hat ihre eigene Lücke, unabhängig von einer gleichzeitig laufenden Privattaste. |
| `clockChange` | `utcBefore`, `utcAfter` | Systemuhr verstellt. |
| `disk` | `state` (`low` \| `full` \| `ok`), `freeBytes` | Freier Speicher am Aufnahmeort. |
| `recorder` | `level` (`info` \| `warning` \| `error`), `code`, `message` | Meldung des Recorders. `message` enthält nie aufgezeichnete Inhalte. |

---

<a id="ev-display-topology"></a>
### 9.4 `display.topology`

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `epoch` | int | ● | Nummer der Epoche ab 0, fortlaufend. |
| `reason` | enum | ● | `initial`, `displayChange`, `dpiChange`, `resume`, `partStart`. |
| `virtual` | Objekt | ● | Umfassendes Rechteck aller Monitore: `x`, `y`, `w`, `h` in Koordinaten des virtuellen Desktops. |
| `monitors` | Objekt[] | ● | Siehe unten. |
| `removed` | string[] | ○ | Kennungen der Monitore, die gegenüber der vorigen Epoche fehlen. |

Je Monitor:

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `id` | string | ● | Stabile Kennung `M<n>`. |
| `name` | string | ● | Windows-Gerätename (`\\.\DISPLAY1`); ändert sich beim Umstecken. |
| `label` | string | ○ | Anzeigename des Geräts. |
| `primary` | bool | ● | Hauptmonitor. |
| `vx`, `vy` | int | ● | Lage der linken oberen Ecke im virtuellen Desktop. |
| `w`, `h` | int | ● | Größe in physischen Pixeln, nach Drehung. |
| `work` | Objekt | ○ | Arbeitsbereich ohne Taskleiste, monitorrelativ: `x`, `y`, `w`, `h`. |
| `dpi` | int | ● | Effektive DPI; `scale` = `dpi / 96`. |
| `scale` | Zahl | ● | Skalierungsfaktor (1.0, 1.25, 1.5, …). |
| `rotation` | int | ● | 0, 90, 180, 270. |
| `refreshHz` | Zahl | ○ | Bildwiederholrate. |
| `hdr` | bool | ○ | HDR aktiv. |
| `adapter` | string | ○ | Grafikadapter, an dem der Monitor hängt. |
| `deviceKey` | string | ○ | Gesalzener Hash der Gerätekennung; dient nur dazu, denselben Monitor in späteren Abschnitten wiederzuerkennen. Seriennummern werden nicht gespeichert. Gebildet als die ersten 16 Hex-Ziffern von SHA-256 über das Salz der Sitzung, gefolgt von der Gerätekennung (UTF-8). Das Salz steht als `deviceSalt` im Sitzungskopf jedes Abschnitts ([9.2](#ev-session)) und im Manifest. |
| `backend` | enum | ● | Tatsächlicher Aufnahmeweg: `dda`, `wgc`, `gdi`, `none` (Monitor wird nicht aufgenommen). |

---

<a id="ev-scope-change"></a>
### 9.5 `scope.change`

Beschreibt den ab `t` gültigen Aufnahmebereich vollständig (kein Differenzformat).

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `mode` | enum | ● | `screen`, `app`, `region`. |
| `monitors` | string oder string[] | ◐ | Bei `screen`: `"active"` (der Monitor, auf dem gearbeitet wird), `"all"` oder eine Liste von Kennungen. |
| `apps` | Objekt[] | ◐ | Bei `app`: je Eintrag `process`, `pid` ○, `match` (`process` \| `window`), `win` ○. |
| `includeRelated` | bool | ○ | Bei `app`: zugehörige Hilfsfenster anderer Prozesse einschließen. Standard `true`. |
| `region` | Rect | ◐ | Bei `region`: das Rechteck. |
| `reason` | enum | ● | `initial`, `user`, `followWindow`, `resume`. |

Eingaben außerhalb des Aufnahmebereichs werden **nie** aufgezeichnet; sie erscheinen ausschließlich als Lücke `outOfScope` ([9.19](#ev-gap)). Eine Einstellung, sie mitzuprotokollieren, gibt es nicht.

```json
{"seq":5,"t":0,"type":"scope.change","mode":"app","apps":[{"process":"EXCEL.EXE","match":"process"}],"includeRelated":true,"reason":"initial"}
```

---

<a id="ev-keystate-sync"></a>
### 9.6 `keystate.sync`

Hält fest, welche Tasten laut System gerade gedrückt sind, und korrigiert den mitgeführten Zustand. Wird im Sitzungskopf, nach jeder Lücke und nach jeder Wiederaufnahme geschrieben.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `reason` | enum | ● | `initial`, `afterGap`, `resume`, `mismatch`, `periodic`, `secureDesktop` (Abgleich nach der Rückkehr vom sicheren Desktop; `gapId` nennt dessen Lücke). |
| `down` | Objekt[] | ● | Gedrückte Tasten: `vk`, `key`. Druckbare Tasten werden hier nie genannt, nur Umschalter und Feststeller. |
| `mouseDown` | string[] | ● | Gedrückte Maustasten. |
| `locks` | string[] | ● | Aktive Feststeller: `caps`, `num`, `scroll`. |
| `corrected` | Objekt[] | ○ | Berichtigungen: `vk`, `key`, `was` (`down` \| `up`), `now`. |
| `gapId` | string | ○ | Lücke, nach der abgeglichen wurde. |

```json
{"seq":4020,"t":139004000,"type":"keystate.sync","reason":"secureDesktop","gapId":"g1","down":[],"mouseDown":[],"locks":["num"],"corrected":[{"vk":162,"key":"LeftCtrl","was":"down","now":"up"},{"vk":160,"key":"LeftShift","was":"down","now":"up"}]}
```

Das Beispiel stammt aus [Anhang B.2](#beispiele): Strg und Umschalt wurden vor einer Lücke gedrückt und in der Lücke losgelassen.

---

<a id="ev-mouse"></a>
### 9.7 `mouse.*`

**`mouse.move`** – ein Bündel aufeinanderfolgender Bewegungsmesswerte. Aufbau und Regeln stehen in [Mausbewegungs-Bündel](#mausbewegungen).

**`mouse.down`, `mouse.up`**

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `ep`, `m`, `x`, `y` | – | ● | Position, siehe [Koordinatenmodell](#koordinatenmodell). |
| `button` | enum | ● | Logische Taste. |
| `physButton` | enum | ● | Physische Taste. |
| `mods` | string[] | ○ | Gehaltene Umschalter. |
| `held` | string[] | ○ | Weitere gehaltene Maustasten. |
| `inj` | – | ○ | Künstliche Eingabe. |
| `device` | enum | ○ | Eingabegerät: `mouse` (Standard), `touch`, `pen`. |
| `win` | string | ◐ | Fenster der obersten Ebene unter dem Zeiger **zum Hook-Zeitpunkt**. Pflicht bei `mouse.down`, sofern ein Fenster vorhanden ist. |
| `hitHwnd` | string | ○ | Handle des genau getroffenen (Kind-)Fensters, hexadezimal. |
| `fg` | string | ○ | Fenster im Vordergrund zum Hook-Zeitpunkt. |

**`mouse.wheel`** – wie oben ohne `button`/`physButton`, zusätzlich:

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `axis` | enum | ● | `v` (senkrecht) \| `h` (waagerecht). |
| `delta` | int | ● | Rohwert; ±120 entspricht einer Raste. Positiv = vom Nutzer weg bzw. nach rechts. Hochauflösende Räder und Touchpads liefern kleinere Werte. |

**`mouse.cursor`** – die Form des Mauszeigers hat sich geändert (`tSrc: "obs"`).

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `cursor` | string | ● | Kennung `cur<n>`. |
| `std` | enum | ○ | Standardform, falls erkannt: `arrow`, `ibeam`, `hand`, `wait`, `appStarting`, `cross`, `sizeNS`, `sizeWE`, `sizeNWSE`, `sizeNESW`, `sizeAll`, `no`, `help`, `upArrow`, `pin`, `person`. |
| `file` | string | ◐ | `cursors/cur<n>.png`; Pflicht beim ersten Auftreten der Kennung. |
| `hotspot` | int[2] | ◐ | Aktiver Punkt im Zeigerbild; Pflicht beim ersten Auftreten. |
| `visible` | bool | ○ | `false`, wenn der Zeiger ausgeblendet ist. |

```json
{"seq":1734,"t":83412066,"inSeq":1212,"type":"mouse.down","ep":0,"m":"M1","x":1056,"y":701,"button":"primary","physButton":"left","win":"w13","hitHwnd":"0x000A12F4","fg":"w13"}
{"seq":1737,"t":83498120,"inSeq":1213,"type":"mouse.up","ep":0,"m":"M1","x":1056,"y":701,"button":"primary","physButton":"left","win":"w13","fg":"w13"}
{"seq":2210,"t":95100400,"inSeq":52310,"type":"mouse.wheel","ep":0,"m":"M1","x":900,"y":620,"axis":"v","delta":-120,"win":"w12","fg":"w12"}
```

---

<a id="ev-key"></a>
### 9.8 `key.*`

**`key.down`, `key.up`** – jeder Tastendruck und jedes Loslassen, sofern nicht zurückgehalten (dann [`text.masked`](#ev-text-masked)).

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `vk` | int | ● | Virtueller Tastencode. |
| `sc` | int | ● | Scancode. |
| `ext` | bool | ○ | Erweiterte Taste (z. B. rechte Strg-Taste, Pfeiltasten, Enter des Ziffernblocks). |
| `key` | string | ● | Kanonischer, layoutunabhängiger Tastenname, siehe Tabelle unten. Zwei Sonderwerte stehen für Tasten, deren Inhalt entfernt wurde: `Redacted` (geschwärzt, [13.6](#edits-redactions)) und `Substituted` (verallgemeinert, [13.14](#edits-substitutions)); `vk` und `sc` sind dann 0, `ch` fehlt. |
| `placeholder` | string | ○ | Nur bei `key: "Substituted"`, an der ersten Taste einer verallgemeinerten Fundstelle: Name des Platzhalters. |
| `hkl` | string | ◐ | Aktives Tastaturlayout im Zielfenster (8 Hex-Ziffern). Pflicht bei `key.down`. |
| `ch` | string | ○ | **Übersetzung zum Aufnahmezeitpunkt:** die druckbaren Zeichen, die dieser Tastendruck nach Layout, Umschaltern, Feststellern und Tottasten-Zustand erzeugt. Fehlt bei Tasten ohne Zeichen (auch Enter, Tab, Backspace). Nur bei `key.down`, Ausnahme `altCode`. |
| `dead` | bool | ○ | Tottaste: `ch` nennt das Akzentzeichen, es entsteht aber noch kein Text. |
| `deadFrom` | int | ○ | `seq` der Tottaste, mit der `ch` zusammengesetzt wurde (`^` + `e` → `ê`). |
| `altCode` | string | ○ | Nur bei `key.up` der Alt-Taste: die über den Ziffernblock eingegebene Ziffernfolge; `ch` nennt das entstandene Zeichen. |
| `rep` | bool | ○ | Wiederholung einer bereits gedrückten Taste (Auto-Repeat). |
| `mods` | string[] | ○ | Umschalter, die **vor** diesem Ereignis gedrückt waren. |
| `locks` | string[] | ◐ | Aktive Feststeller (`caps`, `num`, `scroll`). Pflicht bei `key.down`, darf leer sein. |
| `altDown` | bool | ○ | Systemkennzeichen „Alt gedrückt“. |
| `altGrPart` | bool | ○ | Vom System erzeugter Strg-Anteil der AltGr-Taste – kein eigener Tastendruck des Nutzers. |
| `inj` | – | ○ | Künstliche Eingabe. |
| `ime` | bool | ○ | Eine Eingabemethode (IME) war aktiv; `ch` ist dann unzuverlässig. |
| `fg` | string | ◐ | Fenster im Vordergrund zum Hook-Zeitpunkt; Pflicht, sofern vorhanden. |
| `focus` | int | ○ | `seq` des [`focus`](#ev-focus)-Ereignisses, das für diese Taste galt. |
| `ctx` | enum | ◐ | Eingabekontext, nach dem über die Freigabe entschieden wurde. Pflicht bei `key.down`. |
| `recovered` | bool | ○ | Aus dem zweiten Eingabekanal ergänzt; dann `src: "raw"`. |

Kanonische Tastennamen (`key`):

| Gruppe | Namen |
|---|---|
| Buchstaben, Ziffernreihe | `A` … `Z`, `0` … `9` |
| Funktionstasten | `F1` … `F24` |
| Steuerung | `Enter`, `Tab`, `Space`, `Backspace`, `Escape`, `Insert`, `Delete`, `Home`, `End`, `PageUp`, `PageDown`, `Left`, `Right`, `Up`, `Down`, `PrintScreen`, `Pause`, `Apps` |
| Umschalter | `LeftShift`, `RightShift`, `LeftCtrl`, `RightCtrl`, `LeftAlt`, `RightAlt`, `LeftWin`, `RightWin` |
| Feststeller | `CapsLock`, `NumLock`, `ScrollLock` |
| Ziffernblock | `NumPad0` … `NumPad9`, `NumPadAdd`, `NumPadSubtract`, `NumPadMultiply`, `NumPadDivide`, `NumPadDecimal`, `NumPadEnter` |
| Layoutabhängige Tasten | `Oem1` … `Oem8`, `Oem102`, `OemPlus`, `OemComma`, `OemMinus`, `OemPeriod` – was sie bedeuten, sagt `ch` |
| Medien und Browser | `VolumeUp`, `VolumeDown`, `VolumeMute`, `MediaPlayPause`, `MediaNext`, `MediaPrev`, `MediaStop`, `BrowserBack`, `BrowserForward`, `BrowserRefresh`, `BrowserHome`, `BrowserSearch` |
| Alles andere | `Vk` + zweistelliger Hexcode, z. B. `VkE7` |

Die Felder `vk`, `sc`, `ext`, `hkl`, `mods`, `locks` reichen aus, um die Übersetzung später mit besseren Regeln zu wiederholen; `ch` hält fest, was zum Aufnahmezeitpunkt galt.

```json
{"seq":1657,"t":80050000,"inSeq":645,"type":"key.down","vk":83,"sc":31,"key":"S","hkl":"04070407","mods":["ctrl"],"locks":["num"],"fg":"w12","focus":318,"ctx":"textField"}
{"seq":1700,"t":81290300,"inSeq":648,"type":"key.down","vk":78,"sc":49,"key":"N","hkl":"04070407","ch":"n","locks":["num"],"fg":"w13","focus":1663,"ctx":"textField"}
{"seq":1702,"t":81352000,"inSeq":649,"type":"key.up","vk":78,"sc":49,"key":"N","fg":"w13"}
```

Diese drei Zeilen stammen aus [Anhang A](#beispiel-a). Ein `@` über AltGr in einem Terminal, aus der anderen Sitzung von [Anhang B.1](#beispiele):

```json
{"seq":912,"t":44221900,"inSeq":9120,"type":"key.down","vk":81,"sc":16,"key":"Q","hkl":"04070407","ch":"@","mods":["altGr"],"locks":[],"fg":"w7","focus":812,"ctx":"opaque"}
```

---

<a id="ev-text-masked"></a>
### 9.9 `text.masked`

Platzhalter für eine zusammenhängende Folge zurückgehaltener Tasten. Für diese Tasten existieren **keine** `key.down`- und `key.up`-Ereignisse – weder Tastencodes noch Einzelzeiten erreichen die Festplatte.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `t` | µs | ● | Beginn der Folge (erste zurückgehaltene Taste). |
| `tEnd` | µs | ● | Ende der Folge: Zeit des letzten zurückgehaltenen Hook-Ereignisses (Drücken oder Loslassen). |
| `count` | int | ◐ | Anzahl zurückgehaltener **Tastenanschläge**, siehe „Zählweise“ unten. Pflicht – außer die Einstellung „Anzahl nicht speichern“ ist aktiv. |
| `reason` | enum | ● | Grund der Zurückhaltung, siehe Tabelle unten. |
| `rule` | string | ◐ | Nur bei `terminalPrompt` (dort Pflicht): die zutreffende Erkennungsregel `T1`, `T2` oder `T3` ([Datenschutz](10-datenschutz-und-sicherheit.md)). |
| `promptLine` | string | ○ | Nur bei `terminalPrompt`: die erkannte Abfragezeile als aufgezeichneter Bildschirmtext, höchstens 120 Zeichen (z. B. `[sudo] password for admin:`). |
| `inSeq` | int | ● | Eingabenummer des ersten zurückgehaltenen Hook-Ereignisses. |
| `inSeqEnd` | int | ◐ | Eingabenummer des letzten zurückgehaltenen Hook-Ereignisses. Pflicht, wenn `count` vorhanden ist. |
| `events` | int | ◐ | Anzahl der zurückgehaltenen Hook-Ereignisse (Drücken und Loslassen) zwischen `inSeq` und `inSeqEnd`. Mausereignisse dazwischen tragen eigene Nummern und zählen nicht mit. Pflicht, wenn `count` vorhanden ist. |
| `fg` | string | ◐ | Fenster im Vordergrund; Pflicht, sofern vorhanden. |
| `focus` | int | ○ | `seq` des geltenden `focus`-Ereignisses. |
| `inj` | – | ○ | Mindestens eine der Tasten war künstlich erzeugt. |

| `reason` | Bedeutung |
|---|---|
| `passwordField` | Erkanntes Passwortfeld. |
| `stickyPassword` | Das Feld war früher in der Sitzung ein Passwortfeld („einmal Passwort, immer Passwort“). |
| `sensitiveField` | Textfeld, dessen Name oder Beschriftung auf Geheimes hindeutet (PIN, TAN, Sicherheitscode …). |
| `terminalPrompt` | Erkannte Passwortabfrage im Terminal; mit `rule` und `promptLine`. |
| `autoType` | Erkannte Tastenfolge eines Passwortmanagers. |
| `loginWindow` | Undurchsichtige Oberfläche mit Anmelde-Merkmalen. |
| `privacyMode` | Privatmodus war aktiv (Privattaste). |
| `strictUnknown` | Schutzstufe „Streng“: undurchsichtige Oberfläche, deshalb kein Klartext. |
| `unresolved` | Das Eingabeziel ließ sich nicht rechtzeitig oder nicht widerspruchsfrei einstufen. Tritt in beiden Schutzstufen auf. |

Wann welcher Grund greift, legt [Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md) fest.

Tasten ohne Zeichen und ohne Korrekturwirkung (Enter, Tab, Escape, Tastenkombinationen mit Strg/Alt/Win) werden auch in einem Passwortfeld als normale `key.*`-Ereignisse geschrieben, mit `ctx: "passwordField"`.

Ein `text.masked` gilt zugleich als Lückenmarker für den Tastaturkanal; der Vollständigkeitsbericht führt es unter der Ursache `maskedInput`. Ein eigenes `gap`-Ereignis entsteht dafür nicht.

**Zählweise.** Ein Tastenanschlag ist jedes Drücken einer zurückgehaltenen Taste, das **kein reiner Modifikator** ist:

- Es zählen Zeichentasten, Tottasten sowie Bearbeitungs- und Bewegungstasten im Feld (Rücktaste, Entf, Pfeiltasten, Pos1, Ende) – jede automatische Wiederholung einzeln.
- Es zählen **nicht**: Umschalt, Strg, Alt, AltGr und Win für sich sowie die Feststeller (Feststell-, Num- und Rollen-Taste). Ein großes „A“ ist ein Anschlag, nicht zwei.
- `count` ist damit eine Obergrenze für die Länge des Inhalts, nicht die Länge selbst (Korrekturen zählen mit). `events` zählt dagegen **alle** zurückgehaltenen Hook-Ereignisse, auch die der Modifikatoren.

Dieselbe Zählweise gilt für `{masked:N}` in allen Ausgaben und für N in `{unreviewed:N}` bei getippten Läufen ([13.10](#edits-review), [Schritterkennung](05-schritterkennung.md#textvorlagen)).

**Anzahl nicht speichern.** Ist schon die Länge einer Eingabe schützenswert, kann das Speichern der Anzahl abgeschaltet werden (`privacy.storeMaskedCount: false`). Dann fehlen `count`, `inSeqEnd` und `events`; in `steps.json` steht dafür `{ "masked": null }` ([12.7](#steps-json)), in Ausgaben `{masked}` ohne Zahl.

Damit sich die Länge auch nicht aus der Nummernfolge ablesen lässt, rückt die Eingabeverarbeitung – sie vergibt die Eingabenummern ([6](#eingabenummer)) – die Nummer um einen **zufälligen Zuschlag** vor. Jede zurückgehaltene Taste hat eine Nummer verbraucht; der Zuschlag vergrößert die sichtbare Lücke um einen unbekannten Betrag:

| Punkt | Festlegung |
|---|---|
| Wo | An **jeder Grenze** der zurückgehaltenen Folge: überall, wo auf zurückgehaltene Tasten wieder ein Eingabeereignis folgt, das eine Nummer trägt und geschrieben wird. Das ist das erste Ereignis nach dem Ende der Folge – und ebenso jedes Ereignis **mitten in ihr**: ein Mausereignis, eine Taste ohne Inhalt (Enter, Tab, Esc), eine Tastenkombination. |
| Warum nicht nur am Ende | Ein Mausereignis mitten in der Folge trägt seine eigene Nummer. Ohne Zuschlag davor ließe sich aus ihr abzählen, wie viele Tasten bis dahin gefallen waren. |
| Wie viel | Je Grenze unabhängig gezogen: eine nicht vorhersagbare ganze Zahl aus 0 bis 4095, gleichverteilt, aus der Zufallsquelle des Betriebssystems. Der Wert wird nirgends gespeichert. |
| Was bleibt | `inSeq` des `text.masked` ist weiterhin die Nummer der ersten zurückgehaltenen Taste. Alle späteren Nummern liegen um die Summe der bisherigen Zuschläge höher; an der Reihenfolge ändert das nichts. |
| Prüfung | F18 nimmt die betroffenen Nummern aus der Zählprüfung aus ([16.5](#absturzsicherheit)). |

In beiden Betriebsarten gilt außerdem: Eine zurückgehaltene Taste beendet **kein** Mausbewegungs-Bündel ([10](#mausbewegungen)). Sonst verrieten die Bündelgrenzen bei gleichzeitiger Mausbewegung den Zeitpunkt jeder einzelnen Taste – und bei abgeschalteter Anzahl auch, wie viele es mindestens waren.

```json
{"seq":2301,"t":101200400,"tEnd":103950100,"type":"text.masked","count":12,"inSeq":31870,"inSeqEnd":31893,"events":24,"reason":"passwordField","fg":"w21","focus":2299}
```

---

<a id="ev-window"></a>
### 9.10 `window.*`

Fenster werden über eine sitzungsweite Kennung `win` (`w<n>`) angesprochen. Sie wird beim ersten Auftreten eines Fensterhandles vergeben; wird ein Handle nach dem Schließen neu verwendet, entsteht eine neue Kennung.

**`window.info`** – beschreibt ein Fenster. Darf beliebig oft wiederholt werden; es gilt der jeweils jüngste Stand.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `win` | string | ● | Fensterkennung. |
| `hwnd` | string | ● | Fensterhandle, hexadezimal. |
| `partial` | bool | ○ | Bisher ist nur das Handle bekannt; eine vollständige Beschreibung folgt. |
| `pid` | int | ○ | Prozess-ID. |
| `process` | string | ○ | Name der Programmdatei (`notepad.exe`). |
| `exe` | string | ○ | Pfad der Programmdatei. Pfade unterhalb des Benutzerprofils beginnen mit `%USERPROFILE%`, sofern `privacy.recordIdentity` nicht gesetzt ist. |
| `title` | string | ○ | Fenstertitel. |
| `titleWithheld` | enum | ○ | Grund aus `withheld`, wenn der Titel nicht gespeichert wurde. |
| `class` | string | ○ | Fensterklasse. |
| `owner` | string | ○ | Kennung des Besitzerfensters. |
| `kind` | enum | ● | `window`, `dialog`, `menu`, `popup`, `dropdown`, `tooltip`, `toast`, `systemUi` (Taskleiste, Startmenü, Umschalter), `unknown`. |
| `rect`, `ep` | Rect, int | ○ | Lage und Größe. |
| `state` | enum | ○ | `normal`, `minimized`, `maximized`, `fullscreen`, `hidden`. |
| `topmost`, `toolWindow`, `cloaked` | bool | ○ | Fenstereigenschaften. |
| `elevated` | bool | ○ | Prozess läuft mit erhöhten Rechten. `null`, wenn nicht feststellbar. |
| `integrity` | enum | ○ | `low`, `appContainer`, `medium`, `high`, `system`. |
| `dpiAwareness` | enum | ○ | `unaware`, `system`, `perMonitor`, `perMonitorV2`. |
| `framework` | string | ○ | Wie `fw` der Elementbeschreibung. |
| `appId` | string | ○ | Anwendungskennung gepackter Apps. |
| `hosted` | Objekt | ○ | Tatsächliche Anwendung, wenn das Fenster nur ein Rahmen ist: `pid`, `process`. |
| `surface` | enum | ○ | `structured` (Standard: Elemente sind ermittelbar) \| `partial` (nur teilweise ermittelbar) \| `opaque` (nur als Bild greifbar). |
| `surfaceKind` | enum | ○ | Bei `partial` und `opaque`: `remoteDesktop`, `virtualMachine`, `terminal`, `game`, `canvas`, `video`, `unknown`. |
| `inScope` | bool | ○ | Gehört zum Aufnahmebereich. Standard `true`. |
| `excluded` | bool | ○ | Steht auf der Ausschlussliste. |
| `excludedRule` | string | ○ | Anzeigename der zutreffenden Ausschlussregel. Für ausgeschlossene Fenster werden Titel, Programmname und Pfad **nicht** gespeichert – nur dieser Name. |

Weitere Typen der Familie:

| Typ | Felder | Bedeutung |
|---|---|---|
| `window.open` | `win` ●, `kind` ●, `first` ○ | Fenster wurde sichtbar. `first: true` beim ersten Mal. |
| `window.close` | `win` ●, `destroyed` ○ | Fenster verschwand; `destroyed: true`, wenn es zerstört wurde. |
| `window.foreground` | `win` ●, `prev` ○ | Fenster kam in den Vordergrund. |
| `window.title` | `win` ●, `title` ◐, `titleWithheld` ○ | Titel geändert. |
| `window.rect` | `win` ●, `rect` ●, `state` ●, `ep` ● | Lage, Größe oder Zustand geändert (Ende von Verschieben/Größenänderung, Minimieren, Maximieren, Wiederherstellen). |
| `window.menu` | `win` ○, `phase` ● (`start` \| `end` \| `popupStart` \| `popupEnd`) | Menümodus begonnen/beendet, Menü aufgeklappt/zugeklappt. |
| `window.dialog` | `win` ●, `phase` ● (`start` \| `end`) | Dialog begonnen/beendet. |
| `window.switch` | `phase` ● (`start` \| `end`) | Fensterumschalter (Alt+Tab) sichtbar/weg. |
| `window.dragdrop` | `win` ○, `phase` ● (`start` \| `end`) | Ziehen-und-Ablegen begonnen/beendet. |

```json
{"seq":1659,"t":80120000,"tObs":80151200,"tSrc":"sys","tErr":16000,"type":"window.info","win":"w13","hwnd":"0x000A12E0","pid":9312,"process":"notepad.exe","title":"Speichern unter","class":"#32770","owner":"w12","kind":"dialog","ep":0,"rect":{"m":"M1","x":400,"y":180,"w":820,"h":560},"state":"normal","integrity":"medium","framework":"Win32"}
{"seq":1660,"t":80120000,"tObs":80151300,"tSrc":"sys","tErr":16000,"type":"window.open","win":"w13","kind":"dialog","first":true}
{"seq":1661,"t":80136000,"tObs":80151400,"tSrc":"sys","tErr":16000,"type":"window.foreground","win":"w13","prev":"w12"}
```

---

<a id="ev-focus"></a>
### 9.11 `focus`

Der Tastaturfokus hat gewechselt. Dieses Ereignis trägt die Einstufung des Eingabekontexts, auf der die Freigabe oder Zurückhaltung von Tasten beruht.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `win` | string | ● | Fenster mit Fokus. |
| `ctx` | enum | ● | Eingabekontext. |
| `ctxSrc` | enum | ● | Woher die Einstufung stammt: `uia`, `win32`, `heuristic`, `sticky` (früher in der Sitzung als Passwortfeld erkannt), `rule` (Ausschluss- oder Nutzerregel), `timeout`. |
| `ms` | int | ○ | Dauer der Einstufung in Millisekunden. |
| `el` | ElementRef | ○ | Fokussiertes Element in Kurzform. |
| `ctlHwnd` | string | ○ | Handle des fokussierten Kindfensters. |
| `ref` | int | ○ | `seq` eines früheren `focus`-Ereignisses, das hiermit neu eingestuft wird. |

`t` ist der Zeitpunkt des Fokuswechsels, `tObs` der Abschluss der Einstufung. Die vollständige Beschreibung des fokussierten Elements folgt als [`element`](#ev-element) mit `trigger: "focus"` und `ref` auf dieses Ereignis.

```json
{"seq":1663,"t":80140000,"tObs":80182700,"tSrc":"sys","tErr":16000,"type":"focus","win":"w13","ctx":"textField","ctxSrc":"uia","ms":38,"el":{"rid":"42.660192.4.1001","ct":"Edit","name":"Dateiname:","aid":"1001"}}
```

---

<a id="ev-element"></a>
### 9.12 `element`

Ergebnis einer Element-Ermittlung. Es wird auch dann geschrieben, wenn die Ermittlung scheitert – das Scheitern ist selbst eine Information.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `trigger` | enum | ● | Anlass: `dwell` (Maus verweilt), `buttonDown`, `buttonUp`, `wheel`, `focus`, `snapshot`. |
| `ref` | int | ● | Auslösendes Ereignis: bei `dwell` das letzte `mouse.move`-Bündel vor dem Verweilen, sonst das Maus- bzw. `focus`-Ereignis. |
| `status` | enum | ● | `ok`, `timeout`, `notAvailable`, `accessDenied`, `skipped` (ausgeschlossene Anwendung, außerhalb des Bereichs), `error`. |
| `src` | enum | ● | `uia`, `msaa`, `win32` oder `none` (bei `skipped`). |
| `ms` | int | ● | Dauer der Abfrage in Millisekunden. |
| `ep`, `m`, `x`, `y` | – | ◐ | Treffpunkt; Pflicht bei allen punktbezogenen Anlässen. |
| `win` | string | ○ | Fenster, zu dem das Element gehört. |
| `el` | ElementDescriptor | ◐ | Pflicht bei `status: "ok"`. |
| `path` | ElementRef[] | ○ | Vorfahren vom Fenster bis zum Elternelement, von außen nach innen. |
| `dwellRef` | int | ○ | Nur bei `buttonDown`: `seq` des `element`-Ereignisses mit `trigger: "dwell"`, das der Recorder beim Tastendruck als aktuell betrachtete. |
| `dwellValid` | bool | ○ | Nur bei `buttonDown`: ob dieses Vorab-Ergebnis beim Tastendruck noch als gültig galt. |
| `dwellInvalidatedBy` | enum | ○ | Wenn nicht mehr gültig: `age`, `moved`, `wheel`, `key`, `windowEvent`, `screenChange`. |
| `win32` | Win32Context | ○ | Bei `buttonDown`, `buttonUp`, `wheel` und `focus`: Win32-Kontext für Automatisierung (AutoHotkey), siehe unten. Wird auch geschrieben, wenn die UIA-Ermittlung scheitert. |

**Win32Context** (`win32`) – so, wie AutoHotkey Fenster und Steuerelemente anspricht. Alle Koordinaten sind physische Pixel im virtuellen Bildschirm (nicht monitorlokal wie `rect` im ElementDescriptor).

| Feld | Bedeutung |
|---|---|
| `window` | Hauptfenster: `hwnd`, `title`, `class` (ahk_class), `exe` (ahk_exe), `path`, `pid`, `rect` und `client` (Bildschirmrechtecke), `state` (`normal` \| `maximized` \| `minimized`), `topmost`, `dpi`, `owner` (`hwnd`, `title`, `class`). |
| `point` | Treffpunkt als `[x, y]`: `screen`, `window` (relativ zur linken oberen Fensterecke, AHK `CoordMode "Window"`), `client` (relativ zum Clientbereich). |
| `control` | Tiefstes sichtbares Steuerelement unter dem Punkt bzw. (bei `focus`) mit Tastaturfokus: `hwnd`, `class`, `classNN` (Klassenname + laufende Nummer unter allen Nachfahren wie bei AutoHotkey), `id` (Steuerelement-ID), `rect` (clientrelativ wie `ControlGetPos`), `screenRect`, `point` (Treffpunkt im Steuerelement), `style`, `exStyle`, `enabled`, `visible`, `parentClass`, `text` bzw. `textWithheld` (`password` \| `editable` \| `privacy`). |
| `focus` | Steuerelement mit Tastaturfokus zum Klickzeitpunkt (gleiche Felder wie `control`). |
| `pixel` | Farbe am Treffpunkt als `0xRRGGBB` (wie `PixelGetColor`). |
| `texts` | Sichtbare Texte der nicht editierbaren Steuerelemente (`c` = ClassNN, `t` = Text), höchstens 60 – entspricht `WinGetText` ohne Feldinhalte. |
| `ahk` | Fertige Angaben: `winTitle` (`ahk_class … ahk_exe …`), `winTitleFull` (mit Titel), `control` (ClassNN). |

Datenschutz: Inhalte editierbarer Steuerelemente (Edit, RichEdit, Scintilla, Browser- und Terminalflächen) werden hier nie gelesen – sie stehen ausschließlich über UIA mit Maskierung und Durchsicht im Journal. Passwortfelder (`ES_PASSWORD`) werden erkannt. Im Privatmodus fehlen alle Texte, in der Schutzstufe „streng“ fehlen `texts`. Ausgeschlossene Anwendungen erhalten keinen Win32-Kontext.

`t` ist der Beginn der Abfrage, `tObs` ihr Ende. Die Entscheidung, ob Vorab- und Tastendruck-Ergebnis **übereinstimmen**, trifft nicht der Recorder, sondern die [Schritterkennung](05-schritterkennung.md#zielbestimmung); deshalb stehen beide Ergebnisse vollständig im Journal.

```json
{"seq":1733,"t":83270200,"tObs":83304900,"type":"element","trigger":"dwell","ref":1732,"status":"ok","src":"uia","ms":34,"ep":0,"m":"M1","x":1056,"y":701,"win":"w13","el":{"rid":"42.660192.4.1","ct":"Button","lct":"Schaltfläche","name":"Speichern","aid":"1","cls":"Button","fw":"Win32","hwnd":"0x000A12F4","rect":{"m":"M1","x":1012,"y":688,"w":88,"h":26},"enabled":true,"patterns":["Invoke","LegacyIAccessible"],"access":"Alt+S","dupIndex":1,"dupCount":1},"path":[{"ct":"Window","name":"Speichern unter"}]}
{"seq":1736,"t":83412300,"tObs":83441800,"type":"element","trigger":"buttonDown","ref":1734,"status":"ok","src":"uia","ms":29,"ep":0,"m":"M1","x":1056,"y":701,"win":"w13","dwellRef":1733,"dwellValid":true,"el":{"rid":"42.660192.4.1","ct":"Button","lct":"Schaltfläche","name":"Speichern","aid":"1","cls":"Button","fw":"Win32","hwnd":"0x000A12F4","rect":{"m":"M1","x":1012,"y":688,"w":88,"h":26},"enabled":true,"patterns":["Invoke","LegacyIAccessible"],"access":"Alt+S","dupIndex":1,"dupCount":1},"path":[{"ct":"Window","name":"Speichern unter"}]}
```

---

<a id="ev-automation"></a>
### 9.12a `cursor` und `win32.state` – Wartesignale und Zustand

| Typ | Felder | Bedeutung |
|---|---|---|
| `cursor` | `shape` (`arrow` \| `ibeam` \| `wait` \| `appStarting` \| `hand` \| … \| `custom` \| `hidden`), `busy` (bool) | Wechsel des Mauszeigers in den Zustand „beschäftigt“ (`wait`, `appStarting`) und zurück; alle 100 ms geprüft, nur Wechsel werden geschrieben. |
| `win32.state` | `win`, `state` (`hwnd`, `title`, `class`, `exe`, `focus` = ClassNN, `texts` wie im Win32Context, `cursor`) | Zustand des Vordergrundfensters 700 ms nach der letzten Eingabe (Ruhe). Grundlage für `verify` und Erfolgsprüfungen. |

Der Win32Context enthält zusätzlich `cursor` (Zeigerform beim Klick), in `window` die Felder `app` (`product`, `description`, `company`, `version`, `commandLine` – Argumente mit Passwort-, Schlüssel-, Token- oder E-Mail-Mustern werden ausgelassen –, `elevated`) und `kbLayout`; Einträge in `texts` tragen `r` (Rechteck im Clientbereich), `checked` (`checked` \| `unchecked` \| `mixed` für Kontroll- und Optionsfelder) und `enabled`; `control` trägt `checked`. Der ElementDescriptor trägt `idx`/`sibCount` (Position unter gleichartigen Geschwistern), `nameDup` (Name mehrfach vorhanden), `range` und `cell`.

---

<a id="ev-ui"></a>
### 9.13 `ui.*`

Zustandsänderungen von Elementen, gemeldet von UI Automation, WinEvents oder durch Nachlesen am fokussierten Element. Gemeinsame Felder: `el` ● (ElementRef), `win` ○.

| Typ | Weitere Felder | Bedeutung |
|---|---|---|
| `ui.value` | `phase` ● (`focusIn` \| `change` \| `focusOut` \| `poll`), `value` ◐, `len` ○, `truncated` ○, `caret` ○ (`before`, `after`: je höchstens 80 Zeichen um die Einfügemarke, wenn der Wert gekürzt ist), `sel` ○ (`[start, end]`), `withheld` ○ | Inhalt eines Felds zu einem Zeitpunkt. Grundlage für „Feldinhalt vorher/nachher“. |
| `ui.text` | `change` ● (`composition` \| `compositionFinalized` \| `autoComplete` \| `autoCorrect` \| `unknown`), `text` ◐, `withheld` ○ | Text wurde ohne direkte Entsprechung zu Tasten geändert (Eingabemethode, Autovervollständigung). |
| `ui.toggle` | `state` ● (`on` \| `off` \| `mixed`) | Umschaltzustand geändert. |
| `ui.selection` | `items` ○ (ElementRef[], höchstens 20), `count` ○, `text` ○, `textLen` ○, `withheld` ○ | Auswahl geändert. `el` ist der Behälter (Liste, Baum, Register, Textfeld); `items` die gewählten Einträge, `text` der markierte Text. |
| `ui.expand` | `state` ● (`expanded` \| `collapsed` \| `partial` \| `leaf`) | Auf-/zugeklappt. |
| `ui.invoke` | – | Element wurde ausgelöst (auch per Tastatur). |
| `ui.scroll` | `hPct` ●, `vPct` ● | Neue Scrollposition (0–100, `-1` = nicht scrollbar). |
| `ui.notification` | `text` ◐, `kindOf` ● (`notification` \| `liveRegion` \| `toast` \| `alert`), `withheld` ○ | Mitteilung der Anwendung oder des Systems. `el` ist hier optional. |

Inhalte (`value`, `text`) durchlaufen dieselbe Prüfung wie Tasteneingaben: Für Passwortfelder, im Privatmodus und in ausgeschlossenen Anwendungen fehlt der Inhalt, und `withheld` nennt den Grund.

```json
{"seq":1665,"t":80183000,"type":"ui.value","el":{"rid":"42.660192.4.1001","ct":"Edit","name":"Dateiname:","aid":"1001"},"win":"w13","phase":"focusIn","value":"*.txt","sel":[0,5]}
{"seq":1725,"t":82300000,"tObs":82312400,"tSrc":"sys","tErr":16000,"type":"ui.value","el":{"rid":"42.660192.4.1001","ct":"Edit","name":"Dateiname:","aid":"1001"},"win":"w13","phase":"change","value":"notiz.txt","sel":[9,9]}
```

---

<a id="ev-ui-snapshot"></a>
### 9.14 `ui.snapshot`

Begrenzter Text-Schnappschuss eines neu erschienenen Fensters, Dialogs, Menüs oder Popups. Er hält fest, was dort **stand** – Wortlaut einer Meldung, Einträge eines Menüs, Beschriftungen der Schaltflächen –, unabhängig vom Bild.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `ref` | int | ● | Auslösendes Ereignis (`window.open`, `window.menu`, `window.dialog` oder `marker`). |
| `win` | string | ● | Fenster. |
| `trigger` | enum | ● | `windowOpen`, `menuOpen`, `dialogStart`, `popup`, `manual`. |
| `status` | enum | ● | Wie bei `element`, zusätzlich `partial` (Zeit- oder Knotengrenze erreicht). |
| `ms` | int | ● | Dauer in Millisekunden. |
| `ep`, `m` | – | ● | Monitor, auf den sich die Knotenrechtecke beziehen. |
| `limits` | Objekt | ○ | Angewendete Grenzen: `maxNodes`, `maxDepth`, `maxMs`. |
| `nodes` | Objekt[] | ● | Knoten in Lesereihenfolge; darf bei Fehlschlag leer sein. |

Je Knoten:

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `i` | int | ● | Laufende Nummer ab 0. |
| `p` | int | ● | Nummer des Elternknotens, `-1` für die Wurzel. |
| `ct` | string | ● | Steuerelementtyp. |
| `name`, `aid`, `rid` | string | ○ | Wie in der Elementbeschreibung. |
| `value` | string | ○ | Wert, höchstens 200 Zeichen. |
| `valueWithheld` | enum | ○ | Grund, wenn der Wert fehlt. |
| `r` | int[4] | ○ | Rechteck `[x, y, w, h]` relativ zu Monitor `m`. |
| `st` | string[] | ○ | Zustände: `disabled`, `offscreen`, `focused`, `selected`, `on`, `off`, `mixed`, `expanded`, `collapsed`, `password`, `default`, `readOnly`. |

```json
{"seq":1666,"t":80160000,"tObs":80248000,"type":"ui.snapshot","ref":1660,"win":"w13","trigger":"windowOpen","status":"ok","ms":88,"ep":0,"m":"M1","limits":{"maxNodes":300,"maxDepth":12,"maxMs":250},"nodes":[
 {"i":0,"p":-1,"ct":"Window","name":"Speichern unter","r":[400,180,820,560]},
 {"i":1,"p":0,"ct":"Text","name":"Dateiname:","r":[420,624,90,18]},
 {"i":2,"p":0,"ct":"Edit","name":"Dateiname:","aid":"1001","value":"*.txt","r":[520,620,560,26],"st":["focused"]},
 {"i":3,"p":0,"ct":"ComboBox","name":"Dateityp:","value":"Textdateien (*.txt)","r":[520,652,560,26],"st":["collapsed"]},
 {"i":4,"p":0,"ct":"Button","name":"Speichern","aid":"1","r":[1012,688,88,26],"st":["default"]},
 {"i":5,"p":0,"ct":"Button","name":"Abbrechen","aid":"2","r":[1108,688,88,26]}]}
```

(Im Journal steht das Ereignis in einer einzigen Zeile; hier zur Lesbarkeit umbrochen und um die Knoten der Ordneransicht gekürzt.)

---

<a id="ev-frame"></a>
### 9.15 `frame`

Ein `frame`-Ereignis ist eine **Beobachtung des Bildschirms**: „So sah Monitor M zum Zeitpunkt `t` aus.“ Jeder Bildauslöser erzeugt genau ein `frame`-Ereignis mit eigener Kennung – auch wenn sich das Bild nicht geändert hat; dann verweist es auf ein vorhandenes Bild und belegt keinen Speicher. Wie die Bilddaten abgelegt sind, steht in [Bildspeicher](#bildspeicher).

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `id` | string | ● | Bild-Kennung `f` + mindestens sechs Ziffern, sitzungsweit fortlaufend. |
| `ep`, `m` | – | ● | Epoche und Monitor. |
| `w`, `h` | int | ● | Größe in Pixeln (= Monitorgröße der Epoche). |
| `trigger` | enum | ● | Auslöser, siehe unten. |
| `ref` | int | ◐ | Auslösendes Ereignis. Pflicht bei `preInput`, `settle`, `manual`, `comment`. |
| `guarantee` | enum | ◐ | Pflicht bei `preInput`: `beforeInput` (Darstellungszeit liegt nachweislich vor dem Zeitstempel der Eingabe), `atInput` (zum Eingabezeitpunkt aufgenommen, ohne Nachweis – z. B. beim GDI-Rückfall), `none`. |
| `settle` | Objekt | ◐ | Pflicht bei `settle`: `reason` (`quiet` = Bildschirm kam zur Ruhe \| `timeout` = Obergrenze erreicht \| `nextInput` = nächste Eingabe kam zuvor), `quietMs`. |
| `store` | Objekt | ● | Ablage, siehe [Bildspeicher](#bildspeicher). |
| `pixelHash` | string | ○ | Schneller Hash über die Pixel des vollständigen Bildes (`"xxh3:<hex>"`), zum Prüfen der Wiederherstellung. |
| `cursor` | Objekt | ○ | Mauszeiger zum Bildzeitpunkt: `x`, `y` (aktiver Punkt, monitorrelativ), `cursor` (Kennung), `visible`, `baked` (`true`, wenn der Aufnahmeweg den Zeiger bereits ins Bild gezeichnet hat). Ohne `baked` enthält das Bild **keinen** Zeiger. |
| `changed` | Objekt | ○ | Änderung gegenüber dem vorigen `frame`-Ereignis desselben Monitors: `ratio` (Anteil geänderter Pixel, 0–1), `rects` (höchstens 16 Rechtecke `[x, y, w, h]`). |
| `scopeRects` | int[4][] | ○ | Bei eingeschränktem Aufnahmebereich: die Flächen mit echtem Inhalt. Alles außerhalb ist schwarz gefüllt. |
| `masks` | Objekt[] | ○ | Zusätzlich gefüllte Flächen innerhalb des Inhalts: `x`, `y`, `w`, `h`, `why` (`excludedApp` \| `passwordField` \| `privacyMode` \| `protectedContent` \| `occluded` \| `outOfScope` \| `redacted` \| `substituted`), bei `substituted` zusätzlich `label` (Name des Platzhalters); siehe [11.5](#bildspeicher). |
| `protected` | bool | ○ | Das System hat geschützte Inhalte ausgeblendet. |
| `hdr` | bool | ○ | Das Bild wurde von HDR nach 8 Bit umgerechnet. |
| `backend` | enum | ○ | Aufnahmeweg dieses Bildes, falls abweichend von der Topologie. |

| `trigger` | Wann | `ref` zeigt auf |
|---|---|---|
| `preInput` | Vorher-Bild zu einer Eingabe (Maustaste, Radbeginn, Tastendruck – beim Tippen gedrosselt) | die Eingabe |
| `settle` | Nachher-Bild, sobald sich der Bildschirm nach einer Eingabe beruhigt hat | die letzte Eingabe vor der Beruhigung |
| `change` | Wesentliche Änderung ohne Eingabe (Zwischenbild) | letzte Eingabe davor (optional) |
| `manual` | Schnappschuss-Taste und jeder Marker des Autors, gleich welchen Typs | das `marker`-Ereignis |
| `comment` | Eingefrorenes Bild für einen Kommentar | das `comment`-Ereignis |
| `sessionStart`, `resume`, `sessionStop` | Beginn eines Abschnitts, Ende einer Pause, letztes Bild beim Stopp | – |
| `topology`, `scope` | Erstes Bild nach Monitor- bzw. Bereichswechsel | das auslösende Ereignis (optional) |
| `periodic` | Zeitgesteuert (nur wenn eingeschaltet) | – |

Die Bildauslöser sind bewusst **unabhängig von den Schrittregeln**: Der Recorder speichert zu jeder Eingabe ein Vorher-Bild und zu jeder Beruhigung ein Nachher-Bild, gleichgültig, welche Schritte später daraus werden. So haben auch neu berechnete Schritte ihre Bilder.

```json
{"seq":1701,"t":80198000,"tObs":81291200,"tSrc":"present","type":"frame","id":"f000024","ep":0,"m":"M1","w":2560,"h":1440,"trigger":"preInput","ref":1700,"guarantee":"beforeInput","store":{"kind":"same","as":"f000023"}}
{"seq":1735,"t":83108300,"tObs":83413900,"tSrc":"present","type":"frame","id":"f000029","ep":0,"m":"M1","w":2560,"h":1440,"trigger":"preInput","ref":1734,"guarantee":"beforeInput","store":{"kind":"delta","base":"f000028","rect":{"x":1012,"y":688,"w":88,"h":26},"blob":"sha256:a41d…09c2","bytes":612},"cursor":{"x":1056,"y":701,"cursor":"cur1","visible":true}}
{"seq":1745,"t":83611000,"tObs":83996000,"tSrc":"present","type":"frame","id":"f000030","ep":0,"m":"M1","w":2560,"h":1440,"trigger":"settle","ref":1737,"settle":{"reason":"quiet","quietMs":300},"store":{"kind":"delta","base":"f000028","rect":{"x":293,"y":131,"w":927,"h":609},"blob":"sha256:5c0e…41bb","bytes":48211},"pixelHash":"xxh3:8f1c22a09b3d4e57","changed":{"ratio":0.13,"rects":[[400,180,820,560],[293,131,900,32]]},"cursor":{"x":1056,"y":701,"cursor":"cur1","visible":true}}
```

Zur Zeit eines Bildes: `t` ist die **Darstellungszeit des gezeigten Inhalts** – der Moment, in dem dieses Bild auf dem Bildschirm erschien. Bei einem Vorher-Bild liegt `t` daher stets vor der Eingabe, auf die `ref` zeigt, und kann bei unverändertem Bildschirm deutlich davor liegen (erste Zeile: der Bildschirm zeigte seit 80,198 s unverändert dasselbe Bild, die Taste folgte bei 81,290 s). In der zweiten Zeile hat sich nur die Schaltfläche geändert, als der Zeiger sie erreichte; gespeichert wird allein dieser Ausschnitt.

---

<a id="ev-clipboard"></a>
### 9.16 `clipboard`

Die Zwischenablage hat sich geändert. Die **Herkunft** wird immer aufgezeichnet, der **Inhalt** nur auf ausdrücklichen Wunsch. Ohne Inhaltsaufzeichnung wird der Inhalt **gar nicht abgerufen** ([Aufzeichnungskern](03-aufzeichnungskern.md#zwischenablage), [Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md)); Angaben, die einen Abruf voraussetzen, fehlen dann. Das Verbot des Abrufs geht jeder Größenangabe vor.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `clip` | string | ● | Laufende Kennung des Inhalts `cb<n>`. |
| `seqNo` | int | ○ | Folgenummer der Zwischenablage laut System. |
| `kind` | enum | ● | `text`, `image`, `files`, `html`, `mixed`, `other`, `empty` – abgeleitet aus den Formatnamen. |
| `formats` | string[] | ○ | Namen der angebotenen Formate, höchstens 20. |
| `size` | int | ○ | Größe des Inhalts: Zeichen (Text) oder Anzahl (Dateien). Nur vorhanden, wenn der Inhalt abgerufen wurde – also bei eingeschalteter Inhaltsaufzeichnung – oder sich die Angabe ohne Abruf feststellen ließ. |
| `sizes` | Objekt | ○ | Größe je Format in Bytes, z. B. `{ "CF_UNICODETEXT": 70 }`. **Wahlfrei und im Regelfall nicht vorhanden:** Ein Format steht nur darin, soweit sich seine Größe **ohne Abruf des Inhalts** feststellen ließ oder es bei eingeschalteter Inhaltsaufzeichnung ohnehin abgerufen wurde. Nach heutigem Kenntnisstand gibt es ohne Abruf keinen dokumentierten Weg; ohne Inhaltsaufzeichnung fehlt das Feld dann ganz. Kein Leser verlässt sich darauf. |
| `ownerWin` | string | ○ | Fenster, das den Inhalt bereitgestellt hat, sonst das Vordergrundfenster. |
| `sameAs` | string | ○ | Kennung eines früheren, inhaltsgleichen Eintrags dieser Sitzung – nur bei eingeschalteter Inhaltsaufzeichnung. Der Vergleich geschieht nur im Arbeitsspeicher; ein Hash des Inhalts wird nicht gespeichert. |
| `content` | string | ○ | Textinhalt, höchstens `valueMaxChars` Zeichen – nur bei eingeschalteter Inhaltsaufzeichnung. |
| `truncated` | bool | ○ | `content` wurde gekürzt. |
| `sensitive` | bool | ○ | Der Inhalt ist von der Quelle als vertraulich markiert. |
| `withheld` | enum | ◐ | Pflicht, wenn `content` fehlt und `kind` nicht `empty` ist: `optOut` (Standard: Inhaltsaufzeichnung ist aus), `sensitiveFormat` (von der Quelle als vertraulich gekennzeichnet), `password` (aus einem Passwortfeld kopiert), `strict` (Schutzstufe „Streng“), `excludedApp`, `privacyMode`, `tooLarge`. |

```json
{"seq":3120,"t":140220000,"type":"clipboard","clip":"cb2","seqNo":418,"kind":"text","formats":["CF_UNICODETEXT","CF_TEXT","CF_LOCALE"],"ownerWin":"w12","withheld":"optOut"}
{"seq":6120,"t":251300000,"type":"clipboard","clip":"cb4","seqNo":433,"kind":"text","formats":["CF_UNICODETEXT","CF_TEXT","CF_LOCALE"],"size":34,"sizes":{"CF_UNICODETEXT":70},"ownerWin":"w12","content":"Angebot 2026-114 Projekt Nordhafen"}
```

Die erste Zeile ist der Regelfall ohne Inhaltsaufzeichnung: Herkunft und Formate, **keine Größe**. Die zweite stammt aus einer Sitzung mit eingeschalteter Inhaltsaufzeichnung: Der Text wurde abgerufen, deshalb stehen dort `size` (34 Zeichen) und die Größe des abgerufenen Formats.

---

<a id="ev-comment"></a>
### 9.17 `comment`

Kommentar, den der Nutzer **während der Aufnahme** eingibt. (Nachträgliche Kommentare stehen in [`edits.json`](#edits-json).)

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `id` | string | ● | Kennung `cm<n>`. |
| `text` | string | ● | Kommentartext. |
| `frame` | string | ○ | Kennung des eingefrorenen Bildes. |
| `region` | Rect | ○ | Markierter Bereich in diesem Bild; dann ist `ep` Pflicht. |
| `gapId` | string | ○ | Lücke `recorderInteraction`, die die Dauer der Eingabe abdeckt. |

`t` ist der Moment, in dem der Kommentar ausgelöst wurde (zu diesem Zeitpunkt wurde das Bild eingefroren), `tObs` der Moment des Bestätigens.

---

<a id="ev-marker"></a>
### 9.18 `marker`

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `id` | string | ● | Kennung `mk<n>`. |
| `kind` | enum | ● | Art des Markers, siehe Tabelle unten. |
| `label` | string | ○ | Kurze Bezeichnung in den Worten des Autors, höchstens 200 Zeichen. |
| `frame` | string | ◐ | Kennung des zum Marker aufgenommenen Bildes (`frame` mit `trigger: "manual"` und `ref` auf den Marker). Pflicht bei `snapshot` und bei jedem Marker des Autors – `note`, `error`, `expected`, `important`: Ein Marker sagt etwas über den Bildschirm in genau diesem Moment. |
| `tFrom` | µs | ◐ | Nur bei `redactLastInput` (dort Pflicht): Beginn des zu entfernenden Zeitraums – das `t` der ersten Taste des betroffenen Tastenlaufs. Das Ende ist das `t` des Markers; `ref` zeigt auf diese erste Taste. |
| `redactionId` | string | ◐ | Nur bei `redactLastInput` (dort Pflicht): Kennung der zugehörigen Schwärzung in `edits.json`. |

| `kind` | Gruppe | Bedeutung |
|---|---|---|
| `note` | Marker des Autors | Einfache Sprungmarke in der Zeitachse. Standard, wenn kein Typ gewählt wird. |
| `error` | Marker des Autors | „Hier tritt der Fehler auf.“ |
| `expected` | Marker des Autors | „So sollte es aussehen.“ |
| `important` | Marker des Autors | Besonders wichtige Stelle. |
| `snapshot` | Befehl | Schnappschuss-Taste: eigener Schritt mit Bild. |
| `discardPrevious` | Befehl | Der Nutzer verwirft den letzten Schritt. |
| `redactLastInput` | Befehl | „Letzte Eingabe schwärzen“. |

**Marker des Autors** (`note`, `error`, `expected`, `important`) werden zu `marker`-Schritten, die ihren Typ als `input.markerKind` tragen ([12.7](#steps-json)). Sie sind – wie die Ziel-Angaben – **Aussagen des Autors, keine Beobachtungen**: Ein Marker `error` belegt nicht, dass ein Fehler auftrat, sondern dass der Autor an dieser Stelle einen sah. Der Typ lässt sich im Viewer nachträglich ändern (`edits.json`, `steps[].markerKind`); das Journal bleibt dabei unverändert.

Der frühere Wert `marker` ist gleichbedeutend mit `note`: Leser behandeln beide gleich, Schreiber verwenden `note`. Einen unbekannten Wert behandeln Leser wie `note` (G6).

`discardPrevious` löscht nichts: Die Rohdaten bleiben erhalten, die Schritterkennung kennzeichnet den betroffenen Schritt als verworfen.

`redactLastInput` ändert im Moment des Befehls ebenfalls nichts am Journal. Der Marker hält nur fest, **welcher Zeitraum** zu entfernen ist, und trägt selbst keinen Inhalt. Er macht das Journal eigenständig (G3): Auch ohne `edits.json` steht fest, was beim Stopp oder bei der Wiederherstellung endgültig zu schwärzen ist. Ablauf und Umfang stehen unter [Schwärzen während der Aufnahme](#redact-last-input).

```json
{"seq":2190,"t":118400000,"type":"marker","id":"mk3","kind":"redactLastInput","ref":2140,"tFrom":112350000,"redactionId":"r1"}
```

Ein Marker des Autors mit Typ und dem Bild, das zu ihm aufgenommen wurde:

```json
{"seq":3480,"t":154200000,"type":"marker","id":"mk4","kind":"error","frame":"f000131"}
{"seq":3481,"t":154150000,"tObs":154203400,"tSrc":"present","type":"frame","id":"f000131","ep":0,"m":"M1","w":2560,"h":1440,"trigger":"manual","ref":3480,"store":{"kind":"delta","base":"f000130","rect":{"x":820,"y":500,"w":920,"h":300},"blob":"sha256:d2c7…1f08","bytes":20944}}
```

---

<a id="ev-gap"></a>
### 9.19 `gap`

Eine Lücke ist eine Zeitspanne, in der der Recorder etwas nicht aufzeichnen **konnte** oder **durfte**. Lücken werden beim Beginn und beim Ende geschrieben, damit sie auch einen Absturz überstehen.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `gapId` | string | ● | Kennung `g<n>`; Beginn und Ende tragen dieselbe. |
| `phase` | enum | ● | `begin`, `end` oder `single` (Beginn und Ende in einem Ereignis). |
| `cause` | enum | ● | Ursache, siehe unten. |
| `tFrom` | µs | ● | Beginn. Bei `begin` und `single` ist `t` = `tFrom`. |
| `tTo` | µs | ◐ | Ende; Pflicht bei `end` und `single`. Bei `end` ist `t` = `tTo`. |
| `channels` | string[] | ● | Betroffene Kanäle: `keyboard`, `mouse`, `frames`, `elements`, `windows`, `clipboard`, `fieldValues` (Feldinhalte, Wertänderungen, Werte in Text-Schnappschüssen) oder `all`. |
| `inputLost` | enum | ● | Ob in der Lücke Nutzereingaben verloren gegangen sein können: `none`, `possible`, `certain`, `unknown`. |
| `win` | string | ○ | Betroffenes Fenster. |
| `detail` | Objekt | ○ | Ursachenspezifische Angaben, nie aufgezeichnete Inhalte. |

Ein `begin` ohne zugehöriges `end` gilt bis zum Ende des Abschnitts.

**Angaben in `detail`** – je Ursache festgelegt; alle sind wahlfrei, soweit nicht anders vermerkt:

| Ursache | Felder in `detail` | Bedeutung |
|---|---|---|
| `excludedApp` | `rule` (Pflicht) | Anzeigename der Ausschlussregel. **Keine** Zähler, kein Programmname, kein Titel. Auch die Eingabenummer `inSeq` läuft in dieser Lücke nicht weiter. |
| `outOfScope` | `counts` | Am Ende-Ereignis: Zahl der nicht aufgezeichneten Ereignisse je Art – `keys`, `clicks`, `wheel`, `moves`. Nur Zahlen; keine Inhalte, Positionen oder Einzelzeiten. |
| `privacyMode` | `trigger`, `rule`, am Ende-Ereignis `end` | Auslöser, Regel und Endgrund – wie beim `system`-Ereignis `privacyMode`. |
| `secureDesktop` | `desktop`, `kind` | `desktop`: Name des aktiven Desktops (wie bei `system`/`desktopSwitch`). `kind`: `uac` (Abfrage der Benutzerkontensteuerung; erkannt daran, dass der Prozess `consent.exe` läuft) oder `other` (etwa Strg+Alt+Entf, Bildschirmschoner). Beim `begin` steht die Art, die zu diesem Zeitpunkt erkennbar war; maßgeblich ist `kind` im `end`-Ereignis (erkennt der Recorder die Abfrage erst später, wird aus `other` noch `uac`). Während der Lücke werden keine Bilder aufgenommen; nach ihrem Ende folgen `keystate.sync` (`reason: "secureDesktop"`) und ein erzwungenes Schlüsselbild (`trigger: "resume"`). |
| `hookLost`, `inputMismatch` | `channel`, `hookEvents`, `rawEvents` | Betroffener Kanal und die Zähler beider Kanäle im Zeitraum. |
| `captureLost` | `m`, `reason` | Monitor und Anlass (`accessLost`, `modeChange`, `backendSwitch`). |
| `frameDropped` | `frame` | Kennung des betroffenen Bildes. |
| `overload` | `dropped` | Was verworfen wurde (`frames`, `elements`, `snapshots`) mit Anzahl. |

| `cause` | Bedeutung | Typische Kanäle | `inputLost` |
|---|---|---|---|
| `paused` | Aufnahme angehalten: vom Nutzer – oder sie wartet nach Sperre, Benutzerwechsel oder Trennen auf die Bestätigung zum Fortsetzen ([9.2](#ev-session)). | `all` | `possible` |
| `privacyMode` | Privatmodus (Privattaste, erkannte Passwortabfrage): Inhalte werden zurückgehalten. | **verbindlich:** immer `keyboard`, `clipboard`, `fieldValues`; zusätzlich `frames`, wenn „Im Privatmodus auch Bilder aussetzen“ eingeschaltet ist | `none` |
| `excludedApp` | Eine Anwendung der Ausschlussliste ist im Vordergrund. | `keyboard`, `mouse`, `elements`, `clipboard` | `certain` |
| `outOfScope` | Arbeit außerhalb des Aufnahmebereichs. | `keyboard`, `mouse`, `elements` | `certain` |
| `secureDesktop` | Sicherer Desktop aktiv (UAC-Abfrage, Anmeldebildschirm). | `all` | `possible` |
| `sessionLocked` | Arbeitsstation gesperrt. Auf ihr Ende folgt stets eine Lücke `paused` bis zur Bestätigung. | `all` | `possible` |
| `sessionDisconnected` | Sitzung getrennt (Remote-Desktop, Benutzerwechsel). Auf ihr Ende folgt stets eine Lücke `paused` bis zur Bestätigung. | `all` | `unknown` |
| `suspend` | Rechner im Ruhezustand. | `all` | `none` |
| `elevatedWindow` | Fenster mit höheren Rechten im Vordergrund; Eingaben sind für den Recorder nicht sichtbar. | `keyboard`, `mouse`, `elements` | `possible` |
| `hookLost` | Ein Eingabe-Hook lieferte nicht mehr; erkannt über den zweiten Kanal. | `keyboard` oder `mouse` | `possible` |
| `inputMismatch` | Unerklärte Abweichung zwischen beiden Eingabekanälen. | `keyboard` oder `mouse` | `possible` |
| `captureLost` | Bildschirmaufnahme vorübergehend nicht möglich (Zugriff verloren, Moduswechsel). | `frames` | `none` |
| `frameDropped` | Ein einzelnes Bild konnte nicht aufgenommen oder gespeichert werden. | `frames` | `none` |
| `protectedContent` | Geschützte Inhalte wurden vom System ausgeblendet. | `frames` | `none` |
| `recorderInteraction` | Nutzer bedient den Recorder (Kommentar, Leiste). | `all` | `none` |
| `diskFull` | Kein Speicherplatz; Aufnahme angehalten. | `all` | `possible` |
| `overload` | Verarbeitung kam nicht nach; Daten wurden verworfen. | je nach Fall | `possible` |
| `crash` | Recorder wurde unerwartet beendet; von der Wiederherstellung eingetragen. | `all` | `unknown` |
| `unsupportedInput` | Eingabeart erkannt, aber nicht aufzeichenbar (z. B. Mehrfinger-Geste). | `mouse` | `certain` |
| `relativeMouse` | Eine Anwendung hat den Mauszeiger gefangen und wertet nur relative Bewegung aus (Spiele, 3D-Ansichten); Positionen sind nicht aussagekräftig. | `mouse` | `none` |
| `unknown` | Ursache nicht feststellbar. | – | `unknown` |

Zurückgehaltene Tasten in Passwortfeldern erzeugen kein `gap`, sondern [`text.masked`](#ev-text-masked); im Vollständigkeitsbericht erscheinen sie unter der Ursache `maskedInput`.

```json
{"seq":4012,"t":131000000,"type":"gap","gapId":"g1","phase":"begin","cause":"secureDesktop","tFrom":131000000,"channels":["all"],"inputLost":"possible","detail":{"desktop":"Winlogon","kind":"uac"}}
{"seq":4019,"t":139000000,"type":"gap","gapId":"g1","phase":"end","cause":"secureDesktop","tFrom":131000000,"tTo":139000000,"channels":["all"],"inputLost":"possible","detail":{"desktop":"Winlogon","kind":"uac"}}
```

<a id="ev-process"></a>
### 9.20 `process.info`, `process.exit`, `window.hang`, `eventlog.entry`

Programm-Kontext (Paket 10 aus [12](12-entwickler-fahrplan.md)); MINOR-Erweiterung im Sinn von 15.2. Quelle ist `ProcessWatch` im Aufzeichnungskern ([03 §8.5](03-aufzeichnungskern.md#85-programm-kontext)).

| Typ | Felder | Bedeutung |
|---|---|---|
| `process.info` | `proc` (`p<n>`), `pid`, `process` (Dateiname), `win` (erstes Fenster), `startedBeforeSession`, `product`, `version`; mit „Erweitertem Programmkontext“ `build` (`fileTimeUtc`, `size`, `peTimestampUtc` bzw. `peTimestamp`, `pdbId`, `pdbFile`) und `git` (`branch`, `commit`, `detached`, `basis: "workingTree"`, `dirty: "notChecked"`); `handle: "denied"`, wenn das Ende nicht beobachtbar ist | Ein Programm, mit dessen Fenster gearbeitet wurde. Zeitpunkt: erstes Fenster. Nie Pfade, Befehlszeilen, Umgebung, Fernadressen, Autoren oder Commit-Texte |
| `process.exit` | `proc`, `pid`, `process`, `win` (letztes Fenster), `exitCode`, `exitCodeHex`, `class` (`normal`, `error`, `crash`, `unknown`), `wasHung` | Programmende; `t` aus der Endzeit des Prozesses. `crash` nur bei eindeutigen Fehlercodes (`ExitCodes.Classify`) |
| `window.hang` | `phase` (`start`, `end`), `win`, `proc`, `process`, `durationMs`, `untilStop` | Ein sichtbares Fenster des Programms reagiert nicht (`IsHungAppWindow`) |
| `eventlog.entry` | `proc`, `process`, `win`, `eventId` (1000, 1002, 1026), `recordId`, `provider`, `facts` (`app`, `appVersion`, `module`, `moduleVersion`, `exceptionCode`, `offset`, `exceptionType`); `text` nur mit Paket 12 | Eintrag des Anwendungsprotokolls zu einem aufgenommenen Programm, im Zeitraum der Aufnahme |

Mit der Quelle „Fehlertexte der Ereignisanzeige“ (Paket 12) trägt `eventlog.entry` für 1000 und 1026 zusätzlich `text` (Beschreibung des Eintrags, Profilpfade ersetzt, höchstens 8 000 Zeichen, `truncated`) – inhaltstragend und prüfpflichtig.

`process.*`, `window.hang` und `eventlog.entry` sind **nicht** strukturell (`Redactor.Structural`): Ein geschwärzter Zeitraum leert sie. `text` ist inhaltstragend und unterliegt der Durchsicht (Paket 12).

```json
{"seq":120,"t":100000,"type":"process.info","proc":"p1","pid":4242,"process":"MyApp.exe","win":"w3","startedBeforeSession":false,"product":"MyApp","version":"2.3.1+0123abc","build":{"peTimestamp":"0x9F3A12C4","pdbId":"0123456789ABCDEF0123456789ABCDEF1","pdbFile":"MyApp.pdb"},"git":{"basis":"workingTree","dirty":"notChecked","branch":"main","commit":"0123456789abcdef0123456789abcdef01234567"}}
{"seq":980,"t":41250000,"tSrc":"sys","type":"process.exit","proc":"p1","pid":4242,"process":"MyApp.exe","win":"w3","exitCode":3221225477,"exitCodeHex":"0xC0000005","class":"crash"}
{"seq":984,"t":41900000,"tSrc":"sys","type":"eventlog.entry","proc":"p1","process":"MyApp.exe","win":"w3","eventId":1000,"recordId":88231,"provider":"Application Error","facts":{"app":"MyApp.exe","appVersion":"2.3.1.0","module":"ntdll.dll","exceptionCode":"0xC0000005","offset":"0x00000000000a1b2c"}}
```

<a id="ev-log"></a>
### 9.21 `log.source`, `log.lines`

Mitgelesene Protokolldateien (Paket 13 aus [12](12-entwickler-fahrplan.md)); Quelle `LogWatch`. Nur Zeilen, die während der Aufnahme dazukommen; nur lokale Festplatten; höchstens 5 Dateien, 2 000 Zeichen je Zeile, 256 KB je Datei, 200 Zeilen je Ereignis.

| Typ | Felder | Bedeutung |
|---|---|---|
| `log.source` | `log` (`l<n>`), `file` (nur Dateiname), `startLength` | Eine mitgelesene Datei, beim Start |
| `log.lines` | `log`, `file`, `lines` (durch `\n` getrennt), `count`, `truncated`, `dropped` (übersprungene Zeilen: Grenzen, Pause, Privattaste), `rotated` | Neue Zeilen; `t` ist der Zeitpunkt der Beobachtung, nicht der Zeitstempel der Anwendung |

`lines` ist inhaltstragend: prüfpflichtig, in geschwärzten Zeiträumen geleert, nie im Suchindex.

**Leser-Mindestversion.** Eine Aufnahme mit inhaltstragender Quelle trägt im Manifest `requires: {"reader": "0.13.0", "reason": "contentEvents"}`. Ein Programm älterer Version, das diese Angabe kennt, öffnet die Sitzung nicht (`NotSupportedException`); Programme bis 0.12 kennen sie nicht – deshalb ist sie seit 0.13 eingebaut, und die Durchsicht der neuen Ereignisse gibt es erst ab dieser Version (Entscheidung O-16).

---

<a id="mausbewegungen"></a>
## 10. Mausbewegungs-Bündel

Jeder Bewegungsmesswert der Maus wird gespeichert – ungefiltert und ohne Vereinfachung. Damit das bei Mäusen mit 1000 Messwerten je Sekunde nicht ausufert, fasst `mouse.move` aufeinanderfolgende Messwerte zu einem Bündel aus Differenzen zusammen.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `t` | µs | ● | Zeit des ersten Messwerts. |
| `ep`, `m`, `x`, `y` | – | ● | Position des ersten Messwerts. |
| `n` | int ≥ 1 | ● | Anzahl der Messwerte im Bündel. |
| `d` | int[3][] | ◐ | `n − 1` Differenzen `[dt, dx, dy]` zum jeweils vorigen Messwert (`dt` in µs). Fehlt bei `n` = 1. |
| `tEnd`, `xEnd`, `yEnd` | – | ● | Zeit und Position des letzten Messwerts. Dienen als Prüfwerte und zum schnellen Überfliegen. |
| `held` | string[] | ○ | Während des ganzen Bündels gehaltene Maustasten (kennzeichnet Ziehbewegungen). |
| `inj`, `device` | – | ○ | Wie bei `mouse.down`; im ganzen Bündel gleich. |

**Auspacken:**

```
Messwert 0 = (t, x, y)
Messwert i = Messwert (i−1) + d[i−1]        für i = 1 … n−1
Es muss gelten: Messwert (n−1) = (tEnd, xEnd, yEnd)
```

**Ein Bündel endet**

1. vor jedem anderen Eingabeereignis (`mouse.down`, `mouse.up`, `mouse.wheel`, `key.*`, `text.masked`) sowie vor `marker`, `comment`, `session.*` und `gap` – kein Bündel überspannt eine Eingabe;
2. wenn der Zeiger auf einen anderen Monitor wechselt;
3. wenn sich `held`, `inj` oder `device` ändert;
4. nach 50 ms ohne Bewegung;
5. spätestens nach 250 ms oder 256 Messwerten;
6. bei jedem Wechsel der Topologie-Epoche.

Aus Regel 1 folgt: Der Mausweg vor einer Aktion besteht aus ganzen Bündeln, und die Position der Aktion schließt nahtlos an `xEnd`/`yEnd` des letzten Bündels an.

**Zurückgehaltene Tasten** sind von Regel 1 ausgenommen. Ein Bündel endet am **Beginn** einer zurückgehaltenen Folge (vor `text.masked`), aber nicht an den einzelnen Tasten, die nicht geschrieben werden. Die Bündelgrenzen sagen damit nichts darüber, wann eine maskierte Taste fiel.

**Eingabenummern im Bündel.** `inSeq` ist die Nummer des ersten Messwerts. Die übrigen Messwerte tragen die folgenden Nummern – lückenlos, außer zurückgehaltene Tasten fielen zwischen zwei Messwerte desselben Bündels: Dann sind die Nummern aufsteigend, aber nicht lückenlos. Die übersprungenen Nummern erklärt das `text.masked`, in dessen Zeitspanne das Bündel liegt (Prüfung F18); welcher Messwert welche Nummer trägt, ist dort nicht festgehalten und wird nicht gebraucht.

```json
{"seq":1732,"t":83185000,"inSeq":1206,"type":"mouse.move","ep":0,"m":"M1","x":1049,"y":703,"n":6,"d":[[1000,2,-1],[1000,2,0],[1000,1,-1],[1000,1,0],[1000,1,0]],"tEnd":83190000,"xEnd":1056,"yEnd":701}
```

Größenordnung: Bei 1000 Messwerten je Sekunde entstehen während der Bewegung etwa 11 KB je Sekunde; über eine einstündige Sitzung mit üblichem Bewegungsanteil sind das 10–15 MB.

---

<a id="bildspeicher"></a>
## 11. Bildspeicher

### 11.1 Drei Ebenen

| Ebene | Wo | Inhalt |
|---|---|---|
| Bildereignis | `frame` im Journal | Eine Beobachtung mit Kennung, Zeit, Auslöser und Ablage. |
| Ablage | `frame.store` | Wie das Bild dieser Beobachtung gewonnen wird: Schlüsselbild, Differenzbild, Verweis oder keines. |
| Blob | Datei in `frames/` | PNG-Daten, benannt nach ihrem Inhalt. |

Schritte, Bearbeitungen und Exporte verwenden ausschließlich **Bild-Kennungen** (`f000029`). Dateinamen in `frames/` sind ein Detail der Ablage.

### 11.2 Blobs (inhaltsadressiert)

- Pfad: `frames/<erste zwei Hex-Ziffern>/<SHA-256 der Dateibytes, 64 Hex-Ziffern, klein>.png`.
- Verweis im Journal: `"blob": "sha256:<64 Hex-Ziffern>"`.
- Ein Blob wird genau einmal geschrieben und danach nie geändert. Gleicher Inhalt wird nur einmal gespeichert – auch über Monitore und Zeitpunkte hinweg.
- Jeder Leser kann einen Blob prüfen, indem er die Datei hasht und mit dem Namen vergleicht.
- **Reihenfolge beim Schreiben:** erst der Blob (unter vorläufigem Namen, dann atomar umbenannt), danach das `frame`-Ereignis. Ein Journal verweist daher nie auf einen fehlenden Blob. Blobs ohne Verweis (nach einem Absturz dazwischen) sind harmlos und dürfen aufgeräumt werden.
- PNG-Parameter: 8 Bit je Kanal, kein Interlacing, sRGB. Schlüsselbilder ohne Alphakanal (RGB), Differenzbilder mit Alphakanal (RGBA). Schreiber kodieren deterministisch (feste Einstellungen, keine Zeit- oder Textblöcke), damit gleiche Pixel gleiche Bytes ergeben.

### 11.3 Ablagearten

| `store.kind` | Weitere Felder | Bedeutung |
|---|---|---|
| `key` | `blob`, `bytes` | Schlüsselbild: der Blob ist das vollständige Bild in `w` × `h`. |
| `delta` | `base`, `rect`, `blob`, `bytes` | Differenzbild gegenüber dem Bild `base`. Der Blob hat die Größe von `rect` (`x`, `y`, `w`, `h` in Bildpixeln). Pixel mit Alpha 0 sind **unverändert**, Pixel mit Alpha 255 sind **neu**. Andere Alphawerte kommen nicht vor. |
| `same` | `as` | Das Bild ist pixelgleich mit dem Bild `as`. Kein Blob. |
| `none` | `reason` | Kein Bild: `dropped`, `captureFailed`, `privacyMode`, `outOfScope`, `protected`. Bei `dropped` und `captureFailed` steht daneben ein `gap` mit `phase: "single"` und `detail.frame`. |

Regeln:

- `base` und `as` nennen ein **früheres** Bild desselben Monitors und derselben Epoche mit gleicher Größe.
- Die Zahl der Differenzschritte bis zum Schlüsselbild ist höchstens `capture.maxDeltaChain` (Standard 30). Verweise der Art `same` zählen nicht mit.
- Ein neues Schlüsselbild wird geschrieben: zu Beginn jedes Abschnitts, nach einer Pause, zu Beginn jeder Topologie-Epoche, nach einem Wechsel des Aufnahmewegs, wenn der geänderte Anteil `capture.keyframeChangeRatio` erreicht, und wenn die Kette sonst zu lang würde.
- Ob ein Vorher-Bild als `same` oder `delta` abgelegt wird, entscheidet ein **exakter** Pixelvergleich. Schwellwerte gelten nur dafür, *wann* ein Zwischen- oder Nachher-Bild ausgelöst wird, nie dafür, ob ein ausgelöstes Bild gespeichert wird.

### 11.4 Ein Bild wiederherstellen

```
bild(f):
  s = store von f
  key    →  Blob dekodieren
  same   →  bild(s.as)
  delta  →  b = bild(s.base)
            jedes Pixel des Blobs mit Alpha 255 an Position (rect.x + i, rect.y + j) in b eintragen
            Ergebnis: b
  none   →  kein Bild vorhanden
```

Ist `pixelHash` angegeben, muss das Ergebnis dazu passen. Wiederhergestellte Bilder dürfen in `cache/` abgelegt werden.

### 11.5 Gefüllte Flächen

Flächen, die nicht gespeichert werden dürfen, werden **vor** dem Kodieren schwarz (`#000000`) gefüllt; die betroffenen Pixel erreichen die Festplatte nie.

- Außerhalb des Aufnahmebereichs: alles, was nicht in `frame.scopeRects` liegt. Der Grund dieser Fläche ist stets `outOfScope`, ohne dass er eigens eingetragen wird.
- Innerhalb: die Flächen in `frame.masks` mit ihrem Grund (`excludedApp`, `passwordField`, `privacyMode`, `protectedContent`, `occluded`, `outOfScope`, nach endgültigem Schwärzen auch `redacted`, nach endgültig angewendeter Verallgemeinerung `substituted`).

| `why` | Bedeutung |
|---|---|
| `excludedApp` | Fenster einer Anwendung der Ausschlussliste. |
| `passwordField` | Erkanntes Passwortfeld. |
| `privacyMode` | Im Privatmodus zurückgehaltener Bildinhalt. |
| `protectedContent` | Vom System ausgeblendeter, geschützter Inhalt. |
| `occluded` | Modus „Eine Anwendung“: Stelle, an der ein fremdes Fenster die Anwendung verdeckt hat. |
| `outOfScope` | Fläche innerhalb von `scopeRects`, die dennoch nicht zum Aufnahmebereich gehört – etwa der Zwischenraum, wenn mehrere Fenster einer Anwendung als ein umschließendes Rechteck geführt werden, oder ein Fensterteil, der über den gewählten Bereich hinausragt. |
| `redacted` | Vom Autor endgültig geschwärzt. |
| `substituted` | Vom Autor verallgemeinert: Die Fläche zeigte einen Wert, den ein Platzhalter ersetzt; `label` nennt ihn ([13.14](#edits-substitutions)). |

Maßgeblich sind die Angaben im Ereignis, nicht die Farbe: Eine schwarze Fläche ohne Eintrag ist Bildinhalt, eine mit Eintrag ist eine Auslassung. In allen Ausgaben erscheint eine solche Fläche schwarz gefüllt; Viewer und Exporte dürfen sie zusätzlich beschriften. Ausgaben, die gefüllte Flächen mit ihrem Grund ausweisen ([KI-Export](06-ki-export-und-mcp.md)), nennen die Fläche außerhalb von `scopeRects` ebenfalls als `outOfScope`.

### 11.6 Mauszeiger

Bilder enthalten den Mauszeiger **nicht** (Ausnahme `cursor.baked`). Position und Form stehen in `frame.cursor`; die Zeigerbilder liegen einmal je Form in `cursors/cur<n>.png` und werden über [`mouse.cursor`](#ev-mouse) eingeführt. So lässt sich jedes Bild mit und ohne Zeiger ausgeben.

### 11.7 Größenordnungen

| Bildart | Full HD (1920 × 1080) | 4K (3840 × 2160) |
|---|---|---|
| Schlüsselbild einer üblichen Anwendungsoberfläche | 0,2–1 MB | 0,8–4 MB |
| Differenzbild nach einem Tastendruck | 0,2–3 KB | 0,3–5 KB |
| Differenzbild nach Dialog oder Menü | 10–150 KB | 30–500 KB |
| Verweis `same` | 0 | 0 |

Die Zahlen sind Richtwerte für die Planung; gemessen wird in Meilenstein M1 ([Roadmap](11-roadmap-tests-risiken.md)).

---

<a id="steps-json"></a>
## 12. `steps.json`

### 12.1 Rolle

`steps.json` enthält die aus dem Journal **abgeleiteten Schritte**. Die Datei ist

- **vollständig neu berechenbar** – sie enthält nichts, was nicht aus `events.jsonl` folgt;
- **sprachneutral** – sie enthält strukturierte Daten, keine ausformulierten Sätze. Texte entstehen beim Anzeigen und Exportieren aus den [Textvorlagen](05-schritterkennung.md#textvorlagen);
- **unabhängig von Bearbeitungen** – was der Nutzer ändert, steht in [`edits.json`](#edits-json).

Die Regeln, nach denen Schritte entstehen, stehen in der [Schritterkennung](05-schritterkennung.md). Hier ist nur der Aufbau der Datei festgelegt.

### 12.2 Oberste Ebene

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `format` | string | ● | Konstant `"steprec.steps"`. |
| `schemaVersion` | string | ● | Formatversion. |
| `sessionId` | string | ● | Zugehörige Sitzung. |
| `stepBuilderVersion` | string | ● | Regelversion (`MAJOR.MINOR.PATCH`). |
| `params` | Objekt | ● | Die tatsächlich angewendeten Schwellwerte, siehe [Parameter](05-schritterkennung.md#parameter). |
| `derivedAtUtc` | Zeit | ● | Zeitpunkt der Ableitung. |
| `source` | Objekt | ● | Stand des Journals: `lastSeq`, `journalSha256` ○. |
| `steps` | Step[] | ● | Schritte in zeitlicher Reihenfolge (`tFrom`, dann `anchorSeq`). |
| `timeline` | Objekt[] | ● | Lückenlose Zerlegung der Sitzungsdauer, siehe 12.9. |
| `gaps` | Objekt[] | ● | Alle Lücken in einer Liste, siehe 12.10. |
| `completeness` | Objekt | ● | Ausführlicher Vollständigkeitsbericht, siehe 12.11. |
| `warnings` | Objekt[] | ○ | Auffälligkeiten bei der Ableitung: `code`, `seq` ○, `message`. |

### 12.3 Schritt

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `id` | string | ● | `"s"` + `anchorSeq`. Bleibt über Neuberechnungen stabil, solange dasselbe Ereignis einen Schritt verankert. |
| `index` | int | ● | Position in zeitlicher Reihenfolge unter **allen** Schritten des Journals, ab 1 – auch ersetzter und solcher aus verworfenen Abschnitten. Die Nummer, die Ausgaben nennen, ist nicht `index`, sondern die Schrittnummer `number` aus der Sicht der Leseschicht ([12.13](#schrittsicht)). |
| `anchorSeq` | int | ● | `seq` des Ankerereignisses (z. B. das `mouse.down` eines Klicks, das erste `key.down` einer Texteingabe). |
| `kind` | enum | ● | Schrittart: `click`, `doubleClick`, `tripleClick`, `rightClick`, `drag`, `scroll`, `type`, `hotkey`, `key`, `hover`, `windowSwitch`, `system`, `wait`, `comment`, `snapshot`, `marker`, `gap`, `move`. |
| `action` | Objekt | ◐ | Aktion im neutralen [Aktionsvokabular](05-schritterkennung.md#aktionsvokabular). `null` bei `system`, `comment`, `snapshot`, `marker` und `gap` sowie bei einer Texteingabe, deren Text nicht eindeutig feststeht oder zurückgehalten wurde. Ein `wait`-Schritt trägt das Verb `wait`, obwohl er keine Nutzereingabe enthält. |
| `importance` | enum | ● | `primary`, `secondary`, `trace`. |
| `evidence` | Objekt | ● | Belegstufe, siehe 12.5. |
| `tFrom`, `tTo` | µs | ● | Erstes und letztes Eingabeereignis des Schritts (einschließlich Anlaufweg der Maus). |
| `seq` | Objekt | ● | Zugeordnete Ereignisse, siehe 12.4. |
| `ctx` | Objekt | ○ | Fensterkontext: `win`, `process`, `title`, `kind`, `surface`, `surfaceKind`, `elevated`, `ep`, `m`. |
| `target` | Objekt | ○ | Ziel der Aktion, siehe 12.6. |
| `endTarget` | Objekt | ○ | Nur `drag`: Ziel beim Loslassen, gleicher Aufbau. |
| `position` | Objekt | ○ | `ep`, `m`, `x`, `y`; dazu abgeleitet `inElement` (`[fx, fy]`, Anteil 0–1 im Zielrechteck) und `inWindow` (`[x, y]` relativ zum Fenster). |
| `endPosition` | Objekt | ○ | Nur `drag`. |
| `input` | Objekt | ○ | Artspezifische Eingabedaten, siehe 12.7. |
| `effect` | Objekt | ○ | Beobachtete Wirkung, siehe 12.8. |
| `frames` | Objekt | ● | Zugeordnete Bilder: `m`, `pre`, `post` (Bild-Kennung oder `null`), `between` (Liste), `issues` ○ (`preMissing`, `preNotGuaranteed`, `postMissing`, `postTimeout`, `masked`), `others` ○ (weitere Monitore: `m`, `pre`, `post`). |
| `approach` | Objekt | ○ | Mausweg vor der Aktion: `from` (`m`, `x`, `y`), `distancePx`, `directPx`, `durationUs`, `straightness` (0–1), `path` (vereinfachte Punktfolge `[x, y, dtMs]`), `dwells` (Zwischenhalte: `x`, `y`, `durationUs`, `el` ○), `restUs` (Ruhezeit auf dem Ziel unmittelbar vor der Aktion). |
| `clipboard` | Objekt | ○ | `op` (`copy` \| `cut` \| `paste`), `clip`. |
| `snapshots` | int[] | ○ | `seq` zugehöriger `ui.snapshot`-Ereignisse. |
| `comments` | int[] | ○ | `seq` zugehöriger `comment`-Ereignisse. |
| `gapId` | string | ◐ | Nur `gap`. |
| `flags` | Objekt | ○ | Boolesche Kennzeichen: `masked`, `opaque`, `injected`, `ime`, `discarded`, `incomplete`, `recovered`, `plaintext` (Eingabe im Klartext aus einem Fenster, in dem Passwortfelder nicht erkennbar sind – Grundlage für die [Durchsicht](#edits-review)). |
| `prov` | Objekt[] | ● | Herkunft aller inhaltstragenden Werte, siehe 12.12. |

### 12.4 `seq` – Buchführung

| Feld | Typ | Bedeutung |
|---|---|---|
| `input` | int[2][] | Geschlossene `seq`-Bereiche `[von, bis]` mit den Eingabeereignissen der Aktion selbst. |
| `approach` | int[2][] | Bereiche mit den `mouse.move`-Bündeln des Anlaufwegs. |
| `related` | int[] | Weitere herangezogene Ereignisse (Elemente, Fenster, Werte, Bilder). Nicht Teil der Buchführung. |

**Eingabeereignisse** im Sinn der Buchführung sind `mouse.move`, `mouse.down`, `mouse.up`, `mouse.wheel`, `key.down`, `key.up` und `text.masked` ohne `self: true`. Innerhalb eines Bereichs zählen nur diese Typen; andere Ereignisse, die zufällig dazwischen liegen, bleiben unberücksichtigt. Ein Bereich darf kein Eingabeereignis eines anderen Schritts enthalten – notfalls wird er geteilt. Jedes Eingabeereignis der Sitzung liegt in genau einem Bereich genau eines Schritts.

### 12.5 `evidence` – Belegstufe

| Feld | Typ | Bedeutung |
|---|---|---|
| `level` | enum | `confirmed` (belegt), `inputOnly` (nur Eingabe), `unknown` (unbekannt). |
| `input` | enum | `observed`, `masked`, `unobserved`. |
| `target` | enum | `agreed`, `corroborated`, `single`, `conflict`, `position`, `none`, `n/a`. |
| `result` | enum | `valueConfirmed`, `valueOnly`, `simulated`, `effectObserved`, `none`, `n/a`. |
| `cause` | enum | Für Schritte ohne Nutzereingabe: `none`, `unobserved`; sonst `n/a`. |
| `reasons` | string[] | Kurzcodes, die die Einstufung begründen (z. B. `elementAgreed`, `valueMatchesKeys`, `opaqueSurface`, `uiaTimeout`, `elementConflict`, `maskedByDesign`). |

Die genauen Kriterien stehen unter [Belegstufen](05-schritterkennung.md#belegstufen).

### 12.6 `target` – Ziel

| Feld | Typ | Bedeutung |
|---|---|---|
| `status` | enum | Wie `evidence.target`. |
| `el` | ElementDescriptor | Das gewählte Element. Fehlt bei `position` und `none`. |
| `path` | ElementRef[] | Vorfahren. |
| `source` | enum | `uia`, `msaa`, `win32`. |
| `from` | Objekt | `seq` der zugrunde liegenden Ereignisse: `dwell`, `down`, `focus`, `corroboration` (Liste). |
| `alternatives` | Objekt[] | Bei `conflict`: die abweichenden Kandidaten mit `el`, `fromSeq`. |
| `win32` | Win32Context | Win32-Kontext des auslösenden Ereignisses (Klick, Mausrad, Fokus des Eingabefelds), siehe [9.12](#ev-element). Auch bei `position` und `none` vorhanden. Bei Ziehen trägt `endTarget.win32` den Kontext am Endpunkt. |

### 12.6a Angaben für Automatisierung

Für KI-gestützte und automatische Skripterstellung (AutoHotkey) leitet die Schritterkennung je Aktionsschritt ab:

| Feld | Inhalt |
|---|---|
| `sync` | Wartesignale: `settleMs` und `settleReason` (Bildschirm ruhig nach der Eingabe), `busyMs`/`busyShape` (Mauszeiger beschäftigt), `waitWindow` (`title`, `afterMs`), `readyMs` (Anwendung bereit), `thinkMs` (menschliche Denkpause bis zur nächsten Eingabe – gehört nicht in ein Skript). |
| `verify` | Zustand danach aus `win32.state`: `title`, `class`, `exe`, `focus`, `titleChanged` (`from`, `to`), `changes` (je ClassNN: `from`/`to`, `checked`, `enabled`, `appeared`, `disappeared`; höchstens 12). |
| `selectors` | Wege zum Element, absteigend nach `score` (5 = sehr stabil): `classNN` (`stable`), `controlId`, `controlText` (`unique`), `uia` (`controlType`, `name`, `automationId`, `className`, `index`/`of`, `nameUnique`, `path`), `keyboard` (`accelerator`, `accessKey`), `label` (Beschriftung `rightOf`/`below`), `relative` (Anteil am Clientbereich), `window` (Fensterkoordinaten, `pixel`). |
| `menuPath` | `items` (Menüeinträge von der Menüleiste bis zum gewählten Befehl), `win32Menu` (klassisches Menü, `MenuSelect` möglich), `accelerator`. |
| `choose` | Auswahl in Liste, Baum, Register oder Auswahlfeld: `item`, `itemType`, `index`/`of`, `container`, `win32Control` (ClassNN für `ControlChooseString`). |
| `data` | Datenquellen: `pasteFrom` (Programm, aus dem die eingefügten Daten kopiert wurden), `fileDialog` (`folder`, `fileName`, `dialog`). |

Auf oberster Ebene von `steps.json`:

| Feld | Inhalt |
|---|---|
| `parameters` | Veränderliche Werte: `name` (Variablenname aus der Feldbeschriftung), `step`, `field`, `value` bzw. `secret: true` (maskiert, ohne Wert), `kind` (`fileName`), `source`/`from` (Zwischenablage), `generalized`. |
| `repetitions` | Unmittelbar wiederholte Schrittfolgen: `steps`, `length`, `repeats`, `varying` (`position`, `kind`, `values`). |

### 12.7 `input` – je Schrittart

| Schrittart | Felder |
|---|---|
| `click`, `doubleClick`, `tripleClick`, `rightClick` | `button`, `physButton`, `count`, `mods`, `pressUs` (Dauer des letzten Drückens), `longPress`, `device` |
| `drag` | `button`, `mods`, `path` (`[x, y, dtMs]`, vereinfacht; `dtMs` ab dem Drücken), `distancePx`, `durationUs` (gedrückt insgesamt), `holdBeforeMoveUs` (gedrückt gehalten vor der ersten Bewegung > 3 px), `holdAtEndUs` (Verweilen über dem Ziel vor dem Loslassen), `dragKind` (`dragDrop` \| `selectText` \| `moveWindow` \| `resizeWindow` \| `slider` \| `draw` \| `unknown`), `selectedText` ○, `withheld` ○ |
| `scroll` | `axis`, `notches` (Zahl, mit Vorzeichen), `events`, `deltaSum`, `lines` ○, `mods`, `semantic` (`scroll` \| `zoom`), `before` ○, `after` ○ (`hPct`, `vPct`) |
| `type` | `tokens`, `text` ○, `masked`, `count` ○ (Tastenanschläge; fehlt bei einer maskierten Eingabe, deren Anzahl nicht gespeichert wurde), `maskReason` ○, `rule` ○, `promptLine` ○ (bei maskierten Eingaben: Grund, Regel und Abfragezeile aus `text.masked`), `valueBefore` ○, `valueAfter` ○, `valueWithheld` ○, `terminator` ○ (`key`, `mods`), `inputMethod` (`keyboard` \| `ime` \| `autoComplete` \| `mixed`), `resultSource` (`value` \| `simulated` \| `none`), `durationUs`, `pauses` ○ |
| `hotkey` | `combo` (kanonisch, z. B. `"Ctrl+S"`), `keys` (Tastennamen), `count`, `commonMeaning` ○ (bloßer Hinweis, z. B. `save`) |
| `key` | `key`, `mods` ○, `count`, `held`, `holdUs` ○ |
| `hover` | `dwellUs` |
| `windowSwitch` | `via` (`altTab` \| `winTab` \| `taskbar` \| `hotkey` \| `click` \| `unknown`), `count` ○, `from`, `to` (Fensterkennungen) |
| `system` | `event` (`windowOpened` \| `windowClosed` \| `foregroundChanged` \| `titleChanged` \| `notification` \| `topologyChanged` \| `scopeChanged` \| `valueChanged`), `refSeq` |
| `wait` | `durationUs`, `busyCursor`, `until` ○ (`seq`, `what`) |
| `comment`, `snapshot`, `marker` | `refSeq` (das Ereignis), bei `marker` zusätzlich `markerKind` (`note` \| `error` \| `expected` \| `important`, aus `marker.kind`) und `label` ○ |
| `gap` | `cause`, `channels`, `inputLost`; bei `cause: "secureDesktop"` zusätzlich `desktopKind` (`uac` \| `other`, aus `detail.kind` des `end`-Ereignisses). Lücken wegen sicherem Desktop, gesperrtem Rechner, getrennter Sitzung und Ruhezustand haben weder Vorher- noch Nachher-Bild; ein Nachher-Bild wird nie aus einer solchen Lücke gebunden. Bei `uac` zählt der Schritt als Hauptschritt (Titel „Benutzerkontensteuerung bestätigen“). |
| `move` | wie `approach` |

`tokens` einer Texteingabe ist eine Liste in Tippreihenfolge:

| Form | Bedeutung | Beispiel |
|---|---|---|
| Zeichenkette | Getippte Zeichen | `"notiz.txr"` |
| `{ "key": …, "mods": …, "n": … }` | Taste ohne Zeichen innerhalb der Eingabe, `n`-mal | `{ "key": "Backspace" }` |
| `{ "masked": n }` | `n` zurückgehaltene Tastenanschläge (Zählweise wie `text.masked.count`, [9.9](#ev-text-masked)). `n` ist `null`, wenn die Anzahl nicht gespeichert wurde. | `{ "masked": 12 }`, `{ "masked": null }` |
| `{ "uncertain": "…" }` | Zeichen laut Tastenfolge, aber unzuverlässig (IME aktiv, Einrastfunktion) | `{ "uncertain": "nihao" }` |
| `{ "placeholder": "…" }` | An dieser Stelle stand ein Wert, den eine [Verallgemeinerung](#edits-substitutions) durch einen Platzhalter ersetzt. In `steps.json` selbst nur nach endgültigem Anwenden; in der Sicht der Leseschicht für jede geltende Regel. | `{ "placeholder": "Benutzername" }` |

### 12.8 `effect` – Wirkung

| Feld | Typ | Bedeutung |
|---|---|---|
| `observed` | bool | Mindestens eine Wirkung wurde beobachtet. |
| `causality` | enum | `direct` (im Wirkfenster, keine konkurrierende Eingabe), `plausible`, `none`. |
| `latencyUs` | µs | Zeit von der Eingabe bis zur ersten Wirkung. |
| `windowsOpened`, `windowsClosed` | Objekt[] | `win`, `title`, `kind`, `snapshot` ○ (`seq`). |
| `foreground` | Objekt | `from`, `to`. |
| `titleChanges` | Objekt[] | `win`, `from`, `to`. |
| `focus` | Objekt | `seq`, `el` (ElementRef), `ctx`. |
| `values` | Objekt[] | `el`, `before`, `after`, `withheld` ○. |
| `toggles`, `selections`, `expansions` | Objekt[] | `el`, `state` bzw. `items`. |
| `invoked` | ElementRef[] | Ausgelöste Elemente. |
| `scroll` | Objekt | `el`, `before`, `after`. |
| `notifications` | string[] | Mitteilungstexte. |
| `clipboard` | Objekt | `clip`, `kind`, `size` ○ (nur, wenn das `clipboard`-Ereignis sie nennt). |
| `screen` | Objekt | `changedRatio`, `settled`, `settleUs`, `settleReason`. |
| `cursor` | string[] | Zeigerformen im Wirkfenster (z. B. `["wait", "arrow"]`). |

Jede einzelne Wirkung darf zusätzlich `afterUs` tragen: die Zeit von der Eingabe bis zu dieser Wirkung.

### 12.9 `timeline`

Liste von Abschnitten, die die Sitzungsdauer **lückenlos und überschneidungsfrei** aufteilen:

| Feld | Typ | Bedeutung |
|---|---|---|
| `kind` | enum | `steps` (mindestens ein Schritt ist aktiv), `idle` (Leerlauf), `gap` (Lücke; hat Vorrang vor den anderen). |
| `tFrom`, `tTo` | µs | Grenzen. |
| `steps` | string[] | Bei `steps`: Kennungen der aktiven Schritte. |
| `gapId` | string | Bei `gap`. |

Es gilt: Summe aller Abschnitte = `durationUs` der Sitzung.

### 12.10 `gaps`

Eine einheitliche Liste aller Lücken – aus `gap`-Ereignissen und aus Ableitung:

| Feld | Typ | Bedeutung |
|---|---|---|
| `cause` | enum | Ursachen aus [`gap`](#ev-gap), zusätzlich `maskedInput` (aus `text.masked`), `unobserved` (Schritt mit Belegstufe `unknown`) und `removedByAuthor` (endgültig geschwärzter Zeitraum, siehe [Schwärzungen](#edits-redactions); für einen nur im Overlay geschwärzten Zeitraum setzt die Leseschicht denselben Eintrag beim Lesen ein). |
| `gapId` | string | Bei Lücken aus dem Journal. |
| `tFrom`, `tTo` | µs | Dauer; `tTo` darf bei offener Lücke `null` sein. |
| `channels`, `inputLost` | – | Wie im Ereignis. |
| `seq` | int[] | Zugehörige Ereignisse. |
| `step` | string | Schritt, der die Lücke darstellt. |

<a id="steps-completeness"></a>
### 12.11 `completeness`

```json
{
  "stepBuilderVersion": "1.0.0",
  "bookkeeping": {
    "inputEvents": 81, "assigned": 81, "orphans": 0, "orphanSeqRanges": [], "duplicates": 0, "selfInputEvents": 3,
    "timeline": { "durationUs": 86000000, "stepsUs": 7941000, "idleUs": 78059000, "gapsUs": 0, "unaccountedUs": 0 },
    "ok": true
  },
  "evidence": {
    "actionSteps": 7, "confirmed": 7, "inputOnly": 0, "unknown": 0, "confirmedRatio": 1.0,
    "byReason": {}
  },
  "gaps": { "count": 0, "totalUs": 0, "byCause": {} },
  "frames": { "requested": 31, "stored": 24, "same": 7, "dropped": 0, "preGuaranteed": 7, "preNotGuaranteed": 0 },
  "journal": { "schemaOk": true, "seqContinuous": true, "refsOk": true, "blobsOk": true, "chainOk": true, "issues": [] }
}
```

Der Bericht trennt bewusst zwei Aussagen: `bookkeeping` sagt, ob **alles Aufgezeichnete verbucht** ist; `evidence` und `gaps` sagen, **wie gut belegt** die Schritte sind und was nicht aufgezeichnet werden konnte. Ein Bericht mit `bookkeeping.ok: true` behauptet nie, dass nichts fehlt. Wie die Werte berechnet werden, steht unter [Vollständigkeitsprüfung](05-schritterkennung.md#vollstaendigkeitspruefung).

### 12.12 `prov` – Herkunft der Inhalte

Ein Schritt wiederholt Inhalte aus dem Journal (Namen, Werte, getippten Text, Fenstertitel). Damit Schwärzungen zuverlässig greifen, nennt jeder Schritt für jeden solchen Wert die Quelle:

| Feld | Typ | Bedeutung |
|---|---|---|
| `path` | string | Pfad des Werts im Schritt, z. B. `input.valueAfter`, `target.el`, `effect.titleChanges[0].to`. |
| `seq` | int | Quellereignis. Für Werte aus vielen Ereignissen stattdessen `seqRanges` (z. B. die Tasten einer Texteingabe). |
| `field` | string | Pfad des Quellfelds im Ereignis, z. B. `value`, `el`, `title`, `ch`. |
| `copy` | bool | `true`, wenn der Wert unverändert übernommen wurde (dann lassen sich Schwärzungen von Teilbereichen übertragen). |

Regel für die Leseschicht: Ist ein Quellfeld ganz oder teilweise geschwärzt, wird der abgeleitete Wert bei `copy: true` an denselben Stellen geschwärzt und andernfalls vollständig ersetzt. Ein inhaltstragender Wert ohne `prov`-Eintrag ist ein Formatfehler.

<a id="schrittsicht"></a>
### 12.13 Schritte in der Sicht der Leseschicht

Die `steps.json` der Sitzung kennt keine Bearbeitungen. Wer Schritte **ausgibt** – KI-Paket, MCP-Server, Vergleich –, erhält sie über die Leseschicht. Sie wendet Schwärzungen, Durchsicht und Verallgemeinerungen an und ergänzt an jedem Schritt zwei Felder, die es in der Datei der Sitzung nicht gibt:

| Feld | Typ | Bedeutung |
|---|---|---|
| `state` | enum | Zustand in der [wirksamen Schrittfolge](05-schritterkennung.md#wirksame-schrittfolge): `active` (wirksam) \| `replaced` (durch eine Neuaufnahme ersetzt) \| `unused` (gehört zu einer nicht verwendeten Neuaufnahme). |
| `number` | int | **Schrittnummer:** Position in der wirksamen Schrittfolge, fortlaufend ab 1, gezählt über **alle** wirksamen Schritte – auch Neben- und Spurschritte, ausgeblendete und einzeln verworfene. Nur bei `state: "active"`; ersetzte und nicht verwendete Schritte tragen keine. |

Regeln:

- `number` hängt nicht von Detailstufe oder Filter ab: Derselbe Schritt hat in jeder Ausgabe dieselbe Nummer. Lässt eine Ausgabe Schritte weg, fehlen deren Nummern dort; sie werden nicht neu vergeben. Die Nummerierung einer Anleitung ist davon unabhängig – sie zählt nur, was die Anleitung zeigt ([Anleitungs-Export](09-anleitungs-export.md)).
- `number` zählt die Schritte aus `steps.json`. Zusammenfassungen, Teilungen, eine eigene Reihenfolge und eingefügte Hinweise der Anleitung ändern sie nicht.
- `index` und `id` bleiben, wie sie in `steps.json` stehen. Solange nichts ersetzt und kein Abschnitt verworfen ist, sind `number` und `index` gleich. Stabil über Bearbeitungen hinweg ist allein `id`; `number` ändert sich, sobald eine Ersetzung angelegt oder entfernt oder ein Abschnitt verworfen wird.
- `state` sagt nichts über `flags.discarded`: Ein **einzeln** verworfener Schritt („Letzten Schritt verwerfen“) ist geschehen, bleibt `active` und behält seine Nummer.
- Schritte eines **verworfenen Abschnitts** liefert die Leseschicht gar nicht ([13.16](#edits-sections)); sie haben in keiner Ausgabe einen Zustand. Nur die Verwaltungsansicht des Viewers kennt für sie den Zustand `sectionDiscarded`.
- Beide Felder stehen in der `steps.json` des KI-Pakets und in den Antworten des MCP-Servers ([KI-Export](06-ki-export-und-mcp.md)) – **nie** in der `steps.json` der Sitzung. Ein Leser, der sie dort fände, ignoriert sie.

```json
[
  { "id": "s1502", "index": 5, "state": "replaced" },
  { "id": "s4433", "index": 12, "state": "active", "number": 5 },
  { "id": "s1657", "index": 8, "state": "active", "number": 7 }
]
```

(Gekürzt auf die drei Felder und die Kennung: Schritt `s1502` ist durch eine Neuaufnahme ersetzt; deren erster Schritt `s4433` steht an seiner Stelle und trägt die Nummer 5, obwohl er zeitlich später liegt und in der Sitzung den `index` 12 hat.)

---

<a id="edits-json"></a>
## 13. `edits.json`

### 13.1 Rolle und Verankerung

`edits.json` enthält alles, was Menschen (im Viewer) oder Agenten (über den MCP-Server) an einer Sitzung ändern. Die Datei verändert weder `events.jsonl` noch `steps.json`.

Alle Einträge sind an **Rohdaten** verankert, nie an Schrittnummern:

| Anker | Bedeutung |
|---|---|
| `anchor` (int) | `seq` eines Ereignisses. Gemeint ist der Schritt, dessen `anchorSeq` gleich ist – oder, falls es nach einer Neuberechnung keinen solchen gibt, der Schritt, dessen `seq.input` oder `seq.approach` dieses Ereignis enthält. |
| Bild-Kennung | `frame`-Kennung (`f000029`). Koordinaten sind Bildpixel und damit monitorrelativ. |
| `seq` + Feldpfad | Für Schwärzungen von Textstellen. |
| `seq`-Bereiche | Für Schwärzungen eines Zeitraums oder ganzen Schritts und für den Stand der Durchsicht. |

Lässt sich ein Eintrag nach einer Neuberechnung keinem Schritt mehr zuordnen, wird er nach `orphans` verschoben und im Viewer angezeigt – nie stillschweigend verworfen.

### 13.2 Oberste Ebene

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `format` | string | ● | Konstant `"steprec.edits"`. |
| `schemaVersion` | string | ● | Formatversion. |
| `sessionId` | string | ● | Zugehörige Sitzung. |
| `revision` | int | ● | Zähler, bei jedem Speichern um 1 erhöht. |
| `savedAtUtc`, `savedBy` | Zeit, string | ● | Letzter Schreiber (`"viewer"`, `"mcp:set_step_text"`, `"recorder"`). |
| `guide` | Objekt | ○ | Angaben zur Anleitung: `title` (zugleich der Titel der Aufnahme in Bibliothek, Startfenster und Ausgaben; „Umbenennen“ in der Bibliothek schreibt hierher und ändert den Ordnernamen nicht), `subtitle`, `description`, `author`, `tags` (Schlagwörter), `intro`, `outro`, `language`. `auto` (Voreinstellung der automatischen Markierungen für alle Schritte, Schlüssel wie `steps[].auto`; dazu `auto.style`, der Markenstil, siehe §13.19). `intro` ist die Einleitung der Anleitung – und beim Support-Paket der **Hinweis für den Empfänger** (siehe unter der Tabelle). |
| `goal` | Objekt | ○ | Vom Autor geänderte Ziel-Angaben; überlagern `manifest.goal`, siehe [13.12](#edits-goal). |
| `view` | Objekt | ○ | Darstellungsvorgabe für alle Schritte, siehe 13.4. |
| `steps` | Objekt[] | ○ | Änderungen je Schritt, siehe 13.3. |
| `order` | (int \| string)[] | ○ | Reihenfolge in der Anleitung: Anker und Kennungen eingefügter Schritte. Fehlt = wirksame Schrittfolge. Das KI-Protokoll folgt dieser Liste nie; es zeigt stets die wirksame Schrittfolge ([Schritterkennung](05-schritterkennung.md#wirksame-schrittfolge)). |
| `chapters` | Objekt[] | ○ | `id`, `title`, `startAt` (Anker oder Kennung, mit dem das Kapitel beginnt). |
| `merges` | Objekt[] | ○ | Zusammengefasste Schritte (nur Anleitungen): `id`, `anchors`, `text` ○, `createdUtc`, `by`. Siehe [13.17](#edits-merges). |
| `splits` | Objekt[] | ○ | `anchor`, `atSeq`: Schritt wird vor dem Eingabeereignis `atSeq` geteilt. |
| `inserted` | Objekt[] | ○ | Eingefügte Schritte: `id`, `kind` (`note` \| `warning` \| `tip` \| `step`), `after` (Anker, Kennung oder `"start"`), `text`, `frame` ○, `view` ○. |
| `comments` | Objekt[] | ○ | Kommentare zum Schritt: `id`, `anchor`, `kind` (`note` \| `tip` \| `warning` \| `internal`), `text`, `createdUtc`, `by` (`user` \| `mcp`), `origin` (`viewer`, Standard \| `recording`), `hidden` ○ (bool: vom Autor ausgeblendet). Bei `origin: "recording"` zusätzlich `sourceSeq` (das `comment`-Ereignis im Journal): Ein solcher Eintrag ändert Text oder Art eines **während der Aufnahme** geschriebenen Kommentars oder blendet ihn aus, ohne das Journal zu berühren. **Interne und ausgeblendete Kommentare** – `kind: "internal"` oder `hidden: true` – erscheinen in **keiner** Ausgabe: weder in der Anleitung noch im KI-Paket noch über den MCP-Server, auch nicht in Rohereignissen und Suchtreffern. Bei einem Kommentar aus der Aufnahme bleiben das `comment`-Ereignis und sein Schritt mit Nummer und Zeit stehen, damit die Zählung stimmt; sein Text aber wird von der Leseschicht wie eine geschwärzte Textstelle ersetzt (`[redacted]`, [13.6](#edits-redactions)). Sichtbar bleiben solche Kommentare allein im Viewer. Das unterscheidet sie von einem ausgeblendeten **Schritt** (13.3), der nur in der Anleitung fehlt. |
| `pins` | Objekt[] | ○ | Kommentar-Pins im Bild: `id`, `frame`, `x`, `y`, `text`, `anchor` ○. |
| `annotations` | Objekt[] | ○ | Zeichnungen im Bild, siehe 13.5. |
| `redactions` | Objekt[] | ○ | Schwärzungen, siehe 13.6. |
| `substitutions` | Objekt[] | ○ | Verallgemeinerungen „Wert → Platzhalter“, siehe [13.14](#edits-substitutions). |
| `replacements` | Objekt[] | ○ | Ersetzungen: Eine Neuaufnahme tritt an die Stelle früherer Schritte, siehe [13.13](#edits-replacements). |
| `sections` | Objekt[] | ○ | Entscheidungen über ganze Aufnahmeabschnitte – derzeit: verworfen, siehe [13.16](#edits-sections). |
| `assets` | Objekt[] | ○ | Vom Nutzer eingebrachte Bilder (Bildersatz), siehe 13.9. |
| `review` | Objekt | ○ | Stand der Durchsicht der Eingaben, siehe [13.10](#edits-review). |
| `qualityIgnores` | Objekt[] | ○ | Fundstellen der Qualitätsprüfung, die der Autor ausdrücklich ignoriert, siehe [13.15](#edits-quality). |
| `exports` | Objekt | ○ | Zuletzt genutzte Export-Einstellungen; den Aufbau legt der [Anleitungs-Export](09-anleitungs-export.md) fest. |
| `sharing` | Objekt | ○ | Freigaben der Sitzung. Derzeit ein Feld: `mcp` (bool, Standard `true`). Bei `false` stellt der MCP-Server die Sitzung nicht bereit; sie erscheint nicht in `list_sessions` (*Kann*, [KI-Export](06-ki-export-und-mcp.md)). |
| `orphans` | Objekt[] | ○ | Einträge ohne zuordenbaren Schritt, jeweils mit ihrem ursprünglichen Abschnittsnamen. |

**Empfängerhinweis des Support-Pakets.** Der Text, der dem Empfänger eines Support-Pakets vorangestellt wird, steht in `guide.intro` – ein eigenes Feld gibt es dafür nicht. Ist er über Profil oder Kommandozeile vorbelegt, trägt ihn die Aufzeichnungssteuerung nach dem sauberen Stopp ein ([13.7](#edits-writers)); der Autor kann ihn im Viewer ändern. Wie jeder Text der Anleitung ist er Autorentext: Schwärzungen und Verallgemeinerungen gelten, und die Musterprüfung vor der Weitergabe erfasst ihn.

<a id="edits-steps"></a>
### 13.3 Änderungen je Schritt (`steps[]`)

| Feld | Typ | Bedeutung |
|---|---|---|
| `anchor` | int | Anker (Pflicht). |
| `text` | Objekt | Vom Autor oder einem Agenten gesetzte Texte je Sprache: `{ "de": { "title": "…", "body": "…", "by": "user", "atUtc": "…" } }`. `title` ist der Kurztitel, `body` der Text der Anweisung (im MCP-Tool `set_step_text` heißt er `description`). Fehlt eines der beiden oder eine ganze Sprache, gilt dort die Vorlage. |
| `text.<lang>.by` | enum | Herkunft des Textes: `user` (Autor im Viewer) \| `mcp` (Agent über den MCP-Server). Pflicht. |
| `text.<lang>.client` | string | Bei `mcp`: Name des Clients aus den Anfrage-Metadaten. |
| `text.<lang>.atUtc` | Zeit | Zeitpunkt des Schreibens. |
| `text.<lang>.history` | Objekt[] | Frühere Fassungen in derselben Form (`title`, `body`, `by`, `client`, `atUtc`), jüngste zuerst, höchstens 10. Damit lässt sich ein Agententext zurücknehmen. |
| `hidden` | bool | In der Anleitung ausblenden. Im KI-Paket und über den MCP-Server bleibt der Schritt sichtbar – er ist geschehen. |
| `discarded` | bool | Überschreibt das Kennzeichen „verworfen“ des Schritts: `false` nimmt ein Verwerfen aus der Aufnahme zurück, `true` verwirft nachträglich. Fehlt das Feld, gilt `flags.discarded` aus `steps.json`. |
| `importance` | enum | Wichtigkeit überschreiben. |
| `bookmark` | bool | Lesezeichen des Autors (Stern im Editor, Filter „nur Lesezeichen“). Reine Arbeitsnotiz: erscheint in keiner Ausgabe; die bereinigte Kopie entfernt das Feld. Fehlt es, ist kein Lesezeichen gesetzt. |
| `markerKind` | enum | Nur für `marker`-Schritte: Typ des Markers überschreiben (`note` \| `error` \| `expected` \| `important`). Fehlt das Feld, gilt `input.markerKind` aus `steps.json`. |
| `view` | Objekt | Darstellung nur für diesen Schritt, siehe 13.4. |
| `frameChoice` | Objekt | Anderes Bild wählen: `pre`, `post` (Bild-Kennungen). |
| `frameReplacement` | Objekt | Extern bearbeitetes Bild verwenden: `pre`, `post` (Kennungen aus `assets`). |
| `auto` | Objekt | Automatische Markierungen ein/aus, überschreibt `guide.auto`: `clickMarker` (Klickpunkt), `dragPath` (Zug-/Haltepfad mit Start- und Endmarke), `elementFrame` (Zielrahmen), `labels` (Hinweise wie Haltedauer, Taste, Modifikatoren), `approachPath` (Mausweg davor, Standard aus), `keyCaps` (Tasten und getippter Text), `stepNumber` (Nummer des Schritts wie in der Anleitung als Marke an der Stelle der Aktion, Standard aus; [13](13-handbuch-fahrplan.md) Paket 22). Gilt für Viewer, Kopieren und Anleitungen; KI-Paket und MCP zeigen stets die festen Markierungen als Beleg – ohne Schrittnummer und ohne Markenstil. |

### 13.4 Darstellungswahl (`view`)

| Feld | Typ | Bedeutung |
|---|---|---|
| `mode` | enum | `screen` (ganzer Bildschirm), `window` (nur das Anwendungsfenster), `focus` (Ausschnitt um die Aktion), `custom`. |
| `rect` | int[4] | Bei `custom`: Ausschnitt `[x, y, w, h]` in Bildpixeln. |
| `showPost` | enum | Nachher-Bild: `changed` (nur wenn es sich unterscheidet), `always`, `never`. |
| `showCursor` | bool | Mauszeiger einzeichnen. |
| `importance` | enum | Nur in der Vorgabe: gezeigte Stufen `primary` \| `secondary` \| `all`. |
| `style` | string | Name einer Stilvorlage für Markierungen. |

Die Wahl wirkt nur auf Anzeige und Export. Die gespeicherten Bilder bleiben vollständig.

### 13.5 Annotationen

| Feld | Typ | Bedeutung |
|---|---|---|
| `id` | string | Eindeutig in der Datei. |
| `frame` | string | Bild, auf dem gezeichnet wird (Pflicht). |
| `anchor` | int | Zugehöriger Schritt, falls vorhanden. |
| `type` | enum | `arrow`, `line`, `rect`, `ellipse`, `highlight`, `text`, `badge`, `freehand`, `magnifier`, `spotlight`. |
| `geom` | – | Je Typ: `arrow`/`line` `[x1, y1, x2, y2]`; `rect`/`ellipse`/`highlight`/`spotlight` `[x, y, w, h]`; `text`/`badge` `[x, y]`; `freehand` Punktliste `[[x, y], …]` (der Editor glättet beim Loslassen; höchstens 400 Punkte); `magnifier` `{ "src": [x, y, w, h], "at": [x, y], "zoom": 2 }`. Rechtecke dürfen mit negativer Breite oder Höhe gespeichert sein; Leser normalisieren. |
| `text` | string | Bei `text` und `badge`. Bei automatisch zählenden Nummern schreibt der Editor die aktuelle Zahl hinein, damit ältere Versionen und das KI-Paket sie zeigen. |
| `auto`, `start` | bool, int | Nur `badge` ([13](13-handbuch-fahrplan.md) Paket 22): `auto: true` = Nummer zählt automatisch (je Bild in der Reihenfolge des Anlegens, neu nach Einfügen und Löschen); `start` = ab dieser Nummer neu zählen. Ohne `auto` ist die Beschriftung fest. |
| `style` | Objekt | `color`, `width`, `fill`, `fontSize`, `opacity`; ab Paket 22 optional `dash` (`solid` \| `dash` \| `dot`), `heads` bei `arrow`/`line` (`end` \| `both` \| `start` \| `none`), `fill` als Farbe mit `fillOpacity` (0,05–1, Standard 0,25) bei `rect`/`ellipse`, `hue` bei `highlight` (Farbton; fehlt er, bleibt der Markierstift gelb, auch wenn `color` gesetzt ist), `bg` bei `text` (`white` Standard \| `none` \| `fill` = farbiger Grund mit weißer Schrift \| Farbe), `shape` bei `badge` (`circle` Standard \| `square` \| `rounded` \| `pin` \| `ring`), `seq` bei `badge` (`num` \| `alpha` \| `ALPHA` \| `roman`). Fehlt ein Schlüssel, bleibt das bisherige Aussehen; bei `spotlight` zusätzlich `dim` (Stärke der Abdunklung, 0–1, Standard 0,55); bei `magnifier` zusätzlich `shape` (`round`: Rechteck mit abgerundeten Ecken, Standard des Editors; `circle`: kreisförmige Linse; unbekannte Werte wie `round`). |

Eigenheiten der neueren Typen:

- **`line`** ist ein Pfeil ohne Spitze; Geometrie und Treffer wie `arrow`.
- **`freehand`** ist ein Linienzug. Die Punkte stehen in Bildpixeln; der Umriss ist der kleinste Rahmen um alle Punkte.
- **`spotlight`**: Der Bereich bleibt hell, alles übrige wird abgedunkelt. Mehrere Spotlights auf demselben Bild wirken zusammen: Abgedunkelt wird nur, was außerhalb aller Spotlight-Rechtecke liegt, und zwar einmal, mit der stärksten der angegebenen Abdunklungen (`dim` wird auf 0,1 bis 0,9 begrenzt). Der Editor wählt die Stärke über die Strichstärke (dünn 0,4 · mittel 0,55 · dick 0,7). Beim Anklicken wird ein Spotlight zuletzt geprüft, weil es große Flächen deckt.
- **`magnifier`**: `src` ist der vergrößerte Bereich, `at` der Mittelpunkt der Linse; die Linse ist `src` mal `zoom` groß (`zoom` wird auf 1,25 bis 4 begrenzt, Standard 2). Linse und Quelle lassen sich getrennt verschieben, ein Griff an der Linse ändert den Zoom. Ist `src` kleiner als 2 × 2 Pixel, wird die Lupe ignoriert.
- **Zeichenreihenfolge im Bild:** erst die Abdunklung der Spotlights, dann die Lupen, dann alle übrigen Markierungen in der Reihenfolge der Liste. Eine Lupe wird nur gezeichnet, wenn das vollständige (bereits geschwärzte) Bild als Quelle vorliegt.
- **Nur auf dem eigenen Bild.** Eine Markierung steht auf dem Bild `frame` und auf keinem anderen, auch nicht auf dem Nachher-Bild desselben Schritts. Treffer, Griffe, Größenänderung und Verschieben berechnet eine gemeinsame Funktion (`AnnotationGeometry`), die Editor, Export und KI-Paket gleich benutzen. Das KI-Paket beschreibt jede Markierung als Text, z. B. „Spotlight auf (x, y, w×h) – übriges Bild abgedunkelt“ ([06](06-ki-export-und-mcp.md)).
| `z` | int | Zeichenreihenfolge. |

Alle Koordinaten sind Bildpixel des vollständigen Bildes – unabhängig davon, wie das Bild gerade zugeschnitten oder skaliert angezeigt wird.

<a id="edits-redactions"></a>
### 13.6 Schwärzungen

Schwärzungen sind keine Zeichnungen, sondern Zugriffsregeln: Jede Stelle, die Daten einer Sitzung nach außen gibt – Viewer-Export, Kopieren, KI-Paket, MCP-Server, Suche – liest über die gemeinsame Leseschicht ([Architektur](02-architektur.md)) und **muss** sie anwenden.

Es gibt drei Arten:

| `kind` | Trifft | Anker |
|---|---|---|
| `image` | Bildpunkte in einem Rechteck | Bild-Kennungen + Rechteck |
| `text` | eine Textstelle in einem Ereignisfeld | `seq` + Feldpfad + Zeichenbereich |
| `range` | alles in einem Zeitraum – Eingaben, Texte, Bilder –, auch ein ganzer Schritt | `seq`-Bereiche |

„Ganzer Schritt“ ist keine eigene Art: Beim Anlegen wird der Schritt auf seine `seq`-Bereiche aufgelöst und als `range` gespeichert. So bleibt die Schwärzung gültig, wenn die Schritte später neu berechnet und anders geschnitten werden.

Gemeinsame Felder aller Arten: `id`, `kind`, `createdUtc`, `by`, dazu nach endgültigem Schwärzen `applied: true` und `appliedUtc`.

**Bild-Schwärzung (`image`)**

| Feld | Typ | Bedeutung |
|---|---|---|
| `areas` | Objekt[] | Betroffene Bilder mit Fläche. Je Eintrag: `m`, `from`, `to` (Bild-Kennungen; ein einzelnes Bild hat `from` = `to`) und `rect` (`[x, y, w, h]` in Bildpixeln). Gemeint sind alle `frame`-Ereignisse dieses Monitors mit Kennungen von `from` bis `to`. Mehrere Einträge mit unterschiedlichem `rect` bilden eine wandernde Fläche ab. |
| `style` | enum | `black` (Standard), `pixelate`, `blur`. Die beiden letzten sind rein optische Varianten und werden **nicht** aus den Originalpixeln berechnet ([Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md)). |
| `rule` | Objekt | Wie `areas` entstand: `kind` (`explicit` = einzeln gewählt \| `unchanged` = alle aufeinanderfolgenden Bilder, in denen die Fläche unverändert ist \| `window` = folgt dem Fenster \| `element` = folgt dem Bedienelement \| `monitor` = diese Bildschirmstelle in allen Bildern des Monitors \| `recurrence` = pixelgleiches Wiederauftreten), dazu `win` bzw. `el` (ElementRef) und `origin` (`frame`, `rect`: die ursprünglich markierte Stelle). Die Angabe dient der Nachvollziehbarkeit und erlaubt dem Viewer, die Ausdehnung neu zu berechnen – maßgeblich ist stets `areas`. |

Die Fläche wird auf dem **wiederhergestellten Vollbild** unkenntlich gemacht, bevor zugeschnitten, skaliert oder beschriftet wird. Ein Bild, das in keinem Eintrag von `areas` liegt, bleibt unberührt – auch wenn es denselben Inhalt zeigt. Deshalb löst der Viewer jede Ausdehnungsregel beim Anlegen in `areas` auf.

**Text-Schwärzung (`text`)**

| Feld | Typ | Bedeutung |
|---|---|---|
| `targets` | Objekt[] | Fundstellen: `seq`, `field` (Pfad im Ereignis, z. B. `value`, `title`, `el.name`, `ch`, `promptLine`, `nodes[2].value`), `range` ○ (`[start, end]` in Zeichen, gezählt als Unicode-Codepunkte; fehlt = ganzes Feld). |

Fundstellen werden beim Anlegen ermittelt und einzeln gespeichert; der gesuchte Text selbst steht nicht in `edits.json`. Abgeleitete Werte in `steps.json` folgen über [`prov`](#steps-json). Einen frei wählbaren Ersatztext gibt es nicht – der Platzhalter ist fest (siehe „Darstellung in Ausgaben“).

**Zeitraum-Schwärzung (`range`)**

| Feld | Typ | Bedeutung |
|---|---|---|
| `seqRanges` | int[2][] | Geschlossene `seq`-Bereiche `[von, bis]`. |
| `images` | bool | Auch die Bilder des Zeitraums zurückhalten. Standard `true`. |
| `origin` | enum | `period` (vom Autor gewählter Zeitraum) \| `step` (aus einem Schritt aufgelöst). |
| `anchor` | int | Nur bei `origin: "step"`: Anker des Schritts beim Anlegen. Er bestimmt allein, wo der Platzhalter erscheint. |
| `trigger` | enum | Nur bei einer Schwärzung, die **während der Aufnahme** entstand: `lastInput` (Befehl „Letzte Eingabe schwärzen“). Sie wird beim Stopp endgültig angewendet, siehe [unten](#redact-last-input). Fehlt bei Schwärzungen aus dem Viewer. |
| `markerSeq` | int | Bei `trigger: "lastInput"` (dort Pflicht): der zugehörige `marker` im Journal. |

Betroffen sind alle Ereignisse mit `seq` in einem der Bereiche **und** alle Ereignisse, deren `ref` dorthin zeigt (später geschriebene Anreicherungen und Bilder). Ihre Inhalte werden nicht ausgegeben; bei `images: true` auch nicht die Bilder, deren `frame`-Ereignis betroffen ist. Ein Schritt, dessen Eingabeereignisse vollständig betroffen sind, erscheint nur noch als Platzhalter; ein teilweise betroffener verliert die betroffenen Inhalte über `prov`.

Für einen Schritt umfasst `seqRanges` beim Anlegen seine Bereiche `seq.input`, `seq.approach` und die Ereignisse aus `seq.related`. Das unterscheidet die Schwärzung von `hidden`: Ein ausgeblendeter Schritt fehlt nur in der Anleitung, ein geschwärzter überall.

**Was erhalten bleibt.** Eine Zeitraum-Schwärzung entfernt Inhalte, nicht das Gerüst der Sitzung. Ausgenommen sind deshalb **Strukturereignisse**, die keine Inhalte des Nutzers tragen und ohne die spätere Ereignisse nicht deutbar wären: `session.*`, `system`, `display.topology`, `scope.change`, `keystate.sync` und `gap`. Sie werden weiter ausgegeben und auch beim endgültigen Schwärzen nicht gekürzt. Ein `marker` bleibt ebenfalls stehen, verliert aber sein `label`. Erhalten bleibt außerdem das `window.info` eines Fensters, auf das nach dem Zeitraum noch ein Ereignis verweist – jedoch ohne `title`, der wie eine Textstelle geschwärzt wird. So bleiben Epochen, Lücken, Abschnitte und Fensterkennungen gültig.

<a id="redact-last-input"></a>
**Schwärzen während der Aufnahme – „Letzte Eingabe schwärzen“**

Der Befehl ([Bedienung](07-aufnahme-bedienung.md), [Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md)) ist für den Fall gedacht, dass etwas Vertrauliches getippt wurde, bevor die Privattaste gedrückt war. Neben dem endgültigen Schwärzen im Viewer ist er die **zweite ausdrückliche Ausnahme** vom Grundsatz, dass das Rohprotokoll nur angehängt wird. Er wirkt in zwei Stufen:

| Stufe | Wann | Was geschieht |
|---|---|---|
| 1 | sofort | Der Recorder schreibt einen [`marker`](#ev-marker) mit `kind: "redactLastInput"` ins Journal und legt in `edits.json` eine Zeitraum-Schwärzung an: `kind: "range"`, `origin: "period"`, `trigger: "lastInput"`, `markerSeq`. Fehlt `edits.json`, entsteht die Datei dabei. Das Journal bleibt in diesem Moment unverändert. Von nun an hält die Leseschicht den Zeitraum zurück – auch gegenüber der mitlaufenden Anzeige der Aufnahmeleiste. |
| 2 | beim Stopp | Nachdem `session.stop` geschrieben ist und bevor das Manifest abgeschlossen wird, wird die Schwärzung **endgültig angewendet** – an Ort und Stelle, mit der unten beschriebenen Wirkung im Format. Der Eintrag erhält `applied: true`, `manifest.history` den Eintrag `redactedDestructive` mit `detail.trigger: "lastInput"`. Eine Rückfrage gibt es an dieser Stelle nicht mehr; sie fand beim Befehl statt. |

*Umfang.* Der Zeitraum reicht von der ersten Taste des **letzten gespeicherten Tastenlaufs** (`marker.tFrom`) bis zum Befehl (`t` des Markers). Der letzte gespeicherte Tastenlauf ist der jüngste Textlauf nach den Regeln der [Schritterkennung](05-schritterkennung.md#schrittarten), dessen Tasten im Klartext im Journal stehen – offen oder abgeschlossen, einschließlich seiner Abschlusstaste. Maskierte Läufe zählen nicht; ihr Inhalt existiert nicht. Betroffen sind **alle** Ereignisse, die vor dem Marker geschrieben wurden und deren `t` nicht vor `tFrom` liegt (außer den Strukturereignissen), dazu wie bei jeder Zeitraum-Schwärzung alles, was später über `ref` auf sie verweist: die Tasten, die Feldinhalte, die sie wiedergeben, und die Bilder, die den Text zeigen – einschließlich dessen, was zwischen dem Ende des Laufs und dem Befehl geschah. Der Recorder schreibt den Marker erst, wenn alles im Journal steht, was er bis zum Befehl beobachtet hat (auch Bilder, die noch kodiert wurden); `seqRanges` steht damit beim Befehl fest.

```json
{ "id": "r1", "kind": "range", "origin": "period", "trigger": "lastInput", "markerSeq": 2190, "seqRanges": [[2140, 2142], [2144, 2189]], "images": true, "createdUtc": "2026-11-11T15:12:03.400000Z", "by": "user" }
```

(Ereignis 2143 ist hier ausgespart: eine spät eingetroffene Beobachtung zu einem früheren Schritt, deren `t` vor `tFrom` liegt.)

*Was der Befehl zusichert – und was nicht.* Was im Zeitraum lag, ist nach dem Stopp in keiner Datei des Sitzungsordners mehr enthalten; an seiner Stelle steht eine Lücke mit der Ursache `removedByAuthor`. **Nicht** erfasst ist, was *nach* dem Befehl noch auf dem Bildschirm oder in einem Feld steht: Spätere Bilder und Feldinhalte zeichnen es erneut auf. Dafür bleibt das Schwärzen im Viewer.

*Absturz.* Endet die Aufnahme ohne Stopp, bleibt Stufe 1 wirksam. Die Wiederherstellung ([16.4](#absturzsicherheit)) holt Stufe 2 nach, bevor die Sitzung für irgendeinen Leser freigegeben wird. Maßgeblich ist dafür der `marker` im Journal (`tFrom` und sein `seq`), nicht der Zustand von `edits.json`.

**Darstellung in Ausgaben** – einheitlich für Viewer-Export, Kopien, KI-Paket und MCP-Server:

| Geschwärzt | Deutsch | Englisch |
|---|---|---|
| Textstelle | `[geschwärzt]` | `[redacted]` |
| Schritt (`range` mit `origin: "step"`) | „Schritt geschwärzt“ | “Step redacted” |
| Zeitraum (`range` mit `origin: "period"`) | Lücke mit Ursache `removedByAuthor` („vom Autor entfernt“) | gap, “removed by the author” |
| Bildfläche | schwarz gefüllte Fläche | schwarz gefüllte Fläche |

Die Länge des Geschwärzten wird nie ausgegeben, auch keine Anfangsbuchstaben. Ob ein geschwärzter Schritt in der Anleitung als Platzhalter erscheint oder entfällt, legt der [Anleitungs-Export](09-anleitungs-export.md) fest.

**Endgültiges Schwärzen** entfernt die Inhalte aus den Rohdaten. Es ist nötig, bevor eine Sitzung als Ordner oder `.steprec` weitergegeben wird, und geschieht auf einem von zwei Wegen ([Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md)):

- **Bereinigte Kopie:** Eine neue Sitzung mit eigener `sessionId` wird aus der geschwärzten Sicht geschrieben; das Original bleibt unverändert. Ihr Manifest trägt den Eintrag `sanitizedCopy`.
- **An Ort und Stelle:** Die Sitzung wird umgeschrieben. Das ist die einzige Operation, die Rohdaten verändert. Ausgelöst wird sie durch ausdrücklichen Befehl im Viewer – oder, allein für Schwärzungen mit `trigger: "lastInput"`, beim Stopp der Aufnahme (siehe [oben](#redact-last-input)).

Wirkung im Format, in allen Fällen gleich:

1. **Textstelle:** Der Inhalt des Felds wird im Journal durch die feste Markierung `[redacted]` ersetzt (im Rohprotokoll sprachunabhängig); die Zeile erhält `redacted` mit dem Feldpfad. Betroffene Tastenereignisse verlieren `ch`; `vk` und `sc` werden 0, `key` wird `"Redacted"`.
2. **Zeitraum:** Jede betroffene Zeile wird auf einen Rest gekürzt, der nur `seq`, `t`, `type` und `redacted: ["*"]` trägt – dazu, allein für die Prüfung der Eingabenummern, `inSeq` (bei Bündeln mit `n`, bei `text.masked` mit `inSeqEnd`). Stehen bleibt, was die Regel „Was erhalten bleibt“ ausnimmt. Die Ableitung macht aus einer Folge gekürzter Zeilen eine Lücke mit der Ursache `removedByAuthor`.
3. **Bilder eines Zeitraums** (`range` mit `images: true`): Die `frame`-Zeilen werden wie alle Zeilen gekürzt. Jedes verbleibende Bild, dessen `base` oder `as` ein Bild des Zeitraums nennt, wird aus seiner wiederhergestellten Ansicht neu abgelegt – als Schlüsselbild oder als Differenzbild gegenüber einem verbleibenden Bild –, sodass kein verbleibendes Bild mehr Bilddaten des Zeitraums braucht. Danach werden alle Blobs gelöscht, die kein verbleibendes `frame`-Ereignis mehr nennt.
4. **Bildfläche:** Geschwärzt wird auf **Blob-Ebene**. Jeder Blob, der im betroffenen Bild zur betroffenen Fläche Pixel beiträgt, wird durch eine Fassung mit schwarz gefüllter Fläche ersetzt (neuer Name, da neuer Inhalt); alle `frame`-Ereignisse, die ihn nennen, werden angepasst und erhalten einen `masks`-Eintrag mit `why: "redacted"`. Damit sind auch alle anderen Bilder geschwärzt, die denselben Blob verwenden oder über eine Differenzkette davon abhängen. Die alten Blobs werden gelöscht.
5. **Danach:** `steps.json` neu ableiten, `cache/` und `export/.tmp/` leeren, Prüfketten und Prüfsummen neu berechnen. Die angewendeten Einträge in `redactions` bleiben als Vermerk stehen und erhalten `applied: true`; sie nennen weiterhin nur Fundstellen, nie Inhalte. `manifest.history` hält fest, dass, wann und wie viel geschwärzt wurde – nie, was.

Zeilen werden dabei nie entfernt, und `seq` bleibt unverändert; Bearbeitungen und Verweise behalten ihre Anker.

<a id="edits-writers"></a>
### 13.7 Gleichzeitiges Schreiben

Viewer und MCP-Server können dieselbe Datei ändern. Jeder Schreiber liest `revision`, wendet seine Änderung an und ersetzt die Datei atomar mit `revision + 1` – aber nur, wenn die Datei noch die gelesene `revision` trägt. Andernfalls lädt er neu und wiederholt die Änderung.

Während einer laufenden Aufnahme und unmittelbar nach ihrem Stopp schreibt allein die **Aufzeichnungssteuerung** des Recorders in `edits.json` (`savedBy: "recorder"`), nach derselben Regel. Viewer und MCP-Server öffnen eine laufende Sitzung nicht. Die Aufzeichnungssteuerung schreibt genau in diesen vier Fällen – und nie etwas anderes:

| Wann | Was |
|---|---|
| Befehl „Letzte Eingabe schwärzen“ | eine Zeitraum-Schwärzung mit `trigger: "lastInput"` ([13.6](#redact-last-input)) |
| sauberer Stopp eines Abschnitts mit `purpose: "replacement"` | den Eintrag in `replacements` – mit den Werten aus `session.start.replaces` ([13.13](#edits-replacements)) |
| „Aufnahme verwerfen“ während eines weiteren Abschnitts | den Eintrag in `sections` mit `discarded: true` ([13.16](#edits-sections)) |
| sauberer Stopp mit vorbelegtem Empfängerhinweis | `guide.intro`, sofern das Feld noch leer ist |

### 13.8 Beispiele

**Beispiel 1 – die Sitzung aus [Anhang A](#beispiel-a).** Der Autor hat einen Kommentar zum vierten Schritt geschrieben (dem getippten Text im Editor, Anker 352) und die Eingaben durchgesehen. Mehr steht nicht in der Datei – genau der Stand, den der Kopf von [Beispiel A des KI-Exports](06-ki-export-und-mcp.md#13-beispiel-a-datei-im-editor-speichern) nennt: ein Kommentar, keine Schwärzung, Durchsicht vollständig.

```json
{
  "format": "steprec.edits",
  "schemaVersion": "1.0",
  "sessionId": "019328a4-6b1e-7c3a-9d52-0f4e8a1b77c0",
  "revision": 7,
  "savedAtUtc": "2026-11-11T13:34:05.000000Z",
  "savedBy": "viewer",
  "comments": [ { "id": "uc1", "anchor": 352, "kind": "note", "text": "Der Text ist beliebig. Wichtig ist der Speichervorgang ab Schritt 5.",
      "createdUtc": "2026-11-11T13:33:40.000000Z", "by": "user", "origin": "viewer" } ],
  "review": {
    "inputs": [],
    "confirmed": { "atUtc": "2026-11-11T13:34:05.000000Z", "throughSeq": 1756, "patternsVersion": "1.0" }
  }
}
```

Keine Eingabe dieser Sitzung ist auffällig; die gesammelte Bestätigung deckt deshalb alle Tippläufe, Feldinhalte und den Kommentar.

**Beispiel 2 – eine andere, stärker bearbeitete Sitzung** (eigene `sessionId`; ihre Nummern haben mit Anhang A nichts zu tun). Es zeigt geänderte Texte von Autor und Agent, Darstellungswahl, Kapitel, einen Tipp, einen internen und einen ausgeblendeten Kommentar aus der Aufnahme, eine Zeichnung, alle drei Arten von Schwärzungen und Einzelentscheidungen der Durchsicht:

```json
{
  "format": "steprec.edits",
  "schemaVersion": "1.0",
  "sessionId": "01932c57-9d02-7e48-b1a6-3c5f8e2d4a90",
  "revision": 12,
  "savedAtUtc": "2026-11-12T09:14:11.000000Z",
  "savedBy": "viewer",
  "guide": { "title": "Angebot als PDF exportieren", "language": "de" },
  "view": { "mode": "window", "showPost": "changed", "importance": "primary", "showCursor": false },
  "steps": [
    { "anchor": 2412, "text": { "de": { "title": "Kundennummer eingeben", "body": "Geben Sie die Kundennummer des Empfängers ein.",
        "by": "user", "atUtc": "2026-11-12T09:10:12.000000Z" } } },
    { "anchor": 2455, "view": { "mode": "focus" }, "auto": { "clickMarker": true, "elementFrame": true } },
    { "anchor": 2489, "text": { "de": { "title": "Export starten", "body": "Klicken Sie auf „Exportieren“.",
        "by": "mcp", "client": "claude-code", "atUtc": "2026-11-12T09:12:30.000000Z" } } }
  ],
  "chapters": [ { "id": "ch1", "title": "Exportieren", "startAt": 2455 } ],
  "comments": [
    { "id": "uc1", "anchor": 2489, "kind": "tip", "text": "Alternativ genügt die Eingabetaste.", "createdUtc": "2026-11-12T09:11:40.000000Z", "by": "user", "origin": "viewer" },
    { "id": "uc2", "anchor": 2412, "kind": "internal", "text": "Vor der Weitergabe prüfen, ob die Nummer geschwärzt ist.", "createdUtc": "2026-11-12T09:11:55.000000Z", "by": "user", "origin": "viewer" },
    { "id": "uc3", "anchor": 2301, "kind": "note", "origin": "recording", "sourceSeq": 2301, "hidden": true, "createdUtc": "2026-11-12T09:12:02.000000Z", "by": "user" }
  ],
  "annotations": [ { "id": "a1", "frame": "f000052", "anchor": 2455, "type": "arrow", "geom": [960, 640, 1040, 694], "style": { "color": "#D32F2F", "width": 4 } } ],
  "redactions": [
    { "id": "r1", "kind": "image", "areas": [ { "m": "M1", "from": "f000041", "to": "f000058", "rect": [0, 1392, 2560, 48] } ], "style": "black",
      "rule": { "kind": "unchanged", "origin": { "frame": "f000052", "rect": [0, 1392, 2560, 48] } }, "createdUtc": "2026-11-12T09:08:02.000000Z", "by": "user" },
    { "id": "r2", "kind": "text", "targets": [ { "seq": 2440, "field": "value", "range": [0, 11] } ], "createdUtc": "2026-11-12T09:08:30.000000Z", "by": "user" },
    { "id": "r3", "kind": "range", "origin": "step", "anchor": 2470, "seqRanges": [[2466, 2476], [2479, 2481]], "images": true, "createdUtc": "2026-11-12T09:09:05.000000Z", "by": "user" }
  ],
  "review": {
    "inputs": [
      { "kind": "typed", "seqRange": [2412, 2438], "status": "kept", "flagged": ["longRandom"], "atUtc": "2026-11-12T09:08:50.000000Z" },
      { "kind": "value", "seq": 2440, "status": "redacted", "flagged": ["iban"], "atUtc": "2026-11-12T09:08:30.000000Z" },
      { "kind": "comment", "commentId": "uc1", "status": "kept", "flagged": ["longRandom"], "atUtc": "2026-11-12T09:12:40.000000Z" }
    ],
    "confirmed": { "atUtc": "2026-11-12T09:13:02.000000Z", "throughSeq": 2611, "patternsVersion": "1.0" }
  }
}
```

Die Kommentare `uc2` (intern) und `uc3` (ausgeblendeter Kommentar aus der Aufnahme) erscheinen in keiner Ausgabe; `uc1` schon. Die Kennungen unter `flagged` (`longRandom`, `iban`) stehen beispielhaft für Einträge der Musterliste, die [Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md) führt.

### 13.9 Bildersatz

**Bildersatz (`assets`).** Bearbeitet der Nutzer ein Bild in einem externen Programm, wird das Ergebnis als Datei in `assets/` abgelegt und hier verzeichnet. Ein Schritt verwendet es über `frameReplacement`.

| Feld | Typ | Bedeutung |
|---|---|---|
| `id` | string | Kennung, eindeutig in der Datei. |
| `file` | string | Pfad im Sitzungsordner, `assets/<id>.png`. |
| `basedOn` | string | Bild-Kennung, aus der der Ersatz entstand. |
| `w`, `h` | int | Pixelmaße. |
| `sameGeometry` | bool | `true`, wenn die Maße denen des Ausgangsbilds entsprechen – Koordinaten sind dann deckungsgleich. |
| `redactionsApplied` | string[] | Kennungen der Bild-Schwärzungen, die beim Erzeugen bereits eingerechnet waren. |
| `createdUtc` | Zeit | Zeitpunkt der Übernahme. |

Ein Bildersatz darf keine Schwärzung umgehen: Bild-Schwärzungen des Ausgangsbilds, die nicht in `redactionsApplied` stehen, werden bei `sameGeometry: true` auch auf den Ersatz angewendet. Bei `sameGeometry: false` wird der Ersatz nicht mehr ausgegeben, bis der Nutzer ihn neu erzeugt oder ausdrücklich bestätigt; stattdessen erscheint das geschwärzte Ausgangsbild.

<a id="edits-review"></a>
### 13.10 Durchsicht der Eingaben (`review`)

`review` hält fest, welche Eingaben der Autor durchgesehen hat. Was als Eingabe und was als auffällig gilt und wann die Durchsicht Pflicht ist, regelt [Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md); die Bedienung beschreibt der [Viewer](08-viewer.md). Hier steht nur die Ablage.

| Feld | Typ | Bedeutung |
|---|---|---|
| `inputs` | Objekt[] | Entscheidungen je Eingabe, siehe unten. |
| `confirmed` | Objekt | Bestätigung der Liste als Ganzes: `atUtc`, `throughSeq` (letztes `seq` des Journals im Moment der Bestätigung), `patternsVersion` (Stand der Musterprüfung). Sie deckt alle Eingaben bis `throughSeq`, die keine Einzelentscheidung brauchen – und Kommentare und Ziel-Angaben aus dem Viewer, die nach `atUtc` weder angelegt noch geändert wurden. |

Je Eintrag in `inputs`:

| Feld | Typ | Bedeutung |
|---|---|---|
| `kind` | enum | `typed` (Tipplauf), `value` (Feldinhalt), `clipboard` (aufgezeichneter Zwischenablage-Inhalt), `comment` (Kommentar des Autors), `goal` (Ziel-Angabe des Autors), `marker` (Text eines Markers: seine Bezeichnung `label`). |
| `seqRange` | int[2] | Bei `typed`: erstes und letztes Tastenereignis des Laufs. |
| `seq` | int | Bei `value`, `clipboard`, einem Kommentar aus der Aufnahme und einem Marker-Text: das Ereignis. Bei einer Ziel-Angabe aus der Aufnahme: das `session.start`-Ereignis. |
| `commentId` | string | Bei einem Kommentar aus dem Viewer: seine Kennung in `comments`. Wird der Text danach geändert, gilt der Kommentar wieder als nicht durchgesehen. |
| `field` | enum | Bei `goal`: `description` \| `expected` \| `reference`. Für eine im Viewer geänderte Angabe (`edits.goal`) fehlt `seq`; ändert der Autor sie danach erneut, gilt sie wieder als nicht durchgesehen. |
| `status` | enum | `kept` (durchgesehen und behalten) \| `redacted` (geschwärzt; die Schwärzung selbst steht unter `redactions`). |
| `flagged` | string[] | Warum die Eingabe beim Durchsehen als auffällig galt: `plaintextWindow` und/oder Kennungen von Mustern. Nur zur Nachvollziehbarkeit. |
| `atUtc` | Zeit | Zeitpunkt der Entscheidung. |

Regeln für alle Leser:

- Eine Eingabe gilt als **durchgesehen**, wenn ein Eintrag sie vollständig deckt (bei `typed`: alle ihre Tastenereignisse liegen in `seqRange`) – oder wenn sie nicht auffällig ist und `confirmed` sie einschließt. Maßgeblich sind Ereignisnummern, nicht der Zuschnitt der Schritte.
- **Kommentare des Autors** gehören zur Durchsicht – die während der Aufnahme geschriebenen ebenso wie die aus dem Viewer. Sie sind Freitext und können Vertrauliches enthalten. Interne und ausgeblendete Kommentare brauchen keine Entscheidung: Sie erscheinen ohnehin in keiner Ausgabe.
- **Ziel-Angaben** (`goal`: Beschreibung, erwartetes Ergebnis, Bezug) sind ebenfalls Autorentext und gehören zur Durchsicht – je Feld einzeln, in der wirksamen Fassung aus Manifest und Overlay ([13.12](#edits-goal)).
- **Marker-Texte** – die Bezeichnung `label` eines Markers des Autors – gehören aus demselben Grund dazu. Ein Marker ohne Bezeichnung braucht keinen Eintrag; ein Kommentar, den der Autor an einem Marker anlegt, zählt als Kommentar.
- Ob eine Eingabe **auffällig** ist, berechnet die Leseschicht: aus `flags.plaintext` des Schritts (Klartext aus einem Fenster, in dem Passwortfelder nicht erkennbar sind) und aus der Musterprüfung. Das Journal trägt dafür kein eigenes Kennzeichen, weil die Muster sich mit dem Programm weiterentwickeln.
- **Ungeprüfte auffällige Eingaben** geben Wege ohne Oberfläche – MCP-Server und Export per Kommandozeile – nur als Zähler aus: `{unreviewed:N}`. Die Eingabe wird dabei immer als Ganzes ersetzt. Es gibt keinen Schalter, der das aufhebt; freigegeben wird allein durch die Durchsicht im Viewer.
- **Was N zählt:**

  | Eingabe | N |
  |---|---|
  | getippter Lauf (`typed`) | Tastenanschläge, gezählt wie `text.masked.count` ([9.9](#ev-text-masked)): jedes Drücken außer reinen Modifikatoren und Feststellern, Korrekturtasten und Wiederholungen eingeschlossen |
  | Feldinhalt (`value`) | Zeichen des gespeicherten Werts |
  | Zwischenablage-Inhalt (`clipboard`) | Zeichen des gespeicherten Inhalts |
  | Kommentar (`comment`) | Zeichen des Kommentartexts |
  | Ziel-Angabe (`goal`) | Zeichen des Felds |
  | Marker-Text (`marker`) | Zeichen der Bezeichnung |

  „Zeichen“ sind Unicode-Codepunkte des gespeicherten Texts; ein gekürzt gespeicherter Wert zählt mit seiner gespeicherten Länge.
- Kommen Eingaben hinzu (angehängte Aufnahme) oder erkennt ein neuerer Stand der Musterprüfung weitere Treffer, sind genau diese Eingaben ungeprüft.
- Maskierte Eingaben brauchen keinen Eintrag; ihr Inhalt existiert nicht.

### 13.11 Export-Einstellungen (`exports`)

Freies Objekt des Anleitungs-Exports; andere Leser ignorieren es.

<a id="edits-goal"></a>
### 13.12 Ziel-Angaben (`goal`)

Ändert der Autor im Viewer, was die Aufnahme zeigen soll, steht die Änderung hier. Manifest und Journal bleiben unverändert.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `description`, `expected`, `reference` | string | ○ | Bedeutung und Längengrenzen wie in [`manifest.goal`](#manifest-goal). |
| `atUtc` | Zeit | ● | Zeitpunkt der letzten Änderung. |
| `by` | enum | ● | `user`. Agenten können Ziel-Angaben nicht schreiben. |

Die **wirksamen Ziel-Angaben** entstehen Feld für Feld: Steht ein Feld im Overlay, gilt dieser Wert; eine leere Zeichenkette nimmt die Angabe aus allen Ausgaben; fehlt das Feld im Overlay, gilt der Wert aus dem Manifest. Alle Ausgaben zeigen nur die wirksamen Angaben – stets als Angabe des Autors gekennzeichnet, nie als Beobachtung. Für Durchsicht, Schwärzungen und Verallgemeinerungen gelten die Regeln aus [4.7](#manifest-goal).

```json
{ "goal": { "expected": "Die E-Mail mit der PDF-Datei liegt im Ordner „Gesendet“.", "reference": "",
    "atUtc": "2026-11-12T10:20:44.000000Z", "by": "user" } }
```

(Das erwartete Ergebnis ist neu gefasst, der Bezug aus allen Ausgaben genommen, die Beschreibung gilt weiter aus dem Manifest.)

<a id="edits-replacements"></a>
### 13.13 Ersetzungen (`replacements`)

Mit „Neu aufnehmen und ersetzen“ nimmt der Autor einen Teil des Ablaufs noch einmal auf. Die Neuaufnahme wird ein eigener [Aufnahmeabschnitt](#aufnahmeabschnitte) mit `purpose: "replacement"`; ein Eintrag hier lässt sie **an die Stelle** der früheren Schritte treten. Die ersetzten Schritte bleiben in Journal und `steps.json` – nichts wird gelöscht.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `id` | string | ● | Kennung, eindeutig in der Datei (`rp1`). |
| `replaces` | Objekt | ● | Der ersetzte Bereich: `section` (Nummer des Abschnitts, in dem er liegt), `fromSeq`, `toSeq` (erstes und letztes Ereignis des Bereichs). |
| `by` | Objekt | ● | Die Neuaufnahme: `section` (Nummer ihres Abschnitts). |
| `atUtc` | Zeit | ● | Zeitpunkt, zu dem die Ersetzung angelegt wurde. |

`section` ist die Abschnittsnummer `part` aus `manifest.parts` und `session.start`.

**Wer den Eintrag anlegt.** Die Aufzeichnungssteuerung trägt die Ersetzung selbst ein – **unmittelbar nach dem sauberen Stopp** der Neuaufnahme, bevor der Viewer die Sitzung öffnet: `replaces` übernimmt `section`, `fromSeq` und `toSeq` unverändert aus `session.start.replaces` des neuen Abschnitts, `by.section` ist dessen Nummer, `atUtc` der Zeitpunkt des Eintragens. Der Autor muss die Ersetzung also nicht eigens bestätigen. **Kein** Eintrag entsteht, wenn der Abschnitt durch Absturz endete (nach der Wiederherstellung ist er eine nicht verwendete Neuaufnahme) oder verworfen wurde ([13.16](#edits-sections)). Im Viewer lässt sich der Eintrag entfernen und wieder anlegen.

Regeln:

- **Ersetzt** sind alle Schritte, deren `anchorSeq` im Bereich `fromSeq` … `toSeq` liegt – gleich welcher Art, auch Lücken- und Systemschritte. Der Viewer legt den Bereich vom ersten Ereignis des ersten bis zum letzten Ereignis des letzten markierten Schritts an; so bleibt er gültig, wenn die Schritte später anders geschnitten werden.
- An ihre Stelle treten **alle** Schritte des Abschnitts `by.section`, in ihrer zeitlichen Reihenfolge. Wie daraus die Schrittfolge und die Nummerierung der Ausgaben entstehen, legt die [Schritterkennung](05-schritterkennung.md#wirksame-schrittfolge) fest.
- Ersetzte Schritte gelten in Anleitung und KI-Paket als „ersetzt“. Sie sind weder ausgeblendet noch geschwärzt: Die ausführlichste Detailstufe des KI-Pakets zeigt beide Fassungen, und Schwärzungen, Durchsicht und Verallgemeinerungen gelten für sie unverändert.
- **Rückgängig** heißt: Der Eintrag wird entfernt. Die früheren Schritte gelten wieder; der Abschnitt der Neuaufnahme bleibt im Journal und gilt als **nicht verwendete Neuaufnahme** – seine Schritte gehören zu keiner Ausgabe außer der ausführlichsten Detailstufe des KI-Pakets, dort gekennzeichnet. Die Ersetzung lässt sich wieder anlegen; ihren vorgesehenen Bereich nennt `session.start.replaces` des Abschnitts.
- Ein Bereich darf in einem Abschnitt liegen, der selbst eine Neuaufnahme ist (ein Teil der Neuaufnahme wird noch einmal ersetzt). Eine Ersetzung, deren Bereich in einer nicht verwendeten Neuaufnahme liegt, bleibt ohne Wirkung – ebenso eine, deren Abschnitt `by.section` oder `replaces.section` verworfen ist.
- Bedingungen (Prüfung F22): `replaces.section` und `by.section` existieren und sind verschieden; `by.section` ist größer als `replaces.section` und ein Abschnitt mit `purpose: "replacement"`; jeder Abschnitt ist `by.section` höchstens eines Eintrags; `fromSeq` ≤ `toSeq`, beide im Abschnitt `replaces.section`; die Bereiche verschiedener Einträge überschneiden sich nicht.
- Die Buchführung des Vollständigkeitsberichts bezieht sich weiter auf das ganze Journal.

```json
{ "replacements": [ { "id": "rp1", "replaces": { "section": 0, "fromSeq": 2140, "toSeq": 2305 }, "by": { "section": 1 },
    "atUtc": "2026-11-12T08:41:30.000000Z" } ] }
```

*Stand der Umsetzung:* Abschnitte (`parts[]`, `session.start` mit `part`, `purpose`, `replaces`) und `replacements[]` sind umgesetzt; geschrieben werden sie beim Anhängen einer Neuaufnahme (`SectionAppender`, [03 §1.7](03-aufzeichnungskern.md#weitere-abschnitte)), aufgehoben und wieder angelegt im Viewer (`SectionEdits`). Die Bedingungen von F22 prüft die Leseschicht beim Aufbau der wirksamen Folge (ungültige Einträge bleiben ohne Wirkung, mit Grund `unknownSection`, `order`, `notReplacement`, `duplicate`, `range`, `overlap`, `discarded`, `inUnused`); die Formatprüfung (`validate`) meldet sie nicht eigens. `sections[]` (13.16) wird gelesen, aber von diesem Programm nicht geschrieben – eine verworfene Neuaufnahme entsteht gar nicht erst als Abschnitt. `parts[].discarded` wird nicht geschrieben.

<a id="edits-substitutions"></a>
### 13.14 Verallgemeinerungen (`substitutions`)

Eine Verallgemeinerung ist eine Regel „Wert → Platzhalter“: Der eigene Benutzername wird zu `{Benutzername}`, ein Servername zu `{Server}`. So taugt eine Anleitung für andere, und persönliche Werte bleiben aus den Ausgaben heraus. Die Regel wirkt wie eine Schwärzung über die gemeinsame Leseschicht in **allen** Ausgaben – nur steht statt `[geschwärzt]` der Platzhalter.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `id` | string | ● | Kennung, eindeutig in der Datei (`sb1`). |
| `match` | Objekt | ◐ | Was ersetzt wird – genau eines von `literal` (Zeichenkette) oder `pattern` (regulärer Ausdruck). Pflicht, solange die Regel nicht endgültig angewendet ist. |
| `placeholder` | string | ● | **Name** des Platzhalters ohne Klammern, z. B. `Benutzername`. In Ausgaben erscheint er als `{Benutzername}`. |
| `caseSensitive` | bool | ○ | Groß- und Kleinschreibung beachten. Standard `false`. |
| `scope` | enum | ● | `all` (ganze Sitzung) \| `steps` (nur die Schritte aus `anchors`). |
| `anchors` | int[] | ◐ | Pflicht bei `scope: "steps"`: Anker der Schritte, für die die Regel gilt. |
| `targets` | string[] | ● | Wo ersetzt wird: `text`, `images` oder beides. Mindestens eines. |
| `images` | Objekt | ○ | Ermittelte Bildflächen, siehe „Bilder“. Nur bei `targets` mit `images`. |
| `suggestion` | enum | ○ | Wenn die Regel aus einem Vorschlag übernommen wurde, dessen Art: `userName`, `machineName`, `domain`, `profilePath`. |
| `createdUtc`, `by` | Zeit, enum | ● | Zeitpunkt und Urheber (`user`). |
| `applied`, `appliedUtc` | bool, Zeit | ○ | Nach endgültigem Anwenden, siehe unten. |

**Was passt (`match`)**

| Form | Regel |
|---|---|
| `literal` | Mindestens 3 Zeichen. Gesucht wird die Zeichenkette als Ganzes. Beginnt oder endet sie mit einem Buchstaben oder einer Ziffer, zählt eine Fundstelle nur, wenn dort im Text **kein** weiterer Buchstabe und keine Ziffer anschließt: `max` passt in `C:\Users\max\Dokumente` und `max@firma.example`, nicht in `maximal`. |
| `pattern` | Regulärer Ausdruck in der Syntax von .NET, beschränkt auf das, was sich ohne Zurückverfolgung auswerten lässt: keine Rückverweise, keine Vor- und Rückgriffe, keine atomaren Gruppen. Höchstens 500 Zeichen. Er darf nicht auf die leere Zeichenkette passen. Ausgewertet wird kulturunabhängig; die Laufzeit ist damit linear zur Textlänge. |

- Mehrere Regeln werden in der Reihenfolge der Liste angewendet. Was eine Regel ersetzt hat, durchsucht keine spätere mehr.
- Mehrere Regeln dürfen denselben Platzhalter verwenden (zwei Servernamen → `{Server}`).
- `placeholder`: 1 bis 40 Zeichen, beginnt mit einem Buchstaben, danach Buchstaben, Ziffern, Leerzeichen, `_` und `-`. Nicht erlaubt sind die kanonischen Tastennamen ([9.8](#ev-key)) und die Marken `masked`, `uncertain`, `unreviewed` – sonst wäre `{Enter}` in einer Tastenfolge zweideutig. Ein Wert mit umschließenden geschweiften Klammern wird beim Lesen wie der Name ohne sie behandelt.

**Wo ersetzt wird (`targets` enthält `text`)**

Die Leseschicht wendet die Regeln auf **jeden Text** an, den sie ausgibt: getippten Text, Feldinhalte, Element- und Fensternamen, Adressen und Pfade, Text-Schnappschüsse, aufgezeichnete Zwischenablage-Inhalte, Kommentare, Marker-Bezeichnungen, Ziel-Angaben und vom Autor gesetzte Schritttexte. Bei `scope: "steps"` gilt das nur für Texte, die zu den genannten Schritten gehören – ihre Ereignisse, ihre abgeleiteten Werte, ihre Texte und Kommentare.

| Stelle | Darstellung nach der Leseschicht |
|---|---|
| Text in einem Feld (Titel, Name, Wert, Kommentar, Ziel …) | Die Fundstelle wird durch `{Name}` ersetzt: `C:\Users\{Benutzername}\Dokumente`. Der Rest des Texts bleibt. |
| Ereignis | Das Kopffeld `substituted` nennt die Pfade der betroffenen Felder – entsprechend `redacted`. |
| Getippter Text im Schritt | `input.text` und `action.text` tragen `{Name}`; in `input.tokens` steht an der Stelle ein eigenes Token `{ "placeholder": "Name" }`. |
| Tastenereignisse der Fundstelle | Von der ersten bis zur letzten Taste, die zur Fundstelle beitrug – Korrekturtasten dazwischen eingeschlossen –, ohne Zeichen und Tastencodes: kein `ch`, `vk` und `sc` sind 0, `key` ist `"Substituted"`, `substituted` nennt `ch`. Die erste dieser Tasten trägt zusätzlich `placeholder` mit dem Namen. |
| Geschwärztes, Maskiertes, Ungeprüftes | Bleibt, was es ist: `[geschwärzt]`, `{masked:N}`, `{unreviewed:N}`. Verallgemeinert wird nur, was sonst im Klartext ausgegeben würde. |

Geprüft wird bei getipptem Text der **eingefügte Text des Laufs** (`input.text`) – nicht die einzelne Taste. Ein Wert, der über mehrere Läufe verteilt getippt wurde, wird als getippter Text nicht erkannt; im Feldinhalt danach schon.

Der Viewer darf dem Autor die Originalwerte anzeigen („Originalwerte zeigen“). Das ist eine reine Anzeige: Kopieren, Exportieren, Suchen und der MCP-Server liefern stets die verallgemeinerte Sicht.

**Bilder (`targets` enthält `images`)**

In Bildern lässt sich nur ersetzen, wo die **Position des Texts bekannt** ist. Eine Texterkennung gibt es in Formatversion 1 nicht. Der Viewer ermittelt die Flächen, wenn die Regel angelegt oder geändert wird und wenn ein Abschnitt hinzukommt, und speichert sie:

| Feld | Typ | Bedeutung |
|---|---|---|
| `areas` | Objekt[] | Betroffene Bilder mit Fläche, gleiche Form wie bei der Bild-Schwärzung ([13.6](#edits-redactions)): `m`, `from`, `to`, `rect`. Dazu `source`: `element` (Rechteck eines Elements, dessen Name oder Wert die Fundstelle enthält) \| `windowTitle` (Titelzeile eines Fensters, soweit ihr Rechteck bekannt ist) \| `typed` (Rechteck des Felds, in das der Wert getippt wurde) \| `manual` (vom Autor von Hand markiert). |
| `throughSeq` | int | Bis zu diesem Ereignis wurden die Flächen ermittelt. |
| `unresolved` | int | Zahl der Text-Fundstellen, zu denen keine Bildposition bekannt ist. Ist sie größer als 0, weist der Viewer darauf hin und bietet an, die Stellen von Hand zu markieren. |

- Die Leseschicht füllt jede Fläche wie eine Schwärzung schwarz und setzt – sofern er hineinpasst – den Platzhalter `{Name}` in heller Schrift hinein. Ausgaben, die gefüllte Flächen mit ihrem Grund ausweisen, nennen den Grund `substituted` und den Platzhalter.
- Ersetzt wird die **ganze** Fläche des Elements, nicht die einzelne Textstelle darin: Wo im Feld der Wert steht, ist nicht bekannt.
- Maßgeblich ist allein `areas`. Für Bilder nach `throughSeq` gilt bis zur nächsten Ermittlung nur die Textersetzung; die Qualitätsprüfung weist darauf hin.
- Nicht erreichbar ist Text, den es nur als Bildpunkte gibt – Inhalte von Remote-Sitzungen, Zeichenflächen, Vorschaubilder der Taskleiste. Das ist eine benannte Grenze, kein Fehler.

**Was eine Verallgemeinerung nicht leistet – und endgültiges Anwenden**

Bis zum endgültigen Anwenden stehen die Originalwerte in den Rohdaten, **und die Regel selbst nennt den Wert** (`match.literal`). `edits.json` ist deshalb so vertraulich wie die Sitzung. Dauerhaft entfernt wird der Wert auf denselben zwei Wegen wie bei Schwärzungen ([13.6](#edits-redactions)) – bereinigte Kopie oder an Ort und Stelle:

1. **Text:** Im Journal steht an jeder Fundstelle der Platzhalter `{Name}`; die Zeile erhält `substituted` mit dem Feldpfad. Betroffene Tastenereignisse nehmen die oben beschriebene Form an.
2. **Bild:** Jede Fläche aus `areas` wird auf Blob-Ebene gefüllt wie eine Bild-Schwärzung; die `frame`-Ereignisse erhalten einen `masks`-Eintrag mit `why: "substituted"` und `label` (dem Namen des Platzhalters).
3. **Regel:** Der Eintrag bleibt als Vermerk stehen, erhält `applied: true` und **verliert `match`** – in der bereinigten Kopie ebenso wie an Ort und Stelle. Danach nennt keine Datei der Sitzung den Wert mehr.
4. Danach wie bei Schwärzungen: `steps.json` neu ableiten, `cache/` und `export/.tmp/` leeren, Prüfketten und Prüfsummen erneuern; `manifest.history` vermerkt den Vorgang unter `redactedDestructive` mit der Zahl der angewendeten Regeln.

```json
{ "substitutions": [
    { "id": "sb1", "match": { "literal": "mmuster" }, "placeholder": "Benutzername", "caseSensitive": false, "scope": "all",
      "targets": ["text", "images"], "suggestion": "userName",
      "images": { "areas": [ { "m": "M1", "from": "f000044", "to": "f000051", "rect": [520, 620, 560, 26], "source": "element" } ],
        "throughSeq": 2611, "unresolved": 1 },
      "createdUtc": "2026-11-12T09:15:02.000000Z", "by": "user" },
    { "id": "sb2", "match": { "pattern": "SRV-[A-Z]{2}[0-9]{2}" }, "placeholder": "Server", "caseSensitive": true, "scope": "steps",
      "anchors": [2412, 2489], "targets": ["text"], "createdUtc": "2026-11-12T09:16:40.000000Z", "by": "user" }
] }
```

Aus dem Fenstertitel `\\SRV-FS01\Freigabe – mmuster` wird damit in jeder Ausgabe `\\{Server}\Freigabe – {Benutzername}`, aus dem getippten `mmuster` das Token `{ "placeholder": "Benutzername" }`.

<a id="edits-quality"></a>
### 13.15 Ignorierte Fundstellen der Qualitätsprüfung (`qualityIgnores`)

Die Qualitätsprüfung vor dem Export ([Anleitungs-Export](09-anleitungs-export.md)) meldet Fundstellen nach einem Regelkatalog. Welche Kriterien sie aus den Schrittdaten liest, steht in der [Schritterkennung](05-schritterkennung.md#qualitaetskriterien). Hier steht nur, was der Autor ausdrücklich **ignoriert** hat.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `rule` | string | ● | Kennung der Regel aus dem Katalog: `Q` und mindestens zwei Ziffern (`Q07`). |
| `anchor` | int | ◐ | Anker des Schritts, an dem die Fundstelle hängt. Fehlt bei Fundstellen, die die ganze Sitzung betreffen. |
| `key` | string | ○ | Unterscheidet mehrere Fundstellen derselben Regel am selben Schritt – etwa die Sprache (`en`), eine Bild-Kennung oder die Art eines Vorschlags (`userName`). Den Wertebereich legt die Regel fest. Höchstens 64 Zeichen; **nie** aufgezeichneter Inhalt. |
| `atUtc` | Zeit | ● | Zeitpunkt des Ignorierens. |
| `by` | enum | ○ | `user`. |

Regeln:

- Eine Fundstelle ist durch (`rule`, `anchor`, `key`) bestimmt; jeder Dreiklang steht höchstens einmal in der Liste.
- Eine ignorierte Fundstelle erscheint in der Prüfung nicht mehr und zählt nicht mit. Wird der Eintrag entfernt, erscheint sie wieder.
- Ignorieren wirkt **nur** auf die Hinweisliste. Es hebt weder die Sperre für ungeprüfte auffällige Eingaben auf ([13.10](#edits-review)) noch eine Schwärzung oder Verallgemeinerung.
- Lässt sich ein Anker nach einer Neuberechnung keinem Schritt mehr zuordnen, wandert der Eintrag nach `orphans`.

```json
{ "qualityIgnores": [
    { "rule": "Q02", "anchor": 2455, "atUtc": "2026-11-12T09:20:11.000000Z", "by": "user" },
    { "rule": "Q14", "anchor": 2412, "key": "machineName", "atUtc": "2026-11-12T09:20:30.000000Z", "by": "user" }
] }
```

<a id="edits-sections"></a>
### 13.16 Aufnahmeabschnitte (`sections`)

Hier steht, was über einen **ganzen** [Aufnahmeabschnitt](#aufnahmeabschnitte) entschieden wurde. Derzeit gibt es eine Entscheidung: Der Abschnitt ist **verworfen**.

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `section` | int | ● | Abschnittsnummer (`part`). Nie 0; je Abschnitt höchstens ein Eintrag. |
| `discarded` | bool | ● | `true`: Der Abschnitt ist verworfen. `false`: Ein beim Aufnehmen verworfener Abschnitt wurde im Viewer wieder aufgenommen. |
| `atUtc` | Zeit | ● | Zeitpunkt der Entscheidung. |
| `by` | enum | ○ | `recorder` („Aufnahme verwerfen“ während der Aufnahme) \| `user` (im Viewer). |

**Das Journal wird nie gekürzt.** Ein verworfener Abschnitt wird markiert, nicht entfernt: Seine Zeilen stehen weiter in `events.jsonl`, seine Bilder in `frames/`, und `seq`, Sitzungsuhr, Eingabenummern und Kennungen laufen über ihn hinweg fort.

**Verwerfen während der Aufnahme.** „Aufnahme verwerfen“ in einem **weiteren** Abschnitt – einer Fortsetzung oder einer Neuaufnahme – betrifft **nur diesen Abschnitt**. Die Aufzeichnungssteuerung

1. beendet ihn mit `session.stop` und `reason: "discarded"`,
2. trägt hier den Eintrag mit `discarded: true` und `by: "recorder"` ein,
3. legt **keine** Ersetzung an.

Alle früheren Abschnitte, die Bearbeitungen und die Sitzung selbst bleiben, wie sie waren. Den ersten Abschnitt kann man so nicht verwerfen: Wer die erste Aufnahme verwirft, verwirft die ganze Sitzung ([Bedienung](07-aufnahme-bedienung.md)).

**Was gilt.** Maßgeblich ist der Eintrag hier, wenn es ihn gibt. Ohne Eintrag gilt ein Abschnitt als verworfen, wenn sein `session.stop` den Grund `discarded` trägt. Ein verworfener Abschnitt bleibt damit auch dann verworfen, wenn `edits.json` fehlt oder beschädigt ist.

**Wirkung in der Leseschicht.** Ein verworfener Abschnitt gehört zu **keiner Ausgabe**:

- Seine Schritte, Ereignisse, Bilder und Texte erscheinen weder in der Anleitung noch im KI-Paket – in keiner Detailstufe – noch in Antworten des MCP-Servers (auch nicht in Rohereignissen, Bildern und Suchtreffern), in Kopien, im Schrittabgleich, in der Qualitätsprüfung oder im Suchindex.
- Anders als eine nicht verwendete Neuaufnahme wird er auch dort nicht gezeigt, wo ersetzte Schritte sichtbar sind.
- Eine Ersetzung, die sich auf ihn stützt, ist ohne Wirkung ([13.13](#edits-replacements)).
- Seine Eingaben brauchen keine Entscheidung in der Durchsicht.
- Ausgaben, die über die Aufzeichnung selbst Auskunft geben, dürfen nennen, **dass** und wie viele Abschnitte verworfen wurden – nie, was darin war. Die Zeit eines verworfenen Abschnitts geht in keine Zeitrechnung einer Ausgabe ein.
- Beim Verwerfen und beim Wiederaufnehmen werden `cache/search/` und `export/.tmp/` geleert, bevor die Änderung gespeichert ist.

**Was Verwerfen nicht leistet.** Die Rohdaten des Abschnitts liegen weiter im Sitzungsordner; wer den Ordner weitergibt, gibt sie mit. **Dauerhaft entfernt** werden sie erst durch die bereinigte Kopie ([13.6](#edits-redactions)): Sie enthält von einem verworfenen Abschnitt nur noch das Gerüst – Sitzungskopf, Sitzungsende und die auf den Rest gekürzten Zeilen, wie bei einem entfernten Zeitraum –, aber keine Inhalte und keine Bilder. Dasselbe geschieht, wenn der Autor die Sitzung an Ort und Stelle endgültig schwärzt.

```json
{ "sections": [ { "section": 2, "discarded": true, "atUtc": "2026-11-12T08:52:07.000000Z", "by": "recorder" } ] }
```

Das zugehörige Ende des Abschnitts im Journal:

```json
{"seq":5388,"t":265400000,"type":"session.stop","reason":"discarded","utc":"2026-11-12T08:52:06.800000Z"}
```

<a id="edits-merges"></a>
### 13.17 Zusammengefasste Schritte (`merges`)

Der Autor kann aufeinanderfolgende Schritte für Anleitungen zu **einem** Schritt zusammenfassen. Das ist reine Darstellung der Anleitung: Schritte, Nummern, Journal und `steps.json` bleiben unverändert, und KI-Paket, MCP-Server, Vergleich und AutoHotkey-Ausgaben zeigen weiter jeden Einzelschritt. Das KI-Paket vermerkt die Gruppe nur als Hinweiszeile am Schritt ([06](06-ki-export-und-mcp.md#54-feldkatalog)).

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `id` | string | ● | Eindeutig in der Datei (`mg1`, `mg2` …). |
| `anchors` | int[] | ● | Anker der zusammengefassten Schritte in der Reihenfolge der Anleitung, mindestens zwei. Der **erste** Anker führt die Gruppe. |
| `text` | Objekt | ○ | Eigener Text der Gruppe je Sprache, im Aufbau wie `steps[].text` ([13.3](#edits-steps)): `{ "de": { "title": "…", "body": "…", "by": "user", "atUtc": "…" } }`. Ohne `text` gilt der Titel des führenden Schritts und der verbundene Text der sichtbaren Mitglieder. |
| `createdUtc`, `by` | Zeit, enum | ○ | Zeitpunkt und Urheber (`user`). |

Regeln:

- **Voraussetzungen beim Anlegen** (der Editor prüft sie; Leser verlassen sich nicht darauf): Die Schritte folgen in der Anleitungsreihenfolge unmittelbar aufeinander; keiner ist eine Lücke (`gap`) oder ein eingefügter Schritt; keiner gehört schon zu einer Gruppe; in der Auswahl beginnt kein Kapitel (nur der erste Schritt darf es tun).
- **Verhalten eines Lesers:** Anker, die keinem Schritt entsprechen, werden übergangen; bleiben weniger als zwei, ist der Eintrag wirkungslos. Gehört ein Anker zu mehreren Einträgen, gilt der erste.
- **Darstellung:** Nur der führende Schritt erscheint in der Anleitung. Sein Zeitraum reicht vom frühesten Beginn bis zum spätesten Ende der Mitglieder; Vorher-Bild ist das erste, Nachher-Bild das letzte vorhandene Bild der sichtbaren Mitglieder; die Wichtigkeit ist die höchste der Mitglieder; Kommentare aller Mitglieder gehören zur Gruppe. Der Anleitungstext ist, wenn kein eigener Text gesetzt ist, der Text der sichtbaren Mitglieder hintereinander (Text des Autors, sonst Text der Vorlage). Geschwärzte und ausgeblendete Mitglieder tragen nichts bei; ist der führende Schritt ausgeblendet, ist die Gruppe ausgeblendet.
- **Texte:** `text` ist Autorentext wie jeder andere: Schwärzungen und Verallgemeinerungen gelten, die Durchsicht erfasst ihn, und „Alle ersetzen“ ändert auch ihn. Für eine fehlende Sprache gilt dieselbe Regel wie bei Schritttexten („Fehlende Übersetzung“ im Export).
- **Aufheben:** Das Entfernen des Eintrags stellt die Einzelschritte wieder her; Journal und `steps.json` brauchen dafür nichts.
- **Bearbeitungsstände:** Zusammenfassungen gehören zu dem, was ein Bearbeitungsstand sichert ([14](#history)).

```json
{ "merges": [
    { "id": "mg1", "anchors": [1700, 1734, 1790], "createdUtc": "2026-11-12T09:41:07.000000Z", "by": "user",
      "text": { "de": { "title": "Datei speichern", "body": "Speichern Sie die Datei als notiz.txt.", "by": "user", "atUtc": "2026-11-12T09:42:00.000000Z" } } }
] }
```

---

<a id="cache"></a>
### 13.18 Formatierter Text (`fmt`)

Autorentexte können eine kleine Auszeichnung tragen ([09 §11.1](09-anleitungs-export.md#111-formatierter-text-des-autors), [13](13-handbuch-fahrplan.md) Paket 21). Gedeutet wird sie **nur**, wenn das Feld als formatiert gekennzeichnet ist; jeder Text ohne Kennzeichen bleibt wörtlich – so ändern sich bestehende Aufnahmen nicht.

| Ort | Kennzeichen | Gilt für |
|---|---|---|
| `steps[].text.<lang>` | `"fmt": 1` | `body` (der Titel ist nie formatiert) |
| `merges[].text.<lang>` | `"fmt": 1` | `body` |
| `steps[].chapter`, `steps[].chapter.<lang>` | `"fmt": 1` | `text` der jeweiligen Sprache |
| `comments[]` | `"fmt": 1` | `text` |
| `guide` bzw. `guide.i18n.<lang>` | `"fmt": { "intro": 1, "outro": 1 }` | Einleitung, Abschluss je Feld |

- Ein Text mit `by: "mcp"` (geschrieben von einem Agenten) bleibt immer wörtlich, auch mit Kennzeichen; ein unbekannter Wert von `fmt` gilt als „nicht formatiert“.
- Neue optionale Felder: `schemaVersion` bleibt „1.0“. Programme bis 0.13 zeigen formatierte Texte mit ihren Zeichen (`**fett**`), verlieren aber nichts.
- **Variablen** (Paket 23): `guide.vars` = `{ "Produkt": "Kasse 3000" }`, je Sprache `guide.i18n.<lang>.vars` (überschreibt). Namen: Buchstabe, dann bis zu 39 Buchstaben, Ziffern, Leerzeichen, `_ . -`. `{{Name}}` wirkt in allen eigenen Texten, auch ohne `fmt`; in formatiertem Text wird der Wert maskiert eingesetzt (seine Sterne bleiben Sterne). Werte durchlaufen Schwärzung und Verallgemeinerung.
- **Querverweise und Tabellen** sind Teil der Auszeichnung (`fmt: 1`), siehe [09 §11.1](09-anleitungs-export.md#111-formatierter-text-des-autors).
- Der Editor setzt das Kennzeichen nur, wenn der Text tatsächlich Formatierung enthält; sonst speichert er reinen Text ohne Kennzeichen.

| Nr. | Anforderung | Priorität | Abnahme |
|---|---|---|---|
| DAT-084 | Auszeichnung wird nur bei `fmt: 1` und nicht bei `by: "mcp"` gedeutet; alle übrigen Texte bleiben wörtlich. | Muss | Ein alter Text mit `*`, `[`, `` ` `` ergibt dieselbe Ausgabe wie vorher (`LegacyOutputTests`); ein Agententext mit `**x**` zeigt die Sterne |

### 13.20 Gliederung (Paket 24)

| Ort | Schlüssel | Bedeutung | Fehlt = |
|---|---|---|---|
| `steps[].chapter` | `level` | `2` = Unterkapitel (nur nach einem Kapitel wirksam) | Kapitel |
| `steps[].chapter` | `hidden` | `true` = Kapitel samt seinen Schritten (bis zum nächsten Kapitel gleicher oder höherer Ebene) nicht in der Anleitung | sichtbar |
| `inserted[]` | `type` | `"text"` = Textabschnitt: Überschrift und Text ohne Nummer und ohne Bild | nummerierter Schritt |
| `guide.doc` | `numbering` | zusätzlich `perChapter` (je Kapitel ab 1) | `chapter` |

**Überschriftenregel** (genehmigte Korrektur, [13](13-handbuch-fahrplan.md)): Eine Kapitelüberschrift steht vor dem ersten *gezeigten* Schritt ihres Bereichs und entfällt nur, wenn der Bereich keinen gezeigten Schritt hat. Bis 0.13 ging sie verloren, sobald ihr erster Schritt ausgeblendet, ein Nebenschritt oder Mitglied einer Zusammenfassung war. Zählung mit Unterkapiteln: Kapitel „2“, Unterkapitel „2.3“ (teilt den Zähler der zweiten Ebene mit losen Schritten), Schritte darin „2.3.1“; Sprungmarken `k2`, `k2-3`, Textabschnitte `t1`.

| Nr. | Anforderung | Priorität | Abnahme |
|---|---|---|---|
| DAT-086 | Ohne `level`, `hidden`, `type` und `perChapter` bleibt jede Ausgabe gleich (bis auf die Überschriftenregel). | Muss | `LegacyOutputTests`, `OutlineTests` |

### 13.21 Bildunterschriften, Warnhinweise, Hinweise je Sprache (Paket 25)

| Ort | Schlüssel | Bedeutung | Fehlt = |
|---|---|---|---|
| `steps[]` | `caption`, `alt` | `{ "de": "…", "en": "…" }` – eigene Bildunterschrift, Alternativtext (fehlt die Sprache: Deutsch) | Überschrift des Schritts |
| `guide.doc` | `captions` | `none` \| `auto` („Abb. n: Überschrift“) \| `custom` (eigener Text, sonst Überschrift) | `none` |
| `guide.doc` | `figures`, `warnFirst` | Abbildungsverzeichnis am Ende; Warnhinweise vor dem Schritttext | aus |
| `comments[]` | `signal` | `danger` \| `warning` \| `caution` \| `notice` – nur zusammen mit `kind: "warning"`; Kennzeichnung „GEFAHR:“, „WARNUNG:“, „VORSICHT:“, „HINWEIS:“ | gewöhnlicher Kasten |
| `comments[]` | `i18n.<lang>.text`, `i18n.<lang>.fmt` | Text des Hinweises in dieser Sprache | Grundtext |

`signal` statt neuer `kind`-Werte: `kind` ist schutzrelevant (§15.7); ältere Versionen zeigen einen Warnhinweis als „Achtung:“. Neue `guide.doc`-Schlüssel schreibt der Editor nur, wenn sie vom Standard abweichen.

| Nr. | Anforderung | Priorität | Abnahme |
|---|---|---|---|
| DAT-087 | Ohne `captions`, `signal` und `i18n` an Hinweisen ist jede Ausgabe gleich. | Muss | `LegacyOutputTests`, `FiguresTests` |

### 13.22 Impressum, Änderungsverzeichnis, Glossar (Paket 26)

| Ort | Form | Fehlt = |
|---|---|---|
| `guide.imprint` | `{ "text": "…", "fmt": 1?, "i18n": { "en": { "text", "fmt" } } }` | kein Abschnitt |
| `guide.revisions[]` | `{ "id", "version", "date": "YYYY-MM-DD", "author", "text", "i18n": { "en": { "text" } } }` | kein Abschnitt |
| `guide.glossary[]` | `{ "id", "term", "text", "i18n": { "en": { "term", "text" } } }` | kein Abschnitt |
| `guide.doc` | `imprint`, `revisions`: `front` (Standard) \| `back` \| `off`; `glossary`: `false` schaltet ab | vorn bzw. an |

Reihenfolge in der Anleitung: Titelseite · Impressum und Änderungen (bei `front`) · Inhaltsverzeichnis · Einleitung · Schritte · Abschluss · Abbildungsverzeichnis · Impressum und Änderungen (bei `back`) · Glossar (alphabetisch, als Tabelle). Alle Texte laufen über Variablen, Schwärzung und Verallgemeinerung. Der Editor trägt nie von selbst einen Benutzer- oder Rechnernamen ein; „Eintrag mit heutigem Datum“ setzt nur Version und Datum. Die Störungsbehebung ist ein Textabschnitt mit Tabelle (Problem | Mögliche Ursache | Abhilfe), siehe §13.20.

| Nr. | Anforderung | Priorität | Abnahme |
|---|---|---|---|
| DAT-088 | Impressum, Änderungsverzeichnis und Glossar erscheinen nur, wenn Daten da sind, an der gewählten Stelle und je Sprache; ein geschützter Wert darin erscheint nicht. | Soll | `PartsTests` |

### 13.19 Markenstil (`guide.auto.style`)

Aussehen der automatischen Mausmarken in Editor und Anleitungen ([13](13-handbuch-fahrplan.md) Paket 22). Fehlt der Block, gilt das bisherige Aussehen („Klassisch“: roter Ring mit Fadenkreuz, blauer Rahmen der Stärke 3).

| Feld | Werte | Bedeutung |
|---|---|---|
| `preset` | `numbers`, `subtle`, `bold`, `custom` | Name des gewählten Stils (nur zur Anzeige) |
| `click` | `ring` (Standard), `dot`, `target`, `none` | Klickmarke: Ring mit Fadenkreuz · kleiner Punkt · kräftiger Doppelring · keine |
| `clickColor`, `targetColor`, `numberColor` | `#RRGGBB` | Farben von Klickmarke, Zielrahmen und Schrittnummer; ungültige Werte gelten als fehlend |
| `targetWidth` | 1–8 (Standard 3) | Stärke des Zielrahmens |
| `numberShape` | `circle` (Standard), `rounded`, `square` | Form der Schrittnummer |

Mitgelieferte Stile: **Klassisch** (kein Block), **Nummern** (`click: none`, `stepNumber: true`), **Dezent** (`click: dot`, `targetWidth: 1.5`), **Kräftig** (`click: target`, `targetWidth: 5`). Die Schrittnummer ist dieselbe Zeichenkette wie in der Anleitung (Zählweise und Kapitel aus `guide.doc`); sie steht an der Klickstelle (bei sichtbarer Klickmarke rechts oberhalb), beim Ziehen am Startpunkt, bei Tastaturschritten oben links am Zielrahmen. KI-Paket und MCP verwenden den Stil nie.

| Nr. | Anforderung | Priorität | Abnahme |
|---|---|---|---|
| DAT-085 | Neue Markenschlüssel (`stepNumber`, `guide.auto.style`, `annotations[].auto/start`, neue `style`-Schlüssel) sind optional; ohne sie ist jedes Bild pixelgleich zu vorher. | Muss | `MarkStyleTests`: ohne Schalter ändert eine übergebene Nummer kein Pixel; Markierstift ohne `hue` bleibt gelb; KI-Bild mit eingeschalteter Nummer gleicht dem ohne |

## 14. `cache/`, `history/` und `export/`

**`cache/`** enthält ausschließlich Daten, die sich aus der Sitzung wiederherstellen lassen. Der Ordner darf jederzeit gelöscht werden und wird nicht in `.steprec` gepackt.

| Pfad | Inhalt |
|---|---|
| `cache/thumbs/<Bild-Kennung>-<Kantenlänge>.jpg` | Vorschaubilder |
| `cache/full/<Bild-Kennung>.png` | Wiederhergestellte Vollbilder |
| `cache/journal.idx` | Sprungtabelle `seq` → Dateiposition und Zeitindex |
| `cache/catalog.json` | Teure Angaben für die Bibliothek: Zahl der ungeprüften Eingaben und der Anleitungsschritte. Felder: `stamp` (Revision von `edits.json`, letzte `seq`, Regelversion, getrennt durch `\|`), `journalBytes` (Länge von `events.jsonl`), `unreviewed`, `guideSteps`. Gilt nur, solange Revision und Journallänge zum Stand der Sitzung passen. |
| `cache/thumbs/list-<Breite>.png` | Vorschaubild der Bibliothek und des Startfensters: Vorher-Bild des ersten Anleitungsschritts **über die Leseschicht**, also mit allen Schwärzungen (Standardbreite 192). Daneben `list-<Breite>.png.json` mit `journalBytes` und `revision` als Stand. |
| `cache/search/` | Suchindex der Sitzung, siehe unten |

`cache/` enthält dieselben Pixel und Texte wie die Rohdaten und ist ebenso vertraulich. Nach endgültigem Schwärzen, nach jeder Neuberechnung der Schritte und bei jedem Speichern von `edits.json` (auch beim Umbenennen) wird der Ordner geleert; wer ihn braucht, baut ihn neu. Aufnahmen, die noch laufen, und Archive (`.steprec`, direkt gelesen) bekommen keinen Zwischenspeicher.

<a id="suchindex"></a>
**Suchindex `cache/search/`**

Der Index macht die Sitzung für die **Volltextsuche über alle Sitzungen** auffindbar – in der Sitzungsbibliothek und für die Suche des MCP-Servers über einen ganzen Ordner. Jede Sitzung hat ihren eigenen Index; eine Datei über mehrere Sitzungen gibt es nicht.

| Datei | Inhalt |
|---|---|
| `cache/search/meta.json` | Formatversion und der Stand der Sitzung, aus dem der Index entstand |
| `cache/search/entries.jsonl` | Die Einträge, einer je Zeile |

Felder von `meta.json`:

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `format` | string | ● | Konstant `"steprec.search"`. |
| `version` | int | ● | Version des Indexformats, hier `1`. Ein Leser, der die Version nicht kennt, verwirft den Index und baut ihn neu. |
| `stamp` | string | ● | Stand der Sitzung, aus dem der Index entstand: Indexversion, `revision` aus `edits.json` (`0` ohne Datei), Länge von `events.jsonl` in Bytes, Regelversion der Schritte und Sprache, getrennt durch `\|` (`1\|7\|482113\|1.5.0\|de`). Ein Index mit anderem Stand gilt als veraltet. |
| `sessionId` | string | ● | Zugehörige Sitzung. |
| `entries` | int | ● | Zahl der Einträge. |

Je Zeile in `entries.jsonl`:

| Feld | Typ | Pfl. | Bedeutung |
|---|---|---|---|
| `s` | string | ● | Kennung des Schritts (`s1734`). |
| `n` | int | ● | Schrittnummer in der wirksamen Schrittfolge. |
| `a` | int | ● | Anker des Schritts. |
| `t` | int | ● | Zeit des Schritts (µs seit Aufnahmebeginn). |
| `f` | enum | ● | Art des Texts: `stepText` (Kurzfassung, Protokollsatz und vom Autor gesetzter Schritttext), `elements` (Name des Ziel-Elements), `windows` (Fenstertitel), `typed` (getippter Text, nur bei durchgesehenen Eingaben), `comments` (Kommentar des Autors), `markers` (Bezeichnung eines Markers). |
| `x` | string | ● | Der Text – so, wie ihn die Leseschicht ausgibt. |
| `m` | enum | ○ | Bei `markers`: der Typ des Markers (`note`, `error`, `expected`, `important`). |

Ziel-Angaben und Titel der Sitzung stehen nicht im Index; die Suche findet sie nicht.

Regeln:

1. **Abgeleitet.** Der Index lässt sich jederzeit neu erzeugen. Fehlt er oder ist er veraltet, wird er bei der nächsten Suche gebaut und – bei abgeschlossenen Sitzungen in beschreibbaren Ordnern – gespeichert; bei Archiven (`.steprec`) und laufenden Aufnahmen entsteht er nur im Arbeitsspeicher bzw. gar nicht. Kein Leser verlässt sich auf sein Vorhandensein. Die Bibliothek im Viewer nutzt ihn; der MCP-Server baut die Einträge je Aufruf aus der Leseschicht (dieselbe Definition, `SessionSearch.Entries`).
2. **Nur über die Leseschicht.** Jeder Eintrag ist ein Text, den die Leseschicht in diesem Moment auch an den MCP-Server ausgeben würde. In den Index kommen deshalb **nie**: geschwärzte Inhalte, maskierte Eingaben (ihr Inhalt existiert nicht), ungeprüfte auffällige Eingaben, interne und ausgeblendete Kommentare, Texte aus verworfenen Abschnitten und die Originalwerte von Verallgemeinerungen – dort steht der Platzhalter. Auch die Platzhalter der Leseschicht selbst (`[geschwärzt]`, `{unreviewed:N}`, `{masked:N}`) werden nicht aufgenommen.
3. **Verwerfen.** Ändert sich etwas, das die Sicht der Leseschicht bestimmt, wird der Ordner `cache/search/` gelöscht, **bevor** die Änderung gespeichert ist: Schwärzungen, Verallgemeinerungen, der Stand der Durchsicht, Art und Sichtbarkeit von Kommentaren, Ziel-Angaben, Ersetzungen, verworfene Abschnitte. Zusätzlich gilt ein Index als veraltet und wird nicht benutzt, wenn `stamp` nicht zum Stand der Sitzung passt – andere `revision` von `edits.json`, andere Länge des Journals, andere Regelversion oder andere Sprache.
4. **Vertraulich wie eine Ausgabe.** Der Index liegt in `cache/`, wird nie in `.steprec` gepackt und nach endgültigem Schwärzen mit dem ganzen Ordner geleert.
5. **Kein Austauschformat.** Der Aufbau gilt für `version` 1. Neue Arten in `f` und neue Felder dürfen mit einer höheren Version hinzukommen; ein Programm darf daneben eigene Hilfsdateien im selben Ordner führen, die denselben Regeln unterliegen.

```json
{"s":"s1700","n":6,"a":1700,"t":81290000,"f":"typed","x":"notiz.txt"}
{"s":"s1734","n":7,"a":1734,"t":83412000,"f":"elements","x":"Speichern"}
{"s":"s1734","n":7,"a":1734,"t":83412000,"f":"windows","x":"Speichern unter"}
{"s":"s352","n":3,"a":352,"t":13180000,"f":"comments","x":"Der Text ist beliebig. Wichtig ist der Speichervorgang ab Schritt 5."}
```

<a id="history"></a>
**`history/` – Bearbeitungsstände**

Der Viewer sichert `edits.json` automatisch, damit sich ein früherer Stand der Bearbeitung zurückholen lässt. Die Stände sind Kopien des Overlays, kein eigenes Format.

| Punkt | Festlegung |
|---|---|
| Dateiname | `edits-<UTC>-r<Revision>-<Anlass>.json`. `<UTC>` hat die Form `JJJJMMTTThhmmssfffZ` (Millisekunden), `<Revision>` ist die `revision` der gesicherten Datei, `<Anlass>` besteht aus bis zu 20 Buchstaben und Ziffern. Beispiel: `edits-20261112T093012345Z-r17-auto.json`. |
| Anlass | `open` (erstes Speichern nach dem Öffnen der Sitzung), `auto` (alle späteren), `replaceAll` („Alle ersetzen“), `cropAll` („Zuschnitt auf alle“), `template` („Vorlage anwenden“), `restore` (vor dem Wiederherstellen). |
| Inhalt | Der Stand von `edits.json` **vor** dem Speichern, das ihn ersetzt, Byte für Byte als lesbares JSON. |
| Zeitpunkt | Beim ersten Speichern nach dem Öffnen; danach höchstens alle 10 Minuten seit dem jüngsten Stand; erzwungen vor „Alle ersetzen“, „Zuschnitt auf alle“, „Vorlage anwenden“ und Wiederherstellen. Eine Revision wird nur einmal gesichert (kein doppelter Stand), und eine Datei ohne Revision (nie bearbeitet) wird nicht gesichert. Geschrieben wird nur bei Sitzungen, die nicht schreibgeschützt sind. |
| Grenzen | Höchstens **40 Stände** und **15 MB** zusammen; die ältesten werden zuerst gelöscht. |
| Fehler | Schlägt das Sichern fehl, wird das Speichern nicht aufgehalten; der Fehler steht im Protokoll des Programms. |
| Wiederherstellen | Eine ganz normale Bearbeitung: Die Revision steigt weiter, und „Rückgängig“ macht sie rückgängig. Die Kopfdaten (`format`, `schemaVersion`, `sessionId`, `revision`, `savedAtUtc`, `savedBy`) bleiben die von heute. Standardmäßig bleiben außerdem `redactions`, `substitutions` und `review` auf dem heutigen Stand: **Ein alter Stand macht keinen Schutz rückgängig.** Wer im Fenster „Bearbeitungsstände“ das Kästchen „… wie jetzt behalten“ abwählt, holt sie ausdrücklich mit zurück. Das Fenster zeigt je Stand eine Zusammenfassung und den Unterschied zum jetzigen Stand; ein Stand ohne Unterschied lässt sich nicht wiederherstellen. Ein geöffnetes `.steprec`-Archiv hat keine Stände. |

Schutzregeln:

- `history/` ist so vertraulich wie `edits.json` (ältere Stände enthalten Verallgemeinerungsregeln mit den Originalwerten und Texte, die später geschwärzt wurden). Der Ordner wird **nie** in `.steprec` gepackt, nie exportiert und nie in eine bereinigte Kopie übernommen.
- **Endgültiges Schwärzen löscht alle Stände**, denn ein alter Stand könnte Geschwärztes noch beschreiben.
- Der Ordner gehört nicht zum Datenmodell: Kein Leser verlässt sich auf ihn, und die Bibliothek überspringt ihn beim Suchen nach Aufnahmen.

**`export/`** nimmt erzeugte Ausgaben auf (empfohlene Unterordner: `ki-paket/`, `html/`, `markdown/`). Exporte sind eigenständige Erzeugnisse mit angewendeten Schwärzungen; ihr Aufbau ist im [KI-Export](06-ki-export-und-mcp.md) und im [Anleitungs-Export](09-anleitungs-export.md) beschrieben. Dateinamen wie `schritt007-vorher.png` gibt es nur dort – im Sitzungsordner gelten allein Bild-Kennungen.

**`export/.tmp/`** nimmt die Zwischendateien auf, die das Tool einem anderen Programm als Datei übergeben muss oder die beim Erzeugen einer Ausgabe entstehen: das Bild für „extern bearbeiten“, die Datei hinter „als Datei kopieren“ und dem Herausziehen, die Exportvorschau, unfertige Exporte ([Viewer](08-viewer.md), [Anleitungs-Export](09-anleitungs-export.md)).

- Zwischendateien liegen **ausschließlich hier** – nie in `%TEMP%` und an keinem anderen Ort außerhalb des Sitzungsordners. Kann dort nicht geschrieben werden (schreibgeschütztes Medium, direkt geöffnetes Archiv), stehen die Funktionen, die eine Zwischendatei brauchen, nicht zur Verfügung; ausgewichen wird nicht.
- Sie enthalten nur die Sicht der Leseschicht: Schwärzungen und der Stand der Durchsicht sind angewendet.
- Der Ordner ist flüchtig. Er wird beim Schließen der Sitzung geleert; Reste eines Absturzes räumt das nächste Öffnen der Sitzung auf. Kein Leser verlässt sich auf seinen Inhalt.
- Ändern sich Schwärzungen oder der Stand der Durchsicht, wird er wie `cache/` geleert.
- Er wird nie in `.steprec` gepackt und geht in keine Prüfsumme ein.

---

<a id="zustandsdateien"></a>
## 14a. Zustandsdateien außerhalb der Sitzung

Zwei Dateien gehören nicht zu einer Sitzung, sondern zum Programm auf diesem Rechner: der Oberflächenzustand und die Anfragen von KI-Agenten. Sie liegen im **Zustandsordner** (`StateDir`). Das ist der Ordner neben der EXE, wenn das Programm tragbar läuft (`StepRecorder.settings.json` liegt dort oder der EXE-Ordner ist beschreibbar), sonst `%LOCALAPPDATA%\StepRecorder`. Die Einstellungen liegen davon getrennt ([10](10-datenschutz-und-sicherheit.md)); in den Zustandsdateien stehen keine Vorlieben und keine aufgezeichneten Inhalte. Alle Zustandsdateien werden atomar ersetzt; fehlt eine, beginnt das Programm ohne sie.

<a id="zustandsdatei-der-oberfläche"></a>
### 14a.1 Oberflächenzustand `StepRecorder.state.json`

Flüchtiger Zustand der Oberfläche: Fensterlagen, zuletzt gewählte Schritte, letzter Aufnahmebereich. Nur die Oberfläche schreibt; Kommandozeile und MCP-Server lesen höchstens.

| Feld | Typ | Bedeutung |
|---|---|---|
| `version` | int | Formatversion, hier `1`. |
| `lastStopped`, `atUtc` | string, Zeit | Ordner der zuletzt beendeten Aufnahme und Zeitpunkt. `record --stop` erkennt daran, dass die Aufnahme beendet ist (am Zeitstempel, nicht an einer Prozess-Kennung). |
| `windows` | Objekt | Fensterlage je Fenster (Start, Editor …): `x`, `y`, `w`, `h` in physischen Pixeln, `max` (maximiert), `dpi` beim Speichern. Beim Öffnen wird die Lage an die vorhandenen Monitore und ihre Skalierung angepasst. |
| `viewer` | Objekt | Spaltenbreiten (`left`, `right`) und zuletzt gewählter Reiter (`tab`) des Editors. |
| `export` | Objekt | `lastFormat` und je Format (`formats.<Format>`) die zuletzt gewählten Optionen und der Zielordner. |
| `library` | Objekt | Sortierung (`sort`), Ansicht (`mode`) und Ordner für gepackte Aufnahmen (`packFolder`). |
| `lastRegion` | Objekt | Zuletzt gewählter Aufnahmebereich: `x`, `y`, `w`, `h` in physischen Pixeln des virtuellen Bildschirms, dazu der Monitor (`device`, `monX`, `monY`, `monW`, `monH`) und `dpi`. Beim Wiederverwenden ist der Bereich exakt, verschoben, beschnitten oder verloren. |
| `sessions` | Objekt | Merkzettel je Aufnahme, nach `sessionId`: `at` (zuletzt benutzt), `step` (zuletzt gewählter Schritt), `all` (Filter „Alle“), `display` (Bildansicht), `zoom`, `export` (letzter Export dieser Aufnahme). Höchstens 50 Einträge; die am längsten unbenutzten werden verworfen. |
| `requests` | Objekt | Verlauf der Agent-Anfragen: `recent` (Zeitpunkte der gezeigten Anfragen der letzten Stunde) und `blockedUntil` (Ende der Sperre „15 Minuten keine Anfragen“). Siehe [06](06-ki-export-und-mcp.md#1910-request_recording-und-get_recording_request-nur-mit---allow-requests). |

Regeln:

- **Tolerant lesen, Unbekanntes bewahren:** Unbekannte Schlüssel bleiben beim Schreiben erhalten. Ist die Datei nicht lesbar, werden nur `lastStopped` und `atUtc` gerettet; alles andere beginnt leer.
- **Keine Inhalte, keine Ausgabesteuerung:** Der Zustand enthält Pfade und Kennungen, aber weder Texte noch Bilder aus Aufnahmen. Er bestimmt nie, was ein Export oder der MCP-Server ausgibt. Der einzige Schutz, der hier liegt, ist der Missbrauchsschutz für Agent-Anfragen (`requests`); er schützt vor einem Agenten, nicht vor dem Besitzer des Rechners.

<a id="anfrage-und-statusdateien"></a>
### 14a.2 Anfrage- und Statusdateien `requests/`

Fragt ein KI-Agent über den MCP-Server um eine Vorführung an ([06](06-ki-export-und-mcp.md#1910-request_recording-und-get_recording_request-nur-mit---allow-requests)), laufen Anfrage und Antwort über zwei Dateien je Anfrage im Ordner `requests/` des Zustandsordners. Zwei Dateien, weil jede nur einen Schreiber hat.

| Datei | Schreiber | Inhalt |
|---|---|---|
| `rq-<Kennung>.request.json` | MCP-Server | `id`, `goal`, `expected` ○, `title` ○, `root` (Aufnahmeordner des Servers, vom Menschen eingerichtet), `createdUtc`, `expiresUtc` (120 s nach dem Anlegen). |
| `rq-<Kennung>.status.json` | Oberfläche | `id`, `state`, `sessionId` ○ (bei `finished`), `reason` ○, `atUtc`. |

Regeln:

- **Kennung:** `rq-` und zwölf Kleinbuchstaben oder Ziffern (Hexadezimal, zufällig). Alles andere ist keine gültige Kennung; ein Tool, das eine Kennung nimmt, prüft das Muster, bevor es einen Pfad bildet.
- **Text des Agenten ist ungeprüft.** `goal`, `expected` und `title` werden beim Anlegen bereinigt: keine Steuerzeichen außer dem Zeilenumbruch, keine Zeichen zur Schreibrichtung oder unsichtbaren Leerzeichen, mehr als zwei Zeilenumbrüche in Folge zusammengefasst; `title` eine Zeile. Längen: `goal` 10 bis 500, `expected` bis 500, `title` bis 120 Zeichen. Die Texte werden nur angezeigt und – nach Zustimmung – als Ziel der Aufnahme mit `statedBy: "agent"` übernommen ([4.7](#manifest-goal)); sie werden nie ausgeführt.
- **Zustände** (`state`): `pending` (ohne Statusdatei, solange die Frist läuft), `declined`, `expired`, `recording`, `finished`, `discarded`, `failed`. Fehlt die Statusdatei und ist `expiresUtc` überschritten, gilt `expired`. Abgeschlossene Zustände (`declined`, `expired`, `finished`, `discarded`, `failed`) werden nie überschrieben; aus `recording` führt der Weg nur zu `finished`, `discarded` oder `failed`.
- **`reason`:** bei `declined` `user`, `blocked`, `busy`, `tooSoon` oder `hourlyLimit`; bei `failed` `notDelivered` oder `startFailed`.
- **Zustellung:** Der Server meldet die Kennung der laufenden Oberfläche über deren Fensterkanal (`--recording-request <Kennung>`). Läuft keine Oberfläche, entsteht keine offene Anfrage.
- **Aufräumen:** Dateien, die älter als ein Tag sind, werden beim Eingang der nächsten Anfrage gelöscht. Die Aufnahme selbst liegt im Aufnahmeordner und hängt nicht von diesen Dateien ab.

---

<a id="versionierung"></a>
## 15. Versionierung und Migration

### 15.1 Welche Versionen es gibt

| Version | Wo sie steht | Was sie beschreibt | Wann sie steigt |
|---|---|---|---|
| `schemaVersion` (`MAJOR.MINOR`) | `manifest.json`, `session.start`, `steps.json`, `edits.json` | Aufbau der Dateien | MINOR bei abwärtskompatibler Erweiterung, MAJOR bei einem Bruch |
| `stepBuilderVersion` (`MAJOR.MINOR.PATCH`) | `steps.json`, `manifest.rules` | Regeln der Schritterkennung | siehe [Regel-Versionierung](05-schritterkennung.md#regelversion) |
| Vorlagenversion | nur im Programm | Textvorlagen | ohne Einfluss auf gespeicherte Daten |
| `recorder.version` | `manifest.json`, `session.start` | Programmversion | rein informativ |

### 15.2 Was eine MINOR-Erweiterung darf

Erlaubt: neue optionale Felder, neue Ereignistypen, neue Aufzählungswerte (auch neue `system.kind` und `gap.cause`), neue Dateien im Sitzungsordner.

Nicht erlaubt: die Bedeutung, Einheit oder den Typ eines bestehenden Felds ändern; ein Pflichtfeld entfernen; die Bedeutung eines bestehenden Aufzählungswerts ändern. Dafür ist eine neue MAJOR-Version nötig.

### 15.3 Verhalten eines Lesers

| Datei gegenüber Programm | Verhalten |
|---|---|
| gleiche MAJOR, MINOR kleiner oder gleich | Vollständig lesen und schreiben. |
| gleiche MAJOR, MINOR größer | Lesen; Unbekanntes ignorieren (G6) und darauf hinweisen. `edits.json` und `manifest.json` nur schreiben, wenn unbekannte Felder dabei erhalten bleiben – sonst schreibgeschützt öffnen. |
| MAJOR kleiner | Lesen über eine Migration im Arbeitsspeicher; Schreiben erst nach Migration in eine Kopie. |
| MAJOR größer | Nicht öffnen; die nötige Programmversion nennen. |

### 15.4 Unbekanntes bewahren

- Wer `manifest.json` oder `edits.json` neu schreibt, übernimmt unbekannte Felder unverändert.
- Journalzeilen unbekannten Typs werden bei Wiederherstellung, endgültigem Schwärzen und Migration **wörtlich** übernommen.
- Erweiterungen Dritter verwenden Feldnamen mit `x_` und Typen mit `x.`; sie unterliegen denselben Regeln.

### 15.5 Migration

- Eine Migration arbeitet **nie am Original**. Sie erzeugt eine Kopie der Sitzung in der neuen Formatversion; `sessionId` bleibt gleich, `manifest.history` erhält den Eintrag `migrated`.
- Jede Migration ist deterministisch und je Versionsschritt dokumentiert. Blobs werden unverändert übernommen.
- `steps.json` wird bei einer Migration nicht umgeschrieben, sondern neu abgeleitet.

### 15.6 Festgehaltene Regelversion

Eine Sitzung behält ihre `steps.json`, bis der Nutzer ausdrücklich „Schritte neu berechnen“ wählt. Ein neueres Programm zeigt eine ältere Sitzung also so, wie sie abgeleitet wurde – dafür genügt die gespeicherte `steps.json`, alte Regeln müssen nicht mitgeliefert werden. Bei einer Neuberechnung:

1. Die bisherige Datei wird als `steps.<alte Regelversion>.json` aufbewahrt.
2. Die neue `steps.json` entsteht aus dem unveränderten Journal.
3. Einträge in `edits.json` werden über ihre Anker neu zugeordnet; nicht Zuordenbares wandert nach `orphans`.
4. `cache/` wird geleert, `manifest.rules` und `manifest.history` werden aktualisiert.

<a id="erweiterungen"></a>
### 15.7 Erweiterungen und Schutzrelevantes

Die folgenden Angaben sind **Erweiterungen im Sinn von 15.2**: neue optionale Felder, neue Aufzählungswerte, ein neuer Ordner im Cache. Sie gehören zur Formatversion 1.0, die noch nicht veröffentlicht ist; `schemaVersion` bleibt deshalb `"1.0"`. Für sie gilt dasselbe, was für jede spätere MINOR-Erweiterung gilt: **Eine Sitzung ohne sie ist vollständig gültig**, und fehlt eine Angabe, gilt ihr Standard.

| Erweiterung | Wo | Fehlt = |
|---|---|---|
| Ziel der Aufnahme | `manifest.goal`, `session.start.goal`, `edits.goal` | keine Ziel-Angaben |
| Aufnahmeprofil | `manifest.profile`, `session.start.profile` | ohne Profil aufgenommen |
| Marker mit Typ | `marker.kind` mit `note`, `error`, `expected`, `important`; `input.markerKind`; `steps[].markerKind` | `note` |
| Zweck eines Abschnitts | `session.start.purpose`, `replaces`; `parts[].purpose`, `replaces`, `firstSeq`, `lastSeq` | `initial` bzw. `append` |
| Ersetzungen | `edits.replacements[]` | nichts ersetzt |
| Verworfene Abschnitte | `edits.sections[]`; `session.stop.reason: "discarded"`; `parts[].discarded` | nichts verworfen |
| Abschnittsweise Angaben | `manifest.performance[]`, `session.stop.performance`; `deviceSalt` in `session.start` und Manifest | keine Zähler; ohne `deviceKey` kein Salz nötig |
| Schrittsicht der Leseschicht | `state`, `number` – nur in Ausgaben, nie in der Sitzung | – |
| Verallgemeinerungen | `edits.substitutions[]`; `substituted` am Ereignis; `key: "Substituted"`, `placeholder`; Token `{ "placeholder": … }`; `masks[].why: "substituted"` | nichts verallgemeinert |
| Ignorierte Fundstellen | `edits.qualityIgnores[]` | nichts ignoriert |
| Suchindex | `cache/search/` | wird bei Bedarf erzeugt |
| Programm-Kontext und mitgelesene Inhalte (Pakete 10–13) | `process.*`, `window.hang`, `eventlog.entry`, `ui.snapshot`, `log.*`, `clipboard.content`; `manifest.requires`; `edits.review.inputs[].key`; Kommentarart `agent` | keine solchen Ereignisse |

Eine Umbenennung ist darunter: Der Wert `marker` von `marker.kind` heißt jetzt `note`. Leser nehmen beide an.

**Schutzrelevantes wird nie ignoriert.** Der tolerante Leser (G6) darf Unbekanntes überlesen – mit einer Ausnahme: Bereiche von `edits.json`, die bestimmen, **was eine Ausgabe zeigen darf**. Das sind `redactions`, `substitutions`, `review`, `replacements`, `sections` sowie `kind` und `hidden` der Kommentare. Trifft ein Leser dort auf etwas, das er nicht versteht – eine unbekannte Art von Schwärzung, eine unbekannte Form von `match`, einen unbekannten Wert von `scope` oder `targets`, ein Feld, dessen Namen er nicht kennt (Erweiterungen Dritter mit `x_` ausgenommen) –, dann gilt:

- Er erzeugt **keine Ausgabe** aus dieser Sitzung: keinen Export, keine Kopie, keine Antwort des MCP-Servers, keinen Suchindex.
- Der Viewer darf die Sitzung dem Autor anzeigen, weist auf den Grund hin und nennt die nötige Programmversion.
- `validate` meldet den Fall als Fehler der Prüfung F15.

Ein älteres Programm kann so nie eine Schutzmaßnahme übergehen, die ein neueres angelegt hat.

---

<a id="absturzsicherheit"></a>
## 16. Absturzsicherheit und Integrität

### 16.1 Schreibregeln

1. **Manifest zuerst.** Beim Aufnahmestart entsteht `manifest.json` mit `state: "recording"` und die Sperrdatei `session.lock` – bevor das erste Ereignis geschrieben wird.
2. **Journal regelmäßig sichern.** Geschriebene Zeilen erreichen das Betriebssystem spätestens nach 250 ms. Bei jedem `session.checkpoint` (mindestens alle 30 s), vor jeder Pause und beim Stopp werden die Daten dauerhaft auf den Datenträger geschrieben.
3. **Blob vor Ereignis.** Siehe [Bildspeicher](#bildspeicher).
4. **Ganze Dateien atomar ersetzen.** `manifest.json`, `steps.json` und `edits.json` werden in eine vorläufige Datei geschrieben, dauerhaft gesichert und dann in einem Schritt an die Stelle der alten Datei gesetzt. Es gibt nie eine halb geschriebene Fassung.
5. **Notreserve.** Während der Aufnahme liegt im Sitzungsordner eine Reservedatei `session.reserve` (Standard 8 MiB). Ist der Datenträger voll, wird sie gelöscht, damit die abschließenden Ereignisse und das Manifest noch geschrieben werden können. Sie ist kein Teil der Sitzung.
6. **Schwärzungen aus der Aufnahme.** Beim Stopp werden Schwärzungen mit `trigger: "lastInput"` endgültig angewendet – nachdem `session.stop` geschrieben und bevor das Manifest abgeschlossen ist ([13.6](#redact-last-input)). Das Journal wird dabei als ganze Datei atomar ersetzt (wie in Regel 4); Prüfkette und Prüfsummen entstehen neu. Bis dahin steht im Manifest weiter `state: "recording"`, sodass ein Abbruch mittendrin als Absturz erkannt und nachgeholt wird.

### 16.2 Prüfkette

Jedes `session.checkpoint` trägt in `chain` einen fortgeschriebenen Hash über das Journal:

```
chain(0) = 32 Nullbytes
chain(n) = SHA-256( chain(n−1) ‖ Bytes aller Zeilen nach dem Ende des vorigen abgedeckten Bereichs
                                 bis unmittelbar vor der Zeile des n-ten Checkpoints, jeweils mit Zeilenvorschub )
```

Der Bereich eines Checkpoints beginnt mit der Zeile des vorigen Checkpoints (sodass auch dessen Inhalt abgedeckt ist) und endet vor der eigenen Zeile; `lines` nennt die Zeilenzahl. Der letzte Checkpoint vor `session.stop` wird über die Prüfsumme des Segments im Manifest abgedeckt.

Die Kette erkennt abgeschnittene, beschädigte und versehentlich veränderte Journale. Sie ist **keine Signatur**: Wer die Datei absichtlich ändert, kann die Kette neu berechnen. Nach endgültigem Schwärzen wird sie neu berechnet und der Vorgang in `manifest.history` vermerkt.

### 16.3 Voller Datenträger

Sinkt der freie Speicher unter die Warnschwelle, schreibt der Recorder `system` (`kind: "disk"`, `state: "low"`). Ist kein Platz mehr, folgen – nach Freigabe der Notreserve – in dieser Reihenfolge: `system` (`state: "full"`), `gap` (`phase: "begin"`, `cause: "diskFull"`), `session.pause` (`reason: "diskFull"`), `session.checkpoint`. Die Aufnahme bleibt angehalten, bis wieder Platz ist oder der Nutzer stoppt. Eingaben werden währenddessen nicht aufgezeichnet; die Lücke weist das aus.

### 16.4 Wiederherstellung nach Absturz

Eine Sitzung gilt als abgestürzt, wenn `manifest.state` `recording` ist und kein laufender Prozess sie laut `session.lock` hält. Die Wiederherstellung

1. liest das Journal Zeile für Zeile und hält bei der ersten Zeile an, die kein gültiges JSON-Objekt ist oder die `seq`-Folge bricht (`lastValidSeq`);
2. schneidet die Datei hinter der letzten gültigen Zeile ab (`discardedBytes` – höchstens eine unvollständige Zeile);
3. prüft die Prüfkette bis zum letzten Checkpoint (`chainOk`);
4. hängt an: `gap` mit `phase: "begin"`, `cause: "crash"` und `tFrom` = Zeit des letzten gültigen Ereignisses; `session.recovered`; `session.stop` mit `reason: "recovered"`. Endet das Journal bereits mit `session.stop` – der Abbruch fiel in den Abschluss der Aufnahme –, entfallen Lücke und zweites `session.stop`;
5. holt ausstehende Schwärzungen aus der Aufnahme nach: Für jeden `marker` mit `kind: "redactLastInput"`, dessen Zeitraum noch ungekürzte Zeilen enthält, wird der Zeitraum endgültig geschwärzt ([13.6](#redact-last-input)) – auch dann, wenn `edits.json` fehlt oder den Eintrag nicht enthält;
6. baut `manifest.json` aus dem Journal neu auf (G3), setzt `state: "recovered"` und ergänzt `history`;
7. leitet `steps.json` ab.

Gültige Zeilen werden dabei nie entfernt und – mit der einen Ausnahme aus Schritt 5 – nie verändert. Verloren ist höchstens, was zwischen der letzten Sicherung (Regel 2) und dem Absturz geschah. Vor dem Abschluss von Schritt 5 gibt kein Leser Inhalte der Sitzung aus.

### 16.5 Formatprüfungen

Der Befehl `validate` ([Architektur](02-architektur.md)) prüft eine Sitzung gegen dieses Dokument. Die Prüfungen auf Schritt-Ebene stehen in der [Vollständigkeitsprüfung](05-schritterkennung.md#vollstaendigkeitspruefung); auf Format-Ebene sind es:

| Code | Prüfung |
|---|---|
| F01 | `manifest.json` entspricht dem Schema; `sessionId` und `schemaVersion` stimmen mit `session.start` überein. |
| F02 | Jede Journalzeile ist ein JSON-Objekt mit `seq`, `t`, `type`; keine Zeile über 1 MiB. |
| F03 | `seq` beginnt bei 1 und ist lückenlos. |
| F04 | Jeder Abschnitt beginnt mit dem vollständigen Sitzungskopf. |
| F05 | Jedes `ref` zeigt auf ein vorhandenes Ereignis mit kleinerem `seq`. |
| F06 | Jede Fensterkennung wurde vor ihrer Verwendung durch `window.info` eingeführt. |
| F07 | Jedes Ereignis mit Koordinaten trägt `ep`; Epoche und Monitor sind in `display.topology` definiert. |
| F08 | Zu jedem `gap` mit `phase: "begin"` gibt es höchstens ein `end` mit gleicher `gapId`; `tFrom` ≤ `tTo`. |
| F09 | Jedes `mouse.move`-Bündel lässt sich auspacken und endet bei (`tEnd`, `xEnd`, `yEnd`); `n` passt zu `d`. |
| F10 | Je Kanal (Maus, Tastatur) haben Hook-Ereignisse ohne `recovered` in `seq`-Reihenfolge nicht fallende `t`; nach `inSeq` sortiert gilt das auch kanalübergreifend. |
| F11 | Jeder genannte Blob existiert, und sein Name ist der SHA-256 seines Inhalts. `base` und `as` nennen frühere Bilder desselben Monitors, derselben Epoche und Größe; die Kettenlänge ist eingehalten. |
| F12 | Im gründlichen Modus: Jedes Bild lässt sich wiederherstellen und passt zu `pixelHash`. |
| F13 | Die Prüfkette aller Checkpoints stimmt. |
| F14 | `steps.json` entspricht dem Schema; jeder inhaltstragende Wert hat einen `prov`-Eintrag; jedes `anchorSeq` existiert. |
| F15 | `edits.json` entspricht dem Schema; Anker, Bild-Kennungen und Fundstellen existieren oder stehen in `orphans`. In den schutzrelevanten Bereichen steht nichts Unbekanntes ([15.7](#erweiterungen)). |
| F16 | Zählwerte und Prüfsummen im Manifest passen zu den Dateien. |
| F17 | Kein gespeicherter Wert überschreitet die in `settings.capture` genannten Grenzen. |
| F18 | Jedes Eingabeereignis aus dem Hook trägt `inSeq`; keine Nummer ist doppelt vergeben. Jede fehlende Nummer ist erklärt: Sie liegt zwischen `inSeq` und `inSeqEnd` eines `text.masked` (dort passt die Zahl der fehlenden Nummern zu `events`) oder zeitlich in einer Lücke, die den Kanal betrifft. **Ausnahme ohne gespeicherte Anzahl:** Trägt ein `text.masked` kein `inSeqEnd`, gelten alle fehlenden Nummern als erklärt, die von seinem `inSeq` an bis vor der Nummer des ersten Eingabeereignisses liegen, dessen `t` nach seinem `tEnd` liegt. Wie viele es sind, wird dort nicht geprüft – in ihnen stecken die zufälligen Zuschläge ([9.9](#ev-text-masked)). Über eine Lücke `excludedApp` hinweg fehlt **keine** Nummer, weil dort keine vergeben wird. Eine unerklärte Lücke in der Nummernfolge ist ein Fehler. |
| F19 | **Ziel-Angaben:** `goal` steht nur im `session.start` des ersten Abschnitts; `manifest.goal` stimmt damit überein. `goal` und `edits.goal` enthalten nur `description`, `expected`, `reference` als Zeichenketten innerhalb der Längengrenzen – im Journal und im Manifest keine leeren. `edits.goal` trägt `atUtc` und `by`. |
| F20 | **Marker:** `kind` ist einer der festgelegten Werte (der frühere Wert `marker` gilt als `note`). `redactLastInput` trägt `tFrom`, `redactionId` und `ref`. Bei `snapshot` und bei jedem Marker des Autors (`note`, `error`, `expected`, `important`) ist `frame` genannt; fehlt das genannte Bild im Journal, ist das ein **Hinweis**, kein Fehler (Abbruch zwischen beiden Zeilen). `label` hat höchstens 200 Zeichen. |
| F21 | **Profil:** `manifest.profile` stimmt mit `session.start.profile` des ersten Abschnitts überein. `effective` widerspricht `settings` nicht (`scopeMode`, `privacyLevel`, `clipboardContent`, `capture`, `excludedAdded`). `weakens` nennt nur bekannte Einstellungen. |
| F22 | **Abschnitte und Ersetzungen:** Jeder Abschnitt mit `purpose: "replacement"` nennt in `replaces` einen früheren Abschnitt und einen Bereich, der darin liegt; `parts[]` im Manifest stimmt mit den Sitzungsköpfen überein. Für `edits.replacements[]` gelten die Bedingungen aus [13.13](#edits-replacements): Kennungen eindeutig, Abschnitte vorhanden und verschieden, `by.section` ein späterer Abschnitt mit `purpose: "replacement"` und höchstens einmal verwendet, `fromSeq` ≤ `toSeq` im Abschnitt `replaces.section`, keine Überschneidung. Für `edits.sections[]`: Der Abschnitt existiert und ist nicht Abschnitt 0; je Abschnitt höchstens ein Eintrag. `inSeq` ist über alle Abschnitte hinweg eindeutig. Trägt ein Monitor einen `deviceKey`, nennt jeder Sitzungskopf dasselbe `deviceSalt`. `manifest.performance` hat je Abschnitt höchstens einen Eintrag. |
| F23 | **Verallgemeinerungen:** Jeder Eintrag hat genau eines von `match.literal` und `match.pattern` – oder `applied: true` und kein `match`. Literal mindestens 3 Zeichen; Muster in der zulässigen Teilmenge übersetzbar, höchstens 500 Zeichen, passt nicht auf die leere Zeichenkette. `placeholder` ist ein gültiger Name, kein Tastenname und keine reservierte Marke. `scope: "steps"` hat `anchors`, deren Schritte existieren oder unter `orphans` stehen. `targets` ist nicht leer. Bildflächen nennen vorhandene Bilder. Zwei Regeln mit gleichem `match` sind ein **Hinweis**. |
| F24 | **Ignorierte Fundstellen:** `rule` hat die Form `Q` und mindestens zwei Ziffern; (`rule`, `anchor`, `key`) ist eindeutig; `key` hat höchstens 64 Zeichen; der Anker existiert oder steht unter `orphans`. |
| F25 | **Suchindex**, falls vorhanden: `meta.json` ist lesbar, `formatVersion` bekannt, `sessionId` passt, `entries` stimmt mit der Zeilenzahl überein. Ein veralteter oder unvollständiger Index ist ein **Hinweis**. Im gründlichen Modus wird jeder Eintrag gegen die Sicht der Leseschicht geprüft: Ein Text, den die Leseschicht nicht ausgeben würde, ist ein **Fehler**. |

Die Prüfungen F19 bis F25 betreffen ausschließlich wahlfreie Angaben. Eine Sitzung, die keine davon enthält, besteht sie ohne Weiteres.

---

<a id="json-schema"></a>
## 17. JSON-Schema-Auszüge

Die vollständigen Schemata (JSON Schema, Entwurf 2020-12) werden in Meilenstein M1 aus den Datentypen des Programms erzeugt und unter `docs/schema/` veröffentlicht – eine einzige Quelle für Code und Dokumentation. Die folgenden Auszüge zeigen den Stil und die wichtigsten Strukturen. Überall gilt `additionalProperties: true` (G6) – außer in den schutzrelevanten Bereichen von `edits.json` ([15.7](#erweiterungen)).

**Ereigniskopf**

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "urn:steprec:1.0:event",
  "type": "object",
  "required": ["seq", "t", "type"],
  "properties": {
    "seq":  { "type": "integer", "minimum": 1 },
    "t":    { "type": "integer", "minimum": 0 },
    "type": { "type": "string" },
    "tObs": { "type": "integer", "minimum": 0 },
    "tSrc": { "enum": ["hook", "raw", "present", "sys", "obs", "ref", "est"] },
    "tErr": { "type": "integer", "minimum": 0 },
    "ref":  { "type": "integer", "minimum": 1 },
    "src":  { "enum": ["hook", "raw", "uia", "msaa", "win32", "winevent", "dda", "wgc", "gdi", "user", "recorder"] },
    "ep":   { "type": "integer", "minimum": 0 },
    "self": { "type": "boolean" },
    "redacted": { "type": "array", "items": { "type": "string" } },
    "substituted": { "type": "array", "items": { "type": "string" } }
  },
  "additionalProperties": true
}
```

**Rechteck und `mouse.down`**

```json
{
  "$id": "urn:steprec:1.0:mouse.down",
  "$defs": {
    "rect": {
      "type": "object",
      "required": ["m", "x", "y", "w", "h"],
      "properties": {
        "m": { "type": "string", "pattern": "^M[0-9]+$" },
        "x": { "type": "integer" }, "y": { "type": "integer" },
        "w": { "type": "integer", "minimum": 0 }, "h": { "type": "integer", "minimum": 0 }
      }
    },
    "button": { "enum": ["primary", "secondary", "middle", "x1", "x2"] },
    "mods": { "type": "array", "items": { "enum": ["ctrl", "alt", "shift", "win", "altGr"] }, "uniqueItems": true }
  },
  "allOf": [ { "$ref": "urn:steprec:1.0:event" } ],
  "required": ["ep", "m", "x", "y", "button", "physButton"],
  "properties": {
    "type": { "const": "mouse.down" },
    "m": { "type": "string", "pattern": "^M[0-9]+$" },
    "x": { "type": "integer" }, "y": { "type": "integer" },
    "button": { "$ref": "#/$defs/button" },
    "physButton": { "enum": ["left", "right", "middle", "x1", "x2"] },
    "mods": { "$ref": "#/$defs/mods" },
    "held": { "type": "array", "items": { "$ref": "#/$defs/button" } },
    "inj": { "enum": [true, "lowerIl"] },
    "device": { "enum": ["mouse", "touch", "pen"] },
    "inSeq": { "type": "integer", "minimum": 0 },
    "win": { "type": "string", "pattern": "^w[0-9]+$" },
    "hitHwnd": { "type": "string", "pattern": "^0x[0-9A-F]+$" },
    "fg": { "type": "string", "pattern": "^w[0-9]+$" },
    "recovered": { "const": true }
  },
  "if": { "not": { "required": ["recovered"] } },
  "then": { "required": ["inSeq"] }
}
```

**`frame` mit den vier Ablagearten**

```json
{
  "$id": "urn:steprec:1.0:frame",
  "allOf": [ { "$ref": "urn:steprec:1.0:event" } ],
  "required": ["id", "ep", "m", "w", "h", "trigger", "store"],
  "properties": {
    "type": { "const": "frame" },
    "id": { "type": "string", "pattern": "^f[0-9]{6,}$" },
    "w": { "type": "integer", "minimum": 1 }, "h": { "type": "integer", "minimum": 1 },
    "trigger": { "enum": ["preInput", "settle", "change", "manual", "comment", "sessionStart", "resume", "sessionStop",
      "topology", "scope", "periodic"] },
    "guarantee": { "enum": ["beforeInput", "atInput", "none"] },
    "settle": {
      "type": "object", "required": ["reason"],
      "properties": { "reason": { "enum": ["quiet", "timeout", "nextInput"] }, "quietMs": { "type": "integer", "minimum": 0 } }
    },
    "store": {
      "oneOf": [
        { "type": "object", "required": ["kind", "blob"],
          "properties": { "kind": { "const": "key" }, "blob": { "type": "string", "pattern": "^sha256:[0-9a-f]{64}$" }, "bytes": { "type": "integer" } } },
        { "type": "object", "required": ["kind", "base", "rect", "blob"],
          "properties": { "kind": { "const": "delta" }, "base": { "type": "string", "pattern": "^f[0-9]{6,}$" },
            "rect": { "type": "object", "required": ["x", "y", "w", "h"] },
            "blob": { "type": "string", "pattern": "^sha256:[0-9a-f]{64}$" }, "bytes": { "type": "integer" } } },
        { "type": "object", "required": ["kind", "as"],
          "properties": { "kind": { "const": "same" }, "as": { "type": "string", "pattern": "^f[0-9]{6,}$" } } },
        { "type": "object", "required": ["kind", "reason"],
          "properties": { "kind": { "const": "none" }, "reason": { "enum": ["dropped", "captureFailed", "privacyMode", "outOfScope", "protected"] } } }
      ]
    }
  },
  "if": { "properties": { "trigger": { "const": "preInput" } } },
  "then": { "required": ["ref", "guarantee"] }
}
```

**`gap`**

```json
{
  "$id": "urn:steprec:1.0:gap",
  "allOf": [ { "$ref": "urn:steprec:1.0:event" } ],
  "required": ["gapId", "phase", "cause", "tFrom", "channels", "inputLost"],
  "properties": {
    "type": { "const": "gap" },
    "gapId": { "type": "string", "pattern": "^g[0-9]+$" },
    "phase": { "enum": ["begin", "end", "single"] },
    "cause": { "enum": ["paused", "privacyMode", "excludedApp", "outOfScope", "secureDesktop", "sessionLocked",
      "sessionDisconnected", "suspend", "elevatedWindow", "hookLost", "inputMismatch", "captureLost", "frameDropped",
      "protectedContent", "recorderInteraction", "diskFull", "overload", "crash", "unsupportedInput", "relativeMouse",
      "unknown"] },
    "tFrom": { "type": "integer", "minimum": 0 },
    "tTo": { "type": ["integer", "null"], "minimum": 0 },
    "channels": { "type": "array", "minItems": 1,
      "items": { "enum": ["keyboard", "mouse", "frames", "elements", "windows", "clipboard", "fieldValues", "all"] } },
    "inputLost": { "enum": ["none", "possible", "certain", "unknown"] }
  },
  "if": { "properties": { "phase": { "enum": ["end", "single"] } } },
  "then": { "required": ["tTo"] }
}
```

**Schritt (gekürzt)**

```json
{
  "$id": "urn:steprec:1.0:step",
  "type": "object",
  "required": ["id", "index", "anchorSeq", "kind", "importance", "evidence", "tFrom", "tTo", "seq", "frames", "prov"],
  "properties": {
    "id": { "type": "string", "pattern": "^s[0-9]+$" },
    "index": { "type": "integer", "minimum": 1 },
    "anchorSeq": { "type": "integer", "minimum": 1 },
    "kind": { "enum": ["click", "doubleClick", "tripleClick", "rightClick", "drag", "scroll", "type", "hotkey", "key",
      "hover", "windowSwitch", "system", "wait", "comment", "snapshot", "marker", "gap", "move"] },
    "action": { "type": ["object", "null"],
      "properties": { "verb": { "enum": ["click", "doubleClick", "tripleClick", "rightClick", "middleClick", "moveTo",
        "dragTo", "scroll", "write", "press", "hotkey", "wait"] } } },
    "importance": { "enum": ["primary", "secondary", "trace"] },
    "evidence": {
      "type": "object", "required": ["level"],
      "properties": {
        "level": { "enum": ["confirmed", "inputOnly", "unknown"] },
        "input": { "enum": ["observed", "masked", "unobserved"] },
        "target": { "enum": ["agreed", "corroborated", "single", "conflict", "position", "none", "n/a"] },
        "result": { "enum": ["valueConfirmed", "valueOnly", "simulated", "effectObserved", "none", "n/a"] },
        "cause": { "enum": ["none", "unobserved", "n/a"] },
        "reasons": { "type": "array", "items": { "type": "string" } }
      }
    },
    "seq": {
      "type": "object", "required": ["input", "approach"],
      "properties": {
        "input":    { "type": "array", "items": { "type": "array", "items": { "type": "integer" }, "minItems": 2, "maxItems": 2 } },
        "approach": { "type": "array", "items": { "type": "array", "items": { "type": "integer" }, "minItems": 2, "maxItems": 2 } },
        "related":  { "type": "array", "items": { "type": "integer" } }
      }
    },
    "frames": {
      "type": "object", "required": ["pre", "post"],
      "properties": { "pre": { "type": ["string", "null"] }, "post": { "type": ["string", "null"] },
        "between": { "type": "array", "items": { "type": "string" } } }
    }
  }
}
```

**`marker` mit Typ**

```json
{
  "$id": "urn:steprec:1.0:marker",
  "required": ["id", "kind"],
  "properties": {
    "type": { "const": "marker" },
    "id": { "type": "string", "pattern": "^mk[0-9]+$" },
    "kind": { "enum": ["note", "error", "expected", "important", "snapshot", "discardPrevious", "redactLastInput", "marker"] },
    "label": { "type": "string", "maxLength": 200 },
    "frame": { "type": "string", "pattern": "^f[0-9]{6,}$" },
    "tFrom": { "type": "integer", "minimum": 0 },
    "redactionId": { "type": "string" }
  },
  "allOf": [
    { "$ref": "urn:steprec:1.0:event" },
    { "if": { "properties": { "kind": { "enum": ["snapshot", "note", "error", "expected", "important", "marker"] } } }, "then": { "required": ["frame"] } },
    { "if": { "properties": { "kind": { "const": "redactLastInput" } } }, "then": { "required": ["ref", "tFrom", "redactionId"] } }
  ]
}
```

(`marker` in der Aufzählung ist der frühere Name von `note`; Schreiber verwenden ihn nicht mehr.)

**Ziel und Profil** – gleich in `manifest.json` und `session.start`

```json
{
  "$id": "urn:steprec:1.0:goal-and-profile",
  "$defs": {
    "goal": {
      "type": "object", "minProperties": 1,
      "properties": {
        "description": { "type": "string", "minLength": 1, "maxLength": 2000 },
        "expected": { "type": "string", "minLength": 1, "maxLength": 2000 },
        "reference": { "type": "string", "minLength": 1, "maxLength": 200, "pattern": "^[^\\r\\n]*$" }
      }
    },
    "profile": {
      "type": "object", "required": ["name", "effective"],
      "properties": {
        "name": { "type": "string", "minLength": 1, "maxLength": 100 },
        "id": { "enum": ["guide", "support", "aiTrace"] },
        "builtIn": { "type": "boolean" },
        "effective": {
          "type": "object",
          "properties": {
            "scopeMode": { "enum": ["screen", "app", "region"] },
            "privacyLevel": { "enum": ["balanced", "strict"] },
            "clipboardContent": { "type": "boolean" },
            "capture": { "type": "object" },
            "excludedAdded": { "type": "array", "items": { "type": "object" } },
            "afterStop": {
              "type": "object", "required": ["action"],
              "properties": { "action": { "enum": ["viewer", "export", "supportPackage"] }, "export": { "type": "object" } }
            },
            "showGoalFields": { "type": "boolean" }
          }
        },
        "weakens": { "type": "array", "uniqueItems": true, "items": { "enum": ["privacyLevel", "clipboardContent"] } }
      }
    }
  },
  "properties": { "goal": { "$ref": "#/$defs/goal" }, "profile": { "$ref": "#/$defs/profile" } }
}
```

In `edits.goal` dürfen die drei Textfelder leer sein (`minLength` 0); dort sind zusätzlich `atUtc` und `by` Pflicht.

**`edits.json`: Ersetzungen, Verallgemeinerungen, verworfene Abschnitte, ignorierte Fundstellen**

```json
{
  "$id": "urn:steprec:1.0:edits-extensions",
  "type": "object",
  "properties": {
    "replacements": {
      "type": "array",
      "items": {
        "type": "object", "required": ["id", "replaces", "by", "atUtc"], "additionalProperties": false,
        "properties": {
          "id": { "type": "string", "minLength": 1 },
          "replaces": {
            "type": "object", "required": ["section", "fromSeq", "toSeq"], "additionalProperties": false,
            "properties": {
              "section": { "type": "integer", "minimum": 0 },
              "fromSeq": { "type": "integer", "minimum": 1 },
              "toSeq": { "type": "integer", "minimum": 1 }
            }
          },
          "by": {
            "type": "object", "required": ["section"], "additionalProperties": false,
            "properties": { "section": { "type": "integer", "minimum": 1 } }
          },
          "atUtc": { "type": "string", "format": "date-time" }
        }
      }
    },
    "substitutions": {
      "type": "array",
      "items": {
        "type": "object", "required": ["id", "placeholder", "scope", "targets", "createdUtc", "by"],
        "properties": {
          "id": { "type": "string", "minLength": 1 },
          "match": {
            "type": "object", "minProperties": 1, "maxProperties": 1, "additionalProperties": false,
            "properties": {
              "literal": { "type": "string", "minLength": 3 },
              "pattern": { "type": "string", "minLength": 1, "maxLength": 500 }
            }
          },
          "placeholder": { "type": "string", "pattern": "^\\p{L}[\\p{L}\\p{N} _-]{0,39}$" },
          "caseSensitive": { "type": "boolean" },
          "scope": { "enum": ["all", "steps"] },
          "anchors": { "type": "array", "minItems": 1, "items": { "type": "integer", "minimum": 1 } },
          "targets": { "type": "array", "minItems": 1, "uniqueItems": true, "items": { "enum": ["text", "images"] } },
          "images": {
            "type": "object",
            "properties": {
              "areas": { "type": "array", "items": { "type": "object", "required": ["m", "from", "to", "rect", "source"],
                "properties": { "source": { "enum": ["element", "windowTitle", "typed", "manual"] } } } },
              "throughSeq": { "type": "integer", "minimum": 0 },
              "unresolved": { "type": "integer", "minimum": 0 }
            }
          },
          "suggestion": { "enum": ["userName", "machineName", "domain", "profilePath"] },
          "createdUtc": { "type": "string", "format": "date-time" },
          "by": { "enum": ["user"] },
          "applied": { "const": true },
          "appliedUtc": { "type": "string", "format": "date-time" }
        },
        "allOf": [
          { "if": { "properties": { "scope": { "const": "steps" } } }, "then": { "required": ["anchors"] } },
          { "if": { "required": ["applied"] }, "then": { "not": { "required": ["match"] } }, "else": { "required": ["match"] } }
        ]
      }
    },
    "sections": {
      "type": "array",
      "items": {
        "type": "object", "required": ["section", "discarded", "atUtc"], "additionalProperties": false,
        "properties": {
          "section": { "type": "integer", "minimum": 1 },
          "discarded": { "type": "boolean" },
          "atUtc": { "type": "string", "format": "date-time" },
          "by": { "enum": ["recorder", "user"] }
        }
      }
    },
    "qualityIgnores": {
      "type": "array",
      "items": {
        "type": "object", "required": ["rule", "atUtc"],
        "properties": {
          "rule": { "type": "string", "pattern": "^Q[0-9]{2,}$" },
          "anchor": { "type": "integer", "minimum": 1 },
          "key": { "type": "string", "maxLength": 64 },
          "atUtc": { "type": "string", "format": "date-time" },
          "by": { "enum": ["user"] }
        }
      }
    }
  }
}
```

---

<a id="beispiele"></a>
## 18. Anhang: durchgespielte Beispiele

Die Beispiele sind von Hand ausgeschrieben, um zu zeigen, dass das Format die Fälle ausdrücken kann. Hashwerte sind gekürzt (`sha256:a41d…09c2`), lange Differenzlisten mit `…` abgekürzt; in echten Dateien stehen sie vollständig.

<a id="beispiel-a"></a>
### Anhang A – Einfacher Fall: Datei im Editor speichern

**Ablauf.** Im Editor steht ein ungespeicherter Text. Der Nutzer drückt Strg+S (Schritt 5), tippt im Dialog „Speichern unter“ den Dateinamen und korrigiert einen Tippfehler (Schritt 6) und klickt auf „Speichern“ (Schritt 7). Ein Monitor `M1` mit 2560 × 1440, Aufnahmebereich „ganzer Bildschirm“.

Der Auszug gehört zur selben Sitzung wie das Manifest-Beispiel in [4.6](#manifest), das erste `edits.json`-Beispiel in [13.8](#edits-json) und [Beispiel A des KI-Exports](06-ki-export-und-mcp.md#13-beispiel-a-datei-im-editor-speichern): 86 Sekunden, sieben Schritte, 81 Eingabeereignisse. Davor liegen die Schritte 1 bis 4 – Win+R, „notepad“ mit Eingabetaste im Dialog „Ausführen“, ein Klick in den Textbereich des Editors und der getippte Text –; sie sind dort vollständig gezeigt und hier nicht ausgeschrieben.

#### A.1 Auszug aus `events.jsonl`

Strg+S und der erscheinende Dialog:

```json
{"seq":1655,"t":79950000,"inSeq":644,"type":"key.down","vk":162,"sc":29,"key":"LeftCtrl","hkl":"04070407","locks":["num"],"fg":"w12","focus":318,"ctx":"textField"}
{"seq":1656,"t":78902000,"tObs":79951100,"tSrc":"present","type":"frame","id":"f000022","ep":0,"m":"M1","w":2560,"h":1440,"trigger":"preInput","ref":1655,"guarantee":"beforeInput","store":{"kind":"same","as":"f000021"}}
{"seq":1657,"t":80050000,"inSeq":645,"type":"key.down","vk":83,"sc":31,"key":"S","hkl":"04070407","mods":["ctrl"],"locks":["num"],"fg":"w12","focus":318,"ctx":"textField"}
{"seq":1658,"t":80128000,"inSeq":646,"type":"key.up","vk":83,"sc":31,"key":"S","fg":"w12"}
{"seq":1659,"t":80120000,"tObs":80151200,"tSrc":"sys","tErr":16000,"type":"window.info","win":"w13","hwnd":"0x000A12E0","pid":9312,"process":"notepad.exe","title":"Speichern unter","class":"#32770","owner":"w12","kind":"dialog","ep":0,"rect":{"m":"M1","x":400,"y":180,"w":820,"h":560},"state":"normal","integrity":"medium","framework":"Win32"}
{"seq":1660,"t":80120000,"tObs":80151300,"tSrc":"sys","tErr":16000,"type":"window.open","win":"w13","kind":"dialog","first":true}
{"seq":1661,"t":80136000,"tObs":80151400,"tSrc":"sys","tErr":16000,"type":"window.foreground","win":"w13","prev":"w12"}
{"seq":1662,"t":80171000,"inSeq":647,"type":"key.up","vk":162,"sc":29,"key":"LeftCtrl","fg":"w13"}
{"seq":1663,"t":80140000,"tObs":80182700,"tSrc":"sys","tErr":16000,"type":"focus","win":"w13","ctx":"textField","ctxSrc":"uia","ms":38,"el":{"rid":"42.660192.4.1001","ct":"Edit","name":"Dateiname:","aid":"1001"}}
{"seq":1664,"t":80144700,"tObs":80182900,"type":"element","trigger":"focus","ref":1663,"status":"ok","src":"uia","ms":38,"ep":0,"win":"w13","el":{"rid":"42.660192.4.1001","ct":"Edit","lct":"Bearbeiten","name":"Dateiname:","aid":"1001","cls":"Edit","fw":"Win32","rect":{"m":"M1","x":520,"y":620,"w":560,"h":26},"enabled":true,"focused":true,"password":false,"value":"*.txt","patterns":["Value","Text","LegacyIAccessible"]},"path":[{"ct":"Window","name":"Speichern unter"}]}
{"seq":1665,"t":80183000,"type":"ui.value","el":{"rid":"42.660192.4.1001","ct":"Edit","name":"Dateiname:","aid":"1001"},"win":"w13","phase":"focusIn","value":"*.txt","sel":[0,5]}
{"seq":1666,"t":80160000,"tObs":80248000,"type":"ui.snapshot","ref":1660,"win":"w13","trigger":"windowOpen","status":"ok","ms":88,"ep":0,"m":"M1","nodes":[ … siehe 9.14 … ]}
{"seq":1667,"t":80198000,"tObs":80521400,"tSrc":"present","type":"frame","id":"f000023","ep":0,"m":"M1","w":2560,"h":1440,"trigger":"settle","ref":1662,"settle":{"reason":"quiet","quietMs":300},"store":{"kind":"delta","base":"f000021","rect":{"x":400,"y":180,"w":820,"h":560},"blob":"sha256:77b0…e3d1","bytes":96402},"changed":{"ratio":0.12,"rects":[[400,180,820,560]]}}
```

Beachtenswert: Die Fensterereignisse 1659–1661 **geschahen** bei 80,120 s, wurden aber erst bei 80,151 s **beobachtet** – nach dem Loslassen von S (1658). Die Zeitachse ordnet sie über `t` richtig ein. Die Tasten 1655 und 1657 verweisen mit `focus: 318` auf den Fokus, der für sie galt: den Textbereich des Editors. Dieses `focus`-Ereignis und seine Element-Beschreibung (319) stammen vom Start des Editors und sind hier nicht gezeigt.

Die Texteingabe (gekürzt um die Tasten o, t, i, z, Punkt, t, x mit den Nummern 1703–1718):

```json
{"seq":1700,"t":81290300,"inSeq":648,"type":"key.down","vk":78,"sc":49,"key":"N","hkl":"04070407","ch":"n","locks":["num"],"fg":"w13","focus":1663,"ctx":"textField"}
{"seq":1701,"t":80198000,"tObs":81291200,"tSrc":"present","type":"frame","id":"f000024","ep":0,"m":"M1","w":2560,"h":1440,"trigger":"preInput","ref":1700,"guarantee":"beforeInput","store":{"kind":"same","as":"f000023"}}
{"seq":1702,"t":81352000,"inSeq":649,"type":"key.up","vk":78,"sc":49,"key":"N","fg":"w13"}
{"seq":1719,"t":82058000,"inSeq":664,"type":"key.down","vk":82,"sc":19,"key":"R","hkl":"04070407","ch":"r","locks":["num"],"fg":"w13","focus":1663,"ctx":"textField"}
{"seq":1720,"t":82119000,"inSeq":665,"type":"key.up","vk":82,"sc":19,"key":"R","fg":"w13"}
{"seq":1721,"t":82190000,"inSeq":666,"type":"key.down","vk":8,"sc":14,"key":"Backspace","hkl":"04070407","locks":["num"],"fg":"w13","focus":1663,"ctx":"textField"}
{"seq":1722,"t":82121500,"tObs":82191000,"tSrc":"present","type":"frame","id":"f000027","ep":0,"m":"M1","w":2560,"h":1440,"trigger":"preInput","ref":1721,"guarantee":"beforeInput","store":{"kind":"delta","base":"f000026","rect":{"x":578,"y":624,"w":8,"h":18},"blob":"sha256:0be4…7a19","bytes":188}}
{"seq":1723,"t":82251000,"inSeq":667,"type":"key.up","vk":8,"sc":14,"key":"Backspace","fg":"w13"}
{"seq":1724,"t":82290000,"inSeq":668,"type":"key.down","vk":84,"sc":20,"key":"T","hkl":"04070407","ch":"t","locks":["num"],"fg":"w13","focus":1663,"ctx":"textField"}
{"seq":1725,"t":82300000,"tObs":82312400,"tSrc":"sys","tErr":16000,"type":"ui.value","el":{"rid":"42.660192.4.1001","ct":"Edit","name":"Dateiname:","aid":"1001"},"win":"w13","phase":"change","value":"notiz.txt","sel":[9,9]}
{"seq":1726,"t":82352000,"inSeq":669,"type":"key.up","vk":84,"sc":20,"key":"T","fg":"w13"}
{"seq":1727,"t":82301500,"tObs":82661000,"tSrc":"present","type":"frame","id":"f000028","ep":0,"m":"M1","w":2560,"h":1440,"trigger":"settle","ref":1726,"settle":{"reason":"quiet","quietMs":300},"store":{"kind":"delta","base":"f000027","rect":{"x":570,"y":624,"w":16,"h":18},"blob":"sha256:c9a2…51f0","bytes":204}}
```

Beim Tippen entsteht nicht zu jeder Taste ein Vorher-Bild, sondern höchstens alle 250 ms (`typingFrameMinGapMs`): hier zu den Tasten n, i, t und Backspace (`f000024` bis `f000027`).

Der Mausweg und der Klick:

```json
{"seq":1728,"t":82702000,"type":"mouse.cursor","cursor":"cur1","std":"arrow"}
{"seq":1729,"t":82590000,"inSeq":670,"type":"mouse.move","ep":0,"m":"M1","x":650,"y":640,"n":250,"d":[[1000,1,0],[1000,1,0], … ],"tEnd":82839000,"xEnd":812,"yEnd":671}
{"seq":1730,"t":82840000,"inSeq":920,"type":"mouse.move","ep":0,"m":"M1","x":813,"y":671,"n":250,"d":[ … ],"tEnd":83089000,"xEnd":1010,"yEnd":700}
{"seq":1731,"t":83090000,"inSeq":1170,"type":"mouse.move","ep":0,"m":"M1","x":1011,"y":700,"n":36,"d":[ … ],"tEnd":83125000,"xEnd":1048,"yEnd":703}
{"seq":1732,"t":83185000,"inSeq":1206,"type":"mouse.move","ep":0,"m":"M1","x":1049,"y":703,"n":6,"d":[[1000,2,-1],[1000,2,0],[1000,1,-1],[1000,1,0],[1000,1,0]],"tEnd":83190000,"xEnd":1056,"yEnd":701}
{"seq":1733,"t":83270200,"tObs":83304900,"type":"element","trigger":"dwell","ref":1732,"status":"ok","src":"uia","ms":34,"ep":0,"m":"M1","x":1056,"y":701,"win":"w13","el":{ … Button „Speichern“, siehe 9.12 … },"path":[{"ct":"Window","name":"Speichern unter"}]}
{"seq":1734,"t":83412066,"inSeq":1212,"type":"mouse.down","ep":0,"m":"M1","x":1056,"y":701,"button":"primary","physButton":"left","win":"w13","hitHwnd":"0x000A12F4","fg":"w13"}
{"seq":1735,"t":83108300,"tObs":83413900,"tSrc":"present","type":"frame","id":"f000029","ep":0,"m":"M1","w":2560,"h":1440,"trigger":"preInput","ref":1734,"guarantee":"beforeInput","store":{"kind":"delta","base":"f000028","rect":{"x":1012,"y":688,"w":88,"h":26},"blob":"sha256:a41d…09c2","bytes":612},"cursor":{"x":1056,"y":701,"cursor":"cur1","visible":true}}
{"seq":1736,"t":83412300,"tObs":83441800,"type":"element","trigger":"buttonDown","ref":1734,"status":"ok","src":"uia","ms":29,"ep":0,"m":"M1","x":1056,"y":701,"win":"w13","dwellRef":1733,"dwellValid":true,"el":{ … derselbe Button, gleiche RuntimeId … },"path":[{"ct":"Window","name":"Speichern unter"}]}
{"seq":1737,"t":83498120,"inSeq":1213,"type":"mouse.up","ep":0,"m":"M1","x":1056,"y":701,"button":"primary","physButton":"left","win":"w13","fg":"w13"}
{"seq":1738,"t":83499000,"tObs":83512600,"tSrc":"sys","tErr":16000,"type":"ui.invoke","el":{"rid":"42.660192.4.1","ct":"Button","name":"Speichern","aid":"1"},"win":"w13"}
{"seq":1739,"t":83592000,"tObs":83607100,"tSrc":"sys","tErr":16000,"type":"window.close","win":"w13","destroyed":true}
{"seq":1740,"t":83596000,"tObs":83607300,"tSrc":"sys","tErr":16000,"type":"window.foreground","win":"w12","prev":"w13"}
{"seq":1741,"t":83604000,"tObs":83619800,"tSrc":"sys","tErr":16000,"type":"window.title","win":"w12","title":"notiz.txt – Editor"}
{"seq":1742,"t":83600000,"tObs":83641200,"tSrc":"sys","tErr":16000,"type":"focus","win":"w12","ctx":"textField","ctxSrc":"uia","ms":36,"el":{"rid":"42.593210.4.15","ct":"Document","name":"Text-Editor"}}
{"seq":1745,"t":83611000,"tObs":83996000,"tSrc":"present","type":"frame","id":"f000030","ep":0,"m":"M1","w":2560,"h":1440,"trigger":"settle","ref":1737,"settle":{"reason":"quiet","quietMs":300},"store":{"kind":"delta","base":"f000028","rect":{"x":293,"y":131,"w":927,"h":609},"blob":"sha256:5c0e…41bb","bytes":48211},"pixelHash":"xxh3:8f1c22a09b3d4e57","changed":{"ratio":0.13,"rects":[[400,180,820,560],[293,131,900,32]]},"cursor":{"x":1056,"y":701,"cursor":"cur1","visible":true}}
```

Das Vorher-Bild `f000029` zeigt den Zustand, den der Nutzer vor dem Klick sah: Seine Darstellungszeit (83,108 s) liegt vor dem Klick (83,412 s), und es enthält nur die Änderung seit dem letzten Bild – die hervorgehobene Schaltfläche.

#### A.2 Auszug aus `steps.json`

```json
{
  "format": "steprec.steps",
  "schemaVersion": "1.0",
  "sessionId": "019328a4-6b1e-7c3a-9d52-0f4e8a1b77c0",
  "stepBuilderVersion": "1.0.0",
  "derivedAtUtc": "2026-11-11T13:31:31.540000Z",
  "source": { "lastSeq": 1756 },
  "steps": [
    {
      "id": "s1657", "index": 5, "anchorSeq": 1657, "kind": "hotkey",
      "action": { "verb": "hotkey", "keys": ["Ctrl", "S"] },
      "importance": "primary",
      "evidence": { "level": "confirmed", "input": "observed", "target": "single", "result": "effectObserved", "cause": "n/a",
        "reasons": ["keysObserved", "foregroundKnown", "effectObserved"] },
      "tFrom": 79950000, "tTo": 80171000,
      "seq": { "input": [[1655, 1662]], "approach": [], "related": [318, 319, 1656, 1659, 1660, 1661, 1663, 1666, 1667] },
      "ctx": { "win": "w12", "process": "notepad.exe", "title": "Unbenannt – Editor", "kind": "window", "ep": 0, "m": "M1" },
      "target": { "status": "single", "source": "uia",
        "el": { "rid": "42.593210.4.15", "ct": "Document", "lct": "Dokument", "name": "Text-Editor",
          "rect": { "m": "M1", "x": 301, "y": 211, "w": 1398, "h": 780 } },
        "path": [ { "ct": "Window", "name": "Unbenannt – Editor" } ],
        "from": { "focus": 318 } },
      "input": { "combo": "Ctrl+S", "keys": ["LeftCtrl", "S"], "count": 1, "commonMeaning": "save" },
      "effect": { "observed": true, "causality": "direct", "latencyUs": 70000,
        "windowsOpened": [ { "win": "w13", "title": "Speichern unter", "kind": "dialog", "snapshot": 1666, "afterUs": 70000 } ],
        "foreground": { "from": "w12", "to": "w13" },
        "focus": { "seq": 1663, "el": { "ct": "Edit", "name": "Dateiname:", "aid": "1001" }, "ctx": "textField" },
        "screen": { "changedRatio": 0.12, "settled": true, "settleUs": 148000, "settleReason": "quiet" } },
      "frames": { "m": "M1", "pre": "f000022", "post": "f000023", "between": [] },
      "snapshots": [1666],
      "prov": [
        { "path": "ctx.title", "seq": 310, "field": "title", "copy": true },
        { "path": "target.el", "seq": 319, "field": "el", "copy": true },
        { "path": "target.path", "seq": 319, "field": "path", "copy": true },
        { "path": "effect.windowsOpened[0].title", "seq": 1659, "field": "title", "copy": true },
        { "path": "effect.focus.el", "seq": 1663, "field": "el", "copy": true }
      ]
    },
    {
      "id": "s1700", "index": 6, "anchorSeq": 1700, "kind": "type",
      "action": { "verb": "write", "text": "notiz.txt" },
      "importance": "primary",
      "evidence": { "level": "confirmed", "input": "observed", "target": "corroborated", "result": "valueConfirmed", "cause": "n/a",
        "reasons": ["focusFieldKnown", "valueMatchesKeys"] },
      "tFrom": 81290300, "tTo": 82352000,
      "seq": { "input": [[1700, 1726]], "approach": [], "related": [1663, 1664, 1665, 1725, 1727] },
      "ctx": { "win": "w13", "process": "notepad.exe", "title": "Speichern unter", "kind": "dialog", "ep": 0, "m": "M1" },
      "target": { "status": "corroborated", "source": "uia",
        "el": { "rid": "42.660192.4.1001", "ct": "Edit", "lct": "Bearbeiten", "name": "Dateiname:", "aid": "1001",
          "rect": { "m": "M1", "x": 520, "y": 620, "w": 560, "h": 26 } },
        "path": [ { "ct": "Window", "name": "Speichern unter" } ],
        "from": { "focus": 1663, "corroboration": [1725] } },
      "input": { "tokens": ["notiz.txr", { "key": "Backspace" }, "t"], "text": "notiz.txt", "masked": false, "count": 11,
        "valueBefore": "*.txt", "valueAfter": "notiz.txt", "inputMethod": "keyboard", "resultSource": "value", "durationUs": 1061700 },
      "effect": { "observed": true, "causality": "direct",
        "screen": { "changedRatio": 0.0003, "settled": true, "settleReason": "quiet" } },
      "frames": { "m": "M1", "pre": "f000024", "post": "f000028", "between": ["f000025", "f000026", "f000027"] },
      "prov": [
        { "path": "ctx.title", "seq": 1659, "field": "title", "copy": true },
        { "path": "target.el", "seq": 1664, "field": "el", "copy": true },
        { "path": "target.path", "seq": 1664, "field": "path", "copy": true },
        { "path": "input.tokens", "seqRanges": [[1700, 1726]], "field": "ch", "copy": false },
        { "path": "input.text", "seqRanges": [[1700, 1726]], "field": "ch", "copy": false },
        { "path": "action.text", "seqRanges": [[1700, 1726]], "field": "ch", "copy": false },
        { "path": "input.valueBefore", "seq": 1665, "field": "value", "copy": true },
        { "path": "input.valueAfter", "seq": 1725, "field": "value", "copy": true }
      ]
    },
    {
      "id": "s1734", "index": 7, "anchorSeq": 1734, "kind": "click",
      "action": { "verb": "click", "m": "M1", "x": 1056, "y": 701, "button": "primary", "clicks": 1 },
      "importance": "primary",
      "evidence": { "level": "confirmed", "input": "observed", "target": "agreed", "result": "effectObserved", "cause": "n/a",
        "reasons": ["elementAgreed", "elementInvoked", "hitWindowMatches"] },
      "tFrom": 82590000, "tTo": 83498120,
      "seq": { "input": [[1734, 1737]], "approach": [[1729, 1732]], "related": [1733, 1735, 1736, 1738, 1739, 1740, 1741, 1742, 1745] },
      "ctx": { "win": "w13", "process": "notepad.exe", "title": "Speichern unter", "kind": "dialog", "ep": 0, "m": "M1" },
      "target": { "status": "agreed", "source": "uia",
        "el": { "rid": "42.660192.4.1", "ct": "Button", "lct": "Schaltfläche", "name": "Speichern", "aid": "1",
          "rect": { "m": "M1", "x": 1012, "y": 688, "w": 88, "h": 26 }, "access": "Alt+S", "dupIndex": 1, "dupCount": 1 },
        "path": [ { "ct": "Window", "name": "Speichern unter" } ],
        "from": { "dwell": 1733, "down": 1736, "corroboration": [1738] } },
      "position": { "ep": 0, "m": "M1", "x": 1056, "y": 701, "inElement": [0.5, 0.5], "inWindow": [656, 521] },
      "input": { "button": "primary", "physButton": "left", "count": 1, "mods": [], "pressUs": 86054, "longPress": false, "device": "mouse" },
      "effect": { "observed": true, "causality": "direct", "latencyUs": 86934,
        "invoked": [ { "ct": "Button", "name": "Speichern", "aid": "1" } ],
        "windowsClosed": [ { "win": "w13", "title": "Speichern unter", "kind": "dialog", "afterUs": 179934 } ],
        "foreground": { "from": "w13", "to": "w12" },
        "titleChanges": [ { "win": "w12", "from": "Unbenannt – Editor", "to": "notiz.txt – Editor", "afterUs": 191934 } ],
        "focus": { "seq": 1742, "el": { "ct": "Document", "name": "Text-Editor" }, "ctx": "textField" },
        "screen": { "changedRatio": 0.13, "settled": true, "settleUs": 198934, "settleReason": "quiet" } },
      "frames": { "m": "M1", "pre": "f000029", "post": "f000030", "between": [] },
      "approach": { "from": { "m": "M1", "x": 650, "y": 640 }, "distancePx": 412, "directPx": 411, "durationUs": 600000, "straightness": 0.998,
        "path": [[650, 640, 0], [813, 671, 250], [1011, 700, 500], [1049, 703, 595], [1056, 701, 600]],
        "dwells": [], "restUs": 222066 },
      "prov": [
        { "path": "ctx.title", "seq": 1659, "field": "title", "copy": true },
        { "path": "target.el", "seq": 1736, "field": "el", "copy": true },
        { "path": "target.path", "seq": 1736, "field": "path", "copy": true },
        { "path": "effect.invoked[0]", "seq": 1738, "field": "el", "copy": true },
        { "path": "effect.windowsClosed[0].title", "seq": 1659, "field": "title", "copy": true },
        { "path": "effect.titleChanges[0].from", "seq": 310, "field": "title", "copy": true },
        { "path": "effect.titleChanges[0].to", "seq": 1741, "field": "title", "copy": true },
        { "path": "effect.focus.el", "seq": 1742, "field": "el", "copy": true }
      ]
    }
  ]
}
```

(Gekürzt um `params`, `timeline`, `gaps`, `completeness` und die Schritte 1 bis 4.)

Die Tastenkombination `s1657` trägt als Ziel das Element, das beim Drücken den Fokus hatte – den Textbereich des Editors – mit dem Status `single`: Der Fokus ist bekannt, eine Reaktion dieses Elements gab es nicht ([Schritterkennung, 6.4](05-schritterkennung.md#zielbestimmung)). Belegt ist der Schritt trotzdem, weil bei Tastenkombinationen das Fenster im Vordergrund den Ausschlag gibt.

#### A.3 Vom Schritt zur Zeile in `trace.md`

Die Form von `trace.md` legt der [KI-Export](06-ki-export-und-mcp.md) fest. Für Schritt 7 stammen die dort gezeigten Angaben aus diesen Feldern:

| Angabe in `trace.md` | Quelle in `steps.json` |
|---|---|
| „Schritt 7 · 00:01:23,412 · Klick (2,1 s nach Schritt 6)“ | Schrittnummer `number` der Leseschicht (hier gleich `index`, weil nichts ersetzt ist), Zeit des Ankerereignisses (83 412 066 µs), `kind`, Abstand der Ankerzeiten zu Schritt 6 |
| „Linksklick auf Schaltfläche ‚Speichern‘“ | `action`, `input.button`, `target.el.ct`, `target.el.name` über die Textvorlage |
| „Pfad: Fenster ‚Speichern unter‘ › Schaltfläche ‚Speichern‘“ | `target.path`, `target.el` |
| „Position: (1056, 701) … im Element (0,50 / 0,50)“ | `position` |
| „Mausweg davor: 412 px in 0,6 s, direkt, kein Verweilen“ | `approach.distancePx`, `durationUs`, `straightness`, leere `dwells`; das Ruhen auf dem Ziel selbst (`restUs`, hier 0,22 s) ist kein Zwischenhalt |
| „Dialog geschlossen nach 180 ms · Fenstertitel … → …“ | `effect.windowsClosed[0].afterUs`, `effect.titleChanges` |
| „Beleg: belegt (Ziel vor und beim Klick übereinstimmend)“ | `evidence.level`, `evidence.target`, `evidence.reasons` |
| Bilder vorher / Ausschnitt / nachher | `frames.pre`, `frames.post`, zugeschnitten um `target.el.rect` |

---

### Anhang B – Schwierige Fälle

Jeder Fall zeigt die entscheidenden Journalzeilen und, was daraus in `steps.json` wird. Die Fälle stammen aus verschiedenen Sitzungen; ihre Nummern hängen nicht zusammen.

#### B.1 Passwortabfrage im Terminal mit Privattaste

**Ablauf.** In Windows Terminal tippt der Nutzer `ssh admin@server01` und Enter. Das Terminal fragt nach dem Passwort. Ein Terminal ist eine undurchsichtige Oberfläche: Ein Passwortfeld ist dort nicht erkennbar. Der Nutzer drückt die Privattaste, tippt das Passwort, drückt Enter und schaltet den Privatmodus wieder aus.

```json
{"seq":800,"t":40102000,"tObs":40131000,"tSrc":"sys","tErr":16000,"type":"window.info","win":"w7","hwnd":"0x00081A44","pid":7720,"process":"WindowsTerminal.exe","title":"PowerShell","class":"CASCADIA_HOSTING_WINDOW_CLASS","kind":"window","ep":0,"rect":{"m":"M1","x":300,"y":160,"w":1200,"h":720},"state":"normal","surface":"opaque","surfaceKind":"terminal"}
{"seq":812,"t":40150000,"tObs":40172000,"tSrc":"sys","tErr":16000,"type":"focus","win":"w7","ctx":"opaque","ctxSrc":"heuristic","ms":22}
{"seq":840,"t":42000100,"inSeq":9100,"type":"key.down","vk":83,"sc":31,"key":"S","hkl":"04070407","ch":"s","locks":[],"fg":"w7","focus":812,"ctx":"opaque"}
{"seq":910,"t":44190000,"inSeq":9118,"type":"key.down","vk":162,"sc":541,"key":"LeftCtrl","hkl":"04070407","locks":[],"fg":"w7","focus":812,"ctx":"opaque","altGrPart":true}
{"seq":911,"t":44190100,"inSeq":9119,"type":"key.down","vk":165,"sc":56,"ext":true,"key":"RightAlt","hkl":"04070407","locks":[],"fg":"w7","focus":812,"ctx":"opaque"}
{"seq":912,"t":44221900,"inSeq":9120,"type":"key.down","vk":81,"sc":16,"key":"Q","hkl":"04070407","ch":"@","mods":["altGr"],"locks":[],"fg":"w7","focus":812,"ctx":"opaque"}
{"seq":938,"t":45102000,"inSeq":9140,"type":"key.down","vk":13,"sc":28,"key":"Enter","hkl":"04070407","locks":[],"fg":"w7","focus":812,"ctx":"opaque"}
{"seq":939,"t":45170000,"inSeq":9141,"type":"key.up","vk":13,"sc":28,"key":"Enter","fg":"w7"}
{"seq":950,"t":47300000,"type":"system","kind":"privacyMode","active":true,"trigger":"hotkey","gapId":"g2"}
{"seq":951,"t":47300000,"type":"gap","gapId":"g2","phase":"begin","cause":"privacyMode","tFrom":47300000,"channels":["keyboard","clipboard","fieldValues"],"inputLost":"none","win":"w7"}
{"seq":952,"t":48100200,"tEnd":50650900,"type":"text.masked","count":14,"inSeq":9148,"inSeqEnd":9175,"events":28,"reason":"privacyMode","fg":"w7","focus":812}
{"seq":953,"t":51020000,"inSeq":9176,"type":"key.down","vk":13,"sc":28,"key":"Enter","hkl":"04070407","locks":[],"fg":"w7","focus":812,"ctx":"opaque"}
{"seq":954,"t":51088000,"inSeq":9177,"type":"key.up","vk":13,"sc":28,"key":"Enter","fg":"w7"}
{"seq":955,"t":51900000,"type":"system","kind":"privacyMode","active":false,"trigger":"hotkey","end":"toggle","gapId":"g2"}
{"seq":956,"t":51900000,"type":"gap","gapId":"g2","phase":"end","cause":"privacyMode","tFrom":47300000,"tTo":51900000,"channels":["keyboard","clipboard","fieldValues"],"inputLost":"none","win":"w7","detail":{"trigger":"hotkey","end":"toggle"}}
```

Die Tasten der Privattaste selbst stehen mit `self: true` im Journal und sind hier weggelassen. Zeile 910 ist der vom System erzeugte Strg-Anteil von AltGr (`altGrPart`), kein eigener Tastendruck.

Daraus werden zwei Schritte (gekürzt):

```json
[
  { "id": "s840", "kind": "type", "importance": "primary",
    "action": { "verb": "write", "text": "ssh admin@server01", "then": { "verb": "press", "keys": ["Enter"], "presses": 1 } },
    "evidence": { "level": "inputOnly", "input": "observed", "target": "none", "result": "simulated", "cause": "n/a",
      "reasons": ["opaqueSurface", "valueUnreadable"] },
    "ctx": { "win": "w7", "process": "WindowsTerminal.exe", "title": "PowerShell", "surface": "opaque", "surfaceKind": "terminal" },
    "input": { "tokens": ["ssh admin@server01"], "text": "ssh admin@server01", "masked": false, "count": 18,
      "terminator": { "key": "Enter" }, "inputMethod": "keyboard", "resultSource": "simulated" },
    "flags": { "opaque": true, "plaintext": true } },
  { "id": "s952", "kind": "type", "action": null, "importance": "primary",
    "evidence": { "level": "unknown", "input": "masked", "target": "none", "result": "none", "cause": "n/a",
      "reasons": ["maskedByDesign", "privacyMode"] },
    "ctx": { "win": "w7", "process": "WindowsTerminal.exe", "title": "PowerShell", "surface": "opaque", "surfaceKind": "terminal" },
    "input": { "tokens": [ { "masked": 14 } ], "masked": true, "count": 14, "maskReason": "privacyMode", "terminator": { "key": "Enter" },
      "inputMethod": "keyboard", "resultSource": "none" },
    "flags": { "opaque": true, "masked": true } }
]
```

Eine KI liest daraus: Der Befehl ist bekannt, sein Ergebnis im Terminal nur aus den Bildern ersichtlich; danach wurden 14 Tasten **bewusst nicht aufgezeichnet**, gefolgt von Enter. In `gaps` steht dafür ein Eintrag mit `cause: "maskedInput"` und einer mit `cause: "privacyMode"`. Der Befehl selbst (`s840`) stammt aus einem Fenster, in dem Passwortfelder nicht erkennbar sind, und trägt deshalb `flags.plaintext`: Bis der Autor ihn im Viewer durchgesehen hat, geben MCP-Server und Kommandozeilen-Export ihn nur als `{unreviewed:18}` aus ([Durchsicht](#edits-review)) – 18 Tastenanschläge; das AltGr für „@“ zählt als reiner Modifikator nicht mit.

Erkennt der Recorder die Passwortabfrage selbst, sieht die Zeile so aus – mit Regel und Abfragezeile. Der Privatmodus (`trigger: "terminalPrompt"`) endet dann von selbst mit der Eingabetaste (`end: "enter"`), während die Privattaste ein Umschalter ohne automatisches Ende ist (oben: `end: "toggle"`):

```json
{"seq":952,"t":48100200,"tEnd":50650900,"type":"text.masked","count":14,"inSeq":9142,"inSeqEnd":9169,"events":28,"reason":"terminalPrompt","rule":"T1","promptLine":"admin@server01's password:","fg":"w7","focus":812}
```

#### B.2 UAC-Lücke

**Ablauf.** Der Nutzer startet ein Installationsprogramm mit Strg+Umschalt+Enter („als Administrator“). Die Abfrage der Benutzerkontensteuerung erscheint auf dem sicheren Desktop; dort kann der Recorder weder Eingaben noch Bilder aufzeichnen. Danach liegt ein Fenster mit erhöhten Rechten im Vordergrund.

```json
{"seq":4003,"t":130412000,"inSeq":60233,"type":"key.down","vk":13,"sc":28,"key":"Enter","hkl":"04070407","mods":["ctrl","shift"],"locks":["num"],"fg":"w27","ctx":"nonText"}
{"seq":4011,"t":130990000,"tObs":131004000,"tSrc":"sys","tErr":16000,"type":"system","kind":"desktopSwitch","to":"secure","desktop":"Winlogon"}
{"seq":4012,"t":131000000,"type":"gap","gapId":"g1","phase":"begin","cause":"secureDesktop","tFrom":131000000,"channels":["all"],"inputLost":"possible","detail":{"desktop":"Winlogon","kind":"uac"}}
{"seq":4018,"t":138980000,"tObs":138991000,"tSrc":"sys","tErr":16000,"type":"system","kind":"desktopSwitch","to":"default"}
{"seq":4019,"t":139000000,"type":"gap","gapId":"g1","phase":"end","cause":"secureDesktop","tFrom":131000000,"tTo":139000000,"channels":["all"],"inputLost":"possible","detail":{"desktop":"Winlogon","kind":"uac"}}
{"seq":4020,"t":139004000,"type":"keystate.sync","reason":"secureDesktop","gapId":"g1","down":[],"mouseDown":[],"locks":["num"],"corrected":[{"vk":162,"key":"LeftCtrl","was":"down","now":"up"},{"vk":160,"key":"LeftShift","was":"down","now":"up"}]}
{"seq":4021,"t":139020000,"tObs":139061000,"tSrc":"present","type":"frame","id":"f000188","ep":0,"m":"M1","w":2560,"h":1440,"trigger":"resume","store":{"kind":"key","blob":"sha256:3e77…b0c4","bytes":612400}}
{"seq":4022,"t":139040000,"tObs":139066000,"tSrc":"sys","tErr":16000,"type":"window.info","win":"w31","hwnd":"0x001C0F2A","pid":13388,"process":"setup.exe","title":"Setup – Beispielprogramm","kind":"window","ep":0,"rect":{"m":"M1","x":700,"y":360,"w":640,"h":480},"state":"normal","elevated":true,"integrity":"high"}
{"seq":4023,"t":139040000,"tObs":139066100,"tSrc":"sys","tErr":16000,"type":"window.foreground","win":"w31","prev":"w27"}
{"seq":4024,"t":139040000,"tObs":139066200,"type":"gap","gapId":"g2","phase":"begin","cause":"elevatedWindow","tFrom":139040000,"channels":["keyboard","mouse","elements"],"inputLost":"possible","win":"w31"}
```

Zeile 4020 ist entscheidend: Strg und Umschalt wurden vor der Lücke gedrückt, ihr Loslassen fiel in die Lücke. Ohne den Abgleich gälten sie weiter als gedrückt, und der nächste getippte Buchstabe würde als Tastenkombination gedeutet.

In `steps.json` entstehen: ein `hotkey`-Schritt (`Ctrl+Shift+Enter`) mit `flags.incomplete: true` (das Loslassen fehlt), ein `gap`-Schritt für `g1` (8 s, `inputLost: "possible"`, Belegstufe `unknown`, `input.desktopKind: "uac"`, ohne Bilder; als Anleitungsschritt heißt er „Benutzerkontensteuerung bestätigen“), ein `system`-Schritt „Fenster ‚Setup – Beispielprogramm‘ geöffnet“ mit `evidence.cause: "unobserved"` und ein `gap`-Schritt für `g2`. Die Bildschirmaufnahme läuft während `g2` weiter; nur Eingaben und Elemente fehlen. Eine KI darf daraus **nicht** schließen, dass im UAC-Dialog „Ja“ geklickt wurde – das Format sagt nur, dass danach das Setup-Fenster erschien.

#### B.3 Einfügen aus der Zwischenablage

**Ablauf.** Der Nutzer kopiert im Editor eine markierte Zeile mit Strg+C und fügt sie mit Strg+V in das Feld „Betreff“ eines Formulars ein. Die Aufzeichnung des Zwischenablage-Inhalts ist ausgeschaltet (Standard).

```json
{"seq":3118,"t":140210000,"inSeq":48811,"type":"key.down","vk":67,"sc":46,"key":"C","hkl":"04070407","mods":["ctrl"],"locks":[],"fg":"w12","focus":3050,"ctx":"textField"}
{"seq":3120,"t":140220000,"type":"clipboard","clip":"cb2","seqNo":418,"kind":"text","formats":["CF_UNICODETEXT","CF_TEXT","CF_LOCALE"],"ownerWin":"w12","withheld":"optOut"}
{"seq":3148,"t":146100000,"tObs":146139000,"tSrc":"sys","tErr":16000,"type":"focus","win":"w15","ctx":"textField","ctxSrc":"uia","ms":35,"el":{"rid":"42.201544.4.77","ct":"Edit","name":"Betreff"}}
{"seq":3150,"t":146140000,"type":"ui.value","el":{"rid":"42.201544.4.77","ct":"Edit","name":"Betreff"},"win":"w15","phase":"focusIn","value":"","sel":[0,0]}
{"seq":3160,"t":146800000,"inSeq":49102,"type":"key.down","vk":86,"sc":47,"key":"V","hkl":"04070407","mods":["ctrl"],"locks":[],"fg":"w15","focus":3148,"ctx":"textField"}
{"seq":3163,"t":146815000,"tObs":146841000,"tSrc":"sys","tErr":16000,"type":"ui.value","el":{"rid":"42.201544.4.77","ct":"Edit","name":"Betreff"},"win":"w15","phase":"change","value":"Angebot 2026-114 Projekt Nordhafen","sel":[34,34]}
```

```json
[
  { "id": "s3118", "kind": "hotkey", "action": { "verb": "hotkey", "keys": ["Ctrl", "C"] },
    "evidence": { "level": "confirmed", "input": "observed", "target": "single", "result": "effectObserved", "cause": "n/a",
      "reasons": ["keysObserved", "foregroundKnown", "clipboardChanged"] },
    "target": { "status": "single", "el": { "ct": "Document", "name": "Text-Editor" }, "from": { "focus": 3050 } },
    "input": { "combo": "Ctrl+C", "keys": ["LeftCtrl", "C"], "count": 1, "commonMeaning": "copy" },
    "clipboard": { "op": "copy", "clip": "cb2" },
    "effect": { "observed": true, "causality": "direct", "latencyUs": 10000, "clipboard": { "clip": "cb2", "kind": "text" } } },
  { "id": "s3160", "kind": "hotkey", "action": { "verb": "hotkey", "keys": ["Ctrl", "V"] },
    "evidence": { "level": "confirmed", "input": "observed", "target": "corroborated", "result": "effectObserved", "cause": "n/a",
      "reasons": ["keysObserved", "focusFieldKnown", "valueChanged", "clipboardLineage"] },
    "target": { "status": "corroborated", "el": { "ct": "Edit", "name": "Betreff" }, "from": { "focus": 3148, "corroboration": [3163] } },
    "input": { "combo": "Ctrl+V", "keys": ["LeftCtrl", "V"], "count": 1, "commonMeaning": "paste" },
    "clipboard": { "op": "paste", "clip": "cb2" },
    "effect": { "observed": true, "causality": "direct", "latencyUs": 15000,
      "values": [ { "el": { "ct": "Edit", "name": "Betreff" }, "before": "", "after": "Angebot 2026-114 Projekt Nordhafen" } ] } }
]
```

(Gekürzt; beide Schritte nennen als Ziel das Element, das beim Drücken den Fokus hatte – beim Kopieren nur über den Fokus bekannt (`single`), beim Einfügen zusätzlich durch die Wertänderung des Felds bestätigt (`corroborated`).)

Die Herkunft ist lückenlos: Eingefügt wurde der Inhalt `cb2`, kopiert in Schritt `s3118` aus Fenster `w12`. Eine Größe nennt das Ereignis nicht – ohne Inhaltsaufzeichnung wird der Inhalt gar nicht abgerufen, und ohne Abruf lässt sich seine Größe nach heutigem Kenntnisstand nicht feststellen. Was eingefügt wurde, belegt hier der Feldinhalt danach (34 Zeichen). Wäre das Zielfeld nicht lesbar (etwa in einem Remote-Fenster), bliebe genau diese Aussage: „Text aus der Zwischenablage, kopiert in Schritt `s3118`, Inhalt nicht aufgezeichnet“ – mit `result: "none"`. Die Tastenkombination selbst wäre weiter belegt, ihr Ergebnis nicht.

#### B.4 Remote-Desktop-Fenster

**Ablauf.** Im Fenster einer Remote-Desktop-Verbindung klickt der Nutzer auf eine Schaltfläche des entfernten Rechners und tippt einen Namen. Für den lokalen Rechner ist der gesamte Fensterinhalt ein einziges Bild.

```json
{"seq":5200,"t":210020000,"tObs":210049000,"tSrc":"sys","tErr":16000,"type":"window.info","win":"w20","hwnd":"0x00130B62","pid":15244,"process":"mstsc.exe","title":"srv-app01 – Remotedesktopverbindung","class":"TscShellContainerClass","kind":"window","ep":0,"rect":{"m":"M1","x":200,"y":100,"w":1936,"h":1119},"state":"normal","surface":"opaque","surfaceKind":"remoteDesktop"}
{"seq":5241,"t":212400500,"inSeq":77410,"type":"mouse.down","ep":0,"m":"M1","x":1180,"y":640,"button":"primary","physButton":"left","win":"w20","hitHwnd":"0x00130B70","fg":"w20"}
{"seq":5243,"t":212400800,"tObs":212431000,"type":"element","trigger":"buttonDown","ref":5241,"status":"ok","src":"uia","ms":30,"ep":0,"m":"M1","x":1180,"y":640,"win":"w20","dwellRef":5239,"dwellValid":true,"el":{"ct":"Pane","name":"Eingabeerfassungsfenster","cls":"IHWindowClass","hwnd":"0x00130B70","rect":{"m":"M1","x":208,"y":131,"w":1920,"h":1080},"opaqueHint":true}}
{"seq":5244,"t":212470300,"inSeq":77411,"type":"mouse.up","ep":0,"m":"M1","x":1180,"y":640,"button":"primary","physButton":"left","win":"w20","fg":"w20"}
{"seq":5260,"t":214100000,"inSeq":77460,"type":"key.down","vk":77,"sc":50,"key":"M","hkl":"04070407","ch":"M","mods":["shift"],"locks":[],"fg":"w20","focus":5202,"ctx":"opaque"}
```

```json
{ "id": "s5241", "kind": "click", "action": { "verb": "click", "m": "M1", "x": 1180, "y": 640, "button": "primary", "clicks": 1 },
  "importance": "primary",
  "evidence": { "level": "inputOnly", "input": "observed", "target": "position", "result": "effectObserved", "cause": "n/a",
    "reasons": ["opaqueSurface"] },
  "ctx": { "win": "w20", "process": "mstsc.exe", "title": "srv-app01 – Remotedesktopverbindung", "surface": "opaque", "surfaceKind": "remoteDesktop" },
  "target": { "status": "position" },
  "position": { "ep": 0, "m": "M1", "x": 1180, "y": 640, "inWindow": [980, 540] },
  "effect": { "observed": true, "causality": "plausible", "screen": { "changedRatio": 0.08, "settled": true, "settleReason": "quiet" } },
  "flags": { "opaque": true } }
```

Obwohl beide Element-Ermittlungen übereinstimmen, ist das Ziel **nicht** belegt: Das gefundene Element ist nur die Hülle des Remote-Bildes (`opaqueHint`). `target.status` ist deshalb `position`, ein Elementname wird nicht ausgegeben, und die Textvorlage beschreibt den Klick über Position und Bildausschnitt. Die getippten Zeichen werden als Texteingabe mit Belegstufe `inputOnly` geführt und an jeder Eingabetaste getrennt.

#### B.5 Monitorwechsel während der Aufnahme

**Ablauf.** Ein Notebook hängt an einem externen Monitor (`M2`, Hauptmonitor, links) und wird während der Aufnahme abgedockt. Windows ordnet den Desktop neu an und verschiebt die Fenster auf das eingebaute Display (`M1`).

```json
{"seq":7001,"t":300120000,"type":"gap","gapId":"g4","phase":"begin","cause":"captureLost","tFrom":300120000,"channels":["frames"],"inputLost":"none","detail":{"reason":"modeChange"}}
{"seq":7002,"t":300450000,"type":"display.topology","epoch":1,"reason":"displayChange","virtual":{"x":0,"y":0,"w":1920,"h":1200},"monitors":[{"id":"M1","name":"\\\\.\\DISPLAY1","label":"Integriertes Display","primary":true,"vx":0,"vy":0,"w":1920,"h":1200,"dpi":120,"scale":1.25,"rotation":0,"backend":"dda"}],"removed":["M2"]}
{"seq":7003,"t":300470000,"tObs":300522000,"tSrc":"present","type":"frame","id":"f000351","ep":1,"m":"M1","w":1920,"h":1200,"trigger":"topology","ref":7002,"store":{"kind":"key","blob":"sha256:9d10…4a6e","bytes":412008}}
{"seq":7004,"t":300470000,"type":"gap","gapId":"g4","phase":"end","cause":"captureLost","tFrom":300120000,"tTo":300470000,"channels":["frames"],"inputLost":"none"}
{"seq":7005,"t":300480000,"tObs":300531000,"tSrc":"sys","tErr":16000,"type":"window.rect","win":"w9","ep":1,"rect":{"m":"M1","x":120,"y":80,"w":1400,"h":900},"state":"normal"}
{"seq":7050,"t":305000200,"inSeq":93120,"type":"mouse.down","ep":1,"m":"M1","x":640,"y":412,"button":"primary","physButton":"left","win":"w9","fg":"w9"}
```

In Epoche 0 lag `M1` bei `vx: 2560` rechts neben `M2`; in Epoche 1 liegt derselbe Monitor bei `vx: 0`. Weil alle Koordinaten monitorrelativ sind und `M1` seine Kennung behält, bleibt jede Angabe eindeutig. Die Aufnahme läuft weiter: ein Schlüsselbild eröffnet die neue Epoche, die 350 ms ohne Bilder sind als Lücke ausgewiesen, Eingaben gingen keine verloren. In `steps.json` erscheint ein `system`-Schritt (`event: "topologyChanged"`); der folgende Klick trägt `ctx.ep: 1`.

#### B.6 Aufnahme nur einer Anwendung, Arbeit außerhalb

**Ablauf.** Aufgenommen wird nur Excel. Zwischendurch wechselt der Nutzer für 42 Sekunden zu einem anderen Programm und kehrt zurück.

```json
{"seq":5,"t":0,"type":"scope.change","mode":"app","apps":[{"process":"EXCEL.EXE","match":"process"}],"includeRelated":true,"reason":"initial"}
{"seq":2609,"t":117950000,"type":"gap","gapId":"g2","phase":"begin","cause":"outOfScope","tFrom":117950000,"channels":["keyboard","mouse","elements","clipboard"],"inputLost":"certain"}
{"seq":2610,"t":118200000,"tObs":118214000,"tSrc":"sys","tErr":16000,"type":"window.info","win":"w9","hwnd":"0x00240C1E","kind":"window","inScope":false,"titleWithheld":"outOfScope"}
{"seq":2611,"t":118200000,"tObs":118214100,"tSrc":"sys","tErr":16000,"type":"window.foreground","win":"w9","prev":"w3"}
{"seq":2613,"t":118260000,"tObs":118309000,"tSrc":"present","type":"frame","id":"f000097","ep":0,"m":"M1","w":1920,"h":1080,"trigger":"change","store":{"kind":"delta","base":"f000096","rect":{"x":300,"y":120,"w":512,"h":600},"blob":"sha256:6f02…d7e9","bytes":1904},"scopeRects":[[0,0,812,1040]],"masks":[{"x":300,"y":120,"w":512,"h":600,"why":"occluded"}]}
{"seq":2630,"t":160300000,"tObs":160312000,"tSrc":"sys","tErr":16000,"type":"window.foreground","win":"w3","prev":"w9"}
{"seq":2631,"t":160300000,"type":"gap","gapId":"g2","phase":"end","cause":"outOfScope","tFrom":117950000,"tTo":160300000,"channels":["keyboard","mouse","elements","clipboard"],"inputLost":"certain","detail":{"counts":{"keys":214,"clicks":12,"wheel":30,"moves":1840}}}
{"seq":2632,"t":160301000,"type":"keystate.sync","reason":"afterGap","gapId":"g2","down":[],"mouseDown":[],"locks":[]}
```

Zwischen den Zeilen 2609 und 2631 steht **kein einziges** Eingabeereignis: Was außerhalb getippt und geklickt wurde, erreicht das Journal nicht. Am Ende der Lücke steht nur, wie viele Ereignisse je Art es waren (`detail.counts`) – keine Tasten, keine Positionen, keine Einzelzeiten; die zugehörigen Eingabenummern fehlen im Journal und sind durch die Lücke erklärt. Mitprotokolliert werden Eingaben außerhalb des Aufnahmebereichs nie; eine Einstellung dafür gibt es nicht. Für ausgeschlossene Anwendungen (`excludedApp`) gibt es nicht einmal diese Zähler, sondern allein den Anzeigenamen der Regel – und auch die Eingabenummer steht dort still, damit sich die Zahl der Eingaben nicht aus der Nummernfolge ablesen lässt. Vom fremden Fenster ist nur bekannt, dass es existiert (`inScope: false`), nicht sein Titel. Das Bild `f000097` behält Monitorgröße; echten Inhalt hat nur die Fläche des Excel-Fensters (`scopeRects`), und wo das fremde Fenster Excel verdeckte, ist die Fläche schwarz gefüllt (`occluded`).

In `steps.json` wird daraus ein einziger `gap`-Schritt (42,35 s, `cause: "outOfScope"`, `inputLost: "certain"`). Eine KI weiß damit: In dieser Zeit hat der Nutzer woanders gearbeitet, und was er dort tat, gehört nicht zur Aufzeichnung.

---

<a id="review-abdeckung"></a>
## Abdeckung der Befunde aus dem Architektur-Review

| Befund | Lösung im Format | Abschnitt |
|---|---|---|
| Nr. 3 – Schwärzungen hängen am Schritt, Leser erreichen Rohdaten; Neuberechnung nummeriert um | Verankerung an `seq` und Bild-Kennung statt an Schrittnummern; Bild- und Text-Schwärzung mit Fundstellen; `prov` überträgt Schwärzungen auf abgeleitete Werte; Pflicht zur Anwendung in jeder Ausgabe; festgehaltene Regelversion; `orphans` | [13.1](#edits-json), [13.6](#edits-redactions), [12.12](#steps-json), [15.6](#versionierung) |
| Nr. 5 – Vollständigkeitsprüfung beweist nur Buchführung | `evidence` je Schritt mit getrennten Angaben zu Eingabe, Ziel, Ergebnis, Ursache; Bericht trennt `bookkeeping` von `evidence`/`gaps`; `maskedInput` und `unobserved` zählen als Lücken; Zwischenablage-Herkunft immer; `ui.text` und `ui.value` für Eingaben ohne Tasten | [12.5](#steps-json), [12.10](#steps-json), [12.11](#steps-completeness), [9.16](#ev-clipboard), [9.13](#ev-ui) |
| Nr. 7 – Das Modell weiß nicht, was auf dem Bildschirm stand | `ui.snapshot` für jedes neue Fenster, Menü, Popup; `dupIndex`/`dupCount`, `row`, `url`, `doc` am Element; `effect.focus` | [9.14](#ev-ui-snapshot), [9.1](#ereignistypen), [12.8](#steps-json) |
| Nr. 10 – Bildregeln versagen unter Last und blockieren die Neuberechnung | Ein `frame` je Auslöser, Auslöser unabhängig von Schrittregeln; `same`/`delta`, inhaltsadressiert; `settle.reason: "nextInput"`; `store.kind: "none"` mit Lücke `frameDropped`; `guarantee` | [9.15](#ev-frame), [11](#bildspeicher), [9.19](#ev-gap) |
| Nr. 11 – Zeitsemantik des Journals unbestimmt | `t`, `tObs`, `tSrc`, `tErr`; Zeitachse nach (`t`, `seq`); Bündel enden vor jeder Eingabe; `vk`, `sc`, `hkl`, `ch` an jeder Taste | [6](#ereigniskopf), [7](#zeitmodell), [10](#mausbewegungen), [9.8](#ev-key) |
| Nr. 12 – Koordinaten brechen bei Topologiewechsel | Epochen, stabile Monitor-Kennung, monitorrelative Koordinaten, Bilder in Darstellungsrichtung; `rectIssue` statt falscher Rechtecke | [8](#koordinatenmodell), [9.4](#ev-display-topology), Anhang B.5 |
| Nr. 13 – Undurchsichtige Oberflächen liefern scheinbar sichere Angaben | `surface`/`surfaceKind` am Fenster, `ctx: "opaque"`, `opaqueHint`, `target.status: "position"`; Weg einer Ziehbewegung als Primärdaten (`input.path`) | [9.10](#ev-window), [9.1](#ereignistypen), [12.6](#steps-json), Anhang B.4 |
| Fehlend – Absturzsicherheit und Formatentwicklung | Manifest beim Start; Prüfkette statt Prüfsumme über die ganze Datei; Bilder nach Kennung und Inhalt statt nach Schrittnummer benannt; `steps.json` ohne gerenderte Texte; Verhalten bei vollem Datenträger; Versions- und Migrationsregeln | [4](#manifest), [16](#absturzsicherheit), [11.2](#bildspeicher), [12.1](#steps-json), [15](#versionierung) |
| Fehlend – Anzeige- und Eingabe-Einstellungen | `inputSettings` (vertauschte Maustasten, Doppelklickzeit, Einrastfunktion) im Manifest und als Ereignis; `hdr` an Monitor und Bild | [4.2](#manifest), [9.3](#ev-system), [9.15](#ev-frame) |

---

<a id="anforderungen"></a>
## 19. Anforderungen

| ID | Anforderung | Priorität | Abnahmekriterium |
|---|---|---|---|
| DAT-001 | Eine Sitzung besteht ausschließlich aus offenen Formaten: JSON, JSON Lines (UTF-8 ohne BOM) und PNG. | Muss | Eine Beispielsitzung lässt sich mit Texteditor und Bildbetrachter lesen; `validate` meldet keine Fehler. |
| DAT-002 | Der Sitzungsordner folgt dem Aufbau aus Abschnitt 2; unbekannte Dateien werden ignoriert. | Muss | Sitzung mit zusätzlichen Fremddateien öffnet fehlerfrei. |
| DAT-003 | `.steprec` ist ein ZIP mit `manifest.json` als erstem Eintrag. Gepackt wird nach einer Positivliste (3); `cache/`, `export/` (auch `export/.tmp/`), `history/` und `session.lock` fehlen in jedem Fall. | Muss | Gepackte Sitzung wird ohne vollständiges Entpacken erkannt; Entpacken ergibt einen gültigen Sitzungsordner; das Archiv enthält keinen Eintrag unter `cache/`, `export/` oder `history/`. |
| DAT-004 | `manifest.json` wird beim Aufnahmestart angelegt und stets atomar ersetzt. | Muss | Recorder wird an 100 zufälligen Zeitpunkten hart beendet: Das Manifest ist jedes Mal vollständig lesbar. |
| DAT-005 | Das Journal ist selbsttragend: Manifest und Schritte lassen sich allein aus `events.jsonl` und `frames/` wiederherstellen. | Muss | Nach Löschen von `manifest.json` und `steps.json` erzeugt die Wiederherstellung inhaltsgleiche Dateien (bis auf `history` und Zeitstempel der Ableitung). |
| DAT-006 | Das Journal wird nur angehängt; Zeilen werden nie entfernt oder umsortiert; `seq` ist lückenlos. | Muss | Prüfung F03 besteht für jede Testsitzung, auch nach endgültigem Schwärzen. |
| DAT-007 | Jedes Ereignis trägt den Ereigniskopf mit `seq`, `t`, `type`; `ref` zeigt auf ein früheres Ereignis. | Muss | Prüfungen F02 und F05 bestehen. |
| DAT-008 | Jedes Ereignis unterscheidet Zeitpunkt des Geschehens (`t`) und der Beobachtung (`tObs`) und nennt die Herkunft der Zeit. | Muss | Ein Testjournal, in dem Fenster- und Elementereignisse künstlich um 500 ms verspätet geschrieben werden, ergibt dieselben Schritte wie das unverzögerte. |
| DAT-009 | Koordinaten sind physische Pixel, monitorrelativ, mit Epoche; es gilt „Bildpixel = monitorrelative Koordinate“. | Muss | Klicks auf ein Testmuster bei gemischter Skalierung und gedrehtem Monitor: Die Markierung im Bild liegt höchstens 1 Pixel neben dem geklickten Punkt. |
| DAT-010 | Monitore tragen eine stabile Kennung; jede Änderung der Anordnung eröffnet eine Epoche; die Aufnahme läuft weiter. | Muss | An- und Abstecken eines Monitors während der Aufnahme: Sitzung bleibt gültig, alle Koordinaten sind eindeutig, F07 besteht. |
| DAT-011 | Unplausible Elementrechtecke werden nicht als `rect` gespeichert, sondern mit `rectIssue` ausgewiesen. | Muss | Testanwendung, die skalierte Rechtecke meldet: kein Schritt erhält einen Elementrahmen an falscher Stelle. |
| DAT-012 | Alle 44 Ereignistypen entsprechen den Feldtabellen in Abschnitt 9. | Muss | Jede Zeile jeder Testsitzung besteht die Schemaprüfung ihres Typs. |
| DAT-013 | Jede Fensterkennung wird vor ihrer ersten Verwendung durch `window.info` eingeführt. | Muss | Prüfung F06 besteht. |
| DAT-014 | Jeder Bewegungsmesswert der Maus wird gespeichert; Bündel enden vor jeder anderen Eingabe. | Muss | Geskriptete Bewegung mit 1000 Messwerten je Sekunde über 60 s: Die ausgepackte Anzahl und jede Position stimmen mit der Vorgabe überein; F09 besteht. |
| DAT-015 | Tastenereignisse tragen Tastencode, Scancode, Kennzeichen, Layout und die Übersetzung zum Aufnahmezeitpunkt. | Muss | Aus den gespeicherten Feldern lässt sich die Zeichenfolge für die Layouts Deutsch, US und Französisch (mit Tottasten) ohne Zugriff auf den Aufnahmerechner neu berechnen. |
| DAT-016 | Zurückgehaltene Tasten erscheinen nur als `text.masked` mit Zeitspanne und – sofern die Anzahl gespeichert wird – der Zahl der Tastenanschläge nach der Zählweise aus 9.9 (ohne reine Modifikatoren); ohne Tastencodes und ohne Einzelzeiten. Ist die Anzahl abgeschaltet, fehlt sie überall. | Muss | Ein Test-Passwort mit Großbuchstaben und einer Korrektur wird in ein Passwortfeld getippt: Die Suche im gesamten Sitzungsordner findet es nicht; im betroffenen Zeitraum existieren keine `key.*`-Ereignisse druckbarer Tasten; `count` zählt jede Zeichen- und Korrekturtaste einmal und Umschalt nicht. Mit `storeMaskedCount: false` fehlen `count`, `inSeqEnd` und `events`, und `steps.json` trägt `{ "masked": null }`; die Eingabenummer springt an jeder Grenze der zurückgehaltenen Folge um einen zufälligen Zuschlag. Passwörter von 6 und von 20 Zeichen, getippt bei bewegter Maus und mit einem Klick mitten in der Eingabe: Aus Nummernfolge und Bündelgrenzen lässt sich weder die Zahl der Tasten noch der Zeitpunkt einer einzelnen Taste bestimmen; kein Bündel endet an einer zurückgehaltenen Taste. |
| DAT-017 | Fehlt ein Inhalt aus Datenschutzgründen, nennt das Ereignis den Grund (`withheld`, `valueWithheld`, `titleWithheld`). | Muss | In Testsitzungen mit Passwortfeld, Privatmodus und Ausschlussliste hat jedes inhaltslose Feld einen Grund. |
| DAT-018 | Jede Lücke steht als `gap` mit Ursache, betroffenen Kanälen und `inputLost` im Journal, geschrieben bei Beginn und Ende. Auf eine Lücke `sessionLocked` oder `sessionDisconnected` folgt unmittelbar eine Lücke `paused` mit `session.pause` (`reason: "lock"`), die erst mit der Bestätigung endet. | Muss | Für UAC, Sperre, Pause, Adminfenster, ausgeschlossene Anwendung und Arbeit außerhalb des Bereichs entsteht je ein passendes Paar; F08 besteht. Sperren während der Aufnahme: Im Journal stehen nacheinander die Lücken `sessionLocked` und `paused`; zwischen dem Entsperren und `session.resume` liegt kein Eingabe- und kein Bildereignis. |
| DAT-019 | Jeder Bildauslöser erzeugt genau ein `frame`-Ereignis; die Auslöser hängen nicht von Schrittregeln ab. | Muss | Zu jedem `mouse.down` und jedem Radbeginn existiert ein `frame` mit `trigger: "preInput"`; Neuberechnung mit geänderten Regeln findet für jeden Schritt ein Vorher-Bild. |
| DAT-020 | Bilddaten liegen inhaltsadressiert in `frames/`; Blobs werden einmal geschrieben, vor dem verweisenden Ereignis. | Muss | Prüfung F11 besteht; nach hartem Beenden verweist kein Ereignis auf einen fehlenden Blob. |
| DAT-021 | Schlüssel- und Differenzbilder sind verlustfrei; jedes Bild lässt sich pixelgenau wiederherstellen. | Muss | 1000 zufällig gewählte Bilder einer Testsitzung stimmen nach der Wiederherstellung mit dem unabhängig mitgeschnittenen Original überein (F12). |
| DAT-022 | Differenzketten sind begrenzt; Schlüsselbilder entstehen nach den Regeln aus 11.3. | Muss | Keine Kette überschreitet `maxDeltaChain`; jede Epoche und jeder Abschnitt beginnt mit einem Schlüsselbild. |
| DAT-023 | Flächen außerhalb des Aufnahmebereichs und maskierte Flächen werden vor dem Kodieren schwarz gefüllt und im Ereignis ausgewiesen. | Muss | Aufnahme „eine Anwendung“ vor einem Testmuster: Kein Pixel des Musters findet sich in irgendeinem Blob. |
| DAT-024 | Bilder enthalten keinen Mauszeiger; Position und Form sind getrennt gespeichert. | Soll | Dasselbe Bild lässt sich mit und ohne Zeiger ausgeben. |
| DAT-025 | Jedes Vorher-Bild weist aus, ob seine Darstellungszeit nachweislich vor der Eingabe liegt (`guarantee`). | Muss | Für alle Bilder mit `guarantee: "beforeInput"` gilt `t` < `t` des Ereignisses in `ref`. |
| DAT-026 | Zur Zwischenablage wird die Herkunft immer, der Inhalt nur bei eingeschalteter Option gespeichert; ein Hash des Inhalts wird nie gespeichert. Fehlt der Inhalt, nennt `withheld` den Grund (`optOut`, `sensitiveFormat`, `password`, `strict`, `excludedApp`, `privacyMode`, `tooLarge`). Größenangaben (`size`, `sizes`) sind wahlfrei: Sie stehen nur dabei, soweit sie sich ohne Abruf des Inhalts feststellen lassen oder der Inhalt aufgezeichnet wurde. | Muss | Standardeinstellung: Ein kopierter Testtext findet sich in keinem `clipboard`-Ereignis; `clip`, `kind`, `formats` und `ownerWin` sind vorhanden; der Inhalt wurde nicht abgerufen – eine Testanwendung mit verzögert bereitgestellten Formaten erhält keine Anforderung –, und fehlende Größenangaben sind kein Fehler. Mit eingeschalteter Option: `size` nennt die Zeichenzahl des abgerufenen Texts; Kopieren aus einem Passwortfeld ergibt `withheld: "password"`, in der Stufe „Streng“ `withheld: "strict"`. |
| DAT-027 | `steps.json` ist rein abgeleitet, sprachneutral und reproduzierbar. | Muss | Löschen und Neuableiten mit gleicher Regelversion ergibt eine bis auf `derivedAtUtc` bytegleiche Datei; die Datei enthält keine ausformulierten Sätze. |
| DAT-028 | Schritte tragen eine an das Ankerereignis gebundene Kennung und weisen ihre Eingabeereignisse als `seq`-Bereiche aus. | Muss | Jedes Eingabeereignis einer Testsitzung liegt in genau einem Bereich genau eines Schritts. |
| DAT-029 | Jeder aus dem Journal übernommene Inhalt in einem Schritt hat einen `prov`-Eintrag. | Muss | Prüfung F14 besteht; eine Text-Schwärzung an einem Quellfeld entfernt den Inhalt aus allen abgeleiteten Stellen. |
| DAT-030 | Der ausführliche Vollständigkeitsbericht steht in `steps.json`, die Kurzfassung im Manifest; beide trennen Buchführung und Belegqualität. | Muss | Beide Fassungen stimmen in den gemeinsamen Zahlen überein; eine Sitzung mit UAC-Lücke meldet `bookkeeping.ok: true` **und** mindestens eine Lücke. |
| DAT-031 | Bearbeitungen sind an `seq` oder Bild-Kennung verankert; nach einer Neuberechnung bleibt jeder Eintrag erhalten – zugeordnet oder unter `orphans`. | Muss | Neuberechnung mit Regeln, die Schritte anders zusammenfassen: Anzahl der Einträge in `edits.json` vorher = zugeordnete + verwaiste nachher. |
| DAT-032 | Bearbeiten verändert Rohdaten nie; einzige Ausnahme ist endgültiges Schwärzen. | Muss | Prüfsummen von `events.jsonl` und aller Blobs sind nach einer beliebigen Bearbeitungssitzung im Viewer unverändert. |
| DAT-033 | Schwärzungen (Bildfläche, Textstelle, Zeitraum) sind so gespeichert, dass jede Ausgabe sie anwenden kann und muss. | Muss | Ein geschwärzter Testtext, eine geschwärzte Bildfläche und ein geschwärzter Zeitraum erscheinen in keinem Export, keiner MCP-Antwort, keinem Suchergebnis und keiner Kopie in die Zwischenablage. |
| DAT-034 | Endgültiges Schwärzen entfernt Inhalte aus Journal und Blobs, ohne Zeilen oder `seq` zu ändern; die angewendeten Einträge bleiben als Vermerk ohne Inhalt stehen, `history` hält den Vorgang fest. | Muss | Nach endgültigem Schwärzen (an Ort und Stelle und als bereinigte Kopie) findet die Suche den Testinhalt in keiner Datei des Sitzungsordners; `validate` besteht. |
| DAT-035 | Die Darstellungswahl (ganzer Bildschirm, Fenster, Ausschnitt) ist je Sitzung und je Schritt in `edits.json` gespeichert und verändert keine Bilder. | Muss | Umschalten der Darstellung ändert keinen Blob; der Export folgt der Wahl. |
| DAT-036 | Gleichzeitiges Schreiben von `edits.json` wird über `revision` erkannt. | Soll | Viewer und MCP-Server ändern gleichzeitig: Keine Änderung geht verloren. |
| DAT-037 | `cache/` ist jederzeit löschbar und wird nach endgültigem Schwärzen geleert. | Muss | Sitzung öffnet nach Löschen von `cache/` unverändert; nach endgültigem Schwärzen ist der Ordner leer. |
| DAT-038 | Jede Datei trägt `schemaVersion`; Leser ignorieren Unbekanntes und bewahren es beim Schreiben. | Muss | Testsitzung mit unbekannten Feldern, Typen und Aufzählungswerten öffnet; nach Speichern von `edits.json` und `manifest.json` sind die unbekannten Felder noch da. |
| DAT-039 | Migration arbeitet nie am Original. | Muss | Nach einer Migration ist die Quellsitzung bytegleich. |
| DAT-040 | Die Regelversion einer Sitzung bleibt festgehalten; neu berechnet wird nur auf ausdrücklichen Wunsch, die vorige Fassung bleibt als Sicherung. | Muss | Öffnen mit neuerer Programmversion ändert `steps.json` nicht; nach Neuberechnung existiert `steps.<alte Version>.json`. |
| DAT-041 | Nach hartem Beenden des Recorders gehen höchstens die letzten 250 ms verloren, nach Stromausfall höchstens ein Checkpoint-Abstand; gültige Zeilen bleiben unverändert. | Muss | 100 harte Abbrüche an zufälligen Stellen: Wiederherstellung gelingt jedes Mal, Verlust innerhalb der Grenze. |
| DAT-042 | Checkpoints tragen eine Prüfkette über das Journal. | Soll | Eine absichtlich beschädigte Zeile wird von F13 gemeldet. |
| DAT-043 | Bei vollem Datenträger entsteht die festgelegte Ereignisfolge mit Lücke; die Notreserve sichert den Abschluss. | Muss | Aufnahme auf ein volles Testlaufwerk: Sitzung ist gültig, enthält `gap` mit `cause: "diskFull"`. |
| DAT-044 | `validate` führt die Prüfungen F01–F18 aus (zu F19–F25 siehe DAT-073). | Muss | Für jede Prüfung existiert eine Testsitzung, die sie verletzt und erkannt wird. |
| DAT-045 | Vollständige JSON-Schemata werden aus den Datentypen des Programms erzeugt und veröffentlicht. | Soll | Schemata liegen unter `docs/schema/`; alle Beispiele dieses Dokuments bestehen sie. |
| DAT-046 | Das Format erlaubt mehrere Aufnahmeabschnitte je Sitzung; Leser verarbeiten sie. | Soll | Eine Testsitzung mit zwei Abschnitten und unterschiedlicher Monitor-Anordnung öffnet und ergibt eine durchgehende Zeitachse. |
| DAT-047 | Rechnername, Benutzername und Profilpfade werden nur nach ausdrücklicher Einstellung gespeichert. | Muss | Standardeinstellung: Die Suche nach Benutzer- und Rechnername findet in Manifest und `window.info.exe` nichts. |
| DAT-048 | Größengrenzen werden eingehalten: Zeile höchstens 1 MiB, Werte höchstens `valueMaxChars`, Schnappschüsse höchstens `snapshotMaxNodes`. | Muss | Prüfung F17 besteht für eine Sitzung mit sehr großen Dokumenten und Fenstern. |
| DAT-049 | Text-Schnappschüsse sind als flache, begrenzte Knotenliste gespeichert. | Muss | Für jeden in einer Testsitzung geöffneten Dialog existiert ein `ui.snapshot`, dessen Knoten den Wortlaut der Meldung enthalten. |
| DAT-050 | Erweiterungen Dritter verwenden die Präfixe `x_` und `x.` und stören keinen Leser. | Kann | Sitzung mit `x.`-Ereignissen und `x_`-Feldern besteht `validate`. |
| DAT-051 | Alle Eingabeereignisse aus dem Hook (`mouse.move`, `mouse.down`, `mouse.up`, `mouse.wheel`, `key.*`, `text.masked`) tragen die Eingabenummer `inSeq`, vergeben von der Eingabeverarbeitung in der Reihenfolge des Capture-Hosts; jede fehlende Nummer ist durch eine Lücke oder ein `text.masked` erklärt. Für Eingaben, deren Fenster-Stempel zu einer ausgeschlossenen Anwendung gehört, wird keine Nummer vergeben. | Muss | Lasttest mit 1000 Messwerten je Sekunde und schnellem Tippen: Prüfung F18 meldet keine unerklärte Lücke. Tippen in einer ausgeschlossenen Anwendung: Die letzte Nummer vor und die erste nach der Lücke `excludedApp` folgen unmittelbar aufeinander. |
| DAT-052 | Extern bearbeitete Bilder liegen in `assets/` und sind in `edits.json` verzeichnet; Schwärzungen des Ausgangsbilds lassen sich über sie nicht umgehen. | Soll | Nach Anlegen einer Schwärzung auf dem Ausgangsbild erscheint der zuvor erzeugte Bildersatz in keinem Export unverändert. |
| DAT-053 | Das Manifest hält das Ergebnis des Unsichtbarkeits-Selbsttests und den Ausweichmodus fest. | Muss | Aufnahme im Ausweichmodus: `manifest.selfTest.invisibility.mode` ist `fallbackHidden`. |
| DAT-054 | Die Schwärzung eines Zeitraums ist als `range` mit `seq`-Bereichen gespeichert; ein ganzer Schritt wird beim Anlegen auf seine `seq`-Bereiche aufgelöst. | Muss | Ein geschwärzter Schritt bleibt nach einer Neuberechnung mit Regeln, die die Schritte anders schneiden, vollständig geschwärzt; kein Inhalt seiner Ereignisse erscheint in einer Ausgabe. |
| DAT-055 | Die Platzhalter sind einheitlich: Textstelle `[geschwärzt]` bzw. `[redacted]`, Schritt „Schritt geschwärzt“ bzw. “Step redacted”, Zeitraum als Lücke `removedByAuthor`, Bildfläche schwarz gefüllt. Die Länge des Geschwärzten wird nie ausgegeben. | Muss | Alle Ausgabewege (Anleitung, Kopie, KI-Paket, MCP) zeigen für dieselbe Schwärzung denselben Platzhalter; keiner enthält eine Längenangabe oder einen abweichenden Ersatztext. |
| DAT-056 | `edits.json` hält den Stand der Durchsicht je Eingabe fest – für Tippläufe, Feldinhalte, Zwischenablage-Inhalte, Kommentare, Ziel-Angaben und Marker-Texte; ungeprüfte auffällige Eingaben lassen sich daraus für Wege ohne Oberfläche als `{unreviewed:N}` ausgeben. N zählt bei Tippläufen Tastenanschläge (wie `text.masked.count`), sonst Zeichen. Einen Schalter dagegen gibt es nicht. | Muss | Sitzung mit einer Terminal-Eingabe: vor der Durchsicht liefern MCP-Server und Kommandozeilen-Export `{unreviewed:N}`, nach `status: "kept"` den Klartext; eine nachträglich angehängte Aufnahme ist wieder ungeprüft. Für „Abc“, mit Umschalt getippt, ist N = 3; für einen auffälligen Feldinhalt mit 20 Zeichen ist N = 20. |
| DAT-057 | Lückenmarker tragen die festgelegten Angaben: bei `excludedApp` den Anzeigenamen der Regel und keine Zähler (auch `inSeq` zählt dort nicht weiter), bei `outOfScope` höchstens Ereigniszahlen je Art, bei `privacyMode` die verbindliche Kanalliste (`keyboard`, `clipboard`, `fieldValues`, bei ausgesetzten Bildern zusätzlich `frames`) und am Ende den Endgrund. Eingaben außerhalb des Aufnahmebereichs werden nie aufgezeichnet. | Muss | Aufnahme mit ausgeschlossener Anwendung und mit Arbeit außerhalb des Bereichs: `gap.detail` enthält genau diese Angaben; Programmname und Fenstertitel der ausgeschlossenen Anwendung finden sich nirgends in der Sitzung; zwischen Beginn und Ende einer Lücke `outOfScope` steht kein Eingabeereignis. Terminal-Passwortabfrage, mit Strg+C abgebrochen: Der Endgrund `end` ist `cancelCtrlC`. |
| DAT-058 | `text.masked` kennt alle Gründe aus Abschnitt 9.9; bei `terminalPrompt` stehen Regel und erkannte Abfragezeile dabei. | Muss | Für jeden Grund existiert eine Testsitzung; die Passwortabfrage im Terminal ergibt `rule` und `promptLine`, die getippten Zeichen fehlen in der ganzen Sitzung. |
| DAT-059 | Texte, die Autor oder Agent für einen Schritt setzen, tragen je Sprache Kurztitel, Text, Herkunft (`user` oder `mcp` mit Client) und Zeit; frühere Fassungen bleiben erhalten. | Soll | Ein über `set_step_text` geschriebener Text ist im Viewer als Agententext erkennbar und lässt sich auf die vorige Fassung zurücksetzen. |
| DAT-060 | `edits.json` bildet ab: Rücknahme von „verworfen“, Änderung und Ausblenden von Kommentaren aus der Aufnahme, Spotlight-Markierung, Bild-Schwärzung, die einem Element oder Fenster folgt. Interne **und** ausgeblendete Kommentare erscheinen in keiner Ausgabe. | Muss | Jede dieser Bearbeitungen lässt sich speichern, neu laden und wirkt in Anleitung und KI-Paket; das Journal bleibt bytegleich; der Text eines Kommentars mit `kind: "internal"` und eines Kommentars mit `hidden: true` – auch eines aus der Aufnahme – findet sich in keiner Ausgabe: weder in Anleitung, KI-Paket und MCP-Antworten noch in Rohereignissen und Suchtreffern; vom Kommentar aus der Aufnahme bleibt nur der Schritt mit Platzhalter. |
| DAT-061 | „Letzte Eingabe schwärzen“ ist im Format abgebildet: `marker` mit `kind: "redactLastInput"` im Journal, Zeitraum-Schwärzung mit `trigger: "lastInput"` in `edits.json`, endgültige Anwendung beim Stopp und – nach einem Absturz – bei der Wiederherstellung. Strukturereignisse bleiben erhalten. | Soll | Kanarie tippen, Befehl auslösen, weiterarbeiten, stoppen: Die Bytesuche findet die Kanarie in keiner Datei des Sitzungsordners; an ihrer Stelle steht eine Lücke `removedByAuthor`; `seq` ist lückenlos, alle Formatprüfungen bestehen, `history` nennt `redactedDestructive` mit `trigger: "lastInput"`. Dasselbe nach hartem Beenden vor dem Stopp und anschließender Wiederherstellung – auch wenn `edits.json` zuvor gelöscht wurde. |
| DAT-062 | Zwischendateien liegen ausschließlich in `export/.tmp/` des Sitzungsordners, enthalten nur die geschwärzte Sicht und sind flüchtig; der Ordner gehört nicht zur Sitzung. | Muss | Extern bearbeiten, als Datei kopieren, Herausziehen und Exportvorschau erzeugen Dateien nur unter `export/.tmp/`; nach dem Schließen der Sitzung und nach Absturz mit erneutem Öffnen ist der Ordner leer; `validate` und das Packen ignorieren ihn. |
| DAT-063 | `edits.json` kann eine Sitzung von der Bereitstellung über den MCP-Server ausnehmen (`sharing.mcp: false`). | Kann | Eine so gekennzeichnete Sitzung erscheint nicht in `list_sessions`; Viewer und Exporte bleiben unberührt. |
| DAT-064 | Das Ziel der Aufnahme (`goal` mit `description`, `expected`, `reference`) steht wahlfrei im `session.start` des ersten Abschnitts und im Manifest; Änderungen aus dem Viewer stehen als `edits.goal` im Overlay. Die Angaben sind Autorentext: Durchsicht, Schwärzung und Verallgemeinerung gelten für sie. | Soll | Aufnahme mit und ohne Ziel-Angaben: Beide Sitzungen sind gültig. Nach Löschen des Manifests stellt die Wiederherstellung `goal` aus dem Journal wieder her. Eine im Viewer geleerte Angabe erscheint in keiner Ausgabe; eine Ziel-Angabe mit Mustertreffer erscheint vor der Durchsicht über MCP-Server und Kommandozeilen-Export nur als `{unreviewed:N}`. |
| DAT-065 | Marker tragen einen Typ: `marker.kind` kennt neben den Befehlen `note`, `error`, `expected` und `important`; der Schritt trägt den Typ als `input.markerKind`, das Overlay kann ihn ändern (`steps[].markerKind`). Der frühere Wert `marker` gilt als `note`. Zu jedem Marker des Autors gehört ein Bild (`trigger: "manual"`). | Soll | Je Typ ein Marker gesetzt: vier `marker`-Schritte mit dem jeweiligen `markerKind`, jeder mit seinem Bild. Ein Journal mit `kind: "marker"` ergibt `markerKind: "note"`. Nach Ändern des Typs im Viewer ist das Journal bytegleich. |
| DAT-066 | Manifest und Sitzungskopf halten das Aufnahmeprofil fest: Name, Kennung mitgelieferter Profile, die wirksamen Einstellungen und – falls zutreffend –, worin die Aufnahme schwächer schützt als der Standard. | Soll | Aufnahme mit dem Profil „Support-Fall“ und im Startfenster geänderter Schutzstufe: `profile.effective` nennt die tatsächlich wirksamen Werte und widerspricht `settings` nicht (F21); bei schwächerem Schutz steht der Punkt in `weakens`. Eine Sitzung ohne `profile` ist gültig. |
| DAT-067 | Abschnitte tragen ihren Zweck; eine Neuaufnahme nennt im Sitzungskopf den Bereich, den sie ersetzen soll. Ein Eintrag in `edits.replacements[]` lässt sie an die Stelle der früheren Schritte treten, ohne Rohdaten zu ändern; die Aufzeichnungssteuerung legt ihn unmittelbar nach dem sauberen Stopp mit den Werten aus `session.start.replaces` an. Entfernen des Eintrags stellt den früheren Stand her. | Soll | Drei Schritte neu aufgenommen und ersetzt: Schon beim ersten Öffnen im Viewer steht der Eintrag in `edits.json`, mit denselben Werten wie `session.start.replaces`; nach einem Absturz der Neuaufnahme steht keiner dort. Journal und `steps.json` enthalten beide Fassungen, die Anleitung zeigt nur die neue. Nach Entfernen des Eintrags zeigt sie wieder die alte, und die Neuaufnahme erscheint in keiner Anleitung. F22 besteht in allen drei Zuständen; Prüfsummen von Journal und Blobs sind unverändert. |
| DAT-068 | Verallgemeinerungen sind als Regeln „Wert → Platzhalter“ gespeichert (`match.literal` oder `match.pattern`, `placeholder`, `caseSensitive`, `scope`, `targets`) und werden von der Leseschicht in jeder Ausgabe angewendet: in Text als `{Name}`, in getipptem Text als eigenes Token, betroffene Tasten ohne Zeichen, Bildflächen gefüllt und beschriftet. | Soll | Kanarie als Benutzername in Fenstertitel, Feldinhalt, getipptem Text und Kommentar: Mit der Regel findet sich die Kanarie in keiner Ausgabe – Anleitung, KI-Paket, MCP-Server einschließlich Rohereignissen und Suche, Kopie aus dem Viewer; überall steht `{Benutzername}`. Bei der Regel `max` bleibt `maximal` unverändert. |
| DAT-069 | Endgültiges Anwenden einer Verallgemeinerung entfernt den Wert aus Journal, Blobs **und aus der Regel selbst**; bis dahin ist `edits.json` so vertraulich wie die Sitzung. | Soll | Nach bereinigter Kopie und nach Anwenden an Ort und Stelle: Bytesuche nach der Kanarie in allen Dateien der Sitzung einschließlich `edits.json` ohne Treffer; der Eintrag trägt `applied: true` und kein `match`; `validate` besteht. |
| DAT-070 | `edits.qualityIgnores[]` hält ignorierte Fundstellen der Qualitätsprüfung fest – bestimmt durch Regel, Anker und Schlüssel, ohne aufgezeichneten Inhalt. Ignorieren hebt keine Schutzregel auf. | Soll | Eine ignorierte Fundstelle erscheint nach erneutem Öffnen nicht mehr und nach Entfernen des Eintrags wieder. Ist eine Fundstelle zu ungeprüften Eingaben ignoriert, bleibt die Exportsperre des Viewers bestehen. |
| DAT-071 | Der Suchindex liegt je Sitzung unter `cache/search/`, trägt eine Formatversion und entsteht ausschließlich aus der Sicht der Leseschicht; er wird verworfen, sobald sich diese Sicht ändert. | Soll | Sitzung mit geschwärztem Text, maskierter Eingabe, ungeprüfter auffälliger Eingabe, internem Kommentar und einer Verallgemeinerung: Die Bytesuche in `cache/search/` findet keinen dieser Inhalte. Nach einer neuen Schwärzung ist der Ordner gelöscht, bevor `edits.json` gespeichert ist. Nach Löschen von `cache/` liefert die Suche dieselben Treffer. |
| DAT-072 | Schutzrelevante Bereiche von `edits.json` werden nie stillschweigend überlesen: Versteht ein Leser dort etwas nicht, erzeugt er aus der Sitzung keine Ausgabe. | Muss | `edits.json` mit einer erfundenen Art von Schwärzung und mit einem unbekannten Feld in `substitutions`: Export, Kopie, MCP-Server und Indexbau verweigern und nennen die nötige Programmversion; der Viewer zeigt die Sitzung an; F15 meldet einen Fehler. |
| DAT-073 | `validate` prüft die Erweiterungen mit F19 bis F25; Sitzungen ohne sie bestehen unverändert. | Soll | Für jede dieser Prüfungen existiert eine Testsitzung, die sie verletzt und gemeldet wird; alle Testsitzungen ohne die neuen Angaben bestehen weiter. |
| DAT-074 | Ein verworfener Aufnahmeabschnitt wird markiert, nie entfernt: `session.stop` mit `reason: "discarded"` und ein Eintrag in `edits.sections[]`. Das Journal wird nicht gekürzt. Die Leseschicht hält den Abschnitt aus allen Ausgaben heraus; dauerhaft entfernt wird er erst durch die bereinigte Kopie. „Aufnahme verwerfen“ in einem weiteren Abschnitt betrifft nur diesen. | Soll | Neuaufnahme starten, Kanarie tippen, „Aufnahme verwerfen“: Die früheren Abschnitte und ihre Prüfsummen sind unverändert, `seq` ist lückenlos, es gibt keinen Eintrag in `replacements`. Die Kanarie findet sich in keiner Ausgabe – Anleitung, KI-Paket in jeder Detailstufe, MCP einschließlich Rohereignissen und Suche, Suchindex, Vergleich –, auch nicht nach Löschen von `edits.json`. In der bereinigten Kopie findet die Bytesuche sie in keiner Datei. |
| DAT-075 | Die Leseschicht ergänzt an jedem ausgegebenen Schritt `state` (`active`, `replaced`, `unused`) und bei wirksamen Schritten die Schrittnummer `number`; beide stehen nie in der `steps.json` der Sitzung. | Soll | Sitzung mit einer Ersetzung: Im KI-Paket und über den MCP-Server trägt jeder Schritt `state`; die wirksamen sind lückenlos ab 1 nummeriert, in jeder Detailstufe gleich; ersetzte tragen keine Nummer. Ein einzeln verworfener Schritt ist `active` und nummeriert. Die `steps.json` der Sitzung enthält keines der beiden Felder. |
| DAT-076 | Über Abschnittsgrenzen laufen `seq`, Sitzungsuhr, Eingabenummer `inSeq`, Epochen und Kennungen fort. Das Salz für die Wiedererkennung von Monitoren steht als `deviceSalt` im Sitzungskopf jedes Abschnitts; Leistungszähler werden je Abschnitt geführt. | Soll | Sitzung mit drei Abschnitten, der zweite verworfen: Keine `inSeq` kommt doppelt vor (F18, F22); derselbe Monitor trägt in allen Abschnitten dieselbe Kennung und denselben `deviceKey`; `manifest.performance` hat je sauber beendetem Abschnitt genau einen Eintrag, der dem `session.stop` entspricht. |
| DAT-077 | Ein fremdes `.steprec` wird nur nach der Positivliste und mit festen Grenzen entpackt: erlaubte Namen, keine Pfadsprünge, Laufwerke oder Gerätenamen, höchstens 200 000 Einträge, 2 GiB je Eintrag, 20 GiB insgesamt, Kompressionsverhältnis höchstens 250 bei Einträgen über 1 MB; mitgezählt werden die tatsächlich geschriebenen Bytes. Entpackt wird über einen Zwischenordner; bei jedem Fehler bleibt nichts zurück. | Muss | Präparierte Archive (Pfadsprung, absoluter Pfad, Gerätename, doppelter Eintrag, Zip-Bombe, falsche Größenangabe, fehlendes Journal): jedes wird abgelehnt, am Ziel liegt weder ein Ordner noch eine Datei; Rundreise Packen → Entpacken ergibt gleiche Prüfsummen für Journal, Blobs und Overlay |
| DAT-078 | `history/` hält Bearbeitungsstände von `edits.json` (Dateiname `edits-<UTC>-r<Revision>-<Anlass>.json`): beim ersten Speichern nach dem Öffnen, danach höchstens alle 10 Minuten, erzwungen vor „Alle ersetzen“, „Zuschnitt auf alle“, „Vorlage anwenden“ und Wiederherstellen; höchstens 40 Stände und 15 MB. Wiederherstellen ist eine normale Bearbeitung und lässt Schwärzungen, Verallgemeinerungen und Durchsicht standardmäßig auf dem heutigen Stand. Endgültiges Schwärzen löscht alle Stände; `.steprec` und bereinigte Kopie enthalten keine. | Muss | Tests zu Intervall, Deduplizierung nach Revision, Obergrenzen, Schutz beim Wiederherstellen (alter Stand ohne Schwärzung stellt keine Schwärzung ab), Revision steigt weiter, Löschen nach endgültigem Schwärzen; Kanarienwert nach endgültigem Schwärzen in keiner Datei unter `history/` |
| DAT-079 | `edits.merges[]` fasst aufeinanderfolgende Schritte für Anleitungen zusammen (`id`, `anchors`, optional `text` je Sprache). Schritte, Journal und `steps.json` bleiben unverändert; KI-Paket, MCP, Vergleich und AutoHotkey zeigen weiter jeden Einzelschritt. | Soll | Zusammenfassen und Aufheben ändern keine Rohdatei und keine `steps.json`; die Anleitung zeigt einen Schritt, das KI-Paket alle mit einer Hinweiszeile; „Alle ersetzen“ erfasst den Gruppentext |
| DAT-080 | Der sichere Desktop ist eine Lücke `secureDesktop` mit `detail.kind` `uac` oder `other` (im `end`-Ereignis), eingeleitet durch `system`/`desktopSwitch`; danach folgen `keystate.sync` (`reason: "secureDesktop"`) und ein erzwungenes Schlüsselbild. Während der Lücke und bei gesperrtem Rechner oder Ruhezustand werden keine Bilder aufgenommen; kein Nachher-Bild wird aus einer solchen Lücke gebunden. | Muss | Synthetische UAC-Lücke: Schritt mit `desktopKind: "uac"`, ohne Bilder; Validator fehlerfrei; Zustandslogik: Eintritt sofort, Rückkehr erst nach zwei Lesungen von `Default`; keine eigene Lücke bei gesperrtem Rechner |
| DAT-081 | `goal.statedBy` kennt `author` (Standard, Feld fehlt) und `agent`. Ein Agent-Ziel entsteht nur nach sichtbarer Zustimmung am Rechner und wird in Manifest, `get_session` und im Kopf von `trace.md` als vom Agenten formuliert gekennzeichnet. | Muss | Aufnahme nach einer Agent-Anfrage: Manifest `goal.statedBy: "agent"`; `trace.md` nennt „vom KI-Agenten formuliert, vom Menschen bestätigt“ |
| DAT-082 | Der Oberflächenzustand `StepRecorder.state.json` und die Dateien unter `requests/` liegen im Zustandsordner, nicht in der Sitzung. Der Zustand enthält keine aufgezeichneten Inhalte; unbekannte Schlüssel bleiben erhalten; eine unlesbare Datei kostet nur den Zustand, nie eine Aufnahme. Anfragen tragen eine Kennung nach dem Muster `rq-<12 Hex>`; abgeschlossene Zustände werden nie überschrieben. | Muss | Zustandsdatei mit unbekanntem Schlüssel und mit zerstörtem Inhalt; Zustandsübergänge der Anfrage inklusive Frist von 120 s; ungültige Kennung führt zu keinem Dateizugriff |
| DAT-083 | Vorschaubilder und Kataloge (`cache/thumbs/`, `cache/catalog.json`) entstehen nur über die Leseschicht, enthalten also alle Schwärzungen, und werden bei jedem Speichern von `edits.json` verworfen. Für laufende Aufnahmen und Archive entsteht kein Zwischenspeicher. | Muss | Vorschaubild einer Sitzung mit geschwärzter Bildfläche: Fläche schwarz; nach Änderung der Schwärzung kein altes Vorschaubild mehr |
