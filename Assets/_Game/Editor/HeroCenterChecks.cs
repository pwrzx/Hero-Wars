using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TowerTrials;
using UnityEditor;
using UnityEngine;

public static class HeroCenterChecks
{
    [MenuItem("Tower Trials/Verify Hero Center")]
    public static void Verify()
    {
        HeroCenterCatalog catalog = HeroCenterCatalog.Load();
        int assertions = 0;
        void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
        Check(catalog != null, "Catalog exists");
        Check(catalog.equipment.Select(item => item.id).Distinct().Count() == catalog.equipment.Length, "Unique equipment IDs");
        Check(catalog.equipment.All(item => item.prefab != null && item.icon != null), "Every item references imported art and a rendered icon");
        Check(catalog.equipment.Count(item => item.slot == EquipmentSlot.Skin && item.starter) == 1, "One unlocked starter hero");
        Check(catalog.equipment.Where(item => item.slot == EquipmentSlot.Skin && !item.starter).All(item => item.rarity == ItemRarity.Rare || item.rarity == ItemRarity.Legendary), "Other heroes are Rare or Legendary");
        Check(catalog.chests.Length == 4, "Four chest tiers");
        HeroProfile profile = new HeroProfile(); profile.Repair(catalog);
        Check(profile.owned.Count == 3, "Starter skin, armor and weapon are owned");
        Check(!profile.TryEquip(catalog.Find("skin.fire")), "A locked hero cannot be equipped");
        profile.gold = 0; profile.gems = 0;
        Check(!profile.TryOpenChest(catalog, catalog.chests[0], new System.Random(7), out _, out _, out _), "Insufficient funds reject the purchase");
        Check(profile.gold == 0 && profile.gems == 0 && profile.owned.Count == 3, "Rejected purchase makes no changes");
        foreach (HeroChest chest in catalog.chests)
        {
            Check(chest.weights.Sum() == 100 && chest.weights.All(weight => weight >= 0), "Valid visible probabilities: " + chest.id);
            for (int rarity = 0; rarity < 4; rarity++)
                if (chest.weights[rarity] > 0) Check(catalog.equipment.Any(item => !item.starter && (int)item.rarity == rarity), "Populated reward pool");
            var random = new System.Random(812);
            for (int roll = 0; roll < 400; roll++)
            {
                profile.gold = profile.gems = 100000;
                int beforeOwned = profile.owned.Count;
                Check(profile.TryOpenChest(catalog, chest, random, out HeroEquipment reward, out bool duplicate, out int compensation), "Affordable chest opens");
                Check(chest.weights[(int)reward.rarity] > 0 && !reward.starter, "Reward respects published rarity pool");
                Check(profile.Owns(reward.id), "Reward is in inventory");
                Check(profile.owned.Count == beforeOwned + (duplicate ? 0 : 1), "Duplicates do not add repeated IDs");
                Check(profile.gems == 100000 - (chest.costsGems ? chest.price : 0), "Exact gem debit");
                Check(profile.gold == 100000 - (chest.costsGems ? 0 : chest.price) + compensation, "Exact gold debit and duplicate credit");
            }
        }
        foreach (HeroEquipment item in catalog.equipment)
        {
            profile.owned.Add(item.id); Check(profile.TryEquip(item), "Owned item equips");
            Check(profile.Equipped(item.slot) == item.id, "Correct slot updates");
        }
        HeroProfile restored = JsonUtility.FromJson<HeroProfile>(JsonUtility.ToJson(profile)); restored.Repair(catalog);
        Check(restored.skin == profile.skin && restored.armor == profile.armor && restored.weapon == profile.weapon, "Loadout survives serialization");
        Check(restored.owned.Distinct().Count() == restored.owned.Count, "Save repair removes duplicates");
        restored.skin = "unknown"; restored.weapon = "skin.fire"; restored.gold = -25; restored.owned.Add("missing"); restored.Repair(catalog);
        Check(restored.gold == 0 && catalog.Find(restored.skin).slot == EquipmentSlot.Skin && catalog.Find(restored.weapon).slot == EquipmentSlot.Weapon, "Damaged saves repair safely");
        DateTime day = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        Check(restored.TryClaimDaily(day), "First daily claim succeeds");
        int gold = restored.gold, gems = restored.gems;
        Check(!restored.TryClaimDaily(day.AddHours(2)) && restored.gold == gold && restored.gems == gems, "Daily reward cannot be claimed twice");
        Check(restored.TryClaimDaily(day.AddDays(1)), "Next UTC day resets the gift");
        Check(UnityEditor.EditorBuildSettings.scenes[0].path == HeroCenterSetup.ScenePath, "Hero Center is the startup scene");
        string report = "PASS: " + assertions + " Hero Center assertions.\nCatalog references, starter/locked heroes, rarity pools, 1,600 seeded chest openings, exact currency transactions, duplicates, equipment gates, save repair, daily gifts and startup scene.\n";
        Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/HeroCenterValidation.txt", report); Debug.Log(report);
    }
}
