using UnityEngine;
using UnityEngine.Rendering;

namespace HushBlocks.FullGame
{
    /// <summary>Small non-physics chips and cut-off block pieces.</summary>
    public static class HushDebris
    {
        public static void Burst(Transform parent, Vector3 position, Color color, int count, float power, float size = 0.1f)
        {
            HushArtAssets.EnsureLoaded();
            for (int i = 0; i < count; i++)
            {
                GameObject piece;
                if (HushArtAssets.DebrisPrefab != null)
                {
                    piece = Object.Instantiate(HushArtAssets.DebrisPrefab, parent);
                    piece.name = "Resonance Chip";
                    var col = piece.GetComponent<Collider>();
                    if (col != null) Object.Destroy(col);
                    var rb = piece.GetComponent<Rigidbody>();
                    if (rb != null) Object.Destroy(rb);
                }
                else
                {
                    piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    piece.name = "Resonance Chip";
                    Object.Destroy(piece.GetComponent<Collider>());
                    piece.transform.SetParent(parent, false);
                }

                float scale = size * UnityEngine.Random.Range(0.45f, 1.35f);
                piece.transform.localScale = new Vector3(scale, scale * UnityEngine.Random.Range(0.6f, 1.4f), scale);
                piece.transform.position = position + UnityEngine.Random.insideUnitSphere * 0.18f;
                piece.transform.rotation = UnityEngine.Random.rotation;

                var renderer = piece.GetComponentInChildren<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", color);
                    block.SetColor("_EmissionColor", color * 2.2f);
                    renderer.SetPropertyBlock(block);
                }

                var chip = piece.AddComponent<HushChip>();
                chip.Launch(
                    new Vector3(
                        UnityEngine.Random.Range(-1f, 1f) * power,
                        UnityEngine.Random.Range(0.35f, 1.15f) * power,
                        UnityEngine.Random.Range(-1f, 1f) * power),
                    UnityEngine.Random.insideUnitSphere * 420f,
                    UnityEngine.Random.Range(0.7f, 1.5f),
                    scale);
            }
        }

        public static void Chunk(Transform parent, Vector3 center, Vector3 size, bool attuned, Vector3 velocity)
        {
            HushArtAssets.EnsureLoaded();
            GameObject root;
            if (HushArtAssets.DebrisPrefab != null)
            {
                root = Object.Instantiate(HushArtAssets.DebrisPrefab, parent);
                root.name = "Severed Layer";
                var col = root.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                var rb = root.GetComponent<Rigidbody>();
                if (rb != null) Object.Destroy(rb);
                root.transform.position = center;
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = size;

                var renderer = root.GetComponentInChildren<MeshRenderer>();
                if (renderer != null)
                {
                    Color color = attuned ? HushPalette.Amber : HushPalette.Cyan;
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", color);
                    block.SetColor("_EmissionColor", color * 1.8f);
                    renderer.SetPropertyBlock(block);
                }
            }
            else
            {
                root = new GameObject("Severed Layer");
                root.transform.SetParent(parent, false);
                root.transform.position = center;
                root.transform.rotation = Quaternion.identity;

                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                Object.Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(root.transform, false);
                body.transform.localScale = size;
                var bodyRenderer = body.GetComponent<MeshRenderer>();
                bodyRenderer.sharedMaterial = HushMaterials.Body;
                bodyRenderer.shadowCastingMode = ShadowCastingMode.On;

                var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                band.name = "Band";
                Object.Destroy(band.GetComponent<Collider>());
                band.transform.SetParent(root.transform, false);
                band.transform.localScale = new Vector3(size.x * 1.01f, 0.035f, size.z * 1.01f);
                band.transform.localPosition = new Vector3(0f, size.y * 0.5f - 0.0175f, 0f);
                var bandRenderer = band.GetComponent<MeshRenderer>();
                bandRenderer.sharedMaterial = HushMaterials.Band;
                bandRenderer.shadowCastingMode = ShadowCastingMode.Off;

                Color color = attuned ? HushPalette.Amber : HushPalette.Cyan;
                var block = new MaterialPropertyBlock();
                bandRenderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_EmissionColor", color * 1.8f);
                bandRenderer.SetPropertyBlock(block);
            }

            var chip = root.AddComponent<HushChip>();
            chip.Launch(velocity, UnityEngine.Random.insideUnitSphere * 260f, 2.4f, size.x);
            chip.FadeChildren = true;
        }
    }

    /// <summary>Kinematic tumbling chip that shrinks out after its lifetime.</summary>
    public sealed class HushChip : MonoBehaviour
    {
        public bool FadeChildren;

        private Vector3 velocity;
        private Vector3 spin;
        private float life;
        private float maxLife;
        private float baseScale;
        private float groundY;
        private bool grounded;

        public void Launch(Vector3 initialVelocity, Vector3 angularVelocity, float lifetime, float scale)
        {
            velocity = initialVelocity;
            spin = angularVelocity;
            life = lifetime;
            maxLife = lifetime;
            baseScale = scale;
            groundY = transform.position.y;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            velocity.y -= 17f * dt;
            transform.position += velocity * dt;
            transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + spin * dt);

            // A soft bounce keeps debris on the stage without real physics.
            if (!grounded && transform.position.y < 0.1f)
            {
                transform.position = new Vector3(transform.position.x, 0.1f, transform.position.z);
                velocity.y = Mathf.Abs(velocity.y) * 0.24f;
                velocity.x *= 0.7f;
                velocity.z *= 0.7f;
                spin *= 0.6f;
                if (Mathf.Abs(velocity.y) < 0.35f) grounded = true;
            }

            float fade = Mathf.Clamp01(life / (maxLife * 0.55f));
            if (fade < 1f)
            {
                float scale = baseScale * Mathf.Lerp(0.05f, 1f, fade);
                transform.localScale = FadeChildren ? transform.localScale : new Vector3(scale, scale, scale);
                if (FadeChildren)
                {
                    foreach (Transform child in transform)
                    {
                        child.localScale = Vector3.Scale(child.localScale, Vector3.one * (0.985f));
                    }
                }
            }
        }
    }
}
