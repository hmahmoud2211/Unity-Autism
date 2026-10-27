using UnityEngine;

namespace Numu
{
    /// <summary>Slow idle drift / bob for clouds, boats and mascots. Calms down under reduced motion.</summary>
    public class NumuDrift : MonoBehaviour
    {
        public Vector3 amplitude = new Vector3(0.3f, 0.1f, 0.1f);
        public float speed = 0.25f;
        public float rollDegrees;
        public float yawDegrees;
        public float phase;

        Vector3 _rest;
        Quaternion _restRot;

        void Awake()
        {
            _rest = transform.localPosition;
            _restRot = transform.localRotation;
            if (Mathf.Approximately(phase, 0f)) phase = (transform.position.x * 0.37f + transform.position.z * 0.21f) % 6.28f;
        }

        void Update()
        {
            float k = NumuSettings.ReducedMotion ? 0.1f : 1f;
            float t = Time.time * speed + phase;
            transform.localPosition = _rest + new Vector3(Mathf.Sin(t) * amplitude.x, Mathf.Sin(t * 1.7f) * amplitude.y, Mathf.Cos(t * 0.8f) * amplitude.z) * k;
            if (rollDegrees != 0f || yawDegrees != 0f)
                transform.localRotation = _restRot * Quaternion.Euler(0f, Mathf.Sin(t * 0.9f) * yawDegrees * k, Mathf.Sin(t * 1.3f) * rollDegrees * k);
        }
    }
}
