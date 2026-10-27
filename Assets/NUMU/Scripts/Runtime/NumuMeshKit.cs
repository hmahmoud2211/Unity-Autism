using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Numu
{
    /// <summary>Colour helpers. Colours are authored in sRGB and converted when a mesh is built.</summary>
    public static class NumuColor
    {
        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            return c;
        }

        public static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

        public static Color Jitter(Color c, System.Random rng, float amount)
        {
            float k = 1f + ((float)rng.NextDouble() * 2f - 1f) * amount;
            return Shade(c, k);
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    /// <summary>Small deterministic value noise used to break up procedural shapes.</summary>
    public static class NumuNoise
    {
        static float Hash(int x, int y, int z)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + z * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        public static float Value3(float x, float y, float z)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y), zi = Mathf.FloorToInt(z);
            float xf = Smooth(x - xi), yf = Smooth(y - yi), zf = Smooth(z - zi);
            float c000 = Hash(xi, yi, zi), c100 = Hash(xi + 1, yi, zi);
            float c010 = Hash(xi, yi + 1, zi), c110 = Hash(xi + 1, yi + 1, zi);
            float c001 = Hash(xi, yi, zi + 1), c101 = Hash(xi + 1, yi, zi + 1);
            float c011 = Hash(xi, yi + 1, zi + 1), c111 = Hash(xi + 1, yi + 1, zi + 1);
            float x00 = Mathf.Lerp(c000, c100, xf), x10 = Mathf.Lerp(c010, c110, xf);
            float x01 = Mathf.Lerp(c001, c101, xf), x11 = Mathf.Lerp(c011, c111, xf);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, yf), Mathf.Lerp(x01, x11, yf), zf);
        }

        /// <summary>Two-octave noise in roughly [-1, 1].</summary>
        public static float Signed(Vector3 p) => (Value3(p.x, p.y, p.z) * 0.65f + Value3(p.x * 2.1f + 7.3f, p.y * 2.1f, p.z * 2.1f) * 0.35f) * 2f - 1f;
    }

    /// <summary>Accumulates vertices/triangles from many primitives and turns them into a Mesh.</summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<Vector2> UVs = new List<Vector2>();
        public readonly List<int> Triangles = new List<int>();

        public int VertexCount => Vertices.Count;

        public int Add(Vector3 p, Vector3 n, Color c, Vector2 uv = default)
        {
            Vertices.Add(p);
            Normals.Add(n);
            Colors.Add(c);
            UVs.Add(uv);
            return Vertices.Count - 1;
        }

        public void Tri(int a, int b, int c)
        {
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }

        /// <summary>Adds a triangle, flipping it if needed so its face normal agrees with <paramref name="outward"/>.</summary>
        public void TriFacing(int a, int b, int c, Vector3 outward)
        {
            Vector3 fn = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
            if (Vector3.Dot(fn, outward) < 0f) Tri(a, c, b);
            else Tri(a, b, c);
        }

        public void QuadFacing(int a, int b, int c, int d, Vector3 outward)
        {
            TriFacing(a, b, c, outward);
            TriFacing(a, c, d, outward);
        }

        public void Append(MeshBuilder o, Matrix4x4 m)
        {
            int start = Vertices.Count;
            Matrix4x4 nm = m.inverse.transpose;
            bool flip = m.determinant < 0f;
            for (int i = 0; i < o.Vertices.Count; i++)
            {
                Vertices.Add(m.MultiplyPoint3x4(o.Vertices[i]));
                Normals.Add(nm.MultiplyVector(o.Normals[i]).normalized);
                Colors.Add(o.Colors[i]);
                UVs.Add(o.UVs[i]);
            }
            for (int i = 0; i < o.Triangles.Count; i += 3)
            {
                if (flip) Tri(start + o.Triangles[i], start + o.Triangles[i + 2], start + o.Triangles[i + 1]);
                else Tri(start + o.Triangles[i], start + o.Triangles[i + 1], start + o.Triangles[i + 2]);
            }
        }

        public void Append(MeshBuilder o) => Append(o, Matrix4x4.identity);

        /// <summary>Recomputes smooth normals for vertices in [fromVertex, end) using triangles from fromTriangle.</summary>
        public void RecalculateNormals(int fromVertex = 0, int fromTriangle = 0)
        {
            for (int i = fromVertex; i < Normals.Count; i++) Normals[i] = Vector3.zero;
            for (int i = fromTriangle; i < Triangles.Count; i += 3)
            {
                int a = Triangles[i], b = Triangles[i + 1], c = Triangles[i + 2];
                Vector3 fn = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
                if (a >= fromVertex) Normals[a] += fn;
                if (b >= fromVertex) Normals[b] += fn;
                if (c >= fromVertex) Normals[c] += fn;
            }
            for (int i = fromVertex; i < Normals.Count; i++)
            {
                Normals[i] = Normals[i].sqrMagnitude > 1e-12f ? Normals[i].normalized : Vector3.up;
            }
        }

        public void SetAlpha(float a, int fromVertex = 0)
        {
            for (int i = fromVertex; i < Colors.Count; i++) Colors[i] = NumuColor.WithAlpha(Colors[i], a);
        }

        /// <summary>Wind weight grows with height between y0 and y1 (used by foliage sway).</summary>
        public void SetAlphaByHeight(float y0, float y1, int fromVertex = 0)
        {
            for (int i = fromVertex; i < Colors.Count; i++)
            {
                float a = Mathf.Clamp01(Mathf.InverseLerp(y0, y1, Vertices[i].y));
                Colors[i] = NumuColor.WithAlpha(Colors[i], a * a);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (Vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            var linear = new List<Color>(Colors.Count);
            foreach (var c in Colors) linear.Add(new Color(c.linear.r, c.linear.g, c.linear.b, c.a));
            mesh.SetColors(linear);
            mesh.SetUVs(0, UVs);
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Generic primitive generators. Every primitive appends into a builder through a matrix.</summary>
    public static class Shapes
    {
        public static Matrix4x4 TRS(Vector3 p, Quaternion r, Vector3 s) => Matrix4x4.TRS(p, r, s);
        public static Matrix4x4 T(Vector3 p) => Matrix4x4.Translate(p);
        public static Matrix4x4 TR(Vector3 p, Quaternion r) => Matrix4x4.TRS(p, r, Vector3.one);
        public static Quaternion Euler(float x, float y, float z) => Quaternion.Euler(x, y, z);

        static void Emit(MeshBuilder mb, MeshBuilder part, Matrix4x4 m) => mb.Append(part, m);

        // ---------------------------------------------------------------- boxes

        static readonly Vector3[] FaceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        static readonly Vector3[] FaceU = { Vector3.up, Vector3.forward, Vector3.forward, Vector3.right, Vector3.right, Vector3.up };
        static readonly Vector3[] FaceV = { Vector3.forward, Vector3.up, Vector3.right, Vector3.forward, Vector3.up, Vector3.right };

        public static void Box(MeshBuilder mb, Matrix4x4 m, Vector3 size, Color c)
        {
            var part = new MeshBuilder();
            Vector3 h = size * 0.5f;
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = FaceN[f], u = FaceU[f], v = FaceV[f];
                Vector3 ctr = Vector3.Scale(n, h);
                Vector3 uu = Vector3.Scale(u, h), vv = Vector3.Scale(v, h);
                int i0 = part.Add(ctr - uu - vv, n, c, new Vector2(0, 0));
                int i1 = part.Add(ctr + uu - vv, n, c, new Vector2(1, 0));
                int i2 = part.Add(ctr + uu + vv, n, c, new Vector2(1, 1));
                int i3 = part.Add(ctr - uu + vv, n, c, new Vector2(0, 1));
                part.QuadFacing(i0, i1, i2, i3, n);
            }
            Emit(mb, part, m);
        }

        static List<float> RoundCoords(float half, float r, int seg)
        {
            float inner = Mathf.Max(0f, half - r);
            var list = new List<float>();
            for (int k = seg; k >= 1; k--) list.Add(-(inner + r * Mathf.Tan(k / (float)seg * Mathf.PI * 0.25f)));
            list.Add(-inner);
            list.Add(inner);
            for (int k = 1; k <= seg; k++) list.Add(inner + r * Mathf.Tan(k / (float)seg * Mathf.PI * 0.25f));
            return list;
        }

        /// <summary>Box with rounded edges. <paramref name="noise"/> displaces vertices for a hand-made feel.</summary>
        public static void RoundedBox(MeshBuilder mb, Matrix4x4 m, Vector3 size, float radius, int seg, Color c, float noise = 0f, int seed = 0)
        {
            var part = new MeshBuilder();
            Vector3 h = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.999f);
            Vector3 inner = h - Vector3.one * radius;
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = FaceN[f], u = FaceU[f], v = FaceV[f];
                float hu = Vector3.Dot(Abs(u), h), hv = Vector3.Dot(Abs(v), h), hn = Vector3.Dot(Abs(n), h);
                var cu = RoundCoords(hu, radius, seg);
                var cv = RoundCoords(hv, radius, seg);
                int baseIdx = part.VertexCount;
                for (int j = 0; j < cv.Count; j++)
                {
                    for (int i = 0; i < cu.Count; i++)
                    {
                        Vector3 p = n * hn + u * cu[i] + v * cv[j];
                        Vector3 q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 d = p - q;
                        Vector3 nrm;
                        if (d.sqrMagnitude > 1e-10f)
                        {
                            nrm = d.normalized;
                            p = q + nrm * radius;
                        }
                        else nrm = n;
                        if (noise > 0f)
                        {
                            Vector3 wp = p + Vector3.one * (seed * 3.17f);
                            p += nrm * (NumuNoise.Signed(wp * 1.3f) * noise);
                        }
                        part.Add(p, nrm, c, new Vector2(i / (float)(cu.Count - 1), j / (float)(cv.Count - 1)));
                    }
                }
                int w = cu.Count;
                for (int j = 0; j < cv.Count - 1; j++)
                {
                    for (int i = 0; i < w - 1; i++)
                    {
                        int a = baseIdx + j * w + i;
                        part.QuadFacing(a, a + 1, a + w + 1, a + w, n);
                    }
                }
            }
            Emit(mb, part, m);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // ---------------------------------------------------------------- lathe / cylinders

        /// <summary>Revolves a profile of (radius, y) points around +Y. Profile runs bottom-centre → outside → top-centre.</summary>
        public static void Lathe(MeshBuilder mb, Matrix4x4 m, IList<Vector2> profile, int segments, Color c, bool smooth = true, float arc = 360f)
        {
            var part = new MeshBuilder();
            int pc = profile.Count;
            var segN = new Vector2[pc - 1];
            for (int k = 0; k < pc - 1; k++)
            {
                Vector2 d = profile[k + 1] - profile[k];
                segN[k] = new Vector2(d.y, -d.x).normalized;
            }
            bool full = arc >= 359.9f;
            int ringCount = full ? segments : segments + 1;
            if (smooth)
            {
                var ringStart = new int[pc];
                for (int k = 0; k < pc; k++)
                {
                    Vector2 n2 = k == 0 ? segN[0] : k == pc - 1 ? segN[pc - 2] : (segN[k - 1] + segN[k]).normalized;
                    ringStart[k] = part.VertexCount;
                    for (int s = 0; s < ringCount; s++)
                    {
                        float a = s / (float)segments * arc * Mathf.Deg2Rad;
                        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                        part.Add(new Vector3(profile[k].x * ca, profile[k].y, profile[k].x * sa), new Vector3(n2.x * ca, n2.y, n2.x * sa), c, new Vector2(s / (float)segments, k / (float)(pc - 1)));
                    }
                }
                for (int k = 0; k < pc - 1; k++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        int s1 = full ? (s + 1) % segments : s + 1;
                        float a = (s + 0.5f) / segments * arc * Mathf.Deg2Rad;
                        Vector3 outward = new Vector3(segN[k].x * Mathf.Cos(a), segN[k].y, segN[k].x * Mathf.Sin(a));
                        part.QuadFacing(ringStart[k] + s, ringStart[k] + s1, ringStart[k + 1] + s1, ringStart[k + 1] + s, outward);
                    }
                }
            }
            else
            {
                for (int k = 0; k < pc - 1; k++)
                {
                    int r0 = part.VertexCount;
                    for (int s = 0; s < ringCount; s++)
                    {
                        float a = s / (float)segments * arc * Mathf.Deg2Rad;
                        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                        Vector3 n = new Vector3(segN[k].x * ca, segN[k].y, segN[k].x * sa);
                        part.Add(new Vector3(profile[k].x * ca, profile[k].y, profile[k].x * sa), n, c);
                        part.Add(new Vector3(profile[k + 1].x * ca, profile[k + 1].y, profile[k + 1].x * sa), n, c);
                    }
                    for (int s = 0; s < segments; s++)
                    {
                        int s1 = full ? (s + 1) % segments : s + 1;
                        float a = (s + 0.5f) / segments * arc * Mathf.Deg2Rad;
                        Vector3 outward = new Vector3(segN[k].x * Mathf.Cos(a), segN[k].y, segN[k].x * Mathf.Sin(a));
                        part.QuadFacing(r0 + s * 2, r0 + s1 * 2, r0 + s1 * 2 + 1, r0 + s * 2 + 1, outward);
                    }
                }
            }
            Emit(mb, part, m);
        }

        public static void Cylinder(MeshBuilder mb, Matrix4x4 m, float rBottom, float rTop, float height, int segments, Color c, bool caps = true)
        {
            var prof = new List<Vector2>();
            if (caps) prof.Add(new Vector2(0, 0));
            prof.Add(new Vector2(rBottom, 0));
            prof.Add(new Vector2(rTop, height));
            if (caps) prof.Add(new Vector2(0, height));
            Lathe(mb, m, prof, segments, c, false);
        }

        /// <summary>Cylinder lying between two points.</summary>
        public static void Beam(MeshBuilder mb, Vector3 a, Vector3 b, float radius, int segments, Color c)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            Quaternion r = Quaternion.FromToRotation(Vector3.up, d.normalized);
            Cylinder(mb, TR(a, r), radius, radius, d.magnitude, segments, c);
        }

        public static void Cone(MeshBuilder mb, Matrix4x4 m, float radius, float height, int segments, Color c)
        {
            Lathe(mb, m, new[] { new Vector2(0, 0), new Vector2(radius, 0), new Vector2(0, height) }, segments, c, false);
        }

        // ---------------------------------------------------------------- spheres and blobs

        static List<Vector3> _icoVerts;
        static List<int> _icoTris;
        static int _icoLevel = -1;

        static void BuildIco(int level)
        {
            if (_icoLevel == level) return;
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            var tris = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };
            for (int l = 0; l < level; l++)
            {
                var cache = new Dictionary<long, int>();
                var nt = new List<int>(tris.Count * 4);
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int idx)) return idx;
                    verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
                    cache[key] = verts.Count - 1;
                    return verts.Count - 1;
                }
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = nt;
            }
            _icoVerts = verts;
            _icoTris = tris;
            _icoLevel = level;
        }

        /// <summary>
        /// Noisy ellipsoid with smooth normals. <paramref name="flattenBottom"/> squashes the lower half (clouds, bushes).
        /// <paramref name="colorFn"/> lets callers shade by local direction (e.g. lighter tops).
        /// </summary>
        public static void Blob(MeshBuilder mb, Matrix4x4 m, Vector3 radii, Color c, float noise = 0f, float freq = 1.5f, int seed = 0,
            int level = 2, float flattenBottom = 0f, System.Func<Vector3, Color> colorFn = null)
        {
            BuildIco(level);
            var part = new MeshBuilder();
            Vector3 off = new Vector3(seed * 1.71f, seed * 2.93f, seed * 0.37f);
            foreach (var d in _icoVerts)
            {
                float k = 1f + (noise > 0f ? NumuNoise.Signed(d * freq + off) * noise : 0f);
                Vector3 p = Vector3.Scale(d * k, radii);
                if (flattenBottom > 0f && p.y < 0f) p.y *= 1f - flattenBottom;
                part.Add(p, d, colorFn != null ? colorFn(d) : c);
            }
            foreach (int idx in _icoTris) part.Triangles.Add(idx);
            // Make sure winding faces outwards, then smooth normals.
            for (int i = 0; i < part.Triangles.Count; i += 3)
            {
                int a = part.Triangles[i], b = part.Triangles[i + 1], cc = part.Triangles[i + 2];
                Vector3 fn = Vector3.Cross(part.Vertices[b] - part.Vertices[a], part.Vertices[cc] - part.Vertices[a]);
                Vector3 ctr = (part.Vertices[a] + part.Vertices[b] + part.Vertices[cc]) / 3f;
                if (Vector3.Dot(fn, ctr) < 0f)
                {
                    part.Triangles[i + 1] = cc;
                    part.Triangles[i + 2] = b;
                }
            }
            part.RecalculateNormals();
            Emit(mb, part, m);
        }

        public static void Sphere(MeshBuilder mb, Vector3 center, float radius, Color c, int level = 2)
        {
            Blob(mb, T(center), Vector3.one * radius, c, 0f, 1f, 0, level);
        }

        public static void Ellipsoid(MeshBuilder mb, Matrix4x4 m, Vector3 radii, Color c, int level = 2)
        {
            Blob(mb, m, radii, c, 0f, 1f, 0, level);
        }

        // ---------------------------------------------------------------- torus and tubes

        public static void Torus(MeshBuilder mb, Matrix4x4 m, float R, float r, int segU, int segV, Color c, float arcDeg = 360f)
        {
            var part = new MeshBuilder();
            bool full = arcDeg >= 359.9f;
            int uCount = full ? segU : segU + 1;
            for (int i = 0; i < uCount; i++)
            {
                float u = i / (float)segU * arcDeg * Mathf.Deg2Rad;
                Vector3 ringC = new Vector3(Mathf.Cos(u) * R, 0, Mathf.Sin(u) * R);
                Vector3 outDir = new Vector3(Mathf.Cos(u), 0, Mathf.Sin(u));
                for (int j = 0; j < segV; j++)
                {
                    float v = j / (float)segV * Mathf.PI * 2f;
                    Vector3 n = outDir * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    part.Add(ringC + n * r, n, c);
                }
            }
            for (int i = 0; i < segU; i++)
            {
                int i1 = full ? (i + 1) % segU : i + 1;
                for (int j = 0; j < segV; j++)
                {
                    int j1 = (j + 1) % segV;
                    int a = i * segV + j, b = i1 * segV + j, cc = i1 * segV + j1, d = i * segV + j1;
                    Vector3 outward = part.Normals[a] + part.Normals[cc];
                    part.QuadFacing(a, b, cc, d, outward);
                }
            }
            Emit(mb, part, m);
        }

        /// <summary>Tube swept along a polyline (ropes, slides, rails, tails).</summary>
        public static void Tube(MeshBuilder mb, IList<Vector3> path, float radius, int sides, Color c, bool caps = true, System.Func<float, float> radiusFn = null)
        {
            if (path.Count < 2) return;
            var part = new MeshBuilder();
            Vector3 prevNormal = Vector3.zero;
            var centers = new List<Vector3>();
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 t = (i == 0 ? path[1] - path[0] : i == path.Count - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1]).normalized;
                Vector3 nrm;
                if (i == 0)
                {
                    nrm = Vector3.Cross(t, Mathf.Abs(t.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                }
                else
                {
                    nrm = (prevNormal - Vector3.Dot(prevNormal, t) * t).normalized;
                }
                prevNormal = nrm;
                Vector3 bin = Vector3.Cross(t, nrm);
                float rr = radiusFn != null ? radiusFn(i / (float)(path.Count - 1)) * radius : radius;
                for (int s = 0; s < sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = nrm * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    part.Add(path[i] + dir * rr, dir, c, new Vector2(s / (float)sides, i / (float)(path.Count - 1)));
                }
                centers.Add(path[i]);
            }
            for (int i = 0; i < path.Count - 1; i++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int s1 = (s + 1) % sides;
                    int a = i * sides + s, b = i * sides + s1, cc = (i + 1) * sides + s1, d = (i + 1) * sides + s;
                    part.QuadFacing(a, b, cc, d, part.Normals[a] + part.Normals[cc]);
                }
            }
            if (caps)
            {
                for (int end = 0; end < 2; end++)
                {
                    int ring = end == 0 ? 0 : path.Count - 1;
                    Vector3 t = end == 0 ? (path[0] - path[1]).normalized : (path[ring] - path[ring - 1]).normalized;
                    int ci = part.Add(path[ring], t, c);
                    for (int s = 0; s < sides; s++)
                    {
                        int a = ring * sides + s, b = ring * sides + (s + 1) % sides;
                        int na = part.Add(part.Vertices[a], t, c);
                        int nb = part.Add(part.Vertices[b], t, c);
                        part.TriFacing(ci, na, nb, t);
                    }
                }
            }
            Emit(mb, part, Matrix4x4.identity);
        }

        // ---------------------------------------------------------------- extrusion

        /// <summary>Extrudes a 2D outline (XY plane) along Z, centred on z = 0. Front face looks down -Z.</summary>
        public static void Extrude(MeshBuilder mb, Matrix4x4 m, IList<Vector2> shape, float depth, Color c, float smoothAngle = 35f, Color? sideColor = null)
        {
            var pts = new List<Vector2>(shape);
            if (SignedArea(pts) < 0f) pts.Reverse();
            var part = new MeshBuilder();
            var tris = Triangulate(pts);
            float hz = depth * 0.5f;
            int n = pts.Count;
            int frontStart = part.VertexCount;
            foreach (var p in pts) part.Add(new Vector3(p.x, p.y, -hz), Vector3.back, c, p);
            int backStart = part.VertexCount;
            foreach (var p in pts) part.Add(new Vector3(p.x, p.y, hz), Vector3.forward, c, p);
            for (int i = 0; i < tris.Count; i += 3)
            {
                part.TriFacing(frontStart + tris[i], frontStart + tris[i + 1], frontStart + tris[i + 2], Vector3.back);
                part.TriFacing(backStart + tris[i], backStart + tris[i + 1], backStart + tris[i + 2], Vector3.forward);
            }
            Color sc = sideColor ?? c;
            var edgeN = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 d = pts[(i + 1) % n] - pts[i];
                edgeN[i] = new Vector3(d.y, -d.x, 0).normalized;
            }
            float cosLimit = Mathf.Cos(smoothAngle * Mathf.Deg2Rad);
            for (int i = 0; i < n; i++)
            {
                int i1 = (i + 1) % n;
                Vector3 nPrev = edgeN[(i - 1 + n) % n], nCur = edgeN[i], nNext = edgeN[i1];
                Vector3 n0 = Vector3.Dot(nPrev, nCur) > cosLimit ? (nPrev + nCur).normalized : nCur;
                Vector3 n1 = Vector3.Dot(nCur, nNext) > cosLimit ? (nCur + nNext).normalized : nCur;
                int a = part.Add(new Vector3(pts[i].x, pts[i].y, -hz), n0, sc);
                int b = part.Add(new Vector3(pts[i1].x, pts[i1].y, -hz), n1, sc);
                int cc = part.Add(new Vector3(pts[i1].x, pts[i1].y, hz), n1, sc);
                int d = part.Add(new Vector3(pts[i].x, pts[i].y, hz), n0, sc);
                part.QuadFacing(a, b, cc, d, nCur);
            }
            Emit(mb, part, m);
        }

        public static float SignedArea(IList<Vector2> p)
        {
            float a = 0f;
            for (int i = 0; i < p.Count; i++)
            {
                Vector2 p0 = p[i], p1 = p[(i + 1) % p.Count];
                a += p0.x * p1.y - p1.x * p0.y;
            }
            return a * 0.5f;
        }

        /// <summary>Ear-clipping triangulation of a simple counter-clockwise polygon.</summary>
        public static List<int> Triangulate(IList<Vector2> pts)
        {
            var result = new List<int>();
            var idx = new List<int>();
            for (int i = 0; i < pts.Count; i++) idx.Add(i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i - 1 + idx.Count) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = pts[ia], b = pts[ib], c = pts[ic];
                    if (Cross2(b - a, c - b) <= 1e-7f) continue;
                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == ia || j == ib || j == ic) continue;
                        if (PointInTri(pts[j], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    result.Add(ia);
                    result.Add(ib);
                    result.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3)
            {
                result.Add(idx[0]);
                result.Add(idx[1]);
                result.Add(idx[2]);
            }
            return result;
        }

        static float Cross2(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross2(b - a, p - a), d2 = Cross2(c - b, p - b), d3 = Cross2(a - c, p - c);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // ---------------------------------------------------------------- 2D outlines

        public static List<Vector2> RoundedRect(float w, float h, float r, int cornerSeg = 6)
        {
            var pts = new List<Vector2>();
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f - 1e-4f);
            Vector2[] centers = { new Vector2(w / 2 - r, h / 2 - r), new Vector2(-w / 2 + r, h / 2 - r), new Vector2(-w / 2 + r, -h / 2 + r), new Vector2(w / 2 - r, -h / 2 + r) };
            for (int k = 0; k < 4; k++)
            {
                for (int s = 0; s <= cornerSeg; s++)
                {
                    float a = (k * 90f + s / (float)cornerSeg * 90f) * Mathf.Deg2Rad;
                    pts.Add(centers[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }
            return pts;
        }

        public static List<Vector2> Circle(float r, int seg)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return pts;
        }

        public static List<Vector2> Star(float rOuter, float rInner, int points, float roundness = 0f)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < points * 2; i++)
            {
                float a = (90f + i * 180f / points) * Mathf.Deg2Rad;
                float r = i % 2 == 0 ? rOuter : rInner;
                pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return pts;
        }

        /// <summary>Rectangle with a semicircular top (doors, arched windows).</summary>
        public static List<Vector2> Arch(float w, float h, int seg = 10)
        {
            var pts = new List<Vector2> { new Vector2(-w / 2, 0), new Vector2(w / 2, 0) };
            float r = w / 2;
            float straight = Mathf.Max(0f, h - r);
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI;
                pts.Add(new Vector2(Mathf.Cos(a) * r, straight + Mathf.Sin(a) * r));
            }
            return pts;
        }

        // ---------------------------------------------------------------- curves

        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>Samples a Catmull-Rom spline through the control points at roughly even spacing.</summary>
        public static List<Vector3> SampleSpline(IList<Vector3> pts, float spacing, bool closed = false)
        {
            var dense = new List<Vector3>();
            int n = pts.Count;
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                Vector3 p0 = closed ? pts[(i - 1 + n) % n] : pts[Mathf.Max(i - 1, 0)];
                Vector3 p1 = pts[i % n];
                Vector3 p2 = pts[(i + 1) % n];
                Vector3 p3 = closed ? pts[(i + 2) % n] : pts[Mathf.Min(i + 2, n - 1)];
                for (int k = 0; k < 16; k++) dense.Add(CatmullRom(p0, p1, p2, p3, k / 16f));
            }
            if (!closed) dense.Add(pts[n - 1]);
            var cum = new List<float> { 0f };
            for (int i = 1; i < dense.Count; i++) cum.Add(cum[i - 1] + Vector3.Distance(dense[i - 1], dense[i]));
            float total = cum[cum.Count - 1] + (closed ? Vector3.Distance(dense[dense.Count - 1], dense[0]) : 0f);
            int count = Mathf.Max(closed ? 8 : 2, Mathf.RoundToInt(total / spacing) + (closed ? 0 : 1));
            var result = new List<Vector3>();
            int j = 0;
            for (int i = 0; i < count; i++)
            {
                float target = closed ? total * i / count : total * i / (count - 1);
                while (j < cum.Count - 2 && cum[j + 1] < target) j++;
                float segLen = cum[j + 1] - cum[j];
                float t = segLen > 1e-6f ? (target - cum[j]) / segLen : 0f;
                result.Add(Vector3.Lerp(dense[j], dense[j + 1], Mathf.Clamp01(t)));
            }
            return result;
        }
    }
}
