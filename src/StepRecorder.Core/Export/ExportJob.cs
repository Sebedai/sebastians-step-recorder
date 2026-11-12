namespace StepRecorder.Export;

public readonly record struct ExportProgress(int Done, int Total, string Phase);

/// <summary>
/// Fortschritt und Abbruch eines Exports – an den Thread gebunden, damit die Exporter keine neuen Parameter brauchen:
/// sie rufen nur <see cref="Tick"/> in ihren Schleifen auf. Ohne laufenden Auftrag ist <see cref="Tick"/> wirkungslos.
/// </summary>
public sealed class ExportJob : IDisposable
{
    [ThreadStatic] static ExportJob? _current;
    readonly IProgress<ExportProgress>? _progress;
    readonly CancellationToken _cancel;
    readonly ExportJob? _outer;
    long _lastReport;

    ExportJob(IProgress<ExportProgress>? progress, CancellationToken cancel) { _progress = progress; _cancel = cancel; _outer = _current; }

    public static ExportJob Begin(IProgress<ExportProgress>? progress, CancellationToken cancel)
    {
        var j = new ExportJob(progress, cancel);
        _current = j;
        return j;
    }

    public static CancellationToken Token => _current?._cancel ?? CancellationToken.None;

    /// <summary>Fortschritt melden (höchstens alle 100 ms) und bei Abbruch <see cref="OperationCanceledException"/> werfen.</summary>
    public static void Tick(int done, int total, string phase = "")
    {
        var j = _current;
        if (j == null) return;
        j._cancel.ThrowIfCancellationRequested();
        long now = Environment.TickCount64;
        if (j._progress != null && (now - j._lastReport >= 100 || done >= total))
        {
            j._lastReport = now;
            j._progress.Report(new ExportProgress(done, total, phase));
        }
    }

    public static void ThrowIfCancelled() => _current?._cancel.ThrowIfCancellationRequested();

    public void Dispose() { if (_current == this) _current = _outer; }
}
