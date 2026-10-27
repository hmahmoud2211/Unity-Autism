using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Numu.World;

namespace Numu.EditorTools.World
{
    public static partial class NumuParkBuilder
    {
        static TMP_FontAsset _font;

        static Sprite UISprite(string n) => NumuTextureKit.Sprite(n);

        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Fill(string name, Transform parent, float inset = 0f)
        {
            var rt = Rect(name, parent, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        static Image Img(RectTransform rt, Sprite s, Color c, bool raycast = false)
        {
            var i = rt.gameObject.AddComponent<Image>();
            i.sprite = s;
            i.color = c;
            i.raycastTarget = raycast;
            if (s != null && s.border != Vector4.zero) i.type = Image.Type.Sliced;
            else i.preserveAspect = s != null;
            return i;
        }

        static TextMeshProUGUI Text(RectTransform rt, string text, float size, Color c, bool bold = true, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableAutoSizing = true;
            t.fontSizeMin = size * 0.6f;
            t.fontSizeMax = size;
            return t;
        }

        static Button Btn(RectTransform rt, Graphic g)
        {
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = g;
            var cb = b.colors;
            cb.highlightedColor = new Color(0.95f, 0.95f, 0.95f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.88f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            return b;
        }

        static void Shadow(RectTransform rt, float spread = 14f, float alpha = 0.35f)
        {
            var sh = Fill("Shadow", rt, -spread);
            sh.anchoredPosition = new Vector2(0, -5);
            Img(sh, UISprite("UI_Shadow"), new Color(0.15f, 0.12f, 0.2f, alpha));
            sh.SetAsFirstSibling();
        }

        static CanvasGroup Group(RectTransform rt) => rt.gameObject.AddComponent<CanvasGroup>();

        static void BuildUI(GameplayRefs refs)
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(_root, false);
            new GameObject("UI Audio", typeof(AudioSource), typeof(NumuAudio)).transform.SetParent(_root, false);

            var canvasGo = new GameObject("Park UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = 5 };
            canvasGo.transform.SetParent(_root, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;
            var ui = canvasGo.AddComponent<NumuWorldUI>();
            Color ink = NumuColor.Hex("#3d3350");
            Color park = NumuColor.Hex("#e0589c");

            // HUD: Map button + world chip.
            var hud = Fill("HUD", root);
            ui.hudGroup = Group(hud);
            var map = Rect("Map Button", hud, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -24), new Vector2(200, 84));
            Shadow(map);
            var mapBg = Img(Fill("Bg", map), UISprite("UI_Pill"), Color.white, true);
            Img(Rect("Icon", map, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(52, 52)), NumuWorldIcons.Sprite("Glyph_Map"), NumuColor.Hex("#6a5a86"));
            Text(Rect("Label", map, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, 0), new Vector2(110, 60)), "Map", 34, ink, true, TextAlignmentOptions.MidlineLeft);
            ui.mapButton = Btn(map, mapBg);
            var chip = Rect("World Chip", hud, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(460, 84));
            Shadow(chip);
            Img(Fill("Bg", chip), UISprite("UI_Pill"), NumuColor.Hex("#fde3ef"));
            Img(Rect("Icon", chip, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(56, 56)), NumuTextureKit.Sprite("Icon_Park"), Color.white);
            Text(Rect("Title", chip, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(86, 10), new Vector2(350, 44)), "NUMU Park", 34, NumuColor.Hex("#a8186a"), true, TextAlignmentOptions.MidlineLeft);
            Text(Rect("Sub", chip, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(88, -22), new Vector2(350, 28)), "Identify emotions", 22, NumuColor.Hex("#a8186a"), false, TextAlignmentOptions.MidlineLeft);

            // Toast.
            var toast = Rect("Toast", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(900, 84));
            ui.toastGroup = Group(toast);
            Img(Fill("Bg", toast), UISprite("UI_Pill"), new Color(0.24f, 0.2f, 0.32f, 0.9f));
            ui.toastText = Text(Fill("Text", toast, 14), "", 30, Color.white);

            // Interaction prompt (driven by NumuInteractionSystem).
            var prompt = Rect("Interaction Prompt", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(520, 116));
            var promptGroup = Group(prompt);
            Shadow(prompt, 16, 0.4f);
            var promptBg = Img(Fill("Bg", prompt), UISprite("UI_Pill"), park, true);
            var handBg = Rect("IconBg", prompt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(88, 88));
            Img(handBg, UISprite("UI_Circle"), new Color(1, 1, 1, 0.25f));
            Img(Fill("Icon", handBg, 16), NumuWorldIcons.Sprite("Glyph_Hand"), Color.white);
            var promptLabel = Text(Rect("Label", prompt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(118, 0), new Vector2(380, 80)), "Say hi to Mia", 40, Color.white, true, TextAlignmentOptions.MidlineLeft);
            refs.interactions.promptGroup = promptGroup;
            refs.interactions.promptLabel = promptLabel;
            refs.interactions.promptButton = Btn(prompt, promptBg);

            // Touch joystick.
            var stick = Rect("Joystick", root, new Vector2(0, 0), new Vector2(0, 0), new Vector2(70, 70), new Vector2(260, 260));
            ui.joystickGroup = Group(stick);
            var stickBg = Img(Fill("Bg", stick), UISprite("UI_Circle"), new Color(1, 1, 1, 0.28f), true);
            var handle = Rect("Handle", stick, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112, 112));
            Img(handle, UISprite("UI_Circle"), new Color(1, 1, 1, 0.85f));
            var joystick = stick.gameObject.AddComponent<NumuVirtualJoystick>();
            joystick.background = stick;
            joystick.handle = handle;
            joystick.radius = 90f;
            refs.player.joystick = joystick;

            // Dialogue subtitles.
            var dlg = Rect("Dialogue", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(1240, 210));
            ui.dialogueGroup = Group(dlg);
            Shadow(dlg, 18, 0.4f);
            var dlgBg = Img(Fill("Bg", dlg), UISprite("UI_RoundRect"), new Color(1, 1, 1, 0.97f), true);
            ui.dialogueContinue = Btn(dlg, dlgBg);
            var nameTag = Rect("Name", dlg, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(220, 64));
            Img(nameTag, UISprite("UI_Pill"), park);
            ui.speakerName = Text(Fill("Text", nameTag, 8), "Mia", 34, Color.white);
            ui.dialogueLine = Text(Fill("Line", dlg, 46), "", 44, ink, false, TextAlignmentOptions.MidlineLeft);
            Text(Rect("Hint", dlg, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 14), new Vector2(300, 36)), "Tap to continue", 22, new Color(ink.r, ink.g, ink.b, 0.55f), false, TextAlignmentOptions.MidlineRight);

            // Emotion question.
            var q = Fill("Question", root);
            ui.questionGroup = Group(q);
            Img(Fill("InputBlock", q), null, new Color(0, 0, 0, 0), true);
            var card = Rect("Card", q, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(1340, 470));
            Shadow(card, 20, 0.45f);
            Img(Fill("Bg", card), UISprite("UI_RoundRect"), NumuColor.Hex("#fff9fc"));
            ui.questionText = Text(Rect("Title", card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1200, 80)), "How is Mia feeling?", 56, ink);
            var options = new[] { (NumuEmotion.Happy, "Happy", "#fff1b8"), (NumuEmotion.Sad, "Sad", "#dcecff"), (NumuEmotion.Angry, "Angry", "#ffdcd6"), (NumuEmotion.Fear, "Scared", "#e9e2ff") };
            ui.choices = new List<NumuWorldUI.Choice>();
            for (int i = 0; i < options.Length; i++)
            {
                var (emotion, label, color) = options[i];
                var b = Rect(label, card, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-468 + i * 312, 30), new Vector2(280, 330));
                var bbg = Img(Fill("Bg", b), UISprite("UI_RoundRect"), NumuColor.Hex(color), true);
                var icon = Img(Rect("Face", b, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(190, 190)), NumuWorldIcons.Sprite($"Emotion_{emotion}"), Color.white);
                var lbl = Text(Rect("Label", b, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(250, 70)), label, 46, ink);
                ui.choices.Add(new NumuWorldUI.Choice { emotion = emotion, button = Btn(b, bbg), label = lbl, icon = icon });
            }

            // Feedback.
            var fb = Fill("Feedback", root);
            ui.feedbackGroup = Group(fb);
            Img(Fill("InputBlock", fb), null, new Color(0, 0, 0, 0), true);
            var fcard = Rect("Card", fb, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(1100, 470));
            Shadow(fcard, 20, 0.45f);
            Img(Fill("Bg", fcard), UISprite("UI_RoundRect"), NumuColor.Hex("#f4fbf2"));
            ui.feedbackIcon = Img(Rect("Icon", fcard, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -40), new Vector2(150, 150)), NumuWorldIcons.Sprite("Feedback_Star"), Color.white);
            ui.feedbackTitle = Text(Rect("Title", fcard, new Vector2(0, 1), new Vector2(0, 1), new Vector2(230, -40), new Vector2(820, 80)), "", 56, NumuColor.Hex("#2b7a3c"), true, TextAlignmentOptions.MidlineLeft);
            ui.feedbackMessage = Text(Rect("Message", fcard, new Vector2(0, 1), new Vector2(0, 1), new Vector2(230, -130), new Vector2(820, 170)), "", 34, ink, false, TextAlignmentOptions.TopLeft);
            var cont = Rect("Continue", fcard, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(170, 40), new Vector2(300, 96));
            var contBg = Img(cont, UISprite("UI_Pill"), NumuColor.Hex("#4fae62"), true);
            Text(Fill("Label", cont), "Continue", 38, Color.white);
            ui.feedbackContinue = Btn(cont, contBg);
            var retry = Rect("Try Again", fcard, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-170, 40), new Vector2(300, 96));
            var retryBg = Img(retry, UISprite("UI_Pill"), NumuColor.Hex("#fde3ef"), true);
            Text(Fill("Label", retry), "Try again", 38, NumuColor.Hex("#a8186a"));
            ui.feedbackRetry = Btn(retry, retryBg);
            ui.positiveSprite = NumuWorldIcons.Sprite("Feedback_Star");
            ui.gentleSprite = NumuWorldIcons.Sprite("Feedback_Heart");

            refs.scenario.ui = ui;
            _log.AppendLine("UI: HUD (Map + world chip), interaction prompt, touch joystick, subtitles, emotion question (4 faces), feedback, toast.");
        }
    }
}
