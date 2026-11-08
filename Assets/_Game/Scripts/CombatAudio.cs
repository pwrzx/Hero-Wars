using UnityEngine;

namespace TowerTrials
{
    public enum CombatSound { Swing, Hit, Power, Collapse, Failure }

    // Short original synthesized cues; no downloads, network access or external samples.
    public sealed class CombatAudio : MonoBehaviour
    {
        private AudioSource source;
        private readonly AudioClip[] clips = new AudioClip[5];
        public void Configure(float volume)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = volume;
            for (int i = 0; i < clips.Length; i++) clips[i] = Create((CombatSound)i);
        }

        public void Play(CombatSound sound)
        { if (source != null && clips[(int)sound] != null) source.PlayOneShot(clips[(int)sound]); }

        private static AudioClip Create(CombatSound sound)
        {
            const int sampleRate = 22050;
            float duration = sound == CombatSound.Collapse ? 0.55f : sound == CombatSound.Power ? 0.33f : 0.19f;
            var samples = new float[Mathf.CeilToInt(sampleRate * duration)];
            uint noiseState = 7321;
            float filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / sampleRate, t = time / duration;
                noiseState = noiseState * 1664525u + 1013904223u;
                float noise = (noiseState & 65535) / 32768f - 1;
                filtered = Mathf.Lerp(filtered, noise, 0.12f);
                float value;
                switch (sound)
                {
                    case CombatSound.Swing: value = (noise - filtered) * Mathf.Sin(t * Mathf.PI) * 0.32f; break;
                    case CombatSound.Hit: value = Mathf.Sin(time * 780 * Mathf.PI * 2) * 0.28f + Mathf.Sin(time * 1253 * Mathf.PI * 2) * 0.18f + noise * 0.13f; break;
                    case CombatSound.Power: value = Mathf.Sin(time * 1046.5f * Mathf.PI * 2) * 0.23f + Mathf.Sin(time * 1568 * Mathf.PI * 2) * 0.12f; break;
                    case CombatSound.Collapse: value = filtered * 0.7f + Mathf.Sin(time * 65 * Mathf.PI * 2) * 0.22f; break;
                    default: value = Mathf.Sin(time * Mathf.Lerp(280, 130, t) * Mathf.PI * 2) * 0.3f; break;
                }
                float envelope = Mathf.Min(1, time / 0.008f) * Mathf.Pow(1 - t, sound == CombatSound.Swing ? 1 : 2.8f);
                samples[i] = value * envelope;
            }
            var clip = AudioClip.Create("Synth_" + sound, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            foreach (AudioClip clip in clips)
                if (clip != null) { if (Application.isPlaying) Destroy(clip); else DestroyImmediate(clip); }
        }
    }
}
