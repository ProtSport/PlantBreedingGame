using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PlantBreeding.Localization
{
    /// <summary>
    /// Перекладає ВСІ тексти TextMeshPro на льоту (сцени, картки, тости), без
    /// змін у коді екранів: слухає подію TMP «текст перегенеровано», запам'ятовує
    /// український оригінал і ставить переклад з Loc. Коли код знову пише в
    /// текст (новий оригінал), переклад оновлюється. Зміна мови — перекласти
    /// все, що вже є на екрані. Живе на DontDestroyOnLoad-об'єкті GameManager.
    /// Поле введення імені (TMP_InputField) не чіпає — це текст гравця.
    ///
    /// Подія TMP приходить ПІД ЧАС перебудови Canvas, а писати в текст там не
    /// можна («graphic rebuild loop»), тож змінені тексти стають у чергу і
    /// перекладаються в LateUpdate (найгірше — один кадр українського тексту).
    /// </summary>
    public class LocalizationApplier : MonoBehaviour
    {
        private class Entry
        {
            public string source;  // український оригінал
            public string applied; // що ми поставили
        }

        private readonly Dictionary<TMP_Text, Entry> _entries = new Dictionary<TMP_Text, Entry>();
        private readonly HashSet<TMP_Text> _pending = new HashSet<TMP_Text>();
        private bool _applying;

        private void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            Loc.Changed += ReapplyAll;
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            Loc.Changed -= ReapplyAll;
        }

        private void OnTextChanged(Object obj)
        {
            if (_applying || !(obj is TMP_Text tmp) || tmp == null) return;
            if (_entries.TryGetValue(tmp, out var e) && tmp.text == e.applied) return; // наш же переклад
            _pending.Add(tmp);
        }

        private void LateUpdate()
        {
            if (_pending.Count == 0) return;
            var batch = new List<TMP_Text>(_pending);
            _pending.Clear();
            foreach (var tmp in batch)
            {
                if (tmp == null || IsPlayerInput(tmp)) continue;
                if (!_entries.TryGetValue(tmp, out var e)) { e = new Entry(); _entries[tmp] = e; }
                if (tmp.text == e.applied) continue;
                e.source = tmp.text; // код записав новий (український) текст
                Apply(tmp, e);
            }
        }

        private void Apply(TMP_Text tmp, Entry e)
        {
            string translated = Loc.Translate(e.source);
            e.applied = translated;
            if (tmp.text == translated) return;
            _applying = true;
            tmp.text = translated;
            _applying = false;
        }

        private void ReapplyAll()
        {
            var dead = new List<TMP_Text>();
            foreach (var kv in _entries)
            {
                if (kv.Key == null) { dead.Add(kv.Key); continue; }
                Apply(kv.Key, kv.Value);
            }
            foreach (var d in dead) _entries.Remove(d);

            // Тексти, які ще жодного разу не перегенеровувались після старту
            // (статичні з самого початку) — пройтись по всіх один раз.
            foreach (var tmp in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (_entries.ContainsKey(tmp) || IsPlayerInput(tmp)) continue;
                var e = new Entry { source = tmp.text };
                _entries[tmp] = e;
                Apply(tmp, e);
            }
        }

        private static bool IsPlayerInput(TMP_Text tmp)
        {
            var input = tmp.GetComponentInParent<TMP_InputField>(true);
            return input != null && input.textComponent == tmp;
        }

#if UNITY_EDITOR
        private float _missingLogTimer;
        private int _loggedMissing;

        // Раз на 5 с — нові тексти без перекладу в консоль (щоб поповнювати LocTable).
        private void Update()
        {
            if (Loc.Current == Language.Uk) return;
            _missingLogTimer -= Time.unscaledDeltaTime;
            if (_missingLogTimer > 0f) return;
            _missingLogTimer = 5f;
            if (Loc.Missing.Count == _loggedMissing) return;
            _loggedMissing = Loc.Missing.Count;
            Debug.Log("[Loc] Без перекладу:\n" + string.Join("\n", Loc.Missing));
        }
#endif
    }
}
