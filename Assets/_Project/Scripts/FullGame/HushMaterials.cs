using UnityEngine;

namespace HushBlocks.FullGame
{
    /// <summary>
    /// Shared runtime materials. Block renderers use MaterialPropertyBlock for
    /// per-instance tint/emission so the whole tower batches without baking
    /// dozens of one-off materials.
    /// </summary>
    public static class HushMaterials
    {
        private static Material _body;
        private static Material _band;
        private static Material _ground;
        private static Material _silhouette;
        private static Material _sky;
        private static Material _sprite;
        private static Material _particle;
        private static Material _unlit;

        private static Shader LitShader =>
            Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        private static Shader UnlitShader =>
            Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

        public static Material Body
        {
            get
            {
                if (_body == null)
                {
                    _body = MakeLit("M_Hush_BlockBody", HushPalette.Charcoal, 0.19f, 0.08f, Color.black, 0f);
                }
                return _body;
            }
        }

        public static Material Band
        {
            get
            {
                if (_band == null)
                {
                    _band = MakeLit("M_Hush_BlockBand", Color.white, 0.42f, 0f, HushPalette.CyanHdr, 1f);
                }
                return _band;
            }
        }

        public static Material Ground
        {
            get
            {
                if (_ground == null)
                {
                    _ground = MakeLit("M_Hush_Ground", HushPalette.Hex("#0B1220"), 0.28f, 0.18f, HushPalette.Hex("#071019"), 0.15f);
                }
                return _ground;
            }
        }

        public static Material Silhouette
        {
            get
            {
                if (_silhouette == null)
                {
                    _silhouette = MakeUnlit("M_Hush_Silhouette", HushPalette.Hex("#070B16"), null);
                }
                return _silhouette;
            }
        }

        public static Material Sprite
        {
            get
            {
                if (_sprite == null)
                {
                    _sprite = MakeUnlit("M_Hush_Sprite", Color.white, HushArt.Circle.texture);
                }
                return _sprite;
            }
        }

        public static Material Particle
        {
            get
            {
                if (_particle == null)
                {
                    var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Particles/Standard Unlit");
                    _particle = new Material(shader)
                    {
                        name = "M_Hush_Dust",
                        mainTexture = HushArt.Circle.texture
                    };
                }
                return _particle;
            }
        }

        public static Material Unlit
        {
            get
            {
                if (_unlit == null) _unlit = MakeUnlit("M_Hush_Unlit", Color.white, null);
                return _unlit;
            }
        }

        public static Material GetSky(int district)
        {
            if (_sky == null)
            {
                _sky = MakeUnlit("M_Hush_Sky", Color.white, HushArt.SkyTexture(district));
                _sky.mainTexture = HushArt.SkyTexture(district);
                if (_sky.HasProperty("_Cull")) _sky.SetFloat("_Cull", 0f);
            }
            else
            {
                _sky.mainTexture = HushArt.SkyTexture(district);
            }
            return _sky;
        }

        public static void ApplyBlock(Renderer body, Renderer band, Color bodyColor, Color bodyEmission, Color bandColor, float bandIntensity)
        {
            var properties = new MaterialPropertyBlock();

            if (body != null)
            {
                body.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", bodyColor);
                properties.SetColor("_EmissionColor", bodyEmission);
                body.SetPropertyBlock(properties);
            }

            if (band != null)
            {
                band.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", bandColor);
                properties.SetColor("_EmissionColor", bandColor * bandIntensity);
                band.SetPropertyBlock(properties);
            }
        }

        public static Material MakeLit(string name, Color baseColor, float smoothness, float metallic, Color emission, float emissionIntensity)
        {
            var material = new Material(LitShader)
            {
                name = name,
                enableInstancing = true
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", baseColor);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission * emissionIntensity);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return material;
        }

        public static Material MakeUnlit(string name, Color color, Texture texture)
        {
            var material = new Material(UnlitShader)
            {
                name = name
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                material.mainTexture = texture;
            }
            return material;
        }
    }
}
