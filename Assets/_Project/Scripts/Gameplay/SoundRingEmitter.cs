using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Lightweight, zero-allocation procedural visual effects driver for acoustic sound rings.
    /// Manages an internal pool of concentric voxel ring meshes that expand outward on every
    /// quarter-note beat and instantly snap-collapse into stillness upon block muting.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoundRingEmitter : MonoBehaviour
    {
        [Header("Ring Mesh & Material")]
        [Tooltip("Shared material using Shader Graphs/SH_AcousticWaveRing.")]
        [SerializeField] private Material ringMaterial;
        [Tooltip("Optional custom mesh (quad or flat ring). Defaults to procedural Quad if null.")]
        [SerializeField] private Mesh ringMesh;
        [Tooltip("Number of concentric ring instances pre-allocated in the local pool.")]
        [SerializeField, Range(3, 8)] private int poolSize = 4;

        [Header("Wave Expansion Dynamics")]
        [Tooltip("Local scale at birth when spawned on beat.")]
        [SerializeField] private Vector3 initialScale = new Vector3(0.2f, 0.2f, 0.2f);
        [Tooltip("Maximum expanded local scale at end of acoustic wave lifetime.")]
        [SerializeField] private Vector3 maxScale = new Vector3(2.4f, 2.4f, 2.4f);
        [Tooltip("Duration in seconds for a wave to complete its expansion and dispersion.")]
        [SerializeField, Min(0.2f)] private float waveDuration = 1.0f;
        [Tooltip("Rapid collapse snap duration when sound is muted (seconds).")]
        [SerializeField, Range(0.05f, 0.2f)] private float collapseSnapDuration = 0.10f;

        [Header("System Dependencies")]
        [SerializeField] private RhythmManager rhythmManager;
        [SerializeField] private HushBlock parentHushBlock;

        // Cached Shader Property IDs (Zero GC allocations)
        private static readonly int ExpansionProgressId = Shader.PropertyToID("_ExpansionProgress");
        private static readonly int RingColorId = Shader.PropertyToID("_RingColor");

        // Internal Ring Data Structure
        private struct RingInstance
        {
            public GameObject GameObject;
            public Transform Transform;
            public MeshRenderer Renderer;
            public float Elapsed;
            public bool IsActive;
            public bool IsCollapsing;
            public Vector3 CollapseStartScale;
        }

        // Object Pool & State
        private RingInstance[] ringPool;
        private MaterialPropertyBlock propertyBlock;
        private Transform cachedTransform;
        private bool isMuted;

        // Events
        public event Action OnRingSpawned;
        public event Action OnRingsCollapsed;

        private void Awake()
        {
            cachedTransform = transform;
            propertyBlock = new MaterialPropertyBlock();

            if (rhythmManager == null) rhythmManager = UnityEngine.Object.FindAnyObjectByType<RhythmManager>();
            if (parentHushBlock == null) parentHushBlock = GetComponentInParent<HushBlock>();

            InitializeRingPool();
        }

        private void OnEnable()
        {
            if (rhythmManager != null)
            {
                rhythmManager.OnBeat += HandleBeat;
            }

            if (parentHushBlock != null)
            {
                parentHushBlock.OnMuted += HandleBlockMuted;
                parentHushBlock.OnStateChanged += HandleBlockStateChanged;
            }
        }

        private void OnDisable()
        {
            if (rhythmManager != null)
            {
                rhythmManager.OnBeat -= HandleBeat;
            }

            if (parentHushBlock != null)
            {
                parentHushBlock.OnMuted -= HandleBlockMuted;
                parentHushBlock.OnStateChanged -= HandleBlockStateChanged;
            }
        }

        private void Update()
        {
            UpdateActiveRings();
        }

        // ---------- Pre-Allocated Ring Pool Initialization ----------

        private void InitializeRingPool()
        {
            ringPool = new RingInstance[poolSize];

            // Generate a default planar quad mesh if no custom mesh assigned
            if (ringMesh == null)
            {
                GameObject tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Quad);
                ringMesh = tempPrimitive.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tempPrimitive);
            }

            for (int i = 0; i < poolSize; i++)
            {
                GameObject ringObj = new GameObject($"AcousticWaveRing_{i}");
                ringObj.transform.SetParent(cachedTransform, false);
                ringObj.transform.localPosition = Vector3.zero;
                ringObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Lay flat on XZ plane
                ringObj.transform.localScale = initialScale;

                MeshFilter filter = ringObj.AddComponent<MeshFilter>();
                filter.sharedMesh = ringMesh;

                MeshRenderer renderer = ringObj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ringMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                ringObj.SetActive(false);

                ringPool[i] = new RingInstance
                {
                    GameObject = ringObj,
                    Transform = ringObj.transform,
                    Renderer = renderer,
                    Elapsed = 0f,
                    IsActive = false,
                    IsCollapsing = false,
                    CollapseStartScale = initialScale
                };
            }
        }

        // ---------- Beat-Synchronized Wave Spawning ----------

        private void HandleBeat(long beatIndex)
        {
            // Do not emit acoustic sound rings if block is stabilized / muted
            if (isMuted || (parentHushBlock != null && parentHushBlock.IsSettled)) return;

            SpawnRingWave();
        }

        /// <summary>
        /// Spawns the next available ring from the pool and initiates outward expansion.
        /// </summary>
        public void SpawnRingWave()
        {
            for (int i = 0; i < poolSize; i++)
            {
                if (!ringPool[i].IsActive)
                {
                    ringPool[i].IsActive = true;
                    ringPool[i].IsCollapsing = false;
                    ringPool[i].Elapsed = 0f;
                    ringPool[i].Transform.localScale = initialScale;
                    ringPool[i].GameObject.SetActive(true);

                    // Sync initial shader property
                    UpdateRingShaderProperty(ref ringPool[i], 0f);

                    OnRingSpawned?.Invoke();
                    return;
                }
            }

            // Pool exhaustion fallback: recycle oldest active ring
            int oldestIndex = 0;
            float maxElapsed = -1f;
            for (int i = 0; i < poolSize; i++)
            {
                if (ringPool[i].Elapsed > maxElapsed)
                {
                    maxElapsed = ringPool[i].Elapsed;
                    oldestIndex = i;
                }
            }

            ringPool[oldestIndex].IsActive = true;
            ringPool[oldestIndex].IsCollapsing = false;
            ringPool[oldestIndex].Elapsed = 0f;
            ringPool[oldestIndex].Transform.localScale = initialScale;
            ringPool[oldestIndex].GameObject.SetActive(true);
            UpdateRingShaderProperty(ref ringPool[oldestIndex], 0f);
        }

        // ---------- Wave Expansion & Dispersion Update Loop ----------

        private void UpdateActiveRings()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < poolSize; i++)
            {
                if (!ringPool[i].IsActive) continue;

                if (ringPool[i].IsCollapsing)
                {
                    // Rapid snap collapse toward center core (0.1s)
                    ringPool[i].Elapsed += dt;
                    float collapseT = Mathf.Clamp01(ringPool[i].Elapsed / collapseSnapDuration);

                    ringPool[i].Transform.localScale = Vector3.Lerp(
                        ringPool[i].CollapseStartScale,
                        initialScale * 0.1f,
                        collapseT
                    );

                    UpdateRingShaderProperty(ref ringPool[i], Mathf.Lerp(0.5f, 1.0f, collapseT));

                    if (collapseT >= 1.0f)
                    {
                        ringPool[i].IsActive = false;
                        ringPool[i].IsCollapsing = false;
                        ringPool[i].GameObject.SetActive(false);
                    }
                }
                else
                {
                    // Natural outward acoustic wave expansion
                    ringPool[i].Elapsed += dt;
                    float progress = Mathf.Clamp01(ringPool[i].Elapsed / waveDuration);

                    // Ease-Out Sine curve for natural acoustic deceleration
                    float easedScale = Mathf.Sin(progress * (Mathf.PI * 0.5f));
                    ringPool[i].Transform.localScale = Vector3.Lerp(initialScale, maxScale, easedScale);

                    // Non-linear alpha dispersion driven via MaterialPropertyBlock
                    UpdateRingShaderProperty(ref ringPool[i], progress);

                    if (progress >= 1.0f)
                    {
                        ringPool[i].IsActive = false;
                        ringPool[i].GameObject.SetActive(false);
                    }
                }
            }
        }

        private void UpdateRingShaderProperty(ref RingInstance ring, float progress)
        {
            if (ring.Renderer == null) return;

            ring.Renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ExpansionProgressId, progress);
            ring.Renderer.SetPropertyBlock(propertyBlock);
        }

        // ---------- Mute / Collapse Response ----------

        private void HandleBlockMuted()
        {
            CollapseRings();
        }

        private void HandleBlockStateChanged(HushBlock.BlockState state)
        {
            if (state == HushBlock.BlockState.AmberSettled || state == HushBlock.BlockState.MutingTransition)
            {
                CollapseRings();
            }
            else if (state == HushBlock.BlockState.UnmutedChaotic)
            {
                isMuted = false;
            }
        }

        /// <summary>
        /// Immediately halts outward expansion and reverses scale back toward block core
        /// in a rapid 0.1-second snap, communicating that sound has been completely dampened.
        /// </summary>
        public void CollapseRings()
        {
            isMuted = true;

            for (int i = 0; i < poolSize; i++)
            {
                if (ringPool[i].IsActive && !ringPool[i].IsCollapsing)
                {
                    ringPool[i].IsCollapsing = true;
                    ringPool[i].Elapsed = 0f;
                    ringPool[i].CollapseStartScale = ringPool[i].Transform.localScale;
                }
            }

            OnRingsCollapsed?.Invoke();
        }
    }
}

