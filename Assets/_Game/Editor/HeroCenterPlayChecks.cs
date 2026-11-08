using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TowerTrials;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class HeroCenterPlayChecks
{
    private const string Prefix = "HeroCenterPlayChecks.";
    private static int step, assertions;
    private static double deadline;
    private static string rewardId;
    private static bool running;

    [MenuItem("Tower Trials/Verify Hero Center (Play Mode)")]
    public static string Begin()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "HeroCenter")
            throw new InvalidOperationException("Enter Play mode in HeroCenter first.");
        if (running) throw new InvalidOperationException("The menu playtest is already running.");
        Snapshot(HeroProfile.SaveKey); Snapshot("TowerTrials.Run.v1");
        SessionState.SetBool(Prefix + "HasSnapshot", true);
        HeroCenterController menu = UnityEngine.Object.FindAnyObjectByType<HeroCenterController>();
        var fixture = new HeroProfile(); fixture.Repair(menu.Catalog);
        typeof(HeroCenterController).GetField("profile", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(menu, fixture);
        menu.ShowPage(0);
        typeof(HeroCenterController).GetMethod("RefreshWallet", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(menu, null);
        step = 0; assertions = 0; rewardId = null; running = true;
        deadline = EditorApplication.timeSinceStartup + 35;
        File.WriteAllText("Artifacts/HeroCenterPlayValidation.txt", "RUNNING: isolated Hero Center interaction and scene round-trip checks.");
        EditorApplication.update += Tick;
        return "Started pointer, purchase, equip, persistence and scene-transition checks with isolated player data.";
    }

    private static void Tick()
    {
        try
        {
            if (!Application.isPlaying) throw new Exception("Play mode ended before verification completed.");
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Hero Center playtest exceeded 35 seconds.");
            HeroCenterController menu = UnityEngine.Object.FindAnyObjectByType<HeroCenterController>();
            switch (step)
            {
                case 0:
                    Check(UnityEngine.Object.FindObjectsByType<EventSystem>().Length == 1, "One EventSystem");
                    CheckPortrait();
                    Click("GoToCustomize"); step++; break;
                case 1:
                    Check(menu.ActivePage == 1, "Customize navigation works through pointer events");
                    Click("Item_skin.ice");
                    Check(!menu.Equip("skin.ice") && menu.Profile.skin == "skin.nature", "Locked preview does not equip or unlock");
                    Check(UnityEngine.Object.FindAnyObjectByType<HeroPreview>().Hero.name.Contains("skin.ice"), "Locked skin renders in preview");
                    Click("GoToShop"); step++; break;
                case 2:
                    Check(menu.ActivePage == 2, "Shop navigation works");
                    Check(!UnityEngine.Object.FindAnyObjectByType<HeroPreview>(FindObjectsInactive.Include).gameObject.activeInHierarchy, "Portrait stops rendering on Shop");
                    Click("Open_chest.legendary"); step++; break;
                case 3:
                    if (menu.OpeningChest) return;
                    Check(menu.Profile.gems == 30, "Legendary chest debits exactly 120 gems");
                    Check(menu.Profile.owned.Count == 4, "Chest adds exactly one reward");
                    rewardId = menu.Profile.owned.Last();
                    Check(menu.Catalog.Find(rewardId).rarity == ItemRarity.Legendary, "Legendary chest gives a Legendary item");
                    Click("EquipReward"); step++; break;
                case 4:
                    HeroEquipment reward = menu.Catalog.Find(rewardId);
                    Check(menu.ActivePage == 1 && menu.Profile.Equipped(reward.slot) == rewardId, "Equip now updates loadout and opens the correct inventory");
                    Check(HeroProfile.Load(menu.Catalog).Equipped(reward.slot) == rewardId, "Equipped reward is persisted");
                    Click("GoToHeroCenter"); Click("DailyGift");
                    Check(menu.Profile.gold == 2800 && menu.Profile.gems == 45, "Daily gift credits both currencies");
                    Click("DailyGift");
                    Check(menu.Profile.gold == 2800 && menu.Profile.gems == 45, "Second daily click gives no extra currency");
                    Click("Adventure"); step++; break;
                case 5:
                    if (SceneManager.GetActiveScene().name != "TowerPrototype") return;
                    TowerGameController game = UnityEngine.Object.FindAnyObjectByType<TowerGameController>();
                    if (game == null || game.Run == null) return;
                    Check(UnityEngine.Object.FindObjectsByType<CharacterMotion>().Any(motion => motion.HasImportedVisual), "Equipped Hero Pack model is used in tower gameplay");
                    Check(UnityEngine.Object.FindObjectsByType<EventSystem>().Length == 1, "Scene change preserves a single EventSystem");
                    Click("HeroCenterReturn"); step++; break;
                case 6:
                    if (SceneManager.GetActiveScene().name != "HeroCenter" || menu == null || menu.Profile == null) return;
                    HeroEquipment equipped = menu.Catalog.Find(rewardId);
                    Check(menu.Profile.Equipped(equipped.slot) == rewardId && menu.Profile.gems == 45, "Loadout and balances survive the round trip");
                    CheckPortrait();
                    RestorePlayerData(); running = false; EditorApplication.update -= Tick;
                    string report = "PASS: " + assertions + " live checks.\nReal UI raycasts and pointer handlers; locked hero preview; chest payment/reveal; reward equip; save restore; daily claim protection; adventure; imported hero; Home return.\nOriginal player and campaign saves restored.\n";
                    File.WriteAllText("Artifacts/HeroCenterPlayValidation.txt", report); Debug.Log(report);
                    SceneManager.LoadSceneAsync("HeroCenter");
                    break;
            }
        }
        catch (Exception exception)
        {
            running = false; EditorApplication.update -= Tick; RestorePlayerData();
            File.WriteAllText("Artifacts/HeroCenterPlayValidation.txt", "FAIL at step " + step + ": " + exception + "\nOriginal saves restored.");
            Debug.LogException(exception);
        }
    }

    private static void Click(string name)
    {
        Canvas.ForceUpdateCanvases();
        GameObject target = GameObject.Find(name);
        Check(target != null, "Button exists: " + name);
        var rect = target.GetComponent<RectTransform>();
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && (hits[0].gameObject == target || hits[0].gameObject.transform.IsChildOf(target.transform)), "Button is the top raycast target: " + name);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new Exception(message); }

    private static void CheckPortrait()
    {
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>().Where(camera => camera.name == "HeroPortraitCamera").ToArray();
        Check(cameras.Length == 1 && cameras[0].targetTexture != null, "One portrait camera, rendering only to its texture");
        HeroPreview portrait = UnityEngine.Object.FindAnyObjectByType<HeroPreview>();
        Vector3 center = cameras[0].WorldToViewportPoint(HeroAppearance.BoundsOf(portrait.Hero).center);
        Check(center.x > 0.30f && center.x < 0.70f && center.y > 0.25f && center.y < 0.75f, "Hero remains centered in its portrait");
    }

    private static void Snapshot(string key)
    {
        SessionState.SetBool(Prefix + key + ".exists", PlayerPrefs.HasKey(key));
        SessionState.SetString(Prefix + key, PlayerPrefs.GetString(key, ""));
    }

    public static void RestorePlayerData()
    {
        if (!SessionState.GetBool(Prefix + "HasSnapshot", false)) return;
        foreach (string key in new[] { HeroProfile.SaveKey, "TowerTrials.Run.v1" })
        {
            if (SessionState.GetBool(Prefix + key + ".exists", false)) PlayerPrefs.SetString(key, SessionState.GetString(Prefix + key, ""));
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        SessionState.SetBool(Prefix + "HasSnapshot", false);
    }
}
