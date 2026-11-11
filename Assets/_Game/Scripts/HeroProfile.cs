using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TowerTrials
{
    [Serializable]
    public sealed class HeroProfile
    {
        public const string SaveKey = "HeroWars.HeroProfile.v1";
        public int version = 1, gold = 2500, gems = 150, level = 1;
        public string skin = "skin.nature", armor = "armor.oak", weapon = "weapon.sword1";
        public string dailyClaimUtc = "";
        public List<string> owned = new List<string>();
        public List<int> completedStages = new List<int>();
        public int progressionVersion;
        public int HighestUnlockedStage => Mathf.Min(CampaignConfig.StageCount - 1,
            completedStages.Count == 0 ? 0 : completedStages[completedStages.Count - 1] + 1);

        public static HeroProfile Load(HeroCenterCatalog catalog)
        {
            HeroProfile profile = GameSave.Read<HeroProfile>(SaveKey, value => value.version == 1);
            if (profile == null || profile.version != 1) profile = new HeroProfile();
            profile.Repair(catalog);
            if (profile.progressionVersion == 0)
            {
                RunSave previous = GameSave.Read<RunSave>("TowerTrials.Run.v1", value => value.version == 1);
                if (previous != null)
                    for (int i = 0; i < Mathf.Clamp(previous.stageIndex, 0, CampaignConfig.StageCount - 1); i++)
                        if (!profile.completedStages.Contains(i)) profile.completedStages.Add(i);
                profile.completedStages.Sort();
                profile.progressionVersion = 1;
            }
            return profile;
        }

        public void Repair(HeroCenterCatalog catalog)
        {
            gold = Mathf.Clamp(gold, 0, 1000000000);
            gems = Mathf.Clamp(gems, 0, 1000000000);
            level = Mathf.Clamp(level, 1, 999);
            if (owned == null) owned = new List<string>();
            var unique = new HashSet<string>();
            owned.RemoveAll(id => catalog.Find(id) == null || !unique.Add(id));
            foreach (HeroEquipment item in catalog.equipment)
                if (item.starter && !owned.Contains(item.id)) owned.Add(item.id);
            skin = ValidSelection(catalog, skin, EquipmentSlot.Skin);
            armor = ValidSelection(catalog, armor, EquipmentSlot.Armor);
            weapon = ValidSelection(catalog, weapon, EquipmentSlot.Weapon);
            if (completedStages == null) completedStages = new List<int>();
            var stages = new HashSet<int>();
            completedStages.RemoveAll(index => index < 0 || index >= CampaignConfig.StageCount || !stages.Add(index));
            completedStages.Sort();
            level = Mathf.Clamp(1 + completedStages.Count / 3, 1, 999);
        }

        public bool ClaimStageReward(int stageIndex)
        {
            if (stageIndex < 0 || stageIndex >= CampaignConfig.StageCount || completedStages.Contains(stageIndex)) return false;
            completedStages.Add(stageIndex); completedStages.Sort();
            gold = (int)Math.Min(1000000000L, (long)gold + CampaignConfig.GoldReward(stageIndex));
            gems = (int)Math.Min(1000000000L, (long)gems + CampaignConfig.GemReward(stageIndex));
            level = 1 + completedStages.Count / 3;
            return true;
        }

        private string ValidSelection(HeroCenterCatalog catalog, string id, EquipmentSlot slot)
        {
            HeroEquipment current = catalog.Find(id);
            if (current != null && current.slot == slot && Owns(id)) return id;
            foreach (HeroEquipment item in catalog.equipment)
                if (item.slot == slot && Owns(item.id)) return item.id;
            return "";
        }

        public bool Owns(string id) => owned.Contains(id);
        public string Equipped(EquipmentSlot slot) => slot == EquipmentSlot.Skin ? skin : slot == EquipmentSlot.Armor ? armor : weapon;

        public bool TryEquip(HeroEquipment item)
        {
            if (item == null || !Owns(item.id)) return false;
            if (item.slot == EquipmentSlot.Skin) skin = item.id;
            else if (item.slot == EquipmentSlot.Armor) armor = item.id;
            else weapon = item.id;
            return true;
        }

        public bool TryOpenChest(HeroCenterCatalog catalog, HeroChest chest, System.Random random,
            out HeroEquipment reward, out bool duplicate, out int compensation)
        {
            reward = null; duplicate = false; compensation = 0;
            if (chest == null || chest.price < 0 || chest.weights == null || chest.weights.Length != 4 ||
                (chest.costsGems ? gems : gold) < chest.price) return false;
            int total = 0;
            foreach (int weight in chest.weights) { if (weight < 0) return false; total += weight; }
            if (total <= 0) return false;
            int roll = random.Next(total), selectedRarity = 0;
            for (; selectedRarity < 3; selectedRarity++)
            {
                if (roll < chest.weights[selectedRarity]) break;
                roll -= chest.weights[selectedRarity];
            }
            var pool = new List<HeroEquipment>();
            foreach (HeroEquipment item in catalog.equipment)
                if (!item.starter && (int)item.rarity == selectedRarity) pool.Add(item);
            if (pool.Count == 0) return false;
            reward = pool[random.Next(pool.Count)];
            if (chest.costsGems) gems -= chest.price; else gold -= chest.price;
            duplicate = Owns(reward.id);
            if (duplicate)
            {
                compensation = new[] { 100, 250, 600, 1200 }[selectedRarity];
                gold = (int)Math.Min(1000000000L, (long)gold + compensation);
            }
            else owned.Add(reward.id);
            return true;
        }

        public bool CanClaimDaily(DateTime utcNow) => dailyClaimUtc != utcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public bool TryClaimDaily(DateTime utcNow)
        {
            if (!CanClaimDaily(utcNow)) return false;
            dailyClaimUtc = utcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            gold = (int)Math.Min(1000000000L, (long)gold + 300);
            gems = (int)Math.Min(1000000000L, (long)gems + 15);
            return true;
        }

        public int EquipmentPower(HeroCenterCatalog catalog)
        {
            int total = 0;
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                total += catalog.Find(Equipped(slot))?.power ?? 0;
            return total;
        }

        public void Save()
        {
            GameSave.Write(SaveKey, this);
        }
    }
}
