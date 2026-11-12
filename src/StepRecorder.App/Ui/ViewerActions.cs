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

    /// <summary>Kurzer Clip (GIF) aus den gespeicherten Bildern des Schritts.</summary>
    void SaveClip()
    {
        var s = Sel; if (s == null) return;
        var ids = StepRecorder.Export.StepClip.FrameIds(_view, s);
        if (ids.Count < 2) { Msg.Show("Für diesen Schritt gibt es nur ein Bild – ein Clip lohnt sich hier nicht. Mit dem Modus „KI-Spur“ oder vielen Zusatzbildern entstehen mehr Zwischenbilder.", "Clip", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = $"Schritt-{s.Number:00}-{Fmt.FileSafe(s.AuthorTitle ?? s.Title, 30)}.gif", Filter = "Animiertes GIF|*.gif" };
        if (dlg.ShowDialog(this) != true) return;
        int n = StepRecorder.Export.StepClip.Write(_view, s, dlg.FileName);
        _status.Text = n > 0 ? $"Clip mit {n} Bildern gespeichert" : "Kein Clip möglich";
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

    /// <summary>„Exportieren …“ (Knopf, Menü, Strg+E): Exportdialog mit dem zuletzt gewählten Format.</summary>
    void ShowExport() => ShowExportDialog(StepRecorder.Settings.AppSettings.Load().LastExportFormat is { Length: > 0 } f ? f : "html");

    public void ShowExportDialog(string format)
    {
        if (_view.UnreviewedCount > 0 && format != "ki")
        {
            if (Msg.Show($"{_view.UnreviewedCount} auffällige Eingaben sind noch nicht durchgesehen. Die Anleitung kann erst nach der Durchsicht erstellt werden.\n\nJetzt prüfen?", "Durchsicht", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes) ShowReview();
            if (_view.UnreviewedCount > 0) return;
        }
        var d = new ExportDialog(format, _view.Language, StepRecorder.Export.DocSettings.From(_view.Edits.Obj("guide"))) { Owner = this };
        if (d.ShowDialog() != true) return;
        {
            var st = StepRecorder.Settings.AppSettings.Load();
            if (d.Format is "ki" or "ahk") st.ExportReadable = d.Readable;
            st.LastExportFormat = d.Format;
            st.Save();
        }
        var view = SessionView.Open(_path, headless: d.Format == "ki", lang: d.Language);
        try
        {
            ExportResult r = d.Format switch
            {
                "ki" => KiPackage.Export(view, new KiOptions { Detail = d.Detail, ImageProfile = d.Profile, Language = d.Language, Zip = d.Zip, Readable = d.Readable, Before = d.Before, Crop = d.Crop, After = d.AfterImages }),
                "ahk" when d.AhkKi => AhkKiPackage.Export(view, new AhkKiOptions { Readable = d.Readable }),
                "ahk" => AhkExport.Export(view, new AhkOptions { RealTiming = d.RealTiming, Readable = d.Readable }),
                _ => GuideExport.Export(view, new GuideOptions { Format = d.Format, Language = d.Language, Importance = d.Importance, View = d.View, Overview = d.Overview, GifSeconds = d.GifSeconds, MouseMarks = d.MarkMouse, KeyMarks = d.MarkKeys, After = d.Format == "gif" ? "never" : d.GuideAfter })
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
        w.Content = new TextBox { Text = StepCompare.Markdown(r, "de"), FontFamily = Theme.Mono, FontSize = Theme.FsHint, IsReadOnly = true, BorderThickness = new Thickness(0), Padding = new Thickness(28, 24, 28, 24), VerticalContentAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        w.Show();
    }
}

/// <summary>Einfacher Eingabedialog (Text, optional Titel und Kommentarart).</summary>
sealed class InputDialog : Window
{
    readonly TextBox _value = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top };
    readonly TextBox? _title;
    readonly RadioButton[]? _kinds;
    readonly TextBlock _titleLabel = U.Text("Titel", Theme.FsHint, true, Theme.Fg2, new Thickness(0, 0, 0, 6));
    public string Value => _value.Text;
    public string TitleValue => _title?.Text ?? "";
    public string Kind => _kinds == null ? "note" : ViewerWindow.NoteKindsUi[Math.Max(0, Array.FindIndex(_kinds, k => k.IsChecked == true))].kind;
    public string TitleLabel { set => _titleLabel.Text = value; }

    public InputDialog(string prompt, string value, string? title = null, bool kinds = false)
    {
        Title = AppInfo.DisplayName; Width = 600; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Surface);
        var p = new StackPanel();
        if (kinds)
        {
            var (track, items) = U.Segmented("kind", 0, ViewerWindow.NoteKindsUi.Select(k => ((string?)k.glyph, k.name)).ToArray());
            _kinds = items;
            items[^1].ToolTip = "Erscheint in keiner Ausgabe";
            track.Margin = new Thickness(0, 0, 0, 16);
            p.Children.Add(track);
        }
        if (title != null) { _title = new TextBox { Text = title }; p.Children.Add(new StackPanel { Margin = new Thickness(0, 0, 0, 16), Children = { _titleLabel, _title } }); }
        _value.Text = value;
        p.Children.Add(title != null ? U.Field("Text", _value, margin: new Thickness(0)) : _value);
        var hint = U.Row(U.Kbd("Strg+Eingabe"), U.Text("übernimmt", Theme.FsHint, fg: Theme.Muted, margin: new Thickness(4, 0, 0, 0)));
        ((FrameworkElement)hint.Children[1]).VerticalAlignment = VerticalAlignment.Center;
        hint.Margin = new Thickness(0, 12, 0, 0);
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
        Title = "Durchsicht der Eingaben"; Width = 800; Height = 660; MinHeight = 420; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Bg);
        var flagged = view.All.Where(s => s.Unreviewed).ToList();
        var others = view.All.Where(s => s.Kind == "type" && !s.Unreviewed && !s.Json.Obj("input")!.Bool("masked")).ToList();
        var list = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };
        list.Children.Add(U.Heading("Durchsicht der Eingaben", Theme.FsTitle, new Thickness(0, 0, 0, 4)));
        list.Children.Add(U.Text(flagged.Count == 0 ? "Keine auffälligen Eingaben. Bitte die übrigen Eingaben kurz prüfen." : $"{flagged.Count} auffällige Eingaben – bitte für jede entscheiden, ob sie bleiben darf.", Theme.FsBody, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 18)));
        var choices = new List<(ViewStep, RadioButton[])>();
        foreach (var s in flagged)
        {
            var (track, items) = U.Segmented("rv" + s.Anchor, -1, (Theme.ICheck, "In Ordnung"), (Theme.IRedact, "Schwärzen"));
            track.VerticalAlignment = VerticalAlignment.Center;
            choices.Add((s, items));
            var dock = new DockPanel();
            DockPanel.SetDock(track, Dock.Right); dock.Children.Add(track);
            var badge = U.IconBadge(Theme.IWarn, 36, Theme.WarnSoft, Theme.Warn, 16); badge.Margin = new Thickness(0, 0, 14, 0); badge.VerticalAlignment = VerticalAlignment.Top;
            DockPanel.SetDock(badge, Dock.Left); dock.Children.Add(badge);
            dock.Children.Add(U.Col(
                U.Text($"Schritt {s.Number} · {s.Json.Obj("ctx")?.Str("title")}", Theme.FsHint, fg: Theme.Muted),
                new TextBlock { Text = Steps.TextTemplates.TokensNotation(s.Json.Obj("input")!), FontFamily = Theme.Mono, FontSize = Theme.FsBody, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 12, 4) },
                U.Text("Grund: " + s.FlagReason, Theme.FsHint, fg: Theme.Warn)));
            list.Children.Add(U.Card(dock, new Thickness(18, 16, 18, 16), new Thickness(0, 0, 0, 12)));
        }
        if (others.Count > 0)
        {
            list.Children.Add(U.SectionLabel($"Unauffällige Eingaben ({others.Count})", new Thickness(0, 16, 0, 10)));
            var box = new StackPanel();
            foreach (var s in others.Take(50))
            {
                var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
                var n = U.Text($"Schritt {s.Number}", Theme.FsHint, fg: Theme.Muted); n.Width = 88; DockPanel.SetDock(n, Dock.Left); row.Children.Add(n);
                row.Children.Add(new TextBlock { Text = Steps.TextTemplates.TokensNotation(s.Json.Obj("input")!), FontFamily = Theme.Mono, FontSize = Theme.FsHint, TextWrapping = TextWrapping.Wrap });
                box.Children.Add(row);
            }
            list.Children.Add(U.Card(box, new Thickness(18, 12, 18, 12)));
        }
        var ok = U.Primary("Fertig", () =>
        {
            if (choices.Any(c => c.Item2.All(r => r.IsChecked != true))) { Msg.Show("Noch nicht alles entschieden\n\nBitte für jede auffällige Eingabe „In Ordnung“ oder „Schwärzen“ wählen.", "Durchsicht", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            foreach (var (s, items) in choices) Decisions.Add((s, items[0].IsChecked == true ? "kept" : "redacted"));
            DialogResult = true;
        });
        var cancel = U.Btn("Abbrechen", () => DialogResult = false);
        var foot = U.Footer(ok, cancel);
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
    readonly CheckBox _zip = new() { Content = "Als ZIP-Datei verpacken" };
    readonly CheckBox _timing = new() { Content = "Auch die Denkpausen der Aufnahme übernehmen", IsChecked = false, Margin = new Thickness(0, 0, 0, 10) };
    readonly CheckBox _ahkKi = new() { Content = "Als KI-Paket: Anleitung für KI, ablauf.json, Bildausschnitte, Texterkennung", IsChecked = false };
    public bool RealTiming => _timing.IsChecked == true;
    public bool AhkKi => _ahkKi.IsChecked == true;
    readonly CheckBox _readable = new() { Content = "Für Menschen lesbar – ausführlich kommentiert", IsChecked = StepRecorder.Settings.AppSettings.Load().ExportReadable };
    readonly TextBlock _readableHint = Hint();
    public bool Readable => _readable.IsChecked == true;
    int FormatIndex => Math.Max(0, Array.FindIndex(_formats, f => f.IsChecked == true));
    public string Format => FormatIndex switch { 1 => "md", 2 => "docx", 3 => "pdf", 4 => "gif", 5 => "ki", 6 => "ahk", _ => "html" };
    readonly CheckBox _overview = new() { Content = "Kleine Übersicht des ganzen Bildschirms einblenden", IsChecked = true, Margin = new Thickness(0, 8, 0, 0) };
    readonly ComboBox _gifSec = new() { Items = { "1,5 Sekunden je Schritt", "2,5 Sekunden je Schritt", "4 Sekunden je Schritt" }, SelectedIndex = 1 };
    public bool Overview => _overview.IsChecked == true;
    public double GifSeconds => _gifSec.SelectedIndex switch { 0 => 1.5, 2 => 4, _ => 2.5 };
    public string Language => _lang.SelectedIndex == 1 ? "en" : "de";
    public string Importance => _imp.SelectedIndex switch { 1 => "secondary", 2 => "all", _ => "primary" };
    public string View => _view.SelectedIndex switch { 1 => "window", 2 => "crop", _ => "screen" };
    public string Detail => _detail.SelectedIndex switch { 0 => "compact", 2 => "full", _ => "standard" };
    public string Profile => _profile.SelectedIndex switch { 1 => "highres", 2 => "original", _ => "compatible" };
    public bool Zip => _zip.IsChecked == true;
    readonly CheckBox _markMouse = new() { Content = "Mausaktionen: Klickpunkt, Zugweg, Zielrahmen, Scrollrichtung", IsChecked = true, Margin = new Thickness(0, 0, 0, 8) };
    readonly CheckBox _markKeys = new() { Content = "Tastatur: gedrückte Tasten und getippten Text als Tasten", IsChecked = true };
    public bool MarkMouse => _markMouse.IsChecked == true;
    public bool MarkKeys => _markKeys.IsChecked == true;

    // Bildauswahl: KI-Paket (Vorher/Ausschnitt/Nachher) und Anleitungen (Nachher-Bild als „Ergebnis“)
    readonly RadioButton[] _before, _guideAfter;
    readonly CheckBox _crop = new() { Content = "Ausschnitt um die Aktion", IsChecked = true, Margin = new Thickness(0, 0, 0, 8) };
    readonly CheckBox _afterImg = new() { Content = "Nachher-Bild", IsChecked = true };
    /// <summary>KI-Paket: auto | always | never.</summary>
    public string Before => Array.FindIndex(_before, r => r.IsChecked == true) switch { 1 => "always", 2 => "never", _ => "auto" };
    public bool Crop => _crop.IsChecked == true;
    public bool AfterImages => _afterImg.IsChecked == true;
    /// <summary>Anleitungen: never | changed | always.</summary>
    public string GuideAfter => Array.FindIndex(_guideAfter, r => r.IsChecked == true) switch { 1 => "changed", 2 => "always", _ => "never" };

    readonly StackPanel _info = new();
    readonly TextBlock _impHint = Hint(), _viewHint = Hint(), _detailHint = Hint(), _profileHint = Hint(), _beforeHint = Hint(), _guideAfterHint = Hint();

    static TextBlock Hint() => U.Hint("", new Thickness(0, 6, 0, 0));

    public ExportDialog(string format, string lang, StepRecorder.Export.DocSettings? doc = null)
    {
        var wa = Theme.ActiveWorkArea();
        Title = "Exportieren"; Width = Math.Min(1000, wa.Width - 24); SizeToContent = SizeToContent.Height; MaxHeight = wa.Height - 24;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Surface);
        int sel = format switch { "md" => 1, "docx" => 2, "pdf" => 3, "gif" => 4, "ki" => 5, "ahk" => 6, _ => 0 };
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 7, Margin = new Thickness(-4, 0, -4, 16) };
        if (doc != null) { _view.SelectedIndex = doc.View switch { "window" => 1, "crop" => 2, _ => 0 }; _overview.IsChecked = doc.Overview; }
        var formats = new List<RadioButton>();
        foreach (var (glyph, name, sub, i) in new[] { (Theme.IGlobe, "HTML", "eine Datei", 0), (Theme.ICode, "Markdown", "Text + Bilder", 1), (Theme.IDoc, "Word", "bearbeitbar", 2), (Theme.IPdf, "PDF", "zum Drucken", 3), (Theme.IPlay, "GIF", "animiert", 4), (Theme.IRobot, "KI-Paket", "für KI", 5), (Theme.IScript, "AutoHotkey", "Skript", 6) })
        {
            var content = U.Col(U.Icon(glyph, 20), new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, FontSize = Theme.FsHint, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = sub, FontSize = Theme.FsCaption, Opacity = 0.85, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            var rb = new RadioButton { GroupName = "fmt", Style = Theme.Style("Choice"), Content = content, IsChecked = i == sel, Padding = new Thickness(6, 12, 6, 12), ToolTip = Loc.T(name) + " – " + Loc.T(sub) };
            formats.Add(rb); grid.Children.Add(rb);
        }
        _formats = formats.ToArray();
        _lang.SelectedIndex = lang == "en" ? 1 : 0;

        // Was steckt drin?
        var infoBox = U.Inset(_info, new Thickness(18, 14, 18, 16));
        infoBox.VerticalAlignment = VerticalAlignment.Top;

        var (beforeTrack, before) = U.Segmented("before", 0, (null, "Automatisch"), (null, "Immer"), (null, "Nie"));
        _before = before;
        var (guideAfterTrack, guideAfter) = U.Segmented("gafter", 0, (null, "Nie"), (null, "Bei Änderung"), (null, "Immer"));
        _guideAfter = guideAfter;

        var options = new Grid();
        options.ColumnDefinitions.Add(new ColumnDefinition()); options.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); options.ColumnDefinitions.Add(new ColumnDefinition());
        for (int r = 0; r < 4; r++) options.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        void G(UIElement e, int r, int c, int span = 1) { Grid.SetRow(e, r); Grid.SetColumn(e, c); Grid.SetColumnSpan(e, span); options.Children.Add(e); }
        var fImp = U.Field("Umfang", U.Col(_imp, _impHint)); var fView = U.Field("Darstellung", U.Col(_view, _viewHint, _overview));
        var fDetail = U.Field("Detailstufe", U.Col(_detail, _detailHint)); var fProfile = U.Field("Bildgröße", U.Col(_profile, _profileHint));
        var fBefore = U.Field("Vorher-Bild", U.Col(beforeTrack, _beforeHint));
        var fGuideAfter = U.Field("Nachher-Bild", U.Col(guideAfterTrack, _guideAfterHint));
        var fMore = U.Field("Weitere Bilder", U.Col(_crop, _afterImg));
        var zipHost = new StackPanel { Children = { _zip }, Margin = new Thickness(0, 28, 0, 0) };
        // links die Einstellungen (zwei Spalten), rechts „Enthält“ – so bleibt der Dialog auch auf Laptops in voller Größe
        G(U.Field("Sprache", _lang), 0, 0); G(fImp, 0, 2); G(fDetail, 0, 2);
        G(fView, 1, 0); G(fProfile, 1, 0); G(zipHost, 1, 2);
        G(fGuideAfter, 2, 0, 3); G(fBefore, 2, 0, 3);
        G(fMore, 3, 0, 3);
        var gifOpts = U.Field("Ablauf", _gifSec, "Das GIF zeigt ein Titelbild und dann jeden Schritt mit Nummer und Überschrift.");
        var ahkOpts = U.Col(_timing, _ahkKi);
        ahkOpts.Margin = new Thickness(0, 0, 0, 16);
        var readableOpts = U.Col(_readable, _readableHint);
        readableOpts.Margin = new Thickness(0, 0, 0, 8);
        var marksOpts = U.Field("Im Bild markieren", U.Col(_markMouse, _markKeys), "Wie im Editor unter „Markierungen“; dort lässt sich das auch je Schritt ein- und ausschalten.");
        void Upd()
        {
            bool ki = Format == "ki", ahk = Format == "ahk", gif = Format == "gif";
            options.Visibility = ahk ? Visibility.Collapsed : Visibility.Visible;
            ahkOpts.Visibility = ahk ? Visibility.Visible : Visibility.Collapsed;
            gifOpts.Visibility = gif ? Visibility.Visible : Visibility.Collapsed;
            _overview.Visibility = _view.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            marksOpts.Visibility = ki || ahk ? Visibility.Collapsed : Visibility.Visible;
            readableOpts.Visibility = ki || ahk ? Visibility.Visible : Visibility.Collapsed;
            _readableHint.Text = ki
                ? (Readable ? "Jede Angabe – auch Systeminfos, Belegqualität, Koordinaten und Lücken – wird in Klartext erklärt, jeder Schritt bekommt einen Satz „In Worten“, dazu LIESMICH.md und Kommentare in den JSON-Dateien. Etwa ein Drittel mehr Text – für eine KI unnötig."
                            : "Aus: knapp und tokensparend für KI-Assistenten. Einschalten, wenn Menschen das Paket lesen sollen (Helpdesk, Entwickler, Prüfung).")
                : (Readable ? "Das Skript erklärt seinen Aufbau, die Begriffe (WinTitle, ClassNN …) und bei jedem Schritt in Worten, was passiert."
                            : "Aus: nur die technischen Kommentare. Einschalten, wenn jemand das Skript verstehen oder anpassen soll.");
            fImp.Visibility = fView.Visibility = ki ? Visibility.Collapsed : Visibility.Visible;
            fDetail.Visibility = fProfile.Visibility = zipHost.Visibility = fBefore.Visibility = fMore.Visibility = ki ? Visibility.Visible : Visibility.Collapsed;
            fGuideAfter.Visibility = !ki && !gif ? Visibility.Visible : Visibility.Collapsed;
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
            _beforeHint.Text = Before switch
            {
                "always" => "Jeder Mausschritt bekommt eine eigene Übersicht vor der Aktion – auch wenn sie dem Nachher-Bild des vorigen Schritts gleicht.",
                "never" => "Keine Übersichten vor der Aktion. Klickpunkt und Zielrahmen zeigt weiterhin der Ausschnitt.",
                _ => "Nur wenn der Bildschirm davor nicht schon im Paket zu sehen ist – sonst verweist das Protokoll auf das frühere Bild."
            };
            _guideAfterHint.Text = GuideAfter switch
            {
                "changed" => "Zusätzlich das Bild nach dem Schritt („Ergebnis“), wenn sich mindestens 2 % des Bildschirms geändert haben oder ein Fenster auf- oder zuging.",
                "always" => "Zusätzlich das Bild nach jedem Schritt („Ergebnis“), sofern eines vorliegt.",
                _ => "Ein Bild je Schritt – wie im Editor."
            };
            FillInfo();
        }
        foreach (var f in _formats) f.Checked += (_, _) => Upd();
        foreach (var r in _before.Concat(_guideAfter)) r.Checked += (_, _) => Upd();
        foreach (var cb in new[] { _imp, _view, _detail, _profile, _gifSec }) cb.SelectionChanged += (_, _) => Upd();
        foreach (var chk in new[] { _zip, _ahkKi, _readable, _crop, _afterImg }) { chk.Checked += (_, _) => Upd(); chk.Unchecked += (_, _) => Upd(); }
        Upd();
        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = U.Col(options, gifOpts, marksOpts, ahkOpts, readableOpts);
        Grid.SetColumn(infoBox, 2);
        columns.Children.Add(left); columns.Children.Add(infoBox);
        U.Dialog(this, "Exportieren", U.Col(grid, columns), U.Primary("Exportieren", () => DialogResult = true, Theme.IExport), U.Btn("Abbrechen", () => DialogResult = false));
        // passt immer ganz auf den Bildschirm: bei wenig Höhe gleichmäßig verkleinern statt abschneiden
        U.FitDialog(this);
    }

    /// <summary>Beschreibt das gewählte Format: wofür, welche Dateien mit welchem Inhalt, wo gespeichert.</summary>
    void FillInfo()
    {
        _info.Children.Clear();
        string imp = _imp.SelectedIndex switch { 1 => "Haupt- und Nebenschritte", 2 => "alle Schritte", _ => "alle Hauptschritte" };
        string guideContent = $"Titel, Einleitung, Voraussetzungen, Kapitel und {imp} – jeweils mit Überschrift, Anleitungstext, Bild und Hinweiskästen; Deckblatt, Inhaltsverzeichnis, Bildgröße und Farbe stellen Sie im Editor im Reiter „Dokument“ ein";
        string mdImages = GuideAfter == "never" ? "ein PNG je Schritt (schritt-001.png …), in der .md-Datei verlinkt." : "ein PNG je Schritt (schritt-001.png …), dazu das Ergebnis (schritt-001-nachher.png), in der .md-Datei verlinkt.";
        (string title, string use, (string file, string what)[] files, string where) = Format switch
        {
            "md" => ("Markdown – Text und ein Bilderordner", "Für Wikis, Git-Repositories, Confluence oder Notiz-Apps wie Obsidian.",
                new[] { ("<Titel>.md", guideContent + " (als Markdown-Text)."), ("bilder\\", mdImages) }, "export\\markdown"),
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
        _info.Children.Add(U.Text(title, Theme.FsSub, true, margin: new Thickness(0, 0, 0, 4)));
        _info.Children.Add(U.Para(use, Theme.FsBody, Theme.Fg2, new Thickness(0, 0, 0, 12)));
        _info.Children.Add(U.SectionLabel("Enthält", new Thickness(0, 0, 0, 6)));
        foreach (var (file, what) in files)
        {
            // Dateiname über der Beschreibung – passt auch in die schmale rechte Spalte
            var g = new DockPanel { Margin = new Thickness(0, 6, 0, 2) };
            var icon = U.Icon(file.EndsWith('\\') ? Theme.IFolder : Theme.IDoc, 14, Theme.AccentText); icon.VerticalAlignment = VerticalAlignment.Top; icon.Margin = new Thickness(0, 2, 10, 0);
            var name = new TextBlock { Text = file, FontFamily = Theme.Mono, FontSize = Theme.FsCaption, FontWeight = FontWeights.SemiBold, Foreground = Theme.Fg, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = file, Margin = new Thickness(0, 2, 0, 2) };
            var desc = U.Para(what, Theme.FsHint, Theme.Fg2);
            DockPanel.SetDock(icon, Dock.Left); g.Children.Add(icon); g.Children.Add(U.Col(name, desc));
            _info.Children.Add(g);
        }
        var saved = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        var savedIcon = U.Icon(Theme.IFolder, 14, Theme.Muted); savedIcon.VerticalAlignment = VerticalAlignment.Top; savedIcon.Margin = new Thickness(0, 2, 10, 0);
        DockPanel.SetDock(savedIcon, Dock.Left); saved.Children.Add(savedIcon);
        saved.Children.Add(U.Hint($"Gespeichert im Aufnahmeordner unter {where}"));
        _info.Children.Add(saved);
    }

    (string, string)[] KiFiles()
    {
        // zusammengesetzter Text: bei englischer Oberfläche gleich englisch bauen (die Übersetzungstabelle kennt nur ganze Sätze)
        bool en = Loc.En;
        var kinds = new List<string>();
        if (Before != "never") kinds.Add(Before == "auto" ? (en ? "before image where the package does not already show that screen" : "Vorher-Bild, wo es nicht schon im Paket steckt") : (en ? "before image" : "Vorher-Bild"));
        if (Crop) kinds.Add(Detail == "full" ? (en ? "close-up around the action" : "Ausschnitt um die Aktion") : (en ? "close-up around the click" : "Ausschnitt um den Klick"));
        if (AfterImages) kinds.Add(en ? "after image" : "Nachher-Bild");
        string which = kinds.Count == 0 ? (en ? "no images (only images for markers)." : "keine Bilder (nur Bilder zu Markern).")
            : string.Join(", ", kinds.Take(kinds.Count - 1)) + (kinds.Count > 1 ? (en ? " and " : " und ") : "") + kinds[^1] + ".";
        var l = new List<(string, string)>
        {
            ("README_AI.md", "Lesehilfe: erklärt der KI das Format und die Regeln, z. B. dass aufgezeichnete Texte Daten sind und keine Anweisungen."),
            ("trace.md", "das Protokoll: jeder Schritt mit Fenster, Ziel, Eingabe, beobachteter Wirkung, Beleg und Bildverweis."),
            ("steps.json", "dieselben Schritte als strukturierte Daten für Programme."),
            ("ai-package.json", "Inhaltsverzeichnis mit Bildmaßen und Prüfsummen."),
            ("images\\", Detail switch
            {
                "compact" => "nur Hauptschritte bekommen ein Bild: Ausschnitt um den Klick oder das Nachher-Bild.",
                "full" => (en ? "every step with " : "jeder Schritt mit ") + which,
                _ => (en ? "main steps with " : "Hauptschritte mit ") + which
            })
        };
        if (Readable) { l.Insert(0, ("LIESMICH.md", "für Menschen: was im Paket steckt, wofür jede Datei ist und wie man es nutzt.")); l[2] = ("trace.md", "das Protokoll mit Erklärungen: jede Angabe in Klartext erläutert, jeder Schritt zusätzlich „In Worten“."); }
        if (Detail == "full") l.Add(("events.jsonl", "alle Rohereignisse der Aufnahme (Maus, Tastatur, Fenster) – Passwörter und Schwärzungen bleiben ausgeblendet."));
        return l.ToArray();
    }
}
