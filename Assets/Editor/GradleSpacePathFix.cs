using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace PlantBreeding.EditorTools
{
    /// <summary>
    /// Проєкт лежить у «Local Sites» — з пробілом. External Dependency Manager пише в
    /// settings.gradle локальний репозиторій Firebase як URI «file:///…/Local Sites/…»,
    /// і Gradle падає: «Cannot convert URI … to a file». Після генерації Gradle-проєкту
    /// (і щоразу, бо EDM перезаписує свій блок у settingsTemplate.gradle) кодуємо пробіл як %20.
    /// </summary>
    public class GradleSpacePathFix : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var settings = Path.Combine(Path.GetDirectoryName(unityLibraryPath.TrimEnd('/', '\\')), "settings.gradle");
            if (!File.Exists(settings)) return;
            var text = File.ReadAllText(settings);
            const string marker = ".replace(\"\\\\\", \"/\")";
            if (!text.Contains("def unityProjectPath") || text.Contains(marker + ".replace(\" \", \"%20\")")) return;
            text = text.Replace(marker, marker + ".replace(\" \", \"%20\")");
            File.WriteAllText(settings, text);
            Debug.Log("[GradleSpacePathFix] settings.gradle: пробіли в шляху до репозиторію Firebase закодовано");
        }
    }
}
