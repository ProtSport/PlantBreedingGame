using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlantBreeding.Core;
using PlantBreeding.Save;

namespace PlantBreeding.Cloud
{
    public enum CloudState
    {
        Off,        // редактор / batchmode / Firebase недоступний
        Connecting,
        Guest,      // анонімний акаунт — прогрес у хмарі, але лише для цього встановлення
        Linked,     // прив'язано до Google — прогрес переживе перевстановлення й новий телефон
        Error
    }

    /// <summary>
    /// Firebase (RELEASE-PLAN п. 6): анонімний вхід при запуску, прив'язка до Google / Apple,
    /// синхронізація PlayerData з Firestore, Crashlytics і Analytics.
    ///
    /// Документ players/{uid}: json (PlayerData), savedAt (ігровий час GameClock),
    /// level/xp/harvests — для вибору при конфлікті, updatedAt — час сервера.
    /// Правила доступу — firestore.rules у корені проєкту (кожен бачить лише свій документ).
    ///
    /// Конфлікт (прогрес змінено і тут, і на іншому пристрої):
    ///  • той самий акаунт — перемагає новіший запис (savedAt);
    ///  • новий пристрій / вхід в інший акаунт — перемагає більший прогрес
    ///    (рівень, XP, урожаї), щоб свіже встановлення не затерло сад у хмарі.
    /// Якщо перемогла хмара — прогрес записується на диск і гра перезапускає сцену саду.
    ///
    /// Живе на об'єкті GameManager. У редакторі вимкнено (EnableInEditor), щоб
    /// тестові запуски не створювали акаунти й не перезаписували хмару.
    /// </summary>
    public class CloudService : MonoBehaviour
    {
        /// <summary>Увімкнути Firebase у Play-режимі редактора (меню PlantBreeding → Хмара в редакторі).</summary>
        public static bool EnableInEditor
        {
#if UNITY_EDITOR
            get => UnityEditor.EditorPrefs.GetBool("PB_CloudInEditor", false);
            set => UnityEditor.EditorPrefs.SetBool("PB_CloudInEditor", value);
#else
            get => false;
            set { }
#endif
        }

        private const string Collection = "players";
        private const float UploadIntervalSeconds = 45f;

        public static CloudService Instance { get; private set; }
        public static CloudState State { get; private set; } = CloudState.Off;
        public static event Action Changed;

        /// <summary>Чим прив'язано акаунт: "Google" / "Apple" (порожньо — гість).</summary>
        public static string LinkedProvider { get; private set; } = "";

        /// <summary>Firebase готовий (для Analytics/Crashlytics з інших скриптів).</summary>
        public static bool Ready { get; private set; }

        private FirebaseAuth _auth;
        private FirebaseFirestore _db;
        private bool _dirty;
        private bool _uploading;
        private float _nextUploadAt;
        private bool _synced; // перша синхронізація після входу завершена
        private bool _syncing;
        private float _nextSyncAt;
        private const float RetrySyncSeconds = 60f;

        private static PlayerData Data => GameManager.Instance != null ? GameManager.Instance.playerData : null;

        public static string StatusLine
        {
            get
            {
                var data = Data;
                switch (State)
                {
                    case CloudState.Linked:
                        // Apple може сховати пошту за адресою-пересилкою — її показувати нема сенсу.
                        string email = data?.cloudEmail;
                        if (string.IsNullOrEmpty(email) || email.Contains("privaterelay.appleid.com"))
                            return LinkedProvider == "Apple" ? "Прогрес збережено в Apple ID" : "Прогрес збережено в Google-акаунті";
                        return $"{LinkedProvider}: {email}";
                    case CloudState.Guest: return "Гість · прогрес лише на цьому телефоні";
                    case CloudState.Connecting: return "Підключення до хмари…";
                    case CloudState.Error: return "Немає зв'язку з хмарою — прогрес збережено на телефоні";
                    default: return "Хмара вимкнена";
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void Start()
        {
            if (Application.isBatchMode || (Application.isEditor && !EnableInEditor)) return;
            SetState(CloudState.Connecting);
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result != DependencyStatus.Available)
                {
                    Debug.LogWarning($"[Cloud] Firebase недоступний: {task.Result}");
                    SetState(CloudState.Error);
                    return;
                }
                InitFirebase();
            });
        }

        private void OnEnable() => SaveSystem.Saved += MarkDirty;

        private void OnDisable()
        {
            SaveSystem.Saved -= MarkDirty;
            if (Instance == this) Instance = null;
        }

        private void InitFirebase()
        {
            Ready = true;
            Firebase.Crashlytics.Crashlytics.ReportUncaughtExceptionsAsFatal = true;
            Telemetry.Init();

            _auth = FirebaseAuth.DefaultInstance;
            _db = FirebaseFirestore.DefaultInstance;

            if (_auth.CurrentUser != null) OnSignedIn();
            else
                _auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(t =>
                {
                    if (t.IsFaulted || t.IsCanceled || _auth.CurrentUser == null)
                    {
                        Debug.LogWarning($"[Cloud] Анонімний вхід не вдався: {t.Exception?.GetBaseException().Message}");
                        SetState(CloudState.Error);
                        return;
                    }
                    OnSignedIn();
                });
        }

        private void OnSignedIn()
        {
            var user = _auth.CurrentUser;
            Firebase.Crashlytics.Crashlytics.SetUserId(user.UserId);
            Telemetry.SetUserId(user.UserId);
            var data = Data;
            if (data != null && !user.IsAnonymous && !string.IsNullOrEmpty(user.Email)) data.cloudEmail = user.Email;
            LinkedProvider = "";
            foreach (var info in user.ProviderData)
            {
                if (info.ProviderId == "apple.com") LinkedProvider = "Apple";
                else if (info.ProviderId == "google.com" && LinkedProvider == "") LinkedProvider = "Google";
            }
            if (!user.IsAnonymous && LinkedProvider == "") LinkedProvider = "Google";
            SetState(user.IsAnonymous ? CloudState.Guest : CloudState.Linked);
            Sync();
        }

        // ════════════════════════════════════════════════════════════════
        //  СИНХРОНІЗАЦІЯ
        // ════════════════════════════════════════════════════════════════
        private DocumentReference Doc => _db.Collection(Collection).Document(_auth.CurrentUser.UserId);

        private void Sync()
        {
            var data = Data;
            if (data == null || _auth?.CurrentUser == null) return;
            string uid = _auth.CurrentUser.UserId;
            _syncing = true;

            Doc.GetSnapshotAsync(Source.Server).ContinueWithOnMainThread(t =>
            {
                _syncing = false;
                if (t.IsFaulted || t.IsCanceled)
                {
                    // Офлайн — граємо локально, але НЕ вивантажуємо наосліп (у хмарі може
                    // бути новіший прогрес з іншого пристрою): повторимо читання пізніше.
                    Debug.LogWarning($"[Cloud] Не вдалося прочитати хмару: {t.Exception?.GetBaseException().Message}");
                    _nextSyncAt = Time.unscaledTime + RetrySyncSeconds;
                    return;
                }

                var snap = t.Result;
                if (!snap.Exists)
                {
                    _synced = true;
                    Upload(force: true);
                    return;
                }

                var remote = snap.ToDictionary();
                double rSaved = ToDouble(remote, "savedAt");
                bool sameAccount = data.cloudUid == uid;
                bool remoteChanged = !sameAccount || Math.Abs(rSaved - data.cloudSyncedSavedAt) > 0.5;
                bool localChanged = !sameAccount || data.savedAtUnixSeconds > data.cloudSyncedSavedAt + 0.5;

                bool takeRemote;
                if (!remoteChanged) takeRemote = false;
                else if (!localChanged) takeRemote = true;
                else if (sameAccount) takeRemote = rSaved > data.savedAtUnixSeconds;
                else takeRemote = CompareProgress(remote, data) >= 0;

                _synced = true;
                if (takeRemote && remote.TryGetValue("json", out var jsonObj) && jsonObj is string json)
                {
                    ApplyRemote(json, uid, rSaved);
                }
                else if (localChanged || remoteChanged)
                {
                    Upload(force: true);
                }
            });
        }

        /// <summary>&gt;0 — у хмарі більший прогрес, &lt;0 — на телефоні.</summary>
        private static int CompareProgress(Dictionary<string, object> remote, PlayerData local)
        {
            int c = ToLong(remote, "level").CompareTo(local.level);
            if (c != 0) return c;
            c = ToLong(remote, "xp").CompareTo(local.xp);
            if (c != 0) return c;
            return ToLong(remote, "harvests").CompareTo(local.totalHarvests);
        }

        /// <summary>Хмарна копія перемогла: записати на диск і перезапустити сад з нею.</summary>
        private void ApplyRemote(string json, string uid, double remoteSavedAt)
        {
            var data = Data;
            string email = data.cloudEmail;
            JsonUtility.FromJsonOverwrite(json, data);
            data.cloudUid = uid;
            data.cloudSyncedSavedAt = remoteSavedAt;
            if (!string.IsNullOrEmpty(email)) data.cloudEmail = email;
            data.savedAtUnixSeconds = remoteSavedAt;
            SaveSystem.Save(data, touch: false);

            Debug.Log("[Cloud] Завантажено прогрес з хмари — перезапуск саду");
            GameEvents.RaiseToast("Прогрес завантажено з хмари");
            // Новий GameManager прочитає збереження з диска; сцени-вкладки закриються.
            Destroy(GameManager.Instance.gameObject);
            SceneManager.LoadScene("MainGarden", LoadSceneMode.Single);
        }

        private void MarkDirty() => _dirty = true;

        private void Update()
        {
            if (!_synced && !_syncing && _auth?.CurrentUser != null && _nextSyncAt > 0 && Time.unscaledTime >= _nextSyncAt)
            {
                _nextSyncAt = 0;
                Sync();
                return;
            }
            if (!_synced || !_dirty || _uploading || Time.unscaledTime < _nextUploadAt) return;
            Upload(force: false);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && _synced && _dirty) Upload(force: true);
        }

        private void OnApplicationQuit()
        {
            if (_synced && _dirty) Upload(force: true);
        }

        private void Upload(bool force)
        {
            var data = Data;
            if (data == null || _auth?.CurrentUser == null || (_uploading && !force)) return;
            _dirty = false;
            _uploading = true;
            _nextUploadAt = Time.unscaledTime + UploadIntervalSeconds;

            string uid = _auth.CurrentUser.UserId;
            data.cloudUid = uid;
            double savedAt = data.savedAtUnixSeconds;
            var doc = new Dictionary<string, object>
            {
                { "json", JsonUtility.ToJson(data) },
                { "savedAt", savedAt },
                { "level", data.level },
                { "xp", data.xp },
                { "harvests", data.totalHarvests },
                { "appVersion", Application.version },
                { "updatedAt", FieldValue.ServerTimestamp },
            };
            Doc.SetAsync(doc).ContinueWithOnMainThread(t =>
            {
                _uploading = false;
                if (t.IsFaulted || t.IsCanceled)
                {
                    Debug.LogWarning($"[Cloud] Не вдалося зберегти в хмару: {t.Exception?.GetBaseException().Message}");
                    _dirty = true;
                    return;
                }
                data.cloudSyncedSavedAt = savedAt;
                SaveSystem.Save(data, touch: false);
            });
        }

        // ════════════════════════════════════════════════════════════════
        //  GOOGLE / APPLE
        // ════════════════════════════════════════════════════════════════
        /// <summary>
        /// Прив'язати прогрес до Google (Android і iOS). Якщо цей акаунт уже має прогрес
        /// (перевстановлення / новий телефон) — входимо в нього, і Sync вибирає більший прогрес.
        /// done(ok, повідомлення для тосту).
        /// </summary>
        public static void SignInWithGoogle(Action<bool, string> done)
        {
            var self = Instance;
            if (self == null || self._auth == null)
            {
                done(false, "Хмара ще не підключена — спробуй за хвилину");
                return;
            }
            GoogleSignInBridge.SignIn((ok, tokenOrError, accessToken, cancelled) =>
            {
                if (!ok)
                {
                    if (!cancelled) Debug.LogWarning($"[Cloud] Google: {tokenOrError}");
                    done(false, cancelled ? "" : "Не вдалося увійти через Google");
                    return;
                }
                self.UseCredential(GoogleAuthProvider.GetCredential(tokenOrError, accessToken), "Google", done);
            });
        }

        /// <summary>«Sign in with Apple» (iOS) — те саме, що SignInWithGoogle.</summary>
        public static void SignInWithApple(Action<bool, string> done)
        {
            var self = Instance;
            if (self == null || self._auth == null)
            {
                done(false, "Хмара ще не підключена — спробуй за хвилину");
                return;
            }
            AppleSignInBridge.SignIn((ok, tokenOrError, rawNonce, cancelled) =>
            {
                if (!ok)
                {
                    if (!cancelled) Debug.LogWarning($"[Cloud] Apple: {tokenOrError}");
                    done(false, cancelled ? "" : "Не вдалося увійти через Apple");
                    return;
                }
                self.UseCredential(OAuthProvider.GetCredential("apple.com", tokenOrError, rawNonce, null), "Apple", done);
            });
        }

        private void UseCredential(Credential credential, string provider, Action<bool, string> done)
        {
            var user = _auth.CurrentUser;
            if (user != null && user.IsAnonymous)
            {
                user.LinkWithCredentialAsync(credential).ContinueWithOnMainThread(t =>
                {
                    if (!t.IsFaulted && !t.IsCanceled)
                    {
                        OnSignedIn();
                        done(true, $"Сад прив'язано до {provider} — прогрес не загубиться");
                        return;
                    }
                    if (IsAuthError(t, AuthError.CredentialAlreadyInUse) || IsAuthError(t, AuthError.EmailAlreadyInUse))
                    {
                        // Цей акаунт уже має свій сад — входимо в нього. Apple-токен одноразовий
                        // (nonce), тож беремо оновлений credential, який повернув Firebase.
                        var updated = FindLinkException(t)?.UserInfo?.UpdatedCredential;
                        SignInExisting(updated != null && updated.IsValid() ? updated : credential, done);
                        return;
                    }
                    Debug.LogWarning($"[Cloud] Прив'язка не вдалась: {t.Exception?.GetBaseException().Message}");
                    done(false, "Не вдалося прив'язати акаунт");
                });
            }
            else SignInExisting(credential, done);
        }

        private void SignInExisting(Credential credential, Action<bool, string> done)
        {
            _synced = false;
            _auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted || t.IsCanceled || _auth.CurrentUser == null)
                {
                    Debug.LogWarning($"[Cloud] Вхід не вдався: {t.Exception?.GetBaseException().Message}");
                    _synced = _auth.CurrentUser != null; // лишились у попередньому акаунті
                    done(false, "Не вдалося увійти");
                    return;
                }
                OnSignedIn(); // Sync: якщо в хмарі більший прогрес — завантажить його
                done(true, "Вхід виконано");
            });
        }

        private static FirebaseAccountLinkException FindLinkException(Task task)
        {
            if (task.Exception == null) return null;
            foreach (var e in task.Exception.Flatten().InnerExceptions)
                if (e is FirebaseAccountLinkException link) return link;
            return null;
        }

        private static bool IsAuthError(Task task, AuthError error)
        {
            if (task.Exception == null) return false;
            foreach (var e in task.Exception.Flatten().InnerExceptions)
                if (e is FirebaseException fe && fe.ErrorCode == (int)error) return true;
            return false;
        }

        // ════════════════════════════════════════════════════════════════
        private static void SetState(CloudState state)
        {
            State = state;
            Changed?.Invoke();
        }

        private static double ToDouble(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) && v != null ? Convert.ToDouble(v) : 0;

        private static long ToLong(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) && v != null ? Convert.ToInt64(v) : 0;
    }
}
