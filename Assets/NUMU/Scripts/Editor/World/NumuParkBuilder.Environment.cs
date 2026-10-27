using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Numu.World;

namespace Numu.EditorTools.World
{
    public static partial class NumuParkBuilder
    {
        class ParkMaterials
        {
            public Material bark, leaves, grassCards, paintedSteel, steelGalv, chain, rubberSeat, slidePlastic, deckWood, padRubber, curb;

            public ParkMaterials()
            {
                bark = NumuWorldMaterials.FromAmbientCG("Bark006", 1.4f, 0.35f, new Color(0.56f, 0.5f, 0.45f));
                var leafTex = NumuWorldMaterials.Load<Texture2D>("Assets/NUMU/Worlds/Textures/LeafCluster_Color.png");
                var leafNrm = NumuWorldMaterials.Load<Texture2D>("Assets/NUMU/Worlds/Textures/LeafCluster_Normal.png");
                leaves = NumuWorldMaterials.Lit("M_TreeLeaves", new NumuWorldMaterials.Spec
                {
                    baseMap = leafTex, normal = leafNrm, normalScale = 0.7f, smoothness = 0.28f, alphaClip = true, cutoff = 0.42f, doubleSided = true,
                    color = new Color(0.74f, 0.86f, 0.7f),
                });
                grassCards = NumuWorldMaterials.Lit("M_GrassCards", new NumuWorldMaterials.Spec
                {
                    baseMap = NumuWorldMaterials.Load<Texture2D>("Assets/NUMU/Worlds/Textures/GrassBlades_Color.png"),
                    smoothness = 0.12f, alphaClip = true, cutoff = 0.4f, doubleSided = true, receiveShadows = true, color = new Color(0.72f, 0.82f, 0.62f),
                });
                paintedSteel = NumuWorldMaterials.Lit("M_PaintedSteel_Blue", new NumuWorldMaterials.Spec { color = NumuColor.Hex("#2f6db5"), metallic = 0.35f, smoothness = 0.62f });
                steelGalv = NumuWorldMaterials.Lit("M_PaintedSteel_Green", new NumuWorldMaterials.Spec { color = NumuColor.Hex("#3d8f55"), metallic = 0.35f, smoothness = 0.6f });
                chain = NumuWorldMaterials.Lit("M_GalvanisedChain", new NumuWorldMaterials.Spec { color = NumuColor.Hex("#b9bcc0"), metallic = 0.9f, smoothness = 0.55f });
                rubberSeat = NumuWorldMaterials.Lit("M_RubberSeat", new NumuWorldMaterials.Spec
                {
                    baseMap = NumuWorldMaterials.Load<Texture2D>($"{ACG}/Rubber004/Rubber004_Color.png"),
                    normal = NumuWorldMaterials.Load<Texture2D>($"{ACG}/Rubber004/Rubber004_Normal.png"),
                    color = new Color(0.55f, 0.55f, 0.58f), smoothness = 0.25f, tiling = new Vector2(2f, 2f),
                });
                slidePlastic = NumuWorldMaterials.Lit("M_SlidePlastic", new NumuWorldMaterials.Spec { color = NumuColor.Hex("#f2b51d"), smoothness = 0.72f });
                deckWood = NumuWorldMaterials.Lit("M_DeckWood", new NumuWorldMaterials.Spec
                {
                    baseMap = NumuWorldMaterials.Load<Texture2D>($"{ACG}/Bark012/Bark012_Color.png"),
                    color = NumuColor.Hex("#c99a6e"), smoothness = 0.3f,
                });
                // Poured rubber safety surface: bright terracotta with the granular rubber relief.
                padRubber = NumuWorldMaterials.Lit("M_SafetySurface", new NumuWorldMaterials.Spec
                {
                    normal = NumuWorldMaterials.Load<Texture2D>($"{ACG}/Rubber004/Rubber004_Normal.png"),
                    normalScale = 0.8f,
                    color = NumuColor.Hex("#c8603f"), smoothness = 0.18f, tiling = Vector2.one / 1.2f,
                });
                curb = NumuWorldMaterials.FromAmbientCG("PavingStones138", 2f, 0.4f, new Color(0.92f, 0.92f, 0.9f));
            }
        }

        // ------------------------------------------------------------------ terrain

        static TerrainLayer Layer(string name, string acgId, float tile, float smoothness)
        {
            string path = $"{WorldDir}/TerrainLayer_{name}.terrainlayer";
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (l == null)
            {
                l = new TerrainLayer();
                AssetDatabase.CreateAsset(l, path);
            }
            l.diffuseTexture = NumuWorldMaterials.Load<Texture2D>($"{ACG}/{acgId}/{acgId}_Color.png");
            l.normalMapTexture = NumuWorldMaterials.Load<Texture2D>($"{ACG}/{acgId}/{acgId}_Normal.png");
            l.maskMapTexture = NumuWorldMaterials.Load<Texture2D>($"{ACG}/{acgId}/{acgId}_Mask.png");
            l.tileSize = new Vector2(tile, tile);
            l.normalScale = 1f;
            l.smoothness = smoothness;
            l.metallic = 0f;
            // Mask alpha is smoothness; cap it so lawns and paving never look wet or glossy.
            l.maskMapRemapMin = new Vector4(0f, 0f, 0f, 0f);
            l.maskMapRemapMax = new Vector4(0f, 1f, 1f, smoothness);
            EditorUtility.SetDirty(l);
            return l;
        }

        static void BuildTerrain(ParkMaterials mats)
        {
            const float size = 130f, height = 14f, baseOffset = 2f;
            int hmRes = 257, amRes = 512;
            string dataPath = $"{WorldDir}/ParkTerrain.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, dataPath);
            }
            td.heightmapResolution = hmRes;
            td.alphamapResolution = amRes;
            td.baseMapResolution = 512;
            td.size = new Vector3(size, height, size);
            Vector3 origin = new Vector3(-size / 2f, -baseOffset, -size / 2f);

            // Heights: gentle undulation in the park, flat walkways and playground, rising berms at the edges
            // that hide the horizon line.
            var h = new float[hmRes, hmRes];
            for (int z = 0; z < hmRes; z++)
                for (int x = 0; x < hmRes; x++)
                {
                    float wx = origin.x + x / (float)(hmRes - 1) * size, wz = origin.z + z / (float)(hmRes - 1) * size;
                    var p = new Vector2(wx, wz);
                    float roll = (Mathf.PerlinNoise(wx * 0.045f + 3.1f, wz * 0.045f + 7.7f) - 0.5f) * 0.9f + (Mathf.PerlinNoise(wx * 0.13f, wz * 0.13f) - 0.5f) * 0.18f;
                    float flatten = Mathf.Clamp01((PathDistance(p) - 1.2f) / 4f);
                    if (InPad(p, 3f)) flatten *= Mathf.Clamp01(((Mathf.Max(Mathf.Abs(p.x - PadCenter.x) - PadSize.x * 0.5f, Mathf.Abs(p.y - PadCenter.y) - PadSize.y * 0.5f)) + 0.5f) / 3f);
                    float r = p.magnitude;
                    float berm = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(44f, 62f, r)) * (3.5f + Mathf.PerlinNoise(wx * 0.06f, wz * 0.06f) * 4f);
                    float y = roll * flatten + berm;
                    h[z, x] = Mathf.Clamp01((y + baseOffset) / height);
                }
            td.SetHeights(0, 0, h);

            td.terrainLayers = new[]
            {
                Layer("Grass", "Grass004", 3.2f, 0.2f),
                Layer("Paving", "PavingStones138", 2.4f, 0.4f),
                Layer("Sand", "Ground054", 3f, 0.25f),
            };
            var am = new float[amRes, amRes, 3];
            for (int z = 0; z < amRes; z++)
                for (int x = 0; x < amRes; x++)
                {
                    float wx = origin.x + (x + 0.5f) / amRes * size, wz = origin.z + (z + 0.5f) / amRes * size;
                    var p = new Vector2(wx, wz);
                    float edgeNoise = (Mathf.PerlinNoise(wx * 0.9f, wz * 0.9f) - 0.5f) * 0.35f;
                    float walk = Mathf.Clamp01((1.35f - PathDistance(p) + edgeNoise) / 0.3f);
                    float plaza = Mathf.Clamp01((PlazaRadius + 0.2f - Vector2.Distance(p, PlazaCenter) + edgeNoise) / 0.3f);
                    float paving = Mathf.Max(walk, plaza);
                    // Worn sandy fringe around the playground and under the picnic tables.
                    float padDist = Mathf.Max(Mathf.Abs(p.x - PadCenter.x) - PadSize.x * 0.5f, Mathf.Abs(p.y - PadCenter.y) - PadSize.y * 0.5f);
                    float sand = Mathf.Clamp01((1.6f - padDist + edgeNoise * 2f) / 1.2f) * (1f - paving);
                    float picnic = Mathf.Clamp01((3.2f - Vector2.Distance(p, new Vector2(-13f, 6f)) + edgeNoise * 3f) / 1.5f) * 0.65f * (1f - paving);
                    sand = Mathf.Max(sand, picnic);
                    float grass = Mathf.Max(0f, 1f - paving - sand);
                    am[z, x, 0] = grass;
                    am[z, x, 1] = paving;
                    am[z, x, 2] = sand;
                }
            td.SetAlphamaps(0, 0, am);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.SetParent(_root, false);
            go.transform.position = origin;
            _terrain = go.GetComponent<Terrain>();
            var terrainMat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { name = "M_ParkTerrain" };
            terrainMat.EnableKeyword("_NORMALMAP");
            terrainMat.EnableKeyword("_MASKMAP");
            AssetDatabase.CreateAsset(terrainMat, $"{WorldDir}/M_ParkTerrain.mat");
            _terrain.materialTemplate = terrainMat;
            _terrain.basemapDistance = 60f;
            // The park is gently rolling: a coarser error tolerance and no terrain shadow casting
            // remove most of the terrain cost (it was rendered into every shadow cascade).
            _terrain.heightmapPixelError = 14f;
            _terrain.drawInstanced = true;
            _terrain.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            EditorUtility.SetDirty(td);
            _log.AppendLine($"Terrain {size}x{size} m, layers: grass / paving / sand (ambientCG PBR).");
        }

        // ------------------------------------------------------------------ playground

        static void BuildPlayground(ParkMaterials mats)
        {
            var g = Group("Playground");
            // Rubber safety surface with a paving curb.
            var pad = new MeshBuilder();
            RealisticKit.Slab(pad, PadSize, 0.6f, 0.12f, 1f);
            float padY = GroundY(PadCenter.x, PadCenter.y) + 0.06f;
            MeshGO("SafetySurface", g, pad, mats.padRubber).transform.position = new Vector3(PadCenter.x, padY, PadCenter.y);
            var curb = new MeshBuilder();
            RealisticKit.Slab(curb, PadSize + Vector2.one * 0.5f, 0.85f, 0.2f, 1f);
            MeshGO("Curb", g, curb, mats.curb).transform.position = new Vector3(PadCenter.x, padY - 0.035f, PadCenter.y);

            // Swing set: static frame + one swinging pivot per seat.
            var swingRoot = new GameObject("SwingSet").transform;
            swingRoot.SetParent(g, false);
            swingRoot.position = new Vector3(SwingPos.x, padY, SwingPos.z);
            var frame = new MeshBuilder();
            var hooks = RealisticKit.SwingFrame(frame, 3.4f, 2.45f, 2);
            MeshGO("Frame", swingRoot, frame, mats.paintedSteel, true, ShadowCastingMode.On, false);
            for (int i = 0; i < hooks.Count; i++)
            {
                var pivot = new GameObject($"SeatPivot_{i}").transform;
                pivot.SetParent(swingRoot, false);
                pivot.localPosition = hooks[i];
                var chain = new MeshBuilder();
                var seat = new MeshBuilder();
                RealisticKit.SwingSeat(chain, seat, 1.95f, 0.46f);
                MeshGO("Chains", pivot, chain, mats.chain, false);
                MeshGO("Seat", pivot, seat, mats.rubberSeat, false);
                var swing = pivot.gameObject.AddComponent<NumuSwing>();
                swing.pivot = pivot;
                swing.chainLength = 1.95f;
                // The empty seat just sways gently in the breeze.
                swing.amplitude = i == 0 ? 22f : 3f;
                var seatPoint = new GameObject("SeatPoint").transform;
                seatPoint.SetParent(pivot, false);
                seatPoint.localPosition = new Vector3(0, -1.97f - 0.07f, 0);
            }

            // Slide tower.
            var slide = new GameObject("Slide").transform;
            slide.SetParent(g, false);
            slide.position = new Vector3(SlidePos.x, padY, SlidePos.z);
            var metal = new MeshBuilder();
            var wood = new MeshBuilder();
            var plastic = new MeshBuilder();
            RealisticKit.SlideTower(metal, wood, plastic, 1.5f);
            MeshGO("Structure", slide, metal, mats.steelGalv, true, ShadowCastingMode.On, false);
            MeshGO("Deck", slide, wood, mats.deckWood, true, ShadowCastingMode.On, false);
            MeshGO("Chute", slide, plastic, mats.slidePlastic, true, ShadowCastingMode.On, false);

            // Simple colliders so the player walks around the equipment, not through it.
            var block = new GameObject("SwingBlocker").AddComponent<BoxCollider>();
            block.transform.SetParent(swingRoot, false);
            block.size = new Vector3(3.8f, 2.4f, 0.4f);
            block.center = new Vector3(0, 1.2f, 0);
            var sBlock = new GameObject("SlideBlocker").AddComponent<BoxCollider>();
            sBlock.transform.SetParent(slide, false);
            sBlock.size = new Vector3(1.4f, 2.6f, 4.8f);
            sBlock.center = new Vector3(0, 1.3f, -1.2f);
            _log.AppendLine("Playground: rubber safety surface, steel swing set (2 chain seats), slide tower.");
        }

        // ------------------------------------------------------------------ trees

        static void BuildTrees(ParkMaterials mats)
        {
            var g = Group("Trees");
            var variants = new List<(Mesh bark, Mesh leaves, Mesh leavesLite, float height)>();
            for (int v = 0; v < 5; v++)
            {
                float hgt = 7.5f + v * 1.1f;
                var shape = RealisticKit.Broadleaf(hgt);
                shape.mainBranches = 5 + v % 3;
                var bark = new MeshBuilder();
                var leaves = new MeshBuilder();
                RealisticKit.Tree(bark, leaves, shape, 900 + v * 17);
                // LOD1: identical skeleton (same seed; leaves are generated after the branches) with fewer, larger cards.
                var liteShape = shape;
                liteShape.leafDensity *= 0.35f;
                liteShape.leafCardSize *= 1.45f;
                var barkLite = new MeshBuilder();
                var leavesLite = new MeshBuilder();
                RealisticKit.Tree(barkLite, leavesLite, liteShape, 900 + v * 17);
                var bm = StoreMesh(bark.ToMesh($"TreeBark_{v}"));
                bm.RecalculateTangents();
                var lm = StoreMesh(leaves.ToMesh($"TreeLeaves_{v}"));
                lm.RecalculateTangents();
                var ll = StoreMesh(leavesLite.ToMesh($"TreeLeavesLite_{v}"));
                ll.RecalculateTangents();
                variants.Add((bm, lm, ll, hgt));
            }
            int n = 0;
            var rng = new System.Random(5);
            foreach (var spot in TreeSpots)
            {
                var (bm, lm, ll, _) = variants[n % variants.Count];
                var t = new GameObject($"Tree_{n:00}").transform;
                t.SetParent(g, false);
                t.position = OnGround(spot);
                t.rotation = Quaternion.Euler(0, rng.Next(360), 0);
                t.localScale = Vector3.one * (0.85f + (float)rng.NextDouble() * 0.35f);
                var lod = t.gameObject.AddComponent<LODGroup>();
                var barkGo = Part(t, "Bark", bm, mats.bark, ShadowCastingMode.On);
                var leafGo = Part(t, "Leaves", lm, mats.leaves, ShadowCastingMode.On);
                var leafLite = Part(t, "LeavesLOD1", ll, mats.leaves, ShadowCastingMode.On);
                // Trees light from probes (alpha cards make poor lightmap receivers).
                foreach (var r in new[] { barkGo.GetComponent<MeshRenderer>(), leafGo.GetComponent<MeshRenderer>(), leafLite.GetComponent<MeshRenderer>() })
                    r.receiveGI = ReceiveGI.LightProbes;
                var barkR = barkGo.GetComponent<Renderer>();
                lod.SetLODs(new[]
                {
                    new LOD(0.32f, new Renderer[] { barkR, leafGo.GetComponent<Renderer>() }),
                    new LOD(0.03f, new Renderer[] { barkR, leafLite.GetComponent<Renderer>() }),
                });
                var col = t.gameObject.AddComponent<CapsuleCollider>();
                col.radius = 0.35f;
                col.height = 3f;
                col.center = new Vector3(0, 1.5f, 0);
                n++;
            }
            // A continuous tree line on the surrounding berms closes the view like a real park edge.
            for (int i = 0; i < 34; i++)
            {
                float a = i / 34f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.1f;
                float rad = 49f + (float)rng.NextDouble() * 8f;
                var spot = new Vector3(Mathf.Cos(a) * rad, 0, Mathf.Sin(a) * rad);
                if (Mathf.Abs(spot.x) < 5f && spot.z < 0f) continue; // keep the entrance open
                var (bm, _, lm, _) = variants[(n + i) % variants.Count];
                var t = new GameObject($"TreeLine_{i:00}").transform;
                t.SetParent(g, false);
                t.position = OnGround(spot) + Vector3.down * 0.3f;
                t.rotation = Quaternion.Euler(0, rng.Next(360), 0);
                t.localScale = Vector3.one * (1.15f + (float)rng.NextDouble() * 0.4f);
                var b = Part(t, "Bark", bm, mats.bark, ShadowCastingMode.Off);
                var l = Part(t, "Leaves", lm, mats.leaves, ShadowCastingMode.Off);
                b.GetComponent<MeshRenderer>().receiveGI = ReceiveGI.LightProbes;
                l.GetComponent<MeshRenderer>().receiveGI = ReceiveGI.LightProbes;
                var lod = t.gameObject.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.02f, new Renderer[] { b.GetComponent<Renderer>(), l.GetComponent<Renderer>() }) });
            }

            // Garden bushes along the walks and around the playground.
            var bushTwigs = new MeshBuilder();
            var bushLeaves = new MeshBuilder();
            int bushes = 0;
            for (int i = 0; i < 600 && bushes < 34; i++)
            {
                var p = new Vector2((float)rng.NextDouble() * 76f - 38f, (float)rng.NextDouble() * 76f - 38f);
                float pd = PathDistance(p);
                bool edge = pd > 2.6f && pd < 4.2f;
                bool padEdge = !InPad(p, 2.2f) && InPad(p, 3.6f) && p.y > PadCenter.y - 2f;
                if (!(edge || padEdge) || !LawnFree(p, 0.8f) || p.magnitude > 40f) continue;
                if (Vector2.Distance(p, new Vector2(PlayerStart.x, PlayerStart.z)) < 4f) continue;
                RealisticKit.Bush(bushTwigs, bushLeaves, OnGround(new Vector3(p.x, 0, p.y)), 0.9f + (float)rng.NextDouble() * 0.7f, rng.Next(99999));
                bushes++;
            }
            MeshGO("BushTwigs", g, bushTwigs, mats.bark, true, ShadowCastingMode.Off, false);
            MeshGO("BushLeaves", g, bushLeaves, mats.leaves, true, ShadowCastingMode.On, false);
            _log.AppendLine($"Trees: {n} park trees + tree line from 5 procedural variants (bark + leaf-cluster cards, ~4k tris each); {bushes} bushes.");
        }

        static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, ShadowCastingMode shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            return go;
        }

        // ------------------------------------------------------------------ Poly Haven props

        static GameObject PlaceModel(string id, Transform parent, Vector3 pos, float yaw, float scale = 1f, bool collider = false)
        {
            var prefab = NumuPolyHavenSetup.Prefab(id);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = OnGround(pos);
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one * scale;
            foreach (var t in go.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            if (collider)
            {
                var b = new Bounds(go.transform.position, Vector3.zero);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(b.center);
                box.size = new Vector3(b.size.x / scale, b.size.y / scale, b.size.z / scale);
            }
            return go;
        }

        static void BuildProps()
        {
            NumuPolyHavenSetup.SetupAll(_log);
            var g = Group("Props");
            // Benches facing the playground, where grown-ups watch.
            PlaceModel("painted_wooden_bench", g, new Vector3(7.2f, 0, 0.6f), 0f, 1f, true);
            PlaceModel("painted_wooden_bench", g, new Vector3(13.4f, 0, 0.6f), 0f, 1f, true);
            PlaceModel("painted_wooden_bench", g, new Vector3(-4.2f, 0, -15f), 90f, 1f, true);
            PlaceModel("painted_wooden_bench", g, new Vector3(4.2f, 0, -15f), -90f, 1f, true);
            PlaceModel("metal_trash_can_bin", g, new Vector3(5.2f, 0, 0.4f), 0f, 1f, true);
            PlaceModel("metal_trash_can_bin", g, new Vector3(2.8f, 0, -20.5f), 30f, 1f, true);
            PlaceModel("wooden_picnic_table", g, new Vector3(-13f, 0, 8.5f), 15f, 1f, true);
            PlaceModel("wooden_picnic_table", g, new Vector3(-15.5f, 0, 2.5f), -20f, 1f, true);
            PlaceModel("football_ball", g, new Vector3(-6.5f, 0f, -1f), 40f);

            // Street lamps along the loop and entrance: the Poly Haven lantern is a wall bracket, so each
            // one is mounted on a painted steel post.
            var postMb = new MeshBuilder();
            var lanternMetalMat = NumuWorldMaterials.Lit("M_LanternMetal", new NumuWorldMaterials.Spec { color = NumuColor.Hex("#262b2f"), metallic = 0.6f, smoothness = 0.45f });
            var lanternGlassMat = NumuWorldMaterials.Lit("M_LanternGlass", new NumuWorldMaterials.Spec { color = NumuColor.Hex("#e8e4d6"), smoothness = 0.9f, emission = NumuColor.Hex("#2a2418") });
            Mesh lodMetal = null, lodGlass = null;
            GameObject Lamp(Vector3 p, float yaw)
            {
                var lamp = PlaceModel("street_lamp_02", g, p, yaw, 1f, false);
                if (lamp == null) return null;
                // The scanned lantern is ~20k triangles: it is LOD0 for close-ups only; a ~100-triangle lantern takes over beyond that.
                var scanned = lamp.GetComponentsInChildren<Renderer>();
                var lb = scanned[0].bounds;
                foreach (var sr in scanned) lb.Encapsulate(sr.bounds);
                if (lodMetal == null)
                {
                    var mm = new MeshBuilder();
                    var gm = new MeshBuilder();
                    RealisticKit.Lantern(mm, gm, Vector3.zero, 0.62f);
                    lodMetal = StoreMesh(mm.ToMesh("LanternLOD1_Metal"));
                    lodGlass = StoreMesh(gm.ToMesh("LanternLOD1_Glass"));
                }
                var lod1 = new GameObject("LanternLOD1");
                lod1.transform.SetParent(lamp.transform, false);
                Vector3 hang = new Vector3(lb.center.x, lb.max.y - 0.55f, lb.center.z) + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, 0.15f);
                var lm1 = Part(lod1.transform, "Metal", lodMetal, lanternMetalMat, ShadowCastingMode.Off);
                var lg1 = Part(lod1.transform, "Glass", lodGlass, lanternGlassMat, ShadowCastingMode.Off);
                lod1.transform.position = hang;
                foreach (var pr in new[] { lm1, lg1 }) pr.GetComponent<MeshRenderer>().receiveGI = ReceiveGI.LightProbes;
                var lampLod = lamp.GetComponent<LODGroup>();
                lampLod.SetLODs(new[]
                {
                    new LOD(0.3f, scanned),
                    new LOD(0.015f, new Renderer[] { lm1.GetComponent<Renderer>(), lg1.GetComponent<Renderer>() }),
                });
                foreach (var lr in lamp.GetComponentsInChildren<Renderer>()) lr.shadowCastingMode = ShadowCastingMode.Off;
                Quaternion r = Quaternion.Euler(0, yaw, 0);
                Vector3 basePos = OnGround(p);
                Vector3 postPos = basePos + r * new Vector3(0, 0, -0.43f);
                lamp.transform.position = basePos + Vector3.up * 1.55f;
                RealisticKit.Tube(postMb, new List<Vector3> { postPos + Vector3.down * 0.2f, postPos + Vector3.up * 3.3f }, t => Mathf.Lerp(0.07f, 0.045f, t), 12, 2f);
                RealisticKit.Tube(postMb, new List<Vector3> { postPos + Vector3.down * 0.05f, postPos + Vector3.up * 0.35f }, t => 0.11f, 12, 2f);
                var col = lamp.AddComponent<CapsuleCollider>();
                col.radius = 0.12f;
                col.height = 3.2f;
                col.center = lamp.transform.InverseTransformPoint(postPos + Vector3.up * 1.6f);
                return lamp;
            }
            int lamps = 0;
            for (int i = 0; i < 9; i++)
            {
                float a = (i / 9f) * Mathf.PI * 2f + 0.3f;
                Vector2 p = LoopCenter + new Vector2(Mathf.Cos(a) * (LoopRadii.x + 2.1f), Mathf.Sin(a) * (LoopRadii.y + 2.1f));
                if (Vector2.Distance(p, PlazaCenter) < PlazaRadius + 2f) continue;
                Vector2 toCenter = (LoopCenter - p).normalized;
                Lamp(new Vector3(p.x, 0, p.y), Mathf.Atan2(toCenter.x, toCenter.y) * Mathf.Rad2Deg);
                lamps++;
            }
            foreach (float z in new[] { -42f, -31f })
            {
                Lamp(new Vector3(2.4f, 0, z), -90f);
                lamps++;
            }
            var lampPostMat = NumuWorldMaterials.Lit("M_LampPost", new NumuWorldMaterials.Spec { color = NumuColor.Hex("#2d3338"), metallic = 0.5f, smoothness = 0.45f });
            MeshGO("LampPosts", g, postMb, lampPostMat, true, ShadowCastingMode.On, false);

            // Plaza planters with flowers.
            foreach (var p in new[] { new Vector3(-3.6f, 0, -11.8f), new Vector3(3.6f, 0, -11.8f), new Vector3(-3.6f, 0, -18.2f), new Vector3(3.6f, 0, -18.2f) })
            {
                var planter = PlaceModel("planter_box_01", g, p, p.x > 0 ? 90f : -90f, 1f, true);
                if (planter == null) continue;
                var top = planter.GetComponentInChildren<Renderer>().bounds.max.y;
                for (int k = -1; k <= 1; k += 2)
                {
                    int pick = ((int)Mathf.Abs(p.x * 3f + p.z + k) % 3 + 3) % 3;
                    var f = PlaceModel($"flower_gazania_{"deg"[pick]}", g, p + new Vector3(k * 0.22f, 0, k * 0.12f), k * 70f, 1.6f);
                    if (f != null) f.transform.position = new Vector3(f.transform.position.x, top - 0.08f, f.transform.position.z);
                }
            }

            // Shrubs and ferns: along the entrance, around the plaza and edging the playground.
            var shrubs = Group("Shrubs");
            var rng = new System.Random(77);
            int placed = 0;
            for (int i = 0; i < 400 && placed < 70; i++)
            {
                var p = new Vector2((float)rng.NextDouble() * 80f - 40f, (float)rng.NextDouble() * 80f - 40f);
                float pd = PathDistance(p);
                bool nearPath = pd > 2.2f && pd < 4.5f;
                bool nearPad = !InPad(p, 1.6f) && InPad(p, 3.2f) && p.y > PadCenter.y;
                if (!(nearPath || nearPad) || !LawnFree(p, 0.4f) || p.magnitude > 42f) continue;
                if (Vector2.Distance(p, new Vector2(PlayerStart.x, PlayerStart.z)) < 3f) continue;
                double roll = rng.NextDouble();
                string id = roll < 0.45 ? $"shrub_03_{"ab"[rng.Next(2)]}" : $"fern_02_{"abcd"[rng.Next(4)]}";
                float scale = id.StartsWith("fern") ? 1.2f + (float)rng.NextDouble() * 0.5f : 2.2f + (float)rng.NextDouble() * 0.8f;
                PlaceModel(id, shrubs, new Vector3(p.x, 0, p.y), rng.Next(360), scale);
                placed++;
            }
            _log.AppendLine($"Props: benches, picnic tables, {lamps} street lamps, trash cans, planters + flowers, {placed} shrubs/ferns (Poly Haven CC0).");
        }

        // ------------------------------------------------------------------ grass

        static void BuildGrass(ParkMaterials mats)
        {
            var g = Group("Grass");
            var rng = new System.Random(31);
            int chunks = 0, tufts = 0;
            for (int cx = -2; cx < 2; cx++)
                for (int cz = -2; cz < 2; cz++)
                {
                    var mb = new MeshBuilder();
                    for (int i = 0; i < 700; i++)
                    {
                        var p = new Vector2((cx + (float)rng.NextDouble()) * 21f, (cz + (float)rng.NextDouble()) * 21f);
                        if (!LawnFree(p, -0.6f)) continue;
                        // Longer grass survives at path edges and around tree trunks; mown lawn elsewhere.
                        float pd = PathDistance(p);
                        bool nearTree = false;
                        foreach (var ts in TreeSpots) if (Vector2.Distance(p, new Vector2(ts.x, ts.z)) < 3.2f) { nearTree = true; break; }
                        if (!(nearTree || (pd > 1.3f && pd < 2.4f)) && rng.NextDouble() > 0.04) continue;
                        Vector3 pos = OnGround(new Vector3(p.x, 0, p.y));
                        Vector3 n = _terrain.terrainData.GetInterpolatedNormal((p.x - _terrain.transform.position.x) / 130f, (p.y - _terrain.transform.position.z) / 130f);
                        RealisticKit.GrassTuft(mb, pos - Vector3.up * 0.03f, n, 0.18f + (float)rng.NextDouble() * 0.2f, 0.36f, rng.Next(99999));
                        tufts++;
                    }
                    if (mb.VertexCount == 0) continue;
                    var go = MeshGO($"GrassChunk_{cx}_{cz}", g, mb, mats.grassCards, true, ShadowCastingMode.Off, false);
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                    chunks++;
                }
            _log.AppendLine($"Grass: {tufts} tufts in {chunks} chunks.");
        }

        // ------------------------------------------------------------------ lighting & atmosphere

        static void BuildLightingAndAtmosphere()
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(_root, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = NumuColor.Hex("#fff1de");
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sunGo.transform.rotation = Quaternion.Euler(44f, -38f, 0f);
            var lightData = sunGo.AddComponent<UniversalAdditionalLightData>();
            lightData.softShadowQuality = SoftShadowQuality.Medium;

            var hdri = NumuWorldMaterials.Load<Cubemap>("Assets/ThirdParty/PolyHaven/HDRI/farm_field_puresky_2k.hdr");
            var sky = new Material(Shader.Find("Skybox/Cubemap")) { name = "M_ParkSky" };
            sky.SetTexture("_Tex", hdri);
            sky.SetFloat("_Exposure", 1.05f);
            sky.SetFloat("_Rotation", 210f);
            sky.SetColor("_Tint", new Color(0.52f, 0.52f, 0.52f));
            AssetDatabase.CreateAsset(sky, $"{WorldDir}/M_ParkSky.mat");
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            // Ambient comes from a calm gradient sampled from the HDRI's colours: the HDRI's sun disc
            // would otherwise sparkle through baked lighting. The HDRI still drives the sky and reflections.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = NumuColor.Hex("#a3bedf");
            RenderSettings.ambientEquatorColor = NumuColor.Hex("#c4c9bd");
            RenderSettings.ambientGroundColor = NumuColor.Hex("#6b6a50");
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.9f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = NumuColor.Hex("#b7cbe0");
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 320f;

            var probeGo = new GameObject("ReflectionProbe");
            probeGo.transform.SetParent(_root, false);
            probeGo.transform.position = new Vector3(4f, 2.5f, 2f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.size = new Vector3(110f, 30f, 110f);
            probe.resolution = 128;
            probe.boxProjection = false;

            // Light probes over the walkable area for characters and probe-lit foliage.
            var lpGo = new GameObject("LightProbes");
            lpGo.transform.SetParent(_root, false);
            var group = lpGo.AddComponent<LightProbeGroup>();
            var positions = new List<Vector3>();
            for (float x = -36; x <= 36; x += 6f)
                for (float z = -36; z <= 36; z += 6f)
                {
                    float y = GroundY(x, z);
                    positions.Add(new Vector3(x, y + 0.6f, z));
                    positions.Add(new Vector3(x, y + 3.5f, z));
                }
            group.probePositions = positions.ToArray();

            var volumeGo = new GameObject("PostProcessing");
            volumeGo.transform.SetParent(_root, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = ParkVolumeProfile();

            var rs = new GameObject("WorldRenderSettings").AddComponent<NumuWorldRenderSettings>();
            rs.transform.SetParent(_root, false);
            _log.AppendLine("Lighting: mixed sun with soft shadows, HDRI sky (Poly Haven), skybox ambient + reflections, baked reflection probe, light-probe grid, fog, post FX.");
        }

        static VolumeProfile ParkVolumeProfile()
        {
            string path = $"{WorldDir}/Park_PostProfile.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(-0.1f);
            color.contrast.Override(14f);
            color.saturation.Override(12f);
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.18f);
            bloom.scatter.Override(0.6f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.16f);
            vig.smoothness.Override(0.5f);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.highlights.Override(new Vector4(1f, 0.98f, 0.94f, 0f));
            smh.shadows.Override(new Vector4(0.95f, 0.97f, 1.02f, 0f));
            foreach (var c in profile.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // ------------------------------------------------------------------ baking

        static void BakeLighting(UnityEngine.SceneManagement.Scene scene)
        {
            var settings = new LightingSettings
            {
                name = "ParkLighting",
                bakedGI = true,
                realtimeGI = false,
                mixedBakeMode = MixedLightingMode.IndirectOnly,
                lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                lightmapResolution = 3f,
                lightmapPadding = 2,
                lightmapMaxSize = 1024,
                lightmapCompression = LightmapCompression.NormalQuality,
                // Directional lightmaps keep normal-map detail on baked surfaces (one extra texture, mobile-safe).
                directionalityMode = LightmapsMode.CombinedDirectional,
                ao = true,
                aoMaxDistance = 1.2f,
                indirectResolution = 1f,
                directSampleCount = 32,
                indirectSampleCount = 256,
                environmentSampleCount = 256,
                maxBounces = 2,
                filteringMode = LightingSettings.FilterMode.Auto,
            };
            string path = $"{WorldDir}/ParkLighting.lighting";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(settings, path);
            Lightmapping.lightingSettings = settings;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool ok = Lightmapping.Bake();
            // Guard against a mode/data mismatch: directional decoding without a direction map blows out every lightmapped surface.
            var maps = LightmapSettings.lightmaps;
            bool hasDir = maps.Length > 0 && maps[0].lightmapDir != null;
            if (LightmapSettings.lightmapsMode == LightmapsMode.CombinedDirectional && !hasDir) LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
            _log.AppendLine($"Lighting bake: {(ok ? "done" : "FAILED")} in {sw.Elapsed.TotalSeconds:F0}s — {maps.Length} lightmap(s), mode {LightmapSettings.lightmapsMode}, direction maps: {hasDir} (baked indirect + AO, light probes, reflection probe).");
        }
    }
}
