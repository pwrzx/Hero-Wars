using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TowerTrials
{
    public static class GameUi
    {
        public static readonly Color Ink = new Color32(13, 32, 41, 255);
        public static readonly Color Muted = new Color32(173, 196, 197, 255);
        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); Fit(rect, min, max); return rect;
        }
        public static void Fit(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        public static TMP_Text Label(string name, Transform parent, string text, float size, Color color,
            Vector2 min, Vector2 max, GameTextRole role = GameTextRole.Body)
        {
            var label = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = GameUiTheme.Font(role); label.text = text; label.fontSize = size;
            label.color = color; label.raycastTarget = false; label.alignment = TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }
        public static GameButton Button(string name, Transform parent, string text, UnityAction action, bool primary = false, float size = 34)
        {
            RectTransform root = Rect(name, parent, Vector2.zero, Vector2.one);
            GameUiShape Shape(string part, Color color, float inset)
            {
                RectTransform rect = Rect(part, root, Vector2.zero, Vector2.one);
                rect.offsetMin = Vector2.one * inset; rect.offsetMax = -Vector2.one * inset;
                var graphic = rect.gameObject.AddComponent<GameUiShape>();
                graphic.Configure(GameUiShape.Shape.Plate, color, 14);
                graphic.raycastTarget = false; return graphic;
            }
            GameUiShape rim = Shape("Rim", Color.white, 0);
            GameUiShape face = Shape("Face", Color.white, 2);
            face.raycastTarget = true;
            TMP_Text label = Label("Label", root, text, size, Color.white,
                new Vector2(0.045f, 0.08f), new Vector2(0.955f, 0.92f), GameTextRole.Action);
            var button = root.gameObject.AddComponent<GameButton>();
            button.Configure(primary, rim, face, null, label, label.rectTransform);
            button.onClick.AddListener(GameAudio.Click);
            button.onClick.AddListener(action);
            return button;
        }
        public static void FitSafeArea(RectTransform safe)
        {
            var root = (RectTransform)safe.parent;
            Vector2 screen = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            Rect area = Screen.safeArea;
            // A resize can report the previous orientation/device's insets for a frame.
            // Never turn those stale pixel bounds into anchors outside the current canvas.
            if (area.width <= 0 || area.height <= 0 || area.xMin < 0 || area.yMin < 0 ||
                area.xMax > screen.x + 0.5f || area.yMax > screen.y + 0.5f)
                area = new Rect(0, 0, screen.x, screen.y);
            Vector2 min = area.position / screen, max = area.max / screen;
            float width = Mathf.Min(max.x - min.x, 1120f / Mathf.Max(1, root.rect.width));
            float center = (min.x + max.x) * 0.5f;
            min.x = center - width * 0.5f; max.x = center + width * 0.5f;
            safe.anchorMin = min; safe.anchorMax = max;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }
        public static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = (float)Screen.width / Mathf.Max(1, Screen.height) > 0.65f ? 1 : 0.5f;
        }
        public static void Settings(RectTransform parent, UnityAction close, UnityAction help, UnityAction about)
        {
            Label("SettingsTitle", parent, "Settings", 64, GameUiTheme.Parchment,
                new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.98f), GameTextRole.Display);
            string[] keys = { "Sound", "Music", "ReducedMotion", "BatterySaver", "Haptics" };
            string[] names = { "Sound effects", "Music", "Reduce motion", "Battery saver", "Haptics" };
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i], caption = names[i];
                bool Value() => key == "Sound" ? GamePreferences.Sound : key == "Music" ? GamePreferences.Music :
                    key == "ReducedMotion" ? GamePreferences.ReducedMotion : key == "BatterySaver" ? GamePreferences.BatterySaver : GamePreferences.Haptics;
                GameButton button = null;
                button = Button(key + "Toggle", parent, caption + ": " + (Value() ? "On" : "Off"), () =>
                {
                    GamePreferences.Toggle(key);
                    button.GetComponentInChildren<TMP_Text>().text = caption + ": " + (Value() ? "On" : "Off");
                });
                Fit((RectTransform)button.transform, new Vector2(0.10f, 0.74f - i * 0.105f), new Vector2(0.90f, 0.84f - i * 0.105f));
            }
            Fit((RectTransform)Button("HowToPlay", parent, "How to play", help, false, 29).transform,
                new Vector2(0.10f, 0.19f), new Vector2(0.49f, 0.29f));
            Fit((RectTransform)Button("About", parent, "About & privacy", about, false, 29).transform,
                new Vector2(0.51f, 0.19f), new Vector2(0.90f, 0.29f));
            Fit((RectTransform)Button("CloseSettings", parent, "Done", close, true, 36).transform,
                new Vector2(0.16f, 0.055f), new Vector2(0.84f, 0.16f));
        }
        public const string HelpText = "Pick your battles.\n\nYour blue number is your power. Red numbers belong to enemies.\n\nSwipe right toward a room with a LOWER number. Defeat that enemy to add its power to yours.\n\nClear every room to bring the next tower forward. A higher or equal number defeats you. Retry any time.\n\nClear keeps to earn gold and gems. Unlock cosmetics in the Shop; equip them in Customize.";
        public static string AboutText => "HERO WARS  /  " + Application.version +
            "\n\nAn offline tower puzzle adventure.\nYour progress and settings stay on this device.\nNo accounts, ads, purchases, or analytics.\n\nHero art: OlyPoly\nWeapons: Game Lair Studio\nUI assets: 300Mind\nFonts: Alegreya (SIL Open Font License)\nMusic & combat sounds: original synthesis";
    }
}
