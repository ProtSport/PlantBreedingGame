using System.IO;
using UnityEditor;
using UnityEngine;
using PlantBreeding.Garden;
using static PlantBreeding.EditorTools.UIBuilderKit;

namespace PlantBreeding.EditorTools
{
    /// <summary>
    /// Створює стартовий контент гри (20 кімнатних рослин + 7 горщиків) як
    /// ScriptableObject-асети в Assets/Resources, щоб Resources.Load
    /// працював і в збірках, не лише в редакторі.
    /// Меню: PlantBreeding → Створити стартовий контент.
    /// Ідемпотентно: повторний запуск оновлює вже створені асети, не дублює.
    /// </summary>
    public static class ContentSeeder
    {
        const string PlantsDir = "Assets/Resources/Plants";
        const string PotsDir = "Assets/Resources/Pots";
        const string PlantArtDir = "Assets/Art/Plants";

        [MenuItem("PlantBreeding/Створити стартовий контент")]
        public static void Seed()
        {
            EnsureGenerated(); // потрібні спрайти рослин з Assets/Art/SVG (уже імпортовані)
            Directory.CreateDirectory(PlantsDir);
            Directory.CreateDirectory(PotsDir);

            SeedPlants();
            SeedPots();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlantBreeding] Стартовий контент створено: 20 кімнатних рослин, 7 горщиків.");
        }

        // Каталог — 20 кімнатних рослин, таблиця з docs/ECONOMY.md (розділ «Види»).
        // Продаж = насіння × 1.5 (короткі) … × 1.3 (довгі): вирощування прибуткове,
        // але монети не накопичуються понад те, що з'їдають Лабораторія і горщики.
        // Відкриваються поступово: 2 на рівні 1, далі по одній–дві на рівень до 18.
        // Короткі рослини = активна гра, довгі = «посадив увечері — зібрав вранці».
        // Полив раз на чверть часу росту → до 3 поливів за бонус до ціни.
        static readonly (string id, string name, int lvl, int seed, float growMin, int sell, int xp, PlantRarity rarity, string tint)[] Plants =
        {
            ("violet",          "Фіалка",            1,   8,    3,  12,   8, PlantRarity.Common,    "#E6D2FF"),
            ("cactus",          "Кактус",            1,  12,   10,  18,  12, PlantRarity.Common,    "#D8F0B8"),
            ("aloe",            "Алое",              2,  18,   20,  27,  18, PlantRarity.Common,    "#CFEFD6"),
            ("spider_plant",    "Хлорофітум",        2,  25,   30,  37,  24, PlantRarity.Common,    "#EAF7C8"),
            ("snake_plant",     "Сансев'єрія",       3,  32,   45,  47,  30, PlantRarity.Common,    "#D6E8B0"),
            ("zz_plant",        "Заміокулькас",      4,  40,   60,  58,  36, PlantRarity.Common,    "#BFE3A8"),
            ("pothos",          "Епіпремнум",        5,  50,   90,  72,  45, PlantRarity.Common,    "#E4F5A8"),
            ("peace_lily",      "Спатифілум",        6,  60,  120,  86,  55, PlantRarity.Rare,      "#F4FAF0"),
            ("ficus",           "Фікус Бенджаміна",  7,  70,  150,  99,  62, PlantRarity.Rare,      "#CDE6B4"),
            ("begonia",         "Бегонія",           8,  80,  180, 112,  70, PlantRarity.Rare,      "#FFC9C2"),
            ("calathea",        "Калатея",           9,  95,  240, 132,  82, PlantRarity.Rare,      "#C8E0C8"),
            ("geranium",        "Пеларгонія",       10, 110,  300, 152,  95, PlantRarity.Rare,      "#FFB8B0"),
            ("anthurium",       "Антуріум",         11, 125,  360, 172, 105, PlantRarity.Rare,      "#FFB0B8"),
            ("monstera",        "Монстера",         12, 140,  420, 191, 115, PlantRarity.Epic,      "#B8E0A0"),
            ("hibiscus",        "Гібіскус",         13, 160,  480, 216, 125, PlantRarity.Epic,      "#FFC0CC"),
            ("azalea",          "Азалія",           14, 185,  600, 248, 140, PlantRarity.Epic,      "#FFC8E4"),
            ("fiddle_leaf_fig", "Фікус ліровидний", 15, 210,  720, 280, 155, PlantRarity.Epic,      "#C4E4A8"),
            ("gardenia",        "Гарденія",         16, 240,  840, 317, 170, PlantRarity.Epic,      "#FFF8E8"),
            ("strelitzia",      "Стреліція",        17, 270,  960, 354, 185, PlantRarity.Legendary, "#FFD8A0"),
            ("orchid",          "Орхідея",          18, 320, 1200, 416, 210, PlantRarity.Legendary, "#F0D0FF"),
        };

        static void SeedPlants()
        {
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var p in Plants)
            {
                SeedPlant(p.id, p.name, p.lvl, p.seed, p.growMin * 60f, p.sell, p.xp, p.rarity, p.tint);
                ids.Add(p.id);
            }

            // Прибрати види з попередніх версій каталогу (троянда, ромашка…),
            // щоб у грі лишались лише кімнатні рослини.
            foreach (var guid in AssetDatabase.FindAssets("t:PlantData", new[] { PlantsDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var plant = AssetDatabase.LoadAssetAtPath<PlantData>(path);
                if (plant != null && !ids.Contains(plant.plantId))
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"[PlantBreeding] Видалено застарілий вид: {path}");
                }
            }
        }

        static void SeedPlant(string id, string name, int unlockLevel, int seedCost, float growSeconds,
            int sellPrice, int xp, PlantRarity rarity, string tint)
        {
            string path = $"{PlantsDir}/{id}.asset";
            var plant = AssetDatabase.LoadAssetAtPath<PlantData>(path);
            bool isNew = plant == null;
            if (isNew) plant = ScriptableObject.CreateInstance<PlantData>();

            plant.plantId = id;
            plant.displayName = name;
            plant.growTimeSeconds = growSeconds;
            // Короткі (<10 хв) не просять води — перший цикл новачка без відволікань.
            plant.waterDepleteSeconds = growSeconds < 600f ? growSeconds * 2f : growSeconds / 4f;
            plant.rarity = rarity;
            plant.seedCost = seedCost;
            plant.sellPrice = sellPrice;
            plant.xpReward = xp;
            plant.unlockLevel = unlockLevel;
            // Власний арт (Assets/Art/Plants/<id>/stage1..4.svg — з папки «Рослини»);
            // поки його нема — спільні заготовки, пофарбовані тоном виду.
            string artDir = $"{PlantArtDir}/{id}";
            bool hasArt = AssetDatabase.LoadAssetAtPath<Sprite>($"{artDir}/stage4.svg") != null;
            if (hasArt)
            {
                plant.tint = Color.white;
                plant.sprite25 = AssetDatabase.LoadAssetAtPath<Sprite>($"{artDir}/stage1.svg");
                plant.sprite50 = AssetDatabase.LoadAssetAtPath<Sprite>($"{artDir}/stage2.svg");
                plant.sprite75 = AssetDatabase.LoadAssetAtPath<Sprite>($"{artDir}/stage3.svg");
                plant.sprite100 = AssetDatabase.LoadAssetAtPath<Sprite>($"{artDir}/stage4.svg");
                plant.plantOnly25 = AssetDatabase.LoadAssetAtPath<Sprite>($"{artDir}/stage1-plant.svg");
                plant.plantOnly50 = AssetDatabase.LoadAssetAtPath<Sprite>($"{artDir}/stage2-plant.svg");
                plant.plantOnly75 = AssetDatabase.LoadAssetAtPath<Sprite>($"{artDir}/stage3-plant.svg");
                plant.plantOnly100 = AssetDatabase.LoadAssetAtPath<Sprite>($"{artDir}/stage4-plant.svg");
            }
            else
            {
                Debug.LogWarning($"[PlantBreeding] Немає арту для «{name}» ({artDir}) — використовую заготовку.");
                plant.tint = Hex(tint);
                plant.sprite25 = LoadSprite("plant-stage-2", SvgDir);
                plant.sprite50 = LoadSprite("plant-stage-3", SvgDir);
                plant.sprite75 = LoadSprite("plant-stage-4", SvgDir);
                plant.sprite100 = LoadSprite("plant-stage-5", SvgDir);
                plant.plantOnly25 = plant.plantOnly50 = plant.plantOnly75 = plant.plantOnly100 = null;
            }

            if (isNew) AssetDatabase.CreateAsset(plant, path);
            else EditorUtility.SetDirty(plant);
        }

        // 7 горщиків (docs/ECONOMY.md, «Горщики»): кожен, крім стартового, трохи
        // прискорює ріст + дає другу перевагу. Разом ~4 800 монет — розраховано
        // на запас монет до 18-го рівня разом із Лабораторією.
        static void SeedPots()
        {
            var ids = new System.Collections.Generic.HashSet<string>();
            //      id            назва              рідкість            ріст   продаж  полив  стійк.  підпис                         кольори (верх/сер/низ/обідок/контур)                      прем.  ціна
            ids.Add(SeedPot("plastic",    "Пластиковий",     PotRarity.Common,     0f,    0f,     0f,    0f,    "без бонусу",
                "#A6AFA3", "#848D81", "#5F665D", "#98A095", "#3E443C", false, 0));
            ids.Add(SeedPot("terracotta", "Теракотовий",     PotRarity.Common,    -0.05f, 0f,     0.25f, 0f,    "−5% часу росту\nполив рідше на 25%",
                "#E79A65", "#CE7850", "#9C4B2C", "#D9885E", "#8A4526", false, 120));
            ids.Add(SeedPot("ceramic",    "Керамічний",      PotRarity.Rare,      -0.06f, 0.05f,  0f,    0f,    "−6% часу росту\n+5% ціна продажу",
                "#A9CDE8", "#6E9EC8", "#3F6E99", "#8DB6D9", "#2E5578", false, 300));
            ids.Add(SeedPot("wicker",     "Плетене кашпо",   PotRarity.Rare,      -0.08f, 0f,     0f,    0.25f, "−8% часу росту\n−25% хвороб",
                "#E3C48E", "#C49A5E", "#8E6A38", "#D6B47C", "#6E4E26", false, 550));
            ids.Add(SeedPot("concrete",   "Бетонний",        PotRarity.Epic,      -0.10f, 0f,     0.5f,  0f,    "−10% часу росту\nполив рідше на 50%",
                "#C4C4BF", "#9C9C97", "#6E6E6A", "#B0B0AB", "#4E4E4A", false, 850));
            ids.Add(SeedPot("brass",      "Латунний",        PotRarity.Epic,      -0.12f, 0.10f,  0f,    0f,    "−12% часу росту\n+10% ціна продажу",
                "#F3DEA6", "#D3AC64", "#8A6430", "#D9B36A", "#7A5628", false, 1200));
            ids.Add(SeedPot("marble",     "Мармуровий",      PotRarity.Legendary, -0.15f, 0.10f,  0f,    0.30f, "−15% часу росту\n+10% ціна · −30% хвороб",
                "#F7F5F1", "#E4E0D9", "#C2BCB1", "#EEEAE3", "#C2BCB1", true, 1800));

            // Прибрати горщики з попередніх версій каталогу (напр. «Матовий чорний»).
            foreach (var guid in AssetDatabase.FindAssets("t:PotData", new[] { PotsDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var pot = AssetDatabase.LoadAssetAtPath<PotData>(path);
                if (pot != null && !ids.Contains(pot.potId))
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"[PlantBreeding] Видалено застарілий горщик: {path}");
                }
            }
        }

        static string SeedPot(string id, string name, PotRarity rarity, float growMod, float sellBonus, float waterBonus,
            float resistance, string effect, string top, string mid, string bottom, string rim, string stroke,
            bool premium, int unlockCost)
        {
            string path = $"{PotsDir}/{id}.asset";
            var pot = AssetDatabase.LoadAssetAtPath<PotData>(path);
            bool isNew = pot == null;
            if (isNew) pot = ScriptableObject.CreateInstance<PotData>();

            pot.potId = id;
            pot.displayName = name;
            pot.rarity = rarity;
            pot.growTimeModifier = growMod;
            pot.sellPriceBonus = sellBonus;
            pot.waterIntervalBonus = waterBonus;
            pot.ailmentResistance = resistance;
            pot.effectLabel = effect;
            pot.colorTop = Hex(top);
            pot.colorMid = Hex(mid);
            pot.colorBottom = Hex(bottom);
            pot.colorRim = Hex(rim);
            pot.colorStroke = Hex(stroke);
            pot.premiumVisual = premium;
            pot.unlockCost = unlockCost;

            if (isNew) AssetDatabase.CreateAsset(pot, path);
            else EditorUtility.SetDirty(pot);
            return id;
        }
    }
}
