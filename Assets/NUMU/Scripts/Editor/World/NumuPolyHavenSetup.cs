using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Turns imported Poly Haven FBX models into URP-ready prefabs: builds a PBR material per texture set
    /// (packing roughness/metalness EXRs into a URP mask), remaps the FBX materials, and saves a prefab.
    /// </summary>
    public static class NumuPolyHavenSetup
    {
        const string Root = "Assets/ThirdParty/PolyHaven/Models";
        public const string PrefabDir = "Assets/NUMU/Worlds/Prefabs/Props";

        class TexSet
        {
            public string diff, normal, rough, metal, opacity;
        }

        public static GameObject Prefab(string id) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/PH_{id}.prefab");

        /// <summary>
        /// Some Poly Haven files are packs of variants laid side by side. Each entry becomes its own centred
        /// prefab (PH_{id}_{variant}) made of the listed child objects.
        /// </summary>
        static readonly Dictionary<string, (string variant, string[] children)[]> Splits = new Dictionary<string, (string, string[])[]>
        {
            { "shrub_03", new[] { ("a", new[] { "shrub_03_a_LOD0" }), ("b", new[] { "shrub_03_b_LOD0" }) } },
            // Each flower variant ships LOD0-LOD3 side by side; they become one LODGroup.
            { "flower_gazania", new[]
            {
                ("d", new[] { "flower_gazania_d_LOD0", "flower_gazania_d_LOD1", "flower_gazania_d_LOD2", "flower_gazania_d_LOD3" }),
                ("e", new[] { "flower_gazania_e_LOD0", "flower_gazania_e_LOD1", "flower_gazania_e_LOD2", "flower_gazania_e_LOD3" }),
                ("g", new[] { "flower_gazania_g_LOD0", "flower_gazania_g_LOD1", "flower_gazania_g_LOD2", "flower_gazania_g_LOD3" }),
            } },
            { "fern_02", new[] { ("a", new[] { "fern_02_a" }), ("b", new[] { "fern_02_b" }), ("c", new[] { "fern_02_c" }), ("d", new[] { "fern_02_d" }) } },
            { "football", new[] { ("ball", new[] { "football_inflated" }) } },
            { "metal_trash_can", new[] { ("bin", new[] { "metal_trash_can", "metal_trash_can_handle_left", "metal_trash_can_handle_right" }) } },
        };

        static readonly string[] Foliage = { "shrub", "fern", "flower", "grass", "plant" };

        public static void SetupAll(StringBuilder log)
        {
            Directory.CreateDirectory(PrefabDir);
            int count = 0;
            foreach (var dir in Directory.GetDirectories(Root))
            {
                string id = Path.GetFileName(dir);
                string fbx = $"{Root}/{id}/{id}.fbx";
                if (!File.Exists(fbx)) continue;
                // Prefabs are cheap to regenerate; always rebuild so fixes to the setup apply.
                SetupModel(id, fbx);
                count++;
            }
            log?.AppendLine($"Poly Haven: {count} models set up as URP prefabs.");
        }

        static Dictionary<string, TexSet> FindSets(string id)
        {
            var sets = new Dictionary<string, TexSet>();
            string texDir = $"{Root}/{id}/textures";
            if (!Directory.Exists(texDir)) return sets;
            foreach (var f in Directory.GetFiles(texDir))
            {
                if (f.EndsWith(".meta")) continue;
                string file = Path.GetFileNameWithoutExtension(f);
                string path = f.Replace('\\', '/');
                foreach (var (key, assign) in new (string, System.Action<TexSet, string>)[]
                {
                    ("_diff_", (s, p) => s.diff = p), ("_nor_gl_", (s, p) => s.normal = p), ("_rough_", (s, p) => s.rough = p),
                    ("_metal_", (s, p) => s.metal = p), ("_opacity_", (s, p) => s.opacity = p),
                })
                {
                    int i = file.IndexOf(key);
                    if (i < 0) continue;
                    string prefix = file.Substring(0, i);
                    if (!sets.TryGetValue(prefix, out var set)) sets[prefix] = set = new TexSet();
                    assign(set, path);
                }
            }
            return sets;
        }

        static Texture2D Readable(string path, bool linear)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) return null;
            if (!ti.isReadable || ti.sRGBTexture == linear)
            {
                ti.isReadable = true;
                ti.sRGBTexture = !linear;
                ti.textureType = TextureImporterType.Default;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>URP mask: R metallic, G occlusion (1), B unused, A smoothness = 1 - roughness.</summary>
        static Texture2D PackMask(string prefix, TexSet s, string outDir)
        {
            if (s.rough == null) return null;
            string outPath = $"{outDir}/{prefix}_mask.png";
            if (File.Exists(outPath)) return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            var rough = Readable(s.rough, true);
            var metal = s.metal != null ? Readable(s.metal, true) : null;
            if (rough == null) return null;
            int w = rough.width, h = rough.height;
            var rp = rough.GetPixels();
            Color[] mp = metal != null && metal.width == w && metal.height == h ? metal.GetPixels() : null;
            var outPx = new Color[rp.Length];
            for (int i = 0; i < rp.Length; i++)
                outPx[i] = new Color(mp != null ? mp[i].r : 0f, 1f, 0.5f, 1f - Mathf.Clamp01(rp[i].r));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true);
            tex.SetPixels(outPx);
            tex.Apply();
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(outPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        static int CommonPrefix(string a, string b)
        {
            int n = 0;
            while (n < a.Length && n < b.Length && a[n] == b[n]) n++;
            return n;
        }

        /// <summary>
        /// Saves a prefab whose root is a clean transform (identity rotation, unit scale, pivot at the base centre).
        /// FBX roots often carry unit-conversion scale/rotation, which placement code must never overwrite.
        /// </summary>
        static void SaveWrapped(string name, GameObject model, string[] keepChildren, bool foliage)
        {
            var root = new GameObject(name);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.SetParent(root.transform, true);
            // FBX files with _LODn meshes come with Unity's auto-generated LODGroup; ours replaces it.
            foreach (var existing in inst.GetComponentsInChildren<LODGroup>()) Object.DestroyImmediate(existing);
            if (keepChildren != null)
                for (int i = inst.transform.childCount - 1; i >= 0; i--)
                {
                    var c = inst.transform.GetChild(i);
                    if (System.Array.IndexOf(keepChildren, c.name) < 0) Object.DestroyImmediate(c.gameObject);
                }
            // Stack _LOD1.._LOD3 onto _LOD0 and wire them into a LODGroup.
            var lods = new List<Renderer>();
            if (keepChildren != null && keepChildren.Length > 1 && keepChildren.All(k => k.Contains("_LOD")))
            {
                var lod0 = inst.transform.Find(keepChildren[0]);
                Vector3 c0 = lod0.GetComponent<Renderer>().bounds.center;
                foreach (var k in keepChildren)
                {
                    var t = inst.transform.Find(k);
                    if (t == null) continue;
                    var r = t.GetComponent<Renderer>();
                    t.position += new Vector3(c0.x - r.bounds.center.x, 0f, c0.z - r.bounds.center.z);
                    lods.Add(r);
                }
            }
            var b = new Bounds();
            bool first = true;
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
            {
                if (lods.Count > 0 && r != lods[0] && lods.Contains(r)) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
            {
                // Small foliage doesn't need to cast shadows (big win on mobile); props and furniture do.
                r.shadowCastingMode = foliage ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                // Foliage and small parts (handles, bulbs) light from probes rather than tiny lightmap charts.
                if (foliage || r.bounds.size.magnitude < 0.45f) r.receiveGI = ReceiveGI.LightProbes;
            }
            inst.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
            var group = root.AddComponent<LODGroup>();
            if (lods.Count > 1)
            {
                float[] h = { 0.25f, 0.1f, 0.04f, 0.015f };
                group.SetLODs(lods.Select((r, i) => new LOD(h[Mathf.Min(i, h.Length - 1)], new[] { r })).ToArray());
            }
            else
            {
                // Single-mesh props: cull once they are tiny on screen.
                group.SetLODs(new[] { new LOD(foliage ? 0.02f : 0.012f, inst.GetComponentsInChildren<Renderer>()) });
            }
            group.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        static void SetupModel(string id, string fbx)
        {
            var sets = FindSets(id);
            string texDir = $"{Root}/{id}/textures";
            bool foliage = Foliage.Any(f => id.Contains(f));
            var materials = new Dictionary<string, Material>();
            foreach (var kv in sets)
            {
                var s = kv.Value;
                // Foliage colour maps were merged with their alpha during processing (saved as PNG).
                string diff = s.diff;
                string png = diff != null ? Path.ChangeExtension(diff, ".png") : null;
                if (png != null && File.Exists(png)) diff = png;
                var spec = new NumuWorldMaterials.Spec
                {
                    baseMap = diff != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(diff) : null,
                    normal = s.normal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(s.normal) : null,
                    maskMap = PackMask(kv.Key, s, texDir),
                    smoothness = foliage ? 0.6f : 0.85f,
                    alphaClip = foliage || s.opacity != null,
                    cutoff = 0.4f,
                    doubleSided = foliage,
                };
                materials[kv.Key] = NumuWorldMaterials.Lit($"M_PH_{kv.Key}", spec);
            }

            // Remap every source material onto the matching texture set (exact name, longest prefix, or the asset's own set).
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            var sourceMats = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().Select(m => m.name).Distinct().ToList();
            foreach (var name in sourceMats)
            {
                // Best match = longest shared prefix (e.g. "..._table_table" → "..._table_top", not "..._table_bottom").
                string key = materials.Keys.OrderByDescending(k => CommonPrefix(k, name)).ThenBy(k => k.Length).FirstOrDefault();
                if (key != null) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), materials[key]);
            }
            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            // Packs of variants/LODs are only ever placed per variant; single models get one prefab.
            if (Splits.TryGetValue(id, out var variants))
            {
                AssetDatabase.DeleteAsset($"{PrefabDir}/PH_{id}.prefab");
                foreach (var (variant, keep) in variants) SaveWrapped($"PH_{id}_{variant}", model, keep, foliage);
            }
            else SaveWrapped($"PH_{id}", model, null, foliage);
        }
    }
}
