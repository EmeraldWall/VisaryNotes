# VisaryNotes Design Rationale

Version 1.1 | Date: 2026-10-17 | Companion to [SPEC.md](SPEC.md) and [STRATEGY.md](STRATEGY.md)

Every visual and interaction decision in VisaryNotes is listed here with the reason behind it. If a decision cannot be justified by a principle, a measurement or a cited source, it does not ship. Where a reason is the design team's own judgement, it is labelled **Rationale**. Where it rests on research, it carries a citation.

Version 1.0 responded to three pieces of feedback on prototype v2.

1. The shapes were too rounded.
2. The colours were not legible enough.
3. The app must be usable with one hand, by left and right handed people.

Version 1.1 responds to feedback on prototype v3.

4. Nobody owned consistency and padding. Section 4.7 adds a token system and an automated check, and section 4.8 names who owns it.
5. The glass effect had disappeared. Section 4.2.1 explains why and how glass is now visible without losing legibility.
6. Glass must also apply to Android home screen widgets. Section 4.2.2 sets out what the platform allows and the frosted widget design.

---

## 1. Process: design thinking applied

The work follows the UK Design Council's **Double Diamond**, which splits design into Discover, Define (the problem space) and Develop, Deliver (the solution space), alternating divergent and convergent thinking ([Design Council, History of the Double Diamond](https://www.designcouncil.org.uk/our-resources/the-double-diamond/history-of-the-double-diamond/)). Within each diamond, the Stanford d.school modes (empathize, define, ideate, prototype, test) are used as working methods, not a strict sequence ([Stanford d.school, Design Thinking Bootleg](https://dschool.stanford.edu/resources/design-thinking-bootleg)).

| Phase | What has been done | Output |
| --- | --- | --- |
| Discover | Competitor review, VRIO, Theory of Planned Behavior, Innovation Diffusion Theory | [STRATEGY.md](STRATEGY.md) |
| Define | Problem statements and "How might we" questions (section 2) | This document, section 2 |
| Develop | Prototype v1 (glass showcase), v2 (reader plus notes), v3 (this system) | Design canvas |
| Test | Owner review of v1 and v2. Next, the beta study in STRATEGY.md section 5 plus the reach test in section 9 here | Feedback that drove v3 |
| Deliver | SPEC.md version 0.4 | Build in Visual Studio |

---

## 2. Problem definition

### 2.1 Point of view statements

* A **person who receives documents on their phone** needs to read them comfortably and keep what matters, because office files are hard to read on a small screen and the useful parts get lost.
* A **person holding a phone in one hand** (on a bus, carrying a bag, holding a coffee) needs every frequent action within reach of whichever thumb they use, because stretching to the top of the screen risks dropping the phone or needing the other hand.
* A **person reading for a long time** needs text with high, stable contrast on a calm surface, because low contrast and busy backgrounds tire the eyes and slow reading.

### 2.2 How might we

1. How might we make the thumb, not the index finger, the default input for every frequent action?
2. How might we make the app equally good for left and right hands without asking the user to configure it?
3. How might we keep a distinctive glass identity without letting it touch legibility?
4. How might we use shape and colour to tell paper (content) apart from tools (chrome) at a glance?

---

## 3. Design principles

Five principles, in priority order. When two conflict, the higher one wins.

| # | Principle | Meaning |
| --- | --- | --- |
| P1 | **Legible before beautiful** | Text contrast and calm backgrounds come before any effect |
| P2 | **One thumb, either hand** | Frequent actions live in the bottom third and are mirrored or centred |
| P3 | **Content first** | Paper takes the screen. Tools are thin and can hide |
| P4 | **Every surface has a material** | Each element is paper, glass or ink, never a vague "card" |
| P5 | **Crisp, not soft** | Small radii and a single chamfer motif instead of pills and blobs |

---

## 4. CMF system (Colour, Material, Finish)

CMF is used here the way industrial designers use it on physical products. Every on screen surface is treated as if it were made of something real, and that "something" decides its colour, its translucency and its texture. The physical metaphor is a **reading desk**. A matte desk top, sheets of paper on it, a cut glass ruler resting over the paper, and ink on the paper.

### 4.1 Materials

| Material | Real world analogue | Used for | Why |
| --- | --- | --- | --- |
| **Desk** | Matte painted desk top | The backdrop behind panes on tablets and foldables, and behind pages in Original view | A neutral, slightly darker field makes paper read as a separate object (figure and ground). It is low chroma so it never competes with text |
| **Paper** | Uncoated paper | Every surface that holds content: Library list, reader, editor, notes, document pages | Long reading needs an opaque, uniform background. Contrast stays constant because nothing shows through |
| **Glass** | Cut, frosted glass | Tools only: bottom bar, top status strip, selection menu, linked note sheet, formatting bar | Glass signals "this floats above the content and can move away". Its frost lets the user sense the content underneath without reading through it |
| **Ink** | Printing ink | All text and icons | One flat colour family per theme, no gradients, no transparency on text |
| **Accent light** | A coloured light behind glass | The one primary action per screen, the current selection, links | A single accent keeps the eye on the most important action (see 4.3) |

**Rule.** Text longer than a label (more than about four words) is never placed on glass. It always sits on paper. This is principle P1 applied to materials.

### 4.2 Finishes

| Finish | Applies to | Specification | Why |
| --- | --- | --- | --- |
| **Matte** | Paper, desk | Flat colour. No grain, no gradient, no texture | Texture under text lowers effective contrast and adds visual noise. Paper must be calm |
| **Satin etched** | Glass | Backdrop blur 24dp at 150 percent saturation, tint at 62 percent that rises to 85 percent over images (adaptive), 3 percent grain, a soft sheen on the top half | Low enough for content to be clearly visible through the glass, high enough to keep every contrast target (section 4.2.1). The grain stops glass looking like flat plastic. It is the only texture in the app |
| **Bevel glint** | Primary action | A 6dp light triangle in the chamfered corner | The cut corner catches light like the edge of cut glass, tying the accent button to the glass material |
| **Cut edge** | Glass | 1dp light line on the top edge (the cut catching light), 1dp darker line on the bottom edge | This is how real cut glass shows its thickness. It separates the bar from the paper without a drop shadow |
| **Polished** | Pressed and selected states | Glass opacity rises to 100 percent and a 2dp accent edge appears on the leading side | Feedback by change of finish rather than by colour alone, so the state is visible to colour blind users too |
| **Ink flat** | Text, icons | Solid colour, 100 percent opacity | Semi transparent text changes contrast depending on what is behind it |

#### 4.2.1 Why glass vanished in v3, and the fix

In v3 the glass tint was 90 percent opaque and there was nothing behind the bars except plain paper. Glass at 90 percent over a flat colour looks like a flat bar, so the effect disappeared. The 90 percent value came from a worst case that was too strict. It assumed solid ink could sit behind the whole bar, which never happens once 24dp of blur averages the text into the paper.

The checker now tests glass the way it is actually seen.

| Situation | Backdrop after blur | Tint needed for every target | Tint used |
| --- | --- | --- | --- |
| Normal scrolling (text, paper, highlights, selection, accent controls, desk) | Paper with text averaged in at 15 percent, plus each of those colours | 50 percent in every theme | **62 percent** |
| A photo or large dark or bright block under a bar | Pure white or pure black | 77 to 83 percent depending on theme | **85 percent** |

**Adaptive glass.** The glass handler samples the blurred backdrop under each bar at low resolution, once when scrolling settles and at most a few times a second while it moves. If the sample is far from the paper colour (an image), the tint eases from 62 to 85 percent over 150ms and back. So glass is clearly transparent over text and stays legible over images.

**What makes the glass visible.**

* Content always continues under the bars. Lists, document text and images scroll beneath the top strip and the bottom bar.
* The 24dp blur at 150 percent saturation makes colours behind the glass glow softly, which is what reads as glass.
* A sheen on the top half (light themes 55 percent white, dark themes 6 to 8 percent) and the cut edge lines give the pane thickness.
* A fine grain makes it read as etched glass, not tinted plastic.

High Contrast themes keep glass switched off, because they exist for people who need maximum contrast.

#### 4.2.2 Glass on Android home screen widgets

What the platform allows.

* Android 12 added blur APIs for an app's own windows, such as `Window#setBackgroundBlurRadius` ([Android Open Source Project, window blurs](https://source.android.com/docs/core/display/window-blurs)). Widgets are drawn by the launcher through `RemoteViews`, so these APIs cannot blur the wallpaper behind a widget. This last point is our reading of how widgets work and will be confirmed on devices in phase 3.
* Apps cannot read the wallpaper image any more. `WallpaperManager.getDrawable` returns the default or throws on Android 13 and always throws on Android 14 unless the app holds the all files permission ([Android Developers, WallpaperManager](https://developer.android.com/reference/android/app/WallpaperManager)). So a pre blurred copy of the wallpaper is not an option.
* On Android 12 and newer the launcher rounds widget corners using `system_app_widget_background_radius` ([Android Developers, Android 12 widgets](https://developer.android.com/about/versions/12/features/widgets)). The outer radius is therefore the system's, not ours. Our 0, 2, 4dp and chamfer shapes apply inside the widget.

**Frosted widget design.**

| Layer | Specification | Why |
| --- | --- | --- |
| Frost tint | Light frost 80 percent, dark frost 85 percent | The lowest tints that keep ink at 7 to 1 and labels at 4.5 to 1 over a pure white or pure black wallpaper (section 4.3.4). The wallpaper shows through, unblurred |
| Grain | A tiled 64px noise PNG at 4 percent | Makes the translucency read as frosted glass rather than a faded panel |
| Sheen | Top half gradient, as in the app | Thickness |
| Cut edges | 1dp light top edge, 1dp dark bottom edge | Same material language as in the app |
| Polarity | Auto (default), Light, Dark. Auto uses the wallpaper colour hints from `WallpaperManager.getWallpaperColors` where available, otherwise follows the app theme | Light frost on light wallpapers and dark frost on dark ones looks most like glass. Permission requirements for `getWallpaperColors` are to be confirmed in phase 3 |
| Fallback | Solid paper when the app is in a High Contrast theme | Accessibility first |

All of this is built from `layer-list` drawables, which `RemoteViews` supports. The prototype's widget board has a wallpaper switch (dark, bright, busy stripes) to judge legibility.

### 4.3 Colour

#### 4.3.1 Theory used

* **Itten's contrasts.** Johannes Itten's seven colour contrasts include light and dark (value), hue, saturation and complementary contrast ([Wikipedia summary of Itten's contrasts](https://en.wikipedia.org/wiki/Johannes_Itten); Itten, *The Art of Color*, Wiley, 1973 English edition). VisaryNotes uses them deliberately.
  * **Light and dark contrast carries all text.** Text and its background differ strongly in lightness, never only in hue.
  * **Saturation contrast marks the one action.** Neutrals are nearly grey, the accent is saturated, so the primary action stands out without being large.
  * **Complementary contrast separates highlight from selection.** Highlights are warm yellow (the highlighter pen convention), selection is cool blue (the platform convention). They also differ in lightness so colour blind users can tell them apart.
* **Perceptual construction in OKLCH.** Palettes are built in OKLCH, the polar form of Björn Ottosson's Oklab, which corrects hue and lightness shifts in blues found in CIELAB ([Wikipedia, Oklab](https://en.wikipedia.org/wiki/Oklab_color_space)). Neutrals are tinted very slightly toward the accent hue (chroma below 0.01) so greys and accent feel related, without visibly colouring the paper.
* **Positive polarity for reading.** Dark text on a light background gave better proofreading performance than the reverse for both younger and older adults ([Piepenbrock and colleagues 2013, Ergonomics 56(7), catalogue record](https://documentacion.fundacionmapfre.org/documentacion/publico/pt/bib/143702.do)). So the default theme is light, and dark themes are offered for low light, not as the default.
* **Dark grey, not black, for dark themes.** Material Design recommends dark grey surfaces (#121212) rather than black, partly because elevation is easier to show on grey ([Material Design 2, Dark theme](https://m2.material.io/design/color/dark-theme)). Dusk and Night follow this. A separate OLED theme uses true black for battery reasons, with off white text instead of pure white.

#### 4.3.2 Why the previous palette read poorly

Measured with the WCAG 2 formula, most text pairs in prototype v2 technically passed AA. For example secondary text on dark glass over the aurora measured 5.2 to 1 and on light glass 6.1 to 1. The problems were elsewhere.

1. **Contrast moved.** Glass was 58 to 64 percent opaque over a drifting, colourful aurora. Local contrast changed as the aurora moved, so the same label looked sharp in one place and faint in another.
2. **Same hue text on same hue background.** Mint text on dark green glass has almost no hue contrast, only lightness contrast, which reads as murky.
3. **Grain under text.** The 5 to 7 percent grain overlay sat under text too.
4. **Thin weights at small sizes.** Secondary text was 12 to 13px at regular weight.

#### 4.3.3 New rules

| Rule | Target |
| --- | --- |
| Body and title text on paper | 7 to 1 or more (WCAG 2.2 AAA, SC 1.4.6) ([W3C WCAG 2.2](https://www.w3.org/TR/WCAG22/)) |
| Secondary text (dates, counts, snippets) | 4.5 to 1 or more everywhere, and in practice 6 to 1 or more |
| Any text on glass | Measured against the worst case backdrop (paper, desk, ink or accent scrolling underneath) |
| Icons, borders, dividers, focus rings | 3 to 1 or more (SC 1.4.11) |
| Text on highlight and selection colours | 7 to 1 or more |
| Text colour | Neutral, never accent tinted, except links and the primary button label |
| Aurora | Removed from content screens. Kept only on the empty Library state and the widget preview |

#### 4.3.4 Theme palette with measured contrast

All values below come from `tools/design/contrast.py`, which runs against `tools/design/themes.json` and fails if any pair is under its minimum.

| Theme | Paper | Ink | Secondary | Accent | Ink on paper | Secondary on paper | Secondary on glass 62%, typical | Secondary on glass 85%, any image | Widget label, any wallpaper | Label on accent |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Daylight (default) | #FBFAF7 | #15181A | #474D52 | #0A6249 | 17.1 | 8.2 | 6.0 | 5.7 | 5.0 | 7.4 |
| Sepia | #F3EBD9 | #251C12 | #54452F | #7A3B10 | 14.1 | 7.8 | 5.7 | 5.4 | 4.8 | 8.5 |
| Dusk (dark grey) | #1C1E20 | #ECEDEE | #B9BEC3 | #5ACBA4 | 14.3 | 8.9 | 5.6 | 5.0 | 5.0 | 8.1 |
| Night (warm, low light) | #17140F | #ECE1CE | #BCAE96 | #E2A658 | 14.2 | 8.4 | 5.6 | 5.0 | 5.0 | 7.9 |
| OLED | #000000 | #E4E4E4 | #A9A9A9 | #5ACBA4 | 16.5 | 8.9 | 5.9 | 5.3 | 5.3 | 9.2 |
| High Contrast Light | #FFFFFF | #000000 | #262626 | #002FA7 | 21.0 | 15.1 | 15.1 (glass off) | 15.1 | 15.1 | 10.7 |
| High Contrast Dark | #000000 | #FFFFFF | #E6E6E6 | #FFD60A | 21.0 | 16.8 | 16.8 (glass off) | 16.8 | 16.8 | 14.9 |

Ink (body text) on glass is 8.0 to 1 or more in every case, including widgets. Highlight and selection colours in every theme keep ink at 7.2 to 1 or more. Dividers measure 3.5 to 1 or more against paper. Night uses an amber accent instead of emerald (**Rationale**: warm hues sit more comfortably in a warm, low light palette, and amber keeps saturation contrast against brown neutrals).

**Why emerald as the brand accent.** It is the organisation's name colour (EmeraldWall). It is rare among note apps that lean on yellow, blue and purple (**Rationale**, based on the apps listed in STRATEGY.md). At #0A6249 it reaches 7.0 to 1 on paper, so it can carry text, not only fills.

### 4.4 Shape

All shape values are tokens (section 4.7).

| Element | Shape | Why |
| --- | --- | --- |
| Paper (reader, editor, list) | 0dp corners when full width, 4dp when it sits as a pane on the desk | Paper has square corners. Full bleed surfaces touch the screen edge, where rounding wastes pixels |
| Glass bars and sheets | 4dp corners, 8dp chamfer on the top leading corner of sheets | Small radius avoids jagged anti aliasing while staying crisp (P5) |
| Buttons and chips | 2dp corners | Reads as precise, like a printed label |
| Primary action | Rectangle with one 8dp **chamfer** on the top trailing corner | The chamfer is the signature motif. It echoes a cut glass edge (CMF link) and is distinctive without being decorative. One motif, used once per screen, stays recognisable |
| Selection and focus | 2dp outline, square | Square outlines align with text lines and table cells |
| Avoided | Pills, circles for buttons, blob shapes | Rounded pills were the main reason v2 felt soft and generic |

The radius scale is deliberately short (0, 2, 4dp plus one chamfer size, 8dp). A short scale is easier to apply consistently in MAUI styles.

### 4.5 Elevation

No drop shadows on paper. Elevation is shown by material (glass above paper) and by the cut edge lines. In dark themes a glass surface is slightly lighter than the paper below, which matches Material's guidance that higher surfaces get lighter in dark themes ([Material Design 2, Dark theme](https://m2.material.io/design/color/dark-theme)).

### 4.6 Typography

| Use | Font | Size and weight | Why |
| --- | --- | --- | --- |
| Reading (notes, documents in Reading view) | Literata (SIL OFL) | 17sp regular, line height 1.6, maximum about 70 characters per line | A text face designed for long reading on screens. Size and line length chosen for comfortable reading (**Rationale**). User adjustable 14 to 28sp |
| UI labels and lists | Figtree (SIL OFL) | 15 to 16sp, weight 600 for titles, 500 for secondary | No UI text below 13sp and no weight below 500 under 15sp. Fixes the thin, small secondary text in v2 |
| Data (page numbers, counts, file types) | Figtree with tabular figures | 12 to 13sp, weight 700 | Numbers align in lists |
| Code and Markdown syntax marks | JetBrains Mono (SIL OFL) | 0.85em of surrounding text | Syntax marks stay visibly different from prose |
| Hyperlegible option | Atkinson Hyperlegible (SIL OFL) | Replaces both UI and reading fonts | For readers who need clearer letter shapes |

---

### 4.7 Spacing, sizing and layout tokens

Consistency is not left to judgement. Every spacing, size, radius and type value comes from one file, `tools/design/tokens.json`.

**Spacing scale.** A 4dp base, matching Material's guidance that components align to an 8dp grid while icons, type and some inner elements align to 4dp ([Material Design, spacing methods](https://material.io/design/layout/spacing-methods.html)).

| Token | Value | Use |
| --- | --- | --- |
| sp-1 | 4dp | Icon to label, segment gaps |
| sp-2 | 8dp | Bar padding, gaps between controls, pane gaps |
| sp-3 | 12dp | Inside a row (badge to text) |
| sp-4 | 16dp | Screen margin, row padding |
| sp-6 | 24dp | Reading margin, section spacing |
| sp-8 | 32dp | Large outer margins |
| sp-12 | 48dp | Strip height, touch target |

**Sizes.** Strip 48dp, control 48dp, primary 56dp, rows 48, 56 or 64dp by density, badge 40dp, icon 24dp. **Type.** Seven styles (caption 12/16, label 13/20, UI 15/20, title 16/24, reading 17/28, heading 20/28, title large 26/32). Every line height is a multiple of 4, so text sits on the 4dp grid. **Shape.** 0, 2, 4dp and the 8dp chamfer. **Borders.** 1dp hairline, 2dp strong, 4dp quote bar.

**Layout rules expressed as tokens.** Screen margin sp-4, reading margin sp-6, bar padding sp-2, gap between bar controls sp-2, row padding sp-2 by sp-4, gap inside rows sp-3, gap between sections sp-6, gap between panes sp-2.

### 4.8 Who owns consistency

| Layer | What enforces it |
| --- | --- |
| Single source | `tools/design/tokens.json` (spacing, size, shape, type, glass) and `tools/design/themes.json` (colours). Nothing else defines a value |
| Generation | `python3 tools/design/generate.py` writes `Tokens.xaml` and one `Theme.<name>.xaml` per theme for MAUI, `dimens.xml` for the Android widgets and `tokens.css` for the document viewer and prototypes. Generated files are never edited by hand |
| Components carry the padding | MAUI implicit styles and the custom controls (`BottomBar`, `GlassStrip`, `LibraryRow`, `ChamferButton`, `Segment`) set their own padding and sizes from tokens. Pages only arrange components, they do not set padding |
| Automated check | `python3 tools/design/lint_layout.py src` fails if any XAML `Padding`, `Margin`, `Spacing` or `CornerRadius` uses a literal number, or if any HTML or CSS spacing, radius or font size is not a token. The only exception is content that reproduces a document's own formatting, marked `lint:off` |
| Colour check | `python3 tools/design/contrast.py` fails if any theme misses a contrast target |
| When they run | Locally before every commit (a Visual Studio pre build step can run both), and in the CI job |
| Human owner | The design owner, which on this project is the app owner, approves any change to the token files. Claude Code keeps prototypes and code on tokens and runs both checks before each push |

When the check was first run on prototype v3 it found **483** hard coded spacing, radius and font size values. Prototype v4 has **zero**.

## 5. One handed, ambidextrous ergonomics

### 5.1 Evidence

* In 1,333 observations of people using phones in public, about 49 percent held the phone in one hand, 36 percent cradled it and 15 percent used two hands ([Hoober 2013, UXmatters](https://www.uxmatters.com/mt/archives/2013/02/how-do-users-really-hold-mobile-devices.php)). Among one handed users, about two thirds used the right thumb and one third the left (as quoted from the same study by [Garcia Media](https://garciamedia.com/?p=7518)). A one third left share is far too large to ignore.
* The area a thumb can reach depends on device size, hand size and grip, and can be modelled (Bergstrom-Lehtovirta and Oulasvirta 2014, CHI 2014, pages 1991 to 2000). Its practical consequence is that the top of the screen and the corner opposite the holding hand are the hardest to reach on modern phones.
* Fitts's law says that the time to hit a target grows with distance and shrinks with target size ([Fitts 1954, Journal of Experimental Psychology 47(6)](https://pubmed.ncbi.nlm.nih.gov/13174710)). For a thumb, "distance" means distance from where the thumb rests, which is the lower part of the screen.
* Android recommends touch targets of at least 48 by 48dp, about 9mm, separated by 8dp ([Google, Touch target size](https://support.google.com/accessibility/android/answer/7101858)).
* Android's gesture navigation uses inward swipes from both the left and right edges for Back ([Android Developers, gesture navigation](https://developer.android.com/guide/navigation/gesturenav)). Any app gesture from the side edges would fight with Back for both left and right handed users.

### 5.2 Screen zones

Zones are defined so they work for **both** hands at once. The bottom centre is reachable by either thumb, the bottom corners are each easy for one hand, and the top is hard for both.

| Zone | Share of height | Reach | What goes there |
| --- | --- | --- | --- |
| **Act** | Bottom 30 percent | Easy for both thumbs | Every frequent action. Search, primary action, view switch, the selection menu, the linked note sheet, formatting tools |
| **Read** | Middle 50 percent | Comfortable for scrolling and tapping list rows | Content. List rows are full width, so either thumb can tap the part nearest to it |
| **Glance** | Top 20 percent | Hard for both | Passive information only. File name, page number, save state. Nothing that must be tapped often |

### 5.3 Rules

| # | Rule | Theory |
| --- | --- | --- |
| E1 | **Bottom bar holds search, navigation and the primary action.** The top strip is passive | Fitts's law, thumb reach evidence |
| E2 | **Symmetric by default.** The primary action is centred. Paired actions sit mirrored left and right | Works for both hands without setup (HMW 2) |
| E3 | **Handedness setting** with Centre (default), Left and Right. Left or Right moves the primary action and the fast scroll handle to that side and orders paired buttons so the most used one is nearest the thumb | Fitts's law. Respects the one third left handed share |
| E4 | **No app gestures from side edges.** Library and notes open from the bottom bar or by swiping up on it | Avoids conflict with Android Back from both edges |
| E5 | **Full width list rows** at least 56dp tall in Compact density | Either thumb can tap the near end of a row |
| E6 | **Selection menu appears below the selection** when the selection is in the upper two thirds, and above it only when it is near the bottom | Keeps the menu in or near the Act zone |
| E7 | **Sheets instead of dialogs.** Confirmations, menus and pickers open as bottom sheets with buttons at the bottom | All choices end in the Act zone |
| E8 | **Pull down to reach.** Swiping down anywhere on the Library list beyond the top opens search in the bottom bar with the keyboard | A second route to search that never needs the top of the screen |
| E9 | **Reader tap zones** split the lower half of the page into left and right halves for previous and next page in Original view. Both halves are equal so neither hand is favoured | Symmetry, Fitts's law |
| E10 | **Targets** are 48dp minimum, 56dp for primary actions, with 8dp spacing | Android guidance |
| E11 | **Tablets and foldables.** Each pane has its own bottom bar, so whichever hand holds the device, the nearest pane's tools are at the bottom edge | Rationale, to be confirmed in the reach test (section 9) |

---

## 6. UX laws applied, component by component

| Component | Decision | Law or theory | Reasoning |
| --- | --- | --- | --- |
| Bottom bar | Three slots: Library or Back on one side, primary action in the centre, Search on the other side | Hick's law: choice time grows with the number of alternatives (Hick 1952, Quarterly Journal of Experimental Psychology 4(1), 11 to 26) | Three choices are scanned instantly. Everything else is one level down in a sheet |
| Bottom bar labels | Icon plus text label | Jakob's law: users expect apps to work like the others they use ([Nielsen Norman Group, Jakob's Law](https://nngroup.com/videos/jakobs-law-internet-ux/)) | Android users know bottom bars with labelled icons |
| Library | One paper panel with dividers, not separate cards | Gestalt common region and proximity (**Rationale**) | Reads as one list. Saves the space cards waste on gaps and borders |
| Filters | Four segments: All, Notes, Documents, Linked | Hick's law | Pinned moved into a sort option, fewer chips |
| Primary action | Accent colour, chamfered, centred, 56dp | Fitts's law, Itten saturation contrast | Biggest, closest, most saturated target |
| Reader | Paper full screen, tap toggles all chrome | Content first (P3) | Maximum reading area |
| View switch (Reading and Original) | In the bottom bar, not the top | Fitts's law, E1 | Switching views is frequent while reading |
| Selection menu | Highlight, Quote to note, Copy, in that order with Quote centred | Fitts's law, E2 | The action unique to VisaryNotes sits in the centre |
| Linked note sheet | Handle in the bottom bar, opens to 50 percent height | E7 | The note is reachable without leaving the document |
| Editor toolbar | Docked above the keyboard, scrolls sideways | Fitts's law | Tools live next to the thumbs that are already typing |
| Settings | Grouped sections, each a bottom sheet item | Hick's law | Short lists |

---

## 7. Accessibility beyond contrast

* Never colour alone. Selected states change finish (polished) and add an outline. Highlights also show a bar in the margin.
* Font scale to 200 percent without clipping. Bars grow in height instead of truncating labels.
* TalkBack order follows the visual order bottom bar last, so content is read first.
* Reduce transparency turns all glass into solid paper colour with the cut edge lines kept.

---

## 8. What changed from prototype v2 to v3, and v3 to v4

| Area | v2 | v3 | Reason |
| --- | --- | --- | --- |
| Corners | 16 to 26dp radii, pill chips, mixed radius cards | 0, 2 and 4dp plus one chamfer | Feedback, P5 |
| Search and actions | Top bar | Bottom bar | E1, Fitts's law |
| Primary action | Right side | Centre, or the user's hand side | E2, E3 |
| Colours | Translucent glass over a moving aurora | Opaque paper for content, glass for tools, aurora removed from content screens | 4.3.2, P1 |
| Glass (v4) | 90 percent tint over plain paper, effect invisible | 62 percent tint, content scrolls under bars, sheen and grain, adaptive 85 percent over images | 4.2.1 |
| Spacing (v4) | 483 hand written values | Tokens only, checked automatically | 4.7, 4.8 |
| Home screen widgets (v4) | 94 percent paper | Frosted glass, 80 percent light or 85 percent dark, grain, sheen, cut edges, launcher radius | 4.2.2 |
| Text | 12 to 13px regular secondary text | 13sp minimum at weight 500 or more | 4.6 |
| Default theme | Emerald Mist Dark | Daylight (light) | Positive polarity research |
| Themes | 8, several with tinted text | 7, neutral text, all AAA for body text | 4.3.4 |

---

## 9. How these decisions will be tested

| Test | Method | Pass criterion |
| --- | --- | --- |
| Contrast | `python3 tools/design/contrast.py` runs locally and in CI | Every pair at or above its minimum, including glass and widgets |
| Consistency | `python3 tools/design/lint_layout.py` on XAML and prototypes, locally and in CI | Zero violations |
| Widget legibility | Widgets placed over 5 real wallpapers (light, dark, busy photo, gradient, solid) on Android 12, 14 and 17 | Every label readable, no clipping by the launcher's corner radius |
| Glass visibility | Same participants as the legibility test are asked whether bars look like glass or a solid bar | Most say glass, with no drop in reading comfort |
| One handed reach | 6 or more right handed and 6 or more left handed participants complete 10 tasks with one hand on a 6.1 inch and a 6.7 inch phone (open a file, quote a passage, create a note, switch view, search) | No task needs the second hand. No grip change reported for frequent tasks |
| Legibility | Same participants read a 300 word passage in Daylight, Dusk and OLED and rate comfort | No theme rated below "comfortable" by more than one participant |
| Handedness setting | Participants try Centre, then their own side | Preference recorded. Default stays Centre unless most prefer their side |

---

## References

* Bergstrom-Lehtovirta, J., and Oulasvirta, A. (2014). Modeling the functional area of the thumb on mobile touchscreen surfaces. Proceedings of CHI 2014, 1991 to 2000. ACM.
* Design Council. History of the Double Diamond. https://www.designcouncil.org.uk/our-resources/the-double-diamond/history-of-the-double-diamond/
* Fitts, P. M. (1954). The information capacity of the human motor system in controlling the amplitude of movement. Journal of Experimental Psychology, 47(6), 381 to 391. https://pubmed.ncbi.nlm.nih.gov/13174710
* Google. Touch target size, Android Accessibility Help. https://support.google.com/accessibility/android/answer/7101858
* Android Developers. Ensure compatibility with gesture navigation. https://developer.android.com/guide/navigation/gesturenav
* Hick, W. E. (1952). On the rate of gain of information. Quarterly Journal of Experimental Psychology, 4(1), 11 to 26.
* Hoober, S. (2013). How do users really hold mobile devices? UXmatters. https://www.uxmatters.com/mt/archives/2013/02/how-do-users-really-hold-mobile-devices.php
* Itten, J. (1973). The Art of Color (rev. English ed.). John Wiley.
* Material Design 2. Dark theme. https://m2.material.io/design/color/dark-theme
* Material Design. Spacing methods. https://material.io/design/layout/spacing-methods.html
* Android Open Source Project. Window blurs. https://source.android.com/docs/core/display/window-blurs
* Android Developers. WallpaperManager reference. https://developer.android.com/reference/android/app/WallpaperManager
* Android Developers. Android 12 widgets improvements. https://developer.android.com/about/versions/12/features/widgets
* Nielsen Norman Group. Jakob's Law of Internet User Experience. https://nngroup.com/videos/jakobs-law-internet-ux/
* Oklab colour space (Ottosson, 2020), summary. https://en.wikipedia.org/wiki/Oklab_color_space
* Piepenbrock, C., and colleagues (2013). Positive display polarity is advantageous for both younger and older adults. Ergonomics, 56(7). Catalogue record: https://documentacion.fundacionmapfre.org/documentacion/publico/pt/bib/143702.do
* Stanford d.school. Design Thinking Bootleg. https://dschool.stanford.edu/resources/design-thinking-bootleg
* W3C. Web Content Accessibility Guidelines 2.2. https://www.w3.org/TR/WCAG22/
