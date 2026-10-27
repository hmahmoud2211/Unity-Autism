using System.Collections.Generic;
using UnityEngine;

namespace Numu
{
    /// <summary>Mesh sets for a building: opaque body, glass and warmly lit windows use separate materials.</summary>
    public class BuildingParts
    {
        public MeshBuilder Main = new MeshBuilder();
        public MeshBuilder Glass = new MeshBuilder();
        public MeshBuilder Lit = new MeshBuilder();
        public List<(Vector3 pos, Quaternion rot, Vector2 size)> Decals = new List<(Vector3, Quaternion, Vector2)>();
    }

    /// <summary>
    /// Stylised buildings in local space: front faces -Z (towards the camera), base at y = 0.
    /// Roofs are built from overlapping rows of rounded clay tiles.
    /// </summary>
    public static class ArchitectureKit
    {
        public static readonly Color Cream = NumuColor.Hex("#f8e9cc");
        public static readonly Color Stone = NumuColor.Hex("#cfc3b3");
        public static readonly Color StoneDark = NumuColor.Hex("#ada195");
        public static readonly Color Trim = NumuColor.Hex("#fffaf0");
        public static readonly Color RoofRed = NumuColor.Hex("#e2563c");
        public static readonly Color RoofTeal = NumuColor.Hex("#36a88f");
        public static readonly Color GlassBlue = NumuColor.Hex("#8fcdf0");
        public static readonly Color LitYellow = NumuColor.Hex("#ffd27a");
        public static readonly Color DoorWood = NumuColor.Hex("#9a5c33");

        static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        static Matrix4x4 TRS(Vector3 p, Quaternion r) => Matrix4x4.TRS(p, r, Vector3.one);

        // ================================================================== roofs

        /// <summary>
        /// One sloped roof face covered in tile rows. The face starts at <paramref name="origin"/> (middle of the eave),
        /// runs <paramref name="length"/> along <paramref name="up"/>, and narrows from width w0 at the eave to w1 at the top.
        /// </summary>
        public static void RoofFace(MeshBuilder mb, Vector3 origin, Vector3 across, Vector3 up, Vector3 normal, float length, float w0, float w1,
            Color tile, int seed, float rowH = 0.44f, float tileW = 0.5f)
        {
            var rng = new System.Random(seed);
            int rows = Mathf.Max(1, Mathf.CeilToInt(length / rowH));
            float scallop = 0.11f, bump = 0.07f;
            for (int r = 0; r < rows; r++)
            {
                float v0 = r * rowH;
                float v1 = Mathf.Min(length + 0.05f, v0 + rowH * 1.45f);
                float vm = (v0 + v1) * 0.5f;
                float w = Mathf.Lerp(w0, w1, Mathf.Clamp01(vm / length));
                float wBottom = Mathf.Lerp(w0, w1, Mathf.Clamp01(v0 / length));
                float wTop = Mathf.Lerp(w0, w1, Mathf.Clamp01(v1 / length));
                if (w < 0.15f) continue;
                int tiles = Mathf.Max(1, Mathf.RoundToInt(wBottom / tileW));
                float tw = wBottom / tiles;
                float offset = (r % 2) * 0.5f;
                int samplesPerTile = 6;
                int count = tiles * samplesPerTile + 1;
                int start = mb.VertexCount, tStart = mb.Triangles.Count;
                var tileCols = new Color[tiles + 2];
                for (int i = 0; i < tileCols.Length; i++)
                {
                    tileCols[i] = NumuColor.Jitter(tile, rng, 0.07f);
                    if (rng.NextDouble() < 0.12) tileCols[i] = NumuColor.Shade(tile, 1.12f);
                }
                // 4 rows of vertices: top (tucked), middle (bulged), bottom edge (lifted), under-lip.
                for (int row = 0; row < 4; row++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        float f = i / (float)(count - 1);
                        float xb = (f - 0.5f) * wBottom;
                        float q = Mathf.Repeat(f * tiles + offset, 1f);
                        int ti = Mathf.Clamp(Mathf.FloorToInt(f * tiles + offset), 0, tileCols.Length - 1);
                        float round = Mathf.Sqrt(Mathf.Max(0f, 1f - (2f * q - 1f) * (2f * q - 1f)));
                        float b = bump * Mathf.Sin(q * Mathf.PI);
                        float v, n, x;
                        switch (row)
                        {
                            case 0: v = v1; n = -0.03f; x = (f - 0.5f) * wTop; break;
                            case 1: v = Mathf.Lerp(v0, v1, 0.45f); n = b; x = (f - 0.5f) * Mathf.Lerp(wBottom, wTop, 0.45f); break;
                            case 2: v = v0 - scallop * round; n = b * 0.85f + 0.07f; x = xb; break;
                            default: v = v0 - scallop * round + 0.02f; n = -0.06f; x = xb; break;
                        }
                        Vector3 p = origin + across * x + up * v + normal * n;
                        Color c = row == 3 ? NumuColor.Shade(tileCols[ti], 0.7f) : tileCols[ti];
                        mb.Add(p, normal, c);
                    }
                }
                for (int row = 0; row < 3; row++)
                {
                    for (int i = 0; i < count - 1; i++)
                    {
                        int a = start + row * count + i;
                        Vector3 expected = row == 2 ? -up + normal * 0.2f : normal;
                        mb.QuadFacing(a, a + 1, a + count + 1, a + count, expected);
                    }
                }
                mb.RecalculateNormals(start, tStart);
            }
        }

        /// <summary>Gable roof with ridge along local X. Returns ridge height above the wall top.</summary>
        public static void GableRoof(MeshBuilder mb, Matrix4x4 m, float wallWidth, float wallDepth, float rise, float overhang, Color tile, int seed,
            Color? trim = null, bool soffit = true)
        {
            var part = new MeshBuilder();
            float halfD = wallDepth * 0.5f;
            float H = halfD + overhang;
            float Rt = rise * H / halfD;
            float W = wallWidth + overhang * 2f;
            float L = Mathf.Sqrt(Rt * Rt + H * H);
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                Vector3 origin = new Vector3(0, rise - Rt, s * H);
                Vector3 up = new Vector3(0, Rt, -s * H) / L;
                Vector3 normal = new Vector3(0, H, s * Rt).normalized;
                RoofFace(part, origin, Vector3.right, up, normal, L, W, W, tile, seed + side * 31);
                if (soffit)
                {
                    Vector3 mid = origin + up * (L * 0.5f) - normal * 0.1f;
                    Shapes.Box(part, TRS(mid, Quaternion.LookRotation(up, normal)), new Vector3(W - 0.05f, 0.1f, L), NumuColor.Hex("#8c5a3a"));
                }
                // Verge boards along the gable edges.
                foreach (float ex in new[] { -W * 0.5f, W * 0.5f })
                {
                    Vector3 mid = origin + up * (L * 0.5f) + Vector3.right * ex - normal * 0.02f;
                    Shapes.RoundedBox(part, TRS(mid, Quaternion.LookRotation(up, normal)), new Vector3(0.22f, 0.24f, L + 0.1f), 0.06f, 1, trim ?? Trim);
                }
            }
            var ridge = new List<Vector3> { new Vector3(-W * 0.5f - 0.05f, rise + 0.08f, 0), new Vector3(W * 0.5f + 0.05f, rise + 0.08f, 0) };
            Shapes.Tube(part, ridge, 0.2f, 10, NumuColor.Shade(tile, 0.88f));
            mb.Append(part, m);
        }

        /// <summary>Pyramid / hip roof with N faces over a regular polygon of the given apothem.</summary>
        public static void PyramidRoof(MeshBuilder mb, Matrix4x4 m, int sides, float apothem, float rise, float overhang, Color tile, int seed, Color? finial = null)
        {
            var part = new MeshBuilder();
            float H = apothem + overhang;
            float Rt = rise * H / apothem;
            float L = Mathf.Sqrt(Rt * Rt + H * H);
            float w0 = 2f * H * Mathf.Tan(Mathf.PI / sides);
            for (int f = 0; f < sides; f++)
            {
                Quaternion q = Quaternion.Euler(0, f * 360f / sides, 0);
                Vector3 origin = q * new Vector3(0, rise - Rt, -H);
                Vector3 up = q * new Vector3(0, Rt, H) / L;
                Vector3 normal = q * new Vector3(0, H, -Rt).normalized;
                Vector3 across = q * Vector3.right;
                RoofFace(part, origin, across, up, normal, L, w0 * 1.02f, 0.05f, tile, seed + f * 17, 0.4f, 0.46f);
                // Hip cap.
                Vector3 corner = q * new Vector3(w0 * 0.5f, rise - Rt, -H) + Vector3.up * 0.05f;
                Shapes.Tube(part, new List<Vector3> { corner, new Vector3(0, rise + 0.05f, 0) }, 0.11f, 6, NumuColor.Shade(tile, 0.85f));
            }
            // Soffit plate.
            float cornerOffset = (270f + 180f / sides) % (360f / sides);
            float circum = H / Mathf.Cos(Mathf.PI / sides);
            Shapes.Cylinder(part, Matrix4x4.TRS(new Vector3(0, rise - Rt - 0.12f, 0), Quaternion.Euler(0, -cornerOffset, 0), Vector3.one), circum, circum * 0.9f, 0.15f, sides, NumuColor.Hex("#8c5a3a"));
            Shapes.Sphere(part, new Vector3(0, rise + 0.25f, 0), 0.24f, finial ?? NumuColor.Hex("#f2c14e"), 2);
            mb.Append(part, m);
        }

        // ================================================================== openings

        /// <summary>Window on a wall face. <paramref name="rot"/> faces the window outwards (its forward = wall normal).</summary>
        public static void Window(BuildingParts parts, Vector3 pos, Quaternion rot, float w, float h, bool arched = false, bool lit = false, bool shutters = false,
            Color? frame = null, bool flowerBox = false, int seed = 0)
        {
            Color fc = frame ?? Trim;
            var glassMb = lit ? parts.Lit : parts.Glass;
            var main = parts.Main;
            Vector3 fwd = rot * Vector3.forward;
            Vector3 right = rot * Vector3.right;
            Vector3 upv = rot * Vector3.up;
            if (arched)
            {
                var shape = Shapes.Arch(w, h, 12);
                Shapes.Extrude(glassMb, TRS(pos - upv * (h * 0.5f) + fwd * 0.02f, rot * Quaternion.Euler(0, 180, 0)), shape, 0.08f, lit ? LitYellow : GlassBlue);
                var arc = new List<Vector3>();
                foreach (var p in shape) arc.Add(pos - upv * (h * 0.5f) + right * -p.x + upv * p.y + fwd * 0.1f);
                arc.Add(arc[0]);
                Shapes.Tube(main, arc, 0.1f, 6, fc, false);
                Shapes.Box(main, TRS(pos + fwd * 0.1f + upv * (-h * 0.05f), rot), new Vector3(0.08f, h * 0.9f, 0.08f), fc);
            }
            else
            {
                Shapes.Box(glassMb, TRS(pos + fwd * 0.02f, rot), new Vector3(w, h, 0.06f), lit ? LitYellow : GlassBlue);
                Shapes.RoundedBox(main, TRS(pos + upv * (h * 0.5f + 0.07f) + fwd * 0.08f, rot), new Vector3(w + 0.3f, 0.16f, 0.2f), 0.05f, 1, fc);
                Shapes.RoundedBox(main, TRS(pos - upv * (h * 0.5f + 0.08f) + fwd * 0.12f, rot), new Vector3(w + 0.42f, 0.18f, 0.32f), 0.05f, 1, fc);
                Shapes.RoundedBox(main, TRS(pos + right * (w * 0.5f + 0.07f) + fwd * 0.08f, rot), new Vector3(0.16f, h + 0.2f, 0.2f), 0.05f, 1, fc);
                Shapes.RoundedBox(main, TRS(pos - right * (w * 0.5f + 0.07f) + fwd * 0.08f, rot), new Vector3(0.16f, h + 0.2f, 0.2f), 0.05f, 1, fc);
                Shapes.Box(main, TRS(pos + fwd * 0.09f, rot), new Vector3(0.07f, h, 0.06f), fc);
                Shapes.Box(main, TRS(pos + fwd * 0.09f, rot), new Vector3(w, 0.07f, 0.06f), fc);
            }
            if (shutters)
            {
                Color sc = NumuColor.Hex("#5a9bd4");
                foreach (float s in new[] { -1f, 1f })
                {
                    Shapes.RoundedBox(main, TRS(pos + right * s * (w * 0.5f + 0.42f) + fwd * 0.06f, rot), new Vector3(w * 0.42f, h + 0.1f, 0.08f), 0.04f, 1, sc);
                }
            }
            if (flowerBox)
            {
                Vector3 bp = pos - upv * (h * 0.5f + 0.32f) + fwd * 0.28f;
                Shapes.RoundedBox(main, TRS(bp, rot), new Vector3(w + 0.3f, 0.32f, 0.36f), 0.05f, 1, NumuColor.Hex("#a8693c"));
                var rng = new System.Random(seed);
                for (int i = 0; i < 6; i++)
                {
                    Vector3 fp = bp + right * Mathf.Lerp(-w * 0.5f, w * 0.5f, i / 5f) + upv * 0.22f;
                    Shapes.Sphere(main, fp, 0.16f, NatureKit.FlowerColors[rng.Next(NatureKit.FlowerColors.Length)], 1);
                    Shapes.Sphere(main, fp - upv * 0.08f + fwd * 0.05f, 0.14f, NumuColor.Hex("#4fa548"), 1);
                }
            }
        }

        public static void Door(BuildingParts parts, Vector3 basePos, Quaternion rot, float w, float h, Color color, bool arched = true, bool doubleDoor = false)
        {
            var main = parts.Main;
            Vector3 fwd = rot * Vector3.forward;
            Vector3 right = rot * Vector3.right;
            Vector3 upv = rot * Vector3.up;
            var shape = arched ? Shapes.Arch(w, h, 12) : Shapes.RoundedRect(w, h, 0.08f, 3);
            Vector3 baseOffset = arched ? Vector3.zero : upv * (h * 0.5f);
            Shapes.Extrude(main, TRS(basePos + baseOffset + fwd * 0.04f, rot * Quaternion.Euler(0, 180, 0)), shape, 0.12f, color);
            // Plank grooves.
            for (int i = 1; i < 4; i++)
            {
                float x = -w * 0.5f + w * i / 4f;
                Shapes.Box(main, TRS(basePos + right * x + upv * (h * 0.42f) + fwd * 0.11f, rot), new Vector3(0.04f, h * 0.8f, 0.02f), NumuColor.Shade(color, 0.82f));
            }
            // Frame runs up one side, over the top and down the other (no threshold bar).
            var frameLine = new List<Vector3>();
            for (int i = 1; i < shape.Count; i++) frameLine.Add(basePos + baseOffset + right * -shape[i].x + upv * shape[i].y + fwd * 0.12f);
            frameLine.Add(basePos + baseOffset + right * -shape[0].x + upv * shape[0].y + fwd * 0.12f);
            Shapes.Tube(main, frameLine, 0.09f, 6, Trim, false);
            if (doubleDoor) Shapes.Box(main, TRS(basePos + upv * (h * 0.45f) + fwd * 0.12f, rot), new Vector3(0.06f, h * 0.9f, 0.04f), NumuColor.Shade(color, 0.7f));
            Shapes.Sphere(main, basePos + right * (w * (doubleDoor ? 0.12f : 0.3f)) + upv * (h * 0.45f) + fwd * 0.16f, 0.07f, NumuColor.Hex("#f2c14e"), 1);
            Shapes.RoundedBox(main, TRS(basePos + fwd * 0.4f + upv * 0.05f, rot), new Vector3(w + 0.6f, 0.14f, 0.7f), 0.05f, 1, Stone);
        }

        public static void RoundWindow(BuildingParts parts, Vector3 pos, Quaternion rot, float r, bool lit = false)
        {
            Vector3 fwd = rot * Vector3.forward;
            Shapes.Cylinder(lit ? parts.Lit : parts.Glass, TRS(pos + fwd * 0.02f, rot * Quaternion.Euler(90, 0, 0)), r, r, 0.06f, 20, lit ? LitYellow : GlassBlue);
            Shapes.Torus(parts.Main, TRS(pos + fwd * 0.07f, rot * Quaternion.Euler(90, 0, 0)), r + 0.04f, 0.09f, 24, 8, Trim);
            Shapes.Box(parts.Main, TRS(pos + fwd * 0.08f, rot), new Vector3(r * 2f, 0.06f, 0.05f), Trim);
            Shapes.Box(parts.Main, TRS(pos + fwd * 0.08f, rot), new Vector3(0.06f, r * 2f, 0.05f), Trim);
        }

        static void GableWall(MeshBuilder mb, Vector3 center, Quaternion rot, float width, float rise, Color c)
        {
            var tri = new List<Vector2> { new Vector2(-width * 0.5f, 0), new Vector2(width * 0.5f, 0), new Vector2(0, rise) };
            Shapes.Extrude(mb, TRS(center, rot), tri, 0.3f, c);
        }

        public static void Flag(MeshBuilder pole, MeshBuilder cloth, Vector3 basePos, float height, Color color, int seed, bool star = true)
        {
            Shapes.Cylinder(pole, Matrix4x4.Translate(basePos), 0.07f, 0.06f, height, 8, NumuColor.Hex("#8a6a4a"));
            Shapes.Sphere(pole, basePos + Vector3.up * (height + 0.08f), 0.12f, NumuColor.Hex("#f2c14e"), 1);
            int start = cloth.VertexCount;
            float w = height * 0.42f, h = height * 0.26f;
            Vector3 top = basePos + Vector3.up * (height - 0.08f);
            int cols = 8, rows = 3;
            for (int j = 0; j <= rows; j++)
            {
                for (int i = 0; i <= cols; i++)
                {
                    float u = i / (float)cols, v = j / (float)rows;
                    // Swallow-tail notch on the free end.
                    float notch = u > 0.75f ? (1f - Mathf.Abs(v - 0.5f) * 2f) * (u - 0.75f) * 1.2f : 0f;
                    Vector3 p = top + new Vector3(u * w - notch * w * 0.4f, -v * h, Mathf.Sin(u * 3.2f + seed) * 0.12f * u);
                    cloth.Add(p, Vector3.back, color);
                }
            }
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    int a = start + j * (cols + 1) + i;
                    cloth.QuadFacing(a, a + 1, a + cols + 2, a + cols + 1, Vector3.back);
                }
            cloth.RecalculateNormals(start, cloth.Triangles.Count - cols * rows * 6);
            if (star)
            {
                var starShape = Shapes.Star(h * 0.3f, h * 0.13f, 5);
                float sz = Mathf.Sin(0.38f * 3.2f + seed) * 0.12f * 0.38f;
                Shapes.Extrude(cloth, TRS(top + new Vector3(w * 0.38f, -h * 0.5f, sz - 0.04f), Quaternion.identity), starShape, 0.02f, NumuColor.Hex("#fff2a8"));
                Shapes.Extrude(cloth, TRS(top + new Vector3(w * 0.38f, -h * 0.5f, sz + 0.04f), Quaternion.identity), starShape, 0.02f, NumuColor.Hex("#fff2a8"));
            }
            // Wind weight grows towards the free end (stars included so they move with the cloth).
            for (int i = start; i < cloth.VertexCount; i++)
            {
                float u = Mathf.InverseLerp(top.x, top.x + w, cloth.Vertices[i].x);
                cloth.Colors[i] = NumuColor.WithAlpha(cloth.Colors[i], u);
            }
        }

        // ================================================================== buildings

        /// <summary>Two-storey cream cottage with tiled roof, front cross-gable, dormer, chimney and porch.</summary>
        public static void HomeCottage(BuildingParts parts, int seed)
        {
            var mb = parts.Main;
            var rng = new System.Random(seed);
            Quaternion front = Quaternion.Euler(0, 180, 0); // windows face -Z
            float W = 8.2f, D = 5.4f, H = 4.2f;
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.25f, 0)), new Vector3(W + 0.4f, 0.55f, D + 0.4f), 0.12f, 1, Stone, 0.03f, seed);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.5f + H * 0.5f, 0)), new Vector3(W, H, D), 0.14f, 2, Cream);
            // Front cross-gable wing.
            float gx = -2.0f, gw = 3.6f, gd = 1.4f;
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(gx, 0.5f + H * 0.5f, -D * 0.5f - gd * 0.5f + 0.1f)), new Vector3(gw, H, gd + 0.2f), 0.14f, 2, Cream);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(gx, 0.25f, -D * 0.5f - gd * 0.5f)), new Vector3(gw + 0.4f, 0.55f, gd + 0.4f), 0.12f, 1, Stone);
            float wallTop = 0.5f + H;
            GableRoof(mb, Matrix4x4.Translate(new Vector3(0, wallTop, 0)), W, D, 2.4f, 0.6f, RoofRed, seed);
            GableWall(mb, new Vector3(W * 0.5f - 0.15f, wallTop, 0), Quaternion.Euler(0, 90, 0), D, 2.4f, Cream);
            GableWall(mb, new Vector3(-W * 0.5f + 0.15f, wallTop, 0), Quaternion.Euler(0, 90, 0), D, 2.4f, Cream);
            float frontZ = -D * 0.5f - gd;
            float crossStart = frontZ - 0.55f, crossEnd = -0.5f;
            GableRoof(mb, Matrix4x4.TRS(new Vector3(gx, wallTop, (crossStart + crossEnd) * 0.5f), Quaternion.Euler(0, 90, 0), Vector3.one), crossEnd - crossStart - 1.1f, gw, 2.1f, 0.55f, RoofRed, seed + 5);
            GableWall(mb, new Vector3(gx, wallTop, frontZ + 0.12f), Quaternion.identity, gw, 2.1f, Cream);
            RoundWindow(parts, new Vector3(gx, wallTop + 0.8f, frontZ - 0.05f), front, 0.38f, true);
            // Chimney.
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(2.4f, wallTop + 2.2f, 1.1f)), new Vector3(0.95f, 2.2f, 0.95f), 0.1f, 1, NumuColor.Hex("#c06a4c"), 0.02f, seed);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(2.4f, wallTop + 3.35f, 1.1f)), new Vector3(1.2f, 0.3f, 1.2f), 0.08f, 1, NumuColor.Hex("#e6d6c4"));
            // Dormer on the front slope.
            Vector3 dormer = new Vector3(2.2f, wallTop + 0.75f, -1.25f);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(dormer + Vector3.up * 0.4f), new Vector3(1.9f, 1.6f, 1.8f), 0.1f, 1, Cream);
            GableRoof(mb, Matrix4x4.TRS(dormer + Vector3.up * 1.2f, Quaternion.Euler(0, 90, 0), Vector3.one), 1.8f, 1.9f, 0.9f, 0.3f, RoofRed, seed + 9);
            GableWall(mb, dormer + new Vector3(0, 1.2f, -0.82f), Quaternion.identity, 1.9f, 0.9f, Cream);
            Window(parts, dormer + new Vector3(0, 0.45f, -0.92f), front, 0.9f, 0.85f, false, true);
            // Openings on the front.
            float fz = -D * 0.5f - 0.02f;
            Door(parts, new Vector3(1.0f, 0.55f, fz), front, 1.3f, 2.1f, DoorWood);
            Window(parts, new Vector3(3.0f, 1.95f, fz), front, 1.1f, 1.1f, false, true, false, null, true, seed + 1);
            Window(parts, new Vector3(1.0f, 3.75f, fz), front, 1.0f, 1.0f, false, false, true);
            Window(parts, new Vector3(3.0f, 3.75f, fz), front, 1.0f, 1.0f, false, false, true);
            Window(parts, new Vector3(gx, 1.95f, frontZ - 0.02f), front, 1.3f, 1.15f, false, true, false, null, true, seed + 2);
            Window(parts, new Vector3(gx, 3.75f, frontZ - 0.02f), front, 1.2f, 1.0f, false, false, true);
            // Side windows.
            Quaternion left = Quaternion.Euler(0, -90, 0), right = Quaternion.Euler(0, 90, 0);
            Window(parts, new Vector3(-W * 0.5f - 0.02f, 1.95f, 0.8f), left, 1.0f, 1.0f);
            Window(parts, new Vector3(-W * 0.5f - 0.02f, 3.75f, 0.8f), left, 0.9f, 0.9f);
            Window(parts, new Vector3(W * 0.5f + 0.02f, 1.95f, 0.6f), right, 1.0f, 1.0f, false, true);
            // Porch canopy over the door.
            GableRoof(mb, Matrix4x4.TRS(new Vector3(1.0f, 2.95f, fz - 0.7f), Quaternion.Euler(0, 90, 0), Vector3.one), 1.4f, 1.8f, 0.55f, 0.2f, RoofRed, seed + 13, null, false);
            foreach (float s in new[] { -0.75f, 0.75f })
                Shapes.Beam(mb, new Vector3(1.0f + s, 0.55f, fz - 1.3f), new Vector3(1.0f + s, 2.9f, fz - 1.3f), 0.08f, 8, Trim);
        }

        /// <summary>Little single-storey house with timber corners and lit windows.</summary>
        public static void SmallHouse(BuildingParts parts, int seed)
        {
            var mb = parts.Main;
            Quaternion front = Quaternion.Euler(0, 180, 0);
            float W = 4.6f, D = 3.8f, H = 2.7f;
            Color wall = NumuColor.Hex("#f6d6a4");
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.2f, 0)), new Vector3(W + 0.4f, 0.45f, D + 0.4f), 0.1f, 1, Stone);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.4f + H * 0.5f, 0)), new Vector3(W, H, D), 0.12f, 2, wall);
            foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(c.x * (W * 0.5f), 0.4f + H * 0.5f, c.y * (D * 0.5f))), new Vector3(0.3f, H + 0.1f, 0.3f), 0.06f, 1, StructureKit.Wood[1]);
            float top = 0.4f + H;
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, top - 0.05f, 0)), new Vector3(W + 0.2f, 0.25f, D + 0.2f), 0.06f, 1, StructureKit.Wood[1]);
            GableRoof(mb, Matrix4x4.Translate(new Vector3(0, top, 0)), W, D, 1.9f, 0.5f, RoofRed, seed);
            GableWall(mb, new Vector3(W * 0.5f - 0.12f, top, 0), Quaternion.Euler(0, 90, 0), D, 1.9f, wall);
            GableWall(mb, new Vector3(-W * 0.5f + 0.12f, top, 0), Quaternion.Euler(0, 90, 0), D, 1.9f, wall);
            float fz = -D * 0.5f - 0.02f;
            Door(parts, new Vector3(-0.9f, 0.45f, fz), front, 1.0f, 1.9f, NumuColor.Hex("#b0683a"));
            Window(parts, new Vector3(1.0f, 1.9f, fz), front, 1.1f, 1.0f, false, true, false, null, true, seed);
            Window(parts, new Vector3(W * 0.5f + 0.02f, 1.9f, 0f), Quaternion.Euler(0, 90, 0), 0.9f, 0.9f, false, true);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(-1.3f, top + 1.7f, 0.7f)), new Vector3(0.6f, 1.4f, 0.6f), 0.08f, 1, NumuColor.Hex("#c06a4c"));
        }

        /// <summary>Pastel two-storey school with a central clock tower.</summary>
        public static void School(BuildingParts parts, MeshBuilder flagCloth, int seed)
        {
            var mb = parts.Main;
            Quaternion front = Quaternion.Euler(0, 180, 0);
            float W = 12.4f, D = 4.8f, H = 5.0f;
            Color wall = NumuColor.Hex("#f8dcc0");
            Color wallLight = NumuColor.Hex("#fbe8d2");
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.25f, 0)), new Vector3(W + 0.5f, 0.55f, D + 0.5f), 0.12f, 1, Stone);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.5f + H * 0.5f, 0)), new Vector3(W, H, D), 0.14f, 2, wall);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 2.8f, 0)), new Vector3(W + 0.25f, 0.28f, D + 0.25f), 0.08f, 1, Trim);
            float top = 0.5f + H;
            GableRoof(mb, Matrix4x4.Translate(new Vector3(0, top, 0)), W, D, 2.4f, 0.6f, RoofRed, seed);
            GableWall(mb, new Vector3(W * 0.5f - 0.15f, top, 0), Quaternion.Euler(0, 90, 0), D, 2.4f, wall);
            GableWall(mb, new Vector3(-W * 0.5f + 0.15f, top, 0), Quaternion.Euler(0, 90, 0), D, 2.4f, wall);

            // Tower.
            float tw = 3.4f, th = 8.6f, tz = -0.7f;
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.5f + th * 0.5f, tz)), new Vector3(tw, th, tw + 1.2f), 0.14f, 2, wallLight);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.5f + th, tz)), new Vector3(tw + 0.4f, 0.35f, tw + 1.6f), 0.08f, 1, Trim);
            PyramidRoof(mb, Matrix4x4.TRS(new Vector3(0, 0.6f + th, tz), Quaternion.identity, new Vector3(1f, 1f, (tw + 1.2f) / tw)), 4, tw * 0.5f, 2.6f, 0.45f, RoofRed, seed + 3);
            float tfz = tz - (tw + 1.2f) * 0.5f - 0.02f;
            // Clock face.
            Vector3 clock = new Vector3(0, 0.5f + th - 1.45f, tfz - 0.12f);
            Shapes.Cylinder(mb, TRS(clock, Quaternion.Euler(90, 0, 0)), 1.02f, 1.02f, 0.18f, 32, Trim);
            Shapes.Torus(mb, TRS(clock + Vector3.back * 0.06f, Quaternion.Euler(90, 0, 0)), 1.0f, 0.1f, 32, 8, NumuColor.Hex("#c8853f"));
            for (int i = 0; i < 12; i++)
            {
                Quaternion r = Quaternion.Euler(0, 0, i * 30f);
                Shapes.Box(mb, TRS(clock + r * new Vector3(0, 0.72f, 0) + Vector3.back * 0.1f, r), new Vector3(0.07f, i % 3 == 0 ? 0.24f : 0.14f, 0.04f), NumuColor.Hex("#3b3b4a"));
            }
            Shapes.Box(mb, TRS(clock + Quaternion.Euler(0, 0, -60) * new Vector3(0, 0.22f, 0) + Vector3.back * 0.13f, Quaternion.Euler(0, 0, -60)), new Vector3(0.09f, 0.48f, 0.04f), NumuColor.Hex("#3b3b4a"));
            Shapes.Box(mb, TRS(clock + new Vector3(0, 0.3f, 0) + Vector3.back * 0.14f, Quaternion.identity), new Vector3(0.07f, 0.62f, 0.04f), NumuColor.Hex("#3b3b4a"));
            Shapes.Sphere(mb, clock + Vector3.back * 0.15f, 0.08f, NumuColor.Hex("#3b3b4a"), 1);
            Door(parts, new Vector3(0, 0.55f, tfz), front, 1.7f, 2.6f, NumuColor.Hex("#3f7fd0"), true, true);
            Window(parts, new Vector3(0, 4.2f, tfz), front, 1.1f, 1.4f, true);
            for (int s = 0; s < 3; s++)
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.12f + s * 0.18f - 0.36f, tfz - 0.75f - (2 - s) * 0.42f)), new Vector3(2.6f + (2 - s) * 0.3f, 0.2f, 0.5f), 0.05f, 1, Stone);
            // Facade windows.
            float fz = -D * 0.5f - 0.02f;
            foreach (float x in new[] { -5.0f, -3.2f, 3.2f, 5.0f })
            {
                Window(parts, new Vector3(x, 1.8f, fz), front, 1.05f, 1.3f, true, x > 4f);
                Window(parts, new Vector3(x, 4.0f, fz), front, 1.05f, 1.2f, true);
            }
            if (flagCloth != null) Flag(mb, flagCloth, new Vector3(0.0f, 0.6f + th + 2.6f, tz), 2.6f, NumuColor.Hex("#ff8a3d"), seed);
        }

        /// <summary>Tiny hub cottage with a teal tiled roof (gable facing the camera) and round window.</summary>
        public static void HubCottage(BuildingParts parts, MeshBuilder flagCloth, int seed)
        {
            var mb = parts.Main;
            Quaternion front = Quaternion.Euler(0, 180, 0);
            float W = 4.4f, D = 4.2f, H = 3.0f;
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.2f, 0)), new Vector3(W + 0.45f, 0.45f, D + 0.45f), 0.1f, 1, Stone);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.4f + H * 0.5f, 0)), new Vector3(W, H, D), 0.14f, 2, NumuColor.Hex("#fbf0dc"));
            float top = 0.4f + H;
            GableRoof(mb, Matrix4x4.TRS(new Vector3(0, top, 0), Quaternion.Euler(0, 90, 0), Vector3.one), D, W, 2.5f, 0.5f, RoofTeal, seed);
            GableWall(mb, new Vector3(0, top, -D * 0.5f + 0.12f), Quaternion.identity, W, 2.5f, NumuColor.Hex("#fbf0dc"));
            GableWall(mb, new Vector3(0, top, D * 0.5f - 0.12f), Quaternion.identity, W, 2.5f, NumuColor.Hex("#fbf0dc"));
            float fz = -D * 0.5f - 0.02f;
            RoundWindow(parts, new Vector3(0, top + 0.95f, fz - 0.1f), front, 0.45f, true);
            Door(parts, new Vector3(0, 0.45f, fz), front, 1.3f, 2.2f, NumuColor.Hex("#2f9f8f"));
            Window(parts, new Vector3(-1.45f, 2.0f, fz), front, 0.75f, 0.9f, false, true);
            Window(parts, new Vector3(1.45f, 2.0f, fz), front, 0.75f, 0.9f, false, true);
            Window(parts, new Vector3(W * 0.5f + 0.02f, 2.0f, 0f), Quaternion.Euler(0, 90, 0), 0.9f, 0.9f, true);
            if (flagCloth != null) Flag(mb, flagCloth, new Vector3(1.6f, top + 1.2f, 0.8f), 2.6f, NumuColor.Hex("#ff8a3d"), seed);
        }

        /// <summary>Cream supermarket with flat roof, big shop windows, striped awning and a roof sign box.</summary>
        public static void Supermarket(BuildingParts parts, int seed, out Vector3 signDecalPos, out Vector2 signDecalSize)
        {
            var mb = parts.Main;
            var rng = new System.Random(seed);
            Quaternion front = Quaternion.Euler(0, 180, 0);
            float W = 13.6f, D = 3.8f, H = 3.9f;
            Color wall = NumuColor.Hex("#f9e7c0");
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.2f, 0)), new Vector3(W + 0.5f, 0.45f, D + 0.5f), 0.1f, 1, Stone);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.4f + H * 0.5f, 0)), new Vector3(W, H, D), 0.14f, 2, wall);
            float top = 0.4f + H;
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, top + 0.15f, 0)), new Vector3(W + 0.5f, 0.45f, D + 0.5f), 0.1f, 1, NumuColor.Hex("#f2d49a"));
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, top + 0.5f, 0)), new Vector3(W - 0.4f, 0.3f, D - 0.4f), 0.08f, 1, NumuColor.Hex("#e7d2ae"));
            // Brick-ish corner pilasters.
            foreach (float x in new[] { -W * 0.5f, W * 0.5f })
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(x, 0.4f + H * 0.5f, -D * 0.5f)), new Vector3(0.6f, H, 0.6f), 0.1f, 1, NumuColor.Hex("#f0cf95"));
            float fz = -D * 0.5f - 0.02f;
            foreach (float x in new[] { -4.8f, -2.4f, 2.4f, 4.8f })
                Window(parts, new Vector3(x, 1.55f, fz), front, 2.0f, 1.6f, false, true, false, NumuColor.Hex("#fff4e0"));
            // Glass double door.
            Shapes.Box(parts.Glass, TRS(new Vector3(0, 1.42f, fz + 0.02f), front), new Vector3(1.8f, 2.0f, 0.06f), GlassBlue);
            Shapes.RoundedBox(mb, TRS(new Vector3(0, 1.42f, fz - 0.06f), front), new Vector3(0.1f, 2.1f, 0.1f), 0.03f, 1, Trim);
            Shapes.RoundedBox(mb, TRS(new Vector3(0, 2.5f, fz - 0.06f), front), new Vector3(2.1f, 0.16f, 0.16f), 0.04f, 1, Trim);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(0, 0.45f, fz - 0.5f)), new Vector3(2.4f, 0.14f, 0.9f), 0.05f, 1, Stone);

            // Striped awning: sloped stripes plus a scalloped valance.
            Color red = NumuColor.Hex("#ee4f5a"), white = NumuColor.Hex("#fff7ef");
            float aw = W + 0.6f, aTop = 3.6f, aBot = 2.75f, aOut = 1.8f;
            int stripes = 18;
            Vector3 a0 = new Vector3(0, aTop, fz), a1 = new Vector3(0, aBot, fz - aOut);
            Vector3 slope = (a1 - a0);
            Quaternion slopeRot = Quaternion.LookRotation(slope.normalized, Vector3.Cross(Vector3.right, slope.normalized));
            for (int i = 0; i < stripes; i++)
            {
                float x = -aw * 0.5f + (i + 0.5f) * aw / stripes;
                Vector3 c = (a0 + a1) * 0.5f + Vector3.right * x;
                Shapes.RoundedBox(mb, TRS(c, slopeRot), new Vector3(aw / stripes + 0.01f, 0.12f, slope.magnitude + 0.1f), 0.04f, 1, i % 2 == 0 ? red : white);
                var flap = Shapes.Arch(aw / stripes, aw / stripes * 0.5f, 8);
                for (int k = 0; k < flap.Count; k++) flap[k] = new Vector2(flap[k].x, -flap[k].y);
                Shapes.Extrude(mb, TRS(a1 + Vector3.right * x + Vector3.down * 0.02f, Quaternion.identity), flap, 0.1f, i % 2 == 0 ? red : white);
            }
            foreach (float x in new[] { -aw * 0.5f + 0.1f, aw * 0.5f - 0.1f })
                Shapes.Beam(mb, new Vector3(x, aTop, fz), new Vector3(x, aBot, fz - aOut), 0.06f, 6, NumuColor.Hex("#7a5a40"));

            // Roof sign box.
            Vector3 sign = new Vector3(0, top + 1.55f, -D * 0.5f + 0.6f);
            Shapes.RoundedBox(mb, Matrix4x4.Translate(sign), new Vector3(3.8f, 2.0f, 0.6f), 0.25f, 2, NumuColor.Hex("#f06e86"));
            Shapes.RoundedBox(mb, Matrix4x4.Translate(sign + Vector3.back * 0.05f), new Vector3(3.3f, 1.55f, 0.6f), 0.2f, 2, NumuColor.Hex("#fff3f4"));
            Shapes.RoundedBox(mb, Matrix4x4.Translate(sign + Vector3.back * 0.1f), new Vector3(3.0f, 1.3f, 0.6f), 0.18f, 2, NumuColor.Hex("#f06e86"));
            foreach (float x in new[] { -1.2f, 1.2f })
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(x, top + 0.6f, sign.z + 0.2f)), new Vector3(0.18f, 0.9f, 0.18f), 0.05f, 1, NumuColor.Hex("#8a6a4a"));
            signDecalPos = sign + Vector3.back * 0.42f;
            signDecalSize = new Vector2(1.9f, 1.1f);

            // Shelves of goods visible through the windows.
            for (int i = 0; i < 26; i++)
            {
                float x = Rand(rng, -6f, 6f);
                if (Mathf.Abs(x) < 1.2f) continue;
                Shapes.RoundedBox(mb, Matrix4x4.Translate(new Vector3(x, Rand(rng, 1.0f, 2.4f), fz + 0.5f)), new Vector3(0.3f, 0.4f, 0.3f), 0.05f, 1, NatureKit.FlowerColors[rng.Next(NatureKit.FlowerColors.Length)]);
            }
        }

        /// <summary>Open hexagonal gazebo with white posts and a tiled roof.</summary>
        public static void Gazebo(MeshBuilder mb, int seed, Color roof)
        {
            int sides = 6;
            float r = 1.7f, h = 2.5f;
            Shapes.Lathe(mb, Matrix4x4.identity, new[] { new Vector2(0, 0), new Vector2(r + 0.35f, 0), new Vector2(r + 0.35f, 0.3f), new Vector2(0, 0.3f) }, sides, Stone, false);
            for (int i = 0; i < sides; i++)
            {
                float a = (i + 0.5f) / sides * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * r, 0.3f, Mathf.Sin(a) * r);
                Shapes.Cylinder(mb, Matrix4x4.Translate(p), 0.13f, 0.11f, h, 8, Trim);
                Vector3 p2 = new Vector3(Mathf.Cos(a + Mathf.PI * 2f / sides) * r, 0.3f, Mathf.Sin(a + Mathf.PI * 2f / sides) * r);
                Shapes.Beam(mb, p + Vector3.up * 0.9f, p2 + Vector3.up * 0.9f, 0.06f, 6, Trim);
            }
            Shapes.Torus(mb, Matrix4x4.TRS(new Vector3(0, 0.3f + h, 0), Quaternion.Euler(0, 30, 0), Vector3.one), r, 0.12f, sides, 6, Trim);
            PyramidRoof(mb, Matrix4x4.TRS(new Vector3(0, 0.35f + h, 0), Quaternion.Euler(0, 0, 0), Vector3.one), sides, r * Mathf.Cos(Mathf.PI / sides) + 0.1f, 1.5f, 0.35f, roof, seed);
        }
    }
}
