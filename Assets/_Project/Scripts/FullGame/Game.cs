using System;
using System.Collections;
using UnityEngine;

namespace HushBlocks.FullGame
{
    /// <summary>
    /// Compatibility facade used by the FullGame screens. Every call resolves
    /// the live session and reports a missing session instead of failing silently.
    /// </summary>
    public static class Game
    {
        private static bool TrySession(out HushGameLoopManager session)
        {
            session = HushGameLoopManager.Instance;
            if (session != null) return true;

            Debug.LogError("[Hush Blocks] No HushGameLoopManager is active. Game flow command ignored.");
            return false;
        }

        private static void WithSession(Action<HushGameLoopManager> action)
        {
            if (TrySession(out var session)) action(session);
        }

        // Campaign flow
        public static void Play() => WithSession(session => session.PlayFromTitle());
        public static void PlayNextDistrict() => Play();
        public static void StartDistrict(int index) => WithSession(session => session.StartRun(index));
        public static void AdvanceAfterResults() => WithSession(session => session.AdvanceAfterResults());
        public static void RetryDistrict() => WithSession(session => session.RetryDistrict());
        public static void RestartDistrict() => WithSession(session => session.RetryDistrict());
        public static void SkipDistrictIntro() => WithSession(session => session.SkipDistrictIntro());

        // Navigation
        public static void OpenTitle() => WithSession(session => session.ReturnToTitle());
        public static void OpenDistrictSelect() => WithSession(session => session.ReturnToDistrictSelect());
        public static void QuitToMenu() => WithSession(session => session.QuitToMenu());

        // Overlays
        public static void OpenHowTo(HushReturnTarget target) => WithSession(session => session.OpenHowTo(target));
        public static void OpenSettings(HushReturnTarget target) => WithSession(session => session.OpenSettings(target));
        public static void CloseHowTo() => WithSession(session => session.CloseHowTo());
        public static void CloseSettings() => WithSession(session => session.CloseSettings());

        // Pause and application
        public static void Pause() => WithSession(session => session.PauseGame());
        public static void Resume() => WithSession(session => session.ResumeGame());
        public static void QuitGame()
        {
            if (TrySession(out var session)) session.QuitGame();
        }

        // Screen helpers
        public static void AnimateScreenIn(HushScreen screen) => HushGame.EnsureInstance().AnimateScreenIn(screen);
        public static void StartCoroutine(IEnumerator routine) => HushGame.EnsureInstance().StartCoroutine(routine);

        // Live reads
        public static int CurrentDistrictIndex =>
            HushGameLoopManager.Instance != null ? HushGameLoopManager.Instance.CurrentDistrictIndex : 0;

        public static DistrictDefinition CurrentDistrict =>
            HushGameLoopManager.Instance != null ? HushGameLoopManager.Instance.CurrentDistrict : null;

        public static StackGame CurrentRun =>
            HushGame.Instance != null ? HushGame.Instance.CurrentRun : null;
    }
}
