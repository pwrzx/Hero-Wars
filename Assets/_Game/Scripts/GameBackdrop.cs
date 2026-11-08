using UnityEngine;
using UnityEngine.Rendering;

namespace TowerTrials
{
    [DefaultExecutionOrder(-40)]
    public sealed class GameBackdrop : MonoBehaviour
    {
        private Camera view;
        private Transform painting;
        private float imageAspect;

        public void Configure(Camera camera)
        {
            view = camera;
            Material material = Resources.Load<Material>("Materials/FX_Backdrop");
            if (material == null || material.mainTexture == null)
                throw new System.InvalidOperationException("Prepare the painted backdrop material with PrototypeSetup.PreparePolishAssets.");
            imageAspect = (float)material.mainTexture.width / material.mainTexture.height;
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "AncientChasmPainting";
            quad.transform.SetParent(transform, false);
            Collider collider = quad.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            painting = quad.transform;
            Fit();
        }

        private void LateUpdate() => Fit();

        private void Fit()
        {
            if (view == null || painting == null) return;
            float height = view.orthographicSize * 2;
            float width = height * view.aspect;
            if (imageAspect > view.aspect) width = height * imageAspect;
            else height = width / imageAspect;
            float drift = Application.isPlaying ? Mathf.Sin(Time.time * 0.1f) * 0.055f : 0;
            painting.position = view.transform.TransformPoint(new Vector3(drift, 0, 45));
            painting.rotation = view.transform.rotation;
            painting.localScale = new Vector3(width * 1.025f, height * 1.025f, 1);
        }
    }
}
