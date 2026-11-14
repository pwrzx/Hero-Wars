using System;
using System.IO;
using System.Linq;
using TowerTrials;
using UnityEditor;
using UnityEngine;

public static class PowerProgressionChecks
{
    [MenuItem("Tower Trials/Verify Power Progression")]
    public static string Verify()
    {
        int checks = 0;
        void Check(bool valid, string message) { checks++; if (!valid) throw new Exception(message); }
        var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfig>("Assets/_Game/Data/StarterCampaign.asset");
        var generated = CampaignConfig.CreateStarterStages();
        Check(campaign.stages.Length == CampaignConfig.StageCount, "All 24 levels exist");
        Check(campaign.stages[0].towers[0].enemies.SequenceEqual(new long[] { 7, 13, 29 }), "Exact authored first tower");
        Check(campaign.stages[0].towers[1].enemies.SequenceEqual(new long[] { 54, 74, 134 }), "Second tower scales from 61");
        var opening = new StageRun(campaign.stages[0]);
        foreach (int room in new[] { 0, 1, 2 }) opening.Attack(0, room);
        Check(opening.Power == 61 && opening.TowerIndex == 1, "12 + 7 + 13 + 29 = 61");
        var orders = new System.Collections.Generic.HashSet<string>();
        var report = new System.Text.StringBuilder();
        report.AppendLine("Level / Minimum starting power / First winning training rank / Final power");
        long previousMinimum = 0;
        for (int i = 0; i < campaign.stages.Length; i++)
        {
            StageDefinition stage = campaign.stages[i];
            stage.Validate();
            Check(stage.Signature == generated[i].Signature, "Saved and generated enemies match at level " + i);
            Check(stage.allowPowerWalls && stage.towers.Length == 5 && stage.towers.All(t => t.enemies.Length == 3), "Five towers of three enemies");
            Check(stage.MinimumStartingPower > previousMinimum, "Later levels require stronger permanent power");
            previousMinimum = stage.MinimumStartingPower;
            for (int t = 1; t < 5; t++)
                Check(stage.towers[t].enemies.Min() > stage.towers[t - 1].enemies.Max(), "Each tower is stronger than the previous tower");
            var blocked = new StageRun(stage);
            for (int t = 0; t < stage.towers.Length; t++)
            {
                int[] order = Enumerable.Range(0, 3).Select(floor => blocked.RoomAtFloor(t, floor)).ToArray();
                Check(order.OrderBy(room => room).SequenceEqual(new[] { 0, 1, 2 }), "Each floor gets a different enemy from its own tower");
                orders.Add(string.Join(",", order));
            }
            ClearAvailable(blocked);
            Check(blocked.PowerBlocked && !blocked.Complete, "Base hero reaches a real wall");
            long wallPower = blocked.Power;
            int defeats = blocked.DefeatedCount;
            RunSave checkpoint = JsonUtility.FromJson<RunSave>(JsonUtility.ToJson(blocked.Save(i)));
            Check(StageRun.TryRestore(stage, checkpoint, out var restored) && restored.PowerBlocked &&
                restored.Power == wallPower && restored.DefeatedCount == defeats, "Blocked checkpoint survives reload");
            Check(checkpoint.roomOrder.SequenceEqual(restored.Save(i).roomOrder), "Reload preserves the exact floor order in every tower");
            int guard = Enumerable.Range(0, stage.towers[blocked.TowerIndex].enemies.Length)
                .First(room => !blocked.IsDefeated(blocked.TowerIndex, room));
            Check(blocked.Attack(blocked.TowerIndex, guard) == AttackOutcome.Lost && blocked.Power == wallPower, "Losing preserves power and enemy values");
            Check(StageRun.TryRestore(stage, blocked.Save(i), out var failed) && failed.Failed, "Failed power wall restores");
            Check(new StageRun(stage).Power == 12 && stage.Signature == checkpoint.definitionSignature, "Retry keeps exactly the same enemies");
            var below = new StageRun(stage, stage.MinimumStartingPower - 1);
            ClearAvailable(below);
            Check(below.PowerBlocked && !below.Complete, "One point below the clear threshold cannot win");
            var exact = new StageRun(stage, stage.MinimumStartingPower);
            ClearAvailable(exact);
            Check(exact.Complete && exact.DefeatedCount == 15, "Clear threshold beats all five towers");
            var profile = new HeroProfile { gold = 1000000 };
            while (profile.StartingPower < stage.MinimumStartingPower)
            {
                long before = profile.StartingPower;
                int balance = profile.gold, cost = profile.PowerUpgradeCost;
                Check(profile.TryUpgradePower() && profile.StartingPower == (before * 120 + 99) / 100 &&
                    profile.gold == balance - cost, "Training compounds 20%, rounds up, and charges exactly once");
            }
            profile = JsonUtility.FromJson<HeroProfile>(JsonUtility.ToJson(profile));
            profile.Repair(HeroCenterCatalog.Load());
            var upgraded = new StageRun(stage, profile.StartingPower);
            ClearAvailable(upgraded);
            Check(upgraded.Complete && stage.Signature == checkpoint.definitionSignature, "Permanent upgrades survive reload and beat fixed enemies");
            Check(StageRun.TryRestore(stage, upgraded.Save(i), out var finished) && finished.Complete &&
                finished.StartingPower == profile.StartingPower, "Upgraded completed run restores exactly");
            var changed = new StageDefinition(stage.title, 12, stage.towers) { allowPowerWalls = false };
            Check(!StageRun.TryRestore(stage, new RunSave { version = 1, clearedMasks = new int[5] }, out _), "Legacy campaign checkpoint resets safely");
            RunSave altered = upgraded.Save(i); altered.definitionSignature = changed.Signature;
            Check(!StageRun.TryRestore(stage, altered, out _), "Rebalanced checkpoint cannot reinterpret old kills");
            var earnings = new HeroProfile { gold = 0 };
            Check(earnings.ClaimCombatReward(upgraded.RunId, i, 5), "Kills earn gold");
            Check(!earnings.ClaimCombatReward(upgraded.RunId, i, 5) && !earnings.ClaimCombatReward(upgraded.RunId, i, 3), "Reload and older checkpoints do not duplicate gold");
            Check(earnings.ClaimCombatReward(upgraded.RunId, i, 15) && earnings.gold == 15 * CampaignConfig.DefeatGoldReward(i), "Later kills credit only their new reward");
            Check(earnings.ClaimCombatReward(new StageRun(stage).RunId, i, 1), "A fresh attempt can earn gold again");
            Check(stage.Signature == generated[i].Signature, "Combat and upgrades never mutate campaign data");
            report.AppendLine((i + 1) + " / " + stage.MinimumStartingPower + " / " + profile.powerRank + " / " + upgraded.Power);
        }
        Check(orders.Count > 1, "Fresh campaign towers vary their floor order");
        RunSave legacy = opening.Save(0);
        legacy.version = 3;
        legacy.roomOrder = null;
        Check(StageRun.TryRestore(campaign.stages[0], legacy, out var migrated) && migrated.Power == 61 &&
            migrated.Save(0).roomOrder.SequenceEqual(Enumerable.Repeat(new[] { 0, 1, 2 }, 5).SelectMany(order => order)),
            "Old checkpoints retain their authored floor order and completed rooms");
        RunSave corrupt = opening.Save(0);
        corrupt.roomOrder[0] = corrupt.roomOrder[1];
        Check(!StageRun.TryRestore(campaign.stages[0], corrupt, out _), "Duplicate rooms in a saved tower are rejected");
        Check(StageRun.TryRestore(campaign.stages[0], opening.Save(0), out _), "Editing a saved layout cannot mutate the live run");
        var poor = new HeroProfile { gold = 99 };
        Check(!poor.TryUpgradePower() && poor.gold == 99 && poor.powerRank == 0, "Unaffordable training makes no changes");
        var maximum = new HeroProfile { powerRank = HeroProfile.MaxPowerRank, gold = 1000000 };
        Check(!maximum.TryUpgradePower() && maximum.gold == 1000000, "Maximum training rank makes no charge");
        var tie = new StageDefinition("Tie wall", 12, new TowerDefinition(12, 25, 51)) { allowPowerWalls = true };
        var tied = new StageRun(tie);
        Check(tied.PowerBlocked && tied.Attack(0, 0) == AttackOutcome.Lost, "Equal-power enemies are a wall and defeat the hero");
        Check(StageRun.TryRestore(tie, tied.Save(0), out var restoredTie) && restoredTie.Failed, "Equal-power defeat restores");
        string result = "PASS: " + checks + " power progression checks. Fixed enemies, five towers, real walls, 20% permanent training, exact rewards, retries and save compatibility.\n" + report;
        Directory.CreateDirectory("Artifacts");
        File.WriteAllText("Artifacts/PowerProgressionValidation.txt", result);
        Debug.Log(result);
        return result;
    }

    private static void ClearAvailable(StageRun run)
    {
        while (!run.Complete && !run.Failed && !run.PowerBlocked)
        {
            long[] enemies = run.Definition.towers[run.TowerIndex].enemies;
            int room = Enumerable.Range(0, enemies.Length).Where(r => !run.IsDefeated(run.TowerIndex, r) && enemies[r] < run.Power)
                .OrderBy(r => enemies[r]).First();
            run.Attack(run.TowerIndex, room);
        }
    }
}
