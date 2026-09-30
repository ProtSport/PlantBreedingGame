using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PlantBreeding.Core;
using PlantBreeding.Economy;
using PlantBreeding.Garden;
using PlantBreeding.Localization;
#if UNITY_ANDROID || UNITY_IOS
using Unity.Notifications;
#endif

namespace PlantBreeding.Notifications
{
    /// <summary>
    /// Локальні push-сповіщення (пакет com.unity.mobile.notifications, без
    /// сервера): коли гра йде у фон, плануються нагадування, а при поверненні
    /// всі скасовуються. Що і коли — BuildPlan (працює на будь-якій платформі,
    /// у редакторі план лише пишеться в консоль: меню PlantBreeding → Тест
    /// економіки → Показати план push-сповіщень).
    ///
    /// Сповіщення:
    ///  • перша рослина дозріла / усі рослини дозріли;
    ///  • рослина захворіла (хвороба заплановано з'явиться, поки гри немає);
    ///  • хвора рослина чекає на лікування (через 2 год);
    ///  • нагорода дня чекає (завтра о 19:00) — щоб не перервати стрік.
    /// Дозвіл питаємо не на старті, а після 2-го врожаю — коли гравець уже
    /// розуміє, навіщо сповіщення.
    /// </summary>
    public static class GameNotifications
    {
        private const string ChannelId = "garden";
        private const int PermissionAfterHarvests = 2;
        private const int StreakReminderHour = 19;
        private static readonly TimeSpan MinDelay = TimeSpan.FromMinutes(1);

        public readonly struct PlannedNotification
        {
            public readonly DateTime fireLocal;
            public readonly string title;
            public readonly string text;

            public PlannedNotification(DateTime fireLocal, string title, string text)
            {
                this.fireLocal = fireLocal;
                this.title = title;
                this.text = text;
            }

            public override string ToString() => $"{fireLocal:dd.MM HH:mm} — {title}: {text}";
        }

#if UNITY_ANDROID || UNITY_IOS
        private static bool _initialized;
#endif

        public static void Init()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (_initialized) return;
            var args = NotificationCenterArgs.Default;
            args.AndroidChannelId = ChannelId;
            args.AndroidChannelName = "Сад";
            args.AndroidChannelDescription = PlantAilments.Enabled
                ? "Урожай, хвороби рослин і щоденні нагороди"
                : "Урожай і щоденні нагороди";
            args.PresentationOptions = NotificationPresentation.Alert | NotificationPresentation.Badge | NotificationPresentation.Sound;
            NotificationCenter.Initialize(args);
            _initialized = true;
#endif
        }

        /// <summary>Попросити дозвіл, якщо гравець уже зібрав достатньо врожаїв і ми ще не питали.</summary>
        public static void MaybeRequestPermission(PlayerData data)
        {
            if (data.notificationPermissionAsked || data.totalHarvests < PermissionAfterHarvests) return;
            data.notificationPermissionAsked = true;
#if UNITY_ANDROID || UNITY_IOS
            Init();
            NotificationCenter.RequestPermission();
#else
            Debug.Log("[Notifications] Тут гравця попросять дозволити сповіщення (лише Android/iOS).");
#endif
        }

        /// <summary>Гра йде у фон / закривається: запланувати нагадування.</summary>
        public static void OnAppBackground(PlayerData data)
        {
            var plan = BuildPlan(data);
#if UNITY_ANDROID || UNITY_IOS
            Init();
            NotificationCenter.CancelAllScheduledNotifications();
            foreach (var n in plan)
            {
                var notification = new Notification { Title = n.title, Text = n.text };
                NotificationCenter.ScheduleNotification(notification, new NotificationDateTimeSchedule(n.fireLocal));
            }
#else
            if (plan.Count > 0) Debug.Log("[Notifications] План (лише Android/iOS):\n" + string.Join("\n", plan));
#endif
        }

        /// <summary>Гравець повернувся: скасувати заплановане й прибрати бейдж.</summary>
        public static void OnAppForeground()
        {
#if UNITY_ANDROID || UNITY_IOS
            Init();
            NotificationCenter.CancelAllScheduledNotifications();
            NotificationCenter.CancelAllDeliveredNotifications();
            NotificationCenter.ClearBadge();
#endif
        }

        /// <summary>Що і коли нагадати, якщо гравець зараз закриє гру.</summary>
        public static List<PlannedNotification> BuildPlan(PlayerData data)
        {
            var plan = new List<PlannedNotification>();
            DateTime nowUtc = GameClock.UtcNow;
            DateTime nowLocal = DateTime.Now;

            // Час гри → час пристрою: сповіщення планує ОС за годинником пристрою.
            void Add(DateTime atUtc, string title, string text)
            {
                TimeSpan delay = atUtc - nowUtc;
                if (delay < MinDelay) return;
                plan.Add(new PlannedNotification(nowLocal + delay, Loc.Translate(title), Loc.Translate(text)));
            }

            var garden = GardenManager.Current;
            if (garden != null)
            {
                var ready = new List<(DateTime at, PlotSlot slot)>();
                DateTime? firstAilment = null;
                PlotSlot ailingSlot = null;
                bool anySick = false;

                foreach (var slot in garden.Plots)
                {
                    if (slot.plant == null) continue;
                    if (slot.state == PlotState.Sick) { anySick = true; continue; }
                    if (slot.state != PlotState.Growing && slot.state != PlotState.NeedsWater) continue;

                    DateTime readyAt = nowUtc.AddSeconds(slot.GetRemainingSeconds());
                    var ailmentAt = slot.PendingAilmentAtUtc;
                    if (ailmentAt.HasValue && ailmentAt.Value < readyAt)
                    {
                        // Захворіє раніше, ніж дозріє → про дозрівання поки не нагадуємо.
                        if (!firstAilment.HasValue || ailmentAt.Value < firstAilment.Value)
                        {
                            firstAilment = ailmentAt.Value;
                            ailingSlot = slot;
                        }
                        continue;
                    }
                    ready.Add((readyAt, slot));
                }

                if (ready.Count > 0)
                {
                    var first = ready.OrderBy(r => r.at).First();
                    Add(first.at, $"{first.slot.plant.displayName} дозріла!", "Збери урожай і посади нову рослину");

                    var last = ready.OrderBy(r => r.at).Last();
                    if (ready.Count > 1 && (last.at - first.at).TotalMinutes > 30)
                        Add(last.at, "Усі рослини дозріли", "Сад чекає на збір урожаю");
                }

                if (firstAilment.HasValue)
                    Add(firstAilment.Value, $"{ailingSlot.plant.displayName} захворіла", "Поки рослина хворіє, вона не росте — загляни в сад");

                if (anySick)
                    Add(nowUtc.AddHours(2), "Рослина чекає на лікування", "Вилікуй її, щоб вона знову росла");
            }

            // Стрік: завтра о 19:00 за місцевим часом нагадати про нагороду дня.
            if (data.loginStreak > 0 || data.totalHarvests > 0)
            {
                int nextDay = data.loginStreak > 0 ? EconomyService.LoginCycleIndex(data) + 2 : 1;
                if (nextDay > EconomyConfig.LoginCycle.Length) nextDay = 1;
                DateTime tomorrowLocal = GameClock.LocalNow.Date.AddDays(1).AddHours(StreakReminderHour);
                Add(tomorrowLocal.ToUniversalTime(), "Нагорода дня чекає", $"Заходь забрати нагороду дня {nextDay} і нові завдання");
            }

            return plan;
        }
    }
}
