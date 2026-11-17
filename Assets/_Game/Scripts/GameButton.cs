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
            HeroCenterCatalog kit = HeroCenterCatalog.Load();
            bool tile = !isPrimary && actionIcon == null && string.IsNullOrEmpty(actionLabel.text);
            Sprite art = isPrimary ? kit.primaryButton : tile ? kit.secondaryButton : kit.beveledButton;
            float pixelScale = isPrimary ? 1.35f : tile ? 1.5f : 4;
            face.SetArtwork(art, true, pixelScale);
            rim.SetArtwork(art, true, pixelScale);
            transition = Transition.ColorTint;
            var palette = colors;
            palette.normalColor = isPrimary ? Hex(248, 187, 141) : tile ? Hex(192, 196, 191) : Hex(182, 116, 103);
            palette.highlightedColor = isPrimary ? Hex(255, 218, 166) : tile ? Color.white : Hex(218, 148, 127);
            palette.selectedColor = palette.highlightedColor;
            palette.pressedColor = isPrimary ? Hex(213, 150, 108) : Hex(132, 91, 86);
            palette.disabledColor = isPrimary ? Hex(94, 93, 93) : Hex(112, 77, 74);
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
            Color edge = focus ? Color.white : highlight ? Hex(238, 211, 176) : Hex(94, 80, 74);
            Color ink = disabled ? Hex(218, 216, 200) : primary ? Hex(62, 37, 22) : Hex(255, 245, 220);
            rim.CrossFadeColor(edge, instant ? 0 : 0.1f, true, true);
            label.CrossFadeColor(ink, instant ? 0 : 0.1f, true, true);
            if (icon != null) icon.CrossFadeColor(icon.sprite == null ? ink : Color.white, instant ? 0 : 0.1f, true, true);
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

        public void SetHeroIcon()
        {
            icon.SetArtwork(HeroCenterCatalog.Load().heroIcon, false);
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
