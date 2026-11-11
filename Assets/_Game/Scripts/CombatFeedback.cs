using UnityEngine;
using UnityEngine.Rendering;

namespace TowerTrials
{
    // Bounded mesh-effect pool and native particle bursts. No per-frame object creation.
    [DefaultExecutionOrder(-50)]
    public sealed class CombatFeedback : MonoBehaviour
    {
        private const int PoolSize = 24;
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private static readonly Color Gold = new Color(1, 0.72f, 0.20f);
        private static readonly Color Ice = new Color(0.40f, 0.88f, 1);
        private sealed class Pulse
        {
            public Transform transform;
            public MeshFilter filter;
            public MeshRenderer renderer;
            public MaterialPropertyBlock properties = new MaterialPropertyBlock();
            public float age, life, size, endSize;
            public Vector3 origin;
            public Color color;
            public Transform target;
            public bool active, orb;
        }
        private readonly Pulse[] pulses = new Pulse[PoolSize];
        private Mesh quad, arc;
        private Material glow, ring, slash;
        private ParticleSystem sparks;
        private Camera view;
        private Vector3 cameraRest;
        private float shake, shakeTime, strength;
        public int ImpactCount { get; private set; }
        public int SlashCount { get; private set; }
        public int ActivePulseCount { get; private set; }
        public int ParticleCount => sparks == null ? 0 : sparks.particleCount;

        public void Configure(Camera camera, float intensity)
        {
            view = camera;
            cameraRest = view.transform.position;
            strength = intensity;
            glow = Resources.Load<Material>("Materials/FX_Glow");
            ring = Resources.Load<Material>("Materials/FX_Ring");
            slash = Resources.Load<Material>("Materials/FX_Slash");
            if (glow == null || ring == null || slash == null)
                throw new System.InvalidOperationException("Prepare the combat effect materials before playing.");
            quad = BuildQuad();
            arc = BuildArc();
            for (int i = 0; i < pulses.Length; i++)
            {
                var obj = new GameObject("PooledEffect_" + i, typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(transform, false);
                var renderer = obj.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                pulses[i] = new Pulse { transform = obj.transform, filter = obj.GetComponent<MeshFilter>(), renderer = renderer };
                obj.SetActive(false);
            }
            sparks = MakeParticles("CombatSparks", 160, false);
            MakeParticles("AmbientMotes", 32, true);
        }

        private ParticleSystem MakeParticles(string name, int count, bool ambient)
        {
            var obj = new GameObject(name, typeof(ParticleSystem));
            obj.transform.SetParent(transform, false);
            var particles = obj.GetComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count;
            main.startLifetime = ambient ? 8 : 0.5f;
            main.startSize = ambient ? 0.055f : 0.10f;
            main.startSpeed = ambient ? 0.08f : 0;
            main.gravityModifier = ambient ? 0 : 0.35f;
            main.startColor = ambient ? new Color(0.6f, 1, 0.72f, 0.35f) : Color.white;
            var emission = particles.emission;
            emission.enabled = ambient;
            emission.rateOverTime = ambient ? 2 : 0;
            var shape = particles.shape;
            shape.enabled = ambient;
            if (ambient)
            {
                obj.transform.position = new Vector3(0, 3, -1.4f);
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(8, 9, 0.1f);
            }
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(ambient ? 0 : 1, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glow;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
            return particles;
        }

        private void Update()
        {
            ActivePulseCount = 0;
            foreach (Pulse pulse in pulses)
            {
                if (pulse == null || !pulse.active) continue;
                pulse.age += Time.deltaTime;
                float t = Mathf.Clamp01(pulse.age / pulse.life);
                if (t >= 1)
                {
                    pulse.active = false;
                    pulse.transform.gameObject.SetActive(false);
                    continue;
                }
                ActivePulseCount++;
                if (pulse.orb && pulse.target != null)
                {
                    float ease = t * t;
                    Vector3 destination = pulse.target.position + Vector3.up * 0.8f;
                    pulse.transform.position = Vector3.Lerp(pulse.origin, destination, ease) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.55f);
                }
                float scale = Mathf.Lerp(pulse.size, pulse.endSize, t);
                pulse.transform.localScale = Vector3.one * scale;
                Color color = pulse.color;
                color.a *= Mathf.Pow(1 - t, pulse.orb ? 0.3f : 1.4f);
                pulse.properties.SetColor(Tint, color);
                pulse.renderer.SetPropertyBlock(pulse.properties);
            }
        }

        private void LateUpdate()
        {
            if (view == null) return;
            shakeTime -= Time.unscaledDeltaTime;
            float amount = !GamePreferences.ReducedMotion && shakeTime > 0 ? shake * Mathf.Clamp01(shakeTime / 0.18f) : 0;
            float clock = Time.unscaledTime;
            view.transform.position = cameraRest + new Vector3(Mathf.Sin(clock * 113) * amount, Mathf.Cos(clock * 89) * amount * 0.65f, 0);
        }

        public void Shake(float amount)
        { shake = amount * strength; shakeTime = 0.18f; }

        public void Slash(Vector3 position)
        {
            SlashCount++;
            Spawn(position + new Vector3(0, 0.55f, -0.85f), slash, arc, 0.25f, 0.9f, 1.5f, new Color(0.65f, 1.15f, 1.35f, 1));
        }

        public void Impact(Vector3 position, bool victory)
        {
            ImpactCount++;
            Color color = victory ? Gold : new Color(1, 0.22f, 0.18f);
            Vector3 front = position + new Vector3(0, 0.65f, -0.9f);
            Burst(front, color, 18, 2.8f);
            Spawn(front, glow, quad, 0.13f, 1.4f, 0.2f, new Color(1, 0.95f, 0.72f, 0.8f));
            Spawn(front, ring, quad, 0.32f, 0.25f, 2, color);
            Shake(victory ? 0.07f : 0.11f);
        }

        public void Landing(Vector3 position)
        {
            Burst(position + new Vector3(0, 0.12f, -0.9f), new Color(0.48f, 0.82f, 0.75f, 0.65f), 8, 0.75f);
        }

        public void PowerTransfer(Vector3 origin, Transform hero)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector3 offset = new Vector3(Mathf.Sin(i * 2.4f) * 0.25f, 0.45f + i * 0.06f, -1);
                Pulse pulse = Spawn(origin + offset, glow, quad, 0.35f + i * 0.04f, 0.22f, 0.12f, Gold);
                if (pulse != null) { pulse.orb = true; pulse.target = hero; }
            }
        }

        public void Collapse(Vector3 position)
        {
            Burst(position + new Vector3(0, 1.5f, -1), new Color(0.75f, 0.65f, 0.4f, 0.75f), 28, 3.3f);
            Spawn(position + new Vector3(0, 0.5f, -1), ring, quad, 0.45f, 0.5f, 3, Gold);
            Shake(0.10f);
        }

        public void Victory(Vector3 position)
        {
            Burst(position + new Vector3(0, 2, -1), Gold, 42, 4);
            Spawn(position + new Vector3(0, 0.9f, -1), ring, quad, 0.65f, 0.5f, 3, Ice);
        }

        private void Burst(Vector3 position, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.399963f;
                float variation = 0.45f + (i * 7 % 11) / 18f;
                var particle = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) + 0.45f, 0) * (speed * variation),
                    startColor = color,
                    startSize = 0.06f + (i % 4) * 0.025f,
                    startLifetime = 0.28f + (i % 5) * 0.065f
                };
                sparks.Emit(particle, 1);
            }
        }

        private Pulse Spawn(Vector3 position, Material material, Mesh mesh, float life, float size, float endSize, Color color)
        {
            foreach (Pulse pulse in pulses)
            {
                if (pulse.active) continue;
                pulse.active = true; pulse.age = 0; pulse.life = life;
                pulse.origin = position; pulse.size = size; pulse.endSize = endSize;
                pulse.color = color; pulse.orb = false; pulse.target = null;
                pulse.transform.position = position;
                pulse.transform.rotation = view.transform.rotation;
                pulse.transform.localScale = Vector3.one * size;
                pulse.filter.sharedMesh = mesh;
                pulse.renderer.sharedMaterial = material;
                pulse.properties.SetColor(Tint, color);
                pulse.renderer.SetPropertyBlock(pulse.properties);
                pulse.transform.gameObject.SetActive(true);
                return pulse;
            }
            return null; // Cosmetic saturation never allocates or blocks combat.
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "FeedbackQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f,-0.5f,0), new Vector3(0.5f,-0.5f,0), new Vector3(-0.5f,0.5f,0), new Vector3(0.5f,0.5f,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0,2,1, 2,3,1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildArc()
        {
            const int segments = 28;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(-140, 100, t) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                float width = Mathf.Sin(t * Mathf.PI) * 0.26f;
                vertices[i * 2] = direction * (0.72f - width);
                vertices[i * 2 + 1] = direction * (0.72f + width);
                uv[i * 2] = new Vector2(t, 0); uv[i * 2 + 1] = new Vector2(t, 1);
                colors[i * 2] = colors[i * 2 + 1] = Color.white;
                if (i == segments) continue;
                int a = i * 2, tri = i * 6;
                triangles[tri] = a; triangles[tri+1] = a+1; triangles[tri+2] = a+2;
                triangles[tri+3] = a+1; triangles[tri+4] = a+3; triangles[tri+5] = a+2;
            }
            var mesh = new Mesh { name = "SwordCrescent" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDisable() { if (view != null) view.transform.position = cameraRest; }
        private void OnDestroy()
        {
            if (quad != null) { if (Application.isPlaying) Destroy(quad); else DestroyImmediate(quad); }
            if (arc != null) { if (Application.isPlaying) Destroy(arc); else DestroyImmediate(arc); }
        }
    }
}
