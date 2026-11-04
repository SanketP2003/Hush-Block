using System;
using UnityEngine;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Interactive Modular Block Controller for Hush Blocks.
    /// Manages chaotic resonance vibration, material transitions via MaterialPropertyBlock,
    /// rhythmic muting downward snaps, Harmonic Hush broadcasts, and physics-driven
    /// anti-gravity inversion ("The Resonant Inversion") when rhythmic stability fails.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HushBlock : MonoBehaviour
    {
        public enum BlockState
        {
            /// <summary>Unstable state: high-frequency cyan micro-jitter and acoustic emission.</summary>
            UnmutedChaotic,
            /// <summary>Active transition: downward snap settling and cyan-to-amber material lerp.</summary>
            MutingTransition,
            /// <summary>Permanent stillness: anchored honey-amber foundation with mortar crystallization.</summary>
            AmberSettled,
            /// <summary>Resonant Inversion: negative gravity upward thrust and torque destabilizing the stack.</summary>
            AntiGravityDestabilized
        }

        [Header("State Configuration")]
        [Tooltip("Initial operational state upon scene load.")]
        [SerializeField] private BlockState initialState = BlockState.UnmutedChaotic;
        [Tooltip("Duration in seconds for the muting color/crystal transition.")]
        [SerializeField, Min(0.05f)] private float transitionDuration = 0.35f;

        [Header("Visual Binding")]
        [Tooltip("Child transform that receives visual jitter and snap offsets. Defaults to this transform.")]
        [SerializeField] private Transform visualRoot;
        [Tooltip("Renderer driven by the material property block. Defaults to the first renderer in children.")]
        [SerializeField] private Renderer visualRenderer;

        [Header("Acoustic Micro-Jitter (Chaotic State)")]
        [Tooltip("Spatial amplitude of the vertex/transform jitter (meters). Tuning: 0.02 - 0.05m.")]
        [SerializeField, Min(0f)] private float jitterAmplitude = 0.035f;
        [Tooltip("Temporal frequency of the chaotic vibration (Hz). Tuning: 20 - 40Hz.")]
        [SerializeField, Min(1f)] private float jitterFrequency = 28f;
        [Tooltip("Random seed offset to prevent adjacent blocks from oscillating in identical lockstep.")]
        [SerializeField] private float noisePhaseSeed = 0f;

        [Header("Muting Snap & Foundation")]
        [Tooltip("Downward displacement snap distance applied at the moment of muting (meters).")]
        [SerializeField, Range(0.01f, 0.15f)] private float downwardSnapDepth = 0.045f;
        [Tooltip("Optional child mesh or prefab representing acoustic mortar crystallization.")]
        [SerializeField] private GameObject crystallizationMesh;
        [Tooltip("Optional visual transform representing radiating sound rings while unmuted.")]
        [SerializeField] private Transform soundRingTransform;
        [Tooltip("Radius within which a Perfect mute triggers Harmonic Hush on neighboring blocks.")]
        [SerializeField, Min(0.5f)] private float harmonicRadius = 2.8f;

        [Header("Anti-Gravity Physics ('The Resonant Inversion')")]
        [Tooltip("Upward acceleration force applied to topple upper blocks (+Y thrust in m/s^2). Tuning: 6 - 14.")]
        [SerializeField, Min(1f)] private float antiGravityForce = 9.5f;
        [Tooltip("Rotational torque applied during anti-gravity thrust to break stack friction.")]
        [SerializeField, Min(0f)] private float antiGravityTorque = 4.5f;
        [Tooltip("Duration in seconds the block actively applies upward anti-gravity before resting.")]
        [SerializeField, Min(0.1f)] private float antiGravityDuration = 0.55f;

        [Header("HDR Visual Palette")]
        [Tooltip("Electric cyan radiant color (#00F0FF at +3.5 EV).")]
        [ColorUsage(true, true)]
        [SerializeField] private Color chaoticCyanColor = new Color(0f, 0.941f, 1f) * 11.31f;
        [Tooltip("Warm honey-amber settled color (#FFB300 at +2.0 EV).")]
        [ColorUsage(true, true)]
        [SerializeField] private Color settledAmberColor = new Color(1f, 0.702f, 0f) * 4.0f;
        [Tooltip("Matte charcoal velvety base albedo (#1C1E24).")]
        [SerializeField] private Color baseCharcoalColor = new Color(0.11f, 0.118f, 0.141f, 1f);

        // Cached Shader Property IDs (Zero-allocation string hashing)
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int AcousticBloomId = Shader.PropertyToID("_AcousticBloom");
        private static readonly int StateBlendId = Shader.PropertyToID("_StateBlend");
        private static readonly int JitterIntensityId = Shader.PropertyToID("_JitterIntensity");
        private static readonly int CrystallizationProgressId = Shader.PropertyToID("_CrystallizationProgress");
        private static readonly int CyanColorId = Shader.PropertyToID("_CyanColor");
        private static readonly int AmberColorId = Shader.PropertyToID("_AmberColor");

        // Non-allocating physics buffer
        private static readonly Collider[] OverlapBuffer = new Collider[16];

        // Component & Transform cache
        private Transform cachedTransform;
        private Transform cachedVisualRoot;
        private Renderer cachedRenderer;
        private Rigidbody cachedBody;
        private MaterialPropertyBlock propertyBlock;

        // Runtime state tracking
        private BlockState currentState;
        private Vector3 baseLocalPosition;
        private Vector3 snapVelocity;
        private float transitionTimer;
        private float antiGravityTimer;
        private float originalJitterAmplitude;
        private float harmonicSootheTimer;
        private bool isHarmonicallySoothed;

        // Public Accessors
        public BlockState CurrentState => currentState;
        public bool IsSettled => currentState == BlockState.AmberSettled;
        public bool IsChaotic => currentState == BlockState.UnmutedChaotic;
        public bool IsDestabilized => currentState == BlockState.AntiGravityDestabilized;

        // Events
        public event Action<BlockState> OnStateChanged;
        public event Action OnHarmonicPulse;
        public event Action OnMuted;
        public event Action OnAntiGravityTriggered;

        private void Awake()
        {
            cachedTransform = transform;
            cachedVisualRoot = visualRoot != null ? visualRoot : cachedTransform;
            cachedRenderer = visualRenderer != null
                ? visualRenderer
                : GetComponentInChildren<Renderer>(true);
            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<Renderer>();
            }

            cachedBody = GetComponent<Rigidbody>();
            propertyBlock = new MaterialPropertyBlock();

            baseLocalPosition = cachedVisualRoot.localPosition;
            originalJitterAmplitude = jitterAmplitude;

            if (noisePhaseSeed == 0f)
            {
                noisePhaseSeed = (cachedTransform.position.x * 7.13f + cachedTransform.position.y * 13.37f) % 100f;
            }

            if (crystallizationMesh != null)
            {
                crystallizationMesh.SetActive(false);
            }

            SetState(initialState, notify: false);
        }

        private void Update()
        {
            // Process harmonic soothe recovery timer
            if (isHarmonicallySoothed)
            {
                harmonicSootheTimer -= Time.deltaTime;
                if (harmonicSootheTimer <= 0f)
                {
                    isHarmonicallySoothed = false;
                    jitterAmplitude = originalJitterAmplitude;
                }
            }

            switch (currentState)
            {
                case BlockState.UnmutedChaotic:
                    UpdateChaoticState();
                    break;
                case BlockState.MutingTransition:
                    UpdateTransitionState();
                    break;
                case BlockState.AntiGravityDestabilized:
                    UpdateDestabilizedState();
                    break;
                case BlockState.AmberSettled:
                    // Fully anchored: zero transform updates
                    break;
            }
        }

        private void FixedUpdate()
        {
            // Physics-driven upward thrust during Resonant Inversion
            if (currentState == BlockState.AntiGravityDestabilized && cachedBody != null && !cachedBody.isKinematic)
            {
                // Negative gravity upward acceleration
                cachedBody.AddForce(Vector3.up * antiGravityForce, ForceMode.Acceleration);

                // Rotational torque along lateral axis to topple stacked blocks
                float torqueX = (Mathf.PerlinNoise(Time.time * 8f, noisePhaseSeed) - 0.5f) * 2f;
                float torqueZ = (Mathf.PerlinNoise(noisePhaseSeed, Time.time * 8f) - 0.5f) * 2f;
                Vector3 torqueVector = new Vector3(torqueX, 0f, torqueZ) * antiGravityTorque;

                cachedBody.AddTorque(torqueVector, ForceMode.Acceleration);
            }
        }

        // ---------- State Machine & Gameplay Actions ----------

        /// <summary>
        /// Mutes the active block from chaotic vibration into amber stillness.
        /// </summary>
        /// <param name="isPerfect">True if the player scored a ±50ms Perfect hit.</param>
        public void MuteBlock(bool isPerfect)
        {
            if (currentState == BlockState.AmberSettled) return;

            // 1. Instant downward snap kills acoustic jitter velocity
            cachedVisualRoot.localPosition = baseLocalPosition - (Vector3.up * downwardSnapDepth);

            // 2. Begin transition
            SetState(BlockState.MutingTransition, notify: true);
            OnMuted?.Invoke();

            // 3. Perfect timing triggers Harmonic Hush on neighboring blocks
            if (isPerfect)
            {
                BroadcastHarmonicHush();
            }
        }

        /// <summary>
        /// Triggers 'The Resonant Inversion': negative gravity upward acceleration and torque.
        /// </summary>
        public void TriggerAntiGravity()
        {
            if (currentState == BlockState.AmberSettled) return;

            antiGravityTimer = antiGravityDuration;
            SetState(BlockState.AntiGravityDestabilized, notify: true);
            OnAntiGravityTriggered?.Invoke();
        }

        /// <summary>
        /// Receiver for Harmonic Hush pulses broadcast from adjacent perfect hits.
        /// Soothes jittering double-beat blocks into a calmer state.
        /// </summary>
        public void ReceiveHarmonicStabilization()
        {
            if (currentState != BlockState.UnmutedChaotic && currentState != BlockState.AntiGravityDestabilized) return;

            isHarmonicallySoothed = true;
            harmonicSootheTimer = 1.25f;
            jitterAmplitude = originalJitterAmplitude * 0.35f;

            OnHarmonicPulse?.Invoke();

            // Soft amber pulse feedback through material property block
            cachedRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(AcousticBloomId, 2.5f);
            cachedRenderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>
        /// Searches for nearby blocks and invokes ReceiveHarmonicStabilization without GC allocations.
        /// </summary>
        private void BroadcastHarmonicHush()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(cachedTransform.position, harmonicRadius, OverlapBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;

                HushBlock neighbor = col.GetComponentInParent<HushBlock>();
                if (neighbor != null && neighbor != this)
                {
                    neighbor.ReceiveHarmonicStabilization();
                }
            }
        }

        private void SetState(BlockState nextState, bool notify)
        {
            currentState = nextState;
            transitionTimer = transitionDuration;

            if (crystallizationMesh != null)
            {
                crystallizationMesh.SetActive(nextState == BlockState.AmberSettled || nextState == BlockState.MutingTransition);
            }

            if (cachedBody != null)
            {
                switch (nextState)
                {
                    case BlockState.AmberSettled:
                        if (!cachedBody.isKinematic)
                        {
                            cachedBody.linearVelocity = Vector3.zero;
                            cachedBody.angularVelocity = Vector3.zero;
                        }
                        cachedBody.isKinematic = true;
                        cachedBody.useGravity = false;
                        cachedBody.constraints = RigidbodyConstraints.FreezeAll;
                        break;

                    case BlockState.AntiGravityDestabilized:
                        cachedBody.isKinematic = false;
                        cachedBody.useGravity = false; // Negative gravity handled in FixedUpdate
                        cachedBody.constraints = RigidbodyConstraints.None;
                        break;

                    case BlockState.UnmutedChaotic:
                    case BlockState.MutingTransition:
                        cachedBody.isKinematic = true;
                        cachedBody.useGravity = false;
                        break;
                }
            }

            if (soundRingTransform != null)
            {
                soundRingTransform.gameObject.SetActive(nextState == BlockState.UnmutedChaotic);
            }

            float initialBlend = nextState == BlockState.AmberSettled ? 1f : 0f;
            UpdateShaderProperties(initialBlend, initialBlend);

            if (notify)
            {
                OnStateChanged?.Invoke(nextState);
            }
        }

        // ---------- State Update Loops ----------

        private void UpdateChaoticState()
        {
            float time = Time.time * jitterFrequency + noisePhaseSeed;

            // Algorithmic 3D Perlin / Sine Micro-Jitter
            float noiseX = (Mathf.PerlinNoise(time, noisePhaseSeed) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(noisePhaseSeed, time) - 0.5f) * 1.5f;
            float noiseZ = Mathf.Sin(time * 1.33f);

            Vector3 jitterOffset = new Vector3(noiseX, noiseY, noiseZ) * jitterAmplitude;
            cachedVisualRoot.localPosition = baseLocalPosition + jitterOffset;

            // Pulse sound ring scaling
            if (soundRingTransform != null)
            {
                float ringScale = 1f + (Mathf.Sin(Time.time * 12f) * 0.15f);
                soundRingTransform.localScale = new Vector3(ringScale, ringScale, 1f);
            }

            // High-frequency cyan emission breathing
            float cyanEmissionMod = Mathf.Sin(Time.time * 24f) * 0.35f + 1.0f;
            UpdateShaderProperties(stateBlend: 0f, crystallization: 0f, cyanMod: cyanEmissionMod);
        }

        private void UpdateTransitionState()
        {
            transitionTimer -= Time.deltaTime;
            float progress = 1f - Mathf.Clamp01(transitionTimer / transitionDuration);

            // Weighted spring dampening: return downward snap to baseLocalPosition
            cachedVisualRoot.localPosition = Vector3.SmoothDamp(
                cachedVisualRoot.localPosition,
                baseLocalPosition,
                ref snapVelocity,
                0.08f
            );

            // Lerp shader properties from Cyan (0.0) to Amber (1.0)
            UpdateShaderProperties(stateBlend: progress, crystallization: progress);

            if (transitionTimer <= 0f)
            {
                cachedVisualRoot.localPosition = baseLocalPosition;
                SetState(BlockState.AmberSettled, notify: true);
            }
        }

        private void UpdateDestabilizedState()
        {
            antiGravityTimer -= Time.deltaTime;

            // Severe violent shudder while rising
            float shake = (Mathf.PerlinNoise(Time.time * 45f, 0f) - 0.5f) * (jitterAmplitude * 2.5f);
            if (cachedBody != null && cachedBody.isKinematic)
            {
                cachedVisualRoot.localPosition = baseLocalPosition + new Vector3(shake, shake * 0.5f, 0f);
            }

            UpdateShaderProperties(stateBlend: 0f, crystallization: 0f, cyanMod: 2.0f);

            if (antiGravityTimer <= 0f)
            {
                // Revert to chaotic state or enable normal gravity for collapse
                if (cachedBody != null)
                {
                    cachedBody.useGravity = true;
                }
                SetState(BlockState.UnmutedChaotic, notify: true);
            }
        }

        // ---------- Material Control (Zero GC Allocations) ----------

        private void UpdateShaderProperties(float stateBlend, float crystallization, float cyanMod = 1f)
        {
            if (cachedRenderer == null) return;

            cachedRenderer.GetPropertyBlock(propertyBlock);

            // Standard URP Shader Graph reference bindings
            propertyBlock.SetFloat(StateBlendId, stateBlend);
            propertyBlock.SetFloat(CrystallizationProgressId, crystallization);
            propertyBlock.SetFloat(JitterIntensityId, (1f - stateBlend) * jitterAmplitude);

            // Color Lerp
            Color activeColor = Color.Lerp(chaoticCyanColor * cyanMod, settledAmberColor, stateBlend);
            propertyBlock.SetColor(ColorId, activeColor);
            propertyBlock.SetColor(BaseColorId, baseCharcoalColor);
            propertyBlock.SetColor(EmissionColorId, activeColor);
            propertyBlock.SetFloat(AcousticBloomId, Mathf.Lerp(3.5f * cyanMod, 2.0f, stateBlend));

            cachedRenderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>
        /// Recovers the block to an exact world position and resets kinematic stability.
        /// </summary>
        public void ResetToFoundation(Vector3 worldPos)
        {
            cachedTransform.position = worldPos;
            baseLocalPosition = cachedVisualRoot.localPosition;
            jitterAmplitude = originalJitterAmplitude;
            isHarmonicallySoothed = false;
            SetState(BlockState.AmberSettled, notify: true);
        }
    }
}
