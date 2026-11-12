using System.Text.Json.Nodes;
using StepRecorder.Steps;

namespace StepRecorder.Tests;

public class TextImprovementTests
{
    [Fact]
    public void Glossary_ReplacesWholeWords_LongestFirst()
    {
        var g = new Glossary(new[] { "Text-Editor=Editor", "SAP Easy Access=SAP-Startseite", "SAP=ERP", "kaputt" });
        Assert.Equal(3, g.Count);
        Assert.Equal("In „Editor“ klicken", g.Apply("In „Text-Editor“ klicken"));
        Assert.Equal("Zu „SAP-Startseite“ wechseln", g.Apply("Zu „SAP Easy Access“ wechseln"));
        Assert.Equal("SAPGUI bleibt", g.Apply("SAPGUI bleibt"));   // nur ganze Wörter
    }

    [Fact]
    public void MenuPath_InTitleAndGuide()
    {
        var step = new JsonObject
        {
            ["kind"] = "click", ["ctx"] = new JsonObject { ["title"] = "Editor" },
            ["target"] = new JsonObject { ["el"] = new JsonObject { ["ct"] = "MenuItem", ["name"] = "Speichern unter" } },
            ["menuPath"] = new JsonObject { ["items"] = new JsonArray("Datei", "Speichern unter") },
            ["input"] = new JsonObject()
        };
        Assert.Equal("„Datei › Speichern unter“ wählen", TextTemplates.Title(step, "de"));
        Assert.Equal("Wählen Sie im Menü „Datei › Speichern unter“.", TextTemplates.Body(step, "de"));
    }

    [Fact]
    public void RibbonGroup_InGuide()
    {
        var step = new JsonObject
        {
            ["kind"] = "click", ["ctx"] = new JsonObject { ["title"] = "Word" },
            ["target"] = new JsonObject { ["status"] = "element", ["el"] = new JsonObject { ["ct"] = "Button", ["name"] = "Fett" },
                                          ["path"] = new JsonArray(new JsonObject { ["ct"] = "Pane", ["name"] = "Menüband" }, new JsonObject { ["ct"] = "Group", ["name"] = "Schriftart" }) },
            ["input"] = new JsonObject()
        };
        Assert.Equal("Klicken Sie in der Gruppe „Schriftart“ auf die Schaltfläche „Fett“.", TextTemplates.Body(step, "de"));
    }
}

public class KeyInputTests
{
    [Theory]
    [InlineData("^", "e", "ê")]
    [InlineData("´", "a", "á")]
    [InlineData("`", "E", "È")]
    [InlineData("^", " ", "^")]
    [InlineData("^", "x", "^x")]
    [InlineData("~", "n", "ñ")]
    public void DeadKeys_Compose(string dead, string ch, string expected) => Assert.Equal(expected, StepRecorder.Capture.KeyTranslator.Compose(dead, ch));

    [Fact]
    public void VkPacket_YieldsUnicodeChar()
    {
        var k = new StepRecorder.Capture.KeyTranslator();
        Assert.Equal("ß", k.Translate(0xE7, 'ß', 0).ch);
        Assert.Equal("€", k.Translate(0xE7, '€', 0).ch);
        Assert.Null(k.Translate(0xE5, 0, 0).ch);   // IME-Taste: kein Zeichen
    }
}

public class DeadKeyComposeTests
{
    [Fact]
    public void AlreadyComposed_IsKept() => Assert.Equal("ê", StepRecorder.Capture.KeyTranslator.Compose("^", "ê"));
}
