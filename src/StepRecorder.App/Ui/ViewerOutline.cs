using System.Text.Json.Nodes;
using System.Windows.Controls;
using StepRecorder.Data;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>Gliederung im Editor (13, Paket 24): Kapitel verschieben, Unterkapitel, Kapitel ausblenden, Textabschnitte und Bausteine.</summary>
sealed partial class ViewerWindow
{
    /// <summary>Folge wie in der Liste (samt ausgeblendeten Schritten) mit der Ebene der Kapitelanfänge.</summary>
    List<SeqItem> SeqItems() => _view.Effective(importance: "all", includeManual: true, ordered: true)
        .Select(x => new SeqItem(x.Anchor, x.ChapterTitle is { Length: > 0 } ? x.ChapterLevel : 0)).ToList();

    /// <summary>Kapitel, in dem der gewählte Schritt liegt (Anker des Kapitelbeginns).</summary>
    long? CurrentChapter() => Sel is { } s ? (s.ChapterTitle != null ? s.Anchor : ChapterOps.ChapterOf(SeqItems(), s.Anchor)) : null;

    void MoveChapter(int dir)
    {
        if (CurrentChapter() is not { } start) { _status.Text = Loc.L("Der gewählte Schritt liegt in keinem Kapitel.", "The selected step is not in a chapter."); return; }
        var order = ChapterOps.MoveChapter(SeqItems(), start, dir);
        if (order == null) { _status.Text = dir < 0 ? Loc.L("Das Kapitel steht schon oben.", "The chapter is already at the top.") : Loc.L("Das Kapitel steht schon unten.", "The chapter is already at the bottom."); return; }
        long keep = Sel?.Anchor ?? start;
        Change(e => e["order"] = new JsonArray(order.Select(x => (JsonNode)x).ToArray()));
        SelectAnchor(keep);
        _status.Text = Loc.F("Kapitel verschoben – rückgängig mit {0}", "Chapter moved – undo with {0}", Cmd("undo").Gesture);
    }

    /// <summary>Kapitel ↔ Unterkapitel (Ebene 2 nur, wenn davor schon ein Kapitel beginnt).</summary>
    void ToggleChapterLevel()
    {
        if (CurrentChapter() is not { } start) return;
        var seq = SeqItems();
        int idx = seq.FindIndex(x => x.Anchor == start);
        bool hasParent = seq.Take(idx).Any(x => x.ChapterLevel > 0);
        var st = _view.All.Concat(_view.Manual).FirstOrDefault(x => x.Anchor == start);
        bool toSub = st?.ChapterLevel != 2;
        if (toSub && !hasParent) { _status.Text = Loc.L("Ein Unterkapitel braucht davor ein Kapitel.", "A subchapter needs a chapter before it."); return; }
        Change(e => { var se = StepEdit(e, start); if (se["chapter"] is JsonObject c) { if (toSub) c["level"] = 2; else c.Remove("level"); } });
        _status.Text = toSub ? Loc.L("Jetzt ein Unterkapitel", "Now a subchapter") : Loc.L("Jetzt ein Kapitel", "Now a chapter");
    }

    void ToggleChapterHidden()
    {
        if (CurrentChapter() is not { } start) return;
        var st = _view.All.Concat(_view.Manual).FirstOrDefault(x => x.Anchor == start);
        bool hide = st?.ChapterHidden != true;
        Change(e => { var se = StepEdit(e, start); if (se["chapter"] is JsonObject c) { if (hide) c["hidden"] = true; else c.Remove("hidden"); } });
        _status.Text = hide ? Loc.L("Kapitel ausgeblendet – seine Schritte erscheinen in keiner Anleitung.", "Chapter hidden – its steps appear in no guide.") : Loc.L("Kapitel wieder eingeblendet", "Chapter shown again");
    }

    static (string id, string de, string en, string bodyDe, string bodyEn)[] Blocks => ManualBlocks.All;

    /// <summary>Textabschnitt (ohne Nummer und Bild) nach dem gewählten Schritt einfügen.</summary>
    void InsertSection(string kind)
    {
        if (_view.Session.ReadOnly) return;
        var b = Blocks.FirstOrDefault(x => x.id == kind);
        if (b.id == null) b = Blocks[0];
        var cur = Sel;
        long after = cur == null ? 0 : cur.Manual ? cur.After : cur.Anchor;
        long anchor = 0;
        string title = Loc.For(_view.Language, b.de, b.en), body = Loc.For(_view.Language, b.bodyDe, b.bodyEn);
        Change(e =>
        {
            var arr = e["inserted"] as JsonArray ?? new JsonArray();
            anchor = -(1000 + arr.OfType<JsonObject>().Select(x => -x.Long("anchor") - 1000).DefaultIfEmpty(0).Max() + 1);
            arr.Add(new JsonObject { ["anchor"] = anchor, ["after"] = after, ["type"] = "text", ["title"] = title, ["body"] = "", ["createdUtc"] = Json.UtcNow() });
            e["inserted"] = arr;
            // Text als eigener, formatierter Autorentext (Listen, fett)
            if (body.Length > 0)
            {
                var se = StepEdit(e, anchor);
                se["text"] = new JsonObject { [_view.Language] = new JsonObject { ["title"] = title, ["body"] = body, ["fmt"] = 1 } };
            }
            if (e["order"] is JsonArray order && order.Count > 0 && cur != null)
            {
                int i = order.Select((o, ix) => (o, ix)).FirstOrDefault(x => x.o is JsonValue v && v.TryGetValue<long>(out var l) && l == cur.Anchor).ix;
                order.Insert(Math.Min(order.Count, i + 1), anchor);
            }
        });
        SelectAnchor(anchor);
        _status.Text = Loc.L("Textabschnitt eingefügt – ohne Nummer und ohne Bild; Text rechts bearbeiten.", "Text section inserted – without number and image; edit the text on the right.");
    }

    MenuItem ChapterSubMenu(bool enabled)
    {
        var s = Sel;
        var st = CurrentChapter() is { } c ? _view.All.Concat(_view.Manual).FirstOrDefault(x => x.Anchor == c) : null;
        var sub = Sub(Loc.L("Kapitel", "Chapter"), Theme.IText,
            Item("chapter", s?.ChapterTitle != null ? Loc.L("Kapitel hier entfernen", "Remove chapter here") : null, enabled && s != null), Sep(),
            Item("chapterUp", enabled: enabled && st != null), Item("chapterDown", enabled: enabled && st != null),
            Item("chapterLevel", st?.ChapterLevel == 2 ? Loc.L("Zum Kapitel machen", "Make a chapter") : Loc.L("Zum Unterkapitel machen", "Make a subchapter"), enabled && st != null),
            Item("chapterHide", st?.ChapterHidden == true ? Loc.L("Kapitel einblenden", "Show chapter") : Loc.L("Kapitel ausblenden", "Hide chapter"), enabled && st != null));
        return sub;
    }

    MenuItem SectionSubMenu(bool enabled)
    {
        var sub = Sub(Loc.L("Textabschnitt einfügen", "Insert text section"), Theme.IText);
        foreach (var b in Blocks)
        {
            string k = b.id;
            sub.Items.Add(U.Mi(Loc.L(b.de, b.en), null, () => InsertSection(k)));
        }
        sub.IsEnabled = enabled;
        return sub;
    }
}
