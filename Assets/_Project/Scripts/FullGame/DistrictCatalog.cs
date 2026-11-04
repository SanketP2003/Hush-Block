using UnityEngine;

namespace HushBlocks.FullGame
{
    /// <summary>
    /// Immutable tuning for one district. Everything that changes between
    /// districts lives here so gameplay, UI and audio read from one source.
    /// </summary>
    public sealed class DistrictDefinition
    {
        public readonly int Index;
        public readonly string Name;
        public readonly string Tagline;
        public readonly string Description;
        public readonly int Bpm;
        public readonly int TargetFloors;
        public readonly int TwoStarScore;
        public readonly int ThreeStarScore;
        public readonly float StartWidth;
        public readonly float SwayRange;
        public readonly float SwayGrowth;
        public readonly Color Accent;
        public readonly Color SkyTop;
        public readonly Color SkyBottom;
        public readonly Color Horizon;
        public readonly Color Moon;
        public readonly string[] Tips;

        public DistrictDefinition(
            int index,
            string name,
            string tagline,
            string description,
            int bpm,
            int targetFloors,
            int twoStarScore,
            int threeStarScore,
            float startWidth,
            float swayRange,
            float swayGrowth,
            string accent,
            string skyTop,
            string skyBottom,
            string horizon,
            string moon,
            params string[] tips)
        {
            Index = index;
            Name = name;
            Tagline = tagline;
            Description = description;
            Bpm = bpm;
            TargetFloors = targetFloors;
            TwoStarScore = twoStarScore;
            ThreeStarScore = threeStarScore;
            StartWidth = startWidth;
            SwayRange = swayRange;
            SwayGrowth = swayGrowth;
            Accent = HushPalette.Hex(accent);
            SkyTop = HushPalette.Hex(skyTop);
            SkyBottom = HushPalette.Hex(skyBottom);
            Horizon = HushPalette.Hex(horizon);
            Moon = HushPalette.Hex(moon);
            Tips = tips;
        }

        public string Number => $"DISTRICT 0{Index + 1}";
        public float BeatDuration => 60f / Bpm;
    }

    /// <summary>The three-district campaign, ordered by difficulty.</summary>
    public static class DistrictCatalog
    {
        public static readonly DistrictDefinition[] All =
        {
            new DistrictDefinition(
                0,
                "Whisper Row",
                "THE CITY HOLDS ITS BREATH",
                "Low rooftops, slow pulse. Learn the weight of silence.",
                96,
                10,
                2800,
                4500,
                3.4f,
                2.2f,
                0.02f,
                "#00F0FF",
                "#05070E",
                "#0F1D38",
                "#1B4A63",
                "#C9F4FF",
                "TAP ON THE BEAT TO SILENCE VIBRATING BLOCKS.",
                "PERFECT TIMING PRESERVES 100% BLOCK WIDTH.",
                "CHAIN 5 PERFECTS TO RESTORE LOST FOUNDATION.",
                "AMBER BLOCKS ARE ANCHORED & BALANCED."),
            new DistrictDefinition(
                1,
                "Echo Terrace",
                "EVERY BEAT LEAVES A MARK",
                "The terraces answer back. Wider swings, quicker pulse.",
                112,
                14,
                4200,
                6500,
                3.1f,
                2.5f,
                0.03f,
                "#C7A6FF",
                "#060512",
                "#191238",
                "#35205E",
                "#E9D7FF",
                "FASTER PULSE — WATCH THE AMBER RETICLE.",
                "FEEDBACK HOWLER APPROACHING ON FLOOR 7.",
                "COUNTER-STRIKE: TAP OFF-BEAT TO SHATTER HOWLERS.",
                "SLICING ACTIVE — REMAIN IN THE GROOVE."),
            new DistrictDefinition(
                2,
                "Resonant Apex",
                "THE LAST SIGNAL BEFORE DAWN",
                "A narrow summit, a hard pulse, and a sky on fire.",
                128,
                18,
                5600,
                8500,
                2.85f,
                2.85f,
                0.04f,
                "#FFB300",
                "#050409",
                "#241015",
                "#6B3A12",
                "#FFD7A0",
                "APEX TEMPO — SHATTER HOWLERS & MASTER SILENCE.",
                "MISSES TRIGGER SEISMIC TOWER TREMORS.",
                "REACH FLOOR 18 TO BLOOM THE ROOFTOP SANCTUARY.",
                "HARMONIZE THE SKYLINE BEFORE DAWN.")
        };

        public static int Count => All.Length;

        public static DistrictDefinition Get(int index)
        {
            return All[Mathf.Clamp(index, 0, All.Length - 1)];
        }
    }
}
