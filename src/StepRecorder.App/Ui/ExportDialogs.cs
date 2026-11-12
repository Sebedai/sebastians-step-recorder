using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StepRecorder.Export;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Exportdialog: Format, Ziel („Speichern unter“), Sprache, Art, Umfang, Darstellung bzw. Detailstufe und Bildprofil.
/// Liefert eine vollständige <see cref="ExportRequest"/>; ausgeführt wird sie vom Editor.
/// </summary>
sealed class ExportDialog : Window
{
    sealed record Fmt(string Id, string Glyph, string Name, string Sub);

    static List<Fmt> Formats() => new List<Fmt>
    {
        new("html", Theme.IGlobe, "HTML", Loc.L("eine Datei", "single file")),
        new("md", Theme.ICode, "Markdown", Loc.L("Text + Bilder", "text + images")),
        new("docx", Theme.IDoc, "Word", Loc.L("bearbeitbar", "editable")),
        new("pdf", Theme.IPdf, "PDF", Loc.L("zum Drucken", "for printing")),
        new("pptx", Theme.IDoc, "PowerPoint", Loc.L("Folien", "slides")),
        new("tour", Theme.IMouse, Loc.L("Klick-Tour", "Click tour"), Loc.L("zum Durchklicken", "click through")),
        new("bugreport", Theme.IError, Loc.L("Fehlerbericht", "Bug report"), Loc.L("für Tickets", "for tickets")),
        new("gif", Theme.IPlay, "GIF", Loc.L("animiert", "animated")),
        new("ki", Theme.IRobot, Loc.L("KI-Paket", "AI package"), Loc.L("für KI", "for AI")),
        new("ahk", Theme.IScript, "AutoHotkey", Loc.L("Skript", "script")),
    }.Where(f => Exporter.IsKnownFormat(f.Id)).ToList();

    readonly List<Fmt> _fmts;
    readonly RadioButton[] _formatBtns;
    readonly Func<ExportRequest, string?, string> _defaultTarget;
    readonly TextBox _target = new() { FontFamily = Theme.Mono, FontSize = 12, Padding = new Thickness(8, 6, 8, 6), VerticalContentAlignment = VerticalAlignment.Center };
    readonly Button _resetTarget;
    /// <summary>Ziel steht fest (getippt, gewählt oder vom letzten Export) – sonst wird es aus den Optionen berechnet.</summary>
    bool _targetCustom, _syncTarget, _userEdited;

    readonly ComboBox _lang = new() { Items = { "Deutsch", "English" } };
    readonly ComboBox _preset = new();
    readonly ComboBox _imp = new() { Items = { Loc.L("Hauptschritte", "Main steps"), Loc.L("Haupt- und Nebenschritte", "Main and secondary steps"), Loc.L("Alle Schritte", "All steps") }, SelectedIndex = 0 };
    readonly ComboBox _view = new() { Items = { Loc.L("Ganzer Bildschirm", "Full screen"), Loc.L("Anwendungsfenster", "Application window"), Loc.L("Ausschnitt um die Aktion", "Detail around the action") }, SelectedIndex = 0 };
    readonly ComboBox _missing = new() { Items = { Loc.L("Automatischer Text der Exportsprache", "Automatic text in the export language"), Loc.L("Eigener Text der anderen Sprache", "Own text in the other language") }, SelectedIndex = 0 };
    readonly ComboBox _detail = new() { Items = { Loc.L("Kompakt", "Compact"), Loc.L("Standard", "Standard"), Loc.L("Vollständig", "Complete") }, SelectedIndex = 1 };
    readonly ComboBox _profile = new() { Items = { Loc.L("Kompatibel (≤ 1568 px)", "Compatible (≤ 1568 px)"), Loc.L("Hochauflösend (≤ 2576 px)", "High resolution (≤ 2576 px)"), Loc.L("Original", "Original") }, SelectedIndex = 0 };
    readonly ComboBox _gifSec = new() { Items = { Loc.L("1,5 Sekunden je Schritt", "1.5 seconds per step"), Loc.L("2,5 Sekunden je Schritt", "2.5 seconds per step"), Loc.L("4 Sekunden je Schritt", "4 seconds per step") }, SelectedIndex = 1 };
    readonly CheckBox _zip = new() { Content = Loc.L("Zusätzlich als ZIP-Datei verpacken", "Also pack as ZIP file"), Margin = new Thickness(0, 2, 0, 0) };
    readonly CheckBox _timing = new() { Content = Loc.L("Auch die Denkpausen der Aufnahme übernehmen", "Also keep the pauses of the recording"), Margin = new Thickness(0, 0, 0, 8) };
    readonly CheckBox _ahkKi = new() { Content = Loc.L("Als KI-Paket: Anleitung für KI, ablauf.json, Bildausschnitte, Texterkennung", "As AI package: AI instructions, ablauf.json, image details, text recognition") };
    readonly CheckBox _overview = new() { Content = Loc.L("Kleine Übersicht des ganzen Bildschirms einblenden", "Show a small overview of the whole screen"), IsChecked = true, Margin = new Thickness(0, 6, 0, 0) };
    readonly CheckBox _markMouse = new() { Content = Loc.L("Mausaktionen: Klickpunkt, Zugweg, Zielrahmen, Scrollrichtung", "Mouse actions: click point, drag path, target frame, scroll direction"), IsChecked = true, Margin = new Thickness(0, 2, 0, 6) };
    readonly CheckBox _markKeys = new() { Content = Loc.L("Tastatur: gedrückte Tasten und getippten Text als Tasten", "Keyboard: pressed keys and typed text as key caps"), IsChecked = true };

    readonly StackPanel _info = new();
    readonly TextBlock _impHint = Hint(), _viewHint = Hint(), _detailHint = Hint(), _profileHint = Hint(), _presetHint = Hint();

    static TextBlock Hint() => U.Text("", 11.5, fg: Theme.Muted, margin: new Thickness(0, 5, 0, 0));

    /// <summary>Ordnerwahl für „Speichern unter“: null = unverändert, "" = wieder Standardort der Aufnahme, sonst gewählter Ordner.</summary>
    public string? FolderChoice { get; private set; }

    string FormatId => _fmts[Math.Max(0, Array.FindIndex(_formatBtns, f => f.IsChecked == true))].Id;
    /// <summary>Tatsächliches Format (AutoHotkey als KI-Paket ist ein eigenes Ordnerformat).</summary>
    public string Format => FormatId == "ahk" && _ahkKi.IsChecked == true ? "ahk-ki" : FormatId;
    string TextLanguage => _lang.SelectedIndex == 1 ? "en" : "de";
    string Preset => GuidePresets.All[Math.Max(0, _preset.SelectedIndex)].id;
    bool IsGuide => Format is "html" or "md" or "docx" or "pdf" or "pptx" or "tour" or "bugreport" or "gif";
    bool HasPreset => Format is "html" or "md" or "docx" or "pdf";

    public ExportRequest Request => new()
    {
        Format = Format, Target = _target.Text.Trim(), Language = TextLanguage,
        Importance = _imp.SelectedIndex switch { 1 => "secondary", 2 => "all", _ => "primary" },
        View = _view.SelectedIndex switch { 1 => "window", 2 => "crop", _ => "screen" }, Overview = _overview.IsChecked == true,
        GifSeconds = _gifSec.SelectedIndex switch { 0 => 1.5, 2 => 4, _ => 2.5 }, MouseMarks = _markMouse.IsChecked == true, KeyMarks = _markKeys.IsChecked == true,
        Template = HasPreset ? Preset : null, MissingTranslation = _missing.SelectedIndex == 1 ? "other" : "auto",
        Detail = _detail.SelectedIndex switch { 0 => "compact", 2 => "full", _ => "standard" },
        ImageProfile = _profile.SelectedIndex switch { 1 => "highres", 2 => "original", _ => "compatible" }, Zip = _zip.IsChecked == true,
        RealTiming = _timing.IsChecked == true,
    };

    /// <param name="initial">Vorbelegung (gemerkte Optionen).</param>
    /// <param name="defaultTarget">Standardziel für eine Anforderung; zweiter Wert: Ordner (null = zuletzt genutzter, "" = export\ der Aufnahme).</param>
    /// <param name="initialTarget">Ziel des letzten Exports dieser Aufnahme in diesem Format (sonst Standardziel).</param>
    public ExportDialog(ExportRequest initial, Func<ExportRequest, string?, string> defaultTarget, string? initialTarget = null)
    {
        _defaultTarget = defaultTarget;
        Title = Loc.L("Exportieren", "Export"); Width = 840; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Surface);
        _fmts = Formats();
        string start = Exporter.Normalize(initial.Format) is "ahk-ki" ? "ahk" : Exporter.Normalize(initial.Format);
        int sel = Math.Max(0, _fmts.FindIndex(f => f.Id == start));
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = Math.Min(_fmts.Count, 7), Margin = new Thickness(-4, 0, -4, 12) };
        var btns = new List<RadioButton>();
        for (int i = 0; i < _fmts.Count; i++)
        {
            var f = _fmts[i];
            var content = U.Col(U.Icon(f.Glyph, 22), new TextBlock { Text = f.Name, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = f.Sub, FontSize = 11, Opacity = 0.75, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0) });
            var rb = new RadioButton { GroupName = "fmt", Style = Theme.Style("Choice"), Content = content, IsChecked = i == sel };
            btns.Add(rb); grid.Children.Add(rb);
        }
        _formatBtns = btns.ToArray();
        foreach (var (id, de, en, _, _) in GuidePresets.All) _preset.Items.Add(Loc.L(de, en));

        // Vorbelegung
        _lang.SelectedIndex = initial.Language == "en" ? 1 : 0;
        _preset.SelectedIndex = Math.Max(0, Array.FindIndex(GuidePresets.All, p => p.id == GuidePresets.Normalize(initial.Template)));
        _imp.SelectedIndex = initial.Importance switch { "secondary" => 1, "all" => 2, _ => 0 };
        _view.SelectedIndex = initial.View switch { "window" => 1, "crop" => 2, _ => 0 };
        _overview.IsChecked = initial.Overview ?? true;
        _missing.SelectedIndex = initial.MissingTranslation == "other" ? 1 : 0;
        _gifSec.SelectedIndex = initial.GifSeconds <= 1.5 ? 0 : initial.GifSeconds >= 4 ? 2 : 1;
        _markMouse.IsChecked = initial.MouseMarks; _markKeys.IsChecked = initial.KeyMarks;
        _detail.SelectedIndex = initial.Detail switch { "compact" => 0, "full" => 2, _ => 1 };
        _profile.SelectedIndex = initial.ImageProfile switch { "highres" => 1, "original" => 2, _ => 0 };
        _zip.IsChecked = initial.Zip; _timing.IsChecked = initial.RealTiming;
        _ahkKi.IsChecked = Exporter.Normalize(initial.Format) == "ahk-ki" || initial.AhkKi;

        // Speichern unter
        var browse = U.Btn(Theme.IFolder, Loc.L("Durchsuchen …", "Browse …"), Browse, null, "Ghost");
        _resetTarget = U.Btn(null, Loc.L("Standardort", "Default location"), () => { _targetCustom = _userEdited = false; FolderChoice = ""; UpdateTarget(); }, Loc.L("Wieder im Ordner „export“ der Aufnahme speichern", "Save in the recording's \"export\" folder again"), "Link");
        _target.TextChanged += (_, _) => { if (!_syncTarget) { _targetCustom = _userEdited = true; _resetTarget.Visibility = Visibility.Visible; } };
        var targetRow = new DockPanel();
        DockPanel.SetDock(browse, Dock.Right); targetRow.Children.Add(browse);
        targetRow.Children.Add(_target);
        var targetField = U.Field(Loc.L("Speichern unter", "Save as"), U.Col(targetRow, _resetTarget));
        _resetTarget.HorizontalAlignment = HorizontalAlignment.Left; _resetTarget.Margin = new Thickness(-4, 2, 0, 0);
        if (initialTarget != null) { _syncTarget = true; _target.Text = initialTarget; _syncTarget = false; _targetCustom = true; }

        var infoBox = new Border { Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(16, 12, 16, 14), Margin = new Thickness(0, 0, 0, 16), Child = _info };

        var options = new Grid();
        options.ColumnDefinitions.Add(new ColumnDefinition()); options.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); options.ColumnDefinitions.Add(new ColumnDefinition());
        for (int i = 0; i < 3; i++) options.RowDefinitions.Add(new RowDefinition());
        void G(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); options.Children.Add(e); }
        var fLang = U.Field(Loc.L("Sprache", "Language"), _lang);
        var fPreset = U.Field(Loc.L("Art", "Type"), U.Col(_preset, _presetHint));
        var fImp = U.Field(Loc.L("Umfang", "Scope"), U.Col(_imp, _impHint));
        var fView = U.Field(Loc.L("Darstellung", "Images"), U.Col(_view, _viewHint, _overview));
        var fMissing = U.Field(Loc.L("Fehlt eine Übersetzung", "If a translation is missing"), _missing);
        var fDetail = U.Field(Loc.L("Detailstufe", "Level of detail"), U.Col(_detail, _detailHint));
        var fProfile = U.Field(Loc.L("Bildgröße", "Image size"), U.Col(_profile, _profileHint));
        var zipHost = new StackPanel { Children = { _zip }, Margin = new Thickness(0, 24, 0, 0) };
        G(fLang, 0, 0); G(fPreset, 0, 2); G(fImp, 1, 0); G(fView, 1, 2); G(fMissing, 2, 0);
        G(fDetail, 1, 0); G(fProfile, 1, 2); G(zipHost, 2, 2);
        var gifOpts = U.Field(Loc.L("Ablauf", "Timing"), _gifSec, Loc.L("Das GIF zeigt ein Titelbild und dann jeden Schritt mit Nummer und Überschrift.", "The GIF shows a title image, then every step with number and heading."));
        var ahkOpts = U.Col(_timing, _ahkKi); ahkOpts.Margin = new Thickness(0, 0, 0, 12);
        var marksOpts = U.Field(Loc.L("Im Bild markieren", "Mark in the image"), U.Col(_markMouse, _markKeys), Loc.L("Wie im Editor unter „Markierungen“; dort lässt sich das auch je Schritt ein- und ausschalten.", "As under \"Markers\" in the editor, where it can also be switched per step."));

        void Upd()
        {
            bool ki = Format == "ki", ahk = FormatId == "ahk", guide = IsGuide;
            options.Visibility = ahk ? Visibility.Collapsed : Visibility.Visible;
            ahkOpts.Visibility = ahk ? Visibility.Visible : Visibility.Collapsed;
            gifOpts.Visibility = Format == "gif" ? Visibility.Visible : Visibility.Collapsed;
            _overview.Visibility = _view.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            marksOpts.Visibility = guide && Format != "bugreport" ? Visibility.Visible : Visibility.Collapsed;
            fPreset.Visibility = HasPreset ? Visibility.Visible : Visibility.Hidden;
            fImp.Visibility = fView.Visibility = guide ? Visibility.Visible : Visibility.Collapsed;
            fMissing.Visibility = guide && Format != "gif" ? Visibility.Visible : Visibility.Collapsed;
            fDetail.Visibility = fProfile.Visibility = zipHost.Visibility = ki ? Visibility.Visible : Visibility.Collapsed;
            _impHint.Text = _imp.SelectedIndex switch
            {
                1 => Loc.L("Zusätzlich Nebenschritte wie Scrollen, Navigationstasten und Fensterwechsel.", "Also secondary steps such as scrolling, navigation keys and window switches."),
                2 => Loc.L("Zusätzlich die Spur, z. B. Mausbewegungen ohne Aktion und Titeländerungen.", "Also the trace, e.g. mouse movements without action and title changes."),
                _ => Loc.L("Nur was eine Anleitung braucht: Klicks, Eingaben, Tastenkürzel, Kommentare.", "Only what a guide needs: clicks, input, shortcuts, comments.")
            };
            _viewHint.Text = _view.SelectedIndex switch
            {
                1 => Loc.L("Nur das bediente Fenster, ohne den restlichen Bildschirm.", "Only the window being used, without the rest of the screen."),
                2 => Loc.L("Vergrößerter Bereich rund um den Klick oder die Eingabe.", "Enlarged area around the click or the input."),
                _ => Loc.L("Das komplette Bildschirmfoto.", "The complete screenshot.")
            };
            _detailHint.Text = _detail.SelectedIndex switch
            {
                0 => Loc.L("Haupt- und Nebenschritte, Bilder nur für Hauptschritte, kurze Texte – spart Platz bei der KI.", "Main and secondary steps, images only for main steps, short texts – saves AI context."),
                2 => Loc.L("Alle Schritte mit allen Bildern und den Rohereignissen – für die Fehlersuche.", "All steps with all images and the raw events – for troubleshooting."),
                _ => Loc.L("Haupt- und Nebenschritte, Hauptschritte mit bis zu drei Bildern.", "Main and secondary steps, main steps with up to three images.")
            };
            _profileHint.Text = _profile.SelectedIndex switch
            {
                1 => Loc.L("Schärfer, braucht aber mehr Platz im KI-Kontext.", "Sharper, but takes more AI context."),
                2 => Loc.L("Unverkleinert – größte Dateien.", "Not scaled down – largest files."),
                _ => Loc.L("Passt für alle gängigen KI-Assistenten.", "Works with all common AI assistants.")
            };
            var p = GuidePresets.All[Math.Max(0, _preset.SelectedIndex)];
            _presetHint.Text = Loc.L(p.hintDe, p.hintEn);
            UpdateTarget();
            FillInfo();
        }
        // Optionen, die Dateiname oder Dateiart ändern: Ziel im selben Ordner neu berechnen
        void Renamed()
        {
            if (_userEdited && FolderChoice == null) FolderChoice = FolderOf(_target.Text.Trim());
            _targetCustom = false;
            Upd();
        }
        foreach (var f in _formatBtns) f.Checked += (_, _) => Renamed();
        foreach (var cb in new[] { _preset, _lang }) cb.SelectionChanged += (_, _) => Renamed();
        foreach (var cb in new[] { _imp, _view, _detail, _profile, _gifSec, _missing }) cb.SelectionChanged += (_, _) => Upd();
        _ahkKi.Checked += (_, _) => Renamed(); _ahkKi.Unchecked += (_, _) => Renamed();
        _zip.Checked += (_, _) => Upd(); _zip.Unchecked += (_, _) => Upd();
        Upd();
        var ok = U.Primary(Loc.L("Exportieren", "Export"), Accept, Theme.IExport);
        U.Dialog(this, Loc.L("Exportieren", "Export"), U.Col(grid, infoBox, targetField, options, gifOpts, marksOpts, ahkOpts), ok, U.Btn(Loc.L("Abbrechen", "Cancel"), () => DialogResult = false));
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.FocusedElement is not TextBox) { Accept(); e.Handled = true; } };
    }

    void Accept()
    {
        var t = _target.Text.Trim();
        if (t.Length == 0 || !Path.IsPathRooted(t) || t.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            Msg.Show(Loc.L("Bitte einen vollständigen Pfad angeben, z. B. über „Durchsuchen …“.", "Please enter a full path, e.g. via \"Browse …\"."), Title);
            return;
        }
        if (_userEdited && FolderChoice == null) FolderChoice = FolderOf(t);
        DialogResult = true;
    }

    /// <summary>Ordner, der für dieses Format gemerkt wird (bei Ordnerformaten der übergeordnete Ordner).</summary>
    string FolderOf(string target) => Path.GetDirectoryName(target.TrimEnd('\\', '/')) ?? target;

    void UpdateTarget()
    {
        if (_targetCustom) { _resetTarget.Visibility = Visibility.Visible; return; }
        _syncTarget = true;
        var r = Request; r.Target = null;
        try { _target.Text = _defaultTarget(r, FolderChoice); } catch { }
        _syncTarget = false;
        _resetTarget.Visibility = FolderChoice is { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
    }

    void Browse()
    {
        string cur = _target.Text.Trim();
        if (Exporter.IsFolderOutput(Format))
        {
            var d = new Microsoft.Win32.OpenFolderDialog { Title = Loc.L("Ordner für das Paket wählen", "Choose folder for the package") };
            var parent = FolderOf(cur);
            if (Directory.Exists(parent)) d.InitialDirectory = parent;
            if (d.ShowDialog(this) != true) return;
            SetCustom(Path.Combine(d.FolderName, Path.GetFileName(cur.TrimEnd('\\', '/'))), d.FolderName);
        }
        else
        {
            string ext = Exporter.Extension(Format);
            var d = new Microsoft.Win32.SaveFileDialog { FileName = Path.GetFileName(cur), DefaultExt = "." + ext, AddExtension = true, Filter = $"{ext.ToUpperInvariant()}|*.{ext}", OverwritePrompt = true };
            var dir = Path.GetDirectoryName(cur);
            if (dir != null && Directory.Exists(dir)) d.InitialDirectory = dir;
            if (d.ShowDialog(this) != true) return;
            SetCustom(d.FileName, Path.GetDirectoryName(d.FileName)!);
        }
    }

    void SetCustom(string target, string folder)
    {
        _syncTarget = true; _target.Text = target; _syncTarget = false;
        _targetCustom = _userEdited = true; FolderChoice = folder;
        _resetTarget.Visibility = Visibility.Visible;
    }

    /// <summary>Beschreibt das gewählte Format: wofür, welche Dateien mit welchem Inhalt.</summary>
    void FillInfo()
    {
        _info.Children.Clear();
        string imp = _imp.SelectedIndex switch { 1 => Loc.L("Haupt- und Nebenschritte", "main and secondary steps"), 2 => Loc.L("alle Schritte", "all steps"), _ => Loc.L("alle Hauptschritte", "all main steps") };
        string guideContent = Preset == "standard" || !HasPreset
            ? Loc.F("Titel, Einleitung, Voraussetzungen, Kapitel und {0} – jeweils mit Überschrift, Anleitungstext, Bild und Hinweiskästen; Deckblatt, Inhaltsverzeichnis, Bildgröße und Farbe stellen Sie im Editor im Reiter „Dokument“ ein", "Title, introduction, prerequisites, chapters and {0} – each with heading, instructions, image and note boxes; set cover, table of contents, image size and colour in the editor's \"Document\" tab", imp)
            : Loc.F("Titel und {0} mit Überschrift und Anleitungstext, ohne Bilder und Deckblatt, fortlaufend nummeriert", "Title and {0} with heading and instructions, without images or cover, numbered consecutively", imp) + (Preset == "checklist" ? Loc.L("; je Schritt ein Kästchen zum Abhaken", "; a tick box per step") : "");
        (string title, string use, (string file, string what)[] files) = Format switch
        {
            "md" => (Loc.L("Markdown – Text und ein Bilderordner", "Markdown – text and an image folder"), Loc.L("Für Wikis, Git-Repositories, Confluence oder Notiz-Apps wie Obsidian.", "For wikis, Git repositories, Confluence or note apps like Obsidian."),
                new[] { (Loc.L("<Titel>.md", "<title>.md"), guideContent + Loc.L(" (als Markdown-Text).", " (as Markdown text).")), (Loc.L("bilder\\", "bilder\\"), Loc.L("ein PNG je Schritt, in der .md-Datei verlinkt.", "one PNG per step, linked from the .md file.")) }),
            "docx" => (Loc.L("Word – zum Weiterbearbeiten", "Word – for further editing"), Loc.L("Öffnet in Word oder LibreOffice; Texte, Layout und Bilder lassen sich dort noch anpassen.", "Opens in Word or LibreOffice; texts, layout and images can still be adjusted there."),
                new[] { (Loc.L("<Titel>.docx", "<title>.docx"), guideContent + Loc.L(". Die Bilder sind eingebettet.", ". Images are embedded.")) }),
            "pdf" => (Loc.L("PDF – fertig zum Drucken und Weitergeben", "PDF – ready to print and share"), Loc.L("Sieht überall gleich aus und lässt sich nicht versehentlich ändern.", "Looks the same everywhere and cannot be changed by accident."),
                new[] { (Loc.L("<Titel>.pdf", "<title>.pdf"), guideContent + Loc.L(". Die Bilder sind eingebettet.", ". Images are embedded.")) }),
            "pptx" => (Loc.L("PowerPoint – zum Vorführen und Schulen", "PowerPoint – for presenting and training"), Loc.L("Eine Folie je Schritt mit Bild und Text; der volle Text steht in den Notizen.", "One slide per step with image and text; the full text is in the notes."),
                new[] { (Loc.L("<Titel>.pptx", "<title>.pptx"), Loc.F("Titelfolie, Kapitel und {0}, Abschlussfolie.", "Title slide, chapters and {0}, closing slide.", imp)) }),
            "tour" => (Loc.L("Klick-Tour – Schritte im Browser durchklicken", "Click tour – click through the steps in a browser"), Loc.L("Eine HTML-Datei ohne Skript: Wer auf die markierte Stelle klickt, kommt zum nächsten Schritt.", "One HTML file without script: clicking the marked spot leads to the next step."),
                new[] { (Loc.L("<Titel>.html", "<title>.html"), Loc.F("Startseite, {0} mit Bild und Klickfläche, Endseite.", "Start page, {0} with image and click area, end page.", imp)) }),
            "bugreport" => (Loc.L("Fehlerbericht – für Ticket und Issue", "Bug report – for ticket and issue"), Loc.L("Schritte zum Nachstellen, erwartetes und tatsächliches Ergebnis, Umgebung – als Markdown.", "Steps to reproduce, expected and actual result, environment – as Markdown."),
                new[] { (Loc.L("<Titel>.md", "<title>.md"), Loc.L("der Bericht zum Einfügen in ein Ticket.", "the report to paste into a ticket.")), (Loc.L("bilder\\", "bilder\\"), Loc.L("Bilder der Schritte als Anhang.", "step images as attachments.")) }),
            "gif" => (Loc.L("GIF – animiert, für Wiki, Chat und E-Mail", "GIF – animated, for wiki, chat and email"), Loc.L("Spielt die Schritte nacheinander ab – ohne Anleitungstexte, nur mit Überschriften.", "Plays the steps one after another – without instructions, only with headings."),
                new[] { (Loc.L("<Titel>.gif", "<title>.gif"), Loc.F("Titelbild, dann {0} mit Bild, Nummer und Überschrift; läuft in Schleife.", "Title image, then {0} with image, number and heading; loops.", imp)) }),
            "ki" => (Loc.L("KI-Paket – für ChatGPT, Claude & Co.", "AI package – for ChatGPT, Claude & co."), Loc.L("Ein Ordner, den Sie einer KI geben (hochladen oder als ZIP anhängen). Die KI erfährt daraus genau, was in welcher Reihenfolge passiert ist und wie sicher das belegt ist.", "A folder you give to an AI (upload or attach as ZIP). It tells the AI exactly what happened in which order and how well that is evidenced."),
                KiFiles()),
            "ahk-ki" => (Loc.L("AutoHotkey als KI-Paket", "AutoHotkey as AI package"), Loc.L("Ein Skript-Entwurf plus alles, was ein KI-Assistent braucht, um das Skript zu prüfen und zu verbessern.", "A script draft plus everything an AI assistant needs to check and improve it."),
                new[] { ("entwurf.ahk", Loc.L("das AutoHotkey-v2-Skript als Entwurf.", "the AutoHotkey v2 script draft.")), ("ablauf.json", Loc.L("alle Schritte mit Fenstern, Steuerelementen, Wartebedingungen und Prüfungen.", "all steps with windows, controls, wait conditions and checks.")), ("README_AHK_KI.md", Loc.L("Anleitung für den KI-Assistenten.", "instructions for the AI assistant.")), (Loc.L("bilder\\", "bilder\\"), Loc.L("Bildausschnitte der Bedienelemente mit erkanntem Text.", "image details of the controls with recognised text.")) }),
            "ahk" => (Loc.L("AutoHotkey – Ablauf automatisch wiederholen", "AutoHotkey – replay the sequence automatically"), Loc.L("Ein AutoHotkey-v2-Skript, das die aufgenommenen Schritte nachspielt. Zum Ausführen muss AutoHotkey v2 installiert sein.", "An AutoHotkey v2 script that replays the recorded steps. AutoHotkey v2 must be installed to run it."),
                new[] { (Loc.L("<Titel>.ahk", "<title>.ahk"), Loc.L("das Skript: findet Fenster über ahk_class/ahk_exe und Steuerelemente über ClassNN, wartet wie in der Aufnahme und prüft das Ergebnis. Getippte Werte stehen als Parameter oben, Passwörter werden beim Start abgefragt. Abbruch mit Strg+Esc.", "the script: finds windows via ahk_class/ahk_exe and controls via ClassNN, waits as in the recording and checks the result. Typed values are parameters at the top, passwords are asked for at start. Cancel with Ctrl+Esc.")), (Loc.L("bilder\\", "bilder\\"), Loc.L("Bildausschnitte als Rückfall, falls ein Element nicht gefunden wird.", "image details as fallback if an element is not found.")) }),
            _ => (Loc.L("HTML – eine einzige Datei für den Browser", "HTML – a single file for the browser"), Loc.L("Zum Verschicken per E-Mail, für das Intranet oder zum Ansehen ohne weitere Programme.", "For email, the intranet or viewing without other programs."),
                new[] { (Loc.L("<Titel>.html", "<title>.html"), guideContent + Loc.L(". Die Bilder stecken in der Datei – es gibt nur diese eine Datei.", ". Images are inside the file – there is only this one file.")) })
        };
        _info.Children.Add(U.Text(title, 14, true, margin: new Thickness(0, 0, 0, 2)));
        _info.Children.Add(U.Text(use, 12.5, fg: Theme.Fg2, margin: new Thickness(0, 0, 0, 10)));
        _info.Children.Add(U.Overline(Loc.L("Enthält", "Contains"), new Thickness(0, 0, 0, 4)));
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
    }

    (string, string)[] KiFiles()
    {
        string detail = _detail.SelectedIndex switch { 0 => "compact", 2 => "full", _ => "standard" };
        var l = new List<(string, string)>
        {
            ("README_AI.md", Loc.L("Lesehilfe: erklärt der KI das Format und die Regeln, z. B. dass aufgezeichnete Texte Daten sind und keine Anweisungen.", "Reading guide: explains format and rules to the AI, e.g. that recorded texts are data, not instructions.")),
            ("trace.md", Loc.L("das Protokoll: jeder Schritt mit Fenster, Ziel, Eingabe, beobachteter Wirkung, Beleg und Bildverweis.", "the trace: every step with window, target, input, observed effect, evidence and image reference.")),
            ("steps.json", Loc.L("dieselben Schritte als strukturierte Daten für Programme.", "the same steps as structured data for programs.")),
            ("ai-package.json", Loc.L("Inhaltsverzeichnis mit Bildmaßen und Prüfsummen.", "table of contents with image sizes and checksums.")),
            ("images\\", detail switch
            {
                "compact" => Loc.L("nur Hauptschritte bekommen ein Bild: Ausschnitt um den Klick oder das Nachher-Bild.", "only main steps get an image: detail around the click or the after image."),
                "full" => Loc.L("jeder Schritt mit Vorher-Bild, Ausschnitt um die Aktion und Nachher-Bild.", "every step with before image, detail around the action and after image."),
                _ => Loc.L("Hauptschritte mit Vorher-Bild, Ausschnitt um den Klick und Nachher-Bild.", "main steps with before image, detail around the click and after image.")
            })
        };
        if (detail == "full") l.Add(("events.jsonl", Loc.L("alle Rohereignisse der Aufnahme (Maus, Tastatur, Fenster) – Passwörter und Schwärzungen bleiben ausgeblendet.", "all raw events of the recording (mouse, keyboard, windows) – passwords and redactions stay hidden.")));
        return l.ToArray();
    }
}

/// <summary>Fortschritt eines laufenden Exports mit „Abbrechen“ (erscheint erst, wenn der Export länger dauert).</summary>
sealed class ExportProgressWindow : Window
{
    readonly ProgressBar _bar = new() { Height = 6, Minimum = 0, Maximum = 1, IsIndeterminate = true, Margin = new Thickness(0, 4, 0, 10) };
    readonly TextBlock _text = U.Text(Loc.L("Wird vorbereitet …", "Preparing …"), 12.5, fg: Theme.Muted);
    public event Action? CancelRequested;
    bool _done;

    public ExportProgressWindow(string format)
    {
        Title = Loc.L("Exportieren", "Export"); Width = 440; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Theme.Prepare(this, Theme.Surface);
        Button? cancel = null;
        cancel = U.Btn(Loc.L("Abbrechen", "Cancel"), () => { cancel!.IsEnabled = false; _text.Text = Loc.L("Wird abgebrochen …", "Cancelling …"); CancelRequested?.Invoke(); });
        U.Dialog(this, Loc.F("{0} wird erstellt …", "Creating {0} …", format.ToUpperInvariant() switch { "KI" => Loc.L("KI-Paket", "AI package"), "AHK" or "AHK-KI" => "AutoHotkey", var f => f }), U.Col(_bar, _text), cancel);
        Closing += (_, e) => { if (!_done) { e.Cancel = true; cancel.IsEnabled = false; CancelRequested?.Invoke(); } };
    }

    public void Report(ExportProgress p)
    {
        if (p.Total <= 0) return;
        _bar.IsIndeterminate = false; _bar.Maximum = p.Total; _bar.Value = Math.Min(p.Done, p.Total);
        _text.Text = Loc.F("{0} von {1}", "{0} of {1}", p.Done, p.Total) + (p.Phase.Length > 0 ? " · " + p.Phase : "");
    }

    /// <summary>Schließt das Fenster (mehrfach aufrufbar; vor dem Anzeigen: schließt gleich nach dem Laden).</summary>
    public void Finish()
    {
        if (_done) return;
        _done = true;
        if (IsLoaded) Close(); else Loaded += (_, _) => Close();
    }
}

/// <summary>Ergebnis eines Exports: Pfad, Hinweise, „Öffnen“, „Im Ordner zeigen“, „Pfad kopieren“.</summary>
static class ExportResultDialog
{
    public static void Show(Window owner, ExportResult r, string format)
    {
        var w = new Window { Title = Loc.L("Export erstellt", "Export created"), Width = 600, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        bool isFile = File.Exists(r.Path);
        // Skripte nie per Doppelklick-Verhalten öffnen – das würde sie ausführen
        bool canOpen = isFile ? !r.Path.EndsWith(".ahk", StringComparison.OrdinalIgnoreCase) : Directory.Exists(r.Path);
        var path = new TextBox { Text = r.Path, IsReadOnly = true, FontFamily = Theme.Mono, FontSize = 12, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(8, 6, 8, 6) };
        var body = U.Col(path);
        foreach (var t in r.Warnings) body.Children.Add(U.Row(U.Icon(Theme.IWarn, 12, Theme.Warn), U.Text(t, 12.5, fg: Theme.Fg2, margin: new Thickness(8, 0, 0, 0))));
        foreach (var t in r.Notices) body.Children.Add(U.Row(U.Icon(Theme.IInfo, 12, Theme.Muted), U.Text(t, 12.5, fg: Theme.Fg2, margin: new Thickness(8, 0, 0, 0))));
        foreach (var c in body.Children.OfType<StackPanel>()) { c.Margin = new Thickness(0, 8, 0, 0); foreach (var tb in c.Children.OfType<TextBlock>()) tb.TextWrapping = TextWrapping.Wrap; ((TextBlock)c.Children[^1]).MaxWidth = 520; }
        var buttons = new List<Button>();
        if (canOpen) buttons.Add(U.Primary(Loc.L("Öffnen", "Open"), () => { Shell.Open(r.Path); w.Close(); }, Theme.IOpen));
        var reveal = U.Btn(Theme.IFolder, Loc.L("Im Ordner zeigen", "Show in folder"), () => { Shell.Reveal(r.Path); w.Close(); }, null, canOpen ? null : "Primary");
        buttons.Add(reveal);
        buttons.Add(U.Btn(Theme.ICopy, Loc.L("Pfad kopieren", "Copy path"), () => Shell.CopyPath(r.Path)));
        buttons.Add(U.Btn(Loc.L("Schließen", "Close"), () => w.Close()));
        U.Dialog(w, Loc.L("Export erstellt", "Export created"), body, buttons.ToArray());
        w.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) w.Close(); };
        w.ShowDialog();
    }
}
