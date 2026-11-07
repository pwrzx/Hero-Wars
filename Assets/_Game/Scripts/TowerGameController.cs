using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace TowerTrials
{
    public sealed class TowerGameController : MonoBehaviour
    {
        [SerializeField] private CampaignConfig campaign;
        [SerializeField] private Camera gameCamera;
        [SerializeField, Range(15, 100)] private float swipeThreshold = 48;
        [SerializeField, Range(0.1f, 1)] private float attackDuration = 0.26f;
        private const string SaveKey = "TowerTrials.Run.v1";
        private const float FloorY = 0.7f, RoomHeight = 1.65f, ActiveX = 1.05f, HomeX = -2.55f;
        private static readonly Color Gold = new Color(1, 0.77f, 0.35f);
        private static readonly Color Ink = new Color(0.025f, 0.085f, 0.12f, 0.96f);
        private static readonly Color Paper = new Color(0.88f, 0.95f, 0.93f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private Transform presentation, hero;
        private RectTransform canvasRect, safeArea, resultPanel;
        private TMP_Text stageLabel, powerLabel, hint, resultTitle, resultDetail;
        private UnityEngine.UI.Button continueButton;
        private TMP_Text continueLabel;
        private readonly List<TowerView> towers = new List<TowerView>();
        private readonly List<Badge> badges = new List<Badge>();
        private readonly List<UnityEngine.UI.Image> progress = new List<UnityEngine.UI.Image>();
        private readonly Dictionary<string, InputActionReference> inputReferences = new Dictionary<string, InputActionReference>();
        private int stageIndex;
        private bool busy, preview;
        private Rect lastSafeArea;
        private Vector2 lastScreen;
        public StageRun Run { get; private set; }
        public bool Busy => busy;
        public int StageIndex => stageIndex;
        public Camera GameCamera => gameCamera;

        private sealed class TowerView
        {
            public Transform root;
            public Transform[] rooms;
            public Transform[] enemies;
            public GameObject[] markers;
        }
        private sealed class Badge
        {
            public Transform target;
            public RectTransform rect;
            public Vector3 offset;
        }

        public void Configure(CampaignConfig config, Camera camera)
        { campaign = config; gameCamera = camera; }

        private void Start()
        {
            preview = false;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            if (campaign == null || campaign.stages == null || campaign.stages.Length == 0)
                throw new InvalidOperationException("Assign a campaign to TowerGameController.");
            RunSave saved = null;
            try { saved = JsonUtility.FromJson<RunSave>(PlayerPrefs.GetString(SaveKey, "")); }
            catch (ArgumentException) { /* An interrupted or obsolete save starts a fresh run. */ }
            stageIndex = saved == null ? 0 : Mathf.Clamp(saved.stageIndex, 0, campaign.stages.Length - 1);
            if (saved == null || saved.stageIndex != stageIndex ||
                !StageRun.TryRestore(campaign.stages[stageIndex], saved, out var restored))
                Run = new StageRun(campaign.stages[stageIndex]);
            else Run = restored;
            RebuildPresentation();
            SaveProgress();
        }

        public void BuildPreview()
        {
            preview = true;
            stageIndex = 0;
            Run = new StageRun(campaign.stages[0]);
            RebuildPresentation();
            UpdateLayout();
        }

        private void LateUpdate()
        {
            if (Run == null || gameCamera == null || canvasRect == null) return;
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            Vector2 dimensions = new Vector2(Screen.width, Screen.height);
            if (dimensions != lastScreen || Screen.safeArea != lastSafeArea)
            {
                lastScreen = dimensions;
                lastSafeArea = Screen.safeArea;
                safeArea.anchorMin = lastSafeArea.position / dimensions;
                safeArea.anchorMax = (lastSafeArea.position + lastSafeArea.size) / dimensions;
                gameCamera.orthographicSize = Mathf.Max(6.4f, 3.9f / gameCamera.aspect);
                Canvas.ForceUpdateCanvases();
            }
            foreach (Badge badge in badges)
            {
                if (badge.rect == null) continue;
                bool visible = badge.target != null && badge.target.gameObject.activeInHierarchy;
                badge.rect.gameObject.SetActive(visible);
                if (!visible) continue;
                Vector3 screen = gameCamera.WorldToScreenPoint(badge.target.TransformPoint(badge.offset));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 point);
                badge.rect.anchoredPosition = point;
            }
        }

        private void RebuildPresentation()
        {
            Transform previous = transform.Find("Presentation");
            if (previous != null)
            {
                previous.gameObject.SetActive(false);
                previous.name = "RetiredPresentation";
                if (Application.isPlaying) Destroy(previous.gameObject);
                else DestroyImmediate(previous.gameObject);
            }
            presentation = new GameObject("Presentation").transform;
            presentation.SetParent(transform, false);
            towers.Clear(); badges.Clear(); progress.Clear();
            lastScreen = Vector2.zero;
            BuildWorld();
            BuildHud();
            busy = false;
            RefreshHud();
            UpdateLayout();
            if (Run.Failed || Run.Complete) ShowResult();
        }

        private void BuildWorld()
        {
            // Original Blender exports are the single source of geometry for both characters and rooms.
            var backdrop = new GameObject("DistantRuins").transform;
            backdrop.SetParent(presentation, false);
            for (int i = 0; i < 9; i++)
            {
                float height = 3 + (i * 7 % 5);
                Block("Distant keep", backdrop, new Vector3(-12 + i * 3.1f, height / 2 - 2, 8 + i % 3),
                    new Vector3(1.4f, height, 1.6f), "Backdrop");
            }
            var home = new GameObject("HeroTower").transform;
            home.SetParent(presentation, false);
            home.position = new Vector3(HomeX, FloorY, 0);
            Model("TowerRoom", home, Vector3.zero, 1);
            // An open hero platform keeps the knight and power badge unobstructed.
            Block("Hero foundation", home, new Vector3(0, -2, 0.2f), new Vector3(2.3f, 3.9f, 1.3f), "Stone_DeepTeal");
            hero = Model("Knight", presentation, new Vector3(HomeX, FloorY + 0.12f, -0.12f), 0.78f);

            for (int t = 0; t < Run.Definition.towers.Length; t++)
            {
                var view = new TowerView();
                towers.Add(view);
                if (t < Run.TowerIndex) continue;
                int distance = t - Run.TowerIndex;
                view.root = new GameObject("EnemyTower_" + (t + 1)).transform;
                view.root.SetParent(presentation, false);
                view.root.position = new Vector3(ActiveX + distance * 2.9f, FloorY, distance * 2.4f);
                view.root.localScale = Vector3.one * Mathf.Pow(0.83f, distance);
                int count = Run.Definition.towers[t].enemies.Length;
                view.rooms = new Transform[count];
                view.enemies = new Transform[count];
                view.markers = new GameObject[count];
                for (int r = 0; r < count; r++)
                {
                    view.rooms[r] = Model("TowerRoom", view.root, new Vector3(0, r * RoomHeight, 0), 1);
                    view.rooms[r].name = "Room_" + r;
                    view.rooms[r].gameObject.SetActive(!Run.IsDefeated(t, r));
                    var marker = Block("Target rim", view.root, new Vector3(0, r * RoomHeight + 0.13f, -0.76f),
                        new Vector3(2.1f, 0.045f, 0.06f), "Eye_Amber");
                    marker.gameObject.SetActive(false);
                    view.markers[r] = marker.gameObject;
                    if (Run.IsDefeated(t, r)) continue;
                    Transform enemy = Model("Goblin", view.root, new Vector3(0.12f, r * RoomHeight + 0.13f, -0.12f), 0.86f);
                    view.enemies[r] = enemy;
                }
                Model("TowerCrown", view.root, new Vector3(0, count * RoomHeight, 0), 1);
                Block("Tower foundation", view.root, new Vector3(0, -2, 0.2f), new Vector3(2.3f, 3.9f, 1.3f), "Stone_DeepTeal");
                TintTower(view, distance == 0 ? 1 : 0.56f);
            }
        }

        private void BuildHud()
        {
            var canvasGo = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasGo.transform.SetParent(presentation, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = canvasGo.GetComponent<RectTransform>();
            var labels = Panel("PowerBadges", canvasRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            powerLabel = AddBadge(hero, new Vector3(0, 2.18f, 0), new Color(0.09f, 0.39f, 0.69f), labels, Run.Power);
            for (int t = Run.TowerIndex; t < towers.Count; t++)
            {
                TowerView view = towers[t];
                for (int r = 0; r < view.enemies.Length; r++)
                    if (view.enemies[r] != null)
                        AddBadge(view.enemies[r], new Vector3(0, 1.68f, 0),
                            new Color(0.62f, 0.12f, 0.18f), labels, Run.Definition.towers[t].enemies[r]);
            }
            safeArea = Panel("SafeArea", canvasRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            var input = Panel("SwipeArea", safeArea, new Vector2(0, 0.15f), new Vector2(1, 0.83f),
                Vector2.zero, Vector2.zero, Color.clear);
            input.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            input.gameObject.AddComponent<SwipeAttackInput>().controller = this;

            var header = Panel("Header", safeArea, new Vector2(0, 1), Vector2.one,
                new Vector2(32, -248), new Vector2(-32, -28), Ink);
            var title = Text("GameTitle", header, "TOWER TRIALS", 46, Paper);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -22), new Vector2(710, 68));
            stageLabel = Text("StageTitle", header, "", 26, Gold);
            Place(stageLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -104), new Vector2(920, 46));
            var retry = Button("Retry", header, "RETRY", RestartStage);
            Place(retry.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-24, -24), new Vector2(155, 63));
            int towerCount = Run.Definition.towers.Length;
            for (int t = 0; t < towerCount; t++)
            {
                var bar = Panel("TowerProgress_" + t, header, new Vector2((float)t / towerCount, 0),
                    new Vector2((float)(t + 1) / towerCount, 0), new Vector2(16, 29),
                    new Vector2(-16, 37), new Color(0.17f, 0.29f, 0.32f));
                progress.Add(bar.GetComponent<UnityEngine.UI.Image>());
            }

            var footer = Panel("Footer", safeArea, Vector2.zero, new Vector2(1, 0),
                new Vector2(32, 28), new Vector2(-32, 222), Ink);
            var gesture = Text("Gesture", footer, "SWIPE TOWARD A ROOM  >", 32, Gold);
            Place(gesture.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(940, 55));
            gesture.alignment = TextAlignmentOptions.Center;
            hint = Text("Hint", footer, "", 26, Paper);
            Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(940, 65));
            hint.alignment = TextAlignmentOptions.Center;

            resultPanel = Panel("ResultOverlay", safeArea, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0.01f, 0.035f, 0.05f, 0.87f));
            resultPanel.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var card = Panel("ResultCard", resultPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-450, -280), new Vector2(450, 280), Ink);
            resultTitle = Text("ResultTitle", card, "", 55, Gold);
            Place(resultTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(820, 90));
            resultTitle.alignment = TextAlignmentOptions.Center;
            resultDetail = Text("ResultDetail", card, "", 31, Paper);
            Place(resultDetail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(780, 135));
            resultDetail.alignment = TextAlignmentOptions.Center;
            continueButton = Button("Continue", card, "", Continue);
            continueLabel = continueButton.GetComponentInChildren<TMP_Text>();
            Place(continueButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(700, 95));
            resultPanel.gameObject.SetActive(false);

            if (!preview)
            {
                var eventsGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventsGo.transform.SetParent(presentation, false);
                var module = eventsGo.GetComponent<InputSystemUIInputModule>();
                var actions = InputSystem.actions;
                if (actions == null) throw new InvalidOperationException("Assign the project's InputSystem_Actions as project-wide actions.");
                module.actionsAsset = actions;
                module.point = UiAction(actions, "UI/Point");
                module.leftClick = UiAction(actions, "UI/Click");
                module.move = UiAction(actions, "UI/Navigate");
                module.submit = UiAction(actions, "UI/Submit");
                module.cancel = UiAction(actions, "UI/Cancel");
            }
        }

        private InputActionReference UiAction(InputActionAsset actions, string name)
        {
            if (inputReferences.TryGetValue(name, out InputActionReference reference)) return reference;
            reference = InputActionReference.Create(actions.FindAction(name, true));
            inputReferences.Add(name, reference);
            return reference;
        }

        private void OnDestroy()
        {
            foreach (InputActionReference reference in inputReferences.Values)
                if (reference != null) Destroy(reference);
            inputReferences.Clear();
        }

        public void PreviewSwipe(Vector2 start, Vector2 end)
        {
            if (busy || Run.Failed || Run.Complete) return;
            SelectRoom(FindRoom(end));
        }

        public bool SubmitSwipe(Vector2 start, Vector2 end)
        {
            if (busy || Run == null || Run.Failed || Run.Complete) return false;
            float threshold = swipeThreshold * Screen.width / 1080f;
            Vector2 delta = end - start;
            if (delta.sqrMagnitude < threshold * threshold || delta.x < threshold * 0.5f)
            {
                SelectRoom(-1);
                hint.text = "Swipe right toward an enemy room.";
                return false;
            }
            int room = FindRoom(end);
            if (room < 0) return false;
            busy = true; // Lock before the first animation frame: a second finger cannot award power twice.
            SelectRoom(-1);
            StartCoroutine(Attack(room));
            return true;
        }

        public Vector2 EnemyScreenPosition(int room) => gameCamera.WorldToScreenPoint(
            towers[Run.TowerIndex].enemies[room].position + Vector3.up * 0.65f);

        private int FindRoom(Vector2 position)
        {
            if (Run.Complete) return -1;
            TowerView tower = towers[Run.TowerIndex];
            float best = float.MaxValue;
            int room = -1;
            for (int i = 0; i < tower.enemies.Length; i++)
            {
                if (tower.enemies[i] == null || !tower.enemies[i].gameObject.activeSelf) continue;
                float distance = Mathf.Abs(position.y - EnemyScreenPosition(i).y);
                if (distance < best) { best = distance; room = i; }
            }
            return room;
        }

        private void SelectRoom(int room)
        {
            if (Run.Complete) return;
            TowerView tower = towers[Run.TowerIndex];
            for (int i = 0; i < tower.markers.Length; i++) tower.markers[i].SetActive(i == room);
        }

        private IEnumerator Attack(int room)
        {
            int index = Run.TowerIndex;
            TowerView tower = towers[index];
            Transform enemy = tower.enemies[room];
            long gained = Run.Definition.towers[index].enemies[room];
            Vector3 home = hero.position;
            Vector3 destination = enemy.position + new Vector3(-0.63f, 0.08f, -0.25f);
            yield return MoveHero(home, destination, attackDuration, 0.65f);
            Quaternion idleRotation = hero.localRotation;
            for (float time = 0; time < 0.14f; time += Time.deltaTime)
            {
                hero.localRotation = idleRotation * Quaternion.Euler(0, 0, -24 * Mathf.Sin(time / 0.14f * Mathf.PI));
                yield return null;
            }
            hero.localRotation = idleRotation;
            AttackOutcome outcome = Run.Attack(index, room);
            SaveProgress(); // Save the resolved transaction, before cosmetic animations can be interrupted.
            if (outcome == AttackOutcome.Lost)
            {
                hint.text = "Too strong. Find a different order.";
                yield return MoveHero(destination, home, attackDuration, 0.3f);
                busy = false;
                ShowResult();
                yield break;
            }
            enemy.gameObject.SetActive(false);
            tower.rooms[room].gameObject.SetActive(false);
            powerLabel.text = Run.Power.ToString("N0");
            hint.text = "+" + gained.ToString("N0") + " POWER";
            Transform slash = Block("ImpactFlash", presentation, destination + new Vector3(0.6f, 0.65f, -0.2f),
                new Vector3(0.10f, 1.4f, 0.04f), "Eye_Amber");
            slash.rotation = Quaternion.Euler(0, 0, -40);
            yield return MoveHero(destination, home, attackDuration, 0.45f);
            Destroy(slash.gameObject);
            if (outcome == AttackOutcome.TowerCleared || outcome == AttackOutcome.StageCleared)
            {
                hint.text = "TOWER CLEARED";
                yield return CollapseAndAdvance(index);
            }
            RefreshHud();
            busy = false;
            if (Run.Complete) ShowResult();
        }

        private IEnumerator MoveHero(Vector3 a, Vector3 b, float duration, float arc)
        {
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                float t = Mathf.Clamp01(time / duration);
                hero.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0, 1, t)) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * arc);
                yield return null;
            }
            hero.position = b;
        }

        private IEnumerator CollapseAndAdvance(int index)
        {
            Transform collapsing = towers[index].root;
            Vector3 start = collapsing.position;
            for (float time = 0; time < 0.65f; time += Time.deltaTime)
            {
                float t = time / 0.65f;
                collapsing.position = start + new Vector3(0, -7 * t * t, 0);
                collapsing.rotation = Quaternion.Euler(0, 0, t * -17);
                collapsing.localScale = Vector3.one * (1 - t * 0.55f);
                yield return null;
            }
            collapsing.gameObject.SetActive(false);
            Destroy(collapsing.gameObject);
            if (Run.Complete) yield break;
            var starts = new List<Vector3>();
            var scales = new List<Vector3>();
            for (int i = Run.TowerIndex; i < towers.Count; i++)
            { starts.Add(towers[i].root.position); scales.Add(towers[i].root.localScale); }
            for (float time = 0; time < 0.55f; time += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, time / 0.55f);
                for (int i = Run.TowerIndex; i < towers.Count; i++)
                {
                    int offset = i - Run.TowerIndex;
                    Transform moving = towers[i].root;
                    moving.position = Vector3.Lerp(starts[offset], new Vector3(ActiveX + offset * 2.9f, FloorY, offset * 2.4f), t);
                    moving.localScale = Vector3.Lerp(scales[offset], Vector3.one * Mathf.Pow(0.83f, offset), t);
                }
                yield return null;
            }
            for (int i = Run.TowerIndex; i < towers.Count; i++)
            {
                int offset = i - Run.TowerIndex;
                towers[i].root.position = new Vector3(ActiveX + offset * 2.9f, FloorY, offset * 2.4f);
                towers[i].root.localScale = Vector3.one * Mathf.Pow(0.83f, offset);
            }
            TintTower(towers[Run.TowerIndex], 1);
        }

        public void RestartStage()
        {
            if (busy) return;
            StopAllCoroutines();
            Run = new StageRun(campaign.stages[stageIndex]);
            RebuildPresentation();
            SaveProgress();
        }

        public void Continue()
        {
            if (busy || (!Run.Complete && !Run.Failed)) return;
            if (Run.Complete) stageIndex = (stageIndex + 1) % campaign.stages.Length;
            RestartStage();
        }

        private void RefreshHud()
        {
            stageLabel.text = "STAGE " + (stageIndex + 1).ToString("00") + "  /  " + Run.Definition.title;
            powerLabel.text = Run.Power.ToString("N0");
            hint.text = "Defeat weaker foes. Take their power.";
            for (int i = 0; i < progress.Count; i++)
                progress[i].color = i < Run.TowerIndex ? Gold : i == Run.TowerIndex ? new Color(0.24f, 0.69f, 0.64f) : new Color(0.17f, 0.29f, 0.32f);
        }

        private void ShowResult()
        {
            bool complete = Run.Complete;
            resultPanel.gameObject.SetActive(true);
            resultTitle.text = complete ? "STAGE CLEARED" : "HERO DEFEATED";
            resultDetail.text = complete
                ? "Every tower has fallen.\nFinal power: " + Run.Power.ToString("N0")
                : "That enemy was too powerful.\nTry a different order of attacks.";
            continueLabel.text = complete ? (stageIndex == campaign.stages.Length - 1 ? "PLAY AGAIN" : "NEXT STAGE") : "TRY AGAIN";
        }

        private void SaveProgress()
        {
            if (preview || Run == null) return;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Run.Save(stageIndex)));
            PlayerPrefs.Save();
        }
        private void OnApplicationPause(bool paused) { if (paused) SaveProgress(); }

        private TMP_Text AddBadge(Transform target, Vector3 offset, Color color, Transform parent, long power)
        {
            var badge = Panel("Power_" + power, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-63, -30), new Vector2(63, 30), color);
            var outline = badge.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.8f, 0.89f, 0.82f, 0.8f);
            outline.effectDistance = new Vector2(2, -2);
            TMP_Text label = Text("Number", badge, power.ToString("N0"), 36, Color.white);
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 22;
            label.fontSizeMax = 36;
            badges.Add(new Badge { target = target, rect = badge, offset = offset });
            return label;
        }

        private static void TintTower(TowerView tower, float brightness)
        {
            foreach (Renderer renderer in tower.root.GetComponentsInChildren<Renderer>())
            {
                var block = new MaterialPropertyBlock();
                Color color = renderer.sharedMaterial.GetColor(BaseColor);
                block.SetColor(BaseColor, new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a));
                renderer.SetPropertyBlock(block);
            }
        }

        private static Material Mat(string name) => Resources.Load<Material>("Materials/" + name);

        private static Transform Model(string name, Transform parent, Vector3 position, float scale)
        {
            var source = Resources.Load<GameObject>("Models/" + name);
            if (source == null) throw new InvalidOperationException("Missing Blender export: Models/" + name);
            Transform obj = Instantiate(source, parent).transform;
            obj.name = name;
            obj.localPosition = position;
            obj.localScale = Vector3.one * scale;
            // Blender's exported forward direction faces away from our side-on camera.
            obj.localRotation = Quaternion.Euler(0, 180, 0);
            foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>())
            {
                var replacements = renderer.sharedMaterials;
                for (int i = 0; i < replacements.Length; i++)
                {
                    Material replacement = Mat(replacements[i].name.Replace(" (Instance)", ""));
                    if (replacement != null) replacements[i] = replacement;
                }
                renderer.sharedMaterials = replacements;
            }
            return obj;
        }

        private static Transform Block(string name, Transform parent, Vector3 position, Vector3 size, string material)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = Mat(material);
            Collider collider = obj.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            return obj.transform;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Vector2 lower, Vector2 upper, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = lower; rect.offsetMax = upper;
            var image = obj.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static TMP_Text Text(string name, Transform parent, string value, float size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TMP_Text text = obj.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value; text.fontSize = size; text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position; }

        private static UnityEngine.UI.Button Button(string name, Transform parent, string label, UnityEngine.Events.UnityAction callback)
        {
            var rect = Panel(name, parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Gold);
            rect.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            button.onClick.AddListener(callback);
            TMP_Text text = Text("Label", rect, label, 29, Ink);
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
