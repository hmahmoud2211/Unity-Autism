using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Numu.World
{
    /// <summary>
    /// Finds the nearest interactable in range of the player and shows a large, friendly prompt button.
    /// Space / E / gamepad South or tapping the button triggers it.
    /// </summary>
    public class NumuInteractionSystem : MonoBehaviour
    {
        public Transform player;
        public CanvasGroup promptGroup;
        public TMP_Text promptLabel;
        public Button promptButton;

        NumuInteractable _current;
        NumuInteractable[] _all = Array.Empty<NumuInteractable>();
        float _refresh;

        public bool Suspended { get; set; }

        void Start()
        {
            if (promptButton != null) promptButton.onClick.AddListener(Trigger);
            SetPrompt(null);
        }

        void Update()
        {
            if ((_refresh -= Time.deltaTime) <= 0f)
            {
                _all = FindObjectsByType<NumuInteractable>();
                _refresh = 1f;
            }
            NumuInteractable best = null;
            float bestD = float.MaxValue;
            if (!Suspended && player != null)
            {
                foreach (var i in _all)
                {
                    if (i == null || !i.interactable) continue;
                    float d = Vector3.Distance(Flat(i.transform.position), Flat(player.position));
                    if (d < i.radius && d < bestD) { best = i; bestD = d; }
                }
            }
            if (best != _current) SetPrompt(best);

            if (_current != null)
            {
                var kb = Keyboard.current;
                var pad = Gamepad.current;
                if ((kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) ||
                    (pad != null && pad.buttonSouth.wasPressedThisFrame))
                {
                    Debug.Log($"[NUMU] Interaction triggered by {(pad != null && pad.buttonSouth.wasPressedThisFrame ? "gamepad " + pad.displayName : "keyboard")}");
                    Trigger();
                }
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void SetPrompt(NumuInteractable i)
        {
            _current = i;
            if (promptGroup == null) return;
            bool show = i != null;
            promptGroup.alpha = show ? 1f : 0f;
            promptGroup.interactable = show;
            promptGroup.blocksRaycasts = show;
            if (show && promptLabel != null) promptLabel.text = i.prompt;
        }

        public void Trigger()
        {
            if (_current == null || Suspended) return;
            var i = _current;
            SetPrompt(null);
            Numu.NumuAudio.Play(Numu.NumuSfx.Tap);
            i.Interact();
        }
    }
}
