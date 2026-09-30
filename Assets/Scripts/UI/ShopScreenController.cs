using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlantBreeding.Core;
using PlantBreeding.Shop;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Екран «Крамниця» за макетом Крамниця/export-shop/kramnytsia.html.
    /// Assets/Editor/ShopScreenBuilder.cs будує каркас (фон, шапка, заголовок,
    /// смуга вкладок, скрол, нижнє меню, тост), цей скрипт у рантаймі наповнює
    /// розділи (Набори, Кристали, Монети, Горщики, Насіння, Грядки, Клуб) і
    /// шторку підтвердження покупки.
    ///
    /// Каталог — ShopCatalog, що можна купити — ShopService, оплата — StoreGateway
    /// (In-App Purchase). Список перебудовується наступного кадру після покупки
    /// чи оновлення цін (не в обробнику кліку — InputSystem hover-баг, див.
    /// SceneNavButton.UnloadNextFrame).
    /// </summary>
    public class ShopScreenController : MonoBehaviour
    {
        [Header("Каркас зі сцени")]
        public ScrollRect scrollRect;
        public Transform listContent;
        public Transform tabsContent;

        [Header("Тост")]
        public GameObject toastRoot;
        public TMP_Text toastLabel;

        [Header("Графіка (з UIBuilderKit)")]
        public Sprite sprCard;        // rounded-20
        public Sprite sprCardLine;    // rounded-20-line
        public Sprite sprSmall;       // rounded-16
        public Sprite sprSmallLine;   // rounded-16-line
        public Sprite sprSheet;       // rounded-24
        public Sprite sprPill;        // pill-tiny
        public Sprite sprCircle;      // circle-fill
        public Sprite sprCircleLine;  // circle-line
        public Sprite sprCheck;       // icon-check
        public Sprite sprGlow;
        public Sprite sprGem;
        public Sprite sprCoin;
        public Sprite sprLock;
        public Sprite sprPlus;
        public Sprite sprPotCrystal;
        public Sprite sprPotJade;
        public Sprite sprPotGold;
        public Sprite sprSeedPack;
        public TMP_FontAsset fontHead;
        public TMP_FontAsset fontUi;

        private static readonly (ShopSection section, string label)[] Tabs =
        {
            (ShopSection.Sets, "Набори"), (ShopSection.Gems, "Кристали"), (ShopSection.Coins, "Монети"),
            (ShopSection.Pots, "Горщики"), (ShopSection.Seeds, "Насіння"), (ShopSection.Plots, "Грядки"),
            (ShopSection.Club, "Клуб"),
        };

        private readonly Dictionary<ShopSection, RectTransform> _sections = new Dictionary<ShopSection, RectTransform>();
        private readonly List<(ShopSection section, Image bg, TMP_Text label)> _tabs = new List<(ShopSection, Image, TMP_Text)>();
        private ShopSection _activeTab = ShopSection.Sets;
        private TMP_Text _starterTimer;
        private bool _dirty;
        private float _tick;
        private float _toastTimer;

        // Шторка покупки
        private GameObject _sheetRoot;
        private TMP_Text _sheetKind, _sheetTitle, _sheetDesc;
        private GameObject _sheetDoneIcon;
        private Button _sheetConfirm, _sheetCancel;
        private Image _sheetConfirmBg;
        private TMP_Text _sheetConfirmLabel, _sheetCancelLabel;
        private ShopProduct _sheetProduct;
        private enum SheetState { Confirm, Processing, Done, Error }
        private SheetState _sheetState;

        private static PlayerData Data => GameManager.Instance != null ? GameManager.Instance.playerData : null;

        // ════════════════════════════════════════════════════════════════
        private void OnEnable()
        {
            ShopService.Changed += MarkDirty;
            StoreGateway.StoreUpdated += MarkDirty;
        }

        private void OnDisable()
        {
            ShopService.Changed -= MarkDirty;
            StoreGateway.StoreUpdated -= MarkDirty;
        }

        private void Start()
        {
            StoreGateway.Init();
            BuildTabs();
            BuildList();
            BuildSheet();
            if (toastRoot != null) { toastRoot.SetActive(false); toastRoot.transform.SetAsLastSibling(); }
            if (scrollRect != null) scrollRect.onValueChanged.AddListener(_ => SyncTabWithScroll());
            RefreshTabs();
        }

        private void MarkDirty() => _dirty = true;

        private void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f && toastRoot != null) toastRoot.SetActive(false);
            }

            if (_dirty)
            {
                _dirty = false;
                BuildList();
            }

            _tick += Time.deltaTime;
            if (_tick < 1f) return;
            _tick = 0f;
            UpdateStarterTimer();
        }

        // ════════════════════════════════════════════════════════════════
        //  ВКЛАДКИ
        // ════════════════════════════════════════════════════════════════
        private void BuildTabs()
        {
            if (tabsContent == null) return;
            foreach (var (section, text) in Tabs)
            {
                var chip = new GameObject("Tab_" + section, typeof(RectTransform), typeof(Image), typeof(Button),
                    typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                chip.transform.SetParent(tabsContent, false);
                var img = chip.GetComponent<Image>();
                img.sprite = sprPill; img.type = Image.Type.Sliced;
                var hl = chip.GetComponent<HorizontalLayoutGroup>();
                hl.padding = new RectOffset(14, 14, 0, 0); hl.childAlignment = TextAnchor.MiddleCenter;
                hl.childControlWidth = true; hl.childControlHeight = true;
                hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
                chip.GetComponent<LayoutElement>().preferredHeight = 34;

                var label = Label(chip.transform, text, fontUi, 13, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                label.textWrappingMode = TextWrappingModes.NoWrap;

                var btn = chip.GetComponent<Button>();
                btn.targetGraphic = img;
                var s = section;
                btn.onClick.AddListener(() => ScrollTo(s));
                _tabs.Add((section, img, label));
            }
        }

        private void RefreshTabs()
        {
            foreach (var (section, bg, label) in _tabs)
            {
                bool on = section == _activeTab;
                bg.color = on ? ColGreen : Rgba(Color.white, 0.05f);
                label.color = on ? Hex("#0E130C") : Hex("#C9CFC1");
                bg.gameObject.SetActive(_sections.ContainsKey(section));
            }
        }

        /// <summary>Тап на вкладку — прокрутити список до розділу.</summary>
        private void ScrollTo(ShopSection section)
        {
            if (scrollRect == null || !_sections.TryGetValue(section, out var target)) return;
            Canvas.ForceUpdateCanvases();
            var content = scrollRect.content;
            float maxY = Mathf.Max(0f, content.rect.height - scrollRect.viewport.rect.height);
            float y = Mathf.Clamp(SectionTop(target) - 4f, 0f, maxY);
            scrollRect.StopMovement();
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
            _activeTab = section;
            RefreshTabs();
        }

        /// <summary>Прокрутка списку — підсвітити вкладку розділу, що зараз угорі.</summary>
        private void SyncTabWithScroll()
        {
            if (scrollRect == null || _sections.Count == 0) return;
            float y = scrollRect.content.anchoredPosition.y;
            var current = _sections.OrderBy(kv => SectionTop(kv.Value)).First().Key;
            foreach (var kv in _sections.OrderBy(kv => SectionTop(kv.Value)))
                if (SectionTop(kv.Value) - 40f <= y) current = kv.Key;
            // Докрутили до низу — останній розділ (Клуб), навіть якщо його верх не дійшов до краю.
            float maxY = scrollRect.content.rect.height - scrollRect.viewport.rect.height;
            if (maxY > 0 && y >= maxY - 2f) current = _sections.OrderBy(kv => SectionTop(kv.Value)).Last().Key;
            if (current == _activeTab) return;
            _activeTab = current;
            RefreshTabs();
        }

        private static float SectionTop(RectTransform section) =>
            -section.anchoredPosition.y - section.rect.height * (1f - section.pivot.y);

        // ════════════════════════════════════════════════════════════════
        //  СПИСОК
        // ════════════════════════════════════════════════════════════════
        private void BuildList()
        {
            if (listContent == null) return;
            float keepY = scrollRect != null ? scrollRect.content.anchoredPosition.y : 0f;

            for (int i = listContent.childCount - 1; i >= 0; i--)
                Destroy(listContent.GetChild(i).gameObject);
            _sections.Clear();
            _starterTimer = null;

            var data = Data;
            if (data == null) return;

            BuildSets(data);
            BuildGems();
            BuildCoins();
            BuildPots(data);
            BuildSeeds(data);
            BuildPlots(data);
            BuildClub(data);
            BuildFooter();

            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.content.anchoredPosition = new Vector2(0, keepY);
            }
            RefreshTabs();
            UpdateStarterTimer();
        }

        private Transform Section(ShopSection section, string title, string rightNote = null)
        {
            var go = new GameObject("Section_" + section, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(listContent, false);
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0.5f, 1f);
            var vl = go.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 12;
            vl.padding = new RectOffset(0, 0, 0, 18);
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            _sections[section] = rt;

            if (title != null)
            {
                var head = Row(go.transform, 30, 8, TextAnchor.LowerLeft);
                var t = Label(head.transform, title, fontHead, 24, ColText, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
                Flex(t.gameObject);
                if (rightNote != null)
                {
                    var n = Label(head.transform, rightNote, fontUi, 12, ColGreen, FontStyles.Bold, TextAlignmentOptions.BottomRight);
                    n.gameObject.AddComponent<LayoutElement>().preferredWidth = 90;
                }
            }
            return go.transform;
        }

        // ── Набори ───────────────────────────────────────────────────────
        private void BuildSets(PlayerData data)
        {
            var starter = ShopCatalog.Get(ShopCatalog.StarterId);
            var pro = ShopCatalog.Get(ShopCatalog.ProId);
            bool showStarter = ShopService.CanBuy(data, starter);
            bool showPro = ShopService.CanBuy(data, pro);
            if (!showStarter && !showPro) return;

            var sec = Section(ShopSection.Sets, null);

            if (showStarter)
            {
                var card = Card(sec, "Starter", Hex("#27351D"), Rgba(ColGoldLt, 0.45f), 18, 18, 18, 16, 10);
                var glow = Img(card.transform, "Glow", sprGlow, Rgba(ColGoldLt, 0.22f));
                IgnoreLayout(glow.gameObject);
                Place(glow.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-40, -40), new Vector2(200, 200));

                var top = Row(card.transform, 22, 8, TextAnchor.MiddleLeft);
                Pill(top.transform, "ОДИН РАЗ", ColGoldLt, Hex("#1A1408"), 11, 22);
                _starterTimer = Label(top.transform, "", fontUi, 12, ColGoldLt, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                Flex(_starterTimer.gameObject);

                var mid = Row(card.transform, 120, 12, TextAnchor.LowerLeft);
                var left = Column(mid.transform, 6);
                Flex(left);
                var title = Label(left.transform, starter.title, fontHead, 28, ColText, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
                LE(title.gameObject, -1, 34);
                PerkRow(left.transform, sprGem, null, $"{Fmt(starter.gems)} кристалів");
                PerkRow(left.transform, sprCoin, null, $"{Fmt(starter.coins)} монет");
                PerkRow(left.transform, null, Hex("#A9DCEB"), "Кришталевий горщик");
                var pot = Img(mid.transform, "Pot", sprPotCrystal, Color.white);
                pot.preserveAspect = true;
                LE(pot.gameObject, 104, 104);

                var bottom = Row(card.transform, 40, 8, TextAnchor.MiddleLeft);
                var note = Label(bottom.transform, $"Перші {ShopCatalog.StarterWindowDays} дні гри", fontUi, 12, ColMut, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                Flex(note.gameObject);
                PriceButton(bottom.transform, starter, ColGoldLt, Hex("#1A1408"), 40, 96, 15);
                ClickCard(card, starter);
            }

            if (showPro)
            {
                var card = Card(sec, "Pro", Rgba(Color.white, 0.04f), Rgba(Color.white, 0.10f), 16, 16, 16, 16, 14, horizontal: true);
                var pot = Img(card.transform, "Pot", sprPotJade, Color.white);
                pot.preserveAspect = true;
                LE(pot.gameObject, 62, 62);
                var col = Column(card.transform, 3);
                Flex(col);
                var title = Label(col.transform, pro.title, fontHead, 21, ColText, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                LE(title.gameObject, -1, 26);
                var desc = Label(col.transform, pro.desc, fontUi, 12, ColMut, FontStyles.Bold, TextAlignmentOptions.TopLeft);
                desc.textWrappingMode = TextWrappingModes.Normal;
                PriceButton(card.transform, pro, ColGreen, Hex("#0E130C"), 36, 88, 14);
                ClickCard(card, pro);
            }
        }

        // ── Кристали ─────────────────────────────────────────────────────
        private void BuildGems()
        {
            var sec = Section(ShopSection.Gems, "Кристали");
            var grid = Grid(sec, 2, new Vector2(174, 150), 10);
            var products = ShopCatalog.All.Where(p => p.section == ShopSection.Gems).ToList();
            for (int i = 0; i < products.Count; i++)
            {
                var p = products[i];
                bool best = i == products.Count - 1;
                var card = Cell(grid, p.id, best ? Rgba(ColBlue, 0.14f) : Rgba(ColBlue, 0.06f), best ? ColBlue : Rgba(ColBlue, 0.2f), 8);
                IconCluster(card.transform, sprGem, i switch { 0 => 1, 1 => 3, 2 => 5, _ => 6 }, 62, 36);
                var amount = Label(card.transform, Fmt(p.gems), fontUi, 17, ColText, FontStyles.Bold, TextAlignmentOptions.Center);
                LE(amount.gameObject, -1, 22);
                PriceButton(card.transform, p, ColGreen, Hex("#0E130C"), 36, -1, 14);
                if (!string.IsNullOrEmpty(p.badge)) CornerBadge(card.transform, p.badge, best ? ColGoldLt : ColBlue);
                ClickCard(card, p);
            }
        }

        // ── Монети ───────────────────────────────────────────────────────
        private void BuildCoins()
        {
            var sec = Section(ShopSection.Coins, "Монети");
            var grid = Grid(sec, 3, new Vector2(112, 160), 10);
            var products = ShopCatalog.All.Where(p => p.section == ShopSection.Coins).ToList();
            for (int i = 0; i < products.Count; i++)
            {
                var p = products[i];
                var card = Cell(grid, p.id, Rgba(ColGoldLt, 0.06f), Rgba(ColGoldLt, 0.22f), 5, padX: 8);
                IconCluster(card.transform, sprCoin, i switch { 0 => 1, 1 => 3, _ => 5 }, 48, 30);
                var amount = Label(card.transform, Fmt(p.coins), fontUi, 15, ColText, FontStyles.Bold, TextAlignmentOptions.Center);
                LE(amount.gameObject, -1, 20);
                var note = Label(card.transform, p.note ?? "", fontUi, 11, ColMut, FontStyles.Bold, TextAlignmentOptions.Center);
                LE(note.gameObject, -1, 14);
                PriceButton(card.transform, p, ColGreen, Hex("#0E130C"), 34, -1, 13);
                ClickCard(card, p);
            }
        }

        // ── Преміум-горщики ──────────────────────────────────────────────
        private void BuildPots(PlayerData data)
        {
            var sec = Section(ShopSection.Pots, "Преміум-горщики", "Назавжди");
            foreach (var p in ShopCatalog.All.Where(x => x.section == ShopSection.Pots))
            {
                bool gold = p.potId == "gold";
                var card = Card(sec, p.id, gold ? Rgba(ColGoldLt, 0.06f) : Rgba(Color.white, 0.04f),
                    gold ? Rgba(ColGoldLt, 0.3f) : Rgba(Color.white, 0.10f), 10, 14, 12, 12, 12, horizontal: true);

                var box = Img(card.transform, "PotBox", sprGlow, Rgba(PotTint(p.potId), 0.28f));
                LE(box.gameObject, 78, 78);
                var art = Img(box.transform, "Pot", PotSprite(p.potId), Color.white);
                art.preserveAspect = true;
                Place(art.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66, 66));

                var col = Column(card.transform, 6);
                Flex(col);
                var title = Label(col.transform, p.title, fontHead, 21, ColText, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                LE(title.gameObject, -1, 24);
                var chips = Row(col.transform, 20, 5, TextAnchor.MiddleLeft);
                foreach (var effect in p.desc.Split(new[] { " · " }, System.StringSplitOptions.RemoveEmptyEntries))
                    Pill(chips.transform, effect.Replace("часу росту", "часу"), Rgba(ColGreen, 0.14f), ColChipOk, 11, 20);

                bool owned = data.ownedPotIds.Contains(p.potId);
                if (owned) OwnedPill(card.transform, "Є");
                else
                {
                    PriceButton(card.transform, p, ColGreen, Hex("#0E130C"), 36, 80, 14);
                    ClickCard(card, p);
                }
            }
        }

        // ── Насіння ──────────────────────────────────────────────────────
        private void BuildSeeds(PlayerData data)
        {
            var sec = Section(ShopSection.Seeds, "Насіння");
            BuildCapsule(sec, data);

            var p = ShopCatalog.Get(ShopCatalog.SeedsId);
            var card = Card(sec, p.id, Rgba(Color.white, 0.04f), Rgba(Color.white, 0.10f), 14, 14, 14, 14, 14, horizontal: true);
            var art = Img(card.transform, "Pack", sprSeedPack, Color.white);
            art.preserveAspect = true;
            LE(art.gameObject, 58, 66);

            var col = Column(card.transform, 4);
            Flex(col);
            var title = Label(col.transform, p.title, fontHead, 21, ColText, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            LE(title.gameObject, -1, 24);
            var desc = Label(col.transform, p.desc, fontUi, 12, ColMut, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            desc.textWrappingMode = TextWrappingModes.Normal;
            var plant = ShopService.SeedPlant(data);
            if (plant != null)
            {
                var chipRow = Row(col.transform, 22, 0, TextAnchor.MiddleLeft);
                Pill(chipRow.transform, $"Зараз: {plant.displayName}", Rgba(ColGreen, 0.12f), ColChipOk, 12, 22);
            }

            PriceButton(card.transform, p, ColGreen, Hex("#0E130C"), 36, 80, 14);
            ClickCard(card, p);
        }

        // ── Насіннєва капсула (гача за кристали) ─────────────────────────
        private void BuildCapsule(Transform sec, PlayerData data)
        {
            var p = ShopCatalog.Capsule;
            var card = Card(sec, p.id, Rgba(ColGoldLt, 0.06f), Rgba(ColGoldLt, 0.30f), 14, 14, 14, 14, 10);

            var top = Row(card.transform, 66, 14, TextAnchor.MiddleLeft);
            var art = Img(top.transform, "Capsule", sprSeedPack, ColGoldLt);
            art.preserveAspect = true;
            LE(art.gameObject, 58, 66);
            var col = Column(top.transform, 4);
            Flex(col);
            var title = Label(col.transform, p.title, fontHead, 21, ColText, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            LE(title.gameObject, -1, 24);
            var desc = Label(col.transform, $"{SeedCapsule.PlantsPerCapsule} насінини трьох різних рослин — сюрприз",
                fontUi, 12, ColMut, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            desc.textWrappingMode = TextWrappingModes.Normal;
            CornerBadge(card.transform, "Сюрприз", ColGoldLt);

            // Шанси — обов'язково показати до покупки (Google Play / App Store).
            var odds = Label(card.transform, "Шанси для кожної насінини: " + OddsText(), fontUi, 11, ColMutLt,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            odds.textWrappingMode = TextWrappingModes.Normal;

            var btn = PriceButton(card.transform, p, ColGoldLt, Hex("#1A1408"), 40, -1, 15);
            btn.GetComponentInChildren<TMP_Text>().text = $"Відкрити · {SeedCapsule.GemPrice} кристалів";
            ClickCard(card, p);
        }

        private static string OddsText() => string.Join(" · ",
            SeedCapsule.Odds.Select(o => $"{SeedCapsule.RarityName(o.rarity)} {Mathf.RoundToInt(o.chance * 100)}%"));

        // ── Додаткові грядки ─────────────────────────────────────────────
        private void BuildPlots(PlayerData data)
        {
            var sec = Section(ShopSection.Plots, "Додаткові грядки", "Назавжди");
            var grid = Grid(sec, 2, new Vector2(174, 150), 10);
            foreach (var p in ShopCatalog.All.Where(x => x.section == ShopSection.Plots))
            {
                bool owned = ShopService.IsOwned(data, p);
                bool locked = !owned && !ShopService.CanBuy(data, p);
                var card = Cell(grid, p.id, Rgba(Color.white, locked ? 0.025f : 0.04f),
                    locked ? Rgba(Color.white, 0.08f) : Rgba(ColGreen, 0.3f), 10, align: TextAnchor.UpperLeft);

                var slot = Img(card.transform, "Slot", sprSmall, locked ? Color.clear : Rgba(ColGreen, 0.06f));
                slot.type = Image.Type.Sliced;
                LE(slot.gameObject, -1, 64);
                var slotLine = Img(slot.transform, "Border", sprSmallLine, locked ? Rgba(Color.white, 0.18f) : Rgba(ColGreen, 0.5f));
                slotLine.type = Image.Type.Sliced;
                Stretch(slotLine.rectTransform);

                if (owned || !locked)
                {
                    var ring = Img(slot.transform, "Ring", sprCircle, Rgba(ColGreen, owned ? 0.3f : 0.16f));
                    Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
                    var icon = Img(ring.transform, "Icon", owned ? sprCheck : sprPlus, owned ? ColGreenBright : Color.white);
                    icon.preserveAspect = true;
                    Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
                }
                else
                {
                    var lockIcon = Img(slot.transform, "Lock", sprLock, ColMut);
                    lockIcon.preserveAspect = true;
                    Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
                }

                var title = Label(card.transform, p.title, fontUi, 15, locked ? ColMutLt : ColText, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                LE(title.gameObject, -1, 20);

                if (owned) OwnedPill(card.transform, "Куплено", stretch: true);
                else if (locked)
                {
                    var hint = Pill(card.transform, $"{StoreGateway.PriceLabel(p)} · після 7-ї", Rgba(Color.white, 0.06f), ColMutLt, 12, 36);
                    hint.transform.parent.GetComponent<LayoutElement>().flexibleWidth = 1;
                }
                else
                {
                    PriceButton(card.transform, p, ColGreen, Hex("#0E130C"), 36, -1, 14);
                    ClickCard(card, p);
                }
            }
        }

        // ── Клуб садівника ───────────────────────────────────────────────
        private void BuildClub(PlayerData data)
        {
            var p = ShopCatalog.Get(ShopCatalog.ClubId);
            var sec = Section(ShopSection.Club, "Підписка");
            var card = Card(sec, p.id, Hex("#1D2716"), ColGoldLt, 18, 18, 18, 18, 14);

            var title = Label(card.transform, p.title, fontHead, 26, ColText, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            LE(title.gameObject, -1, 30);
            PerkRow(card.transform, sprGem, null, $"{ShopCatalog.ClubDailyGems} кристали щодня");
            PerkRow(card.transform, sprCoin, null, $"{ShopCatalog.ClubDailyCoins} монет щодня");
            PerkRow(card.transform, sprCoin, null, $"+{Mathf.RoundToInt(ShopCatalog.ClubSellBonus * 100)}% монет з продажу");
            PerkRow(card.transform, sprCircleLine, null, "Рамка «Клуб» і аватар «Сонце» — назавжди", ColGoldLt);

            if (ShopService.IsClubActive(data))
            {
                OwnedPill(card.transform, "Ти в Клубі садівника", stretch: true, height: 42);
                var manage = Label(card.transform, "Скасувати чи змінити підписку — у налаштуваннях App Store / Google Play.",
                    fontUi, 11, ColMut, FontStyles.Normal, TextAlignmentOptions.Center);
                manage.textWrappingMode = TextWrappingModes.Normal;
            }
            else
            {
                var btn = PriceButton(card.transform, p, ColGoldLt, Hex("#1A1408"), 42, -1, 15);
                btn.GetComponentInChildren<TMP_Text>().text = StoreGateway.PriceLabel(p).Contains("міс")
                    ? StoreGateway.PriceLabel(p)
                    : StoreGateway.PriceLabel(p) + " / місяць";
                ClickCard(card, p);
            }
        }

        // ── Відновити покупки ────────────────────────────────────────────
        private void BuildFooter()
        {
            var col = Column(listContent, 6);
            col.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
            col.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 4, 8);

            var restore = new GameObject("Restore", typeof(RectTransform), typeof(Image), typeof(Button));
            restore.transform.SetParent(col.transform, false);
            restore.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f);
            LE(restore, -1, 30);
            var rl = Label(restore.transform, "Відновити покупки", fontUi, 13, ColGreen, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(rl.rectTransform);
            restore.GetComponent<Button>().onClick.AddListener(Restore);

            var note = Label(col.transform, "Горщики з колекцій, аватари й рамки здобуваються тільки в грі.",
                fontUi, 11, ColMut, FontStyles.Normal, TextAlignmentOptions.Center);
            note.textWrappingMode = TextWrappingModes.Normal;

            if (StoreGateway.IsTestStore)
            {
                var test = Label(col.transform, "Редактор: тестовий магазин — покупки без оплати.",
                    fontUi, 11, ColGoldLt, FontStyles.Normal, TextAlignmentOptions.Center);
                test.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        private void UpdateStarterTimer()
        {
            if (_starterTimer == null || Data == null) return;
            double left = ShopService.StarterSecondsLeft(Data);
            if (left <= 0) { MarkDirty(); return; }
            int d = (int)(left / 86400), h = (int)(left % 86400 / 3600), m = (int)(left % 3600 / 60);
            _starterTimer.text = d > 0 ? $"Лишилось {d} дн {h} год" : $"Лишилось {h} год {m} хв";
        }

        // ════════════════════════════════════════════════════════════════
        //  ПОКУПКА
        // ════════════════════════════════════════════════════════════════
        private void OnBuy(ShopProduct p)
        {
            var data = Data;
            if (data == null || p == null || !ShopService.CanBuy(data, p)) return;
            _sheetProduct = p;
            SetSheet(SheetState.Confirm);
        }

        private void ConfirmPurchase()
        {
            if (_sheetState == SheetState.Done || _sheetState == SheetState.Error) { CloseSheet(); return; }
            if (_sheetState != SheetState.Confirm || _sheetProduct == null) return;

            var p = _sheetProduct;
            if (p.gemPrice > 0) { OpenCapsule(); return; }
            SetSheet(SheetState.Processing);
            StoreGateway.Purchase(p, (outcome, message) =>
            {
                if (this == null) return; // екран закрили, поки йшла оплата — товар усе одно нараховано
                switch (outcome)
                {
                    case PurchaseOutcome.Success:
                        SetSheet(SheetState.Done, message);
                        break;
                    case PurchaseOutcome.Cancelled:
                        CloseSheet();
                        break;
                    default:
                        SetSheet(SheetState.Error, message);
                        break;
                }
                MarkDirty();
            });
        }

        /// <summary>Капсула за кристали — без магазину застосунків, результат одразу.</summary>
        private void OpenCapsule()
        {
            var picks = SeedCapsule.TryOpen();
            if (picks == null)
            {
                SetSheet(SheetState.Error, $"Бракує кристалів: потрібно {SeedCapsule.GemPrice}. Кристали є вище в Крамниці.");
                return;
            }
            SetSheet(SheetState.Done, string.Join("\n",
                picks.Select(pl => $"{pl.displayName} — {SeedCapsule.RarityName(pl.rarity).ToLower()}")));
            MarkDirty();
        }

        private void Restore()
        {
            StoreGateway.Restore((ok, message) =>
            {
                if (this == null) return;
                ShowToast(message);
                MarkDirty();
            });
        }

        // ── Шторка ───────────────────────────────────────────────────────
        private void BuildSheet()
        {
            _sheetRoot = new GameObject("PurchaseSheet", typeof(RectTransform));
            _sheetRoot.transform.SetParent(transform, false);
            Stretch((RectTransform)_sheetRoot.transform);

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
            dim.transform.SetParent(_sheetRoot.transform, false);
            Stretch((RectTransform)dim.transform);
            dim.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.016f, 0.6f);
            dim.GetComponent<Button>().onClick.AddListener(() => { if (_sheetState != SheetState.Processing) CloseSheet(); });

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(_sheetRoot.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0);
            prt.pivot = new Vector2(0.5f, 0);
            prt.offsetMin = new Vector2(0, -24); prt.offsetMax = new Vector2(0, 0); // −24: сховати нижні закруглення
            var pimg = panel.GetComponent<Image>();
            pimg.sprite = sprSheet; pimg.type = Image.Type.Sliced; pimg.color = Hex("#172013");
            pimg.raycastTarget = true;
            var vl = panel.GetComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(20, 20, 10, 58); vl.spacing = 6;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var handleRow = new GameObject("HandleRow", typeof(RectTransform), typeof(LayoutElement));
            handleRow.transform.SetParent(panel.transform, false);
            handleRow.GetComponent<LayoutElement>().preferredHeight = 20;
            var handle = Img(handleRow.transform, "Handle", sprPill, Rgba(Color.white, 0.2f));
            handle.type = Image.Type.Sliced;
            Place(handle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(40, 5));

            var doneRow = new GameObject("DoneIcon", typeof(RectTransform), typeof(LayoutElement));
            doneRow.transform.SetParent(panel.transform, false);
            doneRow.GetComponent<LayoutElement>().preferredHeight = 70;
            var ring = Img(doneRow.transform, "Ring", sprCircle, Rgba(ColGreen, 0.18f));
            Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            var check = Img(ring.transform, "Check", sprCheck, ColGreenBright);
            check.preserveAspect = true;
            Place(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            _sheetDoneIcon = doneRow;

            _sheetKind = Label(panel.transform, "", fontUi, 11, ColMut, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _sheetKind.characterSpacing = 8;
            LE(_sheetKind.gameObject, -1, 16);
            _sheetTitle = Label(panel.transform, "", fontHead, 30, ColText, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            LE(_sheetTitle.gameObject, -1, 36);
            _sheetDesc = Label(panel.transform, "", fontUi, 14, ColMutLt, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _sheetDesc.textWrappingMode = TextWrappingModes.Normal;

            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(panel.transform, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = 10;

            _sheetConfirm = SheetButton(panel.transform, 52, ColGreen, Hex("#0E130C"), 16, out _sheetConfirmBg, out _sheetConfirmLabel);
            _sheetConfirm.onClick.AddListener(ConfirmPurchase);
            _sheetCancel = SheetButton(panel.transform, 44, Color.clear, ColMutLt, 14, out _, out _sheetCancelLabel);
            _sheetCancelLabel.text = "Скасувати";
            _sheetCancel.onClick.AddListener(CloseSheet);

            _sheetRoot.SetActive(false);
        }

        private Button SheetButton(Transform parent, float height, Color bg, Color fg, float fontSize, out Image img, out TMP_Text label)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            img = go.GetComponent<Image>();
            img.sprite = sprPill; img.type = Image.Type.Sliced;
            img.color = bg.a > 0 ? bg : new Color(0, 0, 0, 0.001f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            label = Label(go.transform, "", fontUi, fontSize, fg, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return btn;
        }

        private void SetSheet(SheetState state, string message = null)
        {
            _sheetState = state;
            var p = _sheetProduct;
            if (_sheetRoot == null || p == null) return;
            _sheetRoot.SetActive(true);
            _sheetRoot.transform.SetAsLastSibling();
            if (toastRoot != null) toastRoot.transform.SetAsLastSibling();

            bool done = state == SheetState.Done;
            _sheetDoneIcon.SetActive(done);
            _sheetKind.gameObject.SetActive(!done);
            _sheetCancel.gameObject.SetActive(state == SheetState.Confirm);
            _sheetConfirm.interactable = state != SheetState.Processing;
            _sheetConfirmBg.color = state == SheetState.Processing ? Rgba(ColGreen, 0.4f) : ColGreen;

            switch (state)
            {
                case SheetState.Confirm:
                case SheetState.Processing:
                    _sheetKind.text = p.kind.ToUpper();
                    _sheetTitle.text = p.title;
                    _sheetTitle.alignment = TextAlignmentOptions.MidlineLeft;
                    _sheetDesc.text = p.desc;
                    _sheetDesc.alignment = TextAlignmentOptions.TopLeft;
                    _sheetConfirmLabel.text = state == SheetState.Processing ? "Оплата…"
                        : p.gemPrice > 0 ? $"Відкрити за {p.gemPrice} кристалів"
                        : $"Купити за {StoreGateway.PriceLabel(p)}";
                    if (p.gemPrice > 0) _sheetDesc.text = p.desc + "\n\nШанси для кожної насінини: " + OddsText();
                    break;
                case SheetState.Done:
                    _sheetTitle.text = p.gemPrice > 0 ? "Капсулу відкрито" : "Готово";
                    _sheetTitle.alignment = TextAlignmentOptions.Center;
                    _sheetDesc.text = p.gemPrice > 0 ? message
                        : string.IsNullOrEmpty(message) ? $"{p.title} додано." : $"{p.title} додано: {message}.";
                    _sheetDesc.alignment = TextAlignmentOptions.Top;
                    _sheetConfirmLabel.text = "Добре";
                    break;
                case SheetState.Error:
                    _sheetKind.text = p.kind.ToUpper();
                    _sheetTitle.text = "Не вдалося";
                    _sheetTitle.alignment = TextAlignmentOptions.MidlineLeft;
                    _sheetDesc.text = string.IsNullOrEmpty(message) ? "Спробуй пізніше." : message;
                    _sheetDesc.alignment = TextAlignmentOptions.TopLeft;
                    _sheetConfirmLabel.text = "Закрити";
                    break;
            }
        }

        private void CloseSheet()
        {
            if (_sheetRoot != null) _sheetRoot.SetActive(false);
            _sheetProduct = null;
            _sheetState = SheetState.Confirm;
        }

        private void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (toastLabel != null) toastLabel.text = message;
            if (toastRoot != null) { toastRoot.SetActive(true); toastRoot.transform.SetAsLastSibling(); }
            _toastTimer = 2.2f;
        }

        // ════════════════════════════════════════════════════════════════
        //  ХЕЛПЕРИ ПОБУДОВИ
        // ════════════════════════════════════════════════════════════════
        private void ClickCard(GameObject card, ShopProduct p)
        {
            var btn = card.GetComponent<Button>();
            if (btn == null) btn = card.AddComponent<Button>();
            btn.targetGraphic = card.GetComponent<Image>();
            btn.onClick.AddListener(() => OnBuy(p));
        }

        private Button PriceButton(Transform parent, ShopProduct p, Color bg, Color fg, float height, float width, float fontSize)
        {
            var go = new GameObject("Price", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = height; le.minHeight = height;
            if (width > 0) { le.preferredWidth = width; le.minWidth = width; }
            else le.flexibleWidth = 1;
            var img = go.GetComponent<Image>();
            img.sprite = sprPill; img.type = Image.Type.Sliced; img.color = bg;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OnBuy(p));
            var label = Label(go.transform, StoreGateway.PriceLabel(p), fontUi, fontSize, fg, FontStyles.Bold, TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true; label.fontSizeMin = 10; label.fontSizeMax = fontSize;
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(8, 0); label.rectTransform.offsetMax = new Vector2(-8, 0);
            return btn;
        }

        private void OwnedPill(Transform parent, string text, bool stretch = false, float height = 36)
        {
            var label = Pill(parent, text, Rgba(ColGreen, 0.16f), ColChipOk, 13, height);
            var le = label.transform.parent.GetComponent<LayoutElement>();
            if (stretch) le.flexibleWidth = 1;
            else { le.preferredWidth = 64; le.minWidth = 64; }
        }

        /// <summary>Пігулка з текстом. Повертає лейбл (батько — сама пігулка з LayoutElement).</summary>
        private TMP_Text Pill(Transform parent, string text, Color bg, Color fg, float fontSize, float height)
        {
            var go = new GameObject("Pill", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprPill; img.type = Image.Type.Sliced; img.color = bg;
            var hl = go.GetComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(9, 9, 0, 0); hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = height; le.minHeight = height;
            var label = Label(go.transform, text, fontUi, fontSize, fg, FontStyles.Bold, TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private void CornerBadge(Transform card, string text, Color bg)
        {
            var label = Pill(card, text, bg, Hex("#0E1A20"), 11, 20);
            var rt = (RectTransform)label.transform.parent;
            IgnoreLayout(rt.gameObject);
            var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-10, -10);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 20);
        }

        private void PerkRow(Transform parent, Sprite icon, Color? swatch, string text, Color? iconTint = null)
        {
            var row = Row(parent, 22, 8, TextAnchor.MiddleLeft);
            if (icon != null)
            {
                var img = Img(row.transform, "Icon", icon, iconTint ?? Color.white);
                img.preserveAspect = true;
                LE(img.gameObject, 18, 18);
            }
            else
            {
                var sw = Img(row.transform, "Swatch", sprSmall, swatch ?? Color.white);
                sw.type = Image.Type.Sliced;
                LE(sw.gameObject, 18, 18);
            }
            var label = Label(row.transform, text, fontUi, 13, ColText, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Flex(label.gameObject);
        }

        /// <summary>Купка іконок (кристали/монети) — більший пакет, більша купка.</summary>
        private void IconCluster(Transform parent, Sprite icon, int count, float boxHeight, float size)
        {
            var box = new GameObject("Icons", typeof(RectTransform), typeof(LayoutElement));
            box.transform.SetParent(parent, false);
            box.GetComponent<LayoutElement>().preferredHeight = boxHeight;
            Vector2[] layout = count switch
            {
                1 => new[] { new Vector2(0, 0) },
                3 => new[] { new Vector2(-14, -6), new Vector2(14, -6), new Vector2(0, 8) },
                5 => new[] { new Vector2(-26, -10), new Vector2(26, -10), new Vector2(-12, 2), new Vector2(12, 2), new Vector2(0, 14) },
                _ => new[] { new Vector2(-30, -10), new Vector2(30, -10), new Vector2(-16, 2), new Vector2(16, 2), new Vector2(0, -6), new Vector2(0, 16) },
            };
            for (int i = 0; i < layout.Length; i++)
            {
                float s = count == 1 ? size + 4 : size - (i < layout.Length - 1 ? 4 : 0);
                var img = Img(box.transform, "Icon", icon, Color.white);
                img.preserveAspect = true;
                Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), layout[i], new Vector2(s, s));
            }
        }

        private GameObject Card(Transform parent, string name, Color bg, Color border,
            int padL, int padR, int padT, int padB, float spacing, bool horizontal = false)
        {
            var go = new GameObject("Card_" + name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprCard; img.type = Image.Type.Sliced; img.color = bg;
            img.raycastTarget = true;
            var line = Img(go.transform, "Border", sprCardLine, border);
            line.type = Image.Type.Sliced;
            Stretch(line.rectTransform);
            IgnoreLayout(line.gameObject);

            HorizontalOrVerticalLayoutGroup lg = horizontal
                ? go.AddComponent<HorizontalLayoutGroup>()
                : (HorizontalOrVerticalLayoutGroup)go.AddComponent<VerticalLayoutGroup>();
            lg.padding = new RectOffset(padL, padR, padT, padB);
            lg.spacing = spacing;
            lg.childAlignment = horizontal ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft;
            lg.childControlWidth = true; lg.childControlHeight = true;
            lg.childForceExpandWidth = !horizontal; lg.childForceExpandHeight = false;
            return go;
        }

        private Transform Grid(Transform parent, int columns, Vector2 cell, float spacing)
        {
            var go = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            go.transform.SetParent(parent, false);
            var gl = go.GetComponent<GridLayoutGroup>();
            gl.cellSize = cell; gl.spacing = new Vector2(spacing, spacing);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = columns;
            gl.childAlignment = TextAnchor.UpperCenter;
            return go.transform;
        }

        private GameObject Cell(Transform grid, string name, Color bg, Color border, float spacing,
            int padX = 12, TextAnchor align = TextAnchor.UpperCenter)
        {
            var card = Card(grid, name, bg, border, padX, padX, 14, 12, spacing);
            card.GetComponent<VerticalLayoutGroup>().childAlignment = align;
            return card;
        }

        private GameObject Row(Transform parent, float height, float spacing, TextAnchor align)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            if (height > 0) go.GetComponent<LayoutElement>().minHeight = height;
            var hl = go.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing; hl.childAlignment = align;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            return go;
        }

        private GameObject Column(Transform parent, float spacing)
        {
            var go = new GameObject("Column", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var vl = go.GetComponent<VerticalLayoutGroup>();
            vl.spacing = spacing;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            return go;
        }

        private Image Img(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI Label(Transform parent, string text, TMP_FontAsset font, float size, Color color,
            FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            if (font != null) tmp.font = font;
            tmp.fontSize = size; tmp.color = color; tmp.fontStyle = style; tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void LE(GameObject go, float w, float h)
        {
            var le = GetOrAdd<LayoutElement>(go);
            if (w >= 0) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; }
        }

        private static void Flex(GameObject go)
        {
            GetOrAdd<LayoutElement>(go).flexibleWidth = 1;
        }

        private static void IgnoreLayout(GameObject go)
        {
            GetOrAdd<LayoutElement>(go).ignoreLayout = true;
        }

        // Unity-компоненти мають «фальшивий null», тож оператор ?? для них не годиться.
        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
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

        private Sprite PotSprite(string potId) => potId switch
        {
            "crystal" => sprPotCrystal,
            "jade" => sprPotJade,
            _ => sprPotGold,
        };

        private static Color PotTint(string potId) => potId switch
        {
            "crystal" => Hex("#8FD0E6"),
            "jade" => Hex("#6EC49A"),
            _ => Hex("#E4C77E"),
        };

        /// <summary>1500 → «1 500».</summary>
        private static string Fmt(int n) => n.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", " ");

        private static readonly Color ColText = UIColors.Text;
        private static readonly Color ColMut = UIColors.Hex("#8C9683");
        private static readonly Color ColMutLt = UIColors.Hex("#A9B2A0");
        private static readonly Color ColGreen = UIColors.Green;
        private static readonly Color ColGreenBright = UIColors.Hex("#B5E67A");
        private static readonly Color ColChipOk = UIColors.Hex("#CDEBA0");
        private static readonly Color ColGoldLt = UIColors.GoldLt;
        private static readonly Color ColBlue = UIColors.Blue;

        private static Color Hex(string hex) => UIColors.Hex(hex);
        private static Color Rgba(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
