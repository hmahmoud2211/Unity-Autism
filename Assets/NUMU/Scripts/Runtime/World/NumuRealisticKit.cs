using System.Collections.Generic;
using UnityEngine;

namespace Numu.World
{
    /// <summary>
    /// Game-ready geometry for the realistic worlds. Unlike the stylised map kit, these meshes carry proper
    /// UVs for tiling PBR textures and are split per material (bark / leaves / painted metal / rubber / ...).
    /// </summary>
    public static class RealisticKit
    {
        static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        // ================================================================== tubes with texture-space UVs

        /// <summary>Tube along a path with UVs in metres (u around the circumference, v along the length).</summary>
        public static void Tube(MeshBuilder mb, IList<Vector3> path, System.Func<float, float> radiusAt, int sides, float uvScale = 1f, bool capEnd = true)
        {
            int start = mb.VertexCount;
            Vector3 prevN = Vector3.zero;
            float along = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0) along += Vector3.Distance(path[i - 1], path[i]);
                Vector3 t = (i == 0 ? path[1] - path[0] : i == path.Count - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1]).normalized;
                Vector3 n = i == 0 ? Vector3.Cross(t, Mathf.Abs(t.y) > 0.95f ? Vector3.right : Vector3.up).normalized : (prevN - Vector3.Dot(prevN, t) * t).normalized;
                prevN = n;
                Vector3 b = Vector3.Cross(t, n);
                float r = radiusAt(i / (float)(path.Count - 1));
                float circ = Mathf.PI * 2f * r;
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = n * Mathf.Cos(a) + b * Mathf.Sin(a);
                    // Round the circumference to whole texture repeats so the seam stays invisible.
                    float uRepeats = Mathf.Max(1f, Mathf.Round(circ * uvScale));
                    mb.Add(path[i] + dir * r, dir, Color.white, new Vector2(s / (float)sides * uRepeats, along * uvScale));
                }
            }
            int stride = sides + 1;
            for (int i = 0; i < path.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = start + i * stride + s;
                    mb.QuadFacing(a, a + 1, a + stride + 1, a + stride, mb.Normals[a] + mb.Normals[a + stride + 1]);
                }
            if (capEnd)
            {
                int last = start + (path.Count - 1) * stride;
                Vector3 t = (path[path.Count - 1] - path[path.Count - 2]).normalized;
                int c = mb.Add(path[path.Count - 1] + t * radiusAt(1f) * 0.3f, t, Color.white, new Vector2(0.5f, 0.5f));
                for (int s = 0; s < sides; s++) mb.TriFacing(c, last + s, last + s + 1, t);
            }
        }

        static List<Vector3> Curve(Vector3 start, Vector3 dir, float length, Vector3 bendTowards, float bend, int segments)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                pts.Add(start + dir * (length * t) + bendTowards * (bend * t * t * length));
            }
            return pts;
        }

        // ================================================================== trees

        public struct TreeShape
        {
            public float height;
            public float crownRadius;
            public float crownBase;     // fraction of height where the crown starts
            public int mainBranches;
            public float leafCardSize;
            public float leafDensity;
        }

        public static TreeShape Broadleaf(float height) => new TreeShape
        {
            height = height, crownRadius = height * 0.42f, crownBase = 0.27f, mainBranches = 7, leafCardSize = height * 0.16f, leafDensity = 1.9f,
        };

        /// <summary>
        /// Deciduous park tree: bark-textured trunk and branches plus alpha-tested leaf-cluster cards.
        /// Leaf normals are bent outward from the crown centre so the canopy shades as one soft volume.
        /// </summary>
        public static void Tree(MeshBuilder bark, MeshBuilder leaves, TreeShape s, int seed)
        {
            var rng = new System.Random(seed);
            float h = s.height;
            float r0 = h * 0.035f;
            Vector3 lean = new Vector3(Rand(rng, -0.06f, 0.06f), 0, Rand(rng, -0.06f, 0.06f));
            var trunk = Curve(Vector3.down * 0.15f, (Vector3.up + lean).normalized, h * 0.82f, new Vector3(Rand(rng, -1, 1), 0, Rand(rng, -1, 1)).normalized, 0.06f, 10);
            Tube(bark, trunk, t => r0 * (t < 0.06f ? Mathf.Lerp(1.7f, 1f, t / 0.06f) : Mathf.Lerp(1f, 0.25f, t)), 12, 1.2f);
            // Root flare lumps.
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Rand(rng, -0.4f, 0.4f);
                Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Tube(bark, new List<Vector3> { d * r0 * 0.5f + Vector3.up * 0.35f, d * r0 * 1.6f + Vector3.up * 0.05f, d * r0 * 2.6f + Vector3.down * 0.1f }, t => r0 * Mathf.Lerp(0.55f, 0.18f, t), 6, 1.2f);
            }

            Vector3 crownCenter = Vector3.up * (h * (s.crownBase + (1f - s.crownBase) * 0.45f));
            var tips = new List<(Vector3 p, Vector3 dir, float size)>();
            for (int b = 0; b < s.mainBranches; b++)
            {
                float az = (b + Rand(rng, -0.3f, 0.3f)) / s.mainBranches * Mathf.PI * 2f;
                float tStart = Rand(rng, s.crownBase - 0.04f, s.crownBase + 0.2f);
                Vector3 start = trunk[Mathf.Clamp(Mathf.RoundToInt(tStart * 10f / 0.82f), 1, trunk.Count - 1)];
                float elev = Rand(rng, 35f, 62f) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(az) * Mathf.Cos(elev), Mathf.Sin(elev), Mathf.Sin(az) * Mathf.Cos(elev));
                float len = h * Rand(rng, 0.32f, 0.44f);
                var path = Curve(start, dir, len, Vector3.up, 0.25f, 6);
                float rb = r0 * Rand(rng, 0.42f, 0.55f);
                Tube(bark, path, t => rb * Mathf.Lerp(1f, 0.2f, t), 8, 1.2f);
                tips.Add((path[path.Count - 1], dir, 1f));
                int subs = 2 + rng.Next(3);
                for (int k = 0; k < subs; k++)
                {
                    float ts = Rand(rng, 0.35f, 0.85f);
                    Vector3 sp = path[Mathf.RoundToInt(ts * (path.Count - 1))];
                    Vector3 side = Vector3.Cross(dir, Vector3.up).normalized * (k % 2 == 0 ? 1f : -1f);
                    Vector3 sd = (dir * 0.6f + side * Rand(rng, 0.5f, 0.9f) + Vector3.up * Rand(rng, 0.2f, 0.6f)).normalized;
                    float sl = len * Rand(rng, 0.35f, 0.55f);
                    var spath = Curve(sp, sd, sl, Vector3.up, 0.2f, 4);
                    Tube(bark, spath, t => rb * 0.5f * Mathf.Lerp(1f, 0.2f, t), 6, 1.2f);
                    tips.Add((spath[spath.Count - 1], sd, 0.85f));
                    tips.Add((spath[spath.Count / 2], sd, 0.7f));
                }
                tips.Add((path[path.Count / 2], dir, 0.75f));
            }
            tips.Add((trunk[trunk.Count - 1], Vector3.up, 1f));

            // Leaf clusters around branch tips, plus fill to round out the crown silhouette.
            var centers = new List<(Vector3 p, float size)>();
            foreach (var (p, dir, size) in tips)
            {
                int n = Mathf.RoundToInt(3 * s.leafDensity * size) + 1;
                for (int i = 0; i < n; i++)
                    centers.Add((p + new Vector3(Rand(rng, -1, 1), Rand(rng, -0.5f, 0.8f), Rand(rng, -1, 1)) * s.leafCardSize * 0.45f, size));
            }
            int fill = Mathf.RoundToInt(48 * s.leafDensity);
            for (int i = 0; i < fill; i++)
            {
                Vector3 d = Random3(rng);
                d.y = d.y * 0.75f + 0.1f;
                Vector3 p = crownCenter + Vector3.Scale(d, new Vector3(s.crownRadius, s.crownRadius * 0.78f, s.crownRadius)) * Rand(rng, 0.55f, 0.98f);
                centers.Add((p, 0.9f));
            }
            foreach (var (p, size) in centers) LeafCluster(leaves, p, s.leafCardSize * size * Rand(rng, 0.85f, 1.15f), crownCenter, rng);
        }

        static Vector3 Random3(System.Random rng)
        {
            Vector3 v;
            do v = new Vector3(Rand(rng, -1f, 1f), Rand(rng, -1f, 1f), Rand(rng, -1f, 1f));
            while (v.sqrMagnitude > 1f || v.sqrMagnitude < 0.04f);
            return v.normalized;
        }

        static void LeafCluster(MeshBuilder mb, Vector3 center, float size, Vector3 crownCenter, System.Random rng)
        {
            Vector3 outward = (center - crownCenter).normalized;
            Quaternion baseRot = Quaternion.Euler(Rand(rng, 0, 360), Rand(rng, 0, 360), Rand(rng, 0, 360));
            for (int q = 0; q < 3; q++)
            {
                Quaternion r = baseRot * Quaternion.Euler(q * 60f, q * 37f, 0);
                Vector3 right = r * Vector3.right * (size * 0.5f);
                Vector3 up = r * Vector3.up * (size * 0.5f);
                Vector3[] corners = { center - right - up, center + right - up, center + right + up, center - right + up };
                Vector2[] uvs = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                int i0 = mb.VertexCount;
                for (int k = 0; k < 4; k++)
                {
                    // Bent normals: mostly crown-outward for a soft, volumetric canopy.
                    Vector3 n = ((corners[k] - crownCenter).normalized * 0.75f + outward * 0.25f).normalized;
                    mb.Add(corners[k], n, Color.white, uvs[k]);
                }
                mb.Tri(i0, i0 + 1, i0 + 2);
                mb.Tri(i0, i0 + 2, i0 + 3);
            }
        }

        /// <summary>Rounded garden bush: a dome of leaf-cluster cards over a few short twigs.</summary>
        public static void Bush(MeshBuilder twigs, MeshBuilder leaves, Vector3 basePos, float size, int seed)
        {
            var rng = new System.Random(seed);
            Vector3 centre = basePos + Vector3.up * size * 0.45f;
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Rand(rng, -0.4f, 0.4f);
                Vector3 tip = centre + new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)) * size * 0.35f;
                Tube(twigs, new List<Vector3> { basePos, Vector3.Lerp(basePos, tip, 0.5f) + Vector3.up * 0.1f, tip }, t => size * 0.03f * (1f - t * 0.6f), 5, 2f);
            }
            int cards = Mathf.RoundToInt(10 + size * 8);
            for (int i = 0; i < cards; i++)
            {
                Vector3 d = Random3(rng);
                d.y = Mathf.Abs(d.y) * 0.8f + 0.05f;
                Vector3 p = centre + Vector3.Scale(d, new Vector3(size * 0.55f, size * 0.42f, size * 0.55f)) * Rand(rng, 0.4f, 0.95f);
                LeafCluster(leaves, p, size * Rand(rng, 0.55f, 0.75f), centre - Vector3.up * size * 0.2f, rng);
            }
        }

        /// <summary>Clump of crossed grass cards (UV maps the grass-blade atlas).</summary>
        public static void GrassTuft(MeshBuilder mb, Vector3 basePos, Vector3 groundNormal, float height, float width, int seed)
        {
            var rng = new System.Random(seed);
            float yaw = Rand(rng, 0, 180);
            for (int q = 0; q < 3; q++)
            {
                Quaternion r = Quaternion.FromToRotation(Vector3.up, groundNormal) * Quaternion.Euler(Rand(rng, -8, 8), yaw + q * 60f, 0);
                Vector3 right = r * Vector3.right * (width * 0.5f);
                Vector3 up = r * Vector3.up * height;
                int i0 = mb.VertexCount;
                float u0 = (rng.Next(2)) * 0.5f;
                mb.Add(basePos - right, groundNormal, Color.white, new Vector2(u0, 0));
                mb.Add(basePos + right, groundNormal, Color.white, new Vector2(u0 + 0.5f, 0));
                mb.Add(basePos + right + up, groundNormal, Color.white, new Vector2(u0 + 0.5f, 1));
                mb.Add(basePos - right + up, groundNormal, Color.white, new Vector2(u0, 1));
                mb.Tri(i0, i0 + 1, i0 + 2);
                mb.Tri(i0, i0 + 2, i0 + 3);
            }
        }

        // ================================================================== playground

        /// <summary>
        /// Steel A-frame swing set. Frame goes into <paramref name="frame"/>; returns the local pivot positions
        /// (top bar hook points) for each seat so chains/seats can be built as swinging children.
        /// </summary>
        public static List<Vector3> SwingFrame(MeshBuilder frame, float width, float height, int seats)
        {
            float spread = height * 0.42f;
            foreach (float x in new[] { -width * 0.5f, width * 0.5f })
            {
                Vector3 top = new Vector3(x, height, 0);
                foreach (float z in new[] { -spread, spread })
                    Tube(frame, new List<Vector3> { new Vector3(x * 1.04f, -0.4f, z), Vector3.Lerp(new Vector3(x * 1.04f, -0.4f, z), top, 0.5f), top }, t => 0.035f, 12, 2f);
                Tube(frame, new List<Vector3> { new Vector3(x * 1.03f, height * 0.45f, -spread * 0.55f), new Vector3(x * 1.03f, height * 0.45f, spread * 0.55f) }, t => 0.022f, 10, 2f);
            }
            Tube(frame, new List<Vector3> { new Vector3(-width * 0.5f - 0.12f, height + 0.02f, 0), new Vector3(width * 0.5f + 0.12f, height + 0.02f, 0) }, t => 0.045f, 14, 2f);
            var hooks = new List<Vector3>();
            for (int i = 0; i < seats; i++) hooks.Add(new Vector3(Mathf.Lerp(-width * 0.5f, width * 0.5f, (i + 1f) / (seats + 1f)), height - 0.02f, 0));
            return hooks;
        }

        /// <summary>Two chains (real interlocking links) and a curved rubber strap seat hanging from local origin.</summary>
        public static void SwingSeat(MeshBuilder chain, MeshBuilder seat, float length, float seatWidth)
        {
            foreach (float x in new[] { -seatWidth * 0.42f, seatWidth * 0.42f })
            {
                float linkLen = 0.055f;
                int links = Mathf.RoundToInt(length / (linkLen * 0.78f));
                for (int i = 0; i < links; i++)
                {
                    Vector3 c = new Vector3(x, -i * linkLen * 0.78f - linkLen * 0.5f, 0);
                    Quaternion r = Quaternion.Euler(0, i % 2 == 0 ? 0f : 90f, 90f);
                    Shapes.Torus(chain, Matrix4x4.TRS(c, r, new Vector3(1f, 1f, 1.7f)), linkLen * 0.32f, 0.006f, 10, 5, Color.white);
                }
            }
            // Curved strap seat: a thick flexible band, dipping in the middle like real rubber seats.
            int segs = 12;
            float w = seatWidth, d = 0.17f, th = 0.025f;
            int start = seat.VertexCount;
            for (int i = 0; i <= segs; i++)
            {
                float u = i / (float)segs;
                float x = Mathf.Lerp(-w * 0.5f, w * 0.5f, u);
                float sag = Mathf.Sin(u * Mathf.PI) * 0.07f;
                float y = -length - 0.02f - sag;
                foreach (var (dy, dz, n) in new[] { (0f, -d * 0.5f, Vector3.up), (0f, d * 0.5f, Vector3.up), (-th, d * 0.5f, Vector3.down), (-th, -d * 0.5f, Vector3.down) })
                    seat.Add(new Vector3(x, y + dy, dz), n, Color.white, new Vector2(u * 2f, (dz + d * 0.5f) / d * 0.6f + (dy < 0 ? 0.6f : 0f)));
            }
            for (int i = 0; i < segs; i++)
            {
                int a = start + i * 4, b = a + 4;
                seat.QuadFacing(a, b, b + 1, a + 1, Vector3.up);
                seat.QuadFacing(a + 3, a + 2, b + 2, b + 3, Vector3.down);
                seat.QuadFacing(a + 1, b + 1, b + 2, a + 2, Vector3.forward);
                seat.QuadFacing(a, a + 3, b + 3, b, Vector3.back);
            }
            seat.RecalculateNormals(start, seat.Triangles.Count - segs * 24);
        }

        /// <summary>Slide tower: posts and deck, ladder, guard rails and a smooth moulded chute.</summary>
        public static void SlideTower(MeshBuilder metal, MeshBuilder wood, MeshBuilder plastic, float deckHeight)
        {
            float half = 0.6f;
            foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                Tube(metal, new List<Vector3> { new Vector3(c.x * half, -0.4f, c.y * half), new Vector3(c.x * half, deckHeight + 1.0f, c.y * half) }, t => 0.04f, 12, 2f);
            // Deck planks.
            for (int i = 0; i < 6; i++)
            {
                float z = -half + 0.05f + i * (half * 2f - 0.1f) / 5f;
                Shapes.Box(wood, Matrix4x4.TRS(new Vector3(0, deckHeight, z), Quaternion.identity, Vector3.one), new Vector3(half * 2f + 0.08f, 0.045f, 0.19f), Color.white);
            }
            // Guard rails on three sides (the front stays open for the chute).
            foreach (float y in new[] { deckHeight + 0.45f, deckHeight + 0.9f })
            {
                Tube(metal, new List<Vector3> { new Vector3(-half, y, half), new Vector3(half, y, half) }, t => 0.022f, 10, 2f);
                Tube(metal, new List<Vector3> { new Vector3(-half, y, -half), new Vector3(-half, y, half) }, t => 0.022f, 10, 2f);
                Tube(metal, new List<Vector3> { new Vector3(half, y, -half), new Vector3(half, y, -0.25f) }, t => 0.022f, 10, 2f);
            }
            // Ladder on the back.
            foreach (float x in new[] { -0.25f, 0.25f })
                Tube(metal, new List<Vector3> { new Vector3(x, -0.3f, half + 0.75f), new Vector3(x, deckHeight + 0.6f, half - 0.02f) }, t => 0.025f, 10, 2f);
            for (int i = 1; i <= 5; i++)
            {
                float t = i / 6f;
                Vector3 a = Vector3.Lerp(new Vector3(-0.25f, -0.3f, half + 0.75f), new Vector3(-0.25f, deckHeight + 0.6f, half - 0.02f), t);
                Tube(metal, new List<Vector3> { a, a + Vector3.right * 0.5f }, tt => 0.018f, 8, 2f);
            }
            // Chute: a U-shaped trough curving down to the ground.
            var path = new List<Vector3>();
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                float drop = deckHeight * (1f - Mathf.SmoothStep(0f, 1f, t)) + 0.28f;
                path.Add(new Vector3(-half * 0.1f, drop + (t > 0.92f ? (t - 0.92f) * 1.2f : 0f), -half - t * deckHeight * 1.55f));
            }
            Chute(plastic, path, 0.27f, 0.17f, 0.018f);
        }

        static void Chute(MeshBuilder mb, IList<Vector3> path, float halfWidth, float wall, float thickness)
        {
            int prof = 14;
            int start = mb.VertexCount, tStart = mb.Triangles.Count;
            for (int layer = 0; layer < 2; layer++)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 t = (i == 0 ? path[1] - path[0] : i == path.Count - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1]).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, t).normalized;
                    Vector3 up = Vector3.Cross(t, side).normalized;
                    for (int k = 0; k <= prof; k++)
                    {
                        float a = Mathf.PI * k / prof;
                        Vector3 d = side * Mathf.Cos(a) * halfWidth - up * Mathf.Sin(a) * wall;
                        Vector3 off = layer == 1 ? -up * thickness : Vector3.zero;
                        mb.Add(path[i] + d + off, layer == 0 ? up : -up, Color.white, new Vector2(k / (float)prof, i / (float)(path.Count - 1)));
                    }
                }
            }
            int stride = prof + 1, layerSize = stride * path.Count;
            for (int layer = 0; layer < 2; layer++)
                for (int i = 0; i < path.Count - 1; i++)
                    for (int k = 0; k < prof; k++)
                    {
                        int a = start + layer * layerSize + i * stride + k;
                        Vector3 expect = mb.Normals[a];
                        mb.QuadFacing(a, a + 1, a + stride + 1, a + stride, expect);
                    }
            // Rolled lips along both edges.
            foreach (int k in new[] { 0, prof })
            {
                var lip = new List<Vector3>();
                for (int i = 0; i < path.Count; i++) lip.Add(mb.Vertices[start + i * stride + k]);
                Tube(mb, lip, t => thickness * 1.6f, 8, 1f, false);
            }
            mb.RecalculateNormals(start, tStart);
        }

        /// <summary>Low-poly Victorian-style lantern (LOD1 stand-in for scanned lamps): cap, finial, glass body, base.</summary>
        public static void Lantern(MeshBuilder metal, MeshBuilder glass, Vector3 centre, float height)
        {
            float h = height, r = height * 0.32f;
            var m = Matrix4x4.TRS(centre + Vector3.down * h * 0.5f, Quaternion.Euler(0, 45f, 0), Vector3.one);
            Shapes.Lathe(metal, m, new[] { new Vector2(0, 0), new Vector2(r * 0.55f, 0), new Vector2(r * 0.75f, h * 0.12f), new Vector2(0, h * 0.12f) }, 4, Color.white, false);
            Shapes.Lathe(glass, m * Matrix4x4.Translate(Vector3.up * h * 0.12f), new[] { new Vector2(0, 0), new Vector2(r * 0.72f, 0), new Vector2(r, h * 0.55f), new Vector2(0, h * 0.55f) }, 4, Color.white, false);
            Shapes.Lathe(metal, m * Matrix4x4.Translate(Vector3.up * h * 0.67f), new[] { new Vector2(0, 0), new Vector2(r * 1.25f, 0), new Vector2(r * 0.3f, h * 0.26f), new Vector2(0, h * 0.3f) }, 4, Color.white, false);
            Shapes.Lathe(metal, m * Matrix4x4.Translate(Vector3.up * h * 0.97f), new[] { new Vector2(0, 0), new Vector2(r * 0.18f, 0), new Vector2(0, h * 0.12f) }, 6, Color.white, false);
        }

        /// <summary>Flat rounded slab (safety surfacing, paving pads) with planar UVs in metres.</summary>
        public static void Slab(MeshBuilder mb, Vector2 size, float corner, float thickness, float uvScale)
        {
            var outline = Shapes.RoundedRect(size.x, size.y, corner, 8);
            int start = mb.VertexCount;
            var tris = Shapes.Triangulate(outline);
            foreach (var p in outline) mb.Add(new Vector3(p.x, 0f, p.y), Vector3.up, Color.white, p * uvScale);
            for (int i = 0; i < tris.Count; i += 3) mb.TriFacing(start + tris[i], start + tris[i + 1], start + tris[i + 2], Vector3.up);
            int n = outline.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % n];
                Vector3 nrm = new Vector3(b.y - a.y, 0, -(b.x - a.x)).normalized;
                int i0 = mb.Add(new Vector3(a.x, 0, a.y), nrm, Color.white, new Vector2(i * 0.1f, 0));
                int i1 = mb.Add(new Vector3(b.x, 0, b.y), nrm, Color.white, new Vector2((i + 1) * 0.1f, 0));
                int i2 = mb.Add(new Vector3(b.x, -thickness, b.y), nrm, Color.white, new Vector2((i + 1) * 0.1f, thickness * uvScale));
                int i3 = mb.Add(new Vector3(a.x, -thickness, a.y), nrm, Color.white, new Vector2(i * 0.1f, thickness * uvScale));
                mb.QuadFacing(i0, i1, i2, i3, nrm);
            }
        }
    }
}
