using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TowerTrials;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class MobileReleaseBuild
{
    public const string ApplicationId = "com.herowars.towertrials";
    private static readonly string[] Scenes = { StartupLoadingSetup.ScenePath, HeroCenterSetup.ScenePath, PrototypeSetup.ScenePath };

    [MenuItem("Hero Wars/Release/Prepare mobile settings")]
    public static void Prepare()
    {
        PlayerSettings.productName = "Hero Wars";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ApplicationId);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
        foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbols(target);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols.Split(';').Where(symbol => symbol != "ENABLE_RUNTIME_PIPELINE")));
        }
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.bundleVersionCode = Math.Max(1, PlayerSettings.Android.bundleVersionCode);
        PlayerSettings.Android.forceInternetPermission = false;
        PlayerSettings.Android.forceSDCardPermission = false;
        PlayerSettings.Android.androidIsGame = true;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.buildNumber = "1";
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.iOS.requiresPersistentWiFi = false;
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.runInBackground = false;
        EditorBuildSettings.scenes = Scenes.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        CampaignConfig campaign = AssetDatabase.LoadAssetAtPath<CampaignConfig>("Assets/_Game/Data/StarterCampaign.asset");
        if (campaign.stages.Length < CampaignConfig.StageCount)
        {
            Undo.RecordObject(campaign, "Expand release campaign");
            campaign.stages = CampaignConfig.Expand(campaign.stages);
            EditorUtility.SetDirty(campaign);
        }
        foreach (StageDefinition stage in campaign.stages) stage.Validate();
        CompressMobileTextures();
        PrepareIcon();
        AssetDatabase.SaveAssets();
        Debug.Log("Prepared Android ARM64/API 36 and iOS 15+/Metal release settings; 24 validated keeps.");
    }

    private static void CompressMobileTextures()
    {
        string[] folders = { "Assets/_Game/HeroCenter/Icons", "Assets/_Game/Art/Backgrounds", "Assets/300Mind/2D Game UI Kit/Sprites" };
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform; settings.overridden = true;
                settings.maxTextureSize = Math.Min(4096, importer.maxTextureSize);
                settings.format = TextureImporterFormat.ASTC_4x4;
                settings.compressionQuality = 70;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
        }
    }

    [MenuItem("Hero Wars/Release/Refresh app icons")]
    public static void PrepareIcon()
    {
        const string path = "Assets/_Game/Art/HeroWarsIcon.png";
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (icon == null)
        {
            var stage = new GameObject("ReleaseIconRig") { hideFlags = HideFlags.HideAndDontSave };
            stage.transform.position = new Vector3(3000, 0, 0);
            var camera = new GameObject("IconCamera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(stage.transform, false);
            camera.transform.localPosition = new Vector3(0, 1.9f, -5);
            camera.transform.LookAt(stage.transform.position + Vector3.up * 1.9f);
            camera.orthographic = true; camera.orthographicSize = 1.45f;
            camera.backgroundColor = new Color32(17, 49, 55, 255);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.cullingMask = 1 << 30; camera.allowHDR = false;
            GameObject hero = HeroAppearance.Create(HeroCenterCatalog.Load(), new HeroProfile(), stage.transform, 3.15f);
            hero.transform.localRotation = Quaternion.Euler(0, 168, 0);
            foreach (Transform part in hero.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 30;
            var light = new GameObject("IconLight", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(stage.transform, false);
            light.type = LightType.Directional; light.color = new Color(1, 0.89f, 0.73f);
            light.intensity = 2.5f; light.cullingMask = 1 << 30;
            light.transform.localRotation = Quaternion.Euler(25, -25, 0);
            var target = new RenderTexture(1024, 1024, 24) { antiAliasing = 4 };
            var pixels = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create(); camera.targetTexture = target; camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(stage);
            }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false; importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        var iconImporter = (TextureImporter)AssetImporter.GetAtPath(path);
        iconImporter.textureCompression = TextureImporterCompression.Uncompressed;
        foreach (string platform in new[] { "Android", "iPhone" })
        {
            TextureImporterPlatformSettings settings = iconImporter.GetPlatformTextureSettings(platform);
            settings.name = platform; settings.overridden = true;
            settings.maxTextureSize = 1024; settings.format = TextureImporterFormat.RGB24;
            iconImporter.SetPlatformTextureSettings(settings);
        }
        iconImporter.SaveAndReimport();
        icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
#pragma warning disable CS0618
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.iOS, Enumerable.Repeat(icon, PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.iOS).Length).ToArray());
#pragma warning restore CS0618
        // Platform overrides take precedence over the default icon, including Unity's
        // starter adaptive icons. Populate every size and required texture layer.
        foreach (NamedBuildTarget platform in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(platform))
            {
                PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(platform, kind);
                foreach (PlatformIcon slot in slots)
                    slot.SetTextures(Enumerable.Repeat(icon, Math.Max(1, slot.minLayerCount)).ToArray());
                PlayerSettings.SetPlatformIcons(platform, kind, slots);
            }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Hero Wars/Release/Build Android test APK")]
    public static void AndroidApk() => BuildAndroid(false);
    [MenuItem("Hero Wars/Release/Build Android store bundle")]
    public static void AndroidBundle() => BuildAndroid(true);
    private static void BuildAndroid(bool bundle)
    {
        RequireStopped(BuildTarget.Android);
        bool priorBundle = EditorUserBuildSettings.buildAppBundle;
        bool priorSigning = PlayerSettings.Android.useCustomKeystore;
        string priorKey = PlayerSettings.Android.keystoreName, priorAlias = PlayerSettings.Android.keyaliasName;
        string priorKeyPassword = PlayerSettings.Android.keystorePass, priorAliasPassword = PlayerSettings.Android.keyaliasPass;
        string key = Environment.GetEnvironmentVariable("HERO_WARS_KEYSTORE");
        bool signed = bundle && !string.IsNullOrEmpty(key);
        string path = bundle ? "Builds/Android/HeroWars" + (signed ? "-release.aab" : "-unsigned.aab") : "Builds/Android/HeroWars-QA.apk";
        Directory.CreateDirectory("Builds/Android");
        try
        {
            EditorUserBuildSettings.buildAppBundle = bundle;
            PlayerSettings.Android.useCustomKeystore = signed;
            if (signed)
            {
                PlayerSettings.Android.keystoreName = key;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("HERO_WARS_KEYSTORE_PASSWORD");
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("HERO_WARS_KEY_ALIAS");
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("HERO_WARS_KEY_PASSWORD");
            }
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            { scenes = Scenes, target = BuildTarget.Android, locationPathName = path, options = BuildOptions.CompressWithLz4HC });
            Check(report, path);
            if (bundle && !signed)
            {
                using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
                    foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase) &&
                        (entry.Name.EndsWith(".RSA", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".DSA", StringComparison.OrdinalIgnoreCase) ||
                         entry.Name.EndsWith(".SF", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".MF", StringComparison.OrdinalIgnoreCase))).ToArray()) entry.Delete();
            }
        }
        finally
        {
            EditorUserBuildSettings.buildAppBundle = priorBundle;
            PlayerSettings.Android.useCustomKeystore = priorSigning;
            PlayerSettings.Android.keystoreName = priorKey; PlayerSettings.Android.keyaliasName = priorAlias;
            PlayerSettings.Android.keystorePass = priorKeyPassword; PlayerSettings.Android.keyaliasPass = priorAliasPassword;
        }
    }

    [MenuItem("Hero Wars/Release/Export iOS Xcode project")]
    public static void Ios()
    {
        RequireStopped(BuildTarget.iOS);
        Directory.CreateDirectory("Builds/iOS");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        { scenes = Scenes, target = BuildTarget.iOS, locationPathName = "Builds/iOS", options = BuildOptions.None });
        Check(report, "Builds/iOS");
    }
    private static void RequireStopped(BuildTarget expected)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building.");
        if (EditorUserBuildSettings.activeBuildTarget != expected)
            throw new InvalidOperationException("Switch the active build platform to " + expected + " first.");
    }
    private static void Check(BuildReport report, string path)
    {
        Directory.CreateDirectory("Artifacts");
        string summary = report.summary.result + ": " + path + "\n" + report.summary.totalSize + " bytes; " + report.summary.totalTime +
            "; errors=" + report.summary.totalErrors + "; warnings=" + report.summary.totalWarnings;
        File.WriteAllText("Artifacts/Build-" + report.summary.platform + ".txt", summary);
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(summary);
        Debug.Log(summary);
    }
}
