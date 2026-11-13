using System.Windows;
using System.Windows.Controls;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Dokumentbereich „Status und Fertigstellung“ (13, Paket 30): Prüfliste, Entwurf oder Freigabe, Wasserzeichen.
/// Freigeben geht nur, wenn alle Pflichtpunkte erledigt sind; jede inhaltliche Änderung danach macht wieder einen Entwurf daraus.
/// </summary>
sealed partial class ViewerWindow
{
    void StatusSection(DocSettings d)
    {
        bool ro = _view.Session.ReadOnly;
        var box = Section(Loc.L("Status und Fertigstellung", "Status and completion"));
        string state = GuideStatus.State(_view);
        string label = state switch
        {
            "released" => Loc.L("Freigegeben", "Released"),
            "changed" => Loc.L("Entwurf (nach der Freigabe geändert)", "Draft (changed after release)"),
            "draft" => Loc.L("Entwurf", "Draft"),
            _ => Loc.L("Ohne Status", "No status"),
        };
        var chip = U.Chip(label, state == "released" ? Theme.AccentSoft : Theme.WarnSoft, state == "released" ? Theme.AccentText : Theme.Warn, state == "released" ? Theme.ICheck : Theme.IWarn);
        chip.HorizontalAlignment = HorizontalAlignment.Left; chip.Margin = new Thickness(0, 0, 0, 10);
        box.Children.Add(chip);

        var items = GuideStatus.Readiness(_view);
        var list = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        foreach (var it in items)
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var icon = U.Icon(it.Done ? Theme.ICheck : it.Required ? Theme.IWarn : Theme.IInfo, 14, it.Done ? Theme.AccentText : it.Required ? Theme.Warn : Theme.Muted);
            icon.Margin = new Thickness(0, 0, 8, 0);
            DockPanel.SetDock(icon, Dock.Left); row.Children.Add(icon);
            string text = Loc.L(it.De, it.En) + (it.Count > 0 && !it.Done ? $" ({it.Count})" : "") + (it.Required ? "" : Loc.L(" – empfohlen", " – recommended"));
            row.Children.Add(U.Text(text, Theme.FsHint, false, it.Done ? Theme.Muted : Theme.Fg));
            list.Children.Add(row);
        }
        box.Children.Add(list);

        bool can = GuideStatus.CanRelease(items);
        var acts = new WrapPanel { Margin = new Thickness(-3, 0, 0, 6) };
        var release = U.Primary(Loc.L("Freigeben", "Release"), () =>
        {
            var fresh = GuideStatus.Readiness(_view);
            if (!GuideStatus.CanRelease(fresh)) { Msg.Show(Loc.L("Noch nicht alle Pflichtpunkte sind erledigt.", "Not all required items are done yet.")); return; }
            Change(e => { GuideStatus.Release(_view, fresh); }, refill: false);
            _status.Text = Loc.L("Freigegeben – das Wasserzeichen „Entwurf“ entfällt. Jede Änderung macht wieder einen Entwurf daraus.", "Released – the “Draft” watermark is gone. Any change turns it into a draft again.");
            ShowCover();
        });
        release.IsEnabled = can && !ro && state != "released";
        release.ToolTip = can ? Loc.L("Anleitung als freigegeben kennzeichnen", "Mark the guide as released") : Loc.L("Erst alle Pflichtpunkte erledigen", "First complete all required items");
        acts.Children.Add(release);
        if (state is "none" or "released" or "changed")
            acts.Children.Add(U.Btn(Theme.IEdit, Loc.L("Als Entwurf kennzeichnen", "Mark as draft"), () => { Change(e => GuideStatus.SetDraft(e), refill: false); ShowCover(); }, Loc.L("Mit Wasserzeichen „Entwurf“ in PDF, Word und HTML", "With a “Draft” watermark in PDF, Word and HTML")));
        if (state != "none")
            acts.Children.Add(U.Btn(Theme.IDelete, Loc.L("Status entfernen", "Remove status"), () => { Change(e => (e["guide"] as System.Text.Json.Nodes.JsonObject)?.Remove("status"), refill: false); ShowCover(); }));
        foreach (var b in acts.Children.OfType<Button>()) if (ro) b.IsEnabled = false;
        box.Children.Add(acts);

        var wm = new CheckBox { Content = Loc.L("Wasserzeichen „Entwurf“ bei Entwürfen", "“Draft” watermark for drafts"), IsChecked = d.Watermark != "off", IsEnabled = !ro };
        wm.Checked += (_, _) => DocChange(x => x.Watermark = "auto"); wm.Unchecked += (_, _) => DocChange(x => x.Watermark = "off");
        box.Children.Add(wm);
    }
}
