using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CrownAndHearth.Game
{
    public sealed class HistoryChart : UnityEngine.UI.MaskableGraphic, IPointerClickHandler
    {
        double[] _values = Array.Empty<double>();
        double _maximum = 1;
        int _selected;
        public Action<int> SamplePicked;

        public void SetSeries(double[] values, double maximum, int selected)
        {
            _values = values;
            _maximum = maximum;
            _selected = selected;
            SetVerticesDirty();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_values.Length == 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out var position)) return;
            var bounds = rectTransform.rect;
            float fraction = Mathf.InverseLerp(bounds.xMin + 3, bounds.xMax - 3, position.x);
            SamplePicked?.Invoke(Mathf.RoundToInt(fraction * (_values.Length - 1)));
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (_values.Length == 0) return;
            var bounds = rectTransform.rect;
            float x0 = bounds.xMin + 3, x1 = bounds.xMax - 3;
            float y0 = bounds.yMin + 3, y1 = bounds.yMax - 3;
            var guide = new Color(0.88f, 0.81f, 0.66f, 0.22f);
            Line(mesh, new Vector2(x0, y0), new Vector2(x1, y0), 1, guide);
            Line(mesh, new Vector2(x0, y1), new Vector2(x1, y1), 1, guide);

            Vector2 Point(int i) => new Vector2(
                _values.Length == 1 ? x1 : Mathf.Lerp(x0, x1, i / (float)(_values.Length - 1)),
                Mathf.Lerp(y0, y1, (float)(_values[i] / _maximum)));
            var active = Point(_selected);
            Line(mesh, new Vector2(active.x, y0), new Vector2(active.x, y1), 1, guide);
            for (int i = 1; i < _values.Length; i++) Line(mesh, Point(i - 1), Point(i), 2, color);
            Line(mesh, active - new Vector2(3, 0), active + new Vector2(3, 0), 6, color);
        }

        static void Line(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, float thickness, Color color)
        {
            var dir = b - a;
            if (dir.sqrMagnitude <= 0.0001f) return;
            var normal = new Vector2(-dir.y, dir.x).normalized * (thickness * 0.5f);
            int i = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2);
            mesh.AddTriangle(i + 2, i + 3, i);
        }
    }
}
