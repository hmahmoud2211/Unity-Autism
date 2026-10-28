using System;
using System.Collections.Generic;
using UnityEngine;

namespace Numu.World
{
    public enum NumuEmotion { Neutral, Happy, Sad, Angry, Fear, Surprise, Disgust }

    /// <summary>
    /// Drives facial blendshapes for the six core emotions, with smooth transitions,
    /// natural blinking and a simple jaw flap while the character is talking.
    /// Presets reference blendshape names on the face mesh, so the same component works for every avatar.
    /// </summary>
    [ExecuteAlways]
    // After UniVRM's Vrm10Instance (11000), which rewrites expression blendshapes every frame: NUMU's face wins,
    // while the VRM runtime keeps driving spring bones (hair, clothes).
    [DefaultExecutionOrder(11100)]
    public class NumuFacialExpressions : MonoBehaviour
    {
        [Serializable]
        public class ShapeWeight
        {
            public string shape;
            [Range(0f, 1f)] public float weight = 1f;
        }

        [Serializable]
        public class ExpressionPreset
        {
            public NumuEmotion emotion;
            public List<ShapeWeight> shapes = new List<ShapeWeight>();
        }

        public SkinnedMeshRenderer face;
        public List<ExpressionPreset> presets = new List<ExpressionPreset>();
        public NumuEmotion emotion = NumuEmotion.Neutral;
        [Range(0f, 1f)] public float intensity = 1f;
        [Tooltip("Seconds to blend between expressions.")]
        public float transitionTime = 0.35f;

        [Header("Blink")]
        [Tooltip("Resting face applied under every expression (a character's own look, e.g. slightly wider eyes, a shy glance).")]
        public List<ShapeWeight> baseShapes = new List<ShapeWeight>();
        public bool autoBlink = true;
        public List<string> blinkShapes = new List<string>();
        public Vector2 blinkInterval = new Vector2(2.2f, 5.5f);

        [Header("Talking")]
        public bool talking;
        public List<ShapeWeight> talkShapes = new List<ShapeWeight>();

        int[] _presetIndex = Array.Empty<int>();
        float[] _current = Array.Empty<float>();
        float[] _target = Array.Empty<float>();
        readonly Dictionary<string, int> _lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Mesh _cachedMesh;
        float _nextBlink, _blinkT = -1f;

        public event Action<NumuEmotion> ExpressionChanged;

        public void SetExpression(NumuEmotion e, float strength = 1f)
        {
            emotion = e;
            intensity = Mathf.Clamp01(strength);
            ExpressionChanged?.Invoke(e);
        }

        void OnEnable()
        {
            Rebuild();
            _nextBlink = Time.time + UnityEngine.Random.Range(blinkInterval.x, blinkInterval.y);
        }

        void Rebuild()
        {
            _lookup.Clear();
            _cachedMesh = face != null ? face.sharedMesh : null;
            if (_cachedMesh == null) return;
            int n = _cachedMesh.blendShapeCount;
            for (int i = 0; i < n; i++) _lookup[_cachedMesh.GetBlendShapeName(i)] = i;
            if (_current.Length != n)
            {
                _current = new float[n];
                _target = new float[n];
            }
        }

        int Index(string shape)
        {
            if (string.IsNullOrEmpty(shape)) return -1;
            if (_lookup.TryGetValue(shape, out int i)) return i;
            // Allow presets to name a shape without its mesh prefix (e.g. "Blendshape.mouthSmile_L").
            foreach (var kv in _lookup)
                if (kv.Key.EndsWith("." + shape, StringComparison.OrdinalIgnoreCase) || kv.Key.EndsWith("_" + shape, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            // Rocketbox-style ARKit names ("AK_44_MouthSmileLeft") also match faces that use the plain ARKit
            // names ("mouthSmileLeft"), which most realistic character pipelines export.
            string arkit = ArkitName(shape);
            if (arkit != null)
                foreach (var kv in _lookup)
                    if (kv.Key.Equals(arkit, StringComparison.OrdinalIgnoreCase) || kv.Key.EndsWith("." + arkit, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return -1;
        }

        /// <summary>"AK_44_MouthSmileLeft" → "mouthSmileLeft"; null when the name isn't an ARKit preset name.</summary>
        static string ArkitName(string shape)
        {
            if (!shape.StartsWith("AK_", StringComparison.Ordinal) || shape.Length < 7) return null;
            int us = shape.IndexOf('_', 3);
            if (us < 0 || us + 1 >= shape.Length) return null;
            string n = shape.Substring(us + 1);
            return char.ToLowerInvariant(n[0]) + n.Substring(1);
        }

        /// <summary>How many of the six emotions can be shown with this face's blendshapes (for asset validation).</summary>
        public int SupportedEmotionCount()
        {
            Rebuild();
            int count = 0;
            foreach (var p in presets)
            {
                if (p.emotion == NumuEmotion.Neutral) { count++; continue; }
                int found = 0;
                foreach (var w in p.shapes) if (Index(w.shape) >= 0) found++;
                if (p.shapes.Count > 0 && found >= Mathf.CeilToInt(p.shapes.Count * 0.5f)) count++;
            }
            return count;
        }

        public bool HasShapes => _cachedMesh != null && _cachedMesh.blendShapeCount > 0;

        void LateUpdate()
        {
            if (face == null) return;
            if (face.sharedMesh != _cachedMesh) Rebuild();
            if (_current.Length == 0) return;

            Array.Clear(_target, 0, _target.Length);
            foreach (var p in presets)
            {
                if (p.emotion != emotion) continue;
                foreach (var s in p.shapes)
                {
                    int i = Index(s.shape);
                    if (i >= 0) _target[i] = Mathf.Max(_target[i], s.weight * intensity);
                }
            }

            foreach (var s in baseShapes)
            {
                int i = Index(s.shape);
                if (i >= 0) _target[i] = Mathf.Max(_target[i], s.weight);
            }

            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
            if (talking && Application.isPlaying)
            {
                // Two incommensurate sines give an irregular, speech-like jaw rhythm.
                float flap = Mathf.Clamp01(0.5f + 0.35f * Mathf.Sin(Time.time * 13.1f) + 0.25f * Mathf.Sin(Time.time * 7.3f));
                foreach (var s in talkShapes)
                {
                    int i = Index(s.shape);
                    if (i >= 0) _target[i] = Mathf.Max(_target[i], s.weight * flap);
                }
            }

            if (autoBlink && Application.isPlaying)
            {
                if (_blinkT < 0f && Time.time >= _nextBlink) _blinkT = 0f;
                if (_blinkT >= 0f)
                {
                    _blinkT += dt;
                    float b = _blinkT < 0.07f ? _blinkT / 0.07f : 1f - (_blinkT - 0.07f) / 0.1f;
                    if (_blinkT > 0.17f)
                    {
                        _blinkT = -1f;
                        _nextBlink = Time.time + UnityEngine.Random.Range(blinkInterval.x, blinkInterval.y);
                        b = 0f;
                    }
                    foreach (var s in blinkShapes)
                    {
                        int i = Index(s);
                        if (i >= 0) _target[i] = Mathf.Max(_target[i], Mathf.Clamp01(b));
                    }
                }
            }

            float k = transitionTime <= 0f ? 1f : 1f - Mathf.Exp(-dt / (transitionTime * 0.35f));
            float kFast = 1f - Mathf.Exp(-dt / 0.02f);
            for (int i = 0; i < _current.Length; i++)
            {
                // Blinks need to be fast regardless of the expression blend time.
                bool blink = false;
                foreach (var s in blinkShapes) if (Index(s) == i) { blink = true; break; }
                _current[i] = Mathf.Lerp(_current[i], _target[i], blink ? kFast : k);
                face.SetBlendShapeWeight(i, _current[i] * 100f);
            }
        }

        /// <summary>Snaps straight to the target expression (used for previews and screenshots).</summary>
        public void ApplyImmediately()
        {
            Rebuild();
            if (_current.Length == 0) return;
            Array.Clear(_current, 0, _current.Length);
            foreach (var p in presets)
            {
                if (p.emotion != emotion) continue;
                foreach (var s in p.shapes)
                {
                    int i = Index(s.shape);
                    if (i >= 0) _current[i] = Mathf.Max(_current[i], s.weight * intensity);
                }
            }
            for (int i = 0; i < _current.Length; i++) face.SetBlendShapeWeight(i, _current[i] * 100f);
        }
    }
}
