Deutsche Fassung: [README.md](README.md)

# Sebastians Step Recorder

**Demonstrate a task once – and get a finished guide with pictures.**

Sebastians Step Recorder records what you do on your Windows PC: every click, every entry, every key combination – together with a screenshot and the name of the control you clicked. From this it creates user guides as PDF, Word, HTML, Markdown or animated GIF, a package for AI assistants, or an AutoHotkey script that repeats the procedure.

A single file, no installation, no admin rights, no internet. The program never appears in its own screenshots. **Free to use** – including for business ([CC BY-ND 4.0](#license)).

**[⬇ Download: SebastiansStepRecorder.exe](https://github.com/Sebedai/sebastians-step-recorder/releases/latest)**

---

## Contents

- [Quick start](#quick-start)
- [The recording modes](#the-recording-modes)
- [While recording](#while-recording)
- [The editor](#the-editor)
- [Designing user guides](#designing-user-guides)
- [Export formats](#export-formats)
- [Privacy](#privacy)
- [For AI and automation](#for-ai-and-automation)
- [For software developers](#for-software-developers)
- [System requirements and notes](#system-requirements-and-notes)
- [Frequently asked questions](#frequently-asked-questions)
- [License](#license)

---

## Quick start

1. **Download `SebastiansStepRecorder.exe`** and put it anywhere, e.g. `C:\Tools\` or a USB stick. Double-click to start the program.
2. **Choose a mode:** What is the recording for? If in doubt, choose "Guide".
3. **Click an area:** the whole screen (or simply press Enter), a single application, or a region you draw yourself. Recording starts immediately.
4. **Demonstrate:** just work as usual. To finish, press **Ctrl+Shift+F10** or click "Stop" on the recording bar.
5. **Edit and export:** the editor opens with all steps. Adjust the texts, mark up the images, then click **Export** – done.

On first start, Windows SmartScreen may show a warning because the file is not signed: "More info" → "Run anyway".

---

## The recording modes

The mode determines how precisely things are recorded. More detail means more accurate AI and script output, but also larger recordings.

| Mode | Intended for | Level of detail | After stopping |
|---|---|---|---|
| **Guide** | Step-by-step guides for people | **Lean:** clicks, entries and keys; no mouse paths; extra images only for large changes | The editor opens |
| **Support case** | Making an error reproducible for the help desk | **Detailed:** all inputs including mouse paths; extra images also for small changes; extended application context | Review the inputs, then a ZIP package is created |
| **AI trace** | Procedures for AI assistants or AutoHotkey | **Complete:** everything; extra images for every visible change; extended application context | The AI export opens |
| **Reproduce bug** | Documenting a bug for developers | **Detailed** like support case, goal and expected result captured up front, program context (program exit, hangs, build) | The editor jumps to the failure point |
| **Custom** | When you want to decide exactly yourself | **Freely selectable:** input types, extra images, application context, action after stopping | selectable |

"Customize …" takes over the level of detail of a mode as a starting point for "Custom".

### When are images created?

**In every mode**, two images are created for each input: one right **before** the input and one as soon as the screen has been calm for 0.3 seconds **afterwards** (after 3 seconds at the latest).

**Extra images** are created when the screen changes without you entering anything – for example because a program is loading. They are captured as soon as the screen has been calm for 0.3 seconds afterwards, or after 5 seconds at the latest if there is constant movement. How large the change has to be is determined by the setting:

| Setting | Extra image from a change of … | Examples | At most every | Mode |
|---|---|---|---|---|
| **Large changes only** | approx. 2 % of the screen (about 200 × 200 pixels at Full HD) | new window, page change | 2 seconds | Guide |
| **Small changes too** | approx. 0.1 % (about 45 × 45 pixels) | message, tooltip, status display | 0.5 seconds | Support case |
| **Every visible change** | approx. 0.01 % (about 15 × 15 pixels) | progress bar, counter, loading animation | 0.25 seconds | AI trace |

**Extended application context** means: in addition, menu paths, list entries, Explorer folders, browser addresses, Office documents and status bars are read. The basic context is enough for guides; the extended one is worthwhile for good AutoHotkey scripts and AI analysis.

---

## While recording

A small **recording bar** floats at the top of the screen, showing the time, the number of steps and the most important buttons. In addition, an **icon appears in the notification area of the taskbar** (bottom right). Clicking it shows or hides the bar; right-clicking opens a menu with all commands. On Windows 11 the icon initially sits in the overflow "^"; via *Taskbar settings → Other system tray icons* you can make it permanently visible.

| Shortcut | Effect |
|---|---|
| **Ctrl+Shift+F9** | Start recording immediately · Pause / Resume |
| **Ctrl+Shift+F10** | Stop and save the recording |
| **Ctrl+Shift+F1** | Mark the error location |
| **Ctrl+Shift+F2** | "Expected" marker |
| **Ctrl+Shift+F7** | Set a marker |
| **Ctrl+Shift+F12** | Enter a comment |
| **Ctrl+Shift+F5** | Dictation on/off: spoken words become comments |
| **Ctrl+Shift+F11** | Snapshot |
| **Ctrl+Shift+F8** | Private key: inputs are only counted, not stored |
| **Ctrl+Shift+F6** | Discard the last step |
| **Ctrl+Shift+F4** | Show / hide the bar (minimize to the notification area) |
| **Ctrl+Shift+F3** | Instant screenshot: pick an area, draw on it in the editor, copy (while recording: snapshot) |

**Dictation:** Instead of typing, simply speak your explanations. Each sentence is saved as a comment at the point where you started it. Recognition runs offline via Windows speech recognition; the microphone is only open while dictation is running (red microphone on the bar), and only the text is stored.

**Countdown and signal tones:** If you like, the program counts down before starting and plays short tones on start, stop, pause and markers – handy when no bar is visible.

---

## The editor

After recording, the editor opens: the steps on the left, the image in the middle, text and settings on the right.

**Steps**
- Edit the heading and guide text directly (with spell checking) – the automatically generated texts are a good start.
- Reorder steps by dragging, hide them, or classify them as main or secondary steps.
- **Select several steps** with Ctrl+click or Shift+click and edit them together.
- **Insert your own steps:** text-only steps or image steps from a file, the clipboard or a new screenshot.
- **Replace or re-capture an image** when the interface has changed – the original can be restored at any time.
- **Re-record and replace a section** (Ctrl+R): mark steps and demonstrate the section again – the new steps take the place of the old ones. The old ones are kept, the seam is marked, Ctrl+Z undoes the replacement.
- **Bookmarks** (Ctrl+Shift+L): mark steps with a star and filter by them.
- **Text snippets** (Ctrl+Shift+B): insert frequently used texts into notes and step texts; managed under *Settings → General*.
- **Step numbers and markup in images:** optionally the step number – exactly as in the guide, e.g. “2.3” – appears right at the click point (View → Mouse marks → Step number). Mark styles: Classic, Numbers, Subtle, Bold. Twelve colors, custom colors and an eyedropper; numbers renumber automatically after deleting (also as a, b, c or i, ii, iii), double-headed arrows, dashed frames, filled shapes, colored highlighter; “apply style” and “mark target” with one click.
- **Outline:** chapters and subchapters (numbers like 2.3.1), move whole chapters with Alt+Shift+↑/↓ or hide them, numbering “per chapter” and text sections without numbers – empty or prefilled as safety information, FAQ, contact. A chapter heading is kept even if its first step is hidden.
- **Cross-references, variables and tables:** via “＋” in the text field a text refers to another step (“see step 2.3” – the number stays right after reordering), inserts variables such as {{Product}} or {{Version}} (defined in the document pane, per language) and inserts tables that appear as real tables in PDF, Word, HTML and Markdown.
- **Formatted text:** the guide text, notes, introduction and closing support **bold** (Ctrl+B), *italic* (Ctrl+I), input such as `notes.txt` (Ctrl+E), keys such as Ctrl+S, names of buttons, links (Ctrl+K) as well as bulleted and numbered lists. The formatting appears in PDF, Word, HTML and Markdown; redactions and placeholders still apply everywhere.
- **Find and replace** (Ctrl+H) across all headings, texts, notes and chapters.
- **Automatically cleaned up:** When first opened, the editor hides superfluous steps – the click into a field right before typing, duplicate clicks on the same element, a window switch away and straight back, and entries that were deleted again. Nothing is deleted: Ctrl+Z undoes it, and under *⋯ → Clean up* you can see every suggestion with its reason.
- **Better texts:** Menus appear as a path ("File › Save as"), ribbon buttons with their group. A **glossary** in the settings replaces technical names with your own (e.g. "SAP Easy Access=SAP start page").

**Images**
- Before/after image, whole screen, only the application window, or a section around the action (with a small overview of the whole screen).
- Drawing tools: arrow, frame, ellipse, highlight, text, numbered markers, redact, pixelate, crop – in six colors and three line widths.
- Automatic markup: click point, drag path, clicked element, and pressed keys as small keycaps in the image – each can be switched off individually.
- Zoom (Ctrl+mouse wheel), large view (F11 or double-click), copy image (Ctrl+C).
- **Short clip per step:** *Copy → Save short clip (GIF)* shows the step as a small animation – from the before image through the intermediate images to the result.

**More features**
- **Play:** the steps as a slide show.
- **Page view (Ctrl+F2):** the pages of the finished guide right in the editor – exactly as the PDF contains them. Every change appears after a moment; clicking a step on a page selects it.
- **Print (Ctrl+P):** send the guide straight to the printer.
- **Preview in browser:** view the finished guide as HTML (*File → Preview in browser*).
- **Compare:** put two recordings of the same procedure side by side (F7) – pair list, both images side by side with highlighted image differences, timing differences; can be saved as Markdown with before/after images.
- **Command palette** (Ctrl+Shift+P): find any command by name.
- **Response times:** every step shows how long the application took; the "Slow" filter shows only slow steps.
- **Copy as Markdown:** one step (Ctrl+M) or all steps (Ctrl+Shift+M) – for pull requests, issues and wikis.
- **German and English:** with "DE | EN" above the image you maintain texts in both languages – one recording becomes two guides.
- Everything is saved automatically; undo with Ctrl+Z.

---

## Designing user guides

On the **"Document"** tab, the steps become a proper user guide:

- **Title page:** title, subtitle, version, target audience, author or company, **logo**.
- **Texts:** introduction, prerequisites (as a list) and a conclusion.
- **Structure:** cover page, table of contents (with page numbers in the PDF), numbering as 1.1, 1.2 … or continuous.
- **Chapters:** at any step you can start a new chapter with its own introduction.
- **Notes:** Note, Tip, Caution, Important and Result – set off in color.
- **Images:** size (small to full width), placement below or beside the text, image section; changeable per step or hidden entirely.
- **Appearance:** accent color, addressing the reader formally ("Sie") or informally ("du") in the automatic texts, footer with page number.
- **Templates:** save logo, color, footer and structure once as a template – new recordings can automatically get your corporate look.
- **Several recordings in one manual:** append further recordings or continue a recording directly; they appear as separate chapters.
- **Page:** A4 or Letter, portrait or landscape (Word and PDF).
- **Your own Word template:** a `.dotx` or `.docx` with your company's header and footer, fonts, styles and margins; the content appears at the marked place (`StepRecorderContent`). Templates with macros are rejected.
- **Guide languages:** German, English, French, Spanish, Italian or Dutch – the automatic step texts and all headings of the guide.

---

## Export formats

| Format | Result | Suitable for |
|---|---|---|
| **PDF** | finished document with cover page, table of contents and page numbers | printing, sharing, archiving |
| **Word (.docx)** | editable document with real headings | further editing in Word or LibreOffice |
| **HTML** | a single file, images embedded, images can be enlarged by clicking | e-mail, intranet, viewing without any other program |
| **Markdown** | text plus image folder `<name>-bilder/`, ASCII file names, note boxes for GitHub/GitLab or Obsidian, optional front matter (YAML); the same recording gives the same file | wikis, Git repositories, Docusaurus, MkDocs, Obsidian |
| **GIF** | animated short version: title image, then each step with number and heading; with a size target (e.g. 10 MB for pull requests) | teams, chat, wiki, quick preview |
| **Video (MP4)** | the same image sequence as an H.264 video, created with Windows Media Foundation | training platforms, presentations, chat |
| **AI package** | trace, steps, images and reading guide for AI assistants | ChatGPT, Claude & co. |
| **AutoHotkey** | AutoHotkey v2 script that repeats the procedure | automating recurring procedures |
| **UI test** | test draft for FlaUI (C#), pywinauto (Python) or Playwright (browser steps), optionally as a test AI package | automated UI tests |
| **Support package** | ZIP with HTML guide and AI package | passing errors to the help desk |

**Readable for people:** For the AI package and AutoHotkey there is the option *"Readable for people – fully commented"*. Every detail – including system info, screens, evidence quality, coordinates and gaps – is then explained in plain language, each step gets an "In words" sentence, and a `LIESMICH.md` plus explanations in the JSON files are added; the script explains its structure and every step. Without the option the export stays concise and saves tokens when only an AI reads it.

**Which images are included:** In the export dialog, for the AI package you choose the *before image* (*Automatic*, *Always*, *Never*) as well as the *close-up around the action* and the *after image*. *Automatic* (default) only includes a before image if the screen before it is not already visible in the package – otherwise the trace refers to the previous step's after image. For guides (HTML, Markdown, Word, PDF) the image after the step can additionally appear as the *result* (*Never*, *On change*, *Always*; default *Never*).

All exports are created without additional software. The program sends nothing – you pass the files on yourself.

---

## Privacy

- **Password fields** are always masked; the dots are visible in the image, and the trace only contains the number of characters.
- **Private key** (Ctrl+Shift+F8): input is only counted, not stored.
- **Password prompts in terminals** (`ssh`, `sudo`, `Read-Host -AsSecureString` …) are recognised from the terminal's text and masked until Enter; the bar shows "Password prompt detected". Everything else typed in a terminal must be reviewed before sharing.
- **Exclusion list:** password managers such as KeePass, 1Password or Bitwarden are never recorded; the list can be extended.
- **Review before export:** conspicuous inputs (keys, IBANs, card numbers, passwords in commands) must be checked before sharing.
- **Redact:** redact or pixelate areas in the image, redact texts everywhere, replace personal values (e.g. user name) with placeholders.
- **Finding personal data in images:** the Windows text recognition searches all images for e-mail addresses, IBANs, card numbers, phone numbers and keys, and offers them for redaction.
- Everything stays on your computer. The program needs no internet and sends no data (exception: the optional update check, which is switched off by default).

---

## For AI and automation

- **AI package:** contains a reading guide for the AI, the complete trace (`trace.md`), all steps as JSON, and the images. Each step states how reliably its effect is documented.
- **MCP server:** AI agents can query recordings directly:
  ```bash
  SebastiansStepRecorder.exe mcp --root "C:\Tools\Sessions"
  ```
  Example for Claude Code (`.mcp.json`):
  ```json
  { "mcpServers": { "steprecorder": { "command": "C:\\Tools\\SebastiansStepRecorder.exe", "args": ["mcp", "--root", "C:\\Tools\\Sessions"] } } }
  ```
- **AutoHotkey:** for each click and entry, the window class, program, control (ClassNN), coordinates and wait signals are recorded. From this an AutoHotkey v2 script is created, or a package with which an AI assistant can improve the script.
- **Command line:**

  | Command | Effect |
  |---|---|
  | `SebastiansStepRecorder.exe` | start window |
  | `SebastiansStepRecorder.exe <recording folder>` | open a recording in the editor |
  | `SebastiansStepRecorder.exe record [--scope screen\|app\|region] [--profile …] [--title …]` | start recording immediately |
  | `SebastiansStepRecorder.exe record --stop` | stop the running recording |
  | `SebastiansStepRecorder.exe export <recording> --format pdf\|docx\|html\|md\|gif\|mp4\|ki\|ahk\|flaui\|pywinauto\|playwright [--lang de\|en\|fr\|es\|it\|nl]` | export |
  | `SebastiansStepRecorder.exe compare <A> <B> [--images]` | compare two recordings (with image differences) |
  | `SebastiansStepRecorder.exe list` · `steps <recording>` | list recordings or steps (`--json`) |
  | `SebastiansStepRecorder.exe testability <recording> --strict` | report controls without an AutomationId (exit 9 for CI) |
  | `SebastiansStepRecorder.exe --help` | all options |

  Instead of a recording path you can always write `latest` – the newest recording.

---

## For software developers

**Bugs with full context.** The *Reproduce bug* mode also records what the program did: program exit with exit code ("crash" only for unambiguous error codes), "not responding", crash data from the event log, product version, build identity and – as additional information – Git branch and commit. At the "Error" and "Expected" markers the visible text of the window is stored (e.g. the error message). Optionally (*Settings → Developer*, off by default, always subject to review): error texts from the event log (.NET stack traces), clipboard content and new lines of explicitly named **log files** (`record --log app.log`).

**Agent loop (MCP).** An agent such as Claude Code gets the failure point with the preceding steps, selectors, wait signals, program context and image in one call with `get_failure_context`. The MCP prompts *analyze bug*, *write UI test*, *write guide* and *verify fix* appear as slash commands. With `--allow-requests` the agent can ask for a re-test recording and compare it with the bug recording using `compare_sessions`. "Connect to AI" creates the setup for Claude Code, VS Code, Cursor and Codex.

**UI tests from recordings.** *Export → UI test* creates a test draft: FlaUI (C#, xUnit), pywinauto (pytest) or Playwright (browser steps only). Selectors by stability (AutomationId before name and type, coordinates only as a marked fallback), waiting for windows instead of fixed pauses, "Expected" and "Error" markers as assertions, passwords only from environment variables. The **testability report** shows which controls lack an AutomationId – in the editor or as a CI step (`testability --strict`). The program itself never replays anything.

**Faster every day.** *Settings → Developer → Stay ready in the notification area* keeps the program reachable so that **Ctrl+Shift+F9** starts a recording at any time (no autostart, nothing captured while idle). `steprec.cmd` next to the EXE lets consoles wait for a recording to finish. Instant screenshot with **Ctrl+Shift+F3**, command palette with **Ctrl+Shift+P**.

**Docs and reviews in the repository.** Markdown with stable names for clean diffs, terminal commands as code blocks, "Copy all steps as Markdown", GIF with a size target and the visual comparison with before/after images for pull requests.

---

## System requirements and notes

- **Windows 10 (version 2004 or later) or Windows 11, 64-bit** (x64; for Windows on ARM there is also `SebastiansStepRecorder-arm64.exe`, not yet tested on ARM devices). No installation, no .NET needed – everything is inside the EXE (approx. 76 MB, with .NET 10).
- **Windows Server and older Windows 10 versions** (e.g. Windows Server 2019) cannot exclude their own windows from screenshots. The program detects this and offers to record **with the display hidden:** bar and frame stay invisible, and operation works via shortcuts and the notification-area icon.
- **Text recognition** (for the search for personal data) uses the OCR built into Windows. If the language pack is missing, you can install it under *Settings → Time & language → Language*.
- **Where is the data?** If the EXE's folder is writable, settings and recordings are stored next to it (folder `Sessions`) – ideal for a USB stick. Otherwise they are under `%APPDATA%\StepRecorder` and `%LOCALAPPDATA%\StepRecorder`. The recordings folder can be changed in the settings.
- **Screen capture:** the program uses the fast Windows desktop duplication (DXGI) and automatically falls back to classic capture if there are problems. Portrait-orientation monitors are captured the classic way.
- **Screen readers:** all buttons – including the icon-only ones – have names for speech output, NVDA and JAWS.
- **Updates:** the program only checks for new versions on request (*Settings → General*); it queries only the public release page, without any identifier or personal data. Nothing is downloaded automatically.
- **Inputs:** dead keys (^, ´, `), the on-screen keyboard, and input from remote-support and password tools are recognized correctly. If Windows removes the input monitoring (e.g. under high load), the program re-installs it immediately and notes the possible gap.
- **Dark or light:** the program starts in the dark theme; under *Settings → Appearance* you can choose "Light" or "Like Windows".
- **Interface language (including English):** Like Windows / Deutsch / English (*Settings → General → Interface language*); applies from the next start. The guide texts can be generated in German or English independently of this.

---

## Frequently asked questions

**Is the recording bar visible in the images?**
No. The program's bar, frame, menus and dialogs are excluded from the screenshots by Windows. Where that is not possible (e.g. Windows Server 2019), the program records with the display hidden.

**Can I extend a recording later?**
Yes: in the editor under "Document" → "Continue recording" or "Append recording". Individual images can be re-captured, and your own steps can be inserted.

**What happens to passwords?**
Entries in password fields are never stored. For passwords outside password fields (e.g. in the terminal) there is the private key Ctrl+Shift+F8.

**Can I pass on a recording without the editor?**
Yes – either as an exported file (PDF, HTML …) or the whole recording folder. Anyone who has the program opens the folder via "Open" (choose `manifest.json` there) or simply drags it onto the start window.

---

## License

[![CC BY-ND 4.0](https://img.shields.io/badge/License-CC%20BY--ND%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nd/4.0/deed.en)

The program and source code are licensed under **[Creative Commons Attribution-NoDerivatives 4.0 International (CC BY-ND 4.0)](https://creativecommons.org/licenses/by-nd/4.0/deed.en)**.

| Allowed | Not allowed |
|---|---|
| using the program free of charge – privately and professionally | distributing or publishing modified versions of the program or source code |
| passing it on unmodified, including within companies | using parts of the source code in your own published projects |
| | giving the impression that the author endorses a distribution or use |

Whenever you pass it on, you must credit the author (Sebedai), link to this repository, and name the license. The license applies to the program and source code – not to the guides, images and packages you create with the program; those belong to you.

The binding license text is in [LICENSE](LICENSE). Third-party components (the .NET runtime from Microsoft, MIT license) are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Bugs and wishes are welcome via [Issues](https://github.com/Sebedai/sebastians-step-recorder/issues).

© 2026 Sebedai
