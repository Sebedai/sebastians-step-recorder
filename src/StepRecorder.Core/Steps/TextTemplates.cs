using System.Globalization;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.Steps;

/// <summary>
/// Textvorlagen (05 §12): Titel und Text je Schritt, Stil Protokoll oder Anleitung. Sprachen: Deutsch, Englisch,
/// Französisch, Spanisch, Italienisch, Niederländisch (<see cref="OutText.Languages"/>); unbekannte Sprache → Englisch.
/// </summary>
public static class TextTemplates
{
    static readonly Dictionary<string, (string de, string deArt, string en)> TypeWords = new()
    {
        ["Button"] = ("Schaltfläche", "die Schaltfläche", "button"), ["SplitButton"] = ("Schaltfläche", "die Schaltfläche", "button"),
        ["Edit"] = ("Feld", "das Feld", "field"), ["Document"] = ("Textbereich", "den Textbereich", "text area"),
        ["CheckBox"] = ("Kontrollkästchen", "das Kontrollkästchen", "checkbox"), ["RadioButton"] = ("Optionsfeld", "das Optionsfeld", "option"),
        ["ComboBox"] = ("Auswahlliste", "die Auswahlliste", "drop-down list"), ["List"] = ("Liste", "die Liste", "list"),
        ["ListItem"] = ("Eintrag", "den Eintrag", "item"), ["TreeItem"] = ("Eintrag", "den Eintrag", "item"), ["DataItem"] = ("Zeile", "die Zeile", "row"),
        ["Menu"] = ("Menü", "das Menü", "menu"), ["MenuBar"] = ("Menüleiste", "die Menüleiste", "menu bar"), ["MenuItem"] = ("Menüpunkt", "den Menüpunkt", "menu item"),
        ["TabItem"] = ("Registerkarte", "die Registerkarte", "tab"), ["Tree"] = ("Baumansicht", "die Baumansicht", "tree"),
        ["Table"] = ("Tabelle", "die Tabelle", "table"), ["DataGrid"] = ("Tabelle", "die Tabelle", "table"), ["HeaderItem"] = ("Spaltenüberschrift", "die Spaltenüberschrift", "column header"),
        ["Hyperlink"] = ("Link", "den Link", "link"), ["Image"] = ("Bild", "das Bild", "image"), ["Slider"] = ("Schieberegler", "den Schieberegler", "slider"),
        ["Spinner"] = ("Drehfeld", "das Drehfeld", "spinner"), ["ScrollBar"] = ("Bildlaufleiste", "die Bildlaufleiste", "scroll bar"),
        ["ToolBar"] = ("Symbolleiste", "die Symbolleiste", "toolbar"), ["StatusBar"] = ("Statusleiste", "die Statusleiste", "status bar"),
        ["TitleBar"] = ("Titelleiste", "die Titelleiste", "title bar"), ["Text"] = ("Text", "den Text", "text"), ["Window"] = ("Fenster", "das Fenster", "window"),
    };

    /// <summary>Typwörter mit Artikel für Französisch, Spanisch, Italienisch, Niederländisch (immer mit Artikel verwendet).</summary>
    public static readonly IReadOnlyDictionary<string, (string fr, string es, string it, string nl)> TypeWordsMore = new Dictionary<string, (string, string, string, string)>
    {
        ["Button"] = ("le bouton", "el botón", "il pulsante", "de knop"), ["SplitButton"] = ("le bouton", "el botón", "il pulsante", "de knop"),
        ["Edit"] = ("le champ", "el campo", "il campo", "het veld"), ["Document"] = ("la zone de texte", "el área de texto", "l'area di testo", "het tekstgebied"),
        ["CheckBox"] = ("la case à cocher", "la casilla", "la casella di controllo", "het selectievakje"), ["RadioButton"] = ("l'option", "la opción", "l'opzione", "de optie"),
        ["ComboBox"] = ("la liste déroulante", "la lista desplegable", "l'elenco a discesa", "de vervolgkeuzelijst"), ["List"] = ("la liste", "la lista", "l'elenco", "de lijst"),
        ["ListItem"] = ("l'entrée", "la entrada", "la voce", "het item"), ["TreeItem"] = ("l'entrée", "la entrada", "la voce", "het item"), ["DataItem"] = ("la ligne", "la fila", "la riga", "de rij"),
        ["Menu"] = ("le menu", "el menú", "il menu", "het menu"), ["MenuBar"] = ("la barre de menus", "la barra de menús", "la barra dei menu", "de menubalk"), ["MenuItem"] = ("la commande", "la opción de menú", "la voce di menu", "het menu-item"),
        ["TabItem"] = ("l'onglet", "la pestaña", "la scheda", "het tabblad"), ["Tree"] = ("l'arborescence", "la vista de árbol", "la struttura ad albero", "de structuurweergave"),
        ["Table"] = ("le tableau", "la tabla", "la tabella", "de tabel"), ["DataGrid"] = ("le tableau", "la tabla", "la tabella", "de tabel"), ["HeaderItem"] = ("l'en-tête de colonne", "el encabezado de columna", "l'intestazione di colonna", "de kolomkop"),
        ["Hyperlink"] = ("le lien", "el vínculo", "il collegamento", "de koppeling"), ["Image"] = ("l'image", "la imagen", "l'immagine", "de afbeelding"), ["Slider"] = ("le curseur", "el control deslizante", "il dispositivo di scorrimento", "de schuifregelaar"),
        ["Spinner"] = ("le compteur", "el control numérico", "la casella numerica", "het numerieke veld"), ["ScrollBar"] = ("la barre de défilement", "la barra de desplazamiento", "la barra di scorrimento", "de schuifbalk"),
        ["ToolBar"] = ("la barre d'outils", "la barra de herramientas", "la barra degli strumenti", "de werkbalk"), ["StatusBar"] = ("la barre d'état", "la barra de estado", "la barra di stato", "de statusbalk"),
        ["TitleBar"] = ("la barre de titre", "la barra de título", "la barra del titolo", "de titelbalk"), ["Text"] = ("le texte", "el texto", "il testo", "de tekst"), ["Window"] = ("la fenêtre", "la ventana", "la finestra", "het venster"),
    };

    static readonly Dictionary<string, string> KeyDe = new()
    {
        ["Ctrl"] = "Strg", ["Shift"] = "Umschalt", ["Enter"] = "Eingabetaste", ["Escape"] = "Esc", ["Space"] = "Leertaste", ["Backspace"] = "Rücktaste",
        ["Delete"] = "Entf", ["Insert"] = "Einfg", ["Home"] = "Pos1", ["End"] = "Ende", ["PageUp"] = "Bild auf", ["PageDown"] = "Bild ab",
        ["Left"] = "Pfeil nach links", ["Right"] = "Pfeil nach rechts", ["Up"] = "Pfeil nach oben", ["Down"] = "Pfeil nach unten",
        ["PrintScreen"] = "Druck", ["Apps"] = "Kontextmenütaste", ["Tab"] = "Tab", ["Alt"] = "Alt", ["Win"] = "Win"
    };

    /// <summary>Tastennamen der weiteren Sprachen (Strg heißt überall „Ctrl“); nicht aufgeführte Tasten bleiben, wie aufgezeichnet.</summary>
    static readonly Dictionary<string, Dictionary<string, string>> KeyMore = new()
    {
        ["fr"] = new() { ["Shift"] = "Maj", ["Enter"] = "Entrée", ["Escape"] = "Échap", ["Space"] = "Espace", ["Backspace"] = "Retour arrière", ["Delete"] = "Suppr", ["Insert"] = "Inser",
                         ["Home"] = "Origine", ["End"] = "Fin", ["PageUp"] = "Page précédente", ["PageDown"] = "Page suivante", ["Left"] = "Flèche gauche", ["Right"] = "Flèche droite",
                         ["Up"] = "Flèche haut", ["Down"] = "Flèche bas", ["PrintScreen"] = "Impr écran", ["Apps"] = "touche Menu" },
        ["es"] = new() { ["Shift"] = "Mayús", ["Enter"] = "Intro", ["Escape"] = "Esc", ["Space"] = "Espacio", ["Backspace"] = "Retroceso", ["Delete"] = "Supr", ["Insert"] = "Insert",
                         ["Home"] = "Inicio", ["End"] = "Fin", ["PageUp"] = "Re Pág", ["PageDown"] = "Av Pág", ["Left"] = "Flecha izquierda", ["Right"] = "Flecha derecha",
                         ["Up"] = "Flecha arriba", ["Down"] = "Flecha abajo", ["PrintScreen"] = "Impr Pant", ["Apps"] = "tecla de menú" },
        ["it"] = new() { ["Shift"] = "Maiusc", ["Enter"] = "Invio", ["Escape"] = "Esc", ["Space"] = "Barra spaziatrice", ["Delete"] = "Canc", ["Insert"] = "Ins",
                         ["End"] = "Fine", ["PageUp"] = "Pag su", ["PageDown"] = "Pag giù", ["Left"] = "Freccia sinistra", ["Right"] = "Freccia destra",
                         ["Up"] = "Freccia su", ["Down"] = "Freccia giù", ["PrintScreen"] = "Stamp", ["Apps"] = "tasto Menu" },
        ["nl"] = new() { ["Escape"] = "Esc", ["Space"] = "Spatiebalk", ["Left"] = "Pijl-links", ["Right"] = "Pijl-rechts", ["Up"] = "Pijl-omhoog", ["Down"] = "Pijl-omlaag",
                         ["Apps"] = "Menutoets" },
    };

    public static string Key(string k, string lang)
    {
        if (lang == "de") return string.Join("+", k.Split('+').Select(p => KeyDe.TryGetValue(p, out var d) ? d : p));
        if (KeyMore.TryGetValue(lang, out var map)) return string.Join("+", k.Split('+').Select(p => map.TryGetValue(p, out var d) ? d : p));
        return k switch { "Escape" => "Esc", _ => k };
    }

    static string Q(string s, string lang) => OutText.Quote(s, lang);

    /// <summary>Text in der Sprache der Ausgabe (Deutsch, Englisch, Französisch, Spanisch, Italienisch, Niederländisch).</summary>
    static string L(string lang, string de, string en, string fr, string es, string it, string nl) => OutText.For(lang, de, en, fr, es, it, nl);

    static bool More(string lang) => lang is "fr" or "es" or "it" or "nl";

    static readonly (string art, string suffix)[] ItArticles = { ("il ", "l "), ("lo ", "llo "), ("la ", "lla "), ("l'", "ll'"), ("i ", "i "), ("gli ", "gli "), ("le ", "lle ") };

    /// <summary>Italienisch: Präposition mit Artikel verschmelzen („su“ + „il pulsante“ → „sul pulsante“).</summary>
    static string It(string prep, string label)
    {
        string stem = prep switch { "su" => "su", "in" => "ne", "a" => "a", "di" => "de", "da" => "da", _ => "" };
        if (stem.Length > 0)
            foreach (var (art, suffix) in ItArticles)
                if (label.StartsWith(art, StringComparison.Ordinal)) return stem + suffix + label[art.Length..];
        return prep + " " + label;
    }

    /// <summary>Französisch: „à le“ → „au“, „de le“ → „du“.</summary>
    static string Fr(string prep, string label)
    {
        if (prep is "à" or "de")
        {
            if (label.StartsWith("le ", StringComparison.Ordinal)) return (prep == "à" ? "au " : "du ") + label[3..];
            if (label.StartsWith("les ", StringComparison.Ordinal)) return (prep == "à" ? "aux " : "des ") + label[4..];
        }
        return prep + " " + label;
    }

    /// <summary>Spanisch: „a el“ → „al“, „de el“ → „del“.</summary>
    static string Es(string prep, string label)
        => prep is "a" or "de" && label.StartsWith("el ", StringComparison.Ordinal) ? (prep == "a" ? "al " : "del ") + label[3..] : prep + " " + label;

    public static string CleanName(string? n)
    {
        if (string.IsNullOrWhiteSpace(n)) return "";
        var s = n.Replace("\r", " ").Replace("\n", " ").Replace("&", "").Trim();
        if (s.EndsWith(':')) s = s[..^1].TrimEnd();
        return s.Length > 60 ? s[..59] + "…" : s;
    }

    /// <summary>
    /// Welche Stufe der Rückfallkette die Zielbezeichnung liefert: 1 Name, 2 Beschriftung, 3 Wert, 4 Hilfetext,
    /// 5 Vorfahre, 6 nur Position, 7 unbekannt (Qualitätsprüfung Q01).
    /// </summary>
    public static int TargetRule(JsonObject step, string which = "target")
    {
        var t = step.Obj(which);
        var el = t?.Obj("el");
        string ct = el.Str("ct") ?? "Unknown";
        if (CleanName(el.Str("name")).Length > 0) return 1;
        if (CleanName(el.Obj("labeledBy").Str("name")).Length > 0) return 2;
        if (el.Str("value") is { Length: > 0 } && ct is "ComboBox" or "Edit" or "Spinner") return 3;
        if (CleanName(el.Str("help")).Length > 0) return 4;
        if (el != null && (t?.Arr("path")?.Any(p => CleanName(p.Str("name")).Length > 0) ?? false)) return 5;
        return (which == "endTarget" ? step.Obj("endPosition") : step.Obj("position")) != null ? 6 : 7;
    }

    /// <summary>Bezeichnung des Ziels (Rückfallkette: Name → beschriftet durch → Wert → Hilfe → Vorfahre → Position).</summary>
    public static string TargetLabel(JsonObject step, string lang, bool article, string which = "target")
    {
        if (More(lang)) return TargetLabelMore(step, lang, which);
        var t = step.Obj(which);
        var el = t?.Obj("el");
        var ctx = step.Obj("ctx");
        string ct = el.Str("ct") ?? "Unknown";
        TypeWords.TryGetValue(ct, out var tw);
        string word = lang == "de" ? (article ? (tw.deArt ?? "das Element") : (tw.de ?? "Element")) : (tw.en ?? "element");
        if (tw == default) word = lang == "de" ? (article ? "das Element" : "Element") : "element";
        string name = CleanName(el.Str("name"));
        if (name.Length == 0) name = CleanName(el.Obj("labeledBy").Str("name"));
        if (name.Length > 0) return $"{word} {Q(name, lang)}";
        if (el.Str("value") is { Length: > 0 } v && ct is "ComboBox" or "Edit" or "Spinner")
            return lang == "de" ? $"{word} mit dem Wert {Q(CleanName(v), lang)}" : $"{word} with the value {Q(CleanName(v), lang)}";
        if (CleanName(el.Str("help")) is { Length: > 0 } h) return $"{word} ({h})";
        var path = t?.Arr("path");
        var anc = path?.Reverse().Select(p => CleanName(p.Str("name"))).FirstOrDefault(n => n.Length > 0);
        if (el != null && anc != null) return lang == "de" ? $"{word} im Bereich {Q(anc, lang)}" : $"{word} in {Q(anc, lang)}";
        var pos = which == "endTarget" ? step.Obj("endPosition") : step.Obj("position");
        string title = CleanName(ctx.Str("title"));
        if (pos != null)
            return lang == "de" ? $"die Stelle bei ({pos.Int("x")}, {pos.Int("y")}){(title.Length > 0 ? " im Fenster " + Q(title, lang) : "")}"
                                : $"the spot at ({pos.Int("x")}, {pos.Int("y")}){(title.Length > 0 ? " in the window " + Q(title, lang) : "")}";
        return lang == "de" ? "ein unbekanntes Element" : "an unknown element";
    }

    /// <summary>Zielbezeichnung auf Französisch, Spanisch, Italienisch, Niederländisch – immer mit Artikel („le bouton « OK »“).</summary>
    static string TargetLabelMore(JsonObject step, string lang, string which)
    {
        var t = step.Obj(which);
        var el = t?.Obj("el");
        var ctx = step.Obj("ctx");
        string ct = el.Str("ct") ?? "Unknown";
        string word = TypeWordsMore.TryGetValue(ct, out var tw) ? L(lang, "", "", tw.fr, tw.es, tw.it, tw.nl) : L(lang, "", "", "l'élément", "el elemento", "l'elemento", "het element");
        string name = CleanName(el.Str("name"));
        if (name.Length == 0) name = CleanName(el.Obj("labeledBy").Str("name"));
        if (name.Length > 0) return $"{word} {Q(name, lang)}";
        if (el.Str("value") is { Length: > 0 } v && ct is "ComboBox" or "Edit" or "Spinner")
            return word + L(lang, "", "", " avec la valeur ", " con el valor ", " con il valore ", " met de waarde ") + Q(CleanName(v), lang);
        if (CleanName(el.Str("help")) is { Length: > 0 } h) return $"{word} ({h})";
        var anc = t?.Arr("path")?.Reverse().Select(p => CleanName(p.Str("name"))).FirstOrDefault(n => n.Length > 0);
        if (el != null && anc != null) return word + L(lang, "", "", " dans la zone ", " en la zona ", " nell'area ", " in het gebied ") + Q(anc, lang);
        var pos = which == "endTarget" ? step.Obj("endPosition") : step.Obj("position");
        string title = CleanName(ctx.Str("title"));
        if (pos != null)
        {
            string at = $"({pos.Int("x")}, {pos.Int("y")})";
            return L(lang, "", "", $"l'emplacement {at}", $"el punto {at}", $"il punto {at}", $"de plek {at}")
                 + (title.Length > 0 ? L(lang, "", "", " dans la fenêtre ", " de la ventana ", " nella finestra ", " in het venster ") + Q(title, lang) : "");
        }
        return L(lang, "", "", "un élément inconnu", "un elemento desconocido", "un elemento sconosciuto", "een onbekend element");
    }

    /// <summary>Menüpfad „Datei › Speichern unter“ (nur bei mindestens zwei Ebenen).</summary>
    static string MenuPath(JsonObject step)
    {
        var items = step.Obj("menuPath")?.Arr("items")?.Select(x => CleanName(x?.GetValue<string>())).Where(x => x.Length > 0).ToList();
        return items is { Count: >= 2 } ? string.Join(" › ", items.TakeLast(4)) : "";
    }

    /// <summary>Gruppe im Menüband (z. B. „Schriftart“), wenn das Element in einer benannten Gruppe liegt.</summary>
    static string RibbonGroup(JsonObject step)
    {
        var path = step.Obj("target")?.Arr("path");
        if (path == null) return "";
        foreach (var p in path.Reverse().OfType<JsonObject>().Take(3))
            if (p.Str("ct") == "Group" && CleanName(p.Str("name")) is { Length: > 0 } g && g != N(step)) return g;
        return "";
    }

    static string N(JsonObject step) => CleanName(step.Obj("target").Obj("el").Str("name")) is { Length: > 0 } n ? n : CleanName(step.Obj("target").Obj("el").Obj("labeledBy").Str("name"));
    static string F(JsonObject step, string lang) => CleanName(step.Obj("ctx").Str("title")) is { Length: > 0 } t ? Q(t, lang) : L(lang, "(ohne Titel)", "(untitled)", "(sans titre)", "(sin título)", "(senza titolo)", "(zonder titel)");

    public static string Duration(long us, string lang)
    {
        var c = OutText.Culture(lang);
        if (us < 1_000_000) return $"{us / 1000} ms";
        if (us < 60_000_000) return (us / 1e6).ToString("0.0", c) + " s";
        var ts = TimeSpan.FromTicks(us * 10);
        return $"{(int)ts.TotalMinutes} min {ts.Seconds} s";
    }

    public static string CauseText(string cause, string lang) => cause switch
    {
        "paused" => L(lang, "Die Aufnahme war angehalten.", "Recording was paused.",
            "L'enregistrement était en pause.", "La grabación estaba en pausa.", "La registrazione era in pausa.", "De opname was gepauzeerd."),
        "privacyMode" => L(lang, "Privatmodus: Eingaben wurden nicht aufgezeichnet.", "Private mode: input was not recorded.",
            "Mode privé : les saisies n'ont pas été enregistrées.", "Modo privado: no se grabaron las entradas.", "Modalità privata: gli input non sono stati registrati.", "Privémodus: invoer is niet opgenomen."),
        "excludedApp" => L(lang, "Eine ausgeschlossene Anwendung war im Vordergrund; davon wurde nichts aufgezeichnet.", "An excluded application was in the foreground; nothing of it was recorded.",
            "Une application exclue était au premier plan ; rien n'en a été enregistré.", "Una aplicación excluida estaba en primer plano; no se grabó nada de ella.", "Un'applicazione esclusa era in primo piano; non ne è stato registrato nulla.", "Een uitgesloten toepassing stond op de voorgrond; daarvan is niets opgenomen."),
        "outOfScope" => L(lang, "Arbeit außerhalb des Aufnahmebereichs; nicht aufgezeichnet.", "Work outside the capture area; not recorded.",
            "Travail hors de la zone d'enregistrement ; non enregistré.", "Trabajo fuera del área de grabación; no se grabó.", "Attività fuori dall'area di registrazione; non registrata.", "Werk buiten het opnamegebied; niet opgenomen."),
        "secureDesktop" => L(lang, "Sicherer Desktop (z. B. Abfrage der Benutzerkontensteuerung): Eingaben und Bilder konnten nicht aufgezeichnet werden.", "Secure desktop (e.g. a User Account Control prompt): input and images could not be recorded.",
            "Bureau sécurisé (p. ex. une invite du Contrôle de compte d'utilisateur) : les saisies et les images n'ont pas pu être enregistrées.", "Escritorio seguro (p. ej., un aviso del Control de cuentas de usuario): no se pudieron grabar las entradas ni las imágenes.",
            "Desktop sicuro (ad es. una richiesta di Controllo account utente): non è stato possibile registrare input e immagini.", "Beveiligd bureaublad (bijv. een melding van Gebruikersaccountbeheer): invoer en afbeeldingen konden niet worden opgenomen."),
        "sessionLocked" or "sessionDisconnected" or "suspend" => L(lang, "Der Rechner war gesperrt, getrennt oder im Ruhezustand.", "The computer was locked, disconnected or asleep.",
            "L'ordinateur était verrouillé, déconnecté ou en veille.", "El equipo estaba bloqueado, desconectado o en suspensión.", "Il computer era bloccato, disconnesso o in sospensione.", "De computer was vergrendeld, losgekoppeld of in de slaapstand."),
        "elevatedWindow" => L(lang, "Ein Fenster mit Administratorrechten war im Vordergrund: Eingaben waren nicht sichtbar, Bilder wurden aufgezeichnet.", "A window with administrator rights was in the foreground: input was not visible, images were recorded.",
            "Une fenêtre avec des droits d'administrateur était au premier plan : les saisies n'étaient pas visibles, les images ont été enregistrées.", "Una ventana con derechos de administrador estaba en primer plano: las entradas no eran visibles; las imágenes sí se grabaron.",
            "Una finestra con diritti di amministratore era in primo piano: gli input non erano visibili, le immagini sono state registrate.", "Een venster met beheerdersrechten stond op de voorgrond: invoer was niet zichtbaar, afbeeldingen zijn opgenomen."),
        "hookLost" or "inputMismatch" or "overload" => L(lang, "Eingaben wurden möglicherweise unvollständig aufgezeichnet.", "Input may have been recorded incompletely.",
            "Les saisies ont peut-être été enregistrées de façon incomplète.", "Es posible que las entradas se grabaran de forma incompleta.", "Gli input potrebbero essere stati registrati in modo incompleto.", "Invoer is mogelijk onvolledig opgenomen."),
        "captureLost" or "frameDropped" => L(lang, "Für diesen Zeitraum fehlen Bilder.", "Images are missing for this period.",
            "Des images manquent pour cette période.", "Faltan imágenes de este periodo.", "Mancano immagini per questo periodo.", "Voor deze periode ontbreken afbeeldingen."),
        "protectedContent" => L(lang, "Geschützter Inhalt wurde vom System ausgeblendet.", "Protected content was hidden by the system.",
            "Un contenu protégé a été masqué par le système.", "El sistema ocultó contenido protegido.", "Il sistema ha nascosto contenuti protetti.", "Beveiligde inhoud is door het systeem verborgen."),
        "diskFull" => L(lang, "Der Datenträger war voll; die Aufnahme war angehalten.", "The disk was full; recording was paused.",
            "Le disque était plein ; l'enregistrement était en pause.", "El disco estaba lleno; la grabación estaba en pausa.", "Il disco era pieno; la registrazione era in pausa.", "De schijf was vol; de opname was gepauzeerd."),
        "crash" => L(lang, "Der Recorder wurde unerwartet beendet; ab hier fehlt die Aufzeichnung.", "The recorder ended unexpectedly; the recording is missing from here on.",
            "L'enregistreur s'est arrêté de façon inattendue ; l'enregistrement manque à partir d'ici.", "El grabador se cerró inesperadamente; a partir de aquí falta la grabación.", "Il registratore si è chiuso in modo imprevisto; da qui in poi la registrazione manca.", "De recorder is onverwacht gestopt; vanaf hier ontbreekt de opname."),
        "unsupportedInput" => L(lang, "Eine nicht aufzeichenbare Eingabe (z. B. Mehrfinger-Geste) wurde erkannt.", "An input that cannot be recorded (e.g. a multi-finger gesture) was detected.",
            "Une saisie impossible à enregistrer (p. ex. un geste à plusieurs doigts) a été détectée.", "Se detectó una entrada que no se puede grabar (p. ej., un gesto con varios dedos).", "È stato rilevato un input non registrabile (ad es. un gesto con più dita).", "Er is invoer gedetecteerd die niet kan worden opgenomen (bijv. een gebaar met meerdere vingers)."),
        "relativeMouse" => L(lang, "Der Mauszeiger war von der Anwendung gefangen; Positionen sind ohne Aussage.", "The pointer was captured by the application; positions are meaningless.",
            "Le pointeur était capturé par l'application ; les positions ne sont pas significatives.", "La aplicación había capturado el puntero; las posiciones no son significativas.", "Il puntatore era catturato dall'applicazione; le posizioni non sono significative.", "De aanwijzer was door de toepassing vastgehouden; posities zijn niet zinvol."),
        "maskedInput" => L(lang, "Eine Tasteneingabe wurde bewusst nicht aufgezeichnet.", "A keyboard input was deliberately not recorded.",
            "Une saisie au clavier n'a délibérément pas été enregistrée.", "Una entrada de teclado no se grabó deliberadamente.", "Un input da tastiera non è stato registrato deliberatamente.", "Een toetsenbordinvoer is bewust niet opgenomen."),
        "unobserved" => L(lang, "Die Ursache wurde nicht beobachtet.", "The cause was not observed.",
            "La cause n'a pas été observée.", "No se observó la causa.", "La causa non è stata osservata.", "De oorzaak is niet waargenomen."),
        "removedByAuthor" => L(lang, "Dieser Zeitraum wurde vom Autor entfernt.", "This period was removed by the author.",
            "Cette période a été supprimée par l'auteur.", "El autor eliminó este periodo.", "Questo periodo è stato rimosso dall'autore.", "Deze periode is door de auteur verwijderd."),
        _ => L(lang, "Die Aufzeichnung ist hier unvollständig.", "The recording is incomplete here.",
            "L'enregistrement est incomplet ici.", "La grabación está incompleta aquí.", "La registrazione qui è incompleta.", "De opname is hier onvolledig.")
    };

    public static string ShortCause(string cause, string lang) => cause switch
    {
        "paused" => L(lang, "Pause", "pause", "pause", "pausa", "pausa", "pauze"),
        "privacyMode" => L(lang, "Privatmodus", "private mode", "mode privé", "modo privado", "modalità privata", "privémodus"),
        "excludedApp" => L(lang, "ausgeschlossene Anwendung", "excluded application", "application exclue", "aplicación excluida", "applicazione esclusa", "uitgesloten toepassing"),
        "outOfScope" => L(lang, "außerhalb des Aufnahmebereichs", "outside capture area", "hors de la zone d'enregistrement", "fuera del área de grabación", "fuori dall'area di registrazione", "buiten het opnamegebied"),
        "secureDesktop" => L(lang, "sicherer Desktop", "secure desktop", "bureau sécurisé", "escritorio seguro", "desktop sicuro", "beveiligd bureaublad"),
        "sessionLocked" => L(lang, "Rechner gesperrt", "computer locked", "ordinateur verrouillé", "equipo bloqueado", "computer bloccato", "computer vergrendeld"),
        "elevatedWindow" => L(lang, "Administratorfenster", "administrator window", "fenêtre administrateur", "ventana de administrador", "finestra di amministratore", "beheerdersvenster"),
        "removedByAuthor" => L(lang, "vom Autor entfernt", "removed by author", "supprimé par l'auteur", "eliminado por el autor", "rimosso dall'autore", "verwijderd door de auteur"),
        "crash" => L(lang, "Absturz", "crash", "plantage", "fallo", "arresto anomalo", "crash"),
        "diskFull" => L(lang, "Datenträger voll", "disk full", "disque plein", "disco lleno", "disco pieno", "schijf vol"),
        _ => L(lang, "unvollständig", "incomplete", "incomplet", "incompleto", "incompleto", "onvolledig")
    };

    /// <summary>Getippte Token in Notation: <c>notiz.txr{Backspace}t</c>, <c>{masked:12}</c>, <c>{Enter}</c>.</summary>
    public static string TokensNotation(JsonObject input)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var tok in input.Arr("tokens") ?? new JsonArray())
        {
            if (tok is JsonValue v && v.TryGetValue<string>(out var s)) sb.Append(s.Replace("{", "{{").Replace("}", "}}"));
            else if (tok is JsonObject o)
            {
                if (o.ContainsKey("masked")) sb.Append(o["masked"] is null ? "{masked}" : $"{{masked:{o.Long("masked")}}}");
                else if (o.Str("placeholder") is { } p) sb.Append('{').Append(p).Append('}');
                else if (o.Str("uncertain") is { } u) sb.Append($"{{uncertain:{u}}}");
                else if (o.Str("unreviewed") is { } ur) sb.Append($"{{unreviewed:{ur}}}");
                else if (o.Str("key") is { } k)
                {
                    var mods = o.StrList("mods");
                    string name = (mods.Count > 0 ? string.Join("+", mods.Select(Cap)) + "+" : "") + k;
                    for (int i = 0; i < Math.Max(1, o.Int("n", 1)); i++) sb.Append('{').Append(name).Append('}');
                }
            }
        }
        if (input.Obj("terminator") is { } term) sb.Append('{').Append(term.Str("key")).Append('}');
        return sb.ToString();
    }

    static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Titel (≤60 Zeichen) eines Schritts.</summary>
    public static string Title(JsonObject step, string lang)
    {
        string kind = step.Str("kind") ?? "";
        var input = step.Obj("input") ?? new JsonObject();
        string n = N(step);
        string qn = Q(n, lang);
        string ct = step.Obj("target").Obj("el").Str("ct") ?? "";
        string combo = input.Str("combo") ?? "", key = input.Str("key") ?? "";
        string t = kind switch
        {
            "click" or "doubleClick" or "tripleClick" or "rightClick" when n.Length == 0 => L(lang, $"Klick im Fenster {F(step, lang)}", $"Click in window {F(step, lang)}",
                $"Clic dans la fenêtre {F(step, lang)}", $"Clic en la ventana {F(step, lang)}", $"Clic nella finestra {F(step, lang)}", $"Klik in venster {F(step, lang)}"),
            "click" when input.Bool("longPress") && step.Obj("input").StrList("mods").Count == 0 => L(lang, $"{qn} gedrückt halten", $"Press and hold {qn}",
                $"Maintenir {qn} enfoncé", $"Mantener pulsado {qn}", $"Tenere premuto {qn}", $"{qn} ingedrukt houden"),
            "click" when MenuPath(step) is { Length: > 0 } mp => L(lang, $"{Q(mp, lang)} wählen", $"Choose {Q(mp, lang)}",
                $"Choisir {Q(mp, lang)}", $"Elegir {Q(mp, lang)}", $"Scegliere {Q(mp, lang)}", $"{Q(mp, lang)} kiezen"),
            "click" => ct switch
            {
                "CheckBox" => step.Obj("target").Obj("el").Str("toggle") == "on"
                    ? L(lang, $"{qn} deaktivieren", $"Clear {qn}", $"Décocher {qn}", $"Desactivar {qn}", $"Deselezionare {qn}", $"{qn} uitschakelen")
                    : L(lang, $"{qn} aktivieren", $"Select {qn}", $"Cocher {qn}", $"Activar {qn}", $"Selezionare {qn}", $"{qn} inschakelen"),
                "ComboBox" => L(lang, $"{qn} öffnen", $"Open {qn}", $"Ouvrir {qn}", $"Abrir {qn}", $"Aprire {qn}", $"{qn} openen"),
                "ListItem" or "TreeItem" or "DataItem" or "RadioButton" => L(lang, $"{qn} auswählen", $"Select {qn}", $"Sélectionner {qn}", $"Seleccionar {qn}", $"Selezionare {qn}", $"{qn} selecteren"),
                "MenuItem" => L(lang, $"Menüpunkt {qn} wählen", $"Choose {qn}", $"Choisir {qn}", $"Elegir {qn}", $"Scegliere {qn}", $"{qn} kiezen"),
                "TabItem" => L(lang, $"Zu {qn} wechseln", $"Switch to {qn}", $"Passer à {qn}", $"Cambiar a {qn}", $"Passare a {qn}", $"Overschakelen naar {qn}"),
                "Edit" or "Document" => L(lang, $"In {qn} klicken", $"Click in {qn}", $"Cliquer dans {qn}", $"Hacer clic en {qn}", $"Fare clic in {qn}", $"In {qn} klikken"),
                _ => L(lang, $"{qn} anklicken", $"Click {qn}", $"Cliquer sur {qn}", $"Hacer clic en {qn}", $"Fare clic su {qn}", $"Op {qn} klikken")
            },
            "doubleClick" => L(lang, $"Doppelklick auf {qn}", $"Double-click {qn}", $"Double-cliquer sur {qn}", $"Doble clic en {qn}", $"Doppio clic su {qn}", $"Dubbelklikken op {qn}"),
            "tripleClick" => L(lang, $"Dreifachklick auf {qn}", $"Triple-click {qn}", $"Triple-clic sur {qn}", $"Triple clic en {qn}", $"Triplo clic su {qn}", $"Driemaal klikken op {qn}"),
            "rightClick" => L(lang, $"Rechtsklick auf {qn}", $"Right-click {qn}", $"Clic droit sur {qn}", $"Clic derecho en {qn}", $"Clic destro su {qn}", $"Rechtsklikken op {qn}"),
            "drag" => input.Str("dragKind") switch
            {
                "selectText" => L(lang, "Text markieren", "Select text", "Sélectionner du texte", "Seleccionar texto", "Selezionare testo", "Tekst selecteren"),
                "moveWindow" => L(lang, "Fenster verschieben", "Move window", "Déplacer la fenêtre", "Mover la ventana", "Spostare la finestra", "Venster verplaatsen"),
                "resizeWindow" => L(lang, "Fenstergröße ändern", "Resize window", "Redimensionner la fenêtre", "Cambiar el tamaño de la ventana", "Ridimensionare la finestra", "Venstergrootte wijzigen"),
                _ => n.Length > 0 ? L(lang, $"{qn} ziehen", $"Drag {qn}", $"Faire glisser {qn}", $"Arrastrar {qn}", $"Trascinare {qn}", $"{qn} slepen")
                                  : L(lang, "Ziehen", "Drag", "Faire glisser", "Arrastrar", "Trascinare", "Slepen")
            },
            "scroll" => input.Str("semantic") == "zoom" ? L(lang, "Ansicht zoomen", "Zoom view", "Zoomer l'affichage", "Hacer zoom en la vista", "Zoom della vista", "Weergave zoomen")
                      : n.Length > 0 ? L(lang, $"In {qn} scrollen", $"Scroll in {qn}", $"Faire défiler {qn}", $"Desplazarse en {qn}", $"Scorrere in {qn}", $"In {qn} scrollen")
                      : L(lang, "Scrollen", "Scroll", "Faire défiler", "Desplazarse", "Scorrere", "Scrollen"),
            "type" when input.Bool("masked") => input.Str("maskReason") is "privacyMode" or "strictUnknown" or "strictPlaintext"
                ? L(lang, "Vertrauliche Eingabe", "Confidential input", "Saisie confidentielle", "Entrada confidencial", "Immissione riservata", "Vertrouwelijke invoer")
                : L(lang, "Kennwort eingeben", "Enter password", "Saisir le mot de passe", "Escribir la contraseña", "Immettere la password", "Wachtwoord invoeren"),
            "type" when input.Str("text") is { Length: > 0 and <= 30 } tx && !tx.Contains('\n') => L(lang, $"{Q(tx, lang)} eingeben", $"Type {Q(tx, lang)}",
                $"Saisir {Q(tx, lang)}", $"Escribir {Q(tx, lang)}", $"Digitare {Q(tx, lang)}", $"{Q(tx, lang)} typen"),
            "type" => n.Length > 0 ? L(lang, $"Text in {qn} eingeben", $"Enter text in {qn}", $"Saisir du texte dans {qn}", $"Escribir texto en {qn}", $"Immettere testo in {qn}", $"Tekst invoeren in {qn}")
                                   : L(lang, "Text eingeben", "Enter text", "Saisir du texte", "Escribir texto", "Immettere testo", "Tekst invoeren"),
            "hotkey" => L(lang, $"{Key(combo, lang)} drücken", $"Press {input.Str("combo")}",
                $"Appuyer sur {Key(combo, lang)}", $"Pulsar {Key(combo, lang)}", $"Premere {Key(combo, lang)}", $"Op {Key(combo, lang)} drukken"),
            "key" => input.Int("count", 1) > 1
                ? L(lang, $"{Key(key, lang)} {input.Int("count")}-mal drücken", $"Press {input.Str("key")} {input.Int("count")} times",
                    $"Appuyer {input.Int("count")} fois sur {Key(key, lang)}", $"Pulsar {Key(key, lang)} {input.Int("count")} veces", $"Premere {Key(key, lang)} {input.Int("count")} volte", $"{input.Int("count")} keer op {Key(key, lang)} drukken")
                : L(lang, $"{Key(key, lang)} drücken", $"Press {input.Str("key")}", $"Appuyer sur {Key(key, lang)}", $"Pulsar {Key(key, lang)}", $"Premere {Key(key, lang)}", $"Op {Key(key, lang)} drukken"),
            "hover" => L(lang, $"Auf {Q(n.Length > 0 ? n : "?", lang)} zeigen", $"Point to {Q(n.Length > 0 ? n : "?", lang)}",
                $"Pointer {Q(n.Length > 0 ? n : "?", lang)}", $"Señalar {Q(n.Length > 0 ? n : "?", lang)}", $"Puntare su {Q(n.Length > 0 ? n : "?", lang)}", $"Naar {Q(n.Length > 0 ? n : "?", lang)} wijzen"),
            "move" => L(lang, "Mausbewegung", "Mouse movement", "Mouvement de la souris", "Movimiento del ratón", "Movimento del mouse", "Muisbeweging"),
            "windowSwitch" => L(lang, $"Zu {WinTitle(step, input.Str("to"), lang)} wechseln", $"Switch to {WinTitle(step, input.Str("to"), lang)}",
                $"Passer à {WinTitle(step, input.Str("to"), lang)}", $"Cambiar a {WinTitle(step, input.Str("to"), lang)}", $"Passare a {WinTitle(step, input.Str("to"), lang)}", $"Overschakelen naar {WinTitle(step, input.Str("to"), lang)}"),
            "system" => input.Str("event") switch
            {
                "windowOpened" => L(lang, $"{F(step, lang)} erscheint", $"{F(step, lang)} appears", $"{F(step, lang)} s'affiche", $"Aparece {F(step, lang)}", $"Compare {F(step, lang)}", $"{F(step, lang)} verschijnt"),
                "windowClosed" => L(lang, $"{F(step, lang)} schließt sich", $"{F(step, lang)} closes", $"{F(step, lang)} se ferme", $"Se cierra {F(step, lang)}", $"Si chiude {F(step, lang)}", $"{F(step, lang)} wordt gesloten"),
                "foregroundChanged" => L(lang, $"{F(step, lang)} im Vordergrund", $"{F(step, lang)} in front", $"{F(step, lang)} au premier plan", $"{F(step, lang)} en primer plano", $"{F(step, lang)} in primo piano", $"{F(step, lang)} op de voorgrond"),
                "titleChanged" => L(lang, "Fenstertitel ändert sich", "Window title changes", "Le titre de la fenêtre change", "Cambia el título de la ventana", "Cambia il titolo della finestra", "Venstertitel verandert"),
                "valueChanged" => L(lang, "Inhalt ändert sich", "Content changes", "Le contenu change", "Cambia el contenido", "Cambia il contenuto", "Inhoud verandert"),
                "notification" => L(lang, "Mitteilung", "Notification", "Notification", "Notificación", "Notifica", "Melding"),
                "topologyChanged" => L(lang, "Monitor-Anordnung geändert", "Monitor layout changed", "Disposition des écrans modifiée", "Disposición de monitores cambiada", "Disposizione dei monitor modificata", "Monitorindeling gewijzigd"),
                "scopeChanged" => L(lang, "Aufnahmebereich gewechselt", "Capture area changed", "Zone d'enregistrement changée", "Área de grabación cambiada", "Area di registrazione cambiata", "Opnamegebied gewijzigd"),
                "appExit" => ProgramTitle(input, lang),
                "appHang" => L(lang, $"{Prog(input, lang)} reagiert nicht", $"{Prog(input, lang)} is not responding",
                    $"{Prog(input, lang)} ne répond pas", $"{Prog(input, lang)} no responde", $"{Prog(input, lang)} non risponde", $"{Prog(input, lang)} reageert niet"),
                "appEventLog" => L(lang, $"Fehlermeldung von Windows zu {Prog(input, lang)}", $"Windows error report for {Prog(input, lang)}",
                    $"Rapport d'erreurs Windows pour {Prog(input, lang)}", $"Informe de errores de Windows para {Prog(input, lang)}", $"Segnalazione errori di Windows per {Prog(input, lang)}", $"Windows-foutrapport voor {Prog(input, lang)}"),
                _ => L(lang, "Systemereignis", "System event", "Événement système", "Evento del sistema", "Evento di sistema", "Systeemgebeurtenis")
            },
            "wait" => L(lang, "Warten", "Wait", "Attendre", "Esperar", "Attendere", "Wachten"),
            "comment" => L(lang, "Kommentar", "Comment", "Commentaire", "Comentario", "Commento", "Opmerking"),
            "snapshot" => L(lang, "Schnappschuss", "Snapshot", "Instantané", "Instantánea", "Istantanea", "Momentopname"),
            "marker" => input.Str("markerKind") switch
            {
                "error" => L(lang, "Fehler tritt auf", "Error occurs", "Une erreur se produit", "Se produce un error", "Si verifica un errore", "Er treedt een fout op"),
                "expected" => L(lang, "Soll-Zustand", "Expected state", "État attendu", "Estado esperado", "Stato previsto", "Verwachte toestand"),
                "important" => L(lang, "Wichtige Stelle", "Important point", "Point important", "Punto importante", "Punto importante", "Belangrijk punt"),
                _ => L(lang, "Marker", "Marker", "Repère", "Marcador", "Marcatore", "Markering")
            },
            "gap" when input.Str("cause") == "secureDesktop" && input.Str("desktopKind") == "uac" => L(lang, "Benutzerkontensteuerung bestätigen", "Confirm User Account Control",
                "Confirmer le Contrôle de compte d'utilisateur", "Confirmar el Control de cuentas de usuario", "Confermare Controllo account utente", "Gebruikersaccountbeheer bevestigen"),
            "gap" => L(lang, "Lücke: ", "Gap: ", "Lacune : ", "Interrupción: ", "Interruzione: ", "Onderbreking: ") + ShortCause(input.Str("cause") ?? "unknown", lang),
            _ => kind
        };
        return t.Length > 60 ? t[..59] + "…" : t;
    }

    /// <summary>Titel des Zielfensters (bei Fensterwechseln) – sonst das Fenster des Schritts.</summary>
    static string WinTitle(JsonObject step, string? win, string lang)
        => CleanName(step.Obj("input")?.Str("toTitle")) is { Length: > 0 } t ? Q(t, lang) : F(step, lang);

    /// <summary>Text eines Schritts. style: "protocol" oder "guide".</summary>
    public static string Body(JsonObject step, string lang, string style = "guide", string address = "formal", string? commentText = null)
    {
        bool de = lang == "de", guide = style == "guide";
        string kind = step.Str("kind") ?? "";
        var input = step.Obj("input") ?? new JsonObject();
        var mods = input.StrList("mods");
        string Z(bool art = true) => TargetLabel(step, lang, art);
        bool posOnly = step.Obj("target").Str("status") is "position" or "none" || step.Obj("target").Obj("el") == null;
        string modText = string.Join("+", mods.Select(m => Key(Cap(m), lang)));
        string ct = step.Obj("target").Obj("el").Str("ct") ?? "";
        string f = F(step, lang);
        string combo = input.Str("combo") ?? "";
        string r = kind switch
        {
            "click" or "doubleClick" or "tripleClick" or "rightClick" when posOnly && step.Obj("position") is { } p =>
                guide ? L(lang, $"Klicken Sie im Fenster {f} auf die im Bild markierte Stelle.", $"In the window {f}, click the spot marked in the image.",
                          $"Dans la fenêtre {f}, cliquez sur l'emplacement marqué dans l'image.", $"En la ventana {f}, haga clic en el punto marcado en la imagen.",
                          $"Nella finestra {f}, fare clic sul punto evidenziato nell'immagine.", $"Klik in het venster {f} op de plek die in de afbeelding is gemarkeerd.")
                      : L(lang, $"Linksklick an Position ({p.Int("x")}, {p.Int("y")}) im Fenster {f}", $"Left click at position ({p.Int("x")}, {p.Int("y")}) in window {f}",
                          $"Clic gauche à la position ({p.Int("x")}, {p.Int("y")}) dans la fenêtre {f}", $"Clic izquierdo en la posición ({p.Int("x")}, {p.Int("y")}) de la ventana {f}",
                          $"Clic sinistro nella posizione ({p.Int("x")}, {p.Int("y")}) della finestra {f}", $"Linkerklik op positie ({p.Int("x")}, {p.Int("y")}) in venster {f}"),
            "click" when guide && mods.Count == 0 && ct == "CheckBox" => step.Obj("target").Obj("el").Str("toggle") == "on"
                ? L(lang, $"Deaktivieren Sie {Z()}.", $"Clear {Z()}.", $"Décochez {Z()}.", $"Desactive {Z()}.", $"Deselezionare {Z()}.", $"Schakel {Z()} uit.")
                : L(lang, $"Aktivieren Sie {Z()}.", $"Select {Z()}.", $"Cochez {Z()}.", $"Active {Z()}.", $"Selezionare {Z()}.", $"Schakel {Z()} in."),
            "click" when guide && mods.Count == 0 && MenuPath(step) is { Length: > 0 } mp => L(lang, $"Wählen Sie im Menü {Q(mp, lang)}.", $"From the menu, choose {Q(mp, lang)}.",
                $"Dans le menu, choisissez {Q(mp, lang)}.", $"En el menú, elija {Q(mp, lang)}.", $"Nel menu, scegliere {Q(mp, lang)}.", $"Kies in het menu {Q(mp, lang)}."),
            "click" when guide && mods.Count == 0 && ct is "Button" or "SplitButton" && RibbonGroup(step) is { Length: > 0 } rg && !posOnly => L(lang, $"Klicken Sie in der Gruppe {Q(rg, lang)} auf {Z()}.", $"In the {Q(rg, lang)} group, click {Z()}.",
                $"Dans le groupe {Q(rg, lang)}, cliquez sur {Z()}.", $"En el grupo {Q(rg, lang)}, haga clic en {Z()}.", $"Nel gruppo {Q(rg, lang)}, fare clic {It("su", Z())}.", $"Klik in de groep {Q(rg, lang)} op {Z()}."),
            "click" when guide && mods.Count == 0 && ct is "RadioButton" or "MenuItem" => L(lang, $"Wählen Sie {Z()}.", $"Choose {Z()}.", $"Choisissez {Z()}.", $"Elija {Z()}.", $"Scegliere {Z()}.", $"Kies {Z()}."),
            "click" when guide && mods.Count == 0 && ct is "ListItem" or "TreeItem" or "DataItem" => L(lang, $"Wählen Sie {Z()} aus.", $"Select {Z()}.", $"Sélectionnez {Z()}.", $"Seleccione {Z()}.", $"Selezionare {Z()}.", $"Selecteer {Z()}."),
            "click" when guide && mods.Count == 0 && ct == "TabItem" => L(lang, $"Wechseln Sie zu {Z()}.", $"Switch to {Z()}.", $"Passez {Fr("à", Z())}.", $"Cambie {Es("a", Z())}.", $"Passare {It("a", Z())}.", $"Ga naar {Z()}."),
            "click" when guide && mods.Count == 0 && ct == "ComboBox" => L(lang, $"Öffnen Sie {Z()}.", $"Open {Z()}.", $"Ouvrez {Z()}.", $"Abra {Z()}.", $"Aprire {Z()}.", $"Open {Z()}."),
            "click" when guide && mods.Count == 0 && ct is "Edit" or "Document" => L(lang, $"Klicken Sie in {Z()}.", $"Click in {Z()}.", $"Cliquez dans {Z()}.", $"Haga clic en {Z()}.", $"Fare clic {It("in", Z())}.", $"Klik in {Z()}."),
            "click" when mods.Count == 0 && input.Bool("longPress") && !posOnly => guide
                ? L(lang, $"Halten Sie die Maustaste auf {Z()} etwa {Seconds(input.Long("pressUs"), lang)} gedrückt.", $"Press and hold {Z()} for about {Seconds(input.Long("pressUs"), lang)}.",
                    $"Maintenez le bouton de la souris enfoncé sur {Z()} pendant environ {Seconds(input.Long("pressUs"), lang)}.", $"Mantenga pulsado el botón del ratón sobre {Z()} durante unos {Seconds(input.Long("pressUs"), lang)}.",
                    $"Tenere premuto il pulsante del mouse {It("su", Z())} per circa {Seconds(input.Long("pressUs"), lang)}.", $"Houd de muisknop op {Z()} ongeveer {Seconds(input.Long("pressUs"), lang)} ingedrukt.")
                : L(lang, $"Linke Maustaste {Seconds(input.Long("pressUs"), lang)} gedrückt gehalten auf {Z(false)}", $"Left button held for {Seconds(input.Long("pressUs"), lang)} on {Z(false)}",
                    $"Bouton gauche maintenu {Seconds(input.Long("pressUs"), lang)} sur {Z(false)}", $"Botón izquierdo mantenido {Seconds(input.Long("pressUs"), lang)} sobre {Z(false)}",
                    $"Pulsante sinistro tenuto premuto per {Seconds(input.Long("pressUs"), lang)} {It("su", Z(false))}", $"Linkerknop {Seconds(input.Long("pressUs"), lang)} ingedrukt gehouden op {Z(false)}"),
            "click" when mods.Count > 0 => guide
                ? L(lang, $"Halten Sie {modText} gedrückt und klicken Sie auf {Z()}.", $"Hold {modText} and click {Z()}.",
                    $"Maintenez la touche {modText} enfoncée et cliquez sur {Z()}.", $"Mantenga pulsada la tecla {modText} y haga clic en {Z()}.", $"Tenere premuto {modText} e fare clic {It("su", Z())}.", $"Houd {modText} ingedrukt en klik op {Z()}.")
                : L(lang, $"{modText}+Linksklick auf {Z(false)}", $"{modText}+left click on {Z(false)}",
                    $"{modText}+clic gauche sur {Z(false)}", $"{modText}+clic izquierdo en {Z(false)}", $"{modText}+clic sinistro {It("su", Z(false))}", $"{modText}+linkerklik op {Z(false)}"),
            "click" => guide
                ? L(lang, $"Klicken Sie auf {Z()}.", $"Click {Z()}.", $"Cliquez sur {Z()}.", $"Haga clic en {Z()}.", $"Fare clic {It("su", Z())}.", $"Klik op {Z()}.")
                : L(lang, $"Linksklick auf {Z(false)}", $"Left click on {Z(false)}", $"Clic gauche sur {Z(false)}", $"Clic izquierdo en {Z(false)}", $"Clic sinistro {It("su", Z(false))}", $"Linkerklik op {Z(false)}"),
            "doubleClick" => guide
                ? L(lang, $"Doppelklicken Sie auf {Z()}.", $"Double-click {Z()}.", $"Double-cliquez sur {Z()}.", $"Haga doble clic en {Z()}.", $"Fare doppio clic {It("su", Z())}.", $"Dubbelklik op {Z()}.")
                : L(lang, $"Doppelklick auf {Z(false)}", $"Double click on {Z(false)}", $"Double-clic sur {Z(false)}", $"Doble clic en {Z(false)}", $"Doppio clic {It("su", Z(false))}", $"Dubbelklik op {Z(false)}"),
            "tripleClick" => guide
                ? L(lang, $"Klicken Sie dreimal schnell auf {Z()}.", $"Triple-click {Z()}.", $"Cliquez trois fois rapidement sur {Z()}.", $"Haga triple clic en {Z()}.", $"Fare triplo clic {It("su", Z())}.", $"Klik driemaal snel op {Z()}.")
                : L(lang, $"Dreifachklick auf {Z(false)}", $"Triple click on {Z(false)}", $"Triple-clic sur {Z(false)}", $"Triple clic en {Z(false)}", $"Triplo clic {It("su", Z(false))}", $"Driemaal klikken op {Z(false)}"),
            "rightClick" => guide
                ? L(lang, $"Klicken Sie mit der rechten Maustaste auf {Z()}.", $"Right-click {Z()}.", $"Cliquez avec le bouton droit sur {Z()}.", $"Haga clic con el botón derecho en {Z()}.", $"Fare clic con il pulsante destro {It("su", Z())}.", $"Klik met de rechtermuisknop op {Z()}.")
                : L(lang, $"Rechtsklick auf {Z(false)}", $"Right click on {Z(false)}", $"Clic droit sur {Z(false)}", $"Clic derecho en {Z(false)}", $"Clic destro {It("su", Z(false))}", $"Rechtsklik op {Z(false)}"),
            "drag" => input.Str("dragKind") switch
            {
                "selectText" when input.Str("selectedText") is { } st => guide
                    ? L(lang, $"Markieren Sie den Text {Q(st, lang)}.", $"Select the text {Q(st, lang)}.", $"Sélectionnez le texte {Q(st, lang)}.", $"Seleccione el texto {Q(st, lang)}.", $"Selezionare il testo {Q(st, lang)}.", $"Selecteer de tekst {Q(st, lang)}.")
                    : L(lang, $"Text markiert durch Ziehen: {Q(st, lang)}", $"Text selected by dragging: {Q(st, lang)}", $"Texte sélectionné par glissement : {Q(st, lang)}", $"Texto seleccionado arrastrando: {Q(st, lang)}", $"Testo selezionato trascinando: {Q(st, lang)}", $"Tekst geselecteerd door slepen: {Q(st, lang)}"),
                "moveWindow" => guide
                    ? L(lang, $"Verschieben Sie das Fenster {f}.", $"Move the window {f}.", $"Déplacez la fenêtre {f}.", $"Mueva la ventana {f}.", $"Spostare la finestra {f}.", $"Verplaats het venster {f}.")
                    : L(lang, $"Fenster {f} verschoben", $"Window {f} moved", $"Fenêtre {f} déplacée", $"Ventana {f} movida", $"Finestra {f} spostata", $"Venster {f} verplaatst"),
                "resizeWindow" => guide
                    ? L(lang, $"Ändern Sie die Größe des Fensters {f}.", $"Resize the window {f}.", $"Redimensionnez la fenêtre {f}.", $"Cambie el tamaño de la ventana {f}.", $"Ridimensionare la finestra {f}.", $"Wijzig de grootte van het venster {f}.")
                    : L(lang, $"Fenster {f} in der Größe geändert", $"Window {f} resized", $"Fenêtre {f} redimensionnée", $"Tamaño de la ventana {f} cambiado", $"Finestra {f} ridimensionata", $"Grootte van venster {f} gewijzigd"),
                _ => guide
                    ? L(lang, $"Ziehen Sie {Z()} auf {TargetLabel(step, lang, true, "endTarget")}.", $"Drag {Z()} onto {TargetLabel(step, lang, true, "endTarget")}.",
                        $"Faites glisser {Z()} sur {TargetLabel(step, lang, true, "endTarget")}.", $"Arrastre {Z()} hasta {TargetLabel(step, lang, true, "endTarget")}.",
                        $"Trascinare {Z()} {It("su", TargetLabel(step, lang, true, "endTarget"))}.", $"Sleep {Z()} naar {TargetLabel(step, lang, true, "endTarget")}.")
                    : L(lang, $"Ziehen von {Z(false)} nach {TargetLabel(step, lang, false, "endTarget")}", $"Drag from {Z(false)} to {TargetLabel(step, lang, false, "endTarget")}",
                        $"Glissement {Fr("de", Z(false))} vers {TargetLabel(step, lang, false, "endTarget")}", $"Arrastre desde {Z(false)} hasta {TargetLabel(step, lang, false, "endTarget")}",
                        $"Trascinamento {It("da", Z(false))} {It("a", TargetLabel(step, lang, false, "endTarget"))}", $"Slepen van {Z(false)} naar {TargetLabel(step, lang, false, "endTarget")}")
            },
            "scroll" when input.Str("semantic") == "zoom" => guide
                ? L(lang, "Vergrößern oder verkleinern Sie die Ansicht mit Strg+Mausrad.", "Zoom with Ctrl+mouse wheel.", "Zoomez avec Ctrl+molette de la souris.", "Haga zoom con Ctrl+rueda del ratón.", "Eseguire lo zoom con Ctrl+rotellina del mouse.", "Zoom met Ctrl+muiswiel.")
                : L(lang, $"Zoom mit Strg+Mausrad ({Math.Abs(input.Dbl("notches")):0.#} Rasten)", $"Zoom with Ctrl+wheel ({Math.Abs(input.Dbl("notches")):0.#} notches)",
                    $"Zoom avec Ctrl+molette ({Math.Abs(input.Dbl("notches")):0.#} crans)", $"Zoom con Ctrl+rueda ({Math.Abs(input.Dbl("notches")):0.#} muescas)",
                    $"Zoom con Ctrl+rotellina ({Math.Abs(input.Dbl("notches")):0.#} scatti)", $"Zoomen met Ctrl+muiswiel ({Math.Abs(input.Dbl("notches")):0.#} klikken)"),
            "scroll" => ScrollText(step, input, lang, guide),
            "type" when input.Bool("masked") => MaskedText(step, input, lang, guide),
            "type" => TypeText(step, input, lang, guide),
            "hotkey" => guide
                ? L(lang, $"Drücken Sie {Key(combo, lang)}.", $"Press {input.Str("combo")}.", $"Appuyez sur {Key(combo, lang)}.", $"Pulse {Key(combo, lang)}.", $"Premere {Key(combo, lang)}.", $"Druk op {Key(combo, lang)}.")
                : L(lang, $"Tastenkombination {Key(combo, lang)}", $"Keyboard shortcut {input.Str("combo")}", $"Raccourci clavier {Key(combo, lang)}", $"Atajo de teclado {Key(combo, lang)}", $"Scelta rapida da tastiera {Key(combo, lang)}", $"Sneltoets {Key(combo, lang)}") + Meaning(input, lang, guide),
            "key" => KeyText(input, lang, guide),
            "hover" => guide
                ? L(lang, $"Zeigen Sie auf {Z()}.", $"Point to {Z()}.", $"Pointez sur {Z()}.", $"Señale {Z()}.", $"Posizionare il puntatore {It("su", Z())}.", $"Wijs {Z()} aan.")
                : L(lang, $"Mauszeiger verweilt auf {Z(false)}", $"Pointer rests on {Z(false)}", $"Le pointeur reste sur {Z(false)}", $"El puntero se detiene sobre {Z(false)}", $"Il puntatore resta {It("su", Z(false))}", $"Aanwijzer blijft op {Z(false)}"),
            "move" => guide ? "" : L(lang, "Mausbewegung ohne folgende Aktion", "Mouse movement without a following action",
                "Mouvement de la souris sans action suivante", "Movimiento del ratón sin acción posterior", "Movimento del mouse senza azione successiva", "Muisbeweging zonder volgende actie"),
            "windowSwitch" => guide
                ? L(lang, $"Wechseln Sie mit Alt+Tab zum Fenster {WinTitle(step, input.Str("to"), lang)}.", $"Press Alt+Tab to switch to the window {WinTitle(step, input.Str("to"), lang)}.",
                    $"Appuyez sur Alt+Tab pour passer à la fenêtre {WinTitle(step, input.Str("to"), lang)}.", $"Pulse Alt+Tab para cambiar a la ventana {WinTitle(step, input.Str("to"), lang)}.",
                    $"Premere Alt+Tab per passare alla finestra {WinTitle(step, input.Str("to"), lang)}.", $"Druk op Alt+Tab om naar het venster {WinTitle(step, input.Str("to"), lang)} te gaan.")
                : L(lang, $"Fensterwechsel mit Alt+Tab zu {WinTitle(step, input.Str("to"), lang)}", $"Window switch with Alt+Tab to {WinTitle(step, input.Str("to"), lang)}",
                    $"Changement de fenêtre avec Alt+Tab vers {WinTitle(step, input.Str("to"), lang)}", $"Cambio de ventana con Alt+Tab a {WinTitle(step, input.Str("to"), lang)}",
                    $"Cambio di finestra con Alt+Tab a {WinTitle(step, input.Str("to"), lang)}", $"Vensterwissel met Alt+Tab naar {WinTitle(step, input.Str("to"), lang)}"),
            "system" => SystemText(step, input, lang, guide),
            "wait" => guide
                ? (input.Obj("until")?.Str("what") == "windowOpened"
                    ? L(lang, $"Warten Sie, bis das Fenster erscheint.", "Wait until the window appears.", "Attendez que la fenêtre s'affiche.", "Espere a que aparezca la ventana.", "Attendere che venga visualizzata la finestra.", "Wacht tot het venster verschijnt.")
                    : L(lang, "Warten Sie, bis der Vorgang abgeschlossen ist.", "Wait for the operation to finish.", "Attendez la fin de l'opération.", "Espere a que finalice la operación.", "Attendere il completamento dell'operazione.", "Wacht tot de bewerking is voltooid."))
                : L(lang, $"Wartezeit von {Duration(input.Long("durationUs"), lang)}", $"Waiting time of {Duration(input.Long("durationUs"), lang)}",
                    $"Temps d'attente de {Duration(input.Long("durationUs"), lang)}", $"Tiempo de espera de {Duration(input.Long("durationUs"), lang)}", $"Tempo di attesa di {Duration(input.Long("durationUs"), lang)}", $"Wachttijd van {Duration(input.Long("durationUs"), lang)}"),
            "comment" => guide ? (commentText ?? "") : L(lang, $"Kommentar des Autors: {commentText}", $"Author's comment: {commentText}",
                $"Commentaire de l'auteur : {commentText}", $"Comentario del autor: {commentText}", $"Commento dell'autore: {commentText}", $"Opmerking van de auteur: {commentText}"),
            "snapshot" => guide ? "" : L(lang, "Schnappschuss", "Snapshot", "Instantané", "Instantánea", "Istantanea", "Momentopname"),
            "marker" => MarkerText(input, lang, guide),
            "gap" when input.Str("cause") == "secureDesktop" && input.Str("desktopKind") == "uac" => guide
                ? L(lang, "Bestätigen Sie die Abfrage der Benutzerkontensteuerung (Windows fragt, ob das Programm Änderungen vornehmen darf). Diese Abfrage wird aus Sicherheitsgründen nicht aufgezeichnet.",
                    "Confirm the User Account Control prompt (Windows asks whether the program may make changes). For security reasons this prompt is not recorded.",
                    "Confirmez l'invite du Contrôle de compte d'utilisateur (Windows demande si le programme peut apporter des modifications). Pour des raisons de sécurité, cette invite n'est pas enregistrée.",
                    "Confirme el aviso del Control de cuentas de usuario (Windows pregunta si el programa puede realizar cambios). Por motivos de seguridad, este aviso no se graba.",
                    "Confermare la richiesta di Controllo account utente (Windows chiede se il programma può apportare modifiche). Per motivi di sicurezza questa richiesta non viene registrata.",
                    "Bevestig de melding van Gebruikersaccountbeheer (Windows vraagt of het programma wijzigingen mag aanbrengen). Om veiligheidsredenen wordt deze melding niet opgenomen.")
                : L(lang, $"Abfrage der Benutzerkontensteuerung (sicherer Desktop, {Duration(step.Long("tTo") - step.Long("tFrom"), lang)}): Eingaben und Bilder wurden nicht aufgezeichnet.",
                    $"User Account Control prompt (secure desktop, {Duration(step.Long("tTo") - step.Long("tFrom"), lang)}): input and images were not recorded.",
                    $"Invite du Contrôle de compte d'utilisateur (bureau sécurisé, {Duration(step.Long("tTo") - step.Long("tFrom"), lang)}) : les saisies et les images n'ont pas été enregistrées.",
                    $"Aviso del Control de cuentas de usuario (escritorio seguro, {Duration(step.Long("tTo") - step.Long("tFrom"), lang)}): no se grabaron entradas ni imágenes.",
                    $"Richiesta di Controllo account utente (desktop sicuro, {Duration(step.Long("tTo") - step.Long("tFrom"), lang)}): input e immagini non sono stati registrati.",
                    $"Melding van Gebruikersaccountbeheer (beveiligd bureaublad, {Duration(step.Long("tTo") - step.Long("tFrom"), lang)}): invoer en afbeeldingen zijn niet opgenomen."),
            "gap" => guide ? L(lang, "Hinweis: ", "Note: ", "Remarque : ", "Nota: ", "Nota: ", "Opmerking: ") + CauseText(input.Str("cause") ?? "unknown", lang)
                           : L(lang, $"Lücke von {Duration(step.Long("tTo") - step.Long("tFrom"), lang)}: ", $"Gap of {Duration(step.Long("tTo") - step.Long("tFrom"), lang)}: ",
                               $"Lacune de {Duration(step.Long("tTo") - step.Long("tFrom"), lang)} : ", $"Interrupción de {Duration(step.Long("tTo") - step.Long("tFrom"), lang)}: ",
                               $"Interruzione di {Duration(step.Long("tTo") - step.Long("tFrom"), lang)}: ", $"Onderbreking van {Duration(step.Long("tTo") - step.Long("tFrom"), lang)}: ") + CauseText(input.Str("cause") ?? "unknown", lang),
            _ => ""
        };
        if (kind == "drag" && r.Length > 0) r += DragDetails(input, lang, guide);
        if (!guide)
        {
            if (step.Obj("target").Str("status") == "single" && kind is "click" or "doubleClick" or "rightClick" or "drag" or "type")
                r += L(lang, " (Ziel nicht bestätigt)", " (target not confirmed)", " (cible non confirmée)", " (destino no confirmado)", " (destinazione non confermata)", " (doel niet bevestigd)");
            if (step.Obj("target").Str("status") == "conflict") r += L(lang, " (Ziel uneindeutig)", " (target ambiguous)", " (cible ambiguë)", " (destino ambiguo)", " (destinazione ambigua)", " (doel dubbelzinnig)");
            var flags = step.StrList("flags");
            if (flags.Contains("injected")) r += L(lang, " (automatisch erzeugte Eingabe)", " (synthetic input)", " (saisie synthétique)", " (entrada sintética)", " (input sintetico)", " (synthetische invoer)");
            if (flags.Contains("discarded")) r += L(lang, " (vom Autor verworfen)", " (discarded by author)", " (rejeté par l'auteur)", " (descartado por el autor)", " (scartato dall'autore)", " (verworpen door de auteur)");
            if (flags.Contains("incomplete")) r += L(lang, " (unvollständig aufgezeichnet)", " (incompletely recorded)", " (enregistré de façon incomplète)", " (grabado de forma incompleta)", " (registrato in modo incompleto)", " (onvolledig opgenomen)");
        }
        if (guide && address == "informal" && de) r = Informal(r);
        else if (guide && address == "neutral" && de) r = Neutral(r);
        return r;
    }

    static string Seconds(long us, string lang) => (us / 1_000_000.0).ToString("0.#", lang == "en" ? CultureInfo.GetCultureInfo("en-US") : OutText.Culture(lang)) + " s";

    /// <summary>Gedrückt halten beim Ziehen: Taste, Modifikatoren, Haltezeit vor der Bewegung und über dem Ziel.</summary>
    static string DragDetails(JsonObject input, string lang, bool guide)
    {
        var parts = new List<string>();
        string button = input.Str("button") ?? "primary";
        var mods = input.StrList("mods");
        long holdBefore = input.Long("holdBeforeMoveUs"), holdEnd = input.Long("holdAtEndUs");
        string modKeys = string.Join("+", mods.Select(m => Key(Cap(m), lang)));
        if (guide)
        {
            if (button == "secondary") parts.Add(L(lang, "Verwenden Sie dabei die rechte Maustaste.", "Use the right mouse button.",
                "Utilisez le bouton droit de la souris.", "Use el botón derecho del ratón.", "Utilizzare il pulsante destro del mouse.", "Gebruik hierbij de rechtermuisknop."));
            if (mods.Count > 0) parts.Add(L(lang, $"Halten Sie dabei {modKeys} gedrückt.", $"Hold {string.Join("+", mods.Select(Cap))} while dragging.",
                $"Maintenez la touche {modKeys} enfoncée pendant le glissement.", $"Mantenga pulsada la tecla {modKeys} mientras arrastra.", $"Tenere premuto {modKeys} durante il trascinamento.", $"Houd hierbij {modKeys} ingedrukt."));
            if (holdBefore >= 500_000) parts.Add(L(lang, $"Halten Sie die Maustaste zuerst etwa {Seconds(holdBefore, lang)} gedrückt, bevor Sie ziehen.", $"Hold the button for about {Seconds(holdBefore, lang)} before dragging.",
                $"Maintenez d'abord le bouton enfoncé pendant environ {Seconds(holdBefore, lang)} avant de faire glisser.", $"Mantenga pulsado el botón unos {Seconds(holdBefore, lang)} antes de arrastrar.",
                $"Tenere premuto il pulsante per circa {Seconds(holdBefore, lang)} prima di trascinare.", $"Houd de muisknop eerst ongeveer {Seconds(holdBefore, lang)} ingedrukt voordat u sleept."));
            if (holdEnd >= 800_000) parts.Add(L(lang, $"Warten Sie über dem Ziel etwa {Seconds(holdEnd, lang)}, bevor Sie loslassen.", $"Wait over the target for about {Seconds(holdEnd, lang)} before releasing.",
                $"Attendez environ {Seconds(holdEnd, lang)} au-dessus de la cible avant de relâcher.", $"Espere unos {Seconds(holdEnd, lang)} sobre el destino antes de soltar.",
                $"Attendere circa {Seconds(holdEnd, lang)} sopra la destinazione prima di rilasciare.", $"Wacht ongeveer {Seconds(holdEnd, lang)} boven het doel voordat u loslaat."));
            return parts.Count == 0 ? "" : " " + string.Join(" ", parts);
        }
        string bname = button switch
        {
            "secondary" => L(lang, "rechte Taste", "right button", "bouton droit", "botón derecho", "pulsante destro", "rechterknop"),
            "middle" => L(lang, "mittlere Taste", "middle button", "bouton du milieu", "botón central", "pulsante centrale", "middelste knop"),
            "primary" => L(lang, "linke Taste", "left button", "bouton gauche", "botón izquierdo", "pulsante sinistro", "linkerknop"),
            _ => button
        };
        parts.Add((mods.Count > 0 ? modKeys + "+" : "") + bname);
        parts.Add(L(lang, "gedrückt ", "held ", "maintenu ", "mantenido ", "tenuto premuto ", "ingedrukt ") + Seconds(input.Long("durationUs"), lang));
        if (holdBefore >= 300_000) parts.Add(L(lang, "davon vor der Bewegung ", "before moving ", "dont avant le mouvement ", "antes de mover ", "prima del movimento ", "vóór de beweging ") + Seconds(holdBefore, lang));
        if (holdEnd >= 300_000) parts.Add(L(lang, "über dem Ziel ", "over target ", "au-dessus de la cible ", "sobre el destino ", "sopra la destinazione ", "boven het doel ") + Seconds(holdEnd, lang));
        if (input.Long("distancePx") > 0) parts.Add($"{input.Long("distancePx")} px");
        return " (" + string.Join(", ", parts) + ")";
    }

    static string Meaning(JsonObject input, string lang, bool guide)
    {
        if (guide || input.Str("commonMeaning") is not { } m) return "";
        string? d = lang switch
        {
            "de" => m switch { "save" => "Speichern", "copy" => "Kopieren", "cut" => "Ausschneiden", "paste" => "Einfügen", "undo" => "Rückgängig", "selectAll" => "Alles markieren", "find" => "Suchen", "print" => "Drucken", "closeWindow" => "Fenster schließen", "new" => "Neu", "open" => "Öffnen", _ => null },
            "fr" => m switch { "save" => "Enregistrer", "copy" => "Copier", "cut" => "Couper", "paste" => "Coller", "undo" => "Annuler", "selectAll" => "Tout sélectionner", "find" => "Rechercher", "print" => "Imprimer", "closeWindow" => "Fermer la fenêtre", "new" => "Nouveau", "open" => "Ouvrir", _ => m },
            "es" => m switch { "save" => "Guardar", "copy" => "Copiar", "cut" => "Cortar", "paste" => "Pegar", "undo" => "Deshacer", "selectAll" => "Seleccionar todo", "find" => "Buscar", "print" => "Imprimir", "closeWindow" => "Cerrar ventana", "new" => "Nuevo", "open" => "Abrir", _ => m },
            "it" => m switch { "save" => "Salva", "copy" => "Copia", "cut" => "Taglia", "paste" => "Incolla", "undo" => "Annulla", "selectAll" => "Seleziona tutto", "find" => "Trova", "print" => "Stampa", "closeWindow" => "Chiudi finestra", "new" => "Nuovo", "open" => "Apri", _ => m },
            "nl" => m switch { "save" => "Opslaan", "copy" => "Kopiëren", "cut" => "Knippen", "paste" => "Plakken", "undo" => "Ongedaan maken", "selectAll" => "Alles selecteren", "find" => "Zoeken", "print" => "Afdrukken", "closeWindow" => "Venster sluiten", "new" => "Nieuw", "open" => "Openen", _ => m },
            _ => m
        };
        return d != null ? L(lang, $" (üblich: {d})", $" (usually: {d})", $" (habituellement : {d})", $" (normalmente: {d})", $" (di solito: {d})", $" (meestal: {d})") : "";
    }

    static string ScrollText(JsonObject step, JsonObject input, string lang, bool guide)
    {
        double n = input.Dbl("notches");
        bool v = input.Str("axis") != "h";
        string dir = v ? (n < 0 ? L(lang, "nach unten", "down", "vers le bas", "hacia abajo", "verso il basso", "omlaag") : L(lang, "nach oben", "up", "vers le haut", "hacia arriba", "verso l'alto", "omhoog"))
                       : (n > 0 ? L(lang, "nach rechts", "right", "vers la droite", "hacia la derecha", "verso destra", "naar rechts") : L(lang, "nach links", "left", "vers la gauche", "hacia la izquierda", "verso sinistra", "naar links"));
        string z = TargetLabel(step, lang, false);
        if (guide) return L(lang, $"Scrollen Sie in {TargetLabel(step, lang, true)} {dir}.", $"Scroll {dir} in {z}.",
            $"Faites défiler {dir} dans {z}.", $"Desplácese {dir} en {z}.", $"Scorrere {dir} {It("in", z)}.", $"Scrol {dir} in {z}.");
        return L(lang, $"Scrollen {dir} um {Math.Abs(n):0.#} Rasten in {z}", $"Scroll {dir} {Math.Abs(n):0.#} notches in {z}",
            $"Défilement {dir} de {Math.Abs(n):0.#} crans dans {z}", $"Desplazamiento {dir} de {Math.Abs(n):0.#} muescas en {z}",
            $"Scorrimento {dir} di {Math.Abs(n):0.#} scatti {It("in", z)}", $"{Math.Abs(n):0.#} klikken {dir} scrollen in {z}");
    }

    static string TypeText(JsonObject step, JsonObject input, string lang, bool guide)
    {
        bool de = lang == "de";
        string z = TargetLabel(step, lang, guide);
        bool noTarget = step.Obj("target").Obj("el") == null;
        string text = input.Str("text") ?? TokensNotation(new JsonObject { ["tokens"] = input["tokens"]?.DeepClone() });
        string? term = input.Obj("terminator").Str("key");
        string qt = Q(text, lang);
        if (guide)
        {
            string core = noTarget
                ? L(lang, $"Geben Sie {qt} ein", $"Type {qt}", $"Saisissez {qt}", $"Escriba {qt}", $"Digitare {qt}", $"Typ {qt}")
                : L(lang, $"Geben Sie in {z} {qt} ein", $"Type {qt} in {z}", $"Saisissez {qt} dans {z}", $"Escriba {qt} en {z}", $"Digitare {qt} {It("in", z)}", $"Typ {qt} in {z}");
            if (term != null)
                core += de ? (term == "Enter" ? " und drücken Sie die Eingabetaste" : $" und drücken Sie die Taste {Key(term, lang)}")
                           : L(lang, "", $" and press {term}", $" et appuyez sur {Key(term, lang)}", $" y pulse {Key(term, lang)}", $" e premere {Key(term, lang)}", $" en druk op {Key(term, lang)}");
            return core + ".";
        }
        string r = noTarget
            ? L(lang, "Text eingegeben", "Text entered", "Texte saisi", "Texto escrito", "Testo digitato", "Tekst ingevoerd")
            : L(lang, $"Text in {z} eingegeben", $"Text entered in {z}", $"Texte saisi dans {z}", $"Texto escrito en {z}", $"Testo digitato {It("in", z)}", $"Tekst ingevoerd in {z}");
        if (term != null) r += L(lang, $" – abgeschlossen mit {Key(term, lang)}", $" – finished with {term}",
            $" – validé avec {Key(term, lang)}", $" – terminado con {Key(term, lang)}", $" – confermato con {Key(term, lang)}", $" – afgesloten met {Key(term, lang)}");
        return r;
    }

    static string MaskedText(JsonObject step, JsonObject input, string lang, bool guide)
    {
        string reason = input.Str("maskReason") ?? "";
        bool priv = reason == "privacyMode";
        bool strict = reason.StartsWith("strict");
        // erkannte Passwortabfrage im Terminal (10 §6.3): die Abfragezeile ist der Kontext („Abfrage: [sudo] password for admin:“)
        if (reason == "terminalPrompt" && input.Str("promptLine") is { Length: > 0 } pl)
        {
            string q = Q(pl, lang);
            if (guide) return L(lang, $"Geben Sie an der Abfrage {q} Ihr Kennwort ein.", $"At the prompt {q}, enter your password.", $"À l’invite {q}, saisissez votre mot de passe.", $"En la solicitud {q}, escriba su contraseña.", $"Al prompt {q}, immettere la password.", $"Voer bij de prompt {q} uw wachtwoord in.");
            long? k = input.LongN("count");
            return k != null
                ? L(lang, $"Eingabe an der Abfrage {q}: {k} Tasten, nicht aufgezeichnet (maskiert)", $"Input at the prompt {q}: {k} keys, not recorded (masked)", $"Saisie à l’invite {q} : {k} touches, non enregistrées (masquées)", $"Entrada en la solicitud {q}: {k} teclas, no grabadas (enmascaradas)", $"Input al prompt {q}: {k} tasti, non registrati (mascherati)", $"Invoer bij de prompt {q}: {k} toetsen, niet opgenomen (gemaskeerd)")
                : L(lang, $"Eingabe an der Abfrage {q}: nicht aufgezeichnet (maskiert)", $"Input at the prompt {q}: not recorded (masked)", $"Saisie à l’invite {q} : non enregistrée (masquée)", $"Entrada en la solicitud {q}: no grabada (enmascarada)", $"Input al prompt {q}: non registrato (mascherato)", $"Invoer bij de prompt {q}: niet opgenomen (gemaskeerd)");
        }
        if (guide) return priv || strict
            ? L(lang, "Geben Sie hier Ihre vertraulichen Daten ein.", "Enter your confidential data here.", "Saisissez ici vos données confidentielles.", "Escriba aquí sus datos confidenciales.", "Immettere qui i propri dati riservati.", "Voer hier uw vertrouwelijke gegevens in.")
            : L(lang, "Geben Sie Ihr Kennwort ein.", "Enter your password.", "Saisissez votre mot de passe.", "Escriba su contraseña.", "Immettere la password.", "Voer uw wachtwoord in.");
        long? n = input.LongN("count");
        string z = step.Obj("target").Obj("el") != null ? TargetLabel(step, lang, false) : L(lang, "Feld", "field", "le champ", "el campo", "il campo", "het veld");
        if (priv) return n != null
            ? L(lang, $"Eingabe: {n} Tasten, nicht aufgezeichnet (Privatmodus)", $"Input: {n} keys, not recorded (private mode)", $"Saisie : {n} touches, non enregistrée (mode privé)",
                $"Entrada: {n} teclas, no grabada (modo privado)", $"Input: {n} tasti, non registrato (modalità privata)", $"Invoer: {n} toetsen, niet opgenomen (privémodus)")
            : L(lang, "Eingabe: nicht aufgezeichnet (Privatmodus)", "Input: not recorded (private mode)", "Saisie : non enregistrée (mode privé)",
                "Entrada: no grabada (modo privado)", "Input: non registrato (modalità privata)", "Invoer: niet opgenomen (privémodus)");
        if (strict) return n != null
            ? L(lang, $"Eingabe: {n} Tasten, nicht aufgezeichnet (Schutzstufe „Streng“)", $"Input: {n} keys, not recorded (protection level \"strict\")", $"Saisie : {n} touches, non enregistrée (niveau de protection {Q("strict", "fr")})",
                $"Entrada: {n} teclas, no grabada (nivel de protección {Q("estricto", "es")})", $"Input: {n} tasti, non registrato (livello di protezione {Q("rigoroso", "it")})", $"Invoer: {n} toetsen, niet opgenomen (beveiligingsniveau {Q("strikt", "nl")})")
            : L(lang, "Eingabe: nicht aufgezeichnet (Schutzstufe „Streng“)", "Input: not recorded (protection level \"strict\")", $"Saisie : non enregistrée (niveau de protection {Q("strict", "fr")})",
                $"Entrada: no grabada (nivel de protección {Q("estricto", "es")})", $"Input: non registrato (livello di protezione {Q("rigoroso", "it")})", $"Invoer: niet opgenomen (beveiligingsniveau {Q("strikt", "nl")})");
        return n != null
            ? L(lang, $"Eingabe in {z}: {n} Tasten, nicht aufgezeichnet (maskiert)", $"Input in {z}: {n} keys, not recorded (masked)", $"Saisie dans {z} : {n} touches, non enregistrée (masquée)",
                $"Entrada en {z}: {n} teclas, no grabada (enmascarada)", $"Input {It("in", z)}: {n} tasti, non registrato (mascherato)", $"Invoer in {z}: {n} toetsen, niet opgenomen (gemaskeerd)")
            : L(lang, $"Eingabe in {z}: nicht aufgezeichnet (maskiert)", $"Input in {z}: not recorded (masked)", $"Saisie dans {z} : non enregistrée (masquée)",
                $"Entrada en {z}: no grabada (enmascarada)", $"Input {It("in", z)}: non registrato (mascherato)", $"Invoer in {z}: niet opgenomen (gemaskeerd)");
    }

    static string KeyText(JsonObject input, string lang, bool guide)
    {
        string k = Key(input.Str("key") ?? "", lang);
        int n = input.Int("count", 1);
        bool endsTaste = k.EndsWith("taste");
        if (guide) return n > 1
            ? L(lang, $"Drücken Sie {n}-mal {(endsTaste ? "die " + k : "die Taste " + k)}.", $"Press {k} {n} times.", $"Appuyez {n} fois sur {k}.", $"Pulse {k} {n} veces.", $"Premere {k} {n} volte.", $"Druk {n} keer op {k}.")
            : L(lang, endsTaste ? $"Drücken Sie die {k}." : $"Drücken Sie die Taste {k}.", $"Press {k}.", $"Appuyez sur {k}.", $"Pulse {k}.", $"Premere {k}.", $"Druk op {k}.");
        return n > 1
            ? L(lang, $"{n}× Taste {k}", $"{k} pressed {n} times", $"Touche {k} appuyée {n} fois", $"Tecla {k} pulsada {n} veces", $"Tasto {k} premuto {n} volte", $"Toets {k} {n} keer ingedrukt")
            : L(lang, $"Taste {k}", $"Key {k}", $"Touche {k}", $"Tecla {k}", $"Tasto {k}", $"Toets {k}");
    }

    static string SystemText(JsonObject step, JsonObject input, string lang, bool guide)
    {
        string f = F(step, lang);
        string qt = Q(input.Str("title") ?? "", lang), qn = Q(input.Str("text") ?? "", lang);
        return input.Str("event") switch
        {
            "windowOpened" => guide
                ? L(lang, $"Das Fenster {f} wird angezeigt.", $"The window {f} appears.", $"La fenêtre {f} s'affiche.", $"Aparece la ventana {f}.", $"Viene visualizzata la finestra {f}.", $"Het venster {f} verschijnt.")
                : L(lang, $"Fenster {f} erschien", $"Window {f} appeared", $"Fenêtre {f} affichée", $"Apareció la ventana {f}", $"Finestra {f} visualizzata", $"Venster {f} verscheen"),
            "windowClosed" => guide
                ? L(lang, $"Das Fenster {f} schließt sich.", $"The window {f} closes.", $"La fenêtre {f} se ferme.", $"Se cierra la ventana {f}.", $"La finestra {f} si chiude.", $"Het venster {f} wordt gesloten.")
                : L(lang, $"Fenster {f} wurde geschlossen", $"Window {f} closed", $"Fenêtre {f} fermée", $"Se cerró la ventana {f}", $"Finestra {f} chiusa", $"Venster {f} gesloten"),
            "foregroundChanged" => guide
                ? L(lang, $"Das Fenster {f} tritt in den Vordergrund.", $"The window {f} comes to the front.", $"La fenêtre {f} passe au premier plan.", $"La ventana {f} pasa a primer plano.", $"La finestra {f} passa in primo piano.", $"Het venster {f} komt op de voorgrond.")
                : L(lang, $"Fenster {f} kam in den Vordergrund", $"Window {f} came to the foreground", $"Fenêtre {f} passée au premier plan", $"La ventana {f} pasó a primer plano", $"Finestra {f} portata in primo piano", $"Venster {f} kwam op de voorgrond"),
            "titleChanged" => L(lang, $"Fenstertitel geändert: {qt}", $"Window title changed: {qt}", $"Titre de la fenêtre modifié : {qt}", $"Título de la ventana cambiado: {qt}", $"Titolo della finestra modificato: {qt}", $"Venstertitel gewijzigd: {qt}"),
            "notification" => guide
                ? L(lang, $"Es erscheint die Mitteilung {qn}.", $"The notification {qn} appears.", $"La notification {qn} s'affiche.", $"Aparece la notificación {qn}.", $"Viene visualizzata la notifica {qn}.", $"De melding {qn} verschijnt.")
                : L(lang, $"Mitteilung: {qn}", $"Notification: {qn}", $"Notification : {qn}", $"Notificación: {qn}", $"Notifica: {qn}", $"Melding: {qn}"),
            "valueChanged" => L(lang, "Inhalt änderte sich ohne beobachtete Eingabe", "Content changed without observed input", "Le contenu a changé sans saisie observée", "El contenido cambió sin entrada observada", "Il contenuto è cambiato senza input osservato", "Inhoud gewijzigd zonder waargenomen invoer"),
            "topologyChanged" => L(lang, "Monitor-Anordnung geändert", "Monitor layout changed", "Disposition des écrans modifiée", "Disposición de monitores cambiada", "Disposizione dei monitor modificata", "Monitorindeling gewijzigd"),
            "scopeChanged" => L(lang, "Aufnahmebereich gewechselt", "Capture area changed", "Zone d'enregistrement changée", "Área de grabación cambiada", "Area di registrazione cambiata", "Opnamegebied gewijzigd"),
            "appExit" or "appHang" or "appEventLog" => ProgramText(input, lang),
            _ => ""
        };
    }

    /// <summary>Programm eines Programm-Schritts: Produktname, sonst Programmdatei.</summary>
    static string Prog(JsonObject input, string lang) => Q(input.Str("product") is { Length: > 0 } p ? p : input.Str("process") ?? "?", lang);

    static string ProgramTitle(JsonObject input, string lang)
    {
        string p = Prog(input, lang);
        return input.Str("class") switch
        {
            "crash" => L(lang, $"{p} stürzt ab", $"{p} crashes", $"{p} plante", $"{p} se bloquea", $"{p} si arresta in modo anomalo", $"{p} crasht"),
            "error" when input.LongN("exitCode") is long c => L(lang, $"{p} endet mit Code {Capture.ExitCodes.Display((uint)c)}", $"{p} exits with code {Capture.ExitCodes.Display((uint)c)}",
                $"{p} se termine avec le code {Capture.ExitCodes.Display((uint)c)}", $"{p} finaliza con el código {Capture.ExitCodes.Display((uint)c)}",
                $"{p} termina con il codice {Capture.ExitCodes.Display((uint)c)}", $"{p} stopt met code {Capture.ExitCodes.Display((uint)c)}"),
            "normal" => L(lang, $"{p} wird beendet", $"{p} closes", $"{p} se ferme", $"{p} se cierra", $"{p} si chiude", $"{p} wordt afgesloten"),
            _ => L(lang, $"{p} wurde beendet", $"{p} ended", $"{p} s'est terminé", $"{p} finalizó", $"{p} è terminato", $"{p} is beëindigd")
        };
    }

    /// <summary>Text eines Programm-Schritts (12, Paket 10): Einstufung, Exit-Code, Dauer, Fakten der Ereignisanzeige.</summary>
    static string ProgramText(JsonObject input, string lang)
    {
        string p = Prog(input, lang);
        var sb = new System.Text.StringBuilder();
        switch (input.Str("event"))
        {
            case "appExit":
                uint code = (uint)(input.LongN("exitCode") ?? 0);
                string? name = input.LongN("exitCode") != null ? Capture.ExitCodes.Name(code, lang) : null;
                string hex = Capture.ExitCodes.Hex(code), disp = Capture.ExitCodes.Display(code);
                sb.Append(input.Str("class") switch
                {
                    "crash" => L(lang, $"Das Programm {p} wurde unerwartet beendet{(name != null ? ": " + name : "")} (Code {hex}).", $"The program {p} ended unexpectedly{(name != null ? ": " + name : "")} (code {hex}).",
                        $"Le programme {p} s'est arrêté de façon inattendue{(name != null ? " : " + name : "")} (code {hex}).", $"El programa {p} se cerró inesperadamente{(name != null ? ": " + name : "")} (código {hex}).",
                        $"Il programma {p} si è chiuso in modo imprevisto{(name != null ? ": " + name : "")} (codice {hex}).", $"Het programma {p} is onverwacht beëindigd{(name != null ? ": " + name : "")} (code {hex})."),
                    "error" => L(lang, $"Das Programm {p} wurde mit Code {disp} beendet.", $"The program {p} exited with code {disp}.",
                        $"Le programme {p} s'est terminé avec le code {disp}.", $"El programa {p} finalizó con el código {disp}.", $"Il programma {p} è terminato con il codice {disp}.", $"Het programma {p} is beëindigd met code {disp}."),
                    "normal" => L(lang, $"Das Programm {p} wurde beendet.", $"The program {p} closed.",
                        $"Le programme {p} a été fermé.", $"El programa {p} se cerró.", $"Il programma {p} è stato chiuso.", $"Het programma {p} is afgesloten."),
                    _ => L(lang, $"Das Programm {p} wurde beendet; der Exit-Code ist nicht bekannt.", $"The program {p} ended; the exit code is unknown.",
                        $"Le programme {p} s'est terminé ; le code de sortie est inconnu.", $"El programa {p} finalizó; se desconoce el código de salida.", $"Il programma {p} è terminato; il codice di uscita non è noto.", $"Het programma {p} is beëindigd; de afsluitcode is onbekend.")
                });
                if (input.Bool("wasHung")) sb.Append(L(lang, " Es reagierte davor nicht mehr.", " It had stopped responding before.",
                    " Il ne répondait plus auparavant.", " Antes había dejado de responder.", " In precedenza non rispondeva più.", " Het reageerde daarvoor niet meer."));
                break;
            case "appHang":
                string d = Dur(input.Long("durationMs"), lang);
                sb.Append(input.Bool("untilStop")
                    ? L(lang, $"Das Programm {p} reagierte bis zum Ende der Aufnahme nicht.", $"The program {p} did not respond until the end of the recording.",
                        $"Le programme {p} n'a plus répondu jusqu'à la fin de l'enregistrement.", $"El programa {p} no respondió hasta el final de la grabación.",
                        $"Il programma {p} non ha risposto fino alla fine della registrazione.", $"Het programma {p} reageerde tot het einde van de opname niet.")
                    : L(lang, $"Das Programm {p} reagierte {d} lang nicht.", $"The program {p} did not respond for {d}.",
                        $"Le programme {p} n'a pas répondu pendant {d}.", $"El programa {p} no respondió durante {d}.", $"Il programma {p} non ha risposto per {d}.", $"Het programma {p} reageerde {d} lang niet."));
                break;
            case "appEventLog":
                sb.Append(L(lang, $"Die Ereignisanzeige von Windows meldet einen Fehler zu {p}.", $"The Windows event log reports an error for {p}.",
                    $"Le journal des événements de Windows signale une erreur pour {p}.", $"El registro de eventos de Windows informa de un error en {p}.",
                    $"Il registro eventi di Windows segnala un errore per {p}.", $"Het Windows-logboek meldt een fout voor {p}."));
                break;
        }
        foreach (var f in input.Arr("eventLog")?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
        {
            var parts = new List<string>();
            if (f.Str("exceptionType") is { } et) parts.Add(L(lang, "Ausnahme ", "exception ", "exception ", "excepción ", "eccezione ", "uitzondering ") + et);
            if (f.Str("exceptionCode") is { } ec) parts.Add(L(lang, "Ausnahmecode ", "exception code ", "code d'exception ", "código de excepción ", "codice eccezione ", "uitzonderingscode ") + ec);
            if (f.Str("module") is { } mod) parts.Add(L(lang, "fehlerhaftes Modul ", "faulting module ", "module défaillant ", "módulo con errores ", "modulo con errori ", "defecte module ") + mod + (f.Str("moduleVersion") is { } mv ? " " + mv : "")
                + (f.Str("offset") is { } off ? L(lang, ", Offset ", ", offset ", ", décalage ", ", desplazamiento ", ", offset ", ", offset ") + off : ""));
            if (parts.Count > 0) sb.Append(' ').Append(L(lang, "Ereignisanzeige: ", "Event log: ", "Journal des événements : ", "Registro de eventos: ", "Registro eventi: ", "Logboek: ")).Append(string.Join(" · ", parts)).Append('.');
        }
        return sb.ToString();
    }

    static string Dur(long ms, string lang) => ms < 1000 ? $"{ms} ms" : (ms / 1000.0).ToString(ms < 10_000 ? "0.0" : "0", OutText.Culture(lang)) + " s";

    static string MarkerText(JsonObject input, string lang, bool guide)
    {
        string label = input.Str("label") is { } l ? " – " + l : "";
        return input.Str("markerKind") switch
        {
            "error" => (guide
                ? L(lang, "Hinweis: Hier tritt der Fehler auf.", "Note: The error occurs here.", "Remarque : l'erreur se produit ici.", "Nota: aquí se produce el error.", "Nota: qui si verifica l'errore.", "Opmerking: hier treedt de fout op.")
                : L(lang, "Marker des Autors: Hier tritt der Fehler auf", "Author's marker: The error occurs here", "Repère de l'auteur : l'erreur se produit ici", "Marcador del autor: aquí se produce el error", "Marcatore dell'autore: qui si verifica l'errore", "Markering van de auteur: hier treedt de fout op")) + label,
            "expected" => (guide
                ? L(lang, "Hinweis: So sollte es aussehen.", "Note: This is how it should look.", "Remarque : voici le résultat attendu.", "Nota: así debería verse.", "Nota: ecco come dovrebbe apparire.", "Opmerking: zo hoort het eruit te zien.")
                : L(lang, "Marker des Autors: So sollte es aussehen", "Author's marker: This is how it should look", "Repère de l'auteur : voici le résultat attendu", "Marcador del autor: así debería verse", "Marcatore dell'autore: ecco come dovrebbe apparire", "Markering van de auteur: zo hoort het eruit te zien")) + label,
            "important" => (guide
                ? L(lang, "Wichtig: Beachten Sie diese Stelle.", "Important: Note this point.", "Important : faites attention à ce point.", "Importante: preste atención a este punto.", "Importante: prestare attenzione a questo punto.", "Belangrijk: let op dit punt.")
                : L(lang, "Marker des Autors: Wichtig", "Author's marker: Important", "Repère de l'auteur : important", "Marcador del autor: importante", "Marcatore dell'autore: importante", "Markering van de auteur: belangrijk")) + label,
            _ => guide ? (input.Str("label") ?? "") : L(lang, "Marker", "Marker", "Repère", "Marcador", "Marcatore", "Markering") + label
        };
    }

    static string Informal(string s) => s
        .Replace("Klicken Sie", "Klicke").Replace("Doppelklicken Sie", "Doppelklicke").Replace("Geben Sie", "Gib").Replace("Drücken Sie", "Drücke")
        .Replace("Wählen Sie", "Wähle").Replace("Öffnen Sie", "Öffne").Replace("Wechseln Sie", "Wechsle").Replace("Ziehen Sie", "Ziehe").Replace("Zeigen Sie", "Zeige")
        .Replace("Scrollen Sie", "Scrolle").Replace("Aktivieren Sie", "Aktiviere").Replace("Deaktivieren Sie", "Deaktiviere").Replace("Markieren Sie", "Markiere")
        .Replace("Verschieben Sie", "Verschiebe").Replace("Warten Sie", "Warte").Replace("Halten Sie", "Halte").Replace("Beachten Sie", "Beachte")
        .Replace(" Ihr ", " dein ").Replace(" Ihre ", " deine ").Replace("drücken Sie", "drücke").Replace("klicken Sie", "klicke")
        // vollständige du-Form (13, genehmigte Korrektur)
        .Replace("Ändern Sie", "Ändere").Replace("Vergrößern oder verkleinern Sie", "Vergrößere oder verkleinere").Replace("Bestätigen Sie", "Bestätige")
        .Replace("Verwenden Sie", "Verwende").Replace("bevor Sie ziehen", "bevor du ziehst").Replace("bevor Sie loslassen", "bevor du loslässt");

    static string Neutral(string s) => s; // Neutrale Form: Vorlagen bleiben in der Sie-Form, wenn keine eigene Vorlage existiert
}
