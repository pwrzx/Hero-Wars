using UnityEngine;

namespace TowerTrials
{
    // Original, softly voiced music and UI cues. Nothing streams or downloads in a player.
    public sealed class GameAudio : MonoBehaviour
    {
        private static GameAudio instance;
        private AudioSource music, ui;
        private AudioClip melody, click;
        public static void Ensure()
        {
            if (!Application.isPlaying || instance != null) return;
            var root = new GameObject("GameAudio");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<GameAudio>();
        }
        private void Awake()
        {
            music = gameObject.AddComponent<AudioSource>();
            ui = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = ui.playOnAwake = false;
            music.spatialBlend = ui.spatialBlend = 0;
            music.loop = true;
            melody = CreateMusic();
            click = CreateClick();
            music.clip = melody;
            music.volume = 0;
            music.Play();
        }
        private void Update()
        {
            float target = GamePreferences.Music ? 0.16f : 0;
            music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime * 0.3f);
        }
        public static void Click()
        {
            Ensure();
            if (instance != null && GamePreferences.Sound) instance.ui.PlayOneShot(instance.click, 0.16f);
        }
        private static AudioClip CreateMusic()
        {
            const int rate = 22050, seconds = 16;
            var data = new float[rate * seconds];
            int[] notes = { 57, 64, 69, 64, 53, 60, 65, 60, 55, 62, 67, 62, 52, 59, 64, 59 };
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                int beat = Mathf.FloorToInt(t);
                float age = t - beat;
                float frequency = 440 * Mathf.Pow(2, (notes[beat] - 69) / 12f);
                float attack = Mathf.Min(1, age / 0.025f);
                float pluck = Mathf.Exp(-age * 4) * attack;
                float bell = Mathf.Sin(t * frequency * 2 * Mathf.PI) + 0.22f * Mathf.Sin(t * frequency * 4 * Mathf.PI);
                float pad = Mathf.Sin(t * 110 * 2 * Mathf.PI) * 0.05f + Mathf.Sin(t * 165 * 2 * Mathf.PI) * 0.025f;
                // All sustained oscillators complete integral cycles at the loop boundary.
                data[i] = bell * pluck * 0.30f + pad;
            }
            var clip = AudioClip.Create("Original_Lanterns", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
        private static AudioClip CreateClick()
        {
            const int rate = 22050;
            var data = new float[2205];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                data[i] = Mathf.Sin(t * 880 * Mathf.PI * 2) * Mathf.Exp(-t * 55) * Mathf.Min(1, t / 0.004f);
            }
            var clip = AudioClip.Create("Original_Confirm", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
        private void OnApplicationPause(bool paused) { AudioListener.pause = paused; }
        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (melody != null) Destroy(melody);
            if (click != null) Destroy(click);
        }
    }
}
