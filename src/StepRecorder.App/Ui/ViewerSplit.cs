using System.Windows;
using System.Windows.Controls;
using StepRecorder.Data;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>Schritt teilen und Ergebnis als eigenen Schritt (13, Paket 34).</summary>
sealed partial class ViewerWindow
{
    void SplitStep()
    {
        if (Sel is not { } s || _view.Session.ReadOnly || s.TextSection) return;
        InlineCommit(now: true);
        string title = s.AuthorTitle ?? s.Title, body = s.AuthorBody ?? s.Guide;
        // Vorschlag: Mitte an einer Satzgrenze
        int cut = body.Length / 2;
        int dot = body.IndexOfAny(new[] { '.', '!', '?', '\n' }, Math.Max(0, cut - 40));
        if (dot > 0 && dot < body.Length - 1) cut = dot + 1;
        var (b1, b2) = StepSplit.SplitText(body, cut);
        var d = new SplitDialog(title, b1, title, b2) { Owner = this };
        if (d.ShowDialog() != true) return;
        long anchor = 0;
        Change(e => anchor = StepSplit.Split(e, s, _view.Language, d.Title1, d.Body1, d.Title2, d.Body2));
        SelectAnchor(anchor);
        _status.Text = Loc.L("Schritt geteilt – der neue Schritt zeigt das Bild nach der Aktion. Die Aufnahme selbst bleibt unverändert.", "Step split – the new step shows the image after the action. The recording itself stays unchanged.");
    }

    void ResultStep()
    {
        if (Sel is not { } s || _view.Session.ReadOnly) return;
        if (s.PostFrame == null && s.PreFrame == null) { _status.Text = Loc.L("Dieser Schritt hat kein Bild.", "This step has no image."); return; }
        long anchor = 0;
        Change(e => anchor = StepSplit.Result(e, s, Loc.For(_view.Language, "Ergebnis", "Result"), ""));
        SelectAnchor(anchor);
        _status.Text = Loc.L("Ergebnisschritt eingefügt – mit dem Bild nach der Aktion.", "Result step inserted – with the image after the action.");
    }
}

/// <summary>Zwei Hälften eines Schritts bearbeiten.</summary>
sealed class SplitDialog : DialogWindow
{
    readonly TextBox _t1 = new(), _b1 = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70 }, _t2 = new(), _b2 = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70 };
    public string Title1 => _t1.Text.Trim();
    public string Body1 => _b1.Text.Trim();
    public string Title2 => _t2.Text.Trim();
    public string Body2 => _b2.Text.Trim();

    public SplitDialog(string t1, string b1, string t2, string b2)
    {
        Title = AppInfo.DisplayName; Width = 620; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Surface);
        _t1.Text = t1; _b1.Text = b1; _t2.Text = t2; _b2.Text = b2;
        var p = new StackPanel { Children =
        {
            U.Hint(Loc.L("Der erste Teil bleibt am Schritt (Bild vor der Aktion), der zweite wird ein neuer Schritt mit dem Bild danach.", "The first part stays with the step (image before the action), the second becomes a new step with the image after it."), new Thickness(0, 0, 0, 10)),
            U.Field(Loc.L("Teil 1 – Überschrift", "Part 1 – heading"), _t1), U.Field(Loc.L("Teil 1 – Text", "Part 1 – text"), _b1),
            U.Field(Loc.L("Teil 2 – Überschrift", "Part 2 – heading"), _t2), U.Field(Loc.L("Teil 2 – Text", "Part 2 – text"), _b2),
        } };
        U.Dialog(this, Loc.L("Schritt teilen", "Split step"), p, U.Primary(Loc.L("Teilen", "Split"), () => DialogResult = true), U.Btn(Loc.L("Abbrechen", "Cancel"), () => DialogResult = false));
    }
}
