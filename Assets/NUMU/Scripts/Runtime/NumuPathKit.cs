using System.Collections.Generic;
using UnityEngine;

namespace Numu
{
    /// <summary>Curved cobbled paths and paved plazas draped over island tops.</summary>
    public static class PathKit
    {
        public static readonly Color PathBase = NumuColor.Hex("#e3cfa6");
        public static readonly Color PathEdge = NumuColor.Hex("#cdb68c");
        public static readonly Color[] Cobbles =
        {
            NumuColor.Hex("#f6ead2"), NumuColor.Hex("#efdfc2"), NumuColor.Hex("#f9f0de"), NumuColor.Hex("#e8d5b4"),
        };
        public static readonly Color[] GreyCobbles =
        {
            NumuColor.Hex("#e9e3da"), NumuColor.Hex("#ddd5ca"), NumuColor.Hex("#f2ede6"), NumuColor.Hex("#d3cbbf"),
        };

        static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        /// <summary>
        /// Ribbon path through control points. <paramref name="ground"/> snaps a point onto the terrain surface.
        /// </summary>
        public static void StonePath(MeshBuilder mb, IList<Vector3> ctrl, float width, int seed, System.Func<Vector3, Vector3> ground, Color[] cobbles = null)
        {
            var rng = new System.Random(seed);
            cobbles ??= Cobbles;
            var pts = Shapes.SampleSpline(ctrl, 0.35f);
            for (int layer = 0; layer < 2; layer++)
            {
                float w = layer == 0 ? width + 0.4f : width;
                float lift = layer == 0 ? 0.03f : 0.05f;
                Color col = layer == 0 ? PathEdge : PathBase;
                int start = mb.VertexCount;
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector3 t = (i == 0 ? pts[1] - pts[0] : i == pts.Count - 1 ? pts[i] - pts[i - 1] : pts[i + 1] - pts[i - 1]);
                    t.y = 0f;
                    t.Normalize();
                    Vector3 side = new Vector3(t.z, 0f, -t.x);
                    float wobble = 1f + (NumuNoise.Value3(i * 0.3f, seed, layer) - 0.5f) * 0.25f;
                    Vector3 l = ground(pts[i] - side * w * 0.5f * wobble) + Vector3.up * lift;
                    Vector3 r = ground(pts[i] + side * w * 0.5f * wobble) + Vector3.up * lift;
                    mb.Add(l, Vector3.up, col);
                    mb.Add(r, Vector3.up, col);
                }
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    int a = start + i * 2;
                    mb.QuadFacing(a, a + 1, a + 3, a + 2, Vector3.up);
                }
                // Rounded end caps.
                foreach (int end in new[] { 0, pts.Count - 1 })
                {
                    Vector3 c = ground(pts[end]) + Vector3.up * lift;
                    for (int k = 0; k < 8; k++)
                    {
                        float a0 = k / 8f * Mathf.PI * 2f, a1 = (k + 1) / 8f * Mathf.PI * 2f;
                        int ci = mb.Add(c, Vector3.up, col);
                        int i0 = mb.Add(ground(c + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * w * 0.5f) + Vector3.up * lift, Vector3.up, col);
                        int i1 = mb.Add(ground(c + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * w * 0.5f) + Vector3.up * lift, Vector3.up, col);
                        mb.TriFacing(ci, i0, i1, Vector3.up);
                    }
                }
            }
            // Cobbles.
            float rowStep = 0.62f;
            float acc = 0f;
            int row = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                acc += Vector3.Distance(pts[i - 1], pts[i]);
                if (acc < rowStep) continue;
                acc = 0f;
                row++;
                Vector3 t = pts[i] - pts[i - 1];
                t.y = 0;
                t.Normalize();
                Vector3 side = new Vector3(t.z, 0f, -t.x);
                int across = Mathf.Max(2, Mathf.RoundToInt(width / 0.62f));
                for (int k = 0; k < across; k++)
                {
                    float f = (k + 0.5f + (row % 2) * 0.35f) / across - 0.5f;
                    if (Mathf.Abs(f) > 0.45f) continue;
                    Vector3 p = pts[i] + side * (f * width) + t * Rand(rng, -0.08f, 0.08f);
                    Cobble(mb, ground(p) + Vector3.up * 0.06f, Rand(rng, 0.2f, 0.27f), cobbles[rng.Next(cobbles.Length)], rng.Next(9999));
                }
            }
        }

        static void Cobble(MeshBuilder mb, Vector3 p, float r, Color c, int seed)
        {
            Shapes.Blob(mb, Matrix4x4.TRS(p, Quaternion.Euler(0, seed % 360, 0), Vector3.one), new Vector3(r, r * 0.22f, r * 0.85f), c, 0.12f, 2.4f, seed, 1, 0.6f);
        }

        /// <summary>Elliptical paved area filled with staggered cobbles.</summary>
        public static void Plaza(MeshBuilder mb, Vector3 center, float rx, float rz, int seed, System.Func<Vector3, Vector3> ground, Color[] cobbles = null, Color? baseColor = null)
        {
            var rng = new System.Random(seed);
            cobbles ??= Cobbles;
            Color baseCol = baseColor ?? PathBase;
            int rings = 6, seg = 40;
            for (int layer = 0; layer < 2; layer++)
            {
                float grow = layer == 0 ? 0.35f : 0f;
                float lift = layer == 0 ? 0.03f : 0.05f;
                Color col = layer == 0 ? PathEdge : baseCol;
                int start = mb.VertexCount;
                int ci = mb.Add(ground(center) + Vector3.up * lift, Vector3.up, col);
                for (int j = 1; j <= rings; j++)
                {
                    float f = j / (float)rings;
                    for (int i = 0; i < seg; i++)
                    {
                        float a = i / (float)seg * Mathf.PI * 2f;
                        float wob = 1f + (NumuNoise.Value3(Mathf.Cos(a) * 2f, Mathf.Sin(a) * 2f, seed) - 0.5f) * 0.12f;
                        Vector3 p = center + new Vector3(Mathf.Cos(a) * (rx + grow) * f * wob, 0, Mathf.Sin(a) * (rz + grow) * f * wob);
                        mb.Add(ground(p) + Vector3.up * lift, Vector3.up, col);
                    }
                }
                for (int i = 0; i < seg; i++) mb.TriFacing(ci, start + 1 + i, start + 1 + (i + 1) % seg, Vector3.up);
                for (int j = 0; j < rings - 1; j++)
                    for (int i = 0; i < seg; i++)
                    {
                        int a = start + 1 + j * seg + i, b = start + 1 + j * seg + (i + 1) % seg;
                        mb.QuadFacing(a, b, b + seg, a + seg, Vector3.up);
                    }
            }
            float step = 0.6f;
            int rowI = 0;
            for (float z = -rz; z <= rz; z += step * 0.86f, rowI++)
            {
                for (float x = -rx + (rowI % 2) * step * 0.5f; x <= rx; x += step)
                {
                    float e = (x * x) / (rx * rx) + (z * z) / (rz * rz);
                    if (e > 0.86f) continue;
                    Vector3 p = center + new Vector3(x + Rand(rng, -0.06f, 0.06f), 0, z + Rand(rng, -0.06f, 0.06f));
                    Cobble(mb, ground(p) + Vector3.up * 0.06f, Rand(rng, 0.22f, 0.29f), cobbles[rng.Next(cobbles.Length)], rng.Next(9999));
                }
            }
        }
    }
}
