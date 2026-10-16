using Firebase.Analytics;
using PlantBreeding.Core;

namespace PlantBreeding.Cloud
{
    /// <summary>
    /// Google Analytics for Firebase — ключові кроки гравця, щоб бачити, де
    /// новачки йдуть з гри (RELEASE-PLAN п. 6). Покупки в магазинах застосунків
    /// Analytics рахує сам (in_app_purchase). Поки Firebase не готовий
    /// (редактор, офлайн-старт) — виклики просто ігноруються.
    /// </summary>
    public static class Telemetry
    {
        private static bool _subscribed;

        public static void Init()
        {
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
            if (_subscribed) return;
            _subscribed = true;
            GameEvents.OnLevelUp += level =>
                Log(FirebaseAnalytics.EventLevelUp, new Parameter(FirebaseAnalytics.ParameterLevel, level));
        }

        public static void SetUserId(string uid)
        {
            if (CloudService.Ready) FirebaseAnalytics.SetUserId(uid);
        }

        public static void Log(string name, params Parameter[] parameters)
        {
            if (!CloudService.Ready) return;
            if (parameters == null || parameters.Length == 0) FirebaseAnalytics.LogEvent(name);
            else FirebaseAnalytics.LogEvent(name, parameters);
        }

        // ── Події гри ──────────────────────────────────────────────────────
        public static void Plant(string plantId, string potId) =>
            Log("plant", new Parameter("plant_id", plantId), new Parameter("pot_id", potId ?? ""));

        public static void Harvest(string plantId, int coins, int totalHarvests)
        {
            Log("harvest", new Parameter("plant_id", plantId), new Parameter("coins", coins));
            // Перший урожай — кінець «навчання» (воронка новачка в Analytics).
            if (totalHarvests == 1) Log(FirebaseAnalytics.EventTutorialComplete);
        }

        public static void Care(string action) => Log("care", new Parameter("action", action));

        public static void SpeedUp(int gems) =>
            Log(FirebaseAnalytics.EventSpendVirtualCurrency,
                new Parameter(FirebaseAnalytics.ParameterVirtualCurrencyName, "gems"),
                new Parameter(FirebaseAnalytics.ParameterValue, gems),
                new Parameter(FirebaseAnalytics.ParameterItemName, "speed_up"));
    }
}
