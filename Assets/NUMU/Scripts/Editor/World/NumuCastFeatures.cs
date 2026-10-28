using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Measures where each Rocketbox avatar's facial features sit in its head texture, from the facial rig rather than
    /// by guessing, so one texture pipeline (Tools/AssetPipeline/stylize_character.py) can restyle every avatar:
    /// eyebrows, eye openings, mouth and cheeks (surface points in front of the facial bones), the eyeball iris centre
    /// and its UV scale. Written as JSON to Tools/AssetPipeline/features/&lt;Avatar&gt;.json (UVs in Unity convention, v up).
    /// </summary>
    public static class NumuCastFeatures
    {
        public const string OutDir = "Tools/AssetPipeline/features";

        public static string ExportAll()
        {
            Directory.CreateDirectory(OutDir);
            var sb = new StringBuilder();
            foreach (var dir in Directory.GetDirectories(NumuCharacterSetup.AvatarRoot))
            {
                string avatar = Path.GetFileName(dir);
                sb.AppendLine(Export(avatar));
            }
            return sb.ToString();
        }

        public static string Export(string avatar)
        {
            string fbx = $"{NumuCharacterSetup.AvatarRoot}/{avatar}/{avatar}_facial.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var r = model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            if (r == null) return $"{avatar}: no skinned mesh";
            var m = r.sharedMesh;
            var v = m.vertices;
            var uv = m.uv;
            var bp = m.bindposes;
            int headSub = Enumerable.Range(0, m.subMeshCount).FirstOrDefault(i => r.sharedMaterials[i] != null && r.sharedMaterials[i].name.EndsWith("_head"));
            var head = m.GetSubMesh(headSub);
            Vector3 Bone(string suffix)
            {
                int i = System.Array.FindIndex(r.bones, b => b != null && b.name.EndsWith(suffix));
                return i < 0 ? Vector3.positiveInfinity : bp[i].inverse.MultiplyPoint(Vector3.zero);
            }
            Vector3 lEye = Bone("LEye"), rEye = Bone("REye");
            // Rocketbox meshes are Z-up and face -Y.
            var fwd = new Vector3(0, -1, 0);
            float eyeR = 0.0175f;
            var eyeballs = new HashSet<int>();
            var weights = m.boneWeights;
            int lIdx = System.Array.FindIndex(r.bones, b => b.name.EndsWith("LEye")), rIdx = System.Array.FindIndex(r.bones, b => b.name.EndsWith("REye"));
            for (int i = 0; i < v.Length; i++)
                if ((weights[i].boneIndex0 == lIdx || weights[i].boneIndex0 == rIdx) && weights[i].weight0 > 0.5f) eyeballs.Add(i);
            var skin = Enumerable.Range(head.firstVertex, head.vertexCount).Where(i => !eyeballs.Contains(i)).ToArray();

            // The skin surface point straight in front of a bone (closest to the forward ray through it).
            Vector2 Surface(Vector3 p)
            {
                int best = -1;
                float bestScore = float.MaxValue;
                foreach (int i in skin)
                {
                    Vector3 d = v[i] - p;
                    float depth = Vector3.Dot(d, fwd);
                    if (depth < -0.002f) continue;
                    float lateral = (d - fwd * depth).sqrMagnitude;
                    float score = lateral + 0.02f * depth * depth;
                    if (score < bestScore) { bestScore = score; best = i; }
                }
                return best < 0 ? Vector2.zero : uv[best];
            }

            // Eye openings: lid-margin vertices hugging the front of each eyeball.
            List<Vector2> Lid(Vector3 eye)
            {
                var list = new List<Vector2>();
                foreach (int i in skin)
                {
                    Vector3 d = v[i] - eye;
                    if (d.magnitude < eyeR + 0.004f && Vector3.Dot(d.normalized, fwd) > 0.55f) list.Add(uv[i]);
                }
                return list;
            }

            // Iris centre: the eyeball vertex furthest forward; UV radius per degree from the eyeball's front rings.
            Vector2 irisUV = Vector2.zero;
            float uvPerDeg = 0f;
            var front = eyeballs.Where(i => Vector3.Distance(v[i], rEye) < 0.03f).OrderByDescending(i => Vector3.Dot(v[i] - rEye, fwd)).ToList();
            if (front.Count > 0)
            {
                irisUV = uv[front[0]];
                Vector3 axis = (v[front[0]] - rEye).normalized;
                var samples = front.Select(i => (deg: Vector3.Angle(v[i] - rEye, axis), d: Vector2.Distance(uv[i], irisUV))).Where(s => s.deg > 5f && s.deg < 45f).ToList();
                if (samples.Count > 0) uvPerDeg = samples.Sum(s => s.d * s.deg) / samples.Sum(s => s.deg * s.deg);
            }

            // Separate UV islands of the head (ponytails, buns, scarf pieces): triangles connected through shared
            // vertices, except the largest (the head itself) and the eye and mouth-interior islands (lower left).
            var headTris = m.GetTriangles(headSub);
            var parent = new Dictionary<int, int>();
            int Find(int a) { while (parent.TryGetValue(a, out int p) && p != a) a = parent[a] = parent.TryGetValue(p, out int pp) ? pp : p; return a; }
            foreach (int i in headTris) if (!parent.ContainsKey(i)) parent[i] = i;
            for (int t = 0; t < headTris.Length; t += 3)
            {
                int a = Find(headTris[t]);
                parent[Find(headTris[t + 1])] = a;
                parent[Find(headTris[t + 2])] = a;
            }
            var islands = new Dictionary<int, List<int>>();
            for (int t = 0; t < headTris.Length; t += 3)
            {
                int root = Find(headTris[t]);
                if (!islands.TryGetValue(root, out var list)) islands[root] = list = new List<int>();
                list.Add(t);
            }
            int largest = islands.OrderByDescending(kv => kv.Value.Count).First().Key;
            var loose = islands.Where(kv => kv.Key != largest && kv.Value.Any(t => Enumerable.Range(0, 3).Any(k => { var q = uv[headTris[t + k]]; return q.x > 0.37f || q.y > 0.42f; })))
                               .SelectMany(kv => kv.Value).ToList();
            string P(Vector2 p) => $"[{p.x:F4}, {p.y:F4}]";
            string Ps(IEnumerable<Vector2> ps) => "[" + string.Join(", ", ps.Select(P)) + "]";
            string tex = Path.GetFileName(AssetDatabase.GetAssetPath(r.sharedMaterials[headSub].mainTexture));
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"avatar\": \"{avatar}\",");
            json.AppendLine($"  \"prefix\": \"{tex.Split('_')[0]}\",");
            json.AppendLine($"  \"iris_uv\": {P(irisUV)},");
            json.AppendLine($"  \"iris_uv_per_deg\": {uvPerDeg:F6},");
            foreach (var (side, s) in new[] { ("L", "L"), ("R", "R") })
            {
                json.AppendLine($"  \"brow_outer_{side}\": {P(Surface(Bone($"{s}OuterEyebrow")))},");
                json.AppendLine($"  \"brow_inner_{side}\": {P(Surface(Bone($"{s}InnerEyebrow")))},");
                json.AppendLine($"  \"eye_{side}\": {P(Surface(side == "L" ? lEye : rEye))},");
                json.AppendLine($"  \"lid_{side}\": {Ps(Lid(side == "L" ? lEye : rEye))},");
                json.AppendLine($"  \"mouth_corner_{side}\": {P(Surface(Bone($"{s}MouthCorner")))},");
                json.AppendLine($"  \"cheek_{side}\": {P(Surface(Bone($"{s}Cheek")))},");
            }
            json.AppendLine($"  \"upper_lip\": {P(Surface(Bone("MUpperLip")))},");
            json.AppendLine($"  \"lower_lip\": {P(Surface(Bone("MBottomLip")))},");
            json.AppendLine($"  \"nose\": {P(Surface(Bone("MNose")))},");
            json.AppendLine("  \"loose_islands\": [" + string.Join(", ", loose.Select(t => $"[{uv[headTris[t]].x:F4}, {uv[headTris[t]].y:F4}, {uv[headTris[t + 1]].x:F4}, {uv[headTris[t + 1]].y:F4}, {uv[headTris[t + 2]].x:F4}, {uv[headTris[t + 2]].y:F4}]")) + "]");
            json.AppendLine("}");
            File.WriteAllText($"{OutDir}/{avatar}.json", json.ToString());
            return $"{avatar}: iris {P(irisUV)} {uvPerDeg * 2048f:F2} px/deg, lids {Lid(lEye).Count}/{Lid(rEye).Count}, {loose.Count} loose island triangles";
        }
    }
}
