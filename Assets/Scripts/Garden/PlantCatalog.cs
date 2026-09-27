using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PlantBreeding.Garden
{
    /// <summary>
    /// Кеш усіх видів з Assets/Resources/Plants (ім'я асета = plantId),
    /// відсортованих за рівнем відкриття — порядок «кар'єри» гравця.
    /// </summary>
    public static class PlantCatalog
    {
        private static List<PlantData> _all;

        public static IReadOnlyList<PlantData> All
        {
            get
            {
                if (_all == null)
                {
                    _all = Resources.LoadAll<PlantData>("Plants")
                        .OrderBy(p => p.unlockLevel)
                        .ThenBy(p => p.seedCost)
                        .ToList();
                }
                return _all;
            }
        }

        public static PlantData Get(string plantId)
        {
            if (string.IsNullOrEmpty(plantId)) return null;
            foreach (var p in All)
                if (p.plantId == plantId) return p;
            return null;
        }
    }
}
