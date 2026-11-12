using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TowerTrials;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class HeroCenterSetup
{
    public const string ScenePath = "Assets/_Game/Scenes/HeroCenter.unity";
    private const string Root = "Assets/_Game/HeroCenter";
    private const string WeaponRoot = "Assets/GameLair/MedievalFantasyWeaponPack/Prefabs/";
    private const string HeroRoot = "Assets/Pack_Heros/Prefabs/";
    private static readonly List<Material> temporaryMaterials = new List<Material>();

    [MenuItem("Tower Trials/Set Up Hero Center")]
    public static void SetUp()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before setting up Hero Center.");
        Directory.CreateDirectory(Root + "/Icons"); Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory("Assets/_Game/Resources/HeroCenter"); AssetDatabase.Refresh();
        GameUiAssets.PrepareFonts();
        PrepareAtlas("Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_1.png");
        PrepareAtlas("Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_2.png");
        string path = "Assets/_Game/Resources/HeroCenter/Catalog.asset";
        HeroCenterCatalog catalog = AssetDatabase.LoadAssetAtPath<HeroCenterCatalog>(path);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<HeroCenterCatalog>(); AssetDatabase.CreateAsset(catalog, path); }
        catalog.goldIcon = SpriteFromPack(1, 16); catalog.gemIcon = SpriteFromPack(1, 22);
        catalog.heroIcon = SpriteFromPack(2, 13); catalog.armorIcon = SpriteFromPack(1, 1);
        catalog.shopIcon = SpriteFromPack(1, 0); catalog.settingsIcon = SpriteFromPack(1, 4);
        catalog.dailyIcon = SpriteFromPack(1, 3); catalog.slotFrame = SpriteFromPack(1, 25);
        PrepareButtonArt(catalog);
        catalog.background = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Backgrounds/AncientChasm.png");
        catalog.weaponMaterial = PrepareWeaponMaterial();
        catalog.cloakMaterial = PrepareCloakMaterial();
        catalog.armedAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Pack_Heros/Animations/Fire/Fire_AC.controller");
        catalog.equipment = new[]
        {
            Item("skin.nature", "Grove Warden", "The first chapter of your legend.", EquipmentSlot.Skin, ItemRarity.Common, HeroRoot + "Hero_Nature.prefab", 10, true),
            Item("skin.ice", "Frost Sentinel", "An icebound hero. Unlock in chests.", EquipmentSlot.Skin, ItemRarity.Rare, HeroRoot + "Hero_Ice.prefab", 35),
            Item("skin.rock", "Stone Guardian", "A mountain's strength. Unlock in chests.", EquipmentSlot.Skin, ItemRarity.Rare, HeroRoot + "Hero_Rock.prefab", 35),
            Item("skin.fire", "Ember Sovereign", "A legend forged in fire.", EquipmentSlot.Skin, ItemRarity.Legendary, HeroRoot + "Hero_Fire.prefab", 100),
            Item("armor.oak", "Oakguard Shield", "A trusted shield for a new hero.", EquipmentSlot.Armor, ItemRarity.Common, WeaponRoot + "ShieldOne.prefab", 8, true),
            Item("armor.sentinel", "Sentinel Shield", "Steel protection for the journey.", EquipmentSlot.Armor, ItemRarity.Rare, WeaponRoot + "ShieldTwo.prefab", 25),
            Item("armor.kingsguard", "Kingsguard Shield", "Protection worthy of a sovereign.", EquipmentSlot.Armor, ItemRarity.Legendary, WeaponRoot + "ShieldThree.prefab", 80),
            Item("weapon.sword1", "Wayfarer Sword", "A reliable blade. Ready for adventure.", EquipmentSlot.Weapon, ItemRarity.Common, WeaponRoot + "SwordOne.prefab", 12, true),
            Item("weapon.axe1", "Ironwood Axe", "A sturdy, honest edge.", EquipmentSlot.Weapon, ItemRarity.Common, WeaponRoot + "AxeOne.prefab", 14),
            Item("weapon.mace1", "Traveler Mace", "A simple answer to a tough enemy.", EquipmentSlot.Weapon, ItemRarity.Common, WeaponRoot + "BluntOne.prefab", 14),
            Item("weapon.sword2", "Azure Edge", "A blade carried by seasoned heroes.", EquipmentSlot.Weapon, ItemRarity.Rare, WeaponRoot + "SwordTwo.prefab", 30),
            Item("weapon.axe2", "Sentinel Axe", "Forged to defend the realm.", EquipmentSlot.Weapon, ItemRarity.Rare, WeaponRoot + "AxeTwo.prefab", 32),
            Item("weapon.mace2", "Wardbreaker", "Steel forged for the front line.", EquipmentSlot.Weapon, ItemRarity.Rare, WeaponRoot + "BluntTwo.prefab", 32),
            Item("weapon.axe3", "Dusk Cleaver", "An edge from a forgotten age.", EquipmentSlot.Weapon, ItemRarity.Epic, WeaponRoot + "AxeThree.prefab", 60),
            Item("weapon.mace3", "Stormcrusher", "Heavy enough to shake a kingdom.", EquipmentSlot.Weapon, ItemRarity.Epic, WeaponRoot + "BluntThree.prefab", 60),
            Item("weapon.sword3", "Crownfall", "A golden blade with a legendary past.", EquipmentSlot.Weapon, ItemRarity.Legendary, WeaponRoot + "SwordThree.prefab", 100)
        };
        foreach (HeroEquipment item in catalog.equipment)
        {
            if (item.prefab == null) throw new InvalidOperationException("Missing asset for " + item.id);
            item.gripPoint = DefaultGripPoint(item.id);
            item.icon = BakeIcon(item.id, item.prefab, item.slot == EquipmentSlot.Skin, item.cloak ? catalog.cloakMaterial : item.slot == EquipmentSlot.Skin ? null : catalog.weaponMaterial, null, item.cloak);
        }
        catalog.chests = new[]
        {
            Chest("chest.common", "Wayfarer Chest", ItemRarity.Common, 400, false, new[] {70,25,4,1}),
            Chest("chest.rare", "Sentinel Chest", ItemRarity.Rare, 1200, false, new[] {0,80,17,3}),
            Chest("chest.epic", "Arcane Chest", ItemRarity.Epic, 50, true, new[] {0,0,90,10}),
            Chest("chest.legendary", "Sovereign Chest", ItemRarity.Legendary, 120, true, new[] {0,0,0,100})
        };
        foreach (HeroChest chest in catalog.chests)
            chest.icon = BakeIcon(chest.id, Resources.Load<GameObject>("Models/Chest"), false, null, HeroCenterCatalog.RarityColor(chest.rarity));
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        BuildScene(catalog);
        Debug.Log("Hero Center ready: 4 heroes, 3 shields, 9 weapons, 4 chest types. Native hero cloaks, source models and textures preserved.");
    }

    private static HeroEquipment Item(string id, string title, string description, EquipmentSlot slot, ItemRarity rarity, string path, int power, bool starter = false, bool cloak = false)
        => new HeroEquipment { id = id, title = title, description = description, slot = slot, rarity = rarity,
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path), power = power, starter = starter, cloak = cloak };
    private static HeroChest Chest(string id, string title, ItemRarity rarity, int price, bool gems, int[] weights)
        => new HeroChest { id = id, title = title, rarity = rarity, price = price, costsGems = gems, weights = weights };

    // The imported pack's origins are near the guard/head; the actual grasp is lower on the handle.
    public static Vector3 DefaultGripPoint(string id)
    {
        switch (id)
        {
            case "weapon.sword1": return new Vector3(0, -0.065f, 0);
            case "weapon.sword2": return new Vector3(0, -0.08f, 0);
            case "weapon.sword3": return new Vector3(0, -0.06f, 0);
            case "weapon.axe1": return new Vector3(0, -0.08f, 0);
            case "weapon.axe2": return new Vector3(0, -0.12f, 0);
            case "weapon.axe3": return new Vector3(0, -0.09f, 0);
            case "weapon.mace1": return new Vector3(0, -0.13f, 0);
            case "weapon.mace2": return new Vector3(0, -0.10f, 0);
            case "weapon.mace3": return new Vector3(0, -0.14f, 0);
            case "armor.oak": return new Vector3(0, 0, -0.18f);
            case "armor.sentinel": return new Vector3(0, 0, -0.20f);
            case "armor.kingsguard": return new Vector3(0, 0, -0.21f);
            default: return Vector3.zero;
        }
    }

    private static void PrepareAtlas(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidOperationException("Import the 2D Mobile Game UI Kit first.");
        // The supplied files are 3118 x 1754. Lift the pack's 2048 cap so the source pixels stay intact.
        if (importer.maxTextureSize >= 4096 && importer.textureCompression == TextureImporterCompression.Uncompressed) return;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }

    private static Sprite SpriteFromPack(int sheet, int index)
    {
        string path = "Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_" + sheet + ".png";
        string name = "UI-pack_Sprite_" + sheet + "_" + index;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First(sprite => sprite.name == name);
    }

    public static void PrepareButtonArt(HeroCenterCatalog catalog)
    {
        catalog.primaryButton = Sliced(catalog, "GoldenAction", SpriteFromPack(1, 21), 25);
        catalog.secondaryButton = Sliced(catalog, "DarkAction", SpriteFromPack(2, 10), 22);
        catalog.beveledButton = Sliced(catalog, "BeveledAction", SpriteFromPack(2, 4), 102);
        EditorUtility.SetDirty(catalog);
    }

    private static Sprite Sliced(HeroCenterCatalog catalog, string name, Sprite source, float edge)
    {
        Sprite existing = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(catalog)).OfType<Sprite>().FirstOrDefault(s => s.name == name);
        if (existing != null) return existing;
        Sprite sliced = Sprite.Create(source.texture, source.rect, Vector2.one * 0.5f, source.pixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
        sliced.name = name; AssetDatabase.AddObjectToAsset(sliced, catalog); return sliced;
    }

    private static Material PrepareWeaponMaterial()
    {
        string path = Root + "/Materials/Weapons_URP.mat";
        Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result != null) return result;
        Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/GameLair/MedievalFantasyWeaponPack/Materials/WeaponMaterial.mat");
        result = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Weapons_URP" };
        result.SetTexture("_BaseMap", source.mainTexture);
        result.SetColor("_BaseColor", Color.white); result.SetFloat("_Smoothness", 0.28f);
        AssetDatabase.CreateAsset(result, path); return result;
    }

    private static Material PrepareCloakMaterial()
    {
        string path = Root + "/Materials/Cloaks_URP.mat";
        Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result != null) return result;
        result = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Pack_Heros/Materials/ColorPalette.mat")) { name = "Cloaks_URP" };
        result.SetFloat("_Cull", 0);
        AssetDatabase.CreateAsset(result, path); return result;
    }

    private static Sprite BakeIcon(string id, GameObject prefab, bool isHero, Material replacement, Color? chestColor, bool force = false)
    {
        string path = Root + "/Icons/" + id + ".png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null && !force) return existing;
        var rig = new GameObject("ThumbnailRig") { hideFlags = HideFlags.HideAndDontSave };
        rig.transform.position = new Vector3(2000, 0, 0);
        RenderTexture texture = null;
        Texture2D pixels = null;
        RenderTexture previous = RenderTexture.active;
        temporaryMaterials.Clear();
        try
        {
            GameObject model = UnityEngine.Object.Instantiate(prefab, rig.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0, isHero ? 158 : 145, isHero || chestColor.HasValue ? 0 : -16);
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                if (animator.runtimeAnimatorController != null)
                {
                    AnimationClip idle = animator.runtimeAnimatorController.animationClips.FirstOrDefault(clip => clip.name.Contains("Idle"));
                    if (idle != null) idle.SampleAnimation(animator.gameObject, 0.35f);
                }
                animator.enabled = false;
            }
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (replacement != null) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => replacement).ToArray();
                if (chestColor.HasValue)
                {
                    bool metal = renderer.name.IndexOf("gold", StringComparison.OrdinalIgnoreCase) >= 0 || renderer.name.IndexOf("lock", StringComparison.OrdinalIgnoreCase) >= 0 || renderer.name.IndexOf("band", StringComparison.OrdinalIgnoreCase) >= 0;
                    var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.color = metal ? chestColor.Value : Color.Lerp(new Color32(67, 43, 34, 255), chestColor.Value, 0.3f);
                    material.SetFloat("_Metallic", metal ? 0.45f : 0); material.SetFloat("_Smoothness", 0.3f);
                    renderer.sharedMaterial = material; temporaryMaterials.Add(material);
                }
            }
            Bounds bounds = HeroAppearance.BoundsOf(model);
            var camera = new GameObject("ThumbnailCamera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(rig.transform, false);
            camera.transform.position = bounds.center + new Vector3(0, bounds.size.y * 0.10f, -Mathf.Max(10, bounds.size.magnitude * 3));
            camera.transform.LookAt(bounds.center);
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x) * 0.60f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 29; camera.allowHDR = false; camera.farClipPlane = 1000;
            foreach (Vector3 rotation in new[] {new Vector3(35, -35, 0), new Vector3(10, 140, 0)})
            {
                var light = new GameObject("ThumbnailLight", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(rig.transform, false); light.transform.rotation = Quaternion.Euler(rotation);
                light.type = LightType.Directional; light.intensity = 1.6f; light.cullingMask = 1 << 29;
            }
            foreach (Transform child in rig.GetComponentsInChildren<Transform>()) child.gameObject.layer = 29;
            texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            texture.Create(); camera.targetTexture = texture; camera.Render();
            RenderTexture.active = texture;
            pixels = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(rig);
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            foreach (Material material in temporaryMaterials) UnityEngine.Object.DestroyImmediate(material);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 512;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void BuildScene(HeroCenterCatalog catalog)
    {
        Scene scene;
        if (File.Exists(ScenePath))
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
            {
                EditorSceneManager.SaveOpenScenes();
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }
        else
        {
            EditorSceneManager.SaveOpenScenes();
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(10, 21, 37, 255); camera.cullingMask = ~((1 << 29) | (1 << 30));
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.44f, 0.48f, 0.57f); RenderSettings.fog = false;
            new GameObject("HeroCenter").AddComponent<HeroCenterController>();
        }
        SceneManager.SetActiveScene(scene);
        HeroCenterController menu = UnityEngine.Object.FindAnyObjectByType<HeroCenterController>();
        if (menu == null) throw new InvalidOperationException("Existing Hero Center scene has no controller.");
        menu.Configure(catalog); menu.BuildPreview();
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(entry => entry.path == ScenePath);
        int startup = scenes.FindIndex(entry => entry.path == StartupLoadingSetup.ScenePath);
        scenes.Insert(startup < 0 ? 0 : startup + 1, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Selection.activeGameObject = menu.gameObject;
    }
}
