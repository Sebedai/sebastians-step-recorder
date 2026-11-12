using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Native;

namespace StepRecorder.Capture;

/// <summary>
/// Win32-Kontext einer Mausposition bzw. des Tastaturfokus – so, wie AutoHotkey Fenster und Steuerelemente
/// anspricht: WinTitle (Titel, ahk_class, ahk_exe, ahk_pid), ClassNN, Steuerelement-ID, Koordinaten relativ zu
/// Bildschirm, Fenster und Clientbereich, Pixelfarbe, Fenstertexte (WinText).
/// Alle Koordinaten sind physische Pixel im virtuellen Bildschirm.
/// Datenschutz: Inhalte editierbarer Steuerelemente werden hier nie gelesen (die liefert UIA mit Maskierung und
/// Durchsicht); Passwortfelder werden erkannt und ausgelassen; im Privatmodus gibt es gar keine Texte.
/// </summary>
public static partial class Win32Detail
{
    public enum TextLevel { None, Control, All }

    const int MaxText = 300, MaxTexts = 60, TextTimeoutMs = 60, TextBudgetMs = 200;

    /// <summary>Kontext der Position (vx, vy): Hauptfenster, Steuerelement unter dem Mauszeiger, Fokus, Pixelfarbe.</summary>
    public static JsonObject? AtPoint(int vx, int vy, TextLevel texts)
    {
        var pt = new Win32.POINT { X = vx, Y = vy };
        nint hit = Win32.WindowFromPoint(pt);
        if (hit == 0) return null;
        nint top = Win32.GetAncestor(hit, 2 /*GA_ROOT*/);
        if (top == 0) top = hit;
        // in die tiefste sichtbare Kindebene absteigen (wie MouseGetPos)
        nint ctl = hit;
        for (int i = 0; i < 16; i++)
        {
            var cp = pt; ScreenToClient(ctl, ref cp);
            nint child = ChildWindowFromPointEx(ctl, cp, 0x0001 /*SKIPINVISIBLE*/ | 0x0004 /*SKIPTRANSPARENT*/);
            if (child == 0 || child == ctl) break;
            ctl = child;
        }
        var o = new JsonObject { ["src"] = "win32" };
        var win = WindowInfo(top, out var wr, out var client);
        o["window"] = win;
        if (Excluded(win)) texts = TextLevel.None;
        var point = new JsonObject { ["screen"] = Xy(vx, vy), ["window"] = Xy(vx - wr.Left, vy - wr.Top) };
        if (client is { } c) point["client"] = Xy(vx - c.X, vy - c.Y);
        o["point"] = point;
        JsonObject? control = null;
        if (ctl != top)
        {
            control = ControlInfo(top, ctl, texts, client);
            if (control != null)
            {
                if (GetWindowRectRaw(ctl, out var cr)) control["point"] = Xy(vx - cr.Left, vy - cr.Top);
                o["control"] = control;
                if (ListInfo(ctl, control.Str("class") ?? "", texts, pt) is { } li) { li["classNN"] = control.Str("classNN"); o["list"] = li.Clean(); }
            }
        }
        else
        {
            // Popup-Menü (#32768) bzw. aufgeklappte Liste eines Kombinationsfelds (ComboLBox) – eigene Hauptfenster
            string topCls = win.Str("class") ?? "";
            if (topCls == "#32768" && texts != TextLevel.None && MenuAt(top, pt, texts) is { } menu) o["menu"] = menu;
            else if (topCls == "ComboLBox" && ListInfo(top, topCls, texts, pt) is { } li)
            {
                if (ComboListOwner(top) is { } owner) li["owner"] = owner;
                o["list"] = li;
            }
        }
        if (FileDialog(top, texts) is { } fd) o["fileDialog"] = fd;
        if (FocusOf(top) is { } f && f != ctl && f != top && ControlInfo(top, f, texts == TextLevel.None ? TextLevel.None : TextLevel.Control, client) is { } fi) o["focus"] = fi;
        if (Pixel(vx, vy) is { } px) o["pixel"] = px;
        if (CursorShape() is { } cur) o["cursor"] = cur;
        if (texts == TextLevel.All && VisibleTexts(top) is { Count: > 0 } list) o["texts"] = list;
        o["ahk"] = Ahk(win, control);
        return o;
    }

    /// <summary>Kontext des Tastaturfokus im Vordergrundfenster (für Eingaben und Tastenkürzel).</summary>
    public static JsonObject? Focused(TextLevel texts)
    {
        nint fg = Win32.GetForegroundWindow();
        if (fg == 0) return null;
        nint top = Win32.GetAncestor(fg, 2);
        if (top == 0) top = fg;
        var o = new JsonObject { ["src"] = "win32" };
        var win = WindowInfo(top, out _, out var client);
        o["window"] = win;
        if (Excluded(win)) texts = TextLevel.None;
        JsonObject? control = null;
        if (FocusOf(top) is { } f && f != top)
        {
            control = ControlInfo(top, f, texts == TextLevel.None ? TextLevel.None : TextLevel.Control, client);
            if (control != null)
            {
                o["control"] = control;
                // bei jeder Fokusprobe (auch je Tastendruck): nur Anzahl und Auswahl, keine Eintragsliste
                if (ListInfo(f, control.Str("class") ?? "", texts == TextLevel.All ? TextLevel.Control : texts, null) is { } li) { li["classNN"] = control.Str("classNN"); o["list"] = li.Clean(); }
            }
        }
        if (FileDialog(top, texts) is { } fd) o["fileDialog"] = fd;
        o["ahk"] = Ahk(win, control);
        return o;
    }

    /// <summary>Zustand eines Fensters nach einer Aktion: Titel, Fokus, sichtbare Texte mit Häkchen (Grundlage für Erfolgsprüfungen).</summary>
    public static JsonObject? State(nint top, TextLevel texts)
    {
        if (top == 0 || !Win32.IsWindow(top)) return null;
        var o = new JsonObject { ["hwnd"] = Json.Hex(top), ["title"] = Trunc(Win32.GetWindowTitle(top), MaxText), ["class"] = Win32.GetClassNameOf(top) };
        Win32.GetWindowThreadProcessId(top, out uint pid);
        if (Win32.GetProcessPath(pid) is { } path) o["exe"] = Path.GetFileName(path);
        if (Excluded(o)) texts = TextLevel.None;
        if (FocusOf(top) is { } f && f != top && ClassNN(top, f) is { } nn)
        {
            o["focus"] = nn;
            // Auswahl einer Liste mit Fokus nach der Aktion (z. B. per Tastatur gewählt)
            if (ListInfo(f, Win32.GetClassNameOf(f), texts, null) is { } li) { li["classNN"] = nn; li.Remove("items"); li.Remove("itemsTruncated"); o["focusList"] = li; }
        }
        if (texts != TextLevel.None && VisibleTexts(top) is { Count: > 0 } list) o["texts"] = list;
        if (StatusBar(top, texts) is { } sb) o["statusBar"] = sb;
        if (FileDialog(top, texts) is { } fd) o["fileDialog"] = fd;
        if (CursorShape() is { } cur) o["cursor"] = cur;
        return o;
    }

    static readonly Dictionary<nint, string> CursorNames = new();

    /// <summary>Aktuelle Form des Mauszeigers (arrow, ibeam, wait, appStarting, hand, …); wait/appStarting = Anwendung beschäftigt.</summary>
    public static string? CursorShape()
    {
        lock (CursorNames)
        {
            if (CursorNames.Count == 0)
                foreach (var (id, name) in new[] { (32512, "arrow"), (32513, "ibeam"), (32514, "wait"), (32515, "cross"), (32516, "upArrow"), (32642, "sizeNWSE"), (32643, "sizeNESW"), (32644, "sizeWE"), (32645, "sizeNS"), (32646, "sizeAll"), (32648, "no"), (32649, "hand"), (32650, "appStarting"), (32651, "help") })
                { nint h = LoadCursor(0, id); if (h != 0) CursorNames[h] = name; }
        }
        var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref ci)) return null;
        if ((ci.flags & 1) == 0) return "hidden";
        lock (CursorNames) return CursorNames.TryGetValue(ci.hCursor, out var n) ? n : "custom";
    }

    static readonly Dictionary<uint, JsonObject?> AppCache = new();

    /// <summary>
    /// Befehlszeile des Programms speichern (nur mit „Erweitertem Programmkontext“, 10 §3). Vom Recorder je Aufnahme gesetzt;
    /// ohne sie bleibt nur das Kennzeichen <c>privateArgs</c> für die Erkennung privater Browserfenster.
    /// </summary>
    public static bool KeepCommandLine;

    /// <summary>Pfade unter dem Benutzerprofil mit dem Platzhalter <c>%USERPROFILE%</c> (10 §3) – der Benutzername gelangt so nicht ins Protokoll.</summary>
    public static string ProfilePath(string path) => ProfilePath(path, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Wie <see cref="ProfilePath(string)"/> mit vorgegebenem Profilordner (für Tests); wirkt auch mitten in Befehlszeilen.</summary>
    public static string ProfilePath(string path, string? home)
    {
        try
        {
            home = home?.TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(path) || home is not { Length: > 3 }) return path;
            return System.Text.RegularExpressions.Regex.Replace(path, System.Text.RegularExpressions.Regex.Escape(home) + @"(?=[\\/""\s]|$)", "%USERPROFILE%",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        }
        catch { return path; }
    }

    static JsonObject? AppView(JsonObject? cached)
    {
        if (cached?.DeepClone() is not JsonObject o) return null;
        if (!KeepCommandLine) o.Remove("commandLine");
        return o;
    }

    /// <summary>Programm des Fensters: Produkt, Version, Hersteller, Startbefehl (ohne verdächtige Argumente), Administratorrechte.</summary>
    static JsonObject? AppInfo(uint pid)
    {
        lock (AppCache) if (AppCache.TryGetValue(pid, out var cached)) return AppView(cached);
        JsonObject? o = null;
        try
        {
            var path = Win32.GetProcessPath(pid);
            if (path != null)
            {
                o = new JsonObject();
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(path);
                    if (!string.IsNullOrWhiteSpace(vi.ProductName)) o["product"] = vi.ProductName.Trim();
                    if (!string.IsNullOrWhiteSpace(vi.FileDescription)) o["description"] = vi.FileDescription.Trim();
                    if (!string.IsNullOrWhiteSpace(vi.CompanyName)) o["company"] = vi.CompanyName.Trim();
                    if (!string.IsNullOrWhiteSpace(vi.ProductVersion)) o["version"] = vi.ProductVersion.Trim();
                    else if (!string.IsNullOrWhiteSpace(vi.FileVersion)) o["version"] = vi.FileVersion.Trim();
                }
                catch { }
                if (CommandLine(pid) is { } cmd)
                {
                    o["commandLine"] = ProfilePath(SafeCommandLine(cmd, path));
                    if (AppProbe.PrivateWindow(null, cmd)) o["privateArgs"] = true;
                }
                o["elevated"] = Elevated(pid) is bool e ? e : null;
                if (o["elevated"] is null) o["elevatedUnknown"] = true;
                o.Clean();
            }
        }
        catch { }
        lock (AppCache) AppCache[pid] = o;
        return AppView(o);
    }

    /// <summary>Argumente mit Passwörtern, Schlüsseln, Tokens oder E-Mail-Adressen werden ausgelassen.</summary>
    static string SafeCommandLine(string cmd, string path)
    {
        cmd = cmd.Trim();
        if (cmd.Length > 600) cmd = cmd[..600] + "…";
        bool suspicious = ReadLayer.Patterns.Flag(cmd) != null || ReadLayer.Patterns.Hint(cmd) != null
            || System.Text.RegularExpressions.Regex.IsMatch(cmd, @"(?i)(pass(wort|word)?|pwd|token|secret|apikey|api-key|key=|auth|bearer)");
        return suspicious ? $"\"{path}\" [Argumente ausgelassen: möglicherweise vertraulich]" : cmd;
    }

    static string? CommandLine(uint pid)
    {
        nint h = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == 0) return null;
        try
        {
            int size = 4096;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                nint buf = Marshal.AllocHGlobal(size);
                try
                {
                    int st = NtQueryInformationProcess(h, 60 /*ProcessCommandLineInformation*/, buf, size, out int ret);
                    if (st == unchecked((int)0xC0000004) /*INFO_LENGTH_MISMATCH*/) { size = Math.Max(ret, size * 2); continue; }
                    if (st != 0) return null;
                    int len = (ushort)Marshal.ReadInt16(buf);
                    nint str = Marshal.ReadIntPtr(buf, IntPtr.Size);
                    return len > 0 && str != 0 ? Marshal.PtrToStringUni(str, len / 2) : null;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            return null;
        }
        catch { return null; }
        finally { Win32.CloseHandle(h); }
    }

    static bool? Elevated(uint pid)
    {
        nint h = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == 0) return null;
        try
        {
            if (!Win32.OpenProcessToken(h, 0x0008 /*TOKEN_QUERY*/, out var tok)) return null;
            try { return Win32.GetTokenInformation(tok, 20 /*TokenElevation*/, out int e, 4, out _) ? e != 0 : null; }
            finally { Win32.CloseHandle(tok); }
        }
        finally { Win32.CloseHandle(h); }
    }

    /// <summary>Häkchen von Kontroll- und Optionsfeldern (BM_GETCHECK): checked, unchecked, mixed.</summary>
    static string? CheckState(nint h, string cls)
    {
        if (!cls.Contains("BUTTON", StringComparison.OrdinalIgnoreCase)) return null;
        long type = Win32.GetWindowLongPtr(h, -16) & 0xF;
        if (type is not (2 or 3 or 4 or 5 or 6 or 9)) return null;   // CHECKBOX, AUTOCHECKBOX, RADIOBUTTON, 3STATE, AUTO3STATE, AUTORADIOBUTTON
        if (SendMessageTimeout(h, 0x00F0 /*BM_GETCHECK*/, 0, 0, 0x0002, TextTimeoutMs, out var r) == 0) return null;
        return (long)r switch { 1 => "checked", 2 => "mixed", _ => "unchecked" };
    }

    // ------------------------------------------------------------------------------------------

    static JsonObject WindowInfo(nint top, out RECT wr, out (int X, int Y, int W, int H)? client)
    {
        var o = new JsonObject { ["hwnd"] = Json.Hex(top) };
        var title = Win32.GetWindowTitle(top);
        if (title.Length > 0) o["title"] = Trunc(title, MaxText);
        o["class"] = Win32.GetClassNameOf(top);
        Win32.GetWindowThreadProcessId(top, out uint pid);
        o["pid"] = pid;
        if (Win32.GetProcessPath(pid) is { } path) { o["exe"] = Path.GetFileName(path); o["path"] = ProfilePath(path); }
        GetWindowRectRaw(top, out wr);
        o["rect"] = Rect(wr.Left, wr.Top, wr.Right - wr.Left, wr.Bottom - wr.Top);
        client = null;
        if (GetClientRect(top, out var cr))
        {
            var p = new Win32.POINT(); ClientToScreen(top, ref p);
            client = (p.X, p.Y, cr.Right - cr.Left, cr.Bottom - cr.Top);
            o["client"] = Rect(p.X, p.Y, cr.Right - cr.Left, cr.Bottom - cr.Top);
        }
        o["state"] = Win32.IsIconic(top) ? "minimized" : IsZoomed(top) ? "maximized" : "normal";
        long ex = Win32.GetWindowLongPtr(top, -20);
        if ((ex & 0x8) != 0) o["topmost"] = true;
        try { uint dpi = GetDpiForWindow(top); if (dpi > 0) o["dpi"] = dpi; } catch { }
        if (AppInfo(pid) is { } app) o["app"] = app;
        uint tid = Win32.GetWindowThreadProcessId(top, out _);
        o["kbLayout"] = ((long)Win32.GetKeyboardLayout(tid) & 0xFFFF).ToString("X4");
        nint owner = GetWindow(top, 4 /*GW_OWNER*/);
        if (owner != 0) o["owner"] = new JsonObject { ["hwnd"] = Json.Hex(owner), ["title"] = Trunc(Win32.GetWindowTitle(owner), 200), ["class"] = Win32.GetClassNameOf(owner) };
        return o;
    }

    static JsonObject? ControlInfo(nint top, nint ctl, TextLevel texts, (int X, int Y, int W, int H)? client)
    {
        if (ctl == 0 || !Win32.IsWindow(ctl)) return null;
        string cls = Win32.GetClassNameOf(ctl);
        var o = new JsonObject { ["hwnd"] = Json.Hex(ctl), ["class"] = cls };
        if (ClassNN(top, ctl) is { } nn) o["classNN"] = nn;
        int id = GetDlgCtrlID(ctl);
        if (id != 0) o["id"] = id;
        if (GetWindowRectRaw(ctl, out var r))
        {
            o["screenRect"] = Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            if (client is { } c) o["rect"] = Rect(r.Left - c.X, r.Top - c.Y, r.Right - r.Left, r.Bottom - r.Top); // wie ControlGetPos (Client)
        }
        long style = Win32.GetWindowLongPtr(ctl, -16), ex = Win32.GetWindowLongPtr(ctl, -20);
        o["style"] = "0x" + style.ToString("X8"); o["exStyle"] = "0x" + ex.ToString("X8");
        if (!IsWindowEnabled(ctl)) o["enabled"] = false;
        if (!Win32.IsWindowVisible(ctl)) o["visible"] = false;
        bool password = IsEditClass(cls) && (style & 0x20) != 0;
        if (password) { o["password"] = true; o["textWithheld"] = "password"; }
        else if (IsEditClass(cls) || IsContentSurface(cls)) o["textWithheld"] = "editable";
        else if (texts == TextLevel.None) o["textWithheld"] = "privacy";
        else if (ReadText(ctl) is { Length: > 0 } t) o["text"] = t;
        if (CheckState(ctl, cls) is { } chk) o["checked"] = chk;
        if (!password && texts != TextLevel.None && Caret(ctl, cls, style) is { } caret) o["caret"] = caret;
        nint parent = GetParent(ctl);
        if (parent != 0 && parent != top) o["parentClass"] = Win32.GetClassNameOf(parent);
        return o;
    }

    /// <summary>ClassNN wie AutoHotkey: Klassenname + laufende Nummer unter allen Nachfahren des Hauptfensters.</summary>
    static string? ClassNN(nint top, nint ctl)
    {
        string cls = Win32.GetClassNameOf(ctl);
        int n = 0; string? res = null;
        EnumChildWindows(top, (h, _) =>
        {
            if (Win32.GetClassNameOf(h) == cls) { n++; if (h == ctl) { res = cls + n; return false; } }
            return true;
        }, 0);
        return res;
    }

    /// <summary>Sichtbare Texte der nicht editierbaren Steuerelemente (entspricht WinGetText ohne Feldinhalte).</summary>
    static JsonArray VisibleTexts(nint top)
    {
        (int X, int Y, int W, int H)? client = null;
        if (GetClientRect(top, out var crc)) { var p0 = new Win32.POINT(); ClientToScreen(top, ref p0); client = (p0.X, p0.Y, crc.Right - crc.Left, crc.Bottom - crc.Top); }
        var list = new JsonArray();
        var counts = new Dictionary<string, int>();
        var sw = Stopwatch.StartNew();
        EnumChildWindows(top, (h, _) =>
        {
            string cls = Win32.GetClassNameOf(h);
            counts[cls] = counts.GetValueOrDefault(cls) + 1;
            if (sw.ElapsedMilliseconds > TextBudgetMs || list.Count >= MaxTexts) return false;
            if (!Win32.IsWindowVisible(h) || IsEditClass(cls) || IsContentSurface(cls)) return true;
            if (ReadText(h) is { Length: > 0 } t)
            {
                var item = new JsonObject { ["c"] = cls + counts[cls], ["t"] = Trunc(t, 120) };
                if (client is { } cl && GetWindowRectRaw(h, out var r)) item["r"] = new JsonArray(r.Left - cl.X, r.Top - cl.Y, r.Right - r.Left, r.Bottom - r.Top);
                if (CheckState(h, cls) is { } chk) item["checked"] = chk;
                if (!IsWindowEnabled(h)) item["enabled"] = false;
                list.Add(item);
            }
            return true;
        }, 0);
        return list;
    }

    static nint? FocusOf(nint top)
    {
        uint tid = Win32.GetWindowThreadProcessId(top, out _);
        var gi = new Win32.GUITHREADINFO { cbSize = Marshal.SizeOf<Win32.GUITHREADINFO>() };
        if (!Win32.GetGUIThreadInfo(tid, ref gi) || gi.hwndFocus == 0) return null;
        return gi.hwndFocus;
    }

    static string? ReadText(nint h)
    {
        if (SendMessageTimeout(h, 0x000E /*WM_GETTEXTLENGTH*/, 0, 0, 0x0002 /*ABORTIFHUNG*/, TextTimeoutMs, out var len) == 0) return null;
        int n = (int)Math.Min((long)len, MaxText);
        if (n <= 0) return null;
        var sb = new StringBuilder(n + 1);
        if (SendMessageTimeoutText(h, 0x000D /*WM_GETTEXT*/, n + 1, sb, 0x0002, TextTimeoutMs, out _) == 0) return null;
        var s = sb.ToString().Replace("\r\n", "\n").Trim();
        return s.Length == 0 ? null : s;
    }

    /// <summary>Farbe am Punkt als 0xRRGGBB (wie PixelGetColor).</summary>
    static string? Pixel(int vx, int vy)
    {
        nint dc = Win32.GetDC(0);
        if (dc == 0) return null;
        try
        {
            uint c = GetPixel(dc, vx, vy);
            if (c == 0xFFFFFFFF) return null;
            return $"0x{c & 0xFF:X2}{(c >> 8) & 0xFF:X2}{(c >> 16) & 0xFF:X2}";
        }
        finally { Win32.ReleaseDC(0, dc); }
    }

    static JsonObject Ahk(JsonObject win, JsonObject? control)
    {
        string cls = win.Str("class") ?? "", exe = win.Str("exe") ?? "";
        string winTitle = (cls.Length > 0 ? "ahk_class " + cls : "") + (exe.Length > 0 ? (cls.Length > 0 ? " " : "") + "ahk_exe " + exe : "");
        var o = new JsonObject { ["winTitle"] = winTitle };
        if (win.Str("title") is { Length: > 0 } t) o["winTitleFull"] = t + " " + winTitle;
        if (control?.Str("classNN") is { } nn) o["control"] = nn;
        return o;
    }

    /// <summary>Editierbare Steuerelemente: Inhalt nie über WM_GETTEXT lesen.</summary>
    public static bool IsEditClass(string cls)
        => cls.Contains("Edit", StringComparison.OrdinalIgnoreCase) || cls.Contains("Scintilla", StringComparison.OrdinalIgnoreCase)
        || cls.Contains("TextBox", StringComparison.OrdinalIgnoreCase) || cls.Contains("Memo", StringComparison.OrdinalIgnoreCase);

    /// <summary>Darstellungsflächen ohne eigene Steuerelemente (Browser, Terminals, Dokumente).</summary>
    public static bool IsContentSurface(string cls)
        => cls.StartsWith("Chrome_", StringComparison.Ordinal) || cls is "Internet Explorer_Server" or "MozillaWindowClass" or "ConsoleWindowClass"
        or "CASCADIA_HOSTING_WINDOW_CLASS" or "Windows.UI.Core.CoreWindow" or "DirectUIHWND" or "_WwG" or "EXCEL7" or "PuTTY" or "mintty";

    static JsonArray Xy(int x, int y) => new(x, y);
    static JsonObject Rect(int x, int y, int w, int h) => new() { ["x"] = x, ["y"] = y, ["w"] = w, ["h"] = h };
    static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];

    static bool GetWindowRectRaw(nint h, out RECT r) => GetWindowRect(h, out r);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    delegate bool EnumProc(nint h, nint l);
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint h, out RECT r);
    [DllImport("user32.dll")] static extern bool GetClientRect(nint h, out RECT r);
    [DllImport("user32.dll")] static extern bool ClientToScreen(nint h, ref Win32.POINT p);
    [DllImport("user32.dll")] static extern bool ScreenToClient(nint h, ref Win32.POINT p);
    [DllImport("user32.dll")] static extern nint ChildWindowFromPointEx(nint parent, Win32.POINT p, uint flags);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(nint parent, EnumProc proc, nint l);
    [DllImport("user32.dll")] static extern int GetDlgCtrlID(nint h);
    [DllImport("user32.dll")] static extern bool IsWindowEnabled(nint h);
    [DllImport("user32.dll")] static extern bool IsZoomed(nint h);
    [DllImport("user32.dll")] static extern nint GetParent(nint h);
    [DllImport("user32.dll")] static extern nint GetWindow(nint h, uint cmd);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(nint h);
    [DllImport("gdi32.dll")] static extern uint GetPixel(nint dc, int x, int y);
    [StructLayout(LayoutKind.Sequential)] struct CURSORINFO { public int cbSize; public int flags; public nint hCursor; public Win32.POINT pt; }
    [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CURSORINFO ci);
    [DllImport("user32.dll")] static extern nint LoadCursor(nint inst, int id);
    [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(nint h, int cls, nint buf, int len, out int ret);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern nint SendMessageTimeout(nint h, uint msg, nint w, nint l, uint flags, uint timeout, out nint result);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageTimeoutW")] static extern nint SendMessageTimeoutText(nint h, uint msg, nint w, StringBuilder l, uint flags, uint timeout, out nint result);
}
