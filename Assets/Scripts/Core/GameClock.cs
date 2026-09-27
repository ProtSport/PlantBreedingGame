using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace PlantBreeding.Core
{
    /// <summary>
    /// Ігровий час, захищений від перевідного годинника пристрою. УСЯ логіка
    /// часу (ріст, полив, прискорення, щоденні нагороди, лабораторія) бере
    /// час звідси, а не з DateTime.UtcNow.
    ///
    /// Два захисти:
    ///  1. Мережевий час. На старті й після повернення з фону береться час
    ///     сервера (HTTP-заголовок Date). Далі в межах сесії час рахується як
    ///     «час сервера + скільки минуло за монотонним таймером Unity», тож
    ///     перевід годинника пристрою під час гри ні на що не впливає.
    ///  2. Час ніколи не йде назад. Найбільший бачений момент зберігається в
    ///     сейві (PlayerData.lastSeenUnixSeconds). Якщо гравець офлайн перевів
    ///     годинник уперед і зібрав урожай, то після повернення реального часу
    ///     ігровий час «замерзає», поки реальний його не наздожене. Виграш
    ///     повертається штрафом, і повторювати трюк немає сенсу.
    ///
    /// Без мережі використовується годинник пристрою (з захистом №2).
    /// Часовий пояс береться з пристрою (межа дня = місцева північ).
    /// </summary>
    public static class GameClock
    {
        // Кілька незалежних джерел: беремо перше, що відповіло.
        private static readonly string[] TimeSources =
        {
            "https://www.google.com",
            "https://www.cloudflare.com",
            "https://www.microsoft.com",
        };

        private const int RequestTimeoutSeconds = 5;

        private static bool _hasNetworkAnchor;
        private static double _anchorServerUnix;
        private static double _anchorRealtime;
        private static double _lastSeenUnix;
        private static bool _syncing;

        /// <summary>true, якщо в цій сесії час підтверджено сервером.</summary>
        public static bool IsNetworkSynced => _hasNetworkAnchor;

        public static double LastSeenUnixSeconds => _lastSeenUnix;

        public static DateTime UtcNow => DateTime.UnixEpoch.AddSeconds(NowUnixSeconds);

        /// <summary>Місцевий час пристрою для меж дня (щоденні нагороди/завдання).</summary>
        public static DateTime LocalNow => UtcNow.ToLocalTime();

        public static double NowUnixSeconds
        {
            get
            {
                double now = _hasNetworkAnchor
                    ? _anchorServerUnix + (Time.realtimeSinceStartupAsDouble - _anchorRealtime)
                    : DeviceUnixSeconds;

                // Монотонність: ніколи не раніше за вже бачений момент.
                if (now < _lastSeenUnix) now = _lastSeenUnix;
                else _lastSeenUnix = now;
                return now;
            }
        }

        private static double DeviceUnixSeconds => (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

        /// <summary>
        /// Гра йде у фон: мережевий якір скидається (монотонний таймер Unity
        /// на деяких платформах не рахує час у фоні). Після повернення до
        /// нової синхронізації діє годинник пристрою + захист «не назад».
        /// </summary>
        public static void OnPause() => _hasNetworkAnchor = false;

        /// <summary>Викликати одразу після завантаження сейва.</summary>
        public static void Init(PlayerData data)
        {
            _lastSeenUnix = Math.Max(_lastSeenUnix, data.lastSeenUnixSeconds);
        }

        /// <summary>Записати найбільший бачений момент у сейв (викликає SaveSystem.Save).</summary>
        public static void WriteTo(PlayerData data)
        {
            data.lastSeenUnixSeconds = Math.Max(data.lastSeenUnixSeconds, NowUnixSeconds);
        }

        /// <summary>
        /// Запитує мережевий час. onSynced викликається лише при успіху (щоб
        /// після синхронізації перерахувати день/грядки).
        /// </summary>
        public static IEnumerator Sync(Action onSynced)
        {
            if (_syncing) yield break;
            _syncing = true;
            try
            {
                foreach (var url in TimeSources)
                {
                    using (var req = UnityWebRequest.Head(url))
                    {
                        req.timeout = RequestTimeoutSeconds;
                        double sentAt = Time.realtimeSinceStartupAsDouble;
                        yield return req.SendWebRequest();
                        if (req.result != UnityWebRequest.Result.Success) continue;

                        string header = req.GetResponseHeader("Date");
                        if (string.IsNullOrEmpty(header)
                            || !DateTime.TryParseExact(header, "r", CultureInfo.InvariantCulture,
                                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var serverUtc))
                            continue;

                        // Заголовок має точність 1 с; половина часу запиту — оцінка затримки.
                        double now = Time.realtimeSinceStartupAsDouble;
                        _anchorServerUnix = (serverUtc - DateTime.UnixEpoch).TotalSeconds + (now - sentAt) / 2.0;
                        _anchorRealtime = now;
                        _hasNetworkAnchor = true;

                        double drift = DeviceUnixSeconds - _anchorServerUnix;
                        if (Math.Abs(drift) > 120)
                            Debug.Log($"[GameClock] Годинник пристрою відрізняється від мережевого на {drift / 60:0} хв — використовую мережевий.");

                        onSynced?.Invoke();
                        yield break;
                    }
                }
                Debug.Log("[GameClock] Мережевий час недоступний — годинник пристрою з захистом від перемотування назад.");
            }
            finally
            {
                _syncing = false;
            }
        }
    }
}
