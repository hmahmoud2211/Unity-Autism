using TMPro;
using UnityEngine;

namespace Numu
{
    /// <summary>
    /// Numbered badge lying on an island path. Shows locked / unlocked / completed states and
    /// gently bobs when it is the next level to play.
    /// </summary>
    [ExecuteAlways]
    public class NumuLevelMarker : MonoBehaviour
    {
        public string islandId;
        public int level = 1;
        public string title;
        public Color themeColor = Color.white;

        public Renderer face;
        public TMP_Text number;
        public GameObject lockIcon;
        public GameObject starIcon;
        public Transform visual;

        static readonly Color LockedTint = new Color(0.78f, 0.78f, 0.82f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock _mpb;
        Vector3 _restPos;
        float _phase;

        public LevelState State => NumuProgress.Get(islandId, level);

        void OnEnable()
        {
            if (visual != null) _restPos = visual.localPosition;
            _phase = level * 0.7f + (islandId ?? "").Length;
            NumuProgress.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => NumuProgress.Changed -= Refresh;

        public void Refresh()
        {
            var s = State;
            if (face != null)
            {
                _mpb ??= new MaterialPropertyBlock();
                face.GetPropertyBlock(_mpb);
                // Locked badges keep their island colour, just softened, so the overview still reads like the map.
                _mpb.SetColor(BaseColorId, s == LevelState.Locked ? Color.Lerp(themeColor, LockedTint, 0.45f) : themeColor);
                face.SetPropertyBlock(_mpb);
            }
            if (number != null) number.gameObject.SetActive(true);
            if (lockIcon != null) lockIcon.SetActive(s == LevelState.Locked);
            if (starIcon != null) starIcon.SetActive(s == LevelState.Completed);
        }

        void Update()
        {
            if (visual == null || !Application.isPlaying) return;
            bool next = State == LevelState.Unlocked;
            float amp = NumuSettings.ReducedMotion || !next ? 0f : 0.12f;
            visual.localPosition = _restPos + Vector3.up * (Mathf.Sin(Time.time * 2.4f + _phase) * 0.5f + 0.5f) * amp;
        }
    }
}
