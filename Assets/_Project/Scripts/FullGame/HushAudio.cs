using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HushBlocks.FullGame
{
    /// <summary>
    /// Procedural audio for Hush Blocks. Music loops, the metronome click and
    /// every sound effect are synthesized at boot; no audio assets are needed.
    /// Gameplay music and clicks are scheduled on the DSP clock so the visual
    /// beat, scoring window and audio all share one source of truth.
    /// </summary>
    public sealed class HushAudio : MonoBehaviour
    {
        public enum SfxId
        {
            UiClick,
            UiBack,
            Place,
            Perfect,
            Good,
            Offbeat,
            Miss,
            Countdown,
            Go,
            LevelClear,
            GameOver,
            Star,
            HeartLost
        }

        private const int SampleRate = 44100;
        private const int BeatsPerLoop = 16;

        public const int MenuBpm = 72;
        private const float MenuBeatDuration = 60f / MenuBpm;

        private static HushAudio instance;

        private readonly Dictionary<SfxId, AudioClip> effects = new();
        private readonly List<AudioSource> sfxPool = new();
        private readonly AudioSource[] musicSources = new AudioSource[2];

        private AudioSource clickSource;
        private AudioClip menuLoop;
        private readonly AudioClip[] districtLoops = new AudioClip[DistrictCatalog.Count];
        private readonly AudioClip[] districtClicks = new AudioClip[DistrictCatalog.Count];
        private int activeMusicSource;
        private Coroutine musicFade;
        private Coroutine pendingMusic;
        private float menuFade = 1f;
        private bool paused;
        private bool preloaded;

        public static bool Ready => instance != null && instance.preloaded;
        public static HushAudio Instance => instance;

        public static HushAudio Create()
        {
            if (instance != null) return instance;
            var go = new GameObject("Hush Audio");
            instance = go.AddComponent<HushAudio>();
            DontDestroyOnLoad(go);
            instance.Initialize();
            return instance;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                preloaded = false;
            }

            if (musicFade != null) StopCoroutine(musicFade);
            if (pendingMusic != null) StopCoroutine(pendingMusic);

            foreach (var source in musicSources)
            {
                if (source != null) source.Stop();
            }

            if (clickSource != null) clickSource.Stop();

            foreach (var source in sfxPool)
            {
                if (source != null) source.Stop();
            }
        }

        private void Initialize()
        {
            for (int i = 0; i < musicSources.Length; i++)
            {
                var go = new GameObject("Music " + (i + 1));
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
                musicSources[i] = source;
            }

            var clickObject = new GameObject("Beat Clicks");
            clickObject.transform.SetParent(transform, false);
            clickSource = clickObject.AddComponent<AudioSource>();
            clickSource.playOnAwake = false;
            clickSource.loop = true;
            clickSource.spatialBlend = 0f;
            clickSource.volume = 0f;

            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("SFX " + (i + 1));
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                sfxPool.Add(source);
            }

            BuildEffects();
            ApplyVolumes();
        }

        // ------------------------------------------------------------------ preload

        public static IEnumerator Preload(Action<float> progress)
        {
            return new PreloadOperation(instance, progress);
        }

        private sealed class PreloadOperation : CustomYieldInstruction
        {
            private readonly HushAudio audio;
            private readonly Action<float> progress;
            private int step;
            private int districtIndex;

            public PreloadOperation(HushAudio audio, Action<float> progress)
            {
                this.audio = audio;
                this.progress = progress;
                if (audio == null || audio.preloaded)
                {
                    progress?.Invoke(1.0f);
                    step = 999;
                }
            }

            public override bool keepWaiting
            {
                get
                {
                    if (audio == null || audio.preloaded || step >= 999)
                        return false;

                    switch (step)
                    {
                        case 0:
                            audio.BuildEffects();
                            progress?.Invoke(0.08f);
                            step = 1;
                            return true;

                        case 1:
                            if (audio.menuLoop == null) audio.menuLoop = audio.BuildMenuLoop();
                            progress?.Invoke(0.35f);
                            step = 2;
                            districtIndex = 0;
                            return true;

                        case 2:
                            if (districtIndex < DistrictCatalog.Count)
                            {
                                if (audio.districtLoops[districtIndex] == null)
                                {
                                    audio.districtLoops[districtIndex] = audio.BuildDistrictLoop(districtIndex, out AudioClip clicks);
                                    audio.districtClicks[districtIndex] = clicks;
                                }
                                districtIndex++;
                                progress?.Invoke(0.35f + 0.65f * districtIndex / DistrictCatalog.Count);
                                return true;
                            }
                            audio.preloaded = true;
                            progress?.Invoke(1.0f);
                            step = 999;
                            return false;

                        default:
                            return false;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ music

        public static void PlayMenu()
        {
            if (instance == null) return;
            instance.PlayMenuInternal();
        }

        private void PlayMenuInternal()
        {
            if (menuLoop == null) menuLoop = BuildMenuLoop();
            if (pendingMusic != null) StopCoroutine(pendingMusic);
            pendingMusic = null;
            clickSource.Stop();

            var source = musicSources[activeMusicSource];
            if (source.clip == menuLoop && source.isPlaying) return;

            activeMusicSource = 1 - activeMusicSource;
            var next = musicSources[activeMusicSource];
            next.Stop();
            next.clip = menuLoop;
            next.loop = true;
            next.time = 0f;
            next.volume = 0f;
            next.Play();

            FadeMusicTo(next, MenuVolume, 0.8f);
            var previous = musicSources[1 - activeMusicSource];
            if (previous.isPlaying) StartCoroutine(FadeOutAndStop(previous, 0.6f));
        }

        public static double PlayDistrict(int index)
        {
            if (instance == null) return AudioSettings.dspTime;
            if (index < 0 || index >= DistrictCatalog.Count) index = 0;
            if (instance.districtLoops[index] == null)
            {
                instance.districtLoops[index] = instance.BuildDistrictLoop(index, out AudioClip clickClip);
                instance.districtClicks[index] = clickClip;
            }

            double startDsp = AudioSettings.dspTime + 0.55;
            instance.StartDistrictInternal(index, startDsp);
            return startDsp;
        }

        private void StartDistrictInternal(int index, double startDsp)
        {
            if (pendingMusic != null) StopCoroutine(pendingMusic);
            paused = false;

            var old = musicSources[activeMusicSource];
            activeMusicSource = 1 - activeMusicSource;
            var next = musicSources[activeMusicSource];
            next.Stop();
            next.clip = districtLoops[index];
            next.loop = true;
            next.time = 0f;
            next.volume = 0f;

            if (startDsp > AudioSettings.dspTime + 0.03) next.PlayScheduled(startDsp);
            else next.Play();

            if (old.isPlaying) StartCoroutine(FadeOutAndStop(old, 0.35f));

            clickSource.Stop();
            clickSource.clip = districtClicks[index];
            clickSource.time = 0f;
            clickSource.volume = ClickVolume;
            if (startDsp > AudioSettings.dspTime + 0.03) clickSource.PlayScheduled(startDsp);
            else clickSource.Play();

            pendingMusic = StartCoroutine(FadeMusicInAt(next, startDsp, GameplayMusicVolume, 0.5f));
        }

        private IEnumerator FadeMusicInAt(AudioSource source, double startDsp, float target, float duration)
        {
            while (AudioSettings.dspTime < startDsp) yield return null;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                source.volume = Mathf.Lerp(0f, target, t);
                yield return null;
            }
            source.volume = target;
            pendingMusic = null;
        }

        private void FadeMusicTo(AudioSource source, float target, float duration)
        {
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = StartCoroutine(FadeRoutine(source, target, duration));
        }

        private IEnumerator FadeRoutine(AudioSource source, float target, float duration)
        {
            float start = source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration)));
                yield return null;
            }
            source.volume = target;
            musicFade = null;
        }

        private IEnumerator FadeOutAndStop(AudioSource source, float duration)
        {
            if (source == null) yield break;
            float start = source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (source == null) yield break;
                source.volume = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration)));
                yield return null;
            }
            if (source != null)
            {
                source.Stop();
                source.volume = 0f;
            }
        }

        public static void StopClicks()
        {
            if (instance != null) instance.clickSource.Stop();
        }

        public static void DuckGameplay(float target)
        {
            if (instance == null) return;
            var source = instance.musicSources[instance.activeMusicSource];
            instance.FadeMusicTo(source, instance.GameplayMusicVolume * Mathf.Clamp01(target), 0.5f);
        }

        public static void StopGameplay(float fade = 0.4f)
        {
            if (instance == null) return;
            instance.clickSource.Stop();
            if (instance.pendingMusic != null)
            {
                instance.StopCoroutine(instance.pendingMusic);
                instance.pendingMusic = null;
            }
            var source = instance.musicSources[instance.activeMusicSource];
            instance.StartCoroutine(instance.FadeOutAndStop(source, fade));
        }

        public static void PauseGameplay(bool pause)
        {
            if (instance == null) return;
            instance.paused = pause;
            if (pause)
            {
                instance.clickSource.Pause();
                for (int i = 0; i < instance.musicSources.Length; i++)
                {
                    if (instance.musicSources[i].isPlaying) instance.musicSources[i].Pause();
                }
            }
            else
            {
                instance.clickSource.UnPause();
                for (int i = 0; i < instance.musicSources.Length; i++)
                {
                    instance.musicSources[i].UnPause();
                }
                instance.clickSource.volume = instance.ClickVolume;
            }
        }

        // ------------------------------------------------------------------ settings

        public static void ApplyVolumes()
        {
            if (instance != null) instance.ApplyVolumesInternal();
        }

        private void ApplyVolumesInternal()
        {
            var save = HushProgress.Data;
            AudioListener.volume = Mathf.Clamp01(save.masterVolume);
            menuFade = 1f;
            for (int i = 0; i < musicSources.Length; i++)
            {
                if (musicSources[i].clip == menuLoop) musicSources[i].volume = MenuVolume;
                else if (musicSources[i].isPlaying && !paused) musicSources[i].volume = GameplayMusicVolume;
            }
            if (!paused) clickSource.volume = ClickVolume;
        }

        private float MenuVolume => HushProgress.Data.musicVolume * 0.52f;
        private float GameplayMusicVolume => HushProgress.Data.musicVolume * 0.48f;
        private float ClickVolume => HushProgress.Data.beatClicks ? HushProgress.Data.sfxVolume * 0.3f : 0f;

        public static void Sfx(SfxId id, float volume = 1f, float pitch = 1f)
        {
            if (instance == null) return;
            if (!instance.effects.TryGetValue(id, out AudioClip clip)) return;
            var source = instance.sfxPool[UnityEngine.Random.Range(0, instance.sfxPool.Count)];
            source.pitch = pitch;
            source.PlayOneShot(clip, Mathf.Clamp01(volume * HushProgress.Data.sfxVolume));
        }

        // ------------------------------------------------------------------ synthesis

        private void BuildEffects()
        {
            if (effects.Count > 0) return;
            effects[SfxId.UiClick] = MakeClip("sfx_ui_click", 0.09f, (data, rate) =>
            {
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float envelope = Mathf.Exp(-t * 60f);
                    float tone = Mathf.Sin(2f * Mathf.PI * 1250f * t) * 0.5f;
                    float noise = (Hash(i) * 2f - 1f) * Mathf.Exp(-t * 170f) * 0.35f;
                    data[i] = (tone + noise) * envelope * 0.6f;
                }
            });

            effects[SfxId.UiBack] = MakeClip("sfx_ui_back", 0.14f, (data, rate) =>
            {
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float frequency = Mathf.Lerp(720f, 360f, t / 0.14f);
                    float envelope = Mathf.Exp(-t * 28f);
                    data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.42f;
                }
            });

            effects[SfxId.Place] = MakeClip("sfx_place", 0.26f, (data, rate) =>
            {
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float frequency = Mathf.Lerp(165f, 54f, Mathf.Clamp01(t / 0.2f));
                    float envelope = Mathf.Exp(-t * 20f);
                    float body = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.9f;
                    float thump = (Hash(i) * 2f - 1f) * Mathf.Exp(-t * 90f) * 0.35f;
                    data[i] = (body + thump) * envelope * 0.75f;
                }
            });

            effects[SfxId.Perfect] = MakeClip("sfx_perfect", 1.0f, (data, rate) =>
            {
                float[] partials = { 880f, 1320f, 1760f, 2640f };
                float[] gains = { 0.34f, 0.22f, 0.16f, 0.08f };
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float value = 0f;
                    for (int p = 0; p < partials.Length; p++)
                    {
                        value += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * Mathf.Exp(-t * (3.6f + p * 1.4f)) * gains[p];
                    }
                    value += (Hash(i) * 2f - 1f) * Mathf.Exp(-t * 70f) * 0.05f;
                    data[i] = value;
                }
            });

            effects[SfxId.Good] = MakeClip("sfx_good", 0.5f, (data, rate) =>
            {
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float value = Mathf.Sin(2f * Mathf.PI * 659f * t) * Mathf.Exp(-t * 6.5f) * 0.32f;
                    value += Mathf.Sin(2f * Mathf.PI * 988f * t) * Mathf.Exp(-t * 8f) * 0.14f;
                    data[i] = value;
                }
            });

            effects[SfxId.Offbeat] = MakeClip("sfx_offbeat", 0.28f, (data, rate) =>
            {
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float frequency = 300f + Mathf.Sin(t * 55f) * 24f;
                    float value = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.3f;
                    value += Mathf.Sin(2f * Mathf.PI * (frequency * 0.5f) * t) * 0.2f;
                    data[i] = value * Mathf.Exp(-t * 11f);
                }
            });

            effects[SfxId.Miss] = MakeClip("sfx_miss", 0.7f, (data, rate) =>
            {
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float frequency = Mathf.Lerp(210f, 58f, Mathf.Clamp01(t / 0.55f));
                    float saw = Mathf.Repeat(frequency * t, 1f) * 2f - 1f;
                    float value = saw * 0.3f + Mathf.Sin(2f * Mathf.PI * frequency * 0.5f * t) * 0.35f;
                    value += (Hash(i) * 2f - 1f) * Mathf.Exp(-t * 24f) * 0.2f;
                    data[i] = value * Mathf.Exp(-t * 6.5f);
                }
            });

            effects[SfxId.Countdown] = MakeClip("sfx_countdown", 0.18f, (data, rate) =>
            {
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float value = Mathf.Sin(2f * Mathf.PI * 660f * t) * 0.3f;
                    data[i] = value * Mathf.Exp(-t * 22f);
                }
            });

            effects[SfxId.Go] = MakeClip("sfx_go", 0.42f, (data, rate) =>
            {
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float value = Mathf.Sin(2f * Mathf.PI * 990f * t) * 0.28f;
                    value += Mathf.Sin(2f * Mathf.PI * 1485f * t) * 0.14f;
                    value += Mathf.Sin(2f * Mathf.PI * 1980f * t) * 0.08f;
                    data[i] = value * Mathf.Exp(-t * 7f);
                }
            });

            effects[SfxId.LevelClear] = MakeClip("sfx_level_clear", 2.2f, (data, rate) =>
            {
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
                for (int n = 0; n < notes.Length; n++)
                {
                    int offset = Mathf.RoundToInt(n * 0.13f * rate);
                    for (int i = offset; i < data.Length; i++)
                    {
                        float t = (i - offset) / (float)rate;
                        float value = Mathf.Sin(2f * Mathf.PI * notes[n] * t) * Mathf.Exp(-t * 2.4f) * 0.22f;
                        value += Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * t) * Mathf.Exp(-t * 4.2f) * 0.07f;
                        data[i] += value;
                    }
                }
            });

            effects[SfxId.GameOver] = MakeClip("sfx_game_over", 2.4f, (data, rate) =>
            {
                float[] notes = { 220f, 174.61f, 146.83f, 110f };
                for (int n = 0; n < notes.Length; n++)
                {
                    int offset = Mathf.RoundToInt(n * 0.34f * rate);
                    for (int i = offset; i < data.Length; i++)
                    {
                        float t = (i - offset) / (float)rate;
                        float value = Mathf.Sin(2f * Mathf.PI * notes[n] * t) * Mathf.Exp(-t * 1.7f) * 0.26f;
                        value += Mathf.Sin(2f * Mathf.PI * notes[n] * 0.5f * t) * Mathf.Exp(-t * 1.1f) * 0.18f;
                        data[i] += value;
                    }
                }
            });

            effects[SfxId.Star] = MakeClip("sfx_star", 0.8f, (data, rate) =>
            {
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float value = Mathf.Sin(2f * Mathf.PI * 1568f * t) * Mathf.Exp(-t * 5f) * 0.22f;
                    value += Mathf.Sin(2f * Mathf.PI * 2093f * t) * Mathf.Exp(-t * 7f) * 0.14f;
                    data[i] = value;
                }
            });

            effects[SfxId.HeartLost] = MakeClip("sfx_heart_lost", 0.5f, (data, rate) =>
            {
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float value = Mathf.Sin(2f * Mathf.PI * 120f * t) * Mathf.Exp(-t * 8f) * 0.3f;
                    value += (Hash(i) * 2f - 1f) * Mathf.Exp(-t * 40f) * 0.12f;
                    data[i] = value;
                }
            });

            foreach (var pair in effects) Normalize(pair.Value);
        }

        private AudioClip BuildMenuLoop()
        {
            int samplesPerBeat = Mathf.RoundToInt(MenuBeatDuration * SampleRate);
            int total = samplesPerBeat * BeatsPerLoop;
            var data = new float[total];

            int[][] chords =
            {
                new[] { 57, 60, 64, 67 },
                new[] { 53, 57, 60, 64 },
                new[] { 48, 52, 55, 59 },
                new[] { 55, 59, 62, 65 }
            };

            float barDuration = samplesPerBeat * 4f / SampleRate;
            for (int bar = 0; bar < 4; bar++)
            {
                int start = bar * samplesPerBeat * 4;
                AddPad(data, start, samplesPerBeat * 4, chords[bar], 0.055f, SampleRate);
                AddBass(data, start, samplesPerBeat, 1, chords[bar][0] - 24, 0.06f, SampleRate);
                // Sparse, music-box arpeggio.
                for (int step = 0; step < 4; step++)
                {
                    int note = chords[bar][(step + 1) % chords[bar].Length] + 24;
                    AddPluck(data, start + step * samplesPerBeat + samplesPerBeat / 2, note, 0.075f, 0.32f, SampleRate, true);
                }
            }

            AddDelay(data, (int)(0.34f * SampleRate), 0.26f, 0.16f);
            Normalize(data, 0.72f);
            ApplyLoopFades(data, SampleRate);
            return BuildClip("music_menu", data);
        }

        private AudioClip BuildDistrictLoop(int index, out AudioClip clickClip)
        {
            var definition = DistrictCatalog.Get(index);
            int samplesPerBeat = Mathf.RoundToInt(60f / definition.Bpm * SampleRate);
            int total = samplesPerBeat * BeatsPerLoop;
            var data = new float[total];

            int[][] chords;
            int[] bassRoots;
            switch (index)
            {
                case 1:
                    chords = new[]
                    {
                        new[] { 50, 53, 57, 60 },
                        new[] { 46, 50, 53, 57 },
                        new[] { 41, 45, 48, 52 },
                        new[] { 48, 52, 55, 59 }
                    };
                    bassRoots = new[] { 38, 34, 29, 36 };
                    break;
                case 2:
                    chords = new[]
                    {
                        new[] { 52, 55, 59, 62 },
                        new[] { 48, 52, 55, 59 },
                        new[] { 43, 47, 50, 55 },
                        new[] { 50, 54, 57, 62 }
                    };
                    bassRoots = new[] { 40, 36, 31, 38 };
                    break;
                default:
                    chords = new[]
                    {
                        new[] { 57, 60, 64, 67 },
                        new[] { 53, 57, 60, 64 },
                        new[] { 48, 52, 55, 59 },
                        new[] { 55, 59, 62, 65 }
                    };
                    bassRoots = new[] { 45, 41, 36, 43 };
                    break;
            }

            for (int bar = 0; bar < 4; bar++)
            {
                int start = bar * samplesPerBeat * 4;
                AddPad(data, start, samplesPerBeat * 4, chords[bar], 0.05f, SampleRate);

                // Two low pulses per bar.
                AddBass(data, start, samplesPerBeat, 1, bassRoots[bar], 0.12f, SampleRate);
                AddBass(data, start + samplesPerBeat * 2, samplesPerBeat, 1, bassRoots[bar], 0.09f, SampleRate);

                // Eighth-note arpeggio with a soft pluck.
                for (int step = 0; step < 8; step++)
                {
                    int note = chords[bar][step % chords[bar].Length] + (step >= 4 ? 12 : 0) + 12;
                    AddPluck(data, start + step * samplesPerBeat / 2, note, 0.05f, 0.2f, SampleRate, false);
                }

                // Kick + brushed hats. The click track carries the precise beat.
                AddKick(data, start, 0.1f, SampleRate);
                AddKick(data, start + samplesPerBeat * 2, 0.08f, SampleRate);
                for (int step = 0; step < 8; step++)
                {
                    AddHat(data, start + step * samplesPerBeat / 2, step % 2 == 0 ? 0.018f : 0.03f, SampleRate);
                }
            }

            AddDelay(data, (int)(0.285f * SampleRate), 0.24f, 0.14f);
            Normalize(data, 0.78f);
            ApplyLoopFades(data, SampleRate);
            clickClip = BuildClickLoop(definition.Bpm, samplesPerBeat);
            return BuildClip("music_district_" + index, data);
        }

        private AudioClip BuildClickLoop(int bpm, int samplesPerBeat)
        {
            int total = samplesPerBeat * BeatsPerLoop;
            var data = new float[total];
            for (int beat = 0; beat < BeatsPerLoop; beat++)
            {
                int start = beat * samplesPerBeat;
                bool downbeat = beat % 4 == 0;
                int length = Mathf.RoundToInt(SampleRate * (downbeat ? 0.05f : 0.032f));
                float frequency = downbeat ? 1420f : 1080f;
                float gain = downbeat ? 0.5f : 0.3f;
                for (int i = 0; i < length && start + i < total; i++)
                {
                    float t = i / (float)SampleRate;
                    float envelope = Mathf.Exp(-t * (downbeat ? 70f : 95f));
                    float tone = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.6f;
                    tone += Mathf.Sin(2f * Mathf.PI * frequency * 0.5f * t) * 0.2f;
                    data[start + i] += tone * envelope * gain;
                }

                // Faint off-beat tick to help internalize subdivisions.
                int offStart = start + samplesPerBeat / 2;
                int offLength = Mathf.RoundToInt(SampleRate * 0.016f);
                for (int i = 0; i < offLength && offStart + i < total; i++)
                {
                    float t = i / (float)SampleRate;
                    float envelope = Mathf.Exp(-t * 220f);
                    data[offStart + i] += (Hash(i + beat * 131) * 2f - 1f) * envelope * 0.06f;
                }
            }
            Normalize(data, 0.7f);
            return BuildClip("click_" + bpm, data);
        }

        private static void AddPad(float[] data, int start, int length, int[] notes, float gain, int rate)
        {
            float duration = length / (float)rate;
            for (int n = 0; n < notes.Length; n++)
            {
                float frequency = MidiToFrequency(notes[n]);
                for (int i = 0; i < length; i++)
                {
                    int index = start + i;
                    if (index < 0 || index >= data.Length) continue;
                    float t = i / (float)rate;
                    float attack = Mathf.Clamp01(t / 0.65f);
                    float release = Mathf.Clamp01((duration - t) / 0.9f);
                    float envelope = attack * release;
                    float tremolo = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.13f * t + n);
                    float value = Mathf.Sin(2f * Mathf.PI * frequency * t);
                    value += Mathf.Sin(2f * Mathf.PI * (frequency * 1.0045f) * t) * 0.7f;
                    value += Mathf.Sin(2f * Mathf.PI * (frequency * 0.997f) * t) * 0.5f;
                    data[index] += value * envelope * tremolo * gain;
                }
            }
        }

        private static void AddBass(float[] data, int start, int length, int beats, int midi, float gain, int rate)
        {
            float frequency = MidiToFrequency(midi);
            int voices = Mathf.Max(1, beats);
            for (int voice = 0; voice < voices; voice++)
            {
                int offset = start + voice * length / voices;
                for (int i = 0; i < length / voices; i++)
                {
                    int index = offset + i;
                    if (index < 0 || index >= data.Length) continue;
                    float t = i / (float)rate;
                    float envelope = Mathf.Exp(-t * 3.2f) * Mathf.Clamp01(t * 90f);
                    float value = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.85f;
                    value += Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * 0.12f;
                    data[index] += value * envelope * gain;
                }
            }
        }

        private static void AddPluck(float[] data, int start, int midi, float gain, float decay, int rate, bool soft)
        {
            float frequency = MidiToFrequency(midi);
            int length = Mathf.RoundToInt(rate * (soft ? 1.4f : 0.7f));
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index < 0 || index >= data.Length) continue;
                float t = i / (float)rate;
                float envelope = Mathf.Exp(-t * (soft ? 2.1f : 3.4f));
                float value = Mathf.Sin(2f * Mathf.PI * frequency * t);
                value += Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * (soft ? 0.12f : 0.2f);
                value += Mathf.Sin(2f * Mathf.PI * frequency * 3f * t) * 0.05f;
                data[index] += value * envelope * gain * Mathf.Exp(-t * (1f / Mathf.Max(0.05f, decay)) * 0.15f);
            }
        }

        private static void AddKick(float[] data, int start, float gain, int rate)
        {
            int length = Mathf.RoundToInt(rate * 0.28f);
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index < 0 || index >= data.Length) continue;
                float t = i / (float)rate;
                float frequency = 94f * Mathf.Exp(-t * 22f) + 42f;
                float value = Mathf.Sin(2f * Mathf.PI * frequency * t);
                data[index] += value * Mathf.Exp(-t * 9f) * gain;
            }
        }

        private static void AddHat(float[] data, int start, float gain, int rate)
        {
            int length = Mathf.RoundToInt(rate * 0.045f);
            int index = start;
            for (int i = 0; i < length; i++)
            {
                int target = start + i;
                if (target < 0 || target >= data.Length) continue;
                float t = i / (float)rate;
                float noise = Hash(index + i * 17) * 2f - 1f;
                float bright = noise - (Hash(index + i * 17 + 1) * 2f - 1f) * 0.6f;
                data[target] += bright * Mathf.Exp(-t * 90f) * gain;
            }
        }

        private static void AddDelay(float[] data, int delaySamples, float feedback, float mix)
        {
            if (delaySamples <= 0 || delaySamples >= data.Length) return;
            for (int i = delaySamples; i < data.Length; i++)
            {
                data[i] += data[i - delaySamples] * feedback;
            }
            // A second, offset tap widens the space without true stereo.
            int second = Mathf.RoundToInt(delaySamples * 1.47f);
            if (second < data.Length)
            {
                for (int i = second; i < data.Length; i++)
                {
                    data[i] += data[i - second] * mix;
                }
            }
        }

        private static void ApplyLoopFades(float[] data, int rate)
        {
            int fade = Mathf.RoundToInt(rate * 0.012f);
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                data[i] *= t;
                data[data.Length - 1 - i] *= t;
            }
        }

        private static void Normalize(AudioClip clip)
        {
            if (clip == null) return;
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            Normalize(data, 0.86f);
            clip.SetData(data, 0);
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float value = Mathf.Abs(data[i]);
                if (value > max) max = value;
            }
            if (max <= 0.0001f) return;
            float scale = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i] * scale, -1f, 1f);
        }

        private static AudioClip MakeClip(string name, float seconds, Action<float[], int> fill)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            var data = new float[samples];
            fill(data, SampleRate);
            Normalize(data, 0.87f);
            return BuildClip(name, data);
        }

        private static AudioClip BuildClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float MidiToFrequency(int midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }

        private static float Hash(int value)
        {
            unchecked
            {
                uint x = (uint)value;
                x ^= x >> 16;
                x *= 0x7feb352du;
                x ^= x >> 15;
                x *= 0x846ca68bu;
                x ^= x >> 16;
                return (x & 0xFFFFFF) / 16777215f;
            }
        }

        public static float DistrictBeatDuration(int index)
        {
            var definition = DistrictCatalog.Get(index);
            int samplesPerBeat = Mathf.RoundToInt(60f / definition.Bpm * SampleRate);
            return samplesPerBeat / (float)SampleRate;
        }
    }
}
