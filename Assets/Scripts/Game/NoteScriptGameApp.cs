using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NoteScriptGame.Audio;
using NoteScriptGame.Music;
using UnityEngine;

namespace NoteScriptGame.Game
{
    /// <summary>Resolution-independent standalone Hybrid Music Game shell.</summary>
    public sealed partial class NoteScriptGameApp : MonoBehaviour
    {
        private enum Page { Library, Rhythm, Listen, FreePlay, Studio, Results }
        private sealed class SongRow { public string Path; public MusicSong Song; public bool Bundled; }
        private const double Approach = 2.5;
        private static readonly KeyCode[] LaneKeys = { KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.J, KeyCode.K, KeyCode.L };
        private static readonly KeyCode[] PianoKeys = { KeyCode.Z, KeyCode.S, KeyCode.X, KeyCode.D, KeyCode.C, KeyCode.V, KeyCode.G, KeyCode.B, KeyCode.H, KeyCode.N, KeyCode.J, KeyCode.M, KeyCode.Comma };

        private MusicPlaybackEngine player;
        private readonly List<SongRow> library = new List<SongRow>();
        private Page page = Page.Library;
        private MusicSong selected;
        private RhythmChart chart;
        private RhythmDifficulty difficulty = RhythmDifficulty.Normal;
        private bool guideMusic = true;
        private string importPath = "";
        private string status = "Ready. Choose a song to play.";
        private string studioText = "";
        private string studioFileName = "My First Song.md";
        private int score, combo, maxCombo, perfect, great, good, miss;
        private float life = 100;
        private bool cleared;
        private string lastJudgement = "";
        private double judgementUntil;
        private string userSongsDirectory;

        private void Awake()
        {
            Application.targetFrameRate = 120;
            gameObject.name = "NoteScriptGame";
            player = gameObject.AddComponent<MusicPlaybackEngine>();
            userSongsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "UserSongs"));
            Directory.CreateDirectory(userSongsDirectory);
            ReloadSongs();
        }

        private void ReloadSongs()
        {
            string oldPath = selected != null ? library.FirstOrDefault(r => r.Song == selected)?.Path : null;
            library.Clear();
            string bundled = Path.Combine(Application.streamingAssetsPath, "Songs");
            LoadSongDirectory(bundled, true);
            LoadSongDirectory(userSongsDirectory, false);
            library.Sort((a,b) => string.Compare(a.Song.Title, b.Song.Title, StringComparison.OrdinalIgnoreCase));
            var previous = library.FirstOrDefault(r => r.Path == oldPath);
            selected = previous != null ? previous.Song : library.Count > 0 ? library[0].Song : null;
            if (selected != null && string.IsNullOrEmpty(studioText)) studioText = selected.OriginalMarkdown;
            status = "Loaded " + library.Count + " songs. Imported files remain editable and independent.";
        }

        private void LoadSongDirectory(string directory, bool bundled)
        {
            if (!Directory.Exists(directory)) return;
            foreach (string path in Directory.GetFiles(directory, "*.md", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var result = NoteScriptMarkdownParser.Parse(File.ReadAllText(path));
                    if (result.Success) library.Add(new SongRow { Path = path, Song = result.Song, Bundled = bundled });
                    else Debug.LogWarning("NoteScriptGame skipped " + path + ": " + result.Message);
                }
                catch (Exception e) { Debug.LogWarning("Song load failed: " + path + " - " + e.Message); }
            }
        }

        private void ImportSong()
        {
            try
            {
                string source = importPath.Trim().Trim('"');
                if (!File.Exists(source) || !source.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Enter an existing .md file path.");
                string text = File.ReadAllText(source);
                MusicImportResult result = NoteScriptMarkdownParser.Parse(text);
                if (!result.Success) throw new InvalidOperationException(result.Message);
                string target = Path.Combine(userSongsDirectory, Path.GetFileName(source));
                if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                {
                    string name = Path.GetFileNameWithoutExtension(source);
                    for (int i = 1; File.Exists(target); i++) target = Path.Combine(userSongsDirectory, name + " (" + i + ").md");
                    File.Copy(source, target);
                }
                ReloadSongs();
                var row = library.FirstOrDefault(r => string.Equals(r.Path, target, StringComparison.OrdinalIgnoreCase));
                if (row != null) Select(row);
                status = "Imported: " + result.Song.Title + " (" + result.Song.Events.Count + " notes / triggers).";
                importPath = "";
            }
            catch (Exception ex) { status = "IMPORT ERROR: " + ex.Message; }
        }

        private void Select(SongRow entry)
        {
            selected = entry.Song;
            studioText = entry.Song.OriginalMarkdown;
            studioFileName = Path.GetFileName(entry.Path);
        }

        private void ChangePage(Page next)
        {
            if (page == next) return;
            player.StopSong();
            page = next;
            NativeBuildPage();
        }

        private void BeginListening()
        {
            if (selected == null) return;
            player.Volume = .75f;
            player.StartSong(selected, .25);
            ChangeToPlaying(Page.Listen);
        }

        private void BeginRhythm()
        {
            if (selected == null) return;
            chart = RhythmChartGenerator.Generate(selected, difficulty);
            score = combo = maxCombo = perfect = great = good = miss = 0;
            life = 100;
            cleared = false;
            lastJudgement = "";
            player.Volume = guideMusic ? .40f : 0f;
            player.StartSong(selected, Approach);
            ChangeToPlaying(Page.Rhythm);
        }

        private void ChangeToPlaying(Page target)
        {
            page = target;
            NativeBuildPage();
        }

        private void BeginFreePlay()
        {
            var song = new MusicSong { Title = "Free Play", DurationSeconds = 3600 };
            song.Tracks.Add(new MusicTrack { Name = "Keyboard", Sound = "piano", Volume = .8f });
            player.Volume = .90f;
            player.StartSong(song, .20);
            ChangeToPlaying(Page.FreePlay);
        }

        private void PlayPianoKey(int n)
        {
            var song = player.Song;
            if (song == null) return;
            player.PlayHit(new MusicEvent { TrackIndex = 0, MidiNote = 60 + n, Velocity = 105, DurationSeconds = .4 });
        }

        private void HitLane(int lane)
        {
            if (chart == null || player.IsPaused) return;
            double now = player.Position;
            RhythmNote best = null;
            double delta = .1401;
            foreach (RhythmNote n in chart.Notes)
            {
                if (n.Lane != lane || n.Judged) continue;
                double distance = Math.Abs(n.TimeSeconds - now);
                if (distance < delta) { delta = distance; best = n; }
            }
            if (best == null) return;
            best.Judged = true;
            best.Hit = true;
            if (best.IsHold) best.Holding = true;
            combo++;
            maxCombo = Math.Max(combo,maxCombo);
            int baseScore;
            if (delta <= .045) { perfect++; baseScore = 1000; lastJudgement = "PERFECT"; life = Mathf.Min(100,life+2); }
            else if (delta <= .090) { great++; baseScore = 700; lastJudgement = "GREAT"; life = Mathf.Min(100,life+1); }
            else { good++; baseScore = 400; lastJudgement = "GOOD"; }
            int multiplier = combo >= 50 ? 4 : combo >= 25 ? 3 : combo >= 10 ? 2 : 1;
            score += baseScore * multiplier;
            judgementUntil = Time.unscaledTime + .55;
            if (!guideMusic) { player.Volume = .65f; player.PlayHit(best.Source); player.Volume = 0f; }
            else player.PlayHit(best.Source);
        }

        private void ReleaseLane(int lane)
        {
            if (chart == null || page != Page.Rhythm) return;
            foreach (var n in chart.Notes)
            {
                if (n.Lane == lane && n.Holding)
                {
                    n.Holding = false;
                    if (player.Position < n.TimeSeconds + n.DurationSeconds - .12)
                    {
                        life -= 8;
                        combo = 0;
                        lastJudgement = "EARLY RELEASE";
                        judgementUntil = Time.unscaledTime + .55;
                    }
                }
            }
        }

        private void Update()
        {
            if (page == Page.Rhythm && chart != null && !player.IsPaused)
            {
                double now = player.Position;
                foreach (var n in chart.Notes)
                {
                    if (!n.Judged && now > n.TimeSeconds + .14)
                    {
                        n.Judged = true;
                        miss++;
                        combo = 0;
                        life -= 12;
                        lastJudgement = "MISS";
                        judgementUntil = Time.unscaledTime + .45;
                    }
                    if (n.Holding && now >= n.TimeSeconds + n.DurationSeconds) n.Holding = false;
                }
                if (life <= 0) EndRhythm(false);
                else if (now > chart.DurationSeconds + .45) EndRhythm(true);
            }
            NativeTick();
        }

        private void EndRhythm(bool success)
        {
            cleared = success;
            player.StopSong();
            page = Page.Results;
            NativeBuildPage();
        }

        private void NewStudioSong()
        {
            studioFileName = "New Song.md";
            studioText = "# New Song\n\n```notescript\ntempo: 120\ntime: 4/4\nkey: C major\n\ntrack Melody\nsound: piano\nvolume: 0.70\nvelocity: 90\nbar 1: C4 q | E4 q | G4 q | C5 q\nbar 2: [C4 E4 G4] h | G4 q | C5 q\n```\n";
            status = "New in-memory song. Edit and click Save as .md.";
        }

        private void ValidateStudio(bool save)
        {
            var result=NoteScriptMarkdownParser.Parse(studioText);
            if (!result.Success) { status = "VALIDATION: " + result.Message; return; }
            if (!save) { status = "VALID: "+result.Song.Title+" | "+result.Song.Events.Count+" events | "+result.Song.DurationSeconds.ToString("F1")+" sec"; return; }
            string filename=Path.GetFileName(studioFileName.Trim());
            if (!filename.EndsWith(".md",StringComparison.OrdinalIgnoreCase)) filename += ".md";
            if (filename.Length <= 3) { status="Choose a filename."; return; }
            try
            {
                string target=Path.Combine(userSongsDirectory,filename);
                File.WriteAllText(target,studioText);
                ReloadSongs();
                var row=library.FirstOrDefault(r=>string.Equals(r.Path,target,StringComparison.OrdinalIgnoreCase));
                if (row!=null) Select(row);
                status = "Saved: "+target;
            }
            catch(Exception ex) { status="SAVE ERROR: "+ex.Message; }
        }

        private void OnDestroy()
        {
            if (runtimePanel != null) Destroy(runtimePanel);
        }
    }
}
