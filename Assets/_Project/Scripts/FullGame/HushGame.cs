using UnityEngine;

namespace HushBlocks.FullGame
{
    public enum HushReturnTarget
    {
        Title,
        Pause
    }

    [DisallowMultipleComponent]
    public sealed class HushGame : MonoBehaviour
    {
        public static HushGame Instance { get; private set; }

        public HushUI UI { get; private set; }
        public BootScreen Boot { get; private set; }
        public TitleScreen Title { get; private set; }
        public DistrictSelectScreen DistrictSelect { get; private set; }
        public DistrictIntroScreen DistrictIntro { get; private set; }
        public HowToScreen HowTo { get; private set; }
        public SettingsScreen Settings { get; private set; }
        public HudScreen HUD { get; private set; }
        public PauseScreen Pause { get; private set; }
        public ResultsScreen Results { get; private set; }
        public EndingScreen Ending { get; private set; }
        public StackGame CurrentRun { get; private set; }

        public static HushGame EnsureInstance()
        {
            if (Instance != null) return Instance;

            Instance = FindAnyObjectByType<HushGame>();
            if (Instance != null) return Instance;

            var root = new GameObject("Hush Full Game UI");
            Instance = root.AddComponent<HushGame>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent == null && gameObject.name != "Game Bootstrap")
            {
                DontDestroyOnLoad(gameObject);
            }
            BuildScreens();
        }

        private void BuildScreens()
        {
            if (UI != null) return;

            UI = new HushUI();
            Boot = new BootScreen(this);
            Title = new TitleScreen(this);
            DistrictSelect = new DistrictSelectScreen(this);
            DistrictIntro = new DistrictIntroScreen(this);
            HowTo = new HowToScreen(this);
            Settings = new SettingsScreen(this);
            HUD = new HudScreen(this);
            Pause = new PauseScreen(this);
            Results = new ResultsScreen(this);
            Ending = new EndingScreen(this);

            Boot.Build(UI.SafeArea);
            Title.Build(UI.SafeArea);
            DistrictSelect.Build(UI.SafeArea);
            DistrictIntro.Build(UI.SafeArea);
            HowTo.Build(UI.SafeArea);
            Settings.Build(UI.SafeArea);
            HUD.Build(UI.SafeArea);
            Pause.Build(UI.SafeArea);
            Results.Build(UI.SafeArea);
            Ending.Build(UI.SafeArea);

            UI.SetFade(0f);
        }

        private void Update()
        {
            Boot?.Tick();
            Title?.Tick();
            DistrictSelect?.Tick();
            DistrictIntro?.Tick();
            HowTo?.Tick();
            Settings?.Tick();
            HUD?.Tick();
            Pause?.Tick();
            Results?.Tick();
            Ending?.Tick();
        }

        public void AnimateScreenIn(HushScreen screen)
        {
            if (screen == null || screen.Root == null) return;
            StartCoroutine(AnimateScreenInRoutine(screen));
        }

        private System.Collections.IEnumerator AnimateScreenInRoutine(HushScreen screen)
        {
            var group = screen.CanvasGroup;
            group.alpha = 0f;
            screen.Root.localScale = Vector3.one * 0.985f;
            float elapsed = 0f;
            while (elapsed < 0.22f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / 0.22f);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                group.alpha = eased;
                screen.Root.localScale = Vector3.one * Mathf.Lerp(0.985f, 1f, eased);
                yield return null;
            }
            group.alpha = 1f;
            screen.Root.localScale = Vector3.one;
        }

        private void HideAll()
        {
            Boot?.Hide();
            Title?.Hide();
            DistrictSelect?.Hide();
            DistrictIntro?.Hide();
            HowTo?.Hide();
            Settings?.Hide();
            HUD?.Hide();
            Pause?.Hide();
            Results?.Hide();
            Ending?.Hide();
        }

        private void Show(HushScreen screen, bool instant = false)
        {
            HideAll();
            screen?.Show(instant);
        }

        public void ShowBootScreen()
        {
            Show(Boot, instant: true);
        }

        public void ShowTitleScreen()
        {
            Show(Title);
        }

        public void ShowEndingScreen()
        {
            Show(Ending);
        }

        public void ShowDistrictSelect()
        {
            Show(DistrictSelect);
        }

        public void ShowDistrictIntro(DistrictDefinition district)
        {
            DistrictIntro.Present(district);
            Show(DistrictIntro);
        }

        public void ShowHowTo(HushReturnTarget target)
        {
            HowTo.ReturnTarget = target;
            Show(HowTo);
        }

        public void ShowSettings(HushReturnTarget target)
        {
            Settings.ReturnTarget = target;
            Show(Settings);
        }

        public void ShowGameplayHUD(StackGame run = null)
        {
            if (run != null) SetCurrentRun(run);
            HUD.Bind(CurrentRun);
            Show(HUD);
        }

        public void ShowHazardWarning()
        {
            ShowGameplayHUD(CurrentRun);
        }

        public void ShowPauseMenu()
        {
            Show(Pause);
        }

        public void ShowResults(RunResult result)
        {
            Results.Present(result);
            Show(Results);
        }

        public void UpdateHeightDisplay(int height)
        {
            if (HUD != null && CurrentRun != null && !HUD.IsVisible)
                HUD.Bind(CurrentRun);
        }

        public void UpdateSilenceChain(int chain)
        {
        }

        public void SetCurrentRun(StackGame run)
        {
            if (CurrentRun == run) return;
            HUD?.Unbind();
            CurrentRun = run;
            if (CurrentRun != null) HUD?.Bind(CurrentRun);
        }

        public void ClearCurrentRun()
        {
            HUD?.Unbind();
            CurrentRun = null;
        }

        // ------------------------------------------------------------------ flow facade

        public void Play() => Game.Play();
        public void OpenTitle() => Game.OpenTitle();
        public void OpenDistrictSelect() => Game.OpenDistrictSelect();
        public void OpenHowTo(HushReturnTarget target) => Game.OpenHowTo(target);
        public void OpenSettings(HushReturnTarget target) => Game.OpenSettings(target);
        public void CloseHowTo() => Game.CloseHowTo();
        public void CloseSettings() => Game.CloseSettings();
        public void PauseGame() => Game.Pause();
        public void ResumeGame() => Game.Resume();
        public void RestartDistrict() => Game.RestartDistrict();
        public void RetryDistrict() => Game.RetryDistrict();
        public void NextDistrict() => Game.AdvanceAfterResults();
        public void QuitToMenu() => Game.QuitToMenu();
        public void QuitGame() => Game.QuitGame();
        public void StartDistrict(int index) => Game.StartDistrict(index);
        public void AdvanceAfterResults() => Game.AdvanceAfterResults();
        public void SkipDistrictIntro() => Game.SkipDistrictIntro();
    }
}
