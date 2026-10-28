using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Numu.World
{
    /// <summary>
    /// In-world interface: speech subtitles, the emotion question (or a picture-choice question), gentle feedback and the HUD.
    /// Layouts are built by the scene builder; this component only drives them.
    /// </summary>
    public class NumuWorldUI : MonoBehaviour
    {
        [Serializable]
        public class Choice
        {
            public NumuEmotion emotion;
            public Button button;
            public TMP_Text label;
            public Image icon;
        }

        [Header("Dialogue")]
        public CanvasGroup dialogueGroup;
        public TMP_Text speakerName;
        public TMP_Text dialogueLine;
        public Button dialogueContinue;

        [Header("Question")]
        public CanvasGroup questionGroup;
        public TMP_Text questionText;
        public List<Choice> choices = new List<Choice>();

        [Header("Feedback")]
        public CanvasGroup feedbackGroup;
        public Image feedbackIcon;
        public TMP_Text feedbackTitle;
        public TMP_Text feedbackMessage;
        public Button feedbackContinue;
        public Button feedbackRetry;
        public Sprite positiveSprite;
        public Sprite gentleSprite;

        [Header("HUD")]
        public CanvasGroup hudGroup;
        public Button mapButton;
        public CanvasGroup toastGroup;
        public TMP_Text toastText;
        public CanvasGroup joystickGroup;
        [Tooltip("Switches between third- and first-person view.")]
        public Button viewButton;
        public TMP_Text viewLabel;
        public CanvasGroup lookAreaGroup;

        /// <summary>A picture option for <see cref="AskChoice"/>; it temporarily replaces an emotion card.</summary>
        public struct Option
        {
            public string label;
            public Sprite icon;
            public Color color;
        }

        bool _continuePressed;
        NumuEmotion? _answer;
        int _picked = -1;
        (string label, Sprite icon, Color color)[] _emotionLooks;
        bool? _retryChoice;
        float _toastTimer;
        readonly Dictionary<CanvasGroup, float> _targets = new Dictionary<CanvasGroup, float>();

        void Awake()
        {
            foreach (var g in new[] { dialogueGroup, questionGroup, feedbackGroup, toastGroup }) Show(g, false, true);
            Show(hudGroup, true, true);
            dialogueContinue.onClick.AddListener(() => _continuePressed = true);
            feedbackContinue.onClick.AddListener(() => _retryChoice = false);
            feedbackRetry.onClick.AddListener(() => _retryChoice = true);
            _emotionLooks = new (string, Sprite, Color)[choices.Count];
            for (int i = 0; i < choices.Count; i++)
            {
                var c = choices[i];
                var e = c.emotion;
                int index = i;
                c.button.onClick.AddListener(() =>
                {
                    _answer = e;
                    _picked = index;
                });
                _emotionLooks[i] = (c.label.text, c.icon.sprite, c.button.targetGraphic.color);
            }
            mapButton.onClick.AddListener(() =>
            {
                Debug.Log("[NUMU] Map button pressed: leaving the park");
                Numu.NumuAudio.Play(Numu.NumuSfx.Close);
                NumuBridge.ExitToHost();
            });
            // Touch joystick only where touch input exists (always shown in the Editor for testing).
            _joystickAllowed = Application.isEditor || Touchscreen.current != null;
            Show(joystickGroup, _joystickAllowed, true);
            Show(lookAreaGroup, true, true);
            _view = Camera.main != null ? Camera.main.GetComponent<NumuFollowCamera>() : null;
            if (viewButton != null && _view != null)
            {
                viewButton.onClick.AddListener(() =>
                {
                    Numu.NumuAudio.Play(Numu.NumuSfx.Tap);
                    _view.ToggleView();
                });
                _view.ViewChanged += UpdateViewLabel;
                UpdateViewLabel(_view.CurrentView);
            }
        }

        NumuFollowCamera _view;

        void OnDestroy()
        {
            if (_view != null) _view.ViewChanged -= UpdateViewLabel;
        }

        void UpdateViewLabel(NumuFollowCamera.View v)
        {
            if (viewLabel != null) viewLabel.text = v == NumuFollowCamera.View.FirstPerson ? "First person" : "Third person";
        }

        public void Show(CanvasGroup g, bool visible, bool instant = false)
        {
            if (g == null) return;
            _targets[g] = visible ? 1f : 0f;
            g.interactable = visible;
            g.blocksRaycasts = visible;
            if (instant || Numu.NumuSettings.ReducedMotion) g.alpha = visible ? 1f : 0f;
        }

        void Update()
        {
            float step = Time.unscaledDeltaTime * 4f;
            foreach (var kv in _targets) kv.Key.alpha = Mathf.MoveTowards(kv.Key.alpha, kv.Value, step);
            if (_toastTimer > 0f && (_toastTimer -= Time.unscaledDeltaTime) <= 0f) Show(toastGroup, false);
            var kb = Keyboard.current;
            if (kb != null && dialogueGroup.interactable && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) _continuePressed = true;
        }

        public void Toast(string message, float seconds = 3f)
        {
            toastText.text = message;
            _toastTimer = seconds;
            Show(toastGroup, true);
        }

        bool _joystickAllowed;

        /// <summary>HUD and the touch stick hide together during conversations, so nothing competes with the question.</summary>
        public void SetHudVisible(bool v)
        {
            Show(hudGroup, v);
            Show(joystickGroup, v && _joystickAllowed);
            Show(lookAreaGroup, v);
        }

        /// <summary>Shows a subtitle line; continues on tap/Space or after a reading-time based delay.</summary>
        public IEnumerator Say(string speaker, string line, float autoAdvance = -1f)
        {
            speakerName.text = speaker;
            dialogueLine.text = line;
            _continuePressed = false;
            Show(dialogueGroup, true);
            float wait = autoAdvance > 0f ? autoAdvance : Mathf.Clamp(1.6f + line.Length * 0.055f, 2.5f, 7f);
            float t = 0f;
            // Ignore taps during the first moment so a double tap can't skip a line unread.
            while (!(_continuePressed && t > 0.6f) && t < wait)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Show(dialogueGroup, false);
            yield return new WaitForSecondsRealtime(0.25f);
        }

        void SetCard(int i, string label, Sprite icon, Color color)
        {
            choices[i].label.text = label;
            choices[i].icon.sprite = icon;
            choices[i].button.targetGraphic.color = color;
        }

        /// <summary>Asks a question and reports the chosen emotion plus the response time in milliseconds.</summary>
        public IEnumerator Ask(string question, Action<NumuEmotion, long> onAnswer)
        {
            for (int i = 0; i < choices.Count; i++)
            {
                choices[i].button.gameObject.SetActive(true);
                SetCard(i, _emotionLooks[i].label, _emotionLooks[i].icon, _emotionLooks[i].color);
            }
            questionText.text = question;
            _answer = null;
            Show(questionGroup, true);
            float start = Time.realtimeSinceStartup;
            while (_answer == null) yield return null;
            long ms = (long)((Time.realtimeSinceStartup - start) * 1000f);
            Show(questionGroup, false);
            onAnswer(_answer.Value, ms);
        }

        /// <summary>
        /// Asks a picture-choice question (e.g. "What could you do?") on the same cards as the emotion question.
        /// Reports the chosen option index and the response time in milliseconds.
        /// </summary>
        public IEnumerator AskChoice(string question, IList<Option> options, Action<int, long> onAnswer)
        {
            for (int i = 0; i < choices.Count; i++)
            {
                bool used = i < options.Count;
                choices[i].button.gameObject.SetActive(used);
                if (used) SetCard(i, options[i].label, options[i].icon, options[i].color);
            }
            questionText.text = question;
            _picked = -1;
            Show(questionGroup, true);
            float start = Time.realtimeSinceStartup;
            while (_picked < 0) yield return null;
            long ms = (long)((Time.realtimeSinceStartup - start) * 1000f);
            Show(questionGroup, false);
            onAnswer(_picked, ms);
        }

        public IEnumerator Feedback(bool positive, string title, string message, bool offerRetry, Action<bool> onDone)
        {
            feedbackIcon.sprite = positive ? positiveSprite : gentleSprite;
            feedbackTitle.text = title;
            feedbackMessage.text = message;
            feedbackRetry.gameObject.SetActive(offerRetry);
            _retryChoice = null;
            Show(feedbackGroup, true);
            while (_retryChoice == null) yield return null;
            Show(feedbackGroup, false);
            onDone(_retryChoice.Value);
        }
    }
}
