using UnityEngine;
using UnityEngine.UI;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Підганяє розмір клітинок GridLayoutGroup так, щоб columns × rows
    /// клітинок рівно вміщались у батьківську область (viewport скролу).
    /// Для сітки грядок «Мій сад» (2×4): висота області залежить від екрана
    /// і safe area (SafeAreaFitter стискає скрол), тому рахується в рантаймі.
    /// Клітинка не нижча за minCellHeight — на дуже низьких екранах сітка
    /// просто прокручується.
    /// rows = 0 — лише ширина: колонки на всю ширину самої сітки, висота клітинки
    /// fixedCellHeight (Дендрарій, розгорнута колекція).
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public class GridCellFitter : MonoBehaviour
    {
        public int columns = 2;
        public int rows = 4;
        public float minCellHeight = 76f;
        public float fixedCellHeight = 0f;

        private GridLayoutGroup _grid;
        private RectTransform _area;
        private Vector2 _appliedSize;

        private void Awake()
        {
            _grid = GetComponent<GridLayoutGroup>();
        }

        private void LateUpdate()
        {
            // Лінива ініціалізація: при AddComponent поля (rows) задаються вже після Awake.
            if (_area == null) _area = rows > 0 ? transform.parent as RectTransform : (RectTransform)transform;
            if (_area == null) return;
            var size = _area.rect.size;
            if (size == _appliedSize || size.x <= 0 || (rows > 0 && size.y <= 0)) return;
            _appliedSize = size;

            var pad = _grid.padding;
            float w = (size.x - pad.horizontal - _grid.spacing.x * (columns - 1)) / columns;
            float h = rows > 0 ? (size.y - pad.vertical - _grid.spacing.y * (rows - 1)) / rows : fixedCellHeight;
            _grid.cellSize = new Vector2(Mathf.Floor(w), Mathf.Floor(rows > 0 ? Mathf.Max(minCellHeight, h) : h));
        }
    }
}
