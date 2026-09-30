using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlantBreeding.Core;
using PlantBreeding.Collections;
using PlantBreeding.Garden;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Логіка екрану «Дендрарій · Колекції» (docs/ECONOMY.md, «Колекції»).
    /// Assets/Editor/DexScreenBuilder.cs будує статичний каркас (шапка/скрол/
    /// деталь-оверлей/нижнє меню), цей скрипт наповнює список картками.
    ///
    /// Дані — реальні: каталог CollectionCatalog, прогрес і нагороди —
    /// CollectionService (від PlayerData). Кнопка «Забрати» на картці або в
    /// деталі видає нагороду колекції.
    ///
    /// Картки/чіпи будуються в Start() і перебудовуються лише після отримання
    /// нагороди (змінюється стан карток); фільтр лише перемикає SetActive.
    /// Деталь-оверлей — неклікабельний вміст, його безпечно перебудовувати
    /// при кожному відкритті.
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
        private DetailDef _openDetail;
        private int _activeChip;
        private float _tick;
        private float _toastTimer;

        private static PlayerData Data => GameManager.Instance != null ? GameManager.Instance.playerData : null;

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
        //  ПОБУДОВА СПИСКУ
        // ════════════════════════════════════════════════════════════════
        private void BuildList()
        {
            ClearChildren(listContent);
            _cards.Clear();
            _chipBgs.Clear();
            _chipLabels.Clear();

            var data = Data;
            if (data != null) CollectionService.EnsureWeek(data);

            BuildDiscoveryRow(listContent);
            BuildWeekCard(listContent);
            BuildChipsRow(listContent);

            if (data != null)
                foreach (var def in CollectionCatalog.All)
                    _cards.Add(BuildCollectionCard(listContent, data, def));

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
        // (тема тижня доступна лише коли гравець відкрив 4+ видів)
        private void BuildWeekCard(Transform parent)
        {
            var data = Data;
            if (data == null || !CollectionService.HasWeek(data))
            {
                BuildWeekLockedCard(parent);
                return;
            }

            var week = WeekDetail(data);

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
            var timer = MakePill(top.transform, $"⏱ {CollectionService.WeekDaysLeft()} дн", ColGold, Rgba(ColGold, 0.12f), Rgba(ColGold, 0.32f));
            timer.fontSize = 11;

            var desc = MakeLabel(card.transform, "Desc", "Збери врожай кожного з цих видів до кінця тижня.", fontUi, 12.5f, Hex("#A9B2A0"), FontStyles.Normal);
            desc.alignment = TextAlignmentOptions.TopLeft;
            AddFlexLabel(desc);

            BuildSlotRow(card.transform, week.slots, showCheck: true);

            // Прогрес + кнопка
            var progRow = MakeRow(card.transform, 44, 12);
            string left = week.claimed ? "Отримано" : week.claimable ? "Готово" : "Прогрес";
            var col = BuildProgressColumn(progRow.transform, left, week.count, week.pct, ColGold, ColGold);
            col.GetComponent<LayoutElement>().flexibleWidth = 1;
            var btn = MakeActionButton(progRow.transform, week.claimable ? "Забрати" : "Переглянути", week.claimable);
            AddClick(card, () => OpenDetail(WeekDetail(Data)));
            if (week.claimable) btn.onClick.AddListener(() => Claim(null));
            else btn.onClick.AddListener(() => OpenDetail(WeekDetail(Data)));

            var rewardLabel = MakeLabel(card.transform, "Reward", "Нагорода: " + CollectionService.DescribeWeekReward(data), fontUi, 12, Hex("#A9B2A0"), FontStyles.Normal);
            rewardLabel.alignment = TextAlignmentOptions.MidlineLeft;
            AddFlexLabel(rewardLabel);
        }

        // ── Заглушка тижневої колекції, коли відкрито < 4 видів ────────────
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
        private CardHandle BuildCollectionCard(Transform parent, PlayerData data, CollectionDef def)
        {
            var r = GetRar(def.accent);
            int have = CollectionService.Progress(data, def), total = CollectionService.Total(def);
            bool claimable = CollectionService.CanClaim(data, def);
            bool claimed = CollectionService.IsClaimed(data, def);

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

            var border = MakeImage(card.transform, "Border", sprRoundedCardLine, claimable ? r.fill : Rgba(Color.white, 0.10f), Image.Type.Sliced);
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

            var typePill = MakePill(head.transform, GroupLabel(def.group), r.fill, r.bg, r.border);
            typePill.fontSize = 10.5f;

            var desc = MakeLabel(card.transform, "Desc", def.desc, fontUi, 12.5f, ColMut, FontStyles.Normal);
            desc.alignment = TextAlignmentOptions.TopLeft;
            AddFlexLabel(desc);

            BuildSlotRow(card.transform, MiniSlots(data, def), showCheck: false);

            // Прогрес + кнопка
            var progRow = MakeRow(card.transform, 40, 12);
            string left = claimed ? "Отримано" : claimable ? "Готово" : "Зібрано";
            var col = BuildProgressColumn(progRow.transform, left, $"{have}/{total}", Pct(have, total), ColMut, r.fill);
            col.GetComponent<LayoutElement>().flexibleWidth = 1;
            var btn = MakeActionButton(progRow.transform, claimable ? "Забрати" : "Переглянути", claimable);

            // Рядок нагороди
            var rewardRow = new GameObject("Reward", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            rewardRow.transform.SetParent(card.transform, false);
            var rvl = rewardRow.GetComponent<VerticalLayoutGroup>();
            rvl.spacing = 10; rvl.padding = new RectOffset(0, 0, 2, 0);
            rvl.childControlWidth = true; rvl.childControlHeight = true;
            rvl.childForceExpandWidth = true; rvl.childForceExpandHeight = false;
            var divider = MakeImage(rewardRow.transform, "Divider", null, Rgba(Color.white, 0.08f), Image.Type.Simple);
            divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 1;
            string rewardText = (claimed ? "Отримано: " : "Нагорода: ") + CollectionService.DescribeReward(def);
            var rewardLabel = MakeLabel(rewardRow.transform, "Text", rewardText, fontUi, 12, claimed ? ColMut : Hex("#A9B2A0"), FontStyles.Normal);
            rewardLabel.alignment = TextAlignmentOptions.MidlineLeft;
            AddFlexLabel(rewardLabel);

            string id = def.id;
            AddClick(card, () => OpenDetail(CollectionDetail(Data, CollectionCatalog.Get(id))));
            if (claimable) btn.onClick.AddListener(() => Claim(id));
            else btn.onClick.AddListener(() => OpenDetail(CollectionDetail(Data, CollectionCatalog.Get(id))));

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
            _openDetail = d;

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
            bool claimable = d.claimable;
            if (detailClaimBg != null) detailClaimBg.color = claimable ? ColGreenBright : Rgba(Color.white, 0.05f);
            if (detailClaimLabel != null)
            {
                detailClaimLabel.text = d.claimed ? "Нагороду отримано" : claimable ? "Забрати нагороду" : d.claimLabel;
                detailClaimLabel.color = claimable ? Hex("#0E130C") : Hex("#7C8573");
            }
            if (detailClaimButton != null) detailClaimButton.interactable = claimable;

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
            name.enableAutoSizing = true; name.fontSizeMin = 12; name.fontSizeMax = 18; // «Фікус Бенджаміна»

            var sub = MakeLabel(cell.transform, "Sub", slot.sub ?? (done ? "" : "ще не відкрито"), fontUi, 11, done ? ColMut : ColDim, FontStyles.Normal);
            Place(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(150, 16));
            sub.alignment = TextAlignmentOptions.Center;
        }

        private void CloseDetail()
        {
            if (detailPanel != null) detailPanel.SetActive(false);
            _openDetail = null;
        }

        private void HandleClaimClick()
        {
            if (_openDetail != null && _openDetail.claimable) Claim(_openDetail.collectionId);
        }

        /// <summary>
        /// Видає нагороду (collectionId == null — колекція тижня), перебудовує
        /// список і, якщо деталь відкрита, оновлює її.
        /// </summary>
        private void Claim(string collectionId)
        {
            bool ok = collectionId == null ? CollectionService.ClaimWeek() : CollectionService.Claim(collectionId);
            if (!ok) return;
            ShowToast("Нагороду отримано");

            BuildList();
            ApplyChipFilter();
            RefreshHeader();

            if (detailPanel != null && detailPanel.activeSelf && _openDetail != null)
            {
                var data = Data;
                OpenDetail(_openDetail.collectionId == null
                    ? WeekDetail(data)
                    : CollectionDetail(data, CollectionCatalog.Get(_openDetail.collectionId)));
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  ФІЛЬТР / ШАПКА
        // ════════════════════════════════════════════════════════════════
        private void ApplyChipFilter()
        {
            for (int i = 0; i < _chipBgs.Count; i++)
            {
                bool on = i == _activeChip;
                _chipBgs[i].color = on ? ColGreen : Rgba(Color.white, 0.055f);
                _chipLabels[i].color = on ? Hex("#0E130C") : Hex("#C9CFC1");
            }

            foreach (var card in _cards)
            {
                bool show = _activeChip == 0 || (int)card.def.group == _activeChip - 1;
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
                int collected = data != null ? CollectionService.CompletedCount(data) : 0;
                _discoveryLabel.text = $"Відкрито {discovered} з {total} видів · зібрано {collected} з {CollectionCatalog.All.Length} колекцій";
            }
        }

        private static int _cachedTotal = -1;
        private static int TotalSpeciesCount()
        {
            if (_cachedTotal < 0)
                _cachedTotal = PlantCatalog.All.Count;
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
        private static string GroupLabel(CollectionGroup group) =>
            group == CollectionGroup.Theme ? "Тематичні" : "Майстерність";

        private static int Pct(int have, int total) => total <= 0 ? 0 : Mathf.RoundToInt(100f * have / total);

        private static string RarityKey(PlantRarity rarity) => rarity switch
        {
            PlantRarity.Rare => "blue",
            PlantRarity.Epic => "violet",
            PlantRarity.Legendary => "gold",
            _ => "green",
        };

        private static string RarityLabel(PlantRarity rarity) => rarity switch
        {
            PlantRarity.Rare => "рідкісна",
            PlantRarity.Epic => "епічна",
            PlantRarity.Legendary => "легендарна",
            _ => "звичайна",
        };

        /// <summary>Розгорнута колекція з реальним прогресом гравця.</summary>
        private DetailDef CollectionDetail(PlayerData data, CollectionDef def)
        {
            if (data == null || def == null) return null;
            int have = CollectionService.Progress(data, def), total = CollectionService.Total(def);
            return new DetailDef
            {
                collectionId = def.id,
                typeLabel = GroupLabel(def.group),
                name = def.name,
                count = $"{have}/{total}",
                pct = Pct(have, total),
                intro = def.desc + "\n\nНагорода: " + CollectionService.DescribeReward(def),
                hint = def.hint,
                claimLabel = $"Забрати нагороду · ще {total - have}",
                claimable = CollectionService.CanClaim(data, def),
                claimed = CollectionService.IsClaimed(data, def),
                slots = CollectionService.SlotKeys(def).Select(k => MakeSlot(data, def, k)).ToList(),
            };
        }

        /// <summary>Колекція тижня: 4 відомі види, зараховуються врожаї цього тижня.</summary>
        private DetailDef WeekDetail(PlayerData data)
        {
            if (data == null || !CollectionService.HasWeek(data)) return null;
            int total = data.weekPlantIds.Count;
            int have = data.weekPlantIds.Count(id => data.weekHarvestedIds.Contains(id));
            var slots = data.weekPlantIds.Select(id =>
            {
                var p = PlantCatalog.Get(id);
                bool done = data.weekHarvestedIds.Contains(id);
                return new SlotDef(done && p != null ? RarityKey(p.rarity) : "lock", done,
                    p != null ? p.displayName : id, p != null ? RarityLabel(p.rarity) : null,
                    done ? "зібрано цього тижня" : "ще не зібрано");
            }).ToList();

            return new DetailDef
            {
                collectionId = null,
                typeLabel = $"Колекція тижня · {CollectionService.WeekDaysLeft()} дн",
                name = "Колекція тижня",
                count = $"{have}/{total}",
                pct = Pct(have, total),
                intro = "Збери врожай кожного з цих видів до кінця тижня. У понеділок — нові 4 види, прогрес обнуляється."
                        + "\n\nНагорода: " + CollectionService.DescribeWeekReward(data),
                hint = "Види тижня обираються з тих, що ти вже відкривав.",
                claimLabel = $"Забрати нагороду · ще {total - have}",
                claimable = CollectionService.IsWeekClaimable(data),
                claimed = data.weekClaimed,
                slots = slots,
            };
        }

        /// <summary>Міні-слоти на картці: до 5 штук; для великих колекцій — пропорційно прогресу.</summary>
        private static List<SlotDef> MiniSlots(PlayerData data, CollectionDef def)
        {
            const int maxMini = 5;
            var keys = CollectionService.SlotKeys(def);
            var list = new List<SlotDef>();
            if (keys.Count <= maxMini)
            {
                foreach (var k in keys)
                {
                    var slot = MakeSlot(data, def, k);
                    list.Add(new SlotDef(slot.rarity, slot.done));
                }
                return list;
            }

            int have = CollectionService.Progress(data, def);
            int filled = have == 0 ? 0 : Mathf.Max(1, have * maxMini / keys.Count);
            for (int i = 0; i < maxMini; i++)
                list.Add(i < filled ? new SlotDef(def.accent, true) : new SlotDef("lock", false));
            return list;
        }

        private static SlotDef MakeSlot(PlayerData data, CollectionDef def, string key)
        {
            bool done = CollectionService.IsSlotDone(data, def, key);
            switch (def.goal)
            {
                case CollectionGoal.DiscoverPlant:
                case CollectionGoal.PerfectCare:
                case CollectionGoal.HarvestPlant:
                {
                    var p = PlantCatalog.Get(key);
                    if (p == null) return new SlotDef("lock", false, key);
                    int count = CollectionService.HarvestCount(data, key);
                    string sub = def.goal switch
                    {
                        CollectionGoal.HarvestPlant =>
                            $"зібрано {Mathf.Min(count, CollectionCatalog.VeteranHarvests)}/{CollectionCatalog.VeteranHarvests}",
                        CollectionGoal.PerfectCare => done ? "без пропусків" : "ще не вдалось",
                        _ => done ? $"зібрано {count}×" : $"з рівня {p.unlockLevel}",
                    };
                    return new SlotDef(done ? RarityKey(p.rarity) : "lock", done, p.displayName, RarityLabel(p.rarity), sub);
                }
                case CollectionGoal.CureAilment:
                {
                    var a = PlantAilments.Get((AilmentKind)int.Parse(key));
                    return new SlotDef(done ? def.accent : "lock", done, a.name, a.isPest ? "шкідник" : "хвороба",
                        done ? "вилікувано" : "ще не траплялось");
                }
                case CollectionGoal.OwnPot:
                {
                    var pot = Resources.Load<PotData>("Pots/" + key);
                    return new SlotDef(done ? def.accent : "lock", done, pot != null ? pot.displayName : key, null,
                        done ? "куплено" : pot != null ? $"{pot.unlockCost} монет" : null);
                }
                case CollectionGoal.CompleteCollection:
                {
                    var other = CollectionCatalog.Get(key);
                    if (other == null) return new SlotDef("lock", false, key);
                    return new SlotDef(done ? other.accent : "lock", done, other.name, null,
                        $"{CollectionService.Progress(data, other)}/{CollectionService.Total(other)}");
                }
            }
            return new SlotDef("lock", done, key);
        }

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
