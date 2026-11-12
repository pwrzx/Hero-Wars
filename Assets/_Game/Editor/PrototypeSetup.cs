using System;
using System.IO;
using TMPro;
using TowerTrials;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PrototypeSetup
{
    public const string ScenePath = "Assets/_Game/Scenes/TowerPrototype.unity";

    [MenuItem("Tower Trials/Create Starter Scene")]
    public static void CreateScene()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before creating a scene.");
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Starter scene already exists; open it instead of rebuilding.");
        Directory.CreateDirectory("Assets/_Game/Scenes");
        Directory.CreateDirectory("Assets/_Game/Resources/Materials");
        Directory.CreateDirectory("Assets/_Game/Data");
        AssetDatabase.Refresh();
        PreparePolishAssets();
        if (Resources.Load<TMP_Settings>("TMP Settings") == null || TMP_Settings.defaultFontAsset == null)
            throw new InvalidOperationException("Import TMP essentials with PrototypeSetup.ImportTextResources first.");
        MakeMaterial("Stone_DeepTeal", new Color(0.12f, 0.24f, 0.27f));
        MakeMaterial("Stone_Trim", new Color(0.28f, 0.43f, 0.44f));
        MakeMaterial("Gold", new Color(0.9f, 0.57f, 0.15f), 0.5f);
        MakeMaterial("Knight_Blue", new Color(0.08f, 0.27f, 0.64f), 0.3f);
        MakeMaterial("Steel", new Color(0.57f, 0.72f, 0.8f), 0.65f);
        MakeMaterial("Charcoal", new Color(0.025f, 0.05f, 0.07f));
        MakeMaterial("Skin", new Color(0.72f, 0.44f, 0.27f));
        MakeMaterial("Goblin_Jade", new Color(0.21f, 0.53f, 0.3f));
        MakeMaterial("Goblin_Plum", new Color(0.31f, 0.10f, 0.25f));
        MakeMaterial("Eye_Amber", new Color(1, 0.68f, 0.1f));
        MakeMaterial("Cape", new Color(0.08f, 0.13f, 0.3f));
        MakeMaterial("Backdrop", new Color(0.035f, 0.20f, 0.22f));
        var config = AssetDatabase.LoadAssetAtPath<CampaignConfig>("Assets/_Game/Data/StarterCampaign.asset");
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<CampaignConfig>();
            AssetDatabase.CreateAsset(config, "Assets/_Game/Data/StarterCampaign.asset");
        }
        foreach (StageDefinition stage in config.stages) stage.Validate();

        // Additive creation preserves the user's existing scene and any unsaved changes in it.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        Camera camera = cameraGo.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6.95f;
        camera.transform.position = new Vector3(0, 5.6f, -18);
        camera.transform.LookAt(new Vector3(0, 3.25f, 0));
        camera.backgroundColor = new Color(0.045f, 0.23f, 0.26f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.farClipPlane = 100;
        var lightGo = new GameObject("Sun", typeof(Light));
        Light light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.8f;
        light.color = new Color(1, 0.91f, 0.73f);
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(36, -24, 0);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.52f, 0.65f, 0.69f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = camera.backgroundColor;
        RenderSettings.fogStartDistance = 23;
        RenderSettings.fogEndDistance = 48;
        var game = new GameObject("TowerGame").AddComponent<TowerGameController>();
        game.Configure(config, camera);
        game.BuildPreview();
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.defaultScreenWidth = 540;
        PlayerSettings.defaultScreenHeight = 960;
        EditorSceneManager.SaveScene(scene, ScenePath);
        var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!buildScenes.Exists(entry => entry.path == ScenePath))
            buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = game.gameObject;
        Debug.Log("Tower Trials prototype created. Open TowerPrototype and press Play.");
    }

    public static void ImportTextResources()
    {
        if (Resources.Load<TMP_Settings>("TMP Settings") == null || TMP_Settings.defaultFontAsset == null)
            TMP_PackageResourceImporter.ImportResources(true, false, false);
    }

    [MenuItem("Tower Trials/Prepare Combat Effects")]
    public static void PreparePolishAssets()
    {
        const string folder = "Assets/_Game/Resources/Materials/";
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        Shader glow = Shader.Find("TowerTrials/SoftGlow");
        Shader painting = Shader.Find("TowerTrials/PaintedBackdrop");
        if (glow == null || painting == null) throw new InvalidOperationException("Import the Tower Trials effect shaders first.");
        MakeMaterial("Keep_Limestone", new Color(0.72f, 0.69f, 0.62f));
        MakeMaterial("Keep_Ashlar", new Color(0.32f, 0.40f, 0.46f));
        MakeMaterial("Keep_Vermilion", new Color(0.68f, 0.20f, 0.13f));
        string[] names = { "FX_Glow", "FX_Ring", "FX_Slash" };
        for (int i = 0; i < names.Length; i++)
        {
            string path = folder + names[i] + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(glow) { name = names[i] };
                material.SetColor("_Tint", Color.white);
                material.SetFloat("_Shape", i);
                AssetDatabase.CreateAsset(material, path);
            }
        }
        string backgroundPath = folder + "FX_Backdrop.mat";
        Texture2D valley = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Backgrounds/DawnValley.png");
        if (valley == null) throw new InvalidOperationException("The DawnValley background is missing.");
        Material background = AssetDatabase.LoadAssetAtPath<Material>(backgroundPath);
        if (background == null)
        {
            background = new Material(painting) { name = "FX_Backdrop" };
            AssetDatabase.CreateAsset(background, backgroundPath);
        }
        if (background.mainTexture != valley)
        {
            background.mainTexture = valley;
            EditorUtility.SetDirty(background);
        }
        AssetDatabase.SaveAssets();
    }

    private static void MakeMaterial(string name, Color color, float metallic = 0)
    {
        string path = "Assets/_Game/Resources/Materials/" + name + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", 0.28f);
        if (name == "Eye_Amber")
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.4f);
        }
        AssetDatabase.CreateAsset(material, path);
    }
}
