using UnityEngine;
using UnityEngine.Rendering;

namespace HushBlocks.FullGame
{
    /// <summary>
    /// A single tower layer. The body, edge bands and front node share one
    /// MaterialPropertyBlock each, so a full tower renders without allocating
    /// unique materials.
    /// </summary>
    public sealed class BlockView : MonoBehaviour
    {
        public float Width { get; private set; }
        public float Height { get; private set; }
        public float Depth { get; private set; }
        public float CenterX { get; private set; }
        public float TopY { get; private set; }
        public bool Attuned { get; private set; }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private Transform modelInstance;
        private Renderer modelRenderer;
        private MaterialPropertyBlock modelBlock;

        private Transform body;
        private Transform topBand;
        private Transform bottomBand;
        private Transform node;
        private Renderer bodyRenderer;
        private Renderer topRenderer;
        private Renderer bottomRenderer;
        private Renderer nodeRenderer;
        private MaterialPropertyBlock bodyBlock;
        private MaterialPropertyBlock topBlock;
        private MaterialPropertyBlock bottomBlock;
        private MaterialPropertyBlock nodeBlock;
        private BoxCollider boxCollider;
        private float blend;
        private float targetBlend;
        private float flash;
        private float seed;
        private Vector3 bodyBasePosition;
        private bool moving;

        private float tremorIntensity;
        private float tremorStartTime;
        private float tremorDelay;

        public static BlockView Create(
            Transform parent,
            float centerX,
            float bottomY,
            float width,
            float height,
            float depth,
            bool attuned,
            bool isMoving = false)
        {
            var root = new GameObject("Block " + width.ToString("0.00"));
            root.transform.SetParent(parent, false);

            var view = root.AddComponent<BlockView>();
            view.moving = isMoving;
            view.seed = UnityEngine.Random.value * 100f;
            view.Width = width;
            view.Height = height;
            view.Depth = depth;
            view.CenterX = centerX;
            view.TopY = bottomY + height;
            root.transform.position = new Vector3(centerX, bottomY + height * 0.5f, 0f);

            HushArtAssets.EnsureLoaded();
            if (HushArtAssets.BlockPrefab != null)
            {
                var inst = Object.Instantiate(HushArtAssets.BlockPrefab, root.transform);
                inst.name = "DoubleBeatBlock_Model";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = new Vector3(width / 10f, height / 6.34f, depth / 8f);

                // Strip nested gameplay components so BlockView handles stacking collision
                var childHb = inst.GetComponent<HushBlocks.Gameplay.HushBlock>();
                if (childHb != null) Object.Destroy(childHb);
                var childCol = inst.GetComponent<Collider>();
                if (childCol != null) Object.Destroy(childCol);
                var childRb = inst.GetComponent<Rigidbody>();
                if (childRb != null)
                {
                    childRb.isKinematic = true;
                    childRb.detectCollisions = false;
                    Object.Destroy(childRb);
                }

                view.modelInstance = inst.transform;
                view.modelRenderer = inst.GetComponentInChildren<MeshRenderer>();
                view.modelBlock = new MaterialPropertyBlock();
                view.bodyBasePosition = Vector3.zero;
            }
            else
            {
                view.body = CreatePart(root.transform, "Body", PrimitiveType.Cube, HushMaterials.Body, out view.bodyRenderer);
                view.body.localScale = new Vector3(width, height, depth * 0.96f);
                view.bodyBasePosition = view.body.localPosition;

                view.topBand = CreatePart(root.transform, "Top Band", PrimitiveType.Cube, HushMaterials.Band, out view.topRenderer);
                view.topBand.localScale = new Vector3(width * 1.012f, height * 0.055f, depth * 1.012f);
                view.topBand.localPosition = new Vector3(0f, height * 0.5f - height * 0.0275f, 0f);

                view.bottomBand = CreatePart(root.transform, "Bottom Band", PrimitiveType.Cube, HushMaterials.Band, out view.bottomRenderer);
                view.bottomBand.localScale = new Vector3(width * 1.008f, height * 0.026f, depth * 1.008f);
                view.bottomBand.localPosition = new Vector3(0f, -height * 0.5f + height * 0.013f, 0f);

                view.node = CreatePart(root.transform, "Resonance Node", PrimitiveType.Cube, HushMaterials.Band, out view.nodeRenderer);
                float nodeSize = Mathf.Min(0.38f, width * 0.24f);
                view.node.localScale = new Vector3(nodeSize, height * 0.24f, 0.02f);
                view.node.localPosition = new Vector3(0f, 0.015f, -depth * 0.48f - 0.011f);

                view.bodyBlock = new MaterialPropertyBlock();
                view.topBlock = new MaterialPropertyBlock();
                view.bottomBlock = new MaterialPropertyBlock();
                view.nodeBlock = new MaterialPropertyBlock();
            }

            view.boxCollider = root.AddComponent<BoxCollider>();
            view.boxCollider.size = new Vector3(width, height, depth);
            view.boxCollider.enabled = false;

            view.Attuned = attuned;
            view.blend = attuned ? 1f : 0f;
            view.targetBlend = view.blend;
            view.RefreshColors();
            return view;
        }

        private static Transform CreatePart(Transform parent, string name, PrimitiveType type, Material material, out Renderer renderer)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = Vector3.zero;
            part.transform.localRotation = Quaternion.identity;
            renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = name == "Body" ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = name == "Body";
            return part.transform;
        }

        public void Resize(float width, float centerX)
        {
            Width = Mathf.Max(0.1f, width);
            CenterX = centerX;
            transform.position = new Vector3(centerX, TopY - Height * 0.5f, 0f);

            if (modelInstance != null)
            {
                modelInstance.localScale = new Vector3(Width / 10f, Height / 6.34f, Depth / 8f);
            }
            if (body != null)
            {
                body.localScale = new Vector3(Width, Height, Depth * 0.96f);
            }
            if (topBand != null)
            {
                topBand.localScale = new Vector3(Width * 1.012f, Height * 0.055f, Depth * 1.012f);
            }
            if (bottomBand != null)
            {
                bottomBand.localScale = new Vector3(Width * 1.008f, Height * 0.026f, Depth * 1.008f);
            }
            if (node != null)
            {
                float nodeSize = Mathf.Min(0.38f, Width * 0.24f);
                node.localScale = new Vector3(nodeSize, Height * 0.24f, 0.02f);
            }

            if (boxCollider != null) boxCollider.size = new Vector3(Width, Height, Depth);
        }

        public void MoveTo(float centerX, float y)
        {
            CenterX = centerX;
            transform.position = new Vector3(centerX, y, 0f);
        }

        public void Attune(bool immediate = false)
        {
            Attuned = true;
            moving = false;
            targetBlend = 1f;
            flash = 1f;
            if (immediate)
            {
                blend = 1f;
                RefreshColors();
            }
            if (modelInstance != null) modelInstance.localPosition = bodyBasePosition;
            if (body != null) body.localPosition = bodyBasePosition;
        }

        public void ApplyTremor(float intensity, float delay = 0f)
        {
            tremorIntensity = intensity;
            tremorStartTime = Time.time;
            tremorDelay = delay;
        }

        public void SetMoving(bool value)
        {
            moving = value;
        }

        public void Pulse(float amount)
        {
            flash = Mathf.Max(flash, Mathf.Clamp01(amount));
        }

        public void Topple(Vector3 force)
        {
            if (boxCollider == null) return;
            boxCollider.enabled = true;
            var rigidbody = gameObject.AddComponent<Rigidbody>();
            rigidbody.mass = Mathf.Max(0.4f, Width * Height * Depth * 2.2f);
            rigidbody.linearDamping = 0.05f;
            rigidbody.angularDamping = 0.08f;
            rigidbody.AddForce(force, ForceMode.Impulse);
            rigidbody.AddTorque(UnityEngine.Random.insideUnitSphere * 4f, ForceMode.Impulse);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (blend != targetBlend)
            {
                blend = Mathf.MoveTowards(blend, targetBlend, dt * 4.2f);
                RefreshColors();
            }

            if (flash > 0.001f)
            {
                flash = Mathf.MoveTowards(flash, 0f, dt * 4.5f);
                RefreshColors();
            }

            // 1. Unmuted anti-gravity jitter on moving or unanchored blocks
            if (!Attuned)
            {
                float time = Time.time * 42f + seed;
                Vector3 jitter = new Vector3(
                    (Mathf.PerlinNoise(time, seed) - 0.5f) * 0.024f,
                    (Mathf.PerlinNoise(seed, time * 0.8f) - 0.5f) * 0.008f,
                    (Mathf.PerlinNoise(time * 0.9f, seed + 9f) - 0.5f) * 0.012f);

                if (modelInstance != null)
                {
                    modelInstance.localPosition = bodyBasePosition + jitter;
                }
                if (body != null)
                {
                    body.localPosition = bodyBasePosition + jitter;
                }
                if (topBand != null)
                {
                    topBand.localPosition = new Vector3(0f, Height * 0.5f - Height * 0.0275f, 0f) + jitter;
                }

                // Rapid frequency emission ripple on moving block
                if (moving)
                {
                    RefreshColors();
                }
            }
            else
            {
                // Attuned: snaps to weighted still equilibrium (zero jitter)
                if (modelInstance != null && modelInstance.localPosition != bodyBasePosition)
                {
                    modelInstance.localPosition = Vector3.Lerp(modelInstance.localPosition, bodyBasePosition, 1f - Mathf.Exp(-20f * dt));
                }
                if (body != null && body.localPosition != bodyBasePosition)
                {
                    body.localPosition = Vector3.Lerp(body.localPosition, bodyBasePosition, 1f - Mathf.Exp(-20f * dt));
                }
            }

            // 2. Structural tremor seismograph wave
            if (tremorIntensity > 0.001f && Time.time >= tremorStartTime + tremorDelay)
            {
                float elapsed = Time.time - (tremorStartTime + tremorDelay);
                if (elapsed < 0.75f)
                {
                    float wobble = Mathf.Sin(elapsed * 34f) * Mathf.Exp(-elapsed * 7.5f) * tremorIntensity * 0.035f;
                    transform.position = new Vector3(CenterX + wobble, TopY - Height * 0.5f, 0f);
                }
                else
                {
                    tremorIntensity = 0f;
                    transform.position = new Vector3(CenterX, TopY - Height * 0.5f, 0f);
                }
            }
        }

        private void RefreshColors()
        {
            Color cyan = HushPalette.Cyan;
            Color amber = HushPalette.Amber;
            float cyanRipple = (!Attuned && moving) ? (1f + Mathf.Sin(Time.time * 28f + seed) * 0.45f) : 1f;
            Color band = Color.Lerp(cyan * cyanRipple, amber, blend);
            float flashBoost = 1f + flash * 2.8f;

            if (modelRenderer != null)
            {
                modelRenderer.GetPropertyBlock(modelBlock);
                modelBlock.SetColor(BaseColor, Color.Lerp(HushPalette.Charcoal, HushPalette.Hex("#211F1A"), blend));
                modelBlock.SetColor(EmissionColor, band * (1.2f * flashBoost));
                modelRenderer.SetPropertyBlock(modelBlock);
            }

            if (bodyRenderer != null)
            {
                bodyBlock.SetColor(BaseColor, Color.Lerp(HushPalette.Charcoal, HushPalette.Hex("#211F1A"), blend));
                bodyBlock.SetColor(EmissionColor, Color.Lerp(cyan * 0.018f, amber * 0.026f, blend) * (1f + flash * 1.6f));
                bodyRenderer.SetPropertyBlock(bodyBlock);
            }

            if (topRenderer != null)
            {
                topBlock.SetColor(BaseColor, band);
                topBlock.SetColor(EmissionColor, band * (1.15f * flashBoost));
                topRenderer.SetPropertyBlock(topBlock);
            }

            if (bottomRenderer != null)
            {
                bottomBlock.SetColor(BaseColor, band);
                bottomBlock.SetColor(EmissionColor, band * 0.35f);
                bottomRenderer.SetPropertyBlock(bottomBlock);
            }

            if (nodeRenderer != null)
            {
                nodeBlock.SetColor(BaseColor, band);
                nodeBlock.SetColor(EmissionColor, band * (0.9f * flashBoost));
                nodeRenderer.SetPropertyBlock(nodeBlock);
            }
        }
    }
}
