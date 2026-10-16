using System;
using System.Security.Cryptography;
using System.Text;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace PlantBreeding.Cloud
{
    /// <summary>
    /// «Sign in with Apple» на iOS (Plugins/iOS/CloudSignInBridge.mm, AuthenticationServices).
    /// Обов'язковий поруч із входом через Google (App Review 4.8). Повертає identity token
    /// і rawNonce — з них CloudService робить Firebase-credential (OAuthProvider "apple.com").
    /// Nonce: Apple отримує SHA-256 від випадкового рядка, Firebase — сам рядок, і перевіряє,
    /// що токен виданий саме на цей запит.
    /// Потрібні: capability «Sign in with Apple» (додає Editor/IosSignInPostprocess.cs)
    /// і провайдер Apple, увімкнений у Firebase → Authentication.
    /// </summary>
    public class AppleSignInBridge : MonoBehaviour
    {
        private const string ReceiverName = "AppleSignInReceiver";

        public static bool IsSupported => Application.platform == RuntimePlatform.IPhonePlayer;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void PBAppleSignIn(string hashedNonce, string receiver);
#endif

        private static AppleSignInBridge _receiver;
        private static Action<bool, string, string, bool> _pending;
        private static string _rawNonce;

        /// <summary>done(ok, identityToken або опис помилки, rawNonce, скасовано гравцем).</summary>
        public static void SignIn(Action<bool, string, string, bool> done)
        {
            if (!IsSupported)
            {
                done(false, "Вхід через Apple працює лише на iPhone", null, false);
                return;
            }
            if (_receiver == null)
            {
                var go = new GameObject(ReceiverName);
                DontDestroyOnLoad(go);
                _receiver = go.AddComponent<AppleSignInBridge>();
            }
            _pending = done;
            _rawNonce = RandomNonce(32);
#if UNITY_IOS && !UNITY_EDITOR
            PBAppleSignIn(Sha256Hex(_rawNonce), ReceiverName);
#endif
        }

        /// <summary>Виклик з рідного коду: "ok|identityToken", "cancel|", "err|опис".</summary>
        public void OnAppleSignInResult(string message)
        {
            int bar = message.IndexOf('|');
            string kind = bar >= 0 ? message.Substring(0, bar) : message;
            string payload = bar >= 0 ? message.Substring(bar + 1) : "";
            var done = _pending;
            _pending = null;
            done?.Invoke(kind == "ok", payload, _rawNonce, kind == "cancel");
        }

        private static string RandomNonce(int length)
        {
            const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVXYZabcdefghijklmnopqrstuvwxyz-._";
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var sb = new StringBuilder(length);
            foreach (var b in bytes) sb.Append(chars[b % chars.Length]);
            return sb.ToString();
        }

        private static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
