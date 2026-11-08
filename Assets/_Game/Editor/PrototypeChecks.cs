using System;
using System.Collections.Generic;
using System.IO;
using TowerTrials;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class PrototypeChecks
{
    private static int assertions;

    [MenuItem("Tower Trials/Verify Game Rules")]
    public static void VerifyRulesMenu() => VerifyRules();

    public static string VerifyRules()
    {
        assertions = 0;
        var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfig>("Assets/_Game/Data/StarterCampaign.asset");
        if (campaign == null) throw new InvalidOperationException("The saved starter campaign is missing.");
        StageDefinition[] stages = campaign.stages;
        foreach (StageDefinition stage in stages)
        {
            stage.Validate();
            Explore(stage, new StageRun(stage));
        }
        // Fixed rule fixture stays independent of editable balancing numbers.
        var fixture = new StageDefinition("Combat fixture", 20,
            new TowerDefinition(4, 31, 15), new TowerDefinition(26, 83, 47), new TowerDefinition(62, 350, 105));
        var run = new StageRun(fixture);
        Check(run.Attack(0, 0) == AttackOutcome.Defeated && run.Power == 24, "20 + 4 must become 24");
        Check(run.Attack(0, 0) == AttackOutcome.Invalid && run.Power == 24, "Duplicate attack cannot award power twice");
        Check(run.Attack(1, 0) == AttackOutcome.Invalid, "Queued towers cannot be attacked");
        Check(run.Attack(0, 1) == AttackOutcome.Lost && run.Power == 24, "Stronger enemy defeats hero without granting power");
        Check(run.Attack(0, 2) == AttackOutcome.Invalid, "Cannot attack after losing");
        Check(StageRun.TryRestore(fixture, run.Save(0), out var lost) && lost.Failed && lost.Power == 24, "Failed run restores");
        Check(!StageRun.TryRestore(fixture, new RunSave { clearedMasks = new[] { 0, 1, 0 } }, out _), "Future tower progress is rejected");
        Check(!StageRun.TryRestore(fixture, new RunSave { clearedMasks = new[] { 256, 0, 0 } }, out _), "Invalid room bits are rejected");
        Check(!StageRun.TryRestore(fixture, new RunSave { version = 22, clearedMasks = new[] { 0, 0, 0 } }, out _), "Unknown save version is rejected");
        Check(!StageRun.TryRestore(fixture, new RunSave { clearedMasks = new[] { 2, 0, 0 } }, out _), "Impossible saved defeat is rejected");
        ExpectInvalid(new StageDefinition("Tie after a kill", 20, new TowerDefinition(4, 24)));
        ExpectInvalid(new StageDefinition("Blocked stage", 20, new TowerDefinition(21, 30)));
        ExpectInvalid(new StageDefinition("Repeated powers", 20, new TowerDefinition(4, 4)));
        ExpectInvalid(new StageDefinition("Negative power", 20, new TowerDefinition(-2)));
        bool overflow = false;
        try { new StageDefinition("Overflow", long.MaxValue - 1, new TowerDefinition(3)).Validate(); }
        catch (OverflowException) { overflow = true; }
        Check(overflow, "Power overflow must be rejected");
        string report = "PASS: " + assertions + " assertions; all reachable winning orders across 3 stages; save restore; ties; invalid stages; overflow.";
        Directory.CreateDirectory("Artifacts");
        File.WriteAllText("Artifacts/RulesVerification.txt", report);
        Debug.Log(report);
        return report;
    }

    private static void Explore(StageDefinition stage, StageRun run)
    {
        Check(StageRun.TryRestore(stage, run.Save(0), out var restored) && restored.Power == run.Power &&
            restored.TowerIndex == run.TowerIndex && restored.Complete == run.Complete, "Every reachable state must round-trip");
        // Exercise Unity's actual JSON serializer too, not just an in-memory snapshot.
        var snapshot = JsonUtility.FromJson<RunSave>(JsonUtility.ToJson(run.Save(0)));
        Check(StageRun.TryRestore(stage, snapshot, out _), "JSON save round-trip");
        if (run.Complete) return;
        bool hasWinningMove = false;
        for (int room = 0; room < stage.towers[run.TowerIndex].enemies.Length; room++)
        {
            if (run.IsDefeated(run.TowerIndex, room)) continue;
            long enemy = stage.towers[run.TowerIndex].enemies[room];
            Check(enemy != run.Power, "No reachable equal-power encounter");
            if (enemy > run.Power) continue;
            hasWinningMove = true;
            Check(StageRun.TryRestore(stage, snapshot, out var branch), "Restore branch");
            int previousTower = branch.TowerIndex;
            AttackOutcome result = branch.Attack(previousTower, room);
            Check(result != AttackOutcome.Invalid && result != AttackOutcome.Lost, "Weaker enemy must lose");
            Check(branch.Power == run.Power + enemy, "Power is awarded exactly once");
            if (branch.TowerIndex > previousTower)
                for (int r = 0; r < stage.towers[previousTower].enemies.Length; r++)
                    Check(branch.IsDefeated(previousTower, r), "Tower advances only after every room is cleared");
            Explore(stage, branch);
        }
        Check(hasWinningMove, "Every reachable live state has a winning continuation");
    }

    private static void ExpectInvalid(StageDefinition stage)
    {
        bool rejected = false;
        try { stage.Validate(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Invalid stage must be rejected: " + stage.title);
    }
    private static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new Exception("RULE CHECK FAILED: " + message); }

    // Runs through the real pointer handlers and animation frames in an already-started Play session.
    private static TowerGameController playGame;
    private static double playDeadline;
    private static int swipeCount;
    private static bool checkedFailure;
    private static bool checkedShortGesture;
    private static Transform attackedRoom;
    private static long expectedFinalPower;
    private static int failureRoom;

    public static string StartPlaytest()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        Directory.CreateDirectory("Artifacts");
        File.WriteAllText("Artifacts/Playtest.txt", "RUNNING: pointer, animation and combat feedback playtest.");
        playGame = UnityEngine.Object.FindAnyObjectByType<TowerGameController>();
        if (playGame == null || playGame.Run == null) throw new InvalidOperationException("Game has not started.");
        playGame.RestartStage();
        attackedRoom = null;
        expectedFinalPower = playGame.Run.Definition.startingPower;
        foreach (TowerDefinition tower in playGame.Run.Definition.towers)
            foreach (long power in tower.enemies) expectedFinalPower = checked(expectedFinalPower + power);
        failureRoom = -1;
        for (int r = 0; r < playGame.Run.Definition.towers[0].enemies.Length; r++)
            if (playGame.Run.Definition.towers[0].enemies[r] > playGame.Run.Power) { failureRoom = r; break; }
        if (failureRoom < 0) throw new InvalidOperationException("This playtest requires one stronger enemy in the first tower.");
        swipeCount = 0; checkedFailure = false; checkedShortGesture = false;
        playDeadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update -= TickPlaytest;
        EditorApplication.update += TickPlaytest;
        return "Running pointer/animation playtest; report will be saved in Artifacts/Playtest.txt";
    }

    private static void TickPlaytest()
    {
        try
        {
            if (playGame == null || !EditorApplication.isPlaying) throw new Exception("Play session ended early.");
            if (EditorApplication.timeSinceStartup > playDeadline) throw new Exception("Playtest exceeded 60 seconds.");
            if (playGame.Busy) return;
            if (attackedRoom != null && checkedFailure)
            {
                Check(!attackedRoom.gameObject.activeInHierarchy, "Defeated enemy room disappears");
                Transform knight = playGame.transform.Find("Presentation/Knight");
                Check(Vector3.Distance(knight.position, new Vector3(-2.55f, 0.82f, -0.12f)) < 0.01f, "Hero returns to his tower");
                attackedRoom = null;
            }
            if (!checkedShortGesture)
            {
                Vector2 position = playGame.EnemyScreenPosition(0);
                Check(!playGame.SubmitSwipe(position, position + Vector2.right * 2), "Short taps do not attack");
                checkedShortGesture = true;
            }
            if (!checkedFailure)
            {
                if (!playGame.Run.Failed) { SendPointerSwipe(failureRoom); return; }
                Check(playGame.Run.Power == playGame.Run.Definition.startingPower, "Failure grants no power");
                playGame.Continue();
                Check(!playGame.Run.Failed, "Retry returns to a playable state");
                checkedFailure = true;
            }
            if (playGame.Run.Complete)
            {
                Check(playGame.transform.Find("Presentation/EnemyTower_1") == null, "Cleared tower disappears");
                Check(playGame.Run.Power == expectedFinalPower, "Final power matches the edited campaign");
                Check(playGame.Feedback.ImpactCount == swipeCount, "Exactly one impact per successful attack");
                Check(playGame.Feedback.SlashCount == swipeCount, "Exactly one sword slash per successful attack");
                Check(playGame.Feedback.ActivePulseCount <= 24 && playGame.Feedback.ParticleCount <= 160, "Combat effects stay within their mobile budgets");
                var motion = playGame.transform.Find("Presentation/Knight").GetComponent<CharacterMotion>();
                Check(motion.HasWeaponRig && motion.SwingCount == swipeCount, "Sword arm swings once per attack");
                Check(playGame.GetComponentInChildren<GameBackdrop>() != null, "Painted background is present");
                Check(UnityEngine.Object.FindObjectsByType<EventSystem>().Length == 1, "One EventSystem");
                Directory.CreateDirectory("Artifacts");
                File.WriteAllText("Artifacts/Playtest.txt", "PASS: real pointer down/drag/up; short swipe ignored; stronger-enemy failure; retry; " +
                    swipeCount + " attacks; power reaches " + expectedFinalPower + "; real sword-arm swings and trails; bounded impact VFX; painted background; defeated rooms disappear; hero returns home; all towers collapse and advance; stage-clear overlay; local saves.");
                Debug.Log("POINTER_PLAYTEST_PASS");
                EditorApplication.update -= TickPlaytest;
                return;
            }
            int tower = playGame.Run.TowerIndex;
            long[] powers = playGame.Run.Definition.towers[tower].enemies;
            for (int room = 0; room < powers.Length; room++)
            {
                if (playGame.Run.IsDefeated(tower, room) || powers[room] >= playGame.Run.Power) continue;
                SendPointerSwipe(room);
                swipeCount++;
                return;
            }
            throw new Exception("No winning swipe available.");
        }
        catch (Exception exception)
        {
            EditorApplication.update -= TickPlaytest;
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText("Artifacts/Playtest.txt", "FAIL: " + exception);
            Debug.LogException(exception);
        }
    }

    private static void SendPointerSwipe(int room)
    {
        var input = UnityEngine.Object.FindAnyObjectByType<SwipeAttackInput>();
        attackedRoom = playGame.transform.Find("Presentation/EnemyTower_" + (playGame.Run.TowerIndex + 1) + "/Room_" + room);
        Vector2 end = playGame.EnemyScreenPosition(room);
        Vector2 start = end - Vector2.right * (Screen.width * 0.45f);
        var pointer = new PointerEventData(EventSystem.current) { pointerId = -1, position = start };
        ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        pointer.position = end;
        ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        Check(playGame.Busy, "Pointer gesture starts one attack");
        Check(!playGame.SubmitSwipe(start, end), "Extra swipes cannot interrupt an attack");
    }
}
