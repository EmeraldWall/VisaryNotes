# VisaryNotes
A document reader and note taking app for Android phones, foldables and tablets, with a distinct glass design. Built with C# and .NET MAUI in Visual Studio.

* Product and technical specification: [docs/SPEC.md](docs/SPEC.md)
* Design rationale (CMF, colour theory, UX laws, one handed use, tokens): [docs/DESIGN.md](docs/DESIGN.md)
* Strategy analysis (VRIO, Theory of Planned Behavior, Innovation Diffusion Theory): [docs/STRATEGY.md](docs/STRATEGY.md)
* Design prototype sources: `design/prototype`

## Build in Visual Studio

1. Install Visual Studio 2026 (or 2022 17.14 or later) with the **.NET Multi-platform App UI development** workload. It brings the .NET 10 SDK, the Android SDK and an emulator.
2. Open `VisaryNotes.sln`.
3. Set **VisaryNotes.App** as the startup project, pick an Android emulator or a USB connected phone in the run target list, and press F5.
4. Run the Core tests from **Test Explorer** (`tests/VisaryNotes.Core.Tests`).

From a terminal with the `maui-android` workload installed:

```
dotnet test tests/VisaryNotes.Core.Tests
dotnet build src/VisaryNotes.App -f net10.0-android -c Release
```

The APK lands in `src/VisaryNotes.App/bin/Release/net10.0-android/com.visary.notes-Signed.apk`. It is signed with the debug key, so it can be sideloaded for testing but not published. Make signed releases with **Build > Archive** in Visual Studio.

CI (`.github/workflows/build.yml`) runs only on demand or on a `v*` tag, to save Actions minutes.

## Layout

* `src/VisaryNotes.Core`: readers (txt, Markdown, RTF, ODT), document model, HTML rendering for the Reading and Original views, themes, SQLite library. No MAUI dependency, fully unit tested.
* `src/VisaryNotes.App`: the MAUI app. Pages, glass controls, action pill, Android file intents and home screen widgets (`Platforms/Android`).
* `tools/design`: tokens and themes, the source for every size, colour and font in the app.

## Design checks

```
python3 tools/design/contrast.py           # every theme meets its contrast targets
python3 tools/design/lint_layout.py src design/prototype   # no hard coded spacing, radius or font size
python3 tools/design/generate.py           # regenerate MAUI, Android and CSS token files
```
