# VisaryNotes
A document reader and note taking app for Android phones, foldables and tablets, with a distinct glass design. Built with C# and .NET MAUI in Visual Studio.

* Product and technical specification: [docs/SPEC.md](docs/SPEC.md)
* Design rationale (CMF, colour theory, UX laws, one handed use, tokens): [docs/DESIGN.md](docs/DESIGN.md)
* Strategy analysis (VRIO, Theory of Planned Behavior, Innovation Diffusion Theory): [docs/STRATEGY.md](docs/STRATEGY.md)
* Design prototype sources: `design/prototype`

## Design checks

```
python3 tools/design/contrast.py           # every theme meets its contrast targets
python3 tools/design/lint_layout.py src design/prototype   # no hard coded spacing, radius or font size
python3 tools/design/generate.py           # regenerate MAUI, Android and CSS token files
```
