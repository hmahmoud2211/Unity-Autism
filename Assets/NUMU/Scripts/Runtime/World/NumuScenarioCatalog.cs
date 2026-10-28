namespace Numu.World
{
    /// <summary>Which map levels have a playable realistic-world activity (island id + level number → scenario id).</summary>
    public static class NumuScenarioCatalog
    {
        public static string ScenarioFor(string island, int level)
        {
            switch (island)
            {
                case "park" when level == 4: return "park_swing_happy";              // Park › Identify emotions
                case "home" when level == 4: return "home_mum_sad";                  // Home › Recognize family emotions
                case "school" when level == 4: return "school_lina_nervous";         // School › Class situations
                case "supermarket" when level == 4 || level == 5: return "market_checkout"; // Supermarket › Talk to cashier / Wait in line
                default: return null;
            }
        }
    }
}
