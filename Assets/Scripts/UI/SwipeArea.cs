using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Горизонтальний свайп по області (екран догляду: між рослинами).
    /// Swiped(+1) — свайп ліворуч (наступна), Swiped(−1) — праворуч (попередня).
    /// Потрібен Graphic з raycastTarget на тому ж об'єкті, щоб ловити дотик.
    /// </summary>
    public class SwipeArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>Мінімальна довжина свайпу в пікселях екрана (частка ширини).</summary>
        public float minDistanceFraction = 0.12f;

        public event Action<int> Swiped;

        private Vector2 _start;

        public void OnBeginDrag(PointerEventData eventData) => _start = eventData.position;

        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            var delta = eventData.position - _start;
            if (Mathf.Abs(delta.x) < Screen.width * minDistanceFraction) return;
            if (Mathf.Abs(delta.x) < Mathf.Abs(delta.y) * 1.5f) return; // вертикальний жест — не свайп
            Swiped?.Invoke(delta.x < 0 ? 1 : -1);
        }
    }
}
