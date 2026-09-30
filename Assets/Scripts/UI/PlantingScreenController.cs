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
        public TMP_FontAsset fontHead;
        public TMP_FontAsset fontUi;

        private class PlantCardHandle
        {
            public PlantData plant;
            public Image bg, border;
            public CanvasGroup group;
            public TMP_Text sub;
            public Button buy5;
            public Image buy5Bg;
            public TMP_Text buy5Label;
        }

        private const int BulkSeedCount = 5;

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
            // Автовибір лише сорту, який реально є в запасі (інакше довелось би
            // "садити" неіснуючу насінину) — якщо запасу немає, гравець сам
            // тапає картку, що докуповує насінину.
            _selectedPlant = _allPlants.FirstOrDefault(p => GameManager.Instance != null && GameManager.Instance.GetSeedCount(p.plantId) > 0
                && !IsLevelLocked(p));

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

                    BuildBulkBuyButton(card, handle);
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

            if (gm.GetSeedCount(plant.plantId) > 0)
            {
                _selectedPlant = plant;
            }
            else if (plant.seedCost > 0 && gm.TryBuySeed(plant.plantId, plant.seedCost))
            {
                _selectedPlant = plant;
            }
            RefreshVisuals();
        }

        /// <summary>
        /// Пілюля «+5» у кутку картки: купує 5 насінин одним тапом (вкладена
        /// кнопка перехоплює клік — картка під нею не спрацьовує).
        /// </summary>
        private void BuildBulkBuyButton(Transform card, PlantCardHandle handle)
        {
            var go = new GameObject("Buy5", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(card, false);
            Place((RectTransform)go.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-5, -5), new Vector2(58, 20));
            var bg = go.GetComponent<Image>();
            bg.sprite = sprPill; bg.type = Image.Type.Sliced;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => HandleBulkBuy(handle.plant));

            var label = MakeLabel(go.transform, "Label", "", fontUi, 9.5f, UIColors.Text, FontStyles.Bold);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            handle.buy5 = btn;
            handle.buy5Bg = bg;
            handle.buy5Label = label;
        }

        private void HandleBulkBuy(PlantData plant)
        {
            var gm = GameManager.Instance;
            if (gm == null || IsLevelLocked(plant) || plant.seedCost <= 0) return;
            if (gm.TryBuySeeds(plant.plantId, plant.seedCost, BulkSeedCount))
            {
                _selectedPlant = plant;
                SaveSystem.Save(gm.playerData);
            }
            RefreshVisuals();
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
                int count = gm != null ? gm.GetSeedCount(h.plant.plantId) : 0;
                bool inStock = count > 0;
                bool canBuy = h.plant.seedCost > 0 && gm != null && gm.playerData.coins >= h.plant.seedCost;
                bool levelLocked = IsLevelLocked(h.plant);

                ApplyCardStyle(h.bg, h.border, h.group, selected, gold: false, interactable: !levelLocked && (inStock || canBuy));

                h.sub.text = levelLocked ? $"з рівня {h.plant.unlockLevel}"
                    : inStock ? $"×{count} насіння"
                    : h.plant.seedCost > 0 ? $"Купити · {h.plant.seedCost} монет"
                    : "Лише зі схрещування";
                h.sub.color = levelLocked || inStock ? UIColors.Soft : UIColors.GoldLt;

                if (h.buy5 != null)
                {
                    int bulkCost = h.plant.seedCost * BulkSeedCount;
                    bool showBulk = !levelLocked && h.plant.seedCost > 0;
                    bool canBulk = showBulk && gm != null && gm.playerData.coins >= bulkCost;
                    h.buy5.gameObject.SetActive(showBulk);
                    h.buy5.interactable = canBulk;
                    h.buy5Label.text = $"+{BulkSeedCount} · {bulkCost}";
                    h.buy5Label.color = canBulk ? UIColors.Hex("#10160B") : UIColors.Soft;
                    h.buy5Bg.color = canBulk ? UIColors.GoldLt : UIColors.Rgba(Color.white, 0.08f);
                }
            }

            foreach (var h in _potCards)
            {
                bool selected = _selectedPot == h.pot;
                bool owned = IsPotOwned(h.pot);
                bool canUnlock = !owned && !h.pot.IsRewardOnly && gm != null && gm.playerData.coins >= h.pot.unlockCost;

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
                if (coinsLabel != null) coinsLabel.text = GameManager.Instance.playerData.coins.ToString("N0");
                if (gemsLabel != null) gemsLabel.text = GameManager.Instance.playerData.gems.ToString("N0");
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

            if (plantButton != null) plantButton.interactable = hasPlant;
            if (plantButtonLabel != null)
                plantButtonLabel.text = hasPlant
                    ? $"Посадити {_selectedPlant.displayName.ToLower()}"
                    : "Оберіть рослину";

            if (!hasPlant) return;

            var forecast = PlantingForecast.Compute(_selectedPlant, _selectedPot, _selectedBoost);
            if (growLabel != null) growLabel.text = forecast.GrowLabel;
            if (sellLabel != null) sellLabel.text = forecast.sellPrice.ToString();

            float growModPercent = ((_selectedPot != null ? _selectedPot.growTimeModifier : 0f)
                + StarterBoostCatalog.Get(_selectedBoost).growTimeModifier) * 100f;
            if (growNoteLabel != null)
                growNoteLabel.text = Mathf.Abs(growModPercent) > 0.1f ? $"{growModPercent:+0;-0}% модиф." : "базовий";

            if (sellNoteLabel != null)
            {
                float potSell = _selectedPot != null ? _selectedPot.sellPriceBonus : 0f;
                sellNoteLabel.text = potSell > 0.001f ? $"+{potSell * 100f:0}% горщик" : "+полив до 30%";
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  ДІЇ
        // ════════════════════════════════════════════════════════════════
        private void OnPlantClicked()
        {
            if (_selectedPlant == null || GameManager.Instance == null) return;

            int boostCost = StarterBoostCatalog.Get(_selectedBoost).coinCost;
            if (GameManager.Instance.playerData.coins < boostCost) return; // картка мала бути задизейблена раніше

            if (!GameManager.Instance.TryConsumeSeed(_selectedPlant.plantId)) return;
            GameManager.Instance.TrySpendCoins(boostCost);

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
