# NoteScriptGame — standalone architecture

## Product contract

A hybrid music game (Rhythm + Free Play) and native music studio. Gameplay is first, studio evolves without replacing the engine.

**Strict independence:** no runtime call, dynamic linking, source-code inclusion, or subprocess execution of NoteScript, Node.js, its Control API, or its Studio web app. Imported NoteScript `.md` files are read as *data* only.

```text
External / Bundled .md Songs
             |
             v
NoteScriptMarkdownParser (C#)
             |
             v
MusicSong / MusicTrack / MusicEvent (canonical)
          /                   \
         v                     v
RhythmChartGenerator     MusicPlaybackEngine
         |                     |
         v                     v
BeatLane6 Gameplay       Audio DSP Scheduler
         |                     |
          \                   /
           v                 v
           Unity NoteScriptGameApp
          Library / Listen / Play / Studio
```

The parser reads NoteScript v0.1 semantics from independent code; changes are tested against static input files rather than relying on another process. Unknown tokens fail with diagnostics.

## Timebase

`MusicEvent.StartQuarter` and `DurationQuarter` preserve the musical timebase. Seconds are computed via constant BPM; variable tempo maps are out of scope in v0.1. All playback uses `AudioSettings.dspTime` and a small lookahead rather than assuming frame delta is sample-accurate. Gameplay judges against the same transport position.

## Data ownership

- `Assets/StreamingAssets/Songs/`: built-in offline test/library songs.
- `UserSongs/`: per-project / adjacent-exe user songs; Git-ignored. Direct text editor saves new/edited `.md` files here.
- `Assets/Scripts/`: source only.
- `Builds/`: generated Windows executable, excluded from Git.
- Original NoteScript source is not cloned into the Unity project and is not a runtime dependency.

## Audio milestone boundary

The v0.1 fallback audio source is a lightweight local oscillator/percussion synth, not NoteScript sampled audio. Name-based timbre mapping is approximate; SFZ/SF2/SF3, user sample packs, exact instrument sound compatibility and output-device control are future work.

## Chart milestone boundary

Six columns A/S/D/J/K/L, deterministic pitch/trigger mapping and Easy/Normal/Hard density. Note onset, duration and track index are preserved. Difficulty selection does not edit the song. Tap/Hold and chord combinations; score, combo and weighted accuracy; pause/resume and exit are supplied in the UI.

## Next engineering tasks

- [x] Replace fixed-size IMGUI front end with native-resolution UI Toolkit, 16:9 Rhythm playfield, 23–27-inch reference displays, saved UI scale and fullscreen/windowed settings. See UI-DISPLAY.md.
- [ ] Visually verify UI Toolkit on physical 1080p/1440p displays and refine small-window behavior.
- [ ] Add actual folder-based audio sample packs and layered/velocity samples; manage voice lifetime and cache eviction.
- [ ] Add genuine editable Piano Roll, recorder, multi-track mixer and `.nsg` with explicit versioned schema.
- [ ] Add file picker/folder drop and batch-import UI with per-song diagnostic report.
- [ ] Add dedicated EditMode NUnit suites, PlayMode input/audio tests and portable Windows UI qualification.
- [ ] Handle long notes with continuous, sustained voices; score Hold release for pointer and keyboard consistently.
- [ ] Add calibration, best score persistence, stage customization/3D visuals and MIDI controller support.
