using UnityEngine;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Wires the Phase 1 greybox controller to the Phase 2 deterministic rhythm clock.
    ///
    /// Blocks can only be stabilized when input lands within the configured
    /// timing window. Missed beats are tracked separately from successful silences,
    /// and the silence chain decays after inactivity.
    /// </summary>
    [RequireComponent(typeof(RhythmManager))]
    [RequireComponent(typeof(GreyboxGameController))]
    public sealed class RhythmGameplayIntegration : MonoBehaviour
    {
        [Header("Feedback")]
        [SerializeField] private float feedbackPulseSeconds = 0.35f;

        private GreyboxGameController gameController;
        private RhythmManager rhythmManager;
        private float pulseUntil;
        private TimingResult lastResult;
        private long lastBeatIndex;

        public TimingResult LastResult => lastResult;

        private void Awake()
        {
            rhythmManager = GetComponent<RhythmManager>();
            gameController = GetComponent<GreyboxGameController>();

            if (rhythmManager == null)
            {
                Debug.LogError("[RhythmGameplayIntegration] RhythmManager is required on the same GameObject.", this);
            }

            if (gameController == null)
            {
                Debug.LogError("[RhythmGameplayIntegration] GreyboxGameController is required on the same GameObject.", this);
            }
        }

        private void OnEnable()
        {
            if (rhythmManager != null)
            {
                rhythmManager.OnInputScored += HandleInputScored;
                rhythmManager.OnSilenceChainChanged += HandleSilenceChainChanged;
                rhythmManager.OnBeat += HandleBeat;
                rhythmManager.OnAccurateInput += HandleAccurateInput;
                rhythmManager.OnEarlyInput += HandleEarlyInput;
                rhythmManager.OnLateInput += HandleLateInput;
                rhythmManager.OnMissedBeat += HandleMissedBeat;
            }
        }

        private void OnDisable()
        {
            if (rhythmManager != null)
            {
                rhythmManager.OnInputScored -= HandleInputScored;
                rhythmManager.OnSilenceChainChanged -= HandleSilenceChainChanged;
                rhythmManager.OnBeat -= HandleBeat;
                rhythmManager.OnAccurateInput -= HandleAccurateInput;
                rhythmManager.OnEarlyInput -= HandleEarlyInput;
                rhythmManager.OnLateInput -= HandleLateInput;
                rhythmManager.OnMissedBeat -= HandleMissedBeat;
            }
        }

        private void Update()
        {
            if (rhythmManager == null) return;

            if (lastBeatIndex != rhythmManager.CurrentBeatIndex && Time.time > pulseUntil)
            {
                lastBeatIndex = rhythmManager.CurrentBeatIndex;
            }
        }

        private void HandleInputScored(TimingResult result)
        {
            lastResult = result;
            pulseUntil = Time.time + feedbackPulseSeconds;
        }

        private void HandleSilenceChainChanged(int chain)
        {
            pulseUntil = Time.time + feedbackPulseSeconds;
        }

        private void HandleBeat(long beatIndex)
        {
            lastBeatIndex = beatIndex;
            pulseUntil = Time.time + feedbackPulseSeconds;
        }

        private void HandleAccurateInput(long beatIndex)
        {
            lastResult = TimingResult.Accurate;
            pulseUntil = Time.time + feedbackPulseSeconds;
        }

        private void HandleEarlyInput(long beatIndex)
        {
            lastResult = TimingResult.Early;
            pulseUntil = Time.time + feedbackPulseSeconds;
        }

        private void HandleLateInput(long beatIndex)
        {
            lastResult = TimingResult.Late;
            pulseUntil = Time.time + feedbackPulseSeconds;
        }

        private void HandleMissedBeat(long beatIndex)
        {
            lastResult = TimingResult.Miss;
            pulseUntil = Time.time + feedbackPulseSeconds;
        }
    }
}