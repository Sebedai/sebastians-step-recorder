using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.App.Ui;

/// <summary>
/// Editor für Bedienungsanleitungen: „Titelseite und Einleitung“ (Titelseite, Einleitung, Voraussetzungen, Abschluss,
/// Aufbau, Bilder, Gestaltung, Vorlage, weitere Aufnahmen) sowie Kapitel und Hinweise am Schritt.
/// </summary>
sealed partial class ViewerWindow
{
    string? _focusCommentId;


    static JsonObject GuideOf(JsonObject edits) { var g = edits["guide"] as JsonObject ?? new JsonObject(); edits["guide"] = g; return g; }

    /// <summary>Speichert ohne die Oberfläche neu aufzubauen (Eingabefelder behalten den Fokus); Liste wird nachgezogen, sobald der Fokus den Detailbereich verlässt.</summary>
    void QuietChange(Action<JsonObject> edit, bool refillLater = false)
    {
        if (_view.Session.ReadOnly) return;
        _undo.Add(Json.Line(_view.Edits)); _redo.Clear();
        edit(_view.Edits);
        if (!_view.SaveEdits()) { _view.ReloadEdits(); Msg.Show("Die Sitzung wurde zwischenzeitlich geändert; bitte erneut versuchen."); }
        UpdateUndo();
        _status.Text = "Gespeichert";
        if (_cover) _coverHost.Content = CoverPreview();
        if (refillLater) Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => { if (!_details.IsKeyboardFocusWithin) Fill(); });
    }

    /// <summary>Ändert eine Dokument-Einstellung.</summary>
    void DocChange(Action<DocSettings> change, bool quiet = true)
    {
        void Do(JsonObject e) { var g = GuideOf(e); var d = DocSettings.From(g); change(d); g["doc"] = d.ToJson(); }
        if (quiet) QuietChange(Do); else Change(Do, refill: false);
    }

    /// <summary>Textfeld, das beim Verlassen speichert (nur wenn sich etwas geändert hat).</summary>
    TextBox CommitBox(string value, Action<string> save, string? placeholder = null, bool multi = false, double minH = 0)
    {
        var t = new Wpf.Ui.Controls.TextBox { Text = value, PlaceholderText = placeholder ?? "", PlaceholderEnabled = placeholder != null, ClearButtonEnabled = false, IsReadOnly = _view.Session.ReadOnly, TextWrapping = TextWrapping.Wrap, MaxLength = 4000 };
        Spell(t);
        if (multi) { t.AcceptsReturn = true; t.MinHeight = minH > 0 ? minH : 64; t.VerticalContentAlignment = VerticalAlignment.Top; }
        string orig = value;
        t.LostKeyboardFocus += (_, _) => { var v = multi ? t.Text.TrimEnd() : t.Text.Trim(); if (v == orig) return; orig = v; save(v); };
        t.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { t.Text = orig; _list.Focus(); e.Handled = true; }
            else if (e.Key == Key.Enter && (!multi || Keyboard.Modifiers == ModifierKeys.Control)) { _list.Focus(); e.Handled = true; }
        };
        return t;
    }

    // =====================================================================================
    // Titelseite und Einleitung (Einstellungen der Anleitung)
    // =====================================================================================

    void DocDetails()
    {
        _details.Children.Clear();
        _editTitle = _editBody = null; _editAnchor = null;
        var guide = _view.Edits.Obj("guide");
        string lang = _view.Language;
        var d = DocSettings.From(guide, lang);
        string GS(string key) => DocSettings.GuideText(guide, key, lang) ?? "";
        bool en = lang != "de", ro = _view.Session.ReadOnly;

        _details.Children.Add(U.Heading("Titelseite und Einleitung", Theme.FsTitle, new Thickness(0, 4, 0, 4)));
        _details.Children.Add(U.Hint("Alles, was aus den Schritten eine fertige Anleitung macht – gilt für HTML, Word, PDF, Markdown und GIF.", new Thickness(0, 0, 0, 12)));
        if (en) _details.Children.Add(Callout(false, "Englische Fassung", "Titel, Untertitel, Zielgruppe, Texte, Voraussetzungen und Fußzeile gelten hier nur für Englisch. Leer gelassene Felder übernehmen die deutsche Fassung."));
        var actions = new WrapPanel { Margin = new Thickness(-3, 0, 0, 0) };
        actions.Children.Add(U.Btn(Theme.IEye, "Vorschau im Browser", PreviewDocument, "Anleitung als HTML erzeugen und im Standardbrowser öffnen"));
        actions.Children.Add(U.Btn(Theme.IExport, "Exportieren …", ShowExport, EditorCommands.Tip("export"), "Primary"));
        _details.Children.Add(actions);

        // Titelseite
        var title = Section("Titelseite");
        title.Children.Add(U.Field("Titel", CommitBox(GS("title") is { Length: > 0 } tt ? tt : _view.Title, v => { if (v.Length == 0) return; QuietChange(e => SetGuideText(e, "title", v)); UpdateTitle(); }, "Titel der Anleitung")));
        title.Children.Add(U.Field("Untertitel", CommitBox(d.Subtitle, v => LocDocChange("subtitle", v), "z. B. Kurzanleitung für neue Mitarbeitende")));
        var two = new Grid();
        two.ColumnDefinitions.Add(new ColumnDefinition()); two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); two.ColumnDefinitions.Add(new ColumnDefinition());
        var ver = U.Field("Version", CommitBox(d.Version, v => DocChange(x => x.Version = v), "z. B. 1.0"));
        var aud = U.Field("Zielgruppe", CommitBox(d.Audience, v => LocDocChange("audience", v), "z. B. Vertrieb"));
        Grid.SetColumn(aud, 2); two.Children.Add(ver); two.Children.Add(aud);
        title.Children.Add(two);
        title.Children.Add(U.Field("Autor oder Firma", CommitBox(d.Author, v => DocChange(x => x.Author = v), "erscheint auf der Titelseite")));
        var logoRow = new WrapPanel { Margin = new Thickness(-3, 0, 0, 0) };
        if (d.LogoBytes() is { } lb)
        {
            try
            {
                var bi = new System.Windows.Media.Imaging.BitmapImage(); bi.BeginInit(); bi.StreamSource = new MemoryStream(lb); bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bi.EndInit(); bi.Freeze();
                logoRow.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(Theme.RSmall), Padding = new Thickness(6), Margin = new Thickness(3, 3, 8, 3), BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Child = new Image { Source = bi, MaxHeight = 40, MaxWidth = 140 } });
            }
            catch { }
        }
        var pick = U.Btn(Theme.IOpen, d.Logo.Length > 0 ? "Anderes Logo …" : "Logo wählen …", PickLogo, "PNG, JPG oder BMP – erscheint auf der Titelseite bzw. im Kopf"); pick.IsEnabled = !ro;
        logoRow.Children.Add(pick);
        if (d.Logo.Length > 0) logoRow.Children.Add(U.Btn(Theme.IDelete, "Entfernen", () => DocChange(x => x.Logo = "", quiet: false)));
        title.Children.Add(U.Field("Logo", logoRow));

        // Einleitung und Texte
        var texts = Section("Einleitung und Texte");
        texts.Children.Add(U.Field("Einleitung", CommitBox(GS("intro"), v => QuietChange(e => SetGuideText(e, "intro", v)), "Wozu dient die Anleitung, was ist das Ergebnis?", true),
            "Ziel und erwartetes Ergebnis der Aufnahme werden automatisch ergänzt."));
        texts.Children.Add(U.Field("Voraussetzungen", CommitBox(d.Prerequisites, v => LocDocChange("prerequisites", v), "Eine pro Zeile, z. B. Zugang zum Portal", true),
            "Erscheint als Aufzählung vor dem ersten Schritt."));
        texts.Children.Add(U.Field("Abschluss", CommitBox(GS("outro"), v => QuietChange(e => SetGuideText(e, "outro", v)), "z. B. Fertig! Bei Fragen: Servicedesk, Tel. …", true)));

        // Aufbau
        var build = Section("Aufbau");
        CheckBox Check(string text, bool value, Action<bool> set, string tip)
        {
            var c = new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(0, 0, 0, 8), ToolTip = tip, IsEnabled = !ro };
            c.Checked += (_, _) => set(true); c.Unchecked += (_, _) => set(false);
            return c;
        }
        build.Children.Add(Check("Eigene Titelseite", d.Cover, v => DocChange(x => x.Cover = v, quiet: false), "Eigene erste Seite mit Logo, Titel, Untertitel, Version, Autor und Datum"));
        build.Children.Add(Check("Inhaltsverzeichnis", d.Toc, v => DocChange(x => x.Toc = v), "Mit Kapiteln und Schritten; im PDF mit Seitenzahlen. Erscheint ab 3 Kapiteln oder 6 Schritten."));
        build.Children.Add(Check("Datum anzeigen", d.ShowDate, v => DocChange(x => x.ShowDate = v, quiet: false), "Stand-Datum auf der Titelseite bzw. im Kopf"));
        int numIdx = d.Numbering switch { "flat" => 1, "none" => 2, _ => 0 };
        var (numTrack, nums) = U.Segmented("dnum", numIdx, (null, "1.1, 1.2 …"), (null, "1, 2, 3 …"), (null, "Keine"));
        for (int i = 0; i < 3; i++) { string v = new[] { "chapter", "flat", "none" }[i]; nums[i].Checked += (_, _) => DocChange(x => x.Numbering = v); }
        build.Children.Add(U.Field("Nummerierung der Schritte", numTrack, "„1.1“ zählt innerhalb der Kapitel – Kapitel legen Sie am Schritt unter „Einordnung“ an.", new Thickness(0, 8, 0, 16)));

        // Bilder
        var images = Section("Bilder");
        var viewBox = new ComboBox { MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = !ro };
        foreach (var (_, name) in DocSettings.Views) viewBox.Items.Add(name);
        viewBox.SelectedIndex = Math.Max(0, Array.FindIndex(DocSettings.Views, x => x.id == d.View));
        viewBox.SelectionChanged += (_, _) => { string v = DocSettings.Views[Math.Max(0, viewBox.SelectedIndex)].id; DocChange(x => x.View = v); };
        var ov = new CheckBox { Content = "Im Bereich um die Aktion eine kleine Übersicht des ganzen Bildschirms zeigen", IsChecked = d.Overview, Margin = new Thickness(0, 8, 0, 0), IsEnabled = !ro };
        ov.Checked += (_, _) => DocChange(x => x.Overview = true); ov.Unchecked += (_, _) => DocChange(x => x.Overview = false);
        images.Children.Add(U.Field("Bildausschnitt", U.Col(viewBox, ov), "„Bereich um die Aktion“ zeigt je Schritt nur die Umgebung von Klick oder Eingabe – gut lesbar, auch auf kleinen Seiten. Vorschau im Editor über dem Bild unter „Bildausschnitt“."));
        int sizeIdx = Math.Max(0, Array.FindIndex(DocSettings.Sizes, x => x.id == d.ImageSize));
        var (sizeTrack, sizes) = U.Segmented("dsize", sizeIdx, DocSettings.Sizes.Select(x => ((string?)null, x.name)).ToArray());
        for (int i = 0; i < sizes.Length; i++) { string v = DocSettings.Sizes[i].id; sizes[i].Checked += (_, _) => DocChange(x => x.ImageSize = v); }
        images.Children.Add(U.Field("Größe", sizeTrack, "Je Schritt änderbar unter „Bild in der Anleitung“."));
        var (layTrack, lays) = U.Segmented("dlay", d.Layout == "side" ? 1 : 0, (null, "Unter dem Text"), (null, "Neben dem Text"));
        lays[0].Checked += (_, _) => DocChange(x => x.Layout = "below"); lays[1].Checked += (_, _) => DocChange(x => x.Layout = "side");
        images.Children.Add(U.Field("Anordnung", layTrack, "„Neben dem Text“ ergibt kompakte Anleitungen: Text links, Bild rechts."));

        // Gestaltung
        var look = Section("Gestaltung");
        var swatches = new WrapPanel();
        foreach (var (hex, name) in DocSettings.Accents)
        {
            bool on = string.Equals(hex, d.Accent, StringComparison.OrdinalIgnoreCase);
            var dot = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand, ToolTip = name,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), BorderBrush = on ? Theme.Fg : Theme.Line, BorderThickness = new Thickness(on ? 2.5 : 1)
            };
            string h = hex;
            dot.MouseLeftButtonUp += (_, _) => { if (!ro) DocChange(x => x.Accent = h, quiet: false); };
            swatches.Children.Add(dot);
        }
        look.Children.Add(U.Field("Akzentfarbe", swatches, "Für Titel, Kapitel und Schrittnummern."));
        string addr = guide?.Str("address") ?? AppSettingsAddress();
        var (adTrack, ads) = U.Segmented("daddr", addr == "informal" ? 1 : 0, (null, "Sie"), (null, "du"));
        ads[0].Checked += (_, _) => Change(e => GuideOf(e)["address"] = "formal"); ads[1].Checked += (_, _) => Change(e => GuideOf(e)["address"] = "informal");
        look.Children.Add(U.Field("Anrede in automatischen Texten", adTrack, "Von Ihnen geänderte Texte bleiben unverändert."));
        look.Children.Add(U.Field("Fußzeile", CommitBox(d.Footer, v => LocDocChange("footer", v), "z. B. Firma · intern"), "Links in der Fußzeile; rechts steht die Seitenzahl (Word, PDF)."));

        // Vorlage
        var st = StepRecorder.Settings.AppSettings.Load();
        var tplBox = Section("Vorlage", st.DefaultTemplate.Length > 0 ? $"Neue Aufnahmen erhalten automatisch die Vorlage „{st.DefaultTemplate}“." : "Logo, Farbe, Autor, Fußzeile, Aufbau und Bilder als Firmenlook für weitere Anleitungen übernehmen.");
        var tpl = new ComboBox { MinWidth = 180, IsEnabled = !ro && st.DocTemplates.Count > 0 };
        tpl.Items.Add(st.DocTemplates.Count == 0 ? "Noch keine Vorlage" : "Vorlage anwenden …");
        foreach (var name in st.DocTemplates.Keys.OrderBy(x => x)) tpl.Items.Add(name + (name == st.DefaultTemplate ? "  (Standard)" : ""));
        tpl.SelectedIndex = 0;
        tpl.SelectionChanged += (_, _) =>
        {
            if (tpl.SelectedIndex <= 0) return;
            var name = st.DocTemplates.Keys.OrderBy(x => x).ElementAt(tpl.SelectedIndex - 1);
            ApplyTemplate(st.DocTemplates[name]);
            _status.Text = $"Vorlage „{name}“ angewendet – rückgängig mit Strg+Z";
        };
        var save = U.Btn(Theme.ISave, "Als Vorlage speichern …", SaveTemplate, "Logo, Farbe, Autor, Fußzeile, Aufbau und Bilder als Vorlage für weitere Anleitungen speichern");
        var tplRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) }; tplRow.Children.Add(tpl); tplRow.Children.Add(save);
        tplBox.Children.Add(tplRow);

        // Weitere Aufnahmen
        var appended = (guide?.Arr("append") ?? new JsonArray()).OfType<JsonObject>().ToList();
        var more = Section("Weitere Aufnahmen", appended.Count == 0 ? "Längere Handbücher aus mehreren Aufnahmen: angehängte Aufnahmen erscheinen im Export als eigene Kapitel hinter dieser." : "Erscheinen im Export als eigene Kapitel hinter dieser Aufnahme:");
        for (int i = 0; i < appended.Count; i++)
        {
            var a = appended[i]; int idx = i;
            string p = a.Str("path") ?? "";
            string atitle = Session.LooksLikeSession(p) ? (Json.ReadObject(Path.Combine(p, "manifest.json"))?.Str("title") is { Length: > 0 } mt ? mt : Path.GetFileName(p)) : "nicht gefunden: " + Path.GetFileName(p);
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var del = U.Ghost(Theme.IDelete, null, () => Change(e => { if (GuideOf(e)["append"] is JsonArray arr && idx < arr.Count) arr.RemoveAt(idx); }, refill: false), "Nicht mehr anhängen (die Aufnahme selbst bleibt erhalten)");
            del.MinHeight = 30; del.Padding = new Thickness(7, 4, 7, 4); del.Margin = new Thickness(0);
            DockPanel.SetDock(del, Dock.Right); row.Children.Add(del);
            var rowText = U.Row(U.Text($"{idx + 1}.", Theme.FsHint, fg: Theme.Muted, margin: new Thickness(0, 0, 8, 0)), U.Text(atitle, Theme.FsHint, true)); rowText.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(rowText);
            row.ToolTip = p;
            more.Children.Add(row);
        }
        var appRow = new WrapPanel { Margin = new Thickness(-3, 0, 0, 12) };
        appRow.Children.Add(U.Btn(EditorCommands.Get("append").Glyph, "Aufnahme anhängen …", () => Run("append"), "Eine vorhandene Aufnahme hinten anhängen"));
        appRow.Children.Add(U.Btn(EditorCommands.Get("continueRec").Glyph, "Aufnahme fortsetzen", () => Run("continueRec"), "Jetzt weiter aufnehmen – die neue Aufnahme wird automatisch angehängt"));
        foreach (var b in appRow.Children.OfType<Button>()) b.IsEnabled = !ro;
        more.Children.Add(appRow);
    }

    /// <summary>Text in guide in der aktuellen Sprache setzen (Deutsch direkt, andere unter guide.i18n.&lt;lang&gt;).</summary>
    void SetGuideText(JsonObject e, string key, string value)
    {
        var g = GuideOf(e);
        if (_view.Language == "de") { g[key] = value; return; }
        var i18n = g["i18n"] as JsonObject ?? new JsonObject(); g["i18n"] = i18n;
        var loc = i18n[_view.Language] as JsonObject ?? new JsonObject(); i18n[_view.Language] = loc;
        loc[key] = value;
    }

    /// <summary>Sprachabhängiges Dokumentfeld (Untertitel, Zielgruppe, Voraussetzungen, Fußzeile).</summary>
    void LocDocChange(string key, string value)
    {
        if (_view.Language == "de") { DocChange(x => { switch (key) { case "subtitle": x.Subtitle = value; break; case "audience": x.Audience = value; break; case "prerequisites": x.Prerequisites = value; break; case "footer": x.Footer = value; break; } }); return; }
        QuietChange(e => SetGuideText(e, key, value));
    }

    void PickLogo()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Bilder|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
        if (dlg.ShowDialog(this) != true) return;
        try { using var img = System.Drawing.Image.FromFile(dlg.FileName); string b64 = DocSettings.LogoFrom(img); DocChange(x => x.Logo = b64, quiet: false); }
        catch (Exception ex) { Msg.Show("Das Logo lässt sich nicht öffnen:\n" + ex.Message); }
    }

    /// <summary>Felder, die eine Vorlage überträgt (nicht: Untertitel, Version, Voraussetzungen – die gehören zur einzelnen Anleitung).</summary>
    static readonly string[] TemplateKeys = { "author", "audience", "footer", "cover", "toc", "showDate", "numbering", "imageSize", "layout", "accent", "logo", "view", "overview" };

    void SaveTemplate()
    {
        var st = StepRecorder.Settings.AppSettings.Load();
        var d = new InputDialog("Name der Vorlage (z. B. Firmenname)", st.DefaultTemplate.Length > 0 ? st.DefaultTemplate : "Standard") { Owner = this };
        if (d.ShowDialog() != true || d.Value.Trim().Length == 0) return;
        string name = d.Value.Trim();
        var doc = DocSettings.From(_view.Edits.Obj("guide")).ToJson();
        var t = new JsonObject();
        foreach (var k in TemplateKeys) t[k] = doc[k]?.DeepClone();
        t["address"] = _view.Edits.Obj("guide")?.Str("address") ?? AppSettingsAddress();
        st.DocTemplates[name] = t;
        bool makeDefault = Msg.Show($"Vorlage „{name}“ gespeichert.\n\nSollen neue Aufnahmen diese Vorlage automatisch erhalten?", "Vorlage", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        if (makeDefault) st.DefaultTemplate = name;
        st.Save();
        if (_cover) DocDetails();
    }

    void ApplyTemplate(JsonObject t)
    {
        Change(e =>
        {
            var g = GuideOf(e);
            var cur = DocSettings.From(g).ToJson();
            foreach (var k in TemplateKeys) if (t[k] != null) cur[k] = t[k]!.DeepClone();
            g["doc"] = cur;
            if (t.Str("address") is { } a) g["address"] = a;
        }, refill: false);
        if (_cover) DocDetails();
    }

    /// <summary>Neue Aufnahme ohne Dokument-Einstellungen: Standardvorlage still übernehmen.</summary>
    void ApplyDefaultTemplate()
    {
        try
        {
            if (_view.Session.ReadOnly || _view.Edits.Obj("guide")?.Obj("doc") != null) return;
            var st = StepRecorder.Settings.AppSettings.Load();
            if (st.DefaultTemplate.Length == 0 || !st.DocTemplates.TryGetValue(st.DefaultTemplate, out var t)) return;
            var g = GuideOf(_view.Edits);
            var cur = new DocSettings().ToJson();
            foreach (var k in TemplateKeys) if (t[k] != null) cur[k] = t[k]!.DeepClone();
            g["doc"] = cur;
            if (t.Str("address") is { } a && g["address"] == null) g["address"] = a;
            _view.SaveEdits();
        }
        catch { }
    }

    void AddAppended(string path)
    {
        string parent = Path.GetDirectoryName(_view.Session.Path.TrimEnd('\\', '/')) ?? "";
        string rel = Path.GetRelativePath(parent, path);
        Change(e =>
        {
            var g = GuideOf(e);
            var arr = g["append"] as JsonArray ?? new JsonArray(); g["append"] = arr;
            if (!arr.OfType<JsonObject>().Any(x => string.Equals(x.Str("path"), path, StringComparison.OrdinalIgnoreCase)))
                arr.Add(new JsonObject { ["path"] = path, ["rel"] = rel, ["addedUtc"] = Json.UtcNow() });
        }, refill: false);
        if (_cover) DocDetails();
    }

    void AppendSession()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Aufnahme (manifest.json)|manifest.json", InitialDirectory = Path.GetDirectoryName(_view.Session.Path.TrimEnd('\\')) };
        if (dlg.ShowDialog(this) != true) return;
        var p = Path.GetDirectoryName(dlg.FileName)!;
        if (string.Equals(Path.GetFullPath(p).TrimEnd('\\'), Path.GetFullPath(_view.Session.Path).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) { Msg.Show("Das ist diese Aufnahme selbst."); return; }
        AddAppended(p);
        _status.Text = "Aufnahme angehängt – sie erscheint im Export als eigenes Kapitel.";
    }

    /// <summary>Weiter aufnehmen; die neue Aufnahme wird nach dem Stopp an diese angehängt und der Editor kehrt hierher zurück.</summary>
    void ContinueRecording()
    {
        if (_view.Session.ReadOnly) { Msg.Show("Diese Sitzung ist schreibgeschützt."); return; }
        var st = StepRecorder.Settings.AppSettings.Load();
        var o = UiApp.OptionsFromSettings(StepRecorder.Capture.ScopeMode.Screen);
        UiApp.ApplyProfile(o, st.LastProfile);
        o.Scope = StepRecorder.Capture.ScopeMode.Screen;
        o.Title = _view.Title + " (Fortsetzung)";
        UiApp.Instance.StartRecording(o, folder =>
        {
            Show(); Activate();
            if (folder != null) { AddAppended(folder); Msg.Show("Fortsetzung angehängt\n\nDie neue Aufnahme erscheint im Export als eigenes Kapitel. Bearbeiten lässt sie sich über „Öffnen“ wie jede andere Aufnahme.", "Aufnahme fortsetzen", MessageBoxButton.OK, MessageBoxImage.Information); }
        });
    }

    static string AppSettingsAddress() { try { return StepRecorder.Settings.AppSettings.Load().Address; } catch { return "formal"; } }

    /// <summary>HTML erzeugen und im Standardbrowser öffnen.</summary>
    void PreviewDocument()
    {
        InlineCommit(now: true);
        Keyboard.ClearFocus();
        if (_view.UnreviewedCount > 0)
        {
            if (Msg.Show($"Vorschau erst nach der Durchsicht\n\n{_view.UnreviewedCount} auffällige Eingaben sind noch nicht durchgesehen. Jetzt durchsehen?", "Vorschau", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) ShowReview();
            return;
        }
        var old = Cursor; Cursor = Cursors.Wait;
        try
        {
            string dir = Path.Combine(Path.GetTempPath(), "StepRecorder-Vorschau");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, Fmt.FileSafe(_view.Title.Length > 0 ? _view.Title : "Anleitung") + ".html");
            var r = GuideExport.Export(_view, new GuideOptions { Format = "html", OutPath = file });
            Process.Start(new ProcessStartInfo(r.Path!) { UseShellExecute = true });
            _status.Text = "Vorschau geöffnet: " + r.Path;
        }
        catch (Exception ex) { UiApp.ShowError(ex); }
        finally { Cursor = old; }
    }

    // =====================================================================================
    // Pro Schritt: Kapitel und Hinweise (Oberfläche in ViewerCover.cs)
    // =====================================================================================

    /// <summary>Kapitelobjekt zum Schreiben: Deutsch direkt, andere Sprachen unter chapter.&lt;lang&gt;.</summary>
    JsonObject ChapterTarget(JsonObject e, long anchor)
    {
        var se = StepEdit(e, anchor);
        var c = se["chapter"] as JsonObject ?? new JsonObject(); se["chapter"] = c;
        if (_view.Language == "de") return c;
        var loc = c[_view.Language] as JsonObject ?? new JsonObject(); c[_view.Language] = loc;
        return loc;
    }

    void AddNoteBox(long anchor, string kind)
    {
        string? id = null;
        Change(e =>
        {
            var arr = e["comments"] as JsonArray ?? new JsonArray();
            id = NextId(e, "comments", "uc");
            arr.Add(new JsonObject { ["id"] = id, ["anchor"] = anchor, ["kind"] = kind, ["text"] = "", ["createdUtc"] = Json.UtcNow(), ["by"] = "user", ["origin"] = "viewer" });
            e["comments"] = arr;
            _focusCommentId = id;
        }, refill: false);
    }

    void ToggleChapter(long anchor, bool on)
    {
        if (on) _status.Text = "Kapitel angelegt – Titel rechts eingeben.";
        Change(e =>
        {
            var se = StepEdit(e, anchor);
            if (on) se["chapter"] ??= new JsonObject { ["title"] = "Neues Kapitel", ["text"] = "" };
            else se.Remove("chapter");
        });
    }
}
