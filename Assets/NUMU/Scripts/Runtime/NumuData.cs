using UnityEngine;

namespace Numu
{
    /// <summary>Static content for the four learning islands. Activity names match the design exactly.</summary>
    public static class NumuData
    {
        public class IslandInfo
        {
            public string Id;
            public string Name;
            public string Subtitle;
            public Color Theme;      // pills, markers
            public Color Title;      // headline text
            public Color Panel;      // card background
            public Color PanelEdge;  // card border
            public string[] Levels;
        }

        public static readonly IslandInfo Home = new IslandInfo
        {
            Id = "home",
            Name = "Home",
            Subtitle = "Emotion + Independence",
            Theme = NumuColor.Hex("#4fae62"),
            Title = NumuColor.Hex("#1f6a34"),
            Panel = NumuColor.Hex("#ecf7dc"),
            PanelEdge = NumuColor.Hex("#c9e6ad"),
            Levels = new[] { "Morning routine", "Self-care skills", "Asking for help", "Recognize family emotions", "Organize toys" },
        };

        public static readonly IslandInfo School = new IslandInfo
        {
            Id = "school",
            Name = "School",
            Subtitle = "Attention + Social Understanding",
            Theme = NumuColor.Hex("#3d8ee6"),
            Title = NumuColor.Hex("#1c4fa6"),
            Panel = NumuColor.Hex("#e4f1fd"),
            PanelEdge = NumuColor.Hex("#bcd9f6"),
            Levels = new[] { "Follow teacher", "Focus on task", "Listen carefully", "Class situations", "Choose the right response" },
        };

        public static readonly IslandInfo Park = new IslandInfo
        {
            Id = "park",
            Name = "Park",
            Subtitle = "Social Interaction + Emotion Recognition",
            Theme = NumuColor.Hex("#e0589c"),
            Title = NumuColor.Hex("#a8186a"),
            Panel = NumuColor.Hex("#fde3ef"),
            PanelEdge = NumuColor.Hex("#f6c0da"),
            Levels = new[] { "Wait turn", "Share toys", "Play with peers", "Identify emotions", "Help a friend" },
        };

        public static readonly IslandInfo Supermarket = new IslandInfo
        {
            Id = "supermarket",
            Name = "Supermarket",
            Subtitle = "Communication + Daily Living",
            Theme = NumuColor.Hex("#f39b2c"),
            Title = NumuColor.Hex("#8a3a12"),
            Panel = NumuColor.Hex("#fff5c9"),
            PanelEdge = NumuColor.Hex("#f6dd94"),
            Levels = new[] { "Ask for an item", "Make choices", "Shopping list", "Talk to cashier", "Wait in line" },
        };

        public static readonly IslandInfo[] Islands = { Home, School, Park, Supermarket };

        public static IslandInfo Get(string id)
        {
            foreach (var i in Islands) if (i.Id == id) return i;
            return null;
        }
    }

    public enum LevelState { Locked, Unlocked, Completed }

    /// <summary>Level progress per island. Level 1 starts unlocked; completing a level unlocks the next.</summary>
    public static class NumuProgress
    {
        public static event System.Action Changed;

        static string Key(string island, int level) => $"numu.progress.{island}.{level}";

        public static LevelState Get(string island, int level)
        {
            int v = PlayerPrefs.GetInt(Key(island, level), level == 1 ? (int)LevelState.Unlocked : (int)LevelState.Locked);
            return (LevelState)Mathf.Clamp(v, 0, 2);
        }

        public static void Complete(string island, int level)
        {
            PlayerPrefs.SetInt(Key(island, level), (int)LevelState.Completed);
            if (level < 5 && Get(island, level + 1) == LevelState.Locked)
                PlayerPrefs.SetInt(Key(island, level + 1), (int)LevelState.Unlocked);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void ResetAll()
        {
            foreach (var i in NumuData.Islands)
                for (int l = 1; l <= 5; l++) PlayerPrefs.DeleteKey(Key(i.Id, l));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
