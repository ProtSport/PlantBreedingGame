using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Safe area (виріз камери, «брова», жестова смуга): тримає інтерфейс у
    /// Screen.safeArea, а фон лишає на весь екран.
    ///
    /// Екрани будуються Editor-білдерами прямо в Canvas (без спільного контейнера),
    /// тому компонент зсуває кожен прямий дочірній елемент за його прив'язкою:
    ///  • прив'язаний до верху (шапка) — нижче на верхній відступ;
    ///  • прив'язаний до низу (нижнє меню, кнопка «Посадити») — вище на нижній;
    ///  • розтягнутий по висоті (списки, прокрутка) — стискається з обох боків.
    /// Фон (Background) і те, що створюється вже під час гри (модальні вікна,
    /// затемнення), не чіпаємо — вони мають накривати весь екран.
    ///
    /// Вішається автоматично на кожен Canvas верхнього рівня в кожній сцені
    /// (Install), тож у білдерах і сценах нічого додавати не треба.
    /// Перевірка в редакторі: Window → General → Device Simulator (телефон з вирізом).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>
        /// Перевірка екранів (Editor/ScreenAudit): підміняє розмір екрана й safe area,
        /// щоб у batchmode «зняти» телефони з вирізом без Device Simulator. null — справжні.
        /// </summary>
        public static Vector2Int? SimulatedScreen;
        public static Rect? SimulatedSafeArea;

        /// <summary>Частка висоти екрана під вирізом камери зверху (для елементів, створених у рантаймі).</summary>
        public static float TopInsetFraction => ScreenSize.y > 0 ? Mathf.Max(0f, ScreenSize.y - SafeArea.yMax) / ScreenSize.y : 0f;

        /// <summary>Частка висоти екрана під жестовою смугою знизу (для елементів, створених у рантаймі).</summary>
        public static float BottomInsetFraction => ScreenSize.y > 0 ? Mathf.Max(0f, SafeArea.yMin) / ScreenSize.y : 0f;

        private static Vector2Int ScreenSize => SimulatedScreen ?? new Vector2Int(Screen.width, Screen.height);
        private static Rect SafeArea => SimulatedSafeArea ?? Screen.safeArea;

        private static readonly HashSet<string> FullScreenNames = new HashSet<string> { "Background", "Bg", "Dim" };

        private struct Original
        {
            public RectTransform rt;
            public Vector2 offsetMin, offsetMax;
        }

        private readonly List<Original> _items = new List<Original>();
        private RectTransform _canvasRect;
        private Rect _appliedSafeArea;
        private Vector2Int _appliedScreen;
        private float _appliedHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            AttachToScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded += (scene, _) => AttachToScene(scene);
        }

        private static void AttachToScene(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                var canvas = go.GetComponent<Canvas>();
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    && go.GetComponent<SafeAreaFitter>() == null)
                    go.AddComponent<SafeAreaFitter>();
            }
        }

        private void Awake()
        {
            _canvasRect = (RectTransform)transform;
            // Лише те, що є в сцені зараз (зібране білдером); модальні вікна,
            // створені пізніше, лишаються на весь екран.
            Collect(transform, depth: 0);
        }

        /// <summary>
        /// Повноекранна панель без відступів (DetailPanel Дендрарію, RenamePanel
        /// Профілю) сама лишається на весь екран — її фон/затемнення накриває виріз, —
        /// а зсуваються її власні дочірні елементи.
        /// </summary>
        private void Collect(Transform parent, int depth)
        {
            foreach (Transform child in parent)
            {
                if (FullScreenNames.Contains(child.name) || !(child is RectTransform rt)) continue;
                if (depth == 0 && IsFullScreenPanel(rt))
                {
                    Collect(rt, depth + 1);
                    continue;
                }
                _items.Add(new Original { rt = rt, offsetMin = rt.offsetMin, offsetMax = rt.offsetMax });
            }
        }

        private static bool IsFullScreenPanel(RectTransform rt) =>
            rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one
            && rt.offsetMin == Vector2.zero && rt.offsetMax == Vector2.zero;

        private void Start() => Apply();

        private void Update()
        {
            if (SafeArea != _appliedSafeArea || ScreenSize != _appliedScreen
                || !Mathf.Approximately(_canvasRect.rect.height, _appliedHeight))
                Apply();
        }

        private void Apply()
        {
            var safe = SafeArea;
            var screen = ScreenSize;
            _appliedSafeArea = safe;
            _appliedScreen = screen;
            _appliedHeight = _canvasRect.rect.height;
            if (screen.y <= 0 || _appliedHeight <= 0) return;

            // Пікселі екрана → одиниці Canvas (CanvasScaler масштабує весь Canvas).
            float toCanvas = _appliedHeight / screen.y;
            float top = Mathf.Max(0f, screen.y - safe.yMax) * toCanvas;
            float bottom = Mathf.Max(0f, safe.yMin) * toCanvas;

            foreach (var item in _items)
            {
                if (item.rt == null) continue;
                var rt = item.rt;
                var min = item.offsetMin;
                var max = item.offsetMax;
                bool anchoredTop = rt.anchorMax.y >= 0.999f;
                bool anchoredBottom = rt.anchorMin.y <= 0.001f;

                if (anchoredTop && anchoredBottom)
                {
                    // Розтягнутий на всю висоту — між вирізом і жестовою смугою.
                    min.y += bottom;
                    max.y -= top;
                }
                else if (anchoredTop)
                {
                    min.y -= top;
                    max.y -= top;
                }
                else if (anchoredBottom)
                {
                    min.y += bottom;
                    max.y += bottom;
                }
                rt.offsetMin = min;
                rt.offsetMax = max;
            }
        }
    }
}
