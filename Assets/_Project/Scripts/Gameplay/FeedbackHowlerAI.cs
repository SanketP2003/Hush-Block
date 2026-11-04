using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Predator AI and Counter-Rhythm System for Hush Blocks.
    /// Manages ledge prowling along the active tower stack, low-frequency timing distortion,
    /// Feedback Shriek wave attacks, and the off-beat syncopated counter-strike vulnerability window.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FeedbackHowlerAI : MonoBehaviour
    {
        public enum HowlerState
        {
            /// <summary>Clambering up/down outer stack ledges, emitting timing distortion.</summary>
            ProwlingLedges,
            /// <summary>Windup phase: speaker jaw flashes on syncopated half-beats (Counter-Strike Window!).</summary>
            PreparingShriek,
            /// <summary>Executing physical stomp or acoustic shriek attack.</summary>
            ShatterStrike,
            /// <summary>Stunned and vulnerable after an overload strike.</summary>
            StunnedVulnerable,
            /// <summary>Overloaded and shattered into inert charcoal soot; ledge cleared.</summary>
            Dispersed
        }

        [Header("State & Identity")]
        [SerializeField] private HowlerState currentState = HowlerState.ProwlingLedges;
        [Tooltip("Patrol side of the tower: +1 for right ledge, -1 for left ledge.")]
        [SerializeField] private int ledgeSide = 1;
        [Tooltip("Horizontal offset distance from tower stack edge.")]
        [SerializeField, Min(0.2f)] private float ledgeClearance = 1.2f;

        [Header("Prowling & Timing Distortion")]
        [Tooltip("Vertical climbing speed along stack ledges (m/s).")]
        [SerializeField, Min(0.5f)] private float climbSpeed = 2.4f;
        [Tooltip("Interval between deciding new attack actions while prowling.")]
        [SerializeField, Range(3f, 12f)] private float attackDecisionInterval = 6.0f;
        [Tooltip("Timing window distortion penalty applied to RhythmManager while prowling.")]
        [SerializeField, Range(0.05f, 0.4f)] private float timingDistortionFactor = 0.25f;

        [Header("Combat Counter-Rhythm (Vulnerability Window)")]
        [Tooltip("Windup duration before unleashing the feedback shriek.")]
        [SerializeField, Min(1.0f)] private float windupDuration = 2.0f;
        [Tooltip("Speaker jaw renderer or visual transform that flashes on syncopated half-beats.")]
        [SerializeField] private Renderer speakerJawRenderer;
        [Tooltip("Cyan emission color during attack windup (#00F0FF at +3.5 EV).")]
        [ColorUsage(true, true)]
        [SerializeField] private Color cyanDistortionColor = new Color(0f, 0.941f, 1f) * 11.31f;
        [Tooltip("Inert soot charcoal color after overload dispersal (#1C1E24).")]
        [SerializeField] private Color charcoalSootColor = new Color(0.11f, 0.118f, 0.141f, 1f);

        [Header("Attacks & Destruction")]
        [Tooltip("VFX wave cone or particle prefab spawned during Feedback Shriek.")]
        [SerializeField] private GameObject feedbackShriekWavePrefab;
        [Tooltip("Particle system spawned when the Howler shatters into charcoal dust.")]
        [SerializeField] private ParticleSystem sootDispersalParticles;
        [Tooltip("Downward stomp force applied to break an amber block seal.")]
        [SerializeField, Min(1f)] private float stompForce = 8.0f;

        [Header("System Dependencies")]
        [SerializeField] private TowerManager towerManager;
        [SerializeField] private RhythmManager rhythmManager;

        private FullGame.StackGame stackRun;
        private FullGame.HushStage stageRef;

        // Cached Shader Property IDs
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // Runtime Tracking
        private MaterialPropertyBlock propertyBlock;
        private Transform cachedTransform;
        private float targetLedgeY;
        private float attackTimer;
        private bool isSyncopatedWindowActive;
        private Coroutine windupCoroutine;

        // Public Properties
        public HowlerState CurrentState => currentState;
        public float TimingDistortionFactor => timingDistortionFactor;
        public bool IsCounterVulnerable => currentState == HowlerState.PreparingShriek && isSyncopatedWindowActive;
        public bool IsDispersed => currentState == HowlerState.Dispersed;

        // Events
        public event Action<HowlerState> OnStateChanged;
        public event Action OnShriekLaunched;
        public event Action OnCounterOverloadSuccess;
        public event Action OnHowlerDispersed;

        private void Awake()
        {
            cachedTransform = transform;
            propertyBlock = new MaterialPropertyBlock();

            if (towerManager == null) towerManager = UnityEngine.Object.FindAnyObjectByType<TowerManager>();
            if (rhythmManager == null) rhythmManager = UnityEngine.Object.FindAnyObjectByType<RhythmManager>();
        }

        private void OnEnable()
        {
            if (rhythmManager != null)
            {
                rhythmManager.OnDoubleBeat += HandleSyncopatedDoubleBeat;
                rhythmManager.OnBeat += HandleBeat;
            }
        }

        private void OnDisable()
        {
            if (rhythmManager != null)
            {
                rhythmManager.OnDoubleBeat -= HandleSyncopatedDoubleBeat;
                rhythmManager.OnBeat -= HandleBeat;
            }
        }

        private void Start()
        {
            attackTimer = attackDecisionInterval;
            SetState(HowlerState.ProwlingLedges);
        }

        private void Update()
        {
            switch (currentState)
            {
                case HowlerState.ProwlingLedges:
                    UpdateProwling();
                    break;
                case HowlerState.PreparingShriek:
                    // Windup handled in coroutine
                    break;
                case HowlerState.ShatterStrike:
                    break;
                case HowlerState.StunnedVulnerable:
                    break;
                case HowlerState.Dispersed:
                    break;
            }
        }

        public void InitializeForStackGame(FullGame.StackGame run, FullGame.HushStage stage, int side = -1)
        {
            stackRun = run;
            stageRef = stage;
            ledgeSide = side;
            attackTimer = 2.0f;
            windupDuration = 2.4f;
            if (speakerJawRenderer == null) speakerJawRenderer = GetComponentInChildren<MeshRenderer>();
            gameObject.SetActive(true);
            SetState(HowlerState.ProwlingLedges);
        }

        // ---------- Prowling & Timing Distortion ----------

        private void UpdateProwling()
        {
            if (stackRun != null)
            {
                float towerTopY = stackRun.TowerTopY;
                targetLedgeY = Mathf.Max(0.8f, towerTopY + 0.15f);

                float currentX = ledgeSide * (ledgeClearance + 1.1f);
                float newY = Mathf.MoveTowards(cachedTransform.position.y, targetLedgeY, climbSpeed * Time.deltaTime);

                cachedTransform.position = new Vector3(currentX, newY, 0f);
                cachedTransform.rotation = Quaternion.Euler(0f, ledgeSide > 0 ? -120f : 120f, 0f);

                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    attackTimer = attackDecisionInterval;
                    InitiateAttackWindup();
                }
                return;
            }

            if (towerManager == null) return;

            // Follow tower height along the outer ledge coordinates
            float tY = towerManager.CurrentHeight * 1.0f;
            targetLedgeY = Mathf.Max(1.0f, tY - 1.0f);

            float cX = (towerManager.transform.position.x) + (ledgeSide * (ledgeClearance + 0.65f));
            float nY = Mathf.MoveTowards(cachedTransform.position.y, targetLedgeY, climbSpeed * Time.deltaTime);

            cachedTransform.position = new Vector3(cX, nY, towerManager.transform.position.z);

            // Periodically decide to initiate an attack
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = attackDecisionInterval;
                InitiateAttackWindup();
            }
        }

        private void InitiateAttackWindup()
        {
            if (currentState != HowlerState.ProwlingLedges) return;

            SetState(HowlerState.PreparingShriek);
            if (windupCoroutine != null) StopCoroutine(windupCoroutine);
            windupCoroutine = StartCoroutine(AttackWindupSequence());
        }

        private IEnumerator AttackWindupSequence()
        {
            float elapsed = 0f;

            while (elapsed < windupDuration)
            {
                elapsed += Time.deltaTime;

                if (stackRun != null && stackRun.BeatDuration > 0f)
                {
                    float phase = Mathf.Repeat(stackRun.BeatSeconds / stackRun.BeatDuration, 1f);
                    bool syncopated = phase >= 0.38f && phase <= 0.64f;
                    isSyncopatedWindowActive = syncopated;
                    FlashSpeakerJaw(syncopated ? cyanDistortionColor * 1.5f : Color.black);
                }

                yield return null;
            }

            isSyncopatedWindowActive = false;
            FlashSpeakerJaw(Color.black);

            // Windup completed without player counter-strike: release Feedback Shriek!
            ExecuteAcousticAttack();
        }

        // ---------- Counter-Rhythm Synchronization ----------

        private void HandleSyncopatedDoubleBeat(long beatIndex)
        {
            if (currentState != HowlerState.PreparingShriek) return;

            // Flash speaker jaw on syncopated half-beats
            isSyncopatedWindowActive = true;
            FlashSpeakerJaw(cyanDistortionColor * 1.5f);

            // Window remains open for a brief sample tolerance
            StartCoroutine(CloseSyncopatedWindowAfterDelay(0.12f));
        }

        private void HandleBeat(long beatIndex)
        {
            // Close syncopated window when the primary beat lands
            isSyncopatedWindowActive = false;
        }

        private IEnumerator CloseSyncopatedWindowAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            isSyncopatedWindowActive = false;
            FlashSpeakerJaw(Color.black);
        }

        private void FlashSpeakerJaw(Color color)
        {
            if (speakerJawRenderer == null) return;

            speakerJawRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, color);
            speakerJawRenderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>
        /// Player executes an off-beat counter-tap during the vulnerability window.
        /// Overloads the Howler's speaker jaw with acoustic mortar, shattering it into charcoal soot.
        /// </summary>
        public bool TryCounterStrike()
        {
            if (currentState != HowlerState.PreparingShriek) return false;

            if (isSyncopatedWindowActive)
            {
                // Counter-strike successful!
                if (windupCoroutine != null) StopCoroutine(windupCoroutine);
                StartCoroutine(OverloadAndDisperse());
                OnCounterOverloadSuccess?.Invoke();
                return true;
            }
            else
            {
                // Struck outside syncopated window: premature strike fails
                return false;
            }
        }

        // ---------- Acoustic Attacks & Overload ----------

        private void ExecuteAcousticAttack()
        {
            SetState(HowlerState.ShatterStrike);

            if (stackRun != null)
            {
                stackRun.DamageLife();
                if (stageRef != null)
                {
                    stageRef.CameraRig.Shake(0.18f);
                    stageRef.Burst(cachedTransform.position, FullGame.HushPalette.Cyan, 16, 1.8f);
                }
                stackRun.ApplyTowerTremor(1.5f);
                FullGame.HushAudio.Sfx(FullGame.HushAudio.SfxId.Miss, 0.9f);
                StartCoroutine(RecoverToProwling(2.0f));
                return;
            }

            // Randomly choose between Feedback Shriek wave or direct Resonance Ledge Stomp
            bool executeShriek = UnityEngine.Random.value > 0.4f;

            if (executeShriek)
            {
                SpawnFeedbackShriekWave();
            }
            else
            {
                ExecuteResonanceStrike();
            }

            // Return to prowling after attack settles
            StartCoroutine(RecoverToProwling(1.5f));
        }

        private void SpawnFeedbackShriekWave()
        {
            OnShriekLaunched?.Invoke();

            if (feedbackShriekWavePrefab != null)
            {
                Vector3 spawnPos = cachedTransform.position + (Vector3.left * ledgeSide * 0.5f);
                Quaternion spawnRot = Quaternion.LookRotation(-Vector3.right * ledgeSide, Vector3.up);
                Instantiate(feedbackShriekWavePrefab, spawnPos, spawnRot);
            }

            // Crack mortar seams on the nearest anchored amber block, reverting it to chaotic cyan
            if (towerManager != null && towerManager.AnchoredBlocks.Count > 0)
            {
                var blocks = towerManager.AnchoredBlocks;
                for (int i = blocks.Count - 1; i >= 0; i--)
                {
                    HushBlock b = blocks[i];
                    if (b != null && b.IsSettled)
                    {
                        // Acoustic reversion: cracks amber seal back into unmuted hazard!
                        b.TriggerAntiGravity();
                        break;
                    }
                }
            }
        }

        private void ExecuteResonanceStrike()
        {
            // Stomps ledge: applies physical downward stomp force and triggers upward anti-gravity on upper block
            if (towerManager != null && towerManager.ActiveBlock != null)
            {
                towerManager.ActiveBlock.TriggerAntiGravity();
                Rigidbody rb = towerManager.ActiveBlock.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForce(Vector3.down * stompForce, ForceMode.Impulse);
                }
            }
        }

        private IEnumerator RecoverToProwling(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (currentState != HowlerState.Dispersed)
            {
                SetState(HowlerState.ProwlingLedges);
            }
        }

        private IEnumerator OverloadAndDisperse()
        {
            SetState(HowlerState.StunnedVulnerable);

            // Dramatic over-frequency white-hot flash
            FlashSpeakerJaw(Color.white * 8f);
            yield return new WaitForSeconds(0.2f);

            SetState(HowlerState.Dispersed);
            FlashSpeakerJaw(charcoalSootColor);

            // Spawn charcoal soot dispersal particles
            if (stageRef != null)
            {
                stageRef.Burst(cachedTransform.position, FullGame.HushPalette.Charcoal, 28, 2.5f);
                stageRef.Burst(cachedTransform.position, FullGame.HushPalette.Cyan, 12, 1.6f);
            }
            if (sootDispersalParticles != null)
            {
                sootDispersalParticles.transform.position = cachedTransform.position;
                sootDispersalParticles.Play();
            }

            OnHowlerDispersed?.Invoke();

            if (stackRun != null)
            {
                // Dispersed in FullGame: hide and deactivate
                yield return new WaitForSeconds(0.15f);
                gameObject.SetActive(false);
            }
            else
            {
                // Hide mesh and deactivate
                if (speakerJawRenderer != null) speakerJawRenderer.enabled = false;
                Renderer mainRenderer = GetComponent<Renderer>();
                if (mainRenderer != null) mainRenderer.enabled = false;

                Destroy(gameObject, 2.0f);
            }
        }

        private void SetState(HowlerState nextState)
        {
            currentState = nextState;
            OnStateChanged?.Invoke(nextState);
        }
    }
}
