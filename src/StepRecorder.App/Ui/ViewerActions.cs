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
using StepRecorder.Settings;

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
        Msg.Show(Loc.L("Die Zwischenablage ist gerade gesperrt.", "The clipboard is currently locked."));
    }

    bool ConfirmUnreviewed()
    {
        if (_view.UnreviewedCount == 0 || _askedUnreviewed) return true;
        _askedUnreviewed = true;
        return Msg.Show(Loc.N(_view.UnreviewedCount, "1 auffällige Eingabe ist noch nicht durchgesehen. Trotzdem Text kopieren?", "{0} auffällige Eingaben sind noch nicht durchgesehen. Trotzdem Text kopieren?", "1 conspicuous input has not been reviewed yet. Copy the text anyway?", "{0} conspicuous inputs have not been reviewed yet. Copy the text anyway?"), Loc.L("Durchsicht", "Review"),MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
    bool _askedUnreviewed;

    void CopyImage()
    {
        if (_current == null) return;
        var d = new DataObject();
        d.SetImage(_current);
        if (CurrentPng() is { } png) d.SetData("PNG", new MemoryStream(png));
        SetClipboard(d, Loc.F("Bild kopiert · {0} × {1}", "Image copied · {0} × {1}", _current.PixelWidth, _current.PixelHeight));
    }

    void CopyOriginal()
    {
        var s = Sel; if (s?.PreFrame == null) return;
        using var bmp = _view.Frame(_showAfter ? s.PostFrame ?? s.PreFrame : s.PreFrame);
        if (bmp == null) return;
        var src = U.ToSource(bmp);
        var d = new DataObject(); d.SetImage(src);
        SetClipboard(d, Loc.F("Originalbild kopiert · {0} × {1}", "Original image copied · {0} × {1}", src.PixelWidth, src.PixelHeight));
    }

    string StepText(ViewStep s) => $"{s.Number}. {s.AuthorTitle ?? s.Title}\n{s.AuthorBody ?? s.Guide}" + string.Concat(s.Comments.Select(c => (_view.Language == "en" ? "\nNote: " : "\nHinweis: ") + c.Str("text")));

    void CopyText()
    {
        var s = Sel; if (s == null || !ConfirmUnreviewed()) return;
        var d = new DataObject(); d.SetText(StepText(s));
        SetClipboard(d, Loc.L("Text kopiert", "Text copied"));
    }

    void CopyImageWithText()
    {
        var s = Sel; if (s == null || !ConfirmUnreviewed()) return;
        var png = CurrentPng();
        var html = new StringBuilder($"<p><b>{Fmt.HtmlEscape(s.Number + ". " + (s.AuthorTitle ?? s.Title))}</b><br>{Fmt.HtmlEscape(s.AuthorBody ?? s.Guide)}</p>");
        string noteLabel = Loc.For(_view.Language, "Hinweis:", "Note:");
        foreach (var c in s.Comments) html.Append($"<p><i>{noteLabel}</i> {Fmt.HtmlEscape(c.Str("text"))}</p>");
        if (png != null) html.Append($"<p><img width=\"{Math.Min(_current!.PixelWidth, 605)}\" src=\"data:image/png;base64,{Convert.ToBase64String(png)}\"></p>");
        var d = new DataObject();
        d.SetData(DataFormats.Html, HtmlClipboard(html.ToString()));
        d.SetText(StepText(s));
        if (_current != null) d.SetImage(_current);
        SetClipboard(d, Loc.L("Bild mit Text kopiert", "Image with text copied"));
    }

    void CopyMarkdown()
    {
        var s = Sel; if (s == null || !ConfirmUnreviewed()) return;
        // ohne Bildverweis: das Bild käme beim Kopieren nicht mit (Paket 18)
        var d = new DataObject();
        d.SetText(GuideExport.StepMarkdown(_view, s, MdDialect()));
        SetClipboard(d, Loc.L("Als Markdown kopiert (ohne Bild – „Bild kopieren“ legt es zusätzlich in die Zwischenablage)", "Copied as Markdown (without image – \"Copy image\" puts it on the clipboard as well)"));
    }

    /// <summary>Alle Schritte als Markdown-Text (Paket 18) – für Pull Requests, Issues und Wikis; Bilder über den Markdown-Export.</summary>
    void CopyAllMarkdown()
    {
        if (!ConfirmUnreviewed()) return;
        using var v = SessionView.Open(_path, headless: true, lang: _view.Language);
        var d = new DataObject();
        d.SetText(GuideExport.MarkdownForClipboard(v, _view.Language, MdDialect()));
        SetClipboard(d, Loc.L("Alle Schritte als Markdown kopiert", "All steps copied as Markdown"));
    }

    /// <summary>Dialekt aus den gemerkten Markdown-Exportoptionen (Standard: GitHub).</summary>
    static string MdDialect()
        => AppState.Load().Export.Formats.TryGetValue("md", out var o) && o.Str("mdDialect") is "plain" or "github" or "obsidian" ? o.Str("mdDialect")! : "github";

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
            SetClipboard(d, Loc.L("Ganze Anleitung kopiert", "Whole guide copied"));
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
        if (ids.Count < 2) { Msg.Show(Loc.L("Für diesen Schritt gibt es nur ein Bild – ein Clip lohnt sich hier nicht. Mit dem Modus „KI-Spur“ oder vielen Zusatzbildern entstehen mehr Zwischenbilder.", "There is only one image for this step – a clip is not worthwhile here. The \"AI trace\" mode or many extra images produce more intermediate images."), "Clip", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = Loc.For(_view.Language, "Schritt", "Step") + $"-{s.Number:00}-{Fmt.FileSafe(s.AuthorTitle ?? s.Title, 30)}.gif", Filter = Loc.L("Animiertes GIF", "Animated GIF") + "|*.gif" };
        if (dlg.ShowDialog(this) != true) return;
        int n = StepRecorder.Export.StepClip.Write(_view, s, dlg.FileName);
        _status.Text = n > 0 ? Loc.F("Clip mit {0} Bildern gespeichert", "Clip with {0} images saved", n) : Loc.L("Kein Clip möglich", "No clip possible");
    }

    void SaveImage()
    {
        var s = Sel; if (s == null) return;
        var png = CurrentPng(); if (png == null) return;
        string stepWord = Loc.For(_view.Language, "Schritt", "Step");
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = $"{stepWord}-{s.Number:00}-{Fmt.FileSafe(s.AuthorTitle ?? s.Title, 30)}.png", Filter = "PNG|*.png" };
        if (dlg.ShowDialog(this) == true) File.WriteAllBytes(dlg.FileName, png);
    }

    // =====================================================================================
    // Export, Durchsicht, Support-Paket, Übersicht, Vergleich
    // =====================================================================================

    public void StartSupportFlow()
    {
        Msg.Show(Loc.L("Support-Paket in drei Schritten:\n1. Aufnahme ansehen\n2. Eingaben durchsehen\n3. Paket erstellen\n\nDas Programm versendet nichts.", "Support package in three steps:\n1. View the recording\n2. Review the inputs\n3. Create the package\n\nThe program does not send anything."), Loc.L("Support-Paket", "Support package"),MessageBoxButton.OK, MessageBoxImage.Information);
        ShowReview(forceAll: true);
        if (_view.UnreviewedCount > 0) return;
        var view = SessionView.Open(_path);
        try
        {
            var r = GuideExport.SupportPackage(view);
            if (Msg.Show(Loc.F("Support-Paket erstellt:\n{0}\n\nGeben Sie das ZIP selbst an den Helpdesk weiter. Im Explorer zeigen?", "Support package created:\n{0}\n\nPass the ZIP on to the help desk yourself. Show in Explorer?", r.Path), Loc.L("Support-Paket", "Support package"),MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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
            // Prüfposten außerhalb getippter Eingaben (Programmausgaben, auffällige Texte des Autors – Paket 12)
            foreach (var (item, status) in w.ItemDecisions)
            {
                inputs.Add(new JsonObject { ["kind"] = item.Kind, ["key"] = item.Key, ["status"] = status, ["flagged"] = new JsonArray(item.FlagReason ?? item.Source), ["atUtc"] = Json.UtcNow() });
                if (status != "redacted") continue;
                if (item.Key.StartsWith("seq:") && item.Seq > 0)
                    redactions.Add(new JsonObject { ["id"] = NextId(e, "redactions", "r") + "_i" + item.Seq, ["kind"] = "range", ["origin"] = "review", ["seqRanges"] = new JsonArray(new JsonArray(item.Seq, item.Seq)), ["images"] = false, ["createdUtc"] = Json.UtcNow(), ["by"] = "user" });
                else if (item.Key.StartsWith("comment:") && e["comments"] is JsonArray cs)
                    foreach (var c in cs.OfType<JsonObject>().Where(c => "comment:" + c.Str("id") == item.Key)) c["kind"] = "internal";
                else if (item.Key.StartsWith("goal:"))
                {
                    var g = e["goal"] as JsonObject ?? new JsonObject();
                    g[item.Key[5..]] = "";
                    e["goal"] = g;
                }
            }
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
        sb.AppendLine(Loc.F("Titel: {0}", "Title: {0}", _view.Title));
        sb.AppendLine(Loc.F("Aufgenommen: {0} · Dauer {1}", "Recorded: {0} · Duration {1}", Loc.DateAndTime(_view.Session.StartUtc.ToLocalTime()), Fmt.Time(_view.Session.DurationUs, Loc.Lang)));
        sb.AppendLine(Loc.F("Zustand: {0} · Profil: {1} · Schutzstufe: {2}", "State: {0} · Profile: {1} · Protection level: {2}", _view.Session.State, m.Obj("profile").Str("name"), m.Obj("settings").Obj("privacy").Str("level")));
        if (_view.Goal() is { } g) sb.AppendLine(Loc.F("Ziel: {0} · Erwartet: {1} · Bezug: {2}", "Goal: {0} · Expected: {1} · Reference: {2}", g.Str("description"), g.Str("expected"), g.Str("reference")));
        sb.AppendLine();
        sb.AppendLine(Loc.L("Vollständigkeitsbericht", "Completeness report"));
        sb.AppendLine(Loc.F("  Buchführung: {0} Eingabeereignisse, {1} zugeordnet, {2} ohne Schritt", "  Bookkeeping: {0} input events, {1} assigned, {2} without a step", bk.Long("inputEvents"), bk.Long("assigned"), bk.Long("orphans")));
        sb.AppendLine(Loc.F("  Belegqualität: {0} Aktionsschritte · {1} belegt · {2} nur Eingabe · {3} unbekannt", "  Evidence quality: {0} action steps · {1} confirmed · {2} input only · {3} unknown", ev.Long("actionSteps"), ev.Long("confirmed"), ev.Long("inputOnly"), ev.Long("unknown")));
        sb.AppendLine(Loc.F("  Lücken: {0} ({1})", "  Gaps: {0} ({1})", c.Obj("gaps").Long("count"), Fmt.Dur(c.Obj("gaps").Long("totalUs"), Loc.Lang)));
        sb.AppendLine(Loc.F("  Bilder: {0} angefordert · {1} gespeichert · {2} unverändert · Vorher-Bild garantiert {3}/{4}", "  Images: {0} requested · {1} stored · {2} unchanged · Before image guaranteed {3}/{4}", fr.Long("requested"), fr.Long("stored"), fr.Long("same"), fr.Long("preGuaranteed"), fr.Long("preGuaranteed") + fr.Long("preNotGuaranteed")));
        foreach (var gp in _view.Steps.Arr("gaps") ?? new JsonArray())
            sb.AppendLine($"  – {Fmt.Time(gp.Long("tFrom"), Loc.Lang)}: {Steps.TextTemplates.CauseText(gp.Str("cause") ?? "", Loc.Lang)}");
        var (waitMs, thinkMs, slow) = ReadLayer.Timing.Totals(_view.All.Where(s => !s.Redacted && s.Importance != "trace").Select(s => s.Json));
        if (waitMs + thinkMs > 0)
        {
            bool de = Loc.Lang == "de";
            sb.AppendLine(Loc.F("  Reaktionszeiten: Anwendung wartete {0} · Denkzeit {1} · {2} langsame Schritte (ab {3})", "  Response times: application waited {0} · thinking time {1} · {2} slow steps (from {3})",
                ReadLayer.Timing.Ms(waitMs, de), ReadLayer.Timing.Ms(thinkMs, de), slow, ReadLayer.Timing.Ms(ReadLayer.Timing.SlowMs, de)));
        }
        sb.AppendLine();
        long bytes = 0; try { bytes = new DirectoryInfo(_view.Session.Path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); } catch { }
        sb.AppendLine(Loc.F("Speicher: {0:0.0} MB · {1}", "Storage: {0:0.0} MB · {1}", bytes / 1024.0 / 1024.0, _view.Session.Path));
        var r = Msg.Show(sb + Loc.L("\n\nOrdner im Explorer öffnen?", "\n\nOpen folder in Explorer?"), Loc.L("Sitzungsübersicht", "Recording overview"),MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (r == MessageBoxResult.Yes) Process.Start("explorer.exe", $"\"{_view.Session.Path}\"");
    }

    void Compare()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = Loc.L("Zweite Aufnahme wählen (manifest.json)", "Choose second recording (manifest.json)"), Filter = Loc.L("Sitzung", "Recording") + "|manifest.json", InitialDirectory = Path.GetDirectoryName(_view.Session.Path) };
        if (dlg.ShowDialog(this) != true) return;
        // Paarliste, Bilder nebeneinander, Bildunterschiede (08 §5.9, Paket 19)
        new CompareWindow(_path, Path.GetDirectoryName(dlg.FileName)!) { Owner = this }.Show();
    }

    /// <summary>Zuletzt gewähltes Ziel des Formats „UI-Test“ (sonst FlaUI).</summary>
    static string LastTestFormat()
    {
        var last = AppState.Load().Export.LastFormat;
        return last is "flaui" or "pywinauto" or "playwright" or "test-ki" ? last : "flaui";
    }

    /// <summary>Testbarkeits-Bericht (Paket 16): Bedienelemente ohne AutomationId oder eindeutigen Namen.</summary>
    void ShowTestability()
    {
        using var v = SessionView.Open(_path, headless: true, lang: _view.Language);
        var findings = TestCodeGen.Testability(v, Loc.Lang);
        var w = new DialogWindow { Title = Loc.F("Testbarkeit · {0} Befunde", "Testability · {0} findings", findings.Count), Width = 960, Height = 620, Owner = this };
        Theme.Prepare(w, Theme.Surface);
        var list = new ListBox { BorderThickness = new Thickness(0), Background = Theme.Surface, Margin = new Thickness(16, 12, 16, 16) };
        System.Windows.Automation.AutomationProperties.SetName(list, Loc.L("Befunde", "Findings"));
        if (findings.Count == 0)
            list.Items.Add(new ListBoxItem { Content = U.Text(Loc.L("Alle benutzten Bedienelemente haben eine AutomationId oder einen eindeutigen Namen.", "All controls used have an AutomationId or a unique name."), 13, fg: Theme.Fg2), IsEnabled = false });
        foreach (var f in findings)
        {
            string head = Loc.F("Schritt {0} · {1} „{2}“", "Step {0} · {1} \"{2}\"", f.Int("step"), f.Str("controlType") ?? "?", f.Str("name") ?? "–");
            var col = U.Col(U.Text(head, 13, true, fg: f.Str("severity") == "warning" ? Theme.Warn : Theme.Fg),
                            U.Text(string.Join(", ", f.StrList("issues")) + (f.Str("process") is { } p ? " · " + p : ""), 11.5, fg: Theme.Muted),
                            U.Text(f.Str("advice") ?? "", 12, fg: Theme.Fg2));
            col.Margin = new Thickness(4, 6, 4, 6);
            list.Items.Add(new ListBoxItem { Content = col, Tag = f.Int("step") });
        }
        list.MouseDoubleClick += (_, _) =>
        {
            if (list.SelectedItem is ListBoxItem { Tag: int n } && _view.All.FirstOrDefault(s => s.Number == n) is { } st) { JumpTo(st.Anchor, null); Activate(); }
        };
        var copy = U.Btn(Theme.ICopy, Loc.L("Als Markdown kopieren", "Copy as Markdown"), () =>
        {
            var d = new DataObject(); d.SetText(TestCodeGen.TestabilityMarkdown(v, findings, Loc.Lang));
            SetClipboard(d, Loc.L("Testbarkeits-Bericht kopiert", "Testability report copied"));
        }, null, "Ghost");
        copy.Margin = new Thickness(16, 12, 16, 0); copy.HorizontalAlignment = HorizontalAlignment.Left;
        var hint = U.Text(Loc.F("Doppelklick springt zum Schritt. Für die CI: {0} testability <Aufnahme> --strict", "Double-click jumps to the step. For CI: {0} testability <recording> --strict", AppInfo.ExeName), 11.5, fg: Theme.Muted, margin: new Thickness(16, 6, 16, 0));
        var dock = new DockPanel();
        DockPanel.SetDock(copy, Dock.Top); DockPanel.SetDock(hint, Dock.Top);
        dock.Children.Add(copy); dock.Children.Add(hint); dock.Children.Add(list);
        w.Content = dock;
        w.ShowDialog();
    }
}

/// <summary>Einfacher Eingabedialog (Text, optional Titel und Kommentarart).</summary>
sealed class InputDialog : DialogWindow
{
    readonly TextBox _value = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top };
    readonly TextBox? _title;
    readonly RadioButton[]? _kinds;
    readonly TextBlock _titleLabel = U.Text(Loc.L("Titel", "Title"), Theme.FsHint, true, Theme.Fg2, new Thickness(0, 0, 0, 6));
    public string Value => _value.Text;
    public string TitleValue => _title?.Text ?? "";
    public string Kind => _kinds == null ? "note" : ViewerWindow.NoteKindsUi[Math.Max(0, Array.FindIndex(_kinds, k => k.IsChecked == true))].kind;
    public string TitleLabel { set => _titleLabel.Text = value; }
    TextBox _lastBox;

    public InputDialog(string prompt, string value, string? title = null, bool kinds = false)
    {
        Title = AppInfo.DisplayName; Width = 600; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Surface);
        _lastBox = _value;
        var p = new StackPanel();
        if (kinds)
        {
            var (track, items) = U.Segmented("kind", 0, ViewerWindow.NoteKindsUi.Select(k => ((string?)k.glyph, k.name)).ToArray());
            _kinds = items;
            items[^1].ToolTip = Loc.L("Erscheint in keiner Ausgabe", "Does not appear in any output");
            track.Margin = new Thickness(0, 0, 0, 16);
            p.Children.Add(track);
        }
        if (title != null) { _title = new TextBox { Text = title }; p.Children.Add(new StackPanel { Margin = new Thickness(0, 0, 0, 16), Children = { _titleLabel, _title } }); }
        _value.Text = value;
        p.Children.Add(title != null ? U.Field(Loc.L("Text", "Text"), _value, margin: new Thickness(0)) : _value);
        var hint = U.Row(U.Kbd(Loc.Keys("Ctrl+Enter")), U.Text(Loc.L("übernimmt", "applies"), Theme.FsHint, fg: Theme.Muted, margin: new Thickness(4, 0, 0, 0)));
        ((FrameworkElement)hint.Children[1]).VerticalAlignment = VerticalAlignment.Center;
        // Textbausteine: in das zuletzt bearbeitete Feld (Titel oder Text)
        _value.GotKeyboardFocus += (_, _) => _lastBox = _value;
        if (_title != null) _title.GotKeyboardFocus += (_, _) => _lastBox = _title;
        var snippet = SnippetUi.InsertButton(this, () => _lastBox);
        snippet.VerticalAlignment = VerticalAlignment.Center;
        var hintRow = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(snippet, Dock.Right); hintRow.Children.Add(snippet); hintRow.Children.Add(hint);
        p.Children.Add(hintRow);
        U.Dialog(this, prompt, p, U.Primary(Loc.L("Übernehmen", "Apply"), () => DialogResult = true), U.Btn(Loc.L("Abbrechen", "Cancel"), () => DialogResult = false));
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == SnippetUi.ShortcutKey && System.Windows.Input.Keyboard.Modifiers == SnippetUi.ShortcutMods) { SnippetUi.ShowMenu(this, _lastBox, () => _lastBox, null); e.Handled = true; return; }
            if (e.Key == System.Windows.Input.Key.Enter && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control) { DialogResult = true; e.Handled = true; }
            if (e.Key == System.Windows.Input.Key.Escape) DialogResult = false;
        };
        Loaded += (_, _) => { var f = _title ?? _value; f.Focus(); f.CaretIndex = f.Text.Length; };
    }
}

/// <summary>Durchsicht der Eingaben: jede auffällige Eingabe braucht eine eigene Entscheidung.</summary>
sealed class ReviewWindow : DialogWindow
{
    public List<(ViewStep step, string status)> Decisions { get; } = new();
    public List<(ReviewItem item, string status)> ItemDecisions { get; } = new();

    public ReviewWindow(SessionView view)
    {
        Title = Loc.L("Durchsicht der Eingaben", "Input review"); Width = 800; Height = 660; MinHeight = 420; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Bg);
        var flagged = view.AllWithInactive.Where(s => s.Unreviewed).ToList();
        var others = view.AllWithInactive.Where(s => s.Kind == "type" && !s.Unreviewed && !s.Json.Obj("input")!.Bool("masked")).ToList();
        var list = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };
        list.Children.Add(U.Heading(Loc.L("Durchsicht der Eingaben", "Input review"), Theme.FsTitle, new Thickness(0, 0, 0, 4)));
        list.Children.Add(U.Text(flagged.Count == 0 ? Loc.L("Keine auffälligen Eingaben. Bitte die übrigen Eingaben kurz prüfen.", "No conspicuous inputs. Please briefly check the remaining inputs.") : Loc.N(flagged.Count, "1 auffällige Eingabe – bitte entscheiden, ob sie bleiben darf.", "{0} auffällige Eingaben – bitte für jede entscheiden, ob sie bleiben darf.", "1 conspicuous input – please decide whether it may stay.", "{0} conspicuous inputs – please decide for each whether it may stay."), Theme.FsBody, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 18)));
        var choices = new List<(ViewStep, RadioButton[])>();
        foreach (var s in flagged)
        {
            var (track, items) = U.Segmented("rv" + s.Anchor, -1, (Theme.ICheck, Loc.L("In Ordnung", "OK")), (Theme.IRedact, Loc.L("Schwärzen", "Redact")));
            track.VerticalAlignment = VerticalAlignment.Center;
            choices.Add((s, items));
            var dock = new DockPanel();
            DockPanel.SetDock(track, Dock.Right); dock.Children.Add(track);
            var badge = U.IconBadge(Theme.IWarn, 36, Theme.WarnSoft, Theme.Warn, 16); badge.Margin = new Thickness(0, 0, 14, 0); badge.VerticalAlignment = VerticalAlignment.Top;
            DockPanel.SetDock(badge, Dock.Left); dock.Children.Add(badge);
            dock.Children.Add(U.Col(
                U.Text(Loc.F("Schritt {0} · {1}", "Step {0} · {1}", s.Number, s.Json.Obj("ctx")?.Str("title")), Theme.FsHint, fg: Theme.Muted),
                new TextBlock { Text = Steps.TextTemplates.TokensNotation(s.Json.Obj("input")!), FontFamily = Theme.Mono, FontSize = Theme.FsBody, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 12, 4) },
                U.Text(Loc.L("Grund: ", "Reason: ") + Patterns.KindName(s.FlagReason ?? "", Loc.Lang), Theme.FsHint, fg: Theme.Warn)));
            list.Children.Add(U.Card(dock, new Thickness(18, 16, 18, 16), new Thickness(0, 0, 0, 12)));
        }
        // Programmausgaben und auffällige Texte des Autors (Paket 12)
        var itemChoices = new List<(ReviewItem, RadioButton[])>();
        var openItems = view.ReviewItems.Where(i => i.Required && !i.Reviewed).ToList();
        if (openItems.Count > 0)
            list.Children.Add(U.SectionLabel(Loc.F("Mitgelesene Inhalte und Texte ({0})", "Content read along and texts ({0})", openItems.Count), new Thickness(0, 6, 0, 10)));
        foreach (var it in openItems)
        {
            var (track, items) = U.Segmented("rvi" + it.Key.Replace(':', '_'), -1, (Theme.ICheck, Loc.L("In Ordnung", "OK")), (Theme.IRedact, Loc.L("Schwärzen", "Redact")));
            track.VerticalAlignment = VerticalAlignment.Center;
            itemChoices.Add((it, items));
            var dock = new DockPanel();
            DockPanel.SetDock(track, Dock.Right); dock.Children.Add(track);
            string source = it.Source switch
            {
                "eventlog.entry" => Loc.L("Fehlertext der Ereignisanzeige", "Event log error text"), "log.lines" => Loc.L("Protokollzeilen", "Log lines"), "clipboard" => Loc.L("Inhalt der Zwischenablage", "Clipboard content"),
                "comment" => Loc.L("Kommentar aus der Aufnahme", "Comment from the recording"), "marker" => Loc.L("Bezeichnung eines Markers", "Marker label"), "note" => Loc.L("Hinweis", "Note"), "goal" => Loc.L("Ziel der Aufnahme", "Goal of the recording"), _ => it.Source
            };
            string preview = it.Text.Length > 600 ? it.Text[..600] + "…" : it.Text;
            dock.Children.Add(U.Col(
                U.Text(source, Theme.FsHint, fg: Theme.Muted),
                new TextBlock { Text = preview, FontFamily = Theme.Mono, FontSize = Theme.FsHint, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 12, 4), MaxHeight = 220 },
                U.Text(it.FlagReason != null ? Loc.L("Grund: ", "Reason: ") + Patterns.KindName(it.FlagReason, Loc.Lang) : Loc.L("Mitgelesener Inhalt – vor der Weitergabe bestätigen", "Content read along – confirm before sharing"), Theme.FsHint, fg: Theme.Warn)));
            list.Children.Add(U.Card(dock, new Thickness(18, 16, 18, 16), new Thickness(0, 0, 0, 12)));
        }
        // Viele Terminal-Befehle: übrige Klartext-Eingaben gesammelt freigeben (Musterfunde brauchen weiter eine Einzelentscheidung)
        var plain = choices.Where(c => c.Item1.FlagReason == "plaintextWindow").ToList();
        if (plain.Count > 1)
        {
            var all = U.Btn(Loc.F("Übrige Klartext-Eingaben ({0}): in Ordnung", "Remaining plain-text inputs ({0}): OK", plain.Count), () => { foreach (var (_, items) in plain) if (items.All(r => r.IsChecked != true)) items[0].IsChecked = true; });
            all.HorizontalAlignment = HorizontalAlignment.Left; all.Margin = new Thickness(0, 0, 0, 12);
            all.ToolTip = Loc.L("Setzt alle noch offenen Eingaben aus Terminal-, Remote-Desktop- und VM-Fenstern auf „In Ordnung“. Auffällige Muster (Schlüssel, IBAN …) bleiben einzeln zu entscheiden.", "Sets all still-open inputs from terminal, remote desktop and VM windows to “OK”. Conspicuous patterns (keys, IBAN …) still have to be decided individually.");
            list.Children.Insert(2, all);
        }
        if (others.Count > 0)
        {
            list.Children.Add(U.SectionLabel(Loc.F("Unauffällige Eingaben ({0})", "Inconspicuous inputs ({0})", others.Count), new Thickness(0, 16, 0, 10)));
            var box = new StackPanel();
            foreach (var s in others.Take(50))
            {
                var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
                var n = U.Text(Loc.F("Schritt {0}", "Step {0}", s.Number), Theme.FsHint, fg: Theme.Muted); n.Width = 88; DockPanel.SetDock(n, Dock.Left); row.Children.Add(n);
                row.Children.Add(new TextBlock { Text = Steps.TextTemplates.TokensNotation(s.Json.Obj("input")!), FontFamily = Theme.Mono, FontSize = Theme.FsHint, TextWrapping = TextWrapping.Wrap });
                box.Children.Add(row);
            }
            list.Children.Add(U.Card(box, new Thickness(18, 12, 18, 12)));
        }
        var ok = U.Primary(Loc.L("Fertig", "Done"), () =>
        {
            if (choices.Any(c => c.Item2.All(r => r.IsChecked != true)) || itemChoices.Any(c => c.Item2.All(r => r.IsChecked != true))) { Msg.Show(Loc.L("Noch nicht alles entschieden\n\nBitte für jede auffällige Eingabe „In Ordnung“ oder „Schwärzen“ wählen.", "Not everything decided yet\n\nPlease choose “OK” or “Redact” for every conspicuous input."), Loc.L("Durchsicht", "Review"),MessageBoxButton.OK, MessageBoxImage.Information); return; }
            foreach (var (s, items) in choices) Decisions.Add((s, items[0].IsChecked == true ? "kept" : "redacted"));
            foreach (var (it, items) in itemChoices) ItemDecisions.Add((it, items[0].IsChecked == true ? "kept" : "redacted"));
            DialogResult = true;
        });
        var cancel = U.Btn(Loc.L("Abbrechen", "Cancel"), () => DialogResult = false);
        var foot = U.Footer(ok, cancel);
        var root = new DockPanel();
        DockPanel.SetDock(foot, Dock.Bottom);
        root.Children.Add(foot);
        root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }
}
