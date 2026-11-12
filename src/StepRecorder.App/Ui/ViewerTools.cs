using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.Native;
using StepRecorder.ReadLayer;

namespace StepRecorder.App.Ui;

/// <summary>
/// Editor-Werkzeuge: Mehrfachauswahl, eingefügte Schritte, Bild neu aufnehmen/ersetzen, Suchen und Ersetzen,
/// personenbezogene Daten in Bildern finden (Texterkennung), Sprache der Texte, Rechtschreibprüfung.
/// </summary>
sealed partial class ViewerWindow
{
    List<ViewStep> SelSteps => _list.SelectedItems.OfType<ListBoxItem>().Select(i => i.Tag).OfType<ViewStep>()
        .Select(s => _view.All.FirstOrDefault(x => x.Id == s.Id) ?? _view.Manual.FirstOrDefault(x => x.Id == s.Id) ?? s).ToList();

    /// <summary>Rechtschreibprüfung in der Sprache der Texte (Windows-Wörterbücher, Vorschläge per Rechtsklick).</summary>
    void Spell(TextBox t)
    {
        t.Language = System.Windows.Markup.XmlLanguage.GetLanguage(_view.Language == "en" ? "en-US" : "de-DE");
        SpellCheck.SetIsEnabled(t, true);
    }

    // =====================================================================================
    // Mehrfachauswahl
    // =====================================================================================

    void ShowBulk()
    {
        var steps = SelSteps;
        _details.Children.Clear();
        _editTitle = _editBody = null; _editAnchor = null;
        bool ro = _view.Session.ReadOnly;
        var head = U.Text($"{steps.Count} Schritte ausgewählt", 17, true, margin: new Thickness(0, 0, 0, 2)); head.FontFamily = Theme.Display;
        _details.Children.Add(head);
        _details.Children.Add(U.Text("Strg+Klick wählt einzelne Schritte dazu, Umschalt+Klick einen Bereich. Alles hier gilt für alle ausgewählten Schritte.", 12, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 6)));

        WrapPanel Row() { var w = new WrapPanel { Margin = new Thickness(-3, 0, 0, 0) }; _details.Children.Add(w); return w; }
        Button B(string? glyph, string text, Action a, string? tip = null) { var b = U.Btn(glyph, text, a, tip); b.IsEnabled = !ro; return b; }

        _details.Children.Add(U.Overline("Sichtbarkeit", new Thickness(0, 14, 0, 6)));
        var r1 = Row();
        r1.Children.Add(B(Theme.IEye, "Ausblenden", () => BulkEdit(steps, se => se["hidden"] = true)));
        r1.Children.Add(B(Theme.IEye, "Einblenden", () => BulkEdit(steps, se => se["hidden"] = false)));

        _details.Children.Add(U.Overline("Wichtigkeit", new Thickness(0, 14, 0, 6)));
        var r2 = Row();
        foreach (var (imp, name) in new[] { ("primary", "Hauptschritt"), ("secondary", "Nebenschritt"), ("trace", "Spur") })
            r2.Children.Add(B(null, name, () => BulkEdit(steps.Where(s => !s.Manual), se => se["importance"] = imp)));

        _details.Children.Add(U.Overline("Im Dokument", new Thickness(0, 14, 0, 6)));
        var r3 = Row();
        r3.Children.Add(B(Theme.IDoc, "Kapitel davor beginnen", () => ToggleChapter(steps[0].Anchor, true), "Vor dem ersten ausgewählten Schritt beginnt ein neues Kapitel"));
        r3.Children.Add(B(Theme.ICamera, "Bilder zeigen", () => BulkEdit(steps, se => se.Remove("showImage"))));
        r3.Children.Add(B(Theme.ICamera, "Bilder weglassen", () => BulkEdit(steps, se => se["showImage"] = false)));
        var size = new ComboBox { MinWidth = 200, IsEnabled = !ro, Margin = new Thickness(3, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        size.Items.Add("Bildgröße für alle …");
        size.Items.Add("wie im Dokument");
        foreach (var (_, name) in DocSettings.Sizes) size.Items.Add(name);
        size.SelectedIndex = 0;
        size.SelectionChanged += (_, _) =>
        {
            int i = size.SelectedIndex; if (i <= 0) return;
            BulkEdit(steps, se => { if (i == 1) se.Remove("imageSize"); else se["imageSize"] = DocSettings.Sizes[i - 2].id; });
        };
        _details.Children.Add(size);

        _details.Children.Add(U.Overline("Hinweiskasten", new Thickness(0, 14, 0, 6)));
        var r4 = Row();
        foreach (var (k, name) in NoteKinds)
            r4.Children.Add(B(Theme.IAdd, name, () =>
            {
                var d = new InputDialog($"{name} zu {steps.Count} Schritten hinzufügen", "") { Owner = this };
                if (d.ShowDialog() != true || d.Value.Trim().Length == 0) return;
                Change(e =>
                {
                    var arr = e["comments"] as JsonArray ?? new JsonArray();
                    foreach (var s in steps) arr.Add(new JsonObject { ["id"] = NextId(e, "comments", "uc"), ["anchor"] = s.Anchor, ["kind"] = k, ["text"] = d.Value.Trim(), ["createdUtc"] = Json.UtcNow(), ["by"] = "user", ["origin"] = "viewer" });
                    e["comments"] = arr;
                });
            }));
        var manual = steps.Where(s => s.Manual).ToList();
        if (manual.Count > 0)
        {
            _details.Children.Add(U.Overline("Eingefügte Schritte", new Thickness(0, 14, 0, 6)));
            Row().Children.Add(B(Theme.IDelete, $"{manual.Count} eingefügte löschen", () => DeleteManual(manual)));
        }
    }

    void BulkEdit(IEnumerable<ViewStep> steps, Action<JsonObject> edit)
    {
        var list = steps.ToList();
        if (list.Count == 0) return;
        var keep = list.Select(s => s.Id).ToHashSet();
        Change(e => { foreach (var s in list) edit(StepEdit(e, s.Anchor)); });
        // Auswahl wiederherstellen
        foreach (var it in _list.Items.OfType<ListBoxItem>()) if (it.Tag is ViewStep vs && keep.Contains(vs.Id)) it.IsSelected = true;
        _status.Text = $"{list.Count} Schritte geändert – rückgängig mit Strg+Z";
    }

    // =====================================================================================
    // Eingefügte Schritte
    // =====================================================================================

    /// <summary>Menü „Einfügen“: Textschritt oder Bildschritt aus Datei, Zwischenablage oder Bildschirmfoto.</summary>
    void InsertMenu(FrameworkElement anchor)
    {
        var m = Menu(new (string, Action)[]
        {
            ("Textschritt einfügen", () => InsertManual(null)),
            ("-", () => { }),
            ("Bildschritt: Bild aus Datei …", () => { if (PickImageFile() is { } b) using (b) InsertManual(b); }),
            ("Bildschritt: Bild aus der Zwischenablage", () => { if (ClipboardImage() is { } b) using (b) InsertManual(b); else Msg.Show("In der Zwischenablage ist kein Bild."); }),
            ("Bildschritt: Bildschirmfoto in 3 Sekunden …", () => CaptureAfterCountdown(b => InsertManual(b))),
        });
        m.PlacementTarget = anchor; m.Placement = System.Windows.Controls.Primitives.PlacementMode.Top; m.IsOpen = true;
    }

    void InsertManual(System.Drawing.Bitmap? image)
    {
        if (_view.Session.ReadOnly) { Msg.Show("Diese Sitzung ist schreibgeschützt."); return; }
        var cur = Sel;
        long after = cur == null ? 0 : cur.Manual ? cur.After : cur.Anchor;
        string? img = image != null ? _view.AddAsset(image, "schritt") : null;
        long anchor = 0;
        Change(e =>
        {
            var arr = e["inserted"] as JsonArray ?? new JsonArray();
            anchor = -(1000 + arr.OfType<JsonObject>().Select(x => -x.Long("anchor") - 1000).DefaultIfEmpty(0).Max() + 1);
            arr.Add(new JsonObject { ["anchor"] = anchor, ["after"] = after, ["title"] = image != null ? "Bild" : "Neuer Schritt", ["body"] = "", ["image"] = img, ["createdUtc"] = Json.UtcNow() });
            e["inserted"] = arr;
            // in einer vorhandenen Reihenfolge direkt hinter den ausgewählten Schritt setzen
            if (e["order"] is JsonArray order && order.Count > 0 && cur != null)
            {
                int i = order.Select((o, ix) => (o, ix)).FirstOrDefault(x => x.o is JsonValue v && v.TryGetValue<long>(out var l) && l == cur.Anchor).ix;
                order.Insert(Math.Min(order.Count, i + 1), anchor);
            }
        });
        if (_list.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag is ViewStep vs && vs.Anchor == anchor) is { } item)
        {
            _list.SelectedItems.Clear(); item.IsSelected = true; _list.ScrollIntoView(item);
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, FocusInlineTitle);
        }
        _status.Text = "Schritt eingefügt – Überschrift und Text rechts eingeben.";
    }

    void DeleteManual(List<ViewStep> steps)
    {
        var anchors = steps.Where(s => s.Manual).Select(s => s.Anchor).ToHashSet();
        if (anchors.Count == 0) return;
        Change(e =>
        {
            if (e["inserted"] is JsonArray ins) foreach (var x in ins.OfType<JsonObject>().Where(x => anchors.Contains(x.Long("anchor"))).ToList()) ins.Remove(x);
            if (e["steps"] is JsonArray st) foreach (var x in st.OfType<JsonObject>().Where(x => anchors.Contains(x.Long("anchor"))).ToList()) st.Remove(x);
            if (e["comments"] is JsonArray cm) foreach (var x in cm.OfType<JsonObject>().Where(x => anchors.Contains(x.Long("anchor"))).ToList()) cm.Remove(x);
            if (e["order"] is JsonArray or) foreach (var x in or.Where(x => x is JsonValue v && v.TryGetValue<long>(out var l) && anchors.Contains(l)).ToList()) or.Remove(x);
        });
        _status.Text = "Eingefügte Schritte gelöscht – rückgängig mit Strg+Z";
    }

    // =====================================================================================
    // Bild ersetzen / neu aufnehmen
    // =====================================================================================

    void ReplaceImageMenu(ViewStep s, FrameworkElement? anchor = null)
    {
        var items = new List<(string, Action)>
        {
            ("Bild neu aufnehmen (Bildschirmfoto in 3 Sekunden) …", () => CaptureAfterCountdown(b => ReplaceImage(s, b))),
            ("Bild aus Datei …", () => { if (PickImageFile() is { } b) using (b) ReplaceImage(s, b); }),
            ("Bild aus der Zwischenablage", () => { if (ClipboardImage() is { } b) using (b) ReplaceImage(s, b); else Msg.Show("In der Zwischenablage ist kein Bild."); }),
        };
        if (s.ImageReplaced) { items.Add(("-", () => { })); items.Add(("Originalbild wiederherstellen", () => Change(e => StepEdit(e, s.Anchor).Remove("frameChoice"), refill: false))); }
        var m = Menu(items);
        if (anchor != null) { m.PlacementTarget = anchor; m.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; }
        m.IsOpen = true;
    }

    void ReplaceImage(ViewStep s, System.Drawing.Bitmap bmp)
    {
        if (_view.Session.ReadOnly) { Msg.Show("Diese Sitzung ist schreibgeschützt."); return; }
        string id = _view.AddAsset(bmp, "ersatz");
        if (s.Manual) Change(e => { if (e["inserted"] is JsonArray ins) foreach (var x in ins.OfType<JsonObject>().Where(x => x.Long("anchor") == s.Anchor)) x["image"] = id; }, refill: false);
        else Change(e => StepEdit(e, s.Anchor)["frameChoice"] = new JsonObject { ["pre"] = id, ["post"] = id, ["by"] = "user" }, refill: false);
        InvalidateThumb(s);
        _status.Text = "Bild ersetzt – „Originalbild wiederherstellen“ im Menü des Schritts macht das rückgängig.";
    }

    System.Drawing.Bitmap? PickImageFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Bilder|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Alle Dateien|*.*" };
        if (dlg.ShowDialog(this) != true) return null;
        try { using var tmp = new System.Drawing.Bitmap(dlg.FileName); return new System.Drawing.Bitmap(tmp); }
        catch (Exception ex) { Msg.Show("Das Bild lässt sich nicht öffnen:\n" + ex.Message); return null; }
    }

    static System.Drawing.Bitmap? ClipboardImage()
    {
        try
        {
            if (!Clipboard.ContainsImage()) return null;
            var src = Clipboard.GetImage();
            if (src == null) return null;
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(src));
            using var ms = new MemoryStream(); enc.Save(ms); ms.Position = 0;
            using var tmp = new System.Drawing.Bitmap(ms);
            return new System.Drawing.Bitmap(tmp);
        }
        catch { return null; }
    }

    /// <summary>Editor ausblenden, 3 Sekunden Countdown, dann Bildschirmfoto des Monitors unter dem Mauszeiger.</summary>
    void CaptureAfterCountdown(Action<System.Drawing.Bitmap> done)
    {
        var start = UiApp.Instance.Windows.OfType<StartWindow>().FirstOrDefault(w => w.IsVisible);
        Hide(); start?.Hide();
        var cd = new CountdownWindow(3, "Bildschirmfoto in");
        cd.Finished += () =>
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, async () =>
            {
                await Task.Delay(250); // Countdown sicher verschwunden
                System.Drawing.Bitmap? shot = null;
                try
                {
                    Win32.GetCursorPos(out var p);
                    var mons = ScreenGrabber.GetMonitors();
                    var m = mons.FirstOrDefault(x => x.Bounds.Contains(p.X, p.Y)) ?? mons[0];
                    shot = ScreenGrabber.Capture(m.Bounds);
                }
                catch (Exception ex) { UiApp.ShowError(ex); }
                Show(); Activate();
                if (shot != null) using (shot) done(shot);
            });
        };
        cd.Show();
    }

    // =====================================================================================
    // Suchen und Ersetzen
    // =====================================================================================

    void ShowSearchReplace()
    {
        InlineCommit(now: true);
        var w = new SearchReplaceWindow(this) { Owner = this };
        w.ShowDialog();
    }

    /// <summary>Ersetzt in Überschriften, Texten, Hinweiskästen, Kapiteln und Dokumenttexten der aktuellen Sprache; liefert die Zahl der Treffer.</summary>
    internal int ReplaceAll(Regex rx, string replacement, bool steps, bool notes, bool chapters, bool doc, bool dryRun)
    {
        int hits = 0;
        string R(string? s) { if (string.IsNullOrEmpty(s)) return s ?? ""; int n = rx.Matches(s).Count; hits += n; return n > 0 && !dryRun ? rx.Replace(s, replacement.Replace("$", "$$")) : s; }
        void Do(JsonObject e)
        {
            var all = _view.Effective(guide: false, importance: "all", includeManual: true);
            if (steps)
                foreach (var s in all)
                {
                    string t0 = s.AuthorTitle ?? s.Title, b0 = s.AuthorBody ?? s.Guide;
                    string t1 = R(t0), b1 = R(b0);
                    if (dryRun || (t1 == t0 && b1 == b0)) continue;
                    var se = StepEdit(e, s.Anchor);
                    var text = se["text"] as JsonObject ?? new JsonObject();
                    text[_view.Language] = new JsonObject { ["title"] = t1, ["body"] = b1, ["by"] = "user", ["atUtc"] = Json.UtcNow() };
                    se["text"] = text;
                }
            if (notes && e["comments"] is JsonArray cm)
                foreach (var c in cm.OfType<JsonObject>()) { var v = R(c.Str("text")); if (!dryRun) c["text"] = v; }
            if (chapters && e["steps"] is JsonArray st)
                foreach (var se in st.OfType<JsonObject>())
                    if (se.Obj("chapter") is { } ch)
                    {
                        var target = _view.Language == "de" ? ch : ch.Obj(_view.Language);
                        if (target == null) continue;
                        foreach (var k in new[] { "title", "text" }) { var v = R(target.Str(k)); if (!dryRun && target[k] != null) target[k] = v; }
                    }
            if (doc && e["guide"] is JsonObject g)
            {
                var target = _view.Language == "de" ? g : g.Obj("i18n")?.Obj(_view.Language);
                if (target != null) foreach (var k in new[] { "title", "intro", "outro" }) { var v = R(target.Str(k)); if (!dryRun && target[k] != null) target[k] = v; }
                var d = _view.Language == "de" ? g.Obj("doc") : g.Obj("i18n")?.Obj(_view.Language);
                if (d != null) foreach (var k in new[] { "subtitle", "audience", "prerequisites", "footer" }) { var v = R(d.Str(k)); if (!dryRun && d[k] != null) d[k] = v; }
            }
        }
        if (dryRun) Do((JsonObject)_view.Edits.DeepClone());
        else if (true) { Change(Do); _titleBlock.Text = _view.Title; }
        return hits;
    }

    // =====================================================================================
    // Personenbezogene Daten in Bildern (Texterkennung)
    // =====================================================================================

    void ScanImagesForPersonalData()
    {
        if (!Ocr.Available) { Msg.Show("Texterkennung nicht verfügbar\n\nFür diese Funktion braucht Windows ein OCR-Sprachpaket (Einstellungen › Zeit und Sprache › Sprache › Optionen › „Optische Zeichenerkennung“).", "Persönliche Daten suchen", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        new PiiScanWindow(this, _view) { Owner = this }.ShowDialog();
    }

    internal void ApplyPiiRedactions(IEnumerable<(string frame, string m, System.Drawing.Rectangle rect)> hits, string style)
    {
        var list = hits.ToList();
        if (list.Count == 0) return;
        Change(e =>
        {
            var arr = e["redactions"] as JsonArray ?? new JsonArray();
            foreach (var (frame, m, r) in list)
                arr.Add(new JsonObject
                {
                    ["id"] = NextId(e, "redactions", "r"), ["kind"] = "image", ["style"] = style,
                    ["areas"] = new JsonArray(new JsonObject { ["m"] = m, ["from"] = frame, ["to"] = frame, ["rect"] = new JsonArray(r.X, r.Y, r.Width, r.Height) }),
                    ["rule"] = new JsonObject { ["kind"] = "ocr", ["origin"] = new JsonObject { ["frame"] = frame } }, ["createdUtc"] = Json.UtcNow(), ["by"] = "user"
                });
            e["redactions"] = arr;
        });
        _status.Text = $"{list.Count} Stellen in Bildern geschwärzt – rückgängig mit Strg+Z";
    }

    // =====================================================================================
    // Sprache der Texte
    // =====================================================================================

    void SetTextLanguage(string lang)
    {
        if (_view.Language == lang) return;
        InlineCommit(now: true);
        var keep = Sel?.Id;
        _view.Language = lang; _view.Rebuild();
        _thumbs.Clear();
        Fill(keep);
        if (_detailTab == 2) DocDetails();
        _status.Text = lang == "en" ? "Texts in English – the German texts are kept separately." : "Texte auf Deutsch – die englischen Texte bleiben getrennt erhalten.";
    }
}

/// <summary>Großer Countdown mitten auf dem Bildschirm (vor Bildschirmfotos und Aufnahmen).</summary>
sealed class CountdownWindow : Window
{
    readonly TextBlock _num = new() { FontSize = 64, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center };
    int _left;
    public event Action? Finished;

    public CountdownWindow(int seconds, string caption, string? hint = null)
    {
        _left = seconds;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false; ShowActivated = false;
        SizeToContent = SizeToContent.WidthAndHeight; WindowStartupLocation = WindowStartupLocation.CenterScreen; ResizeMode = ResizeMode.NoResize;
        _num.FontFamily = Theme.Display;
        var col = new StackPanel { Margin = new Thickness(36, 22, 36, 24) };
        col.Children.Add(new TextBlock { Text = caption, Foreground = new SolidColorBrush(Color.FromRgb(0xBA, 0xB8, 0xB1)), FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center });
        col.Children.Add(_num);
        if (hint != null) col.Children.Add(new TextBlock { Text = hint, Foreground = new SolidColorBrush(Color.FromRgb(0xBA, 0xB8, 0xB1)), FontSize = 12.5, TextAlignment = TextAlignment.Center, MaxWidth = 360, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });
        Content = new Border { Background = new SolidColorBrush(Color.FromArgb(235, 0x17, 0x16, 0x14)), CornerRadius = new CornerRadius(12), BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x6A, 0x2B)), BorderThickness = new Thickness(2), Child = col };
        _num.Text = _left.ToString();
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        t.Tick += (_, _) =>
        {
            _left--;
            if (_left <= 0) { t.Stop(); Close(); Finished?.Invoke(); return; }
            _num.Text = _left.ToString();
            Anim.Pop(_num, 0.55, 360);
            Tones.Tick();
        };
        Loaded += (_, _) => { Tones.Tick(); t.Start(); if (Content is UIElement ce) Anim.Pop(ce, 0.85, 300); Anim.Pop(_num, 0.55, 360); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { t.Stop(); Close(); } };
    }
}

/// <summary>Suchen und Ersetzen über alle Schritte, Hinweise, Kapitel und Dokumenttexte.</summary>
sealed class SearchReplaceWindow : Window
{
    readonly TextBox _find = new() { Tag = "Suchen nach …" }, _repl = new() { Tag = "Ersetzen durch …" };
    readonly CheckBox _case = new() { Content = "Groß-/Kleinschreibung beachten", Margin = new Thickness(0, 0, 18, 6) }, _word = new() { Content = "Nur ganze Wörter", Margin = new Thickness(0, 0, 18, 6) };
    readonly CheckBox _steps = new() { Content = "Überschriften und Texte", IsChecked = true, Margin = new Thickness(0, 0, 18, 6) }, _notes = new() { Content = "Hinweiskästen", IsChecked = true, Margin = new Thickness(0, 0, 18, 6) },
        _chapters = new() { Content = "Kapitel", IsChecked = true, Margin = new Thickness(0, 0, 18, 6) }, _doc = new() { Content = "Titel und Dokumenttexte", IsChecked = true, Margin = new Thickness(0, 0, 18, 6) };
    readonly TextBlock _count = new() { FontSize = 12.5, Margin = new Thickness(0, 8, 0, 0) };
    readonly ViewerWindow _v;

    public SearchReplaceWindow(ViewerWindow v)
    {
        _v = v;
        Title = "Suchen und Ersetzen"; Width = 560; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Theme.Prepare(this, Theme.Surface);
        var opts = new WrapPanel(); foreach (var c in new[] { _case, _word }) opts.Children.Add(c);
        var scope = new WrapPanel(); foreach (var c in new[] { _steps, _notes, _chapters, _doc }) scope.Children.Add(c);
        var body = U.Col(U.Field("Suchen", _find), U.Field("Ersetzen durch", _repl), opts, U.Overline("Wo", new Thickness(0, 10, 0, 6)), scope, _count,
            U.Text("Ersetzt wird in der aktuellen Textsprache. Automatisch erzeugte Texte werden dabei zu Ihren eigenen Texten.", 11.5, fg: Theme.Muted, margin: new Thickness(0, 6, 0, 0)));
        var go = U.Primary("Alle ersetzen", Apply);
        U.Dialog(this, "Suchen und Ersetzen", body, go, U.Btn("Schließen", Close));
        foreach (var t in new[] { _find, _repl }) t.TextChanged += (_, _) => Count();
        foreach (var c in new[] { _case, _word, _steps, _notes, _chapters, _doc }) { c.Checked += (_, _) => Count(); c.Unchecked += (_, _) => Count(); }
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Loaded += (_, _) => _find.Focus();
        Count();
    }

    Regex? Rx()
    {
        if (_find.Text.Length == 0) return null;
        var p = Regex.Escape(_find.Text);
        if (_word.IsChecked == true) p = @"(?<![\p{L}\p{N}])" + p + @"(?![\p{L}\p{N}])";
        return new Regex(p, _case.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase);
    }

    void Count()
    {
        var rx = Rx();
        if (rx == null) { _count.Text = "Suchbegriff eingeben."; _count.Foreground = Theme.Muted; return; }
        int n = _v.ReplaceAll(rx, _repl.Text, _steps.IsChecked == true, _notes.IsChecked == true, _chapters.IsChecked == true, _doc.IsChecked == true, dryRun: true);
        _count.Text = n == 0 ? "Keine Treffer." : $"{n} Treffer";
        _count.Foreground = n == 0 ? Theme.Muted : Theme.AccentText;
    }

    void Apply()
    {
        var rx = Rx(); if (rx == null) return;
        int n = _v.ReplaceAll(rx, _repl.Text, _steps.IsChecked == true, _notes.IsChecked == true, _chapters.IsChecked == true, _doc.IsChecked == true, dryRun: false);
        _count.Text = $"{n} Stellen ersetzt – rückgängig mit Strg+Z im Editor.";
        _count.Foreground = Theme.Success;
    }
}

/// <summary>Durchsucht alle Bilder der Aufnahme per Texterkennung nach E-Mail-Adressen, IBANs, Telefon- und Kartennummern, Schlüsseln.</summary>
sealed class PiiScanWindow : Window
{
    readonly ViewerWindow _v;
    readonly SessionView _view;
    readonly StackPanel _list = new();
    readonly TextBlock _status = new() { FontSize = 12.5, Margin = new Thickness(0, 0, 0, 8) };
    readonly ComboBox _style = new() { Items = { "Schwärzen", "Verpixeln" }, SelectedIndex = 0, MinWidth = 140 };
    readonly List<(CheckBox cb, string frame, string m, System.Drawing.Rectangle rect)> _hits = new();
    readonly Button _apply;
    bool _cancel;

    public PiiScanWindow(ViewerWindow v, SessionView view)
    {
        _v = v; _view = view;
        Title = "Persönliche Daten in Bildern"; Width = 640; Height = 560; ResizeMode = ResizeMode.CanResize; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Theme.Prepare(this, Theme.Surface);
        var body = new DockPanel();
        var top = U.Col(U.Text("Die Texterkennung von Windows sucht in allen Bildern nach E-Mail-Adressen, IBANs, Kartennummern, Telefonnummern und Schlüsseln. Gefundene Stellen werden nur im jeweiligen Bild geschwärzt.", 12.5, fg: Theme.Fg2, margin: new Thickness(0, 0, 0, 10)), _status);
        DockPanel.SetDock(top, Dock.Top); body.Children.Add(top);
        body.Children.Add(new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 330 });
        _apply = U.Primary("Ausgewählte schwärzen", Apply, Theme.IShield);
        _apply.IsEnabled = false;
        var root = new DockPanel();
        var footer = new DockPanel { Margin = new Thickness(20, 12, 20, 12) };
        var right = U.Row(_apply, U.Btn("Schließen", () => { _cancel = true; Close(); }));
        DockPanel.SetDock(right, Dock.Right); footer.Children.Add(right);
        footer.Children.Add(U.Row(U.Text("Art", 12.5, fg: Theme.Muted, margin: new Thickness(0, 7, 8, 0)), _style));
        var foot = new Border { Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Child = footer };
        DockPanel.SetDock(foot, Dock.Bottom); root.Children.Add(foot);
        var head = U.Text("Persönliche Daten in Bildern suchen", 18, true, margin: new Thickness(0, 0, 0, 10)); head.FontFamily = Theme.Display;
        root.Children.Add(new Border { Padding = new Thickness(24, 20, 24, 12), Child = U.Col(head, body) });
        Content = root;
        Closed += (_, _) => _cancel = true;
        Loaded += async (_, _) => await Scan();
    }

    async Task Scan()
    {
        var steps = _view.Effective(guide: false, importance: "all", includeManual: true).Where(s => !s.Redacted).ToList();
        var frames = new List<(string frame, ViewStep step)>();
        foreach (var s in steps) foreach (var f in new[] { s.PreFrame, s.PostFrame }) if (f != null && !frames.Any(x => x.frame == f)) frames.Add((f, s));
        int done = 0, found = 0;
        foreach (var (frame, step) in frames)
        {
            if (_cancel) return;
            _status.Text = $"Bild {++done} von {frames.Count} wird geprüft …  ({found} Fundstellen)";
            var res = await Task.Run(() =>
            {
                using var bmp = _view.Frame(frame);
                return bmp == null ? new() : Ocr.FindSensitive(bmp);
            });
            string m = frame.StartsWith("u:") ? frame : _view.Session.Frame(frame)?.Str("m") ?? "";
            foreach (var (rect, kind, text) in res)
            {
                found++;
                var cb = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
                var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
                DockPanel.SetDock(cb, Dock.Left); row.Children.Add(cb);
                var label = new TextBlock { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
                label.Inlines.Add(new System.Windows.Documents.Run($"Schritt {(step.Manual ? "eingefügt" : step.Number.ToString())}  ") { FontFamily = Theme.Mono, Foreground = Theme.Muted });
                label.Inlines.Add(new System.Windows.Documents.Run(Patterns.KindName(kind) + ": ") { FontWeight = FontWeights.SemiBold });
                label.Inlines.Add(new System.Windows.Documents.Run(text));
                label.MouseLeftButtonUp += (_, _) => cb.IsChecked = cb.IsChecked != true;
                row.Children.Add(label);
                _list.Children.Add(row);
                _hits.Add((cb, frame, m, rect));
            }
        }
        _status.Text = found == 0 ? $"{frames.Count} Bilder geprüft – keine Fundstellen. Namen und frei geschriebene Angaben erkennt die Suche nicht; bitte trotzdem selbst durchsehen."
                                  : $"{frames.Count} Bilder geprüft – {found} Fundstellen. Bitte prüfen und auswählen.";
        _apply.IsEnabled = found > 0;
    }

    void Apply()
    {
        var sel = _hits.Where(h => h.cb.IsChecked == true).Select(h => (h.frame, h.m, h.rect)).ToList();
        _v.ApplyPiiRedactions(sel, _style.SelectedIndex == 1 ? "pixelate" : "black");
        Close();
    }
}

/// <summary>Kurze Signaltöne (selbst erzeugt, über die Standard-Audioausgabe – funktioniert auch per Remotedesktop).</summary>
static class Tones
{
    static byte[] Wave(params (double freq, int ms)[] parts)
    {
        const int rate = 22050;
        var samples = new List<short>();
        foreach (var (f, ms) in parts)
        {
            int n = rate * ms / 1000;
            for (int i = 0; i < n; i++)
            {
                double env = Math.Min(1, Math.Min(i / (rate * 0.005), (n - i) / (rate * 0.02)));
                samples.Add(f <= 0 ? (short)0 : (short)(Math.Sin(2 * Math.PI * f * i / rate) * 9000 * env));
            }
        }
        using var ms2 = new MemoryStream(); using var w = new BinaryWriter(ms2);
        int bytes = samples.Count * 2;
        w.Write("RIFF"u8.ToArray()); w.Write(36 + bytes); w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(bytes); foreach (var s in samples) w.Write(s);
        return ms2.ToArray();
    }

    static void Play(byte[] wav)
    {
        Task.Run(() => { try { using var ms = new MemoryStream(wav); using var p = new System.Media.SoundPlayer(ms); p.PlaySync(); } catch { } });
    }

    static readonly Lazy<byte[]> _start = new(() => Wave((660, 90), (0, 30), (880, 140)));
    static readonly Lazy<byte[]> _stop = new(() => Wave((880, 90), (0, 30), (520, 180)));
    static readonly Lazy<byte[]> _pause = new(() => Wave((600, 120)));
    static readonly Lazy<byte[]> _tick = new(() => Wave((1000, 45)));
    static readonly Lazy<byte[]> _remind = new(() => Wave((740, 80), (0, 60), (740, 80), (0, 60), (740, 80)));

    public static void Remind() => Play(_remind.Value);
    public static void Start() => Play(_start.Value);
    public static void Stop() => Play(_stop.Value);
    public static void Pause() => Play(_pause.Value);
    public static void Tick() => Play(_tick.Value);
}
