using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlantBreeding.Core;
using PlantBreeding.Collections;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Логіка екрану «Дендрарій · Колекції» (ТЗ «Дендрарій — тільки колекції»).
    /// Assets/Editor/DexScreenBuilder.cs будує статичний каркас (шапка/скрол/
    /// деталь-оверлей/нижнє меню), цей скрипт наповнює список картками.
    ///
    /// Каркасний прохід: дані колекцій — плейсхолдер із CollectionCatalog
    /// (у грі поки лише вид rose_basic). Єдина «справжня» логіка — лічильник
    /// «відкрито X з Y видів» рахується з реального реєстру
    /// PlayerData.discoveredPlantIds (Pokédex, заповнюється в PlotSlot.Harvest).
    ///
    /// Картки/чіпи будуються ОДИН РАЗ у Start() і НІКОЛИ не знищуються (як у
    /// PlantingScreenController/LabScreenController) — фільтр лише перемикає
    /// SetActive вже існуючих карток. Деталь-оверлей — неклікабельний вміст,
    /// тому його безпечно перебудовувати при кожному відкритті.
    /// </summary>
    public class DexScreenController : MonoBehaviour
    {
        private const string DexSceneName = "Dex";

        [Header("Шапка")]
        public TMP_Text coinsLabel;
        public TMP_Text gemsLabel;

        [Header("Список (наповнюється в рантаймі)")]
        public Transform listContent;

        [Header("Деталь-оверлей (розкрита колекція)")]
        public GameObject detailPanel;
        public Button detailBackButton;
        public TMP_Text detailKicker;
        public TMP_Text detailTitle;
        public TMP_Text detailCount;
        public Transform detailContent;
        public Image detailClaimBg;
        public Button detailClaimButton;
        public TMP_Text detailClaimLabel;

        [Header("Тост")]
        public GameObject toastRoot;
        public TMP_Text toastLabel;

        [Header("Спільна графіка (з UIBuilderKit)")]
        public Sprite sprRoundedCard;
        public Sprite sprRoundedCardLine;
        public Sprite sprRoundedSmall;
        public Sprite sprRoundedSmallLine;
        public Sprite sprPillTiny;
        public Sprite sprSprig;
        public Sprite sprIconCheck;
        public Sprite sprGlow;
        public TMP_FontAsset fontHead;
        public TMP_FontAsset fontUi;

        private struct Rar { public Color fill, bg, border; }

        private class CardHandle
        {
            public CollectionDef def;
            public GameObject root;
        }

        private readonly List<CardHandle> _cards = new List<CardHandle>();
        private readonly List<Image> _chipBgs = new List<Image>();
        private readonly List<TMP_Text> _chipLabels = new List<TMP_Text>();
        private TMP_Text _discoveryLabel;
        private int _activeChip;
        private float _tick;
        private float _toastTimer;

        // ════════════════════════════════════════════════════════════════
        private void Start()
        {
            GameManager.Instance?.MarkDexSeen(); // бейдж нових рослин на іконці Дендрарію зникає
            if (detailBackButton != null) detailBackButton.onClick.AddListener(CloseDetail);
            if (detailClaimButton != null) detailClaimButton.onClick.AddListener(HandleClaimClick);

            BuildList();
            ApplyChipFilter();
            RefreshHeader();

            if (detailPanel != null) detailPanel.SetActive(false);
            if (toastRoot != null) toastRoot.SetActive(false);
        }

        private void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f && toastRoot != null) toastRoot.SetActive(false);
            }

            _tick += Time.deltaTime;
            if (_tick < 1f) return;
            _tick = 0f;
            RefreshHeader();
        }

        // ════════════════════════════════════════════════════════════════
        //  ПОБУДОВА СПИСКУ (один раз)
        // ════════════════════════════════════════════════════════════════
        private void BuildList()
        {
            ClearChildren(listContent);
            _cards.Clear();
            _chipBgs.Clear();
            _chipLabels.Clear();

            BuildDiscoveryRow(listContent);
            BuildWeekCard(listContent);
            BuildChipsRow(listContent);

            foreach (var def in CollectionCatalog.Collections)
                _cards.Add(BuildCollectionCard(listContent, def));

            BuildShowcaseCard(listContent);
        }

        private void BuildDiscoveryRow(Transform parent)
        {
            var row = new GameObject("DiscoveryRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 22;
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 8; hl.padding = new RectOffset(4, 4, 0, 0);
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;

            var star = MakeImage(row.transform, "Star", sprSprig, ColGold, Image.Type.Simple);
            star.preserveAspect = true;
            var sle = star.gameObject.AddComponent<LayoutElement>();
            sle.preferredWidth = 16; sle.preferredHeight = 16;

            _discoveryLabel = MakeLabel(row.transform, "Label", "", fontUi, 13, Hex("#A9B2A0"), FontStyles.Bold);
            _discoveryLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            _discoveryLabel.alignment = TextAlignmentOptions.MidlineLeft;
        }

        // ── Картка тижня (золота рамка + таймер) АБО заглушка-гейт ─────────
        // (ТЗ п.7: тема тижня доступна лише коли гравець відкрив 4+ видів)
        private void BuildWeekCard(Transform parent)
        {
            var gm = GameManager.Instance;
            var discoveredIds = gm != null ? gm.playerData.discoveredPlantIds : null;
            var week = CollectionCatalog.GetWeeklyCollection(discoveredIds);
            if (week == null)
            {
                BuildWeekLockedCard(parent);
                return;
            }

            var card = new GameObject("WeekCard", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            card.transform.SetParent(parent, false);
            var bg = card.GetComponent<Image>();
            bg.sprite = sprRoundedCard; bg.type = Image.Type.Sliced;
            bg.color = Hex("#241E10");
            bg.raycastTarget = true;
            card.GetComponent<LayoutElement>().flexibleWidth = 1;
            var vl = card.GetComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(17, 17, 16, 17); vl.spacing = 11;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            card.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var frame = MakeImage(card.transform, "GoldFrame", sprRoundedCardLine, ColGoldLt, Image.Type.Sliced);
            Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // Ряд: "Колекція тижня" + таймер
            var top = MakeRow(card.transform, 26, 7);
            var flame = MakeLabel(top.transform, "Kicker", "Колекція тижня", fontHead, 22, ColGoldLt, FontStyles.Normal);
            flame.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            flame.alignment = TextAlignmentOptions.MidlineLeft;
            var timer = MakePill(top.transform, "⏱ " + week.endsIn, ColGold, Rgba(ColGold, 0.12f), Rgba(ColGold, 0.32f));
            timer.fontSize = 11;

            var title = MakeLabel(card.transform, "Title", week.title, fontHead, 20, ColText, FontStyles.Normal);
            title.rectTransform.sizeDelta = new Vector2(0, 24);
            title.alignment = TextAlignmentOptions.MidlineLeft;

            var desc = MakeLabel(card.transform, "Desc", week.desc, fontUi, 12.5f, Hex("#A9B2A0"), FontStyles.Normal);
            desc.alignment = TextAlignmentOptions.TopLeft;
            AddFlexLabel(desc);

            BuildSlotRow(card.transform, week.slots, showCheck: true);

            // Прогрес + кнопка "Переглянути"
            var progRow = MakeRow(card.transform, 44, 12);
            var col = BuildProgressColumn(progRow.transform, "Прогрес", week.count, week.pct, ColGold, ColGold);
            col.GetComponent<LayoutElement>().flexibleWidth = 1;
            var btn = MakeActionButton(progRow.transform, "Переглянути", claim: false);
            var weekDetail = WeekDetail(week);
            AddClick(card, () => OpenDetail(weekDetail));
            btn.onClick.AddListener(() => OpenDetail(weekDetail));
        }

        // ── Заглушка тижневої колекції, коли відкрито < 4 видів (ТЗ п.7) ───
        private void BuildWeekLockedCard(Transform parent)
        {
            var card = new GameObject("WeekCardLocked", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            card.transform.SetParent(parent, false);
            var bg = card.GetComponent<Image>();
            bg.sprite = sprRoundedCard; bg.type = Image.Type.Sliced;
            bg.color = Rgba(Color.white, 0.04f);
            card.GetComponent<LayoutElement>().flexibleWidth = 1;
            var vl = card.GetComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(17, 17, 16, 17); vl.spacing = 8;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            card.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var border = MakeImage(card.transform, "Border", sprRoundedCardLine, Rgba(Color.white, 0.08f), Image.Type.Sliced);
            Stretch(border.rectTransform);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var title = MakeLabel(card.transform, "Title", "Колекція тижня", fontHead, 20, ColMut, FontStyles.Normal);
            title.alignment = TextAlignmentOptions.MidlineLeft;

            var desc = MakeLabel(card.transform, "Desc",
                "Відкрий більше видів, щоб отримати доступ до Колекції тижня.",
                fontUi, 12.5f, Hex("#8C9683"), FontStyles.Normal);
            desc.alignment = TextAlignmentOptions.TopLeft;
            AddFlexLabel(desc);
        }

        private void BuildChipsRow(Transform parent)
        {
            var row = new GameObject("Chips", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 34;
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 8; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

            for (int i = 0; i < CollectionCatalog.Chips.Length; i++)
            {
                int idx = i;
                var chip = new GameObject("Chip_" + i, typeof(RectTransform), typeof(Image), typeof(Button),
                    typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                chip.transform.SetParent(row.transform, false);
                var img = chip.GetComponent<Image>();
                img.sprite = sprPillTiny; img.type = Image.Type.Sliced;
                img.raycastTarget = true;
                var chl = chip.GetComponent<HorizontalLayoutGroup>();
                chl.padding = new RectOffset(12, 12, 8, 8); chl.childAlignment = TextAnchor.MiddleCenter;
                chl.childControlWidth = true; chl.childControlHeight = true;
                chl.childForceExpandWidth = false; chl.childForceExpandHeight = false;
                chip.GetComponent<LayoutElement>().preferredHeight = 34;

                var label = MakeLabel(chip.transform, "Label", CollectionCatalog.Chips[i], fontUi, 13, Color.white, FontStyles.Bold);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                var btn = chip.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => { _activeChip = idx; ApplyChipFilter(); });

                _chipBgs.Add(img);
                _chipLabels.Add(label);
            }
        }

        // ── Картка колекції ───────────────────────────────────────────────
        private CardHandle BuildCollectionCard(Transform parent, CollectionDef def)
        {
            var r = GetRar(def.accent);

            var card = new GameObject("Coll_" + def.id, typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            card.transform.SetParent(parent, false);
            var bg = card.GetComponent<Image>();
            bg.sprite = sprRoundedCard; bg.type = Image.Type.Sliced;
            bg.color = Rgba(Color.white, 0.05f);
            bg.raycastTarget = true;
            card.GetComponent<LayoutElement>().flexibleWidth = 1;
            var vl = card.GetComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(16, 16, 15, 16); vl.spacing = 12;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            card.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var border = MakeImage(card.transform, "Border", sprRoundedCardLine, Rgba(Color.white, 0.10f), Image.Type.Sliced);
            Stretch(border.rectTransform);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // Ряд заголовка: [іконка-гілочка][назва flex][тип-пігулка]
            var head = MakeRow(card.transform, 32, 9);
            var iconBox = MakeImage(head.transform, "IconBox", sprRoundedSmall, r.bg, Image.Type.Sliced);
            var ibl = iconBox.gameObject.AddComponent<LayoutElement>();
            ibl.preferredWidth = 30; ibl.preferredHeight = 30;
            var ibLine = MakeImage(iconBox.transform, "Border", sprRoundedSmallLine, r.border, Image.Type.Sliced);
            Stretch(ibLine.rectTransform);
            var ico = MakeImage(iconBox.transform, "Sprig", sprSprig, r.fill, Image.Type.Simple);
            ico.preserveAspect = true;
            Place(ico.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));

            var name = MakeLabel(head.transform, "Name", def.name, fontHead, 21, ColText, FontStyles.Normal);
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            name.alignment = TextAlignmentOptions.MidlineLeft;

            var typePill = MakePill(head.transform, def.typeLabel, r.fill, r.bg, r.border);
            typePill.fontSize = 10.5f;

            var desc = MakeLabel(card.transform, "Desc", def.desc, fontUi, 12.5f, ColMut, FontStyles.Normal);
            desc.alignment = TextAlignmentOptions.TopLeft;
            AddFlexLabel(desc);

            BuildSlotRow(card.transform, def.miniSlots, showCheck: false);

            // Прогрес + кнопка
            var progRow = MakeRow(card.transform, 40, 12);
            var col = BuildProgressColumn(progRow.transform, def.claim ? "Готово" : "Зібрано", def.count, def.pct, ColMut, r.fill);
            col.GetComponent<LayoutElement>().flexibleWidth = 1;
            var btn = MakeActionButton(progRow.transform, def.claim ? "Забрати" : "Переглянути", def.claim);

            // Рядок нагороди
            var rewardRow = new GameObject("Reward", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            rewardRow.transform.SetParent(card.transform, false);
            var rvl = rewardRow.GetComponent<VerticalLayoutGroup>();
            rvl.spacing = 10; rvl.padding = new RectOffset(0, 0, 2, 0);
            rvl.childControlWidth = true; rvl.childControlHeight = true;
            rvl.childForceExpandWidth = true; rvl.childForceExpandHeight = false;
            var divider = MakeImage(rewardRow.transform, "Divider", null, Rgba(Color.white, 0.08f), Image.Type.Simple);
            divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 1;
            var rewardLabel = MakeLabel(rewardRow.transform, "Text", "Нагорода: " + def.reward, fontUi, 12, Hex("#A9B2A0"), FontStyles.Normal);
            rewardLabel.alignment = TextAlignmentOptions.MidlineLeft;
            AddFlexLabel(rewardLabel);

            var detail = def.detail;
            AddClick(card, () => OpenDetail(detail));
            if (def.claim)
                btn.onClick.AddListener(() => OpenDetail(detail)); // «Забрати» відкриває деталь із claim-баром
            else
                btn.onClick.AddListener(() => OpenDetail(detail));

            return new CardHandle { def = def, root = card };
        }

        // ── Особиста вітрина (без прогресу/нагороди) ──────────────────────
        private void BuildShowcaseCard(Transform parent)
        {
            var card = new GameObject("Showcase", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            card.transform.SetParent(parent, false);
            var bg = card.GetComponent<Image>();
            bg.sprite = sprRoundedCard; bg.type = Image.Type.Sliced;
            bg.color = Rgba(Hex("#9FB2E6"), 0.1f);
            card.GetComponent<LayoutElement>().flexibleWidth = 1;
            var vl = card.GetComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(16, 16, 15, 16); vl.spacing = 11;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            card.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var border = MakeImage(card.transform, "Border", sprRoundedCardLine, Rgba(Hex("#9FB2E6"), 0.24f), Image.Type.Sliced);
            Stretch(border.rectTransform);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var head = MakeRow(card.transform, 32, 9);
            var name = MakeLabel(head.transform, "Name", "Особиста вітрина", fontHead, 21, Hex("#E7E0D2"), FontStyles.Normal);
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            name.alignment = TextAlignmentOptions.MidlineLeft;
            MakeActionButton(head.transform, "Редагувати", claim: false, blue: true);

            var desc = MakeLabel(card.transform, "Desc", "5 будь-яких відкритих рослин на показ друзям — без прогресу й нагороди.", fontUi, 12.5f, ColMut, FontStyles.Normal);
            desc.alignment = TextAlignmentOptions.TopLeft;
            AddFlexLabel(desc);

            var showcase = new List<SlotDef>(CollectionCatalog.Showcase) { new SlotDef("add", false) };
            BuildSlotRow(card.transform, showcase, showCheck: false);
        }

        // ════════════════════════════════════════════════════════════════
        //  ДЕТАЛЬ-ОВЕРЛЕЙ (розкрита колекція) — перебудовується при відкритті
        // ════════════════════════════════════════════════════════════════
        private void OpenDetail(DetailDef d)
        {
            if (detailPanel == null || d == null) return;

            if (detailKicker != null) detailKicker.text = d.typeLabel.ToUpper();
            if (detailTitle != null) detailTitle.text = d.name;
            if (detailCount != null) detailCount.text = d.count;

            ClearChildren(detailContent);

            // Вступ + прогрес-бар
            var intro = new GameObject("Intro", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            intro.transform.SetParent(detailContent, false);
            var introBg = intro.GetComponent<Image>();
            introBg.sprite = sprRoundedCard; introBg.type = Image.Type.Sliced; introBg.color = Rgba(Color.white, 0.05f);
            var ivl = intro.GetComponent<VerticalLayoutGroup>();
            ivl.padding = new RectOffset(17, 17, 16, 16); ivl.spacing = 13;
            ivl.childControlWidth = true; ivl.childControlHeight = true;
            ivl.childForceExpandWidth = true; ivl.childForceExpandHeight = false;
            intro.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var introText = MakeLabel(intro.transform, "Text", d.intro, fontUi, 13, Hex("#A9B2A0"), FontStyles.Normal);
            introText.alignment = TextAlignmentOptions.TopLeft;
            AddFlexLabel(introText);
            BuildTrack(intro.transform, d.pct, ColGreen, 9);

            // Сітка слотів 2×N (GridLayoutGroup сам звітує preferred-висоту
            // батьківському VerticalLayoutGroup — CSF тут не потрібен).
            var grid = new GameObject("Slots", typeof(RectTransform), typeof(GridLayoutGroup));
            grid.transform.SetParent(detailContent, false);
            var gl = grid.GetComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(166, 150); gl.spacing = new Vector2(13, 13);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 2;
            foreach (var slot in d.slots) BuildDetailSlot(grid.transform, slot);

            // Підказка
            var hint = new GameObject("Hint", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            hint.transform.SetParent(detailContent, false);
            var hintBg = hint.GetComponent<Image>();
            hintBg.sprite = sprRoundedCard; hintBg.type = Image.Type.Sliced; hintBg.color = Rgba(Hex("#9FB2E6"), 0.08f);
            var hhl = hint.GetComponent<HorizontalLayoutGroup>();
            hhl.padding = new RectOffset(14, 14, 12, 12); hhl.spacing = 10;
            hhl.childControlWidth = true; hhl.childControlHeight = true;
            hhl.childForceExpandWidth = true; hhl.childForceExpandHeight = false;
            hhl.childAlignment = TextAnchor.MiddleLeft;
            hint.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var hintText = MakeLabel(hint.transform, "Text", d.hint, fontUi, 12.5f, Hex("#B9C4EC"), FontStyles.Normal);
            hintText.alignment = TextAlignmentOptions.MidlineLeft;
            AddFlexLabel(hintText);

            // Claim-бар
            bool complete = d.pct >= 100;
            if (detailClaimBg != null) detailClaimBg.color = complete ? ColGreenBright : Rgba(Color.white, 0.05f);
            if (detailClaimLabel != null)
            {
                detailClaimLabel.text = complete ? "Забрати нагороду" : d.claimLabel;
                detailClaimLabel.color = complete ? Hex("#0E130C") : Hex("#7C8573");
            }
            if (detailClaimButton != null) detailClaimButton.interactable = complete;

            detailPanel.SetActive(true);
        }

        private void BuildDetailSlot(Transform parent, SlotDef slot)
        {
            var r = GetRar(slot.rarity);
            bool done = slot.done && slot.rarity != "lock";

            var cell = new GameObject("Slot", typeof(RectTransform), typeof(Image));
            cell.transform.SetParent(parent, false);
            var bg = cell.GetComponent<Image>();
            bg.sprite = sprRoundedCard; bg.type = Image.Type.Sliced;
            bg.color = done ? r.bg : Rgba(Color.white, 0.02f);
            var border = MakeImage(cell.transform, "Border", sprRoundedCardLine, r.border, Image.Type.Sliced);
            Stretch(border.rectTransform);

            var sprig = MakeImage(cell.transform, "Sprig", sprSprig, done ? r.fill : ColDim, Image.Type.Simple);
            sprig.preserveAspect = true;
            if (!done) sprig.color = Rgba(ColDim, 0.35f);
            Place(sprig.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(62, 62));

            if (!done)
            {
                var q = MakeLabel(cell.transform, "Q", "?", fontHead, 42, Hex("#4E5847"), FontStyles.Normal);
                Place(q.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -28), new Vector2(60, 60));
                q.alignment = TextAlignmentOptions.Center;
            }
            else if (!string.IsNullOrEmpty(slot.tier))
            {
                var badge = MakePill(cell.transform, slot.tier, r.fill, r.bg, r.border);
                badge.fontSize = 10;
                var brt = ((RectTransform)badge.transform.parent);
                brt.anchorMin = brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(1, 1);
                brt.anchoredPosition = new Vector2(-10, -10);
                brt.sizeDelta = new Vector2(brt.sizeDelta.x, 20); // висота (пігулка поза layout-групою)
            }

            var name = MakeLabel(cell.transform, "Name", slot.name ?? "", fontHead, 18, done ? ColText : Hex("#6E7865"), FontStyles.Normal);
            Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(150, 22));
            name.alignment = TextAlignmentOptions.Center;

            var sub = MakeLabel(cell.transform, "Sub", done ? (slot.sub ?? "") : "ще не відкрито", fontUi, 11, done ? ColMut : ColDim, FontStyles.Normal);
            Place(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(150, 16));
            sub.alignment = TextAlignmentOptions.Center;
        }

        private void CloseDetail()
        {
            if (detailPanel != null) detailPanel.SetActive(false);
        }

        private void HandleClaimClick()
        {
            // TODO: реальна видача нагороди + claimedSlotCount (ТЗ п.4) — з'явиться
            // разом із CollectionService. Каркасний прохід лише підтверджує тап.
            ShowToast("Нагороду отримано");
            if (detailClaimLabel != null) detailClaimLabel.text = "Отримано";
            if (detailClaimBg != null) detailClaimBg.color = Rgba(Color.white, 0.05f);
            if (detailClaimButton != null) detailClaimButton.interactable = false;
        }

        // ════════════════════════════════════════════════════════════════
        //  ФІЛЬТР / ШАПКА
        // ════════════════════════════════════════════════════════════════
        private void ApplyChipFilter()
        {
            string filter = CollectionCatalog.Chips[_activeChip];
            for (int i = 0; i < _chipBgs.Count; i++)
            {
                bool on = i == _activeChip;
                _chipBgs[i].color = on ? ColGreen : Rgba(Color.white, 0.055f);
                _chipLabels[i].color = on ? Hex("#0E130C") : Hex("#C9CFC1");
            }

            foreach (var card in _cards)
            {
                bool show = _activeChip == 0 || card.def.typeLabel == filter;
                if (card.root != null) card.root.SetActive(show);
            }
        }

        private void RefreshHeader()
        {
            var gm = GameManager.Instance;
            var data = gm != null ? gm.playerData : null;
            if (coinsLabel != null) coinsLabel.text = data != null ? data.coins.ToString("N0") : "0";
            if (gemsLabel != null) gemsLabel.text = data != null ? data.gems.ToString("N0") : "0";

            if (_discoveryLabel != null)
            {
                int discovered = gm != null ? gm.DiscoveredSpeciesCount : 0;
                int total = Mathf.Max(discovered, TotalSpeciesCount());
                _discoveryLabel.text = $"Відкрито {discovered} з {total} видів · {CollectionCatalog.Collections.Count} колекцій";
            }
        }

        private static int _cachedTotal = -1;
        private static int TotalSpeciesCount()
        {
            if (_cachedTotal < 0)
                _cachedTotal = Resources.LoadAll<Garden.PlantData>("Plants").Length;
            return _cachedTotal;
        }

        private void ShowToast(string message)
        {
            if (toastLabel != null) toastLabel.text = message;
            if (toastRoot != null) toastRoot.SetActive(true);
            _toastTimer = 1.8f;
        }

        // ════════════════════════════════════════════════════════════════
        //  ХЕЛПЕРИ ПОБУДОВИ
        // ════════════════════════════════════════════════════════════════
        private DetailDef WeekDetail(WeekDef w) => new DetailDef
        {
            typeLabel = "Колекція тижня · " + w.endsIn, name = w.title, count = w.count, pct = w.pct,
            intro = w.desc + " Незавершена до кінця тижня колекція обнуляється разом із заміною на нову (ТЗ п.4).",
            hint = "Види тижня обираються з тих, що ти вже відкривав — це повторний збір, а не новий контент.",
            claimLabel = "Забери до кінця тижня",
            slots = w.slots,
        };

        private GameObject MakeRow(Transform parent, float height, float spacing)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = height;
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            return row;
        }

        private void BuildSlotRow(Transform parent, List<SlotDef> slots, bool showCheck)
        {
            var row = new GameObject("SlotRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 60;
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 8; hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
            foreach (var slot in slots) BuildSlotMini(row.transform, slot, showCheck);
        }

        private void BuildSlotMini(Transform parent, SlotDef slot, bool showCheck)
        {
            bool add = slot.rarity == "add";
            var r = GetRar(slot.rarity);
            bool done = slot.done && !add && slot.rarity != "lock";

            var cell = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            cell.transform.SetParent(parent, false);
            cell.GetComponent<LayoutElement>().flexibleWidth = 1;
            var bg = cell.GetComponent<Image>();
            bg.sprite = sprRoundedSmall; bg.type = Image.Type.Sliced;
            bg.color = add ? Rgba(Hex("#9FB2E6"), 0.05f) : r.bg;
            var border = MakeImage(cell.transform, "Border", sprRoundedSmallLine, add ? Rgba(Hex("#9FB2E6"), 0.3f) : r.border, Image.Type.Sliced);
            Stretch(border.rectTransform);

            if (add)
            {
                var plus = MakeLabel(cell.transform, "Plus", "+", fontHead, 26, Hex("#9FB2E6"), FontStyles.Normal);
                Stretch(plus.rectTransform);
                plus.alignment = TextAlignmentOptions.Center;
                return;
            }

            if (done)
            {
                var sprig = MakeImage(cell.transform, "Sprig", sprSprig, r.fill, Image.Type.Simple);
                sprig.preserveAspect = true;
                Place(sprig.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));

                if (showCheck)
                {
                    var ring = MakeImage(cell.transform, "CheckRing", sprRoundedSmall, Hex("#8FBF5A"), Image.Type.Sliced);
                    Place(ring.rectTransform, new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-3, 3), new Vector2(18, 18));
                    var check = MakeImage(ring.transform, "Check", sprIconCheck, Hex("#0E130C"), Image.Type.Simple);
                    check.preserveAspect = true;
                    Place(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(11, 11));
                }
            }
            else
            {
                var q = MakeLabel(cell.transform, "Q", "?", fontHead, 20, Hex("#4E5847"), FontStyles.Normal);
                Stretch(q.rectTransform);
                q.alignment = TextAlignmentOptions.Center;
            }
        }

        private GameObject BuildProgressColumn(Transform parent, string leftText, string count, int pct, Color leftColor, Color countColor)
        {
            var col = new GameObject("Progress", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            col.transform.SetParent(parent, false);
            var vl = col.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 5; vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

            var labelRow = MakeRow(col.transform, 15, 0);
            var left = MakeLabel(labelRow.transform, "Left", leftText, fontUi, 11.5f, leftColor, FontStyles.Bold);
            left.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            left.alignment = TextAlignmentOptions.MidlineLeft;
            var cnt = MakeLabel(labelRow.transform, "Count", count, fontUi, 11.5f, countColor, FontStyles.Bold);
            cnt.gameObject.AddComponent<LayoutElement>().preferredWidth = 44;
            cnt.alignment = TextAlignmentOptions.MidlineRight;

            BuildTrack(col.transform, pct, countColor, 7);
            return col;
        }

        private void BuildTrack(Transform parent, int pct, Color fillColor, float height)
        {
            var track = MakeImage(parent, "Track", sprPillTiny, Rgba(Color.white, 0.09f), Image.Type.Sliced);
            track.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var fill = MakeImage(track.transform, "Fill", sprPillTiny, fillColor, Image.Type.Sliced);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(Mathf.Clamp01(pct / 100f), 1);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        }

        private Button MakeActionButton(Transform parent, string label, bool claim, bool blue = false)
        {
            var go = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = claim ? 96 : 118; le.preferredHeight = 38;
            var img = go.GetComponent<Image>();
            img.sprite = sprRoundedSmall; img.type = Image.Type.Sliced;
            img.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            Color textColor;
            if (claim) { img.color = Hex("#8FBF5A"); textColor = Hex("#0E130C"); }
            else if (blue) { img.color = Rgba(Hex("#9FB2E6"), 0.14f); textColor = Hex("#C6D0E8"); }
            else { img.color = Rgba(Color.white, 0.06f); textColor = Hex("#C9CFC1"); }

            var lbl = MakeLabel(go.transform, "Label", label, fontUi, 13, textColor, FontStyles.Bold);
            Stretch(lbl.rectTransform);
            lbl.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        // Пігулка-мітка (тип/таймер/tier). Повертає TMP лейбл для донастройки.
        private TMP_Text MakePill(Transform parent, string text, Color textColor, Color bgColor, Color borderColor)
        {
            var pill = new GameObject("Pill", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            pill.transform.SetParent(parent, false);
            var img = pill.GetComponent<Image>();
            img.sprite = sprPillTiny; img.type = Image.Type.Sliced; img.color = bgColor;
            var hl = pill.GetComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(9, 9, 4, 4); hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            pill.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var border = MakeImage(pill.transform, "Border", null, borderColor, Image.Type.Sliced);
            border.sprite = sprPillTiny;
            Stretch(border.rectTransform);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var label = MakeLabel(pill.transform, "Label", text, fontUi, 11, textColor, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
            return label;
        }

        private void AddClick(GameObject cardWithImage, UnityEngine.Events.UnityAction action)
        {
            var btn = cardWithImage.GetComponent<Button>();
            if (btn == null) btn = cardWithImage.AddComponent<Button>();
            btn.targetGraphic = cardWithImage.GetComponent<Image>();
            btn.onClick.AddListener(action);
        }

        private void AddFlexLabel(TMP_Text label)
        {
            var le = label.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = label.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.GetChild(i).gameObject);
        }

        // ── Палітра рідкості слота (green|gold|rose|blue|violet|lock) ──────
        private static Rar GetRar(string key) => key switch
        {
            "green" => new Rar { fill = Hex("#A7CE73"), bg = Rgba(Hex("#A7CE73"), 0.1f), border = Rgba(Hex("#A7CE73"), 0.28f) },
            "gold" => new Rar { fill = Hex("#E4C77E"), bg = Rgba(Hex("#E4C77E"), 0.1f), border = Rgba(Hex("#E4C77E"), 0.28f) },
            "rose" => new Rar { fill = Hex("#E7A6B4"), bg = Rgba(Hex("#E7A6B4"), 0.1f), border = Rgba(Hex("#E7A6B4"), 0.28f) },
            "blue" => new Rar { fill = Hex("#9FB2E6"), bg = Rgba(Hex("#9FB2E6"), 0.1f), border = Rgba(Hex("#9FB2E6"), 0.28f) },
            "violet" => new Rar { fill = Hex("#B9A6E6"), bg = Rgba(Hex("#B9A6E6"), 0.1f), border = Rgba(Hex("#B9A6E6"), 0.28f) },
            _ => new Rar { fill = ColDim, bg = Rgba(Color.white, 0.02f), border = Rgba(Color.white, 0.08f) },
        };

        // ── Дрібні хелпери (копії, як у LabScreenController) ───────────────
        private Image MakeImage(Transform parent, string name, Sprite sprite, Color color, Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.color = color; img.type = type;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI MakeLabel(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            if (font != null) tmp.font = font;
            tmp.fontSize = size; tmp.color = color; tmp.fontStyle = style;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        private static readonly Color ColText = UIColors.Text;
        private static readonly Color ColMut = UIColors.Hex("#8C9683");
        private static readonly Color ColDim = UIColors.Hex("#5C6653");
        private static readonly Color ColGreen = UIColors.Green;
        private static readonly Color ColGreenBright = UIColors.Hex("#B5E67A");
        private static readonly Color ColGold = UIColors.Hex("#C9A65A");
        private static readonly Color ColGoldLt = UIColors.Hex("#E4C77E");

        private static Color Hex(string hex) => UIColors.Hex(hex);
        private static Color Rgba(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
