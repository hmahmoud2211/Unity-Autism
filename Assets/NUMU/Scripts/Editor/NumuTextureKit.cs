using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Numu.EditorTools
{
    /// <summary>
    /// Tiny anti-aliased vector painter (signed distance fields) used to author the icons and UI sprites
    /// as PNGs inside the project, so no external art is required.
    /// </summary>
    public class IconCanvas
    {
        readonly int _size;
        readonly Color[] _px;

        public IconCanvas(int size)
        {
            _size = size;
            _px = new Color[size * size];
        }

        /// <summary>Fills where sdf(p) &lt; 0. Coordinates are 0..1 with y pointing down, like the reference sketches.</summary>
        public void Fill(Func<Vector2, float> sdf, Color c, float soft = 1f)
        {
            for (int y = 0; y < _size; y++)
            {
                for (int x = 0; x < _size; x++)
                {
                    var p = new Vector2((x + 0.5f) / _size, (y + 0.5f) / _size);
                    float d = sdf(p) * _size;
                    float cov = Mathf.Clamp01(0.5f - d / soft);
                    if (cov <= 0f) continue;
                    int idx = (_size - 1 - y) * _size + x;
                    Color dst = _px[idx];
                    float a = c.a * cov;
                    float outA = a + dst.a * (1f - a);
                    Color rgb = outA > 0f ? (c * a + dst * dst.a * (1f - a)) / outA : Color.clear;
                    rgb.a = outA;
                    _px[idx] = rgb;
                }
            }
        }

        /// <summary>Soft shadow / glow: alpha falls off smoothly outside the shape.</summary>
        public void Glow(Func<Vector2, float> sdf, Color c, float radius)
        {
            for (int y = 0; y < _size; y++)
                for (int x = 0; x < _size; x++)
                {
                    var p = new Vector2((x + 0.5f) / _size, (y + 0.5f) / _size);
                    float d = Mathf.Max(0f, sdf(p));
                    float a = c.a * Mathf.Exp(-(d * d) / (radius * radius) * 3f);
                    int idx = (_size - 1 - y) * _size + x;
                    Color dst = _px[idx];
                    float outA = a + dst.a * (1f - a);
                    Color rgb = outA > 0f ? (c * a + dst * dst.a * (1f - a)) / outA : Color.clear;
                    rgb.a = outA;
                    _px[idx] = rgb;
                }
        }

        public void Save(string path)
        {
            var tex = new Texture2D(_size, _size, TextureFormat.RGBA32, false);
            // Bleed colour into transparent pixels so filtering doesn't create dark fringes.
            var outPx = (Color[])_px.Clone();
            for (int i = 0; i < outPx.Length; i++) if (outPx[i].a <= 0f) outPx[i] = new Color(1, 1, 1, 0);
            tex.SetPixels(outPx);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        // ------------------------------------------------------------------ SDF primitives

        public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        public static float Box(Vector2 p, Vector2 c, Vector2 half, float r = 0f)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * r;
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        public static float Segment(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        public static float Polygon(Vector2 p, IList<Vector2> v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Count - 1; i < v.Count; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        public static float Ring(Vector2 p, Vector2 c, float r, float thickness) => Mathf.Abs((p - c).magnitude - r) - thickness * 0.5f;

        public static Vector2[] V(params float[] xy)
        {
            var r = new Vector2[xy.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return r;
        }
    }

    public static class NumuTextureKit
    {
        public const string IconDir = NumuWorldBuilder.TexturesDir + "/Icons";
        public const string UIDir = NumuWorldBuilder.TexturesDir + "/UI";

        static readonly Color W = Color.white;

        public static void GenerateAll()
        {
            Directory.CreateDirectory(IconDir);
            Directory.CreateDirectory(UIDir);
            var written = new List<(string path, bool sprite, Vector4 border, bool mips)>();

            void Icon(string name, Action<IconCanvas> draw, int size = 256)
            {
                var c = new IconCanvas(size);
                draw(c);
                string path = $"{IconDir}/{name}.png";
                c.Save(path);
                written.Add((path, true, Vector4.zero, true));
            }

            void UISprite(string name, int size, Vector4 border, Action<IconCanvas> draw)
            {
                var c = new IconCanvas(size);
                draw(c);
                string path = $"{UIDir}/{name}.png";
                c.Save(path);
                written.Add((path, true, border, false));
            }

            Icon("Icon_Home", c => House(c, NumuColor.Hex("#e8503c"), NumuColor.Hex("#fff3dc"), NumuColor.Hex("#3f86d9"), true));
            Icon("Icon_Hub", c => House(c, NumuColor.Hex("#2fa58d"), NumuColor.Hex("#fff3dc"), NumuColor.Hex("#3f86d9"), true));
            Icon("Icon_School", c => Book(c, true));
            Icon("Icon_Park", c => Tree(c, true));
            Icon("Icon_Supermarket", c => Cart(c, NumuColor.Hex("#f39b2c"), true));
            Icon("Icon_Cart_White", c => Cart(c, Color.white, false));
            Icon("Icon_Lock", c =>
            {
                c.Fill(p => Mathf.Max(IconCanvas.Ring(p, new Vector2(0.5f, 0.42f), 0.17f, 0.08f), p.y - 0.47f), NumuColor.Hex("#8a93a3"));
                c.Fill(p => IconCanvas.Box(p, new Vector2(0.5f, 0.62f), new Vector2(0.26f, 0.2f), 0.06f), NumuColor.Hex("#a8b0bd"));
                c.Fill(p => IconCanvas.Circle(p, new Vector2(0.5f, 0.6f), 0.05f), NumuColor.Hex("#5c6574"));
            });
            Icon("Icon_Star", c =>
            {
                var star = StarPts(0.5f, 0.52f, 0.44f, 0.2f);
                c.Fill(p => IconCanvas.Polygon(p, star) - 0.02f, NumuColor.Hex("#ffcf2e"));
                c.Fill(p => IconCanvas.Polygon(p, StarPts(0.47f, 0.47f, 0.22f, 0.1f)), NumuColor.Hex("#ffe98a"));
            });
            Icon("Icon_Check", c =>
            {
                c.Fill(p => Mathf.Min(IconCanvas.Segment(p, new Vector2(0.22f, 0.52f), new Vector2(0.42f, 0.72f), 0.07f),
                    IconCanvas.Segment(p, new Vector2(0.42f, 0.72f), new Vector2(0.8f, 0.3f), 0.07f)), W);
            });
            Icon("Icon_Back", c =>
            {
                c.Fill(p => Mathf.Min(IconCanvas.Segment(p, new Vector2(0.58f, 0.24f), new Vector2(0.32f, 0.5f), 0.075f),
                    IconCanvas.Segment(p, new Vector2(0.32f, 0.5f), new Vector2(0.58f, 0.76f), 0.075f)), W);
            });
            Icon("Icon_HomeButton", c =>
            {
                var roof = IconCanvas.V(0.16f, 0.5f, 0.5f, 0.2f, 0.84f, 0.5f);
                c.Fill(p => Mathf.Min(IconCanvas.Polygon(p, roof) - 0.03f, IconCanvas.Box(p, new Vector2(0.5f, 0.66f), new Vector2(0.24f, 0.2f), 0.04f)), W);
            });
            Icon("Icon_SoundOn", c => Speaker(c, true));
            Icon("Icon_SoundOff", c => Speaker(c, false));
            Icon("Icon_Motion", c =>
            {
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.32f + i * 0.18f;
                    c.Fill(p => SineLine(p, y, 0.2f, 0.8f, 0.045f), W);
                }
            });
            Icon("Icon_Play", c => c.Fill(p => IconCanvas.Polygon(p, IconCanvas.V(0.34f, 0.22f, 0.8f, 0.5f, 0.34f, 0.78f)) - 0.03f, W));
            Icon("Icon_Settings", c =>
            {
                c.Fill(p =>
                {
                    Vector2 d = p - new Vector2(0.5f, 0.5f);
                    float a = Mathf.Atan2(d.y, d.x);
                    float r = 0.3f + 0.06f * Mathf.Clamp(Mathf.Cos(a * 8f) * 3f, -1f, 1f);
                    return d.magnitude - r;
                }, W);
                c.Fill(p => IconCanvas.Circle(p, new Vector2(0.5f, 0.5f), 0.12f), new Color(0.35f, 0.35f, 0.45f, 1f));
            });

            // UI sprites (9-sliced).
            UISprite("UI_RoundRect", 128, new Vector4(44, 44, 44, 44), c => c.Fill(p => IconCanvas.Box(p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f) - Vector2.one * (1f / 128f), 0.32f), W));
            UISprite("UI_RoundRectSmall", 64, new Vector4(20, 20, 20, 20), c => c.Fill(p => IconCanvas.Box(p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f) - Vector2.one * (1f / 64f), 0.28f), W));
            UISprite("UI_Pill", 128, new Vector4(63, 63, 63, 63), c => c.Fill(p => IconCanvas.Box(p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f) - Vector2.one * (1f / 128f), 0.495f), W));
            UISprite("UI_Circle", 128, Vector4.zero, c => c.Fill(p => IconCanvas.Circle(p, new Vector2(0.5f, 0.5f), 0.49f), W));
            UISprite("UI_Shadow", 128, new Vector4(56, 56, 56, 56), c => c.Glow(p => IconCanvas.Box(p, new Vector2(0.5f, 0.5f), new Vector2(0.3f, 0.3f), 0.15f), new Color(0.35f, 0.2f, 0.3f, 0.5f), 0.16f));
            UISprite("UI_Gradient", 64, Vector4.zero, c => c.Fill(p => -1f, new Color(1, 1, 1, 1)));

            AssetDatabase.Refresh();
            foreach (var (path, sprite, border, mips) in written)
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = mips;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Bilinear;
                imp.spriteBorder = border;
                imp.SaveAndReimport();
            }
        }

        public static Sprite Sprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/{name}.png") ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/{name}.png");

        public static Texture2D Texture(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{IconDir}/{name}.png") ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{UIDir}/{name}.png");

        // ------------------------------------------------------------------ icon drawings

        static void House(IconCanvas c, Color roof, Color wall, Color door, bool outline)
        {
            var roofPts = IconCanvas.V(0.1f, 0.5f, 0.5f, 0.13f, 0.9f, 0.5f);
            Func<Vector2, float> body = p => IconCanvas.Box(p, new Vector2(0.5f, 0.68f), new Vector2(0.28f, 0.2f), 0.03f);
            Func<Vector2, float> roofF = p => IconCanvas.Polygon(p, roofPts) - 0.035f;
            Func<Vector2, float> chimney = p => IconCanvas.Box(p, new Vector2(0.72f, 0.26f), new Vector2(0.05f, 0.1f), 0.02f);
            if (outline) c.Fill(p => Mathf.Min(Mathf.Min(body(p), roofF(p)), chimney(p)) - 0.045f, W);
            c.Fill(chimney, NumuColor.Shade(roof, 0.8f));
            c.Fill(body, wall);
            c.Fill(roofF, roof);
            c.Fill(p => IconCanvas.Box(p, new Vector2(0.5f, 0.76f), new Vector2(0.075f, 0.12f), 0.04f), door);
            c.Fill(p => IconCanvas.Box(p, new Vector2(0.32f, 0.64f), new Vector2(0.05f, 0.05f), 0.015f), NumuColor.Hex("#8fd0f5"));
            c.Fill(p => IconCanvas.Box(p, new Vector2(0.68f, 0.64f), new Vector2(0.05f, 0.05f), 0.015f), NumuColor.Hex("#8fd0f5"));
        }

        static void Book(IconCanvas c, bool outline)
        {
            Color cover = NumuColor.Hex("#2f7fe0");
            var left = IconCanvas.V(0.12f, 0.28f, 0.48f, 0.34f, 0.48f, 0.8f, 0.12f, 0.74f);
            var right = IconCanvas.V(0.52f, 0.34f, 0.88f, 0.28f, 0.88f, 0.74f, 0.52f, 0.8f);
            var coverPts = IconCanvas.V(0.08f, 0.3f, 0.5f, 0.38f, 0.92f, 0.3f, 0.92f, 0.8f, 0.5f, 0.88f, 0.08f, 0.8f);
            if (outline) c.Fill(p => IconCanvas.Polygon(p, coverPts) - 0.05f, W);
            c.Fill(p => IconCanvas.Polygon(p, coverPts) - 0.01f, cover);
            c.Fill(p => IconCanvas.Polygon(p, left), NumuColor.Hex("#ffffff"));
            c.Fill(p => IconCanvas.Polygon(p, right), NumuColor.Hex("#ffffff"));
            for (int i = 0; i < 3; i++)
            {
                float y = 0.46f + i * 0.1f;
                c.Fill(p => IconCanvas.Segment(p, new Vector2(0.18f, y), new Vector2(0.42f, y + 0.03f), 0.012f), NumuColor.Hex("#b9d4f2"));
                c.Fill(p => IconCanvas.Segment(p, new Vector2(0.58f, y + 0.03f), new Vector2(0.82f, y), 0.012f), NumuColor.Hex("#b9d4f2"));
            }
        }

        static void Tree(IconCanvas c, bool outline)
        {
            Func<Vector2, float> canopy = p => Mathf.Min(Mathf.Min(IconCanvas.Circle(p, new Vector2(0.5f, 0.36f), 0.22f), IconCanvas.Circle(p, new Vector2(0.32f, 0.5f), 0.17f)),
                IconCanvas.Circle(p, new Vector2(0.68f, 0.5f), 0.17f));
            Func<Vector2, float> trunk = p => IconCanvas.Box(p, new Vector2(0.5f, 0.72f), new Vector2(0.06f, 0.16f), 0.03f);
            if (outline) c.Fill(p => Mathf.Min(canopy(p), trunk(p)) - 0.045f, W);
            c.Fill(trunk, NumuColor.Hex("#9a5c33"));
            c.Fill(canopy, NumuColor.Hex("#4fae45"));
            c.Fill(p => IconCanvas.Circle(p, new Vector2(0.44f, 0.3f), 0.09f), NumuColor.Hex("#8bd35a"));
            c.Fill(p => IconCanvas.Circle(p, new Vector2(0.3f, 0.46f), 0.06f), NumuColor.Hex("#7cc94f"));
        }

        static void Cart(IconCanvas c, Color color, bool outline)
        {
            var basket = IconCanvas.V(0.22f, 0.3f, 0.86f, 0.3f, 0.76f, 0.62f, 0.3f, 0.62f);
            Func<Vector2, float> shape = p => Mathf.Min(Mathf.Min(IconCanvas.Polygon(p, basket) - 0.02f,
                IconCanvas.Segment(p, new Vector2(0.08f, 0.2f), new Vector2(0.2f, 0.2f), 0.035f)),
                Mathf.Min(IconCanvas.Segment(p, new Vector2(0.2f, 0.2f), new Vector2(0.32f, 0.7f), 0.035f), IconCanvas.Segment(p, new Vector2(0.32f, 0.7f), new Vector2(0.78f, 0.7f), 0.035f)));
            Func<Vector2, float> wheels = p => Mathf.Min(IconCanvas.Circle(p, new Vector2(0.36f, 0.82f), 0.065f), IconCanvas.Circle(p, new Vector2(0.72f, 0.82f), 0.065f));
            if (outline) c.Fill(p => Mathf.Min(shape(p), wheels(p)) - 0.045f, W);
            c.Fill(shape, color);
            c.Fill(wheels, outline ? NumuColor.Hex("#7a4a1c") : color);
            Color grid = outline ? NumuColor.Hex("#ffe3b0") : new Color(1, 1, 1, 0);
            if (outline)
            {
                for (int i = 1; i < 4; i++)
                {
                    float x = Mathf.Lerp(0.3f, 0.78f, i / 4f);
                    c.Fill(p => IconCanvas.Segment(p, new Vector2(x, 0.36f), new Vector2(x - 0.02f, 0.56f), 0.015f), grid);
                }
                c.Fill(p => IconCanvas.Segment(p, new Vector2(0.28f, 0.45f), new Vector2(0.8f, 0.45f), 0.015f), grid);
            }
        }

        static void Speaker(IconCanvas c, bool on)
        {
            var body = IconCanvas.V(0.14f, 0.4f, 0.3f, 0.4f, 0.5f, 0.22f, 0.5f, 0.78f, 0.3f, 0.6f, 0.14f, 0.6f);
            c.Fill(p => IconCanvas.Polygon(p, body) - 0.02f, W);
            if (on)
            {
                c.Fill(p => Mathf.Max(IconCanvas.Ring(p, new Vector2(0.5f, 0.5f), 0.16f, 0.05f), 0.56f - p.x), W);
                c.Fill(p => Mathf.Max(IconCanvas.Ring(p, new Vector2(0.5f, 0.5f), 0.29f, 0.05f), 0.56f - p.x), W);
            }
            else
            {
                c.Fill(p => Mathf.Min(IconCanvas.Segment(p, new Vector2(0.62f, 0.38f), new Vector2(0.86f, 0.62f), 0.04f),
                    IconCanvas.Segment(p, new Vector2(0.62f, 0.62f), new Vector2(0.86f, 0.38f), 0.04f)), W);
            }
        }

        static float SineLine(Vector2 p, float y, float x0, float x1, float r)
        {
            float best = float.MaxValue;
            Vector2 prev = new Vector2(x0, y);
            for (int i = 1; i <= 20; i++)
            {
                float t = i / 20f;
                Vector2 cur = new Vector2(Mathf.Lerp(x0, x1, t), y + Mathf.Sin(t * Mathf.PI * 2f) * 0.04f);
                best = Mathf.Min(best, IconCanvas.Segment(p, prev, cur, r));
                prev = cur;
            }
            return best;
        }

        static Vector2[] StarPts(float cx, float cy, float ro, float ri)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = (-90f + i * 36f) * Mathf.Deg2Rad;
                float r = i % 2 == 0 ? ro : ri;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }
    }
}
