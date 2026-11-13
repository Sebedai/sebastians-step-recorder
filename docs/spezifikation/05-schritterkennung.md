# 05 · Schritterkennung

Status: Entwurf 0.1 · Stand: 2026-11-11

Dieses Dokument legt fest, wie aus dem Rohprotokoll einer Sitzung **Schritte** werden: welche Schrittarten es gibt, nach welchen Regeln Rohereignisse zusammengefasst werden, wie Ziel, Wirkung und Bilder zugeordnet werden, wann ein Schritt als belegt gilt, wie daraus Sätze in Deutsch und Englisch entstehen und wie die Vollständigkeit geprüft wird. Der Aufbau der Dateien – `events.jsonl` als Eingang, `steps.json` als Ergebnis – steht im [Datenformat](04-datenformat.md); wie die Ereignisse entstehen, im [Aufzeichnungskern](03-aufzeichnungskern.md).

## Inhalt

1. [Grundsätze](#grundsaetze)
2. [Ablauf der Ableitung](#pipeline)
3. [Parameter](#parameter)
4. [Vorbereitung: Zeitachse, Zustände, späte Daten](#spaete-daten)
5. [Schrittarten und ihre Regeln](#schrittarten)
6. [Zielbestimmung](#zielbestimmung)
7. [Wirkungserkennung](#wirkungserkennung)
8. [Bildzuordnung](#bildzuordnung)
9. [Belegstufen](#belegstufen)
10. [Wichtigkeitsstufen](#wichtigkeitsstufen)
11. [Aktionsvokabular](#aktionsvokabular)
12. [Textvorlagen](#textvorlagen)
13. [Vollständigkeitsprüfung](#vollstaendigkeitspruefung)
14. [Mitlaufende Erkennung](#mitlaufend)
15. [Regel-Versionierung und Neuberechnung](#regelversion)
16. [Wirksame Schrittfolge: Ersetzungen und Abschnitte](#wirksame-schrittfolge)
17. [Schrittabgleich zweier Sitzungen](#schrittabgleich)
18. [Kriterien für die Qualitätsprüfung](#qualitaetskriterien)
19. [Abdeckung der Review-Befunde](#review-abdeckung)
20. [Anforderungen](#anforderungen)

---

<a id="grundsaetze"></a>
## 1. Grundsätze

| Nr. | Grundsatz | Bedeutung |
|---|---|---|
| S1 | **Reine Funktion.** | Schritte = f(Journal, Regelversion, Parameter). Die Ableitung greift auf nichts anderes zu – nicht auf den Bildschirm, nicht auf die Uhr, nicht auf Einstellungen des Rechners, auf dem sie läuft. Was sie über den Aufnahmerechner wissen muss, steht im Journal. |
| S2 | **Wiederholbar.** | Gleiches Journal, gleiche Regelversion, gleiche Parameter ergeben dieselbe `steps.json`, Byte für Byte (bis auf `derivedAtUtc`). |
| S3 | **Nichts geht verloren.** | Jedes Eingabeereignis gehört am Ende zu genau einem Schritt. Eine unsichere Deutung senkt Belegstufe oder Wichtigkeit, entfernt aber nie Daten. |
| S4 | **Nichts wird erfunden.** | Benannt wird nur, was im Journal steht. Fehlt ein Name, wird über Position und Bildausschnitt beschrieben. Vermutungen (etwa die übliche Bedeutung einer Tastenkombination) sind als solche gekennzeichnet. |
| S5 | **Zeit statt Reihenfolge.** | Zugeordnet wird über den Zeitpunkt des Geschehens (`t`), nie über die Reihenfolge der Zeilen oder den Zeitpunkt der Beobachtung (`tObs`). |
| S6 | **Sprachneutrales Ergebnis.** | `steps.json` enthält strukturierte Daten. Sätze entstehen erst beim Anzeigen und Exportieren aus Vorlagen. |
| S7 | **Nur Metadaten.** | Die Ableitung liest keine Bilddaten. Für Bildänderungen genügen die Angaben am `frame`-Ereignis. Das hält sie schnell und macht sie unabhängig von Bildbibliotheken. |

---

<a id="pipeline"></a>
## 2. Ablauf der Ableitung

```
events.jsonl
   │
   ▼
 1 Laden und prüfen ──► 2 Zeitachse und Zustandsverläufe ──► 3 Eingaben normalisieren
                                                                   │
   ┌───────────────────────────────────────────────────────────────┘
   ▼
 4 In Schritte teilen ──► 5 Ziel binden ──► 6 Wirkung binden ──► 7 Bilder binden
                                                                   │
   ┌───────────────────────────────────────────────────────────────┘
   ▼
 8 Belegstufe und Wichtigkeit ──► 9 Zeitleiste und Vollständigkeit ──► steps.json
```

| Stufe | Aufgabe | Abschnitt |
|---|---|---|
| 1 Laden und prüfen | Journal lesen, Formatprüfungen (F-Prüfungen des [Datenformats](04-datenformat.md#absturzsicherheit)); Fehler werden zu Warnungen, die Ableitung bricht nicht ab. | [13](#vollstaendigkeitspruefung) |
| 2 Zeitachse und Zustände | Sortieren nach (`t`, `seq`); Verläufe für Fenster, Fokus, Tastenzustand, Aufnahmebereich, Lücken aufbauen. | [4](#spaete-daten) |
| 3 Eingaben normalisieren | Tastendrücke paaren, Mausdrücke bilden, Radereignisse gruppieren, Bewegungen auspacken. | [5](#schrittarten) |
| 4 In Schritte teilen | Regeln je Schrittart anwenden; jedes Eingabeereignis genau einem Schritt zuweisen. | [5](#schrittarten) |
| 5 Ziel binden | Aus den Element-Ermittlungen das Ziel wählen und einstufen. | [6](#zielbestimmung) |
| 6 Wirkung binden | Folgeereignisse im Wirkfenster dem Schritt zuordnen; übrige werden Systemereignisse. | [7](#wirkungserkennung) |
| 7 Bilder binden | Vorher-, Nachher- und Zwischenbilder zuordnen. | [8](#bildzuordnung) |
| 8 Belegstufe und Wichtigkeit | Einstufen nach festen Kriterien. | [9](#belegstufen), [10](#wichtigkeitsstufen) |
| 9 Zeitleiste und Vollständigkeit | Zeitleiste bilden, Lückenliste und Bericht berechnen. | [13](#vollstaendigkeitspruefung) |

**Begriffe**

| Begriff | Bedeutung |
|---|---|
| Eingabeereignis | `mouse.move`, `mouse.down`, `mouse.up`, `mouse.wheel`, `key.down`, `key.up`, `text.masked` ohne `self: true`. Nur diese zählen in der Buchführung. |
| Ankerereignis | Das Ereignis, das einen Schritt begründet. Sein `seq` ist `anchorSeq` und bestimmt die Schritt-Kennung. |
| Aktionsschritt | Schritt, der auf Nutzereingaben beruht: `click`, `doubleClick`, `tripleClick`, `rightClick`, `drag`, `scroll`, `type`, `hotkey`, `key`, `hover`, `windowSwitch`. |
| Wirkfenster | Zeitraum nach einer Eingabe, in dem Folgeereignisse als ihre Wirkung gelten ([7](#wirkungserkennung)). |
| Druck | Ein `mouse.down` mit dem zugehörigen `mouse.up` derselben Taste und allen Bewegungen dazwischen. |

---

<a id="parameter"></a>
## 3. Parameter

Alle Schwellwerte der Ableitung sind benannte Parameter. Die tatsächlich verwendeten Werte stehen in `steps.json` unter `params`; damit ist jede Ableitung nachvollziehbar und wiederholbar. Werte mit der Quelle „Sitzung“ kommen aus `inputSettings` der Aufnahme ([Datenformat](04-datenformat.md#manifest)) – nicht vom Rechner, auf dem abgeleitet wird.

| Parameter | Standard | Quelle | Bedeutung |
|---|---|---|---|
| `doubleClickTimeMs` | 500 | Sitzung | Höchstabstand zweier Tastendrücke eines Mehrfachklicks. |
| `doubleClickWidth`, `doubleClickHeight` | 4, 4 | Sitzung | Größe des Rechtecks, in dem ein Folgeklick liegen muss. |
| `dragWidth`, `dragHeight` | 4, 4 | Sitzung | Bewegung in Pixeln, ab der aus einem Druck ein Ziehen wird. |
| `wheelScrollLines` | 3 | Sitzung | Zeilen je Raste. |
| `stickyKeys`, `filterKeys` | false | Sitzung | Schalten Sonderregeln ein ([5.14](#sonderfaelle)). |
| `longPressMs` | 500 | Regel | Ab dieser Dauer gilt ein Druck als lang. |
| `scrollGroupGapMs` | 400 | Regel | Höchstabstand zwischen Radereignissen einer Scrollbewegung. |
| `repeatMergeGapMs` | 1000 | Regel | Höchstabstand, in dem gleiche Tasten oder Tastenkombinationen zu einem Schritt mit Anzahl verschmelzen. |
| `charRunGapMs` | 1000 | Regel | In einem Nicht-Textfeld: Höchstabstand, in dem einzelne Zeichentasten als zusammengehörige Eingabe gelten. |
| `tabFocusMs` | 300 | Regel | Zeit, in der auf eine Tab-Taste ein Fokuswechsel folgen muss, damit sie als Feldwechsel gilt. |
| `hoverMinMs` | max(500, `hoverTimeMs` der Sitzung) | Regel | Mindestdauer eines Verweilens. |
| `hoverRadiusPx` | 8 | Regel | Radius, in dem der Zeiger beim Verweilen bleiben muss. |
| `hoverEffectMs` | 500 | Regel | Zeit nach dem Verweilen, in der eine Wirkung noch dazugehört. |
| `dwellNoteMs` | 150 | Regel | Ab dieser Dauer wird ein Zwischenhalt im Mausweg vermerkt. |
| `effectWindowMs` | 2000 | Regel | Grunddauer des Wirkfensters. |
| `effectSettleMaxMs` | 3000 | Regel | Obergrenze, bis zu der das Wirkfenster auf das Nachher-Bild wartet. |
| `effectUncertainMs` | 300 | Regel | Liegt eine Wirkung so knapp vor der nächsten Eingabe, gilt die Zuordnung nur als plausibel. |
| `systemMergeMs` | 500 | Regel | Zusammengehörige Systemereignisse innerhalb dieser Zeit bilden einen Schritt. |
| `anomalyWindowMs` | 2000 | Regel | Zeit nach einer Eingabelücke, in der ein Systemereignis als möglicherweise vom Nutzer ausgelöst gilt. |
| `waitMinMs` | 3000 | Regel | Mindestdauer eines Warte-Schritts. |
| `busyCursorMinMs` | 500 | Regel | Mindestdauer eines Wartezeigers, damit er als Hinweis auf Warten zählt. |
| `elementMaxAgeMs` | 1500 | Regel | Höchstalter einer Vorab-Ermittlung beim Tastendruck. |
| `rectTolerancePx` | 2 | Regel | Toleranz beim Vergleich von Rechtecken. |
| `rectOverlapMin` | 0,9 | Regel | Mindestüberdeckung, damit zwei Elemente ohne RuntimeId als dasselbe gelten. |
| `opaqueCoverage` | 0,6 | Regel | Flächenanteil am Fenster, ab dem ein namenloses Hüllelement als undurchsichtig gilt. |
| `approachEpsPx`, `dragEpsPx` | 3, 2 | Regel | Toleranz der Wegvereinfachung. |
| `pathMaxPoints`, `drawPathMaxPoints` | 32, 256 | Regel | Höchstzahl Punkte eines vereinfachten Wegs; der zweite Wert gilt für Zeichenbewegungen. |
| `textPauseNoteMs` | 2000 | Regel | Ab dieser Dauer wird eine Tipp-Pause im Schritt vermerkt. |
| `gapStepMinMs` | 1000 | Regel | Reine Bildlücken werden erst ab dieser Dauer ein eigener Schritt. |

Ein Parameter aus der Spalte „Regel“ darf beim Aufruf überschrieben werden; der überschriebene Wert steht dann in `params`. Fehlt eine Sitzungsangabe, gilt der Standard, und `warnings` vermerkt das.

---

<a id="spaete-daten"></a>
## 4. Vorbereitung: Zeitachse, Zustände, späte Daten

### 4.1 Zeitachse

Alle Ereignisse werden stabil sortiert nach `t`, bei Gleichstand nach `inSeq` (falls beide eines tragen), dann nach `seq`. Diese **Zeitachse** ist die einzige Ordnung, die die folgenden Regeln kennen.

### 4.2 Zustandsverläufe

Aus der Zeitachse entstehen Verläufe, die zu jedem Zeitpunkt abgefragt werden können:

| Verlauf | Quelle | Liefert zu einem Zeitpunkt |
|---|---|---|
| Epoche | `display.topology` | Gültige Monitor-Anordnung. |
| Fenster | `window.info`, `window.open`, `window.close`, `window.title`, `window.rect` | Bekannte Fenster mit Titel, Lage, Art, Oberfläche. |
| Vordergrund | `window.foreground` | Fenster im Vordergrund. |
| Fokus | `focus` (mit zugehörigem `element`) | Fokussiertes Element und Eingabekontext. |
| Tastenzustand | `key.down`, `key.up`, `keystate.sync` | Gedrückte Umschalter, aktive Feststeller. `keystate.sync` überschreibt den mitgeführten Zustand. |
| Aufnahmebereich | `scope.change` | Gültiger Bereich. |
| Lücken | `gap`, `text.masked` | Ob ein Kanal gerade blind ist. |
| Zwischenablage | `clipboard` | Kennung des aktuellen Inhalts. |
| Mauszeiger | `mouse.cursor` | Aktuelle Zeigerform. |
| Feldinhalte | `ui.value` | Letzter bekannter Inhalt je Element. |

### 4.3 Späte Daten

Fenster-, Element- und Bilddaten treffen später ein, als sie gelten. Die Ableitung behandelt das so:

1. **Zuordnung über `t`.** Ein Ereignis gehört in das Wirkfenster des Schritts, in das sein `t` fällt – gleichgültig, wann es beobachtet oder wo es im Journal geschrieben wurde.
2. **Anreicherungen über `ref`.** Ein Ereignis mit `ref` (Element-Ermittlung, Bild, Schnappschuss) gehört zu dem Ereignis, auf das es zeigt, auch wenn sein eigenes `t` anders liegt.
3. **Unsicherheit ausweisen.** Trägt ein Ereignis `tErr` und liegt eine Schrittgrenze innerhalb von `t ± tErr`, bleibt die Zuordnung nach `t` bestehen, die Wirkung erhält aber `causality: "plausible"` statt `"direct"`.
4. **Ergänzte Eingaben.** Ereignisse mit `recovered: true` nehmen als Eingaben teil; der Schritt erhält `flags.recovered` und höchstens die Belegstufe „nur Eingabe“.
5. **Ausgeschlossen.** Ereignisse mit `self: true` werden nie zu Schritten und zählen nicht in der Buchführung.

Daraus folgt die prüfbare Eigenschaft: Ein Journal, in dem Anreicherungen um beliebige Zeit verspätet **geschrieben** werden (gleiches `t`, anderes `tObs` und `seq`), ergibt dieselben Schritte.

---

<a id="schrittarten"></a>
## 5. Schrittarten und ihre Regeln

### 5.0 Übersicht

| `kind` | Bedeutung | Ankerereignis | Standard-Wichtigkeit |
|---|---|---|---|
| `click` | Einfacher Klick (auch mittlere und Zusatztasten, auch vier und mehr Klicks) | erstes `mouse.down` | Haupt |
| `doubleClick` | Doppelklick mit der Haupttaste | erstes `mouse.down` | Haupt |
| `tripleClick` | Dreifachklick mit der Haupttaste | erstes `mouse.down` | Haupt |
| `rightClick` | Einfacher Klick mit der Nebentaste | `mouse.down` | Haupt |
| `drag` | Ziehen | `mouse.down` | Haupt |
| `scroll` | Scrollbewegung | erstes `mouse.wheel` | Neben |
| `type` | Texteingabe (auch maskiert) | erstes `key.down` bzw. `text.masked` | Haupt |
| `hotkey` | Tastenkombination | `key.down` der Nicht-Umschalttaste | Haupt |
| `key` | Einzeltaste | `key.down` | je nach Taste |
| `hover` | Verweilen mit Wirkung | letztes `mouse.move` vor dem Verweilen | Neben oder Spur |
| `windowSwitch` | Fensterwechsel über den Umschalter | erstes `key.down` der Tab-Taste | Neben |
| `system` | Geschehen ohne Nutzereingabe | erstes beteiligtes Ereignis | Neben oder Spur |
| `wait` | Warten auf den Rechner | erstes Ereignis der Wartezeit | Neben |
| `comment` | Kommentar während der Aufnahme | `comment` | Haupt |
| `snapshot` | Schnappschuss-Taste | `marker` | Haupt |
| `marker` | Marker des Autors mit Typ (`note`, `error`, `expected`, `important`); die Befehle unter den Marker-Arten ergeben keinen `marker`-Schritt, siehe 5.11 | `marker` | Haupt oder Neben |
| `gap` | Lücke | `gap` (Beginn) | Haupt oder Neben |
| `move` | Mausbewegung ohne folgende Aktion | erstes `mouse.move` | Spur |

Die Regeln werden in dieser Reihenfolge angewendet: Lücken und Recorder-Ereignisse abgrenzen → Mausdrücke und Radgruppen bilden → Tasten zu Fensterwechseln, Tastenkombinationen, Texteingaben und Einzeltasten ordnen → Mauswege zuweisen → Verweilen erkennen → Übriges zu Systemereignissen und Warten.

### 5.1 Klick, Mehrfachklick, Rechtsklick

**Druck oder Ziehen.** Entfernt sich der Zeiger während eines Drucks von der Position des `mouse.down` um mehr als `dragWidth` waagerecht oder `dragHeight` senkrecht, ist es ein Ziehen ([5.2](#ziehen)), sonst ein Klick.

**Mehrfachklick.** Ein Klick verlängert den vorigen zu einem Mehrfachklick, wenn alle Bedingungen gelten:

- dieselbe Taste (`button`) und dasselbe Fenster unter dem Zeiger (`win`);
- sein `mouse.down` liegt höchstens `doubleClickTimeMs` nach dem `mouse.down` des vorigen Klicks;
- seine Position weicht von der des **ersten** Klicks der Kette um höchstens `doubleClickWidth / 2` bzw. `doubleClickHeight / 2` ab;
- dazwischen liegt kein anderes Eingabeereignis außer `mouse.move`.

Die Kette ist **ein** Schritt mit `input.count`; die einzelnen Klicks sind keine eigenen Schritte.

| Taste | Anzahl | `kind` | `action.verb` |
|---|---|---|---|
| `primary` | 1 | `click` | `click` |
| `primary` | 2 | `doubleClick` | `doubleClick` |
| `primary` | 3 | `tripleClick` | `tripleClick` |
| `primary` | ≥ 4 | `click` | `click` mit `clicks` |
| `secondary` | 1 | `rightClick` | `rightClick` |
| `secondary` | ≥ 2 | `click` | `click` mit `button`, `clicks` |
| `middle` | 1 | `click` | `middleClick` |
| `middle` ≥ 2, `x1`, `x2` | beliebig | `click` | `click` mit `button`, `clicks` |

**Weitere Regeln**

- `input.mods` sind die Umschalter beim `mouse.down` (Strg+Klick, Umschalt+Klick).
- `input.longPress` ist `true`, wenn der letzte Druck mindestens `longPressMs` dauerte.
- Maßgeblich ist die logische Taste. Bei vertauschten Maustasten ist `primary` die physisch rechte Taste; die Vorlagen sprechen dann weiterhin vom „Klick“ und nennen die physische Taste nur im Protokollstil.
- Wird eine zweite Maustaste gedrückt, während die erste gehalten wird, entstehen zwei Schritte in der Reihenfolge ihrer `mouse.down`; der zweite trägt die erste Taste in `input.held`.
- Fehlt das `mouse.up` (Lücke, Sitzungsende, verlorenes Ereignis), entsteht der Schritt trotzdem, mit `flags.incomplete`.

<a id="ziehen"></a>
### 5.2 Ziehen

- Anker ist das `mouse.down`; der Schritt endet mit dem `mouse.up` derselben Taste.
- **Der Weg ist Primärinformation.** `input.path` enthält Start, Ende und die nach Ramer-Douglas-Peucker mit `dragEpsPx` vereinfachte Punktfolge, höchstens `pathMaxPoints` Punkte – bei `dragKind: "draw"` höchstens `drawPathMaxPoints`. Der vollständige Weg bleibt im Journal.
- `target` ist das Ziel beim Drücken ([6](#zielbestimmung)), `endTarget` das Element der Ermittlung mit `trigger: "buttonUp"`. Für `endTarget` gibt es nur diese eine Ermittlung; sein Status ist höchstens `single`.
- `input.mods` sind beim Ziehen die Umschalter **beim Loslassen** (sie entscheiden über Kopieren, Verschieben, Verknüpfen).

`input.dragKind` wird aus der Wirkung bestimmt, in dieser Reihenfolge:

| Bedingung | `dragKind` |
|---|---|
| `window.rect` des Fensters unter dem Startpunkt ändert nur die Lage | `moveWindow` |
| … ändert die Größe | `resizeWindow` |
| `ui.selection` mit `text` am Start- oder Fokuselement | `selectText` (mit `selectedText`) |
| `window.dragdrop` im Zeitraum des Drucks | `dragDrop` |
| Ziel ist `Slider`, `Thumb`, `ScrollBar`, oder `ui.value`/`ui.scroll` des Ziels ändert sich | `slider` |
| Oberfläche undurchsichtig oder teilweise, oder Ziel ist `Pane`, `Custom`, `Image`, `Document` ohne Textauswahl, und der Bildschirm ändert sich | `draw` |
| sonst | `unknown` |

### 5.3 Scrollen

Aufeinanderfolgende `mouse.wheel`-Ereignisse bilden **einen** Schritt, solange gilt: gleiche Achse, gleiches Vorzeichen von `delta`, gleiches Fenster unter dem Zeiger, gleiche Umschalter, Abstand höchstens `scrollGroupGapMs`, und dazwischen kein anderes Eingabeereignis außer `mouse.move`. Bewegungen zwischen den Radereignissen gehören zum Scroll-Schritt.

| Feld | Berechnung |
|---|---|
| `input.notches` | Summe der `delta` geteilt durch 120 (mit Vorzeichen, darf gebrochen sein). |
| `input.events`, `input.deltaSum` | Anzahl der Radereignisse und Rohsumme. |
| `input.lines` | `notches` × `wheelScrollLines` – nur bei senkrechter Achse ohne Umschalter. |
| `input.semantic` | `zoom`, wenn Strg gehalten ist; sonst `scroll`. |
| `input.before`, `input.after` | Scrollposition aus `element.scroll` der Ermittlung mit `trigger: "wheel"` und aus dem letzten `ui.scroll` im Wirkfenster. |

Ein Richtungswechsel beginnt einen neuen Schritt. Hochauflösende Räder und Touchpads liefern viele kleine Werte und Nachlauf; sie fallen unter dieselbe Regel. Eine Zwei-Finger-Zoomgeste kommt als Strg+Rad an und wird `semantic: "zoom"`.

### 5.4 Texteingabe

**Zeichentaste** ist ein `key.down` mit nicht leerem `ch`, das keine Tottaste ist. **Bearbeitungstasten** sind Rücktaste, Entf, Pfeiltasten, Pos1, Ende – auch mit Umschalt oder Strg.

**Ein Textlauf beginnt** mit einer Zeichentaste oder einem `text.masked`, wenn kein Lauf offen ist.

**Er wird fortgesetzt** durch weitere Zeichentasten, Bearbeitungstasten, Tottasten, Umschalter und die Bestandteile von AltGr – solange der Fokus auf demselben Element liegt.

**Er endet** vor dem auslösenden Ereignis bei:

| Auslöser | Folge |
|---|---|
| Fokuswechsel auf ein anderes Element oder anderes Fenster | Neuer Lauf, falls weiter getippt wird. |
| `mouse.down` oder `mouse.wheel` | Der Mausschritt folgt. |
| Tastenkombination, die keine Bearbeitung ist (z. B. Strg+V, Strg+S, Alt+F4) | Eigener `hotkey`-Schritt. |
| Wechsel zwischen aufgezeichneten und zurückgehaltenen Tasten | Maskierte Folgen sind immer eigene Schritte. |
| Beginn einer Lücke | Der Lauf erhält `flags.incomplete`, falls Tasten noch gedrückt waren. |

**Er endet mit** der Taste (sie wird als `input.terminator` Teil des Schritts) bei:

| Taste | Regel |
|---|---|
| Eingabetaste | Abschluss – **außer** sie ist Inhalt. Inhalt ist sie, wenn der Kontext `textField` ist und entweder der nächste gelesene Feldinhalt einen zusätzlichen Zeilenumbruch enthält oder das Element vom Typ `Document` ist. In undurchsichtigen und unbekannten Kontexten ist die Eingabetaste **immer** Abschluss. |
| Tab | Abschluss, wenn innerhalb von `tabFocusMs` ein Fokuswechsel folgt oder der Kontext nicht `textField` ist. Sonst Inhalt (Tabulatorzeichen). |
| Esc | Immer Abschluss. |

Steht eine dieser Tasten allein, ohne offenen Lauf, wird sie eine Einzeltaste ([5.6](#einzeltaste)). Pausen teilen einen Lauf nicht; ab `textPauseNoteMs` werden sie in `input.pauses` vermerkt.

In einem Kontext `nonText` (Fokus auf Liste, Schaltfläche, Menü) gilt eine einzelne Zeichentaste ohne weitere innerhalb von `charRunGapMs` als Einzeltaste; ab zwei Zeichen ist es eine Texteingabe (Schnellsuche in Listen).

**`input.tokens`** entsteht in Tippreihenfolge: aufeinanderfolgende Zeichen werden zu einer Zeichenkette verbunden, Bearbeitungstasten werden Objekte mit `key`, `mods`, `n`, zurückgehaltene Tasten `{ "masked": n }`. Eine Folge von Tasten mit `key: "Substituted"` – ein endgültig verallgemeinerter Wert ([Datenformat](04-datenformat.md#edits-substitutions)) – wird **ein** Token `{ "placeholder": "Name" }` mit dem Namen aus dem Feld `placeholder` der ersten dieser Tasten; in `input.text` steht an der Stelle `{Name}`. Tasten mit `key: "Redacted"` bilden wie bisher keinen Text.

**`input.text`** ist der eingefügte Text, sofern er eindeutig ist: Enthält der Lauf nur Zeichen und Rücktasten, die innerhalb des Laufs getippte Zeichen löschen, wird er nachgespielt (`notiz.txr`, Rücktaste, `t` → `notiz.txt`). Sonst fehlt das Feld.

**Feldinhalt vorher und nachher.** `valueBefore` ist der letzte `ui.value` des Elements mit `t` nicht nach Beginn des Laufs; `valueAfter` der letzte `ui.value` nach der letzten Taste des Laufs und vor der nächsten Eingabe eines anderen Schritts (bevorzugt `phase: "focusOut"`).

**Abgleich.** Sind beide Werte bekannt, werden die Tokens auf `valueBefore` angewendet (eine laut `sel` markierte Stelle wird ersetzt, sonst wird an der Einfügemarke bzw. am Ende eingefügt) und mit `valueAfter` verglichen:

| Ergebnis | `evidence.result` | `input.resultSource` |
|---|---|---|
| Stimmt überein | `valueConfirmed` | `value` |
| Stimmt nicht überein, aber der eingefügte Text lässt sich als Unterschied der beiden Werte eindeutig ablesen (Eingabemethode, Autovervollständigung) | `valueConfirmed`; `input.text` und `inputMethod` aus dem Feld | `value` |
| Werte bekannt, Unterschied nicht eindeutig | `valueOnly` | `value` |
| Kein lesbarer Wert, `input.text` nachgespielt | `simulated` | `simulated` |
| Weder noch | `none` | `none` |

**`input.inputMethod`**: `keyboard` (Text stammt aus Tasten), `ime` (Tasten mit `ime: true` oder `ui.text` mit `composition…`), `autoComplete` (`ui.text` mit `autoComplete` oder der Wert ist länger als getippt und beginnt mit dem Getippten), `mixed`.

**Zurückgehaltene Eingaben.** Ein `text.masked` wird ein eigener `type`-Schritt mit `input.masked: true`, `tokens: [{ "masked": n }]`, ohne `text`, ohne Werte, `action: null`. Der Grund der Zurückhaltung steht in `input.maskReason`.

**Zählweise.** `n` und `input.count` sind `text.masked.count`, unverändert übernommen: die Zahl der **Tastenanschläge**. Gezählt wird jedes Drücken einer zurückgehaltenen Taste außer reinen Modifikatoren – also Zeichentasten, Tottasten, Rücktaste, Entf, Pfeiltasten, Pos1 und Ende, jede automatische Wiederholung einzeln; **nicht** gezählt werden Umschalt, Strg, Alt, AltGr und Win für sich sowie die Feststeller ([Datenformat, 9.9](04-datenformat.md#ev-text-masked)). Die Zahl ist damit eine Obergrenze für die Länge des Inhalts, nicht die Länge selbst. Dieselbe Zählweise gilt für `input.count` einer im Klartext aufgezeichneten Texteingabe und damit für N in `{unreviewed:N}`. Wurde die Anzahl nicht gespeichert (`text.masked` ohne `count`), ist das Token `{ "masked": null }`, und `input.count` fehlt. Bei `terminalPrompt` übernimmt der Schritt außerdem Regel und Abfragezeile (`input.rule`, `input.promptLine`), damit das Protokoll sagen kann, worauf geantwortet wurde. *Stand der Umsetzung:* Die Terminal-Abfrageerkennung (Regel T1) fehlt noch; der Recorder vergibt die Gründe `privacyMode`, `passwordField`, `strictUnknown` und `strictPlaintext`, nicht `terminalPrompt`.

**Klartext aus Fenstern ohne erkennbare Passwortfelder.** Eine im Klartext aufgezeichnete Texteingabe erhält `flags.plaintext`, sobald das Fenster nicht von der Art `structured` ist – also in Terminals (`partial`) sowie in Remote-Desktop- und VM-Fenstern (`opaque`) – oder der Eingabekontext `opaque` war. Das Kennzeichen ändert weder Belegstufe noch Wichtigkeit; es ist die Grundlage dafür, dass die Eingabe als auffällig gilt (Grund „Eingabe in einem Fenster ohne erkennbare Passwortfelder“) und vor der Weitergabe durchgesehen werden muss ([Datenformat](04-datenformat.md#edits-review), [Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md)). In der Schutzstufe „Streng“ wird dort gar nicht erst im Klartext aufgezeichnet: Die Tasten werden zurückgehalten (`maskReason: "strictPlaintext"`; bei unbekanntem Fokus `strictUnknown`) und erscheinen als Schritt „Vertrauliche Eingabe“. Eine Erkennung von Kennwortabfragen im Terminal (Regel T1, `terminalPrompt`) ist **nicht umgesetzt**; die Durchsichtspflicht tritt an ihre Stelle.

**Wertänderung ohne Eingabe.** Ändert sich der Inhalt des fokussierten Felds, ohne dass im selben Zeitraum Tasten, eine Tastenkombination oder ein Mausschritt auf dieses Feld vorliegen (automatisches Ausfüllen, Diktat, Handschrift), entsteht kein `type`-Schritt, sondern ein `system`-Schritt mit `event: "valueChanged"` und `evidence.cause: "unobserved"`.

### 5.5 Tastenkombination

Ein `key.down` einer Nicht-Umschalttaste ist eine Tastenkombination, wenn Strg, Alt oder Win gehalten ist und kein Zeichen über AltGr entsteht. Bearbeitungstasten mit Strg oder Umschalt **innerhalb eines offenen Textlaufs** bleiben Tokens der Texteingabe.

- **`input.combo`** ist die kanonische Schreibweise: Umschalter in der Reihenfolge `Ctrl`, `Alt`, `Shift`, `Win`, dann die Taste, verbunden mit `+` (`Ctrl+Shift+Enter`). Links/Rechts wird hier nicht unterschieden; `input.keys` nennt die tatsächlichen Tasten (`LeftCtrl`, `S`).
- **Zugeordnete Ereignisse:** die Umschalter seit der letzten Schrittgrenze, die Taste selbst mit Wiederholungen und Loslassen, das Loslassen der Umschalter. Bleibt ein Umschalter über mehrere Kombinationen gehalten (Strg halten, C, dann V), gehört sein Drücken zur ersten, sein Loslassen zur letzten Kombination.
- **Wiederholung:** Dieselbe Kombination, erneut ausgelöst innerhalb von `repeatMergeGapMs` ohne andere Eingabe und ohne dass dazwischen ein Fenster aufging, zuging oder der Vordergrund wechselte, erhöht `input.count`.
- **Zwischenablage:** Folgt im Wirkfenster ein `clipboard`-Ereignis, erhält der Schritt `clipboard: { "op": "copy" | "cut", "clip": … }`. Bei `Ctrl+V` und `Shift+Insert` erhält er `op: "paste"` mit der Kennung des Inhalts, der zu diesem Zeitpunkt in der Zwischenablage lag. Dieselbe Regel gilt für Klicks, auf die ein `clipboard`-Ereignis folgt (Menüpunkt „Kopieren“).
- **`input.commonMeaning`** ist ein bloßer Hinweis aus einer festen Tabelle (z. B. `Ctrl+S` → `save`, `Ctrl+C` → `copy`, `Ctrl+V` → `paste`, `Ctrl+Z` → `undo`, `Alt+F4` → `closeWindow`). Was die Kombination in der jeweiligen Anwendung wirklich tat, sagt allein `effect`.
- Alt+Tab und Win+Tab werden Fensterwechsel ([5.8](#fensterwechsel)).

<a id="einzeltaste"></a>
### 5.6 Einzeltaste

Ein `key`-Schritt entsteht aus

- einer Taste ohne Zeichen und ohne Strg/Alt/Win außerhalb eines Textlaufs (Eingabetaste, Esc, Tab, Entf, Funktionstasten, Pfeiltasten, Bild auf/ab, Druck, Leertaste auf einem Nicht-Textelement);
- einer einzelnen Zeichentaste im Kontext `nonText` (siehe 5.4);
- einem **Umschalter allein**: gedrückt und losgelassen, ohne dass dazwischen eine andere Eingabe lag (Win öffnet das Startmenü, Alt aktiviert die Menüleiste).

Dieselbe Taste mit denselben Umschaltern, wiederholt durch Auto-Repeat (`rep`) oder erneutes Drücken innerhalb von `repeatMergeGapMs` ohne andere Eingabe, ist **ein** Schritt mit `input.count`; `input.held` ist `true`, wenn Auto-Repeat beteiligt war. Was die Tasten bewirkten – etwa die neue Auswahl nach dreimal „Pfeil nach unten“ – steht in `effect`.

### 5.7 Verweilen mit Wirkung

Ein **Verweilen** liegt vor, wenn der Zeiger ohne gehaltene Maustaste mindestens `hoverMinMs` innerhalb von `hoverRadiusPx` bleibt. Ein `hover`-Schritt entsteht nur, wenn während des Verweilens oder bis `hoverEffectMs` danach eine **Wirkung** eintritt:

| Wirkung | Wichtigkeit |
|---|---|
| Menü, Untermenü, Popup oder Auswahlliste öffnet sich (`window.open` mit `kind` `menu`, `popup`, `dropdown`; `window.menu` mit `popupStart`; `ui.expand`) | Neben |
| Nur ein Tooltip erscheint (`window.open` mit `kind: "tooltip"`) | Spur |
| Nur eine Bildänderung in der Umgebung des Zeigers (`frame` mit `trigger: "change"`) | Spur |

Anker ist das letzte `mouse.move`-Bündel vor dem Verweilen; der Mausweg dorthin ist der Anlaufweg dieses Schritts. Ein Verweilen ohne Wirkung wird kein Schritt, sondern ein Eintrag in `approach.dwells` des folgenden Mausschritts (ab `dwellNoteMs`). Das letzte Ruhen des Zeigers auf dem Ziel unmittelbar vor einem Klick zählt nicht als Zwischenhalt.

<a id="fensterwechsel"></a>
### 5.8 Fensterwechsel

| Vorgang | Ergebnis |
|---|---|
| **Alt+Tab** (Alt halten, Tab ein- oder mehrmals, auch mit Umschalt; begleitet von `window.switch`) | **Ein** `windowSwitch`-Schritt: `via: "altTab"`, `count` = Zahl der Tab-Drücke, `from`/`to` aus dem Vordergrundverlauf. Die Tasten gehören zu diesem Schritt und bilden keinen `hotkey`. Bleibt der Vordergrund gleich, ist `to` = `from`. |
| Win+Tab, Win+Ziffer, Alt+Esc | `hotkey`-Schritt; der Vordergrundwechsel steht in `effect.foreground`. |
| Klick auf eine Schaltfläche der Taskleiste oder in ein anderes Fenster | Klick-Schritt; der Vordergrundwechsel steht in `effect.foreground`. |
| Vordergrundwechsel ohne Eingabe im Wirkfenster | `system`-Schritt mit `event: "foregroundChanged"`. |

### 5.9 Systemereignis

Ereignisse, die in kein Wirkfenster eines Aktionsschritts fallen, werden `system`-Schritte. Zusammengehörige Ereignisse innerhalb von `systemMergeMs` (ein Fenster öffnet sich, kommt in den Vordergrund, erhält den Fokus) bilden einen Schritt; Anker ist das erste.

| Ereignis | `input.event` | Wichtigkeit | `evidence.cause` |
|---|---|---|---|
| `window.open` mit `kind` `window` oder `dialog` | `windowOpened` | Neben | `none` |
| `window.close` mit `kind` `window` oder `dialog` | `windowClosed` | Neben | `none` |
| `window.foreground` | `foregroundChanged` | Neben | `none` |
| `window.title` | `titleChanged` | Spur | `none` |
| `ui.notification`, `window.open` mit `kind: "toast"` | `notification` | Neben | `none` |
| `ui.value` am fokussierten Feld ohne Eingabe (siehe 5.4) | `valueChanged` | Neben | `unobserved` |
| `display.topology` (ab Epoche 1) | `topologyChanged` | Neben | `none` |
| `scope.change` (außer `reason: "initial"`) | `scopeChanged` | Neben | `none` |
| `window.open` mit `kind` `menu`, `popup`, `dropdown`, `tooltip` | `windowOpened` | Spur | `none` |

Die Spalte nennt den Regelfall. Unabhängig davon wird `cause` zu `unobserved`, wenn das Ereignis **innerhalb** einer Lücke liegt, die Tastatur oder Maus betrifft, oder höchstens `anomalyWindowMs` **nach** deren Ende: Dann kann es die Folge einer Eingabe sein, die nicht aufgezeichnet wurde.

`cause: "unobserved"` bedeutet: Es ist etwas geschehen, das eine Nutzerhandlung nahelegt, aber keine Eingabe wurde beobachtet. Solche Schritte erhalten die Belegstufe „unbekannt“ ([9](#belegstufen)). `cause: "none"` steht bei Ereignissen, die üblicherweise von selbst eintreten; auch dann behauptet das Format nicht, der Nutzer sei unbeteiligt gewesen – nur, dass keine Eingabe dazu vorliegt.

### 5.10 Warten

Ein `wait`-Schritt entsteht zwischen zwei Aktionsschritten, wenn

1. der Zeitraum vom Nachher-Bild des ersten (oder seinem letzten Eingabeereignis) bis zum ersten Eingabeereignis des zweiten mindestens `waitMinMs` dauert **und**
2. in diesem Zeitraum etwas geschah: ein `system`-Schritt, mindestens ein `frame` mit `trigger: "change"` oder ein Wartezeiger (`wait`, `appStarting`) über mindestens `busyCursorMinMs`.

`input.until` nennt das letzte dieser Ereignisse (worauf gewartet wurde), `input.busyCursor` den Wartezeiger. Ein Zeitraum ohne jedes Geschehen ist Leerlauf und kein Schritt. Ein `wait`-Schritt enthält nie Eingabeereignisse.

Liegt in dem Zeitraum eine Lücke, die Tastatur, Maus oder alles betrifft, entsteht **kein** `wait`-Schritt: Was der Nutzer in dieser Zeit tat, ist unbekannt, und „gewartet“ wäre eine Behauptung. Die Lücke und die `system`-Schritte danach sagen, was bekannt ist.

### 5.11 Kommentar, Schnappschuss, Marker

| Ereignis | Ergebnis |
|---|---|
| `comment` | `comment`-Schritt an der Stelle seines `t`; `frames.pre` ist das eingefrorene Bild. Der Text wird beim Anzeigen aus dem Ereignis gelesen (`input.refSeq`). |
| `marker` mit `kind: "snapshot"` | `snapshot`-Schritt mit dem zugehörigen Bild. |
| `marker` mit `kind` `note`, `error`, `expected` oder `important` (der frühere Wert `marker` gilt als `note`) | `marker`-Schritt mit `input.markerKind` und – falls vorhanden – `input.label`. Wichtigkeit: `note` Neben; `error`, `expected` und `important` Haupt. `frames.pre` ist das zum Marker aufgenommene Bild (`marker.frame`); nennt der Marker keines, das jüngste Bild mit `t` nicht nach dem Marker. |
| `marker` mit `kind: "discardPrevious"` | Kein eigener Schritt. Der letzte davor liegende Aktionsschritt der Stufe „Haupt“, der noch nicht verworfen ist, erhält `flags.discarded: true`. Er bleibt in `steps.json`; die Anleitung lässt ihn weg, das KI-Protokoll nennt ihn als „vom Autor verworfen“. Gibt es keinen solchen Schritt, steht eine Warnung in `warnings`. |
| `marker` mit `kind: "redactLastInput"` | Kein eigener Schritt. Der Marker gehört zum Befehl „Letzte Eingabe schwärzen“ ([Datenformat](04-datenformat.md#redact-last-input)): Der Zeitraum von `tFrom` bis zum Marker wird von der Leseschicht zurückgehalten und beim Stopp endgültig geschwärzt. Die Ableitung selbst wertet den Marker nicht aus; nach dem Stopp findet sie an seiner Stelle gekürzte Zeilen und bildet daraus die Lücke `removedByAuthor` (5.12). |

**Marker und Ziel-Angaben sind Aussagen des Autors, keine Beobachtungen.** Ein Marker `error` belegt nicht, dass ein Fehler auftrat, sondern dass der Autor an dieser Stelle einen sah. Deshalb gilt:

- Ein Marker ändert weder Belegstufe noch Wirkung noch Wichtigkeit eines anderen Schritts und geht in keine Regel der Abschnitte 5 bis 10 ein.
- Die Ziel-Angaben der Sitzung (`goal` im Sitzungskopf, [Datenformat](04-datenformat.md#manifest-goal)) liest die Ableitung gar nicht. Sie erscheinen allein über die Vorlagen in [12.9](#ziel-vorlagen).
- Der Typ eines Markers lässt sich im Viewer ändern (`steps[].markerKind` in `edits.json`). Wie jede Bearbeitung wirkt das in der Darstellung, nicht in `steps.json`; die Wichtigkeit folgt dabei dem wirksamen Typ, sofern der Autor sie nicht eigens gesetzt hat.

### 5.12 Lücke

| Lücke im Journal | Ergebnis |
|---|---|
| Betrifft Tastatur oder Maus (`channels` enthält `keyboard`, `mouse` oder `all`), außer `recorderInteraction` | `gap`-Schritt. Wichtigkeit „Haupt“, wenn `inputLost` nicht `none` ist, sonst „Neben“. |
| `privacyMode` mit mindestens einem `text.masked` darin | Kein eigener Schritt; die maskierten `type`-Schritte stellen sie dar. Ohne `text.masked` darin: `gap`-Schritt (Neben). |
| Nur Bilder betroffen (`captureLost`, `protectedContent`) | Ab `gapStepMinMs` ein `gap`-Schritt (Spur); kürzere erscheinen nur als `frames.issues` der betroffenen Schritte. |
| `frameDropped` | Kein Schritt; `frames.issues` des betroffenen Schritts. |
| `recorderInteraction` | Kein Schritt; nur Zeitleiste und Lückenliste. |
| Folge endgültig geschwärzter Zeilen (`redacted: ["*"]`, siehe [Datenformat](04-datenformat.md#edits-redactions)) | `gap`-Schritt mit der Ursache `removedByAuthor` (Neben), vom `t` der ersten bis zum `t` der letzten gekürzten Zeile. Eingabeereignisse unter diesen Zeilen sind ihm zugeordnet, damit die Buchführung aufgeht. Strukturereignisse, die beim Schwärzen stehen bleiben (`session.*`, `system`, `display.topology`, `scope.change`, `keystate.sync`, `gap`, `marker`), unterbrechen die Folge nicht. So entsteht auch die Lücke nach „Letzte Eingabe schwärzen“ (5.11). |

**Benutzerkontensteuerung (UAC).** Eine Lücke mit der Ursache `secureDesktop` und der Art `uac` (`desktopKind`; steht im Ende-Ereignis der Lücke, Erkennung über `consent.exe` auf dem Eingabe-Desktop, siehe [03](03-aufzeichnungskern.md)) wird ein benannter Lückenschritt: Kurztitel „Benutzerkontensteuerung bestätigen“ („Confirm User Account Control“). Der Anleitungstext sagt, dass Windows die Änderung abfragt und diese Abfrage aus Sicherheitsgründen nicht aufgezeichnet wird; der Protokolltext nennt Dauer und dass Eingaben und Bilder fehlen. Der Schritt hat **kein Bild**: Weder Vorher- noch Nachher-Bild wird zugeordnet, und ein Nachher-Bild eines Schritts davor wird nie aus einer solchen Lücke gebunden. Andere Arten des sicheren Desktops (`other`) und Sperre, Trennung und Ruhezustand folgen der allgemeinen Regel (Hinweisschritt „Lücke: …“, ebenfalls ohne Bild). Die Wichtigkeit richtet sich wie bei jeder Lücke nach `inputLost`.

Alle Lücken – auch die ohne eigenen Schritt – stehen in `gaps` und in der Zeitleiste. Ein Schritt, dessen Eingabe bei Beginn einer Lücke noch offen war (Taste gedrückt, Maustaste gehalten), wird mit `flags.incomplete` abgeschlossen. Nach einer Lücke gilt der Tastenzustand aus `keystate.sync`.

### 5.13 Mausweg und Bewegung ohne Aktion

**Anlaufweg.** Alle `mouse.move`-Bündel seit dem letzten Mausschritt (Klick, Ziehen, Scrollen, Verweilen) gehören als `seq.approach` zum **nächsten** Mausschritt – auch wenn dazwischen getippt wurde. Die Zeitspannen von Schritten dürfen sich deshalb überlappen.

`approach` fasst den Weg zusammen:

| Feld | Berechnung |
|---|---|
| `from` | Erster Messwert des Anlaufwegs. |
| `distancePx` | Summe der Abstände aufeinanderfolgender Messwerte. |
| `directPx` | Abstand zwischen erstem und letztem Messwert. |
| `durationUs` | Bewegungszeit: Dauer aller Bündel zuzüglich der Pausen dazwischen, die kürzer als `dwellNoteMs` sind. |
| `straightness` | `directPx / distancePx` (1 = gerade Linie). |
| `path` | Mit `approachEpsPx` vereinfachte Punktfolge `[x, y, dtMs]`, höchstens `pathMaxPoints` Punkte. |
| `dwells` | Zwischenhalte ab `dwellNoteMs` mit Position, Dauer und – falls ermittelt – Element. |
| `restUs` | Ruhezeit auf dem Ziel zwischen dem letzten Messwert und der Aktion. Sie ist kein Zwischenhalt. |

**Bewegung ohne Aktion.** Bündel, auf die bis zum Sitzungsende, einer Lücke oder einem Wechsel der Topologie-Epoche kein Mausschritt folgt, bilden einen `move`-Schritt (Spur). So ist auch die letzte Bewegung verbucht.

<a id="sonderfaelle"></a>
### 5.14 Sonderfälle der Eingabe

| Fall | Erkennung | Behandlung |
|---|---|---|
| **AltGr** | `mods` enthält `altGr`, `ch` ist gesetzt | Zeichen einer Texteingabe, keine Tastenkombination. Ereignisse mit `altGrPart` werden dem Schritt zugeordnet, aber nie als Taste gezeigt. |
| **Tottasten** | `dead: true`, danach `deadFrom` | Die Tottaste erzeugt kein Token; das zusammengesetzte Zeichen der Folgetaste wird Token. |
| **Alt+Ziffernblock** | `key.up` der Alt-Taste mit `altCode` und `ch` | Das Zeichen wird Token; die Zifferntasten gehören zum Schritt, erscheinen aber nicht. |
| **Auto-Repeat** | `rep: true` | Zeichen werden wiederholt in den Text übernommen; Tasten ohne Zeichen erhöhen `count`. |
| **Eingabemethode (IME)** | `ime: true` oder `ui.text` mit `composition…` | Tasten werden `{ "uncertain": … }`-Tokens. Der eingefügte Text stammt aus `ui.text` (`compositionFinalized`) oder aus dem Feldvergleich; `inputMethod: "ime"`. Ohne lesbaren Feldinhalt bleibt die Belegstufe „nur Eingabe“. |
| **Einrastfunktion** | `stickyKeys` der Sitzung | Ein allein gedrückter und losgelassener Umschalter gilt für die nächste Taste als gehalten; doppelt gedrückt bleibt er bis zum nächsten Drücken eingerastet. Der Schritt erhält den Grund `stickyKeysEmulated` und höchstens „nur Eingabe“, sofern nicht Feldinhalt oder Wirkung die Deutung bestätigen. |
| **Anschlagverzögerung** | `filterKeys` der Sitzung | Kurze Tastendrücke erreichen die Anwendung möglicherweise nicht. Texteingaben gelten nur mit bestätigendem Feldinhalt als belegt. |
| **Künstliche Eingaben** | `inj` | Schritte erhalten `flags.injected`. Sie werden sonst wie echte Eingaben behandelt; die Vorlagen weisen darauf hin. |
| **Ergänzte Eingaben** | `recovered: true` | Siehe [4.3](#spaete-daten). |
| **Touch und Stift** | `device` | Gleiche Regeln wie für die Maus; `input.device` hält das Gerät fest. |
| **Touchpad** | – | Zwei-Finger-Scrollen und Zoomen kommen als Radereignisse an (5.3). Drei- und Vier-Finger-Gesten erzeugen keine Eingabeereignisse; ihre Folgen erscheinen als `system`-Schritte mit `cause: "unobserved"` oder, falls der Recorder sie erkennt, als Lücke `unsupportedInput`. |
| **Gefangener Zeiger** | Lücke `relativeMouse` | Positionen sind in dieser Zeit ohne Aussage; Klicks erhalten `target.status: "position"` und die Belegstufe „nur Eingabe“. |
| **Unpaarige Tasten** | `key.up` ohne `key.down` oder umgekehrt | Wird hingenommen und in `warnings` vermerkt; `keystate.sync` berichtigt den Zustand. |

### 5.15 Undurchsichtige Oberflächen

Eine Oberfläche ist **undurchsichtig**, wenn ihr Inhalt für den Recorder nur ein Bild ist: Remote-Desktop, virtuelle Maschine, Spiel, Zeichenfläche, viele Terminals.

**Erkennung** – eines genügt:

- `window.info.surface` ist `opaque`;
- der Eingabekontext (`ctx`) ist `opaque`;
- die Element-Ermittlung trägt `opaqueHint`;
- das ermittelte Element ist ein Hüllelement: Typ `Pane`, `Window`, `Custom`, `Document`, `Image` oder `Group`, ohne unterscheidenden Namen, und sein Rechteck bedeckt mindestens `opaqueCoverage` der Fensterfläche.

Bei `surface: "partial"` wird ein Element nur verwendet, wenn es kein Hüllelement ist.

**Folgen**

- `target.status` ist `position`; ein Elementname wird nicht ausgegeben. Der Schritt erhält `flags.opaque`.
- Die Belegstufe ist höchstens „nur Eingabe“ – auch wenn Vorab- und Tastendruck-Ermittlung übereinstimmen (beide fanden nur die Hülle).
- Die Vorlagen beschreiben über Position und Bildausschnitt.
- Der Weg einer Ziehbewegung wird mit bis zu `drawPathMaxPoints` Punkten ausgegeben.
- Textläufe enden an **jeder** Eingabe-, Tab- und Esc-Taste (5.4), damit etwa eine Terminal-Sitzung nicht zu einem einzigen Schritt wird.
- Eine Texteingabe ist hier **nie** durch einen Feldinhalt bestätigt – auch nicht im Terminal. Text aus dem Terminal soll der Recorder allein lesen, um Passwortabfragen zu erkennen (Regel T1, noch nicht umgesetzt – bis dahin gelten Durchsichtspflicht und die Stufe „Streng“, siehe 5.4); als `ui.value` gespeichert wird er nie ([Aufzeichnungskern](03-aufzeichnungskern.md), [Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md)). `evidence.result` ist deshalb höchstens `simulated`, die Belegstufe höchstens „nur Eingabe“.

---

<a id="zielbestimmung"></a>
## 6. Zielbestimmung

Für jeden Mausschritt stehen bis zu drei unabhängige Belege für das Ziel im Journal:

| Kürzel | Beleg | Quelle |
|---|---|---|
| **V** | Vorab-Ermittlung beim Verweilen, vor der Eingabe | `element` mit `trigger: "dwell"` |
| **T** | Ermittlung beim Tastendruck | `element` mit `trigger: "buttonDown"` und `ref` auf das `mouse.down` |
| **R** | Reaktion des Elements im Wirkfenster | `ui.invoke`, `ui.toggle`, `ui.expand`, `ui.selection` mit diesem Eintrag, `ui.value` oder `focus` auf dasselbe Element |

### 6.1 Wann ein Beleg brauchbar ist

Eine Ermittlung ist **brauchbar**, wenn `status` `ok` ist, ihr Rechteck vorhanden ist und den Klickpunkt enthält (Toleranz `rectTolerancePx`), ihr Fenster das Fenster unter dem Zeiger zum Hook-Zeitpunkt ist (`mouse.down.win`; falls `hitHwnd` und `el.hwnd` beide vorhanden sind, dürfen sie sich nicht widersprechen) und sie kein Hüllelement einer undurchsichtigen Oberfläche ist (5.15).

Für **V** gilt zusätzlich:

- sie ist höchstens `elementMaxAgeMs` älter als das `mouse.down`;
- `dwellValid` der Ermittlung T ist nicht `false`;
- zwischen ihr und dem `mouse.down` liegt kein Ereignis, das den Inhalt unter dem Zeiger geändert haben kann: kein `mouse.wheel`, kein `key.down`, kein `window.open`/`window.close`/`window.foreground`, kein `frame`, dessen `changed.rects` das Elementrechteck schneidet.

So wird der Fall „gescrollt, dann geklickt, ohne die Maus zu bewegen“ erkannt: V ist dann veraltet und zählt nicht.

### 6.2 Wann zwei Belege dasselbe Element meinen

1. Tragen beide eine RuntimeId (`rid`): gleich, wenn die Kennungen gleich sind.
2. Sonst: gleich, wenn `ct`, `name` und `aid` übereinstimmen **und** sich die Rechtecke zu mindestens `rectOverlapMin` überdecken.

Eine Reaktion R gehört zu einem Element, wenn ihr `el` nach derselben Regel dasselbe Element ist (ohne Rechteckvergleich, falls R keines trägt).

### 6.3 Entscheidung

| Fall | Lage | `target.status` | Gewähltes Element |
|---|---|---|---|
| 1 | V und T brauchbar und gleich | `agreed` | T |
| 2 | Genau eines von V, T brauchbar, und eine Reaktion R gehört dazu | `corroborated` | das brauchbare |
| 3 | V und T brauchbar, verschieden, und R gehört zu genau einem | `corroborated` | das von R bestätigte; das andere steht in `alternatives` |
| 4 | Genau eines brauchbar, keine passende Reaktion | `single` | das brauchbare |
| 5 | V und T brauchbar, verschieden, keine eindeutige Reaktion | `conflict` | V (der Zustand, den der Nutzer vor dem Klick sah); T steht in `alternatives` |
| 6 | Keines brauchbar, Fenster bekannt | `position` | – |
| 7 | Nichts bekannt | `none` | – |

`target.from` nennt die `seq` der verwendeten Ereignisse (`dwell`, `down`, `corroboration`). `target.el` ist eine unveränderte Übernahme aus dem gewählten Ereignis.

Die Grundregel lautet also: **belegt ist ein Ziel, wenn zwei unabhängige Belege übereinstimmen.** Der Regelfall ist die Übereinstimmung von Vorab- und Tastendruck-Ermittlung (Fall 1). **Festgelegt ist außerdem:** Eine beobachtete Reaktion des Elements – etwa `ui.invoke` – zählt als gleichwertiger zweiter Beleg (Fälle 2 und 3). Ein Ziel kann damit auch dann belegt sein, wenn nur eine der beiden Ermittlungen gelang, sofern das Element nachweislich reagiert hat. Eine bloße Bildänderung oder ein sich öffnendes Fenster ohne Bezug auf das Element genügt dafür nicht.

### 6.4 Tastatur, Scrollen, Verweilen

| Schrittart | Ziel | Status |
|---|---|---|
| `type`, `key`, `hotkey` | Das fokussierte Element laut dem `focus`-Ereignis, das für die Tasten galt (`key.focus`), mit seiner Beschreibung aus `element` (`trigger: "focus"`) | `corroborated`, wenn eine Reaktion R an diesem Element vorliegt (z. B. der Feldinhalt änderte sich); `single`, wenn nur der Fokus bekannt ist; `none`, wenn kein Fokuselement bekannt ist (undurchsichtig, Zeitüberschreitung) |
| `scroll` | Element der Ermittlung mit `trigger: "wheel"` | `corroborated`, wenn ein `ui.scroll` desselben Elements oder eines Vorfahren folgt; sonst `single` bzw. `position` |
| `hover` | Element der Vorab-Ermittlung an der Verweilposition | `corroborated`, wenn die Wirkung an diesem Element hängt (`ui.expand` daran, oder das geöffnete Fenster gehört demselben Fenster); sonst `single` |
| `drag` | Start wie beim Klick; Ende siehe 5.2 | Start nach 6.3 |

Für `hotkey` und `key` ist das Ziel eine Zusatzangabe; maßgeblich für die Belegstufe ist dort das Fenster im Vordergrund ([9](#belegstufen)).

---

<a id="wirkungserkennung"></a>
## 7. Wirkungserkennung

### 7.1 Wirkfenster

Das Wirkfenster eines Aktionsschritts beginnt mit seinem Ankerereignis und endet zum frühesten dieser Zeitpunkte:

1. Ankerereignis des nächsten Aktionsschritts;
2. Beginn der nächsten Lücke, die Fensterereignisse betrifft;
3. `effectWindowMs` nach dem letzten Eingabeereignis des Schritts – verlängert bis zur Darstellungszeit seines Nachher-Bilds, höchstens aber `effectSettleMaxMs` nach dem letzten Eingabeereignis.

Wirkfenster überlappen sich nicht. Jedes Folgeereignis gehört zu höchstens einem Schritt.

### 7.2 Was als Wirkung zählt

| Ereignis im Wirkfenster | Feld in `effect` |
|---|---|
| `window.open` (mit zugehörigem `ui.snapshot`), `window.close` | `windowsOpened`, `windowsClosed` |
| `window.foreground` | `foreground` |
| `window.title` | `titleChanges` |
| `focus` | `focus` (das letzte im Fenster) |
| `ui.value` an einem anderen Element als dem Zielfeld einer Texteingabe | `values` (vorher aus dem Verlauf der Feldinhalte) |
| `ui.toggle`, `ui.selection`, `ui.expand` | `toggles`, `selections`, `expansions` |
| `ui.invoke` | `invoked` |
| `ui.scroll` | `scroll` |
| `ui.notification` | `notifications` |
| `clipboard` | `clipboard` |
| `frame` mit `trigger: "settle"` zu einem Eingabeereignis des Schritts | `screen` (`changedRatio`, `settled`, `settleUs`, `settleReason`) |
| `mouse.cursor` | `cursor` (Folge der Standardformen) |

`effect.latencyUs` ist der Abstand vom Ankerereignis zur ersten Wirkung; jede einzelne Wirkung darf `afterUs` tragen. `effect.observed` ist `false`, wenn nichts davon vorliegt – das ist eine zulässige und häufige Aussage („keine Wirkung beobachtet“), kein Fehler.

### 7.3 Wie sicher die Zuordnung ist

| `causality` | Bedingung |
|---|---|
| `direct` | Wirkungen liegen im Wirkfenster, und das Fenster endete nicht durch die nächste Eingabe innerhalb von `effectUncertainMs` nach der Wirkung. |
| `plausible` | Die nächste Eingabe folgte so dicht, dass die Wirkung auch ihr gelten könnte, oder die Zeit der Wirkung ist unsicher ([4.3](#spaete-daten)), oder die Oberfläche ist undurchsichtig und nur der Bildschirm änderte sich. |
| `none` | Keine Wirkung beobachtet. |

### 7.4 Was übrig bleibt

Folgeereignisse außerhalb aller Wirkfenster werden Systemereignisse ([5.9](#schrittarten)). Dazu gehört ausdrücklich der Fall **Wirkung ohne beobachtete Ursache**: Wechselt der Vordergrund, schließt ein Fenster oder ändert sich ein Feldinhalt, ohne dass eine Eingabe dazu passt, wird das nicht einem beliebigen Nachbarschritt zugeschlagen, sondern als eigener Schritt mit `cause: "unobserved"` ausgewiesen.

---

<a id="bildzuordnung"></a>
## 8. Bildzuordnung

Bilder werden über die Angaben der `frame`-Ereignisse zugeordnet ([Datenformat](04-datenformat.md#ev-frame)). Maßgeblich ist der Monitor des Ankerereignisses; bei Tastaturschritten der Monitor, auf dem das Vordergrundfenster überwiegend liegt.

| Bild | Regel | Wenn nichts passt |
|---|---|---|
| **Vorher** (`frames.pre`) | Das `frame` mit `trigger: "preInput"`, dessen `ref` das erste Eingabeereignis der Aktion ist. | Das jüngste `frame` desselben Monitors mit `t` vor dem Ankerereignis; dazu `issues: ["preNotGuaranteed"]`. Gibt es keines: `null` und `preMissing`. |
| **Nachher** (`frames.post`) | Das `frame` mit `trigger: "settle"`, dessen `ref` das letzte Eingabeereignis des Schritts ist. | Das Vorher-Bild des nächsten Schritts auf demselben Monitor – **das Nachher-Bild ist spätestens das, was der Nutzer vor seiner nächsten Eingabe sah**. Gibt es keinen nächsten Schritt: das letzte `frame` vor Lücke oder Sitzungsende. Sonst `null` und `postMissing`. |
| **Zwischenbilder** (`frames.between`) | Alle übrigen `frame`-Ereignisse des Monitors mit `t` nach dem Vorher-Bild und nicht nach dem Nachher-Bild: Vorher-Bilder späterer Tasten desselben Schritts und Bilder mit `trigger: "change"`. | Leere Liste. |

Weitere Regeln:

- Ein `frame` mit `store.kind: "none"` gilt als nicht vorhanden; der Schritt erhält den passenden Eintrag in `issues`.
- `guarantee` des Vorher-Bilds ungleich `beforeInput` führt zu `preNotGuaranteed`.
- `settle.reason: "timeout"` führt zu `postTimeout` (der Bildschirm kam nicht zur Ruhe – etwa bei einem Video oder einer Ladeanimation).
- Liegt am Zielrechteck oder Klickpunkt eine gefüllte Fläche (`frame.masks` oder außerhalb von `frame.scopeRects`), steht `masked` in `issues`.
- Überspannt ein Ziehen zwei Monitore, stehen die Bilder des zweiten in `frames.others`.
- `system`-, `wait`- und `gap`-Schritte erhalten als Vorher-Bild das jüngste Bild vor ihrem Beginn und als Nachher-Bild das erste beruhigte Bild danach. **Ausnahme:** `gap`-Schritte mit der Ursache `secureDesktop`, `sessionLocked`, `sessionDisconnected` oder `suspend` bleiben ohne Bild (`pre` und `post` sind `null`, ohne `issues`-Eintrag), weil dort nichts aufgezeichnet werden durfte oder konnte.
- `comment`-, `snapshot`- und `marker`-Schritte tragen als `frames.pre` das Bild, das ihr Ereignis nennt (5.11); `frames.post` ist `null`.
- Über die Grenze eines Aufnahmeabschnitts hinweg wird kein Bild zugeordnet: Für den letzten Schritt eines Abschnitts gilt das Abschnittsende wie ein Sitzungsende, und das Vorher-Bild eines Schritts stammt immer aus seinem eigenen Abschnitt.

Welches Bild in Anleitung und Export gezeigt wird, kann der Nutzer je Schritt ändern (`frameChoice` in `edits.json`); die Zuordnung in `steps.json` bleibt davon unberührt.

---

<a id="belegstufen"></a>
## 9. Belegstufen

### 9.1 Drei Stufen

| Stufe | `evidence.level` | Bedeutung |
|---|---|---|
| **belegt** | `confirmed` | Was der Nutzer tat **und** worauf es sich bezog, ist durch mindestens zwei übereinstimmende Belege gesichert. |
| **nur Eingabe** | `inputOnly` | Die Eingabe selbst ist vollständig bekannt (Position, Tasten). Ziel oder Ergebnis sind nicht bestätigt. |
| **unbekannt** | `unknown` | Es ist bekannt, **dass** etwas geschah, aber nicht **was**: eine bewusst zurückgehaltene Eingabe, eine Lücke, ein Geschehen ohne beobachtete Ursache. Zählt als Lücke. |

Die Stufe sagt nichts über die Richtigkeit des Vorgehens des Nutzers, sondern allein darüber, wie gut die Aufzeichnung den Schritt belegt.

### 9.2 Bestandteile

`evidence` hält die Einstufung in ihren Teilen fest, damit sie nachprüfbar bleibt:

| Feld | Werte | Bedeutung |
|---|---|---|
| `input` | `observed` | Alle Eingabeereignisse des Schritts liegen vor. |
| | `masked` | Eingabe wurde bewusst zurückgehalten. |
| | `unobserved` | Keine Eingabe beobachtet. |
| `target` | `agreed`, `corroborated`, `single`, `conflict`, `position`, `none`, `n/a` | Ergebnis der [Zielbestimmung](#zielbestimmung). `n/a` steht nur bei Schrittarten ohne Ziel (`windowSwitch`, `system`, `wait`, `comment`, `snapshot`, `marker`, `gap`, `move`). Tastaturschritte – `type`, `key`, `hotkey` – tragen den Status nach [6.4](#zielbestimmung), auch wenn das Ziel dort nur Zusatzangabe ist. |
| `result` | `valueConfirmed` | Der eingefügte Text ist durch den Feldinhalt belegt. |
| | `valueOnly` | Feldinhalt bekannt, der eingefügte Text daraus aber nicht eindeutig ablesbar. |
| | `simulated` | Text nur aus der Tastenfolge nachgespielt. |
| | `effectObserved` | Eine Wirkung wurde beobachtet. |
| | `none`, `n/a` | Nichts beobachtet bzw. nicht anwendbar. |
| `cause` | `none`, `unobserved`, `n/a` | Für Schritte ohne Nutzereingabe ([5.9](#schrittarten)). |
| `reasons` | Liste von Codes | Begründung, siehe 9.5. |

### 9.3 Kriterien je Schrittart

| Schrittart | **belegt**, wenn | **nur Eingabe**, wenn | **unbekannt**, wenn |
|---|---|---|---|
| `click`, `doubleClick`, `tripleClick`, `rightClick` | Eingabe beobachtet **und** `target` ist `agreed` oder `corroborated` | Eingabe beobachtet, `target` ist `single`, `conflict`, `position` oder `none` | – |
| `drag` | wie Klick für den Start **und** (Ende ermittelt **oder** `dragKind` ist nicht `unknown`) | sonst | – |
| `scroll` | Eingabe beobachtet **und** `target` ist `corroborated` | sonst | – |
| `type` | Eingabe beobachtet, Feld bekannt (`target` `corroborated` oder `single`) **und** `result` ist `valueConfirmed` | Eingabe beobachtet, aber Feld unbekannt oder `result` ist `valueOnly`, `simulated`, `none` | Eingabe zurückgehalten (`input: "masked"`) |
| `hotkey`, `key` | Eingabe beobachtet **und** das Fenster im Vordergrund zum Hook-Zeitpunkt ist bekannt | Eingabe beobachtet, Fenster unbekannt | – |
| `hover` | Element ermittelt **und** Wirkung hängt daran (`target: "corroborated"`) | sonst | – |
| `windowSwitch` | Eingabe beobachtet **und** der sich ergebende Vordergrund ist bekannt | sonst | – |
| `system` | `cause` ist `none` | – | `cause` ist `unobserved` |
| `gap` | – | – | immer |
| `wait`, `comment`, `snapshot`, `marker`, `move` | immer (Feststellungen des Recorders) | – | – |

Ein beobachteter Klick oder Tastendruck ist nie „unbekannt“: Die Eingabe selbst ist eine Tatsache.

Zwei Punkte dieser Tabelle sind ausdrücklich so entschieden:

- **Reaktion als zweiter Beleg.** Für Mausschritte gilt `target: "corroborated"` – eine Ermittlung plus beobachtete Reaktion des Elements – als ebenso belegt wie die Übereinstimmung beider Ermittlungen ([6.3](#zielbestimmung)).
- **Tasten mit bekanntem Zielfenster.** Tastenkombinationen und Einzeltasten sind belegt, sobald das Fenster im Vordergrund zum Hook-Zeitpunkt bekannt ist (`key.fg`). Eine beobachtete Wirkung ist dafür nicht nötig, weil die Tasten selbst die vollständige Aussage über die Eingabe sind; ob sie etwas bewirkten, steht getrennt in `effect` und `evidence.result`. Ist das Zielfenster undurchsichtig, bleibt die Stufe „belegt“ – was die Tasten dort auslösten, zeigt allein das Bild.

### 9.4 Abwertungen

Unabhängig von der Tabelle gilt für Aktionsschritte **höchstens „nur Eingabe“**, wenn

- mindestens ein Eingabeereignis aus dem zweiten Kanal ergänzt wurde (`recoveredInput`);
- die Eingabe unvollständig ist (`flags.incomplete`, Grund `incompleteInput`);
- die Oberfläche undurchsichtig ist (`opaqueSurface`) – außer bei Tastenkombinationen und Einzeltasten ([9.3](#belegstufen)). Für Texteingaben gibt es davon keine Ausnahme, auch nicht im Terminal (5.15);
- die Einrastfunktion nachgebildet wurde (`stickyKeysEmulated`) oder die Anschlagverzögerung aktiv ist, ohne dass Feldinhalt oder Wirkung die Deutung bestätigen;
- eine Eingabemethode aktiv war und kein lesbarer Feldinhalt vorliegt (`imeActive`).

### 9.5 Begründungscodes

| Code | Bedeutung |
|---|---|
| `elementAgreed` | Ziel vor und beim Klick übereinstimmend ermittelt. |
| `elementCorroborated` | Ziel einmal ermittelt und durch Reaktion des Elements bestätigt. |
| `elementInvoked` | Das Element meldete, dass es ausgelöst wurde. |
| `elementSingle` | Ziel nur einmal ermittelt, keine Bestätigung. |
| `elementConflict` | Zwei Ermittlungen fanden verschiedene Elemente. |
| `dwellStale` | Die Vorab-Ermittlung war beim Klick veraltet. |
| `hitWindowMatches`, `hitWindowMismatch` | Fenster unter dem Zeiger passt (nicht) zum ermittelten Element. |
| `rectImplausible` | Gemeldetes Rechteck enthielt den Klickpunkt nicht. |
| `uiaTimeout`, `uiaUnavailable` | Element-Ermittlung lief ins Zeitlimit bzw. lieferte nichts. |
| `opaqueSurface` | Oberfläche nur als Bild erfassbar. |
| `focusFieldKnown`, `focusUnknown` | Fokussiertes Feld (nicht) bekannt. |
| `valueMatchesKeys` | Feldinhalt bestätigt die Tastenfolge. |
| `valueFromField` | Eingefügter Text aus dem Feldinhalt abgelesen (Eingabemethode, Autovervollständigung). |
| `valueMismatch` | Feldinhalt widerspricht der Tastenfolge. |
| `valueUnreadable` | Feldinhalt nicht lesbar. |
| `valueChanged` | Ein Feldinhalt änderte sich im Wirkfenster. |
| `keysObserved`, `foregroundKnown` | Tasten vollständig, Zielfenster bekannt. |
| `effectObserved`, `noEffectObserved` | Wirkung (nicht) beobachtet. |
| `clipboardChanged`, `clipboardLineage` | Zwischenablage änderte sich; Herkunft des eingefügten Inhalts bekannt. |
| `maskedByDesign`, `privacyMode` | Eingabe bewusst nicht aufgezeichnet. |
| `recoveredInput`, `incompleteInput` | Siehe 9.4. |
| `imeActive`, `stickyKeysEmulated`, `injectedInput` | Besondere Eingabeumstände. |
| `causeUnobserved` | Keine auslösende Eingabe beobachtet. |

Neue Codes dürfen mit einer MINOR-Version der Regeln hinzukommen; Leser behandeln unbekannte Codes als Zusatzinformation.

### 9.6 „Unbekannt“ zählt als Lücke

Jeder Schritt mit der Stufe „unbekannt“ erzeugt einen Eintrag in `steps.json` → `gaps` – **ohne Ausnahme**:

| Schritt | `cause` des Eintrags |
|---|---|
| maskierte Texteingabe | `maskedInput` |
| `system` mit `cause: "unobserved"` | `unobserved` |
| `gap` | die Ursache aus dem Journal; bei einem endgültig geschwärzten Zeitraum `removedByAuthor` |

Das gilt auch dann, wenn der Zeitraum des Schritts schon in einer Lücke des Journals liegt. **Jedes** `text.masked` ergibt einen Eintrag `maskedInput` – auch eines, das in einer Lücke `privacyMode` steht, die selbst in `gaps` geführt wird. Die beiden Einträge sagen Verschiedenes: Die Lücke `privacyMode` nennt den Zeitraum, in dem Inhalte zurückgehalten wurden; `maskedInput` nennt die einzelne Eingabe, deren Inhalt fehlt. Ausgaben können so Zeit- und Inhaltslücken getrennt zählen ([KI-Export](06-ki-export-und-mcp.md)).

Damit kann der Vollständigkeitsbericht nie „keine Lücken“ melden, solange irgendwo etwas Unbekanntes steht.

---

<a id="wichtigkeitsstufen"></a>
## 10. Wichtigkeitsstufen

Schritte werden nie gelöscht, nur eingestuft. Die Anleitung für Menschen zeigt standardmäßig die Hauptschritte; das KI-Protokoll zeigt je nach Detailstufe mehr, in der Stufe „Vollständig“ alles.

| Stufe | `importance` | Standard für |
|---|---|---|
| **Haupt** | `primary` | `click`, `doubleClick`, `tripleClick`, `rightClick`; `drag` (außer Fenster verschieben oder vergrößern); `type`; `hotkey`; `key` mit Aktionstasten (Eingabetaste, Esc, Entf, Leertaste auf einem Bedienelement, Funktionstasten, Druck, Kontextmenütaste, Win allein); `comment`; `snapshot`; `marker` der Typen `error`, `expected` und `important`; `gap` mit möglichem Eingabeverlust |
| **Neben** | `secondary` | `scroll`; `drag` mit `dragKind` `moveWindow` oder `resizeWindow`; `key` mit Navigationstasten (Tab, Pfeiltasten, Bild auf/ab, Pos1, Ende, Rücktaste, Alt allein, Medientasten); `hover`, wenn sich ein Menü oder Popup öffnet; `windowSwitch`; `system` außer Titeländerungen; `wait`; `marker` vom Typ `note`; `gap` ohne Eingabeverlust |
| **Spur** | `trace` | `move`; `hover` mit bloßem Tooltip oder bloßer Bildänderung; `system` mit `titleChanged` oder kleinen Popups; `key` mit einem Umschalter allein (Umschalt, Strg) oder einem Feststeller; reine Bildlücken |

Weitere Regeln:

- `flags.discarded` ändert die Stufe nicht; verworfene Schritte werden von der Anleitung gesondert ausgelassen.
- Der Nutzer kann die Stufe je Schritt überschreiben (`edits.json`, `steps[].importance`) und Schritte ausblenden. Beides ändert `steps.json` nicht.
- Die Stufe hängt nie von der Belegstufe ab: Ein schlecht belegter Klick bleibt ein Hauptschritt.

---

<a id="aktionsvokabular"></a>
## 11. Aktionsvokabular

Jeder Aktionsschritt trägt in `action` eine neutrale, maschinenlesbare Kurzform. Das Vokabular lehnt sich an verbreitete Formate für Bedienfolgen an (die PyAutoGUI-nahe Aktionsmenge, wie sie auch Datensätze für Computer-Use-Agenten verwenden). Es dient dem **Verstehen und Vergleichen**; ein Abspielen ist kein Ziel dieses Projekts.

| `verb` | Weitere Felder | Entsteht aus |
|---|---|---|
| `click` | `m`, `x`, `y`, `button`, `clicks`, `mods` ○ | `click` |
| `doubleClick` | `m`, `x`, `y`, `mods` ○ | `doubleClick` |
| `tripleClick` | `m`, `x`, `y`, `mods` ○ | `tripleClick` |
| `rightClick` | `m`, `x`, `y`, `mods` ○ | `rightClick` |
| `middleClick` | `m`, `x`, `y`, `mods` ○ | `click` mit mittlerer Taste, einfach |
| `moveTo` | `m`, `x`, `y` | `hover`, `move` |
| `dragTo` | `m`, `x`, `y` (Start), `toM`, `toX`, `toY`, `button`, `mods` ○ | `drag` |
| `scroll` | `m`, `x`, `y`, `dx`, `dy` (Rasten; `dy` > 0 = nach oben, `dx` > 0 = nach rechts), `mods` ○ | `scroll` |
| `write` | `text`, `then` ○ | `type` mit bekanntem Text |
| `press` | `keys` (Liste), `presses` | `key` |
| `hotkey` | `keys` (Umschalter, dann Taste) | `hotkey`, `windowSwitch` über Alt+Tab |
| `wait` | `seconds` | `wait` |

Regeln:

- Koordinaten sind monitorrelative Pixel – also zugleich Bildpixel der dem Schritt zugeordneten Bilder.
- Tastennamen sind die kanonischen Namen des [Datenformats](04-datenformat.md#ev-key), Umschalter ohne Seitenangabe: `Ctrl`, `Alt`, `Shift`, `Win`. Die Namen der PyAutoGUI-Schreibweise ergeben sich durch Kleinschreibung (`Enter` → `enter`, `PageUp` → `pageup`).
- Endet eine Texteingabe mit einer Abschlusstaste, steht sie in `then`: `{ "verb": "write", "text": "notiz.txt", "then": { "verb": "press", "keys": ["Enter"], "presses": 1 } }`.
- `write.text` enthält nur, was `input.text` enthält. Ist der Text nicht eindeutig oder zurückgehalten, ist `action` `null` – es wird kein Text erfunden.
- Schritte ohne Nutzereingabe (`system`, `comment`, `snapshot`, `marker`, `gap`) haben `action: null`.

---

<a id="textvorlagen"></a>
## 12. Textvorlagen

### 12.1 Prinzip

Sätze entstehen beim Anzeigen und Exportieren:

```
Vorlage(Schritt, Sprache, Stil, Anrede)        →  { title, body }
```

Jede Vorlage liefert **je Sprache zwei Teile**:

| Teil | Feld | Inhalt | Hängt ab von |
|---|---|---|---|
| **Kurztitel** | `title` | Knappe Benennung des Schritts für Listen und Überschriften, höchstens 60 Zeichen, ohne Anrede | Sprache |
| **Text** | `body` | Der vollständige Satz: im Anleitungsstil die Anweisung, im Protokollstil die Beschreibung der Aktion | Sprache, Stil, Anrede |

Die beiden Teile entsprechen `title` und `body` der Texte in `edits.json` ([Datenformat](04-datenformat.md#edits-json)).

- Hat der Autor oder ein Agent für die Sprache einen eigenen Kurztitel oder Text hinterlegt (`edits.json`, `steps[].text`), gilt dieser; der jeweils andere Teil kommt weiter aus der Vorlage. Für andere Sprachen gilt die Vorlage; Nutzertexte werden nicht übersetzt.
- Die Vorlagen sind Teil des Programms und haben eine eigene Version. Sie ändern gespeicherte Daten nicht.
- Vorlagen dürfen nur verwenden, was im Schritt steht (Grundsatz S4).

| Stil | Zweck | Form |
|---|---|---|
| `protocol` | KI-Protokoll, technische Details im Viewer | Beschreibend, ohne Anrede: „Linksklick auf Schaltfläche ‚Speichern‘“ |
| `guide` | Anleitung für Menschen | Aufforderung: „Klicken Sie auf die Schaltfläche ‚Speichern‘.“ |

Für den Stil `guide` gibt es im Deutschen drei Anredeformen (`address`): `formal` (Sie, Standard), `informal` (du), `neutral` (Grundform: „Auf die Schaltfläche … klicken.“). Jede Vorlage des Anleitungsstils liegt in allen drei Formen vor ([12.5](#anredeformen)). Im Englischen gibt es nur die Befehlsform. Der Kurztitel steht im Deutschen immer in der Grundform und ist damit von der Anrede unabhängig.

**Sprachen der Vorlagen.** Alle Vorlagen (Kurztitel, Text in beiden Stilen, Ursachen von Lücken, Programm-Schritte) gibt es auf Deutsch (`de`), Englisch (`en`), Französisch (`fr`), Spanisch (`es`), Italienisch (`it`) und Niederländisch (`nl`); eine unbekannte Sprache erhält die englischen Texte. Die vier weiteren Sprachen kennen je eine feste Anredeform, wie sie Anleitungen dort üblich verwenden: Französisch und Spanisch die höfliche Befehlsform („Cliquez sur le bouton « Enregistrer ».“, „Haga clic en el botón «Guardar».“), Italienisch den Infinitiv („Fare clic sul pulsante «Salva».“), Niederländisch die Befehlsform („Klik op de knop ‘Opslaan’.“); die Wahl der Anrede ist dort ausgegraut. Kurztitel stehen in der Grundform („Cliquer sur « OK »“, „Op ‘OK’ klikken“). Die Oberfläche des Programms bleibt Deutsch oder Englisch; die weiteren Sprachen gelten nur für Ausgaben ([Anleitungs-Export §10](09-anleitungs-export.md#10-sprache-der-schritttexte)).

### 12.2 Zielbezeichnung

Die Bezeichnung des Ziels wird nach dieser **Rückfallkette** gebildet; die erste zutreffende Regel gilt:

| Nr. | Voraussetzung | Bezeichnung (Beispiel) |
|---|---|---|
| 1 | `el.name` vorhanden | Typwort + Name: Schaltfläche „Speichern“ |
| 2 | `el.labeledBy.name` vorhanden | Typwort + Name der Beschriftung: Feld „Dateiname“ |
| 3 | Element mit Wert, ohne Namen (`ComboBox`, `Edit`, `Spinner`) | Typwort + Wert: Auswahlliste mit dem Wert „Deutsch“ |
| 4 | `el.help` oder `el.desc` vorhanden | Typwort + Hilfetext in Klammern: Schaltfläche („Neues Dokument anlegen“) |
| 5 | `el.row` vorhanden | Eintrag in der Zeile „Müller, Anna“, Spalte „Ort“ |
| 6 | Benannter Vorfahr in `target.path` | Typwort + Ort: Schaltfläche im Bereich „Symbolleiste“ |
| 7 | sonst, oder `target.status` ist `position` oder `none` | Position: die Stelle bei (1180, 640) im Fenster „…“ – mit Verweis auf den Bildausschnitt |

Ergänzende Regeln:

- **Mehrdeutigkeit.** Ist `dupCount` größer als 1, wird unterschieden – bevorzugt über den Zeilenkontext (Schaltfläche „Bearbeiten“ in der Zeile „Rechnung 2026-114“), sonst über die Ordnungszahl (die 3. Schaltfläche „Bearbeiten“).
- **Bereinigung.** Führende und schließende Leerzeichen entfallen, Zeilenumbrüche werden Leerzeichen, ein abschließender Doppelpunkt einer Beschriftung entfällt („Dateiname:“ → „Dateiname“), Kennzeichen für Zugriffstasten (`&`) entfallen.
- **Länge.** Namen über 60 Zeichen werden mit „…“ gekürzt; der volle Name bleibt in den Details.
- **Keine Übersetzung.** Namen, Titel und Werte erscheinen so, wie die Anwendung sie zeigte – auch im englischen Text.
- **Anführung.** Deutsch „…“, Englisch “…”, Französisch « … » (mit geschützten Leerzeichen), Spanisch und Italienisch «…», Niederländisch ‘…’. Getippter Text und Feldinhalte stehen im Protokollstil als Code.
- **Sonderzeichen** in aufgezeichneten Texten entschärft der jeweilige Export für sein Zielformat ([KI-Export](06-ki-export-und-mcp.md), [Anleitungs-Export](09-anleitungs-export.md)).

### 12.3 Typwörter

| `ct` | Deutsch | Englisch |
|---|---|---|
| `Button`, `SplitButton` | die Schaltfläche | button |
| `Edit` | das Feld | field |
| `Document` | der Textbereich | text area |
| `CheckBox` | das Kontrollkästchen | checkbox |
| `RadioButton` | das Optionsfeld | option |
| `ComboBox` | die Auswahlliste | drop-down list |
| `List` | die Liste | list |
| `ListItem`, `TreeItem` | der Eintrag | item |
| `DataItem` | die Zeile | row |
| `Menu`, `MenuBar` | das Menü, die Menüleiste | menu, menu bar |
| `MenuItem` | der Menüpunkt | menu item |
| `TabItem` | die Registerkarte | tab |
| `Tree` | die Baumansicht | tree |
| `Table`, `DataGrid` | die Tabelle | table |
| `HeaderItem` | die Spaltenüberschrift | column header |
| `Hyperlink` | der Link | link |
| `Image` | das Bild | image |
| `Slider` | der Schieberegler | slider |
| `Spinner` | das Drehfeld | spinner |
| `ScrollBar` | die Bildlaufleiste | scroll bar |
| `ToolBar` | die Symbolleiste | toolbar |
| `StatusBar` | die Statusleiste | status bar |
| `TitleBar` | die Titelleiste | title bar |
| `Text` | der Text | text |
| `Window` | das Fenster | window |
| `Pane`, `Group`, `Custom`, `Unknown` und alle übrigen | das Element | element |

Im Protokollstil steht das Typwort ohne Artikel, im Anleitungsstil mit Artikel im passenden Fall.

In Französisch, Spanisch, Italienisch und Niederländisch steht das Typwort in beiden Stilen mit bestimmtem Artikel; die Vorlagen verschmelzen Präposition und Artikel, wo die Sprache es verlangt (Italienisch „su“ + „il pulsante“ → „sul pulsante“, „in“ + „il campo“ → „nel campo“; Französisch „de“ + „le bouton“ → „du bouton“; Spanisch „a“ + „el botón“ → „al botón“).

| `ct` | Französisch | Spanisch | Italienisch | Niederländisch |
|---|---|---|---|---|
| `Button`, `SplitButton` | le bouton | el botón | il pulsante | de knop |
| `Edit` | le champ | el campo | il campo | het veld |
| `Document` | la zone de texte | el área de texto | l'area di testo | het tekstgebied |
| `CheckBox` | la case à cocher | la casilla | la casella di controllo | het selectievakje |
| `RadioButton` | l'option | la opción | l'opzione | de optie |
| `ComboBox` | la liste déroulante | la lista desplegable | l'elenco a discesa | de vervolgkeuzelijst |
| `List` | la liste | la lista | l'elenco | de lijst |
| `ListItem`, `TreeItem` | l'entrée | la entrada | la voce | het item |
| `DataItem` | la ligne | la fila | la riga | de rij |
| `Menu`, `MenuBar` | le menu, la barre de menus | el menú, la barra de menús | il menu, la barra dei menu | het menu, de menubalk |
| `MenuItem` | la commande | la opción de menú | la voce di menu | het menu-item |
| `TabItem` | l'onglet | la pestaña | la scheda | het tabblad |
| `Tree` | l'arborescence | la vista de árbol | la struttura ad albero | de structuurweergave |
| `Table`, `DataGrid` | le tableau | la tabla | la tabella | de tabel |
| `HeaderItem` | l'en-tête de colonne | el encabezado de columna | l'intestazione di colonna | de kolomkop |
| `Hyperlink` | le lien | el vínculo | il collegamento | de koppeling |
| `Image` | l'image | la imagen | l'immagine | de afbeelding |
| `Slider` | le curseur | el control deslizante | il dispositivo di scorrimento | de schuifregelaar |
| `Spinner` | le compteur | el control numérico | la casella numerica | het numerieke veld |
| `ScrollBar` | la barre de défilement | la barra de desplazamiento | la barra di scorrimento | de schuifbalk |
| `ToolBar` | la barre d'outils | la barra de herramientas | la barra degli strumenti | de werkbalk |
| `StatusBar` | la barre d'état | la barra de estado | la barra di stato | de statusbalk |
| `TitleBar` | la barre de titre | la barra de título | la barra del titolo | de titelbalk |
| `Text` | le texte | el texto | il testo | de tekst |
| `Window` | la fenêtre | la ventana | la finestra | het venster |
| alle übrigen | l'élément | el elemento | l'elemento | het element |

### 12.4 Tastennamen

| Kanonisch | Deutsch | Englisch |
|---|---|---|
| `Ctrl`, `Alt`, `Shift`, `Win` | Strg, Alt, Umschalt, Win | Ctrl, Alt, Shift, Win |
| `Enter`, `Tab`, `Escape`, `Space`, `Backspace` | Eingabetaste, Tab, Esc, Leertaste, Rücktaste | Enter, Tab, Esc, Space, Backspace |
| `Delete`, `Insert`, `Home`, `End`, `PageUp`, `PageDown` | Entf, Einfg, Pos1, Ende, Bild auf, Bild ab | Delete, Insert, Home, End, Page Up, Page Down |
| `Left`, `Right`, `Up`, `Down` | Pfeil nach links / rechts / oben / unten | Left / Right / Up / Down Arrow |
| `PrintScreen`, `Apps` | Druck, Kontextmenütaste | Print Screen, Menu key |
| `F1` … `F24`, Buchstaben, Ziffern | unverändert | unverändert |
| Layoutabhängige Tasten (`Oem…`) | das Zeichen aus `ch`, sonst der kanonische Name | wie Deutsch |

Kombinationen werden mit `+` verbunden: `Ctrl+Shift+Enter` → „Strg+Umschalt+Eingabetaste“.

In den weiteren Sprachen heißt `Ctrl` überall „Ctrl“, `Alt`, `Tab`, `Win` und die Funktionstasten bleiben unverändert. Übersetzt werden die Tasten, die auf den Tastaturen der Sprache anders beschriftet sind; alle übrigen erscheinen wie aufgezeichnet:

| Kanonisch | Französisch | Spanisch | Italienisch | Niederländisch |
|---|---|---|---|---|
| `Shift` | Maj | Mayús | Maiusc | Shift |
| `Enter`, `Escape`, `Space` | Entrée, Échap, Espace | Intro, Esc, Espacio | Invio, Esc, Barra spaziatrice | Enter, Esc, Spatiebalk |
| `Backspace`, `Delete`, `Insert` | Retour arrière, Suppr, Inser | Retroceso, Supr, Insert | Backspace, Canc, Ins | Backspace, Delete, Insert |
| `Home`, `End`, `PageUp`, `PageDown` | Origine, Fin, Page précédente, Page suivante | Inicio, Fin, Re Pág, Av Pág | Home, Fine, Pag su, Pag giù | Home, End, PageUp, PageDown |
| `Left`, `Right`, `Up`, `Down` | Flèche gauche / droite / haut / bas | Flecha izquierda / derecha / arriba / abajo | Freccia sinistra / destra / su / giù | Pijl-links / -rechts / -omhoog / -omlaag |
| `PrintScreen`, `Apps` | Impr écran, touche Menu | Impr Pant, tecla de menú | Stamp, tasto Menu | PrintScreen, Menutoets |

Die deutschen Namen sind **eindeutig**: „Eingabetaste“, nie „Eingabe“ – das Wort bezeichnet in dieser Spezifikation sonst jede Eingabe des Nutzers. Endet ein Tastenname auf „taste“ (Eingabetaste, Leertaste, Rücktaste, Kontextmenütaste), entfällt in den Vorlagen das Wort „Taste“ davor: „Eingabetaste“ statt „Taste Eingabetaste“, „Drücken Sie die Eingabetaste.“ statt „Drücken Sie die Taste Eingabetaste.“

**Tastenfolgen im Protokollstil** (Zeile „Getippt“) verwenden unabhängig von der Sprache englische Marken in geschweiften Klammern. Die verbindliche Schreibweise legt der [KI-Export](06-ki-export-und-mcp.md) fest; aus den Tokens eines Schritts ([Datenformat](04-datenformat.md#steps-json)) entstehen:

| Token im Schritt | Marke | Beispiel |
|---|---|---|
| Zeichenkette | die Zeichen selbst | `notiz.txr` |
| `{ "key": … }` | Tastenname; mehrfach gedrückte Tasten werden wiederholt | `{Backspace}`, `{Left}{Left}{Left}` |
| `{ "masked": n }` | `{masked:n}` – n Tastenanschläge nach der Zählweise aus [5.4](#schrittarten) | `{masked:12}` |
| `{ "masked": null }` | `{masked}` – auch die Anzahl wurde nicht gespeichert | `{masked}` |
| `{ "uncertain": … }` | `{uncertain:…}` | `{uncertain:nihao}` |
| `{ "placeholder": "Name" }` | `{Name}` – ein Wert, den der Autor verallgemeinert hat. Platzhalter tragen nie den Namen einer Taste und nie eine der Marken mit Doppelpunkt; damit bleibt die Zeile eindeutig. | `{Benutzername}` |
| Abschlusstaste | Tastenname am Ende | `{Enter}` |

Eine Eingabe, die als auffällig gilt und noch nicht durchgesehen ist, ersetzt die Leseschicht für Wege ohne Oberfläche vollständig durch `{unreviewed:n}` ([Datenformat](04-datenformat.md#edits-review)); das ist keine Sache der Vorlage. Dabei zählt n bei getippten Läufen Tastenanschläge – nach derselben Zählweise wie `{masked:n}` –, bei Feldinhalten, Zwischenablage-Inhalten und Kommentaren Zeichen. Geschweifte Klammern im getippten Text selbst werden verdoppelt (`{{`, `}}`).

### 12.5 Vorlagen für den Text (`body`)

Platzhalter: **Z** = Zielbezeichnung, **F** = Fenstertitel in Anführung, **T** = Text, **K** = Tasten (Namen nach 12.4), **n** = Anzahl, **d** = Dauer. Anleitungsstil in der Form „Sie“; die beiden anderen Anredeformen stehen weiter unten. Jede Schrittart aus [5.0](#schrittarten) hat mindestens eine Zeile.

| Fall | Protokoll (de) | Protokoll (en) | Anleitung (de) | Anleitung (en) |
|---|---|---|---|---|
| Klick | Linksklick auf Z | Left click on Z | Klicken Sie auf Z. | Click Z. |
| Klick mit Umschalter | Strg+Linksklick auf Z | Ctrl+left click on Z | Halten Sie Strg gedrückt und klicken Sie auf Z. | Hold Ctrl and click Z. |
| Doppelklick | Doppelklick auf Z | Double click on Z | Doppelklicken Sie auf Z. | Double-click Z. |
| Dreifachklick | Dreifachklick auf Z | Triple click on Z | Klicken Sie dreimal schnell auf Z. | Triple-click Z. |
| Rechtsklick | Rechtsklick auf Z | Right click on Z | Klicken Sie mit der rechten Maustaste auf Z. | Right-click Z. |
| Klick, Ziel nur als Position | Linksklick an Position (x, y) im Fenster F | Left click at position (x, y) in window F | Klicken Sie im Fenster F auf die im Bild markierte Stelle. | In the window F, click the spot marked in the image. |
| Ziehen | Ziehen von Z nach Z′ | Drag from Z to Z′ | Ziehen Sie Z auf Z′. | Drag Z onto Z′. |
| Ziehen, Text markiert | Text markiert durch Ziehen: T | Text selected by dragging: T | Markieren Sie den Text T. | Select the text T. |
| Ziehen, Fenster | Fenster F verschoben / in der Größe geändert | Window F moved / resized | Verschieben Sie das Fenster F. / Ändern Sie die Größe des Fensters F. | Move / resize the window F. |
| Scrollen | Scrollen nach unten um n Rasten in Z | Scroll down n notches in Z | Scrollen Sie in Z nach unten. | Scroll down in Z. |
| Zoomen | Zoom mit Strg+Mausrad (n Rasten) | Zoom with Ctrl+wheel (n notches) | Vergrößern oder verkleinern Sie die Ansicht mit Strg+Mausrad. | Zoom with Ctrl+mouse wheel. |
| Texteingabe | Text in Z eingegeben | Text entered in Z | Geben Sie in Z „T“ ein. | Type “T” in Z. |
| … mit Abschlusstaste | … – abgeschlossen mit K (z. B. „– abgeschlossen mit Eingabetaste“) | … – finished with K | … und drücken Sie die Taste K. (z. B. „… und drücken Sie die Eingabetaste.“) | … and press K. |
| Texteingabe, maskiert (jeder Grund außer `privacyMode`) | Eingabe in Z: n Tasten, nicht aufgezeichnet (maskiert) | Input in Z: n keys, not recorded (masked) | Geben Sie Ihr Kennwort ein. | Enter your password. |
| Texteingabe, maskiert (`maskReason: "privacyMode"`) | Eingabe: n Tasten, nicht aufgezeichnet (Privatmodus) | Input: n keys, not recorded (private mode) | Geben Sie hier Ihre vertraulichen Daten ein. | Enter your confidential data here. |
| Texteingabe, maskiert, Anzahl nicht gespeichert (`{ "masked": null }`) | Eingabe in Z: nicht aufgezeichnet (maskiert) · im Privatmodus: Eingabe: nicht aufgezeichnet (Privatmodus) | Input in Z: not recorded (masked) · in private mode: Input: not recorded (private mode) | wie die beiden Zeilen darüber | as in the two rows above |
| Tastenkombination | Tastenkombination K | Keyboard shortcut K | Drücken Sie K. | Press K. |
| Einzeltaste | Taste K / n× Taste K | Key K / K pressed n times | Drücken Sie die Taste K. / Drücken Sie n-mal die Taste K. | Press K. / Press K n times. |
| Verweilen | Mauszeiger verweilt auf Z | Pointer rests on Z | Zeigen Sie auf Z. | Point to Z. |
| Mausbewegung ohne folgende Aktion (`move`) | Mausbewegung ohne folgende Aktion | Mouse movement without a following action | (kein Satz – der Schritt erscheint in Anleitungen nicht) | (no sentence – the step does not appear in guides) |
| Fensterwechsel | Fensterwechsel mit Alt+Tab zu F | Window switch with Alt+Tab to F | Wechseln Sie mit Alt+Tab zum Fenster F. | Press Alt+Tab to switch to the window F. |
| Fenster erschien | Fenster F erschien | Window F appeared | Das Fenster F wird angezeigt. | The window F appears. |
| Fenster geschlossen | Fenster F wurde geschlossen | Window F closed | Das Fenster F schließt sich. | The window F closes. |
| Vordergrundwechsel | Fenster F kam in den Vordergrund | Window F came to the foreground | Das Fenster F tritt in den Vordergrund. | The window F comes to the front. |
| Inhalt ohne Eingabe geändert | Inhalt von Z änderte sich ohne beobachtete Eingabe | Content of Z changed without observed input | Z wird automatisch ausgefüllt. | Z is filled in automatically. |
| Mitteilung | Mitteilung: T | Notification: T | Es erscheint die Mitteilung T. | The notification T appears. |
| Warten | Wartezeit von d | Waiting time of d | Warten Sie, bis das Fenster F erscheint. / Warten Sie, bis der Vorgang abgeschlossen ist. | Wait until the window F appears. / Wait for the operation to finish. |
| Kommentar | Kommentar des Autors: T | Author's comment: T | (der Kommentartext als Hinweis) | (the comment text as a note) |
| Schnappschuss | Schnappschuss | Snapshot | (Bild ohne Satz) | (image without sentence) |
| Marker, Typ `note` | Marker | Marker | (kein Satz – eine Sprungmarke für den Autor; nimmt er den Schritt in die Anleitung auf, erscheint die Bezeichnung als Hinweis) | (no sentence – a bookmark for the author; if included in the guide, its label appears as a note) |
| Marker, Typ `error` | Marker des Autors: Hier tritt der Fehler auf | Author's marker: The error occurs here | Hinweis: Hier tritt der Fehler auf. | Note: The error occurs here. |
| Marker, Typ `expected` | Marker des Autors: So sollte es aussehen | Author's marker: This is how it should look | Hinweis: So sollte es aussehen. | Note: This is how it should look. |
| Marker, Typ `important` | Marker des Autors: Wichtig | Author's marker: Important | Wichtig: Beachten Sie diese Stelle. | Important: Note this point. |
| Lücke | Lücke von d: Ursachentext | Gap of d: cause text | Hinweis: Ursachentext | Note: cause text |
| Lücke, Ursache `secureDesktop`, Art `uac` | Abfrage der Benutzerkontensteuerung (sicherer Desktop, d): Eingaben und Bilder wurden nicht aufgezeichnet. | User Account Control prompt (secure desktop, d): input and images were not recorded. | Bestätigen Sie die Abfrage der Benutzerkontensteuerung (Windows fragt, ob das Programm Änderungen vornehmen darf). Diese Abfrage wird aus Sicherheitsgründen nicht aufgezeichnet. | Confirm the User Account Control prompt (Windows asks whether the program may make changes). For security reasons this prompt is not recorded. |

Zu den Zeilen für zurückgehaltene Eingaben:

- **Welche Vorlage gilt**, bestimmt allein `input.maskReason`: `privacyMode` ergibt „(Privatmodus)“, jeder andere Grund „(maskiert)“. Hat die genauere Erkennung den Grund gesetzt – etwa `terminalPrompt` während eines Privatmodus –, gilt „(maskiert)“. Der Kurztitel (12.6) unterscheidet nur zwei Fälle: `privacyMode`, `strictUnknown` und `strictPlaintext` ergeben „Vertrauliche Eingabe“ („Confidential input“), `passwordField` „Kennwort eingeben“.
- **n** ist die Zahl der Tastenanschläge nach der Zählweise aus [5.4](#schrittarten); das Wort „Tasten“ steht dafür kurz.
- **Ohne gespeicherte Anzahl** entfällt die Zahl samt Komma; die Tastenfolge zeigt `{masked}`.
- Die **Abschlusstaste** wird wie bei jeder Texteingabe angehängt: „Eingabe in Z: 14 Tasten, nicht aufgezeichnet (maskiert) – abgeschlossen mit Eingabetaste“.

Zu den Zeilen für Marker:

- Welche Zeile gilt, bestimmt der **wirksame Typ**: `steps[].markerKind` aus `edits.json`, sonst `input.markerKind`. Ein unbekannter Typ wird wie `note` behandelt.
- Im Protokollstil sagt der Zusatz „des Autors“ ausdrücklich, dass der Satz eine Angabe des Autors ist und keine Beobachtung des Recorders. Ein Marker trägt deshalb auch nie eine Belegzeile mit Begründung aus der Aufzeichnung; seine Stufe „belegt“ sagt nur, dass er gesetzt wurde.
- Die Bezeichnung (`label`) steht nicht im Satz; Ausgaben führen sie als eigenes Feld. Im Anleitungsstil wird sie angehängt, wenn sie vorhanden ist: „Hinweis: Hier tritt der Fehler auf – T.“
- Die Sätze der typisierten Marker enthalten keine Aufforderung außer bei `important`; dort gelten die Anredeformen „Beachten Sie diese Stelle.“ – „Beachte diese Stelle.“ – „Diese Stelle beachten.“

Für `move` steht der Weg nicht im Satz; Ausgaben führen ihn als eigenes Feld. Den Wortlaut der Zeile „Wirkung“ im KI-Protokoll legt der [KI-Export](06-ki-export-und-mcp.md) fest; die Vorlagen hier liefern die Zeile „Aktion“, den Kurztitel und die Belegzeile.

**Varianten nach Elementtyp** (nur Anleitungsstil, einfacher Klick mit der Haupttaste):

| Ziel | Bedingung | Deutsch | Englisch |
|---|---|---|---|
| `CheckBox` | Wirkung `on` | Aktivieren Sie Z. | Select Z. |
| `CheckBox` | Wirkung `off` | Deaktivieren Sie Z. | Clear Z. |
| `RadioButton` | – | Wählen Sie Z. | Select Z. |
| `ComboBox` | – | Öffnen Sie Z. | Open Z. |
| `ListItem`, `TreeItem`, `DataItem` | – | Wählen Sie Z aus. | Select Z. |
| `TreeItem` | Wirkung `expanded` / `collapsed` | Klappen Sie Z auf. / Klappen Sie Z zu. | Expand Z. / Collapse Z. |
| `MenuItem` | – | Wählen Sie Z. | Choose Z. |
| `TabItem` | – | Wechseln Sie zu Z. | Switch to Z. |
| `Edit`, `Document` | – | Klicken Sie in Z. | Click in Z. |
| `Hyperlink` | – | Klicken Sie auf Z. | Click Z. |

<a id="anredeformen"></a>
**Anredeformen** (Deutsch, Anleitungsstil). Jede Aufforderung der beiden Tabellen oben gibt es in drei Formen; Sätze ohne Aufforderung („Das Fenster F wird angezeigt.“) sind in allen Formen gleich.

| `formal` (Sie) | `informal` (du) | `neutral` (Grundform) |
|---|---|---|
| Klicken Sie auf Z. | Klicke auf Z. | Auf Z klicken. |
| Halten Sie Strg gedrückt und klicken Sie auf Z. | Halte Strg gedrückt und klicke auf Z. | Strg gedrückt halten und auf Z klicken. |
| Doppelklicken Sie auf Z. | Doppelklicke auf Z. | Auf Z doppelklicken. |
| Klicken Sie dreimal schnell auf Z. | Klicke dreimal schnell auf Z. | Dreimal schnell auf Z klicken. |
| Klicken Sie mit der rechten Maustaste auf Z. | Klicke mit der rechten Maustaste auf Z. | Mit der rechten Maustaste auf Z klicken. |
| Klicken Sie im Fenster F auf die im Bild markierte Stelle. | Klicke im Fenster F auf die im Bild markierte Stelle. | Im Fenster F auf die im Bild markierte Stelle klicken. |
| Klicken Sie in Z. | Klicke in Z. | In Z klicken. |
| Ziehen Sie Z auf Z′. | Ziehe Z auf Z′. | Z auf Z′ ziehen. |
| Markieren Sie den Text T. | Markiere den Text T. | Den Text T markieren. |
| Verschieben Sie das Fenster F. | Verschiebe das Fenster F. | Das Fenster F verschieben. |
| Ändern Sie die Größe des Fensters F. | Ändere die Größe des Fensters F. | Die Größe des Fensters F ändern. |
| Scrollen Sie in Z nach unten. | Scrolle in Z nach unten. | In Z nach unten scrollen. |
| Vergrößern oder verkleinern Sie die Ansicht mit Strg+Mausrad. | Vergrößere oder verkleinere die Ansicht mit Strg+Mausrad. | Die Ansicht mit Strg+Mausrad vergrößern oder verkleinern. |
| Geben Sie in Z „T“ ein. | Gib in Z „T“ ein. | In Z „T“ eingeben. |
| Geben Sie in Z „T“ ein und drücken Sie die Eingabetaste. | Gib in Z „T“ ein und drücke die Eingabetaste. | In Z „T“ eingeben und die Eingabetaste drücken. |
| Geben Sie in Z „T“ ein und drücken Sie die Taste K. | Gib in Z „T“ ein und drücke die Taste K. | In Z „T“ eingeben und die Taste K drücken. |
| Geben Sie Ihr Kennwort ein. | Gib dein Kennwort ein. | Das Kennwort eingeben. |
| Geben Sie hier Ihre vertraulichen Daten ein. | Gib hier deine vertraulichen Daten ein. | Hier die vertraulichen Daten eingeben. |
| Drücken Sie K. | Drücke K. | K drücken. |
| Drücken Sie die Taste K. | Drücke die Taste K. | Die Taste K drücken. |
| Drücken Sie n-mal die Taste K. | Drücke n-mal die Taste K. | n-mal die Taste K drücken. |
| Zeigen Sie auf Z. | Zeige auf Z. | Auf Z zeigen. |
| Wechseln Sie mit Alt+Tab zum Fenster F. | Wechsle mit Alt+Tab zum Fenster F. | Mit Alt+Tab zum Fenster F wechseln. |
| Warten Sie, bis das Fenster F erscheint. | Warte, bis das Fenster F erscheint. | Warten, bis das Fenster F erscheint. |
| Warten Sie, bis der Vorgang abgeschlossen ist. | Warte, bis der Vorgang abgeschlossen ist. | Warten, bis der Vorgang abgeschlossen ist. |
| Wichtig: Beachten Sie diese Stelle. | Wichtig: Beachte diese Stelle. | Wichtig: Diese Stelle beachten. |
| Aktivieren Sie Z. | Aktiviere Z. | Z aktivieren. |
| Deaktivieren Sie Z. | Deaktiviere Z. | Z deaktivieren. |
| Wählen Sie Z. | Wähle Z. | Z wählen. |
| Wählen Sie Z aus. | Wähle Z aus. | Z auswählen. |
| Öffnen Sie Z. | Öffne Z. | Z öffnen. |
| Klappen Sie Z auf. / Klappen Sie Z zu. | Klappe Z auf. / Klappe Z zu. | Z aufklappen. / Z zuklappen. |
| Wechseln Sie zu Z. | Wechsle zu Z. | Zu Z wechseln. |

Bildungsregel für künftige Vorlagen: `formal` = Verb in der Sie-Form + „Sie“ + Rest; `informal` = Befehlsform der zweiten Person Einzahl + Rest, Besitzwörter „dein/deine“ statt „Ihr/Ihre“; `neutral` = Rest + Verb in der Grundform am Satzende, trennbare Verben zusammengeschrieben, Besitzwörter durch den Artikel ersetzt. Eine Vorlage gilt erst als vollständig, wenn alle drei Formen vorliegen.

**Ursachentexte für Lücken**

| Ursache | Deutsch | Englisch |
|---|---|---|
| `paused` | Die Aufnahme war angehalten. | Recording was paused. |
| `privacyMode` | Privatmodus: Eingaben wurden nicht aufgezeichnet. | Private mode: input was not recorded. |
| `excludedApp` | Eine ausgeschlossene Anwendung war im Vordergrund; davon wurde nichts aufgezeichnet. | An excluded application was in the foreground; none of it was recorded. |
| `outOfScope` | Arbeit außerhalb des Aufnahmebereichs; nicht aufgezeichnet. | Work outside the recording scope; not recorded. |
| `secureDesktop` | Sicherer Desktop (z. B. Abfrage der Benutzerkontensteuerung): Eingaben und Bilder konnten nicht aufgezeichnet werden. | Secure desktop (e.g. a User Account Control prompt): input and images could not be recorded. |
| `sessionLocked`, `sessionDisconnected`, `suspend` | Der Rechner war gesperrt, getrennt oder im Ruhezustand. | The computer was locked, disconnected or asleep. |
| `elevatedWindow` | Ein Fenster mit Administratorrechten war im Vordergrund: Eingaben waren nicht sichtbar, Bilder wurden aufgezeichnet. | A window with administrator rights was in the foreground: input was not visible, images were recorded. |
| `hookLost`, `inputMismatch`, `overload` | Eingaben wurden möglicherweise unvollständig aufgezeichnet. | Input may have been recorded incompletely. |
| `captureLost`, `frameDropped` | Für diesen Zeitraum fehlen Bilder. | Images are missing for this period. |
| `protectedContent` | Geschützter Inhalt wurde vom System ausgeblendet. | Protected content was hidden by the system. |
| `diskFull` | Der Datenträger war voll; die Aufnahme war angehalten. | The disk was full; recording was paused. |
| `crash` | Der Recorder wurde unerwartet beendet; ab hier fehlt die Aufzeichnung. | The recorder ended unexpectedly; the recording is missing from here. |
| `unsupportedInput` | Eine nicht aufzeichenbare Eingabe (z. B. Mehrfinger-Geste) wurde erkannt. | An input that cannot be recorded (e.g. a multi-finger gesture) was detected. |
| `relativeMouse` | Der Mauszeiger war von der Anwendung gefangen; Positionen sind ohne Aussage. | The pointer was captured by the application; positions are not meaningful. |
| `maskedInput` | Eine Tasteneingabe wurde bewusst nicht aufgezeichnet. | A keyboard input was deliberately not recorded. |
| `unobserved` | Die Ursache wurde nicht beobachtet. | The cause was not observed. |
| `removedByAuthor` | Dieser Zeitraum wurde vom Autor entfernt. | This period was removed by the author. |
| `unknown` und alle übrigen | Die Aufzeichnung ist hier unvollständig. | The recording is incomplete here. |

**Belegzeile** (nur Protokollstil): „Beleg: belegt“, „Beleg: nur Eingabe“, „Beleg: unbekannt“ – englisch “Evidence: confirmed”, “Evidence: input only”, “Evidence: unknown” –, gefolgt von den Begründungen aus 9.5 in Klammern, z. B. „Beleg: belegt (Ziel vor und beim Klick übereinstimmend)“.

### 12.6 Vorlagen für den Kurztitel (`title`)

Der Kurztitel benennt den Schritt so knapp wie möglich. Er verwendet den bloßen Namen des Ziels (**N**) ohne Typwort; fehlt ein Name, steht das Typwort, und ist das Ziel nur eine Position, das Fenster. Im Deutschen steht er in der Grundform. Überschreitet er 60 Zeichen, wird der Name gekürzt.

| Fall | Kurztitel (de) | Kurztitel (en) |
|---|---|---|
| Klick | „N“ anklicken | Click “N” |
| Doppelklick, Dreifachklick | Doppelklick auf „N“ · Dreifachklick auf „N“ | Double-click “N” · Triple-click “N” |
| Rechtsklick | Rechtsklick auf „N“ | Right-click “N” |
| Klick, Ziel nur als Position | Klick im Fenster F | Click in window F |
| Kontrollkästchen | „N“ aktivieren · „N“ deaktivieren | Select “N” · Clear “N” |
| Auswahlliste | „N“ öffnen | Open “N” |
| Eintrag, Zeile, Optionsfeld | „N“ auswählen | Select “N” |
| Menüpunkt | Menüpunkt „N“ wählen | Choose “N” |
| Registerkarte | Zu „N“ wechseln | Switch to “N” |
| Feld (Klick) | In „N“ klicken | Click in “N” |
| Ziehen | „N“ ziehen · Text markieren · Fenster verschieben · Fenstergröße ändern | Drag “N” · Select text · Move window · Resize window |
| Scrollen, Zoomen | In „N“ scrollen · Ansicht zoomen | Scroll in “N” · Zoom view |
| Texteingabe mit bekanntem Text | „T“ eingeben (T auf 30 Zeichen gekürzt) | Type “T” |
| Texteingabe ohne bekannten Text | Text in „N“ eingeben | Enter text in “N” |
| Texteingabe, maskiert (mit oder ohne gespeicherte Anzahl) | Kennwort eingeben · Vertrauliche Eingabe | Enter password · Confidential input |
| Tastenkombination, Einzeltaste | K drücken · K n-mal drücken (z. B. „Eingabetaste drücken“) | Press K · Press K n times |
| Verweilen | Auf „N“ zeigen | Point to “N” |
| Mausbewegung ohne folgende Aktion | Mausbewegung | Mouse movement |
| Fensterwechsel | Zu F wechseln | Switch to F |
| Fenster erschien, geschlossen, im Vordergrund | F erscheint · F schließt sich · F im Vordergrund | F appears · F closes · F in front |
| Inhalt ohne Eingabe geändert | „N“ ändert sich | “N” changes |
| Mitteilung | Mitteilung | Notification |
| Warten | Warten | Wait |
| Kommentar | Kommentar | Comment |
| Schnappschuss | Schnappschuss | Snapshot |
| Marker, Typ `note` | Marker | Marker |
| Marker, Typ `error` | Fehler tritt auf | Error occurs |
| Marker, Typ `expected` | Soll-Zustand | Expected state |
| Marker, Typ `important` | Wichtige Stelle | Important point |
| Lücke | Lücke: Kurzform der Ursache, z. B. „Lücke: sicherer Desktop“ | Gap: short cause, e.g. “Gap: secure desktop” |
| Lücke, Ursache `secureDesktop`, Art `uac` | Benutzerkontensteuerung bestätigen | Confirm User Account Control |

Die Kurzform der Ursache ist der Kern des Ursachentexts aus 12.5 ohne Satzform: Aufnahme angehalten, Privatmodus, ausgeschlossene Anwendung, außerhalb des Aufnahmebereichs, sicherer Desktop, Rechner gesperrt, Fenster mit Administratorrechten, Eingaben unvollständig, Bilder fehlen, geschützter Inhalt, Datenträger voll, Aufzeichnung abgebrochen, nicht aufzeichenbare Eingabe, Mauszeiger gefangen, maskierte Eingabe, Ursache nicht beobachtet, vom Autor entfernt – englisch entsprechend.

### 12.7 Zusätze nach Lage

| Lage | Zusatz im Protokollstil | Wirkung im Anleitungsstil |
|---|---|---|
| `input.commonMeaning` vorhanden | „(üblich: Speichern)“ | keiner |
| `target.status` ist `single` – nur bei Mausschritten und Texteingaben. Bei `hotkey` und `key` ist das Ziel eine Zusatzangabe ([6.4](#zielbestimmung)) und erhält keinen Zusatz. | „(Ziel nicht bestätigt)“ | keiner; der Viewer kennzeichnet den Schritt |
| `target.status` ist `conflict` | „(Ziel uneindeutig; Alternative: …)“ | keiner; der Viewer kennzeichnet den Schritt |
| `flags.injected` | „(automatisch erzeugte Eingabe)“ | keiner |
| `flags.discarded` | „(vom Autor verworfen)“ | Schritt entfällt |
| `flags.incomplete` | „(unvollständig aufgezeichnet)“ | keiner |
| `clipboard` mit `op: "paste"` | „eingefügt: Inhalt cb2, kopiert in Schritt N aus Fenster F, 34 Zeichen“ | keiner |
| `effect.observed` ist `false` | „keine Wirkung beobachtet“ | keiner |

### 12.8 Beispiele

Die drei Schritte aus dem Beispiel des [Datenformats](04-datenformat.md#beispiel-a), jeweils Kurztitel und Text:

| Schritt | Kurztitel (de) | Text, Protokoll (de) | Text, Anleitung (de, Sie) | Kurztitel (en) | Text, Anleitung (en) |
|---|---|---|---|---|---|
| `s1657` | Strg+S drücken | Tastenkombination Strg+S (üblich: Speichern) | Drücken Sie Strg+S. | Press Ctrl+S | Press Ctrl+S. |
| `s1700` | „notiz.txt“ eingeben | Text in Feld „Dateiname“ eingegeben | Geben Sie in das Feld „Dateiname“ „notiz.txt“ ein. | Type “notiz.txt” | Type “notiz.txt” in the field “Dateiname”. |
| `s1734` | „Speichern“ anklicken | Linksklick auf Schaltfläche „Speichern“ | Klicken Sie auf die Schaltfläche „Speichern“. | Click “Speichern” | Click the button “Speichern”. |

Derselbe Klick in den drei Anredeformen: „Klicken Sie auf die Schaltfläche ‚Speichern‘.“ – „Klicke auf die Schaltfläche ‚Speichern‘.“ – „Auf die Schaltfläche ‚Speichern‘ klicken.“

Die vollständige Darstellung eines Schritts im KI-Protokoll – mit Zeit, Position, Mausweg, Wirkung, Beleg und Bildern – legt der [KI-Export](06-ki-export-und-mcp.md) fest; die Vorlagen liefern dafür die Zeile „Aktion“ und die Belegzeile.

<a id="ziel-vorlagen"></a>
### 12.9 Ziel-Angaben der Sitzung

Die Ziel-Angaben ([Datenformat](04-datenformat.md#manifest-goal)) gehören zu keinem Schritt. Sie stehen im Kopf einer Ausgabe – wo genau, legen [KI-Export](06-ki-export-und-mcp.md) und [Anleitungs-Export](09-anleitungs-export.md) fest. Die Vorlagen liefern je Angabe eine Zeile:

| Angabe | Protokoll (de) | Protokoll (en) | Anleitung (de) | Anleitung (en) |
|---|---|---|---|---|
| `description` | Ziel der Aufnahme (Angabe des Autors): „T“ | Purpose of the recording (stated by the author): “T” | Ziel: T | Purpose: T |
| `expected` | Erwartetes Ergebnis (Angabe des Autors): „T“ | Expected result (stated by the author): “T” | Erwartetes Ergebnis: T | Expected result: T |
| `reference` | Bezug (Angabe des Autors): „T“ | Reference (stated by the author): “T” | Bezug: T | Reference: T |

Regeln:

- Verwendet werden die **wirksamen** Angaben – nach Overlay, Schwärzungen, Durchsicht und Verallgemeinerungen, also so, wie die Leseschicht sie liefert.
- Fehlt eine Angabe, entfällt ihre Zeile. Fehlen alle, entfällt der Block; die Vorlagen erfinden kein Ziel und leiten keines aus den Schritten ab (S4).
- Im Protokollstil steht jede Angabe in Anführung und mit dem Zusatz „Angabe des Autors“: Sie ist **Absicht, keine Beobachtung**. Ob das erwartete Ergebnis eingetreten ist, sagt allein die aufgezeichnete Wirkung der Schritte – kein Vorlagentext behauptet es.
- Die Zeilen enthalten keine Aufforderung und sind in allen Anredeformen gleich.
- Kurztitel gibt es für Ziel-Angaben nicht.

---

<a id="vollstaendigkeitspruefung"></a>
## 13. Vollständigkeitsprüfung

### 13.1 Zwei getrennte Aussagen

Der Vollständigkeitsbericht (`steps.json` → `completeness`, Kurzfassung im Manifest) beantwortet zwei verschiedene Fragen und hält sie auseinander:

| Frage | Teil des Berichts | Kann „in Ordnung“ sein, obwohl … |
|---|---|---|
| **Ist alles Aufgezeichnete verbucht?** | `bookkeeping` | … wichtige Dinge gar nicht aufgezeichnet werden konnten. |
| **Wie gut ist das Verbuchte belegt, und was fehlt?** | `evidence`, `gaps`, `frames` | – |

Ein Bericht darf deshalb nie auf eine einzige Zahl verkürzt werden. Viewer, KI-Protokoll und MCP-Server geben stets beide Teile aus.

### 13.2 Buchführung

| Regel | Aussage | Feld |
|---|---|---|
| B1 | Jedes Eingabeereignis liegt in genau einem `seq`-Bereich (`input` oder `approach`) genau eines Schritts. | `inputEvents`, `assigned`, `orphans`, `orphanSeqRanges`, `duplicates` |
| B2 | Kein Bereich eines Schritts enthält ein Eingabeereignis eines anderen Schritts. | geht in `duplicates` ein |
| B3 | Die Zeitleiste teilt die Sitzungsdauer lückenlos in `steps`, `idle` und `gap`. | `timeline.stepsUs`, `idleUs`, `gapsUs`, `unaccountedUs` |
| B4 | Jede Lücke des Journals steht genau einmal in `gaps`; jedes `text.masked` steht dort als `maskedInput`. | geht in `ok` ein |
| B5 | Jedes `comment` und jeder `marker` (außer `discardPrevious` und `redactLastInput`) hat genau einen Schritt. | geht in `ok` ein |

`bookkeeping.ok` ist genau dann `true`, wenn B1 bis B5 gelten, also `orphans` = 0, `duplicates` = 0 und `unaccountedUs` = 0. Eingaben mit `self: true` zählen gesondert in `selfInputEvents`.

**Zeitleiste.** Jeder Zeitpunkt der Sitzung wird eingestuft: `gap`, wenn er in einer Lücke liegt, die Tastatur, Maus oder alles betrifft; sonst `steps`, wenn er zwischen `tFrom` und `tTo` mindestens eines Schritts liegt; sonst `idle`. Benachbarte Zeitpunkte gleicher Einstufung bilden einen Abschnitt.

### 13.3 Belegqualität

| Feld | Berechnung |
|---|---|
| `evidence.actionSteps` | Anzahl der Aktionsschritte (Abschnitt 2), ohne verworfene. |
| `confirmed`, `inputOnly`, `unknown` | Verteilung dieser Schritte auf die Belegstufen. |
| `confirmedRatio` | `confirmed / actionSteps`; bei 0 Aktionsschritten `null`. |
| `byReason` | Für jeden nicht belegten Aktionsschritt der erste Begründungscode; Anzahl je Code. |
| `gaps.count`, `totalUs`, `byCause` | Aus der Lückenliste, einschließlich `maskedInput` und `unobserved`. |
| `frames.requested` | Anzahl `frame`-Ereignisse. |
| `frames.stored`, `same`, `dropped` | Nach `store.kind`: `key`/`delta`, `same`, `none` mit `dropped` oder `captureFailed`. |
| `frames.preGuaranteed`, `preNotGuaranteed` | Aktionsschritte mit bzw. ohne garantiertes Vorher-Bild. |

### 13.4 Was der Bericht aussagt – und was nicht

- Er **sagt**: Alles, was im Journal steht, ist einem Schritt zugeordnet; diese Schritte sind so und so gut belegt; in diesen Zeiträumen fehlt aus diesen Gründen etwas.
- Er **sagt nicht**: dass außerhalb der genannten Lücken nichts geschah, was der Recorder nicht sehen konnte. Eine Eingabe, die weder der Hook noch der zweite Kanal noch eine Folgewirkung verrät, ist für jedes Aufzeichnungswerkzeug unsichtbar. Der Bericht benennt deshalb die bekannten blinden Flecken, statt Vollständigkeit zu behaupten.

### 13.5 Prüfungen auf Schritt-Ebene

Der Befehl `validate` ([Architektur](02-architektur.md)) führt nach den Formatprüfungen F01–F25 ([Datenformat](04-datenformat.md#absturzsicherheit)) diese Prüfungen aus:

| Code | Prüfung | Schwere |
|---|---|---|
| S01 | `steps.json` entspricht dem Schema; `stepBuilderVersion` und `params` sind vorhanden. | Fehler |
| S02 | Jedes `anchorSeq` existiert, und der Typ des Ankerereignisses passt zur Schrittart. | Fehler |
| S03 | Buchführung B1 und B2. | Fehler |
| S04 | Zeitleiste B3: lückenlos, überschneidungsfrei, Summe = Sitzungsdauer. | Fehler |
| S05 | Lückenliste B4 vollständig; jeder Schritt der Stufe „unbekannt“ ist von einem Eintrag gedeckt. | Fehler |
| S06 | Jede genannte Bild-Kennung existiert; für Vorher-Bilder ohne `preNotGuaranteed` gilt `t` des Bildes < `t` des Ankerereignisses. | Fehler |
| S07 | Eine Neuableitung mit gleicher Regelversion und gleichen Parametern ergibt dieselben Schritte. | Fehler |
| S08 | Jeder inhaltstragende Wert hat einen `prov`-Eintrag, dessen Quelle existiert. | Fehler |
| S09 | Die Belegstufe jedes Schritts folgt aus seinen Bestandteilen nach Abschnitt 9. | Fehler |
| S10 | Kein Schritt nennt ein Element, dessen Rechteck den Klickpunkt nicht enthält. | Fehler |
| S11 | `completeness` stimmt mit den Schritten überein; die Kurzfassung im Manifest stimmt mit `completeness` überein. | Fehler |
| S12 | Einträge in `edits.json` lassen sich zuordnen oder stehen unter `orphans`. | Warnung |
| S13 | Anteil belegter Aktionsschritte, Zahl und Dauer der Lücken (reine Ausgabe, keine Bedingung). | Hinweis |
| S14 | Die wirksame Schrittfolge lässt sich bilden ([16](#wirksame-schrittfolge)): Jeder Schritt liegt entweder in einem verworfenen Abschnitt oder hat genau einen Zustand – wirksam, ersetzt oder nicht verwendet –, und jede Ersetzung ist entweder wirksam oder begründet ohne Wirkung. Die Schrittnummern der wirksamen Schritte sind lückenlos ab 1. Jeder `marker`-Schritt trägt einen bekannten Typ. | Fehler |
| S15 | Eine Neuaufnahme ohne Ersetzung (nicht verwendet), eine Ersetzung ohne Wirkung und jeder verworfene Abschnitt werden gemeldet. | Hinweis |

Eine Sitzung mit Lücken oder schwach belegten Schritten ist **gültig**: `validate` meldet solche Befunde als Hinweis, nicht als Fehler. Ein Fehler liegt nur vor, wenn die Dateien dem Format oder den Regeln widersprechen.

---

<a id="mitlaufend"></a>
## 14. Mitlaufende Erkennung

Die Ableitung muss auch **während der Aufnahme** laufen können: für die kurze Rückmeldung in der Aufnahmeleiste („zuletzt erkannter Schritt“) und damit der Viewer unmittelbar nach dem Stopp Schritte zeigen kann.

| Regel | Inhalt |
|---|---|
| M1 Vorläufige Schritte | Ein Schritt wird gemeldet, sobald seine Eingabe abgeschlossen ist (Klick: nach Ablauf der Mehrfachklick-Frist; Texteingabe: nach ihrem Ende; Tastenkombination: sofort). Er ist bis zum Abschluss als vorläufig gekennzeichnet. |
| M2 Abschluss | Ein Schritt ist abgeschlossen, wenn sein Wirkfenster geschlossen ist und alle Anreicherungen mit `ref` auf seine Ereignisse eingetroffen oder abgelaufen sind. Die Fristen dafür sind endlich: das Zeitlimit der Element-Ermittlung und `effectSettleMaxMs`. |
| M3 Ergebnisgleichheit | Die beim Stopp geschriebene `steps.json` ist identisch mit dem Ergebnis einer Ableitung in einem Durchgang über das fertige Journal – also über den Stand **nach** dem endgültigen Anwenden einer Schwärzung, die während der Aufnahme über „Letzte Eingabe schwärzen“ angefordert wurde. |
| M4 Zeitbudget | Nach dem Stopp liegt `steps.json` für eine Sitzung mit 1000 Schritten innerhalb von 300 ms vor. |
| M5 Kein Rückstau | Die mitlaufende Erkennung liest die Ereignisse hinter dem Schreiber, also nach der Zurückhaltung; sie sieht nie mehr als das Journal und darf den Recorder nicht bremsen. Kommt sie nicht nach, überspringt sie Rückmeldungen, nie Daten. |
| M6 Rückmeldung | Die Leiste zeigt die Zeile „Aktion“ im Protokollstil. Vorläufige Angaben dürfen unvollständig sein („Klick …“, bevor das Ziel feststeht). |
| M7 Zurückgehaltener Zeitraum | Nach „Letzte Eingabe schwärzen“ (`marker` mit `kind: "redactLastInput"`) verwirft die mitlaufende Erkennung alle vorläufigen und abgeschlossenen Schritte, deren Eingaben im betroffenen Zeitraum liegen, und führt an ihrer Stelle die Lücke „vom Autor entfernt“. Die Rückmeldung der Leiste zeigt von diesem Moment an nichts mehr aus dem Zeitraum. |

Vorläufige Schritte werden nicht gespeichert. Nach einem Absturz wird in einem Durchgang abgeleitet – nachdem die Wiederherstellung eine ausstehende Schwärzung aus der Aufnahme nachgeholt hat ([Datenformat](04-datenformat.md#absturzsicherheit)).

---

<a id="regelversion"></a>
## 15. Regel-Versionierung und Neuberechnung

### 15.1 Regelversion

`stepBuilderVersion` hat die Form `MAJOR.MINOR.PATCH`:

| Teil | Steigt, wenn | Folge |
|---|---|---|
| PATCH | Fehlerbehebung oder Beschleunigung **ohne** Änderung der Ergebnisse für gültige Journale. | Keine Neuberechnung nötig. |
| MINOR | Regeln, Schwellwerte oder Begründungscodes ändern sich so, dass Ergebnisse abweichen können; der Aufbau von `steps.json` bleibt verträglich. | Neuberechnung ist möglich, aber freiwillig. |
| MAJOR | Aufbau oder Bedeutung von Schritten ändert sich. | Geht mit einer neuen `schemaVersion` einher ([Datenformat](04-datenformat.md#versionierung)). |

Die umgesetzte Regelversion ist `1.7.0` (1.6.0: Zusammenführung von Terminal-Klartext, UAC-Schritt, Menüpfaden und Begriffsliste; 1.7.0: Programm-Schritte aus [12, Paket 10](12-entwickler-fahrplan.md) – `process.exit` wird zum Systemschritt `appExit` (Absturz Hauptschritt, Fehlercode Nebenschritt, normales Ende Spurdaten), `window.hang` zu `appHang` (Hauptschritt mit Dauer), ein Eintrag der Ereignisanzeige ohne beobachtetes Ende zu `appEventLog`; Fakten der Ereignisanzeige hängen am zugehörigen Schritt unter `input.eventLog`); sie enthält das Klartext-Kennzeichen für alle Fenster außer `structured` (5.4) und den Lückenschritt für die Benutzerkontensteuerung (5.12). Zur Regelversion gehören: die Regeln der Abschnitte 4 bis 10 und 13, die Standardwerte der Parameter und die Liste der Begründungscodes. **Nicht** dazu gehören die Textvorlagen (Abschnitt 12) – sie verändern keine gespeicherten Daten. Ebenfalls nicht dazu gehören die wirksame Schrittfolge, der Schrittabgleich und die Qualitätskriterien (Abschnitte 16 bis 18): Sie lesen `steps.json`, schreiben aber nichts hinein. Der Schrittabgleich trägt eine eigene Version (`compareVersion`).

### 15.2 Goldene Journale

Jede Regelversion wird gegen eine Sammlung gespeicherter Journale mit erwarteter `steps.json` geprüft („goldene Journale“). Die Sammlung enthält mindestens:

- den einfachen Fall und die schwierigen Fälle aus dem Anhang des [Datenformats](04-datenformat.md#beispiele);
- je einen Fall für AltGr, Tottasten, Alt+Ziffernblock, Auto-Repeat, Eingabemethode, Einrastfunktion, künstliche Eingaben;
- Mehrfachklicks bei kurzer, üblicher und langer Doppelklickzeit;
- alle Arten des Ziehens;
- Touchpad-Scrollen und -Zoomen;
- veraltete Vorab-Ermittlung (gescrollt, dann geklickt), widersprüchliche Ermittlungen, Zeitüberschreitung;
- unvollständige Eingaben an Lücken;
- jedes dieser Journale zusätzlich in Fassungen mit verspätet geschriebenen Anreicherungen (gleiche `t`, andere Reihenfolge der Zeilen) – das Ergebnis muss gleich bleiben.

Jede Abweichung gegenüber der Vorversion wird im Änderungsprotokoll der Regeln begründet.

### 15.3 Neuberechnung

Die Regelversion einer Sitzung ist festgehalten ([Datenformat](04-datenformat.md#versionierung)); neu berechnet wird nur auf ausdrücklichen Wunsch. Danach werden die Bearbeitungen aus `edits.json` in dieser Reihenfolge auf die neuen Schritte angewendet – in der Darstellung, nicht in `steps.json`:

1. Einträge über ihre Anker zuordnen; nicht Zuordenbares nach `orphans` verschieben.
2. Teilungen (`splits`) anwenden.
3. Zusammenfassungen (`merges`) anwenden.
4. `hidden`, `importance`, `view`, `frameChoice` anwenden.
5. Reihenfolge (`order`), Kapitel und eingefügte Schritte anwenden.

Vor dem Ersetzen zeigt der Viewer, was sich ändert (hinzugekommene, entfallene, anders eingestufte Schritte); die vorige Fassung bleibt als `steps.<alte Regelversion>.json` erhalten.

---

<a id="wirksame-schrittfolge"></a>
## 16. Wirksame Schrittfolge: Ersetzungen und Abschnitte

`steps.json` enthält **alle** Schritte aller Aufnahmeabschnitte in zeitlicher Reihenfolge – auch die, die der Autor später durch eine Neuaufnahme ersetzt hat. Das bleibt so: Die Ableitung ist eine reine Funktion des Journals (S1), Ersetzungen stehen im Overlay ([Datenformat](04-datenformat.md#edits-replacements)).

*Stand der Umsetzung:* Aufnahmeabschnitte, Ersetzungen und Nahtstellen (16.1 bis 16.5) sind noch nicht umgesetzt – „Abschnitt neu aufnehmen und ersetzen“ ist zurückgestellt. Die Leseschicht kennt bisher die Reihenfolge (16.2, letzter Absatz), das Ausblenden, eigene eingefügte Schritte und das Zusammenfassen (16.7); die Schrittnummer ist die Position in der Schrittliste.

Was Anleitung, KI-Paket, Schrittabgleich und Qualitätsprüfung als **Ablauf** zeigen, ist die **wirksame Schrittfolge**. Sie entsteht in der Leseschicht und ist ihrerseits eine reine Funktion von `steps.json`, den Abschnitten der Sitzung, `edits.replacements[]` und `edits.sections[]`.

### 16.1 Zustand je Schritt

Maßgeblich ist der Abschnitt, in dem das Ankerereignis des Schritts liegt.

**Verworfene Abschnitte zuerst.** Ein Abschnitt, den der Autor verworfen hat ([Datenformat](04-datenformat.md#edits-sections)), gehört zu **keiner** Ausgabe. Seine Schritte stehen zwar in `steps.json` – das Journal wird nie gekürzt, und die Ableitung liest das ganze Journal –, aber die Leseschicht liefert sie nicht: Sie haben keinen der drei Zustände unten, keine Nummer und kommen in keiner der folgenden Regeln vor. Nur die Verwaltungsansicht des Viewers führt sie, mit dem Kennwort `sectionDiscarded`.

Für alle übrigen Schritte gilt genau einer von drei Zuständen; die Leseschicht liefert ihn als `state`:

| Zustand | Kennwort | Bedingung |
|---|---|---|
| wirksam | `active` | weder ersetzt noch nicht verwendet |
| ersetzt | `replaced` | `anchorSeq` liegt im Bereich einer wirksamen Ersetzung |
| nicht verwendet | `unused` | der Schritt liegt in einem Abschnitt mit `purpose: "replacement"`, für den keine wirksame Ersetzung besteht |

Eine Ersetzung ist **wirksam**, wenn ihr Eintrag die Bedingungen des Datenformats erfüllt (Prüfung F22), ihr Bereich nicht in einer nicht verwendeten Neuaufnahme liegt und weder der Abschnitt des Bereichs noch der Ersatzabschnitt verworfen ist. Ausgewertet wird in aufsteigender Abschnittsnummer; so ist das Ergebnis auch bei Ketten eindeutig – einer Neuaufnahme, von der wieder ein Teil ersetzt wurde.

Der Zustand hat nichts mit `flags.discarded` zu tun: Ein **einzeln** verworfener Schritt („Letzten Schritt verwerfen“) ist geschehen und bleibt wirksam; er trägt nur sein Kennzeichen.

### 16.2 Reihenfolge

1. Ausgangspunkt sind die wirksamen Schritte der nicht verworfenen Abschnitte, die keine Neuaufnahme sind – der erste Abschnitt und alle Fortsetzungen –, in zeitlicher Reihenfolge.
2. Für jede wirksame Ersetzung stehen an der Stelle des **ersten ersetzten Schritts** die wirksamen Schritte des Ersatzabschnitts, in ihrer zeitlichen Reihenfolge. Enthält der Ersatzabschnitt selbst eine Ersetzung, gilt die Regel dort ebenso.
3. Liegt im Bereich einer Ersetzung kein Schritt (etwa nach einer Neuberechnung), steht der Ersatz vor dem ersten Schritt desselben Abschnitts, dessen Anker hinter dem Bereich liegt.
4. Ersetzte und nicht verwendete Schritte gehören nicht zur wirksamen Folge.

Eine vom Autor gesetzte Reihenfolge (`order` in `edits.json`) ordnet danach die Schritte der **Anleitung**; Einträge, die ersetzte oder nicht verwendete Schritte nennen, werden übersprungen. Das KI-Protokoll zeigt immer die wirksame Folge selbst.

*Umgesetzt (Leseschicht, `Effective`):* Die Reihenfolge wirkt in zwei Stufen. Für die **Anleitung** (`guide`) gilt sie zusammen mit dem Ausblenden: ausgeblendete und verworfene Schritte entfallen. Für die **Schrittliste des Editors** (`ordered`) gilt nur die Reihenfolge: Ausgeblendete Schritte behalten ihren Platz und bleiben sichtbar; Verschieben rechnet auf derselben Folge. Schritte ohne Eintrag in `order` stehen hinter den genannten, in zeitlicher Reihenfolge; eingefügte eigene Schritte stehen an der Stelle, die `order` ihnen gibt, sonst hinter ihrem Bezugsschritt. KI-Paket, MCP und Vergleich verwenden `order` nicht; der AutoHotkey-Export folgt wie die Anleitung Reihenfolge und Ausblenden, fasst aber keine Schritte zusammen (16.7).

### 16.3 Nummerierung

| Nummer | Regel |
|---|---|
| `id` (`s` + Anker) und `index` in `steps.json` | Bleiben, wie sie sind. `index` zählt weiter über alle Schritte in zeitlicher Reihenfolge. Für Maschinen ist `id` der stabile Verweis. |
| **Schrittnummer** (`number`) | Position in der wirksamen Schrittfolge, fortlaufend ab 1, gezählt über **alle** wirksamen Schritte – auch Neben- und Spurschritte, ausgeblendete und einzeln verworfene. Sie hängt nicht von Detailstufe oder Filter ab: Derselbe Schritt trägt in jeder Ausgabe dieselbe Nummer; lässt eine Ausgabe Schritte weg, fehlen dort deren Nummern. Die Leseschicht liefert sie am Schritt ([Datenformat](04-datenformat.md#schrittsicht)); KI-Paket und MCP-Server nennen diese Nummer. |
| Ersetzte und nicht verwendete Schritte | Erhalten **keine** Schrittnummer. Wo eine Ausgabe sie zeigt, stehen sie mit ihrer `id` und ihrem Zustand. |
| Nummer in der Anleitung | Eine Anleitung zeigt meist nur die Hauptschritte und zählt nur, was sie zeigt. Diese Nummer vergibt der [Anleitungs-Export](09-anleitungs-export.md) über die wirksame Folge nach seinem Filter; sie ist keine Eigenschaft des Schritts und steht in keiner Datei der Sitzung. |
| Verweise in Texten | „2,1 s nach Schritt 6“ im KI-Protokoll meint die Schrittnummer; ein Verweis in einer Anleitung meint deren eigene Nummer. |

Solange nichts ersetzt und kein Abschnitt verworfen ist, sind `number` und `index` gleich. Eine Ersetzung verschiebt die Nummern aller folgenden Schritte, wenn die Neuaufnahme mehr oder weniger Schritte hat als das Ersetzte. Kommentare des Autors, die eine Schrittnummer im Text nennen, passt das Programm nicht an; die Qualitätsprüfung kann darauf hinweisen ([18](#qualitaetskriterien)).

### 16.4 Nahtstellen

Zwischen zwei aufeinanderfolgenden Schritten der wirksamen Folge liegt eine **Nahtstelle**, wenn sie aus verschiedenen Abschnitten stammen. Das ist an beiden Rändern einer Neuaufnahme der Fall und dort, wo eine Aufnahme fortgesetzt wurde.

| Art | Kennwort | Wo |
|---|---|---|
| Beginn einer Neuaufnahme | `replacementStart` | vor dem ersten Schritt eines Ersatzabschnitts |
| Ende einer Neuaufnahme | `replacementEnd` | nach seinem letzten Schritt |
| Fortsetzung | `continuation` | zwischen zwei Abschnitten, von denen keiner den anderen ersetzt |

An einer Nahtstelle ist der Zustand der Anwendung **nicht durchgängig aufgezeichnet**: Die Neuaufnahme entstand später, und was zwischen dem Schritt davor und dem Schritt danach geschah, steht nicht im Ablauf. Deshalb gilt:

- Jede Ausgabe weist jede Nahtstelle aus – mit ihrer Art, dem Zeitpunkt der späteren Aufnahme und bei `replacementStart` der Zahl der ersetzten Schritte. Wie eine Lücke wird eine Nahtstelle nie verschwiegen.
- Über eine Nahtstelle hinweg wird kein Abstand gebildet („x s nach Schritt n“) und keine Wirkung zugeordnet.
- Die Schritte einer Neuaufnahme tragen ihre **tatsächlichen** Zeiten. Die wirksame Folge ist damit nicht durchgehend zeitlich aufsteigend; die Nahtstellen erklären jeden Sprung.

### 16.5 Was die Ausgaben zeigen

| Ausgabe | Ersetzte Schritte | Nicht verwendete Neuaufnahme | Verworfener Abschnitt |
|---|---|---|---|
| Anleitung | fehlen | fehlt | fehlt |
| KI-Paket, knappe und mittlere Detailstufe | fehlen; an der Nahtstelle steht, dass und wie viele Schritte ersetzt wurden | fehlt; der Kopf nennt ihre Zahl | fehlt |
| KI-Paket, ausführlichste Detailstufe | stehen unmittelbar vor dem Ersatz, gekennzeichnet als „ersetzt“, ohne Schrittnummer | steht am Ende, gekennzeichnet als „nicht verwendete Neuaufnahme“ | fehlt – auch hier |
| Schrittabgleich und Qualitätsprüfung | bleiben unberücksichtigt | bleibt unberücksichtigt | bleibt unberücksichtigt |
| Suche | auffindbar, als ersetzt gekennzeichnet | auffindbar, als nicht verwendet gekennzeichnet | nicht auffindbar |

Den Wortlaut legen [KI-Export](06-ki-export-und-mcp.md) und [Anleitungs-Export](09-anleitungs-export.md) fest. Schwärzungen, Durchsicht und Verallgemeinerungen gelten für jeden Schritt unabhängig von seinem Zustand. Von einem verworfenen Abschnitt darf eine Ausgabe höchstens nennen, **dass** es ihn gab – nie, was darin war.

### 16.6 Bearbeitungen und Kennzahlen

- Bearbeitungen an einem ersetzten Schritt – Texte, Kommentare, Markierungen, Darstellung – bleiben gespeichert, wirken aber nicht, solange er ersetzt ist. Wird die Ersetzung zurückgenommen, gelten sie wieder. Sie wandern nicht nach `orphans`.
- Ein Kapitel, das mit einem ersetzten Schritt beginnt, beginnt mit dem ersten Schritt des Ersatzes. Zusammenfassungen und Teilungen, die ersetzte Schritte betreffen, ruhen.
- Der Vollständigkeitsbericht ([13](#vollstaendigkeitspruefung)) bezieht sich unverändert auf das **ganze** Journal. Daneben liefert die Leseschicht **Kennzahlen der wirksamen Folge**: `steps`, `actionSteps`, `confirmed`, `inputOnly`, `unknown`, `replaced`, `unused`, `seams`, `discardedSections`. Ausgaben, die Belegqualität nennen, sagen dazu, auf welche der beiden Mengen sich die Zahl bezieht. Verworfene Abschnitte gehen in keine dieser Zahlen ein außer in die letzte; ihre Zeit zählt in keiner Zeitrechnung einer Ausgabe.

### 16.7 Zusammengefasste Schritte (nur Anleitungen)

Der Autor kann aufeinanderfolgende Schritte zu einem Schritt der **Anleitung** zusammenfassen (`edits.merges[]`, [Datenformat](04-datenformat.md#edits-json)). Die Leseschicht liefert dafür die Sicht `GuideSteps`; sie ist `Effective` mit Reihenfolge, Ausblenden und eigenen Schritten, bei der jede Gruppe als **ein** Schritt erscheint.

- **Gruppe.** Mindestens zwei aufgenommene Schritte, die in der Schrittliste aufeinander folgen; der erste Anker führt. Ein Schritt gehört höchstens einer Gruppe an. Eingefügte eigene Schritte, Lücken und Schritte, mit denen ein Kapitel beginnt (außer dem ersten der Gruppe), lassen sich nicht zusammenfassen. Unbekannte Anker (etwa nach einer Neuberechnung) werden übergangen; bleiben weniger als zwei, entfällt die Gruppe.
- **Der zusammengefasste Schritt** trägt Nummer, Art, Ziel und Kapitel des führenden Schritts. Sein Zeitraum reicht vom frühesten Beginn bis zum spätesten Ende der Mitglieder; Vorher-Bild ist das erste vorhandene Vorher-Bild, Nachher-Bild das letzte vorhandene Nachher-Bild. Die Wichtigkeit ist die höchste der Mitglieder, „ungeprüft“ gilt, sobald ein Mitglied ungeprüft ist, und die Kommentare aller Mitglieder werden vereinigt.
- **Text.** Ohne eigenen Text des Autors verbindet der Anleitungstext die Anleitungstexte der Mitglieder (der Protokolltext ebenso). Der Autor bearbeitet am führenden Schritt den Text der Gruppe; er steht je Sprache in `merges[].text` (`title`, `body`). Fehlt die Übersetzung, gilt die Regel für fehlende Übersetzungen des Exports ([09](09-anleitungs-export.md)).
- **Schwärzung und Ausblenden.** Geschwärzte Mitglieder tragen keinen Text und kein Bild bei. Ist der führende Schritt ausgeblendet, ist die Gruppe ausgeblendet; ein ausgeblendetes Mitglied trägt nichts bei.
- **Reichweite.** Zusammengefasst wird nur in Anleitungen (HTML, Markdown, Word, PDF, PowerPoint, Klick-Tour, Fehlerbericht, GIF) und in der Qualitätsprüfung, die dieselbe Sicht nutzt. `steps.json`, `Effective`, KI-Paket, MCP, Schrittabgleich und AutoHotkey-Export zeigen weiter die Einzelschritte; das KI-Paket weist im Schritt auf die Zusammenfassung hin ([06](06-ki-export-und-mcp.md)). Die Rohdaten und `steps.json` ändern sich nie.

---

<a id="schrittabgleich"></a>
## 17. Schrittabgleich zweier Sitzungen

### 17.1 Zweck und Grenze

Der Schrittabgleich stellt zwei Aufzeichnungen desselben Vorgangs nebeneinander: vor und nach einem Update, „bei mir geht es“ gegen „bei mir geht es nicht“, zwei Personen. Er ordnet die Schritte einander zu und benennt, worin sich ein Paar unterscheidet. Er urteilt nicht, welche Sitzung „richtig“ ist, und er vergleicht nur, was der Recorder beobachtet hat – eine Lücke in A lässt sich mit nichts in B vergleichen.

Verwendet wird das Ergebnis vom Viewer („Vergleichen mit …“), vom MCP-Tool `compare_sessions` und vom Befehl `compare`; deren Form legen [Viewer](08-viewer.md) und [KI-Export](06-ki-export-und-mcp.md) fest. Hier steht das Verfahren.

### 17.2 Eingang

Für jede der beiden Sitzungen – **A** und **B** – die wirksame Schrittfolge ([16](#wirksame-schrittfolge)), **so wie die Leseschicht sie liefert**: mit Schwärzungen, Stand der Durchsicht und Verallgemeinerungen der jeweiligen Sitzung. Der Abgleich öffnet keine Rohdateien. Daraus folgt:

- Geschwärzte, maskierte und ungeprüfte Inhalte sind für den Abgleich nicht vorhanden; er vergleicht dort nichts und sagt das (`notCompared`).
- Verallgemeinerte Werte vergleicht er als Platzhalter. Zwei Sitzungen mit verschiedenen Benutzernamen, in beiden durch `{Benutzername}` ersetzt, gelten an dieser Stelle als gleich.

Die Schritte teilen sich in drei Gruppen:

| Gruppe | Wer dazugehört | Rolle im Abgleich |
|---|---|---|
| **Kernschritte** | Aktionsschritte (Abschnitt 2) mit wirksamer Wichtigkeit „Haupt“, die nicht verworfen sind | Werden einander zugeordnet. |
| **Lückenschritte** | `gap`-Schritte der Stufe „Haupt“ | Werden nie zugeordnet, aber **immer** gemeldet – als „nur in A“ oder „nur in B“. Der Abgleich verschweigt keine Lücke. |
| **Begleitschritte** | Alles Übrige: Neben- und Spurschritte, `system`, `wait`, `comment`, `snapshot`, `marker`, verworfene Schritte | Stören die Zuordnung nie und machen kein Paar zu „geändert“. Sie werden je Seite mitgeführt und auf Wunsch gezeigt (17.6). |

Diese Dreiteilung ist die **Toleranz für zusätzliche Neben- und Spurschritte**: Ob in einer Sitzung zwischendurch gescrollt, gewartet oder ein Tooltip gezeigt wurde, ändert an den Paaren nichts.

### 17.3 Abgleichsschlüssel und Zielidentität

Zwei Kernschritte kommen nur dann als Paar in Frage, wenn sie **dieselbe Aktion** sind:

| Merkmal | Gleich, wenn |
|---|---|
| Aktionsart | `action.verb` stimmt überein. Fehlt `action` (zurückgehaltene oder nicht eindeutige Texteingabe), gilt die Art der Schrittart: `type` → `write`, `windowSwitch` → `hotkey`. |
| Maustaste und Umschalter | bei Mausschritten: `button` und `mods` stimmen überein. |
| Tasten | bei `hotkey` und `press`: dieselbe kanonische Kombination (`input.combo` bzw. Taste mit Umschaltern). Strg+C und Strg+V sind nie ein Paar. |
| Anwendung | `ctx.process` stimmt überein (ohne Groß- und Kleinschreibung). |

Wie gut zwei gleiche Aktionen zusammenpassen, bestimmt die **Zielidentität**. Namen werden dafür bereinigt wie in 12.2; verglichen wird Zeichen für Zeichen, ohne sprachabhängige Regeln.

| Stufe | Bedingung | Wert |
|---|---|---|
| Z5 – gleiche Kennung | Beide Ziele tragen eine nicht leere `aid` (AutomationId), sie ist gleich, und der Typ `ct` ist gleich. | 10 |
| Z4 – gleicher Name | Gleicher bereinigter Name (`name`, ersatzweise `labeledBy.name`) und gleicher `ct`. | 9 |
| Z3 – gleiche Position | Beide Ziele sind nur als Position bekannt (`target.status` `position` oder `none`), die Fensterart `ctx.kind` ist gleich, und die Position im Fenster stimmt bis auf `comparePosTolerance` überein. | 7 |
| Z2 – gleiche Stelle im Aufbau | Gleicher `ct` und gleicher Pfad (Folge aus Typ und Name der Vorfahren in `target.path`), aber anderer oder fehlender Name. | 6 |
| Z1 – Position trifft Element | Nur eine Seite kennt ein Element; die Position der anderen liegt – auf das Fenster bezogen – in dessen Rechteck, und die Fensterart ist gleich. | 5 |
| Z0 – verschieden | sonst | 0 |

Positionen werden auf das Fenster bezogen verglichen (`position.inWindow`, Fenstergröße aus dem Fensterstand, der für den Schritt galt); so stört es nicht, wenn das Fenster in der zweiten Sitzung woanders lag.

Für Tastaturschritte gilt das fokussierte Element als Ziel (6.4). Bei `hotkey` und `press` genügen gleiche Tasten in derselben Anwendung für den Wert 10, wenn die Ziele nach Z4 oder Z5 gleich oder auf beiden Seiten unbekannt sind; sonst gilt 8. Der Fenstertitel geht nie in die Identität ein – er wechselt mit dem Namen des Dokuments.

Ein Paar ist nur zulässig, wenn der Wert mindestens `compareMinScore` erreicht.

### 17.4 Verfahren

Die Kernschritte beider Sitzungen werden **reihenfolgeerhaltend** zugeordnet: Sind (a₁, b₁) und (a₂, b₂) Paare und steht a₁ vor a₂, dann steht b₁ vor b₂. Unter allen solchen Zuordnungen gilt die mit der höchsten Summe der Werte. Berechnet wird sie mit der üblichen Tabelle über die Folgen A = a₁ … aₙ und B = b₁ … bₘ:

```
D[0][j] = D[i][0] = 0
D[i][j] = max( D[i−1][j−1] + w(aᵢ, bⱼ)   falls w(aᵢ, bⱼ) ≥ compareMinScore,
               D[i−1][j],
               D[i][j−1] )
```

Die Zuordnung wird vom Ende her abgelesen. Sind mehrere Wege gleich gut, gilt diese feste Rangfolge: zuerst das Paar (aᵢ, bⱼ), dann „aᵢ nur in A“, dann „bⱼ nur in B“. Alle Werte sind ganze Zahlen; Rundung kommt nicht vor. Jede Umsetzung – auch eine speichersparende – muss dasselbe Ergebnis liefern wie diese Vorschrift.

Die Ausgabe folgt der gemeinsamen Reihenfolge: Paare in ihrer Folge; unzugeordnete Schritte stehen dort, wo sie in ihrer Sitzung liegen, an gleicher Stelle erst die aus A, dann die aus B. Lückenschritte und – auf Wunsch – Begleitschritte werden nach derselben Regel eingereiht.

### 17.5 Kennzeichnung

| Kennzeichnung | Kennwort | Bedeutung |
|---|---|---|
| gleich | `same` | Paar ohne festgestellten Unterschied. |
| geändert | `changed` | Paar mit mindestens einem Unterschied; `differences` nennt sie. |
| nur in A | `onlyA` | Schritt aus A ohne Gegenstück. |
| nur in B | `onlyB` | Schritt aus B ohne Gegenstück. |

Unterschiede eines Paars:

| Unterschied | Kennwort | Festgestellt, wenn | Toleranz |
|---|---|---|---|
| Ziel | `target` | Die Zielidentität ist Z2 (anderer Name an gleicher Stelle) oder Z1 (nur eine Seite kennt das Element). | Z3 bis Z5 gelten als gleiches Ziel, auch wenn sich Rechteck oder Fenstertitel unterscheiden. |
| Text | `text` | Bei Texteingaben: `input.text` unterscheidet sich, oder nur eine Seite ist maskiert. | Verschiedene Tippwege zum selben Text (Korrekturen) sind kein Unterschied. |
| Wirkung | `effect` | Die Wirkungen unterscheiden sich in: geöffneten und geschlossenen Fenstern (Art, Anwendung, bereinigter Titel), Ziel eines Vordergrund- oder Fokuswechsels, geänderten Werten (Element und Wert danach), Zuständen, Auswahlen, ausgelösten Elementen, Mitteilungstexten, Vorgang und Art eines Zwischenablage-Inhalts – oder nur eine Seite hat überhaupt eine beobachtete Wirkung. | Der Anteil geänderter Bildfläche und Zeigerformen zählen nicht. |
| Dauer | `duration` | Die Reaktionszeit – `effect.screen.settleUs`, ersatzweise `effect.latencyUs` – unterscheidet sich um mehr als `compareDurationAbsMs` **und** um mehr als den Faktor `compareDurationFactor`. | Die Denkzeit des Nutzers vor dem Schritt zählt nie. |
| Bild | `image` | Nur wenn der Bildvergleich eingeschaltet ist: Der Ausschnitt des Ziels unterscheidet sich in mehr als `compareImageDiffRatio` der Bildpunkte. | Verglichen wird der Bereich `target.el.rect` – bei Mausschritten im Vorher-Bild, bei Tastaturschritten im Nachher-Bild –, wie die Leseschicht ihn liefert. Ein Bildpunkt gilt als verschieden, wenn ein Farbkanal um mehr als `compareImagePixelDelta` abweicht. |

Lässt sich ein Merkmal nicht vergleichen, wird **kein** Unterschied behauptet und **keine** Gleichheit: Das Paar trägt das Merkmal in `notCompared`. Das gilt für Text, wenn eine Seite geschwärzt oder ungeprüft ist; für die Dauer, wenn eine Seite keine Reaktionszeit hat; für das Bild, wenn ein Bild fehlt, die Rechtecke verschieden groß sind oder der Bildvergleich ausgeschaltet ist. Ein Paar ist `same`, wenn `differences` leer ist – `notCompared` darf dabei gefüllt sein und wird mit ausgegeben.

### 17.6 Ergebnis

```json
{
  "compareVersion": "1.0.0",
  "a": { "sessionId": "019328a4-6b1e-7c3a-9d52-0f4e8a1b77c0", "coreSteps": 7 },
  "b": { "sessionId": "01933b10-88f2-7a61-b3c4-2e7d905a1f66", "coreSteps": 8 },
  "params": { "compareMinScore": 5, "comparePosTolerance": 0.02, "compareDurationAbsMs": 2000, "compareDurationFactor": 2.0,
    "compareImages": false },
  "summary": { "same": 5, "changed": 2, "onlyA": 0, "onlyB": 1, "gapsOnlyA": 0, "gapsOnlyB": 0, "minorOnlyA": 1, "minorOnlyB": 1 },
  "pairs": [
    { "status": "same", "a": "s201", "b": "s188", "score": 10, "notCompared": ["image"] },
    { "status": "changed", "a": "s1700", "b": "s1502", "score": 10, "differences": ["text"], "notCompared": ["image"] },
    { "status": "changed", "a": "s1734", "b": "s1544", "score": 10, "differences": ["effect", "duration"], "notCompared": ["image"] },
    { "status": "onlyB", "b": "s1571", "group": "core" },
    { "status": "onlyB", "b": "s1560", "group": "minor", "kind": "system" }
  ]
}
```

(Auszug: fünf der zehn Einträge. Sieben Paare – fünf gleich, zwei geändert –, ein Kernschritt nur in B, dazu ein Begleitschritt nur in A und einer nur in B, die der Aufrufer verlangt hat.)

| Feld | Bedeutung |
|---|---|
| `compareVersion` | Version des Verfahrens (17.8). |
| `a`, `b` | Die beiden Sitzungen mit der Zahl ihrer Kernschritte. |
| `params` | Die verwendeten Schwellwerte. |
| `summary` | Zählung: Paare `same` und `changed`; Kernschritte `onlyA`, `onlyB`; Lückenschritte und Begleitschritte je Seite getrennt. |
| `pairs[]` | Die Einträge in gemeinsamer Reihenfolge: `status`, `a` und/oder `b` (Schritt-Kennungen), bei Paaren `score`, `differences`, `notCompared`; bei unzugeordneten Schritten `group` (`core` \| `gap` \| `minor`) und `kind`. |

Begleitschritte (`group: "minor"`) stehen nur im Ergebnis, wenn der Aufrufer sie verlangt. Zwei Begleitschritte gelten dabei als gleich und entfallen, wenn sie zwischen denselben Paaren liegen und in Schrittart, Ereignisart und bereinigtem Fenstertitel übereinstimmen; alle anderen erscheinen als `onlyA` oder `onlyB`. So bleibt ein Fehlerdialog, der nur in B erscheint, sichtbar, ohne dass er die Paare verändert.

### 17.7 Parameter

| Parameter | Standard | Bedeutung |
|---|---|---|
| `compareMinScore` | 5 | Mindestwert der Zielidentität für ein Paar. |
| `comparePosTolerance` | 0,02 | Zulässige Abweichung der Position im Fenster, als Anteil von Fensterbreite und -höhe. |
| `compareDurationAbsMs` | 2000 | Mindestunterschied der Reaktionszeit in Millisekunden. |
| `compareDurationFactor` | 2,0 | Mindestverhältnis der Reaktionszeiten. |
| `compareImages` | aus | Bildvergleich einschalten. |
| `compareImageDiffRatio` | 0,02 | Anteil verschiedener Bildpunkte, ab dem ein Bildunterschied gilt. |
| `compareImagePixelDelta` | 16 | Abweichung je Farbkanal (0–255), ab der ein Bildpunkt als verschieden gilt. |
| `compareMaxSteps` | 5000 | Höchstzahl Kernschritte je Sitzung. Darüber wird nicht abgeglichen, sondern das gemeldet. |

Diese Parameter gehören nicht zu `steps.json` und nicht zur Regelversion der Ableitung.

### 17.8 Determinismus

- Das Ergebnis ist eine **reine Funktion** der beiden wirksamen Schrittfolgen in der Sicht der Leseschicht, der Parameter und von `compareVersion`. Es hängt nicht von Uhr, Sprache, Rechner oder Reihenfolge früherer Aufrufe ab.
- Gleiche Eingaben ergeben dasselbe Ergebnis, Feld für Feld – auch auf einem anderen Rechner.
- Textvergleiche sind ordinal nach Normalisierung (Unicode NFC, Leerraum zusammengefasst, Ränder entfernt); Programmnamen werden ohne Groß- und Kleinschreibung verglichen, sprachunabhängig.
- Werden A und B vertauscht, entstehen dieselben Paare mit denselben Unterschieden; `onlyA` und `onlyB` tauschen die Seite. Nur die Auflösung gleich guter Wege (17.4) kann sich dabei unterscheiden – sie ist für jede der beiden Aufrufrichtungen fest.
- `compareVersion` (`MAJOR.MINOR.PATCH`) steigt in MINOR, wenn sich Ergebnisse ändern können. Das Ergebnis wird nicht in der Sitzung gespeichert; es entsteht bei jedem Aufruf neu.

### 17.9 Leistungsziel

| Größe | Ziel (Referenzsystem A) |
|---|---|
| Abgleich zweier Sitzungen mit je 1000 Kernschritten, ohne Bildvergleich | höchstens 300 ms, höchstens 50 MB zusätzlicher Arbeitsspeicher |
| Dasselbe mit je 5000 Kernschritten | höchstens 5 s |
| Bildvergleich | zusätzlich höchstens 10 ms je Paar; der Viewer zeigt das Ergebnis ohne Bilder zuerst und trägt Bildunterschiede nach |

Die Laufzeit wächst mit dem Produkt der Schrittzahlen. Über `compareMaxSteps` hinaus wird nicht abgeglichen.

---

<a id="qualitaetskriterien"></a>
## 18. Kriterien für die Qualitätsprüfung

Die Qualitätsprüfung vor dem Export meldet Stellen, an denen eine Anleitung voraussichtlich schwer zu verstehen oder unvollständig ist. Den Regelkatalog – Nummern `Q01` ff., Schwere, Wortlaut – führt der [Anleitungs-Export](09-anleitungs-export.md). Hier steht, **welche Kriterien sich aus den Schrittdaten ablesen lassen und wie**. Jede Regel des Katalogs stützt sich auf eines oder mehrere davon.

Alle Kriterien gelten für die **wirksame Schrittfolge** in der Sicht der Leseschicht. Sie sind reine Funktionen dieser Daten; die Prüfung ändert nichts an der Sitzung.

| Kriterium | Aussage | Gelesen aus | Erfüllt, wenn | `key` der Fundstelle |
|---|---|---|---|---|
| `targetUnnamed` | Das Ziel hat keinen Namen. | `target.status`, `target.el`, Rückfallkette 12.2 | Mausschritt oder Texteingabe der Stufe „Haupt“, dessen Zielbezeichnung erst über Regel 6 (nur der Ort) oder Regel 7 (nur die Position) zustande kommt. | – |
| `evidenceInputOnly` | Belegstufe „nur Eingabe“. | `evidence.level`, `evidence.reasons` | Aktionsschritt der Stufe „Haupt“ mit `inputOnly`. | – |
| `evidenceUnknown` | Belegstufe „unbekannt“. | `evidence.level`, `evidence.input`, `evidence.cause` | Schritt der Stufe „Haupt“ oder „Neben“ mit `unknown`. Der erste Begründungscode unterscheidet maskierte Eingabe, Lücke und unbeobachtete Ursache. | – |
| `gapInFlow` | Lücke im Hauptablauf. | `gap`-Schritte, `gaps[]` | `gap`-Schritt der Stufe „Haupt“ (möglicher Eingabeverlust) oder Lücke `removedByAuthor`. | Ursache |
| `seam` | Nahtstelle: Der Zustand ist nicht durchgängig aufgezeichnet. | 16.4 | Vor dem Schritt liegt eine Nahtstelle. | Art der Nahtstelle |
| `noEffect` | Keine Wirkung beobachtet. | `effect.observed` | Klick, Doppelklick, Rechtsklick oder Tastenkombination der Stufe „Haupt“ mit `effect.observed: false`. | – |
| `longStep` | Sehr langer Schritt. | `tFrom`, `tTo`, `approach.durationUs` | Dauer der Eingabe – `tTo − tFrom` ohne den Anlaufweg – über `qualityLongStepMs`. | – |
| `longIdle` | Langer Leerlauf vor dem Schritt. | `timeline` | Der Leerlaufabschnitt unmittelbar vor dem Schritt dauert länger als `qualityLongIdleMs`. | – |
| `frameMissing` | Ein Bild fehlt oder ist nicht verlässlich. | `frames.pre`, `frames.post`, `frames.issues` | `preMissing`, `postMissing`, `preNotGuaranteed` oder `postTimeout` an einem Schritt der Stufe „Haupt“; `masked`, wenn die gefüllte Fläche das Ziel trifft. | der Eintrag aus `issues` |
| `targetCutOff` | Der gezeigte Ausschnitt schneidet das Ziel ab. | `target.el.rect`, wirksame Darstellung (`view` aus `edits.json`) | Das Zielrechteck liegt nicht vollständig im Ausschnitt, den die Ausgabe zeigt. | Bild (`pre` \| `post`) |
| `clickOutsideView` | Die Klickmarke liegt außerhalb des Ausschnitts. | `position`, wirksame Darstellung | Der Klickpunkt liegt nicht im gezeigten Ausschnitt. | Bild |
| `viewSize` | Das Bild ist zu klein oder zu groß. | wirksame Darstellung, Bildgröße (`frame.w`, `frame.h`), `target.el.rect` | Der gezeigte Ausschnitt unterschreitet oder überschreitet die Grenzen des Katalogs – oder das Ziel nimmt darin weniger als den dort genannten Anteil ein. | `tooSmall` \| `tooLarge` |
| `textEmpty` | Leerer Schritttext. | `steps[].text` in `edits.json`, Vorlage | Kurztitel oder Text des Schritts ist in der Ausgabesprache leer oder besteht nur aus Leerraum. | Sprache |
| `textDuplicate` | Doppelter Schritttext. | Texte zweier aufeinanderfolgender Hauptschritte | Beide haben in der Ausgabesprache denselben Text (`body`) – nach Vorlage oder Overlay. | Sprache |
| `translationMissing` | Eigener Text nicht übersetzt. | `steps[].text` in `edits.json` | Der Schritt hat einen eigenen Text in einer anderen Sprache, aber keinen in der Ausgabesprache; dort stünde die Vorlage. | Sprache |
| `stepNumberInComment` | Ein Kommentar nennt eine Schrittnummer, die sich verschoben haben kann. | Kommentare des Autors, 16.3 | Der Kommentartext enthält „Schritt“ bzw. “step” mit einer Zahl, und die Sitzung hat mindestens eine Ersetzung oder ausgeblendete Schritte. | – |
| `unreviewedInput` | Ungeprüfte auffällige Eingabe. | Stand der Durchsicht ([Datenformat](04-datenformat.md#edits-review)) | Der Schritt enthält eine auffällige Eingabe ohne Entscheidung. | Art der Eingabe |
| `personalValue` | Persönlicher Wert nicht verallgemeinert. | Texte des Schritts nach der Leseschicht; Vorschlagswerte des Programms | Ein Vorschlagswert – Benutzername, Rechnername, Domäne, Profilpfad des laufenden Systems – kommt im Klartext vor, und keine Verallgemeinerung greift. | Art des Vorschlags (`userName`, `machineName`, `domain`, `profilePath`) |
| `substitutionImagesStale` | Bildflächen einer Verallgemeinerung sind unvollständig. | `substitutions[].images` | `unresolved` ist größer als 0, oder es gibt Bilder nach `throughSeq`. | Kennung der Regel |

Erläuterungen:

- **Wirksame Darstellung** ist der Ausschnitt, den die Ausgabe für den Schritt tatsächlich zeigt: `view` des Schritts, sonst die Vorgabe der Sitzung, sonst der Standard des Exports. Bei `mode: "window"` ist es das Fensterrechteck, bei `focus` der vom Export gewählte Ausschnitt um das Ziel, bei `custom` das gespeicherte Rechteck. Die Größen- und Anteilsgrenzen legt der Katalog fest.
- **Schwellwerte:** `qualityLongStepMs` (Vorgabe 30 000) und `qualityLongIdleMs` (Vorgabe 60 000). Der Katalog darf sie je Regel anders setzen.
- **`personalValue`** verwendet Werte des Rechners, auf dem geprüft wird. Sie werden nie gespeichert; in `qualityIgnores` steht nur die **Art** des Vorschlags, nie der Wert.
- **`unreviewedInput`** ist ein Hinweis auf die Sperre aus dem Datenschutz, nicht ihr Ersatz: Ignorieren hebt die Sperre nicht auf.
- Eine Fundstelle ist durch Regel, Anker des Schritts und `key` bestimmt; so wird sie in `edits.qualityIgnores[]` abgelegt ([Datenformat](04-datenformat.md#edits-quality)). Der Wertebereich von `key` steht in der letzten Spalte; er enthält nie aufgezeichneten Inhalt.
- Neue Kriterien dürfen hinzukommen. Ein Kriterium, das eine Regel des Katalogs nicht kennt, ist kein Fehler.

---

<a id="review-abdeckung"></a>
## 19. Abdeckung der Review-Befunde

| Befund | Lösung in der Schritterkennung | Abschnitt |
|---|---|---|
| Nr. 5 – Vollständigkeitsprüfung beweist nur Buchführung | Getrennte Aussagen zu Buchführung und Belegqualität; Belegstufen mit festen Kriterien; „unbekannt“ zählt als Lücke; Wertänderung ohne Eingabe und Wirkung ohne Ursache werden eigene Schritte statt einem Nachbarn zugeschlagen; Zwischenablage-Herkunft an jedem Schritt | [9](#belegstufen), [13](#vollstaendigkeitspruefung), [5.4](#schrittarten), [7.4](#wirkungserkennung) |
| Nr. 6 – Elementidentität versagt in den interessanten Fällen | Zwei Ermittlungen plus Reaktion als unabhängige Belege; Brauchbarkeitsprüfung erkennt veraltete Vorab-Ergebnisse (Scrollen, Popup unter dem Zeiger); Entscheidungstabelle mit `conflict` statt stiller Wahl | [6](#zielbestimmung) |
| Nr. 7 – Das Modell weiß nicht, was auf dem Bildschirm stand | Wirkung nennt geöffnete Fenster mit ihrem Text-Schnappschuss und den Fokus danach; Zielbezeichnung unterscheidet gleichnamige Elemente über Zeile oder Ordnungszahl | [7.2](#wirkungserkennung), [12.2](#textvorlagen) |
| Nr. 10 – Bildregeln versagen unter Last | Nachher-Bild ist spätestens das Vorher-Bild des nächsten Schritts; fehlende Bilder werden als `issues` ausgewiesen, nie stillschweigend ersetzt | [8](#bildzuordnung) |
| Nr. 11 – Zeitsemantik | Zuordnung allein über `t`; unsichere Zeiten senken `causality`; Prüfung mit verspätet geschriebenen Fassungen | [4.3](#spaete-daten), [15.2](#regelversion) |
| Nr. 13 – Undurchsichtige Oberflächen | Erkennung über vier Merkmale; Ziel nur als Position; Belegstufe höchstens „nur Eingabe“; Textläufe enden an jeder Eingabetaste; Weg einer Ziehbewegung als Primärinformation | [5.15](#schrittarten), [5.2](#ziehen) |
| Nr. 14 – Verifikation deckt nur den einfachen Weg ab | Goldene Journale mit bewusst schwierigen Fällen; Ergebnisgleichheit von mitlaufender und einmaliger Ableitung; Prüfungen S01–S15 | [15.2](#regelversion), [14](#mitlaufend), [13.5](#vollstaendigkeitspruefung) |
| Fehlend – eigene Bedienung des Recorders | Ereignisse mit `self` werden nie Schritte; Recorder-Bedienung erscheint nur als Lücke `recorderInteraction` in der Zeitleiste | [4.3](#spaete-daten), [5.12](#schrittarten) |
| Fehlend – Eingabe-Einstellungen | Doppelklickzeit, Ziehschwelle, Einrastfunktion und vertauschte Tasten kommen aus der Sitzung, nicht vom ableitenden Rechner | [3](#parameter), [5.14](#sonderfaelle) |

---

<a id="anforderungen"></a>
## 20. Anforderungen

| ID | Anforderung | Priorität | Abnahmekriterium |
|---|---|---|---|
| SCH-001 | Die Ableitung ist eine reine Funktion von Journal, Regelversion und Parametern. | Muss | Ableitung derselben Sitzung auf zwei verschiedenen Rechnern (andere Doppelklickzeit, anderes Layout, andere Sprache) ergibt bytegleiche Schritte. |
| SCH-002 | Die Ableitung ist wiederholbar. | Muss | Zehn Ableitungen derselben Sitzung sind bis auf `derivedAtUtc` bytegleich. |
| SCH-003 | Die Ableitung liest nur das Journal, keine Bilddaten. | Muss | Ableitung gelingt mit leerem `frames/`-Ordner und ergibt dieselben Schritte. |
| SCH-004 | Zugeordnet wird allein über `t`. | Muss | Goldene Journale mit verspätet geschriebenen Anreicherungen ergeben dieselben Schritte wie die unverzögerten Fassungen. |
| SCH-005 | Jedes Eingabeereignis gehört zu genau einem Schritt. | Muss | Für alle goldenen Journale und einen einstündigen Lasttest gilt `orphans` = 0 und `duplicates` = 0. |
| SCH-006 | Klick und Mehrfachklick folgen den Einstellungen der Sitzung (Doppelklickzeit und -rechteck). | Muss | Dieselbe Klickfolge mit 200, 500 und 900 ms Doppelklickzeit in der Sitzung ergibt jeweils die erwartete Einteilung. |
| SCH-007 | Druck und Ziehen werden über die Ziehschwelle der Sitzung unterschieden; ein Ziehen trägt Start, Ziel, Weg und Art. | Muss | Goldene Journale für Fenster verschieben, Text markieren, Ziehen-und-Ablegen, Schieberegler und Freihandzeichnen ergeben die erwartete `dragKind` und einen Weg innerhalb der Toleranz. |
| SCH-008 | Radereignisse werden nach Achse, Richtung, Fenster und Zeitabstand zusammengefasst; Strg+Rad gilt als Zoom. | Muss | 20 Rasten in vier Sekunden mit einer Richtungsumkehr ergeben zwei Schritte mit richtigen Summen. |
| SCH-009 | Texteingaben werden zu Läufen je Feld zusammengefasst und an Fokuswechsel, Mausschritt, Tastenkombination sowie Abschlusstasten getrennt. | Muss | Goldenes Journal „Formular mit fünf Feldern und Tab“ ergibt fünf Texteingaben mit `terminator: Tab`. |
| SCH-010 | Eingabe- und Tab-Taste werden nach den Regeln aus 5.4 als Inhalt oder Abschluss gedeutet; in undurchsichtigen Kontexten immer als Abschluss. | Muss | Mehrzeiliger Text im Editor ergibt einen Schritt; drei Befehle im Terminal ergeben drei Schritte. |
| SCH-011 | Getippter Text wird mit dem Feldinhalt vorher und nachher abgeglichen. | Muss | Tippfehler mit Korrektur, überschriebene Markierung und Autovervollständigung werden jeweils richtig als `valueConfirmed` oder `valueOnly` eingestuft. |
| SCH-012 | Zurückgehaltene Tasten werden eigene Texteingabe-Schritte ohne Inhalt; ihre Anzahl wird unverändert aus `text.masked` übernommen und fehlt, wenn sie dort fehlt. | Muss | Ein Passwortfeld-Lauf ergibt genau einen Schritt mit `masked: true`, `action: null`, Belegstufe „unbekannt“ und `tokens: [{ "masked": n }]`; derselbe Lauf mit abgeschalteter Anzahl ergibt `{ "masked": null }` ohne `input.count`. |
| SCH-013 | Tastenkombinationen, Einzeltasten und Zeichen über AltGr, Tottasten und Alt+Ziffernblock werden richtig unterschieden. | Muss | Goldene Journale für `@` über AltGr, `ê` über Tottaste, `©` über Alt+0169 ergeben Text, nicht Tastenkombinationen. |
| SCH-014 | Wiederholte gleiche Tasten und Kombinationen verschmelzen zu einem Schritt mit Anzahl. | Muss | Fünfmal „Pfeil nach unten“ ergibt einen Schritt mit `count: 5`. |
| SCH-015 | Eingabemethode, Einrastfunktion und Anschlagverzögerung werden berücksichtigt und gekennzeichnet. | Soll | Goldene Journale für IME und Einrastfunktion ergeben die erwarteten Tokens und Belegstufen. |
| SCH-016 | Verweilen wird nur mit beobachteter Wirkung ein Schritt. | Soll | Verweilen über einem Menüpunkt mit aufklappendem Untermenü ergibt einen `hover`-Schritt; Verweilen ohne Wirkung keinen. |
| SCH-017 | Alt+Tab wird ein Fensterwechsel-Schritt mit Ausgangs- und Zielfenster. | Muss | Alt halten und dreimal Tab ergibt einen Schritt mit `count: 3` und dem richtigen Zielfenster. |
| SCH-018 | Geschehen außerhalb aller Wirkfenster wird ein eigener Systemschritt; Wirkung ohne beobachtete Ursache wird als solche ausgewiesen. | Muss | Ein fünf Sekunden nach der letzten Eingabe erscheinender Dialog ergibt einen `system`-Schritt; eine Feldänderung ohne Eingabe erhält `cause: "unobserved"`. |
| SCH-019 | Wartezeiten mit Geschehen werden als Warte-Schritt ausgewiesen – außer der Zeitraum enthält eine Lücke, die Tastatur oder Maus betrifft. | Soll | Ladevorgang von acht Sekunden mit Wartezeiger ergibt einen `wait`-Schritt mit `until`; derselbe Zeitraum mit einer UAC-Lücke darin ergibt keinen. |
| SCH-020 | Kommentare, Schnappschüsse und Marker des Autors (`note`, `error`, `expected`, `important`) werden Schritte; „letzten Schritt verwerfen“ kennzeichnet, löscht aber nicht; der Marker zu „Letzte Eingabe schwärzen“ wird kein Schritt. | Muss | Nach `discardPrevious` trägt der vorige Hauptschritt `flags.discarded`; seine Rohereignisse sind unverändert zugeordnet. Ein `marker` mit `kind: "redactLastInput"` ergibt keinen Schritt, und B5 besteht. |
| SCH-021 | Lücken werden nach den Regeln aus 5.12 Schritte; offene Eingaben an einer Lücke werden als unvollständig abgeschlossen. | Muss | Anhang B.2 des Datenformats ergibt die dort genannten Schritte. |
| SCH-022 | Der Mausweg vor einer Aktion wird ihr vollständig zugeordnet; übrige Bewegung wird ein eigener Schritt der Stufe „Spur“. | Muss | Summe der Messwerte aller `approach`- und `move`-Bereiche = Zahl der Messwerte im Journal abzüglich der in Drücken gehaltenen. |
| SCH-023 | Auf undurchsichtigen Oberflächen wird kein Elementname ausgegeben; das Ziel ist die Position. | Muss | Klick in ein Remote-Desktop-Fenster ergibt `target.status: "position"`, keine `target.el`, Belegstufe „nur Eingabe“. |
| SCH-024 | Das Ziel wird nach der Entscheidungstabelle aus 6.3 bestimmt; veraltete Vorab-Ergebnisse zählen nicht. | Muss | Goldene Journale für alle sieben Fälle der Tabelle sowie „gescrollt, dann geklickt“ ergeben den erwarteten Status. |
| SCH-025 | Jede Wirkung wird höchstens einem Schritt zugeordnet; die Sicherheit der Zuordnung ist ausgewiesen. | Muss | In keinem goldenen Journal erscheint ein Folgeereignis in zwei Schritten; schnelle Klickfolgen ergeben `causality: "plausible"`. |
| SCH-026 | Jeder Aktionsschritt erhält Vorher- und Nachher-Bild nach 8; das Nachher-Bild ist spätestens das Vorher-Bild des nächsten Schritts; Fehlendes steht in `issues`. | Muss | Sitzung mit laufendem Video (Bildschirm kommt nie zur Ruhe): Jeder Schritt hat ein Nachher-Bild, gekennzeichnet mit `postTimeout` oder aus dem Folgeschritt. |
| SCH-027 | Die Belegstufe folgt den Kriterien aus Abschnitt 9 und ist nie höher, als die Daten tragen. | Muss | Prüfung S09 besteht für alle goldenen Journale; auf undurchsichtiger Oberfläche ist kein Schritt „belegt“ außer Tastenkombinationen und Einzeltasten; eine Texteingabe im Terminal ist nie „belegt“. |
| SCH-028 | Jeder Schritt der Stufe „unbekannt“ erscheint in der Lückenliste – ohne Ausnahme; jedes `text.masked` ergibt einen Eintrag `maskedInput`, auch innerhalb einer Lücke `privacyMode`. | Muss | Prüfung S05 besteht. Anhang B.1 des Datenformats ergibt in `gaps` je einen Eintrag `privacyMode` und `maskedInput`. |
| SCH-029 | Schritte werden in Haupt, Neben und Spur eingestuft; kein Schritt wird entfernt. | Muss | Die Zahl der Schritte in `steps.json` ändert sich nicht durch Filtereinstellungen des Viewers. |
| SCH-030 | Jeder Aktionsschritt trägt das neutrale Aktionsvokabular; unbekannter oder zurückgehaltener Text führt zu `action: null`. | Muss | Für alle goldenen Journale entspricht `action` der Tabelle in 11. |
| SCH-031 | Textvorlagen liegen für Deutsch und Englisch in den Stilen Protokoll und Anleitung vor und liefern je Sprache **Kurztitel** und **Text**. | Muss | Für jede Schrittart – auch `marker` und `move` – und jeden Fall der Tabellen 12.5 und 12.6, einschließlich der maskierten Eingabe ohne gespeicherte Anzahl, erzeugen beide Sprachen einen Kurztitel von höchstens 60 Zeichen und einen grammatisch richtigen Satz (Prüfung durch Durchsicht einer vollständigen Beispielliste). Kein deutscher Text nennt die Eingabetaste „Eingabe“ oder schreibt „Taste Eingabetaste“. |
| SCH-032 | Für den Anleitungsstil gibt es im Deutschen die Anreden Sie, du und neutral – für **jede** Vorlage, die eine Aufforderung enthält. | Soll | Umschalten der Anrede ändert alle automatischen Texte einer Beispielanleitung, die jeden Fall der Tabelle „Anredeformen“ enthält; kein Satz bleibt in der alten Form. |
| SCH-033 | Die Zielbezeichnung folgt der Rückfallkette; es werden keine Namen erfunden, gleichnamige Ziele werden unterschieden. | Muss | Tabelle mit 20 gleichnamigen Schaltflächen: Jeder Schritt nennt Zeile oder Ordnungszahl. Element ohne jeden Namen: Der Satz nennt die Position. |
| SCH-034 | Vom Nutzer oder Agenten gesetzte Texte haben Vorrang vor Vorlagen und werden nicht übersetzt. | Muss | Ein deutscher Nutzertext erscheint im deutschen Export unverändert; der englische Export zeigt die englische Vorlage. |
| SCH-035 | Der Vollständigkeitsbericht trennt Buchführung und Belegqualität. | Muss | Sitzung mit UAC-Lücke und einem Remote-Desktop-Klick: `bookkeeping.ok` ist `true`, `gaps.count` ≥ 1, `confirmedRatio` < 1. |
| SCH-036 | `validate` führt die Prüfungen S01–S15 aus und unterscheidet Fehler, Warnung, Hinweis. | Muss | Für jede Fehlerprüfung existiert eine Testsitzung, die sie verletzt und gemeldet wird; eine Sitzung mit Lücken endet ohne Fehler. |
| SCH-037 | Die mitlaufende Erkennung ergibt beim Stopp dieselbe `steps.json` wie eine Ableitung in einem Durchgang. | Muss | Für alle goldenen Journale, als Strom eingespielt, sind beide Ergebnisse bytegleich. |
| SCH-038 | Nach dem Stopp liegt `steps.json` für 1000 Schritte innerhalb von 300 ms vor. | Muss | Messung an einer aufgezeichneten Sitzung mit 1000 Schritten auf dem Referenzrechner. |
| SCH-039 | Die mitlaufende Erkennung bremst den Recorder nicht und überspringt bei Überlast nur Rückmeldungen. | Muss | Lasttest: Die Dauer der Hook-Aufrufe unterscheidet sich mit und ohne mitlaufende Erkennung nicht messbar. |
| SCH-040 | Regelversion und Parameter stehen in `steps.json`; neu berechnet wird nur auf ausdrücklichen Wunsch. | Muss | Öffnen einer Sitzung mit neuerer Regelversion lässt `steps.json` unverändert. |
| SCH-041 | Jede Regelversion wird gegen die Sammlung goldener Journale geprüft; Abweichungen sind begründet. | Muss | Die Sammlung enthält alle in 15.2 genannten Fälle; die Prüfung ist Teil des automatischen Builds. |
| SCH-042 | Bearbeitungen überstehen eine Neuberechnung über ihre Anker. | Muss | Nach Neuberechnung mit geänderten Regeln sind alle Einträge aus `edits.json` zugeordnet oder unter `orphans`. |
| SCH-043 | Künstliche und ergänzte Eingaben sind am Schritt gekennzeichnet. | Muss | Eingaben eines Skripts ergeben Schritte mit `flags.injected`; aus dem zweiten Kanal ergänzte Tasten ergeben `flags.recovered` und höchstens „nur Eingabe“. |
| SCH-044 | Kopieren, Ausschneiden und Einfügen tragen die Kennung des Zwischenablage-Inhalts. | Muss | Anhang B.3 des Datenformats ergibt die dort genannten Angaben. |
| SCH-045 | Fehlerhafte Journale führen zu Warnungen, nicht zum Abbruch. | Muss | Journal mit unpaarigen Tasten, fehlendem `mouse.up` und abgeschnittenem Ende ergibt Schritte und Einträge in `warnings`. |
| SCH-046 | Die Ableitung verarbeitet eine einstündige Sitzung in höchstens fünf Sekunden. | Soll | Messung an einer aufgezeichneten Sitzung mit einer Stunde Dauer auf dem Referenzrechner. |
| SCH-047 | Ein Ziel gilt auch dann als belegt, wenn eine Ermittlung und eine beobachtete Reaktion desselben Elements übereinstimmen (`corroborated`). | Muss | Goldenes Journal „schneller Klick ohne Vorab-Ermittlung, Element meldet Auslösung“: Belegstufe „belegt“; dasselbe ohne Reaktion: „nur Eingabe“. |
| SCH-048 | Tastenkombinationen und Einzeltasten sind belegt, sobald das Zielfenster zum Hook-Zeitpunkt bekannt ist – unabhängig von einer beobachteten Wirkung. | Muss | Strg+S ohne jede beobachtete Wirkung in einem bekannten Fenster: „belegt“ mit `effect.observed: false`; derselbe Tastendruck ohne Fenster-Stempel: „nur Eingabe“. |
| SCH-049 | Im Klartext aufgezeichnete Texteingaben aus Fenstern, die nicht von der Art `structured` sind (Terminal, Remote-Desktop, virtuelle Maschine), tragen `flags.plaintext` und sind damit prüfpflichtig. | Muss | Anhang B.1 des Datenformats: Der Befehl im Terminal trägt das Kennzeichen, die maskierte Eingabe nicht. |
| SCH-050 | Ein endgültig geschwärzter Zeitraum wird ein Lückenschritt mit der Ursache `removedByAuthor`; die Buchführung bleibt vollständig. Das gilt ebenso für den Zeitraum, den „Letzte Eingabe schwärzen“ beim Stopp entfernt; stehen gebliebene Strukturereignisse teilen die Lücke nicht. | Muss | Sitzung vor und nach endgültigem Schwärzen eines Zeitraums: `bookkeeping.ok` ist beide Male `true`, danach steht die Lücke in `gaps`. Aufnahme mit „Letzte Eingabe schwärzen“: `steps.json` enthält genau eine Lücke `removedByAuthor` für den Zeitraum und keinen Schritt mit Inhalten daraus. |
| SCH-051 | Die Zahl der Tastenanschläge (`input.count`, `{ "masked": n }`, N in `{masked:N}` und bei Tippläufen in `{unreviewed:N}`) zählt jedes Drücken außer reinen Modifikatoren und Feststellern – Korrektur- und Bewegungstasten sowie jede automatische Wiederholung eingeschlossen. | Muss | „Abc“ mit Umschalt getippt ergibt 3; „abd“, Rücktaste, „c“ ergibt 5; „a“ fünfmal durch Halten ergibt 5; „@“ über AltGr ergibt 1. Maskierte und im Klartext aufgezeichnete Läufe zählen gleich. |
| SCH-052 | Marker des Autors werden `marker`-Schritte mit ihrem Typ (`input.markerKind`) und dem zum Marker aufgenommenen Bild: `error`, `expected` und `important` als Hauptschritte, `note` als Nebenschritt. Marker und Ziel-Angaben gehen in keine Regel der Ableitung ein. | Soll | Goldenes Journal mit je einem Marker jedes Typs: vier `marker`-Schritte mit richtigem Typ, richtiger Wichtigkeit und Bild; Belegstufen und Wirkungen der Nachbarschritte sind dieselben wie im selben Journal ohne Marker. Dasselbe Journal mit und ohne `goal` ergibt dieselbe `steps.json`. |
| SCH-053 | Für jeden Marker-Typ und jede Ziel-Angabe gibt es Vorlagen in Deutsch und Englisch und in beiden Stilen; im Protokollstil sind sie als Angabe des Autors gekennzeichnet. Fehlende Ziel-Angaben erzeugen keine Zeile. | Soll | Beispielliste mit allen vier Marker-Typen und allen drei Ziel-Angaben in beiden Sprachen und Stilen: Jeder Satz ist grammatisch richtig; jeder Protokollsatz zu `error`, `expected`, `important` und zu den Ziel-Angaben nennt den Autor; eine Sitzung ohne `goal` ergibt keinen Ziel-Block. |
| SCH-054 | Die wirksame Schrittfolge entsteht aus `steps.json`, den Abschnitten und `edits.replacements[]`: Ersetzte und nicht verwendete Schritte gehören nicht dazu, der Ersatz steht an der Stelle des Ersetzten. `steps.json` bleibt unverändert. | Soll | Sitzung mit einer Ersetzung, einer Kette aus zwei Ersetzungen und einer nicht verwendeten Neuaufnahme: Zustand und Reihenfolge jedes Schritts entsprechen 16.1 und 16.2; `steps.json` ist vor und nach dem Anlegen und Entfernen der Ersetzungen bytegleich; S14 besteht. |
| SCH-055 | Die Schrittnummer (`number`) zählt fortlaufend über alle wirksamen Schritte und hängt nicht von Detailstufe oder Filter ab; ersetzte und nicht verwendete Schritte erhalten keine. Jede Nahtstelle wird in jeder Ausgabe ausgewiesen; über eine Nahtstelle hinweg wird weder Abstand noch Wirkung noch Bild zugeordnet. | Soll | Ersetzung von drei Schritten durch fünf: Die folgenden Schritte tragen im KI-Paket in jeder Detailstufe dieselbe, um zwei höhere Nummer, ihre `id` bleibt; die Anleitung nummeriert ihre gezeigten Schritte fortlaufend; an beiden Rändern des Ersatzes steht ein Hinweis; kein Text nennt einen Abstand über die Nahtstelle hinweg. |
| SCH-056 | Der Schrittabgleich ordnet die Kernschritte zweier Sitzungen reihenfolgeerhaltend über Aktionsart und Zielidentität zu. Er kennzeichnet jedes Paar als gleich oder geändert (Ziel, Text, Wirkung, Dauer, Bild) und jeden übrigen Schritt als nur in A oder nur in B. Zusätzliche Neben- und Spurschritte ändern die Paare nicht; Lückenschritte werden immer gemeldet. | Soll | Zwei Aufnahmen desselben Ablaufs, die zweite mit zusätzlichem Scrollen, einem anderen getippten Text, einem ausgelassenen Klick und einem Fehlerdialog: Das Ergebnis nennt ein Paar `changed` mit `text`, einen Schritt `onlyA` und alle übrigen Paare `same`; der Dialog erscheint als Begleitschritt nur in B; das Scrollen ändert kein Paar. Eine UAC-Lücke in A steht als `onlyA` mit `group: "gap"` im Ergebnis. |
| SCH-057 | Der Schrittabgleich ist deterministisch: eine reine Funktion der beiden wirksamen Schrittfolgen, der Parameter und der Version des Verfahrens. | Soll | Zehn Aufrufe mit denselben Eingaben auf zwei Rechnern mit verschiedener Sprache und Zeitzone ergeben bytegleiche Ergebnisse. Eine speichersparende und die einfache Umsetzung liefern für eine Sammlung von Sitzungspaaren – darunter solche mit gleich guten Zuordnungen – dasselbe Ergebnis. |
| SCH-058 | Der Schrittabgleich liest beide Sitzungen ausschließlich über die Leseschicht. Geschwärztes, Maskiertes und Ungeprüftes wird nicht verglichen und als nicht verglichen ausgewiesen; Verallgemeinertes wird als Platzhalter verglichen. | Soll | Sitzungspaar mit geschwärztem Text in A und ungeprüfter Eingabe in B: Die betroffenen Paare tragen `notCompared` mit `text`; die Kanarien-Suche im Ergebnis bleibt leer. Zwei Sitzungen mit verschiedenen, jeweils verallgemeinerten Benutzernamen ergeben an dieser Stelle `same`. |
| SCH-059 | Der Schrittabgleich zweier Sitzungen mit je 1000 Kernschritten dauert ohne Bildvergleich höchstens 300 ms. | Soll | Messung auf Referenzsystem A mit zwei aufgezeichneten Sitzungen; mit je 5000 Kernschritten höchstens 5 s; darüber meldet das Programm die Grenze, statt abzugleichen. |
| SCH-060 | Die Kriterien der Qualitätsprüfung sind reine Funktionen der wirksamen Schrittfolge in der Sicht der Leseschicht. Jede Fundstelle ist durch Regel, Anker und Schlüssel bestimmt und enthält keinen aufgezeichneten Inhalt. | Soll | Für jedes Kriterium aus Abschnitt 18 existiert eine Testsitzung, die es erfüllt, und eine, die es nicht erfüllt. Die Prüfung ändert keine Datei der Sitzung. Nach dem Ignorieren einer Fundstelle zu einem persönlichen Wert steht in `qualityIgnores` nur die Art des Vorschlags. |
| SCH-061 | Schritte eines verworfenen Aufnahmeabschnitts gehören zu keiner Folge und keiner Ausgabe: Sie haben keinen Zustand und keine Schrittnummer und gehen weder in den Schrittabgleich noch in die Qualitätsprüfung noch in die Kennzahlen der wirksamen Folge ein. `steps.json` enthält sie weiter. | Soll | Sitzung mit drei Abschnitten, der zweite verworfen: `steps.json` enthält die Schritte aller drei; die wirksame Folge enthält nur die des ersten und dritten, lückenlos nummeriert, mit einer Nahtstelle `continuation` dazwischen; die Kennzahlen nennen `discardedSections: 1`; im KI-Paket der ausführlichsten Detailstufe findet sich kein Schritt und kein Text des zweiten. |
| SCH-062 | Aufeinanderfolgende aufgenommene Schritte lassen sich für Anleitungen zu einem Schritt zusammenfassen (`edits.merges[]`, Abschnitt 16.7). Die Zusammenfassung wirkt nur in Anleitungen und in der Qualitätsprüfung; `steps.json`, KI-Paket, MCP, Schrittabgleich und AutoHotkey-Export zeigen die Einzelschritte. | Soll | Gruppe aus drei Schritten: Die Anleitung zeigt einen Schritt mit erstem Vorher- und letztem Nachher-Bild; `Effective` und KI-Paket enthalten alle drei; ein geschwärztes Mitglied trägt weder Text noch Bild bei; eine Gruppe mit einer Lücke oder einem Kapitelbeginn in der Mitte wird abgelehnt. |
| SCH-063 | Eine Abfrage der Benutzerkontensteuerung (Lücke `secureDesktop`, Art `uac`) wird der benannte Lückenschritt „Benutzerkontensteuerung bestätigen“ ohne Bild; nie wird ein Bild aus dieser Lücke einem Schritt zugeordnet. Dasselbe Bildverbot gilt für Sperre, Trennung und Ruhezustand. | Muss | Synthetische UAC-Lücke: Schritt mit Titel in Deutsch und Englisch, `frames.pre` und `frames.post` sind `null`, der Hauptschritt davor bindet kein Nachher-Bild aus der Lücke, `validate` meldet keinen Fehler. |
| SCH-064 | Die Schrittliste des Viewers folgt der Reihenfolge der Anleitung (`order`); ausgeblendete Schritte behalten ihren Platz. Die Anleitung selbst lässt Ausgeblendete weg. | Soll | Umsortierte Sitzung: Schrittliste und Anleitung zeigen dieselbe Reihenfolge; der ausgeblendete Schritt steht in der Liste an seinem Platz und fehlt in der Anleitung. |

