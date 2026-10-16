using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace PlantBreeding.Cloud
{
    /// <summary>
    /// Вхід через Google — рідна частина повертає Google ID token, з якого CloudService
    /// робить Firebase-credential:
    ///  • Android — Plugins/Android/GoogleSignInBridge.java (Credential Manager);
    ///  • iOS — Plugins/iOS/CloudSignInBridge.mm (Google Sign-In SDK, pod з CloudDependencies.xml;
    ///    URL-схему додає Editor/IosSignInPostprocess.cs). На iOS поруч обов'язково
    ///    є «Sign in with Apple» (AppleSignInBridge) — правило App Review 4.8.
    /// </summary>
    public class GoogleSignInBridge : MonoBehaviour
    {
        /// <summary>
        /// OAuth «Web client» з google-services.json (client_type 3) — Google видає
        /// ID token саме для нього, і Firebase Auth його перевіряє. Не секрет.
        /// </summary>
        public const string WebClientId = "9543369297-r7lsm9pprgjcdvtgl8bdrh7n749bp8f0.apps.googleusercontent.com";

        /// <summary>OAuth-клієнт iOS-застосунку (google-services.json, client_type 2 / CLIENT_ID у GoogleService-Info.plist).</summary>
        public const string IosClientId = "9543369297-a1nk95fpommocl2e3gdoavjul39elb69.apps.googleusercontent.com";
        /// <summary>URL-схема повернення з Google на iOS — IosClientId задом наперед.</summary>
        public const string IosReversedClientId = "com.googleusercontent.apps.9543369297-a1nk95fpommocl2e3gdoavjul39elb69";

        private const string ReceiverName = "GoogleSignInReceiver";

        public static bool IsSupported =>
            Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void PBGoogleSignIn(string clientId, string receiver);
#endif

        private static GoogleSignInBridge _receiver;
        private static Action<bool, string, string, bool> _pending;

        /// <summary>done(ok, idToken або опис помилки, accessToken (iOS; на Android null), скасовано гравцем).</summary>
        public static void SignIn(Action<bool, string, string, bool> done)
        {
            if (!IsSupported)
            {
                done(false, "Вхід через Google працює лише на телефоні", null, false);
                return;
            }
            EnsureReceiver();
            _pending = done;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass("com.protsport.plantbreeding.GoogleSignInBridge");
                bridge.CallStatic("signIn", activity, WebClientId, ReceiverName);
            }
            catch (Exception e)
            {
                Finish(false, e.Message, null, false);
            }
#elif UNITY_IOS && !UNITY_EDITOR
            PBGoogleSignIn(IosClientId, ReceiverName);
#endif
        }

        private static void EnsureReceiver()
        {
            if (_receiver != null) return;
            var go = new GameObject(ReceiverName);
            DontDestroyOnLoad(go);
            _receiver = go.AddComponent<GoogleSignInBridge>();
        }

        /// <summary>
        /// Виклик з рідного коду через UnitySendMessage: "ok|idToken" (Android),
        /// "ok|idToken|accessToken" (iOS), "cancel|", "err|опис".
        /// </summary>
        public void OnGoogleSignInResult(string message)
        {
            var parts = message.Split('|');
            string kind = parts[0];
            if (kind == "ok")
            {
                string access = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : null;
                Finish(true, parts.Length > 1 ? parts[1] : "", access, false);
            }
            else Finish(false, parts.Length > 1 ? message.Substring(kind.Length + 1) : "", null, kind == "cancel");
        }

        private static void Finish(bool ok, string payload, string accessToken, bool cancelled)
        {
            var done = _pending;
            _pending = null;
            done?.Invoke(ok, payload, accessToken, cancelled);
        }
    }
}
