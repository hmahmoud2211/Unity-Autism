using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Re-shapes the Rocketbox boy's head toward the NUMU (JeStiMulE-style) look in code, as a derived mesh; the
    /// imported FBX is never modified.
    /// <list type="bullet">
    /// <item>Larger eyes: each eyeball and its lids are scaled about the eye's pivot and blended smoothly into the
    /// face. Blendshape deltas get the same transform, so blinks still close and expressions keep their shape.</item>
    /// <item>A modelled hair volume: the painted hair area is copied as a shell offset from the skull (thickest on
    /// the crown, tapering to the hairline) and closed by a skirt. The shell shares the skull's skin weights and
    /// blendshape deltas, so it follows the head and the forehead exactly.</item>
    /// </list>
    /// </summary>
    public static class NumuStylizedHead
    {
        public class Settings
        {
            public float eyeScale = 1.22f;     // uniform scale of eyeball and lids
            public float eyeInner = 0.0175f;   // full scale inside this radius (m), just beyond the eyeball
            public float eyeOuter = 0.034f;    // blends back to the original face at this radius
            public float hairTop = 0.013f;     // shell thickness on the crown (m)
            public float hairSide = 0.008f;    // over the ears and the back
            public float hairEdge = 0.0035f;   // at the hairline
            public float hairTaper = 0.022f;   // distance from the hairline over which the thickness builds up
            public int hairSmoothing = 12;
            public bool hairShell = true;      // only for short hair painted on the scalp (not hair cards or scarves)
            public Color hairColor = new Color(0.73f, 0.49f, 0.26f); // the painted hair colour (sRGB) to find the hair area
        }

        public static Mesh Build(SkinnedMeshRenderer source, int headSubmesh, Texture2D headTexture, string assetPath, Settings s, out string report)
        {
            var src = source.sharedMesh;
            int n = src.vertexCount;
            var v = new List<Vector3>(src.vertices);
            var nrm = new List<Vector3>(src.normals);
            var tan = new List<Vector4>(src.tangents);
            var uv = new List<Vector2>(src.uv);
            var bw = new List<BoneWeight>(src.boneWeights);
            var tris = Enumerable.Range(0, src.subMeshCount).Select(i => new List<int>(src.GetTriangles(i))).ToList();
            var shapes = ReadBlendShapes(src);
            var head = src.GetSubMesh(headSubmesh);
            var headVerts = Enumerable.Range(head.firstVertex, head.vertexCount);

            // ---------------------------------------------------------------- larger eyes
            int eyesScaled = 0;
            foreach (string eye in new[] { "LEye", "REye" })
            {
                int bone = System.Array.FindIndex(source.bones, b => b != null && b.name.EndsWith(eye));
                if (bone < 0) continue;
                Vector3 c = src.bindposes[bone].inverse.MultiplyPoint(Vector3.zero);
                float k = s.eyeScale - 1f;
                foreach (int i in headVerts)
                {
                    Vector3 d = v[i] - c;
                    float r = d.magnitude;
                    if (r >= s.eyeOuter) continue;
                    float t = Mathf.InverseLerp(s.eyeInner, s.eyeOuter, r);
                    float w = 1f - t * t * (3f - 2f * t);
                    float dw = r <= s.eyeInner ? 0f : -6f * t * (1f - t) / (s.eyeOuter - s.eyeInner);
                    // Displacement k·w(r)·d. Its Jacobian is a·I + b·û·ûᵀ, with a = 1 + k·w and b = k·r·w'(r).
                    float a = 1f + k * w, b = k * r * dw;
                    Vector3 u = r > 1e-6f ? d / r : Vector3.zero;
                    v[i] = c + d * a;
                    // The original (smooth) normals are kept: transforming them by the blend's Jacobian shows its
                    // falloff as a light band across the eyes. Deltas (small displacements) follow the Jacobian.
                    foreach (var shape in shapes)
                        foreach (var f in shape.frames)
                        {
                            var dv = f.dv[i];
                            if (dv != Vector3.zero) f.dv[i] = a * dv + b * Vector3.Dot(u, dv) * u;
                        }
                    eyesScaled++;
                }
            }

            // ---------------------------------------------------------------- hair shell
            var hairTris = new List<int>();
            var shellOf = new Dictionary<int, int>();
            int skirtQuads = 0;
            if (s.hairShell)
            {
                var pixels = ReadPixels(headTexture, out int tw, out int th);
                bool IsHair(int i)
                {
                    var p = uv[i];
                    if (p.y < 0.45f) return false; // the eye and mouth islands sit in the lower part of the texture
                    int x = Mathf.Clamp((int)(p.x * tw), 0, tw - 1), y = Mathf.Clamp((int)(p.y * th), 0, th - 1);
                    var col = pixels[y * tw + x];
                    // The same colour as the hair at any brightness (shading), and not much else.
                    Color h = s.hairColor;
                    float k = (col.r * h.r + col.g * h.g + col.b * h.b) / (h.r * h.r + h.g * h.g + h.b * h.b);
                    if (k < 0.7f || k > 1.35f) return false;
                    float dr = col.r - k * h.r, dg = col.g - k * h.g, db = col.b - k * h.b;
                    return dr * dr + dg * dg + db * db < 0.003f;
                }
                // Vertices that share a position (UV seams) are one point of the surface.
                var weld = new Dictionary<Vector3Int, int>();
                var group = new int[n];
                var groupPos = new List<Vector3>();
                foreach (int i in headVerts)
                {
                    var key = Vector3Int.RoundToInt(v[i] * 20000f);
                    if (!weld.TryGetValue(key, out int gi)) { gi = groupPos.Count; weld[key] = gi; groupPos.Add(v[i]); }
                    group[i] = gi;
                }
                // Every triangle touching painted hair: the shell then covers the painted hairline entirely, so no painted
                // strands are left on the skin to stretch when the forehead moves.
                hairTris = new List<int>();
                var headTris = tris[headSubmesh];
                for (int t = 0; t < headTris.Count; t += 3)
                    if (IsHair(headTris[t]) || IsHair(headTris[t + 1]) || IsHair(headTris[t + 2]))
                        hairTris.AddRange(new[] { headTris[t], headTris[t + 1], headTris[t + 2] });
                hairTris = LargestComponent(hairTris, group);
                // One clean hair colour for the shell (the painted texture has a soft gradient at the hairline): the UV of
                // the hair vertex whose colour is closest to the median hair colour.
                var hairVerts = hairTris.Distinct().Where(IsHair).ToList();
                Color Sample(Vector2 p) => pixels[Mathf.Clamp((int)(p.y * th), 0, th - 1) * tw + Mathf.Clamp((int)(p.x * tw), 0, tw - 1)];
                var cols = hairVerts.Select(i => Sample(uv[i])).ToList();
                var median = new Color(cols.Select(c => c.r).OrderBy(x => x).ElementAt(cols.Count / 2), cols.Select(c => c.g).OrderBy(x => x).ElementAt(cols.Count / 2), cols.Select(c => c.b).OrderBy(x => x).ElementAt(cols.Count / 2));
                Vector2 hairUV = uv[hairVerts.OrderBy(i => { var c = Sample(uv[i]); return Mathf.Abs(c.r - median.r) + Mathf.Abs(c.g - median.g) + Mathf.Abs(c.b - median.b); }).First()];

                // Welded edges used by one hair triangle form the hairline.
                var edgeUse = new Dictionary<(int, int), int>();
                for (int t = 0; t < hairTris.Count; t += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        var key = Edge(group[hairTris[t + e]], group[hairTris[t + (e + 1) % 3]]);
                        edgeUse[key] = edgeUse.TryGetValue(key, out int c) ? c + 1 : 1;
                    }
                var neighbours = new Dictionary<int, HashSet<int>>();
                foreach (var e in edgeUse.Keys)
                {
                    (neighbours.TryGetValue(e.Item1, out var a) ? a : neighbours[e.Item1] = new HashSet<int>()).Add(e.Item2);
                    (neighbours.TryGetValue(e.Item2, out var b) ? b : neighbours[e.Item2] = new HashSet<int>()).Add(e.Item1);
                }
                var boundary = new HashSet<int>(edgeUse.Where(kv => kv.Value == 1).SelectMany(kv => new[] { kv.Key.Item1, kv.Key.Item2 }));
                var dist = Geodesic(neighbours, boundary, groupPos);
                // A smooth hairline: relax the boundary loop along itself (the triangle edges zig-zag).
                var basePos = new List<Vector3>(groupPos);
                var loopNb = new Dictionary<int, List<int>>();
                foreach (var e in edgeUse.Where(kv => kv.Value == 1).Select(kv => kv.Key))
                {
                    (loopNb.TryGetValue(e.Item1, out var a) ? a : loopNb[e.Item1] = new List<int>()).Add(e.Item2);
                    (loopNb.TryGetValue(e.Item2, out var b) ? b : loopNb[e.Item2] = new List<int>()).Add(e.Item1);
                }
                for (int it = 0; it < 8; it++)
                {
                    var next = new List<Vector3>(basePos);
                    foreach (var kv in loopNb)
                        if (kv.Value.Count == 2) next[kv.Key] = Vector3.Lerp(basePos[kv.Key], (basePos[kv.Value[0]] + basePos[kv.Value[1]]) * 0.5f, 0.5f);
                    basePos = next;
                }

                // Outward direction per welded point: the original (smooth) vertex normals, averaged.
                var gNormal = new Dictionary<int, Vector3>();
                foreach (int i in hairTris.Distinct()) gNormal[group[i]] = (gNormal.TryGetValue(group[i], out var gn) ? gn : Vector3.zero) + nrm[i];
                float zMin = gNormal.Keys.Min(gi => groupPos[gi].z), zMax = gNormal.Keys.Max(gi => groupPos[gi].z);
                var offset = new Dictionary<int, Vector3>();
                foreach (var gi in gNormal.Keys)
                {
                    float up = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(zMin + (zMax - zMin) * 0.35f, zMax, groupPos[gi].z));
                    float core = Mathf.Lerp(s.hairSide, s.hairTop, up);
                    float taper = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dist[gi] / s.hairTaper));
                    offset[gi] = gNormal[gi].normalized * Mathf.Lerp(s.hairEdge, core, taper);
                }
                // A smooth helmet rather than a bumpy copy of the skull: relax the offsets, keeping the hairline fixed.
                for (int it = 0; it < s.hairSmoothing; it++)
                {
                    var next = new Dictionary<int, Vector3>(offset);
                    foreach (var gi in offset.Keys)
                    {
                        if (boundary.Contains(gi) || !neighbours.TryGetValue(gi, out var nb) || nb.Count == 0) continue;
                        Vector3 avg = Vector3.zero;
                        foreach (int j in nb) avg += offset[j];
                        next[gi] = Vector3.Lerp(offset[gi], avg / nb.Count, 0.5f);
                    }
                    offset = next;
                }

                // Shell vertices: one copy per original vertex (keeps UV seams), same weights and blendshape deltas.
                shellOf = new Dictionary<int, int>();
                int Copy(int i, Vector3 pos, Vector3 normal)
                {
                    int ni = v.Count;
                    v.Add(pos); nrm.Add(normal); uv.Add(uv[i]); bw.Add(bw[i]);
                    if (tan.Count > 0) tan.Add(tan[i]);
                    foreach (var shape in shapes) foreach (var f in shape.frames) { f.dv.Add(f.dv[i]); f.dn.Add(f.dn[i]); f.dt.Add(f.dt[i]); }
                    return ni;
                }
                foreach (int i in hairTris.Distinct())
                {
                    shellOf[i] = Copy(i, basePos[group[i]] + offset[group[i]], nrm[i]);
                    uv[shellOf[i]] = hairUV;
                }
                int shellStart = headTris.Count;
                for (int t = 0; t < hairTris.Count; t++) headTris.Add(shellOf[hairTris[t]]);
                RecomputeNormals(v, nrm, headTris, shellStart, headTris.Count, shellOf.Values, group: null);

                // Skirt: closes the gap between the shell's edge and the scalp along the hairline.
                skirtQuads = 0;
                for (int t = 0; t < hairTris.Count; t += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        int a = hairTris[t + e], b = hairTris[t + (e + 1) % 3], c = hairTris[t + (e + 2) % 3];
                        if (edgeUse[Edge(group[a], group[b])] != 1) continue;
                        // Outward = away from the hair triangle, along the surface.
                        Vector3 mid = (v[a] + v[b]) * 0.5f;
                        Vector3 outward = Vector3.ProjectOnPlane(mid - v[c], nrm[a] + nrm[b]).normalized;
                        int a0 = Copy(a, basePos[group[a]] - nrm[a] * 0.001f, outward), b0 = Copy(b, basePos[group[b]] - nrm[b] * 0.001f, outward);
                        int a1 = Copy(shellOf[a], v[shellOf[a]], outward), b1 = Copy(shellOf[b], v[shellOf[b]], outward);
                        uv[a0] = uv[b0] = hairUV;
                        // Unity's front faces wind clockwise seen from outside: order the quad so its normal is "outward".
                        Vector3 fn = Vector3.Cross(v[b0] - v[a0], v[a1] - v[a0]);
                        if (Vector3.Dot(fn, outward) >= 0) headTris.AddRange(new[] { a0, b0, a1, b0, b1, a1 });
                        else headTris.AddRange(new[] { a0, a1, b0, b0, a1, b1 });
                        skirtQuads++;
                    }
            }

            // ---------------------------------------------------------------- write the derived mesh
            // Written into the existing asset when there is one, so every reference to it (prefabs, scenes) stays valid.
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            else mesh.Clear();
            mesh.name = Path.GetFileNameWithoutExtension(assetPath);
            mesh.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(v);
            mesh.SetNormals(nrm);
            if (tan.Count == v.Count) mesh.SetTangents(tan);
            mesh.SetUVs(0, uv);
            mesh.boneWeights = bw.ToArray();
            mesh.bindposes = src.bindposes;
            mesh.subMeshCount = tris.Count;
            for (int i = 0; i < tris.Count; i++) mesh.SetTriangles(tris[i], i, false);
            foreach (var shape in shapes)
                foreach (var f in shape.frames)
                    mesh.AddBlendShapeFrame(shape.name, f.weight, f.dv.ToArray(), f.dn.ToArray(), f.dt.ToArray());
            mesh.RecalculateBounds();
            if (isNew)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                AssetDatabase.CreateAsset(mesh, assetPath);
            }
            else EditorUtility.SetDirty(mesh);
            report = $"{Path.GetFileNameWithoutExtension(assetPath)}: {eyesScaled} vertices reshaped around the eyes (x{s.eyeScale})" +
                     (s.hairShell ? $", hair shell of {hairTris.Count / 3} triangles ({s.hairTop * 100f:F1} cm on the crown) with a {skirtQuads}-quad skirt" : "") +
                     $"; {mesh.vertexCount} vertices, {mesh.blendShapeCount} blendshapes.";
            return mesh;
        }

        // ------------------------------------------------------------------ helpers

        class Frame { public float weight; public List<Vector3> dv, dn, dt; }
        class Shape { public string name; public List<Frame> frames = new List<Frame>(); }

        static List<Shape> ReadBlendShapes(Mesh m)
        {
            var list = new List<Shape>();
            int n = m.vertexCount;
            for (int s = 0; s < m.blendShapeCount; s++)
            {
                var shape = new Shape { name = m.GetBlendShapeName(s) };
                for (int f = 0; f < m.GetBlendShapeFrameCount(s); f++)
                {
                    Vector3[] dv = new Vector3[n], dn = new Vector3[n], dt = new Vector3[n];
                    m.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
                    shape.frames.Add(new Frame { weight = m.GetBlendShapeFrameWeight(s, f), dv = dv.ToList(), dn = dn.ToList(), dt = dt.ToList() });
                }
                list.Add(shape);
            }
            return list;
        }

        /// <summary>Reads a texture's pixels from its source file (the imported texture need not be readable).</summary>
        static Color[] ReadPixels(Texture2D tex, out int w, out int h)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            t.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(tex)));
            w = t.width; h = t.height;
            var px = t.GetPixels();
            Object.DestroyImmediate(t);
            return px;
        }

        static (int, int) Edge(int a, int b) => a < b ? (a, b) : (b, a);

        /// <summary>The biggest connected patch of triangles (by welded vertices): the hair cap, not stray islands.</summary>
        static List<int> LargestComponent(List<int> triList, int[] group)
        {
            int tc = triList.Count / 3;
            var parent = Enumerable.Range(0, tc).ToArray();
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            var owner = new Dictionary<int, int>();
            for (int t = 0; t < tc; t++)
                for (int e = 0; e < 3; e++)
                {
                    int g = group[triList[t * 3 + e]];
                    if (owner.TryGetValue(g, out int o)) parent[Find(t)] = Find(o);
                    else owner[g] = t;
                }
            var best = Enumerable.Range(0, tc).GroupBy(Find).OrderByDescending(g => g.Count()).First();
            var result = new List<int>();
            foreach (int t in best.OrderBy(t => t)) result.AddRange(new[] { triList[t * 3], triList[t * 3 + 1], triList[t * 3 + 2] });
            return result;
        }

        /// <summary>Distance along the surface from the hairline (multi-source Dijkstra over the edge graph).</summary>
        static Dictionary<int, float> Geodesic(Dictionary<int, HashSet<int>> nb, HashSet<int> sources, List<Vector3> pos)
        {
            var dist = nb.Keys.ToDictionary(k => k, k => float.MaxValue);
            var open = new SortedSet<(float, int)>();
            foreach (int s in sources) { dist[s] = 0f; open.Add((0f, s)); }
            while (open.Count > 0)
            {
                var (d, g) = open.Min;
                open.Remove(open.Min);
                if (d > dist[g]) continue;
                foreach (int j in nb[g])
                {
                    float nd = d + Vector3.Distance(pos[g], pos[j]);
                    if (nd >= dist[j]) continue;
                    open.Remove((dist[j], j));
                    dist[j] = nd;
                    open.Add((nd, j));
                }
            }
            return dist;
        }

        /// <summary>Smooth, area-weighted normals for the given vertices, shared across copies at the same position.</summary>
        static void RecomputeNormals(List<Vector3> v, List<Vector3> nrm, List<int> tris, int from, int to, IEnumerable<int> verts, int[] group)
        {
            var acc = new Dictionary<Vector3Int, Vector3>();
            for (int t = from; t < to; t += 3)
            {
                Vector3 a = v[tris[t]], b = v[tris[t + 1]], c = v[tris[t + 2]];
                Vector3 fn = Vector3.Cross(b - a, c - a);
                foreach (int i in new[] { tris[t], tris[t + 1], tris[t + 2] })
                {
                    var key = Vector3Int.RoundToInt(v[i] * 20000f);
                    acc[key] = (acc.TryGetValue(key, out var x) ? x : Vector3.zero) + fn;
                }
            }
            foreach (int i in verts)
                if (acc.TryGetValue(Vector3Int.RoundToInt(v[i] * 20000f), out var n) && n.sqrMagnitude > 0)
                {
                    // Keep the original orientation convention (outward): flip if the face winding disagrees.
                    var nn = n.normalized;
                    nrm[i] = Vector3.Dot(nn, nrm[i]) < 0 ? -nn : nn;
                }
        }
    }
}
