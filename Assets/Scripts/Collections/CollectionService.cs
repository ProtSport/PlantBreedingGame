using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using PlantBreeding.Core;
using PlantBreeding.Economy;
using PlantBreeding.Garden;
using PlantBreeding.Save;

namespace PlantBreeding.Collections
{
    /// <summary>
    /// Логіка колекцій Дендрарію поверх GameManager.playerData (той самий стиль,
    /// що EconomyService). Прогрес рахується від реального стану гравця:
    /// відкриті види, врожаї, вилікувані хвороби, куплені горщики.
    ///
    /// Зібрана колекція не видає нагороду сама — гравець забирає її кнопкою
    /// в Дендрарії (бейдж на іконці нагадує). Після цього діє постійний бонус
    /// колекції (WaterInterval/SellPrice/…), який читають PlotSlot,
    /// PlantingForecast, PlantAilments і EconomyService.
    /// Каталог — CollectionCatalog, план — docs/ECONOMY.md.
    /// </summary>
    public static class CollectionService
    {
        private static GameManager Gm => GameManager.Instance;

        // ════════════════════════════════════════════════════════════════
        //  ПРОГРЕС
        // ════════════════════════════════════════════════════════════════
        public static IReadOnlyList<string> SlotKeys(CollectionDef def) =>
            def.slotKeys ?? (IReadOnlyList<string>)PlantCatalog.All.Select(p => p.plantId).ToList();

        public static bool IsSlotDone(PlayerData data, CollectionDef def, string key) => def.goal switch
        {
            CollectionGoal.DiscoverPlant => data.discoveredPlantIds.Contains(key),
            CollectionGoal.PerfectCare => data.perfectCarePlantIds.Contains(key),
            CollectionGoal.HarvestPlant => HarvestCount(data, key) >= CollectionCatalog.VeteranHarvests,
            CollectionGoal.CureAilment => int.TryParse(key, out int kind) && data.curedAilmentKinds.Contains(kind),
            CollectionGoal.OwnPot => data.ownedPotIds.Contains(key),
            CollectionGoal.CompleteCollection => IsComplete(data, CollectionCatalog.Get(key)),
            _ => false
        };

        public static int Progress(PlayerData data, CollectionDef def) =>
            SlotKeys(def).Count(k => IsSlotDone(data, def, k));

        public static int Total(CollectionDef def) => SlotKeys(def).Count;

        public static bool IsComplete(PlayerData data, CollectionDef def) =>
            def != null && Progress(data, def) >= Total(def);

        public static bool IsClaimed(PlayerData data, CollectionDef def) =>
            def != null && data.claimedCollectionIds.Contains(def.id);

        public static bool CanClaim(PlayerData data, CollectionDef def) =>
            IsComplete(data, def) && !IsClaimed(data, def);

        public static int HarvestCount(PlayerData data, string plantId)
        {
            var s = data.harvestCounts.Find(c => c.plantId == plantId);
            return s?.count ?? 0;
        }

        public static int CompletedCount(PlayerData data) => CollectionCatalog.All.Count(c => IsComplete(data, c));

        /// <summary>Скільки нагород чекає в Дендрарії (бейдж на іконці).</summary>
        public static int ClaimableCount(PlayerData data)
        {
            int n = CollectionCatalog.All.Count(c => CanClaim(data, c));
            if (IsWeekClaimable(data)) n++;
            return n;
        }

        // ════════════════════════════════════════════════════════════════
        //  ПОДІЇ ГРИ
        // ════════════════════════════════════════════════════════════════
        /// <summary>Вид відкрито вперше (GameManager.RecordPlantDiscovered).</summary>
        public static void OnPlantDiscovered(string plantId)
        {
            var gm = Gm;
            if (gm == null) return;
            ToastSlotProgress(gm.playerData, CollectionGoal.DiscoverPlant, plantId);
            CheckCompletions();
        }

        /// <summary>
        /// Урожай зібрано (EconomyService.GrantHarvest). perfectCare — жоден
        /// полив не пропущено (рахує PlotSlot.Harvest).
        /// </summary>
        public static void OnHarvest(string plantId, bool perfectCare)
        {
            var gm = Gm;
            if (gm == null || string.IsNullOrEmpty(plantId)) return;
            var data = gm.playerData;

            var stack = data.harvestCounts.Find(c => c.plantId == plantId);
            if (stack == null)
            {
                stack = new SeedStack(plantId, 0);
                data.harvestCounts.Add(stack);
            }
            stack.count++;
            if (stack.count == CollectionCatalog.VeteranHarvests)
                ToastSlotProgress(data, CollectionGoal.HarvestPlant, plantId);

            if (perfectCare && !data.perfectCarePlantIds.Contains(plantId))
            {
                data.perfectCarePlantIds.Add(plantId);
                ToastSlotProgress(data, CollectionGoal.PerfectCare, plantId);
            }

            TrackWeekHarvest(data, plantId);
            CheckCompletions();
        }

        /// <summary>Рослину вилікувано (PlotSlot.Cure).</summary>
        public static void OnAilmentCured(AilmentKind kind)
        {
            var gm = Gm;
            if (gm == null || kind == AilmentKind.None) return;
            var data = gm.playerData;
            if (data.curedAilmentKinds.Contains((int)kind)) return;
            data.curedAilmentKinds.Add((int)kind);
            ToastSlotProgress(data, CollectionGoal.CureAilment, ((int)kind).ToString());
            CheckCompletions();
        }

        /// <summary>Горщик куплено (GameManager.TryUnlockPot).</summary>
        public static void OnPotOwned(string potId)
        {
            var gm = Gm;
            if (gm == null) return;
            ToastSlotProgress(gm.playerData, CollectionGoal.OwnPot, potId);
            CheckCompletions();
        }

        /// <summary>Тост «Колекція «X»: 2/4» для незавершених колекцій, куди потрапив слот.</summary>
        private static void ToastSlotProgress(PlayerData data, CollectionGoal goal, string key)
        {
            foreach (var def in CollectionCatalog.All)
            {
                if (def.goal != goal || !SlotKeys(def).Contains(key)) continue;
                int have = Progress(data, def), total = Total(def);
                if (have < total) GameEvents.RaiseToast($"Колекція «{def.name}»: {have}/{total}");
            }
        }

        /// <summary>Один раз повідомляє про кожну щойно зібрану колекцію.</summary>
        public static void CheckCompletions()
        {
            var gm = Gm;
            if (gm == null) return;
            var data = gm.playerData;
            bool changed = false;
            foreach (var def in CollectionCatalog.All)
            {
                if (!CanClaim(data, def) || data.announcedCollectionIds.Contains(def.id)) continue;
                data.announcedCollectionIds.Add(def.id);
                GameEvents.RaiseToast($"Колекцію «{def.name}» зібрано! Забери нагороду в Дендрарії");
                changed = true;
            }
            if (changed) SaveSystem.Save(data);
            GameEvents.RaiseDexBadgeChanged();
        }

        // ════════════════════════════════════════════════════════════════
        //  НАГОРОДИ
        // ════════════════════════════════════════════════════════════════
        public static bool Claim(string collectionId)
        {
            var gm = Gm;
            var def = CollectionCatalog.Get(collectionId);
            if (gm == null || def == null) return false;
            var data = gm.playerData;
            if (!CanClaim(data, def)) return false;

            var r = def.reward;
            // Старий сейв: кристал за цю колекцію вже видано автоматично.
            int gems = data.completedCollectionIds.Contains(def.id) ? 0 : r.gems;
            if (gems > 0) gm.AddGems(gems);
            if (r.coins > 0) gm.AddCoins(r.coins);
            if (!string.IsNullOrEmpty(r.avatarId) && !data.ownedAvatarIds.Contains(r.avatarId)) data.ownedAvatarIds.Add(r.avatarId);
            if (!string.IsNullOrEmpty(r.frameId) && !data.ownedFrameIds.Contains(r.frameId)) data.ownedFrameIds.Add(r.frameId);
            if (!string.IsNullOrEmpty(r.potId) && !data.ownedPotIds.Contains(r.potId)) data.ownedPotIds.Add(r.potId);
            if (!string.IsNullOrEmpty(r.titleId))
            {
                if (!data.ownedTitleIds.Contains(r.titleId)) data.ownedTitleIds.Add(r.titleId);
                data.activeTitleId = r.titleId;
            }

            data.claimedCollectionIds.Add(def.id);
            if (!data.completedCollectionIds.Contains(def.id)) data.completedCollectionIds.Add(def.id);
            SaveSystem.Save(data);
            GameEvents.RaiseToast($"«{def.name}»: {DescribeReward(def, gems)}");
            GameEvents.RaiseDexBadgeChanged();
            return true;
        }

        /// <summary>Нагорода одним рядком: «1 кристал · 100 монет · аватар «Кактус» · …».</summary>
        public static string DescribeReward(CollectionDef def) => DescribeReward(def, def.reward.gems);

        private static string DescribeReward(CollectionDef def, int gems)
        {
            var r = def.reward;
            var parts = new List<string>();
            if (gems > 0) parts.Add(gems == 1 ? "1 кристал" : $"{gems} кристали");
            if (r.coins > 0) parts.Add($"{r.coins} монет");
            if (!string.IsNullOrEmpty(r.avatarId)) parts.Add($"аватар «{AvatarName(r.avatarId)}»");
            if (!string.IsNullOrEmpty(r.frameId)) parts.Add($"рамка «{ProfileItemCatalog.FindFrame(r.frameId)?.label ?? r.frameId}»");
            if (!string.IsNullOrEmpty(r.potId)) parts.Add($"горщик «{PotName(r.potId)}»");
            if (!string.IsNullOrEmpty(r.titleId)) parts.Add($"титул «{r.titleId}»");
            string bonus = DescribeBonus(def);
            if (bonus != null) parts.Add(bonus);
            return string.Join(" · ", parts);
        }

        public static string DescribeBonus(CollectionDef def)
        {
            int pct = Mathf.RoundToInt(def.reward.bonusValue * 100f);
            return def.reward.bonus switch
            {
                CollectionBonusKind.WaterInterval => $"ці види просять води рідше на {pct}%",
                CollectionBonusKind.SellPrice => $"+{pct}% ціна продажу цих видів",
                CollectionBonusKind.AilmentResist => $"−{pct}% хвороб цих видів",
                CollectionBonusKind.Xp => $"+{pct}% XP за ці види",
                CollectionBonusKind.CarePerWatering =>
                    $"полив +{Mathf.RoundToInt((EconomyConfig.CareBonusPerWatering + def.reward.bonusValue) * 100f)}% до ціни замість +{Mathf.RoundToInt(EconomyConfig.CareBonusPerWatering * 100f)}%",
                _ => null
            };
        }

        private static string AvatarName(string id) => ProfileItemCatalog.FindAvatar(id)?.label ?? id;

        private static string PotName(string id)
        {
            var pot = Resources.Load<PotData>("Pots/" + id);
            return pot != null ? pot.displayName : id;
        }

        // ════════════════════════════════════════════════════════════════
        //  ПОСТІЙНІ БОНУСИ (діють після отримання нагороди)
        // ════════════════════════════════════════════════════════════════
        private static float Bonus(CollectionBonusKind kind, string plantId)
        {
            var gm = Gm;
            if (gm == null) return 0f;
            var data = gm.playerData;
            float sum = 0f;
            foreach (var def in CollectionCatalog.All)
            {
                if (def.reward.bonus != kind || !data.claimedCollectionIds.Contains(def.id)) continue;
                if (kind != CollectionBonusKind.CarePerWatering && !SlotKeys(def).Contains(plantId)) continue;
                sum += def.reward.bonusValue;
            }
            return sum;
        }

        public static float WaterIntervalBonus(string plantId) => Bonus(CollectionBonusKind.WaterInterval, plantId);
        public static float SellPriceBonus(string plantId) => Bonus(CollectionBonusKind.SellPrice, plantId);
        public static float AilmentResist(string plantId) => Bonus(CollectionBonusKind.AilmentResist, plantId);
        public static float XpBonus(string plantId) => Bonus(CollectionBonusKind.Xp, plantId);

        /// <summary>Бонус до ціни за один вчасний полив (базовий + «Ідеальний догляд»).</summary>
        public static float CareBonusPerWatering =>
            EconomyConfig.CareBonusPerWatering + Bonus(CollectionBonusKind.CarePerWatering, null);

        // ════════════════════════════════════════════════════════════════
        //  КОЛЕКЦІЯ ТИЖНЯ
        // ════════════════════════════════════════════════════════════════
        public const int WeekSlots = 4;
        public const int WeekSeedCount = 3;

        public static int WeekRewardCoins(int level) => 150 + 15 * Mathf.Max(1, level);

        private static string WeekKeyNow
        {
            get
            {
                var now = GameClock.LocalNow;
                return $"{ISOWeek.GetYear(now)}-W{ISOWeek.GetWeekOfYear(now):D2}";
            }
        }

        public static int WeekDaysLeft()
        {
            var now = GameClock.LocalNow;
            int days = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            return days == 0 ? 7 : days;
        }

        /// <summary>
        /// Новий ISO-тиждень → нові 4 види з уже відкритих (детерміновано на
        /// тиждень) і обнулений прогрес. Поки відкрито менше 4 видів — тижня немає.
        /// </summary>
        public static void EnsureWeek(PlayerData data)
        {
            string key = WeekKeyNow;
            bool changed = false;
            if (data.weekKey != key)
            {
                data.weekKey = key;
                data.weekPlantIds.Clear();
                data.weekHarvestedIds.Clear();
                data.weekClaimed = false;
                changed = true;
            }
            if (data.weekPlantIds.Count == 0 && data.discoveredPlantIds.Count >= WeekSlots)
            {
                var pool = new List<string>(data.discoveredPlantIds);
                pool.Sort(StringComparer.Ordinal); // стабільний порядок перед детермінованим вибором
                var now = GameClock.LocalNow;
                var rng = new System.Random(ISOWeek.GetYear(now) * 100 + ISOWeek.GetWeekOfYear(now));
                for (int i = 0; i < WeekSlots; i++)
                {
                    int idx = rng.Next(pool.Count);
                    data.weekPlantIds.Add(pool[idx]);
                    pool.RemoveAt(idx);
                }
                changed = true;
            }
            if (changed) SaveSystem.Save(data);
        }

        public static bool HasWeek(PlayerData data) => data.weekKey == WeekKeyNow && data.weekPlantIds.Count == WeekSlots;

        public static bool IsWeekComplete(PlayerData data) =>
            HasWeek(data) && data.weekPlantIds.All(id => data.weekHarvestedIds.Contains(id));

        public static bool IsWeekClaimable(PlayerData data) => IsWeekComplete(data) && !data.weekClaimed;

        /// <summary>Найдорожчий вид тижня — його насіння в нагороді.</summary>
        public static string WeekSeedPlantId(PlayerData data) =>
            data.weekPlantIds.Select(PlantCatalog.Get).Where(p => p != null)
                .OrderByDescending(p => p.seedCost).Select(p => p.plantId).FirstOrDefault();

        public static string DescribeWeekReward(PlayerData data)
        {
            var seed = PlantCatalog.Get(WeekSeedPlantId(data));
            string s = $"{WeekRewardCoins(data.level)} монет";
            if (seed != null) s += $" · {WeekSeedCount} насіння «{seed.displayName}»";
            return s;
        }

        private static void TrackWeekHarvest(PlayerData data, string plantId)
        {
            EnsureWeek(data);
            if (!HasWeek(data) || !data.weekPlantIds.Contains(plantId) || data.weekHarvestedIds.Contains(plantId)) return;
            data.weekHarvestedIds.Add(plantId);
            if (IsWeekComplete(data))
                GameEvents.RaiseToast("Колекцію тижня зібрано! Забери нагороду в Дендрарії");
            else
                GameEvents.RaiseToast($"Колекція тижня: {data.weekHarvestedIds.Count}/{WeekSlots}");
        }

        public static bool ClaimWeek()
        {
            var gm = Gm;
            if (gm == null) return false;
            var data = gm.playerData;
            if (!IsWeekClaimable(data)) return false;

            data.weekClaimed = true;
            gm.AddCoins(WeekRewardCoins(data.level));
            string seedId = WeekSeedPlantId(data);
            if (seedId != null) gm.AddSeeds(seedId, WeekSeedCount);
            SaveSystem.Save(data);
            GameEvents.RaiseToast($"Колекція тижня: {DescribeWeekReward(data)}");
            GameEvents.RaiseDexBadgeChanged();
            return true;
        }
    }
}
