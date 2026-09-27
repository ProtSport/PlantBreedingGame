using UnityEngine;
using PlantBreeding.Collections;
using PlantBreeding.Economy;
using PlantBreeding.Localization;
using PlantBreeding.Notifications;
using PlantBreeding.Theme;

namespace PlantBreeding.Core
{
    /// <summary>Стартовий сорт, який видається гравцю при першому запуску.</summary>
    public static class StarterSeeds
    {
        public const string PlantId = EconomyConfig.StarterPlantId;
        public const int Count = EconomyConfig.StarterSeedCount;
        public const string DefaultPotId = "plastic";
    }

    /// <summary>
    /// Головний синглтон гри. Тримає стан гравця і дає доступ до нього
    /// з будь-якого місця (GardenManager, UI, магазин тощо).
    /// В MVP навмисно простий — без DI-фреймворків, щоб було легко читати й розширювати.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Player state")]
        public PlayerData playerData = new PlayerData();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Save.SaveSystem.Load(playerData);
            GameClock.Init(playerData);
            ApplySettings();
            EnsureStarterInventory();
            SyncPrestigeFrames();
            EconomyService.RollDay(playerData);
            StartCoroutine(GameClock.Sync(OnClockSynced));

            GameNotifications.Init();
            GameNotifications.OnAppForeground(); // гравець у грі — старі нагадування вже не потрібні
            GameNotifications.MaybeRequestPermission(playerData);
        }

        /// <summary>
        /// Тема й мова з налаштувань гравця (шестірня в шапці). Застосовувачі живуть
        /// тут, на DontDestroyOnLoad-об'єкті, тож працюють на всіх сценах.
        /// </summary>
        private void ApplySettings()
        {
            if (GetComponent<ThemeApplier>() == null) gameObject.AddComponent<ThemeApplier>();
            if (GetComponent<LocalizationApplier>() == null) gameObject.AddComponent<LocalizationApplier>();
            // Лише Темна/Світла ("Як у системі" прибрано з меню) — старий вибір 2 → темна.
            if (playerData.themeMode != (int)ThemeMode.Light) playerData.themeMode = (int)ThemeMode.Dark;
            ThemeService.SetMode((ThemeMode)playerData.themeMode);
            Loc.Set(Loc.FromCode(playerData.language));
        }

        /// <summary>Мережевий час міг показати, що вже інший день (годинник пристрою відставав).</summary>
        private void OnClockSynced() => EconomyService.RollDay(playerData);

        private void EnsureStarterInventory()
        {
            if (playerData.seedInventory.Count == 0)
            {
                playerData.seedInventory.Add(new SeedStack(StarterSeeds.PlantId, StarterSeeds.Count));
            }
            if (string.IsNullOrEmpty(playerData.lastUsedPotId))
            {
                playerData.lastUsedPotId = StarterSeeds.DefaultPotId;
            }
            if (!playerData.ownedPotIds.Contains(StarterSeeds.DefaultPotId))
            {
                playerData.ownedPotIds.Add(StarterSeeds.DefaultPotId);
            }

            if (!playerData.ownedAvatarIds.Contains(ProfileItemCatalog.StarterAvatarId))
            {
                playerData.ownedAvatarIds.Add(ProfileItemCatalog.StarterAvatarId);
            }
            if (string.IsNullOrEmpty(playerData.activeAvatarId))
            {
                playerData.activeAvatarId = ProfileItemCatalog.StarterAvatarId;
            }
            if (!playerData.ownedFrameIds.Contains(ProfileItemCatalog.StarterFrameId))
            {
                playerData.ownedFrameIds.Add(ProfileItemCatalog.StarterFrameId);
            }
            if (string.IsNullOrEmpty(playerData.activeFrameId))
            {
                playerData.activeFrameId = ProfileItemCatalog.StarterFrameId;
            }
        }

        // ── Профіль персонажа (ТЗ «Персонаж», 2026-08) ───────────────────────
        /// <summary>
        /// Рівень престижу — MVP-правило на порогах рівня гравця (легко
        /// поправити, коли з'явиться повна таблиця рівнів 1-20 з GDD 10.5).
        /// Керує рамкою AvatarHero (не тим, яку "Рамку профілю" обрано в
        /// сітці — це окрема, поки не підключена до hero-візуалу система
        /// кастомізації, точно як у мокапі profile-screen.html).
        /// </summary>
        public string GetPrestigeTier()
        {
            int lvl = playerData.level;
            if (lvl >= 13) return "spark";
            if (lvl >= 8) return "gold";
            if (lvl >= 4) return "green";
            return "grey";
        }

        public string GetRankLabel()
        {
            switch (GetPrestigeTier())
            {
                case "spark": return "Легендарний садівник";
                case "gold": return "Золотий садівник";
                case "green": return "Досвідчений садівник";
                default: return "Садівник-новачок";
            }
        }

        /// <summary>
        /// Авто-видає рамки grey/green/gold, щойно гравець досяг відповідного
        /// рівня престижу (реальний, тестований шлях розблокування — на
        /// відміну від "spark", яка чекає на Завдання тижня). Ідемпотентно,
        /// викликати після Load і після кожної зміни рівня.
        /// </summary>
        public void SyncPrestigeFrames()
        {
            string tier = GetPrestigeTier();
            AddOwnedFrame("grey");
            if (tier == "green" || tier == "gold" || tier == "spark") AddOwnedFrame("green");
            if (tier == "gold" || tier == "spark") AddOwnedFrame("gold");
        }

        private void AddOwnedFrame(string frameId)
        {
            if (!playerData.ownedFrameIds.Contains(frameId)) playerData.ownedFrameIds.Add(frameId);
        }

        public bool IsAvatarOwned(string avatarId) => playerData.ownedAvatarIds.Contains(avatarId);
        public bool IsFrameOwned(string frameId) => playerData.ownedFrameIds.Contains(frameId);

        public bool SetActiveAvatar(string avatarId)
        {
            if (!IsAvatarOwned(avatarId)) return false;
            playerData.activeAvatarId = avatarId;
            Save.SaveSystem.Save(playerData);
            return true;
        }

        public bool SetActiveFrame(string frameId)
        {
            if (!IsFrameOwned(frameId)) return false;
            playerData.activeFrameId = frameId;
            Save.SaveSystem.Save(playerData);
            return true;
        }

        /// <summary>Списує 1 токен зміни імені. false без змін, якщо токенів нема або ім'я порожнє.</summary>
        public bool TryRename(string newName)
        {
            newName = newName?.Trim();
            if (string.IsNullOrEmpty(newName) || playerData.renameTokens <= 0) return false;
            if (newName.Length > 18) newName = newName.Substring(0, 18);
            playerData.playerName = newName;
            playerData.renameTokens--;
            Save.SaveSystem.Save(playerData);
            return true;
        }

        public int GetSeedCount(string plantId)
        {
            var stack = playerData.seedInventory.Find(s => s.plantId == plantId);
            return stack?.count ?? 0;
        }

        public void AddSeeds(string plantId, int count)
        {
            if (string.IsNullOrEmpty(plantId) || count <= 0) return;
            var stack = playerData.seedInventory.Find(s => s.plantId == plantId);
            if (stack == null)
            {
                stack = new SeedStack(plantId, 0);
                playerData.seedInventory.Add(stack);
            }
            stack.count += count;
        }

        public bool TryConsumeSeed(string plantId)
        {
            var stack = playerData.seedInventory.Find(s => s.plantId == plantId);
            if (stack == null || stack.count <= 0) return false;
            stack.count--;
            return true;
        }

        public void SetLastUsedPot(string potId)
        {
            playerData.lastUsedPotId = potId;
        }

        public bool IsPotOwned(string potId) => playerData.ownedPotIds.Contains(potId);

        // ── Реєстр відкриттів Дендрарію (Pokédex-логіка) ─────────────────────
        /// <summary>
        /// Позначає вид відкритим назавжди (викликається з PlotSlot.Harvest при
        /// першому зборі врожаю цього виду). Ідемпотентно — повторні збори не
        /// дублюють запис. Джерело правди для прогресу колекцій — не інвентар.
        /// </summary>
        public void RecordPlantDiscovered(string plantId)
        {
            if (string.IsNullOrEmpty(plantId)) return;
            if (playerData.discoveredPlantIds.Contains(plantId)) return;
            playerData.discoveredPlantIds.Add(plantId);
            playerData.unseenDexCount++; // кружечок з числом на іконці Дендрарію
            Save.SaveSystem.Save(playerData);
            PlantCollections.OnPlantDiscovered(plantId); // прогрес колекцій, +1 кристал за зібрану
            GameEvents.RaiseDexBadgeChanged();
        }

        /// <summary>Гравець відкрив Дендрарій — бейдж нових рослин зникає.</summary>
        public void MarkDexSeen()
        {
            if (playerData.unseenDexCount == 0) return;
            playerData.unseenDexCount = 0;
            Save.SaveSystem.Save(playerData);
            GameEvents.RaiseDexBadgeChanged();
        }

        public bool IsPlantDiscovered(string plantId) => playerData.discoveredPlantIds.Contains(plantId);

        public int DiscoveredSpeciesCount => playerData.discoveredPlantIds.Count;

        /// <summary>Списує монети, якщо їх достатньо. Повертає false і нічого не змінює, якщо ні.</summary>
        public bool TrySpendCoins(int amount)
        {
            if (amount <= 0) return true;
            if (playerData.coins < amount) return false;
            playerData.coins -= amount;
            GameEvents.RaiseCurrencyChanged();
            return true;
        }

        /// <summary>Розблоковує горщик за монети назавжди. Повертає false, якщо вже є або грошей не вистачає.</summary>
        public bool TryUnlockPot(string potId, int cost)
        {
            if (IsPotOwned(potId)) return true;
            if (!TrySpendCoins(cost)) return false;
            playerData.ownedPotIds.Add(potId);
            return true;
        }

        /// <summary>Купує count насінин разом (усі або жодної).</summary>
        public bool TryBuySeeds(string plantId, int costEach, int count)
        {
            if (count <= 0 || !TrySpendCoins(costEach * count)) return false;
            AddSeeds(plantId, count);
            return true;
        }

        /// <summary>Купує 1 насінину за монети, якщо їх достатньо.</summary>
        public bool TryBuySeed(string plantId, int cost)
        {
            if (!TrySpendCoins(cost)) return false;
            AddSeeds(plantId, 1);
            return true;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Save.SaveSystem.Save(playerData);
                GameNotifications.OnAppBackground(playerData);
                GameClock.OnPause();
            }
            else
            {
                GameNotifications.OnAppForeground();
                ThemeService.Refresh(); // «як у системі»: тема пристрою могла змінитись
                EconomyService.RollDay(playerData); // повернення з фону після півночі = новий день
                StartCoroutine(GameClock.Sync(OnClockSynced));
            }
        }

        private void OnApplicationQuit()
        {
            GameNotifications.OnAppBackground(playerData);
            Save.SaveSystem.Save(playerData);
        }

        public void AddCoins(int amount)
        {
            playerData.coins = Mathf.Max(0, playerData.coins + amount);
            GameEvents.RaiseCurrencyChanged();
        }

        public bool SpendGems(int amount)
        {
            if (playerData.gems < amount) return false;
            playerData.gems -= amount;
            GameEvents.RaiseCurrencyChanged();
            return true;
        }

        /// <summary>Нараховує кристали (напр. нагорода за завершене дослідження Лабораторії).</summary>
        public void AddGems(int amount)
        {
            playerData.gems = Mathf.Max(0, playerData.gems + amount);
            GameEvents.RaiseCurrencyChanged();
        }

        public void AddXp(int amount)
        {
            playerData.xp += amount;
            int xpForNextLevel = EconomyService.XpForNextLevel(playerData.level);
            while (playerData.xp >= xpForNextLevel)
            {
                playerData.xp -= xpForNextLevel;
                playerData.level++;
                xpForNextLevel = EconomyService.XpForNextLevel(playerData.level);
                EconomyService.GrantLevelUp(playerData.level);
                GameEvents.RaiseLevelUp(playerData.level);
                SyncPrestigeFrames();
            }
            GameEvents.RaiseCurrencyChanged();
        }
    }
}
