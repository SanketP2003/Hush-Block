using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Master Technical Audio Designer & Systems Framework for Hush Blocks in Unity 6 (URP).
    /// Orchestrates adaptive stem crossfading, dynamic AudioMixer snapshots, diatonic pitch-scaled
    /// crystallization chimes, height-based street noise muffling, and zero-allocation pooled SFX dispatching.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AcousticAudioManager : MonoBehaviour
    {
        // SFX Priority Hierarchy (0 = highest priority / non-interruptible, 255 = lowest)
        public const int PriorityHazard = 0;
        public const int PriorityCreature = 32;
        public const int PriorityPlacement = 64;
        public const int PriorityPhysics = 128;
        public const int PriorityAmbient = 192;

        [Header("AudioMixer & Snapshot Architecture")]
        [Tooltip("Master AudioMixer controlling stem group routings and snapshot filters (AM_DecibelRow).")]
        [SerializeField] private AudioMixer masterMixer;

        [Tooltip("Snapshot: Harsh, full-clarity unmuted street noise (0 dB Cacophony, -80 dB Lullaby, 22 kHz LP).")]
        [SerializeField] private AudioMixerSnapshot snapshotChaoticStreet;

        [Tooltip("Snapshot: Muted sanctuary peace (-80 dB Cacophony, 0 dB Lullaby, 4.5 kHz warm LP).")]
        [SerializeField] private AudioMixerSnapshot snapshotMutedSanctuary;

        [Tooltip("Snapshot: Tremor hazard state (-12 dB both stems, boosted cyan static & sub-bass rumble).")]
        [SerializeField] private AudioMixerSnapshot snapshotTremorHazard;

        [Header("AudioMixer Bus Groups (Optional Direct Assignment)")]
        [SerializeField] private AudioMixerGroup masterGroup;
        [SerializeField] private AudioMixerGroup bgmGroup;
        [SerializeField] private AudioMixerGroup stemCacophonyGroup;
        [SerializeField] private AudioMixerGroup stemLullabyGroup;
        [SerializeField] private AudioMixerGroup sfxAcousticGroup;
        [SerializeField] private AudioMixerGroup sfxTapsGroup;
        [SerializeField] private AudioMixerGroup sfxResonanceGroup;
        [SerializeField] private AudioMixerGroup sfxCreaturesGroup;

        [Header("Exposed Mixer Parameter Names")]
        [SerializeField] private string cacophonyVolumeParam = "CacophonyVol";
        [SerializeField] private string lullabyVolumeParam = "LullabyVol";
        [SerializeField] private string streetLowpassParam = "StreetLowpassCutoff";

        [Header("Adaptive Height Muffling")]
        [Tooltip("Maximum tower layer height corresponding to complete high-altitude street noise attenuation.")]
        [SerializeField, Min(4)] private int targetAscentLayers = 12;
        [Tooltip("Street lowpass cutoff frequency at ground level (Hz).")]
        [SerializeField, Range(12000f, 22000f)] private float groundLowpassCutoff = 22000f;
        [Tooltip("Street lowpass cutoff frequency at sanctuary altitude (Hz).")]
        [SerializeField, Range(400f, 6000f)] private float sanctuaryLowpassCutoff = 4500f;

        [Header("Synchronized Audio Stems")]
        [Tooltip("AudioSource for Layer 1: Harsh urban din, buzzing neon, and ticking clocks.")]
        [SerializeField] private AudioSource cacophonySource;
        [Tooltip("AudioSource for Layer 2: Muted rooftop stillness, felt piano, and warm cello.")]
        [SerializeField] private AudioSource lullabySource;
        [SerializeField] private AudioClip cacophonyStemClip;
        [SerializeField] private AudioClip lullabyStemClip;

        [Header("Acoustic Sound Palette (SFX)")]
        [Tooltip("Trowel stone/ceramic contact strike on block placement.")]
        [SerializeField] private AudioClip trowelStrikeClip;
        [Tooltip("Resonant crystallization chime (pitch-scaled diatonically by silence chain).")]
        [SerializeField] private AudioClip crystallizationChimeClip;
        [Tooltip("Harmonic sparkle chime played on a Perfect timing tap.")]
        [SerializeField] private AudioClip perfectHarmonicSparkleClip;
        [Tooltip("Anti-gravity whoosh played when sheared overhang chunks float upward.")]
        [SerializeField] private AudioClip antiGravityWhooshClip;
        [Tooltip("Deep sub-bass resonance swell & tremor rumble played on missed beats or stack instability.")]
        [SerializeField] private AudioClip tremorHazardClip;
        [Tooltip("Cyan static frequency spike emitted by unmuted chaotic blocks or Howler windup.")]
        [SerializeField] private AudioClip cyanStaticBuzzClip;
        [Tooltip("Resonant overload glass/charcoal shatter when the Feedback Howler is dispersed.")]
        [SerializeField] private AudioClip howlerShatterClip;

        [Header("SFX Object Pool Settings")]
        [Tooltip("Number of pre-allocated AudioSources for zero-allocation runtime dispatch.")]
        [SerializeField, Range(8, 32)] private int audioSourcePoolSize = 16;

        [Header("System Dependencies")]
        [SerializeField] private TowerManager towerManager;
        [SerializeField] private RhythmManager rhythmManager;
        [SerializeField] private FeedbackHowlerAI feedbackHowler;

        // ---------- Runtime State Tracking ----------
        private float targetSilenceRatio = 0f;
        private float currentSilenceRatio = 0f;
        private float silenceRatioVelocity = 0f;

        private float tremorHazardWeight = 0f;
        private float tremorHazardVelocity = 0f;
        private float tremorTimer = 0f;
        private bool isTremorActive = false;

        private float currentLowpassCutoff = 22000f;
        private float targetLowpassCutoff = 22000f;
        private float lowpassVelocity = 0f;

        // Pre-allocated Snapshot arrays for zero-GC AudioMixer transitions
        private AudioMixerSnapshot[] baselineSnapshots;
        private readonly float[] snapshotWeights = new float[3];
        private readonly HashSet<string> exposedParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Object Pool
        private struct PooledSource
        {
            public AudioSource Source;
            public int CurrentPriority;
            public float ReleaseTime;
        }
        private PooledSource[] sourcePool;
        private int poolIndexCursor = 0;

        // Diatonic Scale Multipliers (12-TET intervals pre-calculated for 0 alloc)
        // Root (0 st) = 1.0, Maj 3rd (+4 st) = 1.25992, 5th (+7 st) = 1.49831, Octave (+12 st) = 2.0, Oct+3rd (+16 st) = 2.51984
        private static readonly float PitchRoot = 1.00000f;
        private static readonly float PitchMajorThird = 1.25992f;
        private static readonly float PitchPerfectFifth = 1.49831f;
        private static readonly float PitchOctave = 2.00000f;
        private static readonly float PitchOctaveThird = 2.51984f;

        // Public Properties
        public float SilenceRatio => currentSilenceRatio;
        public float CurrentStreetLowpass => currentLowpassCutoff;
        public bool IsTremorActive => isTremorActive;

        // Events
        public event Action<float> OnSilenceRatioUpdated;
        public event Action<bool> OnTremorStateChanged;

        private void Awake()
        {
            ResolveDependencies();
            InitializeAudioMixerReferences();
            InitializeAudioPool();
            EnsureFallbackAudioClips();
        }

        private void OnEnable()
        {
            RegisterEventSubscriptions();
        }

        private void OnDisable()
        {
            UnregisterEventSubscriptions();
        }

        private void Start()
        {
            StartSynchronizedStems();
            UpdateSilenceRatio(immediate: true);
            UpdateHeightMuffling(immediate: true);
        }

        private void Update()
        {
            UpdateSilenceRatioSmooth();
            UpdateTremorState();
            UpdateHeightMufflingSmooth();
            ApplyMixerSnapshotBlend();
        }

        // =========================================================================
        // 1. Initialization & Dependency Resolution
        // =========================================================================

        private void ResolveDependencies()
        {
            if (towerManager == null) towerManager = UnityEngine.Object.FindAnyObjectByType<TowerManager>();
            if (rhythmManager == null) rhythmManager = UnityEngine.Object.FindAnyObjectByType<RhythmManager>();
            if (feedbackHowler == null) feedbackHowler = UnityEngine.Object.FindAnyObjectByType<FeedbackHowlerAI>();
        }

        private void InitializeAudioMixerReferences()
        {
            if (masterMixer == null)
            {
                masterMixer = Resources.Load<AudioMixer>("AM_DecibelRow");
#if UNITY_EDITOR
                if (masterMixer == null)
                {
                    masterMixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/_Project/Audio/AM_DecibelRow.mixer");
                }
#endif
            }

            if (masterMixer != null)
            {
                if (snapshotChaoticStreet == null) snapshotChaoticStreet = masterMixer.FindSnapshot("Snapshot_ChaoticStreet");
                if (snapshotMutedSanctuary == null) snapshotMutedSanctuary = masterMixer.FindSnapshot("Snapshot_MutedSanctuary");
                if (snapshotTremorHazard == null) snapshotTremorHazard = masterMixer.FindSnapshot("Snapshot_TremorHazard");

                // Locate bus groups if not explicitly assigned
                if (stemCacophonyGroup == null) stemCacophonyGroup = FindGroup(masterMixer, "Stem_Cacophony");
                if (stemLullabyGroup == null) stemLullabyGroup = FindGroup(masterMixer, "Stem_Lullaby");
                if (sfxTapsGroup == null) sfxTapsGroup = FindGroup(masterMixer, "SFX_Taps");
                if (sfxResonanceGroup == null) sfxResonanceGroup = FindGroup(masterMixer, "SFX_Resonance");
                if (sfxCreaturesGroup == null) sfxCreaturesGroup = FindGroup(masterMixer, "SFX_Creatures");
                if (sfxAcousticGroup == null) sfxAcousticGroup = FindGroup(masterMixer, "SFX_Acoustic");

#if UNITY_EDITOR
                var so = new UnityEditor.SerializedObject(masterMixer);
                var prop = so.FindProperty("m_ExposedParameters");
                if (prop != null)
                {
                    for (int i = 0; i < prop.arraySize; i++)
                    {
                        var nameProp = prop.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                        if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
                        {
                            exposedParameters.Add(nameProp.stringValue);
                        }
                    }
                }
#endif
            }

            // Pre-allocate snapshot blending array: [0] = Chaotic, [1] = Muted, [2] = Tremor
            if (snapshotChaoticStreet != null && snapshotMutedSanctuary != null && snapshotTremorHazard != null)
            {
                baselineSnapshots = new AudioMixerSnapshot[3]
                {
                    snapshotChaoticStreet,
                    snapshotMutedSanctuary,
                    snapshotTremorHazard
                };
            }
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string groupName)
        {
            if (mixer == null) return null;
            AudioMixerGroup[] matches = mixer.FindMatchingGroups(groupName);
            for (int i = 0; i < matches.Length; i++)
            {
                if (matches[i].name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                {
                    return matches[i];
                }
            }
            return matches.Length > 0 ? matches[0] : null;
        }

        private void InitializeAudioPool()
        {
            sourcePool = new PooledSource[audioSourcePoolSize];
            GameObject poolRoot = new GameObject("AcousticAudioSourcePool");
            poolRoot.transform.SetParent(transform, false);

            for (int i = 0; i < audioSourcePoolSize; i++)
            {
                GameObject sourceObj = new GameObject($"PooledSource_{i:D2}");
                sourceObj.transform.SetParent(poolRoot.transform, false);

                AudioSource src = sourceObj.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f; // Pure 2D crisp diorama mix
                src.loop = false;

                if (sfxAcousticGroup != null)
                {
                    src.outputAudioMixerGroup = sfxAcousticGroup;
                }

                sourcePool[i] = new PooledSource
                {
                    Source = src,
                    CurrentPriority = 255,
                    ReleaseTime = 0f
                };
            }
        }

        // =========================================================================
        // 2. Synchronized Continuous Stems
        // =========================================================================

        private void StartSynchronizedStems()
        {
            if (cacophonySource == null)
            {
                GameObject cacophonyObj = new GameObject("Stem_Cacophony_Source");
                cacophonyObj.transform.SetParent(transform, false);
                cacophonySource = cacophonyObj.AddComponent<AudioSource>();
            }

            if (lullabySource == null)
            {
                GameObject lullabyObj = new GameObject("Stem_Lullaby_Source");
                lullabyObj.transform.SetParent(transform, false);
                lullabySource = lullabyObj.AddComponent<AudioSource>();
            }

            if (stemCacophonyGroup != null) cacophonySource.outputAudioMixerGroup = stemCacophonyGroup;
            if (stemLullabyGroup != null) lullabySource.outputAudioMixerGroup = stemLullabyGroup;

            cacophonySource.loop = true;
            lullabySource.loop = true;

            cacophonySource.clip = cacophonyStemClip;
            lullabySource.clip = lullabyStemClip;

            // Start synchronized on precise future DSP boundary
            double scheduledStartTime = AudioSettings.dspTime + 0.15;
            cacophonySource.PlayScheduled(scheduledStartTime);
            lullabySource.PlayScheduled(scheduledStartTime);
        }

        // =========================================================================
        // 3. Event Subscriptions
        // =========================================================================

        private void RegisterEventSubscriptions()
        {
            if (towerManager != null)
            {
                towerManager.OnBlockAnchored += HandleBlockAnchored;
                towerManager.OnOverhangSliced += HandleOverhangSliced;
                towerManager.OnTowerCollapsed += HandleTowerCollapsed;
                towerManager.OnHeightChanged += HandleHeightChanged;
            }

            if (rhythmManager != null)
            {
                rhythmManager.OnSilenceChainChanged += HandleSilenceChainChanged;
                rhythmManager.OnInputScored += HandleInputScored;
                rhythmManager.OnMissedBeat += HandleMissedBeat;
            }

            if (feedbackHowler != null)
            {
                feedbackHowler.OnShriekLaunched += HandleHowlerShriek;
                feedbackHowler.OnCounterOverloadSuccess += HandleHowlerShatter;
                feedbackHowler.OnHowlerDispersed += HandleHowlerDispersed;
            }
        }

        private void UnregisterEventSubscriptions()
        {
            if (towerManager != null)
            {
                towerManager.OnBlockAnchored -= HandleBlockAnchored;
                towerManager.OnOverhangSliced -= HandleOverhangSliced;
                towerManager.OnTowerCollapsed -= HandleTowerCollapsed;
                towerManager.OnHeightChanged -= HandleHeightChanged;
            }

            if (rhythmManager != null)
            {
                rhythmManager.OnSilenceChainChanged -= HandleSilenceChainChanged;
                rhythmManager.OnInputScored -= HandleInputScored;
                rhythmManager.OnMissedBeat -= HandleMissedBeat;
            }

            if (feedbackHowler != null)
            {
                feedbackHowler.OnShriekLaunched -= HandleHowlerShriek;
                feedbackHowler.OnCounterOverloadSuccess -= HandleHowlerShatter;
                feedbackHowler.OnHowlerDispersed -= HandleHowlerDispersed;
            }
        }

        // =========================================================================
        // 4. Core Audio Engine Calculations (Silence Ratio & Height Muffling)
        // =========================================================================

        /// <summary>
        /// Calculates the exact Silence Ratio: (Total Muted Amber Blocks) / (Total Spawned Blocks).
        /// </summary>
        public void RecalculateSilenceRatio()
        {
            if (towerManager == null) return;

            var anchored = towerManager.AnchoredBlocks;
            int totalAnchored = anchored != null ? anchored.Count : 0;
            int totalSpawned = totalAnchored + (towerManager.ActiveBlock != null ? 1 : 0);

            if (totalSpawned == 0)
            {
                targetSilenceRatio = 0f;
                return;
            }

            int mutedAmberCount = 0;
            for (int i = 0; i < totalAnchored; i++)
            {
                HushBlock block = anchored[i];
                if (block != null && block.IsSettled)
                {
                    mutedAmberCount++;
                }
            }

            // Base physical ratio
            float baseRatio = (float)mutedAmberCount / totalSpawned;

            // Optional rhythm streak stabilization weighting
            int chain = rhythmManager != null ? rhythmManager.SilenceChain : 0;
            float streakBonus = chain >= 5 ? Mathf.Min(0.15f, chain * 0.015f) : 0f;

            targetSilenceRatio = Mathf.Clamp01(baseRatio + streakBonus);
        }

        private void UpdateSilenceRatio(bool immediate)
        {
            RecalculateSilenceRatio();
            if (immediate)
            {
                currentSilenceRatio = targetSilenceRatio;
                silenceRatioVelocity = 0f;
                OnSilenceRatioUpdated?.Invoke(currentSilenceRatio);
            }
        }

        private void UpdateSilenceRatioSmooth()
        {
            if (!Mathf.Approximately(currentSilenceRatio, targetSilenceRatio))
            {
                currentSilenceRatio = Mathf.SmoothDamp(
                    currentSilenceRatio,
                    targetSilenceRatio,
                    ref silenceRatioVelocity,
                    0.35f
                );
                OnSilenceRatioUpdated?.Invoke(currentSilenceRatio);
            }
        }

        /// <summary>
        /// Modulates the street lowpass cutoff frequency based on tower vertical ascent.
        /// As Noa climbs decibel row towards the clouds, traffic rumble recedes into atmospheric calm.
        /// </summary>
        private void UpdateHeightMuffling(bool immediate)
        {
            int height = towerManager != null ? towerManager.CurrentHeight : 0;
            float heightRatio = Mathf.Clamp01((float)height / Mathf.Max(1, targetAscentLayers));

            // Logarithmic human frequency roll-off
            targetLowpassCutoff = Mathf.Lerp(groundLowpassCutoff, sanctuaryLowpassCutoff, heightRatio * heightRatio);

            if (immediate)
            {
                currentLowpassCutoff = targetLowpassCutoff;
                lowpassVelocity = 0f;
                SetMixerFloatSafe(streetLowpassParam, currentLowpassCutoff);
            }
        }

        private void UpdateHeightMufflingSmooth()
        {
            if (!Mathf.Approximately(currentLowpassCutoff, targetLowpassCutoff))
            {
                currentLowpassCutoff = Mathf.SmoothDamp(
                    currentLowpassCutoff,
                    targetLowpassCutoff,
                    ref lowpassVelocity,
                    0.5f
                );
                SetMixerFloatSafe(streetLowpassParam, currentLowpassCutoff);
            }
        }

        private void UpdateTremorState()
        {
            if (isTremorActive)
            {
                tremorTimer -= Time.deltaTime;
                if (tremorTimer <= 0f)
                {
                    isTremorActive = false;
                    OnTremorStateChanged?.Invoke(false);
                }
            }

            float targetTremorWeight = isTremorActive ? 1.0f : 0.0f;
            tremorHazardWeight = Mathf.SmoothDamp(
                tremorHazardWeight,
                targetTremorWeight,
                ref tremorHazardVelocity,
                isTremorActive ? 0.08f : 0.45f
            );
        }

        /// <summary>
        /// Seamless zero-allocation snapshot blending between Chaotic Street, Muted Sanctuary,
        /// and Tremor Hazard states.
        /// </summary>
        private void ApplyMixerSnapshotBlend()
        {
            if (masterMixer == null || baselineSnapshots == null || baselineSnapshots.Length != 3) return;

            // Weights calculation:
            // Non-tremor portion partitioned between Chaotic and Muted by silence ratio
            float normalWeight = 1.0f - tremorHazardWeight;
            snapshotWeights[0] = (1.0f - currentSilenceRatio) * normalWeight; // Snapshot_ChaoticStreet
            snapshotWeights[1] = currentSilenceRatio * normalWeight;          // Snapshot_MutedSanctuary
            snapshotWeights[2] = tremorHazardWeight;                          // Snapshot_TremorHazard

            masterMixer.TransitionToSnapshots(baselineSnapshots, snapshotWeights, Time.deltaTime);

            // Logarithmic volume fallback adjustments for maximum headroom
            float cacophonyDb = LinearToDecibels((1f - currentSilenceRatio) * (1f - tremorHazardWeight * 0.75f));
            float lullabyDb = LinearToDecibels(currentSilenceRatio * (1f - tremorHazardWeight * 0.75f));

            SetMixerFloatSafe(cacophonyVolumeParam, cacophonyDb);
            SetMixerFloatSafe(lullabyVolumeParam, lullabyDb);
        }

        private void SetMixerFloatSafe(string paramName, float value)
        {
            if (masterMixer != null && !string.IsNullOrEmpty(paramName))
            {
#if UNITY_EDITOR
                if (!exposedParameters.Contains(paramName)) return;
#endif
                masterMixer.SetFloat(paramName, value);
            }
        }

        /// <summary>
        /// Converts a [0.0, 1.0] linear amplitude into standard logarithmic decibels (-80 dB to 0 dB).
        /// </summary>
        public static float LinearToDecibels(float linear, float minDb = -80f)
        {
            if (linear <= 0.0001f) return minDb;
            return Mathf.Clamp(20f * Mathf.Log10(linear), minDb, 0f);
        }

        // =========================================================================
        // 5. Diatonic Pitch Scaling & High-Frequency SFX Dispatchers
        // =========================================================================

        /// <summary>
        /// Evaluates the diatonic pitch ratio for crystallization chimes based on silence chain:
        /// 1x -> Root (1.0), 2-4x -> Major 3rd (1.26), 5-9x -> 5th (1.50), 10x+ -> Octave (2.0).
        /// </summary>
        public static float GetDiatonicChimePitch(int silenceChain)
        {
            if (silenceChain <= 1) return PitchRoot;
            if (silenceChain <= 4) return PitchMajorThird;
            if (silenceChain <= 9) return PitchPerfectFifth;
            if (silenceChain <= 14) return PitchOctave;
            return PitchOctaveThird;
        }

        /// <summary>
        /// Dispatches the amber trowel strike SFX and diatonic crystallization chime.
        /// </summary>
        public void PlayTrowelStrike(bool isPerfect)
        {
            // 1. Crisp stone/amber contact transient
            PlayPooledClip(
                clip: trowelStrikeClip,
                group: sfxTapsGroup,
                volume: isPerfect ? 1.0f : 0.85f,
                pitch: UnityEngine.Random.Range(0.97f, 1.03f),
                priority: PriorityPlacement
            );

            // 2. Diatonic crystalline chime scaled by current silence chain
            int chain = rhythmManager != null ? rhythmManager.SilenceChain : 0;
            float chimePitch = GetDiatonicChimePitch(chain);

            PlayPooledClip(
                clip: crystallizationChimeClip,
                group: sfxResonanceGroup,
                volume: isPerfect ? 0.95f : 0.75f,
                pitch: chimePitch,
                priority: PriorityPlacement
            );

            // 3. Perfect Harmonic Sparkle overtone (Double octave chime)
            if (isPerfect && perfectHarmonicSparkleClip != null)
            {
                PlayPooledClip(
                    clip: perfectHarmonicSparkleClip,
                    group: sfxResonanceGroup,
                    volume: 0.85f,
                    pitch: chimePitch * 1.5f, // Quintal harmonic sparkle
                    priority: PriorityPlacement + 1
                );
            }
        }

        /// <summary>
        /// Dispatches the anti-gravity whoosh when sheared overhang chunks float into the sky.
        /// </summary>
        public void PlayAntiGravityWhoosh()
        {
            PlayPooledClip(
                clip: antiGravityWhooshClip,
                group: sfxResonanceGroup,
                volume: 0.8f,
                pitch: UnityEngine.Random.Range(0.92f, 1.15f),
                priority: PriorityPhysics
            );
        }

        /// <summary>
        /// Triggers the anti-gravity tremor hazard state, dipping music stems and playing
        /// an ominous sub-bass resonance rumble and cyan static buzz.
        /// </summary>
        public void PlayTremorHazard(float duration = 1.5f)
        {
            TriggerTremorHazard(duration);

            PlayPooledClip(
                clip: tremorHazardClip,
                group: sfxResonanceGroup,
                volume: 1.0f,
                pitch: UnityEngine.Random.Range(0.95f, 1.05f),
                priority: PriorityHazard
            );

            PlayPooledClip(
                clip: cyanStaticBuzzClip,
                group: sfxResonanceGroup,
                volume: 0.7f,
                pitch: UnityEngine.Random.Range(1.1f, 1.35f),
                priority: PriorityHazard + 2
            );
        }

        /// <summary>
        /// Dispatches the resonant overload shatter when the Feedback Howler is dispersed.
        /// </summary>
        public void PlayHowlerShatter()
        {
            PlayPooledClip(
                clip: howlerShatterClip,
                group: sfxCreaturesGroup,
                volume: 1.1f,
                pitch: UnityEngine.Random.Range(0.96f, 1.04f),
                priority: PriorityCreature
            );
        }

        /// <summary>
        /// Activates the snapshot tremor state for the specified duration before releasing.
        /// </summary>
        public void TriggerTremorHazard(float duration = 1.5f)
        {
            isTremorActive = true;
            tremorTimer = Mathf.Max(tremorTimer, duration);
            OnTremorStateChanged?.Invoke(true);
        }

        // =========================================================================
        // 6. Zero-Allocation AudioSource Object Pooling
        // =========================================================================

        private void PlayPooledClip(AudioClip clip, AudioMixerGroup group, float volume, float pitch, int priority)
        {
            if (clip == null || sourcePool == null || sourcePool.Length == 0) return;

            float now = Time.time;
            int selectedIndex = -1;
            int lowestActivePriority = -1;
            float oldestReleaseTime = float.MaxValue;

            // 1. Search for an idle AudioSource
            for (int i = 0; i < sourcePool.Length; i++)
            {
                int idx = (poolIndexCursor + i) % sourcePool.Length;
                if (!sourcePool[idx].Source.isPlaying || now >= sourcePool[idx].ReleaseTime)
                {
                    selectedIndex = idx;
                    break;
                }

                // Track lowest priority candidate in case pre-emption is required
                if (sourcePool[idx].CurrentPriority > lowestActivePriority)
                {
                    lowestActivePriority = sourcePool[idx].CurrentPriority;
                    oldestReleaseTime = sourcePool[idx].ReleaseTime;
                    selectedIndex = idx;
                }
                else if (sourcePool[idx].CurrentPriority == lowestActivePriority && sourcePool[idx].ReleaseTime < oldestReleaseTime)
                {
                    oldestReleaseTime = sourcePool[idx].ReleaseTime;
                    selectedIndex = idx;
                }
            }

            // 2. Pre-emption check: only override active source if caller priority is strictly higher (lower integer)
            if (selectedIndex >= 0)
            {
                if (sourcePool[selectedIndex].Source.isPlaying && priority > sourcePool[selectedIndex].CurrentPriority)
                {
                    // Caller does not have sufficient priority to interrupt
                    return;
                }

                poolIndexCursor = (selectedIndex + 1) % sourcePool.Length;

                AudioSource src = sourcePool[selectedIndex].Source;
                src.Stop();
                src.clip = clip;
                src.volume = Mathf.Clamp01(volume);
                src.pitch = Mathf.Clamp(pitch, 0.25f, 3.5f);
                src.priority = priority;
                if (group != null) src.outputAudioMixerGroup = group;

                src.Play();

                sourcePool[selectedIndex].CurrentPriority = priority;
                sourcePool[selectedIndex].ReleaseTime = now + (clip.length / Mathf.Max(0.1f, Mathf.Abs(pitch))) + 0.05f;
            }
        }

        // =========================================================================
        // 7. Event Handlers
        // =========================================================================

        private void HandleBlockAnchored(HushBlock block, TimingResult result)
        {
            bool isPerfect = result == TimingResult.Perfect;
            PlayTrowelStrike(isPerfect);
            RecalculateSilenceRatio();
        }

        private void HandleOverhangSliced(float sliceVolume)
        {
            PlayAntiGravityWhoosh();
            RecalculateSilenceRatio();
        }

        private void HandleHeightChanged(int newHeight)
        {
            UpdateHeightMuffling(immediate: false);
            RecalculateSilenceRatio();
        }

        private void HandleSilenceChainChanged(int chain)
        {
            RecalculateSilenceRatio();
        }

        private void HandleInputScored(TimingResult result)
        {
            if (result == TimingResult.Miss)
            {
                PlayTremorHazard(1.2f);
            }
            RecalculateSilenceRatio();
        }

        private void HandleMissedBeat(long beatIndex)
        {
            PlayTremorHazard(1.4f);
            RecalculateSilenceRatio();
        }

        private void HandleTowerCollapsed()
        {
            PlayTremorHazard(3.0f);
            targetSilenceRatio = 0f;
            currentSilenceRatio = 0f;
        }

        private void HandleHowlerShriek()
        {
            PlayTremorHazard(1.6f);
        }

        private void HandleHowlerShatter()
        {
            PlayHowlerShatter();
        }

        private void HandleHowlerDispersed()
        {
            PlayHowlerShatter();
            RecalculateSilenceRatio();
        }

        // =========================================================================
        // 8. Procedural Fallback Audio Synthesis
        // =========================================================================

        /// <summary>
        /// Automatically generates placeholder clips if none are assigned in the inspector,
        /// ensuring the audio system functions out-of-the-box in editor and runtime.
        /// </summary>
        private void EnsureFallbackAudioClips()
        {
            if (trowelStrikeClip == null) trowelStrikeClip = CreateTransientClickClip("Procedural_TrowelClick");
            if (crystallizationChimeClip == null) crystallizationChimeClip = CreateSineChimeClip("Procedural_CrystalChime", 880f);
            if (perfectHarmonicSparkleClip == null) perfectHarmonicSparkleClip = CreateSineChimeClip("Procedural_SparkleChime", 1760f);
            if (antiGravityWhooshClip == null) antiGravityWhooshClip = CreateWhooshClip("Procedural_AntiGravityWhoosh");
            if (tremorHazardClip == null) tremorHazardClip = CreateSubBassRumbleClip("Procedural_TremorHazard");
            if (cyanStaticBuzzClip == null) cyanStaticBuzzClip = CreateStaticBuzzClip("Procedural_CyanStatic");
            if (howlerShatterClip == null) howlerShatterClip = CreateShatterBurstClip("Procedural_HowlerShatter");

            if (cacophonyStemClip == null) cacophonyStemClip = CreateAmbientStemClip("Procedural_CacophonyStem", isCacophony: true);
            if (lullabyStemClip == null) lullabyStemClip = CreateAmbientStemClip("Procedural_LullabyStem", isCacophony: false);
        }

        private static AudioClip CreateSineChimeClip(string name, float baseFreq)
        {
            int sampleRate = 44100;
            float duration = 0.8f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-5.5f * t);
                // Fundamental + 2nd harmonic
                float wave = Mathf.Sin(2f * Mathf.PI * baseFreq * t) * 0.7f +
                             Mathf.Sin(2f * Mathf.PI * baseFreq * 2.01f * t) * 0.3f;
                samples[i] = wave * envelope * 0.5f;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateTransientClickClip(string name)
        {
            int sampleRate = 44100;
            float duration = 0.06f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-55f * t);
                float wave = Mathf.Sin(2f * Mathf.PI * 1400f * t);
                samples[i] = wave * envelope * 0.7f;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateWhooshClip(string name)
        {
            int sampleRate = 44100;
            float duration = 0.55f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / duration;
                float envelope = Mathf.Sin(Mathf.PI * t);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.35f;
                float freq = Mathf.Lerp(250f, 950f, t);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / sampleRate)) * 0.3f;
                samples[i] = (noise + tone) * envelope * 0.6f;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateSubBassRumbleClip(string name)
        {
            int sampleRate = 44100;
            float duration = 1.4f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / sampleCount;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Pow(progress, 0.4f)) * Mathf.Exp(-1.2f * t);
                float wobble = 1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 5f * t);
                float wave = Mathf.Sin(2f * Mathf.PI * 45f * wobble * t);
                samples[i] = Mathf.Clamp(wave * envelope * 0.85f, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateStaticBuzzClip(string name)
        {
            int sampleRate = 44100;
            float duration = 0.45f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-6f * t);
                float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 180f * t));
                float noise = UnityEngine.Random.value * 2f - 1f;
                samples[i] = (square * 0.4f + noise * 0.6f) * envelope * 0.5f;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateShatterBurstClip(string name)
        {
            int sampleRate = 44100;
            float duration = 0.85f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-7f * t);
                float ring1 = Mathf.Sin(2f * Mathf.PI * 2200f * t);
                float ring2 = Mathf.Sin(2f * Mathf.PI * 3420f * t);
                float noise = UnityEngine.Random.value * 2f - 1f;
                samples[i] = (ring1 * 0.35f + ring2 * 0.35f + noise * 0.3f) * envelope * 0.75f;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateAmbientStemClip(string name, bool isCacophony)
        {
            int sampleRate = 44100;
            float duration = 2.0f; // Short loopable stem buffer
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                if (isCacophony)
                {
                    // Street din: 60Hz hum + syncopated ticks
                    float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.25f;
                    float tick = Mathf.Sin(2f * Mathf.PI * 4f * t) > 0.98f ? 0.3f : 0f;
                    samples[i] = (hum + tick) * 0.3f;
                }
                else
                {
                    // Lullaby peace: Warm 220Hz (A3) gentle sine drone
                    float drone = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.2f;
                    float drone5 = Mathf.Sin(2f * Mathf.PI * 330f * t) * 0.1f;
                    samples[i] = (drone + drone5) * 0.3f;
                }
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
