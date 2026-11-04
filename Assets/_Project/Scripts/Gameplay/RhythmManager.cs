using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Timing rating for rhythmic player inputs against the beat conductor.
    /// Includes backward-compatible aliases for project integration.
    /// </summary>
    public enum TimingResult
    {
        None = 0,
        Perfect = 1,
        Accurate = 1, // Backward compatibility alias
        Good = 2,
        Early = 3,
        Late = 4,
        Miss = 5
    }

    /// <summary>
    /// Immutable record describing a scored input event.
    /// </summary>
    [Serializable]
    public readonly struct InputScore
    {
        public readonly TimingResult Result;
        public readonly long BeatIndex;
        public readonly double InputTime;
        public readonly double OffsetSeconds;
        public readonly int SilenceChain;

        public InputScore(TimingResult result, long beatIndex, double inputTime, double offsetSeconds, int silenceChain)
        {
            Result = result;
            BeatIndex = beatIndex;
            InputTime = inputTime;
            OffsetSeconds = offsetSeconds;
            SilenceChain = silenceChain;
        }
    }

    /// <summary>
    /// Audio Conductor and Input Evaluator for Hush Blocks.
    /// Uses AudioSettings.dspTime for a drift-free, sample-accurate clock that drives
    /// quarter-note beats, syncopated double-beats, and input scoring windows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RhythmManager : MonoBehaviour
    {
        [Header("Scriptable Difficulty & Clock")]
        [Tooltip("Optional ScriptableObject profile overriding tempo and timing windows.")]
        [SerializeField] private DifficultyProfile difficultyProfile;
        [Tooltip("Automatically starts the conductor loop when Awake runs.")]
        [SerializeField] private bool playOnAwake = true;
        [Tooltip("Beats per minute. Quarter-note beat interval = 60 / BPM.")]
        [SerializeField, Min(1f)] private float bpm = 120f;

        [Header("Syncopation & Rhythm Structure")]
        [Tooltip("Emits an off-beat syncopated double-beat event between quarter-notes.")]
        [SerializeField] private bool emitSyncopatedDoubleBeats = true;
        [Tooltip("Normalized fraction of the beat interval where the double-beat lands (0.5 = 8th note).")]
        [SerializeField, Range(0.1f, 0.9f)] private float doubleBeatOffset = 0.5f;

        [Header("Scoring Windows (Seconds)")]
        [Tooltip("Maximum absolute offset (±seconds) for a Perfect hit. Standard: ±0.050s (±50ms).")]
        [SerializeField, Min(0.005f)] private float perfectWindow = 0.05f;
        [Tooltip("Maximum absolute offset (±seconds) for a Good hit. Standard: ±0.100s (±100ms).")]
        [SerializeField, Min(0.01f)] private float goodWindow = 0.10f;
        [Tooltip("Extra tolerance beyond the Good window before a hit is considered completely missed.")]
        [SerializeField, Min(0.01f)] private float lateTolerance = 0.06f;

        [Header("Chain Decay")]
        [Tooltip("Seconds of inactivity before an unbroken silence chain decays to zero.")]
        [SerializeField, Min(0.5f)] private float chainDecaySeconds = 3.0f;

        // Conductor clock state (dspTime-backed)
        private double startDspTime;
        private double timeSinceStart;
        private long currentBeatIndex = -1L;
        private long closedBeatIndex = -1L;
        private long emittedDoubleBeatIndex = -1L;
        private bool isPlaying;
        private int silenceChain;
        private float lastSuccessfulHitTime;

        // Scored beat deduplication set
        private readonly HashSet<long> scoredBeats = new HashSet<long>();

        // Properties
        public DifficultyProfile DifficultyProfile => difficultyProfile;
        public double TimeSinceStart => timeSinceStart;
        public long CurrentBeatIndex => currentBeatIndex;
        public float Bpm => difficultyProfile != null ? difficultyProfile.Bpm : bpm;
        public float BeatInterval => difficultyProfile != null ? difficultyProfile.BeatInterval : (60f / Mathf.Max(1f, bpm));
        public bool IsPlaying => isPlaying;
        public int SilenceChain => silenceChain;
        public float PerfectWindow => difficultyProfile != null ? difficultyProfile.AccurateWindow : perfectWindow;
        public float GoodWindow => difficultyProfile != null ? Mathf.Max(goodWindow, difficultyProfile.AccurateWindow + difficultyProfile.LateWindow) : goodWindow;
        public float ChainDecaySeconds => difficultyProfile != null ? difficultyProfile.ChainDecaySeconds : chainDecaySeconds;

        /// <summary>Progress within the current quarter-note beat [0.0, 1.0).</summary>
        public float CurrentBeatProgress
        {
            get
            {
                if (!isPlaying || BeatInterval <= 0f) return 0f;
                double beatStart = currentBeatIndex * BeatInterval;
                return Mathf.Clamp01((float)((timeSinceStart - beatStart) / BeatInterval));
            }
        }

        // Primary Event Delegates
        public event Action<long> OnBeat;
        public event Action<long> OnDoubleBeat;
        public event Action<TimingResult> OnInputScored;
        public event Action<InputScore> OnScoreEvaluated;
        public event Action<int> OnSilenceChainChanged;

        // Integration / Legacy Convenience Delegates
        public event Action<long> OnAccurateInput;
        public event Action<long> OnEarlyInput;
        public event Action<long> OnLateInput;
        public event Action<long> OnMissedBeat;

        private void Awake()
        {
            if (difficultyProfile == null)
            {
                Debug.LogWarning("[RhythmManager] No DifficultyProfile assigned; using inspector timing windows.", this);
            }
            if (playOnAwake)
            {
                Play();
            }
        }

        private void OnDestroy()
        {
            Stop();
        }

        /// <summary>
        /// Starts or restarts the audio conductor clock using AudioSettings.dspTime.
        /// </summary>
        public void Play()
        {
            startDspTime = AudioSettings.dspTime;
            timeSinceStart = 0d;
            currentBeatIndex = -1L;
            closedBeatIndex = -1L;
            emittedDoubleBeatIndex = -1L;
            silenceChain = 0;
            lastSuccessfulHitTime = Time.time;
            scoredBeats.Clear();
            isPlaying = true;
        }

        /// <summary>
        /// Halts the conductor clock.
        /// </summary>
        public void Stop()
        {
            isPlaying = false;
        }

        private void Update()
        {
            if (!isPlaying) return;

            // Sample drift-free DSP time
            timeSinceStart = Math.Max(0d, AudioSettings.dspTime - startDspTime);
            float interval = BeatInterval;
            if (interval <= 0f) return;

            // 1. Dispatch quarter-note beats
            long targetBeat = (long)Math.Floor(timeSinceStart / interval);
            for (long beat = currentBeatIndex + 1L; beat <= targetBeat; beat++)
            {
                currentBeatIndex = beat;
                OnBeat?.Invoke(beat);
            }

            // 2. Dispatch syncopated double-beats (e.g. 8th note off-beats)
            if (emitSyncopatedDoubleBeats && currentBeatIndex >= 0L)
            {
                double doubleBeatTime = currentBeatIndex * interval + interval * doubleBeatOffset;
                if (timeSinceStart >= doubleBeatTime && emittedDoubleBeatIndex < currentBeatIndex)
                {
                    emittedDoubleBeatIndex = currentBeatIndex;
                    OnDoubleBeat?.Invoke(currentBeatIndex);
                }
            }

            // 3. Close expired beat windows (unanswered beats)
            long closeableBeat = (long)Math.Floor((timeSinceStart - GoodWindow - lateTolerance) / interval);
            for (long beat = closedBeatIndex + 1L; beat <= closeableBeat; beat++)
            {
                closedBeatIndex = beat;
                if (!scoredBeats.Contains(beat))
                {
                    // A beat went unanswered past its allowable late window
                    OnMissedBeat?.Invoke(beat);
                }
            }

            // 4. Inactivity chain decay
            if (silenceChain > 0 && Time.time > lastSuccessfulHitTime + ChainDecaySeconds)
            {
                ResetSilenceChain();
            }
        }

        /// <summary>
        /// Evaluates a player tap against the conductor beat clock.
        /// </summary>
        /// <param name="inputTimeSeconds">Seconds elapsed since start (usually TimeSinceStart).</param>
        /// <param name="nearestBeat">The beat index being scored.</param>
        /// <returns>TimingResult rating (Perfect, Good, Early, Late, Miss).</returns>
        public TimingResult EvaluateInput(double inputTimeSeconds, out long nearestBeat)
        {
            float interval = BeatInterval;
            nearestBeat = (long)Math.Round(inputTimeSeconds / interval);

            double beatTime = nearestBeat * interval;
            double offset = inputTimeSeconds - beatTime;
            double absoluteOffset = Math.Abs(offset);

            TimingResult result;
            if (absoluteOffset <= PerfectWindow)
            {
                result = TimingResult.Perfect;
            }
            else if (absoluteOffset <= GoodWindow)
            {
                result = TimingResult.Good;
            }
            else if (offset < 0d && absoluteOffset <= GoodWindow + lateTolerance)
            {
                result = TimingResult.Early;
            }
            else if (offset > 0d && absoluteOffset <= GoodWindow + lateTolerance)
            {
                result = TimingResult.Late;
            }
            else
            {
                result = TimingResult.Miss;
            }

            // Prevent duplicate scoring of the same beat
            if (scoredBeats.Contains(nearestBeat) && (result == TimingResult.Perfect || result == TimingResult.Good))
            {
                result = TimingResult.Miss;
            }

            // Update chain and dispatch result events
            if (result == TimingResult.Perfect || result == TimingResult.Good)
            {
                scoredBeats.Add(nearestBeat);
                silenceChain++;
                lastSuccessfulHitTime = Time.time;
                OnSilenceChainChanged?.Invoke(silenceChain);

                if (result == TimingResult.Perfect)
                {
                    OnAccurateInput?.Invoke(nearestBeat);
                }
            }
            else
            {
                // Missed, Early, or Late breaks the active chain
                if (result == TimingResult.Early) OnEarlyInput?.Invoke(nearestBeat);
                else if (result == TimingResult.Late) OnLateInput?.Invoke(nearestBeat);

                ResetSilenceChain();
            }

            InputScore score = new InputScore(result, nearestBeat, inputTimeSeconds, offset, silenceChain);
            OnInputScored?.Invoke(result);
            OnScoreEvaluated?.Invoke(score);

            return result;
        }

        /// <summary>
        /// Evaluates input occurring right now at the current DSP timestamp.
        /// </summary>
        public TimingResult EvaluateCurrentInput(out long nearestBeat)
        {
            return EvaluateInput(TimeSinceStart, out nearestBeat);
        }

        /// <summary>
        /// Evaluates tap timing at the current DSP timestamp (returns TimingResult and nearest beat).
        /// </summary>
        public TimingResult EvaluateTap(out long nearestBeat)
        {
            return EvaluateInput(TimeSinceStart, out nearestBeat);
        }

        /// <summary>
        /// Evaluates tap timing at the current DSP timestamp.
        /// </summary>
        public TimingResult EvaluateTap()
        {
            return EvaluateInput(TimeSinceStart, out _);
        }

        /// <summary>
        /// Resets the active silence chain to zero and notifies listeners.
        /// </summary>
        public void ResetSilenceChain()
        {
            if (silenceChain == 0) return;
            silenceChain = 0;
            OnSilenceChainChanged?.Invoke(0);
        }

        /// <summary>
        /// Returns the exact DSP-based timestamp when a given beat starts.
        /// </summary>
        public double GetBeatStartTime(long beatIndex)
        {
            return Math.Max(0d, beatIndex) * BeatInterval;
        }

        /// <summary>
        /// Seconds remaining until the next upcoming quarter beat.
        /// </summary>
        public float SecondsUntilNextBeat()
        {
            if (!isPlaying || BeatInterval <= 0f) return 0f;
            double nextBeatTime = (currentBeatIndex + 1L) * BeatInterval;
            return Mathf.Max(0f, (float)(nextBeatTime - timeSinceStart));
        }
    }
}
