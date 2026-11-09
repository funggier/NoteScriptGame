using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NoteScriptGame.Music;
using UnityEngine;
using UnityEngine.UIElements;

namespace NoteScriptGame.Game
{
    /// <summary>
    /// Piano-roll and text-editor views share the original canonical MusicSong.
    /// Edits are in memory until Save .MD; other tracks remain byte-for-byte unchanged.
    /// </summary>
    public sealed partial class NoteScriptGameApp
    {
        private const int RollBarsPerPage = 4;
        private const int RollRuler = 36;
        private const int RollRowHeight = 23;
        private const float RollPixelsPerQuarter = 72f;
        private const float RollLabelWidth = 66f;
        private const double RollSnap = .25;
        private const double RollTolerance = 0.000001;

        private MusicSong rollSong;
        private string rollBaseText;
        private MusicEvent rollSelected;
        private int rollTrack;
        private int rollStartBar = 1;
        private int rollBottomPitch = 48;
        private double rollInsertDuration = 1;
        private bool rollGraphical = true;
        private readonly HashSet<int> rollDirtyBars = new HashSet<int>();
        private VisualElement rollGrid;
        private Label rollDetail;
        private Label rollMessage;
        private sealed class RollHistory
        {
            public MusicSong Song;
            public HashSet<int> ChangedBars;
            public int SelectedIndex;
        }
        private readonly Stack<RollHistory> rollUndo = new Stack<RollHistory>();
        private readonly Stack<RollHistory> rollRedo = new Stack<RollHistory>();

        private RollHistory RollCapture()
        {
            return new RollHistory
            {
                Song = MarkdownPianoRollEditor.Clone(rollSong),
                ChangedBars = new HashSet<int>(rollDirtyBars),
                SelectedIndex = rollSelected == null ? -1 : rollSong.Events.IndexOf(rollSelected)
            };
        }

        private void RollRestore(RollHistory snapshot)
        {
            rollSong = snapshot.Song;
            rollDirtyBars.Clear();
            foreach (int bar in snapshot.ChangedBars) rollDirtyBars.Add(bar);
            rollSelected = snapshot.SelectedIndex >= 0 && snapshot.SelectedIndex < rollSong.Events.Count
                ? rollSong.Events[snapshot.SelectedIndex] : null;
        }

        private void RollRemember(RollHistory before)
        {
            rollUndo.Push(before);
            // Retain only the most recent 24 changes to bound memory on large imported songs.
            if (rollUndo.Count > 24)
            {
                RollHistory[] recent = rollUndo.ToArray();
                rollUndo.Clear();
                for (int i = 23; i >= 0; i--) rollUndo.Push(recent[i]);
            }
            rollRedo.Clear();
        }

        private void RollHistoryStep(bool undo)
        {
            Stack<RollHistory> source = undo ? rollUndo : rollRedo;
            Stack<RollHistory> target = undo ? rollRedo : rollUndo;
            if (rollSong == null || source.Count == 0)
            {
                status = undo ? "No more edits to undo." : "No more edits to redo.";
                NativeFooter();
                return;
            }
            target.Push(RollCapture());
            RollRestore(source.Pop());
            status = undo ? "Undo applied (not yet saved)." : "Redo applied (not yet saved).";
            NativeBuildPage();
        }

        private void RollResetHistory()
        {
            rollUndo.Clear();
            rollRedo.Clear();
        }


        private void BuildStudioWorkspace()
        {
            var content = NContent();
            content.style.paddingTop = 12;
            NText(content, "MUSIC STUDIO", 26, true);
            var toolbar = NRow(content);
            NButton(toolbar, "PIANO ROLL", () =>
            {
                if (!EnsureRollSong()) { NativeFooter(); return; }
                rollGraphical = true;
                NativeBuildPage();
            }, rollGraphical);
            NButton(toolbar, "MARKDOWN SOURCE", () =>
            {
                if (!RollFlushToText()) { NativeFooter(); return; }
                player.StopSong();
                rollGraphical = false;
                NativeBuildPage();
            }, !rollGraphical);
            NButton(toolbar, "PREVIEW SONG", RollPreview, true);
            NButton(toolbar, "STOP", () => player.StopSong());
            NButton(toolbar, "SAVE .MD", RollSave, true);
            NButton(toolbar, "UNDO", () => RollHistoryStep(true));
            NButton(toolbar, "REDO", () => RollHistoryStep(false));
            NButton(toolbar, "NEW SONG", () =>
            {
                if (RollGuardLeaving(() => RollNewSongWithoutPrompt())) return;
                RollNewSongWithoutPrompt();
            });
            if (!rollGraphical)
            {
                RollSourceView(content);
                return;
            }
            if (!EnsureRollSong())
            {
                rollGraphical = false;
                RollSourceView(content);
                return;
            }
            RollGridView(content);
        }

        private void RollNewSongWithoutPrompt()
        {
            player.StopSong();
            NewStudioSong();
            rollSong = null;
            rollBaseText = null;
            rollTrack = 0;
            rollSelected = null;
            rollStartBar = 1;
            rollDirtyBars.Clear();
            RollResetHistory();
            rollGraphical = true;
            NativeBuildPage();
        }

        private bool RollHasUnsavedDraft()
        {
            string baseline = selected != null ? selected.OriginalMarkdown : "";
            return rollDirtyBars.Count > 0 ||
                !string.Equals(studioText, baseline, StringComparison.Ordinal);
        }

        /// <summary>Returns true when leaving was intercepted by the unsaved-changes dialog.</summary>
        private bool RollGuardLeaving(Action continueAction)
        {
            if (!RollHasUnsavedDraft() || nativeRoot == null) return false;
            if (nativeModal != null) return true;
            nativeModal = new VisualElement();
            nativeModal.style.position = Position.Absolute;
            nativeModal.style.left = 0;
            nativeModal.style.top = 0;
            nativeModal.style.right = 0;
            nativeModal.style.bottom = 0;
            nativeModal.style.backgroundColor = new Color(0,0,0,.82f);
            nativeModal.style.justifyContent = Justify.Center;
            nativeModal.style.alignItems = Align.Center;
            nativeRoot.Add(nativeModal);
            var modalCard = NCard(nativeModal);
            modalCard.style.width = Length.Percent(88);
            modalCard.style.maxWidth = 520;
            NText(modalCard, "UNSAVED MUSIC EDITS", 23, true);
            NText(modalCard,
                "Your current Piano Roll or Markdown changes are not saved to UserSongs. Choose what to do before leaving.",
                15, false, UiMute);
            NButton(modalCard, "KEEP EDITING", () =>
            {
                nativeModal.RemoveFromHierarchy();
                nativeModal = null;
            });
            NButton(modalCard, "SAVE .MD THEN CONTINUE", () =>
            {
                RollSave();
                if (!status.StartsWith("Saved:", StringComparison.Ordinal)) return;
                if (nativeModal != null)
                {
                    nativeModal.RemoveFromHierarchy();
                    nativeModal = null;
                }
                continueAction();
            }, true);
            NButton(modalCard, "DISCARD CHANGES", () =>
            {
                studioText = selected == null ? "" : selected.OriginalMarkdown;
                rollSong = null;
                rollBaseText = null;
                rollSelected = null;
                rollDirtyBars.Clear();
                RollResetHistory();
                nativeModal.RemoveFromHierarchy();
                nativeModal = null;
                continueAction();
            });
            return true;
        }

        private bool EnsureRollSong()
        {
            if (rollSong != null && string.Equals(rollBaseText, studioText, StringComparison.Ordinal)) return true;
            MusicImportResult parsed = NoteScriptMarkdownParser.Parse(studioText);
            if (!parsed.Success)
            {
                status = "Piano Roll cannot open invalid Markdown: " + parsed.Message;
                return false;
            }
            rollSong = MarkdownPianoRollEditor.Clone(parsed.Song);
            rollBaseText = studioText;
            RollResetHistory();
            rollSelected = null;
            rollDirtyBars.Clear();
            rollTrack = Mathf.Clamp(rollTrack, 0, rollSong.Tracks.Count - 1);
            return true;
        }

        private void RollSourceView(VisualElement content)
        {
            NText(content, "Editable text source  ·  New .md songs can be played directly", 13, false, UiMute);
            var columns = NRow(content);
            columns.style.flexGrow = 1;
            columns.style.minHeight = 0;
            var editorCard = NCard(columns);
            editorCard.style.flexBasis = 720;
            editorCard.style.flexGrow = 3;
            editorCard.style.minWidth = 360;
            editorCard.style.minHeight = 410;
            editorCard.style.marginRight = 12;
            editorCard.style.marginBottom = 10;
            nativeEditor = new TextField();
            nativeEditor.multiline = true;
            nativeEditor.value = studioText;
            nativeEditor.style.fontSize = 16;
            nativeEditor.style.flexGrow = 1;
            nativeEditor.style.minHeight = 380;
            nativeEditor.RegisterValueChangedCallback(e =>
            {
                studioText = e.newValue;
                rollSong = null;
            });
            editorCard.Add(nativeEditor);

            var control = NCard(columns);
            control.style.flexGrow = 1;
            control.style.flexBasis = 290;
            control.style.minWidth = 240;
            NText(control, "SAVE AS", 16, true);
            nativeFilename = new TextField();
            nativeFilename.value = studioFileName;
            nativeFilename.RegisterValueChangedCallback(e => studioFileName = e.newValue);
            nativeFilename.style.marginTop = 7;
            nativeFilename.style.marginBottom = 16;
            control.Add(nativeFilename);
            NButton(control, "VALIDATE", () => { ValidateStudio(false); NativeFooter(); });
            NButton(control, "SAVE .MD", RollSave, true);
            NButton(control, "OPEN PIANO ROLL", () =>
            {
                if (!EnsureRollSong()) { NativeFooter(); return; }
                rollGraphical = true;
                NativeBuildPage();
            });
            NText(control, "Save creates or updates a user song under UserSongs, without writing to the bundled song file.", 13, false, UiMute);
        }

        private void RollGridView(VisualElement content)
        {
            string trackName = rollSong.Tracks[rollTrack].Name;
            var top = NRow(content);
            NText(top, "TRACK   " + (rollTrack + 1) + "/" + rollSong.Tracks.Count + "   " + trackName,
                16, true, UiAccent);
            NButton(top, "PREVIOUS TRACK", () => RollChangeTrack(-1));
            NButton(top, "NEXT TRACK", () => RollChangeTrack(1));
            NButton(top, "◀ BARS", () => { rollStartBar = Mathf.Max(1, rollStartBar - RollBarsPerPage); NativeBuildPage(); });
            NText(top, "BARS " + rollStartBar + "–" + (rollStartBar + RollBarsPerPage - 1), 16, true);
            NButton(top, "BARS ▶", () =>
            {
                rollStartBar = Mathf.Min(9996, rollStartBar + RollBarsPerPage);
                NativeBuildPage();
            });
            NButton(top, "OCTAVE DOWN", () => { rollBottomPitch = Mathf.Max(12, rollBottomPitch - 12); NativeBuildPage(); });
            NButton(top, "OCTAVE UP", () => { rollBottomPitch = Mathf.Min(84, rollBottomPitch + 12); NativeBuildPage(); });
            NText(content, "Click empty grid to insert · click note to select · drag note to move · 1/16-note snap",
                13, false, UiMute);

            var columns = NRow(content);
            columns.style.flexGrow = 1;
            columns.style.minHeight = 0;
            columns.style.marginTop = 4;

            var left = NCard(columns);
            left.style.flexBasis = 235;
            left.style.minWidth = 220;
            left.style.flexGrow = 0;
            left.style.marginRight = 12;
            left.style.marginBottom = 9;
            NText(left, "NOTE TOOLS", 16, true);
            var durationRow = NRow(left);
            NText(durationRow, "Insert duration", 13, false, UiMute);
            NButton(durationRow, DurationName(rollInsertDuration), () =>
            {
                var options = new[] { .25, .5, 1.0, 2.0, 4.0 };
                int i = Array.FindIndex(options, n => Math.Abs(n - rollInsertDuration) < RollTolerance);
                rollInsertDuration = options[(i + 1) % options.Length];
                NativeBuildPage();
            });
            rollDetail = NText(left, RollSelectionText(), 13, false, UiMute);
            NSpacer(left, 10);
            var transpose = NRow(left);
            NButton(transpose, "PITCH +1", () => RollTransform(note => note.MidiNote++, "Pitch raised"));
            NButton(transpose, "PITCH -1", () => RollTransform(note => note.MidiNote--, "Pitch lowered"));
            var times = NRow(left);
            NButton(times, "← 1/16", () => RollTransform(note => note.StartQuarter -= RollSnap, "Moved earlier"));
            NButton(times, "1/16 →", () => RollTransform(note => note.StartQuarter += RollSnap, "Moved later"));
            var durations = NRow(left);
            NButton(durations, "SHORTER", () => RollChangeDuration(false));
            NButton(durations, "LONGER", () => RollChangeDuration(true));
            NSpacer(left, 8);
            NButton(left, "DELETE NOTE", RollDeleteSelected);
            NSpacer(left, 15);
            rollMessage = NText(left, "Changes are in memory until Save .MD.", 12, false, UiMute);

            var right = NCard(columns);
            right.style.flexBasis = 820;
            right.style.flexGrow = 1;
            right.style.minWidth = 360;
            right.style.marginBottom = 9;
            right.style.paddingLeft = 9;
            right.style.paddingRight = 9;
            right.style.paddingTop = 8;
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 430;
            right.Add(scroll);

            double barSize = MarkdownPianoRollEditor.BarQuarterLength(rollSong);
            double firstQuarter = (rollStartBar - 1) * barSize;
            float width = RollLabelWidth + RollBarsPerPage * (float)barSize * RollPixelsPerQuarter;
            float height = RollRuler + (37 * RollRowHeight);
            rollGrid = new VisualElement();
            rollGrid.style.position = Position.Relative;
            rollGrid.style.width = width;
            rollGrid.style.height = height;
            rollGrid.style.flexShrink = 0;
            rollGrid.style.backgroundColor = new Color(.04f,.06f,.10f);
            scroll.Add(rollGrid);

            for (int row = 0; row < 37; row++)
            {
                int midi = rollBottomPitch + 36 - row;
                float y = RollRuler + row * RollRowHeight;
                var bg = new VisualElement();
                bg.style.position = Position.Absolute;
                bg.style.top = y;
                bg.style.left = 0;
                bg.style.width = width;
                bg.style.height = RollRowHeight;
                bool black = new[] {1,3,6,8,10}.Contains(midi % 12);
                bg.style.backgroundColor = black ? new Color(.046f,.073f,.112f) : new Color(.071f,.10f,.148f);
                bg.style.borderBottomWidth = .5f;
                bg.style.borderBottomColor = UiBorder;
                bg.pickingMode = PickingMode.Ignore;
                rollGrid.Add(bg);
                var note = NText(rollGrid, MarkdownPianoRollEditor.ToPitch(midi), 12, midi % 12 == 0,
                    midi % 12 == 0 ? UiAccent : UiMute);
                note.style.position = Position.Absolute;
                note.style.left = 6;
                note.style.top = y + 2;
                note.style.width = RollLabelWidth - 7;
                note.pickingMode = PickingMode.Ignore;
            }
            for (int bar = 0; bar < RollBarsPerPage; bar++)
            {
                double quarterAtBar = bar * barSize;
                float x = RollLabelWidth + (float)quarterAtBar * RollPixelsPerQuarter;
                var name = NText(rollGrid, "BAR " + (rollStartBar + bar), 12, true, UiAccent);
                name.style.position = Position.Absolute;
                name.style.top = 7;
                name.style.left = x + 5;
                name.pickingMode = PickingMode.Ignore;
            }
            int steps = Mathf.RoundToInt((float)(RollBarsPerPage * barSize / RollSnap));
            for (int i = 0; i <= steps; i++)
            {
                double quarter = i * RollSnap;
                float x = RollLabelWidth + (float)quarter * RollPixelsPerQuarter;
                bool isBar = Math.Abs((quarter / barSize) - Math.Round(quarter / barSize)) < RollTolerance;
                bool isBeat = Math.Abs(quarter - Math.Round(quarter)) < RollTolerance;
                var rule = new VisualElement();
                rule.style.position = Position.Absolute;
                rule.style.left = x;
                rule.style.top = RollRuler;
                rule.style.height = height - RollRuler;
                rule.style.width = isBar ? 2 : 1;
                rule.style.backgroundColor = isBar ? UiAccent : isBeat ? new Color(.32f,.40f,.52f) :
                    new Color(.13f,.19f,.27f);
                rule.pickingMode = PickingMode.Ignore;
                rollGrid.Add(rule);
            }
            // A blank click creates one MIDI note at a quantized onset.
            rollGrid.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                RollInsertAt(e.localPosition, firstQuarter, barSize);
                e.StopPropagation();
            });

            foreach (MusicEvent ev in rollSong.Events.Where(n => n.TrackIndex == rollTrack && !n.IsTrigger))
            {
                if (ev.MidiNote < rollBottomPitch || ev.MidiNote > rollBottomPitch + 36) continue;
                double x = ev.StartQuarter - firstQuarter;
                if (x < -RollTolerance || x >= RollBarsPerPage * barSize - RollTolerance) continue;
                float px = RollLabelWidth + (float)x * RollPixelsPerQuarter;
                float py = RollRuler + (rollBottomPitch + 36 - ev.MidiNote) * RollRowHeight;
                float noteWidth = Mathf.Max(13, (float)ev.DurationQuarter * RollPixelsPerQuarter - 2);
                var element = new VisualElement();
                element.style.position = Position.Absolute;
                element.style.left = px + 1;
                element.style.top = py + 1;
                element.style.width = noteWidth;
                element.style.height = RollRowHeight - 2;
                bool chosen = ev == rollSelected;
                element.style.backgroundColor = chosen ? new Color(1f,.77f,.27f) : UiAccent;
                element.style.borderBottomWidth = 2;
                element.style.borderBottomColor = chosen ? Color.white : new Color(.21f,.45f,.57f);
                element.style.justifyContent = Justify.Center;
                NText(element, MarkdownPianoRollEditor.ToPitch(ev.MidiNote), 11, true,
                    chosen ? new Color(.09f,.13f,.20f) : new Color(.02f,.07f,.11f)).pickingMode = PickingMode.Ignore;
                rollGrid.Add(element);
                Vector2 pointerOrigin = Vector2.zero;
                element.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    rollSelected = ev;
                    pointerOrigin = e.position;
                    element.CapturePointer(e.pointerId);
                    if (rollDetail != null) rollDetail.text = RollSelectionText();
                    e.StopPropagation();
                });
                element.RegisterCallback<PointerUpEvent>(e =>
                {
                    if (element.HasPointerCapture(e.pointerId)) element.ReleasePointer(e.pointerId);
                    if (Vector2.Distance(pointerOrigin, e.position) >= 5)
                    {
                        Vector2 local = rollGrid.WorldToLocal(e.position);
                        int pitch = rollBottomPitch + 36 - Mathf.FloorToInt((local.y - RollRuler) / RollRowHeight);
                        double q = firstQuarter + Math.Floor(Math.Max(0,local.x - RollLabelWidth) /
                            (RollPixelsPerQuarter * RollSnap)) * RollSnap;
                        if (pitch >= rollBottomPitch && pitch <= rollBottomPitch + 36 &&
                            q >= firstQuarter && q < firstQuarter + RollBarsPerPage * barSize)
                            RollTransform(note => { note.MidiNote = pitch; note.StartQuarter = q; }, "Moved note");
                    }
                    else NativeBuildPage();
                    e.StopPropagation();
                });
                element.RegisterCallback<PointerCaptureOutEvent>(_ => { });
            }

            if (rollDirtyBars.Count > 0)
                NText(content, "UNSAVED CHANGES: " + rollDirtyBars.Count +
                    " modified bar(s)  ·  Other tracks and song comments will be preserved.", 13, true, UiAccent);
            else NText(content, "Source .md is unchanged until Save .MD.", 12, false, UiMute);
        }

        private string RollSelectionText()
        {
            if (rollSelected == null) return "No note selected. Click a note or click an empty cell to add.";
            return MarkdownPianoRollEditor.ToPitch(rollSelected.MidiNote) +
                "  |  Bar " + MarkdownPianoRollEditor.BarNumber(rollSong, rollSelected.StartQuarter) +
                "  |  Quarter " + rollSelected.StartQuarter.ToString("F2", CultureInfo.InvariantCulture) +
                "  |  Length " + rollSelected.DurationQuarter.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string DurationName(double value)
        {
            if (Math.Abs(value - .25) < RollTolerance) return "1/16";
            if (Math.Abs(value - .5) < RollTolerance) return "1/8";
            if (Math.Abs(value - 1) < RollTolerance) return "1/4";
            if (Math.Abs(value - 2) < RollTolerance) return "1/2";
            return "WHOLE";
        }

        private void RollInsertAt(Vector2 click, double startQuarter, double barSize)
        {
            if (rollSong == null || click.x < RollLabelWidth || click.y < RollRuler) return;
            int row = Mathf.FloorToInt((click.y - RollRuler) / RollRowHeight);
            if (row < 0 || row > 36) return;
            int pitch = rollBottomPitch + 36 - row;
            double q = startQuarter + Math.Floor((click.x - RollLabelWidth) /
                (RollPixelsPerQuarter * RollSnap)) * RollSnap;
            if (q < startQuarter || q >= startQuarter + barSize * RollBarsPerPage) return;
            int bar = MarkdownPianoRollEditor.BarNumber(rollSong, q);
            double barEnd = bar * barSize;
            double length = Math.Min(rollInsertDuration, barEnd - q);
            // The current C# Markdown parser supports only canonical duration values.
            // Insertion near the end of a bar always falls back to one sixteenth if necessary.
            if (length + RollTolerance < rollInsertDuration) length = .25;
            var ev = new MusicEvent
            {
                TrackIndex = rollTrack,
                MidiNote = pitch,
                StartQuarter = q,
                DurationQuarter = length,
                Velocity = rollSong.Tracks[rollTrack].Velocity
            };
            RollHistory insertSnapshot = RollCapture();
            rollSong.Events.Add(ev);
            string error;
            if (!MarkdownPianoRollEditor.ValidateBar(rollSong, rollTrack, bar, out error))
            {
                rollSong.Events.Remove(ev);
                status = "Cannot add note: " + error;
                NativeFooter();
                return;
            }
            RollRemember(insertSnapshot);
            MarkdownPianoRollEditor.RefreshTiming(rollSong);
            rollDirtyBars.Add(bar);
            rollSelected = ev;
            status = "Added " + MarkdownPianoRollEditor.ToPitch(pitch) + " to bar " + bar;
            NativeBuildPage();
        }

        private void RollTransform(Action<MusicEvent> transformation, string description)
        {
            if (rollSelected == null || rollSong == null) { status = "Select a note first."; NativeFooter(); return; }
            MusicEvent note = rollSelected;
            int oldPitch = note.MidiNote;
            double oldStart = note.StartQuarter, oldLength = note.DurationQuarter;
            int oldBar = MarkdownPianoRollEditor.BarNumber(rollSong, oldStart);
            RollHistory moveSnapshot = RollCapture();
            transformation(note);
            int newBar = MarkdownPianoRollEditor.BarNumber(rollSong, note.StartQuarter);
            string error = null;
            if (note.MidiNote < 0 || note.MidiNote > 127 || note.StartQuarter < 0 ||
                newBar > 10000 || !MarkdownPianoRollEditor.ValidateBar(rollSong, rollTrack, oldBar, out error) ||
                !MarkdownPianoRollEditor.ValidateBar(rollSong, rollTrack, newBar, out error))
            {
                note.MidiNote = oldPitch;
                note.StartQuarter = oldStart;
                note.DurationQuarter = oldLength;
                status = "Edit rejected: " + (error ?? "Pitch or time outside supported limits.");
                NativeFooter();
                return;
            }
            RollRemember(moveSnapshot);
            rollDirtyBars.Add(oldBar);
            rollDirtyBars.Add(newBar);
            MarkdownPianoRollEditor.RefreshTiming(rollSong);
            status = description + " (not yet saved).";
            NativeBuildPage();
        }

        private void RollChangeDuration(bool longer)
        {
            if (rollSelected == null) { status = "Select a note first."; NativeFooter(); return; }
            var durations = new[] { .25, .5, .75, 1.0, 1.5, 2.0, 3.0, 4.0, 6.0 };
            int index = Array.FindIndex(durations, d => Math.Abs(d - rollSelected.DurationQuarter) < RollTolerance);
            if (index < 0) index = 3;
            index = Mathf.Clamp(index + (longer ? 1 : -1), 0, durations.Length - 1);
            double value = durations[index];
            RollTransform(n => n.DurationQuarter = value, longer ? "Lengthened note" : "Shortened note");
        }

        private void RollDeleteSelected()
        {
            if (rollSelected == null || rollSong == null) { status = "Select a note first."; NativeFooter(); return; }
            MusicEvent note = rollSelected;
            int bar = MarkdownPianoRollEditor.BarNumber(rollSong, note.StartQuarter);
            RollHistory deleteSnapshot = RollCapture();
            rollSong.Events.Remove(note);
            string error;
            if (!MarkdownPianoRollEditor.ValidateBar(rollSong, rollTrack, bar, out error))
            {
                rollSong.Events.Add(note);
                status = "Cannot delete: " + error;
            }
            else
            {
                RollRemember(deleteSnapshot);
                rollDirtyBars.Add(bar);
                rollSelected = null;
                MarkdownPianoRollEditor.RefreshTiming(rollSong);
                status = "Note deleted (not yet saved).";
            }
            NativeBuildPage();
        }

        private bool RollFlushToText()
        {
            if (rollSong == null || rollDirtyBars.Count == 0) return true;
            string replacement, error;
            if (!MarkdownPianoRollEditor.TryBuildMarkdown(studioText, rollSong, rollTrack,
                rollDirtyBars, out replacement, out error))
            {
                status = "Piano Roll cannot write .md: " + error;
                return false;
            }
            studioText = replacement;
            rollBaseText = replacement;
            rollSong = MarkdownPianoRollEditor.Clone(NoteScriptMarkdownParser.Parse(replacement).Song);
            rollDirtyBars.Clear();
            RollResetHistory();
            rollSelected = null;
            status = "Piano Roll edits transferred to Markdown in memory. Use SAVE .MD to persist.";
            NativeFooter();
            return true;
        }

        private void RollChangeTrack(int increment)
        {
            if (rollSong == null || !RollFlushToText()) { NativeFooter(); return; }
            rollTrack = (rollTrack + increment + rollSong.Tracks.Count) % rollSong.Tracks.Count;
            rollSelected = null;
            NativeBuildPage();
        }

        private void RollPreview()
        {
            if (!RollFlushToText()) { NativeFooter(); return; }
            MusicImportResult parsed = NoteScriptMarkdownParser.Parse(studioText);
            if (!parsed.Success) { status = parsed.Message; NativeFooter(); return; }
            player.StopSong();
            player.Volume = .60f;
            player.StartSong(parsed.Song, .25);
            status = "Previewing " + parsed.Song.Title + " locally. STOP ends the preview.";
            NativeFooter();
        }

        private void RollSave()
        {
            if (!RollFlushToText()) { NativeFooter(); return; }
            player.StopSong();
            ValidateStudio(true);
            rollBaseText = null; // Reparse updated file on next Piano Roll entry.
            rollSong = null;
            NativeFooter();
            NativeBuildPage();
        }
    }
}
