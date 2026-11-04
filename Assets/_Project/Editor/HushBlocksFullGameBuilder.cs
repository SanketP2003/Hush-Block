using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HushBlocks.FullGame;

namespace HushBlocks.Editor
{
    /// <summary>
    /// Editor automation tool to construct and wire the production FullGame scene
    /// (Assets/_Project/Scenes/HushBlocksFullGame.unity) and configure build settings.
    /// </summary>
    public static class HushBlocksFullGameBuilder
    {
        public const string FullGameScenePath = "Assets/_Project/Scenes/HushBlocksFullGame.unity";
        public const string MainGameplayScenePath = "Assets/_Project/Scenes/MainGameplay.unity";

        [MenuItem("Hush Blocks/Build Full Game Scene", priority = 10)]
        public static void BuildFullGameScene()
        {
            // 1. Ensure target directory exists
            string sceneDir = Path.GetDirectoryName(FullGameScenePath);
            if (!Directory.Exists(sceneDir))
            {
                Directory.CreateDirectory(sceneDir);
            }

            // 2. Create a clean empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. Add single root GameObject with GameBootstrap
            var bootstrapObj = new GameObject("Game Bootstrap");
            var bootstrap = bootstrapObj.AddComponent<GameBootstrap>();

            // Wire authored Blender prefabs
            var so = new SerializedObject(bootstrap);
            so.FindProperty("bedrockPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/BedrockFoundation.prefab");
            so.FindProperty("brownstonePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/BrownstoneDiorama.prefab");
            so.FindProperty("noaPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Noa.prefab");
            so.FindProperty("howlerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/FeedbackHowler.prefab");
            so.FindProperty("sanctuaryPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/RooftopSanctuary.prefab");
            so.FindProperty("blockPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Blocks/HushBlock_DoubleBeat.prefab");
            so.FindProperty("debrisPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/CharcoalDebris.prefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            // 4. Save scene (overwrite if exists)
            bool saved = EditorSceneManager.SaveScene(scene, FullGameScenePath);
            if (!saved)
            {
                Debug.LogError($"[HushBlocksFullGameBuilder] Failed to save scene to {FullGameScenePath}");
                return;
            }

            Debug.Log($"[HushBlocksFullGameBuilder] Successfully created and saved {FullGameScenePath}");

            // 5. Update EditorBuildSettings
            UpdateBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void UpdateBuildSettings()
        {
            var existingScenes = EditorBuildSettings.scenes;
            var sceneList = new List<EditorBuildSettingsScene>();

            // Index 0: FullGame scene (enabled)
            sceneList.Add(new EditorBuildSettingsScene(FullGameScenePath, true));

            // Legacy MainGameplay scene (retained but disabled)
            bool mainGameplayAdded = false;
            foreach (var s in existingScenes)
            {
                if (string.Equals(s.path, FullGameScenePath, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Skip existing fullgame entry to avoid duplicate
                }

                if (string.Equals(s.path, MainGameplayScenePath, System.StringComparison.OrdinalIgnoreCase))
                {
                    sceneList.Add(new EditorBuildSettingsScene(MainGameplayScenePath, false));
                    mainGameplayAdded = true;
                }
                else
                {
                    sceneList.Add(s);
                }
            }

            if (!mainGameplayAdded && File.Exists(MainGameplayScenePath))
            {
                sceneList.Add(new EditorBuildSettingsScene(MainGameplayScenePath, false));
            }

            EditorBuildSettings.scenes = sceneList.ToArray();
            Debug.Log($"[HushBlocksFullGameBuilder] Updated EditorBuildSettings with {sceneList.Count} scenes (Index 0: {FullGameScenePath} [enabled], MainGameplay [disabled]).");
        }
    }
}

