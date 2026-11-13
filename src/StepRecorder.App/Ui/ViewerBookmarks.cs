using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Lesezeichen (08 §12): Schritte mit einem Stern merken (<c>edits.steps[].bookmark</c>) und die Liste danach filtern.
/// Arbeitsnotiz des Autors – erscheint in keiner Ausgabe. Kürzel Strg+Umschalt+L. Dazu Textbausteine im Editor (Strg+Umschalt+B).
/// </summary>
sealed partial class ViewerWindow
{
    /// <summary>Filter „nur Lesezeichen“ neben „Langsam“.</summary>
    ToggleButton _bookmarkToggle = null!;

    ToggleButton BuildBookmarkFilter()
    {
        var t = U.IconToggle(Theme.IStarFill, null, Loc.F("Nur Schritte mit Lesezeichen – setzen mit {0}", "Only bookmarked steps – set with {0}", Cmd("bookmark").Gesture), 14);
        t.Margin = new Thickness(8, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Stretch; t.Padding = new Thickness(10, 4, 10, 4);
        t.Named(Loc.L("Nur Lesezeichen", "Bookmarks only"));
        t.Checked += (_, _) => { if (IsLoaded) Fill(); }; t.Unchecked += (_, _) => { if (IsLoaded) Fill(); };
        return t;
    }

    /// <summary>Lesezeichen am gewählten Schritt setzen oder entfernen (bei Mehrfachauswahl: alle wie der erste umgekehrt).</summary>
    void ToggleBookmark()
    {
        var steps = SelSteps; if (steps.Count == 0) return;
        bool on = !steps[0].Bookmarked;
        if (steps.Count > 1) { BulkEdit(steps, se => SetBookmark(se, on)); return; }
        Change(e => SetBookmark(StepEdit(e, steps[0].Anchor), on));
        _status.Text = on ? Loc.F("Lesezeichen gesetzt – Filter mit dem Stern über der Liste, entfernen mit {0}", "Bookmark set – filter with the star above the list, remove with {0}", Cmd("bookmark").Gesture) : Loc.L("Lesezeichen entfernt", "Bookmark removed");
    }

    static void SetBookmark(JsonObject se, bool on) { if (on) se["bookmark"] = true; else se.Remove("bookmark"); }

    /// <summary>Stern im Detailbereich: zeigt und schaltet das Lesezeichen des Schritts.</summary>
    FrameworkElement BookmarkButton(ViewStep s)
    {
        string label = s.Bookmarked ? Loc.L("Lesezeichen entfernen", "Remove bookmark") : Loc.L("Lesezeichen setzen", "Set bookmark");
        var b = U.Ghost(s.Bookmarked ? Theme.IStarFill : Theme.IStar, null, ToggleBookmark, $"{label} ({Cmd("bookmark").Gesture})");
        b.Named(label, Loc.L("Nur für Sie: erscheint in keiner Ausgabe", "Just for you: appears in no output"));
        if (s.Bookmarked) b.Foreground = Theme.Warn;
        b.Margin = new Thickness(0, -4, -6, 0); b.Padding = new Thickness(8, 4, 8, 4); b.VerticalAlignment = VerticalAlignment.Top;
        b.IsEnabled = !_view.Session.ReadOnly;
        return b;
    }

    /// <summary>Gilt der Lesezeichen-Filter für diesen Schritt (true = anzeigen)?</summary>
    bool PassesBookmarkFilter(ViewStep s) => _bookmarkToggle?.IsChecked != true || s.Bookmarked;

    // ---------------- Textbausteine ----------------

    /// <summary>
    /// Strg+Umschalt+B: Textbaustein an der Schreibmarke einfügen – in Überschrift oder Text des Schritts oder in das gerade
    /// bearbeitete Feld. Ohne Fokus in einem Feld landet er im Anleitungstext.
    /// </summary>
    void InsertSnippet()
    {
        if (_editBody != null && _editBody.IsKeyboardFocusWithin && !_editBody.IsReadOnly) { SnippetForInline(false, _editBody); return; }
        if (Keyboard.FocusedElement is TextBox tb && !tb.IsReadOnly && tb != _search)
        {
            if (tb == _editTitle) SnippetForInline(true, tb);
            else SnippetUi.ShowMenu(this, tb, () => tb, null);
            return;
        }
        if (Sel == null || _view.Session.ReadOnly) return;
        if (_detailTab != 0) _tabBtns[0].IsChecked = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { if (_editBody is { } body) { body.FocusEnd(); SnippetForInline(false, body); } });
    }

    /// <summary>
    /// Menü für die Direktbearbeitung. Das Menü nimmt den Fokus, die Direktbearbeitung speichert dabei und baut den
    /// Detailbereich neu – darum wird das Feld erst beim Klick bestimmt.
    /// </summary>
    void SnippetForInline(bool title, FrameworkElement placement)
    {
        if (title) SnippetUi.ShowMenu(this, placement, () => _editTitle, () => InlineCommit());
        else SnippetUi.ShowMenu(this, placement, text => { _editBody?.InsertText(text); InlineCommit(); });
    }

    /// <summary>Knopf „Textbaustein“ unter dem Anleitungstext.</summary>
    Button SnippetButton()
    {
        Button? b = null;
        b = U.Btn(Theme.ISnippet, Loc.L("Textbaustein", "Snippet"), () =>
        {
            bool title = _lastInlineTitle;
            SnippetForInline(title, b!);
        }, Loc.F("Textbaustein an der Schreibmarke einfügen ({0}) – verwalten in den Einstellungen", "Insert a text snippet at the cursor ({0}) – manage them in the settings", Cmd("snippet").Gesture), "Link");
        b.Padding = new Thickness(6, 2, 6, 2); b.Margin = new Thickness(-6, -2, 0, 0); b.VerticalAlignment = VerticalAlignment.Top;
        return b;
    }

    /// <summary>Zuletzt bearbeitetes Feld der Direktbearbeitung (Überschrift oder Text) – Ziel des Knopfs „Textbaustein“.</summary>
    bool _lastInlineTitle;
}
