using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HushBlocks.Editor
{
    /// <summary>
    /// Applies the Hush Blocks model-import standard to every FBX under
    /// Assets/Art/Models/. The rules are deliberately narrow: unrelated
    /// models keep their own materials and animation settings.
    /// </summary>
    public sealed class HushBlocksAssetPostprocessor : AssetPostprocessor
    {
        private const string ModelsRoot = "Assets/Art/Models/";
        private const string MaterialsRoot = "Assets/Art/Materials/";
        private const uint ImporterVersion = 1;

        /// <summary>
        /// Blender material slot -> shared Unity material. Every voxel palette
        /// name resolves to one of the project standards so the imported
        /// mesh does not create duplicate embedded materials.
        /// </summary>
        private static readonly Dictionary<string, string> MaterialMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Core Voxel Standard
                { "Mat_Charcoal_Base", "M_Charcoal_Base" },
                { "Mat_Indigo_Base", "M_Charcoal_Base" },
                { "Mat_Indigo_Edge", "M_Charcoal_Base" },
                { "Mat_Deep_Recess", "M_Charcoal_Base" },
                { "Mat_Cyan_Active", "M_Cyan_Active" },
                { "Mat_Cyan_Unmuted", "M_Cyan_Active" },
                { "Mat_Amber_Muted", "M_Amber_Muted" },
                { "Mat_Amber_Accent", "M_Amber_Muted" },
                { "Mat_Copper_Patina", "M_Amber_Muted" },
                { "Mat_Copper_Weathered", "M_Amber_Muted" },

                // Noa Character
                { "Mat_Felt_Coat", "M_Charcoal_Base" },
                { "Mat_Charcoal_Trousers", "M_Charcoal_Base" },
                { "Mat_Dampening_Boots", "M_Charcoal_Base" },
                { "Mat_Amber_Crystal", "M_Amber_Muted" },
                { "Mat_Headset_Cyan", "M_Cyan_Active" },
                { "Mat_Hair", "M_Charcoal_Base" },
                { "Mat_Headset", "M_Charcoal_Base" },
                { "Mat_Coat_Trim", "M_Amber_Muted" },
                { "Mat_Skin", "M_Charcoal_Base" },
                { "Mat_Eyes", "M_Charcoal_Base" },
                { "Mat_Wood_Handle", "M_Amber_Muted" },
                { "Mat_Brass", "M_Amber_Muted" },
                { "Mat_Acoustic_Steel", "M_Charcoal_Base" },
                { "Mat_Acoustic_Collar", "M_Amber_Muted" },
                { "Mat_Leather_Belt", "M_Charcoal_Base" },
                { "Mat_Rubber_Sole", "M_Charcoal_Base" },
                { "Mat_Golden_Shockwave", "M_Amber_Muted" },

                // Feedback Howler
                { "Mat_Charcoal_Body", "M_Charcoal_Base" },
                { "Mat_Cyan_Mouth_Glow", "M_Cyan_Active" },
                { "Mat_Cyan_Spines_Glow", "M_Cyan_Active" },
                { "Mat_Howler_Plinth", "M_Charcoal_Base" },
                { "Mat_Sonic_Shriek_Cone", "M_Cyan_Active" },

                // Bedrock Foundation
                { "Stone_VelvetCharcoal", "M_Charcoal_Base" },
                { "Felt_AcousticCharcoal", "M_Charcoal_Base" },
                { "Iron_BlackenedRecess", "M_Charcoal_Base" },
                { "Iron_RoughCast", "M_Charcoal_Base" },
                { "Stone_CarvedEdge", "M_Charcoal_Base" },
                { "Stone_DeepRecess", "M_Charcoal_Base" },
                { "AmberFelt_Glow", "M_Amber_Muted" },
                { "HoneyAmberCore", "M_Amber_Muted" },
                { "HoneyAmberCrystal", "M_Amber_Muted" },
                { "Stone_MasonsRune", "M_Amber_Muted" },

                // Brownstone Diorama
                { "Velvety Charcoal Brick", "M_Charcoal_Base" },
                { "Brownstone Edge", "M_Charcoal_Base" },
                { "Fire Escape Charcoal", "M_Charcoal_Base" },
                { "Soft Interior Glow", "M_Amber_Muted" },
                { "Copper Antenna", "M_Amber_Muted" },
                { "AC Offwhite", "M_Charcoal_Base" },
                { "Matte Felt Acoustic Frame", "M_Charcoal_Base" },
                { "Interior Window Panes", "M_Cyan_Active" },
                { "Warm Metallic Trim", "M_Amber_Muted" },
                { "Diorama Ground", "M_Charcoal_Base" },

                // Rooftop Sanctuary
                { "Mat_Amber_Mortar", "M_Amber_Muted" },
                { "Mat_Amber_Paper", "M_Amber_Muted" },
                { "Mat_Paver_Dark", "M_Charcoal_Base" },
                { "Mat_Paver_Light", "M_Charcoal_Base" },
                { "Mat_Planter_Stone", "M_Charcoal_Base" },
                { "Mat_Bench_Iron", "M_Charcoal_Base" },
                { "Mat_Wood_Bench", "M_Amber_Muted" },
                { "Mat_Lantern_Cap", "M_Amber_Muted" },
                { "Mat_Coping_Stone", "M_Charcoal_Base" },
                { "Mat_Copper", "M_Amber_Muted" },
                { "Mat_Water", "M_Cyan_Active" },
                { "Mat_Leaf_Green", "M_Charcoal_Base" },
                { "Mat_Pastel_Butter", "M_Amber_Muted" },
                { "Mat_Pastel_Mint", "M_Cyan_Active" },
                { "Mat_Pastel_Rose", "M_Amber_Muted" },
                { "Mat_Pastel_Lav", "M_Charcoal_Base" },

                // Debris Chunks
                { "CharcoalBrick.005", "M_Charcoal_Base" },
                { "CharcoalBrick.006", "M_Charcoal_Base" },
                { "CharcoalBrick.007", "M_Charcoal_Base" },
                { "DustMaterial.001", "M_Charcoal_Base" },
                { "DustMaterial.002", "M_Charcoal_Base" },
                { "DustMaterial.003", "M_Charcoal_Base" },
                { "Material", "M_Charcoal_Base" }
            };

        private static readonly HashSet<string> MissingMaterialWarnings =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public override uint GetVersion()
        {
            return ImporterVersion;
        }

        // ------------------------------------------------------------------
        // Import rules
        // ------------------------------------------------------------------
        private void OnPreprocessModel()
        {
            if (!IsHushBlockModel(assetPath))
            {
                return;
            }

            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null)
            {
                return;
            }

            try
            {
                bool animated = ContainsAnimationStack(assetPath);

                // Model tab
                importer.globalScale = 1f;
                importer.useFileScale = true;                 // Convert Units
                importer.bakeAxisConversion = true;           // Rotation reads (0,0,0)
                importer.importNormals = ModelImporterNormals.Import;
                importer.normalCalculationMode = ModelImporterNormalCalculationMode.Unweighted;
                importer.importTangents = ModelImporterTangents.None;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.isReadable = false;
                importer.optimizeMeshPolygons = true;
                importer.optimizeMeshVertices = true;
                importer.weldVertices = false;                // Keep hard voxel edges
                importer.importBlendShapes = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.addCollider = false;

                // Animated blocks need their animation root. Static single-mesh
                // props can be flattened.
                importer.preserveHierarchy = animated;

                // Rig tab
                importer.importAnimation = animated;
                importer.animationType = animated
                    ? ModelImporterAnimationType.Generic
                    : ModelImporterAnimationType.None;

                // Animation tab
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.resampleCurves = false;

                // Materials tab
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

                if (animated)
                {
                    ConfigureLoopingClips(importer);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[HushBlocks] Failed to configure model importer for " +
                    assetPath + ": " + exception);
            }
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!IsHushBlockModel(assetPath) || root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            int triangleCount = 0;
            int materialSlotCount = 0;

            foreach (Renderer renderer in renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    triangleCount += filter.sharedMesh.triangles.Length / 3;
                }

                materialSlotCount += renderer.sharedMaterials.Length;
            }

            ModelImporter importer = assetImporter as ModelImporter;
            int clipCount = 0;
            int loopingClips = 0;

            if (importer != null)
            {
                ModelImporterClipAnimation[] clips =
                    importer.clipAnimations != null && importer.clipAnimations.Length > 0
                        ? importer.clipAnimations
                        : importer.defaultClipAnimations;

                if (clips != null)
                {
                    clipCount = clips.Length;
                    foreach (ModelImporterClipAnimation clip in clips)
                    {
                        if (clip != null &&
                            clip.name.IndexOf("Loop", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            loopingClips++;
                        }
                    }
                }
            }

            Debug.Log(string.Format(
                "[HushBlocks] {0}: {1} renderer(s), {2} triangle(s), {3} slot(s), " +
                "{4} clip(s), {5} loop(s), axis bake={6}.",
                Path.GetFileNameWithoutExtension(assetPath),
                renderers.Length,
                triangleCount,
                materialSlotCount,
                clipCount,
                loopingClips,
                importer != null && importer.bakeAxisConversion));

            // Clip configuration runs from OnPostprocessAllAssets, after the
            // import worker has published its takes.
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
            {
                if (!IsHushBlockModel(path))
                {
                    continue;
                }

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null ||
                    !importer.importAnimation ||
                    (importer.clipAnimations != null && importer.clipAnimations.Length > 0))
                {
                    continue;
                }

                string capturedPath = path;
                ScheduleOnce(delegate
                {
                    ConfigureClipsAfterImport(capturedPath);
                });
            }
        }

        private static void ScheduleOnce(Action action)
        {
            EditorApplication.CallbackFunction tick = null;
            tick = delegate
            {
                EditorApplication.update -= tick;
                action();
            };
            EditorApplication.update += tick;
        }

        /// <summary>
        /// Collapses Blender's duplicate object/NLA takes and enables Loop Time
        /// on clips whose name contains "Loop". Runs after the first import.
        /// </summary>
        private static void ConfigureClipsAfterImport(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null ||
                !importer.importAnimation ||
                (importer.clipAnimations != null && importer.clipAnimations.Length > 0))
            {
                return;
            }

            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            if (defaults == null || defaults.Length == 0)
            {
                return;
            }

            List<ModelImporterClipAnimation> selected = SelectUniqueClips(defaults);
            if (selected.Count == 0)
            {
                return;
            }

            foreach (ModelImporterClipAnimation clip in selected)
            {
                clip.loopTime = clip.name.IndexOf(
                    "Loop", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            importer.clipAnimations = selected.ToArray();
            importer.SaveAndReimport();
            Debug.Log("[HushBlocks] Configured " + selected.Count +
                      " animation clip(s) for " + path + ".");
        }

        private static List<ModelImporterClipAnimation> SelectUniqueClips(
            ModelImporterClipAnimation[] defaults)
        {
            List<string> order = new List<string>();
            Dictionary<string, ModelImporterClipAnimation> unique =
                new Dictionary<string, ModelImporterClipAnimation>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (ModelImporterClipAnimation clip in defaults)
            {
                if (clip == null)
                {
                    continue;
                }

                string baseName = clip.name;
                int separator = baseName.LastIndexOf('|');
                if (separator >= 0)
                {
                    baseName = baseName.Substring(separator + 1);
                }

                ModelImporterClipAnimation existing;
                if (!unique.TryGetValue(baseName, out existing))
                {
                    unique.Add(baseName, clip);
                    order.Add(baseName);
                    continue;
                }

                // Prefer the plain object take over the NLA-prefixed duplicate.
                bool existingIsPrefixed = existing.name.IndexOf('|') >= 0;
                bool candidateIsPlain = clip.name.IndexOf('|') < 0;
                if (existingIsPrefixed && candidateIsPlain)
                {
                    unique[baseName] = clip;
                }
            }

            List<ModelImporterClipAnimation> result =
                new List<ModelImporterClipAnimation>(order.Count);
            foreach (string name in order)
            {
                result.Add(unique[name]);
            }

            return result;
        }

        /// <summary>
        /// Returns the shared project material for a recognised Hush Blocks
        /// slot. Returning null lets Unity keep its normal behaviour for any
        /// material this pipeline does not own.
        /// </summary>
        public Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!IsHushBlockModel(assetPath) || material == null)
            {
                return null;
            }

            string targetName;
            if (!MaterialMap.TryGetValue(material.name, out targetName))
            {
                return null;
            }

            string targetPath = MaterialsRoot + targetName + ".mat";
            Material shared = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (shared == null)
            {
                if (MissingMaterialWarnings.Add(targetName))
                {
                    Debug.LogWarning(
                        "[HushBlocks] Shared material missing: " + targetPath +
                        ". The imported material will be kept for " + assetPath +
                        ". Run Hush Blocks/Create Art Materials to rebuild the palette.");
                }

                return null;
            }

            return shared;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private static bool IsHushBlockModel(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string normalized = path.Replace('\\', '/');
            return normalized.StartsWith(ModelsRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static void ConfigureLoopingClips(ModelImporter importer)
        {
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
            {
                return;
            }

            bool changed = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (clip == null ||
                    clip.name.IndexOf("Loop", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                clip.loopTime = true;
                changed = true;
            }

            if (changed)
            {
                importer.clipAnimations = clips;
            }
        }

        /// <summary>
        /// Binary and ASCII FBX files both store the "AnimStack" node name.
        /// Scanning the file avoids importing every model once to discover
        /// whether it has a rig.
        /// </summary>
        private static bool ContainsAnimationStack(string path)
        {
            const string token = "AnimStack";
            const int bufferSize = 64 * 1024;

            try
            {
                using (FileStream stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] buffer = new byte[bufferSize + token.Length - 1];
                    int carry = 0;

                    while (true)
                    {
                        int read = stream.Read(buffer, carry, bufferSize);
                        if (read <= 0)
                        {
                            return false;
                        }

                        int available = carry + read;
                        if (IndexOfAscii(buffer, available, token) >= 0)
                        {
                            return true;
                        }

                        carry = Math.Min(token.Length - 1, available);
                        Buffer.BlockCopy(buffer, available - carry, buffer, 0, carry);
                    }
                }
            }
            catch (Exception exception)
            {
                // Fail safe: keep hierarchy and animation enabled for files we
                // cannot inspect, rather than silently flattening a rig.
                Debug.LogWarning(
                    "[HushBlocks] Could not inspect " + path +
                    " for animation; assuming animated. " + exception.Message);
                return true;
            }
        }

        private static int IndexOfAscii(byte[] buffer, int length, string token)
        {
            int lastStart = length - token.Length;
            for (int i = 0; i <= lastStart; i++)
            {
                bool match = true;
                for (int j = 0; j < token.Length; j++)
                {
                    if (buffer[i + j] != (byte)token[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }

        // ------------------------------------------------------------------
        // Menu actions
        // ------------------------------------------------------------------
        [MenuItem("Hush Blocks/Create Art Materials")]
        public static void CreateArtMaterials()
        {
            Shader shader = Shader.Find("Shader Graphs/SH_HushBlock_Resonance");
            if (shader == null)
            {
                Debug.LogError(
                    "[HushBlocks] Shader Graphs/SH_HushBlock_Resonance is missing. " +
                    "Cannot create the shared art materials.");
                return;
            }

            EnsureFolder("Assets/Art");
            EnsureFolder(MaterialsRoot.TrimEnd('/'));

            Color charcoal = new Color(0.11f, 0.118f, 0.141f, 1f);
            Color mortar = new Color(0.055f, 0.063f, 0.082f, 1f);
            Color cyanHdr = new Color(0f, 10.646199f, 11.313708f, 11.313708f);
            Color amberHdr = new Color(4f, 2.808f, 0f, 4f);
            Color off = new Color(0f, 0f, 0f, 0f);

            CreateOrUpdateMaterial("M_Charcoal_Base", shader, charcoal, mortar,
                off, off, 0f, 0f, 0f);
            CreateOrUpdateMaterial("M_Cyan_Active", shader, charcoal, mortar,
                cyanHdr, amberHdr, 0f, 0.035f, 0f);
            CreateOrUpdateMaterial("M_Amber_Muted", shader, charcoal, mortar,
                cyanHdr, amberHdr, 1f, 0f, 1f);

            AssetDatabase.SaveAssets();
            Debug.Log("[HushBlocks] Shared art materials created or updated in " +
                      MaterialsRoot);
        }

        private static void CreateOrUpdateMaterial(
            string name,
            Shader shader,
            Color baseColor,
            Color mortarColor,
            Color cyanColor,
            Color amberColor,
            float stateBlend,
            float jitterIntensity,
            float crystallizationProgress)
        {
            string path = MaterialsRoot + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                material.name = name;
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_MortarColor", mortarColor);
            material.SetColor("_CyanColor", cyanColor);
            material.SetColor("_AmberColor", amberColor);
            material.SetFloat("_StateBlend", stateBlend);
            material.SetFloat("_JitterIntensity", jitterIntensity);
            material.SetFloat("_CrystallizationProgress", crystallizationProgress);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_JitterSpeed", 35f);
            material.SetFloat("_PulseSpeed", 24f);
            material.SetFloat("_CrystallizationScale", 20f);
            material.SetFloat("_FlareIntensity", 10f);
            material.SetFloat("_FlareWidth", 0.04f);
            material.SetFloat("_SeamWidth", 0.14f);
            material.SetFloat("_SlitWidth", 0.08f);
            material.SetFloat("_FresnelPower", 3.5f);
            EditorUtility.SetDirty(material);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            if (separator <= 0)
            {
                return;
            }

            string parent = path.Substring(0, separator);
            string leaf = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        [MenuItem("Hush Blocks/Reimport Art Models")]
        public static void ReimportArtModels()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ModelsRoot });
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsHushBlockModel(path))
                {
                    continue;
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[HushBlocks] Reimported " + count + " model(s) under " + ModelsRoot);
        }
    }
}
