using System.Collections.Generic;

namespace PlantBreeding.Collections
{
    /// <summary>Розділ Дендрарію (чіпи-фільтри).</summary>
    public enum CollectionGroup
    {
        Theme,   // тематичні: відкрити 4 види (перший урожай)
        Mastery  // майстерність: довгі цілі після рівня 18
    }

    /// <summary>Що саме заповнює слот колекції (прогрес рахує CollectionService).</summary>
    public enum CollectionGoal
    {
        DiscoverPlant,   // слот = plantId, заповнений після першого врожаю виду
        PerfectCare,     // слот = plantId, вирощений без пропущеного поливу
        HarvestPlant,    // слот = plantId, зібраний VeteranHarvests разів
        CureAilment,     // слот = AilmentKind (число), хоч раз вилікувано
        OwnPot,          // слот = potId, горщик куплено
        CompleteCollection // слот = id іншої колекції, вона зібрана
    }

    /// <summary>Постійний бонус, що діє після отримання нагороди колекції.</summary>
    public enum CollectionBonusKind
    {
        None,
        WaterInterval,   // види колекції просять води рідше (+частка до інтервалу)
        SellPrice,       // види колекції дорожчі при продажу
        AilmentResist,   // види колекції рідше хворіють
        Xp,              // види колекції дають більше XP
        CarePerWatering  // кожен вчасний полив дає більший бонус (усі види)
    }

    /// <summary>
    /// Каталог колекцій Дендрарію (docs/ECONOMY.md, розділ «Колекції»):
    /// 6 тематичних (5 наборів по 4 кімнатні рослини + мета «Ботанік») і
    /// 4 колекції майстерності. Лише дані — прогрес, нагороди й бонуси рахує
    /// CollectionService від реального стану гравця (PlayerData).
    ///
    /// Кристали — лише 7 за всю гру (5 тематичних + «Ідеальний догляд» +
    /// «Садівник-ветеран»), решта нагород — монети, предмети профілю,
    /// ексклюзивний горщик і постійні бонуси.
    /// </summary>
    public static class CollectionCatalog
    {
        /// <summary>Скільки разів треба зібрати вид для слота «Садівник-ветеран».</summary>
        public const int VeteranHarvests = 10;

        /// <summary>Чіпи-фільтри Дендрарію: 0 — усі, далі по CollectionGroup.</summary>
        public static readonly string[] Chips = { "Усі", "Тематичні", "Майстерність" };

        private static readonly CollectionDef[] Catalog =
        {
            // ── Тематичні ───────────────────────────────────────────────────
            new CollectionDef("succulents", "Сукуленти", CollectionGroup.Theme, CollectionGoal.DiscoverPlant, "green",
                "Невибагливі рослини, що запасають воду в листі.",
                "Виростай кожен вид хоч раз — слот заповнюється після першого врожаю.",
                new[] { "cactus", "aloe", "snake_plant", "zz_plant" },
                new CollectionReward { gems = 1, coins = 100, avatarId = "cactus",
                    bonus = CollectionBonusKind.WaterInterval, bonusValue = 0.25f }),

            new CollectionDef("windowsill", "Квіти на підвіконні", CollectionGroup.Theme, CollectionGoal.DiscoverPlant, "rose",
                "Квітучі рослини для світлого підвіконня.",
                "Виростай кожен вид хоч раз — слот заповнюється після першого врожаю.",
                new[] { "violet", "peace_lily", "begonia", "geranium" },
                new CollectionReward { gems = 1, coins = 250, frameId = "floral",
                    bonus = CollectionBonusKind.SellPrice, bonusValue = 0.05f }),

            new CollectionDef("green_leaves", "Зелене листя", CollectionGroup.Theme, CollectionGoal.DiscoverPlant, "green",
                "Декоративно-листяні рослини — зелень на весь рік.",
                "Виростай кожен вид хоч раз — слот заповнюється після першого врожаю.",
                new[] { "spider_plant", "pothos", "ficus", "calathea" },
                new CollectionReward { gems = 1, coins = 250, avatarId = "leaf",
                    // Без хвороб (STG 1) «рідше хворіють» нічого не дає — замість нього +10% XP.
                    bonus = PlantBreeding.Garden.PlantAilments.Enabled ? CollectionBonusKind.AilmentResist : CollectionBonusKind.Xp,
                    bonusValue = PlantBreeding.Garden.PlantAilments.Enabled ? 0.20f : 0.10f }),

            new CollectionDef("tropics", "Тропіки вдома", CollectionGroup.Theme, CollectionGoal.DiscoverPlant, "violet",
                "Великі тропічні рослини з яскравим листям і квітами.",
                "Виростай кожен вид хоч раз — слот заповнюється після першого врожаю.",
                new[] { "anthurium", "monstera", "hibiscus", "fiddle_leaf_fig" },
                new CollectionReward { gems = 1, coins = 500, potId = "bamboo",
                    bonus = CollectionBonusKind.Xp, bonusValue = 0.05f }),

            new CollectionDef("rare_beauties", "Рідкісні красуні", CollectionGroup.Theme, CollectionGoal.DiscoverPlant, "gold",
                "Найвибагливіші рослини гри — вершина садівника.",
                "Виростай кожен вид хоч раз — слот заповнюється після першого врожаю.",
                new[] { "azalea", "gardenia", "strelitzia", "orchid" },
                new CollectionReward { gems = 1, coins = 1000, avatarId = "leg",
                    bonus = CollectionBonusKind.SellPrice, bonusValue = 0.05f }),

            new CollectionDef("botanist", "Ботанік", CollectionGroup.Theme, CollectionGoal.CompleteCollection, "gold",
                "Збери всі 5 тематичних колекцій.",
                "Слот заповнюється, щойно зібрано відповідну колекцію (нагороду забирати не обов'язково).",
                new[] { "succulents", "windowsill", "green_leaves", "tropics", "rare_beauties" },
                new CollectionReward { coins = 1000, frameId = "spark", titleId = "Ботанік" }),

            // ── Майстерність ────────────────────────────────────────────────
            new CollectionDef("doctor", "Лікар рослин", CollectionGroup.Mastery, CollectionGoal.CureAilment, "blue",
                "Вилікуй кожну хворобу і прожени кожного шкідника хоч раз.",
                "Хвороби з'являються з 3-го рівня на рослинах, що ростуть довше 15 хвилин.",
                new[] { "1", "2", "3", "4" },
                new CollectionReward { coins = 300, avatarId = "doctor" }),

            new CollectionDef("potter", "Колекція горщиків", CollectionGroup.Mastery, CollectionGoal.OwnPot, "gold",
                "Купи всі горщики за монети.",
                "Горщики купуються на екрані Посадки, секція «Горщик».",
                new[] { "terracotta", "ceramic", "wicker", "concrete", "brass", "marble" },
                new CollectionReward { frameId = "marble", titleId = "Гончар" }),

            new CollectionDef("perfect_care", "Ідеальний догляд", CollectionGroup.Mastery, CollectionGoal.PerfectCare, "blue",
                "Виростай кожен вид, не пропустивши жодного поливу.",
                "Поливай щоразу, коли рослина просить води. Прискорення за кристали скорочує час для поливів.",
                null, // усі види (PlantCatalog)
                new CollectionReward { gems = 1, frameId = "dew",
                    bonus = CollectionBonusKind.CarePerWatering, bonusValue = 0.02f }),

            new CollectionDef("veteran", "Садівник-ветеран", CollectionGroup.Mastery, CollectionGoal.HarvestPlant, "violet",
                "Збери врожай кожного виду 10 разів.",
                "Рахуються врожаї, зібрані після появи цієї колекції в грі.",
                null, // усі види (PlantCatalog)
                new CollectionReward { gems = 1, avatarId = "can", titleId = "Ветеран" }),
        };

        /// <summary>
        /// Колекції, що є в грі зараз: без «Лікаря рослин», поки хвороби вимкнені
        /// (PlantAilments.Enabled, STG 2).
        /// </summary>
        public static readonly CollectionDef[] All = System.Array.FindAll(Catalog,
            c => PlantBreeding.Garden.PlantAilments.Enabled || c.goal != CollectionGoal.CureAilment);

        public static CollectionDef Get(string id)
        {
            foreach (var c in All)
                if (c.id == id) return c;
            return null;
        }

        /// <summary>Особиста вітрина — плейсхолдер макета (без прогресу й нагороди).</summary>
        public static readonly List<SlotDef> Showcase = new List<SlotDef>
        {
            new SlotDef("rose", true), new SlotDef("gold", true),
            new SlotDef("violet", true), new SlotDef("green", true),
        };
    }

    public class CollectionDef
    {
        public readonly string id;
        public readonly string name;
        public readonly CollectionGroup group;
        public readonly CollectionGoal goal;
        public readonly string accent; // green|gold|rose|blue|violet
        public readonly string desc;
        public readonly string hint;
        /// <summary>Ключі слотів; null — усі види з PlantCatalog (див. CollectionService.SlotKeys).</summary>
        public readonly string[] slotKeys;
        public readonly CollectionReward reward;

        public CollectionDef(string id, string name, CollectionGroup group, CollectionGoal goal, string accent,
            string desc, string hint, string[] slotKeys, CollectionReward reward)
        {
            this.id = id; this.name = name; this.group = group; this.goal = goal; this.accent = accent;
            this.desc = desc; this.hint = hint; this.slotKeys = slotKeys; this.reward = reward;
        }
    }

    public class CollectionReward
    {
        public int gems;
        public int coins;
        public string avatarId;
        public string frameId;
        public string potId;
        public string titleId;
        public CollectionBonusKind bonus;
        public float bonusValue;
    }

    // ── Моделі відображення для DexScreenController ─────────────────────────

    /// <summary>Один слот колекції. rarity ∈ green|gold|rose|blue|violet|lock|add.</summary>
    public class SlotDef
    {
        public string rarity;
        public bool done;
        public string name;  // для розгорнутого слота (детальний екран)
        public string tier;  // пігулка в кутку, напр. "рідкісна"
        public string sub;   // підпис знизу, напр. "зібрано 3×"

        public SlotDef(string rarity, bool done, string name = null, string tier = null, string sub = null)
        {
            this.rarity = rarity; this.done = done;
            this.name = name; this.tier = tier; this.sub = sub;
        }
    }

    /// <summary>Розгорнута колекція (деталь-оверлей) — повний список слотів.</summary>
    public class DetailDef
    {
        public string collectionId; // null — колекція тижня
        public string typeLabel, name, count, intro, hint, claimLabel;
        public int pct;
        public bool claimable, claimed;
        public List<SlotDef> slots;
    }
}
