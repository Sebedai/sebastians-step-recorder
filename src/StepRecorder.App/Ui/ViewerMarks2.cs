using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Bild-Editor II (13, Paket 28): Zeichnungen duplizieren, mit Pfeiltasten verschieben, kopieren und in andere Schritte einfügen,
/// Sprechblase und Stempel, Weichzeichnen als Schwärzungsart.
/// </summary>
sealed partial class ViewerWindow
{
    /// <summary>Zwischenablage für Zeichnungen (auch zwischen Schritten und Editorfenstern): JSON der Zeichnung und Bildgröße.</summary>
    static (JsonObject ann, int w, int h)? _markClip;
    string _stamp = "check";

    JsonObject? SelectedAnnotation() => _selAnn != null && _currentMeta != null ? AnnotationsFor(_currentMeta.FrameId).FirstOrDefault(x => x.Str("id") == _selAnn) : null;

    void CopyMark()
    {
        if (SelectedAnnotation() is not { } a || _currentMeta == null) return;
        _markClip = ((JsonObject)a.DeepClone(), _currentMeta.SourceRect.Width, _currentMeta.SourceRect.Height);
        _status.Text = Loc.L("Zeichnung kopiert – mit Strg+V in diesem oder einem anderen Schritt einfügen.", "Drawing copied – paste it with Ctrl+V into this or another step.");
    }

    /// <summary>Einfügen: auf das aktuelle Bild, etwas versetzt; bei anderer Bildgröße an die gleiche relative Stelle.</summary>
    void PasteMark() => InsertMarkCopy(_markClip?.ann, 16, scaleFrom: _markClip is { } c ? (c.w, c.h) : null);

    void DuplicateMark()
    {
        if (SelectedAnnotation() is { } a) InsertMarkCopy(a, 16, null);
    }

    void InsertMarkCopy(JsonObject? src, float offset, (int w, int h)? scaleFrom)
    {
        var s = Sel;
        if (src == null || s == null || _currentMeta == null || _view.Session.ReadOnly) return;
        var a = (JsonObject)src.DeepClone();
        float dx = offset, dy = offset;
        if (scaleFrom is { } f && f.w > 0 && f.h > 0 && AnnotationGeometry.Bounds(a) is { } b && (f.w != _currentMeta.SourceRect.Width || f.h != _currentMeta.SourceRect.Height))
        {
            // an dieselbe relative Stelle im neuen Bild (Größe bleibt)
            var sr = _currentMeta.SourceRect;
            float nx = sr.X + (b.X - 0) / f.w * sr.Width, ny = sr.Y + (b.Y - 0) / f.h * sr.Height;
            dx = nx - b.X; dy = ny - b.Y;
        }
        a["geom"] = AnnotationGeometry.Moved(a, dx, dy);
        string frame = _currentMeta.FrameId;
        string? newId = null;
        Change(e =>
        {
            var arr = e["annotations"] as JsonArray ?? new JsonArray();
            a["id"] = newId = NextId(e, "annotations", "a");
            a["frame"] = frame; a["anchor"] = s.Anchor;
            arr.Add(a); e["annotations"] = arr;
            if (a.Str("type") == "badge" && a.Bool("auto")) Annotations.Renumber(e, frame);
        }, refill: false);
        _selAnn = newId; DrawSelection();
    }

    /// <summary>Gewählte Zeichnung um 1 Bildpunkt (mit Umschalt 10) verschieben.</summary>
    void NudgeMark(int dx, int dy)
    {
        if (_selAnn is not { } id) return;
        MoveAnnotation(id, dx, dy);
        DrawSelection();
    }

    /// <summary>Stempelart wählen und Werkzeug aktivieren.</summary>
    void ChooseStamp(FrameworkElement owner)
    {
        var m = U.MenuScope(new ContextMenu { PlacementTarget = owner, Placement = System.Windows.Controls.Primitives.PlacementMode.Right });
        foreach (var k in AnnotationGeometry.Stamps)
        {
            string kind = k;
            var mi = new MenuItem { Header = AnnotationGeometry.StampName(k, Loc.IsGerman), IsCheckable = true, IsChecked = _stamp == k };
            mi.Click += (_, _) => { _stamp = kind; SelectTool("stamp"); };
            m.Items.Add(mi);
        }
        m.IsOpen = true;
    }

    /// <summary>Sprechblase: Kasten wie aufgezogen, Spitze unten links außerhalb; Text über einen Dialog.</summary>
    void AddCallout(int x, int y, int w, int h)
    {
        var d = new InputDialog(Loc.L("Text der Sprechblase", "Text of the speech bubble"), "") { Owner = this };
        if (d.ShowDialog() != true || d.Value.Trim().Length == 0) { ShowStep(); return; }
        w = Math.Max(w, 60); h = Math.Max(h, 28);
        AddAnnotation("callout", new JsonArray(x, y, w, h, x - 30, y + h + 40), d.Value.Trim());
    }
}
