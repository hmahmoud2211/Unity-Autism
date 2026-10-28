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
        public static RuntimeAnimatorController PlayerController => AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{Dir}/Player_Child.controller");
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
            log?.AppendLine($"Characters: {avatars} Rocketbox avatars → prefabs with facial rigs; {clips} humanoid clips; 3 animator controllers.");
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
            var ctrl = NewController("Player_Child");
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var state = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("m_idle_neutral_01"), 0f);
            tree.AddChild(Clip("m_walk_neutral_01"), 1.25f);
            tree.AddChild(Clip("m_run_slow_01"), 2.6f);
            ctrl.layers[0].stateMachine.defaultState = state;
            State(ctrl, "Wave", "m_wave_01");
            EditorUtility.SetDirty(ctrl);
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
            PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath(name));
            Object.DestroyImmediate(inst);
        }

        static NumuFacialExpressions.ShapeWeight W(string shape, float w) => new NumuFacialExpressions.ShapeWeight { shape = shape, weight = w };

        /// <summary>
        /// Six readable expressions from the ARKit blendshape set. Each one combines the cues children are
        /// taught to look for (mouth shape + brows + eyes), at moderate strength so faces stay natural.
        /// </summary>
        public static void ConfigureFace(NumuFacialExpressions face)
        {
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
