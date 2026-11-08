using UnityEngine;

namespace TowerTrials
{
    // Small, resolution-independent UI geometry, authored here rather than using text glyphs as icons.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GameUiShape : UnityEngine.UI.MaskableGraphic
    {
        public enum Shape { Plate, Restart, Forward }
        [SerializeField] private Shape shape;
        [SerializeField] private float corner = 8;

        public void Configure(Shape value, Color tint, float cornerSize = 8)
        {
            shape = value;
            color = tint;
            corner = cornerSize;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (shape == Shape.Plate)
            {
                float cut = Mathf.Min(corner, Mathf.Min(rect.width, rect.height) * 0.25f);
                AddVertex(mesh, rect.center);
                AddVertex(mesh, new Vector2(rect.xMin + cut, rect.yMin));
                AddVertex(mesh, new Vector2(rect.xMax - cut, rect.yMin));
                AddVertex(mesh, new Vector2(rect.xMax, rect.yMin + cut));
                AddVertex(mesh, new Vector2(rect.xMax, rect.yMax - cut));
                AddVertex(mesh, new Vector2(rect.xMax - cut, rect.yMax));
                AddVertex(mesh, new Vector2(rect.xMin + cut, rect.yMax));
                AddVertex(mesh, new Vector2(rect.xMin, rect.yMax - cut));
                AddVertex(mesh, new Vector2(rect.xMin, rect.yMin + cut));
                for (int i = 1; i <= 8; i++) mesh.AddTriangle(0, i, i == 8 ? 1 : i + 1);
                return;
            }

            float size = Mathf.Min(rect.width, rect.height);
            float stroke = size * 0.075f;
            Vector2 center = rect.center;
            if (shape == Shape.Restart)
            {
                const int steps = 28;
                float radius = size * 0.33f;
                for (int i = 0; i < steps; i++)
                    Segment(mesh, center + Circle(55 + i * 270f / steps) * radius,
                        center + Circle(55 + (i + 1) * 270f / steps) * radius, stroke);
                Vector2 tip = center + Circle(325) * radius;
                Vector2 tangent = Circle(325 + 90);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                Segment(mesh, tip, tip - tangent * size * 0.23f + normal * size * 0.17f, stroke);
                Segment(mesh, tip, tip - tangent * size * 0.23f - normal * size * 0.17f, stroke);
            }
            else
            {
                Vector2 tip = center + new Vector2(size * 0.3f, 0);
                Segment(mesh, center - new Vector2(size * 0.3f, 0), tip, stroke);
                Segment(mesh, tip, center + new Vector2(size * 0.05f, size * 0.25f), stroke);
                Segment(mesh, tip, center + new Vector2(size * 0.05f, -size * 0.25f), stroke);
            }
        }

        private static Vector2 Circle(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private void AddVertex(UnityEngine.UI.VertexHelper mesh, Vector2 position)
        {
            var vertex = UnityEngine.UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            mesh.AddVert(vertex);
        }

        private void Segment(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, float width)
        {
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);
            int index = mesh.currentVertCount;
            AddVertex(mesh, a - normal);
            AddVertex(mesh, a + normal);
            AddVertex(mesh, b + normal);
            AddVertex(mesh, b - normal);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
