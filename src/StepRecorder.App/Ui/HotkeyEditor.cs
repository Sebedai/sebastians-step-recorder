using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Tastenkürzel der Aufnahme einstellen: ein Erfassungsfeld je Aktion (Eingabe oder Klick startet, nächste Taste übernimmt,
/// Esc bricht ab, Rücktaste leert), Status je Zeile (frei / doppelt / belegt), „Standard wiederherstellen“.
/// </summary>
sealed class HotkeyEditor : StackPanel
{
    readonly Dictionary<string, string> _map;
    readonly Dictionary<string, (Button box, Border chip, TextBlock chipText)> _rows = new();
    readonly TextBlock _summary = U.Text("", 12, fg: Theme.Warn, margin: new Thickness(0, 8, 0, 0));
    string? _capturing;

    public IReadOnlyDictionary<string, string> Map => _map;
    public List<HotkeyIssue> Issues => HotkeyRules.Validate(_map);

    public HotkeyEditor(IReadOnlyDictionary<string, string> current)
    {
        _map = new Dictionary<string, string>(current);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        int r = 0;
        foreach (var a in HotkeyActions.All)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = U.Text(HotkeyActions.Label(a) + (a.Required ? " *" : ""), 13, fg: Theme.Fg2, margin: new Thickness(0, 6, 10, 6));
            label.VerticalAlignment = VerticalAlignment.Center;
            var box = new Button { HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 3, 8, 3), ToolTip = Loc.L("Klicken oder Eingabe drücken, dann die neue Kombination drücken · Esc: abbrechen · Rücktaste: kein Kürzel", "Click or press Enter, then press the new combination · Esc: cancel · Backspace: no shortcut") };
            string id = a.Id;
            box.Click += (_, _) => StartCapture(id);
            box.PreviewKeyDown += (_, e) => OnKey(id, e);
            box.LostKeyboardFocus += (_, _) => { if (_capturing == id) { _capturing = null; Refresh(); } };
            var chipText = new TextBlock { FontSize = 11.5 };
            var chip = new Border { CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 1, 7, 2), Child = chipText, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            Grid.SetRow(label, r); Grid.SetRow(box, r); Grid.SetColumn(box, 1); Grid.SetRow(chip, r); Grid.SetColumn(chip, 2);
            grid.Children.Add(label); grid.Children.Add(box); grid.Children.Add(chip);
            _rows[id] = (box, chip, chipText);
            r++;
        }
        Children.Add(grid);
        var reset = U.Btn(Theme.IUndo, Loc.L("Standard wiederherstellen", "Restore defaults"), () => { foreach (var a in HotkeyActions.All) _map[a.Id] = a.Default; Refresh(); }, null, "Ghost");
        reset.HorizontalAlignment = HorizontalAlignment.Left; reset.Margin = new Thickness(-4, 8, 0, 0);
        Children.Add(reset);
        Children.Add(_summary);
        Children.Add(U.Text(Loc.L("Erkannt werden nur Kürzel, die ein anderes Programm bei Windows angemeldet hat. Erscheint beim Drücken nichts, fängt ein anderes Programm die Kombination ab.",
            "Only shortcuts another program has registered with Windows are detected. If nothing appears when you press a combination, another program intercepts it."), 11.5, fg: Theme.Muted, margin: new Thickness(0, 6, 0, 0)));
        Refresh();
    }

    void StartCapture(string id)
    {
        _capturing = id;
        var (box, _, _) = _rows[id];
        box.Content = Loc.L("Kombination drücken …", "Press combination …");
        box.Focus();
    }

    void OnKey(string id, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_capturing != id)
        {
            if (key is Key.Enter or Key.Space) { StartCapture(id); e.Handled = true; }
            return;
        }
        e.Handled = true;
        if (key == Key.Escape) { _capturing = null; Refresh(); return; }
        if (key == Key.Back && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (!(HotkeyActions.ById(id)?.Required ?? false)) _map[id] = "";
            _capturing = null; Refresh(); return;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) return;
        var parts = new List<string>();
        var m = Keyboard.Modifiers;
        if (m.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (m.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (m.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (m.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        int vk = KeyInterop.VirtualKeyFromKey(key);
        string name = vk switch
        {
            >= 0x70 and <= 0x87 => "F" + (vk - 0x6F),
            >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)vk).ToString(),
            0x13 => "Pause", 0x91 => "Scroll", 0x2D => "Ins", 0x2E => "Del", 0x24 => "Home", 0x23 => "End", 0x21 => "PageUp", 0x22 => "PageDown",
            >= 0x60 and <= 0x69 => "NumPad" + (vk - 0x60),
            _ => ""
        };
        if (name.Length == 0) return;
        parts.Add(name);
        if (HotkeyCombo.TryParse(string.Join("+", parts), out var c)) _map[id] = c.ToString();
        _capturing = null;
        Refresh();
    }

    /// <summary>Anzeige und Status aller Zeilen aktualisieren.</summary>
    public void Refresh()
    {
        var issues = HotkeyRules.Validate(_map);
        var own = AppSettings.Load().EffectiveHotkeys();
        foreach (var a in HotkeyActions.All)
        {
            var (box, chip, text) = _rows[a.Id];
            string combo = _map.GetValueOrDefault(a.Id, "");
            if (_capturing != a.Id) box.Content = combo.Length == 0 ? Loc.L("– kein Kürzel –", "– none –") : Loc.Keys(combo);
            var mine = issues.FirstOrDefault(i => i.Id == a.Id);
            // „belegt“: nur prüfen, wenn sich die Kombination gegenüber der eigenen, gerade angemeldeten Belegung geändert hat
            bool taken = mine == null && combo.Length > 0 && !own.Values.Contains(combo) && !Hotkeys.Probe(combo);
            (string t, System.Windows.Media.Brush bg, System.Windows.Media.Brush fg) = mine != null
                ? (mine.Kind == "duplicate" ? Loc.L("doppelt", "duplicate") : mine.Kind == "required" ? Loc.L("fehlt", "missing") : Loc.L("ungültig", "invalid"), Theme.DangerSoft, Theme.Danger)
                : taken ? (Loc.L("belegt", "taken"), Theme.WarnSoft, Theme.Warn)
                : combo.Length == 0 ? (Loc.L("aus", "off"), Theme.Surface2, Theme.Muted)
                : (Loc.L("frei", "free"), Theme.SuccessSoft, Theme.Success);
            text.Text = t; text.Foreground = fg; chip.Background = bg;
            chip.ToolTip = mine?.Text ?? (taken ? Loc.L("Ein anderes Programm hat diese Kombination angemeldet.", "Another program has registered this combination.") : null);
        }
        _summary.Text = string.Join("\n", issues.Select(i => i.Text).Distinct());
        _summary.Visibility = issues.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
