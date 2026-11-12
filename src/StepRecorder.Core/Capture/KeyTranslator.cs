using System.Globalization;
using System.Text;
using static StepRecorder.Native.Win32;

namespace StepRecorder.Capture;

/// <summary>
/// Übersetzt Tasten in Zeichen mit eigenem Tastenzustand (ToUnicodeEx, Flag 0x4 – verändert den
/// Kernelzustand nicht) und setzt Tottasten selbst zusammen.
/// </summary>
public sealed class KeyTranslator
{
    readonly byte[] _state = new byte[256];
    string? _dead;
    public long DeadSeq;

    public void Reset() { Array.Clear(_state); _dead = null; SyncLocks(); }

    public void SyncLocks()
    {
        foreach (int vk in new[] { 0x14, 0x90, 0x91 })
            _state[vk] = (byte)((GetKeyState(vk) & 1) != 0 ? 0x01 : 0);
    }

    public void Down(uint vk)
    {
        _state[vk] |= 0x80;
        switch (vk)
        {
            case 0xA0 or 0xA1: _state[0x10] |= 0x80; break;
            case 0xA2 or 0xA3: _state[0x11] |= 0x80; break;
            case 0xA4 or 0xA5: _state[0x12] |= 0x80; break;
            case 0x14 or 0x90 or 0x91: _state[vk] ^= 0x01; break;
        }
    }

    public void Up(uint vk)
    {
        _state[vk] &= 0x7F;
        switch (vk)
        {
            case 0xA0 or 0xA1: if ((_state[0xA0] | _state[0xA1]) < 0x80) _state[0x10] &= 0x7F; break;
            case 0xA2 or 0xA3: if ((_state[0xA2] | _state[0xA3]) < 0x80) _state[0x11] &= 0x7F; break;
            case 0xA4 or 0xA5: if ((_state[0xA4] | _state[0xA5]) < 0x80) _state[0x12] &= 0x7F; break;
        }
    }

    public bool IsDown(uint vk) => (_state[vk] & 0x80) != 0;
    public bool Ctrl => IsDown(0xA2) || IsDown(0xA3);
    public bool LCtrl => IsDown(0xA2);
    public bool Alt => IsDown(0xA4) || IsDown(0xA5);
    public bool RAlt => IsDown(0xA5);
    public bool Shift => IsDown(0xA0) || IsDown(0xA1);
    public bool Win => IsDown(0x5B) || IsDown(0x5C);
    public bool AltGr => RAlt && LCtrl;

    public List<string> Locks()
    {
        var l = new List<string>();
        if ((_state[0x14] & 1) != 0) l.Add("caps");
        if ((_state[0x90] & 1) != 0) l.Add("num");
        if ((_state[0x91] & 1) != 0) l.Add("scroll");
        return l;
    }

    /// <summary>Gehaltene Modifier vor dem Ereignis (ohne die Taste selbst).</summary>
    public List<string> Mods(uint exceptVk)
    {
        var l = new List<string>();
        bool altGr = AltGr && exceptVk != 0xA5 && exceptVk != 0xA2;
        if (altGr) { l.Add("altGr"); }
        else
        {
            if (Ctrl && exceptVk is not (0xA2 or 0xA3)) l.Add("ctrl");
            if (Alt && exceptVk is not (0xA4 or 0xA5)) l.Add("alt");
        }
        if (Shift && exceptVk is not (0xA0 or 0xA1)) l.Add("shift");
        if (Win && exceptVk is not (0x5B or 0x5C)) l.Add("win");
        return l;
    }

    /// <summary>Zeichen für einen Tastendruck. dead=true, wenn es eine Tottaste war.</summary>
    public (string? ch, bool dead) Translate(uint vk, uint scan, nint hkl)
    {
        if (vk is 0x0D or 0x09 or 0x08 or 0x1B) return (null, false);
        // Bei Strg/Alt ohne AltGr entsteht kein Textzeichen
        if ((Ctrl || Alt) && !AltGr) return (null, false);
        var sb = new StringBuilder(8);
        int n = ToUnicodeEx(vk, scan, _state, sb, sb.Capacity, 0x4, hkl);
        if (n < 0)
        {
            // Tottaste: Zeichen merken; Kernelzustand bleibt dank Flag 0x4 unberührt
            _dead = sb.Length > 0 ? sb.ToString(0, 1) : "";
            return (null, true);
        }
        if (n == 0) return (null, false);
        string s = sb.ToString(0, n);
        if (s.Length == 1 && char.IsControl(s[0])) return (null, false);
        if (_dead != null)
        {
            string composed = Compose(_dead, s);
            _dead = null;
            return (composed, false);
        }
        return (s, false);
    }

    static string Compose(string dead, string ch)
    {
        string? comb = dead switch
        {
            "^" => "̂", "`" => "̀", "´" => "́", "'" => "́", "~" => "̃", "¨" => "̈", "\"" => "̈",
            "°" => "̊", "¸" => "̧", "ˇ" => "̌", "˘" => "̆", "˝" => "̋", "˛" => "̨", "˙" => "̇", _ => null
        };
        if (ch == " ") return dead;
        if (comb != null)
        {
            var c = (ch + comb).Normalize(NormalizationForm.FormC);
            if (c.Length == 1) return c;
        }
        return dead + ch;
    }

    public static string HklHex(nint hkl) => ((long)hkl & 0xFFFFFFFF).ToString("X8", CultureInfo.InvariantCulture);
}
