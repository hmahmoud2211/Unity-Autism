using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Numu.World
{
    /// <summary>
    /// Data-driven social activity used by the Home, School and Supermarket worlds. The child walks up to a
    /// character and plays through a short script of steps: lines of dialogue, expression and animation
    /// changes, "How is … feeling?" questions and picture-choice "What could you do?" questions.
    /// Every answer is recorded with correctness and response time; wrong answers get a gentle hint and a retry.
    /// </summary>
    public class NumuConversationScenario : MonoBehaviour
    {
        public enum StepKind
        {
            Line,             // actor says text (actor -1 = the player child)
            Expression,       // actor's face changes to emotion at strength
            Animation,        // actor holds an animator state
            EmotionQuestion,  // "How is … feeling?" with the four emotion cards; correct = emotion
            ChoiceQuestion,   // picture-choice question; correct = correctOption
            Wait,
        }

        [Serializable]
        public class Actor
        {
            public string name;
            public NumuNpc npc;
            public NumuFacialExpressions face;
            public Transform faceAnchor;
            [Tooltip("Standing characters turn to face the child when the conversation starts.")]
            public bool turnToPlayer = true;
            [Tooltip("Camera distance in front of the face for this actor's shot.")]
            public float shotDistance = 1.3f;
        }

        [Serializable]
        public class Option
        {
            public string label;
            public Sprite icon;
            public Color color = Color.white;
        }

        [Serializable]
        public class Step
        {
            public StepKind kind;
            public int actor;
            [TextArea] public string text;
            public NumuEmotion emotion;
            public float strength = 1f;
            public string state;
            public List<Option> options = new List<Option>();
            public int correctOption;
            public string praiseTitle = "Yes, that's right!";
            [TextArea] public string praise;
            [TextArea] public string hint;
            public float seconds = 1f;
        }

        [Header("Content")]
        public string scenarioId;
        public string world;
        [Tooltip("Shown when the world opens for this activity.")]
        public string introToast;
        public string doneToast = "Great job! Keep exploring, or tap Map to go back.";
        public string playerName = "You";
        public List<Actor> actors = new List<Actor>();
        public List<Step> steps = new List<Step>();

        [Tooltip("Map island/level this activity counts towards when it was not launched from a specific level.")]
        public string progressIsland;
        public int progressLevel;

        [Header("Scene references")]
        public NumuInteractable interactable;
        public NumuPlayerController player;
        public NumuFollowCamera followCamera;
        public NumuInteractionSystem interactions;
        public NumuWorldUI ui;

        bool _running;
        int _shotActor = -2;

        void Start()
        {
            interactable.Interacted += _ => { if (!_running) StartCoroutine(Run()); };
            if (!string.IsNullOrEmpty(introToast) && NumuSceneRouter.PendingScenario == scenarioId)
                ui.Toast(introToast, 4.5f);
        }

        IEnumerator Run()
        {
            _running = true;
            _shotActor = -2;
            interactions.Suspended = true;
            player.InputEnabled = false;
            ui.SetHudVisible(false);
            var lead = actors[0];
            player.Face(lead.npc.transform.position);

            var saved = new List<(Transform look, float offset, float weight)>();
            foreach (var a in actors) saved.Add((a.npc.lookTarget, a.npc.lookHeightOffset, a.npc.maxLookWeight));
            yield return TurnActorsToPlayer();
            Frame(0);
            yield return new WaitForSeconds(1.0f);

            int questions = 0, solved = 0, attempts = 0;
            foreach (var step in steps)
            {
                var actor = step.actor >= 0 && step.actor < actors.Count ? actors[step.actor] : null;
                switch (step.kind)
                {
                    case StepKind.Line:
                        if (actor == null)
                        {
                            yield return ui.Say(playerName, step.text);
                            break;
                        }
                        if (_shotActor != step.actor)
                        {
                            Frame(step.actor);
                            yield return new WaitForSeconds(0.9f);
                        }
                        if (actor.face != null) actor.face.talking = true;
                        yield return ui.Say(actor.name, step.text);
                        if (actor.face != null) actor.face.talking = false;
                        break;

                    case StepKind.Expression:
                        if (actor?.face != null) actor.face.SetExpression(step.emotion, step.strength);
                        break;

                    case StepKind.Animation:
                        actor?.npc.Override(step.state);
                        break;

                    case StepKind.Wait:
                        yield return new WaitForSeconds(step.seconds);
                        break;

                    case StepKind.EmotionQuestion:
                    case StepKind.ChoiceQuestion:
                        questions++;
                        bool correctInTheEnd = false;
                        yield return AskUntilDone(step, ok => correctInTheEnd = ok, n => attempts += n);
                        if (correctInTheEnd) solved++;
                        break;
                }
            }

            for (int i = 0; i < actors.Count; i++)
            {
                actors[i].npc.lookTarget = saved[i].look;
                actors[i].npc.lookHeightOffset = saved[i].offset;
                actors[i].npc.maxLookWeight = saved[i].weight;
                actors[i].npc.ReleaseOverride();
            }
            followCamera.ClearShot();
            yield return new WaitForSeconds(0.8f);
            player.InputEnabled = true;
            interactions.Suspended = false;
            ui.SetHudVisible(true);
            ui.Toast(doneToast, 4.5f);

            // The activity counts on the map once every question ended with the right answer.
            if (questions > 0 && solved == questions) CompleteProgress();
            NumuBridge.SendToHost("scenarioFinished", $"{{\"scenario\":\"{scenarioId}\",\"attempts\":{attempts},\"questions\":{questions},\"solved\":{solved}}}");
            _running = false;
        }

        IEnumerator AskUntilDone(Step step, Action<bool> result, Action<int> countAttempts)
        {
            var actor = step.actor >= 0 && step.actor < actors.Count ? actors[step.actor] : null;
            if (actor != null && _shotActor != step.actor) Frame(step.actor);
            int attempt = 0;
            while (true)
            {
                attempt++;
                string picked;
                string correct;
                long ms = 0;
                if (step.kind == StepKind.EmotionQuestion)
                {
                    NumuEmotion e = NumuEmotion.Neutral;
                    yield return ui.Ask(step.text, (answer, t) => { e = answer; ms = t; });
                    picked = e.ToString();
                    correct = step.emotion.ToString();
                }
                else
                {
                    int index = -1;
                    var options = new List<NumuWorldUI.Option>();
                    foreach (var o in step.options) options.Add(new NumuWorldUI.Option { label = o.label, icon = o.icon, color = o.color });
                    yield return ui.AskChoice(step.text, options, (i, t) => { index = i; ms = t; });
                    picked = step.options[index].label;
                    correct = step.options[step.correctOption].label;
                }
                bool ok = picked == correct;
                NumuAssessmentStore.Record(new NumuAssessmentResult
                {
                    world = world,
                    scenarioId = scenarioId,
                    question = step.text,
                    correctAnswer = correct,
                    selectedAnswer = picked,
                    isCorrect = ok,
                    attempt = attempt,
                    responseTimeMs = ms,
                });

                bool retry = false;
                if (ok)
                {
                    Numu.NumuAudio.Play(Numu.NumuSfx.Success);
                    yield return ui.Feedback(true, step.praiseTitle, step.praise, false, r => retry = r);
                    countAttempts(attempt);
                    result(true);
                    yield break;
                }
                Numu.NumuAudio.Play(Numu.NumuSfx.Tap);
                yield return ui.Feedback(false, "Good try!", step.hint, true, r => retry = r);
                if (!retry)
                {
                    // "Continue" after a wrong answer moves on calmly; the next lines model the right response.
                    countAttempts(attempt);
                    result(false);
                    yield break;
                }
            }
        }

        /// <summary>Standing characters turn towards the child (seated ones only look).</summary>
        IEnumerator TurnActorsToPlayer()
        {
            var turning = new List<(Transform t, Quaternion from, Quaternion to)>();
            foreach (var a in actors)
            {
                Vector3 to = Vector3.ProjectOnPlane(player.transform.position - a.npc.transform.position, Vector3.up);
                if (!a.turnToPlayer || to.sqrMagnitude < 0.01f) continue;
                turning.Add((a.npc.transform, a.npc.transform.rotation, Quaternion.LookRotation(to.normalized)));
            }
            if (turning.Count == 0) yield break;
            float duration = Numu.NumuSettings.ReducedMotion ? 0f : 0.6f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                foreach (var (tr, from, to) in turning) tr.rotation = Quaternion.Slerp(from, to, k);
                yield return null;
            }
            foreach (var (tr, _, to) in turning) tr.rotation = to;
        }

        /// <summary>Frames an actor's face slightly off-axis at eye level; that actor looks at the viewer, the others at the child.</summary>
        void Frame(int index)
        {
            _shotActor = index;
            var a = actors[index];
            Vector3 faceDir = Vector3.ProjectOnPlane(a.npc.transform.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, faceDir);
            Vector3 camPos = a.faceAnchor.position + faceDir * a.shotDistance + side * 0.3f + Vector3.up * 0.04f;
            followCamera.SetShot(camPos, a.faceAnchor.position + Vector3.down * 0.3f);
            for (int i = 0; i < actors.Count; i++)
            {
                var npc = actors[i].npc;
                bool speaker = i == index;
                npc.lookTarget = speaker ? followCamera.transform : player.transform;
                npc.lookHeightOffset = speaker ? 0f : 1.0f;
                npc.maxLookWeight = speaker ? 0.85f : 0.6f;
            }
        }

        void CompleteProgress()
        {
            int level = NumuSceneRouter.PendingScenario == scenarioId && NumuSceneRouter.PendingLevel > 0 ? NumuSceneRouter.PendingLevel : progressLevel;
            string island = string.IsNullOrEmpty(progressIsland) ? world : progressIsland;
            if (level > 0 && Numu.NumuProgress.Get(island, level) == Numu.LevelState.Unlocked)
                Numu.NumuProgress.Complete(island, level);
        }
    }
}
