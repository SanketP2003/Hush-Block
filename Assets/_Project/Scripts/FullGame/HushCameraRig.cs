using UnityEngine;

namespace HushBlocks.FullGame
{
    /// <summary>
    /// Diorama camera. Follows the top of the tower, drifts with a slow idle
    /// sway and applies a non-linear trauma shake for misses and collapses.
    /// </summary>
    public sealed class HushCameraRig : MonoBehaviour
    {
        private Camera targetCamera;
        private float focusY = 3.2f;
        private float targetFocusY = 3.2f;
        private float focusVelocity;
        private float framingX;
        private float targetFramingX;
        private float framingVelocity;
        private float distance = 22f;
        private float targetDistance = 22f;
        private float trauma;
        private float seed;

        public void Setup(Camera camera)
        {
            targetCamera = camera;
            seed = UnityEngine.Random.value * 120f;
            Snap(3.2f, 0f, 0f);
        }

        public void SetFocus(float worldY, float normalized)
        {
            targetFocusY = worldY;
            targetDistance = Mathf.Lerp(21.5f, 26.5f, Mathf.Clamp01(normalized));
        }

        public void Snap(float worldY, float framing, float normalized)
        {
            focusY = targetFocusY = worldY;
            framingX = targetFramingX = framing;
            distance = targetDistance = Mathf.Lerp(21.5f, 26.5f, Mathf.Clamp01(normalized));
            focusVelocity = 0f;
            framingVelocity = 0f;
            trauma = 0f;
        }

        public void SetFraming(float x, bool instant)
        {
            targetFramingX = x;
            if (instant)
            {
                framingX = x;
                framingVelocity = 0f;
            }
        }

        public void Shake(float amount)
        {
            trauma = Mathf.Clamp01(trauma + amount);
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;
            float dt = Time.unscaledDeltaTime;
            float motion = HushProgress.Data.reducedMotion ? 0.22f : 1f;

            focusY = Mathf.SmoothDamp(focusY, targetFocusY, ref focusVelocity, 0.55f, Mathf.Infinity, dt);
            framingX = Mathf.SmoothDamp(framingX, targetFramingX, ref framingVelocity, 0.65f, Mathf.Infinity, dt);
            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-3f * dt));

            float time = Time.unscaledTime;
            Vector3 focus = new Vector3(
                framingX + Mathf.Sin(time * 0.22f) * 0.055f * motion,
                focusY + Mathf.Sin(time * 0.31f) * 0.04f * motion,
                0f);

            Vector3 direction = new Vector3(0.30f, 0.34f, -1f).normalized;
            Vector3 position = focus + direction * distance;

            trauma = Mathf.Max(0f, trauma - dt * 1.55f);
            float shake = trauma * trauma * motion;
            if (!HushProgress.Data.screenShake) shake = 0f;
            if (shake > 0.0001f)
            {
                float t = time * 42f;
                position += new Vector3(
                    (Mathf.PerlinNoise(seed, t) - 0.5f) * shake * 0.85f,
                    (Mathf.PerlinNoise(seed + 11f, t) - 0.5f) * shake * 0.7f,
                    (Mathf.PerlinNoise(seed + 23f, t) - 0.5f) * shake * 0.35f);
            }

            transform.position = position;
            Quaternion rotation = Quaternion.LookRotation(focus - position, Vector3.up);
            if (shake > 0.0001f)
            {
                rotation *= Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(seed + 37f, time * 33f) - 0.5f) * shake * 3.2f);
            }
            transform.rotation = rotation;
            targetCamera.fieldOfView = 34f;
        }
    }
}
