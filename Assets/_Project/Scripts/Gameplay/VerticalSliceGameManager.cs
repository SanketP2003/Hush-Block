using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HushBlocks.Gameplay
{
    /// <summary>
    /// Master State Orchestrator and Vertical Slice Game Manager for Hush Blocks.
    /// Manages high-level game states (TitleScreen, DistrictIntro, ActiveStacking,
    /// HowlerEncounter, TowerCollapsed, DistrictCleared), orchestrates the Sanctuary Bloom
    /// victory sequence, and executes the Resonant Inversion defeat and retry loop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VerticalSliceGameManager : MonoBehaviour
    {
        public enum GameState
        {
            TitleScreen,
            DistrictIntro,
            ActiveStacking,
            HowlerEncounter,
            TowerCollapsed,
            DistrictCleared
        }

        [Header("State Machine")]
        [SerializeField] private GameState currentState = GameState.ActiveStacking;
        [Tooltip("Automatically begins stacking on start.")]
        [SerializeField] private bool autoStartStacking = true;

        [Header("Lighting Orchestration")]
        [Tooltip("Main directional key light in the scene.")]
        [SerializeField] private Light mainDirectionalLight;
        [Tooltip("Cold nocturnal indigo light color (#6B80BF) during active stacking.")]
        [SerializeField] private Color midnightIndigoLight = new Color(0.42f, 0.50f, 0.75f, 1f);
        [Tooltip("Warm dawn honey-amber light color (#FFE5A3) during sanctuary victory.")]
        [SerializeField] private Color dawnAmberLight = new Color(1.0f, 0.898f, 0.639f, 1f);
        [Tooltip("Duration in seconds for the victory dawn lighting transition.")]
        [SerializeField, Min(1f)] private float victoryTransitionDuration = 3.5f;

        [Header("Minimalist UI Cards")]
        [Tooltip("Canvas or root container for the victory card overlay.")]
        [SerializeField] private GameObject victoryCardCanvas;
        [Tooltip("Victory title label.")]
        [SerializeField] private TextMeshProUGUI victoryText;
        [Tooltip("Canvas or root container for the defeat retry overlay.")]
        [SerializeField] private GameObject defeatCardCanvas;
        [Tooltip("Defeat prompt text label.")]
        [SerializeField] private TextMeshProUGUI defeatText;

        [Header("System Dependencies")]
        [SerializeField] private TowerManager towerManager;
        [SerializeField] private RhythmManager rhythmManager;
        [SerializeField] private AcousticInputController inputController;
        [SerializeField] private DistrictProgression districtProgression;
        [SerializeField] private DioramaCameraController cameraController;

        // Public Properties
        public GameState CurrentState => currentState;
        public bool IsVictory => currentState == GameState.DistrictCleared;
        public bool IsDefeat => currentState == GameState.TowerCollapsed;

        // Events
        public event Action<GameState> OnGameStateChanged;

        private void Awake()
        {
            if (towerManager == null) towerManager = UnityEngine.Object.FindAnyObjectByType<TowerManager>();
            if (rhythmManager == null) rhythmManager = UnityEngine.Object.FindAnyObjectByType<RhythmManager>();
            if (inputController == null) inputController = UnityEngine.Object.FindAnyObjectByType<AcousticInputController>();
            if (districtProgression == null) districtProgression = UnityEngine.Object.FindAnyObjectByType<DistrictProgression>();
            if (cameraController == null) cameraController = UnityEngine.Object.FindAnyObjectByType<DioramaCameraController>();

            if (victoryCardCanvas != null) victoryCardCanvas.SetActive(false);
            if (defeatCardCanvas != null) defeatCardCanvas.SetActive(false);
        }

        private void OnEnable()
        {
            if (towerManager != null)
            {
                towerManager.OnTowerCollapsed += HandleTowerCollapsed;
            }

            if (districtProgression != null)
            {
                districtProgression.OnSanctuaryReached += HandleSanctuaryReached;
                districtProgression.OnHowlerAmbushTriggered += HandleHowlerAmbush;
            }
        }

        private void OnDisable()
        {
            if (towerManager != null)
            {
                towerManager.OnTowerCollapsed -= HandleTowerCollapsed;
            }

            if (districtProgression != null)
            {
                districtProgression.OnSanctuaryReached -= HandleSanctuaryReached;
                districtProgression.OnHowlerAmbushTriggered -= HandleHowlerAmbush;
            }
        }

        private void Start()
        {
            if (autoStartStacking)
            {
                TransitionToState(GameState.ActiveStacking);
            }
        }

        private void Update()
        {
            // Instant restart on defeat via 'R' key or screen tap
            if (currentState == GameState.TowerCollapsed)
            {
#if ENABLE_INPUT_SYSTEM
                bool restartPressed = (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
                                      (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                                      (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
                if (restartPressed)
                {
                    RestartDistrict();
                }
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(0))
                {
                    RestartDistrict();
                }
#endif
            }
        }

        // ---------- State Machine Transitions ----------

        public void TransitionToState(GameState nextState)
        {
            currentState = nextState;
            OnGameStateChanged?.Invoke(nextState);

            switch (nextState)
            {
                case GameState.ActiveStacking:
                    if (rhythmManager != null && !rhythmManager.IsPlaying) rhythmManager.Play();
                    break;

                case GameState.HowlerEncounter:
                    // Howler ambush triggered: heightened distortion
                    break;

                case GameState.DistrictCleared:
                    StartCoroutine(ExecuteSanctuaryBloomSequence());
                    break;

                case GameState.TowerCollapsed:
                    StartCoroutine(ExecuteResonantInversionDefeat());
                    break;
            }
        }

        // ---------- Victory Sequence (Sanctuary Bloom) ----------

        private void HandleSanctuaryReached()
        {
            TransitionToState(GameState.DistrictCleared);
        }

        private IEnumerator ExecuteSanctuaryBloomSequence()
        {
            // 1. Freeze active block spawning and lock input
            if (towerManager != null) towerManager.LockTowerPermanently();
            if (inputController != null) inputController.enabled = false;

            // 2. Smoothly transition lighting to warm dawn amber
            float timer = 0f;
            Color startLightColor = mainDirectionalLight != null ? mainDirectionalLight.color : midnightIndigoLight;
            Color startAmbient = RenderSettings.ambientLight;
            Color targetAmbient = new Color(0.12f, 0.10f, 0.08f, 1f);

            while (timer < victoryTransitionDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / victoryTransitionDuration);

                if (mainDirectionalLight != null)
                {
                    mainDirectionalLight.color = Color.Lerp(startLightColor, dawnAmberLight, t);
                    mainDirectionalLight.intensity = Mathf.Lerp(0.65f, 1.25f, t);
                }

                RenderSettings.ambientLight = Color.Lerp(startAmbient, targetAmbient, t);
                yield return null;
            }

            // 3. Display minimalist victory card
            if (victoryCardCanvas != null)
            {
                victoryCardCanvas.SetActive(true);
            }

            if (victoryText != null)
            {
                victoryText.text = "Decibel Row Quieted — Silence Restored to the Skyline.";
            }
        }

        // ---------- Defeat Sequence (The Resonant Inversion) ----------

        private void HandleTowerCollapsed()
        {
            TransitionToState(GameState.TowerCollapsed);
        }

        private void HandleHowlerAmbush(int height)
        {
            TransitionToState(GameState.HowlerEncounter);
        }

        private IEnumerator ExecuteResonantInversionDefeat()
        {
            // 1. Disable input controller
            if (inputController != null) inputController.enabled = false;

            yield return new WaitForSeconds(0.8f);

            // 2. Display defeat retry card
            if (defeatCardCanvas != null)
            {
                defeatCardCanvas.SetActive(true);
            }

            if (defeatText != null)
            {
                defeatText.text = "The Resonant Inversion Took the Tower.\n[Tap or Press 'R' to Re-tune Foundation]";
            }
        }

        /// <summary>
        /// Instantly reloads the district scene to retry the climb.
        /// </summary>
        public void RestartDistrict()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }
    }
}

