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
                // The caster and unarmed idles leave the fingers open. Retarget the armed
                // humanoid animations so every skin actually grasps its weapon and shield.
                if (animator.isHuman && catalog.armedAnimatorController != null)
                    animator.runtimeAnimatorController = catalog.armedAnimatorController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.keepAnimatorStateOnDisable = true;
                if (animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                    animator.Update(0);
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
            if (!bones.TryGetValue(boneName, out Transform socket))
            {
                Debug.LogWarning("Cannot attach " + item.id + ": missing bone " + boneName, hero);
                return;
            }
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
                item.slot == EquipmentSlot.Weapon ? Quaternion.Euler(0, 0, 90) : Quaternion.LookRotation(Vector3.back, Vector3.right);
            if (item.cloak) equipment.transform.rotation = hero.transform.rotation;
            float target = height * (item.cloak ? 0.56f : item.slot == EquipmentSlot.Weapon ? 0.43f : 0.29f);
            // Measure the model's own length, so animated hand/parent rotations cannot change its size.
            Bounds localBounds = LocalBoundsOf(equipment);
            float length = equipment.transform.TransformVector(Vector3.up * localBounds.size.y).magnitude;
            equipment.transform.localScale *= target / Mathf.Max(0.001f, length);
            if (item.cloak)
            {
                foreach (Renderer original in hero.GetComponentsInChildren<Renderer>())
                    if (!original.transform.IsChildOf(equipment.transform) && original.name.StartsWith("Cloak", StringComparison.OrdinalIgnoreCase))
                        original.enabled = false;
                // Cloaks are authored from the collar down. Align that collar with the neck,
                // rather than centering a long mesh, which lifts its top above the shoulders.
                Transform collar = bones.TryGetValue("CC_Base_NeckTwist01", out Transform neck) ? neck : socket;
                Vector3 mount = new Vector3(localBounds.center.x, localBounds.max.y, localBounds.max.z);
                Vector3 anchor = collar.position - hero.transform.forward * height * 0.08f;
                equipment.transform.position += anchor - equipment.transform.TransformPoint(mount);
            }
            else
            {
                // Hand bones start at the wrist. Use the knuckles to accommodate each skin's palm size.
                string middleName = item.slot == EquipmentSlot.Weapon ? "CC_Base_R_Mid1" : "CC_Base_L_Mid1";
                float palmLength = bones.TryGetValue(middleName, out Transform middle) ? middle.localPosition.y : 15;
                Vector3 palm = new Vector3(0, palmLength * 0.88f, palmLength * 0.25f);
                equipment.transform.localPosition = palm - equipment.transform.localRotation *
                    Vector3.Scale(equipment.transform.localScale, item.gripPoint);
                if (item.slot == EquipmentSlot.Armor) AddShieldHandle(equipment, item.gripPoint, localBounds, replacement);
            }
        }

        private static void AddShieldHandle(GameObject shield, Vector3 grip, Bounds body, Material material)
        {
            // A rear grip provides clearance for the fingers and the stone hero's large gauntlets.
            float span = body.size.y * 0.22f;
            float depth = Mathf.Max(0.02f, body.min.z - grip.z);
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", new Color(0.24f, 0.20f, 0.16f));
            void Bar(string name, Vector3 position, Vector3 scale)
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = name;
                bar.transform.SetParent(shield.transform, false);
                bar.transform.localPosition = position;
                bar.transform.localScale = scale;
                Collider collider = bar.GetComponent<Collider>();
                if (Application.isPlaying) UnityEngine.Object.Destroy(collider);
                else UnityEngine.Object.DestroyImmediate(collider);
                Renderer renderer = bar.GetComponent<Renderer>();
                if (material != null) renderer.sharedMaterial = material;
                renderer.SetPropertyBlock(tint);
            }
            Bar("HandGrip", grip, new Vector3(0.028f, span, 0.028f));
            foreach (float end in new[] { -0.5f, 0.5f })
                Bar("GripMount", grip + new Vector3(0, span * end, depth * 0.5f), new Vector3(0.035f, 0.035f, depth));
        }

        public static Bounds LocalBoundsOf(GameObject model)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool found = false;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                Bounds part = renderer.localBounds;
                Matrix4x4 toModel = model.transform.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = toModel.MultiplyPoint3x4(part.center + Vector3.Scale(part.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
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
