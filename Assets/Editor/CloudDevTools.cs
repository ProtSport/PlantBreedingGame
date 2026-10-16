using UnityEditor;
using PlantBreeding.Cloud;

namespace PlantBreeding.EditorTools
{
    /// <summary>
    /// Firebase у Play-режимі редактора вимкнено за замовчуванням (CloudService.EnableInEditor):
    /// кожен тестовий запуск інакше створював би анонімний акаунт і писав у справжню хмару.
    /// Цей перемикач вмикає його для перевірки синхронізації.
    /// </summary>
    public static class CloudDevTools
    {
        const string MenuPath = "PlantBreeding/Хмара (Firebase) у редакторі";

        [MenuItem(MenuPath)]
        static void Toggle() => CloudService.EnableInEditor = !CloudService.EnableInEditor;

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, CloudService.EnableInEditor);
            return !EditorApplication.isPlaying;
        }
    }
}
