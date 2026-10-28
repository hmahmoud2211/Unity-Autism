using UnityEngine;
using UnityEngine.InputSystem;

namespace Numu.World
{
    /// <summary>
    /// Character test driver: shows an emotion (face, plus hand pose when enabled) on start, and lets a tester switch
    /// with the number keys: 1 Neutral, 2 Happy, 3 Sad, 4 Angry, 5 Surprised, 6 Afraid, 7 Disgust, 8 Confused; H toggles hands.
    /// </summary>
    public class NumuExpressionShowcase : MonoBehaviour
    {
        public EmotionController character;
        [Tooltip("Other characters that show the same emotion (the cast lineup).")]
        public EmotionController[] cast = new EmotionController[0];
        public NumuEmotion startEmotion = NumuEmotion.Surprise;
        public bool withHands = true;
        [Tooltip("Animator state to hold while showing expressions (a calm idle keeps the face toward the camera).")]
        public string idleState = "Breathe";

        static readonly NumuEmotion[] Keys =
        {
            NumuEmotion.Neutral, NumuEmotion.Happy, NumuEmotion.Sad, NumuEmotion.Angry,
            NumuEmotion.Surprise, NumuEmotion.Fear, NumuEmotion.Disgust, NumuEmotion.Confused,
        };

        void Start()
        {
            if (character == null) character = GetComponentInChildren<EmotionController>();
            if (character == null) return;
            foreach (var c in All())
            {
                var anim = c.GetComponent<Animator>();
                if (!string.IsNullOrEmpty(idleState) && anim.HasState(0, Animator.StringToHash(idleState))) anim.Play(idleState);
            }
            Show(startEmotion);
        }

        System.Collections.Generic.IEnumerable<EmotionController> All()
        {
            yield return character;
            foreach (var c in cast)
                if (c != null) yield return c;
        }

        /// <summary>Shows an emotion on the character and the whole lineup (hands per <see cref="withHands"/>).</summary>
        public void Show(NumuEmotion emotion)
        {
            foreach (var c in All())
            {
                c.Hands(HandGesture.None);
                c.Show(emotion, 1f, withHands);
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || character == null) return;
            for (int i = 0; i < Keys.Length; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) Show(Keys[i]);
            if (kb.hKey.wasPressedThisFrame)
            {
                withHands = !withHands;
                foreach (var c in All()) c.Hands(withHands ? c.HandsFor(c.Current) : HandGesture.None);
            }
        }
    }
}
