namespace PlantBreeding.Shop
{
    /// <summary>Тип товару в магазині застосунків (App Store / Google Play).</summary>
    public enum ShopProductType
    {
        Consumable,    // кристали, монети, насіння, набори (набори — одноразові правилом гри)
        NonConsumable, // преміум-горщики, додаткові грядки — назавжди, відновлюються
        Subscription   // «Клуб садівника» — щомісячна
    }

    /// <summary>Розділ Крамниці (вкладки-чіпи вгорі екрану).</summary>
    public enum ShopSection
    {
        Sets,
        Gems,
        Coins,
        Pots,
        Seeds,
        Plots,
        Club
    }

    /// <summary>
    /// Каталог Крамниці за макетом Крамниця/export-shop/kramnytsia.html
    /// (docs/ECONOMY.md, «Крамниця»). Усі покупки — за реальні гроші через
    /// In-App Purchase (StoreKit на iOS, Google Play Billing на Android) —
    /// StoreGateway. Ціни в гривнях — запасні підписи: у грі показується
    /// локалізована ціна з магазину, коли товари завантажено.
    ///
    /// Кожен storeId треба завести в App Store Connect і Google Play Console
    /// з тим самим ідентифікатором і типом.
    ///
    /// За гроші НЕ продається нічого з колекцій Дендрарію (горщик «Бамбуковий»,
    /// аватари й рамки колекцій, 6 горщиків за монети).
    /// </summary>
    public static class ShopCatalog
    {
        public const string StorePrefix = "com.protsport.plantbreeding.";

        public const string StarterId = "starter_pack";
        public const string ProId = "pro_pack";
        public const string SeedsId = "seed_pack";
        public const string Plot7Id = "plot_7";
        public const string Plot8Id = "plot_8";
        public const string ClubId = "club_monthly";

        /// <summary>Скільки днів від першого запуску доступний «Стартовий» набір.</summary>
        public const int StarterWindowDays = 3;
        public const int SeedPackCount = 10;
        public const int ClubDailyGems = 3;
        public const int ClubDailyCoins = 200;
        public const float ClubSellBonus = 0.10f;
        public const string ClubFrameId = "club";
        /// <summary>Аватар Клубу — як і рамка, лишається назавжди після першої покупки.</summary>
        public const string ClubAvatarId = "sun";

        public static readonly ShopProduct[] All =
        {
            // ── Набори (одноразові) ─────────────────────────────────────────
            new ShopProduct(StarterId, ShopProductType.Consumable, ShopSection.Sets, "Стартовий", "Набір · один раз",
                "20 кристалів + 1 500 монет + Кришталевий горщик", "79 грн")
                { gems = 20, coins = 1500, potId = "crystal", oneTime = true },
            new ShopProduct(ProId, ShopProductType.Consumable, ShopSection.Sets, "Садівник-профі", "Набір · один раз",
                "80 кристалів + 5 000 монет + Нефритовий горщик", "349 грн")
                { gems = 80, coins = 5000, potId = "jade", oneTime = true },

            // ── Кристали ────────────────────────────────────────────────────
            new ShopProduct("gems_10", ShopProductType.Consumable, ShopSection.Gems, "10 кристалів", "Кристали",
                "Прискорюють ріст рослини в 10 разів.", "49 грн") { gems = 10 },
            new ShopProduct("gems_30", ShopProductType.Consumable, ShopSection.Gems, "30 кристалів", "Кристали",
                "На 10% більше, ніж у найменшому пакеті.", "129 грн") { gems = 30, badge = "+10%" },
            new ShopProduct("gems_80", ShopProductType.Consumable, ShopSection.Gems, "80 кристалів", "Кристали",
                "На 25% більше, ніж у найменшому пакеті.", "299 грн") { gems = 80, badge = "+25%" },
            new ShopProduct("gems_200", ShopProductType.Consumable, ShopSection.Gems, "200 кристалів", "Кристали",
                "Найвигідніший пакет.", "649 грн") { gems = 200, badge = "Вигідно" },

            // ── Монети ──────────────────────────────────────────────────────
            new ShopProduct("coins_1000", ShopProductType.Consumable, ShopSection.Coins, "1 000 монет", "Монети",
                "Приблизно один день гри.", "49 грн") { coins = 1000, note = "≈ день гри" },
            new ShopProduct("coins_3500", ShopProductType.Consumable, ShopSection.Coins, "3 500 монет", "Монети",
                "Приблизно три дні гри.", "129 грн") { coins = 3500, note = "≈ 3 дні гри" },
            new ShopProduct("coins_10000", ShopProductType.Consumable, ShopSection.Coins, "10 000 монет", "Монети",
                "Приблизно дев'ять днів гри.", "299 грн") { coins = 10000, note = "≈ 9 днів гри" },

            // ── Преміум-горщики (назавжди) ─────────────────────────────────
            new ShopProduct("pot_crystal", ShopProductType.NonConsumable, ShopSection.Pots, "Кришталевий", "Преміум-горщик · назавжди",
                "−15% часу росту · +15% XP", "99 грн") { potId = "crystal" },
            new ShopProduct("pot_jade", ShopProductType.NonConsumable, ShopSection.Pots, "Нефритовий", "Преміум-горщик · назавжди",
                "−15% часу росту · +8% ціна · +8% XP", "99 грн") { potId = "jade" },
            new ShopProduct("pot_gold", ShopProductType.NonConsumable, ShopSection.Pots, "Золотий", "Преміум-горщик · назавжди",
                "−15% часу росту · +12% ціна продажу", "149 грн") { potId = "gold" },

            // ── Насіння ─────────────────────────────────────────────────────
            new ShopProduct(SeedsId, ShopProductType.Consumable, ShopSection.Seeds, "Набір насіння", "Насіння",
                "10 насінин найдорожчого з відкритих видів.", "49 грн") { seeds = SeedPackCount },

            // ── Додаткові грядки (назавжди) ────────────────────────────────
            new ShopProduct(Plot7Id, ShopProductType.NonConsumable, ShopSection.Plots, "7-ма грядка", "Грядка · назавжди",
                "Ще одне місце в саду.", "149 грн") { plotIndex = 6 },
            new ShopProduct(Plot8Id, ShopProductType.NonConsumable, ShopSection.Plots, "8-ма грядка", "Грядка · назавжди",
                "Ще одне місце в саду.", "199 грн") { plotIndex = 7, requiresId = Plot7Id },

            // ── Підписка ────────────────────────────────────────────────────
            new ShopProduct(ClubId, ShopProductType.Subscription, ShopSection.Club, "Клуб садівника", "Підписка",
                "3 кристали і 200 монет щодня · +10% монет з продажу · рамка «Клуб» і аватар «Сонце» назавжди. Щомісяця, скасувати можна будь-коли.", "199 грн / міс")
                { club = true },
        };

        /// <summary>
        /// Насіннєва капсула (SeedCapsule) — за кристали, тому НЕ в All: її не
        /// реєструємо в магазині застосунків. Для шторки покупки — як звичайний товар.
        /// </summary>
        public static readonly ShopProduct Capsule = new ShopProduct("seed_capsule", ShopProductType.Consumable,
            ShopSection.Seeds, "Насіннєва капсула", "Капсула · за кристали",
            "3 насінини трьох різних рослин. Яких — дізнаєшся, коли відкриєш. Може випасти рослина вище твого рівня — її можна посадити одразу.",
            SeedCapsule.GemPrice + " кристалів") { gemPrice = SeedCapsule.GemPrice };

        public static ShopProduct Get(string id)
        {
            foreach (var p in All)
                if (p.id == id) return p;
            return null;
        }

        public static ShopProduct GetByStoreId(string storeId)
        {
            foreach (var p in All)
                if (p.StoreId == storeId || p.id == storeId) return p;
            return null;
        }
    }

    public class ShopProduct
    {
        public readonly string id;
        public readonly ShopProductType type;
        public readonly ShopSection section;
        public readonly string title;
        /// <summary>Підпис над назвою в шторці покупки, напр. «Набір · один раз».</summary>
        public readonly string kind;
        public readonly string desc;
        /// <summary>Ціна для показу, поки магазин не повернув локалізовану.</summary>
        public readonly string fallbackPrice;

        public int gems;
        public int coins;
        public string potId;
        public int seeds;
        public int plotIndex = -1;
        public bool club;
        /// <summary>Витратний товар, який можна купити лише раз (набори).</summary>
        public bool oneTime;
        /// <summary>Доступний лише після покупки іншого товару (8-ма грядка після 7-ї).</summary>
        public string requiresId;
        public string badge;
        /// <summary>&gt;0 — купується за кристали в грі, а не в магазині застосунків.</summary>
        public int gemPrice;
        public string note;

        public string StoreId => ShopCatalog.StorePrefix + id;

        public ShopProduct(string id, ShopProductType type, ShopSection section, string title, string kind,
            string desc, string fallbackPrice)
        {
            this.id = id; this.type = type; this.section = section; this.title = title; this.kind = kind;
            this.desc = desc; this.fallbackPrice = fallbackPrice;
        }
    }
}
