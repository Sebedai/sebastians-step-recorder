using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows.Automation;
using StepRecorder.Native;

namespace StepRecorder.Capture;

/// <summary>
/// Einträge von ListView, TreeView, Registerkarten und Statusleiste über UI Automation statt über einen Puffer im
/// Zielprozess (10 §2, P9: kein Lesen fremden Prozessspeichers). Fensternachrichten liefern nur Zahlen
/// (Anzahl, Auswahl-Index, Ansicht); Texte kommen aus den UIA-Namen. Gleiche Datenform wie zuvor.
/// </summary>
public static partial class Win32Detail
{
    static AutomationElement? UiaOf(nint h) { try { return h == 0 ? null : AutomationElement.FromHandle(h); } catch { return null; } }

    static string? UiaName(AutomationElement? e) { try { return e?.Current.Name; } catch { return null; } }

    static AutomationElement? UiaAt(Win32.POINT p) { try { return AutomationElement.FromPoint(new System.Windows.Point(p.X, p.Y)); } catch { return null; } }

    /// <summary>Vom Element unter dem Punkt aufwärts bis zu einem Eintrag der gewünschten Art innerhalb von <paramref name="container"/>.</summary>
    static AutomationElement? UiaItemAt(Win32.POINT p, nint container, params ControlType[] types)
    {
        var e = UiaAt(p);
        var walker = TreeWalker.ControlViewWalker;
        for (int i = 0; i < 6 && e != null; i++)
        {
            try
            {
                if (types.Contains(e.Current.ControlType)) return e;
                if (e.Current.NativeWindowHandle == (int)container && i > 0) return null;
                e = walker.GetParent(e);
            }
            catch { return null; }
        }
        return null;
    }

    static List<AutomationElement> UiaSelection(AutomationElement? list, int max)
    {
        var res = new List<AutomationElement>();
        try
        {
            if (list?.GetCurrentPattern(SelectionPattern.Pattern) is SelectionPattern sp)
                foreach (var s in sp.Current.GetSelection().Take(max)) res.Add(s);
        }
        catch { }
        return res;
    }

    static List<AutomationElement> UiaChildren(AutomationElement? e, Stopwatch sw, int max)
    {
        var res = new List<AutomationElement>();
        if (e == null) return res;
        var walker = TreeWalker.ControlViewWalker;
        try
        {
            for (var c = walker.GetFirstChild(e); c != null && res.Count < max && sw.ElapsedMilliseconds < ListBudgetMs; c = walker.GetNextSibling(c)) res.Add(c);
        }
        catch { }
        return res;
    }

    /// <summary>Spaltentexte einer Zeile: Namen der Kindelemente (Detailansicht), sonst der Name des Eintrags.</summary>
    static JsonArray UiaRow(AutomationElement item, int cols, Stopwatch sw)
    {
        var row = new JsonArray();
        var cells = UiaChildren(item, sw, cols).Select(UiaName).Where(n => !string.IsNullOrEmpty(n)).ToList();
        if (cells.Count == 0) cells.Add(UiaName(item));
        foreach (var c in cells.Take(Math.Max(1, cols))) row.Add((JsonNode?)SafeItem(c ?? ""));
        return row;
    }

    /// <summary>Position eines Eintrags unter seinen Geschwistern (1-basiert) innerhalb des Zeitbudgets, sonst null.</summary>
    static int? UiaIndex(AutomationElement item, Stopwatch sw)
    {
        var walker = TreeWalker.ControlViewWalker;
        try
        {
            ControlType ct = item.Current.ControlType;
            int n = 1;
            for (var c = walker.GetPreviousSibling(item); c != null; c = walker.GetPreviousSibling(c))
            {
                if (sw.ElapsedMilliseconds > ListBudgetMs || n > 2000) return null;
                if (c.Current.ControlType == ct) n++;
            }
            return n;
        }
        catch { return null; }
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
        if (Send(h, 0x100C /*LVM_GETNEXTITEM*/, -1, 2 /*LVNI_SELECTED*/, out var first) && (long)first >= 0) o["selIndex"] = (long)first + 1;
        var list = UiaOf(h);
        var rows = new JsonArray();
        foreach (var item in UiaSelection(list, MaxSelRows)) { if (sw.ElapsedMilliseconds > ListBudgetMs) break; rows.Add(UiaRow(item, cols, sw)); }
        if (rows.Count > 0) { o["selRows"] = rows; if (rows[0] is JsonArray r0 && r0.Count > 0) o["selText"] = r0[0]?.GetValue<string>(); }
        if (pt is { } p && UiaItemAt(p, h, ControlType.ListItem, ControlType.DataItem) is { } hit)
        {
            var ap = new JsonObject();
            if (UiaIndex(hit, sw) is int idx) ap["index"] = idx;
            var row = UiaRow(hit, cols, sw);
            ap["row"] = row;
            if (row.Count > 0) ap["text"] = row[0]?.GetValue<string>();
            // Spalte: Kindelement, dessen Rechteck den Punkt enthält
            var cells = UiaChildren(hit, sw, cols);
            for (int c = 0; c < cells.Count; c++)
            {
                try { var r = cells[c].Current.BoundingRectangle; if (r.Contains(p.X, p.Y)) { if (c > 0) ap["column"] = c + 1; break; } } catch { }
            }
            o["atPoint"] = ap;
        }
        return o;
    }

    static JsonObject? TreeViewInfo(nint h, TextLevel texts, Win32.POINT? pt)
    {
        if (!Send(h, 0x1105 /*TVM_GETCOUNT*/, 0, 0, out var cnt) || (long)cnt < 0) return null;
        var o = new JsonObject { ["count"] = (long)cnt };
        var sw = Stopwatch.StartNew();
        var tree = UiaOf(h);
        if (UiaSelection(tree, 1).FirstOrDefault() is { } sel)
        {
            if (UiaName(sel) is { } st) o["selText"] = SafeItem(st);
            if (UiaTreePath(sel, h) is { Count: > 1 } sp) o["selPath"] = sp;
        }
        if (pt is { } p && UiaItemAt(p, h, ControlType.TreeItem) is { } hit)
        {
            var ap = new JsonObject();
            if (UiaName(hit) is { } at) ap["text"] = SafeItem(at);
            if (UiaTreePath(hit, h) is { Count: > 0 } path) ap["path"] = path;
            if (ap.Count > 0) o["atPoint"] = ap;
        }
        return o;
    }

    /// <summary>Pfad eines Baum-Eintrags von der Wurzel (höchstens 8 Ebenen).</summary>
    static JsonArray? UiaTreePath(AutomationElement item, nint tree)
    {
        var list = new List<string>();
        var walker = TreeWalker.ControlViewWalker;
        var e = item;
        try
        {
            for (int i = 0; i < 8 && e != null; i++)
            {
                if (e.Current.ControlType != ControlType.TreeItem) break;
                list.Add(SafeItem(UiaName(e) ?? "") ?? "");
                e = walker.GetParent(e);
            }
        }
        catch { }
        list.Reverse();
        return list.Count == 0 ? null : new JsonArray(list.Select(x => (JsonNode)x).ToArray());
    }

    static JsonObject? TabInfo(nint h, TextLevel texts, Win32.POINT? pt, Stopwatch sw)
    {
        if (!Send(h, 0x1304 /*TCM_GETITEMCOUNT*/, 0, 0, out var cnt) || (long)cnt < 0) return null;
        var o = new JsonObject { ["count"] = (long)cnt };
        var tabs = UiaChildren(UiaOf(h), sw, MaxItems).Where(e => { try { return e.Current.ControlType == ControlType.TabItem; } catch { return false; } }).ToList();
        if (Send(h, 0x130B /*TCM_GETCURSEL*/, 0, 0, out var sel) && (long)sel >= 0)
        {
            o["selIndex"] = (long)sel + 1;
            if ((int)(long)sel < tabs.Count && UiaName(tabs[(int)(long)sel]) is { } st) o["selText"] = SafeItem(st);
        }
        if (pt is { } p && UiaItemAt(p, h, ControlType.TabItem) is { } hit)
        {
            var ap = new JsonObject();
            int i = tabs.FindIndex(t => { try { return Automation.Compare(t, hit); } catch { return false; } });
            if (i >= 0) ap["index"] = i + 1;
            if (UiaName(hit) is { } at) ap["text"] = SafeItem(at);
            o["atPoint"] = ap;
        }
        if (texts == TextLevel.All && tabs.Count > 0) AddItems(o, tabs.Count, i => UiaName(tabs[i]), sw);
        return o;
    }

    /// <summary>Texte der Teile einer Statusleiste (Kindelemente der UIA-Statusleiste, sonst ihr Name).</summary>
    static IEnumerable<string?> UiaStatusParts(nint bar, int parts)
    {
        var sw = Stopwatch.StartNew();
        var el = UiaOf(bar);
        var kids = UiaChildren(el, sw, parts).Select(UiaName).ToList();
        if (kids.Count == 0 && UiaName(el) is { Length: > 0 } n) kids.Add(n);
        return kids;
    }
}
