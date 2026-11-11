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
        [SerializeField, Range(0, 1)] private float feedbackIntensity = 0.7f;
        [SerializeField, Range(0, 0.4f)] private float feedbackVolume = 0.16f;
        [SerializeField, Range(12, 50)] private float towerFallGravity = 28;
        [SerializeField, Range(15, 60)] private float characterFacingAngle = 38;
        private const string SaveKey = "TowerTrials.Run.v1";
        private const float FloorY = 0.7f, RoomHeight = 1.65f, ActiveX = 1.05f, HomeX = -2.55f;
        private static readonly Color Gold = GameUiTheme.Brass;
        private static readonly Color Ink = new Color(0.035f, 0.09f, 0.095f, 0.97f);
        private static readonly Color Paper = GameUiTheme.Parchment;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private Transform presentation, hero;
        private RectTransform canvasRect, safeArea, resultPanel;
        private RectTransform pausePanel, pauseCard;
        private RectTransform resultCard;
        private TMP_Text stageLabel, powerLabel, hint, resultTitle, resultDetail;
        private TMP_Text reviveDetail, reviveStatus;
        private GameButton reviveButton;
        private RewardedContinueTicket reviveTicket;
        private StageRun reviveRun;
        private string reviveMessage;
        private UnityEngine.UI.Button continueButton;
        private TMP_Text continueLabel;
        private CharacterMotion heroMotion;
        private CombatFeedback feedback;
        private CombatAudio combatAudio;
        private UnityEngine.UI.Button retryButton;
        private UnityEngine.UI.Button homeButton;
        private readonly List<TowerView> towers = new List<TowerView>();
        private readonly List<Badge> badges = new List<Badge>();
        private readonly List<UnityEngine.UI.Image> progress = new List<UnityEngine.UI.Image>();
        private readonly Dictionary<string, InputActionReference> inputReferences = new Dictionary<string, InputActionReference>();
        private int stageIndex;
        private bool busy, preview;
        private bool paused, rewardGranted;
        private Rect lastSafeArea;
        private Vector2 lastScreen;
        public StageRun Run { get; private set; }
        public bool Busy => busy;
        public bool Paused => paused;
        public int StageIndex => stageIndex;
        public Camera GameCamera => gameCamera;
        public CombatFeedback Feedback => feedback;

        private sealed class TowerView
        {
            public Transform root;
            public Transform[] rooms;
            public Transform[] enemies;
            public CharacterMotion[] motions;
            public GameObject[] markers;
            public TowerSegmentPhysics[] segments;
            public TowerSegmentPhysics crown;
        }
        private sealed class Badge
        {
            public Transform target;
            public RectTransform rect;
            public Vector3 offset;
            public bool hidden;
        }

        public void Configure(CampaignConfig config, Camera camera)
        { campaign = config; gameCamera = camera; }

        private void Start()
        {
            preview = false;
            GamePreferences.Apply();
            if (campaign == null || campaign.stages == null || campaign.stages.Length == 0)
                throw new InvalidOperationException("Assign a campaign to TowerGameController.");
            RunSave saved = GameSave.Read<RunSave>(SaveKey, value => value.version == 1);
            HeroProfile profile = HeroProfile.Load(HeroCenterCatalog.Load());
            if (PlayerPrefs.HasKey(CampaignConfig.SelectedStageKey))
            {
                int selected = Mathf.Clamp(PlayerPrefs.GetInt(CampaignConfig.SelectedStageKey), 0, profile.HighestUnlockedStage);
                PlayerPrefs.DeleteKey(CampaignConfig.SelectedStageKey);
                saved = new StageRun(campaign.stages[selected]).Save(selected);
            }
            else if (saved == null)
                saved = new StageRun(campaign.stages[Mathf.Min(profile.HighestUnlockedStage, campaign.stages.Length - 1)])
                    .Save(Mathf.Min(profile.HighestUnlockedStage, campaign.stages.Length - 1));
            stageIndex = saved == null ? 0 : Mathf.Clamp(saved.stageIndex, 0, campaign.stages.Length - 1);
            rewardGranted = false;
            if (saved == null || saved.stageIndex != stageIndex ||
                !StageRun.TryRestore(campaign.stages[stageIndex], saved, out var restored))
                Run = new StageRun(campaign.stages[stageIndex]);
            else Run = restored;
            SaveProgress();
            RebuildPresentation();
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

        private void Update()
        {
            UpdateRewardedContinue();
            if (!preview && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (Run != null && (Run.Complete || Run.Failed)) HeroCenterController.ReturnToHeroCenter();
                else SetPaused(!paused);
            }
        }

        private void UpdateLayout()
        {
            Vector2 dimensions = new Vector2(Screen.width, Screen.height);
            if (dimensions != lastScreen || Screen.safeArea != lastSafeArea)
            {
                lastScreen = dimensions;
                lastSafeArea = Screen.safeArea;
                GameUi.ConfigureScaler(safeArea.GetComponentInParent<UnityEngine.UI.CanvasScaler>());
                Canvas.ForceUpdateCanvases();
                GameUi.FitSafeArea(safeArea);
                gameCamera.orthographicSize = Mathf.Max(6.4f, 3.9f / gameCamera.aspect);
                Canvas.ForceUpdateCanvases();
            }
            foreach (Badge badge in badges)
            {
                if (badge.rect == null) continue;
                bool visible = !badge.hidden && badge.target != null && badge.target.gameObject.activeInHierarchy;
                badge.rect.gameObject.SetActive(visible);
                if (!visible) continue;
                Vector3 position = badge.target.TransformPoint(badge.offset);
                if (badge.target == hero && busy) position += Vector3.up * 0.4f;
                Vector3 screen = gameCamera.WorldToScreenPoint(position);
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
            SetBusy(false);
            RefreshHud();
            UpdateLayout();
            if (Run.Failed || Run.Complete) ShowResult();
        }

        private void BuildWorld()
        {
            // Original Blender exports are the single source of geometry for both characters and rooms.
            var backdrop = new GameObject("PaintedBackdrop").AddComponent<GameBackdrop>();
            backdrop.transform.SetParent(presentation, false);
            backdrop.Configure(gameCamera);
            if (!preview)
            {
                feedback = presentation.gameObject.AddComponent<CombatFeedback>();
                feedback.Configure(gameCamera, feedbackIntensity);
                combatAudio = presentation.gameObject.AddComponent<CombatAudio>();
                combatAudio.Configure(feedbackVolume);
            }
            var home = new GameObject("HeroTower").transform;
            home.SetParent(presentation, false);
            home.position = new Vector3(HomeX, FloorY, 0);
            Model("TowerRoom", home, Vector3.zero, 1);
            // An open hero platform keeps the knight and power badge unobstructed.
            Block("Hero foundation", home, new Vector3(0, -2, 0.2f), new Vector3(2.3f, 3.9f, 1.3f), "Stone_DeepTeal");
            hero = Model("Knight", presentation, new Vector3(HomeX, FloorY + 0.12f, -0.12f), 0.78f);
            hero.localRotation = Quaternion.Euler(0, 180 - characterFacingAngle, 0);
            heroMotion = hero.GetComponent<CharacterMotion>();
            if (!preview)
            {
                HeroCenterCatalog catalog = HeroCenterCatalog.Load();
                if (catalog != null) heroMotion.UseHeroLoadout(catalog, HeroProfile.Load(catalog));
            }

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
                view.motions = new CharacterMotion[count];
                view.markers = new GameObject[count];
                view.segments = new TowerSegmentPhysics[count];
                int occupiedFloor = 0;
                for (int r = 0; r < count; r++)
                {
                    // Room indices remain stable for combat/saves; only their physical height compacts.
                    bool defeated = Run.IsDefeated(t, r);
                    float roomY = occupiedFloor * RoomHeight;
                    view.rooms[r] = Model("TowerRoom", view.root, new Vector3(0, roomY, 0), 1);
                    view.rooms[r].name = "Room_" + r;
                    view.rooms[r].gameObject.SetActive(!defeated);
                    var marker = Block("Target rim", view.root, new Vector3(0, roomY + 0.13f, -0.76f),
                        new Vector3(2.1f, 0.045f, 0.06f), "Eye_Amber");
                    marker.SetParent(view.rooms[r], true);
                    marker.gameObject.SetActive(false);
                    view.markers[r] = marker.gameObject;
                    if (defeated) continue;
                    Transform enemy = Model("Goblin", view.root, new Vector3(0.12f, roomY + 0.12f, -0.12f), 0.86f);
                    enemy.localRotation = Quaternion.Euler(0, 180 + characterFacingAngle, 0);
                    enemy.SetParent(view.rooms[r], true);
                    view.enemies[r] = enemy;
                    view.motions[r] = enemy.GetComponent<CharacterMotion>();
                    if (!preview)
                    {
                        view.segments[r] = view.rooms[r].gameObject.AddComponent<TowerSegmentPhysics>();
                        view.segments[r].Configure(RoomHeight, false, towerFallGravity, feedback, combatAudio);
                    }
                    occupiedFloor++;
                }
                Transform crown = Model("TowerCrown", view.root, new Vector3(0, occupiedFloor * RoomHeight, 0), 1);
                if (!preview)
                {
                    view.crown = crown.gameObject.AddComponent<TowerSegmentPhysics>();
                    view.crown.Configure(RoomHeight, true, towerFallGravity, feedback, combatAudio);
                    // A permanent support remains when the bottom occupied room is removed.
                    var support = view.root.gameObject.AddComponent<BoxCollider>();
                    support.center = new Vector3(0, -0.2f, 0);
                    support.size = new Vector3(2.46f, 0.2f, 1.46f);
                    support.contactOffset = 0.002f;
                }
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
            GameUi.ConfigureScaler(scaler);
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
                new Vector2(32, -296), new Vector2(-32, -28), Ink);
            AddBorder(header);
            var title = Text("GameTitle", header, "Hero Wars", 54, Paper, GameTextRole.Display);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -12), new Vector2(430, 144));
            stageLabel = Text("StageTitle", header, "", 30, Gold);
            Place(stageLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -176), new Vector2(920, 60));
            stageLabel.enableAutoSizing = true; stageLabel.fontSizeMin = 25; stageLabel.fontSizeMax = 30;
            retryButton = Button("Pause", header, "Pause", () => SetPaused(true), false);
            ((GameButton)retryButton).SetPauseIcon();
            Place(retryButton.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-24, -12), new Vector2(224, 144));
            homeButton = Button("HeroCenterReturn", header, "Home", () =>
            {
                if (!busy) HeroCenterController.ReturnToHeroCenter();
            }, false);
            ((GameButton)homeButton).SetRestartIcon(false);
            Place(homeButton.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-264, -12), new Vector2(224, 144));
            int towerCount = Run.Definition.towers.Length;
            for (int t = 0; t < towerCount; t++)
            {
                var bar = Panel("TowerProgress_" + t, header, new Vector2((float)t / towerCount, 0),
                    new Vector2((float)(t + 1) / towerCount, 0), new Vector2(16, 9),
                    new Vector2(-16, 17), new Color(0.17f, 0.29f, 0.32f));
                progress.Add(bar.GetComponent<UnityEngine.UI.Image>());
            }

            var footer = Panel("Footer", safeArea, Vector2.zero, new Vector2(1, 0),
                new Vector2(32, 28), new Vector2(-32, 222), Ink);
            AddBorder(footer);
            var gesture = Text("Gesture", footer, "Swipe toward a room", 40, Paper, GameTextRole.Action);
            Place(gesture.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(940, 55));
            gesture.alignment = TextAlignmentOptions.Center;
            hint = Text("Hint", footer, "", 32, Paper);
            Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(940, 65));
            hint.alignment = TextAlignmentOptions.Center;

            resultPanel = Panel("ResultOverlay", safeArea, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0.01f, 0.035f, 0.05f, 0.87f));
            resultPanel.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var card = resultCard = Panel("ResultCard", resultPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-450, -280), new Vector2(450, 280), Ink);
            AddBorder(card);
            resultTitle = Text("ResultTitle", card, "", 64, Gold, GameTextRole.Display);
            Place(resultTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(820, 90));
            resultTitle.alignment = TextAlignmentOptions.Center;
            resultDetail = Text("ResultDetail", card, "", 34, Paper);
            Place(resultDetail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(780, 135));
            resultDetail.alignment = TextAlignmentOptions.Center;
            continueButton = Button("Continue", card, "", Continue, true);
            continueLabel = continueButton.GetComponentInChildren<TMP_Text>();
            Place(continueButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(640, 112));
            reviveDetail = Text("ReviveBenefit", card, "Keep your power and cleared rooms.", 34, Paper);
            reviveDetail.alignment = TextAlignmentOptions.Center;
            Place(reviveDetail.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, 100), new Vector2(780, 84));
            reviveButton = GameUi.Button("WatchAdContinue", card, "Watch ad & continue", RequestRewardedContinue, true, 38);
            Place((RectTransform)reviveButton.transform, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, -18), new Vector2(740, 144));
            reviveStatus = Text("ReviveStatus", card, "", 29, GameUi.Muted);
            reviveStatus.alignment = TextAlignmentOptions.Center;
            Place(reviveStatus.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, -128), new Vector2(780, 62));
            reviveDetail.gameObject.SetActive(false);
            reviveButton.gameObject.SetActive(false);
            reviveStatus.gameObject.SetActive(false);
            resultPanel.gameObject.SetActive(false);
            pausePanel = Panel("PauseOverlay", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.015f, 0.03f, 0.045f, 0.94f));
            pausePanel.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            pauseCard = Panel("PauseCard", pausePanel, new Vector2(0.06f, 0.15f), new Vector2(0.94f, 0.85f), Vector2.zero, Vector2.zero, Ink);
            AddBorder(pauseCard);
            pausePanel.gameObject.SetActive(false);

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
            CancelRewardedContinue();
            if (paused) Time.timeScale = 1;
            foreach (InputActionReference reference in inputReferences.Values)
                if (reference != null) { if (Application.isPlaying) Destroy(reference); else DestroyImmediate(reference); }
            inputReferences.Clear();
        }

        public void PreviewSwipe(Vector2 start, Vector2 end)
        {
            if (paused || busy || Run == null || Run.Failed || Run.Complete) return;
            SelectRoom(FindRoom(end));
        }

        public bool SubmitSwipe(Vector2 start, Vector2 end)
        {
            if (paused || busy || Run == null || Run.Failed || Run.Complete) return false;
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
            SetBusy(true); // Lock before the first animation frame: a second finger cannot award power twice.
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
            if (room >= 0)
            {
                long enemy = Run.Definition.towers[Run.TowerIndex].enemies[room];
                hint.text = "Enemy " + GameUiTheme.Number(enemy) + (Run.Power > enemy ? ": ready to strike" : ": too strong");
                hint.color = Run.Power > enemy ? new Color(0.55f, 1, 0.76f) : new Color(1, 0.55f, 0.45f);
            }
        }

        private IEnumerator Attack(int room)
        {
            int index = Run.TowerIndex;
            TowerView tower = towers[index];
            Transform enemy = tower.enemies[room];
            CharacterMotion enemyMotion = tower.motions[room];
            long gained = Run.Definition.towers[index].enemies[room];
            long previousPower = Run.Power;
            Vector3 home = hero.position;
            Vector3 destination = enemy.position + new Vector3(-0.63f, 0.08f, -0.25f);
            hint.text = "";
            yield return PoseHero(CombatPose.Anticipate, 0.055f);
            yield return MoveHero(home, destination, attackDuration, 0.85f);
            feedback.Landing(destination);
            yield return PoseHero(CombatPose.Windup, 0.075f);
            combatAudio.Play(CombatSound.Swing);
            feedback.Slash(enemy.position);
            yield return PoseHero(CombatPose.Strike, 0.11f);
            AttackOutcome outcome = Run.Attack(index, room);
            SaveProgress(); // Save the resolved transaction, before cosmetic animations can be interrupted.
            feedback.Impact(enemy.position, outcome != AttackOutcome.Lost);
            GamePreferences.Impact();
            if (outcome == AttackOutcome.Lost)
            {
                combatAudio.Play(CombatSound.Failure);
                enemyMotion.Pose(CombatPose.Guard, 1);
                for (float time = 0; time < 0.18f; time += Time.deltaTime)
                { heroMotion.Pose(CombatPose.Recoil, time / 0.18f); yield return null; }
                enemyMotion.Release();
                hint.text = "Too strong. Find a different order.";
                yield return MoveHero(destination, home, attackDuration, 0.3f);
                heroMotion.Release();
                SetBusy(false);
                ShowResult();
                yield break;
            }
            combatAudio.Play(CombatSound.Hit);
            foreach (Badge badge in badges) if (badge.target == enemy) badge.hidden = true;
            enemyMotion.HitFlash(0.85f);
            heroMotion.Pose(CombatPose.Recover, 0);
            // A brief local impact hold leaves the game clock and UI responsive.
            yield return new WaitForSecondsRealtime(0.035f);
            StartCoroutine(CountPower(previousPower, Run.Power));
            StartCoroutine(FloatingPower(gained, enemy.position + Vector3.up));
            feedback.PowerTransfer(enemy.position, hero);
            Vector3 roomScale = tower.rooms[room].localScale;
            for (float time = 0; time < 0.14f; time += Time.deltaTime)
            {
                float t = time / 0.14f;
                enemyMotion.Pose(CombatPose.Defeat, t);
                enemyMotion.HitFlash((1 - t) * 0.85f);
                heroMotion.Pose(CombatPose.Recover, t);
                tower.rooms[room].localScale = Vector3.Scale(roomScale, new Vector3(1 - t * 0.12f, 1 - t * 0.9f, 1 - t * 0.12f));
                yield return null;
            }
            enemy.gameObject.SetActive(false);
            tower.rooms[room].gameObject.SetActive(false);
            Coroutine settling = StartCoroutine(SettleTower(index));
            hint.text = "+" + GameUiTheme.Number(gained) + " POWER";
            hint.color = Gold;
            yield return MoveHero(destination, home, attackDuration, 0.45f);
            feedback.Landing(home);
            combatAudio.Play(CombatSound.Power);
            heroMotion.Release();
            heroMotion.ReactToLanding(1);
            // Keep input locked until the crown and every surviving room have landed.
            yield return settling;
            if (outcome == AttackOutcome.TowerCleared || outcome == AttackOutcome.StageCleared)
            {
                hint.text = "TOWER CLEARED";
                yield return CollapseAndAdvance(index);
            }
            RefreshHud();
            SetBusy(false);
            if (Run.Complete) ShowResult();
        }

        private IEnumerator SettleTower(int index)
        {
            TowerView tower = towers[index];
            int floor = 0;
            for (int r = 0; r < tower.rooms.Length; r++)
            {
                if (Run.IsDefeated(index, r)) continue;
                tower.segments[r].DropTo(floor++ * RoomHeight);
            }
            tower.crown.DropTo(floor * RoomHeight);
            bool falling;
            do
            {
                yield return null;
                falling = tower.crown.IsSettling;
                foreach (TowerSegmentPhysics segment in tower.segments)
                    if (segment != null && segment.gameObject.activeInHierarchy) falling |= segment.IsSettling;
            } while (falling);
        }

        private IEnumerator MoveHero(Vector3 a, Vector3 b, float duration, float arc)
        {
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                float t = Mathf.Clamp01(time / duration);
                hero.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0, 1, t)) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * arc);
                heroMotion.Pose(CombatPose.Flight, t);
                yield return null;
            }
            hero.position = b;
        }

        private IEnumerator PoseHero(CombatPose pose, float duration)
        {
            for (float time = 0; time < duration; time += Time.deltaTime)
            { heroMotion.Pose(pose, time / duration); yield return null; }
            heroMotion.Pose(pose, 1);
        }

        private IEnumerator CountPower(long from, long to)
        {
            SizeHeroBadge(to);
            RectTransform badge = powerLabel.transform.parent as RectTransform;
            for (float time = 0; time < 0.38f; time += Time.deltaTime)
            {
                float t = Mathf.Clamp01(time / 0.38f);
                powerLabel.text = GameUiTheme.Number(from + (long)((to - from) * (double)Mathf.SmoothStep(0, 1, t)));
                badge.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * 0.18f);
                yield return null;
            }
            powerLabel.text = GameUiTheme.Number(to);
            badge.localScale = Vector3.one;
        }

        private IEnumerator FloatingPower(long amount, Vector3 position)
        {
            TMP_Text label = Text("PowerGain", canvasRect, "+" + GameUiTheme.Number(amount), 54, Gold, GameTextRole.Number);
            label.alignment = TextAlignmentOptions.Center;
            var shadow = label.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = Ink;
            shadow.effectDistance = new Vector2(2, -3);
            Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330, 100));
            Vector3 screen = gameCamera.WorldToScreenPoint(position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 start);
            for (float time = 0; time < 0.65f; time += Time.deltaTime)
            {
                float t = time / 0.65f;
                label.rectTransform.anchoredPosition = start + Vector2.up * (40 + t * 130);
                label.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.2f, 1, Mathf.Clamp01(t * 4));
                label.color = new Color(Gold.r, Gold.g, Gold.b, Mathf.Clamp01((1 - t) * 3));
                yield return null;
            }
            Destroy(label.gameObject);
        }

        private IEnumerator CollapseAndAdvance(int index)
        {
            Transform collapsing = towers[index].root;
            // The cleared structure is now an outgoing visual; remove its collision support.
            foreach (Collider collider in collapsing.GetComponentsInChildren<Collider>()) collider.enabled = false;
            feedback.Collapse(collapsing.position);
            combatAudio.Play(CombatSound.Collapse);
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
            feedback.Landing(towers[Run.TowerIndex].root.position);
        }

        public void RestartStage()
        {
            if (busy) return;
            CancelRewardedContinue();
            SetPaused(false);
            StopAllCoroutines();
            rewardGranted = false;
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
            powerLabel.text = GameUiTheme.Number(Run.Power);
            SizeHeroBadge(Run.Power);
            hint.text = "Your power: " + GameUiTheme.Number(Run.Power) + ". Choose a lower red number.";
            hint.color = Paper;
            for (int i = 0; i < progress.Count; i++)
                progress[i].color = i < Run.TowerIndex ? Gold : i == Run.TowerIndex ? new Color(0.24f, 0.69f, 0.64f) : new Color(0.17f, 0.29f, 0.32f);
        }

        private void ShowResult()
        {
            bool complete = Run.Complete;
            resultCard.sizeDelta = new Vector2(900, complete ? 560 : 820);
            Place(resultDetail.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f,
                new Vector2(0, complete ? 10 : 200), new Vector2(780, complete ? 135 : 100));
            Place((RectTransform)continueButton.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 56), new Vector2(640, complete ? 112 : 144));
            reviveDetail.gameObject.SetActive(!complete);
            reviveButton.gameObject.SetActive(!complete);
            reviveStatus.gameObject.SetActive(!complete);
            resultPanel.gameObject.SetActive(true);
            resultTitle.text = complete ? "Stage cleared" : "Hero defeated";
            resultDetail.text = complete
                ? (rewardGranted ? "+" + CampaignConfig.GoldReward(stageIndex) + " gold   /   +" + CampaignConfig.GemReward(stageIndex) + " gems\n" : "First-clear reward already saved.\n") +
                    (stageIndex == campaign.stages.Length - 1 ? "Journey complete. Every keep is yours!" : "Next keep unlocked. Final power: " + GameUiTheme.Number(Run.Power))
                : "That enemy was too powerful.\nChoose how to continue.";
            continueLabel.text = complete ? (stageIndex == campaign.stages.Length - 1 ? "Play again" : "Next stage") : "Retry";
            ((GameButton)continueButton).SetPrimary(complete);
            ((GameButton)continueButton).SetRestartIcon(!complete || stageIndex == campaign.stages.Length - 1);
            reviveMessage = null;
            RefreshRewardedContinue();
            if (!preview)
            {
                StartCoroutine(RevealResult());
                if (complete)
                {
                    feedback.Victory(hero.position);
                    combatAudio.Play(CombatSound.Power);
                    StartCoroutine(PoseHero(CombatPose.Celebrate, 0.55f));
                }
            }
        }

        public void RequestRewardedContinue()
        {
            if (preview || busy || Run == null || !Run.Failed || reviveTicket != null) return;
            if (RewardedContinueAds.TryShow(out var ticket))
            {
                reviveTicket = ticket;
                reviveRun = Run;
                reviveMessage = null;
            }
            else reviveMessage = "No ad available right now. You can retry.";
            RefreshRewardedContinue();
        }

        private void UpdateRewardedContinue()
        {
            if (reviveTicket != null && reviveTicket.Result != RewardedContinueResult.Pending)
            {
                RewardedContinueResult result = reviveTicket.Result;
                StageRun expectedRun = reviveRun;
                reviveTicket = null; reviveRun = null;
                if (Run == expectedRun && Run.Failed)
                {
                    if (result == RewardedContinueResult.Rewarded && Run.TryRevive())
                    {
                        StopAllCoroutines();
                        SetPaused(false);
                        SaveProgress();
                        RebuildPresentation();
                        hint.text = "Back in the battle. Choose a weaker enemy.";
                    }
                    else reviveMessage = result == RewardedContinueResult.Skipped
                        ? "Ad not finished. Try another ad or retry."
                        : "Ad unavailable. You can try again or retry.";
                }
            }
            if (Run != null && Run.Failed && resultPanel != null && resultPanel.gameObject.activeSelf)
                RefreshRewardedContinue();
        }

        private void RefreshRewardedContinue()
        {
            if (reviveButton == null || Run == null || !Run.Failed) return;
            bool waiting = reviveTicket != null;
            bool available = !preview && !waiting && RewardedContinueAds.IsReady;
            reviveButton.interactable = available;
            var retry = (GameButton)continueButton;
            if (retry.IsPrimary == available) retry.SetPrimary(!available);
            reviveStatus.text = waiting ? "Waiting for the ad to finish..." : reviveMessage ??
                (available ? "Finish the ad to continue your run." : "No ad available right now. You can retry.");
        }

        private void CancelRewardedContinue()
        {
            reviveTicket?.Cancel();
            reviveTicket = null; reviveRun = null; reviveMessage = null;
        }

        private IEnumerator RevealResult()
        {
            Transform card = resultPanel.Find("ResultCard");
            for (float time = 0; time < 0.23f; time += Time.unscaledDeltaTime)
            {
                float t = 1 - Mathf.Pow(1 - Mathf.Clamp01(time / 0.23f), 3);
                card.localScale = GamePreferences.ReducedMotion ? Vector3.one : Vector3.one * Mathf.Lerp(0.9f, 1, t);
                yield return null;
            }
            card.localScale = Vector3.one;
        }

        private void SetBusy(bool value)
        {
            busy = value;
            if (retryButton != null) retryButton.interactable = true;
            if (homeButton != null) homeButton.interactable = !value;
        }

        private void SaveProgress()
        {
            if (preview || Run == null) return;
            GameSave.Write(SaveKey, Run.Save(stageIndex));
            if (Run.Complete)
            {
                HeroProfile profile = HeroProfile.Load(HeroCenterCatalog.Load());
                if (profile.ClaimStageReward(stageIndex)) { profile.Save(); rewardGranted = true; }
            }
        }
        private void OnApplicationPause(bool background)
        {
            if (!background || preview || Run == null) return;
            SaveProgress();
            if (!Run.Complete && !Run.Failed) SetPaused(true);
        }
        private void OnApplicationQuit() => SaveProgress();

        public void SetPaused(bool value)
        {
            if (pausePanel == null) return;
            paused = value; Time.timeScale = value ? 0 : 1;
            pausePanel.gameObject.SetActive(value);
            if (value) BuildPauseMenu();
        }

        private void ClearPauseCard()
        {
            for (int i = pauseCard.childCount - 1; i >= 0; i--)
            { GameObject child = pauseCard.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
        }

        private void BuildPauseMenu()
        {
            ClearPauseCard();
            GameUi.Label("PauseTitle", pauseCard, "Take a breath", 64, Paper, new Vector2(0.06f, 0.81f), new Vector2(0.94f, 0.96f), GameTextRole.Display);
            GameUi.Label("PauseStage", pauseCard, CampaignConfig.Chapter(stageIndex) + " / Keep " + (stageIndex + 1), 32, GameUi.Muted,
                new Vector2(0.06f, 0.73f), new Vector2(0.94f, 0.82f));
            PauseAction("Resume", "Resume", () => SetPaused(false), 0.58f, true);
            PauseAction("RestartFromPause", "Restart keep", () => { busy = false; RestartStage(); }, 0.44f);
            PauseAction("GameplaySettings", "Settings", BuildGameplaySettings, 0.30f);
            PauseAction("PauseHome", "Hero Center", () => { SaveProgress(); SetPaused(false); HeroCenterController.ReturnToHeroCenter(); }, 0.16f);
        }

        private void PauseAction(string name, string caption, UnityEngine.Events.UnityAction action, float bottom, bool primary = false)
        {
            var button = GameUi.Button(name, pauseCard, caption, action, primary, 36);
            GameUi.Fit((RectTransform)button.transform, new Vector2(0.12f, bottom), new Vector2(0.88f, bottom + 0.11f));
        }

        private void BuildGameplaySettings()
        {
            ClearPauseCard();
            GameUi.Settings(pauseCard, BuildPauseMenu, () => GameplayInformation("How to play", GameUi.HelpText),
                () => GameplayInformation("About & privacy", GameUi.AboutText));
        }

        private void GameplayInformation(string title, string copy)
        {
            ClearPauseCard();
            GameUi.Label("InformationTitle", pauseCard, title, 60, Paper, new Vector2(0.06f, 0.85f), new Vector2(0.94f, 0.97f), GameTextRole.Display);
            GameUi.Label("InformationText", pauseCard, copy, 34, GameUi.Muted, new Vector2(0.09f, 0.23f), new Vector2(0.91f, 0.84f));
            PauseAction("InformationBack", "Back", BuildGameplaySettings, 0.06f, true);
        }

        private TMP_Text AddBadge(Transform target, Vector3 offset, Color color, Transform parent, long power)
        {
            float width = GameUiTheme.PowerBadgeWidth(power);
            var badge = Panel("Power_" + power, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-width * 0.5f, -32), new Vector2(width * 0.5f, 32), Color.clear);
            UiShape("Rim", badge, GameUiShape.Shape.Plate, new Color(0.77f, 0.7f, 0.5f), 5);
            GameUiShape face = UiShape("Face", badge, GameUiShape.Shape.Plate, color, 4);
            face.rectTransform.offsetMin = Vector2.one * 2;
            face.rectTransform.offsetMax = -Vector2.one * 2;
            TMP_Text label = Text("Number", badge, GameUiTheme.Number(power), 44, Color.white, GameTextRole.Number);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.margin = new Vector4(10, 0, 10, 0);
            label.fontSizeMin = 20;
            label.fontSizeMax = 44;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            badges.Add(new Badge { target = target, rect = badge, offset = offset });
            return label;
        }

        private void SizeHeroBadge(long power)
        {
            if (powerLabel != null)
                ((RectTransform)powerLabel.transform.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, GameUiTheme.PowerBadgeWidth(power));
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
            if (name == "Knight" || name == "Goblin") obj.gameObject.AddComponent<CharacterMotion>().Configure(name == "Knight");
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

        private static TMP_Text Text(string name, Transform parent, string value, float size, Color color,
            GameTextRole role = GameTextRole.Body)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TMP_Text text = obj.GetComponent<TextMeshProUGUI>();
            text.font = GameUiTheme.Font(role);
            text.fontStyle = FontStyles.Normal;
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

        private static UnityEngine.UI.Button Button(string name, Transform parent, string label,
            UnityEngine.Events.UnityAction callback, bool primary)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            GameUiShape rim = rect.gameObject.AddComponent<GameUiShape>();
            rim.Configure(GameUiShape.Shape.Plate, Color.white);
            rim.raycastTarget = true;
            GameUiShape face = UiShape("Face", rect, GameUiShape.Shape.Plate, Color.white, 6);
            face.rectTransform.offsetMin = Vector2.one * 2;
            face.rectTransform.offsetMax = -Vector2.one * 2;
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(rect, false);
            Stretch(content);
            GameUiShape icon = UiShape("ActionIcon", content, primary ? GameUiShape.Shape.Forward : GameUiShape.Shape.Restart, Color.white);
            TMP_Text text = Text("Label", content, label, 36, Color.white, GameTextRole.Action);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            // Centre the icon and label as a single group, independent of the button width.
            float labelWidth = primary ? 240 : 100;
            Place(text.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(24, 1), new Vector2(labelWidth, 66));
            Place(icon.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f,
                new Vector2(-labelWidth * 0.5f - 8, 0), new Vector2(40, 40));
            var button = rect.gameObject.AddComponent<GameButton>();
            button.onClick.AddListener(GameAudio.Click);
            button.onClick.AddListener(callback);
            button.Configure(primary, rim, face, icon, text, content);
            return button;
        }

        private static GameUiShape UiShape(string name, Transform parent, GameUiShape.Shape shape, Color color, float corner = 8)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(GameUiShape));
            obj.transform.SetParent(parent, false);
            var graphic = obj.GetComponent<GameUiShape>();
            graphic.Configure(shape, color, corner);
            graphic.raycastTarget = false;
            Stretch(graphic.rectTransform);
            return graphic;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one * 0.5f;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void AddBorder(RectTransform rect)
        {
            Color border = new Color(Gold.r, Gold.g, Gold.b, 0.38f);
            Panel("BorderTop", rect, new Vector2(0, 1), Vector2.one, new Vector2(0, -1.5f), Vector2.zero, border);
            Panel("BorderBottom", rect, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1.5f), border);
            Panel("BorderLeft", rect, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(1.5f, 0), border);
            Panel("BorderRight", rect, new Vector2(1, 0), Vector2.one, new Vector2(-1.5f, 0), Vector2.zero, border);
        }
    }
}
