using System;
using System.Collections.Generic;

namespace PlantBreeding.Core
{
    /// <summary>
    /// Увесь стан гравця, який потрібно зберігати між сесіями.
    /// Простий POCO-клас — серіалізується через JsonUtility (див. Save/SaveSystem.cs).
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        // Стартовий баланс — EconomyConfig (docs/ECONOMY.md). Для старих
        // збережень значення перезапише Load, тож нові дефолти діють лише на новачків.
        public int coins = Economy.EconomyConfig.StarterCoins;
        public int gems = Economy.EconomyConfig.StarterGems;
        public int level = 1;
        public int xp = 0;

        // Стан грядок саду (по індексу слота) — MVP: одна зона "Моя грядка".
        public List<PlotSaveData> plots = new List<PlotSaveData>();

        // Інвентар насіння: id PlantData → кількість. Стартовий набір видається
        // в GameManager при першому запуску (якщо список порожній).
        public List<SeedStack> seedInventory = new List<SeedStack>();

        // Останній використаний гравцем горщик — дефолт для секції "Горщик"
        // на екрані Посадки (зменшує кількість тапів у рутинному циклі).
        public string lastUsedPotId = "";

        // Горщики, які гравець розблокував за монети (назавжди). Стартовий
        // "matte_black" додається в GameManager при першому запуску.
        public List<string> ownedPotIds = new List<string>();

        // Реєстр відкритих видів (Pokédex-логіка Дендрарію): plantId потрапляє
        // сюди при ПЕРШОМУ зборі врожаю і лишається НАЗАВЖДИ, навіть якщо гравець
        // продав усі екземпляри. Це джерело правди для прогресу колекцій
        // Дендрарію — прогрес рахується від факту відкриття, НЕ від поточного
        // інвентарю (ТЗ «Дендрарій — тільки колекції», п.0 і п.8).
        public List<string> discoveredPlantIds = new List<string>();

        // Прогрес гілок Лабораторії (скільки вузлів завершено в кожній) і
        // єдиний активний слот дослідження (Варіант А — одне дослідження
        // одночасно, підтверджено користувачем). Деталі — PlantBreeding.Lab.
        public List<LabBranchSave> labBranches = new List<LabBranchSave>();
        public LabActiveResearch labActive = new LabActiveResearch();

        // Профіль персонажа (ТЗ "Персонаж", 2026-08). Ім'я редагується лише
        // за токен зміни імені (renameTokens) — рідкісна нагорода, не валюта.
        // owned*/active* — інвентар предметів кастомізації профілю
        // (аватари/рамки), окремий від інвентарю рослин/горщиків. Джерело
        // правди для прогресу — ProfileItemCatalog; частину видають колекції (CollectionCatalog).
        public string playerName = "Садівник";
        public int renameTokens = 0;
        public List<string> ownedAvatarIds = new List<string>();
        public string activeAvatarId = "";
        public List<string> ownedFrameIds = new List<string>();
        public string activeFrameId = "";

        // ── Економіка / щоденна петля (EconomyService, docs/ECONOMY.md) ──
        // Дні — локальна дата "yyyy-MM-dd" (скидання опівночі за часом гравця).
        public string lastLoginDay = "";
        public int loginStreak = 0;
        public string loginRewardClaimedDay = "";
        public bool firstHarvestDone = false;
        public int totalHarvests = 0;

        public string dailyTasksDay = "";
        public List<DailyTaskSave> dailyTasks = new List<DailyTaskSave>();
        public bool dailyChestClaimed = false;

        // Грядки, відкриті за монети раніше за потрібний рівень.
        public List<int> boughtPlotIndices = new List<int>();

        // ── Колекції Дендрарію (Collections/CollectionService, docs/ECONOMY.md) ──
        // Старі сейви: колекції, за які кристал уже видано автоматично (до появи
        // кнопки «Забрати») — при отриманні нагороди кристал не дублюється.
        public List<string> completedCollectionIds = new List<string>();
        // Нагороду забрано (діють постійні бонуси колекції).
        public List<string> claimedCollectionIds = new List<string>();
        // Про завершення вже повідомлено тостом (щоб не повторювати).
        public List<string> announcedCollectionIds = new List<string>();
        // Прогрес колекцій майстерності.
        public List<string> perfectCarePlantIds = new List<string>();
        public List<int> curedAilmentKinds = new List<int>();
        public List<SeedStack> harvestCounts = new List<SeedStack>(); // plantId → скільки разів зібрано
        // Титули з колекцій; активний показується замість звання в Профілі.
        public List<string> ownedTitleIds = new List<string>();
        public string activeTitleId = "";
        // Колекція тижня: ISO-тиждень, 4 види на тиждень, зібрані цього тижня, нагороду забрано.
        public string weekKey = "";
        public List<string> weekPlantIds = new List<string>();
        public List<string> weekHarvestedIds = new List<string>();
        public bool weekClaimed;

        // ── Крамниця (Shop/ShopService, docs/ECONOMY.md) ──
        // Перший запуск — від нього рахується вікно «Стартового» набору.
        public double firstLaunchUnixSeconds;
        // Куплені назавжди товари (горщики, грядки) і вже куплені одноразові набори.
        public List<string> purchasedProductIds = new List<string>();
        // Останні оброблені транзакції магазину — захист від подвійного нарахування.
        public List<string> processedTransactionIds = new List<string>();
        // «Клуб садівника»: активність за перевіркою магазину, запасний строк
        // (тестова покупка в редакторі) і день останніх щоденних кристалів.
        public bool clubActive;
        public double clubUntilUnixSeconds;
        public string lastClubGemsDay = "";

        // Найпізніший момент ігрового часу, який бачила гра (GameClock) —
        // захист від перемотування годинника пристрою назад.
        public double lastSeenUnixSeconds;

        // Дозвіл на push-сповіщення вже запитано (GameNotifications — питаємо один раз).
        public bool notificationPermissionAsked;

        // Скільки нових рослин потрапило в колекції з останнього відкриття
        // Дендрарію — число в кружечку на іконці Дендрарію (EconomyOverlayView.DexBadge).
        public int unseenDexCount;

        // Налаштування (шестірня в шапці → SettingsSheet): 0 = темна, 1 = світла,
        // 2 = як у системі; мова — "uk" / "en" / "pl" (Localization.Loc).
        public int themeMode = 0;
        public string language = "uk";
    }

    [Serializable]
    public class DailyTaskSave
    {
        public Economy.DailyTaskKind kind;
        public string plantId = ""; // лише для PlantSpecies
        public int target;
        public int progress;
        public int rewardCoins;
        public int rewardXp;
        public bool claimed;

        public bool IsComplete => progress >= target;
    }

    [Serializable]
    public class LabBranchSave
    {
        public string branchId;
        public int completedLevels;

        public LabBranchSave() { }

        public LabBranchSave(string branchId, int completedLevels)
        {
            this.branchId = branchId;
            this.completedLevels = completedLevels;
        }
    }

    [Serializable]
    public class LabActiveResearch
    {
        // Порожній branchId = немає активного дослідження.
        public string branchId = "";
        public int nodeIndex = -1;
        public double startedAtUnixSeconds;
    }

    [Serializable]
    public class SeedStack
    {
        public string plantId;
        public int count;

        public SeedStack() { }

        public SeedStack(string plantId, int count)
        {
            this.plantId = plantId;
            this.count = count;
        }
    }

    [Serializable]
    public class PlotSaveData
    {
        public int slotIndex;
        public string state;        // зберігаємо enum PlotState як рядок для сумісності версій
        public string plantId;      // id PlantData (ScriptableObject), або "" якщо порожньо
        public double plantedAtUnixSeconds;
        public bool unlocked = true;

        // Параметри посадки фіксуються в момент посадки (горщик/добриво/лабораторія
        // не перераховуються заднім числом), тому зберігаються як є.
        public string potId = "";
        public int boost;
        public float effectiveGrowTimeSeconds;
        public double lastWateredUnixSeconds;
        public int wateredCount;
        public bool speedUpUsed;

        // Хвороба/шкідник (Garden/PlantAilments): запланована або поточна.
        public int ailment;
        public float ailmentAtFraction = -1f;
        public double sickSinceUnixSeconds;
        public int pestTapsLeft;
    }
}
