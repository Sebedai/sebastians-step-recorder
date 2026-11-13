using StepRecorder.Data;
using StepRecorder.Capture;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Passwortabfragen im Terminal (10 §6, DS-022/DS-023): Regeln T1 bis T3 und die Darstellung maskierter Abfragen.</summary>
public class TerminalPromptTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-tp-" + Guid.NewGuid().ToString("N")[..8]);
    public TerminalPromptTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    static string[] L(params string[] lines) => lines;

    // ------------------------------------------------------------------ T1

    [Theory]
    // Beispiele aus 10 §6.2
    [InlineData("admin@srv-app01's password:")]
    [InlineData("[sudo] password for admin:")]
    [InlineData("[sudo] Passwort für admin:")]
    [InlineData(@"Enter passphrase for key 'C:\Users\seb\.ssh\id_ed25519':")]
    [InlineData("Password for 'https://user@example.org':")]
    [InlineData("Geben Sie das Kennwort für \"KONTO\" ein:")]
    [InlineData("Kennwort:")]
    // alle Schlüsselwörter, auch nicht lateinische
    [InlineData("Enter passcode for Duo>")]
    [InlineData("Enter PIN:")]
    [InlineData("Client secret?")]
    [InlineData("GitHub token:")]
    [InlineData("Mot de passe :")]
    [InlineData("Contraseña:")]
    [InlineData("Senha:")]
    [InlineData("Wachtwoord:")]
    [InlineData("Hasło:")]
    [InlineData("Пароль:")]
    [InlineData("请输入密码:")]
    [InlineData("密码：")]
    [InlineData("パスワード:")]
    [InlineData("Neues Domänenkennwort?")]
    [InlineData("PASSWORD:")]
    [InlineData("Password:     ")]                 // Konsolen füllen mit Leerzeichen auf
    [InlineData("Password: ***")]                  // schon sichtbares Maskenecho
    public void T1_RecognizesPromptLines(string line)
    {
        var hit = TerminalPrompt.MatchT1(L("PS C:\\> ssh admin@srv-app01", line));
        Assert.NotNull(hit);
        Assert.Equal("T1", hit!.Rule);
        Assert.Equal("T1", TerminalPrompt.Detect(L(line), null, 0)?.Rule);
        Assert.DoesNotContain("ssh admin", hit.PromptLine);
    }

    [Theory]
    [InlineData(@"PS C:\Users\admin\password-tools>")]     // Verzeichnisname mit Schlüsselwort: Shell, keine Abfrage
    [InlineData(@"C:\Users\passwort>")]
    [InlineData("user@host:~$")]
    [InlineData("Port mapping:")]                          // „pin“ nur als eigenes Wort
    [InlineData("Spinning up containers:")]
    [InlineData("Password changed successfully.")]
    [InlineData("Your password expires soon, please contact the administrator of this system today:")]   // mehr als 60 Zeichen bis zum Ende
    [InlineData("")]
    public void T1_IgnoresOrdinaryLines(string line)
    {
        Assert.Null(TerminalPrompt.MatchT1(L(line)));
        Assert.Null(TerminalPrompt.Detect(L(line), null, 0));
    }

    [Fact]
    public void T1_LooksOnlyAtLastNonEmptyLine()
    {
        Assert.Null(TerminalPrompt.MatchT1(L("Password:", @"PS C:\>")));
        Assert.NotNull(TerminalPrompt.MatchT1(L("Sorry, try again.", "[sudo] password for admin:", "", "   ")));
        Assert.Null(TerminalPrompt.MatchT1(null));
        Assert.Null(TerminalPrompt.MatchT1(Array.Empty<string>()));
    }

    [Fact]
    public void T1_KeywordsAreDataAndComplete()
    {
        var words = TerminalPrompt.Keywords.Select(k => k.Word).ToList();
        foreach (var w in new[] { "password", "passwort", "kennwort", "passphrase", "passcode", "pin", "secret", "token", "mot de passe", "contraseña", "senha", "wachtwoord", "hasło", "пароль", "密码", "パスワード" })
            Assert.Contains(w, words);
    }

    [Fact]
    public void T1_RecognizesPromptThatAppearsAfterTyping()
    {
        // Abfrage erschien erst, als schon getippt wurde (schnelle Folgeabfrage): „nachher“ zählt auch
        var hit = TerminalPrompt.Detect(L("$ sudo ls"), L("$ sudo ls", "[sudo] password for admin:"), 2);
        Assert.Equal("T1", hit?.Rule);
        Assert.Equal("[sudo] password for admin:", hit?.PromptLine);
    }

    // ------------------------------------------------------------------ T2

    [Theory]
    [InlineData("Enter value:", "Enter value: ****", 4)]
    [InlineData("Geheimwort:", "Geheimwort: •••", 3)]
    [InlineData("Zugangscode: ", "Zugangscode: ●●", 2)]
    [InlineData("Code eingeben: *", "Code eingeben: ***", 3)]       // erstes Zeichen schon als Maske zu sehen
    [InlineData("Neuer Wert>", "Neuer Wert>*", 1)]
    public void T2_RecognizesMaskEcho(string before, string after, int typed)
    {
        var hit = TerminalPrompt.Detect(L(before), L(after), typed);
        Assert.Equal("T2", hit?.Rule);
        Assert.Equal(before.TrimEnd(' ', '*').TrimEnd(), hit!.PromptLine);
    }

    [Theory]
    [InlineData("Enter value:", "Enter value: **", 4)]      // nicht gleich viele Maskenzeichen
    [InlineData("Enter value:", "Other: ****", 4)]          // andere Zeile
    [InlineData("Enter value:", "Enter value: abcd", 4)]    // Klartext-Echo
    [InlineData("Enter value:", "Enter value: ****", 0)]
    public void T2_RequiresSameNumberOfMaskChars(string before, string after, int typed)
        => Assert.Null(TerminalPrompt.Detect(L(before), L(after), typed));

    [Fact]
    public void T2_AcceptsRangeWhileEchoLags() => Assert.Equal("T2", TerminalPrompt.Detect(L("Value:"), L("Value: ***"), 2, 4)?.Rule);

    // ------------------------------------------------------------------ T3

    [Theory]
    [InlineData("Bitte Code eingeben:", 2)]
    [InlineData("Enter code?", 3)]
    [InlineData("Code>", 5)]
    [InlineData("Введите код:", 4)]
    public void T3_RecognizesPromptWithoutEcho(string line, int typed)
    {
        var hit = TerminalPrompt.Detect(L("irgendwas davor", line), L("irgendwas davor", line), typed);
        Assert.Equal("T3", hit?.Rule);
        Assert.Equal(line, hit!.PromptLine);
    }

    [Theory]
    [InlineData("Bitte Code eingeben:", "Bitte Code eingeben:", 1)]   // weniger als zwei Zeichen
    [InlineData("Name:", "Name: ab", 2)]                               // Echo
    [InlineData("Weiter", "Weiter", 3)]                                // kein Abfrage-Ende
    [InlineData(@"PS C:\>", @"PS C:\> dir", 3)]                        // PowerShell mit Echo
    [InlineData(@"PS C:\>", @"PS C:\>", 2)]                            // PowerShell, Echo verspätet: trotzdem keine Abfrage
    [InlineData(@"C:\Windows>", @"C:\Windows>", 2)]                    // Eingabeaufforderung, Echo verspätet
    [InlineData("user@host:~$", "user@host:~$", 3)]                    // bash
    [InlineData(">>>", ">>>", 3)]                                      // Python
    public void T3_NegativeCases(string before, string after, int typed)
        => Assert.Null(TerminalPrompt.Detect(L(before), L(after), typed));

    [Fact]
    public void T3_NeedsBothReadings()
    {
        Assert.Null(TerminalPrompt.Detect(null, L("Code:"), 3));
        Assert.Null(TerminalPrompt.Detect(L("Code:"), null, 3));
    }

    // ------------------------------------------------------------------ gespeicherter Text (6.3)

    [Fact]
    public void OnlyThePromptLineLeavesTheRules()
    {
        var hit = TerminalPrompt.Detect(L("geheime-ausgabe-4711", "noch mehr", "Password:"), L("geheime-ausgabe-4711", "noch mehr", "Password:"), 3);
        Assert.NotNull(hit);
        Assert.Equal("Password:", hit!.PromptLine);
        Assert.DoesNotContain("4711", hit.ToString());
    }

    [Fact]
    public void PromptLine_IsCutTo120Chars()
    {
        string line = new string('x', 200) + " password:";
        var hit = TerminalPrompt.MatchT1(L(line));
        Assert.NotNull(hit);
        Assert.Equal(120, hit!.PromptLine.Length);
        Assert.StartsWith("…", hit.PromptLine);
        Assert.EndsWith("password:", hit.PromptLine);
    }

    [Fact]
    public void TailLines_KeepsLastThreeNonEmpty()
    {
        Assert.Equal(new[] { "b", "c", "d" }, TerminalPrompt.TailLines("a\r\n\r\nb   \r\nc\r\n   \r\nd\r\n\r\n"));
        Assert.Empty(TerminalPrompt.TailLines(null));
        Assert.True(TerminalPrompt.CursorLineBlank("Password:\r\n   "));
        Assert.False(TerminalPrompt.CursorLineBlank("x\r\nPassword: "));
    }

    // ------------------------------------------------------------------ Leseschicht

    [Fact]
    public void MaskedPromptStep_ShowsPromptLineAndNeedsNoReview()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "s"), withTerminal: true, withTerminalPrompt: true);
        using var v = SessionView.Open(folder, headless: true);
        var st = v.All.Single(s => s.Kind == "type" && s.Json.Obj("input")?.Str("maskReason") == "terminalPrompt");
        var input = st.Json.Obj("input")!;
        Assert.True(input.Bool("masked"));
        Assert.Equal("T1", input.Str("rule"));
        Assert.Equal(SyntheticSession.PromptLine, input.Str("promptLine"));
        Assert.DoesNotContain("plaintext", st.Json.StrList("flags"));
        Assert.False(st.Unreviewed);
        Assert.Null(st.FlagReason);
        Assert.Contains(SyntheticSession.PromptLine, st.Protocol);
        Assert.Contains("9", st.Protocol);
        Assert.Contains(SyntheticSession.PromptLine, st.Guide);
        // die Lücke des Privatmodus wird durch den maskierten Schritt dargestellt, kein eigener Lückenschritt
        Assert.DoesNotContain(v.All, s => s.Kind == "gap" && s.Json.Obj("input")?.Str("cause") == "privacyMode");
        // Klartext im Terminal bleibt prüfpflichtig
        var plain = v.All.Single(s => s.Kind == "type" && s.Json.Obj("ctx")?.Str("process") == "WindowsTerminal.exe" && s.Json.Obj("input")?.Bool("masked") != true);
        Assert.True(plain.Unreviewed);
        Assert.Equal("plaintextWindow", plain.FlagReason);
    }
}
