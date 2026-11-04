using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HushBlocks.FullGame
{
    /// <summary>
    /// The diorama: night sky, distant city, resonance platform, lighting,
    /// post-processing, dust and the attract-mode tower. Owns the camera rig.
    /// </summary>
    public sealed class HushStage : MonoBehaviour
    {
        public bool IsBuilt { get; private set; }
        public HushCameraRig CameraRig { get; private set; }
        public Camera Camera { get; private set; }
        public Transform BlockRoot { get; private set; }
        public Transform DebrisRoot { get; private set; }
        public Transform ShowcaseRoot { get; private set; }
        public GameObject Noa { get; private set; }
        public HushBlocks.Gameplay.NoaController NoaController { get; private set; }
        public GameObject Howler { get; private set; }
        public HushBlocks.Gameplay.FeedbackHowlerAI HowlerAI { get; private set; }
        public GameObject Sanctuary { get; private set; }

        private Light rimLight;
        private Light cyanLight;
        private ParticleSystem dust;
        private Renderer padRenderer;
        private Renderer[] frameBars;
        private MaterialPropertyBlock padBlock;
        private int district;
        private float pulse;
        private float showcasePhase;

        public void Build()
        {
            if (IsBuilt) return;
            IsBuilt = true;

            BuildCamera();
            BuildEnvironment();
            BuildLighting();
            BuildPostProcessing();
            BuildDust();
            BuildRoots();
            BuildShowcase();
            SetDistrict(0);
            SetShowcase(true);
            CameraRig.Snap(3.2f, -2.6f, 0f);
            CameraRig.SetFraming(-2.6f, true);

            var listeners = FindObjectsByType<AudioListener>();
            if (listeners.Length != 1)
            {
                Debug.LogError($"[HushStage] Expected exactly one AudioListener, but found {listeners.Length}.");
            }
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("Hush Camera", typeof(Camera), typeof(AudioListener), typeof(HushCameraRig));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";

            Camera = cameraObject.GetComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = HushPalette.Ink;
            Camera.fieldOfView = 34f;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 240f;
            Camera.allowHDR = true;
            Camera.allowMSAA = false;
            Camera.depth = 0f;

            var data = Camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            data.renderShadows = true;

            CameraRig = cameraObject.GetComponent<HushCameraRig>();
            CameraRig.Setup(Camera);

            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sky.name = "Night Sky";
            Destroy(sky.GetComponent<Collider>());
            sky.transform.SetParent(cameraObject.transform, false);
            sky.transform.localPosition = new Vector3(0f, 0f, 55f);
            sky.transform.localRotation = Quaternion.identity;
            sky.transform.localScale = new Vector3(112f, 72f, 1f);
            var skyRenderer = sky.GetComponent<MeshRenderer>();
            skyRenderer.sharedMaterial = HushMaterials.GetSky(0);
            skyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            skyRenderer.receiveShadows = false;
        }

        private void BuildEnvironment()
        {
            HushArtAssets.EnsureLoaded();

            // 1. Authored Blender Bedrock Foundation
            if (HushArtAssets.BedrockPrefab != null)
            {
                var bedrock = Instantiate(HushArtAssets.BedrockPrefab, transform);
                bedrock.name = "BedrockFoundation";
                bedrock.transform.localPosition = new Vector3(0f, -4.8f, 0f);
                bedrock.transform.localRotation = Quaternion.identity;

                padBlock = new MaterialPropertyBlock();
                padRenderer = bedrock.GetComponentInChildren<MeshRenderer>();
            }

            // 2. Authored Blender Brownstone Diorama
            if (HushArtAssets.BrownstonePrefab != null)
            {
                var brownstone = Instantiate(HushArtAssets.BrownstonePrefab, transform);
                brownstone.name = "BrownstoneDiorama";
                brownstone.transform.localPosition = new Vector3(0f, 0f, 6f);
                brownstone.transform.localRotation = Quaternion.identity;
            }

            // 3. Authored Blender Noa Character
            if (HushArtAssets.NoaPrefab != null)
            {
                Noa = Instantiate(HushArtAssets.NoaPrefab, new Vector3(1.8f, 0.23f, -0.4f), Quaternion.Euler(0f, -60f, 0f), transform);
                Noa.name = "Noa";
                NoaController = Noa.GetComponent<HushBlocks.Gameplay.NoaController>() ?? Noa.GetComponentInChildren<HushBlocks.Gameplay.NoaController>();
            }

            // 4. Authored Blender Feedback Howler
            if (HushArtAssets.HowlerPrefab != null)
            {
                Howler = Instantiate(HushArtAssets.HowlerPrefab, new Vector3(-2.8f, 3.4f, 0f), Quaternion.Euler(0f, 60f, 0f), transform);
                Howler.name = "FeedbackHowler";
                HowlerAI = Howler.GetComponent<HushBlocks.Gameplay.FeedbackHowlerAI>() ?? Howler.GetComponentInChildren<HushBlocks.Gameplay.FeedbackHowlerAI>();
                Howler.SetActive(false); // Hidden until hazard encounter!
            }

            // Ground base for infinite diorama depth
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Night Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.position = new Vector3(0f, -7.5f, 0f);
            ground.transform.localScale = new Vector3(120f, 1f, 120f);
            var groundRenderer = ground.GetComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = HushMaterials.Ground;
            groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void BuildLighting()
        {
            var keyObject = new GameObject("Key Light", typeof(Light));
            keyObject.transform.SetParent(transform, false);
            keyObject.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
            var key = keyObject.GetComponent<Light>();
            key.type = LightType.Directional;
            key.color = HushPalette.Hex("#CFE4FF");
            key.intensity = 1.6f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.72f;
            key.shadowBias = 0.045f;

            var fillObject = new GameObject("Fill Light", typeof(Light));
            fillObject.transform.SetParent(transform, false);
            fillObject.transform.rotation = Quaternion.Euler(24f, 142f, 0f);
            var fill = fillObject.GetComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = HushPalette.Hex("#5876B4");
            fill.intensity = 0.55f;
            fill.shadows = LightShadows.None;

            var rimObject = new GameObject("Rim Light", typeof(Light));
            rimObject.transform.SetParent(transform, false);
            rimObject.transform.position = new Vector3(4.8f, 5.2f, -3.4f);
            rimLight = rimObject.GetComponent<Light>();
            rimLight.type = LightType.Point;
            rimLight.color = HushPalette.Amber;
            rimLight.intensity = 20f;
            rimLight.range = 17f;
            rimLight.shadows = LightShadows.None;

            var cyanObject = new GameObject("Cyan Bounce", typeof(Light));
            cyanObject.transform.SetParent(transform, false);
            cyanObject.transform.position = new Vector3(-4.5f, 2.8f, 1.4f);
            cyanLight = cyanObject.GetComponent<Light>();
            cyanLight.type = LightType.Point;
            cyanLight.color = HushPalette.Cyan;
            cyanLight.intensity = 12f;
            cyanLight.range = 13f;
            cyanLight.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = HushPalette.Hex("#182236");
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.reflectionIntensity = 0.4f;
        }

        private void BuildPostProcessing()
        {
            var volumeObject = new GameObject("Post FX", typeof(Volume));
            volumeObject.transform.SetParent(transform, false);
            var volume = volumeObject.GetComponent<Volume>();
            volume.isGlobal = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Hush Post FX";
            volume.sharedProfile = profile;

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 0.95f;
            bloom.intensity.value = 1.25f;
            bloom.scatter.value = 0.7f;
            bloom.tint.value = new Color(1f, 0.92f, 0.82f);
            bloom.highQualityFiltering.value = true;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.33f;
            vignette.smoothness.value = 0.46f;
            vignette.color.value = HushPalette.Ink;

            var adjustments = profile.Add<ColorAdjustments>(true);
            adjustments.postExposure.value = 0.12f;
            adjustments.contrast.value = 13f;
            adjustments.saturation.value = 9f;
            adjustments.colorFilter.value = Color.white;

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.ACES;

            var grain = profile.Add<FilmGrain>(true);
            grain.type.value = FilmGrainLookup.Thin1;
            grain.intensity.value = 0.11f;
            grain.response.value = 0.8f;
        }

        private void BuildDust()
        {
            var dustObject = new GameObject("Resonance Dust", typeof(ParticleSystem));
            dustObject.transform.SetParent(transform, false);
            dustObject.transform.position = new Vector3(0f, 7f, 0f);
            dust = dustObject.GetComponent<ParticleSystem>();

            var main = dust.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.55f, 0.95f, 1f, 0.35f),
                new Color(1f, 0.78f, 0.35f, 0.25f));
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = dust.emission;
            emission.rateOverTime = 6f;

            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 16f, 14f);

            var velocity = dust.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            var colorOverLifetime = dust.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.85f, 0.15f),
                    new GradientAlphaKey(0.85f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = dustObject.GetComponent<ParticleSystemRenderer>();
            renderer.material = HushMaterials.Particle;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Depth;
            dust.Play();
        }

        private void BuildRoots()
        {
            BlockRoot = new GameObject("Tower").transform;
            BlockRoot.SetParent(transform, false);
            DebrisRoot = new GameObject("Debris").transform;
            DebrisRoot.SetParent(transform, false);
            ShowcaseRoot = new GameObject("Showcase").transform;
            ShowcaseRoot.SetParent(transform, false);
        }

        private void BuildShowcase()
        {
            float[] widths = { 3.4f, 3.2f, 3.1f, 2.9f, 2.8f, 2.5f, 2.2f, 1.9f };
            float[] offsets = { 0f, 0.02f, -0.03f, 0.05f, -0.02f, 0.03f, 0f, -0.02f };
            float top = 0.18f;
            for (int i = 0; i < widths.Length; i++)
            {
                bool attuned = i % 2 == 0 || i == widths.Length - 1;
                var block = BlockView.Create(ShowcaseRoot, offsets[i], top, widths[i], 0.55f, 1.9f, attuned);
                if (attuned) block.Attune(true);
                top += 0.55f + 0.012f;
            }
        }

        public void SetShowcase(bool visible)
        {
            if (ShowcaseRoot != null) ShowcaseRoot.gameObject.SetActive(visible);
        }

        public void ClearTower()
        {
            if (Sanctuary != null)
            {
                Destroy(Sanctuary);
                Sanctuary = null;
            }
            if (Howler != null)
            {
                Howler.SetActive(false);
            }
            if (BlockRoot == null) return;
            for (int i = BlockRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(BlockRoot.GetChild(i).gameObject);
            }
            if (DebrisRoot != null)
            {
                for (int i = DebrisRoot.childCount - 1; i >= 0; i--)
                {
                    Destroy(DebrisRoot.GetChild(i).gameObject);
                }
            }
        }

        public GameObject SpawnSanctuary(Vector3 position, float width)
        {
            if (Sanctuary != null) Destroy(Sanctuary);
            if (HushArtAssets.SanctuaryPrefab == null) return null;

            Sanctuary = Instantiate(HushArtAssets.SanctuaryPrefab, BlockRoot != null ? BlockRoot : transform);
            Sanctuary.name = "RooftopSanctuary_Blooming";
            Sanctuary.transform.position = position;
            Sanctuary.transform.rotation = Quaternion.identity;
            float targetScale = Mathf.Clamp(width / 7.2f, 0.35f, 0.55f);
            Sanctuary.transform.localScale = Vector3.zero;

            StartCoroutine(AnimateSanctuaryBloom(Sanctuary.transform, targetScale));
            Burst(position + Vector3.up * 0.4f, HushPalette.Amber, 24, 1.8f);
            return Sanctuary;
        }

        private System.Collections.IEnumerator AnimateSanctuaryBloom(Transform sanctTransform, float targetScale)
        {
            float elapsed = 0f;
            const float duration = 1.2f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float k = 1f - Mathf.Pow(1f - t, 3f);
                float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
                if (sanctTransform != null)
                {
                    sanctTransform.localScale = Vector3.one * (targetScale * k * overshoot);
                }
                yield return null;
            }
            if (sanctTransform != null)
            {
                sanctTransform.localScale = Vector3.one * targetScale;
            }
        }

        public void SetDistrict(int index)
        {
            district = Mathf.Clamp(index, 0, DistrictCatalog.Count - 1);
            var definition = DistrictCatalog.Get(district);
            HushMaterials.GetSky(district);
            if (Camera != null) Camera.backgroundColor = definition.SkyTop;
            if (rimLight != null) rimLight.color = definition.Accent;
            if (cyanLight != null) cyanLight.color = Color.Lerp(definition.Accent, HushPalette.Cyan, 0.4f);

            HushMaterials.Silhouette.SetColor("_BaseColor", Color.Lerp(definition.SkyTop, Color.black, 0.35f));
            HushMaterials.Silhouette.SetColor("_Color", Color.Lerp(definition.SkyTop, Color.black, 0.35f));

            if (padRenderer != null)
            {
                padRenderer.GetPropertyBlock(padBlock);
                padBlock.SetColor("_BaseColor", definition.Accent);
                padBlock.SetColor("_EmissionColor", definition.Accent * 0.85f);
                padRenderer.SetPropertyBlock(padBlock);
            }

            if (frameBars != null)
            {
                var block = new MaterialPropertyBlock();
                for (int i = 0; i < frameBars.Length; i++)
                {
                    frameBars[i].GetPropertyBlock(block);
                    block.SetColor("_BaseColor", definition.Accent);
                    block.SetColor("_EmissionColor", definition.Accent * 1.1f);
                    frameBars[i].SetPropertyBlock(block);
                }
            }

            if (dust != null)
            {
                var main = dust.main;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    HushPalette.WithAlpha(definition.Accent, 0.35f),
                    new Color(1f, 0.8f, 0.4f, 0.25f));
            }
        }

        public void SetFocus(float towerTopY, float normalized)
        {
            CameraRig.SetFocus(towerTopY + 1.45f, normalized);
        }

        public void ResetCamera(float towerTopY, float framing)
        {
            CameraRig.Snap(towerTopY + 1.45f, framing, 0f);
        }

        public void Pulse(float beat, bool strong)
        {
            pulse = Mathf.Max(pulse, beat);
            float boost = beat * 12f + (strong ? 4f : 0f);
            if (rimLight != null) rimLight.intensity = 20f + boost;
            if (cyanLight != null) cyanLight.intensity = 12f + boost * 0.6f;
        }

        public void Burst(Vector3 position, Color color, int count, float power)
        {
            HushDebris.Burst(DebrisRoot, position, color, count, power);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 3f);
            if (rimLight != null)
            {
                float breathing = Mathf.Sin(Time.unscaledTime * 0.7f) * 1.2f;
                rimLight.intensity = 20f + pulse * 8f + breathing;
            }

            if (ShowcaseRoot != null && ShowcaseRoot.gameObject.activeSelf)
            {
                showcasePhase += dt * 0.18f;
                ShowcaseRoot.localRotation = Quaternion.Euler(0f, Mathf.Sin(showcasePhase) * 11f, 0f);
            }
        }
    }
}
