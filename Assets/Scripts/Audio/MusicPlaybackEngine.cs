using System;
using System.Collections.Generic;
using UnityEngine;
using NoteScriptGame.Music;

namespace NoteScriptGame.Audio
{
    /// <summary>Local standalone synthesis and DSP-timed playback; no NoteScript process, API or browser.</summary>
    public sealed class MusicPlaybackEngine : MonoBehaviour
    {
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
        private readonly List<MusicEvent> ordered = new List<MusicEvent>();
        private MusicSong song;
        private double startDsp;
        private double startOffset;
        private double pausedPosition;
        private int nextEvent;
        private int nextVoice;
        private bool playing;
        private bool paused;
        private float gain = 0.40f;
        private const double LookAhead = 0.22;
        private const double SafeAudioLead = 0.04;

        public bool IsPlaying { get { return playing; } }
        public bool IsPaused { get { return paused; } }
        public double Position
        {
            get { return paused ? pausedPosition : playing ? startOffset + AudioSettings.dspTime - startDsp : 0.0; }
        }
        public float Volume { get { return gain; } set { gain = Mathf.Clamp01(value); } }
        public MusicSong Song { get { return song; } }

        private void Awake()
        {
            for (int i = 0; i < 64; i++)
            {
                var go = new GameObject("SynthVoice_" + i);
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0;
                source.dopplerLevel = 0;
                voices.Add(source);
            }
        }

        public void StartSong(MusicSong next, double preRollSeconds = 0.25)
        {
            StopSong();
            if (next == null) return;
            song = next;
            ordered.AddRange(song.OrderedEvents());
            startOffset = 0;
            startDsp = AudioSettings.dspTime + Math.Max(0.2, preRollSeconds);
            nextEvent = 0;
            playing = true;
            paused = false;
        }

        public void StopSong()
        {
            foreach (var voice in voices) voice.Stop();
            ordered.Clear();
            song = null;
            playing = false;
            paused = false;
            nextEvent = 0;
            pausedPosition = 0;
        }

        public void PauseSong()
        {
            if (!playing || paused) return;
            pausedPosition = Math.Max(0, Position);
            foreach (var voice in voices) voice.Stop();
            paused = true;
        }

        public void ResumeSong()
        {
            if (!paused || song == null) return;
            paused = false;
            startOffset = pausedPosition;
            startDsp = AudioSettings.dspTime + 0.25;
            nextEvent = 0;
            while (nextEvent < ordered.Count && ordered[nextEvent].TimeSeconds < startOffset - 0.001)
                nextEvent++;
        }

        public void Seek(double seconds)
        {
            if (song == null) return;
            double target = Math.Max(0, Math.Min(song.DurationSeconds, seconds));
            foreach (var voice in voices) voice.Stop();
            nextEvent = 0;
            while (nextEvent < ordered.Count && ordered[nextEvent].TimeSeconds < target - 0.001) nextEvent++;
            startOffset = target;
            startDsp = AudioSettings.dspTime + 0.25;
            if (paused) pausedPosition = target;
        }

        public void PlayHit(MusicEvent ev)
        {
            if (ev == null || song == null || ev.TrackIndex < 0 || ev.TrackIndex >= song.Tracks.Count) return;
            var track = song.Tracks[ev.TrackIndex];
            var clip = GetClip(ev, track);
            if (clip == null) return;
            var source = NextVoice();
            source.clip = clip;
            source.volume = Mathf.Clamp01(gain * track.Volume * ev.Velocity / 127f * 1.20f);
            source.panStereo = track.Pan;
            source.Play();
        }

        private void Update()
        {
            if (!playing || paused || song == null) return;
            double now = Position;
            double limit = now + LookAhead;
            while (nextEvent < ordered.Count && ordered[nextEvent].TimeSeconds <= limit)
            {
                var ev = ordered[nextEvent++];
                if (ev.TimeSeconds < now - 0.08) continue;
                var track = song.Tracks[ev.TrackIndex];
                var clip = GetClip(ev, track);
                if (clip == null) continue;
                var source = NextVoice();
                source.clip = clip;
                source.volume = Mathf.Clamp01(gain * track.Volume * ev.Velocity / 127f * 0.75f);
                source.panStereo = track.Pan;
                double scheduled = startDsp + ev.TimeSeconds - startOffset;
                if (scheduled <= AudioSettings.dspTime + SafeAudioLead)
                    source.Play();
                else
                    source.PlayScheduled(scheduled);
            }
        }

        private AudioSource NextVoice()
        {
            AudioSource voice = voices[nextVoice++ % voices.Count];
            voice.Stop();
            return voice;
        }

        private AudioClip GetClip(MusicEvent ev, MusicTrack track)
        {
            var sound = track.Sound.ToLowerInvariant();
            int bucket = Mathf.CeilToInt((float)Math.Min(6, ev.DurationSeconds) * 8);
            string key = sound + "|" + (ev.IsTrigger ? ev.Trigger : ev.MidiNote.ToString()) + "|" + bucket;
            AudioClip result;
            if (clipCache.TryGetValue(key, out result)) return result;

            const int sampleRate = 44100;
            double duration = Math.Max(0.12, bucket / 8.0);
            bool drum = ev.IsTrigger;
            double tail = drum ? 0.12 : 0.16;
            int samples = Mathf.CeilToInt((float)((duration + tail) * sampleRate));
            float[] data = new float[samples];
            double hz = 440.0 * Math.Pow(2.0, (ev.MidiNote - 69) / 12.0);
            uint random = 2166136261u;
            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)sampleRate;
                double release = Math.Min(1.0, Math.Max(0, (duration + tail - t) / tail));
                double attack = Math.Min(1.0, t * (sound.Contains("pad") || sound.Contains("strings") ? 17 : 400));
                double v;
                if (drum)
                {
                    random = unchecked(random * 1664525u + 1013904223u);
                    double noise = ((random >> 8) & 65535) / 32768.0 - 1.0;
                    if (ev.Trigger.Contains("kick"))
                        v = Math.Sin(2 * Math.PI * (80 * t - 36 * t * t)) * Math.Exp(-16 * t);
                    else if (ev.Trigger.Contains("hat") || ev.Trigger.Contains("shaker") || ev.Trigger.Contains("ride") || ev.Trigger.Contains("crash"))
                        v = noise * Math.Exp(-20 * t) * 0.40;
                    else if (ev.Trigger.Contains("snare") || ev.Trigger.Contains("clap"))
                        v = (noise * 0.65 + Math.Sin(2 * Math.PI * 170 * t) * 0.18) * Math.Exp(-15 * t);
                    else
                        v = noise * Math.Exp(-13 * t) * 0.30;
                }
                else if (sound.Contains("bass"))
                    v = (Math.Sin(2 * Math.PI * hz * t) + 0.22 * Math.Sin(2 * Math.PI * 2 * hz * t)) * Math.Exp(-1.7 * t) * 0.64;
                else if (sound.Contains("pluck") || sound.Contains("guitar") || sound.Contains("marimba"))
                    v = (Math.Sin(2 * Math.PI * hz * t) + 0.35 * Math.Sin(2 * Math.PI * 2 * hz * t)) * Math.Exp(-7 * t) * 0.55;
                else if (sound.Contains("pad") || sound.Contains("strings") || sound.Contains("choir"))
                    v = (Math.Sin(2 * Math.PI * hz * t) + 0.33 * Math.Sin(2 * Math.PI * 2 * hz * t)) * 0.38;
                else if (sound.Contains("organ"))
                    v = (Math.Sin(2 * Math.PI * hz * t) + 0.32 * Math.Sin(2 * Math.PI * 3 * hz * t)) * 0.43;
                else if (sound.Contains("flute"))
                    v = (Math.Sin(2 * Math.PI * hz * t) + 0.08 * Math.Sin(2 * Math.PI * 2 * hz * t)) * 0.5;
                else
                    v = (Math.Sin(2 * Math.PI * hz * t) + 0.24 * Math.Sin(2 * Math.PI * 2 * hz * t) + 0.10 * Math.Sin(2 * Math.PI * 3 * hz * t)) * Math.Exp(-3 * t) * 0.55;

                data[i] = Mathf.Clamp((float)(v * attack * release), -1, 1);
            }
            result = AudioClip.Create("Synth_" + key, samples, 1, sampleRate, false);
            result.SetData(data, 0);
            if (clipCache.Count > 1000) clipCache.Clear();
            clipCache[key] = result;
            return result;
        }

        private void OnDestroy()
        {
            StopSong();
            foreach (AudioClip clip in clipCache.Values) if (clip != null) Destroy(clip);
            clipCache.Clear();
        }
    }
}
