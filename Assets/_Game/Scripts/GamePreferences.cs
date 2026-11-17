using UnityEngine;

namespace TowerTrials
{
    public static class GamePreferences
    {
        public static bool Sound => PlayerPrefs.GetInt("HeroWars.Sound", 1) != 0;
        public static bool Music => PlayerPrefs.GetInt("HeroWars.Music", 1) != 0;
        public static bool ReducedMotion => PlayerPrefs.GetInt("HeroWars.ReducedMotion", 0) != 0;
        public static bool BatterySaver => PlayerPrefs.GetInt("HeroWars.BatterySaver", 0) != 0;
        public static bool Haptics => PlayerPrefs.GetInt("HeroWars.Haptics", 0) != 0;

        public static void Toggle(string preference)
        {
            string key = "HeroWars." + preference;
            int fallback = preference == "Sound" || preference == "Music" ? 1 : 0;
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, fallback) == 0 ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
        }

        public static void Apply()
        {
            Application.targetFrameRate = BatterySaver ? 30 : 60;
            QualitySettings.vSyncCount = 0;
            AudioListener.volume = 1;
            Screen.orientation = ScreenOrientation.Portrait;
            GameAudio.Ensure();
        }

        public static void Impact()
        {
            if (!Haptics || !Application.isMobilePlatform) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
            using (var pulse = effect.CallStatic<AndroidJavaObject>("createOneShot", 18L, -1))
                if (vibrator != null && vibrator.Call<bool>("hasVibrator")) vibrator.Call("vibrate", pulse);
#elif UNITY_IOS && !UNITY_EDITOR
            PlayImpact();
#endif
        }
#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void PlayImpact();
#endif
    }
}
