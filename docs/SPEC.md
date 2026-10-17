# VisaryNotes Product and Technical Specification

Version 0.1 (draft) | Status: for review | Date: 2026-10-17

This document is the single source of truth for what VisaryNotes is, how it looks, and how it is built. Anything not written here is out of scope until it is added here first.

---

## 1. Product summary

VisaryNotes is a note taking app and document reader for Android phones, foldables and tablets. It has three jobs.

1. Capture and organize notes quickly, with full Markdown and inline images.
2. Open documents people receive (Markdown, plain text, RTF, ODF) and show them in two ways. A reflowed reading view that is comfortable on a phone, and a faithful view that respects the layout the author intended.
3. Save and share notes in the formats people actually use.

The visual identity is a distinct glass style called **Visary Glass**, built on top of current Material 3 guidance so the app still feels native to Android.

### 1.1 Goals

| ID | Goal |
| --- | --- |
| G1 | Feels native on Android 17 while having its own recognizable glass look |
| G2 | One codebase that adapts cleanly to phone, foldable (folded, unfolded, tabletop, book) and tablet |
| G3 | Opens .md, .markdown, .txt, .rtf, .odt with readable and faithful view modes |
| G4 | Exports to Markdown, plain text, RTF, ODT, DOCX, HTML and PDF |
| G5 | Interactive home screen widgets |
| G6 | Several themes that all pass WCAG 2.2 AA contrast for text |
| G7 | Works fully offline, no account, no tracking |
| G8 | Very low GitHub Actions usage during development |

### 1.2 Non goals for v1

Cloud sync, collaboration, handwriting recognition, encrypted vaults, full spreadsheet or presentation editing, Wear OS and Android Auto. These are listed in section 14 as future work.

### 1.3 Assumptions (please confirm or correct)

| ID | Assumption |
| --- | --- |
| A1 | Package name `com.visary.notes`, app name "VisaryNotes" |
| A2 | Kotlin and Jetpack Compose only, no XML layouts except where the platform demands it |
| A3 | minSdk 26 (Android 8.0). Glass effects degrade gracefully below Android 12 |
| A4 | compileSdk and targetSdk set to the newest stable platform at scaffold time (Android 17, API 37) |
| A5 | "ODF" in v1 means ODF Text (.odt and flat .fodt). Spreadsheets (.ods) and presentations (.odp) are view only and come in a later phase |
| A6 | Opening .docx is not required for v1, only saving to it. Viewing .docx is a phase 4 stretch goal |
| A7 | Distribution starts as a sideloaded APK built locally, Play Store later |

---

## 2. Target devices and adaptive layout

### 2.1 Device classes

Layouts are chosen by window size class, never by device type, so the same rules work for split screen, freeform windows, Chromebooks and external displays.

| Width class | Typical device | Layout |
| --- | --- | --- |
| Compact (under 600dp) | Phone, folded foldable outer screen | Single pane. Note list, tap opens editor full screen. Bottom glass navigation bar |
| Medium (600 to 839dp) | Unfolded book style foldable, small tablet portrait | List and detail side by side when the hinge allows, otherwise single pane. Navigation rail |
| Expanded (840dp and up) | Tablet landscape, large foldable landscape | Three panes. Folders, note list, editor or viewer. Navigation rail or permanent drawer |

Implementation uses `NavigableListDetailPaneScaffold` from `androidx.compose.material3.adaptive`, which shows list and detail side by side on large windows and one pane at a time on small ones, with predictive back built in ([Android Developers, Build a list detail layout](https://developer.android.com/develop/adaptive-apps/guides/list-detail)).

### 2.2 Foldable postures

Posture and hinge data come from the low level `adaptive` artifact (`Posture`, `HingeInfo`) and Jetpack WindowManager `FoldingFeature`.

| Posture | Behaviour |
| --- | --- |
| Flat, unfolded | Normal expanded or medium layout. Panes never straddle a separating hinge |
| Book (vertical hinge, half open) | List on the left, editor on the right, split exactly at the hinge |
| Tabletop (horizontal hinge, half open) | Document or note on the top half, formatting toolbar and keyboard area on the bottom half. In viewer mode, the bottom half shows page thumbnails and zoom controls |
| Folded (outer screen) | Compact layout. State is preserved across fold and unfold with no reload |

### 2.3 Large screen quality checklist

* Continuity across fold, unfold, rotation and resize with no lost text, cursor or scroll position.
* No locked orientation, no fixed aspect ratio. The app must stay correct when the platform ignores orientation and resizability requests on large screens.
* Full keyboard and mouse support. Shortcuts listed in section 8.4. Right click context menus. Hover states on glass surfaces.
* Drag and drop of text, images and files in and out of the app, including between split screen apps.
* Stylus input in the editor (basic ink sketches are a phase 4 item, plain stylus typing via handwriting to text is handled by the system IME).
* Multi instance. Two notes can be open in two windows side by side on tablets.

---

## 3. Visual design: Visary Glass

### 3.1 Design intent

Material 3 provides the structure (components, motion, shape scale, dynamic color). Visary Glass provides the surface treatment. The look must be clearly glass but must not imitate Apple's Liquid Glass. The differences are deliberate.

| Visary Glass | What it avoids |
| --- | --- |
| Frosted, matte panes with a fine film grain, like etched glass | Glossy, water like lensing and liquid morphing |
| Colour comes from the theme ink that tints the glass from inside | Clear, colourless panes that only refract |
| A single thin "edge glint" line along the top and leading edge, angled like light through a prism | Large specular highlights that move with device tilt |
| Faceted corners using Material 3 Expressive shapes (mixed corner radii, cut corners on primary actions) | Uniform pill and capsule shapes everywhere |
| A slow "aurora" backdrop of two or three soft blobs generated from the theme seed colour | Wallpaper see through effects |

### 3.2 Glass layers

Every glass surface is composed of five layers, top to bottom.

1. **Content** (text, icons). Always drawn at full opacity.
2. **Edge glint**. 1dp gradient stroke, 40 percent to 0 percent white (light themes use theme ink instead of white).
3. **Grain**. A tiled 128px noise texture at 3 to 5 percent opacity, so the glass does not look like flat transparency.
4. **Tint**. Theme surface colour at 55 to 80 percent opacity depending on elevation. Higher elevation means more opaque.
5. **Backdrop blur**. 16 to 32dp radius of whatever sits behind the pane.

Backdrop blur is implemented with Haze (`dev.chrisbanes.haze`), a hardware accelerated blur and tint library for Compose ([Haze docs](https://chrisbanes.github.io/haze/), [source](https://github.com/chrisbanes/haze)). Haze 2.x was still in alpha when this spec was written, so v1 pins the latest stable 1.x line and revisits 2.x once it is stable. Haze cannot blur content drawn in a `SurfaceView` (for example video), so glass over such content falls back to the tinted layer only.

### 3.3 Elevation levels

| Level | Use | Tint opacity | Blur |
| --- | --- | --- | --- |
| Glass 0 | Aurora backdrop | n/a | n/a |
| Glass 1 | Note cards, list items | 55 percent | 16dp |
| Glass 2 | Top bar, bottom bar, rail | 65 percent | 24dp |
| Glass 3 | Sheets, dialogs, menus, formatting toolbar | 80 percent | 32dp |

### 3.4 Readability rules (non negotiable)

* Text is never placed directly on blur alone. There is always a tint layer whose opacity guarantees the contrast targets in section 4.3 against the worst case backdrop.
* The editor and document viewer body area is **solid**, not glass. Glass is used for chrome (bars, cards, sheets, toolbars) only. Long form reading happens on an opaque "paper" surface.
* A "Reduce transparency" setting replaces all glass with solid tinted surfaces. It is turned on automatically when the system high contrast text setting is on, and when battery saver is active.
* A "Reduce motion" setting stops the aurora animation and shortens transitions. It follows the system animation scale by default.

### 3.5 Motion

Material 3 Expressive spring motion for containers. Glass panes fade their blur radius in over 150ms when they appear so the screen does not flash. The aurora drifts very slowly (one full cycle per 60 seconds) and pauses when the app is in the background.

### 3.6 Fallbacks by Android version

| Android version | Glass treatment |
| --- | --- |
| 12 and newer (API 31+) | Full five layer glass with real backdrop blur |
| 8 to 11 (API 26 to 30) | Tint, grain and edge glint, no live blur (or Haze's fallback path if it performs well in testing) |
| Any, with Reduce transparency on | Solid surfaces, edge glint kept as a subtle border |

### 3.7 Typography

* UI font: a variable sans with good small size legibility (proposal: Inter or Google Sans Flex if licensing allows, otherwise Roboto Flex). Bundled, not downloaded.
* Reading font options for notes and documents: Sans, Serif (proposal: Literata), Mono (proposal: JetBrains Mono), and "Hyperlegible" (Atkinson Hyperlegible) for readers who need it.
* Default body size 17sp in the editor and reflow viewer, line height 1.5, user adjustable from 14sp to 28sp. Respects system font scale up to 200 percent without clipping.
* Maximum line length in reflow mode about 70 characters on tablets. Extra width becomes margin, not longer lines.

---

## 4. Themes

### 4.1 Theme list

Each theme defines a seed colour, a backdrop (aurora palette), light and dark variants where it makes sense, and a reading surface colour.

| Theme | Mood | Variants | Notes |
| --- | --- | --- | --- |
| Dynamic | Follows the wallpaper via Material You dynamic colour | Light, Dark | Default on Android 12+ |
| Emerald Mist | Deep green glass, soft mint ink | Light, Dark | Brand theme and default below Android 12 |
| Paper | Warm off white, very low glare, minimal glass | Light | For long reading sessions |
| Midnight Ink | Navy and slate, low blue | Dark | Night writing |
| Sepia Study | Warm brown on cream | Light, Dark | Reading documents |
| Graphite | Neutral greys, no hue | Light, Dark | Distraction free |
| True Black | Pure black for OLED, glass reduced to outlines | Dark | Battery saving |
| High Contrast | Maximum contrast, glass off | Light, Dark | Accessibility |

### 4.2 Theme behaviour

* Theme mode can follow the system, or be forced light or dark.
* Optional scheduled dark mode (sunset to sunrise using local time only, no location permission).
* Per note accent colour that tints the note card glass and the widget, without changing text colours.
* Themes are defined as data (JSON in assets) so new themes can be added without code changes.

### 4.3 Contrast targets

Every theme must pass automated checks in unit tests.

| Element | Minimum contrast | Reference |
| --- | --- | --- |
| Body text and editor text | 4.5 to 1 (target 7 to 1 for Paper, Sepia, High Contrast) | WCAG 2.2 SC 1.4.3 and 1.4.6 ([W3C](https://www.w3.org/TR/WCAG22/)) |
| Large text (24sp+, or 19sp+ bold) | 3 to 1 | WCAG 2.2 SC 1.4.3 |
| Icons, focus rings, input borders | 3 to 1 | WCAG 2.2 SC 1.4.11 |

Glass contrast is tested against the worst case backdrop (the lightest and darkest aurora blob of the theme), not the average.

---

## 5. Features

### 5.1 Notes and organization

* Create, edit, duplicate, delete (with a 30 day recycle bin), pin, archive.
* Folders (nested two levels) and tags (unlimited, colour coded).
* Sort by edited, created, title, or manual drag order.
* Full text search across titles, bodies and imported documents, with highlighted results. Search runs locally using SQLite FTS.
* Checklists inside notes (Markdown task list syntax).
* Note templates (meeting, journal, to do, blank).
* Version history. The last 20 saves of each note are kept and can be restored.
* Autosave on every pause in typing and on app background. No save button needed for notes.

### 5.2 Editor

* Markdown first editor with live styling (headings, bold, italic, code, links, lists render in place while the raw syntax stays editable).
* Toggle between Edit, Split (on medium and expanded widths) and Preview.
* Formatting toolbar on a Glass 3 surface, docked above the keyboard on phones and in the bottom half in tabletop posture.
* Insert image from gallery (Android photo picker, no storage permission needed), camera, or clipboard. Images are copied into app storage and referenced with relative paths so the note stays portable.
* Insert table, link, code block, quote, horizontal rule, checklist.
* Undo and redo with deep history.
* Find and replace.
* Word, character and reading time counter.

### 5.3 Markdown support

| Feature | Supported |
| --- | --- |
| CommonMark core | Yes |
| GitHub Flavored Markdown (tables, task lists, strikethrough, autolinks) | Yes |
| Images, local relative paths, content URIs and https URLs | Yes |
| Footnotes | Yes |
| Heading anchors and table of contents | Yes |
| Syntax highlighted code blocks | Yes |
| YAML front matter | Parsed and shown as a metadata card, not as text |
| Math (LaTeX) | Phase 4 |
| Mermaid diagrams | Phase 4, shown as code until then |
| Raw HTML | Safe subset only (no scripts, no iframes) |

Rendering uses `multiplatform-markdown-renderer` for Compose with its Coil 3 image module ([source](https://github.com/mikepenz/multiplatform-markdown-renderer)). Remote images are loaded only after the user taps "Load remote images" for that document, so opening a file never contacts a server by surprise.

---

## 6. Opening documents

### 6.1 Entry points

* "Open" inside the app using the Storage Access Framework document picker.
* Android "Open with" and "Share to" from file managers, email, browsers and chat apps (intent filters for `ACTION_VIEW` and `ACTION_SEND`).
* Drag and drop onto the app on tablets.
* Recent files list with persisted URI permissions.

Opened files can be read in place, or imported as a note (a copy the user can edit).

### 6.2 Supported input formats

| Format | Extensions | MIME types handled |
| --- | --- | --- |
| Markdown | .md, .markdown, .mdown, .mkd | text/markdown, text/x-markdown, text/plain (by extension) |
| Plain text | .txt, .text, .log | text/plain |
| Rich Text | .rtf | application/rtf, text/rtf |
| OpenDocument Text | .odt, .fodt | application/vnd.oasis.opendocument.text |
| OpenDocument Spreadsheet (later) | .ods | application/vnd.oasis.opendocument.spreadsheet |
| OpenDocument Presentation (later) | .odp | application/vnd.oasis.opendocument.presentation |

### 6.3 Two view modes

Every document opens in a viewer with a two way switch at the top.

**Reading view (reflow).** Content is converted into the app's internal document model (section 6.5) and laid out to fit the screen. Fixed page widths, absolute positions and tiny fonts are dropped. Text size, theme and reading font follow the user's settings. This is the default on compact width.

**Original view (fidelity).** Content is laid out as the author designed it. Page size, margins, fonts (or closest available), colours, alignment, tables, columns, headers and footers are preserved. The page is shown on a paper surface with pinch to zoom, fit width and fit page. This is the default on expanded width.

The user's last choice is remembered per format.

### 6.4 Per format rules

**Markdown.** Reading and Original are the same render, Original simply uses the fonts and widths the author's front matter requests if any. Relative image paths resolve against the folder of the file when the app has access to it (using the document tree permission if the user grants it, otherwise a one time prompt "Allow access to this folder to show images").

**Plain text.**
* Encoding detection (BOM, then UTF 8 validation, then a fallback detector for legacy encodings such as Windows 1252 and Shift JIS), with a manual encoding picker.
* Line ending detection (LF, CRLF, CR) and preserve on save.
* Reading view wraps lines and uses the reading font. Original view uses a monospace font, no wrapping, horizontal scroll, so ASCII tables and art stay intact.
* Files over 5 MB open in a paged, read only viewer to stay responsive.

**RTF.** Android has no built in RTF parser, so VisaryNotes includes its own streaming RTF reader based on the Microsoft RTF 1.9.1 specification.
* Supported. Character formatting (font, size, bold, italic, underline, strike, colour, highlight, super and subscript), paragraph formatting (alignment, indents, spacing, tabs), lists, tables (`\trowd`, `\cell`), embedded PNG and JPEG pictures (`\pict` with `\pngblip` or `\jpegblip`), Unicode (`\uN`), code pages (`\ansicpg`), hyperlinks (`HYPERLINK` field), page size and margins, headers and footers.
* Converted with a warning. WMF and EMF pictures (rasterized if possible, otherwise shown as a placeholder).
* Ignored safely. OLE objects and unknown destinations (`\*` groups).

**ODT.** An ODT file is a ZIP package containing `content.xml`, `styles.xml`, `meta.xml` and a `Pictures/` folder, defined by the OASIS OpenDocument 1.3 standard ([OASIS ODF 1.3](https://docs.oasis-open.org/office/OpenDocument/v1.3/)). VisaryNotes parses it with Android's streaming `XmlPullParser` rather than the large ODF Toolkit, to keep the APK small.
* Supported. Paragraph and character styles with inheritance, headings, lists, tables (including merged cells), images, hyperlinks, footnotes, page layout (size, margins, orientation), headers and footers, sections with columns, document metadata.
* Fonts. If the requested font is not on the device, a metric compatible substitute is used (for example Liberation or Carlito style mapping), and Original view shows a small "Font substituted" notice.
* Flat ODT (.fodt) uses the same parser without the ZIP step.

### 6.5 Internal document model

All readers produce one common tree so every viewer and exporter works with every input.

```
Document
  metadata (title, author, created, modified, page setup)
  styles (named paragraph and character styles)
  blocks
    Heading, Paragraph (runs of styled text), ListBlock, Table,
    Image, CodeBlock, Quote, PageBreak, HorizontalRule, Footnote
```

This model is also what the note editor saves to when the user chooses a rich export format.

### 6.6 Safety and robustness

* All parsing runs off the main thread with size and time limits. A malformed file shows a friendly error and a "Show as plain text" fallback, never a crash.
* ZIP reading guards against zip bombs (maximum uncompressed size, maximum entry count) and path traversal in entry names.
* XML parsing has external entities and DTD loading disabled.
* No document content ever leaves the device unless the user shares or exports it.

---

## 7. Saving and exporting

### 7.1 Native note storage

Notes are stored in a Room (SQLite) database with an FTS index. Each note body is stored as Markdown. Images and attachments live in app private storage, one folder per note.

### 7.2 Export formats

| Format | Extension | How it is produced | Fidelity |
| --- | --- | --- | --- |
| Markdown | .md | Direct | Full |
| Markdown bundle | .zip | .md plus an `assets/` folder for images | Full, portable |
| Plain text | .txt | Markdown stripped to text | Text only |
| Rich Text | .rtf | Own RTF writer from the document model, images embedded as `\pngblip` | High |
| OpenDocument Text | .odt | Own ODT writer. ZIP with `mimetype` stored first and uncompressed, then manifest, content, styles, pictures | High |
| Word | .docx | Own minimal OOXML writer (document, styles, numbering, relationships, media) | High |
| HTML | .html | Single self contained file with inline CSS and base64 images | Full |
| PDF | .pdf | Android `PdfDocument` API, paged with the chosen theme or print friendly style | Full, fixed layout |

All writers are covered by round trip tests (write, read back with our own reader, compare the document model) and by golden files checked once by opening them in LibreOffice and Microsoft Word.

### 7.3 Save locations

* "Save as" opens the system file picker (`ACTION_CREATE_DOCUMENT`), so the user chooses any location including cloud drives that provide a document provider.
* "Share" sends the exported file through the Android share sheet.
* Optional "Auto export folder". The user picks a folder once and VisaryNotes keeps a Markdown copy of every note there, which also acts as a simple backup that any sync app can pick up.
* Opened RTF, ODT and TXT files can be saved back to their original location in their original format when the user edits an imported copy and chooses "Save back".

---

## 8. Home screen widgets and system integration

Widgets are built with Jetpack Glance. Glance offers action types such as `actionRunCallback` and `actionStartActivity`, and on Android 12+ interactive controls such as `CheckBox` ([Android Developers, Glance user interaction](https://developer.android.com/develop/ui/compose/glance/user-interaction)). Callbacks run in the background, so any action that opens the app must use `actionStartActivity` rather than starting an activity from a callback.

### 8.1 Widgets

| Widget | Sizes | Interaction |
| --- | --- | --- |
| Quick Capture | 2x1, 4x1 | Buttons for new text note, new checklist, new photo note, voice to text note. Opens straight into the editor |
| Pinned Note | 2x2 up to 4x4, resizable | Shows a chosen note with Markdown styling. Tap to open. Scrollable list content |
| Checklist | 2x2 up to 4x5, resizable | Tick and untick items directly on the home screen without opening the app. Add item button |
| Recent Notes | 4x2 up to 4x4 | List of latest notes, tap to open, plus a new note button |

### 8.2 Widget styling

Home screen widgets cannot blur the wallpaper behind them, so they use the "glass without blur" treatment. Theme tint at 70 to 85 percent opacity, grain, edge glint and Material 3 widget corner radius. Widgets follow the app theme or the system dynamic colours, chosen in the widget configuration screen. Generated previews are provided for the widget picker.

### 8.3 Other system integration

* Quick Settings tile for "New note".
* App shortcuts (long press icon) for New note, New checklist, Search, Last opened note.
* Share target so text, links and images shared from other apps become new notes.
* Lock screen and notification quick capture via a persistent notification (optional, off by default).
* Predictive back on all screens.
* Per app language support.

### 8.4 Keyboard shortcuts (tablets and Chromebooks)

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

## 9. Accessibility

* Full TalkBack support with meaningful content descriptions, headings exposed as headings in the viewer, and tables announced with row and column positions.
* All touch targets at least 48dp.
* Font scale up to 200 percent without clipping or overlapping.
* Reduce transparency and Reduce motion settings (section 3.4).
* Never use colour alone to show state (tags also show a name, checked items are also struck through).
* Switch Access and keyboard navigation with visible focus rings on glass.

---

## 10. Architecture

### 10.1 Stack

| Area | Choice |
| --- | --- |
| Language | Kotlin |
| UI | Jetpack Compose, Material 3 (Expressive components where stable), material3-adaptive |
| Glass | Haze |
| Navigation | Navigation Compose with type safe routes, adaptive scaffold for panes |
| DI | Hilt |
| Persistence | Room with FTS, DataStore for settings |
| Images | Coil 3 |
| Markdown | commonmark parsing plus multiplatform-markdown-renderer for display |
| Widgets | Jetpack Glance |
| Background work | WorkManager (auto export, trash cleanup) |
| Build | Gradle Kotlin DSL with a version catalog |

Library versions are pinned in `gradle/libs.versions.toml` to the latest stable releases on the day the project is scaffolded, and updated deliberately, not automatically.

### 10.2 Module layout

```
app                      Application, navigation, adaptive shell
core/designsystem        Visary Glass, themes, typography, shapes
core/model               Note and Document model
core/data                Room, repositories, DataStore
core/formats             Readers and writers (md, txt, rtf, odt, docx, html, pdf)
feature/notes            Note list, folders, tags, search
feature/editor           Markdown editor
feature/viewer           Reading view and Original view
feature/settings         Settings and theme picker
widget                   Glance widgets, Quick Settings tile
```

`core/formats` is plain Kotlin with no Android UI dependencies where possible, so its tests run fast on the JVM without an emulator.

### 10.3 Data model (v1)

```
Note        id, title, body (markdown), folderId, color, pinned, archived,
            createdAt, updatedAt, deletedAt
Folder      id, name, parentId, sortOrder
Tag         id, name, color
NoteTag     noteId, tagId
Attachment  id, noteId, fileName, mimeType, size
NoteVersion id, noteId, body, savedAt
RecentFile  uri, displayName, mimeType, lastOpenedAt, viewMode
```

### 10.4 Permissions

Only what is strictly needed. No internet permission is required for the core app. If remote Markdown images are kept as a feature, `INTERNET` is added and remote loading stays opt in per document. No storage permissions (Storage Access Framework and the photo picker are used instead). `POST_NOTIFICATIONS` only if the user enables the quick capture notification. Camera access goes through the system camera intent, so no camera permission either.

---

## 11. Performance targets

| Metric | Target on a mid range phone |
| --- | --- |
| Cold start to note list | under 800ms |
| Open a 1 MB Markdown file | first screen under 500ms |
| Open a 5 MB ODT with images | first page under 1.5s, rest streamed |
| Typing latency in editor | no dropped frames at 60Hz on 10,000 word notes |
| Search across 5,000 notes | under 150ms |
| APK size (release, minified) | under 15 MB |

Glass blur is the main rendering cost. Blur is limited to chrome surfaces, never applied to scrolling list items on Android 11 and below, and automatically reduced when the device reports thermal throttling.

---

## 12. Development workflow with minimal GitHub Actions usage

### 12.1 Key fact

The repository `EmeraldWall/VisaryNotes` is currently **public**. GitHub does not charge Actions minutes for standard GitHub hosted runners on public repositories ([GitHub Docs, GitHub Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions)). As long as the repo stays public and workflows use standard runners, builds should not consume the account's limited minutes. If the repo is made private later, Linux runners count at 1x while Windows counts at 2x and macOS at 10x (same source), so only Linux runners are used.

### 12.2 Rules, so builds stay cheap either way

1. **Build locally first.** All tests in `core/formats` are plain JVM tests and run in seconds with `./gradlew test`. Developers (and Claude Code sessions) run lint and tests locally before pushing.
2. **One workflow, one Linux job.** `ubuntu-latest` only. No matrix builds, no emulator tests in CI.
3. **Manual or tag triggered only.** The workflow runs on `workflow_dispatch` and on version tags (`v*`), not on every push. Optionally on pull requests to `main` with path filters so documentation changes never trigger a build.
4. **Cancel superseded runs** with a `concurrency` group and `cancel-in-progress: true`.
5. **Gradle caching** via the official Gradle setup action, plus configuration cache, so repeat builds take a few minutes.
6. **Short timeout** (`timeout-minutes: 20`) so a hung build cannot burn minutes.
7. **Artifacts.** Only the debug or release APK is uploaded, with a short retention period.

### 12.3 Testing strategy

| Layer | Where it runs |
| --- | --- |
| Format readers and writers (round trip and golden files) | JVM unit tests, local and CI |
| Theme contrast checks | JVM unit tests, local and CI |
| Repositories and Room | Robolectric tests, local and CI |
| UI screenshots for phone, foldable and tablet sizes | Compose screenshot tests, local only |
| Real device checks (fold, widgets, TalkBack) | Manual checklist on a phone, a foldable or emulator, and a tablet before each release |

---

## 13. Delivery phases

| Phase | Scope | Exit criteria |
| --- | --- | --- |
| 0. Foundation | Project scaffold, modules, design system, Visary Glass, 3 themes (Dynamic, Emerald Mist, Paper), adaptive shell, CI workflow | App runs on phone, foldable and tablet emulators with glass chrome and correct pane layouts |
| 1. Notes core | Room storage, note list, folders, tags, Markdown editor with images, search, trash, autosave | Daily usable as a notes app |
| 2. Open documents | TXT, MD, RTF, ODT readers, Reading and Original views, open with and share intents, recent files | Sample corpus of 50 real world files opens without crash and passes visual review |
| 3. Export and widgets | All export formats, Glance widgets, Quick Settings tile, shortcuts, remaining themes | Round trip tests green, widgets interactive on Android 12+ |
| 4. Polish and stretch | Accessibility audit, performance pass, version history UI, .ods and .odp viewing, .docx viewing, math and Mermaid | Release candidate |

---

## 14. Future work (not in v1)

Optional sync through user chosen folders or WebDAV, end to end encrypted notes, handwriting and sketch layer, Wear OS quick capture, desktop companion, collaborative editing, on device AI summaries.

---

## 15. Open questions

1. Is the package name `com.visary.notes` acceptable, and is there a logo or brand colour beyond the emerald theme suggested by the organisation name?
2. Should remote images in Markdown be supported at all (requires the internet permission), or should the app be fully offline by design?
3. Is .docx viewing needed earlier than phase 4?
4. Which fonts are you happy to bundle (licensing and APK size)?
5. Will the repository stay public? This decides whether CI cost is a concern at all.

---

## Appendix A. References

* Android Developers, Build a list detail layout. https://developer.android.com/develop/adaptive-apps/guides/list-detail
* Android Developers, Glance user interaction. https://developer.android.com/develop/ui/compose/glance/user-interaction
* Android Developers, API levels. https://developer.android.com/about/versions/api-levels
* Haze (Compose backdrop blur). https://chrisbanes.github.io/haze/ and https://github.com/chrisbanes/haze
* multiplatform-markdown-renderer. https://github.com/mikepenz/multiplatform-markdown-renderer
* OASIS OpenDocument 1.3. https://docs.oasis-open.org/office/OpenDocument/v1.3/
* W3C WCAG 2.2. https://www.w3.org/TR/WCAG22/
* GitHub Docs, GitHub Actions billing. https://docs.github.com/en/billing/concepts/product-billing/github-actions
