using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Numu.EditorTools
{
    /// <summary>
    /// Builds the NUMU world scene procedurally from the reference composition.
    /// Everything is regenerated on each run: islands, props, characters, UI and interaction wiring.
    /// Positions are authored in reference-image pixels and projected through the real camera,
    /// so the default view lines up with Assets/NUMU_World_Reference.png.
    /// </summary>
    public static partial class NumuWorldBuilder
    {
        public const string Root = "Assets/NUMU";
        public const string GeneratedDir = Root + "/Generated";
        public const string MaterialsDir = Root + "/Materials";
        public const string PrefabsDir = Root + "/Prefabs";
        public const string TexturesDir = Root + "/Textures";
        public const string ScenePath = "Assets/Scenes/NUMU_World.unity";
        public const float RefW = 1584f, RefH = 993f;

        static Camera _cam;
        static NumuCameraRig _rig;
        static Transform _world;
        static StringBuilder _log;
        static readonly Dictionary<string, Mesh> _meshGroups = new Dictionary<string, Mesh>();

        [MenuItem("NUMU/Build World")]
        public static void BuildFromMenu() => Debug.Log(Build());

        public static string Build()
        {
            try
            {
                return BuildInternal();
            }
            catch (System.Exception e)
            {
                return $"BUILD FAILED: {e}\n--- log so far ---\n{_log}";
            }
        }

        static string BuildInternal()
        {
            _log = new StringBuilder();
            _meshGroups.Clear();
            EnsureFolders();
            var active = SceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath)
            {
                return $"Aborted: '{active.path}' has unsaved changes. Save it first so it isn't lost when the NUMU scene opens.";
            }

            var scene = OpenOrCreateScene();
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { GeneratedDir }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

            Materials.Ensure();
            ConfigurePipeline();
            SetupLighting();
            SetupCamera();
            _world = new GameObject("NUMU_World").transform;

            BuildEnvironment();
            BuildIslands();
            BuildContent();
            if (DetailPass)
            {
                RenderPreviews();
                BuildUI();
                WireClickables();
            }
            _cam.ResetAspect();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            _log.Insert(0, $"Built {ScenePath}\n");
            return _log.ToString();
        }

        // ------------------------------------------------------------------ scene plumbing

        static void EnsureFolders()
        {
            foreach (var dir in new[] { Root, GeneratedDir, MaterialsDir, PrefabsDir, TexturesDir })
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
                    AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
                }
            }
        }

        static Scene OpenOrCreateScene()
        {
            if (File.Exists(ScenePath)) return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(s, ScenePath);
            return s;
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Generated meshes are persisted straight away as sub-assets (one file per group) so that
        /// scenes and prefabs can reference them.
        /// </summary>
        public static Mesh Store(Mesh mesh, string group)
        {
            if (_meshGroups.TryGetValue(group, out var main) && main != null)
            {
                AssetDatabase.AddObjectToAsset(mesh, main);
            }
            else
            {
                string path = $"{GeneratedDir}/{group}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                _meshGroups[group] = mesh;
            }
            return mesh;
        }

        public static GameObject MeshObject(string name, Transform parent, MeshBuilder mb, string group, Material mat,
            bool castShadows = true, bool collider = false, Vector3? worldPos = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (worldPos.HasValue) go.transform.position = worldPos.Value;
            var mesh = Store(mb.ToMesh(name), group);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        public static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ------------------------------------------------------------------ reference projection

        /// <summary>World point at height <paramref name="y"/> seen under reference pixel (px, py).</summary>
        public static Vector3 P(float px, float py, float y)
        {
            var ray = _cam.ViewportPointToRay(new Vector3(px / RefW, 1f - py / RefH, 0f));
            float t = (y - ray.origin.y) / ray.direction.y;
            return ray.origin + ray.direction * t;
        }

        /// <summary>World units per reference pixel at (px, py) on the plane at height y.</summary>
        public static float U(float px, float py, float y) => Vector3.Distance(P(px, py, y), P(px + 10f, py, y)) / 10f;

        /// <summary>World point on a vertical plane through <paramref name="anchor"/> facing the camera.</summary>
        public static Vector3 PVertical(float px, float py, Vector3 anchor)
        {
            var ray = _cam.ViewportPointToRay(new Vector3(px / RefW, 1f - py / RefH, 0f));
            var plane = new Plane(Vector3.back, anchor);
            plane.Raycast(ray, out float t);
            return ray.GetPoint(t);
        }

        /// <summary>Height in world units of something spanning pyTop..pyBottom in the reference, standing at its base.</summary>
        public static float HeightFromPixels(float px, float pyBottom, float pyTop, float baseY)
        {
            Vector3 b = P(px, pyBottom, baseY);
            Vector3 top = PVertical(px, pyTop, b);
            return top.y - b.y;
        }

        // ------------------------------------------------------------------ pipeline, lighting, camera

        static void ConfigurePipeline()
        {
            foreach (var path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null) continue;
                bool pc = path.Contains("PC_");
                asset.shadowDistance = 320f;
                asset.shadowCascadeCount = pc ? 4 : 2;
                if (pc) asset.cascade4Split = new Vector3(0.52f, 0.66f, 0.8f);
                else asset.cascade2Split = 0.7f;
                asset.mainLightShadowmapResolution = pc ? 4096 : 2048;
                asset.msaaSampleCount = 4;
                asset.supportsCameraDepthTexture = true;
                EditorUtility.SetDirty(asset);
            }
            _log.AppendLine("URP assets: shadow distance 320, MSAA 4x, depth texture on.");
        }

        static void SetupLighting()
        {
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = NumuColor.Hex("#fff0dc");
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 1f;
            light.shadowBias = 0.6f;
            light.shadowNormalBias = 0.6f;
            sun.transform.rotation = Quaternion.Euler(48f, 58f, 0f);
            var lightData = sun.AddComponent<UniversalAdditionalLightData>();
            lightData.softShadowQuality = SoftShadowQuality.High;

            var rigGo = new GameObject("Lighting Rig");
            var rig = rigGo.AddComponent<NumuLightingRig>();
            rig.skyAmbient = NumuColor.Hex("#fffaff");
            rig.groundAmbient = NumuColor.Hex("#e6d6d2");
            rig.hazeColor = NumuColor.Hex("#f6e3df");

            RenderSettings.skybox = Materials.Sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = NumuColor.Hex("#f2ecf6");
            RenderSettings.ambientEquatorColor = NumuColor.Hex("#efe3dc");
            RenderSettings.ambientGroundColor = NumuColor.Hex("#c9b9b4");
            RenderSettings.fog = false;
            RenderSettings.sun = light;

            var volumeGo = new GameObject("Post Processing");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = Materials.VolumeProfile();
        }

        static void SetupCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.Skybox;
            _cam.nearClipPlane = 5f;
            _cam.farClipPlane = 900f;
            _cam.fieldOfView = 15f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.renderShadows = true;
            camGo.AddComponent<AudioListener>();
            _rig = camGo.AddComponent<NumuCameraRig>();
            _rig.overview = new NumuCameraRig.Pose { pivot = new Vector3(0f, 0f, 0f), pitch = 38f, yaw = 0f, distance = 220f, fov = 15f };
            // Lay out against the reference aspect; the rig re-fits at runtime.
            _cam.aspect = RefW / RefH;
            _rig.Apply(_rig.overview);
        }
    }
}
