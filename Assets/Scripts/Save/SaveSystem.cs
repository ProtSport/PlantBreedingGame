using System.IO;
using UnityEngine;
using PlantBreeding.Core;

namespace PlantBreeding.Save
{
    /// <summary>
    /// MVP-збереження: локальний JSON-файл через JsonUtility.
    /// Достатньо для першого релізу; пізніше тут само підключиться
    /// синхронізація з Firebase/Supabase — інтерфейс лишається той самий
    /// (Save/Load приймають PlayerData), тому виклики в GameManager не зміняться.
    /// </summary>
    public static class SaveSystem
    {
        private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        /// <summary>Не писати на диск (перевірка екранів Editor/ScreenAudit грається з копією прогресу).</summary>
        public static bool Suspended;

        /// <summary>Прогрес записано на диск — хмара (Cloud/CloudService) планує вивантаження.</summary>
        public static event System.Action Saved;

        /// <param name="touch">false — службовий запис (хмара): не міняти savedAt і не планувати вивантаження.</param>
        public static void Save(PlayerData data, bool touch = true)
        {
            if (Suspended) return;
            try
            {
                GameClock.WriteTo(data);
                if (touch) data.savedAtUnixSeconds = GameClock.NowUnixSeconds;
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(FilePath, json);
                if (touch) Saved?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] Не вдалося зберегти прогрес: {e.Message}");
            }
        }

        public static void Load(PlayerData target)
        {
            try
            {
                if (!File.Exists(FilePath)) return; // перший запуск — лишаємо значення за замовчуванням
                string json = File.ReadAllText(FilePath);
                JsonUtility.FromJsonOverwrite(json, target);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] Не вдалося завантажити прогрес: {e.Message}");
            }
        }
    }
}
