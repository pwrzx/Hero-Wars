using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TowerTrials;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class RewardedContinuePlayChecks
{
    private const string Prefix = "RewardedContinuePlayChecks.";
    private static readonly string[] Saves = { HeroProfile.SaveKey, HeroProfile.SaveKey + ".backup", "TowerTrials.Run.v1", "TowerTrials.Run.v1.backup" };
    private static TowerGameController game;
    private static RewardedContinueChecks.Provider provider;
    private static StageRun failedRun;
    private static int step, frame, checks, attacks, gold, gems;
    private static long checkpointPower;
    private static int[] checkpointMasks;
    private static double deadline;

    [InitializeOnLoadMethod]
    private static void RecoverInterruptedCheck()
    { if (SessionState.GetBool(Prefix + "Active", false)) Restore(); }

    [MenuItem("Hero Wars/Release/Verify rewarded continue (Play Mode)")]
    public static string Begin()
    {
        game = UnityEngine.Object.FindAnyObjectByType<TowerGameController>();
        if (!Application.isPlaying || game == null || game.Run == null || game.Busy)
            throw new InvalidOperationException("Start a ready gameplay scene in Play mode first.");
        if (SessionState.GetBool(Prefix + "Active", false)) throw new InvalidOperationException("Check already running");
        foreach (string key in Saves)
        {
            SessionState.SetBool(Prefix + key + ".exists", PlayerPrefs.HasKey(key));
            SessionState.SetString(Prefix + key, PlayerPrefs.GetString(key, ""));
        }
        SessionState.SetBool(Prefix + "Active", true);
        var profile = HeroProfile.Load(HeroCenterCatalog.Load()); gold = profile.gold; gems = profile.gems;
        RewardedContinueAds.Register(null);
        typeof(TowerGameController).GetField("stageIndex", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, 0);
        game.SetPaused(false); game.RestartStage();
        step = checks = attacks = 0; frame = Time.frameCount;
        deadline = EditorApplication.timeSinceStartup + 110;
        File.WriteAllText("Artifacts/RewardedContinuePlayValidation.txt", "RUNNING: death options, retry, reward confirmation and retained progress.");
        EditorApplication.update += Tick;
        return "Testing rewarded-continue hooks with an Editor-only provider; no real ads or network requests.";
    }

    private static void Tick()
    {
        try
        {
            Check(Application.isPlaying && EditorApplication.timeSinceStartup < deadline, "Play check remains live");
            if (Time.frameCount < frame + 3 || game.Busy) return;
            switch (step)
            {
                case 0: Swipe(FindRoom(false)); step++; break;
                case 1: Swipe(FindRoom(true)); step++; break;
                case 2:
                    Check(game.Run.Failed, "Stronger enemy produces the death screen");
                    CaptureCheckpoint();
                    Check(!Control("WatchAdContinue").interactable && Control("Continue").interactable, "Reserved ad option is unavailable while Retry remains usable");
                    game.RequestRewardedContinue(); Check(game.Run.Failed, "No provider cannot grant a free continue");
                    CheckUiBounds();
                    ScreenCapture.CaptureScreenshot("Artifacts/Death-ContinueUnavailable.png");
                    provider = new RewardedContinueChecks.Provider(); RewardedContinueAds.Register(provider);
                    step++; break;
                case 3:
                    Check(Control("WatchAdContinue").interactable, "Loading an ad enables the reserved action");
                    ScreenCapture.CaptureScreenshot("Artifacts/Death-ContinueAvailable.png");
                    Click("WatchAdContinue"); game.RequestRewardedContinue();
                    Check(provider.callbacks.Count == 1 && !Control("WatchAdContinue").interactable, "Double requests show one ad");
                    provider.callbacks[0](RewardedContinueResult.Skipped); step++; break;
                case 4:
                    CheckUnchanged("Skipping"); Click("WatchAdContinue");
                    provider.callbacks[1](RewardedContinueResult.Failed); step++; break;
                case 5:
                    CheckUnchanged("Failed ad"); Click("WatchAdContinue"); Click("Continue");
                    Check(game.Run != failedRun && !game.Run.Failed && game.Run.Power == game.Run.Definition.startingPower, "Retry creates a fresh run");
                    step++; break;
                case 6:
                    provider.callbacks[2](RewardedContinueResult.Rewarded);
                    Check(!game.Run.Failed && game.Run.Power == game.Run.Definition.startingPower, "Reward from abandoned run cannot affect Retry");
                    Swipe(FindRoom(false)); step++; break;
                case 7: Swipe(FindRoom(true)); step++; break;
                case 8: CaptureCheckpoint(); Check(game.Run.Failed, "Second death still offers a continue"); step++; break;
                case 9:
                    Click("WatchAdContinue");
                    Task.Run(() => { provider.callbacks[3](RewardedContinueResult.Rewarded); provider.callbacks[3](RewardedContinueResult.Rewarded); }).GetAwaiter().GetResult();
                    step++; break;
                case 10:
                    Check(game.Run == failedRun && !game.Run.Failed && game.Run.Power == checkpointPower, "Confirmed reward revives the same checkpoint exactly once");
                    Check(game.Run.Save(0).clearedMasks.SequenceEqual(checkpointMasks), "Cleared rooms stay cleared after revive");
                    var overlay = game.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "ResultOverlay");
                    Check(!overlay.gameObject.activeSelf, "Death presentation is dismissed");
                    var saved = GameSave.Read<RunSave>("TowerTrials.Run.v1");
                    Check(StageRun.TryRestore(game.Run.Definition, saved, out var restored) && !restored.Failed && restored.Power == checkpointPower, "Revived checkpoint is saved immediately");
                    Swipe(FindRoom(false)); step++; break;
                case 11:
                    Check(!game.Run.Failed && game.Run.Power > checkpointPower, "Revived hero accepts a new legal pointer attack");
                    var profile = HeroProfile.Load(HeroCenterCatalog.Load());
                    Check(profile.gold == gold && profile.gems == gems, "Ad continue never duplicates currency or stage rewards");
                    Finish(null); return;
            }
            frame = Time.frameCount;
        }
        catch (Exception error) { Finish(error); }
    }

    private static void CaptureCheckpoint()
    { failedRun = game.Run; checkpointPower = game.Run.Power; checkpointMasks = game.Run.Save(0).clearedMasks; }
    private static void CheckUnchanged(string reason)
    { Check(game.Run.Failed && game.Run.Power == checkpointPower && game.Run.Save(0).clearedMasks.SequenceEqual(checkpointMasks), reason + " cannot revive or alter progress"); }
    private static UnityEngine.UI.Button Control(string name)
    {
        GameObject target = GameObject.Find(name); Check(target != null, "Control exists: " + name);
        return target.GetComponent<UnityEngine.UI.Button>();
    }
    private static void Click(string name)
    {
        Canvas.ForceUpdateCanvases(); var button = Control(name); var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "Button receives pointer: " + name);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private static int FindRoom(bool stronger)
    {
        long[] enemies = game.Run.Definition.towers[game.Run.TowerIndex].enemies;
        for (int i = 0; i < enemies.Length; i++)
            if (!game.Run.IsDefeated(game.Run.TowerIndex, i) && (stronger ? enemies[i] > game.Run.Power : enemies[i] < game.Run.Power)) return i;
        throw new Exception("Fixture needs a " + (stronger ? "stronger" : "weaker") + " remaining enemy");
    }
    private static void Swipe(int room)
    {
        var input = UnityEngine.Object.FindAnyObjectByType<SwipeAttackInput>(); Vector2 end = game.EnemyScreenPosition(room);
        var pointer = new PointerEventData(EventSystem.current) { position = end - Vector2.right * Screen.width * 0.4f, pointerId = -1 };
        ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        pointer.position = end; ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        Check(game.Busy, "Pointer starts attack"); attacks++;
    }
    private static void CheckUiBounds()
    {
        foreach (string name in new[] { "WatchAdContinue", "Continue", "ResultTitle", "ResultDetail", "ReviveBenefit", "ReviveStatus" })
        {
            var rect = (RectTransform)GameObject.Find(name).transform; var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Check(corners.All(point => point.x >= 0 && point.x <= Screen.width && point.y >= 0 && point.y <= Screen.height), "Death option stays inside viewport: " + name);
        }
    }
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Restore()
    {
        foreach (string key in Saves)
            if (SessionState.GetBool(Prefix + key + ".exists", false)) PlayerPrefs.SetString(key, SessionState.GetString(Prefix + key, "")); else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save(); SessionState.SetBool(Prefix + "Active", false);
        RewardedContinueAds.Register(null);
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Restore();
        string report = error == null ? "PASS: " + checks + " checks, " + attacks + " real pointer attacks. Death-screen ad slot, no-provider Retry, skipped/failed ads, cancelled/stale and duplicate rewards, worker-thread confirmation, retained power/cleared rooms, saved revival and post-revive combat. Editor-only provider; no real ads. Original saves restored.\n" : "FAIL: " + error;
        File.WriteAllText("Artifacts/RewardedContinuePlayValidation.txt", report);
        if (error != null) Debug.LogException(error); else Debug.Log(report);
        if (Application.isPlaying) SceneManager.LoadSceneAsync("HeroCenter");
    }
}
