using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace HushBlocks.Editor
{
    public static class DecibelRowAudioMixerBuilder
    {
        private const string MixerPath = "Assets/_Project/Audio/AM_DecibelRow.mixer";

        [MenuItem("Hush Blocks/Audio/Setup Decibel Row AudioMixer")]
        public static void SetupAudioMixer()
        {
            var asm = typeof(UnityEditor.Editor).Assembly;
            var mixerControllerType = asm.GetType("UnityEditor.Audio.AudioMixerController");
            if (mixerControllerType == null)
            {
                Debug.LogError("[DecibelRowAudioMixerBuilder] Failed to find AudioMixerController type.");
                return;
            }

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                Debug.LogError($"[DecibelRowAudioMixerBuilder] No AudioMixer found at {MixerPath}.");
                return;
            }

            try
            {
                // 1. Configure Snapshots
                var allAssets = AssetDatabase.LoadAllAssetsAtPath(MixerPath);
                var snapshotList = new List<UnityEngine.Object>();
                foreach (var asset in allAssets)
                {
                    if (asset != null && asset.GetType().Name == "AudioMixerSnapshotController")
                    {
                        snapshotList.Add(asset);
                    }
                }

                string[] desiredSnapshots = new string[]
                {
                    "Snapshot_ChaoticStreet",
                    "Snapshot_MutedSanctuary",
                    "Snapshot_TremorHazard"
                };

                for (int i = 0; i < desiredSnapshots.Length && i < snapshotList.Count; i++)
                {
                    snapshotList[i].name = desiredSnapshots[i];
                    EditorUtility.SetDirty(snapshotList[i]);
                }

                // 2. Configure Groups
                var masterProp = mixerControllerType.GetProperty("masterGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var masterGroup = masterProp?.GetValue(mixer, null);

                var createGroupMethod = mixerControllerType.GetMethod("CreateNewGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var addChildMethod = mixerControllerType.GetMethod("AddChildToParent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (masterGroup == null || createGroupMethod == null || addChildMethod == null)
                {
                    Debug.LogError("[DecibelRowAudioMixerBuilder] Failed to reflect mixer group methods.");
                    return;
                }

                var existingGroups = mixer.FindMatchingGroups(string.Empty);
                var groupMap = new Dictionary<string, UnityEngine.Audio.AudioMixerGroup>();
                foreach (var g in existingGroups)
                {
                    if (g != null && !groupMap.ContainsKey(g.name))
                    {
                        groupMap[g.name] = g;
                    }
                }

                // Helper to create or get group
                object GetOrCreateGroup(string groupName, object parent)
                {
                    if (groupMap.TryGetValue(groupName, out var existing))
                    {
                        return existing;
                    }

                    var newGroup = createGroupMethod.Invoke(mixer, new object[] { groupName, false });
                    if (parent != null)
                    {
                        addChildMethod.Invoke(mixer, new object[] { newGroup, parent });
                    }
                    return newGroup;
                }

                // Level 1: BGM_Music & SFX_Acoustic
                var bgmGroup = GetOrCreateGroup("BGM_Music", masterGroup);
                var sfxGroup = GetOrCreateGroup("SFX_Acoustic", masterGroup);

                // Level 2: BGM stems
                GetOrCreateGroup("Stem_Cacophony", bgmGroup);
                GetOrCreateGroup("Stem_Lullaby", bgmGroup);

                // Level 2: SFX buses
                GetOrCreateGroup("SFX_Taps", sfxGroup);
                GetOrCreateGroup("SFX_Resonance", sfxGroup);
                GetOrCreateGroup("SFX_Creatures", sfxGroup);

                EditorUtility.SetDirty(mixer);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[DecibelRowAudioMixerBuilder] Successfully configured AM_DecibelRow AudioMixer hierarchy and snapshots!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DecibelRowAudioMixerBuilder] Error configuring AudioMixer: {ex}");
            }
        }
    }
}

