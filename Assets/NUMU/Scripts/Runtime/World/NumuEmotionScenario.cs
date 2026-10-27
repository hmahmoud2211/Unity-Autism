using System.Collections;
using UnityEngine;

namespace Numu.World
{
    /// <summary>
    /// Emotion-recognition activity: the child walks up to a character, sees a short natural moment,
    /// is asked how the character feels, gets supportive feedback, and returns to free play.
    /// Every answer is recorded with correctness and response time.
    /// </summary>
    public class NumuEmotionScenario : MonoBehaviour
    {
        [Header("Content")]
        public string scenarioId = "park_swing_happy";
        public string world = "park";
        public string characterName = "Mia";
        public NumuEmotion correctEmotion = NumuEmotion.Happy;
        [TextArea] public string[] lines = { "Wheee! Hi there!", "I love swinging up high. It feels like flying!" };
        public string questionFormat = "How is {0} feeling?";
        [Tooltip("Map island/level this activity counts towards (Park › Identify emotions).")]
        public string progressIsland = "park";
        public int progressLevel = 4;

        [Header("Scene references")]
        public NumuInteractable interactable;
        public NumuNpc npc;
        public NumuFacialExpressions face;
        public NumuSwing swing;
        public Transform faceAnchor;
        public NumuPlayerController player;
        public NumuFollowCamera followCamera;
        public NumuInteractionSystem interactions;
        public NumuWorldUI ui;

        [Header("Animation states")]
        public string talkState = "Laugh";
        public string happyReaction = "Cheer";
        public string idleState = "SitSwing";

        int _attempt;
        bool _running;
        float _baseAmplitude;

        void Start()
        {
            interactable.Interacted += _ => { if (!_running) StartCoroutine(Run()); };
            face.SetExpression(correctEmotion, 0.9f);
            if (swing != null) _baseAmplitude = swing.TargetAmplitude;
            if (NumuSceneRouter.PendingScenario == scenarioId)
                ui.Toast($"Find {characterName} on the swings and say hi!", 4.5f);
        }

        IEnumerator Run()
        {
            _running = true;
            _attempt = 0;
            interactions.Suspended = true;
            player.InputEnabled = false;
            ui.SetHudVisible(false);
            player.Face(npc.transform.position);

            // Mia slows to a stop so her face is steady and easy to read, then the camera frames it.
            if (swing != null) swing.TargetAmplitude = 0f;
            yield return new WaitForSeconds(1.8f);
            Vector3 faceDir = Vector3.ProjectOnPlane(npc.transform.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, faceDir);
            // Slightly off-axis, at the child's eye level; looking a little below the face keeps it in the upper third.
            Vector3 camPos = faceAnchor.position + faceDir * 1.25f + side * 0.28f + Vector3.up * 0.02f;
            followCamera.SetShot(camPos, faceAnchor.position + Vector3.down * 0.32f);
            // During the conversation Mia looks at the viewer.
            var savedLook = npc.lookTarget;
            float savedOffset = npc.lookHeightOffset, savedWeight = npc.maxLookWeight;
            npc.lookTarget = followCamera.transform;
            npc.lookHeightOffset = 0f;
            npc.maxLookWeight = 0.85f;
            yield return new WaitForSeconds(1.0f);

            npc.Override(talkState);
            face.SetExpression(correctEmotion, 1f);
            foreach (var line in lines)
            {
                face.talking = true;
                yield return ui.Say(characterName, line);
                face.talking = false;
            }
            npc.Override(idleState);

            bool done = false;
            while (!done)
            {
                _attempt++;
                NumuEmotion picked = NumuEmotion.Neutral;
                long ms = 0;
                yield return ui.Ask(string.Format(questionFormat, characterName), (e, t) => { picked = e; ms = t; });
                bool correct = picked == correctEmotion;
                NumuAssessmentStore.Record(new NumuAssessmentResult
                {
                    world = world,
                    scenarioId = scenarioId,
                    question = string.Format(questionFormat, characterName),
                    correctAnswer = correctEmotion.ToString(),
                    selectedAnswer = picked.ToString(),
                    isCorrect = correct,
                    attempt = _attempt,
                    responseTimeMs = ms,
                });

                bool retry = false;
                if (correct)
                {
                    Numu.NumuAudio.Play(Numu.NumuSfx.Success);
                    npc.Override(happyReaction);
                    yield return ui.Feedback(true, "Yes, that's right!",
                        $"{characterName} is happy. Look at her big smile and how she laughs!", false, r => retry = r);
                    if (Numu.NumuProgress.Get(progressIsland, progressLevel) == Numu.LevelState.Unlocked)
                        Numu.NumuProgress.Complete(progressIsland, progressLevel);
                    done = true;
                }
                else
                {
                    Numu.NumuAudio.Play(Numu.NumuSfx.Tap);
                    yield return ui.Feedback(false, "Good try!",
                        $"Let's look again. {characterName} has a big smile and she is laughing. What does a smile tell us?", true, r => retry = r);
                    // "Continue" after a wrong answer still ends calmly; "Try again" asks once more.
                    done = !retry;
                }
            }

            npc.Override(idleState);
            npc.lookTarget = savedLook;
            npc.lookHeightOffset = savedOffset;
            npc.maxLookWeight = savedWeight;
            followCamera.ClearShot();
            if (swing != null) swing.TargetAmplitude = _baseAmplitude;
            yield return new WaitForSeconds(0.8f);
            player.InputEnabled = true;
            interactions.Suspended = false;
            ui.SetHudVisible(true);
            ui.Toast("Great job! Keep exploring the park, or tap Map to go back.", 4f);
            NumuBridge.SendToHost("scenarioFinished", $"{{\"scenario\":\"{scenarioId}\",\"attempts\":{_attempt}}}");
            npc.ReleaseOverride();
            _running = false;
        }
    }
}
