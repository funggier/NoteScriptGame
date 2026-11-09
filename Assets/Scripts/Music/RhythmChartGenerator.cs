using System;
using System.Collections.Generic;
using System.Linq;

namespace NoteScriptGame.Music
{
    /// <summary>Deterministic six-lane chart generation; song data never changes.</summary>
    public static class RhythmChartGenerator
    {
        public static RhythmChart Generate(MusicSong song, RhythmDifficulty difficulty)
        {
            if (song == null) throw new ArgumentNullException("song");
            var chart = new RhythmChart { DurationSeconds = song.DurationSeconds };
            var notes = song.Events.Where(e => !e.IsTrigger).Select(e => e.MidiNote).ToArray();
            int min = notes.Length > 0 ? notes.Min() : 60;
            int max = notes.Length > 0 ? notes.Max() : 72;
            double lastEasyQuarter = -100;
            var grouped = song.Events.GroupBy(e => Math.Round(e.StartQuarter, 6)).OrderBy(g => g.Key);
            int id = 0;
            foreach (var group in grouped)
            {
                var pitched = group.Where(e => !e.IsTrigger).OrderBy(e => e.MidiNote).ThenBy(e => e.TrackIndex).ToList();
                var drums = group.Where(e => e.IsTrigger).OrderBy(e => e.TrackIndex).ThenBy(e => e.Trigger).ToList();
                var chosen = new List<MusicEvent>();
                if (difficulty == RhythmDifficulty.Easy)
                {
                    if (group.Key - lastEasyQuarter < 0.5) continue;
                    if (pitched.Count > 0)
                    {
                        chosen.Add(pitched[0]);
                        if (pitched.Count > 1) chosen.Add(pitched[pitched.Count - 1]);
                    }
                    else if (drums.Count > 0) chosen.Add(drums[0]);
                    if (chosen.Count > 0) lastEasyQuarter = group.Key;
                }
                else if (difficulty == RhythmDifficulty.Normal)
                {
                    chosen.AddRange(pitched.Take(3));
                    if (chosen.Count < 3) chosen.AddRange(drums.Take(3 - chosen.Count));
                }
                else
                {
                    chosen.AddRange(pitched);
                    chosen.AddRange(drums);
                    if (chosen.Count > 6) chosen.RemoveRange(6, chosen.Count - 6);
                }

                var used = new HashSet<int>();
                foreach (var e in chosen)
                {
                    int target = e.IsTrigger
                        ? (int)(StableHash(e.Trigger + ":" + e.TrackIndex + ":" + e.StartQuarter.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)) % 6u)
                        : max == min ? 2 : (int)Math.Round((e.MidiNote - min) * 5.0 / (max - min), MidpointRounding.AwayFromZero);
                    target = Math.Max(0, Math.Min(5, target));
                    int lane = PickFreeLane(target, used);
                    if (lane < 0) continue;
                    used.Add(lane);
                    chart.Notes.Add(new RhythmNote
                    {
                        Id = id++,
                        Lane = lane,
                        TimeSeconds = e.TimeSeconds,
                        DurationSeconds = e.DurationSeconds,
                        IsHold = !e.IsTrigger && e.DurationQuarter >= (difficulty == RhythmDifficulty.Easy ? 2 : 1.5),
                        Source = e
                    });
                }
            }
            chart.Notes.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
            return chart;
        }

        private static int PickFreeLane(int target, HashSet<int> used)
        {
            if (!used.Contains(target)) return target;
            for (int d = 1; d < 6; d++)
            {
                if (target - d >= 0 && !used.Contains(target - d)) return target - d;
                if (target + d < 6 && !used.Contains(target + d)) return target + d;
            }
            return -1;
        }

        private static uint StableHash(string s)
        {
            uint hash = 2166136261;
            foreach (char c in s) { hash ^= c; hash *= 16777619; }
            return hash;
        }
    }
}
