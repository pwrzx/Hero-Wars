using UnityEngine;

namespace TowerTrials
{
    [CreateAssetMenu(menuName = "Tower Trials/Campaign", fileName = "StarterCampaign")]
    public sealed class CampaignConfig : ScriptableObject
    {
        public StageDefinition[] stages = CreateStarterStages();

        public static StageDefinition[] CreateStarterStages() => new[]
        {
            new StageDefinition("THE VERDANT KEEP", 20,
                new TowerDefinition(4, 31, 15),
                new TowerDefinition(26, 83, 47),
                new TowerDefinition(62, 350, 105)),
            new StageDefinition("THE HOLLOW CITADEL", 28,
                new TowerDefinition(9, 48, 18),
                new TowerDefinition(41, 140, 67),
                new TowerDefinition(121, 610, 205)),
            new StageDefinition("THE LAST WATCH", 36,
                new TowerDefinition(13, 65, 24),
                new TowerDefinition(53, 187, 89),
                new TowerDefinition(173, 895, 303),
                new TowerDefinition(449, 2400, 737))
        };
    }
}
