using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HushBlocks.FullGame
{
    /// <summary>
    /// Runtime art generation. The game ships without bitmap assets: every
    /// panel, icon and sky is drawn procedurally so the build stays small and
    /// the palette can be tuned in one place.
    /// </summary>
    public static class HushArt
    {
        private static readonly Dictionary<string, Sprite> Sprites = new();
        private static readonly Dictionary<int, Texture2D> Skies = new();
        private static Sprite _rounded;
        private static Sprite _roundedSmall;
        private static Sprite _circle;
        private static Sprite _ring;
        private static Sprite _star;
        private static Sprite _heart;
        private static Sprite _vignette;
        private static Sprite _scrim;
        private static Sprite _glow;
        private static TMP_FontAsset _headingFont;
        private static TMP_FontAsset _bodyFont;

        public static TMP_FontAsset HeadingFont
        {
            get
            {
                if (_headingFont == null)
                {
                    _headingFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
                    if (_headingFont == null) _headingFont = BodyFont;
                }
                return _headingFont;
            }
        }

        public static TMP_FontAsset BodyFont
        {
            get
            {
                if (_bodyFont == null)
                {
                    _bodyFont = TMP_Settings.defaultFontAsset;
                    if (_bodyFont == null) _bodyFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                }
                return _bodyFont;
            }
        }

        public static Sprite Rounded => _rounded != null ? _rounded : _rounded = MakeRounded(64, 18);
        public static Sprite RoundedSmall => _roundedSmall != null ? _roundedSmall : _roundedSmall = MakeRounded(40, 12);
        public static Sprite Circle => _circle != null ? _circle : _circle = MakeCircle(64);
        public static Sprite Ring => _ring != null ? _ring : _ring = MakeRing(128, 5.5f);
        public static Sprite Star => _star != null ? _star : _star = MakeStar(96);
        public static Sprite Heart => _heart != null ? _heart : _heart = MakeHeart(96);
        public static Sprite Glow => _glow != null ? _glow : _glow = MakeGlow(128);
        public static Sprite Vignette => _vignette != null ? _vignette : _vignette = MakeVignette(256);
        public static Sprite Scrim => _scrim != null ? _scrim : _scrim = MakeHorizontalFade(256);

        public static Sprite VerticalGradient(Color top, Color bottom)
        {
            string key = "grad_" + ColorUtility.ToHtmlStringRGBA(top) + ColorUtility.ToHtmlStringRGBA(bottom);
            if (Sprites.TryGetValue(key, out Sprite existing)) return existing;

            const int w = 8;
            const int h = 256;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                Color32 row = Color.Lerp(bottom, top, t);
                for (int x = 0; x < w; x++) pixels[y * w + x] = row;
            }

            var texture = NewTexture(w, h, FilterMode.Bilinear);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            Sprites[key] = sprite;
            return sprite;
        }

        public static Sprite HorizontalGradient(Color left, Color right)
        {
            string key = "hgrad_" + ColorUtility.ToHtmlStringRGBA(left) + ColorUtility.ToHtmlStringRGBA(right);
            if (Sprites.TryGetValue(key, out Sprite existing)) return existing;

            const int w = 256;
            const int h = 8;
            var pixels = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                Color32 column = Color.Lerp(left, right, x / (float)(w - 1));
                for (int y = 0; y < h; y++) pixels[y * w + x] = column;
            }

            var texture = NewTexture(w, h, FilterMode.Bilinear);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            Sprites[key] = sprite;
            return sprite;
        }

        /// <summary>Opaque, camera-facing night sky with stars, moon and skyline.</summary>
        public static Texture2D SkyTexture(int district)
        {
            if (Skies.TryGetValue(district, out Texture2D cached)) return cached;

            var definition = DistrictCatalog.Get(district);
            const int width = 1024;
            const int height = 768;
            var pixels = new Color32[width * height];
            var random = new System.Random(1337 + district * 97);

            Color horizonGlow = definition.Horizon;
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                Color row = Color.Lerp(definition.SkyBottom, definition.SkyTop, Mathf.Pow(t, 0.72f));
                float horizon = Mathf.Exp(-Mathf.Pow((t - 0.16f) / 0.11f, 2f)) * 0.55f;
                row = Color.Lerp(row, horizonGlow, horizon);

                // Gentle vertical banding keeps large gradients from looking flat.
                float dither = (Mathf.PerlinNoise(y * 0.04f, district * 8.1f) - 0.5f) * 0.018f;
                row += new Color(dither, dither, dither, 0f);

                Color32 value = row;
                int rowStart = y * width;
                for (int x = 0; x < width; x++) pixels[rowStart + x] = value;
            }

            // Stars.
            for (int i = 0; i < 260; i++)
            {
                int x = random.Next(8, width - 8);
                int y = random.Next((int)(height * 0.24f), height - 12);
                float brightness = 0.25f + (float)random.NextDouble() * 0.75f;
                float size = (float)random.NextDouble() < 0.12f ? 2f : 1f;
                Color tint = Color.Lerp(definition.Moon, Color.white, (float)random.NextDouble() * 0.5f);
                PlotStar(pixels, width, height, x, y, tint, brightness, size);
            }

            // Moon with a soft halo.
            float moonX = width * (0.70f + district * 0.035f);
            float moonY = height * (0.70f - district * 0.03f);
            float moonRadius = width * (0.052f - district * 0.004f);
            DrawMoon(pixels, width, height, moonX, moonY, moonRadius, definition.Moon);

            // Two skyline layers.
            DrawSkyline(pixels, width, height, random, definition, 0.115f, Color.Lerp(definition.SkyTop, horizonGlow, 0.32f), 0, false);
            DrawSkyline(pixels, width, height, random, definition, 0.075f, Color.Lerp(definition.SkyTop, Color.black, 0.18f), 1, true);

            var texture = NewTexture(width, height, FilterMode.Bilinear);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            texture.name = "HushSky_" + definition.Name;
            Skies[district] = texture;
            return texture;
        }

        public static Sprite StarSprite => Star;

        // ------------------------------------------------------------------ drawing

        private static Sprite MakeRounded(int size, int radius)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - size * 0.5f;
                    float py = y + 0.5f - size * 0.5f;
                    float half = size * 0.5f;
                    float qx = Mathf.Abs(px) - (half - radius);
                    float qy = Mathf.Abs(py) - (half - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
                    float distance = outside + inside - radius;
                    float alpha = Mathf.Clamp01(0.5f - distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            var texture = NewTexture(size, size, FilterMode.Bilinear);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            int border = Mathf.Clamp(radius, 1, size / 2 - 1);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        private static Sprite MakeCircle(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - size * 0.5f;
                    float py = y + 0.5f - size * 0.5f;
                    float distance = Mathf.Sqrt(px * px + py * py);
                    float alpha = Mathf.Clamp01(size * 0.5f - distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            var texture = NewTexture(size, size, FilterMode.Bilinear);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeGlow(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size * 2f - 1f;
                    float py = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(px * px + py * py);
                    float alpha = Mathf.Exp(-distance * distance * 5f) * Mathf.Clamp01(1f - distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            var texture = NewTexture(size, size, FilterMode.Bilinear);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeRing(int size, float thickness)
        {
            var pixels = new Color32[size * size];
            float radius = size * 0.5f - thickness - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - size * 0.5f;
                    float py = y + 0.5f - size * 0.5f;
                    float distance = Mathf.Sqrt(px * px + py * py);
                    float band = Mathf.Abs(distance - radius);
                    float alpha = Mathf.Clamp01(thickness * 0.5f - band + 0.5f);
                    alpha *= Mathf.Clamp01(1f - Mathf.Abs(distance - size * 0.42f) / (size * 0.08f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255));
                }
            }

            var texture = NewTexture(size, size, FilterMode.Bilinear);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeStar(int size)
        {
            const int points = 5;
            var polygon = new Vector2[points * 2];
            for (int i = 0; i < polygon.Length; i++)
            {
                float radius = i % 2 == 0 ? 1f : 0.42f;
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / points;
                polygon[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size * 2f - 1f;
                    float py = (y + 0.5f) / size * 2f - 1f;
                    float coverage = 0f;
                    for (int sy = 0; sy < 3; sy++)
                    {
                        for (int sx = 0; sx < 3; sx++)
                        {
                            var sample = new Vector2(px + (sx - 1) * 0.02f, py + (sy - 1) * 0.02f);
                            if (PointInPolygon(sample, polygon)) coverage += 1f / 9f;
                        }
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(coverage * 255));
                }
            }

            var texture = NewTexture(size, size, FilterMode.Bilinear);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeHeart(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size * 2.6f - 1.3f;
                    float py = (y + 0.5f) / size * 2.6f - 1.1f;
                    float coverage = 0f;
                    for (int sy = 0; sy < 3; sy++)
                    {
                        for (int sx = 0; sx < 3; sx++)
                        {
                            float sxv = px + (sx - 1) * 0.025f;
                            float syv = py + (sy - 1) * 0.025f;
                            float a = sxv * sxv + syv * syv - 1f;
                            if (a * a * a - sxv * sxv * syv * syv * syv <= 0f) coverage += 1f / 9f;
                        }
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(coverage * 255));
                }
            }

            var texture = NewTexture(size, size, FilterMode.Bilinear);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeVignette(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size * 2f - 1f;
                    float py = (y + 0.5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(px * px + py * py);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1.12f, radius)) * 0.72f;
                    pixels[y * size + x] = new Color32(3, 5, 12, (byte)(alpha * 255));
                }
            }

            var texture = NewTexture(size, size, FilterMode.Bilinear);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeHorizontalFade(int size)
        {
            var pixels = new Color32[size * size];
            for (int x = 0; x < size; x++)
            {
                float t = x / (float)(size - 1);
                byte alpha = (byte)(Mathf.Pow(1f - t, 1.5f) * 235f);
                for (int y = 0; y < size; y++) pixels[y * size + x] = new Color32(4, 6, 14, alpha);
            }

            var texture = NewTexture(size, size, FilterMode.Bilinear);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void PlotStar(Color32[] pixels, int width, int height, int x, int y, Color tint, float brightness, float size)
        {
            Color color = tint * brightness;
            color.a = 1f;
            int radius = Mathf.CeilToInt(size);
            for (int oy = -radius; oy <= radius; oy++)
            {
                for (int ox = -radius; ox <= radius; ox++)
                {
                    int px = x + ox;
                    int py = y + oy;
                    if (px < 0 || py < 0 || px >= width || py >= height) continue;
                    float falloff = 1f - Mathf.Clamp01(Mathf.Sqrt(ox * ox + oy * oy) / (size + 0.6f));
                    if (falloff <= 0f) continue;
                    Color32 existing = pixels[py * width + px];
                    pixels[py * width + px] = Color.Lerp(existing, color, falloff);
                }
            }
        }

        private static void DrawMoon(Color32[] pixels, int width, int height, float centerX, float centerY, float radius, Color tint)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - radius * 3f));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(centerX + radius * 3f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(centerY - radius * 3f));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(centerY + radius * 3f));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    int index = y * width + x;
                    if (distance <= radius)
                    {
                        float edge = Mathf.Clamp01(radius - distance);
                        Color disc = Color.Lerp(tint, Color.white, Mathf.Clamp01(1f - distance / radius) * 0.35f);
                        pixels[index] = Color.Lerp(pixels[index], disc, edge);
                    }
                    else
                    {
                        float glow = Mathf.Exp(-(distance - radius) / (radius * 0.55f)) * 0.38f;
                        if (glow > 0.004f) pixels[index] = Color.Lerp(pixels[index], tint, Mathf.Clamp01(glow));
                    }
                }
            }
        }

        private static void DrawSkyline(
            Color32[] pixels,
            int width,
            int height,
            System.Random random,
            DistrictDefinition definition,
            float maxHeightNormalized,
            Color color,
            int layer,
            bool windows)
        {
            int x = -random.Next(0, 30);
            int maxHeight = Mathf.RoundToInt(height * maxHeightNormalized);
            while (x < width)
            {
                int buildingWidth = random.Next(28, 86);
                int buildingHeight = random.Next(maxHeight / 3, maxHeight + 1);
                Color buildingColor = color * (layer == 0 ? 0.92f : 1f);
                buildingColor.a = 1f;

                for (int bx = Mathf.Max(0, x); bx < Mathf.Min(width, x + buildingWidth); bx++)
                {
                    for (int by = 0; by < buildingHeight; by++)
                    {
                        pixels[by * width + bx] = buildingColor;
                    }

                    // Antenna / roof detail.
                    if (random.NextDouble() < 0.05)
                    {
                        int antenna = buildingHeight + random.Next(4, 16);
                        for (int by = buildingHeight; by < Mathf.Min(height, antenna); by++)
                        {
                            pixels[by * width + bx] = buildingColor;
                        }
                    }
                }

                if (windows)
                {
                    for (int wy = 10; wy < buildingHeight - 6; wy += 9)
                    {
                        for (int wx = x + 6; wx < x + buildingWidth - 4; wx += 11)
                        {
                            if (wx < 0 || wx >= width) continue;
                            if (random.NextDouble() > 0.22) continue;
                            Color lit = Color.Lerp(definition.Moon, definition.Accent, (float)random.NextDouble() * 0.5f) * 0.55f;
                            lit.a = 1f;
                            pixels[wy * width + wx] = lit;
                            if (wx + 1 < width) pixels[wy * width + wx + 1] = lit;
                        }
                    }
                }

                x += buildingWidth + random.Next(2, 18);
            }
        }

        private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static Texture2D NewTexture(int width, int height, FilterMode filter)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = filter
            };
            return texture;
        }
    }
}
