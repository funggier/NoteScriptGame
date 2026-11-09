# NoteScriptGame

**Standalone hybrid rhythm game + music studio built from scratch in Unity/C#.**
The source repo is independent of NoteScript: no Node.js server, web UI, remote API, or original NoteScript runtime is used.

## Project

- Unity: **6000.6.7f1**
- Main scene: `Assets/Scenes/NoteScriptGame.unity`
- Windows project location: `T:\Space\Projects\Games\NoteScriptGame`
- License: MIT (code). Check any third-party instrument/sample licenses independently.
- Status: playable foundation plus first graphical Piano Roll. DAW mixer, live recording, sample adapters and native .nsg projects remain planned.

## First run

1. Open this folder with **Unity 6000.6.7f1** (the exact editor version is recorded in `ProjectSettings/ProjectVersion.txt`).
2. Open `Assets/Scenes/NoteScriptGame.unity`.
3. Press **Play**; choose a song from the bundled library.
4. **LISTEN** plays the local synthesized arrangement. **PLAY RHYTHM** starts the six-lane game.

### Controls

| Context | Key |
| --- | --- |
| Rhythm, left hand | A S D |
| Rhythm, right hand | J K L |
| Rhythm or listener | Space: pause/resume |
| Rhythm/listener/free play | Esc: return to library |
| Free-play piano | Z S X D C V G B H N J M , |

The UI uses native-resolution Unity UI Toolkit instead of scaling a 1600x900 image. The Rhythm playfield keeps its own 16:9 aspect ratio.

## Display resolution and UI size

Reference displays are 23–27-inch monitors at Full HD 1920×1080 and QHD 2560×1440. The Windows player uses native fullscreen by default. Rhythm maintains a 16:9 playfield without stretching it.

Open SETTINGS on the main navigation bar to choose UI SCALE 85%, 100%, 110% or 125%, or switch between borderless native fullscreen and windowed 16:9. The selected UI scale is stored in PlayerPrefs; the first-run value is 110% at viewport heights of 1200px or more, 100% otherwise.

The runtime panel asset is Assets/Resources/NoteScriptGamePanel.asset. In the Unity Editor, use NoteScriptGame > Configure Native-Resolution UI to repair scene/panel wiring. NoteScriptGame > Verify and Build Native UI runs setup, Markdown/chart tests and the Windows build.

See docs/UI-DISPLAY.md for technical details and verification limits.
## Markdown files

Import an existing NoteScript `.md` song using the path field on the Library page, or copy it to `UserSongs/` beside the Unity project / built executable and press **REFRESH FILES**. The original file is never changed by import. The built-in original song collection is distributed under `Assets/StreamingAssets/Songs/`.

The importer is a newly-written **C# v0.1 notation parser** supporting:

- `# Title`, fenced `notescript` block
- `tempo`, `time`, `key`, `ppq`
- `track`, `sound`, `instrument`, `volume`, `pan`, `velocity`
- `bar N:` with notes, chords, rests and `@soundpad` triggers
- `w / h / q / e / s` durations, dotted durations, event velocity overrides

Unsupported syntax produces an error rather than silently dropping song events. Library, Rhythm and the basic text Studio all use the **same in-memory C# song structure**.

In MUSIC STUDIO, the default view is a graphical Piano Roll: add, drag, transpose, resize and delete pitched notes in a 1/16-note grid; switch track/bar/octave, undo/redo, preview and save to .md. The Markdown source editor is still available. Saving modifies only changed bar lines and preserves other tracks and original prose. See docs/PIANO-ROLL.md.

**Audio note:** v0.1 contains its own procedural synth implementation. Imported sound/instrument names select approximate timbres, **not the original sampled NoteScript instrument**, so the rendering is not acoustically identical. No runtime downloads or external services are required.

## Smoke verification

The Unity menu **NoteScriptGame → Run Markdown and Chart Smoke Tests** validates all bundled .md songs, deterministic Easy/Normal/Hard chart generation, note lanes, no same-onset conflicts, pitch conversion and rejection of malformed input.

To rebuild the scene: **NoteScriptGame → Create Bootstrap Scene**.
To build Windows: use NoteScriptGame > Build Windows Standalone. For Piano Roll tests use NoteScriptGame > Verify Piano Roll Markdown Roundtrip or Verify Piano Roll and Build Windows.

## Architecture

- `Assets/Scripts/Music/` — independent parser, canonical song models, chart generation
- `Assets/Scripts/Audio/` — standalone local synthesis / Audio DSP scheduling
- `Assets/Scripts/Game/` — library UI, six-lane gameplay, listening, free play, basic text studio
- `Assets/Editor/` — scene generation, smoke verification, Windows build
- `Assets/StreamingAssets/Songs/` — sample song data; no program dependencies

## Roadmap

1. **Playable baseline** (this milestone): Markdown import, built-in songs, listen/transport, six-lane rhythm game, scoring, free-play piano, basic text editor.
2. Sample-based audio engine and plugin-like sound packs. Real-time SFZ/optional SoundFont adapters, MIDI hardware, latency calibration, voice lifetime/hold improvements.
3. Piano-roll and region-based composer, track cards/mixer, record-and-edit loop, native `.nsg` plus Markdown import/export.
4. 3D responsive stage, visual effects, user profiles, saved scores and packaged releases.

## Song attribution

Bundled example `.md` files are original NoteScript repository songs from [funggier/NoteScript](https://github.com/funggier/NoteScript), licensed MIT. They are copied as **static input data** for compatibility verification and offline playing, not linked to the old runtime.
