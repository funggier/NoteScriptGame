using System;
using System.IO;
using System.Linq;
using NoteScriptGame.Game;
using NoteScriptGame.Music;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoteScriptGame.Editor
{
    public static class ProjectSetup
    {
        private const string MainScene = "Assets/Scenes/NoteScriptGame.unity";

        [MenuItem("NoteScriptGame/Create Bootstrap Scene")]
        public static void CreateBootstrapScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.04f, 0.09f);
            cameraGo.AddComponent<AudioListener>();
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0, 0, -10);
            new GameObject("NoteScriptGame Application").AddComponent<NoteScriptGameApp>();
            if (!EditorSceneManager.SaveScene(scene, MainScene))
                throw new InvalidOperationException("Could not save NoteScriptGame bootstrap scene.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
            PlayerSettings.companyName = "funggier";
            PlayerSettings.productName = "NoteScriptGame";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("NSG-SETUP PASS: main scene created, registered and ready for Play.");
        }

        [MenuItem("NoteScriptGame/Run Markdown and Chart Smoke Tests")]
        public static void RunSmokeChecks()
        {
            var root = Path.Combine(Application.streamingAssetsPath, "Songs");
            if (!Directory.Exists(root)) throw new Exception("Missing StreamingAssets/Songs.");
            var paths = Directory.GetFiles(root, "*.md");
            if (paths.Length < 2) throw new Exception("Need at least two bundled songs for smoke checks.");
            int totalEvents = 0, totalChartNotes = 0;
            foreach (string path in paths)
            {
                var parsed = NoteScriptMarkdownParser.Parse(File.ReadAllText(path));
                if (!parsed.Success)
                    throw new Exception("Markdown parser failure in "+Path.GetFileName(path)+"\n"+parsed.Message);
                var song=parsed.Song;
                if (song.DurationSeconds <= 0 || song.Events.Count == 0)
                    throw new Exception("Empty or invalid timeline: " + path);
                totalEvents += song.Events.Count;
                foreach (RhythmDifficulty difficulty in Enum.GetValues(typeof(RhythmDifficulty)))
                {
                    var a = RhythmChartGenerator.Generate(song, difficulty);
                    var b = RhythmChartGenerator.Generate(song, difficulty);
                    if (a.Notes.Count == 0 || a.Notes.Count != b.Notes.Count)
                        throw new Exception("Empty or non-deterministic chart: " + path);
                    var occupied = new System.Collections.Generic.HashSet<string>();
                    foreach (var note in a.Notes)
                    {
                        if (note.Lane < 0 || note.Lane >= 6 || note.TimeSeconds < 0)
                            throw new Exception("Invalid chart lane / time in " + path);
                        string slot = note.TimeSeconds.ToString("F6",System.Globalization.CultureInfo.InvariantCulture) + ":" + note.Lane;
                        if (!occupied.Add(slot)) throw new Exception("Same-lane note collision: "+path+" "+slot);
                    }
                    for (int i=0;i<a.Notes.Count;i++)
                        if (a.Notes[i].Lane != b.Notes[i].Lane || a.Notes[i].TimeSeconds != b.Notes[i].TimeSeconds)
                            throw new Exception("Chart generation is not deterministic: " + path);
                    totalChartNotes += a.Notes.Count;
                }
            }
            int midi;
            if (!NoteScriptMarkdownParser.TryParsePitch("C4",out midi) || midi != 60)
                throw new Exception("C4 MIDI must equal 60.");
            if (!NoteScriptMarkdownParser.TryParsePitch("F#4",out midi) || midi != 66)
                throw new Exception("F#4 MIDI must equal 66.");
            var malformed=NoteScriptMarkdownParser.Parse("# Bad\n\n```notescript\ntempo: 120\ntrack Test\nsound: piano\nbar 1: C4 madeup\n```");
            if (malformed.Success) throw new Exception("Unsupported token was silently accepted.");
            Debug.Log("NSG-SMOKE PASS: "+paths.Length+" Markdown songs, "+totalEvents+
                " source events, "+totalChartNotes+" chart notes across 3 difficulties, deterministic lanes, pitch mapping, invalid-token rejection.");
        }

        [MenuItem("NoteScriptGame/Build Windows Standalone")]
        public static void BuildWindows()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Windows", "NoteScriptGame.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("NSG Windows build failed: " + report.summary.result + " - " + report.summary.totalErrors + " errors.");
            Debug.Log("NSG-BUILD PASS: " + output);
        }
    }
}
