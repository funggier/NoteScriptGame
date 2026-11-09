using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NoteScriptGame.Music
{
    /// <summary>
    /// Edits individual bar statements in the original Markdown rather than rewriting other tracks.
    /// Rejects notes that cannot be represented by the current NoteScript v0.1 rhythm grammar.
    /// </summary>
    public static class MarkdownPianoRollEditor
    {
        private static readonly Regex BarLine = new Regex(@"^bar\s+(\d+)\s*:", RegexOptions.IgnoreCase);
        private static readonly Regex TrackLine = new Regex(@"^track\s+", RegexOptions.IgnoreCase);
        private const double Epsilon = 0.000001;

        private struct DurationToken
        {
            public double Quarter;
            public string Text;
            public DurationToken(double quarter, string text) { Quarter = quarter; Text = text; }
        }

        private static readonly DurationToken[] Durations =
        {
            new DurationToken(6, "w."), new DurationToken(4, "w"),
            new DurationToken(3, "h."), new DurationToken(2, "h"),
            new DurationToken(1.5, "q."), new DurationToken(1, "q"),
            new DurationToken(.75, "e."), new DurationToken(.5, "e"),
            new DurationToken(.375, "s."), new DurationToken(.25, "s")
        };

        public static double BarQuarterLength(MusicSong song)
        {
            return song.BeatsPerBar * (4.0 / song.BeatUnit);
        }

        public static int BarNumber(MusicSong song, double quarter)
        {
            return Math.Max(1, (int)Math.Floor((quarter + Epsilon) / BarQuarterLength(song)) + 1);
        }

        public static bool ValidateBar(MusicSong song, int trackIndex, int bar, out string error)
        {
            string ignored;
            return TryRenderBar(song, trackIndex, bar, out ignored, out error);
        }

        public static bool TryBuildMarkdown(string original, MusicSong changed, int trackIndex,
            IEnumerable<int> modifiedBars, out string updated, out string error)
        {
            updated = null;
            error = null;
            if (changed == null || trackIndex < 0 || trackIndex >= changed.Tracks.Count)
            {
                error = "Invalid song or track index.";
                return false;
            }
            var bars = new SortedSet<int>(modifiedBars ?? Enumerable.Empty<int>());
            if (bars.Count == 0)
            {
                updated = original;
                return true;
            }
            var replacements = new Dictionary<int, string>();
            foreach (int bar in bars)
            {
                string line;
                if (!TryRenderBar(changed, trackIndex, bar, out line, out error))
                {
                    error = "Bar " + bar + ": " + error;
                    return false;
                }
                replacements.Add(bar, line);
            }

            string newline = original.Contains("\r\n") ? "\r\n" : "\n";
            bool terminalNewline = original.EndsWith("\n", StringComparison.Ordinal);
            var lines = original.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            if (terminalNewline && lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                lines.RemoveAt(lines.Count - 1);

            string fence = new string((char)96, 3);
            int opened = -1, closed = -1, currentTrack = -1;
            int targetStart = -1, targetEnd = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                if (opened < 0)
                {
                    if (line.StartsWith(fence + "notescript", StringComparison.OrdinalIgnoreCase)) opened = i;
                    continue;
                }
                if (line.StartsWith(fence, StringComparison.Ordinal))
                {
                    closed = i;
                    if (currentTrack == trackIndex && targetEnd < 0) targetEnd = i;
                    break;
                }
                if (TrackLine.IsMatch(line))
                {
                    if (currentTrack == trackIndex && targetEnd < 0) targetEnd = i;
                    currentTrack++;
                    if (currentTrack == trackIndex) targetStart = i;
                }
            }
            if (opened < 0 || closed < 0 || targetStart < 0 || targetEnd < 0)
            {
                error = "Cannot locate selected track in the original Markdown file.";
                return false;
            }

            var existing = new Dictionary<int, int>();
            int insertion = targetEnd;
            for (int i = targetStart + 1; i < targetEnd; i++)
            {
                var match = BarLine.Match(lines[i].Trim());
                if (!match.Success) continue;
                int number;
                if (!int.TryParse(match.Groups[1].Value, out number)) continue;
                if (existing.ContainsKey(number)) { error = "Duplicate bar statement " + number; return false; }
                existing[number] = i;
                insertion = i + 1;
            }

            // Change only explicitly modified bar statements, preserve all other tracks
            // and their original formatting/metadata/comments.
            foreach (var pair in existing)
                if (replacements.ContainsKey(pair.Key)) lines[pair.Value] = replacements[pair.Key];

            var missing = replacements.Keys.Where(b => !existing.ContainsKey(b)).ToList();
            foreach (int missingBar in missing)
            {
                lines.Insert(insertion++, replacements[missingBar]);
            }

            updated = string.Join(newline, lines) + (terminalNewline ? newline : "");
            var parsed = NoteScriptMarkdownParser.Parse(updated);
            if (!parsed.Success)
            {
                error = "Generated Markdown failed validation: " + parsed.Message;
                updated = null;
                return false;
            }
            if (parsed.Song.Tracks.Count != changed.Tracks.Count)
            {
                error = "Track count changed unexpectedly.";
                updated = null;
                return false;
            }
            return true;
        }

        public static bool TryRenderBar(MusicSong song, int track, int bar, out string rendered, out string error)
        {
            rendered = null;
            error = null;
            if (bar < 1 || bar > 10000 || song == null || track < 0 || track >= song.Tracks.Count)
            {
                error = "Invalid bar or track.";
                return false;
            }
            double length = BarQuarterLength(song);
            double start = (bar - 1) * length;
            double end = start + length;
            var events = song.Events
                .Where(e => e.TrackIndex == track &&
                    e.StartQuarter >= start - Epsilon && e.StartQuarter < end - Epsilon)
                .OrderBy(e => e.StartQuarter)
                .ThenBy(e => e.MidiNote)
                .ToList();
            var result = new List<string>();
            double cursor = start;
            int at = 0;

            while (at < events.Count)
            {
                MusicEvent first = events[at];
                if (first.StartQuarter < cursor - Epsilon)
                {
                    error = "Overlapping notes: simultaneous notes must have the same duration.";
                    return false;
                }
                if (first.StartQuarter + first.DurationQuarter > end + Epsilon)
                {
                    error = "A note extends beyond the bar; split it before saving.";
                    return false;
                }
                if (first.DurationQuarter <= Epsilon)
                {
                    error = "Zero or negative note duration.";
                    return false;
                }
                if (!TryAppendRest(first.StartQuarter - cursor, result, out error)) return false;

                int next = at;
                var chord = new List<MusicEvent>();
                while (next < events.Count && Math.Abs(events[next].StartQuarter - first.StartQuarter) < Epsilon)
                {
                    var eventAtTime = events[next++];
                    if (Math.Abs(first.DurationQuarter - eventAtTime.DurationQuarter) > Epsilon ||
                        first.Velocity != eventAtTime.Velocity)
                    {
                        error = "Chord notes require matching durations and velocities.";
                        return false;
                    }
                    chord.Add(eventAtTime);
                }
                string duration;
                if (!TryDuration(first.DurationQuarter, out duration))
                {
                    error = "Unsupported duration " + first.DurationQuarter.ToString("F6", CultureInfo.InvariantCulture) +
                            "; use 1/16, 1/8, 1/4, 1/2, whole or dotted values.";
                    return false;
                }
                var symbols = new List<string>();
                foreach (var ev in chord)
                {
                    if (ev.IsTrigger)
                    {
                        if (string.IsNullOrWhiteSpace(ev.Trigger) || !Regex.IsMatch(ev.Trigger, @"^[\w-]+$"))
                        {
                            error = "Invalid percussion trigger name.";
                            return false;
                        }
                        symbols.Add("@" + ev.Trigger);
                    }
                    else
                    {
                        if (ev.MidiNote < 0 || ev.MidiNote > 127)
                        {
                            error = "MIDI pitch must be between 0 and 127.";
                            return false;
                        }
                        symbols.Add(ToPitch(ev.MidiNote));
                    }
                }
                if (symbols.Distinct().Count() != symbols.Count)
                {
                    error = "Duplicate notes at the same onset.";
                    return false;
                }
                string body = symbols.Count == 1 ? symbols[0] : "[" + string.Join(" ", symbols) + "]";
                string velocity = first.Velocity == song.Tracks[track].Velocity
                    ? "" : " @" + first.Velocity;
                result.Add(body + " " + duration + velocity);
                cursor = first.StartQuarter + first.DurationQuarter;
                at = next;
            }
            if (!TryAppendRest(end - cursor, result, out error)) return false;
            rendered = "bar " + bar + ": " + string.Join(" | ", result);
            return true;
        }

        private static bool TryAppendRest(double length, List<string> result, out string error)
        {
            error = null;
            if (length < -Epsilon) { error = "Negative rest or overlapping events."; return false; }
            double remaining = Math.Max(0, length);
            int safety = 0;
            while (remaining > Epsilon)
            {
                DurationToken best = new DurationToken(0, "");
                foreach (var duration in Durations)
                {
                    if (duration.Quarter <= remaining + Epsilon)
                    {
                        best = duration;
                        break;
                    }
                }
                if (best.Quarter <= 0 || ++safety > 100)
                {
                    error = "Rest cannot be represented at 1/16-note resolution.";
                    return false;
                }
                result.Add("R " + best.Text);
                remaining -= best.Quarter;
            }
            return true;
        }

        private static bool TryDuration(double quarter, out string symbol)
        {
            foreach (var token in Durations)
            {
                if (Math.Abs(token.Quarter - quarter) < Epsilon)
                {
                    symbol = token.Text;
                    return true;
                }
            }
            symbol = null;
            return false;
        }

        public static string ToPitch(int midi)
        {
            string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            return names[midi % 12] + (midi / 12 - 1).ToString(CultureInfo.InvariantCulture);
        }

        public static MusicSong Clone(MusicSong source)
        {
            var result = new MusicSong
            {
                Title = source.Title,
                Tempo = source.Tempo,
                BeatsPerBar = source.BeatsPerBar,
                BeatUnit = source.BeatUnit,
                Key = source.Key,
                Ppq = source.Ppq,
                OriginalMarkdown = source.OriginalMarkdown,
                DurationSeconds = source.DurationSeconds
            };
            foreach (var track in source.Tracks)
                result.Tracks.Add(new MusicTrack
                {
                    Name = track.Name, Sound = track.Sound, IsInstrument = track.IsInstrument,
                    Volume = track.Volume, Pan = track.Pan, Velocity = track.Velocity
                });
            foreach (var ev in source.Events)
                result.Events.Add(new MusicEvent
                {
                    TrackIndex = ev.TrackIndex, MidiNote = ev.MidiNote,
                    Trigger = ev.Trigger, Velocity = ev.Velocity,
                    StartQuarter = ev.StartQuarter, DurationQuarter = ev.DurationQuarter,
                    TimeSeconds = ev.TimeSeconds, DurationSeconds = ev.DurationSeconds
                });
            return result;
        }

        public static void RefreshTiming(MusicSong song)
        {
            double factor = 60.0 / song.Tempo;
            double maxEnd = 0;
            foreach (var e in song.Events)
            {
                e.TimeSeconds = e.StartQuarter * factor;
                e.DurationSeconds = e.DurationQuarter * factor;
                maxEnd = Math.Max(maxEnd, e.StartQuarter + e.DurationQuarter);
            }
            double barLength = BarQuarterLength(song);
            song.DurationSeconds = Math.Ceiling((maxEnd - Epsilon) / barLength) * barLength * factor;
        }
    }
}
