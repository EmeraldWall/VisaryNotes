# VisaryNotes Specification

Version 1.0 | Status: for review | Date: 2026-10-22 | Previous: [archive/SPEC-0.7.md](archive/SPEC-0.7.md)

This is the single source of truth for what VisaryNotes is, why it is shaped this way, what is built, and what comes next. Anything not written here is out of scope until it is added here first. Section numbers 4 to 14 keep the meaning they had in version 0.7, because code comments cite them.

**What is new in 1.0**

* A positioning built on points of parity, points of difference and a partial blue ocean strategy (section 2).
* VisaryNotes follows open principles (no telemetry, no account, no ads, plain files, code in public view) without an open source licence for now. Section 14 sets out what that allows and what it does not.
* It ships on Google Play and as a GitHub release, with the Play requirements, signing model and listing plan in section 14.7.
* Every requirement has an ID, a status against build 0.2.6 and an acceptance check.
* Known defects found on the owner's phone are listed as release blockers (section 15.1), because they break points of parity.

**Status labels used throughout**

| Label | Meaning |
| --- | --- |
| Built | In build 0.2.6 and checked by tests or by the owner on a phone |
| Built, unverified | In build 0.2.6, not yet confirmed on a phone |
| Partial | Some of the requirement is built |
| Planned 0.x | Scheduled for that release (section 15) |
| Later | After 1.0 |
| Blocker | A defect that must be fixed before the next release |

---

## 1. Product

### 1.1 One line

**VisaryNotes is a private, offline notebook for reading and writing on Android: it opens the documents people send you as well as a dedicated reader does, lets you write as well as a Markdown note app does, and joins the two with quotes that remember where they came from.**

### 1.2 The four jobs

1. **Read.** Open what people send (Markdown, text, RTF, ODT, DOCX, DOC, PDF) in a Reading view that reflows for a phone, and an Original view that keeps the author's layout.
2. **Extract.** Highlight passages and quote them into notes. Every quote keeps a link back to its place.
3. **Write.** Capture and shape notes in Markdown with a fast, comfortable editor.
4. **Reply.** Send work back in the format others use: Word, ODT, RTF, PDF, HTML, Markdown, text.

Notes and documents live together in one **Library**.

### 1.3 Who it is for

Hypotheses from [STRATEGY.md](STRATEGY.md) section 4.2, to be tested in the beta (section 16).

| Segment | Their situation today |
| --- | --- |
| People who receive documents on their phone (minutes, circulars, receipts, briefs) | Read them in a chat app's preview, screenshot the important line, retype it somewhere |
| Students and researchers | Read assigned PDFs and Word files, copy quotes into a separate note app by hand, lose the source |
| Markdown note takers (Markor, Obsidian users) | Good writing tools, poor handling of office files |
| Privacy minded users | Avoid apps that need an account or send data away |
| Owners of foldables and tablets | Want the document and their notes side by side |

### 1.4 Goals

| ID | Goal |
| --- | --- |
| G1 | Reading received documents on a phone is as good as in a dedicated reader (section 7) |
| G2 | Writing Markdown notes is as good as in a dedicated Markdown app (section 6) |
| G3 | The read, extract, write, reply loop works end to end in a few taps (section 7.8) |
| G4 | Nothing the user makes is lost: undo, trash, backup, plain files, no lock in (sections 6.1 and 8) |
| G5 | Usable with one hand, either hand (section 4.4) |
| G6 | Legible in every theme: body text 7 to 1 contrast or better (section 5) |
| G7 | Free, offline, no account, no ads, no tracking, plain files, code in public view (section 14) |
| G8 | Phones, foldables and tablets from one codebase (section 3) |
| G9 | Built and released by one developer from Visual Studio, with near zero CI cost (section 14) |

### 1.5 Non goals for 1.0

Accounts and cloud sync run by VisaryNotes, collaboration, editing office files in place (round trip editing of DOCX or ODT), spreadsheets and presentations, handwriting, AI features, iOS and Windows builds. Section 17 lists what may come later.

### 1.6 Assumptions

| ID | Assumption | Status |
| --- | --- | --- |
| A1 | Visual Studio on Windows, .NET 10, .NET MAUI, C#, Android only (`net10.0-android`) | Confirmed by the owner |
| A2 | Package name `com.vishvplus.visarynotes`, app name VisaryNotes. The package name can never change once the app is on Google Play | Package confirmed by the owner (from build 0.2.7); app name in use |
| A3 | Minimum Android 8.0 (API 26), target API 36 | In use |
| A4 | No open source licence for now: the code is public but all rights are reserved. The open principles in section 14 apply in full. A licence may be chosen later (section 14.3) | Confirmed by the owner |
| A5 | No telemetry of any kind; feedback comes from opt in channels (section 14.5) | Proposed |
| A6 | Published on Google Play and on GitHub Releases at the same time, from the same public source code (section 14.7) | Confirmed by the owner |

---

## 2. Strategy and positioning

The full analysis is in [STRATEGY.md](STRATEGY.md) (VRIO, Theory of Planned Behaviour, Innovation Diffusion Theory) and [UX-REVIEW.md](UX-REVIEW.md). This section states the decisions the build must follow.

### 2.1 Frame of reference

Keller, Sternthal and Tybout argue that a brand first needs a frame of reference (the category people judge it in), then must meet that category's points of parity before its points of difference count (Harvard Business Review, September 2002).

VisaryNotes deliberately **straddles two frames**: note apps (Google Keep, Samsung Notes, Obsidian, Markor) and document readers (Xodo, WPS Office, Moon+ Reader). The owner's decision is to be good at both rather than strong at one. That means it must meet the points of parity of **both** frames, which is a heavier load than either alone and is the main delivery risk.

The frame stated to users (store listing, first screen) is: **"Read what you're sent. Write what you think."**

### 2.2 Points of parity

Must match, or the app is not taken seriously in that frame. Status against build 0.2.6.

| ID | Parity point | Frame | Status |
| --- | --- | --- | --- |
| P1 | Fast capture, search, pin, trash with restore, undo | Notes | Built |
| P2 | Polished, bug free interface | Both | **Blocker** (section 15.1) |
| P3 | Markdown editing basics: live styling, toolbar, lists, checklists, links | Notes | Partial, styling regressed (B3) |
| P4 | Undo and redo, find and replace in a note | Notes | Planned 0.4 |
| P5 | Organisation: tags, and folders or notebooks | Notes | Planned 0.4 |
| P6 | Backup and moving to a new phone without losing anything | Notes | Planned 0.3 |
| P7 | Opens PDF and Word files | Readers | Built |
| P8 | Find in document, contents, text size, resume position | Readers | Built, unverified |
| P9 | Highlights | Readers | Built |
| P10 | Home screen widgets, dark themes, export | Both | Built |

### 2.3 Points of difference

Must be relevant (people care), distinctive (others do not offer it) and believable (the app visibly delivers it).

| ID | Point of difference | Against | Strength |
| --- | --- | --- | --- |
| D1 | **Quote to note with a link back to the source**, and a per document digest of highlights and quotes | All | Main difference. The loop as a whole is hard to copy (VRIO R1) |
| D2 | **Readable Reading view of RTF, ODT, DOC, DOCX and PDF on a phone**, beside an Original view | Note apps, most readers | Main difference. The parsers and a growing test corpus take years to match (VRIO R2) |
| D3 | **Reader and writer equally strong in one app** | Single job apps | The combination is the new space (section 2.4) |
| D4 | **No account, no tracking, plain Markdown files, code in public view** | Keep, Samsung Notes, Obsidian | Parity against Markor on files and privacy; Markor is also open source, which VisaryNotes is not yet |
| D5 | **Reply in the sender's format**, including a Word file of a document's quotes | Note apps | Supporting difference |
| D6 | One handed pill that follows the chosen hand | All | Supporting, easy to copy |
| D7 | Visary Glass look | All | Image only, never at the cost of legibility (VRIO R4) |

### 2.4 Partial blue ocean

Kim and Mauborgne's blue ocean method redesigns an offer with four actions: eliminate, reduce, raise and create ([Blue Ocean Strategy, ERRC grid](https://www.blueoceanstrategy.com/wp-content/uploads/2022/12/Creating-your-Eliminate-Reduce-Raise-Create-Grid.pdf)). A pure blue ocean move would drop editing power to focus on reading. VisaryNotes applies it **partially**: it keeps competing on editing quality (a red ocean factor), and creates new value in the space between reading and writing, which neither category serves.

| Eliminate | Reduce |
| --- | --- |
| Accounts and sign in | In place editing of office formats (view and reply instead) |
| Ads, tracking, telemetry | Organisation complexity: tags and simple notebooks, no databases or plugin systems |
| Proprietary storage and lock in | Settings count and visual decoration |
| A separate Files area | Setup before first use (none) |

| Raise | Create |
| --- | --- |
| Reading quality of received files on a phone | Quote to note with a source link that reopens the passage |
| Markdown editing quality, to the level of dedicated Markdown apps | Highlights digest per document |
| Safety: undo, trash, backup | Reply in the sender's format, including quotes as a Word file |
| One handed reach | Document and note side by side on foldables and tablets |
| Openness: visible code, plain files, open formats, no tracking | A community grown corpus of real documents that makes the readers better (section 14.4) |

### 2.5 Strategy canvas

Offering level per factor, 1 (low) to 5 (high). Competitor columns are the author's assessment from public product information, **not measured**, and are to be checked with beta users.

| Factor | Google Keep | Obsidian | Markor | Office and PDF readers | VisaryNotes 1.0 target |
| --- | --- | --- | --- | --- | --- |
| Capture speed | 5 | 3 | 4 | 1 | 4 |
| Markdown editing quality | 1 | 5 | 4 | 1 | 4 |
| Organisation and linking | 3 | 5 | 2 | 1 | 3 |
| Reading quality of received office files on a phone | 1 | 2 | 1 | 4 | 5 |
| Editing office files in place | 1 | 1 | 1 | 5 | 1 |
| Read to note loop with source links | 1 | 2 | 1 | 2 | 5 |
| No account, offline, plain files | 1 | 4 | 5 | 2 | 5 |
| Openness (visible code, no tracking) | 1 | 2 | 5 | 1 | 4 |
| Cloud sync by the vendor | 5 | 4 | 1 | 4 | 1 |
| One handed design | 2 | 2 | 2 | 2 | 5 |

The VisaryNotes curve is low where competitors spend most (vendor sync, office editing) and high where no one is strong (reading received files, the loop, openness, one handed use).

### 2.6 How the theories shape the build

| Theory | Rule for the build |
| --- | --- |
| VRIO | Invest first in D1 and D2, the only items that are valuable, rare and hard to copy. Organise the project to grow the document corpus (section 14.4) |
| Theory of Planned Behaviour | Perceived control is the weakest link: every destructive action has undo or asks first, data is portable, nothing is hidden behind gestures |
| Innovation Diffusion Theory | Zero setup trial through "Open with"; compatibility with Markdown folders and Word; observable results through exported files that look native |
| Blue ocean | Every new feature must either close a parity gap (2.2) or build a create item (2.4). Anything else waits |
| Open principles | Visible code and no tracking raise trust (TPB attitude) and observability (IDT); document donations let the community grow the hard to copy asset (VRIO) |

---

## 3. Target devices and adaptive layout

| ID | Requirement | Status |
| --- | --- | --- |
| L1 | Layout chosen by window width, not device type: Compact under 600dp (one pane), Medium 600 to 839dp (two panes), Expanded 840dp and up (three panes) | Compact built; Medium and Expanded Planned 0.5 |
| L2 | Foldable book posture: document and linked note on either side of the hinge; tabletop posture: content top, tools bottom | Planned 0.5 |
| L3 | No lost text, cursor or scroll position across fold, unfold, rotation and resize | Built for rotation (config changes declared); folding Planned 0.5 |
| L4 | Keyboard and mouse support on tablets and Chromebooks (section 9.5) | Planned 0.5 |

Implementation notes for L1 and L2 are unchanged from [archive/SPEC-0.7.md](archive/SPEC-0.7.md) section 3.

---

## 4. Visual design and interaction

Rationale and sources are in [DESIGN.md](DESIGN.md). This section lists what the build must implement.

### 4.1 Materials

Paper (opaque content surfaces), glass (tools only, never long text), ink (text, never translucent), one accent for the primary action. No texture anywhere.

### 4.2 Tokens

Every spacing, size, radius, inset and font size comes from `tools/design/tokens.json`, generated into the app by `tools/design/generate.py`. `tools/design/lint_layout.py` rejects literal values in XAML and duplicate implicit styles. Status: Built.

### 4.3 Glass

| ID | Requirement | Status |
| --- | --- | --- |
| V1 | Bars and the pill show a visible tint so labels never sit on bare content | **Blocker** B1 and B2 |
| V2 | Tint at the adaptive maximum (80 percent light, 85 percent dark) until real blur exists | Built, unverified |
| V3 | Backdrop blur on Android 12 and later through a native handler (RenderEffect) | Planned 0.5 |
| V4 | Reduce transparency and High Contrast themes make bars solid paper | Built |

### 4.4 Screen zones and one handed use

* Glance zone (top): passive information only. Read zone (middle): content. Act zone (bottom): every frequent action.
* Handedness: Centre, Left, Right. The action pill moves to that side and can be swiped between positions. Status: Built.
* No app gestures from the side edges. Status: Built.
* Every popup is a bottom sheet (`BottomSheet`), every confirmation offers undo or asks first. Status: Built.
* Touch targets at least 48dp, 56dp for primary actions. Status: Built.

### 4.5 Space budget

| Element | Rule | Status |
| --- | --- | --- |
| Top strip | 48dp, title and one fact (progress, save state, counts) | Built |
| Bottom | Pill row plus at most one bar (filters, view switch, toolbar, find, selection) | Built |
| Content at rest | Never under a bar; content may scroll under bars | **Blocker** B4 at the bottom edge |
| Immersive reading | A tap on the page hides all bars; a second tap shows them | Built |
| List density | Comfortable 64dp, Compact 56dp (default), Dense 48dp | Built |

### 4.6 Readability

Body text 7 to 1, secondary 4.5 to 1, controls 3 to 1, in every theme, checked by `tools/design/contrast.py`. No UI text under 13sp. Status: Built.

### 4.7 Motion

Sheets 200ms, notices and bars 140 to 160ms, nothing ambient. Status: Built.

### 4.8 Typography

Literata for reading, Figtree for interface, JetBrains Mono for code, Tinos for documents that ask for Times. All under open font licences, bundled. Reading size 14 to 28sp, multiplied by the system font scale. Status: Built.

---

## 5. Themes

### 5.1 Themes

Daylight (default), Sepia, Dusk, Night, OLED, High Contrast Light, High Contrast Dark, defined in `tools/design/themes.json`. Status: Built.

### 5.2 Theme behaviour

Themes switch at runtime through dynamic resources; the reader, widgets and status bar follow. Following the system light or dark setting is Planned 0.4.

### 5.3 Contrast checks

`tools/design/contrast.py` runs locally and in CI. Status: Built.

---

## 6. Writing

### 6.1 Library and organisation

| ID | Requirement | Status |
| --- | --- | --- |
| N1 | One Library for notes and documents, filters All, Notes, Docs, Linked, Trash | Built |
| N2 | Continue reading row at the top | Built |
| N3 | Full text search over notes (SQLite FTS5) | Built |
| N4 | Pin, delete to Trash with undo, restore, delete forever, empty Trash (asks first), 30 days retention | Built |
| N5 | Multi select with Select all, Pin, Delete, Restore, Delete forever | Built |
| N6 | Clean up Library: remove documents not opened in 30 days, highlights kept, undo | Built |
| N7 | First run empty state with three first steps and the privacy promise | Built |
| N8 | Markdown import from other apps | Built |
| N9 | Tags (`#tag` in the note and a tag filter) | Planned 0.4 |
| N10 | Notebooks: one level of grouping | Planned 0.4 |
| N11 | Sort by edited, created, title | Planned 0.4 |
| N12 | Search inside documents' text | Planned 0.5 |
| N13 | Templates: meeting, journal, checklist, blank | Planned 0.4 |
| N14 | Version history: the last 20 saves of a note | Later |

### 6.2 Editor

The editor must be as good as dedicated Markdown apps (point of parity P3, P4), because the owner's positioning rejects trading editing away.

| ID | Requirement | Status |
| --- | --- | --- |
| E1 | Live Markdown styling in place: headings larger and bold, bold, italic, code, quotes, checklists, links; syntax marks dimmed but editable | **Blocker** B3 (headings and quotes stopped styling) |
| E2 | Format toolbar above the keyboard, always visible while typing | **Blocker** B5 |
| E3 | Autosave on pause and on leaving, save state and word count in the strip | Built |
| E4 | Preview | Built |
| E5 | Undo and redo | Planned 0.4 |
| E6 | Find and replace in a note | Planned 0.4 |
| E7 | Lists continue on Enter (bullets, numbers, checklists); Enter on an empty item ends the list | Planned 0.4 |
| E8 | Tap a checklist box in preview to tick it | Planned 0.4 |
| E9 | Insert image from the photo picker or camera, stored beside the note | Planned 0.4 |
| E10 | Insert table | Planned 0.4 |
| E11 | Links between notes (`[[Note title]]`) and a backlinks list | Planned 0.4 |
| E12 | Quote source links shown as a readable chip ("OFL.txt"), not raw link code | Planned 0.3 (B8) |
| E13 | The strip shows the save state; it does not repeat the note's first heading | Planned 0.3 (B9) |
| E14 | The pill's primary action is the most frequent one (Preview or Done), with Share secondary | Planned 0.3 (B10) |
| E15 | Toolbar buttons have clear icons and long press labels | Planned 0.4 |
| E16 | Outline of a note's headings | Planned 0.4 |

**Implementation.** A MAUI `Editor` with an Android handler that applies spans to edited paragraphs after a 150ms pause (`Platforms/Android/Handlers/MarkdownSpans.cs`). Undo and redo are an edit history kept by the same handler.

### 6.3 Markdown support

Markdig with advanced extensions: CommonMark, tables, task lists, strikethrough, autolinks, footnotes, YAML front matter. Raw HTML disabled. Remote images only when the user turns them on. Status: Built. Math and Mermaid: Later.

---

## 7. Reading

### 7.1 Entry points

| ID | Requirement | Status |
| --- | --- | --- |
| R1 | Open from inside the app (file picker) | Built |
| R2 | "Open with" and "Share to" from any app, for every supported type | Built |
| R3 | Shared plain text becomes a new note | Built |
| R4 | Files are copied into app storage so they stay readable; reopening the same file finds its highlights | Built |

### 7.2 Formats

Markdown, plain text, RTF, ODT and flat ODT, DOCX, DOC, PDF. Files with the wrong extension are detected from their first bytes. Status: Built. ODS and ODP viewing: Later.

### 7.3 The viewer

One viewer for every format: each reader produces the shared document model (7.6), rendered to HTML in a WebView. Offline, scripts limited to the app's own, file access limited to images the app saved. Status: Built.

### 7.4 Views and reading tools

| ID | Requirement | Status |
| --- | --- | --- |
| R5 | Reading view reflows for the screen; Original view keeps page size, margins, fonts and layout | Built |
| R6 | Plain text in Reading view joins hard wrapped lines into paragraphs and keeps blank line paragraph breaks | **Blocker** B6 |
| R7 | Separator lines (rows of dashes or equals signs) render as a rule, never wrap | **Blocker** B7 |
| R8 | Find in document with match count, previous, next | Built, unverified |
| R9 | Contents: jump to any heading | Built, unverified |
| R10 | Text size control; system font size respected | Built, unverified |
| R11 | Resume at the last position | Built, unverified |
| R12 | PDF Original view draws real pages, first pages first | Built, unverified |
| R13 | Reader themes follow the app theme | Built |

### 7.5 Per format rules

Unchanged from [archive/SPEC-0.7.md](archive/SPEC-0.7.md) section 7.5 (encoding detection for text, the RTF, ODT, DOCX and DOC readers, and the PDF reflow method), plus R6 and R7 above.

### 7.6 Document model

All readers produce one tree in `VisaryNotes.Core` (document, page setup, fixed pages, blocks: heading, paragraph, list, table, image, preformatted, quote, rule, page break). Every viewer and exporter works with every input. Status: Built.

### 7.7 Zero setup open

A file from another app opens straight in the reader. Nothing is added to the Library unless the user chooses Save to Library (in the reader's More sheet). The first document shows a one time tip on selecting and quoting. Status: Built.

### 7.8 The loop: read, extract, write, reply

The main point of difference (D1). Everything here is in the "create" column of the blue ocean grid.

| ID | Requirement | Status |
| --- | --- | --- |
| X1 | Select text in either view; menu offers Highlight, Quote to note, Copy | Built |
| X2 | Quote to note adds a blockquote and a source link to the document's linked note, confirmed with a notice that offers View | Built |
| X3 | Linked note sheet over the reader, with a quick "Add a thought" field | Built |
| X4 | Tapping a source link opens the document **at the quoted passage** | Partial (opens the document, not the passage), Planned 0.5 |
| X5 | Quote anchors: file hash, position in the model, offsets and text, re matched if the file changes | Planned 0.5 |
| X6 | Highlights digest: every highlight and quote of a document in one view, each tappable | Planned 0.5 |
| X7 | Highlight colours (at least three) | Planned 0.5 |
| X8 | Reply: export a document's quotes and notes as a Word file in one step | Planned 0.5 |
| X9 | Document and note side by side on foldables and tablets (L1, L2) | Planned 0.5 |

### 7.9 Safety and robustness

Parsing off the UI thread with size limits; zip bomb and path traversal guards; no DTDs or external entities; HTML built from the model, never passed through; a broken binary file shows a message, never garbage text. Status: Built.

---

## 8. Storage, export and backup

### 8.1 Storage

SQLite (sqlite-net, FTS5) in app storage; note bodies as Markdown; opened documents copied into app storage. Status: Built.

### 8.2 Export

| Format | Status |
| --- | --- |
| Markdown, plain text, HTML | Built |
| Word (.docx), checked in LibreOffice and by round trip tests | Built |
| ODT, RTF | Planned 0.5 |
| PDF and print (WebView print adapter) | Planned 0.5 |
| Markdown bundle with images (.zip) | Planned 0.4 |

### 8.3 Backup and portability

Point of parity P6 and the "no lock in" promise (G4, D4).

| ID | Requirement | Status |
| --- | --- | --- |
| S1 | Back up everything (notes as .md, documents, highlights and links as JSON) to a folder the user picks | Planned 0.3 |
| S2 | Restore from that folder on a new phone | Planned 0.3 |
| S3 | Optional auto backup folder that any sync app (Syncthing, a cloud drive app) can pick up | Planned 0.4 |
| S4 | Work in folder mode: notes live as .md files in a user chosen folder | Later |

---

## 9. Widgets and system integration

### 9.1 How widgets are built

Native `AppWidgetProvider` classes and `RemoteViews` layouts in `Platforms/Android`, frosted with `layer-list` drawables. Status: Built.

### 9.2 Widgets

| Widget | Status |
| --- | --- |
| Quick Capture: New note, Checklist, Open file | Built |
| Continue Reading: title and progress, tap to resume | Built |
| Checklist (tick items on the home screen) | Planned 0.5 |
| Pinned note | Later |

### 9.3 Widget styling

Frost at 80 percent (light) or 85 percent (dark); launcher controls the outer corner radius. Status: Built.

### 9.4 Other integration

| Feature | Status |
| --- | --- |
| Adaptive icon with a themed (monochrome) layer | Built |
| Status and navigation bar colours follow the theme | Built |
| Quick Settings tile "New note" | Planned 0.5 |
| App shortcuts (long press the icon) | Planned 0.5 |
| Predictive back | Planned 0.5 |
| Per app language | Planned 0.6 |

### 9.5 Keyboard shortcuts

Ctrl N, O, F, Shift F, B, I, K, Z, Y, Slash on tablets and Chromebooks. Status: Planned 0.5.

---

## 10. Accessibility

| ID | Requirement | Status |
| --- | --- | --- |
| A11-1 | TalkBack labels on every control; headings marked | Built |
| A11-2 | 48dp targets | Built |
| A11-3 | Font scale up to 200 percent without clipping | Partial (reader follows it; layout check on a device Planned 0.4) |
| A11-4 | Never colour alone for state | Built |
| A11-5 | Reduce transparency | Built |
| A11-6 | Keyboard and switch access with visible focus | Planned 0.5 |

---

## 11. Architecture

### 11.1 Solution

```
VisaryNotes.sln
  src/VisaryNotes.Core/          net10.0 library: documents, rendering, export, data, theming. No UI
  src/VisaryNotes.App/           .NET MAUI app, net10.0-android
    Pages/  Controls/  Services/  Resources/
    Platforms/Android/           MainActivity, handlers, widgets, PDF pages, find
  tests/VisaryNotes.Core.Tests/  xUnit, with Fixtures (sample docx, doc, pdf)
  tools/design/                  tokens, themes, generator, contrast and layout checks
  design/                        prototype and icon sources
  docs/                          SPEC, DESIGN, STRATEGY, UX-REVIEW
```

### 11.2 Dependencies

| Area | Package | Licence |
| --- | --- | --- |
| UI | Microsoft.Maui.Controls | MIT |
| MVVM helpers | CommunityToolkit.Mvvm | MIT |
| Database | sqlite-net-pcl with SQLitePCLRaw | MIT, Apache 2.0 |
| Markdown | Markdig | BSD 2 clause |
| PDF text | PdfPig | Apache 2.0 |
| Fonts | Figtree, Literata, JetBrains Mono (OFL 1.1), Tinos (Apache 2.0) | As listed in `licenses/` |

All are permissive licences, so they allow any future licence choice, open or closed (section 14.3). Their notices must ship with the app. New dependencies must use a permissive licence (MIT, BSD, Apache 2.0, OFL for fonts), not a copyleft one such as GPL, so the licence decision stays open, and must not need Google Play Services.

### 11.3 Data model

```
Note        Id, Title, Body, Pinned, Archived, CreatedAt, UpdatedAt, DeletedAt
Document    Id, Uri, DisplayName, Kind, LastOpenedAt, Progress, ViewMode, InLibrary
Highlight   Id, DocumentId, Text, CreatedAt
NoteLink    Id, NoteId, DocumentId, IsPrimary
note_search FTS5 over title and body
Planned 0.4: Tag, NoteTag, Notebook, Attachment.  Planned 0.5: anchor fields on Highlight, colour
```

### 11.4 Permissions

`INTERNET` only for opt in remote images. No storage permissions (Storage Access Framework and the photo picker instead). No analytics, crash reporting or ad SDKs, and no Google Play Services libraries, so the Play build and the GitHub build are the same code. Every permission added later must be justified here and in the Play data safety form (section 14.7).

### 11.5 Development environment

Visual Studio on Windows with the .NET MAUI workload; Android SDK through Visual Studio; tests in Test Explorer. The app also builds on Linux with the `maui-android` workload, which is what CI uses.

---

## 12. Quality

| Layer | How | Status |
| --- | --- | --- |
| Readers, renderer, exporter, repository | xUnit, 94 tests | Built |
| Real document corpus | `tests/.../Fixtures`, grown from user reported files (section 14.4) | Partial (3 samples) |
| Contrast and layout rules | `contrast.py`, `lint_layout.py` | Built |
| Heuristic review | [UX-REVIEW.md](UX-REVIEW.md) | Built |
| Device check before every release | Real phone: reader bars, editor with keyboard, PDF pages, widgets, TalkBack | Planned 0.3 (a written checklist in `docs/RELEASE-CHECKLIST.md`) |
| Usability test | 5 people per round, tasks T1 to T5, SUS, TPB items (UX-REVIEW section 7) | Planned 0.6 |

The headless emulator available to automated builds cannot lay out MAUI pages correctly, so layout and visual checks must happen on a real device.

---

## 13. Performance targets

| Metric | Target on a mid range phone | Status |
| --- | --- | --- |
| Cold start to Library | under 1.5s | To measure 0.3 |
| Open a 1 MB Markdown file | first screen under 700ms | To measure |
| Open a 5 MB DOCX or ODT | first screen under 2s | To measure |
| Open a 100 page PDF | Reading view under 3s, first Original page under 1s | To measure |
| Typing | no visible lag on 10,000 word notes | Improved in 0.2.4, to measure |
| Search 5,000 notes | under 150ms | To measure |
| APK size | under 40 MB | 37.6 MB |

---

## 14. Open principles, CI and delivery

### 14.1 Key facts

* The repository is public. GitHub does not charge Actions minutes for standard hosted runners on public repositories ([GitHub Docs, Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions)).
* .NET MAUI is MIT licensed (listed as MIT by the [.NET Foundation](https://old.dotnetfoundation.org/projects/dotnet-maui) and on its NuGet packages), so the toolchain allows any licence for the app.
* A public repository without a licence file is not open source. GitHub's own documentation says that without a licence the default copyright laws apply, so the owner keeps all rights and nobody may reproduce, distribute or create derivative works ([GitHub Docs, Licensing a repository](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository)). This is the chosen state for now.
* GitHub's terms still let other users view and fork a public repository on GitHub itself ([GitHub Terms of Service, section D.5](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service#5-license-grant-to-other-users)). That does not grant any right to publish the app elsewhere.

### 14.2 CI rules

1. Visual Studio is the main build machine; CI is a safety net.
2. One workflow, one Linux job: contrast check, layout lint, Core tests, Android build.
3. Runs on demand and on version tags; pull requests to `main` with path filters once contributors arrive.
4. Superseded runs are cancelled; NuGet is cached; the job times out after 25 minutes.
5. Only the APK is kept, briefly. The release keystore never enters the repository.

Status: Built (`.github/workflows/build.yml`).

### 14.3 Licence (deferred) and open principles

**Licence decision.** No open source licence for now. There is no `LICENSE` file and no SPDX header. A short notice in the README states "Copyright the owner. All rights reserved. The source is public so anyone can see how the app works and check its privacy promises."

**Open principles.** These hold whatever licence is chosen later, and they are what users and testers are promised.

| ID | Principle | How it is kept |
| --- | --- | --- |
| O1 | **No telemetry, analytics, crash reporting or ads, ever** | Section 11.4 bans the SDKs; section 14.5 states it to users; the data safety form says so (GP3) |
| O2 | **No account, works offline** | No sign in screen, no server run by VisaryNotes (section 1.5) |
| O3 | **The user owns the data** | Notes are plain Markdown; backup and export in open formats (section 8) |
| O4 | **Code in public view** | The repository stays public, so anyone can read the code and check O1 to O3. The README says plainly that viewing is allowed but copying and redistribution are not |
| O5 | **Same code everywhere** | The Play build and the GitHub build come from the same public tag, with nothing hidden (section 14.7) |
| O6 | **Free, no paywall, no in app purchases** | Section 14.7 |
| O7 | **Open formats in and out** | ODT, RTF, DOCX, Markdown, HTML, plain text (sections 7 and 8) |

**Keeping the licence option open**

| Rule | Why |
| --- | --- |
| Only permissive dependencies (section 11.2) | A GPL dependency would force the whole app under GPL. Permissive ones allow any later choice |
| No outside code contributions yet | Without a licence or contribution terms, code from others has unclear ownership and would complicate a later licence choice. Issues, ideas, translation suggestions and donated test documents are welcome |
| Third party notices | `licenses/` and an in app "Licences" screen, as Apache 2.0 and OFL require, whatever the app's own licence |
| Name and logo | Reserved, noted in the README |

**If a licence is chosen later.** GPL-3.0-or-later keeps the document engine from being taken into closed apps. Apache-2.0 allows wider reuse. Apache 2.0 code such as PdfPig is compatible with GPL-3.0 but not GPL-2.0 ([FSF licence list](https://www.gnu.org/licenses/license-list.html)). Choosing either would also reopen F-Droid (section 14.6) and outside code contributions.

### 14.4 Community and governance

| Item | Detail | Status |
| --- | --- | --- |
| `README.md` | What the app is, screenshots, build in Visual Studio, links | Built, to extend |
| `CONTRIBUTING.md` | How to report a bug, suggest a feature or donate a document. States that code pull requests are not accepted until a licence is chosen (section 14.3) | Planned 0.3 |
| `CODE_OF_CONDUCT.md` | Contributor Covenant 2.1 | Planned 0.3 |
| `SECURITY.md` | Private reporting through GitHub security advisories; a malformed file that crashes or hangs a reader counts as a security issue | Planned 0.3 |
| Issue templates | Bug (device, Android version, steps), Feature (which parity or create item it serves), **Document that will not open** | Planned 0.3 |
| **Corpus contributions** | Users can donate a file that opens badly, with permission, through the "Document that will not open" template. Each one becomes a test fixture. This turns the community into the engine behind point of difference D2 | Planned 0.3 |
| Decision making | The owner is maintainer and final decision maker; proposals that touch positioning are checked against section 2 | Planned 0.3 |
| Translations | String resources extracted; translations suggested through issues, with the contributor's written permission to use them | Planned 0.6 |
| Changelog | `CHANGELOG.md` in Keep a Changelog style, one entry per release | Planned 0.3 |

### 14.5 Privacy

* No telemetry, analytics, crash reporting or advertising SDKs, ever. This is a point of difference and is stated in the app and the store listing.
* Nothing leaves the phone unless the user shares, exports or backs up to a place they choose.
* Remote images in Markdown are off until the user turns them on.
* Feedback comes only from opt in channels: GitHub issues, beta questionnaires, an in app "Report a problem" that opens a prefilled issue without attaching data.
* A short privacy policy (`PRIVACY.md`, published as a web page) states all of the above. Google Play requires the link in Play Console and inside the app (section 14.7), so Settings gets a "Privacy" row.
* Google Play itself may collect crash and performance data for the developer console (Android vitals). That is collected by Google, not by the app. The privacy policy says so plainly, and the app adds no reporting of its own.

### 14.6 Distribution

| Channel | Plan | Status |
| --- | --- | --- |
| GitHub Releases | Signed APK per version tag, with checksums and the changelog | Planned 0.3 |
| F-Droid | F-Droid only includes free and open source software ([F-Droid Inclusion Policy](https://f-droid.org/docs/Inclusion_Policy)), so it is closed to VisaryNotes while there is no licence. A forum thread also reports that .NET MAUI apps cannot be built on its build server yet ([F-Droid Forum](https://forum.f-droid.org/t/why-isnt-c-net-maui-supported/24842)) | Not applicable; revisit if a licence is chosen |
| Installers that track GitHub Releases | Apps such as update checkers that follow a repository's releases can install the GitHub APK. Which ones to mention is to be verified | Planned 0.6 |
| **Google Play** | A main channel, not an afterthought. Same source, no Play Services, Android App Bundle signed through Play. Details in section 14.7 | Closed test 0.6, production 1.0 |
| Reproducible builds | Document the exact SDK and workload versions so anyone can rebuild the release APK and check it matches the public code (principle O4) | Planned 0.6 |

### 14.7 Google Play, with open principles

The owner holds all rights, so publishing on Play needs no licence decision. The rules below make sure the Play app keeps every open principle in section 14.3.

**Play requirements that apply**

| ID | Requirement | What VisaryNotes does | Status |
| --- | --- | --- | --- |
| GP1 | **Target API.** New apps and updates must target Android 16 (API 36) from 31 August 2026 ([Play Console Help, target API](https://support.google.com/googleplay/android-developer/answer/11926878?hl=en-GB)) | Already targets API 36 (A3). Each year's new target is added to the release checklist | Built |
| GP2 | **App Bundle.** New apps must be uploaded as an Android App Bundle (AAB), required since August 2021 ([Android Developers Blog](https://android-developers.googleblog.com/2021/06/the-future-of-android-app-bundles-is.html)). Microsoft's guide says the MAUI Release package format must be set to bundle for Play ([Microsoft Learn](https://learn.microsoft.com/en-in/dotnet/maui/android/deployment/publish-google-play?view=net-maui-8.0)) | Release builds produce an AAB for Play and a signed APK for GitHub. CI builds both | Planned 0.3 |
| GP3 | **Data safety form.** Every app must complete it, even one that collects no data ([Play Console Help, data safety](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en)) | Declare: no data collected, no data shared. Kept true by section 11.4 and 14.5 | Planned 0.6 |
| GP4 | **Privacy policy.** A link is required in Play Console and inside the app ([User Data policy](https://support.google.com/googleplay/android-developer/answer/10144311?hl=en)) | `PRIVACY.md` published as a page; Settings "Privacy" row | Planned 0.3 |
| GP5 | **Closed testing before production.** Personal developer accounts created after 13 November 2023 must run a closed test with at least 12 testers for 14 days in a row before applying for production. Organisation accounts are exempt. This is reported by secondary sources such as [Testers Community](https://www.testerscommunity.com/blog/google-play-12-testers-policy); confirm in Play Console before planning dates | The 0.6 community beta is the closed test, with 15 to 30 testers so drop outs do not break the 12 tester minimum | Planned 0.6 |
| GP6 | **Content rating questionnaire** and target audience | A productivity tool with no user generated content shared between users; expect the lowest rating | Planned 0.6 |
| GP7 | **Developer verification for apps installed outside Play.** Google began enforcing it on 30 September 2026 in Brazil, Indonesia, Singapore and Thailand, with a global rollout planned for 2027. It covers APKs from GitHub and F-Droid; unverified apps can still be installed through an "advanced flow" with a waiting period ([Android Authority](https://www.androidauthority.com/how-to-bypass-android-developer-verification-sideloading-rules-3718002/), [F-Droid Forum](https://forum.f-droid.org/t/google-will-require-developer-verification-to-install-android-apps-including-sideloading/33123)) | Register the package name and the GitHub release signing key under the owner's verified developer account, so GitHub APKs install normally | Planned 0.3; check rules again before 1.0 |

**Signing and update paths**

| Topic | Decision |
| --- | --- |
| Two signing keys | With an AAB, Google re-signs the app for delivery with the app signing key it holds (Play App Signing); the owner only holds the upload key. To verify in Play Console help before the first upload. The GitHub APK is signed with the owner's own release key |
| Users cannot switch channel by updating | Android refuses an update signed by a different key. A user who moves from the GitHub APK to Play, or the other way, must back up (S1), uninstall, install, and restore. The README and release notes say so; backup in 0.3 makes the move safe |
| Reproducible builds | Apply to the GitHub APK, which anyone can rebuild and compare. The Play build is made from the same tagged commit, and the tag is named in the Play release notes |
| Keys | Upload key and release key are kept outside the repository, with an offline backup. Losing the release key ends updates for GitHub users |

**Open principles on Play**

| Rule | Detail |
| --- | --- |
| Same code | The Play build contains nothing that is not in the public repository at the release tag. No closed modules, no Play only features |
| Source link | The store listing and the in app "About" screen link to the public repository, described as "source available", never as "open source" while there is no licence |
| Price | Free, no ads, no in app purchases. Donations, if any, link out from the repository, not from the app |
| Wording | Store text, README and the app never call VisaryNotes "open source" until a licence exists, so no claim is misleading |

**Store listing**

| Element | Plan |
| --- | --- |
| Title | VisaryNotes, with a short description built on the tagline: "Read what you're sent. Write what you think." |
| Full description | Leads with the two jobs (open any document, write notes) and the loop between them, then the three trust points: offline, no tracking, code you can read |
| Screenshots | One per job: Reading view of a DOCX, a PDF, the editor, Quote to note, a widget. Taken on a real phone after the release blockers are fixed |
| Category | Productivity |
| Data safety section | "No data collected" and "No data shared", matching GP3 |

---

## 15. Release plan

### 15.1 Release blockers (owner's phone, build 0.2.6)

| ID | Defect | Breaks |
| --- | --- | --- |
| B1 | Reader top bar is transparent; the file name and progress sit on top of the text | P2, V1 |
| B2 | Bars show no edge or tint, so they do not read as bars | P2, V1 |
| B3 | Editor live styling no longer styles headings and quotes | P3, E1 |
| B4 | Reading/Original bar has no background; text shows between and below its buttons, down into the gesture area | P2, 4.5 |
| B5 | With the keyboard open, the editor's pill and toolbar are hidden | P3, E2 |
| B6 | Plain text Reading view keeps hard line breaks, so sentences break mid line | P8, R6 |
| B7 | Lines of dashes wrap and leave a stray "-" | R7 |
| B8 | Quote source links show as raw `visary-doc://` code in the editor | E12 |
| B9 | The editor strip repeats the note's heading, larger than the heading itself | E13 |
| B10 | Share is the editor's primary action, above writing actions | E14 |

Two earlier fixes (bar tint in 0.2.6, keyboard lift in 0.2.4) did not take effect on the owner's phone. Root cause must be found on a real device before more visual work.

### 15.2 Releases

| Release | Theme | Scope | Exit criteria |
| --- | --- | --- | --- |
| **0.3 Parity and trust** | Close the gaps that stop people trusting the app | B1 to B10; backup and restore (S1, S2); README rights notice and open principles (14.3), contributing, code of conduct, security policy, issue templates including corpus donation; third party licences screen; first GitHub Release; release checklist; first performance measurements; Play developer account and verification (GP7); AAB build (GP2); privacy policy and Settings row (GP4) | Every blocker verified on a real phone; a full backup restores on a second phone; the README states the rights and the open principles; an AAB uploads to an internal test track |
| **0.4 Writing** | Editing on par with Markdown apps (P3, P4, P5) | Undo and redo, find and replace, list continuation, checklist ticking in preview, images, tables, `[[links]]` and backlinks, tags, notebooks, sort, templates, outline, Markdown bundle export, follow system theme | Owner writes daily notes for two weeks without needing another app |
| **0.5 Reading and the loop** | Build the create column | Quote anchors and jump to passage, highlights digest, highlight colours, quotes to Word, ODT and RTF export, PDF export and print, search inside documents, side by side on foldables and tablets, backdrop blur, checklist widget, Quick Settings tile, shortcuts, keyboard support | A quote round trip (select, quote, tap source, land on the passage) works on phone, foldable and tablet |
| **0.6 Community beta** | Learn from real people | 15 to 30 testers from section 1.3, run as the Google Play closed test (GP5) with an APK on GitHub for people without Play; data safety form and content rating (GP3, GP6); usability rounds of 5; translations; reproducible build notes; links to installers that track GitHub Releases | At least 12 testers stay in the closed test for 14 days in a row; measures in section 16 reach their targets or the plan is revised |
| **1.0** | Release | Fixes from the beta; accessibility pass; Play production release and store listing built on the frame in 2.1 (section 14.7); GitHub Release of the same tag | No open severity 3 or 4 findings; Play listing approved; the Play and GitHub builds come from the same tag |

---

## 16. Success measures

No telemetry, so every measure comes from the beta questionnaire, usability sessions or public signals.

| Measure | Target for 1.0 | Source |
| --- | --- | --- |
| Testers who make VisaryNotes their default app for opening received documents (the TPB target behaviour) | 50 percent or more | Beta questionnaire at 4 weeks |
| First open success on the corpus and on testers' own files | 95 percent or more | Corpus tests, task T1 |
| Testers who used Quote to note at least once a week | 40 percent or more | Questionnaire |
| System Usability Scale | 75 or more | After usability sessions |
| Perceived behavioural control score (Ajzen items) | Higher than attitude and norm scores, or rising between rounds | Questionnaire |
| Documents donated to the corpus | 50 by 1.0 | Issue tracker |
| Release blockers open at release | 0 | Section 15.1 |
| Play rating | 4.3 or more, with every review answered | Google Play Console (public reviews, not telemetry) |
| Installs from Play and downloads from GitHub | Tracked per release, no target | Play Console totals, GitHub release download counts |

---

## 17. Later and open questions

### 17.1 Later

Version history; work in folder mode; sync through a user chosen service (WebDAV, Syncthing folder); ODS and ODP viewing; math and Mermaid; drawing; Wear OS capture; iOS and Windows builds from the same code; on device features such as summaries, only if they run fully offline.

### 17.2 Open questions

1. **Licence.** Deferred by the owner. Revisit before 1.0 or when outside code contributions are wanted (section 14.3).
2. **Name.** The package is settled as `com.vishvplus.visarynotes`. Confirm the app name VisaryNotes, and whether to register it.
3. **Repository visibility.** Assumed to stay public for principle O4. Confirm, since a private repository would also end free CI minutes (section 14.1).
4. **Test devices.** Which phone, and whether a foldable or tablet is available, for the release checklist.
5. **Organisation model.** Tags only, notebooks only, or both (section 6.1, N9 and N10).
6. **Play account type.** A personal account needs the 12 tester closed test (GP5) and shows the owner's name on the listing; an organisation account skips the test but needs a registered organisation. Which one to open.
7. **Play App Signing.** Confirm in Play Console help how app signing keys work for new apps, and whether the owner can supply their own key so Play and GitHub builds share one (section 14.7).

---

## Appendix A. References

* Keller, K. L., Sternthal, B. and Tybout, A. (2002). Three Questions You Need to Ask About Your Brand. Harvard Business Review, September 2002. https://store.hbr.org/product/three-questions-you-need-to-ask-about-your-brand/R0209F
* Kim, W. C. and Mauborgne, R. Blue Ocean Strategy. Harvard Business School Press. ERRC grid template: https://www.blueoceanstrategy.com/wp-content/uploads/2022/12/Creating-your-Eliminate-Reduce-Raise-Create-Grid.pdf
* Barney, J. (1991). Firm resources and sustained competitive advantage. Journal of Management, 17(1), 99 to 120.
* Ajzen, I. (1991). The theory of planned behavior. Organizational Behavior and Human Decision Processes, 50(2), 179 to 211.
* Rogers, E. M. (2003). Diffusion of Innovations (5th ed.). Free Press.
* F-Droid Inclusion Policy. https://f-droid.org/docs/Inclusion_Policy
* F-Droid Forum, Why isn't C# .NET MAUI supported? https://forum.f-droid.org/t/why-isnt-c-net-maui-supported/24842
* Free Software Foundation, Various Licenses and Comments about Them. https://www.gnu.org/licenses/license-list.html
* .NET Foundation, .NET MAUI project page. https://old.dotnetfoundation.org/projects/dotnet-maui
* GitHub Docs, Actions billing. https://docs.github.com/en/billing/concepts/product-billing/github-actions
* Play Console Help, Target API level requirements. https://support.google.com/googleplay/android-developer/answer/11926878?hl=en-GB
* Play Console Help, Provide information for Google Play's Data safety section. https://support.google.com/googleplay/android-developer/answer/10787469?hl=en
* Google Play, User Data policy. https://support.google.com/googleplay/android-developer/answer/10144311?hl=en
* Android Developers Blog (June 2021), The future of Android App Bundles is here. https://android-developers.googleblog.com/2021/06/the-future-of-android-app-bundles-is.html
* Microsoft Learn, Publish an Android app for Google Play distribution. https://learn.microsoft.com/en-in/dotnet/maui/android/deployment/publish-google-play?view=net-maui-8.0
* Testers Community, Google Play 12 testers policy (secondary source). https://www.testerscommunity.com/blog/google-play-12-testers-policy
* Android Authority, developer verification and sideloading. https://www.androidauthority.com/how-to-bypass-android-developer-verification-sideloading-rules-3718002/
* F-Droid Forum, Google will require developer verification to install Android apps. https://forum.f-droid.org/t/google-will-require-developer-verification-to-install-android-apps-including-sideloading/33123
* GitHub Docs, Licensing a repository. https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository
* GitHub Terms of Service, section D.5. https://docs.github.com/en/site-policy/github-terms/github-terms-of-service#5-license-grant-to-other-users
* Google Keep Help, delete and restore notes. https://support.google.com/keep/answer/6262770
* Obsidian Help, Outline. https://obsidian.md/help/plugins/outline
* Project documents: [STRATEGY.md](STRATEGY.md), [DESIGN.md](DESIGN.md), [UX-REVIEW.md](UX-REVIEW.md), [archive/SPEC-0.7.md](archive/SPEC-0.7.md)
