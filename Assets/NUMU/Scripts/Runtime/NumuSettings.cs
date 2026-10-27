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
