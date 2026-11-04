using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Dual-compatible input handler for Hush Blocks.
    /// Supports Unity's New Input System (Touchscreen, Mouse, Keyboard) with automatic
    /// fallback to legacy Input (Input.GetMouseButtonDown, Spacebar).
    /// Executes non-allocating Raycast queries to select and mute blocks against the rhythm clock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HushBlockInputHandler : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Audio conductor managing sample-accurate rhythm timing.")]
        [SerializeField] private RhythmManager rhythmManager;
        [Tooltip("Camera used for raycasting screen taps into world space.")]
        [SerializeField] private Camera interactionCamera;

        [Header("Raycast Settings")]
        [Tooltip("Layer mask for selectable HushBlocks. Default: all layers or 'HushBlock'.")]
        [SerializeField] private LayerMask blockLayerMask = ~0;
        [Tooltip("Maximum raycast distance for tapping voxel blocks.")]
        [SerializeField, Min(1f)] private float maxRaycastDistance = 50f;

        // Cached non-allocating raycast buffer
        private readonly RaycastHit[] hitBuffer = new RaycastHit[4];
        private bool wasLegacyPressed;

        private void Awake()
        {
            if (interactionCamera == null) interactionCamera = Camera.main;
            if (rhythmManager == null) rhythmManager = GetComponent<RhythmManager>();
            if (rhythmManager == null) rhythmManager = Object.FindAnyObjectByType<RhythmManager>();
        }

        private void Update()
        {
            bool inputFired = false;
            Vector2 screenPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            // 1. New Input System: Touchscreen
            if (Touchscreen.current != null)
            {
                var touches = Touchscreen.current.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].press.wasPressedThisFrame)
                    {
                        inputFired = true;
                        screenPosition = touches[i].position.ReadValue();
                        break;
                    }
                }
            }

            // 2. New Input System: Mouse
            if (!inputFired && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                inputFired = true;
                screenPosition = Mouse.current.position.ReadValue();
            }

            // 3. New Input System: Keyboard Spacebar (targets nearest unmuted block)
            if (!inputFired && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TriggerCenterTap();
                return;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            // 4. Legacy Input Fallback
            if (!inputFired)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    inputFired = true;
                    screenPosition = Input.mousePosition;
                }
                else if (Input.GetKeyDown(KeyCode.Space))
                {
                    TriggerCenterTap();
                    return;
                }
            }
#endif

            if (inputFired)
            {
                ProcessScreenTap(screenPosition);
            }
        }

        /// <summary>
        /// Casts a ray into the scene and evaluates the hit block against the rhythm conductor.
        /// </summary>
        public void ProcessScreenTap(Vector2 screenPosition)
        {
            if (interactionCamera == null || rhythmManager == null) return;

            Ray ray = interactionCamera.ScreenPointToRay(screenPosition);
            int hitCount = Physics.RaycastNonAlloc(ray, hitBuffer, maxRaycastDistance, blockLayerMask);

            if (hitCount > 0)
            {
                // Find closest block
                float closestDist = float.MaxValue;
                HushBlock selectedBlock = null;

                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = hitBuffer[i];
                    HushBlock block = hit.collider.GetComponentInParent<HushBlock>();
                    if (block != null && hit.distance < closestDist)
                    {
                        closestDist = hit.distance;
                        selectedBlock = block;
                    }
                }

                if (selectedBlock != null)
                {
                    EvaluateBlockTap(selectedBlock);
                }
            }
        }

        private void TriggerCenterTap()
        {
            if (interactionCamera == null) return;
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            ProcessScreenTap(screenCenter);
        }

        private void EvaluateBlockTap(HushBlock block)
        {
            if (block == null || block.IsSettled) return;

            TimingResult result = rhythmManager.EvaluateCurrentInput(out long beatIndex);

            switch (result)
            {
                case TimingResult.Perfect:
                    block.MuteBlock(isPerfect: true);
                    break;

                case TimingResult.Good:
                    block.MuteBlock(isPerfect: false);
                    break;

                case TimingResult.Early:
                case TimingResult.Late:
                case TimingResult.Miss:
                default:
                    block.TriggerAntiGravity();
                    break;
            }
        }
    }
}

