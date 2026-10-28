using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Numu.World;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Builds the realistic NUMU Park World (Assets/Scenes/Worlds/NUMU_Park.unity): sculpted, painted terrain,
    /// PBR materials, CC0 props, procedural trees and playground, Rocketbox characters, lighting, UI and the
    /// emotion-recognition scenario. Re-running regenerates the scene from these definitions.
    /// </summary>
    public static partial class NumuParkBuilder
    {
        public const string ScenePath = "Assets/Scenes/Worlds/NUMU_Park.unity";
        public const string WorldDir = "Assets/NUMU/Worlds/Park";
        const string PH = "Assets/ThirdParty/PolyHaven/Models";
        const string ACG = "Assets/ThirdParty/ambientCG";

        static StringBuilder _log;
        static Transform _root;
        static Terrain _terrain;
        static readonly Dictionary<string, Mesh> _meshAssets = new Dictionary<string, Mesh>();

        // ------------------------------------------------------------------ layout (metres, park space)

        static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -27f);
        static readonly Vector2 PlazaCenter = new Vector2(0f, -15f);
        const float PlazaRadius = 5.5f;
        static readonly Vector2 LoopCenter = new Vector2(0f, 5f);
        static readonly Vector2 LoopRadii = new Vector2(21f, 15.5f);
        static readonly Vector2 PadCenter = new Vector2(10f, 7f);
        static readonly Vector2 PadSize = new Vector2(12.5f, 9f);
        static readonly Vector3 SwingPos = new Vector3(8f, 0f, 8.6f);
        static readonly Vector3 SlidePos = new Vector3(13.6f, 0f, 8.2f);

        static readonly Vector2[] EntrancePath = { new Vector2(0, -60), new Vector2(0.6f, -44), new Vector2(-0.4f, -30), new Vector2(0, -20) };
        static readonly Vector2[] PlaygroundSpur = { new Vector2(5.5f, -9.2f), new Vector2(7.2f, -3f), new Vector2(8.5f, 2.6f) };
        static readonly Vector2[] PicnicSpur = { new Vector2(-17f, -4f), new Vector2(-14f, 2f), new Vector2(-12.5f, 6.5f) };

        static readonly Vector3[] TreeSpots =
        {
            new Vector3(-6, 0, 17), new Vector3(-19, 0, -7), new Vector3(23, 0, -9), new Vector3(5, 0, 26), new Vector3(-27, 0, 17),
            new Vector3(27, 0, 21), new Vector3(-31, 0, -21), new Vector3(31, 0, -23), new Vector3(-11, 0, 31), new Vector3(15, 0, 33),
            new Vector3(-37, 0, 3), new Vector3(37, 0, 5), new Vector3(-20, 0, -33), new Vector3(20, 0, -34), new Vector3(-9, 0, -31),
            new Vector3(10, 0, -29), new Vector3(-24, 0, 32), new Vector3(30, 0, 36), new Vector3(-40, 0, -12), new Vector3(41, 0, -8),
            new Vector3(-2, 0, 40), new Vector3(20, 0, 44), new Vector3(-30, 0, 42), new Vector3(-46, 0, 20), new Vector3(46, 0, 24),
            new Vector3(-12, 0, 2), new Vector3(16, 0, 17),
        };

        // ------------------------------------------------------------------ entry

        [MenuItem("NUMU/Worlds/Build Park World")]
        public static void BuildFromMenu() => Debug.Log(Build());

        public static string Build(bool bake = true)
        {
            _log = new StringBuilder();
            try
            {
                return BuildInternal(bake);
            }
            catch (System.Exception e)
            {
                return $"PARK BUILD FAILED: {e}\n--- log ---\n{_log}";
            }
        }

        static string BuildInternal(bool bake)
        {
            // Checked before anything is deleted: a build attempted in Play Mode must not touch any asset.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Aborted: exit Play Mode before building the Park.";
            var active = SceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath)
                return $"Aborted: '{active.path}' has unsaved changes.";
            Directory.CreateDirectory(WorldDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            _meshAssets.Clear();
            DeleteGeneratedMeshes();

            NumuWorldIcons.Generate();
            NumuCharacterSetup.SetupAll(_log);
            var mats = new ParkMaterials();

            var scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);
            _root = new GameObject("NUMU_Park").transform;

            BuildTerrain(mats);
            Physics.SyncTransforms();
            BuildPlayground(mats);
            BuildTrees(mats);
            BuildProps();
            BuildGrass(mats);
            BuildLightingAndAtmosphere();
            var gameplay = BuildCharactersAndGameplay();
            BuildUI(gameplay);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            if (bake) BakeLighting(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            _log.Insert(0, $"Built {ScenePath}\n");
            return _log.ToString();
        }

        static void AddToBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == ScenePath)) list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ helpers

        static string MeshPath => $"{WorldDir}/ParkMeshes.asset";

        static void DeleteGeneratedMeshes() => AssetDatabase.DeleteAsset(MeshPath);

        public static Mesh StoreMesh(Mesh m)
        {
            if (!File.Exists(MeshPath)) AssetDatabase.CreateAsset(m, MeshPath);
            else AssetDatabase.AddObjectToAsset(m, MeshPath);
            return m;
        }

        /// <param name="lightmap">Broad surfaces get baked lightmaps; thin tubes, cards and small parts light from probes.</param>
        public static GameObject MeshGO(string name, Transform parent, MeshBuilder mb, Material mat, bool isStatic = true, ShadowCastingMode shadows = ShadowCastingMode.On, bool lightmap = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = mb.ToMesh(name);
            mesh.RecalculateTangents();
            bool baked = isStatic && lightmap;
            // Lightmapped geometry needs its own non-overlapping UV2 (UV0 tiles textures in metres).
            if (baked)
            {
                UnwrapParam.SetDefaults(out var up);
                up.packMargin = 0.004f;
                Unwrapping.GenerateSecondaryUVSet(mesh, up);
            }
            StoreMesh(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows;
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            if (isStatic && !lightmap) r.receiveGI = ReceiveGI.LightProbes;
            return go;
        }

        static Transform Group(string name, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent != null ? parent : _root, false);
            return t;
        }

        public static float GroundY(float x, float z) => _terrain != null ? _terrain.SampleHeight(new Vector3(x, 0, z)) + _terrain.transform.position.y : 0f;

        public static Vector3 OnGround(Vector3 p) => new Vector3(p.x, GroundY(p.x, p.z), p.z);

        static float DistToPolyline(Vector2 p, IList<Vector2> line)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector2 a = line[i], b = line[i + 1];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        static float DistToLoop(Vector2 p)
        {
            // Distance to an ellipse, approximated by sampling: plenty accurate for painting.
            float best = float.MaxValue;
            for (int i = 0; i < 96; i++)
            {
                float a = i / 96f * Mathf.PI * 2f;
                Vector2 q = LoopCenter + new Vector2(Mathf.Cos(a) * LoopRadii.x, Mathf.Sin(a) * LoopRadii.y);
                best = Mathf.Min(best, Vector2.Distance(p, q));
            }
            return best;
        }

        /// <summary>Distance from a point to the nearest walkway (paths, loop, plaza).</summary>
        static float PathDistance(Vector2 p)
        {
            float d = Mathf.Min(DistToPolyline(p, EntrancePath), DistToLoop(p));
            d = Mathf.Min(d, DistToPolyline(p, PlaygroundSpur));
            d = Mathf.Min(d, DistToPolyline(p, PicnicSpur));
            d = Mathf.Min(d, Mathf.Max(0f, Vector2.Distance(p, PlazaCenter) - PlazaRadius));
            return d;
        }

        static bool InPad(Vector2 p, float margin = 0f) =>
            Mathf.Abs(p.x - PadCenter.x) < PadSize.x * 0.5f + margin && Mathf.Abs(p.y - PadCenter.y) < PadSize.y * 0.5f + margin;

        /// <summary>Is a lawn spot free of paths, playground and the given clearance?</summary>
        static bool LawnFree(Vector2 p, float clearance) => PathDistance(p) > 1.6f + clearance && !InPad(p, 1.5f + clearance) && Vector2.Distance(p, PlazaCenter) > PlazaRadius + 1.5f + clearance;
    }
}
