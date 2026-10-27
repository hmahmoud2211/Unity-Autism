using UnityEngine;

namespace Numu.World
{
    /// <summary>
    /// Calm third-person camera: follows from behind at a fixed, predictable angle and can be
    /// handed a "shot" (position + look target) for conversations, blending smoothly in and out.
    /// </summary>
    public class NumuFollowCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 5.2f;
        public float height = 2.6f;
        public float lookHeight = 1.0f;
        public float followSharpness = 5f;
        public float yawFollowSharpness = 1.6f;
        public LayerMask obstacleMask = ~0;

        Vector3 _shotPos, _shotLook;
        bool _hasShot;
        float _shotBlend;
        float _yaw;
        Vector3 _smoothedLook;

        void Start()
        {
            if (target != null) _yaw = target.eulerAngles.y;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = DesiredFollowPosition();
            _smoothedLook = target.position + Vector3.up * lookHeight;
            transform.rotation = Quaternion.LookRotation(_smoothedLook - transform.position);
        }

        /// <summary>Moves the camera to a framed shot (e.g. a close-up on a face).</summary>
        public void SetShot(Vector3 position, Vector3 lookAt)
        {
            _shotPos = position;
            _shotLook = lookAt;
            _hasShot = true;
            if (Numu.NumuSettings.ReducedMotion) _shotBlend = 1f;
        }

        public void ClearShot()
        {
            _hasShot = false;
            if (Numu.NumuSettings.ReducedMotion) _shotBlend = 0f;
        }

        Vector3 DesiredFollowPosition()
        {
            Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * lookHeight;
            Vector3 desired = target.position + yawRot * new Vector3(0f, height, -distance);
            // Pull in if something solid is between the child and the camera.
            if (Physics.Linecast(pivot, desired, out var hit, obstacleMask, QueryTriggerInteraction.Ignore))
                desired = hit.point + (pivot - desired).normalized * 0.3f;
            return desired;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            bool reduced = Numu.NumuSettings.ReducedMotion;
            // Only drift the yaw while the child is walking, so the view stays put when standing still.
            var player = target.GetComponent<NumuPlayerController>();
            if (player != null && player.CurrentSpeed > 0.2f)
                _yaw = Mathf.LerpAngle(_yaw, target.eulerAngles.y, 1f - Mathf.Exp(-yawFollowSharpness * dt));

            _shotBlend = Mathf.MoveTowards(_shotBlend, _hasShot ? 1f : 0f, dt * (reduced ? 100f : 1.1f));
            float s = Mathf.SmoothStep(0f, 1f, _shotBlend);

            Vector3 followPos = DesiredFollowPosition();
            Vector3 followLook = target.position + Vector3.up * lookHeight;
            Vector3 wantPos = Vector3.Lerp(followPos, _shotPos, s);
            Vector3 wantLook = Vector3.Lerp(followLook, _shotLook, s);

            float k = reduced ? 1f : 1f - Mathf.Exp(-followSharpness * dt);
            transform.position = Vector3.Lerp(transform.position, wantPos, k);
            _smoothedLook = Vector3.Lerp(_smoothedLook, wantLook, k);
            transform.rotation = Quaternion.LookRotation(_smoothedLook - transform.position);
        }
    }
}
