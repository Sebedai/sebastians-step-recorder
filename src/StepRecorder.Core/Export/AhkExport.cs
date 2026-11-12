using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Capture;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Export;

public sealed class AhkOptions
{
    public string? OutPath { get; set; }
    /// <summary>Auch die Denkpausen des Menschen übernehmen (begrenzt). Standard: nur warten, bis die Anwendung bereit war.</summary>
    public bool RealTiming { get; set; }
    /// <summary>Ordner für Bildausschnitte (ImageSearch-Rückfall); null = keine.</summary>
    public string? ImageDir { get; set; }
    /// <summary>Bildausschnitte neben das Skript schreiben (Unterordner „bilder“).</summary>
    public bool Images { get; set; } = true;
    public bool Overwrite { get; set; } = true;
    /// <summary>Für Menschen lesbar: Aufbau des Skripts erklärt und je Schritt ein Satz „Was hier passiert“.</summary>
    public bool Readable { get; set; }
}

/// <summary>
/// AutoHotkey-v2-Skript aus einer Aufnahme: Fenster über WinTitle (ahk_class/ahk_exe), klassische Steuerelemente
/// über ControlClick/ClassNN, sonst Klicks in Fensterkoordinaten; Eingaben als SendText, Tasten als Send.
/// Maskierte Eingaben (Passwörter) werden zur Laufzeit abgefragt, nie ins Skript geschrieben.
/// Jede Zeile trägt die Aufnahmedaten als Kommentar (UIA-Name, AutomationId, Alternativen).
/// </summary>
public static class AhkExport
{
    public static ExportResult Export(SessionView view, AhkOptions o)
    {
        view.Rebuild();
        var res = new ExportResult { Unreviewed = view.UnreviewedCount };
        if (res.Unreviewed > 0 && !view.Headless)
            throw new InvalidOperationException($"{res.Unreviewed} auffällige Eingaben sind noch nicht durchgesehen (Durchsicht der Eingaben).");
        string title = view.Title.Length > 0 ? view.Title : "Ablauf";
        string outPath = o.OutPath ?? Path.Combine(view.Session.Path, "export", "ahk", Fmt.FileSafe(title) + ".ahk");
        if (Directory.Exists(outPath)) outPath = Path.Combine(outPath, Fmt.FileSafe(title) + ".ahk");
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        if (File.Exists(outPath) && !o.Overwrite) throw new IOException("Ziel existiert bereits: " + outPath);
        if (o.Images && o.ImageDir == null) o.ImageDir = Path.Combine(Path.GetDirectoryName(outPath)!, "bilder");
        var script = Build(view, o, res);
        File.WriteAllText(outPath, script, new UTF8Encoding(true)); // AHK erkennt UTF-8 mit BOM sicher
        res.Files.Add(outPath); res.Path = outPath;
        return res;
    }

    sealed class Ctx
    {
        public readonly StringBuilder Body = new();
        public readonly List<string> Vars = new();
        public string? CurrentWin;
        public bool Snippet;
        public bool UsesGlide, UsesImages, UsesChecks, UsesBusy, UsesExplorer, UsesListView, UsesMsaa;
        public Dictionary<string, JsonObject> Params = new();
        public HashSet<string> DeclaredParams = new();
        public int Masked;
        /// <summary>Zusätzliche Einrückung (Blöcke wie catch { … }).</summary>
        public string Indent = "";
        /// <summary>Benutzername der Aufnahme (aus Profilpfaden) für die Pfad-Verallgemeinerung.</summary>
        public string? UserName;
        /// <summary>Schritte, deren Aktion ein späterer Schritt übernimmt (Menü öffnen vor MenuSelect, Liste aufklappen vor ControlChooseString).</summary>
        public Dictionary<string, string> Absorbed = new();
        /// <summary>Menü oder Listenauswahl dieses Schritts wurde bereits als Hauptweg ausgegeben.</summary>
        public bool MenuDone, ChooseDone;
        public string? LastUrl, LastExplorer, LastOffice;
        public void L(string s = "") => Body.Append("    ").Append(s.Length > 0 ? Indent : "").Append(s).Append("\r\n");
    }

    /// <summary>Text als AHK-Ausdruck mit verallgemeinerten Pfaden (A_MyDocuments, A_UserName …).</summary>
    static string Lit(Ctx c, string s) => AhkPaths.Expr(s, c.UserName);

    public static string Build(SessionView view, AhkOptions o, ExportResult? res = null)
    {
        _view = view;
        var c = new Ctx();
        foreach (var p in view.Steps.Arr("parameters")?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            if (p.Str("step") is { } sid) c.Params[sid] = p;
        var reps = view.Steps.Arr("repetitions")?.OfType<JsonObject>().ToList() ?? new();
        var steps = view.Effective(guide: true, importance: "all").Where(s => s.Kind is not ("move")).ToList();
        c.UserName = UserNameOf(steps);
        c.Absorbed = AbsorbedSteps(steps);
        int nActions = 0;
        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            var j = s.Json;
            string head = $"; --- Schritt {s.Number}: {OneLine(s.AuthorTitle ?? s.Title)}";
            if (s.Kind is "comment" or "marker" or "snapshot" or "system" or "gap")
            {
                c.L(head);
                if (j.Obj("input")?.Str("text") is { } ct) c.L("; " + OneLine(ct));
                foreach (var cm in s.Comments) c.L("; Hinweis: " + OneLine(cm.Str("text") ?? ""));
                c.L();
                continue;
            }
            if (c.Absorbed.TryGetValue(s.Id, out var why))
            {
                c.L(head + $" ({KindLabel(s.Kind)})");
                c.L($"; ({why})");
                CommentedClick(c, s, "Klick in der Aufnahme");
                c.L();
                continue;
            }
            c.MenuDone = c.ChooseDone = false;
            if (reps.FirstOrDefault(r => r.Arr("steps")?.FirstOrDefault()?.GetValue<string>() == s.Id) is { } rp)
            {
                c.L($"; ===== Wiederholung: {rp.Int("length")} Schritte {rp.Int("repeats")}× nacheinander – Kandidat für eine Schleife =====");
                foreach (var vv in rp.Arr("varying") ?? new JsonArray())
                    c.L($";   wechselnd an Position {vv.Int("position")}: " + string.Join(" | ", (vv.Arr("values") ?? new JsonArray()).Select(x => OneLine(x?.GetValue<string>() ?? "", 30))));
            }
            c.L(head + $" ({KindLabel(s.Kind)})");
            if (o.Readable && (s.AuthorBody ?? s.Guide) is { Length: > 0 } words) c.L("; Was hier passiert: " + OneLine(words));
            foreach (var cm in s.Comments) c.L("; Hinweis: " + OneLine(cm.Str("text") ?? ""));
            if (j.Obj("ctx") is { } cx) c.L($"; Fenster: {OneLine(cx.Str("title") ?? "")} ({cx.Str("process")})");
            if (Uia(j.Obj("target")) is { } uia) c.L("; Element: " + uia);
            if (j.Arr("selectors")?.OfType<JsonObject>().FirstOrDefault(x => x.Str("kind") == "keyboard") is { } kb && (kb.Str("accelerator") ?? kb.Str("accessKey")) is { } kbs) c.L("; Tastatur laut Anwendung: " + kbs);
            if (j.Arr("selectors")?.OfType<JsonObject>().FirstOrDefault(x => x.Str("kind") == "label") is { } lb) c.L($"; Beschriftung daneben: „{OneLine(lb.Str("text") ?? "", 40)}“");
            ContextComments(c, s);
            bool emitted = s.Kind switch
            {
                "click" or "doubleClick" or "tripleClick" or "rightClick" => Click(c, s),
                "drag" => Drag(c, s),
                "scroll" => Scroll(c, s),
                "hover" => Hover(c, s),
                "type" => Type(c, s),
                "hotkey" => Hotkey(c, s),
                "key" => Key(c, s),
                "windowSwitch" => Switch(c, s),
                "wait" => Wait(c, s),
                _ => false
            };
            if (!emitted) c.L("; (keine automatisierbare Aktion erkannt)");
            else nActions++;
            Alternatives(c, s, o);
            // Warten, bis die Anwendung bereit ist (Zeiger, neues Fenster, Bildschirm ruhig)
            var sync = j.Obj("sync");
            if (sync?.LongN("busyMs") is long busy && busy > 0) { c.L($"WarteBereit()   ; Anwendung war {busy} ms beschäftigt ({sync.Str("busyShape")})"); c.UsesBusy = true; }
            if (sync?.Obj("waitWindow")?.Str("title") is { Length: > 0 } wt)
            {
                c.L($"Erwarte(() => WinExist({Q(wt)}), {Q("Fenster „" + OneLine(wt, 60) + "“ erscheint")}, Wartezeit)");
                c.UsesChecks = true; c.CurrentWin = null;
            }
            else foreach (var w in j.Obj("effect")?.Arr("windowsOpened") ?? new JsonArray())
            {
                if (w.Str("kind") is "menu" or "popup" or "dropdown" or "tooltip" || string.IsNullOrWhiteSpace(w.Str("title"))) continue;
                c.L($"WinWait {Q(w.Str("title")!)},, Wartezeit   ; nach dem Schritt erschien dieses Fenster");
                c.CurrentWin = null;
            }
            Checks(c, s);
            // Pause bis zum nächsten Schritt
            if (i + 1 < steps.Count)
            {
                long gap = (steps[i + 1].TFrom - s.TTo) / 1000;
                long readyMs = sync?.LongN("readyMs") ?? 300;
                int ms = o.RealTiming ? (int)Math.Clamp(gap, 150, 3000) : (int)Math.Clamp(readyMs + 150, 150, 3000);
                c.L($"Warte {ms}" + (o.RealTiming ? "" : "   ; bis die Anwendung in der Aufnahme bereit war"));
            }
            c.L();
        }

        var sb = new StringBuilder();
        void H(string s = "") => sb.Append(s).Append("\r\n");
        H("#Requires AutoHotkey v2.0");
        H("#SingleInstance Force");
        H(";");
        H($"; {OneLine(view.Title)}");
        H($"; Erzeugt von StepRecorder am {DateTime.Now:dd.MM.yyyy HH:mm} aus der Aufnahme vom {view.Session.StartUtc.ToLocalTime():dd.MM.yyyy HH:mm}.");
        H($"; {nActions} automatisierte Schritte. Bitte vor dem Einsatz prüfen – Fenster, Positionen und Wartezeiten können abweichen.");
        H("; Abbrechen jederzeit mit Strg+Esc.");
        H(";");
        if (o.Readable)
        {
            H("; ---------------------------------------------------------------------------------------------");
            H("; So ist dieses Skript aufgebaut");
            H(";");
            H("; 1. Einstellungen (gleich unten): Tempo verlangsamt oder beschleunigt alle Pausen, Wartezeit legt fest,");
            H(";    wie viele Sekunden auf ein Fenster gewartet wird, Streng = true bricht ab, wenn etwas anders aussieht als in der Aufnahme.");
            H("; 2. Ablauf(): die aufgezeichneten Schritte der Reihe nach. Jeder Schritt beginnt mit „; --- Schritt N“,");
            H(";    darunter steht in Worten, was passiert, in welchem Fenster und auf welchem Element.");
            H("; 3. Hilfsfunktionen am Ende:");
            H(";    Fenster(…)    wartet auf ein Fenster, holt es nach vorn und liefert sein Handle (hwnd).");
            H(";    Warte(ms)     kurze Pause, skaliert mit Tempo.");
            H(";    WarteBereit() wartet, bis die Anwendung nicht mehr beschäftigt ist (Sanduhr/Ladezeiger).");
            H(";    Erwarte(…)    prüft, ob nach einem Schritt das Erwartete eingetreten ist (z. B. ein Fenster erschien).");
            H(";");
            H("; Begriffe: WinTitle beschreibt, wie AutoHotkey ein Fenster findet (Titel, ahk_exe = Programm, ahk_class = Fensterklasse).");
            H("; ClassNN ist der interne Name eines Steuerelements (z. B. Edit1 = erstes Eingabefeld). ControlClick/ControlSetText bedienen");
            H("; Elemente direkt, ohne die Maus zu bewegen – das ist robuster als Klicks auf Koordinaten. Koordinaten (x, y) zählen ab der");
            H("; linken oberen Ecke des Fensters in physischen Pixeln. Zeilen mit „;“ am Anfang sind Kommentare und werden nicht ausgeführt.");
            H("; ---------------------------------------------------------------------------------------------");
            H(";");
        }
        H("; Koordinaten sind physische Pixel wie in der Aufnahme (Per-Monitor-DPI):");
        H("DllCall(\"SetThreadDpiAwarenessContext\", \"ptr\", -4, \"ptr\")");
        H("SetTitleMatchMode 2");
        H("SetControlDelay 50");
        H("SetKeyDelay 30, 20");
        H("SendMode \"Input\"");
        H("CoordMode \"Mouse\", \"Window\"");
        H("CoordMode \"Pixel\", \"Window\"");
        H();
        H("global Tempo := 1.0      ; alle Pausen skalieren (2.0 = doppelt so langsam)");
        H("global Wartezeit := 10   ; Sekunden, die auf Fenster gewartet wird");
        H("global Streng := false   ; true = bei nicht erfüllter Erwartung abbrechen, false = nur Hinweis");
        foreach (var v in c.Vars.Distinct()) H(v);
        H();
        H("^Esc::ExitApp");
        H();
        H("Ablauf()");
        H("ExitApp");
        H();
        H("Ablauf() {");
        H("    hwnd := 0");
        sb.Append(c.Body);
        H("}");
        H();
        H("; Wartet auf das Fenster, holt es nach vorn und liefert sein Handle.");
        H("Fenster(winTitle) {");
        H("    if !WinWait(winTitle,, Wartezeit)");
        H("        throw Error(\"Fenster nicht gefunden: \" winTitle)");
        H("    WinActivate winTitle");
        H("    WinWaitActive winTitle,, Wartezeit");
        H("    return WinExist(winTitle)");
        H("}");
        H();
        H("Warte(ms) => Sleep(Round(ms * Tempo))");
        if (c.UsesGlide) foreach (var l in GlideFunction) H(l);
        if (c.UsesChecks) foreach (var l in CheckFunction) H(l);
        if (c.UsesBusy) foreach (var l in BusyFunction) H(l);
        if (c.UsesImages) foreach (var l in ImageFunction) H(l);
        if (c.UsesExplorer) foreach (var l in ExplorerFunction) H(l);
        if (c.UsesListView) foreach (var l in ListViewFunction) H(l);
        if (c.UsesMsaa) foreach (var l in MsaaFunction) H(l);
        if (c.Masked > 0 && res != null) res.Notices.Add($"{c.Masked} maskierte Eingaben werden beim Start des Skripts abgefragt.");
        return sb.ToString();
    }

    const string NL = "\r\n";

    static readonly string[] CheckFunction =
    {
        "",
        "; Wartet, bis pruefung() wahr ist; sonst Hinweis (oder Abbruch, wenn Streng).",
        "Erwarte(pruefung, beschreibung, sekunden := 5) {",
        "    ende := A_TickCount + sekunden * 1000 * Tempo",
        "    loop {",
        "        try if pruefung()",
        "            return true",
        "        if A_TickCount > ende",
        "            break",
        "        Sleep 100",
        "    }",
        "    if Streng",
        "        throw Error(\"Erwartung nicht erfüllt: \" beschreibung)",
        "    ToolTip \"Nicht erfüllt: \" beschreibung",
        "    SetTimer () => ToolTip(), -4000",
        "    return false",
        "}"
    };

    static readonly string[] BusyFunction =
    {
        "",
        "; Wartet, solange der Mauszeiger „beschäftigt“ zeigt (Sanduhr), höchstens sekunden.",
        "WarteBereit(sekunden := 30) {",
        "    Sleep 150",
        "    ende := A_TickCount + sekunden * 1000",
        "    while (A_Cursor = \"Wait\" || A_Cursor = \"AppStarting\") && A_TickCount < ende",
        "        Sleep 100",
        "}"
    };

    static readonly string[] ImageFunction =
    {
        "",
        "; Rückfall: Bildausschnitt auf dem ganzen Bildschirm suchen und in die Mitte klicken.",
        "KlickBild(datei, variation := 40) {",
        "    CoordMode \"Pixel\", \"Screen\"",
        "    CoordMode \"Mouse\", \"Screen\"",
        "    ok := ImageSearch(&fx, &fy, SysGet(76), SysGet(77), SysGet(76) + SysGet(78), SysGet(77) + SysGet(79), \"*\" variation \" \" A_ScriptDir \"\\\" datei)",
        "    if ok",
        "        Click fx + 24, fy + 24",
        "    CoordMode \"Pixel\", \"Window\"",
        "    CoordMode \"Mouse\", \"Window\"",
        "    return ok",
        "}"
    };

    /// <summary>Kommentierte Alternativen: Menübefehl, Listenauswahl, Bildsuche.</summary>
    static void Alternatives(Ctx c, ViewStep s, AhkOptions o)
    {
        var j = s.Json;
        if (!c.MenuDone && j.Obj("menuPath") is { } mp && mp.Arr("items") is { Count: > 0 } items)
        {
            string path = string.Join(", ", items.Select(x => Q(x?.GetValue<string>() ?? "")));
            if (mp.Bool("win32Menu") && items.Count >= 2 && mp.Str("source") is null or "menuBar") c.L($"; Alternative (klassisches Menü): MenuSelect hwnd,, {path}");
            else c.L($"; Menüpfad: {string.Join(" › ", items.Select(x => x?.GetValue<string>()))}" + (mp.Str("accelerator") is { } acc ? $" · Tastenkürzel {acc}" : ""));
            // Kontextmenü o. Ä.: Befehl direkt an das Besitzerfenster (WM_COMMAND)
            if (mp.LongN("commandId") is long cid && OwnerWinTitle(mp.Obj("owner")) is { } owt)
                c.L($"; Alternative (Befehls-ID {cid}): PostMessage 0x111, {cid}, 0,, {Q(owt)}   ; WM_COMMAND an das Fenster, dem das Menü gehört");
        }
        if (!c.ChooseDone && j.Obj("choose") is { } ch && ch.Str("item") is { } item)
        {
            string? kind32 = ch.Obj("win32")?.Str("kind");
            if (ch.Str("win32Control") is { } cc && !Volatile(cc)) c.L($"; Alternative: ControlChooseString {Lit(c, item)}, {Q(cc)}, hwnd");
            else if (kind32 == "treeView" && ch.Obj("win32")?.StrList("path") is { Count: > 0 } tp) c.L($"; Baumeintrag: {string.Join(" › ", tp.Select(x => OneLine(x, 40)))} (TreeView {ch.Obj("win32")?.Str("classNN")})");
            else c.L($"; Auswahl: „{OneLine(item, 50)}“" + (ch.LongN("index") is long ix ? $" (Eintrag {ix} von {ch.LongN("of")})" : "") + (ch.Obj("container")?.Str("name") is { } cn ? $" in „{OneLine(cn, 40)}“" : ""));
        }
        // Auswahl per Tastatur in einer Liste: Ergebnis direkt wählen
        if (s.Kind is "key" or "hotkey" && j.Obj("list") is { } kl && kl.Obj("after") is { } la && la.Str("classNN") is { } lnn && !Volatile(lnn))
        {
            if (la.Str("kind") is "comboBox" or "listBox" && la.Str("selText") is { Length: > 0 } lt && lt != "[ausgelassen]") c.L($"; Alternative: ControlChooseString {Lit(c, lt)}, {Q(lnn)}, hwnd   ; Auswahl danach");
            else if (la.Str("kind") == "tab" && la.LongN("selIndex") is long ti) c.L($"; Alternative: ControlChooseIndex {ti}, {Q(lnn)}, hwnd   ; Registerkarte danach");
        }
        // MSAA-Standardaktion (DoDefaultAction) als Alternative zum Klick
        if (s.Kind is "click" or "doubleClick" && !c.MenuDone && !c.ChooseDone && W32(s)?.Obj("msaa") is { } ms && ms.Str("defaultAction") is { Length: > 0 } da && WindowPoint(W32(s)) is { } mpt)
        {
            c.L($"; Alternative (MSAA, Standardaktion „{OneLine(da, 30)}“" + (ms.Str("roleText") is { } rt ? $", Rolle {OneLine(rt, 30)}" : "") + $"): MsaaStandardAktion({mpt.x}, {mpt.y}, hwnd)");
            if (!c.Snippet) c.UsesMsaa = true;
        }
        // Wirkung im Anwendungskontext
        if (j.Obj("browser")?.Str("urlAfter") is { } ua && ua != c.LastUrl)
        {
            c.L($"; Danach geöffnete Seite: {ua}");
            c.L($"; Alternative: Run {Q(ua)}");
            c.LastUrl = ua;
        }
        if (j.Obj("explorer")?.Str("folderAfter") is { } fa && fa != c.LastExplorer) { c.L($"; Danach Explorer-Ordner: {fa}"); c.LastExplorer = fa; }
        if (j.Obj("office")?.Obj("after") is { } oa)
            c.L("; Danach in Office: " + string.Join(", ", new[] { ("workbook", "Mappe"), ("sheet", "Blatt"), ("cell", "Zelle"), ("selection", "Auswahl"), ("document", "Dokument") }
                .Where(k => oa.Str(k.Item1) != null).Select(k => $"{k.Item2} {OneLine(oa.Str(k.Item1)!, 80)}")));
        if (j.Obj("data")?.Obj("fileDialog") is { } fd) c.L($"; Dateidialog: Ordner „{fd.Str("folder")}“" + (fd.Str("fileName") is { } fn ? $", Datei „{fn}“" : ""));
        if (j.Obj("data")?.Obj("pasteFrom") is { } pf) c.L($"; Eingefügt aus der Zwischenablage, kopiert in {pf.Str("process")}" + (pf.Str("title") is { } pt ? $" („{OneLine(pt, 40)}“)" : ""));
        if (o.ImageDir != null && !c.Snippet && s.Kind is "click" or "doubleClick" or "rightClick" && ImageFor(s, o.ImageDir) is { } file)
        {
            c.L($"; Alternative (Bildsuche): KlickBild({Q("bilder\\" + file)})");
            c.UsesImages = true;
        }
    }

    /// <summary>Erwartungen nach dem Schritt aus dem beobachteten Zustand (Titel, Häkchen, Texte) – höchstens zwei.</summary>
    static void Checks(Ctx c, ViewStep s)
    {
        if (c.Snippet || s.Json.Obj("verify") is not { } v) return;
        int n = 0;
        foreach (var ch in v.Arr("changes")?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
        {
            if (n >= 2) break;
            string? nn = ch.Str("c");
            if (nn == null || nn.StartsWith("WindowsForms10.") || nn.StartsWith("HwndWrapper[") || nn.StartsWith("Afx:")) continue;
            if (ch.Str("checked") is { } st && st != "mixed")
            { c.L($"Erwarte(() => ControlGetChecked({Q(nn)}, hwnd) = {(st == "checked" ? 1 : 0)}, {Q($"„{OneLine(ch.Str("text") ?? nn, 40)}“ ist {(st == "checked" ? "angehakt" : "nicht angehakt")}")})"); n++; }
            else if (ch.Str("to") is { Length: > 0 and <= 80 } to)
            { c.L($"Erwarte(() => InStr(ControlGetText({Q(nn)}, hwnd), {Q(to)}), {Q($"{nn} zeigt „{OneLine(to, 40)}“")})"); n++; }
            else if (ch.Str("appeared") is { Length: > 0 and <= 80 } ap)
            { c.L($"Erwarte(() => InStr(ControlGetText({Q(nn)}, hwnd), {Q(ap)}), {Q($"„{OneLine(ap, 40)}“ erscheint")})"); n++; }
        }
        // Statusleiste (msctls_statusbar32): geänderter Text als Wartebedingung
        if (n < 2 && v.Obj("statusBar")?.Arr("changed")?.OfType<JsonObject>().FirstOrDefault(x => x.Str("to") is { Length: > 0 and <= 80 } t && t != "[ausgelassen]") is { } sbc)
        {
            int part = Math.Max(1, sbc.Int("part", 1));
            string to = sbc.Str("to")!;
            c.L($"Erwarte(() => InStr(StatusBarGetText({part}, hwnd), {Q(to)}), {Q($"Statusleiste zeigt „{OneLine(to, 40)}“")})");
            c.L($"; oder: StatusBarWait {Q(to)}, Wartezeit, {part}, hwnd");
            n++;
        }
        if (n == 0 && v.Obj("titleChanged")?.Str("to") is { Length: > 0 } title && v.Str("exe") is { } exe)
        { c.L($"Erwarte(() => WinExist({Q(title + " ahk_exe " + exe)}), {Q($"Fenstertitel „{OneLine(title, 50)}“")})"); n++; }
        if (n > 0) c.UsesChecks = true;
    }

    /// <summary>Schreibt einen 48×48-Ausschnitt um den Klickpunkt (unmarkiertes Vorher-Bild) und liefert den Dateinamen.</summary>
    public static string? ImageFor(ViewStep s, string dir, SessionView? view = null)
    {
        view ??= _view;
        if (view == null || s.PreFrame == null || s.Json.Obj("position") == null) return null;
        if (s.Json.Obj("target")?.Obj("el")?.Bool("password") == true || s.Json.Obj("target")?.Obj("win32")?.Obj("control")?.Str("textWithheld") == "password") return null;
        try
        {
            using var img = StepImages.Render(view, s, "before", "original", annotate: false);
            if (img?.Marker is not { } mk) return null;
            var r = System.Drawing.Rectangle.Intersect(new System.Drawing.Rectangle(mk.X - 24, mk.Y - 24, 48, 48), new System.Drawing.Rectangle(0, 0, img.Bitmap.Width, img.Bitmap.Height));
            if (r.Width < 16 || r.Height < 16) return null;
            Directory.CreateDirectory(dir);
            string file = $"schritt-{s.Number:00}.png";
            using var crop = img.Bitmap.Clone(r, img.Bitmap.PixelFormat);
            crop.Save(Path.Combine(dir, file), System.Drawing.Imaging.ImageFormat.Png);
            return file;
        }
        catch { return null; }
    }

    [ThreadStatic] static SessionView? _view;

    /// <summary>Hilfsfunktion im Skript: Maus mit gedrückter Taste in kleinen Schritten bewegen (Anwendungen erkennen so das Ziehen).</summary>
    static readonly string[] GlideFunction =
    {
        "",
        "; Bewegt die Maus in kleinen Schritten von (x1, y1) nach (x2, y2) in etwa ms Millisekunden.",
        "Gleite(x1, y1, x2, y2, ms) {",
        "    n := Max(1, Min(40, Round(Sqrt((x2 - x1) ** 2 + (y2 - y1) ** 2) / 12)))",
        "    Loop n {",
        "        MouseMove Round(x1 + (x2 - x1) * A_Index / n), Round(y1 + (y2 - y1) * A_Index / n)",
        "        Sleep Max(5, Round(ms * Tempo / n))",
        "    }",
        "}"
    };

    /// <summary>AutoHotkey-v2-Code für einen einzelnen Schritt (zum Kopieren im Viewer).</summary>
    public static string Snippet(ViewStep s)
    {
        var c = new Ctx { Snippet = true };
        bool ok = s.Kind switch
        {
            "click" or "doubleClick" or "tripleClick" or "rightClick" => Click(c, s), "drag" => Drag(c, s), "scroll" => Scroll(c, s), "hover" => Hover(c, s),
            "type" => Type(c, s), "hotkey" => Hotkey(c, s), "key" => Key(c, s), "windowSwitch" => Switch(c, s), _ => false
        };
        var head = new StringBuilder();
        foreach (var v in c.Vars.Distinct()) head.Append(v.Replace("global ", "")).Append(NL);
        var body = string.Join(NL, c.Body.ToString().Split(NL).Select(l => l.StartsWith("    ") ? l[4..] : l)).TrimEnd();
        if (c.UsesGlide) body += NL + NL + string.Join(NL, GlideFunction.Select(l => l.Replace("ms * Tempo", "ms")));
        if (c.UsesExplorer) body += NL + string.Join(NL, ExplorerFunction);
        if (c.UsesListView) body += NL + string.Join(NL, ListViewFunction);
        return ok ? head + body + NL : "";
    }

    /// <summary>Kontextkommentare eines Schritts (Explorer, Browser, Office) wie im Skript – für Viewer und Tests.</summary>
    internal static string ContextCommentsFor(ViewStep s)
    {
        var c = new Ctx { Snippet = true };
        ContextComments(c, s);
        return string.Join(NL, c.Body.ToString().Split(NL).Select(l => l.StartsWith("    ") ? l[4..] : l)).TrimEnd();
    }

    // ------------------------------------------------------------------------------------------

    static JsonObject? W32(ViewStep s, string which = "target") => s.Json.Obj(which)?.Obj("win32");

    /// <summary>WinTitle für den Schritt: Dialoge (#32770) mit Titel, sonst ahk_class + ahk_exe; ohne Win32-Daten aus dem Fensterkontext.</summary>
    static string? WinTitle(ViewStep s)
    {
        if (W32(s) is { } w && w.Obj("ahk") is { } ahk)
        {
            var cls = w.Obj("window")?.Str("class");
            if (cls == "#32770" && ahk.Str("winTitleFull") is { } full) return full;
            // Klassennamen, die sich bei jedem Programmstart ändern (WinForms, WPF, MFC): Titel + ahk_exe
            if (Volatile(cls) && w.Obj("window") is { } wi && wi.Str("exe") is { } exe)
                return (wi.Str("title") is { Length: > 0 } t ? t + " " : "") + "ahk_exe " + exe;
            if (ahk.Str("winTitle") is { Length: > 0 } wt) return wt;
        }
        if (s.Json.Obj("ctx") is { } cx)
        {
            var t = cx.Str("title"); var p = cx.Str("process");
            if (!string.IsNullOrEmpty(p)) return (string.IsNullOrEmpty(t) ? "" : t + " ") + "ahk_exe " + p;
            if (!string.IsNullOrEmpty(t)) return t;
        }
        return null;
    }

    static void Activate(Ctx c, ViewStep s) => ActivateTitle(c, WinTitle(s));

    static void ActivateTitle(Ctx c, string? wt)
    {
        if (wt == null) return;
        if (c.Snippet) { c.L($"WinActivate {Q(wt)}"); c.L($"hwnd := WinWaitActive({Q(wt)},, 5)"); c.CurrentWin = wt; return; }
        if (wt != c.CurrentWin) { c.L($"hwnd := Fenster({Q(wt)})"); c.CurrentWin = wt; }
        else c.L("WinActivate hwnd");
    }

    static (int x, int y)? WindowPoint(JsonObject? w32)
        => w32?.Obj("point")?.Arr("window") is { Count: 2 } a ? (a[0]!.GetValue<int>(), a[1]!.GetValue<int>()) : null;

    /// <summary>Klassennamen mit Laufzeitanteil (WinForms „WindowsForms10.…_r13_ad1“, WPF „HwndWrapper[…]“, MFC „Afx:…“).</summary>
    public static bool Volatile(string? cls) => cls != null && (cls.StartsWith("WindowsForms10.", StringComparison.Ordinal) || cls.StartsWith("HwndWrapper[", StringComparison.Ordinal)
        || cls.StartsWith("Afx:", StringComparison.Ordinal) || cls.StartsWith("ATL:", StringComparison.Ordinal));

    /// <summary>Wie das Steuerelement in ControlClick/ControlFocus angesprochen wird: ClassNN, bei wechselnden Klassen sein Text.</summary>
    static string? ControlRef(JsonObject? ctl)
    {
        if (ctl?.Str("classNN") is not { } nn) return null;
        if (!Volatile(ctl.Str("class"))) return nn;
        return ctl.Str("text") is { Length: > 0 } t && !t.Contains('\n') ? t : null;
    }

    /// <summary>Klassisches Win32-Steuerelement, das ControlClick zuverlässig bedient.</summary>
    static bool ClassicControl(JsonObject? control)
        => control?.Str("classNN") != null && control.Arr("point") != null && !Win32Detail.IsContentSurface(control.Str("class") ?? "")
           && control.Str("class") is not ("DirectUIHWND" or "SysListView32" or "SysTreeView32" or "ToolbarWindow32" or "ReBarWindow32" or "SysTabControl32");

    static bool Click(Ctx c, ViewStep s)
    {
        var mods = s.Json.Obj("input")?.StrList("mods") ?? new();
        if (mods.Count == 0 && s.Kind == "click" && MenuCommand(c, s)) return c.MenuDone = true;
        if (mods.Count == 0 && s.Kind is "click" or "doubleClick" && ChooseItem(c, s)) return c.ChooseDone = true;
        if (mods.Count == 0 && s.Kind is "click" or "doubleClick" && ExplorerNavigate(c, s)) return true;
        Activate(c, s);
        ModsDown(c, mods);
        bool ok = ClickCore(c, s);
        ModsUp(c, mods);
        return ok;
    }

    static bool ClickCore(Ctx c, ViewStep s)
    {
        var w = W32(s);
        var inp = s.Json.Obj("input");
        string button = s.Kind == "rightClick" ? "Right" : inp?.Str("button") == "middle" ? "Middle" : "Left";
        int count = s.Kind switch { "doubleClick" => 2, "tripleClick" => 3, _ => 1 };
        var wp = WindowPoint(w);
        var ctl = w?.Obj("control");
        // gedrückt halten: Drücken, warten, loslassen
        if (count == 1 && inp?.Bool("longPress") == true && wp != null)
        {
            long ms = inp.Long("pressUs") / 1000;
            c.L($"Click \"{wp.Value.x} {wp.Value.y} {button} Down\"   ; {ms} ms gedrückt halten");
            c.L($"Warte {ms}");
            c.L($"Click \"{wp.Value.x} {wp.Value.y} {button} Up\"");
            return true;
        }
        if (ClassicControl(ctl) && ControlRef(ctl) is { } cref && ctl!.Arr("point") is { Count: 2 } cp)
        {
            c.L($"ControlClick {Q(cref)}, hwnd,, {Q(button)}, {count}, \"NA x{cp[0]} y{cp[1]}\"" + (cref != ctl.Str("text") && ctl.Str("text") is { Length: > 0 } t ? $"   ; „{OneLine(t, 50)}“" : cref != ctl.Str("classNN") ? $"   ; {ctl.Str("classNN")}" : ""));
            if (wp != null) c.L($"; Alternative: Click \"{wp.Value.x} {wp.Value.y}{(button == "Right" ? " Right" : "")}{(count > 1 ? " " + count : "")}\"");
            return true;
        }
        if (wp != null)
        {
            c.L($"Click \"{wp.Value.x} {wp.Value.y}{(button == "Right" ? " Right" : "")}{(count > 1 ? " " + count : "")}\"   ; Fensterkoordinaten");
            if (ctl?.Str("classNN") is { } nn && ctl.Arr("point") is { Count: 2 } cp2) c.L($"; Alternative: ControlClick {Q(nn)}, hwnd,, {Q(button)}, {count}, \"NA x{cp2[0]} y{cp2[1]}\"");
            if (w?.Str("pixel") is { } px) c.L($"; Prüfung: PixelGetColor({wp.Value.x}, {wp.Value.y}) = {px} in der Aufnahme");
            return true;
        }
        // ohne Win32-Daten: Bildschirmposition
        if (s.Json.Obj("position") is { } pos) { c.L($"CoordMode \"Mouse\", \"Screen\""); c.L($"Click \"{pos.Int("x")} {pos.Int("y")}{(button == "Right" ? " Right" : "")}{(count > 1 ? " " + count : "")}\"   ; Monitor {pos.Str("m")} – ungenau"); c.L("CoordMode \"Mouse\", \"Window\""); return true; }
        return false;
    }

    static bool Drag(Ctx c, ViewStep s)
    {
        Activate(c, s);
        var a = WindowPoint(W32(s));
        var inp = s.Json.Obj("input");
        var pos = s.Json.Obj("position");
        if (a == null || inp == null || pos == null) return false;
        int ox = a.Value.x - pos.Int("x"), oy = a.Value.y - pos.Int("y");   // Monitor- → Fensterkoordinaten
        (int x, int y) end = WindowPoint(W32(s, "endTarget")) ?? (s.Json.Obj("endPosition") is { } ep ? (ep.Int("x") + ox, ep.Int("y") + oy) : a.Value);
        string button = inp.Str("button") switch { "secondary" => "Right", "middle" => "Middle", _ => "Left" };
        var mods = inp.StrList("mods");
        long holdBefore = inp.Long("holdBeforeMoveUs") / 1000, holdEnd = inp.Long("holdAtEndUs") / 1000;
        c.L($"; Ziehen mit gedrückter {(button == "Right" ? "rechter" : button == "Middle" ? "mittlerer" : "linker")} Taste: {inp.Long("durationUs") / 1000} ms, Weg {inp.Long("distancePx")} px");
        ModsDown(c, mods);
        c.L($"Click \"{a.Value.x} {a.Value.y} {button} Down\"");
        c.L(holdBefore >= 100 ? $"Warte {holdBefore}   ; vor der Bewegung gedrückt halten" : "Sleep 50");
        var path = inp.Arr("path") ?? new JsonArray();
        (int x, int y) prev = a.Value;
        for (int i = 1; i < path.Count; i++)
        {
            int x = path[i]![0]!.GetValue<int>() + ox, y = path[i]![1]!.GetValue<int>() + oy;
            long dt = path[i] is JsonArray { Count: >= 3 } pi && path[i - 1] is JsonArray { Count: >= 3 } pp ? pi[2]!.GetValue<long>() - pp[2]!.GetValue<long>() : 200;
            if (i == 1) dt -= holdBefore;                       // Haltezeit wurde schon gewartet
            if (i == path.Count - 1) dt -= holdEnd;             // Verweilen über dem Ziel folgt gesondert
            c.L($"Gleite {prev.x}, {prev.y}, {x}, {y}, {Math.Clamp(dt, 30, 3000)}");
            prev = (x, y);
        }
        if (path.Count < 2) c.L($"Gleite {prev.x}, {prev.y}, {end.x}, {end.y}, 300");
        c.UsesGlide = true;
        if (holdEnd >= 100) c.L($"Warte {holdEnd}   ; über dem Ziel verweilen");
        c.L($"Click \"{end.x} {end.y} {button} Up\"");
        ModsUp(c, mods);
        c.L($"; Alternative (gerade Linie): MouseClickDrag {Q(button)}, {a.Value.x}, {a.Value.y}, {end.x}, {end.y}, 10");
        return true;
    }

    static readonly Dictionary<string, string> ModKeys = new(StringComparer.OrdinalIgnoreCase)
    { ["ctrl"] = "Ctrl", ["shift"] = "Shift", ["alt"] = "Alt", ["win"] = "LWin", ["altGr"] = "RAlt" };

    static void ModsDown(Ctx c, List<string> mods) { foreach (var m in mods) if (ModKeys.TryGetValue(m, out var k)) c.L($"Send \"{{{k} down}}\""); }
    static void ModsUp(Ctx c, List<string> mods) { foreach (var m in Enumerable.Reverse(mods)) if (ModKeys.TryGetValue(m, out var k)) c.L($"Send \"{{{k} up}}\""); }

    static bool Scroll(Ctx c, ViewStep s)
    {
        Activate(c, s);
        var inp = s.Json.Obj("input");
        double notches = inp?.Dbl("notches") ?? 0;
        int n = Math.Max(1, (int)Math.Round(Math.Abs(notches)));
        bool h = inp?.Str("axis") == "h";
        string dir = h ? (notches > 0 ? "WheelRight" : "WheelLeft") : (notches > 0 ? "WheelUp" : "WheelDown");
        var mods = Mods(inp?.StrList("mods") ?? new());
        var wp = WindowPoint(W32(s));
        if (mods.Length > 0)
        {
            if (wp != null) c.L($"MouseMove {wp.Value.x}, {wp.Value.y}");
            c.L($"Send \"{mods}{{{dir} {n}}}\"");
        }
        else c.L(wp != null ? $"Click \"{wp.Value.x} {wp.Value.y} {dir} {n}\"" : $"Click \"{dir} {n}\"");
        return true;
    }

    static bool Hover(Ctx c, ViewStep s)
    {
        Activate(c, s);
        var wp = WindowPoint(W32(s));
        if (wp == null) return false;
        c.L($"MouseMove {wp.Value.x}, {wp.Value.y}   ; zeigen (Menü/Tooltip abwarten)");
        return true;
    }

    static bool Type(Ctx c, ViewStep s)
    {
        Activate(c, s);
        var w = W32(s);
        if (w?.Obj("control") is { } fc && !Win32Detail.IsContentSurface(fc.Str("class") ?? ""))
        {
            if (ControlRef(fc) is { } nn) c.L($"ControlFocus {Q(nn)}, hwnd");
            else if (fc.Str("classNN") is { } vnn) c.L($"; Fokus lag auf {vnn} (Klassenname wechselt je Start – Feld vorher anklicken)");
        }
        var inp = s.Json.Obj("input");
        if (inp == null) return false;
        if (inp.Bool("masked") && (inp.Arr("tokens") is null or { Count: 0 }))
        {
            string v = NewSecret(c, s);
            c.L($"SendText {v}   ; maskierte Eingabe – wird beim Start abgefragt");
            return true;
        }
        var text = new StringBuilder();
        // Eingabe als Parameter oben im Skript, wenn der ganze Text eine Eingabe ist
        string? pn = null;
        if (!c.Snippet && c.Params.TryGetValue(s.Id, out var prm) && prm.Str("value") is { Length: > 0 } pval && inp.Str("text") == pval && !prm.Bool("secret"))
        {
            pn = prm.Str("name")!;
            if (c.DeclaredParams.Add(pn)) c.Vars.Add($"global {pn} := {Lit(c, pval)}   ; Parameter aus Schritt {s.Number}, Feld „{OneLine(prm.Str("field") ?? "", 40)}“");
        }
        // Erkanntes Eingabefeld (Schreibmarke gelesen) bzw. Dateinamenfeld eines Dateidialogs: Inhalt direkt setzen
        if (DirectEdit(c, s, inp, pn)) return true;
        if (pn != null)
        {
            c.L($"SendText {pn}");
            if (inp.Obj("terminator") is { } term0) c.L($"Send \"{Mods(term0.StrList("mods"))}{{{AhkKey(term0.Str("key") ?? "Enter")}}}\"");
            if (inp.Str("valueAfter") is { } va0) c.L($"; Feldinhalt danach in der Aufnahme: „{OneLine(va0, 80)}“");
            return true;
        }
        void FlushText() { if (text.Length > 0) { c.L($"SendText {Lit(c, text.ToString())}"); text.Clear(); } }
        foreach (var tok in inp.Arr("tokens") ?? new JsonArray())
        {
            if (tok is JsonValue v && v.TryGetValue<string>(out var str)) { text.Append(str); continue; }
            if (tok is not JsonObject o) continue;
            if (o.ContainsKey("masked")) { FlushText(); var sv = NewSecret(c, s); c.L($"SendText {sv}   ; maskierte Eingabe – wird beim Start abgefragt"); }
            else if (o.Str("placeholder") is { } ph) { FlushText(); var pv = "Wert_" + Ident(ph); c.Vars.Add($"global {pv} := \"\"   ; Platzhalter „{ph}“ – bitte ausfüllen"); c.L($"SendText {pv}"); }
            else if (o.Str("key") is { } k)
            {
                FlushText();
                int n = Math.Max(1, o.Int("n", 1));
                c.L($"Send \"{Mods(o.StrList("mods"))}{{{AhkKey(k)}{(n > 1 ? " " + n : "")}}}\"");
            }
            else if (o.Str("uncertain") is { } u) { FlushText(); c.L($"; unsicher erfasst: {OneLine(u)}"); }
            else if (o.Str("unreviewed") is { }) { FlushText(); c.L("; (ungeprüfte Eingabe ausgelassen)"); }
        }
        FlushText();
        if (inp.Obj("terminator") is { } term) c.L($"Send \"{Mods(term.StrList("mods"))}{{{AhkKey(term.Str("key") ?? "Enter")}}}\"");
        if (inp.Str("valueAfter") is { } va) c.L($"; Feldinhalt danach in der Aufnahme: „{OneLine(va, 80)}“");
        return true;
    }

    static string NewSecret(Ctx c, ViewStep s)
    {
        c.Masked++;
        string v = "Geheim" + c.Masked;
        string where = s.Json.Obj("ctx")?.Str("title") is { } t ? $" ({OneLine(t, 40)})" : "";
        c.Vars.Add($"global {v} := InputBox(\"Maskierte Eingabe aus Schritt {s.Number}{Esc(where)}:\", \"StepRecorder-Ablauf\", \"Password\").Value");
        return v;
    }

    static bool Hotkey(Ctx c, ViewStep s)
    {
        Activate(c, s);
        var inp = s.Json.Obj("input");
        var keys = inp?.StrList("keys") ?? new();
        if (keys.Count == 0) return false;
        string mods = Mods(keys.Take(keys.Count - 1).ToList());
        string key = keys[^1];
        int n = Math.Max(1, inp!.Int("count", 1));
        string k = key.Length == 1 && char.IsLetterOrDigit(key[0]) ? key.ToLowerInvariant() : "{" + AhkKey(key) + "}";
        for (int i = 0; i < n; i++) c.L($"Send \"{mods}{k}\"" + (i == 0 && inp.Str("commonMeaning") is { } cm ? $"   ; {cm}" : ""));
        return true;
    }

    static bool Key(Ctx c, ViewStep s)
    {
        Activate(c, s);
        var inp = s.Json.Obj("input");
        if (inp?.Str("key") is not { } key) return false;
        int n = Math.Max(1, inp.Int("count", 1));
        c.L($"Send \"{Mods(inp.StrList("mods"))}{{{AhkKey(key)}{(n > 1 ? " " + n : "")}}}\"");
        return true;
    }

    static bool Switch(Ctx c, ViewStep s)
    {
        var wt = WinTitle(s);
        if (wt == null) return false;
        if (c.Snippet) { c.L($"WinActivate {Q(wt)}"); return true; }
        c.L($"hwnd := Fenster({Q(wt)})"); c.CurrentWin = wt;
        return true;
    }

    static bool Wait(Ctx c, ViewStep s)
    {
        long ms = s.Json.Obj("input")?.Long("durationUs") / 1000 ?? 0;
        c.L($"Warte {Math.Clamp(ms, 100, 30000)}   ; Wartezeit aus der Aufnahme");
        return true;
    }

    // ------------------------------------------------------------------------------------------
    // Anwendungskontext: Menü-Befehls-IDs, Listenauswahl, Explorer, Browser, Office, Eingabefelder
    // ------------------------------------------------------------------------------------------

    /// <summary>WinTitle des Fensters, dem ein Menü gehört (bei wechselnden Klassennamen Titel + ahk_exe).</summary>
    static string? OwnerWinTitle(JsonObject? owner)
    {
        if (owner == null) return null;
        if (Volatile(owner.Str("class")) && owner.Str("exe") is { } exe) return (owner.Str("title") is { Length: > 0 } t ? t + " " : "") + "ahk_exe " + exe;
        return owner.Str("winTitle") is { Length: > 0 } wt ? wt : null;
    }

    /// <summary>Klassisches Menü mit Pfad ab Menüleiste/Systemmenü: MenuSelect statt Klick; Befehls-ID als WM_COMMAND-Alternative.</summary>
    static bool MenuCommand(Ctx c, ViewStep s)
    {
        if (s.Json.Obj("menuPath") is not { } mp || !mp.Bool("win32Menu") || mp.Bool("submenu") || mp.Bool("disabled")) return false;
        string? src = mp.Str("source");
        var path = mp.StrList("win32Path");
        var pos = mp.Arr("win32Positions")?.Select(x => x?.GetValue<int>() ?? 0).ToList() ?? new();
        if (OwnerWinTitle(mp.Obj("owner")) is not { } owt || path.Count == 0 || path.Any(p => p.Length == 0)) return false;
        if (!(src == "menuBar" && path.Count >= 2 || src == "systemMenu")) return false;
        ActivateTitle(c, owt);
        var args = (src == "systemMenu" ? new[] { Q("0&") } : Array.Empty<string>()).Concat(path.Select(Q));
        long? id = mp.LongN("commandId");
        c.L($"MenuSelect hwnd,, {string.Join(", ", args)}   ; klassisches Menü {string.Join(" › ", path.Select(p => OneLine(p, 30)))}" + (id is long i0 ? $" (Befehls-ID {i0})" : ""));
        if (id is long cid) c.L($"; Alternative: PostMessage 0x111, {cid}, 0,, hwnd   ; WM_COMMAND direkt an das Fenster");
        if (pos.Count == path.Count)
            c.L($"; Alternative (nach Position, sprachunabhängig): MenuSelect hwnd,, {string.Join(", ", (src == "systemMenu" ? new[] { Q("0&") } : Array.Empty<string>()).Concat(pos.Select(p => Q((p + 1) + "&"))))}");
        if (WindowPoint(W32(s)) is { } wp) c.L($"; Aufnahme: Klick im geöffneten Menü bei {wp.x}, {wp.y} (Menüfenster)");
        return true;
    }

    /// <summary>
    /// Auswahl in Win32-Listen ohne Klickposition: ControlChooseString (ComboBox/ListBox), ControlChooseIndex (Registerkarte),
    /// ListView-Zeile per Tastatur – jeweils mit dem aufgezeichneten Klick als Rückfall.
    /// </summary>
    static bool ChooseItem(Ctx c, ViewStep s)
    {
        if (s.Json.Obj("choose")?.Obj("win32") is not { } ch || ch.Str("classNN") is not { } nn || Volatile(nn)) return false;
        string? kind = ch.Str("kind"), text = ch.Str("text");
        if (text == "[ausgelassen]") text = null;
        long? index = ch.LongN("index");
        string info = (text != null ? $"„{OneLine(text, 50)}“" : "") + (index != null ? $" (Eintrag {index}" + (ch.LongN("count") is long n ? $" von {n})" : ")") : "");
        bool foreignWin = ch.Str("winTitle") != null;   // aufgeklappte Liste: Auswahl im Fenster des Kombinationsfelds
        void Fallback()
        {
            if (foreignWin) { CommentedClick(c, s, "Klick in der aufgeklappten Liste"); return; }
            c.L("catch {");
            c.Indent += "    "; ClickCore(c, s); c.Indent = c.Indent[..^4];
            c.L("}");
        }
        switch (kind)
        {
            case "comboBox" or "listBox" when s.Kind == "click" && (text != null || index != null):
                ActivateTitle(c, ch.Str("winTitle") ?? WinTitle(s));
                string cmd = text != null ? $"ControlChooseString {Lit(c, text)}, {Q(nn)}, hwnd" : $"ControlChooseIndex {index}, {Q(nn)}, hwnd";
                c.L((foreignWin ? "" : "try ") + cmd + $"   ; Auswahl {info}");
                Fallback();
                if (text != null && index != null) c.L($"; Alternative: ControlChooseIndex {index}, {Q(nn)}, hwnd");
                return true;
            case "tab" when s.Kind == "click" && index != null:
                Activate(c, s);
                c.L($"try ControlChooseIndex {index}, {Q(nn)}, hwnd   ; Registerkarte {info}");
                Fallback();
                return true;
            case "listView" when text is { Length: > 0 }:
                Activate(c, s);
                c.UsesListView = true;
                if (s.Kind == "doubleClick")
                {
                    c.L($"if ListViewWaehle({Q(nn)}, hwnd, {Lit(c, text)})   ; Zeile {info}");
                    c.L($"    ControlSend \"{{Enter}}\", {Q(nn)}, hwnd   ; Doppelklick = öffnen");
                    c.L("else {");
                }
                else c.L($"if !ListViewWaehle({Q(nn)}, hwnd, {Lit(c, text)}) {{   ; Zeile {info}");
                c.Indent += "    "; ClickCore(c, s); c.Indent = c.Indent[..^4];
                c.L("}");
                return true;
        }
        return false;
    }

    /// <summary>Explorer: Klick wechselte den Ordner → Ordner direkt öffnen (Shell-COM), Klick als Kommentar.</summary>
    static bool ExplorerNavigate(Ctx c, ViewStep s)
    {
        if (s.Json.Obj("explorer") is not { } ex || ex.Str("folderAfter") is not { } to || to == ex.Str("folder")) return false;
        if (!System.Text.RegularExpressions.Regex.IsMatch(to, @"^([A-Za-z]:\\|\\\\)")) return false;   // virtuelle Ordner (::{GUID}) nicht
        Activate(c, s);
        c.L($"ExplorerNavigiere(hwnd, {Lit(c, to)})   ; Ordner direkt öffnen statt {KindLabel(s.Kind)}");
        if (!c.Snippet) CommentedClick(c, s, "Klick in der Aufnahme");
        c.UsesExplorer = true;
        c.LastExplorer = to;
        return true;
    }

    /// <summary>
    /// Getippter Text in ein erkanntes Win32-Eingabefeld (Schreibmarke gelesen) oder in das Dateinamenfeld eines Dateidialogs:
    /// ControlSetText (Feld war leer bzw. ganz markiert; im Dateidialog mit vollständigem Pfad) oder EditPaste (an der Schreibmarke).
    /// </summary>
    static bool DirectEdit(Ctx c, ViewStep s, JsonObject inp, string? param)
    {
        if (inp.Bool("masked") || inp.Str("text") is not { Length: > 0 } text) return false;
        if (inp.Arr("tokens") is { } toks && toks.Any(t => t is not JsonValue)) return false;   // Tasten, Platzhalter, Maskiertes
        var fc = W32(s)?.Obj("control");
        if (fc == null || fc.Bool("password") || ControlRef(fc) is not { } cref || cref != fc.Str("classNN")) return false;
        var fd = s.Json.Obj("data")?.Obj("fileDialog");
        bool fileName = fd?.Str("fileNameControl") is { } fnc && fnc == cref;
        var caret = fc.Obj("caret");
        if (!fileName && (caret == null || caret.Bool("readOnly"))) return false;
        string value = param ?? Lit(c, text);
        string? vb = inp.Str("valueBefore"), va = inp.Str("valueAfter");
        if (fileName)
        {
            string? folder = fd!.Str("folder");
            bool plainName = text.IndexOfAny(new[] { '\\', '/', ':' }) < 0;
            bool folderOk = folder != null && (fd.Bool("isPath") || System.Text.RegularExpressions.Regex.IsMatch(folder, @"^([A-Za-z]:\\|\\\\)"));
            string expr = plainName && folderOk ? $"{Lit(c, folder!.TrimEnd('\\'))} \"\\\" {value}" : value;
            c.L($"ControlSetText {expr}, {Q(cref)}, hwnd   ; Dateidialog: " + (plainName && folderOk ? "vollständiger Pfad statt Navigation per Klick" : "Dateiname direkt setzen"));
        }
        else
        {
            bool allSelected = caret!.Long("selStart") == 0 && vb != null && caret.Long("selLen") >= vb.Length && vb.Length > 0;
            bool replace = (vb == "" || allSelected) && (va == null || va == text);
            if (replace) c.L($"ControlSetText {value}, {Q(cref)}, hwnd   ; Feld war {(vb == "" ? "leer" : "ganz markiert")} – Inhalt direkt setzen");
            else c.L($"EditPaste {value}, {Q(cref)}, hwnd   ; an der Schreibmarke einfügen" + (caret.LongN("line") is long ln ? $" (Zeile {ln}, Spalte {caret.LongN("col")})" : ""));
        }
        c.L($"; Alternative: SendText {value}");
        if (inp.Obj("terminator") is { } term) c.L($"Send \"{Mods(term.StrList("mods"))}{{{AhkKey(term.Str("key") ?? "Enter")}}}\"");
        if (va != null) c.L($"; Feldinhalt danach in der Aufnahme: „{OneLine(va, 80)}“");
        return true;
    }

    /// <summary>Die aufgezeichnete Klickaktion als Kommentar (wenn ein robusterer Weg sie ersetzt).</summary>
    static void CommentedClick(Ctx c, ViewStep s, string label)
    {
        var tmp = new Ctx { Snippet = true };
        if (s.Kind is "click" or "doubleClick" or "tripleClick" or "rightClick") ClickCore(tmp, s);
        var lines = tmp.Body.ToString().Split(NL, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).Where(l => !l.StartsWith(';')).ToList();
        if (lines.Count > 0) c.L($"; {label}: " + string.Join(" · ", lines));
    }

    /// <summary>Kommentare zum Anwendungskontext vor der Aktion: Explorer-Ordner, Browser-Seite, Office-Dokument (nur bei Wechsel).</summary>
    static void ContextComments(Ctx c, ViewStep s)
    {
        var j = s.Json;
        if (j.Obj("explorer") is { } ex && ex.Str("folder") is { } f && f != c.LastExplorer)
        {
            var sel = ex.StrList("selected");
            c.L($"; Explorer-Ordner: {OneLine(f, 160)}" + (sel.Count > 0 ? $" · ausgewählt: {string.Join(", ", sel.Take(3).Select(x => OneLine(x, 40)))}{(ex.Long("selectedCount") > 3 ? $" (+{ex.Long("selectedCount") - 3})" : "")}" : ""));
            if (!ex.Bool("virtual") && System.Text.RegularExpressions.Regex.IsMatch(f, @"^([A-Za-z]:\\|\\\\)"))
                c.L($"; Alternative: Run 'explorer.exe \"' {Lit(c, f)} '\"'   ; Ordner in einem neuen Fenster öffnen");
            c.LastExplorer = f;
        }
        if (j.Obj("browser") is { } br && br.Str("url") is { } url && url != c.LastUrl)
        {
            c.L($"; Seite in der Aufnahme: {url}" + (br.Bool("schemeAssumed") ? " (https angenommen)" : "") + " – ohne Abfrage und Fragment");
            c.L($"; Alternative: Run {Q(url)}   ; Seite im Standardbrowser öffnen");
            string? title = W32(s)?.Obj("window")?.Str("title") ?? j.Obj("ctx")?.Str("title");
            string? exe = W32(s)?.Obj("window")?.Str("exe") ?? j.Obj("ctx")?.Str("process");
            if (title is { Length: > 0 } && exe != null) c.L($";              WinWait {Q(PageTitle(title) + " ahk_exe " + exe)},, Wartezeit");
            c.LastUrl = url;
        }
        if (j.Obj("office") is { } of)
        {
            string key = string.Join("|", of.Str("workbook"), of.Str("sheet"), of.Str("cell"), of.Str("document"));
            if (key != c.LastOffice)
            {
                c.LastOffice = key;
                if (of.Str("app") == "excel")
                {
                    c.L($"; Excel: Mappe „{OneLine(of.Str("workbook") ?? of.Str("workbookName") ?? "?", 120)}“" + (of.Str("sheet") is { } sh ? $", Blatt „{sh}“" : "") + (of.Str("cell") is { } ce ? $", Zelle {ce}" : "") + (of.Str("selection") is { } se ? $", Auswahl {se}" : ""));
                    c.L("; Alternative (COM): xl := ComObjActive(\"Excel.Application\")");
                    if (of.Str("workbook") is { } wb && System.Text.RegularExpressions.Regex.IsMatch(wb, @"^([A-Za-z]:\\|\\\\)")) c.L($";   xl.Workbooks.Open({Lit(c, wb)})   ; falls die Mappe nicht geöffnet ist");
                    if (of.Str("sheet") is { } sh2) c.L($";   xl.Worksheets({Q(sh2)}).Activate()" + (of.Str("cell") is { } ce2 ? $", xl.Range({Q(of.Str("selection") ?? ce2)}).Select()" : ""));
                }
                else if (of.Str("app") == "word")
                {
                    c.L($"; Word: Dokument „{OneLine(of.Str("document") ?? of.Str("documentName") ?? "?", 120)}“");
                    c.L("; Alternative (COM): wd := ComObjActive(\"Word.Application\")");
                    if (of.Str("document") is { } doc && System.Text.RegularExpressions.Regex.IsMatch(doc, @"^([A-Za-z]:\\|\\\\)")) c.L($";   wd.Documents.Open({Lit(c, doc)})   ; falls das Dokument nicht geöffnet ist");
                }
            }
        }
    }

    /// <summary>Fenstertitel ohne Browsernamen („Seite – Google Chrome“ → „Seite“) für WinWait mit SetTitleMatchMode 2.</summary>
    static string PageTitle(string title)
    {
        foreach (var sep in new[] { " - ", " – ", " — " })
        {
            int i = title.LastIndexOf(sep, StringComparison.Ordinal);
            if (i > 0) return title[..i].Trim();
        }
        return title;
    }

    /// <summary>Benutzername der Aufnahme aus dem ersten Profilpfad (C:\Users\Name\…) in den Schritten.</summary>
    static string? UserNameOf(List<ViewStep> steps)
    {
        static IEnumerable<string> Strings(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o: foreach (var kv in o) foreach (var x in Strings(kv.Value)) yield return x; break;
                case JsonArray a: foreach (var e in a) foreach (var x in Strings(e)) yield return x; break;
                case JsonValue v when v.TryGetValue<string>(out var str): yield return str; break;
            }
        }
        foreach (var s in steps)
            foreach (var str in Strings(s.Json))
                if (str.Length > 9 && str.Contains(":\\", StringComparison.Ordinal) && AhkPaths.UserNameIn(str) is { } u) return u;
        return null;
    }

    /// <summary>
    /// Schritte, deren Aktion ein späterer Schritt übernimmt: Klicks, die das Menü öffnen, wenn danach MenuSelect folgt;
    /// der Klick auf ein Kombinationsfeld, wenn danach ControlChooseString in seiner aufgeklappten Liste folgt.
    /// </summary>
    static Dictionary<string, string> AbsorbedSteps(List<ViewStep> steps)
    {
        var res = new Dictionary<string, string>();
        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            var mp = s.Json.Obj("menuPath");
            bool menuSelect = s.Kind == "click" && mp != null && mp.Bool("win32Menu") && !mp.Bool("submenu") && !mp.Bool("disabled") && OwnerWinTitle(mp.Obj("owner")) != null
                && mp.StrList("win32Path") is { Count: > 0 } wp && wp.All(x => x.Length > 0) && (mp.Str("source") == "menuBar" && wp.Count >= 2 || mp.Str("source") == "systemMenu");
            if (menuSelect)
                for (int k = i - 1; k >= 0; k--)
                {
                    var p = steps[k];
                    if (p.Kind != "click" || p.Json.Obj("menuPath") is not { } pm || pm.LongN("commandId") != null && !pm.Bool("submenu")) break;
                    res[p.Id] = $"Menü öffnen – übernimmt MenuSelect in Schritt {s.Number}";
                }
            var ch = s.Json.Obj("choose")?.Obj("win32");
            if (s.Kind == "click" && ch?.Str("winTitle") != null && ch.Str("kind") == "comboBox" && ch.Str("classNN") is { } nn && !Volatile(nn) && i > 0)
            {
                var p = steps[i - 1];
                if (p.Kind == "click" && W32(p)?.Obj("control") is { } pc && (pc.Str("classNN") == nn || W32(p)?.Obj("list")?.Str("classNN") == nn))
                    res[p.Id] = $"Liste aufklappen – übernimmt ControlChooseString in Schritt {s.Number}";
            }
        }
        return res;
    }

    static readonly string[] ExplorerFunction =
    {
        "",
        "; Öffnet den Ordner im Explorer-Fenster hwnd (Shell-COM); sonst in einem neuen Explorer-Fenster.",
        "ExplorerNavigiere(hwnd, pfad) {",
        "    try {",
        "        for w in ComObject(\"Shell.Application\").Windows {",
        "            if (w.HWND = hwnd) {",
        "                w.Navigate(pfad)",
        "                return true",
        "            }",
        "        }",
        "    }",
        "    Run 'explorer.exe \"' pfad '\"'",
        "    return false",
        "}"
    };

    static readonly string[] ListViewFunction =
    {
        "",
        "; Wählt in einer ListView die Zeile, deren erste Spalte text ist (per Tastatur); false, wenn nicht gefunden.",
        "ListViewWaehle(ctl, hwnd, text) {",
        "    try",
        "        zeilen := StrSplit(ListViewGetContent(\"Col1\", ctl, hwnd), \"`n\")",
        "    catch",
        "        return false",
        "    for i, z in zeilen {",
        "        if (z = text) {",
        "            ControlFocus ctl, hwnd",
        "            ControlSend \"{Home}\", ctl, hwnd",
        "            if (i > 1)",
        "                ControlSend \"{Down \" (i - 1) \"}\", ctl, hwnd",
        "            return true",
        "        }",
        "    }",
        "    return false",
        "}"
    };

    static readonly string[] MsaaFunction =
    {
        "",
        "; Führt die MSAA-Standardaktion (DoDefaultAction) des Elements an den Fensterkoordinaten (x, y) aus.",
        "MsaaStandardAktion(x, y, hwnd) {",
        "    WinGetPos &wx, &wy,,, hwnd",
        "    varKind := Buffer(8 + 2 * A_PtrSize, 0)",
        "    if DllCall(\"oleacc\\AccessibleObjectFromPoint\", \"int64\", ((wy + y) << 32) | ((wx + x) & 0xFFFFFFFF), \"ptr*\", &pacc := 0, \"ptr\", varKind) != 0",
        "        return false",
        "    acc := ComValue(9, pacc)",
        "    acc.accDoDefaultAction(NumGet(varKind, 8, \"int\"))",
        "    return true",
        "}"
    };

    // ------------------------------------------------------------------------------------------

    static string? Uia(JsonObject? target)
    {
        if (target?.Obj("el") is not { } el) return null;
        var parts = new List<string>();
        parts.Add(el.Str("ct") ?? "?");
        if (el.Str("name") is { Length: > 0 } n) parts.Add($"„{OneLine(n, 60)}“");
        if (el.Str("aid") is { Length: > 0 } a) parts.Add("AutomationId " + a);
        if (el.Str("cls") is { Length: > 0 } cl) parts.Add("Klasse " + cl);
        if (target.Arr("path") is { Count: > 0 } p) parts.Add("Pfad " + string.Join(" › ", p.TakeLast(4).Select(x => x.Str("ct") + (x.Str("name") is { Length: > 0 } pn ? " „" + OneLine(pn, 30) + "“" : ""))));
        return string.Join(" · ", parts);
    }

    static string Mods(IEnumerable<string> mods)
    {
        var sb = new StringBuilder();
        foreach (var m in mods.Select(m => m.ToLowerInvariant()))
            sb.Append(m switch { "ctrl" or "strg" or "leftctrl" or "rightctrl" => "^", "alt" or "leftalt" or "rightalt" => "!", "altgr" => "<^>!", "shift" or "umschalt" or "leftshift" or "rightshift" => "+", "win" or "leftwin" or "rightwin" => "#", _ => "" });
        return sb.ToString();
    }

    /// <summary>Kanonischer Tastenname → AutoHotkey-Tastenname.</summary>
    public static string AhkKey(string k) => k switch
    {
        "Escape" => "Esc", "Insert" => "Ins", "Delete" => "Del", "PageUp" => "PgUp", "PageDown" => "PgDn", "Apps" => "AppsKey",
        "LeftCtrl" => "LCtrl", "RightCtrl" => "RCtrl", "LeftAlt" => "LAlt", "RightAlt" => "RAlt", "LeftShift" => "LShift", "RightShift" => "RShift", "LeftWin" => "LWin", "RightWin" => "RWin",
        "Oem1" => "vkBA", "Oem2" => "vkBF", "Oem3" => "vkC0", "Oem4" => "vkDB", "Oem5" => "vkDC", "Oem6" => "vkDD", "Oem7" => "vkDE", "Oem8" => "vkDF", "Oem102" => "vkE2",
        "OemPlus" => "vkBB", "OemComma" => "vkBC", "OemMinus" => "vkBD", "OemPeriod" => "vkBE",
        "VolumeUp" => "Volume_Up", "VolumeDown" => "Volume_Down", "VolumeMute" => "Volume_Mute", "MediaPlayPause" => "Media_Play_Pause", "MediaNext" => "Media_Next", "MediaPrev" => "Media_Prev", "MediaStop" => "Media_Stop",
        _ when k.StartsWith("NumPad", StringComparison.Ordinal) => "Numpad" + k[6..],
        _ when k.Length == 1 => k.ToLowerInvariant(),
        _ => k
    };

    static string KindLabel(string kind) => kind switch
    {
        "click" => "Klick", "doubleClick" => "Doppelklick", "tripleClick" => "Dreifachklick", "rightClick" => "Rechtsklick", "drag" => "Ziehen", "scroll" => "Scrollen",
        "type" => "Eingabe", "hotkey" => "Tastenkürzel", "key" => "Taste", "hover" => "Zeigen", "windowSwitch" => "Fensterwechsel", "wait" => "Warten", _ => kind
    };

    /// <summary>AHK-v2-Zeichenkette in Anführungszeichen.</summary>
    public static string Q(string s) => "\"" + Esc(s) + "\"";
    static string Esc(string s) => s.Replace("`", "``").Replace("\"", "`\"").Replace("\r", "").Replace("\n", "`n").Replace("\t", "`t");
    static string OneLine(string s, int max = 100) { s = s.Replace("\r", " ").Replace("\n", " ").Trim(); return s.Length <= max ? s : s[..max] + "…"; }
    static string Ident(string s) { var r = new string(s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()); return r.Length == 0 ? "Wert" : r; }
}
