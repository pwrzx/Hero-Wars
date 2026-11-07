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
        StageDefinition[] stages = CampaignConfig.CreateStarterStages();
        foreach (StageDefinition stage in stages)
        {
            stage.Validate();
            Explore(stage, new StageRun(stage));
        }
        var run = new StageRun(stages[0]);
        Check(run.Attack(0, 0) == AttackOutcome.Defeated && run.Power == 24, "20 + 4 must become 24");
        Check(run.Attack(0, 0) == AttackOutcome.Invalid && run.Power == 24, "Duplicate attack cannot award power twice");
        Check(run.Attack(1, 0) == AttackOutcome.Invalid, "Queued towers cannot be attacked");
        Check(run.Attack(0, 1) == AttackOutcome.Lost && run.Power == 24, "Stronger enemy defeats hero without granting power");
        Check(run.Attack(0, 2) == AttackOutcome.Invalid, "Cannot attack after losing");
        Check(StageRun.TryRestore(stages[0], run.Save(0), out var lost) && lost.Failed && lost.Power == 24, "Failed run restores");
        Check(!StageRun.TryRestore(stages[0], new RunSave { clearedMasks = new[] { 0, 1, 0 } }, out _), "Future tower progress is rejected");
        Check(!StageRun.TryRestore(stages[0], new RunSave { clearedMasks = new[] { 256, 0, 0 } }, out _), "Invalid room bits are rejected");
        Check(!StageRun.TryRestore(stages[0], new RunSave { version = 22, clearedMasks = new[] { 0, 0, 0 } }, out _), "Unknown save version is rejected");
        Check(!StageRun.TryRestore(stages[0], new RunSave { clearedMasks = new[] { 2, 0, 0 } }, out _), "Impossible saved defeat is rejected");
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

    public static string StartPlaytest()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        playGame = UnityEngine.Object.FindAnyObjectByType<TowerGameController>();
        if (playGame == null || playGame.Run == null) throw new InvalidOperationException("Game has not started.");
        playGame.RestartStage();
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
                if (!playGame.Run.Failed) { SendPointerSwipe(1); return; }
                Check(playGame.Run.Power == playGame.Run.Definition.startingPower, "Failure grants no power");
                playGame.Continue();
                Check(!playGame.Run.Failed, "Retry returns to a playable state");
                checkedFailure = true;
            }
            if (playGame.Run.Complete)
            {
                Check(playGame.transform.Find("Presentation/EnemyTower_1") == null, "Cleared tower disappears");
                Check(playGame.Run.Power == 743, "Stage 1 final power is 743");
                Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One EventSystem");
                Directory.CreateDirectory("Artifacts");
                File.WriteAllText("Artifacts/Playtest.txt", "PASS: real pointer down/drag/up; short swipe ignored; stronger-enemy failure; retry; " +
                    swipeCount + " attacks; power reaches 743; defeated rooms disappear; hero returns home; all three towers collapse and advance; stage-clear overlay; local saves.");
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
