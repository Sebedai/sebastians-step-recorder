using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using StepRecorder.Settings;

namespace StepRecorder.Export.Formats;

/// <summary>
/// Eigene Word-Vorlage (09 §7.3): Das Ergebnis ist die Vorlage selbst – mit ihren Formatvorlagen, Design, Schriften,
/// Seiteneinrichtung, Kopf- und Fußzeilen –, in die der erzeugte Inhalt an der Stelle <c>StepRecorderContent</c>
/// (Inhaltssteuerelement oder Textmarke; fehlt beides: am Ende) eingesetzt wird. Inhaltssteuerelemente mit den
/// Kennungen <c>SR.Title</c>, <c>SR.Subtitle</c>, <c>SR.Author</c>, <c>SR.Date</c>, <c>SR.Version</c> werden gefüllt.
/// Formatvorlagen werden über ihre Kennung gesucht, dann über ihren Namen; fehlende werden mit den Standardwerten
/// angelegt. Vorlagen mit Makros (.dotm/.docm) oder ActiveX werden abgelehnt; der Verweis auf die Vorlagendatei
/// (<c>attachedTemplate</c>) und andere Verweise nach außen aus den Einstellungen werden entfernt.
/// </summary>
public sealed partial class SimpleDocx
{
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace Pr = "http://schemas.openxmlformats.org/package/2006/relationships";
    static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";
    const string RelBase = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
    const string NsWp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    const string NsA = "http://schemas.openxmlformats.org/drawingml/2006/main";
    const string NsPic = "http://schemas.openxmlformats.org/drawingml/2006/picture";

    /// <summary>Kennung (Inhaltssteuerelement) bzw. Name (Textmarke) der Stelle, an der der Inhalt eingesetzt wird.</summary>
    public const string ContentMarker = "StepRecorderContent";

    byte[]? _template;
    Dictionary<string, byte[]>? _parts;
    List<string>? _order;
    string _mainPart = "word/document.xml";

    /// <summary>Werte für die Inhaltssteuerelemente der Vorlage (Kennung → Text), z. B. <c>SR.Title</c>.</summary>
    public Dictionary<string, string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Mit Word-Vorlage?</summary>
    public bool HasTemplate => _template != null;

    /// <summary>
    /// Prüft eine Word-Vorlage; null = brauchbar, sonst der Grund (in der Sprache der Oberfläche).
    /// Abgelehnt werden: keine Word-Datei, Makros (vbaProject, macroEnabled), ActiveX-Steuerelemente.
    /// </summary>
    public static string? CheckTemplate(byte[] data)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
            var names = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();
            var ctEntry = zip.GetEntry("[Content_Types].xml");
            if (ctEntry == null) return Loc.L("Keine Word-Datei (.dotx oder .docx).", "Not a Word file (.dotx or .docx).");
            string ct; using (var r = new StreamReader(ctEntry.Open())) ct = r.ReadToEnd();
            if (ct.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) || ct.Contains("vbaProject", StringComparison.OrdinalIgnoreCase)
                || names.Any(n => n.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase) || n.EndsWith("vbaData.xml", StringComparison.OrdinalIgnoreCase)))
                return Loc.L("Die Vorlage enthält Makros (.dotm/.docm) und wird abgelehnt.", "The template contains macros (.dotm/.docm) and is rejected.");
            if (names.Any(n => n.StartsWith("word/activeX/", StringComparison.OrdinalIgnoreCase)))
                return Loc.L("Die Vorlage enthält ActiveX-Steuerelemente und wird abgelehnt.", "The template contains ActiveX controls and is rejected.");
            if (!ct.Contains("wordprocessingml.document.main+xml") && !ct.Contains("wordprocessingml.template.main+xml"))
                return Loc.L("Keine Word-Datei (.dotx oder .docx).", "Not a Word file (.dotx or .docx).");
            var main = MainPartOf(zip);
            if (zip.GetEntry(main) == null) return Loc.L("Die Word-Vorlage ist beschädigt (Hauptteil fehlt).", "The Word template is damaged (main part missing).");
            XDocument.Load(zip.GetEntry(main)!.Open());
            return null;
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException)
        {
            return Loc.L("Keine lesbare Word-Datei (.dotx oder .docx).", "Not a readable Word file (.dotx or .docx).");
        }
    }

    static string MainPartOf(ZipArchive zip)
    {
        var rels = zip.GetEntry("_rels/.rels");
        if (rels == null) return "word/document.xml";
        var x = XDocument.Load(rels.Open());
        var t = x.Root?.Elements(Pr + "Relationship").FirstOrDefault(e => ((string?)e.Attribute("Type") ?? "").EndsWith("/officeDocument"))?.Attribute("Target")?.Value;
        return t == null ? "word/document.xml" : t.TrimStart('/');
    }

    /// <summary>
    /// Word-Vorlage verwenden. Wirft <see cref="InvalidOperationException"/>, wenn sie abgelehnt wird (z. B. Makros).
    /// Seitengröße und Ränder kommen ab jetzt aus der Vorlage (für die Bildbreiten).
    /// </summary>
    public void UseTemplate(byte[] data)
    {
        if (CheckTemplate(data) is { } why) throw new InvalidOperationException(why);
        _template = data;
        _parts = new(StringComparer.Ordinal); _order = new();
        using (var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
        {
            _mainPart = MainPartOf(zip);
            foreach (var e in zip.Entries)
            {
                if (e.FullName.EndsWith('/')) continue;
                using var ms = new MemoryStream(); using (var s = e.Open()) s.CopyTo(ms);
                string n = e.FullName.Replace('\\', '/');
                if (_parts.TryAdd(n, ms.ToArray())) _order.Add(n);
            }
        }
        // Seiteneinrichtung der Vorlage (letzter Abschnitt) für Satzspiegel und Bildgrößen
        var doc = Load(_mainPart)!;
        var sect = doc.Root?.Element(W + "body")?.Elements(W + "sectPr").LastOrDefault();
        int Twips(XElement? e, string attr, int fallback) => int.TryParse((string?)e?.Attribute(W + attr), out var v) && v > 0 ? v : fallback;
        var sz = sect?.Element(W + "pgSz"); var mar = sect?.Element(W + "pgMar");
        PageWTwips = Twips(sz, "w", 11906); PageHTwips = Twips(sz, "h", 16838);
        _marL = Twips(mar, "left", 1134); _marR = Twips(mar, "right", 1134); _marT = Twips(mar, "top", 1134); _marB = Twips(mar, "bottom", 1134);
    }

    /// <summary>Hat die Vorlage ein Inhaltssteuerelement mit dieser Kennung (z. B. SR.Title)?</summary>
    public bool TemplateHasField(string tag)
        => _parts != null && Load(_mainPart)?.Descendants(W + "sdt").Any(s => TagOf(s) is { } t && t.Equals(tag, StringComparison.OrdinalIgnoreCase)) == true;

    XDocument? Load(string part) => _parts != null && _parts.TryGetValue(part, out var b) ? XDocument.Load(new MemoryStream(b), LoadOptions.PreserveWhitespace) : null;

    static byte[] Bytes(XDocument x)
    {
        using var ms = new MemoryStream();
        using (var w = XmlWriter.Create(ms, new XmlWriterSettings { Encoding = new UTF8Encoding(false) })) x.Save(w);
        return ms.ToArray();
    }

    static string? TagOf(XElement sdt)
    {
        var pr = sdt.Element(W + "sdtPr");
        return (string?)pr?.Element(W + "tag")?.Attribute(W + "val") ?? (string?)pr?.Element(W + "alias")?.Attribute(W + "val");
    }

    static string PartDir(string part) { int i = part.LastIndexOf('/'); return i < 0 ? "" : part[..(i + 1)]; }
    static string RelsOf(string part) { int i = part.LastIndexOf('/'); return (i < 0 ? "" : part[..(i + 1)]) + "_rels/" + part[(i + 1)..] + ".rels"; }

    static string Resolve(string fromPart, string target)
    {
        if (target.StartsWith('/')) return target.TrimStart('/');
        var parts = new List<string>((PartDir(fromPart) + target).Split('/'));
        for (int i = 0; i < parts.Count; i++)
            if (parts[i] == "..") { parts.RemoveAt(i); if (i > 0) parts.RemoveAt(--i); i--; }
            else if (parts[i] == ".") parts.RemoveAt(i--);
        return string.Join("/", parts);
    }

    void SaveWithTemplate(string path)
    {
        var parts = new Dictionary<string, byte[]>(_parts!, StringComparer.Ordinal);
        var order = new List<string>(_order!);
        void Put(string name, byte[] data) { if (!parts.ContainsKey(name)) order.Add(name); parts[name] = data; }

        // Paketinhalt: Hauptteil als Dokument statt Vorlage, Bildtypen
        var ct = XDocument.Load(new MemoryStream(parts["[Content_Types].xml"]));
        foreach (var o in ct.Root!.Elements(Ct + "Override"))
            if ((string?)o.Attribute("ContentType") is { } t && t.Contains("wordprocessingml.template.main+xml"))
                o.SetAttributeValue("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml");
        void Default(string ext, string type)
        {
            if (!ct.Root.Elements(Ct + "Default").Any(d => string.Equals((string?)d.Attribute("Extension"), ext, StringComparison.OrdinalIgnoreCase)))
                ct.Root.AddFirst(new XElement(Ct + "Default", new XAttribute("Extension", ext), new XAttribute("ContentType", type)));
        }
        void Override(string part, string type)
        {
            if (!ct.Root.Elements(Ct + "Override").Any(d => string.Equals(((string?)d.Attribute("PartName"))?.TrimStart('/'), part, StringComparison.OrdinalIgnoreCase)))
                ct.Root.Add(new XElement(Ct + "Override", new XAttribute("PartName", "/" + part), new XAttribute("ContentType", type)));
        }
        if (_media.Any(m => m.ext == "png")) Default("png", "image/png");
        if (_media.Any(m => m.ext is "jpg" or "jpeg")) Default("jpg", "image/jpeg");

        // Beziehungen des Hauptteils: Bilder ergänzen, Formatvorlagen finden
        string relsPart = RelsOf(_mainPart);
        var rels = parts.TryGetValue(relsPart, out var rb) ? XDocument.Load(new MemoryStream(rb)) : new XDocument(new XElement(Pr + "Relationships"));
        string? Target(string type) => rels.Root!.Elements(Pr + "Relationship").FirstOrDefault(e => (string?)e.Attribute("Type") == RelBase + type) is { } e ? Resolve(_mainPart, (string)e.Attribute("Target")!) : null;
        string dir = PartDir(_mainPart);
        for (int i = 0; i < _media.Count; i++)
        {
            string file = "sr-" + _media[i].name;
            rels.Root!.Add(new XElement(Pr + "Relationship", new XAttribute("Id", $"rIdSrImg{i + 1}"), new XAttribute("Type", RelBase + "image"), new XAttribute("Target", "media/" + file)));
            Put(dir + "media/" + file, _media[i].data);
        }

        // Formatvorlagen: über Kennung, dann über Namen; fehlende mit Standardwerten anlegen
        var ours = XDocument.Parse(Styles());
        string? stylesPart = Target("styles");
        XDocument styles;
        if (stylesPart != null && parts.ContainsKey(stylesPart)) styles = XDocument.Load(new MemoryStream(parts[stylesPart]));
        else
        {
            stylesPart = dir + "styles.xml";
            styles = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(W + "styles", ours.Root!.Element(W + "docDefaults")));
            rels.Root!.Add(new XElement(Pr + "Relationship", new XAttribute("Id", "rIdSrStyles"), new XAttribute("Type", RelBase + "styles"), new XAttribute("Target", stylesPart[dir.Length..])));
            Override(stylesPart, "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml");
        }
        for (int i = 0; i < _links.Count; i++)
            rels.Root!.Add(new XElement(Pr + "Relationship", new XAttribute("Id", $"rIdSrLink{i + 1}"), new XAttribute("Type", RelBase + "hyperlink"), new XAttribute("Target", _links[i]), new XAttribute("TargetMode", "External")));
        var map = MergeStyles(styles, ours);
        Put(stylesPart, Bytes(styles));
        Put(relsPart, Bytes(rels));

        // Inhalt: Formatvorlagen umschreiben, Kennungen über die der Vorlage hinaus verschieben
        var doc = Load(_mainPart)!;
        string body = _body.ToString();
        body = Regex.Replace(body, "<w:(pStyle|rStyle) w:val=\"([^\"]*)\"/>", m => $"<w:{m.Groups[1].Value} w:val=\"{X(map.TryGetValue(m.Groups[2].Value, out var id) ? id : m.Groups[2].Value)}\"/>");
        body = body.Replace("r:embed=\"rIdImg", "r:embed=\"rIdSrImg").Replace("r:id=\"rIdLink", "r:id=\"rIdSrLink");
        int maxPr = 0, maxBm = 0;
        foreach (var (n, data) in parts)
            if (n.EndsWith(".xml") && n.StartsWith(dir))
                foreach (Match m in Regex.Matches(Encoding.UTF8.GetString(data), "<(?:wp:docPr|pic:cNvPr)[^>]*\\sid=\"(\\d+)\"")) maxPr = Math.Max(maxPr, int.Parse(m.Groups[1].Value));
        foreach (var bm in doc.Descendants(W + "bookmarkStart")) if (int.TryParse((string?)bm.Attribute(W + "id"), out var bid)) maxBm = Math.Max(maxBm, bid);
        body = Regex.Replace(body, "<(wp:docPr|pic:cNvPr) id=\"(\\d+)\"", m => $"<{m.Groups[1].Value} id=\"{int.Parse(m.Groups[2].Value) + maxPr}\"");
        body = Regex.Replace(body, "<w:(bookmarkStart|bookmarkEnd) w:id=\"(\\d+)\"", m => $"<w:{m.Groups[1].Value} w:id=\"{int.Parse(m.Groups[2].Value) + maxBm}\"");
        var wrapper = XElement.Parse($"<w:body xmlns:w=\"{W.NamespaceName}\" xmlns:r=\"{R.NamespaceName}\" xmlns:wp=\"{NsWp}\">{body}</w:body>");
        var content = wrapper.Nodes().ToList();
        foreach (var n in content) n.Remove();

        var root = doc.Root!;
        foreach (var (prefix, ns) in new[] { ("w", W.NamespaceName), ("r", R.NamespaceName), ("wp", NsWp), ("a", NsA), ("pic", NsPic) })
            if (!root.Attributes().Any(a => a.IsNamespaceDeclaration && a.Value == ns) && root.Attribute(XNamespace.Xmlns + prefix) == null)
                root.SetAttributeValue(XNamespace.Xmlns + prefix, ns);
        Insert(root.Element(W + "body")!, content);
        FillFields(doc);
        Put(_mainPart, Bytes(doc));
        foreach (var n in order.ToList())
            if (n.StartsWith(dir) && Regex.IsMatch(n[dir.Length..], @"^(header|footer)\d*\.xml$") && Load(n) is { } hf) { FillFields(hf); Put(n, Bytes(hf)); }

        // Einstellungen: Verweis auf die Vorlagendatei und Verweise nach außen entfernen
        if (Target("settings") is { } settingsPart && Load(settingsPart) is { } settings)
        {
            settings.Descendants(W + "attachedTemplate").Remove();
            Put(settingsPart, Bytes(settings));
            string sr = RelsOf(settingsPart);
            if (parts.TryGetValue(sr, out var srb))
            {
                var sx = XDocument.Load(new MemoryStream(srb));
                sx.Root?.Elements(Pr + "Relationship").Where(e => (string?)e.Attribute("TargetMode") == "External").Remove();
                Put(sr, Bytes(sx));
            }
        }

        // Dokumentangaben: Titel und Programm (nie Benutzer- oder Rechnername)
        string core = "docProps/core.xml";
        var rootRels = XDocument.Load(new MemoryStream(parts["_rels/.rels"]));
        const string coreType = "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties";
        if (rootRels.Root!.Elements(Pr + "Relationship").FirstOrDefault(e => (string?)e.Attribute("Type") == coreType) is { } ce) core = ((string)ce.Attribute("Target")!).TrimStart('/');
        else { rootRels.Root.Add(new XElement(Pr + "Relationship", new XAttribute("Id", "rIdSrCore"), new XAttribute("Type", coreType), new XAttribute("Target", core))); Put("_rels/.rels", Bytes(rootRels)); }
        Override(core, "application/vnd.openxmlformats-package.core-properties+xml");
        Put(core, Encoding.UTF8.GetBytes($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<cp:coreProperties xmlns:cp=""http://schemas.openxmlformats.org/package/2006/metadata/core-properties"" xmlns:dc=""http://purl.org/dc/elements/1.1/""><dc:title>{X(Title)}</dc:title><dc:creator>StepRecorder</dc:creator></cp:coreProperties>"));
        Put("[Content_Types].xml", Bytes(ct));

        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var n in order.Where(parts.ContainsKey))
        {
            bool media = n.Contains("/media/");
            var e = zip.CreateEntry(n, media ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
            e.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var s = e.Open(); s.Write(parts[n]);
        }
    }

    /// <summary>Ordnet die eigenen Formatvorlagen denen der Vorlage zu (Kennung, dann Name) und legt fehlende an. Liefert eigene Kennung → Kennung im Ergebnis.</summary>
    Dictionary<string, string> MergeStyles(XDocument styles, XDocument ours)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var root = styles.Root!;
        var theirs = root.Elements(W + "style").ToList();
        string? Attr(XElement e, string a) => (string?)e.Attribute(W + a);
        string? Name(XElement e) => (string?)e.Element(W + "name")?.Attribute(W + "val");
        var added = new List<XElement>();
        foreach (var s in ours.Root!.Elements(W + "style"))
        {
            string id = Attr(s, "styleId")!, type = Attr(s, "type") ?? "paragraph", name = Name(s) ?? id;
            var same = theirs.Where(t => (Attr(t, "type") ?? "paragraph") == type).ToList();
            var hit = same.FirstOrDefault(t => string.Equals(Attr(t, "styleId"), id, StringComparison.OrdinalIgnoreCase))
                      ?? same.FirstOrDefault(t => string.Equals(Name(t), name, StringComparison.OrdinalIgnoreCase))
                      ?? (id == "Normal" ? same.FirstOrDefault(t => Attr(t, "default") is "1" or "true") : null);
            if (hit != null) { map[id] = Attr(hit, "styleId")!; continue; }
            var copy = new XElement(s);
            copy.Attribute(W + "default")?.Remove();
            added.Add(copy); map[id] = id;
        }
        foreach (var a in added)
        {
            foreach (var refEl in a.Elements().Where(e => e.Name == W + "basedOn" || e.Name == W + "next" || e.Name == W + "link"))
                if (refEl.Attribute(W + "val") is { } v && map.TryGetValue(v.Value, out var to)) v.Value = to;
            root.Add(a);
        }
        // Sprache für Rechtschreibung und Silbentrennung, sofern die Vorlage keine festlegt
        var rpr = root.Element(W + "docDefaults")?.Element(W + "rPrDefault")?.Element(W + "rPr");
        if (rpr != null && rpr.Element(W + "lang") == null) rpr.Add(new XElement(W + "lang", new XAttribute(W + "val", Language)));
        return map;
    }

    /// <summary>Inhalt an der Marke einsetzen: Inhaltssteuerelement ersetzen, hinter dem Absatz der Textmarke, sonst vor dem letzten Abschnittsende.</summary>
    static void Insert(XElement body, List<XNode> content)
    {
        var sdt = body.Descendants(W + "sdt").FirstOrDefault(s => string.Equals(TagOf(s), ContentMarker, StringComparison.OrdinalIgnoreCase));
        if (sdt != null)
        {
            var p = sdt.Parent?.Name == W + "p" ? sdt.Parent : null;
            if (p != null) { p.AddAfterSelf(content); sdt.Remove(); }
            else { sdt.AddAfterSelf(content); sdt.Remove(); }
            return;
        }
        var bm = body.Descendants(W + "bookmarkStart").FirstOrDefault(b => string.Equals((string?)b.Attribute(W + "name"), ContentMarker, StringComparison.OrdinalIgnoreCase));
        if (bm != null)
        {
            var anchor = bm.Ancestors(W + "p").FirstOrDefault() ?? bm;
            anchor.AddAfterSelf(content);
            return;
        }
        if (body.Elements().LastOrDefault() is { } last && last.Name == W + "sectPr") last.AddBeforeSelf(content);
        else body.Add(content);
    }

    /// <summary>Inhaltssteuerelemente SR.Title usw. mit Text füllen (Format des ersten Laufs bleibt, Platzhalteranzeige entfällt).</summary>
    void FillFields(XDocument doc)
    {
        foreach (var sdt in doc.Descendants(W + "sdt").ToList())
        {
            if (TagOf(sdt) is not { } tag || !Fields.TryGetValue(tag, out var value)) continue;
            var pr = sdt.Element(W + "sdtPr");
            pr?.Element(W + "showingPlcHdr")?.Remove();
            var c = sdt.Element(W + "sdtContent");
            if (c == null) { c = new XElement(W + "sdtContent"); sdt.Add(c); }
            var texts = c.Descendants(W + "t").ToList();
            if (texts.Count > 0)
            {
                texts[0].Value = value;
                texts[0].SetAttributeValue(XNamespace.Xml + "space", "preserve");
                foreach (var t in texts.Skip(1)) t.Remove();
                var run = texts[0].Parent;
                run?.Element(W + "rPr")?.Elements(W + "rStyle").Where(s => ((string?)s.Attribute(W + "val") ?? "").StartsWith("Placeholder", StringComparison.OrdinalIgnoreCase) || (string?)s.Attribute(W + "val") == "Platzhaltertext").Remove();
                continue;
            }
            var r = new XElement(W + "r", new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value));
            if (c.Element(W + "p") is { } p) p.Add(r);
            else if (sdt.Parent?.Name == W + "p") c.Add(r);
            else c.Add(new XElement(W + "p", r));
        }
    }
}
