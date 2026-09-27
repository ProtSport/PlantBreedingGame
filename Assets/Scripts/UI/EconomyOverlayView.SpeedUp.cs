using TMPro;
using UnityEngine;
using PlantBreeding.Core;
using PlantBreeding.Economy;
using PlantBreeding.Garden;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Вікно «Прискорити ріст» — тап на рослину, що росте (GardenManager.GrowingPlotTapped).
    /// Час, що лишився, стає в 10 разів коротшим (EconomyService.TrySpeedUp).
    /// Якщо кристалів бракує — кнопка веде в Крамницю.
    /// </summary>
    public partial class EconomyOverlayView
    {
        private GameObject _speedModal;
        private TMP_Text _speedTitle, _speedRemaining, _speedBalance;
        private ClaimButton _speedAction;
        private PlotSlot _speedSlot;
        private float _speedTimer;

        private void BuildSpeedUpModal()
        {
            _speedModal = MakeModal("SpeedUpModal", CloseSpeedUp, out var panel);

            _speedTitle = MakeLabel(panel, "Title", "", fontHead, 28, UIColors.Text, FontStyles.Normal);
            SetHeight(_speedTitle.gameObject, 32);
            _speedTitle.margin = new Vector4(0, 0, 40, 0); // місце під хрестик
            _speedTitle.enableAutoSizing = true; _speedTitle.fontSizeMin = 16; _speedTitle.fontSizeMax = 28;

            _speedRemaining = MakeLabel(panel, "Remaining", "", fontUi, 14, UIColors.Green, FontStyles.Bold);
            SetHeight(_speedRemaining.gameObject, 18);

            var desc = MakeLabel(panel, "Desc",
                "Рослина дорастає в 10 разів швидше: лишається 10% часу. Один раз на посадку.",
                fontUi, 12, UIColors.Soft, FontStyles.Normal);
            desc.textWrappingMode = TextWrappingModes.Normal;
            SetHeight(desc.gameObject, 34);

            _speedBalance = MakeLabel(panel, "Balance", "", fontUi, 12, UIColors.Blue, FontStyles.Bold);
            SetHeight(_speedBalance.gameObject, 16);

            _speedAction = MakeClaimButton(panel, "", OnSpeedUpAction);
            SetHeight(_speedAction.button.gameObject, 44);

            var close = MakeClaimButton(panel, "Закрити", CloseSpeedUp);
            SetHeight(close.button.gameObject, 40);
            close.bg.color = UIColors.Rgba(Color.white, 0.08f);
        }

        private void OpenSpeedUp(PlotSlot slot)
        {
            if (_speedModal == null || slot == null || slot.plant == null) return;
            _speedSlot = slot;
            _speedModal.SetActive(true);
            RefreshSpeedUp();
        }

        private void CloseSpeedUp()
        {
            _speedSlot = null;
            if (_speedModal != null) _speedModal.SetActive(false);
        }

        private void OnSpeedUpAction()
        {
            if (_speedSlot == null) return;
            if (!EconomyService.CanSpeedUp(_speedSlot))
            {
                CloseSpeedUp();
                return;
            }

            var result = EconomyService.TrySpeedUp(_speedSlot);
            if (result == EconomyService.SpeedUpResult.NotEnoughGems) OpenShop();
            else if (result == EconomyService.SpeedUpResult.Ok) CloseSpeedUp();
        }

        /// <summary>Раз на секунду оновлює таймер відкритого вікна; закриває, якщо рослина вже дозріла.</summary>
        private void UpdateSpeedUp()
        {
            if (_speedSlot == null || _speedModal == null || !_speedModal.activeSelf) return;
            _speedTimer -= Time.unscaledDeltaTime;
            if (_speedTimer > 0f) return;
            _speedTimer = 1f;
            RefreshSpeedUp();
        }

        private void RefreshSpeedUp()
        {
            if (_speedSlot == null || _speedModal == null || !_speedModal.activeSelf) return;
            if (_speedSlot.plant == null || _speedSlot.state == PlotState.Ready || _speedSlot.state == PlotState.Empty)
            {
                CloseSpeedUp();
                return;
            }

            var gm = GameManager.Instance;
            int gems = gm != null ? gm.playerData.gems : 0;
            _speedTitle.text = _speedSlot.plant.displayName;
            double remaining = _speedSlot.GetRemainingSeconds();
            double after = remaining * (1f - EconomyConfig.SpeedUpFraction);
            _speedRemaining.text = _speedSlot.speedUpUsed
                ? $"До врожаю: {TimeFormat.Remaining(remaining)}"
                : $"До врожаю: {TimeFormat.Remaining(remaining)} → {TimeFormat.Remaining(after)}";
            _speedBalance.text = $"У тебе: {gems} {GemsWord(gems)}";

            if (_speedSlot.speedUpUsed)
            {
                SetClaim(_speedAction, false, "Уже прискорено");
                return;
            }

            int cost = EconomyService.SpeedUpCost(_speedSlot);
            if (gems >= cost)
            {
                SetClaim(_speedAction, true, $"Прискорити ×10 · {cost} {GemsWord(cost)}");
                _speedAction.bg.color = UIColors.Blue;
            }
            else
            {
                SetClaim(_speedAction, true, $"Потрібно {cost} {GemsWord(cost)} — купити");
                _speedAction.bg.color = UIColors.GoldLt;
            }
        }

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
