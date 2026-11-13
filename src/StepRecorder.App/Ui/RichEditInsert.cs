using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using StepRecorder.Data;
using StepRecorder.Settings;
using StepRecorder.Text;

namespace StepRecorder.App.Ui;

/// <summary>
/// „Einfügen“ im formatierten Textfeld (13, Paket 23): Querverweis auf einen Schritt oder ein Kapitel, Variable, Tabelle.
/// Die Quellen stellt das gerade aktive Editorfenster bereit.
/// </summary>
sealed partial class RichEdit
{
    /// <summary>Verweisziele des aktiven Editors: (Ziel „s…“/„k…“, Beschriftung in der Liste).</summary>
    public static Func<IReadOnlyList<(string target, string label)>>? RefSource { get; set; }
    /// <summary>Variablen des aktiven Editors: (Name, Wert).</summary>
    public static Func<IReadOnlyList<(string name, string value)>>? VarSource { get; set; }
    /// <summary>Öffnet die Variablen des Dokuments zum Bearbeiten.</summary>
    public static Action? EditVars { get; set; }

    void ShowInsertMenu(FrameworkElement owner)
    {
        if (_box.IsReadOnly) return;
        var caret = _box.CaretPosition;
        var m = U.MenuScope(new ContextMenu { PlacementTarget = owner, Placement = PlacementMode.Bottom });
        var refs = new MenuItem { Header = Loc.L("Querverweis", "Cross-reference") };
        var targets = RefSource?.Invoke() ?? Array.Empty<(string, string)>();
        foreach (var (target, label) in targets.Take(400))
        {
            string t = target;
            var mi = new MenuItem { Header = label };
            mi.Click += (_, _) => { _box.Focus(); _box.CaretPosition = caret; InsertRef(t); };
            refs.Items.Add(mi);
        }
        if (refs.Items.Count == 0) refs.Items.Add(new MenuItem { Header = Loc.L("Keine Ziele", "No targets"), IsEnabled = false });
        m.Items.Add(refs);
        var vars = new MenuItem { Header = Loc.L("Variable", "Variable") };
        foreach (var (name, value) in VarSource?.Invoke() ?? Array.Empty<(string, string)>())
        {
            string n = name;
            var mi = new MenuItem { Header = "{{" + name + "}}", InputGestureText = value.Length > 30 ? value[..30] + "…" : value };
            mi.Click += (_, _) => { _box.Focus(); _box.CaretPosition = caret; InsertText("{{" + n + "}}"); };
            vars.Items.Add(mi);
        }
        if (EditVars != null) { if (vars.Items.Count > 0) vars.Items.Add(new Separator()); vars.Items.Add(U.Mi(Loc.L("Variablen bearbeiten …", "Edit variables …"), null, EditVars)); }
        m.Items.Add(vars);
        m.Items.Add(U.Mi(Loc.L("Tabelle …", "Table …"), null, () => { _box.Focus(); _box.CaretPosition = caret; InsertTable(); }));
        m.IsOpen = true;
    }

    /// <summary>Querverweis als eigener Lauf (Ziel im <c>Tag</c>); die Beschriftung setzt erst die Anleitung mit der Nummer.</summary>
    public void InsertRef(string target)
    {
        if (_box.IsReadOnly || !RichDoc.IsRefTarget(target)) return;
        if (_sourceMode) { _source.SelectedText = "[[ref:" + target + "]]"; return; }
        if (!_box.Selection.IsEmpty) _box.Selection.Text = "";
        var pos = _box.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
        var probe = RichFlow.ToFlow(RichDoc.Parse("[[ref:" + target + "]]"), _style);
        string label = (((Paragraph)probe.Blocks.FirstBlock).Inlines.FirstInline as Run)?.Text ?? target;
        var run = new Run(label, pos) { Tag = "ref:" + target, Foreground = _style.LinkFore, TextDecorations = TextDecorations.Underline };
        var after = new Run(" ", run.ElementEnd);
        _box.CaretPosition = after.ContentEnd;
    }

    /// <summary>Tabelle nach dem aktuellen Absatz einfügen (Zellen lassen sich danach direkt im Feld bearbeiten).</summary>
    void InsertTable()
    {
        var d = new TableDialog { Owner = Window.GetWindow(this) };
        if (d.ShowDialog() != true) return;
        string markup = RichDoc.TableMarkup(d.Cells, d.Header);
        if (_sourceMode) { _source.SelectedText = "\n\n" + markup + "\n\n"; return; }
        var flow = RichFlow.ToFlow(RichDoc.Parse(markup), _style);
        var table = flow.Blocks.OfType<Table>().FirstOrDefault();
        if (table == null) return;
        flow.Blocks.Remove(table);
        Block? anchor = _box.CaretPosition.Paragraph;
        // oberste Ebene suchen (aus Liste oder Tabellenzelle heraus)
        while (anchor != null && anchor.Parent is not FlowDocument)
        {
            if (anchor.Parent is ListItem li) anchor = li.Parent as Block;
            else if (anchor.Parent is TableCell { Parent: TableRow { Parent: TableRowGroup { Parent: Table t } } }) anchor = t;
            else if (anchor.Parent is Block b) anchor = b;
            else anchor = null;
        }
        if (anchor != null) _box.Document.Blocks.InsertAfter(anchor, table); else _box.Document.Blocks.Add(table);
        var next = new Paragraph { Margin = new Thickness(0) };
        _box.Document.Blocks.InsertAfter(table, next);
        _box.CaretPosition = next.ContentStart;
    }
}

/// <summary>Tabelle anlegen: Zeilen und Spalten, Zellen als Text, erste Zeile wahlweise Kopfzeile (13, Paket 23).</summary>
sealed class TableDialog : DialogWindow
{
    readonly Grid _grid = new();
    readonly CheckBox _header = new() { Content = Loc.L("Erste Zeile ist die Kopfzeile", "First row is the header"), IsChecked = true, Margin = new Thickness(0, 12, 0, 0) };
    List<List<string>> _cells = new() { new() { Loc.L("Spalte 1", "Column 1"), Loc.L("Spalte 2", "Column 2") }, new() { "", "" }, new() { "", "" } };
    readonly List<List<TextBox>> _boxes = new();

    public IReadOnlyList<IReadOnlyList<string>> Cells => _cells;
    public bool Header => _header.IsChecked == true;

    public TableDialog()
    {
        Title = AppInfo.DisplayName; Width = 680; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Surface);
        var tools = U.Row(
            U.Btn(Loc.L("+ Zeile", "+ Row"), () => Change(+1, 0)), U.Btn(Loc.L("− Zeile", "− Row"), () => Change(-1, 0)),
            U.Btn(Loc.L("+ Spalte", "+ Column"), () => Change(0, +1)), U.Btn(Loc.L("− Spalte", "− Column"), () => Change(0, -1)));
        tools.Margin = new Thickness(0, 0, 0, 12);
        var scroll = new ScrollViewer { Content = _grid, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        var p = new StackPanel { Children = { tools, scroll, _header, U.Text(Loc.L("Zellen sind einfacher Text; Formatierung danach direkt im Feld. Höchstens 12 Spalten.", "Cells are plain text; format them afterwards right in the field. At most 12 columns."), Theme.FsHint, fg: Theme.Muted, margin: new Thickness(0, 8, 0, 0)) } };
        Rebuild();
        U.Dialog(this, Loc.L("Tabelle einfügen", "Insert table"), p, U.Primary(Loc.L("Einfügen", "Insert"), () => { Read(); DialogResult = true; }), U.Btn(Loc.L("Abbrechen", "Cancel"), () => DialogResult = false));
    }

    void Read() { for (int r = 0; r < _boxes.Count; r++) for (int c = 0; c < _boxes[r].Count; c++) _cells[r][c] = _boxes[r][c].Text; }

    void Change(int dRows, int dCols)
    {
        Read();
        int cols = _cells[0].Count;
        if (dRows > 0) _cells.Add(Enumerable.Repeat("", cols).ToList());
        if (dRows < 0 && _cells.Count > 1) _cells.RemoveAt(_cells.Count - 1);
        if (dCols > 0 && cols < RichDoc.MaxColumns) foreach (var r in _cells) r.Add("");
        if (dCols < 0 && cols > 1) foreach (var r in _cells) r.RemoveAt(r.Count - 1);
        Rebuild();
    }

    void Rebuild()
    {
        _grid.Children.Clear(); _grid.RowDefinitions.Clear(); _grid.ColumnDefinitions.Clear(); _boxes.Clear();
        int cols = _cells[0].Count;
        for (int c = 0; c < cols; c++) _grid.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = 90 });
        for (int r = 0; r < _cells.Count; r++)
        {
            _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var row = new List<TextBox>();
            for (int c = 0; c < cols; c++)
            {
                var tb = new TextBox { Text = _cells[r][c], Margin = new Thickness(2), FontWeight = r == 0 && Header ? FontWeights.SemiBold : FontWeights.Normal };
                System.Windows.Automation.AutomationProperties.SetName(tb, Loc.F("Zeile {0}, Spalte {1}", "Row {0}, column {1}", r + 1, c + 1));
                Grid.SetRow(tb, r); Grid.SetColumn(tb, c);
                _grid.Children.Add(tb); row.Add(tb);
            }
            _boxes.Add(row);
        }
    }
}
