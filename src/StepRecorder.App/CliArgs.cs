namespace StepRecorder.App;

/// <summary>Strenger Argumentleser: unbekannte Optionen und fehlende Werte führen zu Exit-Code 2.</summary>
sealed class CliArgs
{
    public List<string> Positional { get; } = new();
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> Repeated { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Error { get; private set; }

    public string? this[string name] => Values.TryGetValue(name, out var v) ? v : null;
    public bool Has(string flag) => Flags.Contains(flag);
    public List<string> All(string name) => Repeated.TryGetValue(name, out var l) ? l : new();

    /// <param name="from">Ab welchem Argument gelesen wird (Befehl überspringen).</param>
    public static CliArgs Parse(string[] args, int from, string[] valueOptions, string[] flags, params string[] repeatable)
    {
        var r = new CliArgs();
        for (int i = from; i < args.Length; i++)
        {
            string a = args[i];
            if (a.StartsWith("--"))
            {
                if (flags.Contains(a, StringComparer.OrdinalIgnoreCase)) { r.Flags.Add(a); continue; }
                bool rep = repeatable.Contains(a, StringComparer.OrdinalIgnoreCase);
                if (!rep && !valueOptions.Contains(a, StringComparer.OrdinalIgnoreCase)) { r.Error = Settings.Loc.F("Unbekannte Option: {0}", "Unknown option: {0}", a); return r; }
                if (i + 1 >= args.Length || (args[i + 1].StartsWith("--") && args[i + 1].Length > 2)) { r.Error = Settings.Loc.F("Wert fehlt für {0}", "Missing value for {0}", a); return r; }
                string v = args[++i];
                if (rep) { if (!r.Repeated.TryGetValue(a, out var l)) r.Repeated[a] = l = new(); l.Add(v); }
                else r.Values[a] = v;
            }
            else r.Positional.Add(a);
        }
        return r;
    }
}
