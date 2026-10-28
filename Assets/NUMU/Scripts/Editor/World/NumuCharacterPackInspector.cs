using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Numu.World;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Validates an imported character pack against NUMU's needs instead of trusting store descriptions:
    /// humanoid rig, real-world height (adult or child), cost (triangles, bones, materials, textures),
    /// URP compatibility of the materials, and whether the face has blendshapes able to show the six emotions.
    /// Writes a Markdown report.
    /// </summary>
    public static class NumuCharacterPackInspector
    {
        public const string PackUrl = "https://assetstore.unity.com/packages/3d/characters/realistic-modern-character-pack-329789";
        const float ChildHeight = 1.55f;

        [MenuItem("NUMU/Characters/Inspect Realistic Modern Character Pack")]
        public static void InspectFromMenu() => Debug.Log(Inspect(FindPackFolder(), "Docs/CHARACTER_PACK_REPORT.md"));

        /// <summary>Folder of the imported Realistic Modern Character Pack, or null when it isn't in the project.</summary>
        public static string FindPackFolder()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string lower = path.ToLowerInvariant();
                if (lower.Contains("realistic") && lower.Contains("character") && !lower.StartsWith("assets/numu/"))
                {
                    // The pack's root is the first folder under Assets/ on this path.
                    var parts = path.Split('/');
                    return parts.Length > 2 ? $"{parts[0]}/{parts[1]}" : Path.GetDirectoryName(path);
                }
            }
            return null;
        }

        public static string Inspect(string folder, string reportPath = null)
        {
            var sb = new StringBuilder();
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                sb.AppendLine("Realistic Modern Character Pack is not imported in this project.");
                sb.AppendLine($"Get it from {PackUrl}, then Window → Package Manager → My Assets → Download → Import, and run this check again.");
                return sb.ToString();
            }
            sb.AppendLine($"# Character pack report: `{folder}`\n");
            sb.AppendLine("| Model | Humanoid | Height | Adult/child | Triangles | Bones | Blendshapes | Emotions (of 6) | Materials | Non-URP shaders | Max texture |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            int models = 0, humanoid = 0, children = 0, expressive = 0, nonUrp = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null || model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0) continue; // skip props and animation-only files
                models++;
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                bool human = avatar != null && avatar.isHuman && avatar.isValid;
                if (human) humanoid++;

                var inst = Object.Instantiate(model);
                inst.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var b = smrs[0].bounds;
                    foreach (var r in smrs) b.Encapsulate(r.bounds);
                    float height = b.size.y;
                    bool child = height < ChildHeight;
                    if (child) children++;
                    int tris = smrs.Sum(r => r.sharedMesh != null ? (int)(r.sharedMesh.triangles.Length / 3) : 0);
                    int bones = smrs.SelectMany(r => r.bones).Distinct().Count();
                    var face = smrs.OrderByDescending(r => r.sharedMesh != null ? r.sharedMesh.blendShapeCount : 0).First();
                    int shapes = face.sharedMesh != null ? face.sharedMesh.blendShapeCount : 0;
                    int emotions = 1;
                    if (shapes > 0)
                    {
                        var fe = inst.AddComponent<NumuFacialExpressions>();
                        fe.face = face;
                        NumuCharacterSetup.ConfigureFace(fe);
                        emotions = fe.SupportedEmotionCount();
                    }
                    if (emotions >= 6) expressive++;
                    var mats = smrs.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToList();
                    var badShaders = mats.Select(m => m.shader != null ? m.shader.name : "missing").Where(n => !n.StartsWith("Universal Render Pipeline") && !n.StartsWith("Shader Graphs")).Distinct().ToList();
                    if (badShaders.Count > 0) nonUrp++;
                    int maxTex = 0;
                    foreach (var m in mats)
                        foreach (var prop in m.GetTexturePropertyNames())
                            if (m.GetTexture(prop) is Texture t) maxTex = Mathf.Max(maxTex, Mathf.Max(t.width, t.height));
                    sb.AppendLine($"| {Path.GetFileNameWithoutExtension(path)} | {(human ? "yes" : avatar == null ? "no avatar" : "invalid")} | {height:F2} m | {(child ? "child" : "adult")} | {tris:N0} | {bones} | {shapes} | {emotions} | {mats.Count} | {(badShaders.Count == 0 ? "—" : string.Join(", ", badShaders))} | {maxTex} |");
                }
                finally
                {
                    Object.DestroyImmediate(inst);
                }
            }
            sb.AppendLine();
            sb.AppendLine($"**Summary:** {models} character models, {humanoid} with a valid humanoid rig, {children} child-sized (< {ChildHeight} m), " +
                          $"{expressive} able to show all six emotions with blendshapes, {nonUrp} using non-URP shaders (need URP Lit copies, originals left untouched).");
            if (expressive == 0)
                sb.AppendLine("\nNo model has facial blendshapes for the emotion activities: keep the expressive Rocketbox faces for characters the child must read, or add a facial rig (e.g. ARKit-compatible blendshapes).");
            if (children == 0)
                sb.AppendLine("\nNo child-sized models: shrinking adults would give wrong proportions, so children stay on the Rocketbox child avatars.");
            if (!string.IsNullOrEmpty(reportPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, sb.ToString());
            }
            return sb.ToString();
        }
    }
}
