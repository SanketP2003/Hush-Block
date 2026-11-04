using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Dynamic Isometric Diorama Camera Tracking and Micro-Trauma System for Hush Blocks.
    /// Locks camera to true isometric angles (X = 54.736°, Y = 45°), smoothly elevates
    /// with the ascending tower stack, dynamically updates Depth of Field focus distance
    /// for a tilt-shift miniature toy effect, and applies non-linear trauma screenshakes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class DioramaCameraController : MonoBehaviour
    {
        [Header("True Isometric Framing")]
        [Tooltip("Pitch angle along X axis (54.736° true isometric or 35.264° classic).")]
        [SerializeField] private float isometricPitch = 54.736f;
        [Tooltip("Yaw angle along Y axis (45° corner diorama).")]
        [SerializeField] private float isometricYaw = 45.0f;
        [Tooltip("Orthographic viewport half-height size. Standard: 6.5.")]
        [SerializeField, Min(2f)] private float orthographicSize = 6.5f;
        [Tooltip("Distance from tower center along the isometric viewing vector.")]
        [SerializeField, Min(5f)] private float viewDistance = 22.0f;

        [Header("Vertical Elevation Follow")]
        [Tooltip("SmoothDamp smoothing duration for vertical elevator tracking.")]
        [SerializeField, Min(0.05f)] private float followSmoothTime = 0.35f;
        [Tooltip("Vertical viewport offset placing the active layer in the lower-middle sweet spot.")]
        [SerializeField] private float verticalSweetSpotOffset = 1.2f;

        [Header("Tilt-Shift Miniature Depth of Field")]
        [Tooltip("Global Post-Processing Volume containing Depth of Field override.")]
        [SerializeField] private Volume globalVolume;
        [Tooltip("Dynamically adjusts focus distance to match the active layer.")]
        [SerializeField] private bool enableDynamicDepthOfField = true;

        [Header("Micro-Trauma Screenshake")]
        [Tooltip("Trauma decay rate per second (linear dissipation).")]
        [SerializeField, Min(0.1f)] private float traumaDecay = 1.6f;
        [Tooltip("Maximum positional shake displacement in meters at trauma = 1.0.")]
        [SerializeField, Min(0.01f)] private float maxPositionalShake = 0.28f;
        [Tooltip("Maximum rotational shake in degrees at trauma = 1.0.")]
        [SerializeField, Min(0.1f)] private float maxRotationalShake = 2.2f;

        [Header("System References")]
        [SerializeField] private TowerManager towerManager;
        [SerializeField] private FeedbackHowlerAI howlerAI;

        // Camera & Tracking State
        private Camera targetCamera;
        private Transform cameraTransform;
        private DepthOfField depthOfField;
        private Vector3 targetFocusPoint;
        private Vector3 currentElevationVelocity;
        private float currentTrackedY;

        // Trauma State
        private float trauma; // [0, 1]
        private Vector3 shakeOffset;
        private Vector3 rotationalShakeEuler;

        // Public Properties
        public float CurrentTrauma => trauma;
        public Camera TargetCamera => targetCamera;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            cameraTransform = transform;

            if (targetCamera != null)
            {
                targetCamera.enabled = true;
                targetCamera.orthographic = true;
                targetCamera.orthographicSize = orthographicSize;
            }

            var cameraData = GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            cameraData.renderPostProcessing = true;

            if (towerManager == null) towerManager = UnityEngine.Object.FindAnyObjectByType<TowerManager>();
            if (howlerAI == null) howlerAI = UnityEngine.Object.FindAnyObjectByType<FeedbackHowlerAI>();

            InitializeVolumeOverrides();
            ApplyIsometricRotation();

            currentTrackedY = towerManager != null ? towerManager.transform.position.y : 0f;
        }

        private void OnEnable()
        {
            if (towerManager != null)
            {
                towerManager.OnBlockAnchored += HandleBlockAnchored;
                towerManager.OnTowerCollapsed += HandleTowerCollapsed;
            }

            if (howlerAI != null)
            {
                howlerAI.OnShriekLaunched += HandleHowlerShriek;
                howlerAI.OnCounterOverloadSuccess += HandleHowlerOverloaded;
            }
        }

        private void OnDisable()
        {
            if (towerManager != null)
            {
                towerManager.OnBlockAnchored -= HandleBlockAnchored;
                towerManager.OnTowerCollapsed -= HandleTowerCollapsed;
            }

            if (howlerAI != null)
            {
                howlerAI.OnShriekLaunched -= HandleHowlerShriek;
                howlerAI.OnCounterOverloadSuccess -= HandleHowlerOverloaded;
            }
        }

        private void LateUpdate()
        {
            UpdateElevationTracking();
            UpdateTraumaScreenshake();
            UpdateTiltShiftFocus();
        }

        // ---------- Isometric Orientation & Elevation Follow ----------

        public void ApplyIsometricRotation()
        {
            cameraTransform.rotation = Quaternion.Euler(isometricPitch, isometricYaw, 0f);
        }

        private void UpdateElevationTracking()
        {
            float targetY = 0f;

            if (towerManager != null)
            {
                // Target the current layer elevation with viewport sweet-spot compensation
                targetY = (towerManager.CurrentHeight * 1.0f) + verticalSweetSpotOffset;
            }

            currentTrackedY = Mathf.SmoothDamp(
                currentTrackedY,
                targetY,
                ref currentElevationVelocity.y,
                followSmoothTime
            );

            // Construct isometric look vector
            Vector3 towerCenter = towerManager != null ? towerManager.transform.position : Vector3.zero;
            Vector3 targetCenterPoint = new Vector3(towerCenter.x, currentTrackedY, towerCenter.z);

            Vector3 viewDirection = cameraTransform.forward;
            Vector3 desiredPosition = targetCenterPoint - (viewDirection * viewDistance);

            // Combine with micro-trauma displacement
            cameraTransform.position = desiredPosition + shakeOffset;
            cameraTransform.rotation = Quaternion.Euler(
                isometricPitch + rotationalShakeEuler.x,
                isometricYaw + rotationalShakeEuler.y,
                rotationalShakeEuler.z
            );
        }

        // ---------- Tilt-Shift Dynamic Depth of Field ----------

        private void InitializeVolumeOverrides()
        {
            if (globalVolume == null)
            {
                globalVolume = UnityEngine.Object.FindAnyObjectByType<Volume>();
            }

            if (globalVolume != null && globalVolume.profile != null)
            {
                globalVolume.profile.TryGet(out depthOfField);
            }
        }

        private void UpdateTiltShiftFocus()
        {
            if (!enableDynamicDepthOfField || depthOfField == null) return;

            // Focus distance corresponds to camera view distance to active layer
            depthOfField.focusDistance.value = viewDistance;
        }

        // ---------- Micro-Trauma Screenshake System ----------

        public void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + amount);
        }

        /// <summary>Subtle, weighted tactile thump on successful amber block muting.</summary>
        public void TriggerHeavySnapShake()
        {
            AddTrauma(0.24f);
        }

        /// <summary>Rapid, jarring horizontal shudder during a Feedback Howler shriek.</summary>
        public void TriggerShriekShake()
        {
            AddTrauma(0.60f);
        }

        /// <summary>Sustained, low-frequency rumble during physical tower collapse.</summary>
        public void TriggerCollapseShake()
        {
            AddTrauma(1.0f);
        }

        private void UpdateTraumaScreenshake()
        {
            if (trauma <= 0f)
            {
                shakeOffset = Vector3.zero;
                rotationalShakeEuler = Vector3.zero;
                return;
            }

            // Non-linear trauma response: Shake = Trauma^2
            float shakePower = trauma * trauma;

            // Perlin noise continuous smooth vibration
            float time = Time.time * 35f;
            float offsetX = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f;
            float offsetY = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f;
            float offsetZ = (Mathf.PerlinNoise(time, time) - 0.5f) * 2f;

            shakeOffset = cameraTransform.right * (offsetX * maxPositionalShake * shakePower)
                        + cameraTransform.up * (offsetY * maxPositionalShake * shakePower);

            rotationalShakeEuler = new Vector3(
                offsetY * maxRotationalShake * shakePower,
                offsetX * maxRotationalShake * shakePower,
                offsetZ * maxRotationalShake * shakePower * 0.75f
            );

            // Dissipate trauma linearly
            trauma = Mathf.Max(0f, trauma - (traumaDecay * Time.deltaTime));
        }

        // ---------- Event Callbacks ----------

        private void HandleBlockAnchored(HushBlock block, TimingResult result)
        {
            TriggerHeavySnapShake();
        }

        private void HandleTowerCollapsed()
        {
            TriggerCollapseShake();
        }

        private void HandleHowlerShriek()
        {
            TriggerShriekShake();
        }

        private void HandleHowlerOverloaded()
        {
            AddTrauma(0.45f);
        }
    }
}

