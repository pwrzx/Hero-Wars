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
        VerifyEquipment(catalog, Check);
        string report = "PASS: " + assertions + " Hero Center assertions.\nCatalog references, starter/locked heroes, rarity pools, 1,600 seeded chest openings, exact currency transactions, duplicates, equipment gates, save repair, daily gifts and startup scene.\nAll four hero rigs, nine weapons and three shields at portrait/gameplay sizes with rotated parents; closed idle grips; handle alignment through idle, running, attack, jump and death animations.\n";
        Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/HeroCenterValidation.txt", report); Debug.Log(report);
    }

    private static void VerifyEquipment(HeroCenterCatalog catalog, Action<bool, string> check)
    {
        check(catalog.armedAnimatorController != null, "Armed animation controller exists");
        HeroEquipment[] weapons = catalog.equipment.Where(item => item.slot == EquipmentSlot.Weapon).ToArray();
        HeroEquipment[] shields = catalog.equipment.Where(item => item.slot == EquipmentSlot.Armor && !item.cloak).ToArray();
        var owner = new GameObject("EquipmentCheckRoot");
        owner.transform.position = new Vector3(1000, 0, 0);
        try
        {
            foreach (HeroEquipment skin in catalog.equipment.Where(item => item.slot == EquipmentSlot.Skin))
                foreach (float height in new[] { 1.65f, 3.15f })
                    for (int i = 0; i < weapons.Length; i++)
                    {
                        HeroEquipment weapon = weapons[i], shield = shields[i % shields.Length];
                        // An equipment length must be independent of both the avatar pose and its parent's yaw.
                        owner.transform.rotation = Quaternion.Euler(0, i % 2 == 0 ? 0 : 137, 0);
                        var profile = new HeroProfile { skin = skin.id, weapon = weapon.id, armor = shield.id };
                        GameObject hero = HeroAppearance.Create(catalog, profile, owner.transform, height);
                        try
                        {
                            var bones = hero.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(group => group.Key, group => group.First());
                            Animator animator = hero.GetComponentInChildren<Animator>();
                            string context = skin.id + " / " + weapon.id + " / " + shield.id + " / " + height;
                            check(animator.isHuman && animator.runtimeAnimatorController == catalog.armedAnimatorController, "Armed humanoid animations: " + context);
                            foreach (HeroEquipment item in new[] { weapon, shield })
                            {
                                Transform equipment = bones["Equipped_" + item.id];
                                string side = item.slot == EquipmentSlot.Weapon ? "R" : "L";
                                check(equipment.parent == bones["CC_Base_" + side + "_Hand"], "Equipment is parented to the correct hand: " + context);
                                float length = equipment.TransformVector(Vector3.up * HeroAppearance.LocalBoundsOf(equipment.gameObject).size.y).magnitude;
                                check(Mathf.Abs(length / height - (item.slot == EquipmentSlot.Weapon ? 0.43f : 0.29f)) < 0.005f, "Equipment keeps its intended size: " + context);
                                if (item.slot == EquipmentSlot.Armor)
                                {
                                    check(equipment.Find("HandGrip") != null, "Shield has a rear handle: " + context);
                                    float rear = equipment.GetComponent<Renderer>().localBounds.min.z;
                                    float clearance = equipment.TransformVector(Vector3.forward * (rear - item.gripPoint.z)).magnitude;
                                    check(clearance > height * 0.025f, "Shield leaves room for fingers and gauntlets: " + context);
                                }
                            }
                            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips.Distinct())
                                foreach (float fraction in new[] { 0.1f, 0.5f, 0.9f })
                                {
                                    clip.SampleAnimation(animator.gameObject, clip.length * fraction);
                                    foreach (HeroEquipment item in new[] { weapon, shield })
                                    {
                                        string side = item.slot == EquipmentSlot.Weapon ? "R" : "L";
                                        Transform hand = bones["CC_Base_" + side + "_Hand"];
                                        Transform knuckle = bones["CC_Base_" + side + "_Mid1"];
                                        Vector3 anchor = bones["Equipped_" + item.id].TransformPoint(item.gripPoint);
                                        float palmLength = Vector3.Distance(hand.position, knuckle.position);
                                        float alongPalm = Vector3.Dot(anchor - hand.position, hand.up);
                                        check(alongPalm > palmLength * 0.6f && alongPalm < palmLength * 1.1f &&
                                            Vector3.Distance(anchor, knuckle.position) < palmLength * 0.6f,
                                            "Handle stays inside the palm during " + clip.name + ": " + context);
                                    }
                                    if (clip.name.Contains("Idle"))
                                    {
                                        foreach (string side in new[] { "R", "L" })
                                            check(Vector3.Dot(bones["CC_Base_" + side + "_Hand"].up, bones["CC_Base_" + side + "_Index1"].up) < 0.65f,
                                                "Equipped idle closes the " + side + " hand: " + context);
                                        check(Vector3.Dot(bones["Equipped_" + shield.id].forward, hero.transform.forward) > 0.65f,
                                            "Shield faces outward in the ready pose: " + context);
                                    }
                                }
                            check(!hero.GetComponentsInChildren<Renderer>().Any(r => r.name.IndexOf("wand", StringComparison.OrdinalIgnoreCase) >= 0 || r.name.IndexOf("sword_fire", StringComparison.OrdinalIgnoreCase) >= 0),
                                "Original weapon stays hidden: " + context);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(hero); }
                    }
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }
}
