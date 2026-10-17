# VisaryNotes Strategy Analysis

Version 0.1 | Date: 2026-10-17 | Feeds into [SPEC.md](SPEC.md)

This document tests the VisaryNotes concept against three frameworks and turns the results into product decisions.

1. **VRIO** (resource based view) asks which parts of the product can give a lasting edge over existing apps.
2. **Theory of Planned Behavior (TPB)** asks what makes a person intend to start using it and keep using it.
3. **Innovation Diffusion Theory (IDT)** asks which product traits speed up adoption and who adopts first.

Competitor facts are cited. Judgements (for example "imitable at moderate cost") are the author's assessment and are marked as such. Nothing here is market research with real users yet. Section 5 explains how to test the assumptions with beta users.

---

## 1. Why the analysis matters for this app

VisaryNotes has two jobs, note taking and document reading. Each job already has strong free apps.

| Job | Existing alternatives on Android (examples) | What they do well |
| --- | --- | --- |
| Markdown and plain text notes | Markor, free and open source, works offline, edits Markdown, todo.txt and plain text files, exports HTML and PDF ([Markor on GitHub](https://github.com/gsantner/markor)) | Plain files, offline, power features |
| Linked Markdown notes | Obsidian, local plain Markdown files, core app free for personal use, sync is a paid add on ([AlternativeTo, Obsidian](https://www.alternativeto.net/software/obsidian/about/)) | Linking, plugins, cross platform |
| Quick notes | Google Keep, which added bold, italics, underline and headings on Android in 2023 ([Google Workspace Updates](https://workspaceupdates.googleblog.com/2023/08/google-keep-notes-android-rich-text-formatting.html)) | Speed, Google integration |
| ODF and office files | Collabora Office, free and open source, LibreOffice engine, opens .odt, .ods, .odp and Office formats, works offline ([OMG! Ubuntu](https://www.omgubuntu.co.uk/2020/02/collabora-office-libreoffice-for-android-ios)) | Full office fidelity and editing |
| RTF and ODT via cloud docs | Google Docs on Android, which added RTF import in 2016 and ODT import in 2017 ([Google Workspace Updates, 2016](https://workspaceupdates.googleblog.com/2016/01/support-for-more-formats-languages-and.html), [2017](https://workspaceupdates.googleblog.com/2017/01/page-setup-image-editing-and-odf-and-epub-in-docs-sheets-slides.html)) | Collaboration, conversion |

The gap the frameworks keep pointing to is **between** these apps. Note apps do not read RTF or ODT. Office suites read them but are heavy editors and are not note apps. Nobody on this list is built around the loop "receive a document, read it comfortably on a phone, pull what matters into my notes". That loop is where VisaryNotes should compete.

---

## 2. VRIO analysis

Framework source. Barney (1991) proposed that resources yield sustained advantage when they are valuable, rare, imperfectly imitable and non substitutable ([Barney 1991, Journal of Management 17(1), 99 to 120](https://doi.org/10.1177/014920639101700108)). Barney (1995) restated it as Value, Rarity, Imitability and Organization ([Barney 1995, Academy of Management Executive 9(4), 49 to 65](https://doi.org/10.5465/ame.1995.9512032192)).

Reading the table. V, R, I, O are answered Yes, Partly or No. "Costly to imitate" counts as Yes for I. The outcome follows the usual VRIO ladder: competitive disadvantage, parity, temporary advantage, unused advantage, sustained advantage.

| # | Resource or capability | V | R | I (costly to imitate) | O (organised to exploit) | Outcome | Reasoning |
| --- | --- | --- | --- | --- | --- | --- | --- |
| R1 | **Read, extract, write loop.** Highlights and quotes in a document become linked notes, with the document and note side by side on foldables and tablets | Yes | Yes (none of the apps in section 1 is built around it, author's assessment) | Partly to Yes. Needs R2 plus a linking data model plus adaptive UI working together | Not yet, it is not in spec 0.2 | **Unused advantage, becomes sustained if built** | The combination is the hard part to copy, not any single feature |
| R2 | **Own document engine** (RTF and ODT readers, shared document model, Reading and Original views) | Yes. People receive these files and phones read them badly | Partly. Collabora reads ODT with high fidelity but is a full office suite. Google Docs converts files into its own format | Yes. Parsers plus a test corpus of real files take a long time to get right | Yes, spec 0.2 section 7 | **Temporary to sustained advantage** | Fidelity grows with the test corpus, which competitors do not see |
| R3 | **Reading view tuned for phones** (reflow, reading fonts, themes, immersive mode) | Yes | Partly. Common in e-readers, rare for RTF and ODT on Android (author's assessment, to verify) | No. A competent team can copy it | Yes | **Temporary advantage** | Ship it early, it is the visible first-use win |
| R4 | **Visary Glass visual identity** | Partly. Helps image and recall, does not do a job by itself | Partly | No. Visual styles are easy to copy | Yes | **Temporary advantage at best** | Keep it, but never let it cost legibility or screen space |
| R5 | **Offline, no account, plain files** | Yes | No. Markor and Obsidian already work this way | No | Yes | **Competitive parity** | Table stakes for this audience. Missing it would be a disadvantage |
| R6 | **Multi format export** (md, txt, rtf, odt, docx, html, pdf) | Yes | Partly. Markor exports HTML and PDF. Office suites export office formats | Partly. The Open XML SDK makes DOCX easy, RTF and ODT writers take work | Yes, spec section 8 | **Temporary advantage** | Strong for the "send it back to someone" step of the loop |
| R7 | **Adaptive phone, foldable and tablet layouts** | Yes | Partly (author's assessment) | No | Yes, spec section 3 | **Temporary advantage** | Most valuable when it powers R1, document and note side by side |
| R8 | **Interactive widgets** | Yes | No. Widgets are common in note apps | No | Yes | **Parity** | Keep, but a "Continue reading" widget ties it to R1 |
| R9 | **Single developer, C# and Visual Studio** | Partly. Fast decisions, low cost | n/a | n/a | Constrained | **Risk to O for everything above** | Scope must be cut to what one person can maintain |

### 2.1 VRIO conclusions

1. **The lasting edge is R1 built on R2.** A note app that is also a good reader, where reading feeds the notes, is the only item that is valuable, rare and hard to copy as a whole. Spec 0.2 treated reading as a side feature. That has to change.
2. **R2 needs an organisational asset, the test corpus.** A growing set of real RTF and ODT files with expected results is what makes fidelity hard to copy. It belongs in the repo from phase 2.
3. **Glass (R4) is image, not advantage.** It must never take space away from content. That matches the feedback that the prototype "wastes a lot of space".
4. **Parity items (R5, R8) are non negotiable but should get minimum effort.**
5. **R9 limits O.** Cut .ods and .odp, math and Mermaid until the core loop is excellent.

---

## 3. Theory of Planned Behavior

Framework source. Ajzen (1991) holds that intention, the immediate cause of behaviour, is predicted by attitude toward the behaviour, subjective norm and perceived behavioural control, and that perceived control also predicts behaviour directly ([Ajzen 1991, Organizational Behavior and Human Decision Processes 50(2), 179 to 211](https://doi.org/10.1016/0749-5978(91)90020-T)).

**Target behaviour** (TPB needs a specific one). "Within the next month, set VisaryNotes as my app for opening documents I receive, and use it for my notes at least three days a week."

### 3.1 Beliefs and design levers

| TPB component | Likely beliefs (hypotheses to test) | Design levers |
| --- | --- | --- |
| **Attitude** (beliefs about outcomes) | "Files people send me will open and be readable." "It is fast." "It looks good." "My notes stay private and are mine." "It does not waste my screen." | First open must succeed on real files (R2 corpus). Immersive reading with chrome that hides. Compact density by default. Offline and no account stated on first launch |
| **Subjective norm** (what important others expect) | "Colleagues and teachers send me ODT and RTF and expect me to read and reply." "People I respect use Markdown." "My files must go back in a format others can open." | Export and "Save back" in the sender's format. Exports that look professional. Optional, off by default "Made with VisaryNotes" footer, never on by default |
| **Perceived behavioural control** (can I do it) | "Switching will not lose my notes." "I can get my notes out again." "I do not need an account or setup." "I understand where things are." | Import a folder of Markdown files (covers Markor and Obsidian users). Notes stored as plain Markdown, exit is always possible. Zero setup "Open with" path. One unified Library instead of separate silos |

### 3.2 TPB conclusions

1. **Perceived control is the weakest link for a new app.** Importing existing notes and "Open with" support move into phase 1 and 2, not later.
2. **Attitude is formed on the first opened document.** The Reading view must be excellent on a phone before anything decorative.
3. **Subjective norm points at the reply step.** Users read documents because someone sent them. Exporting back in the sender's format closes the loop.

---

## 4. Innovation Diffusion Theory

Framework sources. Rogers names five perceived attributes that explain the rate of adoption. Relative advantage, compatibility, complexity (negative), trialability and observability (Rogers, Diffusion of Innovations, 5th ed., Free Press, 2003). Moore and Benbasat (1991) built a measurement instrument for these perceptions in information technology and added image, voluntariness and result demonstrability ([Moore and Benbasat 1991, Information Systems Research 2(3), 192 to 222](https://doi.org/10.1287/isre.2.3.192)).

### 4.1 Attribute assessment

| Attribute | Current concept (spec 0.2, prototype v1) | Gap | Decision |
| --- | --- | --- | --- |
| **Relative advantage** | Opens RTF and ODT and takes notes | Shown as two separate apps in one shell | Make the read, extract, write loop the headline. One Library for notes and documents |
| **Compatibility** | Plain Markdown, Material 3, Android share and open intents | Existing Markdown folders cannot be used in place | Folder import and an optional "work in folder" mode for Markdown |
| **Complexity** (lower is better) | Three tabs, separate Files area | Users must decide where things live | One Library with type filters. Progressive disclosure for advanced tools |
| **Trialability** | Free, offline | First use still goes through the app's own UI | Make "Open with" the zero setup trial. A file opened from another app shows the reader at once with a single "Save to notes" action |
| **Observability** | Glass look | Results are not visible to other people | Exported files and the home screen widget are the visible results. Make both look polished |
| **Image** (Moore and Benbasat) | Distinct glass design | Risk of style over content | Keep the look, cut its footprint (section 6) |
| **Result demonstrability** (Moore and Benbasat) | Reading and Original switch | Not obvious in a store listing | Store screenshots show the same file in Original and Reading views side by side |
| **Voluntariness** | Personal app | None | Fine |

### 4.2 Who adopts first

Rogers' adopter categories (innovators, early adopters, early majority, late majority, laggards) suggest starting with groups who already feel the problem and like trying tools (source above). Likely first segments (hypotheses, not measured).

1. Markdown note takers on Android who also receive office files (students, researchers, writers).
2. People who use LibreOffice on a computer and get ODT files on their phone.
3. Owners of foldables and tablets who want document and notes side by side.
4. Privacy minded users who avoid account based apps.

Channels for these groups (suggestions). Open source communities, Android app forums and subreddits, LibreOffice and Markdown communities, and F-Droid if the app is open sourced (open question in the spec).

---

## 5. How to validate (no GitHub minutes needed)

| Step | Method | Output |
| --- | --- | --- |
| 1 | Short TPB questionnaire for 15 to 30 beta testers, built with Ajzen's standard 7 point items for attitude, norm, control and intention toward the target behaviour in section 3 | Which component limits intention |
| 2 | Moore and Benbasat style items for relative advantage, compatibility, complexity, trialability, result demonstrability | Which attribute to fix first |
| 3 | Task test. Each tester opens 5 of their own received documents (RTF, ODT, MD, TXT) on their phone | First open success rate. Main input for attitude |
| 4 | Corpus growth. Every file that fails (with permission) is added to the test corpus | Builds R2, the hard to copy asset |

---

## 6. Product decisions from this analysis

These are applied in SPEC.md version 0.3 and in the prototype.

| # | Decision | Driven by |
| --- | --- | --- |
| D1 | **One Library** holds notes and documents together, with filters All, Notes, Documents, Pinned. No separate Files tab | IDT complexity, TPB control |
| D2 | **Read, extract, write loop.** Select text in a document, then Highlight, Quote to note, or Copy. Each quote keeps a link back to its place in the document | VRIO R1 |
| D3 | **Document and note side by side** on foldables (book posture) and tablets. On phones a pull up note sheet sits over the document | VRIO R1 and R7 |
| D4 | **Space first layout.** Compact density by default, a slim top bar that holds search, chrome that hides while reading, no oversized titles, edge to edge reading surface | User feedback, TPB attitude, VRIO R4 |
| D5 | **Zero setup trial.** Files opened from other apps go straight to the reader with "Save to Library" and "Quote to note" | IDT trialability, TPB control |
| D6 | **Markdown folder import** in phase 1 | TPB control, IDT compatibility |
| D7 | **Reply in the sender's format.** Save back and export to RTF, ODT and DOCX stay in v1 | TPB subjective norm |
| D8 | **"Continue reading" widget** next to Quick Capture | VRIO R8 linked to R1, IDT observability |
| D9 | **Test corpus** of real files in the repo from phase 2 | VRIO R2 organisation |
| D10 | **Defer** .ods, .odp, math and Mermaid to after v1 | VRIO R9 |

---

## References

* Ajzen, I. (1991). The theory of planned behavior. Organizational Behavior and Human Decision Processes, 50(2), 179 to 211. https://doi.org/10.1016/0749-5978(91)90020-T
* Barney, J. (1991). Firm resources and sustained competitive advantage. Journal of Management, 17(1), 99 to 120. https://doi.org/10.1177/014920639101700108
* Barney, J. B. (1995). Looking inside for competitive advantage. Academy of Management Executive, 9(4), 49 to 65. https://doi.org/10.5465/ame.1995.9512032192
* Moore, G. C., and Benbasat, I. (1991). Development of an instrument to measure the perceptions of adopting an information technology innovation. Information Systems Research, 2(3), 192 to 222. https://doi.org/10.1287/isre.2.3.192
* Rogers, E. M. (2003). Diffusion of Innovations (5th ed.). Free Press.
* Markor. https://github.com/gsantner/markor
* Obsidian overview, AlternativeTo. https://www.alternativeto.net/software/obsidian/about/
* Google Keep rich text formatting on Android, Google Workspace Updates, August 2023. https://workspaceupdates.googleblog.com/2023/08/google-keep-notes-android-rich-text-formatting.html
* Collabora Office for Android and iOS, OMG! Ubuntu, February 2020. https://www.omgubuntu.co.uk/2020/02/collabora-office-libreoffice-for-android-ios
* Google Docs Android format support, Google Workspace Updates, January 2016. https://workspaceupdates.googleblog.com/2016/01/support-for-more-formats-languages-and.html
* Google Docs ODF import on Android, Google Workspace Updates, January 2017. https://workspaceupdates.googleblog.com/2017/01/page-setup-image-editing-and-odf-and-epub-in-docs-sheets-slides.html
