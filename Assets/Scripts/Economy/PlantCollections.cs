using System.Collections.Generic;
using System.Linq;
using PlantBreeding.Core;
using PlantBreeding.Garden;
using PlantBreeding.Save;

namespace PlantBreeding.Economy
{
    /// <summary>
    /// 5 тематичних колекцій по 4 кімнатні рослини (усі 20 видів). Колекція
    /// зібрана, коли кожен її вид хоч раз вирощено (PlayerData.discoveredPlantIds,
    /// реєстр Дендрарію). За кожну — 1 кристал, один раз. Це ЄДИНЕ ігрове
    /// джерело кристалів крім стартових 3 і Крамниці (docs/ECONOMY.md):
    /// разом 5 кристалів за ~2 тижні гри.
    /// </summary>
    public static class PlantCollections
    {
        public const int GemReward = 1;

        public readonly struct CollectionDef
        {
            public readonly string id;
            public readonly string name;
            public readonly string[] plantIds;

            public CollectionDef(string id, string name, params string[] plantIds)
            {
                this.id = id;
                this.name = name;
                this.plantIds = plantIds;
            }
        }

        public static readonly CollectionDef[] All =
        {
            new CollectionDef("succulents", "Сукуленти", "cactus", "aloe", "snake_plant", "zz_plant"),
            new CollectionDef("windowsill", "Квіти на підвіконні", "violet", "peace_lily", "begonia", "geranium"),
            new CollectionDef("green_leaves", "Зелене листя", "spider_plant", "pothos", "ficus", "calathea"),
            new CollectionDef("tropics", "Тропіки вдома", "anthurium", "monstera", "hibiscus", "fiddle_leaf_fig"),
            new CollectionDef("rare_beauties", "Рідкісні красуні", "azalea", "gardenia", "strelitzia", "orchid"),
        };

        public static int Progress(PlayerData data, CollectionDef c) =>
            c.plantIds.Count(id => data.discoveredPlantIds.Contains(id));

        public static IEnumerable<CollectionDef> ContainingPlant(string plantId) =>
            All.Where(c => c.plantIds.Contains(plantId));

        /// <summary>
        /// Викликається, коли вид відкрито вперше (GameManager.RecordPlantDiscovered).
        /// Показує прогрес колекції і видає кристал за повністю зібрану.
        /// </summary>
        public static void OnPlantDiscovered(string plantId)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var data = gm.playerData;

            foreach (var c in ContainingPlant(plantId))
            {
                int have = Progress(data, c);
                if (have < c.plantIds.Length)
                {
                    GameEvents.RaiseToast($"Колекція «{c.name}»: {have}/{c.plantIds.Length}");
                    continue;
                }
                if (data.completedCollectionIds.Contains(c.id)) continue;

                data.completedCollectionIds.Add(c.id);
                gm.AddGems(GemReward);
                SaveSystem.Save(data);
                GameEvents.RaiseToast($"Колекцію «{c.name}» зібрано! +{GemReward} кристал");
            }
        }
    }
}
