using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace StepRecorder.App.Ui;

/// <summary>
/// Bedienungshilfen: Bildschirmleser (Sprachausgabe, NVDA, JAWS) sollen jede Schaltfläche benennen können.
/// Schaltflächen mit Symbol und Text oder nur mit Symbol bekommen ihren Namen aus dem sichtbaren Text oder dem Tooltip,
/// Eingabefelder aus dem Platzhalter. Ein von Hand gesetzter Name bleibt unangetastet.
/// </summary>
static class Accessibility
{
    public static void Install()
    {
        var h = new RoutedEventHandler((s, _) => Name((FrameworkElement)s));
        foreach (var t in new[] { typeof(ButtonBase), typeof(TextBox), typeof(ComboBox), typeof(Slider), typeof(ListBoxItem) })
            EventManager.RegisterClassHandler(t, FrameworkElement.LoadedEvent, h);
    }

    static void Name(FrameworkElement e)
    {
        try
        {
            string? tip = e.ToolTip as string ?? (e.ToolTip as ToolTip)?.Content as string;
            if (e is ButtonBase b)
            {
                if (b.Content is string) { if (tip != null) Help(e, tip); return; }
                var text = VisibleText(b.Content as DependencyObject);
                Set(e, text ?? tip);
                if (text != null && tip != null && tip != text) Help(e, tip);
            }
            else if (e is TextBox or ComboBox or Slider)
            {
                Set(e, e.Tag as string ?? tip ?? LabelBefore(e));
                if (tip != null) Help(e, tip);
            }
            else if (e is ListBoxItem li && li.Content is not string)
                Set(e, li.Tag is StepRecorder.ReadLayer.ViewStep vs ? (vs.Manual ? "Eingefügter Schritt: " : $"Schritt {vs.Number}: ") + vs.Title + (vs.Hidden ? " (ausgeblendet)" : "") : VisibleText(li.Content as DependencyObject));
        }
        catch { /* Bedienungshilfen dürfen nie die Oberfläche stören */ }
    }

    static void Set(FrameworkElement e, string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || !string.IsNullOrEmpty(AutomationProperties.GetName(e))) return;
        AutomationProperties.SetName(e, Clean(Loc.T(name)));
    }

    static void Help(FrameworkElement e, string text)
    {
        if (string.IsNullOrEmpty(AutomationProperties.GetHelpText(e))) AutomationProperties.SetHelpText(e, Clean(Loc.T(text)));
    }

    static string Clean(string s) => s.Replace("\n", " ").Replace("  ", " ").Trim();

    /// <summary>Sichtbarer Text eines Inhalts – ohne Symbolschrift-Zeichen.</summary>
    internal static string? VisibleText(DependencyObject? root)
    {
        if (root == null) return null;
        var parts = new List<string>();
        void Walk(object o)
        {
            switch (o)
            {
                case TextBlock t when t.FontFamily != Theme.Icons && !string.IsNullOrWhiteSpace(t.Text) && t.Visibility == Visibility.Visible: parts.Add(t.Text.Trim()); return;
                case Panel p: foreach (UIElement c in p.Children) Walk(c); return;
                case Decorator d when d.Child != null: Walk(d.Child); return;
                case ContentControl cc when cc.Content != null: Walk(cc.Content); return;
                case string s when !string.IsNullOrWhiteSpace(s): parts.Add(s.Trim()); return;
            }
        }
        Walk(root);
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    /// <summary>Beschriftung direkt vor dem Feld (U.Field legt Überschrift und Feld untereinander in ein Panel).</summary>
    static string? LabelBefore(FrameworkElement e)
    {
        if (VisualTreeHelper.GetParent(e) is not Panel p) return null;
        int i = p.Children.IndexOf(e);
        return i > 0 && p.Children[i - 1] is TextBlock t && t.FontFamily != Theme.Icons ? t.Text : null;
    }
}
