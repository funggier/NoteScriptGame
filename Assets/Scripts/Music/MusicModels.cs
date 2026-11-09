using System;
using System.Collections.Generic;
using System.Linq;

namespace NoteScriptGame.Music
{
    public sealed class MusicSong
    {
        public string Title = "Untitled";
        public int Tempo = 120;
        public int BeatsPerBar = 4;
        public int BeatUnit = 4;
        public string Key = "C major";
        public int Ppq = 480;
        public string OriginalMarkdown = "";
        public readonly List<MusicTrack> Tracks = new List<MusicTrack>();
        public readonly List<MusicEvent> Events = new List<MusicEvent>();
        public double DurationSeconds;

        public IEnumerable<MusicEvent> OrderedEvents()
        {
            return Events.OrderBy(e => e.TimeSeconds).ThenBy(e => e.TrackIndex);
        }
    }

    public sealed class MusicTrack
    {
        public string Name = "Track";
        public string Sound = "piano";
        public bool IsInstrument;
        public float Volume = 0.65f;
        public float Pan;
        public int Velocity = 90;
    }

    public sealed class MusicEvent
    {
        public int TrackIndex;
        public int MidiNote = -1;
        public string Trigger = "";
        public double StartQuarter;
        public double DurationQuarter;
        public double TimeSeconds;
        public double DurationSeconds;
        public int Velocity;
        public bool IsTrigger { get { return MidiNote < 0; } }
    }

    public sealed class MusicImportResult
    {
        public MusicSong Song;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool Success { get { return Song != null && Errors.Count == 0; } }
        public string Message
        {
            get
            {
                if (Errors.Count > 0) return string.Join("\n", Errors.ToArray());
                return Warnings.Count > 0 ? string.Join("\n", Warnings.ToArray()) : "Imported successfully";
            }
        }
    }

    public enum RhythmDifficulty { Easy, Normal, Hard }

    public sealed class RhythmNote
    {
        public int Id;
        public int Lane;
        public double TimeSeconds;
        public double DurationSeconds;
        public bool IsHold;
        public MusicEvent Source;
        public bool Judged;
        public bool Hit;
        public bool Holding;
    }

    public sealed class RhythmChart
    {
        public readonly List<RhythmNote> Notes = new List<RhythmNote>();
        public double DurationSeconds;
        public int LaneCount = 6;
    }
}
