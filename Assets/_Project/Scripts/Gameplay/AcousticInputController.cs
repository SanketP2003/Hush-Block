using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Multi-platform acoustic input controller for Hush Blocks.
    /// Intercepts rhythmic player taps, interfaces with TowerManager and RhythmManager,
    /// triggers Noa's trowel animation, and dispatches ceramic clicks, crystalline chimes,
    /// and discordant bass buzzes alongside mobile haptics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AcousticInputController : MonoBehaviour
    {
        [Header("System References")]
        [Tooltip("Core stack lifecycle manager handling block placement and slicing.")]
        [SerializeField] private TowerManager towerManager;
        [Tooltip("Rhythm conductor managing timing evaluation.")]
        [SerializeField] private RhythmManager rhythmManager;
        [Tooltip("Optional Animator controller for Noa (the player character).")]
        [SerializeField] private Animator noaAnimator;
        [Tooltip("AudioSource used to play immediate placement feedback SFX.")]
        [SerializeField] private AudioSource audioSource;

        [Header("Acoustic Sound Palette")]
        [Tooltip("Tight wooden/ceramic click played on a Good placement hit.")]
        [SerializeField] private AudioClip goodHitClip;
        [Tooltip("Resonant crystalline chime played on a Perfect hit or silence chain streak.")]
        [SerializeField] private AudioClip perfectHitClip;
        [Tooltip("Discordant acoustic bass buzz played on a Missed beat or early/late tap.")]
        [SerializeField] private AudioClip missHitClip;

        [Header("Haptic & Animation Tuning")]
        [Tooltip("Triggers mobile hardware vibration on taps.")]
        [SerializeField] private bool enableHaptics = true;
        [Tooltip("Name of the trigger parameter on Noa's animator controller.")]
        [SerializeField] private string noaTapTriggerName = "MutingTap";

        // Cached Animator Hash (Zero-allocation)
        private int noaTapTriggerHash;

        // Events
        public event Action<TimingResult> OnInputProcessed;

        private void Awake()
        {
            if (towerManager == null) towerManager = UnityEngine.Object.FindAnyObjectByType<TowerManager>();
            if (rhythmManager == null) rhythmManager = UnityEngine.Object.FindAnyObjectByType<RhythmManager>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            noaTapTriggerHash = Animator.StringToHash(noaTapTriggerName);
        }

        private void Update()
        {
            if (CheckPlayerTapInput())
            {
                ExecuteMutingTap();
            }
        }

        /// <summary>
        /// Detects tap input across Touchscreen, Mouse, Keyboard, and legacy input systems.
        /// </summary>
        private bool CheckPlayerTapInput()
        {
#if ENABLE_INPUT_SYSTEM
            // 1. Touchscreen Tap
            if (Touchscreen.current != null)
            {
                var touches = Touchscreen.current.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].press.wasPressedThisFrame) return true;
                }
            }

            // 2. Mouse Left-Click
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            // 3. Keyboard Spacebar
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            // 4. Legacy Input Fallback
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                return true;
            }
#endif

            return false;
        }

        /// <summary>
        /// Executes a rhythmic placement tap: triggers Noa's animation, evaluates timing,
        /// and plays corresponding acoustic audio and haptic feedback.
        /// </summary>
        public void ExecuteMutingTap()
        {
            if (towerManager == null || towerManager.IsCollapsed || towerManager.IsLocked) return;

            // 1. Trigger Noa's amber trowel swing animation precisely at impact moment
            if (noaAnimator != null && noaAnimator.runtimeAnimatorController != null)
            {
                noaAnimator.SetTrigger(noaTapTriggerHash);
            }

            // 2. Evaluate block placement through TowerManager
            TimingResult result = towerManager.TryPlaceCurrentBlock();

            // 3. Fire acoustic audio and haptic triggers
            PlayAcousticFeedback(result);

            OnInputProcessed?.Invoke(result);
        }

        private void PlayAcousticFeedback(TimingResult result)
        {
            AudioClip clipToPlay = null;

            switch (result)
            {
                case TimingResult.Perfect:
                    clipToPlay = perfectHitClip != null ? perfectHitClip : goodHitClip;
                    TriggerMobileHaptic(intensity: 1.0f);
                    break;

                case TimingResult.Good:
                    clipToPlay = goodHitClip;
                    TriggerMobileHaptic(intensity: 0.5f);
                    break;

                case TimingResult.Early:
                case TimingResult.Late:
                case TimingResult.Miss:
                default:
                    clipToPlay = missHitClip;
                    TriggerMobileHaptic(intensity: 0.8f);
                    break;
            }

            if (audioSource != null && clipToPlay != null)
            {
                audioSource.PlayOneShot(clipToPlay);
            }
        }

        private void TriggerMobileHaptic(float intensity)
        {
            if (!enableHaptics) return;

#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}

