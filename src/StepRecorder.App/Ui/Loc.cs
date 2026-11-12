using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Sprache der Oberfläche. Die Oberfläche ist auf Deutsch geschrieben; bei „English“ übersetzt dieser Dienst
/// beim Laden jedes Elements Texte, Inhalte, Tooltips, Überschriften, Platzhalter, Fenstertitel und Listeneinträge
/// (Tabelle <see cref="UiTextEn"/>) und verfolgt spätere Änderungen von Texten (Statuszeile, Meldungen).
/// </summary>
static class Loc
{
    public static bool En { get; private set; }
    static readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);
    static readonly HashSet<string> _outputs = new(StringComparer.Ordinal);

    public static void Init()
    {
        var env = Environment.GetEnvironmentVariable("STEPRECORDER_UILANG");
        try { En = (env ?? AppSettings.Load().UiLanguage) == "en"; } catch { En = false; }
        if (!En) return;
        // „Loaded“ erreicht nur Elemente mit eigenem Handler; „SizeChanged“ bekommt jedes Element beim ersten Layout
        var h = new SizeChangedEventHandler((s, e) => { if (e.PreviousSize.Width == 0 && e.PreviousSize.Height == 0) Apply(s as FrameworkElement); });
        foreach (var t in new[] { typeof(TextBlock), typeof(ContentControl), typeof(HeaderedItemsControl), typeof(TextBox), typeof(ComboBox) })
            EventManager.RegisterClassHandler(t, FrameworkElement.SizeChangedEvent, h, true);
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) => Apply(s as FrameworkElement)), true);
        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.ToolTipOpeningEvent, new ToolTipEventHandler((s, _) => { if (s is FrameworkElement fe && fe.ToolTip is string tip) Set(tip, v => fe.ToolTip = v); }), true);
    }

    /// <summary>Text übersetzen (bei deutscher Oberfläche unverändert).</summary>
    public static string T(string? s)
    {
        if (!En || string.IsNullOrEmpty(s)) return s ?? "";
        lock (_cache)
        {
            if (_cache.TryGetValue(s, out var c)) return c;
            if (_outputs.Contains(s)) return s;   // schon übersetzt: nie ein zweites Mal (verhindert Endlosschleifen)
            string r;
            if (UiTextEn.Exact.TryGetValue(s, out var ex)) r = ex;
            else
            {
                r = s;
                foreach (var (rx, rep) in UiTextEn.Patterns)
                    try { r = rx.Replace(r, rep); } catch { }
            }
            if (_cache.Count > 5000) { _cache.Clear(); _outputs.Clear(); }
            _cache[s] = r;
            if (r != s) _outputs.Add(r);
            return r;
        }
    }

    static void Set(string? cur, Action<string> set)
    {
        if (string.IsNullOrEmpty(cur)) return;
        var t = T(cur);
        if (!ReferenceEquals(t, cur) && t != cur) set(t);
    }

    static readonly DependencyPropertyDescriptor TextDesc = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
    static readonly DependencyPropertyDescriptor TbDesc = DependencyPropertyDescriptor.FromProperty(TextBox.TextProperty, typeof(TextBox));
    static readonly DependencyProperty Tracked = DependencyProperty.RegisterAttached("LocTracked", typeof(bool), typeof(Loc));

    static void Apply(FrameworkElement? e)
    {
        if (e == null) return;
        try
        {
            if (e.ToolTip is string tip) Set(tip, v => e.ToolTip = v);
            switch (e)
            {
                case Window w:
                    Set(w.Title, v => w.Title = v);
                    break;
                case TextBlock tb:
                    if (tb.Inlines.Count > 1) { foreach (var run in tb.Inlines.OfType<Run>().ToList()) Set(run.Text, v => run.Text = v); }
                    else Set(tb.Text, v => tb.Text = v);
                    Track(tb, TextDesc, () => { if (tb.Inlines.Count <= 1) Set(tb.Text, v => tb.Text = v); });
                    break;
                case TextBox box:
                    if (box.Tag is string ph) Set(ph, v => box.Tag = v);
                    if (box.IsReadOnly) { Set(box.Text, v => box.Text = v); Track(box, TbDesc, () => Set(box.Text, v => box.Text = v)); }
                    break;
                // Listeneinträge und Auswahlfelder nie verändern (würde Auswahl-Ereignisse auslösen) – angezeigt werden sie
                // über TextBlöcke, und die werden übersetzt.
                case ComboBox or ListBoxItem or ComboBoxItem:
                    break;
                case HeaderedItemsControl hi:
                    if (hi.Header is string h) Set(h, v => hi.Header = v);
                    if (hi is MenuItem mi && mi.InputGestureText is { Length: > 0 } g) Set(g, v => mi.InputGestureText = v);
                    break;
                case HeaderedContentControl hc:
                    if (hc.Header is string hh) Set(hh, v => hc.Header = v);
                    if (hc.Content is string c1) Set(c1, v => hc.Content = v);
                    break;
                case ContentControl cc:
                    if (cc.Content is string c2) Set(c2, v => cc.Content = v);
                    break;
            }
        }
        catch { /* Übersetzung darf die Oberfläche nie stören */ }
    }

    /// <summary>Spätere Textänderungen mitübersetzen; beim Entladen wieder abmelden (kein Speicherleck).</summary>
    static void Track(FrameworkElement e, DependencyPropertyDescriptor d, Action onChange)
    {
        if ((bool)e.GetValue(Tracked)) return;
        e.SetValue(Tracked, true);
        EventHandler h = (_, _) => onChange();
        d.AddValueChanged(e, h);
        RoutedEventHandler? un = null;
        un = (_, _) => { d.RemoveValueChanged(e, h); e.Unloaded -= un; e.SetValue(Tracked, false); };
        e.Unloaded += un;
    }
}
