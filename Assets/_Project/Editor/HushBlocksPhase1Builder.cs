using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HushBlocks.Gameplay;

namespace HushBlocks.Editor
{
    /// <summary>Creates or refreshes the Phase 1 greybox scene without changing unrelated scenes.</summary>
    public static class HushBlocksPhase1Builder
    {
        private const string ScenePath = "Assets/_Project/Scenes/MainGameplay.unity";
        private static readonly Color Midnight = Hex("0B0E1A");
        private static readonly Color Charcoal = Hex("20232D");
        private static readonly Color Cyan = Hex("00D9FF");
        private static readonly Color Amber = Hex("FFB547");

        [MenuItem("Hush Blocks/Build Phase 1 Greybox")]
        public static void Build()
        {
            EnsureFolders();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MainGameplay";
            RenderSettings.ambientLight = Midnight;
            RenderSettings.ambientIntensity = 0.35f;

            GameObject cameraObject = new GameObject("Gameplay Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.8f;
            camera.transform.position = new Vector3(0f, 2.4f, -14f);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            camera.backgroundColor = Midnight;
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.tag = "MainCamera";

            GameObject keyLight = new GameObject("Midnight Key Light");
            Light light = keyLight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.42f, 0.5f, 0.75f);
            light.intensity = 0.65f;
            keyLight.transform.rotation = Quaternion.Euler(35f, -25f, 0f);

            GameObject ground = CreateCube("Static Terrace Ground", new Vector3(0f, -2.35f, 0f), new Vector3(12f, 0.5f, 2f), Charcoal);
            ground.isStatic = true;

            CreateBlock("Noisy Block Cyan", new Vector3(-2.7f, -1.1f, 0f), Cyan, ResonanceBlock.State.Noisy);
            CreateBlock("Stabilized Block Amber", new Vector3(2.7f, -1.1f, 0f), Amber, ResonanceBlock.State.Stabilized);
            CreateBlock("Supported Floor Amber", new Vector3(2.7f, 0.25f, 0f), Amber, ResonanceBlock.State.Stabilized);
            CreateBlock("Noisy Block Upper", new Vector3(0f, 0.25f, 0f), Cyan, ResonanceBlock.State.Noisy);
            CreateBlock("Noisy Block Right", new Vector3(4.05f, -1.1f, 0f), Cyan, ResonanceBlock.State.Noisy);

            GameObject controller = new GameObject("Game Controller");
            RhythmManager rhythmManager = controller.AddComponent<RhythmManager>();
            RhythmGameplayIntegration integration = controller.AddComponent<RhythmGameplayIntegration>();
            GreyboxGameController gameController = controller.AddComponent<GreyboxGameController>();
            TowerStabilitySystem towerStability = controller.AddComponent<TowerStabilitySystem>();
            LevelGameplayLoop levelLoop = controller.AddComponent<LevelGameplayLoop>();
            CreateNoaPlaceholder(new Vector3(-5.0f, -0.8f, -0.5f), Cyan, gameController);

            SerializedObject gameControllerSerialized = new SerializedObject(gameController);
            gameControllerSerialized.FindProperty("gameplayCamera").objectReferenceValue = camera;
            gameControllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject rhythmSerialized = new SerializedObject(rhythmManager);
            SerializedObject integrationSerialized = new SerializedObject(integration);

            string profilePath = "Assets/_Project/Settings/DefaultDifficultyProfile.asset";
            DifficultyProfile profile = AssetDatabase.LoadAssetAtPath<DifficultyProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<DifficultyProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            rhythmSerialized.FindProperty("difficultyProfile").objectReferenceValue = profile;
            integrationSerialized.FindProperty("feedbackPulseSeconds").floatValue = 0.35f;
            rhythmSerialized.ApplyModifiedPropertiesWithoutUndo();
            integrationSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = controller;
            Debug.Log($"Hush Blocks Phase 1 greybox built at {ScenePath}");
        }

        private static GameObject CreateNoaPlaceholder(Vector3 position, Color color, GreyboxGameController controller)
        {
            GameObject noa = CreateCube("Noa Placeholder", position, new Vector3(0.75f, 1.25f, 0.75f), color);
            UnityEngine.Object.DestroyImmediate(noa.GetComponent<BoxCollider>());
            NoaFeedbackCharacter feedback = noa.AddComponent<NoaFeedbackCharacter>();
            SerializedObject serialized = new SerializedObject(feedback);
            serialized.FindProperty("interactionSource").objectReferenceValue = controller;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return noa;
        }

        private static GameObject CreateBlock(string name, Vector3 position, Color color, ResonanceBlock.State state)
        {
            GameObject block = CreateCube(name, position, Vector3.one * 1.35f, color);
            ResonanceBlock resonance = block.AddComponent<ResonanceBlock>();
            SerializedObject serialized = new SerializedObject(resonance);
            serialized.FindProperty("initialState").enumValueIndex = (int)state;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return block;
        }

        private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = name + " Material",
                color = color
            };
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.45f);
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static void AddToBuildSettings(string path)
        {
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene scene in current)
                if (scene.path == path) return;

            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[current.Length + 1];
            current.CopyTo(updated, 0);
            updated[current.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = updated;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "_Project");
            EnsureFolder("Assets/_Project", "Scenes");
            EnsureFolder("Assets/_Project", "Settings");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }
    }
}
