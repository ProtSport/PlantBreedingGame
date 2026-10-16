using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlantBreeding.Core;
using PlantBreeding.Economy;
using PlantBreeding.Garden;
using PlantBreeding.Shop;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Картка рослини — тап на рослину, що росте (GardenManager.GrowingPlotTapped):
    /// назва, стадія, рідкість, скільки лишилось рости, горщик і його бонус,
    /// ціна продажу (зараз і з повним доглядом), полив і сонце.
    /// Звідси — екран догляду (сцена «Care») і прискорення за кристали
    /// (час, що лишився, стає в 10 разів коротшим, EconomyService.TrySpeedUp).
    /// Якщо кристалів бракує — кнопка прискорення веде в Крамницю.
    /// </summary>
    public partial class EconomyOverlayView
    {
        private GameObject _cardModal;
        private TMP_Text _cardTitle, _cardSub, _cardRemaining;
        private Image _cardPlant;
        private RectTransform _cardFill;
        private TMP_Text _cardPot, _cardPrice, _cardWater, _cardSun;
        private ClaimButton _cardCare, _cardSpeed;
        private PlotSlot _cardSlot;
        private float _cardTimer;

        private void BuildPlantCardModal()
        {
            _cardModal = MakeModal("PlantCardModal", ClosePlantCard, out var panel);

            _cardTitle = MakeLabel(panel, "Title", "", fontHead, 28, UIColors.Text, FontStyles.Normal);
            SetHeight(_cardTitle.gameObject, 32);
            _cardTitle.margin = new Vector4(0, 0, 40, 0); // місце під хрестик
            _cardTitle.enableAutoSizing = true; _cardTitle.fontSizeMin = 16; _cardTitle.fontSizeMax = 28;

            _cardSub = MakeLabel(panel, "Sub", "", fontUi, 12, UIColors.Soft, FontStyles.Normal);
            SetHeight(_cardSub.gameObject, 16);

            // Рослина на «сцені» — підкладка з м'яким світлом, спрайт поточної стадії.
            var stage = MakeRow(panel, "Stage", 150, UIColors.Rgba(Color.white, 0.04f));
            _cardPlant = MakeImage(stage, "Plant", null, Color.white, Image.Type.Simple);
            _cardPlant.preserveAspect = true;
            Stretch(_cardPlant.rectTransform);
            _cardPlant.rectTransform.offsetMin = new Vector2(0, 6);
            _cardPlant.rectTransform.offsetMax = new Vector2(0, -6);

            _cardRemaining = MakeLabel(panel, "Remaining", "", fontUi, 14, UIColors.Green, FontStyles.Bold);
            SetHeight(_cardRemaining.gameObject, 18);

            var bar = MakeImage(panel, "Bar", sprPill, UIColors.Rgba(Color.white, 0.10f), Image.Type.Sliced);
            SetHeight(bar.gameObject, 6);
            var fill = MakeImage(bar.transform, "Fill", sprPill, UIColors.Green, Image.Type.Sliced);
            _cardFill = fill.rectTransform;
            _cardFill.anchorMin = Vector2.zero; _cardFill.anchorMax = new Vector2(0, 1);
            _cardFill.offsetMin = Vector2.zero; _cardFill.offsetMax = Vector2.zero;

            _cardPot = MakeInfoRow(panel, "Горщик");
            _cardPrice = MakeInfoRow(panel, "Ціна продажу");
            _cardWater = MakeInfoRow(panel, "Полив");
            _cardSun = MakeInfoRow(panel, "Сонце");

            _cardCare = MakeClaimButton(panel, "Догляд", OnCardCare);
            SetHeight(_cardCare.button.gameObject, 44);

            _cardSpeed = MakeClaimButton(panel, "", OnCardSpeedUp);
            SetHeight(_cardSpeed.button.gameObject, 44);
        }

        /// <summary>Рядок «назва — значення»: назва зліва тьмяна, значення справа.</summary>
        private TMP_Text MakeInfoRow(Transform parent, string caption)
        {
            var row = new GameObject(caption, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            SetHeight(row, 18);

            var name = MakeLabel(row.transform, "Caption", caption, fontUi, 12, UIColors.Soft, FontStyles.Normal);
            Stretch(name.rectTransform);

            var value = MakeLabel(row.transform, "Value", "", fontUi, 12, UIColors.Text, FontStyles.Bold);
            Stretch(value.rectTransform);
            value.rectTransform.offsetMin = new Vector2(96, 0);
            value.alignment = TextAlignmentOptions.MidlineRight;
            value.enableAutoSizing = true; value.fontSizeMin = 9; value.fontSizeMax = 12;
            return value;
        }

        internal void OpenPlantCard(PlotSlot slot)
        {
            if (_cardModal == null || slot == null || slot.plant == null) return;
            _cardSlot = slot;
            _cardModal.SetActive(true);
            _cardTimer = 0f;
            RefreshPlantCard();
        }

        internal void ClosePlantCard()
        {
            _cardSlot = null;
            if (_cardModal != null) _cardModal.SetActive(false);
        }

        private void OnCardCare()
        {
            var slot = _cardSlot;
            var garden = GardenManager.Current;
            ClosePlantCard();
            if (garden != null) garden.OpenCareScreen(slot);
        }

        private void OnCardSpeedUp()
        {
            if (_cardSlot == null || !EconomyService.CanSpeedUp(_cardSlot)) return;
            var result = EconomyService.TrySpeedUp(_cardSlot);
            if (result == EconomyService.SpeedUpResult.NotEnoughGems) OpenShop();
            else RefreshPlantCard();
        }

        /// <summary>Раз на секунду оновлює таймер відкритої картки; закриває, якщо рослина вже дозріла.</summary>
        private void UpdatePlantCard()
        {
            if (_cardSlot == null || _cardModal == null || !_cardModal.activeSelf) return;
            _cardTimer -= Time.unscaledDeltaTime;
            if (_cardTimer > 0f) return;
            _cardTimer = 1f;
            RefreshPlantCard();
        }

        private void RefreshPlantCard()
        {
            if (_cardSlot == null || _cardModal == null || !_cardModal.activeSelf) return;
            var slot = _cardSlot;
            if (slot.plant == null || slot.state == PlotState.Ready || slot.state == PlotState.Empty)
            {
                ClosePlantCard();
                return;
            }

            var plant = slot.plant;
            float progress = slot.GetGrowthProgress01();
            _cardTitle.text = plant.displayName;
            _cardSub.text = $"{SeedCapsule.RarityName(plant.rarity)} · {StageName(progress)}";
            _cardPlant.sprite = plant.GetGrowthSprite(progress);
            _cardPlant.color = plant.tint;
            _cardFill.anchorMax = new Vector2(progress, 1);

            double remaining = slot.GetRemainingSeconds();
            _cardRemaining.text = $"До врожаю: {TimeFormat.Remaining(remaining)}";

            // ── Горщик і ціна ──
            var pot = slot.pot;
            _cardPot.text = pot == null ? "Без горщика"
                : pot.effectLabel == "без бонусу" || string.IsNullOrEmpty(pot.effectLabel) ? pot.displayName
                : $"{pot.displayName} · {pot.effectLabel.Replace("\n", " · ")}";

            int basePrice = PlantingForecast.SellPriceWithBonuses(plant, pot);
            int now = Mathf.RoundToInt(basePrice * EconomyService.CareMultiplier(slot.wateredCount, slot.sunCount));
            int best = Mathf.RoundToInt(basePrice * EconomyService.CareMultiplier(EconomyConfig.MaxCareWaterings, EconomyConfig.MaxSunnings));
            _cardPrice.text = now < best ? $"{now} монет · з доглядом до {best}" : $"{now} монет";

            _cardWater.text = slot.state == PlotState.NeedsWater ? $"{slot.wateredCount} з {EconomyConfig.MaxCareWaterings} · хоче пити"
                : $"{slot.wateredCount} з {EconomyConfig.MaxCareWaterings}";
            _cardSun.text = slot.WantsSun ? $"{slot.sunCount} з {EconomyConfig.MaxSunnings} · хоче сонця"
                : $"{slot.sunCount} з {EconomyConfig.MaxSunnings}";

            // ── Догляд: підсвічується, коли рослина чогось просить ──
            string need = slot.state == PlotState.NeedsWater ? "Догляд · хоче пити"
                : slot.WantsSun ? "Догляд · хоче сонця" : "Догляд";
            SetClaim(_cardCare, true, need);
            if (need == "Догляд") _cardCare.bg.color = UIColors.Rgba(UIColors.Green, 0.75f);

            // ── Прискорення ──
            if (slot.speedUpUsed)
            {
                SetClaim(_cardSpeed, false, "Уже прискорено");
                return;
            }
            var gm = GameManager.Instance;
            int gems = gm != null ? gm.playerData.gems : 0;
            int cost = EconomyService.SpeedUpCost(slot);
            if (gems >= cost)
            {
                SetClaim(_cardSpeed, true, $"Прискорити ×10 · {cost} {GemsWord(cost)}");
                _cardSpeed.bg.color = UIColors.Blue;
            }
            else
            {
                SetClaim(_cardSpeed, true, $"Потрібно {cost} {GemsWord(cost)} — купити");
                _cardSpeed.bg.color = UIColors.GoldLt;
            }
        }

        /// <summary>Стадія за тими самими порогами, що й картинки росту (25/50/75%).</summary>
        internal static string StageName(float progress01) =>
            progress01 < 0.25f ? "Паросток" : progress01 < 0.50f ? "Росте" : progress01 < 0.75f ? "Підростає" : "Майже доросла";

        /// <summary>1 кристал / 2 кристали / 5 кристалів.</summary>
        internal static string GemsWord(int n)
        {
            int mod100 = n % 100, mod10 = n % 10;
            if (mod100 >= 11 && mod100 <= 14) return "кристалів";
            if (mod10 == 1) return "кристал";
            if (mod10 >= 2 && mod10 <= 4) return "кристали";
            return "кристалів";
        }
    }
}
