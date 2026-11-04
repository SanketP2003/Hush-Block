using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HushBlocks.Gameplay;

namespace HushBlocks.UI
{
    /// <summary>
    /// Minimalist Tactile Voxel Diagetic HUD for Hush Blocks.
    /// Manages the honey-amber Silence Chain crystalline gauge, Harmonic Hush readiness,
    /// beat-synchronized concentric timing reticle ring over the active block,
    /// and camera micro-trauma screenshake feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HushScoreHUD : MonoBehaviour
    {
        [Header("Palette Reference (Hush Blocks Theme)")]
        private static readonly Color VelvetyCharcoal = new Color(0.11f, 0.118f, 0.141f, 1f); // #1C1E24
        private static readonly Color WarmAmber = new Color(1.0f, 0.702f, 0.0f, 1f);          // #FFB300
        private static readonly Color ElectricCyan = new Color(0.0f, 0.941f, 1.0f, 1f);        // #00F0FF

        [Header("Silence Chain Crystalline Gauge")]
        [Tooltip("Filled image representing the rising honey-amber acoustic fluid.")]
        [SerializeField] private Image chainFillImage;
        [Tooltip("Text displaying current chain multiplier (e.g. 'x7').")]
        [SerializeField] private TextMeshProUGUI chainCountText;
        [Tooltip("Chain count threshold required to unlock Harmonic Hush.")]
        [SerializeField, Min(3)] private int maxChainForHarmonicHush = 8;
        [Tooltip("Icon or glow element unlocked when Harmonic Hush is ready.")]
        [SerializeField] private GameObject harmonicHushReadyIcon;

        [Header("Acoustic Timing Reticle Ring")]
        [Tooltip("RectTransform of the concentric reticle ring snapping to the active block.")]
        [SerializeField] private RectTransform timingRingRect;
        [Tooltip("Image component of the timing ring for color shifts.")]
        [SerializeField] private Image timingRingImage;
        [Tooltip("Maximum expanded radius scale on off-beats.")]
        [SerializeField, Range(1.5f, 4f)] private float ringExpandedScale = 2.4f;
        [Tooltip("Target snapped radius scale on the exact beat impact point.")]
        [SerializeField, Range(0.8f, 1.2f)] private float ringSnappedScale = 1.0f;

        [Header("Camera & World Tracking")]
        [Tooltip("Main gameplay camera for world-to-screen projection.")]
        [SerializeField] private Camera gameplayCamera;
        [Tooltip("Canvas hosting the screen-space HUD.")]
        [SerializeField] private Canvas hudCanvas;

        [Header("Screenshake & Micro-Trauma")]
        [Tooltip("Transform of the camera rig shaken during misses or shatter strikes.")]
        [SerializeField] private Transform cameraRigTransform;
        [Tooltip("Shake duration in seconds.")]
        [SerializeField, Min(0.05f)] private float shakeDuration = 0.25f;
        [Tooltip("Shake displacement magnitude.")]
        [SerializeField, Min(0.01f)] private float shakeMagnitude = 0.12f;

        [Header("System Dependencies")]
        [SerializeField] private RhythmManager rhythmManager;
        [SerializeField] private TowerManager towerManager;
        [SerializeField] private AcousticInputController inputController;

        // Runtime Interpolation State
        private float currentFillAmount;
        private float targetFillAmount;
        private float fillVelocity;
        private Vector3 originalCameraLocalPos;
        private float currentShakeTimer;

        private void Awake()
        {
            if (gameplayCamera == null) gameplayCamera = Camera.main;
            if (rhythmManager == null) rhythmManager = UnityEngine.Object.FindAnyObjectByType<RhythmManager>();
            if (towerManager == null) towerManager = UnityEngine.Object.FindAnyObjectByType<TowerManager>();
            if (inputController == null) inputController = UnityEngine.Object.FindAnyObjectByType<AcousticInputController>();

            if (cameraRigTransform != null) originalCameraLocalPos = cameraRigTransform.localPosition;

            if (harmonicHushReadyIcon != null) harmonicHushReadyIcon.SetActive(false);
            if (chainFillImage != null) chainFillImage.color = WarmAmber;
        }

        private void OnEnable()
        {
            if (rhythmManager != null)
            {
                rhythmManager.OnSilenceChainChanged += HandleSilenceChainChanged;
                rhythmManager.OnInputScored += HandleInputScored;
            }

            if (inputController != null)
            {
                inputController.OnInputProcessed += HandleInputScored;
            }
        }

        private void OnDisable()
        {
            if (rhythmManager != null)
            {
                rhythmManager.OnSilenceChainChanged -= HandleSilenceChainChanged;
                rhythmManager.OnInputScored -= HandleInputScored;
            }

            if (inputController != null)
            {
                inputController.OnInputProcessed -= HandleInputScored;
            }
        }

        private void Update()
        {
            UpdateChainGauge();
            UpdateTimingReticle();
            UpdateCameraShake();
        }

        // ---------- Silence Chain Gauge ----------

        private void HandleSilenceChainChanged(int chain)
        {
            targetFillAmount = Mathf.Clamp01((float)chain / maxChainForHarmonicHush);

            if (chainCountText != null)
            {
                chainCountText.text = chain > 0 ? $"x{chain}" : string.Empty;
                chainCountText.color = chain >= maxChainForHarmonicHush ? WarmAmber : Color.white;
            }

            if (harmonicHushReadyIcon != null)
            {
                harmonicHushReadyIcon.SetActive(chain >= maxChainForHarmonicHush);
            }
        }

        private void UpdateChainGauge()
        {
            if (chainFillImage == null) return;

            currentFillAmount = Mathf.SmoothDamp(
                currentFillAmount,
                targetFillAmount,
                ref fillVelocity,
                0.12f
            );

            chainFillImage.fillAmount = currentFillAmount;
        }

        // ---------- Concentric Timing Reticle Ring ----------

        private void UpdateTimingReticle()
        {
            if (timingRingRect == null || rhythmManager == null) return;

            // 1. Position ring directly over the active sliding block in screen space
            if (towerManager != null && towerManager.ActiveBlock != null && gameplayCamera != null)
            {
                Vector3 worldPos = towerManager.ActiveBlock.transform.position;
                Vector3 screenPoint = gameplayCamera.WorldToScreenPoint(worldPos);

                if (screenPoint.z > 0)
                {
                    timingRingRect.gameObject.SetActive(true);
                    timingRingRect.position = screenPoint;
                }
                else
                {
                    timingRingRect.gameObject.SetActive(false);
                    return;
                }
            }
            else
            {
                timingRingRect.gameObject.SetActive(false);
                return;
            }

            // 2. Animate contraction: starts wide at start of beat, snaps inward to 1.0 on exact beat
            // RhythmManager.CurrentBeatProgress runs from 0.0 to 1.0
            float beatProgress = rhythmManager.CurrentBeatProgress;
            float currentScale = Mathf.Lerp(ringExpandedScale, ringSnappedScale, beatProgress);

            timingRingRect.localScale = new Vector3(currentScale, currentScale, 1f);

            // 3. Modulate reticle color: Cyan during approach, flashes Amber on exact beat snap
            if (timingRingImage != null)
            {
                Color targetRingColor = (beatProgress > 0.88f) ? WarmAmber : ElectricCyan;
                timingRingImage.color = targetRingColor;
            }
        }

        // ---------- Input Feedback & Screenshake ----------

        private void HandleInputScored(TimingResult result)
        {
            switch (result)
            {
                case TimingResult.Perfect:
                    TriggerCameraTrauma(intensity: 0.5f);
                    break;

                case TimingResult.Good:
                    TriggerCameraTrauma(intensity: 0.25f);
                    break;

                case TimingResult.Early:
                case TimingResult.Late:
                case TimingResult.Miss:
                    TriggerCameraTrauma(intensity: 1.0f);
                    break;
            }
        }

        public void TriggerCameraTrauma(float intensity)
        {
            currentShakeTimer = shakeDuration;
            shakeMagnitude = 0.12f * intensity;
        }

        private void UpdateCameraShake()
        {
            if (cameraRigTransform == null) return;

            if (currentShakeTimer > 0f)
            {
                currentShakeTimer -= Time.deltaTime;
                Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * shakeMagnitude;
                randomOffset.z = 0f; // Keep 2D side-view depth stable
                cameraRigTransform.localPosition = originalCameraLocalPos + randomOffset;
            }
            else
            {
                cameraRigTransform.localPosition = originalCameraLocalPos;
            }
        }
    }
}

