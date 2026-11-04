using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// District Progression and Decibel Row Rules Manager for Hush Blocks.
    /// Manages tempo escalations (BPM curves), syncopated double-beat frequency,
    /// Feedback Howler ambush events, and Rooftop Sanctuary Clearance sequence:
    /// stack locking, atmospheric lighting transitions to deep midnight indigo,
    /// honey-amber lantern illumination, and Sanctuary Garden instantiation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DistrictProgression : MonoBehaviour
    {
        [Header("District Identity")]
        [Tooltip("Name of the active Decibel Row district (e.g. 'Whisper Row', 'Resonant Apex').")]
        [SerializeField] private string districtName = "District 1: Whisper Row";
        [Tooltip("Target elevation (stacked layers) required to clear the district and unlock the sanctuary.")]
        [SerializeField, Range(5, 50)] private int targetTowerHeight = 12;
        [Tooltip("Minimum unbroken silence chain required for pristine sanctuary clearance.")]
        [SerializeField, Min(1)] private int requiredSilenceChain = 5;

        [Header("Tempo Escalation (BPM Curve)")]
        [Tooltip("Starting tempo at ground foundation level.")]
        [SerializeField, Min(40f)] private float startingBpm = 110f;
        [Tooltip("Peak tempo when reaching the district summit.")]
        [SerializeField, Min(60f)] private float apexBpm = 145f;
        [Tooltip("Non-linear progression curve mapping normalized height [0, 1] to tempo.")]
        [SerializeField] private AnimationCurve tempoCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Howler Ambush & Syncopation")]
        [Tooltip("Base probability [0, 1] that an incoming layer is a syncopated double-beat block.")]
        [SerializeField, Range(0f, 1f)] private float doubleBeatFrequency = 0.25f;
        [Tooltip("Layer heights where a Feedback Howler ambush occurs.")]
        [SerializeField] private int[] howlerAmbushHeights = new int[] { 6, 10 };
        [Tooltip("Temporary tempo acceleration multiplier during a Howler ambush.")]
        [SerializeField, Range(1.1f, 1.5f)] private float howlerSurgeMultiplier = 1.25f;

        [Header("Rooftop Sanctuary Clearance")]
        [Tooltip("Prefab spawned at the apex of the tower upon successful clearance.")]
        [SerializeField] private GameObject rooftopSanctuaryGardenPrefab;
        [Tooltip("Main directional key light in the scene.")]
        [SerializeField] private Light mainDirectionalLight;
        [Tooltip("Deep midnight indigo ambient sky color during sanctuary peace (#0B0E1A).")]
        [SerializeField] private Color midnightIndigoColor = new Color(0.043f, 0.055f, 0.102f, 1f);
        [Tooltip("Warm honey-amber light color for sanctuary celebration (#FFB300).")]
        [SerializeField] private Color honeyAmberLightColor = new Color(1f, 0.702f, 0f, 1f);
        [Tooltip("Duration in seconds for the smooth environmental clearance transition.")]
        [SerializeField, Min(1f)] private float clearanceTransitionSeconds = 2.5f;

        [Header("System Dependencies")]
        [SerializeField] private TowerManager towerManager;
        [SerializeField] private RhythmManager rhythmManager;

        // Runtime Tracking
        private int currentSilenceChain;
        private bool isClearingSanctuary;
        private readonly HashSet<int> triggeredAmbushHeights = new HashSet<int>();

        // Public Accessors
        public string DistrictName => districtName;
        public int TargetTowerHeight => targetTowerHeight;
        public int CurrentSilenceChain => currentSilenceChain;
        public float DoubleBeatFrequency => doubleBeatFrequency;
        public bool IsCleared => isClearingSanctuary;

        // Event Delegates
        public event Action<int, float> OnDistrictBpmChanged;
        public event Action<int> OnHowlerAmbushTriggered;
        public event Action OnSanctuaryReached;

        private void Awake()
        {
            if (towerManager == null) towerManager = UnityEngine.Object.FindAnyObjectByType<TowerManager>();
            if (rhythmManager == null) rhythmManager = UnityEngine.Object.FindAnyObjectByType<RhythmManager>();
        }

        private void OnEnable()
        {
            if (towerManager != null)
            {
                towerManager.OnBlockAnchored += HandleBlockAnchored;
                towerManager.OnHeightChanged += HandleHeightChanged;
            }

            if (rhythmManager != null)
            {
                rhythmManager.OnSilenceChainChanged += HandleSilenceChainChanged;
            }
        }

        private void OnDisable()
        {
            if (towerManager != null)
            {
                towerManager.OnBlockAnchored -= HandleBlockAnchored;
                towerManager.OnHeightChanged -= HandleHeightChanged;
            }

            if (rhythmManager != null)
            {
                rhythmManager.OnSilenceChainChanged -= HandleSilenceChainChanged;
            }
        }

        private void Start()
        {
            // Apply initial starting tempo
            UpdateDistrictTempo(height: 0);
        }

        private void HandleHeightChanged(int currentHeight)
        {
            if (isClearingSanctuary) return;

            UpdateDistrictTempo(currentHeight);
            CheckHowlerAmbush(currentHeight);
        }

        private void HandleBlockAnchored(HushBlock block, TimingResult result)
        {
            if (isClearingSanctuary || towerManager == null) return;

            int currentHeight = towerManager.CurrentHeight;

            // Check if district target height is attained
            if (currentHeight >= targetTowerHeight)
            {
                if (currentSilenceChain >= requiredSilenceChain)
                {
                    StartCoroutine(ExecuteRooftopSanctuaryClearance());
                }
            }
        }

        private void HandleSilenceChainChanged(int chain)
        {
            currentSilenceChain = chain;
        }

        // ---------- Tempo Escalation & Howler Ambush ----------

        private void UpdateDistrictTempo(int height)
        {
            if (rhythmManager == null) return;

            float normalizedHeight = Mathf.Clamp01((float)height / targetTowerHeight);
            float evaluatedCurve = tempoCurve.Evaluate(normalizedHeight);
            float targetBpm = Mathf.Lerp(startingBpm, apexBpm, evaluatedCurve);

            if (rhythmManager.DifficultyProfile != null)
            {
                rhythmManager.DifficultyProfile.SetBpm(targetBpm);
            }

            OnDistrictBpmChanged?.Invoke(height, targetBpm);
        }

        private void CheckHowlerAmbush(int height)
        {
            if (howlerAmbushHeights == null || triggeredAmbushHeights.Contains(height)) return;

            for (int i = 0; i < howlerAmbushHeights.Length; i++)
            {
                if (howlerAmbushHeights[i] == height)
                {
                    triggeredAmbushHeights.Add(height);
                    StartCoroutine(ExecuteHowlerAmbushSurge(height));
                    break;
                }
            }
        }

        private IEnumerator ExecuteHowlerAmbushSurge(int height)
        {
            OnHowlerAmbushTriggered?.Invoke(height);

            // Temporarily spike tempo to simulate acoustic panic / howling resonance
            if (rhythmManager != null && rhythmManager.DifficultyProfile != null)
            {
                float normalBpm = rhythmManager.DifficultyProfile.Bpm;
                float surgedBpm = normalBpm * howlerSurgeMultiplier;

                rhythmManager.DifficultyProfile.SetBpm(surgedBpm);
                yield return new WaitForSeconds(3.5f);
                rhythmManager.DifficultyProfile.SetBpm(normalBpm);
            }
        }

        // ---------- Rooftop Sanctuary Clearance Sequence ----------

        private IEnumerator ExecuteRooftopSanctuaryClearance()
        {
            isClearingSanctuary = true;

            // 1. Lock the tower stack against further input or oscillation
            if (towerManager != null)
            {
                towerManager.LockTowerPermanently();
            }

            // 2. Spawn Rooftop Sanctuary Garden at the apex
            if (rooftopSanctuaryGardenPrefab != null && towerManager != null)
            {
                Vector3 gardenPos = new Vector3(
                    towerManager.transform.position.x,
                    (targetTowerHeight * 1.0f) + 0.5f,
                    towerManager.transform.position.z
                );
                Instantiate(rooftopSanctuaryGardenPrefab, gardenPos, Quaternion.identity, towerManager.transform);
            }

            // 3. Smooth atmospheric lighting transition to Midnight Indigo & Honey Amber
            float timer = 0f;
            Color initialAmbient = RenderSettings.ambientLight;
            Color initialLightColor = mainDirectionalLight != null ? mainDirectionalLight.color : Color.white;
            float initialIntensity = mainDirectionalLight != null ? mainDirectionalLight.intensity : 1f;

            while (timer < clearanceTransitionSeconds)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / clearanceTransitionSeconds);

                RenderSettings.ambientLight = Color.Lerp(initialAmbient, midnightIndigoColor, t);

                if (mainDirectionalLight != null)
                {
                    mainDirectionalLight.color = Color.Lerp(initialLightColor, honeyAmberLightColor, t);
                    mainDirectionalLight.intensity = Mathf.Lerp(initialIntensity, 1.25f, t);
                }

                yield return null;
            }

            RenderSettings.ambientLight = midnightIndigoColor;
            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.color = honeyAmberLightColor;
            }

            OnSanctuaryReached?.Invoke();
        }
    }
}
