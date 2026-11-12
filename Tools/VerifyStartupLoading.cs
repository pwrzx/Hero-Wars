// Run in Play mode through: unity command script run -- --file Tools/VerifyStartupLoading.cs --entry StartupLoadingVerification.Run
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using TowerTrials;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class StartupLoadingVerification
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> Results = new List<string>();
    private static float deadline;

    public static string SetResolution(int width, int height)
    {
        Assembly editor = typeof(Editor).Assembly;
        Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
        object sizes = sizesType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy).GetValue(null);
        object group = sizesType.GetProperty("currentGroup").GetValue(sizes);
        Type groupType = group.GetType();
        Type sizeType = editor.GetType("UnityEditor.GameViewSize");
        string label = "Startup QA " + width + " x " + height;
        int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
        int index = -1;
        for (int i = 0; i < count; i++)
        {
            object existing = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            if ((int)sizeType.GetProperty("width").GetValue(existing) == width && (int)sizeType.GetProperty("height").GetValue(existing) == height)
            { index = i; break; }
        }
        if (index < 0)
        {
            object size = Activator.CreateInstance(sizeType, new object[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"), 1), width, height, label });
            groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
            index = count;
        }
        var view = EditorWindow.GetWindow(editor.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, index);
        view.Repaint();
        return label;
    }

    public static async Task<string> Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode before running startup verification.");
        Results.Clear();
        deadline = Time.realtimeSinceStartup + 50;
        float previousScale = Time.timeScale;
        bool hadMotion = PlayerPrefs.HasKey("HeroWars.ReducedMotion");
        int previousMotion = PlayerPrefs.GetInt("HeroWars.ReducedMotion");
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> injectFailure = null;
        try
        {
            PlayerPrefs.SetInt("HeroWars.ReducedMotion", 0);
            await CheckLaunch(428, 926, "Phone", false);
            PlayerPrefs.SetInt("HeroWars.ReducedMotion", 1);
            await CheckLaunch(768, 1024, "Tablet", true);
            Check(Time.timeScale == 0, "Startup does not alter the caller's time scale");
            Time.timeScale = previousScale;

            // A direct return home must not introduce another startup overlay.
            HeroCenterController.ReturnToHeroCenter();
            await Until(() => UnityEngine.Object.FindAnyObjectByType<HeroCenterController>()?.Profile != null);
            await Frames(3);
            Check(UnityEngine.Object.FindAnyObjectByType<StartupLoadingScreen>() == null, "Returning home skips startup");

            // Inject a missing destination before Start, then exercise the real serialized retry button.
            injectFailure = (scene, mode) =>
            {
                if (scene.name != StartupLoadingScreen.SceneName) return;
                StartupLoadingScreen loading = UnityEngine.Object.FindAnyObjectByType<StartupLoadingScreen>();
                typeof(StartupLoadingScreen).GetField("destinationScene", Hidden).SetValue(loading, "MissingStartupTestScene");
            };
            SceneManager.sceneLoaded += injectFailure;
            await OpenStartup();
            await Until(() => GameObject.Find("RetryLoading") != null);
            SceneManager.sceneLoaded -= injectFailure; injectFailure = null;
            var failed = UnityEngine.Object.FindAnyObjectByType<StartupLoadingScreen>();
            Check(failed != null && GameObject.Find("LoadingStatus").GetComponent<TMP_Text>().text.Contains("couldn't"), "Missing scene exposes a readable error and retry");
            typeof(StartupLoadingScreen).GetField("destinationScene", Hidden).SetValue(failed, "HeroCenter");
            GameObject.Find("RetryLoading").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            await Until(() => UnityEngine.Object.FindAnyObjectByType<StartupLoadingScreen>() == null);
            Check(UnityEngine.Object.FindAnyObjectByType<HeroCenterController>()?.Profile != null, "Retry recovers into a ready menu");
            string report = "PASS: " + Results.Count + " startup assertions.\n" + string.Join("\n", Results);
            File.WriteAllText("Artifacts/StartupLoadingValidation.txt", report);
            return report;
        }
        catch (Exception error)
        {
            File.WriteAllText("Artifacts/StartupLoadingValidation.txt", "FAIL: " + error + "\n" + string.Join("\n", Results));
            throw;
        }
        finally
        {
            if (injectFailure != null) SceneManager.sceneLoaded -= injectFailure;
            Time.timeScale = previousScale;
            if (hadMotion) PlayerPrefs.SetInt("HeroWars.ReducedMotion", previousMotion);
            else PlayerPrefs.DeleteKey("HeroWars.ReducedMotion");
            PlayerPrefs.Save();
        }
    }

    private static async Task CheckLaunch(int width, int height, string name, bool paused)
    {
        SetResolution(width, height);
        await Frames(3);
        Time.timeScale = paused ? 0 : 1;
        await OpenStartup();
        StartupLoadingScreen loading = UnityEngine.Object.FindAnyObjectByType<StartupLoadingScreen>();
        Check(loading != null, name + ": startup exists before destination activation");
        float previous = 0, started = Time.realtimeSinceStartup;
        bool captured = false, sawComplete = false;
        int firstFrame = Time.frameCount;
        while (loading != null)
        {
            CheckDeadline();
            if (loading.Progress < previous) throw new Exception("Loading progress moved backwards");
            previous = loading.Progress;
            if (loading.Progress >= 1) sawComplete = true;
            if (!captured && loading.Progress > 0.15f)
            {
                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text text in loading.GetComponentsInChildren<TMP_Text>())
                { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, name + ": " + text.name + " fits"); }
                Check(Screen.width == width && Screen.height == height, name + ": native screen dimensions match capture");
                var block = GameObject.Find("InputBlocker").GetComponent<UnityEngine.UI.Image>();
                Check(block.raycastTarget && block.GetComponentInParent<CanvasGroup>().blocksRaycasts, name + ": loading blocks touch input");
                ScreenCapture.CaptureScreenshot("Artifacts/LoadingScreen-" + name + ".png");
                captured = true;
            }
            await Frames(1);
        }
        Check(captured && sawComplete && Time.frameCount > firstFrame + 3, name + ": frames advance and progress reaches 100%");
        Check(Time.realtimeSinceStartup - started >= 1.5f, name + ": startup remains visible for the configured minimum");
        Check(SceneManager.GetActiveScene().name == "HeroCenter", name + ": Hero Center activates");
        var menu = UnityEngine.Object.FindAnyObjectByType<HeroCenterController>();
        Check(menu != null && menu.Profile != null && GameObject.Find("Adventure") != null, name + ": menu is ready when overlay disappears");
        Check(UnityEngine.Object.FindObjectsByType<EventSystem>().Length == 1, name + ": exactly one surviving EventSystem");
    }

    private static async Task OpenStartup()
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(StartupLoadingScreen.SceneName);
        while (!operation.isDone) await Frames(1);
    }

    private static async Task Until(Func<bool> condition)
    { while (!condition()) await Frames(1); }

    private static async Task Frames(int count)
    { for (int i = 0; i < count; i++) { CheckDeadline(); await Awaitable.NextFrameAsync(); } }

    private static void CheckDeadline()
    { if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Startup verification exceeded 50 seconds"); }

    private static void Check(bool passed, string message)
    { if (!passed) throw new Exception(message); Results.Add(message); }
}
