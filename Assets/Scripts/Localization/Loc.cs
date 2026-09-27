using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlantBreeding.Localization
{
    public enum Language
    {
        Uk,
        En,
        Pl
    }

    /// <summary>
    /// Переклад інтерфейсу UA → EN/PL. Вихідна мова гри — українська: усі тексти
    /// в коді й сценах лишаються українськими, а LocalizationApplier підміняє їх
    /// на льоту за таблицею LocTable (ключ — український текст). Тому нова мова
    /// = нова колонка в таблиці, без змін у коді екранів.
    ///
    /// Translate розуміє:
    ///  • точний збіг («Посадити»);
    ///  • шаблони з {0},{1}… («Рівень {0}», «{0}: +{1} монет») — аргументи
    ///    перекладаються рекурсивно (назва рослини всередині фрази);
    ///  • склеєні тексти: рядки через \n і частини через « · » перекладаються окремо;
    ///  • мале/велике перше слово («посадити фіалку» ↔ ключ «Фіалка»).
    /// </summary>
    public static class Loc
    {
        public static Language Current { get; private set; } = Language.Uk;
        public static event Action Changed;

        private static Dictionary<string, string[]> _exact;
        private static List<(Regex regex, string[] templates, int literalLength)> _patterns;
        private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();
        private static readonly HashSet<string> _missing = new HashSet<string>();

        public static string Code(Language lang) => lang switch
        {
            Language.En => "en",
            Language.Pl => "pl",
            _ => "uk"
        };

        public static Language FromCode(string code) => code switch
        {
            "en" => Language.En,
            "pl" => Language.Pl,
            _ => Language.Uk
        };

        public static void Set(Language lang)
        {
            if (Current == lang) return;
            Current = lang;
            _cache.Clear();
            Changed?.Invoke();
        }

        /// <summary>Тексти, для яких не знайшлось перекладу (для перевірки в редакторі).</summary>
        public static IReadOnlyCollection<string> Missing => _missing;

        public static string Translate(string uk)
        {
            if (Current == Language.Uk || string.IsNullOrEmpty(uk)) return uk;
            if (!HasCyrillic(uk)) return uk;
            EnsureBuilt();
            if (_cache.TryGetValue(uk, out var cached)) return cached;

            string result = TranslateCore(uk, 0) ?? uk;
            if (result == uk) _missing.Add(uk);
            _cache[uk] = result;
            return result;
        }

        private static string TranslateCore(string text, int depth)
        {
            if (depth > 4 || string.IsNullOrEmpty(text) || !HasCyrillic(text)) return text;
            int col = Current == Language.En ? 0 : 1;

            if (_exact.TryGetValue(text, out var row)) return row[col];

            // Перша літера в іншому регістрі (назви рослин у «посадити фіалку»).
            string flipped = FlipFirst(text);
            if (flipped != text && _exact.TryGetValue(flipped, out row))
                return char.IsLower(text[0]) ? LowerFirst(row[col]) : row[col];

            // 1) Шаблони, де аргументи не містять роздільників — інакше шаблон
            //    «+{0} насіння «{1}»» «проковтнув» би склеєне «+20 монет · +3 насіння «Кактус»».
            var strict = MatchPattern(text, col, depth, strictArgs: true);
            if (strict != null) return strict;

            // 2) Склеєні тексти: рядки, частини через « · », перелік через «, ».
            if (text.Contains('\n')) return JoinTranslated(text, "\n", depth);
            if (text.Contains(" · ")) return JoinTranslated(text, " · ", depth);
            if (text.Contains(", ")) { var list = JoinTranslated(text, ", ", depth); if (list != null) return list; }

            // 3) Останній шанс — шаблон з роздільниками всередині аргументу.
            return MatchPattern(text, col, depth, strictArgs: false);
        }

        private static string MatchPattern(string text, int col, int depth, bool strictArgs)
        {
            foreach (var (regex, templates, _) in _patterns)
            {
                var m = regex.Match(text);
                if (!m.Success) continue;
                if (strictArgs && HasSeparatorArg(m)) continue;
                var args = new object[m.Groups.Count - 1];
                for (int i = 1; i < m.Groups.Count; i++)
                    args[i - 1] = TranslateCore(m.Groups[i].Value, depth + 1) ?? m.Groups[i].Value;
                return string.Format(templates[col], args);
            }
            return null;
        }

        private static bool HasSeparatorArg(Match m)
        {
            for (int i = 1; i < m.Groups.Count; i++)
            {
                string v = m.Groups[i].Value;
                if (v.Contains('\n') || v.Contains(" · ")) return true;
            }
            return false;
        }

        private static string JoinTranslated(string text, string sep, int depth)
        {
            var parts = text.Split(new[] { sep }, StringSplitOptions.None);
            bool any = false;
            for (int i = 0; i < parts.Length; i++)
            {
                var t = TranslateCore(parts[i], depth + 1);
                if (t != null && t != parts[i]) { parts[i] = t; any = true; }
            }
            return any ? string.Join(sep, parts) : null;
        }

        private static void EnsureBuilt()
        {
            if (_exact != null) return;
            _exact = new Dictionary<string, string[]>();
            _patterns = new List<(Regex, string[], int)>();
            foreach (var (uk, en, pl) in LocTable.Entries)
            {
                if (!uk.Contains("{0}"))
                {
                    _exact[uk] = new[] { en, pl };
                    continue;
                }
                // "Рівень {0}" → ^Рівень (.+?)$ ; довші літерали перевіряються першими.
                var pieces = Regex.Split(uk, @"\{\d\}");
                string pattern = "^" + string.Join("(.+?)", pieces.Select(Regex.Escape)) + "$";
                int literal = pieces.Sum(p => p.Length);
                // Без Singleline: «.» не перетинає рядки — багаторядкові тексти ділимо окремо.
                _patterns.Add((new Regex(pattern, RegexOptions.CultureInvariant), new[] { en, pl }, literal));
            }
            _patterns.Sort((a, b) => b.literalLength.CompareTo(a.literalLength));
        }

        private static bool HasCyrillic(string s)
        {
            foreach (char c in s)
                if (c >= 'Ѐ' && c <= 'ӿ') return true;
            return false;
        }

        private static string FlipFirst(string s) =>
            s.Length == 0 ? s : (char.IsLower(s[0]) ? char.ToUpper(s[0]) : char.ToLower(s[0])) + s.Substring(1);

        private static string LowerFirst(string s) => s.Length == 0 ? s : char.ToLower(s[0]) + s.Substring(1);
    }
}
