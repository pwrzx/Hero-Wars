using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerTrials
{
    public sealed class HeroPreview : MonoBehaviour, IDragHandler
    {
        private const int PreviewLayer = 30;
        private UnityEngine.UI.RawImage image;
        private Camera view;
        private Transform stage, turntable;
        private RenderTexture target;
        private GameObject hero;
        private float yaw = 158;
        public GameObject Hero => hero;

        public void Configure(UnityEngine.UI.RawImage display, HeroCenterCatalog catalog, HeroProfile profile, Transform owner)
        {
            image = display;
            stage = new GameObject("HeroPreviewStage").transform;
            stage.SetParent(owner, false);
            stage.localPosition = new Vector3(1000, 0, 0);
            view = new GameObject("HeroPortraitCamera", typeof(Camera)).GetComponent<Camera>();
            view.transform.SetParent(stage, false);
            view.transform.localPosition = new Vector3(0, 2.1f, -7);
            view.transform.LookAt(stage.position + Vector3.up * 1.72f);
            view.orthographic = true;
            view.orthographicSize = 2.06f;
            view.nearClipPlane = 0.1f; view.farClipPlane = 24;
            view.cullingMask = 1 << PreviewLayer;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Color.clear;
            view.allowHDR = false;
            turntable = new GameObject("HeroTurntable").transform;
            turntable.SetParent(stage, false);
            KeyLight("PortraitKey", new Vector3(30, -35, 0), new Color(1, 0.89f, 0.73f), 2.4f);
            KeyLight("PortraitRim", new Vector3(20, 150, 0), new Color(0.30f, 0.72f, 1), 2.1f);
            KeyLight("PortraitFill", new Vector3(5, 30, 0), new Color(0.65f, 0.79f, 1), 0.8f);
            Pedestal("LowerPlinth", new Vector3(0, -0.19f, 0), new Vector3(1.18f, 0.12f, 1.18f), new Color32(21, 43, 63, 255));
            Pedestal("BrassRim", new Vector3(0, -0.06f, 0), new Vector3(1.12f, 0.025f, 1.12f), new Color32(208, 163, 72, 255));
            Pedestal("DisplayPlinth", new Vector3(0, -0.015f, 0), new Vector3(1.06f, 0.025f, 1.06f), new Color32(37, 65, 84, 255));
            Show(catalog, profile);
            SetLayer(stage.gameObject);
            Resize();
        }

        public void Show(HeroCenterCatalog catalog, HeroProfile profile)
        {
            if (hero != null)
            {
                hero.SetActive(false);
                if (Application.isPlaying) Destroy(hero); else DestroyImmediate(hero);
            }
            hero = HeroAppearance.Create(catalog, profile, turntable, 3.15f);
            SetLayer(turntable.gameObject);
            turntable.localRotation = Quaternion.Euler(0, yaw, 0);
            if (!Application.isPlaying && view != null && target != null) view.Render();
        }

        public void OnDrag(PointerEventData data)
        {
            yaw -= data.delta.x * 0.3f;
            if (turntable != null) turntable.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        private void LateUpdate() => Resize();
        private void OnEnable() { if (stage != null) stage.gameObject.SetActive(true); }
        private void OnDisable() { if (stage != null) stage.gameObject.SetActive(false); }

        public void Resize()
        {
            if (image == null || view == null) return;
            Canvas canvas = image.canvas;
            float scale = canvas == null ? 1 : canvas.scaleFactor;
            Vector2 size = image.rectTransform.rect.size * scale;
            float limit = Mathf.Min(1, 1024f / Mathf.Max(1, Mathf.Max(size.x, size.y)));
            int width = Mathf.Max(64, Mathf.RoundToInt(size.x * limit));
            int height = Mathf.Max(64, Mathf.RoundToInt(size.y * limit));
            if (target != null && target.width == width && target.height == height) return;
            Release();
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "HeroPortrait_FullDisplayResolution", antiAliasing = 4 };
            target.Create();
            view.targetTexture = target;
            image.texture = target;
            view.orthographicSize = Mathf.Max(2.06f, 1.12f / ((float)width / height));
            view.Render();
        }

        private void KeyLight(string name, Vector3 rotation, Color color, float intensity)
        {
            var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(stage, false);
            light.transform.localRotation = Quaternion.Euler(rotation);
            light.type = LightType.Directional; light.color = color; light.intensity = intensity;
            light.cullingMask = 1 << PreviewLayer;
        }

        private void Pedestal(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name; cylinder.transform.SetParent(stage, false);
            cylinder.transform.localPosition = position; cylinder.transform.localScale = scale;
            Collider collider = cylinder.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void SetLayer(GameObject root)
        { foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer; }

        private void Release()
        {
            if (target == null) return;
            if (view != null) view.targetTexture = null;
            target.Release();
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
            target = null;
        }

        private void OnDestroy()
        {
            Release();
            if (stage == null) return;
            foreach (Renderer renderer in stage.GetComponentsInChildren<Renderer>())
                if (renderer.name.EndsWith("Plinth") || renderer.name == "BrassRim")
                {
                    Material material = renderer.sharedMaterial;
                    if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
                }
            if (Application.isPlaying) Destroy(stage.gameObject); else DestroyImmediate(stage.gameObject);
        }
    }
}
