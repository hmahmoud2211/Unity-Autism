using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Numu.World;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Character test scene (Assets/Scenes/NUMU_CharacterTest.unity): the NUMU boy on a small town square, framed like
    /// the expression reference (head and torso, soft light, blurred background), showing the surprised expression
    /// with hands on start. Number keys switch emotions in Play Mode (see <see cref="NumuExpressionShowcase"/>).
    /// </summary>
    public static class NumuCharacterTestScene
    {
        public const string ScenePath = "Assets/Scenes/NUMU_CharacterTest.unity";
        const string Dir = "Assets/NUMU/Characters/TestScene";

        [MenuItem("NUMU/Characters/Build Character Test Scene")]
        public static void BuildFromMenu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Aborted: exit Play Mode first.";
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath) return $"Aborted: '{active.path}' has unsaved changes.";
            var prefab = NumuCharacterSetup.Prefab(NumuCharacterSetup.StylizedBoy);
            if (prefab == null) return "Aborted: Char_NUMU_Boy prefab is missing (run NUMU/Characters setup).";
            System.IO.Directory.CreateDirectory(Dir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Soft daylight: warm key from the front-left, cool sky fill, as in the reference cutscenes.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38f, 155f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.84f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.65f, 0.63f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.4f, 0.38f);
            RenderSettings.skybox = null;

            // Town square: pale paving, an ochre house, a grey wall and a lamp post (all far enough to be blurred).
            var paving = Mat("M_Test_Paving", new Color(0.78f, 0.77f, 0.74f), 0.15f);
            Block("Ground", new Vector3(0, -0.05f, -6f), new Vector3(40, 0.1f, 30), paving);
            Block("House", new Vector3(-3.5f, 3f, -11f), new Vector3(9, 6, 4), Mat("M_Test_Ochre", new Color(0.86f, 0.58f, 0.36f), 0.1f));
            Block("Roof", new Vector3(-3.5f, 6.4f, -11f), new Vector3(9.4f, 0.8f, 4.4f), Mat("M_Test_Roof", new Color(0.62f, 0.36f, 0.26f), 0.1f));
            Block("Wall", new Vector3(5f, 1.1f, -8f), new Vector3(10, 2.2f, 0.5f), Mat("M_Test_Wall", new Color(0.72f, 0.72f, 0.72f), 0.1f));
            Block("Facade", new Vector3(7f, 3.5f, -14f), new Vector3(10, 7, 3), Mat("M_Test_Facade", new Color(0.83f, 0.83f, 0.85f), 0.1f));
            var post = Mat("M_Test_Lamp", new Color(0.12f, 0.12f, 0.13f), 0.3f);
            Block("LampPost", new Vector3(2.4f, 1.8f, -5.5f), new Vector3(0.14f, 3.6f, 0.14f), post);
            Block("LampHead", new Vector3(2.4f, 3.7f, -5.5f), new Vector3(0.35f, 0.4f, 0.35f), post);

            // The character, facing the camera.
            var boy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            boy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var showcase = boy.AddComponent<NumuExpressionShowcase>();
            showcase.character = boy.GetComponent<EmotionController>();
            showcase.startEmotion = NumuEmotion.Surprise;

            // Camera framed like the reference: head and torso, slightly below eye level.
            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.transform.position = new Vector3(0.05f, 1.2f, 1.25f);
            cam.transform.LookAt(new Vector3(0f, 1.13f, 0f));
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.8f, 0.86f, 0.93f);
            var data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var volume = new GameObject("PostProcessing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = Profile();

            EditorSceneManager.SaveScene(scene, ScenePath);
            return $"Built {ScenePath}: Char_NUMU_Boy showing Surprise with hands; keys 1-7 switch emotions, H toggles hands.";
        }

        static Material Mat(string name, Color c, float smoothness) =>
            NumuWorldMaterials.Lit(name, new NumuWorldMaterials.Spec { color = c, smoothness = smoothness });

        static void Block(string name, Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static VolumeProfile Profile()
        {
            string path = $"{Dir}/CharacterTest_PostProfile.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(8f);
            // The reference's soft, out-of-focus background.
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(2.5f);
            dof.gaussianEnd.Override(9f);
            dof.gaussianMaxRadius.Override(1.5f);
            foreach (var c in profile.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
