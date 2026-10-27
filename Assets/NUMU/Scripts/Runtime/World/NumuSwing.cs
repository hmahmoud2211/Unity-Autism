using UnityEngine;

namespace Numu.World
{
    /// <summary>
    /// Pendulum swing: rotates the pivot (chains + seat + rider) with a period derived from the chain
    /// length, easing smoothly between amplitudes so the child can slow down to talk.
    /// </summary>
    public class NumuSwing : MonoBehaviour
    {
        public Transform pivot;
        public float chainLength = 2.1f;
        [Tooltip("Swing amplitude in degrees.")]
        public float amplitude = 24f;
        public float amplitudeChangeRate = 10f;
        [Tooltip("Gentler amplitude used when Reduced Motion is enabled.")]
        public float reducedMotionAmplitude = 9f;

        float _phase;
        float _currentAmplitude;
        Quaternion _rest;

        public float TargetAmplitude { get; set; }

        void Awake()
        {
            if (pivot == null) pivot = transform;
            _rest = pivot.localRotation;
            TargetAmplitude = amplitude;
            _currentAmplitude = amplitude;
        }

        void Update()
        {
            float target = Numu.NumuSettings.ReducedMotion ? Mathf.Min(TargetAmplitude, reducedMotionAmplitude) : TargetAmplitude;
            _currentAmplitude = Mathf.MoveTowards(_currentAmplitude, target, amplitudeChangeRate * Time.deltaTime);
            float omega = Mathf.Sqrt(9.81f / Mathf.Max(0.5f, chainLength));
            _phase += omega * Time.deltaTime;
            float angle = Mathf.Sin(_phase) * _currentAmplitude;
            pivot.localRotation = _rest * Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
