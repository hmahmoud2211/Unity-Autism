using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Numu
{
    /// <summary>
    /// Drives the world map: overview ⇄ island focus, level details, Back/Home navigation and
    /// comfort settings. All references are wired by the scene builder.
    /// </summary>
    public class NumuMapController : MonoBehaviour
    {
        public static NumuMapController Instance { get; private set; }

        [Serializable]
        public class IslandCard
        {
            public string islandId;
            public RectTransform root;
            public Button header;
            public Button[] levelRows;
            public Image[] levelStateIcons;
        }

        [Header("World")]
        public NumuCameraRig rig;
        public NumuIsland[] islands;

        [Header("Overview cards")]
        public CanvasGroup cardsGroup;
        public RectTransform cardsPanel;
        public IslandCard[] cards;
        public CanvasGroup overviewHintGroup;

        [Header("Focus bar")]
        public CanvasGroup focusBarGroup;
        public Image focusChip;
        public Image focusChipEdge;
        public Image focusIcon;
        public TMP_Text focusTitle;
        public TMP_Text focusSubtitle;
        public Button backButton;
        public Button homeButton;
        public Button settingsButton;

        [Header("Focus level strip")]
        public CanvasGroup stripGroup;
        public Button[] stripButtons;
        public Image[] stripPills;
        public TMP_Text[] stripNumbers;
        public TMP_Text[] stripLabels;
        public Image[] stripStateIcons;

        [Header("Level panel")]
        public CanvasGroup levelGroup;
        public Button levelBackdrop;
        public Image levelHeader;
        public Image levelCard;
        public Image levelPortrait;
        public TMP_Text levelPill;
        public TMP_Text levelIsland;
        public TMP_Text levelTitle;
        public TMP_Text levelStatus;
        public Image levelStatusIcon;
        public Button playButton;
        public Image playButtonBg;
        public TMP_Text playLabel;
        public Button completeButton;
        public Button levelCloseButton;

        [Header("Settings")]
        public CanvasGroup settingsGroup;
        public Button settingsBackdrop;
        public Button motionToggle;
        public Image motionBg;
        public TMP_Text motionLabel;
        public RectTransform motionKnob;
        public Button soundToggle;
        public Image soundBg;
        public TMP_Text soundLabel;
        public RectTransform soundKnob;
        public Image soundIcon;
        public Button resetButton;
        public Button settingsClose;

        [Header("Hub")]
        public CanvasGroup hubGroup;
        public Button[] hubIslandButtons;
        public Button hubClose;

        [Header("Toast")]
        public CanvasGroup toastGroup;
        public TMP_Text toastText;

        [Header("Art")]
        public Sprite lockSprite;
        public Sprite starSprite;
        public Sprite soundOnSprite;
        public Sprite soundOffSprite;
        public Sprite[] portraits;     // NumuData.Islands order
        public Sprite[] islandIcons;   // NumuData.Islands order

        enum Mode { Overview, Focus }

        Mode _mode = Mode.Overview;
        string _island;
        int _level;
        float _toastTimer;
        float _cardsHiddenOffset = -340f;
        readonly Dictionary<CanvasGroup, bool> _visible = new Dictionary<CanvasGroup, bool>();

        static readonly Color Off = new Color(0.78f, 0.78f, 0.82f);
        static readonly Color On = new Color(0.31f, 0.68f, 0.38f);

        void Awake() => Instance = this;

        void OnEnable()
        {
            NumuProgress.Changed += RefreshProgress;
            NumuSettings.Changed += RefreshSettings;
        }

        void OnDisable()
        {
            NumuProgress.Changed -= RefreshProgress;
            NumuSettings.Changed -= RefreshSettings;
        }

        void Start()
        {
            foreach (var c in cards)
            {
                string id = c.islandId;
                c.header.onClick.AddListener(() => { NumuAudio.Play(NumuSfx.Tap); FocusIsland(id); });
                for (int i = 0; i < c.levelRows.Length; i++)
                {
                    int lvl = i + 1;
                    c.levelRows[i].onClick.AddListener(() => OpenLevel(id, lvl));
                }
            }
            for (int i = 0; i < stripButtons.Length; i++)
            {
                int lvl = i + 1;
                stripButtons[i].onClick.AddListener(() => { if (_island != null) OpenLevel(_island, lvl); });
            }
            backButton.onClick.AddListener(Back);
            homeButton.onClick.AddListener(Home);
            settingsButton.onClick.AddListener(OpenSettings);
            levelBackdrop.onClick.AddListener(CloseLevel);
            levelCloseButton.onClick.AddListener(CloseLevel);
            playButton.onClick.AddListener(PlayLevel);
            completeButton.onClick.AddListener(CompleteLevel);
            settingsBackdrop.onClick.AddListener(CloseSettings);
            settingsClose.onClick.AddListener(CloseSettings);
            motionToggle.onClick.AddListener(() => { NumuAudio.Play(NumuSfx.Tap); NumuSettings.ReducedMotion = !NumuSettings.ReducedMotion; });
            soundToggle.onClick.AddListener(() => { NumuSettings.SoundOn = !NumuSettings.SoundOn; NumuAudio.Play(NumuSfx.Tap); });
            resetButton.onClick.AddListener(() => { NumuProgress.ResetAll(); ShowToast("Progress reset. Level 1 of every island is ready!"); });
            hubClose.onClick.AddListener(() => { SetVisible(hubGroup, false); Home(); });
            for (int i = 0; i < hubIslandButtons.Length && i < NumuData.Islands.Length; i++)
            {
                string id = NumuData.Islands[i].Id;
                hubIslandButtons[i].onClick.AddListener(() => { SetVisible(hubGroup, false); FocusIsland(id); });
            }

            foreach (var g in new[] { focusBarGroup, stripGroup, levelGroup, settingsGroup, hubGroup, toastGroup }) SetVisible(g, false, true);
            SetVisible(cardsGroup, true, true);
            SetVisible(overviewHintGroup, true, true);
            RefreshProgress();
            RefreshSettings();
        }

        // ------------------------------------------------------------------ navigation

        NumuIsland Island(string id)
        {
            foreach (var i in islands) if (i != null && i.islandId == id) return i;
            return null;
        }

        static int IslandIndex(string id)
        {
            for (int i = 0; i < NumuData.Islands.Length; i++) if (NumuData.Islands[i].Id == id) return i;
            return -1;
        }

        public void FocusIsland(string id)
        {
            var island = Island(id);
            if (island == null) return;
            if (_mode == Mode.Focus && _island == id) return;
            _mode = Mode.Focus;
            _island = id;
            rig.MoveTo(island.focusPose);
            NumuAudio.Play(NumuSfx.Whoosh);
            SetVisible(cardsGroup, false);
            SetVisible(overviewHintGroup, false);
            SetVisible(focusBarGroup, true);

            focusTitle.text = island.displayName;
            focusSubtitle.text = island.subtitle;
            var info = NumuData.Get(id);
            focusChip.color = info != null ? info.Panel : new Color(0.88f, 0.97f, 0.94f);
            focusChipEdge.color = info != null ? info.PanelEdge : new Color(0.6f, 0.85f, 0.78f);
            focusTitle.color = info != null ? info.Title : island.themeDark;
            focusSubtitle.color = focusTitle.color;
            int idx = IslandIndex(id);
            focusIcon.sprite = idx >= 0 && idx < islandIcons.Length ? islandIcons[idx] : islandIcons[islandIcons.Length - 1];

            bool levels = island.hasLevels && info != null;
            SetVisible(stripGroup, levels);
            SetVisible(hubGroup, !levels);
            if (levels) RefreshStrip();
        }

        public void ShowOverview()
        {
            SetVisible(levelGroup, false);
            _mode = Mode.Overview;
            _island = null;
            rig.MoveTo(rig.overview);
            SetVisible(focusBarGroup, false);
            SetVisible(stripGroup, false);
            SetVisible(hubGroup, false);
            SetVisible(cardsGroup, true);
            SetVisible(overviewHintGroup, true);
        }

        public void Back()
        {
            NumuAudio.Play(NumuSfx.Close);
            if (IsVisible(levelGroup)) { CloseLevel(); return; }
            if (IsVisible(settingsGroup)) { CloseSettings(); return; }
            if (_mode == Mode.Focus) ShowOverview();
        }

        public void Home()
        {
            SetVisible(levelGroup, false);
            SetVisible(settingsGroup, false);
            if (_mode == Mode.Focus) NumuAudio.Play(NumuSfx.Close);
            ShowOverview();
        }

        // ------------------------------------------------------------------ level panel

        public void OpenLevel(string islandId, int level)
        {
            var info = NumuData.Get(islandId);
            if (info == null || level < 1 || level > info.Levels.Length) return;
            if (_mode != Mode.Focus || _island != islandId) FocusIsland(islandId);
            _level = level;
            PopulateLevel();
            SetVisible(levelGroup, true);
            NumuAudio.Play(NumuProgress.Get(islandId, level) == LevelState.Locked ? NumuSfx.Locked : NumuSfx.Open);
        }

        void PopulateLevel()
        {
            var info = NumuData.Get(_island);
            var state = NumuProgress.Get(_island, _level);
            int idx = IslandIndex(_island);
            levelHeader.color = info.Theme;
            levelCard.color = info.Panel;
            levelPill.text = $"Level {_level}";
            levelIsland.text = $"{info.Name}  ·  {info.Subtitle.Replace("\n", " ")}";
            levelTitle.text = info.Levels[_level - 1];
            levelTitle.color = info.Title;
            if (idx >= 0 && idx < portraits.Length) levelPortrait.sprite = portraits[idx];
            switch (state)
            {
                case LevelState.Locked:
                    levelStatus.text = $"Locked for now. Finish Level {_level - 1} to open it!";
                    levelStatusIcon.sprite = lockSprite;
                    break;
                case LevelState.Completed:
                    levelStatus.text = "Completed. Great job! You can play it again any time.";
                    levelStatusIcon.sprite = starSprite;
                    break;
                default:
                    levelStatus.text = "Ready to play!";
                    levelStatusIcon.sprite = starSprite;
                    break;
            }
            levelStatusIcon.color = state == LevelState.Unlocked ? new Color(1, 1, 1, 0.35f) : Color.white;
            bool locked = state == LevelState.Locked;
            playButton.interactable = !locked;
            playButtonBg.color = locked ? Off : info.Theme;
            playLabel.text = locked ? "Locked" : state == LevelState.Completed ? "Play again" : "Play";
            completeButton.gameObject.SetActive(!locked && state != LevelState.Completed);
        }

        void PlayLevel()
        {
            NumuAudio.Play(NumuSfx.Tap);
            var info = NumuData.Get(_island);
            // Levels that already have a playable 3D activity open their world; the rest are still in development.
            string scenario = World.NumuScenarioCatalog.ScenarioFor(_island, _level);
            if (scenario != null)
            {
                World.NumuSceneRouter.OpenWorld(_island, scenario, _level);
                return;
            }
            ShowToast($"“{info.Levels[_level - 1]}” is coming soon!");
        }

        void CompleteLevel()
        {
            NumuProgress.Complete(_island, _level);
            NumuAudio.Play(NumuSfx.Success);
            PopulateLevel();
            ShowToast(_level < 5 ? $"Great job! Level {_level + 1} is now open." : "Amazing! You finished every level here!");
        }

        public void CloseLevel()
        {
            if (!IsVisible(levelGroup)) return;
            NumuAudio.Play(NumuSfx.Close);
            SetVisible(levelGroup, false);
        }

        // ------------------------------------------------------------------ settings

        void OpenSettings()
        {
            NumuAudio.Play(NumuSfx.Open);
            RefreshSettings();
            SetVisible(settingsGroup, true);
        }

        void CloseSettings()
        {
            NumuAudio.Play(NumuSfx.Close);
            SetVisible(settingsGroup, false);
        }

        void RefreshSettings()
        {
            bool rm = NumuSettings.ReducedMotion, snd = NumuSettings.SoundOn;
            motionBg.color = rm ? On : Off;
            motionLabel.text = rm ? "On" : "Off";
            motionKnob.anchoredPosition = new Vector2(rm ? 22f : -22f, 0f);
            soundBg.color = snd ? On : Off;
            soundLabel.text = snd ? "On" : "Off";
            soundKnob.anchoredPosition = new Vector2(snd ? 22f : -22f, 0f);
            if (soundIcon != null) soundIcon.sprite = snd ? soundOnSprite : soundOffSprite;
        }

        // ------------------------------------------------------------------ progress display

        void RefreshProgress()
        {
            foreach (var c in cards)
            {
                for (int i = 0; i < c.levelStateIcons.Length; i++) ApplyStateIcon(c.levelStateIcons[i], NumuProgress.Get(c.islandId, i + 1));
            }
            if (_island != null && NumuData.Get(_island) != null) RefreshStrip();
        }

        void RefreshStrip()
        {
            var info = NumuData.Get(_island);
            for (int i = 0; i < stripButtons.Length; i++)
            {
                var s = NumuProgress.Get(_island, i + 1);
                stripPills[i].color = s == LevelState.Locked ? Color.Lerp(info.Theme, Off, 0.5f) : info.Theme;
                stripNumbers[i].text = (i + 1).ToString();
                stripLabels[i].text = info.Levels[i];
                stripLabels[i].color = info.Title;
                ApplyStateIcon(stripStateIcons[i], s);
            }
        }

        void ApplyStateIcon(Image img, LevelState s)
        {
            if (img == null) return;
            img.enabled = s != LevelState.Unlocked;
            img.sprite = s == LevelState.Locked ? lockSprite : starSprite;
        }

        // ------------------------------------------------------------------ feedback

        void ShowToast(string message)
        {
            toastText.text = message;
            _toastTimer = 2.8f;
            SetVisible(toastGroup, true);
        }

        // ------------------------------------------------------------------ visibility / animation

        bool IsVisible(CanvasGroup g) => g != null && _visible.TryGetValue(g, out bool v) && v;

        void SetVisible(CanvasGroup g, bool visible, bool instant = false)
        {
            if (g == null) return;
            _visible[g] = visible;
            g.interactable = visible;
            g.blocksRaycasts = visible;
            if (instant || NumuSettings.ReducedMotion) g.alpha = visible ? 1f : 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool reduced = NumuSettings.ReducedMotion;
            foreach (var kv in _visible)
            {
                float target = kv.Value ? 1f : 0f;
                kv.Key.alpha = reduced ? target : Mathf.MoveTowards(kv.Key.alpha, target, dt * 5f);
            }
            if (cardsPanel != null)
            {
                float targetY = IsVisible(cardsGroup) ? 0f : _cardsHiddenOffset;
                var p = cardsPanel.anchoredPosition;
                p.y = reduced ? targetY : Mathf.Lerp(p.y, targetY, 1f - Mathf.Exp(-dt * 9f));
                cardsPanel.anchoredPosition = p;
            }
            if (_toastTimer > 0f)
            {
                _toastTimer -= dt;
                if (_toastTimer <= 0f) SetVisible(toastGroup, false);
            }
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back();
        }
    }
}
