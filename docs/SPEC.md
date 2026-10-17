# VisaryNotes Product and Technical Specification

Version 0.4 (draft) | Status: for review | Date: 2026-10-17

This document is the single source of truth for what VisaryNotes is, how it looks, and how it is built. Anything not written here is out of scope until it is added here first.

**Change in 0.2.** The app is built entirely in **Visual Studio** (not Android Studio). That means C# and .NET MAUI instead of Kotlin and Jetpack Compose. Every technical section has been rewritten for that toolchain.

**Change in 0.3.** Applies the decisions D1 to D10 from [STRATEGY.md](STRATEGY.md) (VRIO, Theory of Planned Behavior, Innovation Diffusion Theory) and the prototype feedback that the layout wasted space. Reading is now an equal first class job. New sections 4.5 (space budget) and 7.8 (read, extract, write). Layouts, widgets, data model and phases updated.

**Change in 0.4.** Visual design rebuilt on a documented CMF system, colour theory, UX laws and one handed, ambidextrous ergonomics. Full reasoning and sources are in [DESIGN.md](DESIGN.md), which now governs every visual and interaction decision. Sections 3.1, 4, 5 and 9.3 are rewritten to match. Shapes are crisp (0, 2, 4dp plus one chamfer), all actions moved to the bottom of the screen, a handedness setting was added, and the palette was rebuilt so body text reaches 7 to 1 contrast or more in every theme.

---

## 1. Product summary

VisaryNotes is a document reader and a note taking app in one, for Android phones, foldables and tablets. Both jobs are equally important, and the product is built around the loop that connects them.

1. **Read.** Open documents people receive (Markdown, plain text, RTF, ODF) and show them in two ways. A reflowed Reading view that is comfortable on a phone, and an Original view that respects the layout the author intended.
2. **Extract.** Highlight passages and quote them into notes. Every quote keeps a link back to its place in the document.
3. **Write.** Capture and organise notes quickly, with full Markdown and inline images.
4. **Reply.** Save and share in the formats other people use, including the sender's own format.

Notes and documents live together in one **Library**. Strategy analysis behind this framing is in [STRATEGY.md](STRATEGY.md).

The visual identity is a distinct glass style called **Visary Glass**, built on top of Material 3 so the app still feels native to Android.

### 1.1 Goals

| ID | Goal |
| --- | --- |
| G1 | Feels native on current Android while having its own recognizable glass look |
| G2 | One codebase that adapts cleanly to phone, foldable (folded, unfolded, tabletop, book) and tablet |
| G3 | Opens .md, .markdown, .txt, .rtf, .odt with readable and faithful view modes |
| G4 | Exports to Markdown, plain text, RTF, ODT, DOCX, HTML and PDF |
| G5 | Interactive home screen widgets |
| G6 | Several themes that all pass WCAG 2.2 AA contrast for text |
| G7 | Works fully offline, no account, no tracking |
| G8 | Developed, built, debugged and published from Visual Studio on Windows, with very low GitHub Actions usage |
| G9 | Reading and note taking connected by highlights and linked quotes (the read, extract, write loop) |
| G10 | Content first. Chrome takes as little screen space as possible and hides while reading |

### 1.2 Non goals for v1

Cloud sync, collaboration, handwriting recognition, encrypted vaults, full spreadsheet or presentation editing, iOS and Windows builds, Wear OS and Android Auto. These are listed in section 15 as future work. The code is structured so an iOS or Windows build is possible later, because MAUI supports them, but v1 targets Android only.

### 1.3 Assumptions (please confirm or correct)

| ID | Assumption |
| --- | --- |
| A1 | IDE is **Visual Studio 2026 on Windows** (Community edition is enough) with the ".NET Multi-platform App UI development" workload. Not Visual Studio Code |
| A2 | Language C#, UI framework **.NET MAUI on .NET 10** (the current long term support release), Android only target framework `net10.0-android` |
| A3 | Package name `com.visary.notes`, app name "VisaryNotes" |
| A4 | Minimum Android version 8.0 (API 26). Glass blur needs Android 12 (API 31) and degrades gracefully below that |
| A5 | Target Android API 36, the default for .NET 10 ([Microsoft Learn, target frameworks](https://learn.microsoft.com/dotnet/standard/frameworks)). Move to API 37 (Android 17) once the .NET for Android release in use supports it as stable |
| A6 | "ODF" in v1 means ODF Text (.odt and flat .fodt). Spreadsheets (.ods) and presentations (.odp) are view only and come in a later phase |
| A7 | Opening .docx is not required for v1, only saving to it. Viewing .docx is a phase 4 stretch goal |
| A8 | Distribution starts as a signed APK built in Visual Studio and sideloaded, Play Store (AAB) later |

---

## 2. Development environment (Visual Studio only)

### 2.1 Required setup

| Item | Detail |
| --- | --- |
| IDE | Visual Studio 2026 (Windows), workload ".NET Multi-platform App UI development". The MAUI tutorial pairs it with the .NET 10 SDK ([Microsoft, .NET MAUI first app tutorial](https://dotnet.microsoft.com/learn/maui/first-app-tutorial/intro)) |
| Android SDK and JDK | Installed and managed by the Visual Studio installer and **Tools, Android, Android SDK Manager**. Android Studio is not installed or needed |
| Emulators | Created in **Tools, Android, Android Device Manager**. Required profiles are one phone, one foldable and one tablet. Hardware acceleration through Windows Hypervisor Platform |
| Physical devices | USB or wireless debugging, selected from the Visual Studio run target dropdown |
| Hot reload | XAML Hot Reload and .NET Hot Reload for fast UI iteration |
| Tests | Test Explorer runs the xUnit test project, no emulator needed (section 12) |
| Publishing | **Right click project, Publish** in Visual Studio produces a signed APK or AAB with a keystore managed by Visual Studio. No CI needed for a release build |

### 2.2 What this choice means for the design

Everything Android specific that MAUI does not wrap is written in C# under `Platforms/Android`, using the .NET for Android bindings of the Android SDK and AndroidX libraries (NuGet packages named `Xamarin.AndroidX.*`). Android resource XML files (widget layouts, shortcuts, tile icons) live under `Platforms/Android/Resources` and are edited as text in Visual Studio. No Kotlin or Java code is written.

| Concern | Android Studio world | Visual Studio world (what we use) |
| --- | --- | --- |
| UI | Jetpack Compose | .NET MAUI XAML pages and controls, Material 3 enabled |
| Platform code | Kotlin | C# in `Platforms/Android` |
| Widgets | Jetpack Glance | Native `AppWidgetProvider` plus `RemoteViews` in C# |
| Database | Room | sqlite-net with SQLite FTS5 |
| Dependency injection | Hilt | Built in `Microsoft.Extensions.DependencyInjection` |
| Architecture helpers | ViewModel, Flow | CommunityToolkit.Mvvm (`ObservableObject`, `RelayCommand`, messenger) |
| Build | Gradle | MSBuild (`.csproj`), NuGet central package management |

---

## 3. Target devices and adaptive layout

### 3.1 Device classes

Layouts are chosen by window width, never by device type, so the same rules work for split screen, freeform windows, Chromebooks and external displays. The breakpoints follow Android's window size classes.

| Width class | Typical device | Layout |
| --- | --- | --- |
| Compact (under 600dp) | Phone, folded foldable outer screen | Single pane. A passive glance strip at the top (title, counts, page). Every action lives in the bottom glass bar: Library has Continue reading, filters, then Open file, New note (centre) and Search. Reader has the Reading and Original switch, then Library, Find and Notes. The linked note opens as a bottom sheet |
| Medium (600 to 839dp) | Unfolded book style foldable, small tablet portrait | Two panes. Document and its linked note side by side, or Library and detail. Each pane has its own bottom bar. Library opens from the bottom bar, never from a side edge |
| Expanded (840dp and up) | Tablet landscape, large foldable landscape | Three paper panes on the desk: Library, document, linked note. Each pane has its own bottom bar. When a note without a document is open, the middle pane is the editor and the right pane shows backlinks |

**Implementation.** A single `AdaptiveShellPage` hosts the panes. It listens to the window size (`Window.SizeChanged`) and switches between layouts through `VisualStateManager` states driven by MAUI's built in `AdaptiveTrigger` (MinWindowWidth 600 and 840). Pane content is reused across states, so switching layout never recreates the editor or loses its cursor.

### 3.2 Foldable postures

Hinge and posture data come from Jetpack WindowManager (`FoldingFeature`), accessed from C# in one of two ways, decided in phase 0 with a spike.

1. **Preferred.** The `Microsoft.Maui.Controls.Foldable` NuGet package, which provides a `TwoPaneView` and a `UseFoldable()` startup call that hooks into the Android lifecycle to detect hinges ([Microsoft Learn, TwoPaneView](https://learn.microsoft.com/dotnet/maui/user-interface/controls/twopaneview)). Its documentation was last written for older MAUI versions, so it must be verified on .NET 10 first.
2. **Fallback.** A small `FoldService` in `Platforms/Android` that uses the `Xamarin.AndroidX.Window` binding directly and publishes posture and hinge bounds to the shared code.

| Posture | Behaviour |
| --- | --- |
| Flat, unfolded | Normal expanded or medium layout. Panes never straddle a separating hinge |
| Book (vertical hinge, half open) | Document on the left, linked note on the right, split exactly at the hinge. For a plain note, Library on the left and note on the right |
| Tabletop (horizontal hinge, half open) | Document or note on the top half. The bottom half is the Act zone: formatting tools and keyboard, or page thumbnails, view switch and zoom in the viewer |
| Folded (outer screen) | Compact layout. State is preserved across fold and unfold with no reload |

`MainActivity` declares `ConfigurationChanges` for screen size, orientation, screen layout and smallest screen size so folding and rotating do not restart the activity.

### 3.3 Large screen quality checklist

* Continuity across fold, unfold, rotation and resize with no lost text, cursor or scroll position.
* No locked orientation, no fixed aspect ratio. `MainActivity` is resizeable.
* Full keyboard and mouse support. Shortcuts listed in section 9.4, handled by overriding `DispatchKeyEvent` in `MainActivity`. Long press and right click open context menus. Hover states on glass surfaces through `PointerGestureRecognizer`.
* Drag and drop of text and images within the app using MAUI `DragGestureRecognizer` and `DropGestureRecognizer`. Drag and drop of files from other apps through a native `View.IOnDragListener` in phase 3.
* Stylus typing via the system handwriting IME. Ink sketches are future work.
* Multiple windows of the app side by side is a phase 4 stretch item, to be verified against MAUI's Android multi window support.

---

## 4. Visual design

The complete rationale, with sources and measurements, is in [DESIGN.md](DESIGN.md). This section lists only what the build must implement.

### 4.1 CMF tokens

| Material | Finish | Implementation |
| --- | --- | --- |
| Desk | Matte | `Desk` colour token. Background behind panes and behind pages in Original view |
| Paper | Matte, no texture | `Paper` token. Every content surface. Fully opaque |
| Glass | Satin etched: blur 24dp, tint 90 percent, grain 2 percent, 1dp light top edge, 1dp dark bottom edge | `GlassView` control (4.3). Tools only. Text on glass is limited to labels of about four words |
| Ink | Flat | `Ink` and `InkSecondary` tokens. Never semi transparent |
| Accent light | Flat, chamfered | `Accent`, `OnAccent`, `AccentText` tokens. One primary action per screen |

### 4.2 Shape tokens

| Token | Value | Used for |
| --- | --- | --- |
| `Shape.None` | 0dp | Full width paper and bars |
| `Shape.Control` | 2dp | Buttons, segments, inputs, badges, checkboxes |
| `Shape.Pane` | 4dp | Panes on the desk, widgets, sheets |
| `Shape.Chamfer` | one 8dp 45 degree cut, top trailing corner | Primary action only. Sheets use it on the top leading corner |

No pills, circles or radii above 4dp anywhere in the app. In MAUI the chamfer is a `Path` or `RoundRectangle` replacement drawn with MAUI Graphics.

### 4.3 How glass is built in MAUI

MAUI has no built in backdrop blur, so glass is a custom control.

* **`GlassView`** is a MAUI `ContentView` with bindable properties for tint and edge lines. Tint, grain and edge lines are drawn with MAUI Graphics (`GraphicsView` with an `IDrawable`), which works on every Android version.
* **Backdrop blur** is a custom Android handler. On Android 12+ it records the content behind the bar into a `RenderNode` and applies `RenderEffect.CreateBlurEffect` on the GPU. `RenderEffect` set directly on a view blurs the view itself, which is why the `RenderNode` capture step is needed.
* **Evaluate first.** The third party package VitrumMAUI describes the same approach with a tint only fallback below Android 12 ([NuGet, VitrumMAUI](https://www.nuget.org/packages/VitrumMAUI)). Phase 0 compares it with the custom handler and picks one.
* Because tint is 90 percent opaque, contrast does not depend on the blur. Below Android 12, and with Reduce transparency on, glass becomes the solid paper colour with edge lines kept.

### 4.4 Screen zones and one handed rules

From DESIGN.md section 5. The build must follow these rules on every screen.

| Zone | Height | Content |
| --- | --- | --- |
| Glance | Top 44 to 52dp | Passive only: title, counts, page number, save state. Settings may live here because it is rarely used |
| Read | Middle | Content on paper. Full width rows, 56dp minimum in Compact density |
| Act | Bottom bar, up to 30 percent of height | Every frequent action. Primary action centred by default |

* **Handedness setting** Centre (default), Left, Right. Left and Right move the primary action and the fast scroll handle to that side and put the most used paired action nearest that thumb. Implemented as a `Handedness` preference that bottom bar layouts bind to (`Grid.Column` order).
* **No app gestures from the side edges**, because Android uses inward swipes from both edges for Back ([Android Developers, gesture navigation](https://developer.android.com/guide/navigation/gesturenav)).
* Selection menu appears below the selection unless the selection is in the bottom third.
* Menus, pickers and confirmations are bottom sheets with buttons at the bottom.
* Targets 48dp minimum, 56dp for primary actions, 8dp apart ([Google, Touch target size](https://support.google.com/accessibility/android/answer/7101858)).

### 4.5 Space budget (non negotiable)

| Element | Rule |
| --- | --- |
| Glance strip | 44dp in reader and editor, 52dp on the Library. No large page titles |
| Bottom bar | 56dp action row plus optional 44dp segment row plus optional 52dp Continue reading row |
| Reading surface | Edge to edge, 22dp side padding in Reading view, page fills the width in Original view |
| Immersive reading | A tap on the document hides both bars. A second tap shows them |
| List density | Comfortable 64dp, Compact 56dp (default), Dense 48dp without snippet |

### 4.6 Readability rules (non negotiable)

* Body text 7 to 1 or more, secondary text 4.5 to 1 or more, icons and borders 3 to 1 or more, all measured against the worst case backdrop for glass.
* Text never sits on blur alone, and long text never sits on glass.
* No texture under text. Grain only on glass.
* No UI text below 13sp and no weight below 500 under 15sp.
* Reduce transparency turns glass into solid paper. It turns on automatically with system high contrast text and battery saver.

### 4.7 Motion

Short and functional. Sheets slide 200ms, bars fade 150ms. No ambient animation on content screens. Respects the system animation scale.

### 4.8 Typography

| Use | Font | Size and weight |
| --- | --- | --- |
| Reading | Literata | 17sp regular, line height 1.6, about 70 characters per line maximum, user adjustable 14 to 28sp |
| UI | Figtree | 15 to 16sp, 600 for titles, 500 for secondary |
| Data | Figtree, tabular figures | 12 to 13sp, 700 |
| Markdown marks, code | JetBrains Mono | 0.85em |
| Hyperlegible option | Atkinson Hyperlegible | Replaces UI and reading fonts |

All fonts are under the SIL Open Font Licence, bundled in `Resources/Fonts` and registered with `ConfigureFonts`.

---

## 5. Themes

### 5.1 Theme list

Token values live in `tools/design/themes.json`, the single source used by the contrast checker and, at build time, by the generator that writes the MAUI `ResourceDictionary` files.

| Theme | Polarity | Purpose | Ink on paper | Secondary on paper |
| --- | --- | --- | --- | --- |
| Daylight (default) | Light | Everyday reading and writing | 17.1 to 1 | 8.2 to 1 |
| Sepia | Light | Long reading, warmer | 14.1 to 1 | 7.8 to 1 |
| Dusk | Dark grey | Low light, elevation still visible | 14.3 to 1 | 8.9 to 1 |
| Night | Warm dark, amber accent | Very low light | 14.2 to 1 | 8.4 to 1 |
| OLED | True black | Battery, off white text | 16.5 to 1 | 8.9 to 1 |
| High Contrast Light | Light | Accessibility, glass off | 21 to 1 | 15.1 to 1 |
| High Contrast Dark | Dark | Accessibility, glass off | 21 to 1 | 16.8 to 1 |

Daylight is the default because dark text on light backgrounds supported better proofreading performance across age groups (see DESIGN.md 4.3.1).

### 5.2 Theme behaviour

* Themes are MAUI `ResourceDictionary` files generated from `themes.json`, swapped at runtime. All XAML colours use `DynamicResource`.
* Theme mode can follow the system (light follows Daylight, dark follows Dusk), or be fixed.
* **Material You dynamic colour** is offered only as an accent override, and only if the system colour passes the 3 to 1 UI and 4.5 to 1 label checks at runtime. Text colours always come from the theme, because wallpaper colours cannot guarantee contrast.
* The same tokens are emitted as CSS variables for the document viewer and as Android colour resources for widgets.

### 5.3 Contrast checks

`python3 tools/design/contrast.py` checks every pair listed in DESIGN.md 4.3.3 and exits with an error if one falls short. It runs locally and in the CI job. The generated `ResourceDictionary` files are also checked by an xUnit test that reuses the same formula.

---

## 6. Notes and editor

### 6.1 Library and organisation

* **One Library** for notes and documents. Filters All, Notes, Documents, Pinned, Linked. Documents show a type badge (MD, TXT, RTF, ODT) and page count. A "Continue reading" row sits at the top when a document is in progress.
* **Markdown folder import** in phase 1. Point at an existing folder (for example a Markor or Obsidian folder) and its .md files become notes. Optional "work in folder" mode keeps editing those files in place.
* Create, edit, duplicate, delete (with a 30 day recycle bin), pin, archive.
* Folders (nested two levels) and tags (unlimited, colour coded).
* Sort by edited, created, title, or manual drag order.
* Full text search across titles, bodies and imported documents, with highlighted results, using SQLite FTS5.
* Checklists inside notes (Markdown task list syntax).
* Note templates (meeting, journal, to do, blank).
* Version history. The last 20 saves of each note are kept and can be restored.
* Autosave on every pause in typing and on app background. No save button needed for notes.

### 6.2 Editor

* Markdown first editor with live styling. Headings, bold, italic, code, links and lists are styled in place while the raw syntax stays editable.
* **Implementation.** A MAUI `Editor` with a custom Android handler. The handler attaches a `TextWatcher` to the native `EditText` and applies Android spans (`StyleSpan`, `RelativeSizeSpan`, `ForegroundColorSpan`, `TypefaceSpan`) to the changed region only, using the Markdig parser for positions. This keeps typing smooth on long notes.
* Toggle between Edit, Split (on medium and expanded widths) and Preview. Preview uses the document viewer (section 7.3).
* Formatting toolbar on glass, docked above the keyboard on phones and in the bottom half in tabletop posture. Buttons use the 2dp control shape on a neutral fill.
* Insert image from gallery (Android photo picker through `MediaPicker`, no storage permission needed), camera, or clipboard. Images are copied into app storage and referenced with relative paths so the note stays portable.
* Insert table, link, code block, quote, horizontal rule, checklist.
* Undo and redo with deep history, find and replace, word, character and reading time counter.

### 6.3 Markdown support

Parsing uses **Markdig**, a fast CommonMark compliant Markdown processor for .NET with many extensions ([GitHub, xoofx/markdig](https://github.com/xoofx/markdig)).

| Feature | Supported |
| --- | --- |
| CommonMark core | Yes |
| GitHub Flavored Markdown (tables, task lists, strikethrough, autolinks) | Yes (Markdig advanced extensions) |
| Images, local relative paths, content URIs and https URLs | Yes |
| Footnotes | Yes |
| Heading anchors and table of contents | Yes |
| Syntax highlighted code blocks | Yes (bundled highlight.js in the viewer) |
| YAML front matter | Parsed and shown as a metadata card, not as text |
| Math (LaTeX) | Phase 4 (bundled KaTeX in the viewer) |
| Mermaid diagrams | Phase 4, shown as code until then |
| Raw HTML | Sanitized safe subset only (no scripts, no iframes, no event handlers) |

Remote images are loaded only after the user taps "Load remote images" for that document, so opening a file never contacts a server by surprise.

---

## 7. Opening documents

### 7.1 Entry points

* "Open" inside the app using `FilePicker` (Storage Access Framework underneath).
* Android "Open with" and "Share to" from file managers, email, browsers and chat apps. Declared with `[IntentFilter]` attributes on `MainActivity` for `ActionView` and `ActionSend`, handled in `OnNewIntent`.
* Drag and drop onto the app on tablets (phase 3).
* Recent files list with persisted URI permissions (`TakePersistableUriPermission`).

Opened files can be read in place, or imported as a note (a copy the user can edit).

### 7.2 Supported input formats

| Format | Extensions | MIME types handled |
| --- | --- | --- |
| Markdown | .md, .markdown, .mdown, .mkd | text/markdown, text/x-markdown, text/plain (by extension) |
| Plain text | .txt, .text, .log | text/plain |
| Rich Text | .rtf | application/rtf, text/rtf |
| OpenDocument Text | .odt, .fodt | application/vnd.oasis.opendocument.text |
| OpenDocument Spreadsheet (later) | .ods | application/vnd.oasis.opendocument.spreadsheet |
| OpenDocument Presentation (later) | .odp | application/vnd.oasis.opendocument.presentation |

### 7.3 The document viewer

All formats are rendered by one viewer. Each reader converts its file into the shared document model (section 7.6), and an `HtmlRenderer` turns that model into HTML that is displayed in a MAUI `WebView` (Android `WebView`, kept up to date by the Play Store on all supported devices). This gives mature text layout, tables, images, zoom and printing with one code path.

* The viewer is offline and locked down. JavaScript is limited to bundled scripts (highlighting, math), file and network access are disabled, and images are served through a custom `WebViewClient.ShouldInterceptRequest` in the Android handler that resolves note attachments, `content://` URIs and relative paths safely.
* Theme colours and the reading font are injected as CSS variables, so the viewer always matches the app theme.

### 7.4 Two view modes

Every document opens in the viewer with a two way switch at the top.

**Reading view (reflow).** The document model is rendered with the app's reading stylesheet. Fixed page widths, absolute positions and tiny fonts are dropped. Text size, theme and reading font follow the user's settings. This is the default on compact width.

**Original view (fidelity).** The document model is rendered with the author's styles turned into CSS. Page size, margins, fonts (or closest available), colours, alignment, tables, columns, headers and footers are preserved. Pages are laid out as paper sheets on a neutral background with pinch to zoom, fit width and fit page. This is the default on expanded width.

The user's last choice is remembered per format.

### 7.5 Per format rules

**Markdown.** Reading and Original are the same render, Original simply uses the fonts and widths the author's front matter requests if any. Relative image paths resolve against the folder of the file when the app has access to it (using a folder permission if the user grants it, otherwise a one time prompt "Allow access to this folder to show images").

**Plain text.**
* Encoding detection (BOM, then UTF 8 validation, then a fallback for legacy encodings such as Windows 1252 and Shift JIS using `System.Text.Encoding.CodePages`), with a manual encoding picker.
* Line ending detection (LF, CRLF, CR) and preserve on save.
* Reading view wraps lines and uses the reading font. Original view uses a monospace font, no wrapping, horizontal scroll, so ASCII tables and art stay intact.
* Files over 5 MB open in a paged, read only viewer to stay responsive.

**RTF.** Android has no built in RTF support, so VisaryNotes includes its own streaming RTF reader that outputs the document model, based on the Microsoft RTF 1.9.1 specification. The open source RtfPipe library (RTF to HTML for .NET) is used as a reference and test oracle, and may be adopted directly if its licence and Android compatibility check out. Its README warns that complex documents may not match Word exactly ([GitHub, RtfPipe](https://github.com/erdomke/RtfPipe)), which is one reason for owning the reader.
* Supported. Character formatting (font, size, bold, italic, underline, strike, colour, highlight, super and subscript), paragraph formatting (alignment, indents, spacing, tabs), lists, tables (`\trowd`, `\cell`), embedded PNG and JPEG pictures (`\pngblip`, `\jpegblip`), Unicode (`\uN`), code pages (`\ansicpg`), hyperlinks (`HYPERLINK` field), page size and margins, headers and footers.
* Converted with a warning. WMF and EMF pictures (shown as a placeholder in v1).
* Ignored safely. OLE objects and unknown destinations (`\*` groups).

**ODT.** An ODT file is a ZIP package containing `content.xml`, `styles.xml`, `meta.xml` and a `Pictures/` folder, defined by the OASIS OpenDocument 1.3 standard ([OASIS ODF 1.3](https://docs.oasis-open.org/office/OpenDocument/v1.3/)). VisaryNotes reads it with `System.IO.Compression.ZipArchive` and a streaming `XmlReader`, both part of .NET, so no third party dependency is needed.
* Supported. Paragraph and character styles with inheritance, headings, lists, tables (including merged cells), images, hyperlinks, footnotes, page layout (size, margins, orientation), headers and footers, sections with columns, document metadata.
* Fonts. If the requested font is not bundled, a metric compatible substitute is used (for example Carlito for Calibri, Liberation Serif for Times New Roman), and Original view shows a small "Font substituted" notice.
* Flat ODT (.fodt) uses the same parser without the ZIP step.

### 7.6 Internal document model

All readers produce one common tree in `VisaryNotes.Core`, so every viewer and exporter works with every input.

```
Document
  Metadata (title, author, created, modified, page setup)
  Styles (named paragraph and character styles)
  Blocks
    Heading, Paragraph (runs of styled text), ListBlock, Table,
    Image, CodeBlock, Quote, PageBreak, HorizontalRule, Footnote
```

### 7.7 Zero setup open

A file opened from another app (Files, email, chat, browser) goes straight to the reader with no onboarding. The top bar offers "Save to Library" and the selection menu offers "Quote to note". Nothing is added to the Library unless the user chooses to. This is the trial path (STRATEGY.md decision D5).

### 7.8 Read, extract, write

The feature that ties the two jobs together (STRATEGY.md decisions D2 and D3).

* **Select text** in either view. A small glass menu offers Highlight, Quote to note, Copy.
* **Highlight** stores the passage and shows it tinted in both views and in the Library row ("2 highlights").
* **Quote to note** inserts a Markdown blockquote into the linked note, followed by a source link line (document name, page, nearest heading). Tapping the source link opens the document at that passage.
* **Linked note.** Each document can have one primary linked note, created on the first quote. More notes can link to the same document.
* **Where it shows.** Phone, a pull up sheet over the reader (56dp handle, opens to about half height). Foldable book posture, document and note on either side of the hinge. Tablet, third pane.
* **Anchors.** A quote stores the document's file hash, a block path in the document model, character offsets and the quoted text. If the file changes, the anchor is re matched by text search and marked "moved" if it cannot be found exactly.
* **Export.** Notes export with quotes as normal blockquotes and source lines, so they stay readable in any Markdown app.
* **Sidecar option.** Highlights stay in the app database by default and never change the original file. Writing highlights into the file is not planned.

### 7.9 Safety and robustness

* All parsing runs on background threads (`Task.Run`) with size and time limits and a `CancellationToken`. A malformed file shows a friendly error and a "Show as plain text" fallback, never a crash.
* ZIP reading guards against zip bombs (maximum uncompressed size, maximum entry count) and path traversal in entry names.
* `XmlReaderSettings` with `DtdProcessing.Prohibit` and no `XmlResolver`, so external entities are never loaded.
* HTML produced for the viewer is built from the model, never by passing file HTML through, and raw HTML in Markdown is sanitized.
* No document content ever leaves the device unless the user shares or exports it.

---

## 8. Saving and exporting

### 8.1 Native note storage

Notes are stored in SQLite through **sqlite-net-pcl** with the `SQLitePCLRaw.bundle_e_sqlite3` bundle, which includes FTS5 for search. Each note body is stored as Markdown. Images and attachments live in app private storage (`FileSystem.AppDataDirectory`), one folder per note. Settings use MAUI `Preferences`.

### 8.2 Export formats

| Format | Extension | How it is produced | Fidelity |
| --- | --- | --- | --- |
| Markdown | .md | Direct | Full |
| Markdown bundle | .zip | .md plus an `assets/` folder for images, via `ZipArchive` | Full, portable |
| Plain text | .txt | Markdown stripped to text | Text only |
| Rich Text | .rtf | Own RTF writer from the document model, images embedded as `\pngblip` | High |
| OpenDocument Text | .odt | Own ODT writer. ZIP with `mimetype` stored first and uncompressed, then manifest, content, styles, pictures | High |
| Word | .docx | Microsoft's **Open XML SDK** (`DocumentFormat.OpenXml`, MIT licence) ([GitHub, Open XML SDK](https://github.com/dotnet/Open-XML-SDK)) | High |
| HTML | .html | Single self contained file from `HtmlRenderer`, inline CSS and base64 images | Full |
| PDF | .pdf | The viewer's Android `WebView` printed through `CreatePrintDocumentAdapter` and `PrintManager`, which also lets the user print directly | Full, fixed layout |

All writers are covered by round trip tests (write, read back with our own reader, compare the document model) and by golden files checked once by opening them in LibreOffice and Microsoft Word.

### 8.3 Save locations

* "Save as" uses the system file picker (`ACTION_CREATE_DOCUMENT` through a small platform service, or CommunityToolkit.Maui `FileSaver`), so the user chooses any location including cloud drives that provide a document provider.
* "Share" sends the exported file through the Android share sheet (`Share.RequestAsync`).
* Optional "Auto export folder". The user picks a folder once and VisaryNotes keeps a Markdown copy of every note there, which also acts as a simple backup that any sync app can pick up.
* Opened RTF, ODT and TXT files can be saved back to their original location in their original format when the user edits an imported copy and chooses "Save back".

---

## 9. Home screen widgets and system integration

### 9.1 How widgets are built

MAUI has no widget framework, so widgets are written as native Android widgets in C#. A widget is an `AppWidgetProvider` class in `Platforms/Android` that supplies `RemoteViews` built from XML layouts in `Platforms/Android/Resources/layout`. No `.csproj` changes are needed. Microsoft published a guide and a working sample for exactly this in January 2026 ([.NET Blog, How to build Android widgets with .NET MAUI](https://devblogs.microsoft.com/dotnet/how-to-build-android-widgets-with-dotnet-maui/)).

* Only views supported by `RemoteViews` can be used (text, image, buttons, lists via `RemoteViewsService`, and on Android 12+ checkboxes).
* Taps use `PendingIntent`. Ticking a checklist item sends a broadcast to the provider, which updates SQLite and refreshes the widget without opening the app.
* Widgets read data through the same `VisaryNotes.Core` repository as the app, so there is one source of truth.
* When notes change in the app, it calls `AppWidgetManager.NotifyAppWidgetViewDataChanged` so widgets stay current.

### 9.2 Widgets

| Widget | Sizes | Interaction |
| --- | --- | --- |
| Quick Capture | 2x1, 4x1 | Buttons for new text note, new checklist, new photo note, voice to text note. Opens straight into the editor |
| Continue Reading | 2x2 up to 4x2 | Last opened document with page and progress bar. Tap reopens it at the same position |
| Pinned Note | 2x2 up to 4x4, resizable | Shows a chosen note (simple Markdown styling via spans). Tap to open. Scrollable |
| Checklist | 2x2 up to 4x5, resizable | Tick and untick items on the home screen without opening the app. Add item button |
| Recent Notes | 4x2 up to 4x4 | List of latest notes, tap to open, plus a new note button |

Each configurable widget has a small configuration activity (chosen note, theme).

### 9.3 Widget styling

Home screen widgets cannot blur the wallpaper behind them, so they use paper at 94 percent opacity with the cut edge lines, drawn with Android shape and layer list drawables in XML. Inner controls use the 2dp control shape and the chamfered primary action. The launcher may apply its own corner radius to the widget's outer background on Android 12 and newer, which is accepted. Widgets follow the app theme, chosen in the configuration screen. Users are encouraged (in the widget picker preview) to place widgets in the lower half of the home screen, inside thumb reach.

### 9.4 Other system integration

| Feature | How in .NET for Android |
| --- | --- |
| Quick Settings tile "New note" | `TileService` subclass with `[Service]` attribute |
| App shortcuts (long press icon) | Static shortcuts XML plus `ShortcutManagerCompat` for dynamic "last opened note" |
| Share target (text, links, images become notes) | `[IntentFilter]` for `ActionSend` and `ActionSendMultiple` |
| Optional quick capture notification | Notification channel, off by default |
| Predictive back | Enabled in the manifest, back handled through MAUI navigation |
| Per app language | `locales_config.xml` resource |

### 9.5 Keyboard shortcuts (tablets and Chromebooks)

| Shortcut | Action |
| --- | --- |
| Ctrl N | New note |
| Ctrl O | Open file |
| Ctrl S | Save or export current note |
| Ctrl Shift S | Save as |
| Ctrl F | Find in note |
| Ctrl Shift F | Search all notes |
| Ctrl B, Ctrl I, Ctrl U | Bold, italic, underline |
| Ctrl K | Insert link |
| Ctrl P | Print or export PDF |
| Ctrl Slash | Show shortcut help |

---

## 10. Accessibility

* TalkBack support with `SemanticProperties.Description` and `SemanticProperties.HeadingLevel` on all controls. The WebView viewer uses real HTML headings and table markup so TalkBack reads structure correctly.
* All touch targets at least 48dp.
* Font scale up to 200 percent without clipping or overlapping.
* Reduce transparency setting and respect for the system animation scale (sections 4.6 and 4.7).
* Never use colour alone to show state (tags also show a name, checked items are also struck through).
* Switch Access and keyboard navigation with visible focus rings on glass.

---

## 11. Architecture

### 11.1 Solution layout (open `VisaryNotes.sln` in Visual Studio)

```
VisaryNotes.sln
  src/
    VisaryNotes.App/                 .NET MAUI app, net10.0-android
      App.xaml, MauiProgram.cs       Startup, DI, fonts, UseMaterial3
      Views/                         XAML pages (Shell, NoteList, Editor, Viewer, Settings)
      ViewModels/                    CommunityToolkit.Mvvm view models
      Controls/                      GlassView, ChamferButton, BottomBar, AdaptiveShell
      Resources/
        Fonts/  Images/  Raw/        Bundled fonts, icons, viewer CSS and JS
        Styles/Themes/               Generated theme ResourceDictionaries
      Platforms/Android/
        MainActivity.cs              Intents, keyboard shortcuts, config changes
        Handlers/                    Glass blur, Markdown editor spans, WebView client
        Widgets/                     AppWidgetProviders, RemoteViewsServices
        Services/                    Fold posture, file save, dynamic colour, tile
        Resources/                   Widget layouts, drawables, shortcuts, xml
    VisaryNotes.Core/                net10.0 class library, no UI, no Android
      Model/                         Note, Folder, Tag, Document model
      Data/                          sqlite-net repositories, FTS
      Formats/                       Readers and writers (md, txt, rtf, odt, docx, html)
      Theming/                       Theme definitions and contrast maths
  tests/
    VisaryNotes.Core.Tests/          xUnit, net10.0, runs in Test Explorer
      TestFiles/                     Sample md, txt, rtf, odt corpus and golden outputs
  Directory.Packages.props           Central NuGet versions
  .editorconfig
```

Keeping formats, data and theming in a plain `net10.0` library means most logic is tested in seconds on Windows without any emulator, and stays reusable if an iOS or Windows build is added later.

### 11.2 Packages

| Area | Package | Licence |
| --- | --- | --- |
| UI framework | Microsoft.Maui.Controls (10.0.60 or later for full Material 3) | MIT |
| MVVM | CommunityToolkit.Mvvm | MIT |
| MAUI helpers (FileSaver, popups) | CommunityToolkit.Maui | MIT |
| Database | sqlite-net-pcl, SQLitePCLRaw.bundle_e_sqlite3 | MIT, Apache 2.0 |
| Markdown | Markdig | BSD 2 clause |
| DOCX export | DocumentFormat.OpenXml | MIT |
| Legacy encodings | System.Text.Encoding.CodePages | MIT |
| Foldables | Microsoft.Maui.Controls.Foldable or Xamarin.AndroidX.Window | MIT |
| Glass (if adopted) | VitrumMAUI | to verify |
| RTF reference (if adopted) | RtfPipe | to verify |

Versions are pinned in `Directory.Packages.props` to the latest stable releases on the day the solution is created, and updated deliberately through the Visual Studio NuGet manager, not automatically. One known issue to watch. A third party write up reports Android status bar icons becoming hard to read with Material 3 on Microsoft.Maui.Controls 10.0.100 ([startdebugging.net](https://startdebugging.net/ja/2026/09/fix-android-status-bar-icons-unreadable-after-enabling-usematerial3-in-maui-10/)), so the chosen version is checked for that before pinning.

### 11.3 Data model (v1)

```
Note        Id, Title, Body (markdown), FolderId, Color, Pinned, Archived,
            CreatedAt, UpdatedAt, DeletedAt
Folder      Id, Name, ParentId, SortOrder
Tag         Id, Name, Color
NoteTag     NoteId, TagId
Attachment  Id, NoteId, FileName, MimeType, Size
NoteVersion Id, NoteId, Body, SavedAt
Document    Id, Uri, DisplayName, MimeType, FileHash, PageCount, LastPosition,
            LastOpenedAt, ViewMode, InLibrary
Highlight   Id, DocumentId, BlockPath, StartOffset, EndOffset, Text, Color,
            CreatedAt, AnchorState
NoteLink    Id, NoteId, DocumentId, HighlightId (nullable), IsPrimary
NoteSearch  FTS5 virtual table over Title and Body
DocSearch   FTS5 virtual table over extracted document text
```

### 11.4 Permissions

Only what is strictly needed. No storage permissions (Storage Access Framework and the photo picker are used instead). Camera capture goes through `MediaPicker`, which uses the system camera app. `POST_NOTIFICATIONS` only if the user enables the quick capture notification. `INTERNET` is only added if remote Markdown images stay in scope (open question 2), and remote loading is opt in per document.

---

## 12. Testing

| Layer | Tool | Where it runs |
| --- | --- | --- |
| Format readers and writers (round trip and golden files) | xUnit in `VisaryNotes.Core.Tests` | Visual Studio Test Explorer, and CI |
| Theme contrast checks | `tools/design/contrast.py` plus an xUnit test on generated resources | Locally, Test Explorer, and CI |
| One handed reach and legibility | Study with left and right handed participants, DESIGN.md section 9 | Real phones, before beta |
| Repositories and FTS search | xUnit with an in memory or temp file SQLite database | Test Explorer, and CI |
| UI on phone, foldable and tablet | Manual run on Android Device Manager emulators and real devices | Visual Studio, local only |
| Widgets, fold postures, TalkBack | Manual checklist before each release | Real devices or emulators |

A UI automation suite (Appium with the .NET client) is optional in phase 4 and runs locally only.

---

## 13. Performance targets

| Metric | Target on a mid range phone |
| --- | --- |
| Cold start to note list (release build) | under 1.5s |
| Open a 1 MB Markdown file | first screen under 700ms |
| Open a 5 MB ODT with images | first page under 2s |
| Typing latency in editor | no visible lag on 10,000 word notes |
| Search across 5,000 notes | under 150ms |
| APK size (release, trimmed, single ABI) | under 40 MB, measured and tracked from phase 0 |

Release builds use trimming and the .NET for Android AOT defaults to keep startup fast. Startup is measured from phase 0 so regressions are caught early. Glass capture blur is the main rendering cost, so it is limited to chrome surfaces and reduced automatically on thermal throttling.

---

## 14. Low cost CI and delivery

### 14.1 Key facts

* The repository `EmeraldWall/VisaryNotes` is currently **public**. GitHub does not charge Actions minutes for standard GitHub hosted runners on public repositories ([GitHub Docs, Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions)). If the repo becomes private, Linux runners count at 1x while Windows counts at 2x and macOS at 10x (same source).
* .NET MAUI Android apps can be built on Linux by installing the `maui-android` workload ([Microsoft Learn, MAUI installation](https://learn.microsoft.com/dotnet/maui/get-started/installation)). So CI does not need a Windows runner, even though development happens in Visual Studio on Windows.

### 14.2 Rules

1. **Visual Studio is the primary build machine.** Debug, tests and signed release APKs all come from Visual Studio on the developer's PC. CI is a safety net, not a requirement.
2. **One workflow, one Linux job.** `ubuntu-latest`, `actions/setup-dotnet` for .NET 10, `dotnet workload install maui-android`, run `VisaryNotes.Core.Tests`, then `dotnet build -f net10.0-android`. No matrix, no emulator. The workload install step is verified on the first run.
3. **Manual or tag triggered only.** `workflow_dispatch` and version tags (`v*`), not every push. Optionally pull requests to `main` with path filters so documentation changes never trigger a build.
4. **Cancel superseded runs** with a `concurrency` group and `cancel-in-progress: true`.
5. **Caching** of NuGet packages and the .NET workload.
6. **Short timeout** (`timeout-minutes: 25`) so a hung build cannot burn minutes.
7. **Artifacts.** Only the APK is uploaded, with a short retention period. The release keystore never goes into the repo. Signed releases are produced in Visual Studio.

---

## 15. Delivery phases

| Phase | Scope | Exit criteria |
| --- | --- | --- |
| 0. Foundation | Solution in Visual Studio, Core library and tests, Material 3 on, GlassView with blur spike (custom handler vs VitrumMAUI), fold spike (Foldable package vs AndroidX Window), themes generated from themes.json (Daylight, Dusk, High Contrast), handedness setting, bottom bar layouts, adaptive shell, CI workflow running the contrast check | App runs from Visual Studio on phone, foldable and tablet emulators with glass bars, correct pane layouts and passing contrast checks |
| 1. Library core | SQLite storage, unified Library with density setting, folders, tags, Markdown editor with live spans and images, search, trash, autosave, Markdown folder import | Daily usable as a notes app, existing Markdown notes imported |
| 2. Reader and loop | TXT, MD, RTF, ODT readers, document model, Reading and Original views, immersive mode, zero setup open with, highlights, Quote to note, linked notes, test corpus in the repo | Corpus of 50 real world files opens without crash. A quote round trip (select, quote, tap source) works on phone, foldable and tablet |
| 3. Reply and widgets | All export formats and Save back, five widgets, Quick Settings tile, shortcuts, remaining themes, drag and drop from other apps | Round trip tests green, widgets interactive on Android 12+ |
| 4. Beta and polish | Beta with 15 to 30 testers using the TPB and diffusion questionnaires in STRATEGY.md section 5, accessibility audit, performance pass, version history UI | Release candidate |
| Later | .ods and .odp viewing, .docx viewing, math and Mermaid, multi window | After v1 (STRATEGY.md decision D10) |

---

## 16. Future work (not in v1)

iOS and Windows builds from the same solution, optional sync through user chosen folders or WebDAV, end to end encrypted notes, handwriting and sketch layer, Wear OS quick capture, collaborative editing, on device AI summaries.

---

## 17. Open questions

1. Is the package name `com.visary.notes` acceptable, and is there a logo or brand colour beyond the emerald theme suggested by the organisation name?
2. Should remote images in Markdown be supported at all (requires the internet permission), or should the app be fully offline by design?
3. Is .docx viewing needed earlier than phase 4?
4. Are the proposed open licence fonts (Inter, Literata, JetBrains Mono, Atkinson Hyperlegible) acceptable?
5. Will the repository stay public? This decides whether CI cost is a concern at all.
6. Which Visual Studio edition and version is installed, and do you have a physical Android phone, foldable or tablet for testing, or emulators only?

---

## Appendix A. References

* VisaryNotes design rationale (CMF, colour theory, UX laws, ergonomics). [DESIGN.md](DESIGN.md)

* VisaryNotes strategy analysis (VRIO, TPB, IDT). [STRATEGY.md](STRATEGY.md)

* Microsoft Learn, .NET target frameworks (default Android API per .NET version). https://learn.microsoft.com/dotnet/standard/frameworks
* Microsoft, .NET MAUI first app tutorial (Visual Studio setup). https://dotnet.microsoft.com/learn/maui/first-app-tutorial/intro
* Microsoft Learn, .NET MAUI installation (Linux `maui-android` workload). https://learn.microsoft.com/dotnet/maui/get-started/installation
* .NET Blog, Material 3 for .NET MAUI. https://devblogs.microsoft.com/dotnet/dotnet-maui-material-3/
* .NET Blog, How to build Android widgets with .NET MAUI. https://devblogs.microsoft.com/dotnet/how-to-build-android-widgets-with-dotnet-maui/
* Microsoft Learn, .NET MAUI TwoPaneView. https://learn.microsoft.com/dotnet/maui/user-interface/controls/twopaneview
* NuGet, VitrumMAUI. https://www.nuget.org/packages/VitrumMAUI
* Markdig. https://github.com/xoofx/markdig
* RtfPipe. https://github.com/erdomke/RtfPipe
* Open XML SDK. https://github.com/dotnet/Open-XML-SDK
* OASIS OpenDocument 1.3. https://docs.oasis-open.org/office/OpenDocument/v1.3/
* W3C WCAG 2.2. https://www.w3.org/TR/WCAG22/
* GitHub Docs, Actions billing. https://docs.github.com/en/billing/concepts/product-billing/github-actions
