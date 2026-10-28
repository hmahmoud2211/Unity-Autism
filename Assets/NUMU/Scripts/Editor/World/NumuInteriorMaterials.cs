using UnityEngine;

namespace Numu.EditorTools.World
{
    /// <summary>PBR materials shared by the indoor worlds (ambientCG CC0 textures, URP Lit).</summary>
    public static class NumuInteriorMaterials
    {
        const string ACG = "Assets/ThirdParty/ambientCG";

        static Texture2D T(string id, string kind) => NumuWorldMaterials.Load<Texture2D>($"{ACG}/{id}/{id}_{kind}.png");

        /// <summary>A tinted variant of an ambientCG material with its own name (so several tints can coexist).</summary>
        public static Material Acg(string name, string id, float tileMetres, Color tint, float smoothness = 0.5f, float normalScale = 1f, bool mask = true)
        {
            return NumuWorldMaterials.Lit(name, new NumuWorldMaterials.Spec
            {
                baseMap = T(id, "Color"),
                normal = T(id, "Normal"),
                normalScale = normalScale,
                maskMap = mask ? T(id, "Mask") : null,
                smoothness = smoothness,
                tiling = Vector2.one / tileMetres,
                color = tint,
            });
        }

        public static Material Paint(string name, Color color, float smoothness = 0.25f) =>
            // Matte wall paint over plaster: the plaster normal gives a subtle roller texture.
            NumuWorldMaterials.Lit(name, new NumuWorldMaterials.Spec
            {
                normal = T("PaintedPlaster017", "Normal"),
                normalScale = 0.35f,
                color = color,
                smoothness = smoothness,
                tiling = Vector2.one / 1.6f,
            });

        public static Material Solid(string name, Color color, float smoothness, float metallic = 0f) =>
            NumuWorldMaterials.Lit(name, new NumuWorldMaterials.Spec { color = color, smoothness = smoothness, metallic = metallic });

        public static Material Glass(string name = "M_WindowGlass") =>
            NumuWorldMaterials.Lit(name, new NumuWorldMaterials.Spec
            {
                color = new Color(0.82f, 0.9f, 0.95f, 0.16f),
                smoothness = 0.96f,
                transparent = true,
                doubleSided = true,
            });

        public static Material Emissive(string name, Color color, float intensity) =>
            NumuWorldMaterials.Lit(name, new NumuWorldMaterials.Spec { color = color, smoothness = 0.5f, emission = color * intensity });

        public static Material Textured(string name, Texture2D tex, float smoothness = 0.35f, bool alphaClip = false) =>
            NumuWorldMaterials.Lit(name, new NumuWorldMaterials.Spec { baseMap = tex, smoothness = smoothness, alphaClip = alphaClip, cutoff = 0.5f });
    }
}
