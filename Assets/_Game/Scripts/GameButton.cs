using TMPro;
using UnityEngine;

namespace TowerTrials
{
    public sealed class GameButton : UnityEngine.UI.Button
    {
        [SerializeField] private GameUiShape rim, icon;
        [SerializeField] private TMP_Text label;
        [SerializeField] private RectTransform content;
        [SerializeField] private bool primary;
        private Vector2 contentTarget;
        public bool IsPrimary => primary;

        public void Configure(bool isPrimary, GameUiShape border, GameUiShape face,
            GameUiShape actionIcon, TMP_Text actionLabel, RectTransform contents)
        {
            primary = isPrimary;
            rim = border;
            icon = actionIcon;
            label = actionLabel;
            content = contents;
            targetGraphic = face;
            transition = Transition.ColorTint;
            var palette = colors;
            palette.normalColor = isPrimary ? Hex(196, 163, 103) : Hex(17, 46, 48);
            palette.highlightedColor = isPrimary ? Hex(220, 189, 131) : Hex(31, 70, 69);
            palette.selectedColor = palette.highlightedColor;
            palette.pressedColor = isPrimary ? Hex(166, 131, 75) : Hex(10, 33, 36);
            palette.disabledColor = Hex(24, 48, 51);
            palette.colorMultiplier = 1;
            palette.fadeDuration = 0.1f;
            colors = palette;
            FitContents();
            DoStateTransition(currentSelectionState, true);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (rim == null || label == null) return;
            bool disabled = state == SelectionState.Disabled;
            bool focus = state == SelectionState.Selected;
            bool highlight = state == SelectionState.Highlighted;
            Color edge = disabled ? Hex(62, 82, 80) : focus ? Hex(255, 239, 192) :
                highlight ? Hex(231, 204, 148) : primary ? Hex(239, 211, 159) : Hex(146, 126, 81);
            Color ink = disabled ? Hex(149, 173, 168) : primary ? Hex(19, 39, 39) : GameUiTheme.Parchment;
            rim.CrossFadeColor(edge, instant ? 0 : 0.1f, true, true);
            label.CrossFadeColor(ink, instant ? 0 : 0.1f, true, true);
            if (icon != null) icon.CrossFadeColor(ink, instant ? 0 : 0.1f, true, true);
            contentTarget = state == SelectionState.Pressed ? new Vector2(0, -3) : Vector2.zero;
            if (instant && content != null) content.anchoredPosition = contentTarget;
        }

        private void Update()
        {
            if (content == null || (content.anchoredPosition - contentTarget).sqrMagnitude < 0.001f) return;
            content.anchoredPosition = Vector2.Lerp(content.anchoredPosition, contentTarget,
                1 - Mathf.Exp(-30 * Time.unscaledDeltaTime));
        }

        public void SetRestartIcon(bool restart)
        {
            if (icon == null) return;
            icon.Configure(restart ? GameUiShape.Shape.Restart : GameUiShape.Shape.Forward, Color.white);
            FitContents();
            DoStateTransition(currentSelectionState, true);
        }

        public void SetPrimary(bool value)
        { Configure(value, rim, (GameUiShape)targetGraphic, icon, label, content); }

        public void SetPauseIcon()
        {
            if (icon == null) return;
            icon.Configure(GameUiShape.Shape.Pause, Color.white);
            DoStateTransition(currentSelectionState, true);
        }

        private void FitContents()
        {
            if (label == null || icon == null) return;
            float width = Mathf.Clamp(label.GetPreferredValues().x, 60, 400);
            label.rectTransform.sizeDelta = new Vector2(width, 66);
            label.rectTransform.anchoredPosition = new Vector2(24, 1);
            icon.rectTransform.anchoredPosition = new Vector2(-width * 0.5f - 8, 0);
        }

        private static Color Hex(byte r, byte g, byte b) => new Color32(r, g, b, 255);
    }
}
