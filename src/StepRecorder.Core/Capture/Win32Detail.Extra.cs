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
/// Steuerelemente mit Zeigern in Nachrichten (ListView, TreeView, Tab, Statusleiste) werden über einen Puffer im
/// Zielprozess gelesen; nach einer Zeitüberschreitung wird dieser Puffer bewusst nicht freigegeben.
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

    static JsonObject? ListViewInfo(nint h, TextLevel texts, Win32.POINT? pt, Stopwatch sw)
    {
        if (!Send(h, 0x1004 /*LVM_GETITEMCOUNT*/, 0, 0, out var cnt) || (long)cnt < 0) return null;
        var o = new JsonObject { ["count"] = (long)cnt };
        if (Send(h, 0x108F /*LVM_GETVIEW*/, 0, 0, out var view)) o["view"] = (long)view switch { 0 => "icon", 1 => "details", 2 => "smallIcon", 3 => "list", 4 => "tile", _ => null };
        if (Send(h, 0x1032 /*LVM_GETSELECTEDCOUNT*/, 0, 0, out var sc)) o["selCount"] = (long)sc;
        int cols = 1;
        if (Send(h, 0x101F /*LVM_GETHEADER*/, 0, 0, out var hdr) && hdr != 0 && Send(hdr, 0x1200 /*HDM_GETITEMCOUNT*/, 0, 0, out var hc) && (long)hc > 0) cols = (int)Math.Min((long)hc, MaxColumns);
        o["columns"] = cols;
        using var rm = Remote.Open(h, 4096);
        var rows = new JsonArray();
        int first = -1;
        for (int k = 0, idx = -1; k < MaxSelRows && sw.ElapsedMilliseconds < ListBudgetMs; k++)
        {
            if (!Send(h, 0x100C /*LVM_GETNEXTITEM*/, idx, 2 /*LVNI_SELECTED*/, out var next) || (long)next < 0) break;
            idx = (int)next;
            if (first < 0) first = idx;
            if (rm != null) rows.Add(ListViewRow(h, rm, idx, cols));
        }
        if (first >= 0) o["selIndex"] = first + 1;
        if (rows.Count > 0) { o["selRows"] = rows; if (rows[0] is JsonArray r0 && r0.Count > 0) o["selText"] = r0[0]?.GetValue<string>(); }
        if (pt is { } p && rm != null)
        {
            var cp = p; ScreenToClient(h, ref cp);
            // LVHITTESTINFO: POINT pt; UINT flags; int iItem; int iSubItem; int iGroup
            var buf = new byte[24];
            BitConverter.GetBytes(cp.X).CopyTo(buf, 0); BitConverter.GetBytes(cp.Y).CopyTo(buf, 4);
            if (rm.Write(0, buf) && rm.SendTo(h, 0x1039 /*LVM_SUBITEMHITTEST*/, 0, rm.Addr(0), out var hit) && (long)hit >= 0 && rm.Read(0, 24) is { } back)
            {
                int item = BitConverter.ToInt32(back, 12), subItem = BitConverter.ToInt32(back, 16);
                if (item >= 0)
                {
                    var ap = new JsonObject { ["index"] = item + 1 };
                    if (subItem > 0) ap["column"] = subItem + 1;
                    var row = ListViewRow(h, rm, item, cols);
                    ap["row"] = row;
                    if (row.Count > 0) ap["text"] = row[0]?.GetValue<string>();
                    o["atPoint"] = ap;
                }
            }
        }
        return o;
    }

    /// <summary>Spaltentexte einer ListView-Zeile über LVM_GETITEMTEXTW mit LVITEM im Zielprozess.</summary>
    static JsonArray ListViewRow(nint h, Remote rm, int item, int cols)
    {
        var row = new JsonArray();
        const int textOff = 512, cch = MaxItemText + 1;
        for (int c = 0; c < cols; c++)
        {
            var lv = new byte[rm.Target32 ? 60 : 88];
            BitConverter.GetBytes(item).CopyTo(lv, 4);   // iItem
            BitConverter.GetBytes(c).CopyTo(lv, 8);      // iSubItem
            if (rm.Target32) { BitConverter.GetBytes((int)(long)rm.Addr(textOff)).CopyTo(lv, 20); BitConverter.GetBytes(cch).CopyTo(lv, 24); }
            else { BitConverter.GetBytes((long)rm.Addr(textOff)).CopyTo(lv, 24); BitConverter.GetBytes(cch).CopyTo(lv, 32); }
            if (!rm.Write(0, lv) || !rm.Write(textOff, new byte[2])) break;
            if (!rm.SendTo(h, 0x1073 /*LVM_GETITEMTEXTW*/, item, rm.Addr(0), out var len)) break;
            row.Add((JsonNode?)SafeItem(rm.ReadString(textOff, (int)Math.Clamp((long)len, 0, MaxItemText)) ?? ""));
        }
        return row;
    }

    static JsonObject? TreeViewInfo(nint h, TextLevel texts, Win32.POINT? pt)
    {
        if (!Send(h, 0x1105 /*TVM_GETCOUNT*/, 0, 0, out var cnt) || (long)cnt < 0) return null;
        var o = new JsonObject { ["count"] = (long)cnt };
        using var rm = Remote.Open(h, 2048);
        if (rm == null) return o;
        if (Send(h, 0x110A /*TVM_GETNEXTITEM*/, 9 /*TVGN_CARET*/, 0, out var sel) && sel != 0)
        {
            if (TreeText(h, rm, sel) is { } st) o["selText"] = SafeItem(st);
            if (TreePath(h, rm, sel) is { Count: > 1 } sp) o["selPath"] = sp;
        }
        if (pt is { } p)
        {
            var cp = p; ScreenToClient(h, ref cp);
            // TVHITTESTINFO: POINT pt; UINT flags; HTREEITEM hItem
            var buf = new byte[24];
            BitConverter.GetBytes(cp.X).CopyTo(buf, 0); BitConverter.GetBytes(cp.Y).CopyTo(buf, 4);
            if (rm.Write(0, buf) && rm.SendTo(h, 0x1111 /*TVM_HITTEST*/, 0, rm.Addr(0), out var hit) && hit != 0)
            {
                var ap = new JsonObject();
                if (TreeText(h, rm, hit) is { } at) ap["text"] = SafeItem(at);
                if (TreePath(h, rm, hit) is { Count: > 0 } path) ap["path"] = path;
                if (ap.Count > 0) o["atPoint"] = ap;
            }
        }
        return o;
    }

    static string? TreeText(nint h, Remote rm, nint item)
    {
        const int textOff = 512, cch = MaxItemText + 1;
        var tv = new byte[rm.Target32 ? 40 : 56];
        BitConverter.GetBytes(0x0001 | 0x0010).CopyTo(tv, 0);   // TVIF_TEXT | TVIF_HANDLE
        if (rm.Target32) { BitConverter.GetBytes((int)(long)item).CopyTo(tv, 4); BitConverter.GetBytes((int)(long)rm.Addr(textOff)).CopyTo(tv, 16); BitConverter.GetBytes(cch).CopyTo(tv, 20); }
        else { BitConverter.GetBytes((long)item).CopyTo(tv, 8); BitConverter.GetBytes((long)rm.Addr(textOff)).CopyTo(tv, 24); BitConverter.GetBytes(cch).CopyTo(tv, 32); }
        if (!rm.Write(256, tv) || !rm.Write(textOff, new byte[2])) return null;
        if (!rm.SendTo(h, 0x113E /*TVM_GETITEMW*/, 0, rm.Addr(256), out var ok) || ok == 0) return null;
        return rm.ReadString(textOff, MaxItemText);
    }

    /// <summary>Pfad eines TreeView-Eintrags von der Wurzel (höchstens 8 Ebenen).</summary>
    static JsonArray? TreePath(nint h, Remote rm, nint item)
    {
        var list = new List<string>();
        for (int i = 0; i < 8 && item != 0; i++)
        {
            list.Add(SafeItem(TreeText(h, rm, item) ?? "") ?? "");
            if (!Send(h, 0x110A, 3 /*TVGN_PARENT*/, item, out var parent)) break;
            item = parent;
        }
        list.Reverse();
        return new JsonArray(list.Select(x => (JsonNode)x).ToArray());
    }

    static JsonObject? TabInfo(nint h, TextLevel texts, Win32.POINT? pt, Stopwatch sw)
    {
        if (!Send(h, 0x1304 /*TCM_GETITEMCOUNT*/, 0, 0, out var cnt) || (long)cnt < 0) return null;
        var o = new JsonObject { ["count"] = (long)cnt };
        using var rm = Remote.Open(h, 2048);
        if (Send(h, 0x130B /*TCM_GETCURSEL*/, 0, 0, out var sel) && (long)sel >= 0)
        {
            o["selIndex"] = (long)sel + 1;
            if (rm != null && TabText(h, rm, (int)sel) is { } st) o["selText"] = SafeItem(st);
        }
        if (pt is { } p && rm != null)
        {
            var cp = p; ScreenToClient(h, ref cp);
            var buf = new byte[12];
            BitConverter.GetBytes(cp.X).CopyTo(buf, 0); BitConverter.GetBytes(cp.Y).CopyTo(buf, 4);
            if (rm.Write(0, buf) && rm.SendTo(h, 0x130D /*TCM_HITTEST*/, 0, rm.Addr(0), out var hit) && (long)hit >= 0)
            {
                var ap = new JsonObject { ["index"] = (long)hit + 1 };
                if (TabText(h, rm, (int)hit) is { } at) ap["text"] = SafeItem(at);
                o["atPoint"] = ap;
            }
        }
        if (rm != null && texts == TextLevel.All) AddItems(o, (int)cnt, i => TabText(h, rm, i), sw);
        return o;
    }

    static string? TabText(nint h, Remote rm, int i)
    {
        const int textOff = 512, cch = MaxItemText + 1;
        var tc = new byte[rm.Target32 ? 28 : 40];
        BitConverter.GetBytes(0x0001).CopyTo(tc, 0);   // TCIF_TEXT
        if (rm.Target32) { BitConverter.GetBytes((int)(long)rm.Addr(textOff)).CopyTo(tc, 12); BitConverter.GetBytes(cch).CopyTo(tc, 16); }
        else { BitConverter.GetBytes((long)rm.Addr(textOff)).CopyTo(tc, 16); BitConverter.GetBytes(cch).CopyTo(tc, 24); }
        if (!rm.Write(256, tc) || !rm.Write(textOff, new byte[2])) return null;
        if (!rm.SendTo(h, 0x133C /*TCM_GETITEMW*/, i, rm.Addr(256), out var ok) || ok == 0) return null;
        return rm.ReadString(textOff, MaxItemText);
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
            using var rm = Remote.Open(bar, 4096);
            var list = new JsonArray();
            for (int i = 0; i < parts; i++)
            {
                if (!Send(bar, 0x040C /*SB_GETTEXTLENGTHW*/, i, 0, out var lr)) break;
                int len = (int)((long)lr & 0xFFFF), type = (int)(((long)lr >> 16) & 0xFFFF);
                string? t = null;
                if ((type & 0x1000) == 0 /*SBT_OWNERDRAW*/ && len > 0 && len < 2000 && rm != null && rm.Write(0, new byte[2]) && rm.SendTo(bar, 0x040D /*SB_GETTEXTW*/, i, rm.Addr(0), out _))
                    t = rm.ReadString(0, len);
                list.Add((JsonNode?)SafeItem(t ?? ""));
            }
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

    // ------------------------------------------------------------------------------------------
    // Puffer im Zielprozess (für Nachrichten mit Zeigern, die Windows nicht selbst überträgt)
    // ------------------------------------------------------------------------------------------

    internal sealed class Remote : IDisposable
    {
        nint _proc, _mem; bool _leak;
        public bool Target32;

        public static Remote? Open(nint hwnd, int size)
        {
            if (!Environment.Is64BitProcess) return null;   // 32-Bit-Recorder: Strukturen eines 64-Bit-Ziels nicht sicher
            Win32.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return null;
            nint p = Win32.OpenProcess(0x0008 | 0x0010 | 0x0020 | 0x1000, false, pid);   // VM_OPERATION | VM_READ | VM_WRITE | QUERY_LIMITED
            if (p == 0) return null;
            nint mem = VirtualAllocEx(p, 0, size, 0x3000, 0x04);
            if (mem == 0) { Win32.CloseHandle(p); return null; }
            bool wow = IsWow64Process(p, out var w) && w;
            return new Remote { _proc = p, _mem = mem, Target32 = wow };
        }

        public nint Addr(int off) => _mem + off;
        public bool Write(int off, byte[] data) => WriteProcessMemory(_proc, _mem + off, data, data.Length, out _);
        public byte[]? Read(int off, int len) { var b = new byte[len]; return ReadProcessMemory(_proc, _mem + off, b, len, out _) ? b : null; }
        public string? ReadString(int off, int maxChars)
        {
            var b = Read(off, (maxChars + 1) * 2);
            if (b == null) return null;
            var s = Encoding.Unicode.GetString(b);
            int z = s.IndexOf('\0');
            return z >= 0 ? s[..z] : s;
        }

        /// <summary>Nachricht mit Zeiger in diesen Puffer; nach Zeitüberschreitung wird der Puffer nicht freigegeben.</summary>
        public bool SendTo(nint h, uint msg, nint w, nint l, out nint r)
        {
            r = 0;
            if (_leak) return false;   // nach einer Zeitüberschreitung keine weiteren Nachrichten an diesen Puffer
            if (SendMessageTimeout(h, msg, w, l, 0x0002, TextTimeoutMs, out r) != 0) return true;
            _leak = true;
            return false;
        }

        public void Dispose()
        {
            if (_mem != 0 && !_leak) VirtualFreeEx(_proc, _mem, 0, 0x8000);
            if (_proc != 0) Win32.CloseHandle(_proc);
            _mem = _proc = 0;
        }
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
    [DllImport("kernel32.dll")] static extern nint VirtualAllocEx(nint p, nint addr, nint size, uint type, uint protect);
    [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(nint p, nint addr, nint size, uint type);
    [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(nint p, nint addr, byte[] buf, nint size, out nint written);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(nint p, nint addr, byte[] buf, nint size, out nint read);
    [DllImport("kernel32.dll")] static extern bool IsWow64Process(nint p, out bool wow);
}
