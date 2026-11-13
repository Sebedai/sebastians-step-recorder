using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

using StepRecorder.Settings;

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
        // „Loaded“ meldet WPF nur Elementen mit eigenem Loaded-Handler (Optimierung) – Schaltflächen ohne Handler blieben
        // sonst namenlos. Die erste Größenänderung bekommt dagegen jedes sichtbare Element.
        var h = new RoutedEventHandler((s, _) => Name((FrameworkElement)s));
        var hs = new SizeChangedEventHandler((s, _) => Name((FrameworkElement)s));
        foreach (var t in new[] { typeof(ButtonBase), typeof(TextBox), typeof(ComboBox), typeof(Slider), typeof(ListBoxItem) })
        {
            EventManager.RegisterClassHandler(t, FrameworkElement.LoadedEvent, h);
            EventManager.RegisterClassHandler(t, FrameworkElement.SizeChangedEvent, hs);
        }
    }

    static void Name(FrameworkElement e)
    {
        try
        {
            string? tip = e.ToolTip as string ?? (e.ToolTip as ToolTip)?.Content as string;
            // Fensterknöpfe der WPF-UI-Titelleiste haben nur eine Kennung, keinen Namen
            string id = AutomationProperties.GetAutomationId(e) is { Length: > 0 } aid ? aid : e.Name;
            string? sys = id switch
            {
                "TitleBarMinimizeButton" => Loc.L("Minimieren", "Minimize"), "TitleBarMaximizeButton" => Loc.L("Maximieren", "Maximize"),
                "TitleBarCloseButton" => Loc.L("Schließen", "Close"), "TitleBarHelpButton" => Loc.L("Hilfe", "Help"),
                // Teile der Bildlaufleisten
                "PageUp" => Loc.L("Seite nach oben", "Page up"), "PageDown" => Loc.L("Seite nach unten", "Page down"),
                "LineUp" => Loc.L("Zeile nach oben", "Line up"), "LineDown" => Loc.L("Zeile nach unten", "Line down"),
                "PageLeft" => Loc.L("Seite nach links", "Page left"), "PageRight" => Loc.L("Seite nach rechts", "Page right"),
                "LineLeft" => Loc.L("Spalte nach links", "Column left"), "LineRight" => Loc.L("Spalte nach rechts", "Column right"),
                _ => null
            };
            // Bildlaufleisten ohne Kennung (WPF-UI-Vorlage): am Befehl erkennen
            if (sys == null && e is RepeatButton { Command: RoutedCommand cmd })
                sys = cmd == ScrollBar.PageUpCommand ? Loc.L("Seite nach oben", "Page up") : cmd == ScrollBar.PageDownCommand ? Loc.L("Seite nach unten", "Page down")
                    : cmd == ScrollBar.LineUpCommand ? Loc.L("Zeile nach oben", "Line up") : cmd == ScrollBar.LineDownCommand ? Loc.L("Zeile nach unten", "Line down")
                    : cmd == ScrollBar.PageLeftCommand ? Loc.L("Seite nach links", "Page left") : cmd == ScrollBar.PageRightCommand ? Loc.L("Seite nach rechts", "Page right")
                    : cmd == ScrollBar.LineLeftCommand ? Loc.L("Spalte nach links", "Column left") : cmd == ScrollBar.LineRightCommand ? Loc.L("Spalte nach rechts", "Column right") : null;
            if (sys != null) { Set(e, sys); return; }
            if (e is ButtonBase b)
            {
                if (b.Content is string) { if (tip != null) Help(e, tip); return; }
                var text = VisibleText(b.Content as DependencyObject);
                // nur eine Zahl sichtbar (Zähler, Nummer) oder Beschriftung ausgeblendet: der Tooltip sagt mehr
                if (tip != null && (text == null || text.All(c => char.IsDigit(c) || char.IsWhiteSpace(c)))) text = tip;
                Set(e, text ?? tip);
                if (text != null && tip != null && tip != text) Help(e, tip);
            }
            else if (e is TextBox or ComboBox or Slider)
            {
                Set(e, e.Tag as string ?? (e is Wpf.Ui.Controls.TextBox { PlaceholderText.Length: > 0 } ub ? ub.PlaceholderText : null) ?? tip ?? LabelBefore(e));
                if (tip != null) Help(e, tip);
            }
            else if (e is ListBoxItem li && li.Content is not string)
                Set(e, li.Tag is StepRecorder.ReadLayer.ViewStep vs ? (vs.Manual ? Loc.L("Eingefügter Schritt: ", "Inserted step: ") : Loc.F("Schritt {0}: ", "Step {0}: ", vs.Number)) + vs.Title + (vs.Hidden ? Loc.L(" (ausgeblendet)", " (hidden)") : "") : VisibleText(li.Content as DependencyObject));
        }
        catch { /* Bedienungshilfen dürfen nie die Oberfläche stören */ }
    }

    static void Set(FrameworkElement e, string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || !string.IsNullOrEmpty(AutomationProperties.GetName(e))) return;
        AutomationProperties.SetName(e, Clean(name));
    }

    static void Help(FrameworkElement e, string text)
    {
        if (string.IsNullOrEmpty(AutomationProperties.GetHelpText(e))) AutomationProperties.SetHelpText(e, Clean(text));
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

    /// <summary>
    /// Beschriftung vor dem Feld: U.Field legt Überschrift und Feld untereinander in ein Panel – das Feld kann dort auch
    /// in einer Gruppe stecken (mit Hinweis darunter oder „Durchsuchen …“ daneben), deshalb einige Ebenen nach oben.
    /// </summary>
    static string? LabelBefore(FrameworkElement e)
    {
        DependencyObject cur = e;
        for (int depth = 0; depth < 4; depth++)
        {
            if (VisualTreeHelper.GetParent(cur) is not DependencyObject parent) return null;
            if (parent is Panel p && cur is UIElement ue)
            {
                int i = p.Children.IndexOf(ue);
                if (i > 0 && p.Children[i - 1] is TextBlock t && t.FontFamily != Theme.Icons && !string.IsNullOrWhiteSpace(t.Text)) return t.Text;
            }
            cur = parent;
        }
        return null;
    }
}
