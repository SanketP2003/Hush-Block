using System;
using UnityEngine;

namespace HushBlocks.FullGame
{
    /// <summary>
    /// Serialized player profile. Kept deliberately small and versioned so a
    /// future content patch can migrate saves without losing progress.
    /// </summary>
    [Serializable]
    public sealed class HushSaveData
    {
        public int version = 1;

        public int[] bestScores = new int[DistrictCatalog.Count];
        public int[] stars = new int[DistrictCatalog.Count];
        public bool[] unlocked = new bool[DistrictCatalog.Count];

        public bool tutorialSeen;
        public bool completed;
        public int runs;
        public int totalPerfects;
        public int totalFloors;

        public float masterVolume = 0.9f;
        public float musicVolume = 0.62f;
        public float sfxVolume = 0.9f;
        public bool beatClicks = true;
        public bool screenShake = true;
        public bool reducedMotion;

        public void Normalize()
        {
            int count = DistrictCatalog.Count;
            bestScores = Resize(bestScores, count);
            stars = Resize(stars, count);
            unlocked = Resize(unlocked, count);
            if (!HasAny(unlocked)) unlocked[0] = true;
            unlocked[0] = true;

            masterVolume = Mathf.Clamp01(masterVolume);
            musicVolume = Mathf.Clamp01(musicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
        }

        private static int[] Resize(int[] source, int size)
        {
            var result = new int[size];
            if (source != null) Array.Copy(source, result, Mathf.Min(source.Length, size));
            return result;
        }

        private static bool[] Resize(bool[] source, int size)
        {
            var result = new bool[size];
            if (source != null) Array.Copy(source, result, Mathf.Min(source.Length, size));
            return result;
        }

        private static bool HasAny(bool[] source)
        {
            if (source == null) return false;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i]) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// PlayerPrefs-backed profile access with change notifications for live
    /// settings. All reads normalize the data so corrupt or partial saves are
    /// repaired instead of throwing.
    /// </summary>
    public static class HushProgress
    {
        public const string SaveKey = "HushBlocks.FullGame.Save.v1";

        private static HushSaveData data;

        public static event Action Changed;

        public static HushSaveData Data
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        public static void Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    data = JsonUtility.FromJson<HushSaveData>(json);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Hush Blocks] Save data could not be read, starting a fresh profile. " + exception.Message);
                    data = null;
                }
            }

            if (data == null) data = new HushSaveData();
            data.Normalize();
        }

        public static void Save()
        {
            if (data == null) Load();
            data.Normalize();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void NotifyChanged()
        {
            Save();
            Changed?.Invoke();
        }

        public static bool IsUnlocked(int index)
        {
            if (index < 0) return false;
            var save = Data;
            return index < save.unlocked.Length && save.unlocked[index];
        }

        public static int TotalStars
        {
            get
            {
                int total = 0;
                var save = Data;
                for (int i = 0; i < save.stars.Length; i++) total += save.stars[i];
                return total;
            }
        }

        public static int BestScore
        {
            get
            {
                int best = 0;
                var save = Data;
                for (int i = 0; i < save.bestScores.Length; i++) best = Mathf.Max(best, save.bestScores[i]);
                return best;
            }
        }

        /// <summary>First district that has not been cleared, or the last one.</summary>
        public static int NextDistrict
        {
            get
            {
                var save = Data;
                for (int i = 0; i < save.stars.Length; i++)
                {
                    if (save.stars[i] <= 0 && save.unlocked[i]) return i;
                }
                return DistrictCatalog.Count - 1;
            }
        }

        public static int ClearedDistricts
        {
            get
            {
                int cleared = 0;
                var save = Data;
                for (int i = 0; i < save.stars.Length; i++)
                {
                    if (save.stars[i] > 0) cleared++;
                }
                return cleared;
            }
        }

        public static bool AllCleared => ClearedDistricts >= DistrictCatalog.Count;

        /// <summary>Stores a finished run. Returns true when a new personal best was set.</summary>
        public static bool SubmitRun(int district, int score, int stars, bool success)
        {
            var save = Data;
            bool newBest = score > save.bestScores[district];
            save.bestScores[district] = Mathf.Max(save.bestScores[district], score);
            if (success)
            {
                save.stars[district] = Mathf.Max(save.stars[district], Mathf.Clamp(stars, 1, 3));
                if (district + 1 < save.unlocked.Length) save.unlocked[district + 1] = true;
                if (district == DistrictCatalog.Count - 1) save.completed = true;
            }
            save.runs++;
            Save();
            Changed?.Invoke();
            return newBest;
        }

        public static void MarkTutorialSeen()
        {
            if (Data.tutorialSeen) return;
            Data.tutorialSeen = true;
            Save();
        }

        public static void ResetProgress()
        {
            var old = Data;
            data = new HushSaveData
            {
                masterVolume = old.masterVolume,
                musicVolume = old.musicVolume,
                sfxVolume = old.sfxVolume,
                beatClicks = old.beatClicks,
                screenShake = old.screenShake,
                reducedMotion = old.reducedMotion
            };
            data.Normalize();
            Save();
            Changed?.Invoke();
        }
    }
}
