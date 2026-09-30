using UnityEngine;
using PlantBreeding.Collections;
using PlantBreeding.Core;
using PlantBreeding.Lab;

namespace PlantBreeding.Garden
{
    public readonly struct ForecastResult
    {
        public readonly float growTimeSeconds;
        /// <summary>Очікувана ціна продажу (горщик + Лабораторія, без бонусу поливу).</summary>
        public readonly int sellPrice;

        public ForecastResult(float growTimeSeconds, int sellPrice)
        {
            this.growTimeSeconds = growTimeSeconds;
            this.sellPrice = sellPrice;
        }

        public string GrowLabel
        {
            get
            {
                int totalMinutes = Mathf.Max(0, Mathf.RoundToInt(growTimeSeconds / 60f));
                return $"{totalMinutes / 60}:{(totalMinutes % 60):D2}";
            }
        }
    }

    /// <summary>
    /// Прогноз результату посадки: час росту (рослина + горщик + добриво +
    /// Лабораторія) і ціна продажу (рослина + бонус горщика + Лабораторія +
    /// бонус колекції Дендрарію + підписка «Клуб садівника»).
    /// Мутацій/схрещування на цьому етапі немає.
    /// </summary>
    public static class PlantingForecast
    {
        public static ForecastResult Compute(PlantData plant, PotData pot, StarterBoostKind boostKind)
        {
            var boost = StarterBoostCatalog.Get(boostKind);
            float labGrowModifier = GameManager.Instance != null
                ? LabResearchService.GetSpeedGrowModifier(GameManager.Instance.playerData)
                : 0f;
            float growModifier = (pot != null ? pot.growTimeModifier : 0f) + boost.growTimeModifier + labGrowModifier;
            float growTime = Mathf.Max(1f, plant.growTimeSeconds * (1f + growModifier));

            return new ForecastResult(growTime, SellPriceWithBonuses(plant, pot));
        }

        /// <summary>Ціна продажу з бонусом горщика, Лабораторії і колекцій (полив додається при зборі).</summary>
        public static int SellPriceWithBonuses(PlantData plant, PotData pot)
        {
            float lab = GameManager.Instance != null
                ? LabResearchService.GetYieldPriceBonus(GameManager.Instance.playerData)
                : 0f;
            float potBonus = pot != null ? pot.sellPriceBonus : 0f;
            float collection = CollectionService.SellPriceBonus(plant.plantId);
            float club = Shop.ShopService.ClubSellBonus; // «Клуб садівника»: +10%
            return Mathf.RoundToInt(PlantEconomy.SellPrice(plant) * (1f + lab + potBonus + collection + club));
        }
    }
}
