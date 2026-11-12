using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Native;

namespace StepRecorder.Capture;

/// <summary>
/// Zusatzangaben für AutoHotkey: Befehls-IDs klassischer Menüs (MenuSelect, WM_COMMAND), Inhalte und Auswahl von
/// Listen/Auswahlfeldern/ListView/TreeView/Registerkarten (ControlChooseString/-Index), Statusleiste (StatusBarGetText),
/// Schreibmarke in Eingabefeldern (EM_GETSEL – nie der Inhalt), Ordner und Dateinamenfeld von Dateidialogen.
/// Alles mit kurzen Zeitlimits (SendMessageTimeout, ABORTIFHUNG) und Größenbeschränkung; im Privatmodus nichts.
/// Texte von ListView, TreeView, Registerkarten und Statusleiste kommen über UI Automation – nie über Speicher des
/// Zielprozesses (10 §2, P9); Fensternachrichten liefern nur Zahlen (Anzahl, Auswahl).
/// </summary>
public static partial class Win32Detail
{
    const int MaxItems = 50, MaxItemText = 200, ListBudgetMs = 150, MaxColumns = 10, MaxSelRows = 5, MaxParts = 8;

    /// <summary>Vom Recorder gesetzt: ausgeschlossene Programme (exe) – dort keine Texte, Listen, Menüs, Schreibmarken.</summary>
    public static Func<string, bool>? ExcludedExe;

    static bool Excluded(JsonObject win) => win.Str("exe") is { } e && ExcludedExe?.Invoke(e) == true;

    // ------------------------------------------------------------------------------------------
    // Klassische Menüs
    // ------------------------------------------------------------------------------------------

    /// <summary>Menüeintrag unter dem Punkt in einem Popup-Menü (#32768): Befehls-ID, Text, Position, Pfad ab Menüleiste.</summary>
    internal static JsonObject? MenuAt(nint popup, Win32.POINT pt, TextLevel texts)
    {
        try
        {
            if (Win32.GetClassNameOf(popup) != "#32768") return null;
            if (SendMessageTimeout(popup, 0x01E1 /*MN_GETHMENU*/, 0, 0, 0x0002, TextTimeoutMs, out var hm) == 0 || hm == 0) return null;
            int pos = MenuItemFromPoint(0, hm, pt);
            if (pos < 0) return null;
            var o = new JsonObject { ["pos"] = pos };
            uint state = GetMenuState(hm, (uint)pos, 0x0400 /*MF_BYPOSITION*/);
            if (state != 0xFFFFFFFF)
            {
                if ((state & 0x0800) != 0) o["separator"] = true;
                if ((state & 0x0003) != 0) o["enabled"] = false;
                if ((state & 0x0008) != 0) o["checked"] = true;
            }
            nint sub = GetSubMenu(hm, pos);
            if (sub != 0) o["submenu"] = true;
            else
            {
                uint id = GetMenuItemID(hm, pos);
                if (id != 0xFFFFFFFF && id != 0) o["cmdId"] = (long)id;
            }
            if (texts != TextLevel.None && MenuText(hm, pos) is { } t) o["text"] = t;
            // Besitzer des Menüs (das Fenster, das WM_COMMAND erhält)
            uint tid = Win32.GetWindowThreadProcessId(popup, out _);
            var gi = new Win32.GUITHREADINFO { cbSize = Marshal.SizeOf<Win32.GUITHREADINFO>() };
            nint owner = Win32.GetGUIThreadInfo(tid, ref gi) ? gi.hwndMenuOwner : 0;
            if (owner != 0) owner = Win32.GetAncestor(owner, 2) is var r && r != 0 ? r : owner;
            if (owner != 0 && Win32.IsWindow(owner))
            {
                o["owner"] = OwnerInfo(owner);
                // Pfad ab Menüleiste bzw. Systemmenü
                var path = new List<(int pos, string? text)>();
                string source = "popup";
                nint bar = GetMenu(owner);
                if (bar != 0 && FindMenuPath(bar, hm, path, 0, texts)) source = "menuBar";
                else
                {
                    path.Clear();
                    nint sys = GetSystemMenu(owner, false);
                    if (sys != 0 && (sys == hm || FindMenuPath(sys, hm, path, 0, texts))) source = "systemMenu";
                    else path.Clear();
                }
                o["source"] = source;
                if (source != "popup")
                {
                    path.Add((pos, texts != TextLevel.None ? MenuText(hm, pos) : null));
                    o["path"] = new JsonArray(path.Select(p => (JsonNode)new JsonObject { ["pos"] = p.pos, ["text"] = p.text }.Clean()).ToArray());
                }
            }
            return o;
        }
        catch { return null; }
    }

    static bool FindMenuPath(nint menu, nint target, List<(int pos, string? text)> path, int depth, TextLevel texts)
    {
        if (depth > 5) return false;
        int n = GetMenuItemCount(menu);
        if (n <= 0 || n > 300) return false;
        for (int i = 0; i < n; i++)
        {
            nint sub = GetSubMenu(menu, i);
            if (sub == 0) continue;
            path.Add((i, texts != TextLevel.None ? MenuText(menu, i) : null));
            if (sub == target || FindMenuPath(sub, target, path, depth + 1, texts)) return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    static string? MenuText(nint menu, int pos)
    {
        var sb = new StringBuilder(MaxItemText + 1);
        int n = GetMenuString(menu, (uint)pos, sb, sb.Capacity, 0x0400);
        if (n <= 0) return null;
        var s = sb.ToString();
        return s.Length == 0 ? null : s;
    }

    /// <summary>Kurzinfo über ein Hauptfenster mit AutoHotkey-WinTitle.</summary>
    static JsonObject OwnerInfo(nint top)
    {
        var w = new JsonObject { ["hwnd"] = Json.Hex(top), ["class"] = Win32.GetClassNameOf(top) };
        var title = Win32.GetWindowTitle(top);
        if (title.Length > 0) w["title"] = Trunc(title, MaxText);
        Win32.GetWindowThreadProcessId(top, out uint pid);
        if (Win32.GetProcessPath(pid) is { } path) w["exe"] = Path.GetFileName(path);
        w["winTitle"] = Ahk(w, null).Str("winTitle");
        return w;
    }

    // ------------------------------------------------------------------------------------------
    // Listen, Auswahlfelder, ListView, TreeView, Registerkarten
    // ------------------------------------------------------------------------------------------

    internal static string? ListKind(string cls)
    {
        if (cls.Equals("ComboLBox", StringComparison.OrdinalIgnoreCase)) return "comboList";
        if (cls.Contains("SysListView32", StringComparison.OrdinalIgnoreCase)) return "listView";
        if (cls.Contains("SysTreeView32", StringComparison.OrdinalIgnoreCase)) return "treeView";
        if (cls.Contains("SysTabControl32", StringComparison.OrdinalIgnoreCase)) return "tab";
        if (cls.Contains("ComboBoxEx", StringComparison.OrdinalIgnoreCase)) return null;   // Rahmen; das innere ComboBox zählt
        if (cls.Contains("ComboBox", StringComparison.OrdinalIgnoreCase)) return "comboBox";
        if (cls.Contains("ListBox", StringComparison.OrdinalIgnoreCase)) return "listBox";
        return null;
    }

    /// <summary>
    /// Anzahl, Auswahl (1-basiert wie ControlChooseIndex) und Einträge eines Listen-Steuerelements; mit <paramref name="pt"/>
    /// auch der Eintrag unter dem Punkt. Texte nur ab Stufe Control (Einträge ab All), höchstens 50 × 200 Zeichen.
    /// </summary>
    internal static JsonObject? ListInfo(nint h, string cls, TextLevel texts, Win32.POINT? pt)
    {
        if (texts == TextLevel.None || h == 0) return null;
        var kind = ListKind(cls);
        if (kind == null) return null;
        try
        {
            if (LooksSensitive(Win32.GetWindowTitle(Win32.GetAncestor(h, 2)))) return null;
            var sw = Stopwatch.StartNew();
            var o = kind switch
            {
                "comboBox" => ComboInfo(h, texts, sw),
                "listBox" or "comboList" => ListBoxInfo(h, texts, pt, sw),
                "listView" => ListViewInfo(h, texts, pt, sw),
                "treeView" => TreeViewInfo(h, texts, pt),
                "tab" => TabInfo(h, texts, pt, sw),
                _ => null
            };
            if (o == null) return null;
            o["kind"] = kind;
            return o;
        }
        catch { return null; }
    }

    static bool Send(nint h, uint msg, nint w, nint l, out nint r) => SendMessageTimeout(h, msg, w, l, 0x0002, TextTimeoutMs, out r) != 0;

    /// <summary>Texte, die wie Geheimnisse aussehen, werden nicht übernommen.</summary>
    static string? SafeItem(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        if (ReadLayer.Patterns.Flag(s) != null) return "[ausgelassen]";
        return Trunc(s, MaxItemText);
    }

    static bool LooksSensitive(string? title) => title != null && System.Text.RegularExpressions.Regex.IsMatch(title, @"(?i)(passw|kennwort|password|pin\b|anmeld|login|sign in|credential|tresor|vault|keepass|1password|bitwarden)");

    static JsonObject? ComboInfo(nint h, TextLevel texts, Stopwatch sw)
    {
        if (!Send(h, 0x0146 /*CB_GETCOUNT*/, 0, 0, out var cnt) || (long)cnt < 0) return null;
        long style = Win32.GetWindowLongPtr(h, -16);
        bool strings = (style & 0x30) == 0 || (style & 0x200) != 0;   // nicht selbst gezeichnet oder CBS_HASSTRINGS
        var o = new JsonObject { ["count"] = (long)cnt };
        if ((style & 0x3) == 0x3) o["style"] = "dropDownList"; else if ((style & 0x3) == 0x2) o["style"] = "dropDown"; else o["style"] = "simple";
        if (Send(h, 0x0147 /*CB_GETCURSEL*/, 0, 0, out var sel) && (long)sel >= 0)
        {
            o["selIndex"] = (long)sel + 1;
            if (strings && ComboText(h, (int)sel) is { } st) o["selText"] = SafeItem(st);
        }
        if (strings && texts == TextLevel.All) AddItems(o, (int)cnt, i => ComboText(h, i), sw);
        return o;
    }

    static string? ComboText(nint h, int i)
    {
        if (!Send(h, 0x0149 /*CB_GETLBTEXTLEN*/, i, 0, out var len) || (long)len < 0 || (long)len > 4096) return null;
        var sb = new StringBuilder((int)len + 2);
        return SendMessageTimeoutText(h, 0x0148 /*CB_GETLBTEXT*/, i, sb, 0x0002, TextTimeoutMs, out var r) != 0 && (long)r >= 0 ? sb.ToString() : null;
    }

    static JsonObject? ListBoxInfo(nint h, TextLevel texts, Win32.POINT? pt, Stopwatch sw)
    {
        if (!Send(h, 0x018B /*LB_GETCOUNT*/, 0, 0, out var cnt) || (long)cnt < 0) return null;
        long style = Win32.GetWindowLongPtr(h, -16);
        bool strings = (style & 0x30) == 0 || (style & 0x40) != 0;   // LBS_HASSTRINGS
        bool multi = (style & 0x0808) != 0;                          // LBS_MULTIPLESEL | LBS_EXTENDEDSEL
        var o = new JsonObject { ["count"] = (long)cnt };
        if (multi) { o["multiSelect"] = true; if (Send(h, 0x0190 /*LB_GETSELCOUNT*/, 0, 0, out var sc) && (long)sc >= 0) o["selCount"] = (long)sc; }
        if (!multi && Send(h, 0x0188 /*LB_GETCURSEL*/, 0, 0, out var sel) && (long)sel >= 0)
        {
            o["selIndex"] = (long)sel + 1;
            if (strings && ListBoxText(h, (int)sel) is { } st) o["selText"] = SafeItem(st);
        }
        if (pt is { } p)
        {
            var cp = p; ScreenToClient(h, ref cp);
            if (Send(h, 0x01A9 /*LB_ITEMFROMPOINT*/, 0, (nint)((cp.Y << 16) | (cp.X & 0xFFFF)), out var hit) && ((long)hit >> 16 & 0xFFFF) == 0)
            {
                int idx = (int)((long)hit & 0xFFFF);
                if (idx < (long)cnt)
                {
                    var ap = new JsonObject { ["index"] = idx + 1 };
                    if (strings && ListBoxText(h, idx) is { } at) ap["text"] = SafeItem(at);
                    o["atPoint"] = ap;
                }
            }
        }
        if (strings && texts == TextLevel.All) AddItems(o, (int)cnt, i => ListBoxText(h, i), sw);
        return o;
    }

    static string? ListBoxText(nint h, int i)
    {
        if (!Send(h, 0x018A /*LB_GETTEXTLEN*/, i, 0, out var len) || (long)len < 0 || (long)len > 4096) return null;
        var sb = new StringBuilder((int)len + 2);
        return SendMessageTimeoutText(h, 0x0189 /*LB_GETTEXT*/, i, sb, 0x0002, TextTimeoutMs, out var r) != 0 && (long)r >= 0 ? sb.ToString() : null;
    }

    static void AddItems(JsonObject o, int count, Func<int, string?> text, Stopwatch sw)
    {
        var items = new JsonArray();
        int n = Math.Min(count, MaxItems);
        for (int i = 0; i < n && sw.ElapsedMilliseconds < ListBudgetMs; i++) items.Add((JsonNode?)SafeItem(text(i) ?? ""));
        if (items.Count > 0) o["items"] = items;
        if (count > items.Count) o["itemsTruncated"] = true;
    }

    /// <summary>Aufgeklappte Liste eines Kombinationsfelds (ComboLBox): zugehöriges ComboBox im Vordergrundfenster suchen.</summary>
    internal static JsonObject? ComboListOwner(nint list)
    {
        try
        {
            nint fg = Win32.GetForegroundWindow();
            nint top = fg != 0 ? Win32.GetAncestor(fg, 2) : 0;
            if (top == 0 || top == list) return null;
            nint found = 0;
            var sw = Stopwatch.StartNew();
            EnumChildWindows(top, (h, _) =>
            {
                if (sw.ElapsedMilliseconds > 100) return false;
                if (ListKind(Win32.GetClassNameOf(h)) != "comboBox") return true;
                var ci = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
                if (GetComboBoxInfo(h, ref ci) && ci.hwndList == list) { found = h; return false; }
                return true;
            }, 0);
            if (found == 0) return null;
            var o = new JsonObject { ["hwnd"] = Json.Hex(found), ["class"] = Win32.GetClassNameOf(found) };
            if (ClassNN(top, found) is { } nn) o["classNN"] = nn;
            o["window"] = OwnerInfo(top);
            return o;
        }
        catch { return null; }
    }

    // ------------------------------------------------------------------------------------------
    // Eingabefelder: Schreibmarke und Auswahl (nie der Inhalt)
    // ------------------------------------------------------------------------------------------

    internal static JsonObject? Caret(nint h, string cls, long style)
    {
        if (!IsEditClass(cls) || cls.Contains("Scintilla", StringComparison.OrdinalIgnoreCase)) return null;
        if ((style & 0x20) != 0) return null;   // ES_PASSWORD
        try
        {
            if (!Send(h, 0x00B0 /*EM_GETSEL*/, 0, 0, out var sel) || (long)sel == -1) return null;
            int start = (int)((long)sel & 0xFFFF), end = (int)(((long)sel >> 16) & 0xFFFF);
            if (end < start) (start, end) = (end, start);
            var o = new JsonObject();
            if (Send(h, 0x00C9 /*EM_LINEFROMCHAR*/, start, 0, out var line) && (long)line >= 0)
            {
                o["line"] = (long)line + 1;
                if (Send(h, 0x00BB /*EM_LINEINDEX*/, line, 0, out var li) && (long)li >= 0) o["col"] = start - (long)li + 1;
            }
            o["selStart"] = start;
            o["selLen"] = end - start;
            if (end >= 0xFFFF) o["approx"] = true;
            if ((style & 0x4) != 0) { o["multiline"] = true; if (Send(h, 0x00BA /*EM_GETLINECOUNT*/, 0, 0, out var lc)) o["lines"] = (long)lc; }
            if ((style & 0x800) != 0) o["readOnly"] = true;
            return o;
        }
        catch { return null; }
    }

    // ------------------------------------------------------------------------------------------
    // Statusleiste
    // ------------------------------------------------------------------------------------------

    /// <summary>Texte der Teile der ersten sichtbaren Statusleiste (msctls_statusbar32) – wie StatusBarGetText.</summary>
    internal static JsonObject? StatusBar(nint top, TextLevel texts)
    {
        if (texts != TextLevel.All) return null;
        try
        {
            nint bar = 0;
            EnumChildWindows(top, (h, _) =>
            {
                if (Win32.GetClassNameOf(h).Contains("msctls_statusbar32", StringComparison.OrdinalIgnoreCase) && Win32.IsWindowVisible(h)) { bar = h; return false; }
                return true;
            }, 0);
            if (bar == 0) return null;
            if (!Send(bar, 0x0406 /*SB_GETPARTS*/, 0, 0, out var np) || (long)np <= 0) return null;
            int parts = (int)Math.Min((long)np, MaxParts);
            // Texte über UI Automation (P9: kein Puffer im Zielprozess)
            var list = new JsonArray();
            foreach (var t in UiaStatusParts(bar, parts)) list.Add((JsonNode?)SafeItem(t ?? ""));
            if (list.All(x => string.IsNullOrEmpty(x?.GetValue<string>()))) return null;
            var o = new JsonObject { ["parts"] = list };
            if (ClassNN(top, bar) is { } nn) o["classNN"] = nn;
            return o;
        }
        catch { return null; }
    }

    // ------------------------------------------------------------------------------------------
    // Dateidialoge (#32770)
    // ------------------------------------------------------------------------------------------

    /// <summary>Ordner (Adressleiste „Adresse: …“) und ClassNN des Dateinamenfelds (ID 1148) eines Datei-öffnen/-speichern-Dialogs.</summary>
    internal static JsonObject? FileDialog(nint top, TextLevel texts)
    {
        if (texts == TextLevel.None || Win32.GetClassNameOf(top) != "#32770") return null;
        try
        {
            string? folder = null; nint nameEdit = 0;
            var sw = Stopwatch.StartNew();
            EnumChildWindows(top, (h, _) =>
            {
                if (sw.ElapsedMilliseconds > 120) return false;
                string cls = Win32.GetClassNameOf(h);
                if (folder == null && texts == TextLevel.All && cls == "ToolbarWindow32" && ReadText(h) is { } t)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(t, @"^[^:\\]{2,40}:\s*(.+)$");
                    if (m.Success && (m.Groups[1].Value.Contains('\\') || m.Groups[1].Value.Length > 0 && t.StartsWith("Ad", StringComparison.OrdinalIgnoreCase))) folder = m.Groups[1].Value.Trim();
                }
                if (nameEdit == 0 && cls == "Edit")
                {
                    nint p1 = GetParent(h), p2 = p1 != 0 ? GetParent(p1) : 0;
                    if (GetDlgCtrlID(h) == 1148 || p1 != 0 && GetDlgCtrlID(p1) == 1148 || p2 != 0 && GetDlgCtrlID(p2) == 1148) nameEdit = h;
                }
                return true;
            }, 0);
            if (folder == null && nameEdit == 0) return null;
            var o = new JsonObject();
            if (folder != null) { o["folder"] = Trunc(folder, MaxText); if (System.Text.RegularExpressions.Regex.IsMatch(folder, @"^([A-Za-z]:\\|\\\\)")) o["isPath"] = true; }
            if (nameEdit != 0 && ClassNN(top, nameEdit) is { } nn) o["fileNameControl"] = nn;
            return o;
        }
        catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential)] struct COMBOBOXINFO { public int cbSize; public RECT rcItem, rcButton; public int stateButton; public nint hwndCombo, hwndItem, hwndList; }
    [DllImport("user32.dll")] static extern bool GetComboBoxInfo(nint h, ref COMBOBOXINFO ci);
    [DllImport("user32.dll")] static extern int MenuItemFromPoint(nint h, nint menu, Win32.POINT pt);
    [DllImport("user32.dll")] static extern uint GetMenuItemID(nint menu, int pos);
    [DllImport("user32.dll")] static extern nint GetSubMenu(nint menu, int pos);
    [DllImport("user32.dll")] static extern int GetMenuItemCount(nint menu);
    [DllImport("user32.dll")] static extern uint GetMenuState(nint menu, uint id, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMenuStringW")] static extern int GetMenuString(nint menu, uint id, StringBuilder sb, int max, uint flags);
    [DllImport("user32.dll")] static extern nint GetMenu(nint h);
    [DllImport("user32.dll")] static extern nint GetSystemMenu(nint h, bool revert);
}
