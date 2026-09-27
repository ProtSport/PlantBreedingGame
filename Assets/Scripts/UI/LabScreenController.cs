using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlantBreeding.Core;
using PlantBreeding.Lab;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Логіка екрану "Лабораторія · Дослідження" (Assets/Editor/LabScreenBuilder.cs
    /// будує статичний каркас, цей скрипт наповнює його картками гілок і
    /// оновлює таймери). Один активний слот дослідження на весь акаунт
    /// (Варіант А) — картки/вузли будуються ОДИН РАЗ у Start() і більше
    /// ніколи не знищуються, як і в PlantingScreenController: тап лише
    /// змінює властивості вже існуючих об'єктів через RefreshVisuals().
    /// Прогрес рахується від реального часу (Unix-секунди в PlayerData.labActive),
    /// тому не втрачається, якщо гравець закриє екран/гру.
    /// </summary>
    public class LabScreenController : MonoBehaviour
    {
        private const string LabSceneName = "Lab";

        [Header("Шапка")]
        public TMP_Text coinsLabel;
        public TMP_Text gemsLabel;
        public Button navGardenButton;

        [Header("Контейнер гілок (наповнюється в рантаймі)")]
        public Transform branchesContent;

        [Header("Тост-повідомлення")]
        public GameObject toastRoot;
        public TMP_Text toastLabel;

        [Header("Спільна графіка (з UIBuilderKit)")]
        public Sprite sprRoundedCard;
        public Sprite sprRoundedCardLine;
        public Sprite sprRoundedSmall;
        public Sprite sprRoundedSmallLine;
        public Sprite sprPillTiny;
        public Sprite sprCircleFill;
        public Sprite sprCircleLine;
        public Sprite sprGlow;
        public Sprite sprIconBolt;
        public Sprite sprIconHeart;
        public Sprite sprIconCoin;
        public Sprite sprIconCheck;
        public TMP_FontAsset fontHead;
        public TMP_FontAsset fontUi;

        private class NodeHandle
        {
            public Image circleBg;
            public Image circleBorder;
            public Image glowRing;
            public Image activeDot;
            public GameObject checkGroup;
            public GameObject lockGroup;
            public TMP_Text numberLabel;
            public TMP_Text effectLabel;
            public Image connectorLeft;
            public Image connectorRight;
        }

        private class BranchCardHandle
        {
            public LabBranchDef def;
            public AccentColors accent;
            public NodeHandle[] nodes = new NodeHandle[LabResearchService.NodesPerBranch];
            public TMP_Text counterLabel;

            public GameObject footerAvailable;
            public TMP_Text footerAvailableHint;
            public TMP_Text footerAvailableCost;
            public Button footerAvailableButton;

            public GameObject footerActive;
            public TMP_Text footerActiveStatus;
            public TMP_Text footerActivePercent;
            public Image footerActiveFill;
            public TMP_Text footerActiveRushCost;
            public Button footerActiveRushButton;
            public TMP_Text footerActiveRushLabel;

            public GameObject footerMaxed;
        }

        private struct AccentColors
        {
            public Color fill;
            public Color done;
            public Color check;
            public Color counter;
        }

        private readonly List<BranchCardHandle> _cards = new List<BranchCardHandle>();
        private float _tickAccumulator;
        private float _toastTimer;
        private float _pulseClock;

        private void Start()
        {
            if (navGardenButton != null) navGardenButton.onClick.AddListener(CloseToGarden);
            BuildCards();
            RefreshVisuals();
            if (toastRoot != null) toastRoot.SetActive(false);
        }

        private void Update()
        {
            _pulseClock += Time.deltaTime;
            AnimateActivePulse();

            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f && toastRoot != null) toastRoot.SetActive(false);
            }

            _tickAccumulator += Time.deltaTime;
            if (_tickAccumulator < 1f) return;
            _tickAccumulator = 0f;

            var data = GameManager.Instance != null ? GameManager.Instance.playerData : null;
            if (data == null) return;
            LabResearchService.TickCompleteIfDone(data);
            RefreshVisuals();
        }

        // ════════════════════════════════════════════════════════════════
        //  ПОБУДОВА (один раз, картки більше НІКОЛИ не знищуються)
        // ════════════════════════════════════════════════════════════════
        private void BuildCards()
        {
            ClearChildren(branchesContent);
            _cards.Clear();
            foreach (var def in LabResearchCatalog.Branches)
            {
                _cards.Add(BuildBranchCard(def));
            }
        }

        private BranchCardHandle BuildBranchCard(LabBranchDef def)
        {
            var handle = new BranchCardHandle { def = def, accent = GetAccent(def.accent) };

            var card = new GameObject("Branch_" + def.id, typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            card.transform.SetParent(branchesContent, false);
            var cardImg = card.GetComponent<Image>();
            cardImg.sprite = sprRoundedCard;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = Rgba(Color.white, 0.05f);
            card.GetComponent<LayoutElement>().flexibleWidth = 1;

            var border = MakeImage(card.transform, "Border", sprRoundedCardLine, Rgba(Color.white, 0.10f), Image.Type.Sliced);
            Stretch(border.rectTransform);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var vl = card.GetComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(16, 16, 16, 15);
            vl.spacing = 15;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            card.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildHeader(card.transform, handle);
            BuildNodesRow(card.transform, handle);
            var divider = MakeImage(card.transform, "Divider", null, Rgba(Color.white, 0.07f), Image.Type.Simple);
            divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 1;
            BuildFooterAvailable(card.transform, handle);
            BuildFooterActive(card.transform, handle);
            BuildFooterMaxed(card.transform, handle);

            // Безпечний дефолт, поки RefreshVisuals ще не відпрацював хоч раз
            // (напр. GameManager.Instance ще не готовий у перший кадр) —
            // без цього всі три варіанти футера рендерились би одночасно.
            handle.footerActive.SetActive(false);
            handle.footerMaxed.SetActive(false);

            return handle;
        }

        private void BuildHeader(Transform parent, BranchCardHandle handle)
        {
            var header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            header.transform.SetParent(parent, false);
            header.GetComponent<LayoutElement>().preferredHeight = 38;
            var hl = header.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 11; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;

            var iconBox = new GameObject("IconBox", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconBox.transform.SetParent(header.transform, false);
            var iconBoxLe = iconBox.GetComponent<LayoutElement>();
            iconBoxLe.preferredWidth = 38; iconBoxLe.preferredHeight = 38;
            var iconBoxImg = iconBox.GetComponent<Image>();
            iconBoxImg.sprite = sprRoundedSmall; iconBoxImg.type = Image.Type.Sliced;
            iconBoxImg.color = Rgba(handle.accent.fill, 0.16f);
            var iconBoxBorder = MakeImage(iconBox.transform, "Border", sprRoundedSmallLine, Rgba(handle.accent.fill, 0.32f), Image.Type.Sliced);
            Stretch(iconBoxBorder.rectTransform);
            iconBoxBorder.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var icon = MakeImage(iconBox.transform, "Icon", BranchIcon(handle.def), BranchIconTint(handle), Image.Type.Simple);
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));

            var titleCol = new GameObject("TitleCol", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            titleCol.transform.SetParent(header.transform, false);
            titleCol.GetComponent<LayoutElement>().flexibleWidth = 1;
            var tvl = titleCol.GetComponent<VerticalLayoutGroup>();
            tvl.childControlWidth = true; tvl.childControlHeight = false;
            tvl.childForceExpandWidth = true; tvl.childForceExpandHeight = false;
            tvl.childAlignment = TextAnchor.MiddleLeft;

            var name = MakeLabel(titleCol.transform, "Name", handle.def.displayName, fontHead, 20, ColText, FontStyles.Normal);
            name.rectTransform.sizeDelta = new Vector2(0, 24);
            name.alignment = TextAlignmentOptions.MidlineLeft;

            var sub = MakeLabel(titleCol.transform, "Sub", handle.def.subLabel, fontUi, 11, ColMut, FontStyles.Normal);
            sub.rectTransform.sizeDelta = new Vector2(0, 15);
            sub.alignment = TextAlignmentOptions.MidlineLeft;

            var counter = MakeLabel(header.transform, "Counter", "0 / 4", fontUi, 12, handle.accent.counter, FontStyles.Bold);
            counter.gameObject.AddComponent<LayoutElement>().preferredWidth = 40;
            counter.alignment = TextAlignmentOptions.MidlineRight;
            handle.counterLabel = counter;
        }

        private void BuildNodesRow(Transform parent, BranchCardHandle handle)
        {
            var row = new GameObject("NodesRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 70;
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.UpperCenter;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;

            for (int i = 0; i < LabResearchService.NodesPerBranch; i++)
                handle.nodes[i] = BuildNodeCell(row.transform, handle, i);
        }

        private NodeHandle BuildNodeCell(Transform parent, BranchCardHandle handle, int index)
        {
            var cell = new GameObject("Node_" + index, typeof(RectTransform));
            cell.transform.SetParent(parent, false);

            var node = new NodeHandle();

            if (index > 0)
            {
                var connLeft = MakeImage(cell.transform, "ConnLeft", null, Rgba(Color.white, 0.08f), Image.Type.Simple);
                var lrt = connLeft.rectTransform;
                lrt.anchorMin = new Vector2(0, 1); lrt.anchorMax = new Vector2(0.5f, 1);
                lrt.pivot = new Vector2(0.5f, 1);
                lrt.offsetMin = new Vector2(0, -19); lrt.offsetMax = new Vector2(-17, -16);
                node.connectorLeft = connLeft;
            }
            if (index < LabResearchService.NodesPerBranch - 1)
            {
                var connRight = MakeImage(cell.transform, "ConnRight", null, Rgba(Color.white, 0.08f), Image.Type.Simple);
                var rrt = connRight.rectTransform;
                rrt.anchorMin = new Vector2(0.5f, 1); rrt.anchorMax = new Vector2(1, 1);
                rrt.pivot = new Vector2(0.5f, 1);
                rrt.offsetMin = new Vector2(17, -19); rrt.offsetMax = new Vector2(0, -16);
                node.connectorRight = connRight;
            }

            var glow = MakeImage(cell.transform, "Glow", sprGlow, Rgba(ColGreenBright, 0.4f), Image.Type.Simple);
            Place(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -2 - 17), new Vector2(56, 56));
            glow.gameObject.SetActive(false);
            node.glowRing = glow;

            var circle = new GameObject("Circle", typeof(RectTransform));
            circle.transform.SetParent(cell.transform, false);
            var crt = (RectTransform)circle.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0, -2);
            crt.sizeDelta = new Vector2(34, 34);

            var bg = MakeImage(circle.transform, "Bg", sprCircleFill, Rgba(Color.white, 0.04f), Image.Type.Simple);
            Stretch(bg.rectTransform);
            node.circleBg = bg;
            var border = MakeImage(circle.transform, "Border", sprCircleLine, Rgba(Color.white, 0.12f), Image.Type.Simple);
            Stretch(border.rectTransform);
            node.circleBorder = border;

            var check = MakeImage(circle.transform, "Check", sprIconCheck, handle.accent.check, Image.Type.Simple);
            Place(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
            check.gameObject.SetActive(false);
            node.checkGroup = check.gameObject;

            var lockGroup = new GameObject("Lock", typeof(RectTransform));
            lockGroup.transform.SetParent(circle.transform, false);
            Stretch((RectTransform)lockGroup.transform);
            var lockBody = MakeImage(lockGroup.transform, "Body", sprRoundedSmall, Rgba(Color.white, 0.28f), Image.Type.Sliced);
            Place(lockBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -2), new Vector2(13, 10));
            var lockShackle = MakeImage(lockGroup.transform, "Shackle", sprCircleLine, Rgba(Color.white, 0.34f), Image.Type.Simple);
            Place(lockShackle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(11, 11));
            lockGroup.SetActive(false);
            node.lockGroup = lockGroup;

            var number = MakeLabel(circle.transform, "Number", (index + 1).ToString(), fontUi, 12, handle.accent.fill, FontStyles.Bold);
            Stretch(number.rectTransform);
            number.alignment = TextAlignmentOptions.Center;
            number.gameObject.SetActive(false);
            node.numberLabel = number;

            var dot = MakeImage(circle.transform, "Dot", sprCircleFill, ColGreenBright, Image.Type.Simple);
            Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9, 9));
            dot.gameObject.SetActive(false);
            node.activeDot = dot;

            var def = handle.def.nodes[index];
            var label = MakeLabel(cell.transform, "Label", def.effectLine1 + "\n" + def.effectLine2, fontUi, 10.5f, ColSoft, FontStyles.Normal);
            var lrt2 = label.rectTransform;
            lrt2.anchorMin = lrt2.anchorMax = new Vector2(0.5f, 1f);
            lrt2.pivot = new Vector2(0.5f, 1f);
            lrt2.anchoredPosition = new Vector2(0, -42);
            lrt2.sizeDelta = new Vector2(76, 28);
            label.alignment = TextAlignmentOptions.Top;
            node.effectLabel = label;

            return node;
        }

        private void BuildFooterAvailable(Transform parent, BranchCardHandle handle)
        {
            var footer = new GameObject("FooterAvailable", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            footer.transform.SetParent(parent, false);
            footer.GetComponent<LayoutElement>().preferredHeight = 40;
            var hl = footer.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 9; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;

            var hint = MakeLabel(footer.transform, "Hint", "", fontUi, 11.5f, ColMut, FontStyles.Normal);
            hint.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            hint.alignment = TextAlignmentOptions.MidlineLeft;
            handle.footerAvailableHint = hint;

            var (_, pillLabel) = MakePill(footer.transform, sprIconCoin);
            handle.footerAvailableCost = pillLabel;

            var btnGo = new GameObject("ResearchButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGo.transform.SetParent(footer.transform, false);
            var btnLe = btnGo.GetComponent<LayoutElement>();
            btnLe.preferredWidth = 106; btnLe.preferredHeight = 38;
            var btnImg = btnGo.GetComponent<Image>();
            btnImg.sprite = sprRoundedSmall; btnImg.type = Image.Type.Sliced;
            btnImg.color = ColGreenBright;
            btnImg.raycastTarget = true;
            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = btnImg;
            var btnLabel = MakeLabel(btnGo.transform, "Label", "Дослідити", fontUi, 13, Hex("#0E130C"), FontStyles.Bold);
            Stretch(btnLabel.rectTransform);
            btnLabel.alignment = TextAlignmentOptions.Center;

            var branch = handle.def.id;
            btn.onClick.AddListener(() => HandleResearchClick(branch));

            handle.footerAvailable = footer;
            handle.footerAvailableButton = btn;
        }

        private void BuildFooterActive(Transform parent, BranchCardHandle handle)
        {
            var footer = new GameObject("FooterActive", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            footer.transform.SetParent(parent, false);
            var vl = footer.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 8; vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

            var statusRow = new GameObject("StatusRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            statusRow.transform.SetParent(footer.transform, false);
            statusRow.GetComponent<LayoutElement>().preferredHeight = 18;
            var shl = statusRow.GetComponent<HorizontalLayoutGroup>();
            shl.childControlWidth = true; shl.childControlHeight = true;
            shl.childForceExpandWidth = false; shl.childForceExpandHeight = true;

            var status = MakeLabel(statusRow.transform, "Status", "", fontUi, 11.5f, ColMut, FontStyles.Normal);
            status.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            status.alignment = TextAlignmentOptions.MidlineLeft;
            handle.footerActiveStatus = status;

            var percent = MakeLabel(statusRow.transform, "Percent", "0%", fontHead, 16, ColGreen, FontStyles.Bold);
            percent.gameObject.AddComponent<LayoutElement>().preferredWidth = 50;
            percent.alignment = TextAlignmentOptions.MidlineRight;
            handle.footerActivePercent = percent;

            var track = MakeImage(footer.transform, "Track", sprPillTiny, Rgba(Color.white, 0.10f), Image.Type.Sliced);
            track.gameObject.AddComponent<LayoutElement>().preferredHeight = 7;
            var fill = MakeImage(track.transform, "Fill", sprPillTiny, ColGreen, Image.Type.Sliced);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0, 1);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            handle.footerActiveFill = fill;

            var rushRow = new GameObject("RushRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rushRow.transform.SetParent(footer.transform, false);
            rushRow.GetComponent<LayoutElement>().preferredHeight = 38;
            var rhl = rushRow.GetComponent<HorizontalLayoutGroup>();
            rhl.spacing = 9; rhl.childAlignment = TextAnchor.MiddleLeft;
            rhl.childControlWidth = true; rhl.childControlHeight = true;
            rhl.childForceExpandWidth = false; rhl.childForceExpandHeight = true;

            var (_, pillLabel) = MakePill(rushRow.transform, sprIconCoin);
            handle.footerActiveRushCost = pillLabel;

            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(rushRow.transform, false);
            spacer.GetComponent<LayoutElement>().flexibleWidth = 1;

            var rushGo = new GameObject("RushButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            rushGo.transform.SetParent(rushRow.transform, false);
            var rushLe = rushGo.GetComponent<LayoutElement>();
            rushLe.preferredWidth = 140; rushLe.preferredHeight = 38;
            var rushImg = rushGo.GetComponent<Image>();
            rushImg.sprite = sprRoundedSmall; rushImg.type = Image.Type.Sliced;
            rushImg.color = Hex("#C9A65A");
            rushImg.raycastTarget = true;
            var rushHl = rushGo.GetComponent<HorizontalLayoutGroup>();
            rushHl.spacing = 5; rushHl.padding = new RectOffset(12, 10, 0, 0);
            rushHl.childAlignment = TextAnchor.MiddleCenter;
            rushHl.childControlWidth = true; rushHl.childControlHeight = true;
            rushHl.childForceExpandWidth = false; rushHl.childForceExpandHeight = false;

            var rushIcon = MakeImage(rushGo.transform, "Icon", sprIconBolt, Hex("#241C0C"), Image.Type.Simple);
            rushIcon.preserveAspect = true;
            rushIcon.gameObject.AddComponent<LayoutElement>().preferredWidth = 14;
            rushIcon.GetComponent<LayoutElement>().preferredHeight = 14;

            var rushLabel = MakeLabel(rushGo.transform, "Label", "Прискорити · 0", fontUi, 12.5f, Hex("#241C0C"), FontStyles.Bold);
            rushLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            rushLabel.alignment = TextAlignmentOptions.MidlineLeft;
            handle.footerActiveRushLabel = rushLabel;

            var btn = rushGo.GetComponent<Button>();
            btn.targetGraphic = rushImg;
            btn.onClick.AddListener(HandleRushClick);

            handle.footerActive = footer;
            handle.footerActiveRushButton = btn;
        }

        private void BuildFooterMaxed(Transform parent, BranchCardHandle handle)
        {
            var footer = new GameObject("FooterMaxed", typeof(RectTransform), typeof(LayoutElement));
            footer.transform.SetParent(parent, false);
            footer.GetComponent<LayoutElement>().preferredHeight = 24;
            var label = MakeLabel(footer.transform, "Label", "Усі рівні гілки завершено", fontUi, 11.5f, ColMut, FontStyles.Normal);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            handle.footerMaxed = footer;
        }

        // ════════════════════════════════════════════════════════════════
        //  КЛІКИ
        // ════════════════════════════════════════════════════════════════
        private void HandleResearchClick(LabBranchId branch)
        {
            var result = LabResearchService.TryStartResearch(branch);
            switch (result)
            {
                case LabStartResult.Ok:
                    break;
                case LabStartResult.SlotBusy:
                    ShowToast("Спочатку заверши поточне дослідження");
                    break;
                case LabStartResult.NotEnoughCoins:
                    ShowToast("Недостатньо монет");
                    break;
                case LabStartResult.NotEnoughGems:
                    ShowToast("Недостатньо кристалів");
                    break;
                case LabStartResult.AlreadyMaxed:
                    break;
            }
            RefreshVisuals();
        }

        private void HandleRushClick()
        {
            if (!LabResearchService.TryRushActive())
            {
                ShowToast("Недостатньо кристалів");
            }
            RefreshVisuals();
        }

        private void CloseToGarden()
        {
            SceneManager.UnloadSceneAsync(LabSceneName);
        }

        private void ShowToast(string message)
        {
            if (toastLabel != null) toastLabel.text = message;
            if (toastRoot != null) toastRoot.SetActive(true);
            _toastTimer = 1.8f;
        }

        // ════════════════════════════════════════════════════════════════
        //  ВІДМАЛЬОВКА
        // ════════════════════════════════════════════════════════════════
        private void RefreshVisuals()
        {
            var gm = GameManager.Instance;
            var data = gm != null ? gm.playerData : null;

            if (coinsLabel != null) coinsLabel.text = data != null ? data.coins.ToString("N0") : "0";
            if (gemsLabel != null) gemsLabel.text = data != null ? data.gems.ToString("N0") : "0";

            if (data == null) return;

            foreach (var card in _cards)
            {
                RefreshCard(data, card);
            }
        }

        private void RefreshCard(PlayerData data, BranchCardHandle card)
        {
            int completed = LabResearchService.GetCompletedLevels(data, card.def.id);
            card.counterLabel.text = $"{completed} / {LabResearchService.NodesPerBranch}";

            bool reachedActive = LabResearchService.IsActiveOn(data, card.def.id);
            int activeIndex = reachedActive ? data.labActive.nodeIndex : -1;

            for (int i = 0; i < card.nodes.Length; i++)
            {
                RefreshNode(data, card, i, completed, activeIndex);
            }

            bool showActive = reachedActive;
            bool showMaxed = !showActive && completed >= LabResearchService.NodesPerBranch;
            bool showAvailable = !showActive && !showMaxed;

            card.footerActive.SetActive(showActive);
            card.footerAvailable.SetActive(showAvailable);
            card.footerMaxed.SetActive(showMaxed);

            if (showAvailable)
            {
                var def = LabResearchCatalog.GetNode(card.def.id, completed);
                card.footerAvailableHint.text = $"Рівень {completed + 1} доступний";
                card.footerAvailableCost.text = def.coinCost.ToString("N0");
            }
            else if (showActive)
            {
                var def = LabResearchCatalog.GetNode(card.def.id, activeIndex);
                double remaining = LabResearchService.GetActiveRemainingSeconds(data);
                float progress01 = LabResearchService.GetActiveProgress01(data);
                card.footerActiveStatus.text = $"Рівень {activeIndex + 1} · залишилось {FormatRemaining(remaining)}";
                card.footerActivePercent.text = $"{Mathf.RoundToInt(progress01 * 100f)}%";
                card.footerActiveFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);
                card.footerActiveRushCost.text = def.coinCost.ToString("N0");
                int rushCost = LabResearchService.GetRushGemCost(data);
                card.footerActiveRushLabel.text = $"Прискорити · {rushCost}";
            }
        }

        private void RefreshNode(PlayerData data, BranchCardHandle card, int index, int completed, int activeIndex)
        {
            var node = card.nodes[index];
            var state = LabResearchService.GetNodeState(data, card.def.id, index);

            node.checkGroup.SetActive(state == LabNodeState.Done);
            node.lockGroup.SetActive(state == LabNodeState.Locked);
            node.numberLabel.gameObject.SetActive(state == LabNodeState.Available);
            node.activeDot.gameObject.SetActive(state == LabNodeState.Active);
            node.glowRing.gameObject.SetActive(state == LabNodeState.Active);

            switch (state)
            {
                case LabNodeState.Done:
                    node.circleBg.color = Rgba(card.accent.fill, 0.25f);
                    node.circleBorder.color = card.accent.fill;
                    node.effectLabel.color = ColSoft;
                    break;
                case LabNodeState.Active:
                    node.circleBg.color = Rgba(card.accent.fill, 0.18f);
                    node.circleBorder.color = ColGreenBright;
                    node.effectLabel.color = Hex("#CDEBA0");
                    break;
                case LabNodeState.Available:
                    node.circleBg.color = Rgba(card.accent.fill, 0.08f);
                    node.circleBorder.color = Rgba(card.accent.fill, 0.5f);
                    node.effectLabel.color = Hex("#B4BEA8");
                    break;
                default: // Locked
                    node.circleBg.color = Rgba(Color.white, 0.04f);
                    node.circleBorder.color = Rgba(Color.white, 0.12f);
                    node.effectLabel.color = ColLock;
                    break;
            }

            bool reached = index <= completed || index == activeIndex;
            if (node.connectorLeft != null)
                node.connectorLeft.color = reached ? card.accent.done : Rgba(Color.white, 0.08f);
            if (node.connectorRight != null)
                node.connectorRight.color = index < completed ? card.accent.done : Rgba(Color.white, 0.08f);
        }

        private void AnimateActivePulse()
        {
            float t = 0.5f + 0.5f * Mathf.Sin(_pulseClock * (2f * Mathf.PI / 1.6f));
            foreach (var card in _cards)
            {
                foreach (var node in card.nodes)
                {
                    if (node.activeDot.gameObject.activeSelf)
                    {
                        node.activeDot.color = Rgba(ColGreenBright, t);
                    }
                    if (node.glowRing.gameObject.activeSelf)
                    {
                        node.glowRing.color = Rgba(ColGreenBright, Mathf.Lerp(0.18f, 0.42f, t));
                    }
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  ХЕЛПЕРИ
        // ════════════════════════════════════════════════════════════════
        private Sprite BranchIcon(LabBranchDef def) => def.id switch
        {
            LabBranchId.Speed => sprIconBolt,
            LabBranchId.Health => sprIconHeart,
            _ => sprIconCoin,
        };

        private Color BranchIconTint(BranchCardHandle handle) =>
            handle.def.id == LabBranchId.Yield ? Color.white : handle.accent.fill;

        private static AccentColors GetAccent(string accent) => accent switch
        {
            "blue" => new AccentColors { fill = Hex("#8FD0E6"), done = Hex("#5E8AA0"), check = Hex("#D8F0F8"), counter = Hex("#8FD0E6") },
            "gold" => new AccentColors { fill = Hex("#E4C77E"), done = Hex("#C9A65A"), check = Hex("#F6E7BE"), counter = ColMut },
            _ => new AccentColors { fill = Hex("#A7CE73"), done = Hex("#7FA85C"), check = Hex("#CDEBA0"), counter = Hex("#A7CE73") },
        };

        private static string FormatRemaining(double totalSeconds)
        {
            int total = Mathf.CeilToInt((float)totalSeconds);
            int h = total / 3600;
            int m = (total % 3600) / 60;
            if (h > 0) return m > 0 ? $"{h} год {m} хв" : $"{h} год";
            if (m > 0) return $"{m} хв";
            return "<1 хв";
        }

        private (Image bg, TMP_Text label) MakePill(Transform parent, Sprite icon)
        {
            var pill = new GameObject("Pill", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            pill.transform.SetParent(parent, false);
            var img = pill.GetComponent<Image>();
            img.sprite = sprRoundedSmall; img.type = Image.Type.Sliced;
            img.color = Rgba(Color.white, 0.055f);
            var hl = pill.GetComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(7, 12, 6, 6);
            hl.spacing = 6; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            pill.GetComponent<LayoutElement>().preferredHeight = 30;

            var ic = MakeImage(pill.transform, "Icon", icon, Color.white, Image.Type.Simple);
            ic.preserveAspect = true;
            ic.gameObject.AddComponent<LayoutElement>().preferredWidth = 13;
            ic.GetComponent<LayoutElement>().preferredHeight = 13;

            var label = MakeLabel(pill.transform, "Value", "0", fontUi, 12, Hex("#EBE4D6"), FontStyles.Bold);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            return (img, label);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.GetChild(i).gameObject);
        }

        private Image MakeImage(Transform parent, string name, Sprite sprite, Color color, Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
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
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static readonly Color ColText = UIColors.Text;
        private static readonly Color ColSoft = UIColors.Soft;
        private static readonly Color ColGreen = UIColors.Green;
        private static readonly Color ColMut = Hex("#8C9683");
        private static readonly Color ColLock = Hex("#6E7A63");
        private static readonly Color ColGreenBright = Hex("#B5E67A");

        private static Color Hex(string hex) => UIColors.Hex(hex);
        private static Color Rgba(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
