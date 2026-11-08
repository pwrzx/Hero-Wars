using UnityEngine;

namespace TowerTrials
{
    [CreateAssetMenu(menuName = "Tower Trials/Campaign", fileName = "StarterCampaign")]
    public sealed class CampaignConfig : ScriptableObject
    {
        public StageDefinition[] stages = CreateStarterStages();

        public static StageDefinition[] CreateStarterStages() => new[]
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
        };
    }
}
