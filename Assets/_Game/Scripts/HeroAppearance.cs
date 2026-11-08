using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerTrials
{
    public static class HeroAppearance
    {
        public static GameObject Create(HeroCenterCatalog catalog, HeroProfile profile, Transform parent, float height)
        {
            HeroEquipment skin = catalog.Find(profile.skin);
            if (skin == null || skin.prefab == null) return null;
            GameObject hero = UnityEngine.Object.Instantiate(skin.prefab, parent, false);
            hero.name = "EquippedHero_" + skin.id;
            hero.transform.localPosition = Vector3.zero;
            hero.transform.localRotation = Quaternion.identity;
            foreach (Renderer renderer in hero.GetComponentsInChildren<Renderer>())
            {
                string part = renderer.name;
                if (part.IndexOf("sword", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    part.IndexOf("wand", StringComparison.OrdinalIgnoreCase) >= 0)
                    renderer.gameObject.SetActive(false);
            }
            Animator animator = hero.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.keepAnimatorStateOnDisable = true;
            }
            Bounds bounds = BoundsOf(hero);
            hero.transform.localScale *= height / Mathf.Max(0.01f, bounds.size.y);
            bounds = BoundsOf(hero);
            hero.transform.position -= new Vector3(bounds.center.x - parent.position.x, bounds.min.y - parent.position.y, bounds.center.z - parent.position.z);
            Attach(catalog, catalog.Find(profile.weapon), hero, height);
            Attach(catalog, catalog.Find(profile.armor), hero, height);
            return hero;
        }

        private static void Attach(HeroCenterCatalog catalog, HeroEquipment item, GameObject hero, float height)
        {
            if (item == null || item.prefab == null) return;
            var bones = new Dictionary<string, Transform>();
            foreach (Transform bone in hero.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(bone.name)) bones.Add(bone.name, bone);
            string boneName = item.cloak ? "CC_Base_Spine02" : item.slot == EquipmentSlot.Weapon ? "CC_Base_R_Hand" : "CC_Base_L_Hand";
            if (!bones.TryGetValue(boneName, out Transform socket)) socket = hero.transform;
            GameObject equipment = UnityEngine.Object.Instantiate(item.prefab, socket, false);
            equipment.name = "Equipped_" + item.id;
            foreach (Animator animation in equipment.GetComponentsInChildren<Animator>()) animation.enabled = false;
            Material replacement = item.cloak ? catalog.cloakMaterial : catalog.weaponMaterial;
            if (replacement != null)
                foreach (Renderer surface in equipment.GetComponentsInChildren<Renderer>())
                {
                    var materials = surface.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = replacement;
                    surface.sharedMaterials = materials;
                }
            equipment.transform.localPosition = Vector3.zero;
            equipment.transform.localRotation = item.cloak ? Quaternion.identity :
                item.slot == EquipmentSlot.Weapon ? Quaternion.Euler(0, 0, 90) : Quaternion.Euler(0, 90, 90);
            if (item.cloak) equipment.transform.rotation = hero.transform.rotation;
            Bounds bounds = BoundsOf(equipment);
            float target = height * (item.cloak ? 0.56f : item.slot == EquipmentSlot.Weapon ? 0.43f : 0.29f);
            equipment.transform.localScale *= target / Mathf.Max(0.001f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            if (item.cloak)
            {
                foreach (Renderer original in hero.GetComponentsInChildren<Renderer>())
                    if (!original.transform.IsChildOf(equipment.transform) && original.name.StartsWith("Cloak", StringComparison.OrdinalIgnoreCase))
                        original.enabled = false;
                bounds = BoundsOf(equipment);
                Vector3 center = hero.transform.position + Vector3.up * height * 0.52f - hero.transform.forward * height * 0.12f;
                equipment.transform.position += center - bounds.center;
            }
        }

        public static Bounds BoundsOf(GameObject model)
        {
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            bool found = false;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }
    }
}
