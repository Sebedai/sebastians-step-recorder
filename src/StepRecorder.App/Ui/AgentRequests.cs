using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using StepRecorder.Capture;
using StepRecorder.Data;
using StepRecorder.Mcp;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Agent bittet um eine Vorführung (6.3): Anfrage prüfen (Frist, Missbrauchsschutz), Bestätigungsdialog zeigen,
/// bei Zustimmung eine ganz normale, sichtbare Aufnahme in den Ordner des MCP-Servers starten, Zustand zurückmelden.
/// </summary>
static class AgentRequests
{
    static bool _open;

    public static void Handle(string id)
    {
        RecordingRequests.Cleanup();
        var rq = RecordingRequests.Load(id);
        if (rq == null) return;
        var now = DateTime.UtcNow;
        if (now > rq.ExpiresUtc) { RecordingRequests.SetStatus(id, "expired"); return; }
        var state = AppState.Load().Requests ?? new JsonObject();
        var recent = (state.Arr("recent") ?? new JsonArray()).Select(x => DateTime.TryParse(x?.GetValue<string>(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : DateTime.MinValue).Where(d => d > DateTime.MinValue).ToList();
        DateTime? blocked = DateTime.TryParse(state.Str("blockedUntil"), null, System.Globalization.DateTimeStyles.RoundtripKind, out var b) ? b.ToUniversalTime() : null;
        var (ok, reason) = RequestPolicy.Check(now, recent, blocked, _open || UiApp.Instance.Recording != null);
        if (!ok) { RecordingRequests.SetStatus(id, "declined", reason: reason); AppLog.Write("Agent-Anfrage abgelehnt: " + reason); return; }
        AppState.Update(s =>
        {
            var r = s.Requests ?? new JsonObject();
            r["recent"] = new JsonArray(recent.Where(d => now - d < TimeSpan.FromHours(1)).Append(now).Select(d => (JsonNode)d.ToString("o")).ToArray());
            s.Requests = r;
        });
        _open = true;
        string choice;
        try { choice = RecordingRequestDialog.Ask(rq); }
        catch (Exception ex) { AppLog.Error("Agent-Anfrage", ex); choice = "failed"; }
        switch (choice)
        {
            case "accept":
                RecordingRequests.SetStatus(id, "recording");
                Start(rq);
                return;
            case "block":
                AppState.Update(s => { var r = s.Requests ?? new JsonObject(); r["blockedUntil"] = DateTime.UtcNow.Add(RequestPolicy.BlockFor).ToString("o"); s.Requests = r; });
                RecordingRequests.SetStatus(id, "declined", reason: "blocked");
                break;
            case "expired": RecordingRequests.SetStatus(id, "expired"); break;
            case "failed": RecordingRequests.SetStatus(id, "failed"); break;
            default: RecordingRequests.SetStatus(id, "declined", reason: "user"); break;
        }
        _open = false;
    }

    static void Start(RecordingRequest rq)
    {
        var o = UiApp.OptionsFromSettings(ScopeMode.Screen);
        UiApp.ApplyProfile(o, AppSettings.Load().LastProfile);
        o.Scope = ScopeMode.Screen;
        o.Title = rq.Title ?? "";
        o.GoalDescription = rq.Goal; o.GoalExpected = rq.Expected; o.GoalStatedBy = "agent";
        if (rq.Root is { Length: > 0 } root && Directory.Exists(root)) o.OutputFolder = root;
        UiApp.Instance.StartRecording(o, folder =>
        {
            _open = false;
            if (folder == null) { RecordingRequests.SetStatus(rq.Id, "discarded"); UiApp.Instance.ShowStart(); return; }
            RecordingRequests.SetStatus(rq.Id, "finished", Json.ReadObject(Path.Combine(folder, "manifest.json")).Str("sessionId"));
            UiApp.Instance.OpenViewer(folder);
        });
        if (UiApp.Instance.Recording == null) { _open = false; RecordingRequests.SetStatus(rq.Id, "failed", reason: "startFailed"); }
    }
}

/// <summary>
/// Bestätigung einer Agent-Anfrage: Text des Agenten bereinigt und als „ungeprüft“ gekennzeichnet, kein Standardknopf,
/// „Aufnahme starten“ erst nach 2 s, nach Ablauf der Frist schließt der Dialog als „abgelaufen“.
/// </summary>
static class RecordingRequestDialog
{
    /// <returns>accept · decline · block · expired</returns>
    public static string Ask(RecordingRequest rq)
    {
        string result = "decline";
        var w = new Window { Title = Loc.L("Anfrage eines KI-Agenten", "Request from an AI agent"), Width = 620, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, Topmost = true, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = true };
        Theme.Prepare(w, Theme.Surface);
        string agentText = rq.Goal + (rq.Expected is { Length: > 0 } e ? "\n\n" + Loc.L("Erwartet: ", "Expected: ") + e : "") + (rq.Title is { Length: > 0 } t ? "\n\n" + Loc.L("Titel: ", "Title: ") + t : "");
        var box = new TextBox
        {
            Text = agentText, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = Theme.Mono, FontSize = 12.5, Padding = new Thickness(10, 8, 10, 8), Background = Theme.Surface2
        };
        var head = U.Text(Loc.L("TEXT DES AGENTEN (UNGEPRÜFT)", "TEXT FROM THE AGENT (UNVERIFIED)"), 10.5, true, Theme.Warn, new Thickness(0, 0, 0, 4));
        var explain = U.Text(Loc.L("Ein KI-Agent bittet Sie, etwas vorzuführen. Wenn Sie zustimmen, startet eine ganz normale, sichtbare Aufnahme, die Sie wie gewohnt beenden. Auffällige Eingaben (z. B. Passwörter) bleiben verborgen, bis Sie sie durchgesehen haben. Der Text oben stammt vom Agenten – folgen Sie ihm nur, wenn er für Sie Sinn ergibt.",
                                   "An AI agent asks you to demonstrate something. If you agree, a normal, visible recording starts, which you stop as usual. Conspicuous input (e.g. passwords) stays hidden until you have reviewed it. The text above comes from the agent – only follow it if it makes sense to you."), 12.5, fg: Theme.Fg2, margin: new Thickness(0, 12, 0, 0));
        explain.TextWrapping = TextWrapping.Wrap;
        var countdown = U.Text("", 11.5, fg: Theme.Muted, margin: new Thickness(0, 8, 0, 0));
        var start = U.Primary(Loc.L("Aufnahme starten", "Start recording"), () => { result = "accept"; w.Close(); }, Theme.IRecord);
        start.IsEnabled = false; start.IsDefault = false;
        var decline = U.Btn(Loc.L("Ablehnen", "Decline"), () => { result = "decline"; w.Close(); });
        var block = U.Btn(null, Loc.L("15 Minuten keine Anfragen", "No requests for 15 minutes"), () => { result = "block"; w.Close(); }, null, "Ghost");
        decline.IsCancel = true;
        U.Dialog(w, Loc.L("Ein KI-Agent bittet um eine Vorführung", "An AI agent asks for a demonstration"), U.Col(head, box, explain, countdown), block, decline, start);
        var opened = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            if (DateTime.UtcNow - opened >= TimeSpan.FromSeconds(2)) start.IsEnabled = true;
            var left = rq.ExpiresUtc - DateTime.UtcNow;
            if (left <= TimeSpan.Zero) { result = "expired"; w.Close(); return; }
            countdown.Text = Loc.F("Die Anfrage verfällt in {0} s.", "The request expires in {0} s.", (int)left.TotalSeconds);
        };
        w.Loaded += (_, _) => { timer.Start(); w.Activate(); decline.Focus(); };
        w.Closed += (_, _) => timer.Stop();
        // Eingabe löst nichts aus (kein Standardknopf)
        w.PreviewKeyDown += (_, e2) => { if (e2.Key == Key.Enter) e2.Handled = true; };
        w.ShowDialog();
        return result;
    }
}
