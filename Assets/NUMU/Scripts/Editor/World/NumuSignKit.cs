using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Paints textures for signs, posters, whiteboards and packaging. Text is rendered with TextMeshPro through
    /// an offscreen camera and composited as a mask, so printed words look like real type rather than placeholders.
    /// Pixel coordinates start at the bottom-left, like Texture2D.
    /// </summary>
    public static class NumuSignKit
    {
        const int BakeLayer = 31;
        static TMP_FontAsset _font;

        static TMP_FontAsset Font => _font != null ? _font : _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        /// <summary>
        /// Renders white text into a w×h coverage mask (0..1), auto-sized to fit the box. An empty render is
        /// retried once and then reported as an error, so a build never silently ships blank signs.
        /// </summary>
        public static float[] TextMask(string text, int w, int h, float maxSize = 400f, bool bold = true, TextAlignmentOptions align = TextAlignmentOptions.Center, float italic = 0f)
        {
            var mask = RenderMask(text, w, h, maxSize, bold, align, italic);
            if (string.IsNullOrWhiteSpace(text) || Coverage(mask) > 0f) return mask;
            mask = RenderMask(text, w, h, maxSize, bold, align, italic);
            if (Coverage(mask) > 0f) return mask;
            throw new System.InvalidOperationException($"[NUMU] Text bake rendered nothing for \"{text}\" (render pipeline: {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name}).");
        }

        static float Coverage(float[] mask)
        {
            float sum = 0f;
            foreach (var v in mask) sum += v;
            return sum;
        }

        static float[] RenderMask(string text, int w, int h, float maxSize, bool bold, TextAlignmentOptions align, float italic)
        {
            const float unit = 0.01f;
            var holder = new GameObject("TextBake") { hideFlags = HideFlags.HideAndDontSave, layer = BakeLayer };
            var tmp = holder.AddComponent<TextMeshPro>();
            tmp.font = Font;
            tmp.text = text;
            tmp.color = Color.white;
            tmp.alignment = align;
            tmp.fontStyle = (bold ? FontStyles.Bold : FontStyles.Normal) | (italic > 0f ? FontStyles.Italic : FontStyles.Normal);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 1f;
            tmp.fontSizeMax = maxSize * unit * 10f;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = new Vector2(w * unit, h * unit);
            tmp.ForceMeshUpdate();

            var camGo = new GameObject("TextBakeCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            cam.orthographic = true;
            cam.orthographicSize = h * unit * 0.5f;
            cam.aspect = w / (float)h;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << BakeLayer;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 20f;
            camGo.transform.position = new Vector3(0, 0, -10f);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            var px = tex.GetPixels();
            var mask = new float[px.Length];
            for (int i = 0; i < px.Length; i++) mask[i] = Mathf.Clamp01(px[i].r);
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(holder);
            return mask;
        }

        /// <summary>A simple RGBA paint surface.</summary>
        public class Canvas
        {
            public readonly int W, H;
            public readonly Color[] Px;

            public Canvas(int w, int h, Color fill)
            {
                W = w;
                H = h;
                Px = new Color[w * h];
                for (int i = 0; i < Px.Length; i++) Px[i] = fill;
            }

            void Blend(int x, int y, Color c, float a)
            {
                if (x < 0 || y < 0 || x >= W || y >= H || a <= 0f) return;
                int i = y * W + x;
                Px[i] = Color.Lerp(Px[i], new Color(c.r, c.g, c.b, 1f), a * c.a);
            }

            /// <summary>Rounded rectangle with a soft (anti-aliased) edge.</summary>
            public void Rect(float x, float y, float w, float h, Color c, float radius = 0f)
            {
                float cx = x + w * 0.5f, cy = y + h * 0.5f, hx = w * 0.5f, hy = h * 0.5f;
                for (int py = Mathf.Max(0, (int)y - 1); py <= Mathf.Min(H - 1, (int)(y + h) + 1); py++)
                    for (int px = Mathf.Max(0, (int)x - 1); px <= Mathf.Min(W - 1, (int)(x + w) + 1); px++)
                    {
                        float qx = Mathf.Abs(px + 0.5f - cx) - hx + radius, qy = Mathf.Abs(py + 0.5f - cy) - hy + radius;
                        float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                        Blend(px, py, c, Mathf.Clamp01(0.5f - d));
                    }
            }

            public void Circle(float cx, float cy, float r, Color c)
            {
                for (int py = Mathf.Max(0, (int)(cy - r) - 1); py <= Mathf.Min(H - 1, (int)(cy + r) + 1); py++)
                    for (int px = Mathf.Max(0, (int)(cx - r) - 1); px <= Mathf.Min(W - 1, (int)(cx + r) + 1); px++)
                    {
                        float d = new Vector2(px + 0.5f - cx, py + 0.5f - cy).magnitude - r;
                        Blend(px, py, c, Mathf.Clamp01(0.5f - d));
                    }
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color c)
            {
                for (int py = Mathf.Max(0, (int)(cy - ry) - 1); py <= Mathf.Min(H - 1, (int)(cy + ry) + 1); py++)
                    for (int px = Mathf.Max(0, (int)(cx - rx) - 1); px <= Mathf.Min(W - 1, (int)(cx + rx) + 1); px++)
                    {
                        float dx = (px + 0.5f - cx) / rx, dy = (py + 0.5f - cy) / ry;
                        float d = (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
                        Blend(px, py, c, Mathf.Clamp01(0.5f - d));
                    }
            }

            /// <summary>Thick line segment with round caps.</summary>
            public void Line(Vector2 a, Vector2 b, float width, Color c)
            {
                int x0 = (int)(Mathf.Min(a.x, b.x) - width), x1 = (int)(Mathf.Max(a.x, b.x) + width);
                int y0 = (int)(Mathf.Min(a.y, b.y) - width), y1 = (int)(Mathf.Max(a.y, b.y) + width);
                Vector2 ab = b - a;
                for (int py = Mathf.Max(0, y0); py <= Mathf.Min(H - 1, y1); py++)
                    for (int px = Mathf.Max(0, x0); px <= Mathf.Min(W - 1, x1); px++)
                    {
                        var p = new Vector2(px + 0.5f, py + 0.5f);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                        float d = (p - (a + ab * t)).magnitude - width * 0.5f;
                        Blend(px, py, c, Mathf.Clamp01(0.5f - d));
                    }
            }

            /// <summary>Paints text (auto-fitted to the box) in the given colour.</summary>
            public void Text(string text, int x, int y, int w, int h, Color c, float maxSize = 400f, bool bold = true, TextAlignmentOptions align = TextAlignmentOptions.Center)
            {
                if (w < 4 || h < 4) return;
                var mask = TextMask(text, w, h, maxSize, bold, align);
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        Blend(x + i, y + j, c, mask[j * w + i]);
            }

            /// <summary>Copies an image (e.g. a UI icon PNG) into a box, alpha-blended.</summary>
            public void Image(Texture2D src, int x, int y, int w, int h)
            {
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                    {
                        var s = src.GetPixelBilinear((i + 0.5f) / w, (j + 0.5f) / h);
                        Blend(x + i, y + j, s, s.a);
                    }
            }

            /// <summary>Subtle print/paper grain so flat colours don't look computer-generated.</summary>
            public void Grain(float amount, int seed)
            {
                var rng = new System.Random(seed);
                for (int i = 0; i < Px.Length; i++)
                {
                    float k = 1f + ((float)rng.NextDouble() - 0.5f) * amount;
                    var p = Px[i];
                    Px[i] = new Color(Mathf.Clamp01(p.r * k), Mathf.Clamp01(p.g * k), Mathf.Clamp01(p.b * k), p.a);
                }
            }

            public Texture2D Save(string path, int maxSize = 1024)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.SetPixels(Px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.maxTextureSize = maxSize;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 4;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        /// <summary>Loads a generated UI PNG (icons, emotion faces) as a readable texture for compositing.</summary>
        public static Texture2D ReadPng(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }
    }
}
