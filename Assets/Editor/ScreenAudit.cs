using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PlantBreeding.UI;

namespace PlantBreeding.EditorTools
{
    /// <summary>
    /// Знімки всіх екранів на телефонах 16:9 … 21:9 і iPad (RELEASE-PLAN п. 4) —
    /// замість ручного перегляду в Device Simulator. Запускає Play з MainGarden;
    /// далі працює ScreenAuditRunner. Результат — Builds/ScreenAudit/&lt;профіль&gt;/*.png
    /// (червоні лінії — межі safe area).
    ///
    /// Меню: PlantBreeding → Знімки екранів (16:9…21:9)
    /// Batchmode: Unity.exe -batchmode -projectPath . -executeMethod PlantBreeding.EditorTools.ScreenAudit.Run
    /// (без -quit — редактор сам закриється після знімків).
    /// </summary>
    [InitializeOnLoad]
    public static class ScreenAudit
    {
        const string StartedKey = "PB_ScreenAudit_Started";
        const double TimeoutSeconds = 600;

        static ScreenAudit()
        {
            // Запобіжник: якщо щось зависло в batchmode — не висіти вічно.
            if (!Application.isBatchMode || !SessionState.GetBool(ScreenAuditRunner.SessionKey, false)) return;
            EditorApplication.update += () =>
            {
                double started = double.Parse(SessionState.GetString(StartedKey, "0"));
                if (started > 0 && EditorApplication.timeSinceStartup - started > TimeoutSeconds)
                {
                    Debug.LogError("[ScreenAudit] Тайм-аут");
                    EditorApplication.Exit(2);
                }
            };
        }

        [MenuItem("PlantBreeding/Знімки екранів (16:9…21:9)")]
        public static void Run()
        {
            SessionState.SetBool(ScreenAuditRunner.SessionKey, true);
            SessionState.SetString(StartedKey, EditorApplication.timeSinceStartup.ToString());
            EditorSceneManager.OpenScene("Assets/Scenes/MainGarden.unity");
            EditorApplication.EnterPlaymode();
        }
    }
}
