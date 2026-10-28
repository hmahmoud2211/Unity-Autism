using UnityEditor;
using UnityEngine;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Mobile-first import rules for third-party art (Rocketbox, Poly Haven, ambientCG) and generated world textures.
    /// Applied on first import so large source files never land in the project with desktop settings.
    /// </summary>
    public class NumuAssetImportRules : AssetPostprocessor
    {
        static readonly string[] LightmappedProps =
        {
            "bench", "picnic", "planter", "trash_can", "street_lamp", "sofa", "chair", "desk", "shelves", "drawer_cabinet",
            "crate", "cardboard_box", "chalkboard", "coffee_table", "dining_table", "round_wooden_table",
        };

        static bool Managed(string path) =>
            path.StartsWith("Assets/ThirdParty/") || path.StartsWith("Assets/NUMU/Worlds/");

        void OnPreprocessTexture()
        {
            if (!Managed(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            string lower = assetPath.ToLowerInvariant();
            string file = System.IO.Path.GetFileNameWithoutExtension(lower);

            if (lower.Contains("/hdri/") && (lower.EndsWith(".hdr") || lower.EndsWith(".exr")))
            {
                ti.textureShape = TextureImporterShape.TextureCube;
                ti.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                ti.maxTextureSize = 1024;
                ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }

            bool normal = file.Contains("_normal") || file.Contains("nor_gl") || file.EndsWith("_nor");
            bool linearMask = file.Contains("_mask") || file.Contains("rough") || file.Contains("_arm") || file.Contains("_metal") || file.Contains("_ao");
            bool head = assetPath.Contains("/Rocketbox/") && file.Contains("_head_color");
            bool ui = assetPath.Contains("/Worlds/UI/");

            ti.textureType = normal ? TextureImporterType.NormalMap : ui ? TextureImporterType.Sprite : TextureImporterType.Default;
            ti.sRGBTexture = !normal && !linearMask;
            ti.mipmapEnabled = !ui;
            ti.streamingMipmaps = !ui;
            ti.alphaIsTransparency = file.Contains("_color") || file.Contains("diff") || ui;
            ti.maxTextureSize = head ? 2048 : 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.anisoLevel = normal || ui ? 1 : 4;

            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var s = ti.GetPlatformTextureSettings(platform);
                s.overridden = true;
                s.maxTextureSize = head ? 1024 : (normal ? 512 : 1024);
                s.format = normal ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6;
                s.compressionQuality = 50;
                ti.SetPlatformTextureSettings(s);
            }
        }

        void OnPreprocessModel()
        {
            if (!Managed(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.importCameras = false;
            mi.importLights = false;

            if (assetPath.Contains("/Rocketbox/"))
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importBlendShapes = true;
                mi.importBlendShapeNormals = ModelImporterNormals.Calculate;
                bool anim = assetPath.Contains("/Animations/");
                mi.importAnimation = anim;
                mi.materialImportMode = anim ? ModelImporterMaterialImportMode.None : ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                // Mesh compression can visibly distort blendshape faces; keep characters lossless.
                mi.meshCompression = ModelImporterMeshCompression.Off;
            }
            else
            {
                // Only large, simple furniture gets lightmap UVs. Small or many-part props (food, tableware, dials,
                // chess pieces, foliage) can't be packed: Unity then empties the mesh. They light from probes instead.
                string file = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
                mi.generateSecondaryUV = System.Array.Exists(LightmappedProps, k => file.Contains(k));
                // Scanned props are dense (some 20-70k triangles): Unity Mesh LOD simplifies them with screen size.
                // Alpha-card foliage keeps its hand-made LODs.
                bool foliage = file.Contains("shrub") || file.Contains("fern") || file.Contains("flower") || file.Contains("grass");
                mi.generateMeshLods = !foliage;
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            }
        }
    }
}
