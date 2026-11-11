using UnityEngine;

namespace TowerTrials
{
    [CreateAssetMenu(menuName = "Tower Trials/Campaign", fileName = "StarterCampaign")]
    public sealed class CampaignConfig : ScriptableObject
    {
        public const int StageCount = 24;
        public const string SelectedStageKey = "HeroWars.SelectedStage";
        public static int GoldReward(int stage) => 180 + stage * 10;
        public static int GemReward(int stage) => (stage + 1) % 6 == 0 ? 20 : 5;
        public static string Chapter(int stage) => new[] { "Verdant Reach", "Ashen Heights", "Frostmarch", "The Sunken Citadel" }[Mathf.Clamp(stage / 6, 0, 3)];
        public StageDefinition[] stages = CreateStarterStages();

        public static StageDefinition[] CreateStarterStages() => Expand(new[]
        {
            new StageDefinition("THE VERDANT KEEP", 3,
                new TowerDefinition(2, 19, 5),
                new TowerDefinition(16, 23, 37),
                new TowerDefinition(62, 30, 95)),
            new StageDefinition("THE HOLLOW CITADEL", 28,
                new TowerDefinition(9, 48, 18),
                new TowerDefinition(41, 140, 67),
                new TowerDefinition(121, 610, 205)),
            new StageDefinition("THE LAST WATCH", 36,
                new TowerDefinition(13, 65, 24),
                new TowerDefinition(53, 187, 89),
                new TowerDefinition(173, 895, 303),
                new TowerDefinition(449, 2400, 737))
        });

        // Preserve the authored opening and generate deterministic, validated later keeps.
        public static StageDefinition[] Expand(StageDefinition[] opening)
        {
            var result = new StageDefinition[StageCount];
            for (int i = 0; i < result.Length; i++)
            {
                if (opening != null && i < opening.Length) { result[i] = opening[i]; continue; }
                bool created = false;
                for (int attempt = 0; attempt < 256 && !created; attempt++)
                {
                    var random = new System.Random(9471 + i * 193 + attempt * 7919);
                    long power = 10 + i * 3;
                    var unique = new System.Collections.Generic.HashSet<long> { power };
                    var towers = new TowerDefinition[i % 6 == 5 ? 4 : 3];
                    for (int tower = 0; tower < towers.Length; tower++)
                    {
                        var enemies = new long[i >= 6 && (tower + i) % 2 == 0 ? 4 : 3];
                        for (int room = 0; room < enemies.Length; room++)
                        {
                            long enemy = System.Math.Max(1, power * random.Next(40, 89) / 100);
                            while (!unique.Add(enemy)) enemy++;
                            enemies[room] = enemy;
                            power = checked(power + enemy);
                        }
                        for (int j = enemies.Length - 1; j > 0; j--)
                        { int swap = random.Next(j + 1); long value = enemies[j]; enemies[j] = enemies[swap]; enemies[swap] = value; }
                        towers[tower] = new TowerDefinition(enemies);
                    }
                    var stage = new StageDefinition(Chapter(i).ToUpperInvariant() + " / KEEP " + (i % 6 + 1), 10 + i * 3, towers);
                    try { stage.Validate(); result[i] = stage; created = true; }
                    catch (System.ArgumentException) { }
                }
                if (!created) throw new System.InvalidOperationException("Could not create a valid keep: " + i);
            }
            return result;
        }
    }
}
