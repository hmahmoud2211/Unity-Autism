using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Clear, calm emotion faces for the question cards (one strong cue per emotion: mouth shape, brows, eyes)
    /// and a few UI glyphs. Generated as PNG sprites so they stay crisp at any size.
    /// </summary>
    public static class NumuWorldIcons
    {
        public const string Dir = "Assets/NUMU/Worlds/UI";

        static readonly Color Skin = NumuColor.Hex("#ffd36b");
        static readonly Color SkinEdge = NumuColor.Hex("#f2b544");
        static readonly Color Ink = NumuColor.Hex("#4a3226");
        static readonly Color Cheek = NumuColor.Hex("#ff9e8a");
        static readonly Color Tear = NumuColor.Hex("#6cc4ff");

        public static void Generate()
        {
            Directory.CreateDirectory(Dir);
            var paths = new List<string>();
            void Save(string name, System.Action<IconCanvas> draw, int size = 256)
            {
                var c = new IconCanvas(size);
                draw(c);
                string p = $"{Dir}/{name}.png";
                c.Save(p);
                paths.Add(p);
            }

            Save("Emotion_Happy", c => Face(c, "happy"));
            Save("Emotion_Sad", c => Face(c, "sad"));
            Save("Emotion_Angry", c => Face(c, "angry"));
            Save("Emotion_Fear", c => Face(c, "fear"));
            Save("Emotion_Surprise", c => Face(c, "surprise"));
            Save("Emotion_Neutral", c => Face(c, "neutral"));
            Save("Emotion_Disgust", c => Face(c, "disgust"));
            Save("Glyph_Speech", c =>
            {
                c.Fill(p => IconCanvas.Box(p, new Vector2(0.5f, 0.44f), new Vector2(0.36f, 0.26f), 0.14f), Color.white);
                c.Fill(p => IconCanvas.Polygon(p, IconCanvas.V(0.3f, 0.62f, 0.44f, 0.66f, 0.26f, 0.84f)), Color.white);
            });
            Save("Glyph_Map", c =>
            {
                var pts = IconCanvas.V(0.14f, 0.26f, 0.38f, 0.18f, 0.62f, 0.26f, 0.86f, 0.18f, 0.86f, 0.74f, 0.62f, 0.82f, 0.38f, 0.74f, 0.14f, 0.82f);
                c.Fill(p => IconCanvas.Polygon(p, pts), Color.white);
                c.Fill(p => Mathf.Max(IconCanvas.Segment(p, new Vector2(0.38f, 0.2f), new Vector2(0.38f, 0.76f), 0.018f), -1f), new Color(0.75f, 0.75f, 0.8f, 1f));
                c.Fill(p => IconCanvas.Segment(p, new Vector2(0.62f, 0.26f), new Vector2(0.62f, 0.8f), 0.018f), new Color(0.75f, 0.75f, 0.8f, 1f));
            });
            Save("Glyph_Hand", c =>
            {
                c.Fill(p => IconCanvas.Box(p, new Vector2(0.5f, 0.64f), new Vector2(0.2f, 0.18f), 0.1f), Color.white);
                for (int i = 0; i < 4; i++)
                {
                    float x = 0.34f + i * 0.105f;
                    float top = i == 0 || i == 3 ? 0.3f : 0.22f;
                    c.Fill(p => IconCanvas.Segment(p, new Vector2(x, 0.56f), new Vector2(x, top), 0.045f), Color.white);
                }
                c.Fill(p => IconCanvas.Segment(p, new Vector2(0.3f, 0.66f), new Vector2(0.18f, 0.5f), 0.045f), Color.white);
            });
            Save("Glyph_View", c =>
            {
                // An eye: switch between seeing the child (third person) and seeing through their eyes.
                c.Fill(p => Mathf.Max(IconCanvas.Circle(p, new Vector2(0.5f, 0.86f), 0.52f), IconCanvas.Circle(p, new Vector2(0.5f, 0.14f), 0.52f)), Color.white);
                c.Fill(p => IconCanvas.Circle(p, new Vector2(0.5f, 0.5f), 0.17f), new Color(0.42f, 0.35f, 0.53f, 1f));
                c.Fill(p => IconCanvas.Circle(p, new Vector2(0.5f, 0.5f), 0.08f), Color.white);
            });
            Save("Feedback_Star", c =>
            {
                var star = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = (-90f + i * 36f) * Mathf.Deg2Rad;
                    float r = i % 2 == 0 ? 0.44f : 0.2f;
                    star[i] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.53f + Mathf.Sin(a) * r);
                }
                c.Fill(p => IconCanvas.Polygon(p, star) - 0.03f, NumuColor.Hex("#ffc93c"));
            });
            Save("Feedback_Heart", c =>
            {
                c.Fill(p => Mathf.Min(Mathf.Min(IconCanvas.Circle(p, new Vector2(0.36f, 0.4f), 0.17f), IconCanvas.Circle(p, new Vector2(0.64f, 0.4f), 0.17f)),
                    IconCanvas.Polygon(p, IconCanvas.V(0.2f, 0.47f, 0.8f, 0.47f, 0.5f, 0.84f))), NumuColor.Hex("#ff8fb1"));
            });

            ChoiceIcons(Save);

            AssetDatabase.Refresh();
            foreach (var p in paths)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                ti.textureType = TextureImporterType.Sprite;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
        }

        static readonly Color Hand = NumuColor.Hex("#f5c08f");
        static readonly Color HandEdge = NumuColor.Hex("#d99a68");

        /// <summary>Pictures for "What could you do?" answers: one simple, literal symbol each.</summary>
        static void ChoiceIcons(System.Action<string, System.Action<IconCanvas>, int> save)
        {
            void Save(string n, System.Action<IconCanvas> draw) => save(n, draw, 256);
            Color pink = NumuColor.Hex("#ff7aa2"), blue = NumuColor.Hex("#5aa0e6"), grey = NumuColor.Hex("#9a96a8"), red = NumuColor.Hex("#ef5a4c");

            Save("Choice_Hug", c =>
            {
                c.Fill(p => Mathf.Min(Mathf.Min(IconCanvas.Circle(p, new Vector2(0.39f, 0.42f), 0.15f), IconCanvas.Circle(p, new Vector2(0.61f, 0.42f), 0.15f)),
                    IconCanvas.Polygon(p, IconCanvas.V(0.25f, 0.48f, 0.75f, 0.48f, 0.5f, 0.8f))), pink);
                // Two arms wrapping around the heart.
                foreach (float s in new[] { -1f, 1f })
                {
                    c.Fill(p => Mathf.Max(IconCanvas.Ring(p, new Vector2(0.5f, 0.52f), 0.33f, 0.07f), s * (0.5f - p.x) + 0.02f), HandEdge);
                    c.Fill(p => Mathf.Max(Mathf.Max(IconCanvas.Ring(p, new Vector2(0.5f, 0.52f), 0.33f, 0.05f), s * (0.5f - p.x) + 0.04f), 0.3f - p.y), Hand);
                }
            });
            Save("Choice_ThumbsUp", c =>
            {
                c.Fill(p => IconCanvas.Box(p, new Vector2(0.54f, 0.62f), new Vector2(0.2f, 0.17f), 0.07f), HandEdge);
                c.Fill(p => IconCanvas.Box(p, new Vector2(0.54f, 0.62f), new Vector2(0.18f, 0.15f), 0.06f), Hand);
                c.Fill(p => IconCanvas.Segment(p, new Vector2(0.42f, 0.48f), new Vector2(0.46f, 0.2f), 0.075f), HandEdge);
                c.Fill(p => IconCanvas.Segment(p, new Vector2(0.42f, 0.48f), new Vector2(0.46f, 0.2f), 0.058f), Hand);
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.56f + i * 0.08f;
                    c.Fill(p => IconCanvas.Segment(p, new Vector2(0.6f, y), new Vector2(0.7f, y), 0.008f), HandEdge);
                }
                c.Fill(p => IconCanvas.Box(p, new Vector2(0.24f, 0.64f), new Vector2(0.07f, 0.17f), 0.03f), blue);
            });
            Save("Choice_Wave", c =>
            {
                c.Fill(p => IconCanvas.Box(p, new Vector2(0.5f, 0.62f), new Vector2(0.17f, 0.17f), 0.08f), Hand);
                for (int i = 0; i < 4; i++)
                {
                    float x = 0.37f + i * 0.085f;
                    float top = i == 0 || i == 3 ? 0.3f : 0.22f;
                    c.Fill(p => IconCanvas.Segment(p, new Vector2(x, 0.56f), new Vector2(x, top), 0.04f), Hand);
                }
                c.Fill(p => IconCanvas.Segment(p, new Vector2(0.34f, 0.66f), new Vector2(0.22f, 0.5f), 0.04f), Hand);
                // Motion arcs.
                foreach (float s in new[] { -1f, 1f })
                    c.Fill(p => Mathf.Max(IconCanvas.Ring(p, new Vector2(0.5f, 0.42f), 0.34f + (s > 0 ? 0f : 0.06f), 0.025f), Mathf.Abs(p.y - 0.3f) - 0.12f + (p.x < 0.5f ? 1f : 0f)), blue);
            });
            Save("Choice_Wait", c =>
            {
                var glass = IconCanvas.V(0.3f, 0.2f, 0.7f, 0.2f, 0.53f, 0.5f, 0.7f, 0.8f, 0.3f, 0.8f, 0.47f, 0.5f);
                c.Fill(p => IconCanvas.Polygon(p, glass), NumuColor.Hex("#d9ecff"));
                c.Fill(p => IconCanvas.Polygon(p, IconCanvas.V(0.36f, 0.3f, 0.64f, 0.3f, 0.5f, 0.46f)), NumuColor.Hex("#f2c14e"));
                c.Fill(p => IconCanvas.Polygon(p, IconCanvas.V(0.5f, 0.6f, 0.66f, 0.77f, 0.34f, 0.77f)), NumuColor.Hex("#f2c14e"));
                c.Fill(p => IconCanvas.Box(p, new Vector2(0.5f, 0.17f), new Vector2(0.26f, 0.035f), 0.02f), blue);
                c.Fill(p => IconCanvas.Box(p, new Vector2(0.5f, 0.83f), new Vector2(0.26f, 0.035f), 0.02f), blue);
            });
            Save("Choice_Laugh", c =>
            {
                c.Fill(p => IconCanvas.Circle(p, new Vector2(0.5f, 0.5f), 0.43f), Skin);
                foreach (float s in new[] { -1f, 1f })
                {
                    // Squeezed "> <" eyes.
                    c.Fill(p => IconCanvas.Segment(p, new Vector2(0.5f + s * 0.2f, 0.34f), new Vector2(0.5f + s * 0.1f, 0.39f), 0.022f), Ink);
                    c.Fill(p => IconCanvas.Segment(p, new Vector2(0.5f + s * 0.1f, 0.39f), new Vector2(0.5f + s * 0.2f, 0.44f), 0.022f), Ink);
                }
                c.Fill(p => Mathf.Max(IconCanvas.Circle(p, new Vector2(0.5f, 0.55f), 0.24f), 0.56f - p.y), Ink);
                c.Fill(p => Mathf.Max(IconCanvas.Circle(p, new Vector2(0.5f, 0.82f), 0.1f), IconCanvas.Circle(p, new Vector2(0.5f, 0.55f), 0.22f)), Cheek);
            });
            Save("Choice_Shout", c =>
            {
                c.Fill(p => IconCanvas.Circle(p, new Vector2(0.4f, 0.5f), 0.32f), Skin);
                foreach (float s in new[] { -1f, 1f })
                    c.Fill(p => IconCanvas.Segment(p, new Vector2(0.4f + s * 0.17f, 0.34f), new Vector2(0.4f + s * 0.05f, 0.4f), 0.022f), Ink);
                c.Fill(p => IconCanvas.Box(p, new Vector2(0.4f, 0.62f), new Vector2(0.11f, 0.08f), 0.06f), Ink);
                for (int i = -1; i <= 1; i++)
                {
                    float y = 0.5f + i * 0.16f;
                    c.Fill(p => IconCanvas.Segment(p, new Vector2(0.78f, 0.5f + i * 0.06f), new Vector2(0.92f, y), 0.025f), red);
                }
            });
            Save("Choice_Push", c =>
            {
                foreach (float x in new[] { 0.3f, 0.56f })
                    c.Fill(p => Mathf.Min(IconCanvas.Segment(p, new Vector2(x, 0.25f), new Vector2(x + 0.2f, 0.5f), 0.06f), IconCanvas.Segment(p, new Vector2(x + 0.2f, 0.5f), new Vector2(x, 0.75f), 0.06f)), red);
            });
            Save("Choice_WalkAway", c =>
            {
                c.Fill(p => IconCanvas.Box(p, new Vector2(0.32f, 0.5f), new Vector2(0.16f, 0.32f), 0.02f), NumuColor.Hex("#b98a5e"));
                c.Fill(p => IconCanvas.Box(p, new Vector2(0.32f, 0.5f), new Vector2(0.12f, 0.28f), 0.01f), NumuColor.Hex("#3f3550"));
                c.Fill(p => Mathf.Min(IconCanvas.Segment(p, new Vector2(0.52f, 0.5f), new Vector2(0.84f, 0.5f), 0.045f),
                    IconCanvas.Polygon(p, IconCanvas.V(0.9f, 0.5f, 0.74f, 0.36f, 0.74f, 0.64f))), grey);
            });
            Save("Choice_Ignore", c =>
            {
                c.Fill(p => IconCanvas.Box(p, new Vector2(0.5f, 0.44f), new Vector2(0.36f, 0.26f), 0.14f), NumuColor.Hex("#e4e1ec"));
                c.Fill(p => IconCanvas.Polygon(p, IconCanvas.V(0.3f, 0.62f, 0.44f, 0.66f, 0.26f, 0.84f)), NumuColor.Hex("#e4e1ec"));
                for (int i = -1; i <= 1; i++)
                {
                    float x = 0.5f + i * 0.13f;
                    c.Fill(p => IconCanvas.Circle(p, new Vector2(x, 0.44f), 0.04f), grey);
                }
                c.Fill(p => IconCanvas.Segment(p, new Vector2(0.2f, 0.78f), new Vector2(0.8f, 0.12f), 0.03f), grey);
            });
        }

        public static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");

        static void Face(IconCanvas c, string kind)
        {
            Vector2 C = new Vector2(0.5f, 0.5f);
            c.Fill(p => IconCanvas.Circle(p, C, 0.46f), SkinEdge);
            c.Fill(p => IconCanvas.Circle(p, C, 0.43f), Skin);
            float eyeY = kind == "surprise" || kind == "fear" ? 0.42f : 0.44f;
            float eyeR = kind == "surprise" || kind == "fear" ? 0.06f : 0.048f;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 e = new Vector2(0.5f + s * 0.15f, eyeY);
                if (kind == "disgust")
                {
                    // Squinting eyes.
                    c.Fill(p => IconCanvas.Segment(p, e + new Vector2(-0.05f, 0.01f), e + new Vector2(0.05f, -0.005f), 0.022f), Ink);
                }
                else if (kind == "happy")
                {
                    // Smiling "closed crescent" eyes.
                    c.Fill(p => Mathf.Max(IconCanvas.Ring(p, e + new Vector2(0, 0.03f), 0.055f, 0.03f), p.y - (e.y + 0.02f)), Ink);
                    c.Fill(p => IconCanvas.Circle(p, new Vector2(0.5f + s * 0.27f, 0.58f), 0.06f), new Color(Cheek.r, Cheek.g, Cheek.b, 0.6f));
                }
                else
                {
                    c.Fill(p => IconCanvas.Circle(p, e, eyeR), Ink);
                    c.Fill(p => IconCanvas.Circle(p, e + new Vector2(0.018f, -0.02f), eyeR * 0.35f), Color.white);
                }
                // Brows carry most of the emotion.
                Vector2 b0, b1;
                switch (kind)
                {
                    case "angry": b0 = new Vector2(0.5f + s * 0.24f, 0.3f); b1 = new Vector2(0.5f + s * 0.08f, 0.36f); break;
                    case "sad": b0 = new Vector2(0.5f + s * 0.24f, 0.36f); b1 = new Vector2(0.5f + s * 0.09f, 0.3f); break;
                    case "fear": b0 = new Vector2(0.5f + s * 0.24f, 0.31f); b1 = new Vector2(0.5f + s * 0.09f, 0.27f); break;
                    case "surprise": b0 = new Vector2(0.5f + s * 0.23f, 0.27f); b1 = new Vector2(0.5f + s * 0.08f, 0.27f); break;
                    case "disgust": b0 = new Vector2(0.5f + s * 0.23f, 0.32f); b1 = new Vector2(0.5f + s * 0.08f, 0.35f); break;
                    default: b0 = new Vector2(0.5f + s * 0.22f, 0.33f); b1 = new Vector2(0.5f + s * 0.09f, 0.33f); break;
                }
                if (kind != "happy" && kind != "neutral") c.Fill(p => IconCanvas.Segment(p, b0, b1, 0.022f), Ink);
            }
            switch (kind)
            {
                case "happy":
                    c.Fill(p => Mathf.Max(IconCanvas.Circle(p, new Vector2(0.5f, 0.58f), 0.18f), 0.6f - p.y), Ink);
                    c.Fill(p => Mathf.Max(IconCanvas.Circle(p, new Vector2(0.5f, 0.74f), 0.08f), IconCanvas.Circle(p, new Vector2(0.5f, 0.58f), 0.16f)), Cheek);
                    break;
                case "sad":
                    c.Fill(p => Mathf.Max(IconCanvas.Ring(p, new Vector2(0.5f, 0.8f), 0.16f, 0.035f), p.y - 0.7f), Ink);
                    c.Fill(p => Mathf.Min(IconCanvas.Circle(p, new Vector2(0.67f, 0.58f), 0.035f), IconCanvas.Polygon(p, IconCanvas.V(0.67f, 0.48f, 0.7f, 0.57f, 0.64f, 0.57f))), Tear);
                    break;
                case "angry":
                    c.Fill(p => Mathf.Max(IconCanvas.Ring(p, new Vector2(0.5f, 0.82f), 0.14f, 0.035f), p.y - 0.72f), Ink);
                    break;
                case "fear":
                    c.Fill(p => IconCanvas.Box(p, new Vector2(0.5f, 0.68f), new Vector2(0.11f, 0.05f), 0.04f), Ink);
                    c.Fill(p => Mathf.Min(IconCanvas.Segment(p, new Vector2(0.42f, 0.68f), new Vector2(0.58f, 0.68f), 0.008f), 1f), Color.white);
                    break;
                case "surprise":
                    c.Fill(p => IconCanvas.Circle(p, new Vector2(0.5f, 0.69f), 0.075f), Ink);
                    break;
                case "disgust":
                    // Wrinkled nose and a wavy, raised upper lip.
                    c.Fill(p => IconCanvas.Segment(p, new Vector2(0.46f, 0.52f), new Vector2(0.49f, 0.55f), 0.01f), Ink);
                    c.Fill(p => IconCanvas.Segment(p, new Vector2(0.54f, 0.52f), new Vector2(0.51f, 0.55f), 0.01f), Ink);
                    var lip = IconCanvas.V(0.37f, 0.72f, 0.44f, 0.66f, 0.5f, 0.7f, 0.56f, 0.65f, 0.63f, 0.71f);
                    for (int k = 0; k < 4; k++)
                    {
                        Vector2 a = new Vector2(lip[k].x, lip[k].y), b = new Vector2(lip[k + 1].x, lip[k + 1].y);
                        c.Fill(p => IconCanvas.Segment(p, a, b, 0.024f), Ink);
                    }
                    break;
                default:
                    c.Fill(p => IconCanvas.Segment(p, new Vector2(0.4f, 0.68f), new Vector2(0.6f, 0.68f), 0.025f), Ink);
                    break;
            }
        }
    }
}
