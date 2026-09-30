using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PlantBreeding.Core;
using PlantBreeding.Garden;
using PlantBreeding.Save;

namespace PlantBreeding.Shop
{
    /// <summary>
    /// Насіннєва капсула (гача, docs/RELEASE-PLAN.md п. 7): за кристали — по одній
    /// насінині трьох РІЗНИХ видів, яких саме — видно лише після відкриття.
    /// Рідкість кожної насінини — за шансами Odds; їх показуємо в Крамниці ДО покупки
    /// (вимога Google Play і App Store для «коробок-сюрпризів»).
    ///
    /// Може випасти вид вище рівня гравця: насіння з капсули дозволяє посадити
    /// його раніше (PlantingScreenController.IsLevelLocked) — у цьому й принада.
    /// Не In-App Purchase: кристали вже куплені, тож капсули немає в ShopCatalog.All
    /// (її не треба заводити в Google Play Console / App Store Connect).
    /// </summary>
    public static class SeedCapsule
    {
        public const int GemPrice = 15;
        public const int PlantsPerCapsule = 3;
        public const int SeedsPerPlant = 1;

        /// <summary>Шанс рідкості для КОЖНОЇ з трьох насінин (сума = 100%).</summary>
        public static readonly (PlantRarity rarity, float chance)[] Odds =
        {
            (PlantRarity.Common, 0.60f),
            (PlantRarity.Rare, 0.28f),
            (PlantRarity.Epic, 0.10f),
            (PlantRarity.Legendary, 0.02f),
        };

        public static string RarityName(PlantRarity rarity) => rarity switch
        {
            PlantRarity.Rare => "Рідкісна",
            PlantRarity.Epic => "Епічна",
            PlantRarity.Legendary => "Легендарна",
            _ => "Звичайна",
        };

        public static bool CanAfford(PlayerData data) => data != null && data.gems >= GemPrice;

        /// <summary>
        /// Списує кристали й видає насіння. null — бракує кристалів (нічого не списано).
        /// </summary>
        public static List<PlantData> TryOpen()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.SpendGems(GemPrice)) return null;

            var picks = Roll();
            foreach (var plant in picks) gm.AddSeeds(plant.plantId, SeedsPerPlant);
            SaveSystem.Save(gm.playerData);
            ShopService.NotifyChanged();
            return picks;
        }

        /// <summary>
        /// Три різні види: для кожного — рідкість за Odds, потім випадковий вид цієї
        /// рідкості, якого ще немає в капсулі. Якщо всі види якоїсь рідкості вже
        /// випали (легендарних лише 2), шанси решти рідкостей перераховуються.
        /// </summary>
        private static List<PlantData> Roll()
        {
            var picks = new List<PlantData>();
            var pool = PlantCatalog.All.ToList();
            while (picks.Count < PlantsPerCapsule)
            {
                var available = Odds.Where(o => pool.Any(p => p.rarity == o.rarity)).ToList();
                if (available.Count == 0) break;

                float roll = Random.value * available.Sum(o => o.chance);
                var rarity = available[available.Count - 1].rarity;
                foreach (var o in available)
                {
                    if (roll < o.chance) { rarity = o.rarity; break; }
                    roll -= o.chance;
                }

                var candidates = pool.Where(p => p.rarity == rarity).ToList();
                var pick = candidates[Random.Range(0, candidates.Count)];
                picks.Add(pick);
                pool.Remove(pick);
            }
            return picks;
        }
    }
}
