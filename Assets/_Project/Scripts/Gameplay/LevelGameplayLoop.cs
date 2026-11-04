using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Owns the short first-level objective without replacing the rhythm or
    /// tower systems. The level is complete when every tracked block is stable;
    /// it fails when a tracked block falls. A successful input increments the
    /// silence chain, while any missed beat resets it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RhythmManager))]
    [RequireComponent(typeof(TowerStabilitySystem))]
    public sealed class LevelGameplayLoop : MonoBehaviour
    {
        public enum LevelState { Playing, Succeeded, Failed }

        [Header("Objective")]
        [SerializeField] private string objectiveText = "Stabilize every resonance block.";
        [SerializeField] private int minimumStabilizedBlocks = 4;
        [SerializeField] private bool countInitialStabilizedBlocks = true;

        private readonly List<ResonanceBlock> blocks = new();
        private RhythmManager rhythmManager;
        private TowerStabilitySystem tower;
        private LevelState state;
        private int stabilizedCount;
        private int silenceChain;
        private string feedback;
        private bool subscribed;

        public LevelState State => state;
        public int SilenceChain => silenceChain;
        public int StabilizedCount => stabilizedCount;
        public int TotalBlocks => blocks.Count;
        public string ObjectiveText => objectiveText;
        public string Feedback => feedback;

        private void Awake()
        {
            rhythmManager = GetComponent<RhythmManager>();
            tower = GetComponent<TowerStabilitySystem>();
            state = LevelState.Playing;
            feedback = objectiveText;
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshObjectiveBlocks();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (state != LevelState.Playing) return;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) RestartLevel();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.R)) RestartLevel();
#endif
            if (tower != null && Time.frameCount % 3 == 0) CheckCompletion();
        }

        public void RefreshObjectiveBlocks()
        {
            UnsubscribeBlocks();
            blocks.Clear();
            if (tower != null) blocks.AddRange(tower.Blocks);
            if (blocks.Count == 0) blocks.AddRange(FindObjectsByType<ResonanceBlock>());

            foreach (ResonanceBlock block in blocks)
            {
                if (block == null) continue;
                block.OnStabilized += HandleBlockStabilized;
                block.OnFallen += HandleBlockFallen;
            }
            RecountStabilizedBlocks();
            CheckCompletion();
        }

        public void RestartLevel()
        {
            string scenePath = SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(scenePath)) return;
            SceneManager.LoadScene(scenePath);
        }

        private void Subscribe()
        {
            if (subscribed) return;
            if (rhythmManager == null) rhythmManager = GetComponent<RhythmManager>();
            if (rhythmManager != null)
            {
                rhythmManager.OnAccurateInput += HandleAccurateInput;
                rhythmManager.OnMissedBeat += HandleMissedBeat;
            }
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (rhythmManager != null)
            {
                rhythmManager.OnAccurateInput -= HandleAccurateInput;
                rhythmManager.OnMissedBeat -= HandleMissedBeat;
            }
            UnsubscribeBlocks();
            subscribed = false;
        }

        private void UnsubscribeBlocks()
        {
            foreach (ResonanceBlock block in blocks)
            {
                if (block == null) continue;
                block.OnStabilized -= HandleBlockStabilized;
                block.OnFallen -= HandleBlockFallen;
            }
        }

        private void HandleAccurateInput(long beatIndex)
        {
            if (state != LevelState.Playing) return;
            silenceChain++;
            feedback = $"Clean seal on beat {beatIndex}. Silence chain x{silenceChain}.";
        }

        private void HandleMissedBeat(long beatIndex)
        {
            if (state != LevelState.Playing) return;
            silenceChain = 0;
            feedback = $"Beat {beatIndex} missed. The tower is losing silence.";
        }

        private void HandleBlockStabilized()
        {
            if (state != LevelState.Playing) return;
            RecountStabilizedBlocks();
            feedback = $"Block stabilized. {stabilizedCount}/{Mathf.Max(minimumStabilizedBlocks, blocks.Count)} secure.";
            CheckCompletion();
        }

        private void HandleBlockFallen()
        {
            if (state != LevelState.Playing) return;
            state = LevelState.Failed;
            feedback = "The tower fell. Press R or tap Retry to try again.";
        }

        private void RecountStabilizedBlocks()
        {
            stabilizedCount = 0;
            foreach (ResonanceBlock block in blocks)
            {
                if (block != null && block.IsStabilized && countInitialStabilizedBlocks) stabilizedCount++;
            }
        }

        private void CheckCompletion()
        {
            if (state != LevelState.Playing || blocks.Count == 0) return;
            int required = Mathf.Min(Mathf.Max(1, minimumStabilizedBlocks), blocks.Count);
            bool allStable = true;
            foreach (ResonanceBlock block in blocks)
            {
                if (block == null || !block.IsStabilized)
                {
                    allStable = false;
                    break;
                }
            }
            if (allStable && stabilizedCount >= required)
            {
                state = LevelState.Succeeded;
                feedback = "Level complete — the skyline is quiet.";
            }
        }

        private void OnGUI()
        {
            GUIStyle heading = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            GUIStyle body = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            GUI.Label(new Rect(30, 270, 800, 30), objectiveText, heading);
            GUI.Label(new Rect(30, 305, 800, 26), $"Silence chain: x{silenceChain}  |  Stabilized: {stabilizedCount}/{blocks.Count}", body);
            GUI.Label(new Rect(30, 335, 800, 26), feedback, body);
            if (state == LevelState.Succeeded || state == LevelState.Failed)
            {
                GUI.Label(new Rect(30, 375, 800, 34), state == LevelState.Succeeded ? "LEVEL COMPLETE" : "TOWER LOST", heading);
                if (GUI.Button(new Rect(30, 420, 150, 36), "Retry (R)")) RestartLevel();
            }
        }
    }
}
