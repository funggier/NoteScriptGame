# NoteScriptGame

**Standalone hybrid rhythm game + music studio built from scratch in Unity/C#.**
The source repo is independent of NoteScript: no Node.js server, web UI, remote API, or original NoteScript runtime is used.

## Project

- Unity: **6000.6.7f1**
- Main scene: `Assets/Scenes/NoteScriptGame.unity`
- Windows project location: `T:\Space\Projects\Games\NoteScriptGame`
- License: MIT (code). Check any third-party instrument/sample licenses independently.
- Status: **v0.1 playable foundation**. Complete DAW-style piano roll/mixer and .nsg format are later phases.

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

The UI keeps a fixed 1600:900 virtual layout scaled uniformly to the window and is mouse-operable.

## Markdown files

Import an existing NoteScript `.md` song using the path field on the Library page, or copy it to `UserSongs/` beside the Unity project / built executable and press **REFRESH FILES**. The original file is never changed by import. The built-in original song collection is distributed under `Assets/StreamingAssets/Songs/`.

The importer is a newly-written **C# v0.1 notation parser** supporting:

- `# Title`, fenced `notescript` block
- `tempo`, `time`, `key`, `ppq`
- `track`, `sound`, `instrument`, `volume`, `pan`, `velocity`
- `bar N:` with notes, chords, rests and `@soundpad` triggers
- `w / h / q / e / s` durations, dotted durations, event velocity overrides

Unsupported syntax produces an error rather than silently dropping song events. Library, Rhythm and the basic text Studio all use the **same in-memory C# song structure**.

In **MUSIC STUDIO** the first milestone provides a direct Markdown text editor, validation, new-song template and save to local `.md`. The full graphical piano roll, DAW mixer, recording, add-on SFZ/SF2/SF3 sound engines and native `.nsg` composition format are on the roadmap.

**Audio note:** v0.1 contains its own procedural synth implementation. Imported sound/instrument names select approximate timbres, **not the original sampled NoteScript instrument**, so the rendering is not acoustically identical. No runtime downloads or external services are required.

## Smoke verification

The Unity menu **NoteScriptGame → Run Markdown and Chart Smoke Tests** validates all bundled .md songs, deterministic Easy/Normal/Hard chart generation, note lanes, no same-onset conflicts, pitch conversion and rejection of malformed input.

To rebuild the scene: **NoteScriptGame → Create Bootstrap Scene**.
To build a Windows executable: **NoteScriptGame → Build Windows Standalone**.

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
