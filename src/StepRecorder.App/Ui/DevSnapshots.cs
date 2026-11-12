using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StepRecorder.Capture;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Nur für die Entwicklung (Umgebungsvariable STEPRECORDER_UISHOT=&lt;Ordner&gt;): rendert jedes Fenster nach dem Laden
/// als PNG – die Fenster selbst sind in Bildschirmfotos absichtlich unsichtbar. Öffnet nach dem Viewer die Dialoge nacheinander.
/// </summary>
static class DevSnapshots
{
    static string? _dir;
    static int _n;

    public static bool Active => _dir != null;

    public static void Install()
    {
        _dir = Environment.GetEnvironmentVariable("STEPRECORDER_UISHOT");
        if (string.IsNullOrEmpty(_dir)) { _dir = null; return; }
        Directory.CreateDirectory(_dir);
        // Optional feste Fenstergröße (z. B. 1440x900), damit Bilder auf kleinen Testbildschirmen realistisch ausfallen
        var size = Environment.GetEnvironmentVariable("STEPRECORDER_UISHOT_SIZE")?.Split('x');
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            var w = (Window)s;
            if (size is { Length: 2 } && double.TryParse(size[0], out var sw) && double.TryParse(size[1], out var sh))
            {
                if (w is ViewerWindow) { w.MaxWidth = w.MaxHeight = double.PositiveInfinity; w.Width = sw; w.Height = sh; }
                else if (w is StartWindow) { w.SizeToContent = SizeToContent.Manual; w.MaxHeight = double.PositiveInfinity; w.Width = Math.Min(sw, 1200); w.Height = sh; }
            }
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                Save(w);
                if (w is ViewerWindow v) Demo(v);
                else if (w is StartWindow sw) sw.DevPages(() => Save(sw));
                else { if (IsModal(w)) w.DialogResult = false; else w.Close(); }
            };
            t.Start();
        }));
    }

    static bool IsModal(Window w) => (bool)(typeof(Window).GetField("_showingAsDialog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(w) ?? false);

    static void Save(Window w)
    {
        try
        {
            var root = (FrameworkElement)VisualTreeHelper.GetChild(w, 0);
            double scale = PresentationSource.FromVisual(w)?.CompositionTarget.TransformToDevice.M11 ?? 1;
            var rtb = new RenderTargetBitmap((int)(root.ActualWidth * scale), (int)(root.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            // Mica ist im Bild durchsichtig – für das PNG die Grundfläche des Themas unterlegen
            var under = new DrawingVisual();
            using (var dc = under.RenderOpen())
                dc.DrawRectangle(w is Wpf.Ui.Controls.FluentWindow || w.Background is not SolidColorBrush { Color.A: 255 } ? Theme.Bg : w.Background, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
            if (!w.AllowsTransparency) rtb.Render(under);   // Leiste und Overlays bleiben freigestellt
            rtb.Render(root);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(Path.Combine(_dir!, $"{++_n:00}-{w.GetType().Name}{(w.GetType() == typeof(Window) ? "-" + Sanitize(w.Title) : "")}.png"));
            enc.Save(fs);
        }
        catch (Exception ex) { UiApp.Log("snapshot: " + ex); }
    }

    /// <summary>Offene Menüs (eigene Popup-Fenster) abbilden: alle sichtbaren Popups des Programms rendern.</summary>
    public static void SaveOpenPopups(string name)
    {
        if (_dir == null) return;
        int k = 0;
        foreach (var src in PresentationSource.CurrentSources.OfType<System.Windows.Interop.HwndSource>().ToList())
        {
            if (src.RootVisual is not FrameworkElement root || root is Window || root.ActualWidth < 8 || !root.IsVisible) continue;
            if (root.GetType().Name != "PopupRoot") continue;
            try
            {
                double scale = src.CompositionTarget.TransformToDevice.M11;
                var rtb = new RenderTargetBitmap((int)(root.ActualWidth * scale), (int)(root.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                var under = new DrawingVisual();
                using (var dc = under.RenderOpen()) dc.DrawRectangle(Theme.Bg, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
                rtb.Render(under); rtb.Render(root);
                var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
                using var fs = File.Create(Path.Combine(_dir, $"{++_n:00}-Menu-{Sanitize(name)}{(k++ > 0 ? "-" + k : "")}.png"));
                enc.Save(fs);
            }
            catch (Exception ex) { UiApp.Log("snapshot popup: " + ex); }
        }
    }

    static string Sanitize(string s) => new(s.Where(char.IsLetterOrDigit).Take(20).ToArray());

    static void Demo(ViewerWindow v)
    {
        v.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            for (int i = 0; i < Math.Min(v.StepCount, 4); i++)
            {
                v.SelectStep(i);
                v.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, () => { });
                Save(v);
            }
            v.DevDemo(() => Save(v));
            v.DevMenus();
            v.DevShowShortcuts();
            v.DevShowQuality();
            static string Target(StepRecorder.Export.ExportRequest r, string? f) => @"C:\Recordings\Example\export\" + r.Format + @"\Guide." + StepRecorder.Export.Exporter.Extension(r.Format);
            new ExportDialog(new StepRecorder.Export.ExportRequest { Format = "html", Language = "de" }, Target) { Owner = v }.ShowDialog();
            new ExportDialog(new StepRecorder.Export.ExportRequest { Format = "ki", Language = "de" }, Target) { Owner = v }.ShowDialog();
            var demo = new StepRecorder.Export.ExportResult { Path = @"C:\Recordings\Example\export\pdf\Guide.pdf" };
            demo.Notices.Add(Loc.L("12 Schritte, 11 Bilder", "12 steps, 11 images"));
            ExportResultDialog.Show(v, demo, "pdf");
            PackDialog.Show(v, v.SessionPath);
            RecordingRequestDialog.Ask(new StepRecorder.Mcp.RecordingRequest
            {
                Id = "rq-000000000001", ExpiresUtc = DateTime.UtcNow.AddSeconds(118),
                Goal = Loc.L("Bitte zeigen Sie, wie man im Editor eine Datei unter neuem Namen speichert.", "Please show how to save a file under a new name in the editor."),
                Expected = Loc.L("Die Datei liegt danach im Ordner Dokumente.", "The file is then in the Documents folder."),
                Title = Loc.L("Speichern unter", "Save as")
            });
            UiApp.Instance.ShowLibrary();
            new InputDialog(Loc.L("Kommentar", "Comment"), "", null, kinds: true) { Owner = v }.ShowDialog();
            new InputDialog(Loc.L("Text des Schritts", "Step text"), Loc.L("Klicken Sie auf „Speichern“.", "Click \"Save\"."), Loc.L("Datei speichern", "Save file")) { Owner = v }.ShowDialog();
            new CommentWindow().ShowDialog();
            Msg.Show(Loc.L("Aufnahme verwerfen?\n\nDer Sitzungsordner wird gelöscht. Das lässt sich nicht rückgängig machen.", "Discard recording?\n\nThe recording folder is deleted. This cannot be undone."), Loc.L("Aufnahme verwerfen", "Discard recording"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            var bar = new RecordingBar(new RecordingController(new RecordingOptions()));
            bar.Show();
            bar.SetTime(TimeSpan.FromSeconds(83), 12, RecorderState.Recording, false);
            bar.Message(Loc.L("Unsichtbarkeit geprüft · Schutznetz – Passwortfelder werden maskiert.", "Invisibility checked · Safety net – password fields are masked."));
        });
    }
}
