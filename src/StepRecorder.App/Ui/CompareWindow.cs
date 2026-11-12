using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StepRecorder.Compare;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Zwei Aufnahmen vergleichen (08 §5.9, 12 Paket 19): oben die Paarliste aus <see cref="StepCompare"/>, unten zum gewählten
/// Paar beide Bilder (gemeinsamer Zoom und Bildausschnitt, Bildunterschiede in B hervorgehoben) und die Unterschiede als
/// „A → B“. Nur ansehen – nichts wird bearbeitet oder gespeichert, außer auf ausdrücklichen Wunsch als Markdown mit Bildern.
/// </summary>
sealed class CompareWindow : Window
{
    string _pathA, _pathB;
    SessionView _va, _vb;
    JsonObject _result = new();
    readonly object _sync = new();
    readonly ListBox _list = new() { BorderThickness = new Thickness(0), FontSize = 12.5 };
    readonly TextBlock _head = U.Text("", 13, true), _summary = U.Text("", 12.5, fg: Theme.Fg2), _detail = U.Text("", 12.5, fg: Theme.Fg2);
    readonly CheckBox _onlyDiff = new() { Content = Loc.L("Nur Unterschiede", "Differences only"), IsChecked = true, Margin = new Thickness(0, 0, 16, 0) };
    readonly CheckBox _minor = new() { Content = Loc.L("Alle Schritte", "All steps"), Margin = new Thickness(0, 0, 16, 0) };
    readonly CheckBox _images = new() { Content = Loc.L("Bilder vergleichen", "Compare images"), Margin = new Thickness(0, 0, 16, 0) };
    readonly Image _imgA = new() { Stretch = Stretch.Uniform }, _imgB = new() { Stretch = Stretch.Uniform };
    readonly ScrollViewer _scrollA = new(), _scrollB = new();
    readonly TextBlock _labelA = U.Text("", 12, true, Theme.Muted), _labelB = U.Text("", 12, true, Theme.Muted);
    double _zoom;            // 0 = einpassen
    bool _syncing;
    int _imageJob;

    public CompareWindow(string pathA, string pathB)
    {
        _pathA = pathA; _pathB = pathB;
        _va = SessionView.Open(pathA, headless: true, lang: Loc.Lang);
        _vb = SessionView.Open(pathB, headless: true, lang: Loc.Lang);
        Title = Loc.L("Vergleich", "Comparison"); Width = 1280; Height = 860; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Theme.Prepare(this, Theme.Surface);
        System.Windows.Automation.AutomationProperties.SetName(_list, Loc.L("Paare", "Pairs"));

        var swap = U.Btn(Theme.ICompare, Loc.L("Seiten tauschen", "Swap sides"), Swap, null, "Ghost");
        var save = U.Btn(Theme.ISave, Loc.L("Als Markdown mit Bildern …", "As Markdown with images …"), SaveMarkdown, Loc.L("Vorher/Nachher-Bilder für ein Review oder einen Pull Request", "Before/after images for a review or pull request"), "Ghost");
        var toolbar = U.Row(_onlyDiff, _minor, _images, swap, save);
        foreach (var c in new[] { _onlyDiff, _minor, _images }) { c.VerticalAlignment = VerticalAlignment.Center; c.Checked += (_, _) => Recompute(); c.Unchecked += (_, _) => Recompute(); }
        var top = new StackPanel { Margin = new Thickness(16, 12, 16, 8), Children = { _head, _summary, toolbar } };
        _summary.Margin = new Thickness(0, 2, 0, 8);

        // Bilder: gemeinsamer Zoom (Strg+Mausrad) und Bildausschnitt
        foreach (var (sv, img) in new[] { (_scrollA, _imgA), (_scrollB, _imgB) })
        {
            sv.Content = img; sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled; sv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.Background = Theme.CanvasBg;
            sv.PreviewMouseWheel += (_, e) => { if (Keyboard.Modifiers != ModifierKeys.Control) return; Zoom(e.Delta > 0 ? 1.25 : 0.8); e.Handled = true; };
            sv.ScrollChanged += (s, _) => SyncScroll((ScrollViewer)s);
        }
        _scrollA.MouseDoubleClick += (_, _) => OpenAt(true);
        _scrollB.MouseDoubleClick += (_, _) => OpenAt(false);
        var images = new Grid { Margin = new Thickness(16, 8, 16, 8) };
        images.ColumnDefinitions.Add(new ColumnDefinition()); images.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); images.ColumnDefinitions.Add(new ColumnDefinition());
        images.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); images.RowDefinitions.Add(new RowDefinition());
        void Place(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); images.Children.Add(e); }
        Place(_labelA, 0, 0); Place(_labelB, 0, 2); Place(_scrollA, 1, 0); Place(_scrollB, 1, 2);
        _labelA.Margin = _labelB.Margin = new Thickness(0, 0, 0, 4);
        var hint = U.Text(Loc.L("↑/↓ Paar · F4 / Umschalt+F4 nächster/voriger Unterschied · Strg+Mausrad Zoom · Doppelklick auf ein Bild öffnet die Aufnahme an diesem Schritt",
                                "↑/↓ pair · F4 / Shift+F4 next/previous difference · Ctrl+wheel zoom · double-click an image opens the recording at that step"), 11.5, fg: Theme.Muted, margin: new Thickness(16, 0, 16, 10));
        _detail.TextWrapping = TextWrapping.Wrap; _detail.Margin = new Thickness(16, 0, 16, 6);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.6, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var listBorder = new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 1), Child = _list };
        var splitter = new GridSplitter { Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Theme.Line, ResizeDirection = GridResizeDirection.Rows };
        Grid.SetRow(top, 0); Grid.SetRow(listBorder, 1); Grid.SetRow(splitter, 2); Grid.SetRow(_detail, 3); Grid.SetRow(images, 4); Grid.SetRow(hint, 5);
        foreach (UIElement e in new UIElement[] { top, listBorder, splitter, _detail, images, hint }) root.Children.Add(e);
        Content = root;

        _list.SelectionChanged += (_, _) => ShowPair();
        _list.MouseDoubleClick += (_, _) => OpenAt(true);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F4) { NextDifference(Keyboard.Modifiers == ModifierKeys.Shift ? -1 : 1); e.Handled = true; }
            else if (e.Key == Key.Escape) Close();
        };
        Closed += (_, _) => { lock (_sync) { _va.Dispose(); _vb.Dispose(); } };
        Recompute();
    }

    // -------------------------------------------------------------------------------------

    async void Recompute()
    {
        bool images = _images.IsChecked == true, minor = _minor.IsChecked == true;
        _summary.Text = images ? Loc.L("Bilder werden verglichen …", "Comparing images …") : "";
        var va = _va; var vb = _vb;
        JsonObject r;
        try
        {
            r = images ? await Task.Run(() => { lock (_sync) return StepCompare.Compare(va, vb, minor, images: true); })
                       : StepCompare.Compare(va, vb, minor);
        }
        catch (InvalidOperationException) { _summary.Text = Loc.L("Zu viele Schritte für einen Vergleich (mehr als 5000).", "Too many steps to compare (more than 5000)."); return; }
        if (va != _va) return;   // inzwischen getauscht
        _result = r;
        Fill();
    }

    void Fill()
    {
        _head.Text = $"A  {_va.Title}   ·   B  {_vb.Title}";
        var s = _result.Obj("summary")!;
        _summary.Text = Loc.F("{0} gleich · {1} geändert · {2} nur in A · {3} nur in B", "{0} same · {1} changed · {2} only in A · {3} only in B", s.Long("same"), s.Long("changed"), s.Long("onlyA"), s.Long("onlyB"));
        int keep = _list.SelectedItem is ListBoxItem { Tag: JsonObject old } ? old.Int("a") * 100000 + old.Int("b") : -1;
        _list.Items.Clear();
        bool only = _onlyDiff.IsChecked == true;
        foreach (var p in (_result.Arr("pairs") ?? new JsonArray()).OfType<JsonObject>())
        {
            string st = p.Str("status") ?? "";
            if (only && st == "same") continue;
            string sym = st switch { "same" => "=", "changed" => "≠", "onlyA" => "A", _ => "B" };
            var row = new Grid { Margin = new Thickness(6, 3, 6, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            var brush = st switch { "changed" => Theme.Warn, "onlyA" or "onlyB" => Theme.Danger, _ => Theme.Muted };
            void Cell(string text, int col, Brush fg, bool bold = false) { var t = U.Text(text, 12.5, bold, fg); t.TextTrimming = TextTrimming.CharacterEllipsis; t.ToolTip = text; Grid.SetColumn(t, col); row.Children.Add(t); }
            Cell(sym, 0, brush, true);
            Cell(p["a"] != null ? $"{p.Int("a")}  {p.Obj("details")?.Str("a")}" : "–", 1, Theme.Fg);
            Cell(p["b"] != null ? $"{p.Int("b")}  {p.Obj("details")?.Str("b")}" : "–", 2, Theme.Fg);
            Cell(DiffText(p), 3, Theme.Fg2);
            var item = new ListBoxItem { Content = row, Tag = p };
            System.Windows.Automation.AutomationProperties.SetName(item, sym + " " + DiffText(p));
            _list.Items.Add(item);
            if (p.Int("a") * 100000 + p.Int("b") == keep) _list.SelectedItem = item;
        }
        if (_list.SelectedIndex < 0 && _list.Items.Count > 0) _list.SelectedIndex = 0;
        if (_list.Items.Count == 0) { _detail.Text = Loc.L("Keine Unterschiede.", "No differences."); _imgA.Source = _imgB.Source = null; }
    }

    static string DiffText(JsonObject p)
    {
        bool de = Loc.Lang == "de";
        string st = p.Str("status") ?? "";
        if (st == "onlyA") return Loc.L("nur in A", "only in A");
        if (st == "onlyB") return Loc.L("nur in B", "only in B");
        var d = p.StrList("differences").Select(x => StepCompare.DiffName(x, de)).ToList();
        foreach (var u in p.StrList("unchecked")) d.Add(Loc.F("unverglichen: {0}", "not compared: {0}", u == "image" ? Loc.L("Bild", "image") : Loc.L("Inhalt", "content")));
        return d.Count == 0 ? Loc.L("gleich", "same") : string.Join(", ", d);
    }

    async void ShowPair()
    {
        if (_list.SelectedItem is not ListBoxItem { Tag: JsonObject p }) return;
        _list.ScrollIntoView(_list.SelectedItem);
        var stA = p["a"] != null ? _va.All.FirstOrDefault(s => s.Number == p.Int("a")) : null;
        var stB = p["b"] != null ? _vb.All.FirstOrDefault(s => s.Number == p.Int("b")) : null;
        _labelA.Text = stA != null ? $"A · {stA.Number}. {stA.AuthorTitle ?? stA.Title}" : "A · –";
        _labelB.Text = stB != null ? $"B · {stB.Number}. {stB.AuthorTitle ?? stB.Title}" : "B · –";
        _detail.Text = Detail(p, stA, stB);
        int job = ++_imageJob;
        var va = _va; var vb = _vb;
        bool imagesOn = _images.IsChecked == true;
        var (srcA, srcB) = await Task.Run(() =>
        {
            lock (_sync)
            {
                using var ba = stA != null ? StepCompare.StepBitmap(va, stA) : null;
                using var bb = stB != null ? StepCompare.StepBitmap(vb, stB) : null;
                System.Windows.Media.Imaging.BitmapSource? a = ba != null ? Freeze(U.ToSource(ba)) : null, b = null;
                if (bb != null)
                {
                    if (ba != null && imagesOn)
                    {
                        var diff = ImageDiff.Diff(ba, bb);
                        using var hb = ImageDiff.Highlight(bb, diff.Regions, diff.Size);
                        b = Freeze(U.ToSource(hb));
                    }
                    else b = Freeze(U.ToSource(bb));
                }
                return (a, b);
            }
        });
        if (job != _imageJob) return;
        _imgA.Source = srcA; _imgB.Source = srcB;
    }

    static System.Windows.Media.Imaging.BitmapSource Freeze(System.Windows.Media.Imaging.BitmapSource s) { if (s.CanFreeze) s.Freeze(); return s; }

    static string Detail(JsonObject p, ViewStep? a, ViewStep? b)
    {
        bool de = Loc.Lang == "de";
        var lines = new List<string>();
        foreach (var d in p.StrList("differences"))
            switch (d)
            {
                case "target": lines.Add(Loc.L("Ziel: ", "Target: ") + $"{Target(a)} → {Target(b)}"); break;
                case "text": lines.Add(Loc.L("Text: ", "Text: ") + $"{a?.Json.Obj("input")?.Str("text")} → {b?.Json.Obj("input")?.Str("text")}"); break;
                case "effect": lines.Add(Loc.L("Wirkung: ", "Effect: ") + $"{Opened(a)} → {Opened(b)}"); break;
                case "duration": lines.Add(Loc.L("Dauer: ", "Duration: ") + $"{Fmt.Dur((a?.TTo - a?.TFrom) ?? 0, Loc.Lang)} → {Fmt.Dur((b?.TTo - b?.TFrom) ?? 0, Loc.Lang)}"); break;
                case "response" when p.Obj("responseMs") is { } rm: lines.Add(Loc.L("Reaktion: ", "Response: ") + $"{Timing.Ms(rm.Long("a"), de)} → {Timing.Ms(rm.Long("b"), de)}"); break;
                case "image" when p.Obj("imageDiff") is { } idf: lines.Add(Loc.F("Bild: {0} % der Fläche geändert, {1} Bereiche", "Image: {0} % of the area changed, {1} regions", idf.Dbl("changedPct").ToString("0.#", Loc.Culture), idf.Arr("regions")?.Count ?? 0)); break;
            }
        if (p.StrList("unchecked").Count > 0) lines.Add(Loc.L("Unverglichen (zurückgehalten oder ohne Bild): ", "Not compared (withheld or without image): ") + string.Join(", ", p.StrList("unchecked")));
        return lines.Count == 0 ? (p.Str("status") is "onlyA" or "onlyB" ? Loc.L("Dieser Schritt hat auf der anderen Seite kein Gegenstück.", "This step has no counterpart on the other side.") : Loc.L("Keine Unterschiede.", "No differences.")) : string.Join("\n", lines);
    }

    static string Target(ViewStep? s) => s?.Json.Obj("target")?.Obj("el") is { } el ? Loc.F("{0} „{1}“", "{0} \"{1}\"", el.Str("ct"), el.Str("name")) : "–";
    static string Opened(ViewStep? s) => string.Join(", ", s?.Json.Obj("effect")?.Arr("windowsOpened")?.Select(w => w.Str("title")) ?? Array.Empty<string?>()) is { Length: > 0 } t ? t : "–";

    void NextDifference(int dir)
    {
        int n = _list.Items.Count;
        for (int i = 1; i <= n; i++)
        {
            int k = ((_list.SelectedIndex + dir * i) % n + n) % n;
            if (_list.Items[k] is ListBoxItem { Tag: JsonObject p } && p.Str("status") != "same") { _list.SelectedIndex = k; return; }
        }
    }

    void Zoom(double factor)
    {
        double fit = _scrollA.ActualWidth > 0 && _imgA.Source is { } s ? _scrollA.ActualWidth / s.Width : 1;
        _zoom = Math.Clamp((_zoom == 0 ? fit : _zoom) * factor, 0.1, 8);
        bool fitMode = Math.Abs(_zoom - fit) < 0.02;
        foreach (var (sv, img) in new[] { (_scrollA, _imgA), (_scrollB, _imgB) })
        {
            if (fitMode) { img.Stretch = Stretch.Uniform; img.Width = img.Height = double.NaN; sv.HorizontalScrollBarVisibility = sv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled; }
            else if (img.Source is { } src) { img.Stretch = Stretch.Fill; img.Width = src.Width * _zoom; img.Height = src.Height * _zoom; sv.HorizontalScrollBarVisibility = sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; }
        }
        if (fitMode) _zoom = 0;
    }

    void SyncScroll(ScrollViewer from)
    {
        if (_syncing) return;
        _syncing = true;
        var to = from == _scrollA ? _scrollB : _scrollA;
        to.ScrollToHorizontalOffset(from.HorizontalOffset); to.ScrollToVerticalOffset(from.VerticalOffset);
        _syncing = false;
    }

    void Swap()
    {
        lock (_sync) { (_va, _vb) = (_vb, _va); (_pathA, _pathB) = (_pathB, _pathA); }
        Recompute();
    }

    void OpenAt(bool sideA)
    {
        if (_list.SelectedItem is not ListBoxItem { Tag: JsonObject p }) return;
        var key = sideA ? "a" : "b";
        if (p[key] == null) return;
        var v = sideA ? _va : _vb;
        if (v.All.FirstOrDefault(s => s.Number == p.Int(key)) is { } st) UiApp.Instance.OpenViewer(sideA ? _pathA : _pathB, st.Anchor);
    }

    void SaveMarkdown()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = Fmt.AsciiSlug(Loc.L("vergleich-", "comparison-") + _va.Title) + ".md", Filter = "Markdown|*.md", DefaultExt = ".md" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            List<string> files;
            lock (_sync)
            {
                var r = _images.IsChecked == true ? _result : StepCompare.Compare(_va, _vb, _minor.IsChecked == true, images: true);
                files = StepCompare.WriteMarkdownWithImages(r, _va, _vb, dlg.FileName, Loc.Lang);
            }
            if (Msg.Show(Loc.F("Gespeichert: {0} Dateien.\n{1}\n\nIm Explorer zeigen?", "Saved: {0} files.\n{1}\n\nShow in Explorer?", files.Count, dlg.FileName), Title, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{dlg.FileName}\"");
        }
        catch (Exception ex) { UiApp.ShowError(ex); }
    }
}
