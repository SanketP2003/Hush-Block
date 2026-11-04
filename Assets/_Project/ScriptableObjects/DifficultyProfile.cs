using UnityEngine;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Configurable timing and difficulty profile for the Hush Blocks rhythm system.
    /// Change these values on the asset to retune beat windows across levels without touching code.
    /// </summary>
    [CreateAssetMenu(menuName = "Hush Blocks/Difficulty Profile")]
    public sealed class DifficultyProfile : ScriptableObject
    {
        [Header("Tempo")]
        [Tooltip("Beats per minute. The beat interval is derived from this value.")]
        [SerializeField] private float bpm = 120f;

        [Header("Timing Windows (seconds)")]
        [Tooltip("Maximum deviation from the exact beat that still counts as accurate.")]
        [SerializeField] private float accurateWindow = 0.12f;
        [Tooltip("Extra tolerance beyond the accurate window for an early hit.")]
        [SerializeField] private float earlyWindow = 0.15f;
        [Tooltip("Extra tolerance beyond the accurate window for a late hit.")]
        [SerializeField] private float lateWindow = 0.15f;

        [Header("Chain")]
        [Tooltip("Seconds of inactivity before the silence chain resets.")]
        [SerializeField] private float chainDecaySeconds = 3f;

        public float Bpm => bpm;

        /// <summary>Seconds between beats at the current tempo.</summary>
        public float BeatInterval => bpm > 0f ? 60f / bpm : 1f;

        public float AccurateWindow => accurateWindow;
        public float EarlyWindow => earlyWindow;
        public float LateWindow => lateWindow;
        public float ChainDecaySeconds => chainDecaySeconds;

        public void SetBpm(float value)
        {
            bpm = Mathf.Max(1f, value);
        }
    }
}
