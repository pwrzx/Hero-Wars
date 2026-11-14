using UnityEngine;

namespace TowerTrials
{
    [CreateAssetMenu(menuName = "Tower Trials/Campaign", fileName = "StarterCampaign")]
    public sealed class CampaignConfig : ScriptableObject
    {
        public const int StageCount = 24;
        public const int TowersPerStage = 5;
        public const string SelectedStageKey = "HeroWars.SelectedStage";
        public static int GoldReward(int stage) => 180 + stage * 10;
        public static int GemReward(int stage) => (stage + 1) % 6 == 0 ? 20 : 5;
        public static int DefeatGoldReward(int stage) => 20 + Mathf.Clamp(stage, 0, StageCount - 1) * 4;
        public static string Chapter(int stage) => new[] { "Verdant Reach", "Ashen Heights", "Frostmarch", "The Sunken Citadel" }[Mathf.Clamp(stage / 6, 0, 3)];
        public StageDefinition[] stages = CreateStarterStages();

        public static StageDefinition[] CreateStarterStages() => Expand(null);

        // Enemies depend only on the level, never the player's upgrades or number of retries.
        public static StageDefinition[] Expand(StageDefinition[] opening)
        {
            var result = new StageDefinition[StageCount];
            for (int i = 0; i < result.Length; i++)
            {
                if (opening != null && i < opening.Length) { result[i] = opening[i]; continue; }
                long reference = (long)System.Math.Round(12 * System.Math.Pow(1.35, i), System.MidpointRounding.AwayFromZero);
                long power = reference;
                var towers = new TowerDefinition[TowersPerStage];
                for (int tower = 0; tower < towers.Length; tower++)
                {
                    long[] enemies;
                    if (tower == 0)
                        enemies = new[] { Scale(reference, 7, 12), Scale(reference, 13, 12), Scale(reference, 29, 12) };
                    else if (tower == 1)
                        enemies = new[] { Scale(power, 88, 100), Scale(power, 122, 100), Scale(power, 220, 100) };
                    else
                    {
                        int firstRatio = tower == 2 ? 92 : tower == 3 ? 96 : 98;
                        int secondRatio = tower == 2 ? 132 : tower == 3 ? 142 : 152;
                        int wallRatio = tower == 2 ? 20 : tower == 3 ? 50 : 90;
                        long first = Scale(power, firstRatio, 100), second = Scale(power, secondRatio, 100);
                        // The guard exceeds power after the two weaker kills by a fixed upgrade gap.
                        long guard = checked(power + first + second + Scale(reference, wallRatio, 100));
                        enemies = new[] { first, second, guard };
                    }
                    towers[tower] = new TowerDefinition(enemies);
                    foreach (long enemy in enemies) power = checked(power + enemy);
                }
                string title = i == 0 ? "THE VERDANT KEEP" : i == 1 ? "THE HOLLOW CITADEL" : i == 2 ? "THE LAST WATCH" :
                    Chapter(i).ToUpperInvariant() + " / KEEP " + (i % 6 + 1);
                result[i] = new StageDefinition(title, HeroProfile.BaseStartingPower, towers) { allowPowerWalls = true };
                result[i].Validate();
            }
            return result;
        }

        private static long Scale(long value, int numerator, int denominator) =>
            System.Math.Max(1, checked(value * numerator + denominator / 2) / denominator);
    }
}
