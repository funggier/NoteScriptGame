using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using NoteScriptGame.Music;

namespace NoteScriptGame.Game
{
    // This UI replaces the original IMGUI 1600x900 matrix. All pixels are native UI Toolkit units.
    public sealed partial class NoteScriptGameApp
    {
        private const string UiScaleKey = "NSG.UI.ScalePercent";
        private static readonly Color UiBack = new Color(.026f,.041f,.071f);
        private static readonly Color UiPanel = new Color(.061f,.088f,.140f);
        private static readonly Color UiRaised = new Color(.083f,.12f,.18f);
        private static readonly Color UiText = new Color(.94f,.97f,1f);
        private static readonly Color UiMute = new Color(.59f,.69f,.82f);
        private static readonly Color UiAccent = new Color(.13f,.67f,.85f);
        private static readonly Color UiBorder = new Color(.17f,.25f,.35f);

        private UIDocument runtimeDocument;
        private PanelSettings runtimePanel;
        private VisualElement nativeRoot;
        private VisualElement nativeContent;
        private VisualElement nativeModal;
        private VisualElement nativeStageContainer;
        private VisualElement nativeStage;
        private VisualElement nativeProgressBar;
        private Label nativeStatus;
        private Label nativeDisplayInfo;
        private Label nativeClock;
        private Label nativeStats;
        private Label nativeJudgement;
        private TextField nativeImportPath;
        private TextField nativeEditor;
        private TextField nativeFilename;
        private int nativeUiPercent;
        private readonly List<VisualElement> nativeNotes = new List<VisualElement>();
        private readonly VisualElement[] nativeLanes = new VisualElement[6];
        private readonly HashSet<int> nativePressed = new HashSet<int>();
        private static readonly Color[] nativeLaneColors = {
            new Color(.12f,.78f,.94f), new Color(.38f,.58f,.99f),
            new Color(.72f,.43f,.92f), new Color(.98f,.40f,.69f),
            new Color(.99f,.66f,.29f), new Color(.43f,.89f,.53f)
        };

        private void Start()
        {
            runtimeDocument = GetComponent<UIDocument>();
            if (runtimeDocument == null) runtimeDocument = gameObject.AddComponent<UIDocument>();
            PanelSettings panelAsset = runtimeDocument.panelSettings != null
                ? runtimeDocument.panelSettings : Resources.Load<PanelSettings>("NoteScriptGamePanel");
            if (panelAsset == null)
            {
                Debug.LogError("NoteScriptGame requires Assets/Resources/NoteScriptGamePanel.asset.");
                return;
            }
            runtimePanel = Instantiate(panelAsset);
            runtimePanel.scaleMode = PanelScaleMode.ConstantPixelSize;
            nativeUiPercent = PlayerPrefs.GetInt(UiScaleKey, Screen.height >= 1200 ? 110 : 100);
            nativeUiPercent = Mathf.Clamp(nativeUiPercent, 85, 130);
            runtimePanel.scale = nativeUiPercent / 100f;
            runtimeDocument.panelSettings = runtimePanel;
            nativeRoot = runtimeDocument.rootVisualElement;
            nativeRoot.Clear();
            nativeRoot.style.flexGrow = 1;
            nativeRoot.style.backgroundColor = UiBack;
            nativeRoot.style.flexDirection = FlexDirection.Column;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.flexWrap = Wrap.Wrap;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.Center;
            header.style.paddingLeft = 15;
            header.style.paddingRight = 15;
            header.style.paddingTop = 9;
            header.style.paddingBottom = 9;
            header.style.backgroundColor = UiPanel;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = UiBorder;
            header.style.flexShrink = 0;
            nativeRoot.Add(header);

            var brand = new VisualElement();
            brand.style.flexDirection = FlexDirection.Column;
            brand.style.minWidth = 250;
            brand.style.marginRight = 16;
            header.Add(brand);
            NText(brand, "NOTESCRIPT GAME", 23, true);
            NText(brand, "PLAY  /  CREATE  /  PERFORM", 12, false, UiMute);
            NButton(header, "LIBRARY", () => ChangePage(Page.Library));
            NButton(header, "RHYTHM", BeginRhythm);
            NButton(header, "FREE PLAY", BeginFreePlay);
            NButton(header, "MUSIC STUDIO", () => ChangePage(Page.Studio));
            NButton(header, "SETTINGS", NativeToggleSettings);

            nativeContent = new VisualElement();
            nativeContent.style.flexGrow = 1;
            nativeContent.style.minHeight = 0;
            nativeContent.style.alignItems = Align.Center;
            nativeContent.style.width = Length.Percent(100);
            nativeRoot.Add(nativeContent);

            var footer = new VisualElement();
            footer.style.height = 32;
            footer.style.flexShrink = 0;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.alignItems = Align.Center;
            footer.style.paddingLeft = 22;
            footer.style.paddingRight = 22;
            footer.style.backgroundColor = UiPanel;
            nativeRoot.Add(footer);
            nativeStatus = NText(footer, status, 12, false, UiMute);
            nativeDisplayInfo = NText(footer, "", 12, false, UiMute);
            Debug.Log("NSG-NATIVE-UI INITIALIZING: " + Screen.width + "x" + Screen.height + ", scale " + nativeUiPercent + "%, theme " + (runtimePanel.themeStyleSheet != null));
            NativeBuildPage();
            Debug.Log("NSG-NATIVE-UI READY: " + (nativeRoot != null && nativeContent != null ? "PASS" : "FAIL"));
        }

        private static Label NText(VisualElement parent, string value, int font = 16, bool bold = false, Color? color = null)
        {
            var label = new Label(value);
            label.style.fontSize = font;
            label.style.color = color ?? UiText;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 5;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            parent.Add(label);
            return label;
        }

        private static VisualElement NCard(VisualElement parent)
        {
            var card = new VisualElement();
            card.style.backgroundColor = UiPanel;
            card.style.paddingLeft = 20;
            card.style.paddingRight = 20;
            card.style.paddingTop = 18;
            card.style.paddingBottom = 18;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = UiBorder;
            card.style.borderBottomColor = UiBorder;
            card.style.borderLeftColor = UiBorder;
            card.style.borderRightColor = UiBorder;
            parent.Add(card);
            return card;
        }

        private static VisualElement NRow(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 9;
            parent.Add(row);
            return row;
        }

        private static Button NButton(VisualElement parent, string title, Action click, bool primary = false)
        {
            var button = new Button(click) { text = title };
            button.style.fontSize = 14;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.color = UiText;
            button.style.backgroundColor = primary ? new Color(.12f,.43f,.61f) : UiRaised;
            button.style.marginRight = 7;
            button.style.marginBottom = 6;
            button.style.paddingLeft = 16;
            button.style.paddingRight = 16;
            button.style.minHeight = 37;
            button.style.minWidth = 90;
            parent.Add(button);
            return button;
        }

        private static VisualElement NSpacer(VisualElement parent, int height)
        {
            var spacer = new VisualElement();
            spacer.style.height = height;
            spacer.style.flexShrink = 0;
            parent.Add(spacer);
            return spacer;
        }

        private static VisualElement NProgress(VisualElement parent)
        {
            var bar = new VisualElement();
            bar.style.height = 9;
            bar.style.width = Length.Percent(100);
            bar.style.marginTop = 12;
            bar.style.marginBottom = 13;
            bar.style.backgroundColor = UiRaised;
            var filled = new VisualElement();
            filled.style.height = Length.Percent(100);
            filled.style.width = 0;
            filled.style.backgroundColor = UiAccent;
            bar.Add(filled);
            parent.Add(bar);
            return filled;
        }

        private VisualElement NContent(bool scroll = false)
        {
            VisualElement body;
            if (scroll)
            {
                var list = new ScrollView();
                list.style.width = Length.Percent(100);
                list.style.flexGrow = 1;
                nativeContent.Add(list);
                body = new VisualElement();
                body.style.width = Length.Percent(100);
                body.style.maxWidth = 1860;
                body.style.alignSelf = Align.Center;
                list.Add(body);
            }
            else
            {
                body = new VisualElement();
                body.style.flexGrow = 1;
                body.style.minHeight = 0;
                body.style.width = Length.Percent(100);
                body.style.maxWidth = 1860;
                nativeContent.Add(body);
            }
            body.style.paddingLeft = 25;
            body.style.paddingRight = 25;
            body.style.paddingTop = 22;
            body.style.paddingBottom = 15;
            return body;
        }

        private void NativeBuildPage()
        {
            if (nativeContent == null) return;
            nativeContent.Clear();
            nativeStage = null;
            nativeStageContainer = null;
            nativeProgressBar = null;
            nativeClock = null;
            nativeStats = null;
            nativeJudgement = null;
            nativeNotes.Clear();
            for (int i = 0; i < nativeLanes.Length; i++) nativeLanes[i] = null;
            nativePressed.Clear();
            switch (page)
            {
                case Page.Library: NativeLibrary(); break;
                case Page.Listen: NativeListen(); break;
                case Page.Rhythm: NativeRhythm(); break;
                case Page.FreePlay: NativePiano(); break;
                case Page.Studio: NativeStudio(); break;
                case Page.Results: NativeResults(); break;
            }
            NativeFooter();
        }

        private void NativeFooter()
        {
            if (nativeStatus == null) return;
            nativeStatus.text = status.Length > 150 ? status.Substring(0, 150) + "..." : status;
            nativeDisplayInfo.text = Screen.width + " × " + Screen.height + "  /  UI " + nativeUiPercent + "%";
        }

        private void NativeLibrary()
        {
            var content = NContent(true);
            NText(content, "YOUR MUSIC. YOUR GAME.", 28, true);
            NText(content, "Offline library  ·  Standalone Unity C#  ·  NoteScript .md compatible import", 15, false, UiMute);
            var columns = NRow(content);
            columns.style.marginTop = 19;

            var songs = NCard(columns);
            songs.style.flexBasis = 570;
            songs.style.flexGrow = 1.2f;
            songs.style.minWidth = 350;
            songs.style.height = 615;
            songs.style.marginRight = 12;
            songs.style.marginBottom = 12;
            NText(songs, "SONG LIBRARY  (" + library.Count + ")", 19, true);
            var songList = new ScrollView();
            songList.style.flexGrow = 1;
            songList.style.marginTop = 13;
            songs.Add(songList);
            foreach (SongRow entry in library)
            {
                SongRow row = entry;
                bool active = selected == row.Song;
                var item = new Button(() => { Select(row); NativeBuildPage(); });
                item.style.backgroundColor = active ? new Color(.13f,.29f,.36f) : UiRaised;
                item.style.paddingLeft = 13;
                item.style.paddingTop = 10;
                item.style.paddingBottom = 9;
                item.style.marginBottom = 6;
                item.style.alignItems = Align.FlexStart;
                item.style.borderLeftWidth = active ? 3 : 0;
                item.style.borderLeftColor = UiAccent;
                NText(item, row.Song.Title, 17, true);
                NText(item, row.Song.Tempo + " BPM  ·  " + row.Song.Tracks.Count + " tracks  ·  " +
                    TimeSpan.FromSeconds(row.Song.DurationSeconds).ToString(@"mm\:ss") +
                    (row.Bundled ? "  ·  BUILT-IN" : "  ·  IMPORTED"), 12, false, UiMute);
                songList.Add(item);
            }

            var details = NCard(columns);
            details.style.flexBasis = 420;
            details.style.flexGrow = 1;
            details.style.minWidth = 340;
            details.style.minHeight = 615;
            details.style.marginBottom = 12;
            NText(details, selected != null ? selected.Title : "SELECT A SONG", 23, true);
            if (selected != null)
            {
                NText(details, "Tempo        " + selected.Tempo + " BPM", 17);
                NText(details, "Meter         " + selected.BeatsPerBar + "/" + selected.BeatUnit, 17);
                NText(details, "Tracks         " + selected.Tracks.Count, 17);
                NText(details, "Events         " + selected.Events.Count, 17);
                var row = NRow(details);
                NButton(row, "PLAY RHYTHM", BeginRhythm, true);
                NButton(row, "LISTEN", BeginListening);
                var other = NRow(details);
                NButton(other, "DIFFICULTY: " + difficulty.ToString().ToUpperInvariant(), () =>
                {
                    difficulty = (RhythmDifficulty)(((int)difficulty + 1) % 3);
                    NativeBuildPage();

                });
                NButton(other, "EDIT SONG", () => ChangePage(Page.Studio));
            }
            NSpacer(details, 18);
            NText(details, "IMPORT MARKDOWN", 16, true);
            NText(details, "Paste the absolute file path of an existing .md song.", 13, false, UiMute);
            nativeImportPath = new TextField();
            nativeImportPath.value = importPath;
            nativeImportPath.style.minHeight = 36;
            nativeImportPath.style.marginTop = 10;
            nativeImportPath.style.marginBottom = 9;
            nativeImportPath.RegisterValueChangedCallback(e => importPath = e.newValue);
            details.Add(nativeImportPath);
            NButton(details, "IMPORT .MD", () => { ImportSong(); NativeBuildPage(); }, true);
            NSpacer(details, 10);
            NButton(details, "REFRESH FILES", () => { ReloadSongs(); NativeBuildPage(); });
            NText(content, "UserSongs: " + userSongsDirectory, 12, false, UiMute);
        }

        private void NativeListen()
        {
            if (selected == null) return;
            var content = NContent();
            content.style.justifyContent = Justify.Center;
            var card = NCard(content);
            card.style.maxWidth = 1080;
            card.style.width = Length.Percent(100);
            card.style.alignSelf = Align.Center;
            card.style.paddingTop = 38;
            card.style.paddingBottom = 38;
            NText(card, "NOW PLAYING", 15, true, UiAccent);
            NText(card, selected.Title, 35, true);
            NText(card, selected.Tempo + " BPM   ·   " + selected.BeatsPerBar + "/" + selected.BeatUnit +
                "   ·   " + selected.Tracks.Count + " TRACKS", 15, false, UiMute);
            NSpacer(card, 26);
            nativeProgressBar = NProgress(card);
            nativeClock = NText(card, "", 16);
            var actions = NRow(card);
            NButton(actions, "PAUSE / RESUME", () => { if (player.IsPaused) player.ResumeSong(); else player.PauseSong(); }, true);
            NButton(actions, "REPLAY", () => player.Seek(0));
            NButton(actions, "RHYTHM MODE", BeginRhythm);
            NButton(actions, "BACK", () => ChangePage(Page.Library));
            NText(card, "DSP-timed music playback at the native display resolution.", 13, false, UiMute);
        }

        private void NativeRhythm()
        {
            if (selected == null || chart == null) return;
            var content = NContent();
            content.style.paddingTop = 8;
            content.style.paddingBottom = 5;
            var top = NRow(content);
            top.style.justifyContent = Justify.SpaceBetween;
            NText(top, selected.Title + "  ·  " + difficulty.ToString().ToUpperInvariant(), 19, true);
            nativeStats = NText(top, "SCORE 0   ·   COMBO 0   ·   LIFE 100%", 18, true, UiAccent);

            nativeStageContainer = new VisualElement();
            nativeStageContainer.style.flexGrow = 1;
            nativeStageContainer.style.minHeight = 80;
            nativeStageContainer.style.alignItems = Align.Center;
            nativeStageContainer.style.justifyContent = Justify.Center;
            nativeStageContainer.RegisterCallback<GeometryChangedEvent>(_ => NativeFitStage());
            content.Add(nativeStageContainer);

            nativeStage = new VisualElement();
            nativeStage.style.position = Position.Relative;
            nativeStage.style.overflow = Overflow.Hidden;
            nativeStage.style.backgroundColor = new Color(.039f,.064f,.11f);
            nativeStageContainer.Add(nativeStage);
            for (int i = 0; i < 6; i++)
            {
                int laneNumber = i;
                var lane = new VisualElement();
                lane.style.position = Position.Absolute;
                lane.style.borderRightWidth = 1;
                lane.style.borderRightColor = UiBorder;
                lane.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    nativePressed.Add(laneNumber);
                    HitLane(laneNumber);
                    lane.CapturePointer(e.pointerId);
                });
                lane.RegisterCallback<PointerUpEvent>(e =>
                {
                    nativePressed.Remove(laneNumber);
                    ReleaseLane(laneNumber);
                    if (lane.HasPointerCapture(e.pointerId)) lane.ReleasePointer(e.pointerId);
                });
                nativeLanes[i] = lane;
                nativeStage.Add(lane);
                var hit = new VisualElement { name = "hitLine" };
                hit.style.position = Position.Absolute;
                hit.style.left = 0;
                hit.style.right = 0;
                hit.style.height = 5;
                hit.style.backgroundColor = nativeLaneColors[i];
                hit.pickingMode = PickingMode.Ignore;
                lane.Add(hit);
                var legend = NText(lane, LaneKeys[i].ToString(), 19, true, nativeLaneColors[i]);
                legend.name = "keyLegend";
                legend.style.position = Position.Absolute;
                legend.style.left = 0;
                legend.style.right = 0;
                legend.style.bottom = 13;
                legend.style.unityTextAlign = TextAnchor.MiddleCenter;
                legend.pickingMode = PickingMode.Ignore;
            }
            for (int i = 0; i < 128; i++)
            {
                var note = new VisualElement();
                note.style.position = Position.Absolute;
                note.style.display = DisplayStyle.None;
                note.pickingMode = PickingMode.Ignore;
                nativeStage.Add(note);
                nativeNotes.Add(note);
            }
            nativeJudgement = NText(nativeStage, "", 29, true);
            nativeJudgement.pickingMode = PickingMode.Ignore;
            nativeJudgement.style.position = Position.Absolute;
            nativeJudgement.style.unityTextAlign = TextAnchor.MiddleCenter;

            var controls = NRow(content);
            controls.style.justifyContent = Justify.SpaceBetween;
            NText(controls, "A S D  /  J K L     ·     SPACE Pause     ·     ESC Exit", 14, false, UiMute);
            NButton(controls, "PAUSE", () => { if (player.IsPaused) player.ResumeSong(); else player.PauseSong(); });
            NButton(controls, "EXIT", () => ChangePage(Page.Library));
            nativeProgressBar = NProgress(content);
        }

        private void NativeFitStage()
        {
            if (nativeStageContainer == null || nativeStage == null) return;
            float viewportWidth = nativeStageContainer.resolvedStyle.width - 8f;
            float viewportHeight = nativeStageContainer.resolvedStyle.height - 8f;
            if (viewportWidth < 100 || viewportHeight < 100) return;
            float width = Mathf.Floor(Mathf.Min(viewportWidth, viewportHeight * 16f / 9f));
            float height = Mathf.Floor(width * 9f / 16f);
            nativeStage.style.width = width;
            nativeStage.style.height = height;
            float left = width * .16f;
            float laneWidth = width * .68f / 6f;
            for (int i = 0; i < 6; i++)
            {
                var lane = nativeLanes[i];
                if (lane == null) continue;
                lane.style.left = Mathf.Round(left + i * laneWidth);
                lane.style.top = 0;
                lane.style.width = Mathf.Ceil(laneWidth);
                lane.style.height = height;
                var marker = lane.Q<VisualElement>("hitLine");
                if (marker != null)
                {
                    marker.style.top = Mathf.Round(height * .80f);
                    marker.style.height = Mathf.Max(4, height * .012f);
                }
            }
            if (nativeJudgement != null)
            {
                nativeJudgement.style.left = width * .16f;
                nativeJudgement.style.top = height * .25f;
                nativeJudgement.style.width = width * .68f;
            }
        }

        private void NativeDrawNotes()
        {
            if (nativeStage == null || chart == null) return;
            float width = nativeStage.resolvedStyle.width;
            float height = nativeStage.resolvedStyle.height;
            if (width < 200 || height < 100) return;
            double now = player.Position;
            float laneWidth = width * .68f / 6f;
            float laneLeft = width * .16f;
            float yHit = height * .80f;
            float distance = height * .71f;
            int count = 0;
            foreach (RhythmNote n in chart.Notes)
            {
                if (n.Judged && !n.Holding) continue;
                double until = n.TimeSeconds - now;
                if (until > Approach + .25 || until < -.30) continue;
                if (count >= nativeNotes.Count) break;
                var visual = nativeNotes[count++];
                float length = n.IsHold ? Mathf.Max(14, (float)(n.DurationSeconds / Approach * distance)) : Mathf.Max(12, height * .035f);
                visual.style.display = DisplayStyle.Flex;
                visual.style.left = Mathf.Round(laneLeft + n.Lane * laneWidth + 4f);
                visual.style.top = yHit - (float)(until / Approach * distance) - length * .5f;
                visual.style.width = Mathf.Floor(laneWidth - 9);
                visual.style.height = length;
                visual.style.backgroundColor = nativeLaneColors[n.Lane];
                visual.style.borderBottomWidth = 2;
                visual.style.borderBottomColor = Color.white;
            }
            for (int i = count; i < nativeNotes.Count; i++) nativeNotes[i].style.display = DisplayStyle.None;
            for (int i = 0; i < nativeLanes.Length; i++)
                if (nativeLanes[i] != null) nativeLanes[i].style.backgroundColor =
                    nativePressed.Contains(i) ? new Color(.13f,.25f,.32f) :
                    (i % 2 == 0 ? new Color(.08f,.13f,.21f) : new Color(.067f,.109f,.17f));
        }

        private void NativePiano()
        {
            var content = NContent();
            content.style.justifyContent = Justify.Center;
            NText(content, "FREE PLAY  /  PIANO", 29, true);
            NText(content, "Z S X D C V G B H N J M ,   ·   One chromatic octave   ·   ESC to exit", 15, false, UiMute);
            NSpacer(content, 21);
            var card = NCard(content);
            NText(card, "LIVE INSTRUMENT", 17, true, UiAccent);
            var keyboard = new VisualElement();
            keyboard.style.position = Position.Relative;
            keyboard.style.width = Length.Percent(100);
            keyboard.style.maxWidth = 1380;
            keyboard.style.alignSelf = Align.Center;
            keyboard.style.height = 305;
            keyboard.style.marginTop = 20;
            card.Add(keyboard);

            var whites = new[] {0,2,4,5,7,9,11,12};
            var blacks = new[] {1,3,6,8,10};
            foreach (int note in whites) NativePianoKey(keyboard, note, false);
            foreach (int note in blacks) NativePianoKey(keyboard, note, true);
            keyboard.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float keyWidth = keyboard.resolvedStyle.width / 8f;
                if (keyWidth < 12) return;
                foreach (var child in keyboard.Children())
                {
                    int note = int.Parse(child.name.Substring(2));
                    int index = Array.IndexOf(whites, note);
                    bool black = index < 0;
                    if (!black)
                    {
                        child.style.left = Mathf.Floor(index * keyWidth);
                        child.style.width = Mathf.Floor(keyWidth - 2);
                    }
                    else
                    {
                        int leftWhite = note == 1 ? 0 : note == 3 ? 1 : note == 6 ? 3 : note == 8 ? 4 : 5;
                        child.style.left = Mathf.Floor((leftWhite + .70f) * keyWidth);
                        child.style.width = Mathf.Floor(keyWidth * .58f);
                    }
                }
            });
            NSpacer(content, 20);
            NButton(NRow(content), "SONG LIBRARY", () => ChangePage(Page.Library));
            NText(content, "Sample instruments, sustain and recording are planned improvements.", 13, false, UiMute);
        }

        private void NativePianoKey(VisualElement parent, int note, bool black)
        {
            int index = note;
            var key = new VisualElement();
            key.name = "n_" + note;
            key.style.position = Position.Absolute;
            key.style.top = 0;
            key.style.height = black ? 188 : 292;
            key.style.justifyContent = Justify.FlexEnd;
            key.style.alignItems = Align.Center;
            key.style.paddingBottom = 12;
            key.style.backgroundColor = black ? new Color(.07f,.11f,.18f) : new Color(.88f,.92f,.95f);
            key.style.borderBottomWidth = 5;
            key.style.borderBottomColor = black ? UiAccent : new Color(.48f,.56f,.66f);
            string legend = PianoKeys[note].ToString();
            NText(key, legend, 17, true, black ? UiText : new Color(.12f,.17f,.24f)).pickingMode = PickingMode.Ignore;
            key.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0) PlayPianoKey(index); });
            parent.Add(key);
        }

        private void NativeStudio()
        {
            var content = NContent();
            NText(content, "MUSIC STUDIO  /  MARKDOWN COMPOSE", 27, true);
            NText(content, "Edit and validate NoteScript .md songs. Piano roll and multi-track mixer will follow.", 14, false, UiMute);
            var columns = NRow(content);
            columns.style.flexGrow = 1;
            columns.style.minHeight = 0;
            columns.style.marginTop = 15;

            var editorCard = NCard(columns);
            editorCard.style.flexGrow = 3;
            editorCard.style.flexBasis = 690;
            editorCard.style.minWidth = 360;
            editorCard.style.minHeight = 400;
            editorCard.style.marginRight = 12;
            editorCard.style.marginBottom = 12;
            nativeEditor = new TextField();
            nativeEditor.multiline = true;
            nativeEditor.value = studioText;
            nativeEditor.style.fontSize = 16;
            nativeEditor.style.flexGrow = 1;
            nativeEditor.style.minHeight = 370;
            nativeEditor.RegisterValueChangedCallback(e => studioText = e.newValue);
            editorCard.Add(nativeEditor);

            var tools = NCard(columns);
            tools.style.flexGrow = 1;
            tools.style.flexBasis = 280;
            tools.style.minWidth = 260;
            tools.style.minHeight = 400;
            tools.style.marginBottom = 12;
            NText(tools, "SONG FILE", 17, true);
            nativeFilename = new TextField();
            nativeFilename.value = studioFileName;
            nativeFilename.RegisterValueChangedCallback(e => studioFileName = e.newValue);
            nativeFilename.style.marginTop = 10;
            nativeFilename.style.marginBottom = 12;
            tools.Add(nativeFilename);
            NButton(tools, "NEW SONG", () => { NewStudioSong(); nativeEditor.value = studioText; nativeFilename.value = studioFileName; NativeFooter(); });
            NButton(tools, "VALIDATE", () => { ValidateStudio(false); NativeFooter(); });
            NButton(tools, "SAVE .MD", () => { ValidateStudio(true); NativeFooter(); }, true);
            NButton(tools, "LIBRARY", () => ChangePage(Page.Library));
        }

        private void NativeResults()
        {
            var content = NContent();
            content.style.justifyContent = Justify.Center;
            var card = NCard(content);
            card.style.maxWidth = 980;
            card.style.alignSelf = Align.Center;
            card.style.width = Length.Percent(100);
            NText(card, cleared ? "STAGE CLEAR" : "STAGE FAILED", 36, true, cleared ? UiAccent : new Color(.98f,.42f,.54f));
            NText(card, selected != null ? selected.Title : "Results", 21, true);
            NSpacer(card, 20);
            NText(card, "SCORE  " + score + "     MAX COMBO  " + maxCombo, 25, true);
            NText(card, "PERFECT  " + perfect + "     GREAT  " + great + "     GOOD  " + good + "     MISS  " + miss, 16);
            int total = perfect + great + good + miss;
            double accuracy = total > 0 ? (perfect + great*.7 + good*.4)*100.0 / total : 0;
            NText(card, "WEIGHTED ACCURACY: " + accuracy.ToString("F1") + "%", 20, true, UiAccent);
            var buttons = NRow(card);
            NButton(buttons, "RETRY", BeginRhythm, true);
            NButton(buttons, "SONG LIBRARY", () => ChangePage(Page.Library));
        }

        private void NativeToggleSettings()
        {
            if (nativeRoot == null) return;
            if (nativeModal != null)
            {
                nativeModal.RemoveFromHierarchy();
                nativeModal = null;
                return;
            }
            nativeModal = new VisualElement();
            nativeModal.style.position = Position.Absolute;
            nativeModal.style.left = 0;
            nativeModal.style.right = 0;
            nativeModal.style.top = 0;
            nativeModal.style.bottom = 0;
            nativeModal.style.backgroundColor = new Color(0f,0f,0f,.8f);
            nativeModal.style.justifyContent = Justify.Center;
            nativeModal.style.alignItems = Align.Center;
            nativeRoot.Add(nativeModal);
            var card = NCard(nativeModal);
            card.style.width = Length.Percent(88);
            card.style.maxWidth = 550;
            NText(card, "DISPLAY SETTINGS", 25, true);
            NText(card, "For 23–27 inch 1080p and 1440p screens, without upscaled 900p text.", 14, false, UiMute);
            NSpacer(card, 17);
            NText(card, "UI SCALE  /  keeps native output resolution", 16, true);
            var presets = NRow(card);
            foreach (int preset in new[] {85,100,110,125})
            {
                int value = preset;
                NButton(presets, value + "%", () =>
                {
                    nativeUiPercent = value;
                    runtimePanel.scale = value / 100f;
                    PlayerPrefs.SetInt(UiScaleKey, value);
                    PlayerPrefs.Save();
                    NativeToggleSettings();
                    NativeFooter();
                }, preset == nativeUiPercent);
            }
            NSpacer(card, 14);
            NText(card, "WINDOW MODE", 16, true);
            var modes = NRow(card);
            NButton(modes, "NATIVE FULLSCREEN", () =>
            {
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height,
                    FullScreenMode.FullScreenWindow);
                NativeToggleSettings();
            });
            NButton(modes, "16:9 WINDOWED", () =>
            {
                float w = Mathf.Min(1600, Screen.currentResolution.width - 100);
                float h = Mathf.Min(900, Screen.currentResolution.height - 110);
                float s = Mathf.Min(w / 16f, h / 9f);
                Screen.SetResolution(Mathf.RoundToInt(16f*s), Mathf.RoundToInt(9f*s),
                    FullScreenMode.Windowed);
                NativeToggleSettings();
            });
            NSpacer(card, 15);
            NButton(card, "CLOSE", NativeToggleSettings);
        }

        private void NativeTick()
        {
            if (nativeRoot == null) return;
            bool editing = nativeRoot.focusController != null &&
                nativeRoot.focusController.focusedElement is TextField;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (nativeModal != null) NativeToggleSettings();
                else if (page == Page.Rhythm || page == Page.Listen || page == Page.FreePlay)
                    ChangePage(Page.Library);
            }
            if (!editing && nativeModal == null && (page == Page.Rhythm || page == Page.Listen) &&
                Input.GetKeyDown(KeyCode.Space))
            {
                if (player.IsPaused) player.ResumeSong();
                else player.PauseSong();
            }
            if (!editing && nativeModal == null && page == Page.Rhythm)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (Input.GetKeyDown(LaneKeys[i]) && nativePressed.Add(i)) HitLane(i);
                    if (Input.GetKeyUp(LaneKeys[i])) { nativePressed.Remove(i); ReleaseLane(i); }
                }
            }
            if (!editing && nativeModal == null && page == Page.FreePlay)
                for (int i = 0; i < PianoKeys.Length; i++)
                    if (Input.GetKeyDown(PianoKeys[i])) PlayPianoKey(i);

            if (page == Page.Rhythm && chart != null)
            {
                if (nativeStats != null) nativeStats.text = "SCORE " + score + "  ·  COMBO " + combo +
                    "  ·  LIFE " + Mathf.Max(0,life).ToString("0") + "%";
                if (nativeJudgement != null)
                    nativeJudgement.text = Time.unscaledTime < judgementUntil ? lastJudgement : "";
                if (nativeProgressBar != null && chart.DurationSeconds > 0)
                    nativeProgressBar.style.width = Length.Percent(Mathf.Clamp01((float)(player.Position / chart.DurationSeconds)) * 100);
                NativeDrawNotes();
            }
            if (page == Page.Listen && selected != null)
            {
                double current = Math.Max(0, Math.Min(selected.DurationSeconds, player.Position));
                if (nativeProgressBar != null && selected.DurationSeconds > 0)
                    nativeProgressBar.style.width = Length.Percent((float)(current / selected.DurationSeconds * 100));
                if (nativeClock != null) nativeClock.text =
                    TimeSpan.FromSeconds(current).ToString(@"mm\:ss") + " / " +
                    TimeSpan.FromSeconds(selected.DurationSeconds).ToString(@"mm\:ss");
            }
            if (Time.frameCount % 50 == 0) NativeFooter();
        }
    }
}
