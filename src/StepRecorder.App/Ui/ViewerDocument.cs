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
        if (_cover && !PageViewShown) _coverHost.Content = CoverPreview();
        QualityInvalidate();
        AfterEditsSaved();
        if (refillLater) Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => { if (!_details.IsKeyboardFocusWithin) Fill(); });
    }

    /// <summary>Ändert eine Dokument-Einstellung.</summary>
    void DocChange(Action<DocSettings> change, bool quiet = true)
    {
        void Do(JsonObject e) { var g = GuideOf(e); var d = DocSettings.From(g); change(d); g["doc"] = d.ToJson(); }
        if (quiet) QuietChange(Do); else Change(Do, refill: false);
    }

    /// <summary>Formatierbares Textfeld (13, Paket 21), das beim Verlassen speichert: (Text, formatiert?).</summary>
    RichEdit RichBox(string value, bool rich, Action<string, bool> save, string? placeholder = null, double minH = 48)
    {
        var r = new RichEdit(placeholder, minH, 4000, _view.Language, barAlways: false) { IsReadOnly = _view.Session.ReadOnly };
        r.Load(value, rich);
        r.Committed += (v, f) => save(v.TrimEnd(), f);
        r.Left += () => _list.Focus();
        return r;
    }

    /// <summary>Ist ein Hinweis als formatiert gespeichert (und nicht von einem Agenten)?</summary>
    static bool ViewSessionRich(JsonObject c) => SessionView.IsRich(c);

    /// <summary>Kennzeichen „formatiert“ eines Dokumenttexts in der Sprache der Ansicht.</summary>
    static bool GuideFmt(JsonObject? guide, string key, string lang)
        => lang != "de" && guide?.Obj("i18n")?.Obj(lang) is { } loc && loc.Str(key) is { Length: > 0 } ? loc.Obj("fmt")?.Int(key) == 1 : guide?.Obj("fmt")?.Int(key) == 1;

    void SetGuideFmt(JsonObject e, string key, bool rich)
    {
        var g = GuideOf(e);
        JsonObject holder = g;
        if (_view.Language != "de") { var i18n = g["i18n"] as JsonObject ?? new JsonObject(); g["i18n"] = i18n; holder = i18n[_view.Language] as JsonObject ?? new JsonObject(); i18n[_view.Language] = holder; }
        var fmt = holder["fmt"] as JsonObject;
        if (rich) { fmt ??= new JsonObject(); fmt[key] = 1; holder["fmt"] = fmt; }
        else if (fmt != null) { fmt.Remove(key); if (fmt.Count == 0) holder.Remove("fmt"); }
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
        _editTitle = null; _editBody = null; _editAnchor = null;
        var guide = _view.Edits.Obj("guide");
        string lang = _view.Language;
        var d = DocSettings.From(guide, lang);
        string GS(string key) => DocSettings.GuideText(guide, key, lang) ?? "";
        bool en = lang != "de", ro = _view.Session.ReadOnly;

        _details.Children.Add(U.Heading(Loc.L("Titelseite und Einleitung", "Title page and introduction"), Theme.FsTitle, new Thickness(0, 4, 0, 4)));
        _details.Children.Add(U.Hint(Loc.L("Alles, was aus den Schritten eine fertige Anleitung macht – gilt für HTML, Word, PDF, Markdown und GIF.", "Everything that turns the steps into a finished guide – applies to HTML, Word, PDF, Markdown and GIF."), new Thickness(0, 0, 0, 12)));
        if (en) _details.Children.Add(Callout(Theme.IGlobe, Theme.AccentSoft, Theme.AccentText, Loc.L("Englische Fassung", "English version"), Loc.L("Titel, Untertitel, Zielgruppe, Texte, Voraussetzungen und Fußzeile gelten hier nur für Englisch. Leer gelassene Felder übernehmen die deutsche Fassung.", "Title, subtitle, audience, texts, prerequisites and footer apply to English only. Fields left empty use the German version.")));
        var actions = new WrapPanel { Margin = new Thickness(-3, 0, 0, 0) };
        actions.Children.Add(U.Btn(Theme.IEye, Loc.L("Vorschau im Browser", "Preview in browser"), PreviewDocument, Loc.L("Anleitung als HTML erzeugen und im Standardbrowser öffnen", "Create the guide as HTML and open it in the default browser")));
        actions.Children.Add(U.Btn(Theme.IExport, Loc.L("Exportieren …", "Export …"), ShowExport, TipOf("export"), "Primary"));
        _details.Children.Add(actions);

        // Titelseite
        var title = Section(Loc.L("Titelseite", "Title page"));
        title.Children.Add(U.Field(Loc.L("Titel", "Title"), CommitBox(GS("title") is { Length: > 0 } tt ? tt : _view.Title, v => { if (v.Length == 0) return; QuietChange(e => SetGuideText(e, "title", v)); UpdateTitle(); }, Loc.L("Titel der Anleitung", "Guide title"))));
        title.Children.Add(U.Field(Loc.L("Untertitel", "Subtitle"), CommitBox(d.Subtitle, v => LocDocChange("subtitle", v), Loc.L("z. B. Kurzanleitung für neue Mitarbeitende", "e.g. Quick guide for new employees"))));
        var two = new Grid();
        two.ColumnDefinitions.Add(new ColumnDefinition()); two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); two.ColumnDefinitions.Add(new ColumnDefinition());
        var ver = U.Field("Version", CommitBox(d.Version, v => DocChange(x => x.Version = v), Loc.L("z. B. 1.0", "e.g. 1.0")));
        var aud = U.Field(Loc.L("Zielgruppe", "Audience"), CommitBox(d.Audience, v => LocDocChange("audience", v), Loc.L("z. B. Vertrieb", "e.g. Sales")));
        Grid.SetColumn(aud, 2); two.Children.Add(ver); two.Children.Add(aud);
        title.Children.Add(two);
        title.Children.Add(U.Field(Loc.L("Autor oder Firma", "Author or company"), CommitBox(d.Author, v => DocChange(x => x.Author = v), Loc.L("erscheint auf der Titelseite", "appears on the title page"))));
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
        var pick = U.Btn(Theme.IOpen, d.Logo.Length > 0 ? Loc.L("Anderes Logo …", "Different logo …") : Loc.L("Logo wählen …", "Choose logo …"), PickLogo, Loc.L("PNG, JPG oder BMP – erscheint auf der Titelseite bzw. im Kopf", "PNG, JPG or BMP – appears on the title page or in the header")); pick.IsEnabled = !ro;
        logoRow.Children.Add(pick);
        if (d.Logo.Length > 0) logoRow.Children.Add(U.Btn(Theme.IDelete, Loc.L("Entfernen", "Remove"), () => DocChange(x => x.Logo = "", quiet: false)));
        title.Children.Add(U.Field("Logo", logoRow));

        // Einleitung und Texte
        var texts = Section(Loc.L("Einleitung und Texte", "Introduction and texts"));
        texts.Children.Add(U.Field(Loc.L("Einleitung", "Introduction"), RichBox(GS("intro"), GuideFmt(guide, "intro", lang), (v, rich) => QuietChange(e => { SetGuideText(e, "intro", v); SetGuideFmt(e, "intro", rich); }), Loc.L("Wozu dient die Anleitung, was ist das Ergebnis?", "What is the guide for, and what is the result?"), 64),
            Loc.L("Ziel und erwartetes Ergebnis der Aufnahme werden automatisch ergänzt.", "The goal and expected result of the recording are added automatically.")));
        texts.Children.Add(U.Field(Loc.L("Voraussetzungen", "Prerequisites"), CommitBox(d.Prerequisites, v => LocDocChange("prerequisites", v), Loc.L("Eine pro Zeile, z. B. Zugang zum Portal", "One per line, e.g. Access to the portal"), true),
            Loc.L("Erscheint als Aufzählung vor dem ersten Schritt.", "Appears as a list before the first step.")));
        texts.Children.Add(U.Field(Loc.L("Abschluss", "Closing"), RichBox(GS("outro"), GuideFmt(guide, "outro", lang), (v, rich) => QuietChange(e => { SetGuideText(e, "outro", v); SetGuideFmt(e, "outro", rich); }), Loc.L("z. B. Fertig! Bei Fragen: Servicedesk, Tel. …", "e.g. Done! Questions? Service desk, phone …"), 64)));
        // Variablen (13, Paket 23): in jedem Text als {{Name}}; eingebaut sind Titel, Untertitel, Version, Autor, Datum, Zielgruppe
        texts.Children.Add(U.Field(Loc.L("Variablen", "Variables"), CommitBox(VarsText(), v => QuietChange(e => SetVars(e, v)), Loc.L("Je Zeile Name = Wert, z. B. Produkt = Kasse 3000", "One per line, name = value, e.g. Product = Till 3000"), true),
            Loc.L("Im Text als {{Produkt}} – auch über „＋“ im Textfeld. Eingebaut: {{Titel}}, {{Untertitel}}, {{Version}}, {{Autor}}, {{Datum}}, {{Zielgruppe}}.", "In texts as {{Product}} – also via “＋” in the text field. Built in: {{Title}}, {{Subtitle}}, {{Version}}, {{Author}}, {{Date}}, {{Audience}}.")));

        // Aufbau
        var build = Section(Loc.L("Aufbau", "Structure"));
        CheckBox Check(string text, bool value, Action<bool> set, string tip)
        {
            var c = new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(0, 0, 0, 8), ToolTip = tip, IsEnabled = !ro };
            c.Checked += (_, _) => set(true); c.Unchecked += (_, _) => set(false);
            return c;
        }
        build.Children.Add(Check(Loc.L("Eigene Titelseite", "Own title page"), d.Cover, v => DocChange(x => x.Cover = v, quiet: false), Loc.L("Eigene erste Seite mit Logo, Titel, Untertitel, Version, Autor und Datum", "Own first page with logo, title, subtitle, version, author and date")));
        build.Children.Add(Check(Loc.L("Inhaltsverzeichnis", "Table of contents"), d.Toc, v => DocChange(x => x.Toc = v), Loc.L("Mit Kapiteln und Schritten; im PDF mit Seitenzahlen. Erscheint ab 3 Kapiteln oder 6 Schritten.", "With chapters and steps; in the PDF with page numbers. Appears from 3 chapters or 6 steps.")));
        build.Children.Add(Check(Loc.L("Datum anzeigen", "Show date"), d.ShowDate, v => DocChange(x => x.ShowDate = v, quiet: false), Loc.L("Stand-Datum auf der Titelseite bzw. im Kopf", "Date of issue on the title page or in the header")));
        int numIdx = d.Numbering switch { "flat" => 1, "none" => 2, _ => 0 };
        var (numTrack, nums) = U.Segmented("dnum", numIdx, (null, "1.1, 1.2 …"), (null, "1, 2, 3 …"), (null, Loc.L("Keine", "None")));
        for (int i = 0; i < 3; i++) { string v = new[] { "chapter", "flat", "none" }[i]; nums[i].Checked += (_, _) => DocChange(x => x.Numbering = v); }
        build.Children.Add(U.Field(Loc.L("Nummerierung der Schritte", "Step numbering"), numTrack, Loc.L("„1.1“ zählt innerhalb der Kapitel – Kapitel legen Sie am Schritt unter „Einordnung“ an.", "“1.1” counts within chapters – you create chapters on the step under “Placement”."), new Thickness(0, 8, 0, 16)));

        // Bilder
        var images = Section(Loc.L("Bilder", "Images"));
        var viewBox = new ComboBox { MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = !ro };
        foreach (var (_, name) in DocSettings.Views) viewBox.Items.Add(name);
        viewBox.SelectedIndex = Math.Max(0, Array.FindIndex(DocSettings.Views, x => x.id == d.View));
        viewBox.SelectionChanged += (_, _) => { string v = DocSettings.Views[Math.Max(0, viewBox.SelectedIndex)].id; DocChange(x => x.View = v); };
        var ov = new CheckBox { Content = Loc.L("Im Bereich um die Aktion eine kleine Übersicht des ganzen Bildschirms zeigen", "Show a small overview of the whole screen with the area around the action"), IsChecked = d.Overview, Margin = new Thickness(0, 8, 0, 0), IsEnabled = !ro };
        ov.Checked += (_, _) => DocChange(x => x.Overview = true); ov.Unchecked += (_, _) => DocChange(x => x.Overview = false);
        images.Children.Add(U.Field(Loc.L("Bildausschnitt", "Image area"), U.Col(viewBox, ov), Loc.L("„Bereich um die Aktion“ zeigt je Schritt nur die Umgebung von Klick oder Eingabe – gut lesbar, auch auf kleinen Seiten. Vorschau im Editor über dem Bild unter „Bildausschnitt“.", "“Area around the action” shows only the surroundings of the click or input for each step – easy to read, even on small pages. Preview it in the editor above the image under “Image area”.")));
        int sizeIdx = Math.Max(0, Array.FindIndex(DocSettings.Sizes, x => x.id == d.ImageSize));
        var (sizeTrack, sizes) = U.Segmented("dsize", sizeIdx, DocSettings.Sizes.Select(x => ((string?)null, x.name)).ToArray());
        for (int i = 0; i < sizes.Length; i++) { string v = DocSettings.Sizes[i].id; sizes[i].Checked += (_, _) => DocChange(x => x.ImageSize = v); }
        images.Children.Add(U.Field(Loc.L("Größe", "Size"), sizeTrack, Loc.L("Je Schritt änderbar unter „Bild in der Anleitung“.", "Can be changed per step under “Image in the guide”.")));
        var (layTrack, lays) = U.Segmented("dlay", d.Layout == "side" ? 1 : 0, (null, Loc.L("Unter dem Text", "Below the text")), (null, Loc.L("Neben dem Text", "Beside the text")));
        lays[0].Checked += (_, _) => DocChange(x => x.Layout = "below"); lays[1].Checked += (_, _) => DocChange(x => x.Layout = "side");
        images.Children.Add(U.Field(Loc.L("Anordnung", "Arrangement"), layTrack, Loc.L("„Neben dem Text“ ergibt kompakte Anleitungen: Text links, Bild rechts.", "“Beside the text” makes compact guides: text on the left, image on the right.")));

        // Gestaltung
        var look = Section(Loc.L("Gestaltung", "Design"));
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
        look.Children.Add(U.Field(Loc.L("Akzentfarbe", "Accent color"), swatches, Loc.L("Für Titel, Kapitel und Schrittnummern.", "For titles, chapters and step numbers.")));
        string addr = guide?.Str("address") ?? AppSettingsAddress();
        var (adTrack, ads) = U.Segmented("daddr", addr == "informal" ? 1 : 0, (null, Loc.L("Sie", "Formal (Sie)")), (null, Loc.L("du", "Informal (du)")));
        ads[0].Checked += (_, _) => Change(e => GuideOf(e)["address"] = "formal"); ads[1].Checked += (_, _) => Change(e => GuideOf(e)["address"] = "informal");
        look.Children.Add(U.Field(Loc.L("Anrede in automatischen Texten", "Form of address in automatic texts"), adTrack, Loc.L("Von Ihnen geänderte Texte bleiben unverändert.", "Texts you have changed stay unchanged.")));
        look.Children.Add(U.Field(Loc.L("Fußzeile", "Footer"), CommitBox(d.Footer, v => LocDocChange("footer", v), Loc.L("z. B. Firma · intern", "e.g. Company · internal")), Loc.L("Links in der Fußzeile; rechts steht die Seitenzahl (Word, PDF).", "On the left in the footer; the page number is on the right (Word, PDF).")));

        // Seite und Word-Vorlage (09 §4.4, §7.3)
        var page = Section(Loc.L("Seite (Word und PDF)", "Page (Word and PDF)"));
        var (pageTrack, pages) = U.Segmented("dpage", d.Page == "letter" ? 1 : 0, DocSettings.Pages.Select(x => ((string?)null, x.name)).ToArray());
        for (int i = 0; i < pages.Length; i++) { string v = DocSettings.Pages[i].id; pages[i].Checked += (_, _) => DocChange(x => x.Page = v); pages[i].IsEnabled = !ro; }
        var (orTrack, ors) = U.Segmented("dorient", d.Landscape ? 1 : 0, (null, Loc.L("Hochformat", "Portrait")), (null, Loc.L("Querformat", "Landscape")));
        ors[0].Checked += (_, _) => DocChange(x => x.Landscape = false); ors[1].Checked += (_, _) => DocChange(x => x.Landscape = true);
        foreach (var o in ors) o.IsEnabled = !ro;
        page.Children.Add(U.Field(Loc.L("Seitenformat", "Page size"), U.Row(pageTrack, orTrack), Loc.L("Bilder passen sich der Seitenbreite an. Mit einer Word-Vorlage gilt im Word-Dokument deren Seiteneinrichtung.", "Images adapt to the page width. With a Word template, the Word document uses the template's page setup.")));
        var wordRow = new WrapPanel { Margin = new Thickness(-3, 0, 0, 0) };
        if (d.WordTemplate.Length > 0)
        {
            var nameText = U.Text(d.WordTemplateName.Length > 0 ? d.WordTemplateName : Loc.L("Word-Vorlage", "Word template"), Theme.FsHint, true, margin: new Thickness(3, 0, 10, 0));
            nameText.VerticalAlignment = VerticalAlignment.Center;
            wordRow.Children.Add(nameText);
        }
        var pickTpl = U.Btn(Theme.IOpen, d.WordTemplate.Length > 0 ? Loc.L("Andere Vorlage …", "Different template …") : Loc.L("Word-Vorlage wählen …", "Choose Word template …"), PickWordTemplate,
            Loc.L(".dotx oder .docx mit Firmenlayout – Vorlagen mit Makros (.dotm) werden abgelehnt", ".dotx or .docx with corporate layout – templates with macros (.dotm) are rejected")); pickTpl.IsEnabled = !ro;
        wordRow.Children.Add(pickTpl);
        if (d.WordTemplate.Length > 0) wordRow.Children.Add(U.Btn(Theme.IDelete, Loc.L("Entfernen", "Remove"), () => DocChange(x => { x.WordTemplate = ""; x.WordTemplateName = ""; }, quiet: false)));
        page.Children.Add(U.Field(Loc.L("Word-Vorlage", "Word template"), wordRow,
            Loc.F("Das Word-Dokument übernimmt Formatvorlagen, Schriften, Seitenränder, Kopf- und Fußzeile der Vorlage. Der Inhalt steht an der Textmarke oder dem Inhaltssteuerelement „{0}“, sonst am Ende; Felder SR.Title, SR.Subtitle, SR.Author, SR.Date und SR.Version werden gefüllt.",
                  "The Word document takes the template's styles, fonts, margins, header and footer. The content goes to the bookmark or content control “{0}”, otherwise to the end; fields SR.Title, SR.Subtitle, SR.Author, SR.Date and SR.Version are filled.", StepRecorder.Export.Formats.SimpleDocx.ContentMarker)));

        // Vorlage
        var st = StepRecorder.Settings.AppSettings.Load();
        var tplBox = Section(Loc.L("Vorlage", "Template"), st.DefaultTemplate.Length > 0 ? Loc.F("Neue Aufnahmen erhalten automatisch die Vorlage „{0}“.", "New recordings automatically get the template “{0}”.", st.DefaultTemplate) : Loc.L("Logo, Farbe, Autor, Fußzeile, Aufbau und Bilder als Firmenlook für weitere Anleitungen übernehmen.", "Use logo, colour, author, footer, structure and images as a corporate look for further guides."));
        var tpl = new ComboBox { MinWidth = 180, IsEnabled = !ro && st.DocTemplates.Count > 0 };
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
        var tplRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) }; tplRow.Children.Add(tpl); tplRow.Children.Add(save);
        tplBox.Children.Add(tplRow);

        // Weitere Aufnahmen
        var appended = (guide?.Arr("append") ?? new JsonArray()).OfType<JsonObject>().ToList();
        var more = Section(Loc.L("Weitere Aufnahmen", "More recordings"), appended.Count == 0 ? Loc.L("Längere Handbücher aus mehreren Aufnahmen: angehängte Aufnahmen erscheinen im Export als eigene Kapitel hinter dieser.", "Longer manuals from several recordings: appended recordings appear in the export as separate chapters after this one.") : Loc.L("Erscheinen im Export als eigene Kapitel hinter dieser Aufnahme:", "Appear in the export as separate chapters after this recording:"));
        for (int i = 0; i < appended.Count; i++)
        {
            var a = appended[i]; int idx = i;
            string p = a.Str("path") ?? "";
            string atitle = Session.LooksLikeSession(p) ? (Json.ReadObject(Path.Combine(p, "manifest.json"))?.Str("title") is { Length: > 0 } mt ? mt : Path.GetFileName(p)) : Loc.L("nicht gefunden: ", "not found: ") + Path.GetFileName(p);
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var del = U.Ghost(Theme.IDelete, null, () => Change(e => { if (GuideOf(e)["append"] is JsonArray arr && idx < arr.Count) arr.RemoveAt(idx); }, refill: false), Loc.L("Nicht mehr anhängen (die Aufnahme selbst bleibt erhalten)", "Stop appending (the recording itself is kept)"));
            del.MinHeight = 30; del.Padding = new Thickness(7, 4, 7, 4); del.Margin = new Thickness(0);
            DockPanel.SetDock(del, Dock.Right); row.Children.Add(del);
            var rowText = U.Row(U.Text($"{idx + 1}.", Theme.FsHint, fg: Theme.Muted, margin: new Thickness(0, 0, 8, 0)), U.Text(atitle, Theme.FsHint, true)); rowText.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(rowText);
            row.ToolTip = p;
            more.Children.Add(row);
        }
        var appRow = new WrapPanel { Margin = new Thickness(-3, 0, 0, 12) };
        appRow.Children.Add(U.Btn(Theme.IAdd, Loc.L("Aufnahme anhängen …", "Append recording …"), () => Run("append"), Loc.L("Eine vorhandene Aufnahme hinten anhängen", "Append an existing recording at the end")));
        appRow.Children.Add(U.Btn(Theme.IRecord, Loc.L("Aufnahme fortsetzen", "Continue recording"), () => Run("continueRec"), Loc.L("Jetzt weiter aufnehmen – die neue Aufnahme wird automatisch angehängt", "Keep recording now – the new recording is appended automatically")));
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
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = Loc.L("Bilder", "Images") + "|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
        if (dlg.ShowDialog(this) != true) return;
        try { using var img = System.Drawing.Image.FromFile(dlg.FileName); string b64 = DocSettings.LogoFrom(img); DocChange(x => x.Logo = b64, quiet: false); }
        catch (Exception ex) { Msg.Show(Loc.L("Das Logo lässt sich nicht öffnen:\n", "The logo cannot be opened:\n") + ex.Message); }
    }

    /// <summary>Eigene Word-Vorlage wählen (.dotx/.docx); Makros werden abgelehnt (09 §7.3).</summary>
    void PickWordTemplate()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = Loc.L("Word-Vorlage", "Word template") + " (*.dotx;*.docx)|*.dotx;*.docx" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            if (ext is ".dotm" or ".docm") { Msg.Show(Loc.L("Vorlagen mit Makros (.dotm, .docm) werden nicht verwendet.", "Templates with macros (.dotm, .docm) are not used.")); return; }
            var info = new FileInfo(dlg.FileName);
            if (info.Length > DocSettings.WordTemplateMaxBytes) { Msg.Show(Loc.F("Die Word-Vorlage ist größer als {0}.", "The Word template is larger than {0}.", Loc.Bytes(DocSettings.WordTemplateMaxBytes))); return; }
            var data = File.ReadAllBytes(dlg.FileName);
            if (StepRecorder.Export.Formats.SimpleDocx.CheckTemplate(data) is { } why) { Msg.Show(why); return; }
            string b64 = Convert.ToBase64String(data), name = Path.GetFileName(dlg.FileName);
            DocChange(x => { x.WordTemplate = b64; x.WordTemplateName = name; }, quiet: false);
        }
        catch (Exception ex) { Msg.Show(Loc.L("Die Word-Vorlage lässt sich nicht öffnen:\n", "The Word template cannot be opened:\n") + ex.Message); }
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
        if (_cover) DocDetails();
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
