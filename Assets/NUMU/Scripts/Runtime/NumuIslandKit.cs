using System.Collections.Generic;
using UnityEngine;

namespace Numu
{
    public class IslandSpec
    {
        public string Name;
        /// <summary>Top-edge outline in the island's local XZ plane.</summary>
        public List<Vector2> Outline = new List<Vector2>();
        public float TopY = 4f;
        public float BottomY = -3.5f;
        public float Dome = 0.3f;
        public int Seed = 1;
        public Color Grass = NumuColor.Hex("#68b347");
        public Color GrassLight = NumuColor.Hex("#97cc55");
        public Color GrassDark = NumuColor.Hex("#3e8b3c");
        public Color[] RockPalette =
        {
            NumuColor.Hex("#ecd3ae"), NumuColor.Hex("#dcc2a2"), NumuColor.Hex("#c4b5a5"), NumuColor.Hex("#a7a39d"), NumuColor.Hex("#8f9494"),
        };
        public Color[] RockAccents = { NumuColor.Hex("#c9c4bd"), NumuColor.Hex("#b3aca4"), NumuColor.Hex("#f2dcbc") };
        public float SkirtWidth = 7f;
        public bool Skirt = true;
        public float BlockScale = 1f;
    }

    public class IslandShape
    {
        public List<Vector3> Rim = new List<Vector3>();
        public List<Vector3> RimOut = new List<Vector3>();
        public Vector3 Centroid;
        public float TopY;
        public float Perimeter;

        /// <summary>Rim point and outward direction at a fraction of the perimeter.</summary>
        public void Sample(float t, out Vector3 point, out Vector3 outward)
        {
            t = Mathf.Repeat(t, 1f) * Rim.Count;
            int i = Mathf.FloorToInt(t) % Rim.Count;
            int j = (i + 1) % Rim.Count;
            float f = t - Mathf.Floor(t);
            point = Vector3.Lerp(Rim[i], Rim[j], f);
            outward = Vector3.Slerp(RimOut[i], RimOut[j], f).normalized;
        }

        /// <summary>Rim sample closest to a world-space XZ position.</summary>
        public int ClosestRimIndex(Vector3 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < Rim.Count; i++)
            {
                float d = (new Vector2(Rim[i].x - p.x, Rim[i].z - p.z)).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
    }

    public static class IslandKit
    {
        static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        public static IslandShape Build(IslandSpec s, MeshBuilder top, MeshBuilder cliff, MeshBuilder skirt)
        {
            var rng = new System.Random(s.Seed * 7919 + 17);
            var ctrl = new List<Vector2>(s.Outline);
            if (Shapes.SignedArea(ctrl) < 0f) ctrl.Reverse();
            var ctrl3 = new List<Vector3>();
            foreach (var p in ctrl) ctrl3.Add(new Vector3(p.x, 0f, p.y));
            var ring = Shapes.SampleSpline(ctrl3, 0.5f, true);
            int n = ring.Count;
            var outN = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 t = (ring[(i + 1) % n] - ring[(i - 1 + n) % n]).normalized;
                outN[i] = new Vector3(t.z, 0f, -t.x);
            }

            // Area centroid.
            float area = 0f, cx = 0f, cz = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % n];
                float cr = a.x * b.z - b.x * a.z;
                area += cr;
                cx += (a.x + b.x) * cr;
                cz += (a.z + b.z) * cr;
            }
            area *= 0.5f;
            Vector3 centroid = new Vector3(cx / (6f * area), 0f, cz / (6f * area));

            var shape = new IslandShape { Centroid = centroid + Vector3.up * s.TopY, TopY = s.TopY };
            float perimeter = 0f;
            for (int i = 0; i < n; i++) perimeter += Vector3.Distance(ring[i], ring[(i + 1) % n]);
            shape.Perimeter = perimeter;
            for (int i = 0; i < n; i++)
            {
                shape.Rim.Add(ring[i] + Vector3.up * s.TopY);
                shape.RimOut.Add(outN[i]);
            }

            BuildTop(s, top, ring, outN, centroid, rng);
            BuildCliff(s, cliff, ring, outN, perimeter, rng);
            if (s.Skirt && skirt != null) BuildSkirt(s, skirt, ring, outN);
            return shape;
        }

        static float Drip(int i, int seed)
        {
            float v = NumuNoise.Value3(i * 0.33f, seed * 1.37f, 0.5f);
            return Mathf.Max(0f, v - 0.42f) * 1.1f;
        }

        static void BuildTop(IslandSpec s, MeshBuilder mb, List<Vector3> ring, Vector3[] outN, Vector3 c, System.Random rng)
        {
            int n = ring.Count;
            int vStart = mb.VertexCount, tStart = mb.Triangles.Count;
            float[] fr = { 0.2f, 0.42f, 0.62f, 0.78f, 0.9f, 1f };
            Color GrassAt(Vector3 p, float f)
            {
                float k = NumuNoise.Value3(p.x * 0.11f + s.Seed, p.z * 0.11f, 0.3f);
                float k2 = NumuNoise.Value3(p.x * 0.35f, p.z * 0.35f + s.Seed, 2.1f);
                Color g = Color.Lerp(s.Grass, s.GrassLight, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.78f, k)));
                return NumuColor.Shade(g, 0.94f + k2 * 0.1f);
            }
            float H(Vector3 p, float f) => s.TopY + s.Dome * (1f - f * f) + (NumuNoise.Value3(p.x * 0.18f, p.z * 0.18f, s.Seed) - 0.5f) * 0.22f * (1f - f * 0.7f);

            Vector3 cp = new Vector3(c.x, H(c, 0f), c.z);
            int center = mb.Add(cp, Vector3.up, GrassAt(cp, 0f));
            var rings = new List<int>();
            foreach (float f in fr)
            {
                rings.Add(mb.VertexCount);
                for (int i = 0; i < n; i++)
                {
                    Vector3 l0 = ring[i] - outN[i] * 0.55f;
                    Vector3 p = c + (l0 - c) * f;
                    p.y = H(p, f);
                    mb.Add(p, Vector3.up, GrassAt(p, f));
                }
            }
            // Rounded, slightly dripping grass lip.
            var lip = new (float outward, float drop, float drip, Color col)[]
            {
                (0.06f, 0.1f, 0f, s.GrassLight),
                (0.34f, 0.42f, 0f, NumuColor.Shade(s.GrassLight, 0.97f)),
                (0.3f, 0.8f, 1f, s.Grass),
                (-0.05f, 0.98f, 0.85f, s.GrassDark),
            };
            foreach (var l in lip)
            {
                rings.Add(mb.VertexCount);
                for (int i = 0; i < n; i++)
                {
                    float d = Drip(i, s.Seed) * l.drip;
                    Vector3 p = ring[i] + outN[i] * l.outward;
                    p.y = s.TopY - l.drop - d;
                    mb.Add(p, Vector3.up, l.col);
                }
            }
            for (int i = 0; i < n; i++) mb.TriFacing(center, rings[0] + i, rings[0] + (i + 1) % n, Vector3.up);
            for (int r = 0; r < rings.Count - 1; r++)
            {
                for (int i = 0; i < n; i++)
                {
                    int i1 = (i + 1) % n;
                    Vector3 o = outN[i];
                    Vector3 expected = r < fr.Length - 1 ? Vector3.up
                        : r == fr.Length - 1 ? (Vector3.up + o * 0.5f)
                        : r == fr.Length ? (Vector3.up * 0.4f + o)
                        : r == fr.Length + 1 ? o
                        : (o * 0.4f - Vector3.up);
                    mb.QuadFacing(rings[r] + i, rings[r] + i1, rings[r + 1] + i1, rings[r + 1] + i, expected);
                }
            }
            mb.RecalculateNormals(vStart, tStart);
        }

        static void BuildCliff(IslandSpec s, MeshBuilder mb, List<Vector3> ring, Vector3[] outN, float perimeter, System.Random rng)
        {
            int n = ring.Count;
            // Dark core wall that fills any gap between stones.
            Color core = NumuColor.Hex("#8a8079");
            float wallTop = s.TopY - 0.75f;
            var ys = new[] { wallTop, Mathf.Lerp(wallTop, s.BottomY, 0.5f), s.BottomY };
            int baseIdx = mb.VertexCount;
            foreach (float y in ys)
            {
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = ring[i] - outN[i] * 0.35f;
                    p.y = y;
                    mb.Add(p, outN[i], core);
                }
            }
            for (int r = 0; r < ys.Length - 1; r++)
            {
                for (int i = 0; i < n; i++)
                {
                    int i1 = (i + 1) % n;
                    mb.QuadFacing(baseIdx + r * n + i, baseIdx + r * n + i1, baseIdx + (r + 1) * n + i1, baseIdx + (r + 1) * n + i, outN[i]);
                }
            }

            // Arc-length table to place stones along the rim.
            var cum = new float[n + 1];
            for (int i = 0; i < n; i++) cum[i + 1] = cum[i] + Vector3.Distance(ring[i], ring[(i + 1) % n]);
            void At(float arc, out Vector3 p, out Vector3 o)
            {
                arc = Mathf.Repeat(arc, perimeter);
                int i = 0;
                while (i < n - 1 && cum[i + 1] < arc) i++;
                float f = (arc - cum[i]) / Mathf.Max(1e-5f, cum[i + 1] - cum[i]);
                p = Vector3.Lerp(ring[i], ring[(i + 1) % n], f);
                o = Vector3.Slerp(outN[i], outN[(i + 1) % n], f).normalized;
            }

            var layers = new List<(float top, float bot)>();
            float yTop = s.TopY - 0.72f;
            int li = 0;
            while (yTop > s.BottomY + 0.2f && li < 8)
            {
                float h = (li == 0 ? Rand(rng, 1.2f, 1.6f) : Rand(rng, 1.5f, 2.3f)) * s.BlockScale;
                float yBot = Mathf.Max(yTop - h, s.BottomY);
                layers.Add((yTop, yBot));
                yTop = yBot;
                li++;
            }

            for (int l = 0; l < layers.Count; l++)
            {
                var (lt, lb) = layers[l];
                float arc = Rand(rng, 0f, 1.5f);
                Color baseCol = s.RockPalette[Mathf.Min(l, s.RockPalette.Length - 1)];
                // Stop just short of a full loop so the last stone doesn't double up on the first.
                float end = arc + perimeter - 0.6f;
                while (arc < end)
                {
                    float w = Rand(rng, 1.5f, 2.9f) * s.BlockScale;
                    w = Mathf.Min(w, end - arc + 0.6f);
                    At(arc + w * 0.5f, out Vector3 p, out Vector3 o);
                    float prot = (l == 0 ? Rand(rng, -0.05f, 0.18f) : Rand(rng, 0.0f, 0.4f)) + l * 0.14f;
                    float depth = Rand(rng, 1.3f, 2.0f);
                    float hh = (lt - lb) + Rand(rng, 0.12f, 0.4f);
                    Vector3 ctr = p + o * (prot - depth * 0.5f + 0.18f);
                    ctr.y = (lt + lb) * 0.5f + Rand(rng, -0.12f, 0.12f);
                    Quaternion rot = Quaternion.LookRotation(o, Vector3.up) * Quaternion.Euler(Rand(rng, -4f, 4f), Rand(rng, -8f, 8f), Rand(rng, -3f, 3f));
                    Color col = rng.NextDouble() < 0.22 ? s.RockAccents[rng.Next(s.RockAccents.Length)] : baseCol;
                    col = NumuColor.Jitter(col, rng, 0.06f);
                    var size = new Vector3(w + 0.3f, hh, depth);
                    float radius = Mathf.Min(0.42f, Mathf.Min(size.x, size.y) * 0.28f);
                    Shapes.RoundedBox(mb, Matrix4x4.TRS(ctr, rot, Vector3.one), size, radius, 2, col, 0.05f, rng.Next(1000));
                    arc += w;
                }
            }
        }

        static void BuildSkirt(IslandSpec s, MeshBuilder mb, List<Vector3> ring, Vector3[] outN)
        {
            int n = ring.Count;
            var rows = new (float outward, float y, Color col)[]
            {
                (0.2f, -0.6f, NumuColor.Hex("#efe2b4")),
                (2.2f, -1.1f, NumuColor.Hex("#e4d6a6")),
                (s.SkirtWidth, -4.2f, NumuColor.Hex("#c4bd94")),
            };
            int baseIdx = mb.VertexCount;
            foreach (var r in rows)
            {
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = ring[i] + outN[i] * r.outward;
                    p.y = r.y;
                    mb.Add(p, Vector3.up, r.col);
                }
            }
            for (int r = 0; r < rows.Length - 1; r++)
            {
                for (int i = 0; i < n; i++)
                {
                    int i1 = (i + 1) % n;
                    mb.QuadFacing(baseIdx + r * n + i, baseIdx + r * n + i1, baseIdx + (r + 1) * n + i1, baseIdx + (r + 1) * n + i, Vector3.up);
                }
            }
        }

        /// <summary>Rounded boulder rising from the water with an optional mossy cap.</summary>
        public static void Boulder(MeshBuilder mb, Vector3 basePos, Vector3 radii, int seed, float moss = 0.5f, Color? rock = null)
        {
            Color rockCol = rock ?? NumuColor.Hex("#8a909a");
            Color rockLight = NumuColor.Shade(rockCol, 1.18f);
            Color mossCol = NumuColor.Hex("#6fb648");
            Color mossLight = NumuColor.Hex("#9fd25a");
            Shapes.Blob(mb, Matrix4x4.TRS(basePos, Quaternion.Euler(0, seed * 37f, 0), Vector3.one), radii, rockCol, 0.16f, 1.3f, seed, 3, 0f, d =>
            {
                float up = d.y + NumuNoise.Signed(d * 2.3f + Vector3.one * seed) * 0.15f;
                if (moss > 0f && up > 1f - moss)
                {
                    return Color.Lerp(mossCol, mossLight, Mathf.InverseLerp(1f - moss, 1f, up));
                }
                return Color.Lerp(rockCol, rockLight, Mathf.Clamp01(d.y * 0.5f + 0.5f));
            });
        }
    }
}
