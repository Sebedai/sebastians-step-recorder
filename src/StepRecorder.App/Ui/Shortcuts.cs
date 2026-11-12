using System.Windows;
using System.Windows.Controls;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>Beschreibung eines Editor-Befehls: Name, Symbol und Tastenkürzel stehen nur hier.</summary>
sealed record CmdInfo(string Id, string Label, string? Glyph, string? Gesture, string Group);

/// <summary>
/// Katalog aller Editor-Befehle. Menüleiste, Befehlsknöpfe, Rechtsklick-Menüs und die Tastenkürzel-Übersicht
/// lesen Namen, Symbole und Kürzel von hier – dadurch heißt jeder Befehl überall gleich.
/// Die Gruppen entsprechen den Menüs der Menüleiste.
/// </summary>
static class EditorCommands
{
    public static readonly CmdInfo[] All =
    {
        // Datei
        new("newRec", "Neue Aufnahme", Theme.IRecord, "Strg+N", "Datei"),
        new("open", "Öffnen …", Theme.IOpen, "Strg+O", "Datei"),
        new("rename", "Umbenennen …", Theme.IEdit, null, "Datei"),
        new("preview", "Vorschau im Browser", Theme.IEye, null, "Datei"),
        new("export", "Exportieren …", Theme.IExport, "Strg+E", "Datei"),
        new("support", "Support-Paket erstellen …", Theme.IWrench, null, "Datei"),
        new("continueRec", "Aufnahme fortsetzen", Theme.IRecord, null, "Datei"),
        new("append", "Aufnahme anhängen …", Theme.IAdd, null, "Datei"),
        new("showFolder", "Aufnahmeordner zeigen", Theme.IFolder, null, "Datei"),
        new("settings", "Einstellungen …", Theme.ISettings, null, "Datei"),
        new("close", "Schließen", Theme.IClose, "Strg+W", "Datei"),
        // Bearbeiten
        new("undo", "Rückgängig", Theme.IUndo, "Strg+Z", "Bearbeiten"),
        new("redo", "Wiederholen", Theme.IRedo, "Strg+Y", "Bearbeiten"),
        new("copyImage", "Bild kopieren", Theme.ICopy, "Strg+C", "Bearbeiten"),
        new("copyImageText", "Bild mit Text kopieren", null, "Strg+Umschalt+C", "Bearbeiten"),
        new("copyText", "Text kopieren", null, "Strg+Umschalt+T", "Bearbeiten"),
        new("copyMarkdown", "Als Markdown kopieren", null, "Strg+M", "Bearbeiten"),
        new("copyOriginal", "Originalbild kopieren", null, "Strg+Umschalt+O", "Bearbeiten"),
        new("copyGuide", "Ganze Anleitung kopieren", Theme.IDoc, null, "Bearbeiten"),
        new("find", "Schritte durchsuchen", Theme.ISearch, "Strg+F", "Bearbeiten"),
        new("replace", "Suchen und Ersetzen …", null, "Strg+H", "Bearbeiten"),
        // Schritt
        new("editText", "Text bearbeiten", Theme.IEdit, "F2", "Schritt"),
        new("addNote", "Hinweis hinzufügen …", Theme.IComment, "K", "Schritt"),
        new("moreImportant", "Wichtiger", null, "Strg+↑", "Schritt"),
        new("lessImportant", "Unwichtiger", null, "Strg+↓", "Schritt"),
        new("chapter", "Kapitel hier beginnen", Theme.IDoc, null, "Schritt"),
        new("hide", "Ausblenden", Theme.IEye, "Entf", "Schritt"),
        new("moveUp", "Nach oben", Theme.IArrowUp, "Alt+↑", "Schritt"),
        new("moveDown", "Nach unten", Theme.IArrowDown, "Alt+↓", "Schritt"),
        new("insertText", "Textschritt", Theme.IText, "Umschalt+Einfg", "Schritt"),
        new("insertFile", "Bild aus Datei …", Theme.IOpen, null, "Schritt"),
        new("insertClip", "Bild aus der Zwischenablage", Theme.ICopy, null, "Schritt"),
        new("insertShot", "Bildschirmfoto in 3 Sekunden …", Theme.ICamera, null, "Schritt"),
        new("deleteInserted", "Eingefügten Schritt löschen", Theme.IDelete, null, "Schritt"),
        new("prevStep", "Vorheriger Schritt", Theme.IPrev, "↑", "Schritt"),
        new("nextStep", "Nächster Schritt", Theme.INext, "↓", "Schritt"),
        // Bild
        new("toolSelect", "Auswählen", Theme.IPointer, "V", "Bild"),
        new("toolArrow", "Pfeil", Theme.IArrow, "A", "Bild"),
        new("toolRect", "Rahmen", Theme.IRect, "R", "Bild"),
        new("toolEllipse", "Ellipse", null, "E", "Bild"),
        new("toolHighlight", "Hervorheben", Theme.IHighlight, "M", "Bild"),
        new("toolText", "Text", Theme.IText, "T", "Bild"),
        new("toolBadge", "Nummer", null, "N", "Bild"),
        new("toolRedact", "Schwärzen", Theme.IRedact, "X", "Bild"),
        new("toolPixelate", "Verpixeln", Theme.IPixelate, "P", "Bild"),
        new("crop", "Zuschneiden", Theme.ICrop, "C", "Bild"),
        new("cropRemove", "Zuschnitt entfernen", null, null, "Bild"),
        new("cropAll", "Zuschnitt auf alle Schritte übertragen", null, null, "Bild"),
        new("replaceShot", "Bildschirmfoto in 3 Sekunden …", Theme.ICamera, null, "Bild"),
        new("replaceFile", "Bild aus Datei …", Theme.IOpen, null, "Bild"),
        new("replaceClip", "Bild aus der Zwischenablage", Theme.ICopy, null, "Bild"),
        new("restoreImage", "Originalbild wiederherstellen", Theme.IUndo, null, "Bild"),
        new("deleteDrawing", "Ausgewählte Zeichnung löschen", Theme.IDelete, "Entf", "Bild"),
        new("clearDrawings", "Alle Zeichnungen dieses Bilds löschen", null, null, "Bild"),
        new("saveImage", "Bild speichern …", Theme.ISave, "Strg+Umschalt+S", "Bild"),
        new("saveClip", "Kurzen Clip speichern (GIF) …", null, null, "Bild"),
        // Ansicht
        new("beforeAfter", "Vorher/Nachher wechseln", Theme.ICompare, "W", "Ansicht"),
        new("stepMarks", "Mausmarken dieses Schritts ein/aus", Theme.IMouse, "H", "Ansicht"),
        new("play", "Abspielen", Theme.IPlay, "F5", "Ansicht"),
        new("lightbox", "Großansicht", Theme.IFullScreen, "F11", "Ansicht"),
        new("zoomIn", "Vergrößern", Theme.IZoomIn, "Strg+Plus", "Ansicht"),
        new("zoomOut", "Verkleinern", Theme.IZoomOut, "Strg+Minus", "Ansicht"),
        new("zoomFit", "Einpassen", Theme.IFit, "Strg+0", "Ansicht"),
        new("zoom100", "Originalgröße (100 %)", Theme.IActual, "Strg+1", "Ansicht"),
        new("compare", "Mit anderer Aufnahme vergleichen …", null, "F7", "Ansicht"),
        // Prüfen
        new("review", "Durchsicht der Eingaben …", Theme.IChecklist, "Strg+Umschalt+R", "Prüfen"),
        new("piiScan", "Persönliche Daten in Bildern suchen …", Theme.ISearch, null, "Prüfen"),
        new("redactText", "Text überall schwärzen …", null, null, "Prüfen"),
        new("redactStep", "Diesen Schritt schwärzen", Theme.IShield, "Umschalt+Entf", "Prüfen"),
        new("generalize", "Werte verallgemeinern …", null, "Strg+Umschalt+G", "Prüfen"),
        new("cleanup", "Aufräumen …", null, null, "Prüfen"),
        new("overview", "Übersicht und Vollständigkeit …", Theme.IInfo, null, "Prüfen"),
        new("finalRedact", "Endgültig schwärzen …", Theme.IWarn, null, "Prüfen"),
        // Hilfe
        new("shortcuts", "Tastenkürzel", Theme.IKeyboard, "F1", "Hilfe"),
        new("help", "Hilfe und Info", Theme.IHelp, null, "Hilfe"),
    };

    static readonly Dictionary<string, CmdInfo> ById = All.ToDictionary(c => c.Id);

    public static CmdInfo Get(string id) => ById[id];

    /// <summary>Tooltip „Name (Kürzel)“.</summary>
    public static string Tip(string id) { var c = Get(id); return c.Gesture is { } g ? $"{Trim(c.Label)} ({g})" : Trim(c.Label); }

    static string Trim(string label) => label.EndsWith(" …") ? label[..^2] : label;
}

/// <summary>
/// Die eine Tastenkürzel-Übersicht des Programms: Aufnahme (aus den Einstellungen), Editor (aus
/// <see cref="EditorCommands"/>) und Großansicht. Startseite „Hilfe“, Editor (F1) und Aufnahmeleiste zeigen sie.
/// </summary>
static class Shortcuts
{
    public sealed record Group(string Title, List<(string label, string keys)> Items);

    /// <summary>„Ctrl+Shift+F9“ → „Strg+Umschalt+F9“.</summary>
    public static string De(string combo) => string.Join("+", combo.Split('+').Select(p => p.Trim() switch { "Ctrl" => "Strg", "Shift" => "Umschalt", var x => x }));

    public static List<Group> Recording()
    {
        Dictionary<string, string> hk;
        try { hk = AppSettings.Load().Hotkeys; } catch { hk = new(); }
        string K(string name, string def) => De(hk.GetValueOrDefault(name, def));
        return new()
        {
            new("Aufnahme", new()
            {
                ("Sofort aufnehmen · Pause · Fortsetzen", K("instantPause", "Ctrl+Shift+F9")),
                ("Aufnahme beenden", K("stop", "Ctrl+Shift+F10")),
                ("Kommentar", K("comment", "Ctrl+Shift+F12")),
                ("Diktat ein/aus", K("dictation", "Ctrl+Shift+F5")),
                ("Marker setzen", K("marker", "Ctrl+Shift+F7")),
                ("Marker „Fehler“", K("markerError", "Ctrl+Shift+F1")),
                ("Marker „Erwartet“", K("markerExpected", "Ctrl+Shift+F2")),
                ("Schnappschuss", K("snapshot", "Ctrl+Shift+F11")),
                ("Privattaste ein/aus", K("privacy", "Ctrl+Shift+F8")),
                ("Letzten Schritt zurücknehmen", K("discard", "Ctrl+Shift+F6")),
                ("Leiste ein/aus", K("bar", "Ctrl+Shift+F4")),
            }),
        };
    }

    /// <summary>Editor-Kürzel nach Menüs gruppiert, dazu Maus- und Werkzeugbedienung.</summary>
    public static List<Group> Editor()
    {
        var groups = new List<Group>();
        foreach (var g in EditorCommands.All.Where(c => c.Gesture != null).GroupBy(c => c.Group))
        {
            var items = g.Select(c => (c.Label.TrimEnd('…', ' '), c.Gesture!)).ToList();
            if (g.Key == "Schritt") items.Add(("Hinweis hinzufügen (zweite Taste)", "Einfg"));
            if (g.Key == "Bild")
            {
                items.Add(("Auswahl aufheben", "Esc"));
                items.Add(("Bereich kopieren", "Alt+Ziehen"));
            }
            if (g.Key == "Ansicht")
            {
                items.Add(("Zoomen", "Strg+Mausrad"));
                items.Add(("Bild verschieben", "Leertaste+Ziehen"));
            }
            groups.Add(new(g.Key, items));
        }
        groups.Add(new("Großansicht", new() { ("Vorheriges / nächstes Bild", "← →"), ("Erstes / letztes Bild", "Pos1 / Ende"), ("Einpassen / 100 %", "Doppelklick"), ("Schließen", "Esc") }));
        return groups;
    }

    /// <summary>Übersicht als Oberfläche: Gruppen in Spalten, je Zeile Befehl und Tastenkappen.</summary>
    public static FrameworkElement View(IEnumerable<Group> groups, int columns = 2)
    {
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = columns };
        foreach (var g in groups)
        {
            var col = new StackPanel { Margin = new Thickness(0, 0, 28, 20) };
            col.Children.Add(U.Text(g.Title, Theme.FsBody, true, margin: new Thickness(0, 0, 0, 6)));
            var rows = new Grid();
            rows.ColumnDefinitions.Add(new ColumnDefinition());
            rows.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int r = 0;
            foreach (var (label, keys) in g.Items)
            {
                rows.RowDefinitions.Add(new RowDefinition());
                var l = U.Text(label, Theme.FsHint, fg: Theme.Fg2, margin: new Thickness(0, 3, 12, 3)); l.VerticalAlignment = VerticalAlignment.Center; l.TextWrapping = TextWrapping.Wrap;
                // kompakte Tastenkappe mit dem ganzen Kürzel (gut lesbar, braucht wenig Breite)
                var k = new Border
                {
                    Background = Theme.Field, BorderBrush = Theme.CardLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.RSmall), Padding = new Thickness(6, 1, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 0, 2),
                    Child = new TextBlock { Text = keys, FontSize = Theme.FsCaption, Foreground = Theme.Fg }
                };
                Grid.SetRow(l, r); Grid.SetRow(k, r); Grid.SetColumn(k, 1);
                rows.Children.Add(l); rows.Children.Add(k); r++;
            }
            col.Children.Add(rows);
            grid.Children.Add(col);
        }
        return grid;
    }

    /// <summary>Eigenes Fenster mit der Übersicht (Editor F1, Aufnahmeleiste).</summary>
    public static void ShowWindow(Window? owner, IEnumerable<Group> groups, string title = "Tastenkürzel")
    {
        var w = new Window { Title = title, Width = 980, SizeToContent = SizeToContent.Height, WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = owner };
        Theme.Prepare(w, Theme.Surface);
        var list = groups.ToList();
        U.Dialog(w, title, View(list, list.Count > 3 ? 3 : 2), U.Primary("Schließen", w.Close));
        U.FitDialog(w);
        w.ShowDialog();
    }
}
