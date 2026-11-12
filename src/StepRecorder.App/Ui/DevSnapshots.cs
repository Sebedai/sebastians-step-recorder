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
                else if (w is not StartWindow) { if (IsModal(w)) w.DialogResult = false; else w.Close(); }
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
            rtb.Render(root);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(Path.Combine(_dir!, $"{++_n:00}-{w.GetType().Name}{(w.GetType() == typeof(Window) ? "-" + Sanitize(w.Title) : "")}.png"));
            enc.Save(fs);
        }
        catch (Exception ex) { UiApp.Log("snapshot: " + ex); }
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
            v.DevShowShortcuts();
            v.DevShowQuality();
            static string Target(StepRecorder.Export.ExportRequest r, string? f) => @"C:\Recordings\Example\export\" + r.Format + @"\Guide." + StepRecorder.Export.Exporter.Extension(r.Format);
            new ExportDialog(new StepRecorder.Export.ExportRequest { Format = "html", Language = "de" }, Target) { Owner = v }.ShowDialog();
            new ExportDialog(new StepRecorder.Export.ExportRequest { Format = "ki", Language = "de" }, Target) { Owner = v }.ShowDialog();
            var demo = new StepRecorder.Export.ExportResult { Path = @"C:\Recordings\Example\export\pdf\Guide.pdf" };
            demo.Notices.Add(Loc.L("12 Schritte, 11 Bilder", "12 steps, 11 images"));
            ExportResultDialog.Show(v, demo, "pdf");
            PackDialog.Show(v, v.SessionPath);
            UiApp.Instance.ShowLibrary();
            new InputDialog("Kommentar", "", null, kinds: true) { Owner = v }.ShowDialog();
            new InputDialog("Text des Schritts", "Klicken Sie auf „Speichern“.", "Datei speichern") { Owner = v }.ShowDialog();
            for (int tab = 0; tab < 5; tab++) new SettingsWindow(tab) { Owner = v }.ShowDialog();
            new CommentWindow().ShowDialog();
            Msg.Show("Aufnahme verwerfen?\n\nDer Sitzungsordner wird gelöscht. Das lässt sich nicht rückgängig machen.", "Aufnahme verwerfen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            var bar = new RecordingBar(new RecordingController(new RecordingOptions()));
            bar.Show();
            bar.SetTime(TimeSpan.FromSeconds(83), 12, RecorderState.Recording, false);
            bar.Message("Unsichtbarkeit geprüft · Schutznetz – Passwortfelder werden maskiert.");
        });
    }
}
