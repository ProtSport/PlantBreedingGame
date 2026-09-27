using System;
using UnityEngine;

namespace PlantBreeding.Theme
{
    public enum ThemeMode
    {
        Dark = 0,
        Light = 1,
        System = 2
    }

    /// <summary>
    /// Поточна тема: вибір гравця (PlayerData.themeMode) + «як у системі».
    /// Системна тема: Android — Configuration.uiMode; iOS і редактор — поки темна
    /// (iOS потребує нативного плагіна).
    /// </summary>
    public static class ThemeService
    {
        public static ThemeMode Mode { get; private set; } = ThemeMode.Dark;
        public static bool IsLight { get; private set; }
        public static event Action Changed;

        public static void SetMode(ThemeMode mode)
        {
            Mode = mode;
            Refresh();
        }

        /// <summary>Перерахувати (напр. після повернення з фону — система могла змінити тему).</summary>
        public static void Refresh()
        {
            bool light = Mode == ThemeMode.Light || (Mode == ThemeMode.System && SystemPrefersLight());
            if (light == IsLight) return;
            IsLight = light;
            Changed?.Invoke();
        }

        public static bool SystemPrefersLight()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var resources = activity.Call<AndroidJavaObject>("getResources"))
                using (var config = resources.Call<AndroidJavaObject>("getConfiguration"))
                {
                    const int UI_MODE_NIGHT_MASK = 0x30, UI_MODE_NIGHT_YES = 0x20;
                    int uiMode = config.Get<int>("uiMode");
                    return (uiMode & UI_MODE_NIGHT_MASK) != UI_MODE_NIGHT_YES;
                }
#else
                // iOS (UITraitCollection) потребує нативного плагіна; редактор/ПК — профіль
                // .NET Standard без доступу до реєстру Windows. Поки — темна (основна тема гри).
                return false;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Theme] Не вдалося прочитати системну тему: {e.Message}");
                return false;
            }
        }
    }
}
