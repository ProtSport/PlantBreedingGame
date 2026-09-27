using UnityEngine;
using PlantBreeding.Core;
using PlantBreeding.Lab;

namespace PlantBreeding.Garden
{
    public enum AilmentKind
    {
        None,
        Aphids,        // Тля — шкідник
        SpiderMite,    // Павутинний кліщ — шкідник
        PowderyMildew, // Борошниста роса — хвороба
        Chlorosis      // Хлороз — хвороба
    }

    public readonly struct AilmentDef
    {
        public readonly AilmentKind kind;
        public readonly string name;
        public readonly bool isPest;
        public readonly string description;
        /// <summary>Шкідник: скільки тапів, щоб прибрати. Хвороба: 0.</summary>
        public readonly int taps;
        /// <summary>Хвороба: назва засобу, що купується за монети.</summary>
        public readonly string remedyName;

        public AilmentDef(AilmentKind kind, string name, bool isPest, string description, int taps, string remedyName)
        {
            this.kind = kind;
            this.name = name;
            this.isPest = isPest;
            this.description = description;
            this.taps = taps;
            this.remedyName = remedyName;
        }
    }

    /// <summary>
    /// Хвороби і шкідники — MVP розділу 4 GDD (docs/ECONOMY.md, «Хвороби і шкідники»).
    /// Поки рослина хворіє, вона НЕ росте: це друга причина заходити в гру,
    /// окрім урожаю. Шкідників прибирають тапами (безкоштовно, але треба бути
    /// в грі), хвороби лікують засобом за монети. Рослина ніколи не гине.
    ///
    /// Чи захворіє рослина і коли — вирішується в момент посадки (RollAtPlanting),
    /// тож хвороба «чекає» і на гравця, який закрив гру.
    /// </summary>
    public static class PlantAilments
    {
        public static readonly AilmentDef[] All =
        {
            new AilmentDef(AilmentKind.Aphids, "Тля", true,
                "Зелені крапки на листі. Прибери їх — тапай по листочках.", 6, null),
            new AilmentDef(AilmentKind.SpiderMite, "Павутинний кліщ", true,
                "Павутинка між листками. Обприскай листя — тапай, поки не зникне.", 10, null),
            new AilmentDef(AilmentKind.PowderyMildew, "Борошниста роса", false,
                "Білий наліт на листі — грибок. Потрібен фунгіцид.", 0, "Фунгіцид"),
            new AilmentDef(AilmentKind.Chlorosis, "Хлороз", false,
                "Листя жовтіє — бракує заліза. Потрібне залізне добриво.", 0, "Залізне добриво"),
        };

        /// <summary>Хвороби не з'являються, доки гравець не зібрав стільки врожаїв (навчання без відволікань).</summary>
        public const int GraceHarvests = 4;
        /// <summary>Короткі рослини (фіалка, кактус) не хворіють.</summary>
        public const float MinGrowSecondsForAilments = 15f * 60f;

        public static AilmentDef Get(AilmentKind kind)
        {
            foreach (var d in All)
                if (d.kind == kind) return d;
            return All[0];
        }

        /// <summary>
        /// Базовий шанс за посадку: 10% + 2% за годину росту, максимум 30%. Підібрано
        /// симуляцією разом із кривою XP, щоб темп лишився 10–14 днів (docs/ECONOMY.md).
        /// </summary>
        public static float BaseChance(float growSeconds)
        {
            if (growSeconds < MinGrowSecondsForAilments) return 0f;
            return Mathf.Min(0.30f, 0.10f + 0.02f * growSeconds / 3600f);
        }

        /// <summary>
        /// Вирішує в момент посадки, чи буде хвороба і на якій частці росту (0.2…0.7).
        /// Враховує горщик і гілку Лабораторії «Здоров'я».
        /// </summary>
        public static (AilmentKind kind, float atFraction) RollAtPlanting(PlantData plant, PotData pot, float growSeconds)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.playerData.totalHarvests < GraceHarvests) return (AilmentKind.None, -1f);

            float chance = BaseChance(growSeconds)
                * (1f - (pot != null ? pot.ailmentResistance : 0f))
                * (1f - LabResearchService.GetHealthAilmentReduction(gm.playerData));
            if (Random.value >= chance) return (AilmentKind.None, -1f);

            bool pestImmune = LabResearchService.HasPestImmunity(gm.playerData);
            AilmentKind kind;
            if (pestImmune || Random.value >= 0.6f)
                kind = Random.value < 0.5f ? AilmentKind.PowderyMildew : AilmentKind.Chlorosis;
            else
                kind = Random.value < 0.5f ? AilmentKind.Aphids : AilmentKind.SpiderMite;

            return (kind, Random.Range(0.2f, 0.7f));
        }

        /// <summary>Скільки тапів треба на шкідника (гілка «Здоров'я» вузол 3 — удвічі менше).</summary>
        public static int PestTaps(AilmentKind kind)
        {
            int taps = Get(kind).taps;
            var gm = GameManager.Instance;
            if (gm != null && LabResearchService.HasFastRecovery(gm.playerData)) taps = Mathf.Max(2, taps / 2);
            return taps;
        }

        /// <summary>Ціна засобу від хвороби: 30% ціни насіння (мін. 8), удвічі дешевше з вузлом 3 «Здоров'я».</summary>
        public static int RemedyCost(PlantData plant)
        {
            int cost = Mathf.Max(8, Mathf.RoundToInt(plant.seedCost * 0.3f));
            var gm = GameManager.Instance;
            if (gm != null && LabResearchService.HasFastRecovery(gm.playerData)) cost = Mathf.Max(4, cost / 2);
            return cost;
        }
    }
}
