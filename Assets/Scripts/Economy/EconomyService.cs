using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PlantBreeding.Core;
using PlantBreeding.Garden;
using PlantBreeding.Lab;
using PlantBreeding.Notifications;
using PlantBreeding.Save;

namespace PlantBreeding.Economy
{
    public enum DailyTaskKind
    {
        Plant,
        Harvest,
        Water,
        EarnCoins,
        PlantSpecies,
        Cure
    }

    /// <summary>
    /// Чиста логіка економіки (без UI) поверх GameManager.playerData — той самий
    /// стиль, що LabResearchService. Відповідає за все, що дає або забирає
    /// ресурси поза екраном Посадки: урожай, рівні, грядки, щоденну нагороду
    /// і щоденні завдання. Числа — EconomyConfig, план — docs/ECONOMY.md.
    /// Повідомлення для гравця йдуть через GameEvents.RaiseToast, UI
    /// (EconomyOverlayView) сам вирішує, як їх показати.
    /// </summary>
    public static class EconomyService
    {
        private static GameManager Gm => GameManager.Instance;

        public static string DayKey(DateTime localTime) => localTime.ToString("yyyy-MM-dd");
        public static string Today => DayKey(GameClock.LocalNow);

        // ════════════════════════════════════════════════════════════════
        //  ДЕНЬ / ВХІД
        // ════════════════════════════════════════════════════════════════
        /// <summary>
        /// Перевіряє зміну дня (викликати при старті й поверненні з фону).
        /// Новий день → оновлює стрік і генерує нові щоденні завдання.
        /// Повертає true, якщо день змінився.
        /// </summary>
        public static bool RollDay(PlayerData data)
        {
            string today = Today;
            if (data.lastLoginDay == today)
            {
                if (data.dailyTasksDay != today) GenerateDailyTasks(data, today);
                return false;
            }

            bool firstLaunchEver = string.IsNullOrEmpty(data.lastLoginDay);
            if (firstLaunchEver)
            {
                // Перший день — без нагороди за вхід: новачок має спершу виростити
                // 2 стартові рослини й заробити сам. Завтра стрік стане Днем 1.
                data.loginStreak = 0;
                data.loginRewardClaimedDay = today;
            }
            else
            {
                string yesterday = DayKey(GameClock.LocalNow.AddDays(-1));
                data.loginStreak = data.lastLoginDay == yesterday ? data.loginStreak + 1 : 1;
            }

            data.lastLoginDay = today;
            GenerateDailyTasks(data, today);
            SaveSystem.Save(data);
            GameEvents.RaiseDailyStateChanged();
            return true;
        }

        public static bool IsLoginRewardAvailable(PlayerData data) =>
            data.loginStreak > 0 && data.loginRewardClaimedDay != Today;

        /// <summary>Номер дня в 7-денному циклі (0..6) для поточного стріку.</summary>
        public static int LoginCycleIndex(PlayerData data) =>
            (Mathf.Max(1, data.loginStreak) - 1) % EconomyConfig.LoginCycle.Length;

        public static LoginReward GetLoginReward(PlayerData data, int cycleIndex)
        {
            var r = EconomyConfig.LoginCycle[cycleIndex];
            int coins = Mathf.RoundToInt(r.coins * EconomyConfig.LoginCoinsScale(data.level));
            return new LoginReward(coins, r.seedPlantId, r.seedCount);
        }

        public static bool ClaimLoginReward()
        {
            var gm = Gm;
            if (gm == null) return false;
            var data = gm.playerData;
            if (!IsLoginRewardAvailable(data)) return false;

            var reward = GetLoginReward(data, LoginCycleIndex(data));
            data.loginRewardClaimedDay = Today;
            if (reward.coins > 0) gm.AddCoins(reward.coins);
            if (reward.seedCount > 0) gm.AddSeeds(reward.seedPlantId, reward.seedCount);

            SaveSystem.Save(data);
            GameEvents.RaiseDailyStateChanged();
            GameEvents.RaiseToast($"Нагороду дня {LoginCycleIndex(data) + 1} отримано: {FormatReward(reward)}");
            return true;
        }

        public static string FormatReward(LoginReward r)
        {
            var parts = new List<string>();
            if (r.coins > 0) parts.Add($"+{r.coins} монет");
            if (r.seedCount > 0) parts.Add($"+{r.seedCount} насіння «{PlantName(r.seedPlantId)}»");
            return string.Join(" · ", parts);
        }

        // ════════════════════════════════════════════════════════════════
        //  УРОЖАЙ
        // ════════════════════════════════════════════════════════════════
        /// <summary>
        /// Нараховує все за зібрану рослину: продаж (з бонусом догляду і
        /// лабораторії), XP, відкриття в Дендрарії, бонуси першого врожаю,
        /// прогрес завдань. Викликається з PlotSlot.Harvest.
        /// silent = без тосту про продаж (для «Зібрати все», де показується один
        /// підсумковий тост). Повертає нараховані за продаж монети.
        /// </summary>
        public static int GrantHarvest(PlantData plant, PotData pot, int wateredCount, bool silent = false)
        {
            var gm = Gm;
            if (gm == null || plant == null) return 0;
            var data = gm.playerData;

            float care = 1f + Mathf.Min(wateredCount, EconomyConfig.MaxCareWaterings) * EconomyConfig.CareBonusPerWatering;
            int price = Mathf.RoundToInt(PlantingForecast.SellPriceWithBonuses(plant, pot) * care);
            bool doubled = UnityEngine.Random.value < LabResearchService.GetYieldDoubleHarvestChance(data);
            if (doubled) price *= 2;

            gm.RecordPlantDiscovered(plant.plantId);
            gm.AddCoins(price);
            data.totalHarvests++;
            GameNotifications.MaybeRequestPermission(data); // після 2-го врожаю

            if (!silent)
            {
                var msg = $"{plant.displayName}: +{price} монет";
                if (doubled) msg += " (подвійний урожай!)";
                else if (wateredCount > 0) msg += $" (догляд +{Mathf.RoundToInt((care - 1f) * 100f)}%)";
                GameEvents.RaiseToast(msg);
            }

            if (!data.firstHarvestDone)
            {
                data.firstHarvestDone = true;
                gm.AddCoins(EconomyConfig.FirstHarvestBonusCoins);
                GameEvents.RaiseToast($"Перший урожай! Бонус +{EconomyConfig.FirstHarvestBonusCoins} монет — купи нове насіння");
            }

            Track(DailyTaskKind.Harvest, 1);
            Track(DailyTaskKind.EarnCoins, price);

            // XP в кінці — level up міг би показати тост раніше за сам урожай.
            gm.AddXp(PlantEconomy.HarvestXp(plant));
            SaveSystem.Save(data);
            return price;
        }

        // ════════════════════════════════════════════════════════════════
        //  РІВНІ
        // ════════════════════════════════════════════════════════════════
        /// <summary>Нагорода за новий рівень (викликається з GameManager.AddXp).</summary>
        public static void GrantLevelUp(int newLevel)
        {
            var gm = Gm;
            if (gm == null) return;
            int coins = EconomyConfig.LevelUpCoins(newLevel);
            gm.AddCoins(coins);

            var msg = $"Рівень {newLevel}! +{coins} монет";
            var newPlants = PlantCatalog.All.Where(p => p.unlockLevel == newLevel).Select(p => p.displayName).ToList();
            if (newPlants.Count > 0) msg += $" · нове насіння: {string.Join(", ", newPlants)}";
            for (int i = 0; i < EconomyConfig.PlotRules.Length; i++)
            {
                if (EconomyConfig.PlotRules[i].level == newLevel && !gm.playerData.boughtPlotIndices.Contains(i))
                {
                    msg += " · нова грядка";
                    break;
                }
            }
            GameEvents.RaiseToast(msg);
        }

        /// <summary>
        /// XP до наступного рівня: 100 + 50·(р−1) + 7·(р−1)². Підібрано симуляцією
        /// разом із хворобами (docs/ECONOMY.md): 18-й рівень (остання, 20-та рослина)
        /// за ~11–13 днів при 3–4 заходах на день; рівень 2 — ще в першій сесії.
        /// </summary>
        public static int XpForNextLevel(int level)
        {
            int n = Mathf.Max(0, level - 1);
            return 100 + 50 * n + 7 * n * n;
        }

        // ════════════════════════════════════════════════════════════════
        //  ГРЯДКИ
        // ════════════════════════════════════════════════════════════════
        public static bool IsPlotUnlocked(PlayerData data, int slotIndex) =>
            EconomyConfig.GetPlotRule(slotIndex).level <= data.level || data.boughtPlotIndices.Contains(slotIndex);

        public static string LockedPlotLabel(int slotIndex)
        {
            var rule = EconomyConfig.GetPlotRule(slotIndex);
            return rule.coinCost > 0 ? $"Рівень {rule.level}\nабо {rule.coinCost} монет" : $"Рівень {rule.level}";
        }

        /// <summary>Відкриває грядку за монети раніше за рівень. Повертає true, якщо грядка відкрита.</summary>
        public static bool TryBuyPlot(int slotIndex)
        {
            var gm = Gm;
            if (gm == null) return false;
            var data = gm.playerData;
            if (IsPlotUnlocked(data, slotIndex)) return true;

            var rule = EconomyConfig.GetPlotRule(slotIndex);
            if (rule.coinCost <= 0)
            {
                GameEvents.RaiseToast($"Грядка відкриється на рівні {rule.level}");
                return false;
            }
            if (!gm.TrySpendCoins(rule.coinCost))
            {
                GameEvents.RaiseToast($"Потрібно {rule.coinCost} монет (або рівень {rule.level})");
                return false;
            }

            data.boughtPlotIndices.Add(slotIndex);
            SaveSystem.Save(data);
            GameEvents.RaiseToast("Нову грядку відкрито!");
            return true;
        }

        // ════════════════════════════════════════════════════════════════
        //  ПРИСКОРЕННЯ ЗА КРИСТАЛИ
        // ════════════════════════════════════════════════════════════════
        public static bool CanSpeedUp(PlotSlot slot) =>
            slot != null && slot.plant != null && !slot.speedUpUsed
            && (slot.state == PlotState.Growing || slot.state == PlotState.NeedsWater)
            && slot.GetRemainingSeconds() > 1;

        public static int SpeedUpCost(PlotSlot slot) => EconomyConfig.SpeedUpGemCost(slot.GetRemainingSeconds());

        public enum SpeedUpResult { Ok, NotAllowed, NotEnoughGems }

        /// <summary>
        /// Час, що лишився, скорочується до 10% (рослина дорастає в 10 разів швидше;
        /// одразу після посадки 1000 с → 100 с). Один раз на посадку.
        /// </summary>
        public static SpeedUpResult TrySpeedUp(PlotSlot slot)
        {
            var gm = Gm;
            if (gm == null || !CanSpeedUp(slot)) return SpeedUpResult.NotAllowed;
            int cost = SpeedUpCost(slot);
            if (!gm.SpendGems(cost)) return SpeedUpResult.NotEnoughGems;

            string name = slot.plant.displayName;
            double before = slot.GetRemainingSeconds();
            slot.SpeedUp(EconomyConfig.SpeedUpFraction);
            SaveSystem.Save(gm.playerData);
            GameEvents.RaiseToast($"{name}: {TimeFormat.Remaining(before)} → {TimeFormat.Remaining(slot.GetRemainingSeconds())} (−{cost} крист.)");
            return SpeedUpResult.Ok;
        }

        // ════════════════════════════════════════════════════════════════
        //  ЛІКУВАННЯ (Garden/PlantAilments)
        // ════════════════════════════════════════════════════════════════
        /// <summary>Тап по шкіднику з вікна лікування. true — шкідника прибрано.</summary>
        public static bool TapPest(PlotSlot slot)
        {
            if (slot == null || slot.state != PlotState.Sick) return false;
            string ailmentName = slot.CurrentAilment.name;
            if (!slot.TapPest()) return false;
            GameEvents.RaiseToast($"{ailmentName}: прибрано! {slot.plant.displayName} знову росте");
            return true;
        }

        public static int RemedyCost(PlotSlot slot) =>
            slot != null && slot.plant != null ? PlantAilments.RemedyCost(slot.plant) : 0;

        /// <summary>Лікує хворобу засобом за монети. false — не хвороба або бракує монет.</summary>
        public static bool TryCureWithRemedy(PlotSlot slot)
        {
            var gm = Gm;
            if (gm == null || slot == null || slot.state != PlotState.Sick || slot.CurrentAilment.isPest) return false;
            int cost = RemedyCost(slot);
            if (!gm.TrySpendCoins(cost))
            {
                GameEvents.RaiseToast($"Потрібно {cost} монет на {slot.CurrentAilment.remedyName.ToLower()}");
                return false;
            }
            string remedy = slot.CurrentAilment.remedyName;
            slot.Cure();
            SaveSystem.Save(gm.playerData);
            GameEvents.RaiseToast($"{remedy} подіяв — {slot.plant.displayName} знову росте (−{cost} монет)");
            return true;
        }

        // ════════════════════════════════════════════════════════════════
        //  ЩОДЕННІ ЗАВДАННЯ
        // ════════════════════════════════════════════════════════════════
        private static void GenerateDailyTasks(PlayerData data, string today)
        {
            data.dailyTasksDay = today;
            data.dailyChestClaimed = false;
            data.dailyTasks.Clear();

            int lvl = Mathf.Max(1, data.level);
            var rng = new System.Random(today.GetHashCode() ^ data.totalHarvests);

            // Два стабільні (посадити/зібрати) + одне змінне — щодня трохи інший день.
            data.dailyTasks.Add(MakeTask(DailyTaskKind.Plant, Mathf.Min(3 + lvl / 3, 8), lvl));
            data.dailyTasks.Add(MakeTask(DailyTaskKind.Harvest, Mathf.Min(3 + lvl / 3, 8), lvl));

            var unlockedNonStarter = PlantCatalog.All
                .Where(p => p.unlockLevel <= lvl && p.plantId != EconomyConfig.StarterPlantId)
                .ToList();

            // «Вилікувати» — лише коли хвороби вже можливі (після навчальних урожаїв).
            int variants = data.totalHarvests >= PlantAilments.GraceHarvests && lvl >= 3 ? 4 : 3;
            int roll = rng.Next(variants);
            if (roll == 0 && unlockedNonStarter.Count > 0)
            {
                var species = unlockedNonStarter[rng.Next(unlockedNonStarter.Count)];
                var t = MakeTask(DailyTaskKind.PlantSpecies, 2, lvl);
                t.plantId = species.plantId;
                data.dailyTasks.Add(t);
            }
            else if (roll == 1)
            {
                data.dailyTasks.Add(MakeTask(DailyTaskKind.Water, Mathf.Min(2 + lvl / 4, 6), lvl));
            }
            else if (roll == 3)
            {
                data.dailyTasks.Add(MakeTask(DailyTaskKind.Cure, 1, lvl));
            }
            else
            {
                data.dailyTasks.Add(MakeTask(DailyTaskKind.EarnCoins, 60 + 30 * lvl, lvl));
            }
        }

        private static DailyTaskSave MakeTask(DailyTaskKind kind, int target, int level) => new DailyTaskSave
        {
            kind = kind,
            target = target,
            rewardCoins = EconomyConfig.TaskRewardCoins(level),
            rewardXp = EconomyConfig.TaskRewardXp(level),
        };

        /// <summary>Просуває відповідні щоденні завдання. Безпечно викликати будь-де.</summary>
        public static void Track(DailyTaskKind kind, int amount, string plantId = null)
        {
            var gm = Gm;
            if (gm == null || amount <= 0) return;
            bool changed = false;
            foreach (var task in gm.playerData.dailyTasks)
            {
                // Посадка конкретного виду рахується і в загальне "Посадити N".
                bool matches = task.kind == kind
                    || (kind == DailyTaskKind.Plant && task.kind == DailyTaskKind.PlantSpecies && task.plantId == plantId);
                if (!matches || task.IsComplete) continue;

                task.progress = Mathf.Min(task.target, task.progress + amount);
                changed = true;
                if (task.IsComplete) GameEvents.RaiseToast($"Завдання виконано: {DescribeTask(task)} — забери нагороду");
            }
            if (changed) GameEvents.RaiseDailyStateChanged();
        }

        public static bool ClaimTask(int index)
        {
            var gm = Gm;
            if (gm == null) return false;
            var tasks = gm.playerData.dailyTasks;
            if (index < 0 || index >= tasks.Count) return false;
            var task = tasks[index];
            if (!task.IsComplete || task.claimed) return false;

            task.claimed = true;
            gm.AddCoins(task.rewardCoins);
            SaveSystem.Save(gm.playerData);
            GameEvents.RaiseDailyStateChanged();
            gm.AddXp(task.rewardXp);
            return true;
        }

        public static bool IsChestAvailable(PlayerData data) =>
            !data.dailyChestClaimed && data.dailyTasks.Count > 0 && data.dailyTasks.All(t => t.claimed);

        public static bool ClaimChest()
        {
            var gm = Gm;
            if (gm == null || !IsChestAvailable(gm.playerData)) return false;
            gm.playerData.dailyChestClaimed = true;
            int coins = EconomyConfig.DailyChestCoins(gm.playerData.level);
            gm.AddCoins(coins);
            SaveSystem.Save(gm.playerData);
            GameEvents.RaiseDailyStateChanged();
            GameEvents.RaiseToast($"Скриня дня: +{coins} монет · +{EconomyConfig.DailyChestXp} XP. Нові завдання — завтра!");
            gm.AddXp(EconomyConfig.DailyChestXp);
            return true;
        }

        /// <summary>Скільки нагород чекає на тап (для бейджа на кнопці завдань).</summary>
        public static int ClaimableCount(PlayerData data)
        {
            int n = data.dailyTasks.Count(t => t.IsComplete && !t.claimed);
            if (IsChestAvailable(data)) n++;
            if (IsLoginRewardAvailable(data)) n++;
            return n;
        }

        public static string DescribeTask(DailyTaskSave t) => t.kind switch
        {
            DailyTaskKind.Plant => $"Посадити {t.target} рослин",
            DailyTaskKind.Harvest => $"Зібрати {t.target} врожаїв",
            DailyTaskKind.Water => $"Полити {t.target} рослин",
            DailyTaskKind.EarnCoins => $"Заробити {t.target} монет на продажу",
            DailyTaskKind.PlantSpecies => $"Посадити «{PlantName(t.plantId)}» ×{t.target}",
            DailyTaskKind.Cure => t.target == 1 ? "Вилікувати рослину" : $"Вилікувати {t.target} рослини",
            _ => "Завдання"
        };

        private static string PlantName(string plantId)
        {
            var p = PlantCatalog.Get(plantId);
            return p != null ? p.displayName : plantId;
        }
    }
}
