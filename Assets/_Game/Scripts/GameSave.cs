using System;
using UnityEngine;

namespace TowerTrials
{
    // Keep a second valid document so a damaged primary save can recover locally.
    public static class GameSave
    {
        public static T Read<T>(string key, Func<T, bool> valid = null) where T : class
        {
            foreach (string candidate in new[] { key, key + ".backup" })
            {
                string json = PlayerPrefs.GetString(candidate, "");
                if (string.IsNullOrEmpty(json)) continue;
                try
                {
                    T value = JsonUtility.FromJson<T>(json);
                    if (value != null && (valid == null || valid(value))) return value;
                }
                catch (ArgumentException) { }
            }
            return null;
        }

        public static void Write<T>(string key, T value)
        {
            string json = JsonUtility.ToJson(value);
            PlayerPrefs.SetString(key + ".backup", json);
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        // Compiled out of every player build, including development builds.
        public static HeroProfile ResetForDevelopment(HeroCenterCatalog catalog)
        {
            foreach (string key in new[] { HeroProfile.SaveKey, HeroProfile.SaveKey + ".backup",
                "TowerTrials.Run.v1", "TowerTrials.Run.v1.backup", CampaignConfig.SelectedStageKey, "HeroWars.LearnedRules" })
                PlayerPrefs.DeleteKey(key);
            var fresh = new HeroProfile { gold = 0, gems = 0, progressionVersion = 1 };
            fresh.Repair(catalog);
            fresh.Save();
            return fresh;
        }
#endif
    }
}
