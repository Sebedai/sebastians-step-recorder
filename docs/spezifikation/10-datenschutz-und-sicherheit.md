# 10 – Datenschutz und Sicherheit

`Status: Entwurf 0.1 · Stand: 2026-11-11`

Ein Programm, das Tastatur, Maus und Bildschirm mitschneidet, ist technisch von einem Überwachungswerkzeug kaum zu unterscheiden. Dieses Dokument legt fest, wodurch sich StepRecorder davon unterscheidet: wie Passwörter und andere sensible Inhalte gar nicht erst gespeichert werden, wie Gespeichertes zuverlässig geschwärzt wird, wie sich die Netzwerkfreiheit nachprüfen lässt, welche Bedrohungen betrachtet wurden und was offen als Restrisiko bleibt.

## Inhalt

1. [Zweck und Geltungsbereich](#1-zweck-und-geltungsbereich)
2. [Grundsätze](#2-grundsätze)
3. [Dateninventar](#3-dateninventar)
4. [Schutzstufen](#4-schutzstufen)
5. [Passwort-Rückhaltung](#5-passwort-rückhaltung)
6. [Passwortabfragen im Terminal](#6-passwortabfragen-im-terminal)
7. [Privattaste](#7-privattaste)
8. [Ausschlussliste](#8-ausschlussliste)
9. [Zwischenablage](#9-zwischenablage)
10. [Schwärzungsmodell](#10-schwärzungsmodell)
11. [Prüfung vor dem Export](#11-prüfung-vor-dem-export)
12. [Netzwerkfreiheit](#12-netzwerkfreiheit)
13. [Speicherorte, Sync-Ordner und Spuren außerhalb der Sitzung](#13-speicherorte-sync-ordner-und-spuren-außerhalb-der-sitzung)
14. [Bedrohungsmodell](#14-bedrohungsmodell)
15. [Rechtliche Hinweise](#15-rechtliche-hinweise)
16. [Virenscanner, SmartScreen, Smart App Control, Signierung](#16-virenscanner-smartscreen-smart-app-control-signierung)
17. [Prüfungen](#17-prüfungen)
18. [Bezug zum Architektur-Review](#18-bezug-zum-architektur-review)
19. [Anforderungen](#19-anforderungen)
20. [Annahmen und offene Punkte](#20-annahmen-und-offene-punkte)

---

## 1. Zweck und Geltungsbereich

Das Dokument regelt alle Schutzmechanismen des Tools – bei der Aufnahme, in der gespeicherten Sitzung und in jeder Ausgabe. Es gilt Leitprinzip 3 aus dem [Überblick](00-ueberblick.md): **Kollidieren Vollständigkeit und Privatsphäre, hat die Privatsphäre Vorrang; die Lücke wird als solche ausgewiesen.**

Abgrenzung: Die technischen Andockpunkte (Hook, Fokus- und Elementermittlung, Bildaufnahme) beschreibt [03 Aufzeichnungskern](03-aufzeichnungskern.md); wie Masken, Lücken und Schwärzungen gespeichert werden, [04 Datenformat](04-datenformat.md); die Bedienung von Privattaste ([07](07-aufnahme-bedienung.md#102-privattaste)), Durchsicht ([08](08-viewer.md#13-durchsicht-der-eingaben)) und Schwärzung ([08](08-viewer.md#76-schwärzen-und-verpixeln)); Entschärfung in KI-Ausgaben und der MCP-Server [06](06-ki-export-und-mcp.md).

**Andockpunkte im Aufzeichnungskern** ([03](03-aufzeichnungskern.md#andockpunkte-datenschutz)) und wo dieses Dokument über sie entscheidet:

| Andockpunkt in 03 | Entscheidung hier |
|---|---|
| A1 Fokus-Auskunft | Einstufung und Proben: [4.1](#41-einstufung-des-eingabeziels), [5.4](#54-proben-und-einstufung) |
| A2 Rückhaltepuffer | Freigaberegel, Rangfolge, Zeitwerte: [5.3](#53-parameter)–[5.7](#57-was-beim-maskieren-gespeichert-wird) |
| A3 Wertefilter | Feldinhalte, Werte in Text-Schnappschüssen, Fenstertitel: [4.2](#42-verhalten-je-stufe), [5.8](#58-einmal-passwort-immer-passwort), [5.10](#510-sensible-felder-nach-name), [8.3](#83-wirkung) |
| A4 Bildfilter | Passwortfelder, ausgeschlossene Fenster, Umschalter und Benachrichtigungen: [5.9](#59-schwärzung-des-felds-im-bild), [8.3](#83-wirkung)–[8.5](#85-bekannte-grenzen) |
| A5 Zwischenablage-Filter | [Abschnitt 9](#9-zwischenablage) |
| A6 Terminal-Zeile | [Abschnitt 6](#6-passwortabfragen-im-terminal) |
| A7 Ausschluss und Bereich | [Abschnitt 8](#8-ausschlussliste) |

**Was das Tool zusichert – und was nicht**

| Zugesichert | Nicht zugesichert |
|---|---|
| Aufgenommen wird nur nach ausdrücklichem Start und für die Person am Rechner immer sichtbar. | Dass **jede** Passworteingabe erkannt wird. In Terminals, Remote-Desktop-Sitzungen und virtuellen Maschinen sind Passwortfelder technisch nicht erkennbar; dort gilt das Schutznetz ([Abschnitt 4](#4-schutzstufen)). |
| Eingaben in **erkannte** Passwortfelder erreichen nie die Festplatte – weder als Tasten noch als Feldinhalt noch als Bildpunkte. | Dass Bilder frei von sensiblen Inhalten sind. Was im Aufnahmebereich auf dem Bildschirm stand, ist im Bild – bis es geschwärzt wird. |
| Das Programm öffnet keine Netzwerkverbindung. | Schutz gegen andere Programme oder Personen mit Zugriff auf dasselbe Benutzerkonto. |
| Eine Schwärzung gilt in jeder Ausgabe: Viewer, Exporte, KI-Paket, MCP, Suche. | Sicheres Löschen auf Datenträgerebene, in Sicherungen oder im Versionsverlauf von Sync-Diensten. |
| Ein verallgemeinerter Wert erscheint in keiner Ausgabe im Original – auch nicht in Suche, Vergleich und MCP-Antworten. | Dass eine Verallgemeinerung jedes Vorkommen erreicht: Was nur als Bildpunkte existiert oder anders geschrieben ist, bleibt stehen, bis es geschwärzt wird. |
| Kein Aufnahmeprofil und keine eingelesene Einstellungsdatei senkt den Schutz unbemerkt. | Dass eine mitgegebene Datei gut gemeint ist: Profile und Einstellungen aus fremder Hand sind fremde Eingabe und werden so behandelt. |
| Nichts verlässt den Sitzungsordner ohne eine Handlung des Nutzers. | Dass ein KI-Modell eingebettete Anweisungen in aufgezeichneten Texten ignoriert. |

Die rechte Spalte steht so auch in der Nutzerdokumentation und in Kurzform im Startfenster.

## 2. Grundsätze

| Nr. | Grundsatz | Konsequenz |
|---|---|---|
| P1 | **Sichtbar.** Keine Aufnahme ohne ausdrücklichen Start durch die Person am Rechner; während der Aufnahme ist auf jedem aufgenommenen Monitor ein Hinweis zu sehen. | Kein verdeckter Modus, kein Autostart, kein Dienst, kein Fernstart, kein Start in einer nicht interaktiven Sitzung. Auch `record` auf der Kommandozeile zeigt den Hinweis; es gibt keine Option, ihn abzuschalten. Lässt sich kein Hinweis anzeigen, startet die Aufnahme nicht. |
| P2 | **Offline.** Kein Netzwerkzugriff. | Keine Telemetrie, kein Absturzbericht nach außen, keine eingebettete Browser-Komponente ([Abschnitt 12](#12-netzwerkfreiheit)). Einzige Ausnahme ist die **Suche nach Updates**: ab Werk aus, nur auf ausdrücklichen Wunsch in den Einstellungen; sie fragt allein die öffentliche Release-Seite ab, ohne Kennung und ohne persönliche Daten, und lädt nichts herunter (Entscheidung O-14). |
| P3 | **Sparsam ab Werk.** Voreinstellungen erheben das Nötige. | Zwischenablage-Inhalt aus; standardmäßig kein Rechner- oder Benutzername im Manifest; Aufnahmebereich wählbar; Ausschlussliste vorbelegt. |
| P4 | **Im Zweifel maskieren.** Lässt sich ein Eingabeziel nicht rechtzeitig oder nicht widerspruchsfrei einstufen, wird die Eingabe nicht gespeichert. | Zeitüberschreitung, Widerspruch und Fehler führen zu `text.masked`, nie zu Klartext. |
| P5 | **Ehrlich.** Was maskiert, ausgeschlossen oder geschwärzt wurde, ist als solches markiert. | Lückenmarker und Belegstufe „unbekannt“ statt stillem Weglassen. |
| P6 | **Ein Tor.** Alle Ausgaben lesen über die Leseschicht; kein Kanal umgeht Rückhaltung oder Schwärzung. | Gilt für Hook und Raw Input, Feldinhalte, Text-Schnappschüsse, Zwischenablage, Bilder, Caches, Suche, MCP. |
| P7 | **Prüfen vor dem Teilen.** Vor jeder Weitergabe sieht der Nutzer, was er weitergibt. | Durchsicht aller Eingaben und Musterprüfung vor dem Export ([Abschnitt 11](#11-prüfung-vor-dem-export)). |
| P8 | **Lokal.** Daten liegen nur dort, wo der Nutzer sie hinlegt. | Keine versteckten Kopien; abgeleitete Daten nur in `cache/` und `export/` der Sitzung; bekannte Spuren des Betriebssystems sind dokumentiert ([Abschnitt 13](#13-speicherorte-sync-ordner-und-spuren-außerhalb-der-sitzung)). |
| P9 | **Kein Eingriff in fremde Programme.** | Keine Code-Injektion, kein Lesen fremden Prozessspeichers, keine systemweiten Hooks, die Code in andere Prozesse laden. Informationen kommen nur über dokumentierte Schnittstellen (Low-Level-Hooks, Raw Input, UI Automation, WinEvents, Bildschirmaufnahme). |

**Sichtbarkeit und Unsichtbarkeitsgarantie widersprechen sich nicht:** Die Fenster des Recorders fehlen in den *Bildern* ([07](07-aufnahme-bedienung.md#11-unsichtbarkeitsgarantie-aus-nutzersicht)), sind für die Person am Bildschirm aber zu sehen. Der Ausweichmodus (Fenster nur für den Moment eines Bildes verborgen) ändert daran nichts; ein dauerhaft unsichtbarer Betrieb ist ausgeschlossen. Beim Start erscheint zwei Sekunden lang der Hinweis „Aufnahme läuft – Tastatur, Maus und Bildschirm werden aufgezeichnet“ mit der gewählten Schutzstufe. Beim Sperren des Rechners, bei Benutzerwechsel und beim Trennen einer Remote-Sitzung pausiert die Aufnahme und läuft erst nach ausdrücklicher Bestätigung weiter – nach dem Entsperren kann jemand anderes am Rechner sitzen. Im Protokoll steht für die Dauer der Sperre oder Trennung eine Lücke `sessionLocked` bzw. `sessionDisconnected`, danach bis zur Bestätigung eine Lücke `paused`. Eine Einstellung, die das Fortsetzen ohne Bestätigung erlaubt, gibt es nicht.

**Bereitschaft im Infobereich** (Paket 14 aus [12](12-entwickler-fahrplan.md), Entscheidung O-15): Auf Wunsch (Einstellung „Im Infobereich bereit bleiben“, ab Werk aus) bleibt das Programm nach dem Schließen des letzten Fensters mit einem eigenen grauen Symbol im Infobereich. Angemeldet ist dann nur das Tastenkürzel „Sofort aufnehmen“; es laufen keine Hooks und keine Bildaufnahme, und das Programm startet sich nie selbst. Jede Aufnahme zeigt wie immer Leiste und Rahmen. Das ist mit P1 vereinbar: Erfasst wird erst nach ausdrücklichem Start.

P1 und P2 sind **Projektgrundsätze**: Beiträge, die einen verdeckten Modus, Autostart, Fernsteuerung der Aufnahme oder Datenübertragung einführen, werden nicht angenommen ([11, Veröffentlichung](11-roadmap-tests-risiken.md)).

### 2.1 Bildschutz bei Sperre, Ruhezustand und sicherem Desktop

Auf dem sicheren Desktop (Abfrage der Benutzerkontensteuerung, `Strg+Alt+Entf`, Bildschirmschoner), bei gesperrtem Rechner und im Ruhezustand liefert die Bildaufnahme keine verlässlichen Bilder: Sie gibt schwarze oder halbe Bilder zurück, oder sie zeigt Inhalt, den die Person nicht aufnehmen wollte. Deshalb gilt (Technik in [03](03-aufzeichnungskern.md)):

- **Eine Lücke, keine Bilder.** Der Recorder liest alle 250 ms den Eingabe-Desktop. Ein anderer Desktop als „Default“ beginnt sofort eine Lücke `secureDesktop` mit der Art `uac` (die Abfrage läuft, erkennbar an `consent.exe`) oder `other`. Zurück zum normalen Desktop geht es erst nach zwei aufeinanderfolgenden „Default“-Lesungen, damit ein Flackern beim Umschalten keine Bilder zulässt.
- **Nichts aus der Lücke.** Während der Lücke und während Sperre und Ruhezustand entstehen keine Bilder; der Ringpuffer wird verworfen, ein offenes Nachher-Bild entfällt, und eine fehlgeschlagene Aufnahme wird nie als schwarzes Bild gespeichert. Was in der Lücke getippt wird – etwa ein Kennwort in der Abfrage der Benutzerkontensteuerung –, erreicht die Aufzeichnung nicht.
- **Danach neu aufsetzen.** Nach der Lücke gleicht der Recorder den Tastenstatus ab (`keystate.sync`) und erzwingt ein Schlüsselbild.
- **Benannt, nicht verschwiegen.** Die Lücke erscheint in jeder Ausgabe; die Schritterkennung macht aus der Art `uac` einen eigenen Schritt „Benutzerkontensteuerung bestätigen“ ohne Bild. Ein Nachher-Bild wird nie aus einer solchen Lücke gebunden.

### 2.2 Aufnahme auf Bitte eines KI-Agenten

Ein KI-Agent darf eine Vorführung **erbitten**, nie selbst aufnehmen. Das folgt aus P1 (kein Fernstart) und ist so begrenzt:

| Nr. | Regel |
|---|---|
| A1 | **Nur mit Schalter.** Die Werkzeuge `request_recording` und `get_recording_request` ([06](06-ki-export-und-mcp.md)) stehen nur im MCP-Server, der mit `--allow-requests` gestartet wurde; sonst sind sie nicht gelistet. Der Server startet die Oberfläche nie: Läuft sie nicht, endet die Anfrage mit `UI_UNAVAILABLE`. |
| A2 | **Der Text des Agenten ist ungeprüft.** Ziel (10 bis 500 Zeichen), erwartetes Ergebnis (bis 500) und Titel (bis 120) werden von Steuer- und Schreibrichtungszeichen bereinigt und auf ihre Länge geprüft; unbekannte Parameter werden abgelehnt. Der Dialog zeigt den Text als „ungeprüft“ in fester Schrift, getrennt von der Erklärung des Programms. |
| A3 | **Die Person entscheidet, ohne Voreinstellung.** Der Dialog hat keinen Standardknopf; die Eingabetaste löst nichts aus. „Aufnahme starten“ lässt sich erst nach zwei Sekunden betätigen. Zur Wahl stehen außerdem „Ablehnen“ und „15 Minuten keine Anfragen“. Nach 120 Sekunden verfällt die Anfrage (`expired`); der Dialog zeigt die Frist an. |
| A4 | **Ratenbegrenzung.** Höchstens eine offene Anfrage, mindestens 60 Sekunden Abstand, höchstens fünf je Stunde; die Sperre „15 Minuten keine Anfragen“ gilt für alle weiteren. Ein Agent kann die Person so nicht mit Anfragen überhäufen. |
| A5 | **Eine ganz normale Aufnahme.** Die Zustimmung startet eine sichtbare Aufnahme mit allen Schutzregeln (P1, Schutznetz, Privattaste) in den MCP-Ordner. Das Ziel der Aufnahme trägt den Vermerk `statedBy: agent` (Manifest, MCP `get_session`, Kopf von `trace.md`), damit niemand die Angabe für einen Text des Autors hält. |
| A6 | **Keine Fernsteuerung.** Der Agent kann weder starten noch stoppen, pausieren oder markieren. Er sieht allein den Zustand seiner Anfrage (`pending`, `declined`, `expired`, `recording`, `finished`, `discarded`, `failed`) und bei `finished` die Sitzungskennung. Was die Aufnahme enthält, erreicht er nur über die üblichen Lesewege – mit Schwärzung und Zurückhaltung (L7). |
| A7 | **Spuren ohne Inhalt.** Anfrage- und Statusdateien liegen im Zustandsordner des Programms; abgeschlossene Zustände bleiben stehen, damit der Agent das Ergebnis abholen kann. Das Programmprotokoll nennt nur die Art des Vorgangs. |

## 3. Dateninventar

| Datenart | Wo gespeichert | Sensibilität | Stellschrauben |
|---|---|---|---|
| Tastatureingaben (Zeichen, Tasten, Zeiten) | `key.*` im Rohprotokoll; abgeleitet in `steps.json` | hoch | Rückhaltung, Schutzstufe, Privattaste, Ausschlussliste, Schwärzung |
| Maskierte Eingaben | `text.masked`: Anzahl, Grund, Zeitraum – kein Inhalt | gering | Anzahl abschaltbar ([5.7](#57-was-beim-maskieren-gespeichert-wird)) |
| Mausweg und Klicks | `mouse.*` | gering | Aufnahmebereich |
| Bildschirmbilder | `frames/` | hoch – alles Sichtbare | Aufnahmebereich, Aufnahme-Masken, Schwärzung |
| Elementnamen, Werte, Text-Schnappschüsse | `element`, `ui.*`, `ui.snapshot` | mittel bis hoch | Wert-Regeln der Schutzstufe, Ausschlussliste, Schwärzung |
| Fenstertitel, Programmnamen, Adressen, Dateipfade | `window.*`, `element` | mittel – Titel enthalten oft Dateinamen, Betreffzeilen, Kundennamen | Ausschlussliste, Schwärzung |
| Zwischenablage | `clipboard`: Herkunft immer, Inhalt nur auf Wunsch | hoch | Opt-in, Kennzeichnungen sensibler Inhalte |
| Umgebung | `manifest.json`: Windows-Version, Sprache, Tastaturlayout, Monitore, Recorder-Version, Rechte, Schutzstufe | gering | – |
| Programm-Kontext (Paket 10 aus [12](12-entwickler-fahrplan.md)) | `process.info` (Programmname, Produkt, Version; mit „Erweitertem Programmkontext“ Build-Kennung und Git-Zweig mit Commit), `process.exit` (Exit-Code), `window.hang`, `eventlog.entry` (Fakten ohne Freitext) | gering bis mittel – ein Zweigname kann sprechend sein | nur Programme im Aufnahmebereich und nicht ausgeschlossen; nichts während der Pause; Schwärzung (nicht strukturell); nie Pfade, Befehlszeilen, Fernadressen, Autoren, Commit-Texte |
| Bearbeitungen, Kommentare | `edits.json` | mittel | – |
| Angaben des Autors zur Aufnahme: Ziel, erwartetes Ergebnis, Bezug; Bezeichnungen der Marker | `manifest.goal` und `session.start`, `edits.goal`; `marker.label` | mittel – Freitext, oft mit Namen, Ticketnummern, Fehlerbeschreibungen | Durchsicht, Schwärzung, Verallgemeinerung ([11.1](#111-durchsicht-aller-eingaben)) |
| Verallgemeinerungsregeln | `edits.substitutions[]` – **die Regel enthält den Wert**, den sie ersetzt | wie der Wert selbst | endgültiges Anwenden entfernt ihn ([10.9](#109-verallgemeinerung-von-werten)) |
| Aufnahmeprofil der Sitzung | `manifest.profile`: Name, wirksame Einstellungen, schwächere Punkte | gering | – |
| Verworfene Aufnahmeabschnitte | Rohprotokoll und Bilder der Sitzung, als verworfen markiert – das Journal wird nie gekürzt | wie die Aufzeichnung | erscheinen in keiner Ausgabe (L12); dauerhaft entfernt durch bereinigte Kopie oder endgültiges Schwärzen ([10.8](#108-endgültiges-schwärzen)) |
| Bearbeitungsstände | `history/`: automatische Sicherungen von `edits.json` | wie `edits.json` – sie enthalten auch Verallgemeinerungsregeln mit ihren Werten und interne Kommentare | nie in `.steprec` oder bereinigter Kopie; endgültiges Schwärzen löscht alle ([10.8](#108-endgültiges-schwärzen)) |
| Abgeleitetes | `cache/` (Vorschaubilder, Suchindex unter `cache/search/`), `export/` | wie die Quelle | wird bei jeder Änderung an Schwärzungen, Verallgemeinerungen oder Durchsicht verworfen bzw. gemeldet ([10.6](#106-abgeleitete-daten-und-frühere-exporte)) |

**Bewusst nicht erhoben:** Rechnername, Benutzername, Domäne, Sicherheitskennung des Kontos, Seriennummern, Netzwerkadressen, Standort, Liste installierter Programme, Liste laufender Prozesse (nur Programme, mit deren Fenstern gearbeitet wurde), Befehlszeilen fremder Prozesse (Ausnahme: mit „Erweitertem Programmkontext“ die Befehlszeile der aufgenommenen Programme, Argumente mit Geheimnissen ausgelassen, Profilpfad ersetzt – Entscheidung O-12), Ton, Kamera, Dateiinhalte, Netzwerkverkehr, Browserverlauf.

Einzige Ausnahme: Rechner- und Benutzername lassen sich über die Einstellung `privacy.recordIdentity` ausdrücklich zuschalten ([04](04-datenformat.md#manifest)) – etwa für Support-Fälle, in denen der Rechner zugeordnet werden muss. Standard ist aus; Exporte können die Angaben auch dann weglassen.

**Pfade:** Programmpfade unterhalb des Benutzerprofils werden mit dem Platzhalter `%USERPROFILE%` gespeichert (`Win32Detail.ProfilePath`; auch in Befehlszeilen), damit der Benutzername nicht über den Profilpfad ins Protokoll gelangt. Pfade, die im *Bildschirminhalt* vorkommen (Titelzeile, Adressleiste), sind aufgezeichneter Inhalt; die Musterprüfung weist auf Benutzernamen darin hin.

**Außerhalb der Sitzung:** Einstellungen, eigene Aufnahmeprofile (`profiles.json`) und eine auf Befehl erzeugte Austauschdatei (`StepRecorder-Einstellungen.json`) enthalten keine Sitzungsdaten, keine absoluten Pfade und keine Namen von Rechner, Benutzer oder Domäne ([13.4](#134-einstellungen-und-profile-weitergeben)). Sie können aber verraten, was jemand für vertraulich hält – die Ausschlussliste nennt Programme –, und sie können Texte des Autors enthalten: Textbausteine und die Vorbelegungen für das Support-Paket.

## 4. Schutzstufen

Die Schutzstufe wird je Aufnahme gewählt, steht im Manifest und im Kopf jeder Ausgabe. **Standard ist „Schutznetz“** – so vom Auftraggeber entschieden, weil das Ziel „alle Tasteneingaben“ sonst in Terminals und Remote-Sitzungen verfehlt würde.

### 4.1 Einstufung des Eingabeziels

Jede Tasteneingabe wird einem Ziel zugeordnet und dieses eingestuft:

| Einstufung | Bedeutung | Woran erkannt |
|---|---|---|
| `password` | Passwortfeld | UI Automation `IsPassword`; Win32-Eingabefeld mit Passwort-Stil; Feld war in dieser Sitzung schon einmal Passwortfeld |
| `sensitive` | Feld, dessen Name auf Geheimes hindeutet | Name, Beschriftung, Kennung oder Platzhalter passt auf die Liste sensibler Feldnamen ([5.10](#510-sensible-felder-nach-name)) |
| `normal` | gewöhnliches Textfeld, **bestätigt** | Textelement ohne Passwort-Kennzeichen, zweifach übereinstimmend ermittelt, und die Tasten sind nachweislich dort angekommen ([5.5](#55-freigaberegel)) |
| `opaque` | undurchsichtige Oberfläche | Fenster ohne Eingabefelder für UI Automation: Terminal, Remote-Desktop, virtuelle Maschine, Spiel, Zeichenfläche – erkannt an Fensterklasse, Programm oder daran, dass nur das Fenster selbst ermittelbar ist |
| `unresolved` | nicht einstufbar | Zeitlimit, widersprüchliche Auskünfte, Fehler der Ermittlung |
| `excluded` | Fenster einer ausgeschlossenen Anwendung | Ausschlussliste ([Abschnitt 8](#8-ausschlussliste)) |

`opaque` und `unresolved` sind bewusst getrennt: „Das Fenster hat keine Felder“ ist eine verlässliche Eigenschaft des Fensters; „die Auskunft kam nicht rechtzeitig“ ist Unwissen – und Unwissen wird in beiden Stufen maskiert.

Im Datenformat erscheint die Einstufung als Eingabekontext `ctx` mit Herkunft `ctxSrc` ([04](04-datenformat.md#ev-focus)): `normal` → `textField`; `password` und `sensitive` → `passwordField`; `opaque` → `opaque`; `unresolved` → `unknown`. Liegt der Fokus auf einem Element ohne Texteingabe, steht dort `nonText`. Die Schutzstufe selbst steht als `settings.privacy.level` im Manifest: „Schutznetz“ = `balanced`, „Streng“ = `strict` ([04](04-datenformat.md#manifest)).

Ein Fenster, in dem die Stufe „Schutznetz“ im Klartext aufzeichnet, heißt in der Bedienung **Klartext-Fenster** ([07](07-aufnahme-bedienung.md#74-hinweise-und-warnungen)). Texteingaben daraus tragen in `steps.json` das Kennzeichen `flags.plaintext` ([05](05-schritterkennung.md#schrittarten)) – die Grundlage dafür, dass sie als auffällig gelten ([11.1](#111-durchsicht-aller-eingaben)).

### 4.2 Verhalten je Stufe

| Eingabeziel / Datenart | **Schutznetz** (Standard) | **Streng** |
|---|---|---|
| `password`, `sensitive` | maskiert | maskiert |
| `normal` | Klartext | Klartext |
| `opaque`: Terminal | Klartext, aber **prüfpflichtig**: Getipptes gilt als auffällig (`flags.plaintext`) und braucht eine Entscheidung in der Durchsicht ([11.1](#111-durchsicht-aller-eingaben)). Erkannte Passwortabfragen ([Abschnitt 6](#6-passwortabfragen-im-terminal)) werden maskiert (Grund `terminalPrompt`) und sind damit nicht prüfpflichtig. | maskiert (Grund `strictPlaintext`); die Freigabetaste ist nicht umgesetzt |
| `opaque`: Remote-Desktop, VM, Spiel, sonstige | Klartext mit Schutznetz, ebenfalls prüfpflichtig | maskiert (Grund `strictPlaintext`) |
| `opaque` in einem Fenster mit Anmelde-Merkmalen ([5.4](#54-proben-und-einstufung)) | maskiert | maskiert |
| `unresolved` | maskiert | maskiert |
| `excluded` | nichts aufgezeichnet | nichts aufgezeichnet |
| Von Programmen erzeugte Eingaben | wie das Zielfeld; Serien aus Passwortmanagern werden maskiert ([5.11](#511-eingaben-von-passwortmanagern)) | maskiert, außer in `normal` |
| Feldinhalt vorher/nachher | nur bei `normal` | nur bei `normal` |
| Werte in Text-Schnappschüssen | Beschriftungen und Meldungstexte ja; Werte von Eingabefeldern nur bei `normal`, höchstens 200 Zeichen | Beschriftungen und Meldungstexte ja; keine Werte von Eingabefeldern |
| Zwischenablage-Inhalt | nur auf Wunsch | nie |
| Tasten ohne Zeichen (Enter, Tab, Pfeile, Strg+S …) | aufgezeichnet | aufgezeichnet |

„Streng“ maskiert alles Getippte in Terminal-, Remote-Desktop- und VM-Fenstern und in Fenstern, deren Eingabeziel nicht einstufbar ist (`strictUnknown`); Tasten ohne Zeichen und Tastenkombinationen bleiben erhalten. Als Terminal gelten die Konsole, Windows Terminal, PuTTY und verwandte Programme sowie eingebettete Terminals von Electron-Anwendungen (xterm.js); die Liste der Programme ist im Programm hinterlegt und erweiterbar.

„Streng“ wird empfohlen für Aufnahmen auf fremden Rechnern, mit Kundendaten und in Remote-Sitzungen.

### 4.3 Das Schutznetz

Weil Klartext aus undurchsichtigen Oberflächen ein Restrisiko trägt, gehören zur Stufe „Schutznetz“ zwingend:

| Nr. | Bestandteil | Beschreibung |
|---|---|---|
| N1 | **Klartext-Hinweis** | Solange ein Klartext-Fenster den Fokus hat, zeigt die Aufnahmeleiste – bei ausgeblendeter Leiste der Rahmen – den Hinweis mit dem Kürzel der Privattaste. Wortlaut und Verhalten in [07](07-aufnahme-bedienung.md#74-hinweise-und-warnungen). |
| N2 | **Privattaste** | Maskieren auf Tastendruck, garantiert ab der nächsten Taste; ein Umschalter ohne automatisches Ende ([Abschnitt 7](#7-privattaste)). |
| N3 | **Erkennung von Passwortabfragen im Terminal** | Regeln T1 bis T3 in Terminals mit Textzugriff ([Abschnitt 6](#6-passwortabfragen-im-terminal)); erkannte Abfragen werden selbsttätig maskiert. Ergänzend gilt: Alles übrige Getippte aus Terminal-, Remote-Desktop- und VM-Fenstern ist auffällig und braucht in der Durchsicht eine Entscheidung (N5); in der Stufe „Streng“ wird es maskiert. |
| N4 | **Erkennung von Passwortmanager-Eingaben** | [5.11](#511-eingaben-von-passwortmanagern). |
| N5 | **Pflicht-Durchsicht vor der Weitergabe** | Eingaben aus Klartext-Fenstern (Terminal, Remote-Desktop, VM) sind „auffällig“ und verlangen je Eintrag eine Entscheidung, bevor sich im Viewer etwas exportieren lässt. Wege ohne Oberfläche – MCP-Server und Export per Kommandozeile – werden nicht gesperrt, erhalten solche Eingaben bis dahin aber nur als Zähler `{unreviewed:N}`; einen Schalter, der das aufhebt, gibt es nicht ([Abschnitt 11](#11-prüfung-vor-dem-export)). |
| N6 | **Musterprüfung** | Sucht Kennwörter, Schlüssel und Kontonummern in allen Texten; Treffer in Eingaben machen diese ebenfalls auffällig. |
| N7 | **Offene Benennung** | Startfenster, Trace-Kopf und Dokumentation nennen die Grenze in einem Satz. |

N1, N2 und N5 lassen sich in dieser Stufe nicht abschalten.

### 4.4 Aufnahmeprofile: nie unbemerkt schwächer

Ein Aufnahmeprofil ist eine benannte Voreinstellung für eine Aufnahme ([07](07-aufnahme-bedienung.md#47-aufnahmeprofile), [02](02-architektur.md#aufnahmeprofile)). Es kann auch Schutzeinstellungen festlegen. Profile kommen aus vier Quellen – mitgeliefert, selbst angelegt, mit der EXE mitgegeben oder aus einer Austauschdatei eingelesen –, und die beiden letzten sind fremde Hand. Deshalb gilt: **Kein Profil senkt den Schutz, ohne dass die Person am Rechner es vor der Aufnahme sieht.**

**Was als schwächer gilt.** Verglichen werden die wirksamen Einstellungen der Aufnahme mit denen, die ohne Profil gälten – den Einstellungen des Nutzers. Diese Tabelle ist die maßgebliche Liste; 02, 04 und 07 verweisen auf sie.

| Einstellung | Schwächer, wenn | In `manifest.profile.weakens` | Kennung beim Import (`--allow-weaker`) |
|---|---|---|---|
| Schutzstufe | „Schutznetz“ gilt, wo ohne Profil „Streng“ gälte | `privacyLevel` | `profile:<Name>` |
| Inhalt der Zwischenablage | er wird aufgezeichnet, wo er ohne Profil nicht aufgezeichnet würde | `clipboardContent` | `profile:<Name>` |
| Fehlertexte der Ereignisanzeige (Paket 12 aus [12](12-entwickler-fahrplan.md)) | sie werden mitgelesen | `eventLogText` | – (nicht Teil der Austauschdatei) |
| Protokolldateien (Paket 13) | mindestens eine Datei wird mitgelesen | `logFiles` | – (nicht Teil der Austauschdatei) |
| Ausschlussliste | ihr fehlen Einträge, die bisher gelten | – (ein Profil kann der Liste nur hinzufügen) | `excluded` |

*Stand der Umsetzung:* `RecordingOptions.Weakens` vergleicht die Aufnahme mit der Schutzstufe der Einstellungen und zählt jede inhaltstragende Quelle (`ContentSources`: Fehlertexte der Ereignisanzeige, Inhalt der Zwischenablage, Protokolldateien) als schwächer. Das Ergebnis steht in `manifest.profile.weakens`, in der Startmeldung der Leiste („Schwächer als die Einstellungen: …“), bei `record` auf stderr; solange eine Quelle mitliest, zeigt die Leiste ein Auge mit Erklärung. In „Streng“ sind alle Quellen abgeschaltet. Einschalten lassen sie sich nur in den Einstellungen (Reiter „Entwickler“, mit Rückfrage) und Protokolldateien zusätzlich mit `record --log`; die Austauschdatei enthält sie nicht.

Beim Import wird ein Profil mit dem gleichnamigen vorhandenen verglichen, sonst mit den geltenden Einstellungen ([13.4](#134-einstellungen-und-profile-weitergeben)).

Nicht schwächer, sondern anders sind ein anderer Aufnahmebereich, andere Bildeinstellungen und eine andere Ausgabe nach dem Stopp. Sie ändern, *was* aufgenommen wird, und stehen sichtbar im Startfenster.

**Was ein Profil gar nicht kann**

| Ein Profil kann nicht … | Grund |
|---|---|
| den Hinweis auf die laufende Aufnahme, den Klartext-Hinweis, die Privattaste oder die Pflicht-Durchsicht abschalten | P1 sowie N1, N2 und N5 lassen sich durch nichts abschalten ([4.3](#43-das-schutznetz)) |
| Einträge aus der Ausschlussliste streichen | Es ergänzt die Liste der Einstellungen nur |
| Rechner- und Benutzernamen ins Manifest schreiben lassen (`privacy.recordIdentity`) | keine Profil-Einstellung |
| Rückhaltung, Freigaberegel oder die Maskierung erkannter Passwortfelder ändern | keine Einstellungen, sondern feste Regeln ([Abschnitt 5](#5-passwort-rückhaltung)) |
| einen Aufnahmeordner, ein Exportziel oder sonst einen Pfad festlegen | Ein Profil enthält keine Pfade |
| eine Aufnahme starten | Auch `record --profile` und `support` beginnen nichts ohne die Person am Rechner und ohne sichtbaren Hinweis (P1) |

**Regeln**

- **Startmeldung.** Schützt eine Aufnahme schwächer als die Einstellungen – durch ein Profil oder eine Option der Kommandozeile –, nennt die Startmeldung jede Abweichung aus der Tabelle in Klartext, etwa „Profil ‚Schulung‘: Zwischenablage-Inhalt wird aufgezeichnet.“ oder „Schutzstufe ‚Schutznetz‘ statt ‚Streng‘.“ Das steht fest, **bevor** der Aufzeichnungskern Hooks setzt ([03](03-aufzeichnungskern.md)). Die Leiste zeigt die Meldung fünf Sekunden lang ab Aufnahmebeginn; sie lässt sich nicht abschalten, und bei ausgeblendeter Leiste erscheint sie am Rahmen ([07](07-aufnahme-bedienung.md#74-hinweise-und-warnungen)). Beim Start über die Kommandozeile steht dieselbe Aussage zusätzlich auf der Fehlerausgabe ([02](02-architektur.md#aufnahmeprofile)).
- **Jedes Mal.** Die Meldung erscheint bei jedem Start mit schwächerem Schutz – auch bei der Sofortaufnahme, die das zuletzt verwendete Profil nimmt, und auch beim hundertsten Mal. Eine Einstellung „nicht mehr anzeigen“ gibt es nicht.
- **Vorher sichtbar.** Startfenster und Profil-Editor zeigen die Abweichung schon bei der Auswahl, nicht erst beim Start ([07](07-aufnahme-bedienung.md#47-aufnahmeprofile), [Profil-Editor](07-aufnahme-bedienung.md#131-profile-bearbeiten)).
- **Festgehalten.** Die Sitzung nennt dauerhaft, womit sie entstand: `manifest.profile` mit Namen, wirksamen Einstellungen und – unter `weakens` – den Punkten, in denen sie schwächer schützte ([04](04-datenformat.md#manifest-profile)).
- **Mitgelieferte Profile** („Anleitung“, „Support-Fall“, „KI-Spur“) schützen nie schwächer als die Werkseinstellungen: Schutzstufe mindestens „Schutznetz“, Zwischenablage-Inhalt aus. Wer eines davon ändert, hat ein eigenes Profil gleichen Namens – für das die Regeln oben gelten.
- **Mitgegebene Profile.** Eine `profiles.json` neben der EXE wird gelesen, damit etwa ein Helpdesk ein passendes Profil mitliefern kann. Sie ist fremde Eingabe: Jeder Wert wird gegen seinen Wertebereich geprüft, Unbekanntes übergangen, und für die Schutzeinstellungen gilt die Startmeldung wie für jedes Profil. Die **Einstellungen** des Nutzers ändert eine mitgegebene Profildatei nie; dafür braucht es einen Import mit Einzelbestätigung ([13.4](#134-einstellungen-und-profile-weitergeben)).
- **Weitere Aufnahmeabschnitte.** Eine Fortsetzung oder Neuaufnahme übernimmt die Einstellungen des Abschnitts, an den sie anschließt; ein Profil wird nicht neu gewählt, und die Ausschlussliste kann dabei nur wachsen ([03](03-aufzeichnungskern.md#weitere-abschnitte)). Schützt der neue Abschnitt damit schwächer als die aktuellen Einstellungen, sagt die Startmeldung das wie bei einem Profil.

## 5. Passwort-Rückhaltung

### 5.1 Grundsatz

Tasteneingaben werden **zurückgehalten**, bis feststeht, wohin sie gingen, und erst dann entweder im Klartext oder als Zähler gespeichert. Die Rückhaltung betrifft ausschließlich die *Aufzeichnung*; die Eingabe selbst erreicht die Anwendung ohne jede Verzögerung (Vorab-Experiment VE-08 in [11](11-roadmap-tests-risiken.md)).

```
Hook-Zeitpunkt       Tastenereignis + Zeitstempel + Fenster-Stempel
     │
     ▼
Rückhaltepuffer      nur im Arbeitsspeicher, höchstens T_max
     │   ◄── Proben: Fokusereignisse, aktive Fokusabfrage, Fensterklasse und -stil,
     │              Terminal-Erkennung, Privattaste, Ausschlussliste
     ▼
Entscheidung je Tastenlauf
     ├─ Klartext        → key.* ins Rohprotokoll
     ├─ maskiert        → text.masked (Anzahl, Grund, Zeitraum)
     └─ ausgeschlossen  → gap (ausgeschlossene Anwendung)
```

### 5.2 Begriffe

| Begriff | Bedeutung |
|---|---|
| **Fenster-Stempel** | Kennung des Vordergrundfensters (und, soweit ohne Blockieren ermittelbar, des Fokusfensters), festgehalten **im Hook-Zeitpunkt** jeder Taste – nicht erst bei der späteren Verarbeitung ([03](03-aufzeichnungskern.md)). |
| **Fokus-Epoche** | Zeitraum, in dem der Fokus nach allem, was beobachtet wurde, auf demselben Element lag. Eine Epoche endet bei jedem beobachteten Fokus- oder Vordergrundereignis, bei jeder **fokusfähigen Eingabe** (Tab, Umschalt+Tab, Enter, Esc, F6, Strg+Tab, jede Alt- oder Windows-Kombination, jede Maustaste, jede Berührung) und bei jedem Wechsel des Fenster-Stempels. Fokusereignisse zu Elementen, die keine Texteingabe annehmen (etwa Einträge einer Vorschlagsliste unter einem Eingabefeld), beenden die Epoche nicht, solange das bisherige Element weiterhin den Tastaturfokus meldet – sonst würden Felder mit Autovervollständigung grundlos maskiert. |
| **Tastenlauf** | Zusammenhängende Tasten innerhalb einer Epoche mit gleichem Fenster-Stempel. |
| **Probe** | Eine Auskunft über das fokussierte Element mit Elementidentität, Einstufung und Messintervall (Beginn und Ende der Abfrage bzw. Ereigniszeit). |

### 5.3 Parameter

| Parameter | Standard | Bereich | Bedeutung |
|---|---|---|---|
| `T_max` | 1 500 ms | 500–3 000 | Spätestens so lange nach der letzten Taste eines Laufs fällt die Entscheidung; ohne Entscheidung wird maskiert |
| Nachlaufzeit | 300 ms | 100–1 000 | Wartezeit auf verspätete Fokusereignisse, bevor ein Lauf freigegeben wird |
| Schutzband | 50 ms | 20–200 | Unsicherheit um den Zeitpunkt eines Fokuswechsels, den die Anwendung von sich aus vornimmt; Tasten darin gehören zu beiden Nachbar-Epochen |
| Wartezeit ohne lesbaren Inhalt | 1 000 ms | 300–1 500 | Mindestabstand der Probe danach, wenn der Feldinhalt nicht lesbar ist und die Epoche mit einer fokusfähigen Taste begann |
| Zeitlimit Fokusabfrage | 400 ms | 100–1 000 | Dauer einer aktiven Abfrage, danach gilt sie als gescheitert |

### 5.4 Proben und Einstufung

| Quelle | Liefert | Besonderheit |
|---|---|---|
| Fokusereignis (UI Automation, WinEvent) | das Element, das den Fokus **erhalten hat** | Die Identität hängt am Ereignis: Auch wenn die Eigenschaften erst später gelesen werden, ist es das richtige Element – selbst wenn der Fokus inzwischen weitergewandert ist. |
| Aktive Fokusabfrage | das Element, das **jetzt** den Fokus hat | Zur Gegenprobe nach einem Tastenlauf. |
| Fensterklasse und -stil | Passwort-Stil klassischer Eingabefelder; Klasse und Programm bekannter Terminals, Remote- und VM-Fenster | sofort verfügbar aus dem Fenster-Stempel |
| Anmelde-Merkmale | Fenster der Windows-Anmeldeabfrage; Titel oder Klasse mit Begriffen wie „Windows-Sicherheit“, „Anmelden“, „Kennwort“, „Passwort“, „Password“, „Sign in“, „PIN“, „Passphrase“, „BitLocker“ | macht aus `opaque` ein „maskieren“; Liste ist erweiterbar |
| Register „war Passwortfeld“ | Elemente, die in dieser Sitzung je `password` waren | [5.8](#58-einmal-passwort-immer-passwort) |

Einstufung eines Elements: `password`, wenn eine Quelle ein Passwortfeld meldet oder das Register es kennt; `sensitive` nach Namensliste; `normal` nur für Text-Elemente (Eingabefeld, Dokument, Auswahlliste mit Eingabe) ohne jedes Passwort-Kennzeichen; sonst keine Aussage.

### 5.5 Freigaberegel

Ein Tastenlauf mit erster Taste zum Zeitpunkt t₁ und letzter Taste zum Zeitpunkt tₙ wird **nur dann im Klartext gespeichert**, wenn alle Bedingungen erfüllt sind:

1. **Ein Fenster.** Alle Tasten tragen denselben Fenster-Stempel.
2. **Probe davor.** Es gibt eine Probe mit Einstufung `normal` für ein Element E, deren Messintervall **nach** dem Beginn der Epoche liegt. (Sie darf eintreffen, nachdem schon getippt wurde – die Tasten warten.)
3. **Probe danach.** Es gibt eine Probe, deren Messung frühestens bei tₙ beginnt, und sie liefert
   - dasselbe Element E mit `normal`, **oder**
   - ein anderes Element, wobei der Wechsel durch eine **nach tₙ beobachtete Eingabe** als Epochengrenze erklärt ist (z. B. Tab oder Klick nach dem letzten Zeichen), während der Nachlaufzeit kein Fokusereignis mit Ereigniszeit zwischen t₁ und tₙ eingetroffen ist und das Fenster nicht als „Fokusereignisse unzuverlässig“ gilt.
4. **Kein Passwort-Befund in dieser Epoche.** Keine Probe zu einem Element, das den Fokus **in der Epoche des Laufs** hatte, hat `password` oder `sensitive` geliefert; E steht nicht im Register „war Passwortfeld“. Proben zu Elementen, die den Fokus erst durch eine Eingabe nach tₙ erhielten, gehören zur nächsten Epoche.
5. **Die Tasten sind in E angekommen.**
   - Lässt sich der Inhalt von E lesen, muss er sich **im Zeitraum des Laufs geändert** haben (Wertereignis oder Vergleich vorher/nachher). Bleibt er unverändert, sind die Tasten noch unterwegs oder woanders gelandet – der Lauf wird nicht freigegeben.
   - Lässt sich der Inhalt nicht lesen, muss die Probe danach mindestens die Nachlaufzeit nach tₙ gemessen sein; begann die Epoche mit einer fokusfähigen Taste (Tab, Enter …), mindestens die „Wartezeit ohne lesbaren Inhalt“.
6. **Rechtzeitig.** Die Entscheidung fällt innerhalb von `T_max` nach tₙ.

Verschiebt die Anwendung den Fokus **von sich aus** (ohne Eingabe als Grenze, z. B. automatisches Weiterspringen), ist der Zeitpunkt unscharf: Tasten, die weniger als das Schutzband vor oder nach dem Fokusereignis liegen, werden beiden Epochen zugerechnet und nur freigegeben, wenn beide die Regel erfüllen.

Ist eine Bedingung nicht erfüllt, entscheidet die Rangfolge aus [5.6](#56-rangfolge-der-entscheidung).

**Lange Läufe** werden abschnittsweise entschieden: Sobald die Bedingungen für die bisher getippten Tasten erfüllt sind, werden diese freigegeben, während weitergetippt wird. In einem bestätigten normalen Feld liegt die Rückhaltung damit typischerweise unter einer halben Sekunde. Freigegeben wird stets in der ursprünglichen Reihenfolge der Eingaben; der Puffer des Aufzeichnungskerns fasst mindestens zehn Sekunden ([03](03-aufzeichnungskern.md#andockpunkte-datenschutz)).

**Warum zwei Proben:** Fokusauskünfte treffen 50 bis 500 ms verspätet ein. Wer nach Tab sofort tippt, tippt bereits ins nächste Feld, während die letzte Auskunft noch das vorige beschreibt. Eine einzelne Auskunft kann deshalb nie belegen, wohin ein Lauf ging; erst die Übereinstimmung von „davor“ und „danach“ – oder der Nachweis, dass sich dazwischen nichts geändert hat – tut es.

**Warum zusätzlich der Feldinhalt:** Jede Fokusauskunft beschreibt, wo der Fokus *jetzt* ist – nicht, wo er sein wird, wenn die Anwendung die wartenden Tasten verarbeitet. Reagiert eine Anwendung träge, liegen Tab und die folgenden Zeichen noch in ihrer Warteschlange, und zwei Auskünfte nennen übereinstimmend das *alte* Feld. Erst die Änderung des Feldinhalts beweist, dass die Tasten dort angekommen sind. Die [Schritterkennung](05-schritterkennung.md#belegstufen) stützt sich auf denselben Abgleich, verlangt aber mehr: Eine Texteingabe gilt dort erst als „belegt“, wenn der Feldinhalt die Tastenfolge **bestätigt** (`valueConfirmed`). Für die Freigabe hier genügt, dass sich der Inhalt im Zeitraum des Laufs geändert hat – die beiden Regeln beantworten verschiedene Fragen („wohin gingen die Tasten?“ und „was kam dabei heraus?“).

**„Fokusereignisse unzuverlässig“:** Liefert eine aktive Abfrage ein anderes Element als das letzte Fokusereignis, ohne dass eine Epochengrenze dazwischenlag, gilt das Fenster für den Rest der Sitzung als unzuverlässig. Dort zählt nur noch die Variante „dasselbe Element E“ aus Bedingung 3.

**Beispiele**

| Ablauf | Ergebnis |
|---|---|
| Klick ins Feld „Benutzername“, tippen, Pause | Probe davor (Fokusereignis) und danach (aktive Abfrage) nennen dasselbe normale Feld → Klartext |
| Tab ins Passwortfeld, sofort tippen | Das Fokusereignis für das Passwortfeld trifft während des Tippens ein → `password` → maskiert. Träfe es gar nicht ein: keine gültige Probe nach Epochenbeginn → `unresolved` → maskiert |
| Benutzername tippen, Tab, Passwort tippen, Enter – alles in einer Sekunde | Lauf 1: Probe davor `normal`, Feldinhalt änderte sich, danach folgt die Eingabe-Grenze Tab ohne Fokusereignis im Lauf → Klartext. Lauf 2: Fokusereignis `password` → maskiert |
| Wie zuvor, aber die Anwendung hängt eine halbe Sekunde: Tab und Passwort liegen noch in ihrer Warteschlange | Beide Auskünfte nennen noch das Feld „Benutzername“ (`normal`). Dessen Inhalt ändert sich im Zeitraum des zweiten Laufs aber nicht → Bedingung 5 verletzt → nicht freigegeben. Sobald die Anwendung aufholt, trifft das Fokusereignis `password` ein → maskiert |
| Normales Feld, dessen Inhalt sich nicht lesen lässt; Tab, sofort weitertippen | Freigabe erst nach der Wartezeit ohne lesbaren Inhalt und nur, wenn bis dahin kein Fokusereignis eintraf; sonst maskiert |
| Feld für Einmalcode springt nach jeder Ziffer automatisch weiter | Jedes Weiterspringen ist ein Fokusereignis mitten im Lauf → Bedingung 3 verletzt → maskiert (zusätzlich meist `sensitive` nach Name) |
| Anwendung meldet keine Fokusereignisse | Erste Abweichung markiert das Fenster als unzuverlässig; Läufe ohne übereinstimmende Probe danach werden maskiert |

### 5.6 Rangfolge der Entscheidung

Die erste zutreffende Zeile gilt:

| Rang | Bedingung | Ergebnis | Grund im Protokoll |
|---|---|---|---|
| 1 | Fenster-Stempel gehört zu einer ausgeschlossenen Anwendung | nicht aufgezeichnet | Lücke mit Ursache `excludedApp` |
| 2 | Passwort-Befund (Probe oder Register) | maskiert | `passwordField` bzw. `stickyPassword` |
| 3 | Sensibles Feld nach Name | maskiert | `sensitiveField` |
| 4 | Passwortabfrage im Terminal erkannt (Privatmodus mit Auslöser `terminalPrompt`) | maskiert | `terminalPrompt`, dazu Regel und Abfragezeile ([6.3](#63-was-gespeichert-wird)) |
| 5 | Serie aus einem Passwortmanager erkannt | maskiert | `autoType` |
| 6 | Privatmodus der **Privattaste** aktiv – oder Privattaste gedrückt, während Tasten des laufenden Laufs noch zurückgehalten sind | maskiert | `privacyMode` |
| 7 | Freigaberegel erfüllt | **Klartext** | – (Kontext `textField`) |
| 8 | Undurchsichtige Oberfläche mit Anmelde-Merkmalen | maskiert | `loginWindow` |
| 9 | Undurchsichtige Oberfläche | Schutznetz: **Klartext** (Kontext `opaque`), geführt als auffällige Eingabe; Streng: maskiert | – bzw. `strictUnknown` |
| 10 | alles andere | maskiert | `unresolved` |

Die Bezeichner sind die Werte von `text.masked.reason` in [04](04-datenformat.md#ev-text-masked). `unresolved` hat bewusst einen eigenen Wert: Es tritt in **beiden** Stufen auf und ließe sich mit `strictUnknown` nicht wahrheitsgemäß ausdrücken.

Die Ränge 2 bis 6 führen alle zum Maskieren; ihre Reihenfolge bestimmt nur, **welcher Grund** im Protokoll steht. Die genauere Erkennung geht vor der Privattaste: Drückt der Nutzer die Privattaste an einer Abfrage, die das Tool ohnehin erkannt hat, trägt der Lauf den Grund `terminalPrompt` samt Abfragezeile; tippt er im Privatmodus in ein erkanntes Passwortfeld, steht `passwordField`. So geht keine Auskunft verloren, nur weil der Nutzer vorsichtig war. Der Privatmodus der Privattaste steht trotzdem mit Ein- und Ausschalten im Protokoll und wirkt unverändert auf alles Übrige (Feldinhalte, Zwischenablage, [Abschnitt 7](#7-privattaste)).

### 5.7 Was beim Maskieren gespeichert wird

- Ein Eintrag `text.masked` ([04](04-datenformat.md#ev-text-masked)) mit **Anzahl** der maskierten Tastenanschläge (`count`), Grund (`reason`), Beginn und Ende des Laufs (`t`, `tEnd`). Keine Tastencodes, keine Zeichen, **keine Zeiten einzelner Tasten** – der Tipprhythmus kann Rückschlüsse auf das Getippte erlauben.
- Dazu, allein für die Buchführung der beiden Eingabekanäle, die Eingabenummern des ersten und letzten zurückgehaltenen Ereignisses und deren Zahl (`inSeq`, `inSeqEnd`, `events`). Das sind Zähler, keine Inhalte.
- Maskiert werden alle Tasten, die Zeichen erzeugen, sowie Bearbeitungs- und Bewegungstasten im Feld (Rücktaste, Entf, Pfeile, Pos1, Ende).
- **Was die Anzahl zählt:** jeden Tastendruck des Laufs außer reinen Umschalttasten (Umschalt, Strg, Alt, AltGr, Win) – Zeichen, Korrekturen und Bewegungen im Feld, auch Wiederholungen durch Halten. Die Anzahl ist deshalb nicht die Länge des Eingegebenen: Wer ein achtstelliges Kennwort tippt und sich einmal mit der Rücktaste korrigiert, erzeugt die Anzahl 10.
- **Tasten ohne Inhalt** (Enter, Tab, Esc) und Tastenkombinationen bleiben als gewöhnliche Ereignisse erhalten; sie sind für das Verständnis des Ablaufs nötig.
- **Anzahl nicht speichern** (*Kann*, Einstellung `privacy.storeMaskedCount` = aus): für Fälle, in denen schon die Passwortlänge schützenswert ist. Dann fehlen `count`, `inSeqEnd` und `events`, und die Eingabenummer springt nach dem Lauf um einen zufälligen Betrag weiter, damit sich die Länge auch nicht aus der Nummernlücke ablesen lässt. Ausgaben zeigen in diesem Fall `{masked}` ohne Zahl ([06](06-ki-export-und-mcp.md#53-schreibweise)).
- Die Entscheidung gilt für **alle Kanäle**: Der zweite Eingabekanal (Raw Input) liefert dieselben Tasten und unterliegt derselben Rückhaltung; sein Abgleich mit dem Hook speichert für maskierte Läufe nur Zähler.

### 5.8 Einmal Passwort, immer Passwort

Ein Element, das einmal als `password` eingestuft wurde, bleibt es für die ganze Sitzung – auch wenn die Anwendung es nach „Passwort anzeigen“ als gewöhnliches Textfeld meldet.

- Wiedererkennung über die Laufzeitkennung des Elements; ersetzt die Anwendung das Feld beim Umschalten, über gleiches Fenster, gleiche Kennung oder Beschriftung und überlappendes Rechteck.
- Passwortfelder werden auch ohne Fokus registriert, wenn sie im Text-Schnappschuss eines neuen Fensters auftauchen.
- Die Wiedererkennung ist eine Heuristik; ihr Versagen ist ein benanntes Restrisiko ([Abschnitt 14](#14-bedrohungsmodell)).

### 5.9 Schwärzung des Felds im Bild

- Das Rechteck jedes registrierten Passwortfelds wird in jedem Bild **vor dem Speichern** gefüllt (`frame.masks` mit Grund `passwordField`, [04](04-datenformat.md#ev-frame)) – auch wenn nur Punkte zu sehen wären, denn „Passwort anzeigen“ macht daraus Klartext. Viewer und Exporte zeigen an der Stelle eine schwarze Fläche.
- Bilder durchlaufen dafür dieselbe Rückhaltung wie Tasten: Ein Bild wird erst geschrieben, wenn für seinen Zeitpunkt feststeht, welche Passwortfelder sichtbar waren (höchstens `T_max` Verzögerung).
- Solange ein registriertes Passwortfeld offengelegt ist (es meldet sich nicht mehr als Passwortfeld), wird sein Rechteck für **jedes** gespeicherte Bild neu ermittelt. Gelingt das nicht rechtzeitig, wird in diesem Bild das ganze Fenster geschwärzt und das im Protokoll vermerkt.
- Die Maske ist endgültig: Die Bildpunkte existieren in der Sitzung nicht. Das Bild trägt einen Vermerk über Art und Lage der Maske, damit Viewer und Exporte einen Platzhalter zeichnen können ([04](04-datenformat.md)).

### 5.10 Sensible Felder nach Name

*Soll.* Normale Textfelder, deren Name, Beschriftung, Kennung oder Platzhaltertext auf Geheimes hindeutet, werden wie Passwortfelder behandelt. Vorbelegte Liste (erweiterbar, Groß-/Kleinschreibung egal, Wortgrenzen beachtet): PIN, TAN, PUK, CVC, CVV, Prüfziffer, Sicherheitscode, Kartennummer, Kreditkarte, IBAN, Einmalcode, Bestätigungscode, Verifizierungscode, 2FA, OTP, Passphrase, Geheimnis, Secret, Token, API-Schlüssel, API key, Wiederherstellungsschlüssel, Recovery key.

Die Liste ist bewusst großzügig; ein zu Unrecht maskiertes Feld kostet eine Lücke, ein zu Unrecht gespeichertes ein Geheimnis.

### 5.11 Eingaben von Passwortmanagern

*Soll.* Passwortmanager tippen Benutzername und Passwort als künstlich erzeugte Tastenfolge. Trifft sie auf ein erkanntes Passwortfeld, greift die normale Regel. Für alle anderen Fälle gilt: Eine Serie von mindestens acht künstlich erzeugten Zeichen mit weniger als 15 ms Abstand wird vollständig maskiert, wenn in den zehn Sekunden davor ein Programm der Passwortmanager-Liste ([Abschnitt 8](#8-ausschlussliste)) im Vordergrund war oder seine Schnelltaste gedrückt wurde. Der mitmaskierte Benutzername ist der Preis der Vorsicht.

### 5.12 Arbeitsspeicher

- Der Rückhaltepuffer und der gemeinsame Speicherbereich zwischen Capture-Host und Hauptprozess liegen ausschließlich im Arbeitsspeicher: anonym (nicht über einen Namen zu öffnen, Weitergabe nur per Handle an den eigenen Kindprozess) und nicht dateigestützt.
- Beide werden gegen Auslagerung gesperrt und nach Entscheidung bzw. Verbrauch mit Nullen überschrieben.
- Diagnoseprotokolle enthalten nie Tasteninhalte, Zeichen oder Feldwerte – auch nicht auf der ausführlichsten Stufe.
- Die Bereiche werden, soweit das Betriebssystem es anbietet, von Absturzabbildern ausgenommen; das Tool schreibt selbst keine Speicherabbilder.
- Restrisiko: Im Ruhezustand schreibt Windows den gesamten Arbeitsspeicher auf den Datenträger. Der Zeitraum ist auf `T_max` begrenzt.

### 5.13 Anzeige während der Aufnahme

Die Rückmeldung in der Aufnahmeleiste zeigt zurückgehaltene Eingaben ohne Inhalt („Texteingabe …“) und maskierte als „●●● maskiert“. Getippter Text erscheint dort erst nach der Freigabe.

## 6. Passwortabfragen im Terminal

> **Stand der Umsetzung: umgesetzt, Abdeckung noch nicht im echten Terminal bestätigt.** Die Regeln T1 bis T3 und die selbsttätige Maskierung dieses Abschnitts (DS-022, DS-023) sind gebaut: die Regeln als reine, durch Tabellentests geprüfte Funktion (`TerminalPrompt`), das Lesen des Terminaltexts über das TextPattern von UI Automation auf dem UIA-Arbeiter (`UiaService.TerminalTail`), die Steuerung im Recorder (`Recorder.Terminal`). Was noch fehlt, ist der **Versuch an echten Terminals** (O-09 in [11](11-roadmap-tests-risiken.md)): ob Windows Terminal und die klassische Konsole die Zeile an der Schreibmarke so liefern wie angenommen (`Read-Host -AsSecureString`, `runas`, `sudo` in WSL, `ssh`), wie lange das Lesen dauert (Ziel p95 unter 100 ms) und ob Fehlalarme in der Praxis selten bleiben. Bis dieser Versuch bestanden ist, gilt die Erkennung als zusätzliche Schicht – die übrigen Schutzmittel bleiben unverändert:
>
> - **Durchsichtspflicht.** Getipptes in Terminal-, Remote-Desktop- und VM-Fenstern, das nicht maskiert wurde (am Kennzeichen `flags.plaintext`), ist auffällig. Es erreicht Wege ohne Oberfläche nur als Zähler `{unreviewed:N}`, sperrt Exporte aus dem Viewer und braucht eine Einzelentscheidung ([11.1](#111-durchsicht-aller-eingaben)). Als Grund nennt die Durchsicht „Eingabe in einem Fenster ohne erkennbare Passwortfelder (Terminal, Remote-Desktop, VM)“. Eine erkannte und maskierte Abfrage braucht keine Durchsicht.
> - **„Streng“ maskiert dort alles Getippte** ([4.2](#42-verhalten-je-stufe)); die Erkennung läuft in dieser Stufe nicht, weil sie nichts hinzufügen würde.
> - **Privattaste** ([Abschnitt 7](#7-privattaste)) für alles, was die Regeln nicht erkennen ([6.4](#64-grenzen)).
>
> Festlegungen der Umsetzung, die über den Text unten hinausgehen:
>
> - **Wann gelesen wird:** 0,3 s, 1 s und 2,5 s nach jeder Eingabetaste im Terminal und nach dem Wechsel in ein Terminal (T1 „wenn sie erscheint“), sofort beim ersten Zeichen eines Tastenlaufs („vorher“, T1) und 0,12 s nach dem zweiten Zeichen („nachher“, T2 und T3) – spätestens 0,5 s vor Ablauf von `T_max`, dann auch mit nur einem Zeichen. Bis zur Entscheidung hält die Rückhaltung alle Tasten des Laufs zurück; ohne Entscheidung in `T_max` werden sie maskiert (Grund `unresolved`), der Rest des Laufs ebenso.
> - **Was gelesen wird:** die Zeilen bis zur Schreibmarke (leere Auswahl des TextPatterns), sonst die sichtbaren Zeilen; davon die letzten drei nicht leeren. Steht die Schreibmarke nach einer Eingabetaste auf einer leeren Zeile, gilt eine Abfrage darüber als beantwortet; steht nach dem Ende einer Abfrage noch derselbe Text da, wird sie nicht erneut erkannt.
> - **Keine Abfrage sind** gewöhnliche Eingabeaufforderungen von Shells (`PS C:\…>`, `C:\…>`, `>>>`) – auch dann nicht, wenn ein Verzeichnisname ein Schlüsselwort enthält oder das Echo verspätet ankommt; `pin` zählt nur als eigenes Wort (nicht in „mapping“). Doppelpunkt und Fragezeichen in voller Breite (`：`, `？`) gelten wie die gewöhnlichen Zeichen.
> - **Kein Textzugriff:** Scheitert das Lesen in einem Fenster dreimal in Folge (kein TextPattern, Fehler) oder einmal mit Zeitüberschreitung, liest der Recorder dort für den Rest der Aufnahme nicht mehr; es gelten Hinweis, Privattaste und Durchsicht. Alle Abfragen laufen über den UIA-Arbeiter mit seinem Zeitlimit; ein Fehler heißt „keine Erkennung“ und verzögert nie die Eingabe selbst.
> - **Endgrund im Rohprotokoll:** Feld `end` mit den Werten aus [04](04-datenformat.md#ev-system) (`enter`, `cancelCtrlC`, `cancelCtrlD`, `cancelEscape`, `focusChange`, `timeout`, `stopped`); die Ausgaben nennen ihn als `endReason`.

Terminals haben keine Passwortfelder. In der Stufe „Schutznetz“ soll das Tool Passwortabfragen deshalb am **Text des Terminals** erkennen.

### 6.1 Voraussetzung

Das Terminal muss seinen Text über UI Automation anbieten. Das gilt für Windows Terminal und die klassische Konsole (und damit für PowerShell, Eingabeaufforderung, WSL und `ssh` darin). Für Terminals ohne Textzugriff (z. B. PuTTY, manche integrierte Terminals) gibt es **keine** Erkennung; dort bleiben Hinweis, Privattaste und Durchsicht.

Gelesen wird nur das Ende des sichtbaren Textes (die letzten drei nicht leeren Zeilen), jeweils nach einer Enter-Taste, nach Beruhigung der Ausgabe und nach den ersten Zeichen eines neuen Tastenlaufs. Der gelesene Text wird nicht gespeichert – ausgenommen die erkannte Abfragezeile (6.3).

### 6.2 Regeln

Ein Tastenlauf im Terminal wird maskiert, wenn **eine** Regel zutrifft:

| Regel | Bedingung | Beispiele |
|---|---|---|
| **T1 Mustertreffer** | Die letzte nicht leere Zeile endet auf ein Abfragemuster. | `admin@srv-app01's password:` · `[sudo] password for admin:` · `[sudo] Passwort für admin:` · `Enter passphrase for key 'C:\Users\…\id_ed25519':` · `Password for 'https://user@example.org':` · `Geben Sie das Kennwort für "KONTO" ein:` · `Kennwort:` |
| **T2 Maskenecho** | Getippte Zeichen erscheinen als gleich viele Maskenzeichen (`*`, `•`, `●`). | `Read-Host -AsSecureString`, viele Installationsprogramme |
| **T3 Kein Echo an einer Abfrage** | Mindestens zwei Zeichen getippt, der Text am Zeilenende ändert sich nicht, und die Zeile endet auf Doppelpunkt, Fragezeichen oder Größer-Zeichen. | Abfragen mit beliebigem, auch fremdsprachigem Text |

Allgemeines Muster für T1 (als Daten hinterlegt, erweiterbar): ein Schlüsselwort – `password`, `passwort`, `kennwort`, `passphrase`, `passcode`, `pin`, `secret`, `token`, `mot de passe`, `contraseña`, `senha`, `wachtwoord`, `hasło`, `пароль`, `密码`, `パスワード` – gefolgt von höchstens 60 weiteren Zeichen und einem abschließenden Doppelpunkt, Fragezeichen oder Größer-Zeichen am Zeilenende.

- T1 genügt allein, auch wenn das Echo-Verhalten unbekannt ist. Fehlalarme kosten eine Lücke, kein Geheimnis.
- Die Prüfung läuft innerhalb der Rückhaltezeit; die ersten Zeichen des Laufs sind noch nicht gespeichert, wenn die Entscheidung fällt.
- T3 gilt nur an der Eingabezeile. In Vollbild-Programmen des Terminals (Editoren, Dateimanager) trifft die Bedingung „Zeile endet auf Doppelpunkt“ selten zu; verbleibende Fehlalarme sind hingenommen.

**Selbsttätige Maskierung je Abfragezeile.** Die Erkennung braucht kein Zutun des Nutzers:

| Punkt | Festlegung |
|---|---|
| Beginn | Sobald eine Abfragezeile erkannt ist – bei T1 schon, wenn sie erscheint, bei T2 und T3 mit den ersten Zeichen des Laufs –, schaltet das Tool den Privatmodus mit dem Auslöser `terminalPrompt` ein. Leiste und Rahmen zeigen ihn wie den Privatmodus der Privattaste, mit dem Zusatz „Passwortabfrage erkannt“. |
| Wirkung | wie im Privatmodus ([Abschnitt 7](#7-privattaste)): Zeichen- und Bearbeitungstasten werden maskiert, hier mit dem Grund `terminalPrompt`; Tasten ohne Inhalt bleiben erhalten |
| Ende | mit der **Eingabetaste** – außerdem bei Abbruch der Abfrage mit Strg+C, Strg+D oder Esc, bei einem Fokuswechsel, nach 60 s ohne Taste sowie beim Ende der Aufnahme. Das Protokoll hält den Endgrund fest (im Rohprotokoll `end`: `enter`, `cancelCtrlC`, `cancelCtrlD`, `cancelEscape`, `focusChange`, `timeout`, `stopped`; [04](04-datenformat.md#ev-system)), und jede Ausgabe nennt ihn ([06](06-ki-export-und-mcp.md#73-leerlauf-und-hinweiszeilen)). Eine Pause beendet die Maskierung nicht: Während der Pause wird ohnehin nichts aufgezeichnet, und nach dem Fortsetzen gilt sie weiter, bis einer der Endgründe eintritt. |
| Mehrere Abfragen | **Jede Abfragezeile wird für sich behandelt.** Nach dem Ende wird der Text erneut gelesen. Fragt das Programm noch einmal – neues Kennwort und Wiederholung, Passphrasen mehrerer Schlüssel, erneute Abfrage nach einem Tippfehler –, beginnt für diese Zeile eine neue Maskierung mit eigenem Eintrag. Zwischen zwei Abfragen ist der Privatmodus aus. |
| Schnelle Folgeabfrage | Erscheint die nächste Abfrage so schnell, dass der Nutzer schon tippt, bevor sie gelesen ist, greift die Rückhaltung: Die Tasten warten bis zu `T_max` auf das Ergebnis der Prüfung und werden ohne Ergebnis maskiert (Grund `unresolved` oder, im Terminal der Stufe „Schutznetz“, nach Regel T3). |
| Verhältnis zur Privattaste | unabhängig. Die Privattaste kann die selbsttätige Maskierung einer erkannten Abfragezeile nicht aufheben ([Abschnitt 7](#7-privattaste)). |

### 6.3 Was gespeichert wird

Je erkannter Abfragezeile, in der getippt wurde, ein Eintrag `text.masked` ([04](04-datenformat.md#ev-text-masked)) mit dem Grund `terminalPrompt`, der zutreffenden Regel (`rule`: `T1`, `T2` oder `T3`) und – als aufgezeichneter Bildschirmtext – der **Abfragezeile** (`promptLine`), gekürzt auf 120 Zeichen. Sie macht das Protokoll verständlich („Abfrage: `[sudo] password for admin:`“) und ist ohnehin im Bild zu sehen. Die Abfragezeile ist aufgezeichneter Text wie jeder andere: Sie lässt sich schwärzen und unterliegt der Entschärfung in Ausgaben.

Im Protokoll erscheint die Erkennung außerdem als Ein- und Ausschalten des Privatmodus (`system`-Ereignis `privacyMode` mit `trigger: "terminalPrompt"` und der Regel) und als Lücke mit der Ursache `privacyMode` für den Zeitraum ([04](04-datenformat.md#ev-system)).

### 6.4 Grenzen

| Fall | Folge |
|---|---|
| Terminal ohne Textzugriff | keine Erkennung; Schutznetz N1, N2, N5 |
| Passwort als Befehlsargument (`net use … /user:x geheim`, `mysql -pgeheim`) | wird als gewöhnlicher Befehl im Klartext aufgezeichnet; die Musterprüfung vor dem Export weist auf typische Formen hin |
| Anmeldung innerhalb einer Remote-Desktop-Sitzung oder VM | kein Textzugriff, keine Erkennung; Schutznetz bzw. Stufe „Streng“ |
| Abfrage ohne Schlüsselwort und mit sichtbarem Echo | nicht erkennbar |

Diese Grenzen stehen als Risiko R-05 in [11](11-roadmap-tests-risiken.md).

## 7. Privattaste

Die Privattaste ist das Werkzeug des Nutzers für alles, was das Tool nicht selbst als sensibel erkennen kann: ein Passwort in einer Remote-Sitzung, eine Kundennummer, eine private Notiz.

| Punkt | Festlegung |
|---|---|
| Auslöser | Hotkey und Schaltfläche in der Aufnahmeleiste ([07](07-aufnahme-bedienung.md#10-pause-privattaste-letzten-schritt-verwerfen)); **Umschalter** ein/aus |
| Anzeige | Leiste und Rahmen zeigen den Privatmodus an |
| Wirkungsbeginn | **Garantiert ab dem nächsten Tastendruck** nach dem Einschalten. Zusätzlich werden Tasten des gerade laufenden Tastenlaufs mitmaskiert, soweit sie noch zurückgehalten und nicht gespeichert sind – typischerweise die letzten Zehntelsekunden. Bereits abgeschlossene Läufe (etwa der Befehl vor der Passwortabfrage) bleiben unberührt. |
| Ende | erneutes Drücken oder Stopp. Die Privattaste ist ein **Umschalter ohne automatisches Ende**: Ihr Privatmodus endet nie von selbst – auch nicht mit der Eingabetaste, weil viele Programme ein Passwort zweimal abfragen und der Nutzer selbst am besten weiß, wann das Vertrauliche vorbei ist. Eine Variante „bis zur Eingabetaste“ gibt es nicht. |
| Erinnerung | Bleibt der Privatmodus die eingestellte Zeit (30, 60 oder 120 s; Standard 60 s) ohne Taste aktiv, erinnert der Recorder einmal daran – in der Meldezeile, am Symbol im Infobereich und bei ausgeblendeter Leiste mit einem Ton ([07](07-aufnahme-bedienung.md#74-hinweise-und-warnungen)). Er erinnert nur und schaltet nichts aus – damit später nicht versehentlich ein ganzer Abschnitt fehlt |
| Zusammenspiel mit der Terminal-Erkennung | Die Erkennung von Passwortabfragen ([Abschnitt 6](#6-passwortabfragen-im-terminal)) maskiert **zusätzlich und selbsttätig** jede erkannte Abfragezeile bis zur Eingabetaste. Beide Auslöser gelten unabhängig voneinander; der Privatmodus ist aktiv, solange mindestens einer gilt. Die Privattaste schaltet nur ihren eigenen Teil: Drückt der Nutzer sie während einer erkannten Abfrage, **schaltet das ihren Privatmodus ein** (nie die Erkennung aus), und er bleibt über die Eingabetaste hinaus bestehen, bis die Taste erneut gedrückt wird. |

**Wirkung im Privatmodus**

| Datenart | Verhalten |
|---|---|
| Zeichen- und Bearbeitungstasten | maskiert (Zähler, Grund `privacyMode`) |
| Tasten ohne Inhalt, Tastenkombinationen | aufgezeichnet |
| Klicks, Mausweg | aufgezeichnet |
| Feldinhalte, Wertänderungen, Werte in Text-Schnappschüssen | nicht aufgezeichnet |
| Zwischenablage-Inhalt | nicht aufgezeichnet |
| Bilder | aufgezeichnet. Wer auch keine Bilder will, nimmt die Pause. *Kann:* Einstellung „Im Privatmodus auch Bilder aussetzen“ – Bilder werden dann mit Grund `privacyMode` gefüllt. |

Abgrenzung: **Pause** setzt alles aus und ist für längere Unterbrechungen gedacht; der **Privatmodus** hält den Ablauf nachvollziehbar und lässt nur den Inhalt weg.

Weitere Festlegungen:

- Ein- und Ausschalten stehen im Protokoll als `system`-Ereignis `privacyMode` mit dem Auslöser (`hotkey`, `toolbar` oder `terminalPrompt`), der Zeitraum als Lücke mit Ursache `privacyMode` ([04](04-datenformat.md#ev-system)); sie sind kein Nutzerschritt. Der Hotkey ist als Bedienung des Recorders gekennzeichnet und zählt nie als Eingabe des Nutzers.
- Jeder maskierte Lauf ist eine Inhaltslücke und steht in der Lückenübersicht jeder Ausgabe. Wie beide Auslöser im KI-Protokoll erscheinen, zeigt [Beispiel B in 06](06-ki-export-und-mcp.md#14-beispiel-b-sitzung-mit-lücken).
- **Letzte Eingabe schwärzen** (umgesetzt: Menü der Leiste, kein vorbelegtes Kürzel) – für den Fall, dass die Privattaste zu spät kam. Der Befehl wirkt in zwei Stufen:
  1. **Sofort:** Er legt eine Zeitraum-Schwärzung über den zuletzt gespeicherten Tastenlauf an (in `edits.json` eine Schwärzung der Art `range` über dessen Ereignisnummern, [10.2](#102-arten-von-schwärzungen)). Von diesem Moment an gibt keine Ausgabe den Inhalt mehr her – auch nicht die mitlaufende Anzeige der Aufnahmeleiste.
  2. **Beim Stopp:** Die Schwärzung wird endgültig angewendet ([10.8](#108-endgültiges-schwärzen), Weg „an Ort und Stelle“): Der Inhalt wird aus dem Rohprotokoll entfernt, bevor die Sitzung abgeschlossen wird. Das ist die ausdrückliche Ausnahme vom Grundsatz, dass das Rohprotokoll nur angehängt wird ([02](02-architektur.md#schreiben), [03](03-aufzeichnungskern.md)). Ein versehentlich getipptes Kennwort überdauert die Aufnahme damit nicht im Klartext.

  Endet die Aufnahme durch einen Absturz, bleibt die Schwärzung der ersten Stufe wirksam; das endgültige Anwenden wird bei der Wiederherstellung der Sitzung nachgeholt. Nicht zu verwechseln mit „Letzten Schritt verwerfen“ aus [07](07-aufnahme-bedienung.md#10-pause-privattaste-letzten-schritt-verwerfen): Das markiert eine Fehlbedienung und löscht nichts.
- In der Stufe „Streng“ ist zusätzlich die **Freigabetaste** vorgesehen (*nicht umgesetzt*): Sie erlaubt Klartext im aktuellen undurchsichtigen Fenster, bis das Fenster gewechselt wird, und ist in der Leiste ebenso sichtbar. Ohne sie bleibt in dieser Stufe nur, die Stufe für die Aufnahme zu wechseln.

## 8. Ausschlussliste

### 8.1 Regeln

Eine Regel besteht aus einem **Anzeigenamen** und mindestens einem Merkmal: Programmname, Pfadmuster, Fenstertitel-Muster, Fensterklasse. Der Anzeigename erscheint in Lückenmarkern anstelle des Programmnamens, damit die Aufzeichnung nicht verrät, was genau ausgeschlossen wurde, wenn der Nutzer das nicht will.

### 8.2 Vorbelegung

| Gruppe | Merkmale | Hinweis |
|---|---|---|
| Passwortmanager | Programme wie KeePass, KeePassXC, 1Password, Bitwarden, Dashlane, Enpass, NordPass, Keeper, RoboForm, LastPass, Proton Pass | Liste ist Datei, erweiterbar |
| Private Browserfenster | Titel-Muster, z. B. Edge „[InPrivate]“, Firefox „Privater Modus“ bzw. „Private Browsing“; Tor Browser über den Programmpfad | Chrome und verwandte Browser kennzeichnen Inkognito-Fenster nicht im Titel – siehe 8.5 |
| Eigene Regeln | vom Nutzer | z. B. Banking, Personalsoftware, Messenger |

Die Muster werden bei jedem Release gegen die aktuellen Programmversionen geprüft.

### 8.3 Wirkung

| Datenart | Verhalten |
|---|---|
| Tastatur und Maus, solange ein ausgeschlossenes Fenster den Fokus hat | nicht aufgezeichnet; ein Lückenmarker (`gap` mit Ursache `excludedApp`) mit Zeitraum und dem Anzeigenamen der Regel in `detail.rule` ([04](04-datenformat.md#ev-gap)). **Keine Zähler:** Weder die Zahl der Tasten noch die der Klicks, Mausradbewegungen oder Mausbewegungen wird festgehalten – schon der Umfang der Eingaben in einem Passwortmanager oder einem privaten Fenster geht die Aufzeichnung nichts an. |
| Bilder | Alle sichtbaren Fenster ausgeschlossener Anwendungen werden in jedem Bild **vor dem Speichern** schwarz gefüllt (`frame.masks`, Grund `excludedApp`) – auch wenn sie nur im Hintergrund zu sehen sind. Alle Ausgaben zeigen dort eine schwarze Fläche. |
| Elementinformationen, Text-Schnappschüsse | keine |
| Fenstertitel, Programmname, Pfad | nicht gespeichert (Titel enthalten oft Kontonamen); im Protokoll steht nur der Anzeigename der Regel (`window.info.excludedRule`, `titleWithheld: "excludedApp"`) |
| Zwischenablage | Inhalt aus ausgeschlossener Quelle wird nie aufgezeichnet, auch nicht bei eingeschalteter Inhaltsaufzeichnung |

### 8.4 Entscheidung ohne Wettlauf

- **Eingaben:** Entschieden wird anhand des Fenster-Stempels aus dem Hook-Zeitpunkt ([5.2](#52-begriffe)), nicht anhand des später gemeldeten Vordergrundwechsels. Die erste Taste in einem ausgeschlossenen Fenster ist damit bereits ausgeschlossen.
- **Bilder:** Die Rechtecke ausgeschlossener Fenster werden laufend mitgeführt. Für jedes Bild gilt die Vereinigung der Rechtecke aus dem Zeitraum ±100 ms um den Bildzeitpunkt, damit bewegte Fenster nicht aus der Maske rutschen. Bilder durchlaufen die Rückhaltung aus [5.9](#59-schwärzung-des-felds-im-bild); ein neu erschienenes Fenster ist eingestuft, bevor das erste Bild mit ihm geschrieben wird.
- Geschwärzt wird das ganze Fensterrechteck einschließlich Rahmen, auch wenn das Fenster teilweise verdeckt ist.

### 8.5 Bekannte Grenzen

| Grenze | Warum | Umgang |
|---|---|---|
| **Benachrichtigungen** (Toasts) | Sie gehören dem System, nicht der Anwendung, und lassen sich keiner Ausschlussregel zuordnen. Auch Benachrichtigungen anderer Programme können Privates zeigen. | Dokumentiert; das Startfenster empfiehlt „Nicht stören“. *Kann:* Einstellung „Benachrichtigungen in Bildern schwärzen“. |
| **Alt+Tab, Aufgabenansicht, Taskleisten-Vorschau** | Sie zeigen Miniaturen ausgeschlossener Fenster in Fenstern des Systems. | *Soll:* Solange ein ausgeschlossenes Fenster existiert, wird die gesamte Umschalt- bzw. Vorschaufläche geschwärzt. Bis dahin dokumentierte Grenze. |
| Taskleisten-Symbol und Programmname | Teil der Taskleiste | nicht geschwärzt; Schwärzung von Hand möglich |
| Inkognito-Fenster ohne Titelmerkmal | kein verlässliches Merkmal im Titel | *Soll:* Erkennung über das Inkognito-Symbol in der Fensterleiste (UI Automation, einmal je Fenster). Bis dahin: eigene Regel für den Browser oder Aufnahmebereich „eine Anwendung“. |
| Inhalte ausgeschlossener Programme an anderer Stelle | z. B. ein kopierter und woanders eingefügter Text | nicht erkennbar; Musterprüfung und Durchsicht |

### 8.6 Die stärkere Alternative

Der Aufnahmebereich **„eine Anwendung“** ([07](07-aufnahme-bedienung.md#52-eine-anwendung)) kehrt die Logik um: Statt Bestimmtes auszuschließen, wird nur Bestimmtes aufgenommen; alles andere wird vor dem Speichern entfernt. Für Aufnahmen in sensibler Umgebung ist das die empfohlene Wahl.

**Was von der Arbeit außerhalb des Aufnahmebereichs bleibt.** Eingaben außerhalb erscheinen als Lücke mit der Ursache `outOfScope`. Anders als bei einer ausgeschlossenen Anwendung darf diese Lücke sagen, **wie viel** außerhalb geschah:

| | Ausgeschlossene Anwendung (`excludedApp`) | Außerhalb des Aufnahmebereichs (`outOfScope`) |
|---|---|---|
| Wer hat entschieden | der Nutzer hat die Anwendung ausdrücklich als vertraulich gekennzeichnet | der Nutzer hat den Bereich nur nicht ausgewählt |
| Zeitraum der Lücke | ja | ja |
| Ereigniszahlen | **nein, keine** | **ja:** Zahl der nicht aufgezeichneten Ereignisse je Art in `gap.detail.counts` – Tasten (`keys`), Klicks (`clicks`), Mausradbewegungen (`wheel`), Mausbewegungen (`moves`) |
| Inhalte, Tastencodes, Positionen, Einzelzeiten | nie | nie |
| Fenstertitel, Programmname | nie; nur der Anzeigename der Regel | nie (`titleWithheld: "outOfScope"`) |
| Bildpunkte | vor dem Speichern schwarz gefüllt | vor dem Speichern schwarz gefüllt (alles außerhalb von `frame.scopeRects`) |

Die Zähler helfen zu verstehen, warum der Zustand der Anwendung sich nach der Lücke geändert haben kann („14 Tastendrücke, 3 Klicks – nur gezählt, nicht aufgezeichnet“). Gezählt wird im Arbeitsspeicher; die Ereignisse selbst werden nach der Entscheidung verworfen und erreichen weder das Rohprotokoll noch einen Cache ([03](03-aufzeichnungskern.md#aufnahmebereiche)). **Eine Einstellung, Eingaben außerhalb des Aufnahmebereichs doch aufzuzeichnen, gibt es nicht** – der Aufnahmebereich ist auch eine Datenschutzgrenze. Liegt außerhalb des Aufnahmebereichs ein Fenster einer ausgeschlossenen Anwendung im Vordergrund, gilt die strengere Regel: Lücke `excludedApp`, keine Zähler.

## 9. Zwischenablage

*Stand der Umsetzung (Paket 12):* Der Inhalt wird nur mit eingeschalteter Quelle aufgezeichnet (ab Werk aus, nie in „Streng“, nie im Privatmodus): nur Text, höchstens `valueMaxChars` Zeichen (`content`, `size`, `truncated`), nie aus ausgeschlossenen Programmen und nie, wenn die Quelle die Formate `ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory` oder `CanUploadToCloudClipboard` setzt. Gelesen wird auf einem eigenen Thread mit 300 ms Frist (VE-18). Der Inhalt ist ein Prüfposten der Durchsicht (11.1).

| Nr. | Regel |
|---|---|
| Z1 | **Herkunft immer:** Jede Änderung der Zwischenablage wird mit laufender Nummer, Zeit, Liste der Formate, Quellanwendung und der Größe je Format, soweit ohne Abruf des Inhalts feststellbar, vermerkt. So bleibt nachvollziehbar, *dass* und *woher* kopiert und eingefügt wurde. Nach heutigem Kenntnisstand lässt sich die Größe eines Formats mit dokumentierten Mitteln nur durch seinen Abruf bestimmen; ohne Inhaltsaufzeichnung fehlt sie dann (Z6). Ob es einen Weg ohne Abruf gibt, klärt die Prüfung Z-14 in M0 ([03](03-aufzeichnungskern.md#zwischenablage), VE-14 in [11](11-roadmap-tests-risiken.md)). |
| Z2 | **Inhalt nur auf Wunsch:** Die Einstellung „Inhalt der Zwischenablage aufzeichnen“ ist aus. Eingeschaltet erfasst sie ausschließlich Text, höchstens 2 000 Zeichen je Eintrag (gekürzt wird sichtbar). Bilder und Dateiinhalte werden nie erfasst; Dateilisten nur als Anzahl. |
| Z3 | **Kennzeichnungen sensibler Inhalte werden respektiert:** Enthält die Zwischenablage das Format `ExcludeClipboardContentFromMonitorProcessing`, oder steht `CanIncludeInClipboardHistory` bzw. `CanUploadToCloudClipboard` auf 0, wird der Inhalt nie aufgezeichnet und die Herkunft auf „als vertraulich gekennzeichnet“ reduziert. Passwortmanager setzen diese Kennzeichen. |
| Z4 | Inhalt aus ausgeschlossenen Anwendungen, aus Passwortfeldern, im Privatmodus und in der Stufe „Streng“ wird nie aufgezeichnet. |
| Z5 | **Zuordnung ohne Inhalt, kein Fingerabdruck:** Welcher Eintrag eingefügt wurde, ergibt sich aus der Herkunft allein – der laufenden Nummer, der Folgenummer des Systems, dem Besitzer (Quellfenster), den Formaten und, soweit ohne Abruf feststellbar, der Größe je Format: Maßgeblich ist der letzte Eintrag vor der Einfüge-Eingabe. Die Zuordnung hängt nicht an der Größe; sie gelingt auch, wenn diese fehlt. Inhalte werden dafür weder gelesen noch verglichen. Prüfsummen von Inhalten werden nie gebildet oder gespeichert – kurze Geheimnisse ließen sich daraus zurückrechnen. Nur bei eingeschalteter Inhaltsaufzeichnung darf ein aufgezeichneter Text im Arbeitsspeicher mit früheren Einträgen derselben Sitzung verglichen werden, um Wiederholungen zu kennzeichnen. |
| Z6 | **Ohne Inhaltsaufzeichnung wird der Inhalt nie abgerufen.** Das Tool fordert dann kein Format an, liest keine Bytes und löst kein verzögertes Erzeugen in der Quellanwendung aus. Die Größe je Format (Z1) wird nur festgehalten, soweit sie sich ohne Abruf des Inhalts feststellen lässt; sonst fehlt sie – und das ist nach heutigem Kenntnisstand der Regelfall. Das Verbot des Abrufs geht der Größenangabe vor. Die Zwischenablage wird nie verändert und nie länger als wenige Millisekunden geöffnet. |
| Z7 | Was durch Einfügen in ein normales Feld gelangt, erscheint dort als Feldinhalt nach den Regeln der Schutzstufe – unabhängig von Z2. |

## 10. Schwärzungsmodell

### 10.1 Zwei Ebenen

| Ebene | Wann | Wer | Umkehrbar | Beispiele |
|---|---|---|---|---|
| **Aufnahme-Maske** | vor dem Speichern | das Tool | nein – die Daten existieren nicht | Passwortfeld, Fenster ausgeschlossener Anwendungen, Bereich außerhalb des Aufnahmebereichs, maskierte Tasten |
| **Schwärzung** | nach der Aufnahme | der Autor (im Viewer) | ja, bis zum endgültigen Schwärzen | Kundennummer im Bild, Name in einem Fenstertitel, versehentlich getippter Text |

Die Fenster des Recorders sind weder das eine noch das andere: Sie werden vom System gar nicht erst ins Bild gesetzt ([07](07-aufnahme-bedienung.md#11-unsichtbarkeitsgarantie-aus-nutzersicht)).

### 10.2 Arten von Schwärzungen

Der Autor kann vier Dinge schwärzen:

| Art | Trifft | Anker | Gespeichert als ([04](04-datenformat.md#edits-redactions)) |
|---|---|---|---|
| **Bereich im Bild** | Bildpunkte in einem Rechteck | Bild-Kennungen + Rechteck in Bildpixeln, je betroffenem Bild (`areas`) | `kind: "image"` |
| **Textstelle** | eine Zeichenfolge in einem Ereignisfeld | Ereignisnummer + Feldpfad + Zeichenbereich (`targets`) | `kind: "text"` |
| **Schritt** | alles, was zu einem Schritt gehört: Eingaben, Texte, Bilder | Bereiche von Ereignisnummern (`seqRanges`), dazu der Anker des Schritts | `kind: "range"` mit `origin: "step"` |
| **Zeitraum** | alles zwischen zwei Ereignissen: Eingaben, Texte, Bilder | Bereiche von Ereignisnummern (`seqRanges`) | `kind: "range"` mit `origin: "period"` |

**„Schritt“ wird beim Anlegen aufgelöst.** Wählt der Autor „Schritt schwärzen“, hält das Tool sofort die Ereignisnummern fest, die in diesem Moment zum Schritt gehören – seine Eingabeereignisse, seinen Anlaufweg und die zugehörigen Ereignisse (`seq.input`, `seq.approach`, `seq.related`) – und speichert sie als Bereiche. Die Schwärzung hängt danach nicht mehr am Schritt, sondern an diesen Ereignissen. Werden die Schritte später mit neuen Regeln anders geschnitten, bleibt genau das geschwärzt, was der Autor gesehen und ausgewählt hat; der gespeicherte Anker bestimmt nur noch, wo der Platzhalter erscheint.

Schwärzungen hängen damit **nie an Schrittnummern**. Schritte sind abgeleitet und können sich bei einer Neuberechnung ändern; Ereignisnummern und Bild-Kennungen ändern sich nie.

Abgrenzung: Ein **ausgeblendeter** Schritt (`hidden`) fehlt nur in der Anleitung für Menschen; im KI-Paket und über den MCP-Server bleibt er sichtbar. Ein **geschwärzter** Schritt fehlt überall. Ein **ersetzter** Schritt – der Autor hat den Abschnitt neu aufgenommen ([06](06-ki-export-und-mcp.md#77-aufnahmeabschnitte-nahtstellen-und-ersetzte-schritte)) – fehlt in der Anleitung und im KI-Paket der Detailstufen „Kompakt“ und „Standard“; er bleibt in den Rohdaten, in der Detailstufe „Vollständig“ und über den MCP-Server lesbar. **Ersetzen ist keine Schwärzung:** Wer den Inhalt der alten Schritte entfernen will, schwärzt sie; der Viewer sagt das beim Ersetzen und bietet es an ([08](08-viewer.md#93-abschnitt-neu-aufnehmen-und-ersetzen)). Ein **verworfener Aufnahmeabschnitt** – eine weitere Aufnahme, die der Autor vor ihrem Abschluss verworfen hat ([03](03-aufzeichnungskern.md#weitere-abschnitte)) – erscheint in **keiner** Ausgabe, auch nicht in „Vollständig“ und nicht über den MCP-Server (L12). **Verwerfen ist kein Löschen:** Das Journal wird nie gekürzt; der Abschnitt bleibt als verworfen markiert im Sitzungsordner, bis er endgültig entfernt wird ([10.8](#108-endgültiges-schwärzen)).

### 10.3 Ausdehnung auf alle Vorkommen

Eine Schwärzung an einer einzigen Stelle wäre wertlos, wenn derselbe Inhalt im Nachbarbild oder im Rohprotokoll stehen bliebe. Deshalb:

**Bilder**

| Mechanismus | Wirkung | Art | `rule.kind` in 04 |
|---|---|---|---|
| Gültigkeitsbereich | Die Schwärzung gilt automatisch für alle aufeinanderfolgenden Bilder, in denen der Bereich unverändert ist. Das ergibt sich aus dem Bildspeicher: Solange kein Differenzbild den Bereich berührt, zeigt er dasselbe. | automatisch | `unchanged` |
| Fensterbezug | Die Schwärzung folgt dem Fenster, wenn es verschoben wird (Rechteck relativ zum Fenster). | wählbar | `window` |
| Elementbezug | Die Schwärzung folgt dem Rechteck eines Bedienelements über die Zeit. | wählbar | `element` |
| Bildschirmstelle | Die Schwärzung gilt für dieselbe Stelle in allen Bildern des Monitors – etwa für die Taskleiste. | wählbar | `monitor` |
| Wiederauftreten | Suche nach demselben Bildausschnitt in allen anderen Bildern der Sitzung (pixelgleich); Fundstellen werden vorgeschlagen. | Vorschlag | `recurrence` |
| Einzeln gewählt | Der Autor bestimmt die betroffenen Bilder selbst – auch: nur dieses eine. Bleibt derselbe Inhalt dadurch in einem Nachbarbild sichtbar, weist der Viewer darauf hin. | wählbar | `explicit` |

Jede Ausdehnung wird **beim Anlegen** in die Liste der betroffenen Bilder aufgelöst (`areas`); maßgeblich ist danach allein diese Liste. Ein Bild, das nicht darin steht, bleibt unberührt – auch wenn es denselben Inhalt zeigt. Die Regel wird nur mitgespeichert, damit der Viewer die Ausdehnung zeigen und neu berechnen kann.

**Text**

- „Überall schwärzen“ sucht die Zeichenfolge in getipptem Text, Feldinhalten, Elementnamen, Fenstertiteln, Adressen, Text-Schnappschüssen, Zwischenablage-Inhalten und Kommentaren und legt für **jede Fundstelle** einen Anker an. Getippter Text wird auch in den einzelnen Tastenereignissen geschwärzt – in `key.down` **und** im zugehörigen `key.up`, das Taste und Scancode sonst verriete. „Überall schwärzen“ wirkt auch auf Getipptes, das nur als Tastenfolge und nicht als Feldwert vorliegt, sofort.
- **Der gesuchte Begriff wird nicht gespeichert** – nur die Anker. Sonst stünde das Geheimnis in `edits.json`.
- Zu jeder Text-Fundstelle schlägt das Tool die zugehörigen **Bildbereiche** vor: das Rechteck des Elements, in dem der Text stand, bzw. des Felds, in das er getippt wurde, mit dem passenden Gültigkeitsbereich.
- Text, der nur als Bildpunkte existiert (Remote-Sitzung, Zeichenfläche), findet diese Suche nicht; das sagt der Dialog ausdrücklich.

### 10.4 Die Leseschicht erzwingt

| Nr. | Regel |
|---|---|
| L1 | **Ein Zugang.** Viewer, Anleitungs-Exporte, KI-Paket, MCP-Server, Suche, Vergleich, Qualitätsprüfung und Validator lesen Sitzungen ausschließlich über die Leseschicht ([02](02-architektur.md#datenfluss)). Nur sie öffnet `events.jsonl` und `frames/`. |
| L2 | **Textstellen:** Geschwärzte Bereiche werden durch den festen Platzhalter ersetzt ([10.5](#105-darstellung)). Die Länge wird nicht ausgegeben. |
| L3 | **Bilder:** Ein Bild wird zusammengesetzt, **dann** geschwärzt, und erst danach zugeschnitten, skaliert oder beschriftet. Kein Aufrufer erhält ungeschwärzte Bildpunkte. |
| L4 | **Abgeleitete Kopien:** Jeder Text in `steps.json` trägt seine Herkunft (`prov`: Ereignisnummer, Feld) und wird über dieselben Anker gefiltert. Wurde er unverändert übernommen, trifft die Schwärzung dieselbe Stelle; andernfalls wird der abgeleitete Wert ganz ersetzt. |
| L5 | **Schritte und Zeiträume:** Von einem geschwärzten Schritt erscheint nur der Platzhalter; ein entfernter Zeitraum erscheint als Lücke „vom Autor entfernt“ (`removedByAuthor`). Die betroffenen Ereignisse, alle Ereignisse, die sich auf sie beziehen, und die Bilder des Zeitraums werden nicht ausgeliefert. Ein nur teilweise betroffener Schritt verliert die betroffenen Inhalte nach L4. |
| L6 | **Kein Schalter:** Es gibt keine Betriebsart, keinen Parameter und kein Tool, das die Leseschicht umgeht – auch nicht für Diagnose. Ausnahme ist allein der Bearbeitungsmodus des Viewers, der dem Autor die Stelle zeigen muss, um die Schwärzung zu ändern; er kopiert und exportiert nichts. |
| L7 | **Ungeprüftes:** Auffällige Eingaben, die der Autor noch nicht durchgesehen hat, gibt die Leseschicht an Wege **ohne Oberfläche** (MCP-Server, Export per Kommandozeile) nur als Zähler `{unreviewed:N}` heraus ([11.1](#111-durchsicht-aller-eingaben)). Auch dafür gibt es keinen Schalter. |
| L8 | **Interne und ausgeblendete Kommentare:** Kommentare, die der Autor als intern gekennzeichnet oder ausgeblendet hat, erscheinen in keiner Ausgabe – weder in Anleitungen noch im KI-Paket noch über den MCP-Server, auch nicht in Rohereignissen und Suchtreffern ([04](04-datenformat.md#edits-json), [06](06-ki-export-und-mcp.md#74-kommentare-des-autors)). Das gilt ebenso für Kommentare, die während der Aufnahme entstanden und nachträglich so gekennzeichnet wurden. Sie sind Arbeitsnotizen des Autors und bleiben im Viewer. |
| L9 | **Verallgemeinerungen:** Die Leseschicht wendet die Regeln aus `edits.substitutions[]` auf jeden Text an, den sie sonst im Klartext ausgäbe, und auf die Bildflächen, die die Regel nennt ([10.9](#109-verallgemeinerung-von-werten)). Den ursprünglichen Wert erhält kein Aufrufer außer der Anzeige „Originalwerte zeigen“ im Viewer; sie kopiert, exportiert und durchsucht nichts. Die Regel selbst – sie enthält den Wert – gibt die Leseschicht nie heraus. |
| L10 | **Angaben des Autors zur Aufnahme:** Ziel-Angaben und Bezeichnungen der Marker sind Autorentext wie Kommentare. Die Leseschicht liefert von den Ziel-Angaben die wirksame Fassung (Overlay vor Manifest) und wendet Schwärzung, Zurückhaltung (L7) und Verallgemeinerung (L9) darauf an. Eine im Viewer geänderte oder gelöschte Angabe erscheint in keiner Ausgabe mehr in der ursprünglichen Fassung – auch nicht im `session.start`-Ereignis der Rohereignisse. |
| L11 | **Mehrere Sitzungen:** Die Suche über alle Sitzungen und der Vergleich zweier Sitzungen lesen jede Sitzung getrennt über die Leseschicht – mit deren eigenen Schwärzungen, deren Stand der Durchsicht und deren Verallgemeinerungen. Was zurückgehalten ist, wird weder gefunden noch verglichen und nie als gleich oder verschieden gemeldet; kein Ergebnis hängt von einem verborgenen Inhalt ab. Ein Vergleichsergebnis wird in keiner Sitzung abgelegt. Das gilt für alle drei Wege des Vergleichs: die Ansicht im Viewer ([08](08-viewer.md#59-zwei-aufnahmen-vergleichen)), den Befehl `compare` ([02](02-architektur.md#befehle-nachgelagert)) und das Tool `compare_sessions` ([06](06-ki-export-und-mcp.md#199-compare_sessions)). |
| L12 | **Verworfene Aufnahmeabschnitte:** Einen als verworfen markierten weiteren Aufnahmeabschnitt hält die Leseschicht aus allen Ausgaben heraus: Kopien, Anleitungen, KI-Paket in jeder Detailstufe, MCP-Server, Suche, Vergleich, Qualitätsprüfung. Auch abgeleitete Daten enthalten ihn nicht – kein Vorschaubild, kein Eintrag im Suchindex, keine Zwischendatei, keine Zählung. Nennen darf eine Ausgabe allein seine Zeitspanne, wo ein späterer Abschnitt an ihn anschließt ([06](06-ki-export-und-mcp.md#77-aufnahmeabschnitte-nahtstellen-und-ersetzte-schritte)). |
| L13 | **Tastenereignisse verraten nichts.** Das `key.up` einer ungeprüften oder geschwärzten Eingabe gibt Taste und Scancode so wenig preis wie das `key.down`: In Ausgaben stehen `Redacted`, der Code 0 und die Kennzeichnung (`unreviewed` bzw. `redacted`). Geschwärzte Tippschritte verlieren ihren Text samt den Feldwerten, die ihn wiederholen. |

| L14 | **Formatierung hebelt keinen Schutz aus.** Formatierte Autorentexte ([09 §11.1](09-anleitungs-export.md#111-formatierter-text-des-autors)) werden vor dem Deuten der Auszeichnung geschwärzt und verallgemeinert – eingesetzte Platzhalter sind dabei unteilbar und nie Auszeichnung – und nach dem Deuten noch einmal am sichtbaren Text geprüft. Setzt erst das Deuten einen geschützten Wert zusammen (etwa `mm**uster**`), wird er bereinigt; geht das nicht mit der Formatierung, entfällt die Formatierung, nie der Schutz. Linkziele mit geschütztem Wert werden zu Text. Variablen werden vor dem Schwärzen eingesetzt, ihre Werte also wie jeder andere Text geschützt; ein Querverweis auf ein nicht gezeigtes Ziel ergibt „??“ und nie dessen Titel (Paket 23). Platzhalternamen einer Verallgemeinerung werden geprüft; ein unzulässiger Name ergibt `[geschwärzt]` und wird nie als Ersetzungsmuster gedeutet. |

### 10.5 Darstellung

Die Platzhalter sind fest und in allen Ausgaben gleich – Viewer-Export, Kopien, Anleitungen, KI-Paket, MCP-Server ([04](04-datenformat.md#edits-redactions)):

| Geschwärzt | Deutsch | Englisch |
|---|---|---|
| Textstelle | `[geschwärzt]` | `[redacted]` |
| Ganzer Schritt | „Schritt geschwärzt“ | “Step redacted” |
| Zeitraum | Lücke „vom Autor entfernt“ | gap “removed by the author” |
| Bereich im Bild | schwarz gefüllte Fläche | schwarz gefüllte Fläche |

- **Keine Länge, keine Anfangsbuchstaben, kein frei wählbarer Ersatztext.** Zwei verschieden lange geschwärzte Texte ergeben dieselbe Ausgabe.
- **Ein entfernter Zeitraum wird nie verschwiegen:** Er erscheint in **jeder** Ausgabe – auch in Anleitungen für Menschen – als neutraler Hinweis „vom Autor entfernt“. Der Hinweis nennt Zeitpunkt und Dauer, nie den Inhalt oder einen Grund.
- Ob ein geschwärzter **Schritt** in einer Anleitung als Platzhalter erscheint oder entfällt, legt [09](09-anleitungs-export.md#41-umfang) fest; im KI-Paket und über den MCP-Server erscheint er immer als Platzhalter, damit die Zählung der Schritte stimmt und die KI weiß, dass dort etwas war ([06](06-ki-export-und-mcp.md#52-ablauf)).
- **Sprache der Platzhalter:** In Texten für Leser (Anleitungen, `trace.md`, PDF) stehen sie in der Sprache der Ausgabe. In Daten (JSON-Dateien des KI-Pakets, Antworten des MCP-Servers, Rohereignisse) steht für eine geschwärzte Textstelle immer die sprachunabhängige Markierung `[redacted]` – dieselbe, die das endgültige Schwärzen ins Rohprotokoll schreibt ([06](06-ki-export-und-mcp.md#53-schreibweise)).
- **Bild:** Die schwarz gefüllte Fläche ist der Standard und gilt immer im KI-Paket und im MCP-Server. Dieselbe Farbe verwenden die Aufnahme-Masken (Passwortfeld, ausgeschlossene Anwendung, außerhalb des Aufnahmebereichs); Ausgaben für KI weisen deshalb jede solche Fläche mit ihrem Grund aus, damit sie nicht mit Bildschirminhalt verwechselt wird. Für Anleitungen bietet der Viewer zusätzlich die Optik „Verpixeln“ und „Unschärfe“ an. Sie werden **nicht aus den Originalpixeln berechnet**, sondern aus der Durchschnittsfarbe des Bereichs und einem Zufallsmuster – echte Verpixelung und Unschärfe lassen sich bei Text teilweise zurückrechnen.

### 10.6 Abgeleitete Daten und frühere Exporte

- Vorschaubilder und Suchindex in `cache/` entstehen nur aus der Sicht der Leseschicht. Bei jeder Änderung an Schwärzungen, Verallgemeinerungen oder am Stand der Durchsicht werden sie vollständig verworfen – **bevor** die Änderung gespeichert ist – und neu erzeugt.
- **Suchindex** (`cache/search/`, [04](04-datenformat.md#suchindex)): Jede Sitzung hat ihren eigenen Index; die Volltextsuche über alle Sitzungen – in der Sitzungsbibliothek und über den MCP-Server – fragt nur diese Indizes. Für ihn gilt:
  - In den Index kommt allein, was die Leseschicht in diesem Moment auch einem Weg ohne Oberfläche ausgäbe. **Nie** darin stehen: geschwärzte Inhalte, maskierte Eingaben, ungeprüfte auffällige Eingaben, interne und ausgeblendete Kommentare und der ursprüngliche Wert einer Verallgemeinerung – dort steht der Platzhalter. Auch die Platzhalter der Leseschicht selbst sind keine Einträge.
  - Das gilt auch für die Suche im Viewer: Ein auffälliger Text, den der Autor noch nicht durchgesehen hat, ist über die Sitzungsbibliothek nicht auffindbar, bis er freigegeben ist. So gibt es einen Index mit einem Schutzniveau, nicht zwei.
  - Verworfen wird er bei jeder Änderung, die die Sicht der Leseschicht bestimmt: Schwärzungen, Verallgemeinerungen, Stand der Durchsicht, Art und Sichtbarkeit von Kommentaren, Ziel-Angaben, Ersetzungen. Ein Index, dessen Stand nicht zur Sitzung passt, wird nicht benutzt.
  - Er wird aus der Leseschicht **im Modus ohne Oberfläche** gebaut – nie aus der Sicht des Editors, die dem Autor mehr zeigt (etwa den Originaltext interner Kommentare oder noch nicht durchgesehene Eingaben im Klartext). Der Katalog der Sitzungsbibliothek (Titel, Durchsicht-Stand, Vorschaubild) liest ebenfalls nur über die Leseschicht.
  - Er entsteht nach dem Stopp im Hintergrund, ist jederzeit löschbar, wird nie in eine `.steprec`-Datei gepackt und nach endgültigem Schwärzen mit dem ganzen Ordner geleert.
  - Einen Index über mehrere Sitzungen gibt es nicht. Treffer werden nirgends gespeichert; Suchbegriffe stehen in keinem Protokoll. Eine Sitzung, die noch nicht durchsucht werden konnte, weist die Suche als solche aus – nie als trefferlos.
- **Bearbeitungsstände** (`history/`, [04](04-datenformat.md)): Automatische Sicherungen von `edits.json` enthalten die Verallgemeinerungsregeln samt ihren Werten und die internen Kommentare. Sie sind so vertraulich wie `edits.json`, werden nie in eine `.steprec`-Datei oder eine bereinigte Kopie übernommen und beim endgültigen Schwärzen **alle gelöscht**. Das Wiederherstellen eines Stands stellt weder Schwärzungen noch Verallgemeinerungen noch den Stand der Durchsicht auf einen früheren Zustand zurück, solange der Autor das nicht ausdrücklich wählt.
- Wiederhergestellte Vollbilder und die Sprungtabelle des Rohprotokolls in `cache/` sind den Rohdaten gleichgestellt: Sie werden nie direkt ausgegeben, sondern nur über die Leseschicht. Der Ordner ist deshalb so vertraulich wie die Sitzung selbst, wird nie in eine `.steprec`-Datei gepackt und nach endgültigem Schwärzen geleert ([04](04-datenformat.md#cache)).
- Liegen in `export/` der Sitzung Ausgaben, die vor der Schwärzung entstanden sind, listet der Viewer sie auf und bietet an, sie zu löschen. Exporte an anderen Orten kann das Tool nicht kennen; darauf weist der Dialog hin.
- Texte, die bereits in die Zwischenablage kopiert wurden, sind nicht rückholbar.

### 10.7 Was eine Schwärzung nicht leistet

Solange nicht endgültig geschwärzt wurde, stehen die Originaldaten im Sitzungsordner. Wer den **Ordner** weitergibt, gibt sie mit. Die Schwärzung schützt alle *Ausgaben* des Tools, nicht die Rohdateien. Dasselbe gilt für alles, was die Leseschicht nur zurückhält: verworfene Aufnahmeabschnitte, ersetzte Schritte und die Werte, die Verallgemeinerungsregeln nennen.

### 10.8 Endgültiges Schwärzen

Zwei Wege, beide nur auf ausdrücklichen Befehl:

| Weg | Wofür | Ablauf |
|---|---|---|
| **Bereinigte Kopie** (empfohlen; umgesetzt) | Weitergabe einer Sitzung als Ordner oder `.steprec` | Eine neue Sitzung mit eigener Kennung entsteht, in die nur die geschwärzte Sicht geschrieben wird: Ereignisse laufen durch die Leseschicht, Bilder werden mit allen Schwärzungen als Schlüsselbilder neu gespeichert, das Overlay verliert Regelwerte und interne Kommentare. Das Original bleibt unverändert beim Autor. Das Manifest der Kopie vermerkt `sanitizedCopy`. |
| **An Ort und Stelle** („Endgültig schwärzen“; umgesetzt) | Das Geheimnis soll auch beim Autor verschwinden | Die Sitzung wird umgeschrieben. Das ist die einzige Operation, die Rohdaten verändert; das Manifest vermerkt `redactedDestructive`. |

Was dabei mit den Daten geschieht, legt [04](04-datenformat.md#edits-redactions) fest; in beiden Wegen gilt:

1. **Bilder:** Geschwärzt wird in den gespeicherten Bilddaten selbst. Jedes Schlüssel- und Differenzbild, das zur betroffenen Fläche Bildpunkte beiträgt, wird mit schwarz gefüllter Fläche neu geschrieben – damit sind auch alle Bilder geschwärzt, die darauf aufbauen. Die alten Bilddaten werden gelöscht.
2. **Textstellen:** Im Rohprotokoll steht anstelle des Inhalts die feste Markierung; betroffene Tastenereignisse verlieren Zeichen und Tastencodes.
3. **Schritte und Zeiträume:** Jede betroffene Zeile wird auf einen Rest gekürzt, der nur noch Nummer, Zeit und Art nennt. Zeilen werden nie entfernt, damit Nummern und Verweise gültig bleiben; die Schritterkennung macht daraus eine Lücke „vom Autor entfernt“.
4. `steps.json` wird neu abgeleitet, `cache/` geleert, die Schwärzungen in `edits.json` als angewendet markiert (`applied`), Prüfsummen im Manifest erneuert. Der Vermerk im Manifest hält fest, dass, wann und wie viel geschwärzt wurde – nie, was.
5. **Gegenprobe:** Das Tool durchsucht die neue Sitzung nach den geschwärzten Zeichenfolgen (aus dem Arbeitsspeicher) und vergleicht die geschwärzten Bildbereiche; erst danach werden die alten Dateien gelöscht.
6. **Bilder geschwärzter Zeiträume.** „Endgültig schwärzen“ und „Letzte Eingabe schwärzen“ entfernen auch die Bilder der geschwärzten Zeiträume aus den Rohdaten, nicht nur die Zeilen des Protokolls: Bildereignisse im Zeitraum oder mit Bezug darauf verlieren ihre Bilddaten; spätere Bilder, die als Änderung darauf aufbauen, werden vorher als Schlüsselbild neu gespeichert; nicht mehr verwendete Bilddateien werden gelöscht.
7. **Bearbeitungsstände.** Alle Sicherungen unter `history/` werden gelöscht ([10.6](#106-abgeleitete-daten-und-frühere-exporte)); die bereinigte Kopie enthält keine.
8. **Gegenprobe der bereinigten Kopie.** Sie durchsucht jede Textdatei der Kopie – auch die Schritte, die aus der Kopie neu gebaut werden – nach allen geschützten Werten. Ein Treffer löscht die Kopie, und es entsteht keine; Tipp-Schritte mit verallgemeinerten Werten werden dafür entschärft. Eine Kopie entsteht außerdem nie, solange ungeprüfte auffällige Eingaben vorliegen.
9. **Verworfene Aufnahmeabschnitte** gehören nicht zur Sicht der Leseschicht: Die bereinigte Kopie enthält sie nicht. Beim Schwärzen an Ort und Stelle werden ihre Zeilen wie die eines entfernten Zeitraums auf Nummer, Zeit und Art gekürzt und ihre Bilddaten gelöscht; die Gegenprobe schließt sie ein.

Auch die bereinigte Kopie unterliegt der Sperre im Viewer aus [11.1](#111-durchsicht-aller-eingaben): Solange auffällige Eingaben ungeprüft sind, wird sie nicht erzeugt.

Grenzen, die der Dialog nennt: Auf SSDs, in Sicherungen, in Schattenkopien und im Versionsverlauf von Sync-Diensten können frühere Fassungen fortbestehen. Deshalb gilt die Empfehlung, **vor** der ersten Synchronisierung oder Weitergabe zu schwärzen ([Abschnitt 13](#13-speicherorte-sync-ordner-und-spuren-außerhalb-der-sitzung)).

### 10.9 Verallgemeinerung von Werten

Eine **Verallgemeinerung** ersetzt einen bestimmten Wert – den eigenen Benutzernamen, einen Rechner- oder Servernamen, einen Pfadteil – in allen Ausgaben durch einen Platzhalter wie `{Benutzername}` (`edits.substitutions[]`, [04](04-datenformat.md#edits-substitutions); Bedienung in [08](08-viewer.md#94-werte-verallgemeinern); Darstellung für KI in [06](06-ki-export-und-mcp.md#78-verallgemeinerte-werte)). Sie dient zuerst der Wiederverwendbarkeit einer Anleitung. **Zugleich ist sie eine Schutzmaßnahme für Ausgaben:** Was verallgemeinert ist, verlässt den Rechner nicht mehr im Original.

**Einordnung**

| | Aufnahme-Maske | Schwärzung | Verallgemeinerung |
|---|---|---|---|
| Wann | vor dem Speichern | nach der Aufnahme | nach der Aufnahme |
| Wirkt auf | eine Stelle | ausgewählte Stellen (Anker) | **jedes** Vorkommen eines Werts – auch in später angehängten Abschnitten |
| Ausgabe | `{masked:N}`, schwarze Fläche | `[geschwärzt]`, schwarze Fläche | `{Name}`, gefüllte Fläche mit dem Platzhalter als Aufschrift |
| Der Wert steht in `edits.json` | – | **nein** – nur Anker | **ja** – die Regel nennt ihn (`match`) |
| Dauerhaft entfernt | nie gespeichert | durch endgültiges Schwärzen | durch endgültiges Anwenden |
| Gedacht für | Passwörter, ausgeschlossene Anwendungen | Geheimnisse und alles, was verschwinden soll | Namen und Kennungen, die sich wiederholen |

**Regeln**

| Nr. | Regel |
|---|---|
| V1 | **Über die Leseschicht, in jeder Ausgabe.** Kopien aus dem Viewer, Anleitungen, KI-Paket, MCP-Server, Suche, Vergleich, Zwischendateien und Caches erhalten nur die verallgemeinerte Sicht (L9). Einen Weg, der sie umgeht, gibt es nicht. „Originalwerte zeigen“ im Viewer ist eine reine Anzeige für den Autor. |
| V2 | **Fester Platzhalter.** Die Ausgabe ist immer `{Name}`; ein frei wählbarer Ersatztext ist nicht möglich. So kann eine Regel – auch eine aus einer fremden Sitzung – Text nicht unbemerkt umschreiben. Zulässige Namen legt 04 fest. Trifft die Leseschicht auf einen unzulässigen Namen, gibt sie an der Stelle `[geschwärzt]` aus – nie den Wert. |
| V3 | **Nach Schwärzung und Zurückhaltung.** Verallgemeinert wird nur, was sonst im Klartext ausgegeben würde. Eine Verallgemeinerung hebt weder eine Schwärzung noch die Zurückhaltung einer ungeprüften Eingabe auf, und sie zählt nicht als Durchsicht. |
| V4 | **Auch die Tasten.** Wurde der Wert getippt, verschwinden alle Tasten vom ersten bis zum letzten Zeichen einschließlich der Korrekturen dazwischen – in Schritten wie in Rohereignissen. Der Wert lässt sich nicht aus der Tastenfolge zusammensetzen. |
| V5 | **Bilder nur mit bekannter Textlage.** In Bildern wird überdeckt, wo die Lage des Textes bekannt ist: das Rechteck eines Elements, dessen Name oder Wert den Wert enthielt, die Titelzeile eines Fensters, das Feld, in das er getippt wurde, oder eine vom Autor markierte Stelle – immer die ganze Fläche. Eine Texterkennung gibt es nicht. Wo keine Lage bekannt ist, **sagt das Programm das**: beim Anlegen der Regel, in der Qualitätsprüfung vor dem Export (Regel Q15, [09](09-anleitungs-export.md#121-qualitätsprüfung-regelkatalog)) und in den Ausgaben für KI. Es behauptet nie, ein Bild sei bereinigt, wenn es das nicht wissen kann. |
| V6 | **Dauerhaft erst durch endgültiges Anwenden.** Bis dahin stehen die Originalwerte in den Rohdaten, und die Regel nennt den Wert. Wer den Sitzungsordner weitergibt, gibt beides mit ([10.7](#107-was-eine-schwärzung-nicht-leistet)). Dauerhaft entfernt wird auf denselben zwei Wegen wie beim Schwärzen ([10.8](#108-endgültiges-schwärzen)) – bereinigte Kopie oder an Ort und Stelle. Dabei tritt im Rohprotokoll der Platzhalter an die Stelle des Werts, die Bildflächen werden in den Bilddaten gefüllt, **und die Regel verliert ihren Wert**; die Gegenprobe sucht anschließend nach jedem verallgemeinerten Wert. |
| V7 | **Kein Ersatz für die Schwärzung von Geheimnissen.** Für Passwörter, Schlüssel und Kontodaten ist die Verallgemeinerung nicht gedacht: Die Regel hielte das Geheimnis im Klartext in `edits.json`. Sieht der Wert einer neuen Regel nach einem treffsicheren Muster der Musterprüfung aus ([11.2](#112-musterprüfung)), rät der Dialog zur Schwärzung. |
| V8 | **Muster ohne Risiko.** Reguläre Ausdrücke einer Regel werden mit begrenzter Länge und einer Zeitgrenze von 200 ms ausgewertet (04), wenn möglich ohne Zurückverfolgung. Wörtliche Werte (etwa ein Benutzername) laufen als ganzes Wort über die normale Engine, weil die Engine ohne Zurückverfolgung die dafür nötigen Vor- und Rückschau-Konstrukte nicht kann; eigene Muster fallen bei nicht unterstützten Konstrukten auf sie zurück. Eine präparierte Sitzung kann damit weder die Leseschicht aufhalten noch – wegen V2 – Inhalte verfälschen. |

**Vorschläge.** Der Viewer schlägt Regeln für vier Werte vor, die fast jede Aufzeichnung verraten:

| Vorschlag (`suggestion`) | Wert, zur Laufzeit ermittelt |
|---|---|
| `userName` | Anmeldename des angemeldeten Benutzers |
| `machineName` | Name des Rechners |
| `domain` | Name der Domäne, sofern der Rechner einer angehört |
| `profilePath` | Pfad des Benutzerprofils |

- **Nur zur Laufzeit ermittelt, nie gespeichert.** Die Werte stammen aus der laufenden Windows-Sitzung des Rechners, auf dem der Viewer gerade läuft. Sie werden nur im Arbeitsspeicher mit den Texten der Sitzung verglichen und stehen danach in keiner Datei – nicht in Sitzung, Manifest, Einstellungen, Profilen, Caches oder Protokollen. Der Grundsatz „standardmäßig kein Rechner- oder Benutzername“ ([Abschnitt 3](#3-dateninventar)) bleibt unberührt.
- **Annehmen nur bei Treffer.** Der Dialog zeigt die vier Werte des laufenden Rechners mit der Zahl ihrer Fundstellen; ein Vorschlag ohne Fundstelle ist ausgegraut und lässt sich nicht annehmen ([08](08-viewer.md#94-werte-verallgemeinern)). So enthält eine Regel nie einen Wert, der nicht ohnehin in der Sitzung steht. Die Anzeige selbst hinterlässt nichts.
- **Gespeichert wird erst die angenommene Regel** – und sie enthält dann einen Wert, der ohnehin in der Sitzung steht. Lehnt der Autor einen Vorschlag ab, kann die Qualitätsprüfung das als „ignoriert“ vermerken; der Vermerk nennt die Art des Vorschlags (`userName`), nie den Wert ([04](04-datenformat.md#edits-quality)).
- **Anderer Rechner, andere Werte.** Wird die Sitzung auf einem anderen Rechner geöffnet als aufgenommen, passen die Laufzeitwerte nicht zur Aufzeichnung; dann hat kein Vorschlag eine Fundstelle, und der Autor legt eigene Regeln an. Der Dialog sagt das, statt Sicherheit vorzutäuschen.
- Dieselben vier Werte prüft die Qualitätsprüfung vor dem Export (Regel Q14 „Ein persönlicher Wert ist nicht verallgemeinert“, [09](09-anleitungs-export.md#121-qualitätsprüfung-regelkatalog)) – nach denselben Regeln: zur Laufzeit, ohne zu speichern. Die Musterprüfung meldet weiterhin Benutzernamen in Pfaden ([11.2](#112-musterprüfung)); ihr Vorschlag ist jetzt die Verallgemeinerung.

**Grenzen**, die Dialog und Handbuch nennen: Erreicht wird nur die Schreibweise, die die Regel beschreibt – nicht der Kurzname eines Ordners (`MMUSTE~1`), nicht der Anzeigename einer Person, nicht eine E-Mail-Adresse mit anderem Aufbau. Einen Wert, der über mehrere Tippläufe verteilt eingegeben wurde, erkennt die Regel im Getippten nicht (im Feldinhalt danach schon). Und was nur als Bildpunkte existiert, bleibt stehen (V5).

## 11. Prüfung vor dem Export

### 11.1 Durchsicht aller Eingaben

*Stand der Umsetzung (Paket 12 aus [12](12-entwickler-fahrplan.md)):* Neben auffälligen Tippläufen kennt die Durchsicht **Prüfposten** (`SessionView.ReviewItems`): jeder mitgelesene Inhalt (Freitext der Ereignisanzeige, Protokollzeilen, Inhalt der Zwischenablage) ist immer prüfpflichtig; Kommentare, Hinweise, Marker-Bezeichnungen und Ziel-Angaben werden es, sobald die Musterprüfung anschlägt. Ungeprüfte Posten erscheinen in Ausgaben ohne Oberfläche als `{unreviewed:N}` (Inhalt) bzw. ersetzen nur die auffällige Stelle; Exporte aus dem Editor, Fehlerbericht und bereinigte Kopie warten auf die Durchsicht. „Schwärzen“ legt für Inhalte eine Zeitraum-Schwärzung auf das Ereignis, setzt einen Hinweis auf „Intern“ bzw. leert eine Ziel-Angabe. Die Entscheidungen stehen in `edits.review.inputs[]` mit `key`.

Der Viewer bietet eine Liste **aller Texte, die über die Tastatur oder die Zwischenablage in die Sitzung gelangt sind**: Tippläufe, Feldinhalte, aufgezeichnete Zwischenablage-Inhalte, dazu die Texte des Autors: seine Kommentare, seine Ziel-Angaben (Ziel der Aufnahme, erwartetes Ergebnis, Bezug) und die Bezeichnungen seiner Marker. Die Bedienung beschreibt [08](08-viewer.md#13-durchsicht-der-eingaben); hier steht, was sie leisten muss.

**Auffällige Eingaben** sind

1. Eingaben aus **Klartext-Fenstern** – undurchsichtigen Oberflächen, in denen die Stufe „Schutznetz“ im Klartext aufzeichnet (Terminal, Remote-Desktop, VM); in `steps.json` am Kennzeichen `flags.plaintext` zu erkennen –, und
2. Eingaben mit einem **Treffer der Musterprüfung** ([11.2](#112-musterprüfung)), gleich in welchem Fenster.

Ob eine Eingabe auffällig ist, berechnet die Leseschicht bei jedem Lesen aus diesen beiden Quellen. Im Rohprotokoll steht dafür kein eigenes Kennzeichen, weil sich die Muster mit dem Programm weiterentwickeln.

Regeln:

- Auffällige Eingaben stehen in der Liste oben und verlangen je Eintrag eine **Einzelentscheidung**: *in Ordnung*, *schwärzen* oder *überall schwärzen*. Unauffällige lassen sich gesammelt bestätigen.
- **Terminal, Remote-Desktop und VM:** Getipptes aus diesen Fenstern ist auffällig (Grund „Eingabe in einem Fenster ohne erkennbare Passwortfelder“) und braucht eine Entscheidung. Da dabei viele Befehle zusammenkommen, bietet die Liste die Schaltfläche **„Übrige Klartext-Eingaben (N): in Ordnung“**: Sie setzt alle noch offenen Eingaben dieser Fenster auf „In Ordnung“. Auffällige Muster (Schlüssel, IBAN, Kennwörter in Befehlen) bleiben davon ausgenommen und verlangen weiter die Einzelentscheidung. Die Grundlage ist das Kennzeichen `flags.plaintext`, das die Schritterkennung aus der Oberflächenart des Fensters setzt; ältere Aufnahmen leiten sie aus Prozess und Fensterklasse ab.
- Der Stand wird **je Eingabe** gespeichert und bleibt über das Schließen hinaus erhalten (`review` in `edits.json`, [04](04-datenformat.md#edits-review)): je Einzelentscheidung ein Eintrag in `review.inputs` mit der Art der Eingabe, ihren Ereignisnummern und dem Ergebnis `kept` (in Ordnung) oder `redacted` (geschwärzt); die gesammelte Bestätigung der unauffälligen steht in `review.confirmed` mit der letzten erfassten Ereignisnummer und dem Stand der Muster. Verankert ist alles an Ereignisnummern, nicht an Schritten.
- Zu einem auffälligen Tipplauf gehören die Feldinhalte, die ihn wiedergeben (Inhalt des Zielfelds vor und nach der Eingabe). Sie werden mit ihm zurückgehalten und mit ihm freigegeben.
- **Angaben des Autors zur Aufnahme** gehören zur Durchsicht wie Kommentare: die Ziel-Angaben – je Feld einzeln, in der wirksamen Fassung – und die Bezeichnungen der Marker. Sie sind Freitext und enthalten oft genau das, was nicht weitergegeben werden soll: Kundennamen, Ticketnummern, Fehlertexte mit Kontodaten. Auffällig sind sie bei einem Treffer der Musterprüfung; dann gelten Sperre und Zähler wie bei jeder auffälligen Eingabe, und N ist die Zahl der Zeichen. Ändert der Autor eine Angabe nach der Durchsicht, gilt sie wieder als nicht durchgesehen ([04](04-datenformat.md#edits-review)). Dasselbe gilt für die Vorbelegungen des Support-Pakets – Bezug und Hinweis für den Empfänger ([11.3](#113-support-paket)).
- **Schwärzen** lassen sich Ziel-Angaben und Marker-Bezeichnungen als Textstelle wie jeder andere Text; Fundstelle ist das `session.start`- bzw. das `marker`-Ereignis. „Überall schwärzen“ und Verallgemeinerungen erfassen sie mit. Eine im Viewer geänderte Ziel-Angabe ersetzt die ursprüngliche in allen Ausgaben (L10); aus den Rohdaten verschwindet die ursprüngliche Fassung erst mit der bereinigten Kopie oder dem endgültigen Schwärzen – beide schreiben nur die wirksame.
- **Nachträglich Ungeprüftes:** Kommen Eingaben hinzu (Aufnahme fortgesetzt) oder erkennt ein neuerer Stand der Musterprüfung weitere Treffer, sind genau diese Eingaben ungeprüft – nicht die ganze Sitzung.
- **Sperre im Viewer:** Solange eine auffällige Eingabe ungeprüft ist, die in der Ausgabe erscheinen würde, erzeugt der Viewer weder Anleitung noch KI-Paket noch bereinigte Kopie; der Dialog führt in die Durchsicht ([09](09-anleitungs-export.md#12-export-vorprüfung), [08](08-viewer.md#13-durchsicht-der-eingaben)). Die Sperre gilt **nur für Exporte aus dem Viewer** – dort sitzt der Autor und kann entscheiden. Wege ohne Oberfläche werden nicht gesperrt; sie liefern stattdessen Zähler (übernächster Punkt).
- **Kopieren:** Enthält eine Text-Kopie eine ungeprüfte auffällige Eingabe, fragt der Viewer einmal je Sitzung nach und bietet die Durchsicht an. Bild-Kopien fragen nicht – der Nutzer sieht, was er kopiert ([08](08-viewer.md#109-datenschutz-beim-kopieren)).
- **Wege ohne Oberfläche** (MCP-Server, Export per Kommandozeile) können nichts anzeigen und niemanden fragen. Sie erhalten ungeprüfte auffällige Eingaben deshalb nur als Zähler `{unreviewed:N}` (Regel L7), bis der Autor sie im Viewer durchgesehen hat. N ist bei Tippläufen die Zahl der Tastenanschläge, bei Feldinhalten, Zwischenablage-Inhalten, Kommentaren, Ziel-Angaben und Marker-Bezeichnungen die Zahl der Zeichen. Die Eingabe wird immer als Ganzes ersetzt – in Schritten, Rohereignissen, Suche und allen daraus erzeugten Texten ([06](06-ki-export-und-mcp.md#201-leseschicht)). Der Export per Kommandozeile gelingt also auch mit ungeprüften Eingaben und meldet ihre Zahl; nur mit der Option `--strict` bricht er stattdessen ab ([09](09-anleitungs-export.md#14-kommandozeilen-export)).
- **Die Durchsicht lässt sich nicht abschalten.** Es gibt keine Einstellung, keine Kommandozeilen-Option und keinen Tool-Parameter, der ungeprüfte auffällige Eingaben freigibt – auch nicht für den MCP-Server. Freigegeben wird allein durch die Entscheidung des Autors im Viewer. Wer einem Agenten Zugriff auf solche Inhalte geben will, sieht sie vorher durch.
- Maskierte Eingaben brauchen keine Entscheidung; ihr Inhalt existiert nicht.
- Eingaben in einem **verworfenen Aufnahmeabschnitt** brauchen ebenfalls keine Entscheidung: Sie erscheinen in keiner Ausgabe (L12). Eingaben in **ersetzten Schritten** gehören dagegen weiter zur Durchsicht – sie stehen in der ausführlichsten Detailstufe des KI-Pakets und über den MCP-Server.
- In der Stufe „Streng“ gibt es keine Klartext-Fenster; auffällig sind dort nur Mustertreffer.

### 11.2 Musterprüfung

Vor jeder Weitergabe durchsucht das Tool alle Texte der Sitzung (nach Schwärzung) nach typischen sensiblen Mustern – lokal, ohne Netzwerk.

| Muster | Erkennung | Anmerkung |
|---|---|---|
| Kennwörter in Befehlen und Adressen | `://benutzer:kennwort@`, `Password=…;`, `/user:… <wort>`, `-p<wort>`, `--password …` | häufigste Lücke im Terminal |
| Zugangsschlüssel und Token | bekannte Präfixe (`sk-`, `ghp_`, `github_pat_`, `AKIA`, `xox…-`), dreiteilige Token, die mit `eyJ` beginnen, Blöcke `-----BEGIN … PRIVATE KEY-----` | Liste erweiterbar |
| Lange Zufallsfolgen | mindestens 20 Zeichen mit hoher Zeichenvielfalt | viele Fehlalarme; als „möglich“ markiert |
| Zahlungs- und Kontodaten | IBAN (Prüfziffer), Kartennummern (Luhn-Prüfung) | |
| E-Mail-Adressen, Telefonnummern | Formate | standardmäßig nur gezählt, nicht einzeln gemeldet |
| Benutzername im Pfad | `C:\Users\<name>\` | Vorschlag: verallgemeinern ([10.9](#109-verallgemeinerung-von-werten)) |

- Durchsucht werden getippter Text, Feldinhalte, Text-Schnappschüsse, Elementnamen, Fenstertitel, Adressen, Zwischenablage-Inhalte, Kommentare, Ziel-Angaben und Marker-Bezeichnungen.
- Treffer erscheinen mit Umgebung und den Aktionen *schwärzen*, *überall schwärzen*, *ist in Ordnung*. Zu Treffern mit bekanntem Elementrechteck wird der Bildbereich gleich mit vorgeschlagen.
- Die Prüfung entscheidet nie selbst. Ein Treffer **in einer Eingabe** macht diese zur auffälligen Eingabe und verlangt damit eine Einzelentscheidung ([11.1](#111-durchsicht-aller-eingaben)). Treffer in Bildschirmtexten (Elementnamen, Fenstertitel, Text-Schnappschüsse) sind Hinweise und sperren nichts.
- Als auffällig zählen nur die treffsicheren Muster: Kennwörter in Befehlen und Adressen, Zugangsschlüssel und Token, Zahlungs- und Kontodaten. Lange Zufallsfolgen, E-Mail-Adressen, Telefonnummern und Benutzernamen in Pfaden werden gemeldet, sperren aber nicht.
- Sie ist eine Hilfe, keine Garantie; das steht im Dialog.
- **Bildinhalte werden nicht gelesen** (keine Texterkennung). Was nur als Bildpunkte existiert, findet die Prüfung nicht.
- Die Muster sind Daten, versioniert, erweiterbar. Der Stand, mit dem der Autor zuletzt bestätigt hat, steht in `review.confirmed.patternsVersion`.

### 11.3 Support-Paket

Beim Support-Paket verlässt eine Aufzeichnung den Rechner mit Sicherheit – und zwar durch jemanden, der das Programm kaum kennt: Ein Helpdesk gibt die EXE weiter, die Person führt ihr Problem vor und schickt eine ZIP-Datei zurück ([07](07-aufnahme-bedienung.md#49-support-paket-aufnehmen), [08](08-viewer.md#122-support-paket-geführter-ablauf), [09](09-anleitungs-export.md#35-support-paket), [02](02-architektur.md#support-paket)). Deshalb sind die Schutzschritte hier nicht nur angeboten, sondern Teil des Ablaufs.

| Nr. | Regel |
|---|---|
| SP1 | **Die Durchsicht ist Pflicht und lässt sich nicht überspringen.** Der geführte Ablauf zeigt im zweiten Schritt die Liste aller Eingaben und Autorentexte ([11.1](#111-durchsicht-aller-eingaben)). Jede auffällige Eingabe braucht ihre Einzelentscheidung; die übrigen werden ausdrücklich bestätigt – auch wenn keine einzige auffällig ist. Vorher gibt es „Paket erstellen“ nicht. Die Komponente, die das Paket baut, prüft diese Vorbedingung selbst und verlässt sich nicht auf die Oberfläche. |
| SP2 | **Schwärzen und Verallgemeinern gehören zur Durchsicht.** Jeder Eintrag der Liste bietet beides an; die Vorschläge aus [10.9](#109-verallgemeinerung-von-werten) stehen oben. |
| SP3 | **Nur aus einer bereinigten Kopie.** Das Paket entsteht nie aus dem Original, sondern aus einer bereinigten Kopie ([10.8](#108-endgültiges-schwärzen)): Schwärzungen und Verallgemeinerungen sind dort endgültig angewendet, und die Gegenprobe ist bestanden, **bevor** irgendeine Ausgabe entsteht. Schlägt sie fehl, entsteht kein Paket. |
| SP4 | **Genau zwei Ausgaben.** Das ZIP enthält die HTML-Anleitung in einer Datei und das KI-Paket ([06](06-ki-export-und-mcp.md#32-das-ki-paket-im-support-paket)) – keinen Sitzungsordner, kein Rohprotokoll, keine `edits.json`, keinen Cache, keine Einstellungen, kein Protokoll des Programms. Auch die bereinigte Kopie liegt nicht bei; sie wird nach dem Packen mit dem Arbeitsordner gelöscht. |
| SP5 | **Das Programm versendet nichts.** Es ruft kein Mailprogramm, keinen Teilen-Dialog, keinen Upload und keine Adresse auf; am Ende öffnet es den Ordner im Explorer. Die Netzwerkregeln aus [Abschnitt 12](#12-netzwerkfreiheit) gelten unverändert. Wohin die Datei geht, entscheidet und tut die Person selbst. |
| SP6 | **Kein Weg ohne Oberfläche.** Der Befehl `support` ([02](02-architektur.md#befehle-nachgelagert), [07](07-aufnahme-bedienung.md#32-support-aufnahme-über-die-kommandozeile)) öffnet nur den Ablauf; er nimmt nie von selbst auf und erzeugt nie von selbst ein Paket. Eine Option, die Durchsicht oder Kopie überspringt, gibt es nicht. |
| SP7 | **Auch der Dateiname ist eine Ausgabe.** `Support-<Datum>-<Titel>.zip` trägt den Titel so, wie die Leseschicht ihn ausgibt – mit Platzhaltern statt verallgemeinerter Werte. Der letzte Schritt des Ablaufs zeigt den Namen, bevor die Datei entsteht, und lässt ihn ändern. |
| SP8 | **Vorbelegte Texte sind Autorentext.** Bezug und Hinweis für den Empfänger können aus dem Profil oder von der Kommandozeile vorbelegt sein. Die Person sieht sie vor der Aufnahme, kann sie ändern, und sie durchlaufen die Durchsicht wie die Ziel-Angaben. Ein vorbelegter Text kann keinen Schritt des Ablaufs ersetzen oder überspringen. |
| SP9 | **Profil „Support-Fall“.** Für den Start gelten die Regeln aus [4.4](#44-aufnahmeprofile-nie-unbemerkt-schwächer): Auch ein mit der EXE mitgegebenes Profil dieses Namens senkt den Schutz nicht unbemerkt. |

Was der Ablauf nicht verhindern kann, sagt sein erster Bildschirm in zwei Sätzen: Bilder zeigen, was im Aufnahmebereich zu sehen war – wer etwas nicht zeigen will, nimmt nur die betroffene Anwendung auf oder schwärzt es. Und: Wer das Paket bekommt, kann es weitergeben.

## 12. Netzwerkfreiheit

### 12.1 Regeln

| Nr. | Regel |
|---|---|
| NW1 | Kein Prozess des Tools öffnet eine Netzwerkverbindung, löst einen Namen auf oder lauscht auf einem Port – in keiner Betriebsart. |
| NW2 | Kein Update-Check ohne ausdrückliches Einschalten (Ausnahme in P2), keine Telemetrie, keine Absturzberichte nach außen, keine Online-Hilfe, keine nachgeladenen Schriften oder Bilder. |
| NW3 | Keine eingebettete Browser-Komponente. PDF entsteht mit einem eigenen Schreiber ([02](02-architektur.md#technologie)). |
| NW4 | Exporte enthalten keine Verweise auf fremde Quellen; HTML-Exporte sind in sich geschlossen und sperren Nachladen per Richtlinie ([09](09-anleitungs-export.md#5-html)). |
| NW5 | Der MCP-Server nutzt nur stdio; Netzwerk-Transporte des SDK werden nicht eingebunden ([06](06-ki-export-und-mcp.md)). |
| NW6 | Eigener Code verwendet keine Netzwerk-Programmierschnittstellen. XML- und andere Parser lösen keine externen Verweise auf. |
| NW7 | Links in der Oberfläche (Projektseite, Lizenz) öffnen auf ausdrücklichen Klick den Standardbrowser; die Verbindung baut dann der Browser auf, nicht das Tool. |
| NW8 | Jede künftige Funktion mit Netzwerkbezug wäre ausdrücklich einzuschalten, standardmäßig aus und eigens dokumentiert – und stünde im Widerspruch zu P2, bräuchte also eine Änderung dieses Grundsatzes. |

### 12.2 Was nicht in der Hand des Tools liegt

- **Windows** selbst kann beim Start einer EXE deren Signatur und Ruf online prüfen (SmartScreen, Sperrlisten).
- Liegt der Sitzungsordner auf einem **Netzlaufwerk** oder in einem **Sync-Ordner**, überträgt das Betriebssystem bzw. der Sync-Dienst die Dateien ([Abschnitt 13](#13-speicherorte-sync-ordner-und-spuren-außerhalb-der-sitzung)).
- Ein **Agent**, der über MCP liest, kann das Gelesene an seinen KI-Dienst senden. Das ist der Zweck des MCP-Servers und geschieht außerhalb des Tools.

### 12.3 Nachweis

| Ebene | Prüfung |
|---|---|
| Build | Automatische Prüfung der eigenen Assemblies: keine Verweise auf Netzwerk-Namensräume; Abhängigkeiten gegen eine Erlaubnisliste |
| Abnahme | T-12 in [11](11-roadmap-tests-risiken.md): Mitschnitt aller Netzwerkereignisse des Prozesses und seiner Kindprozesse über Aufnahme, Bearbeitung, alle Exporte und MCP-Betrieb – erwartet: keine Verbindung, keine Namensauflösung, kein lauschender Port |
| Nutzer | Anleitung in der Dokumentation: ausgehende Verbindungen der EXE per Firewall-Regel sperren – das Tool funktioniert unverändert; Kontrolle im Ressourcenmonitor |
| Selbstauskunft | Der Info-Dialog nennt die Zusage und verweist auf die Prüfanleitung |

## 13. Speicherorte, Sync-Ordner und Spuren außerhalb der Sitzung

### 13.1 Sync-Ordner und Netzlaufwerke

Eine Sitzung in einem synchronisierten Ordner wird **während der Aufnahme** in die Cloud übertragen – ungeprüft, ungeschwärzt, und mit vielen kleinen Schreibvorgängen, die den Sync-Dienst und die Aufnahme ausbremsen.

| Punkt | Festlegung |
|---|---|
| Erkennung | OneDrive (Umgebungsvariablen, Kennzeichen für Cloud-Dateien), Nextcloud und ownCloud (Sync-Datenbank im Ordnerbaum), Dropbox, Google Drive, iCloud Drive, Syncthing (jeweils Kennzeichen im Ordnerbaum bzw. Laufwerkstyp), Netzlaufwerke |
| Warnung | Im Startfenster und in den Einstellungen, **bevor** die Aufnahme beginnt, mit Begründung und der Empfehlung eines lokalen Ordners; Wahlmöglichkeiten und Wortlaut in [07](07-aufnahme-bedienung.md#44-weitere-elemente). *Soll:* „lokal aufnehmen und nach der Durchsicht hierher verschieben“. |
| Standardordner | **`Sessions` neben der EXE**, sofern dieser Ordner beschreibbar ist und in keinem erkannten Sync-Ordner liegt; **sonst `%LOCALAPPDATA%\StepRecorder\Sessions`**. Der Vorschlag wird geprüft, bevor er angeboten wird, und beim Erststart bestätigt ([07](07-aufnahme-bedienung.md#14-erststart), [02](02-architektur.md#konfiguration)). **Nie „Dokumente“:** Dieser Ordner wird von OneDrive häufig umgeleitet und synchronisiert und wird deshalb weder als Standard noch als Ausweichort vorgeschlagen. Liegt die EXE selbst in einem Sync-Ordner (portable Nutzung aus einem Cloud-Ordner) oder an einem schreibgeschützten Ort, greift der Ausweichort: Er gehört zum lokalen, nicht servergespeicherten Teil des Benutzerprofils und liegt üblicherweise in keinem Sync-Ordner – geprüft wird er trotzdem. Jeder vom Nutzer selbst gewählte Ordner durchläuft dieselbe Prüfung. |
| Öffnen | Wird eine vorhandene Sitzung aus einem Sync-Ordner geöffnet, erscheint ein unaufdringlicher Hinweis, sobald geschwärzt wird: Der Versionsverlauf des Dienstes kann die alte Fassung behalten. |
| Grenze | Die Erkennung ist eine Heuristik; unbekannte Dienste und Sicherungsprogramme erkennt sie nicht. |

### 13.2 Schutz im Dateisystem

- Der Sitzungsordner erhält das Kennzeichen „Inhalt nicht indizieren“, damit die Windows-Suche getippten Text nicht in ihren Index aufnimmt (*Soll*).
- Das Tool legt keine Kopien außerhalb des Sitzungsordners an. **Zwischendateien** – für „Bild als Datei kopieren“, das Herausziehen von Bildern, das externe Bearbeiten und die Exportvorschau – liegen im Sitzungsordner unter `export/.tmp/`, **nie in `%TEMP%`**. Sie enthalten nur die Sicht der Leseschicht (Schwärzungen angewendet), werden beim Schließen der Sitzung entfernt, und Reste eines Absturzes räumt das nächste Öffnen der Sitzung auf. Nur eine schreibgeschützte `.steprec`-Datei hat keinen schreibbaren Sitzungsordner; ihre Vorschau liegt in einem Unterordner von `%TEMP%` und wird beim Schließen entfernt ([08](08-viewer.md#78-extern-bearbeiten), [09](09-anleitungs-export.md#13-dateibenennung-und-ablage)).
- **Löschen heißt löschen, aber nie still endgültig:** „Aufnahme verwerfen“ (Leiste) und „Löschen“ (Bibliothek) verschieben den Ordner in den Papierkorb. Hat das Laufwerk keinen Papierkorb, fragt das Programm ausdrücklich, dass endgültig gelöscht wird und das nicht rückgängig zu machen ist; ohne diese Zustimmung bleibt der Ordner erhalten. Lässt sich der Ordner nicht verschieben, bleibt er ebenfalls erhalten und der Pfad wird genannt. Eine Aufbewahrungsfrist („Rohdaten nach n Tagen löschen“) ist einstellbar und standardmäßig aus.
- **Verschlanken** (*Soll*): entfernt Spurdaten und ungenutzte Bilder aus einer fertigen Sitzung.
- Keine Verschlüsselung in Version 1. Schutz ruhender Daten ist Sache des Betriebssystems (Laufwerksverschlüsselung); eine kennwortgeschützte `.steprec`-Datei ist eine mögliche Ausbaustufe.

### 13.3 Spuren, die das Betriebssystem anlegt

Das Tool schreibt nichts in die Registry und nichts außerhalb seiner Ordner. Folgende Spuren entstehen dennoch durch Windows und sind dokumentiert:

| Spur | Entsteht durch | Umgang |
|---|---|---|
| Entpackte Laufzeitteile unter `%TEMP%` | Single-File-EXE ([02](02-architektur.md#entpacken)) | dokumentiert; ZIP-Variante entpackt nichts |
| Miniaturansichten im Explorer-Cache | Ansehen von Bildordnern im Explorer (`frames/`, `export/`) | dokumentiert; *Kann:* Bilder im Sitzungsordner mit neutraler Dateiendung speichern (Entscheidung in [04](04-datenformat.md)) |
| „Zuletzt verwendet“ und Sprunglisten | Öffnen von Dateien über Windows-Dialoge | das Tool meldet geöffnete Sitzungen nicht an die Liste zuletzt verwendeter Dokumente |
| Auslagerungs- und Ruhezustandsdatei | Speicherverwaltung von Windows | Rückhaltepuffer gegen Auslagerung gesperrt ([5.12](#512-arbeitsspeicher)); Ruhezustand bleibt Restrisiko |
| Versionsverlauf, Sicherungen, Schattenkopien | Sync- und Sicherungsprogramme | Hinweis beim Schwärzen |
| Protokolldateien des MCP-Clients | Clients schreiben die Fehlerausgabe des Servers mit | Der Server protokolliert nie aufgezeichnete Inhalte ([06](06-ki-export-und-mcp.md)) |

### 13.4 Einstellungen und Profile weitergeben

Einstellungen, Vorlagen und Profile lassen sich in eine Austauschdatei `StepRecorder-Einstellungen.json` schreiben und daraus einlesen ([07](07-aufnahme-bedienung.md#132-einstellungen-exportieren-und-importieren), [02](02-architektur.md#einstellungs-austausch)). Für den Datenschutz zählen zwei Fragen: Was verrät die Datei – und was kann eine fremde Datei anrichten?

**Export: was die Datei enthält und was nicht**

| Regel | Festlegung |
|---|---|
| Nur auf Befehl | Die Datei entsteht nur, wenn der Nutzer sie ausdrücklich erzeugt, an dem Ort, den er nennt. Das Programm merkt sich den Ort nicht und legt keine zweite Kopie an. |
| Keine Sitzungsdaten | Keine Aufzeichnung, kein Sitzungstitel, keine Liste zuletzt verwendeter Sitzungen, keine Fensterpositionen. |
| Keine Namen, keine Pfade | Kein Rechner-, Benutzer- oder Domänenname; kein Aufnahmeordner und kein anderer absoluter Pfad. Dateien, auf die eine Vorlage verweist, werden eingebettet statt verlinkt. |
| Auswahl sichtbar | Der Dialog zeigt, welche Teile in die Datei gehen, und sagt, was sie verraten können: Die **Ausschlussliste** nennt Programme, die jemand für vertraulich hält; **Textbausteine**, Vorlagen und die Vorbelegungen für das Support-Paket sind Texte des Autors. Jeder Teil lässt sich abwählen. |
| Lesbar | Die Datei ist Klartext, weder verschlüsselt noch signiert. Wer sie bekommt, kann sie lesen und ändern – das sagt der Dialog. |

**Import: die Datei ist fremde Eingabe**

| Nr. | Regel |
|---|---|
| IM1 | **Nur auf Befehl.** Eingelesen wird nur über den Befehl in den Einstellungen oder `settings import`. Eine Austauschdatei, die neben der EXE oder in einem Sitzungsordner liegt, wird nie von selbst eingelesen. |
| IM2 | **Erst zeigen, dann ändern.** Vor jeder Änderung steht fest und wird gezeigt, was neu ist, was ersetzt wird und was gleich bleibt. Besteht die Datei die Prüfung nicht, ändert sich nichts. |
| IM3 | **Schutz schwächende Änderungen einzeln.** Als schwächend gilt nach der Liste in [4.4](#44-aufnahmeprofile-nie-unbemerkt-schwächer): ein Profil mit niedrigerer Schutzstufe oder eingeschaltetem Zwischenablage-Inhalt – verglichen mit dem gleichnamigen vorhandenen Profil, sonst mit den geltenden Einstellungen – und eine Ausschlussliste, der vorhandene Einträge fehlen. Jede solche Änderung steht als eigene Zeile da, ist nicht vorangekreuzt und wird einzeln bestätigt. Eine Schaltfläche, die alle auf einmal bestätigt, gibt es nicht. |
| IM4 | **Ohne Bestätigung unterbleibt genau diese Änderung.** Das schwächere Profil wird nicht übernommen; die fehlenden Einträge der Ausschlussliste bleiben erhalten. Alles Übrige wird eingelesen. |
| IM5 | **Kommandozeile.** Ohne `--confirm` zeigt `settings import` nur, was sich ändern würde. `--confirm` allein bestätigt keine schwächende Änderung: Jede braucht zusätzlich `--allow-weaker` mit ihrer Kennung; sonst unterbleibt sie und steht in der Ausgabe unter den ausgelassenen ([02](02-architektur.md#befehle-nachgelagert)). |
| IM6 | **Kürzel.** Lässt eine importierte Belegung die Privattaste oder „Letzte Eingabe schwärzen“ ohne Kürzel, weist der Dialog darauf hin; eine Belegung ohne Stopp-Kürzel wird nicht übernommen ([07](07-aufnahme-bedienung.md#132-einstellungen-exportieren-und-importieren)). Die Schaltflächen der Leiste bleiben in jedem Fall. |
| IM7 | **Keine Pfade, kein Netz.** Pfade in der Datei werden nie befolgt – auch keine Netzwerkpfade, bei denen schon das Öffnen eine Verbindung aufbaute. Eingebettete Dateien durchlaufen die Prüfung für Vorlagen: kein ausführbarer Inhalt, keine Makros, keine Verweise nach außen ([09](09-anleitungs-export.md#92-eigene-vorlagen)). |
| IM8 | **Grenzen.** Höchstgröße, strenges JSON, bekannte Formatversion; jeder Wert wird gegen denselben Wertebereich geprüft wie bei der Eingabe von Hand (02). Nichts aus der Datei wird ausgeführt. |
| IM9 | **Was sich nicht importieren lässt.** Die Austauschdatei kennt keine Einstellung, die den Hinweis auf die laufende Aufnahme, die Pflicht-Durchsicht, die Rückhaltung oder `privacy.recordIdentity` berührt, und keine, die einen Aufnahme- oder Exportordner setzt. |
| IM10 | **Rücknahme.** Der Stand vor dem Import bleibt als eine Sicherung erhalten und lässt sich wiederherstellen. Eine laufende Aufnahme bleibt von einem Import unberührt. |

Ein Import ändert nie eine vorhandene Sitzung. Er wirkt nur auf künftige Aufnahmen – und dort greift zusätzlich die Startmeldung aus 4.4.

### 13.5 Protokoll und Diagnose-Paket

Das Programm führt ein Protokoll seiner eigenen Abläufe, damit sich Fehler nachvollziehen lassen. Es ist kein Aufnahmeprotokoll und enthält **keine Inhalte**:

| Regel | Festlegung |
|---|---|
| Nur Abläufe | Das Protokoll nennt Ereignisse, Zustände und Fehler des Programms (Start, Selbsttest, Lücken, Ausnahmen). Es enthält nie Tasteninhalte, Feldwerte, Fenstertitel, Dateinamen aus Aufnahmen, Texte des Autors oder Werte von Kommandozeilen-Argumenten; von Argumenten stehen nur die Namen der Optionen darin. |
| Begrenzt | Je Datei höchstens 1 MB, dazu die vorige Datei; ältere Stände werden überschrieben. Abstürze in jedem Thread werden mitgeschrieben. |
| Ablage | im Zustandsordner des Programms, nie im Sitzungsordner; es geht in keine Sitzung, keine `.steprec`-Datei und keinen Export ein. |
| Diagnose-Paket | Die Einstellungen bieten „Diagnose-Paket …“: eine ZIP-Datei mit dem Protokoll, Programm- und Systemangaben und den bereinigten Einstellungen. Die Einstellungen kommen nach einer Positivliste (Vorlieben im Klartext, Listen nur als Anzahl, der Aufnahmeordner nur als „Standard“ oder „eigener“). Nie enthalten sind Aufnahmen, Texte des Autors, Pfade mit Benutzernamen sowie Rechner- und Domänenname. |
| Nur auf Befehl | Die Datei entsteht nur auf ausdrücklichen Befehl, liegt dort, wo der Nutzer sie speichert, und wird nicht versendet; die Person gibt sie selbst weiter ([07](07-aufnahme-bedienung.md#133-diagnose-paket)). |

Dazu passt die Regel L6: Es gibt keine Diagnose-Betriebsart, die die Leseschicht umgeht.

### 13.6 Sitzungsarchive (.steprec) und Zielordner

- **Packen.** Eine `.steprec`-Datei ist ein ZIP einer Sitzung. Gepackt wird nur, was zur Aufnahme gehört (Manifest, Journal, Schritte, Overlay, Bilder, Mauszeiger, eigene Bilder) – nie `cache/`, `export/`, `history/` oder Sperrdateien; absolute Pfade in Anhängen werden entfernt. Die Bibliothek bietet „Bereinigte Kopie“ (erst nach der Durchsicht) oder „Vollständig“; bei „Vollständig“ nennt der Dialog, was der Empfänger sehen oder aufheben kann, und verlangt eine zweite Bestätigung.
- **Entpacken ist geschützt.** Ein Archiv wird nie mit einem Sammelaufruf entpackt, sondern Eintrag für Eintrag nach einer Positivliste: nur bekannte Dateien und Ordner der Sitzung; keine Pfadsprünge, Laufwerksangaben, Gerätenamen, Namen länger als 260 Zeichen. Grenzen für Zahl der Einträge (200 000), Gesamtgröße (20 GB), Größe je Eintrag (2 GB) und Kompressionsverhältnis (250 : 1 bei Einträgen über 1 MB); überschreitet ein Eintrag beim Schreiben seine angegebene Größe, bricht das Entpacken ab. Es läuft über einen Zwischenordner und ersetzt erst am Ende: Ein präpariertes Archiv hinterlässt nichts. Nichts aus dem Archiv wird ausgeführt.
- **Fremde Anhänge.** Verweise auf angehängte Aufnahmen innerhalb einer entpackten Sitzung werden ignoriert; Namen von Bilddateien werden auf die erwartete Form geprüft (64 Hexzeichen), damit keine Pfade außerhalb der Sitzung gelesen werden.
- **Schreibgeschützt.** Eine geöffnete `.steprec` wird nicht entpackt, sondern gelesen; der Editor zeigt „Schreibgeschützt (.steprec)“ und bietet „Entpacken …“ an. Beim Entpacken in den Aufnahmeordner fragt das Programm bei einer bereits vorhandenen gleichen Aufnahme nach.
- **Zielordner fremder Ausgaben.** KI-Paket und AHK-KI-Paket leeren keinen Ordner, den sie nicht selbst angelegt haben; ein vorhandener fremder Zielordner führt zu Exit-Code 8.

## 14. Bedrohungsmodell

### 14.1 Schutzgüter

1. **Geheimnisse** des Nutzers: Passwörter, Schlüssel, Einmalcodes.
2. **Vertrauliche und personenbezogene Inhalte** auf dem Bildschirm – eigene und die Dritter.
3. **Selbstbestimmung** der Person am Rechner: nicht ohne ihr Wissen aufgezeichnet zu werden.
4. **Verlässlichkeit** der Aufzeichnung und der daraus erzeugten Anleitung.
5. **Rechner und Daten der Empfänger** einer Sitzung oder eines Exports.

### 14.2 Vertrauensgrenzen und Annahmen

| Vertraut | Nicht vertraut |
|---|---|
| das Betriebssystem und das Benutzerkonto, unter dem das Tool läuft | alles, was auf dem Bildschirm erscheint: Webseiten, Dokumente, Fenstertitel, Dateinamen |
| die signierte Programmdatei aus der offiziellen Quelle | Sitzungsdateien aus fremder Hand |
| der Nutzer als Autor seiner Schwärzungen und Kommentare | KI-Agenten und alles, was sie lesen |
| | Empfänger von Exporten |
| | Einstellungs-, Profil- und Austauschdateien aus fremder Hand |

**Außerhalb des Modells:** Angreifer, die bereits Code unter demselben Benutzerkonto oder mit Administratorrechten ausführen (sie könnten selbst mitschneiden), Schwachstellen des Betriebssystems, physische Angriffe auf den Datenträger.

### 14.3 Bedrohungen

| Nr. | Bedrohung | Angreifer | Gegenmaßnahme | Restrisiko |
|---|---|---|---|---|
| B1 | **Missbrauch als Keylogger:** heimliche Aufzeichnung einer anderen Person | jemand mit Zugang zum Rechner | P1: Hinweis nicht abschaltbar, Startmeldung, kein Autostart, Dienst oder Fernstart; Pause bei Sperre und Benutzerwechsel; P2: kein Abfluss durch das Tool; signierte Releases als Merkmal echter Builds | Quelloffen: Ein veränderter Nachbau ohne Hinweis lässt sich nicht verhindern, trägt aber nicht die Projektsignatur. Ein sichtbarer Hinweis kann übersehen werden. |
| B2 | **Verdeckte Überwachung von Beschäftigten** | Arbeitgeber, Administrator | wie B1; keine zentrale Verwaltung, keine stille Verteilung von Aufnahme-Einstellungen; rechtliche Hinweise ([Abschnitt 15](#15-rechtliche-hinweise)) | organisatorischer Missbrauch sichtbarer Aufnahmen |
| B3 | **Versehentlich aufgezeichnetes Geheimnis** | der Nutzer selbst | Rückhaltung, sensible Felder, Passwortmanager-Erkennung, Privattaste, Pflicht-Durchsicht des Getippten aus Terminal, Remote-Desktop und VM, Maskierung dort in der Stufe „Streng“, Musterprüfung, Erkennung von Passwortabfragen im Terminal (T1 bis T3, im echten Terminal noch zu bestätigen) | Terminals, Remote-Desktop und VM in der Stufe „Schutznetz“, solange die Person die Durchsicht nicht sorgfältig macht; Geheimnisse als Befehlsargument; Versagen der Wiedererkennung nach „Passwort anzeigen“ |
| B4 | **Sensible Inhalte in Bildern** werden weitergegeben | Nutzer → Empfänger | Aufnahmebereich, Ausschlussliste, Schwärzung mit Ausdehnung, Bildvorschläge aus Elementrechtecken, Durchsicht | Inhalte, die nur als Bildpunkte existieren; übersehene Stellen; Benachrichtigungen |
| B5 | **Umgehung einer Schwärzung** | Empfänger, KI-Agent | Leseschicht für alle Wege, Verwerfen der Caches, Platzhalter ohne Länge, Verpixelung nicht aus Originalpixeln, Suche ohne Orakelwirkung | Weitergabe des Sitzungsordners statt einer Ausgabe; frühere Exporte |
| B6 | **Reste nach endgültigem Schwärzen** | wer Zugriff auf Datenträger, Sicherung oder Versionsverlauf hat | Gegenprobe; Hinweise; Empfehlung, vor der ersten Synchronisierung zu schwärzen; bereinigte Kopie | bleibt bestehen |
| B7 | **Unbeabsichtigter Upload** roher Aufzeichnungen | Sync-Dienst, Cloud-Anbieter | Erkennung, Warnung, lokaler Standardordner ([13.1](#131-sync-ordner-und-netzlaufwerke)) | unerkannte Dienste und Sicherungsprogramme |
| B8 | **Prompt-Injection** über aufgezeichnete Texte | wer eine Webseite oder ein Dokument gestaltet hat, das während der Aufnahme sichtbar war | Entschärfung und Kennzeichnung ([06, Abschnitt 12](06-ki-export-und-mcp.md)); MCP standardmäßig nur lesend; das Schreib-Tool ändert nur Anleitungstexte im Overlay; keine Tools mit Außenwirkung | Modellverhalten ist nicht garantierbar; der Agent kann andere, mächtigere Werkzeuge besitzen |
| B9 | **Dateizugriff über MCP** | fehlgeleiteter oder bösartiger Agent | Root-Ordner-Sandbox, Kennungen statt Pfade, keine absoluten Pfade in Antworten, Größen- und Mengengrenzen, Schreiben nur mit Flag ([06, Abschnitt 17](06-ki-export-und-mcp.md)) | Alles im freigegebenen Ordner ist für den Agenten lesbar – der Ordner ist bewusst zu wählen |
| B10 | **Abfluss über den Agenten** an dessen KI-Dienst | KI-Dienst | Root-Ordner als Einwilligungsgrenze; Regel L7 (Ungeprüftes wird zurückgehalten); Hinweis im Konfigurationsdialog | bewusst gewählte Nutzung; rechtliche Prüfung liegt beim Nutzer |
| B11 | **Präparierte Sitzungsdatei** | Absender einer `.steprec`-Datei | Grenzen für Größen, Anzahl und Kompression; Entpacken Eintrag für Eintrag nach Positivliste über einen Zwischenordner, bei Fehlern bleibt nichts zurück ([13.6](#136-sitzungsarchive-steprec-und-zielordner)); keine Pfade aus Dateien; nichts wird ausgeführt; aktuelle Bibliotheken für Bilder und ZIP; fremde Sitzungen werden nie mit erhöhten Rechten geöffnet; Muster von Verallgemeinerungsregeln werden mit Zeitgrenze und begrenzter Länge ausgewertet, und ihr Ersatz ist immer ein Platzhalter (V2, V8) | unbekannte Fehler in Bibliotheken |
| B12 | **Eingeschleustes Markup** oder aktive Inhalte in Exporten | Autor von Bildschirminhalten; Opfer ist der Empfänger | Entschärfung aller aufgezeichneten Texte; HTML ohne Skripte und mit Sperre gegen Nachladen; DOCX ohne Felder, Makros, externe Verknüpfungen; PDF ohne aktive Inhalte ([09](09-anleitungs-export.md#11-entschärfung-aufgezeichneter-texte)) | gering |
| B13 | **Manipulierte Programmdatei**, kompromittierte Abhängigkeit | Verbreiter gefälschter Downloads; Angreifer in der Lieferkette | signierte Releases, Prüfsummen, Bau aus dem öffentlichen Repository, wenige Abhängigkeiten mit festgeschriebenen Versionen, Prüfung auf bekannte Schwachstellen im Build | kompromittiertes Build-System |
| B14 | **Untergeschobene Bibliothek** neben der portablen EXE | Schadsoftware; präparierter Download-Ordner | sichere Suchreihenfolge für Bibliotheken ([02](02-architektur.md)); in der ZIP-Variante sind alle eigenen Dateien signiert; Hinweis, die EXE nicht aus Ordnern mit fremden Dateien zu starten | Angreifer mit Schreibrecht im Programmordner |
| B15 | **Erhöhte Rechte** verstärken Fehler | – | Administratorrechte nur für die Aufnahme und nur auf Wunsch; Viewer, Exporte und MCP-Server laufen nie erhöht | Fehler im Aufzeichnungskern |
| B16 | **Auslesen zurückgehaltener Tasten** aus dem Arbeitsspeicher | Speicherabbild, Auslagerungsdatei | [5.12](#512-arbeitsspeicher) | Ruhezustand; Angreifer mit gleichen Rechten (außerhalb des Modells) |
| B17 | **Verfälschte Aufzeichnung:** Tasten verschluckt oder eingeschleust | andere Software | zweiter Eingabekanal, Kennzeichen „von Programm erzeugt“, Lückenmarker ([03](03-aufzeichnungskern.md)) | gezielte Manipulation mit gleichen Rechten |
| B18 | **Dritte im Bild:** Videokonferenz, Bildschirmfreigabe, Chat | der Aufzeichnende | Aufnahmebereich „eine Anwendung“; Ausschlussregeln für Konferenz- und Chatprogramme; rechtlicher Hinweis | organisatorisch |
| B19 | **Präparierte Einstellungs- oder Profildatei:** Eine Austauschdatei oder eine mit der EXE mitgegebene `profiles.json` soll den Schutz senken – Schutzstufe, Zwischenablage-Inhalt, Ausschlussliste, Kürzel – oder über Vorlagen und Pfade Inhalte einschleusen | Absender der Datei; wer die EXE mit Beigaben weitergibt | Import nur auf Befehl, Vorschau, Einzelbestätigung jeder schwächenden Änderung, auf der Kommandozeile `--allow-weaker` je Änderung ([13.4](#134-einstellungen-und-profile-weitergeben)); Profile berühren den Schutz nur innerhalb der Liste aus [4.4](#44-aufnahmeprofile-nie-unbemerkt-schwächer) und nie unbemerkt – Startmeldung bei jedem Start, `weakens` im Manifest; Pfade werden nie befolgt, eingebettete Dateien wie Vorlagen geprüft, feste Größen- und Wertgrenzen | Wer eine schwächende Änderung bestätigt, ohne sie zu verstehen; eine übersehene Startmeldung |
| B20 | **Scheinsicherheit durch Verallgemeinerung:** Ein Wert gilt als entfernt, steht aber noch in Bildern, in anderer Schreibweise oder im weitergegebenen Sitzungsordner | Nutzer → Empfänger | Regeln V1–V8 ([10.9](#109-verallgemeinerung-von-werten)): eine Leseschicht für alle Wege, Tasten eingeschlossen; Bildstellen ohne bekannte Textlage werden gemeldet statt verschwiegen; endgültiges Anwenden entfernt Wert und Regelwert; Gegenprobe | Text, der nur als Bildpunkte existiert; abweichende Schreibweisen; Weitergabe des Ordners vor dem endgültigen Anwenden |
| B21 | **Suche oder Vergleich als Orakel:** Ein verborgener Inhalt soll erraten werden, indem man danach sucht oder eine zweite Sitzung mit Vermutungen danebenlegt | KI-Agent; wer eine präparierte Sitzung in den freigegebenen Ordner legt | Regel L11: Zurückgehaltenes steht in keinem Suchindex und geht in keinen Vergleich ein; es wird nie als gleich oder verschieden gemeldet; der Index wird bei jeder Änderung der Sicht verworfen; Vergleichsergebnisse werden nicht gespeichert | gering |
| B23 | **Aufnahme auf Zuruf eines Agenten:** Ein KI-Agent (oder Text, den er verarbeitet hat) drängt die Person, etwas vorzuführen, und formuliert den Anlass so, dass sie zustimmt | KI-Agent; wer ihm Text unterschiebt | Abschnitt 2.2: nur mit Schalter, Text als „ungeprüft“ gekennzeichnet, bereinigt und begrenzt, kein Standardknopf, Bedenkzeit von 2 s, Frist, Ratenbegrenzung, „15 Minuten keine Anfragen“; sichtbare normale Aufnahme; keine Fernsteuerung | die Person stimmt trotzdem zu; was sie dann vorführt, ist wie jede Aufnahme zu behandeln |
| B24 | **Falsche Bilder und mitgetippte Kennwörter auf dem sicheren Desktop:** schwarze oder halbe Bilder, Eingaben in der Abfrage der Benutzerkontensteuerung | andere Software; Zufall | Abschnitt 2.1: Lücke `secureDesktop`, keine Bilder, Ringpuffer verworfen, Tastenstatus abgleichen | Zeitraum bis zur ersten Lesung (höchstens 250 ms) |
| B22 | **Erschlichenes Support-Paket:** Jemand gibt sich als Helpdesk aus, lässt eine Person ihr Problem aufzeichnen und sich das Paket schicken | Dritter mit Zugang zu einem Kommunikationsweg | sichtbare Aufnahme (P1); Pflicht-Durchsicht mit Schwärzen und Verallgemeinern; nur bereinigte Kopie; genau zwei Ausgaben; das Programm versendet nichts und kennt keinen Empfänger ([11.3](#113-support-paket)) | Täuschung der Person – organisatorisch zu lösen; was im Bild zu sehen war und nicht geschwärzt wurde |

## 15. Rechtliche Hinweise

> Die folgenden Hinweise beschreiben die Rechtslage in Deutschland und der EU in groben Zügen. Sie sind **keine Rechtsberatung** und ersetzen nicht die Prüfung durch Datenschutzbeauftragte oder Rechtskundige. Vor der Veröffentlichung wird dieser Abschnitt fachlich gegengelesen ([Abschnitt 20](#20-annahmen-und-offene-punkte)).

### 15.1 Wer verantwortlich ist

Das Projekt stellt Software bereit und verarbeitet selbst keine Daten – das Tool arbeitet offline. **Verantwortlich** im Sinne der Datenschutz-Grundverordnung ist, wer aufzeichnet, bzw. die Organisation, in deren Auftrag aufgezeichnet wird. Für rein persönliche oder familiäre Tätigkeiten gilt die Verordnung nicht.

### 15.2 Datenschutz-Grundverordnung in der Praxis

| Thema | Was zu bedenken ist | Wie das Tool hilft |
|---|---|---|
| Rechtsgrundlage (Art. 6) | Das eigene Arbeiten zu dokumentieren ist in der Regel unproblematisch. Sobald Daten anderer Personen im Bild sind, braucht es eine Grundlage – je nach Lage Vertrag, berechtigtes Interesse oder Einwilligung. | – |
| Zweckbindung und Datenminimierung (Art. 5) | Nur aufnehmen, was die Anleitung braucht. | Aufnahmebereich, Ausschlussliste, Schutzstufe, Schwärzung |
| Speicherbegrenzung (Art. 5) | Rohdaten löschen, wenn die Anleitung fertig ist. | Löschen, Aufbewahrungsfrist, Verschlanken |
| Information und Rechte Betroffener (Art. 13 ff.) | Wer in einer Aufzeichnung erkennbar ist, hat Anspruch auf Information und Auskunft. | Schwärzung vor Weitergabe |
| Sicherheit (Art. 32) | Aufzeichnungen sind sensible Dateien. | lokale Speicherung, Warnung vor Sync-Ordnern; Laufwerksverschlüsselung ist Sache des Systems |
| Voreinstellungen (Art. 25) | Datenschutz durch Technik und Voreinstellung | Grundsätze P1–P9 |
| **Weitergabe an KI-Dienste** (Art. 28, 44 ff.) | Ein hochgeladenes KI-Paket oder ein Agent, der über MCP liest, übermittelt Inhalte an einen Dienstleister, womöglich in ein Drittland. Das ist vorher zu klären. | Durchsicht und Musterprüfung; Regel L7; Stufe „Streng“ |
| Verzeichnis, Folgenabschätzung (Art. 30, 35) | Bei planmäßigem Einsatz in einer Organisation | Kurzbeschreibung des Tools als Vorlage (*Kann*) |

### 15.3 Beschäftigte und Mitbestimmung

- Nach § 87 Abs. 1 Nr. 6 Betriebsverfassungsgesetz bestimmt der Betriebsrat mit bei Einführung und Anwendung technischer Einrichtungen, die zur Überwachung von Verhalten oder Leistung der Beschäftigten bestimmt sind. Nach der Rechtsprechung genügt, dass eine Einrichtung dazu **objektiv geeignet** ist. Ein Programm, das Eingaben mit Zeitstempeln aufzeichnet, ist das. Der Einsatz im Betrieb sollte deshalb mit dem Betriebsrat abgestimmt und am besten in einer Betriebsvereinbarung geregelt werden (Zweck: Dokumentation, keine Leistungs- und Verhaltenskontrolle; wer aufzeichnet; Aufbewahrung). Für den öffentlichen Dienst gelten die Personalvertretungsgesetze entsprechend.
- Die verdeckte Überwachung von Beschäftigten mit einem Keylogger ohne konkreten Verdacht einer Straftat oder schweren Pflichtverletzung ist unzulässig; so gewonnene Erkenntnisse sind in der Regel nicht verwertbar (Bundesarbeitsgericht, Urteil vom 27.07.2017 – 2 AZR 681/16). Das Tool ist dafür weder gedacht noch geeignet ([Grundsatz P1](#2-grundsätze)).
- Für Beschäftigtendaten gelten Art. 88 der Verordnung und § 26 Bundesdatenschutzgesetz.

### 15.4 Dritte, Vertraulichkeit, Urheberrecht

- **Videokonferenzen, Chats, E-Mails:** Wer andere Personen oder deren Nachrichten mit aufnimmt, braucht dafür eine Grundlage und sollte sie informieren. Für Kamerabilder gilt zusätzlich das Recht am eigenen Bild.
- **Fremde Systeme:** Aufnahmen bei Kunden oder im Fernzugriff berühren Vertraulichkeitsvereinbarungen und Geschäftsgeheimnisse; Berufsgeheimnisträger unterliegen § 203 Strafgesetzbuch.
- **Abgebildete Software:** Bildschirmfotos fremder Programme in Anleitungen sind üblich; Lizenzbedingungen und Markenrechte der Hersteller sind dennoch zu beachten.
- **Andere Länder** haben eigene Regeln, teils strengere.

### 15.5 Was in der Nutzerdokumentation stehen muss

Die Tabelle der Zusicherungen aus [Abschnitt 1](#1-zweck-und-geltungsbereich), die Hinweise dieses Abschnitts in Kurzform, die Empfehlung der Stufe „Streng“ für fremde Systeme und der Hinweis auf KI-Dienste – im Handbuch und, als ein Satz mit Verweis, im Startfenster.

## 16. Virenscanner, SmartScreen, Smart App Control, Signierung

### 16.1 Warum das Tool auffällt

| Merkmal | Warum Schutzprogramme reagieren | Warum es hier nötig ist |
|---|---|---|
| Globale Tastatur- und Maus-Hooks | typisches Merkmal von Keyloggern; einige Schutzprogramme melden schon den Aufruf | Kern der Aufzeichnung |
| Bildschirmaufnahme | typisch für Spionageprogramme | Kern der Aufzeichnung |
| Fenster, die in Aufnahmen nicht erscheinen | selten und daher verdächtig | Unsichtbarkeitsgarantie |
| Neue, selten gesehene, unsignierte EXE | kein Ruf bei SmartScreen | jedes neue Projekt |
| Single-File-EXE, die Teile entpackt | ähnelt gepackter Schadsoftware | portable Auslieferung |

### 16.2 Was Nutzer ohne Gegenmaßnahmen sehen

- **SmartScreen:** „Der Computer wurde durch Windows geschützt“; Start nur über „Weitere Informationen → Trotzdem ausführen“. Unsignierte Dateien bauen ihren Ruf je Datei auf – jede neue Version beginnt bei null. Signierte Dateien zeigen den Herausgeber, und der Ruf überträgt sich zwischen Versionen mit demselben Zertifikat; anfangs kann trotzdem gewarnt werden. Auch teurere Zertifikate umgehen die Warnung nicht mehr.
- **Smart App Control** (Windows 11): kann unsignierte oder unbekannte Programme ganz blockieren, ohne Möglichkeit, das im Einzelfall zu übergehen.
- **Virenscanner und Unternehmens-Schutzsoftware:** Verhaltensmeldungen („möglicher Keylogger“) und gelegentlich falsche Zuordnungen zu Schadsoftware-Familien.
- **Herkunftskennzeichen:** Aus dem Internet geladene Dateien tragen ein Kennzeichen, das diese Prüfungen auslöst; es vererbt sich beim Entpacken einer ZIP-Datei mit dem Explorer.

### 16.3 Code-Signierung

| Punkt | Festlegung |
|---|---|
| Weg | **SignPath Foundation** – kostenlose Signierung für Open-Source-Projekte |
| Bedingungen (Stand November 2026; die Bedingungen des Anbieters sind als Entwurf gekennzeichnet und vor M8 erneut zu prüfen) | anerkannte Open-Source-Lizenz ohne kommerzielle Doppellizenz; keine proprietären Bestandteile; aktiv gepflegt; bereits veröffentlicht; Funktionsbeschreibung auf der Download-Seite; Bau der signierten Dateien aus dem öffentlichen Repository |
| Folgen | Als Herausgeber erscheint „SignPath Foundation“; die Projektseite nennt den Anbieter wie verlangt. Die Bedingung „keine proprietären Bestandteile“ deckt sich mit der Abhängigkeitsregel aus [02](02-architektur.md#technologie). |
| Warum nicht Azure | Die Signierung über Azure steht Einzelpersonen nur in den USA und Kanada offen, in der EU nur Organisationen. |
| Ausweichweg | eigenes Zertifikat einer Zertifizierungsstelle (kostenpflichtig; Schlüssel auf Hardware oder in einem Cloud-Schlüsseldienst) |
| Was signiert wird | die Single-File-EXE; in der ZIP-Variante alle eigenen Programmdateien; eingebettete eigene Dateien vor dem Bündeln; jeweils mit Zeitstempel, damit die Signatur nach Ablauf des Zertifikats gültig bleibt |
| Bis dahin | Vorabversionen sind ausdrücklich als unsigniert gekennzeichnet und kommen mit Prüfsummen und der Anleitung, wie man sie prüft |

Signierte Releases sind Voraussetzung für Version 1.0 (R-01, M8 in [11](11-roadmap-tests-risiken.md)).

**Stand der Umsetzung:** Die Signierung ist **nicht umgesetzt**. Der automatische Bau (Tests auf `windows-latest`, bei einem Tag `v*` ein Release mit EXE und `SHA256SUMS`) liefert unsignierte Dateien mit Prüfsummen; die Anleitung zum Prüfen gehört zu jedem Release.

### 16.4 Verhalten, das Vertrauen schafft

- Keine Verschleierung, kein Laufzeitpacker, keine Selbstveränderung. Ob die Kompression der Single-File-EXE Erkennungen auslöst, misst VE-12; sie wird nur verwendet, wenn nicht.
- Keine Verankerung im System: keine Autostart-Einträge, geplanten Aufgaben, Dienste, Treiber.
- Kein Netzwerk; kein Zugriff auf Anmeldedaten, Browserprofile oder fremde Prozesse (P9).
- Quelltext, Build-Anleitung und eine Liste aller genutzten Systemschnittstellen sind öffentlich.

### 16.5 Fehlalarme

- Vor jeder Veröffentlichung wird die Datei mit Microsoft Defender und einem Mehrfachscanner geprüft; Ergebnisse stehen in den Release-Notizen.
- Fehlalarme werden gemeldet: bei Microsoft über die Einreichungsseite für Dateien (`https://www.microsoft.com/wdsi/filesubmission`, Rolle „Software developer“), bei anderen Herstellern über deren Formulare. Das Projekt führt eine Liste der Meldewege und einen Mustertext.
- Nutzer können Erkennungen über eine Vorlage im Projekt melden (Schutzprogramm, Version, Name der Erkennung, Dateiprüfsumme).

### 16.6 Für Administratoren

Ein eigenes Dokument „Was das Programm technisch tut“ (*Soll*) nennt:

- die genutzten Schnittstellen: Low-Level-Hooks für Tastatur und Maus, Raw Input, Bildschirmaufnahme (Desktop Duplication, ersatzweise Windows.Graphics.Capture oder GDI), UI Automation als Client, WinEvent-Benachrichtigungen ohne Einschleusung in fremde Prozesse, Zwischenablage-Benachrichtigung, Ausnahme der eigenen Fenster von Aufnahmen;
- was es **nicht** tut: Code in andere Prozesse laden, Treiber oder Dienste installieren, sich im System verankern, Netzwerk nutzen, Anmeldedaten lesen;
- wie sich das Programm über das Herausgeberzertifikat freigeben lässt, und welche Verhaltensregeln in Schutzsoftware typischerweise anzupassen sind.

## 17. Prüfungen

Datenschutz-Prüfungen, die in den Testplan ([11](11-roadmap-tests-risiken.md)) eingehen. „Kanarie“ ist eine eindeutige Zeichenfolge bzw. ein eindeutiges Bildmuster, nach dem anschließend gesucht wird – in Sitzungsordner, `cache/`, `export/`, Temp-Ordner und Protokollen, als UTF-8, UTF-16 und Base64.

| ID | Prüfung | Erwartung | Bezug |
|---|---|---|---|
| DT-01 | Kanarie in Passwortfelder tippen: Win32, WPF, WinForms, Chromium, Firefox, Electron, Windows-Anmeldeabfrage; auch per Einfügen | Kanarie nirgends | T-07 |
| DT-02 | „Passwort anzeigen“ mit Bild-Kanarie (auffällige Schrift) | Kein Bild zeigt den Klartext; Feld schwarz; Feldinhalt nicht gespeichert | T-07 |
| DT-03 | Fokus-Wettlauf: geskriptete Folgen aus Tab und Tippen mit 0–500 ms Abstand, 10 000 Durchläufe – auch gegen eine Test-Zielanwendung, die Eingaben künstlich verzögert verarbeitet (0–2 s) | 0 Klartext-Zeichen aus Passwortfeldern | VE-08 |
| DT-04 | Anwendung ohne Fokusereignisse (abschaltbar in der Test-Zielanwendung) | Passwortlauf maskiert; Fenster als unzuverlässig markiert | – |
| DT-05 | Terminal: `ssh`, `sudo`, `runas` (deutsch und englisch), `Read-Host -AsSecureString`, Git-Abfrage; dazu zwei Abfragen unmittelbar hintereinander (`passwd`: neues Kennwort und Wiederholung), ohne dass der Nutzer etwas schaltet | Passwörter maskiert, Abfragezeile und Regel gespeichert; bei zwei Abfragen zwei Einträge, jeder endet mit der Eingabetaste; Fehlalarmquote auf einem Korpus üblicher Befehle gemessen und dokumentiert. **Nicht anwendbar, solange die Erkennung (Abschnitt 6) nicht gebaut ist.** Stattdessen gilt DT-15 für Getipptes im Terminal | R-05 |
| DT-06 | Privattaste vor dem Tippen und mitten im Tastenlauf; zweifache Passwortabfrage in einem Fenster ohne Terminal-Erkennung; Privattaste während einer erkannten Abfrage; Erinnerung | Ab dem ersten Tastendruck nach dem Einschalten keine Kanarie; der Privatmodus der Privattaste endet nicht mit der Eingabetaste, sondern erst mit dem nächsten Druck; an der erkannten Abfrage trägt der Lauf den Grund `terminalPrompt` mit Abfragezeile; Zustand sichtbar; Ein- und Ausschalten mit Auslöser und Endgrund sowie Lücke im Protokoll | – |
| DT-07 | Ausschlussliste: Fenster im Vordergrund, im Hintergrund, bewegt, teilverdeckt, neu erschienen; im ausgeschlossenen Fenster tippen und klicken | Bild-Kanarie in keinem Bild; keine Eingaben; kein Fenstertitel, kein Programmname; die Lücke nennt den Anzeigenamen der Regel und **keine Zähler** | – |
| DT-08 | Zwischenablage mit und ohne Kennzeichnung, Inhaltsaufzeichnung ein und aus; Test-Quellanwendung, die ihre Formate erst auf Anforderung erzeugt und jede Anforderung zählt | Regeln Z1–Z7 eingehalten; bei abgeschalteter Inhaltsaufzeichnung keine einzige Anforderung an die Quellanwendung, Kanarie nirgends, Einfügen trotzdem dem richtigen Eintrag zugeordnet | – |
| DT-09 | Schwärzen einer Textstelle und eines Bildbereichs, die in mehreren Schritten vorkommen; Schwärzen eines ganzen Schritts und eines Zeitraums; danach Schritte mit geänderter Regelversion neu berechnen | Kanarie in keinem Leseweg: Viewer-Cache, Suche, Exporte, KI-Paket, alle MCP-Tools. Platzhalter wie festgelegt: `[geschwärzt]`, „Schritt geschwärzt“, Lücke „vom Autor entfernt“, schwarze Fläche. Nach der Neuberechnung ist dasselbe geschwärzt wie vorher | T-08 |
| DT-10 | Bereinigte Kopie und endgültiges Schwärzen für alle vier Arten | Kanarie in keiner Datei der Ergebnis-Sitzung (Bytesuche und Bildvergleich) | – |
| DT-11 | Netzwerk-Mitschnitt über alle Betriebsarten | keine Verbindung, keine Namensauflösung, kein Port | T-12 |
| DT-12 | Alle Startwege der Aufnahme, auch Kommandozeile und nicht interaktive Sitzung | Kein Start ohne sichtbaren Hinweis; nicht interaktiver Start scheitert mit Meldung | R-11 |
| DT-13 | Aufnahmeordner in OneDrive, Nextcloud, Dropbox, auf Netzlaufwerk; Erststart mit der EXE in einem gewöhnlichen, einem schreibgeschützten und einem synchronisierten Ordner | Warnung vor Beginn; vorgeschlagen wird `Sessions` neben der EXE bzw. der Ausweichort unter `%LOCALAPPDATA%`, nie „Dokumente“ | – |
| DT-14 | Sitzung mit eingebetteter Anweisung in Fenstertitel und Webseitentext | Strukturtest und Agententest aus [06, 12.4](06-ki-export-und-mcp.md) | T-09, R-12 |
| DT-15 | Sitzung mit ungeprüftem Klartext aus einem Terminal und einem getippten Zugangsschlüssel in einem normalen Feld über MCP lesen und per Kommandozeile exportieren | `{unreviewed:N}` in Schritten, Rohereignissen, Suche und Export bis zur Entscheidung im Viewer; danach Klartext bzw. `[geschwärzt]`. Die Hilfe von `mcp` und `export` kennt keine Option, die das umgeht | – |
| DT-16 | Diagnoseprotokolle auf höchster Stufe nach einer Aufnahme mit Kanarien | Kanarie in keinem Protokoll | – |
| DT-17 | Arbeitsspeicher des Prozesses nach Ablauf von `T_max` durchsuchen | Kanarie aus maskierten Läufen nicht mehr vorhanden (*Soll*) | – |
| DT-18 | Aufnahmebereich „Eine Anwendung“: außerhalb Kanarie tippen, klicken und scrollen; dabei auch ein Fenster der Ausschlussliste außerhalb des Bereichs bedienen | Kanarie nirgends; Lücke `outOfScope` nennt nur Zahlen je Art (Tasten, Klicks, Mausrad, Bewegungen) – keine Inhalte, Positionen, Einzelzeiten, Titel; für das ausgeschlossene Fenster Lücke `excludedApp` ohne Zähler | T-06 |
| DT-19 | Sitzung mit einem internen Kommentar aus dem Viewer, einem ausgeblendeten Kommentar und einem während der Aufnahme geschriebenen, nachträglich als intern gekennzeichneten Kommentar (Kanarien) | Kanarie in keiner Ausgabe: Anleitungen, KI-Paket in allen Detailstufen, alle MCP-Tools, Suche | – |
| DT-20 | Ziel-Angaben und Marker-Bezeichnungen mit Kanarien, eine davon mit dem Muster eines Zugangsschlüssels; eine Ziel-Angabe im Viewer ändern, eine löschen; je eine Textstelle in einer Angabe und in einer Marker-Bezeichnung schwärzen | Die Durchsicht listet alle. Die auffällige sperrt den Export im Viewer und erscheint über MCP und Kommandozeile als `{unreviewed:N}`. Geschwärzte Stellen, die ursprüngliche Fassung der geänderten und die gelöschte Angabe stehen in keiner Ausgabe – auch nicht im `session.start` der Rohereignisse, in Suche und Vergleich – und in keiner Datei der bereinigten Kopie | – |
| DT-21 | Verallgemeinerung: Kanarien-Benutzername und -Servername in Fenstertiteln, Pfaden, Elementnamen, Feldinhalten, Ziel-Angaben und getippt (einmal mit Korrektur); Regeln aus einem Vorschlag und von Hand, eine mit Muster; eine Regel mit unzulässigem Platzhalternamen von Hand in die Datei geschrieben | Kanarie in keiner Ausgabe und an keinem abgeleiteten Ort: Kopien, Anleitungen, KI-Paket samt `events.jsonl`, alle MCP-Tools, Suche, Vergleich, `cache/`, `export/.tmp/`. Bildflächen mit bekannter Textlage sind überdeckt; Stellen ohne bekannte Lage sind gemeldet. Nach bereinigter Kopie und nach endgültigem Anwenden: Kanarie in keiner Datei – auch nicht in `edits.json`. Der unzulässige Name ergibt `[geschwärzt]`, nie den Wert | T-15 |
| DT-22 | Vorschläge öffnen und verwerfen, ohne einen anzunehmen – in einer Sitzung ohne diese Werte und in einer mit ihnen; danach einen annehmen | Benutzername, Rechnername, Domäne und Profilpfad des Testsystems stehen danach in keiner Datei außerhalb der aufgezeichneten Inhalte: nicht in Manifest, `edits.json`, Einstellungen, Profilen, Caches, Protokollen. Ohne Vorkommen lässt sich kein Vorschlag annehmen. Nach dem Annehmen steht der Wert allein in der Regel | – |
| DT-23 | Suchindex: Sitzung mit geschwärzter Textstelle, maskierter Eingabe, ungeprüfter auffälliger Eingabe, internem und ausgeblendetem Kommentar und verallgemeinertem Wert (Kanarien); Suche in der Sitzungsbibliothek und über MCP mit `scope: "all"`; danach schwärzen, verallgemeinern, durchsehen, einen Kommentar auf intern stellen | Bytesuche in `cache/search/`: keine Kanarie. Suche: kein Treffer und kein Hinweis auf ein Vorkommen. Nach jeder der Änderungen ist der alte Index gelöscht, bevor die Änderung gespeichert ist; eine freigegebene Eingabe ist danach auffindbar. Suchbegriffe stehen in keinem Protokoll | T-08 |
| DT-24 | Profile: Start mit einem Profil, das „Schutznetz“ statt „Streng“ festlegt bzw. den Zwischenablage-Inhalt einschaltet – über Startfenster, Sofortaufnahme, `record --profile` und mit einer `profiles.json` neben der EXE; Versuch, per Profil Hinweis, Privattaste, Durchsicht oder Ausschlusseinträge abzuschalten | Die Startmeldung nennt jede Abweichung, bevor Hooks gesetzt sind, und bei jedem Start; `manifest.profile.weakens` stimmt; die übrigen Versuche bleiben wirkungslos und werden gemeldet; die mitgelieferten Profile schützen nicht schwächer als die Werkseinstellungen | T-17 |
| DT-25 | Import einer präparierten Austauschdatei: Sie senkt die Schutzstufe eines Profils, schaltet den Zwischenablage-Inhalt ein, leert die Ausschlussliste, lässt die Privattaste ohne Kürzel und enthält eine Vorlage mit Skript und Verweis nach außen, einen Netzwerkpfad und übergroße Werte. Geprüft über den Dialog und über `settings import` – ohne `--confirm`, mit `--confirm` und mit `--allow-weaker` | Jede schwächende Änderung steht einzeln und unbestätigt da; ohne Bestätigung bleibt der Schutz unverändert, alles Übrige wird eingelesen. Ohne `--confirm` ändert sich nichts; `--confirm` allein übernimmt keine schwächende Änderung. Kein Netzwerkzugriff, kein befolgter Pfad, keine aktiven Inhalte in Vorlagen. Die Rücknahme stellt den alten Stand her | T-17 |
| DT-26 | Export der Einstellungen mit allen Teilen auf einem Rechner mit Kanarien in Benutzername, Profilpfad, Aufnahmeordner und Sitzungstiteln | Die Kanarie und jeder absolute Pfad fehlen in der Datei; sie enthält genau die angekreuzten Teile; an keinem anderen Ort entsteht eine Kopie | – |
| DT-27 | Support-Paket: Aufnahme mit Kanarien-Passwort im Terminal, einer geschwärzten Bildstelle, einem verallgemeinerten Benutzernamen und vorbelegtem Bezug; Versuch, den Ablauf ohne Durchsicht abzuschließen – auch in einer Sitzung ohne auffällige Eingabe | Ohne bestätigte Durchsicht entsteht kein Paket. Das ZIP enthält nur die HTML-Anleitung und das KI-Paket; Bytesuche (UTF-8, UTF-16, Base64) und Bildvergleich finden keine Kanarie – auch nicht im Dateinamen. Der Arbeitsordner mit der bereinigten Kopie ist danach gelöscht. Netzwerk-Mitschnitt und Prozessüberwachung zeigen keine Verbindung und kein gestartetes Mail- oder Teilen-Programm | T-16 |
| DT-28 | Vergleich: zwei Fassungen einer Sitzung, die sich nur in einem geschwärzten, einem maskierten und einem ungeprüften Inhalt unterscheiden, jeweils gegen dieselbe dritte Sitzung – im Viewer, mit `compare` und mit `compare_sessions` | Die Ergebnisse beider Fassungen sind bytegleich; die betroffenen Merkmale stehen unter `notCompared`; keine Kanarie im Ergebnis; in keiner Sitzung liegt danach ein Vergleichsergebnis | T-14 |
| DT-30 | Tastenereignisse: Eingabe einer Kanarie in einem Terminal (ungeprüft) und einer geschwärzten Textstelle; Ausgabe über MCP und Export per Kommandozeile; endgültiges Schwärzen | In Wegen ohne Oberfläche tragen `key.down` und `key.up` weder Taste noch Scancode; nach dem endgültigen Schwärzen auch nicht in den Rohdaten | – |
| DT-31 | Endgültiges Schwärzen eines Zeitraums mit Bildern; danach die Bilder vor und nach dem Zeitraum prüfen; Bearbeitungsstände vorher anlegen | Bilder des Zeitraums fehlen in den Rohdaten, spätere Bilder sind pixelgleich, `history/` ist leer, die Gegenprobe findet keine Kanarie | – |
| DT-32 | Präpariertes `.steprec`: Pfadsprung, Gerätename, Zip-Bombe, zu viele Einträge, Eintrag größer als angegeben, fremder Blob-Name | Entpacken schlägt fehl, am Ziel und im Zwischenordner bleibt nichts zurück; nichts außerhalb der Sitzung wird gelesen oder geschrieben | B11 |
| DT-33 | Sicherer Desktop: Abfrage der Benutzerkontensteuerung mit getipptem Kanarien-Text, Sperren, Ruhezustand | Keine Bilder und keine Tasten aus der Lücke, kein schwarzes Bild im Ringpuffer; danach `keystate.sync` und Schlüsselbild | B24 |
| DT-34 | Agent-Anfragen: Aufruf ohne und mit `--allow-requests`; zu kurzes und zu langes Ziel, Steuerzeichen, unbekannter Parameter; mehr als eine offene Anfrage, Abstand, sechste Anfrage in einer Stunde; Eingabetaste im Dialog; Ablehnen, „15 Minuten“, Ablauf der Frist | Ohne Schalter sind die Werkzeuge nicht gelistet; ungültige Anfragen werden abgelehnt; Rate-Limits greifen; die Eingabetaste löst nichts aus; der Agent erfährt nur den Zustand und die Sitzungskennung | B23 |
| DT-35 | Diagnose-Paket und Protokoll nach einer Aufnahme mit Kanarien in Texten, Titeln und Argumenten | Kanarie, Benutzername, Rechnername und Pfade fehlen in Paket und Protokoll | DT-16 |
| DT-36 | Papierkorb: Verwerfen und Löschen auf einem Laufwerk mit und ohne Papierkorb | Mit Papierkorb liegt der Ordner dort; ohne ihn bleibt er ohne ausdrückliche Zustimmung erhalten | – |
| DT-29 | Ersetzen: einen Abschnitt mit Kanarie neu aufnehmen und ersetzen, ohne zu schwärzen; danach die ersetzten Schritte schwärzen. Außerdem eine weitere Aufnahme mit einer zweiten Kanarie beginnen und verwerfen | Der Viewer sagt beim Ersetzen, dass die alten Schritte erhalten bleiben, und bietet das Schwärzen an. Die Kanarie fehlt in der Anleitung und in allen Dateien eines KI-Pakets der Stufen „Kompakt“ und „Standard“; sie steht – wie dokumentiert – in „Vollständig“ und über MCP, bis geschwärzt wird, danach nirgends. Die Durchsicht erfasst auffällige Eingaben in ersetzten Schritten weiterhin. Die Kanarie des verworfenen Abschnitts steht in keiner Ausgabe, keinem Cache, keinem Suchindex und keiner bereinigten Kopie – auch nicht in „Vollständig“ und über MCP; im Journal der Ursprungssitzung steht sie weiter, bis endgültig geschwärzt wird | – |

## 18. Bezug zum Architektur-Review

| Befund | Lösung in diesem Dokument |
|---|---|
| Nr. 1 – Rückhaltung gibt Tasten gegen veralteten Fokus frei | Fenster-Stempel im Hook-Zeitpunkt, Fokus-Epochen, Freigabe nur bei übereinstimmender Probe davor und danach **und** nachgewiesener Ankunft der Tasten im Feld (deckt auch träge Anwendungen ab), Zeitlimit führt zum Maskieren ([5.2](#52-begriffe)–[5.6](#56-rangfolge-der-entscheidung)); Prüfung DT-03, DT-04 |
| Nr. 2 – „Passwörter erreichen nie die Festplatte“ gilt nicht, wo es kein Passwort-Kennzeichen gibt | Zusicherung auf *erkannte* Passwortfelder begrenzt und offen benannt ([Abschnitt 1](#1-zweck-und-geltungsbereich)); Trennung `opaque`/`unresolved`; Stufe „Streng“; Terminal-Erkennung; Passwortmanager-Erkennung; „einmal Passwort, immer Passwort“ mit Bildschwärzung; Feldinhalte und Schnappschuss-Werte durch dasselbe Tor ([4](#4-schutzstufen), [5.8](#58-einmal-passwort-immer-passwort)–[5.11](#511-eingaben-von-passwortmanagern), [6](#6-passwortabfragen-im-terminal)). Abweichend vom Vorschlag des Reviews bleibt Klartext in Terminals und Remote-Fenstern Standard – so entschieden, mit Schutznetz. |
| Nr. 3 – Schwärzungen liegen im Schritt-Overlay, Leser erreichen Rohdaten | Anker an Ereignisnummern und Bild-Kennungen – auch „Schritt schwärzen“ wird beim Anlegen auf Ereignisnummern aufgelöst –, Ausdehnung auf alle Vorkommen, eine Leseschicht für alle Wege mit festen Platzhaltern, Verwerfen der Caches, endgültiges Schwärzen und bereinigte Kopie ([Abschnitt 10](#10-schwärzungsmodell)); die Durchsicht auffälliger Eingaben lässt sich für keinen Weg abschalten ([11.1](#111-durchsicht-aller-eingaben)) |
| Nr. 7 – Text-Schnappschüsse als neue Datenquelle | Werte von Eingabefeldern in Schnappschüssen folgen der Schutzstufe; Passwortfelder aus Schnappschüssen werden registriert ([4.2](#42-verhalten-je-stufe), [5.8](#58-einmal-passwort-immer-passwort)) |
| Nr. 8 – „Kein Netzwerk, überprüfbar“ und nicht vertrauenswürdige Zeichenketten | keine Browser-Komponente, Regeln NW1–NW8, dreistufiger Nachweis ([Abschnitt 12](#12-netzwerkfreiheit)); Prompt-Injection und Markup als B8 und B12 im Bedrohungsmodell |
| Nr. 14 – Verifikation nur für den einfachen Weg | Kanarien-Prüfungen für Text und Bild, Wettlauf-Fuzzing, Netzwerk-Mitschnitt mit Kindprozessen, Schwärzung über alle Lesewege, Prüfungen DT-01 bis DT-29 ([Abschnitt 17](#17-prüfungen)) |
| Fehlend – Erfassungsumfang (Benachrichtigungen, Alt+Tab-Miniaturen, zweiter Monitor) | Benannte Grenzen mit Ausbaustufen ([8.5](#85-bekannte-grenzen)); Aufnahmebereich „eine Anwendung“ als stärkere Alternative; festgelegt, was von der Arbeit außerhalb des Bereichs bleibt – Zähler ja, Inhalte nie – und dass eine ausgeschlossene Anwendung nicht einmal Zähler hinterlässt ([8.6](#86-die-stärkere-alternative)) |
| Fehlend – Sitzungsordner in OneDrive/Nextcloud | Erkennung und Warnung vor der Aufnahme; Standardordner `Sessions` neben der EXE nur, wenn er in keinem Sync-Ordner liegt, sonst lokal unter `%LOCALAPPDATA%`; nie „Dokumente“ ([13.1](#131-sync-ordner-und-netzlaufwerke)) |

## 19. Anforderungen

| ID | Anforderung | Priorität | Abnahmekriterium |
|---|---|---|---|
| DS-001 | Aufnahme nur nach ausdrücklichem Start; auf jedem aufgenommenen Monitor ist durchgehend ein Hinweis sichtbar; er lässt sich nicht abschalten. | Muss | DT-12; im Ausweichmodus fehlt der Hinweis je Bild höchstens 50 ms |
| DS-002 | Kein Autostart, kein Dienst, kein Fernstart, kein Start in einer nicht interaktiven Sitzung. | Muss | System-Diff ohne Autostart-Spuren; Startversuch aus einer Dienstsitzung scheitert |
| DS-003 | Beim Sperren, bei Benutzerwechsel und beim Trennen einer Remote-Sitzung pausiert die Aufnahme und läuft nur nach Bestätigung weiter; eine Einstellung dagegen gibt es nicht. | Muss | Sperren während der Aufnahme: Lücke `sessionLocked` für die Dauer der Sperre, danach `paused` bis zur Bestätigung; ohne Bestätigung steht nach dem Entsperren keine Eingabe und kein Bild im Protokoll |
| DS-004 | Beim Start erscheint zwei Sekunden lang die Startmeldung mit Schutzstufe. | Soll | Sichtprüfung |
| DS-005 | Keine Code-Injektion, kein Lesen fremden Prozessspeichers, keine einschleusenden Hooks. | Muss | Code-Review-Regel; Prüfung der genutzten Schnittstellen im Build |
| DS-006 | Manifest und Protokoll enthalten standardmäßig keine identifizierenden Angaben des Rechners oder Kontos; nur die ausdrückliche Einstellung `privacy.recordIdentity` ändert das. Programmpfade im Benutzerprofil stehen mit Platzhalter. | Muss | Standardeinstellungen: Suche nach Rechner- und Benutzername in einer Sitzung ohne solche Bildschirminhalte ohne Treffer |
| DS-007 | Schutzstufen „Schutznetz“ (Standard) und „Streng“, wählbar je Aufnahme, vermerkt im Manifest und in jeder Ausgabe. | Muss | Beide Stufen aufgezeichnet; Kopf von `trace.md` nennt die Stufe |
| DS-008 | Das Verhalten je Einstufung und Stufe entspricht der Tabelle in 4.2. | Muss | Testreihe über alle Zeilen der Tabelle in beiden Stufen |
| DS-009 | In „Schutznetz“ sind Hinweis in der Leiste, Privattaste und Pflicht-Durchsicht nicht abschaltbar. | Muss | Einstellungen bieten keine Abschaltung; Sichtprüfung |
| DS-010 | Jede Taste trägt einen Fenster-Stempel aus dem Hook-Zeitpunkt. | Muss | Fensterwechsel mitten im Tippen: Zuordnung jeder Taste zum richtigen Fenster |
| DS-011 | Rückhaltung nur im Arbeitsspeicher, höchstens `T_max`; ohne Entscheidung wird maskiert. | Muss | Element-Ermittlung im Test blockiert: alle Läufe maskiert, keiner im Klartext |
| DS-012 | Klartext nur nach der Freigaberegel 5.5. | Muss | DT-03, DT-04 |
| DS-013 | Die Rangfolge 5.6 entscheidet; der Grund steht im Protokoll. | Muss | Je Rang ein Testfall mit erwartetem Grund |
| DS-014 | Maskierte Läufe speichern nur Anzahl, Grund, Zeitraum und die Eingabenummern für die Buchführung – keine Tastencodes, keine Zeichen, keine Einzelzeiten. Das Speichern der Anzahl ist abschaltbar. | Muss (abschaltbare Anzahl: Kann) | Prüfung der Einträge `text.masked`; DT-01 |
| DS-015 | Kein Kanal umgeht die Entscheidung: Raw Input, Feldinhalte, Wertereignisse, Text-Schnappschüsse, Zwischenablage, Bilder. | Muss | DT-01, DT-02 mit Suche in allen Ereignisfamilien |
| DS-016 | Ein einmal als Passwortfeld erkanntes Element bleibt es für die Sitzung. | Muss | DT-02 |
| DS-017 | Passwortfelder werden in jedem Bild vor dem Speichern geschwärzt; Bilder werden dafür zurückgehalten; ist das Rechteck eines offengelegten Felds nicht ermittelbar, wird das Fenster geschwärzt. | Muss | DT-02; Rückfall im Test erzwungen |
| DS-018 | Sensible Felder nach Namensliste werden wie Passwortfelder behandelt. | Soll | Felder „PIN“, „CVC“, „Einmalcode“ in der Test-Zielanwendung: maskiert |
| DS-019 | Serien aus Passwortmanagern werden erkannt und maskiert. | Soll | Auto-Type von KeePass in ein nicht erkennbares Feld: maskiert |
| DS-020 | Rückhaltepuffer und gemeinsamer Speicher: anonym, nicht dateigestützt, gegen Auslagerung gesperrt, nach Gebrauch genullt; Protokolle ohne Tasteninhalte. | Muss (Ausnahme von Absturzabbildern: Soll) | DT-16, DT-17 |
| DS-021 | Die Rückhaltung verzögert nie die Eingabe selbst. | Muss | VE-08: keine messbare zusätzliche Eingabeverzögerung |
| DS-022 | **Umgesetzt; Abdeckung im echten Terminal noch zu bestätigen** (O-09, [6](#6-passwortabfragen-im-terminal)). Passwortabfragen in Terminals mit Textzugriff werden nach T1 erkannt; T2 und T3 ergänzen. Jede erkannte Abfragezeile wird selbsttätig bis zur Eingabetaste maskiert – ohne Zutun des Nutzers und bei mehreren Abfragen hintereinander jede Zeile einzeln. Daneben gelten weiter DS-068 (Durchsichtspflicht für nicht maskierte Eingaben) und DS-069 („Streng“). | Muss (T1), Soll (T2, T3) | DT-05; Regeltabelle mit allen Beispielen aus 6.2 als Unit-Test |
| DS-023 | **Umgesetzt.** Vom Terminaltext wird nur die erkannte Abfragezeile gespeichert (höchstens 120 Zeichen, `text.masked.promptLine`), zusammen mit der zutreffenden Regel (`rule`); alles übrige Gelesene bleibt im Arbeitsspeicher. | Muss | Prüfung des Protokolls nach DT-05 |
| DS-024 | Privattaste als Umschalter **ohne automatisches Ende**: wirkt garantiert ab dem nächsten Tastendruck, maskiert zusätzlich noch zurückgehaltene Tasten des laufenden Laufs, endet nur durch erneutes Drücken oder Stopp, zeigt ihren Zustand und erinnert nach der eingestellten Zeit. Sie gilt unabhängig von der Terminal-Erkennung und kann deren Maskierung nicht aufheben. | Muss | DT-06 |
| DS-025 | „Letzte Eingabe schwärzen“ legt während der Aufnahme sofort eine Zeitraum-Schwärzung über den letzten Tastenlauf an und wendet sie beim Stopp endgültig an. | Soll | Kanarie tippen, Befehl auslösen: sofort in keiner Ausgabe (Leiste, mitlaufende Anzeige); nach dem Stopp in keiner Datei des Sitzungsordners (Bytesuche); nach erzwungenem Absturz und Wiederherstellung ebenso |
| DS-026 | Freigabetaste in der Stufe „Streng“ (nicht umgesetzt). | Soll | Klartext nur im freigegebenen Fenster und nur bis zum Fensterwechsel |
| DS-027 | Ausschlussregeln nach Programm, Pfad, Titel, Klasse mit Anzeigenamen; Vorbelegung für Passwortmanager und private Browserfenster. | Muss | Regel anlegen, prüfen; Vorbelegung gegen aktuelle Programmversionen getestet |
| DS-028 | Wirkung der Ausschlussliste nach 8.3. Der Lückenmarker einer ausgeschlossenen Anwendung nennt Zeitraum und Anzeigenamen der Regel – keine Ereigniszahlen, keinen Programmnamen, keinen Titel. | Muss | DT-07 |
| DS-029 | Ausschluss entscheidet über den Fenster-Stempel; Bildmasken berücksichtigen Fensterbewegung. | Muss | DT-07 mit Fensterwechsel während des Tippens und bewegtem Fenster |
| DS-030 | Schwärzung von Umschalt- und Vorschauflächen; Erkennung von Inkognito-Fenstern; Schwärzung von Benachrichtigungen. | Soll, Soll, Kann | je ein Testfall |
| DS-031 | Zwischenablage nach den Regeln Z1–Z7. | Muss | DT-08 |
| DS-032 | Vier Schwärzungsarten: Bereich im Bild, Textstelle, Schritt und Zeitraum. Alle sind an Ereignisnummern und Bild-Kennungen verankert, nie an Schrittnummern; „Schritt“ wird beim Anlegen auf die Ereignisnummern des Schritts aufgelöst. | Muss | Je Art eine Schwärzung anlegen, Schritte mit geänderter Regelversion neu berechnen: alle Schwärzungen wirken unverändert, auch wenn der geschwärzte Schritt danach anders geschnitten ist (DT-09) |
| DS-033 | Bildschwärzungen gelten automatisch für den Gültigkeitsbereich; „überall schwärzen“ für Text legt Anker für alle Fundstellen an, ohne den Begriff zu speichern. | Muss | DT-09; Suche nach dem Begriff in `edits.json`: kein Treffer |
| DS-034 | Fenster- und Elementbezug, Bildschirmstelle auf dem ganzen Monitor, Suche nach Wiederauftreten, Bildvorschläge zu Textfundstellen. | Soll | Testfälle je Mechanismus |
| DS-035 | Leseschicht-Regeln L1–L8. | Muss | T-08, DT-09, DT-15, DT-19; Build-Regel: nur die Leseschicht öffnet Rohdateien |
| DS-036 | Feste Platzhalter in allen Ausgaben: Textstelle `[geschwärzt]` (englisch `[redacted]`), ganzer Schritt „Schritt geschwärzt“ („Step redacted“), Zeitraum als Lücke „vom Autor entfernt“, Bildbereich als schwarz gefüllte Fläche. Keine Längenangabe, kein frei wählbarer Ersatztext; Verpixelung und Unschärfe nicht aus Originalpixeln. | Muss | DT-09; zwei verschieden lange geschwärzte Texte ergeben dieselbe Ausgabe; Bildvergleich zeigt keine Abhängigkeit vom Original außer der Durchschnittsfarbe |
| DS-037 | Bei jeder Änderung an Schwärzungen, Verallgemeinerungen oder am Stand der Durchsicht werden Caches verworfen und frühere Exporte der Sitzung gemeldet. | Muss | DT-09: Vorschaubild und Suchindex nach Schwärzung ohne Kanarie; DT-23 für Verallgemeinerung und Durchsicht |
| DS-038 | Bereinigte Kopie und endgültiges Schwärzen mit Gegenprobe. | Soll | DT-10 |
| DS-039 | Durchsicht aller Eingaben einschließlich der Kommentare des Autors; auffällige Eingaben (aus Klartext-Fenstern oder mit Mustertreffer) verlangen eine Einzelentscheidung, bevor **aus dem Viewer** exportiert werden kann; der Stand wird je Eingabe gespeichert. Die Durchsicht lässt sich nicht abschalten. | Muss | Export aus dem Viewer mit ungeprüfter auffälliger Eingabe gesperrt (Anleitung, KI-Paket, bereinigte Kopie); nach fortgesetzter Aufnahme und nach einem neueren Stand der Muster sind nur die neuen Eingaben ungeprüft; die Einstellungen bieten keine Abschaltung. Wege ohne Oberfläche sind nicht gesperrt und liefern Zähler (DS-040) |
| DS-040 | Wege ohne Oberfläche (MCP-Server, Export per Kommandozeile) erhalten ungeprüfte auffällige Eingaben nur als Zähler `{unreviewed:N}`, bis sie im Viewer durchgesehen sind. Es gibt keine Option, die das aufhebt. | Muss | DT-15 |
| DS-041 | Musterprüfung mit den Mustern aus 11.2; treffsichere Muster in Eingaben machen diese auffällig, alle übrigen Treffer sind Hinweise. | Soll | Sitzung mit je einem Beispiel je Muster: alle gemeldet; nur die treffsicheren sperren den Export |
| DS-042 | Netzwerkregeln NW1–NW8. | Muss | T-12, DT-11 |
| DS-043 | Build-Prüfung auf Netzwerk-Schnittstellen und Abhängigkeiten. | Soll | Build schlägt fehl, wenn eigener Code eine Netzwerk-Schnittstelle verwendet |
| DS-044 | Erkennung von Sync-Ordnern und Netzlaufwerken mit Warnung vor der Aufnahme. Standardordner ist `Sessions` neben der EXE, sofern beschreibbar und in keinem erkannten Sync-Ordner, sonst `%LOCALAPPDATA%\StepRecorder\Sessions`; „Dokumente“ wird nie vorgeschlagen. | Muss | DT-13 |
| DS-045 | „Lokal aufnehmen, nach der Durchsicht verschieben“. | Soll | Ablauf mit Nextcloud-Ordner: während der Aufnahme keine Datei im Sync-Ordner |
| DS-046 | Kennzeichen „Inhalt nicht indizieren“ am Sitzungsordner; keine Einträge in „Zuletzt verwendet“. | Soll | Windows-Suche findet getippten Text nicht; Liste unverändert |
| DS-047 | Keine Kopien außerhalb des Sitzungsordners; Zwischendateien liegen unter `export/.tmp/` der Sitzung, nie in `%TEMP%`, enthalten nur die geschwärzte Sicht und werden entfernt, auch nach Absturz. | Muss | System-Diff nach Aufnahme, Kopieren, Herausziehen, externem Bearbeiten, Exportvorschau, Absturz und erneutem Öffnen: keine Datei mit Sitzungsinhalt außerhalb des Sitzungsordners, `export/.tmp/` leer |
| DS-048 | Viewer, Exporte und MCP-Server laufen nie mit erhöhten Rechten; fremde Sitzungen werden erhöht nicht geöffnet. | Muss | Start erhöht: Betriebsarten verweigern mit Meldung |
| DS-049 | Startfenster und Handbuch nennen Zusicherungen, Grenzen und die rechtlichen Hinweise. | Muss | Sichtprüfung gegen Abschnitt 1 und 15.5 |
| DS-050 | Releases sind signiert und mit Zeitstempel versehen; Prüfsummen werden veröffentlicht. | Muss (für 1.0) | Signaturprüfung der Release-Dateien |
| DS-051 | Keine Verschleierung, kein Laufzeitpacker; keine Verankerung im System. | Muss | Build- und System-Diff-Prüfung |
| DS-052 | Dokument „Was das Programm technisch tut“; Meldeweg und Vorlage für Fehlalarme. | Soll | Dokumente vorhanden und aktuell zum Release |
| DS-053 | Kurzbeschreibung als Vorlage für Datenschutz und Betriebsrat. | Kann | – |
| DS-054 | Eine Lücke „außerhalb des Aufnahmebereichs“ darf die Zahl der nicht aufgezeichneten Ereignisse je Art nennen (Tasten, Klicks, Mausrad, Bewegungen) – nur Zahlen, nie Inhalte, Tastencodes, Positionen, Einzelzeiten oder Titel. Eine Lücke „ausgeschlossene Anwendung“ nennt keine Zähler. | Muss | DT-18, DT-07 |
| DS-055 | Als intern gekennzeichnete und ausgeblendete Kommentare des Autors erscheinen in keiner Ausgabe. | Muss | DT-19 |
| DS-056 | Ziel-Angaben (Ziel der Aufnahme, erwartetes Ergebnis, Bezug) und Bezeichnungen der Marker sind Autorentext: Sie gehören zur Durchsicht, lassen sich schwärzen und unterliegen Verallgemeinerungen (L10). Ausgaben zeigen von den Ziel-Angaben nur die wirksame Fassung; eine geänderte oder gelöschte Angabe erscheint in keiner Ausgabe und in keiner bereinigten Kopie mehr im Original. | Soll (Schutzregel: Muss bei Umsetzung) | DT-20 |
| DS-057 | Verallgemeinerungen wirken über die Leseschicht in jeder Ausgabe (V1–V4, L9): fester Platzhalter `{Name}`, nach Schwärzung und Zurückhaltung, einschließlich der Tasten des Werts. Die Originalwerte zeigt allein eine Anzeige im Viewer; sie kopiert und exportiert nichts. | Soll (Schutzregel: Muss bei Umsetzung) | DT-21; die Hilfe von `export` und `mcp` kennt keine Option für Originalwerte |
| DS-058 | In Bildern wird nur überdeckt, wo die Textlage bekannt ist; jede Fundstelle ohne bekannte Lage wird gemeldet – beim Anlegen der Regel, in der Qualitätsprüfung und in den Ausgaben für KI (V5). | Soll (Schutzregel: Muss bei Umsetzung) | DT-21: Sitzung mit dem Wert in einem Elementnamen und in einer Terminalausgabe – Element überdeckt, Terminalstelle gemeldet |
| DS-059 | Dauerhaft entfernt wird ein verallgemeinerter Wert erst durch bereinigte Kopie oder endgültiges Anwenden; dabei verliert die Regel ihren Wert, und die Gegenprobe sucht nach jedem verallgemeinerten Wert (V6). Bei Werten, die nach einem Geheimnis aussehen, rät der Dialog zur Schwärzung (V7). | Soll (Schutzregel: Muss bei Umsetzung) | DT-21: Bytesuche in allen Dateien der Ergebnis-Sitzung einschließlich `edits.json` |
| DS-060 | Vorschläge für Benutzername, Rechnername, Domäne und Benutzerprofilpfad entstehen aus Laufzeitwerten, lassen sich nur bei einem Vorkommen in der Sitzung annehmen und werden nicht gespeichert; gespeichert wird allein eine angenommene Regel. | Soll (Schutzregel: Muss bei Umsetzung) | DT-22 |
| DS-061 | Der Suchindex je Sitzung entsteht nur aus der Sicht, die die Leseschicht einem Weg ohne Oberfläche gäbe: nie geschwärzte, maskierte oder ungeprüfte Inhalte, nie interne oder ausgeblendete Kommentare, nie Originalwerte von Verallgemeinerungen. Er wird bei jeder Änderung dieser Sicht verworfen, bevor die Änderung gespeichert ist, und nie in `.steprec` gepackt. | Soll (Schutzregel: Muss bei Umsetzung) | DT-23 |
| DS-062 | Kein Aufnahmeprofil senkt den Schutz unbemerkt: Was als schwächer gilt, legt 4.4 fest; die Startmeldung nennt jede Abweichung vor Aufnahmebeginn und bei jedem Start; `manifest.profile.weakens` hält sie fest. Profile können Hinweis, Privattaste, Durchsicht und Rückhaltung nicht abschalten und die Ausschlussliste nur ergänzen; mitgelieferte Profile schützen nicht schwächer als die Werkseinstellungen. | Soll (Schutzregel: Muss bei Umsetzung) | DT-24 |
| DS-063 | Die Austauschdatei für Einstellungen enthält keine Sitzungsdaten, keine absoluten Pfade und keine Namen von Rechner, Benutzer oder Domäne; sie entsteht nur auf Befehl, und der Dialog zeigt ihre Teile. | Soll (Schutzregel: Muss bei Umsetzung) | DT-26 |
| DS-064 | Der Import behandelt die Austauschdatei als fremde Eingabe (IM1–IM10): nur auf Befehl, Vorschau vor jeder Änderung, Einzelbestätigung jeder Schutz schwächenden Änderung; auf der Kommandozeile ändert sich nichts ohne `--confirm` und keine schwächende Einstellung ohne `--allow-weaker`; Pfade werden nie befolgt. | Soll (Schutzregel: Muss bei Umsetzung) | DT-25 |
| DS-065 | Das Support-Paket entsteht nur nach bestätigter Durchsicht, nur aus einer bereinigten Kopie mit bestandener Gegenprobe und enthält genau die HTML-Anleitung und das KI-Paket; das Programm versendet nichts, und es gibt keinen Weg ohne Oberfläche (SP1–SP9). | Soll (Schutzregel: Muss bei Umsetzung) | DT-27 |
| DS-066 | Suche über alle Sitzungen und Vergleich zweier Sitzungen lesen jede Sitzung getrennt über die Leseschicht; Zurückgehaltenes wird weder gefunden noch verglichen, und kein Ergebnis hängt von einem verborgenen Inhalt ab (L11). | Soll (Schutzregel: Muss bei Umsetzung) | DT-23, DT-28 |
| DS-067 | Ersetzen ist keine Schwärzung: Der Viewer sagt das beim Ersetzen und bietet an, die alten Schritte zu schwärzen. Ersetzte Schritte unterliegen weiter Durchsicht, Schwärzung und Verallgemeinerung; ihr Inhalt erscheint nur in der ausführlichsten Detailstufe des KI-Pakets und über den MCP-Server. Ein weiterer Aufnahmeabschnitt beginnt sichtbar (P1) und meldet schwächeren Schutz wie ein Profil. Ein verworfener weiterer Aufnahmeabschnitt bleibt in den Rohdaten, erscheint aber in keiner Ausgabe und in keinem abgeleiteten Datenbestand (L12); dauerhaft entfernt wird er durch bereinigte Kopie oder endgültiges Schwärzen. | Soll (Schutzregel: Muss bei Umsetzung) | DT-29 |
| DS-068 | Getipptes in Terminal-, Remote-Desktop- und VM-Fenstern ist durchsichtspflichtig (`flags.plaintext`): Es erreicht Wege ohne Oberfläche nur als Zähler, sperrt Exporte aus dem Viewer und braucht eine Einzelentscheidung; die Durchsicht bietet „Übrige Klartext-Eingaben: in Ordnung“ für die Eingaben dieser Fenster, nicht für Musterfunde. | Muss | DT-15 |
| DS-069 | Die Stufe „Streng“ maskiert Getipptes in Terminal-, Remote-Desktop- und VM-Fenstern und in nicht einstufbaren Fenstern (`strictPlaintext`, `strictUnknown`). | Muss | Kanarie im Terminal, in einer RDP-Sitzung und einer VM in der Stufe „Streng“: nirgends |
| DS-070 | Das `key.up` ungeprüfter oder geschwärzter Eingaben verrät keine Taste (L13); „Endgültig schwärzen“ nimmt `key.up` und verknüpfte Ereignisse mit. | Muss | DT-30 |
| DS-071 | „Endgültig schwärzen“ und „Letzte Eingabe schwärzen“ entfernen die Bilder der geschwärzten Zeiträume aus den Rohdaten und leeren `history/`; die bereinigte Kopie enthält Ereignisse und Bilder nur aus der Sicht der Leseschicht und besteht eine Pflicht-Gegenprobe über alle Textdateien. | Muss | DT-10, DT-31 |
| DS-072 | Das Entpacken einer `.steprec`-Datei erfolgt Eintrag für Eintrag nach Positivliste, mit Grenzen für Zahl, Größe und Kompressionsverhältnis, über einen Zwischenordner; ein präpariertes Archiv hinterlässt nichts. Gepackt werden nie `cache/`, `export/`, `history/` oder Sperrdateien. | Muss | DT-32 |
| DS-073 | Verwerfen und Löschen von Aufnahmen gehen über den Papierkorb; ohne Papierkorb wird nur nach ausdrücklicher Rückfrage endgültig gelöscht, nie still. | Muss | DT-36 |
| DS-074 | Sicherer Desktop, Sperre und Ruhezustand: keine Bilder und kein schwarzes Bild im Ringpuffer, Lücke `secureDesktop` mit Art, danach `keystate.sync` und Schlüsselbild (2.1). | Muss | DT-33 |
| DS-075 | Agent-Anfragen nur mit `--allow-requests`; Text ungeprüft gekennzeichnet; kein Standardknopf; Ratenbegrenzung (eine offene, 60 s Abstand, fünf je Stunde, 15 Minuten Sperre); keine Fernsteuerung (A1–A7). | Muss | DT-34 |
| DS-077 | Ein geschützter Wert erscheint in keiner Ausgabe, auch wenn Auszeichnung ihn teilt, in einen Link legt oder über Listenpunkte verteilt (L14). | Muss | `RichProtectionTests` über HTML, Markdown, Word, PDF, PowerPoint, Klick-Tour und Fehlerbericht |
| DS-076 | Das Programmprotokoll und das Diagnose-Paket enthalten keine Inhalte (13.5). | Muss | DT-16, DT-35 |

**Stand der Umsetzung (Funktionsrunde):** Umgesetzt sind unter anderem DS-025 (Letzte Eingabe schwärzen), DS-038 (bereinigte Kopie und endgültiges Schwärzen mit Gegenprobe), DS-039 bis DS-041 (Durchsicht, Zähler, Musterprüfung), DS-055, DS-057 bis DS-059 (Verallgemeinerung), DS-061 (Suchindex) sowie DS-068 bis DS-076. Für die übrigen Zeilen gilt die jeweilige Beschreibung im Text; wo ein Abschnitt „nicht umgesetzt“ sagt, ist das maßgeblich. DS-022 und DS-023 (Terminal-Erkennung nach den Regeln T1 bis T3) sind umgesetzt; ihre Abdeckung in echten Terminals ist noch durch den Versuch O-09 zu bestätigen. **Nicht umgesetzt** sind DS-026 (Freigabetaste) und DS-050 (Signierung).

Die Priorität „Soll (Schutzregel: Muss bei Umsetzung)“ bei DS-056 bis DS-067 heißt: Die Funktion selbst ist *Soll*. Sobald sie gebaut wird, ist die Schutzregel der Zeile *Muss* – keine dieser Funktionen wird ohne sie ausgeliefert.

## 20. Annahmen und offene Punkte

**Annahmen über andere Dokumente**

| Dokument | Annahme |
|---|---|
| [03](03-aufzeichnungskern.md) | Abgeglichen mit den Andockpunkten A1–A7. Vorausgesetzt: Fenster-Stempel im Hook-Zeitpunkt; Fokusereignisse mit Ereigniszeit und Elementbezug; dauerhafte Laufzeitkennung von Elementen; Kennzeichen „von Programm erzeugt“; laufend geführte Fensterliste mit Rechtecken; Bilder werden vor dem Schreiben bis zu `T_max` zurückgehalten; Textzugriff auf Terminals über UI Automation; anonymer gemeinsamer Speicher zwischen Capture-Host und Hauptprozess; Eingaben außerhalb des Aufnahmebereichs werden gezählt und verworfen. **Abweichungen, in 03 nachzuziehen:** (1) 03 protokolliert die Terminal-Zeile nicht (A6); nach [6.3](#63-was-gespeichert-wird) und 04 trägt der maskierte Lauf Regel und Abfragezeile. (2) Die Terminal-Erkennung schaltet den Privatmodus je Abfragezeile selbst ein und aus ([6.2](#62-regeln)). (3) Für ausgeschlossene Anwendungen dürfen keine Ereignisse gezählt werden – auch nicht in Leistungs- oder Diagnosezählern je Fenster ([8.3](#83-wirkung)). **Schlussabgleich:** Ein verworfener weiterer Aufnahmeabschnitt wird nicht zurückgenommen, sondern nur markiert; das Journal wird nie gekürzt, und die Leseschicht hält ihn aus allen Ausgaben heraus (L12). 03 (1.7, AUF-108) beschreibt noch das Kürzen – dort und in 04 ist die Kennzeichnung nachzutragen, ebenso die Behandlung beim endgültigen Schwärzen ([10.8](#108-endgültiges-schwärzen)). |
| [04](04-datenformat.md) | Abgeglichen mit dem Stand vom 2026-11-11: `text.masked` mit `count`, `reason` (einschließlich `unresolved`, `sensitiveField`, `autoType`, `loginWindow`), `rule`, `promptLine`, Eingabenummern; `settings.privacy` (`level`: `balanced`/`strict`, `storeMaskedCount`, `recordIdentity`, `clipboardContent`, `excluded`); `system`-Ereignis `privacyMode` mit `trigger` und `rule`; `gap.cause` und `gap.detail` (`rule` bei `excludedApp`, `counts` bei `outOfScope`); `window.info.excludedRule`, `titleWithheld`; `frame.masks` und schwarz gefüllte Flächen; `ctx` und `ctxSrc`; Gründe für zurückgehaltene Inhalte (`withheld`); Schwärzungen (`image` mit `areas` und `rule.kind`, `text` mit `targets`, `range` mit `origin` und `anchor`), feste Platzhalter, endgültiges Schwärzen und bereinigte Kopie (`redactedDestructive`, `sanitizedCopy`, `applied`); `review` (`inputs`, `confirmed`); `flags.plaintext`; `comments[].kind: "internal"`. Die früher hier genannten Ergänzungswünsche sind in 04 umgesetzt, ebenso der Endgrund des Privatmodus (`endReason`), der Kanal `fieldValues` und die Zählung in `{unreviewed:N}`. **In 04 zu prüfen:** (1) ohne Inhaltsaufzeichnung kein Inhaltsvergleich der Zwischenablage – `sameAs` also nur bei eingeschalteter Inhaltsaufzeichnung ([Abschnitt 9](#9-zwischenablage)); (2) die Einstellung `scope.outside: "record"` entfällt ([8.6](#86-die-stärkere-alternative)); (3) `endReason: "stop"` auch für die erkannte Passwortabfrage, wenn die Aufnahme währenddessen endet ([6.2](#62-regeln)). **Funktionsrunde:** abgeglichen sind `manifest.goal` und `edits.goal`, `manifest.profile` mit `weakens`, `edits.substitutions[]` (Platzhaltername, Muster ohne Zurückverfolgung, `suggestion`, `images.unresolved`, endgültiges Anwenden entfernt `match`), `edits.replacements[]`, `edits.qualityIgnores[]` und der Suchindex `cache/search/`. **Dort nachzutragen:** (4) `review.inputs[].kind` kennt keine Marker-Bezeichnung – sie gehört nach [11.1](#111-durchsicht-aller-eingaben) zur Durchsicht; (5) die bereinigte Kopie schreibt Ziel-Angaben nur in der wirksamen Fassung, auch im `session.start`; (6) die Leseschicht gibt bei einem unzulässigen Platzhalternamen `[geschwärzt]` aus (V2). |
| [05](05-schritterkennung.md) | Abgeglichen: Belegstufe „belegt“ für Texteingaben verlangt den bestätigenden Feldinhalt (`valueConfirmed`); zurückgehaltene Eingaben werden eigene Schritte der Belegstufe „unbekannt“ und stehen als `maskedInput` in der Lückenliste; Privatmodus-Zeiträume mit maskierten Eingaben bilden keinen eigenen Schritt; `flags.plaintext` für Klartext aus Klartext-Fenstern; ein endgültig geschwärzter Zeitraum wird zur Lücke `removedByAuthor`. |
| [02](02-architektur.md) | Leseschicht als einziger Zugang zu Rohdateien, auch für die Zähler-Ausgabe ungeprüfter Eingaben (dort ARC-049); Standard-Aufnahmeordner (dort ARC-050); sichere Suchreihenfolge für Bibliotheken; keine Netzwerk-Pakete; PDF ohne Browser-Komponente. **Funktionsrunde:** abgeglichen sind Aufnahmeprofile samt mitgegebener `profiles.json` (12.5), Austauschdatei und Import mit `--confirm` und `--allow-weaker` (12.6, 8.8), Ablauf des Support-Pakets (7.7), Suchindex je Sitzung (7.5) und Schrittabgleich ohne Orakel (7.6). 02 und 07 verweisen für „was als schwächer gilt“ hierher; die Liste steht in [4.4](#44-aufnahmeprofile-nie-unbemerkt-schwächer). |
| [07](07-aufnahme-bedienung.md), [08](08-viewer.md), [09](09-anleitungs-export.md) | Abgeglichen: Privattaste als Umschalter ohne automatisches Ende, Klartext-Hinweis, Sync-Warnung und Standardordner, Durchsicht mit Einzelentscheidung für auffällige Eingaben, Sperre im Exportdialog, Verpixeln und Unschärfe als reine Optik. **Dort zu ergänzen bzw. zu prüfen:** Anzeige „Passwortabfrage erkannt“ in Leiste und Rahmen und das Verhalten der Privattaste während einer erkannten Abfrage ([Abschnitt 7](#7-privattaste)); Freigabetaste der Stufe „Streng“; Befehl „Letzte Eingabe schwärzen“; bereinigte Kopie und endgültiges Schwärzen; Vorschläge für Bildbereiche zu Textfundstellen; Hinweis, dass die Privattaste noch zurückgehaltene Tasten des laufenden Laufs mitmaskiert; Hinweis des Viewers, wenn eine einzeln gewählte Bildschwärzung denselben Inhalt im Nachbarbild sichtbar lässt. **Funktionsrunde:** abgeglichen sind Startmeldung bei schwächerem Schutz, Profil-Editor, Support-Start und Import-Dialog (07), der geführte Support-Ablauf, die Vorschläge für Verallgemeinerungen, der Hinweis auf Fundstellen ohne Bildposition, „Originalwerte zeigen“ als reine Anzeige und die Suche mit „noch nicht durchsucht“ (08), Inhalt, Benennung und Ablage des Support-Pakets (09, 3.5) und die Regeln Q12, Q14 und Q15 der Qualitätsprüfung (09, 12.1). **Dort zu prüfen bzw. zu ergänzen:** Marker-Bezeichnungen in der Liste der Durchsicht (08 nennt Ziel-Angaben und den Hinweis für den Empfänger); Rat zur Schwärzung, wenn der Wert einer Verallgemeinerung nach einem Geheimnis aussieht (V7); Hinweis „Ersetzen ist keine Schwärzung“ mit dem Angebot zu schwärzen; Hinweis beim Import, wenn auch „Letzte Eingabe schwärzen“ ohne Kürzel bleibt (IM6); Hinweis in der Trefferliste, dass ungeprüfte Eingaben nicht durchsucht werden (offener Punkt 15). |
| [06](06-ki-export-und-mcp.md) | Darstellung `{masked:N}` bzw. `{masked}`, `{unreviewed:N}`, `[geschwärzt]`, „Schritt geschwärzt“, Lücke „vom Autor entfernt“, schwarze Flächen mit ausgewiesenem Grund; keine Option zum Umgehen der Durchsicht; interne Kommentare in keiner Ausgabe; Entschärfung aufgezeichneter Texte. **Funktionsrunde:** Angaben des Autors im Kopf und als Marker, Platzhalter und ihre Abgrenzung von `[geschwärzt]`, Nahtstellen und ersetzte Schritte, `search` mit `scope`, `compare_sessions` mit `notCompared`, KI-Paket im Support-Paket. |
| [11](11-roadmap-tests-risiken.md) | Aufnahme der Prüfungen DT-01 bis DT-36 aus [Abschnitt 17](#17-prüfungen) in den Testplan. |

**Offene Punkte**

1. **Zeitwerte** (`T_max`, Nachlaufzeit, Schutzband) sind Startwerte und werden mit VE-08 und DT-03 gemessen.
2. **Pflicht-Durchsicht:** Sie verlangt Einzelentscheidungen nur für auffällige Eingaben. Sie auf alle Eingaben auszudehnen wäre sicherer, aber lästiger – Entscheidung nach ersten Nutzertests.
3. **Benachrichtigungen:** bleiben nach dem freigegebenen Plan eine dokumentierte Grenze; ob „schwärzen“ Standard werden soll, ist eine Produktentscheidung.
4. **Inkognito-Erkennung** über die Fensterleiste ist auf Machbarkeit und Haltbarkeit über Browser-Versionen zu prüfen.
5. **Ausnahme von Absturzabbildern** und Sperren gegen Auslagerung: Verfügbarkeit und Verhalten der Systemfunktionen in M1 prüfen.
6. **Dateiendung der gespeicherten Bilder:** Eine neutrale Endung verhindert Miniaturansichten im Explorer-Cache, widerspricht aber dem Ziel direkt ansehbarer PNG-Dateien – Entscheidung in 04.
7. **Verschlüsselte Sitzungsdateien** als Ausbaustufe.
8. **Schwellen der Passwortmanager-Erkennung** sind empirisch festzulegen.
9. **Rechtliche Hinweise** (Abschnitt 15) vor der Veröffentlichung fachlich gegenlesen lassen.
10. **Bedingungen der SignPath Foundation** vor M8 erneut prüfen; Ausweichweg bereithalten.
11. **Zeitablauf der Abfragemaskierung:** Die selbsttätige Maskierung einer erkannten Abfragezeile endet nach 60 s ohne Taste ([6.2](#62-regeln)). Ob dieser Wert passt oder ob die Maskierung bestehen bleiben soll, solange die Abfragezeile sichtbar ist, zeigen die Prüfungen DT-05 und DT-06.
12. **Optik „Verpixeln“ und „Unschärfe“:** Für KI-Paket und MCP-Server gilt immer die schwarz gefüllte Fläche. Ob die beiden optischen Varianten in Anleitungen erhalten bleiben (so 04 und 08) oder auch dort entfallen sollen, ist eine Produktentscheidung.
13. **Zähler außerhalb des Aufnahmebereichs:** Festgelegt sind Zahlen je Art. Ob die Zahl der Mausbewegungen (`moves`) einen Nutzen hat oder entfallen kann, zeigt die Praxis.
14. **Vergleichsmaßstab für „schwächer“:** Verglichen wird mit den Einstellungen des Nutzers ([4.4](#44-aufnahmeprofile-nie-unbemerkt-schwächer)) – so auch 02 und 04. Eine mit der EXE mitgegebene Einstellungsdatei könnte diesen Maßstab selbst senken. Ob die Startmeldung zusätzlich immer nennen soll, wenn eine Aufnahme schwächer schützt als die **Werkseinstellungen** (Zwischenablage-Inhalt an), ist eine Produktentscheidung; bis dahin nennt sie die Schutzstufe in jedem Fall.
15. **Suche im Viewer ohne ungeprüfte Inhalte:** Der Suchindex enthält keine ungeprüften auffälligen Eingaben – auch nicht für den Autor selbst. Das ist sicher, kann aber verwundern („ich habe es doch getippt“). Die Trefferliste sollte deshalb nennen, dass ungeprüfte Eingaben nicht durchsucht wurden.
16. **Platzhalter im Dateinamen:** Geschweifte Klammern sind in Windows-Dateinamen erlaubt, in manchen Mail- und Ticketsystemen aber lästig. Ob der Dateiname des Support-Pakets Platzhalter mit oder ohne Klammern trägt, legen 02 und 08 fest.
17. **Verallgemeinerung und Durchsicht:** Eine Verallgemeinerung zählt nicht als Durchsicht (V3). Ob eine auffällige Eingabe, die vollständig aus einem verallgemeinerten Wert besteht, trotzdem eine Einzelentscheidung braucht, zeigen die ersten Nutzertests.
18. **Terminal-Erkennung im echten Terminal bestätigen:** Die Regeln T1 bis T3 sind umgesetzt (6), aber nur mit Unit- und synthetischen Tests geprüft. Offen ist der Versuch O-09 an Windows Terminal und der klassischen Konsole (liefert das TextPattern die Zeile an der Schreibmarke, Dauer, Fehlalarme). Danach ist die Priorität von DS-022 zu prüfen, ebenso der Rückbau der Massenfreigabe „Übrige Klartext-Eingaben: in Ordnung“.
19. **Freigabetaste in „Streng“:** Ohne sie bleibt in dieser Stufe nur, die Stufe zu wechseln, wenn jemand im Terminal bewusst Klartext braucht. Ob die Taste nachgerüstet wird, entscheidet sich nach ersten Erfahrungen.
20. **Signierung (SignPath):** nicht umgesetzt; Releases sind unsigniert mit Prüfsummen.
