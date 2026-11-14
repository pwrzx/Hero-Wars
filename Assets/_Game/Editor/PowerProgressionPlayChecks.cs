using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TowerTrials;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class PowerProgressionPlayChecks
{
    private const string Prefix = "PowerProgressionPlayChecks.";
    private static readonly string[] Keys = { HeroProfile.SaveKey, HeroProfile.SaveKey + ".backup", "TowerTrials.Run.v1", "TowerTrials.Run.v1.backup" };
    private static int phase, frame, checks, upgrades, spent, wallDefeats;
    private static string signature;
    private static double deadline;

    [InitializeOnLoadMethod]
    private static void RecoverInterruptedCheck()
    { if (SessionState.GetBool(Prefix + "Active", false)) Restore(); }

    [MenuItem("Tower Trials/Verify Power Progression (Play Mode)")]
    public static string Begin()
    {
        if (!Application.isPlaying || SessionState.GetBool(Prefix + "Active", false))
            throw new InvalidOperationException("Start a Play session with no progression check already running.");
        if (SceneManager.GetActiveScene().name == "Startup")
            throw new InvalidOperationException("Wait for Startup to finish loading Hero Center before starting the check.");
        foreach (string key in Keys)
        {
            SessionState.SetBool(Prefix + key + ".exists", PlayerPrefs.HasKey(key));
            SessionState.SetString(Prefix + key, PlayerPrefs.GetString(key, ""));
        }
        foreach (string key in new[] { CampaignConfig.SelectedStageKey, "HeroWars.LearnedRules" })
        {
            SessionState.SetBool(Prefix + key + ".exists", PlayerPrefs.HasKey(key));
            SessionState.SetInt(Prefix + key, PlayerPrefs.GetInt(key, 0));
        }
        SessionState.SetBool(Prefix + "Active", true);
        new HeroProfile { progressionVersion = 1 }.Save();
        PlayerPrefs.SetInt(CampaignConfig.SelectedStageKey, 0);
        PlayerPrefs.SetInt("HeroWars.LearnedRules", 1);
        phase = checks = upgrades = spent = wallDefeats = 0;
        signature = null;
        frame = Time.frameCount; deadline = EditorApplication.timeSinceStartup + 180;
        File.WriteAllText("Artifacts/PowerProgressionPlayValidation.txt", "RUNNING: fixed enemies, wall, menu training, upgraded retry and five-tower clear.");
        EditorApplication.update += Tick;
        SceneManager.LoadSceneAsync("TowerPrototype");
        return "Running isolated progression playtest; original player saves will be restored.";
    }

    private static void Tick()
    {
        try
        {
            if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new Exception("Progression playtest stopped or timed out.");
            if (Time.frameCount < frame + 3) return;
            var game = UnityEngine.Object.FindAnyObjectByType<TowerGameController>();
            var menu = UnityEngine.Object.FindAnyObjectByType<HeroCenterController>();
            if (game != null && game.Busy) return;
            switch (phase)
            {
                case 0:
                    if (game == null || game.Run == null) return;
                    Check(game.Run.StartingPower == 12, "Fresh hero starts with 12 power");
                    if (signature == null) signature = game.Run.Definition.Signature;
                    if (!game.Run.PowerBlocked) { Swipe(game); break; }
                    Check(!game.Run.Complete && game.Run.TowerIndex == 2, "Base hero hits the tower-three guard wall");
                    wallDefeats = game.Run.DefeatedCount;
                    Check(StageRun.TryRestore(game.Run.Definition, game.Run.Save(0), out var blocked) && blocked.PowerBlocked,
                        "The actual blocked checkpoint restores");
                    Check(HeroProfile.Load(HeroCenterCatalog.Load()).gold == 2500 + wallDefeats * CampaignConfig.DefeatGoldReward(0), "Failed attempt earns exact kill gold");
                    if (!game.Run.Failed) { Swipe(game, true); break; }
                    ScreenCapture.CaptureScreenshot("Artifacts/PowerProgression-Wall.png");
                    Click("Continue"); phase++; break;
                case 1:
                    if (menu == null || menu.Profile == null) return;
                    Check(menu.Profile.powerRank == 0 && menu.Profile.StartingPower == 12, "Menu shows the untrained hero");
                    Click("PowerTraining"); phase++; break;
                case 2:
                    if (menu == null) return;
                    if (menu.Profile.StartingPower < 24)
                    {
                        long previous = menu.Profile.StartingPower;
                        int cost = menu.Profile.PowerUpgradeCost;
                        Click("UpgradePower"); spent += cost; upgrades++;
                        Check(menu.Profile.StartingPower == (previous * 120 + 99) / 100, "Real button compounds power by 20%");
                        Check(HeroProfile.Load(menu.Catalog).StartingPower == menu.Profile.StartingPower, "Training button saves permanent power immediately");
                        break;
                    }
                    Check(upgrades == 4 && menu.Profile.StartingPower == 27, "Four training ranks reach 27 starting power");
                    ScreenCapture.CaptureScreenshot("Artifacts/PowerProgression-Training.png");
                    phase = 5; break;
                case 5:
                    Click("CloseTraining"); Click("Adventure"); phase = 3; break;
                case 3:
                    if (game == null || game.Run == null) return;
                    Check(game.Run.StartingPower == 27 && game.Run.Power == 27 && game.Run.DefeatedCount == 0,
                        "Adventure restarts the keep with upgraded base power and reset kills");
                    Check(game.Run.Definition.Signature == signature, "Every enemy power stays unchanged after training and retry");
                    phase++; break;
                case 4:
                    if (game == null) return;
                    Check(!game.Run.PowerBlocked && !game.Run.Failed, "Trained hero has a winning move");
                    if (!game.Run.Complete) { Swipe(game); break; }
                    Check(game.Run.TowerIndex == 5 && game.Run.DefeatedCount == 15, "Real pointer combat clears all five towers");
                    var profile = HeroProfile.Load(HeroCenterCatalog.Load());
                    Check(profile.gold == 2500 - spent + (wallDefeats + 15) * CampaignConfig.DefeatGoldReward(0) + CampaignConfig.GoldReward(0),
                        "Final gold equals new kills plus one clear reward minus training costs");
                    Check(profile.powerRank == 4 && profile.completedStages.SequenceEqual(new[] { 0 }), "Permanent training and next-level unlock persist");
                    ScreenCapture.CaptureScreenshot("Artifacts/PowerProgression-Cleared.png");
                    phase = 6; break;
                case 6:
                    Finish(null); return;
            }
            frame = Time.frameCount;
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Swipe(TowerGameController game, bool stronger = false)
    {
        int tower = game.Run.TowerIndex;
        int room = Enumerable.Range(0, game.Run.Definition.towers[tower].enemies.Length)
            .Where(r => !game.Run.IsDefeated(tower, r) && (stronger ? game.Run.Definition.towers[tower].enemies[r] >= game.Run.Power : game.Run.Definition.towers[tower].enemies[r] < game.Run.Power))
            .OrderBy(r => game.Run.Definition.towers[tower].enemies[r]).First();
        var input = UnityEngine.Object.FindAnyObjectByType<SwipeAttackInput>();
        Vector2 end = game.EnemyScreenPosition(room);
        var pointer = new PointerEventData(EventSystem.current) { pointerId = -1, position = end - Vector2.right * Screen.width * 0.45f };
        ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        pointer.position = end; ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        Check(game.Busy, "Pointer starts combat");
    }

    private static void Click(string name)
    {
        var target = GameObject.Find(name);
        Check(target != null, "Control exists: " + name);
        var button = target.GetComponent<UnityEngine.UI.Button>();
        Check(button != null && button.interactable, "Control is usable: " + name);
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && (hits[0].gameObject == target || hits[0].gameObject.transform.IsChildOf(target.transform)), "Control receives a real pointer: " + name);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static void Check(bool valid, string message)
    { checks++; if (!valid) throw new Exception(message); }

    private static void Restore()
    {
        foreach (string key in Keys)
            if (SessionState.GetBool(Prefix + key + ".exists", false)) PlayerPrefs.SetString(key, SessionState.GetString(Prefix + key, ""));
            else PlayerPrefs.DeleteKey(key);
        foreach (string key in new[] { CampaignConfig.SelectedStageKey, "HeroWars.LearnedRules" })
            if (SessionState.GetBool(Prefix + key + ".exists", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(Prefix + key, 0));
            else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save(); SessionState.SetBool(Prefix + "Active", false);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        Restore(); signature = null;
        string result = error == null ? "PASS: " + checks + " live checks. Base hero reached a real wall; real menu clicks bought four 20% upgrades; fixed enemies survived retry; upgraded hero cleared five towers; exact gold and permanent progress persisted. Original player saves restored."
            : "FAIL: " + error;
        File.WriteAllText("Artifacts/PowerProgressionPlayValidation.txt", result);
        if (error != null) Debug.LogException(error); else Debug.Log(result);
        if (Application.isPlaying) SceneManager.LoadSceneAsync("HeroCenter");
    }
}
