# NoteScriptGame — Piano Roll editing (first graphical milestone)

## Scope

A native Unity UI Toolkit Piano Roll works over the same canonical MusicSong/Track/Event data used by the listening mode and six-lane rhythm game. It is an editor for existing NoteScript-compatible Markdown, **not** a dependency on the original NoteScript application or its Node.js engine.

## Controls

Open MUSIC STUDIO, which now starts in PIANO ROLL mode. You can switch to MARKDOWN SOURCE at any time.

- Click an empty grid cell to insert a MIDI note (snap to 1/16 note).
- Click a note to select it. Drag it to another grid cell to move pitch and onset.
- Choose insertion duration 1/16, 1/8, 1/4, 1/2 or whole note. If insufficient room remains before the bar boundary, insert a one-sixteenth note instead.
- Pitch +1 / Pitch -1 change the selected note by a semitone. Shift earlier/later by one sixteenth. Shorter/Longer cycles through supported durations. DELETE NOTE removes the selected note.
- PREVIOUS/NEXT TRACK switches the active track, preserving the edited track in the in-memory Markdown first.
- BARS navigates 4-bar pages. OCTAVE UP/DOWN pans the visible three-octave pitch range.
- UNDO / REDO, including Ctrl+Z and Ctrl+Y in graphical mode, retain up to 24 steps per editing segment.
- PREVIEW SONG plays the edited score with the standalone C# audio synthesizer; STOP ends it.
- SAVE .MD persists the edited Markdown to the user song directory. No file is changed until Save.

## How Markdown preservation works

The selected Markdown file is parsed into the canonical MusicSong. Edits affect in-memory MusicEvent objects. On Save / switching to source / track switching, only the changed bar statements for the active track are regenerated. The serializer reuses original text for all other bars, tracks, music metadata and non-music Markdown prose. It rejects unrepresentable edits instead of silently altering events.

The original bundled examples in Assets/StreamingAssets/Songs are never edited; saved user versions are written under UserSongs. Saving a file whose name is already used by another user song automatically creates a unique name rather than replacing that song. Updating the same user song writes a validated temporary file and uses an atomic replacement where supported.

If you leave Music Studio with unsaved changes, a dialog offers Keep Editing, Save and Continue, or Discard Changes. Changes are in memory until saved to disk.

## Current notation limitations

The NoteScript v0.1 Markdown grammar uses sequential events within each bar. Overlapping onsets of different lengths cannot be represented directly. Chords require matching onset, duration and velocity. Notes crossing a bar boundary and unsupported note lengths are rejected. The editor supports pitched MIDI notes only in the grid; existing SoundPad triggers remain in the song and in the original Markdown, though a graphical percussion roll is future work.

This initial implementation edits notes but does not yet provide: drag-to-resize, tempo-map automation, a graphical track mixer, live MIDI recording, SFZ/SoundFont sample rendering, audio latency calibration or a native versioned .nsg project format.

## Source and qualification

- Assets/Scripts/Music/MarkdownPianoRollEditor.cs — per-bar serializer, semantic validation, clone and duration normalization
- Assets/Scripts/Game/NoteScriptGamePianoRoll.cs — Native UI Toolkit grid, note editing, undo/redo, previews, navigation
- Assets/Scripts/Game/NoteScriptGameApp.cs — song library, transport, saving and gameplay
- Assets/Editor/PianoRollQualification.cs — parser, serializer, chart and player build regressions

Run Unity menu NoteScriptGame > Verify Piano Roll Markdown Roundtrip. It validates all bundled songs against the existing parser and a synthetic two-track example with a moved note, chord insertion, invalid overlap and chart generation. The combined menu action Verify Piano Roll and Build Windows also builds a standalone Windows player.

The build and headless runtime smoke cannot prove visible layout quality or physical keyboard/audio operation. Those remain separate interactive qualification items.
