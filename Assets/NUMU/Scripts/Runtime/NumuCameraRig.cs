using System;
using UnityEngine;

namespace Numu
{
    /// <summary>
    /// Orbit-style camera around a pivot. Keeps the reference 5:3 composition on any landscape
    /// aspect (wider screens reveal more ocean, narrower ones widen the field of view) and
    /// eases smoothly between the world overview and island close-ups.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class NumuCameraRig : MonoBehaviour
    {
        [Serializable]
        public struct Pose
        {
            public Vector3 pivot;
            public float pitch;
            public float yaw;
            public float distance;
            public float fov;

            public static Pose Lerp(Pose a, Pose b, float t) => new Pose
            {
                pivot = Vector3.Lerp(a.pivot, b.pivot, t),
                pitch = Mathf.LerpAngle(a.pitch, b.pitch, t),
                yaw = Mathf.LerpAngle(a.yaw, b.yaw, t),
                distance = Mathf.Exp(Mathf.Lerp(Mathf.Log(a.distance), Mathf.Log(b.distance), t)),
                fov = Mathf.Lerp(a.fov, b.fov, t),
            };
        }

        public Pose overview = new Pose { pivot = Vector3.zero, pitch = 38f, yaw = 0f, distance = 220f, fov = 15f };
        [Tooltip("Aspect ratio of the reference composition (1584 x 993).")]
        public float designAspect = 1584f / 993f;
        public float transitionTime = 1.1f;

        public event Action Arrived;

        Camera _cam;
        Pose _current, _from, _to;
        float _t = 1f;
        bool _animating;

        public bool IsAnimating => _animating;
        public Pose Current => _current;
        public Camera Cam => _cam != null ? _cam : (_cam = GetComponent<Camera>());

        void OnEnable()
        {
            _current = overview;
            Apply(_current);
        }

        public void SnapTo(Pose p)
        {
            _animating = false;
            _current = p;
            Apply(p);
            Arrived?.Invoke();
        }

        public void MoveTo(Pose p)
        {
            if (NumuSettings.ReducedMotion)
            {
                SnapTo(p);
                return;
            }
            _from = _current;
            _to = p;
            _t = 0f;
            _animating = true;
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                _current = overview;
                Apply(_current);
                return;
            }
            if (_animating)
            {
                _t += Time.unscaledDeltaTime / Mathf.Max(0.05f, transitionTime);
                float e = _t >= 1f ? 1f : (_t < 0.5f ? 4f * _t * _t * _t : 1f - Mathf.Pow(-2f * _t + 2f, 3f) / 2f);
                _current = Pose.Lerp(_from, _to, e);
                if (_t >= 1f)
                {
                    _animating = false;
                    _current = _to;
                    Apply(_current);
                    Arrived?.Invoke();
                    return;
                }
            }
            Apply(_current);
        }

        public void Apply(Pose p)
        {
            Quaternion rot = Quaternion.Euler(p.pitch, p.yaw, 0f);
            transform.SetPositionAndRotation(p.pivot - rot * Vector3.forward * p.distance, rot);
            Cam.fieldOfView = FitFov(p.fov, Cam.aspect);
        }

        /// <summary>Vertical FOV that keeps the design's horizontal coverage on narrower screens.</summary>
        public float FitFov(float verticalFov, float aspect)
        {
            if (aspect >= designAspect || aspect <= 0f) return verticalFov;
            float tanH = Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad) * designAspect;
            return 2f * Mathf.Atan(tanH / aspect) * Mathf.Rad2Deg;
        }
    }
}
