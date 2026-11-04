using System;
using System.Collections.Generic;
using UnityEngine;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Core stack lifecycle and foundation physics manager for Hush Blocks.
    /// Handles rhythmic block spawning, horizontal beat-synchronized oscillation,
    /// garbage-free precision slicing with anti-gravity debris, and aggregate Center of Mass (CoM)
    /// topple calculations causing cascading physical collapses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerManager : MonoBehaviour
    {
        [Header("Required Dependencies")]
        [Tooltip("Sample-accurate rhythm clock driving placement timing and oscillation.")]
        [SerializeField] private RhythmManager rhythmManager;
        [Tooltip("Prefab instantiated for each incoming layer block.")]
        [SerializeField] private HushBlock blockPrefab;
        [Tooltip("Root transform parenting all anchored tower blocks.")]
        [SerializeField] private Transform towerRoot;
        [Tooltip("Initial foundation base defining ground position and stability center.")]
        [SerializeField] private Transform foundationBase;

        [Header("Modular Grid & Oscillation")]
        [Tooltip("Exact vertical voxel step height per layer (meters). Standard: 1.0m.")]
        [SerializeField, Min(0.2f)] private float voxelStepSize = 1.0f;
        [Tooltip("Maximum horizontal sliding displacement from tower center.")]
        [SerializeField, Min(0.5f)] private float slideAmplitude = 3.2f;
        [Tooltip("Alternates sliding axis between X and Z for each stacked layer.")]
        [SerializeField] private bool alternateSlidingAxis = false;
        [Tooltip("Offset threshold where placement snaps perfectly without slicing (meters).")]
        [SerializeField, Min(0.01f)] private float perfectSnapTolerance = 0.08f;

        [Header("Anti-Gravity Debris (Garbage-Free Slicing)")]
        [Tooltip("Prefab for sliced overhang chunks that float upward into the night sky.")]
        [SerializeField] private GameObject debrisPrefab;
        [Tooltip("Pre-allocated debris pool size to prevent runtime heap allocations.")]
        [SerializeField, Range(8, 64)] private int debrisPoolSize = 24;
        [Tooltip("Upward anti-gravity drift speed for sheared charcoal rubble (m/s).")]
        [SerializeField, Min(0.5f)] private float debrisFloatSpeed = 3.8f;
        [Tooltip("Angular tumble torque applied to sheared debris chunks.")]
        [SerializeField, Min(0.5f)] private float debrisTumbleTorque = 4.0f;

        [Header("Stability & Topple Dynamics")]
        [Tooltip("Maximum allowed horizontal Center of Mass offset before the stack topples.")]
        [SerializeField, Min(0.1f)] private float toppleToleranceCoM = 0.65f;
        [Tooltip("Number of unmuted miss strikes before structural collapse triggers.")]
        [SerializeField, Range(1, 10)] private int maxConsecutiveMisses = 3;
        [Tooltip("Explosive torque applied to upper blocks during physical collapse.")]
        [SerializeField, Min(1f)] private float collapseTorqueMagnitude = 8.5f;

        // Stack State
        private readonly List<HushBlock> anchoredBlocks = new List<HushBlock>();
        private HushBlock activeSlidingBlock;
        private Transform previousTopTransform;
        private Vector3 currentLayerCenter;
        private Vector3 currentSlidingAxis = Vector3.right;
        private int currentLayerIndex;
        private int consecutiveMissCount;
        private bool isTowerLocked;
        private bool isCollapsed;

        // Pre-allocated Debris Object Pool
        private readonly Queue<GameObject> debrisPool = new Queue<GameObject>();
        private readonly List<DebrisInstance> activeDebris = new List<DebrisInstance>();

        private struct DebrisInstance
        {
            public GameObject GameObject;
            public Rigidbody Body;
            public float Lifetime;
        }

        // Public Properties
        public int CurrentHeight => currentLayerIndex;
        public HushBlock ActiveBlock => activeSlidingBlock;
        public IReadOnlyList<HushBlock> AnchoredBlocks => anchoredBlocks;
        public bool IsCollapsed => isCollapsed;
        public bool IsLocked => isTowerLocked;

        // Event Delegates
        public event Action<HushBlock, TimingResult> OnBlockAnchored;
        public event Action<float> OnOverhangSliced;
        public event Action<int> OnHeightChanged;
        public event Action OnTowerCollapsed;

        private void Awake()
        {
            if (rhythmManager == null) rhythmManager = UnityEngine.Object.FindAnyObjectByType<RhythmManager>();
            if (towerRoot == null) towerRoot = transform;

            InitializeDebrisPool();

            // Register foundation base if provided
            if (foundationBase != null)
            {
                HushBlock baseBlock = foundationBase.GetComponent<HushBlock>();
                if (baseBlock != null)
                {
                    anchoredBlocks.Add(baseBlock);
                }
                previousTopTransform = foundationBase;
                currentLayerCenter = foundationBase.position;
            }
            else
            {
                currentLayerCenter = towerRoot.position;
            }
        }

        private void Start()
        {
            SpawnNextSlidingBlock();
        }

        private void Update()
        {
            if (isCollapsed || isTowerLocked) return;

            UpdateSlidingBlockOscillation();
            UpdateActiveDebris();
        }

        // ---------- Spawning & Oscillation ----------

        /// <summary>
        /// Spawns the next rhythmic incoming block at layer height + 1.
        /// </summary>
        public void SpawnNextSlidingBlock()
        {
            if (isCollapsed || isTowerLocked) return;

            currentLayerIndex++;
            currentLayerCenter = new Vector3(
                previousTopTransform != null ? previousTopTransform.position.x : towerRoot.position.x,
                (foundationBase != null ? foundationBase.position.y : towerRoot.position.y) + (currentLayerIndex * voxelStepSize),
                previousTopTransform != null ? previousTopTransform.position.z : towerRoot.position.z
            );

            // Determine sliding axis
            if (alternateSlidingAxis)
            {
                currentSlidingAxis = (currentLayerIndex % 2 == 1) ? Vector3.right : Vector3.forward;
            }
            else
            {
                currentSlidingAxis = Vector3.right; // 2.5D side view
            }

            Vector3 spawnPos = currentLayerCenter - (currentSlidingAxis * slideAmplitude);
            Quaternion spawnRot = Quaternion.identity;

            if (blockPrefab != null)
            {
                activeSlidingBlock = Instantiate(blockPrefab, spawnPos, spawnRot, towerRoot);
            }
            else
            {
                // Fallback procedural voxel cube
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"HushBlock_Layer_{currentLayerIndex}";
                cube.transform.position = spawnPos;
                cube.transform.SetParent(towerRoot);
                activeSlidingBlock = cube.AddComponent<HushBlock>();
            }

            // Inherit previous top layer's cross-sectional dimensions
            if (previousTopTransform != null)
            {
                activeSlidingBlock.transform.localScale = previousTopTransform.localScale;
            }
            else
            {
                activeSlidingBlock.transform.localScale = Vector3.one * voxelStepSize;
            }

            OnHeightChanged?.Invoke(currentLayerIndex);
        }

        private void UpdateSlidingBlockOscillation()
        {
            if (activeSlidingBlock == null || rhythmManager == null) return;

            // Oscillate in sample-accurate sync with beat clock
            // Harmonic sine curve: 0 at exact beat, reaching max amplitudes on off-beats
            float time = (float)rhythmManager.TimeSinceStart;
            float interval = Mathf.Max(0.1f, rhythmManager.BeatInterval);
            float progress = Mathf.Sin((time / interval) * Mathf.PI);

            Vector3 offset = currentSlidingAxis * (progress * slideAmplitude);
            activeSlidingBlock.transform.position = currentLayerCenter + offset;
        }

        // ---------- Placement & Precision Slicing ----------

        /// <summary>
        /// Triggers rhythmic block placement. Evaluates overlap against the previous block,
        /// performs zero-allocation slicing, spawns anti-gravity debris, and checks center-of-mass toppling.
        /// </summary>
        public TimingResult TryPlaceCurrentBlock()
        {
            if (activeSlidingBlock == null || isCollapsed || isTowerLocked) return TimingResult.None;

            TimingResult result = rhythmManager != null 
                ? rhythmManager.EvaluateTap(out _) 
                : TimingResult.Good;

            if (result == TimingResult.Miss || result == TimingResult.Early || result == TimingResult.Late)
            {
                HandlePlacementMiss();
                return result;
            }

            // Reference underlying block dimensions
            Transform prevT = previousTopTransform != null ? previousTopTransform : foundationBase;
            Transform currT = activeSlidingBlock.transform;

            Vector3 prevPos = prevT != null ? prevT.position : currentLayerCenter;
            Vector3 prevScale = prevT != null ? prevT.localScale : Vector3.one * voxelStepSize;
            Vector3 currPos = currT.position;
            Vector3 currScale = currT.localScale;

            // Compute 1D projection along active sliding axis
            float prevCoord = Vector3.Dot(prevPos, currentSlidingAxis);
            float prevSize = Vector3.Dot(prevScale, currentSlidingAxis);
            float currCoord = Vector3.Dot(currPos, currentSlidingAxis);
            float currSize = Vector3.Dot(currScale, currentSlidingAxis);

            float delta = currCoord - prevCoord;
            float absDelta = Mathf.Abs(delta);

            // 1. Perfect Snap: within precision tolerance, snap fully without slicing
            if (absDelta <= perfectSnapTolerance || result == TimingResult.Perfect && absDelta <= perfectSnapTolerance * 1.5f)
            {
                Vector3 snappedPos = currPos;
                snappedPos.x = (currentSlidingAxis.x > 0.5f) ? prevPos.x : currPos.x;
                snappedPos.z = (currentSlidingAxis.z > 0.5f) ? prevPos.z : currPos.z;
                currT.position = snappedPos;

                AnchorBlock(activeSlidingBlock, TimingResult.Perfect);
                return TimingResult.Perfect;
            }

            // 2. Overlap Evaluation
            float prevMin = prevCoord - (prevSize * 0.5f);
            float prevMax = prevCoord + (prevSize * 0.5f);
            float currMin = currCoord - (currSize * 0.5f);
            float currMax = currCoord + (currSize * 0.5f);

            float overlapMin = Mathf.Max(prevMin, currMin);
            float overlapMax = Mathf.Min(prevMax, currMax);
            float overlapSize = overlapMax - overlapMin;

            if (overlapSize <= 0.02f)
            {
                // Completely missed the foundation
                HandlePlacementMiss();
                return TimingResult.Miss;
            }

            // 3. Garbage-Free Box Slicing
            float survivingCenterCoord = (overlapMin + overlapMax) * 0.5f;

            // Surviving chunk adjustments
            Vector3 newPosition = currPos;
            Vector3 newScale = currScale;

            if (currentSlidingAxis.x > 0.5f)
            {
                newPosition.x = survivingCenterCoord;
                newScale.x = overlapSize;
            }
            else if (currentSlidingAxis.z > 0.5f)
            {
                newPosition.z = survivingCenterCoord;
                newScale.z = overlapSize;
            }

            currT.position = newPosition;
            currT.localScale = newScale;

            // Overhang chunk calculation
            float overhangSize = absDelta;
            float overhangCenterCoord = (delta > 0f)
                ? overlapMax + (overhangSize * 0.5f)
                : overlapMin - (overhangSize * 0.5f);

            Vector3 debrisPos = currPos;
            Vector3 debrisScale = currScale;

            if (currentSlidingAxis.x > 0.5f)
            {
                debrisPos.x = overhangCenterCoord;
                debrisScale.x = overhangSize;
            }
            else if (currentSlidingAxis.z > 0.5f)
            {
                debrisPos.z = overhangCenterCoord;
                debrisScale.z = overhangSize;
            }

            SpawnAntiGravityDebris(debrisPos, debrisScale);
            OnOverhangSliced?.Invoke(overhangSize * voxelStepSize);

            // Reset consecutive misses on successful placement
            consecutiveMissCount = 0;
            AnchorBlock(activeSlidingBlock, result);
            return result;
        }

        private void AnchorBlock(HushBlock block, TimingResult result)
        {
            block.MuteBlock(isPerfect: result == TimingResult.Perfect);
            block.transform.SetParent(towerRoot);

            anchoredBlocks.Add(block);
            previousTopTransform = block.transform;

            OnBlockAnchored?.Invoke(block, result);

            // Center of Mass stability evaluation
            if (CheckTowerToppleThreshold())
            {
                TriggerCascadingCollapse();
            }
            else
            {
                SpawnNextSlidingBlock();
            }
        }

        private void HandlePlacementMiss()
        {
            consecutiveMissCount++;

            if (activeSlidingBlock != null)
            {
                activeSlidingBlock.TriggerAntiGravity();
                activeSlidingBlock = null;
            }

            if (consecutiveMissCount >= maxConsecutiveMisses || CheckTowerToppleThreshold())
            {
                TriggerCascadingCollapse();
            }
            else
            {
                SpawnNextSlidingBlock();
            }
        }

        // ---------- Center of Mass & Cascading Collapse ----------

        /// <summary>
        /// Calculates the aggregate Center of Mass (CoM) of all stacked blocks and checks topple threshold.
        /// </summary>
        public bool CheckTowerToppleThreshold()
        {
            if (anchoredBlocks.Count < 2) return false;

            Vector3 basePos = foundationBase != null ? foundationBase.position : towerRoot.position;
            Vector3 totalWeightedPosition = Vector3.zero;
            float totalMass = 0f;

            for (int i = 0; i < anchoredBlocks.Count; i++)
            {
                HushBlock b = anchoredBlocks[i];
                if (b == null) continue;

                Vector3 scale = b.transform.localScale;
                float volume = scale.x * scale.y * scale.z;
                float mass = Mathf.Max(0.1f, volume);

                totalWeightedPosition += b.transform.position * mass;
                totalMass += mass;
            }

            if (totalMass <= 0f) return false;

            Vector3 centerOfMass = totalWeightedPosition / totalMass;
            float horizontalOffset = Vector2.Distance(
                new Vector2(centerOfMass.x, centerOfMass.z),
                new Vector2(basePos.x, basePos.z)
            );

            return horizontalOffset > toppleToleranceCoM;
        }

        /// <summary>
        /// Switches stacked blocks to dynamic rigidbodies and applies angular torque,
        /// causing a cascading physical collapse.
        /// </summary>
        public void TriggerCascadingCollapse()
        {
            if (isCollapsed) return;
            isCollapsed = true;

            Vector3 basePos = foundationBase != null ? foundationBase.position : towerRoot.position;

            for (int i = 0; i < anchoredBlocks.Count; i++)
            {
                HushBlock block = anchoredBlocks[i];
                if (block == null) continue;

                Rigidbody rb = block.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.constraints = RigidbodyConstraints.None;

                    // Apply outward torque away from center
                    Vector3 directionFromCenter = (block.transform.position - basePos).normalized;
                    Vector3 torque = Vector3.Cross(directionFromCenter, Vector3.up) * collapseTorqueMagnitude;
                    torque += UnityEngine.Random.insideUnitSphere * (collapseTorqueMagnitude * 0.5f);

                    rb.AddTorque(torque, ForceMode.Impulse);
                    rb.AddForce(directionFromCenter * 2.5f, ForceMode.VelocityChange);
                }
            }

            if (activeSlidingBlock != null)
            {
                activeSlidingBlock.TriggerAntiGravity();
            }

            OnTowerCollapsed?.Invoke();
        }

        /// <summary>
        /// Permanently anchors the tower upon reaching District Sanctuary clearance.
        /// </summary>
        public void LockTowerPermanently()
        {
            isTowerLocked = true;
            if (activeSlidingBlock != null)
            {
                Destroy(activeSlidingBlock.gameObject);
                activeSlidingBlock = null;
            }
        }

        // ---------- Anti-Gravity Debris Pooling ----------

        private void InitializeDebrisPool()
        {
            debrisPool.Clear();
            for (int i = 0; i < debrisPoolSize; i++)
            {
                GameObject obj;
                if (debrisPrefab != null)
                {
                    obj = Instantiate(debrisPrefab, towerRoot);
                }
                else
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.name = "Charcoal_Debris_Pooled";
                    obj.transform.SetParent(towerRoot);
                }

                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb == null) rb = obj.AddComponent<Rigidbody>();
                rb.useGravity = false;

                obj.SetActive(false);
                debrisPool.Enqueue(obj);
            }
        }

        private void SpawnAntiGravityDebris(Vector3 worldPos, Vector3 scale)
        {
            if (debrisPool.Count == 0) return;

            GameObject debris = debrisPool.Dequeue();
            debris.transform.position = worldPos;
            debris.transform.localScale = scale;
            debris.SetActive(true);

            Rigidbody rb = debris.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.up * debrisFloatSpeed;
                rb.angularVelocity = UnityEngine.Random.insideUnitSphere * debrisTumbleTorque;
            }

            activeDebris.Add(new DebrisInstance
            {
                GameObject = debris,
                Body = rb,
                Lifetime = 3.5f
            });
        }

        private void UpdateActiveDebris()
        {
            for (int i = activeDebris.Count - 1; i >= 0; i--)
            {
                DebrisInstance item = activeDebris[i];
                item.Lifetime -= Time.deltaTime;

                if (item.Lifetime <= 0f)
                {
                    item.GameObject.SetActive(false);
                    debrisPool.Enqueue(item.GameObject);
                    activeDebris.RemoveAt(i);
                }
                else
                {
                    activeDebris[i] = item;
                }
            }
        }
    }
}

