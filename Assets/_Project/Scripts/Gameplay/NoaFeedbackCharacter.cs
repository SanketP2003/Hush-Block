using UnityEngine;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Lightweight project-authored Noa placeholder for the fixed side-view plane.
    /// Character feedback is intentionally separate from block state and physics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoaFeedbackCharacter : MonoBehaviour
    {
        [SerializeField] private GreyboxGameController interactionSource;
        [SerializeField] private float idleBobAmount = 0.035f;
        [SerializeField] private float idleBobSpeed = 2.2f;
        [SerializeField] private float sealPulseSeconds = 0.28f;
        [SerializeField] private float sealPulseScale = 1.18f;
        [SerializeField] private Color idleColor = new Color(0.32f, 0.75f, 1f);
        [SerializeField] private Color sealColor = new Color(1f, 0.71f, 0.28f);

        private Renderer characterRenderer;
        private Material materialInstance;
        private Vector3 basePosition;
        private Vector3 baseScale;
        private float pulseUntil;

        public bool IsPulsing => Time.time < pulseUntil;

        private void Awake()
        {
            characterRenderer = GetComponentInChildren<Renderer>();
            basePosition = transform.localPosition;
            baseScale = transform.localScale;
            if (characterRenderer != null && characterRenderer.sharedMaterial != null)
            {
                materialInstance = new Material(characterRenderer.sharedMaterial);
                materialInstance.name = gameObject.name + " Material Instance";
                characterRenderer.material = materialInstance;
            }
            ApplyColor(idleColor);
        }

        private void OnEnable()
        {
            if (interactionSource == null) interactionSource = FindAnyObjectByType<GreyboxGameController>();
            if (interactionSource != null) interactionSource.OnVelvetSealApplied += HandleVelvetSeal;
        }

        private void OnDisable()
        {
            if (interactionSource != null) interactionSource.OnVelvetSealApplied -= HandleVelvetSeal;
        }

        private void OnDestroy()
        {
            if (materialInstance != null) Destroy(materialInstance);
        }

        private void Update()
        {
            float bob = Mathf.Sin(Time.time * idleBobSpeed) * idleBobAmount;
            transform.localPosition = basePosition + Vector3.up * bob;
            float pulse = IsPulsing ? Mathf.Lerp(1f, sealPulseScale, Mathf.PingPong(Time.time * 8f, 1f)) : 1f;
            transform.localScale = baseScale * pulse;
            if (!IsPulsing) ApplyColor(idleColor);
        }

        private void HandleVelvetSeal(ResonanceBlock block)
        {
            pulseUntil = Time.time + sealPulseSeconds;
            ApplyColor(sealColor);
        }

        private void ApplyColor(Color color)
        {
            if (materialInstance == null) return;
            materialInstance.color = color;
            materialInstance.SetColor("_EmissionColor", color * 0.7f);
        }
    }
}
