using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Numu.World;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Play Mode QA driver for the conversation activities. It walks the child to the activity, reads every line,
    /// answers each question wrongly once (to exercise the hint and "Try again"), then correctly, and captures
    /// Game View screenshots along the way. Progress is logged with a "[NUMU-TEST]" prefix; <see cref="Report"/>
    /// returns the transcript.
    /// </summary>
    public static class NumuScenarioTestDriver
    {
        static string _out;
        static double _next, _started;
        static int _phase, _shots, _questionIndex;
        static bool _answeredWrong, _lastWrong, _done;
        static string _lastQuestion;
        static readonly StringBuilder _log = new StringBuilder();
        static readonly HashSet<string> _captured = new HashSet<string>();

        public static string Report => _log.ToString();
        public static bool Done => _done;

        static bool _walkFirstPerson;
        static Vector3 _walkStart;

        /// <param name="walkFirstPerson">
        /// Walks in third person with the touch joystick, switches to first person, walks up to the character and
        /// checks that first person is still active after the conversation (instead of placing the child directly).
        /// </param>
        public static string Start(string outDir, bool walkFirstPerson = false)
        {
            _walkFirstPerson = walkFirstPerson;
            if (!Application.isPlaying) return "Not in Play Mode.";
            _out = outDir;
            Directory.CreateDirectory(outDir);
            _log.Clear();
            _captured.Clear();
            _phase = 0;
            _shots = 0;
            _questionIndex = 0;
            _answeredWrong = _lastWrong = _done = false;
            _lastQuestion = null;
            _started = EditorApplication.timeSinceStartup;
            _next = _started + 1.5;
            if (walkFirstPerson) _phase = 10;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return "started";
        }

        static void Log(string s)
        {
            string line = $"{EditorApplication.timeSinceStartup - _started,6:F1}s  {s}";
            _log.AppendLine(line);
            Debug.Log($"[NUMU-TEST] {s}");
        }

        static void Shot(string name)
        {
            if (!_captured.Add(name)) return;
            string path = Path.Combine(_out, $"{_shots++:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Log($"screenshot {Path.GetFileName(path)}");
        }

        static bool Visible(CanvasGroup g) => g != null && g.interactable && g.alpha > 0.95f;

        static void Tick()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.update -= Tick;
                return;
            }
            double now = EditorApplication.timeSinceStartup;
            if (now < _next) return;
            if (now - _started > 240) { Finish("TIMEOUT"); return; }

            // Works with the data-driven activities and with the Park's original swing activity.
            var scenario = Object.FindFirstObjectByType<NumuConversationScenario>();
            var park = scenario == null ? Object.FindFirstObjectByType<NumuEmotionScenario>() : null;
            var ui = Object.FindFirstObjectByType<NumuWorldUI>();
            if ((scenario == null && park == null) || ui == null) { Finish("no scenario/ui in scene"); return; }
            var player = scenario != null ? scenario.player : park.player;
            var interactable = scenario != null ? scenario.interactable : park.interactable;
            var interactions = scenario != null ? scenario.interactions : park.interactions;
            var followCamera = scenario != null ? scenario.followCamera : park.followCamera;
            var npc = scenario != null ? scenario.actors[0].npc.transform : park.npc.transform;

            var view = followCamera;
            switch (_phase)
            {
                case 10:
                    Shot("tp_start");
                    _walkStart = player.transform.position;
                    Stick(player, new Vector2(0f, 1f));
                    Log($"third person: {view.CurrentView}; walking forward with the touch joystick");
                    _phase = 11;
                    _next = now + 3.0;
                    return;
                case 11:
                    Shot("tp_walking");
                    var anim = player.GetComponent<Animator>();
                    Log($"walked {Vector3.Distance(_walkStart, player.transform.position):F2} m at {player.CurrentSpeed:F2} m/s; animator Locomotion: {anim.GetCurrentAnimatorStateInfo(0).IsName("Locomotion")}, camera distance {Vector3.Distance(view.transform.position, player.transform.position):F2} m");
                    Stick(player, Vector2.zero);
                    view.ToggleView();
                    Log($"pressed the view button → {view.CurrentView}");
                    _phase = 12;
                    _next = now + 1.5;
                    return;
                case 12:
                    bool hidden = System.Array.TrueForAll(player.GetComponentsInChildren<Renderer>(), r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly);
                    Log($"first person: camera {view.transform.position.y - player.transform.position.y:F2} m above the feet, body hidden (shadow only): {hidden}");
                    Shot("fp_view");
                    // Stand 5 m from the activity facing the character, then walk up in first person.
                    var cc0 = player.GetComponent<CharacterController>();
                    cc0.enabled = false;
                    Vector3 spot = interactable.transform.position;
                    Vector3 dir = Vector3.ProjectOnPlane(spot - npc.position, Vector3.up).normalized;
                    Vector3 from = spot + dir * 3.5f;
                    from.y = player.transform.position.y;
                    player.transform.SetPositionAndRotation(from, Quaternion.LookRotation(-dir));
                    cc0.enabled = true;
                    view.SnapBehindTarget();
                    Stick(player, new Vector2(0f, 0.55f));
                    _phase = 13;
                    _next = now + 0.3;
                    return;
                case 13:
                    if (Visible(interactions.promptGroup) || now - _started > 40)
                    {
                        Stick(player, Vector2.zero);
                        Log($"walked up in first person; prompt visible: {Visible(interactions.promptGroup)} at {Vector3.Distance(player.transform.position, npc.position):F2} m from the character");
                        Shot("fp_approach");
                        _phase = 1;
                        _next = now + 1.0;
                        return;
                    }
                    _next = now + 0.1;
                    return;
                case 0:
                    Shot("arrival");
                    // Walk-in is tested separately; here the child is placed beside the activity.
                    var cc = player.GetComponent<CharacterController>();
                    cc.enabled = false;
                    Vector3 target = interactable.transform.position;
                    Vector3 look = npc.position - target;
                    look.y = 0;
                    player.transform.SetPositionAndRotation(target, Quaternion.LookRotation(look.normalized));
                    cc.enabled = true;
                    followCamera.SnapBehindTarget();
                    Log($"player placed at {target}");
                    _phase = 1;
                    _next = now + 1.5;
                    return;
                case 1:
                    bool promptShown = Visible(interactions.promptGroup);
                    Log($"prompt visible: {promptShown} ('{interactions.promptLabel.text}')");
                    Shot("prompt");
                    interactions.Trigger();
                    _phase = 2;
                    _next = now + 0.5;
                    return;
            }

            // Conversation loop.
            if (Visible(ui.dialogueGroup))
            {
                string speaker = ui.speakerName.text, line = ui.dialogueLine.text;
                if (_captured.Add("line:" + line))
                {
                    Log($"line  {speaker}: {line}");
                    Shot($"line_{_captured.Count}");
                    _next = now + 1.2;
                    return;
                }
                ui.dialogueContinue.onClick.Invoke();
                _next = now + 0.6;
                return;
            }
            if (Visible(ui.questionGroup))
            {
                string q = ui.questionText.text;
                if (q != _lastQuestion) { _lastQuestion = q; _answeredWrong = false; _questionIndex++; }
                Shot($"question{_questionIndex}{(_answeredWrong ? "_retry" : "")}");
                int correct;
                if (scenario != null)
                {
                    var step = scenario.steps.Find(s => (s.kind == NumuConversationScenario.StepKind.EmotionQuestion || s.kind == NumuConversationScenario.StepKind.ChoiceQuestion) && s.text == q);
                    correct = step.kind == NumuConversationScenario.StepKind.ChoiceQuestion ? step.correctOption : ui.choices.FindIndex(c => c.emotion == step.emotion);
                }
                else correct = ui.choices.FindIndex(c => c.emotion == park.correctEmotion);
                int pick = _answeredWrong ? correct : (correct + 1) % 4;
                Log($"question '{q}' → picking '{ui.choices[pick].label.text}' ({(pick == correct ? "correct" : "wrong on purpose")})");
                _lastWrong = pick != correct;
                _answeredWrong = true;
                ui.choices[pick].button.onClick.Invoke();
                _next = now + 1.0;
                return;
            }
            if (Visible(ui.feedbackGroup))
            {
                Shot($"feedback{_questionIndex}{(_lastWrong ? "_gentle" : "_praise")}");
                Log($"feedback '{ui.feedbackTitle.text}': {ui.feedbackMessage.text}");
                if (_lastWrong && ui.feedbackRetry.gameObject.activeInHierarchy) { Log("→ Try again"); ui.feedbackRetry.onClick.Invoke(); }
                else { Log("→ Continue"); ui.feedbackContinue.onClick.Invoke(); }
                _next = now + 1.0;
                return;
            }
            if (_phase == 2 && player.InputEnabled && Visible(ui.hudGroup) && now - _started > 8)
            {
                if (_walkFirstPerson)
                {
                    bool hidden = System.Array.TrueForAll(player.GetComponentsInChildren<Renderer>(), r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly);
                    Log($"after the conversation: view {view.CurrentView}, first person active {view.IsFirstPerson}, body hidden {hidden}");
                    if (!view.IsFirstPerson) { Finish("FAIL (view not preserved)"); return; }
                }
                Shot("free_play");
                Log($"conversation finished; HUD back, input enabled. Toast: '{ui.toastText.text}'");
                Finish("PASS");
                return;
            }
            _next = now + 0.3;
        }

        static void Stick(NumuPlayerController player, Vector2 v) =>
            typeof(NumuVirtualJoystick).GetProperty("Value").SetValue(player.joystick, v);

        static void Finish(string result)
        {
            Log($"RESULT {result}");
            string file = Path.Combine(Application.persistentDataPath, "numu_assessment_results.json");
            if (File.Exists(file)) Log($"results file: {file} ({new FileInfo(file).Length} bytes)");
            _done = true;
            EditorApplication.update -= Tick;
        }
    }
}
