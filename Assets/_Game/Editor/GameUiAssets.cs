using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

public static class GameUiAssets
{
    [MenuItem("Tower Trials/Prepare UI Fonts")]
    public static void PrepareFonts()
    {
        Directory.CreateDirectory("Assets/_Game/Resources/Fonts");
        AssetDatabase.Refresh();
        Bake("AlegreyaSC-Bold", "TT_Display", false);
        Bake("AlegreyaSans-Medium", "TT_Body", false);
        Bake("AlegreyaSans-Bold", "TT_Bold", true);
        AssetDatabase.SaveAssets();
        Debug.Log("Game UI fonts prepared: Alegreya SC Bold, Alegreya Sans Medium and Bold.");
    }

    private static void Bake(string sourceName, string assetName, bool tabularNumbers)
    {
        string path = "Assets/_Game/Resources/Fonts/" + assetName + ".asset";
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null) return;
        Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Art/Fonts/" + sourceName + ".ttf");
        if (source == null) throw new InvalidOperationException("Missing UI font source: " + sourceName);
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, false);
        font.name = assetName;
        var characters = new List<uint>();
        for (uint code = 32; code <= 126; code++) characters.Add(code);
        characters.AddRange(new uint[] { 160, 0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2026 });
        if (!font.TryAddCharacters(characters.ToArray(), out uint[] missing))
            throw new InvalidOperationException("UI font glyphs could not be baked: " + sourceName + ", missing " + missing.Length);
        if (tabularNumbers)
        {
            float advance = 0;
            for (uint digit = '0'; digit <= '9'; digit++)
                advance = Mathf.Max(advance, font.characterLookupTable[digit].glyph.metrics.horizontalAdvance);
            for (uint digit = '0'; digit <= '9'; digit++)
            {
                var glyph = font.characterLookupTable[digit].glyph;
                GlyphMetrics metrics = glyph.metrics;
                glyph.metrics = new GlyphMetrics(metrics.width, metrics.height,
                    metrics.horizontalBearingX + (advance - metrics.horizontalAdvance) * 0.5f,
                    metrics.horizontalBearingY, advance);
            }
        }
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font, path);
        foreach (Texture2D texture in font.atlasTextures)
        {
            texture.name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(texture, font);
        }
        font.material.name = assetName + " Material";
        AssetDatabase.AddObjectToAsset(font.material, font);
        EditorUtility.SetDirty(font);
    }
}
