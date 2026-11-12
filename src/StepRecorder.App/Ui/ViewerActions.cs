using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StepRecorder.Compare;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.App.Ui;

sealed partial class ViewerWindow
{
    // =====================================================================================
    // Kopieren
    // =====================================================================================

    byte[]? CurrentPng()
    {
        if (_current == null) return null;
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(_current));
        using var ms = new MemoryStream(); enc.Save(ms); return ms.ToArray();
    }

    void SetClipboard(DataObject d, string confirm)
    {
        for (int i = 0; i < 5; i++)
        {
            try { Clipboard.SetDataObject(d, true); _status.Text = confirm; return; }
            catch { System.Threading.Thread.Sleep(100); }
        }
        Msg.Show("Die Zwischenablage ist gerade gesperrt.");
    }

    bool ConfirmUnreviewed()
    {
        if (_view.UnreviewedCount == 0 || _askedUnreviewed) return true;
        _askedUnreviewed = true;
        return Msg.Show($"{_view.UnreviewedCount} auffällige Eingaben sind noch nicht durchgesehen. Trotzdem Text kopieren?", "Durchsicht", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
    bool _askedUnreviewed;

    void CopyImage()
    {
        if (_current == null) return;
        var d = new DataObject();
        d.SetImage(_current);
        if (CurrentPng() is { } png) d.SetData("PNG", new MemoryStream(png));
        SetClipboard(d, $"Bild kopiert · {_current.PixelWidth} × {_current.PixelHeight}");
    }

    void CopyOriginal()
    {
        var s = Sel; if (s?.PreFrame == null) return;
        using var bmp = _view.Frame(_showAfter ? s.PostFrame ?? s.PreFrame : s.PreFrame);
        if (bmp == null) return;
        var src = U.ToSource(bmp);
        var d = new DataObject(); d.SetImage(src);
        SetClipboard(d, $"Originalbild kopiert · {src.PixelWidth} × {src.PixelHeight}");
    }

    string StepText(ViewStep s) => $"{s.Number}. {s.AuthorTitle ?? s.Title}\n{s.AuthorBody ?? s.Guide}" + string.Concat(s.Comments.Select(c => "\nHinweis: " + c.Str("text")));

    void CopyText()
    {
        var s = Sel; if (s == null || !ConfirmUnreviewed()) return;
        var d = new DataObject(); d.SetText(StepText(s));
        SetClipboard(d, "Text kopiert");
    }

    void CopyImageWithText()
    {
        var s = Sel; if (s == null || !ConfirmUnreviewed()) return;
        var png = CurrentPng();
        var html = new StringBuilder($"<p><b>{Fmt.HtmlEscape(s.Number + ". " + (s.AuthorTitle ?? s.Title))}</b><br>{Fmt.HtmlEscape(s.AuthorBody ?? s.Guide)}</p>");
        foreach (var c in s.Comments) html.Append($"<p><i>Hinweis:</i> {Fmt.HtmlEscape(c.Str("text"))}</p>");
        if (png != null) html.Append($"<p><img width=\"{Math.Min(_current!.PixelWidth, 605)}\" src=\"data:image/png;base64,{Convert.ToBase64String(png)}\"></p>");
        var d = new DataObject();
        d.SetData(DataFormats.Html, HtmlClipboard(html.ToString()));
        d.SetText(StepText(s));
        if (_current != null) d.SetImage(_current);
        SetClipboard(d, "Bild mit Text kopiert");
    }

    void CopyMarkdown()
    {
        var s = Sel; if (s == null || !ConfirmUnreviewed()) return;
        var d = new DataObject();
        d.SetText($"### {s.Number}. {s.AuthorTitle ?? s.Title}\n\n{s.AuthorBody ?? s.Guide}\n\n![Schritt {s.Number}](Schritt-{s.Number:00}.png)\n");
        SetClipboard(d, "Als Markdown kopiert");
    }

    void CopyWholeGuide()
    {
        if (!ConfirmUnreviewed()) return;
        var tmp = Path.Combine(_view.Session.Path, "export", ".tmp", "guide.html");
        Directory.CreateDirectory(Path.GetDirectoryName(tmp)!);
        var view = SessionView.Open(_path, headless: true);
        try
        {
            GuideExport.Export(view, new GuideOptions { Format = "html", OutPath = tmp });
            var html = File.ReadAllText(tmp);
            int b = html.IndexOf("<body>", StringComparison.Ordinal);
            var body = b >= 0 ? html[(b + 6)..html.LastIndexOf("</body>", StringComparison.Ordinal)] : html;
            var d = new DataObject();
            d.SetData(DataFormats.Html, HtmlClipboard(body));
            d.SetText(string.Join("\n\n", _view.Effective(guide: true, importance: "primary").Select(StepText)));
            SetClipboard(d, "Ganze Anleitung kopiert");
        }
        finally { view.Dispose(); try { File.Delete(tmp); } catch { } }
    }

    /// <summary>CF_HTML-Kopf mit Byte-Offsets.</summary>
    static string HtmlClipboard(string fragment)
    {
        const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        string pre = "<html><body><!--StartFragment-->", post = "<!--EndFragment--></body></html>";
        int hLen = string.Format(header, 0, 0, 0, 0).Length;
        int startHtml = hLen, startFrag = startHtml + Encoding.UTF8.GetByteCount(pre), endFrag = startFrag + Encoding.UTF8.GetByteCount(fragment), endHtml = endFrag + Encoding.UTF8.GetByteCount(post);
        return string.Format(header, startHtml, endHtml, startFrag, endFrag) + pre + fragment + post;
    }

    void SaveImage()
    {
        var s = Sel; if (s == null) return;
        var png = CurrentPng(); if (png == null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = $"Schritt-{s.Number:00}-{Fmt.FileSafe(s.AuthorTitle ?? s.Title, 30)}.png", Filter = "PNG|*.png" };
        if (dlg.ShowDialog(this) == true) File.WriteAllBytes(dlg.FileName, png);
    }

    // =====================================================================================
    // Export, Durchsicht, Support-Paket, Übersicht, Vergleich
    // =====================================================================================

    public void ShowExportDialog(string format)
    {
        if (_view.UnreviewedCount > 0 && format != "ki")
        {
            if (Msg.Show($"{_view.UnreviewedCount} auffällige Eingaben sind noch nicht durchgesehen. Die Anleitung kann erst nach der Durchsicht erstellt werden.\n\nJetzt prüfen?", "Durchsicht", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes) ShowReview();
            if (_view.UnreviewedCount > 0) return;
        }
        var d = new ExportDialog(format, _view.Language, StepRecorder.Export.DocSettings.From(_view.Edits.Obj("guide"))) { Owner = this };
        if (d.ShowDialog() != true) return;
        var view = SessionView.Open(_path, headless: d.Format == "ki", lang: d.Language);
        try
        {
            ExportResult r = d.Format switch
            {
                "ki" => KiPackage.Export(view, new KiOptions { Detail = d.Detail, ImageProfile = d.Profile, Language = d.Language, Zip = d.Zip }),
                "ahk" when d.AhkKi => AhkKiPackage.Export(view, new AhkKiOptions()),
                "ahk" => AhkExport.Export(view, new AhkOptions { RealTiming = d.RealTiming }),
                _ => GuideExport.Export(view, new GuideOptions { Format = d.Format, Language = d.Language, Importance = d.Importance, View = d.View, Overview = d.Overview, GifSeconds = d.GifSeconds, MouseMarks = d.MarkMouse, KeyMarks = d.MarkKeys })
            };
            _status.Text = "Exportiert: " + r.Path;
            var msg = "Export erstellt:\n" + r.Path + (r.Warnings.Count + r.Notices.Count > 0 ? "\n\n" + string.Join("\n", r.Warnings.Concat(r.Notices)) : "") + "\n\nIm Explorer zeigen?";
            if (Msg.Show(msg, "Export", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                Process.Start("explorer.exe", File.Exists(r.Path) ? $"/select,\"{r.Path}\"" : $"\"{r.Path}\"");
        }
        finally { view.Dispose(); }
    }

    public void StartSupportFlow()
    {
        Msg.Show("Support-Paket in drei Schritten:\n1. Aufnahme ansehen\n2. Eingaben durchsehen\n3. Paket erstellen\n\nDas Programm versendet nichts.", "Support-Paket", MessageBoxButton.OK, MessageBoxImage.Information);
        ShowReview(forceAll: true);
        if (_view.UnreviewedCount > 0) return;
        var view = SessionView.Open(_path);
        try
        {
            var r = GuideExport.SupportPackage(view);
            if (Msg.Show("Support-Paket erstellt:\n" + r.Path + "\n\nGeben Sie das ZIP selbst an den Helpdesk weiter. Im Explorer zeigen?", "Support-Paket", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                Process.Start("explorer.exe", $"/select,\"{r.Path}\"");
        }
        finally { view.Dispose(); }
    }

    void ShowReview() => ShowReview(false);

    void ShowReview(bool forceAll)
    {
        var w = new ReviewWindow(_view) { Owner = this };
        if (w.ShowDialog() != true) return;
        Change(e =>
        {
            var review = e["review"] as JsonObject ?? new JsonObject();
            var inputs = review["inputs"] as JsonArray ?? new JsonArray();
            var redactions = e["redactions"] as JsonArray ?? new JsonArray();
            foreach (var (step, status) in w.Decisions)
            {
                inputs.Add(new JsonObject { ["kind"] = "typed", ["anchor"] = step.Anchor, ["seqRange"] = new JsonArray(step.Anchor, step.Anchor), ["status"] = status, ["flagged"] = new JsonArray(step.FlagReason ?? ""), ["atUtc"] = Json.UtcNow() });
                if (status == "redacted")
                    redactions.Add(new JsonObject { ["id"] = NextId(e, "redactions", "r") + "_" + step.Anchor, ["kind"] = "range", ["origin"] = "step", ["anchor"] = step.Anchor, ["seqRanges"] = step.Json.Obj("seq")?.Arr("input")?.DeepClone() ?? new JsonArray(), ["images"] = true, ["createdUtc"] = Json.UtcNow(), ["by"] = "user" });
            }
            review["inputs"] = inputs;
            review["confirmed"] = new JsonObject { ["atUtc"] = Json.UtcNow(), ["throughSeq"] = _view.Session.Events.Count, ["patternsVersion"] = Patterns.Version };
            e["review"] = review;
            e["redactions"] = redactions;
        });
    }

    void ShowOverview()
    {
        var c = _view.Steps.Obj("completeness")!;
        var bk = c.Obj("bookkeeping")!; var ev = c.Obj("evidence")!; var fr = c.Obj("frames")!;
        var m = _view.Session.Manifest;
        var sb = new StringBuilder();
        sb.AppendLine($"Titel: {_view.Title}");
        sb.AppendLine($"Aufgenommen: {_view.Session.StartUtc.ToLocalTime():dd.MM.yyyy HH:mm} · Dauer {Fmt.Time(_view.Session.DurationUs, "de")}");
        sb.AppendLine($"Zustand: {_view.Session.State} · Profil: {m.Obj("profile").Str("name")} · Schutzstufe: {m.Obj("settings").Obj("privacy").Str("level")}");
        if (_view.Goal() is { } g) sb.AppendLine($"Ziel: {g.Str("description")} · Erwartet: {g.Str("expected")} · Bezug: {g.Str("reference")}");
        sb.AppendLine();
        sb.AppendLine("Vollständigkeitsbericht");
        sb.AppendLine($"  Buchführung: {bk.Long("inputEvents")} Eingabeereignisse, {bk.Long("assigned")} zugeordnet, {bk.Long("orphans")} ohne Schritt");
        sb.AppendLine($"  Belegqualität: {ev.Long("actionSteps")} Aktionsschritte · {ev.Long("confirmed")} belegt · {ev.Long("inputOnly")} nur Eingabe · {ev.Long("unknown")} unbekannt");
        sb.AppendLine($"  Lücken: {c.Obj("gaps").Long("count")} ({Fmt.Dur(c.Obj("gaps").Long("totalUs"), "de")})");
        sb.AppendLine($"  Bilder: {fr.Long("requested")} angefordert · {fr.Long("stored")} gespeichert · {fr.Long("same")} unverändert · Vorher-Bild garantiert {fr.Long("preGuaranteed")}/{fr.Long("preGuaranteed") + fr.Long("preNotGuaranteed")}");
        foreach (var gp in _view.Steps.Arr("gaps") ?? new JsonArray())
            sb.AppendLine($"  – {Fmt.Time(gp.Long("tFrom"), "de")}: {Steps.TextTemplates.CauseText(gp.Str("cause") ?? "", "de")}");
        sb.AppendLine();
        long bytes = 0; try { bytes = new DirectoryInfo(_view.Session.Path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); } catch { }
        sb.AppendLine($"Speicher: {bytes / 1024.0 / 1024.0:0.0} MB · {_view.Session.Path}");
        var r = Msg.Show(sb + "\n\nOrdner im Explorer öffnen?", "Sitzungsübersicht", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (r == MessageBoxResult.Yes) Process.Start("explorer.exe", $"\"{_view.Session.Path}\"");
    }

    void Compare()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Zweite Aufnahme wählen (manifest.json)", Filter = "Sitzung|manifest.json", InitialDirectory = Path.GetDirectoryName(_view.Session.Path) };
        if (dlg.ShowDialog(this) != true) return;
        using var other = SessionView.Open(Path.GetDirectoryName(dlg.FileName)!, headless: true);
        using var me = SessionView.Open(_path, headless: true);
        var r = StepCompare.Compare(me, other);
        var w = new Window { Title = "Vergleich", Width = 960, Height = 720, Owner = this };
        Theme.Prepare(w, Theme.Surface);
        w.Content = new TextBox { Text = StepCompare.Markdown(r, "de"), FontFamily = Theme.Mono, FontSize = 12.5, IsReadOnly = true, BorderThickness = new Thickness(0), Padding = new Thickness(20, 16, 20, 16), VerticalContentAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        w.Show();
    }
}

/// <summary>Einfacher Eingabedialog (Text, optional Titel und Kommentarart).</summary>
sealed class InputDialog : Window
{
    readonly TextBox _value = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top };
    readonly TextBox? _title;
    readonly RadioButton[]? _kinds;
    readonly TextBlock _titleLabel = U.Text("Titel", 12.5, true, Theme.Fg2, new Thickness(0, 0, 0, 5));
    public string Value => _value.Text;
    public string TitleValue => _title?.Text ?? "";
    public string Kind => _kinds == null ? "note" : Array.FindIndex(_kinds, k => k.IsChecked == true) switch { 1 => "tip", 2 => "warning", 3 => "internal", _ => "note" };
    public string TitleLabel { set => _titleLabel.Text = value; }

    public InputDialog(string prompt, string value, string? title = null, bool kinds = false)
    {
        Title = AppInfo.DisplayName; Width = 560; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Surface);
        var p = new StackPanel();
        if (kinds)
        {
            var (track, items) = U.Segmented("kind", 0, (Theme.IComment, "Hinweis"), (Theme.ICheck, "Tipp"), (Theme.IWarn, "Achtung"), (Theme.ILock, "Intern"));
            _kinds = items;
            items[3].ToolTip = "Erscheint in keiner Ausgabe";
            track.Margin = new Thickness(0, 0, 0, 14);
            p.Children.Add(track);
        }
        if (title != null) { _title = new TextBox { Text = title }; p.Children.Add(new StackPanel { Margin = new Thickness(0, 0, 0, 14), Children = { _titleLabel, _title } }); }
        _value.Text = value;
        p.Children.Add(title != null ? U.Field("Text", _value, margin: new Thickness(0)) : _value);
        var hint = U.Row(U.Kbd("Strg+Eingabe"), U.Text("übernimmt", 12, fg: Theme.Muted, margin: new Thickness(4, 1, 0, 0)));
        hint.Margin = new Thickness(0, 10, 0, 0);
        p.Children.Add(hint);
        U.Dialog(this, prompt, p, U.Primary("Übernehmen", () => DialogResult = true), U.Btn("Abbrechen", () => DialogResult = false));
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control) { DialogResult = true; e.Handled = true; }
            if (e.Key == System.Windows.Input.Key.Escape) DialogResult = false;
        };
        Loaded += (_, _) => { var f = _title ?? _value; f.Focus(); f.CaretIndex = f.Text.Length; };
    }
}

/// <summary>Durchsicht der Eingaben: jede auffällige Eingabe braucht eine eigene Entscheidung.</summary>
sealed class ReviewWindow : Window
{
    public List<(ViewStep step, string status)> Decisions { get; } = new();

    public ReviewWindow(SessionView view)
    {
        Title = "Durchsicht der Eingaben"; Width = 760; Height = 620; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Bg);
        var flagged = view.All.Where(s => s.Unreviewed).ToList();
        var others = view.All.Where(s => s.Kind == "type" && !s.Unreviewed && !s.Json.Obj("input")!.Bool("masked")).ToList();
        var list = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        list.Children.Add(U.Text("Durchsicht der Eingaben", 18, true, margin: new Thickness(0, 0, 0, 4)));
        list.Children.Add(U.Text(flagged.Count == 0 ? "Keine auffälligen Eingaben. Bitte die übrigen Eingaben kurz prüfen." : $"{flagged.Count} auffällige Eingaben – bitte für jede entscheiden, ob sie bleiben darf.", 13, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 16)));
        var choices = new List<(ViewStep, RadioButton[])>();
        foreach (var s in flagged)
        {
            var (track, items) = U.Segmented("rv" + s.Anchor, -1, (Theme.ICheck, "In Ordnung"), (Theme.IRedact, "Schwärzen"));
            track.VerticalAlignment = VerticalAlignment.Center;
            choices.Add((s, items));
            var dock = new DockPanel();
            DockPanel.SetDock(track, Dock.Right); dock.Children.Add(track);
            var badge = U.IconBadge(Theme.IWarn, 34, Theme.WarnSoft, Theme.Warn, 14); badge.Margin = new Thickness(0, 0, 14, 0); badge.VerticalAlignment = VerticalAlignment.Top;
            DockPanel.SetDock(badge, Dock.Left); dock.Children.Add(badge);
            dock.Children.Add(U.Col(
                U.Text($"Schritt {s.Number} · {s.Json.Obj("ctx")?.Str("title")}", 12, fg: Theme.Muted),
                new TextBlock { Text = Steps.TextTemplates.TokensNotation(s.Json.Obj("input")!), FontFamily = Theme.Mono, FontSize = 13.5, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 12, 3) },
                U.Text("Grund: " + s.FlagReason, 12, fg: Theme.Warn)));
            list.Children.Add(U.Card(dock, new Thickness(16, 14, 16, 14), new Thickness(0, 0, 0, 10)));
        }
        if (others.Count > 0)
        {
            list.Children.Add(U.Overline($"Unauffällige Eingaben ({others.Count})", new Thickness(0, 14, 0, 8)));
            var box = new StackPanel();
            foreach (var s in others.Take(50))
            {
                var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
                var n = U.Text($"Schritt {s.Number}", 12, fg: Theme.Muted); n.Width = 76; DockPanel.SetDock(n, Dock.Left); row.Children.Add(n);
                row.Children.Add(new TextBlock { Text = Steps.TextTemplates.TokensNotation(s.Json.Obj("input")!), FontFamily = Theme.Mono, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
                box.Children.Add(row);
            }
            list.Children.Add(U.Card(box, new Thickness(16, 10, 16, 10)));
        }
        var ok = U.Primary("Fertig", () =>
        {
            if (choices.Any(c => c.Item2.All(r => r.IsChecked != true))) { Msg.Show("Noch nicht alles entschieden\n\nBitte für jede auffällige Eingabe „In Ordnung“ oder „Schwärzen“ wählen.", "Durchsicht", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            foreach (var (s, items) in choices) Decisions.Add((s, items[0].IsChecked == true ? "kept" : "redacted"));
            DialogResult = true;
        });
        var cancel = U.Btn("Abbrechen", () => DialogResult = false);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } };
        ok.Margin = new Thickness(8, 0, 0, 0); cancel.Margin = new Thickness(8, 0, 0, 0); cancel.MinWidth = 96;
        var foot = new Border { Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 12, 20, 12), Child = footer };
        var root = new DockPanel();
        DockPanel.SetDock(foot, Dock.Bottom);
        root.Children.Add(foot);
        root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }
}

/// <summary>Exportdialog: Format, Sprache, Umfang, Darstellung bzw. Detailstufe und Bildprofil.</summary>
sealed class ExportDialog : Window
{
    readonly RadioButton[] _formats;
    readonly ComboBox _lang = new() { Items = { "Deutsch", "English" } };
    readonly ComboBox _imp = new() { Items = { "Hauptschritte", "Haupt- und Nebenschritte", "Alle Schritte" }, SelectedIndex = 0 };
    readonly ComboBox _view = new() { Items = { "Ganzer Bildschirm", "Anwendungsfenster", "Ausschnitt um die Aktion" }, SelectedIndex = 0 };
    readonly ComboBox _detail = new() { Items = { "Kompakt", "Standard", "Vollständig" }, SelectedIndex = 1 };
    readonly ComboBox _profile = new() { Items = { "Kompatibel (≤ 1568 px)", "Hochauflösend (≤ 2576 px)", "Original" }, SelectedIndex = 0 };
    readonly CheckBox _zip = new() { Content = "Als ZIP-Datei verpacken", Margin = new Thickness(0, 2, 0, 0) };
    readonly CheckBox _timing = new() { Content = "Auch die Denkpausen der Aufnahme übernehmen", IsChecked = false, Margin = new Thickness(0, 0, 0, 8) };
    readonly CheckBox _ahkKi = new() { Content = "Als KI-Paket: Anleitung für KI, ablauf.json, Bildausschnitte, Texterkennung", IsChecked = false };
    public bool RealTiming => _timing.IsChecked == true;
    public bool AhkKi => _ahkKi.IsChecked == true;
    int FormatIndex => Math.Max(0, Array.FindIndex(_formats, f => f.IsChecked == true));
    public string Format => FormatIndex switch { 1 => "md", 2 => "docx", 3 => "pdf", 4 => "gif", 5 => "ki", 6 => "ahk", _ => "html" };
    readonly CheckBox _overview = new() { Content = "Kleine Übersicht des ganzen Bildschirms einblenden", IsChecked = true, Margin = new Thickness(0, 6, 0, 0) };
    readonly ComboBox _gifSec = new() { Items = { "1,5 Sekunden je Schritt", "2,5 Sekunden je Schritt", "4 Sekunden je Schritt" }, SelectedIndex = 1 };
    public bool Overview => _overview.IsChecked == true;
    public double GifSeconds => _gifSec.SelectedIndex switch { 0 => 1.5, 2 => 4, _ => 2.5 };
    public string Language => _lang.SelectedIndex == 1 ? "en" : "de";
    public string Importance => _imp.SelectedIndex switch { 1 => "secondary", 2 => "all", _ => "primary" };
    public string View => _view.SelectedIndex switch { 1 => "window", 2 => "crop", _ => "screen" };
    public string Detail => _detail.SelectedIndex switch { 0 => "compact", 2 => "full", _ => "standard" };
    public string Profile => _profile.SelectedIndex switch { 1 => "highres", 2 => "original", _ => "compatible" };
    public bool Zip => _zip.IsChecked == true;
    readonly CheckBox _markMouse = new() { Content = "Mausaktionen: Klickpunkt, Zugweg, Zielrahmen, Scrollrichtung", IsChecked = true, Margin = new Thickness(0, 2, 0, 6) };
    readonly CheckBox _markKeys = new() { Content = "Tastatur: gedrückte Tasten und getippten Text als Tasten", IsChecked = true };
    public bool MarkMouse => _markMouse.IsChecked == true;
    public bool MarkKeys => _markKeys.IsChecked == true;

    readonly StackPanel _info = new();
    readonly TextBlock _impHint = Hint(), _viewHint = Hint(), _detailHint = Hint(), _profileHint = Hint();

    static TextBlock Hint() => U.Text("", 11.5, fg: Theme.Muted, margin: new Thickness(0, 5, 0, 0));

    public ExportDialog(string format, string lang, StepRecorder.Export.DocSettings? doc = null)
    {
        Title = "Exportieren"; Width = 760; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Surface);
        int sel = format switch { "md" => 1, "docx" => 2, "pdf" => 3, "gif" => 4, "ki" => 5, "ahk" => 6, _ => 0 };
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 7, Margin = new Thickness(-4, 0, -4, 12) };
        if (doc != null) { _view.SelectedIndex = doc.View switch { "window" => 1, "crop" => 2, _ => 0 }; _overview.IsChecked = doc.Overview; }
        var formats = new List<RadioButton>();
        foreach (var (glyph, name, sub, i) in new[] { (Theme.IGlobe, "HTML", "eine Datei", 0), (Theme.ICode, "Markdown", "Text + Bilder", 1), (Theme.IDoc, "Word", "bearbeitbar", 2), (Theme.IPdf, "PDF", "zum Drucken", 3), (Theme.IPlay, "GIF", "animiert", 4), (Theme.IRobot, "KI-Paket", "für KI", 5), (Theme.IScript, "AutoHotkey", "Skript", 6) })
        {
            var content = U.Col(U.Icon(glyph, 22), new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) },
                new TextBlock { Text = sub, FontSize = 11, Opacity = 0.75, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0) });
            var rb = new RadioButton { GroupName = "fmt", Style = Theme.Style("Choice"), Content = content, IsChecked = i == sel };
            formats.Add(rb); grid.Children.Add(rb);
        }
        _formats = formats.ToArray();
        _lang.SelectedIndex = lang == "en" ? 1 : 0;

        // Was steckt drin?
        var infoBox = new Border { Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(16, 12, 16, 14), Margin = new Thickness(0, 0, 0, 16), Child = _info };

        var options = new Grid();
        options.ColumnDefinitions.Add(new ColumnDefinition()); options.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); options.ColumnDefinitions.Add(new ColumnDefinition());
        options.RowDefinitions.Add(new RowDefinition()); options.RowDefinitions.Add(new RowDefinition());
        void G(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); options.Children.Add(e); }
        var fImp = U.Field("Umfang", U.Col(_imp, _impHint)); var fView = U.Field("Darstellung", U.Col(_view, _viewHint, _overview));
        var fDetail = U.Field("Detailstufe", U.Col(_detail, _detailHint)); var fProfile = U.Field("Bildgröße", U.Col(_profile, _profileHint));
        G(U.Field("Sprache", _lang), 0, 0); G(fImp, 0, 2); G(fView, 1, 0); G(fDetail, 0, 2); G(fProfile, 1, 0); G(new StackPanel { Children = { _zip }, Margin = new Thickness(0, 24, 0, 0) }, 1, 2);
        var zipHost = (FrameworkElement)_zip.Parent;
        var gifOpts = U.Field("Ablauf", _gifSec, "Das GIF zeigt ein Titelbild und dann jeden Schritt mit Nummer und Überschrift.");
        gifOpts.Margin = new Thickness(0, 0, 0, 12);
        var ahkOpts = U.Col(_timing, _ahkKi);
        ahkOpts.Margin = new Thickness(0, 0, 0, 12);
        var marksOpts = U.Field("Im Bild markieren", U.Col(_markMouse, _markKeys), "Wie im Editor unter „Markierungen“; dort lässt sich das auch je Schritt ein- und ausschalten.");
        marksOpts.Margin = new Thickness(0, 0, 0, 12);
        void Upd()
        {
            bool ki = Format == "ki", ahk = Format == "ahk";
            options.Visibility = ahk ? Visibility.Collapsed : Visibility.Visible;
            ahkOpts.Visibility = ahk ? Visibility.Visible : Visibility.Collapsed;
            gifOpts.Visibility = Format == "gif" ? Visibility.Visible : Visibility.Collapsed;
            _overview.Visibility = _view.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            marksOpts.Visibility = ki || ahk ? Visibility.Collapsed : Visibility.Visible;
            fImp.Visibility = fView.Visibility = ki ? Visibility.Collapsed : Visibility.Visible;
            fDetail.Visibility = fProfile.Visibility = zipHost.Visibility = ki ? Visibility.Visible : Visibility.Collapsed;
            _impHint.Text = _imp.SelectedIndex switch
            {
                1 => "Zusätzlich Nebenschritte wie Scrollen, Navigationstasten und Fensterwechsel.",
                2 => "Zusätzlich die Spur, z. B. Mausbewegungen ohne Aktion und Titeländerungen.",
                _ => "Nur was eine Anleitung braucht: Klicks, Eingaben, Tastenkürzel, Kommentare."
            };
            _viewHint.Text = _view.SelectedIndex switch
            {
                1 => "Nur das bediente Fenster, ohne den restlichen Bildschirm.",
                2 => "Vergrößerter Bereich rund um den Klick oder die Eingabe.",
                _ => "Das komplette Bildschirmfoto."
            };
            _detailHint.Text = _detail.SelectedIndex switch
            {
                0 => "Haupt- und Nebenschritte, Bilder nur für Hauptschritte, kurze Texte – spart Platz bei der KI.",
                2 => "Alle Schritte mit allen Bildern und den Rohereignissen – für die Fehlersuche.",
                _ => "Haupt- und Nebenschritte, Hauptschritte mit bis zu drei Bildern."
            };
            _profileHint.Text = _profile.SelectedIndex switch
            {
                1 => "Schärfer, braucht aber mehr Platz im KI-Kontext.",
                2 => "Unverkleinert – größte Dateien.",
                _ => "Passt für alle gängigen KI-Assistenten."
            };
            FillInfo();
        }
        foreach (var f in _formats) f.Checked += (_, _) => Upd();
        foreach (var cb in new[] { _imp, _view, _detail, _profile, _gifSec }) cb.SelectionChanged += (_, _) => Upd();
        foreach (var chk in new[] { _zip, _ahkKi }) { chk.Checked += (_, _) => Upd(); chk.Unchecked += (_, _) => Upd(); }
        Upd();
        U.Dialog(this, "Exportieren", U.Col(grid, infoBox, options, gifOpts, marksOpts, ahkOpts),U.Primary("Exportieren", () => DialogResult = true, Theme.IExport), U.Btn("Abbrechen", () => DialogResult = false));
    }

    /// <summary>Beschreibt das gewählte Format: wofür, welche Dateien mit welchem Inhalt, wo gespeichert.</summary>
    void FillInfo()
    {
        _info.Children.Clear();
        string imp = _imp.SelectedIndex switch { 1 => "Haupt- und Nebenschritte", 2 => "alle Schritte", _ => "alle Hauptschritte" };
        string guideContent = $"Titel, Einleitung, Voraussetzungen, Kapitel und {imp} – jeweils mit Überschrift, Anleitungstext, Bild und Hinweiskästen; Deckblatt, Inhaltsverzeichnis, Bildgröße und Farbe stellen Sie im Editor im Reiter „Dokument“ ein";
        (string title, string use, (string file, string what)[] files, string where) = Format switch
        {
            "md" => ("Markdown – Text und ein Bilderordner", "Für Wikis, Git-Repositories, Confluence oder Notiz-Apps wie Obsidian.",
                new[] { ("<Titel>.md", guideContent + " (als Markdown-Text)."), ("bilder\\", "ein PNG je Schritt (schritt-001.png …), in der .md-Datei verlinkt.") }, "export\\markdown"),
            "docx" => ("Word – zum Weiterbearbeiten", "Öffnet in Word oder LibreOffice; Texte, Layout und Bilder lassen sich dort noch anpassen.",
                new[] { ("<Titel>.docx", guideContent + ". Die Bilder sind eingebettet.") }, "export\\docx"),
            "pdf" => ("PDF – fertig zum Drucken und Weitergeben", "Sieht überall gleich aus und lässt sich nicht versehentlich ändern.",
                new[] { ("<Titel>.pdf", guideContent + ". Die Bilder sind eingebettet.") }, "export\\pdf"),
            "gif" => ("GIF – animiert, für Wiki, Chat und E-Mail", "Spielt die Schritte nacheinander ab – ideal für Teams, Confluence oder eine schnelle Vorschau. Ohne Anleitungstexte, nur mit Überschriften.",
                new[] { ("<Titel>.gif", $"Titelbild, dann {imp} mit Bild, Nummer und Überschrift; {GifSeconds.ToString("0.#")} Sekunden je Schritt, läuft in Schleife.") }, "export\\gif"),
            "ki" => ("KI-Paket – für ChatGPT, Claude & Co.", "Ein Ordner, den Sie einer KI geben (hochladen oder als ZIP anhängen). Die KI erfährt daraus genau, was in welcher Reihenfolge passiert ist und wie sicher das belegt ist.",
                KiFiles(), "export\\ki-paket-<Datum und Uhrzeit>" + (Zip ? " (zusätzlich als ZIP-Datei)" : "")),
            "ahk" when AhkKi => ("AutoHotkey als KI-Paket", "Ein Skript-Entwurf plus alles, was ein KI-Assistent braucht, um das Skript zu prüfen und zu verbessern.",
                new[] { ("entwurf.ahk", "das AutoHotkey-v2-Skript als Entwurf."), ("ablauf.json", "alle Schritte mit Fenstern, Steuerelementen, Wartebedingungen und Prüfungen."), ("README_AHK_KI.md", "Anleitung für den KI-Assistenten."), ("bilder\\", "Bildausschnitte der Bedienelemente mit erkanntem Text.") }, "export\\ahk-ki\\<Titel>"),
            "ahk" => ("AutoHotkey – Ablauf automatisch wiederholen", "Ein AutoHotkey-v2-Skript, das die aufgenommenen Schritte nachspielt. Zum Ausführen muss AutoHotkey v2 installiert sein.",
                new[] { ("<Titel>.ahk", "das Skript: findet Fenster über ahk_class/ahk_exe und Steuerelemente über ClassNN, wartet wie in der Aufnahme und prüft das Ergebnis. Getippte Werte stehen als Parameter oben, Passwörter werden beim Start abgefragt. Abbruch mit Strg+Esc."), ("bilder\\", "Bildausschnitte als Rückfall, falls ein Element nicht gefunden wird.") }, "export\\ahk"),
            _ => ("HTML – eine einzige Datei für den Browser", "Zum Verschicken per E-Mail, für das Intranet oder zum Ansehen ohne weitere Programme.",
                new[] { ("<Titel>.html", guideContent + ". Die Bilder stecken in der Datei – es gibt nur diese eine Datei.") }, "export\\html")
        };
        _info.Children.Add(U.Text(title, 14, true, margin: new Thickness(0, 0, 0, 2)));
        _info.Children.Add(U.Text(use, 12.5, fg: Theme.Fg2, margin: new Thickness(0, 0, 0, 10)));
        _info.Children.Add(U.Overline("Enthält", new Thickness(0, 0, 0, 4)));
        foreach (var (file, what) in files)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var icon = U.Icon(file.EndsWith('\\') ? Theme.IFolder : Theme.IDoc, 12, Theme.AccentText); icon.VerticalAlignment = VerticalAlignment.Top; icon.Margin = new Thickness(0, 2, 7, 0);
            var name = new TextBlock { Text = file, FontFamily = Theme.Mono, FontSize = 12, Foreground = Theme.Fg, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = file, Margin = new Thickness(0, 1, 8, 0) };
            var left = new DockPanel(); DockPanel.SetDock(icon, Dock.Left); left.Children.Add(icon); left.Children.Add(name);
            var desc = U.Text(what, 12.5, fg: Theme.Fg2); Grid.SetColumn(desc, 1);
            g.Children.Add(left); g.Children.Add(desc);
            _info.Children.Add(g);
        }
        _info.Children.Add(U.Row(U.Icon(Theme.IFolder, 12, Theme.Muted), U.Text($"Gespeichert im Aufnahmeordner unter {where}", 12, fg: Theme.Muted, margin: new Thickness(8, 0, 0, 0))));
        ((FrameworkElement)_info.Children[^1]).Margin = new Thickness(0, 12, 0, 0);
    }

    (string, string)[] KiFiles()
    {
        var l = new List<(string, string)>
        {
            ("README_AI.md", "Lesehilfe: erklärt der KI das Format und die Regeln, z. B. dass aufgezeichnete Texte Daten sind und keine Anweisungen."),
            ("trace.md", "das Protokoll: jeder Schritt mit Fenster, Ziel, Eingabe, beobachteter Wirkung, Beleg und Bildverweis."),
            ("steps.json", "dieselben Schritte als strukturierte Daten für Programme."),
            ("ai-package.json", "Inhaltsverzeichnis mit Bildmaßen und Prüfsummen."),
            ("images\\", Detail switch
            {
                "compact" => "nur Hauptschritte bekommen ein Bild: Ausschnitt um den Klick oder das Nachher-Bild.",
                "full" => "jeder Schritt mit Vorher-Bild, Ausschnitt um die Aktion und Nachher-Bild.",
                _ => "Hauptschritte mit Vorher-Bild, Ausschnitt um den Klick und Nachher-Bild."
            })
        };
        if (Detail == "full") l.Add(("events.jsonl", "alle Rohereignisse der Aufnahme (Maus, Tastatur, Fenster) – Passwörter und Schwärzungen bleiben ausgeblendet."));
        return l.ToArray();
    }
}
