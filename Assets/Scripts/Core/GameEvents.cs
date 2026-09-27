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

        public static void RaiseCurrencyChanged() => OnCurrencyChanged?.Invoke();
        public static void RaiseLevelUp(int newLevel) => OnLevelUp?.Invoke(newLevel);
        public static void RaisePlotStateChanged(int slotIndex) => OnPlotStateChanged?.Invoke(slotIndex);
        public static void RaiseToast(string message) => OnToast?.Invoke(message);
        public static void RaiseDailyStateChanged() => OnDailyStateChanged?.Invoke();
        public static void RaiseDexBadgeChanged() => OnDexBadgeChanged?.Invoke();
    }
}
