using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace NoteScriptGame.Music
{
    /// <summary>Independent Unity/C# implementation of the NoteScript v0.1 Markdown song grammar.
    /// Does not call NoteScript, Node, a web API, or any JavaScript runtime.</summary>
    public static class NoteScriptMarkdownParser
    {
        private static readonly Regex BarPattern = new Regex(@"^bar\s+(\d+)\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex EventPattern = new Regex(@"^(\[[^\]]+\]|@[\w-]+|[A-Ga-g][#b]?\d+|R)\s+([whqes])([.]?)(?:\s+@(\d{1,3}))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NotePattern = new Regex(@"^([A-Ga-g])([#b]?)(\d+)$", RegexOptions.Compiled);

        public static MusicImportResult Parse(string markdown)
        {
            var result = new MusicImportResult();
            if (string.IsNullOrWhiteSpace(markdown))
            {
                result.Errors.Add("The Markdown file is empty.");
                return result;
            }

            var song = new MusicSong { OriginalMarkdown = markdown };
            var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool inScript = false;
            bool hasFence = false;
            bool closed = false;
            MusicTrack currentTrack = null;
            var seenBars = new Dictionary<int, HashSet<int>>();
            int maximumBar = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                int lineNumber = i + 1;
                if (!inScript)
                {
                    if (line.StartsWith("# ") && song.Title == "Untitled") song.Title = line.Substring(2).Trim();
                    if (line.StartsWith("```notescript", StringComparison.OrdinalIgnoreCase))
                    {
                        inScript = true;
                        hasFence = true;
                    }
                    continue;
                }
                if (line.StartsWith("```"))
                {
                    closed = true;
                    inScript = false;
                    break;
                }
                if (line.Length == 0 || line.StartsWith("//")) continue;
                int colon = line.IndexOf(':');
                if (colon >= 0 && !line.StartsWith("bar ", StringComparison.OrdinalIgnoreCase))
                {
                    string key = line.Substring(0, colon).Trim().ToLowerInvariant();
                    string value = line.Substring(colon + 1).Trim();
                    if (key == "tempo")
                    {
                        int n;
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n < 20 || n > 400)
                            result.Errors.Add("Line " + lineNumber + ": tempo must be 20-400.");
                        else song.Tempo = n;
                    }
                    else if (key == "time")
                    {
                        string[] pair = value.Split('/');
                        int n, d;
                        if (pair.Length != 2 || !int.TryParse(pair[0], out n) || !int.TryParse(pair[1], out d) ||
                            n < 1 || n > 16 || (d != 1 && d != 2 && d != 4 && d != 8 && d != 16))
                            result.Errors.Add("Line " + lineNumber + ": invalid time signature.");
                        else { song.BeatsPerBar = n; song.BeatUnit = d; }
                    }
                    else if (key == "key") song.Key = value;
                    else if (key == "ppq")
                    {
                        int n;
                        if (!int.TryParse(value, out n) || n <= 0) result.Errors.Add("Line " + lineNumber + ": invalid PPQ.");
                        else song.Ppq = n;
                    }
                    else if (currentTrack != null && key == "sound") { currentTrack.Sound = value; currentTrack.IsInstrument = false; }
                    else if (currentTrack != null && key == "instrument") { currentTrack.Sound = value; currentTrack.IsInstrument = true; }
                    else if (currentTrack != null && key == "volume") ReadFloat(value, lineNumber, result, x => currentTrack.Volume = x, 0, 1);
                    else if (currentTrack != null && key == "pan") ReadFloat(value, lineNumber, result, x => currentTrack.Pan = x, -1, 1);
                    else if (currentTrack != null && key == "velocity")
                    {
                        int n;
                        if (!int.TryParse(value, out n) || n < 1 || n > 127)
                            result.Errors.Add("Line " + lineNumber + ": velocity must be 1-127.");
                        else currentTrack.Velocity = n;
                    }
                    else result.Errors.Add("Line " + lineNumber + ": unsupported property '" + key + "'.");
                    continue;
                }

                if (line.StartsWith("track ", StringComparison.OrdinalIgnoreCase))
                {
                    currentTrack = new MusicTrack { Name = line.Substring(6).Trim() };
                    if (currentTrack.Name.Length == 0) result.Errors.Add("Line " + lineNumber + ": track name cannot be empty.");
                    song.Tracks.Add(currentTrack);
                    seenBars.Add(song.Tracks.Count - 1, new HashSet<int>());
                    continue;
                }

                Match bar = BarPattern.Match(line);
                if (bar.Success)
                {
                    if (currentTrack == null) { result.Errors.Add("Line " + lineNumber + ": bar before any track."); continue; }
                    int barNo = int.Parse(bar.Groups[1].Value, CultureInfo.InvariantCulture);
                    int trackIndex = song.Tracks.Count - 1;
                    if (barNo < 1 || barNo > 10000 || !seenBars[trackIndex].Add(barNo))
                    {
                        result.Errors.Add("Line " + lineNumber + ": invalid or duplicate bar " + barNo + ".");
                        continue;
                    }
                    maximumBar = Math.Max(maximumBar, barNo);
                    double capacity = song.BeatsPerBar * (4.0 / song.BeatUnit);
                    double offset = (barNo - 1) * capacity;
                    double cursor = 0;
                    foreach (var part in bar.Groups[2].Value.Split('|'))
                    {
                        string token = part.Trim();
                        var m = EventPattern.Match(token);
                        if (!m.Success)
                        {
                            result.Errors.Add("Line " + lineNumber + ": unsupported event '" + token + "'.");
                            continue;
                        }
                        double duration = DurationQuarter(m.Groups[2].Value);
                        if (m.Groups[3].Value == ".") duration *= 1.5;
                        int velocity = currentTrack.Velocity;
                        if (m.Groups[4].Success && (!int.TryParse(m.Groups[4].Value, out velocity) || velocity < 1 || velocity > 127))
                        {
                            result.Errors.Add("Line " + lineNumber + ": event velocity must be 1-127.");
                            velocity = currentTrack.Velocity;
                        }
                        string body = m.Groups[1].Value;
                        if (cursor + duration > capacity + 0.00001)
                            result.Errors.Add("Line " + lineNumber + ": bar " + barNo + " overflows its time signature.");
                        if (!body.Equals("R", StringComparison.OrdinalIgnoreCase))
                        {
                            IEnumerable<string> pitches = body.StartsWith("[")
                                ? body.Substring(1, body.Length - 2).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                : new[] { body };
                            foreach (string pitch in pitches)
                            {
                                var ev = new MusicEvent
                                {
                                    TrackIndex = trackIndex,
                                    StartQuarter = offset + cursor,
                                    DurationQuarter = duration,
                                    Velocity = velocity
                                };
                                if (pitch.StartsWith("@")) ev.Trigger = pitch.Substring(1);
                                else
                                {
                                    int midi;
                                    if (!TryParsePitch(pitch, out midi))
                                    {
                                        result.Errors.Add("Line " + lineNumber + ": invalid pitch '" + pitch + "'.");
                                        continue;
                                    }
                                    ev.MidiNote = midi;
                                }
                                song.Events.Add(ev);
                            }
                        }
                        cursor += duration;
                    }
                    continue;
                }

                result.Errors.Add("Line " + lineNumber + ": unsupported statement '" + line + "'.");
            }

            if (!hasFence) result.Errors.Add("Missing ```notescript fenced section.");
            if (hasFence && !closed) result.Errors.Add("Unclosed notescript fence.");
            if (song.Tracks.Count == 0) result.Errors.Add("Song contains no tracks.");
            if (song.Events.Count == 0) result.Errors.Add("Song contains no playable notes or triggers.");
            if (song.Tracks.Count > 64) result.Errors.Add("Songs support up to 64 tracks.");
            if (song.Events.Count > 50000) result.Errors.Add("Songs support up to 50,000 events.");

            double secondsPerQuarter = 60.0 / song.Tempo;
            foreach (var ev in song.Events)
            {
                ev.TimeSeconds = ev.StartQuarter * secondsPerQuarter;
                ev.DurationSeconds = ev.DurationQuarter * secondsPerQuarter;
            }
            song.Events.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
            song.DurationSeconds = maximumBar * song.BeatsPerBar * (4.0 / song.BeatUnit) * secondsPerQuarter;
            if (result.Errors.Count == 0) result.Song = song;
            return result;
        }

        private static double DurationQuarter(string symbol)
        {
            switch (symbol.ToLowerInvariant())
            {
                case "w": return 4;
                case "h": return 2;
                case "q": return 1;
                case "e": return 0.5;
                case "s": return 0.25;
                default: return 1;
            }
        }

        private static void ReadFloat(string input, int line, MusicImportResult result, Action<float> assign, float min, float max)
        {
            float v;
            if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out v) || v < min || v > max)
                result.Errors.Add("Line " + line + ": parameter must be between " + min + " and " + max + ".");
            else assign(v);
        }

        public static bool TryParsePitch(string pitch, out int midi)
        {
            midi = 0;
            Match m = NotePattern.Match(pitch);
            if (!m.Success) return false;
            int octave;
            if (!int.TryParse(m.Groups[3].Value, out octave)) return false;
            string name = m.Groups[1].Value.ToUpperInvariant();
            int semitone = "C D EF G A B".IndexOf(name, StringComparison.Ordinal);
            // Explicit pitch-class table avoids the ambiguity of indexing note letters.
            switch (name)
            {
                case "C": semitone = 0; break;
                case "D": semitone = 2; break;
                case "E": semitone = 4; break;
                case "F": semitone = 5; break;
                case "G": semitone = 7; break;
                case "A": semitone = 9; break;
                case "B": semitone = 11; break;
                default: return false;
            }
            if (m.Groups[2].Value == "#") semitone++;
            if (m.Groups[2].Value == "b") semitone--;
            midi = (octave + 1) * 12 + semitone;
            return midi >= 0 && midi <= 127;
        }
    }
}
