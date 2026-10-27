namespace Numu.World
{
    /// <summary>Which map levels have a playable realistic-world activity (island id + level number → scenario id).</summary>
    public static class NumuScenarioCatalog
    {
        public static string ScenarioFor(string island, int level)
        {
            if (island == "park" && level == 4) return "park_swing_happy"; // Park › Identify emotions
            return null;
        }
    }
}
