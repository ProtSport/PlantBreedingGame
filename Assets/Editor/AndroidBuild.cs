using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PlantBreeding.EditorTools
{
    /// <summary>
    /// Збірки для Android.
    ///
    /// Tools → Build Android APK — швидкий APK для встановлення на свій телефон
    /// (налагоджувальний підпис; покупки в ньому НЕ працюють).
    ///
    /// Tools → Build Android App Bundle (Google Play) — підписаний .aab для
    /// завантаження в Google Play Console (внутрішнє тестування / реліз).
    /// Підписується ключем завантаження з Keystore/ (поза git, див. .gitignore):
    /// паролі — у Keystore/keystore.properties. versionCode щоразу +1 — Google Play
    /// не приймає дві збірки з однаковим номером.
    ///
    /// З командного рядка:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod PlantBreeding.EditorTools.AndroidBuild.Build
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod PlantBreeding.EditorTools.AndroidBuild.BuildAppBundle
    /// </summary>
    public static class AndroidBuild
    {
        const string CompanyName = "ProtSport";
        const string PackageName = "com.protsport.plantbreeding";
        const string ApkPath = "Builds/Android/PlantBreedingGame.apk";
        const string AabPath = "Builds/Android/PlantBreedingGame.aab";
        const string KeystorePropsPath = "Keystore/keystore.properties";

        [MenuItem("Tools/Build Android APK")]
        public static void Build()
        {
            ApplyPlayerSettings();
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;
            RunBuild(ApkPath);
        }

        [MenuItem("Tools/Build Android App Bundle (Google Play)")]
        public static void BuildAppBundle()
        {
            ApplyPlayerSettings();
            if (!ApplyUploadKeystore()) return;

            PlayerSettings.Android.bundleVersionCode++;
            AssetDatabase.SaveAssets();
            EditorUserBuildSettings.buildAppBundle = true;
            Debug.Log($"[AndroidBuild] versionCode {PlayerSettings.Android.bundleVersionCode}, version {PlayerSettings.bundleVersion}");
            RunBuild(AabPath);
            EditorUserBuildSettings.buildAppBundle = false;
        }

        static void RunBuild(string outputPath)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AndroidBuild] OK: {outputPath} ({new FileInfo(outputPath).Length / (1024 * 1024)} MB)");
            }
            else
            {
                Debug.LogError($"[AndroidBuild] FAILED: {summary.result}, errors: {summary.totalErrors}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Підпис ключем завантаження. Паролі не зберігаються в ProjectSettings
        /// (Unity їх не серіалізує) — щоразу читаються з Keystore/keystore.properties.
        /// </summary>
        static bool ApplyUploadKeystore()
        {
            if (!File.Exists(KeystorePropsPath))
            {
                Fail($"Немає {KeystorePropsPath} — ключ підпису не знайдено. Відновлюй папку Keystore/ з резервної копії.");
                return false;
            }

            var props = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(KeystorePropsPath))
            {
                if (line.StartsWith("#") || !line.Contains("=")) continue;
                int i = line.IndexOf('=');
                props[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
            }

            if (!props.TryGetValue("keystore", out var keystore) || !File.Exists(keystore))
            {
                Fail($"Файл ключа з {KeystorePropsPath} не знайдено.");
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = props.GetValueOrDefault("storePassword");
            PlayerSettings.Android.keyaliasName = props.GetValueOrDefault("keyAlias");
            PlayerSettings.Android.keyaliasPass = props.GetValueOrDefault("keyPassword");
            return true;
        }

        static void Fail(string message)
        {
            Debug.LogError("[AndroidBuild] " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else EditorUtility.DisplayDialog("Збірка для Google Play", message, "OK");
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, PackageName);
            // ARM64 — вимога Google Play і більшості сучасних телефонів; потребує IL2CPP.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();
        }
    }
}
