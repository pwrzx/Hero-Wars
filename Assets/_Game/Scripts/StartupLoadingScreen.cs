using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerTrials
{
    // Runs once at launch. Returning home loads HeroCenter directly.
    public sealed class StartupLoadingScreen : MonoBehaviour
    {
        public const string SceneName = "Startup";
        [SerializeField] private string destinationScene = "HeroCenter";
        [SerializeField, Min(0)] private float minimumDisplaySeconds = 1.6f;
        [SerializeField, Range(0, 1)] private float fadeSeconds = 0.35f;
        [SerializeField] private Texture2D background;
        [SerializeField] private Sprite crest;
        [SerializeField] private CanvasGroup curtain;
        [SerializeField] private RectTransform safeArea, progressFill;
        [SerializeField] private UnityEngine.UI.RawImage painting;
        [SerializeField] private UnityEngine.UI.CanvasScaler scaler;
        [SerializeField] private TMP_Text status, percentage, tip;
        [SerializeField] private GameButton retryButton;
        private AsyncOperation sceneLoad;
        private Coroutine loading;
        private Rect lastSafeArea;
        private Vector2 lastScreen;
        private float elapsed, shownProgress;
        private int shownPercent = -1, shownTip = -1;
        private bool fading;
        public float Progress => shownProgress;

        private static readonly string[] Tips =
        {
            "Attack a weaker enemy to add its power to yours.",
            "Clear every room to bring the next tower forward.",
            "Gold and gems unlock new looks for your hero."
        };

        public void Configure(Texture2D artwork, Sprite emblem)
        { background = artwork; crest = emblem; }

        private void Awake()
        {
            if (curtain == null) BuildPreview();
            // Runtime UnityEvent listeners from the editor preview aren't serialized.
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(GameAudio.Click);
            retryButton.onClick.AddListener(Retry);
            DontDestroyOnLoad(gameObject);
            UpdateLayout();
        }

        private void Start() => Retry();

        private void Retry()
        {
            if (loading != null) return;
            elapsed = shownProgress = 0;
            shownPercent = shownTip = -1;
            retryButton.gameObject.SetActive(false);
            curtain.alpha = 1;
            status.text = "Preparing your adventure...";
            SetProgress(0);
            loading = StartCoroutine(LoadHeroCenter());
        }

        private IEnumerator LoadHeroCenter()
        {
            // Render the splash before initializing audio and loading the menu.
            yield return null;
            GamePreferences.Apply();
            string failure = null;
            try
            {
                if (!Application.CanStreamedLevelBeLoaded(destinationScene))
                    throw new InvalidOperationException("Startup destination is missing from the build: " + destinationScene);
                sceneLoad = SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single);
                if (sceneLoad == null) throw new InvalidOperationException("Unable to load " + destinationScene);
                sceneLoad.allowSceneActivation = false;
            }
            catch (Exception exception) { failure = exception.Message; }
            if (failure != null)
            {
                Debug.LogError(failure, this);
                status.text = "Your adventure couldn't open.";
                tip.text = "Please try again.";
                retryButton.gameObject.SetActive(true);
                loading = null;
                yield break;
            }

            while (sceneLoad.progress < 0.9f || elapsed < minimumDisplaySeconds || shownProgress < 0.94f)
            {
                // Unity stops at 0.9 until activation. Reserve 5% for menu initialization.
                float target = Mathf.Clamp01(sceneLoad.progress / 0.9f) * 0.95f;
                SetProgress(Mathf.MoveTowards(shownProgress, target, Time.unscaledDeltaTime * 0.85f));
                if (sceneLoad.progress >= 0.9f) status.text = "Opening Hero Center...";
                yield return null;
            }
            sceneLoad.allowSceneActivation = true;
            while (!sceneLoad.isDone) yield return null;
            // HeroCenter builds its interface in Start and settles TMP meshes next frame.
            yield return null;
            yield return null;
            SetProgress(1);
            status.text = "Your adventure awaits";
            yield return null;
            fading = true;
            float duration = GamePreferences.ReducedMotion ? 0 : fadeSeconds;
            float fadeTime = 0;
            while (fadeTime < duration)
            {
                fadeTime += Time.unscaledDeltaTime;
                curtain.alpha = 1 - Mathf.Clamp01(fadeTime / duration);
                yield return null;
            }
            Destroy(gameObject);
        }

        private void Update()
        {
            UpdateLayout();
            if (loading == null || fading) return;
            elapsed += Time.unscaledDeltaTime;
            int index = Mathf.FloorToInt(elapsed / 4) % Tips.Length;
            if (index == shownTip) return;
            shownTip = index;
            tip.text = Tips[index];
        }

        private void UpdateLayout()
        {
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (screen == lastScreen && Screen.safeArea == lastSafeArea) return;
            lastScreen = screen; lastSafeArea = Screen.safeArea;
            GameUi.ConfigureScaler(scaler);
            Canvas.ForceUpdateCanvases();
            GameUi.FitSafeArea(safeArea);
            if (painting.texture == null) return;
            float imageAspect = (float)painting.texture.width / painting.texture.height;
            float aspect = Mathf.Max(1, screen.x) / Mathf.Max(1, screen.y);
            float width = Mathf.Min(1, aspect / imageAspect), height = Mathf.Min(1, imageAspect / aspect);
            painting.uvRect = new Rect((1 - width) * 0.5f, (1 - height) * 0.5f, width, height);
        }

        private void SetProgress(float value)
        {
            shownProgress = Mathf.Clamp01(value);
            progressFill.anchorMax = new Vector2(shownProgress, 1);
            int percent = Mathf.FloorToInt(shownProgress * 100);
            if (percent == shownPercent) return;
            shownPercent = percent;
            percentage.SetText("{0}%", percent);
        }

        private void OnDestroy()
        {
            // Unblock Unity's async queue if startup is interrupted externally.
            if (sceneLoad != null && !sceneLoad.isDone) sceneLoad.allowSceneActivation = true;
        }

        public void BuildPreview()
        {
            if (curtain != null) return;
            var root = new GameObject("StartupCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            GameUi.ConfigureScaler(scaler);
            curtain = root.GetComponent<CanvasGroup>();
            RectTransform rect = root.GetComponent<RectTransform>();
            Panel("Midnight", rect, Vector2.zero, Vector2.one, new Color32(10, 28, 37, 255));
            painting = GameUi.Rect("AncientChasm", rect, Vector2.zero, Vector2.one).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            painting.texture = background; painting.raycastTarget = false;
            painting.color = new Color(0.58f, 0.72f, 0.77f);
            var blocker = Panel("InputBlocker", rect, Vector2.zero, Vector2.one, new Color(0.02f, 0.045f, 0.06f, 0.36f));
            blocker.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            safeArea = GameUi.Rect("SafeArea", rect, Vector2.zero, Vector2.one);

            RectTransform emblem = GameUi.Rect("HeroCrest", safeArea, new Vector2(0.42f, 0.79f), new Vector2(0.58f, 0.88f));
            var icon = emblem.gameObject.AddComponent<UnityEngine.UI.Image>();
            icon.sprite = crest; icon.preserveAspect = true; icon.raycastTarget = false;
            Text("Series", "T O W E R   T R I A L S", 26, GameUiTheme.Parchment, 0.75f, 0.79f, GameTextRole.Action);
            Text("HeroTitle", "HERO", 154, GameUiTheme.Parchment, 0.64f, 0.755f, GameTextRole.Display);
            Text("WarsTitle", "WARS", 158, GameUiTheme.Brass, 0.535f, 0.65f, GameTextRole.Display);
            Text("Tagline", "A legend in the making.", 33, GameUiTheme.Parchment, 0.485f, 0.53f);
            RectTransform rule = Panel("TitleRule", safeArea, new Vector2(0.38f, 0.465f), new Vector2(0.62f, 0.465f), GameUiTheme.Brass);
            rule.sizeDelta = new Vector2(0, 2);

            status = Text("LoadingStatus", "Preparing your adventure...", 34, GameUiTheme.Parchment, 0.205f, 0.25f, GameTextRole.Action);
            var track = Panel("ProgressRim", safeArea, new Vector2(0.14f, 0.18f), new Vector2(0.86f, 0.18f), GameUiTheme.Brass);
            track.sizeDelta = new Vector2(0, 16);
            RectTransform well = Panel("ProgressTrack", track, Vector2.zero, Vector2.one, new Color32(10, 28, 37, 255));
            well.offsetMin = new Vector2(2, 2); well.offsetMax = new Vector2(-2, -2);
            progressFill = Panel("ProgressFill", well, Vector2.zero, new Vector2(0, 1), GameUiTheme.Brass);
            percentage = Text("Percentage", "0%", 27, GameUiTheme.Parchment, 0.135f, 0.17f, GameTextRole.Number);
            tip = Text("GameplayTip", Tips[0], 29, GameUi.Muted, 0.065f, 0.12f);
            tip.textWrappingMode = TextWrappingModes.Normal;
            Text("Version", "HERO WARS  /  " + Application.version, 22, GameUi.Muted, 0.016f, 0.05f, GameTextRole.Action);
            retryButton = GameUi.Button("RetryLoading", safeArea, "Try again", Retry, true, 32);
            GameUi.Fit((RectTransform)retryButton.transform, new Vector2(0.27f, 0.28f), new Vector2(0.73f, 0.34f));
            retryButton.gameObject.SetActive(false);
            lastScreen = new Vector2(-1, -1);
            UpdateLayout();
        }

        private TMP_Text Text(string name, string value, float size, Color color, float bottom, float top,
            GameTextRole role = GameTextRole.Body)
        {
            TMP_Text label = GameUi.Label(name, safeArea, value, size, color,
                new Vector2(0.09f, bottom), new Vector2(0.91f, top), role);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true; label.fontSizeMin = size * 0.65f; label.fontSizeMax = size;
            return label;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            RectTransform rect = GameUi.Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false;
            return rect;
        }
    }
}
