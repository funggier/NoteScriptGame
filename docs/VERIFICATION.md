# NoteScriptGame v0.1 — verified execution record

Date: 2026-11-09

## Environment

- Unity Editor `6000.6.7f1`, `N:\Unity\6000.6.7f1\Editor\Unity.exe`
- Unity project: `T:\Space\Projects\Games\NoteScriptGame`
- Runtime: C#/.NET within Unity, **no Node.js / NoteScript Control API**
- Public source: https://github.com/funggier/NoteScriptGame (branch `main`)

## Completed checks

| Check | Observation |
| --- | --- |
| Scene generation | **PASS** — `NSG-SETUP PASS`, `Assets/Scenes/NoteScriptGame.unity` included in player build |
| Editor import and chart smoke | **PASS** — 21 song files, 14,700 source events, 31,880 chart notes across Easy/Normal/Hard |
| Determinism | **PASS** — regenerated charts have equal note count, lane and onset |
| Same-lane collision | **PASS** — no two notes share lane at identical chart time |
| Pitch parser | **PASS** — C4 maps to 60, F#4 to 66 |
| Invalid notation | **PASS** — malformed token rejected |
| Windows player compilation/build | **PASS** — `NSG-BUILD PASS`, Windows x64 executable generated; rebuilt after piano layout polish |
| Windows process startup | **PARTIAL** — launched and initialized Unity/graphics/input, runtime log showed no managed exceptions; visible UI, keyboard input, timing and sound output were **not** interactively qualified |
| GitHub source sanity | **PASS** — run https://github.com/funggier/NoteScriptGame/actions/runs/42579356325 on initial commit; static structure check only |
| GitHub public | **PASS** — public repo created and first push verified |

## Local logs outside Git repository

- `T:\Space\Projects\Games\NoteScriptGame-setup.log`
- `T:\Space\Projects\Games\NoteScriptGame-smoke.log`
- `T:\Space\Projects\Games\NoteScriptGame-build-polished.log`
- `T:\Space\Projects\Games\NoteScriptGame-runtime.log`

## Reproduce

Scene setup (only if scene must be reconstructed):

```powershell
& 'N:\Unity\6000.6.7f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'T:\Space\Projects\Games\NoteScriptGame' -executeMethod NoteScriptGame.Editor.ProjectSetup.CreateBootstrapScene -logFile 'T:\Space\Projects\Games\NoteScriptGame-setup.log'
```

Import and chart tests:

```powershell
& 'N:\Unity\6000.6.7f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'T:\Space\Projects\Games\NoteScriptGame' -executeMethod NoteScriptGame.Editor.ProjectSetup.RunSmokeChecks -logFile 'T:\Space\Projects\Games\NoteScriptGame-smoke.log'
```

Windows build:

```powershell
& 'N:\Unity\6000.6.7f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'T:\Space\Projects\Games\NoteScriptGame' -executeMethod NoteScriptGame.Editor.ProjectSetup.BuildWindows -logFile 'T:\Space\Projects\Games\NoteScriptGame-build.log'
```

## Explicitly not validated yet

- Actual audio latency / MIDI controller input / sustained note release semantics
- Pointer and computer-keyboard judgement equivalence
- Audio compatibility with original third-party SFZ/SF2/SF3 sample packs
- Native file dialogs, .nsg versioned serialization and full piano-roll/mixer
- Visual UI inspection in an interactive desktop session

The independent sample synthesizer produces approximate timbres, not bit-identical NoteScript audio.

## Native-resolution UI Toolkit verification (2026-11-09)

New user requirement: 23–27-inch screens, Full HD/QHD, preserved image proportions, smaller sharper UI.

Implementation:
- Removed the fixed 1600x900 IMGUI renderer and its full-window GUI.matrix scaling.
- Added responsive runtime UI Toolkit layouts, PanelSettings with ConstantPixelSize, native text rasterization, a 16:9 fitted rhythm field, saved UI scale presets and native/windowed display modes.
- Added correct A/S/D/J/K/L lane labels and a judgement line positioned at 80% of the stage height, matching chart-note arrival.
- Separated song/game logic (NoteScriptGameApp.cs) from native rendering and keyboard UI (NoteScriptGameNativeUI.cs).
- Wired UIDocument to Assets/Resources/NoteScriptGamePanel.asset in Main Scene.

Observed automated checks, all PASS:
- NSG-HIDPI PASS — panel, native layout, editor display setup.
- NSG-SMOKE PASS — 21 .md songs, 14,700 canonical events, 31,880 generated chart notes across 3 difficulty levels.
- NSG-BUILD PASS — Windows x64 player built in isolated Unity 6000.6.7f1 qualification project.
- NSG-NATIVE-UI-QUALIFICATION PASS — verified PanelSettings, UIDocument scene reference, no old matrix-scaling path, chart regression and build.
- NSG-NATIVE-UI INITIALIZING — 1904x998 headless window client, 100% scale, theme True.
- NSG-NATIVE-UI READY: PASS — runtime VisualElement hierarchy created; no managed exceptions found in smoke log.
- Built player copied into the real project Build/Windows folder; UserSongs preserved. Local separate High-DPI preview ZIP created without user files.

Remaining qualification: **interactive rendered UI inspection on 1920x1080 and 2560x1440 monitors**, visual font sharpness, actual pointer+keyboard gameplay and audio output must still be checked manually. The headless runtime check cannot establish pixel-perfect visible output.

Source details: docs/UI-DISPLAY.md.
## Piano Roll graphical editor verification (2026-11-09)

Implementation: graphical Unity UI Toolkit Piano Roll sharing canonical MusicSong/MusicTrack/MusicEvent; four-bar pages; three-octave visible pitch range; 1/16 snap; add/select/drag/move/transpose/change duration/delete; track navigation; preview and source switch; 24-step Undo/Redo; unsaved-changes guard; validated, atomic, duplicate-safe Markdown file saving.

Automated checks, PASS:
- NSG-SMOKE PASS: 21 bundled .md songs; 14,700 source events; 31,880 rhythm chart notes across Easy/Normal/Hard.
- NSG-PIANO-ROLL PASS: all 21 songs survive selective one-bar Markdown rewrite, all 14,700 events preserve semantic timing/pitches/velocity; cross-bar move succeeds; other track and prose retained; chord creation accepted; overlapping note rejected; game chart generated after an edit.
- NSG-HIDPI PASS: project-owned ThemeStyleSheet imported in Unity Editor, linked to runtime PanelSettings and scene; native pixels used, 16:9 playfield.
- NSG-BUILD PASS and NSG-PIANO-ROLL-WINDOWS PASS: Unity 6000.6.7f1 built Windows x64 player.
- NSG-NATIVE-UI INITIALIZING: headless standalone 1904x998 window, 100% scale, theme True.
- NSG-NATIVE-UI READY PASS.
- NSG-PIANO-UI PASS: standalone runtime created graphical Piano Roll for Starter Pluck track with 32 events; no managed exceptions found.
- The fully built player was copied into the real project Builds/Windows; existing UserSongs files were not touched.

Log evidence lives outside source control at Builds/Verification/PianoRoll-20261109.
Critical limitation: headless smoke proves initialization, not the rendered visual quality, hands-on mouse dragging/keyboard operation or audible sample output on real hardware. Separate interactive acceptance is still required.