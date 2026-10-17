using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlantBreeding.Core;
using PlantBreeding.Garden;
using PlantBreeding.Save;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Логіка екрану "Посадка": три послідовні вибори (рослина / горщик /
    /// добриво) + прогноз + кнопка "Посадити". Статичний каркас (заголовок,
    /// скрол-контейнери, PreviewBar) будується Assets/Editor/PlantingScreenBuilder.cs,
    /// цей скрипт лише наповнює контейнери картками й рахує прогноз.
    ///
    /// Картки будуються ОДИН РАЗ у Start() і більше ніколи не знищуються —
    /// вибір/купівля лише міняють колір/текст/interactable вже існуючих
    /// об'єктів (RefreshVisuals). Знищення GameObject-а картки в тому ж
    /// кадрі, коли по ній щойно клікнули, ламає InputSystemUIInputModule
    /// (MissingReferenceException в hover-трекінгу) — навіть відкладання на
    /// кадр не завжди рятує, тому найнадійніше просто ніколи не знищувати.
    ///
    /// Ціль посадки (PlotSlot) передається через PlantingRequest — сцена
    /// завантажується додатково (additive) поверх MainGarden, тому референс
    /// на той самий об'єкт PlotSlot лишається живим без серіалізації.
    /// </summary>
    public class PlantingScreenController : MonoBehaviour
    {
        private const string PlantingSceneName = "Planting";

        [Header("Контейнери карток (наповнюються в рантаймі)")]
        public Transform plantsContent;
        public Transform potsContent;
        public Transform boostsContent;

        [Header("Шапка")]
        public TMP_Text coinsLabel;
        public TMP_Text gemsLabel;
        public Button backButton;

        [Header("Прогноз + кнопка")]
        public TMP_Text growLabel;
        public TMP_Text growNoteLabel;
        public TMP_Text sellLabel;      // колонка «продаж» (раніше «мутація» — мутацій на цьому етапі немає)
        public TMP_Text sellNoteLabel;
        public TMP_Text forecastPlaceholder;
        public Transform forecastColumns;
        public Image previewPlantImage;
        public Image previewPotGlyph;
        public Button plantButton;
        public TMP_Text plantButtonLabel;

        [Header("Спільна графіка (з UIBuilderKit)")]
        public Sprite sprRoundedCard;
        public Sprite sprRoundedCardLine;
        public Sprite sprPill;
        public Sprite sprPillLine;
        public Sprite sprPotGlyph;
        public Sprite sprCoin;
        public Sprite sprTag;   // rounded-12 — цінник у кутку картки рослини
        public TMP_FontAsset fontHead;
        public TMP_FontAsset fontUi;

        private class PlantCardHandle
        {
            public PlantData plant;
            public Image bg, border;
            public CanvasGroup group;
            public TMP_Text sub;
            public Image priceBg;      // цінник у кутку: ціна ОДНІЄЇ рослини
            public TMP_Text priceLabel;
            public GameObject priceCoin;
        }


        private class PotCardHandle
        {
            public PotData pot;
            public Image bg, border;
            public CanvasGroup group;
            public TMP_Text effect;
        }

        private class BoostCardHandle
        {
            public StarterBoostKind kind;
            public Image bg, border;
            public CanvasGroup group;
        }

        private List<PlantData> _allPlants;
        private List<PotData> _pots;
        private readonly List<PlantCardHandle> _plantCards = new List<PlantCardHandle>();
        private readonly List<PotCardHandle> _potCards = new List<PotCardHandle>();
        private readonly List<BoostCardHandle> _boostCards = new List<BoostCardHandle>();

        private PlantData _selectedPlant;
        private PotData _selectedPot;
        private StarterBoostKind _selectedBoost = StarterBoostKind.None;

        private void OnEnable() => Shop.ShopService.Changed += RefreshVisuals; // горщик куплено в Крамниці
        private void OnDisable() => Shop.ShopService.Changed -= RefreshVisuals;

        private void Start()
        {
            if (PlantingRequest.TargetSlot == null)
            {
                Debug.LogWarning("[PlantingScreenController] Немає цільового слоту — сцену відкрито напряму.");
            }

            if (backButton != null) backButton.onClick.AddListener(Close);
            if (plantButton != null) plantButton.onClick.AddListener(OnPlantClicked);

            LoadCatalogs();
            ApplyDefaults();
            BuildPlantCards();
            BuildPotCards();
            BuildBoostCards();
            RefreshVisuals();
            RefreshForecast();
        }

        private void LoadCatalogs()
        {
            // Показуємо всі відомі сорти, навіть якщо запас насіння = 0 —
            // картка тоді пропонує докупити насінину за монети. Порядок —
            // за рівнем відкриття: закриті види видно наперед як ціль.
            _allPlants = PlantCatalog.All.ToList();

            _pots = Resources.LoadAll<PotData>("Pots")
                .OrderBy(p => (int)p.rarity)
                .ThenBy(p => p.IsRewardOnly ? int.MaxValue : p.unlockCost)
                .ToList();
        }

        private void ApplyDefaults()
        {
            // Автовибір: подарована насінина (старт, нагороди) або перша рослина, на яку вистачає монет.
            // Купівля — однієї рослини і лише в момент посадки (OnPlantClicked).
            _selectedPlant = _allPlants.FirstOrDefault(p => !IsLevelLocked(p) && HasFreeSeed(p))
                             ?? _allPlants.FirstOrDefault(p => !IsLevelLocked(p) && CanAfford(p));

            string lastUsedPotId = GameManager.Instance != null ? GameManager.Instance.playerData.lastUsedPotId : null;
            _selectedPot = _pots.FirstOrDefault(p => p.potId == lastUsedPotId) ?? _pots.FirstOrDefault();

            // Добриво завжди стартує на "Без добрива" — витратний ресурс, не автовибираємо.
            _selectedBoost = StarterBoostKind.None;
        }

        // ════════════════════════════════════════════════════════════════
        //  ПОБУДОВА КАРТОК (один раз, більше НІКОЛИ не знищуються)
        // ════════════════════════════════════════════════════════════════
        private void BuildPlantCards()
        {
            ClearChildren(plantsContent);
            _plantCards.Clear();
            foreach (var plant in _allPlants)
            {
                var handle = new PlantCardHandle { plant = plant };
                BuildCard(plantsContent, () => HandlePlantClick(plant), card =>
                {
                    handle.bg = card.GetComponent<Image>();
                    handle.border = card.Find("Border").GetComponent<Image>();
                    handle.group = card.GetComponent<CanvasGroup>();

                    // Фінальний вигляд (доросла рослина, стадія 4) — щоб було видно, що саме виросте.
                    var icon = MakeImage(card, "Icon", plant.sprite100 != null ? plant.sprite100 : plant.sprite25, plant.tint);
                    Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(62, 72));
                    icon.preserveAspect = true;

                    var name = MakeLabel(card, "Name", plant.displayName, fontHead, 16, UIColors.Text, FontStyles.Normal);
                    Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(96, 20));
                    name.alignment = TextAlignmentOptions.Center;

                    var sub = MakeLabel(card, "Sub", "", fontUi, 10.5f, UIColors.Soft, FontStyles.Normal);
                    Place(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(96, 14));
                    sub.alignment = TextAlignmentOptions.Center;
                    handle.sub = sub;

                    BuildPriceTag(card, handle);
                });
                _plantCards.Add(handle);
            }
        }

        private void BuildPotCards()
        {
            ClearChildren(potsContent);
            _potCards.Clear();
            foreach (var pot in _pots)
            {
                var handle = new PotCardHandle { pot = pot };
                BuildCard(potsContent, () => HandlePotClick(pot), card =>
                {
                    handle.bg = card.GetComponent<Image>();
                    handle.border = card.Find("Border").GetComponent<Image>();
                    handle.group = card.GetComponent<CanvasGroup>();

                    var glyph = MakeImage(card, "Icon", sprPotGlyph, pot.colorMid, Image.Type.Simple);
                    Place(glyph.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(58, 46));
                    glyph.preserveAspect = true;

                    var name = MakeLabel(card, "Name", pot.displayName, fontHead, 14, UIColors.Text, FontStyles.Normal);
                    Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 42), new Vector2(98, 20));
                    name.alignment = TextAlignmentOptions.Center;
                    name.enableAutoSizing = true; name.fontSizeMin = 10; name.fontSizeMax = 14;

                    // До 3 рядків: 2 рядки ефекту (PotData.effectLabel) + ціна, якщо ще не куплено.
                    var eff = MakeLabel(card, "Effect", "", fontUi, 9, UIColors.Soft, FontStyles.Bold);
                    Place(eff.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(98, 36));
                    eff.alignment = TextAlignmentOptions.Center;
                    eff.lineSpacing = -8;
                    handle.effect = eff;
                });
                _potCards.Add(handle);
            }
        }

        private void BuildBoostCards()
        {
            ClearChildren(boostsContent);
            _boostCards.Clear();
            foreach (var def in StarterBoostCatalog.All)
            {
                var handle = new BoostCardHandle { kind = def.kind };
                BuildBoostCard(boostsContent, def, () => HandleBoostClick(def.kind), card =>
                {
                    handle.bg = card.GetComponent<Image>();
                    handle.border = card.Find("Border").GetComponent<Image>();
                    handle.group = card.GetComponent<CanvasGroup>();
                });
                _boostCards.Add(handle);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  КЛІКИ (лише міняють вибір/гроші, картки не чіпають напряму)
        // ════════════════════════════════════════════════════════════════
        private void HandlePlantClick(PlantData plant)
        {
            var gm = GameManager.Instance;
            if (gm == null || IsLevelLocked(plant)) return;

            // Тап лише обирає рослину; платимо при посадці (кнопка «Посадити · 8»).
            if (HasFreeSeed(plant) || CanAfford(plant)) _selectedPlant = plant;
            RefreshVisuals();
        }

        /// <summary>
        /// Цінник у кутку картки: скільки коштує ОДНА рослина (монети знімаються при посадці).
        /// Не кнопка — тап проходить на саму картку (вибір рослини).
        /// </summary>
        private void BuildPriceTag(Transform card, PlantCardHandle handle)
        {
            // Радіус 12; ширину рахує LayoutPriceTag за текстом — однакові відступи з обох боків.
            var go = new GameObject("Price", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(card, false);
            Place((RectTransform)go.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6, -6), new Vector2(44, 24));
            var bg = go.GetComponent<Image>();
            bg.sprite = sprTag != null ? sprTag : sprPill; bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;

            var label = MakeLabel(go.transform, "Label", "", fontUi, 11f, UIColors.Text, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            GameObject coinGo = null;
            if (sprCoin != null)
            {
                coinGo = new GameObject("Coin", typeof(RectTransform), typeof(Image));
                coinGo.transform.SetParent(go.transform, false);
                var coin = coinGo.GetComponent<Image>();
                coin.sprite = sprCoin; coin.preserveAspect = true; coin.raycastTarget = false;
                Place((RectTransform)coinGo.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-TagPad, 0), new Vector2(TagCoin, TagCoin));
            }

            handle.priceBg = bg;
            handle.priceLabel = label;
            handle.priceCoin = coinGo;
        }

        private const float TagPad = 8f, TagCoin = 13f, TagGap = 4f;

        /// <summary>Ширина цінника = відступ + текст (+ проміжок + монета) + відступ.</summary>
        private static void LayoutPriceTag(PlantCardHandle h, bool withCoin)
        {
            float textW = Mathf.Ceil(h.priceLabel.GetPreferredValues(h.priceLabel.text).x);
            float coinW = withCoin ? TagGap + TagCoin : 0f;
            var rt = (RectTransform)h.priceBg.transform;
            rt.sizeDelta = new Vector2(TagPad + textW + coinW + TagPad, 24);
            var lrt = h.priceLabel.rectTransform;
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0, 1);
            lrt.pivot = new Vector2(0, 0.5f);
            lrt.anchoredPosition = new Vector2(TagPad, 0);
            lrt.sizeDelta = new Vector2(textW, 0);
            if (h.priceCoin != null) h.priceCoin.SetActive(withCoin);
        }

        private void HandlePotClick(PotData pot)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            bool owned = IsPotOwned(pot);
            if (owned)
            {
                _selectedPot = pot;
                gm.SetLastUsedPot(pot.potId);
            }
            else if (pot.IsShopPot)
            {
                SceneNavButton.OpenTab(SceneNavButton.ShopScene); // преміум-горщик — у Крамниці
                return;
            }
            else if (!pot.IsRewardOnly && gm.TryUnlockPot(pot.potId, pot.unlockCost))
            {
                _selectedPot = pot;
            }
            else if (!pot.IsRewardOnly && gm.playerData.coins >= pot.unlockCost)
            {
                // Монет на горщик вистачає, але тоді нічого буде посадити.
                Core.GameEvents.RaiseToast($"Залиш {gm.CheapestPlantCost} монет на рослину — горщик купиш трохи згодом");
            }
            RefreshVisuals();
        }

        private void HandleBoostClick(StarterBoostKind kind)
        {
            var gm = GameManager.Instance;
            var def = StarterBoostCatalog.Get(kind);
            if (def.coinCost <= 0 || (gm != null && gm.playerData.coins >= def.coinCost))
            {
                _selectedBoost = kind;
            }
            RefreshVisuals();
        }

        // ════════════════════════════════════════════════════════════════
        //  ВІДМАЛЬОВКА (лише властивості вже існуючих об'єктів)
        // ════════════════════════════════════════════════════════════════
        private void RefreshVisuals()
        {
            var gm = GameManager.Instance;

            foreach (var h in _plantCards)
            {
                bool selected = _selectedPlant == h.plant;
                bool free = HasFreeSeed(h.plant);
                bool canBuy = CanAfford(h.plant);
                bool levelLocked = IsLevelLocked(h.plant);

                ApplyCardStyle(h.bg, h.border, h.group, selected, gold: false, interactable: !levelLocked && (free || canBuy));

                // Під назвою — лише «з рівня N» для закритих; ціну показує цінник у кутку.
                h.sub.text = levelLocked ? $"з рівня {h.plant.unlockLevel}" : "";
                h.sub.color = UIColors.Soft;

                if (h.priceBg != null)
                {
                    h.priceBg.gameObject.SetActive(!levelLocked);
                    // Подарована насінина (старт, нагороди, капсула) — посадка без оплати.
                    h.priceLabel.text = free ? "Безкоштовно" : h.plant.seedCost.ToString();
                    LayoutPriceTag(h, withCoin: !free && h.priceCoin != null);
                    h.priceLabel.color = free ? UIColors.Green : canBuy ? UIColors.Text : UIColors.Soft;
                    h.priceBg.color = UIColors.Rgba(Color.white, canBuy || free ? 0.10f : 0.05f);
                }
            }

            foreach (var h in _potCards)
            {
                bool selected = _selectedPot == h.pot;
                bool owned = IsPotOwned(h.pot);
                bool canUnlock = !owned && !h.pot.IsRewardOnly && gm != null && gm.CanSpendKeepingPlantMoney(h.pot.unlockCost);

                ApplyCardStyle(h.bg, h.border, h.group, selected, gold: h.pot.premiumVisual && !selected, interactable: owned || canUnlock || h.pot.IsShopPot);

                string gold = ColorUtility.ToHtmlStringRGB(UIColors.GoldLt);
                h.effect.text = owned
                    ? h.pot.effectLabel
                    : h.pot.IsRewardOnly
                        ? $"{h.pot.effectLabel}\n<color=#{gold}>{h.pot.rewardSource}</color>"
                        : $"{h.pot.effectLabel}\n<color=#{gold}>Купити · {h.pot.unlockCost}</color>";
                h.effect.color = h.pot.growTimeModifier != 0f ? UIColors.Green : UIColors.Soft;
            }

            foreach (var h in _boostCards)
            {
                bool selected = _selectedBoost == h.kind;
                var def = StarterBoostCatalog.Get(h.kind);
                bool interactable = def.coinCost <= 0 || (gm != null && gm.playerData.coins >= def.coinCost);
                ApplyPillStyle(h.bg, h.border, h.group, selected, interactable);
            }

            RefreshForecast();
        }

        /// <summary>Стартовий (безкоштовний) або вже куплений/отриманий з колекції.</summary>
        private static bool IsPotOwned(PotData pot) =>
            (pot.unlockCost <= 0 && !pot.IsRewardOnly)
            || (GameManager.Instance != null && GameManager.Instance.IsPotOwned(pot.potId));

        /// <summary>
        /// Вид ще не відкрито за рівнем — але насіння з Насіннєвої капсули
        /// (Shop/SeedCapsule) дозволяє посадити його раніше.
        /// </summary>
        private static bool IsLevelLocked(PlantData plant) =>
            GameManager.Instance != null && plant.unlockLevel > GameManager.Instance.playerData.level
            && GameManager.Instance.GetSeedCount(plant.plantId) <= 0;

        /// <summary>Є подарована насінина цього виду (старт, нагороди, капсула) — посадка без оплати.</summary>
        private static bool HasFreeSeed(PlantData plant) =>
            GameManager.Instance != null && GameManager.Instance.GetSeedCount(plant.plantId) > 0;

        private static bool CanAfford(PlantData plant) =>
            plant.seedCost > 0 && GameManager.Instance != null && GameManager.Instance.playerData.coins >= plant.seedCost;

        /// <summary>Скільки монет зніме посадка: рослина (якщо не подарована) + добриво.</summary>
        private int PlantingCost(PlantData plant) =>
            (HasFreeSeed(plant) ? 0 : plant.seedCost) + StarterBoostCatalog.Get(_selectedBoost).coinCost;

        private static void ApplyCardStyle(Image bg, Image border, CanvasGroup group, bool selected, bool gold, bool interactable)
        {
            bg.color = selected ? UIColors.Rgba(UIColors.Green, 0.20f) : (gold ? UIColors.Rgba(UIColors.GoldLt, 0.14f) : UIColors.Rgba(Color.white, 0.05f));
            border.color = selected ? UIColors.Green : (gold ? UIColors.Gold : UIColors.Rgba(Color.white, 0.10f));
            if (group != null)
            {
                group.alpha = interactable ? 1f : 0.5f;
                group.interactable = interactable;
                group.blocksRaycasts = interactable;
            }
        }

        private static void ApplyPillStyle(Image bg, Image border, CanvasGroup group, bool selected, bool interactable)
        {
            bg.color = selected ? UIColors.Rgba(UIColors.Green, 0.18f) : UIColors.Rgba(Color.white, 0.05f);
            border.color = selected ? UIColors.Green : UIColors.Rgba(Color.white, 0.10f);
            if (group != null)
            {
                group.alpha = interactable ? 1f : 0.5f;
                group.interactable = interactable;
                group.blocksRaycasts = interactable;
            }
        }

        private void RefreshForecast()
        {
            if (GameManager.Instance != null)
            {
                if (coinsLabel != null) coinsLabel.text = HeaderView.FormatAmount(GameManager.Instance.playerData.coins);
                if (gemsLabel != null) gemsLabel.text = HeaderView.FormatAmount(GameManager.Instance.playerData.gems);
            }

            bool hasPlant = _selectedPlant != null;
            if (forecastColumns != null) forecastColumns.gameObject.SetActive(hasPlant);
            if (forecastPlaceholder != null) forecastPlaceholder.gameObject.SetActive(!hasPlant);

            if (previewPlantImage != null)
            {
                previewPlantImage.enabled = hasPlant;
                if (hasPlant)
                {
                    // Над гліфом обраного горщика — рослина без власного горщика (якщо є арт).
                    var plantOnly = _selectedPlant.GetPlantOnlySprite(1f);
                    previewPlantImage.sprite = plantOnly != null ? plantOnly
                        : (_selectedPlant.sprite25 != null ? _selectedPlant.sprite25 : _selectedPlant.sprite100);
                    previewPlantImage.color = _selectedPlant.tint;
                }
            }
            if (previewPotGlyph != null && _selectedPot != null)
            {
                previewPotGlyph.color = _selectedPot.colorMid;
            }

            int cost = hasPlant ? PlantingCost(_selectedPlant) : 0;
            bool affordable = hasPlant && GameManager.Instance != null && GameManager.Instance.playerData.coins >= cost;
            if (plantButton != null) plantButton.interactable = affordable;
            if (plantButtonLabel != null)
                plantButtonLabel.text = !hasPlant ? "Оберіть рослину"
                    : cost > 0 ? $"Посадити · {cost} монет"
                    : "Посадити";

            if (!hasPlant) return;

            var forecast = PlantingForecast.Compute(_selectedPlant, _selectedPot, _selectedBoost);
            // Два рядки «Підпис: значення» з кожного боку (звичайний текст — так його
            // перекладає LocalizationApplier за шаблонами «Час росту: {0}» тощо).
            if (growLabel != null) growLabel.text = $"Час росту: {TimeFormat.Remaining(forecast.growTimeSeconds)}";
            if (sellLabel != null) sellLabel.text = $"Отримаю: {forecast.sellPrice} монет";

            bool hasBoost = _selectedBoost != StarterBoostKind.None;
            float growModPercent = ((_selectedPot != null ? _selectedPot.growTimeModifier : 0f)
                + StarterBoostCatalog.Get(_selectedBoost).growTimeModifier) * 100f;
            if (growNoteLabel != null)
            {
                string value = Mathf.Abs(growModPercent) > 0.1f ? $"{growModPercent:+0;−0}% часу" : "базовий";
                growNoteLabel.text = hasBoost ? $"Горщик і добриво: {value}" : $"Горщик: {value}";
            }

            if (sellNoteLabel != null)
            {
                float potSell = _selectedPot != null ? _selectedPot.sellPriceBonus : 0f;
                sellNoteLabel.text = potSell > 0.001f
                    ? $"Бонус: горщик +{Mathf.RoundToInt(potSell * 100f)}%, полив до +30%"
                    : "Бонус: полив до +30%";
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  ДІЇ
        // ════════════════════════════════════════════════════════════════
        private void OnPlantClicked()
        {
            if (_selectedPlant == null || GameManager.Instance == null) return;

            // Одна рослина: подарована насінина — безкоштовно, інакше ціна рослини; плюс добриво.
            var gm = GameManager.Instance;
            int cost = PlantingCost(_selectedPlant);
            if (gm.playerData.coins < cost) return; // кнопка мала бути неактивна
            bool free = HasFreeSeed(_selectedPlant);
            if (free && !gm.TryConsumeSeed(_selectedPlant.plantId)) return;
            if (cost > 0 && !gm.TrySpendCoins(cost)) return;

            GameManager.Instance.SetLastUsedPot(_selectedPot != null ? _selectedPot.potId : "");
            PlantingRequest.TargetSlot?.Plant(_selectedPlant, _selectedPot, _selectedBoost);
            SaveSystem.Save(GameManager.Instance.playerData);

            Close();
        }

        private void Close()
        {
            PlantingRequest.Clear();
            SceneManager.UnloadSceneAsync(PlantingSceneName);
        }

        // ════════════════════════════════════════════════════════════════
        //  ПРИМІТИВИ ПОБУДОВИ (рантайм, без Editor-залежностей)
        // ════════════════════════════════════════════════════════════════
        private void BuildCard(Transform parent, System.Action onClick, System.Action<Transform> build)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = 104;
            go.GetComponent<LayoutElement>().preferredHeight = 128;

            var bg = go.GetComponent<Image>();
            bg.sprite = sprRoundedCard;
            bg.type = Image.Type.Sliced;

            var border = MakeImage(go.transform, "Border", sprRoundedCardLine, Color.clear, Image.Type.Sliced);
            Stretch(border.rectTransform);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => onClick());

            build(go.transform);
        }

        private void BuildBoostCard(Transform parent, StarterBoostDef def, System.Action onClick, System.Action<Transform> build)
        {
            var go = new GameObject("BoostCard", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = 150;
            go.GetComponent<LayoutElement>().preferredHeight = 68;

            var bg = go.GetComponent<Image>();
            bg.sprite = sprPill;
            bg.type = Image.Type.Sliced;

            var border = MakeImage(go.transform, "Border", sprPillLine, Color.clear, Image.Type.Sliced);
            Stretch(border.rectTransform);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => onClick());

            var name = MakeLabel(go.transform, "Name", def.displayName, fontUi, 13, UIColors.Text, FontStyles.Bold);
            Place(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -9), new Vector2(122, 16));
            name.alignment = TextAlignmentOptions.TopLeft;
            name.textWrappingMode = TextWrappingModes.NoWrap;

            Color subColor = def.kind == StarterBoostKind.Growth ? UIColors.Green
                : UIColors.Soft;

            // Ефект і ціна — окремі рядки (не одна довга строка з "·", яка вилазила за межі картки).
            var effect = MakeLabel(go.transform, "Effect", def.subLabel, fontUi, 10, subColor, FontStyles.Normal);
            Place(effect.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -27), new Vector2(122, 14));
            effect.alignment = TextAlignmentOptions.TopLeft;
            effect.textWrappingMode = TextWrappingModes.NoWrap;

            if (def.coinCost > 0)
            {
                var cost = MakeLabel(go.transform, "Cost", $"{def.coinCost} монет", fontUi, 10, UIColors.Soft, FontStyles.Normal);
                Place(cost.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -43), new Vector2(122, 14));
                cost.alignment = TextAlignmentOptions.TopLeft;
                cost.textWrappingMode = TextWrappingModes.NoWrap;
            }

            build(go.transform);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.GetChild(i).gameObject);
        }

        private Image MakeImage(Transform parent, string name, Sprite sprite, Color color, Image.Type type = Image.Type.Simple)
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
    }

    /// <summary>Палітра, доступна і рантайм-, і Editor-коду (без залежності на UnityEditor).</summary>
    internal static class UIColors
    {
        public static readonly Color Text = Hex("#F2ECDF");
        public static readonly Color Soft = Hex("#93A088");
        public static readonly Color Green = Hex("#A7CE73");
        public static readonly Color Gold = Hex("#C9A65A");
        public static readonly Color GoldLt = Hex("#E4C77E");
        public static readonly Color Blue = Hex("#8FD0E6");

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color Rgba(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
