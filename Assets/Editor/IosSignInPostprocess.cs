#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using PlantBreeding.Cloud;

namespace PlantBreeding.EditorTools
{
    /// <summary>
    /// Після експорту Xcode-проєкту (iOS-збірка — на Mac / у Codemagic) налаштовує вхід:
    ///  • Google — URL-схема REVERSED_CLIENT_ID в Info.plist (повернення з вікна входу) і GIDClientID;
    ///  • Apple — capability «Sign in with Apple» (entitlements) і фреймворк AuthenticationServices.
    /// Компілюється лише з модулем iOS Build Support (UNITY_IOS) — на Windows без нього файл порожній.
    /// </summary>
    public static class IosSignInPostprocess
    {
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            AddGoogleUrlScheme(path);
            AddSignInWithApple(path);
        }

        private static void AddGoogleUrlScheme(string path)
        {
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;

            root.SetString("GIDClientID", GoogleSignInBridge.IosClientId);

            PlistElementArray urlTypes = root.values.TryGetValue("CFBundleURLTypes", out var existing)
                ? existing.AsArray()
                : root.CreateArray("CFBundleURLTypes");
            foreach (var item in urlTypes.values)
            {
                var dict = item.AsDict();
                if (dict.values.TryGetValue("CFBundleURLSchemes", out var schemes))
                    foreach (var s in schemes.AsArray().values)
                        if (s.AsString() == GoogleSignInBridge.IosReversedClientId) { plist.WriteToFile(plistPath); return; }
            }
            var google = urlTypes.AddDict();
            google.SetString("CFBundleURLName", "google-sign-in");
            google.SetString("CFBundleTypeRole", "Editor");
            google.CreateArray("CFBundleURLSchemes").AddString(GoogleSignInBridge.IosReversedClientId);
            plist.WriteToFile(plistPath);
        }

        private static void AddSignInWithApple(string path)
        {
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            // Рідний код входу (Plugins/iOS/CloudSignInBridge.mm) компілюється в UnityFramework.
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "AuthenticationServices.framework", false);
            string mainTarget = project.GetUnityMainTargetGuid();
            project.WriteToFile(projectPath);

            var capabilities = new ProjectCapabilityManager(projectPath, "Unity-iPhone/plantbreeding.entitlements", null, mainTarget);
            capabilities.AddSignInWithApple();
            capabilities.WriteToFile();
        }
    }
}
#endif
