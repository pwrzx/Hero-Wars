using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TowerTrials;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class MobilePlayChecks
{
    private const string Prefix = "MobilePlayChecks.";
    private static readonly string[] Strings = { HeroProfile.SaveKey, HeroProfile.SaveKey + ".backup", "TowerTrials.Run.v1", "TowerTrials.Run.v1.backup" };
    private static readonly string[] Integers = { "HeroWars.Sound", "HeroWars.Music", "HeroWars.ReducedMotion", "HeroWars.BatterySaver", "HeroWars.Haptics", "HeroWars.LearnedRules", CampaignConfig.SelectedStageKey };
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static TowerGameController game;
    private static int step, assertions, attacks, pausedFrame, completedFrame;
    private static float pausedTime;
    private static Vector3 pausedPosition;
    private static double deadline;

    [InitializeOnLoadMethod]
    private static void RecoverInterruptedCheck()
    {
        if (SessionState.GetBool(Prefix + "Active", false)) Restore();
    }

    [MenuItem("Hero Wars/Release/Verify mobile loop (Play Mode)")]
    public static string Begin()
    {
        game = UnityEngine.Object.FindAnyObjectByType<TowerGameController>();
        if (!Application.isPlaying || game == null || game.Run == null || game.Busy) throw new InvalidOperationException("Start a ready gameplay scene first.");
        foreach (string key in Strings)
        {
            SessionState.SetBool(Prefix + key + ".exists", PlayerPrefs.HasKey(key));
            SessionState.SetString(Prefix + key, PlayerPrefs.GetString(key, ""));
        }
        foreach (string key in Integers)
        {
            SessionState.SetBool(Prefix + key + ".exists", PlayerPrefs.HasKey(key));
            SessionState.SetInt(Prefix + key, PlayerPrefs.GetInt(key, 0));
        }
        SessionState.SetBool(Prefix + "Active", true);
        PlayerPrefs.DeleteKey(CampaignConfig.SelectedStageKey);
        var fixture = new HeroProfile { progressionVersion = 1 }; fixture.Repair(HeroCenterCatalog.Load()); fixture.Save();
        typeof(TowerGameController).GetField("stageIndex", Private).SetValue(game, 0);
        game.SetPaused(false); game.RestartStage();
        step = assertions = attacks = 0;
        pausedFrame = Time.frameCount;
        deadline = EditorApplication.timeSinceStartup + 100;
        File.WriteAllText("Artifacts/MobilePlayValidation.txt", "RUNNING: pause, settings, interrupted flight, clear rewards, unlocks and journey navigation.");
        EditorApplication.update += Tick;
        return "Running the isolated mobile play loop; original saves and settings will be restored.";
    }

    private static void Tick()
    {
        try
        {
            Check(Application.isPlaying && EditorApplication.timeSinceStartup < deadline, "Mobile loop remains live within its deadline");
            switch (step)
            {
                case 0:
                    if (Time.frameCount < pausedFrame + 2) return;
                    Click("Pause");
                    Check(game.Paused && Time.timeScale == 0, "Pause button freezes simulation");
                    pausedFrame = Time.frameCount; pausedTime = Time.time; step++; break;
                case 1:
                    if (Time.frameCount < pausedFrame + 2) return;
                    Check(Time.time == pausedTime && !game.SubmitSwipe(Vector2.zero, Vector2.right * Screen.width), "Paused simulation cannot move or accept combat");
                    Click("GameplaySettings");
                    step++; break;
                case 2:
                    bool sound = GamePreferences.Sound;
                    Click("SoundToggle"); Check(GamePreferences.Sound != sound, "Sound preference changes"); Click("SoundToggle");
                    bool motion = GamePreferences.ReducedMotion;
                    Click("ReducedMotionToggle"); Check(GamePreferences.ReducedMotion != motion, "Reduced-motion preference changes"); Click("ReducedMotionToggle");
                    Click("CloseSettings"); step++; break;
                case 3:
                    Click("Resume");
                    Check(!game.Paused && Time.timeScale == 1, "Resume restores simulation time");
                    SendSwipe(WinningRoom()); attacks++;
                    typeof(TowerGameController).GetMethod("OnApplicationPause", Private).Invoke(game, new object[] { true });
                    Check(game.Paused, "Backgrounding pauses an in-flight attack");
                    pausedPosition = game.transform.Find("Presentation/Knight").position;
                    pausedFrame = Time.frameCount; step++; break;
                case 4:
                    if (Time.frameCount < pausedFrame + 2) return;
                    Check(game.transform.Find("Presentation/Knight").position == pausedPosition, "Background pause preserves the exact attack position");
                    Click("Resume"); step++; break;
                case 5:
                    if (game.Busy) return;
                    if (!game.Run.Complete) { SendSwipe(WinningRoom()); attacks++; return; }
                    HeroProfile earned = HeroProfile.Load(HeroCenterCatalog.Load());
                    Check(earned.gold == 2500 + CampaignConfig.GoldReward(0) && earned.gems == 150 + CampaignConfig.GemReward(0), "First clear pays exact gold and gems");
                    Check(earned.completedStages.SequenceEqual(new[] { 0 }) && earned.HighestUnlockedStage == 1, "Keep two unlocks after completing keep one");
                    Check(!earned.ClaimStageReward(0), "Reopening a completed keep cannot duplicate rewards");
                    ScreenCapture.CaptureScreenshot("Artifacts/Release-StageCleared.png");
                    completedFrame = Time.frameCount; step++; break;
                case 6:
                    if (Time.frameCount < completedFrame + 3) return;
                    Click("Continue");
                    Check(game.StageIndex == 1 && !game.Run.Complete, "Next keep enters a fresh playable stage");
                    step++; break;
                case 7:
                    Click("HeroCenterReturn"); step++; break;
                case 8:
                    if (SceneManager.GetActiveScene().name != "HeroCenter") return;
                    HeroCenterController menu = UnityEngine.Object.FindAnyObjectByType<HeroCenterController>();
                    if (menu == null || menu.Profile == null) return;
                    Click("Journey");
                    step++; break;
                case 9:
                    HeroCenterController journey = UnityEngine.Object.FindAnyObjectByType<HeroCenterController>();
                    var stages = journey.GetComponentsInChildren<UnityEngine.UI.Button>().Where(button => button.name.StartsWith("SelectStage_")).ToArray();
                    Check(stages.Length == 24 && stages.Count(button => button.interactable) == 2, "Journey shows 24 keeps with exactly two unlocked");
                    Click("SelectStage_1");
                    step++; break;
                case 10:
                    if (SceneManager.GetActiveScene().name == "HeroCenter" && GameObject.Find("BeginAdventure") != null)
                    { Click("BeginAdventure"); return; }
                    if (SceneManager.GetActiveScene().name != "TowerPrototype") return;
                    game = UnityEngine.Object.FindAnyObjectByType<TowerGameController>();
                    if (game == null || game.Run == null) return;
                    Check(game.StageIndex == 1 && !PlayerPrefs.HasKey(CampaignConfig.SelectedStageKey), "Journey selection is consumed exactly once");
                    Finish(null); break;
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static int WinningRoom()
    {
        long[] enemies = game.Run.Definition.towers[game.Run.TowerIndex].enemies;
        for (int i = 0; i < enemies.Length; i++) if (!game.Run.IsDefeated(game.Run.TowerIndex, i) && enemies[i] < game.Run.Power) return i;
        throw new Exception("Expected a winning route");
    }
    private static void SendSwipe(int room)
    {
        var input = UnityEngine.Object.FindAnyObjectByType<SwipeAttackInput>();
        Vector2 end = game.EnemyScreenPosition(room);
        var pointer = new PointerEventData(EventSystem.current) { pointerId = -1, position = end - Vector2.right * Screen.width * 0.4f };
        ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        pointer.position = end;
        ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        Check(game.Busy, "Pointer swipe starts exactly one attack");
    }
    private static void Click(string name)
    {
        Canvas.ForceUpdateCanvases();
        GameObject target = GameObject.Find(name);
        Check(target != null, "Control exists: " + name);
        var rect = (RectTransform)target.transform;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && (hits[0].gameObject == target || hits[0].gameObject.transform.IsChildOf(target.transform)), "Control receives the top pointer hit: " + name);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
    }
    private static void Check(bool valid, string message) { assertions++; if (!valid) throw new Exception(message); }
    private static void Restore()
    {
        foreach (string key in Strings)
            if (SessionState.GetBool(Prefix + key + ".exists", false)) PlayerPrefs.SetString(key, SessionState.GetString(Prefix + key, "")); else PlayerPrefs.DeleteKey(key);
        foreach (string key in Integers)
            if (SessionState.GetBool(Prefix + key + ".exists", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(Prefix + key, 0)); else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save(); SessionState.SetBool(Prefix + "Active", false);
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        if (game != null) game.SetPaused(false);
        Restore(); GamePreferences.Apply();
        string report = error == null ? "PASS: " + assertions + " checks and " + attacks + " real pointer attacks. Pause/resume, background interruption, persistent settings, first-clear rewards, duplicate protection, next-stage unlock, 24-keep journey and stage selection. Original saves and settings restored.\n" : "FAIL: " + error;
        File.WriteAllText("Artifacts/MobilePlayValidation.txt", report);
        if (error != null) Debug.LogException(error); else Debug.Log(report);
        if (Application.isPlaying) SceneManager.LoadSceneAsync("HeroCenter");
    }
}
