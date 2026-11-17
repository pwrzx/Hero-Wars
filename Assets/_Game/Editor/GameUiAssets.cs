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
        const string kit = "Assets/300Mind/2D Game UI Kit/Fonts/";
        Bake(kit + "Oswald-Regular.ttf", "TT_Body", false);
        Bake(kit + "Oswald-SemiBold.ttf", "TT_Bold", true);
        Bake("Assets/_Game/Art/Fonts/GermaniaOne-Regular.ttf", "TT_Display", false, Resources.Load<TMP_FontAsset>("Fonts/TT_Body"));
        AssetDatabase.SaveAssets();
        Debug.Log("Game UI fonts prepared: Germania One and Oswald.");
    }

    private static void Bake(string sourcePath, string assetName, bool tabularNumbers, TMP_FontAsset fallback = null)
    {
        string path = "Assets/_Game/Resources/Fonts/" + assetName + ".asset";
        Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null) throw new InvalidOperationException("Missing UI font source: " + sourcePath);
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null && new SerializedObject(existing).FindProperty("m_SourceFontFileGUID").stringValue
            == AssetDatabase.AssetPathToGUID(sourcePath)) return;
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, false);
        font.name = assetName;
        var characters = new List<uint>();
        for (uint code = 32; code <= 126; code++) characters.Add(code);
        characters.AddRange(new uint[] { 160, 0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2026 });
        if (fallback != null) font.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
        if (!font.TryAddCharacters(characters.ToArray(), out uint[] missing) &&
            (fallback == null || Array.Exists(missing, code => !fallback.HasCharacter((int)code))))
            throw new InvalidOperationException("Missing glyphs in " + sourcePath + ": " + string.Join(", ", missing));
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
        if (existing != null)
        {
            // Scenes serialize references to all three objects: the font, its material and its atlas.
            Texture2D pixels = font.atlasTexture;
            Texture2D atlas = existing.atlasTexture;
            atlas.LoadRawTextureData(pixels.GetRawTextureData<byte>());
            atlas.Apply(false);
            Material temporaryMaterial = font.material;
            font.material = existing.material;
            font.atlasTextures = new[] { atlas };
            EditorUtility.CopySerialized(font, existing);
            existing.ReadFontAssetDefinition();
            EditorUtility.SetDirty(existing);
            EditorUtility.SetDirty(atlas);
            font.material = temporaryMaterial;
            font.atlasTextures = new[] { pixels };
            UnityEngine.Object.DestroyImmediate(font);
            return;
        }
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
