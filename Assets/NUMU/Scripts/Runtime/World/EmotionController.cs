using System.Collections;
using UnityEngine;

namespace Numu.World
{
    /// <summary>Arm poses that make an emotion readable from the hands, placed with arm IK relative to the head.</summary>
    public enum HandGesture
    {
        None,
        /// <summary>Both hands up to the mouth and cheeks, palms on the face, elbows down (surprise).</summary>
        HandsToFace,
        /// <summary>Both hands raised in front of the chin, palms out (fear).</summary>
        HandsUpDefensive,
        /// <summary>One hand scratching the side of the head (confused).</summary>
        ScratchHead,
    }

    /// <summary>
    /// Reusable emotion performer for every NUMU character, in gameplay or cutscenes: sets the facial expression
    /// (blendshapes, via <see cref="NumuFacialExpressions"/>), supports it with a head pose (tilt, lowered or raised
    /// head), plays a matching body gesture and/or hand pose, and points the gaze. Eight emotions: neutral, happy, sad,
    /// angry, fear (afraid), surprise, disgust and confused. Head and hand poses are applied on top of any clip (idle,
    /// talk…) through the animator's IK pass and after animation, so they work on any humanoid.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class EmotionController : MonoBehaviour
    {
        public NumuFacialExpressions face;
        public NumuNpc npc;

        [Header("Gesture per emotion (animator states; empty = face only)")]
        public string happyGesture = "Wave";
        public string sadGesture = "TalkSad";
        public string angryGesture = "Angry";
        public string fearGesture = "Nervous";
        public string surpriseGesture = "";
        public string disgustGesture = "";
        public string confusedGesture = "";

        [Header("Hand pose per emotion (arm IK)")]
        public HandGesture surpriseHands = HandGesture.HandsToFace;
        public HandGesture fearHands = HandGesture.HandsUpDefensive;
        public HandGesture confusedHands = HandGesture.ScratchHead;
        [Tooltip("Seconds to raise or lower the hands.")]
        public float handBlendTime = 0.35f;

        [Header("Head pose supporting the face")]
        public bool headPose = true;
        [Tooltip("Seconds to settle into a new head pose.")]
        public float headBlendTime = 0.4f;

        public NumuEmotion Current { get; private set; }
        public HandGesture CurrentHands { get; private set; }

        Animator _anim;
        Transform _leftEye, _rightEye;
        HandGesture _shownHands;
        float _handWeight;
        Vector3 _headPose, _headPoseVelocity;
        float _intensity = 1f;

        /// <summary>Head pose per emotion in degrees: pitch (down +), yaw (turn), roll (tilt).</summary>
        public static Vector3 HeadPoseFor(NumuEmotion e)
        {
            switch (e)
            {
                case NumuEmotion.Happy: return new Vector3(-3f, 0f, 5f);     // chin up a little, friendly tilt
                case NumuEmotion.Sad: return new Vector3(14f, 0f, 4f);       // head lowered
                case NumuEmotion.Angry: return new Vector3(7f, 0f, 0f);      // head lowered, glaring
                case NumuEmotion.Fear: return new Vector3(5f, -7f, 0f);      // pulled down and slightly away
                case NumuEmotion.Surprise: return new Vector3(-6f, 0f, 0f);  // head back
                case NumuEmotion.Disgust: return new Vector3(-4f, 10f, -3f); // turning away
                case NumuEmotion.Confused: return new Vector3(0f, 6f, -11f); // a clear tilt, toward the head-scratching hand
                default: return Vector3.zero;
            }
        }

        void Awake()
        {
            _anim = GetComponent<Animator>();
            // Eye bones locate the face for the hand poses. Rigs that don't map them in the humanoid avatar
            // (e.g. Rocketbox) still have them under the head: "...LEye" / "...REye".
            _leftEye = _anim.GetBoneTransform(HumanBodyBones.LeftEye);
            _rightEye = _anim.GetBoneTransform(HumanBodyBones.RightEye);
            var headBone = _anim.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null && (_leftEye == null || _rightEye == null))
                foreach (var t in headBone.GetComponentsInChildren<Transform>())
                {
                    if (t.name.EndsWith("LEye")) _leftEye = t;
                    else if (t.name.EndsWith("REye")) _rightEye = t;
                }
            if (face == null) face = GetComponent<NumuFacialExpressions>();
            if (npc == null) npc = GetComponent<NumuNpc>();
        }

        /// <summary>Shows an emotion on the face (and optionally its gesture). Transitions are smooth.</summary>
        public void Show(NumuEmotion emotion, float intensity = 1f, bool withGesture = false)
        {
            Current = emotion;
            _intensity = intensity;
            if (face != null) face.SetExpression(emotion, intensity);
            if (withGesture)
            {
                Gesture(GestureFor(emotion));
                Hands(HandsFor(emotion));
            }
        }

        public void Neutral() => Show(NumuEmotion.Neutral);

        /// <summary>Holds an animator state (e.g. "Wave", "Talk") until <see cref="ReleaseGesture"/>.</summary>
        public void Gesture(string state)
        {
            if (npc != null && !string.IsNullOrEmpty(state)) npc.Override(state);
        }

        public void ReleaseGesture()
        {
            if (npc != null) npc.ReleaseOverride();
            Hands(HandGesture.None);
        }

        /// <summary>Raises the hands into a pose (blended over <see cref="handBlendTime"/>), or lowers them with None.</summary>
        public void Hands(HandGesture gesture) => CurrentHands = gesture;

        public void LookAt(Transform target, float weight = 0.8f, float heightOffset = 1f)
        {
            if (npc == null) return;
            npc.lookTarget = target;
            npc.maxLookWeight = weight;
            npc.lookHeightOffset = heightOffset;
        }

        /// <summary>Plays a short emotional moment: face + gesture for <paramref name="seconds"/>, then back to idle.</summary>
        public IEnumerator Perform(NumuEmotion emotion, float seconds, float intensity = 1f, string gesture = null)
        {
            Show(emotion, intensity);
            Gesture(gesture ?? GestureFor(emotion));
            Hands(HandsFor(emotion));
            yield return new WaitForSeconds(seconds);
            ReleaseGesture();
        }

        public string GestureFor(NumuEmotion e)
        {
            switch (e)
            {
                case NumuEmotion.Happy: return happyGesture;
                case NumuEmotion.Sad: return sadGesture;
                case NumuEmotion.Angry: return angryGesture;
                case NumuEmotion.Fear: return fearGesture;
                case NumuEmotion.Surprise: return surpriseGesture;
                case NumuEmotion.Disgust: return disgustGesture;
                case NumuEmotion.Confused: return confusedGesture;
                default: return null;
            }
        }

        public HandGesture HandsFor(NumuEmotion e) =>
            e == NumuEmotion.Surprise ? surpriseHands : e == NumuEmotion.Fear ? fearHands : e == NumuEmotion.Confused ? confusedHands : HandGesture.None;

        // ------------------------------------------------------------------ hand poses

        void Update()
        {
            if (CurrentHands != HandGesture.None) _shownHands = CurrentHands;
            float target = CurrentHands == HandGesture.None ? 0f : 1f;
            _handWeight = Mathf.MoveTowards(_handWeight, target, Time.deltaTime / Mathf.Max(0.01f, handBlendTime));
            var pose = headPose ? HeadPoseFor(Current) * _intensity : Vector3.zero;
            _headPose = Vector3.SmoothDamp(_headPose, pose, ref _headPoseVelocity, headBlendTime * 0.5f);
        }

        struct HandPlan { public bool active; public Vector3 wrist, elbow; public Quaternion rotation; }

        HandPlan Plan(int side, Transform hand)
        {
            // Character frame: the face is located from the eyes when they are mapped, else from the head bone.
            Transform head = _anim.GetBoneTransform(HumanBodyBones.Head);
            Vector3 fwd = transform.forward, right = transform.right, up = Vector3.up;
            bool hasEyes = _leftEye != null && _rightEye != null;
            Vector3 eyes = hasEyes ? (_leftEye.position + _rightEye.position) * 0.5f : head.position + up * 0.08f + fwd * 0.07f;
            // Face size: width from the eye spacing, height from the eyes' height above the head bone (a child's face
            // proportions: ~5 cm between the eyes, ~8 cm from the head bone up to the eyes).
            float faceScale = hasEyes ? Vector3.Distance(_leftEye.position, _rightEye.position) / 0.05f : 1f;
            float faceHeight = hasEyes ? Mathf.Max(0.5f, Vector3.Dot(eyes - head.position, up) / 0.08f) : 1f;
            Vector3 mouth = eyes - up * 0.058f * faceHeight + fwd * 0.012f * faceScale;
            Transform chest = _anim.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = _anim.GetBoneTransform(HumanBodyBones.Chest);
            Vector3 chestPos = chest != null ? chest.position : head.position - up * 0.25f;

            // Hand length from the rig: wrist → middle knuckle, times ~2 to the fingertips.
            var midKnuckle = _anim.GetBoneTransform(side < 0 ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            float handLen = midKnuckle != null ? Vector3.Distance(hand.position, midKnuckle.position) * 2.05f : 0.15f;

            Vector3 tip, fingers, thumbSide, elbow;
            if (_shownHands == HandGesture.ScratchHead)
            {
                // Right hand only, resting on the top-side of the head: fingers reaching across the crown, palm down,
                // elbow raised and out.
                if (side < 0) return new HandPlan { active = false };
                tip = eyes + right * 0.035f * faceScale + up * 0.095f * faceHeight - fwd * 0.035f * faceScale;
                fingers = (up * 0.35f - right - fwd * 0.1f).normalized;
                thumbSide = -fwd;
                elbow = chestPos + right * 0.3f + up * 0.24f;
            }
            else if (_shownHands == HandGesture.HandsToFace)
            {
                // Fingertips on the cheeks beside the mouth; fingers up and leaning in, palms toward the face.
                tip = mouth + right * side * 0.034f * faceScale + up * 0.03f * faceHeight + fwd * 0.012f * faceScale;
                fingers = (up - fwd * 0.28f - right * side * 0.32f).normalized;
                thumbSide = right * side;
                elbow = chestPos + right * side * 0.2f - up * 0.22f + fwd * 0.08f;
            }
            else
            {
                // Hands raised in front of the chin, palms out, a little apart.
                tip = mouth - up * 0.02f * faceHeight + right * side * 0.07f * faceScale + fwd * 0.17f;
                fingers = (up + fwd * 0.15f - right * side * 0.1f).normalized;
                thumbSide = -right * side;
                elbow = chestPos + right * side * 0.26f - up * 0.18f + fwd * 0.05f;
            }
            thumbSide = Vector3.ProjectOnPlane(thumbSide, fingers).normalized;
            return new HandPlan
            {
                active = true,
                wrist = tip - fingers * handLen,
                elbow = elbow,
                rotation = HandRotation(side, hand, fingers, thumbSide),
            };
        }

        /// <summary>World rotation of the hand bone with its fingers along <paramref name="fingers"/> and its thumb side
        /// along <paramref name="thumbSide"/>; the bone's local axes are measured from the rig, so any humanoid works.</summary>
        Quaternion HandRotation(int side, Transform hand, Vector3 fingers, Vector3 thumbSide)
        {
            var index = _anim.GetBoneTransform(side < 0 ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var little = _anim.GetBoneTransform(side < 0 ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            var middle = _anim.GetBoneTransform(side < 0 ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            if (index == null || little == null || middle == null) return hand.rotation;
            var inv = Quaternion.Inverse(hand.rotation);
            Vector3 localFingers = inv * (middle.position - hand.position);
            Vector3 localThumb = inv * (index.position - little.position);
            var localFrame = Quaternion.LookRotation(localFingers, Vector3.ProjectOnPlane(localThumb, localFingers));
            var worldFrame = Quaternion.LookRotation(fingers, thumbSide);
            return worldFrame * Quaternion.Inverse(localFrame);
        }

        void OnAnimatorIK(int layer)
        {
            if (_anim == null || _handWeight <= 0f || _shownHands == HandGesture.None) return;
            var lh = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
            var rh = _anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (lh == null || rh == null || _anim.GetBoneTransform(HumanBodyBones.Head) == null) return;
            float w = Mathf.SmoothStep(0f, 1f, _handWeight);
            foreach (var (goal, hint, plan) in new[] { (AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, Plan(-1, lh)), (AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, Plan(1, rh)) })
            {
                if (!plan.active) continue;
                _anim.SetIKPositionWeight(goal, w);
                _anim.SetIKPosition(goal, plan.wrist);
                _anim.SetIKHintPositionWeight(hint, w);
                _anim.SetIKHintPosition(hint, plan.elbow);
            }
        }

        void LateUpdate()
        {
            if (_anim == null) return;
            ApplyHeadPose();
            // The IK pass places the wrists; the hand orientation is set on the bones afterwards, exactly.
            if (_handWeight <= 0f || _shownHands == HandGesture.None) return;
            float w = Mathf.SmoothStep(0f, 1f, _handWeight);
            foreach (var (bone, side) in new[] { (HumanBodyBones.LeftHand, -1), (HumanBodyBones.RightHand, 1) })
            {
                var hand = _anim.GetBoneTransform(bone);
                if (hand == null) continue;
                var plan = Plan(side, hand);
                if (plan.active) hand.rotation = Quaternion.Slerp(hand.rotation, plan.rotation, w);
            }
        }

        /// <summary>Adds the emotion's head pose on top of the animation: 40% in the neck, the rest in the head.</summary>
        void ApplyHeadPose()
        {
            if (_headPose.sqrMagnitude < 0.01f) return;
            var head = _anim.GetBoneTransform(HumanBodyBones.Head);
            var neck = _anim.GetBoneTransform(HumanBodyBones.Neck);
            if (head == null) return;
            var q = Quaternion.AngleAxis(_headPose.z, transform.forward) * Quaternion.AngleAxis(_headPose.x, transform.right) * Quaternion.AngleAxis(_headPose.y, Vector3.up);
            if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, q, 0.4f) * neck.rotation;
            head.rotation = Quaternion.Slerp(Quaternion.identity, q, neck != null ? 0.6f : 1f) * head.rotation;
        }
    }
}
