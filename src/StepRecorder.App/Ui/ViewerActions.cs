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
                U.Text("Grund: " + Patterns.KindName(s.FlagReason ?? "", "de"), 12, fg: Theme.Warn)));
            list.Children.Add(U.Card(dock, new Thickness(16, 14, 16, 14), new Thickness(0, 0, 0, 10)));
        }
        // Viele Terminal-Befehle: übrige Klartext-Eingaben gesammelt freigeben (Musterfunde brauchen weiter eine Einzelentscheidung)
        var plain = choices.Where(c => c.Item1.FlagReason == "plaintextWindow").ToList();
        if (plain.Count > 1)
        {
            var all = U.Btn($"Übrige Klartext-Eingaben ({plain.Count}): in Ordnung", () => { foreach (var (_, items) in plain) if (items.All(r => r.IsChecked != true)) items[0].IsChecked = true; });
            all.HorizontalAlignment = HorizontalAlignment.Left; all.Margin = new Thickness(0, 0, 0, 12);
            all.ToolTip = "Setzt alle noch offenen Eingaben aus Terminal-, Remote-Desktop- und VM-Fenstern auf „In Ordnung“. Auffällige Muster (Schlüssel, IBAN …) bleiben einzeln zu entscheiden.";
            list.Children.Insert(2, all);
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
