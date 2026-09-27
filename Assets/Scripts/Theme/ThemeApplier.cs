using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantBreeding.Theme
{
    /// <summary>
    /// Світла тема без переробки екранів: усі кольори в грі задані для темної
    /// теми (білдери + рантайм-код), а цей компонент у світлій темі на льоту
    /// підміняє колір кожного Graphic (Image/TMP) за таблицею «темний токен →
    /// світлий» (палітра — макет «Світла-Темна_тема_Мови»: фон #FBF3E4, текст
    /// #4A3B2E…) і загальними правилами для решти. Оригінал запам'ятовується,
    /// тож повернення в темну тему — точне. Якщо код сам змінює колір
    /// (вибір картки, стан кнопки), це помічається і перефарбовується знову.
    /// Фон-градієнт (спрайт bg-gradient) у світлій темі замінюється на кремовий.
    /// Живе на DontDestroyOnLoad-об'єкті GameManager.
    /// </summary>
    public class ThemeApplier : MonoBehaviour
    {
        private class Entry
        {
            public Color original;
            public Color applied;
            public Sprite originalSprite;
            public bool ignore;
        }

        private const float RescanSeconds = 0.25f;
        private static readonly Color LightBackground = Hex("F6EEDD");
        private static readonly Color LightSurface = Hex("FFF9EE");
        private static readonly Color Ink = Hex("2E2519");

        // Токени темної палітри (UIBuilderKit/UIColors) → світла палітра.
        private static readonly Dictionary<string, Color> Tokens = new Dictionary<string, Color>
        {
            // поверхні
            { "0E130C", LightBackground }, { "141B0F", LightSurface }, { "161E10", LightSurface },
            { "172013", LightSurface }, { "0D120A", LightSurface }, { "12180D", LightBackground },
            { "1C2414", Hex("EFE5D0") }, { "5C6653", Hex("B8AE98") },
            // тексти
            { "F2ECDF", Ink }, { "EBE4D6", Hex("3A3024") }, { "E8E0CC", Hex("3A3024") },
            { "DDE8C8", Hex("3F5A2E") }, { "C9CFC1", Hex("5A6152") }, { "A9B2A0", Hex("6B7560") },
            { "93A088", Hex("6B7560") }, { "8C9683", Hex("7A826F") }, { "9FAE92", Hex("6E7A62") },
            { "6E7A64", Hex("8A927E") }, { "C6D0E8", Hex("4A5A80") }, { "CDEBA0", Hex("4E7F2C") },
            // акценти (темніші — щоб читались на світлому)
            { "A7CE73", Hex("5E9A3A") }, { "B5E67A", Hex("6FA83F") }, { "C9A65A", Hex("A07D2E") },
            { "E4C77E", Hex("A9832F") }, { "8FD0E6", Hex("2F7FA0") }, { "E7A66B", Hex("B8692C") },
        };

        private readonly Dictionary<Graphic, Entry> _entries = new Dictionary<Graphic, Entry>();
        private float _rescanTimer;
        private Camera _camera;
        private Color _cameraOriginal;
        private bool _cameraTracked;

        private void OnEnable() => ThemeService.Changed += HandleThemeChanged;
        private void OnDisable() => ThemeService.Changed -= HandleThemeChanged;

        private void HandleThemeChanged()
        {
            if (ThemeService.IsLight)
            {
                _rescanTimer = 0f; // одразу перефарбувати все
                return;
            }
            // Назад у темну — повернути оригінали й забути.
            foreach (var kv in _entries)
            {
                if (kv.Key == null) continue;
                kv.Key.color = kv.Value.original;
                if (kv.Key is Image img && kv.Value.originalSprite != null) img.sprite = kv.Value.originalSprite;
            }
            _entries.Clear();
            if (_cameraTracked && _camera != null) _camera.backgroundColor = _cameraOriginal;
            _cameraTracked = false;
        }

        private void LateUpdate()
        {
            if (!ThemeService.IsLight) return;

            _rescanTimer -= Time.unscaledDeltaTime;
            if (_rescanTimer <= 0f)
            {
                _rescanTimer = RescanSeconds;
                foreach (var g in FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!_entries.ContainsKey(g)) Track(g);
                TrackCamera();
            }

            // Код змінив колір після нас → новий оригінал.
            List<Graphic> dead = null;
            foreach (var kv in _entries)
            {
                var g = kv.Key;
                if (g == null) { (dead ??= new List<Graphic>()).Add(g); continue; }
                if (!kv.Value.ignore && g.color != kv.Value.applied)
                {
                    kv.Value.original = g.color;
                    ApplyTo(g, kv.Value);
                }
            }
            if (dead != null) foreach (var d in dead) _entries.Remove(d);
        }

        private void Track(Graphic g)
        {
            var e = new Entry { original = g.color };
            if (g.GetComponentInParent<ThemeIgnore>(true) != null)
            {
                e.applied = g.color; // відстежуємо, але не чіпаємо
                e.ignore = true;
                _entries[g] = e;
                return;
            }
            if (g is Image img && img.sprite != null && img.sprite.name == "bg-gradient") e.originalSprite = img.sprite;
            _entries[g] = e;
            ApplyTo(g, e);
        }

        private void ApplyTo(Graphic g, Entry e)
        {
            Color c;
            if (e.originalSprite != null && g is Image img)
            {
                img.sprite = null; // градієнт темний — у світлій темі суцільний кремовий фон
                c = LightBackground;
            }
            else
            {
                c = Map(e.original, g is TMP_Text);
            }
            e.applied = c;
            if (g.color != c) g.color = c;
        }

        private void TrackCamera()
        {
            var cam = Camera.main;
            if (cam == null || (_cameraTracked && cam == _camera)) return;
            _camera = cam;
            _cameraOriginal = cam.backgroundColor;
            _cameraTracked = true;
            cam.backgroundColor = LightBackground;
        }

        /// <summary>Колір темної теми → світлої. Альфа зберігається.</summary>
        public static Color Map(Color c, bool isText)
        {
            string hex = ColorUtility.ToHtmlStringRGB(c);
            if (Tokens.TryGetValue(hex, out var mapped)) return WithAlpha(mapped, c.a);

            float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));

            if (isText)
                return lum > 0.5f ? WithAlpha(Ink, c.a) : c; // світлий текст → темне чорнило

            if (c.r == 1f && c.g == 1f && c.b == 1f && c.a >= 0.99f) return c; // арт/іконки як є
            if (min > 0.85f && c.a < 0.6f) return WithAlpha(Ink, Mathf.Min(1f, c.a * 1.2f)); // білі рамки/скло
            if (c.r < 0.02f && c.g < 0.02f && c.b < 0.02f) return WithAlpha(c, c.a * 0.5f); // затемнення модалок
            if (lum < 0.12f && c.a >= 0.5f) return WithAlpha(LightSurface, c.a); // темні панелі
            return c;
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
