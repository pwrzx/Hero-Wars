using System;
using System.IO;
using TowerTrials;
using UnityEditor;
using UnityEngine;

public static class DefeatEconomyChecks
{
    [MenuItem("Tower Trials/Verify Defeat Rewards & Gold Training")]
    public static string Verify()
    {
        int checks = 0;
        void Check(bool valid, string message) { checks++; if (!valid) throw new Exception(message); }
        var definition = new StageDefinition("Reward timing", 20, new TowerDefinition(4, 15, 60)) { allowPowerWalls = true };
        var run = new StageRun(definition);
        Check(!run.TryReviveWithPowerBoost(), "A live run cannot claim a reward");
        run.Attack(0, 1);
        Check(run.Power == 35 && !run.Failed && !run.Complete, "Stronger remaining enemy does not automatically end the run");
        Check(run.Attack(0, 2) == AttackOutcome.Lost && run.Power == 35, "An actual attack is required to lose");
        Check(run.TryReviveWithPowerBoost() && run.Power == 42 && run.DefeatedCount == 1 && run.StartingPower == 20,
            "Reward adds 20% current power and preserves kills without upgrading permanent base power");
        Check(!run.TryReviveWithPowerBoost() && run.Power == 42, "A duplicate reward cannot boost a live run");
        run.Attack(0, 0);
        Check(run.Power == 46 && run.PowerBlocked && !run.Failed, "A power wall stays playable until the hero attacks");
        run.Attack(0, 2);
        Check(run.TryReviveWithPowerBoost() && run.Power == 56, "Second confirmed reward compounds and rounds up");
        run.Attack(0, 2);
        Check(run.TryReviveWithPowerBoost() && run.Power == 68, "A later defeat can earn another separate reward");
        var saved = JsonUtility.FromJson<RunSave>(JsonUtility.ToJson(run.Save(0)));
        Check(saved.version == RunSave.CurrentVersion && saved.rewardedRevives.Length == 3, "Checkpoint records all reward timings");
        Check(StageRun.TryRestore(definition, saved, out var restored) && restored.Power == 68 && restored.DefeatedCount == 2 && !restored.Failed,
            "Reward checkpoints replay correctly even when rooms were killed out of order");
        Check(restored.Attack(0, 2) == AttackOutcome.StageCleared && restored.Power == 128, "Boosted hero can defeat the blocker");
        Check(StageRun.TryRestore(definition, restored.Save(0), out var completed) && completed.Complete && completed.Power == 128,
            "A completed boosted run survives reload exactly");
        Check(!completed.TryReviveWithPowerBoost(), "Completed runs cannot receive an ad boost");
        var oldBoostedSave = run.Save(0);
        oldBoostedSave.version = 3;
        oldBoostedSave.roomOrder = null;
        Check(StageRun.TryRestore(definition, oldBoostedSave, out var oldBoosted) && oldBoosted.Power == 68 &&
            oldBoosted.IsDefeated(0, 1) && oldBoosted.RoomAtFloor(0, 1) == 1, "Legacy boosted checkpoints retain power, kills and floor positions");
        RunSave damaged = run.Save(0);
        damaged.rewardedRevives[0].clearedMasks[0] = 256;
        Check(!StageRun.TryRestore(definition, damaged, out _), "Invalid reward room bits are rejected");
        Check(StageRun.TryRestore(definition, run.Save(0), out _), "Changing a saved snapshot cannot mutate the live run's reward history");
        damaged = run.Save(0); damaged.rewardedRevives[1].clearedMasks[0] = 0;
        Check(!StageRun.TryRestore(definition, damaged, out _), "Reward snapshots cannot undo previous kills");
        damaged = run.Save(0); damaged.rewardedRevives[0].clearedMasks[0] = 7;
        Check(!StageRun.TryRestore(definition, damaged, out _), "Rewards after an impossible stage clear are rejected");
        var legacyDefinition = new StageDefinition("Legacy", 12, new TowerDefinition(7, 13, 29));
        var legacy = new StageRun(legacyDefinition); legacy.Attack(0, 0); legacy.Attack(0, 2);
        RunSave legacySave = legacy.Save(0); legacySave.version = 2; legacySave.rewardedRevives = null;
        Check(StageRun.TryRestore(legacyDefinition, legacySave, out var legacyRun) && legacyRun.Failed && legacyRun.Power == 19,
            "Version-two failed checkpoints still restore");
        Check(legacyRun.TryReviveWithPowerBoost() && legacyRun.Power == 23 && StageRun.TryRestore(legacyDefinition, legacyRun.Save(0), out var upgradedSave) && upgradedSave.Power == 23,
            "Old checkpoints migrate to the current format with boosted power");
        legacySave.version = 1;
        Check(StageRun.TryRestore(legacyDefinition, legacySave, out _), "Original puzzle checkpoints remain compatible");
        var nearLimit = new StageRun(new StageDefinition("Overflow guard", 1, new TowerDefinition(long.MaxValue / 2 - 1)) { allowPowerWalls = true });
        bool overflowRejected = false;
        for (int i = 0; i < 512; i++)
        {
            nearLimit.Attack(0, 0);
            long before = nearLimit.Power;
            if (nearLimit.TryReviveWithPowerBoost()) continue;
            overflowRejected = nearLimit.Failed && nearLimit.Power == before;
            break;
        }
        Check(overflowRejected, "An overflowing reward fails without reviving or changing power");

        var catalog = HeroCenterCatalog.Load();
        var profile = new HeroProfile { gold = 200 };
        Check(profile.TryUpgradeGold() && profile.gold == 0 && profile.goldRank == 1 && profile.GoldBonusPercent == 10 && profile.GoldUpgradeCost == 300,
            "Gold training charges exactly once and adds ten percentage points");
        Check(!profile.TryUpgradeGold() && profile.gold == 0 && profile.goldRank == 1, "Unaffordable gold training does not change the profile");
        for (int stage = 0; stage < CampaignConfig.StageCount; stage++)
        foreach (int rank in new[] { 0, 1, 7, HeroProfile.MaxGoldRank })
        {
            var earnings = new HeroProfile { gold = 0, goldRank = rank, progressionVersion = 1 };
            int perKill = (CampaignConfig.DefeatGoldReward(stage) * (100 + rank * 10) + 99) / 100;
            int firstClear = (CampaignConfig.GoldReward(stage) * (100 + rank * 10) + 99) / 100;
            string id = Guid.NewGuid().ToString("N");
            Check(earnings.ClaimCombatReward(id, stage, 5) && earnings.gold == 5 * perKill, "Gold bonus applies to each enemy kill");
            Check(!earnings.ClaimCombatReward(id, stage, 5) && earnings.gold == 5 * perKill, "A restored kill cannot grant gold again");
            Check(earnings.ClaimCombatReward(id, stage, 15) && earnings.gold == 15 * perKill, "Bulk and incremental rewards use the same rounding");
            Check(earnings.ClaimStageReward(stage) && earnings.gold == 15 * perKill + firstClear, "First-clear gold receives the income bonus");
            Check(!earnings.ClaimStageReward(stage) && earnings.gold == 15 * perKill + firstClear, "A replay cannot duplicate the first-clear reward");
            var reloaded = JsonUtility.FromJson<HeroProfile>(JsonUtility.ToJson(earnings)); reloaded.Repair(catalog);
            Check(reloaded.goldRank == rank && reloaded.GoldBonusPercent == rank * 10, "Gold training survives reload");
        }
        var changing = new HeroProfile { gold = 1000 };
        changing.ClaimCombatReward("gold-upgrade-run", 1, 1);
        int oldBalance = changing.gold;
        changing.TryUpgradeGold();
        Check(!changing.ClaimCombatReward("gold-upgrade-run", 1, 1) && changing.gold == oldBalance - 200, "An upgrade cannot retroactively pay old kills");
        changing.ClaimCombatReward("gold-upgrade-run", 1, 2);
        Check(changing.gold == oldBalance - 200 + 27, "Only new kills use the increased bonus");
        var capped = new HeroProfile { goldRank = HeroProfile.MaxGoldRank, gold = 1000000000 - 1 };
        Check(!capped.TryUpgradeGold(), "Maximum gold rank cannot charge another upgrade");
        capped.ClaimCombatReward("capped", 23, 15);
        Check(capped.gold == 1000000000 && capped.GoldWithBonus(int.MaxValue) == 1000000000, "Gold amounts and wallet clamp safely");
        var corrupt = new HeroProfile { goldRank = -5 }; corrupt.Repair(catalog);
        Check(corrupt.goldRank == 0, "Invalid gold rank repairs safely");
        string report = "PASS: " + checks + " defeat/economy assertions. Attack-required defeat, 20% rewarded revival, repeated rewards and exact checkpoint timing, old saves, rounding, overflow, gold training, all-level kill/clear income and reward protection.\n";
        Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/DefeatEconomyValidation.txt", report);
        Debug.Log(report); return report;
    }
}
