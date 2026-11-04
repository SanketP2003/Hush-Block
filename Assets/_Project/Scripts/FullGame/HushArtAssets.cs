using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HushBlocks.FullGame
{
    /// <summary>
    /// Central registry and loader for authored Blender assets and prefabs in Hush Blocks.
    /// Eliminates runtime procedural primitive generation by providing direct access
    /// to authentic voxel models: BedrockFoundation, BrownstoneDiorama, Noa, FeedbackHowler,
    /// RooftopSanctuary, HushBlock_DoubleBeat, and CharcoalDebris.
    /// </summary>
    public static class HushArtAssets
    {
        public static GameObject BedrockPrefab { get; set; }
        public static GameObject BrownstonePrefab { get; set; }
        public static GameObject NoaPrefab { get; set; }
        public static GameObject HowlerPrefab { get; set; }
        public static GameObject SanctuaryPrefab { get; set; }
        public static GameObject BlockPrefab { get; set; }
        public static GameObject DebrisPrefab { get; set; }

        public static void Initialize(
            GameObject bedrock,
            GameObject brownstone,
            GameObject noa,
            GameObject howler,
            GameObject sanctuary,
            GameObject block,
            GameObject debris)
        {
            if (bedrock != null) BedrockPrefab = bedrock;
            if (brownstone != null) BrownstonePrefab = brownstone;
            if (noa != null) NoaPrefab = noa;
            if (howler != null) HowlerPrefab = howler;
            if (sanctuary != null) SanctuaryPrefab = sanctuary;
            if (block != null) BlockPrefab = block;
            if (debris != null) DebrisPrefab = debris;

            EnsureLoaded();
        }

        public static void EnsureLoaded()
        {
#if UNITY_EDITOR
            if (BedrockPrefab == null)
                BedrockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/BedrockFoundation.prefab");
            if (BrownstonePrefab == null)
                BrownstonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/BrownstoneDiorama.prefab");
            if (NoaPrefab == null)
                NoaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Noa.prefab");
            if (HowlerPrefab == null)
                HowlerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/FeedbackHowler.prefab");
            if (SanctuaryPrefab == null)
                SanctuaryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/RooftopSanctuary.prefab");
            if (BlockPrefab == null)
                BlockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Blocks/HushBlock_DoubleBeat.prefab");
            if (DebrisPrefab == null)
                DebrisPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/CharcoalDebris.prefab");
#endif
        }
    }
}

