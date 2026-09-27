using System;
using System.Collections.Generic;
using System.Globalization;

namespace PlantBreeding.Collections
{
    /// <summary>
    /// Статичні дані екрану «Дендрарій · Колекції» (ТЗ «Дендрарій — тільки
    /// колекції»). ЦЕ ПЛЕЙСХОЛДЕР-КАРКАС: у грі поки існує лише вид rose_basic,
    /// тому прогрес/слоти тут прописані вручну — рівно як мок-масиви в макеті
    /// Дендрарій/export/dex-collections.html (WEEK / COLLECTIONS / SHOWCASE /
    /// DETAIL).
    ///
    /// Справжнє джерело правди прогресу — PlayerData.discoveredPlantIds
    /// (заповнюється в PlotSlot.Harvest, Pokédex-логіка). Коли з'явиться більше
    /// видів, цей каталог замінить CollectionService, що рахуватиме заповнені
    /// слоти з реєстру відкриттів, а не з жорстко прописаних тут значень.
    /// Тип колекцій і правила формування слотів — ТЗ п.2.
    /// </summary>
    public static class CollectionCatalog
    {
        // Чіпи-фільтри під карткою тижня (фільтрують список за typeLabel).
        public static readonly string[] Chips = { "Усі", "За родиною", "Рідкість", "Особливі" };

        /// <summary>
        /// Колекція тижня (ТЗ п.7): реальна логіка (не мок) — раз на 7 днів
        /// обирається тема з 4 видів, які гравець вже колись відкривав,
        /// детерміновано на ISO-тиждень (той самий набір увесь тиждень без
        /// бекенду). Якщо відкрито менше 4 видів — повертає null, і UI
        /// показує заглушку "Відкрий більше видів".
        /// Слот вважається виконаним одразу — вид уже відкрито назавжди,
        /// той самий принцип "прогрес від факту відкриття", що й для інших
        /// колекцій (ТЗ п.0). Окремого трекінгу "перезібрано саме цього
        /// тижня" зараз немає — додати разом із CollectionService, коли
        /// зʼявиться більше видів і сенс у справжньому re-collect-квесті.
        /// </summary>
        public static WeekDef GetWeeklyCollection(IReadOnlyList<string> discoveredIds)
        {
            if (discoveredIds == null || discoveredIds.Count < 4) return null;

            var pool = new List<string>(discoveredIds);
            pool.Sort(StringComparer.Ordinal); // стабільний порядок перед детермінованим вибором

            var now = DateTime.UtcNow;
            int seed = ISOWeek.GetYear(now) * 100 + ISOWeek.GetWeekOfYear(now);
            var rng = new Random(seed);
            var picked = new List<string>();
            for (int i = 0; i < 4 && pool.Count > 0; i++)
            {
                int idx = rng.Next(pool.Count);
                picked.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            var slots = new List<SlotDef>();
            foreach (var plantId in picked)
            {
                var plant = UnityEngine.Resources.Load<Garden.PlantData>("Plants/" + plantId);
                slots.Add(new SlotDef("green", true, plant != null ? plant.displayName : plantId));
            }

            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7;

            return new WeekDef
            {
                title = "Тема тижня",
                desc = "4 знайомих види, обрані на цей тиждень — уже є у твоїй колекції.",
                endsIn = daysUntilMonday + " дн",
                count = "4/4",
                pct = 100,
                slots = slots,
            };
        }

        public static readonly List<CollectionDef> Collections = new List<CollectionDef>
        {
            new CollectionDef
            {
                id = "roses", name = "Родина троянд", typeLabel = "За родиною", accent = "rose",
                desc = "База → рідкісна → епічна → легендарна форма троянди.",
                miniSlots = new List<SlotDef>
                {
                    new SlotDef("rose", true), new SlotDef("rose", true),
                    new SlotDef("gold", true), new SlotDef("lock", false),
                },
                count = "3/4", pct = 75, claim = false, reward = "рамка «Трояндова» + 50 кр.",
                detail = new DetailDef
                {
                    typeLabel = "За родиною · постійна", name = "Родина троянд", count = "3/4", pct = 75,
                    intro = "Усі рідкісні форми троянди. Слот лишається заповненим НАЗАВЖДИ після першого збору — навіть якщо ти продав рослину.",
                    hint = "Легендарну троянду виводять у Лабораторії через схрещування рідкісної + епічної.",
                    claimLabel = "Забрати нагороду · ще 1 слот",
                    slots = new List<SlotDef>
                    {
                        new SlotDef("rose", true, "Базова", "лвл 1", "зібрано 12×"),
                        new SlotDef("blue", true, "Рідкісна", "лвл 2", "зібрано 3×"),
                        new SlotDef("gold", true, "Епічна", "лвл 3", "зібрано 1×"),
                        new SlotDef("lock", false, "Легендарна"),
                    },
                },
            },
            new CollectionDef
            {
                id = "gold", name = "Золота колекція", typeLabel = "Особливі", accent = "gold",
                desc = "По одному золотистому варіанту кожного виду.",
                miniSlots = new List<SlotDef>
                {
                    new SlotDef("gold", true), new SlotDef("gold", true), new SlotDef("lock", false),
                    new SlotDef("lock", false), new SlotDef("lock", false),
                },
                count = "2/9", pct = 22, claim = false, reward = "золота лійка + 200 кр.",
                detail = new DetailDef
                {
                    typeLabel = "Особливі · постійна", name = "Золота колекція", count = "2/9", pct = 22,
                    intro = "По одному золотистому варіанту кожного виду. Золоті форми випадають рідко при ідеальному догляді.",
                    hint = "Золоті варіанти частіше з'являються при ідеальній якості догляду під час росту.",
                    claimLabel = "Забрати нагороду · зібери 9",
                    slots = new List<SlotDef>
                    {
                        new SlotDef("gold", true, "Троянда", "золота", "зібрано 4×"),
                        new SlotDef("gold", true, "Фікус", "золотий", "зібрано 1×"),
                        new SlotDef("lock", false, "Півонія"),
                        new SlotDef("lock", false, "Орхідея"),
                        new SlotDef("lock", false, "Лотос"),
                    },
                },
            },
            new CollectionDef
            {
                id = "legendary", name = "Усі легендарні", typeLabel = "Рідкість", accent = "violet",
                desc = "По одному екземпляру кожної легендарної рослини гри.",
                miniSlots = new List<SlotDef>
                {
                    new SlotDef("violet", true), new SlotDef("lock", false), new SlotDef("lock", false),
                    new SlotDef("lock", false), new SlotDef("lock", false),
                },
                count = "1/7", pct = 14, claim = false, reward = "титул «Легендар» + 500 кр.",
                detail = new DetailDef
                {
                    typeLabel = "Рідкість · постійна, розширюється", name = "Усі легендарні", count = "1/7", pct = 14,
                    intro = "По одному екземпляру кожної легендарної рослини. Набір автоматично росте, коли в оновленнях додають нові легендарні види (ТЗ п.4).",
                    hint = "Легендарні виводяться лише схрещуванням у Лабораторії — за монети їх не купити.",
                    claimLabel = "Забрати нагороду · зібери 7",
                    slots = new List<SlotDef>
                    {
                        new SlotDef("violet", true, "Місячна троянда", "легенда", "зібрано 1×"),
                        new SlotDef("lock", false, "Вогнецвіт"),
                        new SlotDef("lock", false, "Зоряний лотос"),
                        new SlotDef("lock", false, "Кришталева орхідея"),
                        new SlotDef("lock", false, "Тіньова папороть"),
                        new SlotDef("lock", false, "Сонячний сонях"),
                        new SlotDef("lock", false, "Райдужний пік"),
                    },
                },
            },
            new CollectionDef
            {
                id = "glow", name = "Світло та метелики", typeLabel = "Особливі", accent = "green",
                desc = "Види, що світяться вночі й приваблюють метеликів.",
                miniSlots = new List<SlotDef>
                {
                    new SlotDef("green", true), new SlotDef("green", true),
                    new SlotDef("green", true), new SlotDef("green", true),
                },
                count = "4/4", pct = 100, claim = true, reward = "фон «Світляки» + 120 кр.",
                detail = new DetailDef
                {
                    typeLabel = "Особливі · постійна", name = "Світло та метелики", count = "4/4", pct = 100,
                    intro = "Види, що світяться вночі й приваблюють метеликів. Колекцію завершено — забери нагороду.",
                    hint = "Нічне світіння видно на екрані «Мій сад» після заходу сонця.",
                    claimLabel = "Забрати нагороду",
                    slots = new List<SlotDef>
                    {
                        new SlotDef("green", true, "Світляк", "лвл 1", "зібрано 8×"),
                        new SlotDef("green", true, "Місячниця", "лвл 2", "зібрано 5×"),
                        new SlotDef("green", true, "Нічна фіалка", "лвл 3", "зібрано 2×"),
                        new SlotDef("green", true, "Метеликоцвіт", "лвл 4", "зібрано 1×"),
                    },
                },
            },
        };

        // Особиста вітрина: 4 обрані рослини + 1 порожній слот «додати»
        // (обробляється в UI). Без прогресу й нагороди (ТЗ п.2).
        public static readonly List<SlotDef> Showcase = new List<SlotDef>
        {
            new SlotDef("rose", true), new SlotDef("gold", true),
            new SlotDef("violet", true), new SlotDef("green", true),
        };
    }

    /// <summary>Один слот колекції. rarity ∈ green|gold|rose|blue|violet|lock.</summary>
    public class SlotDef
    {
        public string rarity;
        public bool done;         // вид відкрито хоч раз (заповнений назавжди)
        public string name;       // для розгорнутого слота (детальний екран)
        public string tier;       // напр. "лвл 2" / "золота"
        public string sub;        // напр. "зібрано 3×"

        public SlotDef(string rarity, bool done, string name = null, string tier = null, string sub = null)
        {
            this.rarity = rarity; this.done = done;
            this.name = name; this.tier = tier; this.sub = sub;
        }
    }

    public class WeekDef
    {
        public string title, desc, endsIn, count;
        public int pct;
        public List<SlotDef> slots;
    }

    /// <summary>Розгорнута колекція (екран B макета) — повний список слотів.</summary>
    public class DetailDef
    {
        public string typeLabel, name, count, intro, hint, claimLabel;
        public int pct;
        public List<SlotDef> slots;
    }

    public class CollectionDef
    {
        public string id, name, typeLabel, accent, desc, count, reward;
        public int pct;
        public bool claim;        // action == claim (100%)
        public List<SlotDef> miniSlots;
        public DetailDef detail;
    }
}
