using System;
using UnityEngine;
using PlantBreeding.Core;
using PlantBreeding.Save;

namespace PlantBreeding.Lab
{
    /// <summary>Візуальний стан вузла — точна відповідність 4 станам зі скріну макету.</summary>
    public enum LabNodeState
    {
        Done,
        Active,
        Available,
        Locked
    }

    public enum LabStartResult
    {
        Ok,
        AlreadyMaxed,
        SlotBusy,
        NotEnoughCoins,
        NotEnoughGems
    }

    /// <summary>
    /// Чиста логіка Лабораторії (без UI), працює напряму з GameManager.playerData —
    /// той самий стиль, що PlantEconomy/PlantingForecast. Один активний слот
    /// дослідження на весь акаунт (Варіант А, свідомо обраний користувачем:
    /// стратегічний вибір "що качати першим" + гачок під другий слот за кристали
    /// пізніше). Реальний таймер зберігається як Unix-секунди (як PlotSaveData),
    /// тому прогрес не втрачається між сесіями.
    /// </summary>
    public static class LabResearchService
    {
        public const int NodesPerBranch = 4;

        // ════════════════════════════════════════════════════════════════
        //  ПРОГРЕС ГІЛОК
        // ════════════════════════════════════════════════════════════════
        public static int GetCompletedLevels(PlayerData data, LabBranchId branch)
        {
            var save = FindBranchSave(data, branch);
            return save?.completedLevels ?? 0;
        }

        public static int GetLabLevel(PlayerData data)
        {
            int total = 0;
            foreach (var branch in LabResearchCatalog.Branches)
                total += GetCompletedLevels(data, branch.id);
            return total;
        }

        static LabBranchSave FindBranchSave(PlayerData data, LabBranchId branch)
        {
            string id = branch.ToString();
            return data.labBranches.Find(b => b.branchId == id);
        }

        static LabBranchSave GetOrCreateBranchSave(PlayerData data, LabBranchId branch)
        {
            var save = FindBranchSave(data, branch);
            if (save == null)
            {
                save = new LabBranchSave(branch.ToString(), 0);
                data.labBranches.Add(save);
            }
            return save;
        }

        // ════════════════════════════════════════════════════════════════
        //  АКТИВНЕ ДОСЛІДЖЕННЯ (єдиний слот)
        // ════════════════════════════════════════════════════════════════
        public static bool HasActiveResearch(PlayerData data) => !string.IsNullOrEmpty(data.labActive?.branchId);

        public static bool IsActiveOn(PlayerData data, LabBranchId branch) =>
            HasActiveResearch(data) && data.labActive.branchId == branch.ToString();

        public static LabNodeState GetNodeState(PlayerData data, LabBranchId branch, int index)
        {
            int completed = GetCompletedLevels(data, branch);
            if (index < completed) return LabNodeState.Done;
            if (IsActiveOn(data, branch) && data.labActive.nodeIndex == index) return LabNodeState.Active;
            // "Доступно" лишається видимим навіть якщо БУДЬ-ЯКА гілка зараз зайнята —
            // тап на неї дасть повідомлення "спочатку заверши", а не сховає кнопку
            // (узгоджено з користувачем, Варіант А).
            if (index == completed) return LabNodeState.Available;
            return LabNodeState.Locked;
        }

        public static float GetActiveProgress01(PlayerData data)
        {
            if (!HasActiveResearch(data)) return 0f;
            var branch = ParseBranch(data.labActive.branchId);
            var def = LabResearchCatalog.GetNode(branch, data.labActive.nodeIndex);
            double elapsed = NowUnixSeconds() - data.labActive.startedAtUnixSeconds;
            return Mathf.Clamp01((float)(elapsed / def.durationSeconds));
        }

        public static double GetActiveRemainingSeconds(PlayerData data)
        {
            if (!HasActiveResearch(data)) return 0;
            var branch = ParseBranch(data.labActive.branchId);
            var def = LabResearchCatalog.GetNode(branch, data.labActive.nodeIndex);
            double elapsed = NowUnixSeconds() - data.labActive.startedAtUnixSeconds;
            return Math.Max(0, def.durationSeconds - elapsed);
        }

        /// <summary>Дешевшає лінійно від maxRushGemCost (щойно стартувало) до 1 (майже завершено).</summary>
        public static int GetRushGemCost(PlayerData data)
        {
            if (!HasActiveResearch(data)) return 0;
            var branch = ParseBranch(data.labActive.branchId);
            var def = LabResearchCatalog.GetNode(branch, data.labActive.nodeIndex);
            float remaining01 = 1f - GetActiveProgress01(data);
            if (remaining01 <= 0f) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(def.maxRushGemCost * remaining01));
        }

        /// <summary>Викликати періодично (раз/сек) поки екран відкрито — завершує вузол, якщо таймер вийшов.</summary>
        public static bool TickCompleteIfDone(PlayerData data)
        {
            if (!HasActiveResearch(data)) return false;
            if (GetActiveProgress01(data) < 1f) return false;
            CompleteActive(data);
            return true;
        }

        public static LabStartResult TryStartResearch(LabBranchId branch)
        {
            var gm = GameManager.Instance;
            if (gm == null) return LabStartResult.NotEnoughCoins;
            var data = gm.playerData;

            if (HasActiveResearch(data)) return LabStartResult.SlotBusy;

            int completed = GetCompletedLevels(data, branch);
            if (completed >= NodesPerBranch) return LabStartResult.AlreadyMaxed;

            var def = LabResearchCatalog.GetNode(branch, completed);
            if (data.coins < def.coinCost) return LabStartResult.NotEnoughCoins;
            if (data.gems < def.gemCost) return LabStartResult.NotEnoughGems;

            gm.TrySpendCoins(def.coinCost);
            if (def.gemCost > 0) gm.SpendGems(def.gemCost);

            data.labActive = new LabActiveResearch
            {
                branchId = branch.ToString(),
                nodeIndex = completed,
                startedAtUnixSeconds = NowUnixSeconds()
            };
            SaveSystem.Save(data);
            return LabStartResult.Ok;
        }

        public static bool TryRushActive()
        {
            var gm = GameManager.Instance;
            if (gm == null) return false;
            var data = gm.playerData;
            if (!HasActiveResearch(data)) return false;

            int cost = GetRushGemCost(data);
            if (cost > 0 && !gm.SpendGems(cost)) return false;

            CompleteActive(data);
            return true;
        }

        static void CompleteActive(PlayerData data)
        {
            var branch = ParseBranch(data.labActive.branchId);
            int index = data.labActive.nodeIndex;
            var def = LabResearchCatalog.GetNode(branch, index);

            var save = GetOrCreateBranchSave(data, branch);
            save.completedLevels = Math.Max(save.completedLevels, index + 1);

            data.labActive = new LabActiveResearch();

            if (def.gemReward > 0) GameManager.Instance?.AddGems(def.gemReward);
            SaveSystem.Save(data);
        }

        // ════════════════════════════════════════════════════════════════
        //  ЕФЕКТИ (застосовуються реальними системами гри)
        // ════════════════════════════════════════════════════════════════
        /// <summary>Модифікатор часу росту (від'ємний = швидше) — той самий формат, що PotData.growTimeModifier.</summary>
        public static float GetSpeedGrowModifier(PlayerData data)
        {
            int completed = GetCompletedLevels(data, LabBranchId.Speed);
            if (completed <= 0) return 0f;
            return LabResearchCatalog.GetNode(LabBranchId.Speed, completed - 1).magnitude;
        }

        /// <summary>Бонус до ціни продажу (вузли 1-2 гілки Врожайність; вузли 3-4 не додають до цього).</summary>
        public static float GetYieldPriceBonus(PlayerData data)
        {
            int completed = GetCompletedLevels(data, LabBranchId.Yield);
            if (completed <= 0) return 0f;
            int priceLevel = Math.Min(completed, 2);
            return LabResearchCatalog.GetNode(LabBranchId.Yield, priceLevel - 1).magnitude;
        }

        /// <summary>Шанс подвійного врожаю (розблоковується вузлами 3-4 гілки Врожайність).</summary>
        public static float GetYieldDoubleHarvestChance(PlayerData data)
        {
            int completed = GetCompletedLevels(data, LabBranchId.Yield);
            if (completed < 3) return 0f;
            return LabResearchCatalog.GetNode(LabBranchId.Yield, completed - 1).magnitude;
        }

        // ── Здоров'я (хвороби/шкідники — Garden/PlantAilments) ───────────
        /// <summary>Зниження шансу хвороби/шкідника: вузол 1 = −10%, вузол 2+ = −20%.</summary>
        public static float GetHealthAilmentReduction(PlayerData data)
        {
            int completed = GetCompletedLevels(data, LabBranchId.Health);
            if (completed <= 0) return 0f;
            return LabResearchCatalog.GetNode(LabBranchId.Health, Math.Min(completed, 2) - 1).magnitude;
        }

        /// <summary>Вузол 3 «швидше відновлення»: удвічі менше тапів на шкідника і дешевші ліки.</summary>
        public static bool HasFastRecovery(PlayerData data) => GetCompletedLevels(data, LabBranchId.Health) >= 3;

        /// <summary>Вузол 4 «імунітет до шкідників»: лишаються тільки хвороби.</summary>
        public static bool HasPestImmunity(PlayerData data) => GetCompletedLevels(data, LabBranchId.Health) >= 4;

        // ════════════════════════════════════════════════════════════════
        static LabBranchId ParseBranch(string id) => (LabBranchId)Enum.Parse(typeof(LabBranchId), id);

        static double NowUnixSeconds() =>
            GameClock.NowUnixSeconds; // захищений від перевідного годинника
    }
}
