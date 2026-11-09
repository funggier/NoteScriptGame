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
    public sealed class NoteScriptGameApp : MonoBehaviour
    {
        private enum Page { Library, Rhythm, Listen, FreePlay, Studio, Results }
        private sealed class SongRow { public string Path; public MusicSong Song; public bool Bundled; }
        private const float Width = 1600;
        private const float Height = 900;
        private const double Approach = 2.5;
        private static readonly KeyCode[] LaneKeys = { KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.J, KeyCode.K, KeyCode.L };
        private static readonly KeyCode[] PianoKeys = { KeyCode.Z, KeyCode.S, KeyCode.X, KeyCode.D, KeyCode.C, KeyCode.V, KeyCode.G, KeyCode.B, KeyCode.H, KeyCode.N, KeyCode.J, KeyCode.M, KeyCode.Comma };
        private static readonly Color[] LaneColors = {
            new Color(.20f,.82f,1f), new Color(.42f,.55f,1f), new Color(.73f,.42f,1f),
            new Color(1f,.40f,.69f), new Color(1f,.69f,.35f), new Color(.56f,1f,.59f)
        };

        private MusicPlaybackEngine player;
        private readonly List<SongRow> library = new List<SongRow>();
        private Page page = Page.Library;
        private MusicSong selected;
        private RhythmChart chart;
        private RhythmDifficulty difficulty = RhythmDifficulty.Normal;
        private bool guideMusic = true;
        private Vector2 songScroll;
        private Vector2 studioScroll;
        private string importPath = "";
        private string status = "Ready. Choose a song to play.";
        private string studioText = "";
        private string studioFileName = "My First Song.md";
        private int score, combo, maxCombo, perfect, great, good, miss;
        private float life = 100;
        private bool cleared;
        private string lastJudgement = "";
        private double judgementUntil;
        private Texture2D white;
        private GUIStyle titleStyle, subtitleStyle, smallStyle, keyStyle, pianoKeyStyle, bigStyle, buttonStyle, accentButtonStyle, inputStyle, editorStyle;
        private readonly HashSet<int> down = new HashSet<int>();
        private string userSongsDirectory;

        private void Awake()
        {
            Application.targetFrameRate = 120;
            gameObject.name = "NoteScriptGame";
            player = gameObject.AddComponent<MusicPlaybackEngine>();
            white = new Texture2D(1,1,TextureFormat.RGBA32,false);
            white.SetPixel(0,0,Color.white);
            white.Apply();
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
            down.Clear();
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
            down.Clear();
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
        }

        private void EndRhythm(bool success)
        {
            cleared = success;
            player.StopSong();
            page = Page.Results;
        }

        private void HandleKeys()
        {
            Event e = Event.current;
            if (e == null || (e.type != EventType.KeyDown && e.type != EventType.KeyUp)) return;
            if (GUI.GetNameOfFocusedControl() == "SongPath" || GUI.GetNameOfFocusedControl() == "MusicEditor" || GUI.GetNameOfFocusedControl() == "StudioName")
                return;
            if (e.keyCode == KeyCode.Escape && e.type == EventType.KeyDown)
            {
                if (page == Page.Rhythm || page == Page.Listen || page == Page.FreePlay)
                    ChangePage(Page.Library);
                e.Use(); return;
            }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space && (page == Page.Listen || page == Page.Rhythm))
            {
                if (player.IsPaused) player.ResumeSong(); else player.PauseSong();
                e.Use(); return;
            }
            if (page == Page.Rhythm)
            {
                int index = Array.IndexOf(LaneKeys, e.keyCode);
                if (index >= 0)
                {
                    if (e.type == EventType.KeyDown && down.Add(index)) HitLane(index);
                    if (e.type == EventType.KeyUp) { down.Remove(index); ReleaseLane(index); }
                    e.Use();
                }
            }
            else if (page == Page.FreePlay && e.type == EventType.KeyDown)
            {
                int index = Array.IndexOf(PianoKeys, e.keyCode);
                if (index >= 0) { PlayPianoKey(index); e.Use(); }
            }
        }

        private void CreateStyles()
        {
            if (white == null || titleStyle != null) return;
            titleStyle = MakeLabel(33, new Color(.93f,.97f,1f), true);
            subtitleStyle = MakeLabel(22, new Color(.86f,.91f,1f), true);
            smallStyle = MakeLabel(17, new Color(.61f,.70f,.83f));
            keyStyle = MakeLabel(25, Color.white, true);
            keyStyle.alignment = TextAnchor.MiddleCenter;
            pianoKeyStyle = MakeLabel(20, new Color(.10f,.16f,.24f), true);
            pianoKeyStyle.alignment = TextAnchor.MiddleCenter;
            bigStyle = MakeLabel(60, Color.white, true);
            bigStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle = MakeButton(new Color(.14f,.19f,.30f));
            accentButtonStyle = MakeButton(new Color(.18f,.49f,.72f));
            inputStyle = new GUIStyle(GUI.skin.textField) { fontSize = 18, padding = new RectOffset(12,12,8,8) };
            inputStyle.normal.textColor = Color.white;
            editorStyle = new GUIStyle(GUI.skin.textArea) { fontSize = 17, padding = new RectOffset(18,16,16,16), wordWrap = false };
            editorStyle.normal.textColor = Color.white;
        }

        private GUIStyle MakeLabel(int size, Color color, bool bold = false)
        {
            return new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                normal = new GUIStyleState { textColor = color }, wordWrap = true, alignment = TextAnchor.MiddleLeft };
        }

        private GUIStyle MakeButton(Color color)
        {
            return new GUIStyle(GUI.skin.button)
            {
                fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = new GUIStyleState { background = MakeTexture(color), textColor = Color.white },
                hover = new GUIStyleState { background = MakeTexture(color * 1.30f), textColor = Color.white },
                active = new GUIStyleState { background = MakeTexture(color * .80f), textColor = Color.white },
                padding = new RectOffset(12,12,9,9), border = new RectOffset(0,0,0,0)
            };
        }

        private Texture2D MakeTexture(Color c)
        {
            var tex = new Texture2D(1,1);
            tex.SetPixel(0,0,c);
            tex.Apply();
            return tex;
        }

        private void Rect(float x, float y, float w, float h, Color color)
        {
            Color old = GUI.color; GUI.color = color;
            GUI.DrawTexture(new Rect(x,y,w,h),white);
            GUI.color = old;
        }
        private void Label(float x,float y,float w,float h,string text,GUIStyle style) { GUI.Label(new Rect(x,y,w,h),text,style); }
        private bool Button(float x,float y,float w,float h,string text,bool accent = false)
        { return GUI.Button(new Rect(x,y,w,h),text, accent ? accentButtonStyle : buttonStyle); }

        private void OnGUI()
        {
            CreateStyles();
            float s = Mathf.Min(Screen.width / Width, Screen.height / Height);
            float x = (Screen.width - Width*s)*.5f, y = (Screen.height - Height*s)*.5f;
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(x,y,0),Quaternion.identity,Vector3.one) * Matrix4x4.Scale(new Vector3(s,s,1));
            Rect(0,0,Width,Height,new Color(.035f,.055f,.10f));
            for (int i = 0; i < 24; i++) Rect(0, i*37, Width, 37, Color.Lerp(new Color(.03f,.07f,.14f,.15f),new Color(.13f,.05f,.15f,.10f),i/24f));
            Rect(0,0,Width,7,new Color(.13f,.72f,.94f));
            Rect(48,26,58,54,new Color(.13f,.70f,.86f));
            Label(58,26,49,52,"N",bigStyle);
            Label(122,23,470,47,"NOTESCRIPT GAME",titleStyle);
            Label(124,67,445,27,"PLAY  /  CREATE  /  PERFORM",smallStyle);
            if (Button(650,33,190,47,"MUSIC LIBRARY",page==Page.Library)) ChangePage(Page.Library);
            if (Button(853,33,165,47,"FREE PLAY",page==Page.FreePlay)) BeginFreePlay();
            if (Button(1031,33,177,47,"MUSIC STUDIO",page==Page.Studio)) { ChangePage(Page.Studio); if (selected != null) studioText = selected.OriginalMarkdown; }
            if (Button(1360,33,182,47,"REFRESH FILES")) ReloadSongs();
            Rect(48,103,1494,2,new Color(.18f,.28f,.42f));
            switch (page)
            {
                case Page.Library: DrawLibrary(); break;
                case Page.Listen: DrawListen(); break;
                case Page.Rhythm: DrawRhythm(); break;
                case Page.FreePlay: DrawFreePlay(); break;
                case Page.Studio: DrawStudio(); break;
                case Page.Results: DrawResults(); break;
            }
            Rect(0,848,Width,52,new Color(.055f,.082f,.14f));
            Label(48,854,1500,35, status.Length > 180 ? status.Substring(0,180)+"..." : status,smallStyle);
            HandleKeys();
            GUI.matrix = old;
        }

        private void DrawLibrary()
        {
            Label(62,115,720,55,"YOUR MUSIC. YOUR GAME.",titleStyle);
            Label(62,162,800,35,"Select a NoteScript Markdown song and listen or play BeatLane6.",smallStyle);
            Rect(60,214,860,566,new Color(.075f,.11f,.19f));
            Label(82,226,700,37,"SONG LIBRARY   (" + library.Count + ")",subtitleStyle);
            Rect(78,272,821,1,new Color(.20f,.29f,.42f));
            songScroll = GUI.BeginScrollView(new Rect(74,287,832,470),songScroll,new Rect(0,0,805,Mathf.Max(470,library.Count*66)));
            for (int i = 0; i < library.Count; i++)
            {
                var row = library[i]; float yy = i*66;
                bool isSelected = row.Song == selected;
                Rect(0,yy,790,60,isSelected ? new Color(.13f,.32f,.45f) : (i%2==0 ? new Color(.095f,.14f,.22f) : new Color(.08f,.12f,.20f)));
                if (GUI.Button(new Rect(8, yy+2,775,55),GUIContent.none,GUIStyle.none)) Select(row);
                Label(22,yy+4,540,30,row.Song.Title,subtitleStyle);
                Label(22,yy+33,735,20,row.Song.Tempo + " BPM    " + row.Song.Tracks.Count + " tracks    " +
                    TimeSpan.FromSeconds(row.Song.DurationSeconds).ToString(@"mm\:ss") + (row.Bundled ? "    BUILT-IN" : "    IMPORTED"),smallStyle);
                if (isSelected) Rect(0,yy,5,60,new Color(.26f,.85f,1f));
            }
            GUI.EndScrollView();
            Rect(948,214,590,566,new Color(.075f,.11f,.19f));
            Label(971,232,530,39,selected != null ? selected.Title : "No songs found",titleStyle);
            if (selected != null)
            {
                Label(975,293,520,38,"TEMPO        "+selected.Tempo+" BPM",subtitleStyle);
                Label(975,337,520,38,"TIME              "+selected.BeatsPerBar+"/"+selected.BeatUnit,subtitleStyle);
                Label(975,381,520,38,"TRACKS         "+selected.Tracks.Count,subtitleStyle);
                Label(975,425,520,38,"EVENTS          "+selected.Events.Count,subtitleStyle);
                if (Button(969,496,267,65,"PLAY RHYTHM",true)) BeginRhythm();
                if (Button(1251,496,264,65,"LISTEN",true)) BeginListening();
                if (Button(969,574,267,50,"EDIT MARKDOWN")) { ChangePage(Page.Studio); studioText = selected.OriginalMarkdown; }
                if (Button(1251,574,264,50,"DIFFICULTY: "+difficulty))
                    difficulty = (RhythmDifficulty)(((int)difficulty+1)%3);
            }
            Label(970,656,534,28,"Import another .md file (absolute local path):",smallStyle);
            GUI.SetNextControlName("SongPath");
            importPath = GUI.TextField(new Rect(972,690,395,45),importPath,inputStyle);
            if (Button(1380,690,140,45,"IMPORT",true)) ImportSong();
            Label(64,795,1450,28,"USER SONGS: " + userSongsDirectory,smallStyle);
        }

        private void DrawListen()
        {
            if (selected == null) { ChangePage(Page.Library); return; }
            Label(75,135,1410,53,"NOW PLAYING",titleStyle);
            Rect(85,208,1430,500,new Color(.075f,.11f,.19f));
            Label(133,238,1280,80,selected.Title,bigStyle);
            Label(133,330,1220,36,selected.Tempo + " BPM  /  " + selected.BeatsPerBar + "/" + selected.BeatUnit +
                "  /  " + selected.Tracks.Count + " TRACKS",subtitleStyle);
            double current = Math.Max(0,Math.Min(selected.DurationSeconds,player.Position));
            double duration = Math.Max(.01,selected.DurationSeconds);
            Rect(133,431,1315,12,new Color(.13f,.22f,.33f));
            Rect(133,431,(float)(1315*current/duration),12,new Color(.22f,.80f,.95f));
            Label(133,457,1290,35,TimeSpan.FromSeconds(current).ToString(@"mm\:ss") + "    /    " + TimeSpan.FromSeconds(duration).ToString(@"mm\:ss"),subtitleStyle);
            if (Button(132,560,250,65,player.IsPaused?"RESUME":"PAUSE",true)) { if (player.IsPaused) player.ResumeSong(); else player.PauseSong(); }
            if (Button(403,560,250,65,"REPLAY")) player.Seek(0);
            if (Button(675,560,250,65,"RHYTHM MODE",true)) BeginRhythm();
            if (Button(947,560,250,65,"STOP")) ChangePage(Page.Library);
            Label(90,748,1420,60,"Standalone Unity DSP-time music player  |  Native generated synth (sound packs are planned)",smallStyle);
        }

        private void DrawRhythm()
        {
            if (chart == null) { ChangePage(Page.Library); return; }
            double now = player.Position;
            Label(69,121,1000,42,selected.Title,subtitleStyle);
            Label(69,170,800,35,difficulty.ToString().ToUpperInvariant()+"   |   A S D  /  J K L   |   SPACE Pause   ESC Menu",smallStyle);
            Label(1120,122,380,47,"SCORE  " + score,subtitleStyle);
            Label(1120,170,410,35,"COMBO   "+combo+"     LIFE   "+Mathf.Max(0,life).ToString("0")+"%",smallStyle);
            const float left=320, top=230, laneWidth=160, hitY=706, travel=420;
            Rect(left-7,top-10,laneWidth*6+14,550,new Color(.075f,.10f,.19f));
            for (int i=0;i<6;i++)
            {
                Rect(left+i*laneWidth,top,laneWidth-4,540,new Color(.11f+i*.004f,.16f,.24f,.95f));
                Rect(left+i*laneWidth,hitY,laneWidth-4,9,LaneColors[i]);
                Label(left+i*laneWidth,hitY+17,laneWidth-4,55,LaneKeys[i].ToString(),keyStyle);
            }
            foreach (var n in chart.Notes)
            {
                if (n.Judged && !n.Holding) continue;
                double approachTime = n.TimeSeconds-now;
                if (approachTime > Approach+.35 || approachTime < -.35) continue;
                float py=hitY-(float)(approachTime/Approach)*travel;
                float nx=left+laneWidth*n.Lane+10;
                if (n.IsHold)
                {
                    float tailY=hitY-(float)((n.TimeSeconds+n.DurationSeconds-now)/Approach)*travel;
                    Rect(nx+42,Mathf.Max(top,tailY),laneWidth-104,Mathf.Max(0,py-Mathf.Max(top,tailY)),LaneColors[n.Lane]*.75f);
                }
                if (py>=top && py<=hitY+40)
                {
                    Rect(nx,py-17,laneWidth-24,34,LaneColors[n.Lane]);
                    Rect(nx+7,py-10,laneWidth-38,6,Color.white*.6f);
                }
            }
            if (lastJudgement.Length>0 && Time.unscaledTime < judgementUntil)
            {
                Label(1060,360,440,90,lastJudgement,bigStyle);
            }
            Label(100,265,190,50,"PERFECT "+perfect,subtitleStyle);
            Label(100,310,190,50,"GREAT       "+great,subtitleStyle);
            Label(100,355,190,50,"GOOD         "+good,subtitleStyle);
            Label(100,400,190,50,"MISS          "+miss,subtitleStyle);
            float progress=selected.DurationSeconds <=0?0:Mathf.Clamp01((float)(Math.Max(0,now)/selected.DurationSeconds));
            Rect(100,810,1380,10,new Color(.15f,.22f,.30f));
            Rect(100,810,1380*progress,10,new Color(.20f,.82f,.95f));
            if (Button(1060,745,200,52,player.IsPaused?"RESUME":"PAUSE")) { if (player.IsPaused) player.ResumeSong(); else player.PauseSong(); }
            if (Button(1280,745,200,52,"EXIT")) ChangePage(Page.Library);
            for (int i=0;i<6;i++)
                if (GUI.Button(new Rect(left+i*laneWidth,hitY-25,laneWidth-5,65),GUIContent.none,GUIStyle.none)) HitLane(i);
        }

        private void DrawResults()
        {
            Label(90,160,1400,90,cleared ? "STAGE CLEAR" : "STAGE FAILED",bigStyle);
            Rect(170,278,1260,385,new Color(.075f,.11f,.19f));
            Label(250,303,1100,70,selected != null ? selected.Title : "Result",titleStyle);
            Label(250,393,1050,65,"SCORE      "+score+"        MAX COMBO      "+maxCombo,titleStyle);
            Label(250,473,1080,60,"PERFECT   "+perfect+"       GREAT   "+great+"       GOOD   "+good+"       MISS   "+miss,subtitleStyle);
            int total=perfect+great+good+miss;
            double accuracy=total>0?100.0*(perfect*1.0+great*.7+good*.4)/total:0;
            Label(250,551,980,60,"WEIGHTED ACCURACY:   "+accuracy.ToString("F1")+"%",subtitleStyle);
            if (Button(400,712,340,73,"RETRY",true)) BeginRhythm();
            if (Button(860,712,340,73,"SONG LIBRARY")) ChangePage(Page.Library);
        }

        private void DrawFreePlay()
        {
            Label(76,138,1120,60,"FREE PLAY // PIANO",titleStyle);
            Label(78,199,1240,42,"Computer keyboard: Z S X D C V G B H N J M ,   |   ESC to return",smallStyle);
            Rect(78,283,1440,410,new Color(.08f,.12f,.20f));
            // One chromatic octave with a real black/white piano arrangement.
            int[] whiteNotes = { 0, 2, 4, 5, 7, 9, 11, 12 };
            string[] names = { "C4", "D4", "E4", "F4", "G4", "A4", "B4", "C5" };
            const float pianoLeft = 119f, whiteWidth = 166f, whiteTop = 336f;
            for (int i = 0; i < whiteNotes.Length; i++)
            {
                float px = pianoLeft + i * whiteWidth;
                Rect(px, whiteTop, whiteWidth-3, 285, new Color(.85f,.91f,.96f));
                Label(px+8, 557, whiteWidth-18, 29, names[i], pianoKeyStyle);
                Label(px+8, 586, whiteWidth-18, 29, PianoKeys[whiteNotes[i]].ToString(), pianoKeyStyle);
                if (GUI.Button(new Rect(px,whiteTop,whiteWidth-3,285),GUIContent.none,GUIStyle.none))
                    PlayPianoKey(whiteNotes[i]);
            }
            int[] blackNotes = { 1, 3, 6, 8, 10 };
            int[] blackAfterWhite = { 0, 1, 3, 4, 5 };
            for (int i = 0; i < blackNotes.Length; i++)
            {
                float px = pianoLeft + (blackAfterWhite[i]+1)*whiteWidth-54;
                Rect(px, whiteTop, 108, 181, new Color(.09f,.13f,.22f));
                Rect(px+7, whiteTop+9, 94, 6, new Color(.21f,.70f,.90f));
                Label(px, 467, 108, 37, PianoKeys[blackNotes[i]].ToString(), keyStyle);
                if (GUI.Button(new Rect(px,whiteTop,108,181),GUIContent.none,GUIStyle.none))
                    PlayPianoKey(blackNotes[i]);
            }
            Label(101,723,1410,60,"Native synthesized audio. Selectable sample instruments and piano-roll recording will be added to this standalone core.",smallStyle);
            if (Button(1252,747,238,55,"BACK")) ChangePage(Page.Library);
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

        private void DrawStudio()
        {
            Label(70,122,1040,55,"MUSIC STUDIO  //  MARKDOWN COMPOSE",titleStyle);
            Label(70,175,1210,33,"Editable .md workspace. Full piano roll, track mixer and .nsg project format are planned upgrades.",smallStyle);
            Rect(72,231,1024,540,new Color(.07f,.11f,.18f));
            GUI.SetNextControlName("MusicEditor");
            studioText = GUI.TextArea(new Rect(85,246,998,510),studioText,editorStyle);
            Rect(1116,231,403,540,new Color(.075f,.11f,.19f));
            Label(1142,250,345,42,"SONG FILE",subtitleStyle);
            GUI.SetNextControlName("StudioName");
            studioFileName = GUI.TextField(new Rect(1144,301,340,48),studioFileName,inputStyle);
            if(Button(1144,381,340,58,"NEW SONG")) NewStudioSong();
            if(Button(1144,456,340,58,"VALIDATE",true)) ValidateStudio(false);
            if(Button(1144,531,340,58,"SAVE AS .MD",true)) ValidateStudio(true);
            if(Button(1144,606,340,58,"SONG LIBRARY")) ChangePage(Page.Library);
            Label(78,789,1350,35,"The .md file stays readable and portable. Unsupported notation is rejected with a line number.",smallStyle);
        }

        private void OnDestroy()
        {
            if (white != null) Destroy(white);
        }
    }
}
