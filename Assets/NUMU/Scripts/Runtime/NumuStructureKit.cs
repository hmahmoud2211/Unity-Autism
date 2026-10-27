using System.Collections.Generic;
using UnityEngine;

namespace Numu
{
    /// <summary>Wooden structures, water features and atmosphere pieces.</summary>
    public static class StructureKit
    {
        public static readonly Color[] Wood =
        {
            NumuColor.Hex("#b9824f"), NumuColor.Hex("#a8713f"), NumuColor.Hex("#c4915c"), NumuColor.Hex("#9a6538"),
        };
        public static readonly Color WoodDark = NumuColor.Hex("#7a4a2a");
        public static readonly Color Rope = NumuColor.Hex("#dcb987");

        static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        static Color WoodColor(System.Random r) => NumuColor.Jitter(Wood[r.Next(Wood.Length)], r, 0.05f);

        static Vector3 SagPoint(Vector3 a, Vector3 b, float t, float sag) => Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));

        /// <summary>Plank bridge between two deck points. Planks run across, posts and rope rails along both sides.</summary>
        public static void PlankBridge(MeshBuilder mb, Vector3 a, Vector3 b, float width, float sag, int seed, bool hangers = false)
        {
            var rng = new System.Random(seed);
            Vector3 dir = b - a;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            float len = flat.magnitude;
            Vector3 side = Vector3.Cross(Vector3.up, flat.normalized);
            int planks = Mathf.Max(3, Mathf.RoundToInt(dir.magnitude / 0.52f));
            for (int k = 0; k < planks; k++)
            {
                float t = (k + 0.5f) / planks;
                Vector3 p = SagPoint(a, b, t, sag);
                Vector3 tan = (SagPoint(a, b, Mathf.Min(1f, t + 0.01f), sag) - SagPoint(a, b, Mathf.Max(0f, t - 0.01f), sag)).normalized;
                Quaternion rot = Quaternion.LookRotation(tan, Vector3.up) * Quaternion.Euler(Rand(rng, -2f, 2f), Rand(rng, -3f, 3f), Rand(rng, -2.5f, 2.5f));
                var size = new Vector3(width + Rand(rng, -0.1f, 0.15f), 0.14f, dir.magnitude / planks * 0.86f);
                Shapes.RoundedBox(mb, Matrix4x4.TRS(p, rot, Vector3.one), size, 0.05f, 1, WoodColor(rng));
            }
            // Stringers under the deck.
            foreach (float s in new[] { -0.38f, 0.38f })
            {
                var path = new List<Vector3>();
                for (int i = 0; i <= 12; i++) path.Add(SagPoint(a, b, i / 12f, sag) + side * (s * width) + Vector3.down * 0.14f);
                Shapes.Tube(mb, path, 0.1f, 6, WoodDark);
            }
            // Posts and rope rails.
            int postCount = Mathf.Max(2, Mathf.RoundToInt(len / 2.6f) + 1);
            foreach (float s in new[] { -0.5f, 0.5f })
            {
                var tops = new List<Vector3>();
                for (int i = 0; i < postCount; i++)
                {
                    float t = i / (float)(postCount - 1);
                    Vector3 basePt = SagPoint(a, b, t, sag) + side * (s * (width + 0.25f));
                    bool end = i == 0 || i == postCount - 1;
                    float h = end ? 1.35f : 1.05f;
                    float th = end ? 0.3f : 0.2f;
                    Shapes.RoundedBox(mb, Matrix4x4.TRS(basePt + Vector3.up * (h * 0.5f - 0.25f), Quaternion.LookRotation(flat), Vector3.one), new Vector3(th, h + 0.5f, th), 0.06f, 1, WoodColor(rng));
                    if (end) Shapes.Ellipsoid(mb, Matrix4x4.Translate(basePt + Vector3.up * (h - 0.2f)), new Vector3(0.2f, 0.12f, 0.2f), WoodDark, 1);
                    tops.Add(basePt + Vector3.up * (h - 0.32f));
                }
                for (int i = 0; i < tops.Count - 1; i++)
                {
                    var rope = new List<Vector3>();
                    for (int k = 0; k <= 8; k++) rope.Add(SagPoint(tops[i], tops[i + 1], k / 8f, 0.18f));
                    Shapes.Tube(mb, rope, 0.055f, 5, Rope, false);
                    if (hangers)
                    {
                        for (int k = 1; k < 4; k++)
                        {
                            float t = k / 4f;
                            Vector3 top = SagPoint(tops[i], tops[i + 1], t, 0.18f);
                            float gt = Mathf.Lerp(i / (float)(tops.Count - 1), (i + 1) / (float)(tops.Count - 1), t);
                            Vector3 deck = SagPoint(a, b, gt, sag) + side * (s * (width + 0.15f)) + Vector3.up * 0.05f;
                            Shapes.Tube(mb, new List<Vector3> { top, deck }, 0.035f, 4, Rope, false);
                        }
                    }
                }
            }
        }

        /// <summary>Wooden staircase from <paramref name="top"/> down to <paramref name="bottom"/> with side stringers and rails.</summary>
        public static void Stairs(MeshBuilder mb, Vector3 top, Vector3 bottom, float width, int seed, bool rails = true)
        {
            var rng = new System.Random(seed);
            Vector3 d = bottom - top;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.back;
            Quaternion yaw = Quaternion.LookRotation(flat.normalized, Vector3.up);
            Vector3 side = yaw * Vector3.right;
            float rise = top.y - bottom.y;
            int steps = Mathf.Max(2, Mathf.CeilToInt(rise / 0.34f));
            float run = flat.magnitude / steps;
            for (int k = 0; k < steps; k++)
            {
                float t = (k + 0.5f) / steps;
                Vector3 p = Vector3.Lerp(top, bottom, t);
                p.y = top.y - rise * (k + 1) / steps + 0.08f;
                Shapes.RoundedBox(mb, Matrix4x4.TRS(p, yaw * Quaternion.Euler(0, Rand(rng, -2f, 2f), 0), Vector3.one), new Vector3(width, 0.16f, run * 1.05f + 0.08f), 0.05f, 1, WoodColor(rng));
            }
            float slopeLen = d.magnitude;
            Quaternion slope = Quaternion.LookRotation(d.normalized, Vector3.up);
            foreach (float s in new[] { -0.5f, 0.5f })
            {
                Vector3 mid = (top + bottom) * 0.5f + side * (s * (width + 0.12f)) + Vector3.down * 0.08f;
                Shapes.RoundedBox(mb, Matrix4x4.TRS(mid, slope, Vector3.one), new Vector3(0.16f, 0.42f, slopeLen + 0.3f), 0.05f, 1, WoodDark);
                if (!rails) continue;
                Vector3 pTop = top + side * (s * (width + 0.15f));
                Vector3 pBot = bottom + side * (s * (width + 0.15f));
                foreach (var pp in new[] { pTop, pBot, Vector3.Lerp(pTop, pBot, 0.5f) })
                {
                    Shapes.RoundedBox(mb, Matrix4x4.TRS(pp + Vector3.up * 0.45f, yaw, Vector3.one), new Vector3(0.18f, 1.2f, 0.18f), 0.05f, 1, WoodColor(rng));
                }
                Shapes.Beam(mb, pTop + Vector3.up * 0.95f, pBot + Vector3.up * 0.95f, 0.07f, 6, WoodColor(rng));
            }
        }

        /// <summary>Plank platform on stilts (docks, landings).</summary>
        public static void Deck(MeshBuilder mb, Vector3 center, Quaternion rot, Vector2 size, float waterY, int seed, bool posts = true)
        {
            var rng = new System.Random(seed);
            int planks = Mathf.Max(2, Mathf.RoundToInt(size.y / 0.5f));
            for (int k = 0; k < planks; k++)
            {
                float z = -size.y * 0.5f + (k + 0.5f) * size.y / planks;
                Vector3 p = center + rot * new Vector3(Rand(rng, -0.06f, 0.06f), 0f, z);
                Shapes.RoundedBox(mb, Matrix4x4.TRS(p, rot * Quaternion.Euler(0, Rand(rng, -1.5f, 1.5f), 0), Vector3.one), new Vector3(size.x + Rand(rng, -0.1f, 0.1f), 0.16f, size.y / planks * 0.88f), 0.05f, 1, WoodColor(rng));
            }
            foreach (float sx in new[] { -0.5f, 0.5f })
            {
                Shapes.RoundedBox(mb, Matrix4x4.TRS(center + rot * new Vector3(sx * (size.x - 0.3f), -0.16f, 0), rot, Vector3.one), new Vector3(0.18f, 0.2f, size.y), 0.04f, 1, WoodDark);
            }
            if (!posts) return;
            for (int ix = 0; ix < 2; ix++)
            {
                for (int iz = 0; iz <= Mathf.Max(1, Mathf.RoundToInt(size.y / 3f)); iz++)
                {
                    float fx = ix == 0 ? -0.5f : 0.5f;
                    float fz = -0.5f + iz / Mathf.Max(1f, Mathf.RoundToInt(size.y / 3f));
                    Vector3 p = center + rot * new Vector3(fx * (size.x + 0.1f), 0f, fz * size.y);
                    float h = center.y - waterY + 1.6f;
                    Shapes.Cylinder(mb, Matrix4x4.Translate(new Vector3(p.x, waterY - 1.2f, p.z)), 0.22f, 0.2f, h + 0.5f, 10, WoodColor(rng));
                    Shapes.Ellipsoid(mb, Matrix4x4.Translate(new Vector3(p.x, center.y + 0.62f, p.z)), new Vector3(0.21f, 0.08f, 0.21f), WoodDark, 1);
                }
            }
        }

        /// <summary>
        /// Curved waterfall sheet: starts at <paramref name="lip"/>, arcs outwards and drops to <paramref name="waterY"/>.
        /// UV.x runs across the fall, UV.y from lip to base.
        /// </summary>
        public static void WaterfallSheet(MeshBuilder mb, Vector3 lip, Vector3 outward, float width, float waterY, int across = 6, int along = 14)
        {
            outward.y = 0f;
            outward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, outward);
            float drop = lip.y - waterY;
            int start = mb.VertexCount;
            for (int j = 0; j <= along; j++)
            {
                float t = j / (float)along;
                float outT = Mathf.Sqrt(t) * Mathf.Min(1.4f, drop * 0.22f) + 0.15f;
                float w = width * (1f + t * 0.25f);
                for (int i = 0; i <= across; i++)
                {
                    float s = i / (float)across - 0.5f;
                    float bulge = (1f - 4f * s * s) * 0.18f;
                    Vector3 p = lip + outward * (outT + bulge) + side * (s * w) + Vector3.down * (t * drop + 0.05f);
                    mb.Add(p, outward, Color.white, new Vector2(i / (float)across, t));
                }
            }
            int stride = across + 1;
            for (int j = 0; j < along; j++)
            {
                for (int i = 0; i < across; i++)
                {
                    int a = start + j * stride + i;
                    mb.QuadFacing(a, a + 1, a + stride + 1, a + stride, outward);
                }
            }
        }

        /// <summary>White foam puffs and a ripple ring where a waterfall hits the water.</summary>
        public static void Splash(MeshBuilder mb, Vector3 center, float width, int seed)
        {
            var rng = new System.Random(seed);
            Color white = NumuColor.Hex("#ffffff");
            Color tint = NumuColor.Hex("#e7f6fb");
            int puffs = Mathf.Max(4, Mathf.RoundToInt(width * 3f));
            for (int i = 0; i < puffs; i++)
            {
                Vector3 p = center + new Vector3(Rand(rng, -width * 0.6f, width * 0.6f), Rand(rng, -0.05f, 0.25f), Rand(rng, -0.3f, 0.6f));
                float r = Rand(rng, 0.3f, 0.55f) * Mathf.Clamp(width * 0.5f, 0.7f, 1.4f);
                Shapes.Blob(mb, Matrix4x4.Translate(p), new Vector3(r, r * 0.7f, r), white, 0.12f, 2f, rng.Next(999), 2, 0.4f, d => Color.Lerp(tint, white, d.y * 0.5f + 0.5f));
            }
        }

        /// <summary>Puffy cartoon cloud made of overlapping blobs with a flattened base.</summary>
        public static void Cloud(MeshBuilder mb, Vector3 center, float size, int seed, float stretch = 1.6f)
        {
            var rng = new System.Random(seed);
            Color top = NumuColor.Hex("#ffffff");
            Color bottom = NumuColor.Hex("#e8e2f1");
            int puffs = 5 + rng.Next(4);
            for (int i = 0; i < puffs; i++)
            {
                float t = puffs == 1 ? 0f : i / (float)(puffs - 1) - 0.5f;
                float r = size * Rand(rng, 0.45f, 0.75f) * (1f - Mathf.Abs(t) * 0.7f);
                Vector3 p = center + new Vector3(t * size * stretch * 1.6f + Rand(rng, -0.2f, 0.2f) * size, r * Rand(rng, 0.1f, 0.45f), Rand(rng, -0.35f, 0.35f) * size);
                Shapes.Blob(mb, Matrix4x4.Translate(p), new Vector3(r * 1.1f, r * 0.9f, r), top, 0.06f, 1.4f, rng.Next(999), 2, 0.55f,
                    d => Color.Lerp(bottom, top, Mathf.Clamp01(d.y * 0.8f + 0.55f)));
            }
        }

        /// <summary>Tall pastel karst pillar for the misty background, with a grassy cap.</summary>
        public static void KarstPillar(MeshBuilder mb, Vector3 basePos, float height, float radius, int seed, bool grassCap = true)
        {
            var rng = new System.Random(seed);
            Color low = NumuColor.Hex("#aaa6bd");
            Color high = NumuColor.Hex("#d3c6d6");
            Color capCol = NumuColor.Hex("#a9c79a");
            int seg = 18, rows = 14;
            int start = mb.VertexCount, tStart = mb.Triangles.Count;
            for (int j = 0; j <= rows; j++)
            {
                float t = j / (float)rows;
                float y = t * height;
                for (int i = 0; i < seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float ledge = Mathf.Floor(t * 5f + NumuNoise.Value3(i * 0.2f, seed, 0f) * 0.8f) / 5f;
                    // Conical karst profile: broad base, narrowing to a craggy peak.
                    float r = radius * (1f - 0.78f * Mathf.Pow(t, 1.25f)) * (0.75f + 0.5f * NumuNoise.Value3(Mathf.Cos(a) * 1.5f + seed, Mathf.Sin(a) * 1.5f, t * 3f));
                    r *= 1f + (0.08f * (1f - Mathf.Repeat(t * 5f, 1f))) * (ledge > 0 ? 1f : 0f);
                    if (j == rows) r *= 0.6f;
                    // Lean the upper part slightly so peaks don't look like candles.
                    Vector3 lean = new Vector3(Mathf.Sin(seed * 1.7f), 0, Mathf.Cos(seed * 2.3f)) * (radius * 0.35f * t * t);
                    mb.Add(basePos + lean + dir * r + Vector3.up * y, dir, Color.Lerp(low, high, t));
                }
            }
            Vector3 topLean = new Vector3(Mathf.Sin(seed * 1.7f), 0, Mathf.Cos(seed * 2.3f)) * (radius * 0.35f);
            int topCenter = mb.Add(basePos + topLean + Vector3.up * (height + radius * 0.12f), Vector3.up, high);
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < seg; i++)
                {
                    int i1 = (i + 1) % seg;
                    int a = start + j * seg + i;
                    float ang = (i + 0.5f) / seg * Mathf.PI * 2f;
                    mb.QuadFacing(a, start + j * seg + i1, start + (j + 1) * seg + i1, start + (j + 1) * seg + i, new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)));
                }
            }
            for (int i = 0; i < seg; i++) mb.TriFacing(topCenter, start + rows * seg + i, start + rows * seg + (i + 1) % seg, Vector3.up);
            mb.RecalculateNormals(start, tStart);
            if (grassCap)
            {
                Vector3 capLean = new Vector3(Mathf.Sin(seed * 1.7f), 0, Mathf.Cos(seed * 2.3f)) * (radius * 0.35f);
                Shapes.Blob(mb, Matrix4x4.Translate(basePos + capLean + Vector3.up * (height + radius * 0.02f)), new Vector3(radius * 0.36f, radius * 0.14f, radius * 0.36f), capCol, 0.15f, 1.5f, seed, 2, 0.6f);
                int trees = rng.Next(1, 4);
                for (int k = 0; k < trees; k++)
                {
                    Vector3 tp = basePos + capLean + Vector3.up * (height + radius * 0.08f) + new Vector3(Rand(rng, -0.2f, 0.2f), 0, Rand(rng, -0.2f, 0.2f)) * radius;
                    float s = radius * Rand(rng, 0.14f, 0.22f);
                    Shapes.Blob(mb, Matrix4x4.Translate(tp + Vector3.up * s), new Vector3(s, s * 1.1f, s), NumuColor.Hex("#9cc483"), 0.1f, 1.5f, rng.Next(99), 2);
                }
            }
        }
    }
}
