using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Export;

/// <summary>
/// Zeichnet Tastatureingaben als kleine Tastatur-Tasten ins Schrittbild (wie ein „Keycast“ in Bildschirmvideos):
/// Tastenkombinationen und Einzeltasten unten mittig, getippter Text als Eingabefeld unter dem Zielfeld.
/// Maskierte, geschwärzte und ungeprüfte Eingaben zeigen keinen Inhalt.
/// </summary>
public static class KeyCaps
{
    enum Kind { Key, Plus, Text, Badge, Note }
    sealed record Item(Kind K, string Text);

    static readonly Color PanelBg = Color.FromArgb(226, 24, 25, 30), CapTop = Color.FromArgb(255, 255, 255), CapBottom = Color.FromArgb(226, 230, 237),
        CapEdge = Color.FromArgb(139, 147, 160), CapShade = Color.FromArgb(118, 126, 140), CapText = Color.FromArgb(28, 31, 38), Accent = Color.FromArgb(67, 97, 238);

    /// <summary>Beschriftungen der Tasten wie auf einer Tastatur (deutsch/englisch).</summary>
    public static string Label(string k, bool de) => k switch
    {
        "LeftCtrl" or "RightCtrl" or "Ctrl" => de ? "Strg" : "Ctrl",
        "LeftShift" or "RightShift" or "Shift" => de ? "Umschalt" : "Shift",
        "LeftAlt" or "Alt" => "Alt",
        "RightAlt" or "AltGr" => "AltGr",
        "LeftWin" or "RightWin" or "Win" => "Win",
        "Enter" or "NumPadEnter" => de ? "Eingabe" : "Enter",
        "Escape" => "Esc",
        "Space" => de ? "Leertaste" : "Space",
        "Backspace" => de ? "Rücktaste" : "Backspace",
        "Delete" => de ? "Entf" : "Del",
        "Insert" => de ? "Einfg" : "Ins",
        "Tab" => "Tab",
        "Home" => de ? "Pos1" : "Home",
        "End" => de ? "Ende" : "End",
        "PageUp" => de ? "Bild ↑" : "PgUp",
        "PageDown" => de ? "Bild ↓" : "PgDn",
        "Left" => "←", "Right" => "→", "Up" => "↑", "Down" => "↓",
        "PrintScreen" => de ? "Druck" : "PrtSc",
        "Apps" => de ? "Menü" : "Menu",
        "CapsLock" => de ? "Feststell" : "Caps Lock",
        "NumLock" => "Num", "ScrollLock" => de ? "Rollen" : "Scroll",
        "NumPadAdd" => "Num +", "NumPadSubtract" => "Num −", "NumPadMultiply" => "Num *", "NumPadDivide" => "Num /", "NumPadDecimal" => de ? "Num ," : "Num .",
        _ when k.StartsWith("NumPad", StringComparison.Ordinal) => "Num " + k[6..],
        "OemPlus" => "+", "OemComma" => ",", "OemMinus" => "-", "OemPeriod" => ".",
        "Oem1" => de ? "Ü" : ";", "Oem2" => de ? "#" : "/", "Oem3" => de ? "Ö" : "`", "Oem4" => de ? "ß" : "[",
        "Oem5" => de ? "^" : "\\", "Oem6" => de ? "´" : "]", "Oem7" => de ? "Ä" : "'", "Oem102" => "<",
        "VolumeUp" => de ? "Lauter" : "Vol +", "VolumeDown" => de ? "Leiser" : "Vol −", "VolumeMute" => de ? "Stumm" : "Mute",
        "MediaPlayPause" => "▶ ❚❚", "BrowserBack" => de ? "Zurück" : "Back", "BrowserForward" => de ? "Vorwärts" : "Forward",
        _ => k
    };

    static string ModKey(string m) => m switch { "ctrl" => "Ctrl", "shift" => "Shift", "alt" => "Alt", "win" => "Win", "altGr" => "AltGr", _ => m };

    /// <summary>Was für einen Schritt gezeigt wird; leer, wenn der Schritt keine Tastatureingabe ist.</summary>
    static List<Item> Items(ViewStep step, SessionView view, bool de)
    {
        var list = new List<Item>();
        var input = step.Json.Obj("input");
        if (input == null || step.Redacted) return list;
        void Keys(IEnumerable<string> keys)
        {
            bool first = true;
            foreach (var k in keys) { if (!first) list.Add(new(Kind.Plus, "+")); list.Add(new(Kind.Key, Label(k, de))); first = false; }
        }
        switch (step.Kind)
        {
            case "hotkey":
                {
                    var keys = input.StrList("keys");
                    if (keys.Count == 0) keys = (input.Str("combo") ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries).ToList();
                    Keys(keys);
                    if (input.Int("count", 1) > 1) list.Add(new(Kind.Badge, input.Int("count") + "×"));
                    break;
                }
            case "key":
                {
                    var keys = input.StrList("mods").Select(ModKey).ToList();
                    if (input.Str("key") is { } k) keys.Add(k);
                    Keys(keys);
                    if (input.Int("count", 1) > 1) list.Add(new(Kind.Badge, input.Int("count") + "×"));
                    if (input.Bool("held")) list.Add(new(Kind.Note, de ? "gehalten" : "held"));
                    break;
                }
            case "type":
                if (input.Bool("masked"))
                {
                    int n = input.Int("count", 0);
                    list.Add(new(Kind.Text, new string('•', Math.Clamp(n, 4, 12))));
                    list.Add(new(Kind.Note, de ? "verborgen" : "hidden"));
                }
                else if (step.Unreviewed) list.Add(new(Kind.Note, de ? "Eingabe – noch nicht durchgesehen" : "Input – not reviewed yet"));
                else
                {
                    const int MaxChars = 44;
                    int used = 0;
                    foreach (var tok in input.Arr("tokens") ?? new JsonArray())
                    {
                        if (used >= MaxChars) { list.Add(new(Kind.Note, "…")); break; }
                        if (tok is JsonValue v && v.TryGetValue<string>(out var s))
                        {
                            s = view.Clean(s).Replace("\r", "").Replace("\n", "⏎").Replace("\t", "⇥");
                            if (s.Length + used > MaxChars) s = s[..Math.Max(1, MaxChars - used)] + "…";
                            used += s.Length;
                            if (list.Count > 0 && list[^1].K == Kind.Text) list[^1] = list[^1] with { Text = list[^1].Text + s };
                            else list.Add(new(Kind.Text, s));
                        }
                        else if (tok is JsonObject o)
                        {
                            if (o["masked"] != null) { list.Add(new(Kind.Text, "••••")); used += 4; continue; }
                            if (o.Str("key") is not { } key) { list.Add(new(Kind.Note, "…")); continue; }
                            Keys(o.StrList("mods").Select(ModKey).Append(key));
                            if (o.Int("n", 1) > 1) list.Add(new(Kind.Badge, o.Int("n") + "×"));
                            used += 3;
                        }
                    }
                }
                if (input.Obj("terminator") is { } term && term.Str("key") is { } tk)
                {
                    if (list.Count > 0) list.Add(new(Kind.Plus, "→"));
                    Keys(term.StrList("mods").Select(ModKey).Append(tk));
                }
                break;
        }
        return list;
    }

    /// <summary>Zeichnet die Tastatureingabe des Schritts ins Bild (nichts, wenn der Schritt keine hat).</summary>
    public static void Draw(Graphics g, ViewStep step, RenderedImage img, Size size, SessionView view)
    {
        bool de = view.Language != "en";
        var items = Items(step, view, de);
        if (items.Count == 0) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        float keyH = (float)Math.Clamp(Math.Min(size.Height * 0.058, size.Width * 0.04), 28, 54);
        float pad = keyH * 0.30f, gap = keyH * 0.20f, radius = keyH * 0.22f;
        using var capFont = new Font("Segoe UI Semibold", keyH * 0.42f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var textFont = new Font("Segoe UI", keyH * 0.46f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var noteFont = new Font("Segoe UI", keyH * 0.38f, FontStyle.Italic, GraphicsUnit.Pixel);
        var fmt = StringFormat.GenericTypographic;

        float Width(Item it) => it.K switch
        {
            Kind.Key => Math.Max(keyH, g.MeasureString(it.Text, capFont, PointF.Empty, fmt).Width + keyH * 0.62f),
            Kind.Text => g.MeasureString(it.Text, textFont, PointF.Empty, fmt).Width + keyH * 0.9f,
            Kind.Badge => g.MeasureString(it.Text, capFont, PointF.Empty, fmt).Width + keyH * 0.5f,
            Kind.Note => g.MeasureString(it.Text, noteFont, PointF.Empty, fmt).Width + keyH * 0.1f,
            _ => g.MeasureString(it.Text, capFont, PointF.Empty, fmt).Width + keyH * 0.1f
        };
        var widths = items.Select(Width).ToList();
        float innerW = widths.Sum() + gap * (items.Count - 1);
        float panelW = innerW + 2 * pad, panelH = keyH + 2 * pad + keyH * 0.08f;
        // Zu breit: Zeichenfläche verkleinern statt abschneiden
        float shrink = Math.Min(1f, (size.Width - 16) / panelW);
        var state = g.Save();

        // Lage: Texteingabe unter/über dem Zielfeld, sonst unten mittig
        float px = (size.Width - panelW * shrink) / 2, py = size.Height - panelH * shrink - Math.Max(14, size.Height * 0.045f);
        if (step.Kind == "type" && img.TargetRect is { } tr && tr.Width > 0)
        {
            float below = tr.Bottom + keyH * 0.35f, above = tr.Top - keyH * 0.35f - panelH * shrink;
            px = Math.Clamp(tr.Left, 8, Math.Max(8, size.Width - panelW * shrink - 8));
            if (below + panelH * shrink <= size.Height - 6) py = below;
            else if (above >= 6) py = above;
        }
        g.TranslateTransform(px, py);
        g.ScaleTransform(shrink, shrink);

        // Schatten und Fläche
        for (int i = 4; i >= 1; i--)
            using (var sh = new SolidBrush(Color.FromArgb(14, 0, 0, 0)))
            using (var sp = Round(new RectangleF(-i, -i + 3, panelW + 2 * i, panelH + 2 * i), radius * 1.6f + i)) g.FillPath(sh, sp);
        using (var bg = new SolidBrush(PanelBg)) using (var pp = Round(new RectangleF(0, 0, panelW, panelH), radius * 1.6f)) g.FillPath(bg, pp);
        using (var edge = new Pen(Color.FromArgb(70, 255, 255, 255), 1)) using (var pp = Round(new RectangleF(0.5f, 0.5f, panelW - 1, panelH - 1), radius * 1.6f)) g.DrawPath(edge, pp);

        float x = pad, y = pad;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i]; float w = widths[i];
            switch (it.K)
            {
                case Kind.Key:
                    {
                        float depth = keyH * 0.09f;
                        var face = new RectangleF(x, y, w, keyH - depth);
                        using (var shade = new SolidBrush(CapShade)) using (var sp = Round(new RectangleF(x, y + depth, w, keyH - depth), radius)) g.FillPath(shade, sp);
                        using (var lg = new LinearGradientBrush(face, CapTop, CapBottom, LinearGradientMode.Vertical)) using (var fp = Round(face, radius)) g.FillPath(lg, fp);
                        using (var pen = new Pen(CapEdge, 1)) using (var fp = Round(face, radius)) g.DrawPath(pen, fp);
                        Centered(g, it.Text, capFont, new SolidBrush(CapText), face, fmt);
                        break;
                    }
                case Kind.Text:
                    {
                        var box = new RectangleF(x, y + keyH * 0.04f, w, keyH * 0.92f);
                        using (var fb = new SolidBrush(Color.FromArgb(250, 250, 251))) using (var bp = Round(box, radius * 0.8f)) g.FillPath(fb, bp);
                        using (var ac = new Pen(Accent, Math.Max(1.5f, keyH * 0.06f))) g.DrawLine(ac, box.Left + radius * 0.6f, box.Bottom - 1, box.Right - radius * 0.6f, box.Bottom - 1);
                        var tsz = g.MeasureString(it.Text, textFont, PointF.Empty, fmt);
                        float tx = box.Left + keyH * 0.36f, ty = box.Top + (box.Height - tsz.Height) / 2;
                        using (var tb = new SolidBrush(CapText)) g.DrawString(it.Text, textFont, tb, tx, ty, fmt);
                        // Schreibmarke
                        using (var caret = new Pen(Accent, Math.Max(1.2f, keyH * 0.045f))) g.DrawLine(caret, tx + tsz.Width + 2, box.Top + box.Height * 0.22f, tx + tsz.Width + 2, box.Bottom - box.Height * 0.22f);
                        break;
                    }
                case Kind.Badge:
                    {
                        var b = new RectangleF(x, y + keyH * 0.18f, w, keyH * 0.62f);
                        using (var bb = new SolidBrush(Accent)) using (var bp = Round(b, b.Height / 2)) g.FillPath(bb, bp);
                        Centered(g, it.Text, capFont, Brushes.White, b, fmt);
                        break;
                    }
                case Kind.Note:
                    Centered(g, it.Text, noteFont, new SolidBrush(Color.FromArgb(205, 210, 220)), new RectangleF(x, y, w, keyH), fmt);
                    break;
                default:
                    Centered(g, it.Text, capFont, new SolidBrush(Color.FromArgb(185, 190, 200)), new RectangleF(x, y, w, keyH * 0.92f), fmt);
                    break;
            }
            x += w + gap;
        }
        g.Restore(state);
    }

    static void Centered(Graphics g, string text, Font font, Brush brush, RectangleF r, StringFormat fmt)
    {
        var sz = g.MeasureString(text, font, PointF.Empty, fmt);
        g.DrawString(text, font, brush, r.Left + (r.Width - sz.Width) / 2, r.Top + (r.Height - sz.Height) / 2, fmt);
    }

    static GraphicsPath Round(RectangleF r, float rad)
    {
        rad = Math.Max(0.5f, Math.Min(rad, Math.Min(r.Width, r.Height) / 2));
        var p = new GraphicsPath();
        p.AddArc(r.Left, r.Top, 2 * rad, 2 * rad, 180, 90);
        p.AddArc(r.Right - 2 * rad, r.Top, 2 * rad, 2 * rad, 270, 90);
        p.AddArc(r.Right - 2 * rad, r.Bottom - 2 * rad, 2 * rad, 2 * rad, 0, 90);
        p.AddArc(r.Left, r.Bottom - 2 * rad, 2 * rad, 2 * rad, 90, 90);
        p.CloseFigure();
        return p;
    }
}
