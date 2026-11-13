using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Textbausteine (08 §12): wiederkehrende Hinweise und Schritttexte. Gespeichert in den Einstellungen (Name und Text),
/// eingefügt über ein Menü an der Schreibmarke, verwaltet in einem kleinen Dialog; im Einstellungs-Austausch der Teil
/// <c>textSnippets</c>.
/// </summary>
static class SnippetUi
{
    /// <summary>Strg+Umschalt+B – im Editor und in den Eingabedialogen.</summary>
    public const Key ShortcutKey = Key.B;
    public const ModifierKeys ShortcutMods = ModifierKeys.Control | ModifierKeys.Shift;

    /// <summary>
    /// Menü mit allen Bausteinen am Element <paramref name="placement"/>. Das Zielfeld bestimmt <paramref name="target"/> erst beim
    /// Klick (es kann inzwischen neu aufgebaut sein); die Schreibmarke von jetzt gilt, solange sich der Text nicht geändert hat.
    /// </summary>
    public static void ShowMenu(Window owner, FrameworkElement placement, Func<TextBox?> target, Action? after)
    {
        var first = target();
        int selStart = first?.SelectionStart ?? 0, selLen = first?.SelectionLength ?? 0;
        string before = first?.Text ?? "";
        var m = U.MenuScope(new ContextMenu { PlacementTarget = placement, Placement = PlacementMode.Bottom });
        var snippets = AppSettings.Load().TextSnippets;
        foreach (var sn in snippets)
        {
            var text = sn.Text;
            var mi = U.Mi(sn.Name, null, () => Insert(target(), first, before, selStart, selLen, text, after), Theme.ISnippet);
            mi.ToolTip = text.Length > 400 ? text[..400] + "…" : text;
            m.Items.Add(mi);
        }
        if (snippets.Count == 0)
            m.Items.Add(new MenuItem { Header = Loc.L("Noch keine Textbausteine", "No text snippets yet"), IsEnabled = false });
        m.Items.Add(new Separator());
        m.Items.Add(U.Mi(Loc.L("Textbausteine verwalten …", "Manage text snippets …"), null, () => Manage(owner), Theme.ISettings));
        m.IsOpen = true;
    }

    static void Insert(TextBox? box, TextBox? first, string before, int selStart, int selLen, string snippet, Action? after)
    {
        if (box == null || box.IsReadOnly || !box.IsEnabled) return;
        int start, len;
        if (ReferenceEquals(box, first)) { start = box.SelectionStart; len = box.SelectionLength; }
        else if (box.Text == before) { start = selStart; len = selLen; }
        else { start = box.Text.Length; len = 0; }
        var (text, caret) = TextSnippet.Insert(box.Text, start, len, snippet);
        if (box.MaxLength > 0 && text.Length > box.MaxLength) { Msg.Show(Loc.L("Der Textbaustein passt nicht mehr in dieses Feld.", "The text snippet no longer fits into this field.")); return; }
        box.Text = text;
        box.Focus();
        box.CaretIndex = caret;
        after?.Invoke();
    }

    /// <summary>Knopf „Textbaustein“ für Eingabedialoge: fügt in das zuletzt bearbeitete Feld ein.</summary>
    public static Button InsertButton(Window owner, Func<TextBox?> target)
    {
        Button? b = null;
        b = U.Btn(Theme.ISnippet, Loc.L("Textbaustein", "Snippet"), () => ShowMenu(owner, b!, target, null),
            Loc.F("Textbaustein an der Schreibmarke einfügen ({0})", "Insert a text snippet at the cursor ({0})", ViewerCommands.Gesture(ShortcutKey, ShortcutMods)), "Link");
        b.Padding = new Thickness(6, 2, 6, 2);
        return b;
    }

    // =====================================================================================
    // Verwalten
    // =====================================================================================

    /// <summary>Bausteine anlegen, ändern, löschen. „Speichern“ schreibt sofort in die Einstellungen.</summary>
    public static bool Manage(Window? owner)
    {
        var w = new Window { Title = Loc.L("Textbausteine", "Text snippets"), Width = 760, Height = 540, MinWidth = 560, MinHeight = 400, ShowInTaskbar = false, WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen };
        if (owner != null && owner.IsVisible) w.Owner = owner;
        Theme.Prepare(w, Theme.Surface);
        var items = AppSettings.Load().TextSnippets.Select(s => new TextSnippet { Name = s.Name, Text = s.Text }).ToList();
        var list = new ListBox { MinWidth = 220, Margin = new Thickness(0, 0, 16, 0) }.Named(Loc.L("Textbausteine", "Text snippets"));
        var name = new TextBox { MaxLength = TextSnippet.MaxName };
        var text = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 160, MaxLength = TextSnippet.MaxText, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top };
        bool loading = false;
        TextSnippet? Current() => list.SelectedIndex >= 0 && list.SelectedIndex < items.Count ? items[list.SelectedIndex] : null;
        void Refill(int select)
        {
            loading = true;
            list.Items.Clear();
            foreach (var it in items) list.Items.Add(new ListBoxItem { Content = it.Name.Length > 0 ? it.Name : Loc.L("(ohne Namen)", "(unnamed)") });
            list.SelectedIndex = Math.Min(select, items.Count - 1);
            loading = false;
            Show();
        }
        void Show()
        {
            loading = true;
            var c = Current();
            name.Text = c?.Name ?? ""; text.Text = c?.Text ?? "";
            name.IsEnabled = text.IsEnabled = c != null;
            loading = false;
        }
        list.SelectionChanged += (_, _) => { if (!loading) Show(); };
        name.TextChanged += (_, _) =>
        {
            if (loading || Current() is not { } c) return;
            c.Name = name.Text;
            if (list.SelectedItem is ListBoxItem li) li.Content = c.Name.Trim().Length > 0 ? c.Name : Loc.L("(ohne Namen)", "(unnamed)");
        };
        text.TextChanged += (_, _) => { if (!loading && Current() is { } c) c.Text = text.Text; };
        var add = U.Btn(Theme.IAdd, Loc.L("Neu", "New"), () =>
        {
            if (items.Count >= TextSnippet.MaxCount) return;
            items.Add(new TextSnippet { Name = Loc.F("Baustein {0}", "Snippet {0}", items.Count + 1), Text = "" });
            Refill(items.Count - 1);
            name.Focus(); name.SelectAll();
        }, Loc.L("Neuen Textbaustein anlegen", "Create a new text snippet"), "Ghost");
        var del = U.Btn(Theme.IDelete, Loc.L("Löschen", "Delete"), () =>
        {
            int i = list.SelectedIndex;
            if (i < 0 || i >= items.Count) return;
            items.RemoveAt(i);
            Refill(Math.Max(0, i - 1));
        }, Loc.L("Gewählten Textbaustein löschen", "Delete the selected text snippet"), "Ghost");
        var left = new DockPanel();
        var leftButtons = U.Row(add, del); leftButtons.Margin = new Thickness(-3, 8, 0, 0);
        DockPanel.SetDock(leftButtons, Dock.Bottom); left.Children.Add(leftButtons); left.Children.Add(list);
        var right = U.Col(U.Field(Loc.L("Name", "Name"), name, Loc.L("Erscheint im Menü „Textbaustein“.", "Shown in the “Snippet” menu.")),
                          U.Field(Loc.L("Text", "Text"), text, Loc.F("Wird an der Schreibmarke eingefügt – im Editor auch mit {0}.", "Inserted at the cursor – in the editor also with {0}.", ViewerCommands.Gesture(ShortcutKey, ShortcutMods)), new Thickness(0)));
        var grid = new Grid { Height = 360 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(right, 1);
        grid.Children.Add(left); grid.Children.Add(right);
        var body = U.Col(U.Hint(Loc.L("Für wiederkehrende Hinweise und Schritttexte. Textbausteine stehen in den Einstellungen und lassen sich mit ihnen exportieren.", "For recurring notes and step texts. Text snippets are stored in the settings and can be exported with them."), new Thickness(0, 0, 0, 12)), grid);
        bool saved = false;
        U.Dialog(w, Loc.L("Textbausteine", "Text snippets"), body, U.Primary(Loc.L("Speichern", "Save"), () =>
        {
            int bad = items.FindIndex(i => !TextSnippet.Valid(i.Name, i.Text));
            if (bad >= 0)
            {
                list.SelectedIndex = bad;
                Msg.Show(Loc.F("Textbaustein {0} unvollständig\n\nJeder Baustein braucht einen Namen (höchstens {1} Zeichen, einzeilig) und einen Text.", "Text snippet {0} incomplete\n\nEvery snippet needs a name (at most {1} characters, one line) and a text.", bad + 1, TextSnippet.MaxName), w.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var s = AppSettings.Load();
            s.TextSnippets = TextSnippet.Clean(items);
            if (!s.Save()) { Msg.Show(Loc.L("Nicht gespeichert\n\n", "Not saved\n\n") + s.LastSaveError, w.Title, MessageBoxButton.OK, MessageBoxImage.Error); return; }
            saved = true;
            w.DialogResult = true;
        }), U.Btn(Loc.L("Abbrechen", "Cancel"), () => w.DialogResult = false));
        w.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { w.DialogResult = false; e.Handled = true; } };
        Refill(0);
        w.ShowDialog();
        return saved;
    }
}
