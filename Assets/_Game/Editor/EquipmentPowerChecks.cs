using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TowerTrials;
using UnityEditor;
using UnityEngine;

public static class EquipmentPowerChecks
{
    [MenuItem("Tower Trials/Verify Equipment Power & Dev Reset")]
    public static string Verify()
    {
        int checks = 0;
        void Check(bool valid, string message) { checks++; if (!valid) throw new Exception(message); }
        HeroCenterCatalog catalog = HeroCenterCatalog.Load();
        var profile = new HeroProfile(); profile.Repair(catalog);
        Check(profile.CombatStartingPower(catalog) == 12 && profile.EquipmentBonusPercent(catalog) == 0,
            "Starter equipment preserves the 12-power opening");
        foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
        {
            int expected = rarity == ItemRarity.Common ? 5 : rarity == ItemRarity.Rare ? 10 : rarity == ItemRarity.Epic ? 20 : 35;
            Check(HeroCenterCatalog.RarityPowerBonusPercent(rarity) == expected, "Published rarity bonus: " + rarity);
            foreach (EquipmentSlot slot in new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor })
                Check(new HeroEquipment { slot = slot, rarity = rarity }.PowerBonusPercent == expected,
                    "Weapons and shields share the rarity bonus");
        }
        foreach (HeroEquipment item in catalog.equipment)
        {
            Check(item.PowerBonusPercent == (item.starter || item.slot == EquipmentSlot.Skin ? 0 : HeroCenterCatalog.RarityPowerBonusPercent(item.rarity)),
                "Actual catalog item uses the correct bonus: " + item.id);
            if (!profile.Owns(item.id)) profile.owned.Add(item.id);
        }
        foreach (HeroEquipment armor in catalog.equipment.Where(item => item.slot == EquipmentSlot.Armor))
        foreach (HeroEquipment weapon in catalog.equipment.Where(item => item.slot == EquipmentSlot.Weapon))
        foreach (int rank in new[] { 0, 4, 41, HeroProfile.MaxPowerRank })
        {
            profile.powerRank = rank;
            Check(profile.TryEquip(armor) && profile.TryEquip(weapon), "Owned gear equips");
            int expectedBonus = armor.PowerBonusPercent + weapon.PowerBonusPercent;
            long expected = (profile.StartingPower * (100 + expectedBonus) + 99) / 100;
            Check(profile.EquipmentBonusPercent(catalog) == expectedBonus && profile.CombatStartingPower(catalog) == expected,
                "Both equipped bonuses add, apply to trained base and round up once");
            var restored = JsonUtility.FromJson<HeroProfile>(JsonUtility.ToJson(profile)); restored.Repair(catalog);
            Check(restored.CombatStartingPower(catalog) == expected, "Equipment power survives profile serialization");
            var stage = CampaignConfig.CreateStarterStages()[0];
            var run = new StageRun(stage, expected);
            Check(StageRun.TryRestore(stage, run.Save(0), out var restoredRun) && restoredRun.StartingPower == expected,
                "A geared run preserves exact starting power");
        }
        profile.powerRank = 4;
        long geared = profile.CombatStartingPower(catalog);
        profile.skin = "skin.fire";
        Check(profile.CombatStartingPower(catalog) == geared, "Hero skins remain cosmetic");
        var locked = new HeroProfile { weapon = "weapon.sword3", armor = "armor.kingsguard" };
        Check(locked.EquipmentBonusPercent(catalog) == 0, "Unowned gear cannot grant power");
        var chestProfile = new HeroProfile { gems = 1000 }; chestProfile.Repair(catalog);
        HeroChest legendary = catalog.chests.First(chest => chest.rarity == ItemRarity.Legendary);
        HeroEquipment gear = null;
        for (int roll = 0; roll < 100 && gear == null; roll++)
        {
            chestProfile.gems = 1000;
            Check(chestProfile.TryOpenChest(catalog, legendary, new System.Random(roll), out var reward, out _, out _), "Legendary chest opens");
            if (reward.slot != EquipmentSlot.Skin) gear = reward;
        }
        Check(gear != null && gear.PowerBonusPercent == 35 && chestProfile.TryEquip(gear) && chestProfile.CombatStartingPower(catalog) == 17,
            "An actual chest reward increases power when equipped");
        chestProfile.owned.Add(gear.id);
        Check(chestProfile.CombatStartingPower(catalog) == 17, "Duplicate ownership cannot stack equipped power");
        Check(profile.NextCombatStartingPower(catalog) == (profile.NextStartingPower * (100 + profile.EquipmentBonusPercent(catalog)) + 99) / 100,
            "Training preview includes equipment power");

        // Exercise the real reset in one synchronous call, then restore every original key.
        string[] strings = { HeroProfile.SaveKey, HeroProfile.SaveKey + ".backup", "TowerTrials.Run.v1", "TowerTrials.Run.v1.backup" };
        string[] integers = { CampaignConfig.SelectedStageKey, "HeroWars.LearnedRules" };
        var existed = new Dictionary<string, bool>();
        var text = new Dictionary<string, string>();
        var numbers = new Dictionary<string, int>();
        foreach (string key in strings) { existed[key] = PlayerPrefs.HasKey(key); text[key] = PlayerPrefs.GetString(key, ""); }
        foreach (string key in integers) { existed[key] = PlayerPrefs.HasKey(key); numbers[key] = PlayerPrefs.GetInt(key, 0); }
        bool sound = GamePreferences.Sound;
        try
        {
            profile.gold = 9999; profile.gems = 999; profile.goldRank = 12; profile.completedStages.Add(0); profile.Save();
            PlayerPrefs.SetString("TowerTrials.Run.v1", "old checkpoint");
            PlayerPrefs.SetString("TowerTrials.Run.v1.backup", "old checkpoint");
            PlayerPrefs.SetInt(CampaignConfig.SelectedStageKey, 10); PlayerPrefs.SetInt("HeroWars.LearnedRules", 1);
            HeroProfile fresh = GameSave.ResetForDevelopment(catalog);
            Check(fresh.gold == 0 && fresh.gems == 0 && fresh.level == 1 && fresh.powerRank == 0 && fresh.goldRank == 0 && fresh.CombatStartingPower(catalog) == 12,
                "Dev reset starts at zero currency, rank zero, level one and power 12");
            Check(fresh.owned.Count == 3 && fresh.owned.All(id => catalog.Find(id).starter) && fresh.completedStages.Count == 0,
                "Dev reset restores only starter equipment and clears completed stages");
            Check(fresh.combatRewardedDefeats == 0 && string.IsNullOrEmpty(fresh.combatRunId) && string.IsNullOrEmpty(fresh.dailyClaimUtc),
                "Dev reset clears training reward and daily receipts");
            Check(!PlayerPrefs.HasKey("TowerTrials.Run.v1") && !PlayerPrefs.HasKey("TowerTrials.Run.v1.backup") &&
                !PlayerPrefs.HasKey(CampaignConfig.SelectedStageKey) && !PlayerPrefs.HasKey("HeroWars.LearnedRules"),
                "Old checkpoints, backup, stage selection and tutorial flag are removed");
            var loaded = HeroProfile.Load(catalog);
            Check(loaded.gold == 0 && loaded.gems == 0 && loaded.HighestUnlockedStage == 0 && loaded.owned.Count == 3,
                "A fresh profile reload cannot recover old campaign progress");
            PlayerPrefs.DeleteKey(HeroProfile.SaveKey);
            loaded = HeroProfile.Load(catalog);
            Check(loaded.gold == 0 && loaded.powerRank == 0 && loaded.completedStages.Count == 0,
                "The backup also contains the new zero-progress profile");
            Check(GamePreferences.Sound == sound, "Reset preserves audio preferences");
        }
        finally
        {
            foreach (string key in strings) if (existed[key]) PlayerPrefs.SetString(key, text[key]); else PlayerPrefs.DeleteKey(key);
            foreach (string key in integers) if (existed[key]) PlayerPrefs.SetInt(key, numbers[key]); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
        Check(strings.All(key => PlayerPrefs.HasKey(key) == existed[key] && (!existed[key] || PlayerPrefs.GetString(key) == text[key])) &&
            integers.All(key => PlayerPrefs.HasKey(key) == existed[key] && (!existed[key] || PlayerPrefs.GetInt(key) == numbers[key])),
            "The check restored all original player data exactly");
        string report = "PASS: " + checks + " equipment/reset assertions. Rarity bonuses, every shield/weapon combination, training stacking, actual chest loot, duplicate protection, checkpoint persistence and zero-progress reset. Original player data restored.\n";
        Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/EquipmentPowerValidation.txt", report);
        Debug.Log(report); return report;
    }
}
