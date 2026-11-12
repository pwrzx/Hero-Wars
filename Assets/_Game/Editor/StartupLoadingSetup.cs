using System;
using System.Collections.Generic;
using System.IO;
using TowerTrials;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class StartupLoadingSetup
{
    public const string ScenePath = "Assets/_Game/Scenes/Startup.unity";

    [MenuItem("Hero Wars/Set up startup loading screen")]
    public static void SetUp()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before setting up startup.");
        if (!File.Exists(ScenePath))
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var camera = new GameObject("StartupCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(10, 28, 37, 255);
                camera.cullingMask = 0;
                var events = new GameObject("StartupEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                var loading = new GameObject("StartupLoadingScreen").AddComponent<StartupLoadingScreen>();
                HeroCenterCatalog catalog = HeroCenterCatalog.Load();
                loading.Configure(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Backgrounds/AncientChasm.png"), catalog.heroIcon);
                loading.BuildPreview();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(entry => entry.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Startup loading screen is first in the build, followed by Hero Center.");
    }
}
