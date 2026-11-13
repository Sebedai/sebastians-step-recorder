using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Bild-Editor I (13, Paket 22): zwölf Farben, freie Farbe und Pipette, Farbe je Werkzeug, Stil von Zeichnungen
/// (Linienart, Spitzen, Füllung, Hintergrund, Form und Zählweise von Nummern, Deckkraft), Stil übertragen,
/// Schrittnummer im Bild, Markenstile und „Ziel markieren“.
/// </summary>
sealed partial class ViewerWindow
{
    /// <summary>Gelb des Markierstifts (bisher fest).</summary>
    const string HighlightYellow = "#FFEB3B";

    readonly Dictionary<string, string> _toolColor = new() { ["highlight"] = HighlightYellow };
    readonly Dictionary<string, JsonObject> _toolStyle = new();
    RadioButton? _customColorBtn;
    Ellipse? _customColorDot;
    bool _pipette;
    Dictionary<long, string>? _stepNumbers;

    // ---------------- Farben ----------------

    void BuildColorGrid(StackPanel pal)
    {
        var colorGrid = new UniformGrid { Columns = 3 };
        RadioButton Swatch(string hex, string tip, out Ellipse dot)
        {
            dot = new Ellipse { Width = 16, Height = 16, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), Stroke = Theme.LineStrong, StrokeThickness = 1 };
            var rb = new RadioButton { Content = dot, GroupName = "color", IsChecked = hex == _color, ToolTip = tip, Style = Theme.Style("ToolToggle"), Margin = new Thickness(1), MinHeight = 26, MinWidth = 0, Padding = new Thickness(3), Tag = hex };
            rb.Checked += (_, _) => PickColor((string)rb.Tag);
            return rb;
        }
        foreach (var (hex, name) in Palette)
        {
            var rb = Swatch(hex, Loc.F("Farbe: {0}", "Color: {0}", name), out _);
            _colorBtns.Add(rb); colorGrid.Children.Add(rb);
        }
        // eigene Farbe (zuletzt gewählt), freie Farbwahl, Pipette
        _customColorBtn = Swatch("#808080", Loc.L("Eigene Farbe", "Custom color"), out var cd);
        _customColorDot = cd; _customColorBtn.Visibility = Visibility.Collapsed;
        _colorBtns.Add(_customColorBtn);
        var more = new Button { Content = U.Icon(Theme.IPalette, 14), ToolTip = Loc.L("Weitere Farbe …", "More colors …"), Style = Theme.Style("Ghost"), Margin = new Thickness(1), MinHeight = 26, MinWidth = 0, Padding = new Thickness(3), Focusable = false };
        more.Click += (_, _) => ChooseCustomColor();
        var pip = new Button { Content = U.Icon(Theme.IPipette, 14), ToolTip = Loc.L("Pipette – Farbe aus dem Bild aufnehmen", "Eyedropper – pick a color from the image"), Style = Theme.Style("Ghost"), Margin = new Thickness(1), MinHeight = 26, MinWidth = 0, Padding = new Thickness(3), Focusable = false };
        pip.Click += (_, _) => SetPipette(!_pipette);
        _pipetteBtn = pip;
        var extra = new UniformGrid { Columns = 3 };
        extra.Children.Add(_customColorBtn); extra.Children.Add(more); extra.Children.Add(pip);
        pal.Children.Add(colorGrid);
        pal.Children.Add(extra);
    }

    Button? _pipetteBtn;

    /// <summary>Pipette ein/aus: der nächste Klick ins Bild nimmt die Farbe auf.</summary>
    void SetPipette(bool on)
    {
        _pipette = on;
        if (_pipetteBtn != null) _pipetteBtn.Background = on ? Theme.AccentSoft : Brushes.Transparent;
        _overlay.Cursor = on ? System.Windows.Input.Cursors.Pen : _tool == "select" ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Cross;
        if (on) _status.Text = Loc.L("Pipette: in das Bild klicken, um die Farbe zu übernehmen.", "Eyedropper: click into the image to pick the color.");
    }

    /// <summary>Farbe gewählt: merken je Werkzeug, auf die ausgewählte Zeichnung anwenden.</summary>
    void PickColor(string hex)
    {
        _color = hex;
        if (_syncingColor) return;
        if (_selAnn != null) RecolorSelected(hex);
        else if (_tool != "select") _toolColor[_tool] = hex;
    }

    /// <summary>Zeigt eine Farbe in der Palette als gewählt; nicht vorhandene Farben landen im Feld „eigene Farbe“.</summary>
    void ShowColor(string hex)
    {
        var btn = _colorBtns.FirstOrDefault(c => c != _customColorBtn && string.Equals((string)c.Tag, hex, StringComparison.OrdinalIgnoreCase));
        if (btn == null && _customColorBtn != null && _customColorDot != null)
        {
            try { _customColorDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); } catch { return; }
            _customColorBtn.Tag = hex; _customColorBtn.ToolTip = Loc.F("Eigene Farbe {0}", "Custom color {0}", hex);
            _customColorBtn.Visibility = Visibility.Visible;
            btn = _customColorBtn;
        }
        btn?.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
    }

    void SetColor(string hex)
    {
        bool sel = _syncingColor;
        _syncingColor = true; ShowColor(hex); _syncingColor = sel;
        PickColor(hex);
    }

    void ChooseCustomColor()
    {
        using var d = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true };
        try { d.Color = System.Drawing.ColorTranslator.FromHtml(_color); } catch { }
        if (d.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        SetColor($"#{d.Color.R:X2}{d.Color.G:X2}{d.Color.B:X2}");
    }

    /// <summary>Pipette: Farbe des angeklickten Bildpunkts (aus dem angezeigten Bild).</summary>
    bool PipetteAt(Point overlayPos)
    {
        if (!_pipette || _currentMeta?.Bitmap is not { } bmp) return false;
        int x = (int)Math.Clamp(overlayPos.X, 0, bmp.Width - 1), y = (int)Math.Clamp(overlayPos.Y, 0, bmp.Height - 1);
        var c = bmp.GetPixel(x, y);
        SetPipette(false);
        SetColor($"#{c.R:X2}{c.G:X2}{c.B:X2}");
        _status.Text = Loc.F("Farbe {0} übernommen", "Picked color {0}", _color);
        return true;
    }

    /// <summary>Werkzeug gewechselt: dessen zuletzt benutzte Farbe zeigen (Markierstift standardmäßig gelb).</summary>
    void ToolColorFor(string tool)
    {
        if (tool is "select" or "crop" or "redact" or "pixelate" or "spotlight") return;
        if (_toolColor.TryGetValue(tool, out var hex) && !string.Equals(hex, _color, StringComparison.OrdinalIgnoreCase))
        {
            _syncingColor = true; ShowColor(hex); _color = hex; _syncingColor = false;
        }
    }

    // ---------------- Stil von Zeichnungen ----------------

    /// <summary>Zusätzliche Stilschlüssel für neue Zeichnungen eines Werkzeugs.</summary>
    JsonObject? ToolStyleFor(string type)
    {
        var st = _toolStyle.TryGetValue(type, out var s) ? (JsonObject)s.DeepClone() : null;
        if (type == "highlight") { st ??= new JsonObject(); st["hue"] = _color; }
        return st;
    }

    /// <summary>Menü „Stil“: wirkt auf die ausgewählte Zeichnung, sonst auf neue Zeichnungen des aktuellen Werkzeugs.</summary>
    void ShowStyleMenu(FrameworkElement owner)
    {
        JsonObject? sel = _selAnn != null && _currentMeta != null ? AnnotationsFor(_currentMeta.FrameId).FirstOrDefault(x => x.Str("id") == _selAnn) : null;
        string type = sel?.Str("type") ?? _tool;
        var cur = sel?.Obj("style") ?? (_toolStyle.TryGetValue(type, out var ts) ? ts : new JsonObject());
        var m = U.MenuScope(new ContextMenu { PlacementTarget = owner, Placement = PlacementMode.Right });
        string Who() => sel != null ? Loc.L("Ausgewählte Zeichnung", "Selected drawing") : Loc.F("Neue Zeichnungen: {0}", "New drawings: {0}", ToolName(type));
        m.Items.Add(new MenuItem { Header = U.Text(Who(), Theme.FsCaption, true, Theme.Muted), IsHitTestVisible = false, Focusable = false });
        MenuItem Group(string label, string key, string? def, params (string? value, string text)[] opts)
        {
            var g = new MenuItem { Header = label };
            string? now = cur.Str(key) ?? def;
            foreach (var (value, text) in opts)
            {
                var mi = new MenuItem { Header = text, IsCheckable = true, IsChecked = now == value };
                string? v = value;
                mi.Click += (_, _) => SetStyleKey(sel, type, key, v == def ? null : v);
                g.Items.Add(mi);
            }
            return g;
        }
        bool lines = type is "arrow" or "line" or "rect" or "ellipse" or "freehand";
        if (lines) m.Items.Add(Group(Loc.L("Linienart", "Line style"), "dash", "solid", ("solid", Loc.L("durchgezogen", "solid")), ("dash", Loc.L("gestrichelt", "dashed")), ("dot", Loc.L("gepunktet", "dotted"))));
        if (type is "arrow" or "line")
            m.Items.Add(Group(Loc.L("Spitzen", "Heads"), "heads", type == "arrow" ? "end" : "none", ("end", Loc.L("am Ende", "at the end")), ("both", Loc.L("an beiden Enden", "at both ends")), ("start", Loc.L("am Anfang", "at the start")), ("none", Loc.L("keine", "none"))));
        if (type is "rect" or "ellipse")
            m.Items.Add(Group(Loc.L("Füllung", "Fill"), "fill", "none", ("none", Loc.L("keine", "none")), (sel?.Obj("style")?.Str("color") ?? _color, Loc.L("leicht in der Linienfarbe", "light in the line color"))));
        if (type == "text")
            m.Items.Add(Group(Loc.L("Hintergrund", "Background"), "bg", "white", ("white", Loc.L("weiß", "white")), ("none", Loc.L("keiner", "none")), ("fill", Loc.L("in der Farbe, Text weiß", "in the color, white text"))));
        if (type == "badge")
        {
            m.Items.Add(Group(Loc.L("Form", "Shape"), "shape", "circle", ("circle", Loc.L("Kreis", "Circle")), ("square", Loc.L("Quadrat", "Square")), ("rounded", Loc.L("abgerundet", "Rounded")), ("pin", Loc.L("Stecknadel", "Pin")), ("ring", Loc.L("Kreis mit Rand", "Circle with border"))));
            m.Items.Add(Group(Loc.L("Zählweise", "Counting"), "seq", "num", ("num", "1, 2, 3"), ("alpha", "a, b, c"), ("ALPHA", "A, B, C"), ("roman", "i, ii, iii")));
        }
        if (type is not ("select" or "crop" or "redact" or "pixelate" or "spotlight" or "magnifier"))
            m.Items.Add(Group(Loc.L("Deckkraft", "Opacity"), "opacity", null, (null, "100 %"), ("0.75", "75 %"), ("0.5", "50 %")));
        if (sel != null)
        {
            m.Items.Add(new Separator());
            m.Items.Add(U.Mi(Loc.F("Stil auf alle Zeichnungen „{0}“ übertragen", "Apply style to all “{0}” drawings", ToolName(type)), null, () => ApplyStyleToAll(sel.Str("id")!)));
            if (type == "badge")
            {
                m.Items.Add(U.Mi(Loc.L("Ab hier neu zählen bei …", "Restart counting here at …"), null, () => RestartBadge(sel)));
                m.Items.Add(U.Mi(sel.Bool("auto") ? Loc.L("Feste Beschriftung …", "Fixed label …") : Loc.L("Wieder automatisch zählen", "Count automatically again"), null, () => ToggleBadgeAuto(sel)));
            }
        }
        if (m.Items.Count <= 1) m.Items.Add(new MenuItem { Header = Loc.L("Für dieses Werkzeug gibt es keine Stiloptionen.", "This tool has no style options."), IsEnabled = false });
        m.IsOpen = true;
    }

    static string ToolName(string type) => type switch
    {
        "arrow" => Loc.L("Pfeil", "Arrow"), "line" => Loc.L("Linie", "Line"), "rect" => Loc.L("Rahmen", "Rectangle"), "ellipse" => Loc.L("Ellipse", "Ellipse"),
        "freehand" => Loc.L("Freihand", "Freehand"), "highlight" => Loc.L("Hervorheben", "Highlight"), "text" => Loc.L("Text", "Text"), "badge" => Loc.L("Nummer", "Number"),
        "spotlight" => "Spotlight", "magnifier" => Loc.L("Lupe", "Magnifier"), _ => type
    };

    void SetStyleKey(JsonObject? sel, string type, string key, string? value)
    {
        JsonNode? node = value == null ? null : key == "opacity" && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? JsonValue.Create(d) : JsonValue.Create(value);
        if (sel == null)
        {
            var st = _toolStyle.TryGetValue(type, out var s) ? s : _toolStyle[type] = new JsonObject();
            if (node == null) st.Remove(key); else st[key] = node;
            _status.Text = Loc.F("Gilt für neue Zeichnungen „{0}“", "Applies to new “{0}” drawings", ToolName(type));
            return;
        }
        string id = sel.Str("id")!, frame = sel.Str("frame") ?? "";
        Change(e =>
        {
            if (AnnotationEdit(e, id) is not { } a) return;
            var st = a.Obj("style") ?? new JsonObject();
            if (node == null) st.Remove(key); else st[key] = node.DeepClone();
            a["style"] = st;
            if (key == "seq") Annotations.Renumber(e, frame);
        }, refill: false);
        // auch neue Zeichnungen dieser Art so zeichnen
        var ts = _toolStyle.TryGetValue(type, out var t2) ? t2 : _toolStyle[type] = new JsonObject();
        if (node == null) ts.Remove(key); else ts[key] = node.DeepClone();
    }

    void ApplyStyleToAll(string id)
    {
        int n = 0;
        Change(e => n = Annotations.ApplyStyleToSameType(e, id), refill: false);
        InvalidateAllThumbs();
        _status.Text = Loc.F("Stil auf {0} weitere Zeichnungen übertragen – rückgängig mit Strg+Z", "Style applied to {0} more drawings – undo with Ctrl+Z", n);
    }

    void RestartBadge(JsonObject sel)
    {
        var d = new InputDialog(Loc.L("Ab dieser Nummer neu zählen bei", "Restart counting at this number with"), "1") { Owner = this };
        if (d.ShowDialog() != true || !int.TryParse(d.Value.Trim(), out var start) || start < 1 || start > 9999) return;
        string id = sel.Str("id")!, frame = sel.Str("frame") ?? "";
        Change(e => { if (AnnotationEdit(e, id) is { } a) { a["auto"] = true; a["start"] = start; Annotations.Renumber(e, frame); } }, refill: false);
    }

    void ToggleBadgeAuto(JsonObject sel)
    {
        string id = sel.Str("id")!, frame = sel.Str("frame") ?? "";
        if (sel.Bool("auto"))
        {
            var d = new InputDialog(Loc.L("Feste Beschriftung der Nummer", "Fixed label of the number"), sel.Str("text") ?? "") { Owner = this };
            if (d.ShowDialog() != true || d.Value.Trim().Length == 0) return;
            string text = d.Value.Trim();
            Change(e => { if (AnnotationEdit(e, id) is { } a) { a.Remove("auto"); a.Remove("start"); a["text"] = text.Length > 6 ? text[..6] : text; Annotations.Renumber(e, frame); } }, refill: false);
        }
        else Change(e => { if (AnnotationEdit(e, id) is { } a) { a["auto"] = true; Annotations.Renumber(e, frame); } }, refill: false);
    }

    // ---------------- Schrittnummer und Markenstil ----------------

    /// <summary>Gedruckte Schrittnummer für das Bild, wenn „Schrittnummer“ eingeschaltet ist.</summary>
    string? StepLabel(ViewStep s)
    {
        if (!_view.MarksFor(s).Number) return null;
        var map = _stepNumbers ??= GuideExport.StepNumbers(_view);
        return map.TryGetValue(s.Anchor, out var n) && n.Length > 0 ? n : null;
    }

    /// <summary>Kennung der Nummernfolge – ändert sie sich (Umsortieren, Kapitel), müssen alle Vorschaubilder neu.</summary>
    string NumbersSignature()
    {
        _stepNumbers = null;
        if (!_view.MarksFor(null).Number && !(_view.Edits.Arr("steps") ?? new JsonArray()).OfType<JsonObject>().Any(x => x.Obj("auto")?["stepNumber"] != null)) return "";
        var map = _stepNumbers = GuideExport.StepNumbers(_view);
        return string.Join(",", map.Select(kv => kv.Key + ":" + kv.Value));
    }

    MenuItem MarkStyleMenu()
    {
        string cur = _view.Edits.Obj("guide")?.Obj("auto")?.Obj("style")?.Str("preset") ?? "classic";
        var sub = Sub(Loc.L("Stil der Marken", "Mark style"), Theme.IPalette);
        foreach (var (id, de, en) in MarkStyle.Presets)
        {
            string k = id;
            sub.Items.Add(Check(Loc.L(de, en), cur == id, () => ApplyMarkPreset(k)));
        }
        sub.Items.Add(Sep());
        sub.Items.Add(U.Mi(Loc.L("Farbe der Marken …", "Color of the marks …"), null, ChooseMarkColor));
        var shapes = Sub(Loc.L("Form der Schrittnummer", "Shape of the step number"), null);
        string shape = _view.MarkStyle()?.NumberShape ?? "circle";
        foreach (var (v, t) in new[] { ("circle", Loc.L("Kreis", "Circle")), ("rounded", Loc.L("abgerundet", "Rounded")), ("square", Loc.L("Quadrat", "Square")) })
        {
            string k = v;
            shapes.Items.Add(Check(t, shape == v, () => SetMarkStyleKey("numberShape", k == "circle" ? null : k)));
        }
        sub.Items.Add(shapes);
        return sub;
    }

    void ApplyMarkPreset(string id)
    {
        var (style, number) = MarkStyle.PresetJson(id);
        Change(e =>
        {
            var g = e.Obj("guide") ?? new JsonObject(); e["guide"] = g;
            var auto = g.Obj("auto") ?? new JsonObject(); g["auto"] = auto;
            if (style == null) auto.Remove("style"); else auto["style"] = style;
            if (number) auto["stepNumber"] = true; else if (id == "classic") auto.Remove("stepNumber");
            if (auto.Count == 0) g.Remove("auto");
        }, refill: false);
        InvalidateAllThumbs();
        _status.Text = Loc.F("Markenstil „{0}“ für alle Schritte", "Mark style “{0}” for all steps", Loc.L(MarkStyle.Presets.First(p => p.id == id).de.Split(' ')[0], MarkStyle.Presets.First(p => p.id == id).en.Split(' ')[0]));
    }

    void SetMarkStyleKey(string key, string? value)
    {
        Change(e =>
        {
            var g = e.Obj("guide") ?? new JsonObject(); e["guide"] = g;
            var auto = g.Obj("auto") ?? new JsonObject(); g["auto"] = auto;
            var st = auto.Obj("style") ?? new JsonObject { ["preset"] = "custom" };
            if (value == null) st.Remove(key); else st[key] = value;
            if (st.Str("preset") is null or "classic") st["preset"] = "custom";
            auto["style"] = st;
        }, refill: false);
        InvalidateAllThumbs();
    }

    void ChooseMarkColor()
    {
        using var d = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true, Color = System.Drawing.Color.FromArgb(220, 30, 30) };
        if (d.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        string hex = $"#{d.Color.R:X2}{d.Color.G:X2}{d.Color.B:X2}";
        Change(e =>
        {
            var g = e.Obj("guide") ?? new JsonObject(); e["guide"] = g;
            var auto = g.Obj("auto") ?? new JsonObject(); g["auto"] = auto;
            var st = auto.Obj("style") ?? new JsonObject();
            st["clickColor"] = hex; st["numberColor"] = hex;
            if (st.Str("preset") is null or "classic") st["preset"] = "custom";
            auto["style"] = st;
        }, refill: false);
        InvalidateAllThumbs();
    }

    /// <summary>
    /// „Ziel markieren“: macht aus Zielrechteck und Klickstelle des Schritts bearbeitbare Zeichnungen – Rahmen um das Ziel
    /// und eine Nummer daneben (für alle gewählten Schritte).
    /// </summary>
    void MarkTarget()
    {
        var steps = SelSteps.Count > 0 ? SelSteps : Sel is { } one ? new List<ViewStep> { one } : new();
        int done = 0;
        var plans = new List<(ViewStep s, string frame, System.Drawing.Rectangle r)>();
        foreach (var s in steps)
        {
            if (s.Manual || s.Redacted) continue;
            using var img = StepImages.Render(_view, s, "before", "original", annotate: true, applyCrop: false);
            if (img?.TargetRect is not { } t || t.Width < 4 || t.Height < 4) continue;
            // Ausgabe → Bildkoordinaten (bei "original" ist der Maßstab 1, die Quelle der ganze Bildschirm)
            var r = new System.Drawing.Rectangle(img.SourceRect.X + (int)(t.X / img.Scale), img.SourceRect.Y + (int)(t.Y / img.Scale), (int)(t.Width / img.Scale), (int)(t.Height / img.Scale));
            plans.Add((s, img.FrameId, r));
        }
        if (plans.Count == 0) { _status.Text = Loc.L("Für diesen Schritt ist kein Ziel bekannt.", "No target is known for this step."); return; }
        string color = _color; int width = _stroke, fontSize = _fontSize;
        Change(e =>
        {
            var arr = e["annotations"] as JsonArray ?? new JsonArray(); e["annotations"] = arr;
            foreach (var (s, frame, r) in plans)
            {
                var pad = 4;
                arr.Add(new JsonObject { ["id"] = NextId(e, "annotations", "a"), ["frame"] = frame, ["anchor"] = s.Anchor, ["type"] = "rect", ["geom"] = new JsonArray(r.X - pad, r.Y - pad, r.Width + 2 * pad, r.Height + 2 * pad), ["style"] = new JsonObject { ["color"] = color, ["width"] = width, ["fontSize"] = fontSize } });
                arr.Add(new JsonObject { ["id"] = NextId(e, "annotations", "a"), ["frame"] = frame, ["anchor"] = s.Anchor, ["type"] = "badge", ["auto"] = true, ["text"] = "1", ["geom"] = new JsonArray(r.X - pad, r.Y - pad), ["style"] = new JsonObject { ["color"] = color, ["width"] = width, ["fontSize"] = fontSize } });
                Annotations.Renumber(e, frame);
                done++;
            }
        }, refill: false);
        InvalidateAllThumbs();
        _status.Text = Loc.F("Ziel markiert bei {0} Schritten – die Zeichnungen lassen sich bearbeiten.", "Target marked in {0} steps – the drawings can be edited.", done);
    }
}
