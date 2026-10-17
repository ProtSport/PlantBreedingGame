using System.Linq;
using UnityEngine;
using PlantBreeding.Collections;
using PlantBreeding.Core;
using PlantBreeding.Garden;
using PlantBreeding.Save;

namespace PlantBreeding.Economy
{
    /// <summary>
    /// «Завдання тижня» (RELEASE-PLAN п. 9): 5 завдань на ISO-тиждень (оновлюються щопонеділка),
    /// за кожне — монети й XP (×3 від щоденного), за всі п'ять — скриня тижня з аватаром або
    /// рамкою профілю (WeeklyRewardOrder, по одній на тиждень; коли все зібрано — монети).
    /// Прогрес рахує той самий EconomyService.Track, що й щоденні завдання.
    /// Вікно — вкладка «Тиждень» у «Завданнях» (EconomyOverlayView).
    /// </summary>
    public static class WeeklyTasks
    {
        private static GameManager Gm => GameManager.Instance;

        /// <summary>Нагороди скрині тижня по черзі — аватар, рамка, аватар… (ProfileItemCatalog).</summary>
        public static readonly (bool avatar, string id)[] WeeklyRewardOrder =
        {
            (true, "rose"), (false, "morning"), (true, "sunflower"), (false, "september"),
            (true, "bee"), (false, "vine"), (true, "butterfly"), (false, "garden"), (true, "porcini"),
        };

        // ── Тиждень ──────────────────────────────────────────────────────
        /// <summary>Новий тиждень → нові завдання; зараховує сьогоднішній вхід. Викликає RollDay.</summary>
        public static void Ensure(PlayerData data)
        {
            string key = CollectionService.CurrentWeekKey;
            if (data.weeklyKey != key || data.weeklyTasks.Count == 0) Generate(data, key);

            string today = EconomyService.Today;
            if (!data.weeklyLoginDays.Contains(today))
            {
                data.weeklyLoginDays.Add(today);
                Progress(data, DailyTaskKind.LoginDays, data.weeklyLoginDays.Count, absolute: true);
            }
        }

        private static void Generate(PlayerData data, string key)
        {
            data.weeklyKey = key;
            data.weeklyChestClaimed = false;
            data.weeklyTasks.Clear();
            data.weeklyPlantedSpecies.Clear();
            data.weeklyLoginDays.Clear();

            int lvl = Mathf.Max(1, data.level);
            int unlocked = PlantCatalog.All.Count(p => p.unlockLevel <= lvl);
            data.weeklyTasks.Add(Make(DailyTaskKind.Harvest, Mathf.Min(25 + 2 * lvl, 60), lvl));
            data.weeklyTasks.Add(Make(DailyTaskKind.DistinctSpecies, Mathf.Clamp(Mathf.Min(4 + lvl / 4, unlocked), 2, 8), lvl));
            data.weeklyTasks.Add(Make(DailyTaskKind.Care, Mathf.Min(12 + lvl, 30), lvl));
            data.weeklyTasks.Add(Make(DailyTaskKind.EarnCoins, 400 + 120 * lvl, lvl));
            data.weeklyTasks.Add(Make(DailyTaskKind.LoginDays, EconomyConfig.WeeklyLoginDays, lvl));
        }

        private static DailyTaskSave Make(DailyTaskKind kind, int target, int level) => new DailyTaskSave
        {
            kind = kind,
            target = target,
            rewardCoins = EconomyConfig.TaskRewardCoins(level) * EconomyConfig.WeeklyRewardMultiplier,
            rewardXp = EconomyConfig.TaskRewardXp(level) * EconomyConfig.WeeklyRewardMultiplier,
        };

        // ── Прогрес ──────────────────────────────────────────────────────
        /// <summary>Подія гри (з EconomyService.Track) → відповідні тижневі завдання.</summary>
        public static void Track(PlayerData data, DailyTaskKind kind, int amount, string plantId)
        {
            if (data.weeklyKey != CollectionService.CurrentWeekKey) return; // тиждень минув — нові згенерує RollDay
            switch (kind)
            {
                case DailyTaskKind.Harvest:
                case DailyTaskKind.EarnCoins:
                    Progress(data, kind, amount, absolute: false);
                    break;
                case DailyTaskKind.Water:
                case DailyTaskKind.Sun:
                    Progress(data, DailyTaskKind.Care, amount, absolute: false);
                    break;
                case DailyTaskKind.Plant:
                    if (!string.IsNullOrEmpty(plantId) && !data.weeklyPlantedSpecies.Contains(plantId))
                    {
                        data.weeklyPlantedSpecies.Add(plantId);
                        Progress(data, DailyTaskKind.DistinctSpecies, data.weeklyPlantedSpecies.Count, absolute: true);
                    }
                    break;
            }
        }

        private static void Progress(PlayerData data, DailyTaskKind kind, int value, bool absolute)
        {
            bool changed = false;
            for (int i = 0; i < data.weeklyTasks.Count; i++)
            {
                var task = data.weeklyTasks[i];
                if (task.kind != kind || task.IsComplete) continue;
                int next = absolute ? value : task.progress + value;
                task.progress = Mathf.Min(task.target, next);
                changed = true;
                if (task.IsComplete) GameEvents.RaiseTaskCompleted(i, true); // картка «Завдання тижня виконано»
            }
            if (changed) GameEvents.RaiseDailyStateChanged();
        }

        // ── Нагороди ─────────────────────────────────────────────────────
        public static bool Claim(int index)
        {
            var gm = Gm;
            if (gm == null) return false;
            var tasks = gm.playerData.weeklyTasks;
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
            !data.weeklyChestClaimed && data.weeklyTasks.Count > 0 && data.weeklyTasks.All(t => t.claimed);

        /// <summary>Наступна ще не отримана нагорода з WeeklyRewardOrder; null — усе зібрано.</summary>
        public static (bool avatar, string id)? NextReward(PlayerData data)
        {
            foreach (var r in WeeklyRewardOrder)
            {
                bool owned = r.avatar ? data.ownedAvatarIds.Contains(r.id) : data.ownedFrameIds.Contains(r.id);
                if (!owned) return r;
            }
            return null;
        }

        /// <summary>«аватар «Троянда»» / «рамка «Ранкова»» / «N монет» — для рядка скрині.</summary>
        public static string DescribeChest(PlayerData data)
        {
            var r = NextReward(data);
            if (r == null) return $"{EconomyConfig.WeeklyChestCoins(data.level)} монет";
            return r.Value.avatar
                ? $"аватар «{ProfileItemCatalog.FindAvatar(r.Value.id)?.label}»"
                : $"рамка «{ProfileItemCatalog.FindFrame(r.Value.id)?.label}»";
        }

        public static bool ClaimChest()
        {
            var gm = Gm;
            if (gm == null || !IsChestAvailable(gm.playerData)) return false;
            var data = gm.playerData;
            string what = DescribeChest(data);
            var r = NextReward(data);
            data.weeklyChestClaimed = true;
            if (r == null) gm.AddCoins(EconomyConfig.WeeklyChestCoins(data.level));
            else if (r.Value.avatar) data.ownedAvatarIds.Add(r.Value.id);
            else data.ownedFrameIds.Add(r.Value.id);
            SaveSystem.Save(data);
            GameEvents.RaiseDailyStateChanged();
            GameEvents.RaiseToast($"Скриня тижня: {what}! Нові завдання — у понеділок");
            return true;
        }

        /// <summary>Скільки тижневих нагород чекає на тап (бейдж на кнопці завдань).</summary>
        public static int ClaimableCount(PlayerData data) =>
            data.weeklyTasks.Count(t => t.IsComplete && !t.claimed) + (IsChestAvailable(data) ? 1 : 0);
    }
}
