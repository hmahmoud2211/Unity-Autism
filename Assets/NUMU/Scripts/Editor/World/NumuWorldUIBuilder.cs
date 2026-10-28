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
    /// <summary>
    /// Builds the in-world canvas shared by every realistic world: HUD (Map button + world chip), interaction
    /// prompt, touch joystick, subtitles, the four-card question, feedback and toast.
    /// </summary>
    public static class NumuWorldUIBuilder
    {
        public class Spec
        {
            public string canvasName = "World UI";
            public string title = "NUMU Park";
            public string subtitle = "Identify emotions";
            public Sprite chipIcon;
            public Color chipBg = NumuColor.Hex("#fde3ef");
            public Color chipInk = NumuColor.Hex("#a8186a");
            public Color accent = NumuColor.Hex("#e0589c");
            public string prompt = "Say hi";
            public string speaker = "Friend";
            public string question = "How is your friend feeling?";
        }

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

        public static NumuWorldUI Build(Transform parent, Spec spec, NumuInteractionSystem interactions, NumuPlayerController player)
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(parent, false);
            new GameObject("UI Audio", typeof(AudioSource), typeof(NumuAudio)).transform.SetParent(parent, false);

            var canvasGo = new GameObject(spec.canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = 5 };
            canvasGo.transform.SetParent(parent, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;
            var ui = canvasGo.AddComponent<NumuWorldUI>();
            Color ink = NumuColor.Hex("#3d3350");

            // Look area: drag on the right of the screen to turn the camera. First child, so every button is above it.
            var look = Rect("Look Area", root, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            look.anchorMin = new Vector2(0.4f, 0f);
            look.anchorMax = Vector2.one;
            look.offsetMin = look.offsetMax = Vector2.zero;
            Img(look, null, new Color(0, 0, 0, 0), true);
            look.gameObject.AddComponent<NumuLookArea>();
            ui.lookAreaGroup = Group(look);

            // HUD: Map button + world chip.
            var hud = Fill("HUD", root);
            ui.hudGroup = Group(hud);
            var map = Rect("Map Button", hud, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -24), new Vector2(200, 84));
            Shadow(map);
            var mapBg = Img(Fill("Bg", map), UISprite("UI_Pill"), Color.white, true);
            Img(Rect("Icon", map, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(52, 52)), NumuWorldIcons.Sprite("Glyph_Map"), NumuColor.Hex("#6a5a86"));
            Text(Rect("Label", map, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, 0), new Vector2(110, 60)), "Map", 34, ink, true, TextAlignmentOptions.MidlineLeft);
            ui.mapButton = Btn(map, mapBg);
            // View switch (third / first person).
            var viewBtn = Rect("View Button", hud, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -24), new Vector2(320, 84));
            Shadow(viewBtn);
            var viewBg = Img(Fill("Bg", viewBtn), UISprite("UI_Pill"), Color.white, true);
            Img(Rect("Icon", viewBtn, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(56, 56)), NumuWorldIcons.Sprite("Glyph_View"), NumuColor.Hex("#6a5a86"));
            ui.viewLabel = Text(Rect("Label", viewBtn, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(84, 0), new Vector2(220, 60)), "Third person", 32, ink, true, TextAlignmentOptions.MidlineLeft);
            ui.viewButton = Btn(viewBtn, viewBg);
            var chip = Rect("World Chip", hud, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(460, 84));
            Shadow(chip);
            Img(Fill("Bg", chip), UISprite("UI_Pill"), spec.chipBg);
            Img(Rect("Icon", chip, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(56, 56)), spec.chipIcon, Color.white);
            Text(Rect("Title", chip, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(86, 10), new Vector2(350, 44)), spec.title, 34, spec.chipInk, true, TextAlignmentOptions.MidlineLeft);
            Text(Rect("Sub", chip, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(88, -22), new Vector2(350, 28)), spec.subtitle, 22, spec.chipInk, false, TextAlignmentOptions.MidlineLeft);

            // Toast.
            var toast = Rect("Toast", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(900, 84));
            ui.toastGroup = Group(toast);
            Img(Fill("Bg", toast), UISprite("UI_Pill"), new Color(0.24f, 0.2f, 0.32f, 0.9f));
            ui.toastText = Text(Fill("Text", toast, 14), "", 30, Color.white);

            // Interaction prompt (driven by NumuInteractionSystem).
            var prompt = Rect("Interaction Prompt", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(520, 116));
            var promptGroup = Group(prompt);
            Shadow(prompt, 16, 0.4f);
            var promptBg = Img(Fill("Bg", prompt), UISprite("UI_Pill"), spec.accent, true);
            var handBg = Rect("IconBg", prompt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(88, 88));
            Img(handBg, UISprite("UI_Circle"), new Color(1, 1, 1, 0.25f));
            Img(Fill("Icon", handBg, 16), NumuWorldIcons.Sprite("Glyph_Hand"), Color.white);
            var promptLabel = Text(Rect("Label", prompt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(118, 0), new Vector2(380, 80)), spec.prompt, 40, Color.white, true, TextAlignmentOptions.MidlineLeft);
            interactions.promptGroup = promptGroup;
            interactions.promptLabel = promptLabel;
            interactions.promptButton = Btn(prompt, promptBg);

            // Touch joystick.
            var stick = Rect("Joystick", root, new Vector2(0, 0), new Vector2(0, 0), new Vector2(70, 70), new Vector2(260, 260));
            ui.joystickGroup = Group(stick);
            Img(Fill("Bg", stick), UISprite("UI_Circle"), new Color(1, 1, 1, 0.28f), true);
            var handle = Rect("Handle", stick, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112, 112));
            Img(handle, UISprite("UI_Circle"), new Color(1, 1, 1, 0.85f));
            var joystick = stick.gameObject.AddComponent<NumuVirtualJoystick>();
            joystick.background = stick;
            joystick.handle = handle;
            joystick.radius = 90f;
            player.joystick = joystick;

            // Dialogue subtitles.
            var dlg = Rect("Dialogue", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(1240, 210));
            ui.dialogueGroup = Group(dlg);
            Shadow(dlg, 18, 0.4f);
            var dlgBg = Img(Fill("Bg", dlg), UISprite("UI_RoundRect"), new Color(1, 1, 1, 0.97f), true);
            ui.dialogueContinue = Btn(dlg, dlgBg);
            var nameTag = Rect("Name", dlg, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(220, 64));
            Img(nameTag, UISprite("UI_Pill"), spec.accent);
            ui.speakerName = Text(Fill("Text", nameTag, 8), spec.speaker, 34, Color.white);
            ui.dialogueLine = Text(Fill("Line", dlg, 46), "", 44, ink, false, TextAlignmentOptions.MidlineLeft);
            Text(Rect("Hint", dlg, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 14), new Vector2(300, 36)), "Tap to continue", 22, new Color(ink.r, ink.g, ink.b, 0.55f), false, TextAlignmentOptions.MidlineRight);

            // Question: four picture cards (emotion faces by default; NumuWorldUI.AskChoice swaps them).
            var q = Fill("Question", root);
            ui.questionGroup = Group(q);
            Img(Fill("InputBlock", q), null, new Color(0, 0, 0, 0), true);
            var card = Rect("Card", q, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(1340, 470));
            Shadow(card, 20, 0.45f);
            Img(Fill("Bg", card), UISprite("UI_RoundRect"), NumuColor.Hex("#fff9fc"));
            ui.questionText = Text(Rect("Title", card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1200, 80)), spec.question, 56, ink);
            // Colour code after Plutchik's wheel (as in JeStiMulE): happy yellow, sad blue, angry red, fear green.
            var options = new[] { (NumuEmotion.Happy, "Happy", "#fff1b8"), (NumuEmotion.Sad, "Sad", "#d6e8ff"), (NumuEmotion.Angry, "Angry", "#ffd6d0"), (NumuEmotion.Fear, "Scared", "#d9f2d9") };
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
            var retryBg = Img(retry, UISprite("UI_Pill"), spec.chipBg, true);
            Text(Fill("Label", retry), "Try again", 38, spec.chipInk);
            ui.feedbackRetry = Btn(retry, retryBg);
            ui.positiveSprite = NumuWorldIcons.Sprite("Feedback_Star");
            ui.gentleSprite = NumuWorldIcons.Sprite("Feedback_Heart");
            return ui;
        }
    }
}
