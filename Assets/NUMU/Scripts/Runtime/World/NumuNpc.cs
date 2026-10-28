using System.Collections.Generic;
using UnityEngine;

namespace Numu.World
{
    /// <summary>
    /// Lightweight ambient character: plays a base animation, occasionally varies it, and turns its
    /// head toward the player when close (humanoid look-at IK). Optionally strolls between waypoints.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class NumuNpc : MonoBehaviour
    {
        public string displayName = "Friend";
        public string baseState = "Idle";
        public List<string> idleVariations = new List<string>();
        public Vector2 variationInterval = new Vector2(8f, 16f);
        public Transform lookTarget;
        public float lookRadius = 6f;
        [Tooltip("Height above the look target's origin to look at (1 m ≈ a child's face; 0 for a camera).")]
        public float lookHeightOffset = 1.0f;
        [Range(0f, 1f)] public float maxLookWeight = 0.75f;
        public NumuFacialExpressions face;

        [Header("Greeting (optional)")]
        [Tooltip("Animator state played once when the child first comes within greetRadius (e.g. \"Wave\").")]
        public string greetState;
        public float greetRadius = 6f;
        public NumuEmotion greetEmotion = NumuEmotion.Happy;
        bool _greeted;

        [Header("Hand IK (e.g. holding swing chains)")]
        public Transform leftHandTarget;
        public Transform rightHandTarget;

        [Header("Strolling (optional)")]
        public List<Transform> waypoints = new List<Transform>();
        public float walkSpeed = 0.9f;
        public string walkState = "Walk";

        Animator _anim;
        float _nextVariation;
        float _lookWeight;
        int _wp;
        bool _overridden;
        string _current;

        void Awake() => _anim = GetComponent<Animator>();

        void Start()
        {
            Play(baseState, 0f);
            _nextVariation = Time.time + Random.Range(variationInterval.x, variationInterval.y);
            // Desynchronise identical idles between characters.
            _anim.Update(Random.Range(0f, 3f));
        }

        public void Play(string state, float fade = 0.3f)
        {
            if (_current == state || _anim == null) return;
            _current = state;
            if (fade <= 0f) _anim.Play(state, 0, Random.value);
            else _anim.CrossFadeInFixedTime(state, fade);
        }

        /// <summary>Scenario control: hold a specific animation until ReleaseOverride.</summary>
        public void Override(string state, float fade = 0.3f)
        {
            _overridden = true;
            Play(state, fade);
        }

        /// <summary>
        /// Turns the body to a new facing over <paramref name="seconds"/>, playing a stepping turn clip so the
        /// feet move instead of sliding (the clip's own rotation is baked out; the body is rotated here).
        /// </summary>
        public System.Collections.IEnumerator TurnTo(Quaternion target, float seconds)
        {
            Quaternion from = transform.rotation;
            float angle = Vector3.SignedAngle(transform.forward, target * Vector3.forward, Vector3.up);
            bool step = Mathf.Abs(angle) > 25f && seconds > 0f;
            if (step) Override(angle < 0f ? "TurnLeft" : "TurnRight", 0.15f);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(from, target, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            transform.rotation = target;
            if (step) ReleaseOverride();
        }

        public void ReleaseOverride()
        {
            _overridden = false;
            Play(baseState);
        }

        void Update()
        {
            if (_overridden) return;
            if (!_greeted && !string.IsNullOrEmpty(greetState) && lookTarget != null &&
                Vector3.Distance(lookTarget.position, transform.position) < greetRadius)
            {
                _greeted = true;
                if (face != null) face.SetExpression(greetEmotion, 0.9f);
                Play(greetState, 0.25f);
                CancelInvoke(nameof(BackToBase));
                Invoke(nameof(BackToBase), 2.6f);
                _nextVariation = Time.time + 6f;
            }
            if (waypoints.Count > 1)
            {
                var wp = waypoints[_wp];
                Vector3 to = Vector3.ProjectOnPlane(wp.position - transform.position, Vector3.up);
                if (to.magnitude < 0.4f) _wp = (_wp + 1) % waypoints.Count;
                else
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.normalized), 180f * Time.deltaTime);
                    transform.position += transform.forward * walkSpeed * Time.deltaTime;
                    Play(walkState);
                }
                return;
            }
            if (idleVariations.Count > 0 && Time.time >= _nextVariation)
            {
                _nextVariation = Time.time + Random.Range(variationInterval.x, variationInterval.y);
                string v = idleVariations[Random.Range(0, idleVariations.Count)];
                Play(v, 0.4f);
                Invoke(nameof(BackToBase), 4f);
            }
        }

        void BackToBase()
        {
            if (!_overridden) Play(baseState, 0.5f);
        }

        void OnAnimatorIK(int layer)
        {
            foreach (var (goal, target) in new[] { (AvatarIKGoal.LeftHand, leftHandTarget), (AvatarIKGoal.RightHand, rightHandTarget) })
            {
                if (target == null) continue;
                _anim.SetIKPositionWeight(goal, 1f);
                _anim.SetIKRotationWeight(goal, 0.25f);
                _anim.SetIKPosition(goal, target.position);
                _anim.SetIKRotation(goal, target.rotation);
            }
            if (lookTarget == null) return;
            float d = Vector3.Distance(lookTarget.position, transform.position);
            Vector3 toTarget = (lookTarget.position - transform.position).normalized;
            bool inFront = Vector3.Dot(transform.forward, toTarget) > -0.1f;
            float want = d < lookRadius && inFront ? maxLookWeight : 0f;
            _lookWeight = Mathf.MoveTowards(_lookWeight, want, Time.deltaTime * 1.5f);
            _anim.SetLookAtWeight(_lookWeight, 0.15f, 0.8f, 1f, 0.6f);
            _anim.SetLookAtPosition(lookTarget.position + Vector3.up * lookHeightOffset);
        }
    }
}
