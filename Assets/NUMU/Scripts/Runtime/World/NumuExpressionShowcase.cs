using UnityEngine;
using UnityEngine.InputSystem;

namespace Numu.World
{
    /// <summary>
    /// Character test driver: shows an emotion (face, plus hand pose when enabled) on start, and lets a tester switch
    /// with the number keys: 1 Neutral, 2 Happy, 3 Sad, 4 Angry, 5 Surprised, 6 Afraid, 7 Disgust; H toggles hands.
    /// </summary>
    public class NumuExpressionShowcase : MonoBehaviour
    {
        public EmotionController character;
        public NumuEmotion startEmotion = NumuEmotion.Surprise;
        public bool withHands = true;
        [Tooltip("Animator state to hold while showing expressions (a calm idle keeps the face toward the camera).")]
        public string idleState = "Breathe";

        static readonly NumuEmotion[] Keys =
        {
            NumuEmotion.Neutral, NumuEmotion.Happy, NumuEmotion.Sad, NumuEmotion.Angry,
            NumuEmotion.Surprise, NumuEmotion.Fear, NumuEmotion.Disgust,
        };

        void Start()
        {
            if (character == null) character = GetComponentInChildren<EmotionController>();
            if (character == null) return;
            var anim = character.GetComponent<Animator>();
            if (!string.IsNullOrEmpty(idleState) && anim.HasState(0, Animator.StringToHash(idleState))) anim.Play(idleState);
            character.Show(startEmotion, 1f, withHands);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || character == null) return;
            for (int i = 0; i < Keys.Length; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame)
                {
                    character.Hands(HandGesture.None);
                    character.Show(Keys[i], 1f, withHands);
                }
            if (kb.hKey.wasPressedThisFrame)
            {
                withHands = !withHands;
                character.Hands(withHands ? character.HandsFor(character.Current) : HandGesture.None);
            }
        }
    }
}
