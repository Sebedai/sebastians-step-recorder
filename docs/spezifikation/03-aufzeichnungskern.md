# 03 – Aufzeichnungskern

Status: Entwurf 0.1 · Stand: 2026-11-11

**Zweck.** Dieses Dokument legt fest, **was** der Recorder während einer Aufnahme beobachtet und **wie** er es tut: Eingaben über zwei Kanäle, Bilder mit Vorher-Bild-Garantie, Elemente über UI Automation, Fenster-, Sitzungs- und Anzeigeereignisse, Zwischenablage – dazu Zeitmodell, Aufnahmebereiche, Unsichtbarkeitsgarantie, Lückenerkennung und Leistungsbudgets. Prozesse und Threads stehen in [02 – Architektur](02-architektur.md), die Felder der Ereignisse in [04 – Datenformat](04-datenformat.md), die Deutung zu Schritten in [05 – Schritterkennung](05-schritterkennung.md), die Schutzregeln in [10 – Datenschutz und Sicherheit](10-datenschutz-und-sicherheit.md).

**Quellenlage.** Aussagen über das Verhalten von Windows sind gekennzeichnet: *(belegt)* – steht in der Herstellerdokumentation; *(unbestätigt → VE-nn)* – nicht in Primärquellen gefunden, wird im genannten Vorab-Experiment aus [11](11-roadmap-tests-risiken.md) geklärt; *(unbestätigt → Z-n)* – dafür fehlt dort noch ein Experiment, siehe [Abschnitt 15](#offene-punkte). Alle Zahlen ohne Quellenangabe sind Entwurfswerte und einstellbar.

## Inhalt

1. [Überblick und Lebenszyklus](#lebenszyklus)
2. [Zeitmodell](#zeitmodell)
3. [Eingabekanäle](#eingabekanaele)
4. [Bildschirmaufnahme](#bildschirmaufnahme)
5. [Aufnahmebereiche](#aufnahmebereiche)
6. [Unsichtbarkeitsgarantie](#unsichtbarkeit)
7. [UI Automation](#ui-automation)
8. [Fenster-, Sitzungs- und Anzeigeereignisse](#fensterereignisse)
9. [Zwischenablage](#zwischenablage)
10. [Lückenerkennung](#lueckenerkennung)
11. [Andockpunkte für den Datenschutz](#andockpunkte-datenschutz)
12. [Leistungsbudgets](#leistungsbudgets)
13. [Zuordnung der Review-Befunde](#review)
14. [Anforderungen](#anforderungen)
15. [Offene Punkte und Vorab-Experimente](#offene-punkte)

---

<a id="lebenszyklus"></a>
## 1 Überblick und Lebenszyklus

### 1.1 Grundsätze

1. **Der Kern beobachtet, er deutet nicht.** Er schreibt Rohereignisse; was ein Klick, ein Doppelklick oder ein Schritt ist, entscheidet die Schritterkennung.
2. **Der Kern greift nie ein.** Keine Eingabe wird verändert, verzögert oder verschluckt; keine fremde Anwendung wird gesteuert. Einzige Ausnahme: Die registrierten Hotkeys des Recorders erreichen die Zielanwendung nicht ([3.9](#eigene-bedienung)).
3. **Kein stiller Verlust.** Jede Stelle, an der etwas verloren gehen kann, zählt mit und erzeugt eine Lücke (`gap`).
4. **Jede Angabe trägt Zeit und Herkunft:** wann es geschah, wann es beobachtet wurde, über welchen Kanal.
5. **Bildauslöser und Element-Ermittlungen hängen nicht von Schrittregeln ab.** Neu berechnete Schritte haben dadurch immer Bilder und Ziele.
6. **Datenschutz geht vor Vollständigkeit.** Im Zweifel fehlt ein Inhalt – mit Marker.

### 1.2 Erzeuger der Ereignisse

Die Typnamen sind in [04, Ereignistypen](04-datenformat.md#ereignistypen) verbindlich festgelegt. Jeder Typ hat hier genau einen Erzeuger.

| Familie / Typ | Erzeuger | Auslöser | Abschnitt |
|---|---|---|---|
| `session.start`, `session.pause`, `session.resume`, `session.stop` | Aufzeichnungssteuerung | Start, Pause, Fortsetzen, Stopp | [1.3](#lebenszyklus) |
| `session.checkpoint` | Schreiber | mindestens alle 30 s, vor Pause und Stopp | [02](02-architektur.md#schreiben) |
| `session.recovered` | Wiederherstellung (kein Teil der Aufnahme) | nächster Programmstart nach Absturz | [04](04-datenformat.md#absturzsicherheit) |
| `system` (`environment`, `selfTest`) | Aufzeichnungssteuerung | Sitzungskopf, Vorprüfung | [1.4](#lebenszyklus), [6.3](#unsichtbarkeit) |
| `system` (`inputSettings`, `sessionLock`, `sessionUnlock`, `remoteConnect`, `remoteDisconnect`, `consoleConnect`, `consoleDisconnect`, `powerSuspend`, `powerResume`, `desktopSwitch`, `clockChange`) | Systembeobachter | Nachricht des Systems | [8.3](#fensterereignisse) |
| `system` (`keyboardLayout`, `hook`) | Eingabeverarbeitung, Host-Überwachung | Layoutwechsel; Hook gesetzt, verloren, neu gesetzt | [3.4](#kanalabgleich), [3.6](#zeichenuebersetzung) |
| `system` (`captureBackend`) | Bildschirmaufnahme | Wechsel des Aufnahmewegs | [4.2](#bildschirmaufnahme) |
| `system` (`privacyMode`) | Schutzfilter | Privatmodus ein/aus | [10](10-datenschutz-und-sicherheit.md) |
| `system` (`disk`, `recorder`) | Schreiber, Wächter | Speicherplatz; Meldungen des Recorders | [02](02-architektur.md#schreiben) |
| `display.topology` | Systembeobachter | Start, Anzeigeänderung | [4.13](#topologie) |
| `scope.change` | Aufzeichnungssteuerung | Start, Wechsel des Aufnahmebereichs | [5](#aufnahmebereiche) |
| `keystate.sync` | Eingabeverarbeitung | Start, nach Lücke, Fortsetzen, Abweichung | [3.5](#tastenzustand) |
| `mouse.move`, `mouse.down`, `mouse.up`, `mouse.wheel` | Eingabeverarbeitung | Hook-Ereignis (oder Ergänzung aus Raw Input) | [3.7](#eingabekanaele) |
| `mouse.cursor` | Bildschirmaufnahme | Zeigerform geändert | [4.7](#bildschirmaufnahme) |
| `key.down`, `key.up` | Eingabeverarbeitung, freigegeben durch den Schutzfilter | Hook-Ereignis (oder Ergänzung aus Raw Input) | [3.1](#eingabekanaele)–[3.6](#zeichenuebersetzung) |
| `text.masked` | Schutzfilter | zurückgehaltener Tastenlauf | [11](#andockpunkte-datenschutz) |
| `window.info`, `window.open`, `window.close`, `window.foreground`, `window.title`, `window.rect`, `window.menu`, `window.dialog`, `window.switch`, `window.dragdrop` | Fensterregister | WinEvents | [8.1](#fensterereignisse), [8.2](#fensterereignisse) |
| `focus` | UI-Automation-Dienst; Einstufung `ctx` durch den Schutzfilter | Fokuswechsel | [7.6](#ui-automation) |
| `element` | UI-Automation-Dienst | Verweilen, Maustaste, Mausrad, Fokus, Schnappschuss | [7.2](#doppelte-ermittlung) |
| `ui.value`, `ui.text`, `ui.toggle`, `ui.selection`, `ui.expand`, `ui.invoke`, `ui.scroll`, `ui.notification` | UI-Automation-Dienst | UIA-Ereignisse, WinEvents, Nachlesen am fokussierten Element | [7.6](#ui-automation), [7.7](#ui-automation) |
| `ui.snapshot` | UI-Automation-Dienst | neues Fenster, Dialog, Menü, Popup | [7.5](#text-schnappschuss) |
| `frame` | Bildschirmaufnahme und Bildkodierung | Bildauslöser | [4](#bildschirmaufnahme) |
| `clipboard` | Systembeobachter | Zwischenablage geändert | [9](#zwischenablage) |
| `comment`, `marker` | Aufzeichnungssteuerung (ausgelöst durch die Oberfläche) | Nutzeraktion; der Kern friert dazu das Bild ein. Marker tragen ihren Typ | [3.9](#eigene-bedienung), [4.4](#vorher-bild) |
| `gap` | die jeweilige Erkennung | siehe Katalog | [10](#lueckenerkennung) |

### 1.3 Zustände einer Aufnahme

Die Zustände aus Nutzersicht stehen in [07](07-aufnahme-bedienung.md); technisch gilt:

| Zustand | Hooks, Raw Input | Bildaufnahme | UI Automation | Fenster, System, Zwischenablage | Schreiber |
|---|---|---|---|---|---|
| Bereit | – | – | – | – | – |
| Vorlauf (Vorprüfung) | – | Probebilder, werden verworfen | wird vorbereitet | Topologie wird gelesen | – |
| Aufnahme | aktiv | aktiv | aktiv | aktiv | aktiv |
| Pause | entfernt | angehalten | angehalten | angehalten | schreibt Beginn der Lücke `paused`, danach nichts |
| Verarbeitung (Abschluss) | entfernt | Schlussbilder | wird abgemeldet | wird abgemeldet | leert Puffer, schließt ab |

In der Pause wird **nichts** beobachtet. Hotkeys des Recorders bleiben nutzbar, weil sie über `RegisterHotKey` laufen und keinen Hook brauchen.

### 1.4 Start

Bevor die Vorprüfung beginnt, stehen die wirksamen Einstellungen fest ([1.6](#start-vorgaben)). Für einen weiteren Aufnahmeabschnitt in einer bestehenden Sitzung gelten die Abweichungen aus [1.7](#weitere-abschnitte).

1. **Vorprüfung** (höchstens 1 s auf Referenzsystem A): Topologie lesen; je Monitor im Aufnahmebereich Aufnahmeweg wählen und ein Probebild holen; Rahmen anzeigen; Unsichtbarkeits-Selbsttest ([6.3](#unsichtbarkeit)) für alle Monitore gleichzeitig; UI Automation vorbereiten; Speicherplatz und Schreibbarkeit prüfen; Eingabe-Einstellungen lesen. Gleichzeitig wird der Capture-Host gestartet und aufgewärmt – noch **ohne Hooks**. Schlägt der Selbsttest fehl, folgt die Rückfrage aus [07](07-aufnahme-bedienung.md).
2. Sitzungsordner, Manifest, Sperr- und Reservedatei anlegen ([04](04-datenformat.md#absturzsicherheit)); Schreiber starten. Das Manifest nennt von Anfang an das Aufnahmeprofil und die Ziel-Angaben des Autors, soweit vorhanden.
3. Capture-Threads starten; erstes Bild je Monitor liegt im Bildring.
4. Fensterregister, Systembeobachter, Zwischenablage und UIA-Ereignisse starten.
5. Dem Capture-Host den Befehl „laufen" geben: Er setzt Hooks und Raw Input und quittiert. Mit der Quittung steht der Nullpunkt der Sitzungsuhr fest, und der Tastenzustand wird gelesen.
6. **Sitzungskopf** in der Reihenfolge aus [04, Abschnitt 5.2](04-datenformat.md#journal) schreiben; zusätzlich das Ergebnis des Selbsttests, der Zustand der Hooks sowie Fokus, Element und Text-Schnappschuss des Vordergrundfensters als Ausgangslage.
7. Erst danach werden Eingabeereignisse aus dem Ring geschrieben; die Anzeige wechselt auf „Aufnahme".

Die Anzeige (Rahmen) ist ab Schritt 1 sichtbar – also bevor irgendetwas beobachtet wird, das gespeichert werden könnte.

Vom Auslöser bis zum ersten aufgezeichneten Ereignis vergehen ohne Countdown höchstens 1,5 s auf Referenzsystem A – einschließlich Vorprüfung und Selbsttest aller Monitore ([12.8](#leistungsbudgets)).

### 1.5 Stopp

1. Capture-Host beenden (Hooks entfernt).
2. Je Monitor ein Schlussbild (Auslöser `sessionStop`); offene Nachher-Bilder zuordnen.
3. Ausstehende UIA-Abfragen bis zu ihrem Zeitlimit abwarten, höchstens 1 s.
4. Fensterregister, Systembeobachter, Zwischenablage, UIA-Ereignisse abmelden.
5. Den Rückhaltepuffer des Schutzfilters abschließend entscheiden lassen ([11](#andockpunkte-datenschutz)).
6. Bildkodierung leerlaufen lassen (Fortschritt sichtbar, wenn es länger als 1 s dauert).
7. Letzten Kontrollpunkt und `session.stop` schreiben.
8. Schwärzungen, die während der Aufnahme über „Letzte Eingabe schwärzen" angefordert wurden, endgültig anwenden – die ausdrückliche Ausnahme vom Nur-Anhängen ([02, Abschnitt 7.2](02-architektur.md#schreiben), [10](10-datenschutz-und-sicherheit.md)).
9. Dauerhaft sichern, Manifest abschließen.
10. Wurde der Abschnitt als Ersatz für markierte Schritte gestartet: die Ersetzung ins Bearbeitungs-Overlay eintragen ([1.7](#weitere-abschnitte)).

<a id="start-vorgaben"></a>
### 1.6 Was vor dem Start feststeht: wirksame Einstellungen und Ziel-Angaben

**Wirksame Einstellungen.** Der Kern kennt keine Aufnahmeprofile. Vor der Vorprüfung bestimmt die Komponente Aufnahmeprofile aus Startangaben, Profil und Einstellungen die **wirksamen Einstellungen** ([02, Abschnitt 12.5](02-architektur.md#aufnahmeprofile)); nur sie erreichen den Kern.

| Bestandteil eines Profils | Wirkung im Kern | Abschnitt |
|---|---|---|
| Aufnahmebereich-Art | belegt die Bereichsart vor; das Ziel (Monitor, Fenster, Rechteck) wird beim Start bestimmt. Der Wechsel während der Aufnahme bleibt möglich | [5](#aufnahmebereiche) |
| Schutzstufe | Parameter des Schutzfilters an den Andockpunkten A1 bis A3 | [11](#andockpunkte-datenschutz) |
| Zwischenablage-Inhalt | an: der Inhalt wird abgerufen und läuft durch A5; aus: kein Abruf | [9](#zwischenablage) |
| Ergänzungen der Ausschlussliste | erweitern die Ausschlussliste an A7 | [11](#andockpunkte-datenschutz) |
| Bildeinstellungen | Schwellen und Grenzen der Bildaufnahme: Zwischenbilder-Stufe und die Werte unter `capture` | [4.5](#nachher-bild), [4.6](#bildspeicher) |
| Ziel-Felder zeigen, Ausgabe nach dem Stopp, Vorbelegungen | keine – das betrifft Oberfläche und Export | – |

Regeln:

1. **Geprüft, bevor etwas angelegt wird.** Werte außerhalb ihres Bereichs erreichen den Kern nicht.
2. **Festgehalten für den Abschnitt.** Die wirksamen Einstellungen gelten vom Start bis zum Stopp des Aufnahmeabschnitts. Ändert jemand währenddessen Profile oder Einstellungen, wirkt das erst auf die nächste Aufnahme. Was der Autor über die Aufnahmeleiste ändert – Aufnahmebereich, Privattaste –, läuft wie bisher als Ereignis ins Protokoll.
3. **Im Sitzungskopf nachlesbar.** `session.start` nennt die wirksamen Einstellungen (`settings`) und das Profil mit seinem Namen (`profile`); das Manifest spiegelt beides (`manifest.profile`, [04, Abschnitt 4.8](04-datenformat.md#manifest-profile)). Eine Sitzung lässt sich damit ohne die Profildatei des Aufnahmerechners deuten.
4. **Ein Profil stellt Parameter ein, es schaltet nichts ab.** Die Andockpunkte aus [Abschnitt 11](#andockpunkte-datenschutz), die Anzeige der Aufnahme, der Selbsttest der Unsichtbarkeit, die Lückenerkennung und die Leistungsbudgets gelten mit jedem Profil. Die Ausschlussliste kann ein Profil nur erweitern.
5. **Kein Profil senkt den Schutz unbemerkt.** Schützen die wirksamen Einstellungen schwächer als die Einstellungen des Nutzers, erfährt der Autor das, bevor der Capture-Host Hooks setzt ([07](07-aufnahme-bedienung.md), [02](02-architektur.md#aufnahmeprofile)), und der Sitzungskopf hält fest, worin (`profile.weakens`).

**Ziel-Angaben.** Beim Start kann der Autor festhalten, was gezeigt wird, welches Ergebnis er erwartet und worauf sich die Aufnahme bezieht: `manifest.goal` mit `description`, `expected` und `reference` – alle drei freiwillig.

- **Herkunft.** Startfenster oder Support-Ablauf; Vorbelegung aus Profil oder Kommandozeile ([07](07-aufnahme-bedienung.md), [02](02-architektur.md#befehle-nachgelagert)).
- **Keine Tastenereignisse.** Die Felder werden ausgefüllt, bevor der Capture-Host Hooks setzt ([1.4](#lebenszyklus), Schritt 5). Was der Autor dort tippt, erscheint deshalb nie als Eingabe im Protokoll.
- **Sitzungskopf.** Die Aufzeichnungssteuerung schreibt die Angaben beim Anlegen ins Manifest und – damit das Journal auch ohne Manifest verständlich bleibt – ins `session.start` des ersten Abschnitts ([04, Abschnitt 4.7](04-datenformat.md#manifest-goal)). Leere Felder fehlen; sind alle leer, fehlt `goal`.
- **Unverändert und begrenzt.** Der Kern speichert den Text, wie er eingegeben wurde, innerhalb der Längengrenzen aus 04. Er deutet ihn nicht.
- **Absicht, nicht Beobachtung.** Kein Ereignis des Kerns stützt sich auf diese Texte, und sie ändern nichts daran, was aufgezeichnet wird.
- **Schutz.** Es sind Texte des Autors wie Kommentare: Durchsicht und Schwärzung greifen über die Leseschicht ([02, Abschnitt 7.4](02-architektur.md#datenfluss), [10](10-datenschutz-und-sicherheit.md)). In Diagnoseprotokollen stehen sie nie.
- **Später ändern.** Geändert wird im Viewer über `edits.goal`; Manifest und Journal bleiben, wie sie sind. Weitere Abschnitte fragen keine Ziel-Angaben ab und schreiben keine ([1.7](#weitere-abschnitte)).

<a id="weitere-abschnitte"></a>
### 1.7 Weitere Aufnahmeabschnitte: Anhängen sowie „Neu aufnehmen und ersetzen"

Eine Sitzung kann mehrere Aufnahmeabschnitte enthalten ([04, Abschnitt 7.4](04-datenformat.md#aufnahmeabschnitte)). Ein weiterer Abschnitt entsteht auf zwei Wegen, die sich für den Kern nur im Sitzungskopf und am Ende unterscheiden:

| Weg | Auslöser | Zweck im Sitzungskopf (`session.start`) | Folge im Bearbeitungs-Overlay |
|---|---|---|---|
| Anhängen | „Aufnahme fortsetzen" im Viewer, `record --append` ([07](07-aufnahme-bedienung.md), [08](08-viewer.md)) | `purpose: "append"` | keine |
| Neu aufnehmen und ersetzen | im Viewer markierte Schritte, Befehl „Neu aufnehmen und ersetzen" ([08](08-viewer.md)) | `purpose: "replacement"` und `replaces` mit dem Bereich, für den neu aufgenommen wird | eine Ersetzung in `edits.replacements` |

**Grundsatz.** Ein weiterer Abschnitt hängt an. Er ändert keine Zeile, kein Bild und keinen Schritt der vorhandenen Abschnitte. Auch beim Ersetzen bleiben die alten Rohdaten vollständig erhalten; ersetzt wird allein in der Darstellung.

**Voraussetzungen** – geprüft in der Vorprüfung, bevor irgendetwas in die Sitzung geschrieben wird:

1. Die Sitzung ist abgeschlossen (nicht laufend, nicht abgestürzt) und liegt als beschreibbarer Ordner vor, nicht als gepacktes `.steprec`.
2. Niemand sonst schreibt sie (Sperrdatei). Der Viewer hat seine Bearbeitungen gesichert und die Sitzung freigegeben.
3. Ihre Formatversion ist die, die dieser Recorder schreibt.
4. Das Journal endet mit `session.stop` und hat die im Manifest genannte Länge.
5. Der Aufnahmebereich lässt sich herstellen (unten).

Ist eine Voraussetzung nicht erfüllt, beginnt keine Aufnahme, und die Sitzung bleibt unberührt.

**Aufnahmebereich der Sitzung.** Der Abschnitt startet mit dem Aufnahmebereich der Sitzung: beim Anhängen mit dem zuletzt gültigen, beim Ersetzen mit dem, der beim ersten zu ersetzenden Schritt galt. Verworfene Abschnitte zählen dabei nicht.

| Gespeicherter Bereich | Wiederherstellung | Nicht möglich, wenn |
|---|---|---|
| Ganzer Bildschirm – Monitor mit Aktivität oder alle | unmittelbar | – |
| Ganzer Bildschirm – bestimmter Monitor | derselbe Monitor, wiedererkannt am Gerät ([4.13](#topologie)) | der Monitor nicht angeschlossen ist |
| Eine Anwendung | dieselbe Anwendung nach Programmdatei bzw. Anwendungskennung ([5.4](#aufnahmebereiche)). Prozess- und Fensterkennungen des alten Abschnitts gelten nicht mehr; bei „nur dieses Fenster" wird das einzige Fenster der Anwendung genommen, sonst öffnet sich die Fensterwahl | die Anwendung nicht läuft |
| Bereich (Rechteck) | dasselbe Rechteck auf demselben Monitor, sofern dessen Pixelmaße und Drehung unverändert sind | der Monitor fehlt oder andere Maße hat |

Lässt sich der Bereich nicht herstellen, öffnet sich die passende Auswahl ([07](07-aufnahme-bedienung.md)) – nie wird stillschweigend ein anderer Bereich genommen. Beim Start über die Kommandozeile endet der Aufruf stattdessen mit Exit-Code 7. Der tatsächlich verwendete Bereich steht wie immer als `scope.change` im Kopf des neuen Abschnitts; der Autor kann ihn während der Aufnahme wechseln.

**Einstellungen.** Der Abschnitt übernimmt die wirksamen Einstellungen seines Bezugsabschnitts – beim Anhängen ist das der letzte nicht verworfene Abschnitt, beim Ersetzen der mit den zu ersetzenden Schritten –, damit Bilder und Schutz zum Rest der Sitzung passen. Zwei Abweichungen: Die Ausschlussliste wird mit der aktuell geltenden vereinigt; sie kann nur wachsen. Und schützt das Ergebnis schwächer als die aktuellen Einstellungen, sagt die Startmeldung das wie bei einem Profil ([1.6](#start-vorgaben)). Ein Profil wird nicht neu gewählt, Ziel-Angaben werden nicht abgefragt.

**Start.** Es gilt die Reihenfolge aus [1.4](#lebenszyklus) mit diesen Unterschieden:

| Schritt aus 1.4 | Beim weiteren Abschnitt |
|---|---|
| 1 Vorprüfung | zusätzlich die Voraussetzungen oben. Wahl des Aufnahmewegs und Selbsttest der Unsichtbarkeit laufen neu – sie gelten je Start, nicht je Sitzung |
| 2 Sitzung anlegen | Die bestehende Sitzung wird geöffnet: Sperr- und Reservedatei anlegen, Manifest auf `recording` setzen, Journal zum Anhängen öffnen. Der Schreiber setzt `seq` lückenlos fort |
| 5 Hooks setzen | unverändert; die Quittung ist der Nullpunkt **dieses** Abschnitts ([2.1](#zeitmodell)) |
| 6 Sitzungskopf | vollständig wie beim ersten Abschnitt, mit der nächsten Abschnittsnummer und dem Zweck des Abschnitts (`purpose`, beim Ersetzen zusätzlich `replaces`), `display.topology` mit dem Grund `partStart` und einem Schlüsselbild je Monitor – ohne Ziel-Angaben und ohne Profil |

**Was über die Abschnittsgrenze fortläuft** – nie neu vergeben, nie wiederverwendet:

| Größe | Regel |
|---|---|
| `seq` | lückenlos weiter |
| Sitzungsuhr `t` | beginnt beim Ende des vorigen Abschnitts; die Zeit zwischen den Abschnitten zählt nicht mit |
| Eingabenummer `inSeq` | beginnt oberhalb der höchsten im Journal genannten Nummer; keine Nummer kommt doppelt vor |
| Topologie-Epoche | nächste Nummer – auch wenn die Monitore unverändert sind |
| Monitor-Kennungen | Derselbe Monitor behält seine Kennung, wenn er am Gerät wiedererkannt wird. Sonst erhält er eine neue – nie die eines anderen |
| Kennungen von Fenstern, Bildern, Lücken, Kommentaren, Markern und Zeigerformen | laufen weiter. Fenster werden im neuen Abschnitt neu eingeführt (`window.info`), auch wenn sie im alten schon vorkamen: Fensterkennungen des Systems überdauern keinen Abschnitt |

**Was an der Grenze neu beginnt:**

- **Uhrenbasis.** Zählerstand, Zählerfrequenz und Millisekunden-Zähler des Systems werden neu gelesen und stehen im `session.start` des Abschnitts. Ein Abschnitt kann nach einem Neustart oder auf einem anderen Rechner entstehen.
- **Bildketten.** Das erste Bild je Monitor ist ein Schlüsselbild; kein Differenzbild verweist in einen früheren Abschnitt. Gleiche Bildinhalte teilen sich trotzdem denselben Blob, weil Blobs nach ihrem Inhalt benannt sind.
- **Zustände.** Tastenzustand, Fokus, Verweil-Ergebnisse, Rückhaltepuffer, Fensterregister und der Vergleich der Zwischenablage beginnen leer.
- **Lücken.** Keine Lücke bleibt über die Grenze offen. Die Zeit zwischen den Abschnitten ist keine Lücke, weil die Sitzungsuhr sie nicht enthält.

**Während der Aufnahme** gilt die ganze Sitzung als laufende Aufnahme: Abnehmer ohne Oberfläche liefern sie in dieser Zeit nicht aus ([02, Abschnitt 7.4](02-architektur.md#datenfluss)). Endet der Abschnitt durch einen Absturz, greift die Wiederherstellung aus [04](04-datenformat.md#absturzsicherheit); die früheren Abschnitte sind davon nicht betroffen.

**Stopp.** Wie in [1.5](#lebenszyklus). Danach wird `steps.json` über das gesamte Journal neu abgeleitet. Bearbeitungen bleiben gültig, weil sie an Ereignisnummern und Bild-Kennungen hängen, nicht an Schrittnummern. Abgeleitete Daten der Sitzung (Vorschaubilder, Suchindex) entstehen neu. Eingaben des neuen Abschnitts sind ungeprüft; der Stand der Durchsicht für die alten bleibt ([10](10-datenschutz-und-sicherheit.md)).

**Ersetzung eintragen.** Wurde der Abschnitt als Ersatz gestartet und sauber beendet, trägt die Aufzeichnungssteuerung eine Ersetzung ins Bearbeitungs-Overlay ein – über dieselbe Schnittstelle wie bei „Letzte Eingabe schwärzen": einen Eintrag in `edits.replacements` mit `replaces` (`section`, `fromSeq`, `toSeq` – dieselben Werte wie im `session.start` des Abschnitts), `by.section` (der neue Abschnitt) und `atUtc` ([04, Abschnitt 13.13](04-datenformat.md#edits-replacements)). `section` ist die Abschnittsnummer, die in `session.start` als `part` steht.

- Der Sitzungskopf sagt, **wofür** aufgenommen wurde; erst der Eintrag im Overlay lässt den Abschnitt an die Stelle der alten Schritte treten. Ohne Eintrag ist er eine nicht verwendete Neuaufnahme.
- Wer die Ersetzung im Viewer aufhebt, entfernt nur den Eintrag. Weil der vorgesehene Bereich im Sitzungskopf steht, lässt sie sich jederzeit wieder anlegen.
- Endet der Abschnitt durch Absturz, entsteht kein Eintrag; nach der Wiederherstellung ist er eine nicht verwendete Neuaufnahme. Wird er verworfen, entsteht ebenfalls keiner (unten).

**Die Zeitachse bleibt wahr.** Der Ersatz liegt im Journal und auf der Sitzungsuhr am **Ende** der Sitzung, nicht an der Stelle der ersetzten Schritte. Dass er in Anleitung und KI-Paket dort erscheint, ist Darstellung und Sache der Leseschicht. Dauern und Abstände lassen sich deshalb nie über eine Abschnittsgrenze hinweg rechnen.

**Ausgangslage.** Der Kern kann nicht herstellen, was vor den ersetzten Schritten auf dem Bildschirm stand. Der Kopf des neuen Abschnitts hält darum fest, wovon der Ersatz ausgeht: Schlüsselbild, Vordergrundfenster, Fokus, Element und Text-Schnappschuss.

**Verwerfen.** „Aufnahme verwerfen" betrifft bei einem weiteren Abschnitt nur diesen Abschnitt – nie die Sitzung und nie frühere Abschnitte.

1. **Nichts wird gekürzt.** Das Journal wird nie gekürzt, auch hier nicht. Der Abschnitt endet wie bei jedem Stopp ([1.5](#lebenszyklus)) – einschließlich der endgültigen Anwendung von „Letzte Eingabe schwärzen" – und wird als **verworfen** gekennzeichnet. Das Kennzeichen legt [04](04-datenformat.md#aufnahmeabschnitte) fest.
2. **Aus allen Ausgaben heraus.** Die Leseschicht hält einen verworfenen Abschnitt aus jeder Ausgabe heraus: Seine Schritte, Ereignisse, Bilder und Texte erscheinen weder in Anleitung und KI-Paket noch beim MCP-Server, in Suche, Suchindex, Schrittabgleich oder Support-Paket ([02, Abschnitt 7.4](02-architektur.md#datenfluss)).
3. **Kein Ersatz, kein Bezug.** Für einen verworfenen Ersatz-Abschnitt entsteht kein Eintrag in `edits.replacements`; die Schritte, die er ersetzen sollte, gelten weiter. Als Bezugsabschnitt für Aufnahmebereich und Einstellungen späterer Abschnitte zählt er nicht.
4. **Die Zählungen laufen weiter.** `seq`, Sitzungsuhr, Eingabenummer, Epochen und Kennungen setzen hinter einem verworfenen Abschnitt fort wie hinter jedem anderen.
5. **Verworfen heißt nicht gelöscht.** Die Rohdaten des Abschnitts bleiben im Sitzungsordner. Wer den Ordner weitergibt, gibt sie mit; die Oberfläche sagt das beim Verwerfen ([07](07-aufnahme-bedienung.md)).

Die erste Aufnahme einer Sitzung zu verwerfen bleibt, was es war: Die ganze Sitzung wandert in den Papierkorb ([07](07-aufnahme-bedienung.md)).

---

<a id="zeitmodell"></a>
## 2 Zeitmodell

Die Feldbedeutungen stehen in [04, Zeitmodell](04-datenformat.md#zeitmodell). Dieser Abschnitt legt fest, **woher** der Kern die Zeiten nimmt.

### 2.1 Zeitbasis

- Einzige Uhr ist der Leistungszähler von Windows (`QueryPerformanceCounter`). Er ist systemweit; Capture-Host und Hauptprozess lesen dieselbe Uhr.
- Nullpunkt eines Aufnahmeabschnitts ist der Moment, in dem der Capture-Host das Setzen der Hooks quittiert. `t` zählt von dort in Mikrosekunden; bei angehängten Abschnitten läuft die Sitzungsuhr ab dem Ende des vorigen Abschnitts weiter ([04, Abschnitt 7.4](04-datenformat.md#zeitmodell)).
- Jeder Abschnitt hat seine eigene Uhrenbasis: Zählerstand, Zählerfrequenz und Millisekunden-Zähler des Systems zum Nullpunkt stehen in seinem `session.start`. Keine Umrechnung greift auf die Basis eines anderen Abschnitts zurück – zwischen zwei Abschnitten kann ein Neustart oder ein anderer Rechner liegen ([1.7](#weitere-abschnitte)).
- Darstellungszeiten von Bildern, die vor dem Nullpunkt liegen (das erste Bild zeigt einen schon länger unveränderten Bildschirm), werden auf den Nullpunkt gesetzt.

### 2.2 Geschehen und Beobachtung

Jedes Ereignis trägt `t` (wann es geschah, bestmögliche Schätzung) und – wenn abweichend – `tObs` (wann der Kern es erfuhr). Zugeordnet wird immer nach `t`.

| Ereignisart | `t` | `tSrc` | Unsicherheit |
|---|---|---|---|
| Hook-Eingaben (`mouse.*`, `key.*`) | Zählerstand, als Erstes im Hook-Aufruf gelesen | `hook` | unter 0,1 ms |
| Aus Raw Input ergänzte Eingaben | Zählerstand bei Verarbeitung der Raw-Input-Nachricht, abzüglich der mittleren gemessenen Verzögerung des Kanals | `raw` oder `est` | wenige Millisekunden; `tErr` gesetzt |
| Bilder über Desktop Duplication | Darstellungszeit `LastPresentTime` – selbst ein Zählerstand *(belegt)* | `present` | exakt |
| Bilder über Windows.Graphics.Capture | Systemzeit des Bilds (`SystemRelativeTime`) *(unbestätigt → Z-3)*; sonst Zeitpunkt des Eintreffens | `present` bzw. `obs` | exakt bzw. eine Bildperiode |
| Bilder über GDI | Zeitpunkt, zu dem die Kopie abgeschlossen war | `obs` | Dauer der Kopie |
| WinEvents (`window.*`, Teile von `focus` und `ui.*`) | Ereigniszeit des Systems in Millisekunden, auf die Sitzungsuhr umgerechnet | `sys` | ±16 ms; `tErr` gesetzt |
| Ergebnisse von UIA-Abfragen (`element`, `ui.snapshot`, nachgelesene `ui.value`) | Beginn der Abfrage; `tObs` ist ihr Ende | `obs` | der beschriebene Zustand liegt zwischen `t` und `tObs` |
| UIA-Ereignisse (`focus`, `ui.*`) | Eintreffen – das System liefert keinen Zeitstempel | `obs` | unbekannt, typisch 10–200 ms nach dem Geschehen |
| Zwischenablage, Sitzungs-, Energie-, Anzeigenachrichten | Eintreffen | `obs` | wenige Millisekunden |

### 2.3 Systemzeiten umrechnen

Hook-Strukturen und WinEvents tragen einen Millisekunden-Zähler des Systems (32 Bit, läuft nach 49,7 Tagen über). Der Kern hält dafür Stützpaare aus Millisekunden-Zähler und Leistungszähler, erneuert sie alle 10 s und rechnet darüber um. Liegt das Ergebnis nach `tObs` oder mehr als 5 s davor, wird es verworfen und `t` = `tObs` mit `tSrc: "obs"` gesetzt.

### 2.4 Reihenfolge

- `seq` ist die Schreibreihenfolge und sonst nichts.
- **Je Kanal geordnet.** Mausereignisse unter sich und Tastenereignisse unter sich werden in der Reihenfolge geschrieben, in der sie geschahen ([04, Abschnitt 7.3](04-datenformat.md#zeitmodell)).
- **Zwischen den Kanälen nicht.** Der Schutzfilter hält Tasten zurück, bis ihr Eingabekontext feststeht; Mausereignisse warten darauf nicht. Eine Taste kann deshalb später im Protokoll stehen als ein Mausereignis, das nach ihr geschah.
- **Eingabenummer (Pflicht).** **Jedes** Eingabeereignis aus dem Hook trägt eine laufende Nummer, `inSeq`. Sie folgt der Reihenfolge der Hook-Aufrufe im Capture-Host über beide Kanäle und gibt damit die physische Reihenfolge wieder. Auch Eingaben, die später nicht geschrieben werden (maskiert, außerhalb des Aufnahmebereichs, eigene Bedienung), verbrauchen eine Nummer; nach einem Ring-Überlauf rückt die Nummer um die vom Capture-Host gemeldete Zahl verlorener Sätze vor. Fehlt eine Nummer im Protokoll, muss deshalb eine Lücke oder ein `text.masked` sie erklären.
- **Angehaltener Zähler bei ausgeschlossenen Anwendungen.** Für Eingaben, deren Fenster-Stempel – bei Maustasten und Rad auch das Fenster unter dem Zeiger – zu einer ausgeschlossenen Anwendung gehört, wird **keine** Nummer vergeben. Die Nummernfolge läuft nach der Lücke `excludedApp` nahtlos weiter und verrät so nicht, wie viel dort geschah. Damit das ohne Wettlauf gelingt, vergibt die Eingabeverarbeitung die Nummern beim Entnehmen aus dem Ring: in der Reihenfolge des Capture-Hosts und erst nach der Ausschlussprüfung über den Fenster-Stempel. Ein noch unbekanntes Fenster wird dafür sofort eingestuft; Programm und Titel lassen sich ohne Wartezeit lesen.
- Sätze aus Raw Input werden getrennt gezählt und verbrauchen keine Eingabenummern.
- **Bezug vor Anreicherung.** Bilder und Elementdaten zu einer zurückgehaltenen Taste werden erst nach ihr geschrieben.
- Aus Raw Input ergänzte Ereignisse (`recovered`) haben keine Eingabenummer und dürfen ein kleineres `t` haben als ihre Vorgänger.
- Alle übrigen Ereignisse (Fenster, Elemente, Bilder) sind **Nachzügler**: Sie werden geschrieben, sobald sie vorliegen, und tragen die Zeit des Geschehens. Der größte beobachtete Rückstand steht in den Leistungszählern der Sitzung.
- Mausbewegungen werden nach den Regeln aus [04, Mausbewegungs-Bündel](04-datenformat.md#mausbewegungen) gebündelt; kein Bündel überspannt ein anderes Eingabeereignis.

### 2.5 Wanduhr und Ruhezustand

- `session.start` nennt Wanduhrzeit und Zeitzone; `session.checkpoint` liefert Stützstellen. Wird die Systemuhr verstellt, schreibt der Kern `system` (`clockChange`).
- Ruhezustand: Beim Einschlafen und Aufwachen entstehen `system`-Ereignisse und eine Lücke `suspend`. Ob der Leistungszähler die Schlafzeit mitzählt, wird nicht vorausgesetzt *(unbestätigt → Z-5)*; nach dem Aufwachen folgen ein Kontrollpunkt, eine neue Topologie-Epoche und ein Abgleich des Tastenzustands.

---

<a id="eingabekanaele"></a>
## 3 Eingabekanäle

### 3.1 Kanal A: Low-Level-Hooks

Der Capture-Host setzt `WH_KEYBOARD_LL` und `WH_MOUSE_LL` auf seinem Hook-Thread ([02](02-architektur.md#thread-modell)). Low-Level-Hooks werden im eigenen Prozess aufgerufen; es wird nichts in fremde Prozesse geladen.

| Sachverhalt | Quellenlage |
|---|---|
| Der Aufruf geschieht synchron, bevor die Eingabe die Anwendung erreicht, im Kontext des setzenden Threads über dessen Nachrichtenschleife | belegt |
| Zeitlimit `LowLevelHooksTimeout` (Registry, je Benutzer); ab Windows 10 1709 sind höchstens 1000 ms wirksam | belegt |
| Standardwert des Zeitlimits 300 ms | unbestätigt → Z-13 (nur in einem älteren Microsoft-Blog genannt) |
| Bei Überschreitung übergeht Windows den Hook; seit Windows 7 wird er stillschweigend entfernt, ohne Benachrichtigung | belegt |
| Entfernt wird erst nach der elften Überschreitung | unbestätigt → Z-13 |
| Es gibt keine dokumentierte Möglichkeit, das Entfernen festzustellen; Microsoft empfiehlt zum Beobachten Raw Input | belegt |
| Der zuletzt gesetzte Hook wird zuerst aufgerufen; ein Hook kann ein Ereignis verschlucken, sodass spätere Hooks es nie sehen | belegt |
| Ein Prozess ohne Adminrechte erhält keine Eingaben, solange ein Fenster mit höheren Rechten im Vordergrund ist | unbestätigt → VE-02 (nur indirekt belegt) |
| Die Position in der Maus-Hook-Struktur ist in physischen Pixeln angegeben, wenn der Prozess Per-Monitor-V2 ist | belegt; Verhalten bei gemischter Skalierung → VE-06 |

Erfasst werden je Ereignis unverändert:

- **Maus:** Nachricht (Bewegung; linke, rechte, mittlere Taste und Zusatztasten gedrückt/losgelassen; Rad senkrecht und waagerecht), Position, Raddrehung bzw. Zusatztaste, Kennzeichen (künstlich erzeugt; von einem Prozess niedrigerer Integrität erzeugt), Systemzeit, Zusatzinformation (daraus die Zeigerart Maus, Touch oder Stift).
- **Tastatur:** Nachricht (gedrückt/losgelassen, auch als Systemtaste), virtueller Tastencode, Scancode, Kennzeichen (erweiterte Taste, künstlich erzeugt, niedrigere Integrität, Alt gedrückt, losgelassen), Systemzeit, Zusatzinformation.

Der Host liest beim Start `LowLevelHooksTimeout` (nur lesend). Der Wert wird mit den Eingabe-Einstellungen in der Sitzung festgehalten ([04, Abschnitt 4.2](04-datenformat.md#manifest)); liegt er unter 200 ms, warnt die Aufnahmeleiste.

### 3.2 Regeln für den Hook-Aufruf

1. Ist der Aufrufcode negativ, wird sofort weitergereicht.
2. Als Erstes wird der Leistungszähler gelesen.
3. Die Felder der Struktur werden unverändert in einen Satz kopiert.
4. Das Vordergrundfenster wird gelesen – bei **jedem** Ereignis, auch bei Mausbewegungen. Das ist der **Fenster-Stempel**, auf dem die Schutzregeln in [10](10-datenschutz-und-sicherheit.md) aufbauen.
5. Bei Maustasten und Radbeginn zusätzlich: das Fenster unter dem Zeiger und dessen oberstes Fenster. Bei Tasten, Maustasten und Mausrad: Eintrag in die Pin-Tafel sowie die Signale „Pin" und „Daten vorhanden".
6. Der Satz wird in den Eingabe-Ring geschrieben. Der Aufruf wartet nie.
7. Der Aufruf endet **immer** mit dem Weiterreichen an den nächsten Hook und gibt dessen Ergebnis zurück.
8. Verboten sind: Speicheranforderung, Sperren, Ein-/Ausgabe, Protokollierung, COM, UI Automation und jeder Aufruf, der Nachrichten an fremde Fenster sendet.
9. Am Ende wird die eigene Dauer gemessen und im Histogramm gezählt.

Zeitziel: Median höchstens 20 µs, 99,9 % unter 1 ms, nie mehr als die Hälfte des wirksamen Zeitlimits. Ob das Lesen des Fensters unter dem Zeiger im Aufruf das Budget hält, misst VE-01 *(unbestätigt → Z-7)*. Hält es nicht, liest die Eingabeverarbeitung das Fenster unmittelbar nach der Entnahme aus dem Ring (Ziel unter 2 ms nach der Eingabe) und kennzeichnet die Angabe.

### 3.3 Kanal B: Raw Input

Der Host registriert auf einem eigenen Thread Tastatur und Maus für Raw Input mit `RIDEV_INPUTSINK` (Empfang auch ohne Fokus) und Benachrichtigung über An- und Abstecken von Geräten. Gelesen wird gebündelt.

Zweck:

1. **Verlust erkennen:** Was Raw Input meldet und der Hook nicht, hat der Hook nicht gesehen.
2. **Ergänzen:** Fehlende Tasten und Maustasten werden aus Raw Input nachgetragen.
3. **Gerät benennen** *(Kann)*: Welche Tastatur oder Maus die Eingabe erzeugte.

Raw Input eignet sich nicht als Hauptkanal: Es kommt nicht vor der Anwendung an (keine Vorher-Bild-Garantie), liefert bei Mäusen relative Bewegungen statt Positionen und kennzeichnet künstlich erzeugte Eingaben nicht.

| Sachverhalt | Quellenlage |
|---|---|
| Raw Input wird vom System nicht stillschweigend abgeschaltet | unbestätigt → VE-02 (nur Blog) |
| Raw Input sieht Tasten, die ein fremder Hook verschluckt | unbestätigt → VE-02 |
| Raw Input liefert Eingaben, während ein Fenster mit Adminrechten im Vordergrund ist | unbestätigt → VE-02 |
| Raw Input enthält künstlich erzeugte Eingaben | unbestätigt → Z-11 |
| Ein leeres Geräte-Handle kennzeichnet künstlich erzeugte Eingaben | **widerlegt** als verlässliches Merkmal: Es kommt auch bei Präzisions-Touchpads vor |

Von Maus-Raw-Input werden nur Tasten und Rad ausgewertet; Bewegungen werden gezählt, nicht gespeichert.

<a id="kanalabgleich"></a>
### 3.4 Abgleich der Kanäle, Hook-Verlust, Ring-Überlauf

**Paarbildung.** Ein Tastenereignis ist bestimmt durch Scancode samt Erweiterungskennzeichen und die Richtung (gedrückt/losgelassen). Ein Raw-Ereignis sucht sein Hook-Gegenstück innerhalb von ±100 ms; gefundene Paare sind erledigt. Für Maustasten und Rad gilt dasselbe mit Taste und Richtung.

| Befund | Deutung | Reaktion |
|---|---|---|
| Hook- und Raw-Ereignis passen | Normalfall | Hook-Ereignis protokollieren |
| Hook-Ereignis ohne Raw-Gegenstück, als künstlich gekennzeichnet | künstlich erzeugte Eingabe | protokollieren mit Kennzeichen; kein Fehler |
| Hook-Ereignis ohne Raw-Gegenstück, nicht gekennzeichnet | Gerät ohne Raw Input oder Raw-Verlust | protokollieren; Zähler |
| Raw-Ereignis ohne Hook-Gegenstück nach 250 ms | Der Hook hat es nicht gesehen: von fremdem Programm verschluckt, einzelne Zeitüberschreitung oder Hook entfernt | Ereignis aus Raw Input ergänzen (`recovered`, Kanal `raw`); Zähler „Fehlstelle" |
| Mindestens 3 Fehlstellen in 2 s **oder** 500 ms lang Raw-Ereignisse ohne ein einziges Hook-Ereignis | Hook verloren | Hook neu setzen; `system` (`hook`: `lost`, `reinstalled`); Lücke `hookLost` vom ersten betroffenen Ereignis bis zum Neusetzen; danach Tastenzustand abgleichen |
| Einzelne Fehlstellen, die sich nicht ergänzen lassen oder nicht erklärbar sind | unerklärte Abweichung | Lücke `inputMismatch` (Phase `single`) |

Regeln für ergänzte Ereignisse:

- Sie tragen kein Kennzeichen „künstlich", kein Vordergrundfenster aus dem Hook-Aufruf und eine geschätzte Zeit. Für Maustasten gilt die letzte bekannte Zeigerposition.
- Sie durchlaufen den Schutzfilter wie alle Eingaben.
- Ihr Vorher-Bild hat höchstens die Garantiestufe `atInput` ([4.4](#vorher-bild)).
- Hat ein fremdes Programm die Taste bewusst abgefangen und eine andere erzeugt (Tastenumbelegung), stehen beide im Protokoll: die physische Taste als ergänztes Ereignis, die erzeugte als künstliches Hook-Ereignis. Die Schritterkennung kann so sagen, was gedrückt wurde und was ankam.

**Weitere Wächter:**

| Wächter | Prüft | Folge |
|---|---|---|
| Herzschlag ([02](02-architektur.md#prozessmodell)) | Leben beider Host-Threads | Neustart des Hosts, Lücke `hookLost` |
| Aufrufdauer | Jeder Hook-Aufruf über der Hälfte des Zeitlimits | `system` (`recorder`, Warnung); Zähler |
| Systemzeit der letzten Eingabe (`GetLastInputInfo`, alle 250 ms) | Das System meldet eine Eingabe, die mehr als 500 ms jünger ist als das letzte Ereignis beider Kanäle | Lücke, Ursache nach Lage: `elevatedWindow`, `secureDesktop`, sonst `unknown` |
| Bewegung nur in Raw Input | Raw meldet länger als 200 ms Bewegung, der Hook keine, und der Zeiger steht | Zeiger ist gefangen (relativer Mausmodus in Spielen, 3D- und Fernsteuerprogrammen): Lücke `relativeMouse` für den Mauskanal, bis der Zeiger wieder frei ist |

**Kein stilles Weiterlaufen.** Gelingt das Neusetzen nicht oder wiederholt sich der Verlust dreimal in einer Minute, pausiert die Aufnahme mit sichtbarer Meldung (`session.pause`, Grund `error`).

**Ring-Überlauf.** Kann der Host nicht schreiben, zählt er mit und schreibt, sobald wieder Platz ist, einen Überlaufsatz mit Anzahl und Zeitraum. Daraus wird eine Lücke `overload`. Ab 75 % Füllung schreibt der Host nur noch jede vierte Mausbewegung (ebenfalls als `overload` vermerkt); Tasten, Maustasten und Rad gehen erst bei vollem Ring verloren.

Der Recorder setzt seine Hooks nicht vorsorglich neu, um in der Hook-Kette nach vorn zu rücken. Neu gesetzt wird nur nach erkanntem Verlust.

<a id="tastenzustand"></a>
### 3.5 Tastenzustand und Neuabgleich

Die Eingabeverarbeitung führt aus dem Ereignisstrom einen eigenen Tastenzustand (alle Tasten, Feststeller, Maustasten). Er dient der Zeichenübersetzung, den Angaben `mods` und `locks` und der Erkennung von Wiederholungen.

Das Problem: Geht das Loslassen einer Taste verloren – typisch nach Win+L, einer UAC-Abfrage, einem Adminfenster oder einem Hook-Verlust –, gilt die Taste weiter als gedrückt, und alles Folgende würde als Tastenkombination gedeutet.

| Anlass für den Abgleich | `reason` in `keystate.sync` |
|---|---|
| Sitzungskopf | `initial` |
| Ende jeder Lücke; Entsperren; Hook neu gesetzt; Vordergrund wechselt von Admin- zu normalem Fenster | `afterGap` |
| Rückkehr vom sicheren Desktop (UAC-Abfrage, `Strg+Alt+Entf`) | `secureDesktop`; das Ereignis nennt die Lücke (`gapId`) |
| Fortsetzen nach Pause | `resume` |
| Stichprobe alle 2 s ohne Eingabe zeigt eine Abweichung bei Umschaltern | `mismatch` |

Verfahren:

1. Für Umschalter, Maustasten und alle Tasten, die als gedrückt gelten, wird der tatsächliche Zustand über `GetAsyncKeyState` gelesen.
2. Abweichungen werden im eigenen Zustand berichtigt.
3. `keystate.sync` hält den Stand und die Berichtigungen fest. Genannt werden dort nur Umschalter, Feststeller und Maustasten; druckbare Tasten werden berichtigt, aber nicht genannt ([04](04-datenformat.md#ev-keystate-sync)).

Die Feststeller (Feststelltaste, Num, Rollen) werden aus den Ereignissen mitgeführt und beim Abgleich neu gelesen. Das verlässliche Verfahren dafür aus einem Prozess ohne Tastaturfokus wird noch festgelegt *(unbestätigt → Z-1)*; bis dahin gilt bei Zweifel der mitgeführte Wert, und die Zeichenübersetzung wird über den Feldinhalt gegengeprüft.

<a id="zeichenuebersetzung"></a>
### 3.6 Zeichenübersetzung

Zu jedem Tastendruck hält der Kern fest, welches Zeichen er zum Aufnahmezeitpunkt erzeugte – zusätzlich zu den Rohdaten (Tastencode, Scancode, Kennzeichen, Layout), mit denen sich die Übersetzung später wiederholen lässt.

1. **Layout:** das aktive Layout des Threads, dem das Vordergrundfenster aus dem Hook-Aufruf gehört. Ein Wechsel erzeugt `system` (`keyboardLayout`).
2. **Übersetzung:** `ToUnicodeEx` mit dem **eigenen** Tastenzustand und gesetztem Flag 0x4. Dieses Flag verhindert, dass der Tastaturzustand des Systems verändert wird *(belegt, ab Windows 10 1607)*. Ohne das Flag ändert der Aufruf den Tastaturpuffer des Systems und stört Tottasten *(belegt)*.
3. **Tottasten:** Weil der Systemzustand unberührt bleibt, setzt der Kern Tottaste und Folgetaste selbst zusammen: Akzentzeichen merken, mit dem nächsten Zeichen nach den Unicode-Regeln vereinigen; gibt es keine Vereinigung, entstehen Akzent und Zeichen getrennt, wie in Windows. Abweichungen exotischer Layouts von den Unicode-Regeln sind möglich *(unbestätigt → Z-10)* und werden über den Feldinhalt erkannt. *Kann:* die Tottasten-Tabelle direkt aus dem Layout lesen.
4. **AltGr:** Auf Layouts mit AltGr erzeugt Windows beim Druck der rechten Alt-Taste zusätzlich eine künstliche linke Strg-Taste. Der Kern erkennt dieses Paar, führt es als `altGr` und wertet das Ergebnis als Zeichen, nicht als Tastenkombination.
5. **Alt+Ziffernblock:** Die Ziffernfolge bei gehaltener Alt-Taste wird erkannt; beim Loslassen von Alt wird das entstandene Zeichen berechnet (mit führender Null nach der ANSI-, sonst nach der OEM-Zeichentabelle).
6. **Wiederholung:** Ein weiterer Druck einer bereits gedrückten Taste ist eine Wiederholung.
7. **Eingabemethoden (IME):** Der Hook sieht nur die getippten Grundtasten, nicht den entstehenden Text. Der Kern kennzeichnet Tasten bei aktiver Eingabemethode; der entstandene Text kommt über `ui.text` und den Feldinhalt ([7.6](#ui-automation)).
8. **Text ohne Tasten** (Einfügen, Autovervollständigung, Emoji-Auswahl, Spracheingabe, Bildschirmtastatur) erscheint ebenfalls nur über Feldinhalt, `ui.text` und die Herkunft der Zwischenablage.

Die Übersetzung geschieht im Hauptprozess, nie im Hook-Aufruf.

### 3.7 Maus: Bewegung, Tasten, Rad, Zeigerart

- **Bewegung:** Jeder Messwert des Hooks wird gespeichert – ungefiltert, auch bei Mäusen mit hoher Abtastrate. Die Bündelung folgt [04](04-datenformat.md#mausbewegungen). Ausgedünnt wird nur im Notfall eines fast vollen Eingabe-Rings, und dann als Lücke vermerkt ([3.4](#kanalabgleich)).
- **Position:** Der Hook meldet die Zielposition vor dem Begrenzen auf den Bildschirm. Der Kern begrenzt sie auf die Fläche des virtuellen Desktops der Epoche und rechnet sie in monitorrelative Koordinaten um ([4.13](#topologie)).
- **Tasten:** Protokolliert werden logische und physische Taste. Ob Hook und Raw Input bei vertauschten Maustasten die physische oder die logische Taste melden, wird geprüft *(unbestätigt → Z-2)*; die Einstellung selbst steht in der Sitzung.
- **Rad:** Rohwert und Achse. Touchpads und hochauflösende Räder liefern kleinere Werte als eine Raste.
- **Zeigerart:** Aus Touch oder Stift erzeugte Mausereignisse tragen eine Signatur in der Zusatzinformation und werden als `touch` bzw. `pen` gekennzeichnet. Ihr Vorher-Bild hat höchstens die Stufe `atInput`, weil nicht gesichert ist, dass der Hook vor der Anwendung aufgerufen wird *(unbestätigt → Z-9)*.
- **Nicht erfassbar:** Mehrfinger-Gesten auf Touchpad und Touchscreen erzeugen keine oder nur abgeleitete Mausereignisse. Ihre Wirkung (Fensterwechsel, Scrollen, Zoom) wird über Fensterereignisse und Bilder sichtbar; wo der Kern eine solche Eingabe erkennt, schreibt er eine Lücke `unsupportedInput`.

### 3.8 Künstlich erzeugte Eingaben

Der Hook kennzeichnet Eingaben, die ein Programm erzeugt hat, und solche aus Prozessen niedrigerer Integrität. Der Kern protokolliert sie wie alle anderen, mit Kennzeichen. Er filtert sie nicht: Texterweiterer, Fernwartung, Bildschirmtastatur und Automatisierungswerkzeuge gehören zum Geschehen. Wie die Schutzstufe „Streng" damit umgeht, regelt [10](10-datenschutz-und-sicherheit.md). Der Recorder selbst erzeugt nie Eingaben.

<a id="eigene-bedienung"></a>
### 3.9 Eigene Bedienung des Recorders

Die Hooks sehen auch, was der Nutzer am Recorder selbst tut. Damit daraus keine Nutzerschritte werden:

| Fall | Behandlung |
|---|---|
| Hotkey des Recorders | Die Tasten bleiben als `key.*` im Protokoll, gekennzeichnet mit `self`. Die Eingabeverarbeitung kennt die belegten Kombinationen und kennzeichnet auch die zugehörigen Umschalter. |
| Mausklick oder Mausrad auf einem Fenster des Recorders | Wird **nicht** als Eingabeereignis geschrieben; die Zeitspanne steht als Lücke `recorderInteraction`. Mausbewegungen bleiben im Protokoll (nur Koordinaten). Kein Element, kein Bildauslöser. |
| Tasten in einem Textfeld des Recorders (Kommentar) | Werden **nicht** als `key.*` geschrieben. Der Text steht im `comment`; die Dauer deckt eine Lücke `recorderInteraction` ab. |
| Fenster- und Bereichswahl, Bereichsmenü, Rückfragen | Lücke `recorderInteraction` für die Dauer; Eingaben darin werden nicht geschrieben. |
| Fenster des Recorders selbst | Erscheinen sie in Fensterereignissen, tragen diese `self`. |

„Fenster des Recorders" ist jedes Fenster, das dem Hauptprozess gehört; geprüft wird am Vordergrundfenster und am Fenster unter dem Zeiger aus dem Hook-Aufruf.

**Bild einfrieren.** Kommentar, Schnappschuss und Marker lösen als Erstes – noch bevor irgendein Fenster des Recorders erscheint – einen Bildauslöser mit dem Zeitstempel des Hotkeys aus ([4.4](#vorher-bild)). Das Kommentarfeld zeigt danach dieses eingefrorene Bild. Ein offenes Menü der dokumentierten Anwendung ist damit festgehalten, auch wenn es sich beim Öffnen des Kommentarfelds schließt.

**Marker mit Typ.** Ein Marker trägt einen von vier Typen (`marker.kind`, [04](04-datenformat.md#ev-marker)): `note` (Standard), `error` („Hier tritt der Fehler auf"), `expected` („So sollte es aussehen") und `important`. Jeder Typ hat eine eigene Kürzel-Funktion; welche davon vorbelegt sind, steht in [07](07-aufnahme-bedienung.md). Technisch verhalten sich alle gleich:

1. **Keine Oberfläche.** Kein Fenster, kein Fokuswechsel, kein Eingriff außer dem registrierten Kürzel selbst.
2. **Kein Nutzerschritt.** Die Tasten des Kürzels stehen als `key.*` mit `self` im Protokoll. Weder aus ihnen noch aus dem Marker wird ein Nutzerschritt; ein Element wird dafür nicht ermittelt.
3. **Zeitpunkt und Bild.** Zeitpunkt des Markers ist der Hook-Zeitstempel der auslösenden Taste. Für diesen Zeitpunkt wird das Bild eingefroren – je Monitor im Aufnahmebereich, mit der Vorher-Bild-Garantie aus [4.4](#vorher-bild) –, und der Marker verweist darauf (`frame`; für `error`, `expected` und `important` ist der Verweis Pflicht). Hat der Hook die Taste nicht gesehen (Lücke), gilt der Moment, in dem das Kürzel den Hauptprozess erreicht; das Bild trägt dann höchstens `atInput`.
4. **Wortlaut bei `error` und `expected`.** Zusätzlich entsteht ein Text-Schnappschuss des Vordergrundfensters ([7.5](#text-schnappschuss)). Der Text einer Fehlermeldung steht damit als Text im Protokoll, nicht nur im Bild. Liegt das Vordergrundfenster außerhalb des Aufnahmebereichs oder gehört es zu einer ausgeschlossenen Anwendung, entfällt der Schnappschuss.
5. **Über die Aufnahmeleiste.** Wird ein Marker dort gesetzt, gelten die Regeln für Klicks auf Recorder-Fenster: kein Eingabeereignis, Lücke `recorderInteraction` für die Dauer. Zeitpunkt des Markers ist der erste Klick – auch wenn danach noch ein Typ aus dem Menü gewählt wird. Ein offenes Menü der dokumentierten Anwendung kann dieser Klick schließen; störungsfrei ist nur das Kürzel.
6. **Nicht in der Pause.** In der Pause gibt es weder Hook-Zeitstempel noch Bild; Marker werden dann nicht angenommen.
7. **Angabe des Autors.** Der Typ sagt, was der Autor meint. Der Kern prüft nicht, ob am Bildschirm ein Fehler zu sehen ist, und leitet aus dem Typ nichts ab.

Jedes zusätzliche Kürzel ist eine weitere Tastenkombination, die die Zielanwendung während der Aufnahme nicht erreicht (siehe unten).

Technische Randbedingungen für die Bedienung ([07](07-aufnahme-bedienung.md)):

- Hotkeys laufen über `RegisterHotKey` im Hauptprozess, nicht über die Hooks.
- **Registrierte Hotkeys erreichen die Zielanwendung nicht.** Windows liefert die auslösende Taste einer registrierten Kombination nur an den Recorder. Das ist die einzige Stelle, an der der Recorder in die Eingabe eingreift – die ausdrückliche Ausnahme von Grundsatz 2. Die Hooks selbst reichen weiterhin jede Eingabe unverändert weiter.
- Das Kommentarfeld nimmt den Fokus; eine Kommentareingabe ohne Fokuswechsel gibt es nicht.
- Die Alt-Taste schließt Win32-Menüs. Standard-Hotkeys enthalten deshalb kein Alt.
- Auch ein Klick auf eine fokuslose Leiste schließt viele Menüs und Flyouts, weil sie auf jeden Klick außerhalb reagieren. Wirklich störungsfrei sind nur Hotkeys.

### 3.10 Momentaufnahme der Eingabe-Einstellungen

Zum Sitzungskopf liest der Kern die Einstellungen, die die Deutung der Rohereignisse beeinflussen: vertauschte Maustasten, Doppelklickzeit und -rechteck, Ziehschwelle, Radzeilen, Anschlagverzögerung und Wiederholrate, Verweilzeit, Einrastfunktion und verwandte Hilfen, Blinkzeit der Einfügemarke, installierte Tastaturlayouts sowie `LowLevelHooksTimeout`. Die Feldliste steht in [04, Abschnitt 4.2](04-datenformat.md#manifest). Ändert sich eine Einstellung während der Aufnahme, folgt `system` (`inputSettings`).

### 3.11 Erinnerungsuhr

Der Kern führt eine reine Uhr (`ReminderClock`, ohne Oberfläche und ohne Systemzugriff, damit testbar), die der Oberfläche meldet, wann sie den Autor erinnern soll. Sie **erinnert nur** – sie pausiert, maskiert oder beendet nichts und schreibt nichts ins Protokoll.

| Erinnerung | Auslöser | Wiederholung |
|---|---|---|
| „Aufnahme läuft noch“ | N Minuten (aus, 5, 10 oder 30; Standard 10) ohne **jede** Eingabe – auch ohne Mausbewegung und auch außerhalb des Aufnahmebereichs | im selben Abstand, höchstens dreimal je Leerlaufphase; jede Eingabe beginnt eine neue Phase |
| „Privattaste ist noch an“ | S Sekunden (30, 60 oder 120; Standard 60) ohne Tastendruck, seit der Privatmodus der Privattaste an ist | einmal je Einschalten |

- **Nie in Pause, Lücke oder gesperrter Sitzung.** Solange die Aufnahme pausiert ist oder eine Lücke `sessionLocked`, `sessionDisconnected`, `secureDesktop` oder `suspend` läuft, stehen beide Uhren; nach dem Ende beginnen sie von vorn.
- Der Kern prüft die Uhr im 250-ms-Takt des Wächters und meldet der Oberfläche Art und Wert. Wie die Erinnerung gezeigt wird, steht in [07](07-aufnahme-bedienung.md#74-hinweise-und-warnungen).

---

<a id="bildschirmaufnahme"></a>
## 4 Bildschirmaufnahme

### 4.1 Ziele

1. Zu jeder Eingabe ein Bild, das **nachweislich vor** der Eingabe dargestellt wurde.
2. Zu jeder Eingabe ein Bild danach; dazu Bilder von Änderungen ohne Eingabe.
3. Verlustfreie Bilder in Monitorgröße, ohne Mauszeiger, ohne Recorder.
4. Kosten, die auch bei 4K und mehreren Monitoren tragbar sind.
5. Kein fehlendes Bild ohne Marker.

### 4.2 Aufnahmewege und Rückfallkette

Je Monitor wird in der Vorprüfung der erste funktionierende Weg gewählt: Desktop Duplication, sonst Windows.Graphics.Capture, sonst GDI. Der Weg steht in `display.topology`; ein Wechsel während der Aufnahme erzeugt `system` (`captureBackend`) und ein neues Schlüsselbild.

| Eigenschaft | Desktop Duplication (`dda`) | Windows.Graphics.Capture (`wgc`) | GDI (`gdi`) |
|---|---|---|---|
| Rolle | Hauptweg | erster Rückfall | letzter Rückfall |
| Darstellungszeit je Bild | ja, als Zählerstand *(belegt)* | ja *(unbestätigt → Z-3)* | nein |
| Geänderte und verschobene Bereiche | ja *(belegt)* | nicht vorausgesetzt | nein |
| Abruf | bei Änderung | bei Änderung | zyklisch |
| Mauszeiger | getrennt geliefert oder schon im Bild *(belegt)* | abschaltbar ab Windows 10 2004 *(belegt)* | nicht im Bild |
| Monitoraufnahme ohne Auswahldialog | ja | ab Windows 10 1903 *(belegt)*; Nutzbarkeit ohne Paketidentität *(unbestätigt → Z-3)* | ja |
| Sichtbarer Rahmen des Systems | keiner | gelber Rahmen; abschaltbar ab Build 20348, dokumentiert nur mit Zustimmung und Paketfähigkeit *(belegt)*, ohne Paketidentität *(unbestätigt → Z-3)* | keiner |
| Sicherer Desktop | nicht aufnehmbar *(belegt)* | nicht aufnehmbar | nicht aufnehmbar |
| Geschützte Inhalte | geschwärzt, mit Kennzeichen *(belegt)* | geschwärzt, ohne Kennzeichen | geschwärzt |
| Grenzen | höchstens 4 duplizierende Prozesse je Sitzung; Hybrid-Grafik; 8-Bit-Modi *(belegt)*; Remote-Sitzungen *(unbestätigt → VE-10)* | – | langsam |

Ein gelber Systemrahmen des Rückfallwegs ist am Bildschirm sichtbar; ob er im aufgenommenen Bild erscheint, klärt Z-3. Wäre er im Bild, gilt der Weg für die Unsichtbarkeitsgarantie nicht als bestanden – er umrandet zwar nicht den Recorder, verfälscht aber den Inhalt – und es folgt GDI.

### 4.3 Desktop Duplication im Einzelnen

- **Ein Gerät und ein Thread je Monitor.** Das Direct3D-Gerät wird auf **dem Adapter** erzeugt, an dem der Monitor hängt ([4.11](#bildschirmaufnahme)).
- **Schleife:** Bild abrufen (`AcquireNextFrame`), Metadaten lesen, Bild in den Bildring kopieren (im Grafikspeicher), Bild sofort freigeben. Die Freigabe geschieht so früh wie möglich, damit das System Änderungen weiter sammeln kann.
- **Nur-Zeiger-Meldungen** (Darstellungszeit 0) erzeugen kein Bild; sie aktualisieren die Zeigerdaten.
- **Bildrate:** höchstens 30 Abrufe je Sekunde und Monitor; dazwischen sammelt das System geänderte Bereiche. Ein Pin weckt den Thread sofort.
- **Bildring:** 4 Bilder je Monitor im Grafikspeicher (1080p: 33 MB, 4K: 133 MB), bei knappem Grafikspeicher 3.
- **Geänderte Bereiche** werden je Abruf gelesen und bis zum nächsten gespeicherten Bild vereinigt; verschobene Bereiche zählen als geändert.

<a id="vorher-bild"></a>
### 4.4 Vorher-Bild-Garantie

**Definition.** Zu jeder auslösenden Eingabe E mit Hook-Zeitstempel T ist das Vorher-Bild ein Bild, dessen Darstellungszeit **kleiner als T** ist.

**Warum das genügt.** T wird im Hook-Aufruf gelesen, also bevor Windows die Eingabe an die Anwendung weitergibt. Jede Reaktion der Anwendung wird nach T gezeichnet und dargestellt. Ein Bild mit Darstellungszeit vor T kann die Reaktion nicht enthalten. Beide Zeiten stammen aus demselben Zähler.

**Auslöser** – technisch bestimmt, unabhängig von den Schrittregeln:

| Auslöser | `frame.trigger` |
|---|---|
| Jede Maustaste, gedrückt und losgelassen | `preInput` |
| Erstes Radereignis nach mindestens 200 ms ohne Rad | `preInput` |
| Jeder Tastendruck, der keine Wiederholung ist. Beim zusammenhängenden Tippen (druckbare Tasten im Abstand unter 500 ms im selben Fenster) höchstens eines je 250 ms (`capture.typingFrameMinGapMs`); Eingabe-, Tabulator-, Escape-, Funktions- und Steuertasten sowie Tastenkombinationen lösen immer aus | `preInput` |
| Schnappschuss-Taste; Marker jedes Typs ([3.9](#eigene-bedienung)) | `manual` |
| Kommentar | `comment` |
| Wechsel des Aufnahmebereichs, neue Topologie-Epoche, Fortsetzen, Sitzungsbeginn, Stopp | `scope`, `topology`, `resume`, `sessionStart`, `sessionStop` |

Jeder Auslöser erzeugt je Monitor im Aufnahmebereich genau ein `frame`-Ereignis – auch wenn sich nichts geändert hat; dann verweist es auf ein vorhandenes Bild und kostet keinen Speicher ([4.6](#bildspeicher)).

**Ablauf je Monitor.** Der Hook-Aufruf schreibt T und die zuletzt veröffentlichten Bildnummern in die Pin-Tafel. Der Capture-Thread wertet neue Pins aus, **bevor** er ein Bild im Ring überschreibt:

1. Sofort ein Bild abrufen, ohne zu warten.
2. Liefert der Abruf ein Bild mit Darstellungszeit vor T, ist es der Kandidat (frischester Stand vor der Eingabe).
3. Liefert er ein Bild mit Darstellungszeit ab T oder keines, ist der Kandidat das jüngste Bild im Ring mit Darstellungszeit vor T.
4. Der Kandidat wird festgehalten (nicht überschreibbar), bis er ausgelesen ist.
5. Gibt es keinen Kandidaten, fehlt das Vorher-Bild: `frame` mit Ablage `none` und Lücke `frameDropped`.

**Halteregel.** Nur der Capture-Thread überschreibt Bilder im Ring, und er prüft die Pin-Tafel vor jedem Überschreiben. Ein langsamer Verbraucher kann ein Vorher-Bild deshalb nicht verdrängen – auch nicht, wenn im Aufnahmebereich ein Video läuft. Die Bildnummern in der Pin-Tafel geben zusätzlich eine harte Untergrenze: Kein Bild ab dieser Nummer wird überschrieben, solange der Pin offen ist.

**Garantiestufen** ([04](04-datenformat.md#ev-frame)):

| Stufe | Bedingung |
|---|---|
| `beforeInput` | Darstellungszeit (oder beim GDI-Weg: Abschluss der Kopie) liegt nachweislich vor T, und T stammt aus dem Hook-Aufruf |
| `atInput` | Bild wurde zum Eingabezeitpunkt geholt, ohne Nachweis: Eingabe aus Raw Input ergänzt; Eingabe von Touch oder Stift; GDI-Weg ohne rechtzeitig abgeschlossenes zyklisches Bild; Ausweichmodus nach einer Änderung ([6.4](#unsichtbarkeit)) |
| `none` | kein Bild |

**Alter.** Das Vorher-Bild kann älter sein als die Eingabe – beim ruhigen Bildschirm um Sekunden, sonst höchstens um ein Abrufintervall (33 ms). Die Darstellungszeit steht als `t` am Bild.

<a id="nachher-bild"></a>
### 4.5 Nachher-Bilder und Zwischenbilder

**Beruhigung.** Ein Monitor gilt als ruhig, wenn `capture.settleQuietMs` (Standard 300 ms) lang keine wesentliche Änderung eintrat. Wesentlich ist eine Änderung, wenn in einem Abruf mindestens 0,1 % der Monitorfläche geändert wurde oder ein Fenster der obersten Ebene erschien oder verschwand. Die blinkende Einfügemarke und kleine Fortschrittsanzeigen liegen darunter.

**Dauerbewegte Bereiche.** Ändert sich ein Bereich über 2 s in mindestens 80 % der Abrufe (Video, Animation), wird er von der Beruhigungsprüfung ausgenommen, bis er 1 s lang ruht. So kommt der Rest des Bildschirms trotz Video zur Ruhe.

**Nachher-Bild** (`settle`): das erste ruhige Bild nach der Eingabe, spätestens nach `capture.settleMaxMs` (Standard 3 s) und **spätestens das Vorher-Bild der nächsten Eingabe**.

| `settle.reason` | Bedeutung |
|---|---|
| `quiet` | Bildschirm kam zur Ruhe |
| `timeout` | Obergrenze erreicht; das Bild zeigt den Stand zu diesem Zeitpunkt |
| `nextInput` | Die nächste Eingabe kam vorher; ihr Vorher-Bild dient zugleich als Nachher-Bild |

Jede Eingabe hat damit ein Nachher-Bild, auch wenn Ladeanzeigen nie zur Ruhe kommen.

**Zwischenbilder** (`change`) entstehen ohne Eingabe:

- nach jeder Beruhigung, die auf eine wesentliche Änderung folgt (Ladevorgang beendet, Dialog erschienen);
- sofort, wenn ein Fenster der obersten Ebene erscheint – nach einer Kurzberuhigung von 150 ms, damit auch kurzlebige Dialoge und Menüs ein Bild bekommen;
- bei Daueränderung höchstens alle 5 s.

Die Einstellung „Zwischenbilder" aus [07](07-aufnahme-bedienung.md) wählt die Schwellen:

| Stufe | Wesentliche Änderung ab | Höchstens |
|---|---|---|
| wenige | 2 % der Fläche oder neues Fenster | 1 Bild je 2 s |
| normal | 0,1 % der Fläche oder neues Fenster | 2 Bilder je Sekunde |
| viele | jede Beruhigung | 4 Bilder je Sekunde |

Zeitgesteuerte Bilder (`periodic`) sind standardmäßig aus.

<a id="bildspeicher"></a>
### 4.6 Bildspeicher: Erzeugung von Schlüssel- und Differenzbildern

Ablage, Dateinamen und Felder legt [04, Bildspeicher](04-datenformat.md#bildspeicher) fest. Der Kern erzeugt sie so:

1. **Bezugsbild.** Je Monitor hält der Kern das zuletzt gespeicherte Bild im Arbeitsspeicher – so, wie es gespeichert wurde, also einschließlich aller schwarz gefüllten Flächen.
2. **Auslesen.** Aus dem Grafikspeicher wird nur der Bereich gelesen, der sich laut den gesammelten Änderungsmeldungen seit dem Bezugsbild geändert haben kann. Bei den Rückfallwegen wird das ganze Bild gelesen.
3. **Warten auf den Schutzfilter.** Ein Bild wird erst gefiltert und kodiert, wenn für seine Darstellungszeit feststeht, welche Flächen zu füllen sind – vor allem, welche Passwortfelder sichtbar waren. Bis dahin liegt es nur im Arbeitsspeicher, höchstens so lange wie die Rückhaltung der Tasten (`T_max` in [10](10-datenschutz-und-sicherheit.md), Standard 1,5 s). Fällt bis dahin keine Entscheidung, gilt die strengere Maske.
4. **Filter vor dem Vergleich.** Aufnahmebereich ([5](#aufnahmebereiche)) und Bildfilter des Datenschutzes ([11](#andockpunkte-datenschutz)) füllen ihre Flächen schwarz und weisen sie im Ereignis aus ([04, Abschnitt 11.5](04-datenformat.md#bildspeicher)). Der Vergleich und alles Weitere arbeiten nur noch auf dem gefilterten Bild; ungefilterte Pixel erreichen weder Kodierer noch Datenträger.
5. **Exakter Vergleich** mit dem Bezugsbild, pixelgenau.
6. **Ablage wählen:**
   - kein Pixel geändert → `same` (Verweis, kein Blob);
   - geänderter Anteil ab `capture.keyframeChangeRatio` (Standard 0,4), Kette sonst länger als `capture.maxDeltaChain` (Standard 30), neuer Abschnitt, Fortsetzen, neue Epoche, neuer Aufnahmeweg → `key`;
   - sonst → `delta`: das umschließende Rechteck aller geänderten Pixel; unveränderte Pixel darin sind durchsichtig.
7. **Kodieren** als PNG, verlustfrei und deterministisch; Blob schreiben; **danach** das `frame`-Ereignis ([02](02-architektur.md#schreiben)). Das Ereignis zu einem Bild steht nie vor dem Ereignis, auf das es sich bezieht.
8. Das Bezugsbild wird fortgeschrieben; `pixelHash` hält den Stand zur Prüfung fest.

Schwellen entscheiden nur, **wann** ein Zwischen- oder Nachher-Bild ausgelöst wird – nie, ob ein ausgelöstes Bild gespeichert wird.

### 4.7 Mauszeiger

- Gespeicherte Bilder enthalten den Zeiger **nicht**. Seine Position kommt aus den Hook-Ereignissen, seine Form aus der Bildaufnahme.
- Jede neue Form wird einmal mit aktivem Punkt abgelegt; ein Formwechsel erzeugt `mouse.cursor`. Standardformen (Pfeil, Textmarke, Hand, Warten …) werden erkannt und benannt – die Wartemarke zeigt eine beschäftigte Anwendung, die Hand einen Link.
- Meldet der Aufnahmeweg, dass der Zeiger bereits ins Bild gezeichnet ist (Software-Zeiger), trägt das Bild `cursor.baked`.
- Bei den Rückfallwegen wird die Form zehnmal je Sekunde und bei jeder Eingabe abgefragt.

<a id="ueberlast"></a>
### 4.8 Überlast

Auslesen und Kodieren laufen über begrenzte Warteschlangen (zusammen höchstens 512 MB unkodierte Bilddaten, einschließlich der Bilder, die auf den Schutzfilter warten). Staut es sich, wird in dieser Reihenfolge abgebaut:

1. zeitgesteuerte Bilder entfallen;
2. Zwischenbilder werden zusammengelegt (nur das jüngste bleibt);
3. die Kompressionsstufe sinkt (größere Dateien, weniger Rechenzeit);
4. Nachher-Bilder werden auf das nächste Vorher-Bild gelegt (`nextInput`);
5. erst zuletzt entfällt ein Vorher-Bild: Ablage `none`, Lücke `frameDropped` mit Verweis auf die Eingabe.

Jeder Abbau wird gezählt; die Zähler stehen in der Sitzung.

### 4.9 Störungen der Aufnahme

| Signal | Bedeutung | Reaktion |
|---|---|---|
| Zeitüberschreitung beim Abruf | keine Änderung | normal |
| Zugriff verloren (`DXGI_ERROR_ACCESS_LOST`) | Desktopwechsel, Moduswechsel, Neustart der Fensterverwaltung, Vollbildwechsel *(belegt)* | Duplizierung neu aufbauen (sofort, dann 100, 250, 500 ms, höchstens alle 2 s); Topologie neu lesen; erstes Bild danach ist ein Schlüsselbild. Fallen Eingaben in die Zeit ohne Bild: Lücke `captureLost` bzw. `secureDesktop` |
| Zugriff verweigert beim Aufbau | sicherer Desktop aktiv *(belegt)* | Lücke `secureDesktop`; warten auf die Rückkehr ([8.3](#fensterereignisse)) |
| Nicht unterstützt | Hybrid-Grafik oder ungeeigneter Modus *(belegt)* | nächster Aufnahmeweg |
| Höchstzahl duplizierender Prozesse erreicht | andere Aufnahmeprogramme belegen die vier Plätze | nächster Aufnahmeweg |
| Sitzung getrennt | Remote-Verbindung beendet | Lücke `sessionDisconnected`; nach Wiederverbinden neu aufbauen |
| Kennzeichen „geschützte Inhalte ausgeblendet" | Teile des Bilds sind schwarz | Bild trägt `protected`; Lücke `protectedContent` für die Dauer |
| Dreimal in 5 s kein Neuaufbau möglich | Weg dauerhaft gestört | nächster Aufnahmeweg; der Selbsttest der Unsichtbarkeit läuft für den neuen Weg erneut |

### 4.10 HDR

- Version 1 fordert Bilder mit 8 Bit je Kanal an; Windows wandelt HDR-Inhalte dafür um *(belegt)*. Betroffene Bilder tragen `hdr`; die Topologie nennt den HDR-Zustand des Monitors.
- Die Wandlung kann Farben und Helligkeit anders wiedergeben, als der Nutzer sie sah *(unbestätigt → Z-12)*. Das ist ein bekannter Mangel der Version 1 und steht in der Testmatrix.
- *Soll, spätere Stufe:* Aufnahme im Gleitkommaformat und eigene Tonwertabbildung auf den SDR-Weißpegel des Monitors, damit gewöhnliche Fensterinhalte farbtreu bleiben; Spitzlichter oberhalb des Weißpegels werden abgeschnitten.

### 4.11 Hybrid-Grafik und Drehung

- **Hybrid-Grafik:** Das Gerät wird auf dem Adapter erzeugt, dem der Monitor zugeordnet ist. Der Recorder fordert nie die leistungsstarke Grafikkarte an. Schlägt die Duplizierung dennoch mit „nicht unterstützt" fehl – auf Hybrid-Systemen so vorgesehen, wenn der Prozess der dedizierten Karte zugeordnet ist *(belegt)* –, folgt der nächste Aufnahmeweg, und die Aufnahmeleiste nennt die Abhilfe (Grafikeinstellung „Energie sparen" für das Programm). Der Recorder ändert diese Einstellung nicht selbst.
- **Drehung:** Die Duplizierung liefert die Fläche in Geräteausrichtung samt Drehangabe. Der Kern dreht Bild und Bereichsangaben in die Darstellungsrichtung. Gespeicherte Bilder haben immer die Maße des Monitors, wie der Nutzer ihn sieht.

### 4.12 Rückfallwege

**Windows.Graphics.Capture:** Aufnahme je Monitor ohne Auswahldialog; Zeiger abgeschaltet; rahmenlos, wo das System es erlaubt. Bilder treffen bei Änderung ein. Geänderte Bereiche werden durch Vergleich des ganzen Bilds bestimmt. Für die Garantie gilt die Systemzeit des Bilds; ist deren Bedeutung nicht bestätigt, zählt nur ein Bild, das vor T eingetroffen ist.

**GDI:** zyklische Kopie des Monitors, 5 Bilder je Sekunde (4K: 3), mit Vergleich des ganzen Bilds. Vorher-Bild ist das letzte zyklische Bild, dessen Kopie vor T abgeschlossen war (`beforeInput`, Alter bis zu einem Zyklus). Fehlt ein solches Bild oder ist es älter als 1 s, wird sofort kopiert (`atInput`).

<a id="topologie"></a>
### 4.13 Topologie und Koordinaten

Das Koordinatenmodell steht in [04, Koordinatenmodell](04-datenformat.md#koordinatenmodell). Der Kern stellt sicher:

1. **Stabile Monitor-Kennung.** Jeder Monitor erhält beim ersten Auftreten `M1`, `M2` … Die Zuordnung hängt am Gerät (Gerätepfad der Anzeige), nicht an der Windows-Nummer und nicht am Monitor-Handle. Gespeichert wird nur ein gesalzener Hash des Gerätepfads, keine Seriennummer.
2. **Epochen.** Bei Start und bei jeder Änderung (Monitor an/ab, Auflösung, Drehung, Skalierung, Hauptmonitor) schreibt der Kern `display.topology` mit neuer Epoche. Erkannt wird die Änderung über Anzeigenachrichten **und** über den Verlust der Duplizierung. Die Aufnahme läuft weiter; das erste Bild der Epoche ist ein Schlüsselbild.
3. **Umrechnung zum Zeitpunkt des Geschehens.** Ein Punkt wird über die Monitorrechtecke der Epoche einem Monitor zugeordnet, die zu seinem `t` galt – nicht über eine spätere Abfrage des Systems.
4. **Unsicherheit beim Wechsel.** Die Meldung über eine neue Anordnung trifft etwas nach der Änderung ein. Eingaben innerhalb von 100 ms um einen Epochenwechsel werden gezählt (Leistungszähler); fallen Eingaben in die Zeit ohne Bild, entsteht `captureLost`.
5. **Physische Pixel überall.** Beide Prozesse sind Per-Monitor-V2. Dass Hook-, Raw-Input-, UIA- und Bildkoordinaten bei gemischter Skalierung auf ±1 Pixel übereinstimmen, prüft VE-06.
6. **Plausibilität von Elementrechtecken.** Ein Rechteck, das den auslösenden Punkt nicht enthält, leer ist oder erkennbar skaliert gemeldet wurde (Anwendungen ohne DPI-Unterstützung, manche Qt- und Java-Programme), wird nicht als Rechteck gespeichert, sondern mit Grund vermerkt ([04, Abschnitt 8.4](04-datenformat.md#koordinatenmodell)).
7. **Epochen über Aufnahmeabschnitte.** Ein weiterer Aufnahmeabschnitt beginnt immer mit einer neuen Epoche (Grund `partStart`), auch bei unveränderter Anordnung; die Epochennummern laufen sitzungsweit fort. Koordinaten und Bildketten eines Abschnitts beziehen sich nie auf eine Epoche eines früheren. Monitore werden über ihren gesalzenen Gerätehash wiedererkannt und behalten dann ihre Kennung; dafür muss das Salz der Sitzung für spätere Abschnitte lesbar sein. Es ist kein Geheimnis – es verhindert nur, dass sich Monitore über verschiedene Sitzungen hinweg wiedererkennen lassen ([1.7](#weitere-abschnitte)).

### 4.14 Bildaufnahme aussetzen (Suspend und Resume)

Auf dem sicheren Desktop, bei gesperrtem Rechner und im Ruhezustand liefert die Bildaufnahme keine verlässlichen Bilder. Der Bildspeicher (`FrameEngine`) hat dafür einen eigenen Zustand „ausgesetzt“, der zusätzlich zur Pause gilt ([10, 2.1](10-datenschutz-und-sicherheit.md#21-bildschutz-bei-sperre-ruhezustand-und-sicherem-desktop)):

1. **Suspend.** Der Kern setzt die Aufnahme aus, sobald eine der Lücken `secureDesktop`, `sessionLocked`, `sessionDisconnected` oder `suspend` beginnt. Der Ringpuffer wird verworfen (er könnte schwarze oder halbe Bilder enthalten), ein offenes Nachher-Bild entfällt, und es werden keine Bilder mehr aufgenommen.
2. **Kein Bild aus Lücken.** Ein Bildwunsch im ausgesetzten Zustand – Vorher-Bild einer Eingabe, Nachher- oder Zwischenbild – wird nicht erfüllt, sondern als Bildereignis ohne gespeichertes Bild (`store: none`, Grund `secureDesktop`) vermerkt; das Vorher-Bild gilt dann als nicht garantiert und zählt in den Statistiken. Eine fehlgeschlagene Aufnahme (zum Beispiel weil der Desktop wechselt) liefert kein Bild und kommt nie als schwarzes Bild in den Ringpuffer.
3. **Resume.** Mit dem Ende der Lücke wird die Aufnahme wieder freigegeben. Nach dem sicheren Desktop erzwingt der Kern sofort ein Schlüsselbild; nach dem Ruhezustand ebenfalls, solange die Sitzung nicht noch gesperrt oder getrennt ist. Nach Sperre und Trennen folgt es erst mit dem bestätigten Fortsetzen ([8.3](#fensterereignisse)).
4. Pause und Aussetzen sind getrennte Zustände: Eine Pause hält auch Eingaben an, das Aussetzen betrifft nur die Bilder.

---

<a id="aufnahmebereiche"></a>
## 5 Aufnahmebereiche

Was der Nutzer wählt, steht in [07](07-aufnahme-bedienung.md); die Felder von `scope.change` in [04](04-datenformat.md#ev-scope-change). Hier steht, wie der Kern den Bereich durchsetzt.

### 5.1 Grundregeln

1. **Der Bereich gilt für Bilder und Eingaben gleichermaßen.**
2. **Entfernt wird vor dem Speichern.** Pixel außerhalb des Bereichs erreichen weder Kodierer noch Datenträger ([4.6](#bildspeicher), Schritt 4).
3. **Die Leinwand bleibt monitorgroß.** Auch bei eingeschränktem Bereich hat jedes Bild die Maße des Monitors; alles außerhalb ist schwarz gefüllt und als Auslassung ausgewiesen. So gilt überall: Bildpixel = monitorrelative Koordinate, und Differenzbilder funktionieren unverändert.
4. **Im Zweifel draußen.** Ist unklar, ob ein Fenster oder ein Pixel zum Bereich gehört, wird es nicht gespeichert.

### 5.2 Wann eine Eingabe im Bereich liegt

| Bereich | Mauseingabe im Bereich, wenn … | Tastatureingabe im Bereich, wenn … |
|---|---|---|
| Ganzer Bildschirm, Monitor mit Aktivität oder alle | immer | immer |
| Ganzer Bildschirm, bestimmter Monitor | der Punkt auf diesem Monitor liegt | das Vordergrundfenster überwiegend auf diesem Monitor liegt |
| Eine Anwendung | das Fenster unter dem Zeiger zum Bereich gehört ([5.4](#aufnahmebereiche)) | das Vordergrundfenster zum Bereich gehört |
| Bereich (Rechteck) | der Punkt im Rechteck liegt | das Rechteck des fokussierten Elements (ersatzweise der Einfügemarke) das Rechteck schneidet; ist beides unbekannt: das Vordergrundfenster zu mindestens der Hälfte im Rechteck liegt |

Maßgeblich sind Vordergrundfenster und Fenster unter dem Zeiger **aus dem Hook-Aufruf**.

Eingaben außerhalb werden **nie aufgezeichnet, nur gezählt**. Der Aufnahmebereich ist auch eine Datenschutzgrenze; eine Option, außerhalb mitzuprotokollieren, gibt es nicht.

- Tasten, Maustasten, Mausrad **und Mausbewegungen** außerhalb des Bereichs erreichen weder das Rohprotokoll noch einen Zwischenspeicher. Für die Dauer steht eine Lücke `outOfScope`; ihr Ende nennt die Zahl der nicht aufgezeichneten Ereignisse je Art (Tasten, Klicks, Mausrad, Bewegungen) – nur Zahlen, keine Inhalte, Tastencodes, Positionen oder Einzelzeiten ([04](04-datenformat.md#ev-gap)). Gezählt wird im Arbeitsspeicher.
- Für Mausbewegungen entscheidet die Lage des Punkts: bei „Eine Anwendung" gegenüber der sichtbaren Fläche der Fenster im Bereich (dieselbe Fensterlage wie für die Bilder, [5.5](#aufnahmebereiche)), sonst gegenüber dem Monitor bzw. dem Rechteck.
- Für Fenster außerhalb gibt es keine Element-Ermittlung, keinen Text-Schnappschuss, keine Titel und keine Bilder.
- Liegt außerhalb des Bereichs ein Fenster einer ausgeschlossenen Anwendung im Vordergrund, gilt die strengere Regel: Lücke `excludedApp`, keine Zähler, keine Eingabenummern ([2.4](#zeitmodell)).

### 5.3 Ganzer Bildschirm

- **Monitor mit Aktivität:** Capture-Threads laufen für alle Monitore (nur Bildring im Grafikspeicher). Gespeichert wird je Auslöser der Monitor, auf dem die Eingabe stattfand (Maus: Monitor des Punkts; Tastatur: Monitor des Vordergrundfensters). Zwischenbilder entstehen nur für Monitore, auf denen in den letzten 10 s gearbeitet wurde. Ein Monitor, auf dem der Nutzer nichts tut, wird nicht gespeichert.
- **Bestimmter Monitor:** nur dieser; Eingaben auf anderen gelten als außerhalb. Wird der Monitor entfernt, pausiert die Aufnahme (`session.pause`, Grund `scopeLost`).
- **Alle Monitore:** jeder Auslöser gilt für jeden Monitor; unveränderte Monitore kosten dank Verweis nichts.

### 5.4 Eine Anwendung: welche Fenster dazugehören

Gewählt wird eine Anwendung (alle Fenster ihrer Prozesse) oder ein einzelnes Fenster samt seinen Dialogen und Menüs. Eine Anwendung ist über ihre Programmdatei bzw. ihre Anwendungskennung bestimmt, nicht über eine Prozessnummer: Wird sie beendet und neu gestartet, gehört sie wieder dazu.

Ein Fenster gehört zum Bereich, wenn die erste zutreffende Regel es einschließt:

| Rang | Regel | Beispiele |
|---|---|---|
| 1 | Das Fenster gehört einem Prozess der gewählten Anwendung oder einem seiner Kindprozesse | Hauptfenster, Dialoge, Menüs, Aufklapplisten, Tooltips |
| 2 | Die Besitzerkette des Fensters führt zu einem Fenster im Bereich | Dialog eines anderen Prozesses, der der Anwendung zugeordnet ist |
| 3 | Rahmenfenster einer gehosteten App, dessen Inhaltsfenster einer gewählten Anwendung gehört | Store-Apps im Rahmen von `ApplicationFrameHost` |
| 4 | Fenster eines bekannten Hilfsprozesses, das in den Vordergrund tritt, während ein Fenster im Bereich Vordergrund war (nur mit `includeRelated`, Standard an) | Dateiauswahl, „Öffnen mit", Druckdialog, Fenster von Eingabemethoden |
| – | alles andere | außerhalb |

Die Liste der Hilfsprozesse für Rang 4 ist vorbelegt und erweiterbar; die Prozessnamen werden in M1 an echten Systemen festgestellt. Bei „nur dieses Fenster" gelten Rang 1 nur für das gewählte Fenster und Rang 2 für seine Besitzerkette.

Die Entscheidung steht je Fenster in `window.info` (`inScope`). Fremde Fenster werden nur mit Kennung geführt – ohne Titel, ohne Klasse.

### 5.5 Eine Anwendung: Bilder

1. Aufgenommen wird weiterhin der ganze Monitor (nur so gilt die Vorher-Bild-Garantie). Vor dem Speichern bleibt nur die **sichtbare Fläche der Fenster im Bereich** stehen.
2. **Fensterlage zum Bildzeitpunkt.** Das Fensterregister führt für Fenster der obersten Ebene Lage, Reihenfolge, Sichtbarkeit und Verhüllung als Verlauf. Die sichtbare Fläche wird für die Darstellungszeit des Bilds berechnet. Maßgeblich sind die sichtbaren Fenstergrenzen ohne Schatten.
3. **Vorsicht bei Bewegung.** Hat sich die Fensterlage innerhalb von 50 ms um die Darstellungszeit geändert, bleibt nur stehen, was vorher **und** nachher zum Bereich gehörte.
4. **Verdeckung.** Wo ein fremdes Fenster ein Fenster im Bereich überdeckt, wird schwarz gefüllt und die Fläche als `occluded` in `frame.masks` vermerkt.
5. **Abgerundete Ecken.** In den Eckbereichen abgerundeter Fenster scheint der Hintergrund durch; diese Pixel werden mit entfernt *(Soll)*.
6. `frame.scopeRects` nennt die Flächen mit echtem Inhalt.

Menüs, Aufklapplisten und Tooltips der Anwendung sind eigene Fenster und über Rang 1 eingeschlossen, auch wenn sie über das Hauptfenster hinausragen.

*Kann, spätere Stufe:* verdeckte Fenster über eine fensterbezogene Aufnahme vollständig erfassen. Dafür gäbe es keine Vorher-Bild-Garantie; deshalb ist es nicht Teil von Version 1.

### 5.6 Bereich (Rechteck)

Das Rechteck ist monitorrelativ und fest; es folgt keinem Fenster. Bilder behalten Monitorgröße, außerhalb des Rechtecks ist schwarz gefüllt. Wird der Monitor entfernt oder seine Auflösung so geändert, dass das Rechteck nicht mehr ganz darauf liegt, pausiert die Aufnahme (`scopeLost`).

### 5.7 Wechsel während der Aufnahme

Ein Wechsel erzeugt `scope.change` mit der vollständigen neuen Festlegung und gilt ab der nächsten Eingabe. Capture-Threads für neu hinzukommende Monitore starten; das erste Bild nach dem Wechsel ist ein Schlüsselbild mit Auslöser `scope`. Die Bedienung der Auswahl ist eigene Bedienung ([3.9](#eigene-bedienung)).

Mit welchem Bereich ein weiterer Aufnahmeabschnitt einer bestehenden Sitzung beginnt und wie ein gespeicherter Bereich wiederhergestellt wird, steht in [1.7](#weitere-abschnitte).

### 5.8 Grenzen des Erfassungsumfangs

| Fläche | Verhalten |
|---|---|
| Benachrichtigungen (Toasts) | Bei „Ganzer Bildschirm" Teil des Bilds. Der Kern erkennt sie als Fensterart `toast` und kann sie über den Bildfilter schwarz füllen; ob das Standard ist, regelt [10](10-datenschutz-und-sicherheit.md). Bei „Eine Anwendung" liegen sie außerhalb. |
| Fensterumschalter, Aufgabenansicht, Vorschaubilder der Taskleiste | Zeigen verkleinerte Livebilder anderer Fenster. Einzelne Vorschaubilder lassen sich nicht gezielt entfernen. Steht eine laufende Anwendung auf der Ausschlussliste, füllt der Bildfilter das gesamte Umschalter-Fenster schwarz. |
| Zweiter Monitor mit fremdem Inhalt | Bei „Monitor mit Aktivität" nicht gespeichert, solange dort nicht gearbeitet wird. |
| Aufnahmeordner in einem synchronisierten Ordner | Kein Thema des Kerns; die Prüfung steht in [02](02-architektur.md#konfiguration), die Regel in [10](10-datenschutz-und-sicherheit.md). |

---

<a id="unsichtbarkeit"></a>
## 6 Unsichtbarkeitsgarantie

Die Zusage aus Nutzersicht steht in [07, Abschnitt 11](07-aufnahme-bedienung.md): In keinem Bild erscheint ein Teil des Recorders, und der Inhalt dahinter ist vollständig zu sehen.

### 6.1 Ausnahme auf Systemebene

- Jedes Fenster der obersten Ebene, das dem Hauptprozess gehört, erhält die Anzeigezuordnung `WDA_EXCLUDEFROMCAPTURE`. Das System lässt solche Fenster bei Bildschirmaufnahmen aus und zeigt, was dahinter liegt. Verfügbar ab Windows 10 2004, nur bei aktiver Fensterzusammensetzung *(belegt)* – beides ist auf den unterstützten Systemen gegeben.
- **Jedes** heißt: Leiste, Rahmen, Kommentarfeld, Fenster- und Bereichswahl, Rückfragen, Menüs, Aufklapplisten, Tooltips, Startfenster, Viewer. In WPF sind Menüs, Aufklapplisten und Tooltips eigene Fenster.
- Die Zuordnung wird gesetzt, **bevor** das Fenster zum ersten Mal sichtbar wird. Dafür gibt es eine zentrale Stelle, an der jedes neue Fenster des UI-Threads vorbeikommt; kein Fenster setzt sie selbst.
- Dass die Ausnahme auf allen drei Aufnahmewegen und für alle Fensterarten (auch durchsichtige) wirkt, ist durch Erfahrungsberichte gestützt, aber nicht dokumentiert *(unbestätigt → VE-05, Z-6)*. Deshalb der Selbsttest.

### 6.2 Keine Spuren in der Windows-Oberfläche

Während der Aufnahme:

- kein Eintrag in der Taskleiste und im Fensterumschalter – Leiste und Rahmen sind Werkzeugfenster, Startfenster und Viewer sind verborgen (nicht minimiert);
- kein Symbol im Infobereich;
- keine Windows-Benachrichtigungen des Recorders.

Grenze der Zusage: Sie gilt für die Fenster des Recorders und die Flächen der Windows-Oberfläche, die er selbst bestimmt. Zeigt ein fremdes Programm den Prozessnamen an (zum Beispiel der Task-Manager), ist das Inhalt des Bildschirms.

### 6.3 Selbsttest bei jedem Aufnahmestart

Für jeden Monitor im Aufnahmebereich und den dafür gewählten Aufnahmeweg:

1. Ein **Prüffenster** des Recorders wird angezeigt: klein (64 × 64 Pixel), oberstes Fenster, mit gesetzter Ausnahme und einem zufälligen, eindeutigen Farbmuster.
2. Zwei Bildaufbauten abwarten; Bild A über den Aufnahmeweg holen.
3. Prüffenster verbergen; zwei Bildaufbauten abwarten; Bild B holen.
4. **Bestanden**, wenn im Bereich des Prüffensters (a) das Farbmuster in Bild A nicht vorkommt und (b) Bild A und Bild B dort pixelgleich sind. Bedingung (b) schließt den Fall aus, dass das System statt des Inhalts eine schwarze Fläche liefert.
5. Unterscheiden sich A und B, weil sich der Bildschirm dort zufällig geändert hat, wird bis zu dreimal an anderer Stelle wiederholt. Bleibt das Ergebnis unklar, gilt der Test als nicht bestanden.

Der Test dauert höchstens 300 ms je Monitor. Das Ergebnis steht als `system` (`selfTest`) in der Sitzung. Schlägt er fehl, wird zuerst der nächste Aufnahmeweg geprüft; besteht keiner, folgt die Rückfrage zum Ausweichmodus ([07](07-aufnahme-bedienung.md)). Es wird nie stillschweigend im Normalmodus aufgenommen.

### 6.4 Ausweichmodus

Der Ausweichmodus hält die Zusage „nie im Bild" auch ohne Systemausnahme – um den Preis kurzer Ausblendungen der Anzeige.

1. Sichtbar sind nur noch Rahmen und Kurzanzeige; bedient wird über Hotkeys.
2. Liegt ein Monitor außerhalb des Aufnahmebereichs, steht die Anzeige dort. Dann gibt es keine Einschränkung.
3. Sonst werden Rahmen und Kurzanzeige **für jedes Bild verborgen**: verbergen, zwei Bildaufbauten abwarten, Bild holen, wieder zeigen. Bilder, die bei sichtbarer Anzeige eintreffen, dienen nur der Änderungserkennung und werden nie gespeichert.
4. Ein solches **sauberes Bild** wird geholt: nach jeder wesentlichen Änderung samt Kurzberuhigung, bei jedem Bildauslöser und sonst alle 2 s – höchstens zweimal je Sekunde.
5. **Vorher-Bild:** Die Vorher-Bild-Garantie wird in diesem Fall **nicht zugesagt**; die Rückfrage aus [07](07-aufnahme-bedienung.md) sagt das. Einzelne Bilder tragen dennoch `beforeInput`, wenn der Nachweis gelingt: Hat sich seit dem letzten sauberen Bild nichts Wesentliches geändert, ist dieses das Vorher-Bild. Andernfalls wird sofort ein sauberes Bild geholt (`atInput`); es kann den Zustand kurz nach der Eingabe zeigen.
6. Die Sitzung vermerkt den Modus (`selfTest`, `mode: "fallbackHidden"`).

### 6.5 Laufende Überwachung

*(Soll)* Alle 100 ms und bei jedem Bildauslöser prüft der Hauptprozess, ob jedes seiner sichtbaren Fenster die Ausnahme trägt. Ein Verstoß wird sofort behoben, als `system` (`recorder`, Fehler) vermerkt und gezählt; Bilder aus dem betroffenen Zeitraum tragen einen Vermerk. Der Zähler muss im Test null bleiben.

### 6.6 Abnahme

Ein bekanntes Vollbild-Testmuster wird mit darüberliegenden Recorder-Fenstern (Leiste, Rahmen, Kommentarfeld, Tooltip, Menü) aufgenommen – auf jedem Aufnahmeweg, unter Windows 10 und 11. Jedes gespeicherte Bild muss pixelgleich zur Referenz sein (T-05 in [11](11-roadmap-tests-risiken.md)).

Nebenwirkung: Auch andere Aufnahmeprogramme und Bildschirmfreigaben zeigen die Fenster des Recorders nicht.

---

<a id="ui-automation"></a>
## 7 UI Automation

### 7.1 Zugriff

- Der Kern nutzt die COM-Schnittstelle der UI Automation direkt, auf eigenen Arbeitsthreads im Mehrfach-Apartment, die keine Fenster besitzen ([02](02-architektur.md#thread-modell)). Das entspricht der Vorgabe des Herstellers *(belegt)*.
- Zeitlimits: Verbindungsaufbau 500 ms (Standard des Systems 2 s), einzelner Aufruf 1000 ms (Standard 20 s) *(Standardwerte belegt)*.
- **Nur lesend.** Kein Muster, das den Zustand der Anwendung ändert, wird aufgerufen; auch das automatische Setzen des Fokus ist abgeschaltet.
- Fenster des Recorders, ausgeschlossene Anwendungen und Fenster außerhalb des Aufnahmebereichs werden nicht abgefragt (`status: "skipped"`).

<a id="doppelte-ermittlung"></a>
### 7.2 Doppelte Ermittlung des Ziels

Ein einzelner Blick auf das Element unter dem Zeiger genügt nicht: Vor dem Klick ermittelt, kann er veraltet sein; nach dem Klick ermittelt, zeigt er oft schon das Fenster dahinter. Deshalb ermittelt der Kern zweimal und überlässt den Vergleich der Schritterkennung ([05, Zielbestimmung](05-schritterkennung.md#zielbestimmung)). Das Rohprotokoll enthält beide Ergebnisse, aber kein Urteil über ihre Übereinstimmung; dieses steht als `target.status` in `steps.json` ([04](04-datenformat.md#steps-json)).

**Vorab-Ermittlung beim Verweilen** (`trigger: "dwell"`). Ruht der Zeiger 80 ms (weniger als 5 Pixel Bewegung in 100 ms), wird das Element unter ihm ermittelt und gemerkt. Das Ergebnis verliert seine Gültigkeit durch:

| Anlass | `dwellInvalidatedBy` |
|---|---|
| Älter als 1,5 s | `age` |
| Zeiger hat das Rechteck des Elements verlassen | `moved` |
| Mausrad gedreht | `wheel` |
| Taste gedrückt | `key` |
| Fenster in der Umgebung erschienen, verschwunden, verschoben; Vordergrund gewechselt | `windowEvent` |
| Der Bildschirm hat sich im Rechteck des Elements geändert (Meldung der Bildaufnahme) | `screenChange` |

Nach einem solchen Anlass beginnt das Verweilen neu.

**Ermittlung beim Tastendruck** (`trigger: "buttonDown"`). Bei jeder gedrückten Maustaste wird sofort erneut ermittelt. Das Ereignis nennt, welches Vorab-Ergebnis der Kern in diesem Moment für aktuell hielt, ob es noch gültig war und wodurch nicht.

**Weitere Anlässe:** beim Loslassen (`buttonUp`, Ziel eines Ziehens), beim Radbeginn (`wheel`), bei jedem Fokuswechsel (`focus`), bei Schnappschuss und Kommentar (`snapshot`).

**Was geschrieben wird.** Ermittlungen bei Maustaste, Rad, Fokus und Schnappschuss stehen immer im Protokoll, auch gescheiterte. Vorab-Ermittlungen werden geschrieben, sobald eine Maustaste oder ein Radereignis darauf Bezug nimmt oder das Verweilen die Verweilzeit des Systems erreicht (`hoverTimeMs`); kürzere, folgenlose werden verworfen.

Die schwierigen Fälle und was der Kern dafür liefert:

| Fall | Was geschieht |
|---|---|
| Rad drehen, dann klicken, ohne die Maus zu bewegen | Das Rad macht das Vorab-Ergebnis ungültig; nach Radende und Bildänderung wird neu ermittelt. Reicht die Zeit nicht, bleibt nur die Ermittlung beim Druck. |
| Ein Popup erscheint unter dem ruhenden Zeiger | Fensterereignis und Bildänderung machen das Vorab-Ergebnis ungültig; neu ermittelt wird das Popup-Element. |
| Schneller Klick auf einen Menüeintrag | Verweilen zu kurz; die Ermittlung beim Druck trifft womöglich schon das Fenster hinter dem geschlossenen Menü. Das Fenster unter dem Zeiger aus dem Hook-Aufruf zeigt den Widerspruch, und der Text-Schnappschuss des Menüs ([7.5](#text-schnappschuss)) enthält die Einträge mit Rechtecken – die Schritterkennung kann den Eintrag über die Position zuordnen. |

Den Klick so lange aufzuhalten, bis das Element ermittelt ist, kommt nicht in Frage (Grundsatz 2).

### 7.3 Erfasste Eigenschaften

Die Felder der Elementbeschreibung stehen in [04, Gemeinsame Bausteine](04-datenformat.md#ereignistypen). Der Kern beschafft sie so:

- **Ein Aufruf je Element.** Alle Eigenschaften und Musterwerte kommen über eine Sammelanfrage (CacheRequest) in einem prozessübergreifenden Aufruf.
- **Laufzeit-Kennung.** Jede Beschreibung trägt die Laufzeit-Kennung des Elements. Sie bleibt gleich, solange das Element lebt, und ist die Grundlage für den Vergleich von Vorab- und Druck-Ermittlung und für das Register „war Passwortfeld" in [10](10-datenschutz-und-sicherheit.md).
- **Vorfahren** lassen sich nicht mit anfordern – Eltern und Vorfahren sind in einer Sammelanfrage nicht zulässig *(belegt)*. Der Pfad bis zum Fenster wird Schritt für Schritt mit einer schlanken Anfrage gegangen, höchstens 12 Ebenen und 150 ms.
- **Position unter Gleichnamigen** (`dupIndex`, `dupCount`): Zählung der sichtbaren Elemente desselben Fensters mit gleichem Typ und Namen, begrenzt auf 100 ms. Wird die Grenze erreicht, fehlen beide Angaben.
- **Zeilenkontext** (`row`): Liegt das Element in einem Listen-, Tabellen- oder Baumeintrag, werden Zeilen- und Spaltenangaben und bis zu acht Zelltexte gelesen. So lässt sich sagen, **welche** von zwanzig gleich beschrifteten Schaltflächen gemeint war.
- **Adresse und Dokument:** In Browsern liefert der umgebende Dokumentknoten die Adresse (`url`); sonst, wo vorhanden, Name oder Pfad des Dokuments (`doc`).
- **Scrollstand** des nächsten scrollbaren Vorfahren, für Scroll-Schritte.
- **Werte** laufen durch den Wertefilter ([11](#andockpunkte-datenschutz)). Bei Elementen mit gesetzter Passwort-Eigenschaft wird der Wert gar nicht erst gelesen.

### 7.4 Rückfall und undurchsichtige Oberflächen

**Rückfallkette.** Liefert die UI Automation nichts Brauchbares (Zeitlimit, Fehler, nur die Fensterhülle), folgt MSAA (Name, Rolle, Zustand, Lage des Objekts unter dem Punkt), danach Win32 (Fensterklasse, Steuerelement-Kennung, Fenstertext mit Zeitlimit). Die Quelle steht am Ereignis. Auch die Rückfallaufrufe laufen zeitbegrenzt auf dem UIA-Arbeiter.

<a id="undurchsichtig"></a>
**Undurchsichtige Oberflächen.** Manche Fenster zeigen ihren Inhalt nur als Bild: Remote-Desktop, virtuelle Maschinen, Spiele, Terminals, Zeichenflächen, Videos. Die UI Automation liefert dort nur die Hülle. Der Kern erkennt das und beschreibt nichts, was er nicht weiß.

Einstufung eines Fensters als undurchsichtig (`window.info`: `surface`, `surfaceKind`), wenn eines zutrifft:

1. Programm oder Fensterklasse stehen auf der vorbelegten, erweiterbaren Liste (Remote-Desktop-Clients, Hypervisor-Konsolen, Terminalprogramme).
2. **Strukturprobe:** Ermittlungen an mindestens drei verschiedenen Punkten liefern dasselbe Element, dessen Rechteck mindestens 90 % der Fensterfläche bedeckt und das vom Typ Bereich, Fenster, Dokument ohne Kinder oder benutzerdefiniert ist.
3. Der Elementbaum eines Fensters ab 400 × 300 Pixeln hat höchstens drei Knoten.

Innerhalb eines sonst gegliederten Fensters gilt dasselbe für einzelne Flächen (Zeichenfläche, eingebettetes Video): Das Element trägt `opaqueHint`.

Folgen im Kern:

- Für undurchsichtige Fenster entfallen Vorab-Ermittlungen (sie kosten nur). Bei Maustasten wird die Hülle mit Hinweis protokolliert.
- Der Eingabekontext ist `opaque`; es gibt keinen Feldinhalt als Gegenprobe. Die Aufnahmeleiste erhält das Signal für den Klartext-Hinweis ([10](10-datenschutz-und-sicherheit.md)).
- Terminals, die ihren Text über UI Automation anbieten (Windows Terminal, klassische Konsole), bleiben für Elemente undurchsichtig. Ihr Text wird nur für die Erkennung von Passwortabfragen gelesen ([11](#andockpunkte-datenschutz), A6); als Feldinhalt (`ui.value`) wird er nicht gespeichert.
- Mauswege sind hier Primärdaten (Zeichnen, Ziehen in einer Fernsitzung); sie stehen ohnehin vollständig im Protokoll.
- Die Einstufung wird beim Sitzungsbeginn, bei jedem neuen Fenster und nach größeren Inhaltswechseln neu geprüft – ein frisch gestartetes Programm, dessen Barrierefreiheit noch nicht bereit ist, soll nicht dauerhaft als undurchsichtig gelten.

<a id="text-schnappschuss"></a>
### 7.5 Text-Schnappschuss neuer Fenster

Bilder zeigen, was auf dem Bildschirm stand; eine KI soll es aber nicht aus Pixeln raten müssen. Deshalb hält der Kern von allem, was neu erscheint, den **Wortlaut** fest (`ui.snapshot`).

| Auslöser | `trigger` | Zeitbudget |
|---|---|---|
| Fenster der obersten Ebene erscheint | `windowOpen` | 250 ms |
| Dialog beginnt | `dialogStart` | 250 ms |
| Menü klappt auf | `menuOpen` | 100 ms, sofort beim Aufklappen – Menüs sind kurzlebig |
| Popup, Aufklappliste, Flyout, Tooltip | `popup` | 100 ms |
| Schnappschuss-Taste; Marker der Typen `error` und `expected` ([3.9](#eigene-bedienung)); Vordergrundfenster beim Beginn jedes Aufnahmeabschnitts | `manual` | 250 ms |

Inhalt und Grenzen:

- Durchlauf in Lesereihenfolge durch die Steuerelement-Sicht; nur sichtbare Elemente; höchstens `capture.snapshotMaxNodes` Knoten (Standard 300), 12 Ebenen und das Zeitbudget. Wird eine Grenze erreicht, ist der Schnappschuss als unvollständig gekennzeichnet.
- Je Knoten: Typ, Name, Kennungen, Rechteck, Zustände, bei Werten höchstens 200 Zeichen. Werte laufen durch den Wertefilter.
- Kein vollständiger Abzug des Elementbaums (das ist eine spätere Ausbaustufe), sondern das, was man liest: Meldungstexte, Menüeinträge, Beschriftungen.
- *Soll:* erneuter Schnappschuss, wenn sich der Titel des Vordergrundfensters ändert und der Bildschirm danach zur Ruhe kommt (Seitenwechsel in Assistenten und Browsern).
- Für Fenster außerhalb des Aufnahmebereichs und ausgeschlossene Anwendungen entsteht kein Schnappschuss.

### 7.6 Fokus und Feldinhalt

**Fokus.** Jeder Fokuswechsel (WinEvent und UIA-Ereignis, zusammengeführt) löst eine Ermittlung des fokussierten Elements aus. Der Kern liefert dem Schutzfilter die Einstufungsgrundlage ([11](#andockpunkte-datenschutz), A1); dieser bestimmt den Eingabekontext, und das `focus`-Ereignis wird geschrieben. Die vollständige Beschreibung folgt als `element` mit `trigger: "focus"`.

Dazu gehört das **Fokusrechteck** – Rechteck des Elements, wo vorhanden auch der Einfügemarke. Es sagt, wo auf dem Bildschirm getippt wurde, und dient der Bereichsprüfung ([5.2](#aufnahmebereiche)) und den Bildausschnitten.

**Feldinhalt vorher/nachher** (`ui.value`):

| Zeitpunkt | `phase` |
|---|---|
| Fokus kommt in ein Feld mit lesbarem Inhalt | `focusIn` |
| Das Element meldet eine Wertänderung | `change` |
| Fokus verlässt das Feld; Eingabe- oder Tabulatortaste; Klick an anderer Stelle | `focusOut` |
| Kein Änderungsereignis verfügbar: 300 ms nach dem letzten Tastendruck eines Laufs | `poll` |

- Gelesen werden höchstens `capture.valueMaxChars` Zeichen (Standard 2000). Bei längeren Inhalten Länge und die Umgebung der Einfügemarke.
- In großen Dokumenten (Textverarbeitung) wird nie das ganze Dokument gelesen, sondern die Zeile bzw. der Absatz um die Einfügemarke.
- Alle Inhalte laufen durch den Wertefilter; in Passwortfeldern wird nichts gelesen.

**Markierter Text:** Nach einem Ziehen in einem Textfeld wird die Auswahl gelesen (`ui.selection`), begrenzt auf 500 Zeichen.

### 7.7 Ereignisse der UI Automation

Angemeldet wird – immer auf demselben Thread ohne Fenster:

- **global:** Fokuswechsel; Fenster geöffnet/geschlossen (Gegenprobe zu den WinEvents); Menü geöffnet/geschlossen; Tooltip geöffnet; Mitteilungen und Live-Bereiche (`ui.notification`) – das sind die Texte, die auch ein Vorleseprogramm ansagen würde, etwa „Datei gespeichert".
- **nur für das fokussierte Element** und, bei Listen, Bäumen, Tabellen, Registern und Kombinationsfeldern, seine Kinder: Wert, Umschaltzustand, Auswahl, Auf-/Zuklappen, Auslösen, Textänderung (gedrosselt). Bei jedem Fokuswechsel wird um-angemeldet.

Die Behandlung reiht nur ein; gelesen wird auf dem Arbeiter. `t` ist das Eintreffen (`tSrc: "obs"`). Beim Stopp werden alle Anmeldungen entfernt.

### 7.8 Browser und Oberflächenbibliotheken

| Umgebung | Lage | Quellenlage |
|---|---|---|
| Win32, WinForms, WPF, WinUI, UWP | eigene UIA-Anbieter | – |
| Chrome, Edge und darauf aufbauende Programme | UIA-Anbieter ab Chrome 138 standardmäßig aktiv; eine Unternehmensrichtlinie kann ihn bis Chrome 146 abschalten | belegt |
| Chromium, Electron: Aktivierung erst beim ersten Zugriff | Die erste Abfrage kann leer oder langsam sein („kaltes" Fenster) | unbestätigt → VE-04, Z-8 |
| Firefox | UIA über eine Einstellung; Standard-Aktivierung war ab Version 143 angekündigt | Auslieferung unbestätigt → VE-04 |
| Qt, Java, ältere Bibliotheken | teils über MSAA, teils gar nicht (Java nur mit eingeschalteter Zugriffsbrücke) | → VE-04 |

Folge für den Kern: Kommt ein Fenster eines Chromium- oder Firefox-Programms in den Vordergrund, stellt der Kern eine harmlose Leseabfrage an dessen Dokument („Aufwärmen"), damit die Barrierefreiheit bereit ist, bevor der erste Klick fällt. Ergebnisse während des Aufwärmens gelten nicht als Beleg für eine undurchsichtige Oberfläche.

Nebenwirkung: Eine aktive UIA-Abfrage kann Programme verlangsamen oder ihren Speicherbedarf erhöhen (bekannt von Browsern). Je Anwendung lässt sich die Element-Ermittlung deshalb abschalten; das Fenster gilt dann als undurchsichtig.

### 7.9 Zeitbudget und Hänger

- Ein Arbeiter bearbeitet alle Abfragen nacheinander. Vorab-Ermittlungen sind verdrängbar (die jüngste gewinnt, höchstens 10 je Sekunde); Ermittlungen bei Maustaste, Fokus und Schnappschuss werden nie verworfen.
- Antwortet ein Aufruf 1,5 s lang nicht, gilt der Arbeiter als hängend: Ein Ersatzarbeiter mit eigener UIA-Instanz übernimmt, die hängende Abfrage endet mit `status: "timeout"`. Mehr als vier hängende Arbeiter gibt es nicht; dann ist die Ermittlung für die verursachende Anwendung 30 s gesperrt (`skipped`).
- Fenster mit höheren Rechten lassen sich nicht abfragen (`accessDenied`).
- Zeitüberschreitungen sind **keine Lücke** – die Eingabe ist ja aufgezeichnet –, sondern senken die Belegstufe des Schritts ([05](05-schritterkennung.md#belegstufen)).

---

<a id="fensterereignisse"></a>
## 8 Fenster-, Sitzungs- und Anzeigeereignisse

### 8.1 WinEvents

Der Systembeobachter meldet sich außerhalb fremder Prozesse an (kein Laden von Code in andere Programme) und schließt den eigenen Prozess aus. Der Rückruf kopiert nur Ereignis, Fenster, Objekt, Thread und Ereigniszeit und reiht ein.

| WinEvent | Ergebnis |
|---|---|
| Vordergrund gewechselt | `window.foreground` |
| Fenster erzeugt, gezeigt, verborgen, zerstört (nur Fenster der obersten Ebene) | `window.info`, `window.open`, `window.close` |
| Name geändert (Fenster der obersten Ebene) | `window.title` |
| Verschieben/Größenänderung beendet; minimieren, wiederherstellen | `window.rect` |
| Lage geändert (nur Fenster der obersten Ebene; gedrosselt) | Verlauf der Fensterlage für [5.5](#aufnahmebereiche); nicht jedes Ereignis wird protokolliert |
| Menümodus und Menü-Popup, Beginn und Ende | `window.menu`; Auslöser für den Text-Schnappschuss |
| Dialog, Beginn und Ende | `window.dialog`; Auslöser für den Text-Schnappschuss |
| Fensterumschalter, Beginn und Ende | `window.switch` |
| Ziehen-und-Ablegen, Beginn und Ende | `window.dragdrop` |
| Mauserfassung, Beginn und Ende | Hinweis für Ziehvorgänge (am folgenden Mausereignis) |
| Fokus | Auslöser für [7.6](#ui-automation) |
| Zustand, Wert, Auswahl, Auslösen, Live-Bereich | `ui.toggle`, `ui.value`, `ui.selection`, `ui.invoke`, `ui.notification` – wenn kein UIA-Ereignis dasselbe meldet |
| Verhüllt/enthüllt (virtuelle Desktops, angehaltene Apps) | Sichtbarkeit im Fensterregister |
| Eingabemethode gezeigt/verborgen | Kennzeichen für aktive Eingabemethode |
| Desktop gewechselt | `system` (`desktopSwitch`); siehe [8.3](#fensterereignisse) |

`t` ist die Ereigniszeit des Systems (`tSrc: "sys"`), `tObs` der Rückruf. Doppelmeldungen von WinEvent und UIA-Ereignis werden zusammengeführt (gleiches Element, gleiche Art, innerhalb von 200 ms); es gilt die frühere Zeit.

### 8.2 Fensterregister

Für jedes Fenster der obersten Ebene, das der Kern sieht, führt das Register: Handle (mit Zähler gegen Wiederverwendung), Kennung `w<n>`, Art, Klasse, Titel, Prozess, Programmdatei, Anwendungskennung, Besitzer, Lage (sichtbare Grenzen), Zustand, Monitor, DPI-Verhalten, Rechte, Oberflächeneinstufung, Zugehörigkeit zum Aufnahmebereich, Ausschluss. Felder: [04](04-datenformat.md#ev-window).

- Fenstertitel der obersten Ebene werden ohne Nachricht an das Fenster gelesen; ein hängendes Programm kann den Kern dadurch nicht aufhalten. Was doch eine Nachricht braucht, läuft mit Zeitlimit auf dem Anreicherungsthread.
- Titel laufen durch den Wertefilter ([11](#andockpunkte-datenschutz)).
- Für ein Fenster, das schon wieder verschwunden ist, wenn die Anreicherung es erreicht (Menüs), steht `window.info` mit `partial`.
- **Gehostete Apps:** Das Rahmenfenster nennt die tatsächliche Anwendung (`hosted`).
- **Rechte:** Aus dem Prozess-Token; ist es nicht lesbar, gilt das Fenster als höher berechtigt.

### 8.3 Sitzung, sicherer Desktop, Adminfenster, Energie

**Sicherer Desktop** (UAC-Abfrage, `Strg+Alt+Entf`, Bildschirmschoner, Anmelde- und Sperrbildschirm). Dort kann ein normaler Prozess weder Eingaben noch Bilder beobachten. Der Entwurf sah mehrere Signale vor; **umgesetzt** ist eines, das allein genügt: der Name des Eingabedesktops.

| Signal | Stand |
|---|---|
| Der Eingabedesktop (`OpenInputDesktop`, Name über `GetUserObjectInformation`) heißt nicht „Default“ oder lässt sich nicht öffnen (dann „inaccessible“); Abfrage **alle 250 ms**, durchgehend während der Aufnahme | umgesetzt (`DesktopProbe`) |
| Aufbau der Duplizierung wird verweigert bzw. Zugriff geht verloren | belegt; als Zusatzsignal vorgesehen, nicht nötig |
| WinEvent „Desktop gewechselt" | unbestätigt → Z-4; wird nicht benutzt |

**Zustandslogik (`DesktopWatch`, rein und getestet).**

- **Beginn sofort.** Die erste Lesung eines anderen Desktops als „Default“ beginnt die Lücke.
- **Ende erst nach zwei „Default“-Lesungen in Folge.** Beim Umschalten flackert der Name; eine einzelne „Default“-Lesung beendet die Lücke nicht. Das Ende liegt damit 250 bis 500 ms nach der Rückkehr. Dazwischen auftretende andere Lesungen beginnen das Zählen von vorn.
- **Art.** Läuft bei der Lesung die Benutzerkontensteuerung (Prozess `consent.exe`), ist die Art `uac`, sonst `other`. Wird `consent.exe` erst später sichtbar, wird die Art während der Lücke auf `uac` angehoben.
- **Zurücksetzen.** Sperre, Trennen und Ruhezustand setzen den Zustand zurück; eine offene Lücke `secureDesktop` geht in der Sperr-Lücke auf (Ende mit `end: sessionLocked`). Solange der Rechner gesperrt, getrennt oder im Ruhezustand ist, beginnt keine eigene Lücke `secureDesktop`.

**Folge:**

1. Beim Beginn: `system` (`desktopSwitch`, `to: secure`, mit dem Namen des Desktops), Lücke `secureDesktop` (alle Kanäle, Beginn sofort geschrieben) mit `detail.desktop` und `detail.kind`, und die Bildaufnahme wird ausgesetzt ([4.14](#bildschirmaufnahme)).
2. Beim Ende: die Lücke endet mit der endgültigen Art (`detail.kind`), `system` (`desktopSwitch`, `to: default`), der Tastenzustand wird zurückgesetzt und neu gelesen (`keystate.sync` mit `reason: secureDesktop` und `gapId`, [3.5](#tastenzustand)), die Bildaufnahme wird freigegeben und ein **Schlüsselbild erzwungen**.
3. Eingaben auf dem sicheren Desktop erreichen die Aufzeichnung nicht; das gilt auch für ein dort getipptes Kennwort.

**Schritterkennung.** Der Schrittbau (Version 1.5.0; `StepParams.BuilderVersion`) macht aus der Lücke `secureDesktop` mit der Art `uac` einen eigenen Schritt „Benutzerkontensteuerung bestätigen“ (englisch „Confirm User Account Control“) und nennt die Dauer; Lücken ohne Art `uac` bleiben Lückenschritte. Lückenschritte durch sicheren Desktop, Sperre, Trennen und Ruhezustand tragen **kein Bild**, und ein Nachher-Bild wird nie aus einem Bild gebunden, das in einer solchen Lücke liegt ([05](05-schritterkennung.md)).

**Sperren, Benutzerwechsel, Trennen.** Der Kern meldet sich für Sitzungsbenachrichtigungen an (Wiederholung, falls der Dienst noch nicht bereit ist *(belegt)*). Wird die Arbeitsstation gesperrt, der Benutzer gewechselt oder die Sitzung getrennt, **pausiert die Aufnahme**: Wie in jeder Pause werden die Hooks entfernt und die Bildaufnahme angehalten.

1. Beim Sperren bzw. Trennen: `system`-Ereignis und Beginn der Lücke `sessionLocked` bzw. `sessionDisconnected`.
2. Beim Entsperren bzw. Wiederverbinden: `system`-Ereignis, Ende dieser Lücke, `session.pause` mit dem Grund `lock` und **Beginn einer Lücke `paused`**.
3. Die Aufnahme läuft erst weiter, wenn die Person am Rechner das bestätigt ([07](07-aufnahme-bedienung.md); [10](10-datenschutz-und-sicherheit.md), DS-003) – nach dem Entsperren kann jemand anderes dort sitzen. Mit der Bestätigung folgen `session.resume`, der Abgleich des Tastenzustands, falls nötig eine neue Topologie-Epoche und ein Schlüsselbild.

Ohne Bestätigung bleibt die Aufnahme pausiert; ein selbsttätiges Fortsetzen gibt es nicht. Beim Sperren und Trennen wird zudem die Bildaufnahme ausgesetzt ([4.14](#bildschirmaufnahme)); Bilder des Sperrbildschirms entstehen nicht.

**Fenster mit Adminrechten.** Kommt ein höher berechtigtes Fenster in den Vordergrund und läuft der Recorder ohne Adminrechte:

- Lücke `elevatedWindow` (Tastatur, Maus, Elemente) bis ein normales Fenster Vordergrund wird. **Bilder laufen weiter** – die Duplizierung zeigt auch solche Fenster –, sodass sichtbar bleibt, was geschah.
- Ob WinEvents für solche Fenster eintreffen, ist unbestätigt (→ Z-4). Deshalb fragt der Wächter das Vordergrundfenster zusätzlich alle 250 ms ab.
- Die Aufnahmeleiste bietet „als Administrator neu starten" an ([02](02-architektur.md#prozessmodell), [07](07-aufnahme-bedienung.md)).

**Energie.** Einschlafen und Aufwachen → `system` (`powerSuspend`, `powerResume`) und Lücke `suspend` (alle Kanäle, `inputLost: none`). Beim Einschlafen wird die Bildaufnahme ausgesetzt ([4.14](#bildschirmaufnahme)) und der Zustand der Desktop-Beobachtung zurückgesetzt; beim Aufwachen folgt – sofern die Sitzung nicht gesperrt oder getrennt ist – ein erzwungenes Schlüsselbild.

**Abmelden und Herunterfahren.** Der Kern schließt die Sitzung sofort ab (`session.stop`, Grund `systemShutdown` bzw. `sessionEnd`) und blockiert das Herunterfahren nicht.

### 8.4 Anzeige

Anzeigenachrichten (Auflösung, Anordnung, Skalierung, Arbeitsbereich, Geräte an/ab) führen zu einer neuen Topologie-Epoche ([4.13](#topologie)). Diese Nachrichten gehen nur an Fenster der obersten Ebene; der Systembeobachter besitzt dafür ein verborgenes Fenster, das nie angezeigt wird.

---

<a id="zwischenablage"></a>
## 9 Zwischenablage

Felder: [04](04-datenformat.md#ev-clipboard). Grundsatz: **Herkunft immer, Inhalt nur auf Wunsch.**

1. Der Kern meldet sich als Zuhörer der Zwischenablage an und erfährt jede Änderung.
2. **Ohne Inhaltsaufzeichnung (Standard) wird der Inhalt gar nicht abgerufen.** Der Kern liest nur die Herkunft: Folgenummer des Systems, bereitstellendes Fenster und dessen Prozess (ersatzweise das Vordergrundfenster) und die Namen der angebotenen Formate. Dafür muss die Zwischenablage nicht geöffnet und kein Format angefordert werden; die Quellanwendung wird nicht veranlasst, verzögert bereitgestellte Formate zu erzeugen. Die Art des Inhalts (Text, Bild, Dateien …) ergibt sich aus den Formatnamen.
3. **Größe je Format** steht im Ereignis nur, soweit sie sich ohne Abruf des Inhalts feststellen lässt. Nach heutigem Kenntnisstand ist das mit dokumentierten Mitteln nicht möglich *(unbestätigt → Z-14)*; ohne Inhaltsaufzeichnung fehlt die Größe dann. Mit Inhaltsaufzeichnung steht sie für die abgerufenen Formate bereit (Zeichenzahl bei Text, Anzahl bei Dateilisten).
4. **Vertraulichkeitsmarken der Quelle** werden beachtet *(belegt)*. Sie sind an der Formatliste erkennbar: Ist das Format „von Überwachung ausschließen" vorhanden, greift der Kern auch bei eingeschalteter Inhaltsaufzeichnung auf nichts zu. Die Marken „nicht in den Verlauf" und „nicht in die Cloud" gelten ohne Inhaltsaufzeichnung schon durch ihr Vorhandensein als Hinweis auf Vertrauliches (`sensitive`); ihr Wert wird nur bei eingeschalteter Inhaltsaufzeichnung gelesen. Vertraulich Gekennzeichnetes wird nie gespeichert.
5. **Mit Inhaltsaufzeichnung** wird ausschließlich Text abgerufen (begrenzt nach [10](10-datenschutz-und-sicherheit.md)), Dateilisten nur als Anzahl, Bilder und Dateiinhalte nie. Die Zwischenablage wird dafür höchstens 5 ms geöffnet gehalten; ist sie belegt, folgen bis zu fünf Versuche im Abstand von 10 ms, danach bleibt es bei der Herkunft. Der Inhalt läuft durch den Schutzfilter ([11](#andockpunkte-datenschutz), A5).
6. **Kein Eingriff.** Die Zwischenablage wird nie geleert oder verändert.
7. **Gleichheit ohne Spur – nur mit Inhaltsaufzeichnung.** Um Wiederholungen zu erkennen, vergleicht der Kern den Textinhalt mit den letzten acht Inhalten, ausschließlich im Arbeitsspeicher. Gespeichert werden Kennung und Verweis auf den gleichen früheren Eintrag, kein Hash. Ohne Inhaltsaufzeichnung gibt es keinen Inhalt und damit keinen Vergleich; die Zuordnung zum Einfügen läuft dann allein über die Folge der Ereignisse.
8. Bei eingeschränktem Aufnahmebereich nennt das Ereignis für Quellen außerhalb nur Kennung und Art, nicht das Fenster; für ausgeschlossene Anwendungen gilt [10](10-datenschutz-und-sicherheit.md).

Die Verbindung zum Einfügen stellt die Schritterkennung her: Maßgeblich ist das letzte `clipboard`-Ereignis vor der Einfüge-Eingabe.

---

<a id="lueckenerkennung"></a>
## 10 Lückenerkennung

Felder und Ursachen: [04, `gap`](04-datenformat.md#ev-gap). Eine Lücke wird beim Beginn **und** beim Ende geschrieben; so übersteht sie einen Absturz. Punktuelle Lücken haben die Phase `single`.

### 10.1 Katalog

| Ursache (`cause`) | Erkennung | Betroffene Kanäle | Ende | Abschnitt |
|---|---|---|---|---|
| `paused` | Nutzer pausiert; außerdem nach dem Entsperren bzw. Wiederverbinden, bis das Fortsetzen bestätigt ist | alle | Fortsetzen bzw. Bestätigung | [1.3](#lebenszyklus), [8.3](#fensterereignisse) |
| `privacyMode` | Privatmodus, ausgelöst durch den Schutzfilter: Privattaste (Umschalter, endet nicht von selbst) oder erkannte Passwortabfrage im Terminal (die Erkennung ist nicht umgesetzt, [10, 6](10-datenschutz-und-sicherheit.md#6-passwortabfragen-im-terminal)) | Tastatur, je nach Regel weitere | Privattaste erneut gedrückt; bei erkannter Abfrage: Eingabetaste, Strg+C, Strg+D, Esc, Fokuswechsel oder 60 s ohne Taste. Der Grund steht am Ende-Ereignis (`endReason`) | [10](10-datenschutz-und-sicherheit.md) |
| `excludedApp` | Anwendung der Ausschlussliste stellt den Fenster-Stempel | Tastatur, Maus, Elemente, Zwischenablage; **ohne Zähler und ohne Eingabenummern** | anderes Fenster im Vordergrund | [2.4](#zeitmodell), [10](10-datenschutz-und-sicherheit.md) |
| `outOfScope` | Tasten, Maustasten, Rad oder Mausbewegungen außerhalb des Aufnahmebereichs | Tastatur, Maus, Elemente; mit Zählern je Art | nächste Eingabe im Bereich | [5.2](#aufnahmebereiche) |
| `secureDesktop` | Eingabedesktop ist nicht „Default“ (Art `uac` oder `other` in `detail.kind`) | alle | zwei aufeinanderfolgende „Default“-Lesungen | [8.3](#fensterereignisse) |
| `sessionLocked` | Sitzungsbenachrichtigung | alle | Entsperren; danach `paused` bis zur Bestätigung | [8.3](#fensterereignisse) |
| `sessionDisconnected` | Sitzungsbenachrichtigung (Trennen, Benutzerwechsel); Fehler der Bildaufnahme | alle | Wiederverbinden; danach `paused` bis zur Bestätigung | [8.3](#fensterereignisse) |
| `suspend` | Energienachricht | alle | Aufwachen | [8.3](#fensterereignisse) |
| `elevatedWindow` | Vordergrundfenster mit höheren Rechten | Tastatur, Maus, Elemente (Bilder laufen weiter) | normales Fenster im Vordergrund | [8.3](#fensterereignisse) |
| `hookLost` | Kanalabgleich; Herzschlag des Hosts | Tastatur oder Maus | Hook neu gesetzt | [3.4](#kanalabgleich) |
| `inputMismatch` | Fehlstelle, die sich weder ergänzen noch erklären lässt | Tastatur oder Maus | punktuell | [3.4](#kanalabgleich) |
| `captureLost` | Duplizierung verloren, kein sicherer Desktop; Epochenwechsel | Bilder | Aufnahme wieder aufgebaut | [4.9](#bildschirmaufnahme) |
| `frameDropped` | Vorher-Bild nicht verfügbar; Bild wegen Überlast verworfen | Bilder | punktuell, mit Verweis auf das Bild | [4.4](#vorher-bild), [4.8](#ueberlast) |
| `protectedContent` | Kennzeichen der Bildaufnahme | Bilder | Kennzeichen entfällt | [4.9](#bildschirmaufnahme) |
| `recorderInteraction` | Kommentarfeld, Fenster- und Bereichswahl, Rückfragen | alle (keine Nutzereingabe geht verloren) | Bedienung beendet | [3.9](#eigene-bedienung) |
| `diskFull` | Schreibfehler wegen fehlenden Platzes | alle | Platz vorhanden und fortgesetzt | [04](04-datenformat.md#absturzsicherheit) |
| `overload` | Ring-Überlauf; ausgedünnte Mausbewegungen | Tastatur und/oder Maus | Ring wieder aufnahmefähig | [3.4](#kanalabgleich) |
| `unsupportedInput` | erkannte, aber nicht aufzeichenbare Eingabeart (Mehrfinger-Geste) | Maus | punktuell | [3.7](#eingabekanaele) |
| `relativeMouse` | Raw Input meldet Bewegung, der Zeiger steht (gefangener Zeiger) | Maus (Positionen nicht aussagekräftig) | Zeiger wieder frei | [3.4](#kanalabgleich) |
| `unknown` | Das System meldet Eingaben, die kein Kanal sah, ohne erklärende Lage | Tastatur, Maus | Kanäle liefern wieder | [3.4](#kanalabgleich) |
| `crash` | – (trägt die Wiederherstellung ein) | alle | – | [04](04-datenformat.md#absturzsicherheit) |

Maskierte Tasten sind keine Lücke dieses Katalogs: Sie stehen als `text.masked` im Protokoll und zählen im Vollständigkeitsbericht unter `maskedInput` ([04](04-datenformat.md#ev-text-masked)).

### 10.2 Regeln

1. **Beginn sofort, Ende beim Ende.** Der Beginn wird geschrieben, sobald die Lücke feststeht – nicht erst, wenn sie vorbei ist. Vor einer Pause wird zusätzlich dauerhaft gesichert.
2. **Rückdatierung.** Wird eine Lücke erst später bemerkt (Hook-Verlust), beginnt sie beim ersten betroffenen Ereignis, nicht beim Bemerken.
3. **Überlagerung.** Lücken verschiedener Ursachen dürfen sich überlagern; jede hat ihre eigene Kennung.
4. **`inputLost`** sagt, ob in der Lücke Nutzereingaben verloren gegangen sein können. Der Kern setzt `certain`, wenn er verlorene Eingaben gezählt hat (Zähler in `detail`), `none`, wenn der Kanal bauartbedingt nichts verlieren kann (Bildlücken, eigene Bedienung), sonst `possible` oder `unknown`.
5. **Nach jeder Lücke**, die die Tastatur betrifft, folgt ein Abgleich des Tastenzustands.
6. **Kein Verlust ohne Lücke.** Jede Stelle, an der der Kern etwas verwirft – Ring, Warteschlangen, Bildabbau, Zeitlimits bei Eingaben –, hat einen Zähler und erzeugt eine Lücke oder (bei Elementen) einen Fehlstatus. Das wird durch Fehlerinjektion geprüft ([11](11-roadmap-tests-risiken.md)).

---

<a id="andockpunkte-datenschutz"></a>
## 11 Andockpunkte für den Datenschutz

Was maskiert, zurückgehalten oder ausgeschlossen wird, entscheidet [10](10-datenschutz-und-sicherheit.md). Der Kern stellt dafür sieben feste Stellen bereit. Nichts erreicht den Schreiber oder die Bildkodierung an ihnen vorbei.

| Nr. | Andockpunkt | Was der Kern liefert | Was der Schutzfilter entscheidet |
|---|---|---|---|
| A1 | **Fokus-Auskunft** | Zu jedem Fokuswechsel (mit Ereigniszeit und Elementbezug) und auf Anfrage: fokussiertes Element (Typ, Passwort-Eigenschaft, Klasse, Bibliothek, Laufzeit-Kennung), Kindfenster mit Fokus und dessen Passwort-Stil, Fenster-Stempel aus dem Hook-Aufruf, Oberflächeneinstufung, Beginn und Ende der Abfrage. Dazu ein Strom „Fokus kann sich geändert haben": fokusfähige Eingaben nach der Liste in 10 (Tabulator- und Eingabetaste, Mausklick und weitere), Fensterwechsel, Fokus- und Fensterereignisse | Eingabekontext `ctx` und dessen Herkunft |
| A2 | **Rückhaltepuffer** | Tasteneingaben laufen in ihrer Reihenfolge durch den Puffer, bevor sie den Schreiber erreichen; Mausereignisse warten nicht auf zurückgehaltene Tasten. Zurückgehaltenes liegt nur im Arbeitsspeicher. Der Puffer fasst mindestens 10 s Eingaben unter Volllast | Freigabe als `key.*`, Ersatz durch `text.masked`, Dauer der Rückhaltung |
| A3 | **Wertefilter** | Jeder Inhalt aus der Oberfläche – Elementwerte, Feldinhalte, Auswahltexte, Knoten von Text-Schnappschüssen, Fenstertitel, Mitteilungen – mit Herkunft (Element, Fenster, Kontext) | Speichern, Kürzen oder Weglassen mit Grund (`withheld`) |
| A4 | **Bildfilter** | Vor dem Vergleich und Kodieren jedes Bilds: Fensterlage zum Bildzeitpunkt, Rechtecke erkannter Passwortfelder, Fensterarten (Toast, Umschalter), Zustand des Privatmodus. Bilder warten dafür im Arbeitsspeicher, bis die Entscheidung für ihren Zeitpunkt feststeht (höchstens `T_max`) | Flächen, die schwarz zu füllen sind, mit Grund (`frame.masks`) |
| A5 | **Zwischenablage-Filter** | Herkunft, Formate, Vertraulichkeitsmarken; den Textinhalt nur, wenn die Einstellung es erlaubt | Speichern des Inhalts oder Weglassen mit Grund |
| A6 | **Terminal-Zeile** | Auf Anfrage das Ende des sichtbaren Textes eines Terminalfensters – die letzten drei nicht leeren Zeilen –, wo das Programm seinen Text über UI Automation anbietet (Windows Terminal, klassische Konsole). Angefragt wird nach einer Eingabetaste, nach Beruhigung der Ausgabe und nach den ersten Zeichen eines neuen Tastenlaufs. Der Kern selbst schreibt den gelesenen Text nirgends hin | Erkennung von Passwortabfragen und Maskierung der Eingabe, bis die Abfrage endet: Eingabetaste, Strg+C, Strg+D, Esc, Fokuswechsel oder 60 s ohne Taste. Gespeichert wird allein die erkannte **Abfragezeile** – der Text der Abfrage, nicht die Eingabe –, auf 120 Zeichen gekürzt, zusammen mit dem `text.masked` |
| A7 | **Ausschluss und Bereich** | Je Fenster: Programm, Titel, Zugehörigkeit zum Aufnahmebereich | Ausschluss (`excluded`); daraus folgen im Kern: keine Eingaben, keine Zähler, keine Eingabenummern, keine Elemente, keine Titel, Fläche schwarz gefüllt |

Zusagen des Kerns an diese Stellen:

1. **Reihenfolge:** Der Rückhaltepuffer gibt Tasten nur in ihrer ursprünglichen Reihenfolge frei; die Eingabenummer hält ihre Lage gegenüber den Mausereignissen fest ([2.4](#zeitmodell)).
2. **Keine Nebenwege:** Übersetzte Zeichen, Tastencodes und Einzelzeiten zurückgehaltener Tasten verlassen den Puffer nie – auch nicht in Protokolldateien, Leistungszähler oder die Vorschau der Schritterkennung.
3. **Dieselbe Prüfung für Werte wie für Tasten:** Ein Feldinhalt wird nie gelesen oder gespeichert, wenn die Tasten desselben Felds zurückgehalten würden.
4. **Bilder zuerst filtern:** Der Bildfilter wirkt vor dem Vergleich mit dem Bezugsbild; ungefilterte Pixel gelangen in kein Differenzbild.
5. **Absturz:** Was beim Absturz im Rückhaltepuffer liegt, ist verloren. Das ist gewollt.
6. **Nur Arbeitsspeicher:** Eingabe-Ring, Rückhaltepuffer und wartende Bilder liegen ausschließlich im Arbeitsspeicher. Ring und Rückhaltepuffer sind gegen Auslagerung gesperrt und werden nach Entscheidung bzw. Verbrauch mit Nullen überschrieben ([02](02-architektur.md#gemeinsamer-speicher), [10](10-datenschutz-und-sicherheit.md)).

---

<a id="leistungsbudgets"></a>
## 12 Leistungsbudgets

Alle Werte sind Zielwerte des Entwurfs. Sie werden in M0 (VE-01, VE-04, VE-09) und M1 gemessen und danach bestätigt oder angepasst.

### 12.1 Referenzsysteme und Lastprofile

Diese drei Referenzsysteme sind der eine Satz, auf den sich **alle** Zeitziele der Spezifikation beziehen; andere Kapitel bilden ihre Ziele darauf ab.

| Kürzel | Referenzsystem |
|---|---|
| A | Büro-PC: 4 Kerne (Leistungsklasse 2018), integrierte Grafik, 8 GB RAM, SATA-SSD, ein Monitor 1920 × 1080 bei 100 % |
| B | Entwickler-PC: 8 Kerne, eigene Grafikkarte, 32 GB RAM, NVMe-SSD, zwei Monitore 3840 × 2160 bei 150 % |
| C | Notebook mit Hybrid-Grafik: 2560 × 1440 intern bei 150 % und 3840 × 2160 extern bei 100 %, auch im Akkubetrieb |

| Kürzel | Lastprofil |
|---|---|
| P1 | Leerlauf: Aufnahme läuft, keine Eingabe, ruhiger Bildschirm |
| P2 | Büroarbeit: etwa 20 Aktionen und 200 getippte Zeichen je Minute, Fensterwechsel |
| P3 | Dauerbewegung: Video im Aufnahmebereich, kaum Eingaben |
| P4 | Stress: Maus mit 1000 Hz dauernd bewegt, 20 Tasten je Sekunde, schnelle Fensterwechsel, erzwungene Speicherbereinigungen und 4K-Bildkodierung im Hauptprozess |

### 12.2 Eingabepfad

| Größe | Ziel | Messung |
|---|---|---|
| Dauer des Hook-Aufrufs, Median | ≤ 20 µs | P4 auf A, B, C (auch Akkubetrieb) |
| Dauer des Hook-Aufrufs, 99 % | ≤ 100 µs | wie oben |
| Dauer des Hook-Aufrufs, **99,9 %** | **< 1 ms** | wie oben; Histogramm des Hosts |
| Dauer des Hook-Aufrufs, Höchstwert | < 50 % des wirksamen Zeitlimits | wie oben, 30 min |
| Durchsatz ohne Verlust | 8 000 Sätze je Sekunde über 60 s | Lastgenerator |
| Hook bis Eingabeverarbeitung, 99 % | ≤ 5 ms | P4 |
| Überbrückung bei Stillstand des Hauptprozesses | ≥ 30 s bei 2 000 Sätzen je Sekunde | Hauptprozess anhalten |

### 12.3 Bildpfad

| Größe | Ziel |
|---|---|
| Pin bis Auswahl des Vorher-Bilds, 99 % | ≤ 10 ms |
| Kosten eines Abrufs | ≤ 2 ms; höchstens 30 Abrufe je Sekunde und Monitor |
| Auslesen und Vergleich einer vollen 4K-Fläche | ≤ 15 ms |
| PNG-Kodierung eines Differenzbilds, 95 % | ≤ 30 ms |
| PNG-Kodierung eines Schlüsselbilds | 1080p ≤ 80 ms, 4K ≤ 300 ms |
| Freigabe durch den Schutzfilter bis Blob geschrieben, 95 % in P2 | ≤ 500 ms |
| Selbsttest der Unsichtbarkeit | ≤ 300 ms je Monitor |

### 12.4 UI Automation

| Größe | Ziel |
|---|---|
| Element-Ermittlung samt Pfad, Median / 95 % | ≤ 15 ms / ≤ 120 ms |
| Hartes Limit je Aufruf | 1 s |
| Text-Schnappschuss | ≤ 250 ms (Menü, Popup ≤ 100 ms) |
| Vorab-Ermittlungen | höchstens 10 je Sekunde |
| Übereinstimmung von Vorab- und Druck-Ermittlung in gegliederten Standardanwendungen | ≥ 90 % der Klicks (VE-04) |

### 12.5 Prozessorlast

Summe beider Prozesse, in Prozent **eines** Kerns, gemittelt über eine Minute:

| Profil | System A | System B |
|---|---|---|
| P1 Leerlauf | ≤ 1 % | ≤ 1 % |
| P2 Büroarbeit | ≤ 5 % (Spitzen ≤ 25 % für unter 1 s) | ≤ 8 % |
| P3 Dauerbewegung | ≤ 10 % | ≤ 15 % |
| P4 Stress | ≤ 25 % | ≤ 30 % |

### 12.6 Arbeits- und Grafikspeicher

| Größe | System A (1 × 1080p) | System B (2 × 4K) |
|---|---|---|
| Capture-Host, privater Speicher | ≤ 60 MB | ≤ 60 MB |
| Hauptprozess, Arbeitssatz im Normalbetrieb | ≤ 350 MB | ≤ 600 MB |
| Hauptprozess, Obergrenze bei vollen Warteschlangen | ≤ 600 MB | ≤ 1,2 GB |
| Grafikspeicher für den Bildring (4 Bilder je Monitor) | ≈ 33 MB | ≈ 266 MB |

Im Hauptprozess entfallen je 4K-Monitor rund 33 MB auf das Bezugsbild und bis zu 66 MB auf Auslesepuffer; die Warteschlange der Bildkodierung ist auf 512 MB begrenzt.

### 12.7 Datenträger

| Größe | Wert |
|---|---|
| Rohprotokoll bei bewegter Maus (1000 Messwerte je Sekunde) | etwa 11 KB je Sekunde |
| Rohprotokoll in P2 | 0,3–1 MB je Minute |
| Bildgrößen | Richtwerte in [04, Abschnitt 11.7](04-datenformat.md#bildspeicher) |
| Sitzungsgröße, 10 min P2 | System A ≤ 60 MB, System B ≤ 250 MB |
| Schreibdurchsatz in P2 auf System B | Mittel ≤ 1 MB/s; Spitzen bis 40 MB/s (Schlüsselbilder beider Monitore), von der Warteschlange gepuffert |
| Schreibdurchsatz in P3 auf System B | ≤ 8 MB/s dauerhaft |
| Mindestdurchsatz des Datenträgers für P2 auf System B ohne verworfene Bilder | 5 MB/s sequenziell |

Liegt der gemessene Durchsatz des Aufnahmeorts darunter (langsamer USB-Stick, Netzlaufwerk, synchronisierter Ordner), warnt die Aufnahmeleiste; die Abbaureihenfolge aus [4.8](#ueberlast) greift.

Zum Vergleich: Ein vollständiges 4K-Bild je Eingabe und Monitor ergäbe in P2 über 10 Minuten mehrere Gigabyte.

### 12.8 Start, Stopp, Dauerlauf

| Größe | Ziel |
|---|---|
| **Auslöser bis erstes aufgezeichnetes Ereignis** (ohne Countdown) | **≤ 1,5 s auf System A**, einschließlich Vorprüfung und Selbsttest aller Monitore |
| davon Vorprüfung | ≤ 1 s auf System A; die Selbsttests der Monitore laufen gleichzeitig (je ≤ 300 ms) |
| davon Capture-Host | wird während der Vorprüfung gestartet (≤ 500 ms bis „bereit"); das Setzen der Hooks auf Befehl dauert ≤ 50 ms |
| davon Sitzungskopf schreiben | ≤ 200 ms |
| Start eines weiteren Aufnahmeabschnitts ([1.7](#weitere-abschnitte)) | wie oben, zuzüglich Öffnen der bestehenden Sitzung: höchstens 1 s mehr bei einem Journal bis 100 MB |
| Stopp bis Sitzung abgeschlossen (ohne Schritterkennung) | ≤ 2 s zuzüglich Leerlaufen der Bildkodierung (in P2 ≤ 1 s) |
| Dauerlauf 8 h in P2 | Arbeitsspeicher wächst um höchstens 5 %; Zahl der Handles konstant; Hook-Dauer unverändert (T-13) |

### 12.9 Selbstmessung

Der Kern führt Leistungszähler und legt sie beim Stopp im Manifest unter `performance` ab ([04, Abschnitt 4.1](04-datenformat.md#manifest)). Die Namen sind hier festgelegt; Leser dürfen unbekannte Zähler übergehen. Mit jedem Aufnahmeabschnitt beginnen die Zähler bei null ([1.7](#weitere-abschnitte)).

| Zähler | Einheit | Bedeutung |
|---|---|---|
| `hookCalls` | Anzahl | Hook-Aufrufe insgesamt |
| `hookP50Us`, `hookP99Us`, `hookP999Us`, `hookMaxUs` | µs | Dauer der Hook-Aufrufe: Median, 99 %, 99,9 %, Höchstwert |
| `hookHistogram` | Anzahl je Klasse | Klassen bis 50, 100, 250, 500, 1000, 2000, 5000 µs und darüber |
| `hookOverHalfTimeout` | Anzahl | Aufrufe über der Hälfte des wirksamen Zeitlimits |
| `hostGcCount` | Anzahl | Speicherbereinigungen des Capture-Hosts nach der Aufwärmphase (Soll: 0) |
| `ringMaxFillPct` | Prozent | höchste Füllung des Eingabe-Rings |
| `ringOverflows`, `ringDroppedRecords`, `movesThinned` | Anzahl | Überläufe, verlorene Sätze, ausgedünnte Mausbewegungen |
| `rawOnlyEvents` | Anzahl | Fehlstellen: von Raw Input gemeldet, vom Hook nicht gesehen |
| `hookReinstalls`, `hostRestarts` | Anzahl | neu gesetzte Hooks, Neustarts des Capture-Hosts |
| `keystateCorrections` | Anzahl | Berichtigungen beim Abgleich des Tastenzustands |
| `framesTriggered`, `framesKey`, `framesDelta`, `framesSame`, `framesDropped` | Anzahl | Bildauslöser und ihre Ablage |
| `frameQueueMaxBytes` | Bytes | höchste Füllung der Bild-Warteschlangen |
| `frameEncodeP95Ms`, `frameWriteDelayP95Ms` | ms | Kodierzeit und Zeit vom Einreihen bis zum geschriebenen Blob |
| `degradePeriodicSkipped`, `degradeChangeCoalesced`, `degradeCompressionLowered`, `degradeSettleMerged` | Anzahl | Abbaustufen 1 bis 4 aus [4.8](#ueberlast) |
| `captureRebuilds`, `backendSwitches` | Anzahl | Neuaufbauten der Bildaufnahme, Wechsel des Aufnahmewegs |
| `uiaQueries`, `uiaP50Ms`, `uiaP95Ms`, `uiaTimeouts`, `uiaWorkersReplaced` | Anzahl bzw. ms | Abfragen der UI Automation |
| `snapshots`, `snapshotsPartial` | Anzahl | Text-Schnappschüsse, davon unvollständig |
| `lateMaxMs` | ms | größter Rückstand eines Nachzüglers (`tObs` − `t`) |
| `inputNearEpochChange` | Anzahl | Eingaben innerhalb von 100 ms um einen Epochenwechsel |
| `selfWindowViolations` | Anzahl | Verstöße aus der laufenden Überwachung der Unsichtbarkeit (Soll: 0) |

---

<a id="review"></a>
## 13 Zuordnung der Review-Befunde

| Befund des Architektur-Reviews | Lösung | Abschnitt, Anforderungen |
|---|---|---|
| Nr. 4: Der Hook-Wächter übersieht echte Verluste (einzelne Zeitüberschreitung, von fremden Programmen verschluckte Tasten, hängende Umschalter nach Win+L oder UAC) | Raw Input als zweiter Kanal mit Abgleich je Taste; Ergänzung mit Herkunftsvermerk; Abgleich des Tastenzustands nach jeder Lücke, protokolliert | [3.3](#eingabekanaele), [3.4](#kanalabgleich), [3.5](#tastenzustand); AUF-013 bis AUF-019 |
| Nr. 6: Element-Identität scheitert in den interessanten Fällen | Ermittlung beim Verweilen **und** beim Druck, beide mit Laufzeit-Kennung im Protokoll; Fenster unter dem Zeiger aus dem Hook-Aufruf; Gültigkeit des Vorab-Ergebnisses mit Grund; Vergleich in der Schritterkennung | [3.2](#eingabekanaele), [7.2](#doppelte-ermittlung); AUF-068 |
| Nr. 9: Speicherbereinigung im Prozess des Hooks | Regeln für den Hook-Aufruf; eigener Prozess laut 02; Budget 99,9 % unter 1 ms | [3.2](#eingabekanaele), [12.2](#leistungsbudgets); AUF-011, AUF-012 |
| Nr. 10: Bildregeln versagen unter Last und blockieren die Neuberechnung | Pin im Hook-Aufruf und Halteregel; Nachher-Bild spätestens das nächste Vorher-Bild; Auslöser unabhängig von Schrittregeln; Differenzbilder; Lücke `frameDropped` | [4.4](#vorher-bild)–[4.6](#bildspeicher), [4.8](#ueberlast); AUF-033 bis AUF-040, AUF-049 |
| Nr. 11: Zeitsemantik des Protokolls | Zeit des Geschehens und der Beobachtung je Quelle; Systemzeiten umgerechnet; Bündel enden an jedem anderen Ereignis; Rohdaten je Taste samt Layout und Übersetzung | [2](#zeitmodell), [3.6](#zeichenuebersetzung); AUF-004 bis AUF-007, AUF-023 |
| Nr. 12: Koordinaten brechen bei Topologiewechsel | Epochen, stabile Monitor-Kennung, monitorrelative Koordinaten, Zuordnung zum Zeitpunkt des Geschehens, Drehung, Plausibilität der Elementrechtecke | [4.11](#bildschirmaufnahme), [4.13](#topologie); AUF-048, AUF-050, AUF-051, AUF-078 |
| Nr. 13: Undurchsichtige Oberflächen liefern selbstsichere Unwahrheit | Einstufung je Fenster und Fläche; keine erfundenen Elemente; Mausweg als Primärdaten; „kalte" Fenster werden aufgewärmt | [7.4](#undurchsichtig), [7.8](#ui-automation); AUF-074, AUF-079 |
| Nr. 7 (Anteil des Kerns): Das Modell weiß nicht, was auf dem Bildschirm stand | Text-Schnappschuss jedes neuen Fensters, Dialogs, Menüs, Popups; Position unter Gleichnamigen, Zeilenkontext, Adresse; Fokus nach Tastenschritten | [7.3](#ui-automation), [7.5](#text-schnappschuss), [7.6](#ui-automation); AUF-070, AUF-071, AUF-075, AUF-076 |
| Nr. 1 und 2 (Anteil des Kerns): Rückhaltung gegen veralteten Fokus | Vordergrundfenster aus dem Hook-Aufruf; Strom „Fokus kann sich geändert haben"; Rückhaltepuffer in Eingabereihenfolge; Wertefilter | [11](#andockpunkte-datenschutz); AUF-089 |
| Fehlend: eigene Bedienung des Recorders | Hotkeys mit Kennzeichen `self`; Eingaben in Recorder-Fenster werden nicht geschrieben und stehen als Lücke `recorderInteraction`; Bild einfrieren vor dem ersten Recorder-Fenster | [3.9](#eigene-bedienung); AUF-027, AUF-028 |
| Fehlend: Erfassungsumfang (Benachrichtigungen, zweiter Monitor, Umschalter-Vorschauen) | Monitor mit Aktivität; Fensterarten für den Bildfilter; benannte Grenzen | [5.3](#aufnahmebereiche), [5.8](#aufnahmebereiche); AUF-054, AUF-060 |
| Fehlend: Anzeige- und Eingabe-Einstellungen (HDR, vertauschte Tasten, Doppelklickzeit, Einrastfunktion) | HDR-Kennzeichen und spätere Tonwertabbildung; Momentaufnahme der Eingabe-Einstellungen | [3.10](#eingabekanaele), [4.10](#bildschirmaufnahme); AUF-029, AUF-046 |

---

<a id="anforderungen"></a>
## 14 Anforderungen

AUF-097 bis AUF-109 gehören zu den nachträglich freigegebenen Funktionen (Aufnahmeprofile, Ziel-Angaben, Marker mit Typ, „Neu aufnehmen und ersetzen"). Die Funktionen sind Soll. Die Priorität „Soll (Schutzregel: Muss bei Umsetzung)" bedeutet: Ob die Funktion gebaut wird, ist Soll; wird sie gebaut, ist die Schutzregel der Anforderung Muss.

| ID | Anforderung | Priorität | Abnahmekriterium |
|---|---|---|---|
| AUF-001 | Der Kern durchläuft die Zustände und die Start- und Stoppreihenfolge aus Abschnitt 1; der Sitzungskopf steht vor dem ersten Eingabeereignis. | Muss | Formatprüfung F04 ([04](04-datenformat.md#absturzsicherheit)) besteht für jede Testsitzung. |
| AUF-002 | Die Vorprüfung wählt je Monitor den Aufnahmeweg und führt den Selbsttest für alle Monitore gleichzeitig aus. Vom Auslöser bis zum ersten aufgezeichneten Ereignis vergehen ohne Countdown höchstens 1,5 s auf Referenzsystem A. | Muss | Messung auf Referenzsystem A (ein Monitor) und C (zwei Monitore); Ergebnis je Prüfung in der Sitzung. |
| AUF-003 | Beobachtet wird nur zwischen Start bzw. Fortsetzen und Pause bzw. Stopp; die Anzeige ist vor Beginn der Beobachtung sichtbar. | Muss | In Bereit und Pause existiert kein Hook und kein Bildabruf; Mitschnitt des Starts zeigt den Rahmen vor dem ersten gespeicherten Bild. |
| AUF-004 | Alle Zeiten stammen aus dem Leistungszähler; `t` zählt Mikrosekunden ab dem Moment, in dem die Hooks aktiv sind. | Muss | Einheitstest der Umrechnung; kein Ereignis mit negativem `t`. |
| AUF-005 | Jedes Ereignis trägt die Zeit des Geschehens und, wenn abweichend, die der Beobachtung sowie die Zeitquelle nach der Tabelle in 2.2. | Muss | Stichprobe je Ereignisart gegen die Tabelle. |
| AUF-006 | Systemzeiten aus Hook-Strukturen und WinEvents werden über Stützpaare umgerechnet; unplausible Ergebnisse werden verworfen. | Soll | Fensterereignisse der Test-Zielanwendung liegen innerhalb von ±20 ms um deren eigenes Protokoll. |
| AUF-007 | Je Kanal werden Hook-Eingaben in der Reihenfolge ihres Geschehens geschrieben. Jedes Eingabeereignis aus dem Hook trägt die Eingabenummer `inSeq` (Pflicht); sie hält die Reihenfolge über beide Kanäle fest, auch wenn der Schutzfilter Tasten zurückhält. | Muss | Formatprüfungen F10 und F18; T-02 (10 000 Ereignisse in richtiger Reihenfolge, kein Hook-Ereignis ohne `inSeq`, keine unerklärte Lücke in der Nummernfolge). |
| AUF-008 | Verstellen der Systemuhr und Ruhezustand werden protokolliert und stören die Sitzungsuhr nicht. | Soll | Uhr während der Aufnahme verstellen; Ruhezustand: Ereignisse vorhanden, `t` monoton. |
| AUF-009 | Die Low-Level-Hooks erfassen jedes Maus- und Tastaturereignis mit den unveränderten Feldern der Hook-Struktur. | Muss | T-02: Rohprotokoll stimmt mit dem Protokoll der Test-Zielanwendung überein. |
| AUF-010 | Der Recorder verändert, verzögert und verschluckt keine Eingabe und erzeugt selbst keine. **Ausdrückliche Ausnahme:** Die registrierten Hotkeys des Recorders erreichen die Zielanwendung nicht. | Muss | Test-Zielanwendung empfängt mit und ohne Recorder dieselbe Eingabefolge – ausgenommen die registrierten Hotkey-Kombinationen; Codeprüfung des Hook-Pfads (jeder Aufruf wird weitergereicht). |
| AUF-011 | Der Hook-Aufruf hält die Regeln aus 3.2 ein (keine Anforderung, keine Sperre, keine Ein-/Ausgabe, immer weiterreichen). | Muss | Codeprüfung; Zähler der Speicherbereinigungen des Hosts unverändert (ARC-003). |
| AUF-012 | 99,9 % der Hook-Aufrufe dauern unter 1 ms; kein Aufruf über der Hälfte des wirksamen Zeitlimits. | Muss | VE-01 und Lasttest P4 über 30 min auf A, B, C: Histogramm des Hosts. |
| AUF-013 | Raw Input läuft als zweiter Kanal für Tastatur (Muss) sowie Maustasten und Rad (Soll). | Muss | Raw-Sätze im Ring während T-02; Abgleichszähler in der Sitzung. |
| AUF-014 | Hook- und Raw-Ereignisse werden je Taste abgeglichen; was der Hook nicht sah, wird ergänzt und als ergänzt gekennzeichnet. | Muss | Test mit einem Programm, das Tasten verschluckt (VE-02): Die Tasten stehen als ergänzt im Protokoll. |
| AUF-015 | Ein verlorener Hook wird innerhalb von 1 s nach dem ersten betroffenen Ereignis erkannt, neu gesetzt und als Lücke `hookLost` vermerkt. | Muss | Hook im Testbuild künstlich entfernen: Lücke mit richtigem Beginn; Eingaben danach vollständig. |
| AUF-016 | Lässt sich die Eingabeerfassung nicht wiederherstellen, pausiert die Aufnahme mit sichtbarer Meldung. | Muss | Fehlerinjektion: wiederholter Hook-Verlust → Pause und Meldung. |
| AUF-017 | Ein Überlauf des Eingabe-Rings wird gezählt und als Lücke `overload` vermerkt; Tasten und Maustasten gehen erst bei vollem Ring verloren. | Muss | Hauptprozess 60 s anhalten: Lücke mit Zählern; keine Lücke ohne Marker. |
| AUF-018 | Jeder Bewegungsmesswert der Maus wird gespeichert und nach 04 gebündelt; ausgedünnt wird nur bei fast vollem Eingabe-Ring und dann mit Lücke. | Muss | T-02 mit einer 1000-Hz-Maus; Formatprüfung F09. |
| AUF-019 | Der Tastenzustand wird mitgeführt und zu den Anlässen aus 3.5 abgeglichen und protokolliert. | Muss | Win+L und UAC-Abfrage während der Aufnahme: Danach werden getippte Buchstaben nicht als Tastenkombination gedeutet. |
| AUF-020 | Die Zeichenübersetzung verändert den Tastaturzustand des Systems nicht. | Muss | 1000 Zeichen mit Tottasten in einen Editor tippen, mit und ohne Recorder: identischer Text. |
| AUF-021 | Tottasten und AltGr werden layoutgerecht übersetzt. | Muss | Testmatrix Deutsch, Englisch (US), Französisch: `ch` stimmt mit dem Feldinhalt überein. |
| AUF-022 | Eingaben über Alt+Ziffernblock werden erkannt und mit entstandenem Zeichen vermerkt. | Soll | Test mit Codes mit und ohne führende Null. |
| AUF-023 | Zu jeder Taste stehen Tastencode, Scancode, Kennzeichen, Layout und die Übersetzung zum Aufnahmezeitpunkt im Protokoll. | Muss | Die Übersetzung lässt sich aus den Rohfeldern für die Testlayouts nachrechnen. |
| AUF-024 | Künstlich erzeugte Eingaben sind gekennzeichnet und werden nicht gefiltert. | Muss | Eingabe-Skripter: alle Ereignisse tragen das Kennzeichen. |
| AUF-025 | Die Zeigerart (Maus, Touch, Stift) ist gekennzeichnet. | Soll | Test mit Touchscreen und Stift. |
| AUF-026 | Tasten bei aktiver Eingabemethode sind gekennzeichnet; der entstandene Text steht über Feldinhalt bzw. `ui.text` im Protokoll. | Soll | Testmatrix Japanisch/Chinesisch. |
| AUF-027 | Bedienung des Recorders wird nie zu Nutzerschritten: Hotkeys sind gekennzeichnet; Tasten, Klicks und Rad in Fenstern des Recorders werden nicht als Eingabeereignisse geschrieben, ihre Zeitspanne steht als Lücke `recorderInteraction`. | Muss | Kommentar mit bekanntem Text eingeben und Leiste anklicken: Der Text steht nur im `comment`; kein `key.*` und kein `mouse.down` dazu im Protokoll; Lücke vorhanden. |
| AUF-028 | Kommentar, Schnappschuss und Marker frieren das Bild ein, bevor ein Fenster des Recorders erscheint. | Muss | Offenes Menü der Test-Zielanwendung, Kommentar-Hotkey: Das eingefrorene Bild zeigt das Menü. |
| AUF-029 | Die Eingabe-Einstellungen aus 3.10 stehen im Sitzungskopf; Änderungen werden protokolliert. | Soll | Doppelklickzeit während der Aufnahme ändern → `system`-Ereignis. |
| AUF-030 | Geräte lassen sich je Eingabe benennen. | Kann | Zwei Tastaturen: Ereignisse tragen unterschiedliche Gerätekennungen. |
| AUF-031 | Meldet das System Eingaben, die kein Kanal sah, entsteht eine Lücke mit der nach Lage bestimmten Ursache. | Soll | Adminfenster im Vordergrund, dort tippen: Lücke `elevatedWindow`. |
| AUF-032 | Je Monitor wird der erste funktionierende Aufnahmeweg der Kette gewählt und protokolliert; ein Ausfall führt zum nächsten Weg. | Muss | Duplizierung im Testbuild sperren: Rückfall, `system`-Ereignis, Aufnahme läuft weiter. |
| AUF-033 | **Vorher-Bild-Garantie:** Zu jeder auslösenden Hook-Eingabe gibt es ein Bild, dessen Darstellungszeit vor dem Zeitstempel der Eingabe liegt. | Muss | T-04: Test-Zielanwendung mit Zähler, 1000 von 1000 Vorher-Bildern zeigen den alten Zähler – auch mit Video im Hintergrund. |
| AUF-034 | Ein für eine Eingabe benötigtes Bild wird nicht überschrieben, bevor es ausgelesen ist. | Muss | T-04 unter Lastprofil P4; Zähler `frameDropped` bleibt 0. |
| AUF-035 | Die Bildauslöser sind technisch bestimmt und unabhängig von den Schrittregeln. | Muss | Dieselbe Rohaufnahme, zwei Regelversionen der Schritterkennung: Jeder Schritt beider Versionen hat Bilder. |
| AUF-036 | Jede auslösende Eingabe hat ein Nachher-Bild: bei Beruhigung, bei Erreichen der Obergrenze oder als Vorher-Bild der nächsten Eingabe. | Muss | Test mit Daueranimation: Jedes `preInput` hat ein `settle` mit Grund. |
| AUF-037 | Wesentliche Änderungen ohne Eingabe und neu erscheinende Fenster erzeugen Zwischenbilder. | Muss | Test-Zielanwendung öffnet zeitgesteuert einen Dialog für 400 ms: Ein Bild zeigt ihn. |
| AUF-038 | Dauerbewegte Bereiche verhindern die Beruhigung des übrigen Bildschirms nicht. | Soll | Video im Fenster, Klick daneben: `settle` mit Grund `quiet`. |
| AUF-039 | Schlüssel- und Differenzbilder werden nach 4.6 erzeugt; die Wiederherstellung ist pixelgleich. | Muss | Formatprüfung F12 im gründlichen Modus für alle Testsitzungen. |
| AUF-040 | Ein Bildauslöser ohne Pixeländerung belegt keinen Speicher. | Muss | 100 Tastendrücke ohne Bildänderung: 100 `frame`-Ereignisse, kein neuer Blob. |
| AUF-041 | Gespeicherte Bilder enthalten keinen Mauszeiger; Form und Position stehen getrennt. | Muss | Pixelvergleich mit Referenz ohne Zeiger; Ausnahme nur mit Kennzeichen. |
| AUF-042 | Formwechsel des Zeigers werden protokolliert; Standardformen sind benannt. | Soll | Zeiger über Link und Textfeld bewegen: benannte Formen im Protokoll. |
| AUF-043 | Nach Verlust des Zugriffs wird die Aufnahme selbsttätig wieder aufgebaut; die Zeit ohne Bild ist als Lücke vermerkt. | Muss | Auflösung während der Aufnahme ändern: Aufnahme läuft weiter, neues Schlüsselbild, Lücke falls Eingaben betroffen. |
| AUF-044 | Der sichere Desktop wird erkannt (Eingabedesktop alle 250 ms) und als Lücke `secureDesktop` mit der Art `uac` oder `other` vermerkt; sie beginnt sofort und endet nach zwei aufeinanderfolgenden „Default“-Lesungen. | Muss | UAC-Abfrage und Sperrbildschirm: Lücke mit Beginn und Ende; kein Bild des sicheren Desktops; ein einzelnes „Default“-Flackern beendet die Lücke nicht (Test der Zustandslogik). |
| AUF-045 | Vom System ausgeblendete geschützte Inhalte sind am Bild und als Lücke gekennzeichnet. | Muss | Geschütztes Testfenster: Kennzeichen und Lücke vorhanden. |
| AUF-046 | HDR-Monitore werden aufgenommen; betroffene Bilder sind gekennzeichnet. | Muss | Testmatrix „HDR an": Bilder vorhanden und gekennzeichnet. |
| AUF-047 | Auf Systemen mit Hybrid-Grafik funktioniert die Aufnahme ohne Zutun, notfalls über den Rückfallweg. | Muss | VE-10 und Referenzsystem C: Aufnahme mit Vorher-Bild-Garantie. |
| AUF-048 | Gedrehte Monitore werden in Darstellungsrichtung gespeichert. | Muss | Hochformat-Monitor: Bildmaße und Klickpositionen stimmen (VE-06). |
| AUF-049 | Bei Überlast wird in der Reihenfolge aus 4.8 abgebaut; jedes fehlende Bild ist als `frameDropped` vermerkt. | Muss | Datenträger im Test drosseln: Zähler und Lücken stimmen überein; kein fehlendes Bild ohne Marker. |
| AUF-050 | Änderungen der Monitor-Anordnung beenden die Aufnahme nicht; sie erzeugen eine neue Epoche mit stabilen Monitor-Kennungen. | Muss | Abdocken und Umstecken während der Aufnahme (Testmatrix): Aufnahme läuft weiter; derselbe Monitor behält seine Kennung. |
| AUF-051 | Alle Koordinaten sind physische Pixel, monitorrelativ und über Hook, Elemente und Bilder hinweg konsistent. | Muss | VE-06: Abweichung höchstens ±1 Pixel bei 100 % + 150 % und Hochformat. |
| AUF-052 | Je Monitor erfolgen höchstens 30 Bildabrufe je Sekunde. | Soll | Messung in P3. |
| AUF-053 | Der Kern setzt die Aufnahmebereiche Bildschirm, Anwendung und Rechteck für Bilder **und** Eingaben durch. | Muss | T-06 sowie entsprechende Tests für Monitor und Rechteck. |
| AUF-054 | Bei „Monitor mit Aktivität" wird nur der Monitor gespeichert, auf dem gearbeitet wird. | Muss | Zwei Monitore, Arbeit nur auf einem: Für den anderen entsteht außer dem Anfangsbild kein Blob. |
| AUF-055 | Die Zugehörigkeit von Fenstern zu einer Anwendung folgt der Rangfolge aus 5.4 und steht je Fenster im Protokoll. | Muss | T-06 mit Dialogen, Menüs, Dateiauswahl und Neustart der Anwendung. |
| AUF-056 | Pixel außerhalb des Aufnahmebereichs erreichen den Datenträger nicht; bei bewegten Fenstern wird vorsichtig entfernt. | Muss | Fremdes Testfenster mit Kanarien-Muster überlappt und kreuzt die Zielanwendung: Das Muster kommt in keinem Blob vor. |
| AUF-057 | Verdeckte Teile der Anwendung sind schwarz gefüllt und als verdeckt gekennzeichnet. | Muss | T-06: `frame.masks` mit `occluded` deckt die Überlappung. |
| AUF-058 | Tasten, Maustasten, Rad und Mausbewegungen außerhalb des Bereichs werden nie aufgezeichnet, sondern nur gezählt und als Lücke `outOfScope` mit Zählern je Art vermerkt. Eine Option zum Mitprotokollieren gibt es nicht. | Muss | T-06: Kanarien-Text, außerhalb getippt, kommt im Sitzungsordner nicht vor; kein `mouse.move` mit Position außerhalb des Bereichs; Lücke mit Zählern vorhanden. |
| AUF-059 | Ein Wechsel des Bereichs während der Aufnahme wird protokolliert und gilt ab der nächsten Eingabe. | Muss | Wechsel von Bildschirm zu Anwendung: `scope.change`, Schlüsselbild, danach nur noch die Anwendung in den Bildern. |
| AUF-060 | Benachrichtigungen und Fensterumschalter sind als Fensterarten erkennbar und lassen sich über den Bildfilter schwarz füllen. | Soll | Testbenachrichtigung: Fensterart `toast`; mit eingeschaltetem Filter ist die Fläche gefüllt. |
| AUF-061 | Jedes Fenster des Recorders ist vor seiner ersten Darstellung von Bildschirmaufnahmen ausgenommen. | Muss | T-05 mit Leiste, Rahmen, Kommentarfeld, Tooltip, Menü. |
| AUF-062 | Während der Aufnahme hat der Recorder keinen Eintrag in Taskleiste und Fensterumschalter, kein Symbol im Infobereich und zeigt keine Windows-Benachrichtigung. | Muss | T-05; Bild des Fensterumschalters während der Aufnahme. |
| AUF-063 | Bei jedem Aufnahmestart beweist ein Selbsttest je Monitor und Aufnahmeweg, dass Recorder-Fenster fehlen und der Inhalt dahinter sichtbar ist. | Muss | Testbuild mit abgeschalteter Ausnahme: Selbsttest schlägt fehl; mit Ausnahme besteht er; Dauer ≤ 300 ms je Monitor. |
| AUF-064 | Schlägt der Selbsttest auf allen Wegen fehl, wird nur im Ausweichmodus aufgenommen; auch dort erscheint kein Recorder-Fenster im Bild. | Muss | Ausweichmodus im Testbuild erzwingen: T-05 besteht; Garantiestufen der Vorher-Bilder entsprechen 6.4. |
| AUF-065 | Der Hauptprozess überwacht laufend, dass alle seine sichtbaren Fenster ausgenommen sind. | Soll | Zähler der Verstöße bleibt in allen Tests null. |
| AUF-066 | Ein bekanntes Testmuster wird mit darüberliegenden Recorder-Fenstern auf jedem Aufnahmeweg pixelgleich aufgenommen. | Muss | T-05 unter Windows 10 22H2 und Windows 11. |
| AUF-067 | Die UI Automation läuft auf eigenen Threads ohne Fenster, mit Zeitlimits und ausschließlich lesend. | Muss | ARC-017, ARC-018; ein hängendes Testprogramm blockiert weder Eingabe- noch Bildpfad. |
| AUF-068 | Das Ziel einer Mauseingabe wird beim Verweilen **und** beim Druck ermittelt; beide Ergebnisse und die Gültigkeit des Vorab-Ergebnisses stehen im Protokoll. | Muss | VE-04; die drei Fälle aus 7.2 gegen die Test-Zielanwendung liefern die beschriebenen Vermerke. |
| AUF-069 | Zu einem Element werden die Eigenschaften aus 04 in einem Aufruf gelesen, soweit die Anwendung sie anbietet. | Muss | Stichprobe gegen ein Prüfwerkzeug für UI Automation in den Anwendungsarten der Testmatrix. |
| AUF-070 | Der Pfad bis zum Fenster, die Position unter gleichnamigen Elementen und der Zeilenkontext werden ermittelt, jeweils zeitbegrenzt. | Muss | Tabelle mit zwanzig gleich beschrifteten Schaltflächen: Zeilenkontext und Position unterscheiden jeden Klick. |
| AUF-071 | Für Browser wird die Adresse des Dokuments ermittelt. | Soll | Chrome, Edge, Firefox: `url` am Element. |
| AUF-072 | Element-Ermittlungen halten das Zeitbudget; ein hängender Aufruf wird durch einen Ersatzarbeiter abgelöst und endet mit Fehlstatus. | Muss | Testprogramm, das 10 s nicht antwortet: Ermittlung endet nach dem Limit mit `timeout`; folgende Ermittlungen funktionieren. |
| AUF-073 | Liefert die UI Automation nichts Brauchbares, folgen MSAA und Win32; die Quelle steht am Ereignis. | Soll | Testprogramm ohne UIA-Anbieter: Ergebnis mit Quelle `msaa` oder `win32`. |
| AUF-074 | Undurchsichtige Oberflächen werden erkannt und als solche vermerkt; es werden keine Elemente erfunden. | Muss | Testmatrix „Undurchsichtige Oberflächen": Einstufung stimmt; Klicks tragen den Hinweis statt eines Elementnamens. |
| AUF-075 | Von jedem neu erscheinenden Fenster, Dialog, Menü und Popup im Aufnahmebereich entsteht ein begrenzter Text-Schnappschuss. | Muss | Meldungsdialog und Kontextmenü der Test-Zielanwendung: Wortlaut und Einträge stehen im Schnappschuss. |
| AUF-076 | Fokuswechsel werden verfolgt; für Felder mit lesbarem Inhalt wird der Inhalt beim Betreten, bei Änderung und beim Verlassen festgehalten. | Muss | Tippen, Einfügen und Autovervollständigung in ein Textfeld: Feldinhalt vorher und nachher stimmt mit der Test-Zielanwendung überein. |
| AUF-077 | Für das fokussierte Element werden Wert-, Zustands-, Auswahl- und Auslöse-Ereignisse aufgezeichnet; Mitteilungen der Anwendung ebenfalls. | Soll | Kontrollkästchen per Leertaste umschalten: `ui.toggle`; Statusmeldung: `ui.notification`. |
| AUF-078 | Unplausible Elementrechtecke werden nicht als Rechteck gespeichert, sondern mit Grund vermerkt. | Muss | Testanwendung ohne DPI-Unterstützung auf 150 %: Kein Rechteck, das den Klickpunkt nicht enthält. |
| AUF-079 | Chromium- und Firefox-Fenster werden beim Vordergrundwechsel aufgewärmt; eine noch nicht bereite Barrierefreiheit führt nicht zur Einstufung „undurchsichtig". | Soll | Erster Klick nach dem Start von Chrome und VS Code liefert ein Element (VE-04). |
| AUF-080 | Die WinEvents aus 8.1 werden aufgezeichnet; Rückrufe reihen nur ein. | Muss | T-02 mit Fensteraktionen: Fensterereignisse stimmen mit der Test-Zielanwendung überein. |
| AUF-081 | Das Fensterregister führt je Fenster die Angaben aus 8.2; ein hängendes Programm hält es nicht auf. | Muss | Hängendes Testprogramm im Vordergrund: Fensterereignisse anderer Programme erscheinen weiter innerhalb von 500 ms. |
| AUF-082 | Sperren, Benutzerwechsel, Trennen, Ruhezustand, Abmelden und Herunterfahren werden erkannt und als Ereignis und Lücke vermerkt; die Sitzung bleibt lesbar. Nach Sperren, Benutzerwechsel und Trennen bleibt die Aufnahme pausiert, bis das Fortsetzen bestätigt ist. | Muss | Testmatrix „Sonderfälle"; Sperren während der Aufnahme: Lücke `sessionLocked`, nach dem Entsperren Lücke `paused` und Rückfrage; ohne Bestätigung wird nichts aufgezeichnet. |
| AUF-083 | Fenster mit höheren Rechten im Vordergrund erzeugen die Lücke `elevatedWindow`; Bilder laufen weiter. | Muss | Programm mit Adminrechten bedienen: Lücke vorhanden, Bilder zeigen das Geschehen. |
| AUF-084 | Anzeigeänderungen werden über Nachrichten und über den Verlust der Duplizierung erkannt. | Muss | Skalierung während der Aufnahme ändern: neue Epoche innerhalb von 1 s. |
| AUF-085 | Jede Änderung der Zwischenablage wird mit Herkunft protokolliert (Folgenummer, bereitstellender Prozess, Formate). Ohne eingeschaltete Inhaltsaufzeichnung wird der Inhalt gar nicht abgerufen und kein Format angefordert; Vertraulichkeitsmarken der Quelle werden beachtet. | Muss | Testanwendung, die Formate verzögert bereitstellt: Ohne Inhaltsaufzeichnung erhält sie keine Aufforderung zum Bereitstellen. Kopieren aus einem Passwortmanager mit Ausschlussmarke: kein Inhalt, keine Größe. Kanarien-Text kommt im Sitzungsordner nicht vor. |
| AUF-086 | Der Kern verändert die Zwischenablage nie; mit Inhaltsaufzeichnung hält er sie höchstens 5 ms geöffnet, ohne öffnet er sie nicht. Der Vergleich mit früheren Inhalten findet nur bei eingeschalteter Inhaltsaufzeichnung und nur im Arbeitsspeicher statt. | Muss | Kopieren und Einfügen von 1000 Einträgen mit und ohne Recorder: identisches Ergebnis, keine Zugriffsfehler in der Testanwendung; im Sitzungsordner findet sich kein Hash eines Inhalts. |
| AUF-087 | Alle Ursachen des Katalogs in 10.1 werden erkannt; Lücken werden bei Beginn und Ende geschrieben. | Muss | Je Ursache ein Test; harte Beendigung während einer Lücke: Der Beginn steht im Protokoll. |
| AUF-088 | Nichts wird ohne Zähler und Marker verworfen. | Muss | Fehlerinjektion an Ring, Warteschlangen und Datenträger: Summe der Zähler = Summe der Marker. |
| AUF-089 | Eingaben, Werte, Titel, Zwischenablage-Inhalte und Bilder erreichen Schreiber und Bildkodierung nur über die Andockpunkte A1 bis A7; die fünf Zusagen aus Abschnitt 11 gelten. | Muss | T-07 (Kanarien-Passwort); Architekturtest: kein Schreibzugriff an den Andockpunkten vorbei. |
| AUF-090 | Die Prozessorlast hält die Zielwerte aus 12.5 ein. | Soll | Messung je Profil auf A und B. |
| AUF-091 | Arbeits- und Grafikspeicher halten die Zielwerte aus 12.6 ein. | Soll | Messung in P2 und P4 auf A und B. |
| AUF-092 | Sitzungsgröße und Schreibdurchsatz halten die Zielwerte aus 12.7 ein; ein zu langsamer Datenträger führt zu Warnung und markiertem Abbau. | Soll | VE-09; Test mit gedrosseltem Datenträger. |
| AUF-093 | Start und Stopp halten die Zeiten aus 12.8 ein; für den Start gilt die Obergrenze von 1,5 s aus AUF-002. | Soll | Messung auf A. |
| AUF-094 | Im Dauerlauf bleiben Speicher, Handles und Hook-Dauer stabil. | Soll | T-13 (8 Stunden). |
| AUF-095 | Der Kern legt die Leistungszähler aus 12.9 beim Stopp im Manifest ab. | Soll | `manifest.performance` enthält die Zähler; Werte stimmen mit einer externen Messung überein (ARC-045). |
| AUF-096 | Für Eingaben, deren Fenster-Stempel zu einer ausgeschlossenen Anwendung gehört, wird keine Eingabenummer vergeben; die Nummernfolge läuft nach der Lücke `excludedApp` nahtlos weiter. | Muss | Bekannte Zahl von Tasten und Mausbewegungen in einem ausgeschlossenen Testfenster, auch unmittelbar nach dessen Erscheinen: `inSeq` des letzten Ereignisses davor und des ersten danach sind aufeinanderfolgend; die Lücke nennt keine Zähler. |
| AUF-097 | Der Kern erhält vor der Vorprüfung geprüfte wirksame Einstellungen und hält sie für den ganzen Aufnahmeabschnitt fest; er liest selbst keine Aufnahmeprofile. `session.start` nennt die wirksamen Einstellungen, das Manifest zusätzlich den Namen des Profils. | Soll | Aufnahme mit Profil starten und währenddessen `profiles.json` und die Einstellungen ändern: Verhalten bis zum Stopp unverändert; `session.start` nennt die wirksamen Werte, `manifest.profile` den Namen; Architekturtest: Der Aufzeichnungskern verweist nicht auf die Komponente Aufnahmeprofile. |
| AUF-098 | Ein Profil stellt nur Parameter ein: Andockpunkte, Anzeige der Aufnahme, Selbsttest, Lückenerkennung und Budgets gelten mit jedem Profil; die Ausschlussliste kann ein Profil nur erweitern. | Soll (Schutzregel: Muss bei Umsetzung) | T-05 und T-07 bestehen mit jedem mitgelieferten Profil; Profil mit einer Ergänzung der Ausschlussliste: Das Testfenster ist ausgeschlossen; kein Profilwert entfernt einen Eintrag der Liste. |
| AUF-099 | Ziel-Angaben des Autors stehen im Manifest (`manifest.goal`) und im Sitzungskopf des ersten Abschnitts. Was der Autor dafür tippt, erscheint nicht als Eingabeereignis; in Diagnoseprotokollen stehen die Texte nie. | Soll | Kanarien-Texte in allen drei Feldern: im Manifest und im Kopf vorhanden; im Journal kein `key.*` und kein `text.masked` aus der Zeit der Eingabe; Suche in `logs\` findet sie nicht; leere Felder fehlen. |
| AUF-100 | Marker tragen einen von vier Typen (`note`, `error`, `expected`, `important`) mit je eigener Kürzel-Funktion. Per Kürzel entstehen sie ohne Fenster und Fokuswechsel; ihre Tasten sind als eigene Bedienung gekennzeichnet und werden nie zu Nutzerschritten; das Bild wird zum Zeitpunkt des Kürzels eingefroren. Wird an der Leiste eine Bezeichnung eingegeben ([07](07-aufnahme-bedienung.md), BED-134), gilt diese Eingabe ebenfalls als eigene Bedienung: Sie steht nur als Text im `marker`-Ereignis (höchstens 200 Zeichen, unterliegt der Durchsicht), nie als Tastenereignis; Zeitpunkt des Markers ist der erste Klick. | Soll | Offenes Menü der Test-Zielanwendung, je Typ das Kürzel: Das Menü bleibt offen; `marker` mit dem Typ und Verweis auf ein Bild mit `beforeInput`, das das Menü zeigt; die Schritterkennung erzeugt aus den Tasten keinen Schritt. |
| AUF-101 | Zu Markern der Typen `error` und `expected` entsteht ein Text-Schnappschuss des Vordergrundfensters, sofern es im Aufnahmebereich liegt und nicht ausgeschlossen ist. | Soll | Meldungsdialog der Test-Zielanwendung, Fehler-Marker: Der Wortlaut steht im `ui.snapshot` mit Bezug auf den Marker; mit einer ausgeschlossenen Anwendung im Vordergrund entsteht keiner. |
| AUF-102 | Ein weiterer Aufnahmeabschnitt hängt an eine abgeschlossene Sitzung an, ohne eine vorhandene Zeile oder ein vorhandenes Bild zu ändern. `seq`, Sitzungsuhr, Eingabenummer, Epochen und Kennungen laufen fort; der Abschnitt beginnt mit vollständigem Sitzungskopf, neuer Epoche und Schlüsselbildern. | Soll | Abschnitt anhängen: Die Bytes des Journals bis zur alten Länge sind unverändert (Prüfsumme); die Formatprüfungen F03, F04, F07, F11, F13 und F18 bestehen; kein Differenzbild des neuen Abschnitts verweist auf ein Bild des alten. |
| AUF-103 | Jeder Aufnahmeabschnitt hat eine eigene Uhrenbasis; die Zeit zwischen den Abschnitten zählt nicht zur Sitzungsuhr. | Soll | Abschnitt nach einem Neustart des Rechners und auf einem zweiten Rechner anhängen: `t` fällt nicht, der Beginn ist gleich dem Ende des vorigen Abschnitts; die Wanduhrzeit jedes Ereignisses stimmt auf 1 s. |
| AUF-104 | Ein weiterer Abschnitt startet mit dem Aufnahmebereich der Sitzung – beim Ersetzen mit dem der zu ersetzenden Schritte. Lässt er sich nicht herstellen, folgt die passende Auswahl, über die Kommandozeile Exit-Code 7; nie wird stillschweigend ein anderer Bereich verwendet. | Soll | Je Bereichsart ein Test „herstellbar" und „nicht herstellbar" (Anwendung beendet, Monitor abgezogen, Auflösung geändert); `scope.change` im Kopf des neuen Abschnitts nennt den verwendeten Bereich. |
| AUF-105 | Ein weiterer Abschnitt beginnt nur, wenn die Voraussetzungen aus 1.7 erfüllt sind; andernfalls bleibt die Sitzung unberührt. Solange er läuft, gilt die ganze Sitzung als laufende Aufnahme. | Soll | Gepackte, gesperrte und abgestürzte Sitzung: kein Start, der Datei-Vergleich zeigt keine Änderung; während des Abschnitts liefern MCP-Server und `export` die Sitzung nicht aus. |
| AUF-106 | Ein weiterer Abschnitt übernimmt die wirksamen Einstellungen seines Bezugsabschnitts; die Ausschlussliste wird mit der aktuell geltenden vereinigt. Schützt das Ergebnis schwächer als die aktuellen Einstellungen, wird das vor Beginn gemeldet. | Soll | Sitzung mit der Stufe „Schutznetz", aktuelle Einstellung „Streng", dazu ein neuer Eintrag in der Ausschlussliste: Der Abschnitt nimmt mit „Schutznetz" auf, die Startmeldung nennt die Abweichung, das neu ausgeschlossene Testfenster ist ausgeschlossen. |
| AUF-107 | Ein Ersatz-Abschnitt nennt in seinem `session.start` den Zweck (`purpose: "replacement"`) und den Bereich (`replaces`). Nach seinem sauberen Stopp trägt die Aufzeichnungssteuerung die Ersetzung in `edits.replacements` ein; bei Absturz oder Verwerfen entsteht kein Eintrag. Die ersetzten Rohdaten bleiben unverändert im Journal. | Soll | Schritte 3 bis 5 neu aufnehmen: Sitzungskopf und Eintrag nennen denselben Bereich, `by.section` ist der neue Abschnitt; die alten Ereignisse stehen unverändert im Journal; Formatprüfung F22 besteht. Absturz während der Neuaufnahme: kein Eintrag, der Abschnitt gilt nach der Wiederherstellung als nicht verwendete Neuaufnahme. |
| AUF-108 | Wird ein weiterer Abschnitt verworfen, wird das Journal nicht gekürzt: Der Abschnitt endet wie bei einem Stopp und wird als verworfen gekennzeichnet. Die Leseschicht hält ihn aus allen Ausgaben heraus; frühere Abschnitte und die Sitzung bleiben unberührt. | Soll | Kanarien-Text in einem weiteren Abschnitt tippen und den Abschnitt verwerfen: Das Journal ist bis zur alten Länge bytegleich und endet mit `session.stop`; die Sitzung liegt nicht im Papierkorb; der Kanarien-Text kommt in keiner Ausgabe vor – Anleitung, KI-Paket, Antworten des MCP-Servers, Suche und `cache/search/`, Abgleich, Support-Paket. Ein danach angehängter Abschnitt übernimmt Bereich und Einstellungen nicht vom verworfenen. |
| AUF-109 | Der Start eines weiteren Abschnitts hält das Zeitziel aus 12.8 ein. | Soll | Messung auf Referenzsystem A mit einem Journal von 100 MB. |
| AUF-110 | Bei sicherem Desktop, Sperre, Trennen und Ruhezustand wird die Bildaufnahme ausgesetzt: Ringpuffer verworfen, offenes Nachher-Bild entfällt, kein Bild aus einer Lücke, nie ein schwarzes Bild im Ringpuffer; danach `keystate.sync` (bei sicherem Desktop mit `reason: secureDesktop`) und ein erzwungenes Schlüsselbild (4.14, 8.3). | Muss | Synthetische UAC-Lücke und Sperre: kein Bild im Lückenzeitraum; Schlüsselbild nach dem Ende; Validator fehlerfrei. |
| AUF-111 | Der Schrittbau (Version 1.5.0) macht aus einer UAC-Lücke den Schritt „Benutzerkontensteuerung bestätigen“ ohne Bild; Nachher-Bilder werden nie aus einer Lücke ohne Bildschirm gebunden. | Soll | Test mit synthetischer UAC-Lücke (Titel deutsch und englisch, kein Bild, Hauptschritt). |
| AUF-112 | Die Erinnerungsuhr meldet „Aufnahme läuft noch“ nach der eingestellten Leerlaufzeit (höchstens dreimal je Phase) und „Privattaste ist noch an“ einmal je Einschalten; in Pause, Sperre, sicherem Desktop und Lücken stehen die Uhren; sie erinnert nur (3.11). | Soll | Test der reinen Zustandslogik mit künstlicher Zeit. |

---

<a id="offene-punkte"></a>
## 15 Offene Punkte und Vorab-Experimente

### 15.1 Bezug zu den Experimenten aus 11

| Experiment | Betrifft hier |
|---|---|
| VE-01 | Hook-Aufruf, Budget ([3.2](#eingabekanaele), [12.2](#leistungsbudgets)) |
| VE-02 | Raw Input bei verschluckten Tasten und Adminfenstern ([3.3](#eingabekanaele), [3.4](#kanalabgleich)) |
| VE-03 | Vorher-Bild-Garantie ([4.4](#vorher-bild)) |
| VE-04 | Trefferquote und Dauer der Element-Ermittlung ([7](#ui-automation)) |
| VE-05 | Ausnahme der eigenen Fenster auf allen Aufnahmewegen ([6](#unsichtbarkeit)) |
| VE-06 | Koordinaten bei gemischter Skalierung ([4.13](#topologie)) |
| VE-08 | Rückhaltung ([11](#andockpunkte-datenschutz)) |
| VE-09 | Bildspeicher und Kodierzeiten ([4.6](#bildspeicher), [12](#leistungsbudgets)) |
| VE-10 | Hybrid-Grafik, HDR, Remote-Sitzungen ([4.9](#bildschirmaufnahme)–[4.12](#bildschirmaufnahme)) |

### 15.2 Zusätzlich nötige Prüfungen

Für diese Punkte gibt es in [11](11-roadmap-tests-risiken.md) noch kein Experiment. Vorschlag: als Erweiterung der genannten Experimente oder als eigene kleine Prüfungen in M0 aufnehmen.

| Nr. | Frage | Vorschlag | Ausweichweg |
|---|---|---|---|
| Z-1 | Wie liest ein Prozess ohne Tastaturfokus die Feststeller verlässlich? | eigene Prüfung | mitgeführter Wert; Gegenprobe über den Feldinhalt |
| Z-2 | Melden Hook und Raw Input bei vertauschten Maustasten die physische oder die logische Taste? | Erweiterung VE-06 | Zuordnung über die Einstellung in der Sitzung |
| Z-3 | Windows.Graphics.Capture: Bedeutung der Systemzeit des Bilds; gelber Rahmen im Bild; rahmenlos und überhaupt nutzbar ohne Paketidentität? | Erweiterung VE-05/VE-10 | Garantie nur über Eintreffzeit; bei Rahmen im Bild Rückfall auf GDI |
| Z-4 | Tritt „Desktop gewechselt" bei UAC und Sperre ein? Kommen WinEvents für Fenster mit Adminrechten an? | Erweiterung VE-02 | Abfrage des Eingabedesktops und des Vordergrundfensters im Takt. Für den sicheren Desktop **umgesetzt**: Der Eingabedesktop wird alle 250 ms gelesen, das WinEvent wird nicht benötigt; offen bleibt es für Fenster mit Adminrechten. |
| Z-5 | Zählt der Leistungszähler die Schlafzeit mit? | eigene Prüfung | Lücke und Kontrollpunkt machen es unerheblich |
| Z-6 | Wirkt die Ausnahme auch für durchsichtige Fenster, Tooltips und Popups? | Erweiterung VE-05 | Ausweichmodus |
| Z-7 | Halten das Lesen des Fensters unter dem Zeiger und seines obersten Fensters im Hook-Aufruf das Budget? | Erweiterung VE-01 | Lesen unmittelbar nach der Entnahme aus dem Ring |
| Z-8 | Wann aktivieren Chromium, Electron und Firefox ihre Barrierefreiheit; genügt das Aufwärmen? | Erweiterung VE-04 | Einstufung „undurchsichtig" mit späterer Neuprüfung |
| Z-9 | Was sehen Hook und Raw Input bei Touch, Stift und Touchpad-Gesten; wird der Hook vor der Anwendung aufgerufen? | eigene Prüfung | Garantiestufe `atInput`; Lücke `unsupportedInput` |
| Z-10 | Stimmt die Tottasten-Zusammensetzung nach Unicode-Regeln mit echten Layouts überein? | eigene Prüfung | Tottasten-Tabelle aus dem Layout lesen |
| Z-11 | Enthält Raw Input künstlich erzeugte Eingaben? | Erweiterung VE-02 | Abgleichsregel behandelt beide Fälle bereits |
| Z-12 | Wie weicht die 8-Bit-Wandlung auf HDR-Monitoren vom Gesehenen ab? | Erweiterung VE-10 | Kennzeichen; spätere eigene Tonwertabbildung |
| Z-13 | Welches Zeitlimit gilt tatsächlich, und wann entfernt Windows den Hook? | Erweiterung VE-01 | Budget ist ohnehin weit unter jedem denkbaren Limit |
| Z-14 | Lässt sich die Größe eines Formats der Zwischenablage feststellen, ohne das Format abzurufen (und damit die Quellanwendung zum Bereitstellen zu veranlassen)? | eigene Prüfung | ohne Inhaltsaufzeichnung keine Größe im Ereignis |

### 15.3 Offene Festlegungen

| Punkt | Stand |
|---|---|
| Liste der Hilfsprozesse für die Zugehörigkeit (5.4, Rang 4) und der Programme für undurchsichtige Oberflächen (7.4) | Vorbelegung wird in M1 an echten Systemen erstellt |
| Schwellenwerte (Verweilen 80 ms, Beruhigung, Kurzberuhigung, Paarfenster ±100 ms) | Startwerte; Feinjustierung in M6 (offene Entscheidung O-06 in [11](11-roadmap-tests-risiken.md)) |
| Dauer der Rückhaltung im Puffer | legt [10](10-datenschutz-und-sicherheit.md) fest (`T_max`, Standard 1,5 s); der Kern ist auf mindestens 10 s ausgelegt |
| Verdeckte Fenster vollständig aufnehmen; eigene HDR-Tonwertabbildung; Gerätebenennung | spätere Ausbaustufen |
| Verworfener Aufnahmeabschnitt (1.7) | Entschieden: Das Journal wird nie gekürzt; der Abschnitt wird gekennzeichnet und aus allen Ausgaben herausgehalten. Noch festzulegen: das Kennzeichen und wie Sitzungsdauer und Zählwerte einen verworfenen Abschnitt führen ([04](04-datenformat.md#aufnahmeabschnitte)); seine Behandlung in Schritterkennung und Vollständigkeitsbericht ([05](05-schritterkennung.md#wirksame-schrittfolge)); der Hinweis beim Verwerfen ([07](07-aufnahme-bedienung.md)); was beim endgültigen Schwärzen und in einer bereinigten Kopie von ihm bleibt ([10](10-datenschutz-und-sicherheit.md#108-endgültiges-schwärzen)) – dieses Dokument setzt voraus: nichts von seinem Inhalt |
| Eingabenummer über Abschnittsgrenzen (1.7) | Dieses Dokument lässt sie fortlaufen, weil die Formatprüfung F18 keine doppelte Nummer erlaubt; [04](04-datenformat.md#eingabenummer) sagt es bisher nicht ausdrücklich |
| Ablage des Salzes, mit dem Monitore über Abschnitte wiedererkannt werden (4.13) | legt [04](04-datenformat.md) fest |
| Leistungszähler bei mehreren Abschnitten (12.9) | Ob `manifest.performance` je Abschnitt oder über die Sitzung geführt wird, legt [04](04-datenformat.md#manifest) fest; der Kern zählt je Abschnitt |
