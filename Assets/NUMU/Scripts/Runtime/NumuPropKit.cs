using System.Collections.Generic;
using UnityEngine;

namespace Numu
{
    /// <summary>Furniture, playground equipment, vehicles and shop props. Local space, base at y = 0, front faces -Z.</summary>
    public static class PropKit
    {
        static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        static Matrix4x4 TRS(Vector3 p, Quaternion r) => Matrix4x4.TRS(p, r, Vector3.one);
        static Color Wood(System.Random r) => NumuColor.Jitter(StructureKit.Wood[r.Next(StructureKit.Wood.Length)], r, 0.05f);

        public static readonly Color Metal = NumuColor.Hex("#5f6b78");
        public static readonly Color MetalLight = NumuColor.Hex("#c9d2dc");
        public static readonly Color[] Fruit =
        {
            NumuColor.Hex("#ff9a1f"), NumuColor.Hex("#e8413b"), NumuColor.Hex("#8cc63f"), NumuColor.Hex("#ffd43b"), NumuColor.Hex("#8a5bc4"),
        };

        // ------------------------------------------------------------------ playground

        /// <summary>Half-pipe slide channel swept along a path.</summary>
        public static void SlideChannel(MeshBuilder mb, IList<Vector3> path, float radius, Color color)
        {
            int prof = 9;
            int start = mb.VertexCount, tStart = mb.Triangles.Count;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 t = (i == 0 ? path[1] - path[0] : i == path.Count - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, t).normalized;
                Vector3 up = Vector3.Cross(t, side).normalized;
                for (int k = 0; k <= prof; k++)
                {
                    float a = Mathf.PI * (k / (float)prof);
                    Vector3 d = side * Mathf.Cos(a) - up * Mathf.Sin(a) * 0.75f;
                    mb.Add(path[i] + d * radius, -d, color);
                }
                for (int k = 0; k <= prof; k++)
                {
                    float a = Mathf.PI * (k / (float)prof);
                    Vector3 d = side * Mathf.Cos(a) - up * Mathf.Sin(a) * 0.75f;
                    mb.Add(path[i] + d * (radius + 0.08f) + Vector3.down * 0.02f, d, NumuColor.Shade(color, 0.85f));
                }
            }
            int stride = (prof + 1) * 2;
            for (int i = 0; i < path.Count - 1; i++)
            {
                for (int k = 0; k < prof; k++)
                {
                    int a = start + i * stride + k;
                    int b = a + stride;
                    mb.QuadFacing(a, a + 1, b + 1, b, mb.Normals[a]);
                    int oa = a + prof + 1, ob = b + prof + 1;
                    mb.QuadFacing(oa, oa + 1, ob + 1, ob, mb.Normals[oa]);
                }
            }
            mb.RecalculateNormals(start, tStart);
            // Rolled rims.
            foreach (int k in new[] { 0, prof })
            {
                var rim = new List<Vector3>();
                for (int i = 0; i < path.Count; i++) rim.Add(mb.Vertices[start + i * stride + k]);
                Shapes.Tube(mb, rim, 0.09f, 6, NumuColor.Shade(color, 1.08f));
            }
        }

        public static void Playground(MeshBuilder mb, int seed)
        {
            var rng = new System.Random(seed);
            Color post = NumuColor.Hex("#f2b544");
            float half = 1.0f, deck = 1.8f, postH = 3.4f;
            foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(c.x * half, postH * 0.5f, c.y * half)), new Vector3(0.24f, postH, 0.24f), 0.06f, 1, post);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, deck, 0)), new Vector3(half * 2f + 0.4f, 0.2f, half * 2f + 0.4f), 0.05f, 1, Wood(rng));
            for (int i = 0; i < 5; i++)
            {
                float x = -half + i * half * 0.5f;
                Shapes.Box(mb, Matrix4x4.Translate(new Vector3(x, deck + 0.45f, -half)), new Vector3(0.08f, 0.8f, 0.08f), NumuColor.Hex("#ffffff"));
            }
            Shapes.Beam(mb, new Vector3(-half, deck + 0.85f, -half), new Vector3(half, deck + 0.85f, -half), 0.07f, 6, NumuColor.Hex("#4aa3df"));
            ArchitectureKit.PyramidRoof(mb, Matrix4x4.Translate(new Vector3(0, postH, 0)), 4, half + 0.1f, 1.3f, 0.3f, NumuColor.Hex("#e84a3c"), seed);
            // Red slide to the left, yellow curly slide to the right.
            var red = new List<Vector3>();
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                red.Add(new Vector3(-half - 0.2f - t * 3.2f, deck + 0.25f - deck * (t * t * (3f - 2f * t)) + 0.15f, -0.2f - t * 0.6f));
            }
            SlideChannel(mb, red, 0.45f, NumuColor.Hex("#e8453c"));
            var yellow = new List<Vector3>();
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                float a = t * 1.6f;
                yellow.Add(new Vector3(half + 0.2f + Mathf.Sin(a) * 2.2f, deck + 0.25f - deck * t + 0.15f, -0.2f - (1f - Mathf.Cos(a)) * 1.6f));
            }
            SlideChannel(mb, yellow, 0.45f, NumuColor.Hex("#ffc43a"));
            // Ladder at the back.
            foreach (float x in new[] { -0.4f, 0.4f })
                Shapes.Beam(mb, new Vector3(x, 0, half + 1.0f), new Vector3(x, deck + 0.2f, half + 0.05f), 0.07f, 6, NumuColor.Hex("#4aa3df"));
            for (int i = 1; i < 5; i++)
            {
                float t = i / 5f;
                Vector3 c = Vector3.Lerp(new Vector3(0, 0, half + 1.0f), new Vector3(0, deck + 0.2f, half + 0.05f), t);
                Shapes.Beam(mb, c + Vector3.left * 0.4f, c + Vector3.right * 0.4f, 0.05f, 6, NumuColor.Hex("#ffffff"));
            }
            // Sand patch.
            Shapes.Blob(mb, Matrix4x4.Translate(new Vector3(0, -0.05f, 0)), new Vector3(3.6f, 0.12f, 2.6f), NumuColor.Hex("#f3dfb0"), 0.05f, 1f, seed, 2, 0.9f);
        }

        public static void SwingSet(MeshBuilder mb, int seed, int seats = 2)
        {
            var rng = new System.Random(seed);
            float w = 1.3f * seats + 0.6f, h = 2.6f;
            foreach (float s in new[] { -0.5f, 0.5f })
            {
                Vector3 top = new Vector3(s * w, h, 0);
                Shapes.Beam(mb, top, new Vector3(s * (w + 0.3f), 0, -0.9f), 0.1f, 6, Wood(rng));
                Shapes.Beam(mb, top, new Vector3(s * (w + 0.3f), 0, 0.9f), 0.1f, 6, Wood(rng));
            }
            Shapes.Beam(mb, new Vector3(-w * 0.5f - 0.1f, h, 0), new Vector3(w * 0.5f + 0.1f, h, 0), 0.11f, 8, Wood(rng));
            for (int i = 0; i < seats; i++)
            {
                float x = -w * 0.5f + 0.6f + i * 1.3f + 0.35f;
                Color seat = i % 2 == 0 ? NumuColor.Hex("#e8453c") : NumuColor.Hex("#4aa3df");
                foreach (float o in new[] { -0.3f, 0.3f })
                    Shapes.Beam(mb, new Vector3(x + o, h, 0), new Vector3(x + o, 0.75f, 0), 0.025f, 4, StructureKit.Rope);
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(x, 0.7f, 0)), new Vector3(0.8f, 0.1f, 0.38f), 0.04f, 1, seat);
            }
        }

        public static void Fountain(MeshBuilder mb, MeshBuilder water, int seed)
        {
            Color stone = NumuColor.Hex("#f1ece6");
            Color stoneDark = NumuColor.Hex("#d7cfc6");
            Shapes.Lathe(mb, Matrix4x4.identity, new[]
            {
                new Vector2(0, 0), new Vector2(2.0f, 0), new Vector2(2.1f, 0.2f), new Vector2(2.1f, 0.55f), new Vector2(1.95f, 0.65f),
                new Vector2(1.8f, 0.55f), new Vector2(1.75f, 0.3f), new Vector2(0, 0.3f),
            }, 28, stone, true);
            Shapes.Lathe(mb, Matrix4x4.identity, new[]
            {
                new Vector2(0, 0.3f), new Vector2(0.38f, 0.3f), new Vector2(0.28f, 1.2f), new Vector2(0.3f, 1.4f), new Vector2(0, 1.4f),
            }, 16, stoneDark, true);
            Shapes.Lathe(mb, Matrix4x4.identity, new[]
            {
                new Vector2(0, 1.3f), new Vector2(0.4f, 1.3f), new Vector2(1.0f, 1.55f), new Vector2(1.05f, 1.75f), new Vector2(0.92f, 1.78f),
                new Vector2(0.85f, 1.62f), new Vector2(0, 1.62f),
            }, 24, stone, true);
            Shapes.Lathe(mb, Matrix4x4.identity, new[]
            {
                new Vector2(0, 1.6f), new Vector2(0.16f, 1.6f), new Vector2(0.12f, 2.3f), new Vector2(0.22f, 2.45f), new Vector2(0, 2.6f),
            }, 12, stoneDark, true);
            // Water surfaces and jets.
            Shapes.Cylinder(water, Matrix4x4.Translate(new Vector3(0, 0.48f, 0)), 1.82f, 1.82f, 0.02f, 28, NumuColor.Hex("#7fd3f2"));
            Shapes.Cylinder(water, Matrix4x4.Translate(new Vector3(0, 1.66f, 0)), 0.9f, 0.9f, 0.02f, 20, NumuColor.Hex("#8fdcf5"));
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var arc = new List<Vector3>();
                for (int k = 0; k <= 8; k++)
                {
                    float t = k / 8f;
                    arc.Add(new Vector3(0, 2.5f, 0) + dir * (t * 0.95f) + Vector3.up * (0.35f * Mathf.Sin(t * Mathf.PI) - t * 0.85f));
                }
                Shapes.Tube(water, arc, 0.05f, 5, NumuColor.Hex("#d8f4ff"));
            }
            Shapes.Blob(water, Matrix4x4.Translate(new Vector3(0, 2.7f, 0)), new Vector3(0.18f, 0.3f, 0.18f), NumuColor.Hex("#eefaff"), 0.1f, 2f, seed, 2);
        }

        // ------------------------------------------------------------------ furniture

        public static void Bench(MeshBuilder mb, int seed)
        {
            var rng = new System.Random(seed);
            Color leg = NumuColor.Hex("#4b5560");
            for (int i = 0; i < 3; i++)
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.62f, -0.18f + i * 0.2f)), new Vector3(2.2f, 0.1f, 0.18f), 0.04f, 1, Wood(rng));
            for (int i = 0; i < 2; i++)
                Shapes.RoundedBox(mb, TRS(new Vector3(0, 0.95f + i * 0.25f, 0.3f + i * 0.04f), Quaternion.Euler(-12, 0, 0)), new Vector3(2.2f, 0.16f, 0.08f), 0.04f, 1, Wood(rng));
            foreach (float x in new[] { -0.9f, 0.9f })
            {
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(x, 0.3f, 0)), new Vector3(0.1f, 0.6f, 0.6f), 0.04f, 1, leg);
                Shapes.RoundedBox(mb, TRS(new Vector3(x, 0.95f, 0.3f), Quaternion.Euler(-12, 0, 0)), new Vector3(0.1f, 0.7f, 0.1f), 0.03f, 1, leg);
            }
        }

        public static void LampPost(MeshBuilder mb, MeshBuilder glow, float height = 3.2f)
        {
            Color iron = NumuColor.Hex("#3c4a55");
            Shapes.Lathe(mb, Matrix4x4.identity, new[]
            {
                new Vector2(0, 0), new Vector2(0.3f, 0), new Vector2(0.28f, 0.25f), new Vector2(0.1f, 0.4f), new Vector2(0.08f, height - 0.4f),
                new Vector2(0.16f, height - 0.3f), new Vector2(0, height - 0.3f),
            }, 10, iron, true);
            Shapes.Cylinder(glow, Matrix4x4.Translate(new Vector3(0, height - 0.3f, 0)), 0.22f, 0.3f, 0.55f, 8, NumuColor.Hex("#ffe3a0"));
            Shapes.Lathe(mb, Matrix4x4.Translate(new Vector3(0, height + 0.2f, 0)), new[] { new Vector2(0, 0), new Vector2(0.42f, 0), new Vector2(0.05f, 0.35f), new Vector2(0, 0.4f) }, 8, iron, false);
            Shapes.Sphere(mb, new Vector3(0, height + 0.65f, 0), 0.08f, iron, 1);
        }

        public static void Mailbox(MeshBuilder mb)
        {
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.55f, 0)), new Vector3(0.16f, 1.1f, 0.16f), 0.04f, 1, StructureKit.Wood[0]);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 1.3f, 0)), new Vector3(0.6f, 0.55f, 0.9f), 0.22f, 2, NumuColor.Hex("#3f86d9"));
            Shapes.RoundedBox(mb, TRS(new Vector3(0.34f, 1.45f, 0.1f), Quaternion.Euler(0, 0, 0)), new Vector3(0.05f, 0.4f, 0.12f), 0.02f, 1, NumuColor.Hex("#e8453c"));
            Shapes.RoundedBox(mb, TRS(new Vector3(0.34f, 1.62f, 0.2f), Quaternion.identity), new Vector3(0.05f, 0.14f, 0.3f), 0.02f, 1, NumuColor.Hex("#e8453c"));
        }

        public static void ParasolTable(MeshBuilder mb, int seed)
        {
            var rng = new System.Random(seed);
            Shapes.Lathe(mb, Matrix4x4.identity, new[] { new Vector2(0, 0), new Vector2(0.35f, 0), new Vector2(0.08f, 0.15f), new Vector2(0.07f, 0.75f), new Vector2(0.75f, 0.78f), new Vector2(0.75f, 0.88f), new Vector2(0, 0.88f) }, 16, Wood(rng), false);
            Shapes.Cylinder(mb, Matrix4x4.Translate(new Vector3(0, 0.88f, 0)), 0.04f, 0.04f, 1.6f, 6, NumuColor.Hex("#f6efe6"));
            int start = mb.VertexCount;
            Color a = NumuColor.Hex("#b98ae6"), b = NumuColor.Hex("#f4e9ff");
            Shapes.Lathe(mb, Matrix4x4.Translate(new Vector3(0, 2.0f, 0)), new[] { new Vector2(0, -0.05f), new Vector2(1.35f, -0.05f), new Vector2(1.35f, 0f), new Vector2(0.15f, 0.55f), new Vector2(0, 0.6f) }, 16, a, false);
            for (int i = start; i < mb.VertexCount; i++)
            {
                Vector3 v = mb.Vertices[i];
                float ang = Mathf.Atan2(v.z, v.x);
                if (Mathf.Repeat(ang / (Mathf.PI * 2f) * 8f, 1f) > 0.5f) mb.Colors[i] = b;
            }
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -1.1f : 1.1f;
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(x, 0.45f, 0)), new Vector3(0.5f, 0.08f, 0.5f), 0.03f, 1, Wood(rng));
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(x + (i == 0 ? -0.22f : 0.22f), 0.75f, 0)), new Vector3(0.08f, 0.6f, 0.5f), 0.03f, 1, Wood(rng));
                foreach (var l in new[] { new Vector2(-0.2f, -0.2f), new Vector2(0.2f, -0.2f), new Vector2(-0.2f, 0.2f), new Vector2(0.2f, 0.2f) })
                    Shapes.Box(mb, Matrix4x4.Translate(new Vector3(x + l.x, 0.22f, l.y)), new Vector3(0.06f, 0.44f, 0.06f), StructureKit.WoodDark);
            }
        }

        /// <summary>Picket or rail fence along a polyline in world space (ground-sampled by the caller).</summary>
        public static void Fence(MeshBuilder mb, IList<Vector3> pts, bool picket, int seed)
        {
            var rng = new System.Random(seed);
            Color white = NumuColor.Hex("#fdfaf3");
            Color c = picket ? white : StructureKit.Wood[0];
            float spacing = picket ? 0.32f : 1.4f;
            for (int s = 0; s < pts.Count - 1; s++)
            {
                Vector3 a = pts[s], b = pts[s + 1];
                Vector3 d = b - a;
                float len = new Vector3(d.x, 0, d.z).magnitude;
                Quaternion rot = Quaternion.LookRotation(new Vector3(d.z, 0, -d.x).normalized, Vector3.up);
                int n = Mathf.Max(1, Mathf.RoundToInt(len / spacing));
                for (int i = 0; i <= n; i++)
                {
                    if (i == n && s < pts.Count - 2) continue;
                    Vector3 p = Vector3.Lerp(a, b, i / (float)n);
                    if (picket)
                    {
                        var shape = new List<Vector2> { new Vector2(-0.09f, 0), new Vector2(0.09f, 0), new Vector2(0.09f, 0.82f), new Vector2(0, 0.98f), new Vector2(-0.09f, 0.82f) };
                        Shapes.Extrude(mb, TRS(p, rot), shape, 0.06f, white);
                    }
                    else
                    {
                        Shapes.RoundedBox(mb, TRS(p + Vector3.up * 0.5f, rot), new Vector3(0.2f, 1.0f, 0.2f), 0.05f, 1, Wood(rng));
                    }
                }
                Vector3 off = rot * Vector3.forward * 0.05f;
                foreach (float y in picket ? new[] { 0.3f, 0.65f } : new[] { 0.4f, 0.78f })
                {
                    Shapes.RoundedBox(mb, TRS((a + b) * 0.5f + Vector3.up * y - off, Quaternion.LookRotation(new Vector3(d.x, d.y, d.z).normalized, Vector3.up)),
                        new Vector3(0.06f, picket ? 0.08f : 0.13f, d.magnitude + 0.1f), 0.03f, 1, c);
                }
            }
        }

        public static void Planter(MeshBuilder mb, float w, float d, int seed)
        {
            var rng = new System.Random(seed);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.3f, 0)), new Vector3(w, 0.6f, d), 0.06f, 1, Wood(rng));
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.58f, 0)), new Vector3(w - 0.2f, 0.06f, d - 0.2f), 0.03f, 1, NumuColor.Hex("#6b4a32"));
            for (int i = 0; i < Mathf.RoundToInt(w * d * 5f); i++)
            {
                Vector3 p = new Vector3(Rand(rng, -w * 0.4f, w * 0.4f), 0.6f, Rand(rng, -d * 0.35f, d * 0.35f));
                if (rng.NextDouble() < 0.4) Shapes.Sphere(mb, p + Vector3.up * 0.12f, 0.2f, NumuColor.Hex("#4fa548"), 1);
                else NatureKit.Flower(mb, p, 0.22f, NatureKit.FlowerColors[rng.Next(NatureKit.FlowerColors.Length)], rng.Next(9999));
            }
        }

        // ------------------------------------------------------------------ school

        public static void SchoolBus(MeshBuilder mb, MeshBuilder glass)
        {
            Color yellow = NumuColor.Hex("#f8c22e");
            Color dark = NumuColor.Hex("#333a44");
            float L = 5.2f, W = 2.1f, H = 2.0f;
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.55f + H * 0.5f, 0)), new Vector3(L, H, W), 0.45f, 3, yellow);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(L * 0.5f - 0.2f, 0.95f, 0)), new Vector3(1.2f, 1.0f, W - 0.05f), 0.35f, 2, yellow);
            Shapes.Box(mb, Matrix4x4.Translate(new Vector3(0, 1.15f, 0)), new Vector3(L + 0.04f, 0.1f, W + 0.04f), dark);
            for (int i = 0; i < 4; i++)
            {
                float x = -L * 0.5f + 0.75f + i * 1.0f;
                foreach (float z in new[] { -W * 0.5f - 0.01f, W * 0.5f + 0.01f })
                    Shapes.RoundedBox(glass, Matrix4x4.Translate(new Vector3(x, 1.95f, z)), new Vector3(0.82f, 0.62f, 0.06f), 0.08f, 1, NumuColor.Hex("#9ad5f5"));
            }
            Shapes.RoundedBox(glass, TRS(new Vector3(L * 0.5f + 0.01f, 1.95f, 0), Quaternion.Euler(0, 90, 0)), new Vector3(W - 0.4f, 0.7f, 0.06f), 0.08f, 1, NumuColor.Hex("#9ad5f5"));
            foreach (float x in new[] { -L * 0.3f, L * 0.32f })
                foreach (float z in new[] { -W * 0.5f, W * 0.5f })
                {
                    // Cylinders extend along +Z from their origin once rotated.
                    Shapes.Cylinder(mb, TRS(new Vector3(x, 0.5f, z - 0.15f), Quaternion.Euler(90, 0, 0)), 0.48f, 0.48f, 0.3f, 16, dark);
                    float hubZ = z < 0f ? z - 0.2f : z + 0.16f;
                    Shapes.Cylinder(mb, TRS(new Vector3(x, 0.5f, hubZ), Quaternion.Euler(90, 0, 0)), 0.22f, 0.22f, 0.04f, 12, MetalLight);
                }
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(L * 0.5f + 0.55f, 0.55f, 0)), new Vector3(0.15f, 0.25f, W + 0.1f), 0.05f, 1, MetalLight);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(-L * 0.5f - 0.05f, 0.65f, 0)), new Vector3(0.15f, 0.25f, W + 0.1f), 0.05f, 1, MetalLight);
            foreach (float z in new[] { -0.7f, 0.7f })
                Shapes.Sphere(mb, new Vector3(L * 0.5f + 0.62f, 0.95f, z), 0.15f, NumuColor.Hex("#fff6d0"), 1);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(-0.3f, 0.55f + H + 0.05f, 0)), new Vector3(L * 0.7f, 0.12f, W * 0.6f), 0.05f, 1, NumuColor.Hex("#f5d76a"));
        }

        public static void Blackboard(MeshBuilder mb, int seed)
        {
            var rng = new System.Random(seed);
            Color wood = Wood(rng);
            foreach (float x in new[] { -1.15f, 1.15f })
            {
                Shapes.RoundedBox(mb, TRS(new Vector3(x, 1.2f, -0.25f), Quaternion.Euler(-10, 0, 0)), new Vector3(0.14f, 2.5f, 0.14f), 0.04f, 1, wood);
                Shapes.RoundedBox(mb, TRS(new Vector3(x, 1.0f, 0.45f), Quaternion.Euler(25, 0, 0)), new Vector3(0.12f, 2.2f, 0.12f), 0.04f, 1, wood);
            }
            Shapes.RoundedBox(mb, TRS(new Vector3(0, 1.65f, -0.28f), Quaternion.Euler(-10, 0, 0)), new Vector3(2.4f, 1.5f, 0.12f), 0.06f, 1, wood);
            Shapes.Box(mb, TRS(new Vector3(0, 1.65f, -0.36f), Quaternion.Euler(-10, 0, 0)), new Vector3(2.15f, 1.27f, 0.04f), NumuColor.Hex("#2f6a52"));
            Shapes.RoundedBox(mb, TRS(new Vector3(0, 0.88f, -0.48f), Quaternion.Euler(-10, 0, 0)), new Vector3(2.3f, 0.08f, 0.2f), 0.03f, 1, wood);
            // Chalk doodles: a sun, a plus sum and wavy lines.
            Quaternion r = Quaternion.Euler(-10, 0, 0);
            Vector3 o = new Vector3(0, 1.65f, -0.39f);
            Color chalk = NumuColor.Hex("#f4fbf6");
            var wave = new List<Vector3>();
            for (int i = 0; i <= 10; i++) wave.Add(o + r * new Vector3(-0.85f + i * 0.08f, -0.35f + Mathf.Sin(i * 1.2f) * 0.06f, 0));
            Shapes.Tube(mb, wave, 0.022f, 4, chalk);
            var circ = new List<Vector3>();
            for (int i = 0; i <= 12; i++) circ.Add(o + r * new Vector3(0.55f + Mathf.Cos(i / 12f * 6.283f) * 0.18f, 0.22f + Mathf.Sin(i / 12f * 6.283f) * 0.18f, 0));
            Shapes.Tube(mb, circ, 0.022f, 4, chalk);
            Shapes.Tube(mb, new List<Vector3> { o + r * new Vector3(-0.7f, 0.25f, 0), o + r * new Vector3(-0.4f, 0.25f, 0) }, 0.022f, 4, chalk);
            Shapes.Tube(mb, new List<Vector3> { o + r * new Vector3(-0.55f, 0.1f, 0), o + r * new Vector3(-0.55f, 0.4f, 0) }, 0.022f, 4, chalk);
            Shapes.Tube(mb, new List<Vector3> { o + r * new Vector3(-0.2f, 0.3f, 0), o + r * new Vector3(0.05f, 0.3f, 0) }, 0.022f, 4, chalk);
            Shapes.Tube(mb, new List<Vector3> { o + r * new Vector3(-0.2f, 0.18f, 0), o + r * new Vector3(0.05f, 0.18f, 0) }, 0.022f, 4, chalk);
        }

        public static void DeskWithBooks(MeshBuilder mb, int seed)
        {
            var rng = new System.Random(seed);
            Color wood = Wood(rng);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.8f, 0)), new Vector3(1.6f, 0.12f, 1.0f), 0.04f, 1, wood);
            foreach (var l in new[] { new Vector2(-0.68f, -0.4f), new Vector2(0.68f, -0.4f), new Vector2(-0.68f, 0.4f), new Vector2(0.68f, 0.4f) })
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(l.x, 0.4f, l.y)), new Vector3(0.1f, 0.8f, 0.1f), 0.03f, 1, StructureKit.WoodDark);
            Color[] cols = { NumuColor.Hex("#e8453c"), NumuColor.Hex("#3f86d9"), NumuColor.Hex("#5fbf5a"), NumuColor.Hex("#ffc43a"), NumuColor.Hex("#a16ad6") };
            float y = 0.86f;
            for (int i = 0; i < 3; i++)
            {
                float h = Rand(rng, 0.12f, 0.2f);
                Shapes.RoundedBox(mb, TRS(new Vector3(-0.3f, y + h * 0.5f, 0), Quaternion.Euler(0, Rand(rng, -15, 15), 0)), new Vector3(0.6f, h, 0.45f), 0.03f, 1, cols[rng.Next(cols.Length)]);
                y += h;
            }
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0.4f, 1.06f, 0)), new Vector3(0.38f, 0.38f, 0.38f), 0.06f, 1, cols[rng.Next(cols.Length)]);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0.45f, 1.42f, 0.05f)), new Vector3(0.3f, 0.3f, 0.3f), 0.06f, 1, cols[rng.Next(cols.Length)]);
        }

        // ------------------------------------------------------------------ market

        public static void ProduceCrate(MeshBuilder mb, Color fruit, int seed, float w = 1.2f, float d = 0.8f, float tilt = 0f)
        {
            var rng = new System.Random(seed);
            Quaternion r = Quaternion.Euler(tilt, 0, 0);
            Color wood = Wood(rng);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.1f + i * 0.17f;
                Shapes.RoundedBox(mb, TRS(r * new Vector3(0, y, -d * 0.5f), r), new Vector3(w, 0.13f, 0.06f), 0.02f, 1, wood);
                Shapes.RoundedBox(mb, TRS(r * new Vector3(0, y, d * 0.5f), r), new Vector3(w, 0.13f, 0.06f), 0.02f, 1, wood);
                Shapes.RoundedBox(mb, TRS(r * new Vector3(-w * 0.5f, y, 0), r), new Vector3(0.06f, 0.13f, d), 0.02f, 1, wood);
                Shapes.RoundedBox(mb, TRS(r * new Vector3(w * 0.5f, y, 0), r), new Vector3(0.06f, 0.13f, d), 0.02f, 1, wood);
            }
            Shapes.Box(mb, TRS(r * new Vector3(0, 0.05f, 0), r), new Vector3(w, 0.06f, d), NumuColor.Shade(wood, 0.8f));
            float fr = 0.13f;
            for (int layer = 0; layer < 2; layer++)
                for (float x = -w * 0.5f + fr * 1.3f; x < w * 0.5f - fr; x += fr * 1.9f)
                    for (float z = -d * 0.5f + fr * 1.3f; z < d * 0.5f - fr; z += fr * 1.9f)
                    {
                        Vector3 p = r * new Vector3(x + layer * fr * 0.9f + Rand(rng, -0.03f, 0.03f), 0.45f + layer * fr * 1.2f, z + Rand(rng, -0.03f, 0.03f));
                        Shapes.Sphere(mb, p, fr * Rand(rng, 0.9f, 1.1f), NumuColor.Jitter(fruit, rng, 0.07f), 1);
                    }
        }

        public static void ProduceStand(MeshBuilder mb, int seed)
        {
            var rng = new System.Random(seed);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.45f, 0)), new Vector3(2.8f, 0.9f, 1.4f), 0.06f, 1, Wood(rng));
            for (int i = 0; i < 2; i++)
            {
                var m = new MeshBuilder();
                ProduceCrate(m, Fruit[rng.Next(Fruit.Length)], seed + i, 1.25f, 0.9f, -18f);
                mb.Append(m, Matrix4x4.Translate(new Vector3(-0.68f + i * 1.36f, 0.9f, 0.05f)));
            }
            var back = new MeshBuilder();
            ProduceCrate(back, Fruit[rng.Next(Fruit.Length)], seed + 7, 2.6f, 0.6f, -10f);
            mb.Append(back, Matrix4x4.Translate(new Vector3(0, 1.35f, 0.55f)));
        }

        public static void ShoppingCart(MeshBuilder mb, int seed)
        {
            var rng = new System.Random(seed);
            Color wire = NumuColor.Hex("#7d8fa6");
            float w = 0.9f, l = 1.3f, h = 0.75f, y0 = 0.55f;
            Vector3[] bottom = { new Vector3(-w / 2, y0, -l / 2 + 0.15f), new Vector3(w / 2, y0, -l / 2 + 0.15f), new Vector3(w / 2, y0, l / 2), new Vector3(-w / 2, y0, l / 2) };
            Vector3[] top = { new Vector3(-w / 2 - 0.05f, y0 + h, -l / 2), new Vector3(w / 2 + 0.05f, y0 + h, -l / 2), new Vector3(w / 2 + 0.05f, y0 + h, l / 2 + 0.1f), new Vector3(-w / 2 - 0.05f, y0 + h, l / 2 + 0.1f) };
            for (int i = 0; i < 4; i++)
            {
                Shapes.Beam(mb, bottom[i], bottom[(i + 1) % 4], 0.03f, 4, wire);
                Shapes.Beam(mb, top[i], top[(i + 1) % 4], 0.035f, 4, wire);
                Shapes.Beam(mb, bottom[i], top[i], 0.03f, 4, wire);
            }
            for (int k = 1; k < 6; k++)
            {
                float t = k / 6f;
                Shapes.Beam(mb, Vector3.Lerp(bottom[0], bottom[3], t), Vector3.Lerp(top[0], top[3], t), 0.02f, 3, wire);
                Shapes.Beam(mb, Vector3.Lerp(bottom[1], bottom[2], t), Vector3.Lerp(top[1], top[2], t), 0.02f, 3, wire);
            }
            for (int k = 1; k < 4; k++)
            {
                float t = k / 4f;
                Shapes.Beam(mb, Vector3.Lerp(bottom[0], bottom[1], t), Vector3.Lerp(top[0], top[1], t), 0.02f, 3, wire);
            }
            Shapes.Beam(mb, top[2] + Vector3.up * 0.1f, top[3] + Vector3.up * 0.1f, 0.06f, 8, NumuColor.Hex("#e8453c"));
            foreach (var b in bottom)
            {
                Shapes.Beam(mb, b, new Vector3(b.x, 0.18f, b.z), 0.03f, 4, wire);
                Shapes.Cylinder(mb, TRS(new Vector3(b.x, 0.12f, b.z), Quaternion.Euler(0, 0, 90)), 0.12f, 0.12f, 0.08f, 10, NumuColor.Hex("#333a44"));
            }
            // Groceries.
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = new Vector3(Rand(rng, -0.3f, 0.3f), y0 + 0.35f + Rand(rng, 0f, 0.4f), Rand(rng, -0.4f, 0.4f));
                if (i % 3 == 0) Shapes.RoundedBox(mb, TRS(p, Quaternion.Euler(0, Rand(rng, 0, 90), 0)), new Vector3(0.25f, 0.35f, 0.18f), 0.04f, 1, NatureKit.FlowerColors[rng.Next(NatureKit.FlowerColors.Length)]);
                else Shapes.Sphere(mb, p, 0.15f, Fruit[rng.Next(Fruit.Length)], 1);
            }
            Shapes.Blob(mb, Matrix4x4.Translate(new Vector3(0.15f, y0 + 0.9f, 0.2f)), new Vector3(0.12f, 0.35f, 0.12f), NumuColor.Hex("#5fbf5a"), 0.1f, 2f, seed, 1);
        }

        public static void CrateStack(MeshBuilder mb, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = i < 2 ? new Vector3(i * 0.95f - 0.45f, 0.4f, 0) : new Vector3(0, 1.2f, 0);
                Shapes.RoundedBox(mb, TRS(p, Quaternion.Euler(0, Rand(rng, -8, 8), 0)), new Vector3(0.85f, 0.8f, 0.85f), 0.06f, 1, Wood(rng));
                Shapes.Box(mb, TRS(p + Vector3.back * 0.43f, Quaternion.identity), new Vector3(0.7f, 0.08f, 0.02f), StructureKit.WoodDark);
            }
        }

        public static void StripedStall(MeshBuilder mb, int seed)
        {
            var rng = new System.Random(seed);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.45f, 0)), new Vector3(2.2f, 0.9f, 1.1f), 0.06f, 1, Wood(rng));
            foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                Shapes.Beam(mb, new Vector3(c.x * 1.0f, 0, c.y * 0.5f), new Vector3(c.x * 1.0f, 2.1f + (c.y > 0 ? 0.3f : 0f), c.y * 0.5f), 0.05f, 6, StructureKit.WoodDark);
            for (int i = 0; i < 7; i++)
            {
                float x = -1.2f + (i + 0.5f) * 2.4f / 7f;
                Shapes.RoundedBox(mb, TRS(new Vector3(x, 2.3f, 0), Quaternion.Euler(-15, 0, 0)), new Vector3(2.4f / 7f + 0.01f, 0.08f, 1.5f), 0.03f, 1, i % 2 == 0 ? NumuColor.Hex("#ee4f5a") : NumuColor.Hex("#fff7ef"));
            }
            var m = new MeshBuilder();
            ProduceCrate(m, Fruit[rng.Next(Fruit.Length)], seed, 0.9f, 0.7f, -15f);
            mb.Append(m, Matrix4x4.Translate(new Vector3(-0.5f, 0.9f, 0)));
            var m2 = new MeshBuilder();
            ProduceCrate(m2, Fruit[rng.Next(Fruit.Length)], seed + 1, 0.9f, 0.7f, -15f);
            mb.Append(m2, Matrix4x4.Translate(new Vector3(0.5f, 0.9f, 0)));
        }

        public static void ChalkSign(MeshBuilder mb, int seed)
        {
            var rng = new System.Random(seed);
            Color wood = Wood(rng);
            Shapes.RoundedBox(mb, TRS(new Vector3(0, 0.75f, -0.2f), Quaternion.Euler(-14, 0, 0)), new Vector3(0.95f, 1.5f, 0.08f), 0.04f, 1, wood);
            Shapes.Box(mb, TRS(new Vector3(0, 0.8f, -0.25f), Quaternion.Euler(-14, 0, 0)), new Vector3(0.75f, 1.15f, 0.03f), NumuColor.Hex("#2f4a5a"));
            Shapes.RoundedBox(mb, TRS(new Vector3(0, 0.75f, 0.2f), Quaternion.Euler(14, 0, 0)), new Vector3(0.95f, 1.5f, 0.08f), 0.04f, 1, wood);
            Quaternion r = Quaternion.Euler(-14, 0, 0);
            Vector3 o = new Vector3(0, 0.8f, -0.27f);
            for (int i = 0; i < 4; i++)
                Shapes.Box(mb, TRS(o + r * new Vector3(0, 0.35f - i * 0.22f, 0), r), new Vector3(0.5f - (i % 2) * 0.15f, 0.04f, 0.01f), NumuColor.Hex("#f4fbf6"));
        }

        public static void ShelfOfGoods(MeshBuilder mb, int seed)
        {
            var rng = new System.Random(seed);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 1.0f, 0.2f)), new Vector3(1.6f, 2.0f, 0.12f), 0.04f, 1, Wood(rng));
            for (int s = 0; s < 3; s++)
            {
                float y = 0.3f + s * 0.6f;
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, y, 0)), new Vector3(1.6f, 0.08f, 0.5f), 0.02f, 1, Wood(rng));
                for (float x = -0.65f; x < 0.7f; x += 0.26f)
                    Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(x, y + 0.2f, 0)), new Vector3(0.2f, 0.34f, 0.24f), 0.04f, 1, NatureKit.FlowerColors[rng.Next(NatureKit.FlowerColors.Length)]);
            }
            foreach (float x in new[] { -0.8f, 0.8f })
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(x, 1.0f, 0)), new Vector3(0.08f, 2.0f, 0.55f), 0.02f, 1, Wood(rng));
        }

        // ------------------------------------------------------------------ boat

        public static void Sailboat(MeshBuilder mb, MeshBuilder sail, int seed)
        {
            Color hull = NumuColor.Hex("#f5efe4");
            Color stripe = NumuColor.Hex("#3f86d9");
            Color wood = NumuColor.Hex("#b77a45");
            // Hull: squashed blob with a flat deck.
            int start = mb.VertexCount, tStart = mb.Triangles.Count;
            Shapes.Blob(mb, Matrix4x4.identity, new Vector3(1.0f, 0.55f, 2.4f), hull, 0f, 1f, seed, 3, 0f, d => d.y > 0.1f && d.y < 0.35f ? stripe : hull);
            for (int i = start; i < mb.VertexCount; i++)
            {
                Vector3 v = mb.Vertices[i];
                if (v.z > 1.2f) v.x *= Mathf.Lerp(1f, 0.35f, (v.z - 1.2f) / 1.2f);
                v.y = Mathf.Min(v.y, 0.3f);
                mb.Vertices[i] = v;
            }
            mb.RecalculateNormals(start, tStart);
            Shapes.Box(mb, Matrix4x4.Translate(new Vector3(0, 0.31f, -0.1f)), new Vector3(1.5f, 0.05f, 3.4f), wood);
            Shapes.Cylinder(mb, Matrix4x4.Translate(new Vector3(0, 0.3f, 0.2f)), 0.07f, 0.06f, 4.2f, 8, wood);
            Shapes.Beam(mb, new Vector3(0, 0.95f, 0.2f), new Vector3(0, 0.95f, -1.9f), 0.05f, 6, wood);
            // Triangular sail in the YZ plane, double-sided via the flag material.
            int s0 = sail.VertexCount;
            Vector3 a = new Vector3(0, 1.0f, 0.15f), b = new Vector3(0, 4.3f, 0.15f), c = new Vector3(0, 1.0f, -1.85f);
            int rows = 6;
            for (int j = 0; j <= rows; j++)
            {
                float t = j / (float)rows;
                Vector3 l = Vector3.Lerp(a, b, t), r = Vector3.Lerp(c, b, t);
                for (int i = 0; i <= 4; i++)
                {
                    float u = i / 4f;
                    Vector3 p = Vector3.Lerp(l, r, u) + Vector3.right * (Mathf.Sin(u * Mathf.PI) * 0.25f * (1f - t));
                    sail.Add(p, Vector3.right, NumuColor.WithAlpha(NumuColor.Hex("#fffaf0"), u * 0.5f));
                }
            }
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < 4; i++)
                {
                    int q = s0 + j * 5 + i;
                    sail.QuadFacing(q, q + 1, q + 6, q + 5, Vector3.right);
                }
            sail.RecalculateNormals(s0, sail.Triangles.Count - rows * 4 * 6);
            var star = Shapes.Star(0.42f, 0.18f, 5);
            Shapes.Extrude(sail, Matrix4x4.TRS(new Vector3(0.22f, 2.0f, -0.5f), Quaternion.Euler(0, -90, 0), Vector3.one), star, 0.03f, NumuColor.WithAlpha(NumuColor.Hex("#ffb732"), 0.2f));
            Shapes.Extrude(sail, Matrix4x4.TRS(new Vector3(-0.22f, 2.0f, -0.5f), Quaternion.Euler(0, -90, 0), Vector3.one), star, 0.03f, NumuColor.WithAlpha(NumuColor.Hex("#ffb732"), 0.2f));
        }

        // ------------------------------------------------------------------ signs and markers

        /// <summary>Wooden signboard (board + darker border + plank grooves + posts). Board centre at (0, boardY, 0), front faces -Z.</summary>
        public static void SignBoard(MeshBuilder mb, float w, float h, float boardY, int seed, float postSpread = 0.62f)
        {
            var rng = new System.Random(seed);
            Color border = NumuColor.Hex("#6e3a1f");
            Color face = NumuColor.Hex("#9c5530");
            Color faceLight = NumuColor.Hex("#ad6439");
            var outer = Shapes.RoundedRect(w + 0.36f, h + 0.36f, 0.45f, 6);
            Shapes.Extrude(mb, Matrix4x4.Translate(new Vector3(0, boardY, 0.05f)), outer, 0.42f, border);
            var inner = Shapes.RoundedRect(w, h, 0.35f, 6);
            Shapes.Extrude(mb, Matrix4x4.Translate(new Vector3(0, boardY, -0.1f)), inner, 0.3f, face, 35f, faceLight);
            for (int i = 1; i < 3; i++)
            {
                float y = boardY - h * 0.5f + h * i / 3f;
                Shapes.Box(mb, Matrix4x4.Translate(new Vector3(0, y, -0.26f)), new Vector3(w - 0.4f, 0.05f, 0.02f), NumuColor.Shade(face, 0.85f));
            }
            // Wood grain streaks.
            for (int i = 0; i < 6; i++)
            {
                float y = boardY + Rand(rng, -h * 0.4f, h * 0.4f);
                float x = Rand(rng, -w * 0.3f, w * 0.3f);
                Shapes.Box(mb, Matrix4x4.Translate(new Vector3(x, y, -0.26f)), new Vector3(Rand(rng, 0.6f, 1.6f), 0.035f, 0.02f), faceLight);
            }
            foreach (float s in new[] { -postSpread, postSpread })
            {
                float px = s * w * 0.5f;
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(px, (boardY - h * 0.5f) * 0.5f, 0.32f)), new Vector3(0.38f, boardY - h * 0.5f + 0.3f, 0.38f), 0.08f, 1, StructureKit.Wood[3]);
            }
            foreach (float s in new[] { -1f, 1f })
                Shapes.Sphere(mb, new Vector3(s * (w * 0.5f - 0.05f), boardY + h * 0.5f - 0.05f, -0.28f), 0.09f, NumuColor.Hex("#5a3018"), 1);
        }

        /// <summary>Level badge: white rim disc (base) and a separate face disc (tinted per state).</summary>
        public static void MarkerBase(MeshBuilder rim, MeshBuilder face, float radius)
        {
            Shapes.Lathe(rim, Matrix4x4.identity, new[]
            {
                new Vector2(0, 0), new Vector2(radius * 1.02f, 0), new Vector2(radius * 1.08f, 0.1f), new Vector2(radius, 0.24f), new Vector2(radius * 0.85f, 0.26f), new Vector2(0, 0.26f),
            }, 40, NumuColor.Hex("#ffffff"), true);
            Shapes.Lathe(face, Matrix4x4.identity, new[]
            {
                new Vector2(0, 0.2f), new Vector2(radius * 0.8f, 0.2f), new Vector2(radius * 0.8f, 0.3f), new Vector2(radius * 0.72f, 0.36f), new Vector2(0, 0.36f),
            }, 40, Color.white, true);
        }

        public static void LockIcon(MeshBuilder mb, float s)
        {
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0, -s * 0.15f)), new Vector3(s * 0.9f, s * 0.12f, s * 0.7f), s * 0.05f, 1, NumuColor.Hex("#ffffff"));
            Shapes.Torus(mb, Matrix4x4.TRS(new Vector3(0, 0, s * 0.22f), Quaternion.Euler(0, 0, 0), Vector3.one), s * 0.28f, s * 0.07f, 16, 6, NumuColor.Hex("#ffffff"), 180f);
        }

        public static void StarIcon(MeshBuilder mb, float s)
        {
            var star = Shapes.Star(s * 0.5f, s * 0.22f, 5);
            Shapes.Extrude(mb, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90, 0, 0), Vector3.one), star, s * 0.14f, NumuColor.Hex("#ffd23f"));
        }
    }
}
