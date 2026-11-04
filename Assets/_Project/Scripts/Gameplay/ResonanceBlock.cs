using System;
using System.Collections.Generic;
using UnityEngine;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Full resonance-block state machine with five explicit states and input-driven muting.
    ///
    /// States:
    ///   Noisy       — block vibrates with cyan sound rings, player can stabilize.
    ///   Stabilizing — player input currently being processed; temporary transition.
    ///   Stabilized  — block is warm amber, silent, provides structural support.
    ///   Destabilizing — failed tap or missed beat; block begins to break down.
    ///   Falling     — block has fallen, removed from structure, can be recycled.
    ///
    /// Input-driven muting: when the player taps within the rhythm window,
    /// the block transitions from Noisy → Stabilizing → Stabilized.
    ///
    /// Failed tap: if the player taps outside the rhythm window or taps a
    /// already-stabilized block, the block enters Destabilizing → Falling.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResonanceBlock : MonoBehaviour
    {
        /// <summary>Explicit block states for the state machine.</summary>
        [Serializable]
        public enum State { Noisy, Stabilizing, Stabilized, Destabilizing, Falling }

        [Header("State")]
        [SerializeField] private State initialState = State.Noisy;
        [SerializeField] [Range(0f, 1f)] private float transitionTime = 0.35f;
        [SerializeField] private bool resetOnLoad = true;

        [Header("Noisy Visuals")]
        [SerializeField] private float vibrationAmount = 0.035f;
        [SerializeField] private float vibrationSpeed = 18f;
        [SerializeField] private Color noisyColor = new Color(0f, 0.85f, 1f);
        [SerializeField] private float soundRingInterval = 0.25f;

        [Header("Amber Stabilized Color")]
        [SerializeField] private Color stabilizedColor = new Color(1f, 0.71f, 0.28f);

        [Header("Destabilizing / Falling")]
        [SerializeField] private float destabilizeShakeAmount = 0.05f;
        [SerializeField] private float fallSpeed = 8f;
        [SerializeField] private AnimationCurve fallCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Events")]
        [SerializeField] private bool broadcastEvents = true;

        private Renderer blockRenderer;
        private Material blockMaterialInstance;
        private Vector3 baseLocalPosition;
        private State currentState;
        private float transitionTimer;
        private float lastSoundRingTime;
        private float destabilizeTimer;
        private Rigidbody blockBody;
        private Vector3 fallStartPosition;
        private float fallStartTime;

        // Event callbacks — subscribers can hook into block lifecycle
        /// <summary>Fired when the block state changes.</summary>
        public event Action<State> OnStateChanged;
        /// <summary>Fired when the block is stabilized by player input.</summary>
        public event Action OnStabilized;
        /// <summary>Fired when the block begins to destabilize.</summary>
        public event Action OnDestabilized;
        /// <summary>Fired when the block starts falling.</summary>
        public event Action OnFallen;

        public State CurrentState => currentState;
        public bool IsNoisy => currentState == State.Noisy;
        public bool IsStabilized => currentState == State.Stabilized;
        public bool IsFalling => currentState == State.Falling;
        public bool CanBeStabilized => currentState == State.Noisy;

        private void Awake()
        {
            blockRenderer = GetComponent<Renderer>();
            blockBody = GetComponent<Rigidbody>();
            baseLocalPosition = transform.localPosition;

            // Create a per-block material instance to avoid bleed across blocks.
            if (blockRenderer != null && blockRenderer.sharedMaterial != null)
            {
                blockMaterialInstance = new Material(blockRenderer.sharedMaterial)
                {
                    name = gameObject.name + " Resonance Material"
                };
                blockRenderer.material = blockMaterialInstance;
            }

            SetState(initialState, notifyLifecycle: false);

            // If we're not resuming from a load, start in Noisy state so the
            // greybox can immediately present a vibrating cyan block to the player.
            if (resetOnLoad) Invoke("RefreshStateFromInitial", 0.01f);
        }

        private void OnDestroy()
        {
            if (blockMaterialInstance != null) Destroy(blockMaterialInstance);
        }

        private void RefreshStateFromInitial()
        {
            // Ensures the initial state is applied after any scene-load delays.
            SetState(initialState);
        }

        private void Update()
        {
            // Each state has its own per-frame logic.
            switch (currentState)
            {
                case State.Noisy:
                    UpdateNoisy();
                    break;
                case State.Stabilizing:
                    UpdateStabilizing();
                    break;
                case State.Stabilized:
                    UpdateStabilized();
                    break;
                case State.Destabilizing:
                    UpdateDestabilizing();
                    break;
                case State.Falling:
                    UpdateFalling();
                    break;
            }
        }

        /// <summary>Public API: request a Velvet Seal stabilization on this block.</summary>
        /// <param name="fromInput">True when the request originates from a player tap;
        /// false when called programmatically.</param>
        public void RequestStabilize(bool fromInput = true)
        {
            // Cannot stabilize if the block is already stabilized or falling.
            if (currentState == State.Stabilized || currentState == State.Falling) return;

            // If the block is noisy, begin the stabilizing transition.
            if (currentState == State.Noisy)
            {
                SetState(State.Stabilizing, fromInput: true);
            }
            // If currently stabilizing (e.g. second tap during a chain), finish immediately.
            else if (currentState == State.Stabilizing)
            {
                SetState(State.Stabilized, fromInput: true);
            }
            // If destabilizing, a new input can arrest the fall.
            else if (currentState == State.Destabilizing)
            {
                SetState(State.Stabilizing, fromInput: true);
            }
        }

        /// <summary>Public API: mark this block as failed by player input.</summary>
        public void RequestFail()
        {
            // Only noisy and stabilizing blocks can fail.
            if (currentState == State.Noisy || currentState == State.Stabilizing)
            {
                SetState(State.Destabilizing);
            }
        }

        /// <summary>Public API: advance the block toward Falling state.</summary>
        public void RequestFall()
        {
            if (currentState != State.Destabilizing && currentState != State.Falling) return;
            SetState(State.Falling);
        }

        /// <summary>Returns whether this block currently accepts player input.</summary>
        public bool CanAcceptInput()
        {
            return currentState == State.Noisy;
        }

        /// <summary>Schedules a safe release without exposing coroutine ownership to the tower system.</summary>
        public void RequestFallAfter(float delay, Func<bool> tokenIsCurrent)
        {
            if (currentState != State.Stabilized) return;
            StartCoroutine(ReleaseAfterDelay(delay, tokenIsCurrent));
        }

        /// <summary>Snaps a fallen block back to a deterministic grid position for recovery.</summary>
        public void RecoverToGrid(Vector3 worldPosition)
        {
            if (currentState == State.Falling) SetState(State.Stabilized);
            transform.position = worldPosition;
            baseLocalPosition = transform.localPosition;
            if (blockBody != null)
            {
                blockBody.linearVelocity = Vector3.zero;
                blockBody.angularVelocity = Vector3.zero;
                blockBody.isKinematic = true;
                blockBody.useGravity = false;
            }
        }

        private System.Collections.IEnumerator ReleaseAfterDelay(float delay, Func<bool> tokenIsCurrent)
        {
            yield return new WaitForSeconds(delay);
            if (tokenIsCurrent == null || tokenIsCurrent()) RequestFall();
        }

        // ---------- State-Specific Update Logic ----------

        private void UpdateNoisy()
        {
            // Continuous gentle vibration while noisy.
            float offset = Mathf.Sin(Time.time * vibrationSpeed + transform.position.y) * vibrationAmount;
            transform.localPosition = baseLocalPosition + new Vector3(offset, 0f, 0f);

            // Emit periodic sound rings while noisy.
            if (Time.time >= lastSoundRingTime + soundRingInterval)
            {
                lastSoundRingTime = Time.time;
                OnSoundRing?.Invoke();
            }

            // If the player successfully stabilized, transition immediately.
            // (The actual transition is performed in ApplyStateTransition
            // after the ApplyStateTransition timer completes; here we just
            // request the state change so the state machine handles it.)
        }

        private void UpdateStabilizing()
        {
            transitionTimer -= Time.deltaTime;
            UpdateStateVisuals();
            if (transitionTimer <= 0f) SetState(State.Stabilized);
        }

        private void UpdateStabilized()
        {
            // Stabilized blocks are visually still with warm amber color.
            // No vibration, no sound rings.
            // Material color is already set when the state was entered.
        }

        private void UpdateDestabilizing()
        {
            float offset = Mathf.Sin(Time.time * vibrationSpeed * 1.5f) * destabilizeShakeAmount;
            transform.localPosition = baseLocalPosition + new Vector3(offset, 0f, 0f);
            destabilizeTimer -= Time.deltaTime;
            UpdateStateVisuals();
            if (destabilizeTimer <= 0f) SetState(State.Falling);
        }

        private void UpdateFalling()
        {
            if (blockBody != null)
            {
                blockBody.isKinematic = false;
                blockBody.useGravity = true;
                return;
            }

            float fallProgress = Mathf.Clamp01((Time.time - fallStartTime) / 1f);
            float yOffset = fallCurve.Evaluate(fallProgress) * fallSpeed;
            transform.position = new Vector3(fallStartPosition.x, fallStartPosition.y - yOffset, fallStartPosition.z);
        }

        // ---------- Public API ----------

        /// <summary>Transition the block to a new state.</summary>
        /// <param name="newState">The target state.</param>
        /// <param name="fromInput">True when this change originates from a player tap.</param>
        private void SetState(State newState, bool fromInput = false, bool notifyLifecycle = true)
        {
            // Prevent duplicate state.
            if (currentState == newState)
            {
                // Still fire the event if we're re-entering the same state
                // via a different path (e.g. a second tap during a chain).
                OnStateChanged?.Invoke(newState);
                return;
            }

            // Emit the sound ring when going noisy → stabilizing → stabilized.
            if (currentState == State.Noisy && newState == State.Stabilized)
            {
                OnSoundRing?.Invoke();
            }

            // Fire state-change event.
            OnStateChanged?.Invoke(newState);
            currentState = newState;
            transitionTimer = transitionTime;
            destabilizeTimer = transitionTime;
            if (newState == State.Falling)
            {
                fallStartPosition = transform.position;
                fallStartTime = Time.time;
                if (blockBody != null)
                {
                    blockBody.isKinematic = false;
                    blockBody.useGravity = true;
                    blockBody.linearVelocity = Vector3.down * fallSpeed;
                }
            }
            else if (blockBody != null)
            {
                blockBody.isKinematic = true;
                blockBody.useGravity = false;
                blockBody.linearVelocity = Vector3.zero;
                blockBody.angularVelocity = Vector3.zero;
            }

            // Visual + material update.
            UpdateStateVisuals();

            // Design-time events.
            if (notifyLifecycle)
            {
                if (newState == State.Stabilized) OnStabilized?.Invoke();
                if (newState == State.Destabilizing) OnDestabilized?.Invoke();
                if (newState == State.Falling) OnFallen?.Invoke();
            }

            // Debug log for designers.
            if (broadcastEvents && currentState != newState)
            {
                Debug.Log($"[ResonanceBlock] {gameObject.name}: state → {newState}");
            }
        }

        /// <summary>Update material color and emission based on current state.</summary>
        private void UpdateStateVisuals()
        {
            if (blockMaterialInstance == null) return;

            Color color = Color.black;
            float emissionMult = 0.1f;

            switch (currentState)
            {
                case State.Noisy:
                    color = noisyColor;
                    emissionMult = 1.4f;
                    break;
                case State.Stabilizing:
                    // Blend between noisy and stabilized over the transition.
                    float t = 1f - (transitionTimer / transitionTime);
                    color = Color.Lerp(noisyColor, stabilizedColor, t);
                    emissionMult = Mathf.Lerp(1.4f, 0.35f, t);
                    break;
                case State.Stabilized:
                    color = stabilizedColor;
                    emissionMult = 0.35f;
                    break;
                case State.Destabilizing:
                    // Blend noisy toward a desaturated gray as it breaks.
                    color = Color.Lerp(noisyColor, Color.gray, 1f - (destabilizeTimer / transitionTime));
                    emissionMult = 0.2f;
                    break;
                case State.Falling:
                    // Dim, grayish while falling.
                    color = Color.Lerp(noisyColor, Color.gray, 0.7f);
                    emissionMult = 0.1f;
                    break;
            }

            blockMaterialInstance.color = color;
            blockMaterialInstance.SetColor("_EmissionColor", color * emissionMult);
        }

        // ---------- Events for other systems ----------

        /// <summary>Fired when a sound ring pulse is emitted.</summary>
        public event Action OnSoundRing;

        /// <summary>Fired when the block state changes.</summary>
        public event Action<State> OnStateChangedInternal;

        /// <summary>Fired when the block is successfully stabilized.</summary>
        public event Action OnStabilizedInternal;

        /// <summary>Fired when the block begins to destabilize.</summary>
        public event Action OnDestabilizedInternal;

        /// <summary>Fired when the block falls.</summary>
        public event Action OnFallenInternal;
    }
}