using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TowerTrials;
using UnityEditor;
using UnityEngine;

// Exercises real physics steps and real attacks, including every order in a three-room
// tower. Fixtures are runtime-only, and the player's campaign and save are restored.
public static class TowerPhysicsChecks
{
    private const string SaveKey = "TowerTrials.Run.v1";
    private const string Report = "Artifacts/TowerPhysicsValidation.txt";
    private static readonly int[][] Orders =
    {
        new[] { 2, 0, 1 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
        new[] { 2, 1, 0 }, new[] { 0, 1, 2 }, new[] { 1, 2, 0 },
        new[] { 0, 1, 2, 3, 4, 5, 6, 7 }
    };
    private static readonly long[] Powers = { 7, 13, 29, 53, 97, 191, 383, 769 };
    private static readonly MethodInfo Reload = typeof(TowerGameController).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
    private static TowerGameController game;
    private static CampaignConfig originalCampaign, fixture;
    private static string originalSave;
    private static string originalBackup;
    private static bool hadBackup;
    private static bool hadSave, waiting, sawFall, sawLanding;
    private static int scenario, move, assertions, attacks;
    private static int observedSlashes, observedImpacts;
    private static Transform attackingHero;
    private static Animator attackAnimator;
    private static double deadline;
    private static Exception runtimeFailure;
    private static readonly Dictionary<TowerSegmentPhysics, int> landings = new Dictionary<TowerSegmentPhysics, int>();

    [MenuItem("Tower Trials/Verify Tower Physics (Play Mode)")]
    public static string StartChecks()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        game = UnityEngine.Object.FindAnyObjectByType<TowerGameController>();
        if (game == null || game.Run == null || game.Busy) throw new InvalidOperationException("Wait for the game to be ready.");
        originalCampaign = (CampaignConfig)new SerializedObject(game).FindProperty("campaign").objectReferenceValue;
        hadSave = PlayerPrefs.HasKey(SaveKey);
        originalSave = PlayerPrefs.GetString(SaveKey, "");
        hadBackup = PlayerPrefs.HasKey(SaveKey + ".backup");
        originalBackup = PlayerPrefs.GetString(SaveKey + ".backup", "");
        fixture = ScriptableObject.CreateInstance<CampaignConfig>();
        scenario = move = assertions = attacks = 0;
        waiting = false;
        runtimeFailure = null;
        deadline = EditorApplication.timeSinceStartup + 180;
        Directory.CreateDirectory("Artifacts");
        File.WriteAllText(Report, "RUNNING: gravity, contacts, compaction, facing, targeting and save restoration.");
        BeginScenario();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived += CaptureRuntimeError;
        return "Running tower physics regression checks (26 attacks across seven orders).";
    }

    private static void BeginScenario()
    {
        long[] powers = new long[Orders[scenario].Length];
        Array.Copy(Powers, powers, powers.Length);
        var stage = new StageDefinition("PHYSICS CHECK", 1000, new TowerDefinition(powers), new TowerDefinition(199));
        fixture.stages = new StageDefinition[originalCampaign.stages.Length];
        for (int i = 0; i < fixture.stages.Length; i++) fixture.stages[i] = stage;
        game.Configure(fixture, game.GameCamera);
        game.RestartStage();
        move = 0;
        VerifyStack();
    }

    private static Transform ActiveTower() => game.transform.Find("Presentation/EnemyTower_" + (game.Run.TowerIndex + 1));

    private static void Tick()
    {
        try
        {
            if (runtimeFailure != null) throw runtimeFailure;
            Check(EditorApplication.isPlaying && game != null, "Play session remains active");
            Check(EditorApplication.timeSinceStartup < deadline, "Physics run finishes within 180 seconds");
            foreach (var entry in landings)
            {
                if (entry.Key == null) continue;
                Check(entry.Key.RecoveryCount == 0, "Every segment lands on a real collider without recovery");
                sawFall |= entry.Key.IsSettling && entry.Key.GetComponent<Rigidbody>().linearVelocity.y < -0.1f;
                sawLanding |= entry.Key.LandingCount > entry.Value;
                if (entry.Key.IsSettling)
                    Check(!game.SubmitSwipe(Vector2.zero, Vector2.right * Screen.width), "Input stays locked during the fall");
            }
            if (waiting) VerifyCombatTiming();
            if (game.Busy) return;
            if (waiting)
            {
                Check(sawFall, "Removing a room starts a gravity-driven fall");
                Check(sawLanding, "A supporting collision produces a landing");
                VerifyStack();
                move++;
                waiting = false;
                landings.Clear();
                if (move < Orders[scenario].Length)
                {
                    Vector3 crownPosition = ActiveTower().Find("TowerCrown").position;
                    long power = game.Run.Power;
                    ReloadSavedRun();
                    Check(game.Run.Power == power, "Saved power survives a complete presentation reload");
                    VerifyStack();
                    Check(Vector3.Distance(ActiveTower().Find("TowerCrown").position, crownPosition) < 0.001f,
                        "Restored saves preserve the compacted crown height");
                }
            }
            if (move == Orders[scenario].Length)
            {
                Check(game.Run.TowerIndex == 1, "A completely cleared tower advances exactly once");
                Check(Mathf.Abs(ActiveTower().position.x - 1.05f) < 0.001f && ActiveTower().localScale == Vector3.one,
                    "Queued tower advances into the active position and scale");
                scenario++;
                if (scenario == Orders.Length)
                {
                    Finish(null);
                    return;
                }
                BeginScenario();
            }
            Transform tower = ActiveTower();
            foreach (TowerSegmentPhysics segment in tower.GetComponentsInChildren<TowerSegmentPhysics>())
                landings[segment] = segment.LandingCount;
            sawFall = sawLanding = false;
            int room = Orders[scenario][move];
            Vector2 end = game.EnemyScreenPosition(room);
            Vector2 start = end - Vector2.right * Screen.width * 0.4f;
            attackingHero = game.transform.Find("Presentation/Knight");
            attackAnimator = attackingHero.GetComponentInChildren<Animator>();
            observedSlashes = game.Feedback.SlashCount;
            observedImpacts = game.Feedback.ImpactCount;
            Check(game.SubmitSwipe(start, end), "A swipe targets the original room ID at its current physical height");
            waiting = true;
            attacks++;
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void VerifyCombatTiming()
    {
        Check(attackAnimator != null, "Combat uses the equipped animated hero");
        AnimatorStateInfo state = attackAnimator.GetCurrentAnimatorStateInfo(0);
        if (game.Feedback.SlashCount > observedSlashes)
        {
            Check(state.IsName("Base Layer.Attack"), "The imported hero swings when the slash appears");
            Check(attackingHero.position.x > 0, "The swing starts at the enemy tower");
            observedSlashes = game.Feedback.SlashCount;
        }
        if (game.Feedback.ImpactCount > observedImpacts)
        {
            Check(state.IsName("Base Layer.Attack") && state.normalizedTime >= 0.35f && state.normalizedTime < 0.5f,
                "Impact feedback coincides with the weapon's contact pose");
            Check(attackingHero.position.x > 0, "The hero hits before returning home");
            observedImpacts = game.Feedback.ImpactCount;
        }
        if (Mathf.Abs(attackingHero.position.x + 2.55f) < 0.01f && game.Feedback.ImpactCount > 0)
            Check(!state.IsName("Base Layer.Attack"), "No attack animation replays at the home tower");
    }

    private static void VerifyStack()
    {
        Transform tower = ActiveTower();
        Transform hero = game.transform.Find("Presentation/Knight");
        Check(Vector3.Dot(hero.forward, Vector3.right) > 0.4f, "Hero turns toward the enemies");
        Check(Vector3.Distance(hero.position, new Vector3(-2.55f, 0.82f, -0.12f)) < 0.001f, "Hero rests on his original platform");
        int floor = 0, index = game.Run.TowerIndex;
        for (int room = 0; room < game.Run.Definition.towers[index].enemies.Length; room++)
        {
            Transform roomView = tower.Find("Room_" + room);
            if (game.Run.IsDefeated(index, room))
            {
                Check(!roomView.gameObject.activeSelf, "The defeated room and its colliders disappear");
                continue;
            }
            Check(Mathf.Abs(roomView.localPosition.y - floor * 1.65f) < 0.001f, "Occupied floors remain contiguous");
            Check(roomView.localScale == Vector3.one, "Surviving rooms keep their structural scale");
            var segment = roomView.GetComponent<TowerSegmentPhysics>();
            Check(!segment.IsSettling && segment.GetComponent<Rigidbody>().isKinematic, "Settled rooms are stable supports");
            Check(segment.RecoveryCount == 0, "Settlement uses collision, without a timeout snap");
            Transform enemy = roomView.Find("Goblin");
            Check(enemy != null && enemy.gameObject.activeInHierarchy, "The surviving enemy rides its room");
            Check(Vector3.Dot(enemy.forward, Vector3.left) > 0.4f, "Enemy turns toward the hero");
            Check(Mathf.Abs(enemy.position.y - (0.82f + floor * 1.65f)) < 0.001f, "Enemy feet follow the supporting room");
            if (floor == 0) Check(Mathf.Abs(enemy.position.y - hero.position.y) < 0.001f, "The lowest survivor shares the hero's level");
            Check(roomView.Find("Target rim") != null, "Target indicator rides the same room");
            Vector2 expected = game.GameCamera.WorldToScreenPoint(enemy.position + Vector3.up * 0.65f);
            Check(Vector2.Distance(expected, game.EnemyScreenPosition(room)) < 0.01f, "Swipe targeting follows the moved enemy");
            floor++;
        }
        Transform crown = tower.Find("TowerCrown");
        Check(Mathf.Abs(crown.localPosition.y - floor * 1.65f) < 0.001f, "Crown rests directly above the occupied stack");
        Check(!crown.GetComponent<TowerSegmentPhysics>().IsSettling, "Crown finishes falling before input unlocks");
        Check(tower.GetComponent<BoxCollider>() != null, "Tower foundation remains a permanent landing support");
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception("TOWER PHYSICS CHECK FAILED: " + message);
    }

    private static void CaptureRuntimeError(string message, string stack, LogType type)
    {
        if (runtimeFailure == null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            runtimeFailure = new Exception("Runtime error during physics validation: " + message + "\n" + stack);
    }

    private static void ReloadSavedRun()
    {
        // A fresh session has no old cosmetic coroutines targeting the retired HUD.
        game.StopAllCoroutines();
        Reload.Invoke(game, null);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= CaptureRuntimeError;
        landings.Clear();
        try
        {
            if (game != null && EditorApplication.isPlaying)
            {
                game.Configure(originalCampaign, game.GameCamera);
                if (hadSave) PlayerPrefs.SetString(SaveKey, originalSave);
                else PlayerPrefs.DeleteKey(SaveKey);
                if (hadBackup) PlayerPrefs.SetString(SaveKey + ".backup", originalBackup);
                else PlayerPrefs.DeleteKey(SaveKey + ".backup");
                PlayerPrefs.Save();
                ReloadSavedRun();
            }
        }
        catch (Exception restorationError) { error = error ?? restorationError; }
        finally
        {
            if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
            string report = error == null
                ? "PASS: " + assertions + " assertions; " + attacks + " real attacks; all six three-room kill orders; eight-floor bottom-up collapse; real gravity and supporting collisions; no recovery snaps; crown/room compaction; surviving enemies at hero height; facing; input lock; target rims and swipe positions; partial-save reloads; next-tower advance. Original campaign and player save restored."
                : "FAIL: " + error;
            File.WriteAllText(Report, report);
            if (error == null) Debug.Log(report); else Debug.LogException(error);
        }
    }
}
