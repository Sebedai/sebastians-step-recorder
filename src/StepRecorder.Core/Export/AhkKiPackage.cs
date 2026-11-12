using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Compare;
using StepRecorder.Data;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;
using StepRecorder.Steps;

namespace StepRecorder.Export;

public sealed class AhkKiOptions
{
    public string? OutDir { get; set; }
    /// <summary>Weitere Aufnahmen desselben Ablaufs: Unterschiede zeigen Parameter und Sonderfälle.</summary>
    public List<string> With { get; set; } = new();
    public bool Ocr { get; set; } = true;
    public bool ContextImages { get; set; } = true;
    /// <summary>Sprache von Anleitung und Skriptentwurf (de/en); null = Sprache der Aufnahme.</summary>
    public string? Language { get; set; }
}

/// <summary>
/// AHK-KI-Paket: alles, was ein KI-Assistent braucht, um aus der Aufnahme ein robustes AutoHotkey-v2-Skript zu schreiben –
/// Anleitung für die KI, ablauf.json (Fenster, Wege zum Element nach Stabilität, Eingaben, Wartesignale, Erwartungen,
/// Parameter, Wiederholungen, Umgebung), Skriptentwurf, Bildausschnitte und Texterkennung um den Klickpunkt.
/// Wie bei Anleitungen ist die Durchsicht auffälliger Eingaben Voraussetzung; Passwörter sind nie enthalten.
/// </summary>
public static class AhkKiPackage
{
    public const string FormatVersion = "1.0";

    public static ExportResult Export(SessionView view, AhkKiOptions o)
    {
        view.Rebuild();
        string lang = o.Language ?? view.Session.Language;
        view.Language = lang;
        var res = new ExportResult { Unreviewed = view.UnreviewedCount };
        if (res.Unreviewed > 0 && !view.Headless)
            throw new InvalidOperationException(Loc.For(lang, $"{res.Unreviewed} auffällige Eingaben sind noch nicht durchgesehen (Durchsicht der Eingaben).", $"{res.Unreviewed} conspicuous inputs have not been reviewed yet (input review)."));
        string title = view.Title.Length > 0 ? view.Title : Loc.For(lang, "Ablauf", "Workflow");
        string dir = o.OutDir ?? Path.Combine(view.Session.Path, "export", "ahk-ki", Fmt.FileSafe(title));
        ExportFolders.PrepareOwned(dir, "ablauf.json");
        string imgDir = Path.Combine(dir, "bilder");

        // Skriptentwurf (schreibt auch die 48×48-Ausschnitte für ImageSearch)
        var ahkOpt = new AhkOptions { ImageDir = imgDir, Language = lang };
        File.WriteAllText(Path.Combine(dir, "entwurf.ahk"), AhkExport.Build(view, ahkOpt, res), new UTF8Encoding(true));

        var doc = Document(view, o, imgDir, title, lang);
        var steps = doc.Arr("steps")!;
        // Vergleich mit weiteren Aufnahmen
        var comparisons = new JsonArray();
        int k = 0;
        foreach (var other in o.With)
        {
            try
            {
                using var ov = SessionView.Open(other, headless: true);
                using var me = SessionView.Open(view.Session.Path, headless: true);
                var cmp = StepCompare.Compare(me, ov);
                string file = $"vergleich-{++k}.md";
                File.WriteAllText(Path.Combine(dir, file), StepCompare.Markdown(cmp, lang), new UTF8Encoding(false));
                comparisons.Add(new JsonObject { ["session"] = ov.Title, ["file"] = file });
            }
            catch (Exception ex) { res.Warnings.Add(Loc.For(lang, $"Vergleich mit {other} nicht möglich: {ex.Message}", $"Comparison with {other} not possible: {ex.Message}")); }
        }
        if (comparisons.Count > 0) doc["comparisons"] = comparisons;
        File.WriteAllText(Path.Combine(dir, "ablauf.json"), Json.Pretty(doc), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "README_AHK_KI.md"), Readme(title, steps.Count, comparisons.Count > 0, lang), new UTF8Encoding(false));
        res.Files.AddRange(Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories));
        res.Path = dir;
        if (!Ocr.Available && o.Ocr) res.Notices.Add(Loc.For(lang, "Texterkennung nicht verfügbar (keine OCR-Sprache installiert).", "Text recognition not available (no OCR language installed)."));
        return res;
    }

    /// <summary>Inhalt von ablauf.json. Ohne <paramref name="imgDir"/> entstehen keine Bilder (z. B. für MCP).</summary>
    public static JsonObject Document(SessionView view, AhkKiOptions o, string? imgDir, string? title = null, string? lang = null)
    {
        lang ??= o.Language ?? view.Session.Language;
        title ??= view.Title.Length > 0 ? view.Title : Loc.For(lang, "Ablauf", "Workflow");
        var steps = new JsonArray();
        var apps = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        int ocrCount = 0;
        var all = view.Effective(guide: true, importance: "all");
        int tick = 0;
        foreach (var s in all)
        {
            ExportJob.Tick(++tick, all.Count, Loc.For(lang, "Schritte", "Steps"));
            if (s.Kind == "move") continue;
            var item = Step(view, s, imgDir, o, ref ocrCount, apps);
            if (item != null) steps.Add(item);
        }
        var start = view.Session.Events.FirstOrDefault(e => e.Str("type") == "session.start");
        return new JsonObject
        {
            ["format"] = "steprec.ahk-ki", ["formatVersion"] = FormatVersion, ["title"] = title,
            ["recordedUtc"] = Json.Utc(view.Session.StartUtc), ["durationMs"] = view.Session.DurationUs / 1000,
            ["goal"] = view.Goal()?.DeepClone(),
            ["environment"] = new JsonObject
            {
                ["recorderElevated"] = start?.Obj("recorder")?["elevated"]?.DeepClone(),
                ["os"] = start?.Obj("environment")?.DeepClone(),
                ["monitors"] = start?.Obj("topology")?.Arr("monitors")?.DeepClone(),
                ["keyboardLayouts"] = new JsonArray(view.Session.Events.Where(e => e.Str("hkl") != null).Select(e => e.Str("hkl")!).Distinct().Take(5).Select(x => (JsonNode)x).ToArray()),
                ["applications"] = new JsonArray(apps.Values.Select(a => (JsonNode)a).ToArray()),
                ["coordinates"] = Loc.For(lang, "physische Pixel; window = relativ zur linken oberen Fensterecke, client = relativ zum Clientbereich, screen = virtueller Bildschirm", "physical pixels; window = relative to the top-left corner of the window, client = relative to the client area, screen = virtual screen")
            }.Clean(),
            ["parameters"] = view.Steps.Arr("parameters")?.DeepClone(),
            ["repetitions"] = view.Steps.Arr("repetitions")?.DeepClone(),
            ["steps"] = steps
        }.Clean();
    }

    static JsonObject? Step(SessionView view, ViewStep s, string? imgDir, AhkKiOptions o, ref int ocrCount, Dictionary<string, JsonObject> apps)
    {
        var j = s.Json;
        var o2 = new JsonObject
        {
            ["id"] = s.Id, ["n"] = s.Number, ["kind"] = s.Kind, ["importance"] = s.Importance, ["tMs"] = s.TFrom / 1000,
            ["title"] = s.AuthorTitle ?? s.Title, ["text"] = s.AuthorBody ?? s.Guide, ["protocol"] = s.Protocol, ["evidence"] = s.EvidenceLevel
        };
        if (s.Comments.Count > 0) o2["comments"] = new JsonArray(s.Comments.Select(c => (JsonNode)(c.Str("text") ?? "")).ToArray());
        if (s.Kind is "comment" or "marker" or "snapshot" or "system" or "gap") { if (j.Obj("input")?.Str("markerKind") is { } mk) o2["marker"] = mk; return o2.Clean(); }

        var w32 = j.Obj("target")?.Obj("win32");
        var win = w32?.Obj("window");
        var window = new JsonObject
        {
            ["winTitle"] = w32?.Obj("ahk")?.Str("winTitle"), ["winTitleFull"] = w32?.Obj("ahk")?.Str("winTitleFull"),
            ["title"] = win?.Str("title") ?? j.Obj("ctx")?.Str("title"), ["class"] = win?.Str("class"), ["exe"] = win?.Str("exe") ?? j.Obj("ctx")?.Str("process"),
            ["state"] = win?.Str("state"), ["dpi"] = win?["dpi"]?.DeepClone(), ["elevated"] = win?.Obj("app")?["elevated"]?.DeepClone(),
            ["volatileClass"] = AhkExport.Volatile(win?.Str("class")) ? true : null, ["owner"] = win?.Obj("owner")?.DeepClone()
        }.Clean();
        o2["window"] = window;
        if (win?.Str("exe") is { } exe && win.Obj("app") is { } app && !apps.ContainsKey(exe))
        {
            var a = (JsonObject)app.DeepClone(); a["exe"] = exe; a["path"] = win.Str("path");
            apps[exe] = a;
        }
        if (apps.TryGetValue(win?.Str("exe") ?? "", out var known) && win?.Str("class") is { } wc)
        {
            var classes = known["windowClasses"] as JsonArray ?? new JsonArray();
            if (!classes.Any(x => x?.GetValue<string>() == wc)) classes.Add((JsonNode)wc);
            known["windowClasses"] = classes;
        }
        if (j.Arr("selectors") is { } sel) o2["selectors"] = sel.DeepClone();
        if (w32 != null)
        {
            o2["point"] = new JsonObject { ["window"] = w32.Obj("point")?.Arr("window")?.DeepClone(), ["client"] = w32.Obj("point")?.Arr("client")?.DeepClone(), ["screen"] = w32.Obj("point")?.Arr("screen")?.DeepClone(), ["inControl"] = w32.Obj("control")?.Arr("point")?.DeepClone() }.Clean();
            if (w32.Obj("control") is { } ctl) o2["control"] = new JsonObject { ["classNN"] = ctl.Str("classNN"), ["class"] = ctl.Str("class"), ["id"] = ctl["id"]?.DeepClone(), ["text"] = ctl.Str("text"), ["textWithheld"] = ctl.Str("textWithheld"), ["checked"] = ctl.Str("checked"), ["rect"] = ctl.Obj("rect")?.DeepClone(), ["caret"] = ctl.Obj("caret")?.DeepClone() }.Clean();
            if (w32.Obj("focus") is { } fc) o2["focusBefore"] = fc.Str("classNN");
            if (w32.Str("cursor") is { } cur) o2["cursorAtClick"] = cur;
            if (w32.Str("pixel") is { } px) o2["pixel"] = px;
            if (w32.Arr("texts") is { } texts) o2["windowTexts"] = texts.DeepClone();
            if (w32.Obj("msaa") is { } msaa) o2["msaa"] = msaa.DeepClone();
        }
        if (j.Obj("target")?.Obj("el") is { } el) o2["element"] = el.DeepClone();
        if (j.Obj("target")?.Arr("path") is { } path) o2["elementPath"] = path.DeepClone();
        if (Input(s) is { } inp) o2["input"] = inp;
        foreach (var key in new[] { "sync", "verify", "menuPath", "choose", "data", "explorer", "browser", "office", "list" })
            if (j[key] is JsonNode n) o2[key] = n.DeepClone();
        if (j.Obj("effect") is { } eff && eff.Bool("observed"))
            o2["effect"] = new JsonObject { ["windowsOpened"] = eff.Arr("windowsOpened")?.DeepClone(), ["windowsClosed"] = eff.Arr("windowsClosed")?.DeepClone(), ["titleChanges"] = eff.Arr("titleChanges")?.DeepClone(), ["values"] = eff.Arr("values")?.DeepClone() }.Clean();

        // Bilder und Texterkennung um den Klickpunkt
        bool mouse = s.Kind is "click" or "doubleClick" or "tripleClick" or "rightClick" or "drag" or "hover" or "scroll";
        if (mouse && imgDir != null && s.PreFrame != null && j.Obj("position") != null)
        {
            var images = new JsonObject();
            if (File.Exists(Path.Combine(imgDir, $"schritt-{s.Number:00}.png"))) images["needle"] = $"bilder/schritt-{s.Number:00}.png";
            if (o.ContextImages)
                try
                {
                    using var ctxImg = StepImages.Render(view, s, "crop", "compatible", annotate: true, respectMarks: false);
                    if (ctxImg != null) { Directory.CreateDirectory(imgDir); string f = $"schritt-{s.Number:00}-kontext.png"; ctxImg.Bitmap.Save(Path.Combine(imgDir, f), System.Drawing.Imaging.ImageFormat.Png); images["context"] = "bilder/" + f; }
                }
                catch { }
            if (images.Count > 0) o2["images"] = images;
            if (o.Ocr && ocrCount < 80)
                try
                {
                    using var raw = StepImages.Render(view, s, "crop", "original", annotate: false);
                    if (raw?.Marker is { } mk && Ocr.Around(raw.Bitmap, mk) is { } ocr) { o2["ocr"] = ocr; ocrCount++; }
                }
                catch { }
        }
        return o2.Clean();
    }

    static JsonObject? Input(ViewStep s)
    {
        var i = s.Json.Obj("input");
        if (i == null) return null;
        var o = new JsonObject();
        switch (s.Kind)
        {
            case "type":
                o["masked"] = i.Bool("masked");
                if (!i.Bool("masked")) o["text"] = i.Str("text");
                o["notation"] = TextTemplates.TokensNotation(i);
                if (i.Obj("terminator") is { } t) o["terminator"] = t.DeepClone();
                if (i.Str("valueBefore") is { } vb) o["fieldBefore"] = vb;
                if (i.Str("valueAfter") is { } va) o["fieldAfter"] = va;
                o["durationMs"] = i.Long("durationUs") / 1000;
                break;
            case "hotkey":
                o["combo"] = i.Str("combo"); o["keys"] = i.Arr("keys")?.DeepClone(); o["count"] = i.Int("count", 1); o["meaning"] = i.Str("commonMeaning");
                break;
            case "key":
                o["key"] = i.Str("key"); o["ahkKey"] = AhkExport.AhkKey(i.Str("key") ?? ""); o["mods"] = i.Arr("mods")?.DeepClone(); o["count"] = i.Int("count", 1); o["held"] = i["held"]?.DeepClone();
                break;
            case "scroll":
                o["notches"] = i["notches"]?.DeepClone(); o["axis"] = i.Str("axis"); o["mods"] = i.Arr("mods")?.DeepClone(); o["semantic"] = i.Str("semantic");
                break;
            case "drag":
                o["button"] = i.Str("button"); o["mods"] = i.Arr("mods")?.DeepClone(); o["durationMs"] = i.Long("durationUs") / 1000; o["distancePx"] = i["distancePx"]?.DeepClone();
                o["holdBeforeMoveMs"] = i.Long("holdBeforeMoveUs") / 1000; o["holdAtEndMs"] = i.Long("holdAtEndUs") / 1000; o["dragKind"] = i.Str("dragKind");
                // Weg in Fensterkoordinaten
                if (s.Json.Obj("target")?.Obj("win32")?.Obj("point")?.Arr("window") is { Count: 2 } wp && s.Json.Obj("position") is { } pos && i.Arr("path") is { } path)
                {
                    int ox = wp[0]!.GetValue<int>() - pos.Int("x"), oy = wp[1]!.GetValue<int>() - pos.Int("y");
                    o["pathWindow"] = new JsonArray(path.OfType<JsonArray>().Select(p => (JsonNode)new JsonArray(p[0]!.GetValue<int>() + ox, p[1]!.GetValue<int>() + oy, p.Count > 2 ? p[2]!.GetValue<long>() : 0)).ToArray());
                }
                break;
            default:
                o["button"] = i.Str("button"); o["count"] = i.Int("count", 1); o["mods"] = i.Arr("mods")?.DeepClone();
                o["pressMs"] = i.Long("pressUs") / 1000; o["longPress"] = i.Bool("longPress");
                break;
        }
        return o.Clean();
    }

    /// <summary>Regeln für KI-Assistenten (auch über MCP), deutsch.</summary>
    public static string Rules => Readme("Ablauf", 0, false, "de");

    /// <summary>Regeln für KI-Assistenten in der gewünschten Sprache (de/en).</summary>
    public static string RulesFor(string lang) => Readme(Loc.For(lang, "Ablauf", "Workflow"), 0, false, lang);

    static string Readme(string title, int steps, bool comparisons, string lang) => lang == "de" ? ReadmeDe(title, steps, comparisons) : ReadmeEn(title, steps, comparisons);

    static string ReadmeDe(string title, int steps, bool comparisons) => $"""
# AHK-KI-Paket: {title}

Dieses Paket wurde von **StepRecorder** erzeugt. Es beschreibt einen am Windows-PC vorgeführten Ablauf ({steps} Schritte) so genau, dass daraus ein **robustes AutoHotkey-v2-Skript** entstehen kann. Lies zuerst diese Datei, dann `ablauf.json`; `entwurf.ahk` ist ein automatisch erzeugter Startpunkt.

## Dateien

| Datei | Inhalt |
|---|---|
| `ablauf.json` | Alle Schritte mit Fenster, Wegen zum Element, Eingaben, Wartesignalen, Erwartungen, Parametern, Wiederholungen und Umgebung |
| `entwurf.ahk` | Automatisch erzeugtes Skript (AutoHotkey v2) – verbessern, nicht blind übernehmen |
| `bilder/schritt-NN.png` | 48×48-Ausschnitt um den Klickpunkt (für `ImageSearch`) |
| `bilder/schritt-NN-kontext.png` | Ausschnitt mit Markierungen: roter Ring = Klick, blauer Rahmen = Element, orange = Ziehweg |
{(comparisons ? "| `vergleich-N.md` | Unterschiede zu weiteren Aufnahmen desselben Ablaufs |\n" : "")}
## Regeln für das Skript

1. **AutoHotkey v2** (`#Requires AutoHotkey v2.0`). Koordinaten in `ablauf.json` sind physische Pixel; deshalb am Anfang `DllCall("SetThreadDpiAwarenessContext", "ptr", -4, "ptr")`.
2. **Fenster** über `window.winTitle` (`ahk_class … ahk_exe …`) ansprechen, vor jeder Aktion `WinWait` + `WinActivate` + `WinWaitActive`. Dialoge (`#32770`) mit Titel unterscheiden. `window.volatileClass: true` heißt: Klassenname ändert sich je Start – stattdessen Titel + `ahk_exe` verwenden.
3. **Wege zum Element** stehen in `selectors`, absteigend nach `score` (5 = sehr stabil). Bevorzugt: `classNN`/`controlId` → `ControlClick`/`ControlFocus`/`ControlSetText`; `controlText` → `ControlClick "Text"`; `uia` → UIA-v2-Bibliothek (Name, ControlType, AutomationId, Index); `label` → Feld neben dieser Beschriftung; `relative` → Anteil am Clientbereich (übersteht Größenänderungen); `window` → Fensterkoordinaten nur als letzter Ausweg. Mit `images.needle` als Rückfall `ImageSearch`; `ocr` nennt erkannten Text am Klickpunkt.
4. **Tastatur vor Maus**, wenn die Anwendung ein Tastenkürzel nennt (`selectors[kind=keyboard]`, `menuPath.accelerator`). Klassische Menüs (`menuPath.win32Menu`) mit `MenuSelect` über `menuPath.win32Path` (bzw. Positionen `win32Positions` als „N&“), alternativ `PostMessage 0x111, menuPath.commandId, 0,, <menuPath.owner.winTitle>` (WM_COMMAND). Listen und Auswahlfelder (`choose`) mit `ControlChooseString`/`ControlChooseIndex`, wenn `choose.win32Control` bzw. `choose.win32` (Art, ClassNN, Index 1-basiert, Text) vorhanden ist; ListView-Zeilen über `ListViewGetContent` + Tastatur; MSAA-Standardaktion (`msaa.defaultAction`) als Alternative zum Klick.
5. **Nicht blind schlafen:** `sync` sagt, worauf gewartet wurde – `busyMs` (Mauszeiger beschäftigt → warten, solange `A_Cursor` „Wait“/„AppStarting“ ist), `waitWindow` (→ `WinWait`), `settleMs`/`readyMs` (Bildschirm ruhig). `thinkMs` ist menschliche Denkzeit und gehört **nicht** ins Skript.
6. **Erfolg prüfen:** `verify` beschreibt den Zustand nach dem Schritt (Titel, Fokus, geänderte Texte und Häkchen nach ClassNN). Daraus Prüfungen ableiten (`WinExist`, `ControlGetText`, `ControlGetChecked`) und bei Abweichung abbrechen oder melden.
7. **Anwendungskontext:** `explorer.folder`/`folderAfter` → Ordner direkt öffnen (`Run 'explorer.exe "…"'` oder Shell-COM `Navigate`) statt Doppelklicks; `browser.url` (ohne Abfrage/Fragment) → `Run "<url>"` + `WinWait` auf den Seitentitel; `office` (Excel: Mappe, Blatt, Zelle; Word: Dokument) → `ComObjActive("Excel.Application")`; `data.fileDialog.folder` + `fileNameControl` → vollständigen Pfad mit `ControlSetText` ins Dateinamenfeld; `control.caret` (Zeile/Spalte, nie Inhalt) → `EditPaste`/`ControlSetText` statt Tippen; `verify.statusBar` → `StatusBarGetText`/`StatusBarWait` als Wartebedingung. Benutzerpfade verallgemeinern (`A_MyDocuments`, `A_Desktop`, `A_AppData`, `A_Temp`, `EnvGet("USERPROFILE")`, `A_UserName` …).
8. **Parameter:** `parameters` sind die veränderlichen Werte (Eingaben, eingefügte Daten, Dateinamen). Als Variablen oder Funktionsparameter oben im Skript anlegen. `secret: true` = Passwort: **nie** ins Skript schreiben, zur Laufzeit abfragen (`InputBox(..., "Password")`) oder aus einem sicheren Speicher lesen.
9. **Wiederholungen** (`repetitions`): gleiche Schrittfolge mehrfach hintereinander – als Schleife über die wechselnden Werte (`varying`) schreiben.
10. **Maus:** `input.button`, `pressMs`/`longPress` (gedrückt halten → `Click "x y Down"`, warten, `Click "x y Up"`), beim Ziehen `holdBeforeMoveMs`, `pathWindow` (Punkte mit ms) und `holdAtEndMs` – in kleinen Schritten bewegen, nicht springen.
11. **Rechte:** Ist ein Zielprogramm `elevated: true` (`environment.applications`), muss das Skript als Administrator laufen (`if !A_IsAdmin` → `Run '*RunAs "' A_ScriptFullPath '"'`).
12. **Start:** `environment.applications[].commandLine` zeigt, wie das Programm gestartet wurde (`Run`), falls es nicht schon läuft.
13. Kommentiere jeden Schritt kurz mit seiner Nummer und dem Text aus `title`. Am Anfang `^Esc::ExitApp` als Notbremse.

## Felder je Schritt (Auszug)

`kind` (click, doubleClick, rightClick, drag, scroll, type, hotkey, key, hover, windowSwitch, wait …), `title`/`text` (lesbare Beschreibung), `window`, `selectors`, `point` (window/client/screen/inControl), `control` (ClassNN, ID, Text, Häkchen), `element` (UI Automation), `elementPath`, `input`, `sync`, `verify`, `menuPath` (mit `commandId`, `win32Path`, `owner`), `choose` (mit `win32`), `data` (Dateidialog, Herkunft eingefügter Daten), `explorer`, `browser`, `office`, `list` (Win32-Liste: Anzahl, Auswahl, Einträge), `msaa`, `effect`, `images`, `ocr`, `windowTexts` (sichtbare Texte des Fensters mit Position `r` im Clientbereich).

Datenschutz: Passwortfelder und Privatmodus-Eingaben sind nie enthalten; Inhalte anderer Eingabefelder nur, wenn sie in der Aufnahme durchgesehen wurden.
""";

    static string ReadmeEn(string title, int steps, bool comparisons) => $"""
# AHK AI package: {title}

This package was created by **StepRecorder**. It describes a workflow demonstrated on a Windows PC ({steps} steps) in enough detail to produce a **robust AutoHotkey v2 script** from it. Read this file first, then `ablauf.json`; `entwurf.ahk` is an automatically generated starting point.

## Files

| File | Content |
|---|---|
| `ablauf.json` | All steps with window, ways to reach the element, inputs, wait signals, expectations, parameters, repetitions and environment |
| `entwurf.ahk` | Automatically generated script (AutoHotkey v2) – improve it, do not adopt it blindly |
| `bilder/schritt-NN.png` | 48×48 crop around the click point (for `ImageSearch`) |
| `bilder/schritt-NN-kontext.png` | Crop with marks: red ring = click, blue frame = element, orange = drag path |
{(comparisons ? "| `vergleich-N.md` | Differences to further recordings of the same workflow |\n" : "")}
## Rules for the script

1. **AutoHotkey v2** (`#Requires AutoHotkey v2.0`). Coordinates in `ablauf.json` are physical pixels; therefore start with `DllCall("SetThreadDpiAwarenessContext", "ptr", -4, "ptr")`.
2. Address **windows** via `window.winTitle` (`ahk_class … ahk_exe …`); before every action use `WinWait` + `WinActivate` + `WinWaitActive`. Tell dialogs (`#32770`) apart by title. `window.volatileClass: true` means the class name changes with every start – use title + `ahk_exe` instead.
3. **Ways to reach the element** are in `selectors`, descending by `score` (5 = very stable). Preferred: `classNN`/`controlId` → `ControlClick`/`ControlFocus`/`ControlSetText`; `controlText` → `ControlClick "Text"`; `uia` → UIA v2 library (name, ControlType, AutomationId, index); `label` → field next to this label; `relative` → fraction of the client area (survives resizing); `window` → window coordinates only as a last resort. Use `ImageSearch` with `images.needle` as a fallback; `ocr` gives the text recognized at the click point.
4. **Keyboard before mouse** when the application names a shortcut (`selectors[kind=keyboard]`, `menuPath.accelerator`). Classic menus (`menuPath.win32Menu`) with `MenuSelect` via `menuPath.win32Path` (or the positions `win32Positions` as "N&"), alternatively `PostMessage 0x111, menuPath.commandId, 0,, <menuPath.owner.winTitle>` (WM_COMMAND). Lists and selection fields (`choose`) with `ControlChooseString`/`ControlChooseIndex` when `choose.win32Control` or `choose.win32` (kind, ClassNN, 1-based index, text) is present; ListView rows via `ListViewGetContent` + keyboard; the MSAA default action (`msaa.defaultAction`) as an alternative to the click.
5. **Do not sleep blindly:** `sync` says what was waited for – `busyMs` (mouse pointer busy → wait while `A_Cursor` is "Wait"/"AppStarting"), `waitWindow` (→ `WinWait`), `settleMs`/`readyMs` (screen calm). `thinkMs` is human thinking time and does **not** belong in the script.
6. **Check success:** `verify` describes the state after the step (title, focus, changed texts and check marks by ClassNN). Derive checks from it (`WinExist`, `ControlGetText`, `ControlGetChecked`) and abort or report on deviation.
7. **Application context:** `explorer.folder`/`folderAfter` → open the folder directly (`Run 'explorer.exe "…"'` or shell COM `Navigate`) instead of double clicks; `browser.url` (without query/fragment) → `Run "<url>"` + `WinWait` for the page title; `office` (Excel: workbook, sheet, cell; Word: document) → `ComObjActive("Excel.Application")`; `data.fileDialog.folder` + `fileNameControl` → full path with `ControlSetText` into the file name field; `control.caret` (line/column, never content) → `EditPaste`/`ControlSetText` instead of typing; `verify.statusBar` → `StatusBarGetText`/`StatusBarWait` as a wait condition. Generalize user paths (`A_MyDocuments`, `A_Desktop`, `A_AppData`, `A_Temp`, `EnvGet("USERPROFILE")`, `A_UserName` …).
8. **Parameters:** `parameters` are the variable values (inputs, pasted data, file names). Create them as variables or function parameters at the top of the script. `secret: true` = password: **never** write it into the script, ask for it at runtime (`InputBox(..., "Password")`) or read it from a secure store.
9. **Repetitions** (`repetitions`): the same sequence of steps several times in a row – write it as a loop over the varying values (`varying`).
10. **Mouse:** `input.button`, `pressMs`/`longPress` (hold → `Click "x y Down"`, wait, `Click "x y Up"`), when dragging `holdBeforeMoveMs`, `pathWindow` (points with ms) and `holdAtEndMs` – move in small steps, do not jump.
11. **Rights:** If a target program is `elevated: true` (`environment.applications`), the script must run as administrator (`if !A_IsAdmin` → `Run '*RunAs "' A_ScriptFullPath '"'`).
12. **Start:** `environment.applications[].commandLine` shows how the program was started (`Run`), in case it is not already running.
13. Comment every step briefly with its number and the text from `title`. Start with `^Esc::ExitApp` as an emergency brake.

## Fields per step (excerpt)

`kind` (click, doubleClick, rightClick, drag, scroll, type, hotkey, key, hover, windowSwitch, wait …), `title`/`text` (readable description), `window`, `selectors`, `point` (window/client/screen/inControl), `control` (ClassNN, ID, text, check mark), `element` (UI Automation), `elementPath`, `input`, `sync`, `verify`, `menuPath` (with `commandId`, `win32Path`, `owner`), `choose` (with `win32`), `data` (file dialog, origin of pasted data), `explorer`, `browser`, `office`, `list` (Win32 list: count, selection, entries), `msaa`, `effect`, `images`, `ocr`, `windowTexts` (visible texts of the window with position `r` in the client area).

Privacy: password fields and private-mode inputs are never included; contents of other input fields only if they were reviewed in the recording.
""";
}
