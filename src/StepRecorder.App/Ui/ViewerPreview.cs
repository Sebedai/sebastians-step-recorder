using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Seitenansicht im Editor (13, Paket 20): Mitte „Bild | Seite“, Aktualisierung nach jedem Speichern (verzögert, abbrechbar, in eigener
/// Leseansicht ohne Oberfläche), Abgleich der Auswahl in beide Richtungen, Drucken.
/// </summary>
sealed partial class ViewerWindow
{
    PageView? _pageView;
    bool _pageMode, _previewStale = true, _previewBusy;
    readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    readonly GuideImageCache _previewCache = new();
    CancellationTokenSource? _previewCts;
    int _previewGen;
    RadioButton? _imgModeBtn, _pageModeBtn;
    readonly List<FrameworkElement> _imageOnly = new();

    /// <summary>Ist die Seitenansicht gerade zu sehen (Seitenmodus oder Eintrag „Titelseite und Einleitung“)?</summary>
    bool PageViewShown => _pageView != null && _pageView.Visibility == Visibility.Visible;

    /// <summary>Umschalter „Bild | Seite“ für die Ansichtszeile über dem Bild.</summary>
    FrameworkElement BuildCenterModeToggle()
    {
        var (track, btns) = U.Segmented("cmode", 0, (Theme.IImage, Loc.L("Bild", "Image")), (Theme.IDoc, Loc.L("Seite", "Page")));
        _imgModeBtn = btns[0]; _pageModeBtn = btns[1];
        track.ToolTip = Loc.L("Bild bearbeiten oder die Seiten der fertigen Anleitung ansehen (Strg+F2)", "Edit the image or view the pages of the finished guide (Ctrl+F2)");
        btns[0].Checked += (_, _) => { if (IsLoaded && _pageMode) SetPageMode(false); };
        btns[1].Checked += (_, _) => { if (IsLoaded && !_pageMode) SetPageMode(true); };
        return track;
    }

    PageView EnsurePageView(Grid host)
    {
        if (_pageView != null) return _pageView;
        _pageView = new PageView { Visibility = Visibility.Collapsed };
        host.Children.Add(_pageView);
        _pageView.BlockClicked += (b, dbl) =>
        {
            if (b.Foreign) { _status.Text = Loc.L("Dieser Schritt gehört zu einer angehängten Aufnahme – zum Bearbeiten diese öffnen.", "This step belongs to an appended recording – open that recording to edit it."); return; }
            if (_cover) LeaveCover();
            _syncFromPage = true;
            try { SelectAnchor(b.Anchor); } finally { _syncFromPage = false; }
            if (dbl) { if (b.Key.StartsWith("s:")) SetPageMode(false); }
        };
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); BuildPreview(); };
        return _pageView;
    }

    bool _syncFromPage;

    void SetPageMode(bool on)
    {
        if (!PdfRaster.Available && on)
        {
            Msg.Show(Loc.L("Die Seitenansicht braucht den PDF-Renderer von Windows, der auf diesem Rechner fehlt. Nutzen Sie „Vorschau im Browser“ oder exportieren Sie ein PDF.", "The page view needs the Windows PDF renderer, which is missing on this computer. Use “Preview in browser” or export a PDF."));
            _imgModeBtn?.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
            return;
        }
        InlineCommit(now: true);
        _pageMode = on;
        (on ? _pageModeBtn : _imgModeBtn)?.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
        if (on) _tool = "select";
        ShowStep();
        _status.Text = on ? Loc.L("Seitenansicht: so sieht die Anleitung als PDF aus – Klick auf einen Schritt wählt ihn, Doppelklick öffnet sein Bild.", "Page view: this is how the guide looks as a PDF – click a step to select it, double-click to open its image.") : _status.Text;
    }

    void TogglePageMode() => SetPageMode(!_pageMode);

    /// <summary>Mitte nach Modus einrichten; true, wenn die Seitenansicht gezeigt wird (ShowStep rendert dann kein Bild).</summary>
    bool ApplyCenterMode()
    {
        if (_pageView == null) return false;
        bool pages = (_pageMode || _cover) && PdfRaster.Available;
        _pageView.Visibility = pages ? Visibility.Visible : Visibility.Collapsed;
        foreach (var e in new FrameworkElement[] { _zoom, _paletteEl, _zoomBarEl, _imgCaptionPill }) if (pages) e.Visibility = Visibility.Collapsed; else if (!_cover) e.Visibility = Visibility.Visible;
        foreach (var e in _imageOnly) e.Visibility = pages ? Visibility.Collapsed : Visibility.Visible;
        if (pages)
        {
            _noImage.Visibility = Visibility.Collapsed;
            _coverHost.Visibility = Visibility.Collapsed;
            if (_previewStale && !_previewBusy) RequestPreview(now: true);
        }
        return pages;
    }

    /// <summary>Nach jedem Speichern: Seiten neu aufbauen (verzögert; ohne sichtbare Seitenansicht nur vormerken).</summary>
    void AfterEditsSaved()
    {
        _previewStale = true;
        if (PageViewShown) RequestPreview();
    }

    void RequestPreview(bool now = false)
    {
        if (_pageView == null) return;
        _previewTimer.Stop();
        if (now) BuildPreview(); else _previewTimer.Start();
    }

    async void BuildPreview()
    {
        if (_pageView == null || _closed) return;
        _previewCts?.Cancel();
        var cts = new CancellationTokenSource(); _previewCts = cts;
        int gen = ++_previewGen;
        string path = _path, lang = _view.Language;
        var cache = _previewCache;
        _previewBusy = true; _previewStale = false;
        if (_pageView.Document == null) _pageView.Message(Loc.L("Seiten werden aufgebaut …", "Building pages …"));
        else _pageView.Banner(Loc.L("Wird aktualisiert …", "Updating …"));
        try
        {
            var result = await Task.Run(() =>
            {
                using var job = ExportJob.Begin(null, cts.Token);
                using var v = SessionView.Open(path, headless: true, lang: lang);
                v.AuthorTextFallback = true;
                var p = GuideExport.RenderPdf(v, new GuideOptions { Language = lang, ImageCache = cache, MissingTranslation = "other" });
                cts.Token.ThrowIfCancellationRequested();
                var raster = PdfRaster.Open(p.Pdf);
                return (p, raster);
            });
            if (gen != _previewGen || _closed) { result.raster.Dispose(); return; }
            _pageView.SetDocument(result.p, result.raster);
            _pageView.Banner(result.p.Unreviewed > 0 ? Loc.N(result.p.Unreviewed, "1 auffällige Eingabe ist ungeprüft – sie erscheint hier als Platzhalter; Export und Druck warten auf die Durchsicht.", "{0} auffällige Eingaben sind ungeprüft – sie erscheinen hier als Platzhalter; Export und Druck warten auf die Durchsicht.",
                "1 flagged input is unreviewed – it appears as a placeholder here; export and printing wait for the review.", "{0} flagged inputs are unreviewed – they appear as placeholders here; export and printing wait for the review.") : null, result.p.Unreviewed > 0);
            if (!_cover && Sel is { } s && !_pageView.IsVisible("s:" + s.Anchor)) _pageView.ScrollToKey("s:" + s.Anchor, false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (gen != _previewGen) return;
            AppLog.Write("Seitenansicht: " + ex);
            _pageView.Banner(Loc.L("Die Seiten konnten nicht aktualisiert werden: ", "The pages could not be updated: ") + ex.Message, true);
            if (_pageView.Document == null) _pageView.Message(Loc.L("Die Seitenansicht ist nicht verfügbar.", "The page view is not available."));
        }
        finally { if (gen == _previewGen) _previewBusy = false; }
    }

    /// <summary>In der Seitenansicht den gewählten Schritt zeigen (statt sein Bild zu rendern).</summary>
    void ShowStepInPages(ViewStep? s)
    {
        if (_pageView == null || _syncFromPage) return;
        if (_cover) { if (_pageView.Document != null) _pageView.ScrollToKey(_pageView.Document.Blocks.FirstOrDefault()?.Key ?? "", false, -10000); return; }
        if (s != null) _pageView.ScrollToKey((s.ChapterTitle != null ? "c:" : "s:") + s.Anchor, true);
    }

    /// <summary>Nur für Entwickler-Schnappschüsse: warten, bis die Seiten aufgebaut und gerastert sind.</summary>
    internal void DevWaitPreview()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 30000)
        {
            Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, () => { });
            if (!_previewBusy && !_previewTimer.IsEnabled && sw.ElapsedMilliseconds > 1500) break;
            Thread.Sleep(40);
        }
        for (int i = 0; i < 40; i++) { Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, () => { }); Thread.Sleep(25); }
    }

    // =====================================================================================
    // Drucken
    // =====================================================================================

    async void PrintGuide()
    {
        InlineCommit(now: true);
        if (!PdfRaster.Available) { Msg.Show(Loc.L("Zum Drucken fehlt der PDF-Renderer von Windows. Exportieren Sie ein PDF und drucken Sie es von dort.", "The Windows PDF renderer needed for printing is missing. Export a PDF and print it from there.")); return; }
        if (_view.UnreviewedCount > 0)
        {
            if (Msg.Show(Loc.F("Drucken erst nach der Durchsicht\n\n{0} auffällige Eingaben sind noch nicht durchgesehen. Jetzt durchsehen?", "Printing only after the review\n\n{0} conspicuous inputs have not been reviewed yet. Review them now?", _view.UnreviewedCount), Loc.L("Drucken", "Print"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) ShowReview();
            return;
        }
        var dlg = new PrintDialog { UserPageRangeEnabled = true };
        if (dlg.ShowDialog() != true) return;
        string path = _path, lang = _view.Language;
        var cache = _previewCache;
        int dpi = 200;
        try
        {
            var pages = await ExportProgressWindow.Run(this, Loc.L("Seiten werden für den Druck vorbereitet …", "Preparing pages for printing …"), (progress, ct) =>
            {
                using var job = ExportJob.Begin(progress, ct);
                using var v = SessionView.Open(path, headless: false, lang: lang);
                var p = GuideExport.RenderPdf(v, new GuideOptions { Language = lang, ImageCache = cache });
                using var raster = PdfRaster.Open(p.Pdf);
                var list = new List<byte[]>();
                for (int i = 0; i < raster.PageCount; i++)
                {
                    ExportJob.Tick(i + 1, raster.PageCount, Loc.L("Seiten", "Pages"));
                    list.Add(raster.RenderPng(i, (int)(p.PageW / 72.0 * dpi)));
                }
                return (list, p.PageW, p.PageH);
            });
            int from = 1, to = pages.list.Count;
            if (dlg.PageRangeSelection == PageRangeSelection.UserPages) { from = Math.Max(1, dlg.PageRange.PageFrom); to = Math.Min(pages.list.Count, dlg.PageRange.PageTo); }
            var doc = new FixedDocument();
            double w = pages.PageW / 72.0 * 96, h = pages.PageH / 72.0 * 96;
            doc.DocumentPaginator.PageSize = new Size(w, h);
            for (int i = from - 1; i < to; i++)
            {
                var bi = new BitmapImage(); bi.BeginInit(); bi.StreamSource = new MemoryStream(pages.list[i]); bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit(); bi.Freeze();
                var fp = new FixedPage { Width = w, Height = h, Background = Brushes.White };
                fp.Children.Add(new Image { Source = bi, Width = w, Height = h, Stretch = Stretch.Fill });
                var pc = new PageContent(); ((System.Windows.Markup.IAddChild)pc).AddChild(fp);
                doc.Pages.Add(pc);
            }
            dlg.PrintDocument(doc.DocumentPaginator, _view.Title);
            _status.Text = Loc.N(to - from + 1, "1 Seite an den Drucker geschickt.", "{0} Seiten an den Drucker geschickt.", "1 page sent to the printer.", "{0} pages sent to the printer.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { UiApp.ShowError(ex); }
    }
}
