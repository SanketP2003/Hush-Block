using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HushBlocks.FullGame
{
    /// <summary>
    /// Master Entry Point & Bootstrap for Hush Blocks FullGame layer.
    /// Spawns procedural audio, UI, diorama stage, and session manager in deterministic order,
    /// performs singleton validation, and executes the boot-to-title startup sequence.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Authored Blender Prefabs")]
        [SerializeField] private GameObject bedrockPrefab;
        [SerializeField] private GameObject brownstonePrefab;
        [SerializeField] private GameObject noaPrefab;
        [SerializeField] private GameObject howlerPrefab;
        [SerializeField] private GameObject sanctuaryPrefab;
        [SerializeField] private GameObject blockPrefab;
        [SerializeField] private GameObject debrisPrefab;

        private HushStage stage;
        private HushGameLoopManager session;

        public HushStage Stage => stage;
        public HushGameLoopManager Session => session;

        private void Awake()
        {
            Application.runInBackground = true;

            // Register authored Blender assets
            HushArtAssets.Initialize(
                bedrockPrefab,
                brownstonePrefab,
                noaPrefab,
                howlerPrefab,
                sanctuaryPrefab,
                blockPrefab,
                debrisPrefab);

            // a. HushAudio.Create()
            HushAudio.Create();

            // b. HushGame game = HushGame.EnsureInstance()
            HushGame game = HushGame.EnsureInstance();
            game.transform.SetParent(transform, false);

            // c. create a child "Hush Stage" with HushStage and call Build() once
            var stageObj = new GameObject("Hush Stage");
            stageObj.transform.SetParent(transform, false);
            stage = stageObj.GetComponent<HushStage>() ?? stageObj.AddComponent<HushStage>();
            stage.Build();

            // d. add HushGameLoopManager to the bootstrap object and call Initialize(stage, game, bootControlled: true)
            session = GetComponent<HushGameLoopManager>() ?? gameObject.AddComponent<HushGameLoopManager>();
            session.Initialize(stage, game, bootControlled: true);

            // e. validate one AudioListener, one EventSystem, one HushGame, one HushGameLoopManager, one HushStage
            ValidateSingletons();
        }

        private void ValidateSingletons()
        {
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            if (listeners.Length != 1)
            {
                Debug.LogError($"[GameBootstrap] Expected exactly 1 AudioListener, found {listeners.Length}.");
            }

            var eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystems.Length != 1)
            {
                Debug.LogError($"[GameBootstrap] Expected exactly 1 EventSystem, found {eventSystems.Length}.");
            }

            var games = FindObjectsByType<HushGame>(FindObjectsInactive.Include);
            if (games.Length != 1)
            {
                Debug.LogError($"[GameBootstrap] Expected exactly 1 HushGame, found {games.Length}.");
            }

            var sessions = FindObjectsByType<HushGameLoopManager>(FindObjectsInactive.Include);
            if (sessions.Length != 1)
            {
                Debug.LogError($"[GameBootstrap] Expected exactly 1 HushGameLoopManager, found {sessions.Length}.");
            }

            var stages = FindObjectsByType<HushStage>(FindObjectsInactive.Include);
            if (stages.Length != 1)
            {
                Debug.LogError($"[GameBootstrap] Expected exactly 1 HushStage, found {stages.Length}.");
            }
        }

        private IEnumerator Start()
        {
            HushGame game = HushGame.Instance;
            if (game == null)
            {
                Debug.LogError("[GameBootstrap] HushGame.Instance is null in Start.");
                yield break;
            }

            Debug.Log("[GameBootstrap] Start begun: Showing boot screen.");

            // a. game.ShowBootScreen()
            game.ShowBootScreen();

            // b. yield return HushAudio.Preload(p => game.Boot.SetProgress(p))
            yield return HushAudio.Preload(p =>
            {
                if (game.Boot != null)
                {
                    game.Boot.SetProgress(p);
                }
            });

            // c. wait 0.4 seconds realtime
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.4f)
            {
                yield return null;
            }

            // d. HushAudio.PlayMenu()
            HushAudio.PlayMenu();
            Debug.Log("[GameBootstrap] Menu audio played.");

            // e. game.ShowTitleScreen()
            game.ShowTitleScreen();
            Debug.Log("[GameBootstrap] Title screen shown.");

            // f. set the fade overlay to black, then fade it out over 0.35 seconds
            game.UI.SetFade(1f);
            float fadeDuration = 0.35f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                game.UI.SetFade(alpha);
                yield return null;
            }
            game.UI.SetFade(0f);
            Debug.Log("[GameBootstrap] Boot sequence complete.");
        }
    }
}