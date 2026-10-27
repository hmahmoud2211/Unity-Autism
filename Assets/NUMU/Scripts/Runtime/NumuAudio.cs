using UnityEngine;

namespace Numu
{
    public enum NumuSfx { Tap, Open, Close, Whoosh, Success, Locked }

    /// <summary>Soft, child-friendly UI sounds synthesised at startup (no audio assets needed).</summary>
    [RequireComponent(typeof(AudioSource))]
    public class NumuAudio : MonoBehaviour
    {
        static NumuAudio _instance;
        AudioSource _source;
        AudioClip[] _clips;

        void Awake()
        {
            _instance = this;
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0.55f;
            _clips = new[]
            {
                Tone("tap", new[] { 880f }, 0.07f, 0.6f),
                Tone("open", new[] { 660f, 990f }, 0.16f, 0.5f),
                Tone("close", new[] { 740f, 520f }, 0.14f, 0.45f),
                Sweep("whoosh", 260f, 620f, 0.35f, 0.28f),
                Tone("success", new[] { 784f, 988f, 1175f, 1568f }, 0.42f, 0.5f),
                Tone("locked", new[] { 330f, 300f }, 0.18f, 0.35f),
            };
        }

        public static void Play(NumuSfx sfx)
        {
            if (_instance == null || !NumuSettings.SoundOn) return;
            _instance._source.PlayOneShot(_instance._clips[(int)sfx]);
        }

        /// <summary>Sequence of soft bell-like notes (sine + a little second harmonic, quick attack, smooth decay).</summary>
        static AudioClip Tone(string name, float[] notes, float length, float gain)
        {
            int rate = 44100;
            int samples = Mathf.CeilToInt(rate * length);
            var data = new float[samples];
            float noteLen = length / notes.Length;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                int n = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / noteLen));
                float lt = t - n * noteLen;
                float env = Mathf.Min(1f, lt / 0.006f) * Mathf.Exp(-lt * 9f / Mathf.Max(0.05f, noteLen * 2.2f));
                float f = notes[n];
                data[i] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.8f + Mathf.Sin(4f * Mathf.PI * f * t) * 0.2f) * env * gain;
            }
            var clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Sweep(string name, float f0, float f1, float length, float gain)
        {
            int rate = 44100;
            int samples = Mathf.CeilToInt(rate * length);
            var data = new float[samples];
            float phase = 0f;
            var rng = new System.Random(3);
            for (int i = 0; i < samples; i++)
            {
                float u = i / (float)samples;
                float f = Mathf.Lerp(f0, f1, u * u);
                phase += 2f * Mathf.PI * f / rate;
                float env = Mathf.Sin(u * Mathf.PI);
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.25f;
                data[i] = (Mathf.Sin(phase) * 0.35f + noise) * env * gain;
            }
            var clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
