using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Numu.EditorTools
{
    /// <summary>Creates (or refreshes) the shared NUMU materials under Assets/NUMU/Materials.</summary>
    public static class Materials
    {
        public static Material Vertex, Foliage, Ground, Rock, Cloud, Far, Water, Waterfall, Glossy, Lamp, WindowLit, Sky, Character, CharacterEyes, Glass, Flag;

        const string Toon = "NUMU/Toon";

        public static void Ensure()
        {
            Vertex = Make("M_Vertex", Toon, m => ToonDefaults(m));
            Foliage = Make("M_Foliage", Toon, m =>
            {
                ToonDefaults(m);
                m.SetFloat("_WindStrength", 1f);
                m.SetFloat("_ShadeSoftness", 0.45f);
                m.SetFloat("_RimStrength", 0.22f);
                m.SetColor("_RimColor", NumuColor.Hex("#fff6c8"));
            });
            Flag = Make("M_Flag", Toon, m =>
            {
                ToonDefaults(m);
                m.SetFloat("_WindStrength", 1f);
                m.SetFloat("_Cull", (float)CullMode.Off);
            });
            Ground = Make("M_Ground", Toon, m =>
            {
                ToonDefaults(m);
                m.SetFloat("_ShadeSoftness", 0.5f);
                m.SetFloat("_RimStrength", 0.08f);
            });
            Rock = Make("M_Rock", Toon, m =>
            {
                ToonDefaults(m);
                m.SetColor("_ShadeColor", NumuColor.Hex("#b4a8cf"));
                m.SetFloat("_ShadeSoftness", 0.4f);
                m.SetFloat("_RimStrength", 0.14f);
            });
            Cloud = Make("M_Cloud", Toon, m =>
            {
                ToonDefaults(m);
                m.SetColor("_ShadeColor", NumuColor.Hex("#dcd3ee"));
                m.SetFloat("_ShadeSoftness", 0.6f);
                m.SetFloat("_ShadowStrength", 0.25f);
                m.SetFloat("_RimStrength", 0.45f);
                m.SetColor("_RimColor", NumuColor.Hex("#ffffff"));
                m.SetColor("_EmissionColor", NumuColor.Hex("#1a1418"));
            });
            Far = Make("M_FarMountains", Toon, m =>
            {
                ToonDefaults(m);
                m.SetColor("_ShadeColor", NumuColor.Hex("#d9d0e6"));
                m.SetFloat("_ShadeSoftness", 0.6f);
                m.SetFloat("_ShadowStrength", 0.2f);
                m.SetFloat("_RimStrength", 0.3f);
                m.SetColor("_EmissionColor", NumuColor.Hex("#221a1e"));
            });
            Glossy = Make("M_Glossy", Toon, m =>
            {
                ToonDefaults(m);
                m.SetFloat("_HighlightStrength", 0.9f);
                m.SetFloat("_HighlightSize", 0.93f);
            });
            Character = Make("M_Character", Toon, m =>
            {
                ToonDefaults(m);
                m.SetColor("_ShadeColor", NumuColor.Hex("#d6c3dc"));
                m.SetFloat("_ShadeSoftness", 0.5f);
                m.SetFloat("_RimStrength", 0.35f);
                m.SetFloat("_RimPower", 2.6f);
                m.SetFloat("_HighlightStrength", 0.12f);
                m.SetFloat("_HighlightSize", 0.97f);
            });
            CharacterEyes = Make("M_CharacterEyes", Toon, m =>
            {
                ToonDefaults(m);
                m.SetFloat("_HighlightStrength", 1.2f);
                m.SetFloat("_HighlightSize", 0.95f);
                m.SetFloat("_RimStrength", 0.05f);
            });
            Glass = Make("M_Glass", Toon, m =>
            {
                ToonDefaults(m);
                m.SetColor("_ShadeColor", NumuColor.Hex("#c9d8f0"));
                m.SetFloat("_HighlightStrength", 1f);
                m.SetFloat("_HighlightSize", 0.9f);
                m.SetFloat("_RimStrength", 0.4f);
                m.SetColor("_RimColor", NumuColor.Hex("#e9f6ff"));
            });
            Lamp = Make("M_LampGlow", Toon, m =>
            {
                ToonDefaults(m);
                m.SetColor("_EmissionColor", NumuColor.Hex("#ffcf7a") * 1.6f);
            });
            WindowLit = Make("M_WindowLit", Toon, m =>
            {
                ToonDefaults(m);
                m.SetColor("_EmissionColor", NumuColor.Hex("#7a4d12"));
            });
            Water = Make("M_Water", "NUMU/Water", m =>
            {
                m.SetColor("_DeepColor", NumuColor.Hex("#2fa6d6"));
                m.SetColor("_MidColor", NumuColor.Hex("#46c0e4"));
                m.SetColor("_ShallowColor", NumuColor.Hex("#9ae6ea"));
                m.SetFloat("_DepthScale", 0.32f);
                m.SetFloat("_ShallowAlpha", 0.6f);
                m.SetFloat("_FoamDistance", 0.75f);
                m.SetFloat("_FoamRings", 0.32f);
                m.SetFloat("_RippleScale", 0.08f);
                m.SetFloat("_RippleStrength", 0.22f);
                m.SetFloat("_SparkleStrength", 0.18f);
            });
            Waterfall = Make("M_Waterfall", "NUMU/Waterfall", m =>
            {
                m.SetColor("_TopColor", NumuColor.Hex("#8fdcf3"));
                m.SetColor("_BottomColor", NumuColor.Hex("#d9f5fc"));
                m.SetFloat("_Speed", 1.1f);
                m.SetFloat("_StreakScale", 6f);
                m.SetFloat("_Opacity", 0.95f);
            });
            Sky = Make("M_Sky", "NUMU/Sky", m =>
            {
                m.SetColor("_TopColor", NumuColor.Hex("#eed8de"));
                m.SetColor("_HorizonColor", NumuColor.Hex("#fde9dc"));
                m.SetColor("_BottomColor", NumuColor.Hex("#cfe6f1"));
                m.SetColor("_GlowColor", NumuColor.Hex("#ffe2b8"));
            });
            AssetDatabase.SaveAssets();
        }

        static void ToonDefaults(Material m)
        {
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_VertexColorStrength", 1f);
            m.SetColor("_ShadeColor", NumuColor.Hex("#bfb1d9"));
            m.SetFloat("_ShadeStep", -0.05f);
            m.SetFloat("_ShadeSoftness", 0.38f);
            m.SetFloat("_ShadowStrength", 0.8f);
            m.SetColor("_RimColor", NumuColor.Hex("#fff3e2"));
            m.SetFloat("_RimStrength", 0.18f);
            m.SetFloat("_RimPower", 3.2f);
            m.SetFloat("_HighlightStrength", 0f);
            m.SetColor("_EmissionColor", Color.black);
            m.SetFloat("_WindStrength", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_Cull", (float)CullMode.Back);
        }

        public static Material Make(string name, string shaderName, System.Action<Material> setup)
        {
            string path = $"{NumuWorldBuilder.MaterialsDir}/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.Exception($"Shader '{shaderName}' not found (did it fail to compile?)");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            setup(m);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Textured toon material (signs, icons). Cached by name.</summary>
        public static Material Textured(string name, Texture2D tex, float alphaClip = 0.5f, bool unlitish = false)
        {
            return Make(name, Toon, m =>
            {
                ToonDefaults(m);
                m.SetTexture("_BaseMap", tex);
                m.SetFloat("_AlphaClip", alphaClip);
                m.SetFloat("_VertexColorStrength", 0f);
                if (unlitish)
                {
                    m.SetColor("_ShadeColor", NumuColor.Hex("#f2ecf6"));
                    m.SetFloat("_RimStrength", 0f);
                }
            });
        }

        public static VolumeProfile VolumeProfile()
        {
            string path = $"{NumuWorldBuilder.MaterialsDir}/NUMU_PostProfile.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(NumuColor.Hex("#fff1e0"));

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.08f);
            color.contrast.Override(6f);
            color.saturation.Override(4f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.None);

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(4f);

            foreach (var c in profile.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
