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
        class GameplayRefs
        {
            public NumuPlayerController player;
            public NumuFollowCamera followCamera;
            public NumuInteractionSystem interactions;
            public NumuConversationScenario scenario;
            public NumuInteractable friendInteraction;
        }

        static GameObject SpawnCharacter(string avatar, string displayName, Transform parent, Vector3 pos, float yaw)
        {
            var prefab = NumuCharacterSetup.Prefab(avatar);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = displayName;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.GetComponent<Animator>().runtimeAnimatorController = NumuCharacterSetup.NpcController(NumuCharacterSetup.IsFemale(avatar));
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.shadowCastingMode = ShadowCastingMode.On;
                smr.lightProbeUsage = LightProbeUsage.BlendProbes;
            }
            return go;
        }

        /// <summary>Moves a character so its pelvis (in the given seated clip) rests on a seat point.</summary>
        static void SeatOn(GameObject character, Vector3 seat, string sitClip, float backOffset)
        {
            var clip = NumuCharacterSetup.Clip(sitClip);
            var anim = character.GetComponent<Animator>();
            clip.SampleAnimation(character, 1f);
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 want = seat + Vector3.up * 0.1f - character.transform.forward * backOffset;
            character.transform.position += want - hips.position;
        }

        static NumuNpc MakeNpc(GameObject go, string baseState, Transform look, params string[] variations)
        {
            var npc = go.AddComponent<NumuNpc>();
            npc.displayName = go.name;
            npc.baseState = baseState;
            npc.lookTarget = look;
            npc.idleVariations = new List<string>(variations);
            npc.face = go.GetComponent<NumuFacialExpressions>();
            return npc;
        }

        static void Blocker(GameObject go, float radius, float height)
        {
            var c = go.AddComponent<CapsuleCollider>();
            c.radius = radius;
            c.height = height;
            c.center = new Vector3(0, height * 0.5f, 0);
        }

        static GameplayRefs BuildCharactersAndGameplay()
        {
            var refs = new GameplayRefs();
            var g = Group("Characters");

            // ---------------------------------------------------------------- player (third person)
            string avatar = NumuCharacterSetup.PlayerAvatar;
            var player = SpawnCharacter(avatar, "Player", g, OnGround(PlayerStart), 0f);
            player.GetComponent<Animator>().runtimeAnimatorController = NumuCharacterSetup.PlayerControllerFor(avatar);
            var cc = player.AddComponent<CharacterController>();
            float capsule = Mathf.Clamp(NumuWorldScaffold.Bounds(player).size.y * 0.95f, 1.2f, 1.75f);
            cc.height = capsule;
            cc.radius = 0.22f;
            cc.center = new Vector3(0, capsule * 0.5f + 0.02f, 0);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.03f;
            var pc = player.AddComponent<NumuPlayerController>();
            pc.animator = player.GetComponent<Animator>();
            refs.player = pc;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(_root, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            // Nothing in the park samples the depth texture: skipping it avoids a depth prepass on mobile.
            camData.requiresDepthOption = CameraOverrideOption.Off;
            camData.requiresColorOption = CameraOverrideOption.Off;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<NumuFollowCamera>();
            follow.target = player.transform;
            // Close behind-the-shoulder framing (as in JeStiMulE): the whole child stays in view.
            follow.distance = 3.8f;
            follow.height = 1.9f;
            follow.lookHeight = 1.05f;
            follow.obstacleMask = ~0 & ~(1 << 2);
            camGo.transform.position = player.transform.position + new Vector3(0, 2.3f, -4.8f);
            camGo.transform.LookAt(player.transform.position + Vector3.up * 1.05f);
            pc.cameraTransform = camGo.transform;
            refs.followCamera = follow;

            // ---------------------------------------------------------------- two friends at the swings (the activity)
            // Mia swings; Lina waits for her turn in front of the swings, and smiles and waves when the child comes near.
            var seatPoint = _root.Find("Playground/SwingSet/SeatPivot_0/SeatPoint");
            var otherSeat = _root.Find("Playground/SwingSet/SeatPivot_1/SeatPoint");
            var mia = SpawnCharacter("Female_Child_01", "Mia", seatPoint, seatPoint.position, 180f);
            mia.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
            SeatOn(mia, seatPoint.position, "f_sit_chair_idle_neutral_01", 0.02f);
            var miaNpc = MakeNpc(mia, "SitSwing", player.transform);
            miaNpc.maxLookWeight = 0.55f;
            // Hands grip the chains about shoulder height above the seat.
            foreach (var (side, name) in new[] { (-1f, "LeftHandChain"), (1f, "RightHandChain") })
            {
                var h = new GameObject(name).transform;
                h.SetParent(seatPoint.parent, false);
                // Mia faces -Z, so her left hand is on +X in the swing's space.
                h.localPosition = new Vector3(-side * 0.2f, seatPoint.localPosition.y + 0.42f, 0.03f);
                h.localRotation = Quaternion.Euler(0, 180, side * 90f);
                if (side < 0) miaNpc.leftHandTarget = h; else miaNpc.rightHandTarget = h;
            }
            mia.GetComponent<NumuFacialExpressions>().SetExpression(NumuEmotion.Happy, 0.9f);

            Vector3 seatGround = OnGround(new Vector3(seatPoint.position.x, 0, seatPoint.position.z));
            Vector3 towardOther = Vector3.ProjectOnPlane(otherSeat.position - seatPoint.position, Vector3.up).normalized;
            // Clear of Mia's swing arc (about 0.8 m forward of the seat at full swing).
            var lina = SpawnCharacter("Female_Child_02", "Lina", g, OnGround(seatGround + towardOther * 0.8f + Vector3.back * 1.7f), 180f);
            lina.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var linaNpc = MakeNpc(lina, "Idle", player.transform, "LookAround", "Clap");
            linaNpc.greetState = "Wave";
            linaNpc.greetRadius = 7f;
            lina.GetComponent<NumuFacialExpressions>().SetExpression(NumuEmotion.Happy, 0.6f);
            Blocker(lina, 0.25f, 1.4f);

            var linaHead = lina.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
            var interactionGo = new GameObject("Interaction_SayHiToFriends");
            interactionGo.transform.SetParent(g, false);
            interactionGo.transform.position = OnGround(lina.transform.position + Vector3.back * 1.6f);
            var interactable = interactionGo.AddComponent<NumuInteractable>();
            interactable.prompt = "Say hi to Lina";
            interactable.radius = 2.2f;
            interactable.focusPoint = linaHead;
            refs.friendInteraction = interactable;

            var scenarioGo = new GameObject("Scenario_ParkSwingHappy");
            scenarioGo.transform.SetParent(_root, false);
            var scenario = scenarioGo.AddComponent<NumuConversationScenario>();
            scenario.scenarioId = "park_swing_happy";
            scenario.world = "park";
            scenario.progressIsland = "park";
            scenario.progressLevel = 4;
            scenario.openWithGroupShot = true;
            scenario.introToast = "Lina and Mia are at the swings. Go and say hi!";
            scenario.doneToast = "Great job! Keep exploring the park, or tap Map to go back.";
            scenario.actors.Add(new NumuConversationScenario.Actor { name = "Lina", npc = linaNpc, face = lina.GetComponent<NumuFacialExpressions>(), faceAnchor = linaHead, turnToPlayer = true, shotDistance = 1.1f });
            scenario.actors.Add(new NumuConversationScenario.Actor { name = "Mia", npc = miaNpc, face = mia.GetComponent<NumuFacialExpressions>(), faceAnchor = mia.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head), turnToPlayer = false, shotDistance = 1.25f });
            scenario.interactable = interactable;
            scenario.player = pc;
            scenario.followCamera = follow;
            var L = NumuConversationScenario.StepKind.Line;
            var A = NumuConversationScenario.StepKind.Animation;
            var E = NumuConversationScenario.StepKind.Expression;
            scenario.steps.Add(new NumuConversationScenario.Step { kind = E, actor = 0, emotion = NumuEmotion.Happy, strength = 1f });
            scenario.steps.Add(new NumuConversationScenario.Step { kind = A, actor = 0, state = "Wave" });
            scenario.steps.Add(new NumuConversationScenario.Step { kind = NumuConversationScenario.StepKind.Wait, seconds = 1.6f });
            scenario.steps.Add(new NumuConversationScenario.Step { kind = A, actor = 0, state = "Talk" });
            scenario.steps.Add(new NumuConversationScenario.Step { kind = L, actor = 0, text = "Hi! I'm Lina. Mia and I are taking turns on the swing." });
            scenario.steps.Add(new NumuConversationScenario.Step { kind = L, actor = 0, text = "Do you want to play with us?" });
            scenario.steps.Add(new NumuConversationScenario.Step { kind = A, actor = 0, state = "Idle" });
            scenario.steps.Add(new NumuConversationScenario.Step
            {
                kind = NumuConversationScenario.StepKind.EmotionQuestion, actor = 0, text = "How is your friend feeling?", emotion = NumuEmotion.Happy,
                praise = "Lina is happy. She has a big smile and she waved hello to you.",
                hint = "Look at Lina's face: the corners of her mouth go up in a smile, and she waved at you. What does a smile tell us?",
            });
            scenario.steps.Add(new NumuConversationScenario.Step { kind = L, actor = -1, text = "Yes please! Let's take turns." });
            scenario.steps.Add(new NumuConversationScenario.Step { kind = A, actor = 0, state = "Clap" });
            scenario.steps.Add(new NumuConversationScenario.Step { kind = L, actor = 0, text = "Yay! You can go after Mia." });
            refs.scenario = scenario;

            // ---------------------------------------------------------------- ambient park life

            var omar = SpawnCharacter("Male_Child_02", "Omar", g, OnGround(new Vector3(-8f, 0, -3f)), 90f);
            var omarNpc = MakeNpc(omar, "Idle", player.transform);
            omarNpc.walkSpeed = 1.05f;
            omarNpc.walkState = "Walk";
            var wpRoot = Group("OmarRoute", g);
            foreach (var p in new[] { new Vector3(-9.5f, 0, -3.5f), new Vector3(-5f, 0, 1.5f), new Vector3(-2.5f, 0, -3.5f), new Vector3(-6.5f, 0, -7f) })
            {
                var wp = new GameObject("Waypoint").transform;
                wp.SetParent(wpRoot, false);
                wp.position = OnGround(p);
                omarNpc.waypoints.Add(wp);
            }
            omar.GetComponent<NumuFacialExpressions>().SetExpression(NumuEmotion.Happy, 0.4f);

            var bench = _root.Find("Props").GetChild(0);
            var mum = SpawnCharacter("Female_Adult_01", "Grown-up on the bench", g, bench.position, BenchFacingYaw(bench));
            SeatOn(mum, bench.position + Vector3.up * BenchSeatHeight(bench), "f_sit_chair_idle_neutral_01", -0.02f);
            MakeNpc(mum, "Sit", player.transform);
            mum.GetComponent<NumuFacialExpressions>().SetExpression(NumuEmotion.Happy, 0.35f);

            // Anime park visitor (VRoid); falls back to the Rocketbox adult if the VRM isn't imported.
            string helperAvatar = NumuCharacterSetup.UseAnimeCharacters && NumuCharacterSetup.Prefab("VRM_SakuradaFumiriya") != null ? "VRM_SakuradaFumiriya" : "Male_Adult_02";
            var helper = SpawnCharacter(helperAvatar, "Park helper", g, OnGround(new Vector3(4.2f, 0, 6.6f)), 70f);
            MakeNpc(helper, "Listen", player.transform, "LookAround", "Idle");
            helper.GetComponent<NumuFacialExpressions>().SetExpression(NumuEmotion.Neutral, 1f);
            Blocker(helper, 0.28f, 1.8f);

            var interactionsGo = new GameObject("InteractionSystem");
            interactionsGo.transform.SetParent(_root, false);
            var interactions = interactionsGo.AddComponent<NumuInteractionSystem>();
            interactions.player = player.transform;
            refs.interactions = interactions;
            scenario.interactions = interactions;

            _log.AppendLine("Characters: player child (third person), Mia on the swing and Lina waiting her turn (the activity), Omar strolling, a grown-up on the bench, a park helper.");
            return refs;
        }

        /// <summary>The Poly Haven bench's backrest side decides which way a sitter faces.</summary>
        static float BenchFacingYaw(Transform bench) => bench.eulerAngles.y + BenchFrontYawOffset;

        // The bench model's seat faces +Z in its own space (backrest on -Z).
        const float BenchFrontYawOffset = 0f;

        static float BenchSeatHeight(Transform bench) => 0.44f;
    }
}
