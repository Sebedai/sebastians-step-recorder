namespace StepRecorder.Export;

/// <summary>Ziel ist nicht beschreibbar oder würde fremde Dateien überschreiben (Exit-Code 8).</summary>
public sealed class ExportTargetException : IOException
{
    public ExportTargetException(string message) : base(message) { }
}

/// <summary>Zielordner für Ordner-Exporte (KI-Paket, AutoHotkey-KI): nie einen fremden Ordner leeren.</summary>
public static class ExportFolders
{
    /// <summary>
    /// Bereitet einen Ordner vor, den der Export vollständig besitzt. Ein vorhandener Ordner wird nur ersetzt,
    /// wenn er leer ist oder die Kennzeichen-Datei eines früheren Exports enthält (<paramref name="marker"/>).
    /// Sonst bricht der Export ab, statt fremde Dateien zu löschen.
    /// </summary>
    /// <summary>Prüft nur (ohne zu löschen), ob ein Ordner ersetzt werden dürfte.</summary>
    public static void CheckOwnable(string dir, string marker)
    {
        if (File.Exists(dir)) throw new ExportTargetException($"Ziel ist eine Datei, kein Ordner: {dir}");
        if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any() && !File.Exists(Path.Combine(dir, marker)))
            throw new ExportTargetException($"Der Zielordner ist nicht leer und enthält keinen früheren Export ({marker}) – er wird nicht überschrieben: {dir}");
    }

    public static void PrepareOwned(string dir, string marker)
    {
        if (File.Exists(dir)) throw new ExportTargetException($"Ziel ist eine Datei, kein Ordner: {dir}");
        if (Directory.Exists(dir))
        {
            bool empty = !Directory.EnumerateFileSystemEntries(dir).Any();
            if (!empty && !File.Exists(Path.Combine(dir, marker)))
                throw new ExportTargetException($"Der Zielordner ist nicht leer und enthält keinen früheren Export ({marker}) – er wird nicht überschrieben: {dir}");
            if (!empty) Directory.Delete(dir, true);
        }
        Directory.CreateDirectory(dir);
    }
}
