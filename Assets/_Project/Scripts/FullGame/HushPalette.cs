using UnityEngine;

namespace HushBlocks.FullGame
{
    /// <summary>Shared palette for the full game. Mirrors the art bible hex values.</summary>
    public static class HushPalette
    {
        public static readonly Color Ink = Hex("#05070E");
        public static readonly Color Midnight = Hex("#0B0E1A");
        public static readonly Color Charcoal = Hex("#1C1E24");
        public static readonly Color CharcoalLight = Hex("#252A36");
        public static readonly Color Cyan = Hex("#00F0FF");
        public static readonly Color Amber = Hex("#FFB300");
        public static readonly Color AmberSoft = Hex("#FFD166");
        public static readonly Color Text = Hex("#E8F0FF");
        public static readonly Color Muted = Hex("#8A97AD");
        public static readonly Color Panel = Hex("#0E1220");
        public static readonly Color PanelLight = Hex("#151C2E");
        public static readonly Color Border = Hex("#253048");
        public static readonly Color Danger = Hex("#FF3B5C");
        public static readonly Color Success = Hex("#3DFFB5");

        public static readonly Color CyanHdr = new Color(0f, 10.646f, 11.314f, 1f);
        public static readonly Color AmberHdr = new Color(4f, 2.808f, 0f, 1f);

        public static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out Color color);
            return color;
        }

        public static Color Hdr(Color color, float intensity)
        {
            return new Color(color.r * intensity, color.g * intensity, color.b * intensity, color.a);
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
