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
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Editor für Bedienungsanleitungen: Reiter „Dokument“ (Titelseite, Einleitung, Voraussetzungen, Abschluss,
/// Aufbau, Bilder, Gestaltung) sowie pro Schritt Kapitelbeginn, Bildoptionen und Hinweiskästen.
/// </summary>
sealed partial class ViewerWindow
{
    string? _focusCommentId;

    static (string kind, string name)[] NoteKinds => new[] { ("note", Loc.L("Hinweis", "Note")), ("tip", Loc.L("Tipp", "Tip")), ("warning", Loc.L("Achtung", "Warning")), ("important", Loc.L("Wichtig", "Important")), ("result", Loc.L("Ergebnis", "Result")) };

    static JsonObject GuideOf(JsonObject edits) { var g = edits["guide"] as JsonObject ?? new JsonObject(); edits["guide"] = g; return g; }

    /// <summary>Speichert ohne die Oberfläche neu aufzubauen (Eingabefelder behalten den Fokus); Liste wird nachgezogen, sobald der Fokus den Detailbereich verlässt.</summary>
    void QuietChange(Action<JsonObject> edit, bool refillLater = false)
    {
        if (_view.Session.ReadOnly) return;
        _undo.Add(Json.Line(_view.Edits)); _redo.Clear();
        edit(_view.Edits);
        if (!_view.SaveEdits()) { _view.ReloadEdits(); Msg.Show(Loc.L("Die Sitzung wurde zwischenzeitlich geändert; bitte erneut versuchen.", "The session was changed in the meantime; please try again.")); }
        UpdateUndo();
        _status.Text = Loc.L("Gespeichert", "Saved");
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
        var t = new TextBox { Text = value, Tag = placeholder, IsReadOnly = _view.Session.ReadOnly, TextWrapping = TextWrapping.Wrap, MaxLength = 4000 };
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
    // Reiter „Dokument“
    // =====================================================================================

    void DocDetails()
    {
        _details.Children.Clear();
        _editTitle = _editBody = null; _editAnchor = null;
        var guide = _view.Edits.Obj("guide");
        string lang = _view.Language;
        var d = DocSettings.From(guide, lang);
        string GS(string key) => DocSettings.GuideText(guide, key, lang) ?? "";
        bool en = lang != "de";

        var intro = U.Text(Loc.L("Alles, was aus den Schritten eine fertige Bedienungsanleitung macht. Gilt für HTML, Word, PDF, Markdown und GIF.", "Everything that turns the steps into a finished user guide. Applies to HTML, Word, PDF, Markdown and GIF."), 12.5, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 10));
        _details.Children.Add(intro);
        if (en) _details.Children.Add(Callout(Theme.IGlobe, Theme.AccentSoft, Theme.AccentText, Loc.L("Englische Fassung", "English version"), Loc.L("Titel, Untertitel, Zielgruppe, Texte, Voraussetzungen und Fußzeile gelten hier nur für Englisch. Leer gelassene Felder übernehmen die deutsche Fassung.", "Title, subtitle, audience, texts, prerequisites and footer apply to English only. Fields left empty use the German version.")));
        var actions = new WrapPanel { Margin = new Thickness(-3, 0, 0, 6) };
        actions.Children.Add(U.Btn(Theme.IEye, Loc.L("Vorschau im Browser", "Preview in browser"), PreviewDocument, Loc.L("Anleitung als HTML erzeugen und im Standardbrowser öffnen", "Create the guide as HTML and open it in the default browser"), "Primary"));
        actions.Children.Add(U.Btn(Theme.IExport, Loc.L("Exportieren …", "Export …"), () => ShowExportDialog("pdf"), Loc.L("Als PDF, Word, HTML, Markdown oder GIF speichern (Strg+E)", "Save as PDF, Word, HTML, Markdown or GIF (Ctrl+E)")));
        _details.Children.Add(actions);

        // Vorlagen
        _details.Children.Add(U.Overline(Loc.L("Vorlage", "Template"), new Thickness(0, 14, 0, 8)));
        var st = StepRecorder.Settings.AppSettings.Load();
        var tpl = new ComboBox { MinWidth = 180, IsEnabled = !_view.Session.ReadOnly };
        tpl.Items.Add(st.DocTemplates.Count == 0 ? Loc.L("Noch keine Vorlage", "No template yet") : Loc.L("Vorlage anwenden …", "Apply template …"));
        foreach (var name in st.DocTemplates.Keys.OrderBy(x => x)) tpl.Items.Add(name + (name == st.DefaultTemplate ? Loc.L("  (Standard)", "  (default)") : ""));
        tpl.SelectedIndex = 0;
        tpl.SelectionChanged += (_, _) =>
        {
            if (tpl.SelectedIndex <= 0) return;
            var name = st.DocTemplates.Keys.OrderBy(x => x).ElementAt(tpl.SelectedIndex - 1);
            ApplyTemplate(st.DocTemplates[name]);
            _status.Text = Loc.F("Vorlage „{0}“ angewendet – rückgängig mit Strg+Z", "Template “{0}” applied – undo with Ctrl+Z", name);
        };
        var save = U.Btn(Theme.ISave, Loc.L("Als Vorlage speichern …", "Save as template …"), SaveTemplate, Loc.L("Logo, Farbe, Autor, Fußzeile, Aufbau und Bilder als Vorlage für weitere Anleitungen speichern", "Save logo, color, author, footer, layout and images as a template for further guides"));
        var tplRow = new WrapPanel(); tplRow.Children.Add(tpl); tplRow.Children.Add(save);
        _details.Children.Add(U.Field(Loc.L("Firmenlook übernehmen", "Apply corporate look"), tplRow, st.DefaultTemplate.Length > 0 ? Loc.F("Neue Aufnahmen erhalten automatisch die Vorlage „{0}“.", "New recordings automatically get the template “{0}”.", st.DefaultTemplate) : Loc.L("Eine Vorlage kann als Standard für neue Aufnahmen gelten.", "A template can be the default for new recordings.")));

        // Titelseite
        _details.Children.Add(U.Overline(Loc.L("Titelseite", "Cover page"), new Thickness(0, 10, 0, 8)));
        _details.Children.Add(U.Field(Loc.L("Titel", "Title"), CommitBox(GS("title") is { Length: > 0 } tt ? tt : _view.Title, v => { if (v.Length == 0) return; QuietChange(e => SetGuideText(e, "title", v)); _titleBlock.Text = _view.Title; }, Loc.L("Titel der Anleitung", "Guide title"))));
        _details.Children.Add(U.Field(Loc.L("Untertitel", "Subtitle"), CommitBox(d.Subtitle, v => LocDocChange("subtitle", v), Loc.L("z. B. Kurzanleitung für neue Mitarbeitende", "e.g. Quick guide for new employees"))));
        var two = new Grid();
        two.ColumnDefinitions.Add(new ColumnDefinition()); two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); two.ColumnDefinitions.Add(new ColumnDefinition());
        var ver = U.Field("Version", CommitBox(d.Version, v => DocChange(x => x.Version = v), Loc.L("z. B. 1.0", "e.g. 1.0")));
        var aud = U.Field(Loc.L("Zielgruppe", "Audience"), CommitBox(d.Audience, v => LocDocChange("audience", v), Loc.L("z. B. Vertrieb", "e.g. Sales")));
        Grid.SetColumn(aud, 2); two.Children.Add(ver); two.Children.Add(aud);
        _details.Children.Add(two);
        _details.Children.Add(U.Field(Loc.L("Autor oder Firma", "Author or company"), CommitBox(d.Author, v => DocChange(x => x.Author = v), Loc.L("erscheint auf der Titelseite", "appears on the cover page"))));
        // Logo
        var logoRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        if (d.LogoBytes() is { } lb)
        {
            try
            {
                var bi = new System.Windows.Media.Imaging.BitmapImage(); bi.BeginInit(); bi.StreamSource = new MemoryStream(lb); bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bi.EndInit(); bi.Freeze();
                logoRow.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(4), Padding = new Thickness(6), Margin = new Thickness(0, 3, 8, 3), BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Child = new Image { Source = bi, MaxHeight = 40, MaxWidth = 140 } });
            }
            catch { }
        }
        logoRow.Children.Add(U.Btn(Theme.IOpen, d.Logo.Length > 0 ? Loc.L("Anderes Logo …", "Different logo …") : Loc.L("Logo wählen …", "Choose logo …"), PickLogo, Loc.L("PNG, JPG oder BMP – erscheint auf Titelseite bzw. im Kopf", "PNG, JPG or BMP – appears on the cover page or in the header")));
        if (d.Logo.Length > 0) logoRow.Children.Add(U.Btn(Theme.IDelete, Loc.L("Entfernen", "Remove"), () => DocChange(x => x.Logo = "", quiet: false)));
        _details.Children.Add(U.Field("Logo", logoRow));

        // Texte
        _details.Children.Add(U.Overline(Loc.L("Texte", "Texts"), new Thickness(0, 10, 0, 8)));
        _details.Children.Add(U.Field(Loc.L("Über diese Anleitung", "About this guide"), CommitBox(GS("intro"), v => QuietChange(e => SetGuideText(e, "intro", v)), Loc.L("Wozu dient die Anleitung, was ist das Ergebnis?", "What is the guide for, and what is the result?"), true),
            Loc.L("Ziel und erwartetes Ergebnis der Aufnahme werden automatisch ergänzt.", "The goal and expected result of the recording are added automatically.")));
        _details.Children.Add(U.Field(Loc.L("Voraussetzungen", "Prerequisites"), CommitBox(d.Prerequisites, v => LocDocChange("prerequisites", v), Loc.L("Eine pro Zeile, z. B. Zugang zum Portal", "One per line, e.g. Access to the portal"), true),
            Loc.L("Erscheint als Aufzählung vor dem ersten Schritt.", "Appears as a list before the first step.")));
        _details.Children.Add(U.Field(Loc.L("Abschluss", "Closing"), CommitBox(GS("outro"), v => QuietChange(e => SetGuideText(e, "outro", v)), Loc.L("z. B. Fertig! Bei Fragen: Servicedesk, Tel. …", "e.g. Done! Questions? Service desk, phone …"), true)));

        // Aufbau
        _details.Children.Add(U.Overline(Loc.L("Aufbau", "Layout"), new Thickness(0, 10, 0, 8)));
        CheckBox Check(string text, bool value, Action<bool> set, string tip)
        {
            var c = new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(0, 0, 0, 8), ToolTip = tip, IsEnabled = !_view.Session.ReadOnly };
            c.Checked += (_, _) => set(true); c.Unchecked += (_, _) => set(false);
            return c;
        }
        _details.Children.Add(Check(Loc.L("Deckblatt", "Cover page"), d.Cover, v => DocChange(x => x.Cover = v), Loc.L("Eigene Titelseite mit Logo, Titel, Untertitel, Version, Autor und Datum", "Separate cover page with logo, title, subtitle, version, author and date")));
        _details.Children.Add(Check(Loc.L("Inhaltsverzeichnis", "Table of contents"), d.Toc, v => DocChange(x => x.Toc = v), Loc.L("Mit Kapiteln und Schritten; im PDF mit Seitenzahlen. Erscheint ab 3 Kapiteln oder 6 Schritten.", "With chapters and steps; in the PDF with page numbers. Appears from 3 chapters or 6 steps.")));
        _details.Children.Add(Check(Loc.L("Datum anzeigen", "Show date"), d.ShowDate, v => DocChange(x => x.ShowDate = v), Loc.L("Stand-Datum auf Titelseite bzw. im Kopf", "Date of issue on the cover page or in the header")));
        int numIdx = d.Numbering switch { "flat" => 1, "none" => 2, _ => 0 };
        var (numTrack, nums) = U.Segmented("dnum", numIdx, (null, "1.1, 1.2 …"), (null, "1, 2, 3 …"), (null, Loc.L("Keine", "None")));
        for (int i = 0; i < 3; i++) { string v = new[] { "chapter", "flat", "none" }[i]; nums[i].Checked += (_, _) => DocChange(x => x.Numbering = v); }
        _details.Children.Add(U.Field(Loc.L("Nummerierung der Schritte", "Step numbering"), numTrack, Loc.L("„1.1“ zählt innerhalb der Kapitel – Kapitel legen Sie am Schritt fest (Reiter „Schritt“ › Kapitel).", "“1.1” counts within chapters – you set chapters on the step (“Step” tab › Chapter).")));

        // Bilder
        _details.Children.Add(U.Overline(Loc.L("Bilder", "Images"), new Thickness(0, 10, 0, 8)));
        var viewBox = new ComboBox { MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = !_view.Session.ReadOnly };
        foreach (var (_, name) in DocSettings.Views) viewBox.Items.Add(name);
        viewBox.SelectedIndex = Math.Max(0, Array.FindIndex(DocSettings.Views, x => x.id == d.View));
        viewBox.SelectionChanged += (_, _) => { string v = DocSettings.Views[Math.Max(0, viewBox.SelectedIndex)].id; DocChange(x => x.View = v); };
        var ov = new CheckBox { Content = Loc.L("Im Ausschnitt eine kleine Übersicht des ganzen Bildschirms zeigen", "Show a small overview of the whole screen in the detail"), IsChecked = d.Overview, Margin = new Thickness(0, 8, 0, 0), IsEnabled = !_view.Session.ReadOnly };
        ov.Checked += (_, _) => DocChange(x => x.Overview = true); ov.Unchecked += (_, _) => DocChange(x => x.Overview = false);
        _details.Children.Add(U.Field(Loc.L("Bildausschnitt", "Image area"), U.Col(viewBox, ov), Loc.L("„Bereich um die Aktion“ zeigt je Schritt nur die Umgebung von Klick oder Eingabe – gut lesbar, auch auf kleinen Seiten. Im Editor unter „Ganzer Bildschirm ▾“ vorschauen.", "“Area around the action” shows only the surroundings of the click or input for each step – easy to read, even on small pages. Preview it in the editor under “Full screen ▾”.")));
        int sizeIdx = Math.Max(0, Array.FindIndex(DocSettings.Sizes, x => x.id == d.ImageSize));
        var (sizeTrack, sizes) = U.Segmented("dsize", sizeIdx, DocSettings.Sizes.Select(x => ((string?)null, x.name)).ToArray());
        for (int i = 0; i < sizes.Length; i++) { string v = DocSettings.Sizes[i].id; sizes[i].Checked += (_, _) => DocChange(x => x.ImageSize = v); }
        _details.Children.Add(U.Field(Loc.L("Größe", "Size"), sizeTrack, Loc.L("Pro Schritt änderbar (Reiter „Schritt“ › Im Dokument).", "Can be changed per step (“Step” tab › In document).")));
        var (layTrack, lays) = U.Segmented("dlay", d.Layout == "side" ? 1 : 0, (null, Loc.L("Unter dem Text", "Below the text")), (null, Loc.L("Neben dem Text", "Beside the text")));
        lays[0].Checked += (_, _) => DocChange(x => x.Layout = "below"); lays[1].Checked += (_, _) => DocChange(x => x.Layout = "side");
        _details.Children.Add(U.Field(Loc.L("Anordnung", "Arrangement"), layTrack, Loc.L("„Neben dem Text“ ergibt kompakte Anleitungen: Text links, Bild rechts.", "“Beside the text” makes compact guides: text on the left, image on the right.")));

        // Gestaltung
        _details.Children.Add(U.Overline(Loc.L("Gestaltung", "Design"), new Thickness(0, 10, 0, 8)));
        var swatches = new WrapPanel();
        foreach (var (hex, name) in DocSettings.Accents)
        {
            bool on = string.Equals(hex, d.Accent, StringComparison.OrdinalIgnoreCase);
            var dot = new Border
            {
                Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand, ToolTip = name,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), BorderBrush = on ? Theme.Fg : Theme.Line, BorderThickness = new Thickness(on ? 2.5 : 1)
            };
            string h = hex;
            dot.MouseLeftButtonUp += (_, _) => { DocChange(x => x.Accent = h, quiet: false); };
            swatches.Children.Add(dot);
        }
        _details.Children.Add(U.Field(Loc.L("Akzentfarbe", "Accent color"), swatches, Loc.L("Für Titel, Kapitel und Schrittnummern.", "For titles, chapters and step numbers.")));
        string addr = guide?.Str("address") ?? AppSettingsAddress();
        var (adTrack, ads) = U.Segmented("daddr", addr == "informal" ? 1 : 0, (null, Loc.L("Sie", "Formal (Sie)")), (null, Loc.L("du", "Informal (du)")));
        ads[0].Checked += (_, _) => Change(e => GuideOf(e)["address"] = "formal"); ads[1].Checked += (_, _) => Change(e => GuideOf(e)["address"] = "informal");
        _details.Children.Add(U.Field(Loc.L("Anrede in automatischen Texten", "Form of address in automatic texts"), adTrack, Loc.L("Von Ihnen geänderte Texte bleiben unverändert.", "Texts you have changed stay unchanged.")));
        _details.Children.Add(U.Field(Loc.L("Fußzeile", "Footer"), CommitBox(d.Footer, v => LocDocChange("footer", v), Loc.L("z. B. Firma · intern", "e.g. Company · internal")), Loc.L("Links in der Fußzeile; rechts steht die Seitenzahl (Word, PDF).", "On the left in the footer; the page number is on the right (Word, PDF).")));

        // Weitere Aufnahmen
        _details.Children.Add(U.Overline(Loc.L("Weitere Aufnahmen", "More recordings"), new Thickness(0, 10, 0, 8)));
        var appended = (guide?.Arr("append") ?? new JsonArray()).OfType<JsonObject>().ToList();
        _details.Children.Add(U.Text(appended.Count == 0 ? Loc.L("Längere Handbücher aus mehreren Aufnahmen: angehängte Aufnahmen erscheinen im Export als eigene Kapitel hinter dieser.", "Longer manuals from several recordings: appended recordings appear in the export as separate chapters after this one.") : Loc.L("Erscheinen im Export als eigene Kapitel hinter dieser Aufnahme:", "Appear in the export as separate chapters after this recording:"), 12, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 6)));
        for (int i = 0; i < appended.Count; i++)
        {
            var a = appended[i]; int idx = i;
            string p = a.Str("path") ?? "";
            string title = Session.LooksLikeSession(p) ? (Json.ReadObject(Path.Combine(p, "manifest.json"))?.Str("title") is { Length: > 0 } mt ? mt : Path.GetFileName(p)) : Loc.L("nicht gefunden: ", "not found: ") + Path.GetFileName(p);
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var del = U.Ghost(Theme.IDelete, null, () => Change(e => { if (GuideOf(e)["append"] is JsonArray arr && idx < arr.Count) arr.RemoveAt(idx); }, refill: false), Loc.L("Nicht mehr anhängen (die Aufnahme selbst bleibt erhalten)", "Stop appending (the recording itself is kept)"));
            del.MinHeight = 26; del.Padding = new Thickness(6, 3, 6, 3); del.Margin = new Thickness(0);
            DockPanel.SetDock(del, Dock.Right); row.Children.Add(del);
            row.Children.Add(U.Row(U.Text($"{idx + 1}.", 12.5, fg: Theme.Muted, margin: new Thickness(0, 0, 8, 0)), U.Text(title, 12.5, true)));
            row.ToolTip = p;
            _details.Children.Add(row);
        }
        var appRow = new WrapPanel { Margin = new Thickness(-3, 0, 0, 0) };
        appRow.Children.Add(U.Btn(Theme.IAdd, Loc.L("Aufnahme anhängen …", "Append recording …"), AppendSession, Loc.L("Eine vorhandene Aufnahme hinten anhängen", "Append an existing recording at the end")));
        appRow.Children.Add(U.Btn(Theme.IRecord, Loc.L("Aufnahme fortsetzen", "Continue recording"), ContinueRecording, Loc.L("Jetzt weiter aufnehmen – die neue Aufnahme wird automatisch angehängt", "Keep recording now – the new recording is appended automatically")));
        _details.Children.Add(appRow);
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
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = Loc.L("Bilder", "Images") + "|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
        if (dlg.ShowDialog(this) != true) return;
        try { using var img = System.Drawing.Image.FromFile(dlg.FileName); string b64 = DocSettings.LogoFrom(img); DocChange(x => x.Logo = b64, quiet: false); }
        catch (Exception ex) { Msg.Show(Loc.L("Das Logo lässt sich nicht öffnen:\n", "The logo cannot be opened:\n") + ex.Message); }
    }

    static string[] TemplateKeys => DocSettings.TemplateKeys;

    void SaveTemplate()
    {
        var st = StepRecorder.Settings.AppSettings.Load();
        var d = new InputDialog(Loc.L("Name der Vorlage (z. B. Firmenname)", "Template name (e.g. company name)"), st.DefaultTemplate.Length > 0 ? st.DefaultTemplate : Loc.L("Standard", "Default")) { Owner = this };
        if (d.ShowDialog() != true || d.Value.Trim().Length == 0) return;
        string name = d.Value.Trim();
        var doc = DocSettings.From(_view.Edits.Obj("guide")).ToJson();
        var t = new JsonObject();
        foreach (var k in TemplateKeys) t[k] = doc[k]?.DeepClone();
        t["address"] = _view.Edits.Obj("guide")?.Str("address") ?? AppSettingsAddress();
        st.DocTemplates[name] = t;
        bool makeDefault = Msg.Show(Loc.F("Vorlage „{0}“ gespeichert.\n\nSollen neue Aufnahmen diese Vorlage automatisch erhalten?", "Template “{0}” saved.\n\nShould new recordings get this template automatically?", name), Loc.L("Vorlage", "Template"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        if (makeDefault) st.DefaultTemplate = name;
        st.Save();
        if (_detailTab == 2) DocDetails();
    }

    void ApplyTemplate(JsonObject t)
    {
        _view.SnapshotBefore = "template";
        Change(e =>
        {
            var g = GuideOf(e);
            var cur = DocSettings.From(g).ToJson();
            foreach (var k in TemplateKeys) if (t[k] != null) cur[k] = t[k]!.DeepClone();
            g["doc"] = cur;
            if (t.Str("address") is { } a) g["address"] = a;
        }, refill: false);
        if (_detailTab == 2) DocDetails();
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
        if (_detailTab == 2) DocDetails();
    }

    void AppendSession()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = Loc.L("Aufnahme", "Recording") + " (manifest.json)|manifest.json", InitialDirectory = Path.GetDirectoryName(_view.Session.Path.TrimEnd('\\')) };
        if (dlg.ShowDialog(this) != true) return;
        var p = Path.GetDirectoryName(dlg.FileName)!;
        if (string.Equals(Path.GetFullPath(p).TrimEnd('\\'), Path.GetFullPath(_view.Session.Path).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) { Msg.Show(Loc.L("Das ist diese Aufnahme selbst.", "This is the recording itself.")); return; }
        AddAppended(p);
        _status.Text = Loc.L("Aufnahme angehängt – sie erscheint im Export als eigenes Kapitel.", "Recording appended – it appears in the export as a separate chapter.");
    }

    /// <summary>Weiter aufnehmen; die neue Aufnahme wird nach dem Stopp an diese angehängt und der Editor kehrt hierher zurück.</summary>
    void ContinueRecording()
    {
        if (_view.Session.ReadOnly) { Msg.Show(Loc.L("Diese Sitzung ist schreibgeschützt.", "This session is read-only.")); return; }
        var st = StepRecorder.Settings.AppSettings.Load();
        var o = UiApp.OptionsFromSettings(StepRecorder.Capture.ScopeMode.Screen);
        UiApp.ApplyProfile(o, st.LastProfile);
        o.Scope = StepRecorder.Capture.ScopeMode.Screen;
        o.Title = _view.Title + (_view.Language == "en" ? " (continued)" : " (Fortsetzung)");
        UiApp.Instance.StartRecording(o, folder =>
        {
            Show(); Activate();
            if (folder != null) { AddAppended(folder); Msg.Show(Loc.L("Fortsetzung angehängt\n\nDie neue Aufnahme erscheint im Export als eigenes Kapitel. Bearbeiten lässt sie sich über „Öffnen“ wie jede andere Aufnahme.", "Continuation appended\n\nThe new recording appears in the export as a separate chapter. You can edit it via “Open” like any other recording."), Loc.L("Aufnahme fortsetzen", "Continue recording"), MessageBoxButton.OK, MessageBoxImage.Information); }
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
            if (Msg.Show(Loc.F("Vorschau erst nach der Durchsicht\n\n{0} auffällige Eingaben sind noch nicht durchgesehen. Jetzt durchsehen?", "Preview only after the review\n\n{0} conspicuous inputs have not been reviewed yet. Review them now?", _view.UnreviewedCount), Loc.L("Vorschau", "Preview"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) ShowReview();
            return;
        }
        var old = Cursor; Cursor = Cursors.Wait;
        try
        {
            // Zwischendateien liegen im Aufnahmeordner, nicht in %TEMP% (E13); nur schreibgeschützte Archive weichen aus
            string dir = _view.Session.ReadOnly ? Path.Combine(Path.GetTempPath(), "StepRecorder-Vorschau") : Path.Combine(_view.Session.Path, "export", ".tmp", "vorschau");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, Fmt.FileSafe(_view.Title.Length > 0 ? _view.Title : Loc.For(_view.Language, "Anleitung", "Guide")) + ".html");
            var r = GuideExport.Export(_view, new GuideOptions { Format = "html", OutPath = file });
            Process.Start(new ProcessStartInfo(r.Path!) { UseShellExecute = true });
            _status.Text = Loc.L("Vorschau geöffnet: ", "Preview opened: ") + r.Path;
        }
        catch (Exception ex) { UiApp.ShowError(ex); }
        finally { Cursor = old; }
    }

    // =====================================================================================
    // Pro Schritt: Kapitel, Bild im Dokument, Hinweiskästen
    // =====================================================================================

    void StepDocSection(ViewStep s)
    {
        bool ro = _view.Session.ReadOnly;
        long anchor = s.Anchor;

        // Kapitel
        _details.Children.Add(U.Overline(Loc.L("Kapitel", "Chapter"), new Thickness(0, 18, 0, 8)));
        var chap = new CheckBox { Content = Loc.L("Hier beginnt ein neues Kapitel", "A new chapter starts here"), IsChecked = s.ChapterTitle != null, IsEnabled = !ro, ToolTip = Loc.L("Gliedert die Anleitung: Kapitelüberschrift mit Einleitung vor diesem Schritt, Eintrag im Inhaltsverzeichnis", "Structures the guide: chapter heading with introduction before this step, entry in the table of contents") };
        chap.Checked += (_, _) => ToggleChapter(anchor, true);
        chap.Unchecked += (_, _) => ToggleChapter(anchor, false);
        _details.Children.Add(chap);
        if (s.ChapterTitle != null)
        {
            var ct = CommitBox(s.ChapterTitle, v => { if (v.Length > 0) QuietChange(e => ChapterTarget(e, anchor)["title"] = v, refillLater: true); }, Loc.L("Titel des Kapitels", "Chapter title"));
            ct.FontWeight = FontWeights.SemiBold; ct.Margin = new Thickness(0, 8, 0, 0);
            var cx = CommitBox(s.ChapterText ?? "", v => QuietChange(e => ChapterTarget(e, anchor)["text"] = v), Loc.L("Kurze Einleitung zum Kapitel (optional)", "Short introduction to the chapter (optional)"), true, 48);
            cx.Margin = new Thickness(0, 6, 0, 0);
            _details.Children.Add(ct); _details.Children.Add(cx);
        }

        // Bild im Dokument
        _details.Children.Add(U.Overline(Loc.L("Im Dokument", "In document"), new Thickness(0, 18, 0, 8)));
        var row = new DockPanel();
        var show = new CheckBox { Content = Loc.L("Bild zeigen", "Show image"), IsChecked = s.ShowImage != false, IsEnabled = !ro, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0), ToolTip = Loc.L("Für reine Textschritte ausschalten", "Turn off for text-only steps") };
        show.Checked += (_, _) => Change(e => StepEdit(e, anchor).Remove("showImage"), refill: false);
        show.Unchecked += (_, _) => Change(e => StepEdit(e, anchor)["showImage"] = false, refill: false);
        DockPanel.SetDock(show, Dock.Left); row.Children.Add(show);
        var size = new ComboBox { MinWidth = 150, IsEnabled = !ro && s.ShowImage != false, ToolTip = Loc.L("Bildgröße nur für diesen Schritt", "Image size for this step only") };
        size.Items.Add(Loc.L("Größe wie im Dokument", "Size as in document"));
        foreach (var (_, name) in DocSettings.Sizes) size.Items.Add(Loc.L("Bild ", "Image ") + name);
        size.SelectedIndex = s.ImageSize == null ? 0 : 1 + Math.Max(0, Array.FindIndex(DocSettings.Sizes, x => x.id == s.ImageSize));
        size.SelectionChanged += (_, _) =>
        {
            int i = size.SelectedIndex;
            Change(e => { if (i <= 0) StepEdit(e, anchor).Remove("imageSize"); else StepEdit(e, anchor)["imageSize"] = DocSettings.Sizes[i - 1].id; }, refill: false);
        };
        row.Children.Add(size);
        _details.Children.Add(row);
        var imgActs = new WrapPanel { Margin = new Thickness(-3, 6, 0, 0) };
        Button? repl = null;
        repl = U.Btn(Theme.ICamera, s.Manual && s.PreFrame == null ? Loc.L("Bild hinzufügen …", "Add image …") : Loc.L("Bild ersetzen …", "Replace image …"), () => ReplaceImageMenu(s, repl), Loc.L("Bildschirmfoto neu aufnehmen oder Bild aus Datei bzw. Zwischenablage nehmen", "Take a new screenshot or use an image from a file or the clipboard"));
        repl.IsEnabled = !ro; imgActs.Children.Add(repl);
        if (s.ImageReplaced) imgActs.Children.Add(U.Btn(Theme.IUndo, Loc.L("Originalbild", "Original image"), () => Change(e => StepEdit(e, anchor).Remove("frameChoice"), refill: false), Loc.L("Aufgenommenes Bild wiederherstellen", "Restore the recorded image")));
        if (s.Manual) imgActs.Children.Add(U.Btn(Theme.IDelete, Loc.L("Schritt löschen", "Delete step"), () => DeleteManual(new List<ViewStep> { s }), Loc.L("Eingefügten Schritt löschen", "Delete inserted step")));
        _details.Children.Add(imgActs);

        // Hinweiskästen
        _details.Children.Add(U.Overline(s.Comments.Count > 0 ? Loc.F("Hinweiskästen ({0})", "Notes ({0})", s.Comments.Count) : Loc.L("Hinweiskästen", "Notes"), new Thickness(0, 18, 0, 8)));
        TextBox? focus = null;
        foreach (var c in s.Comments)
        {
            string? cid = c.Str("id");
            string kind = c.Str("kind") ?? "note";
            var (_, bar, _) = GuideExport.NoteStyle(kind, "de");
            var barBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + bar));
            var kinds = new ComboBox { MinWidth = 120, IsEnabled = !ro, Margin = new Thickness(0, 0, 6, 0) };
            foreach (var (_, name) in NoteKinds) kinds.Items.Add(name);
            kinds.SelectedIndex = Math.Max(0, Array.FindIndex(NoteKinds, k => k.kind == kind));
            kinds.SelectionChanged += (_, _) => { string k = NoteKinds[Math.Max(0, kinds.SelectedIndex)].kind; Change(e => { if (e["comments"] is JsonArray arr) foreach (var x in arr.OfType<JsonObject>().Where(x => x.Str("id") == cid)) x["kind"] = k; }, refill: false); };
            var del = U.Ghost(Theme.IDelete, null, () => RemoveComment(cid), Loc.L("Hinweiskasten löschen", "Delete note"));
            del.MinHeight = 28; del.Padding = new Thickness(7, 4, 7, 4); del.Margin = new Thickness(0);
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            DockPanel.SetDock(del, Dock.Right); head.Children.Add(del); head.Children.Add(kinds);
            kinds.HorizontalAlignment = HorizontalAlignment.Left;
            var text = CommitBox(c.Str("text") ?? "", v => QuietChange(e => { if (e["comments"] is JsonArray arr) foreach (var x in arr.OfType<JsonObject>().Where(x => x.Str("id") == cid)) x["text"] = v; }), Loc.L("Text des Hinweises", "Note text"), true, 44);
            if (cid != null && cid == _focusCommentId) focus = text;
            var box = new Border
            {
                BorderBrush = barBrush, BorderThickness = new Thickness(3, 0, 0, 0), Background = Theme.Surface2, CornerRadius = new CornerRadius(3),
                Padding = new Thickness(10, 8, 8, 8), Margin = new Thickness(0, 0, 0, 8), Child = U.Col(head, text)
            };
            _details.Children.Add(box);
        }
        if (!ro)
        {
            var add = new WrapPanel { Margin = new Thickness(-3, 0, 0, 0) };
            foreach (var (k, name) in NoteKinds)
            {
                var b = U.Btn(Theme.IAdd, name, () => AddNoteBox(anchor, k), Loc.F("Hinweiskasten „{0}“ zu diesem Schritt hinzufügen", "Add a “{0}” note to this step", name), "Ghost");
                b.Padding = new Thickness(8, 4, 8, 4); b.Margin = new Thickness(1, 0, 1, 0);
                add.Children.Add(b);
            }
            _details.Children.Add(add);
        }
        // Interne Notizen: nur im Editor sichtbar, in keiner Ausgabe
        if (s.InternalComments.Count > 0)
        {
            _details.Children.Add(U.Overline(Loc.F("Interne Notizen ({0}) – in keiner Ausgabe", "Internal notes ({0}) – not in any output", s.InternalComments.Count), new Thickness(0, 18, 0, 8)));
            foreach (var c in s.InternalComments)
            {
                string? cid = c.Str("id");
                var del = U.Ghost(Theme.IDelete, null, () => RemoveComment(cid), Loc.L("Notiz löschen", "Delete note"));
                del.MinHeight = 28; del.Padding = new Thickness(7, 4, 7, 4); del.Margin = new Thickness(0); del.IsEnabled = !ro;
                var dock = new DockPanel();
                DockPanel.SetDock(del, Dock.Right); dock.Children.Add(del);
                dock.Children.Add(new TextBlock { Text = c.Str("text") ?? "", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Fg2, VerticalAlignment = VerticalAlignment.Center });
                _details.Children.Add(new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(3, 0, 0, 0), Background = Theme.Surface2, CornerRadius = new CornerRadius(3), Padding = new Thickness(10, 8, 8, 8), Margin = new Thickness(0, 0, 0, 8), Child = dock });
            }
        }
        if (focus != null) { _focusCommentId = null; var f = focus; Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { f.Focus(); f.BringIntoView(); }); }
    }

    /// <summary>Aufgenommenen Kommentar als intern markieren (04 §edits.comments: origin „recording“, sourceSeq).</summary>
    void SetRecordedCommentHidden(long sourceSeq, bool hidden)
    {
        if (sourceSeq <= 0 || _view.IsHiddenComment(sourceSeq) == hidden) return;
        Change(e =>
        {
            var arr = e["comments"] as JsonArray ?? new JsonArray();
            foreach (var x in arr.OfType<JsonObject>().Where(x => x.Str("origin") == "recording" && x.Long("sourceSeq") == sourceSeq).ToList()) arr.Remove(x);
            if (hidden) arr.Add(new JsonObject { ["id"] = NextId(e, "comments", "rc"), ["origin"] = "recording", ["sourceSeq"] = sourceSeq, ["kind"] = "internal", ["hidden"] = true, ["createdUtc"] = Json.UtcNow(), ["by"] = "user" });
            e["comments"] = arr;
        });
        _status.Text = hidden ? Loc.L("Kommentar ist intern – erscheint in keiner Ausgabe.", "Comment is internal – not shown in any output.") : Loc.L("Kommentar erscheint wieder in den Ausgaben.", "Comment appears in the outputs again.");
    }

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
        if (on) _status.Text = Loc.L("Kapitel angelegt – Titel rechts eingeben.", "Chapter created – enter the title on the right.");
        Change(e =>
        {
            var se = StepEdit(e, anchor);
            if (on) se["chapter"] ??= new JsonObject { ["title"] = Loc.For(_view.Language, "Neues Kapitel", "New chapter"), ["text"] = "" };
            else se.Remove("chapter");
        });
    }
}
