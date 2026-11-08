using System;
using TMPro;
using UnityEngine;

namespace TowerTrials
{
    public enum GameTextRole { Body, Display, Action, Number }

    public static class GameUiTheme
    {
        public static readonly Color Brass = new Color32(196, 163, 103, 255);
        public static readonly Color Parchment = new Color32(240, 232, 208, 255);
        private static TMP_FontAsset display, body, bold;

        public static TMP_FontAsset Font(GameTextRole role)
        {
            if (display == null) display = Load("TT_Display");
            if (body == null) body = Load("TT_Body");
            if (bold == null) bold = Load("TT_Bold");
            return role == GameTextRole.Display ? display :
                role == GameTextRole.Action || role == GameTextRole.Number ? bold : body;
        }

        private static TMP_FontAsset Load(string name)
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/" + name);
            if (font == null) throw new InvalidOperationException("Missing game font: " + name + ". Run Tower Trials > Prepare UI Fonts.");
            return font;
        }
    }
}
