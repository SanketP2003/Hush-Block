using System.Collections.Generic;
using UnityEngine;

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Deterministic support graph for grid-aligned resonance blocks.
    /// Stabilized blocks support the cell directly above them; ground supports
    /// the first row. A missed beat destabilizes the lowest eligible noisy block,
    /// then dependent blocks are released in a controlled order.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RhythmManager))]
    public sealed class TowerStabilitySystem : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private float cellSize = 1.35f;
        [SerializeField] private float gridTolerance = 0.08f;
        [SerializeField] private float groundY = -1.6f;

        [Header("Collapse")]
        [SerializeField] private float supportCheckInterval = 0.05f;
        [SerializeField] private float releaseDelay = 0.08f;
        [SerializeField] private bool destabilizeOneBlockPerMiss = true;

        private readonly List<ResonanceBlock> blocks = new();
        private readonly Dictionary<ResonanceBlock, ResonanceBlock> supports = new();
        private readonly HashSet<ResonanceBlock> scheduledReleases = new();
        private RhythmManager rhythmManager;
        private float nextSupportCheck;
        private int releaseVersion;

        public IReadOnlyList<ResonanceBlock> Blocks => blocks;
        public float CellSize => cellSize;

        private void Awake()
        {
            rhythmManager = GetComponent<RhythmManager>();
        }

        private void OnEnable()
        {
            if (rhythmManager == null) rhythmManager = GetComponent<RhythmManager>();
            if (rhythmManager != null) rhythmManager.OnMissedBeat += HandleMissedBeat;
            RefreshBlocks();
            nextSupportCheck = 0f;
        }

        private void OnDisable()
        {
            if (rhythmManager != null) rhythmManager.OnMissedBeat -= HandleMissedBeat;
            UnsubscribeAll();
        }

        private void Update()
        {
            if (Time.time < nextSupportCheck) return;
            nextSupportCheck = Time.time + supportCheckInterval;
            RebuildSupportGraph();
        }

        public void RefreshBlocks()
        {
            UnsubscribeAll();
            scheduledReleases.Clear();
            blocks.Clear();
            blocks.AddRange(FindObjectsByType<ResonanceBlock>());
            foreach (ResonanceBlock block in blocks)
            {
                if (block == null) continue;
                block.OnStateChanged += HandleBlockStateChanged;
                block.OnFallen += HandleBlockFallen;
                EnsureControlledPhysics(block);
            }
            RebuildSupportGraph();
        }

        public bool IsSupported(ResonanceBlock block)
        {
            if (block == null || block.IsFalling) return false;
            if (IsGroundRow(block)) return true;
            return supports.TryGetValue(block, out ResonanceBlock support) && support != null && support.IsStabilized;
        }

        public ResonanceBlock GetSupport(ResonanceBlock block)
        {
            supports.TryGetValue(block, out ResonanceBlock support);
            return support;
        }

        public void RecoverAllToGrid()
        {
            foreach (ResonanceBlock block in blocks)
            {
                if (block == null) continue;
                Vector3 p = block.transform.position;
                p.x = Mathf.Round(p.x / cellSize) * cellSize;
                p.y = Mathf.Round(p.y / cellSize) * cellSize;
                block.RecoverToGrid(p);
            }
            RebuildSupportGraph();
        }

        private void HandleMissedBeat(long beatIndex)
        {
            ResonanceBlock target = FindMissTarget();
            if (target == null) return;
            target.RequestFail();
            if (!destabilizeOneBlockPerMiss)
            {
                ReleaseUnsupportedBlocks();
            }
        }

        private ResonanceBlock FindMissTarget()
        {
            ResonanceBlock result = null;
            foreach (ResonanceBlock block in blocks)
            {
                if (block == null || !block.CanAcceptInput()) continue;
                if (result == null || block.transform.position.y < result.transform.position.y ||
                    (Mathf.Approximately(block.transform.position.y, result.transform.position.y) && block.transform.position.x < result.transform.position.x))
                {
                    result = block;
                }
            }
            return result;
        }

        private void HandleBlockStateChanged(ResonanceBlock.State state)
        {
            if (state == ResonanceBlock.State.Falling || state == ResonanceBlock.State.Destabilizing)
            {
                ReleaseUnsupportedBlocks();
            }
            else
            {
                RebuildSupportGraph();
            }
        }

        private void HandleBlockFallen()
        {
            ReleaseUnsupportedBlocks();
        }

        private void ReleaseUnsupportedBlocks()
        {
            int version = ++releaseVersion;
            foreach (ResonanceBlock block in blocks)
            {
                if (block == null || block.IsFalling || !block.IsStabilized) continue;
                if (!IsSupported(block)) ScheduleRelease(block, version);
            }
        }

        private void ScheduleRelease(ResonanceBlock block, int version)
        {
            if (block == null || !scheduledReleases.Add(block)) return;
            block.RequestFallAfter(releaseDelay, () =>
            {
                scheduledReleases.Remove(block);
                return version == releaseVersion;
            });
        }

        private void RebuildSupportGraph()
        {
            supports.Clear();
            foreach (ResonanceBlock block in blocks)
            {
                if (block == null || block.IsFalling || !block.IsStabilized || IsGroundRow(block)) continue;
                ResonanceBlock support = FindDirectSupport(block);
                if (support != null) supports[block] = support;
            }
        }

        private ResonanceBlock FindDirectSupport(ResonanceBlock block)
        {
            Vector3 position = block.transform.position;
            ResonanceBlock best = null;
            float bestDistance = float.MaxValue;
            foreach (ResonanceBlock candidate in blocks)
            {
                if (candidate == null || candidate == block || candidate.IsFalling || !candidate.IsStabilized) continue;
                Vector3 candidatePosition = candidate.transform.position;
                float horizontal = Mathf.Abs(candidatePosition.x - position.x);
                float vertical = position.y - candidatePosition.y;
                if (horizontal > gridTolerance || Mathf.Abs(vertical - cellSize) > gridTolerance) continue;
                if (vertical < bestDistance) { best = candidate; bestDistance = vertical; }
            }
            return best;
        }

        private bool IsGroundRow(ResonanceBlock block)
        {
            return block != null && Mathf.Abs(block.transform.position.y - groundY) <= gridTolerance;
        }

        private static void EnsureControlledPhysics(ResonanceBlock block)
        {
            Rigidbody body = block.GetComponent<Rigidbody>();
            if (body == null) body = block.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = !block.IsFalling;
            body.useGravity = block.IsFalling;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.mass = 1f;
            body.linearDamping = 1.5f;
            body.angularDamping = 8f;
        }

        private void UnsubscribeAll()
        {
            foreach (ResonanceBlock block in blocks)
            {
                if (block == null) continue;
                block.OnStateChanged -= HandleBlockStateChanged;
                block.OnFallen -= HandleBlockFallen;
            }
        }
    }
}
