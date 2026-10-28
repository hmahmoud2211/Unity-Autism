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
    /// Shared scene-building context for the indoor realistic worlds (Home, School, Supermarket): mesh storage,
    /// prop placement, characters, the third-person player rig, probes, post-processing and lightmap baking.
    /// The Park builder predates this class and keeps its own equivalents.
    /// </summary>
    public class NumuWorldScaffold
    {
        /// <summary>Layer for furniture and character blockers: the player collides with them, the follow camera ignores them.</summary>
        public const int CameraIgnoreLayer = 2;

        public readonly string Name, ScenePath, WorldDir;
        public readonly StringBuilder Log = new StringBuilder();
        public Transform Root;
        public Scene Scene;

        string MeshPath => $"{WorldDir}/{Name}Meshes.asset";

        public NumuWorldScaffold(string name)
        {
            Name = name;
            ScenePath = $"Assets/Scenes/Worlds/NUMU_{name}.unity";
            WorldDir = $"Assets/NUMU/Worlds/{name}";
        }

        // ------------------------------------------------------------------ scene

        /// <summary>Opens (or creates) the world scene, empties it and prepares a fresh root. Returns an error message or null.</summary>
        public string Begin()
        {
            var active = SceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath) return $"Aborted: '{active.path}' has unsaved changes.";
            Directory.CreateDirectory(WorldDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.DeleteAsset(MeshPath);
            Scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in Scene.GetRootGameObjects()) Object.DestroyImmediate(go);
            Root = new GameObject($"NUMU_{Name}").transform;
            return null;
        }

        public void SaveAndRegister()
        {
            EditorSceneManager.SaveScene(Scene, ScenePath);
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == ScenePath)) list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ geometry

        public Mesh Store(Mesh m)
        {
            if (!File.Exists(MeshPath)) AssetDatabase.CreateAsset(m, MeshPath);
            else AssetDatabase.AddObjectToAsset(m, MeshPath);
            return m;
        }

        public Transform Group(string name, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent != null ? parent : Root, false);
            return t;
        }

        /// <param name="lightmap">Broad static surfaces get lightmaps; small or thin parts light from probes.</param>
        /// <param name="collider">Adds a MeshCollider. Walls stay on the default layer so the camera avoids them.</param>
        public GameObject MeshGO(string name, Transform parent, MeshBuilder mb, Material mat, bool isStatic = true, ShadowCastingMode shadows = ShadowCastingMode.On,
            bool lightmap = true, float lightmapScale = 1f, bool collider = false, int layer = 0)
        {
            if (mb.VertexCount == 0) return null;
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            var mesh = mb.ToMesh(name);
            if (mesh.vertexCount > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.RecalculateTangents();
            bool baked = isStatic && lightmap;
            if (baked)
            {
                UnwrapParam.SetDefaults(out var up);
                up.packMargin = 0.006f;
                Unwrapping.GenerateSecondaryUVSet(mesh, up);
            }
            Store(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows;
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            if (isStatic && !lightmap) r.receiveGI = ReceiveGI.LightProbes;
            if (baked && !Mathf.Approximately(lightmapScale, 1f))
            {
                var so = new SerializedObject(r);
                so.FindProperty("m_ScaleInLightmap").floatValue = lightmapScale;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// <summary>Adds a box collider covering the given local bounds on the camera-ignore layer (furniture blocking).</summary>
        public static BoxCollider Solid(GameObject go, Vector3 center, Vector3 size)
        {
            var holder = new GameObject("Collider") { layer = CameraIgnoreLayer };
            holder.transform.SetParent(go.transform, false);
            var box = holder.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            return box;
        }

        /// <summary>Places a Poly Haven prefab (see <see cref="NumuPolyHavenSetup"/>). Optionally blocks the player with a box collider.</summary>
        public GameObject Place(string id, Transform parent, Vector3 pos, float yaw, float scale = 1f, bool collider = false, bool isStatic = true)
        {
            var prefab = NumuPolyHavenSetup.Prefab(id);
            if (prefab == null)
            {
                Log.AppendLine($"  missing prop prefab PH_{id}");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * scale;
            if (isStatic)
                foreach (var t in go.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            if (collider)
            {
                var b = LocalBounds(go);
                Solid(go, b.center, b.size);
            }
            return go;
        }

        /// <summary>Bounds of all meshes in the object's own (unrotated, unscaled) space.</summary>
        public static Bounds LocalBounds(GameObject go)
        {
            var toLocal = go.transform.worldToLocalMatrix;
            bool first = true;
            var b = new Bounds();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = toLocal * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        public static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Height of the top surface of a placed prop (for putting things on tables and shelves).</summary>
        public static float Top(GameObject go) => go == null ? 0f : Bounds(go).max.y;

        // ------------------------------------------------------------------ outdoors (the view through the windows)

        /// <summary>
        /// Lawn, trees and bushes around a building footprint, so every window shows a believable outside.
        /// Trees reuse the Park's bark and leaf materials; nearby trees cast shadows through the windows.
        /// </summary>
        public void Outdoors(Rect footprint, Material ground, int trees, int seed, float clearance = 3f, float spread = 26f)
        {
            var g = Group("Outdoors");
            var mb = new MeshBuilder();
            NumuInteriorKit.Slab(mb, new Rect(footprint.center.x - 110f, footprint.center.y - 110f, 220f, 220f), -0.02f, true);
            MeshGO("Ground", g, mb, ground, true, ShadowCastingMode.Off, true, 0.08f);
            var bark = NumuWorldMaterials.Load<Material>($"{NumuWorldMaterials.Dir}/M_Bark006.mat");
            var leaves = NumuWorldMaterials.Load<Material>($"{NumuWorldMaterials.Dir}/M_TreeLeaves.mat");
            var rng = new System.Random(seed);
            float half = Mathf.Max(footprint.width, footprint.height) * 0.5f;
            var placed = new List<Vector2>();
            for (int i = 0, tries = 0; i < trees && tries < trees * 20; tries++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = half + clearance + Mathf.Pow((float)rng.NextDouble(), 1.6f) * spread;
                var p = footprint.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                // Keep a clear band around the walls and space trees apart.
                var grown = new Rect(footprint.xMin - clearance, footprint.yMin - clearance, footprint.width + clearance * 2f, footprint.height + clearance * 2f);
                if (grown.Contains(p) || placed.Exists(q => (q - p).magnitude < 4.5f)) continue;
                placed.Add(p);
                var b = new MeshBuilder();
                var l = new MeshBuilder();
                // Background trees (only seen through windows) use the Park's lite canopy: fewer, larger leaf cards.
                var shape = Numu.World.RealisticKit.Broadleaf(6.5f + (float)rng.NextDouble() * 4f);
                shape.leafDensity *= 0.35f;
                shape.leafCardSize *= 1.45f;
                Numu.World.RealisticKit.Tree(b, l, shape, seed * 31 + i);
                var tree = Group($"Tree_{i}", g);
                tree.position = new Vector3(p.x, 0f, p.y);
                tree.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                MeshGO("Trunk", tree, b, bark, true, ShadowCastingMode.Off, false);
                MeshGO("Leaves", tree, l, leaves, true, ShadowCastingMode.Off, false);
                i++;
            }
            // A low hedge line just outside the walls.
            var twigs = new MeshBuilder();
            var hedge = new MeshBuilder();
            int k = 0;
            for (float x = footprint.xMin - 1.2f; x <= footprint.xMax + 1.2f; x += 1.1f)
                foreach (float z in new[] { footprint.yMin - 1.4f, footprint.yMax + 1.4f })
                    Numu.World.RealisticKit.Bush(twigs, hedge, new Vector3(x + (float)rng.NextDouble() * 0.3f, 0, z), 1.0f + (float)rng.NextDouble() * 0.3f, seed + k++);
            for (float z = footprint.yMin - 0.4f; z <= footprint.yMax + 0.4f; z += 1.1f)
                foreach (float x in new[] { footprint.xMin - 1.4f, footprint.xMax + 1.4f })
                    Numu.World.RealisticKit.Bush(twigs, hedge, new Vector3(x, 0, z + (float)rng.NextDouble() * 0.3f), 1.0f + (float)rng.NextDouble() * 0.3f, seed + k++);
            MeshGO("HedgeTwigs", g, twigs, bark, true, ShadowCastingMode.Off, false);
            MeshGO("Hedge", g, hedge, leaves, true, ShadowCastingMode.Off, false);
        }

        // ------------------------------------------------------------------ characters

        public GameObject SpawnCharacter(string avatar, string displayName, Transform parent, Vector3 pos, float yaw)
        {
            var prefab = NumuCharacterSetup.Prefab(avatar);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = displayName;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.GetComponent<Animator>().runtimeAnimatorController = NumuCharacterSetup.NpcController(avatar.StartsWith("Female"));
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.shadowCastingMode = ShadowCastingMode.On;
                smr.lightProbeUsage = LightProbeUsage.BlendProbes;
            }
            return go;
        }

        /// <summary>Moves a character so its pelvis (in the given seated clip) rests on a seat point.</summary>
        public static void SeatOn(GameObject character, Vector3 seat, string sitClip, float backOffset)
        {
            var clip = NumuCharacterSetup.Clip(sitClip);
            var anim = character.GetComponent<Animator>();
            clip.SampleAnimation(character, 1f);
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 want = seat + Vector3.up * 0.1f - character.transform.forward * backOffset;
            character.transform.position += want - hips.position;
        }

        public static NumuNpc MakeNpc(GameObject go, string baseState, Transform look, params string[] variations)
        {
            var npc = go.AddComponent<NumuNpc>();
            npc.displayName = go.name;
            npc.baseState = baseState;
            npc.lookTarget = look;
            npc.idleVariations = new List<string>(variations);
            npc.face = go.GetComponent<NumuFacialExpressions>();
            return npc;
        }

        public static void Blocker(GameObject go, float radius, float height)
        {
            var holder = new GameObject("Blocker") { layer = CameraIgnoreLayer };
            holder.transform.SetParent(go.transform, false);
            var c = holder.AddComponent<CapsuleCollider>();
            c.radius = radius;
            c.height = height;
            c.center = new Vector3(0, height * 0.5f, 0);
        }

        public static Transform Head(GameObject character) => character.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);

        /// <summary>The child the player controls, with an indoor-tuned follow camera.</summary>
        public (NumuPlayerController player, NumuFollowCamera camera) CreatePlayer(Transform parent, Vector3 pos, float yaw, float camDistance, float camHeight, float farClip = 120f)
        {
            var player = SpawnCharacter("Male_Child_01", "Player", parent, pos, yaw);
            player.GetComponent<Animator>().runtimeAnimatorController = NumuCharacterSetup.PlayerController;
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.4f;
            cc.radius = 0.22f;
            cc.center = new Vector3(0, 0.72f, 0);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.03f;
            var pc = player.AddComponent<NumuPlayerController>();
            pc.animator = player.GetComponent<Animator>();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(Root, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 52f;
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = farClip;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.requiresDepthOption = CameraOverrideOption.Off;
            camData.requiresColorOption = CameraOverrideOption.Off;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<NumuFollowCamera>();
            follow.target = player.transform;
            follow.distance = camDistance;
            follow.height = camHeight;
            follow.lookHeight = 1.0f;
            // Walls block the camera; furniture and people (camera-ignore layer) don't, so it never jumps behind a sofa.
            follow.obstacleMask = ~0 & ~(1 << CameraIgnoreLayer);
            Quaternion rot = Quaternion.Euler(0, yaw, 0);
            camGo.transform.position = pos + rot * new Vector3(0, camHeight, -camDistance);
            camGo.transform.LookAt(pos + Vector3.up);
            pc.cameraTransform = camGo.transform;
            return (pc, follow);
        }

        public NumuInteractionSystem CreateInteractions(Transform player)
        {
            var go = new GameObject("InteractionSystem");
            go.transform.SetParent(Root, false);
            var s = go.AddComponent<NumuInteractionSystem>();
            s.player = player;
            return s;
        }

        public NumuInteractable Interaction(string name, Vector3 pos, string prompt, Transform focus, float radius = 2f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var i = go.AddComponent<NumuInteractable>();
            i.prompt = prompt;
            i.radius = radius;
            i.focusPoint = focus;
            return i;
        }

        // ------------------------------------------------------------------ lighting

        public Light Sun(Vector3 euler, Color color, float intensity)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(Root, false);
            go.transform.rotation = Quaternion.Euler(euler);
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = color;
            sun.intensity = intensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            go.AddComponent<UniversalAdditionalLightData>().softShadowQuality = SoftShadowQuality.Medium;
            RenderSettings.sun = sun;
            return sun;
        }

        /// <summary>Bake-only rectangular light (ceiling panels, window fill). Costs nothing at runtime.</summary>
        public Light AreaLight(string name, Vector3 pos, Vector3 euler, Vector2 size, Color color, float intensity, float range = 12f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            var l = go.AddComponent<Light>();
            l.type = LightType.Rectangle;
            l.areaSize = size;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.lightmapBakeType = LightmapBakeType.Baked;
            l.shadows = LightShadows.Soft;
            return l;
        }

        /// <summary>
        /// Bake-only light facing up from just above the floor: stands in for the light a real floor bounces
        /// back onto the ceiling and upper walls, which keeps interiors from looking like grey boxes.
        /// </summary>
        public void Uplight(Rect floor, Color color, float intensity)
        {
            var l = AreaLight("FloorBounce", new Vector3(floor.center.x, 0.25f, floor.center.y), new Vector3(-90, 0, 0), floor.size * 0.9f, color, intensity, 14f);
            l.shadows = LightShadows.None;
        }

        public Material Sky(string hdriPath, float exposure, float rotation, Color tint)
        {
            var hdri = NumuWorldMaterials.Load<Cubemap>(hdriPath);
            var sky = new Material(Shader.Find("Skybox/Cubemap")) { name = $"M_{Name}Sky" };
            sky.SetTexture("_Tex", hdri);
            sky.SetFloat("_Exposure", exposure);
            sky.SetFloat("_Rotation", rotation);
            sky.SetColor("_Tint", tint);
            string path = $"{WorldDir}/M_{Name}Sky.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(sky, path);
            RenderSettings.skybox = sky;
            return sky;
        }

        public ReflectionProbe ReflectionProbe(string name, Vector3 center, Vector3 size, float intensity = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.position = center;
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Baked;
            p.size = size;
            p.resolution = 128;
            p.boxProjection = true;
            p.intensity = intensity;
            p.blendDistance = 0.6f;
            return p;
        }

        /// <summary>Light probes on a grid at the given heights, skipping points that fail the inside test.</summary>
        public void LightProbes(Rect area, float spacing, float[] heights, System.Func<Vector3, bool> inside = null)
        {
            var go = new GameObject("LightProbes");
            go.transform.SetParent(Root, false);
            var positions = new List<Vector3>();
            for (float x = area.xMin; x <= area.xMax + 0.01f; x += spacing)
                for (float z = area.yMin; z <= area.yMax + 0.01f; z += spacing)
                    foreach (var h in heights)
                    {
                        var p = new Vector3(x, h, z);
                        if (inside == null || inside(p)) positions.Add(p);
                    }
            go.AddComponent<LightProbeGroup>().probePositions = positions.ToArray();
        }

        public void PostProcessing(float exposure, float contrast, float saturation, float temperature, float bloom, float vignette)
        {
            string path = $"{WorldDir}/{Name}_PostProfile.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(exposure);
            color.contrast.Override(contrast);
            color.saturation.Override(saturation);
            profile.Add<WhiteBalance>(true).temperature.Override(temperature);
            var b = profile.Add<Bloom>(true);
            b.threshold.Override(1.1f);
            b.intensity.Override(bloom);
            b.scatter.Override(0.6f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(vignette);
            vig.smoothness.Override(0.5f);
            foreach (var c in profile.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);

            var go = new GameObject("PostProcessing");
            go.transform.SetParent(Root, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var rs = new GameObject("WorldRenderSettings").AddComponent<NumuWorldRenderSettings>();
            rs.transform.SetParent(Root, false);
            // Indoors nothing is far away: tight shadow distance keeps the shadow map sharp and cheap.
            rs.pcShadowDistance = 30f;
            rs.mobileShadowDistance = 20f;
        }

        public void Bake(float resolution, int maxBounces = 3)
        {
            var settings = new LightingSettings
            {
                name = $"{Name}Lighting",
                bakedGI = true,
                realtimeGI = false,
                // Shadowmask: static furniture shadows are baked, so on the Mobile tier (Shadowmask mode) only
                // characters render into the realtime shadow map.
                mixedBakeMode = MixedLightingMode.Shadowmask,
                lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                lightmapResolution = resolution,
                lightmapPadding = 3,
                lightmapMaxSize = 1024,
                // Large, nearly uniform ceilings and walls show block artifacts at normal compression.
                lightmapCompression = LightmapCompression.HighQuality,
                directionalityMode = LightmapsMode.CombinedDirectional,
                ao = true,
                aoMaxDistance = 0.6f,
                indirectResolution = 1f,
                directSampleCount = 32,
                indirectSampleCount = 384,
                environmentSampleCount = 256,
                maxBounces = maxBounces,
                // Interiors live on bounce light: a little extra keeps ceilings and corners from going grey.
                indirectScale = 1.35f,
                albedoBoost = 1.15f,
                filteringMode = LightingSettings.FilterMode.Auto,
            };
            string path = $"{WorldDir}/{Name}Lighting.lighting";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(settings, path);
            Lightmapping.lightingSettings = settings;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool ok = Lightmapping.Bake();
            var maps = LightmapSettings.lightmaps;
            bool hasDir = maps.Length > 0 && maps[0].lightmapDir != null;
            // Directional decoding without a direction map blows out every lightmapped surface.
            if (LightmapSettings.lightmapsMode == LightmapsMode.CombinedDirectional && !hasDir) LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
            Log.AppendLine($"Lighting bake: {(ok ? "done" : "FAILED")} in {sw.Elapsed.TotalSeconds:F0}s — {maps.Length} lightmap(s), mode {LightmapSettings.lightmapsMode}, direction maps: {hasDir}.");
        }
    }
}
