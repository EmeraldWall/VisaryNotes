# VisaryNotes UX Review, version 0.2.4

An expert review of the Android app as built, followed by improvements chosen with the Theory of Planned Behaviour, Innovation Diffusion Theory and VRIO from [STRATEGY.md](STRATEGY.md). The fixes marked **Fixed in 0.2.5** are in the code.

## 1. Method and limits

| Part | What was done | Source |
| --- | --- | --- |
| Heuristic evaluation | Each screen (Library, Reader, Editor, Settings, sheets, widgets) checked against Nielsen's ten usability heuristics, each problem rated on his 0 to 4 severity scale (0 not a problem, 1 cosmetic, 2 minor, 3 major, 4 catastrophe) | Nielsen Norman Group, 10 Usability Heuristics for User Interface Design, nngroup.com/articles/ten-usability-heuristics. Severity scale as reproduced in [MeasuringU, Rating the Severity of Usability Problems](https://measuringu.com/rating-severity/) |
| Cognitive walkthrough | Five core tasks walked step by step, asking at each step whether a first time user would know what to do, see the control, connect it to the goal and see progress | Wharton, Rieman, Lewis and Polson (1994), The Cognitive Walkthrough: A Practitioner's Guide, in Nielsen and Mack (eds.), Usability Inspection Methods, Wiley, pp. 105 to 140 |
| Accessibility check | Touch targets, contrast, screen reader labels, text scaling, colour independence | [Android Developers, Make apps more accessible](https://developer.android.com/guide/topics/ui/accessibility/apps) (48dp touch targets), [W3C WCAG 2.2](https://www.w3.org/TR/WCAG22/), `tools/design/contrast.py` |
| Evidence | The source code of 0.2.4, the owner's screenshots from a real phone (reader, receipt PDF, editor with keyboard, share dialog), and earlier rounds of feedback | This repository |

**Limits, stated plainly.** This is one evaluator's inspection, not a test with users. Nielsen's own guidance is that one evaluator finds only part of the problems, so severity ratings here are a first pass. The emulator in the build environment cannot lay out MAUI pages (see SPEC.md A4), so some findings rest on code reading and the owner's screenshots rather than on watching the app run. Section 6 sets out the user test that should follow.

## 2. Scorecard

| Area | Before (0.2.4) | After (0.2.5) |
| --- | --- | --- |
| Severity 4 problems | 1 | 0 |
| Severity 3 problems | 8 | 2 open (both need a device or users to settle) |
| Walkthrough tasks passing every step | 2 of 5 | 4 of 5 |
| Touch targets under 48dp | 0 interactive | 0 |
| Contrast failures (7 themes) | 0 | 0 |

## 3. Findings

Severity 0 to 4. H numbers are Nielsen's heuristics: H1 visibility of status, H2 match with the real world, H3 user control and freedom, H4 consistency and standards, H5 error prevention, H6 recognition rather than recall, H7 flexibility and efficiency, H8 aesthetic and minimalist design, H9 recover from errors, H10 help.

| ID | Screen | Problem | H | Sev | Evidence | Fix | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| U1 | Library | Removing a document permanently deleted its highlights and note links, from one swipe, with no confirmation and no undo | H3, H5, H9 | 4 | `LibraryRepository.RemoveDocumentAsync` deleted rows in `highlights` and `note_links` | "Remove from Library" only hides the entry; highlights and links stay; Undo restores it; reopening the file restores it too | Fixed in 0.2.5, tested |
| U2 | Library | Deleting a note had no undo. Notes are kept 30 days but nothing in the UI can reach them | H3, H9 | 3 | `MoveToTrashAsync` sets `DeletedAt`; no Trash view | Undo notice for 5 seconds after every deletion | Undo fixed in 0.2.5. Trash view still open (U15) |
| U3 | Library | Pin and delete were only reachable by swiping a row, which nothing hints at and which is awkward with TalkBack. On documents both swipe actions read "Remove" and did the same thing | H6, H4 | 3 | `LibraryPage.xaml` SwipeView, `PinText` and `DeleteText` | A visible 48dp "more" button on each row opens the action sheet. Also removes MAUI's SwipeView from every row, which is heavy on Android | Fixed in 0.2.5 |
| U4 | Library | First run showed one line of grey text and no way forward except the pill | H10, H6 | 3 | `EmptyText` label | Empty state names the core loop and gives three buttons: Open a document, Write a note, Bring Markdown notes, plus "Works offline. No account" | Fixed in 0.2.5 |
| U5 | Reader | Quote to note, the app's core feature, is invisible until the user happens to select text | H6, H10 | 3 | Selection menu appears only after a selection | One time tip on the first document opened | Fixed in 0.2.5. Device check needed for E6: Android's own selection toolbar may appear beside the app's menu |
| U6 | Reader | Quoting slid the note sheet over half the page, interrupting reading | H3 | 2 | `case "quote"` called `ShowLinkedNoteAsync` | Notice "Quoted into your note" with View; the page stays put | Fixed in 0.2.5 |
| U7 | Reader | Copy gave no feedback | H1 | 2 | `case "copy"` | Notice "Copied" | Fixed in 0.2.5 |
| U8 | Editor | Notes could only be shared as md, txt or html, which most recipients do not open in a word processor | H2 | 3 | `EditorPage.ShareAsync` | Share as Word (.docx): headings, lists, links, tables, quotes and code map to Word's built in styles. Verified by opening the file in LibreOffice and by round trip tests | Fixed in 0.2.5. RTF and ODT export still open |
| U9 | Editor | The keyboard covered the pill and the format bar | H1, H7 | 3 | Owner's screenshot, 0.2.3 | Pages lift above the keyboard (SafeAreaEdges All) | Fixed in 0.2.4 |
| U10 | All | Popups were Android system dialogs: centred, system font, out of thumb reach | H4 | 3 | Owner's screenshot of Share as | One BottomSheet for every popup | Fixed in 0.2.3 |
| U11 | Editor | The Link button was a bare arrow glyph, easy to read as "open" or "share" | H2, H6 | 2 | `Add("Link", "↗", ...)` | Chain link icon | Fixed in 0.2.5 |
| U12 | Library | Filter "Linked" is app jargon; it means documents and notes joined by quotes | H2 | 2 | `FilterLabels` | Candidate labels "Quoted" or "With notes"; pick by asking users | Open, test with users |
| U13 | Reader | The blur sat beside the bars, not under them | H8 | 3 | Owner's screenshots | In-page blur removed; native glass | Fixed in 0.2.2 |
| U14 | Reader | No find in document | H7 | 2 | No search UI in ReaderPage | Find bar in the Act zone | Open |
| U15 | Library | No Trash view, so a note deleted more than 5 seconds ago cannot be recovered in the UI | H3, H9 | 3 | See U2 | "Recently deleted" filter or a Settings entry | Open |
| U16 | Reader | "Save to Library", the key action for a first time trial, sits in the top strip, out of thumb reach (design rule E1) | H7 | 2 | `ReaderPage.xaml` SaveButton in the strip | Move it into the pill as the primary action while the document is unsaved | Open |
| U17 | Reader | Reading text size is fixed in CSS pixels and ignores Android's system font size | H7, accessibility | 3 | `ReaderSettings.FontSizePx` | Scale the reading size by the system font scale | Open, needs a device to verify |
| U18 | Editor | The strip repeats the first heading as the title, so the same words appear twice at the top | H8 | 1 | Owner's screenshot | Show the save state only, or hide the title while the heading is on screen | Open |

## 4. Cognitive walkthrough

| Task | Steps | Before | After |
| --- | --- | --- | --- |
| T1 Read a file someone sent | Tap file in a chat app, choose VisaryNotes in Open with, read | Pass | Pass |
| T2 Keep a passage from it | Select text, choose Quote to note, find the quote later | Fails at step 1 (nothing suggests selecting) and step 3 (sheet covers the page) | Pass, tip and notice. Device check pending for the selection toolbar |
| T3 Write and send a note to a colleague who uses Word | New note, write, Share | Fails at the last step (no Word format) | Pass |
| T4 Clean up the Library | Find pin and delete, undo a mistake | Fails at step 1 (hidden swipe) and step 2 (no undo, highlights lost on document removal) | Pass |
| T5 Start from an empty Library | Understand what the app is for and take a first step | Fails (no guidance) | Pass |

## 5. Accessibility

* **Touch targets.** Every interactive control is at least 48dp: pill buttons, segments, toolbar buttons, row "more" buttons, sheet rows and buttons (56dp), toggles (48dp touch area around a 32dp track), the notice action. Badges are 40dp but are not interactive.
* **Contrast.** All seven themes pass `tools/design/contrast.py` (body text 7:1, secondary 4.5:1, controls 3:1).
* **Not colour alone.** Toggles show state by knob position and fill; segments by fill; badges by filled versus outlined.
* **Screen readers.** Icon only controls have `SemanticProperties.Description`; toggles announce On or Off; sheet titles are headings. Row actions are now a button TalkBack can reach, not a swipe.
* **Open.** U17 (system font size in the reader).

## 6. Improvements through the three theories

STRATEGY.md set the target behaviour: "Within the next month, set VisaryNotes as my app for opening documents I receive, and use it for my notes at least three days a week." Each fix below was chosen because it moves one of the theory's levers for that behaviour.

### 6.1 Theory of Planned Behaviour

Ajzen (1991): intention follows attitude, subjective norm and perceived behavioural control, and perceived control also acts on behaviour directly.

| Component | What the review found | What changed |
| --- | --- | --- |
| **Perceived behavioural control** ("I can do this safely") | The weakest link. Actions were hidden (U3), mistakes could not be undone (U1, U2), and a document removal destroyed the user's own work (U1). People who fear losing work do not commit to an app | Undo after every removal, removal never destroys highlights, visible row actions, three clear first steps, "No account. Plain Markdown you can take anywhere" stated where a new user looks first |
| **Attitude** ("using it is good for me") | The core benefit, quoting a passage into a note with a link back, was never shown | First run copy names the benefit; the first document shows how to do it once; quoting no longer interrupts reading |
| **Subjective norm** ("people who matter expect this") | Notes went back to others only as md, txt or html | Share as Word (.docx), the format colleagues and teachers most often expect |

### 6.2 Innovation Diffusion Theory

Rogers (2003), with Moore and Benbasat (1991) for result demonstrability.

| Attribute | Change |
| --- | --- |
| **Relative advantage** | The empty state leads with what other note apps do not do: a quote that keeps a link to its page |
| **Complexity** (lower is better) | Fewer hidden gestures: row actions are visible; one tip teaches the one gesture that cannot be made visible (selection) |
| **Trialability** | Every removal is reversible, so trying things costs nothing. "Open a document" is the first button, matching the zero setup Open with path (D5) |
| **Compatibility** | "Bring Markdown notes" on the first screen, Word export out |
| **Observability and result demonstrability** | A Word file made in VisaryNotes looks native in Word and LibreOffice, so the result is seen by the people it is sent to |

### 6.3 VRIO

| Resource | Finding | Change |
| --- | --- | --- |
| **R1, the read, extract, write loop** (the only sustained advantage) | Valuable and rare, but the app was not **organised** to exploit it: users could not find it, and one swipe could destroy the highlights it produces | Discoverability (tip, empty state copy), protection (U1), flow (U6). This moves R1 from "unused advantage" toward "sustained advantage" |
| **R2, the document engine** | Strong after PDF, DOCX and DOC readers | No change; grow the test corpus from real files users report |
| **R6, multi format export** | Only three plain formats | Word export added; RTF and ODT export are the next step |
| **R4, glass identity** | Earlier blur cost legibility | Already resolved: native tint, nothing sits under a bar at rest |

## 7. Next: test with people

1. **Who.** Five people per round, from the first adopter segments in STRATEGY.md 4.2 (Markdown note takers who receive office files, LibreOffice users, privacy minded users). Nielsen argues that small qualitative rounds of about five users find most problems for the cost, followed by fixing and testing again ([Nielsen Norman Group, Why You Only Need to Test with 5 Users](https://nngroup.com/articles/why-you-only-need-to-test-with-5-users/), based on Nielsen and Landauer 1993). Five is for finding problems, not for measuring scores.
2. **Tasks.** T1 to T5 above, on the tester's own phone with documents they actually received.
3. **Measures.** Task success and time; the System Usability Scale after the session (Brooke 1996, SUS: a quick and dirty usability scale, in Usability Evaluation in Industry); the TPB and diffusion items from STRATEGY.md section 5.
4. **Settle the open items.** U5 (selection toolbar conflict), U12 (filter wording), U16 (Save to Library placement), U17 (font scaling).

## References

* Ajzen, I. (1991). The theory of planned behavior. Organizational Behavior and Human Decision Processes, 50(2), 179 to 211.
* Barney, J. (1991). Firm resources and sustained competitive advantage. Journal of Management, 17(1), 99 to 120.
* Brooke, J. (1996). SUS: a quick and dirty usability scale. In P. W. Jordan et al. (eds.), Usability Evaluation in Industry. Taylor and Francis.
* Moore, G. C. and Benbasat, I. (1991). Development of an instrument to measure the perceptions of adopting an information technology innovation. Information Systems Research, 2(3), 192 to 222.
* Nielsen, J. Why You Only Need to Test with 5 Users. Nielsen Norman Group. https://nngroup.com/articles/why-you-only-need-to-test-with-5-users/
* Nielsen, J. 10 Usability Heuristics for User Interface Design. Nielsen Norman Group, nngroup.com/articles/ten-usability-heuristics.
* Rogers, E. M. (2003). Diffusion of Innovations (5th ed.). Free Press.
* Wharton, C., Rieman, J., Lewis, C. and Polson, P. (1994). The cognitive walkthrough method: a practitioner's guide. In J. Nielsen and R. L. Mack (eds.), Usability Inspection Methods, pp. 105 to 140. Wiley.
* Android Developers. Make apps more accessible. https://developer.android.com/guide/topics/ui/accessibility/apps
* W3C. Web Content Accessibility Guidelines 2.2. https://www.w3.org/TR/WCAG22/
