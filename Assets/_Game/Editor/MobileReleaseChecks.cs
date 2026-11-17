using System;
using System.IO;
using System.Linq;
using TowerTrials;
using UnityEditor;
using UnityEngine;

public static class MobileReleaseChecks
{
    [MenuItem("Hero Wars/Release/Verify release data")]
    public static string Verify()
    {
        int assertions = 0;
        void Check(bool valid, string message) { assertions++; if (!valid) throw new Exception(message); }
        var catalog = HeroCenterCatalog.Load();
        var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfig>("Assets/_Game/Data/StarterCampaign.asset");
        Check(campaign.stages.Length == CampaignConfig.StageCount, "24 release keeps");
        foreach (StageDefinition stage in campaign.stages) stage.Validate();
        Check(catalog.equipment.Length == 16 && catalog.equipment.All(item => !item.cloak), "16 cosmetics with shields-only armor");
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            foreach (string locale in new[] { "en-US", "fr-FR", "ar-SA" })
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(locale);
                Check(GameUiTheme.Number(117213) == "117,213", "Power remains exact and uses supported glyphs in " + locale);
                var gift = new HeroProfile { dailyClaimUtc = "2026-11-11" };
                Check(!gift.CanClaimDaily(new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc)), "Daily receipt remains stable in " + locale);
            }
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = originalCulture; }
        VerifyPowerBadges(Check);
        var profile = new HeroProfile { progressionVersion = 1 }; profile.Repair(catalog);
        int gold = profile.gold, gems = profile.gems;
        for (int i = 0; i < campaign.stages.Length; i++)
        {
            Check(profile.HighestUnlockedStage == i, "Progress unlocks exactly the next keep");
            Check(profile.ClaimStageReward(i), "First clear earns its reward");
            gold += CampaignConfig.GoldReward(i); gems += CampaignConfig.GemReward(i);
            Check(!profile.ClaimStageReward(i), "Repeated completion cannot duplicate rewards");
            Check(profile.gold == gold && profile.gems == gems, "Exact reward balances");
            profile = JsonUtility.FromJson<HeroProfile>(JsonUtility.ToJson(profile)); profile.Repair(catalog);
            Check(profile.completedStages.Count == i + 1, "Reward receipt survives save round trip");
        }
        Check(profile.level == 9 && profile.HighestUnlockedStage == 23, "Campaign has a finite completed state");
        const string temporaryKey = "HeroWars.ReleaseSaveCheck";
        try
        {
            GameSave.Write(temporaryKey, profile);
            PlayerPrefs.SetString(temporaryKey, "{broken-json");
            HeroProfile recovered = GameSave.Read<HeroProfile>(temporaryKey, value => value.version == 1);
            Check(recovered != null && recovered.gold == gold && recovered.completedStages.Count == 24, "Damaged primary save recovers from backup");
            PlayerPrefs.SetString(temporaryKey, "{\"version\":99}");
            recovered = GameSave.Read<HeroProfile>(temporaryKey, value => value.version == 1);
            Check(recovered != null && recovered.version == 1, "Unknown save version recovers from backup");
        }
        finally { PlayerPrefs.DeleteKey(temporaryKey); PlayerPrefs.DeleteKey(temporaryKey + ".backup"); PlayerPrefs.Save(); }
        Check(EditorBuildSettings.scenes.All(scene => scene.enabled) && EditorBuildSettings.scenes.Select(scene => scene.path)
            .SequenceEqual(new[] { StartupLoadingSetup.ScenePath, HeroCenterSetup.ScenePath, PrototypeSetup.ScenePath }),
            "Startup loads first, followed by Hero Center and gameplay");
        Check(PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android) == MobileReleaseBuild.ApplicationId, "Android identity is configured");
        Check(PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS) == MobileReleaseBuild.ApplicationId, "iOS identity is configured");
        string report = "PASS: " + assertions + " release assertions. All 24 keeps validate; sequential unlocks, exact first-clear rewards, repeated-completion protection, progression serialization, primary-save recovery, shield-only equipment, scene list and mobile identifiers.\n";
        Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/MobileReleaseValidation.txt", report);
        return report;
    }

    private static void VerifyPowerBadges(Action<bool, string> check)
    {
        var root = new GameObject("PowerBadgeFixture", typeof(RectTransform), typeof(Canvas), typeof(TowerGameController));
        root.hideFlags = HideFlags.HideAndDontSave;
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        try
        {
            var game = root.GetComponent<TowerGameController>();
            var add = typeof(TowerGameController).GetMethod("AddBadge", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (long value in new[] { 2L, 117213L, long.MaxValue })
            {
                var text = (TMPro.TMP_Text)add.Invoke(game, new object[] { root.transform, Vector3.zero, Color.blue, root.transform, value });
                Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate();
                check(!text.isTextOverflowing && text.text == GameUiTheme.Number(value), "Full power value fits its badge: " + value);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
