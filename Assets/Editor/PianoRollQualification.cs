using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NoteScriptGame.Music;
using UnityEditor;
using UnityEngine;

namespace NoteScriptGame.Editor
{
    /// <summary>Regression verification for in-memory Piano Roll and non-destructive Markdown changes.</summary>
    public static class PianoRollQualification
    {
        [MenuItem("NoteScriptGame/Verify Piano Roll Markdown Roundtrip")]
        public static void Run()
        {
            ProjectSetup.RunSmokeChecks();
            string songsDirectory = Path.Combine(Application.streamingAssetsPath, "Songs");
            string[] files = Directory.GetFiles(songsDirectory, "*.md");
            int checkedSongs = 0, checkedEvents = 0;
            foreach (string path in files)
            {
                string original = File.ReadAllText(path);
                var parsed = NoteScriptMarkdownParser.Parse(original);
                if (!parsed.Success) throw new Exception("Original importer failed for " + path);
                MusicSong song = MarkdownPianoRollEditor.Clone(parsed.Song);
                int track = song.Events[0].TrackIndex;
                int bar = MarkdownPianoRollEditor.BarNumber(song, song.Events[0].StartQuarter);
                string output, error;
                if (!MarkdownPianoRollEditor.TryBuildMarkdown(original, song, track,
                    new[] { bar }, out output, out error))
                    throw new Exception("Piano Roll serializer failed on " + path + ": " + error);
                MusicImportResult roundtrip = NoteScriptMarkdownParser.Parse(output);
                if (!roundtrip.Success || !SameEvents(parsed.Song, roundtrip.Song))
                    throw new Exception("Piano Roll changed song event semantics: " + path);
                checkedSongs++;
                checkedEvents += parsed.Song.Events.Count;
            }

            string fence = new string((char)96, 3);
            string sample = "# Test\n" +
                "This prose must remain unchanged.\n\n" +
                fence + "notescript\n" +
                "tempo: 120\ntime: 4/4\nkey: C major\n" +
                "track Lead\nsound: piano\nvelocity: 90\n" +
                "bar 1: C4 q | R h | G4 q\n" +
                "bar 2: R w\n" +
                "track Harmony\nsound: warm-pad\nvelocity: 75\n" +
                "bar 1: E3 w\n" + fence + "\n";
            var songResult = NoteScriptMarkdownParser.Parse(sample);
            if (!songResult.Success) throw new Exception("Piano Roll fixture does not parse.");
            MusicSong edited = MarkdownPianoRollEditor.Clone(songResult.Song);
            MusicEvent selected = edited.Events.First(e => e.TrackIndex == 0 && e.MidiNote == 67);
            selected.MidiNote = 74; // D5
            selected.StartQuarter = 4.25; // Move from bar 1 to bar 2
            MarkdownPianoRollEditor.RefreshTiming(edited);
            string modified, reason;
            if (!MarkdownPianoRollEditor.TryBuildMarkdown(sample, edited, 0, new[] {1, 2},
                out modified, out reason))
                throw new Exception("Moving a note between bars failed: " + reason);
            if (!modified.Contains("This prose must remain unchanged.") ||
                !modified.Contains("track Harmony\nsound: warm-pad\nvelocity: 75\nbar 1: E3 w"))
                throw new Exception("Music Studio changed other track/prose while editing Lead.");
            var moved = NoteScriptMarkdownParser.Parse(modified);
            if (!moved.Success || !SameEvents(edited, moved.Song))
                throw new Exception("Move across bars did not survive Markdown roundtrip.");
            var validChart = RhythmChartGenerator.Generate(moved.Song, RhythmDifficulty.Hard);
            if (validChart.Notes.Count == 0) throw new Exception("Edited song is not playable.");

            // Adding a chord at an existing onset must preserve the existing duration.
            MusicSong chord = MarkdownPianoRollEditor.Clone(songResult.Song);
            chord.Events.Add(new MusicEvent
            {
                TrackIndex = 0, MidiNote = 64, Velocity = 90,
                StartQuarter = 0, DurationQuarter = 1
            });
            string barOutput;
            if (!MarkdownPianoRollEditor.TryRenderBar(chord, 0, 1, out barOutput, out reason) ||
                !barOutput.Contains("[C4 E4] q"))
                throw new Exception("Chord insertion was not correctly serialized.");

            // Overlapping onsets must be rejected and never silently damaged.
            chord.Events.Add(new MusicEvent
            {
                TrackIndex = 0, MidiNote = 70, Velocity = 90,
                StartQuarter = .5, DurationQuarter = 1
            });
            if (MarkdownPianoRollEditor.ValidateBar(chord, 0, 1, out reason))
                throw new Exception("Overlapping note was incorrectly accepted.");

            if (MarkdownPianoRollEditor.ToPitch(60) != "C4" ||
                MarkdownPianoRollEditor.ToPitch(66) != "F#4")
                throw new Exception("MIDI pitch notation is incorrect.");
            Debug.Log("NSG-PIANO-ROLL PASS: " + checkedSongs +
                " imported songs roundtrip, " + checkedEvents +
                " events preserved, surgical Markdown edit, moved note, chords, overlap rejection and generated gameplay chart.");
        }

        private static bool SameEvents(MusicSong a, MusicSong b)
        {
            if (a == null || b == null || a.Tracks.Count != b.Tracks.Count ||
                a.Events.Count != b.Events.Count) return false;
            var ea = a.Events.OrderBy(e => e.TrackIndex).ThenBy(e => e.StartQuarter)
                .ThenBy(e => e.MidiNote).ThenBy(e => e.Trigger)
                .ThenBy(e => e.DurationQuarter).ToArray();
            var eb = b.Events.OrderBy(e => e.TrackIndex).ThenBy(e => e.StartQuarter)
                .ThenBy(e => e.MidiNote).ThenBy(e => e.Trigger)
                .ThenBy(e => e.DurationQuarter).ToArray();
            for (int i = 0; i < ea.Length; i++)
            {
                if (ea[i].TrackIndex != eb[i].TrackIndex ||
                    ea[i].MidiNote != eb[i].MidiNote ||
                    ea[i].Trigger != eb[i].Trigger ||
                    ea[i].Velocity != eb[i].Velocity ||
                    Math.Abs(ea[i].StartQuarter - eb[i].StartQuarter) > 1e-6 ||
                    Math.Abs(ea[i].DurationQuarter - eb[i].DurationQuarter) > 1e-6)
                    return false;
            }
            return true;
        }

        [MenuItem("NoteScriptGame/Verify Piano Roll and Build Windows")]
        public static void RunAndBuild()
        {
            HighDpiSetup.Configure();
            var panel = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(
                "Assets/Resources/NoteScriptGamePanel.asset");
            if (panel == null || panel.themeStyleSheet == null ||
                !AssetDatabase.GetAssetPath(panel.themeStyleSheet).StartsWith("Assets/Resources/"))
                throw new Exception("Piano Roll build must include project-owned runtime UI theme.");
            Run();
            ProjectSetup.BuildWindows();
            Debug.Log("NSG-PIANO-ROLL-WINDOWS PASS: roundtrip tests and Windows player build completed.");
        }
    }
}
