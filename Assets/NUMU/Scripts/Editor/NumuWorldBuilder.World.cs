using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Numu.EditorTools
{
    public static partial class NumuWorldBuilder
    {
        // ------------------------------------------------------------------ layout (reference pixels)

        class IslandDef
        {
            public string Id;
            public float Top;
            public int Seed;
            public float[] Px;
            public float Dome = 0.3f;
            public string Owner;      // gameplay island this terrain belongs to
            public float BlockScale = 1f;
        }

        static readonly IslandDef[] IslandDefs =
        {
            new IslandDef { Id = "Home", Owner = "home", Top = 4.5f, Seed = 11, Dome = 0.35f, Px = new float[]
            {
                25,300, 45,240, 95,190, 170,150, 250,120, 330,95, 420,90, 500,110, 560,160, 595,230, 600,300,
                570,345, 500,375, 420,390, 330,395, 250,395, 170,392, 90,385, 40,355,
            } },
            new IslandDef { Id = "Home_LedgeLeft", Owner = "home", Top = 2.4f, Seed = 12, Dome = 0.1f, BlockScale = 0.8f, Px = new float[]
            {
                118,405, 160,392, 240,396, 256,420, 236,452, 170,457, 124,442,
            } },
            new IslandDef { Id = "Home_LedgeRight", Owner = "home", Top = 2.2f, Seed = 13, Dome = 0.1f, BlockScale = 0.8f, Px = new float[]
            {
                330,412, 380,398, 460,402, 494,426, 472,456, 400,466, 345,452,
            } },
            new IslandDef { Id = "School", Owner = "school", Top = 6.2f, Seed = 21, Dome = 0.25f, Px = new float[]
            {
                565,215, 585,160, 630,120, 720,100, 810,95, 900,100, 960,120, 990,165, 985,215, 955,250,
                890,268, 800,272, 720,268, 650,258, 595,240,
            } },
            new IslandDef { Id = "Park", Owner = "park", Top = 5.4f, Seed = 31, Dome = 0.3f, Px = new float[]
            {
                995,230, 1010,175, 1050,135, 1120,110, 1210,100, 1300,105, 1380,125, 1430,165, 1450,220,
                1435,275, 1390,305, 1310,320, 1220,325, 1130,322, 1060,312, 1015,280,
            } },
            new IslandDef { Id = "Supermarket", Owner = "supermarket", Top = 3.6f, Seed = 41, Dome = 0.25f, Px = new float[]
            {
                1040,470, 1062,420, 1110,385, 1180,365, 1260,352, 1350,348, 1440,355, 1510,375, 1555,420,
                1568,480, 1555,540, 1515,585, 1450,607, 1360,615, 1270,610, 1180,595, 1105,570, 1058,530,
            } },
            new IslandDef { Id = "Hub", Owner = "hub", Top = 3.3f, Seed = 51, Dome = 0.15f, Px = new float[]
            {
                640,480, 660,440, 705,412, 770,398, 840,402, 895,428, 922,470, 918,512, 885,543, 820,560,
                745,562, 685,548, 650,520,
            } },
            new IslandDef { Id = "Hub_Upper", Owner = "hub", Top = 4.7f, Seed = 52, Dome = 0.1f, BlockScale = 0.7f, Px = new float[]
            {
                705,460, 722,433, 765,425, 815,425, 852,437, 865,462, 845,488, 785,495, 728,488,
            } },
        };

        // Grass-topped islets: x, y, radius(px), top height, seed.
        static readonly float[,] Islets =
        {
            { 60, 585, 48, 2.4f, 71 }, { 240, 592, 58, 2.0f, 72 }, { 540, 603, 30, 1.3f, 73 }, { 588, 512, 26, 1.6f, 74 },
            { 1210, 590, 30, 1.5f, 75 }, { 902, 338, 26, 2.2f, 76 }, { 978, 335, 23, 3.9f, 77 },
        };

        // Boulders: x, y, radius(px), height, moss.
        static readonly float[,] Boulders =
        {
            { 100, 545, 15, 0.9f, 0.5f }, { 380, 532, 28, 1.5f, 0.45f }, { 360, 568, 12, 0.6f, 0.3f }, { 620, 560, 15, 0.8f, 0.4f },
            { 660, 578, 13, 0.7f, 0.2f }, { 955, 520, 12, 0.6f, 0.4f }, { 940, 600, 10, 0.5f, 0f }, { 1140, 612, 32, 1.6f, 0.5f },
            { 1300, 645, 38, 1.8f, 0.35f }, { 1095, 640, 12, 0.6f, 0f }, { 1520, 640, 20, 1.0f, 0.4f }, { 1240, 655, 14, 0.7f, 0.2f },
            { 690, 340, 22, 1.6f, 0.4f }, { 560, 322, 18, 1.4f, 0.5f }, { 622, 345, 12, 0.8f, 0.3f }, { 420, 472, 16, 1.0f, 0.4f },
            { 1000, 640, 14, 0.7f, 0.3f }, { 470, 560, 10, 0.5f, 0f }, { 18, 470, 24, 1.2f, 0.5f }, { 1462, 662, 16, 0.8f, 0.3f },
            { 150, 470, 14, 0.8f, 0.4f }, { 330, 470, 12, 0.7f, 0.3f }, { 1060, 600, 10, 0.5f, 0.3f },
        };

        static readonly Dictionary<string, IslandShape> IslandShapes = new Dictionary<string, IslandShape>();
        static readonly Dictionary<string, Transform> IslandRoots = new Dictionary<string, Transform>();
        static readonly List<Collider> GroundColliders = new List<Collider>();

        // ------------------------------------------------------------------ environment

        static void BuildEnvironment()
        {
            var env = Group("Environment", _world);

            // Ocean.
            var water = new MeshBuilder();
            float size = 900f;
            Vector3 c = P(RefW * 0.5f, RefH * 0.5f, 0f);
            int i0 = water.Add(c + new Vector3(-size, 0, -size), Vector3.up, Color.white);
            int i1 = water.Add(c + new Vector3(size, 0, -size), Vector3.up, Color.white);
            int i2 = water.Add(c + new Vector3(size, 0, size), Vector3.up, Color.white);
            int i3 = water.Add(c + new Vector3(-size, 0, size), Vector3.up, Color.white);
            water.QuadFacing(i0, i1, i2, i3, Vector3.up);
            var waterGo = MeshObject("Ocean", env, water, "Environment", Materials.Water, false);
            waterGo.GetComponent<MeshRenderer>().receiveShadows = false;

            // Haze bands follow the composition: behind the islands and off the sides.
            var rig = Object.FindAnyObjectByType<NumuLightingRig>();
            float backZ = P(RefW * 0.5f, 95f, 6.2f).z;
            float topEdgeZ = P(RefW * 0.5f, 0f, 0f).z;
            rig.hazeStartZ = backZ + 1.5f;
            rig.hazeEndZ = topEdgeZ + 1f;
            rig.hazeAmount = 0.86f;
            float sideX = Mathf.Abs(P(20f, 300f, 4f).x);
            rig.sideHazeStart = sideX + 2f;
            rig.sideHazeEnd = sideX + 26f;
            rig.sideHazeAmount = 0.65f;
            rig.Apply();
            _log.AppendLine($"Haze: z {rig.hazeStartZ:F1}..{rig.hazeEndZ:F1}, |x| {rig.sideHazeStart:F1}..{rig.sideHazeEnd:F1}");

            BuildMountains(env);
            BuildClouds(env);
        }

        static void BuildMountains(Transform env)
        {
            // x, base y, width px, top y (all reference pixels). Bases sit just below the waterline.
            float[,] m =
            {
                { 28, 120, 70, -30 }, { 112, 100, 46, 18 }, { 178, 92, 40, 40 }, { 545, 60, 40, 8 }, { 935, 78, 36, 26 },
                { 1290, 64, 40, 2 }, { 1352, 140, 58, 8 }, { 1424, 92, 44, -10 }, { 1522, 205, 76, 34 }, { 1568, 175, 44, 60 },
                { 255, 60, 30, 30 }, { 1210, 50, 30, 20 },
            };
            var mb = new MeshBuilder();
            for (int i = 0; i < m.GetLength(0); i++)
            {
                Vector3 basePos = P(m[i, 0], m[i, 1], -2f);
                float h = HeightFromPixels(m[i, 0], m[i, 1], m[i, 3], -2f);
                float r = U(m[i, 0], m[i, 1], -2f) * m[i, 2] * 0.5f;
                StructureKit.KarstPillar(mb, basePos, h, r, 300 + i, i % 3 != 1);
                // Smaller companion peaks give each mountain a ragged silhouette.
                var rng = new System.Random(300 + i);
                for (int k = 0; k < (m[i, 0] > 1250 ? 1 : 2); k++)
                {
                    float side = k == 0 ? -1f : 1f;
                    Vector3 off = new Vector3(side * r * (0.9f + (float)rng.NextDouble() * 0.5f), 0, r * (0.3f + (float)rng.NextDouble() * 0.6f));
                    StructureKit.KarstPillar(mb, basePos + off, h * (0.45f + (float)rng.NextDouble() * 0.3f), r * (0.55f + (float)rng.NextDouble() * 0.25f), 350 + i * 7 + k, rng.NextDouble() < 0.5);
                }
            }
            // Small floating rock shelves with trees on the far right (as in the reference).
            foreach (var (px, py, rpx) in new[] { (1458f, 112f, 26f), (1535f, 178f, 32f) })
            {
                Vector3 p = P(px, py, 4f);
                float r = U(px, py, 4f) * rpx;
                StructureKit.KarstPillar(mb, p + Vector3.down * 9f, 9f, r, (int)px, true);
            }
            MeshObject("Mountains", env, mb, "Environment", Materials.Far, false);
        }

        static void BuildClouds(Transform env)
        {
            // x, y, size px, height.
            float[,] cl =
            {
                { 40, 515, 80, 1.2f }, { 175, 498, 55, 1.4f }, { 10, 410, 55, 3.0f }, { 470, 640, 55, 0.8f }, { 960, 662, 50, 0.6f },
                { 1545, 615, 85, 1.0f }, { 1565, 300, 55, 3.0f }, { 1500, 690, 70, 0.4f }, { 300, 40, 60, 5f }, { 990, 30, 55, 5f },
                { 1240, 25, 45, 5f }, { 70, 160, 55, 5f }, { 640, 700, 60, 0.4f }, { 1180, 690, 60, 0.4f }, { 30, 690, 70, 0.4f },
            };
            var mb = new MeshBuilder();
            for (int i = 0; i < cl.GetLength(0); i++)
            {
                Vector3 p = P(cl[i, 0], cl[i, 1], cl[i, 3]);
                float s = U(cl[i, 0], cl[i, 1], cl[i, 3]) * cl[i, 2] * 0.72f;
                StructureKit.Cloud(mb, p, s, 500 + i);
            }
            var go = MeshObject("Clouds", env, mb, "Environment", Materials.Cloud, false);
            go.AddComponent<NumuDrift>().amplitude = new Vector3(0.6f, 0.08f, 0.2f);
        }

        // ------------------------------------------------------------------ islands

        static void BuildIslands()
        {
            IslandShapes.Clear();
            IslandRoots.Clear();
            GroundColliders.Clear();
            var islandsRoot = Group("Islands", _world);

            foreach (var def in IslandDefs)
            {
                var outline = new List<Vector2>();
                for (int i = 0; i < def.Px.Length; i += 2)
                {
                    Vector3 w = P(def.Px[i], def.Px[i + 1], def.Top);
                    outline.Add(new Vector2(w.x, w.z));
                }
                BuildIsland(def.Id, def.Owner, islandsRoot, outline, def.Top, def.Seed, def.Dome, def.BlockScale, true);
            }

            for (int i = 0; i < Islets.GetLength(0); i++)
            {
                float px = Islets[i, 0], py = Islets[i, 1], rpx = Islets[i, 2], top = Islets[i, 3];
                int seed = (int)Islets[i, 4];
                Vector3 c = P(px, py, top);
                float r = U(px, py, top) * rpx;
                var outline = new List<Vector2>();
                var rng = new System.Random(seed);
                for (int k = 0; k < 9; k++)
                {
                    float a = k / 9f * Mathf.PI * 2f;
                    float rr = r * (0.85f + 0.3f * (float)rng.NextDouble());
                    outline.Add(new Vector2(c.x + Mathf.Cos(a) * rr, c.z + Mathf.Sin(a) * rr * 0.9f));
                }
                string id = i == Islets.GetLength(0) - 1 ? "Landing" : $"Islet_{i}";
                BuildIsland(id, null, islandsRoot, outline, top, seed, 0.15f, 0.65f, true);
            }

            var rocks = new MeshBuilder();
            for (int i = 0; i < Boulders.GetLength(0); i++)
            {
                float px = Boulders[i, 0], py = Boulders[i, 1], rpx = Boulders[i, 2], h = Boulders[i, 3], moss = Boulders[i, 4];
                Vector3 p = P(px, py, h * 0.4f);
                float r = U(px, py, 0f) * rpx;
                IslandKit.Boulder(rocks, new Vector3(p.x, h * 0.25f - 0.2f, p.z), new Vector3(r, h, r * 0.9f), 900 + i, moss);
            }
            MeshObject("Boulders", islandsRoot, rocks, "Islands", Materials.Rock, true);

            Physics.SyncTransforms();
        }

        static void BuildIsland(string id, string owner, Transform parent, List<Vector2> worldOutline, float top, int seed, float dome, float blockScale, bool collider)
        {
            Vector2 centre = Vector2.zero;
            foreach (var p in worldOutline) centre += p;
            centre /= worldOutline.Count;
            var local = new List<Vector2>();
            foreach (var p in worldOutline) local.Add(p - centre);

            var spec = new IslandSpec { Name = id, Outline = local, TopY = top, Seed = seed, Dome = dome, BlockScale = blockScale };
            var topMb = new MeshBuilder();
            var cliffMb = new MeshBuilder();
            var skirtMb = new MeshBuilder();
            var shape = IslandKit.Build(spec, topMb, cliffMb, skirtMb);

            var root = Group(id, parent);
            root.position = new Vector3(centre.x, 0f, centre.y);
            var topGo = MeshObject("Top", root, topMb, "Islands", Materials.Ground, true, collider);
            MeshObject("Cliffs", root, cliffMb, "Islands", Materials.Rock, true);
            var skirt = MeshObject("Shelf", root, skirtMb, "Islands", Materials.Ground, false);
            skirt.GetComponent<MeshRenderer>().receiveShadows = false;
            if (collider) GroundColliders.Add(topGo.GetComponent<Collider>());

            // Shape data in world space for later placement.
            var ws = new IslandShape { TopY = shape.TopY, Perimeter = shape.Perimeter, Centroid = shape.Centroid + root.position };
            for (int i = 0; i < shape.Rim.Count; i++)
            {
                ws.Rim.Add(shape.Rim[i] + root.position);
                ws.RimOut.Add(shape.RimOut[i]);
            }
            IslandShapes[id] = ws;
            IslandRoots[id] = root;
        }

        // ------------------------------------------------------------------ ground queries

        /// <summary>Highest island surface under a world XZ position.</summary>
        public static bool GroundAt(Vector3 xz, out Vector3 point, out Vector3 normal)
        {
            point = xz;
            normal = Vector3.up;
            var ray = new Ray(new Vector3(xz.x, 60f, xz.z), Vector3.down);
            float best = float.MaxValue;
            bool found = false;
            foreach (var col in GroundColliders)
            {
                if (col.Raycast(ray, out var hit, 120f) && hit.distance < best)
                {
                    best = hit.distance;
                    point = hit.point;
                    normal = hit.normal;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Ground point under a reference pixel; iterates because the pixel→world mapping depends on height.</summary>
        public static Vector3 G(float px, float py, float guessY = 4f)
        {
            float y = guessY;
            Vector3 p = P(px, py, y);
            for (int i = 0; i < 4; i++)
            {
                if (!GroundAt(p, out var hit, out _)) return new Vector3(p.x, y, p.z);
                if (Mathf.Abs(hit.y - y) < 0.02f) return hit;
                y = hit.y;
                p = P(px, py, y);
            }
            return GroundAt(p, out var h2, out _) ? h2 : p;
        }

        /// <summary>Rim point of an island nearest a reference pixel, plus its outward direction.</summary>
        public static Vector3 Rim(string islandId, float px, float py, out Vector3 outward)
        {
            var shape = IslandShapes[islandId];
            Vector3 guess = P(px, py, shape.TopY);
            int i = shape.ClosestRimIndex(guess);
            outward = shape.RimOut[i];
            return shape.Rim[i];
        }
    }
}
