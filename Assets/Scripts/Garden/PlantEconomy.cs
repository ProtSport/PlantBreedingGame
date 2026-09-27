using UnityEngine;

namespace PlantBreeding.Garden
{
    /// <summary>
    /// Ціна продажу вирощеної рослини. Кожен вид має власну ціну
    /// (PlantData.sellPrice), яка приблизно вдвічі-втричі більша за ціну
    /// насіння: вирощування — основний заробіток (docs/ECONOMY.md; це свідома
    /// зміна правила GDD 10.3 «фарм не прибутковий», поки нема схрещування).
    /// Базова ціна рідкості — запасний варіант для видів без sellPrice.
    /// Бонуси догляду/лабораторії накладає EconomyService.GrantHarvest.
    /// </summary>
    public static class PlantEconomy
    {
        public static int SellPrice(PlantData plant) =>
            plant.sellPrice > 0 ? plant.sellPrice : SellPrice(plant.rarity);

        public static int HarvestXp(PlantData plant) => Mathf.Max(1, plant.xpReward);

        public static int SellPrice(PlantRarity rarity) => rarity switch
        {
            PlantRarity.Common => 12,
            PlantRarity.Rare => 32,
            PlantRarity.Epic => 80,
            PlantRarity.Legendary => 225,
            _ => 0
        };
    }
}
