using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HushBlocks.Gameplay;

namespace HushBlocks.UI
{
    /// <summary>
    /// Master Diegetic HUD Controller for Hush Blocks in Unity 6 (URP).
    /// Orchestrates the four primary tactile voxel UI components:
    /// 1. The Beat Compass (Concentric Timing Ring)
    /// 2. The Acoustic Mortar Vial (SilenceChainMeter)
    /// 3. The Tremor Seismograph (Anti-Gravity Stability Warning)
    /// 4. The Elevation Altimeter (Sanctuary Progress)
    /// Plus zero-allocation pooled 3D micro-feedback floating text.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AcousticHUDController : MonoBehaviour
    {
        [Header("Tactile Voxel Palette")]
        private static readonly Color VelvetyCharcoal = new Color(0.11f, 0.118f, 0.141f, 1f); // #1C1E24
        private static readonly Color HoneyAmber = new Color(1.0f, 0.702f, 0.0f, 1f);         // #FFB300
        private static readonly Color RadiantCyan = new Color(0.0f, 0.941f, 1.0f, 1f);        // #00F0FF
        private static readonly Color GoldSparkle = new Color(1.0f, 0.88f, 0.5f, 1f);         // #FFE082
        private static readonly Color HazardGlitchRed = new Color(1.0f, 0.22f, 0.35f, 1f);

        [Header("Canvas & Projection Camera")]
        [Tooltip("Main gameplay camera for diorama isometric world-to-screen projection.")]
        [SerializeField] private Camera dioramaCamera;
        [Tooltip("Root canvas holding all diegetic HUD elements.")]
        [SerializeField] private Canvas hudCanvas;

        [Header("1. The Beat Compass (Concentric Timing Reticle)")]
        [Tooltip("Root RectTransform of the reticle tracking the active landing zone.")]
        [SerializeField] private RectTransform reticleRootRect;
        [Tooltip("Outer contracting ring collapsing on the exact quarter-note beat.")]
        [SerializeField] private RectTransform contractingRingRect;
        [Tooltip("Image component of the outer ring for dynamic cyan/amber color shifts.")]
        [SerializeField] private Image contractingRingImage;
        [Tooltip("Inner stationary frame indicating voxel placement bounds.")]
        [SerializeField] private Image innerFrameImage;
        [Tooltip("Maximum expansion scale of the outer ring at beat start (e.g. 2.4x).")]
        [SerializeField, Range(1.5f, 3.5f)] private float ringExpandedScale = 2.2f;
        [Tooltip("Contracted scale at the exact moment of beat contact (1.0x).")]
        [SerializeField, Range(0.8f, 1.2f)] private float ringContractedScale = 1.0f;

        [Header("2. The Acoustic Mortar Vial")]
        [Tooltip("Component driving the vertical crystal vial and liquid amber fill.")]
        [SerializeField] private SilenceChainMeter silenceChainMeter;

        [Header("3. The Tremor Seismograph (Anti-Gravity Stability Warning)")]
        [Tooltip("Horizontal container for the bottom stability seismograph.")]
        [SerializeField] private RectTransform seismographBarRect;
        [Tooltip("Image or waveform line representing stack stability.")]
        [SerializeField] private Image seismographWaveformImage;
        [Tooltip("Warning text or banner displaying hazard states.")]
        [SerializeField] private TextMeshProUGUI seismographAlertText;
        [Tooltip("CanvasGroup for strobing cyan hazard warning during imminent collapse.")]
        [SerializeField] private CanvasGroup seismographAlertCanvasGroup;

        [Header("4. The Elevation Altimeter (Sanctuary Progress)")]
        [Tooltip("Vertical container holding floor tick markers.")]
        [SerializeField] private RectTransform altimeterLadderRect;
        [Tooltip("Prefab or template for individual floor tick marks.")]
        [SerializeField] private Image floorTickPrefab;
        [Tooltip("Text displaying current floor count (e.g. '04 / 12F').")]
        [SerializeField] private TextMeshProUGUI floorCountText;
        [Tooltip("Total floors in the current district before reaching Sanctuary.")]
        [SerializeField, Min(4)] private int totalTargetFloors = 12;

        [Header("5. Micro-Feedback Floating Text Pool")]
        [Tooltip("Prefab for floating pixel-font text.")]
        [SerializeField] private TextMeshProUGUI floatingTextPrefab;
        [Tooltip("Number of pre-allocated floating text items (Zero GC).")]
        [SerializeField, Range(4, 16)] private int floatingTextPoolSize = 8;
        [Tooltip("Vertical pixel drift speed for floating feedback text.")]
        [SerializeField, Min(10f)] private float floatingTextDriftSpeed = 45f;
        [Tooltip("Lifetime of floating feedback text before recycling.")]
        [SerializeField, Range(0.3f, 1.5f)] private float floatingTextDuration = 0.75f;

        [Header("Dependencies")]
        [SerializeField] private RhythmManager rhythmManager;
        [SerializeField] private TowerManager towerManager;
        [SerializeField] private AcousticAudioManager audioManager;

        // ---------- Zero-Allocation Floating Text Pool ----------
        private struct FloatingTextInstance
        {
            public TextMeshProUGUI TextComponent;
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector3 WorldAnchorPosition;
            public float ElapsedTime;
            public bool IsActive;
        }
        private FloatingTextInstance[] textPool;
        private int textPoolCursor = 0;

        // Altimeter tick elements
        private Image[] altimeterTicks;

        // Runtime Reticle Tweens
        private Color currentReticleColor = RadiantCyan;
        private Color targetReticleColor = RadiantCyan;
        private float reticlePunchScale = 1.0f;

        // Seismograph State
        private float tremorGlitchTimer = 0f;
        private bool isCriticalHazard = false;

        // Pre-cached text buffers to prevent string allocations in Update
        private static readonly string[] FloorStringCache = new string[32];
        private static readonly string TextPerfect = "PERFECT!";
        private static readonly string TextMuted = "MUTED";
        private static readonly string TextMiss = "MISSED!";
        private static readonly string TextOverload = "CRITICAL INSTABILITY";
        private static readonly string TextStable = "STACK STABLE";

        static AcousticHUDController()
        {
            for (int i = 0; i < FloorStringCache.Length; i++)
            {
                FloorStringCache[i] = $"{i:D2} / 12F";
            }
        }

        private void Awake()
        {
            ResolveDependencies();
            InitializeAltimeterTicks();
            InitializeFloatingTextPool();

            if (seismographAlertCanvasGroup != null)
            {
                seismographAlertCanvasGroup.alpha = 0f;
            }
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
            UpdateAltimeterDisplay(towerManager != null ? towerManager.CurrentHeight : 0);
        }

        private void Update()
        {
            UpdateBeatCompassReticle();
            UpdateSeismographStability();
            UpdateFloatingTextPool();
        }

        // =========================================================================
        // 1. Dependency Resolution & Setup
        // =========================================================================

        private void ResolveDependencies()
        {
            if (dioramaCamera == null) dioramaCamera = Camera.main;
            if (hudCanvas == null) hudCanvas = GetComponentInParent<Canvas>();
            if (rhythmManager == null) rhythmManager = UnityEngine.Object.FindAnyObjectByType<RhythmManager>();
            if (towerManager == null) towerManager = UnityEngine.Object.FindAnyObjectByType<TowerManager>();
            if (audioManager == null) audioManager = UnityEngine.Object.FindAnyObjectByType<AcousticAudioManager>();
            if (silenceChainMeter == null) silenceChainMeter = GetComponentInChildren<SilenceChainMeter>();
        }

        private void InitializeAltimeterTicks()
        {
            if (altimeterLadderRect == null) return;

            altimeterTicks = new Image[totalTargetFloors];
            for (int i = 0; i < totalTargetFloors; i++)
            {
                GameObject tickObj;
                if (floorTickPrefab != null)
                {
                    tickObj = UnityEngine.Object.Instantiate(floorTickPrefab.gameObject, altimeterLadderRect);
                }
                else
                {
                    tickObj = new GameObject($"Tick_{i:D2}");
                    tickObj.transform.SetParent(altimeterLadderRect, false);
                    var img = tickObj.AddComponent<Image>();
                    img.color = VelvetyCharcoal;
                    var rt = tickObj.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(16f, 4f);
                }

                tickObj.name = $"Tick_{i:D2}";
                altimeterTicks[i] = tickObj.GetComponent<Image>();
                altimeterTicks[i].color = VelvetyCharcoal;
            }
        }

        private void InitializeFloatingTextPool()
        {
            textPool = new FloatingTextInstance[floatingTextPoolSize];
            GameObject poolRoot = new GameObject("FloatingTextPool");
            poolRoot.transform.SetParent(transform, false);

            for (int i = 0; i < floatingTextPoolSize; i++)
            {
                GameObject textObj;
                TextMeshProUGUI tmp;

                if (floatingTextPrefab != null)
                {
                    textObj = UnityEngine.Object.Instantiate(floatingTextPrefab.gameObject, poolRoot.transform);
                    tmp = textObj.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    textObj = new GameObject($"FloatingText_{i:D2}");
                    textObj.transform.SetParent(poolRoot.transform, false);
                    tmp = textObj.AddComponent<TextMeshProUGUI>();
                    tmp.fontSize = 20;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.fontStyle = FontStyles.Bold;
                }

                CanvasGroup cg = textObj.GetComponent<CanvasGroup>();
                if (cg == null) cg = textObj.AddComponent<CanvasGroup>();
                cg.alpha = 0f;

                RectTransform rt = textObj.GetComponent<RectTransform>();

                textPool[i] = new FloatingTextInstance
                {
                    TextComponent = tmp,
                    Rect = rt,
                    Group = cg,
                    WorldAnchorPosition = Vector3.zero,
                    ElapsedTime = 0f,
                    IsActive = false
                };
            }
        }

        // =========================================================================
        // 2. Event Subscriptions
        // =========================================================================

        private void RegisterEventSubscriptions()
        {
            if (rhythmManager != null)
            {
                rhythmManager.OnInputScored += HandleInputScored;
                rhythmManager.OnSilenceChainChanged += HandleSilenceChainChanged;
            }

            if (towerManager != null)
            {
                towerManager.OnBlockAnchored += HandleBlockAnchored;
                towerManager.OnHeightChanged += HandleHeightChanged;
                towerManager.OnTowerCollapsed += HandleTowerCollapsed;
            }

            if (audioManager != null)
            {
                audioManager.OnTremorStateChanged += HandleTremorStateChanged;
            }
        }

        private void UnregisterEventSubscriptions()
        {
            if (rhythmManager != null)
            {
                rhythmManager.OnInputScored -= HandleInputScored;
                rhythmManager.OnSilenceChainChanged -= HandleSilenceChainChanged;
            }

            if (towerManager != null)
            {
                towerManager.OnBlockAnchored -= HandleBlockAnchored;
                towerManager.OnHeightChanged -= HandleHeightChanged;
                towerManager.OnTowerCollapsed -= HandleTowerCollapsed;
            }

            if (audioManager != null)
            {
                audioManager.OnTremorStateChanged -= HandleTremorStateChanged;
            }
        }

        // =========================================================================
        // 3. The Beat Compass (Concentric Timing Ring)
        // =========================================================================

        private void UpdateBeatCompassReticle()
        {
            if (reticleRootRect == null || dioramaCamera == null) return;

            // 1. Track 3D world landing zone position
            Vector3 targetWorldPos = Vector3.zero;
            if (towerManager != null && towerManager.ActiveBlock != null)
            {
                targetWorldPos = towerManager.ActiveBlock.transform.position;
            }

            // Project to screen space
            Vector3 screenPos = dioramaCamera.WorldToScreenPoint(targetWorldPos);
            reticleRootRect.position = screenPos;

            // 2. Evaluate concentric ring collapse on quarter-note beat clock
            float beatProgress = rhythmManager != null ? rhythmManager.CurrentBeatProgress : 0f;
            // Inverts so outer ring starts wide and contracts down to 1.0x precisely on beat hit
            float scale = Mathf.Lerp(ringExpandedScale, ringContractedScale, beatProgress);

            if (contractingRingRect != null)
            {
                float finalScale = scale * reticlePunchScale;
                contractingRingRect.localScale = new Vector3(finalScale, finalScale, 1.0f);
            }

            // 3. Smooth color transition back to active electric-cyan
            currentReticleColor = Color.Lerp(currentReticleColor, targetReticleColor, Time.deltaTime * 8.0f);
            if (contractingRingImage != null) contractingRingImage.color = currentReticleColor;
            if (innerFrameImage != null) innerFrameImage.color = currentReticleColor;

            // Return target color back to baseline unmuted cyan
            targetReticleColor = Color.Lerp(targetReticleColor, RadiantCyan, Time.deltaTime * 4.0f);
            reticlePunchScale = Mathf.MoveTowards(reticlePunchScale, 1.0f, Time.deltaTime * 3.0f);

            // Forward beat pulse to SilenceChainMeter for harmonic pulsing
            if (silenceChainMeter != null)
            {
                silenceChainMeter.UpdateBeatPulse(beatProgress);
            }
        }

        // =========================================================================
        // 4. The Tremor Seismograph (Stability & Hazard Warning)
        // =========================================================================

        private void UpdateSeismographStability()
        {
            if (seismographAlertCanvasGroup == null) return;

            if (isCriticalHazard)
            {
                tremorGlitchTimer += Time.deltaTime * 14.0f;
                // Erratic strobing cyan/red alert
                float strobe = Mathf.Sin(tremorGlitchTimer) * 0.5f + 0.5f;
                seismographAlertCanvasGroup.alpha = Mathf.Lerp(0.35f, 1.0f, strobe);

                if (seismographAlertText != null)
                {
                    seismographAlertText.text = TextOverload;
                    seismographAlertText.color = strobe > 0.5f ? RadiantCyan : HazardGlitchRed;
                }

                if (seismographWaveformImage != null)
                {
                    seismographWaveformImage.color = HazardGlitchRed;
                }
            }
            else
            {
                seismographAlertCanvasGroup.alpha = Mathf.MoveTowards(seismographAlertCanvasGroup.alpha, 0f, Time.deltaTime * 2.5f);
                if (seismographAlertText != null)
                {
                    seismographAlertText.text = TextStable;
                    seismographAlertText.color = HoneyAmber;
                }

                if (seismographWaveformImage != null)
                {
                    seismographWaveformImage.color = HoneyAmber;
                }
            }
        }

        // =========================================================================
        // 5. The Elevation Altimeter (Sanctuary Progress)
        // =========================================================================

        private void UpdateAltimeterDisplay(int currentHeight)
        {
            if (altimeterTicks == null) return;

            for (int i = 0; i < altimeterTicks.Length; i++)
            {
                if (altimeterTicks[i] == null) continue;

                if (i < currentHeight)
                {
                    // Locked, anchored amber floor
                    altimeterTicks[i].color = HoneyAmber;
                }
                else if (i == currentHeight)
                {
                    // Active incoming floor
                    altimeterTicks[i].color = RadiantCyan;
                }
                else
                {
                    // Dormant dark charcoal
                    altimeterTicks[i].color = VelvetyCharcoal;
                }
            }

            if (floorCountText != null)
            {
                int index = Mathf.Clamp(currentHeight, 0, FloorStringCache.Length - 1);
                floorCountText.text = FloorStringCache[index];
            }
        }

        // =========================================================================
        // 6. Micro-Feedback Floating Text Pool
        // =========================================================================

        public void SpawnFloatingFeedback(string text, Color color, Vector3 worldPosition)
        {
            if (textPool == null || textPool.Length == 0) return;

            int idx = textPoolCursor;
            textPoolCursor = (textPoolCursor + 1) % textPool.Length;

            textPool[idx].TextComponent.text = text;
            textPool[idx].TextComponent.color = color;
            textPool[idx].WorldAnchorPosition = worldPosition + Vector3.up * 0.8f;
            textPool[idx].ElapsedTime = 0f;
            textPool[idx].IsActive = true;
            textPool[idx].Group.alpha = 1f;

            if (dioramaCamera != null)
            {
                textPool[idx].Rect.position = dioramaCamera.WorldToScreenPoint(textPool[idx].WorldAnchorPosition);
            }
        }

        private void UpdateFloatingTextPool()
        {
            if (textPool == null) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < textPool.Length; i++)
            {
                if (!textPool[i].IsActive) continue;

                textPool[i].ElapsedTime += dt;
                float progress = textPool[i].ElapsedTime / floatingTextDuration;

                if (progress >= 1.0f)
                {
                    textPool[i].IsActive = false;
                    textPool[i].Group.alpha = 0f;
                    continue;
                }

                // Vertical drift
                Vector3 screenBase = dioramaCamera != null
                    ? dioramaCamera.WorldToScreenPoint(textPool[i].WorldAnchorPosition)
                    : textPool[i].Rect.position;

                screenBase.y += progress * floatingTextDriftSpeed;
                textPool[i].Rect.position = screenBase;

                // Alpha fade out over second half of lifetime
                float alpha = progress > 0.5f ? 1.0f - ((progress - 0.5f) * 2.0f) : 1.0f;
                textPool[i].Group.alpha = alpha;
            }
        }

        // =========================================================================
        // 7. Event Handlers
        // =========================================================================

        private void HandleInputScored(TimingResult result)
        {
            Vector3 hitPos = towerManager != null && towerManager.ActiveBlock != null
                ? towerManager.ActiveBlock.transform.position
                : Vector3.zero;

            switch (result)
            {
                case TimingResult.Perfect:
                    targetReticleColor = GoldSparkle;
                    reticlePunchScale = 1.35f;
                    SpawnFloatingFeedback(TextPerfect, GoldSparkle, hitPos);
                    break;

                case TimingResult.Good:
                    targetReticleColor = HoneyAmber;
                    reticlePunchScale = 1.15f;
                    SpawnFloatingFeedback(TextMuted, HoneyAmber, hitPos);
                    break;

                case TimingResult.Early:
                case TimingResult.Late:
                case TimingResult.Miss:
                default:
                    targetReticleColor = HazardGlitchRed;
                    reticlePunchScale = 0.88f;
                    SpawnFloatingFeedback(TextMiss, HazardGlitchRed, hitPos);
                    break;
            }
        }

        private void HandleBlockAnchored(HushBlock block, TimingResult result)
        {
            if (block != null)
            {
                int chain = rhythmManager != null ? rhythmManager.SilenceChain : 0;
                if (chain >= 4)
                {
                    SpawnFloatingFeedback($"CHAIN x{chain}", HoneyAmber, block.transform.position + Vector3.up * 0.4f);
                }
            }
        }

        private void HandleSilenceChainChanged(int chain)
        {
            if (silenceChainMeter != null)
            {
                silenceChainMeter.SetChain(chain);
            }
        }

        private void HandleHeightChanged(int newHeight)
        {
            UpdateAltimeterDisplay(newHeight);
        }

        private void HandleTowerCollapsed()
        {
            isCriticalHazard = true;
            targetReticleColor = HazardGlitchRed;
            UpdateAltimeterDisplay(0);
        }

        private void HandleTremorStateChanged(bool isTremorActive)
        {
            isCriticalHazard = isTremorActive;
        }
    }
}
