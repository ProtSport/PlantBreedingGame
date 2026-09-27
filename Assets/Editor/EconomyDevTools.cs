using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using PlantBreeding.Core;
using PlantBreeding.Economy;
using PlantBreeding.Garden;
using PlantBreeding.Notifications;

namespace PlantBreeding.EditorTools
{
    /// <summary>
    /// Інструменти для перевірки щоденної петлі економіки без очікування
    /// реального часу. Меню: PlantBreeding → Тест економіки.
    /// Пункти з "(Play)" працюють лише в Play-режимі.
    /// </summary>
    public static class EconomyDevTools
    {
        const string Root = "PlantBreeding/Тест економіки/";

        [MenuItem(Root + "Скинути прогрес (новий гравець)")]
        static void ResetProgress()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Скидання прогресу", "Спершу зупини Play-режим.", "OK");
                return;
            }
            string path = Path.Combine(Application.persistentDataPath, "save.json");
            if (File.Exists(path)) File.Delete(path);
            Debug.Log($"[Economy] Збереження видалено ({path}). Наступний Play = новий гравець: 2 фіалки, 0 монет, 3 кристали.");
        }

        [MenuItem(Root + "Перемотати час +1 год (Play)")]
        static void SkipHour() => SkipTime(TimeSpan.FromHours(1));

        [MenuItem(Root + "Перемотати час +8 год (Play)")]
        static void SkipEightHours() => SkipTime(TimeSpan.FromHours(8));

        [MenuItem(Root + "Симулювати наступний день (Play)")]
        static void NextDay()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            // "Вчора" як останній вхід → RollDay зарахує продовження стріку.
            gm.playerData.lastLoginDay = EconomyService.DayKey(GameClock.LocalNow.AddDays(-1));
            EconomyService.RollDay(gm.playerData);
            Debug.Log($"[Economy] Новий день: стрік {gm.playerData.loginStreak}, нові завдання згенеровано.");
        }

        [MenuItem(Root + "Показати план push-сповіщень (Play)")]
        static void ShowNotificationPlan()
        {
            var plan = GameNotifications.BuildPlan(GameManager.Instance.playerData);
            Debug.Log(plan.Count == 0
                ? "[Notifications] Якщо закрити гру зараз — сповіщень не буде."
                : "[Notifications] Якщо закрити гру зараз:\n" + string.Join("\n", plan));
        }

        [MenuItem(Root + "Зробити рослини хворими (Play)")]
        static void MakeSick()
        {
            var garden = UnityEngine.Object.FindFirstObjectByType<GardenManager>();
            if (garden == null) return;
            int i = 0;
            foreach (var plot in garden.Plots)
            {
                if (plot.plant == null || (plot.state != PlotState.Growing && plot.state != PlotState.NeedsWater)) continue;
                // По черзі: шкідник / хвороба — щоб перевірити обидва типи лікування.
                plot.ailment = i++ % 2 == 0 ? AilmentKind.Aphids : AilmentKind.PowderyMildew;
                plot.ailmentAtFraction = 0f;
                plot.Tick();
            }
            Debug.Log($"[Economy] Захворіло рослин: {i}. Тапни хвору грядку, щоб лікувати.");
        }

        [MenuItem(Root + "+500 монет (Play)")]
        static void AddCoins() => GameManager.Instance?.AddCoins(500);

        [MenuItem(Root + "+100 XP (Play)")]
        static void AddXp() => GameManager.Instance?.AddXp(100);

        [MenuItem(Root + "Перемотати час +1 год (Play)", true)]
        [MenuItem(Root + "Перемотати час +8 год (Play)", true)]
        [MenuItem(Root + "Симулювати наступний день (Play)", true)]
        [MenuItem(Root + "Показати план push-сповіщень (Play)", true)]
        [MenuItem(Root + "Зробити рослини хворими (Play)", true)]
        [MenuItem(Root + "+500 монет (Play)", true)]
        [MenuItem(Root + "+100 XP (Play)", true)]
        static bool IsPlaying() => Application.isPlaying && GameManager.Instance != null;

        static void SkipTime(TimeSpan span)
        {
            var garden = UnityEngine.Object.FindFirstObjectByType<GardenManager>();
            if (garden == null) return;
            foreach (var plot in garden.Plots)
            {
                if (plot.plant == null) continue;
                plot.plantedAtUtc -= span;
                plot.lastWateredUtc -= span;
            }
            Debug.Log($"[Economy] Час грядок перемотано на {span.TotalHours} год.");
        }
    }
}
