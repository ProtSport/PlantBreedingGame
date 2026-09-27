using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlantBreeding.Core;
using PlantBreeding.Localization;
using PlantBreeding.Save;
using PlantBreeding.Theme;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Нижній лист «Налаштування» (шестірня в шапці, макет «Світла-Темна_тема_Мови»):
    /// Тема — Темна / Світла (картки-прев'ю), Мова — UA / EN / PL.
    /// Вибір зберігається в PlayerData і застосовується одразу (ThemeService, Loc).
    /// Закривається тапом по затемненню, по «ручці» зверху або хрестиком.
    /// </summary>
    public partial class EconomyOverlayView
    {
        private GameObject _settingsSheet;

        private class ThemeCard
        {
            public ThemeMode mode;
            public Image border;
            public TMP_Text label;
        }

        private class LangRow
        {
            public Language lang;
            public Image ring;
            public Image dot;
        }

        private readonly List<ThemeCard> _themeCards = new List<ThemeCard>();
        private readonly List<LangRow> _langRows = new List<LangRow>();

        private void BuildSettingsSheet()
        {
            // Затемнення на весь екран — тап закриває.
            _settingsSheet = new GameObject("SettingsSheet", typeof(RectTransform), typeof(Image), typeof(Button));
            _settingsSheet.transform.SetParent(_overlayRoot, false);
            Stretch((RectTransform)_settingsSheet.transform);
            var dim = _settingsSheet.GetComponent<Image>();
            dim.color = new Color(0.02f, 0.03f, 0.016f, 0.55f);
            var dimBtn = _settingsSheet.GetComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(CloseSettings);

            // Лист знизу. Нижні кути сховані за краєм екрану (-30), видно лише верхні заокруглені.
            var sheet = new GameObject("Sheet", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            sheet.transform.SetParent(_settingsSheet.transform, false);
            var srt = (RectTransform)sheet.transform;
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0);
            srt.pivot = new Vector2(0.5f, 0);
            srt.anchoredPosition = new Vector2(0, -30);
            srt.sizeDelta = new Vector2(0, 0);
            var sbg = sheet.GetComponent<Image>();
            sbg.sprite = sprRounded; sbg.type = Image.Type.Sliced;
            sbg.color = UIColors.Hex("#172013");
            sbg.raycastTarget = true; // тапи по листу не закривають його
            var sline = MakeImage(sheet.transform, "Border", sprRoundedLine, UIColors.Rgba(Color.white, 0.12f), Image.Type.Sliced);
            sline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(sline.rectTransform);

            var vl = sheet.GetComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(20, 20, 10, 34 + 30);
            vl.spacing = 8;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            sheet.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // «Ручка» — теж закриває.
            var grabRow = new GameObject("GrabberRow", typeof(RectTransform), typeof(Image), typeof(Button));
            grabRow.transform.SetParent(sheet.transform, false);
            SetHeight(grabRow, 14);
            grabRow.GetComponent<Image>().color = Color.clear;
            grabRow.GetComponent<Button>().onClick.AddListener(CloseSettings);
            var grab = MakeImage(grabRow.transform, "Grabber", sprPill, UIColors.Rgba(Color.white, 0.2f), Image.Type.Sliced);
            Place(grab.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(40, 5));

            MakeCloseCross(sheet.transform, CloseSettings);

            var title = MakeLabel(sheet.transform, "Title", "Налаштування", fontHead, 28, UIColors.Text, FontStyles.Normal);
            SetHeight(title.gameObject, 36);
            title.margin = new Vector4(0, 0, 40, 0);

            MakeSectionLabel(sheet.transform, "ТЕМА");
            BuildThemeCards(sheet.transform);
            MakeSpacer(sheet.transform, 10);
            MakeSectionLabel(sheet.transform, "МОВА");
            BuildLanguageList(sheet.transform);

            _settingsSheet.SetActive(false);
        }

        private void MakeSectionLabel(Transform parent, string text)
        {
            var l = MakeLabel(parent, "Section", text, fontUi, 11, UIColors.Hex("#8C9683"), FontStyles.Bold);
            l.characterSpacing = 8;
            SetHeight(l.gameObject, 18);
        }

        private void MakeSpacer(Transform parent, float h)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetHeight(go, h);
        }

        // ── Тема: 2 картки-прев'ю ────────────────────────────────────────
        private void BuildThemeCards(Transform parent)
        {
            var row = new GameObject("ThemeRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            SetHeight(row, 104);
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 10;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;

            AddThemeCard(row.transform, ThemeMode.Dark, "Темна");
            AddThemeCard(row.transform, ThemeMode.Light, "Світла");
            // «Як у системі» прибрано з меню за рішенням користувача (ThemeMode.System лишився в сервісі).
        }

        private void AddThemeCard(Transform parent, ThemeMode mode, string label)
        {
            var card = new GameObject("Theme_" + mode, typeof(RectTransform), typeof(Image), typeof(Button));
            card.transform.SetParent(parent, false);
            var hit = card.GetComponent<Image>();
            hit.color = Color.clear;
            card.GetComponent<Button>().onClick.AddListener(() => SetTheme(mode));

            // Прев'ю — завжди у «своїх» кольорах, незалежно від активної теми.
            var preview = new GameObject("Preview", typeof(RectTransform), typeof(RectMask2D), typeof(ThemeIgnore));
            preview.transform.SetParent(card.transform, false);
            var prt = (RectTransform)preview.transform;
            prt.anchorMin = new Vector2(0, 1); prt.anchorMax = new Vector2(1, 1);
            prt.pivot = new Vector2(0.5f, 1);
            prt.anchoredPosition = Vector2.zero; prt.sizeDelta = new Vector2(0, 78);

            bool dark = mode == ThemeMode.Dark;
            var bg = MakeImage(preview.transform, "Bg", sprRounded, UIColors.Hex(dark ? "#1E2A18" : "#FBF3E4"), Image.Type.Sliced);
            Stretch(bg.rectTransform);
            // Дві «плитки» саду в кольорах теми (без смужки-заголовка зверху).
            for (int i = 0; i < 2; i++)
            {
                var tile = MakeImage(preview.transform, "Tile" + i, sprRounded,
                    UIColors.Hex(dark ? (i == 0 ? "#3F5A2E" : "#2A3324") : (i == 0 ? "#D6EBC4" : "#EDE3CE")), Image.Type.Sliced);
                var trt = tile.rectTransform;
                trt.anchorMin = new Vector2(i == 0 ? 0.1f : 0.53f, 0.14f);
                trt.anchorMax = new Vector2(i == 0 ? 0.47f : 0.9f, 0.86f);
                trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            }

            var border = MakeImage(card.transform, "Border", sprRoundedLine, UIColors.Rgba(Color.white, 0.12f), Image.Type.Sliced);
            var brt = border.rectTransform;
            brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1);
            brt.pivot = new Vector2(0.5f, 1);
            brt.anchoredPosition = Vector2.zero; brt.sizeDelta = new Vector2(0, 78);

            var text = MakeLabel(card.transform, "Label", label, fontUi, 12, UIColors.Soft, FontStyles.Bold);
            Place(text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 2), new Vector2(120, 18));
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true; text.fontSizeMin = 9; text.fontSizeMax = 12;

            _themeCards.Add(new ThemeCard { mode = mode, border = border, label = text });
        }

        // ── Мова: 3 рядки з радіо ────────────────────────────────────────
        private void BuildLanguageList(Transform parent)
        {
            var list = MakeImage(parent, "LangList", sprRounded, UIColors.Rgba(Color.white, 0.04f), Image.Type.Sliced);
            var vl = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            SetHeight(list.gameObject, 3 * 48);
            var line = MakeImage(list.transform, "Border", sprRoundedLine, UIColors.Rgba(Color.white, 0.08f), Image.Type.Sliced);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(line.rectTransform);

            AddLangRow(list.transform, Language.Uk, "UA", "Українська", divider: true);
            AddLangRow(list.transform, Language.En, "EN", "English", divider: true);
            AddLangRow(list.transform, Language.Pl, "PL", "Polski", divider: false);
        }

        private void AddLangRow(Transform parent, Language lang, string code, string name, bool divider)
        {
            var row = new GameObject("Lang_" + code, typeof(RectTransform), typeof(Image), typeof(Button));
            row.transform.SetParent(parent, false);
            SetHeight(row, 48);
            row.GetComponent<Image>().color = Color.clear;
            row.GetComponent<Button>().onClick.AddListener(() => SetLanguage(lang));

            var codeLbl = MakeLabel(row.transform, "Code", code, fontUi, 11, UIColors.Hex("#8C9683"), FontStyles.Bold);
            Place(codeLbl.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(30, 20));
            var nameLbl = MakeLabel(row.transform, "Name", name, fontUi, 14, UIColors.Text, FontStyles.Bold);
            Place(nameLbl.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(56, 0), new Vector2(200, 22));

            var ring = MakeImage(row.transform, "Ring", sprCircle, Color.clear, Image.Type.Simple);
            Place(ring.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(20, 20));
            // Кільце: зовнішнє коло кольору рамки + внутрішнє кольору листа (2 px).
            var hole = MakeImage(ring.transform, "Hole", sprCircle, UIColors.Hex("#1C2616"), Image.Type.Simple);
            Place(hole.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
            var dot = MakeImage(ring.transform, "Dot", sprCircle, UIColors.Green, Image.Type.Simple);
            Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10));

            if (divider)
            {
                var d = MakeImage(row.transform, "Divider", null, UIColors.Rgba(Color.white, 0.06f), Image.Type.Simple);
                d.rectTransform.anchorMin = new Vector2(0, 0); d.rectTransform.anchorMax = new Vector2(1, 0);
                d.rectTransform.pivot = new Vector2(0.5f, 0);
                d.rectTransform.anchoredPosition = Vector2.zero; d.rectTransform.sizeDelta = new Vector2(0, 1);
            }

            _langRows.Add(new LangRow { lang = lang, ring = ring, dot = dot });
        }

        // ── Дії ──────────────────────────────────────────────────────────
        public void OpenSettings()
        {
            if (_settingsSheet == null) return;
            _settingsSheet.SetActive(true);
            _settingsSheet.transform.SetAsLastSibling();
            RefreshSettings();
        }

        private void CloseSettings()
        {
            if (_settingsSheet != null) _settingsSheet.SetActive(false);
        }

        private void SetTheme(ThemeMode mode)
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.playerData.themeMode = (int)mode;
                SaveSystem.Save(gm.playerData);
            }
            ThemeService.SetMode(mode);
            RefreshSettings();
        }

        private void SetLanguage(Language lang)
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.playerData.language = Loc.Code(lang);
                SaveSystem.Save(gm.playerData);
            }
            Loc.Set(lang);
            RefreshSettings();
        }

        private void RefreshSettings()
        {
            foreach (var c in _themeCards)
            {
                bool selected = ThemeService.Mode == c.mode;
                c.border.color = selected ? UIColors.Green : UIColors.Rgba(Color.white, 0.12f);
                c.label.color = selected ? UIColors.Hex("#CDEBA0") : UIColors.Hex("#A9B2A0");
            }
            foreach (var r in _langRows)
            {
                bool selected = Loc.Current == r.lang;
                r.ring.color = selected ? UIColors.Hex("#CDEBA0") : UIColors.Rgba(Color.white, 0.3f);
                r.dot.gameObject.SetActive(selected);
            }
        }
    }
}
