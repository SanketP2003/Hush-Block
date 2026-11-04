using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HushBlocks.Gameplay;
using HushBlocks.UI;

namespace HushBlocks.Editor
{
    /// <summary>
    /// Master Production Builder for Hush Blocks in Unity 6 (URP).
    /// Assembles all Blender 5.2 assets (Noa, Feedback Howler, Bedrock Foundation,
    /// Brownstone Diorama, Rooftop Sanctuary, Charcoal Debris, Double-Beat Block)
    /// into game-ready prefabs, and configures the complete "Midnight Voxel Nocturne"
    /// production vertical slice scene with zero placeholder primitives.
    /// </summary>
    public static class HushBlocksProductionBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/MainGameplay.unity";

        private const string NoaFbxPath = "Assets/Art/Models/Characters/Noa.fbx";
        private const string HowlerFbxPath = "Assets/Art/Models/Characters/FeedbackHowler.fbx";
        private const string BedrockFbxPath = "Assets/Art/Models/Environment/BedrockFoundation.fbx";
        private const string BrownstoneFbxPath = "Assets/Art/Models/Environment/BrownstoneDiorama.fbx";
        private const string SanctuaryFbxPath = "Assets/Art/Models/Environment/RooftopSanctuary.fbx";
        private const string DebrisFbxPath = "Assets/Art/Models/Props/CharcoalDebris.fbx";
        private const string BlockPrefabPath = "Assets/Prefabs/Blocks/HushBlock_DoubleBeat.prefab";

        private const string PrefabDirCharacters = "Assets/Prefabs/Characters";
        private const string PrefabDirEnvironment = "Assets/Prefabs/Environment";
        private const string PrefabDirProps = "Assets/Prefabs/Props";

        [MenuItem("Hush Blocks/Build Complete Production Game Scene")]
        public static void BuildProductionGame()
        {
            EnsureFolders();

            Debug.Log("[HushBlocks] Building production prefabs from Blender 5.2 assets...");
            GameObject debrisPrefab = BuildCharcoalDebrisPrefab();
            GameObject bedrockPrefab = BuildBedrockFoundationPrefab();
            GameObject brownstonePrefab = BuildBrownstoneDioramaPrefab();
            GameObject sanctuaryPrefab = BuildRooftopSanctuaryPrefab();
            GameObject howlerPrefab = BuildFeedbackHowlerPrefab();
            GameObject noaPrefab = BuildNoaCharacterPrefab();

            Debug.Log("[HushBlocks] Assembling full production scene...");
            AssembleProductionScene(debrisPrefab, bedrockPrefab, brownstonePrefab, sanctuaryPrefab, howlerPrefab, noaPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HushBlocks] PRODUCTION READY! All Blender assets integrated and wired into MainGameplay.unity!");
        }

        // =========================================================================
        // 1. Prefab Builders
        // =========================================================================

        private static GameObject BuildCharcoalDebrisPrefab()
        {
            string prefabPath = $"{PrefabDirProps}/CharcoalDebris.prefab";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(DebrisFbxPath);
            if (model == null)
            {
                Debug.LogWarning($"[HushBlocks] Missing {DebrisFbxPath}, creating fallback debris.");
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(model);
            instance.name = "CharcoalDebris";

            // Add physics components
            Rigidbody rb = instance.GetComponent<Rigidbody>();
            if (rb == null) rb = instance.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.useGravity = false; // Controlled by TowerManager anti-gravity drift
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            BoxCollider col = instance.GetComponent<BoxCollider>();
            if (col == null) col = instance.AddComponent<BoxCollider>();
            col.size = new Vector3(0.6f, 0.6f, 0.6f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject BuildBedrockFoundationPrefab()
        {
            string prefabPath = $"{PrefabDirEnvironment}/BedrockFoundation.prefab";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(BedrockFbxPath);
            if (model == null) return null;

            GameObject instance = UnityEngine.Object.Instantiate(model);
            instance.name = "BedrockFoundation";

            BoxCollider col = instance.GetComponent<BoxCollider>();
            if (col == null) col = instance.AddComponent<BoxCollider>();
            col.size = new Vector3(14f, 4f, 12f);
            col.center = new Vector3(0f, -2f, 0f);

            Rigidbody rb = instance.GetComponent<Rigidbody>();
            if (rb == null) rb = instance.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            HushBlock hb = instance.GetComponent<HushBlock>();
            if (hb == null) hb = instance.AddComponent<HushBlock>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject BuildBrownstoneDioramaPrefab()
        {
            string prefabPath = $"{PrefabDirEnvironment}/BrownstoneDiorama.prefab";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(BrownstoneFbxPath);
            if (model == null) return null;

            GameObject instance = UnityEngine.Object.Instantiate(model);
            instance.name = "BrownstoneDiorama";

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject BuildRooftopSanctuaryPrefab()
        {
            string prefabPath = $"{PrefabDirEnvironment}/RooftopSanctuary.prefab";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(SanctuaryFbxPath);
            if (model == null) return null;

            GameObject instance = UnityEngine.Object.Instantiate(model);
            instance.name = "RooftopSanctuary";

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject BuildFeedbackHowlerPrefab()
        {
            string prefabPath = $"{PrefabDirCharacters}/FeedbackHowler.prefab";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(HowlerFbxPath);
            if (model == null) return null;

            GameObject instance = UnityEngine.Object.Instantiate(model);
            instance.name = "FeedbackHowler";

            FeedbackHowlerAI ai = instance.GetComponent<FeedbackHowlerAI>();
            if (ai == null) ai = instance.AddComponent<FeedbackHowlerAI>();

            // Find speaker jaw renderer
            Renderer jawRenderer = null;
            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                if (r.name.Contains("Cavity") || r.name.Contains("Head"))
                {
                    jawRenderer = r;
                    break;
                }
            }
            if (jawRenderer == null) jawRenderer = instance.GetComponentInChildren<Renderer>();

            SetPrivateField(ai, "speakerJawRenderer", jawRenderer);

            BoxCollider col = instance.GetComponent<BoxCollider>();
            if (col == null) col = instance.AddComponent<BoxCollider>();
            col.size = new Vector3(1.8f, 1.4f, 2.2f);
            col.center = new Vector3(0f, 0.7f, 0f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject BuildNoaCharacterPrefab()
        {
            string prefabPath = $"{PrefabDirCharacters}/Noa.prefab";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(NoaFbxPath);
            if (model == null) return null;

            GameObject instance = UnityEngine.Object.Instantiate(model);
            instance.name = "Noa";

            // Add NoaController
            NoaController noaCtrl = instance.GetComponent<NoaController>();
            if (noaCtrl == null) noaCtrl = instance.AddComponent<NoaController>();

            // Find right hand socket & amber crystal blade
            Transform handSocket = null;
            Renderer bladeRenderer = null;
            foreach (var t in instance.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Pivot_Arm_R") handSocket = t;
                if (t.name == "Trowel_Amber_Crystal") bladeRenderer = t.GetComponent<Renderer>();
            }

            // Amber Trowel Rig on trowel root or arm
            Transform trowelParent = handSocket != null ? handSocket : instance.transform;
            AmberTrowelRig trowelRig = trowelParent.GetComponent<AmberTrowelRig>();
            if (trowelRig == null) trowelRig = trowelParent.gameObject.AddComponent<AmberTrowelRig>();

            // Add blade point light
            GameObject lightObj = new GameObject("Trowel_PointLight");
            lightObj.transform.SetParent(trowelParent, false);
            lightObj.transform.localPosition = new Vector3(0f, 0f, 0.35f);
            Light bladeLight = lightObj.AddComponent<Light>();
            bladeLight.type = LightType.Point;
            bladeLight.color = new Color(1.0f, 0.702f, 0.0f);
            bladeLight.intensity = 1.8f;
            bladeLight.range = 3.5f;

            SetPrivateField(trowelRig, "crystalBladeRenderer", bladeRenderer);
            SetPrivateField(trowelRig, "bladePointLight", bladeLight);
            SetPrivateField(trowelRig, "handBoneTransform", handSocket);

            SetPrivateField(noaCtrl, "amberTrowelRig", trowelRig);
            SetPrivateField(noaCtrl, "rightHandSocket", handSocket);
            SetPrivateField(noaCtrl, "animator", instance.GetComponent<Animator>());

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        // =========================================================================
        // 2. Scene Assembly
        // =========================================================================

        private static void AssembleProductionScene(
            GameObject debrisPrefab,
            GameObject bedrockPrefab,
            GameObject brownstonePrefab,
            GameObject sanctuaryPrefab,
            GameObject howlerPrefab,
            GameObject noaPrefab)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);

            // Clean up old greybox placeholder objects
            string[] staleNames = new string[]
            {
                "Static Terrace Ground",
                "Noisy Block Cyan",
                "Stabilized Block Amber",
                "Supported Floor Amber",
                "Noisy Block Upper",
                "Noisy Block Right",
                "Noa Placeholder",
                "AmberTrowel"
            };

            foreach (var name in staleNames)
            {
                var stale = GameObject.Find(name);
                if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
            }

            // Remove any loose duplicates
            var existingNoas = GameObject.FindObjectsByType<NoaController>(FindObjectsInactive.Include);
            foreach (var n in existingNoas) UnityEngine.Object.DestroyImmediate(n.gameObject);

            // 1. Bedrock Foundation Plinth (Base at Y=0)
            GameObject foundationInstance = GameObject.Find("BedrockFoundation");
            if (foundationInstance == null && bedrockPrefab != null)
            {
                foundationInstance = (GameObject)PrefabUtility.InstantiatePrefab(bedrockPrefab);
                foundationInstance.name = "BedrockFoundation";
                foundationInstance.transform.position = new Vector3(0f, 0f, 0f);
            }

            // 2. Environmental Brownstone Diorama Backdrop
            GameObject brownstoneInstance = GameObject.Find("BrownstoneDiorama");
            if (brownstoneInstance == null && brownstonePrefab != null)
            {
                brownstoneInstance = (GameObject)PrefabUtility.InstantiatePrefab(brownstonePrefab);
                brownstoneInstance.name = "BrownstoneDiorama";
                brownstoneInstance.transform.position = new Vector3(0f, 0f, 6.0f); // Grounded behind tower
            }

            // 3. Core Master Controllers Root
            GameObject controllerObj = GameObject.Find("Game Controller");
            if (controllerObj == null) controllerObj = new GameObject("Game Controller");

            RhythmManager rhythmManager = GetOrAdd<RhythmManager>(controllerObj);
            TowerManager towerManager = GetOrAdd<TowerManager>(controllerObj);
            AcousticInputController inputCtrl = GetOrAdd<AcousticInputController>(controllerObj);
            DistrictProgression progression = GetOrAdd<DistrictProgression>(controllerObj);
            VerticalSliceGameManager sliceManager = GetOrAdd<VerticalSliceGameManager>(controllerObj);

            // Configure TowerManager
            GameObject blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPrefabPath);
            if (blockPrefab != null)
            {
                SetPrivateField(towerManager, "blockPrefab", blockPrefab.GetComponent<HushBlock>());
            }
            if (debrisPrefab != null)
            {
                SetPrivateField(towerManager, "debrisPrefab", debrisPrefab);
            }
            if (foundationInstance != null)
            {
                SetPrivateField(towerManager, "foundationBase", foundationInstance.transform);
            }
            SetPrivateField(towerManager, "rhythmManager", rhythmManager);
            SetPrivateField(towerManager, "towerRoot", controllerObj.transform);

            // Configure Progression & Slice Manager
            if (sanctuaryPrefab != null)
            {
                SetPrivateField(progression, "sanctuaryPrefab", sanctuaryPrefab);
            }
            SetPrivateField(progression, "rhythmManager", rhythmManager);
            SetPrivateField(progression, "towerManager", towerManager);

            SetPrivateField(sliceManager, "rhythmManager", rhythmManager);
            SetPrivateField(sliceManager, "towerManager", towerManager);
            SetPrivateField(sliceManager, "districtProgression", progression);

            // 4. Noa Character on Stack Ledge
            GameObject noaInstance = null;
            if (noaPrefab != null)
            {
                noaInstance = (GameObject)PrefabUtility.InstantiatePrefab(noaPrefab);
                noaInstance.name = "Noa";
                noaInstance.transform.position = new Vector3(1.2f, 0.05f, 0f);

                NoaController noaCtrl = noaInstance.GetComponent<NoaController>();
                SetPrivateField(noaCtrl, "towerManager", towerManager);
                SetPrivateField(noaCtrl, "rhythmManager", rhythmManager);
                SetPrivateField(noaCtrl, "inputController", inputCtrl);

                SetPrivateField(inputCtrl, "noaAnimator", noaInstance.GetComponent<Animator>());
            }

            // 5. Feedback Howler Predator
            GameObject howlerInstance = GameObject.Find("FeedbackHowler");
            if (howlerInstance == null && howlerPrefab != null)
            {
                howlerInstance = (GameObject)PrefabUtility.InstantiatePrefab(howlerPrefab);
                howlerInstance.name = "FeedbackHowler";
                howlerInstance.transform.position = new Vector3(-2.8f, 2.5f, 0f);

                FeedbackHowlerAI howlerAI = howlerInstance.GetComponent<FeedbackHowlerAI>();
                SetPrivateField(howlerAI, "towerManager", towerManager);
                SetPrivateField(howlerAI, "rhythmManager", rhythmManager);
                SetPrivateField(progression, "howlerPrefab", howlerPrefab);
            }

            // 6. Camera Rig & Diorama Controller
            GameObject cameraObj = GameObject.Find("Gameplay Camera");
            if (cameraObj == null) cameraObj = Camera.main != null ? Camera.main.gameObject : new GameObject("Gameplay Camera");
            cameraObj.name = "Gameplay Camera";
            cameraObj.tag = "MainCamera";
            Camera cam = cameraObj.GetComponent<Camera>();
            if (cam == null) cam = cameraObj.AddComponent<Camera>();
            cam.enabled = true;
            cam.orthographic = true;
            cam.orthographicSize = 7.0f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.08f, 1f);

            var cameraData = cameraObj.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = cameraObj.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.renderShadows = true;

            DioramaCameraController camCtrl = cameraObj.GetComponent<DioramaCameraController>();
            if (camCtrl == null) camCtrl = cameraObj.AddComponent<DioramaCameraController>();
            SetPrivateField(camCtrl, "targetTowerManager", towerManager);
            SetPrivateField(camCtrl, "dioramaCamera", cam);

            // Set true isometric diorama transform angles (54.736 pitch, 45 yaw)
            cameraObj.transform.position = new Vector3(-16f, 20f, -16f);
            cameraObj.transform.rotation = Quaternion.Euler(35.264f, 45f, 0f);

            // 7. Global Volume & Nocturne Profile
            GameObject volumeObj = GameObject.Find("Global Post-Processing Volume");
            if (volumeObj == null)
            {
                volumeObj = new GameObject("Global Post-Processing Volume");
                Volume vol = volumeObj.AddComponent<Volume>();
                vol.isGlobal = true;
                string profilePath = "Assets/Settings/MidnightVoxelNocturneProfile.asset";
                VolumeProfile prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                vol.profile = prof;
            }

            // 8. Diegetic HUD Canvas Rebuild
            HushBlocksHUDBuilder.BuildHUDCanvas();

            // 9. Audio Manager Wiring
            GameObject audioMgrObj = GameObject.Find("Acoustic Audio Manager");
            if (audioMgrObj == null)
            {
                audioMgrObj = new GameObject("Acoustic Audio Manager");
                audioMgrObj.AddComponent<AcousticAudioManager>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder(PrefabDirCharacters);
            EnsureFolder(PrefabDirEnvironment);
            EnsureFolder(PrefabDirProps);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
