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

        public static void Save(PlayerData data)
        {
            try
            {
                GameClock.WriteTo(data);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(FilePath, json);
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
