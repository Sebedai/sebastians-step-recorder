using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace StepRecorder.App.Ui;

/// <summary>
/// Grundlage aller Dialoge: dasselbe Windows-11-Fenster wie Start und Editor (WPF-UI, Mica, Titelleiste von WPF-UI).
/// Dialoge setzen <see cref="ContentControl.Content"/> wie gewohnt – der Inhalt landet automatisch unter der Titelleiste.
/// Minimieren gibt es in Dialogen nicht, Maximieren nur bei veränderbarer Größe.
/// </summary>
class DialogWindow : Wpf.Ui.Controls.FluentWindow
{
    readonly Wpf.Ui.Controls.TitleBar _bar = new() { ShowMinimize = false, ShowHelp = false };
    readonly DockPanel _frame = new();
    bool _wrapping;

    /// <summary>Eigentlicher Inhalt (ohne Titelleiste).</summary>
    public UIElement? Body { get; private set; }

    public DialogWindow()
    {
        _bar.SetBinding(Wpf.Ui.Controls.TitleBar.TitleProperty, new Binding(nameof(Title)) { Source = this });
        DockPanel.SetDock(_bar, Dock.Top);
        _frame.Children.Add(_bar);
        // Der WPF-UI-Fensterstil setzt eine Mindestgröße (460 × 320) – kleine Rückfragen bekämen sonst leeren Raum
        MinWidth = 0; MinHeight = 0;
        Theme.Prepare(this);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        bool resizable = ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
        _bar.ShowMaximize = resizable; _bar.CanMaximize = resizable;
        base.OnSourceInitialized(e);
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (_wrapping || ReferenceEquals(newContent, _frame)) return;
        _wrapping = true;
        try
        {
            if (Body != null) _frame.Children.Remove(Body);
            Body = newContent as UIElement;
            Content = _frame;   // löst den neuen Inhalt vom Fenster, danach kommt er unter die Titelleiste
            if (Body != null) _frame.Children.Add(Body);
        }
        finally { _wrapping = false; }
    }
}
