using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PlantBreeding.EditorTools
{
    /// <summary>
    /// Пакетні збірки (білдери сцен, AndroidBuild) лишають у редакторі тимчасову
    /// сцену «Untitled». Тому:
    ///  • ▶ Play завжди стартує з MainGarden (як на телефоні — гра починається з саду),
    ///    навіть якщо відкрита сцена Посадки/Лабораторії тощо;
    ///  • при відкритті редактора з порожньою сценою одразу відкривається MainGarden.
    /// </summary>
    [InitializeOnLoad]
    public static class OpenGardenOnStart
    {
        const string GardenScene = "Assets/Scenes/MainGarden.unity";

        static OpenGardenOnStart()
        {
            if (Application.isBatchMode) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GardenScene);
            EditorApplication.delayCall += OpenIfUntitled;
        }

        static void OpenIfUntitled()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var active = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GardenScene) == null) return;
            EditorSceneManager.OpenScene(GardenScene);
        }
    }
}
