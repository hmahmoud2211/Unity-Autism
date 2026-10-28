using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Numu.World;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Prepares the Microsoft Rocketbox (MIT) characters: PBR materials, humanoid animation clips,
    /// Animator controllers and character prefabs with ARKit-blendshape facial expressions.
    /// </summary>
    public static class NumuCharacterSetup
    {
        const string AvatarRoot = "Assets/ThirdParty/Rocketbox/Avatars";
        const string AnimRoot = "Assets/ThirdParty/Rocketbox/Animations";
        public const string Dir = "Assets/NUMU/Characters";

        public static string PrefabPath(string avatar) => $"{Dir}/Char_{avatar}.prefab";
        public static GameObject Prefab(string avatar) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(avatar));
        public static RuntimeAnimatorController PlayerController => PlayerControllerFor(PlayerAvatar);

        public static RuntimeAnimatorController PlayerControllerFor(string avatar) =>
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{Dir}/{(IsFemale(avatar) ? "Player_Female" : "Player_Child")}.controller");

        const string VroidRoot = "Assets/ThirdParty/VRoid";

        /// <summary>Anime (VRoid/VRM) characters: id → (file folder, female).</summary>
        static readonly Dictionary<string, bool> VrmFemale = new Dictionary<string, bool> { { "SendagayaShino", true }, { "SakuradaFumiriya", false } };

        public static bool IsFemale(string avatar) =>
            avatar.StartsWith("Female") || (avatar.StartsWith("VRM_") && VrmFemale.TryGetValue(avatar.Substring(4), out var f) && f);

        /// <summary>
        /// Art direction switch. NUMU uses semi-realistic human characters (Rocketbox) throughout; the VRoid anime
        /// characters stay importable but are only used when this is turned on.
        /// </summary>
        public static bool UseAnimeCharacters = false;

        /// <summary>
        /// The character the child plays: the NUMU-style boy (stylised Rocketbox boy, see <see cref="SetupStylizedBoy"/>),
        /// or the anime avatar when anime is turned on.
        /// </summary>
        public static string PlayerAvatar =>
            UseAnimeCharacters && Prefab("VRM_SendagayaShino") != null ? "VRM_SendagayaShino" :
            Prefab(StylizedBoy) != null ? StylizedBoy : "Male_Child_01";

        public const string StylizedBoy = "NUMU_Boy";
        public static RuntimeAnimatorController NpcController(bool female) => AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{Dir}/{(female ? "NPC_Female" : "NPC_Male")}.controller");

        public static void SetupAll(StringBuilder log)
        {
            Directory.CreateDirectory(Dir);
            int clips = SetupAnimations();
            var female = BuildNpcController("NPC_Female", "f");
            var male = BuildNpcController("NPC_Male", "m");
            BuildPlayerController();
            int avatars = 0;
            foreach (var dir in Directory.GetDirectories(AvatarRoot))
            {
                string name = Path.GetFileName(dir);
                SetupAvatar(name, name.StartsWith("Female") ? female : male);
                avatars++;
            }
            int vrm = SetupVrmCharacters(female, male);
            SetupStylizedBoy(male);
            log?.AppendLine($"Characters: {avatars} Rocketbox avatars and {vrm} anime (VRM) avatars → prefabs with facial rigs; {clips} humanoid clips; 4 animator controllers.");
        }

        // ------------------------------------------------------------------ clips

        public static AnimationClip Clip(string file)
        {
            string path = $"{AnimRoot}/{file}.fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        static int SetupAnimations()
        {
            int n = 0;
            foreach (var path in Directory.GetFiles(AnimRoot, "*.fbx"))
            {
                string p = path.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(p);
                var importer = (ModelImporter)AssetImporter.GetAtPath(p);
                if (importer.clipAnimations.Length == 1 && importer.clipAnimations[0].name == name) { n++; continue; }
                var defaults = importer.defaultClipAnimations;
                if (defaults.Length == 0) continue;
                var c = defaults[0];
                bool moving = name.Contains("walk") || name.Contains("run");
                // Turns and sit-down/stand-up are one-shot transitions; their root motion is baked into the pose.
                bool oneShot = name.Contains("_turn_") || name.Contains("sit_down") || name.Contains("stand_up");
                c.name = name;
                c.loopTime = !oneShot;
                c.loopPose = !moving;
                // Bake rotation and height into the pose; keep horizontal motion only for locomotion clips,
                // which the controller drives in place (root motion is off).
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = !moving;
                c.keepOriginalPositionXZ = true;
                importer.clipAnimations = new[] { c };
                importer.SaveAndReimport();
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ controllers

        /// <summary>Reuses the controller asset (keeping its GUID/identity) and clears it for a fresh build.</summary>
        static AnimatorController NewController(string name)
        {
            string path = $"{Dir}/{name}.controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var st in sm.states) sm.RemoveState(st.state);
            foreach (var p in ctrl.parameters) ctrl.RemoveParameter(p);
            foreach (var bt in AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>()) AssetDatabase.RemoveObjectFromAsset(bt);
            var layers = ctrl.layers;
            layers[0].iKPass = true;
            ctrl.layers = layers;
            return ctrl;
        }

        static AnimatorState State(AnimatorController ctrl, string state, string clip)
        {
            var c = Clip(clip);
            if (c == null)
            {
                Debug.LogWarning($"[NUMU] Missing clip {clip}");
                return null;
            }
            var s = ctrl.layers[0].stateMachine.AddState(state);
            s.motion = c;
            s.writeDefaultValues = true;
            return s;
        }

        /// <summary>First clip that exists out of the candidates.</summary>
        static string Pick(params string[] candidates)
        {
            foreach (var c in candidates)
                if (File.Exists($"{AnimRoot}/{c}.fbx")) return c;
            return candidates[0];
        }

        static AnimatorController BuildNpcController(string name, string g)
        {
            var ctrl = NewController(name);
            var idle = State(ctrl, "Idle", $"{g}_idle_neutral_01");
            State(ctrl, "Walk", $"{g}_walk_neutral_01");
            State(ctrl, "Run", $"{g}_run_slow_01");
            State(ctrl, "Sit", $"{g}_sit_chair_idle_neutral_01");
            State(ctrl, "SitSwing", $"{g}_sit_chair_idle_neutral_01");
            State(ctrl, "Laugh", g == "f" ? "f_gestic_laugh_low" : "m_cheer_01");
            State(ctrl, "Cheer", $"{g}_cheer_01");
            State(ctrl, "Clap", "f_claphands_01");
            State(ctrl, "Wave", $"{g}_wave_01");
            State(ctrl, "Talk", g == "f" ? "f_gestic_talk_excited_01" : "m_gestic_talk_neutral_01");
            State(ctrl, "Listen", g == "f" ? "f_gestic_listen_neutral_01" : "m_gestic_listen_relaxed_01");
            State(ctrl, "LookAround", g == "f" ? "f_idle_look_around_01" : "m_idle_breathe_01");
            // Indoor worlds. Where a clip only exists for one gender, the closest available clip stands in.
            State(ctrl, "SitTable", Pick($"{g}_sit_table_idle_neutral_01"));
            State(ctrl, "SitTableRelaxed", Pick($"{g}_sit_table_idle_relaxed_01", $"{g}_sit_table_idle_neutral_01"));
            State(ctrl, "SitTableThink", Pick($"{g}_sit_table_gestic_thoughtful", $"{g}_sit_table_idle_neutral_01"));
            State(ctrl, "TalkSad", Pick($"{g}_gestic_talk_sad_01"));
            State(ctrl, "ListenSad", Pick($"{g}_gestic_listen_sad_01", $"{g}_gestic_talk_sad_01"));
            State(ctrl, "Nervous", Pick($"{g}_idle_nervous_01", $"{g}_idle_waiting_01"));
            State(ctrl, "TalkNervous", Pick($"{g}_gestic_talk_nervous_01", $"{g}_gestic_talk_neutral_01"));
            State(ctrl, "Angry", Pick($"{g}_idle_angry_01", $"{g}_idle_waiting_01"));
            State(ctrl, "TalkAngry", Pick($"{g}_idle_angry_02", $"{g}_gestic_talk_excited_01"));
            State(ctrl, "Teach", Pick($"{g}_gestic_talk_neutral_02", $"{g}_gestic_talk_neutral_01"));
            State(ctrl, "Nod", Pick($"{g}_gestic_listen_accept_01"));
            State(ctrl, "Trolley", Pick($"{g}_trolley_idle"));
            State(ctrl, "HoldBag", Pick($"{g}_hold_bag_idle_01", $"{g}_idle_waiting_01"));
            State(ctrl, "Waiting", Pick($"{g}_idle_waiting_01"));
            State(ctrl, "Documents", Pick($"{g}_documents_idle", $"{g}_idle_waiting_01"));
            // Transitions: stepping turns (played while the body is rotated in code) and sitting down / standing up.
            State(ctrl, "TurnLeft", Pick($"{g}_turn_left_90"));
            State(ctrl, "TurnRight", Pick($"{g}_turn_right_90"));
            State(ctrl, "SitDown", Pick($"{g}_sit_down_chair_01"));
            State(ctrl, "StandUp", Pick($"{g}_sit_stand_up_chair_01"));
            ctrl.layers[0].stateMachine.defaultState = idle;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void BuildPlayerController()
        {
            foreach (var (name, g) in new[] { ("Player_Child", "m"), ("Player_Female", "f") })
            {
                var ctrl = NewController(name);
                ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
                var state = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
                tree.blendType = BlendTreeType.Simple1D;
                tree.blendParameter = "Speed";
                tree.useAutomaticThresholds = false;
                tree.AddChild(Clip($"{g}_idle_neutral_01"), 0f);
                tree.AddChild(Clip($"{g}_walk_neutral_01"), 1.25f);
                tree.AddChild(Clip($"{g}_run_slow_01"), 2.6f);
                ctrl.layers[0].stateMachine.defaultState = state;
                State(ctrl, "Wave", $"{g}_wave_01");
                EditorUtility.SetDirty(ctrl);
            }
        }

        /// <summary>
        /// Anime characters from VRoid/VRM files (imported by UniVRM as VRM 1.0 with URP MToon10): a NUMU prefab
        /// with the NPC animator, VRoid face presets and character lighting. The imported model stays untouched.
        /// </summary>
        static int SetupVrmCharacters(AnimatorController female, AnimatorController male)
        {
            if (!Directory.Exists(VroidRoot)) return 0;
            int n = 0;
            foreach (var dir in Directory.GetDirectories(VroidRoot))
            {
                string id = Path.GetFileName(dir);
                string path = $"{VroidRoot}/{id}/{id}.vrm";
                var imp = AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                // VRoid exports VRM 0.x: migrate to VRM 1.0 on import, which brings URP MToon10 materials.
                var so = new SerializedObject(imp);
                var migrate = so.FindProperty("MigrateToVrm1");
                var rp = so.FindProperty("RenderPipeline");
                bool changed = migrate != null && !migrate.boolValue;
                if (migrate != null) migrate.boolValue = true;
                if (rp != null && rp.enumNames.Length > 2 && rp.enumValueIndex != System.Array.IndexOf(rp.enumNames, "UniversalRenderPipeline"))
                {
                    rp.enumValueIndex = System.Array.IndexOf(rp.enumNames, "UniversalRenderPipeline");
                    changed = true;
                }
                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    imp.SaveAndReimport();
                }
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = $"Char_VRM_{id}";
                var anim = inst.GetComponent<Animator>();
                anim.runtimeAnimatorController = VrmFemale.TryGetValue(id, out var f) && f ? female : male;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                string meshAsset = $"{Dir}/VRM_{id}_Meshes.asset";
                AssetDatabase.DeleteAsset(meshAsset);
                foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.updateWhenOffscreen = false;
                    r.skinnedMotionVectors = false;
                    MergeSubmeshesByMaterial(r, meshAsset);
                }
                var smr = inst.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(s => s.sharedMesh.blendShapeCount).First();
                var face = inst.AddComponent<NumuFacialExpressions>();
                face.face = smr;
                ConfigureFace(face);
                inst.AddComponent<EmotionController>().face = face;
                PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath($"VRM_{id}"));
                Object.DestroyImmediate(inst);
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ avatars

        static void SetupAvatar(string name, AnimatorController controller)
        {
            string fbx = $"{AvatarRoot}/{name}/{name}_facial.fbx";
            string tex = $"{AvatarRoot}/{name}/Textures";
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            var slots = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().Select(m => m.name).Distinct().ToList();
            bool remapped = false;
            foreach (var slot in slots)
            {
                // Slot names look like "cf001_body", "f001_head", "m003_opacity".
                int us = slot.LastIndexOf('_');
                string prefix = slot.Substring(0, us), part = slot.Substring(us + 1);
                bool cards = part == "opacity";
                var mat = NumuWorldMaterials.Lit($"M_{name}_{part}", new NumuWorldMaterials.Spec
                {
                    baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tex}/{prefix}_{part}_color.png"),
                    normal = cards ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{tex}/{prefix}_{part}_normal.png"),
                    maskMap = cards ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{tex}/{prefix}_{part}_mask.png"),
                    smoothness = cards ? 0.3f : 0.95f,
                    alphaClip = cards,
                    cutoff = 0.5f,
                    doubleSided = cards,
                });
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), slot);
                if (!importer.GetExternalObjectMap().TryGetValue(id, out var existing) || existing != mat)
                {
                    importer.AddRemap(id, mat);
                    remapped = true;
                }
            }
            if (remapped) importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = $"Char_{name}";
            var anim = inst.GetComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var smr = inst.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(s => s.sharedMesh.blendShapeCount).First();
            smr.updateWhenOffscreen = false;
            smr.skinnedMotionVectors = false;
            var face = inst.AddComponent<NumuFacialExpressions>();
            face.face = smr;
            ConfigureFace(face);
            inst.AddComponent<EmotionController>().face = face;
            PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath(name));
            Object.DestroyImmediate(inst);
        }

        /// <summary>
        /// NUMU's serious-game character style (reference: JeStiMulE): the Rocketbox boy with repainted, flattened
        /// textures (Tools/AssetPipeline/stylize_character.py), matte materials without normal maps for soft, simple
        /// shading, a slightly longer and narrower head, and a calm, slightly shy resting face. Same humanoid rig,
        /// animations and 175 facial blendshapes as the original.
        /// </summary>
        static void SetupStylizedBoy(AnimatorController controller)
        {
            const string texDir = "Assets/NUMU/Characters/Stylized/Textures";
            var headTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/numu_boy_head.png");
            var bodyTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/numu_boy_body.png");
            var source = Prefab("Male_Child_01");
            if (headTex == null || bodyTex == null || source == null) return;
            var head = NumuWorldMaterials.Lit("M_NUMU_Boy_Head", new NumuWorldMaterials.Spec { baseMap = headTex, smoothness = 0.18f });
            var body = NumuWorldMaterials.Lit("M_NUMU_Boy_Body", new NumuWorldMaterials.Spec { baseMap = bodyTex, smoothness = 0.08f });
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            inst.name = $"Char_{StylizedBoy}";
            foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (mats[i].name.EndsWith("_head")) mats[i] = head;
                    else if (mats[i].name.EndsWith("_body")) mats[i] = body;
                }
                r.sharedMaterials = mats;
            }
            var anim = inst.GetComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            // A slightly longer, narrower face, as in the reference (humanoid animation never writes bone scale).
            var headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null) headBone.localScale = new Vector3(0.93f, 1.1f, 0.96f);
            var face = inst.GetComponent<NumuFacialExpressions>();
            // Resting look: eyes a little wider, a soft glance down, inner brows slightly raised.
            face.baseShapes = new List<NumuFacialExpressions.ShapeWeight>
            {
                W("AK_21_EyeWideLeft", 0.45f), W("AK_22_EyeWideRight", 0.45f), W("AK_11_EyeLookDownLeft", 0.12f), W("AK_12_EyeLookDownRight", 0.12f),
                W("AK_03_BrowInnerUp", 0.18f),
            };
            PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath(StylizedBoy));
            Object.DestroyImmediate(inst);
        }

        /// <summary>
        /// VRoid exports one sub-mesh per material use (the sample girl has 67 slots for 17 materials), and every
        /// slot is a draw call per pass. Sub-meshes that share a material are merged into one; blendshapes, bones
        /// and material order are kept. The merged mesh is saved next to the prefab; the imported VRM is untouched.
        /// </summary>
        static void MergeSubmeshesByMaterial(SkinnedMeshRenderer r, string assetPath)
        {
            var src = r.sharedMesh;
            var mats = r.sharedMaterials;
            if (src == null || mats.Length < 2 || src.subMeshCount != mats.Length) return;
            var unique = mats.Distinct().ToList();
            if (unique.Count == mats.Length) return;
            var mesh = Object.Instantiate(src);
            mesh.name = src.name + "_merged";
            if (mesh.vertexCount > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var groups = unique.Select(m => Enumerable.Range(0, mats.Length).Where(i => mats[i] == m).SelectMany(i => src.GetTriangles(i)).ToArray()).ToList();
            mesh.subMeshCount = unique.Count;
            for (int i = 0; i < groups.Count; i++) mesh.SetTriangles(groups[i], i, false);
            mesh.RecalculateBounds();
            if (!File.Exists(assetPath)) AssetDatabase.CreateAsset(mesh, assetPath);
            else AssetDatabase.AddObjectToAsset(mesh, assetPath);
            r.sharedMesh = mesh;
            r.sharedMaterials = unique.ToArray();
        }

        /// <summary>
        /// Anime faces (VRoid "Fcl_" shapes): each emotion combines its brow, eye and mouth shapes, so the whole face
        /// reads, not just the mouth. VRoid has no fear shape; fear is composed from worried brows, wide eyes and a
        /// tense, slightly open mouth (verified on the preview sheet).
        /// </summary>
        static void ConfigureVroidFace(NumuFacialExpressions face)
        {
            face.presets = new List<NumuFacialExpressions.ExpressionPreset>
            {
                new NumuFacialExpressions.ExpressionPreset { emotion = NumuEmotion.Neutral },
                new NumuFacialExpressions.ExpressionPreset
                {
                    emotion = NumuEmotion.Happy,
                    shapes = { W("Fcl_BRW_Fun", 0.8f), W("Fcl_EYE_Joy", 0.55f), W("Fcl_MTH_Joy", 0.85f) },
                },
                new NumuFacialExpressions.ExpressionPreset
                {
                    emotion = NumuEmotion.Sad,
                    // Droopy eyes and corners pulled down: clearly not the wide-eyed fear face.
                    shapes = { W("Fcl_BRW_Sorrow", 1f), W("Fcl_EYE_Sorrow", 1f), W("Fcl_MTH_Sorrow", 1f), W("Fcl_MTH_Down", 0.4f) },
                },
                new NumuFacialExpressions.ExpressionPreset
                {
                    emotion = NumuEmotion.Angry,
                    shapes = { W("Fcl_BRW_Angry", 1f), W("Fcl_EYE_Angry", 0.8f), W("Fcl_MTH_Angry", 0.9f) },
                },
                new NumuFacialExpressions.ExpressionPreset
                {
                    emotion = NumuEmotion.Fear,
                    // Worried brows with wide-open eyes and a tense, open mouth.
                    shapes = { W("Fcl_BRW_Sorrow", 1f), W("Fcl_BRW_Surprised", 0.3f), W("Fcl_EYE_Surprised", 1f), W("Fcl_MTH_Surprised", 0.35f), W("Fcl_MTH_E", 0.4f) },
                },
                new NumuFacialExpressions.ExpressionPreset
                {
                    emotion = NumuEmotion.Surprise,
                    shapes = { W("Fcl_BRW_Surprised", 1f), W("Fcl_EYE_Surprised", 1f), W("Fcl_MTH_Surprised", 0.9f) },
                },
                new NumuFacialExpressions.ExpressionPreset
                {
                    // VRoid has no disgust shapes: lowered brows, narrowed eyes, a pressed, downturned mouth.
                    emotion = NumuEmotion.Disgust,
                    shapes = { W("Fcl_BRW_Angry", 0.6f), W("Fcl_EYE_Angry", 0.5f), W("Fcl_MTH_Angry", 0.45f), W("Fcl_MTH_Sorrow", 0.4f), W("Fcl_MTH_E", 0.3f) },
                },
            };
            face.blinkShapes = new List<string> { "Fcl_EYE_Close" };
            face.talkShapes = new List<NumuFacialExpressions.ShapeWeight> { W("Fcl_MTH_A", 0.55f) };
        }

        static NumuFacialExpressions.ShapeWeight W(string shape, float w) => new NumuFacialExpressions.ShapeWeight { shape = shape, weight = w };

        /// <summary>
        /// Six readable expressions from the ARKit blendshape set. Each one combines the cues children are
        /// taught to look for (mouth shape + brows + eyes), at moderate strength so faces stay natural.
        /// </summary>
        public static void ConfigureFace(NumuFacialExpressions face)
        {
            var mesh = face.face != null ? face.face.sharedMesh : null;
            if (mesh != null && Enumerable.Range(0, mesh.blendShapeCount).Any(i => mesh.GetBlendShapeName(i).Contains("Fcl_")))
            {
                ConfigureVroidFace(face);
                return;
            }
            face.presets = new List<NumuFacialExpressions.ExpressionPreset>
            {
                new NumuFacialExpressions.ExpressionPreset { emotion = NumuEmotion.Neutral },
                new NumuFacialExpressions.ExpressionPreset
                {
                    // Duchenne smile: lip corners up + cheeks raised + slightly narrowed eyes.
                    emotion = NumuEmotion.Happy,
                    shapes =
                    {
                        W("AK_44_MouthSmileLeft", 1f), W("AK_45_MouthSmileRight", 1f), W("SR_34_Mouth_Smile_Left", 0.75f), W("SR_35_Mouth_Smile_Right", 0.75f),
                        W("AU_12_LipCornerPuller", 0.55f), W("AK_07_CheekSquintLeft", 0.6f), W("AK_08_CheekSquintRight", 0.6f), W("AU_06_CheekRaiser", 0.55f),
                        W("AK_19_EyeSquintLeft", 0.3f), W("AK_20_EyeSquintRight", 0.3f), W("AK_25_JawOpen", 0.14f), W("AK_28_MouthDimpleLeft", 0.3f), W("AK_29_MouthDimpleRight", 0.3f),
                    },
                },
                new NumuFacialExpressions.ExpressionPreset
                {
                    // Inner brows raised, lip corners pulled down, gaze lowered.
                    emotion = NumuEmotion.Sad,
                    shapes =
                    {
                        W("AK_03_BrowInnerUp", 1f), W("AU_01_InnerBrowRaiser", 0.6f), W("AK_30_MouthFrownLeft", 1f), W("AK_31_MouthFrownRight", 1f),
                        W("AU_15_LipCornerDepressor", 0.6f), W("SR_32_Mouth_Sad_Left", 0.6f), W("SR_33_Mouth_Sad_Right", 0.6f), W("AK_42_MouthShrugLower", 0.4f),
                        W("AU_17_ChinRaiser", 0.3f), W("AK_11_EyeLookDownLeft", 0.25f), W("AK_12_EyeLookDownRight", 0.25f), W("AU_41_LidDroop", 0.3f),
                    },
                },
                new NumuFacialExpressions.ExpressionPreset
                {
                    // Brows pulled down and together, narrowed eyes, pressed lips.
                    emotion = NumuEmotion.Angry,
                    shapes =
                    {
                        W("AK_01_BrowDownLeft", 0.9f), W("AK_02_BrowDownRight", 0.9f), W("AU_04_BrowLowerer", 0.35f), W("SR_04_Eye_Frown", 0.3f),
                        W("AK_50_NoseSneerLeft", 0.6f), W("AK_51_NoseSneerRight", 0.6f), W("AU_09_NoseWrinkler", 0.3f), W("AK_19_EyeSquintLeft", 0.4f), W("AK_20_EyeSquintRight", 0.4f),
                        W("AU_23_LipTightener", 0.5f), W("AK_36_MouthPressLeft", 0.5f), W("AK_37_MouthPressRight", 0.5f), W("AK_30_MouthFrownLeft", 0.4f), W("AK_31_MouthFrownRight", 0.4f),
                    },
                },
                new NumuFacialExpressions.ExpressionPreset
                {
                    // Brows raised and drawn together, eyes wide, lips stretched sideways.
                    emotion = NumuEmotion.Fear,
                    shapes =
                    {
                        W("AK_03_BrowInnerUp", 1f), W("AU_01_InnerBrowRaiser", 0.6f), W("AK_04_BrowOuterUpLeft", 0.6f), W("AK_05_BrowOuterUpRight", 0.6f),
                        W("AU_02_OuterBrowRaiser", 0.4f), W("AK_21_EyeWideLeft", 1f), W("AK_22_EyeWideRight", 1f), W("AU_05_UpperLidRaiser", 0.5f),
                        W("AK_46_MouthStretchLeft", 0.8f), W("AK_47_MouthStretchRight", 0.8f), W("AU_20_LipStretcher", 0.5f), W("AK_25_JawOpen", 0.25f),
                    },
                },
                new NumuFacialExpressions.ExpressionPreset
                {
                    // Wrinkled nose, raised upper lip, lowered lip corners, narrowed eyes and lowered brows.
                    emotion = NumuEmotion.Disgust,
                    shapes =
                    {
                        W("AK_50_NoseSneerLeft", 1f), W("AK_51_NoseSneerRight", 1f), W("AU_09_NoseWrinkler", 0.7f), W("AU_10_UpperLipRaiser", 0.8f),
                        W("AK_48_MouthUpperUpLeft", 0.6f), W("AK_49_MouthUpperUpRight", 0.6f), W("AK_30_MouthFrownLeft", 0.5f), W("AK_31_MouthFrownRight", 0.5f),
                        W("AU_15_LipCornerDepressor", 0.4f), W("AK_01_BrowDownLeft", 0.45f), W("AK_02_BrowDownRight", 0.45f),
                        W("AK_19_EyeSquintLeft", 0.5f), W("AK_20_EyeSquintRight", 0.5f), W("AU_17_ChinRaiser", 0.3f),
                    },
                },
                new NumuFacialExpressions.ExpressionPreset
                {
                    // Whole brow raised, eyes wide open, jaw dropped.
                    emotion = NumuEmotion.Surprise,
                    shapes =
                    {
                        W("AK_04_BrowOuterUpLeft", 1f), W("AK_05_BrowOuterUpRight", 1f), W("AU_02_OuterBrowRaiser", 0.6f), W("AK_03_BrowInnerUp", 0.7f),
                        W("AK_21_EyeWideLeft", 1f), W("AK_22_EyeWideRight", 1f), W("AU_05_UpperLidRaiser", 0.5f), W("AK_25_JawOpen", 0.55f),
                        W("AU_26_JawDrop", 0.4f), W("AK_32_MouthFunnel", 0.3f),
                    },
                },
            };
            face.blinkShapes = new List<string> { "AK_09_EyeBlinkLeft", "AK_10_EyeBlinkRight" };
            face.talkShapes = new List<NumuFacialExpressions.ShapeWeight> { W("AK_25_JawOpen", 0.3f), W("AA_VI_10_aa", 0.45f) };
        }
    }
}
