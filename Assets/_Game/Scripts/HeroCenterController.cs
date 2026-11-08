using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace TowerTrials
{
    public sealed class HeroCenterController : MonoBehaviour
    {
        [SerializeField] private HeroCenterCatalog catalog;
        private static readonly Color Ink = new Color32(10, 21, 37, 255);
        private static readonly Color Surface = new Color32(19, 38, 58, 245);
        private static readonly Color Muted = new Color32(158, 183, 202, 255);
        private static readonly Color White = new Color32(245, 243, 233, 255);
        private static readonly Color Gold = HeroCenterCatalog.RarityColor(ItemRarity.Legendary);
        private readonly System.Random random = new System.Random();
        private readonly List<UnityEngine.UI.Button> chestButtons = new List<UnityEngine.UI.Button>();
        private readonly Dictionary<string, UnityEngine.UI.Image> itemBorders = new Dictionary<string, UnityEngine.UI.Image>();
        private readonly Dictionary<string, TMP_Text> itemStates = new Dictionary<string, TMP_Text>();
        private HeroProfile profile;
        private RectTransform safe, body, heroRect, inventoryContent, details, modal, modalCard, toast;
        private readonly RectTransform[] pages = new RectTransform[3];
        private readonly UnityEngine.UI.Image[] navFaces = new UnityEngine.UI.Image[3];
        private readonly TMP_Text[] navLabels = new TMP_Text[3];
        private readonly TMP_Text[] slotLabels = new TMP_Text[3];
        private readonly UnityEngine.UI.Image[] slotIcons = new UnityEngine.UI.Image[3];
        private readonly TMP_Text[] categoryLabels = new TMP_Text[3];
        private TMP_Text goldText, gemsText, title, subtitle, heroName, powerText, ownedText, dailyText;
        private HeroPreview preview;
        private HeroEquipment selected;
        private EquipmentSlot category;
        private int page;
        private bool openingChest, loading;
        private Rect lastSafeArea;
        private Vector2 lastScreen;
        private Coroutine toastRoutine;
        public HeroProfile Profile => profile;
        public HeroCenterCatalog Catalog => catalog;
        public int ActivePage => page;
        public bool OpeningChest => openingChest;

        public void Configure(HeroCenterCatalog source) => catalog = source;
        private IEnumerator Start()
        {
            BuildInterface();
            // CanvasScaler resolves its scale in Update. Rebuild SDF vertex scale after that first frame.
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in safe.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate(true, true);
                text.SetAllDirty();
            }
        }
        public void BuildPreview() => BuildInterface();

        private void BuildInterface()
        {
            if (catalog == null) catalog = HeroCenterCatalog.Load();
            if (catalog == null) throw new InvalidOperationException("Hero Center catalog is missing. Run Tower Trials > Set Up Hero Center.");
            // Clean previews saved by the earlier editor layout; new stages belong to MenuPresentation.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject legacy = transform.GetChild(i).gameObject;
                if (legacy.name != "HeroPreviewStage") continue;
                legacy.SetActive(false);
                if (Application.isPlaying) Destroy(legacy); else DestroyImmediate(legacy);
            }
            Transform previous = transform.Find("MenuPresentation");
            if (previous != null)
            {
                previous.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(previous.gameObject); else DestroyImmediate(previous.gameObject);
            }
            profile = HeroProfile.Load(catalog);
            chestButtons.Clear(); itemBorders.Clear(); itemStates.Clear();
            openingChest = false; loading = false;
            AudioListener.volume = PlayerPrefs.GetInt("HeroWars.Sound", 1);
            Application.targetFrameRate = 60;
            var presentation = new GameObject("MenuPresentation").transform;
            presentation.SetParent(transform, false);
            var canvasObject = new GameObject("HeroCenterCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(presentation, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            RectTransform root = canvasObject.GetComponent<RectTransform>();
            Panel("Midnight", root, Vector2.zero, Vector2.one, Ink);
            if (catalog.background != null)
            {
                var art = Rect("AncientChasm", root, Vector2.zero, Vector2.one).gameObject.AddComponent<UnityEngine.UI.RawImage>();
                art.texture = catalog.background; art.color = new Color(0.38f, 0.47f, 0.59f, 0.38f); art.raycastTarget = false;
                float imageAspect = (float)catalog.background.width / catalog.background.height;
                float aspect = (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
                float crop = Mathf.Min(1, aspect / imageAspect);
                art.uvRect = new Rect((1 - crop) * 0.5f, 0, crop, 1);
            }
            safe = Rect("SafeArea", root, Vector2.zero, Vector2.one);
            BuildWallet();
            title = Label("ScreenTitle", safe, "Hero Center", 76, White, GameTextRole.Display);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -156), new Vector2(980, 128));
            title.textWrappingMode = TextWrappingModes.NoWrap;
            subtitle = Label("ScreenSubtitle", safe, "A legend in the making.", 30, Muted);
            Place(subtitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -276), new Vector2(970, 50));
            body = Rect("Pages", safe, new Vector2(0.045f, 0.115f), new Vector2(0.955f, 0.807f));
            heroRect = Rect("InteractiveHeroPortrait", body, new Vector2(0.01f, 0.33f), new Vector2(0.99f, 1.015f));
            var portrait = heroRect.gameObject.AddComponent<UnityEngine.UI.RawImage>(); portrait.raycastTarget = true;
            preview = heroRect.gameObject.AddComponent<HeroPreview>();
            for (int i = 0; i < pages.Length; i++) pages[i] = Rect(new[] { "HeroCenter", "Customize", "Shop" }[i], body, Vector2.zero, Vector2.one);
            BuildHome(); BuildCustomize(); BuildShop(); BuildNavigation();
            modal = Panel("ModalOverlay", safe, Vector2.zero, Vector2.one, new Color(0.015f, 0.025f, 0.05f, 0.94f));
            modal.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            modalCard = Panel("ModalCard", modal, new Vector2(0.055f, 0.19f), new Vector2(0.945f, 0.82f), Surface);
            Frame(modalCard, new Color32(79, 105, 126, 255));
            modal.gameObject.SetActive(false);
            toast = Panel("Notification", safe, new Vector2(0.08f, 0), new Vector2(0.92f, 0), Surface);
            toast.pivot = new Vector2(0.5f, 0); toast.sizeDelta = new Vector2(0, 76); toast.anchoredPosition = new Vector2(0, 198);
            Label("Message", toast, "", 28, White).alignment = TextAlignmentOptions.Center;
            toast.gameObject.SetActive(false);
            if (Application.isPlaying && EventSystem.current == null)
            {
                var events = new GameObject("MenuEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(presentation, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            lastScreen = Vector2.zero;
            UpdateSafeArea(); Canvas.ForceUpdateCanvases();
            preview.Configure(portrait, catalog, profile, presentation);
            ShowPage(0); RefreshWallet();
        }

        private void BuildWallet()
        {
            RectTransform header = Panel("PlayerAndWallet", safe, new Vector2(0, 1), Vector2.one, new Color(0.025f, 0.065f, 0.11f, 0.87f));
            header.pivot = new Vector2(0.5f, 1); header.sizeDelta = new Vector2(0, 142); header.anchoredPosition = Vector2.zero;
            var crest = Icon("ProfileCrest", header, catalog.heroIcon, new Vector2(0.04f, 0.23f), new Vector2(0.12f, 0.77f));
            var player = Label("PlayerLevel", header, "HERO\nLevel " + profile.level, 26, White, GameTextRole.Action);
            Fit(player.rectTransform, new Vector2(0.14f, 0.18f), new Vector2(0.30f, 0.84f));
            goldText = Wallet(header, "Gold", catalog.goldIcon, new Vector2(0.325f, 0.22f), new Vector2(0.59f, 0.79f));
            gemsText = Wallet(header, "Gems", catalog.gemIcon, new Vector2(0.615f, 0.22f), new Vector2(0.85f, 0.79f));
            var settings = MakeButton("Settings", header, "", ShowSettings, false);
            Fit(settings.GetComponent<RectTransform>(), new Vector2(0.89f, 0.22f), new Vector2(0.97f, 0.79f));
            Icon("SettingsIcon", settings.transform, catalog.settingsIcon, Vector2.one * 0.18f, Vector2.one * 0.82f);
        }

        private TMP_Text Wallet(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max)
        {
            var button = MakeButton(name + "Wallet", parent, "", () => ShowPage(2), false);
            Fit(button.GetComponent<RectTransform>(), min, max);
            Icon(name + "Icon", button.transform, sprite, new Vector2(0.05f, 0.18f), new Vector2(0.29f, 0.82f));
            TMP_Text value = Label(name + "Amount", button.transform, "", 32, White, GameTextRole.Number);
            value.alignment = TextAlignmentOptions.MidlineRight;
            Fit(value.rectTransform, new Vector2(0.32f, 0.05f), new Vector2(0.90f, 0.95f));
            return value;
        }

        private void BuildHome()
        {
            RectTransform home = pages[0];
            heroName = Label("EquippedHeroName", home, "", 46, White, GameTextRole.Display);
            heroName.alignment = TextAlignmentOptions.Center;
            Fit(heroName.rectTransform, new Vector2(0.12f, 0.283f), new Vector2(0.88f, 0.34f));
            powerText = Label("CollectionPower", home, "", 25, Muted);
            powerText.alignment = TextAlignmentOptions.Center;
            Fit(powerText.rectTransform, new Vector2(0.1f, 0.251f), new Vector2(0.9f, 0.286f));
            var rotate = Label("RotationHint", home, "Drag to rotate", 22, Muted);
            rotate.alignment = TextAlignmentOptions.Center;
            Fit(rotate.rectTransform, new Vector2(0.3f, 0.945f), new Vector2(0.7f, 0.98f));
            var daily = MakeButton("DailyGift", home, "", ClaimDaily, false);
            Fit(daily.GetComponent<RectTransform>(), new Vector2(0.81f, 0.79f), new Vector2(0.995f, 0.93f));
            Icon("DailyGiftIcon", daily.transform, catalog.dailyIcon, new Vector2(0.26f, 0.36f), new Vector2(0.74f, 0.92f));
            dailyText = Label("DailyGiftLabel", daily.transform, "Daily gift", 24, Gold, GameTextRole.Action);
            dailyText.alignment = TextAlignmentOptions.Center;
            Fit(dailyText.rectTransform, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.34f));
            for (int i = 0; i < 3; i++)
            {
                var slot = (EquipmentSlot)i;
                var button = MakeButton(slot + "Shortcut", home, "", () => { ShowPage(1); ShowCategory(slot); }, false);
                Fit(button.GetComponent<RectTransform>(), new Vector2(i / 3f + 0.008f, 0.117f), new Vector2((i + 1) / 3f - 0.008f, 0.24f));
                slotIcons[i] = Icon("EquippedIcon", button.transform, null, new Vector2(0.025f, 0.16f), new Vector2(0.34f, 0.85f));
                slotLabels[i] = Label("EquippedName", button.transform, "", 25, White, GameTextRole.Action);
                Fit(slotLabels[i].rectTransform, new Vector2(0.37f, 0.05f), new Vector2(0.96f, 0.95f));
            }
            var adventure = MakeButton("Adventure", home, "ADVENTURE", StartAdventure, true, 42);
            Fit(adventure.GetComponent<RectTransform>(), new Vector2(0.08f, 0.008f), new Vector2(0.92f, 0.091f));
        }

        private void BuildCustomize()
        {
            RectTransform panel = Panel("Armory", pages[1], Vector2.zero, new Vector2(1, 0.47f), new Color32(10, 25, 43, 250));
            Frame(panel, new Color32(49, 77, 101, 255));
            for (int i = 0; i < 3; i++)
            {
                EquipmentSlot slot = (EquipmentSlot)i;
                var tab = MakeButton("Category" + slot, panel, i == 0 ? "Skins" : i == 1 ? "Armor" : "Weapons", () => ShowCategory(slot), false, 30);
                Fit(tab.GetComponent<RectTransform>(), new Vector2(i / 3f + 0.012f, 0.855f), new Vector2((i + 1) / 3f - 0.012f, 0.983f));
                categoryLabels[i] = tab.GetComponentInChildren<TMP_Text>();
            }
            RectTransform scrollRoot = Rect("EquipmentScroll", panel, new Vector2(0.025f, 0.21f), new Vector2(0.975f, 0.83f));
            var scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            RectTransform viewport = Panel("Viewport", scrollRoot, Vector2.zero, Vector2.one, Color.white);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            inventoryContent = Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            inventoryContent.pivot = new Vector2(0.5f, 1);
            var grid = inventoryContent.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(280, 196); grid.spacing = new Vector2(17, 17);
            grid.padding = new RectOffset(10, 10, 8, 8);
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            var fitter = inventoryContent.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = inventoryContent; scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            details = Rect("SelectedEquipment", panel, new Vector2(0.025f, 0.015f), new Vector2(0.975f, 0.195f));
            ownedText = Label("OwnedCount", pages[1], "", 25, Muted);
            ownedText.alignment = TextAlignmentOptions.Center;
            Fit(ownedText.rectTransform, new Vector2(0.2f, 0.475f), new Vector2(0.8f, 0.51f));
        }

        private void BuildShop()
        {
            var intro = Label("ChestExplanation", pages[2], "Every chest holds one piece of equipment or a hero skin.\nUnlock it here. Make it yours in Customize.", 30, Muted);
            Fit(intro.rectTransform, new Vector2(0.015f, 0.87f), new Vector2(0.985f, 0.985f));
            for (int i = 0; i < catalog.chests.Length; i++)
            {
                HeroChest chest = catalog.chests[i];
                Color color = HeroCenterCatalog.RarityColor(chest.rarity);
                int column = i % 2, row = i / 2;
                RectTransform card = Panel(chest.id, pages[2], new Vector2(column * 0.51f, 0.465f - row * 0.415f),
                    new Vector2(column * 0.51f + 0.49f, 0.855f - row * 0.415f), Surface);
                Frame(card, color);
                Label("ChestRarity", card, chest.rarity.ToString().ToUpperInvariant(), 24, color, GameTextRole.Action,
                    new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.97f));
                Icon("ChestArt", card, chest.icon, new Vector2(0.15f, 0.35f), new Vector2(0.85f, 0.88f));
                TMP_Text name = Label("ChestName", card, chest.title, 34, White, GameTextRole.Display,
                    new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.40f));
                name.alignment = TextAlignmentOptions.Center;
                var open = MakeButton("Open_" + chest.id, card, chest.price.ToString("N0", CultureInfo.InvariantCulture), () => OpenChest(chest.id), true, 32);
                Fit(open.GetComponent<RectTransform>(), new Vector2(0.13f, 0.105f), new Vector2(0.87f, 0.265f));
                TMP_Text cost = open.GetComponentInChildren<TMP_Text>(); Fit(cost.rectTransform, new Vector2(0.30f, 0), new Vector2(0.95f, 1));
                Icon("Currency", open.transform, chest.costsGems ? catalog.gemIcon : catalog.goldIcon, new Vector2(0.1f, 0.20f), new Vector2(0.29f, 0.80f));
                chestButtons.Add(open);
                var odds = Label("DropRates", card, chest.Odds, 20, Muted, GameTextRole.Body,
                    new Vector2(0.025f, 0.015f), new Vector2(0.975f, 0.095f));
                odds.alignment = TextAlignmentOptions.Center;
            }
            var note = Label("DuplicatePolicy", pages[2], "Duplicates turn into gold. All rewards are saved on this device.", 23, Muted);
            note.alignment = TextAlignmentOptions.Center;
            Fit(note.rectTransform, Vector2.zero, new Vector2(1, 0.043f));
        }

        private void BuildNavigation()
        {
            RectTransform nav = Panel("BottomNavigation", safe, Vector2.zero, new Vector2(1, 0), new Color32(8, 19, 34, 255));
            nav.pivot = new Vector2(0.5f, 0); nav.sizeDelta = new Vector2(0, 176); nav.anchoredPosition = Vector2.zero;
            Frame(nav, new Color32(47, 69, 88, 255));
            string[] names = { "Hero Center", "Customize", "Shop" };
            Sprite[] icons = { catalog.heroIcon, catalog.armorIcon, catalog.shopIcon };
            for (int i = 0; i < 3; i++)
            {
                int targetPage = i;
                var button = MakeButton("GoTo" + names[i].Replace(" ", ""), nav, "", () => ShowPage(targetPage), false);
                Fit(button.GetComponent<RectTransform>(), new Vector2(i / 3f + 0.009f, 0.04f), new Vector2((i + 1) / 3f - 0.009f, 0.96f));
                navFaces[i] = button.GetComponent<UnityEngine.UI.Image>();
                Icon("TabIcon", button.transform, icons[i], new Vector2(0.38f, 0.43f), new Vector2(0.62f, 0.91f));
                navLabels[i] = Label("TabLabel", button.transform, names[i], 31, White, GameTextRole.Action);
                navLabels[i].alignment = TextAlignmentOptions.Center;
                Fit(navLabels[i].rectTransform, new Vector2(0.02f, 0.045f), new Vector2(0.98f, 0.39f));
            }
        }

        public void ShowPage(int index)
        {
            if (openingChest || loading) return;
            page = Mathf.Clamp(index, 0, 2);
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].gameObject.SetActive(i == page);
                navLabels[i].color = i == page ? Gold : Muted;
                navFaces[i].color = i == page ? new Color32(41, 67, 88, 255) : new Color32(13, 31, 49, 255);
            }
            title.text = new[] { "Hero Center", "Customize", "Shop" }[page];
            subtitle.text = new[] { "A legend in the making.", "Your hero. Your signature.", "Open a chest. Discover your next legend." }[page];
            heroRect.gameObject.SetActive(page != 2);
            if (page != 2)
            {
                Fit(heroRect, new Vector2(0.01f, page == 0 ? 0.33f : 0.49f), new Vector2(0.99f, 1.015f));
                preview.Show(catalog, profile); Canvas.ForceUpdateCanvases(); preview.Resize();
            }
            if (page == 1) ShowCategory(category);
            if (page == 0) RefreshHome();
        }

        public void ShowCategory(EquipmentSlot slot)
        {
            category = slot;
            for (int i = 0; i < categoryLabels.Length; i++) categoryLabels[i].color = i == (int)slot ? Gold : Muted;
            ClearChildren(inventoryContent); itemBorders.Clear(); itemStates.Clear();
            var items = new List<HeroEquipment>();
            foreach (HeroEquipment item in catalog.equipment) if (item.slot == slot) items.Add(item);
            items.Sort((a, b) => { int ownership = profile.Owns(b.id).CompareTo(profile.Owns(a.id)); return ownership == 0 ? a.rarity.CompareTo(b.rarity) : ownership; });
            foreach (HeroEquipment item in items)
            {
                Color color = HeroCenterCatalog.RarityColor(item.rarity);
                var button = MakeButton("Item_" + item.id, inventoryContent, "", () => SelectEquipment(item.id), false);
                RectTransform rect = button.GetComponent<RectTransform>();
                var edge = Frame(rect, color); itemBorders[item.id] = edge;
                Icon("EquipmentArt", rect, item.icon, new Vector2(0.18f, 0.28f), new Vector2(0.82f, 0.96f));
                Label("EquipmentName", rect, item.title, 24, White, GameTextRole.Action, new Vector2(0.045f, 0.12f), new Vector2(0.955f, 0.34f)).alignment = TextAlignmentOptions.Center;
                var state = Label("Ownership", rect, "", 20, color, GameTextRole.Body, new Vector2(0.03f, 0.005f), new Vector2(0.97f, 0.145f));
                state.alignment = TextAlignmentOptions.Center; itemStates[item.id] = state;
            }
            inventoryContent.anchoredPosition = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            var grid = inventoryContent.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            float width = ((RectTransform)inventoryContent.parent).rect.width;
            grid.cellSize = new Vector2(Mathf.Max(120, (width - 54) / 3), 196);
            ownedText.text = profile.owned.Count + " / " + catalog.equipment.Length + " items unlocked  |  Drag hero to rotate";
            SelectEquipment(profile.Equipped(slot));
        }

        public void SelectEquipment(string id)
        {
            selected = catalog.Find(id);
            if (selected == null) return;
            RefreshItemStates(); ClearChildren(details);
            Color color = HeroCenterCatalog.RarityColor(selected.rarity);
            Label("SelectionName", details, selected.title, 31, White, GameTextRole.Action, new Vector2(0.015f, 0.46f), new Vector2(0.65f, 1));
            Label("SelectionDescription", details, selected.rarity + "  /  " + selected.description, 23, color, GameTextRole.Body, new Vector2(0.015f, 0.02f), new Vector2(0.65f, 0.48f));
            bool owned = profile.Owns(selected.id), equipped = profile.Equipped(selected.slot) == selected.id;
            var action = MakeButton("EquipSelection", details, equipped ? "Equipped" : owned ? "Equip" : "Find in chests", () =>
            {
                if (!profile.Owns(selected.id)) { ShowPage(2); return; }
                Equip(selected.id);
            }, owned && !equipped, owned ? 29 : 23);
            Fit(action.GetComponent<RectTransform>(), new Vector2(0.68f, 0.10f), new Vector2(0.985f, 0.91f));
            action.interactable = !equipped;
            HeroProfile sample = JsonUtility.FromJson<HeroProfile>(JsonUtility.ToJson(profile));
            if (selected.slot == EquipmentSlot.Skin) sample.skin = selected.id;
            else if (selected.slot == EquipmentSlot.Armor) sample.armor = selected.id;
            else sample.weapon = selected.id;
            preview.Show(catalog, sample);
        }

        public bool Equip(string id)
        {
            HeroEquipment item = catalog.Find(id);
            if (!profile.TryEquip(item)) return false;
            profile.Save();
            if (page != 2) preview.Show(catalog, profile);
            RefreshHome();
            if (page == 1) SelectEquipment(id);
            return true;
        }

        private void RefreshItemStates()
        {
            foreach (var pair in itemStates)
            {
                HeroEquipment item = catalog.Find(pair.Key);
                pair.Value.text = profile.Equipped(item.slot) == item.id ? "EQUIPPED" : profile.Owns(item.id) ? item.rarity + "  /  Owned" : item.rarity + "  /  Locked";
                itemBorders[pair.Key].color = selected != null && selected.id == pair.Key ? Gold : HeroCenterCatalog.RarityColor(item.rarity);
            }
        }

        private void RefreshWallet()
        {
            goldText.text = profile.gold.ToString("N0", CultureInfo.InvariantCulture);
            gemsText.text = profile.gems.ToString("N0", CultureInfo.InvariantCulture);
            for (int i = 0; i < chestButtons.Count; i++)
            {
                HeroChest chest = catalog.chests[i];
                chestButtons[i].interactable = !openingChest && (chest.costsGems ? profile.gems : profile.gold) >= chest.price;
            }
            RefreshHome();
        }

        private void RefreshHome()
        {
            HeroEquipment skin = catalog.Find(profile.skin);
            heroName.text = skin.title;
            powerText.text = (skin.starter ? "STARTER HERO" : skin.rarity.ToString().ToUpperInvariant() + " HERO") + "   /   COLLECTION POWER " + profile.EquipmentPower(catalog);
            for (int i = 0; i < 3; i++)
            {
                HeroEquipment item = catalog.Find(profile.Equipped((EquipmentSlot)i));
                slotLabels[i].text = "<color=#9EB7CA>" + ((EquipmentSlot)i).ToString().ToUpperInvariant() + "</color>\n" + item.title;
                slotIcons[i].sprite = item.icon;
                slotIcons[i].color = Color.white;
            }
            dailyText.text = profile.CanClaimDaily(DateTime.UtcNow) ? "Daily gift" : "Claimed";
        }

        public void OpenChest(string id)
        {
            if (openingChest || loading || !Application.isPlaying) return;
            HeroChest chest = Array.Find(catalog.chests, entry => entry.id == id);
            if (!profile.TryOpenChest(catalog, chest, random, out HeroEquipment reward, out bool duplicate, out int compensation))
            { Notify("You need more " + (chest != null && chest.costsGems ? "gems" : "gold") + ". Claim your daily gift in Hero Center."); return; }
            openingChest = true; profile.Save(); RefreshWallet();
            StartCoroutine(RevealReward(reward, duplicate, compensation));
        }

        private IEnumerator RevealReward(HeroEquipment reward, bool duplicate, int compensation)
        {
            ClearChildren(modalCard); modal.gameObject.SetActive(true);
            Color color = HeroCenterCatalog.RarityColor(reward.rarity);
            Frame(modalCard, color);
            Label("LootRarity", modalCard, reward.rarity.ToString().ToUpperInvariant(), 37, color, GameTextRole.Action, new Vector2(0.06f, 0.85f), new Vector2(0.94f, 0.94f)).alignment = TextAlignmentOptions.Center;
            var art = Icon("RewardArt", modalCard, reward.icon, new Vector2(0.15f, 0.36f), new Vector2(0.85f, 0.84f));
            Label("LootName", modalCard, reward.title, 52, White, GameTextRole.Display, new Vector2(0.04f, 0.29f), new Vector2(0.96f, 0.39f)).alignment = TextAlignmentOptions.Center;
            Label("LootStatus", modalCard, duplicate ? "Already owned  /  +" + compensation + " gold" : "NEW " + reward.slot.ToString().ToUpperInvariant() + " UNLOCKED", 28, color, GameTextRole.Action, new Vector2(0.04f, 0.22f), new Vector2(0.96f, 0.29f)).alignment = TextAlignmentOptions.Center;
            var equip = MakeButton("EquipReward", modalCard, "Equip now", () => { Equip(reward.id); CloseModal(); ShowPage(1); ShowCategory(reward.slot); }, true, 34);
            Fit(equip.GetComponent<RectTransform>(), new Vector2(0.12f, 0.115f), new Vector2(0.88f, 0.205f));
            var close = MakeButton("Continue", modalCard, "Continue", CloseModal, false, 30);
            Fit(close.GetComponent<RectTransform>(), new Vector2(0.24f, 0.025f), new Vector2(0.76f, 0.10f));
            equip.interactable = close.interactable = false;
            float time = 0;
            while (time < 0.5f)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / 0.5f);
                art.transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1, 1 - Mathf.Pow(1 - t, 3));
                yield return null;
            }
            openingChest = false; equip.interactable = close.interactable = true; RefreshWallet();
        }

        private void ClaimDaily()
        {
            if (!profile.TryClaimDaily(DateTime.UtcNow)) { Notify("Today's gift is claimed. A new gift arrives tomorrow."); return; }
            profile.Save(); RefreshWallet(); Notify("Daily gift claimed: +300 gold and +15 gems.");
        }

        private void ShowSettings()
        {
            if (openingChest) return;
            ClearChildren(modalCard); modal.gameObject.SetActive(true);
            Label("SettingsTitle", modalCard, "Settings", 64, White, GameTextRole.Display, new Vector2(0.07f, 0.78f), new Vector2(0.93f, 0.93f)).alignment = TextAlignmentOptions.Center;
            UnityEngine.UI.Button sound = null;
            sound = MakeButton("SoundToggle", modalCard, "Sound: " + (AudioListener.volume > 0 ? "On" : "Off"), () =>
            {
                bool enabled = AudioListener.volume <= 0;
                AudioListener.volume = enabled ? 1 : 0;
                PlayerPrefs.SetInt("HeroWars.Sound", enabled ? 1 : 0); PlayerPrefs.Save();
                sound.GetComponentInChildren<TMP_Text>().text = "Sound: " + (enabled ? "On" : "Off");
            }, false, 36);
            Fit(sound.GetComponent<RectTransform>(), new Vector2(0.15f, 0.60f), new Vector2(0.85f, 0.72f));
            Label("Help", modalCard, "Equip owned items in Customize.\nOpen Shop chests to unlock more.\nDuplicates award gold.\n\nHero art: OlyPoly\nWeapons: Game Lair Studio\nUI kit: 300Mind", 31, Muted, GameTextRole.Body, new Vector2(0.10f, 0.20f), new Vector2(0.90f, 0.57f)).alignment = TextAlignmentOptions.Center;
            var close = MakeButton("CloseSettings", modalCard, "Back to hero", CloseModal, true, 34);
            Fit(close.GetComponent<RectTransform>(), new Vector2(0.17f, 0.055f), new Vector2(0.83f, 0.16f));
        }

        public void CloseModal()
        {
            if (openingChest) return;
            modal.gameObject.SetActive(false);
        }

        public void StartAdventure()
        {
            if (loading || openingChest || !Application.isPlaying) return;
            profile.Save(); loading = true;
            SceneManager.LoadSceneAsync("TowerPrototype");
        }

        public static void ReturnToHeroCenter()
        { SceneManager.LoadSceneAsync("HeroCenter"); }

        private void Notify(string message)
        {
            if (!Application.isPlaying) return;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toast.GetComponentInChildren<TMP_Text>().text = message;
            toastRoutine = StartCoroutine(ShowToast());
        }
        private IEnumerator ShowToast()
        { toast.gameObject.SetActive(true); yield return new WaitForSecondsRealtime(3.5f); toast.gameObject.SetActive(false); toastRoutine = null; }

        private void Update()
        {
            if (safe == null) return;
            UpdateSafeArea();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (modal.gameObject.activeSelf) CloseModal(); else ShowPage(0);
            }
        }

        private void UpdateSafeArea()
        {
            Vector2 screen = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            if (screen == lastScreen && Screen.safeArea == lastSafeArea) return;
            lastScreen = screen; lastSafeArea = Screen.safeArea;
            safe.anchorMin = lastSafeArea.position / screen; safe.anchorMax = lastSafeArea.max / screen;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }

        private UnityEngine.UI.Button MakeButton(string name, Transform parent, string text, UnityEngine.Events.UnityAction callback, bool primary, float fontSize = 30)
        {
            RectTransform rect = Panel(name, parent, Vector2.zero, Vector2.one, Color.white);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.sprite = primary ? catalog.primaryButton : catalog.secondaryButton;
            image.type = UnityEngine.UI.Image.Type.Sliced; image.raycastTarget = true;
            if (!primary) image.color = new Color32(29, 54, 76, 255);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var palette = button.colors;
            palette.highlightedColor = new Color(1, 1, 1, 1); palette.pressedColor = new Color(0.72f, 0.78f, 0.86f, 1);
            palette.selectedColor = new Color(1, 0.94f, 0.77f, 1); palette.disabledColor = new Color(0.52f, 0.58f, 0.67f, 0.9f);
            palette.fadeDuration = 0.1f; button.colors = palette;
            button.onClick.AddListener(callback);
            TMP_Text label = Label("Label", rect, text, fontSize, primary ? Ink : White, GameTextRole.Action);
            label.alignment = TextAlignmentOptions.Center;
            Fit(label.rectTransform, new Vector2(0.035f, 0.04f), new Vector2(0.965f, 0.96f));
            return button;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); Fit(rect, min, max); return rect;
        }
        private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            RectTransform rect = Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false;
            return rect;
        }
        private static UnityEngine.UI.Image Icon(string name, Transform parent, Sprite sprite, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            if (sprite == null) image.color = Color.clear;
            return image;
        }
        private static TMP_Text Label(string name, Transform parent, string text, float size, Color color, GameTextRole role = GameTextRole.Body)
        {
            var label = Rect(name, parent, Vector2.zero, Vector2.one).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = GameUiTheme.Font(role); label.text = text; label.fontSize = size; label.color = color;
            label.fontStyle = FontStyles.Normal; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }
        private static TMP_Text Label(string name, Transform parent, string text, float size, Color color, GameTextRole role, Vector2 min, Vector2 max)
        { TMP_Text label = Label(name, parent, text, size, color, role); Fit(label.rectTransform, min, max); return label; }
        private static void Fit(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.pivot = Vector2.one * 0.5f; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position; }
        private static UnityEngine.UI.Image Frame(RectTransform parent, Color color)
        {
            RectTransform top = Panel("RarityEdge", parent, new Vector2(0, 1), Vector2.one, color);
            top.offsetMin = new Vector2(0, -3);
            Panel("BottomEdge", parent, Vector2.zero, new Vector2(1, 0), color * new Color(1, 1, 1, 0.35f)).offsetMax = new Vector2(0, 1);
            return top.GetComponent<UnityEngine.UI.Image>();
        }
        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject; child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }
    }
}
