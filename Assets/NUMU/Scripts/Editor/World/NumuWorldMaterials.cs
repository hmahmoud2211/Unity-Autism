using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Numu.EditorTools.World
{
    /// <summary>URP Lit (PBR) material factory for the realistic worlds.</summary>
    public static class NumuWorldMaterials
    {
        public const string Dir = "Assets/NUMU/Worlds/Materials";

        public class Spec
        {
            public Texture baseMap;
            public Color color = Color.white;
            public Texture normal;
            public float normalScale = 1f;
            public Texture maskMap;          // R metallic, G AO, B height, A smoothness (ambientCG packing)
            public float metallic;
            public float smoothness = 0.3f;
            public Vector2 tiling = Vector2.one;
            public bool alphaClip;
            public float cutoff = 0.45f;
            public bool doubleSided;
            public bool receiveShadows = true;
            public Color emission = Color.black;
            /// <summary>Alpha-blended surface (glass); <see cref="color"/>'s alpha sets the opacity.</summary>
            public bool transparent;
        }

        public static Material Lit(string name, Spec s)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetFloat("_WorkflowMode", 1f);
            m.SetTexture("_BaseMap", s.baseMap);
            m.SetColor("_BaseColor", s.color);
            m.SetTextureScale("_BaseMap", s.tiling);
            m.SetTexture("_MainTex", s.baseMap);
            m.SetTextureScale("_MainTex", s.tiling);

            SetKeyword(m, "_NORMALMAP", s.normal != null);
            m.SetTexture("_BumpMap", s.normal);
            m.SetFloat("_BumpScale", s.normalScale);

            // ambientCG masks keep smoothness in alpha and AO in green: reuse them for both slots.
            SetKeyword(m, "_METALLICSPECGLOSSMAP", s.maskMap != null);
            m.SetTexture("_MetallicGlossMap", s.maskMap);
            SetKeyword(m, "_OCCLUSIONMAP", s.maskMap != null);
            m.SetTexture("_OcclusionMap", s.maskMap);
            m.SetFloat("_OcclusionStrength", 0.8f);
            m.SetFloat("_Metallic", s.metallic);
            m.SetFloat("_Smoothness", s.maskMap != null ? 1f : s.smoothness);
            m.SetFloat("_GlossMapScale", s.smoothness / 0.5f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);

            m.SetFloat("_AlphaClip", s.alphaClip ? 1f : 0f);
            m.SetFloat("_Cutoff", s.cutoff);
            SetKeyword(m, "_ALPHATEST_ON", s.alphaClip);
            m.SetOverrideTag("RenderType", s.transparent ? "Transparent" : s.alphaClip ? "TransparentCutout" : "Opaque");
            m.renderQueue = s.transparent ? (int)RenderQueue.Transparent : s.alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            m.SetFloat("_Surface", s.transparent ? 1f : 0f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", s.transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            m.SetFloat("_DstBlend", s.transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            m.SetFloat("_SrcBlendAlpha", s.transparent ? (float)BlendMode.One : (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", s.transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", s.transparent ? 0f : 1f);
            SetKeyword(m, "_SURFACE_TYPE_TRANSPARENT", s.transparent);

            m.SetFloat("_Cull", s.doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            m.doubleSidedGI = s.doubleSided;
            m.SetFloat("_ReceiveShadows", s.receiveShadows ? 1f : 0f);
            SetKeyword(m, "_RECEIVE_SHADOWS_OFF", !s.receiveShadows);

            bool emissive = s.emission.maxColorComponent > 0.001f;
            SetKeyword(m, "_EMISSION", emissive);
            m.SetColor("_EmissionColor", s.emission);
            m.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void SetKeyword(Material m, string kw, bool on)
        {
            if (on) m.EnableKeyword(kw);
            else m.DisableKeyword(kw);
        }

        public static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogWarning($"[NUMU] Missing asset: {path}");
            return a;
        }

        /// <summary>Material from an ambientCG folder processed by the import script (Color/Normal/Mask).</summary>
        public static Material FromAmbientCG(string id, float tileMetres, float smoothnessScale = 0.5f, Color? tint = null)
        {
            string dir = $"Assets/ThirdParty/ambientCG/{id}";
            return Lit($"M_{id}", new Spec
            {
                baseMap = Load<Texture2D>($"{dir}/{id}_Color.png"),
                normal = Load<Texture2D>($"{dir}/{id}_Normal.png"),
                maskMap = Load<Texture2D>($"{dir}/{id}_Mask.png"),
                smoothness = smoothnessScale,
                tiling = Vector2.one / tileMetres,
                color = tint ?? Color.white,
            });
        }
    }
}
