namespace PlantBreeding.Economy
{
    /// <summary>
    /// Усі числа економіки в одному місці (план — docs/ECONOMY.md).
    /// Ціни/час росту/дохід конкретних видів живуть в асетах PlantData
    /// (Assets/Resources/Plants, створює ContentSeeder) — тут лише правила,
    /// що не прив'язані до одного виду.
    /// </summary>
    public static class EconomyConfig
    {
        // ── Старт нового гравця ──────────────────────────────────────────
        // 2 фіалки і 0 монет: перша сесія = посадив 2 → зібрав за 3 хв →
        // отримав монети → САМ купив нове насіння. Купити щось до першого
        // врожаю неможливо навмисно — це і є перший урок петлі.
        public const string StarterPlantId = "violet";
        public const int StarterSeedCount = 2;
        public const int StarterCoins = 0;

        // Кристали: ЛИШЕ маленький стартовий подарунок (спробувати прискорення
        // 1–3 рази), далі — тільки покупка в Крамниці. Жодна ігрова дія
        // кристалів не нараховує (ні рівні, ні вхід, ні завдання, ні лабораторія).
        public const int StarterGems = 3;

        /// <summary>Одноразовий бонус за найперший урожай у грі (разом із продажем 2 фіалок = 54 монети).</summary>
        public const int FirstHarvestBonusCoins = 30;

        // ── Догляд (полив) ───────────────────────────────────────────────
        // Спрага НЕ зупиняє ріст (гравець, що заходить раз на день, нічого не
        // втрачає), але кожен вчасний полив дає +10% до ціни продажу, максимум
        // +30% — це множник «ідеального догляду» ×1.3 з GDD 10.3.
        public const int MaxCareWaterings = 3;
        public const float CareBonusPerWatering = 0.10f;
        public const int WaterXp = 2;

        // ── Грядки ───────────────────────────────────────────────────────
        // Індекс слота → рівень, на якому відкривається безкоштовно, і ціна,
        // щоб відкрити раніше за монети (GDD 10.5: 5-та на рівні 2, 6-та на 6).
        public static readonly PlotUnlockRule[] PlotRules =
        {
            new PlotUnlockRule(1, 0),
            new PlotUnlockRule(1, 0),
            new PlotUnlockRule(1, 0),
            new PlotUnlockRule(1, 0),
            new PlotUnlockRule(2, 100),
            new PlotUnlockRule(6, 300),
        };

        public static PlotUnlockRule GetPlotRule(int slotIndex) =>
            slotIndex >= 0 && slotIndex < PlotRules.Length ? PlotRules[slotIndex] : new PlotUnlockRule(99, 0);

        // ── Рівні гравця ─────────────────────────────────────────────────
        // Монети масштабуються з рівнем (у GDD 15–30 — замало при цінах
        // насіння до 320). Кристалів за рівень немає — лише Крамниця.
        public static int LevelUpCoins(int newLevel) => 20 * newLevel;

        // ── Прискорення росту за кристали ────────────────────────────────
        // Час, що лишився, стає 10% від себе (рослина дорастає в 10 разів
        // швидше): прискорив одразу після посадки — 1000 с → 100 с. Один раз на посадку. Ціна — від
        // залишку: ≤4 год = 1 кристал (стартових 3 вистачає спробувати на
        // коротких рослинах), кожні наступні 4 год +1.
        public const float SpeedUpFraction = 0.90f;
        public const double SpeedUpHoursPerGem = 4.0;

        public static int SpeedUpGemCost(double remainingSeconds) =>
            System.Math.Max(1, (int)System.Math.Ceiling(remainingSeconds / 3600.0 / SpeedUpHoursPerGem));

        // ── Крамниця кристалів ───────────────────────────────────────────
        // Ціни — плейсхолдер для UI; реальні ціни прийдуть з Google Play /
        // App Store, коли підключимо IAP (GemStore).
        public static readonly GemPack[] GemPacks =
        {
            new GemPack("gems_10", 10, "49 грн", ""),
            new GemPack("gems_30", 30, "129 грн", "+10%"),
            new GemPack("gems_80", 80, "299 грн", "+25%"),
            new GemPack("gems_200", 200, "649 грн", "вигідно"),
        };

        // ── Щоденна нагорода за вхід (7-денний цикл) ─────────────────────
        // Пропустив день → стрік починається з Дня 1. Монети множаться на
        // рівень (LoginCoinsScale), щоб нагорода не знецінювалась.
        // Лише монети й насіння — без кристалів.
        public static readonly LoginReward[] LoginCycle =
        {
            new LoginReward(20, null, 0),
            new LoginReward(30, null, 0),
            new LoginReward(30, "cactus", 3),
            new LoginReward(50, null, 0),
            new LoginReward(40, "aloe", 3),
            new LoginReward(70, null, 0),
            new LoginReward(150, null, 0),
        };

        public static float LoginCoinsScale(int level) => 1f + (level - 1) * 0.15f;

        // ── Щоденні завдання ─────────────────────────────────────────────
        public const int DailyTaskCount = 3;
        public static int DailyChestCoins(int level) => 40 + 10 * level;
        public const int DailyChestXp = 20;

        public static int TaskRewardCoins(int level) => 20 + 6 * level;
        public static int TaskRewardXp(int level) => 15 + 3 * level;
    }

    public readonly struct PlotUnlockRule
    {
        public readonly int level;
        public readonly int coinCost;

        public PlotUnlockRule(int level, int coinCost)
        {
            this.level = level;
            this.coinCost = coinCost;
        }
    }

    public readonly struct LoginReward
    {
        public readonly int coins;
        public readonly string seedPlantId;
        public readonly int seedCount;

        public LoginReward(int coins, string seedPlantId, int seedCount)
        {
            this.coins = coins;
            this.seedPlantId = seedPlantId;
            this.seedCount = seedCount;
        }
    }

    public readonly struct GemPack
    {
        public readonly string productId;
        public readonly int gems;
        public readonly string priceLabel;
        public readonly string badge;

        public GemPack(string productId, int gems, string priceLabel, string badge)
        {
            this.productId = productId;
            this.gems = gems;
            this.priceLabel = priceLabel;
            this.badge = badge;
        }
    }
}
