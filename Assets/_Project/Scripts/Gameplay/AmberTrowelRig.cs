using System;
using System.Collections;
using UnityEngine;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Kinetic tool visual effects and physical rebound rig for Noa's Amber Trowel.
    /// Manages the dynamic emissive crystal blade, attached dynamic point light flare,
    /// impact shockwave emission at the strike frame, and procedural arm/spine recoil IK shock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmberTrowelRig : MonoBehaviour
    {
        [Header("Blade Renderer & Materials")]
        [Tooltip("Renderer of the embedded amber crystal blade.")]
        [SerializeField] private Renderer crystalBladeRenderer;
        [Tooltip("Dynamic point light attached to the blade tip.")]
        [SerializeField] private Light bladePointLight;

        [Header("HDR Visual Palette")]
        [Tooltip("Idle honey-amber glow (+2.0 EV).")]
        [ColorUsage(true, true)]
        [SerializeField] private Color idleAmberColor = new Color(1.0f, 0.702f, 0.0f) * 4.0f;
        [Tooltip("Impact downswing white-gold flare (+25.0 EV).")]
        [ColorUsage(true, true)]
        [SerializeField] private Color impactFlareColor = new Color(1.0f, 0.95f, 0.82f) * 25.0f;
        [Tooltip("Idle point light intensity.")]
        [SerializeField, Range(0.5f, 3f)] private float idleLightIntensity = 1.2f;
        [Tooltip("Impact point light flare intensity.")]
        [SerializeField, Range(3f, 12f)] private float impactLightIntensity = 7.5f;

        [Header("Physical Rebound & Recoil")]
        [Tooltip("Optional transform of Noa's right arm/hand bone for procedural recoil offset.")]
        [SerializeField] private Transform handBoneTransform;
        [Tooltip("Optional transform of Noa's spine bone for structural shock absorption.")]
        [SerializeField] private Transform spineBoneTransform;
        [Tooltip("Distance in meters the tool recoils backward upon masonry impact.")]
        [SerializeField, Range(0.01f, 0.1f)] private float recoilDistance = 0.045f;
        [Tooltip("Recovery duration in seconds to smooth-damp back into hand socket.")]
        [SerializeField, Range(0.04f, 0.2f)] private float recoilRecoveryTime = 0.08f;

        [Header("Impact Shockwave VFX")]
        [Tooltip("Directional golden ring particle prefab spawned on masonry strike.")]
        [SerializeField] private ParticleSystem impactShockwavePrefab;

        // Cached Shader Property IDs (Zero-allocation)
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int AcousticBloomId = Shader.PropertyToID("_AcousticBloom");

        // Runtime Tracking
        private MaterialPropertyBlock propertyBlock;
        private Transform cachedTransform;
        private Vector3 localSocketRestPosition;
        private Quaternion localSocketRestRotation;
        private Vector3 currentRecoilOffset;
        private Vector3 recoilVelocity;
        private float flareTransitionTimer;
        private bool isFlaring;

        // Events
        public event Action OnTrowelImpact;

        private void Awake()
        {
            cachedTransform = transform;
            propertyBlock = new MaterialPropertyBlock();

            localSocketRestPosition = cachedTransform.localPosition;
            localSocketRestRotation = cachedTransform.localRotation;

            if (crystalBladeRenderer == null) crystalBladeRenderer = GetComponentInChildren<MeshRenderer>();
            if (bladePointLight == null) bladePointLight = GetComponentInChildren<Light>();
            if (bladePointLight == null)
            {
                var lightObj = new GameObject("BladePointLight", typeof(Light));
                lightObj.transform.SetParent(transform, false);
                bladePointLight = lightObj.GetComponent<Light>();
                bladePointLight.type = LightType.Point;
                bladePointLight.range = 4.2f;
                bladePointLight.color = new Color(1f, 0.75f, 0.2f);
                bladePointLight.shadows = LightShadows.None;
            }

            SetToolEmission(idleAmberColor, idleLightIntensity);
        }

        private void Update()
        {
            UpdateRecoilSpring();
            UpdateFlareDissipation();
        }

        // ---------- Tool Emission & Dynamic Lighting ----------

        /// <summary>
        /// Flares the blade crystal and point light to high-intensity white-gold (+25.0 EV)
        /// during the trowel downswing.
        /// </summary>
        public void FlareDownswing()
        {
            isFlaring = true;
            flareTransitionTimer = 0.15f;
            SetToolEmission(impactFlareColor, impactLightIntensity);
        }

        /// <summary>
        /// Pulses the trowel with sustained golden radiance during Harmonic Hush channeling.
        /// </summary>
        public void TriggerHarmonicGlow()
        {
            isFlaring = true;
            flareTransitionTimer = 0.65f;
            SetToolEmission(idleAmberColor * 2.5f, impactLightIntensity * 0.8f);
        }

        private void UpdateFlareDissipation()
        {
            if (!isFlaring) return;

            flareTransitionTimer -= Time.deltaTime;
            if (flareTransitionTimer <= 0f)
            {
                isFlaring = false;
                SetToolEmission(idleAmberColor, idleLightIntensity);
            }
        }

        private void SetToolEmission(Color emissionColor, float lightIntensity)
        {
            if (crystalBladeRenderer != null)
            {
                crystalBladeRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissionColorId, emissionColor);
                propertyBlock.SetFloat(AcousticBloomId, lightIntensity);
                crystalBladeRenderer.SetPropertyBlock(propertyBlock);
            }

            if (bladePointLight != null)
            {
                bladePointLight.color = Color.Lerp(Color.white, Color.yellow, 0.4f);
                bladePointLight.intensity = lightIntensity;
            }
        }

        // ---------- Impact Shockwave & Procedural Recoil IK ----------

        /// <summary>
        /// Called precisely on the impact strike frame (via Animation Event or NoaController).
        /// Emits the golden acoustic shockwave, applies physical arm recoil, and signals the active block.
        /// </summary>
        public void ExecuteImpactStrike(Vector3 strikePoint, Vector3 strikeNormal, HushBlock targetBlock)
        {
            // 1. Physical Rebound impulse: pushes hand/tool backward along strike normal
            currentRecoilOffset = -strikeNormal.normalized * recoilDistance;

            // 2. Spawn golden acoustic shockwave
            if (impactShockwavePrefab != null)
            {
                Quaternion shockwaveRot = Quaternion.LookRotation(strikeNormal, Vector3.up);
                ParticleSystem shock = Instantiate(impactShockwavePrefab, strikePoint, shockwaveRot);
                shock.Play();
                Destroy(shock.gameObject, 1.5f);
            }
            else
            {
                SpawnProceduralShockwave(strikePoint, strikeNormal);
            }

            // 3. Signal target block
            if (targetBlock != null)
            {
                targetBlock.MuteBlock(isPerfect: true);
            }

            OnTrowelImpact?.Invoke();
        }

        private void SpawnProceduralShockwave(Vector3 strikePoint, Vector3 strikeNormal)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "GoldenShockwaveRing";
            var col = ring.GetComponent<Collider>();
            if (col != null) Destroy(col);
            ring.transform.position = strikePoint;
            ring.transform.rotation = Quaternion.LookRotation(strikeNormal, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(0.1f, 0.005f, 0.1f);
            var mr = ring.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = FullGame.HushMaterials.Band;
                var block = new MaterialPropertyBlock();
                block.SetColor(Shader.PropertyToID("_BaseColor"), new Color(1f, 0.85f, 0.2f, 0.95f));
                block.SetColor(Shader.PropertyToID("_EmissionColor"), new Color(1f, 0.75f, 0.1f) * 6f);
                mr.SetPropertyBlock(block);
            }
            StartCoroutine(AnimateShockwave(ring.transform));
        }

        private IEnumerator AnimateShockwave(Transform shockTransform)
        {
            float elapsed = 0f;
            const float duration = 0.35f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(0.1f, 1.8f, 1f - Mathf.Pow(1f - t, 3f));
                if (shockTransform != null)
                {
                    shockTransform.localScale = new Vector3(scale, 0.005f, scale);
                }
                yield return null;
            }
            if (shockTransform != null) Destroy(shockTransform.gameObject);
        }

        private void UpdateRecoilSpring()
        {
            if (currentRecoilOffset.sqrMagnitude > 0.00001f)
            {
                currentRecoilOffset = Vector3.SmoothDamp(
                    currentRecoilOffset,
                    Vector3.zero,
                    ref recoilVelocity,
                    recoilRecoveryTime
                );

                cachedTransform.localPosition = localSocketRestPosition + currentRecoilOffset;

                // Procedural micro-shock to spine if assigned
                if (spineBoneTransform != null)
                {
                    spineBoneTransform.localRotation *= Quaternion.Euler(-currentRecoilOffset.y * 15f, 0f, 0f);
                }
            }
            else
            {
                cachedTransform.localPosition = localSocketRestPosition;
            }
        }

        /// <summary>
        /// Helper to snap the trowel rig into Noa's hand bone socket with zero local offset.
        /// </summary>
        public void AttachToHandSocket(Transform handSocket)
        {
            if (handSocket == null) return;
            if (cachedTransform == null) cachedTransform = transform;
            cachedTransform.SetParent(handSocket, false);
            cachedTransform.localPosition = Vector3.zero;
            cachedTransform.localRotation = Quaternion.identity;
            cachedTransform.localScale = Vector3.one;

            localSocketRestPosition = Vector3.zero;
            localSocketRestRotation = Quaternion.identity;
        }
    }
}
