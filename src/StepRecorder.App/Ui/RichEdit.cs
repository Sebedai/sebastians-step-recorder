using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using StepRecorder.Settings;
using StepRecorder.Text;

namespace StepRecorder.App.Ui;

/// <summary>
/// Eingabefeld für formatierten Text (13, Paket 21): sieht aus wie ein Textfeld, kann aber fett, kursiv, Eingabe, Tasten, Bedienelemente,
/// Links und Listen. Gespeichert wird die Auszeichnung (<see cref="RichDoc"/>) – ohne Formatierung bleibt es reiner Text (alte Ausgaben
/// unverändert). Einfügen von außen nur als reiner Text. Tasten: Strg+B, Strg+I, Strg+E (Eingabe), Strg+K (Link), Strg+Umschalt+L/N
/// (Listen), Esc verwirft, Strg+Eingabe schließt ab. Umschalter „&lt;/&gt;“ zeigt die Auszeichnung als Text.
/// </summary>
sealed partial class RichEdit : Border
{
    readonly RichTextBox _box = new() { AcceptsReturn = true, AcceptsTab = false, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsDocumentEnabled = false, AllowDrop = false, Padding = new Thickness(6, 6, 6, 6) };
    readonly TextBox _source = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalContentAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed, FontFamily = Theme.Mono, FontSize = Theme.FsHint };
    readonly WrapPanel _bar = new() { Margin = new Thickness(-2, 0, 0, 4) };
    readonly TextBlock _placeholder = new() { IsHitTestVisible = false, Margin = new Thickness(12, 8, 8, 0), TextWrapping = TextWrapping.Wrap };
    readonly RichFlowStyle _style;
    readonly int _maxLen;
    string _origText = "";
    bool _origRich, _sourceMode;
    /// <summary>Wird bei Verlassen des Feldes mit geändertem Inhalt aufgerufen: (Text, formatiert?).</summary>
    public event Action<string, bool>? Committed;
    /// <summary>Esc oder Strg+Eingabe: Fokus zurück an den Aufrufer.</summary>
    public event Action? Left;
    public RichTextBox Box => _box;
    /// <summary>Beim Verlassen selbst speichern (über <see cref="Committed"/>); aus, wenn der Aufrufer selbst abschließt.</summary>
    public bool AutoCommit { get; set; } = true;
    public bool IsReadOnly { get => _box.IsReadOnly; set { _box.IsReadOnly = value; _source.IsReadOnly = value; _bar.IsEnabled = !value; } }

    public RichEdit(string? placeholder = null, double minH = 64, int maxLen = 8000, string lang = "de", bool barAlways = true)
    {
        _maxLen = maxLen;
        _style = new RichFlowStyle(Theme.Mono, Freeze(Color.FromArgb(46, 128, 128, 128)), Freeze(Color.FromArgb(80, 120, 132, 150)), Theme.AccentText, Theme.AccentText);
        _box.MinHeight = minH; _source.MinHeight = minH;
        _box.FontSize = Theme.FsBody; _box.Foreground = Theme.Fg;
        _box.Language = System.Windows.Markup.XmlLanguage.GetLanguage(lang == "en" ? "en-US" : "de-DE");
        _source.Language = _box.Language;
        SpellCheck.SetIsEnabled(_box, true); SpellCheck.SetIsEnabled(_source, true);
        _box.ContextMenu = null; _source.ContextMenu = null;
        _placeholder.Text = placeholder ?? ""; _placeholder.Foreground = Theme.Muted; _placeholder.FontSize = Theme.FsBody;
        System.Windows.Automation.AutomationProperties.SetName(_box, placeholder ?? Loc.L("Text", "Text"));

        BarButton("B", Loc.L("Fett (Strg+B)", "Bold (Ctrl+B)"), () => EditingCommands.ToggleBold.Execute(null, _box), FontWeights.Bold);
        BarButton("I", Loc.L("Kursiv (Strg+I)", "Italic (Ctrl+I)"), () => EditingCommands.ToggleItalic.Execute(null, _box), FontWeights.Normal, FontStyles.Italic);
        BarButton("‹›", Loc.L("Eingabe, Code oder Dateiname (Strg+E)", "Input, code or file name (Ctrl+E)"), ToggleCode, mono: true);
        BarButton("⌨", Loc.L("Taste oder Tastenkombination, z. B. Strg+S", "Key or key combination, e.g. Ctrl+S"), ToggleKey);
        BarButton("▣", Loc.L("Name eines Bedienelements (Schaltfläche, Menü, Feld)", "Name of a control (button, menu, field)"), ToggleUi);
        BarButton("•", Loc.L("Aufzählung (Strg+Umschalt+L)", "Bulleted list (Ctrl+Shift+L)"), () => EditingCommands.ToggleBullets.Execute(null, _box));
        BarButton("1.", Loc.L("Nummerierung (Strg+Umschalt+N)", "Numbered list (Ctrl+Shift+N)"), () => EditingCommands.ToggleNumbering.Execute(null, _box));
        BarButton("🔗", Loc.L("Link (Strg+K) – nur http, https, mailto", "Link (Ctrl+K) – http, https, mailto only"), AddLink);
        var insert = BarButton("＋", Loc.L("Einfügen: Querverweis, Variable, Tabelle", "Insert: cross-reference, variable, table"), () => { });
        insert.Click += (_, _) => ShowInsertMenu(insert);
        BarButton("⌫", Loc.L("Formatierung der Auswahl entfernen", "Remove formatting from the selection"), ClearFormat);
        BarButton("</>", Loc.L("Als Auszeichnung bearbeiten (**fett**, *kursiv*, `Eingabe`, [[Strg+S]], [[ui:OK]], [Text](https://…))", "Edit as markup (**bold**, *italic*, `input`, [[Ctrl+S]], [[ui:OK]], [text](https://…))"), ToggleSource);
        if (!barAlways) { _bar.Visibility = Visibility.Collapsed; IsKeyboardFocusWithinChanged += (_, _) => _bar.Visibility = IsKeyboardFocusWithin || _sourceMode ? Visibility.Visible : Visibility.Collapsed; }

        var host = new Grid();
        host.Children.Add(_box); host.Children.Add(_source); host.Children.Add(_placeholder);
        Child = new DockPanel { Children = { Dock(_bar), host } };

        _box.TextChanged += (_, _) => UpdatePlaceholder();
        DataObject.AddPastingHandler(_box, OnPaste);
        CommandManager.AddPreviewExecutedHandler(_box, (_, e) => { if (Blocked(e.Command)) e.Handled = true; });
        CommandManager.AddPreviewCanExecuteHandler(_box, (_, e) => { if (Blocked(e.Command)) { e.CanExecute = false; e.Handled = true; } });
        _box.PreviewKeyDown += OnKeyDown;
        _source.PreviewKeyDown += OnKeyDown;
        _box.PreviewTextInput += (_, e) => { if (Length() + e.Text.Length > _maxLen) { e.Handled = true; System.Media.SystemSounds.Beep.Play(); } };
        LostKeyboardFocus += (_, e) => { if (AutoCommit && !IsKeyboardFocusWithin) Commit(); };
    }

    static UIElement Dock(UIElement e) { DockPanel.SetDock(e, System.Windows.Controls.Dock.Top); return e; }
    static Brush Freeze(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    Button BarButton(string label, string tip, Action click, FontWeight? weight = null, FontStyle? style = null, bool mono = false)
    {
        var tb = new TextBlock { Text = label, FontSize = 13, FontWeight = weight ?? FontWeights.SemiBold, FontStyle = style ?? FontStyles.Normal, HorizontalAlignment = HorizontalAlignment.Center };
        if (mono) tb.FontFamily = Theme.Mono;
        var b = new Button { Content = tb, ToolTip = tip, Focusable = false, MinWidth = 30, MinHeight = 28, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(2, 0, 2, 0), Style = Theme.Style("Ghost") };
        System.Windows.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (_, _) => { if (!_sourceMode || label == "</>") click(); _box.Focus(); };
        _bar.Children.Add(b);
        return b;
    }

    static bool Blocked(ICommand c) => c == EditingCommands.ToggleUnderline || c == EditingCommands.ToggleSubscript || c == EditingCommands.ToggleSuperscript
        || c == EditingCommands.IncreaseFontSize || c == EditingCommands.DecreaseFontSize || c == EditingCommands.AlignCenter || c == EditingCommands.AlignJustify
        || c == EditingCommands.AlignLeft || c == EditingCommands.AlignRight || c == EditingCommands.IncreaseIndentation || c == EditingCommands.DecreaseIndentation
        || c == EditingCommands.TabForward || c == EditingCommands.TabBackward
        || (c is RoutedCommand rc && rc.Name.StartsWith("Apply", StringComparison.Ordinal) && rc.OwnerType == typeof(EditingCommands));

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;
        if (e.Key == Key.Escape) { Load(_origText, _origRich); Left?.Invoke(); e.Handled = true; return; }
        if (e.Key == Key.Enter && mods == ModifierKeys.Control) { Left?.Invoke(); e.Handled = true; return; }
        if (_sourceMode) return;
        if (mods == ModifierKeys.Control && e.Key == Key.E) { ToggleCode(); e.Handled = true; }
        else if (mods == ModifierKeys.Control && e.Key == Key.K) { AddLink(); e.Handled = true; }
        else if (mods == ModifierKeys.Control && e.Key == Key.U) e.Handled = true;    // kein Unterstreichen
        else if (mods == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.L) { EditingCommands.ToggleBullets.Execute(null, _box); e.Handled = true; }
        else if (mods == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.N) { EditingCommands.ToggleNumbering.Execute(null, _box); e.Handled = true; }
        else if (e.Key == Key.Enter && mods == ModifierKeys.Shift) { EditingCommands.EnterParagraphBreak.Execute(null, _box); e.Handled = true; }
    }

    void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        // nur reiner Text – fremde Formatierung, Bilder und Tabellen kommen nicht ins Feld
        string? text = e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText) ? e.SourceDataObject.GetData(DataFormats.UnicodeText) as string : null;
        e.CancelCommand();
        if (string.IsNullOrEmpty(text) || _box.IsReadOnly) return;
        text = text.Replace("\r\n", "\n");
        if (Length() + text.Length > _maxLen) text = text[..Math.Max(0, _maxLen - Length())];
        _box.Selection.Text = text;
        _box.Selection.Select(_box.Selection.End, _box.Selection.End);
    }

    int Length() => new TextRange(_box.Document.ContentStart, _box.Document.ContentEnd).Text.Length;

    void ToggleCode()
    {
        var sel = _box.Selection;
        bool on = sel.GetPropertyValue(TextElement.FontFamilyProperty) is FontFamily f && f.Source == _style.Mono.Source;
        if (on) { sel.ApplyPropertyValue(TextElement.FontFamilyProperty, _box.FontFamily); sel.ApplyPropertyValue(TextElement.BackgroundProperty, null); }
        else { sel.ApplyPropertyValue(TextElement.FontFamilyProperty, _style.Mono); sel.ApplyPropertyValue(TextElement.BackgroundProperty, _style.CodeBack); }
    }

    void ToggleKey()
    {
        var sel = _box.Selection;
        if (sel.IsEmpty)
        {
            var d = new InputDialog(Loc.L("Taste oder Tastenkombination (z. B. Strg+Umschalt+S)", "Key or key combination (e.g. Ctrl+Shift+S)"), "") { Owner = Window.GetWindow(this) };
            if (d.ShowDialog() != true || d.Value.Trim().Length == 0) return;
            var run = new Run(d.Value.Trim().Replace("\r", "").Replace("\n", ""), sel.Start) { Background = _style.KeyBack };
            _box.CaretPosition = run.ElementEnd;
            return;
        }
        bool on = sel.GetPropertyValue(TextElement.BackgroundProperty) is SolidColorBrush b && b.Color == ((SolidColorBrush)_style.KeyBack).Color;
        sel.ApplyPropertyValue(TextElement.BackgroundProperty, on ? null : _style.KeyBack);
    }

    void ToggleUi()
    {
        var sel = _box.Selection;
        bool on = sel.GetPropertyValue(TextElement.ForegroundProperty) is SolidColorBrush b && b.Color == ((SolidColorBrush)_style.UiFore).Color;
        sel.ApplyPropertyValue(TextElement.ForegroundProperty, on ? _box.Foreground : _style.UiFore);
        sel.ApplyPropertyValue(TextElement.FontWeightProperty, on ? FontWeights.Normal : FontWeights.SemiBold);
    }

    void AddLink()
    {
        var sel = _box.Selection;
        var d = new InputDialog(Loc.L("Adresse des Links (https://… oder mailto:…)", "Link address (https://… or mailto:…)"), "https://") { Owner = Window.GetWindow(this) };
        if (d.ShowDialog() != true) return;
        string url = d.Value.Trim();
        if (!RichDoc.IsAllowedLink(url, out var ok)) { Msg.Show(Loc.L("Erlaubt sind nur vollständige Adressen mit http, https oder mailto.", "Only complete addresses with http, https or mailto are allowed.")); return; }
        if (sel.IsEmpty)
        {
            var link = new Hyperlink(new Run(ok), sel.Start) { Tag = ok, Foreground = _style.LinkFore, ToolTip = ok };
            _box.CaretPosition = link.ElementEnd.GetNextInsertionPosition(LogicalDirection.Forward) ?? link.ElementEnd;
            return;
        }
        try { _ = new Hyperlink(sel.Start, sel.End) { Tag = ok, Foreground = _style.LinkFore, ToolTip = ok }; }
        catch (InvalidOperationException) { Msg.Show(Loc.L("Ein Link lässt sich nur über Text in einer Zeile legen.", "A link can only span text within one line.")); }
    }

    void ClearFormat()
    {
        var sel = _box.Selection;
        sel.ClearAllProperties();
        sel.ApplyPropertyValue(TextElement.FontFamilyProperty, _box.FontFamily);
    }

    void ToggleSource()
    {
        if (_sourceMode)
        {
            var doc = RichDoc.Parse(_source.Text);
            _box.Document = RichFlow.ToFlow(doc, _style);
            ApplyDocDefaults();
            _sourceMode = false;
            _source.Visibility = Visibility.Collapsed; _box.Visibility = Visibility.Visible;
            _box.Focus();
        }
        else
        {
            _source.Text = Current().ToMarkup();
            _sourceMode = true;
            _source.Visibility = Visibility.Visible; _box.Visibility = Visibility.Collapsed;
            _source.Focus();
        }
        UpdatePlaceholder();
    }

    void ApplyDocDefaults()
    {
        _box.Document.FontFamily = _box.FontFamily; _box.Document.FontSize = _box.FontSize; _box.Document.Foreground = _box.Foreground;
        _box.Document.PagePadding = new Thickness(0);
        _box.Document.TextAlignment = TextAlignment.Left;
        TextBlock.SetLineHeight(_box.Document, 21);
    }

    void UpdatePlaceholder() => _placeholder.Visibility = !_sourceMode && Length() <= 2 && new TextRange(_box.Document.ContentStart, _box.Document.ContentEnd).Text.Trim().Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Inhalt laden: reiner Text (wird nicht gedeutet) oder Auszeichnung.</summary>
    public void Load(string text, bool rich)
    {
        _origText = text ?? ""; _origRich = rich;
        var doc = RichDoc.Parse(rich ? _origText : RichDoc.Escape(_origText));
        _box.Document = RichFlow.ToFlow(doc, _style);
        ApplyDocDefaults();
        if (_sourceMode) _source.Text = doc.ToMarkup();
        _box.CaretPosition = _box.Document.ContentEnd;
        UpdatePlaceholder();
    }

    RichDoc Current() => _sourceMode ? RichDoc.Parse(_source.Text) : RichFlow.FromFlow(_box.Document, _style);

    /// <summary>Aktueller Inhalt: (Text, formatiert?) – ohne Formatierung als reiner Text.</summary>
    public (string text, bool rich) Value()
    {
        var doc = Current();
        if (doc.IsSimple) return (doc.Projection(), false);
        return (doc.ToMarkup(), true);
    }

    /// <summary>Hat sich der Inhalt gegenüber dem geladenen geändert (in kanonischer Form)?</summary>
    public bool Changed(out string text, out bool rich)
    {
        (text, rich) = Value();
        var orig = RichDoc.Parse(_origRich ? _origText : RichDoc.Escape(_origText));
        string origCanon = orig.IsSimple ? orig.Projection() : orig.ToMarkup();
        return text.TrimEnd() != origCanon.TrimEnd() || (rich != (!orig.IsSimple));
    }

    public void Commit()
    {
        if (_box.IsReadOnly) return;
        if (!Changed(out var text, out var rich)) return;
        _origText = text; _origRich = rich;
        Committed?.Invoke(text, rich);
    }

    /// <summary>Gespeicherten Stand übernehmen (danach gilt er als unverändert).</summary>
    public void MarkSaved(string text, bool rich) { _origText = text; _origRich = rich; }

    /// <summary>Auf den geladenen Stand zurücksetzen.</summary>
    public void Revert() => Load(_origText, _origRich);

    /// <summary>Text an der Schreibmarke einfügen (Textbaustein) – als reiner Text.</summary>
    public void InsertText(string text)
    {
        if (_box.IsReadOnly) return;
        if (_sourceMode) { int i = _source.CaretIndex; _source.Text = _source.Text.Insert(i, text); _source.CaretIndex = i + text.Length; return; }
        _box.Focus();
        _box.Selection.Text = text.Replace("\r\n", "\n");
        _box.Selection.Select(_box.Selection.End, _box.Selection.End);
    }

    public void FocusEnd() { if (_sourceMode) { _source.Focus(); _source.CaretIndex = _source.Text.Length; } else { _box.Focus(); _box.CaretPosition = _box.Document.ContentEnd; } }
}
