using System;

namespace PlantBreeding.Core
{
    /// <summary>
    /// Централізовані події гри. UI підписується на них замість того, щоб
    /// кожен елемент інтерфейсу опитував GameManager у Update() (polling).
    /// </summary>
    public static class GameEvents
    {
        public static event Action OnCurrencyChanged;
        public static event Action<int> OnLevelUp;
        public static event Action<int> OnPlotStateChanged; // передає slotIndex
        public static event Action<string> OnToast;          // коротке повідомлення гравцю (нагороди, підказки)
        public static event Action OnDailyStateChanged;      // щоденна нагорода/завдання змінились
        public static event Action OnDexBadgeChanged;        // нова рослина в колекції / Дендрарій переглянуто
        /// <summary>Продаж урожаю: id рослини (null — кілька, «Зібрати все»), підпис, монети.</summary>
        public static event Action<string, string, int> OnSale;
        /// <summary>Завдання виконано — індекс у PlayerData.dailyTasks або weeklyTasks (weekly = true); картка «Забрати».</summary>
        public static event Action<int, bool> OnTaskCompleted;

        public static void RaiseCurrencyChanged() => OnCurrencyChanged?.Invoke();
        public static void RaiseLevelUp(int newLevel) => OnLevelUp?.Invoke(newLevel);
        public static void RaisePlotStateChanged(int slotIndex) => OnPlotStateChanged?.Invoke(slotIndex);
        public static void RaiseToast(string message) => OnToast?.Invoke(message);
        public static void RaiseDailyStateChanged() => OnDailyStateChanged?.Invoke();
        public static void RaiseDexBadgeChanged() => OnDexBadgeChanged?.Invoke();
        public static void RaiseSale(string plantId, string title, int coins) => OnSale?.Invoke(plantId, title, coins);
        public static void RaiseTaskCompleted(int index, bool weekly = false) => OnTaskCompleted?.Invoke(index, weekly);
    }
}
