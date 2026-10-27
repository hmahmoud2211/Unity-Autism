using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Numu.EditorTools
{
    /// <summary>Canvas + TextMeshPro interface: the four island cards and all interaction panels.</summary>
    public static partial class NumuWorldBuilder
    {
        public const string PreviewDir = TexturesDir + "/Previews";
        static readonly Dictionary<string, Sprite> _portraits = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> _vignettes = new Dictionary<string, Sprite>();

        static Sprite S(string name) => NumuTextureKit.Sprite(name);

        // ------------------------------------------------------------------ preview renders

        static void RenderPreviews()
        {
            Directory.CreateDirectory(PreviewDir);
            _portraits.Clear();
            _vignettes.Clear();
            var mascots = new Dictionary<string, string> { { "home", "Mascot_Fox" }, { "school", "Mascot_Penguin" }, { "park", "Mascot_Bunny" }, { "supermarket", "Mascot_Bear" } };
            foreach (var kv in mascots)
            {
                var t = _islands[kv.Key].transform.Find("Mascot");
                if (t == null || t.childCount == 0) continue;
                var mascot = t.GetChild(0).gameObject;
                var b = WorldBounds(mascot);
                Vector3 target = b.center + Vector3.up * b.extents.y * 0.12f;
                Vector3 dir = Quaternion.Euler(8f, -14f, 0f) * Vector3.back;
                float dist = b.extents.y * 1.12f / Mathf.Tan(14f * Mathf.Deg2Rad);
                _portraits[kv.Key] = RenderToSprite($"Portrait_{kv.Key}", target + dir * dist, target, 28f, 512, 512, mascot);
            }
            // Island vignettes (close-up renders of each island's landmark).
            void Vignette(string id, string landmarkPath, float yaw, float pitch, float distScale, Vector3 offset)
            {
                var lm = _islands[id].transform.Find(landmarkPath);
                if (lm == null || lm.childCount == 0) return;
                var b = WorldBounds(lm.GetChild(0).gameObject);
                Vector3 target = b.center + offset;
                Vector3 dir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
                float dist = b.extents.magnitude * distScale;
                _vignettes[id] = RenderToSprite($"Vignette_{id}", target + dir * dist, target, 30f, 600, 528, null);
            }
            Vignette("home", "Buildings", -22f, 22f, 2.3f, Vector3.down * 1.5f);
            Vignette("school", "Buildings", -22f, 28f, 2.5f, new Vector3(3.5f, -2.2f, 0f));
            Vignette("park", "Props", -10f, 42f, 2.1f, Vector3.down * 0.5f);
            Vignette("supermarket", "Buildings", 18f, 18f, 1.5f, new Vector3(-2f, -1.5f, -3f));
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(PreviewDir, "*.png"))
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                imp.textureType = TextureImporterType.Sprite;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            foreach (var id in new List<string>(_portraits.Keys)) _portraits[id] = AssetDatabase.LoadAssetAtPath<Sprite>($"{PreviewDir}/Portrait_{id}.png");
            foreach (var id in new List<string>(_vignettes.Keys)) _vignettes[id] = AssetDatabase.LoadAssetAtPath<Sprite>($"{PreviewDir}/Vignette_{id}.png");
            _log.AppendLine($"Previews: {_portraits.Count} portraits, {_vignettes.Count} vignettes rendered from the 3D scene.");
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.one);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static Sprite RenderToSprite(string name, Vector3 camPos, Vector3 target, float fov, int w, int h, GameObject isolate)
        {
            var camGo = new GameObject("PreviewCamera");
            var cam = camGo.AddComponent<Camera>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(target - camPos));
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 600f;
            cam.aspect = w / (float)h;
            var layers = new Dictionary<GameObject, int>();
            if (isolate != null)
            {
                foreach (var t in isolate.GetComponentsInChildren<Transform>(true))
                {
                    layers[t.gameObject] = t.gameObject.layer;
                    t.gameObject.layer = 31;
                }
                cam.cullingMask = 1 << 31;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                data.renderPostProcessing = false;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.cullingMask = ~(1 << 5);
                data.renderPostProcessing = true;
            }
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes($"{PreviewDir}/{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            foreach (var kv in layers) kv.Key.layer = kv.Value;
            return null;
        }

        // ------------------------------------------------------------------ UI primitives

        static RectTransform R(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform TopLeft(string name, Transform parent, float x, float y, float w, float h) =>
            R(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));

        static RectTransform Stretch(string name, Transform parent, float inset = 0f)
        {
            var rt = R(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        static Image Img(RectTransform rt, Sprite sprite, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            img.preserveAspect = sprite != null && sprite.border == Vector4.zero && sprite.name.StartsWith("Icon");
            return img;
        }

        static TextMeshProUGUI Txt(RectTransform rt, string text, float size, Color color, bool bold = true, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, float minSize = 0f)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            if (minSize > 0f)
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = minSize;
                t.fontSizeMax = size;
            }
            return t;
        }

        static Button Btn(RectTransform rt, Graphic target)
        {
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = target;
            var c = b.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(0.96f, 0.96f, 0.96f);
            c.pressedColor = new Color(0.85f, 0.85f, 0.88f);
            c.selectedColor = Color.white;
            c.disabledColor = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            c.fadeDuration = 0.08f;
            b.colors = c;
            return b;
        }

        static CanvasGroup Group(RectTransform rt)
        {
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            return g;
        }

        /// <summary>Rounded card: soft shadow, coloured edge and fill.</summary>
        static Image Card(RectTransform rt, Color fill, Color edge, float edgeWidth = 3f, bool shadow = true)
        {
            if (shadow)
            {
                var sh = Stretch("Shadow", rt, -14f);
                sh.anchoredPosition = new Vector2(0, -5);
                Img(sh, S("UI_Shadow"), new Color(0.45f, 0.25f, 0.35f, 0.55f));
            }
            Img(Stretch("Edge", rt), S("UI_RoundRect"), edge);
            return Img(Stretch("Fill", rt, edgeWidth), S("UI_RoundRect"), fill);
        }

        static Button CircleButton(string name, Transform parent, Vector2 aMin, Vector2 pivot, Vector2 pos, float size, Sprite icon, Color bg, Color iconColor)
        {
            var rt = R(name, parent, aMin, aMin, pivot, pos, new Vector2(size, size));
            var sh = Stretch("Shadow", rt, -8f);
            sh.anchoredPosition = new Vector2(0, -3);
            Img(sh, S("UI_Shadow"), new Color(0.3f, 0.2f, 0.3f, 0.35f));
            var bgImg = Img(Stretch("Bg", rt), S("UI_Circle"), bg, true);
            Img(Stretch("Icon", rt, size * 0.24f), icon, iconColor);
            return Btn(rt, bgImg);
        }

        // ------------------------------------------------------------------ build UI

        static void BuildUI()
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            _cam.gameObject.AddComponent<PhysicsRaycaster>();
            var audioGo = new GameObject("UI Audio", typeof(AudioSource), typeof(NumuAudio));

            var canvasGo = new GameObject("NUMU UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            var map = canvasGo.AddComponent<NumuMapController>();
            map.rig = _rig;
            var isl = new List<NumuIsland>(_islands.Values);
            map.islands = isl.ToArray();
            map.lockSprite = S("Icon_Lock");
            map.starSprite = S("Icon_Star");
            map.soundOnSprite = S("Icon_SoundOn");
            map.soundOffSprite = S("Icon_SoundOff");
            var portraits = new List<Sprite>();
            var icons = new List<Sprite>();
            foreach (var info in NumuData.Islands)
            {
                portraits.Add(_portraits.TryGetValue(info.Id, out var p) ? p : null);
                icons.Add(S($"Icon_{info.Name}"));
            }
            icons.Add(S("Icon_Hub"));
            map.portraits = portraits.ToArray();
            map.islandIcons = icons.ToArray();

            BuildCards(root, map);
            BuildFocusBar(root, map);
            BuildStrip(root, map);
            BuildHubPanel(root, map);
            BuildLevelPanel(root, map);
            BuildSettingsPanel(root, map);
            BuildToast(root, map);
            EditorUtility.SetDirty(map);
            _log.AppendLine("UI: canvas, 4 island cards, focus bar, level strip, level/settings/hub panels.");
        }

        static void BuildCards(RectTransform root, NumuMapController map)
        {
            var panel = R("Island Cards", root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(-24, 270));
            map.cardsPanel = panel;
            map.cardsGroup = Group(panel);
            var layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            var cards = new List<NumuMapController.IslandCard>();
            foreach (var info in NumuData.Islands)
            {
                var card = R($"Card_{info.Name}", panel, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380, 270));
                Card(card, info.Panel, info.PanelEdge);
                // Soft top glow on the card.
                var glow = R("Glow", card, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(-8, 90));
                Img(glow, S("UI_RoundRect"), new Color(1, 1, 1, 0.35f));

                var c = new NumuMapController.IslandCard { islandId = info.Id, root = card };
                var header = TopLeft("Header", card, 0, 0, 380, 98);
                header.anchorMax = new Vector2(1, 1);
                header.sizeDelta = new Vector2(0, 98);
                var headerImg = Img(header, null, new Color(1, 1, 1, 0), true);
                c.header = Btn(header, headerImg);

                // Island preview (rendered from the 3D world) in a rounded frame.
                var frame = R("Preview", card, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(12, 12), new Vector2(150, 132));
                Img(frame, S("UI_RoundRectSmall"), Color.white);
                var mask = Stretch("Mask", frame, 4f);
                Img(mask, S("UI_RoundRectSmall"), Color.white);
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var pic = Stretch("Image", mask);
                var picImg = Img(pic, _vignettes.TryGetValue(info.Id, out var v) ? v : null, Color.white);
                picImg.preserveAspect = false;

                var portrait = TopLeft("Portrait", card, -8, -38, 158, 158);
                var pImg = Img(portrait, _portraits.TryGetValue(info.Id, out var po) ? po : null, Color.white);
                pImg.preserveAspect = true;

                var icon = TopLeft("Icon", card, 164, 14, 42, 42);
                Img(icon, S($"Icon_{info.Name}"), Color.white).preserveAspect = true;
                var title = TopLeft("Title", card, 210, 8, 160, 52);
                var titleText = Txt(title, info.Name, 36, info.Title, true, TextAlignmentOptions.MidlineLeft, 20);
                titleText.textWrappingMode = TextWrappingModes.NoWrap;
                var sub = TopLeft("Subtitle", card, 164, 58, 212, 42);
                string subtitle = info.Id == "park" ? "Social Interaction +\nEmotion Recognition" : info.Subtitle;
                var subText = Txt(sub, subtitle, 16.5f, info.Title, true, TextAlignmentOptions.TopLeft, 11);
                if (info.Id != "park") subText.textWrappingMode = TextWrappingModes.NoWrap;

                var rows = new List<Button>();
                var stateIcons = new List<Image>();
                for (int i = 0; i < 5; i++)
                {
                    var row = TopLeft($"Level {i + 1}", card, 164, 104 + i * 32.5f, 206, 29);
                    var bg = Img(row, S("UI_Pill"), new Color(1, 1, 1, 0.85f), true);
                    var pill = R("Pill", row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(3, 0), new Vector2(64, 23));
                    Img(pill, S("UI_Pill"), info.Theme);
                    Txt(Stretch("Text", pill), $"Level {i + 1}", 12.5f, Color.white, true, TextAlignmentOptions.Center, 9);
                    var label = R("Label", row, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(74, 0), new Vector2(-96, 0));
                    label.offsetMin = new Vector2(74, 0);
                    label.offsetMax = new Vector2(-22, 0);
                    var lt = Txt(label, info.Levels[i], 13.5f, NumuColor.Shade(info.Title, 0.9f), false, TextAlignmentOptions.MidlineLeft, 8.5f);
                    lt.textWrappingMode = TextWrappingModes.NoWrap;
                    var st = R("State", row, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-5, 0), new Vector2(17, 17));
                    var sti = Img(st, S("Icon_Lock"), Color.white);
                    sti.preserveAspect = true;
                    rows.Add(Btn(row, bg));
                    stateIcons.Add(sti);
                }
                c.levelRows = rows.ToArray();
                c.levelStateIcons = stateIcons.ToArray();
                cards.Add(c);
            }
            map.cards = cards.ToArray();
        }

        static void BuildFocusBar(RectTransform root, NumuMapController map)
        {
            // Settings is always available (top-right).
            map.settingsButton = CircleButton("Settings Button", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -20), 58, S("Icon_Settings"), new Color(1, 1, 1, 0.92f), NumuColor.Hex("#6a5a86"));

            var bar = R("Focus Bar", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(700, 120));
            map.focusBarGroup = Group(bar);
            map.backButton = CircleButton("Back", bar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -20), 66, S("Icon_Back"), NumuColor.Hex("#ffffff"), NumuColor.Hex("#6a5a86"));
            map.homeButton = CircleButton("Home", bar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(98, -20), 66, S("Icon_HomeButton"), NumuColor.Hex("#ffffff"), NumuColor.Hex("#6a5a86"));
            var chip = TopLeft("Island Chip", bar, 176, 16, 430, 76);
            var sh = Stretch("Shadow", chip, -12f);
            sh.anchoredPosition = new Vector2(0, -4);
            Img(sh, S("UI_Shadow"), new Color(0.4f, 0.25f, 0.35f, 0.45f));
            map.focusChipEdge = Img(Stretch("Edge", chip), S("UI_RoundRect"), Color.white);
            map.focusChip = Img(Stretch("Fill", chip, 3f), S("UI_RoundRect"), Color.white);
            map.focusIcon = Img(TopLeft("Icon", chip, 12, 10, 56, 56), S("Icon_Home"), Color.white);
            map.focusIcon.preserveAspect = true;
            map.focusTitle = Txt(TopLeft("Title", chip, 78, 4, 340, 42), "Home", 32, Color.black, true, TextAlignmentOptions.MidlineLeft, 20);
            map.focusSubtitle = Txt(TopLeft("Subtitle", chip, 80, 44, 340, 26), "", 16, Color.black, true, TextAlignmentOptions.MidlineLeft, 10);
        }

        static void BuildStrip(RectTransform root, NumuMapController map)
        {
            var strip = R("Level Strip", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(1360, 68));
            map.stripGroup = Group(strip);
            var layout = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            var buttons = new List<Button>();
            var pills = new List<Image>();
            var nums = new List<TMP_Text>();
            var labels = new List<TMP_Text>();
            var states = new List<Image>();
            for (int i = 0; i < 5; i++)
            {
                var b = R($"Level {i + 1}", strip, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 68));
                var sh = Stretch("Shadow", b, -10f);
                sh.anchoredPosition = new Vector2(0, -4);
                Img(sh, S("UI_Shadow"), new Color(0.4f, 0.25f, 0.35f, 0.4f));
                var bg = Img(Stretch("Bg", b), S("UI_Pill"), new Color(1, 1, 1, 0.95f), true);
                var pill = R("Number", b, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(52, 52));
                pills.Add(Img(pill, S("UI_Circle"), Color.gray));
                nums.Add(Txt(Stretch("Text", pill), (i + 1).ToString(), 28, Color.white, true, TextAlignmentOptions.Center));
                var label = R("Label", b, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
                label.offsetMin = new Vector2(68, 4);
                label.offsetMax = new Vector2(-30, -4);
                labels.Add(Txt(label, "", 17, Color.black, true, TextAlignmentOptions.MidlineLeft, 11));
                var st = R("State", b, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(22, 22));
                var sti = Img(st, S("Icon_Lock"), Color.white);
                sti.preserveAspect = true;
                states.Add(sti);
                buttons.Add(Btn(b, bg));
            }
            map.stripButtons = buttons.ToArray();
            map.stripPills = pills.ToArray();
            map.stripNumbers = nums.ToArray();
            map.stripLabels = labels.ToArray();
            map.stripStateIcons = states.ToArray();
        }

        static void BuildHubPanel(RectTransform root, NumuMapController map)
        {
            var p = R("Hub Panel", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(980, 170));
            map.hubGroup = Group(p);
            Card(p, NumuColor.Hex("#e6f7f1"), NumuColor.Hex("#a7dcc9"));
            Txt(TopLeft("Title", p, 28, 14, 600, 44), "Welcome to NUMU!", 32, NumuColor.Hex("#1d6e5e"), true);
            Txt(TopLeft("Subtitle", p, 30, 54, 600, 28), "Choose an island to explore", 18, NumuColor.Hex("#2f8a76"), true);
            var buttons = new List<Button>();
            for (int i = 0; i < NumuData.Islands.Length; i++)
            {
                var info = NumuData.Islands[i];
                var b = TopLeft(info.Name, p, 28 + i * 232, 92, 220, 60);
                var bg = Img(b, S("UI_Pill"), info.Theme, true);
                var ic = R("Icon", b, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(44, 44));
                Img(ic, S($"Icon_{info.Name}"), Color.white).preserveAspect = true;
                var lt = R("Label", b, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
                lt.offsetMin = new Vector2(60, 0);
                lt.offsetMax = new Vector2(-10, 0);
                Txt(lt, info.Name, 22, Color.white, true, TextAlignmentOptions.MidlineLeft, 14);
                buttons.Add(Btn(b, bg));
            }
            map.hubIslandButtons = buttons.ToArray();
            map.hubClose = CircleButton("Close", p, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -14), 46, S("Icon_Back"), NumuColor.Hex("#ffffff"), NumuColor.Hex("#2f8a76"));
        }

        static void BuildLevelPanel(RectTransform root, NumuMapController map)
        {
            var layer = Stretch("Level Panel", root);
            map.levelGroup = Group(layer);
            var backdrop = Stretch("Backdrop", layer);
            map.levelBackdrop = Btn(backdrop, Img(backdrop, null, new Color(0.18f, 0.12f, 0.22f, 0.38f), true));
            var p = R("Card", layer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(700, 420));
            map.levelCard = Card(p, Color.white, new Color(1, 1, 1, 0.9f), 4f);
            var header = TopLeft("Header", p, 12, 12, 676, 70);
            map.levelHeader = Img(header, S("UI_RoundRect"), Color.green);
            map.levelPill = Txt(TopLeft("Level", header, 24, 6, 200, 58), "Level 1", 34, Color.white, true);
            map.levelIsland = Txt(R("Island", header, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(420, 40)), "", 18, Color.white, true, TextAlignmentOptions.MidlineRight, 12);

            var portraitFrame = TopLeft("Portrait", p, 26, 96, 210, 210);
            Img(portraitFrame, S("UI_Circle"), new Color(1, 1, 1, 0.75f));
            map.levelPortrait = Img(Stretch("Image", portraitFrame, 6f), null, Color.white);
            map.levelPortrait.preserveAspect = true;

            map.levelTitle = Txt(TopLeft("Title", p, 258, 100, 420, 104), "Morning routine", 40, Color.black, true, TextAlignmentOptions.MidlineLeft, 24);
            map.levelStatusIcon = Img(TopLeft("Status Icon", p, 260, 214, 30, 30), S("Icon_Star"), Color.white);
            map.levelStatusIcon.preserveAspect = true;
            map.levelStatus = Txt(TopLeft("Status", p, 298, 206, 380, 64), "Ready to play!", 19, NumuColor.Hex("#4a3d5c"), true, TextAlignmentOptions.MidlineLeft, 12);

            var play = TopLeft("Play", p, 258, 300, 200, 70);
            map.playButtonBg = Img(play, S("UI_Pill"), Color.green, true);
            map.playButton = Btn(play, map.playButtonBg);
            Img(R("Icon", play, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(34, 34)), S("Icon_Play"), Color.white).preserveAspect = true;
            var pl = R("Label", play, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            pl.offsetMin = new Vector2(56, 0);
            pl.offsetMax = new Vector2(-12, 0);
            map.playLabel = Txt(pl, "Play", 26, Color.white, true, TextAlignmentOptions.Midline, 14);
            map.playLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var done = TopLeft("Mark Done", p, 472, 300, 206, 70);
            var doneBg = Img(done, S("UI_Pill"), NumuColor.Hex("#fff6d9"), true);
            map.completeButton = Btn(done, doneBg);
            Img(R("Star", done, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(30, 30)), S("Icon_Star"), Color.white).preserveAspect = true;
            var dl = R("Label", done, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            dl.offsetMin = new Vector2(52, 0);
            dl.offsetMax = new Vector2(-10, 0);
            Txt(dl, "Mark as done", 19, NumuColor.Hex("#8a5a12"), true, TextAlignmentOptions.Midline, 12);

            var back = TopLeft("Back", p, 40, 322, 180, 56);
            var backBg = Img(back, S("UI_Pill"), NumuColor.Hex("#eee9f3"), true);
            map.levelCloseButton = Btn(back, backBg);
            Img(R("Icon", back, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(30, 30)), S("Icon_Back"), NumuColor.Hex("#6a5a86")).preserveAspect = true;
            var bl = R("Label", back, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            bl.offsetMin = new Vector2(44, 0);
            bl.offsetMax = new Vector2(-8, 0);
            Txt(bl, "Back", 22, NumuColor.Hex("#6a5a86"), true, TextAlignmentOptions.Midline);
        }

        static void BuildSettingsPanel(RectTransform root, NumuMapController map)
        {
            var layer = Stretch("Settings Panel", root);
            map.settingsGroup = Group(layer);
            var backdrop = Stretch("Backdrop", layer);
            map.settingsBackdrop = Btn(backdrop, Img(backdrop, null, new Color(0.18f, 0.12f, 0.22f, 0.38f), true));
            var p = R("Card", layer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(560, 400));
            Card(p, NumuColor.Hex("#f6f1fb"), NumuColor.Hex("#ddd0ec"), 4f);
            Txt(TopLeft("Title", p, 32, 20, 400, 50), "Settings", 36, NumuColor.Hex("#4a3d5c"), true);

            (Button btn, Image bg, TMP_Text label, RectTransform knob) Toggle(string name, string title, string desc, float y, Sprite icon, out Image iconImg)
            {
                var row = TopLeft(name, p, 24, y, 512, 92);
                Img(row, S("UI_RoundRect"), new Color(1, 1, 1, 0.9f));
                iconImg = Img(R("Icon", row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(44, 44)), icon, NumuColor.Hex("#6a5a86"));
                iconImg.preserveAspect = true;
                Txt(TopLeft("Title", row, 76, 14, 300, 34), title, 24, NumuColor.Hex("#4a3d5c"), true);
                Txt(TopLeft("Desc", row, 76, 48, 300, 30), desc, 15, NumuColor.Hex("#7a6d8c"), false, TextAlignmentOptions.MidlineLeft, 10);
                var track = R("Toggle", row, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(96, 48));
                var tbg = Img(track, S("UI_Pill"), Color.gray, true);
                var knob = R("Knob", track, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
                Img(knob, S("UI_Circle"), Color.white);
                var lbl = Txt(R("State", row, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-124, 0), new Vector2(50, 30)), "Off", 17, NumuColor.Hex("#6a5a86"), true, TextAlignmentOptions.MidlineRight);
                return (Btn(track, tbg), tbg, lbl, knob);
            }

            var m = Toggle("Reduced Motion", "Reduced motion", "Calmer camera and gentler animations", 82, S("Icon_Motion"), out _);
            map.motionToggle = m.btn; map.motionBg = m.bg; map.motionLabel = m.label; map.motionKnob = m.knob;
            var s = Toggle("Sound", "Sound", "Soft taps and chimes", 186, S("Icon_SoundOn"), out var soundIcon);
            map.soundToggle = s.btn; map.soundBg = s.bg; map.soundLabel = s.label; map.soundKnob = s.knob; map.soundIcon = soundIcon;

            var reset = TopLeft("Reset", p, 24, 300, 250, 64);
            var rbg = Img(reset, S("UI_Pill"), NumuColor.Hex("#fde3e3"), true);
            map.resetButton = Btn(reset, rbg);
            Txt(Stretch("Label", reset), "Reset progress", 20, NumuColor.Hex("#b04a4a"), true, TextAlignmentOptions.Center);
            var close = TopLeft("Close", p, 290, 300, 246, 64);
            var cbg = Img(close, S("UI_Pill"), NumuColor.Hex("#7b62b0"), true);
            map.settingsClose = Btn(close, cbg);
            Txt(Stretch("Label", close), "Done", 22, Color.white, true, TextAlignmentOptions.Center);
        }

        static void BuildToast(RectTransform root, NumuMapController map)
        {
            var t = R("Toast", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(760, 64));
            map.toastGroup = Group(t);
            Img(t, S("UI_Pill"), new Color(0.27f, 0.2f, 0.35f, 0.92f));
            map.toastText = Txt(Stretch("Text", t, 10f), "", 22, Color.white, true, TextAlignmentOptions.Center, 13);
        }

        /// <summary>Clickable wiring for the 3D world (terrain, mascots, signs, markers).</summary>
        static void WireClickables()
        {
            int n = 0;
            foreach (var link in Object.FindObjectsByType<NumuIslandLink>(FindObjectsInactive.Include))
            {
                if (link.GetComponent<NumuClickable>() == null) { link.gameObject.AddComponent<NumuClickable>(); n++; }
            }
            foreach (var m in Object.FindObjectsByType<NumuLevelMarker>(FindObjectsInactive.Include))
            {
                if (m.GetComponent<NumuClickable>() == null) { m.gameObject.AddComponent<NumuClickable>(); n++; }
            }
            _log.AppendLine($"Clickables: {n} colliders wired.");
        }
    }
}
