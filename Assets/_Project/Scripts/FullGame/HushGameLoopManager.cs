using System;
using System.Collections;
using UnityEngine;

namespace HushBlocks.FullGame
{
    /// <summary>
    /// Owns the FullGame campaign flow: title, district intro, run, results,
    /// ending and the pause/settings overlays. Input, audio and UI are delegated;
    /// this class is the single authority for transitions and current state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HushGameLoopManager : MonoBehaviour
    {
        public enum GameState
        {
            TitleScreen,
            DistrictSelect,
            DistrictIntro,
            ActiveStacking,
            HazardEncounter,
            Results,
            Ending,
            Paused
        }

        private HushStage stage;
        private HushGame game;
        private StackGame stackGame;

        private bool initialized;
        private int currentDistrictIndex;
        private GameState currentState = GameState.TitleScreen;

        private int introToken;
        private float introAcceptAt;
        private bool introSkipped;

        private bool hasPendingRunResult;
        private RunResult pendingRunResult;
        private bool resultConsumed;
        private bool resultQueued;

        private bool settingsOpen;
        private HushReturnTarget settingsReturn = HushReturnTarget.Title;
        private bool howToOpen;
        private HushReturnTarget howToReturn = HushReturnTarget.Title;

        public static HushGameLoopManager Instance { get; private set; }

        public GameState CurrentState => currentState;
        public int CurrentDistrictIndex => currentDistrictIndex;
        public DistrictDefinition CurrentDistrict =>
            currentDistrictIndex >= 0 && currentDistrictIndex < DistrictCatalog.Count
                ? DistrictCatalog.Get(currentDistrictIndex)
                : null;
        public StackGame CurrentRun => stackGame;
        public bool IsInGameplay =>
            currentState == GameState.ActiveStacking || currentState == GameState.HazardEncounter;
        public bool IsPaused => currentState == GameState.Paused;
        public bool IsResolved => hasPendingRunResult && resultConsumed;

        public event Action<GameState> OnStateChanged;
        public event Action<int> OnDistrictChanged;
        public event Action OnVictory;
        public event Action OnDefeat;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            if (transform.parent == null && gameObject.name != "Game Bootstrap")
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            if (!initialized)
            {
                Debug.LogError("[HushGameLoopManager] Start ran without Initialize. Call Initialize from GameBootstrap.");
            }
        }

        private void OnDestroy()
        {
            if (stackGame != null)
            {
                stackGame.Finished -= HandleStackFinished;
            }

            if (Instance == this) Instance = null;
        }

        public void Initialize(HushStage hushStage, HushGame hushGame, bool bootControlled)
        {
            if (initialized) return;

            if (hushStage == null || hushGame == null)
            {
                Debug.LogError("[HushGameLoopManager] Initialize requires a stage and a HushGame instance.");
                return;
            }

            stage = hushStage;
            game = hushGame;
            currentDistrictIndex = 0;

            EnsureRunOwner();

            initialized = true;
            SetState(GameState.TitleScreen);

            if (!bootControlled)
            {
                ReturnToTitle();
            }
        }

        private void EnsureRunOwner()
        {
            if (stackGame != null) return;

            var runObject = new GameObject("Hush Stack Game");
            runObject.transform.SetParent(transform, false);
            stackGame = runObject.AddComponent<StackGame>();
            stackGame.Finished += HandleStackFinished;
        }

        private void Update()
        {
            if (!initialized || game == null) return;

            if (settingsOpen)
            {
                if (HushInput.BackDown()) CloseSettings();
                return;
            }

            if (howToOpen)
            {
                if (HushInput.BackDown()) CloseHowTo();
                return;
            }

            if (HushInput.BackDown())
            {
                HandleBack();
                return;
            }

            if (HushInput.PauseDown())
            {
                HandlePauseKey();
                return;
            }

            if (currentState == GameState.DistrictIntro &&
                Time.unscaledTime >= introAcceptAt &&
                (HushInput.ConfirmDown() || HushInput.TapDown()))
            {
                SkipDistrictIntro();
            }
        }

        // ------------------------------------------------------------------ campaign flow

        public bool PlayFromTitle()
        {
            if (!initialized)
            {
                LogNotReady(nameof(PlayFromTitle));
                return false;
            }

            if (currentState != GameState.TitleScreen) return false;

            var save = HushProgress.Data;
            bool fresh = save.runs == 0 && HushProgress.TotalStars == 0;
            int index = fresh ? 0 : HushProgress.NextDistrict;
            return StartRun(index);
        }

        public bool StartRun(int index)
        {
            if (!initialized)
            {
                LogNotReady(nameof(StartRun));
                return false;
            }

            if (currentState == GameState.DistrictIntro ||
                currentState == GameState.ActiveStacking ||
                currentState == GameState.HazardEncounter ||
                currentState == GameState.Ending)
            {
                Debug.LogWarning($"[Hush Blocks] StartRun({index}) rejected while {currentState}.");
                return false;
            }

            if (index < 0 || index >= DistrictCatalog.Count)
            {
                Debug.LogWarning($"[Hush Blocks] StartRun index {index} is outside the district catalog.");
                return false;
            }

            if (!HushProgress.IsUnlocked(index))
            {
                Debug.LogWarning($"[Hush Blocks] District {index} is still locked.");
                return false;
            }

            settingsOpen = false;
            howToOpen = false;
            Time.timeScale = 1f;

            EndRun();

            currentDistrictIndex = index;
            hasPendingRunResult = false;
            resultQueued = false;
            resultConsumed = false;

            introToken++;
            introSkipped = false;
            introAcceptAt = Time.unscaledTime + 0.2f;

            OnDistrictChanged?.Invoke(currentDistrictIndex);
            SetState(GameState.DistrictIntro);

            game.ShowDistrictIntro(DistrictCatalog.Get(currentDistrictIndex));
            StartCoroutine(IntroRoutine(currentDistrictIndex, introToken));
            return true;
        }

        private IEnumerator IntroRoutine(int index, int token)
        {
            float elapsed = 0f;
            const float duration = 2.5f;

            while (elapsed < duration)
            {
                if (token != introToken || currentState != GameState.DistrictIntro) yield break;
                if (introSkipped) break;

                if (Time.unscaledTime >= introAcceptAt &&
                    (HushInput.ConfirmDown() || HushInput.TapDown()))
                {
                    introSkipped = true;
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (token != introToken || currentState != GameState.DistrictIntro) yield break;
            BeginRun(index, token);
        }

        public void SkipDistrictIntro()
        {
            if (currentState == GameState.DistrictIntro) introSkipped = true;
        }

        private void BeginRun(int index, int token)
        {
            if (token != introToken || currentState != GameState.DistrictIntro) return;

            if (!HushProgress.IsUnlocked(index))
            {
                Debug.LogError($"[Hush Blocks] District {index} became locked before the run started.");
                ReturnToDistrictSelect();
                return;
            }

            EnsureRunOwner();
            currentDistrictIndex = index;
            SetState(GameState.ActiveStacking);

            stackGame.Begin(index, stage);
            game.SetCurrentRun(stackGame);
            game.ShowGameplayHUD();
        }

        public bool AdvanceAfterResults()
        {
            if (currentState != GameState.Results || !hasPendingRunResult || resultConsumed) return false;
            if (!pendingRunResult.Success) return false;

            resultConsumed = true;
            int next = pendingRunResult.DistrictIndex + 1;
            if (next >= DistrictCatalog.Count) return ShowEndingInternal();
            return StartRun(next);
        }

        public bool RetryDistrict()
        {
            if (!initialized) return false;

            int index = currentDistrictIndex;
            if (currentState == GameState.Results && hasPendingRunResult)
            {
                index = pendingRunResult.DistrictIndex;
            }
            else if (currentState != GameState.Paused &&
                     currentState != GameState.TitleScreen &&
                     currentState != GameState.DistrictSelect)
            {
                return false;
            }

            return StartRun(index);
        }

        private bool ShowEndingInternal()
        {
            if (currentState != GameState.Results || !hasPendingRunResult) return false;
            if (resultConsumed == false) resultConsumed = true;

            SetState(GameState.Ending);
            hasPendingRunResult = false;
            resultQueued = false;

            stackGame?.End();
            game.ClearCurrentRun();
            game.ShowEndingScreen();

            if (stage != null)
            {
                stage.SetShowcase(false);
                stage.ClearTower();
            }

            HushAudio.StopClicks();
            HushAudio.StopGameplay(0.8f);
            return true;
        }

        public void ReturnToDistrictSelect()
        {
            if (!initialized) return;

            bool fromGameplay = currentState == GameState.ActiveStacking ||
                                currentState == GameState.HazardEncounter ||
                                currentState == GameState.Paused ||
                                currentState == GameState.Results ||
                                currentState == GameState.Ending ||
                                currentState == GameState.DistrictIntro;

            CancelIntro();
            EndRun();
            Time.timeScale = 1f;
            hasPendingRunResult = false;
            resultQueued = false;
            resultConsumed = false;
            settingsOpen = false;
            howToOpen = false;

            SetState(GameState.DistrictSelect);

            if (stage != null)
            {
                stage.SetShowcase(false);
                stage.ClearTower();
            }

            game.ClearCurrentRun();
            HushAudio.StopClicks();
            if (fromGameplay) HushAudio.StopGameplay(0.55f);
            HushAudio.PlayMenu();
            game.ShowDistrictSelect();
        }

        public void ReturnToTitle()
        {
            if (!initialized) return;

            bool fromGameplay = currentState != GameState.TitleScreen;

            CancelIntro();
            EndRun();
            Time.timeScale = 1f;
            hasPendingRunResult = false;
            resultQueued = false;
            resultConsumed = false;
            settingsOpen = false;
            howToOpen = false;

            SetState(GameState.TitleScreen);

            if (stage != null)
            {
                stage.SetShowcase(true);
                stage.ClearTower();
            }

            game.ClearCurrentRun();
            HushAudio.StopClicks();
            if (fromGameplay) HushAudio.StopGameplay(0.6f);
            HushAudio.PlayMenu();
            game.ShowTitleScreen();
        }

        // ------------------------------------------------------------------ pause and overlays

        public bool PauseGame()
        {
            if (!initialized) return false;
            if (currentState != GameState.ActiveStacking && currentState != GameState.HazardEncounter) return false;
            if (stackGame == null || stackGame.IsResolving || stackGame.IsFinished) return false;

            SetState(GameState.Paused);
            Time.timeScale = 0f;
            stackGame.SetPaused(true);
            game.ShowPauseMenu();
            return true;
        }

        public bool ResumeGame()
        {
            if (!initialized || currentState != GameState.Paused) return false;
            if (settingsOpen || howToOpen) return false;

            Time.timeScale = 1f;
            stackGame?.SetPaused(false);
            SetState(GameState.ActiveStacking);
            game.ShowGameplayHUD();

            if (resultQueued) PresentPendingResult();
            return true;
        }

        public bool OpenSettings(HushReturnTarget target)
        {
            if (!initialized || settingsOpen || howToOpen) return false;
            if (currentState != GameState.TitleScreen && currentState != GameState.Paused) return false;

            settingsReturn = target == HushReturnTarget.Pause && currentState == GameState.Paused
                ? HushReturnTarget.Pause
                : HushReturnTarget.Title;
            settingsOpen = true;
            game.ShowSettings(settingsReturn);
            return true;
        }

        public void CloseSettings()
        {
            if (!settingsOpen) return;
            settingsOpen = false;

            if (settingsReturn == HushReturnTarget.Pause && currentState == GameState.Paused)
            {
                Time.timeScale = 0f;
                game.ShowPauseMenu();
                return;
            }

            SetState(GameState.TitleScreen);
            game.ShowTitleScreen();
        }

        public bool OpenHowTo(HushReturnTarget target)
        {
            if (!initialized || settingsOpen || howToOpen) return false;
            if (currentState != GameState.TitleScreen && currentState != GameState.Paused) return false;

            howToReturn = target == HushReturnTarget.Pause && currentState == GameState.Paused
                ? HushReturnTarget.Pause
                : HushReturnTarget.Title;
            howToOpen = true;
            game.ShowHowTo(howToReturn);
            return true;
        }

        public void CloseHowTo()
        {
            if (!howToOpen) return;
            howToOpen = false;

            if (howToReturn == HushReturnTarget.Pause && currentState == GameState.Paused)
            {
                Time.timeScale = 0f;
                game.ShowPauseMenu();
                return;
            }

            SetState(GameState.TitleScreen);
            game.ShowTitleScreen();
        }

        private void HandleBack()
        {
            switch (currentState)
            {
                case GameState.Results:
                    ReturnToDistrictSelect();
                    break;
                case GameState.Ending:
                    ReturnToTitle();
                    break;
                case GameState.Paused:
                    ResumeGame();
                    break;
                case GameState.ActiveStacking:
                case GameState.HazardEncounter:
                    PauseGame();
                    break;
                case GameState.DistrictSelect:
                    ReturnToTitle();
                    break;
                case GameState.TitleScreen:
                case GameState.DistrictIntro:
                default:
                    break;
            }
        }

        private void HandlePauseKey()
        {
            if (currentState == GameState.Paused)
            {
                ResumeGame();
            }
            else if (currentState == GameState.ActiveStacking || currentState == GameState.HazardEncounter)
            {
                PauseGame();
            }
        }

        private void HandleStackFinished(RunResult result)
        {
            if (!initialized) return;
            if (currentState != GameState.ActiveStacking &&
                currentState != GameState.HazardEncounter &&
                currentState != GameState.Paused)
            {
                return;
            }

            pendingRunResult = result;
            hasPendingRunResult = true;
            resultConsumed = false;

            if (currentState == GameState.Paused || (stackGame != null && stackGame.IsPaused))
            {
                resultQueued = true;
                return;
            }

            PresentPendingResult();
        }

        private void PresentPendingResult()
        {
            if (!hasPendingRunResult) return;

            resultQueued = false;
            Time.timeScale = 1f;
            if (stackGame != null && stackGame.IsPaused) stackGame.SetPaused(false);

            SetState(GameState.Results);
            HushAudio.StopClicks();
            HushAudio.DuckGameplay(0.35f);
            game.ShowResults(pendingRunResult);

            if (pendingRunResult.Success) OnVictory?.Invoke();
            else OnDefeat?.Invoke();
        }

        // ------------------------------------------------------------------ lifecycle

        public void QuitToMenu()
        {
            ReturnToTitle();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void EndRun()
        {
            stackGame?.End();
            game?.ClearCurrentRun();

            if (stage != null)
            {
                stage.ClearTower();
            }
        }

        private void CancelIntro()
        {
            introToken++;
            introSkipped = false;
        }

        private void SetState(GameState next)
        {
            if (currentState == next) return;
            currentState = next;
            OnStateChanged?.Invoke(next);
        }

        private void LogNotReady(string method)
        {
            Debug.LogError($"[HushGameLoopManager] {method} called before Initialize.");
        }
    }
}
