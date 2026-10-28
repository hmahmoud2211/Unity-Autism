using System.Collections;
using UnityEngine;

namespace Numu.World
{
    /// <summary>
    /// Reusable emotion performer for any character, in gameplay or cutscenes: sets the facial expression
    /// (blendshapes, via <see cref="NumuFacialExpressions"/>), plays a matching body gesture and points the gaze.
    /// Seven emotions: neutral, happy, sad, angry, fear, surprise and disgust.
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

        public NumuEmotion Current { get; private set; }

        void Awake()
        {
            if (face == null) face = GetComponent<NumuFacialExpressions>();
            if (npc == null) npc = GetComponent<NumuNpc>();
        }

        /// <summary>Shows an emotion on the face (and optionally its gesture). Transitions are smooth.</summary>
        public void Show(NumuEmotion emotion, float intensity = 1f, bool withGesture = false)
        {
            Current = emotion;
            if (face != null) face.SetExpression(emotion, intensity);
            if (withGesture) Gesture(GestureFor(emotion));
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
        }

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
                default: return null;
            }
        }
    }
}
