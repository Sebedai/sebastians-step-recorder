using System.Windows;
using System.Windows.Controls;
using StepRecorder.Mcp;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// „Mit KI verbinden …“ (VIE-116): fertige Einrichtung des MCP-Servers zum Kopieren und was ein Agent sehen kann.
/// Zeigt nur Text – schreibt keine fremden Dateien, startet nichts.
/// </summary>
static class AiConnect
{
    public static void Show(Window? owner)
    {
        var w = new DialogWindow { Title = Loc.L("Mit KI verbinden", "Connect to AI"), Width = 760, SizeToContent = SizeToContent.Height, MaxHeight = Theme.ActiveWorkArea().Height - 40, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = owner == null, Owner = owner, WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen };
        Theme.Prepare(w, Theme.Surface);
        string exe = Environment.ProcessPath ?? StepRecorder.Data.AppInfo.ExeName;
        var root = new TextBox { Text = AppPaths.DefaultSessionsFolder() };
        var allowEdit = new CheckBox { Content = Loc.L("Texte bearbeiten erlauben (--allow-edit) – der Agent darf Überschriften und Anleitungstexte ändern", "Allow editing texts (--allow-edit) – the agent may change headings and guide texts"), Margin = new Thickness(0, 6, 0, 0) };
        var allowReq = new CheckBox { Content = Loc.L("Agent darf um eine Vorführung bitten (--allow-requests) – jede Anfrage bestätigen Sie selbst", "Agent may ask for a demonstration (--allow-requests) – you confirm every request"), Margin = new Thickness(0, 4, 0, 0) };
        var lang = new ComboBox { Items = { Loc.L("Sprache der Aufnahme", "Recording language"), "Deutsch", "English" }, SelectedIndex = 0, MinWidth = 180 };
        var project = Block(); var cmd = Block(); var cli = Block();
        var warn = U.Text("", 12, fg: Theme.Warn, margin: new Thickness(0, 6, 0, 0));
        void Upd()
        {
            var o = new McpConfigOptions(exe, root.Text.Trim(), allowEdit.IsChecked == true, allowReq.IsChecked == true, lang.SelectedIndex switch { 1 => "de", 2 => "en", _ => null });
            project.Text = McpConfig.ProjectJson(o);
            cmd.Text = McpConfig.ClaudeAddCommand(o);
            cli.Text = McpConfig.CommandLine(o);
            var ws = McpConfig.Warnings(o);
            if (!Directory.Exists(o.Root)) ws.Insert(0, Loc.L("Der Ordner existiert nicht.", "The folder does not exist."));
            warn.Text = string.Join("\n", ws); warn.Visibility = ws.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        root.TextChanged += (_, _) => Upd(); allowEdit.Checked += (_, _) => Upd(); allowEdit.Unchecked += (_, _) => Upd();
        allowReq.Checked += (_, _) => Upd(); allowReq.Unchecked += (_, _) => Upd(); lang.SelectionChanged += (_, _) => Upd();
        var browse = U.Btn(Theme.IFolder, null, () => { var d = new Microsoft.Win32.OpenFolderDialog { InitialDirectory = Directory.Exists(root.Text) ? root.Text : null }; if (d.ShowDialog(w) == true) root.Text = d.FolderName; }, Loc.L("Ordner wählen …", "Choose folder …"));
        browse.Margin = new Thickness(8, 0, 0, 0);
        var rootRow = new DockPanel(); DockPanel.SetDock(browse, Dock.Right); rootRow.Children.Add(browse); rootRow.Children.Add(root);
        Upd();
        var sees = U.Text(Loc.L(
            "Ein Agent sieht: alle Aufnahmen in diesem Ordner – Schritte, getippte Texte, Fenstertitel, Bildschirmfotos.\nEr sieht nicht: andere Ordner, Eingaben in erkannten Passwortfeldern, Geschwärztes, auffällige Eingaben vor Ihrer Durchsicht.\nWas der Agent liest, kann er an seinen KI-Dienst weitergeben. Der Server selbst nutzt kein Netzwerk.",
            "An agent sees: all recordings in this folder – steps, typed text, window titles, screenshots.\nIt does not see: other folders, input in recognised password fields, redactions, flagged input before your review.\nWhat the agent reads it may pass on to its AI service. The server itself uses no network."), 12.5, fg: Theme.Fg2, margin: new Thickness(0, 4, 0, 12));
        var body = U.Col(sees,
            U.Field(Loc.L("Aufnahmeordner für den Agenten", "Recordings folder for the agent"), U.Col(rootRow, allowEdit, allowReq, warn)),
            U.Field(Loc.L("Sprache der Antworten", "Answer language"), lang),
            Section(Loc.L("Claude Code und andere: Datei .mcp.json im Projekt", "Claude Code and others: file .mcp.json in the project"), project, Loc.L("Bei Claude Desktop denselben Eintrag unter „mcpServers“ in claude_desktop_config.json ergänzen.", "For Claude Desktop add the same entry under \"mcpServers\" in claude_desktop_config.json.")),
            Section(Loc.L("Oder in der Eingabeaufforderung (Claude Code)", "Or on the command line (Claude Code)"), cmd, null),
            Section(Loc.L("Aufruf des Servers", "Server command"), cli, null));
        U.Dialog(w, Loc.L("Mit KI verbinden", "Connect to AI"), new ScrollViewer { Content = body, MaxHeight = 640, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, U.Primary(Loc.L("Schließen", "Close"), () => w.Close()));
        w.Show();
    }

    static TextBox Block() => new() { IsReadOnly = true, FontFamily = Theme.Mono, FontSize = 12, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(10, 8, 10, 8), Background = Theme.Surface2, BorderThickness = new Thickness(1) };

    static FrameworkElement Section(string title, TextBox box, string? hint)
    {
        var copy = U.Btn(Theme.ICopy, Loc.L("Kopieren", "Copy"), () => Shell.CopyPath(box.Text), null, "Ghost");
        copy.Margin = new Thickness(0);
        var head = new DockPanel { Margin = new Thickness(0, 10, 0, 6) };
        DockPanel.SetDock(copy, Dock.Right); head.Children.Add(copy); head.Children.Add(U.Text(title, 12.5, true, Theme.Fg2));
        var col = U.Col(head, box);
        if (hint != null) col.Children.Add(U.Text(hint, 11.5, fg: Theme.Muted, margin: new Thickness(0, 4, 0, 0)));
        return col;
    }
}
