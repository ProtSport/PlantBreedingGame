using UnityEngine;

namespace PlantBreeding.Core
{
    /// <summary>Спільне форматування тривалості для UI і повідомлень.</summary>
    public static class TimeFormat
    {
        /// <summary>"2 год 15 хв" / "12 хв" / "40 с".</summary>
        public static string Remaining(double seconds)
        {
            int s = Mathf.CeilToInt((float)seconds);
            if (s >= 3600) return $"{s / 3600} год {(s % 3600) / 60} хв";
            if (s >= 60) return $"{Mathf.CeilToInt(s / 60f)} хв";
            return $"{Mathf.Max(0, s)} с";
        }
    }
}
