using System;
using UnityEngine;

namespace TowerTrials
{
    public enum ItemRarity { Common, Rare, Epic, Legendary }
    public enum EquipmentSlot { Skin, Armor, Weapon }

    [Serializable]
    public sealed class HeroEquipment
    {
        public string id, title;
        [TextArea] public string description;
        public EquipmentSlot slot;
        public ItemRarity rarity;
        public bool starter, cloak;
        public GameObject prefab;
        public Sprite icon;
        public int power;
        [Tooltip("Point inside the handle, in the equipment prefab's local coordinates.")]
        public Vector3 gripPoint;
    }

    [Serializable]
    public sealed class HeroChest
    {
        public string id, title;
        public ItemRarity rarity;
        public int price;
        public bool costsGems;
        public int[] weights;
        public Sprite icon;

        public string Odds
        {
            get
            {
                string text = "";
                int entries = 0;
                for (int i = 0; i < weights.Length; i++)
                {
                    if (weights[i] == 0) continue;
                    if (text.Length > 0) text += entries % 2 == 0 ? "\n" : "  /  ";
                    text += weights[i] + "% " + (ItemRarity)i;
                    entries++;
                }
                return text;
            }
        }
    }

    [CreateAssetMenu(menuName = "Tower Trials/Hero Center Catalog")]
    public sealed class HeroCenterCatalog : ScriptableObject
    {
        public HeroEquipment[] equipment = Array.Empty<HeroEquipment>();
        public HeroChest[] chests = Array.Empty<HeroChest>();
        public Sprite goldIcon, gemIcon, shopIcon, heroIcon, armorIcon, settingsIcon, dailyIcon;
        public Sprite primaryButton, secondaryButton, beveledButton, slotFrame;
        public Texture2D background;
        public Material weaponMaterial, cloakMaterial;
        public RuntimeAnimatorController armedAnimatorController;

        public HeroEquipment Find(string id) => Array.Find(equipment, item => item.id == id);
        public static HeroCenterCatalog Load() => Resources.Load<HeroCenterCatalog>("HeroCenter/Catalog");

        public static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return new Color32(71, 159, 255, 255);
                case ItemRarity.Epic: return new Color32(184, 108, 255, 255);
                case ItemRarity.Legendary: return new Color32(255, 199, 67, 255);
                default: return new Color32(207, 214, 225, 255);
            }
        }
    }
}
