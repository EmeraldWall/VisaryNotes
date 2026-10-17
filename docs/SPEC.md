# VisaryNotes Product and Technical Specification

Version 0.3 (draft) | Status: for review | Date: 2026-10-17

This document is the single source of truth for what VisaryNotes is, how it looks, and how it is built. Anything not written here is out of scope until it is added here first.

**Change in 0.2.** The app is built entirely in **Visual Studio** (not Android Studio). That means C# and .NET MAUI instead of Kotlin and Jetpack Compose. Every technical section has been rewritten for that toolchain.

**Change in 0.3.** Applies the decisions D1 to D10 from [STRATEGY.md](STRATEGY.md) (VRIO, Theory of Planned Behavior, Innovation Diffusion Theory) and the prototype feedback that the layout wasted space. Reading is now an equal first class job. New sections 4.5 (space budget) and 7.8 (read, extract, write). Layouts, widgets, data model and phases updated.

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
| Compact (under 600dp) | Phone, folded foldable outer screen | Single pane. Library list, tap opens the reader or editor full screen. No tab bar. A slim bottom action dock (Open file, voice, New note) on the Library only. While reading, a pull up sheet shows the linked note |
| Medium (600 to 839dp) | Unfolded book style foldable, small tablet portrait | Two panes. Document and its linked note side by side, or Library and detail. Library opens as a drawer from the top bar |
| Expanded (840dp and up) | Tablet landscape, large foldable landscape | Three panes. Library list, document, linked note. When a note without a document is open, the middle pane is the editor and the right pane shows backlinks |

**Implementation.** A single `AdaptiveShellPage` hosts the panes. It listens to the window size (`Window.SizeChanged`) and switches between layouts through `VisualStateManager` states driven by MAUI's built in `AdaptiveTrigger` (MinWindowWidth 600 and 840). Pane content is reused across states, so switching layout never recreates the editor or loses its cursor.

### 3.2 Foldable postures

Hinge and posture data come from Jetpack WindowManager (`FoldingFeature`), accessed from C# in one of two ways, decided in phase 0 with a spike.

1. **Preferred.** The `Microsoft.Maui.Controls.Foldable` NuGet package, which provides a `TwoPaneView` and a `UseFoldable()` startup call that hooks into the Android lifecycle to detect hinges ([Microsoft Learn, TwoPaneView](https://learn.microsoft.com/dotnet/maui/user-interface/controls/twopaneview)). Its documentation was last written for older MAUI versions, so it must be verified on .NET 10 first.
2. **Fallback.** A small `FoldService` in `Platforms/Android` that uses the `Xamarin.AndroidX.Window` binding directly and publishes posture and hinge bounds to the shared code.

| Posture | Behaviour |
| --- | --- |
| Flat, unfolded | Normal expanded or medium layout. Panes never straddle a separating hinge |
| Book (vertical hinge, half open) | Document on the left, linked note on the right, split exactly at the hinge. For a plain note, Library on the left and note on the right |
| Tabletop (horizontal hinge, half open) | Document or note on the top half, formatting toolbar and keyboard area on the bottom half. In viewer mode, the bottom half shows page thumbnails and zoom controls |
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

## 4. Visual design: Visary Glass

### 4.1 Design intent

Material 3 provides the structure (components, shapes, colour roles). MAUI 10 supports Material 3 on Android through one project property, `<UseMaterial3>true</UseMaterial3>`, with Microsoft recommending Microsoft.Maui.Controls 10.0.60 or later ([.NET Blog, Material 3 for .NET MAUI](https://devblogs.microsoft.com/dotnet/dotnet-maui-material-3/)). Visary Glass provides the surface treatment on top. The look must be clearly glass but must not imitate Apple's Liquid Glass.

| Visary Glass | What it avoids |
| --- | --- |
| Frosted, matte panes with a fine film grain, like etched glass | Glossy, water like lensing and liquid morphing |
| Colour comes from the theme ink that tints the glass from inside | Clear, colourless panes that only refract |
| A single thin "edge glint" line along the top and leading edge, angled like light through a prism | Large specular highlights that move with device tilt |
| Faceted corners (mixed corner radii, cut corners on primary actions) | Uniform pill and capsule shapes everywhere |
| A slow "aurora" backdrop of two or three soft blobs generated from the theme seed colour | Wallpaper see through effects |

### 4.2 Glass layers

Every glass surface is composed of five layers, top to bottom.

1. **Content** (text, icons). Always drawn at full opacity.
2. **Edge glint**. 1dp gradient stroke, 40 percent to 0 percent white (light themes use theme ink instead of white).
3. **Grain**. A tiled 128px noise texture at 3 to 5 percent opacity, so the glass does not look like flat transparency.
4. **Tint**. Theme surface colour at 55 to 80 percent opacity depending on elevation. Higher elevation means more opaque.
5. **Backdrop blur**. 16 to 32dp radius of whatever sits behind the pane.

### 4.3 How glass is built in MAUI

MAUI has no built in backdrop blur, so the glass is a custom control.

* **`GlassView`** is a MAUI `ContentView` with bindable properties for elevation, tint, corner shape and glint. Layers 2 to 4 are drawn with MAUI Graphics (`GraphicsView` with an `IDrawable`), which works on every Android version.
* **Backdrop blur (layer 5)** is done by a custom Android handler for `GlassView`. On Android 12+ it records the content behind the pane into a `RenderNode` and applies `RenderEffect.CreateBlurEffect`, which runs on the GPU. Note that `RenderEffect` set directly on a view blurs the view itself, not what is behind it, which is why the `RenderNode` capture step is needed.
* **Shortcut for the aurora.** Most glass sits over the aurora backdrop, which the app generates itself. For those panes no capture is needed. The handler simply draws a pre softened copy of the aurora clipped to the pane, which is almost free. Real capture blur is only used for panes over scrolling content (top bar, bottom bar, formatting toolbar).
* **Evaluate first.** The third party NuGet package VitrumMAUI describes the same RenderNode and RenderEffect approach, with a tint only fallback below Android 12 ([NuGet, VitrumMAUI](https://www.nuget.org/packages/VitrumMAUI)). Phase 0 compares it with the custom handler on performance, licence and maintenance, and picks one.

### 4.4 Elevation levels

| Level | Use | Tint opacity | Blur |
| --- | --- | --- | --- |
| Glass 0 | Aurora backdrop | n/a | n/a |
| Glass 1 | Library list panel (one panel holding all rows, not one card per row) | 60 percent | aurora copy only (no capture) |
| Glass 2 | Top bar, bottom bar, rail | 65 percent | 24dp capture |
| Glass 3 | Sheets, dialogs, menus, formatting toolbar | 80 percent | 32dp capture |

### 4.5 Space budget (non negotiable)

Driven by prototype feedback and decision D4 in STRATEGY.md. Content gets the screen. Glass is a thin frame around it.

| Element | Rule |
| --- | --- |
| Top bar | One floating glass bar, 52dp tall, 8dp from the edges. It holds search on the Library and the file name plus view switch in the reader. No large page titles |
| Bottom | No permanent tab bar. Library shows a 60dp action dock. Reader and editor show nothing at the bottom except the 56dp linked note handle (reader) or the formatting bar docked to the keyboard (editor) |
| Reading surface | Edge to edge, 20dp side padding in Reading view, 8dp around the page in Original view |
| Immersive reading | A tap on the document hides all chrome. A second tap shows it. Scrolling down hides it automatically (setting) |
| List density | Comfortable, Compact (default) and Dense. Compact fits about ten Library rows on a 6.1 inch phone. Dense hides the snippet line |
| Gaps | 8dp outer margin, 6 to 8dp between chrome elements. No decorative spacing |
| Panes on large screens | No navigation rail. Panes sit 8dp apart and use the full height |

### 4.6 Readability rules (non negotiable)

* Text is never placed directly on blur alone. There is always a tint layer whose opacity guarantees the contrast targets in section 5.3 against the worst case backdrop.
* The editor and document viewer body area is **solid**, not glass. Glass is used for chrome (bars, cards, sheets, toolbars) only. Long form reading happens on an opaque "paper" surface.
* A "Reduce transparency" setting replaces all glass with solid tinted surfaces. It is turned on automatically when the system high contrast text setting is on, and when battery saver is active.
* A "Reduce motion" setting stops the aurora animation and shortens transitions. It follows the system animation scale by default.

### 4.7 Motion

Container transitions use MAUI animation APIs with spring like easing. Glass panes fade their blur in over 150ms when they appear so the screen does not flash. The aurora drifts very slowly (one full cycle per 60 seconds) and pauses when the app is in the background.

### 4.8 Fallbacks by Android version

| Android version | Glass treatment |
| --- | --- |
| 12 and newer (API 31+) | Full five layer glass with GPU backdrop blur |
| 8 to 11 (API 26 to 30) | Tint, grain, edge glint and the pre softened aurora. No capture blur |
| Any, with Reduce transparency on | Solid surfaces, edge glint kept as a subtle border |

### 4.9 Typography

* Fonts are bundled in `Resources/Fonts` and registered with `ConfigureFonts` in `MauiProgram`.
* UI font proposal: Inter (SIL Open Font Licence), fallback Roboto.
* Reading font options for notes and documents: Sans (Inter), Serif (Literata), Mono (JetBrains Mono), and Hyperlegible (Atkinson Hyperlegible) for readers who need it. All are under the SIL Open Font Licence, which allows bundling.
* Default body size 17 in the editor and reflow viewer, line height 1.5, user adjustable from 14 to 28. Respects system font scale up to 200 percent without clipping.
* Maximum line length in reflow mode about 70 characters on tablets. Extra width becomes margin, not longer lines.

---

## 5. Themes

### 5.1 Theme list

Each theme defines a seed colour, a backdrop (aurora palette), light and dark variants where it makes sense, and a reading surface colour.

| Theme | Mood | Variants | Notes |
| --- | --- | --- | --- |
| Dynamic | Follows the wallpaper via Material You dynamic colour | Light, Dark | Default on Android 12+. Colours read through the Android `system_accent` resources in a platform service |
| Emerald Mist | Deep green glass, soft mint ink | Light, Dark | Brand theme and default below Android 12 |
| Paper | Warm off white, very low glare, minimal glass | Light | For long reading sessions |
| Midnight Ink | Navy and slate, low blue | Dark | Night writing |
| Sepia Study | Warm brown on cream | Light, Dark | Reading documents |
| Graphite | Neutral greys, no hue | Light, Dark | Distraction free |
| True Black | Pure black for OLED, glass reduced to outlines | Dark | Battery saving |
| High Contrast | Maximum contrast, glass off | Light, Dark | Accessibility |

### 5.2 Theme behaviour

* Themes are MAUI `ResourceDictionary` files generated from JSON theme definitions in `VisaryNotes.Core`, swapped at runtime by replacing the merged dictionary. All colours in XAML use `DynamicResource` so a theme switch is instant with no restart.
* Theme mode can follow the system (`Application.RequestedThemeChanged`), or be forced light or dark.
* Optional scheduled dark mode (sunset to sunrise using local time only, no location permission).
* Per note accent colour that tints the note card glass and the widget, without changing text colours.
* The same theme tokens are emitted as CSS variables for the document viewer (section 7.3) and as Android colour resources for widgets, so all three surfaces match.

### 5.3 Contrast targets

Every theme must pass automated checks in unit tests.

| Element | Minimum contrast | Reference |
| --- | --- | --- |
| Body text and editor text | 4.5 to 1 (target 7 to 1 for Paper, Sepia, High Contrast) | WCAG 2.2 SC 1.4.3 and 1.4.6 ([W3C](https://www.w3.org/TR/WCAG22/)) |
| Large text (24sp+, or 19sp+ bold) | 3 to 1 | WCAG 2.2 SC 1.4.3 |
| Icons, focus rings, input borders | 3 to 1 | WCAG 2.2 SC 1.4.11 |

Glass contrast is tested against the worst case backdrop (the lightest and darkest aurora blob of the theme), not the average.

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
* Formatting toolbar on a Glass 3 surface, docked above the keyboard on phones and in the bottom half in tabletop posture.
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

Home screen widgets cannot blur the wallpaper behind them, so they use the "glass without blur" treatment. Theme tint at 70 to 85 percent opacity, grain, edge glint and rounded corners, all drawn with Android shape and layer list drawables in XML. Widgets follow the app theme or the system dynamic colours, chosen in the configuration screen. Preview layouts are provided for the widget picker.

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
* Reduce transparency and Reduce motion settings (section 4.6).
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
      Controls/                      GlassView, AuroraBackground, AdaptiveShell
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
| Theme contrast checks | xUnit | Test Explorer, and CI |
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
| 0. Foundation | Solution in Visual Studio, Core library and tests, Material 3 on, GlassView with blur spike (custom handler vs VitrumMAUI), fold spike (Foldable package vs AndroidX Window), 3 themes (Dynamic, Emerald Mist, Paper), adaptive shell, CI workflow | App runs from Visual Studio on phone, foldable and tablet emulators with glass chrome and correct pane layouts |
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
