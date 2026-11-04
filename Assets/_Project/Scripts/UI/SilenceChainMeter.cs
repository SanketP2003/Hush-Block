using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HushBlocks.UI
{
    /// <summary>
    /// The Acoustic Mortar Vial (Silence Chain Gauge) for Hush Blocks.
    /// Visualizes consecutive silent rhythm placements through a stylized vertical crystal vial
    /// filled with glowing liquid honey-amber light. Reaching streak milestones (5x, 10x) unlocks
    /// golden sparks, energetic bubbling, and the Harmonic Hush Ready glyph.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SilenceChainMeter : MonoBehaviour
    {
        [Header("Palette Configuration")]
        private static readonly Color VelvetyCharcoal = new Color(0.11f, 0.118f, 0.141f, 1f); // #1C1E24
        private static readonly Color LiquidHoneyAmber = new Color(1.0f, 0.702f, 0.0f, 1f);   // #FFB300
        private static readonly Color RadiantCyan = new Color(0.0f, 0.941f, 1.0f, 1f);        // #00F0FF
        private static readonly Color GoldenMilestoneGlow = new Color(1.0f, 0.88f, 0.5f, 1f); // #FFE082

        [Header("Liquid Column & Voxel Vial")]
        [Tooltip("Vertical Image component displaying the rising honey-amber acoustic mortar.")]
        [SerializeField] private Image liquidFillImage;
        [Tooltip("Background casing Image of the crystal vial.")]
        [SerializeField] private Image vialCasingImage;
        [Tooltip("Glow overlay Image pulsing when milestones are achieved.")]
        [SerializeField] private Image vialGlowOverlay;

        [Header("Chain Count Readout")]
        [Tooltip("Pixel-font text displaying current chain multiplier (e.g. 'x7').")]
        [SerializeField] private TextMeshProUGUI chainCountText;
        [Tooltip("Transform punched during chain increments.")]
        [SerializeField] private RectTransform punchTargetRect;

        [Header("Milestone Glyphs & Particles")]
        [Tooltip("Target chain required for the Harmonic Hush Ready glyph.")]
        [SerializeField, Min(3)] private int maxChainTarget = 10;
        [Tooltip("Glyph root activated when streak reaches maxChainTarget.")]
        [SerializeField] private GameObject harmonicHushGlyph;
        [Tooltip("CanvasGroup controlling glyph alpha transitions.")]
        [SerializeField] private CanvasGroup glyphCanvasGroup;
        [Tooltip("Optional UI ParticleSystem crackling with golden sparks at 5x / 10x.")]
        [SerializeField] private ParticleSystem sparkParticles;

        [Header("Animation Tuning")]
        [Tooltip("Smoothing time for liquid level fill interpolation.")]
        [SerializeField, Range(0.05f, 0.5f)] private float fillSmoothTime = 0.18f;
        [Tooltip("Punch scale multiplier on successful placement.")]
        [SerializeField, Range(1.05f, 1.4f)] private float punchScale = 1.18f;
        [Tooltip("Shudder magnitude when the chain breaks on a missed beat.")]
        [SerializeField, Min(1f)] private float breakShakeMagnitude = 6.0f;

        // Pre-allocated text strings to eliminate runtime garbage collection
        private static readonly string[] ChainTextCache = new string[64];
        static SilenceChainMeter()
        {
            for (int i = 0; i < ChainTextCache.Length; i++)
            {
                ChainTextCache[i] = i > 0 ? $"x{i}" : string.Empty;
            }
        }

        // Runtime Interpolation State
        private int currentChain = 0;
        private float currentFill = 0f;
        private float targetFill = 0f;
        private float fillVelocity = 0f;

        private Vector3 originalLocalPos;
        private Vector3 originalLocalScale;
        private float punchProgress = 1.0f;
        private float shakeTimer = 0f;
        private bool isHarmonicReady = false;

        public int CurrentChain => currentChain;
        public bool IsHarmonicReady => isHarmonicReady;

        private void Awake()
        {
            if (punchTargetRect == null) punchTargetRect = GetComponent<RectTransform>();
            if (punchTargetRect != null)
            {
                originalLocalPos = punchTargetRect.localPosition;
                originalLocalScale = punchTargetRect.localScale;
            }

            if (liquidFillImage != null)
            {
                liquidFillImage.type = Image.Type.Filled;
                liquidFillImage.fillMethod = Image.FillMethod.Vertical;
                liquidFillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
                liquidFillImage.color = LiquidHoneyAmber;
            }

            if (glyphCanvasGroup != null)
            {
                glyphCanvasGroup.alpha = 0f;
            }

            if (harmonicHushGlyph != null)
            {
                harmonicHushGlyph.SetActive(false);
            }

            UpdateDisplayImmediate(0);
        }

        private void Update()
        {
            UpdateLiquidFillSmooth();
            UpdatePunchTween();
            UpdateBreakShake();
            UpdateHarmonicPulse();
        }

        /// <summary>
        /// Updates the vial fill level and dispatches animations based on the active Silence Chain.
        /// </summary>
        public void SetChain(int newChain)
        {
            int previousChain = currentChain;
            currentChain = Mathf.Max(0, newChain);

            targetFill = Mathf.Clamp01((float)currentChain / Mathf.Max(1, maxChainTarget));

            if (currentChain > previousChain)
            {
                // Successful streak extension
                TriggerPunch();
                if (liquidFillImage != null) liquidFillImage.color = LiquidHoneyAmber;

                // Milestone triggers (5x, 10x)
                if (currentChain == 5)
                {
                    TriggerMilestoneSparks(isMajor: false);
                }
                else if (currentChain >= maxChainTarget && !isHarmonicReady)
                {
                    isHarmonicReady = true;
                    TriggerMilestoneSparks(isMajor: true);
                    ActivateHarmonicGlyph(true);
                }
            }
            else if (currentChain < previousChain)
            {
                // Chain broken on a miss
                TriggerBreakAnimation();
            }

            UpdateTextDisplay();
        }

        public void UpdateBeatPulse(float beatProgress)
        {
            // If Harmonic Hush is fully charged, pulse the vial overlay to the quarter-note rhythm
            if (isHarmonicReady && vialGlowOverlay != null)
            {
                float pulse = Mathf.Sin(beatProgress * Mathf.PI);
                Color glow = GoldenMilestoneGlow;
                glow.a = Mathf.Lerp(0.2f, 0.75f, pulse);
                vialGlowOverlay.color = glow;
            }
        }

        private void UpdateLiquidFillSmooth()
        {
            if (liquidFillImage == null) return;

            if (!Mathf.Approximately(currentFill, targetFill))
            {
                currentFill = Mathf.SmoothDamp(currentFill, targetFill, ref fillVelocity, fillSmoothTime);
                liquidFillImage.fillAmount = currentFill;
            }
        }

        private void TriggerPunch()
        {
            punchProgress = 0f;
        }

        private void TriggerBreakAnimation()
        {
            isHarmonicReady = false;
            ActivateHarmonicGlyph(false);

            shakeTimer = 0.28f;
            if (liquidFillImage != null)
            {
                // Flash cyan on shatter before draining
                liquidFillImage.color = RadiantCyan;
            }
        }

        private void UpdatePunchTween()
        {
            if (punchProgress < 1.0f && punchTargetRect != null)
            {
                punchProgress += Time.deltaTime * 5.0f;
                float t = Mathf.Clamp01(punchProgress);
                // Elastic punch overshoot curve
                float scaleMod = 1.0f + (punchScale - 1.0f) * Mathf.Sin(t * Mathf.PI);
                punchTargetRect.localScale = originalLocalScale * scaleMod;
            }
        }

        private void UpdateBreakShake()
        {
            if (shakeTimer > 0f && punchTargetRect != null)
            {
                shakeTimer -= Time.deltaTime;
                float progress = Mathf.Clamp01(shakeTimer / 0.28f);
                float xOffset = (UnityEngine.Random.value * 2f - 1f) * breakShakeMagnitude * progress;
                punchTargetRect.localPosition = originalLocalPos + new Vector3(xOffset, 0f, 0f);

                if (shakeTimer <= 0f)
                {
                    punchTargetRect.localPosition = originalLocalPos;
                    if (liquidFillImage != null) liquidFillImage.color = LiquidHoneyAmber;
                }
            }
        }

        private void UpdateHarmonicPulse()
        {
            if (glyphCanvasGroup != null && isHarmonicReady)
            {
                glyphCanvasGroup.alpha = Mathf.MoveTowards(glyphCanvasGroup.alpha, 1.0f, Time.deltaTime * 3.5f);
            }
        }

        private void ActivateHarmonicGlyph(bool activate)
        {
            if (harmonicHushGlyph != null)
            {
                harmonicHushGlyph.SetActive(activate);
            }

            if (glyphCanvasGroup != null)
            {
                if (!activate) glyphCanvasGroup.alpha = 0f;
            }
        }

        private void TriggerMilestoneSparks(bool isMajor)
        {
            if (sparkParticles != null)
            {
                sparkParticles.Emit(isMajor ? 24 : 12);
            }
        }

        private void UpdateTextDisplay()
        {
            if (chainCountText == null) return;

            if (currentChain < ChainTextCache.Length)
            {
                chainCountText.text = ChainTextCache[currentChain];
            }
            else
            {
                chainCountText.text = $"x{currentChain}";
            }

            chainCountText.color = isHarmonicReady ? GoldenMilestoneGlow : LiquidHoneyAmber;
        }

        private void UpdateDisplayImmediate(int chain)
        {
            currentChain = chain;
            targetFill = Mathf.Clamp01((float)chain / Mathf.Max(1, maxChainTarget));
            currentFill = targetFill;

            if (liquidFillImage != null)
            {
                liquidFillImage.fillAmount = currentFill;
            }

            UpdateTextDisplay();
        }
    }
}

