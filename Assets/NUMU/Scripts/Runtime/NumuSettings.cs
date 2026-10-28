using System;
using UnityEngine;

namespace Numu
{
    /// <summary>Player-facing comfort settings, persisted in PlayerPrefs.</summary>
    public static class NumuSettings
    {
        const string ReducedMotionKey = "numu.reducedMotion";
        const string SoundKey = "numu.sound";

        public static event Action Changed;

        public static bool ReducedMotion
        {
            get => PlayerPrefs.GetInt(ReducedMotionKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(ReducedMotionKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        const string LookSensitivityKey = "numu.lookSensitivity";
        const string FirstPersonKey = "numu.firstPerson";

        /// <summary>Camera look speed multiplier for mouse, touch and gamepad (0.3–2.5, default 1).</summary>
        public static float LookSensitivity
        {
            get => PlayerPrefs.GetFloat(LookSensitivityKey, 1f);
            set
            {
                PlayerPrefs.SetFloat(LookSensitivityKey, Mathf.Clamp(value, 0.3f, 2.5f));
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>The child's preferred view in the realistic worlds (third person by default).</summary>
        public static bool PreferFirstPerson
        {
            get => PlayerPrefs.GetInt(FirstPersonKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(FirstPersonKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(SoundKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }
    }
}
