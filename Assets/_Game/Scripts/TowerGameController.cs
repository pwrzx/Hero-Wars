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
        private TMP_Text stageLabel, powerLabel, hint, resultTitle, resultDetail;
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
        private Rect lastSafeArea;
        private Vector2 lastScreen;
        public StageRun Run { get; private set; }
        public bool Busy => busy;
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
            AddBorder(header);
            var title = Text("GameTitle", header, "Tower Trials", 58, Paper, GameTextRole.Display);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -10), new Vector2(660, 100));
            stageLabel = Text("StageTitle", header, "", 30, Gold);
            Place(stageLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -104), new Vector2(920, 46));
            retryButton = Button("Retry", header, "Retry", RestartStage, false);
            Place(retryButton.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-24, -20), new Vector2(224, 104));
            homeButton = Button("HeroCenterReturn", header, "Home", () =>
            {
                if (!busy) HeroCenterController.ReturnToHeroCenter();
            }, false);
            ((GameButton)homeButton).SetRestartIcon(false);
            Place(homeButton.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-24, -130), new Vector2(224, 48));
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
            var card = Panel("ResultCard", resultPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
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
                if (reference != null) { if (Application.isPlaying) Destroy(reference); else DestroyImmediate(reference); }
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
                hint.text = "Enemy " + enemy.ToString("N0") + (Run.Power > enemy ? ": ready to strike" : ": too strong");
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
            combatAudio.Play(CombatSound.Swing);
            yield return MoveHero(home, destination, attackDuration, 0.85f);
            feedback.Landing(destination);
            yield return PoseHero(CombatPose.Windup, 0.075f);
            feedback.Slash(enemy.position);
            yield return PoseHero(CombatPose.Strike, 0.11f);
            AttackOutcome outcome = Run.Attack(index, room);
            SaveProgress(); // Save the resolved transaction, before cosmetic animations can be interrupted.
            feedback.Impact(enemy.position, outcome != AttackOutcome.Lost);
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
            hint.text = "+" + gained.ToString("N0") + " POWER";
            hint.color = Gold;
            yield return MoveHero(destination, home, attackDuration, 0.45f);
            feedback.Landing(home);
            combatAudio.Play(CombatSound.Power);
            yield return PoseHero(CombatPose.Recover, 0.06f);
            heroMotion.Release();
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
            RectTransform badge = powerLabel.transform.parent as RectTransform;
            for (float time = 0; time < 0.38f; time += Time.deltaTime)
            {
                float t = Mathf.Clamp01(time / 0.38f);
                powerLabel.text = (from + (long)((to - from) * (double)Mathf.SmoothStep(0, 1, t))).ToString("N0");
                badge.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * 0.18f);
                yield return null;
            }
            powerLabel.text = to.ToString("N0");
            badge.localScale = Vector3.one;
        }

        private IEnumerator FloatingPower(long amount, Vector3 position)
        {
            TMP_Text label = Text("PowerGain", canvasRect, "+" + amount.ToString("N0"), 54, Gold, GameTextRole.Number);
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
            hint.color = Paper;
            for (int i = 0; i < progress.Count; i++)
                progress[i].color = i < Run.TowerIndex ? Gold : i == Run.TowerIndex ? new Color(0.24f, 0.69f, 0.64f) : new Color(0.17f, 0.29f, 0.32f);
        }

        private void ShowResult()
        {
            bool complete = Run.Complete;
            resultPanel.gameObject.SetActive(true);
            resultTitle.text = complete ? "Stage cleared" : "Hero defeated";
            resultDetail.text = complete
                ? "Every tower has fallen.\nFinal power: " + Run.Power.ToString("N0")
                : "That enemy was too powerful.\nTry a different order of attacks.";
            continueLabel.text = complete ? (stageIndex == campaign.stages.Length - 1 ? "Play again" : "Next stage") : "Try again";
            ((GameButton)continueButton).SetRestartIcon(!complete || stageIndex == campaign.stages.Length - 1);
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

        private IEnumerator RevealResult()
        {
            Transform card = resultPanel.Find("ResultCard");
            for (float time = 0; time < 0.23f; time += Time.unscaledDeltaTime)
            {
                float t = 1 - Mathf.Pow(1 - Mathf.Clamp01(time / 0.23f), 3);
                card.localScale = Vector3.one * Mathf.Lerp(0.9f, 1, t);
                yield return null;
            }
            card.localScale = Vector3.one;
        }

        private void SetBusy(bool value)
        {
            busy = value;
            if (retryButton != null) retryButton.interactable = !value;
            if (homeButton != null) homeButton.interactable = !value;
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
                new Vector2(-68, -32), new Vector2(68, 32), Color.clear);
            UiShape("Rim", badge, GameUiShape.Shape.Plate, new Color(0.77f, 0.7f, 0.5f), 5);
            GameUiShape face = UiShape("Face", badge, GameUiShape.Shape.Plate, color, 4);
            face.rectTransform.offsetMin = Vector2.one * 2;
            face.rectTransform.offsetMax = -Vector2.one * 2;
            TMP_Text label = Text("Number", badge, power.ToString("N0"), 44, Color.white, GameTextRole.Number);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.margin = new Vector4(10, 0, 10, 0);
            label.fontSizeMin = 34;
            label.fontSizeMax = 44;
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
