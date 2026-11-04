using System;
using System.Collections;
using UnityEngine;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Lead character controller for Noa in Hush Blocks.
    /// Manages isometric ledge traversal, procedural vertical step-hopping as the tower ascends,
    /// rhythm-synchronized idle bobbing, precision trowel swing timing, Harmonic Hush channeling,
    /// and stumbles upon missed beats.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoaController : MonoBehaviour
    {
        [Header("Animator & Rig References")]
        [Tooltip("Noa's primary Animator component.")]
        [SerializeField] private Animator animator;
        [Tooltip("Amber Trowel tool rig attached to Noa's hand socket.")]
        [SerializeField] private AmberTrowelRig amberTrowelRig;
        [Tooltip("Right-hand socket transform for mounting the trowel with zero local offset.")]
        [SerializeField] private Transform rightHandSocket;

        [Header("Isometric Ledge Traversal & Elevation")]
        [Tooltip("Offset distance from the block center placing Noa along outer ledge.")]
        [SerializeField, Min(0.2f)] private float ledgeOffset = 0.55f;
        [Tooltip("Height of Noa's feet relative to the top face of the block.")]
        [SerializeField] private float footHeightOffset = 0.05f;
        [Tooltip("Duration of the vertical step-hop when ascending to a new layer (seconds).")]
        [SerializeField, Range(0.15f, 0.45f)] private float stepHopDuration = 0.28f;
        [Tooltip("Peak arc height of the step-hop jump curve (meters).")]
        [SerializeField, Range(0.1f, 0.6f)] private float stepHopPeakHeight = 0.35f;

        [Header("Rhythm Synchronization & Idle Bob")]
        [Tooltip("Procedural idle bounce displacement amplitude in meters.")]
        [SerializeField, Range(0.01f, 0.08f)] private float idleBobAmplitude = 0.03f;
        [Tooltip("Silence chain threshold that triggers Harmonic Hush channeling.")]
        [SerializeField, Min(3)] private int harmonicChannelChainThreshold = 5;

        [Header("System Dependencies")]
        [SerializeField] private TowerManager towerManager;
        [SerializeField] private RhythmManager rhythmManager;
        [SerializeField] private AcousticInputController inputController;
        private float customBpm = 120f;

        // Cached Animator Parameter Hashes (Zero-allocation string hashing)
        private static readonly int OnTapTriggerHash = Animator.StringToHash("OnTap");
        private static readonly int StrikeTriggerHash = Animator.StringToHash("Strike_Trowel");
        private static readonly int StepUpTriggerHash = Animator.StringToHash("Step_Up_Floor");
        private static readonly int StumbleTriggerHash = Animator.StringToHash("Stumble_Tremor");
        private static readonly int ChannelHarmonicTriggerHash = Animator.StringToHash("Hold_Trowel_High");
        private static readonly int BeatSpeedFloatHash = Animator.StringToHash("BeatSpeed");
        private static readonly int IsGroundedBoolHash = Animator.StringToHash("IsGrounded");

        // Runtime Tracking
        private Transform cachedTransform;
        private Vector3 targetLedgePosition;
        private Vector3 currentVelocity;
        private Coroutine stepHopCoroutine;
        private bool isStepHopping;
        private bool isGrounded = true;

        // Public Properties
        public bool IsStepHopping => isStepHopping;
        public AmberTrowelRig TrowelRig => amberTrowelRig;

        // Events
        public event Action OnStepHopStarted;
        public event Action OnStepHopCompleted;
        public event Action OnTrowelStrikeTriggered;
        public event Action OnHarmonicChannelStarted;

        private void Awake()
        {
            cachedTransform = transform;

            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (amberTrowelRig == null) amberTrowelRig = GetComponentInChildren<AmberTrowelRig>();
            if (towerManager == null) towerManager = UnityEngine.Object.FindAnyObjectByType<TowerManager>();
            if (rhythmManager == null) rhythmManager = UnityEngine.Object.FindAnyObjectByType<RhythmManager>();
            if (inputController == null) inputController = UnityEngine.Object.FindAnyObjectByType<AcousticInputController>();

            // Ensure trowel is anchored to hand socket with zero local offset
            if (amberTrowelRig != null && rightHandSocket != null)
            {
                amberTrowelRig.AttachToHandSocket(rightHandSocket);
            }

            targetLedgePosition = cachedTransform.position;
        }

        private void OnEnable()
        {
            if (towerManager != null)
            {
                towerManager.OnBlockAnchored += HandleBlockAnchored;
                towerManager.OnTowerCollapsed += HandleTowerCollapsed;
            }

            if (rhythmManager != null)
            {
                rhythmManager.OnBeat += HandleBeat;
                rhythmManager.OnSilenceChainChanged += HandleSilenceChainChanged;
            }

            if (inputController != null)
            {
                inputController.OnInputProcessed += HandleInputProcessed;
            }
        }

        private void OnDisable()
        {
            if (towerManager != null)
            {
                towerManager.OnBlockAnchored -= HandleBlockAnchored;
                towerManager.OnTowerCollapsed -= HandleTowerCollapsed;
            }

            if (rhythmManager != null)
            {
                rhythmManager.OnBeat -= HandleBeat;
                rhythmManager.OnSilenceChainChanged -= HandleSilenceChainChanged;
            }

            if (inputController != null)
            {
                inputController.OnInputProcessed -= HandleInputProcessed;
            }
        }

        private void Start()
        {
            SnapToTopAnchoredBlock(immediate: true);
        }

        private void Update()
        {
            UpdateRhythmicIdleBob();
            UpdateAnimatorTempo();
        }

        // ---------- Grid & Elevation Step-Hopping ----------

        private void HandleBlockAnchored(HushBlock block, TimingResult result)
        {
            if (block == null) return;

            // Trigger upward step-hop to new floor level
            if (stepHopCoroutine != null) StopCoroutine(stepHopCoroutine);
            stepHopCoroutine = StartCoroutine(ExecuteStepHopSequence(block));
        }

        private IEnumerator ExecuteStepHopSequence(HushBlock targetBlock)
        {
            isStepHopping = true;
            isGrounded = false;
            if (animator != null)
            {
                animator.SetBool(IsGroundedBoolHash, false);
                animator.SetTrigger(StepUpTriggerHash);
            }

            OnStepHopStarted?.Invoke();

            Vector3 startPos = cachedTransform.position;

            // Compute top-face landing point on the newly anchored block
            Vector3 blockPos = targetBlock.transform.position;
            Vector3 blockScale = targetBlock.transform.localScale;

            // Position Noa on the North-East isometric outer ledge
            Vector3 landingPos = new Vector3(
                blockPos.x + (blockScale.x * 0.5f) - (ledgeOffset * 0.5f),
                blockPos.y + (blockScale.y * 0.5f) + footHeightOffset,
                blockPos.z - (blockScale.z * 0.5f) + (ledgeOffset * 0.5f)
            );

            float timer = 0f;
            while (timer < stepHopDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / stepHopDuration);

                // Quadratic jump arc: 4 * t * (1 - t)
                float arc = 4f * t * (1f - t) * stepHopPeakHeight;
                Vector3 currentPos = Vector3.Lerp(startPos, landingPos, t);
                currentPos.y += arc;

                cachedTransform.position = currentPos;
                yield return null;
            }

            cachedTransform.position = landingPos;
            targetLedgePosition = landingPos;
            isStepHopping = false;
            isGrounded = true;

            if (animator != null)
            {
                animator.SetBool(IsGroundedBoolHash, true);
            }

            OnStepHopCompleted?.Invoke();
        }

        public void SetBpm(float bpm)
        {
            customBpm = bpm;
            UpdateAnimatorTempo();
        }

        public void ResetToBase(float centerX, float topY, float width)
        {
            if (stepHopCoroutine != null) StopCoroutine(stepHopCoroutine);
            isStepHopping = false;
            isGrounded = true;
            if (cachedTransform == null) cachedTransform = transform;

            targetLedgePosition = new Vector3(
                centerX + (width * 0.5f) - (ledgeOffset * 0.4f),
                topY + footHeightOffset,
                -(1.9f * 0.5f) + (ledgeOffset * 0.45f)
            );
            cachedTransform.position = targetLedgePosition;
            cachedTransform.rotation = Quaternion.Euler(0f, -50f, 0f);

            if (animator != null)
            {
                animator.SetBool(IsGroundedBoolHash, true);
            }
        }

        public void StepUpFloor(float centerX, float topY, float width, float height)
        {
            Vector3 landingPos = new Vector3(
                centerX + (width * 0.5f) - (ledgeOffset * 0.4f),
                topY + footHeightOffset,
                -(1.9f * 0.5f) + (ledgeOffset * 0.45f)
            );
            if (stepHopCoroutine != null) StopCoroutine(stepHopCoroutine);
            stepHopCoroutine = StartCoroutine(ExecuteStepHopTo(landingPos));
        }

        private IEnumerator ExecuteStepHopTo(Vector3 landingPos)
        {
            isStepHopping = true;
            isGrounded = false;
            if (animator != null)
            {
                animator.SetBool(IsGroundedBoolHash, false);
                animator.SetTrigger(StepUpTriggerHash);
            }

            OnStepHopStarted?.Invoke();

            if (cachedTransform == null) cachedTransform = transform;
            Vector3 startPos = cachedTransform.position;
            float timer = 0f;

            while (timer < stepHopDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / stepHopDuration);

                // Quadratic jump arc: 4 * t * (1 - t)
                float arc = 4f * t * (1f - t) * stepHopPeakHeight;
                Vector3 currentPos = Vector3.Lerp(startPos, landingPos, t);
                currentPos.y += arc;

                cachedTransform.position = currentPos;
                yield return null;
            }

            cachedTransform.position = landingPos;
            targetLedgePosition = landingPos;
            isStepHopping = false;
            isGrounded = true;

            if (animator != null)
            {
                animator.SetBool(IsGroundedBoolHash, true);
            }

            OnStepHopCompleted?.Invoke();
        }

        public void SnapToTopAnchoredBlock(bool immediate)
        {
            if (towerManager == null || towerManager.AnchoredBlocks.Count == 0) return;

            HushBlock topBlock = towerManager.AnchoredBlocks[towerManager.AnchoredBlocks.Count - 1];
            if (topBlock == null) return;

            Vector3 blockPos = topBlock.transform.position;
            Vector3 blockScale = topBlock.transform.localScale;

            targetLedgePosition = new Vector3(
                blockPos.x + (blockScale.x * 0.5f) - (ledgeOffset * 0.5f),
                blockPos.y + (blockScale.y * 0.5f) + footHeightOffset,
                blockPos.z - (blockScale.z * 0.5f) + (ledgeOffset * 0.5f)
            );

            if (immediate)
            {
                cachedTransform.position = targetLedgePosition;
            }
        }

        // ---------- Rhythm Synchronization & Trowel Swing ----------

        private void HandleBeat(long beatIndex)
        {
            // Beat tick: syncs procedural idle phase
        }

        private void UpdateRhythmicIdleBob()
        {
            if (isStepHopping || !isGrounded) return;

            float bpm = customBpm > 0f ? customBpm : (rhythmManager != null ? rhythmManager.Bpm : 120f);
            float beatFrequency = (bpm / 60f) * Mathf.PI * 2f;

            // Procedural breath & bobbing aligned with beat
            float bobOffset = Mathf.Sin(Time.time * beatFrequency) * idleBobAmplitude;
            cachedTransform.position = new Vector3(
                targetLedgePosition.x,
                targetLedgePosition.y + Mathf.Max(0f, bobOffset),
                targetLedgePosition.z
            );
        }

        private void UpdateAnimatorTempo()
        {
            if (animator == null) return;

            // BeatSpeed parameter matches animation clips proportionally to BPM
            float bpm = customBpm > 0f ? customBpm : (rhythmManager != null ? rhythmManager.Bpm : 120f);
            float normalizedSpeed = bpm / 120f;
            animator.SetFloat(BeatSpeedFloatHash, normalizedSpeed);
        }

        private void HandleInputProcessed(TimingResult result)
        {
            if (result == TimingResult.Perfect || result == TimingResult.Good)
            {
                TriggerTrowelStrike(result == TimingResult.Perfect);
            }
            else
            {
                TriggerStumbleTremor();
            }
        }

        /// <summary>
        /// Fires Noa's trowel downswing instantly on player tap, flaring the amber crystal blade.
        /// </summary>
        public void TriggerTrowelStrike(bool isPerfect)
        {
            if (animator != null)
            {
                animator.SetTrigger(OnTapTriggerHash);
                animator.SetTrigger(StrikeTriggerHash);
            }

            if (amberTrowelRig != null)
            {
                amberTrowelRig.FlareDownswing();

                // Trigger strike frame impact
                HushBlock activeBlock = towerManager != null ? towerManager.ActiveBlock : null;
                Vector3 strikePoint = cachedTransform.position + (cachedTransform.forward * 0.4f);
                amberTrowelRig.ExecuteImpactStrike(strikePoint, Vector3.up, activeBlock);
            }

            OnTrowelStrikeTriggered?.Invoke();
        }

        public void TriggerStumble()
        {
            TriggerStumbleTremor();
        }

        private void TriggerStumbleTremor()
        {
            if (animator != null)
            {
                animator.SetTrigger(StumbleTriggerHash);
            }
        }

        // ---------- Harmonic Hush Channeling ----------

        public void TriggerHarmonicChannel()
        {
            StartCoroutine(ExecuteHarmonicChannelingSequence());
        }

        private void HandleSilenceChainChanged(int chain)
        {
            // Trigger Hold_Trowel_High when reaching milestone streaks
            if (chain > 0 && chain % harmonicChannelChainThreshold == 0)
            {
                StartCoroutine(ExecuteHarmonicChannelingSequence());
            }
        }

        private IEnumerator ExecuteHarmonicChannelingSequence()
        {
            if (animator != null)
            {
                animator.SetTrigger(ChannelHarmonicTriggerHash);
            }

            if (amberTrowelRig != null)
            {
                amberTrowelRig.TriggerHarmonicGlow();
            }

            OnHarmonicChannelStarted?.Invoke();

            yield return new WaitForSeconds(0.6f);
        }

        private void HandleTowerCollapsed()
        {
            TriggerStumbleTremor();
        }
    }
}

