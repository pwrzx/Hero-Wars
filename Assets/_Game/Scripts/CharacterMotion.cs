using System.Collections.Generic;
using UnityEngine;

namespace TowerTrials
{
    public enum CombatPose { Anticipate, Flight, Windup, Strike, Recover, Guard, Recoil, Defeat, Celebrate }

    // A small procedural rig for the starter mesh pieces. Gameplay moves the outer root;
    // this component owns only the inner body, shoulder, head and leg pivots.
    public sealed class CharacterMotion : MonoBehaviour
    {
        private Transform body, weapon, shield, head, leftLeg, rightLeg, cape;
        private Quaternion capeRest;
        private TrailRenderer trail;
        private bool acting, knight;
        private float phase;
        private float landingTime, landingStrength;
        private CombatPose previousPose = CombatPose.Recover;
        private Renderer[] surfaces;
        private Animator importedAnimator;
        private float importedAnimatorSpeed;
        private bool importedPoseActive;
        private static readonly int ImportedIdle = Animator.StringToHash("Base Layer.Blend");
        private static readonly int ImportedAttack = Animator.StringToHash("Base Layer.Attack");
        private static readonly int ImportedJump = Animator.StringToHash("Base Layer.Jump");
        private static readonly int ImportedDeath = Animator.StringToHash("Base Layer.Death");
        private Color[] surfaceColors;
        private MaterialPropertyBlock hitProperties;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public Transform SwordTip { get; private set; }
        public int SwingCount { get; private set; }
        public bool HasWeaponRig => weapon != null;
        public bool HasImportedVisual => importedAnimator != null;

        public void Configure(bool isKnight)
        {
            if (body != null) return;
            knight = isKnight;
            // Native Unity objects must be allocated on the main thread, not by the scene loader's constructor.
            hitProperties = new MaterialPropertyBlock();
            surfaces = GetComponentsInChildren<Renderer>();
            surfaceColors = new Color[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++) surfaceColors[i] = surfaces[i].sharedMaterial.GetColor(BaseColor);
            phase = transform.position.x * 0.8f + transform.position.y * 1.7f;
            var parts = new List<Transform>();
            foreach (Transform child in transform) parts.Add(child);
            body = Pivot("BodyRig", transform, Vector3.zero);
            foreach (Transform part in parts) part.SetParent(body, true);
            weapon = Pivot("WeaponShoulder", body, new Vector3(knight ? -0.35f : -0.28f, knight ? 1.15f : 0.95f, 0.05f));
            shield = Pivot("ShieldShoulder", body, new Vector3(0.35f, 1.15f, 0.05f));
            head = Pivot("HeadPivot", body, new Vector3(0, knight ? 1.2f : 0.98f, 0));
            leftLeg = Pivot("LeftHip", body, new Vector3(-0.16f, knight ? 0.65f : 0.55f, 0));
            rightLeg = Pivot("RightHip", body, new Vector3(0.16f, knight ? 0.65f : 0.55f, 0));
            foreach (Transform part in parts)
            {
                string name = part.name;
                if (name == "Cape") { cape = part; capeRest = part.localRotation; continue; }
                if (name.StartsWith("Sword") || name == "Blade" || name.StartsWith("Club") ||
                    ((name.StartsWith("Arm") || name.StartsWith("Pauldron") || name.StartsWith("Goblin arm")) && part.localPosition.x < -0.2f && name != "Armored torso"))
                    part.SetParent(weapon, true);
                else if (name.StartsWith("Shield") || ((name.StartsWith("Arm") || name.StartsWith("Pauldron")) && part.localPosition.x > 0.2f))
                    part.SetParent(shield, true);
                else if (name.StartsWith("Leg") || name.StartsWith("Boot") || name.StartsWith("Goblin leg") || name.StartsWith("Goblin boot"))
                    part.SetParent(part.localPosition.x < 0 ? leftLeg : rightLeg, true);
                else if (name.Contains("Helmet") || name.StartsWith("Eye") || name == "Face" || name == "Visor" ||
                    name.StartsWith("Goblin head") || name.StartsWith("Pointed ear") || name.StartsWith("Glowing eye") || name == "Brow" || name == "Mouth")
                    part.SetParent(head, true);
            }
            SwordTip = Pivot("SwordTip", weapon, new Vector3(-0.13f, 0.67f, 0.10f));
            if (Application.isPlaying && knight)
            {
                trail = SwordTip.gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = Resources.Load<Material>("Materials/FX_Glow");
                trail.time = 0.13f;
                trail.minVertexDistance = 0.025f;
                trail.startWidth = 0.16f;
                trail.endWidth = 0.01f;
                trail.startColor = new Color(0.55f, 0.92f, 1, 0.9f);
                trail.endColor = new Color(1, 0.75f, 0.25f, 0);
                trail.numCornerVertices = 3;
                trail.emitting = false;
            }
            Idle(0);
        }

        public void UseHeroLoadout(HeroCenterCatalog catalog, HeroProfile profile)
        {
            foreach (Renderer original in surfaces) original.enabled = false;
            GameObject visual = HeroAppearance.Create(catalog, profile, body, 1.65f);
            if (visual == null)
            {
                foreach (Renderer original in surfaces) original.enabled = true;
                return;
            }
            importedAnimator = visual.GetComponentInChildren<Animator>();
            if (importedAnimator != null) importedAnimatorSpeed = importedAnimator.speed;
            surfaces = visual.GetComponentsInChildren<Renderer>();
            surfaceColors = new Color[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++) surfaceColors[i] = surfaces[i].sharedMaterial.GetColor(BaseColor);
        }

        private static Transform Pivot(string name, Transform parent, Vector3 position)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = position;
            return pivot;
        }

        private void Update()
        {
            if (body == null || acting) return;
            Idle(Time.time * (knight ? 2.8f : 2.2f) + phase);
            if (landingTime <= 0) return;
            landingTime = Mathf.Max(0, landingTime - Time.deltaTime);
            float compression = Mathf.Sin((1 - landingTime / 0.22f) * Mathf.PI) * landingStrength;
            SetBody(0, -0.07f * compression, 2 * compression, 1 - 0.06f * compression);
        }

        public void ReactToLanding(float strength)
        {
            landingTime = 0.22f;
            landingStrength = Mathf.Clamp01(strength);
        }

        private void Idle(float time)
        {
            float breathe = Mathf.Sin(time);
            SetBody(0, breathe * 0.018f, breathe * 0.8f, 1);
            weapon.localRotation = Quaternion.Euler(0, 0, (knight ? 17 : -7) + breathe * 3);
            shield.localRotation = Quaternion.Euler(0, 0, -8 - breathe * 2);
            head.localRotation = Quaternion.Euler(0, breathe * 2, -breathe * 0.8f);
            leftLeg.localRotation = rightLeg.localRotation = Quaternion.identity;
            if (cape != null) cape.localRotation = capeRest * Quaternion.Euler(Mathf.Sin(time - 0.4f) * 4, 0, 0);
        }

        public void Pose(CombatPose pose, float t)
        {
            acting = true;
            t = Mathf.Clamp01(t);
            if (pose != previousPose && pose == CombatPose.Strike) SwingCount++;
            PoseImported(pose, t);
            previousPose = pose;
            if (trail != null)
            {
                bool emit = pose == CombatPose.Strike;
                if (emit && !trail.emitting) trail.Clear();
                trail.emitting = emit;
            }
            SetBody(0, 0, 0, 1);
            head.localRotation = leftLeg.localRotation = rightLeg.localRotation = Quaternion.identity;
            float angle = 17, shieldAngle = -8;
            switch (pose)
            {
                case CombatPose.Anticipate:
                    SetBody(0.025f * t, -0.10f * t, -7 * t, 1 - 0.055f * t);
                    angle = Mathf.Lerp(17, -48, t); break;
                case CombatPose.Flight:
                    SetBody(0, 0, -10, 1.025f);
                    angle = -45;
                    leftLeg.localRotation = Quaternion.Euler(0, 0, 35);
                    rightLeg.localRotation = Quaternion.Euler(0, 0, -20);
                    break;
                case CombatPose.Windup:
                    angle = Mathf.Lerp(-45, -100, t);
                    SetBody(0.04f * t, 0, -12 * t, 1);
                    shieldAngle = 12; break;
                case CombatPose.Strike:
                    angle = Mathf.Lerp(-100, 125, 1 - Mathf.Pow(1 - t, 3));
                    SetBody(-0.08f * Mathf.Sin(t * Mathf.PI), -0.03f, Mathf.Lerp(-12, 13, t), 1);
                    shieldAngle = 12; break;
                case CombatPose.Recover:
                    angle = Mathf.Lerp(125, 17, Mathf.SmoothStep(0, 1, t)); break;
                case CombatPose.Guard:
                    angle = -25; shieldAngle = 45;
                    SetBody(0.02f, -0.04f, -9, 0.97f); break;
                case CombatPose.Recoil:
                    SetBody(-0.12f * Mathf.Sin(t * Mathf.PI), 0, -22 * Mathf.Sin(t * Mathf.PI), 1);
                    angle = -40; break;
                case CombatPose.Defeat:
                    SetBody(-0.16f * t, -0.2f * t, -70 * t, 1 - 0.45f * t);
                    angle = 65; break;
                case CombatPose.Celebrate:
                    SetBody(0, Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2)) * 0.08f, 0, 1);
                    angle = Mathf.Lerp(17, -10, Mathf.Sin(t * Mathf.PI)); break;
            }
            weapon.localRotation = Quaternion.Euler(0, 0, angle);
            shield.localRotation = Quaternion.Euler(0, 0, shieldAngle);
            if (cape != null) cape.localRotation = capeRest * Quaternion.Euler(pose == CombatPose.Flight ? 24 : 0, 0, 0);
        }

        private void PoseImported(CombatPose pose, float t)
        {
            if (importedAnimator == null) return;
            int state;
            float progress;
            switch (pose)
            {
                case CombatPose.Anticipate: state = ImportedJump; progress = Mathf.Lerp(0, 0.12f, t); break;
                case CombatPose.Flight: state = ImportedJump; progress = Mathf.Lerp(0.12f, 0.85f, t); break;
                case CombatPose.Windup: state = ImportedAttack; progress = Mathf.Lerp(0, 0.22f, t); break;
                case CombatPose.Strike: state = ImportedAttack; progress = Mathf.Lerp(0.22f, 0.40f, t); break;
                case CombatPose.Recover: state = ImportedAttack; progress = Mathf.Lerp(0.40f, 0.999f, t); break;
                case CombatPose.Defeat: state = ImportedDeath; progress = Mathf.Lerp(0, 0.999f, t); break;
                default: return;
            }
            if (!importedAnimator.HasState(0, state)) return;
            if (!importedPoseActive)
            {
                ClearImportedTriggers();
                importedPoseActive = true;
                importedAnimator.speed = 0;
            }
            // The combat coroutine owns the clock. Sampling its phase immediately prevents
            // jump exit times and long controller blends from deferring the swing until home.
            importedAnimator.Play(state, 0, progress);
            importedAnimator.Update(0);
        }

        private void ClearImportedTriggers()
        {
            foreach (AnimatorControllerParameter parameter in importedAnimator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger)
                    importedAnimator.ResetTrigger(parameter.nameHash);
        }

        private void SetBody(float x, float y, float lean, float stretch)
        {
            body.localPosition = new Vector3(x, y, 0);
            body.localRotation = Quaternion.Euler(0, 0, lean);
            body.localScale = new Vector3(1 / Mathf.Sqrt(stretch), stretch, 1);
        }

        public void Release()
        {
            acting = false;
            previousPose = CombatPose.Recover;
            if (importedAnimator != null && importedPoseActive)
            {
                ClearImportedTriggers();
                importedAnimator.speed = importedAnimatorSpeed;
                importedPoseActive = false;
                importedAnimator.Play(ImportedIdle, 0, 0);
                importedAnimator.Update(0);
            }
            HitFlash(0);
            if (trail != null) trail.emitting = false;
            Idle(Time.time * 2.8f + phase);
        }

        public void HitFlash(float amount)
        {
            for (int i = 0; i < surfaces.Length; i++)
            {
                surfaces[i].GetPropertyBlock(hitProperties);
                hitProperties.SetColor(BaseColor, Color.Lerp(surfaceColors[i], new Color(1.8f, 1.65f, 1.3f), amount));
                surfaces[i].SetPropertyBlock(hitProperties);
            }
        }
    }
}
